using System;
using System.Collections.Generic;

namespace BoxPusher.Sim
{
    public sealed class SolveResult
    {
        public bool Solved;
        /// <summary>状态数超过上限，放弃（不代表无解）</summary>
        public bool Aborted;
        public int Pushes;
        public int Moves;
        /// <summary>展开的状态数，衡量搜索难度</summary>
        public int Explored;
        public long Millis;
        public List<Dir> Solution = new();
        public string Message = "";

        public string SolutionString()
        {
            var chars = new char[Solution.Count];
            for (var i = 0; i < chars.Length; i++) chars[i] = DirUtil.ToChar(Solution[i]);
            return new string(chars);
        }
    }

    /// <summary>
    /// 以"推箱次数"为步长的广度优先搜索，找到的解推箱次数最少。
    /// 状态 = (推完后玩家所在格, 所有箱子位置)。玩家位置不做区域归一化：
    /// 机关门 / 传送门会让可达性不对称（从 A 能走到 B 不代表 B 能回到 A），归一化会漏解。
    /// 两次推箱之间的走路用和游戏完全相同的 Rules.TryWalk 泛洪，推箱用 Rules.Step，所以解一定能在游戏里复现。
    /// </summary>
    public static class Solver
    {
        public const int DefaultMaxStates = 400_000;
        public const int MaxBoxes = 12;
        /// <summary>石头最多 1 个（和唯一的机关板配套），位置编进状态键的高 16 位</summary>
        public const int MaxStones = 1;
        public const int MaxCells = 65535;

        private struct Key : IEquatable<Key>
        {
            public ulong A, B, C;
            /// <summary>低 16 位：玩家格；高 16 位：石头格 + 1（0 = 没有石头）</summary>
            public int P;

            public bool Equals(Key o) => A == o.A && B == o.B && C == o.C && P == o.P;
            public override bool Equals(object obj) => obj is Key k && Equals(k);

            public override int GetHashCode()
            {
                unchecked
                {
                    var h = (A * 0x9E3779B97F4A7C15UL) ^ (B * 0xC2B2AE3D27D4EB4FUL) ^ (C * 0x165667B19E3779F9UL) ^ ((ulong)P * 0x27D4EB2F165667C5UL);
                    return (int)(h ^ (h >> 32));
                }
            }
        }

        private struct Node
        {
            public Key Key;
            public int Parent;
            public int PushFrom; // 推之前玩家站的格子
            public Dir Dir;
        }

        public static SolveResult Solve(LevelData d, int maxStates = DefaultMaxStates)
        {
            var m = new LevelMap(d);
            return Solve(m, d.player, Rules.ObjArray(m, d), maxStates);
        }

        public static SolveResult Solve(LevelMap m, int player, byte[] startObjs, int maxStates = DefaultMaxStates)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var res = new SolveResult();
            var boxCount = 0;
            var stoneCount = 0;
            foreach (var o in startObjs)
            {
                if (o == Obj.Box) boxCount++;
                else if (o == Obj.Stone) stoneCount++;
            }

            if (boxCount > MaxBoxes || stoneCount > MaxStones || m.Size > MaxCells)
            {
                res.Aborted = true;
                res.Message = $"箱子超过 {MaxBoxes} 个或石头超过 {MaxStones} 个，求解器不支持";
                return res;
            }
            if (Rules.IsSolved(m, startObjs))
            {
                res.Solved = true;
                res.Message = "初始状态已经通关";
                return res;
            }

            var dead = DeadCells(m, boxCount);
            var nodes = new List<Node>(1024);
            var seen = new Dictionary<Key, int>(1024);
            var boxList = new int[boxCount];

            var root = new Node { Key = MakeKey(startObjs, player, boxList), Parent = -1, PushFrom = -1 };
            nodes.Add(root);
            seen[root.Key] = 0;

            var box = new byte[m.Size];
            var next = new byte[m.Size];
            var stamp = new int[m.Size];
            var queue = new int[m.Size];
            var stampId = 0;
            var head = 0;
            var goalNode = -1;

            while (head < nodes.Count && goalNode < 0)
            {
                if (head >= maxStates)
                {
                    res.Aborted = true;
                    break;
                }
                var cur = nodes[head];
                Decode(cur.Key, box, boxList);

                // 玩家可达区域
                stampId++;
                var qh = 0;
                var qt = 0;
                var start = cur.Key.P & 0xFFFF;
                queue[qt++] = start;
                stamp[start] = stampId;
                while (qh < qt)
                {
                    var c = queue[qh++];
                    for (var i = 0; i < 4; i++)
                        if (Rules.TryWalk(m, box, c, DirUtil.All[i], out var to) && stamp[to] != stampId)
                        {
                            stamp[to] = stampId;
                            queue[qt++] = to;
                        }
                }

                // 每个可达格 × 四个方向，前面是箱子就试着推
                for (var qi = 0; qi < qt && goalNode < 0; qi++)
                {
                    var c = queue[qi];
                    for (var i = 0; i < 4; i++)
                    {
                        var dir = DirUtil.All[i];
                        var n = m.Neighbor(c, dir);
                        if (n < 0 || box[n] == Obj.None) continue;
                        Array.Copy(box, next, box.Length);
                        if (!Rules.Step(m, c, next, dir, out var np, out _, out _, out var bt, null)) continue;
                        if (next[bt] == Obj.Box && dead[bt]) continue; // 石头卡角落不剪枝（它不需要进目标）

                        var key = MakeKey(next, np, boxList);
                        if (seen.ContainsKey(key)) continue;
                        seen[key] = nodes.Count;
                        nodes.Add(new Node { Key = key, Parent = head, PushFrom = c, Dir = dir });
                        if (Rules.IsSolved(m, next))
                        {
                            goalNode = nodes.Count - 1;
                            break;
                        }
                    }
                }
                head++;
            }

