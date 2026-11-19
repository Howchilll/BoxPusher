using System;
using System.Collections.Generic;
using BoxPusher.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BoxPusher
{
    /// <summary>编辑器画笔</summary>
    public enum EditorTool
    {
        Wall, Floor, Void, Goal, Ice, Plate, Gate,
        PortalA, PortalB, PortalC, PortalD,
        Player, Box, Stone,
    }

    /// <summary>
    /// 关卡编辑：鼠标在棋盘上绘制，直接改 LevelData（存盘格式本身），改完刷新 BoardView。
    /// 左键画当前工具，右键擦成空地（并移除箱子 / 玩家），Ctrl+Z 撤销一笔。
    /// </summary>
    public sealed class EditorController
    {
        public const int MinSize = 5;
        public const int MaxWidth = 20;
        public const int MaxHeight = 14;

        public LevelData Current { get; private set; }
        public EditorTool Tool = EditorTool.Wall;
        /// <summary>关卡内容有改动（UI 刷新状态 / 清除上次求解结果）</summary>
        public event Action Changed;
        /// <summary>编辑器自动做了调整（比如超出数量移除了最早的机关），给 UI 显示</summary>
        public event Action<string> Notice;

        private readonly BoardView _view;
        private readonly Stack<LevelData> _undo = new();
        private bool _active = true;
        private bool _stroking;
        private bool _strokeErase;
        private bool _strokeRemoveBox;
        private int _lastCell = -1;
        /// <summary>限量结构的放置顺序（最早的在前）：gate / plate / portal0..3</summary>
        private readonly Dictionary<string, List<int>> _order = new();

        public EditorController(BoardView view)
        {
            _view = view;
            Load(LevelData.CreateEmpty(10, 8));
        }

        public static string ToolName(EditorTool t) => t switch
        {
            EditorTool.Wall => "墙",
            EditorTool.Floor => "空地",
            EditorTool.Void => "场外",
            EditorTool.Goal => "目标点",
            EditorTool.Ice => "冰面",
            EditorTool.Plate => "机关板",
            EditorTool.Gate => "机关门",
            EditorTool.PortalA => "传送门 A",
            EditorTool.PortalB => "传送门 B",
            EditorTool.PortalC => "传送门 C",
            EditorTool.PortalD => "传送门 D",
            EditorTool.Player => "玩家出生点",
            EditorTool.Box => "箱子",
            EditorTool.Stone => "石头",
            _ => "?",
        };

        /// <summary>画笔在工具栏上的预览图</summary>
        public static (Sprite sprite, Color color) ToolIcon(EditorTool t) => t switch
        {
            EditorTool.Player => (TileArt.Player, Color.white),
            EditorTool.Box => (TileArt.Box, Color.white),
            EditorTool.Stone => (TileArt.Stone, Color.white),
            EditorTool.Void => (TileArt.Square, new Color(0, 0, 0, 0.35f)),
            >= EditorTool.PortalA and <= EditorTool.PortalD =>
                (TileArt.Terrain(Tile.Portal, true), TileArt.TerrainTint(Tile.Portal, t - EditorTool.PortalA)),
            _ => (TileArt.Terrain(ToTile(t, out _), true), Color.white),
        };

        private static Tile ToTile(EditorTool t, out int pair)
        {
            pair = 0;
            switch (t)
            {
                case EditorTool.Wall: return Tile.Wall;
                case EditorTool.Floor: return Tile.Floor;
                case EditorTool.Void: return Tile.Void;
                case EditorTool.Goal: return Tile.Goal;
                case EditorTool.Ice: return Tile.Ice;
                case EditorTool.Plate: return Tile.Plate;
                case EditorTool.Gate: return Tile.Gate;
                case EditorTool.PortalA:
                case EditorTool.PortalB:
                case EditorTool.PortalC:
                case EditorTool.PortalD:
                    pair = t - EditorTool.PortalA;
                    return Tile.Portal;
                default: return Tile.Floor;
            }
        }

        // ---------------- 关卡 ----------------

        /// <summary>载入一份关卡（会复制一份，不改原对象）</summary>
        public void Load(LevelData data, bool clearUndo = true)
        {
            Current = data.Clone();
            if (clearUndo) _undo.Clear();
            Rebuild();
        }

        public void NewEmpty(int w, int h)
        {
            PushUndo();
            var id = Current.id;
            var name = Current.name;
            Current = LevelData.CreateEmpty(Clamp(w, MaxWidth), Clamp(h, MaxHeight));
            Current.id = id;
            Current.name = name;
            Rebuild();
        }

        public void Resize(int w, int h)
        {
            PushUndo();
            Current = Current.Resized(Clamp(w, MaxWidth), Clamp(h, MaxHeight));
            Rebuild();
        }

        private static int Clamp(int v, int max) => Mathf.Clamp(v, MinSize, max);

        public void SetMeta(int id, string name)
        {
            Current.id = id;
            Current.name = name ?? "";
        }

        public void SetActive(bool active)
        {
            _active = active;
            _stroking = false;
            if (active) Rebuild();
            else _view.ShowCursor(-1, Color.white);
        }

        private void Rebuild()
        {
            _order.Clear();
            var map = new LevelMap(Current);
            _view.Build(map);
            _view.Sync(Current.player, Rules.ObjArray(map, Current));
            BoardStage.Instance?.Fit(BoardStage.EditViewport);
            Changed?.Invoke();
        }

        // ---------------- 输入 ----------------

        public void Tick(Camera cam)
        {
            if (!_active || cam == null || _view.Map == null) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null && (kb.leftCtrlKey.isPressed || kb.leftCommandKey.isPressed) && kb.zKey.wasPressedThisFrame) Undo();
            if (mouse == null) return;

            var overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            var world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            var cell = overUi ? -1 : _view.WorldToCell(world);
            _view.ShowCursor(cell, mouse.rightButton.isPressed ? UiTheme.Bad : UiTheme.Primary);

            var left = mouse.leftButton;
            var right = mouse.rightButton;
            if (!_stroking && cell >= 0 && (left.wasPressedThisFrame || right.wasPressedThisFrame))
            {
                PushUndo();
                _stroking = true;
                _strokeErase = right.wasPressedThisFrame;
                _strokeRemoveBox = Tool == EditorTool.Box && !_strokeErase && Current.HasBox(cell);
                _lastCell = -1;
            }
            if (_stroking && !left.isPressed && !right.isPressed)
            {
                _stroking = false;
                Changed?.Invoke();
            }
            if (_stroking && cell >= 0 && cell != _lastCell)
            {
                _lastCell = cell;
                if (Apply(cell)) Refresh();
            }
        }

        /// <summary>在格子上落一笔，返回是否有变化</summary>
        private bool Apply(int cell)
        {
            var d = Current;
            var x = cell % d.width;
            var y = cell / d.width;
            var tile = d.TileAt(x, y, out _);
            var before = Snapshot(d, cell);

            if (_strokeErase)
            {
                if (d.HasBox(cell)) d.SetBox(cell, false);
                else if (d.HasStone(cell)) d.SetStone(cell, false);
                else if (d.player == cell) d.player = -1;
                else d.SetTile(x, y, Tile.Floor);
                return before != Snapshot(d, cell);
            }

            switch (Tool)
            {
                case EditorTool.Player:
                    if (!Walkable(tile)) d.SetTile(x, y, Tile.Floor);
                    d.SetBox(cell, false);
                    d.SetStone(cell, false);
                    d.player = cell;
                    break;
                case EditorTool.Box:
                    if (_strokeRemoveBox) d.SetBox(cell, false);
                    else
                    {
                        if (!Walkable(tile)) d.SetTile(x, y, Tile.Floor);
                        if (d.player == cell) d.player = -1;
                        d.SetStone(cell, false);
                        d.SetBox(cell, true);
                    }
                    break;
                case EditorTool.Stone:
                    // 石头只能放在空地上；一关只有一个，放新的会移除旧的
                    if (d.HasStone(cell)) break;
                    foreach (var old in d.stones)
                    {
                        d.SetStone(old, false);
                        Notice?.Invoke($"石头一关只能有 {LevelValidator.MaxStones} 个，已移除原来的（{old % d.width},{old / d.width}）");
                    }
                    if (tile != Tile.Floor) d.SetTile(x, y, Tile.Floor);
                    d.SetBox(cell, false);
                    if (d.player == cell) d.player = -1;
                    d.SetStone(cell, true);
                    break;
                default:
                    var t = ToTile(Tool, out var pair);
                    if (t == Tile.Gate) Limit("gate", Tile.Gate, -1, cell, LevelValidator.MaxGates, "机关门");
                    else if (t == Tile.Plate) Limit("plate", Tile.Plate, -1, cell, LevelValidator.MaxPlates, "机关板");
                    else if (t == Tile.Portal) Limit("portal" + pair, Tile.Portal, pair, cell, LevelValidator.PortalsPerPair, "传送门 " + (char)('A' + pair));
                    d.SetTile(x, y, t, pair);
                    if (t != Tile.Floor) d.SetStone(cell, false); // 石头只能在空地上
                    if (!Walkable(t))
                    {
                        d.SetBox(cell, false);
                        if (d.player == cell) d.player = -1;
                    }
                    break;
            }
            return before != Snapshot(d, cell);
        }

        /// <summary>
        /// 放下一个限量结构前调用：超过上限时把最早放的那个改回空地。
        /// 顺序表只记录本次编辑里放的；载入关卡时已有的视为更早（按扫描顺序排在前面）。
        /// </summary>
        private void Limit(string key, Tile kind, int pair, int cell, int limit, string label)
        {
            var d = Current;
            bool Is(int c)
            {
                var t = d.TileAt(c % d.width, c / d.width, out var pp);
                return t == kind && (pair < 0 || pp == pair);
            }

            if (!_order.TryGetValue(key, out var list)) _order[key] = list = new List<int>();
            list.RemoveAll(c => c == cell || !Is(c));
            var untracked = new List<int>();
            for (var c = 0; c < d.width * d.height; c++)
                if (c != cell && Is(c) && !list.Contains(c)) untracked.Add(c);
            list.InsertRange(0, untracked);
            list.Add(cell);

            while (list.Count > limit)
            {
                var old = list[0];
                list.RemoveAt(0);
                d.SetTile(old % d.width, old / d.width, Tile.Floor);
                Notice?.Invoke($"{label}一关只能有 {limit} 个，已移除最早放的那个（{old % d.width},{old / d.width}）");
            }
        }

        private static string Snapshot(LevelData d, int cell) =>
            d.terrain[cell / d.width][cell % d.width] + (d.HasBox(cell) ? "$" : "") + (d.HasStone(cell) ? "o" : "") + (d.player == cell ? "@" : "");

        private static bool Walkable(Tile t) => t != Tile.Wall && t != Tile.Void && t != Tile.Gate;

        private void Refresh()
        {
            var map = new LevelMap(Current);
            _view.Build(map);
            _view.Sync(Current.player, Rules.ObjArray(map, Current));
        }

        private void PushUndo()
        {
            _undo.Push(Current.Clone());
            if (_undo.Count > 100)
            {
                var keep = _undo.ToArray();
                _undo.Clear();
                for (var i = 49; i >= 0; i--) _undo.Push(keep[i]);
            }
        }

        public void Undo()
        {
            if (_undo.Count == 0) return;
            Current = _undo.Pop();
            Rebuild();
        }
    }
}
