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
    /// Cost: one search a frame for every enemy in every place (<see cref="SearchesPerFrame"/>). A search finds the
    /// ground under at most <see cref="MostGrounded"/> places (one ray each) and line-checks at most
    /// <see cref="MostTried"/> of them (up to about 14 checks each): typically 40 to 80 checks, at worst about 160.
    /// </summary>
    public static class CoverFinder
    {
        public const int SearchesPerFrame = 1;
        public const int MostTried = 10;
        const int RingPoints = 10;
        const int MostColliders = 24;
        /// <summary>Solid things bigger than this (ground tiles) give no "far side" spot; the ring still finds them.
        /// In a cave no solid thing gives one (the wall blocks are about 16 m and their far side is rock).</summary>
        const float BiggestObject = 18f;
        /// <summary>Places that get the ground found under them and a first ranking.</summary>
        const int MostGrounded = 14;

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

            var cave = q.Footing as CaveFooting;
            // The far side of each solid thing nearby, straight behind it and round each edge a little.
            int count = cave != null ? 0 : Physics.OverlapSphereNonAlloc(q.Centre, q.Radius, Nearby, Layers.WorldMask, QueryTriggerInteraction.Ignore);
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

            // Cheap ranking first, as if every place were good cover, on the raw places (no casts); then the ground
            // is found under the best few, ranked again, and the line checks go best first.
            for (int i = 0; i < Places.Count; i++)
            {
                Vector3 up = q.Footing.Up(Places[i]);
                CoverCandidate c = Measure(q, Places[i], up, Vector3.ProjectOnPlane(Places[i] - q.From, up).magnitude);
                c.ChestHidden = c.HeadHidden = c.CanPeek = true;
                float s = CoverScore.Of(c, q.NeedsPeek, q.KeepNear, q.KeepFar);
                if (float.IsInfinity(s)) continue;
                Ranked.Add(new KeyValuePair<float, Vector3>(s, Places[i]));
            }
            Ranked.Sort((x, y) => x.Key.CompareTo(y.Key));
            int grounded = 0;
            for (int i = 0; i < Ranked.Count && grounded < MostGrounded; i++)
            {
                Vector3 p = Ranked[i].Value;
                Vector3 up = q.Footing.Up(p);
                if (!q.Footing.Ground(p, up, out Vector3 g)) continue;
                Vector3 step = g - q.From;
                float rise = Vector3.Dot(step, up), along = Vector3.ProjectOnPlane(step, up).magnitude;
                // A ledge or a pit: not walkable. In a cave, never another level, and only where the cave's own
                // shape says it is a straight walk (the way there then follows the passages).
                if (Mathf.Abs(rise) > (cave != null ? 1.0f : 1.2f + 0.55f * along)) continue;
                if (cave != null && !cave.WalkClear(q.From, g)) continue;
                CoverCandidate c = Measure(q, g, up, along);
                c.ChestHidden = c.HeadHidden = c.CanPeek = true;
                float s = CoverScore.Of(c, q.NeedsPeek, q.KeepNear, q.KeepFar);
                if (float.IsInfinity(s)) continue;
                // Places is no longer needed for the raw points (Ranked holds copies), so the grounded ones go there.
                Places[grounded++] = g;
            }
            Ranked.Clear();
            for (int i = 0; i < grounded; i++)
            {
                Vector3 g = Places[i], up = q.Footing.Up(g);
                CoverCandidate c = Measure(q, g, up, Vector3.ProjectOnPlane(g - q.From, up).magnitude);
                c.ChestHidden = c.HeadHidden = c.CanPeek = true;
                Ranked.Add(new KeyValuePair<float, Vector3>(CoverScore.Of(c, q.NeedsPeek, q.KeepNear, q.KeepFar), g));
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
            for (int s = 0; s < 2 && !c.CanPeek; s++)
            {
                float sign = s == 0 ? 1f : -1f, stepOut = 1.4f * q.Size;
                Vector3 p = g + across * (sign * stepOut);
                Vector3 pUp = q.Footing.Up(p);
                if (!q.Footing.Ground(p, pUp, out Vector3 pg)) continue;
                if (Mathf.Abs(Vector3.Dot(pg - g, pUp)) > 0.8f) continue;
                if (!Clear(q.Threat, pg + pUp * HeadHeight(q))) continue;
                if (Physics.Linecast(g + up * waist, pg + pUp * waist, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                if (q.Footing is CaveFooting cf && !cf.WalkClear(g, pg)) continue;
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
