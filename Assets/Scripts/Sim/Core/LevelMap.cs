using System.Collections.Generic;

namespace BoxPusher.Sim
{
    /// <summary>运行时的静态地形（一维数组），由 LevelData 构建，之后只读。规则 / 求解器 / 渲染都读它。</summary>
    public sealed class LevelMap
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Tile[] Tiles;
        /// <summary>传送门另一端的格子，不是传送门或缺另一端时为 -1</summary>
        public readonly int[] Partner;
        /// <summary>传送门的配对编号（0 = A），非传送门为 -1，渲染配色用</summary>
        public readonly int[] PortalPair;
        public readonly int[] Goals;
        public readonly int[] Plates;
        public readonly bool HasGates;
        public readonly bool HasIce;
        public readonly bool HasPortals;

        public int Size => Width * Height;

        public LevelMap(LevelData d)
        {
            Width = d.width;
            Height = d.height;
            Tiles = new Tile[Size];
            Partner = new int[Size];
            PortalPair = new int[Size];
            var goals = new List<int>();
            var plates = new List<int>();
            var firstOfPair = new int[TileCodec.MaxPortalPairs];
            for (var i = 0; i < firstOfPair.Length; i++) firstOfPair[i] = -1;

            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var c = y * Width + x;
                var t = d.TileAt(x, y, out var pp);
                Tiles[c] = t;
                Partner[c] = -1;
                PortalPair[c] = pp;
                switch (t)
                {
                    case Tile.Goal: goals.Add(c); break;
                    case Tile.Plate: plates.Add(c); break;
                    case Tile.Gate: HasGates = true; break;
                    case Tile.Ice: HasIce = true; break;
                    case Tile.Portal:
                        HasPortals = true;
                        if (firstOfPair[pp] < 0) firstOfPair[pp] = c;
                        else if (Partner[firstOfPair[pp]] < 0)
                        {
                            Partner[c] = firstOfPair[pp];
                            Partner[firstOfPair[pp]] = c;
                        }
                        break;
                }
            }
            Goals = goals.ToArray();
            Plates = plates.ToArray();
        }

        public int X(int cell) => cell % Width;
        public int Y(int cell) => cell / Width;
        public int Cell(int x, int y) => y * Width + x;

        /// <summary>越界返回 -1</summary>
        public int Neighbor(int cell, Dir d)
        {
            var x = cell % Width + DirUtil.Dx(d);
            var y = cell / Width + DirUtil.Dy(d);
            return x < 0 || y < 0 || x >= Width || y >= Height ? -1 : y * Width + x;
        }

        /// <summary>永久不可通行（墙 / 场外）</summary>
        public bool IsSolid(int cell) => cell < 0 || Tiles[cell] == Tile.Wall || Tiles[cell] == Tile.Void;
    }
}
