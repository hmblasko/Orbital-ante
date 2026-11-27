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
    ///  - Kills score by kind, with style feats (one shot, long shot, airborne, melee, the first kill this ante with
    ///    another tool, all four limbs off; a rover splat is one feat and only that) and a streak of quick kills.
    ///    A world's marked target (a red light over one enemy) is worth more.
    ///  - Clear a base: every guard down. Quiet (no guard saw you) and quick are feats. Every base on a world: a bonus.
    ///  - Signal masts near some bases call every enemy in reach onto you every so often until all their cells are
    ///    shot out or a rover rams them at speed. Down before its first call is a feat.
    ///  - Salvage crates near bases, carried back aboard the ship (driving it home is allowed, but only a crate
    ///    carried on foot all the way and never hurt is a feat).
    ///  - Supply drops fall near you during an ante: race the enemies for it, then take half a Focus meter (press) or
    ///    three gun-ladder kills (hold, once an ante).
    ///  - First landing on each world, and hard landings on rough ground (very soft ones are a feat).
    ///
    /// A tall light stands over the nearest few tasks not done yet. Every reach is held to a share of the world, which
    /// is a few hundred metres across. Rules are in <see cref="TaskRules"/> and the points go through
    /// <see cref="AnteLoop.Report"/>. Lives on the solar world and goes with it.
    /// </summary>
    public sealed class TaskDirector : MonoBehaviour, IWorldOffer
    {
        SolarSystemWorld _world;
        AnteLoop _loop;
        TaskConfig _config;
        TaskRules _rules;
        readonly GunFreshness _fresh = new GunFreshness();
        DropClock _dropClock;

        // ------------------------------------------------------------------ per world
        sealed class WorldTasks
        {
            public int Body;
            public string Id;
            public PlanetSurface Surface;
            public double Radius;
            public readonly List<MastTask> Masts = new List<MastTask>();
            public readonly List<CrateTask> Crates = new List<CrateTask>();
            /// <summary>The bases' foundations, only so landing on one is never a hard landing. Clearing bases is no
            /// longer a task (Matthew, 21 Nov 2026: "I don't like the system where you go after outposts ... remove it").</summary>
            public readonly List<BaseTask> Bases = new List<BaseTask>();
            public int MarkCamp = -1, MarkMember = -1;
            public Transform Frame => Surface.Body.transform;
        }

        abstract class Placed
        {
            public WorldTasks World;
            public Vec3d Dir;
            /// <summary>Where it stands in its world's own space, from the ideal ground (good to a few metres: for distances only).</summary>
            public Vector3 Local;
            public GameObject View;
            public float SnapAt;
            public abstract bool Live { get; }
            public void Place(Vec3d dir)
            {
                Dir = dir;
                Local = SpaceConvert.ToVector3(dir * World.Surface.IdealGroundRadius(dir));
            }
            public Vector3 WorldPoint => World.Frame.TransformPoint(Local);
        }

        sealed class MastTask : Placed
        {
            public int Index;
            public SignalMast Rule;
            public Renderer[] Cells;
            public Collider[] CellColliders;
            /// <summary>With the Props model: each cell's canister (its mesh is swapped for the burnt one) and its lit window.</summary>
            public MeshFilter[] CellShapes;
            public Renderer[] CellGlows;
            /// <summary>With the Props model: the model's pieces (two groups, one of them on the falling pivot), the plain shapes shown instead from far off, and which is showing.</summary>
            public GameObject ModelStanding, ModelOnPivot;
            public Renderer[] Plain;
            public bool ModelShown;
            public bool InReach;
            public override bool Live => !Rule.Down;
        }

        sealed class CrateTask : Placed
        {
            public int Index;
            public readonly SalvageCrate Rule = new SalvageCrate();
            public Collider Box;
            /// <summary>The crate's model, when there is one (its shadow is switched off while it is carried).</summary>
            public Renderer Drawn;
            public bool Driven;
            public override bool Live => Rule.State == CrateState.Lying;
        }

        sealed class BaseTask : Placed
        {
            public int Index;
            public BaseSite Site;
            public override bool Live => false;
        }

        sealed class DropTask : Placed
        {
            public int Number, Ante;
            public SupplyDrop Rule;
            public Collider Shell;
            public GameObject OwnLight;
            public override bool Live => Rule.State == DropState.Falling || Rule.State == DropState.Landed;
        }

        readonly Dictionary<int, WorldTasks> _worlds = new Dictionary<int, WorldTasks>();
        readonly List<DropTask> _drops = new List<DropTask>();

        // ------------------------------------------------------------------ kills
        struct HitRecord
        {
            public float At;
            public KillFacts Facts;
            public int Hits;
        }

        struct WokeRecord
        {
            public int Body, Camp, Member;
            public MannequinCombat Combat;
        }

        readonly Dictionary<Mannequin, HitRecord> _hits = new Dictionary<Mannequin, HitRecord>();
        readonly Dictionary<Mannequin, WokeRecord> _woke = new Dictionary<Mannequin, WokeRecord>();
        readonly Dictionary<Mannequin, GameObject> _marked = new Dictionary<Mannequin, GameObject>();
        readonly List<Mannequin> _scratch = new List<Mannequin>();
        readonly List<Mannequin> _gone = new List<Mannequin>();

        // ------------------------------------------------------------------ carrying, opening, landing
        CrateTask _carried;
        public bool CarryingSalvage => _carried != null;
        DropTask _holdingDrop;
        float _held;
        int _ladderDropAnte;
        bool _wasLanded = true;
        float _lastTouchSpeed;
        float _quietUntil;
        readonly HashSet<string> _noFirstLanding = new HashSet<string>();
        OrbitalAnte.Player.PlayerHealth _watchedArmour;

        // ------------------------------------------------------------------ prompts (rebuilt only when they change)
        string _key, _promptCarry, _promptDrop, _promptCrate;
        SupplyDropOffer _shownDropOffer;
        float _keyAt = -1f;

        // ------------------------------------------------------------------ slow pass
        float _slowAt;
        bool _laidOutThisFrame;
        readonly List<Placed> _candidates = new List<Placed>();
        readonly List<float> _distances = new List<float>();
        readonly List<Beacon> _pool = new List<Beacon>();

        float Now => _loop != null ? _loop.PlayTime : Time.time;
        bool Scoring => _loop != null && _loop.Running;

        public void Init(SolarSystemWorld world, AnteLoop loop)
        {
            _world = world;
            _loop = loop;
            _config = loop != null ? loop.Config : TaskConfig.Load();
            _rules = _config.rules;
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

        /// <summary>The task director of the running solar-system run (the break asks it for nearby worlds).</summary>
        public static TaskDirector Active { get; private set; }

        /// <summary>The player holds a melee weapon (a melee style contract is only offered then).</summary>
        public bool HoldsMelee
        {
            get
            {
                Astronaut a = Player;
                return a != null && a.Melee != null && a.Melee.Held != null;
            }
        }

        /// <summary>
        /// The worlds nearest the player for the break's bonus missions, nearest first: planets and moons only (not
        /// the Sun, asteroids or the gas giants, whose ground is a long dive inside), at most eight.
        /// </summary>
        public void NearbyWorlds(List<ContractWorld> into)
        {
            into.Clear();
            SolarSystem system = _world != null ? _world.System : null;
            if (system == null || system.Model == null || system.States == null) return;
            Transform from = Player != null ? Player.transform : _world.Ship != null ? _world.Ship.transform : null;
            if (from == null) return;
            Vec3d at = system.TruePosition(from.position);
            int here = system.Focus != null ? system.Focus.Neighbourhood : -1;
            _nearBodies.Clear();
            _nearDistances.Clear();
            for (int i = 0; i < system.Model.Count; i++)
            {
                BodySpec spec = system.Model.Spec(i);
                if (spec.kind != BodyKind.Planet && spec.kind != BodyKind.Moon) continue;
                double d = (system.States[i].Position - at).Magnitude - spec.radius;
                int slot = 0;
                while (slot < _nearDistances.Count && _nearDistances[slot] <= d) slot++;
                _nearDistances.Insert(slot, d);
                _nearBodies.Insert(slot, i);
            }
            for (int k = 0; k < _nearBodies.Count && into.Count < 8; k++)
            {
                BodySpec spec = system.Model.Spec(_nearBodies[k]);
                into.Add(new ContractWorld
                {
                    body = _nearBodies[k],
                    name = spec.displayName ?? spec.id,
                    landedOn = _noFirstLanding.Contains(spec.id),
                    here = _nearBodies[k] == here,
                    enemiesLeft = _world.Enemies != null ? _world.Enemies.EnemiesLeftAtLeast(_nearBodies[k], spec.radius) : 0,
                    enemiesCounted = _world.Enemies != null,
                });
            }
        }

        readonly List<int> _nearBodies = new List<int>(16);
        readonly List<double> _nearDistances = new List<double>(16);

        void OnEnable()
        {
            // Read the two models now, while the world is being made, not when the first mast comes into range.
            OrbitalAnte.Look.ModelLibrary.Find(PropsModel);
            OrbitalAnte.Look.ModelLibrary.Find(Melee.MeleeItem.ModelId);
            Active = this;
            CombatEvents.PlayerHitSomething += OnPlayerHit;
            EnemySpawner.Down += OnEnemyDown;
            EnemySpawner.Woke += OnEnemyWoke;
            WorldOffers.Add(this);
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
            CombatEvents.PlayerHitSomething -= OnPlayerHit;
            EnemySpawner.Down -= OnEnemyDown;
            EnemySpawner.Woke -= OnEnemyWoke;
            WorldOffers.Remove(this);
            if (_loop != null) _loop.AnteBegan -= OnAnteBegan;
            Astronaut.CarryPace = 1f;
            if (_watchedArmour != null) _watchedArmour.Damaged -= OnArmourDamaged;
            _watchedArmour = null;
        }

        void OnAnteBegan(int ante)
        {
            _dropClock.AnteStarted(_loop.PlayTime);
            _fresh.Clear();
        }

        Astronaut Player => _world != null ? _world.Astronaut : null;

        // ================================================================== scoring helpers

        ScoreResult Score(ScoreEventKind kind, string claim, int feats, string label, int missionPoints = 0, string tag = null)
        {
            if (!Scoring) return default;
            var e = new ScoreEvent { claimId = claim, kind = kind, skillFeats = feats, missionBasePoints = missionPoints, tag = tag };
            return AnteLoop.Report(e, label);
        }

        /// <summary>
        /// A task finished while a long streak is running gets one more feat and the streak is spent, but only when
        /// that raises the bonus (two feats is already the ceiling, so the streak is kept for the next task).
        /// </summary>
        int StreakFeat(int feats, ref string label)
        {
            Streak streak = _loop != null ? _loop.Streak : null;
            if (feats >= 2 || streak == null || streak.CountAt(Now) < _rules.streakForFeat) return 0;
            streak.Spend(Now);
            label = string.IsNullOrEmpty(label) ? RunWords.OnARoll : label + " + " + RunWords.OnARoll;
            return 1;
        }

        static string Join(string what, string feats) => string.IsNullOrEmpty(feats) ? what : what + "  " + feats;

        // ================================================================== kills

        void OnPlayerHit(HitOutcome outcome)
        {
            if (!outcome.Enemy || !(outcome.Target is Mannequin who) || who == null) return;
            Astronaut player = Player;
            bool known = _hits.TryGetValue(who, out HitRecord before);
            var facts = new KillFacts
            {
                airborne = player != null && player.Airborne,
                rover = outcome.Source == HitSource.Rover,
                melee = outcome.Source == HitSource.Melee,
                lowStance = player != null && player.Mode == AstronautMode.Outside && player.Stances.Current != Spacewalk.Stance.Stand,
            };
            if (outcome.Killed)
            {
                // The first hit that landed on it, and from how far.
                facts.oneShot = !known || before.Hits == 0;
                if (player != null && player.Eye != null && !facts.rover && !facts.melee)
                {
                    float longShot = Mathf.Max(10f, _rules.longShot - Combat.RunEffects.Mods.LongShotCloser);
                    facts.longShot = (who.transform.position - player.Eye.transform.position).sqrMagnitude >= longShot * longShot;
                }
                string gun = facts.rover ? "rover" : facts.melee ? "melee"
                           : !string.IsNullOrEmpty(outcome.SourceId) ? outcome.SourceId
                           : player != null && player.Weapons != null && player.Weapons.Current != null && player.Weapons.Current.Data != null
                               ? player.Weapons.Current.Data.Id : "gun";
                facts.freshGun = _fresh.IsFresh(gun);
                _fresh.Remember(gun);
                facts.takenApart = AllFourLimbsOff(who);
            }
            _hits[who] = new HitRecord { At = Now, Facts = facts, Hits = (known ? before.Hits : 0) + 1 };

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
            GameObject glow = null;
            bool marked = who != null && _marked.TryGetValue(who, out glow);
            if (marked)
            {
                if (glow != null) Destroy(glow);
                _marked.Remove(who);
            }
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
                // The sound comes when the streak starts paying: the next task gets the feat.
                if (streak == _rules.streakForFeat) Audio.AudioDirector.PlayLocal("reward.multiplier", 0.7f);
                ScoreResult paid = Score(kind, TaskIds.Enemy(e.BodyId, e.Camp, e.Member), feats, label);
                // The Ghost set puts time back for a LONG SHOT kill (20 Nov 2026).
                if (paid.Awarded && f.longShot) AnteLoop.LongShotKill();
                AnteLoop.ContractKill(e.Body, f);
                AnteLoop.UnlockKill(f);
            }
            if (who != null) _woke.Remove(who);

        }


        void Mark(Mannequin who)
        {
            if (_marked.ContainsKey(who)) return;
            Transform head = who.Rig != null && who.Rig.Head != null ? who.Rig.Head : who.transform;
            float scale = Mathf.Max(0.1f, head.lossyScale.y);
            Renderer glow = Piece(head, PrimitiveType.Sphere, Mat.Unlit(new Color(1f, 0.18f, 0.12f) * 2.4f),
                Vector3.up * (0.42f / scale), Vector3.one * (0.16f / scale), false);
            glow.name = "MarkedTargetLight";
            _marked[who] = glow.gameObject;
        }


        // ================================================================== worlds

        WorldTasks WorldFor(int body) => _worlds.TryGetValue(body, out WorldTasks w) ? w : null;


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
            _laidOutThisFrame = true;
            // Its marked target may have stood up a moment before the world was laid out.
            if (_rules.markedTargets && w.MarkCamp >= 0)
                foreach (KeyValuePair<Mannequin, WokeRecord> kv in _woke)
                    if (kv.Key != null && kv.Value.Body == body && kv.Value.Camp == w.MarkCamp && kv.Value.Member == w.MarkMember) { Mark(kv.Key); break; }
            return w;
        }

        WorldTasks LayOut(int body, PlanetSurface surface)
        {
            string id = surface.Body.Spec.id;
            var w = new WorldTasks { Body = body, Id = id, Surface = surface, Radius = surface.SurfaceRadius };
            SeededRandom random = GameSession.Stream("tasks." + id);
            IReadOnlyList<BaseSite> sites = _world.Bases.SitesOn(body);
            PlanetTerrain terrain = surface.Terrain;

            for (int i = 0; i < sites.Count; i++)
            {
                BaseSite s = sites[i];
                var b = new BaseTask { World = w, Index = i, Site = s };
                b.Dir = s.Up;
                b.Local = SpaceConvert.ToVector3(s.Centre);
                w.Bases.Add(b);

                // Every base rolls for a mast, in the same order every time for this seed.
                bool mast = random.NextDouble() < _rules.mastShare;
                Vec3d spot = Near(s, random, _rules.mastNear, _rules.mastFar);
                if (mast && Dry(terrain, spot))
                {
                    var m = new MastTask { World = w, Index = i, Rule = new SignalMast(_rules.mastCells, _rules.mastCellHealth, _rules.mastCallEvery) };
                    m.Place(spot);
                    w.Masts.Add(m);
                }
            }

            for (int n = 0; n < _rules.salvagePerWorld && sites.Count > 0; n++)
            {
                BaseSite s = sites[random.Range(0, sites.Count)];
                Vec3d spot = Near(s, random, _rules.salvageNear, _rules.salvageFar);
                if (!Dry(terrain, spot)) continue;
                var c = new CrateTask { World = w, Index = n };
                c.Place(spot);
                w.Crates.Add(c);
            }

            // One marked target a world: one member of a camp picked from the seed.
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
            _laidOutThisFrame = false;
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
            Vector3 local = w.Frame.InverseTransformPoint(player);
            UpdateMasts(w, local, player, now);
            // Never build in the frame a world's tasks were laid out: one heavy thing a frame.
            if (!_laidOutThisFrame) BuildNearViews(w, local);
            CheckSpotted();
            UpdateLights(w, local);
        }

        // ------------------------------------------------------------------ masts

        void UpdateMasts(WorldTasks w, Vector3 local, Vector3 player, float now)
        {
            float reach = _rules.MastReachOn(w.Radius);
            Astronaut a = Player;
            bool open = a != null && a.InTheOpen;
            for (int i = 0; i < w.Masts.Count; i++)
            {
                MastTask m = w.Masts[i];
                if (m.Rule.Down) continue;
                bool inReach = open && (m.Local - local).sqrMagnitude <= reach * reach;
                // Coming into a standing mast's reach: say how long before it calls (no hidden timers).
                if (inReach && !m.InReach) AnteLoop.Note("SIGNAL MAST IN RANGE: it calls for help in " + Mathf.RoundToInt(_rules.mastCallEvery) + " s");
                m.InReach = inReach;
                if (!m.Rule.Calls(now, inReach)) continue;
                // Every enemy in reach, of either side, is told where the player is.
                Vector3 at = m.WorldPoint;
                CombatWorld.RaiseAlarm(at, reach, player, Factions.None);
                CombatWorld.RaiseAlarm(at, reach, player, Factions.A);
                Audio.AudioDirector.Play("enemy.flare.fire", at, 1f, 0.7f);
                AnteLoop.Note("THE MAST CALLED FOR HELP: SHOOT OUT ITS CELLS");
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
                BurnOut(m, cell);
                if (fx != null) fx.Sparks(point, normal, 18);
                Audio.AudioDirector.Play("impact.metal", point, 1f, 0.8f);
            }
            if (down) MastDown(m, false);
        }

        /// <summary>A cell that is shot out (or whose mast is down) goes dark and can no longer be hit.</summary>
        static void BurnOut(MastTask m, int cell)
        {
            if (m.CellShapes != null && m.CellShapes[cell] != null)
            {
                Mesh burnt = Prop("cellBurnt");
                if (burnt != null) m.CellShapes[cell].sharedMesh = burnt;
                if (m.CellGlows[cell] != null) m.CellGlows[cell].enabled = false;
            }
            // The plain shape too: it is what shows from far off, and all there is without the model.
            if (m.Cells[cell] != null) m.Cells[cell].sharedMaterial = Mat.Lit(new Color(0.12f, 0.11f, 0.10f), 0.3f);
            if (m.CellColliders[cell] != null) m.CellColliders[cell].enabled = false;
        }

        void Rammed(MastTask m)
        {
            if (m.Rule.Rammed(Now)) MastDown(m, true);
        }

        void MastDown(MastTask m, bool rammed)
        {
            for (int i = 0; i < m.Cells.Length; i++) BurnOut(m, i);
            // It falls over where it stood (a stand-in: no physics, it is part of the world's ground).
            if (m.View != null) m.View.transform.GetChild(0).localRotation = Quaternion.Euler(78f, Random.Range(0f, 360f), 0f);
            // Ramming pays the mast but is never a feat; shooting it out before it ever called is.
            string label = !rammed && !m.Rule.HasCalled ? "BEFORE IT CALLED" : string.Empty;
            int feats = label.Length > 0 ? 1 : 0;
            feats += StreakFeat(feats, ref label);
            Audio.AudioDirector.PlayLocal("reward.tier2");
            Score(ScoreEventKind.InstallationDestroyed, TaskIds.Mast(m.World.Id, m.Index), feats, Join(rammed ? "MAST RAMMED" : "MAST DOWN", label));
            AnteLoop.ContractMastDown();
        }

        // ------------------------------------------------------------------ salvage

        void UpdateCarry(Astronaut a)
        {
            if (_carried == null) return;
            if (a == null || a.Eye == null) { DropCarried(null); return; }
            if (a.Mode == AstronautMode.Driving) _carried.Driven = true;
            // Into the lift or aboard: delivered.
            if (a.Mode == AstronautMode.Aboard || a.Mode == AstronautMode.Lift || a.Mode == AstronautMode.Seated)
            {
                CrateTask c = _carried;
                _carried = null;
                Astronaut.CarryPace = 1f;
                c.Rule.Deliver();
                if (c.View != null) Destroy(c.View);
                c.View = null;
                // Only a crate carried on foot all the way, and never hurt on the way, is a feat.
                string label = c.Rule.HurtWhileCarried || c.Driven ? string.Empty : "CARRIED HOME UNSCRATCHED";
                int feats = label.Length > 0 ? 1 : 0;
                feats += StreakFeat(feats, ref label);
                Audio.AudioDirector.PlayLocal("reward.loot.rare");
                Score(ScoreEventKind.SalvageDelivered, TaskIds.Salvage(c.World.Id, c.Index), feats, Join("SALVAGE DELIVERED", label));
                AnteLoop.ContractCrateDelivered();
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
            c.View.SetActive(true);
            c.View.transform.SetParent(a.Eye.transform, false);
            c.View.transform.localPosition = new Vector3(0.24f, -0.85f, 0.85f);
            c.View.transform.localRotation = Quaternion.identity;
            CrateShadow(c, false);   // carried in front of the eye, it would throw a shadow from nowhere
            if (a.Armour != null && _watchedArmour != a.Armour)
            {
                if (_watchedArmour != null) _watchedArmour.Damaged -= OnArmourDamaged;
                _watchedArmour = a.Armour;
                _watchedArmour.Damaged += OnArmourDamaged;
            }
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
            if (c.View != null && c.World.Surface != null)
            {
                Transform body = c.World.Frame;
                Vector3 put = a != null && a.Eye != null ? a.Eye.transform.position + a.Eye.transform.forward * 1.2f : c.View.transform.position;
                c.Place(SpaceConvert.ToVec3d(body.InverseTransformPoint(put)).Normalized);
                c.View.transform.SetParent(body, false);
                Stand(c);
            }
            if (c.Box != null) c.Box.enabled = true;
            CrateShadow(c, true);
        }

        static void CrateShadow(CrateTask c, bool casts)
        {
            if (c.Drawn != null) OrbitalAnte.Look.SunShadows.Cast(c.Drawn, casts);
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
                if (d.Rule.State == DropState.Landed && d.View != null && _world.Enemies != null
                    && _world.Enemies.LivingWithin(d.View.transform.position, _rules.dropReachedWithin))
                    d.Rule.EnemyNear(0f);
                if (d.View != null && d.Rule.State == DropState.Falling)
                {
                    float above = 600f * (1f - d.Rule.Fallen(now));
                    d.View.transform.localPosition = d.Local + SpaceConvert.ToVector3(d.Dir) * above;
                }
                if (d.Rule.State == DropState.Gone || d.Rule.State == DropState.Opened)
                {
                    if (d.View != null) Destroy(d.View);
                    d.View = null;
                    if (_holdingDrop == d) _holdingDrop = null;
                    _drops.RemoveAt(i);
                }
            }
        }

        void LaunchDrop(WorldTasks w, Vector3 player, float now)
        {
            Transform body = w.Frame;
            Vec3d up = SpaceConvert.ToVec3d(body.InverseTransformPoint(player)).Normalized;
            Vec3d side = Vec3d.Cross(up, System.Math.Abs(up.Y) < 0.9 ? Vec3d.Up : Vec3d.Right).Normalized;
            Vec3d other = Vec3d.Cross(up, side);
            SeededRandom random = GameSession.Stream("tasks.drops.a" + _loop.Run.Director.Ante + "." + _dropClock.Given);
            float near = _rules.DropNearOn(w.Radius), far = Mathf.Max(near, _rules.DropFarOn(w.Radius));
            Vec3d dir = up;
            for (int tries = 0; tries < 8; tries++)
            {
                // A distance over the ground, turned into an angle round the world.
                double angle = random.NextDouble() * System.Math.PI * 2.0;
                double round = (near + random.NextDouble() * (far - near)) / System.Math.Max(1.0, w.Radius);
                Vec3d along = side * System.Math.Cos(angle) + other * System.Math.Sin(angle);
                dir = (up * System.Math.Cos(round) + along * System.Math.Sin(round)).Normalized;
                if (Dry(w.Surface.Terrain, dir)) break;
            }
            var d = new DropTask { World = w, Number = _dropClock.Given - 1, Ante = _loop.Run.Director.Ante, Rule = new SupplyDrop(_rules, now) };
            d.Place(dir);
            d.Local = SpaceConvert.ToVector3(dir * w.Surface.GroundRadius(dir));
            BuildPod(d);
            _drops.Add(d);
            Audio.AudioDirector.PlayLocal("reward.discovery");
            AnteLoop.Note("SUPPLY DROP INCOMING: GET THERE FIRST");
        }

        void Landed(DropTask d)
        {
            if (d.View != null) Stand(d);
            // Solid only once it is down: a moving collider on the planet's body costs every physics step.
            if (d.Shell != null) d.Shell.enabled = true;
            Vector3 at = d.WorldPoint;
            float heard = _rules.DropHeardOn(d.World.Radius);
            // It is loud: everyone within earshot comes to look.
            CombatWorld.RaiseAlarm(at, heard, at, Factions.None);
            CombatWorld.RaiseAlarm(at, heard, at, Factions.A);
            Audio.AudioDirector.Play("impact.metal", at, 1f, 0.5f);
        }

        SupplyDropOffer CurrentDropOffer()
        {
            var ladder = Combat.GunLadderFx.Instance;
            var breath = Combat.DeepBreathFx.Instance;
            bool allowCredit = ladder != null && ladder.CanClimb && _loop != null && _loop.Run != null
                && _ladderDropAnte != _loop.Run.Director.Ante;
            return new SupplyDropOffer(_rules, breath != null && breath.Breath != null ? breath.Breath.Meter : 1f,
                allowCredit ? ladder.Ladder.KillsLeft : 0);
        }

        void OpenDrop(DropTask d, int gunKills)
        {
            if (!d.Rule.Open()) return;
            var ladder = Combat.GunLadderFx.Instance;
            int credited = 0;
            float gained = 0f;
            if (gunKills > 0 && ladder != null && ladder.CreditKills(gunKills))
            {
                credited = gunKills;
                _ladderDropAnte = _loop.Run.Director.Ante;
            }
            else
            {
                var fx = Combat.DeepBreathFx.Instance;
                if (fx != null && fx.Breath != null)
                {
                    float before = fx.Breath.Meter;
                    fx.Breath.Earn(_rules.dropFocus);
                    gained = fx.Breath.Meter - before;
                }
            }
            Audio.AudioDirector.PlayLocal("reward.tier1");
            string label = d.Rule.Reached ? "CONTESTED" : string.Empty;
            int feats = label.Length > 0 ? 1 : 0;
            feats += StreakFeat(feats, ref label);
            Score(ScoreEventKind.OptionalMission, TaskIds.Drop(d.Ante, d.Number), feats,
                Join(SupplyDropOffer.Receipt(credited, gained), label), _rules.dropPoints);
        }

        // ------------------------------------------------------------------ what the player is offered

        /// <summary>The Interact key's name, looked up again once a second (a rebind shows within a second).</summary>
        void RefreshPrompts()
        {
            if (_key != null && Time.unscaledTime < _keyAt) return;
            _keyAt = Time.unscaledTime + 1f;
            string key = GameInput.Describe(GameInput.Interact);
            if (key == _key) return;
            _key = key;
            _promptCarry = "Carrying salvage: take it aboard the ship  (" + key + " to put it down)";
            _promptDrop = null;
            _promptCrate = key + ": pick up salvage (slows you down)";
        }

        public bool Offer(Vector3 eye, Vector3 forward, float dt, out string prompt, out float progress)
        {
            prompt = null;
            progress = -1f;
            Astronaut a = Player;
            if (a == null || GameInput.Interact == null) return false;

            // Something else wants this press: a gun or melee weapon to pick up, or the rover to climb into.
            bool pickupNear = (a.Weapons != null && a.Weapons.NearbyPickup != null) || (a.Melee != null && a.Melee.NearbyPickup != null);
            if (_carried != null)
            {
                bool roverNear = _world.Rover != null && (_world.Rover.transform.position - eye).sqrMagnitude < 4.5f * 4.5f;
                if (pickupNear || roverNear) return false;
                if (GameInput.Interact.WasPressedThisFrame()) { DropCarried(a); return false; }
                RefreshPrompts();
                prompt = _promptCarry;
                return true;
            }

            // A landed supply drop: press for Focus, hold for gun-ladder kills.
            if (pickupNear) { _holdingDrop = null; return false; }
            DropTask drop = NearestDrop(eye);
            // Walking away mid-hold lets go: coming back never opens it without a new press.
            if (_holdingDrop != drop) _holdingDrop = null;
            if (drop != null)
            {
                SupplyDropOffer offer = CurrentDropOffer();
                bool canGun = offer.GunKills > 0;
                if (GameInput.Interact.WasPressedThisFrame()) { _holdingDrop = drop; _held = 0f; }
                if (_holdingDrop == drop)
                {
                    if (GameInput.Interact.IsPressed())
                    {
                        _held += TimeFlow.PlayerDelta(dt);
                        if (canGun && _held >= _rules.dropHoldForGun) { _holdingDrop = null; OpenDrop(drop, offer.GunKills); return false; }
                    }
                    else
                    {
                        _holdingDrop = null;
                        OpenDrop(drop, 0);
                        return false;
                    }
                }
                RefreshPrompts();
                if (_promptDrop == null || offer.GunKills != _shownDropOffer.GunKills
                    || offer.FocusPercent != _shownDropOffer.FocusPercent)
                {
                    _shownDropOffer = offer;
                    _promptDrop = offer.Prompt(_key);
                }
                prompt = _promptDrop;
                if (_holdingDrop == drop && canGun) progress = Mathf.Clamp01(_held / Mathf.Max(0.05f, _rules.dropHoldForGun));
                return true;
            }

            CrateTask crate = NearestCrate(eye, forward);
            if (crate != null)
            {
                if (GameInput.Interact.WasPressedThisFrame()) { PickUp(crate, a); return false; }
                RefreshPrompts();
                prompt = _promptCrate;
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
            if (_world.System == null || _world.System.Focus == null) return null;
            WorldTasks w = WorldFor(_world.System.Focus.Neighbourhood);
            if (w == null) return null;
            for (int i = 0; i < w.Crates.Count; i++)
            {
                CrateTask c = w.Crates[i];
                if (c.Rule.State != CrateState.Lying || c.View == null || !c.View.activeSelf) continue;
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

        readonly List<double> _around = new List<double>(8);

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
            AnteLoop.ContractLanded(body);
            AnteLoop.UnlockLanded(id);

            // Rough ground: the ground under the legs stands well above or below the ground around them.
            Transform t = surface.Body.transform;
            Vec3d up = SpaceConvert.ToVec3d(t.InverseTransformPoint(ship.transform.position)).Normalized;
            double radius = surface.GroundRadius(up);
            Vec3d side = Vec3d.Cross(up, System.Math.Abs(up.Y) < 0.9 ? Vec3d.Up : Vec3d.Right).Normalized;
            Vec3d other = Vec3d.Cross(up, side);
            _around.Clear();
            for (int i = 0; i < 8; i++)
            {
                double angle = i * System.Math.PI / 4.0;
                Vec3d dir = (up * radius + (side * System.Math.Cos(angle) + other * System.Math.Sin(angle)) * _rules.hardLandingSample).Normalized;
                _around.Add(surface.GroundRadius(dir));
            }
            if (!LandingRelief.IsHard(radius, _around, _rules.hardLandingRelief)) return;
            if (OnAFoundation(body, surface, t, ship.transform.position, up)) return;
            string cell = LandingRelief.Cell(up.X, up.Y, up.Z, radius, _rules.hardLandingCell);
            string label = _lastTouchSpeed <= _rules.softTouchdown ? "FEATHER SOFT" : string.Empty;
            int feats = label.Length > 0 ? 1 : 0;
            Score(ScoreEventKind.DifficultLanding, TaskIds.HardLanding(id, cell), feats, Join("HARD LANDING", label));
        }

        /// <summary>The pad and the bases stand on their own level floors: landing there is never a hard landing.</summary>
        bool OnAFoundation(int body, PlanetSurface surface, Transform frame, Vector3 at, Vec3d up)
        {
            if (_world.Base != null && _world.Base.Surface == surface
                && Vec3d.Dot(up, _world.Base.Spot.Up) > System.Math.Cos(60.0 / System.Math.Max(1.0, surface.SurfaceRadius))) return true;
            WorldTasks w = WorldFor(body);
            if (w == null) return false;
            Vector3 local = frame.InverseTransformPoint(at);
            for (int i = 0; i < w.Bases.Count; i++)
            {
                float reach = (float)w.Bases[i].Site.Footprint + 8f;
                if ((w.Bases[i].Local - local).sqrMagnitude < reach * reach) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ spotted

        /// <summary>A living guard that knows where the player is spoils that base's quiet clear.</summary>
        void CheckSpotted()
        {
            if (_world.Enemies == null) return;
            _scratch.Clear();
            foreach (KeyValuePair<Mannequin, WokeRecord> kv in _woke)
            {
                if (kv.Key == null) { _scratch.Add(kv.Key); continue; }
                WokeRecord r = kv.Value;
                if (r.Combat == null || !kv.Key.Alive || !r.Combat.AwareOfPlayer) continue;
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
            _gone.Clear();
            foreach (KeyValuePair<Mannequin, GameObject> kv in _marked) if (kv.Key == null) _gone.Add(kv.Key);
            for (int i = 0; i < _gone.Count; i++) _marked.Remove(_gone[i]);
        }

        // ================================================================== views

        const float BuildWithin = 900f;
        const float HideBeyond = 1100f;
        const float SnapWithin = 250f;

        /// <summary>Builds at most one view a pass (the nearest missing), and puts far ones away (their colliders too).</summary>
        void BuildNearViews(WorldTasks w, Vector3 local)
        {
            float build2 = BuildWithin * BuildWithin, hide2 = HideBeyond * HideBeyond;
            Placed nearest = null;
            float best = build2;
            for (int i = 0; i < w.Masts.Count; i++) Visit(w.Masts[i], local, hide2, ref nearest, ref best);
            for (int i = 0; i < w.Crates.Count; i++)
                if (w.Crates[i] != _carried && w.Crates[i].Rule.State == CrateState.Lying) Visit(w.Crates[i], local, hide2, ref nearest, ref best);
            if (nearest is MastTask m) BuildMast(m);
            else if (nearest is CrateTask c) BuildCrate(c);
        }

        void Visit(Placed p, Vector3 local, float hide2, ref Placed nearest, ref float best)
        {
            float d2 = (p.Local - local).sqrMagnitude;
            if (p.View == null)
            {
                if (d2 < best) { best = d2; nearest = p; }
                return;
            }
            bool on = d2 < hide2;
            if (p.View.activeSelf != on) p.View.SetActive(on);
            if (on && p is MastTask mast && mast.Plain != null)
            {
                float within = mast.ModelShown ? ModelBeyond : ModelWithin;
                ShowModel(mast, d2 < within * within);
            }
            if (on && d2 < SnapWithin * SnapWithin && Time.time >= p.SnapAt)
            {
                p.SnapAt = Time.time + 1f;
                Stand(p);
            }
        }

        /// <summary>
        /// A mast wears its real model only near the player (a world can have two dozen masts, all standing at once
        /// and none hidden by the ground in front); from further off the plain shapes are the same size on screen.
        /// </summary>
        const float ModelWithin = 150f, ModelBeyond = 175f;

        static void ShowModel(MastTask m, bool model)
        {
            if (m.ModelShown == model) return;
            m.ModelShown = model;
            if (m.ModelStanding != null) m.ModelStanding.SetActive(model);
            if (m.ModelOnPivot != null) m.ModelOnPivot.SetActive(model);
            for (int i = 0; i < m.Plain.Length; i++) if (m.Plain[i] != null) m.Plain[i].enabled = !model;
        }

        /// <summary>Stands a view on the ground that is really built under it, upright to its world.</summary>
        static void Stand(Placed p)
        {
            p.View.transform.localPosition = SpaceConvert.ToVector3(p.Dir * p.World.Surface.GroundRadius(p.Dir));
            p.View.transform.localRotation = Quaternion.FromToRotation(Vector3.up, SpaceConvert.ToVector3(p.Dir));
        }

        GameObject Root(Placed p, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p.World.Frame, false);
            p.View = go;
            Stand(p);
            return go;
        }

        static readonly Dictionary<PrimitiveType, Mesh> Shapes = new Dictionary<PrimitiveType, Mesh>();

        /// <summary>Unity's own primitive shape, taken once and shared (its collider is never made).</summary>
        static Mesh Shape(PrimitiveType type)
        {
            if (Shapes.TryGetValue(type, out Mesh mesh) && mesh != null) return mesh;
            var go = GameObject.CreatePrimitive(type);
            mesh = go.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(go);
            Shapes[type] = mesh;
            return mesh;
        }

        public static void ResetStatics()
        {
            Shapes.Clear();
            Active = null;
        }

        /// <summary>
        /// One piece of a stand-in: a shared primitive shape, no shadow, and a collider only when it is solid, so
        /// nothing is added to the planet's body and then taken away again.
        /// </summary>
        static Renderer Piece(Transform parent, PrimitiveType type, Material m, Vector3 at, Vector3 size, bool solid)
        {
            var go = new GameObject(type.ToString());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = Shape(type);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (solid)
            {
                switch (type)
                {
                    case PrimitiveType.Cube: go.AddComponent<BoxCollider>(); break;
                    case PrimitiveType.Sphere: go.AddComponent<SphereCollider>(); break;
                    default: go.AddComponent<CapsuleCollider>(); break;
                }
            }
            return r;
        }

        /// <summary>The model the task objects are drawn from (tools/blender/build_props.py); the primitive shapes are the fallback.</summary>
        public const string PropsModel = "Props";

        static Mesh Prop(string part) => OrbitalAnte.Look.ModelLibrary.PartMesh(PropsModel, part);

        /// <summary>
        /// One piece of the Props model, drawn only (what is solid stays the plain shapes, which are then hidden).
        /// Null when the model or the piece is missing.
        /// </summary>
        static Renderer Drawn(Transform parent, string part, Vector3 at, Quaternion turn, bool castsShadow)
        {
            Mesh mesh = Prop(part);
            if (mesh == null) return null;
            var go = new GameObject(part);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = turn;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = OrbitalAnte.Look.ModelLibrary.PieceMaterial(PropsModel, part);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            if (castsShadow) OrbitalAnte.Look.SunShadows.Cast(r, true);
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
            Renderer footing = Piece(root.transform, PrimitiveType.Cube, dark, new Vector3(0f, 0.3f, 0f), new Vector3(2.4f, 0.6f, 2.4f), true);
            Renderer pole = Piece(pivot, PrimitiveType.Cylinder, steel, new Vector3(0f, 4.5f, 0f), new Vector3(0.45f, 4.5f, 0.45f), true);
            var ram = pole.gameObject.AddComponent<MastRam>();
            ram.Speed = _rules.mastRamSpeed;
            ram.Rammed = () => Rammed(m);
            Renderer dish = Piece(pivot, PrimitiveType.Cylinder, steel, new Vector3(0f, 9.3f, 0.3f), new Vector3(1.8f, 0.06f, 1.8f), false);
            dish.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            // The real model, where there is one: the plain shapes stay as what is solid and are no longer drawn.
            bool modelled = Prop("mast") != null && Prop("mastBase") != null && Prop("cell") != null;
            Transform onPivot = null;
            var plain = new List<Renderer> { footing, pole, dish };
            if (modelled)
            {
                m.ModelStanding = new GameObject("Model");
                m.ModelStanding.transform.SetParent(root.transform, false);
                m.ModelOnPivot = new GameObject("Model");
                onPivot = m.ModelOnPivot.transform;
                onPivot.SetParent(pivot, false);
                Drawn(m.ModelStanding.transform, "mastBase", Vector3.zero, Quaternion.identity, true);
                Drawn(onPivot, "mast", Vector3.zero, Quaternion.identity, true);
                m.CellShapes = new MeshFilter[m.Rule.Cells];
                m.CellGlows = new Renderer[m.Rule.Cells];
            }

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
                plain.Add(cell);
                if (modelled)
                {
                    // Under the pivot, not the scaled box, so the canister keeps its own size and falls with the mast.
                    Renderer can = Drawn(onPivot, "cell", cell.transform.localPosition, cell.transform.localRotation, true);
                    m.CellShapes[i] = can.GetComponent<MeshFilter>();
                    m.CellGlows[i] = Drawn(onPivot, "cellGlow", cell.transform.localPosition, cell.transform.localRotation, false);
                }
            }
            if (modelled)
            {
                m.Plain = plain.ToArray();
                // Begins as the plain shapes; Visit swaps the model in on its next pass if the player is near.
                m.ModelStanding.SetActive(false);
                m.ModelOnPivot.SetActive(false);
            }
        }

        void BuildCrate(CrateTask c)
        {
            GameObject root = Root(c, "SalvageCrate");
            Material shell = Mat.Lit(new Color(0.55f, 0.50f, 0.40f), 0.35f);
            Material band = Mat.Unlit(new Color(0.40f, 0.85f, 1f) * 1.8f);
            Renderer box = Piece(root.transform, PrimitiveType.Cube, shell, new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.6f, 0.55f), true);
            c.Box = box.GetComponent<Collider>();
            Renderer strip = Piece(box.transform, PrimitiveType.Cube, band, new Vector3(0f, 0.15f, 0f), new Vector3(1.02f, 0.12f, 1.02f), false);
            c.Drawn = Drawn(root.transform, "crate", Vector3.zero, Quaternion.identity, true);
            if (c.Drawn != null)
            {
                Drawn(root.transform, "crateGlow", Vector3.zero, Quaternion.identity, false);
                box.enabled = false;
                strip.enabled = false;
            }
        }

        void BuildPod(DropTask d)
        {
            var go = new GameObject("SupplyDrop");
            go.transform.SetParent(d.World.Frame, false);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, SpaceConvert.ToVector3(d.Dir));
            d.View = go;
            Material shell = Mat.Lit(new Color(0.78f, 0.78f, 0.80f), 0.5f);
            Material glow = Mat.Unlit(new Color(0.40f, 0.85f, 1f) * 2.4f);
            Renderer body = Piece(go.transform, PrimitiveType.Cylinder, shell, new Vector3(0f, 0.7f, 0f), new Vector3(1f, 0.7f, 1f), true);
            d.Shell = body.GetComponent<Collider>();
            d.Shell.enabled = false;
            Renderer ring = Piece(go.transform, PrimitiveType.Cylinder, glow, new Vector3(0f, 1.0f, 0f), new Vector3(1.04f, 0.08f, 1.04f), false);
            Renderer ball = Piece(go.transform, PrimitiveType.Sphere, glow, new Vector3(0f, 1.5f, 0f), new Vector3(0.35f, 0.35f, 0.35f), false);
            if (Drawn(go.transform, "pod", Vector3.zero, Quaternion.identity, true) != null)
            {
                Drawn(go.transform, "podGlow", Vector3.zero, Quaternion.identity, false);
                body.enabled = false;
                ring.enabled = false;
                ball.enabled = false;
            }
            // A drop carries its own light: it moves too fast for the shared ones.
            Renderer beam = Piece(go.transform, PrimitiveType.Cylinder, Mat.Unlit(BeamColour(_config.haulBeacon)), Vector3.zero, Vector3.one, false);
            beam.name = "TaskLight";
            d.OwnLight = beam.gameObject;
        }

        // ------------------------------------------------------------------ the lights over tasks

        static Color BeamColour(Color c) => new Color(c.r, c.g, c.b) * 1.6f;

        /// <summary>About two pixels wide at any distance, standing on its task; up to 300 m tall, never taller than the world is wide.</summary>
        static void FitBeam(Transform beam, float distance, double worldRadius)
        {
            float half = Mathf.Min(150f, (float)worldRadius * 0.5f);
            float width = Mathf.Max(0.35f, distance * 0.0035f);
            beam.localScale = new Vector3(width, half, width);
            beam.localPosition = new Vector3(0f, half, 0f);
        }

        /// <summary>One of the few shared lights, moved to whichever tasks are lit (never one per task).</summary>
        sealed class Beacon
        {
            public readonly Transform Root;
            readonly Transform _beam;
            readonly Renderer _renderer;
            Color _colour;

            public Beacon(Transform parent)
            {
                Root = new GameObject("TaskLight").transform;
                Root.SetParent(parent, false);
                _renderer = Piece(Root, PrimitiveType.Cylinder, Mat.Unlit(Color.white), Vector3.zero, Vector3.one, false);
                _beam = _renderer.transform;
                Root.gameObject.SetActive(false);
            }

            public void Show(bool on)
            {
                if (Root != null && Root.gameObject.activeSelf != on) Root.gameObject.SetActive(on);
            }

            public void Put(Transform world, Vector3 local, Vec3d up, Color colour, float distance, double worldRadius)
            {
                if (Root.parent != world) Root.SetParent(world, false);
                Root.localPosition = local;
                Root.localRotation = Quaternion.FromToRotation(Vector3.up, SpaceConvert.ToVector3(up));
                if (colour != _colour)
                {
                    _colour = colour;
                    _renderer.sharedMaterial = Mat.Unlit(BeamColour(colour));
                }
                FitBeam(_beam, distance, worldRadius);
                Show(true);
            }
        }

        Color ColourOf(Placed p) => p is MastTask ? _config.fightBeacon : _config.haulBeacon;

        void UpdateLights(WorldTasks w, Vector3 local)
        {
            while (_pool.Count < Mathf.Max(0, _rules.liveNearby)) _pool.Add(new Beacon(w.Frame));

            _candidates.Clear();
            for (int i = 0; i < w.Masts.Count; i++) if (w.Masts[i].Live) _candidates.Add(w.Masts[i]);
            for (int i = 0; i < w.Crates.Count; i++) if (w.Crates[i].Live && w.Crates[i] != _carried) _candidates.Add(w.Crates[i]);

            // The nearest few within reach are lit, and none closer than 25 m (you can see the thing itself).
            _distances.Clear();
            for (int i = 0; i < _candidates.Count; i++) _distances.Add((_candidates[i].Local - local).magnitude);
            float reach = _rules.NearbyOn(w.Radius);
            int used = 0;
            for (int pick = 0; pick < _pool.Count; pick++)
            {
                int best = -1;
                for (int i = 0; i < _candidates.Count; i++)
                    if (_distances[i] > 25f && _distances[i] <= reach && (best < 0 || _distances[i] < _distances[best])) best = i;
                if (best < 0) break;
                Placed p = _candidates[best];
                _pool[used++].Put(w.Frame, p.Local, p.Dir, ColourOf(p), _distances[best], w.Radius);
                _distances[best] = -1f;
            }
            for (int i = used; i < _pool.Count; i++) _pool[i].Show(false);

            // Supply drops carry their own light, on while falling and until you are close.
            for (int i = 0; i < _drops.Count; i++)
            {
                DropTask d = _drops[i];
                if (d.OwnLight == null || d.View == null) continue;
                float dist = (d.View.transform.localPosition - local).magnitude;
                bool on = d.Rule.State == DropState.Falling || (d.Rule.State == DropState.Landed && dist > 25f);
                if (d.OwnLight.activeSelf != on) d.OwnLight.SetActive(on);
                if (on) FitBeam(d.OwnLight.transform, dist, w.Radius);
            }
        }

        void ShowNoLights()
        {
            for (int i = 0; i < _pool.Count; i++) _pool[i].Show(false);
        }

        /// <summary>For the system map: how many tasks are still to do on a world (-1 if not known yet).</summary>
        /// <summary>
        /// What is still to score on the world the player is at (its neighbourhood), for the line under the ante readout.
        /// False in deep space, at a world with no tasks laid out yet, or with no focus.
        /// </summary>
        public bool TryCountsHere(out string world, out int masts, out int crates)
        {
            world = null;
            masts = crates = 0;
            if (_world == null || _world.System == null || _world.System.Focus == null) return false;
            int body = _world.System.Focus.Neighbourhood;
            WorldTasks w = WorldFor(body);
            if (w == null) return false;
            world = _world.System.Model.Spec(body).displayName;
            for (int i = 0; i < w.Masts.Count; i++) if (w.Masts[i].Live) masts++;
            for (int i = 0; i < w.Crates.Count; i++) if (w.Crates[i].Rule.AwaitingDelivery) crates++;
            return true;
        }

        public int TasksLeftOn(int body)
        {
            WorldTasks w = WorldFor(body);
            if (w == null) return -1;
            int n = 0;
            for (int i = 0; i < w.Masts.Count; i++) if (w.Masts[i].Live) n++;
            for (int i = 0; i < w.Crates.Count; i++) if (w.Crates[i].Rule.AwaitingDelivery) n++;
            return n;
        }
    }
}
