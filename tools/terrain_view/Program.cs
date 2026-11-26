// Pictures of a world's ground worked out from the game's own terrain rules, with no engine.
//
//   dotnet run -c Release -- shot <body> <latitude> <longitude> <metres above ground> <heading> <pitch> <out.ppm> [seed] [sun height] [sun bearing]
//   dotnet run -c Release -- globe <body> <latitude> <longitude> <out.ppm> [seed]
//   dotnet run -c Release -- bench            (time to build ground tiles on every world)
//   dotnet run -c Release -- stats <body>     (how steep and how high the ground is)
//
// A ray is walked from the eye through every dot of the picture until it meets the ground, which is then
// coloured by PlanetTerrain.ColorAt and lit by a plain sun, sky light, shadow and haze. It is NOT the game's
// shader: the fine ground relief is copied from it (set OA_RELIEF=0 to leave it out), but there is no bloom
// or tone mapping, the light is simpler, the colour is worked out at every dot (the game does it
// at every corner of its ground tiles), and trees, rocks and bases are not drawn. It is for judging the shape
// and colour of the land.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Look;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;

static class TerrainView
{
    const int W = 960, H = 540;

    static PlanetTerrain Make(string body, int seed)
    {
        PlanetProfile profile = PlanetProfiles.Defaults().First(p => p.bodyId == body);
        double radius = SolarSystemDefaults.Bodies().First(b => b.id == body).radius;
        return new PlanetTerrain(profile, body, radius, seed);
    }

