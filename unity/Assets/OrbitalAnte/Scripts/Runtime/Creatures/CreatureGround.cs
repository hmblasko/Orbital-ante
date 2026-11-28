using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// What a creature of the land stands on out on a world (<see cref="LandCreatures"/>): the ground as it is really
    /// built (<see cref="PlanetSurface.GroundRadius"/>, the triangles of the tile showing there, which are also what the
    /// player collides with). It is found without colliders, so a creature keeps to the ground however far from the
    /// player it is awake (colliders are only built near the player and the ship). "Up" is away from the world's
    /// middle, and places are kept in the world's own space, because the world can be moved under the player at any
    /// moment. In the practice range a creature has no footing of this kind and keeps the old rule (<see cref="CreatureGround"/>).
    /// </summary>
    public sealed class LandFooting : Footing
    {
        /// <summary>Ground this little above the water counts as its edge: a walker never steps on lower ground there.</summary>
        public const double DryMargin = 0.3;
        /// <summary>
        /// The slope a step is judged on is measured this far ahead along it (metres), never over one frame's run: where
        /// tiles of different detail meet, or where a tile is built again finer under a creature, the ground moves by
        /// centimetres, which over a frame's centimetres of run would read as a cliff.
        /// </summary>
        public const float SlopeRun = 0.5f;
        /// <summary>A step onto ground within this of a cave's air is refused (metres): the built ground spans the hole cut for its mouth.</summary>
        public const double CaveMargin = 0.5;

        readonly Transform _body;
        readonly PlanetSurface _surface;
        /// <summary>The sea's radius, metres from the world's middle, or 0 for a world with none.</summary>
        readonly double _sea;
        /// <summary>The ponds: each one's direction, how near its middle a direction must be to be over it (a cosine) and its top's radius.</summary>
        readonly Vec3d[] _pondAt;
        readonly double[] _pondCos, _pondTop;

        public LandFooting(Transform body, PlanetSurface surface)
        {
            _body = body;
            _surface = surface;
            PlanetTerrain terrain = surface != null ? surface.Terrain : null;
            _sea = terrain != null && terrain.HasSea ? surface.SurfaceRadius + terrain.SeaLevel : 0.0;
            int ponds = terrain != null ? terrain.Ponds.Count : 0;
            _pondAt = new Vec3d[ponds];
            _pondCos = new double[ponds];
            _pondTop = new double[ponds];
            for (int i = 0; i < ponds; i++)
            {
                Pond pond = terrain.Ponds[i];
                _pondAt[i] = pond.Direction.Normalized;
                _pondCos[i] = System.Math.Cos(System.Math.Min(System.Math.PI, pond.Radius / System.Math.Max(1.0, surface.SurfaceRadius)));
                _pondTop[i] = surface.SurfaceRadius + pond.Level;
            }
        }

        public override bool IsFlat => false;

        /// <summary>The world it stands on.</summary>
        public Transform World => _body;

        public override Vector3 Up(Vector3 at)
        {
            Vector3 up = at - _body.position;
            return up.sqrMagnitude > 1e-6f ? up.normalized : _body.up;
        }

        public override bool Ground(Vector3 at, Vector3 up, out Vector3 point)
        {
            point = OnTheGround(at);
            return true;
        }

        /// <summary>The point of the ground straight below (or above) a point, both in world space.</summary>
        public Vector3 OnTheGround(Vector3 at) => _body.TransformPoint(SpaceConvert.ToVector3(GroundPoint(SpaceConvert.ToVec3d(_body.InverseTransformPoint(at)))));

        /// <summary>The same in the world's own space, metres from its middle.</summary>
        public Vec3d GroundPoint(Vec3d inBody)
        {
            if (inBody.SqrMagnitude < 1e-12 || _surface == null) return inBody;
            Vec3d dir = inBody.Normalized;
            return dir * _surface.GroundRadius(dir);
        }

        /// <summary>How far from the world's middle the water's top is over a direction: the sea's, or a pond's where one lies there; 0 for none.</summary>
        public double WaterTop(Vec3d dir)
        {
            double top = _sea;
            for (int i = 0; i < _pondAt.Length; i++)
                if (_pondTop[i] > top && Vec3d.Dot(dir, _pondAt[i]) > _pondCos[i]) top = _pondTop[i];
            return top;
        }

        /// <summary>What a flier keeps above: the ground, or the water's top (the sea, a pond) where the ground is under it.</summary>
        public Vector3 UnderAFlier(Vector3 at)
        {
            Vec3d inBody = SpaceConvert.ToVec3d(_body.InverseTransformPoint(at));
            if (inBody.SqrMagnitude < 1e-12) return at;
            Vec3d dir = inBody.Normalized;
            double ground = _surface != null ? _surface.GroundRadius(dir) : inBody.Magnitude;
            return _body.TransformPoint(SpaceConvert.ToVector3(dir * System.Math.Max(ground, WaterTop(dir))));
        }

        /// <summary>How far a point on the ground is above the water there, the sea or a pond, metres (a large number where there is none).</summary>
        public double AboveWater(Vector3 onTheGround)
        {
            Vec3d inBody = SpaceConvert.ToVec3d(_body.InverseTransformPoint(onTheGround));
            if (inBody.SqrMagnitude < 1e-12) return 1e6;
            double top = WaterTop(inBody.Normalized);
            return top <= 0.0 ? 1e6 : inBody.Magnitude - top;
        }

        public override Vector3 Hold(Vector3 world) => _body.InverseTransformPoint(world);
        public override Vector3 Recall(Vector3 held) => _body.TransformPoint(held);

        /// <summary>
        /// Whether a step from <paramref name="from"/> (on the ground) to <paramref name="to"/> can be taken, and if so
        /// <paramref name="to"/> is put on the ground there. Refused by the ground (<see cref="StepResult.Ground"/>): ground
        /// ahead steeper than it walks (<see cref="LandStep.TooSteep"/>, over <see cref="SlopeRun"/> and against the world's
        /// up, so a creature leaning with a slope still feels it); further into the water; a cave's mouth. Stopped by a
        /// wall (<see cref="StepResult.Wall"/>): something standing on the ground there higher than <paramref name="stepOver"/>
        /// (a rock, a crate, a base's floor).
        /// </summary>
        public StepResult StepOnto(Vector3 from, ref Vector3 to, float steepest, float stepOver)
        {
            Vector3 ground = OnTheGround(to), up = Up(ground);
            Vector3 way = Vector3.ProjectOnPlane(to - from, up);
            if (way.sqrMagnitude > 1e-10f)
            {
                Vector3 d = OnTheGround(from + way.normalized * SlopeRun) - from;
                float rise = Vector3.Dot(d, up), run = (d - up * rise).magnitude;
                if (LandStep.TooSteep(rise, run, steepest)) return StepResult.Ground;
            }
            // Looked for from a little over head height down to its step above the ground: whatever it finds stands in
            // the way. (The ground's own collider is never reached, and a tall thing the line starts inside is the wall probe's.)
            if (Physics.Raycast(ground + up * 3f, -up, 3f - Mathf.Clamp(stepOver, 0f, 2.5f), Layers.WorldMask, QueryTriggerInteraction.Ignore)) return StepResult.Wall;
            // Never further into the water (the sea, a pond); out of it, always (so one that finds itself at the edge can walk out).
            double there = AboveWater(ground);
            if (there < DryMargin && there < AboveWater(from)) return StepResult.Ground;
            // Never into a cave's mouth: the ground found spans the hole cut for it.
            if (_surface != null && _surface.InCave(SpaceConvert.ToVec3d(_body.InverseTransformPoint(ground)), CaveMargin)) return StepResult.Ground;
            to = ground;
            return StepResult.Taken;
        }
    }

    /// <summary>
    /// The ground rules every creature of the land shares, the same in the practice range as they always were, and on a
    /// world through its <see cref="LandFooting"/>: where it looks for a wall, whether a step can be taken, keeping it on
    /// the ground and upright round the world.
    /// </summary>
    public static class CreatureGround
    {
        /// <summary>Something standing on the ground higher than this is a wall to a walking creature (the practice range's old rule).</summary>
        public const float StepOver = 0.3f;

        /// <summary>
        /// Where to test for a wall <paramref name="height"/> above the ground at a point: above the point itself in the
        /// practice range, above the ground under it on a world, so a slope is never taken for a wall.
        /// </summary>
        public static Vector3 Above(Footing footing, Vector3 at, Vector3 up, float height)
        {
            if (footing is LandFooting land)
            {
                Vector3 ground = land.OnTheGround(at);
                return ground + land.Up(ground) * height;
            }
            return at + up * height;
        }

        /// <summary>
        /// Whether a step to <paramref name="to"/> can be taken, and if so <paramref name="to"/> is put on the ground there.
        /// In the practice range: the ground found from above, and a step up or down of more than <see cref="StepOver"/>
        /// is a wall. On a world: see <see cref="LandFooting.StepOnto"/>, with <paramref name="stepOver"/> for what it steps over.
        /// </summary>
        public static StepResult Step(Footing footing, Vector3 from, ref Vector3 to, Vector3 up, float steepest, float stepOver = StepOver)
        {
            if (footing is LandFooting land) return land.StepOnto(from, ref to, steepest, stepOver);
            if (Physics.Raycast(to + up * 3f, -up, out RaycastHit ground, 9f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                // A step up or down of more than a shin is a wall to it (its feet are placed on level ground there).
                if (Mathf.Abs(Vector3.Dot(ground.point - to, up)) > StepOver) return StepResult.Wall;
                to = ground.point;
            }
            return StepResult.Taken;
        }

        /// <summary>
        /// On a world, once a frame after it moves: back onto the ground under it (a tile can be built again finer as the
        /// player comes near) and upright to the world. With <paramref name="leanMost"/> over 0 a long, low body lies along
        /// the ground under it instead, never more than that many radians from upright, eased in. Nothing in the practice range.
        /// </summary>
        public static void Keep(Footing footing, Transform root, float halfLength = 0f, float halfWidth = 0f, float leanMost = 0f, float dt = 0f)
        {
            if (!(footing is LandFooting land)) return;
            Vector3 at = land.OnTheGround(root.position);
            root.position = at;
            Upright(land, root, at, halfLength, halfWidth, leanMost, dt);
        }

        /// <summary>
        /// A flier's root (its height is in its pose): eased toward what is below it, the ground or the sea's top, at most
        /// <paramref name="pace"/> metres a second, so flying over a cliff is not a jump. True while it still has some way to go.
        /// In the practice range the ground is found from above, as it always was.
        /// </summary>
        public static bool Settle(Footing footing, Transform root, float height, float pace, float dt)
        {
            Vector3 up = root.up, at = root.position, below;
            bool world = footing is LandFooting;
            if (footing is LandFooting land)
            {
                below = land.UnderAFlier(at);
                up = land.Up(below);
            }
            else
            {
                if (!Physics.Raycast(at + up * (height + 3f), -up, out RaycastHit ground, height + 15f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
                below = ground.point;
            }
            float rise = Vector3.Dot(below - at, up), step = Mathf.Clamp(rise, -pace * dt, pace * dt);
            // On a world the ground can rise faster than that under a flier (a cliff band, a canyon wall): its root climbs
            // with it as far as it must for its body never to be drawn inside the ground, half a metre clear.
            if (world) step = Mathf.Max(step, rise - Mathf.Max(0f, height - 0.5f));
            root.position = at + up * step;
            return Mathf.Abs(rise - step) > 0.02f;
        }

        /// <summary>Upright to a world (no lean), for a creature in the air: eased, so taking off from a slope is not a snap.</summary>
        public static void Upright(Footing footing, Transform root, float dt)
        {
            if (footing is LandFooting land) Upright(land, root, root.position, 0f, 0f, 0f, dt);
        }

        static void Upright(LandFooting land, Transform root, Vector3 at, float halfLength, float halfWidth, float leanMost, float dt)
        {
            Vector3 radial = land.Up(at), wanted = radial;
            if (leanMost > 0f)
            {
                Vector3 f = root.forward, r = root.right;
                Vector3 along = land.OnTheGround(at + f * halfLength) - land.OnTheGround(at - f * halfLength);
                Vector3 across = land.OnTheGround(at + r * halfWidth) - land.OnTheGround(at - r * halfWidth);
                Vec3d ground = LandStep.PlaneUp(SpaceConvert.ToVec3d(along), SpaceConvert.ToVec3d(across), SpaceConvert.ToVec3d(radial));
                wanted = SpaceConvert.ToVector3(LandStep.Lean(SpaceConvert.ToVec3d(radial), ground, leanMost));
            }
            // Eased in (when it is given the time), so a bump under one end or a take-off from a hillside is not a jerk;
            // but never left leaning past what it can.
            if (dt > 0f)
            {
                wanted = Vector3.Slerp(root.up, wanted, 1f - Mathf.Exp(-10f * dt));
                float most = Mathf.Max(leanMost, Vector3.Angle(root.up, radial) * Mathf.Deg2Rad);
                if (Vector3.Angle(wanted, radial) > most * Mathf.Rad2Deg) wanted = Vector3.RotateTowards(radial, wanted, most, 0f);
            }
            Vector3 up = root.up;
            if (Vector3.Dot(up, wanted) > 0.9999999f) return;
            root.rotation = Quaternion.FromToRotation(up, wanted) * root.rotation;
        }
    }

    /// <summary>
    /// The ground's height under a walker's feet, in walker space (its root's own), for its animator
    /// (<see cref="WalkerAnimInput.Ground"/>): one per walker on a world, given its place once a frame.
    /// </summary>
    public sealed class GroundUnder
    {
        readonly LandFooting _land;
        Vec3d _at, _right, _up, _forward;

        /// <summary>The function the animator asks: the height of the ground at walker-space (x, z). Made once.</summary>
        public readonly System.Func<double, double, double> Height;

        public GroundUnder(LandFooting land)
        {
            _land = land;
            Height = HeightAt;
        }

        /// <summary>Takes the walker's place and turn (once a frame, before it is posed).</summary>
        public void Frame(Transform root)
        {
            Transform world = _land.World;
            _at = SpaceConvert.ToVec3d(world.InverseTransformPoint(root.position));
            _right = SpaceConvert.ToVec3d(world.InverseTransformDirection(root.right));
            _up = SpaceConvert.ToVec3d(world.InverseTransformDirection(root.up));
            _forward = SpaceConvert.ToVec3d(world.InverseTransformDirection(root.forward));
        }

        double HeightAt(double x, double z)
        {
            Vec3d ground = _land.GroundPoint(_at + _right * x + _forward * z);
            return Vec3d.Dot(ground - _at, _up);
        }
    }
}
