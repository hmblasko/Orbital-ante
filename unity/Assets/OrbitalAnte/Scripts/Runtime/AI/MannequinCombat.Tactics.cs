using OrbitalAnte.Dismemberment;
using OrbitalAnte.Enemies;
using UnityEngine;

namespace OrbitalAnte.AI
{
    /// <summary>
    /// Cover and flanking (Matthew, 16 Nov 2026: "enemies try to take cover" and "try to flank").
    ///
    /// Cover: an armed enemy in a fight goes to a spot the player cannot see (rocks, trees, base buildings, the
    /// ship, a crest of ground, a cave wall: <see cref="CoverFinder"/>), then takes turns hiding and looking out to
    /// shoot. Low cover is shot over, head showing; full cover is shot round, a short step out. It ducks back to
    /// reload and while the squad's turns to shoot are all taken, so a squad fires in turns from cover. Each look
    /// out has its own beat before the first shot. Cover that stops hiding it (shot away, or the player walked
    /// round it) is left for another.
    ///
    /// Flank: in a camp's squad, once the fight has gone on a moment, one or two members are sent wide round the
    /// player's side (see <see cref="Squad"/>), heading for cover there. A flanker does not shoot on the way; once
    /// there it fights from that cover like the rest.
    ///
    /// Kinds: the Bruiser never hides or flanks (it walks you down); the Marksman hides but never flanks, and a
    /// lookout on a tower keeps its post; the Runner is the keenest flanker. One that has lost a leg drags itself
    /// to cover close by and stops being sent round the side.
    /// </summary>
    public sealed partial class MannequinCombat
    {
        TacticsRules _tactics;
        int _member = -1;

        // The cover spot (places kept through the footing, so they survive the world moving under the player).
        bool _hasCover;
        CoverSpot _cover;
        Vector3 _coverThreat;
        float _coverSince, _coverRetryAt;
        bool _atCover, _wantPeek, _coverHolding;
        float _coverPhaseEnd, _lastExposedCheck, _peekBlindSince = -1f;
        float _myTurnUntil = -1f;

        // A flank under way.
        bool _flanking;
        Vector3 _flankGoal, _flankVia;
        bool _flankHasVia, _flankToCover, _flankFirstLook;
        float _flankPlannedAt;

        // Cover for one with no gun (only hiding).
        bool _hasHide;
        Vector3 _hide;
        float _hideAt = -100f;

        TacticsRules Tactics
        {
            get
            {
                if (_tactics == null) _tactics = EnemyConfig.Load().tactics ?? new TacticsRules();
                return _tactics;
            }
        }

        bool KindUsesCover => TacticsRules.UsesCover(_cfg.holdsStill, _cfg.closesIn, _cfg.rushes);
        bool KindCanFlank => TacticsRules.CanFlank(_cfg.holdsStill, _cfg.closesIn, _cfg.rushes) && _loco.Wander;
        float Size => Mathf.Max(0.3f, transform.lossyScale.y);
        Footing FootingNow => _owner.Footing ?? Footing.Flat;

        /// <summary>Joins the squad, says where it is and whether it can still be sent round the side.</summary>
        void TickSquad(MovementState state, float now)
        {
            if (Squad == null) return;
            if (_member < 0) _member = Squad.Join(KindCanFlank, TacticsRules.Eagerness(_cfg.rushes));
            Vector3 held = FootingNow.Hold(transform.position);
            Squad.Report(_member, held.x, held.y, held.z);
            bool canFlank = KindCanFlank && Weapon != null && state == MovementState.Intact;
            Squad.SetCanFlank(_member, canFlank, now, Tactics.flankRefill);
        }

        /// <summary>Asks the squad for a turn to shoot, remembering its own (so it does not duck for its own turn).</summary>
        bool TakeSquadTurn(float now, float seconds)
        {
            if (!Squad.TakeTurn(now, seconds)) return false;
            _myTurnUntil = now + seconds;
            return true;
        }

        void LeaveSquad()
        {
            if (Squad != null && _member >= 0) Squad.Leave(_member, Time.time, Tactics.flankRefill);
        }

