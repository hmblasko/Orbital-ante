using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Meteors
{
    /// <summary>What a strike needs to know about the world it falls on. Safe to read from a background thread.</summary>
    public struct PlanWorld
    {
        /// <summary>Distance from the body's centre to the ground in a unit direction, in metres.</summary>
        public Func<Vec3d, double> GroundRadius;
        /// <summary>True where a unit direction is open water (a piece landing there splashes, no crater).</summary>
        public Func<Vec3d, bool> Water;
        /// <summary>No ground anywhere is further out than this from the centre, in metres.</summary>
        public double Ceiling;
        /// <summary>The body's surface radius, in metres, and the pull there.</summary>
        public double SurfaceRadius;
        public double Gravity;
        public bool Air;
        /// <summary>How quickly air slows a rock, per second.</summary>
        public double Drag;
    }

    /// <summary>One rock in the air: the whole meteor, or a piece of it after a burst.</summary>
    public sealed class MeteorPiece
    {
        /// <summary>The piece it broke from, or -1 for the whole rock.</summary>
        public int Parent = -1;
        public int Generation;
        /// <summary>Seconds from the start of the strike when it appears and when it bursts or lands.</summary>
        public double Born, Ends;
        /// <summary>Radius of the crater it makes when it lands (or would have made), in metres.</summary>
        public double Crater;
        /// <summary>Positions in the body's space every <see cref="MeteorPlan.Step"/> seconds from <see cref="Born"/>.</summary>
        public Vec3d[] Path;
        /// <summary>Where it bursts or lands, and its velocity then.</summary>
        public Vec3d End, EndVelocity;
        /// <summary>It bursts into pieces at <see cref="End"/> instead of landing.</summary>
        public bool Bursts;
        /// <summary>It lands in water: a splash and steam, no crater.</summary>
        public bool Splash;
        /// <summary>It never met the ground (should not happen): it simply goes.</summary>
        public bool Lost;

        public bool Lands => !Bursts && !Lost;

        /// <summary>Radius of the rock itself, in metres. Craters are many times wider than the rock that made them.</summary>
        public double Rock => Math.Max(0.12, Crater / 9.0);
    }

    /// <summary>A whole strike worked out in advance: every piece's path, every burst and every landing.</summary>
    public sealed class MeteorPlan
    {
        public const double Step = 0.05;
        public Vec3d Target;
        public bool Air;
        public readonly List<MeteorPiece> Pieces = new List<MeteorPiece>();

        public double Duration
        {
            get
            {
                double d = 0;
                for (int i = 0; i < Pieces.Count; i++) d = Math.Max(d, Pieces[i].Ends);
                return d;
            }
        }

        /// <summary>Where a piece is at a time in the strike (clamped to its own life).</summary>
        public static Vec3d PositionAt(MeteorPiece p, double t)
        {
            if (p.Path == null || p.Path.Length == 0) return p.End;
            double local = (t - p.Born) / Step;
            if (local <= 0) return p.Path[0];
            if (t >= p.Ends) return p.End;
            int i = (int)local;
            int last = p.Path.Length - 1;
            if (i >= last - 1)
            {
                // The last stretch runs from the last regular sample to the end, which falls between samples.
                int from = Math.Max(0, last - 1);
                double t0 = from * Step, t1 = p.Ends - p.Born;
                double k = t1 > t0 ? (t - p.Born - t0) / (t1 - t0) : 1;
                k = k < 0 ? 0 : k > 1 ? 1 : k;
                return p.Path[from] + (p.End - p.Path[from]) * k;
            }
            double f = local - i;
            Vec3d a = p.Path[i], b = p.Path[i + 1];
            return a + (b - a) * f;
        }

        /// <summary>A piece's velocity at a time, from its path.</summary>
        public static Vec3d VelocityAt(MeteorPiece p, double t)
        {
            if (p.Path == null || p.Path.Length < 2) return p.EndVelocity;
            int i = (int)Math.Floor((t - p.Born) / Step);
            if (i < 0) i = 0;
            if (i >= p.Path.Length - 1) return p.EndVelocity;
            return (p.Path[i + 1] - p.Path[i]) / Step;
        }
    }

    /// <summary>Works out a strike: the rock's path aimed to land on the target, its bursts and its pieces' paths.</summary>
    public static class MeteorPlanner
    {
        const double Dt = 0.025;
        const double MaxSeconds = 25;

        public static MeteorPlan Plan(MeteorRules r, SeededRandom rng, Vec3d targetDirection, in PlanWorld world)
        {
            var plan = new MeteorPlan { Air = world.Air };
            Vec3d up = targetDirection.Normalized;
            double groundAtTarget = world.GroundRadius(up);
            Vec3d target = up * groundAtTarget;
            plan.Target = target;

            // Small rocks are commoner than big ones.
            double crater = MeteorAim.Lerp(r.minCrater, r.maxCrater, Math.Pow(rng.NextDouble(), 1.6));
            double speed = MeteorAim.Lerp(r.entrySpeedMin, r.entrySpeedMax, rng.NextDouble());
            double angle = MeteorAim.Lerp(r.entryAngleMin, r.entryAngleMax, rng.NextDouble()) * Math.PI / 180.0;
            Vec3d heading = up.AnyPerpendicular().RotatedAround(up, rng.NextDouble() * Math.PI * 2);
            Vec3d along = (heading * Math.Cos(angle) - up * Math.Sin(angle)).Normalized;
            // On a small world a long straight run in would start below the far horizon; keep it above the ground.
            double height = Math.Min(r.startAltitude, Math.Max(150, world.SurfaceRadius * 1.3));
            double run = height / Math.Sin(angle);
            // Always a few seconds to see it coming, even on a tiny moon where the run in is short.
            speed = Math.Min(speed, run / Math.Max(0.5, r.warningSeconds));
            Vec3d velocity = along * speed;
            Vec3d start = target - along * run;

            // Aim: fly it, see where it lands, move the start by the miss, and again (gravity bends the path).
            for (int i = 0; i < 8; i++)
            {
                Flight test = Fly(start, velocity, -1, groundAtTarget, world);
                if (!test.Landed) break;
                if ((target - test.End).Magnitude < 0.05) break;
                // Turn the whole path round the body's centre by the miss: the same fall, moved along the ground.
                Vec3d from = test.End.Normalized;
                Vec3d axis = Vec3d.Cross(from, up);
                double sin = axis.Magnitude;
                if (sin < 1e-12) break;
                double turn = Math.Atan2(sin, Vec3d.Dot(from, up));
                axis /= sin;
                start = start.RotatedAround(axis, turn);
                velocity = velocity.RotatedAround(axis, turn);
            }

            bool bursts = world.Air && rng.Chance(r.airBreakupChance);
            double burstAt = bursts ? MeteorAim.Lerp(r.breakupAltitudeMin, r.breakupAltitudeMax, rng.NextDouble()) : -1;
            var whole = new MeteorPiece { Crater = crater, Born = 0 };
            Fill(whole, Fly(start, velocity, burstAt, groundAtTarget, world), world);
            plan.Pieces.Add(whole);

            if (whole.Bursts)
            {
                int budget = Math.Max(2, r.maxTotalPieces);
                int count = Math.Min(budget, rng.Range(Math.Max(2, r.minPieces), Math.Max(r.minPieces, r.maxPieces) + 1));
                budget -= count;
                int first = plan.Pieces.Count;
                Burst(plan, 0, count, r, rng, groundAtTarget, world);
                // The biggest piece may burst again lower down.
                if (budget >= 2 && rng.Chance(r.secondBreakChance))
                {
                    MeteorPiece big = plan.Pieces[first];
                    double altitude = big.Path != null && big.Path.Length > 0 ? big.Path[0].Magnitude - groundAtTarget : 0;
                    double again = altitude * MeteorAim.Lerp(0.35, 0.55, rng.NextDouble());
                    if (again > 15)
                    {
                        Flight redo = Fly(big.Path[0], (big.Path.Length > 1 ? (big.Path[1] - big.Path[0]) / MeteorPlan.Step : big.EndVelocity), again, groundAtTarget, world);
                        if (redo.Burst)
                        {
                            Fill(big, redo, world);
                            Burst(plan, first, Math.Min(budget, rng.Range(2, 4)), r, rng, groundAtTarget, world);
                        }
                    }
                }
            }
            return plan;
        }

        /// <summary>Breaks a piece into <paramref name="count"/> pieces that fly apart from where it burst.</summary>
        static void Burst(MeteorPlan plan, int parentIndex, int count, MeteorRules r, SeededRandom rng, double groundAtTarget, in PlanWorld world)
        {
            MeteorPiece parent = plan.Pieces[parentIndex];
            if (count < 1) return;
            // Shares of what is left: one piece takes the most, the rest split the remainder unevenly.
            var shares = new double[count];
            double total = 0;
            for (int i = 0; i < count; i++)
            {
                shares[i] = (i == 0 ? 1.6 : 0.4) + rng.NextDouble();
                total += shares[i];
            }
            double kept = Math.Max(0.05, 1 - r.burstLoss);
            Vec3d v = parent.EndVelocity;
            Vec3d forward = v.Magnitude > 1e-6 ? v.Normalized : -parent.End.Normalized;
            for (int i = 0; i < count; i++)
            {
                double c = Math.Max(r.smallestCrater, parent.Crater * Math.Pow(shares[i] / total * kept, 1.0 / 3.0));
                // Thrown apart mostly sideways to the path: a burst opens like a flower, it does not stop the rock.
                Vec3d kick = RandomUnit(rng);
                kick = (kick - forward * (Vec3d.Dot(kick, forward) * 0.7)).Normalized;
                double spread = MeteorAim.Lerp(r.spreadSpeedMin, r.spreadSpeedMax, rng.NextDouble());
                // Small pieces are thrown harder.
                spread *= Math.Min(1.1, Math.Sqrt(parent.Crater / c));
                Vec3d velocity = v * MeteorAim.Lerp(0.88, 1.0, rng.NextDouble()) + kick * spread;
                var piece = new MeteorPiece { Parent = parentIndex, Generation = parent.Generation + 1, Crater = c, Born = parent.Ends };
                Fill(piece, Fly(parent.End, velocity, -1, groundAtTarget, world), world);
                plan.Pieces.Add(piece);
            }
            // Biggest first, so a second burst always takes the biggest.
            plan.Pieces.Sort(plan.Pieces.Count - count, count, Comparer<MeteorPiece>.Create((a, b) => b.Crater.CompareTo(a.Crater)));
        }

        static void Fill(MeteorPiece piece, in Flight f, in PlanWorld world)
        {
            piece.Path = f.Path;
            piece.End = f.End;
            piece.EndVelocity = f.EndVelocity;
            piece.Ends = piece.Born + f.Seconds;
            piece.Bursts = f.Burst;
            piece.Lost = !f.Burst && !f.Landed;
            piece.Splash = f.Landed && world.Water != null && world.Water(f.End.Normalized);
        }

        struct Flight
        {
            public Vec3d[] Path;
            public Vec3d End, EndVelocity;
            public double Seconds;
            public bool Landed, Burst;
        }

        /// <summary>
        /// Flies a rock from a point until it meets the ground or (when <paramref name="burstAltitude"/> is above 0)
        /// comes down to that height over the target's ground. Pulled toward the centre (1 / distance, as the game's
        /// worlds pull near the ground) and, in air, slowed by drag.
        /// </summary>
        static Flight Fly(Vec3d start, Vec3d velocity, double burstAltitude, double groundAtTarget, in PlanWorld world)
        {
            var path = new List<Vec3d>(256) { start };
            Vec3d p = start, v = velocity;
            double drag = world.Air ? world.Drag : 0;
            int steps = (int)(MaxSeconds / Dt);
            int perSample = (int)Math.Round(MeteorPlan.Step / Dt);
            for (int s = 1; s <= steps; s++)
            {
                Vec3d before = p, vBefore = v;
                double r = p.Magnitude;
                if (r < 1e-3) break;
                Vec3d pull = -(p / r) * (world.Gravity * Math.Min(4.0, world.SurfaceRadius / r));
                v += pull * Dt;
                if (drag > 0) v -= v * (drag * Dt);
                p += v * Dt;
                double rNow = p.Magnitude;

                if (burstAltitude > 0 && rNow - groundAtTarget <= burstAltitude)
                {
                    return Finish(path, p, v, s * Dt, false, true);
                }
                if (rNow <= world.Ceiling)
                {
                    double ground = world.GroundRadius(p / rNow);
                    if (rNow <= ground)
                    {
                        // Narrow down where between the last two steps it met the ground.
                        double lo = 0, hi = 1;
                        for (int k = 0; k < 6; k++)
                        {
                            double mid = (lo + hi) * 0.5;
                            Vec3d q = before + (p - before) * mid;
                            double qr = q.Magnitude;
                            if (qr <= world.GroundRadius(q / qr)) hi = mid;
                            else lo = mid;
                        }
                        Vec3d hit = before + (p - before) * hi;
                        hit = hit.Normalized * world.GroundRadius(hit.Normalized);
                        Vec3d vHit = vBefore + (v - vBefore) * hi;
                        return Finish(path, hit, vHit, (s - 1 + hi) * Dt, true, false);
                    }
                }
                if (s % perSample == 0) path.Add(p);
            }
            return Finish(path, p, v, MaxSeconds, false, false);
        }

        static Flight Finish(List<Vec3d> path, Vec3d end, Vec3d velocity, double seconds, bool landed, bool burst)
        {
            path.Add(end);
            return new Flight { Path = path.ToArray(), End = end, EndVelocity = velocity, Seconds = seconds, Landed = landed, Burst = burst };
        }

        static Vec3d RandomUnit(SeededRandom rng)
        {
            double z = rng.NextDouble() * 2 - 1;
            double a = rng.NextDouble() * Math.PI * 2;
            double s = Math.Sqrt(Math.Max(0, 1 - z * z));
            return new Vec3d(s * Math.Cos(a), s * Math.Sin(a), z);
        }
    }
}
