using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// The ground of one body as pure rules: how high it is and what colour it is in any direction from the
    /// centre, plus the lists of things placed on it (rocks, ponds, plumes, caves). No Unity types, so it is unit
    /// tested outside the engine, and nothing changes after it is built, so several threads can ask it questions
    /// at once.
    ///
    /// The broad shape of each body depends only on its name, so Mars is always Mars. Landmark formations are
    /// also fixed. Everything else is placed by the run seed.
    /// </summary>
    public sealed class PlanetTerrain
    {
        const int Grid = 20;

        public PlanetProfile Profile { get; }
        public string BodyId { get; }
        /// <summary>Radius of the ground where height is zero, in metres.</summary>
        public double Radius { get; }
        public int Seed { get; }
        /// <summary>How much formations are shrunk on this body compared with an Earth-sized one.</summary>
        public double Scale { get; }
        /// <summary>No ground is lower or higher than these, in metres. Generous estimates, not exact.</summary>
        public double MinHeight { get; private set; }
        public double MaxHeight { get; private set; }
        public bool HasSea => Profile.hasSea;
        public double SeaLevel => Profile.seaLevel;

        public List<Stamp> Stamps { get; } = new List<Stamp>();
        public List<Placement> Placements { get; } = new List<Placement>();
        public List<Pond> Ponds { get; } = new List<Pond>();
        public List<PlumeSite> Plumes { get; } = new List<PlumeSite>();
        public List<Cave> Caves { get; } = new List<Cave>();
        public List<TerrainHole> Holes { get; } = new List<TerrainHole>();

        readonly uint _s;
        int[][] _cells;
        readonly float[] _low, _mid, _high, _cliff, _peak, _shore, _pole, _seaDeep;
        readonly bool _hasShore, _hasPole, _iceShelf;
        readonly float[][] _zoneColors;

        public PlanetTerrain(PlanetProfile profile, string bodyId, double bodyRadius, int seed)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            BodyId = bodyId ?? profile.bodyId;
            Radius = profile.surfaceRadius > 0 ? profile.surfaceRadius : bodyRadius;
            Seed = seed;
            Scale = Math.Max(0.3, Math.Min(1.15, Math.Sqrt(Radius / 500.0)));
            _s = Noise3.SeedFrom(BodyId);

            _low = ColorUtil.Parse(profile.lowColor);
            _mid = ColorUtil.Parse(profile.midColor);
            _high = ColorUtil.Parse(profile.highColor);
            _cliff = ColorUtil.Parse(profile.cliffColor);
            _peak = ColorUtil.Parse(profile.peakColor);
            _hasShore = !string.IsNullOrEmpty(profile.shoreColor);
            _shore = ColorUtil.Parse(_hasShore ? profile.shoreColor : profile.lowColor);
            _hasPole = !string.IsNullOrEmpty(profile.poleColor);
            _pole = ColorUtil.Parse(_hasPole ? profile.poleColor : "#FFFFFF");
            _seaDeep = ColorUtil.Parse(profile.seaDeepColor);
            _iceShelf = profile.hasSea && _hasPole && profile.seaLiquid == Liquid.Water;
            _zoneColors = new float[profile.zones.Count][];
            for (int i = 0; i < _zoneColors.Length; i++) _zoneColors[i] = ColorUtil.Parse(profile.zones[i].color);

            _cells = new int[CubeSphere.Faces * Grid * Grid][];
            FormationGenerator.PlaceStamps(this);
            BuildCells();
            EstimateBounds();
            FormationGenerator.PlaceTheRest(this);
        }

        // ------------------------------------------------------------------ height

        /// <summary>
        /// Height of the ground in a direction, in metres above <see cref="Radius"/>. Bumps smaller than
        /// <paramref name="minWavelength"/> are left out, for cheap far-away ground.
        /// </summary>
        public double Height(Vec3d direction, double minWavelength = 0) => Sample(direction, minWavelength).Height;

        public TerrainPoint Sample(Vec3d direction, double minWavelength = 0)
        {
            Vec3d p = direction * Radius;
            var point = new TerrainPoint();
            ApplyStamps(direction, p, minWavelength, ref point);
            point.Height += Base(direction, p, minWavelength, point.Flatten) + point.Cut;
            return point;
        }

        /// <summary>The broad lie of the land with no formations and no small hills. Used to decide what goes where.</summary>
        public double CoarseHeight(Vec3d direction)
        {
            return Base(direction, direction * Radius, Profile.hillWavelength * 0.9, 1.0 / 0.85);
        }

        public Vec3d SurfacePoint(Vec3d direction) => direction * (Radius + Height(direction));

        double Base(Vec3d direction, Vec3d p, double minW, double flatten)
        {
            PlanetProfile f = Profile;
            double cw = f.continentWavelength;
            double ww = cw * 0.7;
            // Pushing the sample point about bends coastlines and ridges so they do not follow the noise's grid.
            var q = new Vec3d(
                p.X + Noise3.Perlin(p, ww, _s + 11) * cw * 0.18,
                p.Y + Noise3.Perlin(p, ww, _s + 12) * cw * 0.18,
                p.Z + Noise3.Perlin(p, ww, _s + 13) * cw * 0.18);

            double height = Noise3.Fbm(q, cw, 4, _s + 1, minW) * f.continentHeight * 1.6 + f.continentBias;

            double keep = 1.0 - 0.85 * flatten;
            if (keep > 0.001)
            {
                double relief = 0;
                // Some regions are rougher than others, but nowhere is dead flat.
                double rough = 0.45 + 0.55 * Noise3.Clamp01(0.5 + Noise3.Fbm(p, f.hillWavelength * 6, 2, _s + 2) * 1.2);
                relief += Noise3.Fbm(p, f.hillWavelength, 4, _s + 3, minW) * f.hillHeight * 1.6 * rough;
                if (f.mountainHeight > 0 && f.mountainCoverage > 0)
                {
                    double start = 0.45 - f.mountainCoverage * 0.9;
                    double mask = Noise3.Smooth(start, start + 0.25, Noise3.Fbm(q, f.mountainWavelength * 3.5, 2, _s + 4));
                    if (mask > 0) relief += Noise3.Ridged(q, f.mountainWavelength, 5, _s + 5, minW) * f.mountainHeight * 1.5 * mask;
                }
                height += relief * keep;
            }

            if (f.detailHeight > 0) height += Noise3.Fbm(p, f.detailWavelength, 2, _s + 6, minW) * f.detailHeight * 1.6;

            if (_iceShelf)
            {
                // Polar seas freeze over: a shelf of ice a little above the water, so the cap is one white sheet.
                double cap = Noise3.Smooth(f.poleLatitude, f.poleLatitude + 0.04, Math.Abs(direction.Y));
                if (cap > 0)
                {
                    double shelf = f.seaLevel + 1.4 + 0.5 * Noise3.Perlin(p, 30, _s + 8);
                    if (height < shelf) height += (shelf - height) * cap;
                }
            }
            return height;
        }

        /// <summary>How steep the ground is: 0 flat, 1 a vertical wall.</summary>
        public double SlopeAt(Vec3d direction, double step = 2.0)
        {
            Vec3d u = direction.AnyPerpendicular();
            Vec3d v = Vec3d.Cross(direction, u);
            double angle = step / Radius;
            double h0 = Height(direction);
            double hu = Height((direction + u * angle).Normalized);
            double hv = Height((direction + v * angle).Normalized);
            double gu = (hu - h0) / step, gv = (hv - h0) / step;
            double g2 = gu * gu + gv * gv;
            return Math.Sqrt(g2 / (1.0 + g2));
        }

        // ------------------------------------------------------------------ stamps

        static int CellIndex(Vec3d direction)
        {
            CubeSphere.Locate(direction, out int face, out double u, out double v);
            int cx = (int)((u + 1.0) * 0.5 * Grid);
            int cy = (int)((v + 1.0) * 0.5 * Grid);
            if (cx < 0) cx = 0; else if (cx >= Grid) cx = Grid - 1;
            if (cy < 0) cy = 0; else if (cy >= Grid) cy = Grid - 1;
            return (face * Grid + cy) * Grid + cx;
        }

        void BuildCells()
        {
            // Straight-line reach of one cell from its middle, on a ball of radius 1. Deliberately generous.
            const double cellReach = 1.6 / Grid;
            var lists = new List<int>[_cells.Length];
            var middles = new Vec3d[_cells.Length];
            for (int face = 0; face < CubeSphere.Faces; face++)
            {
                for (int cy = 0; cy < Grid; cy++)
                {
                    for (int cx = 0; cx < Grid; cx++)
                    {
                        middles[(face * Grid + cy) * Grid + cx] =
                            CubeSphere.Direction(face, (cx + 0.5) / Grid * 2 - 1, (cy + 0.5) / Grid * 2 - 1);
                    }
                }
            }
            for (int i = 0; i < Stamps.Count; i++)
            {
                Stamp s = Stamps[i];
                double reach = s.BoundRadius / Radius + cellReach;
                double reach2 = reach * reach;
                for (int c = 0; c < middles.Length; c++)
                {
                    if ((middles[c] - s.Center).SqrMagnitude > reach2) continue;
                    if (lists[c] == null) lists[c] = new List<int>();
                    lists[c].Add(i);
                }
            }
            for (int c = 0; c < lists.Length; c++) _cells[c] = lists[c] != null ? lists[c].ToArray() : null;
        }

        void ApplyStamps(Vec3d direction, Vec3d p, double minW, ref TerrainPoint point)
        {
            int[] list = _cells[CellIndex(direction)];
            if (list == null) return;
            for (int i = 0; i < list.Length; i++)
            {
                Stamp s = Stamps[list[i]];
                Vec3d d = direction - s.Center;
                double bound = s.BoundRadius / Radius;
                if (d.SqrMagnitude > bound * bound) continue;
                switch (s.Kind)
                {
                    case StampKind.Crater: Crater(s, d, p, ref point); break;
                    case StampKind.Cone: Cone(s, d, p, minW, ref point); break;
                    case StampKind.Dome: Dome(s, d, p, minW, ref point); break;
                    case StampKind.Plateau: Plateau(s, d, p, ref point); break;
                    case StampKind.Curve: Curve(s, direction, p, minW, ref point); break;
                    default: Region(s, d, p, minW, ref point); break;
                }
            }
        }

        static void Tint(ref TerrainPoint point, Stamp s, double amount)
        {
            if (amount <= 0.001 || s.TintStrength <= 0) return;
            float a = (float)Math.Min(1.0, amount * s.TintStrength);
            float total = 1f - (1f - point.TintAmount) * (1f - a);
            float keep = point.TintAmount * (1f - a);
            point.TintR = (point.TintR * keep + s.TintR * a) / total;
            point.TintG = (point.TintG * keep + s.TintG * a) / total;
            point.TintB = (point.TintB * keep + s.TintB * a) / total;
            point.TintAmount = total;
        }

        void Crater(Stamp s, Vec3d d, Vec3d p, ref TerrainPoint point)
        {
            Formation f = s.Formation;
            double dx = Vec3d.Dot(d, s.U) * Radius, dy = Vec3d.Dot(d, s.V) * Radius;
            if (f.Elongation != 1) dx /= f.Elongation;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist >= s.Extent) return;
            double rc = s.Size;
            // A slight wobble, so no crater is a perfect circle.
            double dn = dist / rc * (1.0 + 0.07 * Noise3.Perlin(p, rc * 0.9, s.Seed));
            double depth = s.Height, rim = f.Rim * rc;
            double h;
            if (dn < 1)
            {
                double t = dn <= f.Floor ? 0 : (dn - f.Floor) / (1 - f.Floor);
                double shape = f.Floor > 0 ? Math.Pow(t, 1.6) : t * t;
                h = -depth + (depth + rim) * shape;
                if (f.Peak > 0)
                {
                    if (f.Ring > 0)
                    {
                        double q = (dn - f.Ring) / 0.075;
                        h += f.Peak * depth * Math.Exp(-q * q);
                    }
                    else
                    {
                        double q = dn / 0.2;
                        if (q < 1) h += f.Peak * depth * (1 - q * q) * (1 - q * q);
                    }
                }
                // Stepped walls on the big flat-floored ones.
                if (f.Floor > 0 && t > 0) h += Math.Sin(t * Math.PI * 3) * depth * 0.035 * (1 - t);
                double d4 = dn * dn * dn * dn;
                point.Flatten = Math.Max(point.Flatten, Math.Max(0.9, f.Flatten) * (1 - d4));
            }
            else
            {
                double fade = 1 - Noise3.Smooth(0.8, 1, dist / s.Extent);
                h = rim * Math.Pow(dn, -3.2) * fade;
                if (f.Rampart > 0)
                {
                    double edge = 1.7 + 0.28 * Noise3.Perlin(p, rc * 0.6, s.Seed + 5);
                    h += f.Rampart * depth * (1 - Noise3.Smooth(edge - 0.1, edge, dn)) * fade;
                }
                for (int k = 1; k <= f.OuterRings; k++)
                {
                    double q = (dn - (1 + 0.45 * k)) / 0.07;
                    if (q > -3 && q < 3) h += rim * 0.75 / k * Math.Exp(-q * q) * fade;
                }
                if (f.Flatten > 0) point.Flatten = Math.Max(point.Flatten, f.Flatten * fade * 0.6);
            }
            if (f.Pedestal > 0) h += f.Pedestal * depth * (1 - Noise3.Smooth(1.45, 1.8, dn));
            point.Height += h;

            if (s.TintStrength <= 0) return;
            if (f.Rays > 0 && dn > 1.15)
            {
                double angle = Math.Atan2(dy, dx);
                double streak = 0.5 + 0.5 * Math.Cos(angle * f.Rays + 2.5 * Noise3.Perlin(angle * 1.7, s.Seed * 1e-6, 0.5, s.Seed));
                streak = streak * streak * streak;
                streak *= streak;
                double reach = 1 - dist / s.Extent;
                Tint(ref point, s, streak * reach * 1.4);
            }
            else
            {
                Tint(ref point, s, 1 - Noise3.Smooth(0.8, 1.5, dn));
            }
        }

        void Cone(Stamp s, Vec3d d, Vec3d p, double minW, ref TerrainPoint point)
        {
            Formation f = s.Formation;
            double dx = Vec3d.Dot(d, s.U) * Radius, dy = Vec3d.Dot(d, s.V) * Radius;
            double dn = Math.Sqrt(dx * dx + dy * dy) / s.Size * (1.0 + 0.06 * Noise3.Perlin(p, s.Size * 0.8, s.Seed));
            if (dn >= 1) return;
            double h = s.Height * Math.Pow(1 - dn, f.Power);
            bool inPit = f.Summit > 0 && dn < f.Summit;
            if (inPit)
            {
                double top = s.Height * Math.Pow(1 - f.Summit, f.Power);
                double q = dn / f.Summit;
                h = top - f.SummitDepth * s.Height * (1 - q * q);
            }
            else if (f.Rugged > 0)
            {
                double ridged = Noise3.Ridged(p, s.Size * 0.5, 3, s.Seed + 3, minW);
                h *= Noise3.Lerp(1, 0.55 + 0.9 * ridged, f.Rugged * Noise3.Smooth(0, 0.3, 1 - dn));
            }
            point.Height += h;
            point.Flatten = Math.Max(point.Flatten, Math.Max(f.Flatten, inPit ? 0.9 : 0) * Math.Min(1, (1 - dn) * 3));
            Tint(ref point, s, Math.Sqrt(1 - dn));
        }

        void Dome(Stamp s, Vec3d d, Vec3d p, double minW, ref TerrainPoint point)
        {
            Formation f = s.Formation;
            double dn = d.Magnitude * Radius / s.Size;
            if (f.EdgeNoise > 0) dn *= 1.0 + f.EdgeNoise * 0.6 * Noise3.Fbm(p, s.Size * 0.7, 2, s.Seed + 1);
            if (dn >= 1) return;
            double w = (1 - dn * dn) * (1 - dn * dn);
            double h = s.Height * w;
            if (f.Rugged > 0) h *= Noise3.Lerp(1, 0.5 + Noise3.Ridged(p, s.Size * 0.45, 4, s.Seed + 3, minW), f.Rugged);
            point.Height += h;
            point.Flatten = Math.Max(point.Flatten, f.Flatten * Math.Sqrt(w));
            Tint(ref point, s, Noise3.Smooth(0, 0.35, w));
        }

        void Plateau(Stamp s, Vec3d d, Vec3d p, ref TerrainPoint point)
        {
            Formation f = s.Formation;
            double dn = d.Magnitude * Radius / s.Size;
            if (f.EdgeNoise > 0) dn *= 1.0 + f.EdgeNoise * 0.6 * Noise3.Fbm(p, s.Size * 0.8, 3, s.Seed + 1);
            if (dn >= 1) return;
            double w = 1 - Noise3.Smooth(1 - 2 * f.Steepness, 1, dn);
            point.Height += s.Height * w;
            point.Flatten = Math.Max(point.Flatten, f.Flatten * w);
            Tint(ref point, s, w);
        }

        /// <summary>Where the wandering centre line of a curve is at a point along it, as a sideways offset in metres.</summary>
        static double MeanderAt(Stamp s, double along)
        {
            double offset = 0;
            // A slow bend over the whole length, so nothing natural is ruler-straight.
            if (s.Bow > 0) offset += s.Bow * Noise3.Perlin(along * 2.3, 0.71, s.Seed * 1e-6, s.Seed + 8) * 1.8;
            if (s.MeanderAmount > 0)
                offset += s.MeanderAmount * Noise3.Perlin(along * s.Length / (s.Size * 12.0), s.Seed * 1e-6, 0.37, s.Seed + 9) * 1.6;
            return offset;
        }

        /// <summary>A point on a curve's centre line. <paramref name="along"/> runs from 0 at the start to 1 at the end.</summary>
        public static Vec3d CurvePoint(Stamp s, double along, double radius)
        {
            double angle = along * s.ArcAngle;
            Vec3d on = s.Start * Math.Cos(angle) + s.Along * Math.Sin(angle);
            return (on + s.Normal * (MeanderAt(s, along) / radius)).Normalized;
        }

        void Curve(Stamp s, Vec3d direction, Vec3d p, double minW, ref TerrainPoint point)
        {
            Formation f = s.Formation;
            double side = Vec3d.Dot(direction, s.Normal);
            double cross = Math.Asin(side < -1 ? -1 : side > 1 ? 1 : side) * Radius;
            if (Math.Abs(cross) > s.Extent + s.MeanderAmount * 1.7 + s.Bow * 1.9) return;
            double along = Math.Atan2(Vec3d.Dot(direction, s.Along), Vec3d.Dot(direction, s.Start)) / s.ArcAngle;
            if (along <= 0 || along >= 1) return;
            cross -= MeanderAt(s, along);
            // Narrow and shallow at both ends, so it starts and stops gently.
            double taper = Noise3.Smooth(0, 0.1, along) * (1 - Noise3.Smooth(0.9, 1, along));
            // The width swells and pinches along the way.
            double half = s.Size * (0.55 + 0.45 * taper) * (1.0 + 0.28 * Noise3.Perlin(along * s.Length / (s.Size * 7.0), 0.13, s.Seed * 1e-6, s.Seed + 10));
            double sx = cross / half;
            double x = Math.Abs(sx);
            double w;
            switch (f.Profile)
            {
                case CurveProfile.V:
                    if (x >= 1) return;
                    w = 1 - x;
                    break;
                case CurveProfile.U:
                    if (x >= 1) return;
                    w = 1 - Noise3.Smooth(Math.Max(0.25, f.Floor), 1, x);
                    break;
                case CurveProfile.Ridge:
                    if (x >= 1.6) return;
                    w = 1 - x * x / 2.56;
                    w *= w;
                    if (f.Rugged > 0)
                    {
                        w *= Noise3.Lerp(1, 0.4 + 1.1 * Noise3.Ridged(p, half * 2.4, 4, s.Seed + 3, minW), f.Rugged);
                        // Peaks and passes along the chain.
                        w *= 0.7 + 0.5 * Noise3.Perlin(along * s.Length / (s.Size * 3.0), 0.9, s.Seed * 1e-6, s.Seed + 11);
                    }
                    break;
                case CurveProfile.DoubleRidge:
                {
                    if (x >= 1.5) return;
                    double a = (x - 0.62) / 0.26, b = x / 0.22;
                    w = Math.Exp(-a * a) - 0.45 * Math.Exp(-b * b);
                    break;
                }
                case CurveProfile.Scarp:
                    if (sx <= -1 || sx >= 8) return;
                    w = Noise3.Smooth(-1, 1, sx) * (1 - Noise3.Smooth(2, 8, sx));
                    break;
                default:
                    if (x >= 1) return;
                    w = 1 - Noise3.Smooth(0.75, 1, x);
                    break;
            }
            double h = s.Height * w * taper;
            if (h < 0) point.Cut = Math.Min(point.Cut, h);
            else point.Height += h;
            double flat = s.Height < 0 ? Math.Max(f.Flatten, 0.6) : f.Flatten;
            point.Flatten = Math.Max(point.Flatten, flat * Math.Min(1, Math.Abs(w)) * taper);
            Tint(ref point, s, Math.Min(1, Math.Abs(w)) * taper);
        }

        void Region(Stamp s, Vec3d d, Vec3d p, double minW, ref TerrainPoint point)
        {
            Formation f = s.Formation;
            double dn = d.Magnitude * Radius / s.Size;
            if (f.EdgeNoise > 0) dn *= 1.0 + f.EdgeNoise * 0.6 * Noise3.Fbm(p, s.Size * 0.6, 3, s.Seed + 1);
            if (dn >= 1) return;
            double w = 1 - Noise3.Smooth(0.6, 1, dn);
            double scale = f.TextureScale * Math.Max(0.6, Scale);
            double tex = 0;
            double tint = 1;
            bool fine = scale < minW * 0.5;
            if (!fine || f.Texture == RegionTexture.Streaks)
            {
                switch (f.Texture)
                {
                    case RegionTexture.Dunes:
                    {
                        // A gentle slope up the windward side, a steep drop down the lee side.
                        double phase = Vec3d.Dot(p, s.U) / scale + 1.2 * Noise3.Perlin(p, scale * 3, s.Seed + 2);
                        double t = phase - Math.Floor(phase);
                        double v = t < 0.72 ? t / 0.72 : (1 - t) / 0.28;
                        v = v * v * (3 - 2 * v);
                        tex = v * (0.55 + 0.45 * Noise3.Perlin(p, scale * 2.5, s.Seed + 7));
                        break;
                    }
                    case RegionTexture.StarDunes:
                    {
                        double near = Noise3.Cells(p, scale, s.Seed + 2, out double edge, out _);
                        double v = Math.Max(0, 1 - near * 1.5);
                        tex = Math.Pow(v, 1.4) + 0.25 * Noise3.Smooth(0.25, 0, edge) * (1 - v);
                        break;
                    }
                    case RegionTexture.Yardangs:
                    {
                        // Squeeze the pattern along the wind so the ridges come out long and thin.
                        Vec3d q = p - s.U * (Vec3d.Dot(p, s.U) * 0.78);
                        tex = Noise3.Ridged(q, scale, 2, s.Seed + 2, minW);
                        break;
                    }
                    case RegionTexture.Badlands:
                        tex = Noise3.Ridged(p, scale, 3, s.Seed + 2, minW) - 0.3;
                        break;
                    case RegionTexture.Chaos:
                    {
                        Noise3.Cells(p, scale, s.Seed + 2, out double edge, out uint id);
                        double block = (id & 255u) / 255.0 - 0.45;
                        double solid = Noise3.Smooth(0, 0.14, edge);
                        tex = block * solid * 1.6 - (1 - solid) * 0.7;
                        break;
                    }
                    case RegionTexture.Pits:
                    {
                        double near = Noise3.Cells(p, scale, s.Seed + 2, out _, out _);
                        double bowl = Math.Max(0, 1 - near * 2.3);
                        double q = (near - 0.46) / 0.09;
                        tex = -bowl * bowl + 0.22 * Math.Exp(-q * q);
                        break;
                    }
                    case RegionTexture.Polygons:
                    {
                        double near = Noise3.Cells(p, scale, s.Seed + 2, out double edge, out _);
                        tex = -(1 - Noise3.Smooth(0, 0.1, edge)) + 0.3 * (1 - near);
                        break;
                    }
                    case RegionTexture.Cracks:
                    {
                        Noise3.Cells(p, scale, s.Seed + 2, out double edge, out _);
                        tex = -(1 - Noise3.Smooth(0, 0.07, edge));
                        break;
                    }
                    case RegionTexture.Spikes:
                        tex = Math.Pow(Math.Abs(Noise3.Perlin(p, scale, s.Seed + 2)), 0.6);
                        break;
                    case RegionTexture.Hummocks:
                    {
                        double near = Noise3.Cells(p, scale, s.Seed + 2, out _, out _);
                        double v = Math.Max(0, 1 - near * 1.4);
                        tex = v * v;
                        break;
                    }
                    case RegionTexture.Grooves:
                    {
                        double phase = Vec3d.Dot(p, s.U) / scale + 0.5 * Noise3.Perlin(p, scale * 4, s.Seed + 2);
                        tex = 0.5 + 0.5 * Math.Cos(phase * Math.PI * 2);
                        break;
                    }
                    case RegionTexture.Crosshatch:
                    {
                        double warp = 0.4 * Noise3.Perlin(p, scale * 4, s.Seed + 2);
                        double a = 0.5 + 0.5 * Math.Cos((Vec3d.Dot(p, s.U) / scale + warp) * Math.PI * 2);
                        double b = 0.5 + 0.5 * Math.Cos((Vec3d.Dot(p, s.V) / scale - warp) * Math.PI * 2);
                        tex = Math.Max(a * a * a, b * b * b);
                        break;
                    }
                    case RegionTexture.Streaks:
                    {
                        double phase = Vec3d.Dot(p, s.V) / scale + 1.5 * Noise3.Perlin(p, scale * 3, s.Seed + 2);
                        double v = 0.5 + 0.5 * Math.Cos(phase * Math.PI * 2);
                        tint = v * v * v * v;
                        break;
                    }
                }
            }
            point.Height += (s.Height + tex * f.TextureHeight * Math.Max(0.6, Scale)) * w;
            point.Flatten = Math.Max(point.Flatten, f.Flatten * w);
            Tint(ref point, s, w * tint);
        }

        void EstimateBounds()
        {
            PlanetProfile f = Profile;
            double swing = f.continentHeight * 1.6 + f.hillHeight * 1.6 + f.detailHeight * 1.6;
            double up = 0, down = 0;
            for (int i = 0; i < Stamps.Count; i++)
            {
                Stamp s = Stamps[i];
                Formation g = s.Formation;
                double tex = g.TextureHeight * 1.6;
                switch (s.Kind)
                {
                    case StampKind.Crater:
                        up = Math.Max(up, g.Rim * s.Size + g.Pedestal * s.Height);
                        down = Math.Max(down, s.Height);
                        break;
                    case StampKind.Curve:
                        if (s.Height > 0) up = Math.Max(up, s.Height * 1.5);
                        else down = Math.Max(down, -s.Height);
                        break;
                    case StampKind.Region:
                        up = Math.Max(up, Math.Max(0, s.Height) + tex);
                        down = Math.Max(down, Math.Max(0, -s.Height) + tex);
                        break;
                    default:
                        if (s.Height > 0) up = Math.Max(up, s.Height * 1.5);
                        else down = Math.Max(down, -s.Height);
                        if (s.Kind == StampKind.Cone) down = Math.Max(down, g.SummitDepth * s.Height);
                        break;
                }
            }
            // Formations overlap now and then, so leave room for two of the biggest.
            MaxHeight = f.continentBias + swing + f.mountainHeight * 1.5 + up * 2;
            MinHeight = f.continentBias - swing - down * 2;
        }

        // ------------------------------------------------------------------ holes

        /// <summary>True if this spot is inside a cave opening, where the ground is cut away.</summary>
        public bool InHole(Vec3d direction, double margin)
        {
            for (int i = 0; i < Holes.Count; i++)
            {
                double reach = (Holes[i].Radius + margin) / Radius;
                if ((direction - Holes[i].Direction).SqrMagnitude < reach * reach) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ colour

        /// <summary>
        /// The colour of the ground at a spot. <paramref name="slope"/> is how steep it is there (0 flat, 1 a wall),
        /// which the caller works out from the shape it has already built.
        /// </summary>
        public void ColorAt(Vec3d direction, in TerrainPoint point, double slope, out float r, out float g, out float b)
        {
            PlanetProfile f = Profile;
            Vec3d p = direction * Radius;
            double latitude = Math.Abs(direction.Y);
            double height = point.Height;
            double n = Noise3.Perlin(p, 37, _s + 20) * 0.6 + Noise3.Perlin(p, 9, _s + 21) * 0.4;
            double h = height + n * 3;

            float cr, cg, cb;
            if (h < f.midHeight)
            {
                double span = Math.Max(4, (f.highHeight - f.midHeight) * 0.5);
                Mix(_low, _mid, Noise3.Smooth(f.midHeight - span, f.midHeight, h), out cr, out cg, out cb);
            }
            else
            {
                Mix(_mid, _high, Noise3.Smooth(f.midHeight, f.highHeight, h), out cr, out cg, out cb);
            }

            for (int i = 0; i < _zoneColors.Length; i++)
            {
                ColorZone z = f.zones[i];
                if (latitude < z.minLatitude - 0.06 || latitude > z.maxLatitude + 0.06) continue;
                if (height < z.minHeight - 3 || height > z.maxHeight + 3) continue;
                double m = Noise3.Smooth(z.threshold, z.threshold + 0.18, Noise3.Fbm(p, z.wavelength, 3, _s + 40 + (uint)i * 7));
                if (m <= 0) continue;
                m *= Noise3.Smooth(z.minLatitude - 0.06, z.minLatitude + 0.02, latitude) * (1 - Noise3.Smooth(z.maxLatitude - 0.02, z.maxLatitude + 0.06, latitude));
                m *= Noise3.Smooth(z.minHeight - 3, z.minHeight + 1, height) * (1 - Noise3.Smooth(z.maxHeight - 1, z.maxHeight + 3, height));
                Blend(ref cr, ref cg, ref cb, _zoneColors[i], m * z.strength);
            }

            if (f.hasSea)
            {
                double above = height - f.seaLevel;
                if (_hasShore && above < 1.8) Blend(ref cr, ref cg, ref cb, _shore, 1 - Noise3.Smooth(0.5, 1.8, above));
                if (above < 0)
                {
                    // The sea bed darkens and takes on the colour of deep water.
                    double deep = Noise3.Smooth(0, 18, -above);
                    cr *= (float)(1 - 0.45 * deep);
                    cg *= (float)(1 - 0.45 * deep);
                    cb *= (float)(1 - 0.45 * deep);
                    Blend(ref cr, ref cg, ref cb, _seaDeep, deep * 0.5);
                }
            }

            if (point.TintAmount > 0)
            {
                cr += (point.TintR - cr) * point.TintAmount;
                cg += (point.TintG - cg) * point.TintAmount;
                cb += (point.TintB - cb) * point.TintAmount;
            }

            // Formations keep most of their own colour even where they are steep.
            double steep = Noise3.Smooth(0.5, 0.78, slope) * (1 - 0.65 * point.TintAmount);
            if (steep > 0)
            {
                float lr = _cliff[0], lg = _cliff[1], lb = _cliff[2];
                if (f.strata > 0)
                {
                    // Layers of rock showing on cliff faces.
                    double band = 0.5 + 0.5 * Math.Sin(height / f.strata * Math.PI * 2 + n * 2);
                    float k = (float)(0.72 + 0.5 * band);
                    lr = Math.Min(1f, (lr * 0.6f + _mid[0] * 0.4f) * k);
                    lg = Math.Min(1f, (lg * 0.6f + _mid[1] * 0.4f) * k);
                    lb = Math.Min(1f, (lb * 0.6f + _mid[2] * 0.4f) * k);
                }
                cr += (lr - cr) * (float)steep;
                cg += (lg - cg) * (float)steep;
                cb += (lb - cb) * (float)steep;
            }

            double gentle = 1 - Noise3.Smooth(0.55, 0.8, slope);
            if (height > f.peakHeight - 4)
            {
                Blend(ref cr, ref cg, ref cb, _peak, Noise3.Smooth(f.peakHeight - 4, f.peakHeight + 3, height + n * 4) * gentle);
            }
            if (_hasPole && (!f.hasSea || height > f.seaLevel))
            {
                double edge = f.poleLatitude + n * 0.03;
                Blend(ref cr, ref cg, ref cb, _pole, Noise3.Smooth(edge, edge + 0.035, latitude) * gentle);
            }

            float shade = (float)(0.93 + 0.14 * n);
            r = Clamp(cr * shade);
            g = Clamp(cg * shade);
            b = Clamp(cb * shade);
        }

        static void Mix(float[] a, float[] b, double t, out float r, out float g, out float bl)
        {
            float k = (float)t;
            r = a[0] + (b[0] - a[0]) * k;
            g = a[1] + (b[1] - a[1]) * k;
            bl = a[2] + (b[2] - a[2]) * k;
        }

        static void Blend(ref float r, ref float g, ref float b, float[] to, double amount)
        {
            if (amount <= 0) return;
            float k = (float)Math.Min(1.0, amount);
            r += (to[0] - r) * k;
            g += (to[1] - g) * k;
            b += (to[2] - b) * k;
        }

        static float Clamp(float x) => x < 0 ? 0 : x > 1 ? 1 : x;
    }

    /// <summary>Reads web colours such as "#C1440E" without needing Unity.</summary>
    public static class ColorUtil
    {
        public static float[] Parse(string hex)
        {
            var result = new[] { 0.5f, 0.5f, 0.5f };
            if (string.IsNullOrEmpty(hex)) return result;
            string s = hex[0] == '#' ? hex.Substring(1) : hex;
            if (s.Length < 6) return result;
            for (int i = 0; i < 3; i++)
            {
                int hi = Digit(s[i * 2]), lo = Digit(s[i * 2 + 1]);
                if (hi < 0 || lo < 0) return new[] { 0.5f, 0.5f, 0.5f };
                result[i] = (hi * 16 + lo) / 255f;
            }
            return result;
        }

        static int Digit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }
    }
}
