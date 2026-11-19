using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Audio;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace HowFrame
{
    public static class AudioManager
    {
        private static GameObject _audioManagerObject;
        private static GameObject _audioPool;

        private static readonly Dictionary<string, AudioSource> MusicSources = new Dictionary<string, AudioSource>();
        private static readonly Dictionary<string, AudioClip> MusicClips = new Dictionary<string, AudioClip>();

        private static readonly Dictionary<string, AudioClip> SoundClips = new Dictionary<string, AudioClip>();
        private static readonly Dictionary<string, int> SoundFreq = new Dictionary<string, int>();
        private static readonly List<string> LruList = new List<string>();
        private static readonly Queue<AudioSource> SoundSources = new Queue<AudioSource>();
        private static readonly HashSet<string> SoundCheck = new HashSet<string>();

        private static float _lastCleanupTime = 0;

        private static AudioMixer _mixer;
        private static AudioMixerGroup _musicGroup;
        private static AudioMixerGroup _soundGroup;

        private const int MaxSoundCache = 50; // 最大缓存音效数量

        private static void Init()
        {
            if (_audioManagerObject != null) return;

            _audioManagerObject = new GameObject("AudioManager");
            _audioPool = new GameObject("AudioPool");
            Object.DontDestroyOnLoad(_audioManagerObject);
            Object.DontDestroyOnLoad(_audioPool);

            _audioManagerObject.AddComponent<FakeMono>();
            _mixer = AssetAssistant.AddressableGet<AudioMixer>("AudioMixer");
            if (_mixer != null)
            {
                _musicGroup = _mixer.FindMatchingGroups("Music").FirstOrDefault();
                _soundGroup = _mixer.FindMatchingGroups("Sound").FirstOrDefault();
            }
        }

        #region Music

        // 音乐按名字管理：同一首同时只有一个音源。
        // 淡出期间这首歌还在 MusicSources 里——这时再 AddMusic 同一首，会取消淡出、从当前音量淡回来接着放（不重新开始，没有空白）；
        // 再 EndMusic 同一首会被忽略（不会对同一个音源跑两遍淡出、回收两次）。
        private static readonly Dictionary<string, float> MusicVolumes = new Dictionary<string, float>();      // 每首的目标音量
        private static readonly Dictionary<string, Coroutine> MusicFades = new Dictionary<string, Coroutine>(); // 正在进行的淡入 / 淡出
        private static readonly HashSet<string> MusicFadingOut = new HashSet<string>();

        public const float MusicFadeOutTime = 2f;
        public const float MusicFadeInTime = 0.6f;

        private static FakeMono Host => _audioManagerObject.GetComponent<FakeMono>();

        public static void AddMusic(string fileName, float delay = 0, float volume = 1,
            GameObject father = null, float minDis = 1, float maxDis = 10)
        {
            try
            {
                if (TryResumeMusic(fileName, volume)) return;

                AudioClip clip;
                if (!MusicClips.TryGetValue(fileName, out clip))
                {
                    clip = SafeLoad<AudioClip>(fileName);
                    if (!clip) return;
                    MusicClips[fileName] = clip;
                }

                AudioSource source = GetSoundSource();
                SetupAudioSource(source, clip, father, volume, minDis, maxDis, true, true);

                MusicSources[fileName] = source;
                MusicVolumes[fileName] = volume;

                Host.StartCoroutine(PlayMusicCoroutine(delay, source));
            }
            catch (Exception ex)
            {
                Debug.LogError($"AddMusic {fileName} failed: {ex}");
            }
        }

        /// <summary>播放已经加载好的音乐（按地址单独加载的音乐走这里）</summary>
        public static void AddMusic(string key, AudioClip clip, float volume)
        {
            if (!clip || TryResumeMusic(key, volume)) return;

            MusicClips[key] = clip;
            AudioSource source = GetSoundSource();
            SetupAudioSource(source, clip, null, volume, 1, 10, true, true);
            MusicSources[key] = source;
            MusicVolumes[key] = volume;
            source.Play();
        }

        public static void EndMusic(string fileName)
        {
            if (!MusicSources.TryGetValue(fileName, out var source) || source == null) return;
            if (MusicFadingOut.Contains(fileName)) return; // 已经在淡出

            StopFade(fileName);
            MusicFadingOut.Add(fileName);
            MusicFades[fileName] = Host.StartCoroutine(EndMusicCoroutine(fileName, source, MusicFadeOutTime));
        }

        /// <summary>这首已经在播：正在淡出就取消淡出、淡回目标音量；返回 true 表示不用再新建音源</summary>
        private static bool TryResumeMusic(string fileName, float volume)
        {
            if (!MusicSources.TryGetValue(fileName, out var source)) return false;
            if (source == null)
            {
                ForgetMusic(fileName);
                return false;
            }
            MusicVolumes[fileName] = volume;
            if (!MusicFadingOut.Remove(fileName)) return true; // 正常在播，什么都不用做
            StopFade(fileName);
            MusicFades[fileName] = Host.StartCoroutine(FadeMusicCoroutine(fileName, source, Mathf.Clamp01(volume), MusicFadeInTime));
            return true;
        }

        private static void StopFade(string fileName)
        {
            if (MusicFades.TryGetValue(fileName, out var co) && co != null) Host.StopCoroutine(co);
            MusicFades.Remove(fileName);
        }

        private static void ForgetMusic(string fileName)
        {
            MusicSources.Remove(fileName);
            MusicClips.Remove(fileName);
            MusicVolumes.Remove(fileName);
            MusicFades.Remove(fileName);
            MusicFadingOut.Remove(fileName);
        }

        public static void ChangeMusicVolume(float vol)
        {
            if (_mixer == null) return;
            _mixer.SetFloat("MusicVol", Mathf.Log10(Mathf.Clamp01(vol)) * 20);
        }

        public static void ChangeSoundVolume(float vol)
        {
            if (_mixer == null) return;
            _mixer.SetFloat("SoundVol", Mathf.Log10(Mathf.Clamp01(vol)) * 20);
        }

        private static IEnumerator PlayMusicCoroutine(float delay, AudioSource source)
        {
            yield return new WaitForSeconds(delay);
            if (source != null) source.Play();
        }

        // 淡入淡出都用真实时间：timeScale = 0（暂停）时也能走完
        private static IEnumerator FadeMusicCoroutine(string fileName, AudioSource source, float target, float fadeTime)
        {
            float startVol = source.volume;
            float elapsed = 0f;
            while (elapsed < fadeTime && source != null)
            {
                source.volume = Mathf.Lerp(startVol, target, elapsed / fadeTime);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (source != null) source.volume = target;
            MusicFades.Remove(fileName);
        }

        private static IEnumerator EndMusicCoroutine(string fileName, AudioSource source, float fadeTime)
        {
            yield return FadeMusicCoroutine(fileName, source, 0f, fadeTime);
            if (source != null)
            {
                source.Stop();
                ReturnSourceToPool(source);
            }
            ForgetMusic(fileName);
        }

        #endregion

        #region Sound

        public static void AddSound(string fileName, float delayTime = 0, float volume = 1, int types = 1,
            GameObject father = null, float minDis = 1, float maxDis = 10)
        {
            try
            {
                string tag = fileName + (father ? father.name : "null");
                if (SoundCheck.Contains(tag)) return;
                SoundCheck.Add(tag);

                string soundName = types > 1 ? fileName + Random.Range(1, types + 1) : fileName;

                AudioClip clip = GetSoundClip(soundName);
                if (!clip) return;

                AudioSource source = GetSoundSource();
                SetupAudioSource(source, clip, father, volume, minDis, maxDis, false, false);

                _audioManagerObject.GetComponent<FakeMono>()
                    .StartCoroutine(PlaySoundCoroutine(delayTime, source, tag));
            }
            catch (Exception ex)
            {
                Debug.LogError($"AddSound {fileName} failed: {ex}");
            }
        }

        private static IEnumerator PlaySoundCoroutine(float delayTime, AudioSource source, string tag)
        {
            yield return new WaitForSeconds(delayTime);

            SoundCheck.Remove(tag);
            // 等待期间 source 可能已经被回收 / 复用（clip 被清掉或换了），这时不能再碰它
            if (source == null || source.clip == null) yield break;
            var clip = source.clip;
            source.Play();

            yield return new WaitForSeconds(clip.length + 0.1f);

            if (source == null || source.clip != clip) yield break;
            source.Stop();
            ReturnSourceToPool(source);
        }

        private static AudioClip GetSoundClip(string name)
        {
            if (Time.time - _lastCleanupTime > 30)
            {
                _lastCleanupTime = Time.time;
                CleanupSoundCache();
            }

            if (!SoundClips.TryGetValue(name, out var clip))
            {
                clip = SafeLoad<AudioClip>(name);
                if (!clip) return null;

                SoundClips[name] = clip;
                SoundFreq[name] = 1;
                LruList.Add(name);

                if (SoundClips.Count > MaxSoundCache)
                    CleanupSoundCache();
            }
            else
            {
                SoundFreq[name]++;
                LruList.Remove(name);
                LruList.Add(name);
            }

            return clip;
        }

        #endregion

        #region Helpers

        private static void CleanupSoundCache()
        {
            while (SoundClips.Count > MaxSoundCache)
            {
                string oldest = LruList[0];
                LruList.RemoveAt(0);
                SoundFreq.Remove(oldest);
                // 音频由 AssetAssistant（Addressables）持有，只清本地缓存，不能 Resources.UnloadAsset（之后再播会拿到已卸载的音频）
                SoundClips.Remove(oldest);
            }

            foreach (var key in SoundFreq.Keys.ToList())
            {
                SoundFreq[key] -= 2;
                if (SoundFreq[key] <= 0)
                {
                    LruList.Remove(key);
                    SoundFreq.Remove(key);
                    if (SoundClips.TryGetValue(key, out var clip))
                    {
                        // 音频由 AssetAssistant（Addressables）持有，只清本地缓存，不能 Resources.UnloadAsset（之后再播会拿到已卸载的音频）
                        SoundClips.Remove(key);
                    }
                }
            }
        }

        private static void ReturnSourceToPool(AudioSource source)
        {
            // 已经在池里的（隐藏的）不重复入队：重复入队会把同一个 source 同时发给两个使用者
            if (source == null || !source.gameObject.activeSelf) return;
            source.clip = null;
            source.gameObject.SetActive(false);
            source.transform.SetParent(_audioPool.transform);
            SoundSources.Enqueue(source);
        }

        private static AudioSource GetSoundSource()
        {
            if (SoundSources.Count > 0) return SoundSources.Dequeue();

            var go = new GameObject("PooledAudioSource");
            go.transform.SetParent(_audioPool.transform);
            return go.AddComponent<AudioSource>();
        }

        private static void SetupAudioSource(AudioSource source, AudioClip clip, GameObject father,
            float volume, float minDis, float maxDis, bool loop, bool isMusic)
        {
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.volume = Mathf.Clamp01(volume);
            source.outputAudioMixerGroup = isMusic ? _musicGroup : _soundGroup;

            source.gameObject.SetActive(true);
            source.transform.SetParent(father ? father.transform : _audioManagerObject.transform);
            source.transform.localPosition = Vector3.zero;

            if (father != null)
            {
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = minDis;
                source.maxDistance = maxDis;
            }
            else
            {
                source.spatialBlend = 0f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 1f;
                source.maxDistance = 500f;
            }
        }

        private static T SafeLoad<T>(string path) where T : Object
        {
            try
            {
                return AssetAssistant.AddressableGet<T>(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Audio load failed: {path}, {ex}");
                return null;
            }
        }

        private class FakeMono : MonoBehaviour { }

        /// <summary>
        /// 初始化 AudioManager（延迟初始化，在资源加载完成后调用）
        /// </summary>
        public static void Wake()
        {
            Init();
        }
        #endregion
    }
}
