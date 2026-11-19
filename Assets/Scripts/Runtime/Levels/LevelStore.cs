using System;
using System.Collections.Generic;
using System.IO;
using BoxPusher.Sim;
using HowFrame;
using LitJson;
using UnityEngine;

namespace BoxPusher
{
    /// <summary>草稿列表里的一项</summary>
    public sealed class DraftInfo
    {
        public int LevelId;
        public int Slot;
        public string Path;
        public LevelData Data;
    }

    /// <summary>
    /// 关卡读写。正式关卡 = 每个关卡 id 一个 JSON；草稿 = 每个关卡 id 下任意多个草稿位，只新增、不覆盖、不删除。
    ///
    /// 正式关卡放在 Assets/GameRes/Levels/level_001.json，走 Addressables（标签 Levels，GameInit 启动时预加载），
    /// 运行时用 HowFrame AssetAssistant 从缓存取 TextAsset。
    /// 保存：编辑器里写回 GameRes/Levels 并导入（HowFrame 的自动注册工具会加进 Addressables）；打包后写 persistentDataPath/Levels。
    /// 本次运行里保存过的关卡另外记在内存里，因为 Addressables 缓存还是启动时的内容。
    /// 草稿：编辑器里在工程根目录 LevelDrafts/（不进包），打包后在 persistentDataPath/LevelDrafts。
    /// </summary>
    public static class LevelStore
    {
        public const string Label = "Levels";
        public const string AssetFolder = "Assets/GameRes/Levels";
        private const string Prefix = "level_";

        private static readonly Dictionary<int, LevelData> Saved = new();

        /// <summary>打包后在编辑器里保存的正式关卡（覆盖包内同 id 关卡）</summary>
        private static string PlayerDir => Path.Combine(Application.persistentDataPath, "Levels");

        public static string DraftRoot =>
            Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "LevelDrafts"))
                : Path.Combine(Application.persistentDataPath, "LevelDrafts");

        private static string AssetName(int id) => $"{Prefix}{id:000}";

        private static bool TryParseId(string name, out int id)
        {
            id = 0;
            return name.StartsWith(Prefix, StringComparison.Ordinal) && int.TryParse(name.Substring(Prefix.Length), out id);
        }

        // ---------------- 正式关卡 ----------------

        /// <summary>所有存在的正式关卡 id（升序）</summary>
        public static List<int> OfficialIds()
        {
            var set = new SortedSet<int>(Saved.Keys);
            foreach (var name in ShippedNames())
                if (TryParseId(name, out var id)) set.Add(id);
            if (!Application.isEditor && Directory.Exists(PlayerDir))
                foreach (var f in Directory.GetFiles(PlayerDir, Prefix + "*.json"))
                    if (TryParseId(Path.GetFileNameWithoutExtension(f), out var id)) set.Add(id);
            return new List<int>(set);
        }

        public static bool HasOfficial(int id) => LoadOfficial(id) != null;

        public static LevelData LoadOfficial(int id)
        {
            if (Saved.TryGetValue(id, out var saved)) return saved.Clone();
            if (!Application.isEditor)
            {
                var path = Path.Combine(PlayerDir, AssetName(id) + ".json");
                if (File.Exists(path)) return Parse(File.ReadAllText(path), path);
            }
            var text = ShippedText(AssetName(id));
            return text == null ? null : Parse(text, AssetName(id));
        }

        /// <summary>
        /// 写正式关卡。覆盖已有关卡前，把旧版本自动存成一个新草稿（不会丢东西）。
        /// 返回自动备份的草稿位，没有旧版本时为 -1。
        /// </summary>
        public static int SaveOfficial(LevelData data)
        {
            var backup = -1;
            var old = LoadOfficial(data.id);
            if (old != null)
            {
                old.note = $"覆盖正式关卡前自动备份（原保存于 {old.savedAt}）";
                backup = SaveDraft(old);
            }
            data.savedAt = Now();
            Saved[data.id] = data.Clone();
#if UNITY_EDITOR
            var assetPath = $"{AssetFolder}/{AssetName(data.id)}.json";
            Directory.CreateDirectory(AssetFolder);
            Write(assetPath, data);
            UnityEditor.AssetDatabase.ImportAsset(assetPath); // 触发 HowFrame 自动注册 Addressables（Levels 组 / 标签）
#else
            Directory.CreateDirectory(PlayerDir);
            Write(Path.Combine(PlayerDir, AssetName(data.id) + ".json"), data);
#endif
            return backup;
        }

        // ---------------- 包内关卡（Addressables） ----------------

        private static IEnumerable<string> ShippedNames()
        {
#if UNITY_EDITOR
            // 非运行状态（EditMode 测试 / 编辑器工具）没有 Addressables 缓存，直接读资源文件
            if (!Application.isPlaying)
            {
                if (!Directory.Exists(AssetFolder)) yield break;
                foreach (var f in Directory.GetFiles(AssetFolder, Prefix + "*.json")) yield return Path.GetFileNameWithoutExtension(f);
                yield break;
            }
#endif
            foreach (var t in AssetAssistant.AddressableGetAll<TextAsset>(Prefix)) yield return t.name;
        }

        private static string ShippedText(string name)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var path = $"{AssetFolder}/{name}.json";
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
#endif
            return AssetAssistant.AddressableTryGet<TextAsset>(name, out var t) ? t.text : null;
        }

        // ---------------- 草稿 ----------------

        private static string DraftDir(int id) => Path.Combine(DraftRoot, $"level_{id:000}");

        /// <summary>新建一个草稿位并写入，返回草稿位编号（从 1 开始）。永远不覆盖已有草稿。</summary>
        public static int SaveDraft(LevelData data)
        {
            var dir = DraftDir(data.id);
            Directory.CreateDirectory(dir);
            var slot = 1;
            foreach (var d in ListDrafts(data.id)) slot = Math.Max(slot, d.Slot + 1);
            var copy = data.Clone();
            copy.savedAt = Now();
            Write(Path.Combine(dir, $"draft_{slot:00}.json"), copy);
            return slot;
        }

        public static List<DraftInfo> ListDrafts(int id)
        {
            var list = new List<DraftInfo>();
            var dir = DraftDir(id);
            if (!Directory.Exists(dir)) return list;
            foreach (var f in Directory.GetFiles(dir, "draft_*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(f);
                if (!int.TryParse(name.Substring("draft_".Length), out var slot)) continue;
                var data = Read(f);
                if (data != null) list.Add(new DraftInfo { LevelId = id, Slot = slot, Path = f, Data = data });
            }
            list.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            return list;
        }

        // ---------------- 底层 ----------------

        private static LevelData Read(string path) => Parse(File.ReadAllText(path), path);

        private static LevelData Parse(string json, string source)
        {
            try
            {
                var d = JsonMapper.ToObject<LevelData>(json);
                d.terrain ??= Array.Empty<string>();
                d.boxes ??= Array.Empty<int>();
                d.name ??= "";
                d.note ??= "";
                return d;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LevelStore] 读取失败 {source}: {e.Message}");
                return null;
            }
        }

        private static void Write(string path, LevelData data)
        {
            var writer = new JsonWriter { PrettyPrint = true };
            JsonMapper.ToJson(data, writer);
            File.WriteAllText(path, writer.ToString());
        }

        private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
