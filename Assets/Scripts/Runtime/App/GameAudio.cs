using System.Collections.Generic;
using HowFrame;
using UnityEngine;

namespace BoxPusher
{
    /// <summary>
    /// 声音入口：业务只写逻辑 Id，底层是 HowFrame AudioManager（文件名 = GameRes/Audio 下的 Addressables 地址，启动时按 Audio 标签预加载）。
    /// 音频沿用 ZombieGame 用 numpy 合成的芯片音效。
    /// </summary>
    public static class GameAudio
    {
        private static readonly Dictionary<string, (string clip, float volume, float cooldown)> Sounds = new()
        {
            ["click"] = ("sfx_click", 0.6f, 0.05f),
            ["step"] = ("sfx_click", 0.15f, 0.06f),
            ["push"] = ("sfx_swing", 0.5f, 0.05f),
            ["goal"] = ("sfx_pickup", 0.8f, 0.05f),
            ["portal"] = ("sfx_zap", 0.6f, 0.05f),
            ["slide"] = ("sfx_reroll", 0.45f, 0.05f),
            ["gate"] = ("sfx_wave_start", 0.35f, 0.1f),
            ["deny"] = ("sfx_deny", 0.4f, 0.15f),
            ["undo"] = ("sfx_reroll", 0.3f, 0.05f),
            ["win"] = ("sfx_victory", 0.9f, 0.5f),
            ["save"] = ("sfx_levelup", 0.7f, 0.2f),
        };

        private static readonly Dictionary<string, string> Musics = new()
        {
            ["menu"] = "bgm_menu",
            ["play"] = "bgm_battle",
        };

        private static readonly Dictionary<string, float> NextPlay = new();
        private static string _music;

        public static void Play(string id)
        {
            if (!Application.isPlaying || !Sounds.TryGetValue(id, out var s)) return;
            var now = Time.unscaledTime;
            if (NextPlay.TryGetValue(id, out var next) && now < next) return;
            NextPlay[id] = now + s.cooldown;
            EnsureListener();
            AudioManager.AddSound(s.clip, 0, s.volume);
        }

        /// <summary>同一首重复调用不会重来；null 停止</summary>
        public static void Music(string id)
        {
            if (!Application.isPlaying || id == _music) return;
            EnsureListener();
            if (_music != null && Musics.TryGetValue(_music, out var old)) AudioManager.EndMusic(old);
            _music = id;
            if (id != null && Musics.TryGetValue(id, out var clip)) AudioManager.AddMusic(clip, 0, 0.5f);
        }

        public static void ApplyVolumes()
        {
            AudioManager.ChangeMusicVolume(Progress.Data.musicVolume);
            AudioManager.ChangeSoundVolume(Progress.Data.soundVolume);
        }

        private static AudioListener _listener;

        /// <summary>HowFrame 的 Canvas 预制体里带的 AudioListener 是禁用的，只认启用中的；没有就补一个常驻的</summary>
        public static void EnsureListener()
        {
            if (_listener != null && _listener.isActiveAndEnabled) return;
            _listener = null;
            foreach (var l in Object.FindObjectsOfType<AudioListener>())
                if (l.isActiveAndEnabled) { _listener = l; return; }
            var go = new GameObject("AudioListener");
            Object.DontDestroyOnLoad(go);
            _listener = go.AddComponent<AudioListener>();
        }
    }
}
