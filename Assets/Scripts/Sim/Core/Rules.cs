using System.Collections.Generic;

namespace BoxPusher.Sim
{
    public enum MoveKind : byte { Walk, Push, Slide, Teleport }

    /// <summary>动态层：每格上的可推动物体</summary>
    public static class Obj
    {
        public const byte None = 0;
        /// <summary>箱子：推到目标点上算分</summary>
        public const byte Box = 1;
        /// <summary>石头：和箱子一样能推、会滑、会被传送，但不算目标；用来压机关板</summary>
        public const byte Stone = 2;
    }

    /// <summary>一次移动里发生的一段位移，按发生顺序给渲染层播放动画</summary>
    public struct MoveEvent
    {
        /// <summary>true = 箱子或石头，false = 玩家</summary>
        public bool IsObject;
        public int From;
        public int To;
        public MoveKind Kind;

        public MoveEvent(bool isObject, int from, int to, MoveKind kind)
        {
            IsObject = isObject;
            From = from;
            To = to;
            Kind = kind;
        }
    }

    /// <summary>
    /// 全部规则都在这里，游戏、编辑器试玩、求解器共用同一份代码，保证"求解器说能解"="玩家真的能解"。
    /// 动态层 objs[cell]：0 空、1 箱子、2 石头。箱子和石头的推动规则完全一样，只有箱子算目标。
    ///
    /// 一步的结算顺序（门的开关状态在这一步开始时算一次，整步内不变）：
    /// 1. 目标格是墙 / 场外 / 关着的门 → 不动
    /// 2. 目标格有箱子 / 石头 → 它前方必须可进入（不是墙、门开、没有别的物体），否则不动
    ///    进入新格后：传送门且另一端空着 → 传过去并停下；冰面 → 沿推的方向继续滑，直到被挡或离开冰面
    /// 3. 玩家进入目标格：传送门且另一端空着 → 传过去；玩家在冰面上不滑
    /// "空着" = 没有物体，也不是这一步里玩家 / 物体刚离开或将要站的格子。
    /// </summary>
    public static class Rules
    {
        /// <summary>机关板上有箱子、石头或玩家 → 门打开</summary>
        public static bool GatesOpen(LevelMap m, byte[] objs, int player)
        {
            var plates = m.Plates;
            for (var i = 0; i < plates.Length; i++)
                if (objs[plates[i]] != Obj.None || plates[i] == player) return true;
            return false;
        }

        private static bool Enterable(LevelMap m, int cell, bool gatesOpen)
        {
            if (cell < 0) return false;
            var t = m.Tiles[cell];
            return t != Tile.Wall && t != Tile.Void && (t != Tile.Gate || gatesOpen);
        }

        /// <summary>不推东西的走一步（求解器做可达性泛洪用）。前方有箱子 / 石头返回 false</summary>
        public static bool TryWalk(LevelMap m, byte[] objs, int from, Dir d, out int to)
        {
            to = from;
            var n = m.Neighbor(from, d);
            if (n < 0 || objs[n] != Obj.None || !Enterable(m, n, GatesOpen(m, objs, from))) return false;
            to = n;
            if (m.Tiles[n] == Tile.Portal)
            {
                var p = m.Partner[n];
                if (p >= 0 && objs[p] == Obj.None && p != from) to = p;
            }
            return true;
        }

        /// <summary>
        /// 玩家朝 d 走一步，会直接修改 objs 数组。返回 false 表示被挡住（objs 不变）。
        /// events 可为 null（求解器不需要动画）。
        /// </summary>
        public static bool Step(LevelMap m, int player, byte[] objs, Dir d, out int newPlayer, out bool pushed, List<MoveEvent> events) =>
            Step(m, player, objs, d, out newPlayer, out pushed, out _, out _, events);

        /// <summary>同上，另外给出被推物体的起点 / 终点（没推东西时都是 -1）</summary>
        public static bool Step(LevelMap m, int player, byte[] objs, Dir d, out int newPlayer, out bool pushed,
            out int objFrom, out int objTo, List<MoveEvent> events)
        {
            newPlayer = player;
            objFrom = objTo = -1;
            pushed = false;
            var gates = GatesOpen(m, objs, player);
            var n = m.Neighbor(player, d);
            if (!Enterable(m, n, gates)) return false;

            if (objs[n] != Obj.None)
            {
                var next = m.Neighbor(n, d);
                if (!Enterable(m, next, gates) || objs[next] != Obj.None) return false;

                var kind = objs[n];
                objs[n] = Obj.None;
                var cur = next;
                events?.Add(new MoveEvent(true, n, next, MoveKind.Push));
                while (true)
                {
                    var t = m.Tiles[cur];
                    if (t == Tile.Portal)
                    {
                        var p = m.Partner[cur];
                        if (p >= 0 && objs[p] == Obj.None && p != n && p != player)
                        {
                            events?.Add(new MoveEvent(true, cur, p, MoveKind.Teleport));
                            cur = p;
                        }
                        break;
                    }
                    if (t != Tile.Ice) break;
                    var nx = m.Neighbor(cur, d);
                    if (!Enterable(m, nx, gates) || objs[nx] != Obj.None || nx == n || nx == player) break;
                    events?.Add(new MoveEvent(true, cur, nx, MoveKind.Slide));
                    cur = nx;
                }
                objs[cur] = kind;
                pushed = true;
                objFrom = n;
                objTo = cur;
            }

            events?.Add(new MoveEvent(false, player, n, MoveKind.Walk));
            newPlayer = n;
            if (m.Tiles[n] == Tile.Portal)
            {
                var p = m.Partner[n];
                if (p >= 0 && objs[p] == Obj.None && p != player)
                {
                    events?.Add(new MoveEvent(false, n, p, MoveKind.Teleport));
                    newPlayer = p;
                }
            }
            return true;
        }

        /// <summary>所有目标点上都有箱子（石头不算）</summary>
        public static bool IsSolved(LevelMap m, byte[] objs)
        {
            var goals = m.Goals;
            if (goals.Length == 0) return false;
            for (var i = 0; i < goals.Length; i++)
                if (objs[goals[i]] != Obj.Box) return false;
            return true;
        }

        public static byte[] ObjArray(LevelMap m, int[] boxes, int[] stones)
        {
            var arr = new byte[m.Size];
            foreach (var b in boxes)
                if (b >= 0 && b < arr.Length) arr[b] = Obj.Box;
            if (stones != null)
                foreach (var s in stones)
                    if (s >= 0 && s < arr.Length) arr[s] = Obj.Stone;
            return arr;
        }

        public static byte[] ObjArray(LevelMap m, LevelData d) => ObjArray(m, d.boxes, d.stones);
    }
}