        /// <summary>Lets go of the cover spot and of anything it set on the legs for it.</summary>
        void DropCover()
        {
            _hasCover = false;
            _atCover = false;
            _flanking = false;
            _peekBlindSince = -1f;
            if (_coverHolding && _loco != null)
            {
                _loco.Hold = false;
                _loco.LookAt = null;
            }
            _coverHolding = false;
            if (Squad != null && _member >= 0) Squad.Unclaim(_member);
        }

        void SetCoverHold(bool on, Vector3? lookAt)
        {
            if (on)
            {
                _loco.Hold = true;
                _loco.LookAt = lookAt;
                _coverHolding = true;
            }
            else if (_coverHolding)
            {
                _loco.Hold = false;
                _loco.LookAt = null;
                _coverHolding = false;
            }
        }

        /// <summary>
        /// The armed fight from cover and the flank. Returns false when they do not apply (no cover found, a kind
        /// that does not use them, nobody aware of the player), and the ordinary fight goes on as before.
        /// </summary>
        bool ThinkTactics(MovementState state, bool sees, float distance, float now)
        {
            TacticsRules t = Tactics;
            bool onFeet = state == MovementState.Intact || state == MovementState.OneLeg;
            bool prone = state == MovementState.Crawl || state == MovementState.Wriggle;
            bool aware = sees || now - _lastSaw <= t.waitInCover || (Squad != null && Squad.SawWithin(now, 1.5f));
            if (!aware || !_loco.Wander || (!onFeet && !prone))
            {
                if (_hasCover || _flanking) DropCover();
                return false;
            }

            Vector3 eye = CombatWorld.PlayerEye.position;
            _heardAt = null; _sentUntil = -1000f;
            SetHeardBeat(false, null);

            // Sent round the side?
            if (Squad != null && t.flank && onFeet && _member >= 0)
            {
                SquadRole role = Squad.RoleOf(_member, now, t.flankAfter, SideOfFront(eye), t.nextWave);
                if (role == SquadRole.Flank && !Squad.IsFlankDone(_member) && Flank(eye, distance, sees, now)) return true;
            }

            if (!t.cover || !KindUsesCover)
            {
                // Cover switched off mid-fight: let go of the hold it may have set, or the enemy stays rooted.
                if (_hasCover) DropCover();
                return false;
            }
            // The quick ones close in first and look for cover only once near.
            if (_cfg.closesIn && distance > _cfg.keepFar * 1.5f && !_hasCover) return false;

            if (NeedsNewCover(eye, now) && now >= _coverRetryAt && CoverFinder.TakeSearch())
            {
                var q = BaseQuery(eye, transform.position, prone ? t.crawlCoverSearch : t.coverSearch, prone);
                if (CoverFinder.Find(q, out CoverSpot spot)) TakeCover(spot, eye, now);
                else
                {
                    if (_hasCover) DropCover();
                    _coverRetryAt = now + 2f;   // nothing here: do not search again every think
                }
            }
            if (!_hasCover) return false;
            // Each look out from cover has its own beat before a shot, a little shorter than a first sighting (it
            // knows where the player is); a flanker's first look gets a longer one. Never an instant shot from cover.
            if (sees && !_seesPlayer)
            {
                _seesPlayer = true;
                _reactAt = now + (_flankFirstLook ? ReactionTime + t.flankFirstShotDelay : ReactionTime * 0.7f);
                _flankFirstLook = false;
            }
            else if (!sees) _seesPlayer = false;
            FightFromCover(eye, sees, prone, now);
            return true;
        }

        CoverQuery BaseQuery(Vector3 eye, Vector3 centre, float radius, bool prone)
        {
            return new CoverQuery
            {
                From = transform.position,
                Centre = centre,
                Radius = radius,
                Threat = eye,
                Footing = FootingNow,
                Size = Size,
                // Armed, even on the ground: a spot it cannot shoot from leaves a downed enemy safe and harmless.
                NeedsPeek = true,
                Prone = prone,
                KeepNear = _cfg.rushes ? Mathf.Max(_cfg.keepNear, Tactics.rusherCoverNearest) : _cfg.keepNear,
                KeepFar = Mathf.Min(_cfg.keepFar, _cfg.engageRange),
                Squad = Squad,
                Member = _member,
                Spacing = Tactics.spacing,
            };
        }

