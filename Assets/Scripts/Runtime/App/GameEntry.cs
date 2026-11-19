using UnityEngine;

namespace BoxPusher
{
    /// <summary>Main 场景入口，挂在 GameInit 的 doneShow 节点上，HowFrame 初始化完成后才激活</summary>
    public sealed class GameEntry : MonoBehaviour
    {
        /// <summary>从编辑器返回时直接进选关</summary>
        public static GameScreen NextScreen = GameScreen.Title;

        private void Start()
        {
            Physics2D.simulationMode = SimulationMode2D.Script; // 不用物理
            BoardStage.Create(false);
            if (FindObjectOfType<UiClickSound>() == null) new GameObject("UiClickSound").AddComponent<UiClickSound>();
            GameApp.BootMain(NextScreen);
            NextScreen = GameScreen.Title;
        }
    }
}
