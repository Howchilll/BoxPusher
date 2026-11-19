using System;
using System.Collections.Generic;
using BoxPusher.Sim;
using UnityEngine;

namespace BoxPusher
{
    /// <summary>
    /// 程序生成的占位美术（没有美术资源也能看清每种格子）。全部在第一次访问时画进 64×64 贴图并缓存。
    /// 以后换美术：替换这里返回的 Sprite 即可，渲染层不用改。
    /// </summary>
    public static class TileArt
    {
        private const int N = 64;
        private static readonly Dictionary<string, Sprite> Cache = new();

        public static readonly Color[] PortalColors =
        {
            UiTheme.Hex("#B36BFF"), UiTheme.Hex("#3FC1FF"), UiTheme.Hex("#FF6FAE"), UiTheme.Hex("#5CE07A"),
            UiTheme.Hex("#FFB13F"), UiTheme.Hex("#FF5A4F"), UiTheme.Hex("#E8E36B"), UiTheme.Hex("#9CA8FF"),
        };

        private static readonly Color FloorA = UiTheme.Hex("#3B4357");
        private static readonly Color FloorB = UiTheme.Hex("#363D50");
        private static readonly Color WallTop = UiTheme.Hex("#6B7489");
        private static readonly Color WallSide = UiTheme.Hex("#4A5265");
        private static readonly Color GoalColor = UiTheme.Hex("#F2A541");
        private static readonly Color IceColor = UiTheme.Hex("#A8DDF0");
        private static readonly Color PlateColor = UiTheme.Hex("#D1C46A");
        private static readonly Color GateColor = UiTheme.Hex("#C8604A");
        private static readonly Color CrateColor = UiTheme.Hex("#B9773A");
        private static readonly Color CrateDark = UiTheme.Hex("#7A4A22");
        private static readonly Color PlayerColor = UiTheme.Hex("#5AA9E6");

        public static Sprite Terrain(Tile t, bool checker, bool gatesOpen = false, bool pressed = false) => t switch
        {
            Tile.Wall => Get("wall", DrawWall),
            Tile.Floor => checker ? Get("floorA", p => DrawFloor(p, FloorA)) : Get("floorB", p => DrawFloor(p, FloorB)),
            Tile.Goal => Get("goal", DrawGoal),
            Tile.Ice => Get("ice", DrawIce),
            Tile.Plate => pressed ? Get("plateOn", p => DrawPlate(p, true)) : Get("plateOff", p => DrawPlate(p, false)),
            Tile.Gate => gatesOpen ? Get("gateOpen", p => DrawGate(p, true)) : Get("gateClosed", p => DrawGate(p, false)),
            Tile.Portal => Get("portal", DrawPortal),
            _ => null,
        };

        /// <summary>传送门贴图是白色的，按配对着色</summary>
        public static Color TerrainTint(Tile t, int portalPair) =>
            t == Tile.Portal && portalPair >= 0 ? PortalColors[portalPair % PortalColors.Length] : Color.white;

        public static Sprite Box => Get("box", DrawBox);
        public static Sprite Stone => Get("stone", DrawStone);
        public static Sprite Player => Get("player", DrawPlayer);
        public static Sprite Cursor => Get("cursor", DrawCursor);
        public static Sprite Square => Get("square", p => Fill(p, Color.white));

        public static readonly Color BoxOnGoalTint = UiTheme.Hex("#9BE08A");

        // ---------------- 绘制 ----------------

        private static Sprite Get(string key, Action<Color[]> draw)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var px = new Color[N * N];
            draw(px);
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "TileArt_" + key,
            };
            tex.SetPixels(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            s.name = key;
            Cache[key] = s;
            return s;
        }

        private static void Fill(Color[] p, Color c)
        {
            for (var i = 0; i < p.Length; i++) p[i] = c;
        }

        /// <summary>圆角矩形内部的覆盖率（抗锯齿），坐标单位为像素，原点左下</summary>
        private static float RoundRect(float x, float y, float x0, float y0, float x1, float y1, float r)
        {
            var cx = Mathf.Clamp(x, x0 + r, x1 - r);
            var cy = Mathf.Clamp(y, y0 + r, y1 - r);
            var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
            return Mathf.Clamp01(0.5f - d);
        }