        void TakeCover(CoverSpot spot, Vector3 eye, float now)
        {
            Footing f = FootingNow;
            _cover = new CoverSpot { Hide = f.Hold(spot.Hide), Peek = f.Hold(spot.Peek), Low = spot.Low };
            _coverThreat = f.Hold(eye);
            _coverSince = now;
            _hasCover = true;
            _atCover = false;
            _peekBlindSince = -1f;
            SetCoverHold(false, null);
            if (Squad != null && _member >= 0) Squad.Claim(_member, _cover.Hide.x, _cover.Hide.y, _cover.Hide.z);
        }

        bool NeedsNewCover(Vector3 eye, float now)
        {
            if (!_hasCover) return true;
            Footing f = FootingNow;
            TacticsRules t = Tactics;
            if ((f.Recall(_coverThreat) - eye).sqrMagnitude > t.threatMoved * t.threatMoved) return true;
            if (now - _coverSince > t.keepSpot) return true;
            // Out looking for a while and the player is nowhere to be seen from the peek: they have moved.
            if (_peekBlindSince > 0f && now - _peekBlindSince > 1.5f) return true;
            // The cover stopped hiding it (shot away, felled, or the player has come round it).
            if (_atCover && !_wantPeek && now - _lastExposedCheck > 0.5f)
            {
                _lastExposedCheck = now;
                bool prone = _owner.State == MovementState.Crawl || _owner.State == MovementState.Wriggle;
                if (CoverFinder.Exposed(f.Recall(_cover.Hide), eye, f, Size, prone)) return true;
            }
            return false;
        }

        void FightFromCover(Vector3 eye, bool sees, bool prone, float now)
        {
            TacticsRules t = Tactics;
            Footing f = FootingNow;
            Vector3 hide = f.Recall(_cover.Hide), peek = f.Recall(_cover.Peek);
            _loco.Scramble = false;

            if (!_atCover)
            {
                // On the way: hunched, quick, not shooting.
                SetCoverHold(false, null);
                _loco.FacePoint = null;
                _loco.Destination = hide;
                _loco.Hurry = true;
                _loco.Tactical = _cfg.tacticalHunch;
                if (Flat(hide - transform.position).sqrMagnitude < 0.7f * 0.7f)
                {
                    _atCover = true;
                    _wantPeek = false;
                    // A short first wait, so arriving is not a long sulk.
                    _coverPhaseEnd = now + Rand(0.3f, 0.8f);
                }
                return;
            }

            if (now >= _coverPhaseEnd)
            {
                _wantPeek = !_wantPeek;
                // A heavy shot is lined up for a second first: it stays out long enough to finish it, or the
                // tell would glow and nothing come (which would teach the player the tell lies).
                float leastOut = _cfg.aimTell > 0f ? ReactionTime * 0.7f + _cfg.aimTell + 0.5f : 0f;
                _coverPhaseEnd = now + (_wantPeek ? Mathf.Max(leastOut, Rand(t.peekMin, t.peekMax)) : Rand(t.hideMin, t.hideMax));
            }
            bool reloading = Weapon == null || Weapon.State.Reloading || Weapon.State.Ammo == 0;
            bool turnsFull = Squad != null && Squad.TurnsTaken(now) >= Squad.Shooters && now >= _myTurnUntil;
            bool peeking = prone ? sees : CoverRhythm.Peeking(_wantPeek, reloading, turnsFull);
            if (turnsFull && _wantPeek && !reloading) _coverPhaseEnd = Mathf.Max(_coverPhaseEnd, now + 0.4f);

            if (!peeking)
            {
                // Hidden: low (as low as it goes), still, eyes toward where the player was.
                _peekBlindSince = -1f;
                _loco.FacePoint = null;
                _loco.Hurry = false;
                _loco.Tactical = prone ? 0f : 1f;
                if (Flat(hide - transform.position).sqrMagnitude > 0.6f * 0.6f)
                {
                    SetCoverHold(false, null);
                    _loco.Destination = hide;
                }
                else
                {
                    _loco.Destination = null;
                    SetCoverHold(true, eye);
                }
                return;
            }

            // Out to shoot: over the top of low cover, or a step out from full cover.
            SetCoverHold(false, null);
            _loco.Tactical = _cover.Low ? 0f : _cfg.tacticalHunch;
            _loco.Hurry = false;
            Vector3 at = _cover.Low ? hide : peek;
            if (Flat(at - transform.position).sqrMagnitude > 0.5f * 0.5f && !prone)
            {
                _loco.FacePoint = null;
                _loco.Destination = at;
                return;
            }
            _loco.Destination = null;
            _loco.FacePoint = eye;
            if (sees) _peekBlindSince = -1f;
            else if (_peekBlindSince < 0f) _peekBlindSince = now;
        }

