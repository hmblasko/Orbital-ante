using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Enemies
{
    /// <summary>
    /// Standing on a planet: "up" is away from the middle of the body, places are remembered in the body's own
    /// space (the world can be moved under the player at any moment), and the ground is found by feel where it
    /// is solid and by the terrain's own height where it is not (ground is only solid near the player).
    /// </summary>
    public sealed class PlanetFooting : Footing
    {
        readonly Transform _body;
        readonly PlanetSurface _surface;
        readonly Vector3 _home;   // in the body's space
        readonly float _roam;

        public PlanetFooting(Transform body, PlanetSurface surface, Vector3 homeInBodySpace, float roam)
        {
            _body = body;
            _surface = surface;
            _home = homeInBodySpace;
            _roam = roam;
        }

        Vector3 _floorCentre;   // in the body's space
        float _floorRadius;

        /// <summary>A level floor (a base's) that counts as the ground wherever it is above the terrain.</summary>
        public void SetFloor(Vector3 centreInBodySpace, float radius)
        {
            _floorCentre = centreInBodySpace;
            _floorRadius = radius;
        }

        public override bool IsFlat => false;

        public override Vector3 Up(Vector3 at)
        {
            Vector3 up = at - _body.position;
            return up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
        }

        public override bool Ground(Vector3 at, Vector3 up, out Vector3 point)
        {
            if (base.Ground(at, up, out point)) return true;
            point = OnTheGround(_body.InverseTransformPoint(at));
            return true;
        }

        /// <summary>The point on the terrain straight below (or above) a point given in the body's space, in world space.</summary>
        public Vector3 OnTheGround(Vector3 inBodySpace)
        {
            Vec3d dir = SpaceConvert.ToVec3d(inBodySpace).Normalized;
            double ground = _surface.GroundRadius(dir);
            if (_floorRadius > 0f)
            {
                // Where the line from the body's middle meets the floor's flat top.
                Vector3 floorUp = _floorCentre.normalized, d = SpaceConvert.ToVector3(dir);
                float facing = Vector3.Dot(d, floorUp);
                if (facing > 0.5f)
                {
                    Vector3 onFloor = d * (_floorCentre.magnitude / facing);
                    if ((onFloor - _floorCentre).sqrMagnitude < _floorRadius * _floorRadius && onFloor.magnitude > ground) return _body.TransformPoint(onFloor);
                }
            }
            return _body.TransformPoint(SpaceConvert.ToVector3(dir * ground));
        }

        public override Vector3 WanderPoint(SeededRandom rng, Bounds area, Vector3 from)
        {
            Vector3 up = _home.normalized;
            Vector3 side = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 along = Vector3.Cross(up, side);
            float angle = rng.Range(0f, Mathf.PI * 2f), far = _roam * Mathf.Sqrt(rng.NextFloat());
            return OnTheGround(_home + (side * Mathf.Cos(angle) + along * Mathf.Sin(angle)) * far);
        }

        public override Vector3 Hold(Vector3 world) => _body.InverseTransformPoint(world);
        public override Vector3 Recall(Vector3 held) => _body.TransformPoint(held);
    }
}
