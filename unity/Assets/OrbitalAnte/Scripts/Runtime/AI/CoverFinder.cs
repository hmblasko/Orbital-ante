using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Enemies;
using UnityEngine;

namespace OrbitalAnte.AI
{
    /// <summary>A place to hide and a place to shoot from. Low cover shoots from where it hides (standing up over it).</summary>
    public struct CoverSpot
    {
        public Vector3 Hide;
        public Vector3 Peek;
        /// <summary>Hides only up to the chest: it shoots over the top, head showing.</summary>
        public bool Low;
    }

    /// <summary>What an enemy is looking for.</summary>
    public struct CoverQuery
    {
        /// <summary>Where the enemy stands (its feet).</summary>
        public Vector3 From;
        /// <summary>Where to look round: the enemy itself, or the place a flanker is heading for.</summary>
        public Vector3 Centre;
        public float Radius;
        /// <summary>The player's eye.</summary>
        public Vector3 Threat;
        public Footing Footing;
        /// <summary>The body's scale (heights and widths follow it).</summary>
        public float Size;
        /// <summary>It has a gun and needs somewhere to shoot from too.</summary>
        public bool NeedsPeek;
        /// <summary>Lying on the ground (lost a leg): hiding means keeping a low body out of sight.</summary>
        public bool Prone;
        public float KeepNear, KeepFar;
        /// <summary>A flanker's wanted direction from the player, along the ground; zero when it does not care.</summary>
        public Vector3 Bearing;
        public Squad Squad;
        public int Member;
        public float Spacing;
    }

    /// <summary>
    /// Finds cover by looking, not from a list: anything solid between the player and a spot counts, so rocks,
    /// trees, base buildings, the ship, crests of ground and cave walls are all cover, and cover that is shot
    /// down or felled (a broken base wall, a cut tree) simply stops hiding anyone. Places to try come from the
    /// solid things nearby (the far side of each from the player) and a ring of points round the centre; they
    /// are ranked by <see cref="CoverScore"/> after line checks against the world.
    ///
    /// Cost: one search a frame for every enemy in every place (<see cref="SearchesPerFrame"/>); a search
    /// stops after <see cref="MostTried"/> places, about 40 to 150 short line and shape checks.
    /// </summary>
    public static class CoverFinder
    {
        public const int SearchesPerFrame = 1;
        public const int MostTried = 10;
        const int RingPoints = 10;
        const int MostColliders = 24;
        /// <summary>Solid things bigger than this (ground tiles, cave blocks) give no "far side" spot; the ring still finds them.</summary>
        const float BiggestObject = 18f;

        static int _frame = -1, _searches;
        static readonly Collider[] Nearby = new Collider[48];
        static readonly List<Vector3> Places = new List<Vector3>(64);
        static readonly List<KeyValuePair<float, Vector3>> Ranked = new List<KeyValuePair<float, Vector3>>(64);

        /// <summary>True if a search may run this frame (and counts it).</summary>
        public static bool TakeSearch()
        {
            if (_frame != Time.frameCount) { _frame = Time.frameCount; _searches = 0; }
            if (_searches >= SearchesPerFrame) return false;
            _searches++;
            return true;
        }

        static float ChestHeight(in CoverQuery q) => (q.Prone ? 0.35f : 0.95f) * q.Size;
        static float HeadHeight(in CoverQuery q) => (q.Prone ? 0.5f : 1.55f) * q.Size;

        /// <summary>Nothing solid between two points, looking both ways (cave walls are solid from one side only).</summary>
        public static bool Clear(Vector3 a, Vector3 b) => MannequinCombat.ClearSight(a, b);

        /// <summary>The player can see this spot's chest (the cover there no longer hides anyone).</summary>
        public static bool Exposed(Vector3 hide, Vector3 threat, Footing footing, float size, bool prone)
        {
            Vector3 up = footing.Up(hide);
            return Clear(threat, hide + up * ((prone ? 0.35f : 0.95f) * size));
        }

