using UnityEngine;
using UnityEngine.UI;

namespace BoxPusher
{
    /// <summary>给 UIRoot 下所有按钮自动挂点击音效（面板是运行时实例化的，所以定时扫描新出现的按钮）</summary>
    public sealed class UiClickSound : MonoBehaviour
    {
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            var root = GameObject.Find("UIRoot");
            if (root == null) return;
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                if (b.GetComponent<Hooked>() != null) continue;
                b.gameObject.AddComponent<Hooked>();
                b.onClick.AddListener(() => GameAudio.Play("click"));
            }
        }

        private sealed class Hooked : MonoBehaviour { }
    }
}
