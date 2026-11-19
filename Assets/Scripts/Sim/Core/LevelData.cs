using System;
using System.Collections.Generic;

namespace BoxPusher.Sim
{
    /// <summary>
    /// 关卡的存盘格式（正式关卡和草稿共用）。只有公有字段 + 无参构造，LitJson / System.Text.Json 都能直接读写。
    /// 格子下标 cell = y * width + x，y = 0 是最上面一行。
    /// </summary>
    [Serializable]
    public sealed class LevelData
    {
        public int id;
        public string name = "";
        public int width;
        public int height;
        /// <summary>height 行，每行 width 个字符，编码见 <see cref="TileCodec"/></summary>
        public string[] terrain = Array.Empty<string>();
        public int player = -1;
        public int[] boxes = Array.Empty<int>();
        /// <summary>石头（压机关板用，不算目标），和机关门 / 机关板一起出现</summary>
        public int[] stones = Array.Empty<int>();

        // ---- 由求解器写入（保存时自动计算），只用于展示 ----
        public int parPushes;   // 最少推箱次数（0 = 未求解）
        public int parMoves;    // 对应解法的总步数
        public int stars;       // 难度 1~5（0 = 未评估）
        public int score;       // 难度分

        public string note = "";
        public string savedAt = "";

        public static LevelData CreateEmpty(int width, int height)
        {
            var d = new LevelData { width = width, height = height, terrain = new string[height] };
            for (var y = 0; y < height; y++)
            {
                var row = new char[width];
                for (var x = 0; x < width; x++)
                    row[x] = x == 0 || y == 0 || x == width - 1 || y == height - 1 ? '#' : '-';
                d.terrain[y] = new string(row);
            }
            return d;
        }

        public LevelData Clone()
        {
            var c = (LevelData)MemberwiseClone();
            c.terrain = (string[])terrain.Clone();
            c.boxes = (int[])boxes.Clone();
            c.stones = (int[])(stones ?? Array.Empty<int>()).Clone();
            return c;
        }

        public Tile TileAt(int x, int y, out int portalPair)
        {
            portalPair = -1;
            if (x < 0 || y < 0 || x >= width || y >= height || y >= terrain.Length) return Tile.Void;
            var row = terrain[y];
            return x < row.Length ? TileCodec.FromChar(row[x], out portalPair) : Tile.Void;
        }

        public void SetTile(int x, int y, Tile t, int portalPair = 0)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            var row = terrain[y].PadRight(width, ' ').ToCharArray();
            row[x] = TileCodec.ToChar(t, portalPair);
            terrain[y] = new string(row);
        }

        public bool HasBox(int cell) => Array.IndexOf(boxes, cell) >= 0;
        public bool HasStone(int cell) => stones != null && Array.IndexOf(stones, cell) >= 0;

        public void SetStone(int cell, bool on)
        {
            var list = new List<int>(stones ?? Array.Empty<int>());
            list.Remove(cell);
            if (on) list.Add(cell);
            list.Sort();
            stones = list.ToArray();
        }

        public void SetBox(int cell, bool on)
        {
            var list = new List<int>(boxes);
            list.Remove(cell);
            if (on) list.Add(cell);
            list.Sort();
            boxes = list.ToArray();
        }

        /// <summary>改尺寸，保留左上角已有内容，新出来的区域填空地，外圈补墙</summary>
        public LevelData Resized(int newW, int newH)
        {
            var d = CreateEmpty(newW, newH);
            d.id = id;
            d.name = name;
            d.note = note;
            for (var y = 0; y < Math.Min(height, newH); y++)
            for (var x = 0; x < Math.Min(width, newW); x++)
            {
                var t = TileAt(x, y, out var pp);
                d.SetTile(x, y, t, Math.Max(pp, 0));
            }
            if (player >= 0 && player % width < newW && player / width < newH)
                d.player = player / width * newW + player % width;
            var nb = new List<int>();
            foreach (var b in boxes)
                if (b % width < newW && b / width < newH) nb.Add(b / width * newW + b % width);
            d.boxes = nb.ToArray();
            var ns = new List<int>();
            foreach (var st in stones ?? Array.Empty<int>())
                if (st % width < newW && st / width < newH) ns.Add(st / width * newW + st % width);
            d.stones = ns.ToArray();
            return d;
        }
    }
}