        /// <summary>Looks for the best cover spot. Call only after <see cref="TakeSearch"/> said yes.</summary>
        public static bool Find(in CoverQuery q, out CoverSpot best)
        {
            best = default;
            Places.Clear();
            Ranked.Clear();
            Vector3 upHere = q.Footing.Up(q.Centre);

            // The far side of each solid thing nearby, straight behind it and round each edge a little.
            int count = Physics.OverlapSphereNonAlloc(q.Centre, q.Radius, Nearby, Layers.WorldMask, QueryTriggerInteraction.Ignore);
            int used = 0;
            for (int i = 0; i < count && used < MostColliders; i++)
            {
                Collider c = Nearby[i];
                if (c == null) continue;
                Bounds b = c.bounds;
                if (b.extents.magnitude > BiggestObject) continue;
                Vector3 away = Vector3.ProjectOnPlane(b.center - q.Threat, upHere);
                if (away.sqrMagnitude < 0.01f) continue;
                away.Normalize();
                used++;
                for (int turn = -1; turn <= 1; turn++)
                {
                    Vector3 dir = turn == 0 ? away : Quaternion.AngleAxis(turn * 35f, upHere) * away;
                    float reach = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.y) * b.extents.y + Mathf.Abs(dir.z) * b.extents.z;
                    Places.Add(b.center + dir * (reach + 0.55f * q.Size));
                }
            }