        /// <summary>
        /// Going round the player's side: wide, by a point part of the way round, to cover there if there is any.
        /// Returns false once the flank is over (got there, gave up, or ran into the player) and it fights as usual.
        /// </summary>
        bool Flank(Vector3 eye, float distance, bool sees, float now)
        {
            TacticsRules t = Tactics;
            float since = now - Squad.RoleSince(_member);
            // Ran into the player close up, or taking too long: fight from here.
            if (since > t.flankGiveUp || (sees && distance < _cfg.keepNear))
            {
                EndFlank(false);
                return false;
            }
            Footing f = FootingNow;
            if (!_flanking || now - _flankPlannedAt > 2f) PlanFlank(eye, distance, now);
            if (!_flanking)
            {
                EndFlank(false);
                return false;
            }

            Vector3 goal = f.Recall(_flankGoal);
            Vector3 target = goal;
            if (_flankHasVia)
            {
                Vector3 via = f.Recall(_flankVia);
                if (Flat(via - transform.position).sqrMagnitude < 2f * 2f) _flankHasVia = false;
                else target = via;
            }
            if (!_flankHasVia && Flat(goal - transform.position).sqrMagnitude < 0.8f * 0.8f)
            {
                // There. If the goal was cover, it is now this one's cover; it fights from it with the rest.
                EndFlank(_flankToCover);
                if (_flankToCover)
                {
                    _hasCover = true;
                    _atCover = true;
                    _wantPeek = false;
                    _coverSince = now;
                    _coverThreat = f.Hold(eye);
                    _coverPhaseEnd = now + Rand(0.2f, 0.5f);
                }
                return false;
            }

            SetCoverHold(false, null);
            _seesPlayer = false;          // no shooting on the move round
            _loco.FacePoint = null;
            _loco.Destination = target;
            _loco.Hurry = true;
            _loco.Scramble = false;
            _loco.Tactical = _cfg.tacticalHunch;
            return true;
        }

        void EndFlank(bool intoCover)
        {
            _flanking = false;
            // Only a flank that ends in cover there shoots first from that cover, with the longer beat.
            _flankFirstLook = intoCover;
            if (!intoCover && Squad != null && _member >= 0) Squad.Unclaim(_member);
            if (Squad != null && _member >= 0) Squad.FlankDone(_member);
        }

