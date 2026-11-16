using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Space
{
    /// <summary>Where one body is and how it is moving at one moment, measured from the Sun's starting point.</summary>
    public struct BodyState
    {
        public Vec3d Position;
        public Vec3d Velocity;
        public Vec3d Acceleration;
        /// <summary>How far it has turned about its spin axis, in radians.</summary>
        public double SpinAngle;
    }

    /// <summary>
    /// The solar system as pure rules: where every body is at any time, how it spins, and how hard everything
    /// pulls on a given point. No Unity types, so it is unit tested outside the engine.
    ///
    /// Bodies ride fixed circular orbits ("on rails"). The position at time T is worked out directly from T,
    /// so the system never drifts, and two runs of the same seed are in the same place at the same time.
    /// The seed decides only where along its orbit each body starts, as the design asks.
    /// </summary>
    public sealed class SolarSystemModel
    {
        const double TwoPi = Math.PI * 2.0;

        readonly BodySpec[] _specs;
        readonly int[] _parent;
        readonly double[] _mu;
        readonly double[] _orbitRate;
        readonly Vec3d[] _orbitNormal;
        readonly Vec3d[] _orbitStart;
        readonly double[] _phase;
        readonly Vec3d[] _spinAxis;
        readonly double[] _spinRate;
        readonly double[] _spinStart;
        readonly QuatD[] _tilt;
        readonly double[] _influence;
        readonly double[] _lockRadius;
        readonly Dictionary<string, int> _byId = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// The sign of an ordinary ("prograde") turn. In the real solar system nearly everything goes round
        /// anticlockwise seen from above the north pole. With Unity's left-handed axes and +Y as north, that is a
        /// negative angle, so ordinary orbits and spins have negative rates here and backwards ones positive.
        /// </summary>
        public const double Prograde = -1.0;

        public int Count => _specs.Length;
        public int Seed { get; }

        /// <param name="bodies">Every body. Order does not matter; parents are sorted in front of their children.</param>
        /// <param name="nearFalloff">See <see cref="NearFalloff"/>: 1 (the default) as in Outer Wilds, 2 the real law.</param>
        public SolarSystemModel(IList<BodySpec> bodies, int seed, double nearFalloff = 1.0)
        {
            if (bodies == null || bodies.Count == 0) throw new ArgumentException("A solar system needs at least one body.");
            Seed = seed;
            NearFalloff = Math.Max(0.5, Math.Min(2.0, nearFalloff));
            _specs = SortParentsFirst(bodies);
            int n = _specs.Length;
            _parent = new int[n];
            _mu = new double[n];
            _orbitRate = new double[n];
            _orbitNormal = new Vec3d[n];
            _orbitStart = new Vec3d[n];
            _phase = new double[n];
            _spinAxis = new Vec3d[n];
            _spinRate = new double[n];
            _spinStart = new double[n];
            _tilt = new QuatD[n];
            _influence = new double[n];
            _lockRadius = new double[n];

            for (int i = 0; i < n; i++) _byId[_specs[i].id] = i;

            for (int i = 0; i < n; i++)
            {
                BodySpec s = _specs[i];
                _parent[i] = string.IsNullOrEmpty(s.parentId) ? -1 : _byId[s.parentId];
                _mu[i] = s.surfaceGravity * s.radius * s.radius;

                // Orbit: a circle in a plane tilted by the inclination about the "node" direction.
                Vec3d node = Vec3d.Right.RotatedAround(Vec3d.Up, Radians(s.orbitNode));
                Vec3d normal = Vec3d.Up.RotatedAround(node, Radians(s.orbitInclination));
                _orbitNormal[i] = normal;
                _orbitStart[i] = node;
                if (_parent[i] >= 0 && s.orbitRadius > 0)
                {
                    // Circular orbit speed follows from the parent's pull: v squared = mu / r.
                    double rate = Math.Sqrt(_mu[_parent[i]] / (s.orbitRadius * s.orbitRadius * s.orbitRadius));
                    _orbitRate[i] = s.retrogradeOrbit ? -rate * Prograde : rate * Prograde;
                }

                // The seed only chooses the starting point along the orbit, and how far round the spin starts.
                var rng = SeededRandom.ForStream(seed, "solar.body." + s.id);
                _phase[i] = rng.NextDouble() * TwoPi;
                _spinStart[i] = rng.NextDouble() * TwoPi;

                if (s.tidallyLocked && _parent[i] >= 0)
                {
                    _spinAxis[i] = normal;
                    _spinRate[i] = _orbitRate[i];
                }
                else
                {
                    _spinAxis[i] = normal.RotatedAround(node, Radians(s.axialTilt)).Normalized;
                    double rate = s.dayLength > 0 ? TwoPi / s.dayLength : 0;
                    _spinRate[i] = s.retrogradeSpin ? -rate * Prograde : rate * Prograde;
                }
                _tilt[i] = QuatD.FromTo(Vec3d.Up, _spinAxis[i]);
            }

            // Neighbourhoods. Children come after their parents in the array, so going backwards sizes every moon
            // before its planet, and a planet's neighbourhood can be made big enough to hold all its moons.
            var reach = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                BodySpec s = _specs[i];
                if (_parent[i] < 0)
                {
                    _influence[i] = double.PositiveInfinity;
                    _lockRadius[i] = 0;   // Nobody lands on the Sun, so nothing ever turns with it.
                    continue;
                }
                bool planet = _parent[i] == 0 && (s.kind == BodyKind.Planet || s.kind == BodyKind.GasGiant);
                double radii = planet ? PlanetInfluenceRadii : InfluenceRadii;
                _influence[i] = Math.Max(Math.Max(s.radius * radii, s.radius + 80.0), reach[i] * 1.15);
                _lockRadius[i] = Math.Max(s.radius * 1.5, s.radius + 60.0);
                int parent = _parent[i];
                reach[parent] = Math.Max(reach[parent], s.orbitRadius + _influence[i]);
            }

            // A moon rides its circle at exactly the speed its planet's pull there asks for, so a ship flying up to
            // it inside the planet's neighbourhood falls along with it. That pull depends on the neighbourhoods
            // (see PullStrength), which are only known now, so the moons' rates are worked out again here. The
            // planets' rates come from the Sun, which keeps the real law, so they do not change. (Since 16 Nov 2026
            // Earth's Moon goes round in about 240 seconds.)
            for (int i = 0; i < n; i++)
            {
                BodySpec s = _specs[i];
                if (_parent[i] < 0 || s.orbitRadius <= 0) continue;
                double rate = Math.Sqrt(PullStrength(_parent[i], s.orbitRadius) / s.orbitRadius);
                _orbitRate[i] = s.retrogradeOrbit ? -rate * Prograde : rate * Prograde;
                if (s.tidallyLocked) _spinRate[i] = _orbitRate[i];
            }
        }

        /// <summary>A body's neighbourhood reaches at least this many of its own radii from its centre.</summary>
        public const double InfluenceRadii = 3.5;

        /// <summary>
        /// A planet's neighbourhood reaches at least this many of its own radii. Wider than a moon's or a rock's
        /// (<see cref="InfluenceRadii"/>) so that every planet has a deep band of calm space to orbit in
        /// (Matthew, 16 Nov 2026: "such that a ship can truly orbit"); a moon's must stay small enough not to
        /// overlap its neighbours'.
        /// </summary>
        public const double PlanetInfluenceRadii = 5.0;

        static double Radians(double degrees) => degrees * Math.PI / 180.0;

        static BodySpec[] SortParentsFirst(IList<BodySpec> bodies)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i] == null || string.IsNullOrEmpty(bodies[i].id)) throw new ArgumentException("Every body needs an id.");
                if (!ids.Add(bodies[i].id)) throw new ArgumentException("Two bodies are both called '" + bodies[i].id + "'.");
            }

            var sorted = new List<BodySpec>(bodies.Count);
            var placed = new HashSet<string>(StringComparer.Ordinal);
            int roots = 0;
            while (sorted.Count < bodies.Count)
            {
                int before = sorted.Count;
                for (int i = 0; i < bodies.Count; i++)
                {
                    BodySpec s = bodies[i];
                    if (placed.Contains(s.id)) continue;
                    bool root = string.IsNullOrEmpty(s.parentId);
                    if (!root && !ids.Contains(s.parentId))
                        throw new ArgumentException("'" + s.id + "' orbits '" + s.parentId + "', which does not exist.");
                    if (root || placed.Contains(s.parentId))
                    {
                        if (root) roots++;
                        sorted.Add(s);
                        placed.Add(s.id);
                    }
                }
                if (sorted.Count == before) throw new ArgumentException("Some bodies orbit each other in a loop.");
            }
            if (roots != 1) throw new ArgumentException("Exactly one body (the Sun) must have no parent; found " + roots + ".");
            // The root goes first so index 0 is always the Sun.
            int rootAt = sorted.FindIndex(s => string.IsNullOrEmpty(s.parentId));
            if (rootAt > 0)
            {
                BodySpec root = sorted[rootAt];
                sorted.RemoveAt(rootAt);
                sorted.Insert(0, root);
            }
            return sorted.ToArray();
        }

        // ------------------------------------------------------------------ lookups

        public BodySpec Spec(int index) => _specs[index];
        /// <summary>Index of the body this one orbits, or -1 for the Sun.</summary>
        public int ParentOf(int index) => _parent[index];
        public int IndexOf(string id) => id != null && _byId.TryGetValue(id, out int i) ? i : -1;
        /// <summary>Strength of the body's pull: gravity at distance r is Mu / r squared.</summary>
        public double Mu(int index) => _mu[index];
        /// <summary>Turn rate round the orbit, radians per second. Its sign is <see cref="Prograde"/> for ordinary orbits.</summary>
        public double OrbitRate(int index) => _orbitRate[index];
        public double OrbitSpeed(int index) => Math.Abs(_orbitRate[index]) * _specs[index].orbitRadius;
        /// <summary>Seconds for one trip round the parent. 0 for the Sun.</summary>
        public double OrbitPeriod(int index) => _orbitRate[index] != 0 ? TwoPi / Math.Abs(_orbitRate[index]) : 0;
        public Vec3d OrbitNormal(int index) => _orbitNormal[index];
        public Vec3d SpinAxis(int index) => _spinAxis[index];
        /// <summary>Spin rate in radians per second. Its sign is <see cref="Prograde"/> for ordinary spin.</summary>
        public double SpinRate(int index) => _spinRate[index];
        /// <summary>Inside this distance from its centre, the body is the one that matters most.</summary>
        public double InfluenceRadius(int index) => _influence[index];
        /// <summary>Inside this distance the world is treated as turning with the body, so its ground stands still.</summary>
        public double LockRadius(int index) => _lockRadius[index];

        public BodyState[] NewStateBuffer() => new BodyState[_specs.Length];

        // ------------------------------------------------------------------ motion

        /// <summary>Fills <paramref name="states"/> with every body's position, velocity, acceleration and spin at <paramref name="time"/>.</summary>
        public void Evaluate(double time, BodyState[] states)
        {
            for (int i = 0; i < _specs.Length; i++)
            {
                BodyState state = default;
                int parent = _parent[i];
                if (parent >= 0)
                {
                    double rate = _orbitRate[i];
                    double angle = _phase[i] + rate * time;
                    Vec3d offset = _orbitStart[i].RotatedAround(_orbitNormal[i], angle) * _specs[i].orbitRadius;
                    // Parents are always earlier in the array, so theirs is already worked out.
                    state.Position = states[parent].Position + offset;
                    state.Velocity = states[parent].Velocity + Vec3d.Cross(_orbitNormal[i], offset) * rate;
                    state.Acceleration = states[parent].Acceleration - offset * (rate * rate);
                }
                state.SpinAngle = _spinStart[i] + _spinRate[i] * time;
                states[i] = state;
            }
        }

        /// <summary>
        /// A point on a body's orbit, measured from its parent's centre. <paramref name="fraction"/> runs from
        /// 0 to 1 round the whole orbit. For drawing orbits on the map.
        /// </summary>
        public Vec3d OrbitPoint(int index, double fraction)
        {
            return _orbitStart[index].RotatedAround(_orbitNormal[index], fraction * TwoPi) * _specs[index].orbitRadius;
        }

        Vec3d[][] _outlines;

        /// <summary>
        /// The whole orbit as a closed loop of points measured from the parent's centre. Worked out once and kept,
        /// because an orbit's shape never changes. For drawing orbits on the map.
        /// </summary>
        public Vec3d[] OrbitOutline(int index, int segments = 72)
        {
            if (_outlines == null) _outlines = new Vec3d[_specs.Length][];
            Vec3d[] outline = _outlines[index];
            if (outline == null || outline.Length != segments + 1)
            {
                outline = new Vec3d[segments + 1];
                for (int s = 0; s <= segments; s++) outline[s] = OrbitPoint(index, s / (double)segments);
                _outlines[index] = outline;
            }
            return outline;
        }

        /// <summary>Which way the body's surface faces: its tilt, then its spin. Local "up" is its north pole.</summary>
        public QuatD Orientation(int index, in BodyState state)
        {
            return QuatD.AngleAxis(_spinAxis[index], state.SpinAngle) * _tilt[index];
        }

        // ------------------------------------------------------------------ gravity

        /// <summary>Pull of one body on a point, as an acceleration.</summary>
        public Vec3d GravityFrom(int index, Vec3d point, BodyState[] states)
        {
            Vec3d to = states[index].Position - point;
            double distance = to.Magnitude;
            if (distance < 1e-6) return Vec3d.Zero;
            return to * (PullStrength(index, distance) / distance);
        }

        /// <summary>
        /// How steeply a planet's or moon's pull fades with distance near it: the pull goes as (radius / distance)
        /// to this power. 2 is the real law. Matthew asked on 15 Nov 2026 for gravity "more pronounced when closer
        /// to planet like Outer Wilds"; Outer Wilds lets most of its worlds' pull fade only as 1 / distance, so a
        /// world drags at you well before you reach it and the pull builds steadily as you close in. The default
        /// is 1, the same. The Sun keeps the real law, so the planets' orbits are not touched.
        /// </summary>
        public double NearFalloff { get; }

        /// <summary>
        /// Where a world's pull turns from <see cref="NearFalloff"/> to the inverse-square law, as a share of its
        /// neighbourhood. Matthew asked on 16 Nov 2026 for the pull to be "more pronounced such that a ship can
        /// truly orbit". Before, the pull dropped off a cliff in the outer part of the neighbourhood, to meet the
        /// real law at its edge: it fell faster than 1 / distance cubed there, and an orbit with that is not stable
        /// (a circle a little too low falls in, a little too high flies off). Now the pull bends over gently and
        /// never falls faster than 1 / distance squared anywhere, so every circle round a world is stable, high or
        /// low. The price is that a world pulls harder than the real law far from it too (Earth about seven times),
        /// which is what "more pronounced" asks for.
        /// </summary>
        public const double ReachScale = 1.0;

        /// <summary>
        /// The share of a body's neighbourhood with no outside tug at all. Beyond it, the tug of the body it
        /// orbits (and of its neighbours) is eased in, so at the edge, where the rule for who pulls on you changes,
        /// the pull is the same on both sides. Inside it, an orbit is a clean, repeating loop.
        /// </summary>
        public const double CalmReach = 0.7;

        /// <summary>
        /// How hard body <paramref name="index"/> pulls at <paramref name="distance"/> from its centre, as an
        /// acceleration. The pull at the surface is always the body's <see cref="BodySpec.surfaceGravity"/>, so each
        /// world keeps its own weight on the ground (Matthew, 16 Nov 2026: "certain planets will have more or less
        /// gravity"). Below the surface it fades evenly to nothing at the centre. Above it, it fades as
        /// (radius / distance) ^ <see cref="NearFalloff"/> at first and bends smoothly over to the inverse-square
        /// law far away, the bend centred at <see cref="ReachScale"/> of the neighbourhood:
        ///   g (r / d)^n ((1 + r / s) / (1 + d / s))^(2 - n).
        /// How steeply it falls runs smoothly from n near the world to 2 far off, never steeper, so circular
        /// orbits are stable at every height. The Sun keeps the real law.
        /// </summary>
        public double PullStrength(int index, double distance)
        {
            BodySpec spec = _specs[index];
            double radius = spec.radius;
            double g = spec.surfaceGravity;
            if (distance < radius) return g * distance / radius;
            double edge = _influence[index];
            if (NearFalloff == 2.0 || double.IsInfinity(edge)) return _mu[index] / (distance * distance);
            double s = edge * ReachScale;
            double bend = (1.0 + radius / s) / (1.0 + distance / s);
            // Outer Wilds' way (the default) needs no powers: this runs for every loose object every physics step.
            if (NearFalloff == 1.0) return g * radius / distance * bend;
            return g * Math.Pow(radius / distance, NearFalloff) * Math.Pow(bend, 2.0 - NearFalloff);
        }

        /// <summary>
        /// The true acceleration of a loose object at <paramref name="point"/> that is in the neighbourhood of
        /// body <paramref name="neighbourhood"/> (see <see cref="NeighbourhoodOf"/>).
        ///
        /// The rule, chosen to be easy to learn: deep inside a body's neighbourhood, only that body and its own
        /// moons pull on you, and you are carried along with the body as it travels. In open space (the Sun's
        /// neighbourhood) everything pulls on you.
        ///
        /// What is left out deep inside is the "tide": the difference between how hard the Sun pulls on you and how
        /// hard it pulls on the planet next to you. In a solar system shrunk this much, tides would be strong
        /// enough to drag a low orbit into the ground within minutes, which reads as the game being broken, not
        /// as physics. Without them an orbit round a planet is a clean, repeatable loop. In the outer part of
        /// the neighbourhood (beyond <see cref="CalmReach"/>) the outside world's pull is eased in, so that at the
        /// edge it is exactly what the next neighbourhood out would give, and crossing it is never a jolt.
        /// </summary>
        public Vec3d AccelerationAt(Vec3d point, BodyState[] states, int neighbourhood)
        {
            Vec3d inside = states[neighbourhood].Acceleration;
            Vec3d centre = states[neighbourhood].Position;
            for (int i = 0; i < _specs.Length; i++)
            {
                if (!IsSelfOrAncestor(neighbourhood, i)) continue;
                inside += GravityFrom(i, point, states);
                // A world rides its rails and is not pulled about by its own moons, so near it only the difference
                // a moon makes between here and the world's centre is felt (its tide). Feeling the whole of it would
                // tug every low orbit out of round once each time the moon came by.
                if (i != neighbourhood && neighbourhood != 0) inside -= GravityFrom(i, centre, states);
            }
            int parent = _parent[neighbourhood];
            if (parent < 0) return inside;
            double w = OutsideShare(neighbourhood, (point - states[neighbourhood].Position).Magnitude);
            if (w <= 0) return inside;
            return inside * (1.0 - w) + AccelerationAt(point, states, parent) * w;
        }

        /// <summary>
        /// How much of the outside world's pull is felt at <paramref name="distance"/> from body
        /// <paramref name="index"/>'s centre: 0 within <see cref="CalmReach"/> of its neighbourhood, rising
        /// smoothly to 1 at the edge. Always 0 for the Sun.
        /// </summary>
        public double OutsideShare(int index, double distance)
        {
            double edge = _influence[index];
            if (_parent[index] < 0 || double.IsInfinity(edge)) return 0;
            double from = edge * CalmReach;
            if (distance <= from) return 0;
            if (distance >= edge) return 1;
            double t = (distance - from) / (edge - from);
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>Speed of a circular orbit at <paramref name="distance"/> from the centre of a body.</summary>
        public double CircularOrbitSpeed(int index, double distance)
        {
            distance = Math.Max(1e-6, distance);
            return Math.Sqrt(PullStrength(index, distance) * distance);
        }

        // ------------------------------------------------------------------ whose neighbourhood is this?

        /// <summary>
        /// The body whose neighbourhood a point is in: the smallest one whose sphere of influence contains it
        /// (a moon wins over its planet, a planet over the Sun). <paramref name="current"/> is the answer from last
        /// time; leaving a neighbourhood takes a little extra distance, so the answer does not flicker at the edge.
        /// </summary>
        public int NeighbourhoodOf(Vec3d point, BodyState[] states, int current, double stickiness = 1.1)
        {
            int best = 0;
            bool deeper = true;
            while (deeper)
            {
                deeper = false;
                for (int i = 1; i < _specs.Length; i++)
                {
                    if (_parent[i] != best) continue;
                    double limit = _influence[i] * (IsSelfOrAncestor(i, current) ? stickiness : 1.0);
                    if ((states[i].Position - point).SqrMagnitude < limit * limit)
                    {
                        best = i;
                        deeper = true;
                        break;
                    }
                }
            }
            return best;
        }

        /// <summary>True if <paramref name="candidate"/> is <paramref name="body"/> or something it orbits.</summary>
        public bool IsSelfOrAncestor(int candidate, int body)
        {
            while (body >= 0)
            {
                if (body == candidate) return true;
                body = _parent[body];
            }
            return false;
        }
    }
}
