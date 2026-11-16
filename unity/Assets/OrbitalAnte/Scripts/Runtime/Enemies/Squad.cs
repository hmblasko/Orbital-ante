using System.Collections.Generic;

namespace OrbitalAnte.Enemies
{
    /// <summary>What a member of a squad is doing in a fight.</summary>
    public enum SquadRole
    {
        /// <summary>Fights from the front, from cover: keeps the player's attention.</summary>
        Hold,
        /// <summary>Goes round the player's side while the others hold them (side in <see cref="Squad.SideOf"/>).</summary>
        Flank,
    }

    /// <summary>
    /// What the members of one camp share. Two rules, both there to keep a fight readable:
    ///
    /// 1. Turns to shoot. Only a few of a squad fire at any moment; the rest move, close in or wait. A big
    ///    squad is dangerous because it keeps coming and comes from several sides, not because eight guns
    ///    go off at once (which no player could read or answer).
    /// 2. What one sees, all know. If any member has seen the player lately, the whole squad knows where to
    ///    look. Nobody outside the squad does.
    /// 3. Hold and flank (Matthew, 16 Nov 2026). After a squad has fought face on for a moment, one member
    ///    (two in a squad of four or more, one each side) is sent round the player's side while the rest hold
    ///    from cover. The keenest go first (quick kinds that rush); snipers and walkers-down never go. A flanker
    ///    who falls is replaced only after a pause, so flanks come in waves you can learn, not a stream.
    /// 4. Spots. Members say where they are and which cover spot they took, so two never crowd one rock.
    ///
    /// Plain C#, no engine. Times are game seconds.
    /// </summary>
    public sealed class Squad
    {
        readonly float[] _turnEnds;
        float _lastSaw = float.NegativeInfinity;
        float _engagedSince = float.NegativeInfinity;
        float _refillAt = float.NegativeInfinity;
        /// <summary>A gap longer than this between sightings starts a new fight (and new flanking plans).</summary>
        public const float NewFightGap = 10f;

        sealed class Member
        {
            public bool Active, CanFlank, Done;
            public int Eagerness;
            public SquadRole Role;
            public int Side;
            public float RoleSince;
            public bool HasPos, HasSpot;
            public float X, Y, Z, SpotX, SpotY, SpotZ;
        }

        readonly List<Member> _members = new List<Member>();

        /// <summary>How many of a squad of this size may be shooting at once.</summary>
        public static int ShootersFor(int members) => members >= 6 ? 3 : members >= 2 ? 2 : 1;

        public Squad(int members)
        {
            _turnEnds = new float[ShootersFor(members)];
            for (int i = 0; i < _turnEnds.Length; i++) _turnEnds[i] = float.NegativeInfinity;
        }

        public int Shooters => _turnEnds.Length;

        /// <summary>Asks for a turn to shoot lasting this long. False if every turn is taken: wait and ask again.</summary>
        public bool TakeTurn(float now, float seconds)
        {
            for (int i = 0; i < _turnEnds.Length; i++)
            {
                if (_turnEnds[i] > now) continue;
                _turnEnds[i] = now + (seconds > 0f ? seconds : 0f);
                return true;
            }
            return false;
        }

        /// <summary>How many turns are taken right now.</summary>
        public int TurnsTaken(float now)
        {
            int n = 0;
            for (int i = 0; i < _turnEnds.Length; i++) if (_turnEnds[i] > now) n++;
            return n;
        }

        /// <summary>A member can see the player now.</summary>
        public void Saw(float now)
        {
            if (now - _lastSaw > NewFightGap) StartNewFight(now);
            if (now > _lastSaw) _lastSaw = now;
        }

        /// <summary>When the fight going on now began (the first sighting after a long gap).</summary>
        public float EngagedSince => _engagedSince;

        void StartNewFight(float now)
        {
            _engagedSince = now;
            _refillAt = float.NegativeInfinity;
            for (int i = 0; i < _members.Count; i++)
            {
                _members[i].Role = SquadRole.Hold;
                _members[i].Done = false;
                _members[i].Side = 0;
            }
        }

        // ------------------------------------------------------------------ members and roles

        /// <summary>Adds a member and returns its number in this squad.</summary>
        /// <param name="canFlank">The kind may ever be sent round the side.</param>
        /// <param name="eagerness">Keener members are sent first (see TacticsRules.Eagerness).</param>
        public int Join(bool canFlank, int eagerness)
        {
            _members.Add(new Member { Active = true, CanFlank = canFlank, Eagerness = eagerness });
            return _members.Count - 1;
        }

