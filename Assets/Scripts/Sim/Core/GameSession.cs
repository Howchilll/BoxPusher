using System.Collections.Generic;

namespace BoxPusher.Sim
{
    /// <summary>一局游戏的可变状态 + 撤销栈。不依赖引擎，表现层只读它的状态 / 事件。</summary>
    public sealed class GameSession
    {
        private struct Snapshot
        {
            public int Player;
            public byte[] Objs;
            public int Moves;
            public int Pushes;
        }

        public readonly LevelData Data;
        public readonly LevelMap Map;
        public int Player { get; private set; }
        /// <summary>动态层：0 空 / 1 箱子 / 2 石头（见 Obj）</summary>
        public byte[] Objs { get; private set; }
        public int Moves { get; private set; }
        public int Pushes { get; private set; }
        public bool Solved { get; private set; }
        public bool CanUndo => _history.Count > 0;

        /// <summary>最近一步的位移序列（渲染层据此播动画）</summary>
        public readonly List<MoveEvent> LastEvents = new();

        private readonly Stack<Snapshot> _history = new();

        public GameSession(LevelData data)
        {
            Data = data;
            Map = new LevelMap(data);
            Reset();
        }

        public void Reset()
        {
            Player = Data.player;
            Objs = Rules.ObjArray(Map, Data);
            Moves = 0;
            Pushes = 0;
            _history.Clear();
            LastEvents.Clear();
            Solved = Rules.IsSolved(Map, Objs);
        }

        public bool GatesOpen => Rules.GatesOpen(Map, Objs, Player);

        /// <summary>返回是否真的移动了。已通关后不再接受输入。</summary>
        public bool Move(Dir d)
        {
            LastEvents.Clear();
            if (Solved) return false;
            var snap = new Snapshot { Player = Player, Objs = (byte[])Objs.Clone(), Moves = Moves, Pushes = Pushes };
            var objs = (byte[])Objs.Clone();
            if (!Rules.Step(Map, Player, objs, d, out var np, out var pushed, LastEvents))
            {
                LastEvents.Clear();
                return false;
            }
            _history.Push(snap);
            Player = np;
            Objs = objs;
            Moves++;
            if (pushed) Pushes++;
            Solved = Rules.IsSolved(Map, Objs);
            return true;
        }

        public bool Undo()
        {
            LastEvents.Clear();
            if (_history.Count == 0) return false;
            var s = _history.Pop();
            Player = s.Player;
            Objs = s.Objs;
            Moves = s.Moves;
            Pushes = s.Pushes;
            Solved = Rules.IsSolved(Map, Objs);
            return true;
        }

        public int[] BoxCells()
        {
            var list = new List<int>();
            for (var i = 0; i < Objs.Length; i++)
                if (Objs[i] == Obj.Box) list.Add(i);
            return list.ToArray();
        }

        /// <summary>从当前局面求解，给"提示"用</summary>
        public SolveResult SolveFromHere(int maxStates = Solver.DefaultMaxStates) =>
            Solver.Solve(Map, Player, Objs, maxStates);

        public int BoxesOnGoals
        {
            get
            {
                var n = 0;
                foreach (var g in Map.Goals) if (Objs[g] == Obj.Box) n++;
                return n;
            }
        }
    }
}
