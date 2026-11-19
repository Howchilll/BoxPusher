using System.Collections.Generic;
using HowFrame;

namespace BoxPusher
{
    public sealed class LevelRecord
    {
        public int id;
        public int bestMoves;
        public int bestPushes;
        public bool cleared;
    }

    public sealed class SaveFile
    {
        public List<LevelRecord> records = new();
        public float musicVolume = 0.6f;
        public float soundVolume = 0.8f;
        public int lastLevel = 1;
    }

    /// <summary>存档（通关记录 + 设置），HowFrame DataAssistant 写 JSON：persistentDataPath/Save/BoxPusher.json</summary>
    public static class Progress
    {
        private const string FileName = "BoxPusher";
        private const string Folder = "Save";
        private static SaveFile _data;

        public static SaveFile Data
        {
            get
            {
                if (_data != null) return _data;
                try { _data = DataAssistant.ReadData<SaveFile>(FileName, true, Folder); }
                catch (System.Exception e) { UnityEngine.Debug.LogWarning($"[Progress] 存档读取失败，使用新存档: {e.Message}"); }
                _data ??= new SaveFile();
                _data.records ??= new List<LevelRecord>();
                return _data;
            }
        }

        public static void Save() => DataAssistant.WriteData(Data, FileName, true, Folder);

        public static LevelRecord Get(int id)
        {
            foreach (var r in Data.records)
                if (r.id == id) return r;
            return null;
        }

        public static bool IsCleared(int id) => Get(id)?.cleared == true;

        /// <summary>按关卡列表顺序解锁：第一关和"前一关已通关"的关卡可玩</summary>
        public static bool IsUnlocked(IReadOnlyList<int> order, int index) =>
            index <= 0 || IsCleared(order[index - 1]) || IsCleared(order[index]);

        /// <summary>记录一次通关，返回是否刷新了最少步数</summary>
        public static bool Record(int id, int moves, int pushes)
        {
            var r = Get(id);
            if (r == null)
            {
                r = new LevelRecord { id = id };
                Data.records.Add(r);
            }
            var better = !r.cleared || moves < r.bestMoves;
            if (better)
            {
                r.bestMoves = moves;
                r.bestPushes = pushes;
            }
            r.cleared = true;
            Save();
            return better;
        }
    }
}