            // A ring round the centre: ground crests, cave walls and anything too big to have a far side.
            Vector3 side = Vector3.Cross(upHere, Mathf.Abs(Vector3.Dot(upHere, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right).normalized;
            for (int r = 0; r < 2; r++)
            {
                float far = q.Radius * (r == 0 ? 0.45f : 0.9f);
                for (int k = 0; k < RingPoints; k++)
                {
                    float a = (k + 0.5f * r) * Mathf.PI * 2f / RingPoints;
                    Places.Add(q.Centre + Quaternion.AngleAxis(a * Mathf.Rad2Deg, upHere) * side * far);
                }
            }
            if ((q.Centre - q.From).sqrMagnitude > 1f) Places.Add(q.Centre);

            // Cheap ranking first, as if every place were good cover; the line checks then go best first.
            for (int i = 0; i < Places.Count; i++)
            {
                Vector3 up = q.Footing.Up(Places[i]);
                if (!q.Footing.Ground(Places[i], up, out Vector3 g)) continue;
                Vector3 step = g - q.From;
                float rise = Vector3.Dot(step, up), along = Vector3.ProjectOnPlane(step, up).magnitude;
                if (Mathf.Abs(rise) > 1.2f + 0.55f * along) continue;   // a ledge or a pit: not walkable
                CoverCandidate c = Measure(q, g, up, along);
                c.ChestHidden = c.HeadHidden = c.CanPeek = true;
                float s = CoverScore.Of(c, q.NeedsPeek, q.KeepNear, q.KeepFar);
                if (float.IsInfinity(s)) continue;
                Ranked.Add(new KeyValuePair<float, Vector3>(s, g));
            }
            Ranked.Sort((x, y) => x.Key.CompareTo(y.Key));

            float bestScore = CoverScore.Rejected;
            int tried = 0, found = 0;
            for (int i = 0; i < Ranked.Count && tried < MostTried && found < 2; i++)
            {
                // Nothing further down the list can beat what is already found.
                if (Ranked[i].Key >= bestScore) break;
                tried++;
                Vector3 g = Ranked[i].Value;
                Vector3 up = q.Footing.Up(g);
                if (!TryPlace(q, g, up, out CoverSpot spot, out CoverCandidate c)) continue;
                float s = CoverScore.Of(c, q.NeedsPeek, q.KeepNear, q.KeepFar);
                if (float.IsInfinity(s)) continue;
                found++;
                if (s < bestScore)
                {
                    bestScore = s;
                    best = spot;
                }
            }
            return found > 0;
        }

        static CoverCandidate Measure(in CoverQuery q, Vector3 g, Vector3 up, float along)
        {
            var c = new CoverCandidate
            {
                Travel = along,
                ToThreat = Vector3.ProjectOnPlane(q.Threat - g, up).magnitude,
            };
            if (q.Bearing.sqrMagnitude > 0.01f)
            {
                Vector3 fromThreat = Vector3.ProjectOnPlane(g - q.Threat, up);
                c.OffBearing = fromThreat.sqrMagnitude > 0.01f ? Vector3.Angle(fromThreat, q.Bearing) : 90f;
            }
            if (q.Squad != null)
            {
                Vector3 held = q.Footing.Hold(g);
                c.Taken = q.Squad.Taken(held.x, held.y, held.z, q.Spacing, q.Member);
            }
            return c;
        }

        /// <summary>The line and shape checks for one place, cheapest and likeliest to fail first.</summary>
        static bool TryPlace(in CoverQuery q, Vector3 g, Vector3 up, out CoverSpot spot, out CoverCandidate c)
        {
            spot = default;
            c = Measure(q, g, up, Vector3.ProjectOnPlane(g - q.From, up).magnitude);
            // Hidden at the chest from the player.
            c.ChestHidden = !Clear(q.Threat, g + up * ChestHeight(q));
            if (!c.ChestHidden) return false;
            // Room to stand (or lie) there, and a clear way to walk to it.
            if (!RoomAt(q, g, up)) return false;
            float waist = (q.Prone ? 0.3f : 0.9f) * q.Size;
            Vector3 upFrom = q.Footing.Up(q.From);
            if (Physics.Linecast(q.From + upFrom * waist, g + up * waist, Layers.WorldMask, QueryTriggerInteraction.Ignore)
                && !(q.Footing is CaveFooting))   // in a cave the way follows the passages, not a straight line
                return false;

            Vector3 head = g + up * HeadHeight(q);
            c.HeadHidden = !Clear(q.Threat, head);
            spot.Hide = g;
            spot.Peek = g;
            if (!c.HeadHidden || q.Prone)
            {
                // Low cover: it shoots over the top from where it hides (a body on the ground just lies there).
                spot.Low = true;
                c.CanPeek = !c.HeadHidden;
                return true;
            }
            if (!q.NeedsPeek) return true;

            // Full cover: a short step out to one side or the other to shoot.
            Vector3 across = Vector3.Cross(up, Vector3.ProjectOnPlane(q.Threat - g, up)).normalized;
            for (int s = 0; s < 4 && !c.CanPeek; s++)
            {
                float sign = (s & 1) == 0 ? 1f : -1f, stepOut = (s < 2 ? 1.1f : 1.8f) * q.Size;
                Vector3 p = g + across * (sign * stepOut);
                Vector3 pUp = q.Footing.Up(p);
                if (!q.Footing.Ground(p, pUp, out Vector3 pg)) continue;
                if (Mathf.Abs(Vector3.Dot(pg - g, pUp)) > 0.8f) continue;
                if (!Clear(q.Threat, pg + pUp * HeadHeight(q))) continue;
                if (Physics.Linecast(g + up * waist, pg + pUp * waist, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                if (!RoomAt(q, pg, pUp)) continue;
                spot.Peek = pg;
                c.CanPeek = true;
            }
            return true;
        }

        static bool RoomAt(in CoverQuery q, Vector3 g, Vector3 up)
        {
            float r = 0.28f * q.Size;
            if (q.Prone) return !Physics.CheckSphere(g + up * (0.35f * q.Size), r, Layers.WorldMask, QueryTriggerInteraction.Ignore);
            return !Physics.CheckCapsule(g + up * (0.45f * q.Size), g + up * (1.6f * q.Size), r, Layers.WorldMask, QueryTriggerInteraction.Ignore);
        }
    }
}
