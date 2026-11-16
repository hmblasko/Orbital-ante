using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Enemies
{
    /// <summary>
    /// Standing in a cave network: "up" is away from the middle of the body, the floor is found by feel (the cave's
    /// walls are solid) or else by the network's own shape, strolling keeps to the camp's room, and going anywhere
    /// else follows the network's passages (<see cref="CaveNetwork.NextStep"/>) instead of walking into the rock.
    /// Places are remembered in the body's own space, like <see cref="PlanetFooting"/>.
    /// </summary>
    public sealed class CaveFooting : Footing
    {
        readonly Transform _body;
        readonly PlanetSurface _surface;
        readonly CaveNetwork _network;
        readonly int _room;
        Vector3 _routeFrom, _routeTo, _routeStep;
        float _routeAt = -10f;   // below 0: never planned, so the first plan is never refused

        public CaveFooting(Transform body, PlanetSurface surface, CaveNetwork network, int room)
        {
            _body = body;
            _surface = surface;
            _network = network;
            _room = room;
        }

        public CaveNetwork Network => _network;
        public int Room => _room;
        public override bool IsFlat => false;

        public override Vector3 Up(Vector3 at)
        {
            Vector3 up = at - _body.position;
            return up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
        }

        public override bool Ground(Vector3 at, Vector3 up, out Vector3 point)
        {
            if (base.Ground(at, up, out point)) return true;
            Vec3d local = SpaceConvert.ToVec3d(_body.InverseTransformPoint(at));
            if (_network.FeetAt(local, out Vec3d floor))
            {
                point = _body.TransformPoint(SpaceConvert.ToVector3(floor));
                return true;
            }
            // Out of the cave altogether (it walked out of a way in): the ground outside.
            Vec3d dir = local.Normalized;
            if (!_network.InsideAir(local, 1.5))
            {
                point = _body.TransformPoint(SpaceConvert.ToVector3(dir * _surface.GroundRadius(dir)));
                return true;
            }
            point = at;
            return false;
        }

        /// <summary>A spot on the floor of the camp's room, in world space.</summary>
        public Vector3 InRoom(double angle, double fraction)
        {
            return _body.TransformPoint(SpaceConvert.ToVector3(_network.RoomFloorPoint(_room, angle, fraction)));
        }

        public override Vector3 WanderPoint(SeededRandom rng, Bounds area, Vector3 from)
        {
            return InRoom(rng.NextDouble() * Mathf.PI * 2f, 0.15 + 0.7 * rng.NextDouble());
        }

        public override Vector3 Toward(Vector3 from, Vector3 destination)
        {
            // Kept in the body's own space, so the world turning under it or a recentre never makes it stale.
            Vector3 fromHere = _body.InverseTransformPoint(from);
            Vector3 toHere = _body.InverseTransformPoint(destination);
            bool reached = (_routeStep - fromHere).sqrMagnitude <= 0.8f * 0.8f;
            // Worked out again only when the step is reached, either end has moved a couple of metres or a second
            // has gone by: it is a search through the network, and every enemy in a fight asks every frame.
            bool fresh = Time.time - _routeAt < 1f && (fromHere - _routeFrom).sqrMagnitude < 4f && (toHere - _routeTo).sqrMagnitude < 4f;
            if (_routeAt >= 0f && (fresh && !reached || !TakePlan())) return _body.TransformPoint(_routeStep);
            _routeAt = Time.time;
            _routeFrom = fromHere;
            _routeTo = toHere;
            _routeStep = SpaceConvert.ToVector3(_network.NextStep(SpaceConvert.ToVec3d(fromHere), SpaceConvert.ToVec3d(toHere)));
            return _body.TransformPoint(_routeStep);
        }

        /// <summary>At most this many searches through any network a frame, shared by every enemy in every cave.</summary>
        public const int PlansPerFrame = 2;
        static int _planFrame = -1, _plansThisFrame;

        static bool TakePlan()
        {
            if (_planFrame != Time.frameCount) { _planFrame = Time.frameCount; _plansThisFrame = 0; }
            if (_plansThisFrame >= PlansPerFrame) return false;
            _plansThisFrame++;
            return true;
        }

        public override Vector3 Hold(Vector3 world) => _body.InverseTransformPoint(world);
        public override Vector3 Recall(Vector3 held) => _body.TransformPoint(held);
    }
}
