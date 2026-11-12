using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>The weather at one spot at one moment.</summary>
    public struct WeatherSample
    {
        /// <summary>0 is clear, 1 is the heart of a storm.</summary>
        public double Intensity;
        /// <summary>Which kind of weather this is. Null when clear.</summary>
        public WeatherSpec Spec;
        /// <summary>Wind along the ground in the body's own axes, in metres per second.</summary>
        public Vec3d Wind;
    }

    /// <summary>
    /// A body's weather as pure rules: a handful of storms that drift round the body, each building up and
    /// dying away. Where the storms are depends only on the run seed and the time, so the same seed has the
    /// same weather. No Unity types.
    /// </summary>
    public sealed class WeatherModel
    {
        struct Storm
        {
            public WeatherSpec Spec;
            public double Y;            // height on the unit ball, i.e. the sine of its latitude
            public double StartAngle;
            public double DriftRate;    // radians per second round the body's axis
            public double Period;
            public double Phase;
            public double Radius;       // metres
        }

        readonly Storm[] _storms;
        readonly double _radius;

        public int Count => _storms.Length;
        public bool Any => _storms.Length > 0;

        public WeatherModel(PlanetProfile profile, string bodyId, double surfaceRadius, int seed)
        {
            _radius = surfaceRadius;
            var storms = new List<Storm>();
            double shrink = Math.Max(0.4, Math.Min(1.0, Math.Sqrt(surfaceRadius / 500.0)));
            for (int w = 0; w < profile.weather.Count; w++)
            {
                WeatherSpec spec = profile.weather[w];
                var r = SeededRandom.ForStream(seed, "weather." + bodyId + "." + w);
                for (int i = 0; i < spec.cells; i++)
                {
                    bool placed = spec.permanent || spec.latitude != 0;
                    double y = placed
                        ? Math.Sin((spec.latitude + (spec.permanent ? 0 : r.Range(-8f, 8f))) * Math.PI / 180.0)
                        : r.Range(-0.8f, 0.8f);
                    if (!spec.permanent && spec.latitude != 0 && r.Chance(0.5)) y = -y;
                    double ring = Math.Sqrt(Math.Max(0.05, 1 - y * y));
                    // Storms travel a good deal slower than the wind inside them.
                    double drift = spec.windSpeed * 0.2 / (surfaceRadius * ring) * (r.Chance(0.5) ? 1 : -1);
                    storms.Add(new Storm
                    {
                        Spec = spec, Y = y, StartAngle = r.NextDouble() * Math.PI * 2, DriftRate = drift,
                        Period = spec.permanent ? 0 : r.Range(260f, 520f), Phase = r.NextDouble() * Math.PI * 2,
                        Radius = spec.cellRadius * shrink,
                    });
                }
            }
            _storms = storms.ToArray();
        }

        /// <summary>The middle of a storm at a moment, as a unit direction in the body's own axes.</summary>
        public Vec3d Centre(int index, double time)
        {
            Storm s = _storms[index];
            double angle = s.StartAngle + s.DriftRate * time;
            double ring = Math.Sqrt(Math.Max(0, 1 - s.Y * s.Y));
            return new Vec3d(ring * Math.Cos(angle), s.Y, ring * Math.Sin(angle));
        }

        /// <summary>How strong a storm is at a moment, whatever your distance from it. 0 to 1.</summary>
        public double Strength(int index, double time)
        {
            Storm s = _storms[index];
            if (s.Period <= 0) return 1;
            double wave = 0.5 + 0.5 * Math.Sin(time / s.Period * Math.PI * 2 + s.Phase);
            return Noise3.Smooth(0.2, 0.65, wave);
        }

        public double RadiusOf(int index) => _storms[index].Radius;
        public WeatherSpec SpecOf(int index) => _storms[index].Spec;

        public WeatherSample At(Vec3d direction, double time)
        {
            var best = new WeatherSample();
            for (int i = 0; i < _storms.Length; i++)
            {
                double strength = Strength(i, time);
                if (strength <= 0) continue;
                Vec3d centre = Centre(i, time);
                double distance = (direction - centre).Magnitude * _radius;
                double radius = _storms[i].Radius;
                if (distance >= radius) continue;
                double intensity = strength * (1 - Noise3.Smooth(0.5, 1, distance / radius));
                if (intensity <= best.Intensity) continue;

                // The wind blows the way the storm is travelling, with a swirl round its middle.
                Vec3d east = Vec3d.Cross(Vec3d.Up, direction);
                if (east.SqrMagnitude < 1e-6) east = Vec3d.Right;
                east = east.Normalized * Math.Sign(_storms[i].DriftRate);
                Vec3d swirl = Vec3d.Cross(centre, direction);
                swirl = swirl.SqrMagnitude > 1e-10 ? swirl.Normalized : Vec3d.Zero;
                Vec3d wind = (east * 0.6 + swirl * 0.6) * (_storms[i].Spec.windSpeed * intensity);
                // Keep it along the ground.
                wind -= direction * Vec3d.Dot(wind, direction);

                best.Intensity = intensity;
                best.Spec = _storms[i].Spec;
                best.Wind = wind;
            }
            return best;
        }
    }
}
