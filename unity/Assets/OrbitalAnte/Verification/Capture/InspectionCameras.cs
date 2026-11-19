using System.Collections.Generic;
using UnityEngine;

namespace OrbitalAnte.Verification.Capture
{
    public enum ViewDistance { Close, Medium, Wide }

    /// <summary>One standard way of looking at an asset.</summary>
    public struct InspectionView
    {
        public string Name;
        /// <summary>From the subject toward the camera, in the subject's own axes (z = its front).</summary>
        public Vector3 Direction;
        /// <summary>Flat-on views use a camera with no perspective, like a blueprint.</summary>
        public bool Orthographic;
        public InspectionView(string name, Vector3 direction, bool orthographic)
        {
            Name = name;
            Direction = direction.normalized;
            Orthographic = orthographic;
        }
    }

    /// <summary>
    /// The standard inspection viewpoints and distances. They are fixed here so the same asset is always looked
    /// at from the same places, which is what makes one review comparable with the next.
    /// </summary>
    public static class InspectionCameras
    {
        public const float PerspectiveFov = 32f;

        public static readonly InspectionView Front = new InspectionView("front", new Vector3(0, 0, 1), true);
        public static readonly InspectionView Rear = new InspectionView("rear", new Vector3(0, 0, -1), true);
        public static readonly InspectionView Left = new InspectionView("left", new Vector3(-1, 0, 0), true);
        public static readonly InspectionView Right = new InspectionView("right", new Vector3(1, 0, 0), true);
        public static readonly InspectionView FrontThreeQuarter = new InspectionView("front34", new Vector3(0.72f, 0.22f, 1), false);
        public static readonly InspectionView RearThreeQuarter = new InspectionView("rear34", new Vector3(-0.72f, 0.22f, -1), false);
        public static readonly InspectionView HighThreeQuarter = new InspectionView("high34", new Vector3(-0.8f, 1.05f, 1), false);
        public static readonly InspectionView Top = new InspectionView("top", new Vector3(0, 1, 0.001f), true);
        public static readonly InspectionView Underside = new InspectionView("underside", new Vector3(0, -1, 0.001f), true);

        /// <summary>The seven principal viewpoints (the eighth, the gameplay view, is taken with the game's own camera).</summary>
        public static readonly InspectionView[] Principal = { Front, Rear, Left, Right, FrontThreeQuarter, RearThreeQuarter, HighThreeQuarter };

        public struct Shot
        {
            public InspectionView View;
            public ViewDistance Distance;
            public Shot(InspectionView view, ViewDistance distance) { View = view; Distance = distance; }
        }

        /// <summary>
        /// Hero assets (characters, ships): every principal view at medium distance, three close-ups for surface
        /// and joint detail, two wide views for scale. Twelve pictures.
        /// </summary>
        public static List<Shot> HeroSet()
        {
            var shots = new List<Shot>();
            foreach (InspectionView v in Principal) shots.Add(new Shot(v, ViewDistance.Medium));
            shots.Add(new Shot(FrontThreeQuarter, ViewDistance.Close));
            shots.Add(new Shot(Left, ViewDistance.Close));
            shots.Add(new Shot(RearThreeQuarter, ViewDistance.Close));
            shots.Add(new Shot(FrontThreeQuarter, ViewDistance.Wide));
            shots.Add(new Shot(HighThreeQuarter, ViewDistance.Wide));
            return shots;
        }

        /// <summary>Background assets: a small representative set.</summary>
        public static List<Shot> BackgroundSet() => new List<Shot>
        {
            new Shot(FrontThreeQuarter, ViewDistance.Medium),
            new Shot(Left, ViewDistance.Medium),
            new Shot(HighThreeQuarter, ViewDistance.Wide),
            new Shot(FrontThreeQuarter, ViewDistance.Close),
        };

        /// <summary>The lightweight check: one beauty view and one flat side view.</summary>
        public static List<Shot> QuickSet() => new List<Shot>
        {
            new Shot(FrontThreeQuarter, ViewDistance.Medium),
            new Shot(Left, ViewDistance.Medium),
        };

        /// <summary>How much of the frame the subject's bounding ball fills at each distance (1 = touches the edges).</summary>
        public static float Fill(ViewDistance distance)
        {
            switch (distance)
            {
                case ViewDistance.Close: return 2.2f;   // the frame is smaller than the subject: detail
                case ViewDistance.Medium: return 0.82f; // the whole subject with a margin
                default: return 0.28f;                  // the subject in its surroundings
            }
        }

        /// <summary>The visible bounds of everything drawn under a root. Particle systems and disabled renderers are ignored.</summary>
        public static Bounds BoundsOf(Transform root)
        {
            bool any = false;
            var bounds = new Bounds(root.position, Vector3.zero);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        /// <summary>
        /// Puts a camera at a standard viewpoint. <paramref name="subjectRotation"/> says which way the subject
        /// faces; <paramref name="focus"/> is the point looked at (for close-ups this can be a detail, not the centre).
        /// </summary>
        public static void Place(Camera cam, Bounds bounds, Quaternion subjectRotation, InspectionView view, ViewDistance distance, float aspect, Vector3? focus = null)
        {
            float radius = Mathf.Max(0.01f, bounds.extents.magnitude);
            float fill = Fill(distance);
            Vector3 centre = focus ?? bounds.center;
            Vector3 direction = subjectRotation * view.Direction;
            // The upright frame is the limiting one on a wide screen; on a tall one the sideways frame is.
            float fit = Mathf.Min(1f, aspect);

            cam.orthographic = view.Orthographic;
            float range;
            if (view.Orthographic)
            {
                cam.orthographicSize = radius / (fill * fit);
                range = radius * 4f + 2f;
            }
            else
            {
                cam.fieldOfView = PerspectiveFov;
                range = radius / (Mathf.Sin(PerspectiveFov * 0.5f * Mathf.Deg2Rad) * fill * fit);
            }
            cam.transform.position = centre + direction * range;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction, subjectRotation * Vector3.up)) > 0.98f ? subjectRotation * Vector3.forward : subjectRotation * Vector3.up;
            cam.transform.rotation = Quaternion.LookRotation(-direction, up);
            cam.nearClipPlane = Mathf.Max(0.02f, (range - radius * 1.5f) * 0.2f);
            cam.farClipPlane = Mathf.Max(200f, range + radius * 60f);
        }
    }
}
