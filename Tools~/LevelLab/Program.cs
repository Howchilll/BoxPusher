using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using BoxPusher.Sim;

// 用法：dotnet run -- [levels.txt] [--write]   不带 --write 只打印报告
// levels.txt：以 "; <id> <名字>" 开头一个关卡，后面是 LevelText 格式的地图
static class Program
{
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "gen") return Gen(args);
        var path = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : "levels.txt";
        var write = Array.IndexOf(args, "--write") >= 0;
        var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Assets/GameRes/Levels"));
        var levels = ReadLevels(File.ReadAllText(path));
        var failed = 0;
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine($"{"id",3} {"名字",-10} {"尺寸",6} {"箱",2} {"推",4} {"步",4} {"展开",8} {"ms",6} {"分",4} 星级   机关");
        foreach (var d in levels)
        {
            var warnings = new List<string>();
            var errors = LevelValidator.Validate(d, warnings);
            if (errors.Count > 0)
            {
                failed++;
                Console.WriteLine($"{d.id,3} {d.name} 结构错误: {string.Join("; ", errors)}");
                continue;
            }
            var r = Difficulty.Evaluate(d, 3_000_000);
            var m = new LevelMap(d);
            var mech = (m.HasPortals ? "门" : "") + (m.HasIce ? "冰" : "") + (m.HasGates ? "机关" : "");
            Console.WriteLine($"{d.id,3} {d.name,-10} {d.width + "x" + d.height,6} {d.boxes.Length,2} {r.Pushes,4} {r.Moves,4} {r.Explored,8} {r.Millis,6} {d.score,4} {Difficulty.StarText(d.stars)} {mech} {(r.Solved ? "" : "!! " + r.Message)} {string.Join(";", warnings)}");
            if (!r.Solved) { failed++; continue; }
            if (Array.IndexOf(args, "--sol") >= 0) Console.WriteLine("    " + r.SolutionString());
            // 回放验证：用 GameSession 走一遍解法必须通关
            var s = new GameSession(d);
            foreach (var dir in r.Solution) s.Move(dir);
            if (!s.Solved) { failed++; Console.WriteLine("    !! GameSession 回放未通关"); }
            if (write)
            {
                Directory.CreateDirectory(outDir);
                d.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var json = JsonSerializer.Serialize(d, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                File.WriteAllText(Path.Combine(outDir, $"level_{d.id:000}.json"), json);
            }
        }
        if (write) Console.WriteLine("已写入 " + outDir);
        return failed;
    }

    // gen <模板文件> <箱子数> <尝试次数> <种子> [maxPushes]
    // 模板里的墙 / 机关 / 目标保留，箱子和玩家随机摆；要求解法用到模板里出现的每种机关，取难度分最高的
    static int Gen(string[] a)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var tpl = LevelText.Parse(File.ReadAllText(a[1]));
        int boxes = int.Parse(a[2]), tries = int.Parse(a[3]), seed = int.Parse(a[4]);
        int maxPushes = a.Length > 5 ? int.Parse(a[5]) : 999;
                var m = new LevelMap(tpl);
        var free = new List<int>(); var walk = new List<int>();
        for (var c = 0; c < m.Size; c++)
        {
            var t = m.Tiles[c];
            if (t == Tile.Floor || t == Tile.Ice || t == Tile.Plate || t == Tile.Goal) walk.Add(c);
            if (t == Tile.Floor) free.Add(c); // 箱子 / 石头不放在机关板上
        }
        if (boxes == 0) boxes = m.Goals.Length;
        var dead = Solver.DeadCells(m, boxes);
        LevelData best = null; SolveResult bestR = null; var bestScore = -1; int nValid = 0, nSolved = 0, nAbort = 0, nUse = 0;
        var gate = new object();
        var budget = a.Length > 6 ? int.Parse(a[6]) : 60_000;
        var target = a.Length > 7 ? int.Parse(a[7]) : 0;
        Relaxed = a.Length > 8 && a[8] == "any";
        int Dist(int sc) => target > 0 ? -Math.Abs(sc - target) : sc;
        System.Threading.Tasks.Parallel.For(0, tries, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            var rng = new Random(seed * 100003 + i);
            var d = tpl.Clone();
            var set = new HashSet<int>();
            while (set.Count < boxes) { var c = free[rng.Next(free.Count)]; if (!dead[c]) set.Add(c); }
            var stone = -1;
            if (m.Plates.Length > 0) do stone = free[rng.Next(free.Count)]; while (set.Contains(stone));
            int p; do p = walk[rng.Next(walk.Count)]; while (set.Contains(p) || p == stone);
            d.boxes = new List<int>(set).ToArray(); Array.Sort(d.boxes); d.player = p;
            d.stones = stone >= 0 ? new[] { stone } : Array.Empty<int>();
            if (LevelValidator.Validate(d).Count > 0) return;
            System.Threading.Interlocked.Increment(ref nValid);
            var r = Solver.Solve(d, budget);
            if (r.Aborted) System.Threading.Interlocked.Increment(ref nAbort);
            if (!r.Solved || r.Pushes > maxPushes) return;
            System.Threading.Interlocked.Increment(ref nSolved);
            if (!UsesAll(d, m, r)) return;
            System.Threading.Interlocked.Increment(ref nUse);
            var sc = Difficulty.Score(m, boxes, r);
            lock (gate) if (best == null || Dist(sc) > Dist(bestScore) || (Dist(sc) == Dist(bestScore) && string.CompareOrdinal(LevelText.Format(d), LevelText.Format(best)) < 0)) { bestScore = sc; best = d; bestR = r; }
        });
        if (best == null) { Console.WriteLine($"没有找到合格的摆法 valid={nValid} solved={nSolved} aborted={nAbort} usesAll={nUse} (传送{UTele} 滑行{USlide} 石头开门{UGate})"); return 1; }
        Console.WriteLine($"score={bestScore} stars={Difficulty.Stars(bestScore)} pushes={bestR.Pushes} moves={bestR.Moves} explored={bestR.Explored}");
        Console.WriteLine(bestR.SolutionString());
        Console.Write(LevelText.Format(best));
        return 0;
    }

    /// <summary>解法里每种出现的机关都至少触发一次：传送、滑行、石头压上机关板并穿过机关门</summary>
    static int UTele, USlide, UGate;
    /// <summary>gen 第 9 个参数 any：机关门必须用石头打开，传送 / 冰面至少用到一种</summary>
    static bool Relaxed;
    static bool UsesAll(LevelData d, LevelMap m, SolveResult r)
    {
        bool tele = false, slide = false, gate = false, stoneOnPlate = false;
        var s = new GameSession(d);
        foreach (var dir in r.Solution)
        {
            s.Move(dir);
            foreach (var e in s.LastEvents)
            {
                if (e.Kind == MoveKind.Teleport) tele = true;
                if (e.Kind == MoveKind.Slide) slide = true;
                if (m.Tiles[e.To] == Tile.Gate) gate = true;
            }
            foreach (var pl in m.Plates) if (s.Objs[pl] == Obj.Stone) stoneOnPlate = true;
        }
        if (tele) System.Threading.Interlocked.Increment(ref UTele);
        if (slide) System.Threading.Interlocked.Increment(ref USlide);
        if (gate && stoneOnPlate) System.Threading.Interlocked.Increment(ref UGate);
        if (Relaxed) return (!m.HasGates || (gate && stoneOnPlate)) && (tele || slide);
        return (!m.HasPortals || tele) && (!m.HasIce || slide) && (!m.HasGates || (gate && stoneOnPlate));
    }

    static List<LevelData> ReadLevels(string text)
    {
        var result = new List<LevelData>();
        int id = 0; string name = ""; var body = new StringBuilder(); var open = false;
        void Flush() { if (open && body.Length > 0) result.Add(LevelText.Parse(body.ToString(), id, name)); body.Clear(); }
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            if (raw.StartsWith(";"))
            {
                Flush();
                var parts = raw.Substring(1).Trim().Split(' ', 2);
                id = int.Parse(parts[0]); name = parts.Length > 1 ? parts[1].Trim() : ""; open = true;
            }
            else if (raw.StartsWith("//")) continue;
            else if (open) body.AppendLine(raw);
        }
        Flush();
        return result;
    }
}
