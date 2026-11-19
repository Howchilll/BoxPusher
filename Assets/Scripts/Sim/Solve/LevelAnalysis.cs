using System;
using System.Collections.Generic;
using System.Text;

namespace BoxPusher.Sim
{
    /// <summary>关卡结构校验：保存 / 试玩前跑一次，错误必须改，警告只提示</summary>
    public static class LevelValidator
    {
        public const int MaxGates = 1;
        public const int MaxPlates = 1;
        public const int MaxStones = 1;
        /// <summary>每个字母的传送门最多 2 个（一对）</summary>
        public const int PortalsPerPair = 2;

        public static List<string> Validate(LevelData d, List<string> warnings = null)
        {
            var errors = new List<string>();
            if (d.width < 3 || d.height < 3) errors.Add("尺寸至少 3×3");
            if (d.terrain == null || d.terrain.Length != d.height) { errors.Add("地形行数和高度不一致"); return errors; }

            var m = new LevelMap(d);
            if (d.player < 0 || d.player >= m.Size) errors.Add("没有放玩家出生点");
            else if (!Walkable(m.Tiles[d.player])) errors.Add("玩家出生点不能在墙 / 场外 / 机关门上");

            if (d.boxes.Length == 0) errors.Add("至少放一个箱子");
            if (m.Goals.Length == 0) errors.Add("至少放一个目标点");
            if (d.boxes.Length < m.Goals.Length) errors.Add($"箱子（{d.boxes.Length}）比目标点（{m.Goals.Length}）少，永远无法通关");
            if (d.boxes.Length > Solver.MaxBoxes) warnings?.Add($"箱子超过 {Solver.MaxBoxes} 个，求解器无法评估");

            var seen = new HashSet<int>();
            foreach (var b in d.boxes)
            {
                if (b < 0 || b >= m.Size) { errors.Add("箱子位置越界"); continue; }
                if (!seen.Add(b)) errors.Add($"({m.X(b)},{m.Y(b)}) 放了两个箱子");
                if (!Walkable(m.Tiles[b])) errors.Add($"({m.X(b)},{m.Y(b)}) 的箱子在墙 / 场外 / 机关门上");
                if (b == d.player) errors.Add("箱子和玩家重叠");
            }

            var portalCount = new int[TileCodec.MaxPortalPairs];
            for (var c = 0; c < m.Size; c++)
                if (m.Tiles[c] == Tile.Portal) portalCount[m.PortalPair[c]]++;
            for (var i = 0; i < portalCount.Length; i++)
                if (portalCount[i] != 0 && portalCount[i] != 2)
                    errors.Add($"传送门 {(char)('A' + i)} 有 {portalCount[i]} 个，必须正好一对（{PortalsPerPair} 个）");

            // 机关：机关门、机关板、石头必须一起出现，各一个
            var gates = 0;
            for (var c = 0; c < m.Size; c++)
                if (m.Tiles[c] == Tile.Gate) gates++;
            var stones = d.stones ?? Array.Empty<int>();
            if (gates > MaxGates) errors.Add($"机关门有 {gates} 个，一关只能有 {MaxGates} 个");
            if (m.Plates.Length > MaxPlates) errors.Add($"机关板有 {m.Plates.Length} 个，一关只能有 {MaxPlates} 个");
            if (stones.Length > MaxStones) errors.Add($"石头有 {stones.Length} 个，一关只能有 {MaxStones} 个");
            var anyMech = gates > 0 || m.Plates.Length > 0 || stones.Length > 0;
            if (anyMech && (gates == 0 || m.Plates.Length == 0 || stones.Length == 0))
            {
                var missing = new List<string>();
                if (gates == 0) missing.Add("机关门");
                if (m.Plates.Length == 0) missing.Add("机关板");
                if (stones.Length == 0) missing.Add("石头");
                errors.Add($"机关门、机关板、石头必须一起出现，缺少：{string.Join("、", missing)}");
            }

            foreach (var st in stones)
            {
                if (st < 0 || st >= m.Size) { errors.Add("石头位置越界"); continue; }
                if (m.Tiles[st] != Tile.Floor) errors.Add($"({m.X(st)},{m.Y(st)}) 的石头必须放在空地上");
                if (st == d.player || Array.IndexOf(d.boxes, st) >= 0) errors.Add($"({m.X(st)},{m.Y(st)}) 的石头和箱子 / 玩家重叠");
            }
            // 机关板一开始不能被压住，否则门从第一步起就是开的，机关没有意义
            foreach (var pl in m.Plates)
                if (Array.IndexOf(d.boxes, pl) >= 0 || Array.IndexOf(stones, pl) >= 0)
                    errors.Add($"({m.X(pl)},{m.Y(pl)}) 的机关板一开始不能压着箱子或石头");
            return errors;
        }

        private static bool Walkable(Tile t) => t != Tile.Wall && t != Tile.Void && t != Tile.Gate;
    }

    /// <summary>
    /// 难度评价：综合"最少推箱次数""搜索展开的状态数（分支 / 陷阱多少）""箱子数""用到几种机关"。
    /// 分数只用于相对排序和星级，公式见 Score。
    /// </summary>
    public static class Difficulty
    {
        public static int Score(LevelMap m, int boxCount, SolveResult r)
        {
            if (!r.Solved) return 0;
            var mechanics = (m.HasPortals ? 1 : 0) + (m.HasIce ? 1 : 0) + (m.HasGates ? 1 : 0);
            var search = Math.Log(r.Explored + 1, 2);
            var s = r.Pushes * 1.5 + search * 3.0 + boxCount * 2.0 + mechanics * 3.0 + r.Moves * 0.1;
            return (int)Math.Round(s);
        }