        /// <summary>Living members still in the fight.</summary>
        public int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _members.Count; i++) if (_members[i].Active) n++;
                return n;
            }
        }

        /// <summary>How many may be out flanking at once in a squad with this many fighting.</summary>
        public static int FlankersFor(int active) => active >= 4 ? 2 : active >= 2 ? 1 : 0;

        bool Valid(int id) => id >= 0 && id < _members.Count;

        /// <summary>A member is out of the fight for good (dead, or nothing left to fight with). A flanker's place
        /// is filled again only after <paramref name="refillDelay"/> seconds.</summary>
        public void Leave(int id, float now, float refillDelay)
        {
            if (!Valid(id) || !_members[id].Active) return;
            Member m = _members[id];
            if (m.Role == SquadRole.Flank && !m.Done) _refillAt = now + refillDelay;
            m.Active = false;
            m.Role = SquadRole.Hold;
            m.HasSpot = false;
        }

        /// <summary>Whether this member may be sent round the side from now on (lost a leg: no longer).</summary>
        public void SetCanFlank(int id, bool canFlank, float now, float refillDelay)
        {
            if (!Valid(id)) return;
            Member m = _members[id];
            if (m.CanFlank == canFlank) return;
            m.CanFlank = canFlank;
            if (!canFlank && m.Role == SquadRole.Flank)
            {
                if (!m.Done) _refillAt = now + refillDelay;
                m.Role = SquadRole.Hold;
                m.Side = 0;
            }
        }

        /// <summary>
        /// What this member should be doing now. Hold until the squad has been in this fight for
        /// <paramref name="flankAfter"/> seconds; then the flanking places are handed out, keenest first.
        /// </summary>
        /// <param name="preferredSide">-1 or +1: the side of the squad this member is already on, so it goes round its own side.</param>
        public SquadRole RoleOf(int id, float now, float flankAfter, int preferredSide)
        {
            if (!Valid(id) || !_members[id].Active) return SquadRole.Hold;
            if (!SawWithin(now, NewFightGap) || now - _engagedSince < flankAfter) return _members[id].Role;
            Assign(now, id, preferredSide);
            return _members[id].Role;
        }

        /// <summary>Which way round a flanker goes: -1 or +1 (0 for a member who holds).</summary>
        public int SideOf(int id) => Valid(id) ? _members[id].Side : 0;

        /// <summary>The flanker got there (or gave up): it keeps its place, so no one else is sent that way this fight.</summary>
        public void FlankDone(int id)
        {
            if (Valid(id) && _members[id].Role == SquadRole.Flank) _members[id].Done = true;
        }

        /// <summary>True once this member's flank is over.</summary>
        public bool IsFlankDone(int id) => Valid(id) && _members[id].Done;

        /// <summary>When this member was given its role.</summary>
        public float RoleSince(int id) => Valid(id) ? _members[id].RoleSince : 0f;

        void Assign(float now, int asking, int preferredSide)
        {
            int slots = FlankersFor(ActiveCount);
            int taken = 0, left = 0, right = 0;
            for (int i = 0; i < _members.Count; i++)
            {
                Member m = _members[i];
                if (!m.Active || m.Role != SquadRole.Flank) continue;
                taken++;
                if (m.Side < 0) left++; else right++;
            }
            if (taken >= slots || now < _refillAt) return;

            // The keenest member still holding (ties: whoever joined first), and only it, is sent.
            int best = -1;
            for (int i = 0; i < _members.Count; i++)
            {
                Member m = _members[i];
                if (!m.Active || !m.CanFlank || m.Role != SquadRole.Hold) continue;
                if (best < 0 || m.Eagerness > _members[best].Eagerness) best = i;
            }
            if (best != asking) return;

            Member chosen = _members[best];
            int side = preferredSide < 0 ? -1 : 1;
            // Go round the side nobody is on yet.
            if (side < 0 && left > 0) side = 1;
            else if (side > 0 && right > 0) side = -1;
            chosen.Role = SquadRole.Flank;
            chosen.Side = side;
            chosen.Done = false;
            chosen.RoleSince = now;
        }

        // ------------------------------------------------------------------ where members are

        /// <summary>Where this member is (in whatever space the squad's footing keeps places in).</summary>
        public void Report(int id, float x, float y, float z)
        {
            if (!Valid(id)) return;
            Member m = _members[id];
            m.X = x; m.Y = y; m.Z = z;
            m.HasPos = true;
        }

        /// <summary>The middle of the members who hold (the squad's front). False if none has said where it is.</summary>
        public bool FrontCentre(out float x, out float y, out float z)
        {
            x = y = z = 0f;
            int n = 0;
            for (int pass = 0; pass < 2 && n == 0; pass++)
                for (int i = 0; i < _members.Count; i++)
                {
                    Member m = _members[i];
                    // Holders first; if every one left is flanking, all of them.
                    if (!m.Active || !m.HasPos || (pass == 0 && m.Role != SquadRole.Hold)) continue;
                    x += m.X; y += m.Y; z += m.Z;
                    n++;
                }
            if (n == 0) return false;
            x /= n; y /= n; z /= n;
            return true;
        }

        /// <summary>This member takes a cover spot.</summary>
        public void Claim(int id, float x, float y, float z)
        {
            if (!Valid(id)) return;
            Member m = _members[id];
            m.SpotX = x; m.SpotY = y; m.SpotZ = z;
            m.HasSpot = true;
        }

        public void Unclaim(int id)
        {
            if (Valid(id)) _members[id].HasSpot = false;
        }

        /// <summary>True if another member's spot is within <paramref name="spacing"/> metres of this point.</summary>
        public bool Taken(float x, float y, float z, float spacing, int exceptId)
        {
            float s2 = spacing * spacing;
            for (int i = 0; i < _members.Count; i++)
            {
                if (i == exceptId) continue;
                Member m = _members[i];
                if (!m.Active || !m.HasSpot) continue;
                float dx = m.SpotX - x, dy = m.SpotY - y, dz = m.SpotZ - z;
                if (dx * dx + dy * dy + dz * dz < s2) return true;
            }
            return false;
        }

        /// <summary>True if any member saw the player within this many seconds.</summary>
        public bool SawWithin(float now, float seconds) => now - _lastSaw <= seconds;
    }
}