    static int Main(string[] args)
    {
        string dial = Environment.GetEnvironmentVariable("OA_RELIEF");
        if (!string.IsNullOrEmpty(dial)) Relief = double.Parse(dial);
        string lightDial = Environment.GetEnvironmentVariable("OA_LIGHT");
        if (lightDial == "game") LightMode = 1; else if (lightDial == "new") LightMode = 2;
        string ssDial = Environment.GetEnvironmentVariable("OA_SS");
        if (!string.IsNullOrEmpty(ssDial)) SS = Math.Max(1, int.Parse(ssDial));
        if (args.Length == 0) { Console.WriteLine("shot | globe | bench | stats"); return 1; }
        switch (args[0])
        {
            case "shot":
            {
                int seed = args.Length > 8 ? int.Parse(args[8]) : 1;
                double sunUp = args.Length > 9 ? double.Parse(args[9]) : 32;
                double sunBearing = args.Length > 10 ? double.Parse(args[10]) : 115;
                PlanetTerrain t = Make(args[1], seed);
                Vec3d up = CubeSphere.FromLatLon(double.Parse(args[2]), double.Parse(args[3]));
                double ground = Math.Max(t.Height(up), t.HasSea ? t.SeaLevel : -1e9);
                Vec3d eye = up * (t.Radius + ground + double.Parse(args[4]));
                Vec3d north = CubeSphere.North(up), east = Vec3d.Cross(north, up).Normalized;
                // Pitch is measured from the horizon, which on these small worlds lies well below level.
                double dip = Math.Acos(Math.Min(1, (t.Radius + ground) / eye.Magnitude));
                double heading = double.Parse(args[5]) * Math.PI / 180, pitch = double.Parse(args[6]) * Math.PI / 180 - dip;
                Vec3d flat = north * Math.Cos(heading) + east * Math.Sin(heading);
                Vec3d forward = (flat * Math.Cos(pitch) + up * Math.Sin(pitch)).Normalized;
                double sb = sunBearing * Math.PI / 180, su = sunUp * Math.PI / 180;
                Vec3d sun = ((north * Math.Cos(sb) + east * Math.Sin(sb)) * Math.Cos(su) + up * Math.Sin(su)).Normalized;
                if (Environment.GetEnvironmentVariable("OA_PROPS") == "1") AddProps(t, args[1], up, flat, Vec3d.Cross(up, flat).Normalized);
                Render(t, eye, forward, up, sun, 62, args[7]);
                return 0;
            }
            case "globe":
            {
                int seed = args.Length > 5 ? int.Parse(args[5]) : 1;
                PlanetTerrain t = Make(args[1], seed);
                Vec3d up = CubeSphere.FromLatLon(double.Parse(args[2]), double.Parse(args[3]));
                Vec3d eye = up * (t.Radius * 3.4);
                Vec3d north = CubeSphere.North(up), east = Vec3d.Cross(north, up).Normalized;
                Vec3d sun = (up * 0.55 + east * 0.75 + north * 0.35).Normalized;
                Render(t, eye, -up, north, sun, 40, args[4], false);
                return 0;
            }
            case "map":
            {
                // The picture a body wears from far off (FarMap, the game's own), as a flat map: map <body> <width> <out.ppm> [seed]
                int width = int.Parse(args[2]);
                PlanetTerrain t = Make(args[1], args.Length > 4 ? int.Parse(args[4]) : 1);
                byte[] rgba = FarMap.Paint(t, width);
                int height = width / 2;
                using var file = System.IO.File.Create(args[3]);
                byte[] head = System.Text.Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n");
                file.Write(head, 0, head.Length);
                var row = new byte[width * 3];
                for (int y = height - 1; y >= 0; y--)     // the picture's first row is the south pole
                {
                    for (int x = 0; x < width; x++)
                    {
                        row[x * 3] = rgba[(y * width + x) * 4];
                        row[x * 3 + 1] = rgba[(y * width + x) * 4 + 1];
                        row[x * 3 + 2] = rgba[(y * width + x) * 4 + 2];
                    }
                    file.Write(row, 0, row.Length);
                }
                if (args.Length > 5)
                {
                    // And the heights behind it: map <body> <width> <out.ppm> <seed> <heights.ppm>. Red is the
                    // height from lowest (0) to highest (255), green is 255 where there is sea.
                    var heights = new double[width * height];
                    double low = 1e9, high = -1e9;
                    double dot = t.Radius * Math.PI * 2 / width;
                    System.Threading.Tasks.Parallel.For(0, height, y =>
                    {
                        for (int x = 0; x < width; x++)
                            heights[y * width + x] = t.Sample(FarMap.Direction((x + 0.5) / width, (y + 0.5) / height), dot * 1.6).Height;
                    });
                    foreach (double h in heights) { low = Math.Min(low, h); high = Math.Max(high, h); }
                    using var second = System.IO.File.Create(args[5]);
                    second.Write(head, 0, head.Length);
                    for (int y = height - 1; y >= 0; y--)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            double h = heights[y * width + x];
                            row[x * 3] = (byte)Math.Round((h - low) / Math.Max(1e-6, high - low) * 255);
                            row[x * 3 + 1] = (byte)(t.HasSea && h < t.SeaLevel ? 255 : 0);
                            row[x * 3 + 2] = 0;
                        }
                        second.Write(row, 0, row.Length);
                    }
                    Console.WriteLine($"{args[1]} radius {t.Radius} lowest {low:F1} highest {high:F1} sea {(t.HasSea ? t.SeaLevel.ToString("F1") : "none")}");
                }
                return 0;
            }
            case "flat":
            {
                // Open, fairly level places with hills in view: somewhere to stand for a picture.
                PlanetTerrain t = Make(args[1], args.Length > 2 ? int.Parse(args[2]) : 1);
                var found = new List<(double score, double lat, double lon)>();
                for (double lat = -60; lat <= 60; lat += 4)
                    for (double lon = -180; lon < 180; lon += 4)
                    {
                        Vec3d u = CubeSphere.FromLatLon(lat, lon);
                        double h = t.Height(u);
                        if (t.HasSea && h < t.SeaLevel + 2) continue;
                        Vec3d e = u.AnyPerpendicular(), n = Vec3d.Cross(u, e);
                        double near = 0, far = 0;
                        for (int i = 0; i < 12; i++)
                        {
                            double a = i * Math.PI / 6;
                            Vec3d d = e * Math.Cos(a) + n * Math.Sin(a);
                            near = Math.Max(near, Math.Abs(t.Height((u + d * (30 / t.Radius)).Normalized) - h));
                            far = Math.Max(far, t.Height((u + d * (0.5)).Normalized) - h);
                        }
                        if (near < 4) found.Add((far - near * 4, lat, lon));
                    }
                foreach (var f in found.OrderByDescending(f => f.score).Take(8)) Console.WriteLine($"{f.lat} {f.lon}  relief in view {f.score:F0}");
                Console.WriteLine($"radius {t.Radius:F0}");
                return 0;
            }
            case "land":
            {
                // Where the land is: one letter per 6 degrees (space sea, . low, o hills, M high).
                PlanetTerrain t = Make(args[1], args.Length > 2 ? int.Parse(args[2]) : 1);
                for (int lat = 84; lat >= -84; lat -= 6)
                {
                    var line = new System.Text.StringBuilder($"{lat,4} ");
                    for (int lon = -180; lon < 180; lon += 6)
                    {
                        double h = t.Height(CubeSphere.FromLatLon(lat, lon));
                        double sea = t.HasSea ? t.SeaLevel : -1e9;
                        line.Append(h < sea ? ' ' : h < sea + 8 || (!t.HasSea && h < 0) ? '.' : h < (t.HasSea ? 30 : 15) ? 'o' : 'M');
                    }
                    Console.WriteLine(line);
                }
                return 0;
            }
            case "bases":
            {
                // How many bases find a place on each world, over eight seeds, next to how many are wanted.
                var rules = new OrbitalAnte.Bases.BaseFieldRules();
                foreach (PlanetProfile profile in PlanetProfiles.Defaults())
                {
                    var line = new System.Text.StringBuilder($"{profile.bodyId,-10}");
                    PlanetTerrain first = Make(profile.bodyId, 1);
                    double slowest = 0;
                    line.Append($" wanted {OrbitalAnte.Bases.BaseField.BaseCount(rules, first.Radius),2}, found");
                    for (int seed = 1; seed <= 8; seed++)
                    {
                        PlanetTerrain t = seed == 1 ? first : Make(profile.bodyId, seed);
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        int found = OrbitalAnte.Bases.BaseField.Build(rules, t, new SeededRandom(seed * 31), new[] { 2, 2, 1 }, Vec3d.Zero).Count;
                        slowest = Math.Max(slowest, watch.Elapsed.TotalMilliseconds);
                        line.Append($" {found,2}");
                    }
                    line.Append($"  (slowest search {slowest:F0} ms on this machine)");
                    Console.WriteLine(line);
                }
                return 0;
            }
            case "ridges":
            {
                // How tall worn mountains come out next to plain ridged ones, to set the gain that keeps peaks as tall.
                var random = new Random(2);
                foreach (double wear in new[] { 0.0, 0.2, 0.4, 0.6, 1.0 })
                {
                    var plain = new System.Collections.Generic.List<double>(); var worn = new System.Collections.Generic.List<double>();
                    for (int i = 0; i < 60000; i++)
                    {
                        var p = new Vec3d(random.NextDouble() * 3000, random.NextDouble() * 3000, random.NextDouble() * 3000);
                        plain.Add(Noise3.Ridged(p, 240, 5, 5)); worn.Add(Noise3.ErodedRidged(p, 240, 5, 5, wear));
                    }
                    plain.Sort(); worn.Sort();
                    Console.WriteLine($"wear {wear}: mean {worn.Average() / plain.Average():F2}, 90th {worn[54000] / plain[54000]:F2}, 99th {worn[59400] / plain[59400]:F2}, top {worn[59999]:F2} vs {plain[59999]:F2}");
                }
                return 0;
            }
            case "find":
            {
                // Where each standout landform is at its strongest on a world, to point a camera at it.
                string body = args[1];
                PlanetTerrain with = Make(body, 1);
                foreach (string what in new[] { "canyon", "ridge", "spire", "dune" })
                {
                    PlanetProfile bare = PlanetProfiles.Defaults().First(x => x.bodyId == body);
                    if (what == "canyon") bare.canyonDepth = 0; else if (what == "ridge") bare.ridgeHeight = 0; else if (what == "spire") bare.spireHeight = 0; else bare.duneHeight = 0;
                    var without = new PlanetTerrain(bare, body, SolarSystemDefaults.Bodies().First(x => x.id == body).radius, 1);
                    double best = 0; int bestLat = 0, bestLon = 0;
                    for (int lat = -70; lat <= 70; lat += 4)
                    {
                        for (int lon = -180; lon < 180; lon += 4)
                        {
                            // The change summed over a patch about 60 m across.
                            double sum = 0;
                            Vec3d up = CubeSphere.FromLatLon(lat, lon), u = up.AnyPerpendicular(), v = Vec3d.Cross(up, u);
                            for (int i = -3; i <= 3; i++) for (int j = -3; j <= 3; j++)
                            {
                                Vec3d d = (up + u * (i * 10 / with.Radius) + v * (j * 10 / with.Radius)).Normalized;
                                sum += Math.Abs(with.Height(d) - without.Height(d));
                            }
                            if (sum > best) { best = sum; bestLat = lat; bestLon = lon; }
                        }
                    }
                    Console.WriteLine($"{body} {what}: latitude {bestLat}, longitude {bestLon} (average change {best / 49:F1} m)");
                }
                return 0;
            }
            case "bench": Bench(); return 0;
            case "cost": foreach (string body in new[] { "Earth", "Mars", "Moon", "Titan" }) Cost(Make(body, 1)); return 0;
            case "stats": Stats(Make(args[1], 1)); return 0;
        }
        return 1;
    }

    static void Bench()
    {
        // Once round first without the clock, so the first world is not charged for starting up.
        TerrainMesher.Build(Make("Earth", 1), 0, 3, 1, 1, true);
        TerrainMesher.Build(Make("Mars", 1), 0, 3, 1, 1, true);
        foreach (string body in new[] { "Earth", "Mars", "Moon", "Jupiter", "Europa", "Titan" })
        {
            PlanetTerrain t = Make(body, 1);
            int deepest = TileRules.MaxLevel(t.Radius, 1.7);
            var clock = Stopwatch.StartNew();
            int built = 0;
            for (int face = 0; face < 6; face++)
            {
                for (int k = 0; k < 6; k++)
                {
                    int n = 1 << deepest;
                    TerrainMesher.Build(t, face, deepest, (k * 37 + 5) % n, (k * 91 + 11) % n, true);
                    built++;
                }
            }
            double fine = clock.Elapsed.TotalMilliseconds / built;
            clock.Restart();
            built = 0;
            for (int face = 0; face < 6; face++)
            {
                for (int k = 0; k < 4; k++) { TerrainMesher.Build(t, face, 2, k, (k * 3) % 4, true); built++; }
            }
            double coarse = clock.Elapsed.TotalMilliseconds / built;
            Console.WriteLine($"{body}: finest tile (level {deepest}) {fine:F1} ms, level 2 tile {coarse:F1} ms, {t.Stamps.Count} stamps");
        }
    }

    /// <summary>What one question of the ground costs, in millionths of a second: its height, and its colour.</summary>
    static void Cost(PlanetTerrain t)
    {
        var random = new Random(9);
        int n = 150000;
        var dirs = new Vec3d[n];
        for (int i = 0; i < n; i++) dirs[i] = new Vec3d(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1).Normalized;
        double sum = 0;
        for (int i = 0; i < 20000; i++) { TerrainPoint w = t.Sample(dirs[i]); t.ColorAt(dirs[i], w, 0.6, out float a, out _, out _); sum += a; }
        var points = new TerrainPoint[n];
        for (int i = 0; i < n; i++) points[i] = t.Sample(dirs[i]);
        // The quickest of five goes: other work on the machine can only make a go slower, never quicker.
        double fine = 1e9, coarse = 1e9, gentle = 1e9, steep = 1e9;
        for (int go = 0; go < 5; go++)
        {
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < n; i++) sum += t.Height(dirs[i]);
            fine = Math.Min(fine, clock.Elapsed.TotalMilliseconds * 1000 / n);
            clock.Restart();
            for (int i = 0; i < n; i++) sum += t.Height(dirs[i], 60);
            coarse = Math.Min(coarse, clock.Elapsed.TotalMilliseconds * 1000 / n);
            clock.Restart();
            for (int i = 0; i < n; i++) { t.ColorAt(dirs[i], points[i], 0.2, out float a, out _, out _); sum += a; }
            gentle = Math.Min(gentle, clock.Elapsed.TotalMilliseconds * 1000 / n);
            clock.Restart();
            for (int i = 0; i < n; i++) { t.ColorAt(dirs[i], points[i], 0.7, out float a, out _, out _); sum += a; }
            steep = Math.Min(steep, clock.Elapsed.TotalMilliseconds * 1000 / n);
        }
        Console.WriteLine($"{t.BodyId}: height {fine:F2}, far height {coarse:F2}, colour {gentle:F2} (steep {steep:F2}) millionths of a second");
    }

    static void Stats(PlanetTerrain t)
    {
        var random = new Random(4);
        int n = 20000, land = 0, flat = 0, steep = 0;
        double low = 1e9, high = -1e9, sum = 0;
        for (int i = 0; i < n; i++)
        {
            Vec3d d = new Vec3d(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1);
            if (d.SqrMagnitude > 1 || d.SqrMagnitude < 0.01) { i--; continue; }
            d = d.Normalized;
            double h = t.Height(d);
            low = Math.Min(low, h); high = Math.Max(high, h); sum += h;
            if (t.HasSea && h < t.SeaLevel) continue;
            land++;
            double s = t.SlopeAt(d, 3);
            if (s < 0.12) flat++;
            if (s > 0.6) steep++;
        }
        Console.WriteLine($"{t.BodyId}: heights {low:F0} to {high:F0} m (stated {t.MinHeight:F0} to {t.MaxHeight:F0}), mean {sum / n:F1}; land {100.0 * land / n:F0}%, of it level enough to land on {100.0 * flat / land:F0}%, cliffs {100.0 * steep / land:F0}%");
    }

    static void Render(PlanetTerrain t, Vec3d eye, Vec3d forward, Vec3d upHint, Vec3d sun, double fovDegrees, string path, bool inTheAir = true)
    {
        PlanetProfile f = t.Profile;
        int W = TerrainView.W * SS, H = TerrainView.H * SS;
        Vec3d right = Vec3d.Cross(upHint, forward).Normalized;
        Vec3d up = Vec3d.Cross(forward, right).Normalized;
        double tan = Math.Tan(fovDegrees * Math.PI / 360);
        float[] sky = ColorUtil.Parse(f.skyColor), haze = ColorUtil.Parse(f.hazeColor), sea = ColorUtil.Parse(f.seaColor), deep = ColorUtil.Parse(f.seaDeepColor);
        // From space there is black sky and no haze in the way; the game draws its own air round the globe.
        bool air = f.hasAtmosphere && inTheAir;
        double visibility = air ? f.visibility : 1e9;
        double top = t.Radius + t.MaxHeight + 2;
        var bytes = new byte[W * H * 3];
        var clock = Stopwatch.StartNew();
        Parallel.For(0, H, y =>
        {
            for (int x = 0; x < W; x++)
            {
                double px = (2.0 * (x + 0.5) / W - 1) * tan * W / H, py = (1 - 2.0 * (y + 0.5) / H) * tan;
                Vec3d dir = (forward + right * px + up * py).Normalized;
                double r, g, b;
                Vec3d eyeUp = eye.Normalized;
                double lift = Vec3d.Dot(dir, eyeUp);
                if (air)
                {
                    double k = Math.Pow(Math.Max(0, 1 - Math.Max(0, lift)), 3);
                    r = sky[0] + (haze[0] - sky[0]) * k; g = sky[1] + (haze[1] - sky[1]) * k; b = sky[2] + (haze[2] - sky[2]) * k;
                    double glow = Math.Pow(Math.Max(0, Vec3d.Dot(dir, sun)), 180);
                    r += glow; g += glow * 0.95; b += glow * 0.8;
                }
                else
                {
                    r = g = b = 0.012;
                }

                bool ground = March(t, eye, dir, top, out double dist, out bool water);
                bool prop = PropHit(eye, dir, ground ? dist : 1e9, out double propAt, out Vec3d propNormal, out float[] propColor);
                if (prop) { dist = propAt; water = false; }
                if (ground || prop)
                {
                    Vec3d p = eye + dir * dist;
                    Vec3d n = p.Normalized;
                    double minW = Math.Max(2.7, dist * 0.012);
                    double step = Math.Max(1.7, dist * 0.006);
                    Vec3d normal;
                    float cr, cg, cb;
                    if (prop)
                    {
                        normal = propNormal;
                        // A little break-up so a stand-in is not one flat colour.
                        float mottle = (float)(0.9 + 0.2 * ShaderNoise(p.X * 3.1, p.Y * 3.1, p.Z * 3.1));
                        cr = propColor[0] * mottle; cg = propColor[1] * mottle; cb = propColor[2] * mottle;
                    }
                    else if (water)
                    {
                        double bed = t.SeaLevel - t.Height(n, minW);
                        double murk = Noise3.Smooth(0, 14, bed);
                        t.ColorAt(n, t.Sample(n, minW), 0, out float br, out float bg, out float bb);
                        cr = (float)(br + (sea[0] - br) * (0.55 + 0.45 * murk)); cg = (float)(bg + (sea[1] - bg) * (0.55 + 0.45 * murk)); cb = (float)(bb + (sea[2] - bb) * (0.55 + 0.45 * murk));
                        cr += (deep[0] - cr) * (float)(murk * 0.6); cg += (deep[1] - cg) * (float)(murk * 0.6); cb += (deep[2] - cb) * (float)(murk * 0.6);
                        normal = n;
                    }
                    else
                    {
                        Vec3d u = n.AnyPerpendicular(), v = Vec3d.Cross(n, u);
                        double a = step / t.Radius;
                        Vec3d d1 = (n + u * a).Normalized, d2 = (n + v * a).Normalized, d3 = (n - u * a).Normalized, d4 = (n - v * a).Normalized;
                        Vec3d p1 = d1 * (t.Radius + t.Height(d1, minW)), p2 = d2 * (t.Radius + t.Height(d2, minW));
                        Vec3d p3 = d3 * (t.Radius + t.Height(d3, minW)), p4 = d4 * (t.Radius + t.Height(d4, minW));
                        normal = Vec3d.Cross(p1 - p3, p2 - p4).Normalized;
                        if (Vec3d.Dot(normal, n) < 0) normal = -normal;
                        double cos = Vec3d.Dot(normal, n);
                        double slope = Math.Sqrt(Math.Max(0, 1 - cos * cos));
                        TerrainPoint here = t.Sample(n, minW);
                        double around = (t.Height(d1, minW) + t.Height(d2, minW) + t.Height(d3, minW) + t.Height(d4, minW)) * 0.25;
                        double hollow = Math.Max(-1, Math.Min(1, (around - here.Height) / (step * step) * TerrainMesher.HollowGain));
                        t.ColorAt(n, here, slope, out cr, out cg, out cb, hollow);
                    }
                    if (!prop && !water && Relief > 0)
                    {
                        // The ground shader's fine relief and break-up of colour, copied sum for sum (OA_Ground in
                        // OA_Terrain.shader), so the picture shows what it adds. Its slope is taken by stepping
                        // a little way each side, where the shader uses the difference between neighbouring dots.
                        double e = Math.Max(0.02, dist * 0.0012);
                        Vec3d tu = n.AnyPerpendicular(), tv = Vec3d.Cross(n, tu);
                        double h0 = Ground(p, dist, out double broad, out double mid, out double fine, out double stone, out double nearMid, out double nearFine);
                        double hu = Ground(p + tu * e, dist, out _, out _, out _, out _, out _, out _);
                        double hv = Ground(p + tv * e, dist, out _, out _, out _, out _, out _, out _);
                        normal = (normal - tu * ((hu - h0) / e) - tv * ((hv - h0) / e)).Normalized;
                        double value = (broad - 0.5) * 0.12 + (mid - 0.5) * 0.11 * nearMid + (fine - 0.5) * 0.10 * nearFine;
                        double grey = cr * 0.3 + cg * 0.5 + cb * 0.2;
                        double sr = cr + (cr * 1.05 - cr) * broad, sg = cg, sb = cb + (cb * 0.93 - cb) * broad;
                        double k = stone * nearFine * 0.45;
                        sr += (grey * 1.12 - sr) * k; sg += (grey * 1.1 - sg) * k; sb += (grey * 1.06 - sb) * k;
                        double amount = Math.Min(1, Relief);
                        cr = (float)(cr + (sr * (1 + value) - cr) * amount); cg = (float)(cg + (sg * (1 + value) - cg) * amount); cb = (float)(cb + (sb * (1 + value) - cb) * amount);
                    }
                    double day = Noise3.Smooth(-0.1, 0.25, Vec3d.Dot(n, sun));
                    double lr, lg, lb;
                    if (LightMode == 0)
                    {
                        double lit = Math.Max(0, Vec3d.Dot(normal, sun));
                        if (lit > 0 && Shadowed(t, p + n * 0.6, sun, top)) lit *= 0.12;
                        double skyLight = (air ? 0.34 : 0.06) * (0.6 + 0.4 * Vec3d.Dot(normal, n)) * day;
                        lr = Lin(cr) * (lit * 1.0 * 1.02 + skyLight * (air ? Lin(sky[0]) * 1.2 + 0.25 : 1));
                        lg = Lin(cg) * (lit * 0.97 + skyLight * (air ? Lin(sky[1]) * 1.2 + 0.25 : 1));
                        lb = Lin(cb) * (lit * 0.90 + skyLight * (air ? Lin(sky[2]) * 1.2 + 0.25 : 1));
                    }
                    else
                    {
                        LightAt(t, air, sky, haze, p, n, normal, sun, eyeUp, top, x, y, prop, out double ar, out double ag, out double ab);
                        lr = Lin(cr) * ar; lg = Lin(cg) * ag; lb = Lin(cb) * ab;
                    }
                    if (water)
                    {
                        Vec3d half = (sun - dir).Normalized;
                        double spec = Math.Pow(Math.Max(0, Vec3d.Dot(half, n)), 220) * 1.6;
                        double fres = Math.Pow(1 - Math.Max(0, -Vec3d.Dot(dir, n)), 5);
                        lr += spec + fres * Lin(haze[0]) * 0.5; lg += spec + fres * Lin(haze[1]) * 0.5; lb += spec * 0.9 + fres * Lin(haze[2]) * 0.5;
                    }
                    double fog = air ? 1 - Math.Exp(-dist / visibility * 1.6) : 0;
                    r = Srgb(lr + (Lin(haze[0]) * day - lr) * fog);
                    g = Srgb(lg + (Lin(haze[1]) * day - lg) * fog);
                    b = Srgb(lb + (Lin(haze[2]) * day - lb) * fog);
                }
                int o = (y * W + x) * 3;
                bytes[o] = (byte)(Math.Min(1, Math.Max(0, r)) * 255); bytes[o + 1] = (byte)(Math.Min(1, Math.Max(0, g)) * 255); bytes[o + 2] = (byte)(Math.Min(1, Math.Max(0, b)) * 255);
            }
        });
        if (SS > 1)
        {
            int w = TerrainView.W, h = TerrainView.H;
            var small = new byte[w * h * 3];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    for (int c = 0; c < 3; c++)
                    {
                        int sum = 0;
                        for (int j = 0; j < SS; j++) for (int i = 0; i < SS; i++) sum += bytes[((y * SS + j) * W + x * SS + i) * 3 + c];
                        small[(y * w + x) * 3 + c] = (byte)(sum / (SS * SS));
                    }
            bytes = small; W = w; H = h;
        }
        using (var file = File.Create(path))
        {
            byte[] head = System.Text.Encoding.ASCII.GetBytes($"P6\n{W} {H}\n255\n");
            file.Write(head, 0, head.Length);
            file.Write(bytes, 0, bytes.Length);
        }
        Console.WriteLine($"wrote {path} in {clock.Elapsed.TotalSeconds:F0} s");
    }

    // ------------------------------------------------------------------ light and shadow trials (stage 3)
    // OA_LIGHT=game lights the ground with a copy of the game's own sum (OA_LightIn in OA_Look.hlsl, with the
    // sunlight and fill PlanetSky works out for where the eye is): nothing casts a shadow. OA_LIGHT=new is the
    // trial: cast shadows with a soft edge, plain sunlight with no wrap-round, and sunlight and sky fill worked out
    // for the place being lit, not for where the eye is. OA_PROPS=1 stands a few stand-in shapes in front of the
    // eye (boulders, a figure, a lander) so there is something to cast a shadow. OA_SS=2 smooths the picture.
    static int LightMode, SS = 1;

    struct Prim
    {
        public bool Capsule;
        public Vec3d A, B, E, U, N;
        public double Rx, Ry, Rz;
        public float[] Color;
    }

    static readonly List<Prim> Props = new List<Prim>();

    static void AddProps(PlanetTerrain t, string body, Vec3d up, Vec3d flat, Vec3d side)
    {
        Vec3d At(double forward, double across, double above, out Vec3d u)
        {
            u = (up + (flat * forward + side * across) / t.Radius).Normalized;
            return u * (t.Radius + t.Height(u) + above);
        }
        void Ball(double forward, double across, double above, double rx, double ry, double rz, float[] c, double turn = 0)
        {
            Vec3d a = At(forward, across, above, out Vec3d u);
            Vec3d e = (side * Math.Cos(turn) + flat * Math.Sin(turn)).Normalized;
            e = (e - u * Vec3d.Dot(e, u)).Normalized;
            Props.Add(new Prim { A = a, E = e, U = u, N = Vec3d.Cross(e, u).Normalized, Rx = rx, Ry = ry, Rz = rz, Color = c });
        }
        void Rod(Vec3d a, Vec3d b, double r, float[] c) => Props.Add(new Prim { Capsule = true, A = a, B = b, Rx = r, Color = c });

        float[] rock = body == "Mars" ? new[] { 0.40f, 0.29f, 0.23f } : body == "Moon" ? new[] { 0.36f, 0.36f, 0.37f } : new[] { 0.37f, 0.35f, 0.32f };
        float[] suit = { 0.44f, 0.45f, 0.40f }, hull = { 0.74f, 0.72f, 0.68f }, foil = { 0.62f, 0.50f, 0.27f }, strut = { 0.5f, 0.5f, 0.52f };
        // Boulders, a third sunk in the ground.
        double[,] stones = { { 4.6, 1.9, 0.55, 0.42, 0.6, 0.3 }, { 9, 3.6, 1.3, 0.9, 1.1, 1.1 }, { 12, -4.2, 1.6, 1.15, 1.4, 2.0 }, { 6, -3.1, 0.36, 0.26, 0.4, 0.7 },
            { 15, 0.8, 0.8, 0.55, 0.7, 2.6 }, { 21, -8, 2.3, 1.6, 1.9, 0.4 }, { 3.4, -0.9, 0.2, 0.14, 0.22, 1.9 }, { 5.2, 0.5, 0.15, 0.1, 0.17, 0.2 },
            { 7.6, 1.2, 0.24, 0.17, 0.2, 1.3 }, { 10.5, -1.6, 0.3, 0.2, 0.33, 2.4 }, { 17, 5.5, 1.0, 0.75, 1.2, 0.9 }, { 30, -3, 1.4, 1.0, 1.3, 1.6 } };
        for (int i = 0; i < stones.GetLength(0); i++)
            Ball(stones[i, 0], stones[i, 1], stones[i, 3] * 0.3, stones[i, 2], stones[i, 3], stones[i, 4], rock, stones[i, 5]);
        // A figure the height of a trooper.
        Vec3d feet = At(7.5, -1.1, 0.25, out Vec3d fu);
        Rod(feet, feet + fu * 1.2, 0.25, suit);
        Props.Add(new Prim { A = feet + fu * 1.47, E = side, U = fu, N = flat, Rx = 0.17, Ry = 0.18, Rz = 0.18, Color = suit });
        // The lander: a cabin on a base on four long legs.
        Vec3d foot0 = At(27, 6, 0, out Vec3d lu);
        Vec3d baseAt = foot0 + lu * 5.6;
        Props.Add(new Prim { A = baseAt + lu * 2.1, E = side, U = lu, N = flat, Rx = 2.4, Ry = 1.9, Rz = 2.4, Color = hull });
        Props.Add(new Prim { A = baseAt + lu * 0.3, E = side, U = lu, N = flat, Rx = 2.7, Ry = 0.55, Rz = 2.7, Color = foil });
        for (int i = 0; i < 4; i++)
        {
            double sx = (i & 1) == 0 ? -1 : 1, sz = (i & 2) == 0 ? -1 : 1;
            Vec3d pad = At(27 + sz * 3.5, 6 + sx * 3.5, 0.08, out Vec3d pu);
            Rod(baseAt + side * (sx * 1.8) + flat * (sz * 1.8), pad, 0.1, strut);
            Props.Add(new Prim { A = pad, E = side, U = pu, N = flat, Rx = 0.45, Ry = 0.09, Rz = 0.45, Color = strut });
        }
    }

    static double PrimHit(in Prim q, Vec3d ro, Vec3d rd, out Vec3d normal)
    {
        normal = default;
        if (q.Capsule)
        {
            Vec3d ba = q.B - q.A, oa = ro - q.A;
            double baba = Vec3d.Dot(ba, ba), bard = Vec3d.Dot(ba, rd), baoa = Vec3d.Dot(ba, oa), rdoa = Vec3d.Dot(rd, oa), oaoa = Vec3d.Dot(oa, oa);
            double a = baba - bard * bard, b = baba * rdoa - baoa * bard, c = baba * oaoa - baoa * baoa - q.Rx * q.Rx * baba, h = b * b - a * c;
            if (h < 0) return -1;
            double at = (-b - Math.Sqrt(h)) / a, y = baoa + at * bard;
            if (!(y > 0 && y < baba))
            {
                Vec3d oc = y <= 0 ? oa : ro - q.B;
                b = Vec3d.Dot(rd, oc); c = Vec3d.Dot(oc, oc) - q.Rx * q.Rx; h = b * b - c;
                if (h <= 0) return -1;
                at = -b - Math.Sqrt(h);
            }
            if (at <= 0) return -1;
            Vec3d pa = ro + rd * at - q.A;
            double along = Math.Max(0, Math.Min(1, Vec3d.Dot(pa, ba) / baba));
            normal = (pa - ba * along).Normalized;
            return at;
        }
        Vec3d o = ro - q.A;
        double ox = Vec3d.Dot(o, q.E) / q.Rx, oy = Vec3d.Dot(o, q.U) / q.Ry, oz = Vec3d.Dot(o, q.N) / q.Rz;
        double dx = Vec3d.Dot(rd, q.E) / q.Rx, dy = Vec3d.Dot(rd, q.U) / q.Ry, dz = Vec3d.Dot(rd, q.N) / q.Rz;
        double qa = dx * dx + dy * dy + dz * dz, qb = ox * dx + oy * dy + oz * dz, qc = ox * ox + oy * oy + oz * oz - 1, disc = qb * qb - qa * qc;
        if (disc < 0) return -1;
        double hit = (-qb - Math.Sqrt(disc)) / qa;
        if (hit <= 0) return -1;
        double lx = ox + dx * hit, ly = oy + dy * hit, lz = oz + dz * hit;
        normal = (q.E * (lx / q.Rx) + q.U * (ly / q.Ry) + q.N * (lz / q.Rz)).Normalized;
        return hit;
    }

    static bool PropHit(Vec3d ro, Vec3d rd, double nearest, out double at, out Vec3d normal, out float[] color)
    {
        at = nearest; normal = default; color = null;
        for (int i = 0; i < Props.Count; i++)
        {
            double d = PrimHit(Props[i], ro, rd, out Vec3d n);
            if (d > 0 && d < at) { at = d; normal = n; color = Props[i].Color; }
        }
        return color != null;
    }

    static double LinPeak(double c, double peak) => Math.Pow(Math.Max(0, c / peak), 2.2) * peak;

    static void ToLight(Rgb c, double scale, out double r, out double g, out double b)
    {
        // SkyGlobals.Linear: above 1 the curve is not defined, so convert the colour and scale afterwards.
        double peak = Math.Max(1, Math.Max(c.R * scale, Math.Max(c.G * scale, c.B * scale)));
        r = LinPeak(c.R * scale, peak); g = LinPeak(c.G * scale, peak); b = LinPeak(c.B * scale, peak);
    }

    static double Step01(double from, double to, double x) => Noise3.Smooth(from, to, x);

    /// <summary>The light on a place (to multiply by its colour), by the game's sum today (LightMode 1) or the trial (2).</summary>
    static void LightAt(PlanetTerrain t, bool air, float[] sky, float[] haze, Vec3d p, Vec3d n, Vec3d normal, Vec3d sun, Vec3d eyeUp,
        double top, int px, int py, bool prop, out double r, out double g, out double b)
    {
        var sunBase = new Rgb(1f, 0.96f, 0.88f);
        var spaceAmbient = new Rgb(0.055f, 0.062f, 0.105f);
        double here = Vec3d.Dot(n, sun);
        // The game works sunlight and fill out once, for where the eye is. The trial works them out for the place lit.
        double sunHeight = LightMode == 1 ? Vec3d.Dot(eyeUp, sun) : here;
        double day = Step01(-0.1, 0.25, sunHeight);
        Rgb sunColor = sunBase, ambient = spaceAmbient;
        if (air)
        {
            float light = (float)(0.035 + 0.965 * day);
            var skyRgb = new Rgb(sky[0], sky[1], sky[2]);
            SkyPalette pal = SkyLook.InAir(skyRgb, new Rgb(haze[0], haze[1], haze[2]), (float)sunHeight, light, 1f, false, 1f, 1f);
            sunColor = sunBase * pal.SunTint;
            ambient = (skyRgb * (light * 0.55f)).Saturated(1.25f) + spaceAmbient * 0.5f + pal.Sunset * 0.3f;
        }
        ToLight(sunColor, 1.35, out double sr, out double sg, out double sb);
        ToLight(ambient, 1, out double ar, out double ag, out double ab);
        double facing = Vec3d.Dot(normal, sun), upward = Vec3d.Dot(normal, n);
        double dayHere = Math.Max(0, Math.Min(1, here * 2 + 0.2));
        double bounce = dayHere * Math.Max(0, Math.Min(1, 0.6 - 0.6 * upward)) * 0.14;
        if (LightMode == 1)
        {
            double wrap = Math.Max(0, Math.Min(1, (facing + 0.2) / 1.2));
            double lit = wrap + (Step01(0.05, 0.6, wrap) - wrap) * 0.6;
            double edge = Math.Max(0, Math.Min(1, 1 - Math.Abs(wrap - 0.15) * 6)) * 0.2;
            double reached = Step01(-0.14, 0.02, here);
            double fill = 0.85 + 0.3 * upward;
            r = ar * fill + sr * (lit + edge + bounce) * reached;
            g = ag * fill + sg * (lit + edge * 0.36 + bounce * 0.8) * reached;
            b = ab * fill + sb * (lit + edge * 0.14 + bounce * 0.6) * reached;
            return;
        }

        // The trial. Four looks toward slightly different parts of the Sun give the shadow a soft edge.
        double open = 0;
        if (facing > -0.04 && here > -0.2)
        {
            Vec3d su = sun.AnyPerpendicular(), sv = Vec3d.Cross(sun, su);
            for (int i = 0; i < 4; i++)
            {
                double turn = (ShaderHash(px * 0.37 + i * 7.1, py * 0.53, i * 3.3) + i * 0.25) * Math.PI * 2;
                double far = 0.011 * Math.Sqrt((i + 0.5) / 4);
                Vec3d to = (sun + su * (Math.Cos(turn) * far) + sv * (Math.Sin(turn) * far)).Normalized;
                Vec3d from = p + normal * 0.03 + n * 0.02;
                if (PropHit(from, to, 1e9, out _, out _, out _)) continue;
                if (Shadowed(t, p + n * 0.6, to, top)) continue;
                open += 0.25;
            }
        }
        if (!air)
        {
            // With no air the only fill is sunlight thrown back by the ground round about.
            ar = ar * 0.6 + sr * 0.03 * dayHere; ag = ag * 0.6 + sg * 0.03 * dayHere; ab = ab * 0.6 + sb * 0.03 * dayHere;
        }
        double soft = air ? 0.1 : 0.012;
        double up2 = Step01(-soft, soft * 0.5, here);
        double direct = Math.Max(0, Math.Min(1, (facing + 0.04) / 1.04)) * open;
        double f2 = 0.85 + 0.3 * upward;
        r = ar * f2 + sr * (direct + bounce) * up2;
        g = ag * f2 + sg * (direct + bounce * 0.8) * up2;
        b = ab * f2 + sb * (direct + bounce * 0.6) * up2;
    }

    /// <summary>How strongly the ground shader's fine relief is drawn (the Ground Relief dial). 0 shows the bare mesh colours.</summary>
    static double Relief = 1;

    static double Frac(double x) => x - Math.Floor(x);

    static double ShaderHash(double x, double y, double z)
    {
        x = Frac(x * 0.3183099 + 0.1) * 17; y = Frac(y * 0.3183099 + 0.1) * 17; z = Frac(z * 0.3183099 + 0.1) * 17;
        return Frac(x * y * z * (x + y + z));
    }

    static double ShaderNoise(double x, double y, double z)
    {
        double ix = Math.Floor(x), iy = Math.Floor(y), iz = Math.Floor(z);
        double fx = x - ix, fy = y - iy, fz = z - iz;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
        double a = Noise3.Lerp(ShaderHash(ix, iy, iz), ShaderHash(ix + 1, iy, iz), fx);
        double b = Noise3.Lerp(ShaderHash(ix, iy + 1, iz), ShaderHash(ix + 1, iy + 1, iz), fx);
        double c = Noise3.Lerp(ShaderHash(ix, iy, iz + 1), ShaderHash(ix + 1, iy, iz + 1), fx);
        double d = Noise3.Lerp(ShaderHash(ix, iy + 1, iz + 1), ShaderHash(ix + 1, iy + 1, iz + 1), fx);
        return Noise3.Lerp(Noise3.Lerp(a, b, fy), Noise3.Lerp(c, d, fy), fz);
    }

    static double Ground(Vec3d place, double range, out double broad, out double mid, out double fine, out double stone, out double nearMid, out double nearFine)
    {
        nearMid = Noise3.Clamp01(1 - range / 150);
        nearFine = Noise3.Clamp01(1 - range / 42);
        nearFine *= nearFine;
        broad = ShaderNoise(place.X * 0.043, place.Y * 0.043, place.Z * 0.043);
        mid = ShaderNoise(place.X * 0.62 + 7, place.Y * 0.62 + 7, place.Z * 0.62 + 7);
        double tx = place.X * 0.36 + place.Y * 0.48 - place.Z * 0.8, ty = -place.X * 0.8 + place.Y * 0.6, tz = place.X * 0.48 + place.Y * 0.64 + place.Z * 0.6;
        fine = ShaderNoise(tx * 3.4 + 23, ty * 3.4 + 23, tz * 3.4 + 23);
        stone = Noise3.Clamp01(fine * 1.6 - 0.55);
        stone *= stone;
        return ((mid - 0.5) * 0.16 * nearMid + stone * 0.05 * nearFine) * Relief;
    }

    static double Lin(double c) => Math.Pow(c, 2.2);
    // A soft shoulder so bright ground does not clip, then back to screen values.
    static double Srgb(double c) { c = c * 1.25; c = c / (1 + c * 0.35); return Math.Pow(Math.Max(0, c), 1 / 2.2); }

    static bool March(PlanetTerrain t, Vec3d eye, Vec3d dir, double top, out double dist, out bool water)
    {
        dist = 0; water = false;
        double sea = t.HasSea ? t.SeaLevel : -1e9;
        double travelled = 0;
        // Skip straight to the shell that holds all the ground, if starting outside it.
        double b = Vec3d.Dot(eye, dir), c = eye.SqrMagnitude - top * top;
        if (c > 0)
        {
            double disc = b * b - c;
            if (disc < 0 || b > 0) return false;
            travelled = -b - Math.Sqrt(disc);
        }
        double previous = travelled;
        for (int i = 0; i < 900; i++)
        {
            Vec3d p = eye + dir * travelled;
            double r = p.Magnitude;
            if (r > top + 1 && Vec3d.Dot(p, dir) > 0) return false;
            Vec3d n = p / r;
            double minW = Math.Max(2.7, travelled * 0.012);
            double ground = Math.Max(t.Height(n, minW), sea);
            double above = r - t.Radius - ground;
            if (above < 0.02)
            {
                double lo = previous, hi = travelled;
                for (int k = 0; k < 10; k++)
                {
                    double mid = (lo + hi) * 0.5;
                    Vec3d q = eye + dir * mid;
                    double qr = q.Magnitude;
                    Vec3d qn = q / qr;
                    if (qr - t.Radius - Math.Max(t.Height(qn, minW), sea) < 0) hi = mid; else lo = mid;
                }
                dist = hi;
                Vec3d e = (eye + dir * hi).Normalized;
                water = t.HasSea && t.Height(e, minW) < sea;
                return true;
            }
            previous = travelled;
            travelled += Math.Max(0.4, above * 0.35) + travelled * 0.002;
            if (travelled > 40000) return false;
        }
        return false;
    }

    static bool Shadowed(PlanetTerrain t, Vec3d from, Vec3d sun, double top)
    {
        double travelled = 1.5;
        for (int i = 0; i < 48; i++)
        {
            Vec3d p = from + sun * travelled;
            double r = p.Magnitude;
            if (r > top) return false;
            double above = r - t.Radius - t.Height(p / r, Math.Max(4, travelled * 0.1));
            if (above < 0) return true;
            travelled += Math.Max(2, above * 0.6);
        }
        return false;
    }
}