        public static int Stars(int score) =>
            score <= 0 ? 0 : score < 22 ? 1 : score < 38 ? 2 : score < 54 ? 3 : score < 70 ? 4 : 5;

        public static string StarText(int stars)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < 5; i++) sb.Append(i < stars ? '★' : '☆');
            return sb.ToString();
        }

        /// <summary>求解并把结果写回关卡（parPushes / parMoves / score / stars）</summary>
        public static SolveResult Evaluate(LevelData d, int maxStates = Solver.DefaultMaxStates)
        {
            var r = Solver.Solve(d, maxStates);
            var m = new LevelMap(d);
            d.parPushes = r.Solved ? r.Pushes : 0;
            d.parMoves = r.Solved ? r.Moves : 0;
            d.score = Score(m, d.boxes.Length, r);
            d.stars = Stars(d.score);
            return r;
        }
    }

    /// <summary>
    /// 纯文本关卡格式（XSB 扩展），方便手写 / 复制粘贴：
    /// '#' 墙  ' ' 或 '-' 空地  '_' 场外  '.' 目标  '@' 玩家  '+' 玩家在目标上  '$' 箱子  '*' 箱子在目标上
    /// '~' 冰面  '^' 机关板  '=' 机关门  'A'~'H' 传送门（同字母为一对）
    /// 'o' 石头（只能在空地上）  '%' 玩家在机关板上  'i' 玩家在冰面上  'b' 箱子在冰面上  '&amp;' 箱子在机关板上（校验不通过，仅用于读旧数据）
    /// 外圈墙以外的空格会自动变成场外。
    /// </summary>
    public static class LevelText
    {
        public static LevelData Parse(string text, int id = 0, string name = "")
        {
            var lines = new List<string>();
            foreach (var raw in text.Replace("\r", "").Split('\n'))
                if (raw.Trim().Length > 0) lines.Add(raw.TrimEnd());
            var w = 0;
            foreach (var l in lines) w = Math.Max(w, l.Length);
            var h = lines.Count;
            var d = new LevelData { id = id, name = name, width = w, height = h, terrain = new string[h] };
            var boxes = new List<int>();
            var stones = new List<int>();
            for (var y = 0; y < h; y++)
            {
                var row = new char[w];
                for (var x = 0; x < w; x++)
                {
                    var c = x < lines[y].Length ? lines[y][x] : '_';
                    var cell = y * w + x;
                    switch (c)
                    {
                        case '@': d.player = cell; c = '-'; break;
                        case '+': d.player = cell; c = '.'; break;
                        case '$': boxes.Add(cell); c = '-'; break;
                        case '*': boxes.Add(cell); c = '.'; break;
                        case '&': boxes.Add(cell); c = '^'; break;
                        case 'o': stones.Add(cell); c = '-'; break;
                        case '%': d.player = cell; c = '^'; break;
                        case 'b': boxes.Add(cell); c = '~'; break;
                        case 'i': d.player = cell; c = '~'; break;
                        case ' ': c = '-'; break;
                        case '_': c = ' '; break;
                    }
                    row[x] = c;
                }
                d.terrain[y] = new string(row);
            }
            d.boxes = boxes.ToArray();
            d.stones = stones.ToArray();
            MarkOutside(d);
            return d;
        }

        /// <summary>从边界泛洪：碰不到墙就能到达的空地都算场外</summary>
        private static void MarkOutside(LevelData d)
        {
            var w = d.width;
            var h = d.height;
            var grid = new char[h][];
            for (var y = 0; y < h; y++) grid[y] = d.terrain[y].ToCharArray();
            var q = new Queue<(int, int)>();
            for (var x = 0; x < w; x++) { q.Enqueue((x, 0)); q.Enqueue((x, h - 1)); }
            for (var y = 0; y < h; y++) { q.Enqueue((0, y)); q.Enqueue((w - 1, y)); }
            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue();
                if (x < 0 || y < 0 || x >= w || y >= h || grid[y][x] != '-') continue;
                var cell = y * w + x;
                if (cell == d.player || Array.IndexOf(d.boxes, cell) >= 0 || Array.IndexOf(d.stones, cell) >= 0) continue;
                grid[y][x] = ' ';
                q.Enqueue((x + 1, y)); q.Enqueue((x - 1, y)); q.Enqueue((x, y + 1)); q.Enqueue((x, y - 1));
            }
            for (var y = 0; y < h; y++) d.terrain[y] = new string(grid[y]);
        }

        public static string Format(LevelData d)
        {
            var sb = new StringBuilder();
            for (var y = 0; y < d.height; y++)
            {
                for (var x = 0; x < d.width; x++)
                {
                    var cell = y * d.width + x;
                    var t = d.TileAt(x, y, out var pp);
                    var box = d.HasBox(cell);
                    char c;
                    if (cell == d.player) c = t == Tile.Goal ? '+' : t == Tile.Plate ? '%' : t == Tile.Ice ? 'i' : '@';
                    else if (box) c = t == Tile.Goal ? '*' : t == Tile.Plate ? '&' : t == Tile.Ice ? 'b' : '$';
                    else if (d.HasStone(cell)) c = 'o';
                    else c = t == Tile.Floor ? ' ' : t == Tile.Void ? '_' : TileCodec.ToChar(t, Math.Max(pp, 0));
                    sb.Append(c);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