        void PlanFlank(Vector3 eye, float distance, float now)
        {
            Footing f = FootingNow;
            _flankPlannedAt = now;
            Vector3 up = f.Up(eye);
            Vector3 front = FrontDirection(eye, up);
            if (front.sqrMagnitude < 0.01f) { _flanking = false; return; }
            int side = Squad.SideOf(_member);
            if (side == 0) side = 1;
            TacticsRules t = Tactics;
            float range = Mathf.Clamp(distance, _cfg.keepNear + 3f, Mathf.Max(_cfg.keepNear + 3f, Mathf.Min(_cfg.keepFar, 22f)));

            // Try the full angle first; in tight places (a cave room) a smaller one.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                float angle = t.flankAngle * (attempt == 0 ? 1f : 0.55f) * side;
                Vector3 bearing = Quaternion.AngleAxis(angle, up) * front;
                Vector3 rough = eye + bearing * range;
                Vector3 roughUp = f.Up(rough);
                if (!f.Ground(rough, roughUp, out Vector3 goal)) continue;
                if (Mathf.Abs(Vector3.Dot(goal - transform.position, roughUp)) > 6f) continue;
                // In a cave, only a place in this part of the cave (not the ground above, nor another level).
                if (f is CaveFooting cf && (Mathf.Abs(Vector3.Dot(goal - transform.position, roughUp)) > 1.5f || !cf.WalkClear(eye - roughUp * 1.5f, goal))) continue;

                _flankToCover = false;
                bool wantsCover = t.cover && KindUsesCover;
                bool searching = wantsCover && CoverFinder.TakeSearch();
                if (wantsCover && !searching && _flanking)
                {
                    // No search free this frame: keep the plan it has and try again on the next think.
                    _flankPlannedAt = now - 2f;
                    return;
                }
                if (searching)
                {
                    if (Squad != null) Squad.Unclaim(_member);
                    var q = BaseQuery(eye, goal, 8f, false);
                    q.Bearing = bearing;
                    if (CoverFinder.Find(q, out CoverSpot spot))
                    {
                        _cover = new CoverSpot { Hide = f.Hold(spot.Hide), Peek = f.Hold(spot.Peek), Low = spot.Low };
                        if (Squad != null) Squad.Claim(_member, _cover.Hide.x, _cover.Hide.y, _cover.Hide.z);
                        goal = spot.Hide;
                        _flankToCover = true;
                    }
                }
                _flankGoal = f.Hold(goal);

                // Wide round, not across the player's line of fire: by a point half way round, a little further out.
                Vector3 mine = Flat(transform.position - eye, up);
                float swing = mine.sqrMagnitude > 0.01f ? Vector3.SignedAngle(mine, bearing, up) : 0f;
                _flankHasVia = Mathf.Abs(swing) > 50f;
                if (_flankHasVia)
                {
                    Vector3 half = Quaternion.AngleAxis(swing * 0.5f, up) * mine.normalized;
                    Vector3 v = eye + half * (range * 1.25f);
                    if (f.Ground(v, f.Up(v), out Vector3 vg)) _flankVia = f.Hold(vg);
                    else _flankHasVia = false;
                }
                _flanking = true;
                _hasCover = false;
                _atCover = false;
                return;
            }
            _flanking = false;
        }

        /// <summary>Which way from the player the squad's front lies, along the ground.</summary>
        Vector3 FrontDirection(Vector3 eye, Vector3 up)
        {
            Vector3 centre = transform.position;
            if (Squad != null && Squad.FrontCentre(out float x, out float y, out float z))
                centre = FootingNow.Recall(new Vector3(x, y, z));
            Vector3 front = Flat(centre - eye, up);
            if (front.sqrMagnitude < 0.25f) front = Flat(transform.position - eye, up);
            return front.sqrMagnitude > 0.01f ? front.normalized : Vector3.zero;
        }

        /// <summary>-1 or +1: which side of the line from the player to the squad's front this one stands.</summary>
        int SideOfFront(Vector3 eye)
        {
            Vector3 up = FootingNow.Up(eye);
            Vector3 front = FrontDirection(eye, up);
            if (front.sqrMagnitude < 0.01f) return (_member & 1) == 0 ? 1 : -1;
            float s = Vector3.Dot(Vector3.Cross(up, front), transform.position - eye);
            if (Mathf.Abs(s) < 0.5f) return (_member & 1) == 0 ? 1 : -1;
            return s > 0f ? 1 : -1;
        }

        /// <summary>A spot to hide in for one with no gun to shoot back with. Looked for at most every two seconds.</summary>
        Vector3? HideFromPlayer()
        {
            if (!Tactics.cover || !CombatWorld.PlayerPresent) return null;
            float now = Time.time;
            Vector3 eye = CombatWorld.PlayerEye.position;
            Footing f = FootingNow;
            bool stale = now - _hideAt > 2f;   // a search that found nothing waits too
            if (stale && CoverFinder.TakeSearch())
            {
                _hideAt = now;
                bool prone = _owner.State == MovementState.Crawl || _owner.State == MovementState.Wriggle;
                var q = BaseQuery(eye, transform.position, prone ? Tactics.crawlCoverSearch : Tactics.coverSearch, prone);
                q.NeedsPeek = false;
                q.KeepNear = 0f;
                _hasHide = CoverFinder.Find(q, out CoverSpot spot);
                if (_hasHide) _hide = f.Hold(spot.Hide);
            }
            return _hasHide ? f.Recall(_hide) : (Vector3?)null;
        }

        Vector3 Flat(Vector3 v) => Vector3.ProjectOnPlane(v, transform.up);
        static Vector3 Flat(Vector3 v, Vector3 up) => Vector3.ProjectOnPlane(v, up);
    }
}
