using System.Collections.Generic;
using OrbitalAnte.AI;
using OrbitalAnte.Ante;
using OrbitalAnte.Bases;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Enemies;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Tasks
{
    /// <summary>
    /// The ante tasks in the solar system (Matthew, 20 Nov 2026; batch 1 of the plan "Ante tasks plan"). There is
    /// no task menu: every task is a thing you can see from where you are, and you choose which to do and how.
    ///
    ///  - Kills score by kind, with style feats (headshot, airborne, rover, melee, a gun not used lately, all four
    ///    limbs off) and a streak of quick kills. A world's marked target (a red light over one guard) is worth more.
    ///  - Clear a base: every guard down. Quiet (no guard saw you) and quick are feats.
    ///  - Signal masts near some bases call every enemy in reach onto you every so often until all their cells are
    ///    shot out or a rover rams them.
    ///  - Salvage crates near bases, carried (or driven) back aboard the ship.
    ///  - Supply drops fall near you during an ante: race the enemies for it, then take a Focus refill (press) or
    ///    the gun ladder's next gun (hold).
    ///  - First landing on each world, and hard landings on rough ground (soft ones are a feat).
    ///
    /// A tall light stands over the nearest few tasks not done yet. Rules are in <see cref="TaskRules"/> and the
    /// points go through <see cref="AnteLoop.Report"/>. Lives on the solar world and goes with it.
    /// </summary>
    public sealed class TaskDirector : MonoBehaviour, IWorldOffer
    {
        SolarSystemWorld _world;
        AnteLoop _loop;
        TaskConfig _config;
        TaskRules _rules;
        AnteBalance _balance;
        GunFreshness _fresh;
        DropClock _dropClock;

        // ------------------------------------------------------------------ per world
        sealed class WorldTasks
        {
            public int Body;
            public string Id;
            public PlanetSurface Surface;
            public readonly List<MastTask> Masts = new List<MastTask>();
            public readonly List<CrateTask> Crates = new List<CrateTask>();
            public readonly List<BaseTask> Bases = new List<BaseTask>();
            public int MarkCamp = -1, MarkMember = -1;
        }

        abstract class Placed
        {
            public WorldTasks World;
            public Vec3d Dir;
            public GameObject View;
            public Beacon Light;
            public float SnapAt;
            public Vector3 WorldPoint => World.Surface.Body.transform.TransformPoint(SpaceConvert.ToVector3(Dir * World.Surface.GroundRadius(Dir)));
            public abstract bool Live { get; }
        }

        sealed class MastTask : Placed
        {
            public int Index;
            public SignalMast Rule;
            public Renderer[] Cells;
            public Collider[] CellColliders;
            public override bool Live => !Rule.Down;
        }

        sealed class CrateTask : Placed
        {
            public int Index;
            public readonly SalvageCrate Rule = new SalvageCrate();
            public Collider Box;
            public override bool Live => Rule.State == CrateState.Lying;
        }

        sealed class BaseTask : Placed
        {
            public int Index;
            public BaseSite Site;
            public BaseClear Rule;
            public bool Done;
            public override bool Live => !Done;
        }

        sealed class DropTask : Placed
        {
            public int Number, Ante;
            public SupplyDrop Rule;
            public override bool Live => Rule.State == DropState.Falling || Rule.State == DropState.Landed;
        }

        readonly Dictionary<int, WorldTasks> _worlds = new Dictionary<int, WorldTasks>();
        readonly List<DropTask> _drops = new List<DropTask>();

        // ------------------------------------------------------------------ kills
        struct HitRecord
        {
            public float At;
            public KillFacts Facts;
            public bool Killed;
        }

        struct WokeRecord
        {
            public int Body, Camp, Member;
            public MannequinCombat Combat;
        }

        readonly Dictionary<Mannequin, HitRecord> _hits = new Dictionary<Mannequin, HitRecord>();
        readonly Dictionary<Mannequin, WokeRecord> _woke = new Dictionary<Mannequin, WokeRecord>();
        readonly HashSet<Mannequin> _marked = new HashSet<Mannequin>();
        readonly List<Mannequin> _scratch = new List<Mannequin>();

        // ------------------------------------------------------------------ carrying, opening, landing
        CrateTask _carried;
        DropTask _holdingDrop;
        float _held;
        bool _wasLanded = true;
        float _lastTouchSpeed;
        float _quietUntil;
        readonly HashSet<string> _noFirstLanding = new HashSet<string>();

        static readonly System.Predicate<Mannequin> IsGone = m => m == null;
        float _slowAt;
        readonly List<Placed> _candidates = new List<Placed>();
        readonly List<float> _distances = new List<float>();
        bool _raising;

        float Now => _loop != null ? _loop.PlayTime : Time.time;
        bool Scoring => _loop != null && _loop.Running;

        public void Init(SolarSystemWorld world, AnteLoop loop)
        {
            _world = world;
            _loop = loop;
            _config = loop != null ? loop.Config : TaskConfig.Load();
            _rules = _config.rules;
            _balance = loop != null && loop.Run != null ? loop.Run.Balance : AnteBalanceData.Load().balance;
            _fresh = new GunFreshness(_rules.freshGunMemory);
            _dropClock = new DropClock(_rules);
            var problems = _rules.Problems();
            if (problems.Count > 0) Debug.LogWarning("Task rules: " + string.Join(" ", problems));
            // A run that starts on the pad has already "landed" on Earth.
            if (world.StartedOnPad) _noFirstLanding.Add("Earth");
            // The first seconds of a run settle the ship onto its legs: not a landing.
            _quietUntil = Time.time + 3f;
            if (loop != null)
            {
                loop.AnteBegan += OnAnteBegan;
                if (loop.Running) _dropClock.AnteStarted(loop.PlayTime);
            }
        }

        void OnEnable()
        {
            CombatEvents.PlayerHitSomething += OnPlayerHit;
            EnemySpawner.Down += OnEnemyDown;
            EnemySpawner.Woke += OnEnemyWoke;
            CombatWorld.Alarm += OnAlarm;
            WorldOffers.Add(this);
        }

        void OnDisable()
        {
            CombatEvents.PlayerHitSomething -= OnPlayerHit;
            EnemySpawner.Down -= OnEnemyDown;
            EnemySpawner.Woke -= OnEnemyWoke;
            CombatWorld.Alarm -= OnAlarm;
            WorldOffers.Remove(this);
            if (_loop != null) _loop.AnteBegan -= OnAnteBegan;
            Astronaut.CarryPace = 1f;
            Astronaut a = Player;
            if (a != null && a.Armour != null) a.Armour.Damaged -= OnArmourDamaged;
        }

        void OnAnteBegan(int ante) => _dropClock.AnteStarted(_loop.PlayTime);

        Astronaut Player => _world != null ? _world.Astronaut : null;

        // ================================================================== scoring helpers

        ScoreResult Score(ScoreEventKind kind, string claim, int feats, string label, int missionPoints = 0)
        {
            if (!Scoring) return default;
            var e = new ScoreEvent { claimId = claim, kind = kind, skillFeats = feats, missionBasePoints = missionPoints };
            return AnteLoop.Report(e, label);
        }

        /// <summary>A task finished while a long streak was running gets one more feat, and the streak is spent.</summary>
        int StreakFeat(ref string label)
        {
            Streak streak = _loop != null ? _loop.Streak : null;
            if (streak == null || streak.CountAt(Now) < _rules.streakForFeat) return 0;
            streak.Spend(Now);
            label = string.IsNullOrEmpty(label) ? "ON A STREAK" : label + " + ON A STREAK";
            return 1;
        }

        static string Join(string what, string feats) => string.IsNullOrEmpty(feats) ? what : what + "  " + feats;

        // ================================================================== kills

        void OnPlayerHit(HitOutcome outcome)
        {
            if (!outcome.Enemy || !(outcome.Target is Mannequin who) || who == null) return;
            Astronaut player = Player;
            var facts = new KillFacts
            {
                headshot = outcome.HeadshotKill,
                airborne = player != null && player.Airborne,
                rover = outcome.Source == HitSource.Rover,
                melee = outcome.Source == HitSource.Melee,
            };
            if (outcome.Killed)
            {
                string gun = facts.rover ? "rover" : facts.melee ? "melee"
                           : player != null && player.Weapons != null && player.Weapons.Current != null && player.Weapons.Current.Data != null
                               ? player.Weapons.Current.Data.Id : "gun";
                facts.freshGun = _fresh.IsFresh(gun);
                _fresh.Remember(gun);
                facts.takenApart = AllFourLimbsOff(who);
            }
            _hits[who] = new HitRecord { At = Now, Facts = facts, Killed = outcome.Killed };

            // A guard of a base was engaged: its clear's clock starts.
            if (_woke.TryGetValue(who, out WokeRecord w) && _world.Enemies != null)
            {
                BaseTask b = BaseFor(w.Body, _world.Enemies.BaseOfCamp(w.Body, w.Camp));
                if (b != null) b.Rule.Engaged(Now);
            }
        }

        static bool AllFourLimbsOff(Mannequin who)
        {
            BodyModel m = who.Model;
            return m != null && m.Group(BodyPartId.LeftHand) != 0 && m.Group(BodyPartId.RightHand) != 0
                   && m.Group(BodyPartId.LeftFoot) != 0 && m.Group(BodyPartId.RightFoot) != 0;
        }

        void OnEnemyWoke(int body, int camp, int member, Mannequin who)
        {
            if (who == null) return;
            _woke[who] = new WokeRecord { Body = body, Camp = camp, Member = member, Combat = who.GetComponent<MannequinCombat>() };
            WorldTasks w = WorldFor(body);
            if (w != null && w.MarkCamp == camp && w.MarkMember == member && _rules.markedTargets) Mark(who);
        }

        void OnEnemyDown(EnemySpawner.EnemyDown e)
        {
            Mannequin who = e.Who;
            bool marked = who != null && _marked.Remove(who);
            if (who != null && _hits.TryGetValue(who, out HitRecord hit) && Now - hit.At <= 20f)
            {
                _hits.Remove(who);
                KillFacts f = hit.Facts;
                int feats;
                string label;
                ScoreEventKind kind;
                if (marked)
                {
                    kind = ScoreEventKind.EnemyElite;
                    feats = (f.takenApart ? 1 : 0) + (f.melee ? 1 : 0);
                    label = f.takenApart && f.melee ? "TAKEN APART + MELEE" : f.takenApart ? "TAKEN APART" : f.melee ? "MELEE" : string.Empty;
                    label = Join("MARKED TARGET", label);
                }
                else
                {
                    kind = EnemyTiers.KindFor(e.TypeId, e.Scale);
                    feats = StyleKill.Feats(f, out string style);
                    label = string.IsNullOrEmpty(style) ? "KILL" : style;
                }
                int streak = _loop != null ? _loop.Streak.Add(Now) : 0;
                if (streak >= 3 && streak % 3 == 0) Audio.AudioDirector.PlayLocal("reward.multiplier", 0.6f);
                Score(kind, TaskIds.Enemy(e.BodyId, e.Camp, e.Member), feats, label);
            }
            if (who != null) _woke.Remove(who);

            // A base's last guard: cleared, whoever finished it.
            if (e.BaseIndex >= 0 && _world.Enemies != null && _world.Enemies.BaseGuards(e.Body, e.BaseIndex, out int guards, out int left) && left == 0 && guards > 0)
            {
                BaseTask b = BaseFor(e.Body, e.BaseIndex);
                if (b != null && !b.Done)
                {
                    b.Done = true;
                    int feats = b.Rule.Feats(_rules, Now, out string label);
                    feats += StreakFeat(ref label);
                    Audio.AudioDirector.PlayLocal("reward.tier3");
                    Score(ScoreEventKind.OptionalMission, TaskIds.BaseClear(e.BodyId, e.BaseIndex), feats,
                        Join("BASE CLEARED", label), _rules.BaseClearPoints(e.BaseSize));
                    if (b.Light != null) b.Light.Show(false);
                }
            }
        }

        void Mark(Mannequin who)
        {
            if (!_marked.Add(who)) return;
            Transform head = who.Rig != null && who.Rig.Head != null ? who.Rig.Head : who.transform;
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "MarkedTargetLight";
            Destroy(glow.GetComponent<Collider>());
            var r = glow.GetComponent<Renderer>();
            r.sharedMaterial = Mat.Unlit(new Color(1f, 0.18f, 0.12f) * 2.4f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float scale = Mathf.Max(0.1f, head.lossyScale.y);
            glow.transform.SetParent(head, false);
            glow.transform.localPosition = Vector3.up * (0.42f / scale);
            glow.transform.localScale = Vector3.one * (0.16f / scale);
        }

        void OnAlarm(Vector3 at, float radius, Vector3 playerAt, string faction)
        {
            if (_raising) return;
            WorldTasks w = Here(out _);
            if (w == null) return;
            for (int i = 0; i < w.Bases.Count; i++)
            {
                BaseTask b = w.Bases[i];
                if (b.Done) continue;
                float reach = (float)b.Site.Footprint + 40f;
                if ((b.WorldPoint - at).sqrMagnitude <= reach * reach) b.Rule.Alarm();
            }
        }

        // ================================================================== worlds

        WorldTasks WorldFor(int body) => _worlds.TryGetValue(body, out WorldTasks w) ? w : null;

        BaseTask BaseFor(int body, int index)
        {
            WorldTasks w = WorldFor(body);
            if (w == null || index < 0) return null;
            for (int i = 0; i < w.Bases.Count; i++) if (w.Bases[i].Index == index) return w.Bases[i];
            return null;
        }

        /// <summary>The world the player is near, with its tasks laid out once its ground, bases and camps are known.</summary>
        WorldTasks Here(out Vector3 player)
        {
            player = Vector3.zero;
            Astronaut a = Player;
            SolarSystem system = _world != null ? _world.System : null;
            if (a == null || a.Eye == null || system == null || system.Focus == null) return null;
            player = a.Eye.transform.position;
            int body = system.Focus.Neighbourhood;
            if (body <= 0) return null;
            if (_worlds.TryGetValue(body, out WorldTasks known)) return known;
            PlanetSurface surface = _world.Planets != null ? _world.Planets.SurfaceOf(body) : null;
            if (surface == null || !surface.Ready || surface.Terrain == null) return null;
            if (_world.Bases == null || !_world.Bases.Known(body) || _world.Bases.SitesOn(body) == null) return null;
            if (_world.Enemies == null || _world.Enemies.KnownCamps(body) == null) return null;
            WorldTasks w = LayOut(body, surface);
            _worlds[body] = w;
            return w;
        }

        WorldTasks LayOut(int body, PlanetSurface surface)
        {
            string id = surface.Body.Spec.id;
            var w = new WorldTasks { Body = body, Id = id, Surface = surface };
            SeededRandom random = GameSession.Stream("tasks." + id);
            IReadOnlyList<BaseSite> sites = _world.Bases.SitesOn(body);
            PlanetTerrain terrain = surface.Terrain;

            for (int i = 0; i < sites.Count; i++)
            {
                BaseSite s = sites[i];
                _world.Enemies.BaseGuards(body, i, out int guards, out int left);
                var b = new BaseTask { World = w, Index = i, Site = s, Dir = s.Up, Rule = new BaseClear((int)s.Size, guards) };
                // Cleared earlier in this run (the world was left and come back to): it stays done.
                b.Done = guards == 0 || left == 0;
                w.Bases.Add(b);

                // Every base rolls for a mast, in the same order every time for this seed.
                bool mast = random.NextDouble() < _rules.mastShare;
                Vec3d spot = Near(s, random, _rules.mastNear, _rules.mastFar);
                if (mast && Dry(terrain, spot))
                    w.Masts.Add(new MastTask { World = w, Index = i, Dir = spot, Rule = new SignalMast(_rules.mastCells, _rules.mastCellHealth, _rules.mastCallEvery) });
            }

            for (int n = 0; n < _rules.salvagePerWorld && sites.Count > 0; n++)
            {
                BaseSite s = sites[random.Range(0, sites.Count)];
                Vec3d spot = Near(s, random, _rules.salvageNear, _rules.salvageFar);
                if (Dry(terrain, spot)) w.Crates.Add(new CrateTask { World = w, Index = n, Dir = spot });
            }

            // One marked target a world: the first member of a camp picked from the seed.
            IReadOnlyList<EnemyCamp> camps = _world.Enemies.KnownCamps(body);
            if (camps != null && camps.Count > 0)
            {
                int camp = random.Range(0, camps.Count);
                if (camps[camp].Members != null && camps[camp].Members.Length > 0)
                {
                    w.MarkCamp = camp;
                    w.MarkMember = random.Range(0, camps[camp].Members.Length);
                }
            }
            return w;
        }

        static Vec3d Near(BaseSite s, SeededRandom random, float near, float far)
        {
            double angle = random.NextDouble() * System.Math.PI * 2.0;
            double distance = s.Footprint + near + random.NextDouble() * System.Math.Max(0f, far - near);
            Vec3d along = s.East * System.Math.Cos(angle) + s.North * System.Math.Sin(angle);
            return (s.Centre + along * distance).Normalized;
        }

        static bool Dry(PlanetTerrain terrain, Vec3d dir) => !terrain.HasSea || terrain.Height(dir) > terrain.SeaLevel + 1.0;

        // ================================================================== the frame

        void Update()
        {
            if (_world == null) return;
            WorldTasks w = Here(out Vector3 player);
            float now = Now;
            Astronaut a = Player;

            UpdateCarry(a);
            UpdateLanding();
            UpdateDrops(w, player, now);

            // Masts, views, snapping, the quiet check and the lights: a few times a second is plenty.
            if (Time.time < _slowAt) return;
            _slowAt = Time.time + 0.25f;
            if (w == null) { ShowNoLights(); return; }
            UpdateMasts(w, player, now);
            BuildNearViews(w, player);
            CheckSpotted();
            UpdateLights(w, player);
        }

        // ------------------------------------------------------------------ masts

        void UpdateMasts(WorldTasks w, Vector3 player, float now)
        {
            for (int i = 0; i < w.Masts.Count; i++)
            {
                MastTask m = w.Masts[i];
                if (m.Rule.Down) continue;
                Vector3 at = m.WorldPoint;
                bool inReach = (at - player).sqrMagnitude <= _rules.mastReach * _rules.mastReach && Player != null && Player.InTheOpen;
                if (!m.Rule.Calls(now, inReach)) continue;
                // Every enemy in reach, of either side, is told where the player is.
                _raising = true;
                CombatWorld.RaiseAlarm(at, _rules.mastReach, player, Factions.None);
                CombatWorld.RaiseAlarm(at, _rules.mastReach, player, Factions.A);
                _raising = false;
                Audio.AudioDirector.Play("enemy.flare.fire", at, 1f, 0.7f);
                AnteLoop.Note("A SIGNAL MAST CALLED FOR HELP: SHOOT OUT ITS CELLS");
            }
        }

        void HitCell(MastTask m, int cell, float damage, Vector3 point, Vector3 normal)
        {
            if (m.Rule.CellBroken(cell)) return;
            Weapons.WeaponFx fx = Weapons.WeaponFx.Instance;
            if (fx != null) fx.Sparks(point, normal, 5);
            bool down = m.Rule.Hit(cell, damage, Now);
            if (m.Rule.CellBroken(cell))
            {
                if (m.Cells[cell] != null) m.Cells[cell].sharedMaterial = Mat.Lit(new Color(0.12f, 0.11f, 0.10f), 0.3f);
                if (m.CellColliders[cell] != null) m.CellColliders[cell].enabled = false;
                if (fx != null) fx.Sparks(point, normal, 18);
                Audio.AudioDirector.Play("impact.metal", point, 1f, 0.8f);
            }
            if (down) MastDown(m, false);
        }

        void Rammed(MastTask m)
        {
            if (m.Rule.Rammed(Now)) MastDown(m, true);
        }

        void MastDown(MastTask m, bool rammed)
        {
            for (int i = 0; i < m.Cells.Length; i++)
            {
                if (m.Cells[i] != null) m.Cells[i].sharedMaterial = Mat.Lit(new Color(0.12f, 0.11f, 0.10f), 0.3f);
                if (m.CellColliders[i] != null) m.CellColliders[i].enabled = false;
            }
            // It falls over where it stood (no physics: it is part of the world's ground).
            if (m.View != null) m.View.transform.GetChild(0).localRotation = Quaternion.Euler(78f, Random.Range(0f, 360f), 0f);
            if (m.Light != null) m.Light.Show(false);
            string label = rammed ? "RAMMED" : !float.IsNaN(m.Rule.BrokenWithin) && m.Rule.BrokenWithin <= _rules.mastQuickSeconds ? "QUICK" : string.Empty;
            int feats = label.Length > 0 ? 1 : 0;
            feats += StreakFeat(ref label);
            Audio.AudioDirector.PlayLocal("reward.tier2");
            Score(ScoreEventKind.InstallationDestroyed, TaskIds.Mast(m.World.Id, m.Index), feats, Join("MAST DOWN", label));
        }

        // ------------------------------------------------------------------ salvage

        void UpdateCarry(Astronaut a)
        {
            if (_carried == null) return;
            if (a == null || a.Eye == null) { DropCarried(null); return; }
            // Into the lift or aboard: delivered.
            if (a.Mode == AstronautMode.Aboard || a.Mode == AstronautMode.Lift || a.Mode == AstronautMode.Seated)
            {
                CrateTask c = _carried;
                _carried = null;
                Astronaut.CarryPace = 1f;
                c.Rule.Deliver();
                if (c.View != null) Destroy(c.View);
                c.View = null;
                string label = c.Rule.HurtWhileCarried ? string.Empty : "UNSCRATCHED";
                int feats = label.Length > 0 ? 1 : 0;
                feats += StreakFeat(ref label);
                Audio.AudioDirector.PlayLocal("reward.loot.rare");
                Score(ScoreEventKind.SalvageDelivered, TaskIds.Salvage(c.World.Id, c.Index), feats, Join("SALVAGE DELIVERED", label));
            }
        }

        void OnArmourDamaged(float amount, Vector3 direction)
        {
            if (_carried != null) _carried.Rule.Hurt();
        }

        void PickUp(CrateTask c, Astronaut a)
        {
            if (!c.Rule.PickUp()) return;
            _carried = c;
            Astronaut.CarryPace = _rules.carrySpeed;
            if (c.Box != null) c.Box.enabled = false;
            c.View.transform.SetParent(a.Eye.transform, false);
            c.View.transform.localPosition = new Vector3(0.18f, -0.62f, 0.95f);
            c.View.transform.localRotation = Quaternion.identity;
            if (c.Light != null) c.Light.Show(false);
            if (a.Armour != null) { a.Armour.Damaged -= OnArmourDamaged; a.Armour.Damaged += OnArmourDamaged; }
            Audio.AudioDirector.PlayLocal("ui.select");
            AnteLoop.Note("CARRY IT BACK ABOARD THE SHIP");
        }

        void DropCarried(Astronaut a)
        {
            CrateTask c = _carried;
            _carried = null;
            Astronaut.CarryPace = 1f;
            if (c == null) return;
            c.Rule.Drop();
            if (a != null && a.Eye != null && c.View != null && c.World.Surface != null)
            {
                // Put down at the player's feet, on the world it is on.
                Transform body = c.World.Surface.Body.transform;
                Vector3 feet = a.Eye.transform.position + a.Eye.transform.forward * 1.2f;
                c.Dir = SpaceConvert.ToVec3d(body.InverseTransformPoint(feet)).Normalized;
                c.View.transform.SetParent(body, false);
                Stand(c);
            }
            if (c.Box != null) c.Box.enabled = true;
        }

        // ------------------------------------------------------------------ supply drops

        void UpdateDrops(WorldTasks w, Vector3 player, float now)
        {
            Astronaut a = Player;
            // One drop at a time; one missed while away from any ground falls once you are back on one.
            if (Scoring && w != null && a != null && a.InTheOpen && _drops.Count == 0 && _dropClock.Due(now)) LaunchDrop(w, player, now);

            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                DropTask d = _drops[i];
                if (d.Rule.Tick(now)) Landed(d);
                if (d.Rule.State == DropState.Landed && _world.Enemies != null && _world.Enemies.LivingWithin(d.WorldPoint, _rules.dropReachedWithin))
                    d.Rule.EnemyNear(0f);
                if (d.View != null)
                {
                    if (d.Rule.State == DropState.Falling)
                    {
                        double ground = d.World.Surface.GroundRadius(d.Dir);
                        float above = 600f * (1f - d.Rule.Fallen(now));
                        d.View.transform.localPosition = SpaceConvert.ToVector3(d.Dir * (ground + above));
                    }
                    else if (d.Rule.State != DropState.Landed)
                    {
                        Destroy(d.View);
                        d.View = null;
                    }
                }
                if (d.Rule.State == DropState.Gone || d.Rule.State == DropState.Opened)
                {
                    if (d.View != null) Destroy(d.View);
                    if (_holdingDrop == d) _holdingDrop = null;
                    _drops.RemoveAt(i);
                }
            }
        }

        void LaunchDrop(WorldTasks w, Vector3 player, float now)
        {
            Transform body = w.Surface.Body.transform;
            Vec3d here = SpaceConvert.ToVec3d(body.InverseTransformPoint(player));
            Vec3d up = here.Normalized;
            Vec3d side = Vec3d.Cross(up, System.Math.Abs(up.Y) < 0.9 ? Vec3d.Up : Vec3d.Right).Normalized;
            Vec3d other = Vec3d.Cross(up, side);
            SeededRandom random = GameSession.Stream("tasks.drops.a" + _loop.Run.Director.Ante + "." + _dropClock.Given);
            Vec3d dir = up;
            for (int tries = 0; tries < 8; tries++)
            {
                double angle = random.NextDouble() * System.Math.PI * 2.0;
                double distance = _rules.dropNear + random.NextDouble() * System.Math.Max(0f, _rules.dropFar - _rules.dropNear);
                dir = (up * here.Magnitude + (side * System.Math.Cos(angle) + other * System.Math.Sin(angle)) * distance).Normalized;
                if (Dry(w.Surface.Terrain, dir)) break;
            }
            var d = new DropTask { World = w, Dir = dir, Number = _dropClock.Given - 1, Ante = _loop.Run.Director.Ante, Rule = new SupplyDrop(_rules, now) };
            d.View = BuildPod(body);
            d.View.transform.localRotation = Quaternion.FromToRotation(Vector3.up, SpaceConvert.ToVector3(dir));
            d.Light = new Beacon(d.View.transform, _config.haulBeacon);
            _drops.Add(d);
            Audio.AudioDirector.PlayLocal("reward.discovery");
            AnteLoop.Note("SUPPLY DROP INCOMING: GET THERE FIRST");
        }

        void Landed(DropTask d)
        {
            if (d.View != null) Stand(d);
            Vector3 at = d.WorldPoint;
            // It is loud: everyone within earshot comes to look.
            _raising = true;
            CombatWorld.RaiseAlarm(at, _rules.dropHeardFrom, at, Factions.None);
            CombatWorld.RaiseAlarm(at, _rules.dropHeardFrom, at, Factions.A);
            _raising = false;
            Audio.AudioDirector.Play("impact.metal", at, 1f, 0.5f);
        }

        void OpenDrop(DropTask d, bool gun)
        {
            if (!d.Rule.Open()) return;
            Combat.GunLadderFx ladder = Combat.GunLadderFx.Instance;
            bool gave = gun && ladder != null && ladder.GiveNextGunNow();
            if (!gave)
            {
                Combat.DeepBreathFx breath = Combat.DeepBreathFx.Instance;
                if (breath != null && breath.Breath != null) breath.Breath.Earn(1f);
                Audio.AudioDirector.PlayLocal("reward.tier1");
            }
            string label = d.Rule.Reached ? "CONTESTED" : string.Empty;
            int feats = label.Length > 0 ? 1 : 0;
            feats += StreakFeat(ref label);
            Score(ScoreEventKind.OptionalMission, TaskIds.Drop(d.Ante, d.Number), feats,
                Join(gave ? "SUPPLY DROP: NEW GUN" : "SUPPLY DROP: FOCUS FULL", label), _rules.dropPoints);
        }

        // ------------------------------------------------------------------ what the player is offered

        public bool Offer(Vector3 eye, Vector3 forward, float dt, out string prompt, out float progress)
        {
            prompt = null;
            progress = -1f;
            Astronaut a = Player;
            if (a == null || GameInput.Interact == null) return false;
            string key = GameInput.Describe(GameInput.Interact);

            if (_carried != null)
            {
                if (GameInput.Interact.WasPressedThisFrame()) { DropCarried(a); return false; }
                prompt = "Carrying salvage: take it aboard the ship  (" + key + " to put it down)";
                return true;
            }

            // A landed supply drop: press for Focus, hold for the next gun.
            DropTask drop = NearestDrop(eye);
            if (drop != null)
            {
                Combat.GunLadderFx ladder = Combat.GunLadderFx.Instance;
                bool canGun = ladder != null && ladder.CanGiveNextGun;
                if (GameInput.Interact.WasPressedThisFrame()) { _holdingDrop = drop; _held = 0f; }
                if (_holdingDrop == drop)
                {
                    if (GameInput.Interact.IsPressed())
                    {
                        _held += dt;
                        if (canGun && _held >= _rules.dropHoldForGun) { _holdingDrop = null; OpenDrop(drop, true); return false; }
                    }
                    else
                    {
                        _holdingDrop = null;
                        OpenDrop(drop, false);
                        return false;
                    }
                }
                prompt = canGun ? key + ": refill Focus     hold " + key + ": next gun" : key + ": refill Focus";
                if (_holdingDrop == drop && canGun) progress = Mathf.Clamp01(_held / Mathf.Max(0.05f, _rules.dropHoldForGun));
                return true;
            }

            CrateTask crate = NearestCrate(eye, forward);
            if (crate != null)
            {
                if (GameInput.Interact.WasPressedThisFrame()) { PickUp(crate, a); return false; }
                prompt = key + ": pick up salvage (slows you down)";
                return true;
            }
            return false;
        }

        DropTask NearestDrop(Vector3 eye)
        {
            for (int i = 0; i < _drops.Count; i++)
            {
                DropTask d = _drops[i];
                if (d.Rule.State != DropState.Landed || d.View == null) continue;
                if ((d.View.transform.position - eye).sqrMagnitude <= 3.2f * 3.2f) return d;
            }
            return null;
        }

        CrateTask NearestCrate(Vector3 eye, Vector3 forward)
        {
            Astronaut a = Player;
            if (a == null || _world.System == null || _world.System.Focus == null) return null;
            WorldTasks w = WorldFor(_world.System.Focus.Neighbourhood);
            if (w == null) return null;
            for (int i = 0; i < w.Crates.Count; i++)
            {
                CrateTask c = w.Crates[i];
                if (c.Rule.State != CrateState.Lying || c.View == null) continue;
                Vector3 to = c.View.transform.position - eye;
                if (to.sqrMagnitude > 2.8f * 2.8f) continue;
                if (to.sqrMagnitude > 1f && Vector3.Dot(to.normalized, forward) < 0.75f) continue;
                return c;
            }
            return null;
        }

        // ------------------------------------------------------------------ landings

        void UpdateLanding()
        {
            Ship.Ship ship = _world.Ship;
            SolarSystem system = _world.System;
            if (ship == null || ship.Flight == null || system == null || ship.Gravity == null) return;
            bool landed = ship.Flight.Landed;
            int body = ship.Gravity.Neighbourhood;
            if (!landed && body > 0 && ship.Body != null)
                _lastTouchSpeed = (ship.Body.linearVelocity - system.GroundVelocityAt(body, ship.transform.position)).magnitude;
            if (landed && !_wasLanded && Time.time >= _quietUntil) Touchdown(ship, body);
            _wasLanded = landed;
        }

        void Touchdown(Ship.Ship ship, int body)
        {
            PlanetSurface surface = _world.Planets != null && body > 0 ? _world.Planets.SurfaceOf(body) : null;
            if (surface == null || !surface.Ready) return;
            string id = surface.Body.Spec.id;
            BodyKind kind = surface.Body.Spec.kind;
            if (kind == BodyKind.Star) return;

            if (!_noFirstLanding.Contains(id))
            {
                _noFirstLanding.Add(id);
                bool moon = kind == BodyKind.Moon || kind == BodyKind.Asteroid;
                Score(moon ? ScoreEventKind.NewMoon : ScoreEventKind.NewPlanet, TaskIds.FirstLanding(id), 0,
                    "FIRST LANDING: " + (surface.Body.Spec.displayName ?? id).ToUpperInvariant());
            }

            // Rough ground: the ground around the legs rises and falls by more than the relief.
            Transform t = surface.Body.transform;
            Vec3d here = SpaceConvert.ToVec3d(t.InverseTransformPoint(ship.transform.position));
            Vec3d up = here.Normalized;
            double radius = surface.GroundRadius(up);
            Vec3d side = Vec3d.Cross(up, System.Math.Abs(up.Y) < 0.9 ? Vec3d.Up : Vec3d.Right).Normalized;
            Vec3d other = Vec3d.Cross(up, side);
            var around = new List<double>(8);
            for (int i = 0; i < 8; i++)
            {
                double angle = i * System.Math.PI / 4.0;
                Vec3d dir = (up * radius + (side * System.Math.Cos(angle) + other * System.Math.Sin(angle)) * _rules.hardLandingSample).Normalized;
                around.Add(surface.GroundRadius(dir));
            }
            if (!LandingRelief.IsHard(radius, around, _rules.hardLandingRelief)) return;
            string cell = LandingRelief.Cell(up.X, up.Y, up.Z, radius, _rules.hardLandingCell);
            string label = _lastTouchSpeed <= _rules.softTouchdown ? "FEATHER SOFT" : string.Empty;
            int feats = label.Length > 0 ? 1 : 0;
            Score(ScoreEventKind.DifficultLanding, TaskIds.HardLanding(id, cell), feats, Join("HARD LANDING", label));
        }

        // ------------------------------------------------------------------ spotted

        /// <summary>A living guard that knows where the player is spoils that base's quiet clear.</summary>
        void CheckSpotted()
        {
            if (_world.Enemies == null || _woke.Count == 0) return;
            _scratch.Clear();
            foreach (KeyValuePair<Mannequin, WokeRecord> kv in _woke)
            {
                if (kv.Key == null) { _scratch.Add(kv.Key); continue; }
                WokeRecord r = kv.Value;
                if (r.Combat == null || !kv.Key.Alive || !r.Combat.AwareOfPlayer) continue;
                BaseTask b = BaseFor(r.Body, _world.Enemies.BaseOfCamp(r.Body, r.Camp));
                if (b != null) b.Rule.Alarm();
            }
            for (int i = 0; i < _scratch.Count; i++) _woke.Remove(_scratch[i]);
            // Hits on enemies that were never finished are forgotten after a while.
            if (_hits.Count > 64)
            {
                _scratch.Clear();
                foreach (KeyValuePair<Mannequin, HitRecord> kv in _hits)
                    if (kv.Key == null || Now - kv.Value.At > 30f) _scratch.Add(kv.Key);
                for (int i = 0; i < _scratch.Count; i++) _hits.Remove(_scratch[i]);
            }
            _marked.RemoveWhere(IsGone);
        }

        // ================================================================== views

        const float BuildWithin = 900f;
        const float SnapWithin = 250f;

        void BuildNearViews(WorldTasks w, Vector3 player)
        {
            float build2 = BuildWithin * BuildWithin;
            for (int i = 0; i < w.Masts.Count; i++)
            {
                MastTask m = w.Masts[i];
                if (m.View == null && (m.WorldPoint - player).sqrMagnitude < build2) BuildMast(m);
                Snap(m, player);
            }
            for (int i = 0; i < w.Crates.Count; i++)
            {
                CrateTask c = w.Crates[i];
                if (c.View == null && c.Rule.State == CrateState.Lying && (c.WorldPoint - player).sqrMagnitude < build2) BuildCrate(c);
                if (c != _carried) Snap(c, player);
            }
        }

        void Snap(Placed p, Vector3 player)
        {
            if (p.View == null || Time.time < p.SnapAt || p.View.transform.parent != p.World.Surface.Body.transform) return;
            p.SnapAt = Time.time + 1f;
            if ((p.View.transform.position - player).sqrMagnitude > SnapWithin * SnapWithin) return;
            Stand(p);
        }

        /// <summary>Stands a view on the ground that is really built under it, upright to its world.</summary>
        static void Stand(Placed p)
        {
            Vector3 up = SpaceConvert.ToVector3(p.Dir);
            p.View.transform.localPosition = SpaceConvert.ToVector3(p.Dir * p.World.Surface.GroundRadius(p.Dir));
            p.View.transform.localRotation = Quaternion.FromToRotation(Vector3.up, up);
        }

        GameObject Root(Placed p, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p.World.Surface.Body.transform, false);
            p.View = go;
            Stand(p);
            return go;
        }

        static Renderer Piece(Transform parent, PrimitiveType type, Material m, Vector3 at, Vector3 size, bool solid)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            if (!solid) Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            return r;
        }

        void BuildMast(MastTask m)
        {
            GameObject root = Root(m, "SignalMast");
            // Everything that falls over hangs from one pivot at the foot.
            var pivot = new GameObject("Mast").transform;
            pivot.SetParent(root.transform, false);
            Material steel = Mat.Lit(new Color(0.42f, 0.43f, 0.45f), 0.45f);
            Material dark = Mat.Lit(new Color(0.16f, 0.16f, 0.17f), 0.3f);
            Piece(root.transform, PrimitiveType.Cube, dark, new Vector3(0f, 0.3f, 0f), new Vector3(2.4f, 0.6f, 2.4f), true);
            Renderer pole = Piece(pivot, PrimitiveType.Cylinder, steel, new Vector3(0f, 4.5f, 0f), new Vector3(0.45f, 4.5f, 0.45f), true);
            var ram = pole.gameObject.AddComponent<MastRam>();
            ram.Speed = _rules.mastRamSpeed;
            ram.Rammed = () => Rammed(m);
            Renderer dish = Piece(pivot, PrimitiveType.Cylinder, steel, new Vector3(0f, 9.3f, 0.3f), new Vector3(1.8f, 0.06f, 1.8f), false);
            dish.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);

            Material lit = Mat.Unlit(new Color(1f, 0.36f, 0.16f) * 2.2f);
            int count = m.Rule.Cells;
            m.Cells = new Renderer[count];
            m.CellColliders = new Collider[count];
            for (int i = 0; i < count; i++)
            {
                float turn = i * 360f / count;
                Vector3 side = Quaternion.Euler(0f, turn, 0f) * Vector3.forward * 0.55f;
                Renderer cell = Piece(pivot, PrimitiveType.Cube, lit, new Vector3(side.x, 2.2f + i * 2.4f, side.z), new Vector3(0.6f, 0.9f, 0.6f), true);
                cell.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
                cell.name = "Cell" + i;
                var hit = cell.gameObject.AddComponent<MastCell>();
                int which = i;
                hit.Cell = i;
                hit.Struck = (damage, point, normal) => HitCell(m, which, damage, point, normal);
                m.Cells[i] = cell;
                m.CellColliders[i] = cell.GetComponent<Collider>();
                if (m.Rule.CellBroken(i)) { cell.sharedMaterial = dark; m.CellColliders[i].enabled = false; }
            }
            m.Light = new Beacon(root.transform, _config.fightBeacon);
        }

        void BuildCrate(CrateTask c)
        {
            GameObject root = Root(c, "SalvageCrate");
            Material shell = Mat.Lit(new Color(0.55f, 0.50f, 0.40f), 0.35f);
            Material band = Mat.Unlit(new Color(0.40f, 0.85f, 1f) * 1.8f);
            Renderer box = Piece(root.transform, PrimitiveType.Cube, shell, new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.6f, 0.55f), true);
            c.Box = box.GetComponent<Collider>();
            Piece(box.transform, PrimitiveType.Cube, band, new Vector3(0f, 0.15f, 0f), new Vector3(1.02f, 0.12f, 1.02f), false);
            c.Light = new Beacon(root.transform, _config.haulBeacon);
        }

        GameObject BuildPod(Transform body)
        {
            var go = new GameObject("SupplyDrop");
            go.transform.SetParent(body, false);
            Material shell = Mat.Lit(new Color(0.78f, 0.78f, 0.80f), 0.5f);
            Material glow = Mat.Unlit(new Color(0.40f, 0.85f, 1f) * 2.4f);
            Piece(go.transform, PrimitiveType.Cylinder, shell, new Vector3(0f, 0.7f, 0f), new Vector3(1f, 0.7f, 1f), true);
            Piece(go.transform, PrimitiveType.Cylinder, glow, new Vector3(0f, 1.0f, 0f), new Vector3(1.04f, 0.08f, 1.04f), false);
            Piece(go.transform, PrimitiveType.Sphere, glow, new Vector3(0f, 1.5f, 0f), new Vector3(0.35f, 0.35f, 0.35f), false);
            return go;
        }

        // ------------------------------------------------------------------ the lights over tasks

        /// <summary>A tall thin column of light over a task, drawn wider far off so it can be seen from kilometres.</summary>
        sealed class Beacon
        {
            readonly GameObject _go;

            public Beacon(Transform over, Color colour)
            {
                _go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _go.name = "TaskLight";
                Object.Destroy(_go.GetComponent<Collider>());
                var r = _go.GetComponent<Renderer>();
                r.sharedMaterial = Mat.Unlit(new Color(colour.r, colour.g, colour.b) * 1.6f);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                _go.transform.SetParent(over, false);
                _go.SetActive(false);
            }

            public void Show(bool on)
            {
                if (_go != null && _go.activeSelf != on) _go.SetActive(on);
            }

            public void Fit(float distance)
            {
                if (_go == null) return;
                // About two pixels wide at any distance, 300 m tall, standing on the task.
                float width = Mathf.Max(0.35f, distance * 0.0035f);
                Vector3 parentScale = _go.transform.parent != null ? _go.transform.parent.lossyScale : Vector3.one;
                float s = Mathf.Max(0.01f, parentScale.y);
                _go.transform.localScale = new Vector3(width / s, 150f / s, width / s);
                _go.transform.localPosition = new Vector3(0f, 150f / s, 0f);
            }
        }

        void UpdateLights(WorldTasks w, Vector3 player)
        {
            _candidates.Clear();
            for (int i = 0; i < w.Bases.Count; i++)
            {
                BaseTask b = w.Bases[i];
                if (b.Done) { if (b.Light != null) b.Light.Show(false); continue; }
                if (b.View == null)
                {
                    // A base's light hangs from a marker of our own over the middle of its floor.
                    var go = new GameObject("BaseTask");
                    go.transform.SetParent(w.Surface.Body.transform, false);
                    go.transform.localPosition = SpaceConvert.ToVector3(b.Site.Centre);
                    go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, SpaceConvert.ToVector3(b.Site.Up));
                    b.View = go;
                    b.Light = new Beacon(go.transform, _config.fightBeacon);
                }
                _candidates.Add(b);
            }
            for (int i = 0; i < w.Masts.Count; i++) if (w.Masts[i].Light != null) { if (w.Masts[i].Live) _candidates.Add(w.Masts[i]); else w.Masts[i].Light.Show(false); }
            for (int i = 0; i < w.Crates.Count; i++) if (w.Crates[i].Light != null) { if (w.Crates[i].Live) _candidates.Add(w.Crates[i]); else w.Crates[i].Light.Show(false); }
            for (int i = 0; i < _drops.Count; i++) if (_drops[i].Light != null && _drops[i].World == w) _candidates.Add(_drops[i]);

            // The nearest few within reach are lit; the rest wait until you are closer.
            _distances.Clear();
            for (int i = 0; i < _candidates.Count; i++) _distances.Add((_candidates[i].View.transform.position - player).magnitude);
            float reach = _rules.nearbyReach;
            for (int pick = 0; pick < _rules.liveNearby; pick++)
            {
                int best = -1;
                for (int i = 0; i < _candidates.Count; i++)
                    if (_distances[i] >= 0f && _distances[i] <= reach && (best < 0 || _distances[i] < _distances[best])) best = i;
                if (best < 0) break;
                // Close enough to see the thing itself: no light needed, and it never stands in your face.
                bool lit = _distances[best] > 25f;
                _candidates[best].Light.Show(lit);
                if (lit) _candidates[best].Light.Fit(_distances[best]);
                _distances[best] = -1f;
            }
            for (int i = 0; i < _candidates.Count; i++) if (_distances[i] >= 0f) _candidates[i].Light.Show(false);

            // Supply drops in the air are always lit: they are the thing to race for.
            for (int i = 0; i < _drops.Count; i++)
            {
                DropTask d = _drops[i];
                if (d.Light == null || d.View == null) continue;
                float dist = (d.View.transform.position - player).magnitude;
                bool on = d.Rule.State == DropState.Falling || (d.Rule.State == DropState.Landed && dist > 25f);
                d.Light.Show(on);
                if (on) d.Light.Fit(dist);
            }
        }

        void ShowNoLights()
        {
            foreach (KeyValuePair<int, WorldTasks> kv in _worlds)
            {
                WorldTasks w = kv.Value;
                for (int i = 0; i < w.Bases.Count; i++) if (w.Bases[i].Light != null) w.Bases[i].Light.Show(false);
                for (int i = 0; i < w.Masts.Count; i++) if (w.Masts[i].Light != null) w.Masts[i].Light.Show(false);
                for (int i = 0; i < w.Crates.Count; i++) if (w.Crates[i].Light != null) w.Crates[i].Light.Show(false);
            }
        }

        /// <summary>For the system map: how many tasks are still to do on a world (-1 if not known yet).</summary>
        public int TasksLeftOn(int body)
        {
            WorldTasks w = WorldFor(body);
            if (w == null) return -1;
            int n = 0;
            for (int i = 0; i < w.Bases.Count; i++) if (w.Bases[i].Live) n++;
            for (int i = 0; i < w.Masts.Count; i++) if (w.Masts[i].Live) n++;
            for (int i = 0; i < w.Crates.Count; i++) if (w.Crates[i].Live) n++;
            return n;
        }
    }
}
