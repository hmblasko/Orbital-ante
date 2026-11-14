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
using OrbitalAnte.Core;
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
                    line.Append($" wanted {OrbitalAnte.Bases.BaseField.BaseCount(rules, first.Radius),2}, found");
                    for (int seed = 1; seed <= 8; seed++)
                    {
                        PlanetTerrain t = seed == 1 ? first : Make(profile.bodyId, seed);
                        line.Append($" {OrbitalAnte.Bases.BaseField.Build(rules, t, new SeededRandom(seed * 31), new[] { 2, 2, 1 }, Vec3d.Zero).Count,2}");
                    }
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

                if (March(t, eye, dir, top, out double dist, out bool water))
                {
                    Vec3d p = eye + dir * dist;
                    Vec3d n = p.Normalized;
                    double minW = Math.Max(2.7, dist * 0.012);
                    double step = Math.Max(1.7, dist * 0.006);
                    Vec3d normal;
                    float cr, cg, cb;
                    if (water)
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
                    if (!water && Relief > 0)
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
                    double lit = Math.Max(0, Vec3d.Dot(normal, sun));
                    if (lit > 0 && Shadowed(t, p + n * 0.6, sun, top)) lit *= 0.12;
                    double day = Noise3.Smooth(-0.1, 0.25, Vec3d.Dot(n, sun));
                    double skyLight = (air ? 0.34 : 0.06) * (0.6 + 0.4 * Vec3d.Dot(normal, n)) * day;
                    double lr = Lin(cr) * (lit * 1.0 * 1.02 + skyLight * (air ? Lin(sky[0]) * 1.2 + 0.25 : 1));
                    double lg = Lin(cg) * (lit * 0.97 + skyLight * (air ? Lin(sky[1]) * 1.2 + 0.25 : 1));
                    double lb = Lin(cb) * (lit * 0.90 + skyLight * (air ? Lin(sky[2]) * 1.2 + 0.25 : 1));
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
        using (var file = File.Create(path))
        {
            byte[] head = System.Text.Encoding.ASCII.GetBytes($"P6\n{W} {H}\n255\n");
            file.Write(head, 0, head.Length);
            file.Write(bytes, 0, bytes.Length);
        }
        Console.WriteLine($"wrote {path} in {clock.Elapsed.TotalSeconds:F0} s");
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
