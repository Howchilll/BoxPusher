using BoxPusher.Sim;
using UnityEngine;

namespace BoxPusher
{
    /// <summary>
    /// 每个场景一个：持有 BoardView，驱动 PlayController / EditorController 的每帧逻辑（唯一的 Update）。
    /// 游戏和编辑器试玩用同一套 PlayController，所以编辑器里试出来的结果和正式游戏一致。
    /// </summary>
    public sealed class BoardStage : MonoBehaviour
    {
        public static BoardStage Instance { get; private set; }

        public BoardView View { get; private set; }
        public PlayController Play { get; private set; }
        public EditorController Editor { get; private set; }

        /// <summary>棋盘可用的屏幕区域（归一化）：游戏时让出上下 HUD，编辑时让出左右面板</summary>
        public static readonly Rect PlayViewport = new(0.02f, 0.12f, 0.96f, 0.70f);
        public static readonly Rect EditViewport = new(0.17f, 0.04f, 0.6f, 0.92f);

        private Camera _cam;
        private Rect _viewport = PlayViewport;
        private Vector2Int _screen;

        public static BoardStage Create(bool withEditor)
        {
            var go = new GameObject("BoardStage");
            var s = go.AddComponent<BoardStage>();
            s.View = new GameObject("BoardView").AddComponent<BoardView>();
            s.View.transform.SetParent(go.transform, false);
            if (withEditor) s.Editor = new EditorController(s.View);
            return s;
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void StartPlay(LevelData data)
        {
            Editor?.SetActive(false);
            Play = new PlayController(data, View);
            Fit(PlayViewport);
        }

        public void StopPlay()
        {
            Play = null;
            if (Editor != null)
            {
                Editor.SetActive(true);
                Fit(EditViewport);
            }
        }

        public void Clear()
        {
            Play = null;
            if (Editor == null) View.Clear();
        }

        public void Fit(Rect viewport)
        {
            _viewport = viewport;
            Refit();
        }

        /// <summary>按上一次的视口重新适配（窗口尺寸 / 相机宽高比变化后调用）</summary>
        public void Refit()
        {
            if (_cam == null) _cam = Camera.main;
            View.FitCamera(_cam, _viewport);
        }

        private void Update()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != _screen)
            {
                _screen = size;
                Refit();
            }
            if (Play != null) Play.Tick();
            else Editor?.Tick(_cam != null ? _cam : (_cam = Camera.main));
        }
    }
}
