using System.Collections;
using System.Collections.Generic;
using BoxPusher.Sim;
using UnityEngine;

namespace BoxPusher
{
    /// <summary>
    /// 纯表现层：把 LevelMap + (玩家, 箱子) 画成 SpriteRenderer，并按 MoveEvent 播放动画。
    /// 不持有任何规则状态——每次动画结束都用模拟层的真实状态 Sync 一次，表现永远不会和模拟层分叉。
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float StepTime = 0.085f;
        public const float SlideTime = 0.05f;

        private LevelMap _map;
        private SpriteRenderer[] _terrain = new SpriteRenderer[0];
        /// <summary>格子 → 箱子 / 石头的显示对象</summary>
        private readonly Dictionary<int, Transform> _boxes = new();
        private readonly List<Transform> _boxPool = new();
        private readonly Dictionary<Transform, byte> _kind = new();
        private Transform _player;
        private Transform _terrainRoot, _objectRoot;
        private SpriteRenderer _cursor;
        private Coroutine _anim;
        private bool _gatesOpen;

        public bool Animating => _anim != null;
        public LevelMap Map => _map;

        private void Awake()
        {
            _terrainRoot = new GameObject("Terrain").transform;
            _terrainRoot.SetParent(transform, false);
            _objectRoot = new GameObject("Objects").transform;
            _objectRoot.SetParent(transform, false);
            _player = NewSprite("Player", TileArt.Player, 10, _objectRoot).transform;
            _player.gameObject.SetActive(false);
            _cursor = NewSprite("Cursor", TileArt.Cursor, 20, transform);
            _cursor.gameObject.SetActive(false);
        }

        // ---------------- 坐标 ----------------

        public Vector3 CellToWorld(int cell) => CellToWorld(cell % _map.Width, cell / _map.Width);

        public Vector3 CellToWorld(int x, int y) =>
            new(x - _map.Width * 0.5f + 0.5f, -(y - _map.Height * 0.5f + 0.5f), 0);

        /// <summary>世界坐标 → 格子，越界返回 -1</summary>
        public int WorldToCell(Vector3 w)
        {
            if (_map == null) return -1;
            var x = Mathf.FloorToInt(w.x + _map.Width * 0.5f);
            var y = Mathf.FloorToInt(-w.y + _map.Height * 0.5f);
            return x < 0 || y < 0 || x >= _map.Width || y >= _map.Height ? -1 : y * _map.Width + x;
        }

        // ---------------- 构建 / 同步 ----------------

        /// <summary>按地图重建地形渲染器（尺寸变化时重建，否则复用）</summary>
        public void Build(LevelMap map)
        {
            StopAnim();
            _map = map;
            if (_terrain.Length != map.Size)
            {
                foreach (var r in _terrain) if (r != null) Destroy(r.gameObject);
                _terrain = new SpriteRenderer[map.Size];
                for (var c = 0; c < map.Size; c++)
                    _terrain[c] = NewSprite("T" + c, null, 0, _terrainRoot);
            }
            for (var c = 0; c < map.Size; c++)
            {
                _terrain[c].transform.localPosition = CellToWorld(c);
                RefreshTerrain(c, false, false);
            }
            _gatesOpen = false;
        }

        private void RefreshTerrain(int c, bool gatesOpen, bool pressed)
        {
            var t = _map.Tiles[c];
            var r = _terrain[c];
            var x = c % _map.Width;
            var y = c / _map.Width;
            r.sprite = TileArt.Terrain(t, (x + y) % 2 == 0, gatesOpen, pressed);
            r.color = TileArt.TerrainTint(t, _map.PortalPair[c]);
            r.sortingOrder = t == Tile.Wall ? 2 : 0;
            r.enabled = t != Tile.Void;
        }

        /// <summary>把动态层摆成给定状态（无动画）</summary>
        public void Sync(int player, byte[] objs)
        {
            StopAnim();
            foreach (var t in _boxes.Values) Release(t);
            _boxes.Clear();
            for (var c = 0; c < objs.Length; c++)
            {
                if (objs[c] == Obj.None) continue;
                var t = Acquire(objs[c]);
                t.localPosition = CellToWorld(c);
                _boxes[c] = t;
            }
            _player.gameObject.SetActive(player >= 0);
            if (player >= 0) _player.localPosition = CellToWorld(player);
            _player.localScale = Vector3.one;
            RefreshDynamic(player, objs);
        }

        /// <summary>箱子在目标上变色、机关板按下、机关门开关</summary>
        private void RefreshDynamic(int player, byte[] objs)
        {
            foreach (var kv in _boxes)
            {
                kv.Value.localScale = Vector3.one;
                var isBox = _kind.TryGetValue(kv.Value, out var k) && k == Obj.Box;
                kv.Value.GetComponent<SpriteRenderer>().color =
                    isBox && _map.Tiles[kv.Key] == Tile.Goal ? TileArt.BoxOnGoalTint : Color.white;
            }
            var open = Rules.GatesOpen(_map, objs, player);
            _gatesOpen = open;
            for (var c = 0; c < _map.Size; c++)
            {
                var t = _map.Tiles[c];
                if (t == Tile.Gate || t == Tile.Plate) RefreshTerrain(c, open, objs[c] != Obj.None || c == player);
            }
        }