            res.Explored = head;
            if (goalNode < 0)
            {
                res.Millis = sw.ElapsedMilliseconds;
                res.Message = res.Aborted ? $"搜索超过 {maxStates} 个状态，放弃（可能有解）" : "无解";
                return res;
            }

            // 回溯推箱序列，再补上推箱之间的走路
            var pushes = new List<Node>();
            for (var i = goalNode; nodes[i].Parent >= 0; i = nodes[i].Parent) pushes.Add(nodes[i]);
            pushes.Reverse();

            var p = player;
            var simBox = (byte[])startObjs.Clone();
            foreach (var push in pushes)
            {
                if (!WalkPath(m, simBox, p, push.PushFrom, res.Solution))
                {
                    res.Message = "内部错误：回放走路失败";
                    return res;
                }
                Rules.Step(m, push.PushFrom, simBox, push.Dir, out p, out _, null);
                res.Solution.Add(push.Dir);
            }

            res.Solved = Rules.IsSolved(m, simBox);
            res.Pushes = pushes.Count;
            res.Moves = res.Solution.Count;
            res.Millis = sw.ElapsedMilliseconds;
            res.Message = res.Solved ? $"可解：最少推 {res.Pushes} 次，共 {res.Moves} 步" : "内部错误：回放结果未通关";
            return res;
        }

        /// <summary>
        /// 箱子到了就再也推不动、又不在目标点上的格子：两个相邻方向都是永久墙（角落）。
        /// 只在"箱子数 = 目标数"时才剪枝——多出来的箱子卡在角落里（比如压机关板）不影响通关。
        /// </summary>
        public static bool[] DeadCells(LevelMap m, int boxCount)
        {
            var dead = new bool[m.Size];
            if (boxCount != m.Goals.Length) return dead;
            for (var c = 0; c < m.Size; c++)
            {
                if (m.Tiles[c] == Tile.Goal || m.IsSolid(c)) continue;
                var v = m.IsSolid(m.Neighbor(c, Dir.Up)) || m.IsSolid(m.Neighbor(c, Dir.Down));
                var h = m.IsSolid(m.Neighbor(c, Dir.Left)) || m.IsSolid(m.Neighbor(c, Dir.Right));
                dead[c] = v && h;
            }
            return dead;
        }

        /// <summary>BFS 找一条从 from 走到 to 的路（不推箱子），把方向追加到 output</summary>
        public static bool WalkPath(LevelMap m, byte[] box, int from, int to, List<Dir> output)
        {
            if (from == to) return true;
            var prev = new int[m.Size];
            var prevDir = new Dir[m.Size];
            for (var i = 0; i < prev.Length; i++) prev[i] = -2;
            var q = new Queue<int>();
            q.Enqueue(from);
            prev[from] = -1;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in DirUtil.All)
                {
                    if (!Rules.TryWalk(m, box, c, d, out var n) || prev[n] != -2) continue;
                    prev[n] = c;
                    prevDir[n] = d;
                    if (n == to)
                    {
                        var path = new List<Dir>();
                        for (var x = to; prev[x] >= 0; x = prev[x]) path.Add(prevDir[x]);
                        path.Reverse();
                        output.AddRange(path);
                        return true;
                    }
                    q.Enqueue(n);
                }
            }
            return false;
        }

        private static Key MakeKey(byte[] objs, int player, int[] buf)
        {
            var k = 0;
            var stone = -1;
            for (var i = 0; i < objs.Length; i++)
            {
                if (objs[i] == Obj.Box && k < buf.Length) buf[k++] = i;
                else if (objs[i] == Obj.Stone) stone = i;
            }
            var key = new Key { P = player | ((stone + 1) << 16) };
            for (var i = 0; i < k; i++)
            {
                var v = (ulong)(ushort)(buf[i] + 1); // +1：0 表示空位
                var slot = i / 4;
                var shift = (i % 4) * 16;
                if (slot == 0) key.A |= v << shift;
                else if (slot == 1) key.B |= v << shift;
                else key.C |= v << shift;
            }
            return key;
        }

        private static void Decode(Key key, byte[] objs, int[] buf)
        {
            Array.Clear(objs, 0, objs.Length);
            for (var i = 0; i < buf.Length; i++)
            {
                var slot = i / 4;
                var word = slot == 0 ? key.A : slot == 1 ? key.B : key.C;
                var v = (int)((word >> ((i % 4) * 16)) & 0xFFFF);
                if (v == 0) break;
                objs[v - 1] = Obj.Box;
            }
            var stone = (key.P >> 16) - 1;
            if (stone >= 0) objs[stone] = Obj.Stone;
        }
    }
}
