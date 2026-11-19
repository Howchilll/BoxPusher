using UnityEngine;

namespace BoxPusher
{
    /// <summary>LevelEditor 场景入口（同样挂在 GameInit 的 doneShow 上；直接运行这个场景也能独立启动框架）</summary>
    public sealed class EditorEntry : MonoBehaviour
    {
        private void Start()
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            BoardStage.Create(true);
            if (FindObjectOfType<UiClickSound>() == null) new GameObject("UiClickSound").AddComponent<UiClickSound>();
            GameApp.BootEditor();
        }
    }
}
