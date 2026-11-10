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
        public SolarSystemModel(IList<BodySpec> bodies, int seed)
        {
            if (bodies == null || bodies.Count == 0) throw new ArgumentException("A solar system needs at least one body.");
            Seed = seed;
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
                _influence[i] = Math.Max(Math.Max(s.radius * InfluenceRadii, s.radius + 80.0), reach[i] * 1.15);
                _lockRadius[i] = Math.Max(s.radius * 1.5, s.radius + 60.0);
                int parent = _parent[i];
                reach[parent] = Math.Max(reach[parent], s.orbitRadius + _influence[i]);
            }
        }

        /// <summary>A body's neighbourhood reaches at least this many of its own radii from its centre.</summary>
        public const double InfluenceRadii = 3.5;

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
            double radius = _specs[index].radius;
            // Above the surface: the real inverse-square law. Below it: fades evenly to nothing at the centre.
            double strength = distance >= radius
                ? _mu[index] / (distance * distance)
                : _specs[index].surfaceGravity * distance / radius;
            return to * (strength / distance);
        }

        /// <summary>
        /// The true acceleration of a loose object at <paramref name="point"/> that is in the neighbourhood of
        /// body <paramref name="neighbourhood"/> (see <see cref="NeighbourhoodOf"/>).
        ///
        /// The rule, chosen to be easy to learn: inside a body's neighbourhood, only that body and its own moons
        /// pull on you, and you are carried along with the body as it travels. In open space (the Sun's
        /// neighbourhood) the Sun and every planet and asteroid pull on you.
        ///
        /// What is left out is the "tide": the small difference between how hard the Sun pulls on you and how
        /// hard it pulls on the planet next to you. In a solar system shrunk this much, tides would be strong
        /// enough to drag a low orbit into the ground within minutes, which reads as the game being broken, not
        /// as physics. Without them an orbit round a planet is a clean, repeatable loop.
        /// </summary>
        public Vec3d AccelerationAt(Vec3d point, BodyState[] states, int neighbourhood)
        {
            Vec3d total = states[neighbourhood].Acceleration + GravityFrom(neighbourhood, point, states);
            for (int i = 1; i < _specs.Length; i++)
            {
                if (_parent[i] == neighbourhood) total += GravityFrom(i, point, states);
            }
            return total;
        }

        /// <summary>Speed of a circular orbit at <paramref name="distance"/> from the centre of a body.</summary>
        public double CircularOrbitSpeed(int index, double distance) => Math.Sqrt(_mu[index] / Math.Max(1e-6, distance));

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