        public bool GatesOpen => _gatesOpen;

        // ---------------- 动画 ----------------

        /// <summary>播放一步的位移序列：箱子轨道和玩家轨道并行，各自按顺序播；播完用真实状态 Sync</summary>
        public void Animate(List<MoveEvent> events, int player, byte[] objs)
        {
            StopAnim();
            _anim = StartCoroutine(AnimRoutine(new List<MoveEvent>(events), player, (byte[])objs.Clone()));
        }

        private IEnumerator AnimRoutine(List<MoveEvent> events, int player, byte[] objs)
        {
            Transform movingBox = null;
            var boxTrack = new List<MoveEvent>();
            var playerTrack = new List<MoveEvent>();
            foreach (var e in events)
            {
                if (e.IsObject)
                {
                    if (movingBox == null && _boxes.TryGetValue(e.From, out var t)) movingBox = t;
                    boxTrack.Add(e);
                }
                else playerTrack.Add(e);
            }

            var running = 0;
            if (movingBox != null) { running++; StartCoroutine(Track(movingBox, boxTrack, () => running--)); }
            running++;
            StartCoroutine(Track(_player, playerTrack, () => running--));
            while (running > 0) yield return null;
            _anim = null;
            Sync(player, objs);
        }

        private IEnumerator Track(Transform target, List<MoveEvent> track, System.Action done)
        {
            foreach (var e in track)
            {
                if (e.Kind == MoveKind.Teleport)
                {
                    yield return Scale(target, 1, 0.1f, 0.06f);
                    target.localPosition = CellToWorld(e.To);
                    yield return Scale(target, 0.1f, 1, 0.08f);
                }
                else
                {
                    var from = CellToWorld(e.From);
                    var to = CellToWorld(e.To);
                    var dur = e.Kind == MoveKind.Slide ? SlideTime : StepTime;
                    for (var t = 0f; t < dur; t += Time.deltaTime)
                    {
                        target.localPosition = Vector3.Lerp(from, to, Ease(t / dur));
                        yield return null;
                    }
                    target.localPosition = to;
                }
            }
            done();
        }

        private static IEnumerator Scale(Transform t, float a, float b, float dur)
        {
            for (var x = 0f; x < dur; x += Time.deltaTime)
            {
                t.localScale = Vector3.one * Mathf.Lerp(a, b, x / dur);
                yield return null;
            }
            t.localScale = Vector3.one * b;
        }

        private static float Ease(float t) => 1 - (1 - t) * (1 - t);

        private void StopAnim()
        {
            if (_anim == null) return;
            StopAllCoroutines();
            _anim = null;
        }

        // ---------------- 编辑器光标 ----------------

        public void ShowCursor(int cell, Color color)
        {
            if (cell < 0 || _map == null)
            {
                _cursor.gameObject.SetActive(false);
                return;
            }
            _cursor.gameObject.SetActive(true);
            _cursor.transform.localPosition = CellToWorld(cell);
            _cursor.color = color;
        }

        // ---------------- 摄像机 ----------------

        /// <summary>
        /// 让棋盘完整落在屏幕的某个区域内（归一化视口矩形，例如编辑器左右两侧有面板时只用中间）
        /// </summary>
        public void FitCamera(Camera cam, Rect viewport, float margin = 0.6f)
        {
            if (cam == null || _map == null) return;
            var aspect = cam.aspect * viewport.width / viewport.height;
            var halfH = _map.Height * 0.5f + margin;
            var halfW = _map.Width * 0.5f + margin;
            var size = Mathf.Max(halfH, halfW / aspect) / viewport.height;
            cam.orthographicSize = size;
            // 视口中心相对屏幕中心的偏移，换算成世界坐标后反向移动摄像机
            var offX = (viewport.center.x - 0.5f) * 2 * size * cam.aspect;
            var offY = (viewport.center.y - 0.5f) * 2 * size;
            cam.transform.position = new Vector3(-offX, -offY, -10);
        }

        // ---------------- 工具 ----------------

        public void Clear()
        {
            StopAnim();
            foreach (var t in _boxes.Values) Release(t);
            _boxes.Clear();
            foreach (var r in _terrain) if (r != null) Destroy(r.gameObject);
            _terrain = new SpriteRenderer[0];
            _player.gameObject.SetActive(false);
            _cursor.gameObject.SetActive(false);
            _map = null;
        }

        private Transform Acquire(byte kind)
        {
            Transform t;
            if (_boxPool.Count > 0)
            {
                t = _boxPool[_boxPool.Count - 1];
                _boxPool.RemoveAt(_boxPool.Count - 1);
                t.gameObject.SetActive(true);
            }
            else t = NewSprite("Object", null, 5, _objectRoot).transform;
            t.GetComponent<SpriteRenderer>().sprite = kind == Obj.Stone ? TileArt.Stone : TileArt.Box;
            t.name = kind == Obj.Stone ? "Stone" : "Box";
            _kind[t] = kind;
            return t;
        }

        private void Release(Transform t)
        {
            t.gameObject.SetActive(false);
            _boxPool.Add(t);
        }

        private static SpriteRenderer NewSprite(string name, Sprite s, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s;
            r.sortingOrder = order;
            return r;
        }
    }
}
