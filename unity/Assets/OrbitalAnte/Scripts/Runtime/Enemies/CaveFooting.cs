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
        float _routeAt = -10f;

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
            // Worked out again only when either end has moved a couple of metres or a second has gone by: it is a
            // search through the network, and every enemy in a fight asks every frame.
            if (Time.time - _routeAt < 1f && (from - _routeFrom).sqrMagnitude < 4f && (destination - _routeTo).sqrMagnitude < 4f)
                return (_routeStep - from).sqrMagnitude > 0.8f * 0.8f ? _routeStep : destination;
            _routeAt = Time.time;
            _routeFrom = from;
            _routeTo = destination;
            Vec3d a = SpaceConvert.ToVec3d(_body.InverseTransformPoint(from));
            Vec3d b = SpaceConvert.ToVec3d(_body.InverseTransformPoint(destination));
            _routeStep = _body.TransformPoint(SpaceConvert.ToVector3(_network.NextStep(a, b)));
            return _routeStep;
        }

        public override Vector3 Hold(Vector3 world) => _body.InverseTransformPoint(world);
        public override Vector3 Recall(Vector3 held) => _body.TransformPoint(held);
    }
}
