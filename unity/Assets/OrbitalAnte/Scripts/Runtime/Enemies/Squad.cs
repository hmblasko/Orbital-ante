namespace OrbitalAnte.Enemies
{
    /// <summary>
    /// What the members of one camp share. Two rules, both there to keep a fight readable:
    ///
    /// 1. Turns to shoot. Only a few of a squad fire at any moment; the rest move, close in or wait. A big
    ///    squad is dangerous because it keeps coming and comes from several sides, not because eight guns
    ///    go off at once (which no player could read or answer).
    /// 2. What one sees, all know. If any member has seen the player lately, the whole squad knows where to
    ///    look. Nobody outside the squad does.
    ///
    /// Plain C#, no engine. Times are game seconds.
    /// </summary>
    public sealed class Squad
    {
        readonly float[] _turnEnds;
        float _lastSaw = float.NegativeInfinity;

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
            if (now > _lastSaw) _lastSaw = now;
        }

        /// <summary>True if any member saw the player within this many seconds.</summary>
        public bool SawWithin(float now, float seconds) => now - _lastSaw <= seconds;
    }
}
