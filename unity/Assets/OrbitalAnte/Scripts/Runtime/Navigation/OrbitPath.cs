using System;
using OrbitalAnte.Core;
using OrbitalAnte.Space;

namespace OrbitalAnte.Navigation
{
    /// <summary>What the path you are coasting on does round the world you are near.</summary>
    public enum OrbitKind
    {
        /// <summary>No world to go round (open space), or too slow to say anything useful.</summary>
        None,
        /// <summary>The path comes down to the surface: left alone, you land or crash.</summary>
        MeetsGround,
        /// <summary>A loop that stays clear of the ground and inside the world's calm space: a true orbit.</summary>
        Orbit,
        /// <summary>A loop clear of the ground whose high point reaches out where the Sun (or a planet) tugs at it, so it will not repeat exactly.</summary>
        Tugged,
        /// <summary>The path carries you out of the world's neighbourhood.</summary>
        Leaves,
    }

    /// <summary>The orbit readout's numbers, worked out by <see cref="OrbitPath.Read"/>.</summary>
    public struct OrbitReading
    {
        public OrbitKind Kind;
        /// <summary>The speed, sideways, that would hold a circle at this height (metres per second).</summary>
        public double CircleSpeed;
        /// <summary>Your speed across the world's face (at right angles to "up"), not counting its spin.</summary>
        public double SideSpeed;
        /// <summary>Your speed straight up (positive) or down (negative).</summary>
        public double UpSpeed;
        /// <summary>The lowest and highest points of the path, as heights above the world's round surface.</summary>
        public double LowHeight, HighHeight;
    }

    /// <summary>
    /// The orbit aid (Matthew, 16 Nov 2026: a ship that can "truly orbit"). Given where you are and how you are
    /// moving against a world's centre, works out what a circle at this height needs and where the path you are
    /// coasting on will take you: its lowest and highest points. Uses the world's own pull law
    /// (<see cref="SolarSystemModel.PullStrength"/>), so the answer is right for the Outer Wilds-style pull, not
    /// just for the real law. The world's moons are left out. Plain C#, so it is tested without Unity.
    /// </summary>
    public static class OrbitPath
    {
        /// <summary>Each step of the search for the high and low points is this much further out (or in).</summary>
        const double StepRatio = 1.01;
        /// <summary>Below this speed against the world's centre, the readout says nothing.</summary>
        public const double QuietSpeed = 0.5;

        /// <param name="offset">Where you are, measured from the world's centre (true axes, metres).</param>
        /// <param name="velocity">Your velocity against the world's centre, not against its spinning ground (true axes).</param>
        public static OrbitReading Read(SolarSystemModel model, int body, Vec3d offset, Vec3d velocity)
        {
            var reading = new OrbitReading();
            if (model == null || body <= 0 || body >= model.Count) return reading;
            double d0 = offset.Magnitude;
            double radius = model.Spec(body).radius;
            if (d0 < 1e-3) return reading;
            Vec3d up = offset / d0;
            reading.UpSpeed = Vec3d.Dot(velocity, up);
            Vec3d side = velocity - up * reading.UpSpeed;
            reading.SideSpeed = side.Magnitude;
            reading.CircleSpeed = model.CircularOrbitSpeed(body, d0);
            if (velocity.Magnitude < QuietSpeed) return reading;

            // Energy and the turn about the centre stay the same along the path. At a high or low point all the
            // speed is sideways, so  v0^2 / 2 - (work done by the pull from d0 to d) = L^2 / (2 d^2).
            double half = 0.5 * velocity.SqrMagnitude;
            double turn = d0 * reading.SideSpeed;
            double edge = model.InfluenceRadius(body);
            double limit = edge * 1.05;

            double high = Search(model, body, d0, half, turn, StepRatio, limit);
            double low = Search(model, body, d0, half, turn, 1.0 / StepRatio, radius);
            reading.LowHeight = Math.Max(0, low - radius);
            reading.HighHeight = high - radius;

            if (low <= radius) reading.Kind = OrbitKind.MeetsGround;
            else if (high >= limit) reading.Kind = OrbitKind.Leaves;
            else if (model.OutsideShare(body, high) > 0) reading.Kind = OrbitKind.Tugged;
            else reading.Kind = OrbitKind.Orbit;
            return reading;
        }

        /// <summary>
        /// Walks from <paramref name="d0"/> outward (ratio above 1) or inward (below 1) until the path turns,
        /// or the walk reaches <paramref name="stop"/>, and returns the distance it turns at (or the stop).
        /// </summary>
        static double Search(SolarSystemModel model, int body, double d0, double half, double turn, double ratio, double stop)
        {
            double d = d0;
            double work = 0;
            double pull = model.PullStrength(body, d);
            double left = half - turn * turn / (2 * d * d);
            for (int i = 0; i < 2000; i++)
            {
                double next = d * ratio;
                bool last = ratio > 1 ? next >= stop : next <= stop;
                if (last) next = stop;
                double nextPull = model.PullStrength(body, next);
                // Going out, the pull takes energy away; coming in, it gives it.
                work += 0.5 * (pull + nextPull) * (next - d);
                double nextLeft = half - work - turn * turn / (2 * next * next);
                if (nextLeft < 0)
                {
                    // The turn lies between d and next: share the step by how much was left either side.
                    double share = left / Math.Max(1e-12, left - nextLeft);
                    return d + (next - d) * Math.Max(0, Math.Min(1, share));
                }
                if (last) return stop;
                d = next;
                pull = nextPull;
                left = nextLeft;
            }
            return d;
        }
    }
}
