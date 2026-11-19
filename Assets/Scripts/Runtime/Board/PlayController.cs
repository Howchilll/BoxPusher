using System.Collections.Generic;
using System.Threading.Tasks;
using BoxPusher.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BoxPusher
{
    /// <summary>
    /// 游戏控制：读键盘 → GameSession（模拟层）→ BoardView（表现层）。
    /// HUD 通过 HowFrame 的 Ref&lt;T&gt; 绑定（Moves / Pushes / Goals），面板在 OnShow 订阅、OnHide 退订。
    /// 每次推箱后在后台线程跑一次求解器，局面已无解时播放提示音；"提示"按钮同样用求解器给出下一段走法。
    /// </summary>
    public sealed class PlayController
    {
        private const float RepeatDelay = 0.22f;
        private const float RepeatRate = 0.11f;
        private const int CheckStates = 200_000;

        public readonly GameSession Session;
        public readonly Ref<int> Moves = new(0);
        public readonly Ref<int> Pushes = new(0);
        public readonly Ref<string> Goals = new("");

        private readonly BoardView _view;
        private readonly Queue<Dir> _autoMoves = new();
        private Dir? _held;
        private float _nextRepeat;
        private Dir? _buffered;
        private bool _winReported;

        // 后台求解（死局检测 / 提示）
        private Task<SolveResult> _check;
        private int _checkVersion;
        private int _version;
        private bool _checkIsHint;

        public PlayController(LevelData data, BoardView view)
        {
            Session = new GameSession(data);
            _view = view;
            _view.Build(Session.Map);
            Restart();
        }

        public void Restart()
        {
            Session.Reset();
            _autoMoves.Clear();
            _buffered = null;
            _winReported = false;
            _version++;
            _view.Sync(Session.Player, Session.Objs);
            PushStats();
        }

        public void Undo()
        {
            if (Session.Solved || !Session.Undo()) return;
            _autoMoves.Clear();
            _version++;
            _view.Sync(Session.Player, Session.Objs);
            GameAudio.Play("undo");
            PushStats();
        }

        /// <summary>提示：求解当前局面，自动走到下一次推箱为止</summary>
        public void Hint()
        {
            if (Session.Solved || _check != null) return;
            StartCheck(true);
        }

        public void Tick()
        {
            PollCheck();
            if (Session.Solved)
            {
                if (!_winReported && !_view.Animating)
                {
                    _winReported = true;
                    GameApp.OnSolved(Session);
                }
                return;
            }

            ReadInput();
            if (_view.Animating) return;

            if (_autoMoves.Count > 0)
            {
                DoMove(_autoMoves.Dequeue());
                return;
            }
            if (_buffered.HasValue)
            {
                var d = _buffered.Value;
                _buffered = null;
                DoMove(d);
            }
        }

        private void ReadInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.zKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) { Undo(); return; }
            if (kb.rKey.wasPressedThisFrame) { Restart(); return; }
            if (kb.hKey.wasPressedThisFrame) { Hint(); return; }

            Dir? pressed = null;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) pressed = Dir.Up;
            else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) pressed = Dir.Down;
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) pressed = Dir.Left;
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) pressed = Dir.Right;

            if (pressed.HasValue)
            {
                _autoMoves.Clear();
                _held = pressed;
                _nextRepeat = Time.unscaledTime + RepeatDelay;
                _buffered = pressed;
                return;
            }

            // 按住连走
            if (_held.HasValue && IsHeld(kb, _held.Value))
            {
                if (Time.unscaledTime >= _nextRepeat && !_view.Animating && !_buffered.HasValue)
                {
                    _buffered = _held;
                    _nextRepeat = Time.unscaledTime + RepeatRate;
                }
            }
            else _held = null;
        }

        private static bool IsHeld(Keyboard kb, Dir d) => d switch
        {
            Dir.Up => kb.upArrowKey.isPressed || kb.wKey.isPressed,
            Dir.Down => kb.downArrowKey.isPressed || kb.sKey.isPressed,
            Dir.Left => kb.leftArrowKey.isPressed || kb.aKey.isPressed,
            _ => kb.rightArrowKey.isPressed || kb.dKey.isPressed,
        };

        private void DoMove(Dir d)
        {
            var goalsBefore = Session.BoxesOnGoals;
            var gatesBefore = Session.GatesOpen;
            if (!Session.Move(d))
            {
                GameAudio.Play("deny");
                _autoMoves.Clear();
                return;
            }
            _version++;
            _view.Animate(Session.LastEvents, Session.Player, Session.Objs);

            var pushed = false;
            var teleported = false;
            var slid = false;
            foreach (var e in Session.LastEvents)
            {
                if (e.Kind == MoveKind.Push) pushed = true;
                if (e.Kind == MoveKind.Teleport) teleported = true;
                if (e.Kind == MoveKind.Slide) slid = true;
            }
            if (Session.BoxesOnGoals > goalsBefore) GameAudio.Play("goal");
            else if (teleported) GameAudio.Play("portal");
            else if (slid) GameAudio.Play("slide");
            else if (pushed) GameAudio.Play("push");
            else GameAudio.Play("step");
            if (Session.GatesOpen != gatesBefore) GameAudio.Play("gate");

            PushStats();
            if (pushed && !Session.Solved && _autoMoves.Count == 0)
            {
                StartCheck(false);
            }
        }

        private void PushStats()
        {
            Moves.Value = Session.Moves;
            Pushes.Value = Session.Pushes;
            Goals.Value = $"{Session.BoxesOnGoals}/{Session.Map.Goals.Length}";
        }

        // ---------------- 后台求解 ----------------

        private void StartCheck(bool hint)
        {
            if (_check != null && !hint) return; // 上一次还没算完，跳过这次死局检测
            var map = Session.Map;
            var player = Session.Player;
            var box = (byte[])Session.Objs.Clone();
            _checkVersion = _version;
            _checkIsHint = hint;
            _check = Task.Run(() => Solver.Solve(map, player, box, CheckStates));
        }

        private void PollCheck()
        {
            if (_check == null || !_check.IsCompleted) return;
            var r = _check.IsFaulted ? null : _check.Result;
            var hint = _checkIsHint;
            var stale = _checkVersion != _version;
            _check = null;
            if (r == null || stale) return;

            if (!r.Solved && !r.Aborted)
            {
                GameAudio.Play("deny"); // 死局
                return;
            }
            if (!hint || !r.Solved) return;
            // 走到下一次推箱为止
            var sim = (byte[])Session.Objs.Clone();
            var p = Session.Player;
            foreach (var d in r.Solution)
            {
                _autoMoves.Enqueue(d);
                Rules.Step(Session.Map, p, sim, d, out p, out var pushed, null);
                if (pushed) break;
            }
        }
    }
}
