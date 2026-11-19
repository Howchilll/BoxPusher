using UnityEngine;

namespace BoxPusher
{
    /// <summary>占位配色：UI 生成器和运行时共用（暖色木箱 + 深蓝灰底）</summary>
    public static class UiTheme
    {
        public static readonly Color Bg = Hex("#1B1F2A");
        public static readonly Color Panel = Hex("#262C3A");
        public static readonly Color PanelLight = Hex("#323A4C");
        public static readonly Color Line = Hex("#465066");
        public static readonly Color Dim = new(0, 0, 0, 0.7f);

        public static readonly Color Text = Hex("#ECEFF4");
        public static readonly Color Muted = Hex("#9AA3B5");

        public static readonly Color Button = Hex("#3A4459");
        public static readonly Color Primary = Hex("#D9893B");
        public static readonly Color Good = Hex("#7BC96F");
        public static readonly Color Bad = Hex("#E2645A");
        public static readonly Color Star = Hex("#F2C14E");
        public static readonly Color Locked = Hex("#2C3240");
        public static readonly Color Selected = Hex("#4F7CD9");

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        public static string Rich(string text, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{text}</color>";
    }
}
