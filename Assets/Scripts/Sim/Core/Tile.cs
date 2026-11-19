namespace BoxPusher.Sim
{
    /// <summary>地形（静态层）。箱子 / 玩家是动态层，不在这里。</summary>
    public enum Tile : byte
    {
        Void = 0,   // 场外：不可通行、不渲染，用来做不规则形状
        Wall,       // 墙
        Floor,      // 空地
        Goal,       // 箱子目标点
        Ice,        // 冰面：箱子推上去会一直滑，直到前方被挡或离开冰面
        Plate,      // 机关板：上面有箱子或玩家时，所有机关门打开
        Gate,       // 机关门：关闭时等同于墙；已经站在门里的东西不受影响
        Portal,     // 传送门：成对出现，进入一端时若另一端空着，就被传到另一端
    }

    public enum Dir : byte { Up, Down, Left, Right }

    public static class DirUtil
    {
        public static readonly Dir[] All = { Dir.Up, Dir.Down, Dir.Left, Dir.Right };

        /// <summary>网格 y 轴向下（第 0 行在最上面），与关卡文本一致</summary>
        public static int Dx(Dir d) => d == Dir.Left ? -1 : d == Dir.Right ? 1 : 0;
        public static int Dy(Dir d) => d == Dir.Up ? -1 : d == Dir.Down ? 1 : 0;

        public static char ToChar(Dir d) => d switch { Dir.Up => 'U', Dir.Down => 'D', Dir.Left => 'L', _ => 'R' };
    }

    /// <summary>
    /// 地形字符编码（关卡 JSON 的 terrain 行）：
    /// ' ' 场外  '#' 墙  '-' 空地  '.' 目标  '~' 冰面  '^' 机关板  '=' 机关门  'A'~'H' 传送门（同字母为一对）
    /// </summary>
    public static class TileCodec
    {
        public const int MaxPortalPairs = 8;

        public static char ToChar(Tile t, int portalPair = 0) => t switch
        {
            Tile.Void => ' ',
            Tile.Wall => '#',
            Tile.Floor => '-',
            Tile.Goal => '.',
            Tile.Ice => '~',
            Tile.Plate => '^',
            Tile.Gate => '=',
            Tile.Portal => (char)('A' + portalPair),
            _ => ' ',
        };

        /// <summary>未知字符按场外处理</summary>
        public static Tile FromChar(char c, out int portalPair)
        {
            portalPair = -1;
            switch (c)
            {
                case '#': return Tile.Wall;
                case '-': return Tile.Floor;
                case '.': return Tile.Goal;
                case '~': return Tile.Ice;
                case '^': return Tile.Plate;
                case '=': return Tile.Gate;
            }
            if (c >= 'A' && c < 'A' + MaxPortalPairs)
            {
                portalPair = c - 'A';
                return Tile.Portal;
            }
            return Tile.Void;
        }

        public static string Name(Tile t) => t switch
        {
            Tile.Void => "场外",
            Tile.Wall => "墙",
            Tile.Floor => "空地",
            Tile.Goal => "目标点",
            Tile.Ice => "冰面",
            Tile.Plate => "机关板",
            Tile.Gate => "机关门",
            Tile.Portal => "传送门",
            _ => "?",
        };
    }
}
