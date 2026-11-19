using System.Collections.Generic;
using BoxPusher.Sim;
using HowEnum;
using HowFrame;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BoxPusher
{
    /// <summary>顶层画面，同一时刻只显示一个</summary>
    public enum GameScreen { Title, LevelSelect, Play, Editor }

    /// <summary>试玩来源：正式关卡（记录成绩、可下一关）或编辑器试玩（返回编辑器）</summary>
    public enum PlaySource { Campaign, EditorTest }

    /// <summary>
    /// 组合根 + 画面流程（照 ZombieGame 的 GameApp）。面板之间不直接 Show/Hide，一律调这里。
    /// 两个场景：Main（标题 / 选关 / 游戏）和 LevelEditor（编辑器 + 试玩），UIRoot 跨场景常驻。
    /// </summary>
    public static class GameApp
    {
        public const short ScreenLevel = 0;
        public const short OverlayLevel = 10;
        public const short SettingsLevel = 15;
        public const short AlertLevel = 20;

        public const string MainScene = "Main";

        public static GameScreen Screen { get; private set; }
        public static PlaySource Source { get; private set; }
        /// <summary>正式关卡的顺序（选关、解锁、下一关都按它）</summary>
        public static List<int> LevelOrder { get; private set; } = new();
        /// <summary>当前在玩的正式关卡 id（编辑器试玩时为 -1）</summary>
        public static int CurrentLevelId { get; private set; } = -1;

        private static readonly EnumKey<UINameEnum.Tag>[] ScreenPanels =
        {
            UINameEnum.TitlePanel, UINameEnum.LevelSelectPanel, UINameEnum.PlayPanel, UINameEnum.EditorPanel,
        };

        // ---------------- 启动 ----------------

        /// <summary>Main 场景：GameInit 完成后由 GameEntry 调用</summary>
        public static void BootMain(GameScreen first = GameScreen.Title)
        {
            BootCommon();
            RefreshLevels();
            Go(first);
        }

        /// <summary>LevelEditor 场景：由 EditorEntry 调用</summary>
        public static void BootEditor()
        {
            BootCommon();
            Go(GameScreen.Editor);
        }

        private static void BootCommon()
        {
            GameAudio.EnsureListener();
            GameAudio.ApplyVolumes();
        }

        public static void RefreshLevels() => LevelOrder = LevelStore.OfficialIds();

        public static void Go(GameScreen screen)
        {
            HideIfShown(UINameEnum.WinPanel);
            HideIfShown(UINameEnum.SettingsPanel);
            Screen = screen;
            for (var i = 0; i < ScreenPanels.Length; i++)
                if (i != (int)screen) HideIfShown(ScreenPanels[i]);
            UIManager.Show(ScreenPanels[(int)screen], ScreenLevel);
            GameAudio.Music(screen == GameScreen.Play ? "play" : "menu");
            if (screen != GameScreen.Play && screen != GameScreen.Editor) BoardStage.Instance?.Clear();
        }

        private static void HideIfShown(EnumKeyBase key)
        {
            if (UIManager.Check(key)) UIManager.Hide(key);
        }

        // ---------------- 游戏 ----------------

        public static void PlayLevel(int id)
        {
            var data = LevelStore.LoadOfficial(id);
            if (data == null)
            {
                Debug.LogError($"关卡 {id} 不存在");
                return;
            }
            var errors = LevelValidator.Validate(data);
            if (errors.Count > 0)
            {
                Debug.LogError($"关卡 {id} 结构错误：{string.Join("；", errors)}");
                return;
            }
            Source = PlaySource.Campaign;
            CurrentLevelId = id;
            Progress.Data.lastLevel = id;
            BoardStage.Instance.StartPlay(data);
            Go(GameScreen.Play);
        }

        /// <summary>编辑器里试玩当前编辑的关卡（不离开编辑器场景）</summary>
        public static void PlayTest(LevelData data)
        {
            Source = PlaySource.EditorTest;
            CurrentLevelId = -1;
            BoardStage.Instance.StartPlay(data.Clone());
            Go(GameScreen.Play);
        }

        /// <summary>PlayController 在通关动画播完后调用</summary>
        public static void OnSolved(GameSession s)
        {
            var newBest = false;
            if (Source == PlaySource.Campaign) newBest = Progress.Record(CurrentLevelId, s.Moves, s.Pushes);
            GameAudio.Play("win");
            UIManager.Show(UINameEnum.WinPanel, new WinInfo
            {
                Session = s,
                NewBest = newBest,
                HasNext = Source == PlaySource.Campaign && NextLevelId() > 0,
                FromEditor = Source == PlaySource.EditorTest,
            }, OverlayLevel);
        }

        /// <summary>完美 = 步数不超过求解器给出的参考解步数（关卡未评估时不算完美）</summary>
        public static bool IsPerfect(LevelData d, int moves) => d != null && d.parMoves > 0 && moves <= d.parMoves;

        public static int NextLevelId()
        {
            var i = LevelOrder.IndexOf(CurrentLevelId);
            return i >= 0 && i + 1 < LevelOrder.Count ? LevelOrder[i + 1] : -1;
        }

        public static void PlayNext()
        {
            var next = NextLevelId();
            if (next > 0) PlayLevel(next);
            else Go(GameScreen.LevelSelect);
        }

        public static void Retry()
        {
            HideIfShown(UINameEnum.WinPanel);
            BoardStage.Instance.Play?.Restart();
        }

        /// <summary>游戏里的"返回"：正式关卡回选关，试玩回编辑器</summary>
        public static void LeavePlay()
        {
            BoardStage.Instance.StopPlay();
            Go(Source == PlaySource.EditorTest ? GameScreen.Editor : GameScreen.LevelSelect);
        }

        // ---------------- 场景切换 ----------------

        public static void BackToMainMenu(GameScreen first = GameScreen.Title)
        {
            GameEntry.NextScreen = first;
            UIManager.HideAll();
            SceneManager.LoadScene(MainScene);
        }

        /// <summary>弹窗提示（覆盖在所有面板之上，点"知道了"关闭）</summary>
        public static void Alert(string title, string message)
        {
            GameAudio.Play("deny");
            UIManager.Show(UINameEnum.AlertPanel, new AlertInfo { Title = title, Message = message }, AlertLevel);
        }

        public static void HideAlert() => HideIfShown(UINameEnum.AlertPanel);

        public static void ShowSettings() => UIManager.Show(UINameEnum.SettingsPanel, SettingsLevel);
        public static void HideSettings() => HideIfShown(UINameEnum.SettingsPanel);

        public static void Quit()
        {
            Progress.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    public sealed class AlertInfo
    {
        public string Title;
        public string Message;
    }

    public sealed class WinInfo
    {
        public GameSession Session;
        public bool NewBest;
        public bool HasNext;
        public bool FromEditor;
    }
}