        private static float Circle(float x, float y, float cx, float cy, float r) =>
            Mathf.Clamp01(r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) + 0.5f);

        private static void Blend(Color[] p, int i, Color c, float a)
        {
            if (a <= 0) return;
            p[i] = Color.Lerp(p[i], new Color(c.r, c.g, c.b, 1), a * c.a);
            p[i].a = Mathf.Max(p[i].a, a * c.a);
        }

        private static void ForEach(Color[] p, Action<int, float, float> f)
        {
            for (var y = 0; y < N; y++)
            for (var x = 0; x < N; x++)
                f(y * N + x, x + 0.5f, y + 0.5f);
        }

        private static void DrawFloor(Color[] p, Color c)
        {
            Fill(p, c);
            ForEach(p, (i, x, y) => { if (x < 1 || y < 1) p[i] = c * 0.88f; p[i].a = 1; });
        }

        private static void DrawWall(Color[] p)
        {
            Fill(p, WallSide);
            ForEach(p, (i, x, y) =>
            {
                Blend(p, i, WallTop, RoundRect(x, y, 3, 12, 61, 61, 6));
                // 砖缝
                var row = (int)(y / 16);
                var off = row % 2 == 0 ? 0 : 16;
                if (y > 12 && ((int)y % 16 == 0 || ((int)(x + off) % 32 == 0))) Blend(p, i, WallSide, 0.6f);
            });
        }

        private static void DrawGoal(Color[] p)
        {
            DrawFloor(p, FloorA);
            ForEach(p, (i, x, y) =>
            {
                var ring = Circle(x, y, 32, 32, 18) - Circle(x, y, 32, 32, 13);
                Blend(p, i, GoalColor, ring);
                Blend(p, i, GoalColor, Circle(x, y, 32, 32, 5));
            });
        }

        private static void DrawIce(Color[] p)
        {
            Fill(p, IceColor);
            ForEach(p, (i, x, y) =>
            {
                var s = (x + y) % 22f;
                if (s < 2.2f && x + y > 20 && x + y < 100) Blend(p, i, Color.white, 0.7f);
                if (x < 1 || y < 1) Blend(p, i, IceColor * 0.85f, 1);
            });
        }

        private static void DrawPlate(Color[] p, bool pressed)
        {
            DrawFloor(p, FloorA);
            var top = pressed ? PlateColor * 0.75f : PlateColor;
            ForEach(p, (i, x, y) =>
            {
                Blend(p, i, PlateColor * 0.55f, RoundRect(x, y, 10, 8, 54, 52, 7));
                Blend(p, i, top, RoundRect(x, y, 10, pressed ? 9 : 13, 54, pressed ? 52 : 56, 7));
            });
        }

        private static void DrawGate(Color[] p, bool open)
        {
            DrawFloor(p, FloorB);
            ForEach(p, (i, x, y) =>
            {
                if (open)
                {
                    var frame = RoundRect(x, y, 2, 2, 62, 62, 4) - RoundRect(x, y, 6, 6, 58, 58, 3);
                    Blend(p, i, GateColor, frame * 0.6f);
                    return;
                }
                Blend(p, i, GateColor * 0.6f, RoundRect(x, y, 2, 2, 62, 62, 4));
                for (var b = 0; b < 4; b++)
                    Blend(p, i, GateColor, RoundRect(x, y, 8 + b * 13, 4, 16 + b * 13, 60, 3));
            });
        }

        private static void DrawPortal(Color[] p)
        {
            DrawFloor(p, FloorA);
            ForEach(p, (i, x, y) =>
            {
                Blend(p, i, Color.white, (Circle(x, y, 32, 32, 26) - Circle(x, y, 32, 32, 20)));
                Blend(p, i, Color.white, (Circle(x, y, 32, 32, 15) - Circle(x, y, 32, 32, 11)) * 0.7f);
                Blend(p, i, Color.white, Circle(x, y, 32, 32, 5) * 0.5f);
            });
            // 底色不跟着着色：把地板部分改回接近白色的乘数，着色后地板会偏色，所以地板压暗
            for (var i = 0; i < p.Length; i++)
                if (p[i].r < 0.5f) p[i] = new Color(0.32f, 0.32f, 0.36f, 1);
        }

        private static void DrawBox(Color[] p)
        {
            Fill(p, Color.clear);
            ForEach(p, (i, x, y) =>
            {
                var body = RoundRect(x, y, 5, 5, 59, 59, 5);
                if (body <= 0) return;
                p[i] = new Color(CrateDark.r, CrateDark.g, CrateDark.b, body);
                var inner = RoundRect(x, y, 10, 10, 54, 54, 3);
                Blend(p, i, CrateColor, inner);
                // 对角木条
                var d1 = Mathf.Abs((x - 10) - (y - 10));
                var d2 = Mathf.Abs((x - 10) - (54 - y));
                if (inner > 0 && (d1 < 4 || d2 < 4)) Blend(p, i, CrateDark, 0.85f);
            });
        }

        /// <summary>石头：灰色圆角块 + 高光，和木箱一眼区分</summary>
        private static void DrawStone(Color[] p)
        {
            Fill(p, Color.clear);
            var dark = UiTheme.Hex("#5E6470");
            var light = UiTheme.Hex("#9AA0AC");
            ForEach(p, (i, x, y) =>
            {
                var body = RoundRect(x, y, 7, 6, 57, 54, 18);
                if (body <= 0) return;
                p[i] = new Color(dark.r, dark.g, dark.b, body);
                Blend(p, i, light, RoundRect(x, y, 11, 12, 53, 52, 15));
                Blend(p, i, Color.white, Circle(x, y, 24, 40, 6) * 0.35f);
                Blend(p, i, dark, Circle(x, y, 40, 24, 4) * 0.6f);
            });
        }

        private static void DrawPlayer(Color[] p)
        {
            Fill(p, Color.clear);
            ForEach(p, (i, x, y) =>
            {
                var body = Circle(x, y, 32, 30, 24);
                if (body <= 0) return;
                p[i] = new Color(PlayerColor.r * 0.7f, PlayerColor.g * 0.7f, PlayerColor.b * 0.7f, body);
                Blend(p, i, PlayerColor, Circle(x, y, 32, 31, 21));
                Blend(p, i, Color.white, Circle(x, y, 24, 36, 6));
                Blend(p, i, Color.white, Circle(x, y, 40, 36, 6));
                Blend(p, i, UiTheme.Hex("#1B1F2A"), Circle(x, y, 25, 35, 3));
                Blend(p, i, UiTheme.Hex("#1B1F2A"), Circle(x, y, 41, 35, 3));
            });
        }

        private static void DrawCursor(Color[] p)
        {
            Fill(p, Color.clear);
            ForEach(p, (i, x, y) =>
            {
                var a = RoundRect(x, y, 1, 1, 63, 63, 6) - RoundRect(x, y, 5, 5, 59, 59, 4);
                if (a > 0) p[i] = new Color(1, 1, 1, a);
            });
        }
    }
}
