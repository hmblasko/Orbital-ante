using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Melee;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The melee slot's rules with no Unity objects: the swing's timing, what it can reach, what each of the five
    /// verbs does to a body, the freeze cap, the sword's heat, the zero-G push back and where the items lie.
    /// The numbers are the research report's first guesses; these tests hold the rules, not the feel.
    /// </summary>
    public class MeleeRulesTests
    {
        // The base enemy's hit points (MannequinConfig defaults, the table the report's damage was set against).
        static float[] BaseHp()
        {
            var hp = new float[BodyLayout.Count];
            for (int i = 0; i < hp.Length; i++)
            {
                switch ((BodyPartId)i)
                {
                    case BodyPartId.Head: hp[i] = 90f; break;
                    case BodyPartId.Torso: hp[i] = 240f; break;
                    case BodyPartId.LeftUpperArm: case BodyPartId.RightUpperArm: hp[i] = 60f; break;
                    case BodyPartId.LeftForearm: case BodyPartId.RightForearm: hp[i] = 45f; break;
                    case BodyPartId.LeftHand: case BodyPartId.RightHand: hp[i] = 30f; break;
                    case BodyPartId.LeftThigh: case BodyPartId.RightThigh: hp[i] = 68f; break;
                    case BodyPartId.LeftShin: case BodyPartId.RightShin: hp[i] = 50f; break;
                    default: hp[i] = 30f; break;
                }
            }
            return hp;
        }

        static BodyModel Body(float scale = 1f) => new BodyModel(BaseHp().Select(h => h * scale).ToArray());

        static DamageResult Apply(BodyModel body, MeleeBlow blow) =>
            blow.Sever ? body.ApplySever(blow.Part, blow.Damage) : body.ApplyDamage(blow.Part, blow.Damage);

        static MeleeCandidate C(int target, BodyPartId part, float distance, float yaw, float pitch = 0f, bool living = true) =>
            new MeleeCandidate { Target = target, Part = (int)part, Living = living, Distance = distance, Yaw = yaw, Pitch = pitch };

        // ------------------------------------------------------------------ the catalogue

        [Test]
        public void TheFiveItems_AreAllValid_AndEachHasItsOwnVerb()
        {
            var verbs = new HashSet<MeleeVerb>();
            foreach (string id in MeleeCatalog.Ids)
            {
                MeleeSpec s = MeleeCatalog.Defaults(id);
                Assert.IsNotNull(s, id);
                Assert.AreEqual(id, s.Id);
                var problems = new List<string>();
                s.Validate(problems);
                CollectionAssert.IsEmpty(problems, id + ": " + string.Join("; ", problems));
                Assert.IsTrue(verbs.Add(s.Verb), id + " repeats a verb");
            }
            Assert.AreEqual(5, verbs.Count);
            Assert.IsNull(MeleeCatalog.Defaults("Plunger"), "the plunger is not built");
        }

        [Test]
        public void TheDefaults_AreTheResearchReportsFirstGuesses()
        {
            MeleeSpec sword = MeleeCatalog.LaserSword();
            Assert.AreEqual(0.12f, sword.Windup); Assert.AreEqual(0.10f, sword.Active); Assert.AreEqual(0.35f, sword.Recovery);
            Assert.AreEqual(2.0f, sword.Reach); Assert.AreEqual(70f, sword.Arc);
            Assert.AreEqual(45f, sword.HeadDamage); Assert.AreEqual(120f, sword.TorsoDamage); Assert.AreEqual(70f, sword.ThighDamage);
            Assert.AreEqual(5, sword.HeatSwings); Assert.AreEqual(2.5f, sword.HeatLockSeconds);

            MeleeSpec bat = MeleeCatalog.BaseballBat();
            Assert.AreEqual(2.1f, bat.Reach); Assert.AreEqual(90f, bat.Arc); Assert.AreEqual(0.4f, bat.SweetSpot);
            Assert.AreEqual(1.5f, bat.SweetMultiplier); Assert.AreEqual(4f, bat.LaunchSpeed); Assert.AreEqual(8f, bat.SweetLaunchSpeed);
            Assert.AreEqual(100f, bat.SweetHitstopMs);

            MeleeSpec box = MeleeCatalog.Mailbox();
            Assert.AreEqual(3, box.MaxTargets); Assert.AreEqual(120f, box.Arc); Assert.AreEqual(0.80f, box.Recovery);
            Assert.AreEqual(0.85f, box.WalkSlow, 1e-6);

            MeleeSpec knife = MeleeCatalog.ButterflyKnife();
            Assert.AreEqual(25f, knife.TorsoDamage); Assert.AreEqual(90f, knife.TakedownDamage); Assert.AreEqual(90f, knife.RearArc);
            Assert.AreEqual(1.5f, knife.Reach);

            MeleeSpec pan = MeleeCatalog.FryingPan();
            Assert.AreEqual(100f, pan.BonusHitstopMs);
        }

        [Test]
        public void Rarity_PanMostCommon_KnifeAndBatUncommon_MailboxRare_SwordRarest()
        {
            float pan = MeleeCatalog.FryingPan().RarityWeight, knife = MeleeCatalog.ButterflyKnife().RarityWeight,
                  bat = MeleeCatalog.BaseballBat().RarityWeight, box = MeleeCatalog.Mailbox().RarityWeight,
                  sword = MeleeCatalog.LaserSword().RarityWeight;
            Assert.Greater(pan, knife);
            Assert.AreEqual(knife, bat);
            Assert.Greater(bat, box);
            Assert.Greater(box, sword);
            Assert.Greater(sword, 0f);
        }

        // ------------------------------------------------------------------ the swing's timing

        [Test]
        public void ASwing_GoesWindup_Active_Recovery_Ready_OnTheItemsTimes()
        {
            MeleeSpec s = MeleeCatalog.FryingPan();
            var swing = new MeleeSwing(s);
            Assert.AreEqual(SwingPhase.Ready, swing.Phase);
            Assert.IsTrue(swing.Press());
            Assert.AreEqual(SwingPhase.Windup, swing.Phase);
            swing.Tick(s.Windup - 0.01);
            Assert.AreEqual(SwingPhase.Windup, swing.Phase);
            Assert.IsFalse(swing.CanHit);
            swing.Tick(0.02);
            Assert.AreEqual(SwingPhase.Active, swing.Phase);
            Assert.IsTrue(swing.CanHit);
            Assert.IsFalse(swing.Press(), "no second swing in the middle of one");
            swing.Tick(s.Active);
            Assert.AreEqual(SwingPhase.Recovery, swing.Phase);
            Assert.IsFalse(swing.CanHit);
            swing.Tick(s.Recovery);
            Assert.AreEqual(SwingPhase.Ready, swing.Phase);
            Assert.IsTrue(swing.Press());
        }

        [Test]
        public void ALongFrame_NeverJumpsOverTheHitWindow()
        {
            MeleeSpec s = MeleeCatalog.ButterflyKnife();
            var swing = new MeleeSwing(s);
            swing.Press();
            swing.Tick(1.0);   // longer than the whole swing
            Assert.AreEqual(SwingPhase.Active, swing.Phase, "the swing gets its one chance to hit");
            Assert.IsTrue(swing.CanHit);
            swing.Tick(1.0);
            Assert.AreEqual(SwingPhase.Ready, swing.Phase);
        }

        [Test]
        public void APressLateInRecovery_StartsTheNextSwingOnTime_ButAnEarlyOneIsDropped()
        {
            MeleeSpec s = MeleeCatalog.Mailbox();
            var swing = new MeleeSwing(s);
            swing.Press();
            swing.Tick(s.Windup);
            swing.Tick(s.Active + 0.05);
            Assert.AreEqual(SwingPhase.Recovery, swing.Phase);
            swing.Press();   // far from the end: forgotten
            swing.Tick(s.Recovery);
            Assert.AreEqual(SwingPhase.Ready, swing.Phase);

            swing.Press();
            swing.Tick(s.Windup);
            swing.Tick(s.Active);
            swing.Tick(s.Recovery - 0.1);
            Assert.AreEqual(SwingPhase.Recovery, swing.Phase);
            swing.Press();   // inside the last 0.15 s: kept
            Assert.IsTrue(swing.Tick(0.2), "the kept press starts the next swing");
            Assert.AreEqual(SwingPhase.Windup, swing.Phase);
        }

        [Test]
        public void ASwing_StrikesEachPartOnce_AndOnlyInItsHitWindow()
        {
            MeleeSpec s = MeleeCatalog.BaseballBat();
            var swing = new MeleeSwing(s);
            swing.Press();
            Assert.IsFalse(swing.Strike(7), "not in the windup");
            swing.Tick(s.Windup);
            Assert.IsTrue(swing.Strike(7));
            Assert.IsFalse(swing.Strike(7), "one hit per part per swing");
            Assert.IsTrue(swing.Strike(8), "another part is a separate hit");
            swing.MarkContact();
            Assert.IsFalse(swing.CanHit, "one contact per swing");
            swing.Tick(s.Cycle);
            swing.Press();
            swing.Tick(s.Windup);
            Assert.IsTrue(swing.Strike(7), "a new swing can hit it again");
        }

        // ------------------------------------------------------------------ what the swing reaches

        [Test]
        public void OnlyWhatIsInsideTheReachAndTheArc_IsHit()
        {
            MeleeSpec s = MeleeCatalog.FryingPan();   // 1.8 m, 60 degrees
            float v = MeleeRules.VerticalHalfAngle;
            Assert.IsTrue(MeleeTargeting.InSwing(C(0, BodyPartId.Torso, 1.79f, 29f), s, v));
            Assert.IsFalse(MeleeTargeting.InSwing(C(0, BodyPartId.Torso, 1.81f, 0f), s, v), "out of reach");
            Assert.IsFalse(MeleeTargeting.InSwing(C(0, BodyPartId.Torso, 1.0f, 31f), s, v), "outside the arc");
            Assert.IsFalse(MeleeTargeting.InSwing(C(0, BodyPartId.Torso, 1.0f, 0f, 41f), s, v), "too far above");
        }

        [Test]
        public void OnEachEnemy_ThePartNearestTheCrosshair_IsTheOneHit_LivingFirst()
        {
            MeleeSpec s = MeleeCatalog.FryingPan();
            var all = new List<MeleeCandidate>
            {
                C(0, BodyPartId.LeftHand, 1.0f, 20f),
                C(0, BodyPartId.Torso, 1.2f, 2f),
                C(0, BodyPartId.Head, 1.3f, 1f, 12f),
                C(1, BodyPartId.LeftShin, 0.5f, 0.5f, 0f, living: false),   // a loose limb right in the middle
            };
            var chosen = new List<MeleeCandidate>();
            Assert.AreEqual(1, MeleeTargeting.Choose(all, s, MeleeRules.VerticalHalfAngle, chosen));
            Assert.AreEqual(0, chosen[0].Target, "a living enemy comes before a loose limb");
            Assert.AreEqual((int)BodyPartId.Torso, chosen[0].Part, "the part nearest the crosshair");
        }

        [Test]
        public void TheMailbox_HitsUpToThree_ButASwordCutsExactlyOnePart()
        {
            var all = new List<MeleeCandidate>();
            for (int t = 0; t < 5; t++) all.Add(C(t, BodyPartId.Torso, 1.5f, -50f + t * 25f));
            var chosen = new List<MeleeCandidate>();
            Assert.AreEqual(3, MeleeTargeting.Choose(all, MeleeCatalog.Mailbox(), MeleeRules.VerticalHalfAngle, chosen));
            Assert.AreEqual(2, chosen[0].Target, "nearest the crosshair first");

            MeleeSpec greedySword = MeleeCatalog.LaserSword();
            greedySword.MaxTargets = 4;
            Assert.AreEqual(1, MeleeTargeting.Choose(all, greedySword, MeleeRules.VerticalHalfAngle, chosen), "a cut is always one part");
        }

        // ------------------------------------------------------------------ each verb on a body

        [Test]
        public void TheLaserSword_SeversExactlyTheOnePartItHits_WithEverythingBelowIt()
        {
            MeleeSpec s = MeleeCatalog.LaserSword();
            var body = Body(3f);   // far tougher than the base enemy: a cut still takes the limb
            MeleeBlow blow = MeleeRules.Blow(s, BodyPartId.RightForearm, body.Hp(BodyPartId.RightForearm), true, false, false);
            Assert.IsTrue(blow.Sever);
            DamageResult r = Apply(body, blow);
            Assert.AreEqual(1, r.Detachments.Count);
            Assert.AreEqual(BodyPartId.RightForearm, r.Detachments[0].Root);
            CollectionAssert.AreEquivalent(new[] { BodyPartId.RightForearm, BodyPartId.RightHand }, r.Detachments[0].Members);
            Assert.IsTrue(body.OnBody(BodyPartId.RightUpperArm), "nothing above the cut");
            Assert.IsTrue(body.Alive);
        }

        [Test]
        public void TheLaserSword_NeverOneShotsAHead_AndTakesTwoSwingsForATorso()
        {
            MeleeSpec s = MeleeCatalog.LaserSword();
            var body = Body();
            MeleeBlow head = MeleeRules.Blow(s, BodyPartId.Head, 90f, true, false, false);
            Assert.IsFalse(head.Sever);
            Assert.IsFalse(Apply(body, head).Killed);
            Assert.IsTrue(Apply(body, head).Killed, "the second swing kills");

            var other = Body();
            MeleeBlow torso = MeleeRules.Blow(s, BodyPartId.Torso, 240f, true, false, false);
            Assert.AreEqual(0, Apply(other, torso).Detachments.Count);
            Assert.Greater(Apply(other, torso).Detachments.Count, 0, "two swings destroy the torso");
        }

        [Test]
        public void ApplySever_TakesALimbWhateverItsHitPoints_ButIsAnOrdinaryHitOnHeadAndTorso()
        {
            var body = Body(10f);
            DamageResult thigh = body.ApplySever(BodyPartId.LeftThigh, 1f);
            Assert.AreEqual(1, thigh.Detachments.Count);
            CollectionAssert.AreEquivalent(new[] { BodyPartId.LeftThigh, BodyPartId.LeftShin, BodyPartId.LeftFoot }, thigh.Detachments[0].Members);
            Assert.AreEqual(0, body.ApplySever(BodyPartId.Torso, 1f).Detachments.Count);
            Assert.AreEqual(0, body.ApplySever(BodyPartId.Head, 1f).Detachments.Count);
            Assert.IsTrue(body.Alive);
            // The top of a loose chunk has no joint above it: nothing to cut there.
            int group = body.Group(BodyPartId.LeftThigh);
            Assert.AreEqual(0, body.ApplySever(BodyPartId.LeftThigh, 1f).Detachments.Count);
            Assert.AreEqual(group, body.Group(BodyPartId.LeftThigh));
        }

        [Test]
        public void TheMailbox_BreaksOffAnyLimbItTouches_EvenOnATougherEnemy()
        {
            MeleeSpec s = MeleeCatalog.Mailbox();
            BodyPartId[] limbs =
            {
                BodyPartId.LeftUpperArm, BodyPartId.LeftForearm, BodyPartId.LeftHand, BodyPartId.RightUpperArm,
                BodyPartId.RightForearm, BodyPartId.RightHand, BodyPartId.LeftThigh, BodyPartId.LeftShin,
                BodyPartId.LeftFoot, BodyPartId.RightThigh, BodyPartId.RightShin, BodyPartId.RightFoot,
            };
            foreach (BodyPartId limb in limbs)
            {
                var body = Body(1.5f);
                DamageResult r = Apply(body, MeleeRules.Blow(s, limb, body.Hp(limb), true, false, false));
                Assert.AreEqual(1, r.Detachments.Count, limb.ToString());
                Assert.AreEqual(limb, r.Detachments[0].Root);
            }
        }

        [Test]
        public void TheMailbox_OnAThigh_AlwaysDropsTheEnemy_AndOnTheTorso_KnocksItFlat()
        {
            MeleeSpec s = MeleeCatalog.Mailbox();
            var body = Body(2f);
            Assert.AreEqual(MovementState.Intact, body.State);
            Apply(body, MeleeRules.Blow(s, BodyPartId.RightThigh, body.Hp(BodyPartId.RightThigh), true, false, false));
            Assert.AreEqual(MovementState.Crawl, body.State, "down on the ground");

            MeleeBlow torso = MeleeRules.Blow(s, BodyPartId.Torso, 240f, true, false, false);
            Assert.IsTrue(torso.Knockdown);
            Assert.AreEqual(s.LaunchSpeed, torso.Shove);
            Assert.IsFalse(torso.Sever);
            MeleeBlow head = MeleeRules.Blow(s, BodyPartId.Head, 90f, true, false, false);
            Assert.IsFalse(head.Sever, "the head is not a limb: 60 of 90");
            Assert.AreEqual(60f, head.Damage);
        }

        [Test]
        public void TheBat_KnocksALivingEnemyDown_HarderAndFurtherFromItsSweetSpot()
        {
            MeleeSpec s = MeleeCatalog.BaseballBat();
            Assert.IsFalse(MeleeRules.Sweet(s, 1.6f));
            Assert.IsTrue(MeleeRules.Sweet(s, 1.75f));
            Assert.IsTrue(MeleeRules.Sweet(s, 2.1f));

            MeleeBlow normal = MeleeRules.Blow(s, BodyPartId.Torso, 240f, true, false, false);
            MeleeBlow sweet = MeleeRules.Blow(s, BodyPartId.Torso, 240f, true, true, false);
            Assert.IsTrue(normal.Knockdown);
            Assert.AreEqual(4f, normal.Shove);
            Assert.AreEqual(8f, sweet.Shove);
            Assert.AreEqual(60f, normal.Damage);
            Assert.AreEqual(90f, sweet.Damage, 1e-4);
            Assert.AreEqual(0.06f, MeleeRules.HitstopSeconds(s, false, 0f, 1), 1e-5);
            Assert.AreEqual(0.10f, MeleeRules.HitstopSeconds(s, true, 0f, 1), 1e-5);

            MeleeBlow corpse = MeleeRules.Blow(s, BodyPartId.Torso, 240f, false, false, false);
            Assert.IsFalse(corpse.Knockdown, "corpses and loose pieces just fly");
        }

        [Test]
        public void TheBat_PopsHandsAndFeet_ButRarelyABiggerLimb()
        {
            MeleeSpec s = MeleeCatalog.BaseballBat();
            var body = Body();
            Assert.AreEqual(1, Apply(body, MeleeRules.Blow(s, BodyPartId.RightHand, 30f, true, false, false)).Detachments.Count);
            Assert.AreEqual(1, Apply(body, MeleeRules.Blow(s, BodyPartId.LeftFoot, 30f, true, false, false)).Detachments.Count);
            Assert.AreEqual(0, Apply(body, MeleeRules.Blow(s, BodyPartId.LeftUpperArm, 60f, true, false, false)).Detachments.Count);
        }

        [Test]
        public void TheKnife_AHandStabAlwaysDisarms_FromTheFront()
        {
            MeleeSpec s = MeleeCatalog.ButterflyKnife();
            var body = Body(1.6f);   // a hand with 48 hit points still comes off
            MeleeBlow blow = MeleeRules.Blow(s, BodyPartId.RightHand, body.Hp(BodyPartId.RightHand), true, false, false);
            DamageResult r = Apply(body, blow);
            Assert.AreEqual(1, r.Detachments.Count);
            Assert.IsFalse(body.HandUsable(false), "the gun hand is gone: the gun falls");
            Assert.AreEqual(25f, MeleeRules.Blow(s, BodyPartId.Torso, 240f, true, false, false).Damage, "weak from the front");
        }

        [Test]
        public void TheKnifesTakedown_OnlyFromBehind_OnAnEnemyThatHasNotSeenYouLately()
        {
            MeleeSpec s = MeleeCatalog.ButterflyKnife();
            // 180 is right behind; the rear arc is 90 degrees wide, so 135 and more counts.
            Assert.IsTrue(MeleeRules.IsTakedown(s, true, true, 180f, false));
            Assert.IsTrue(MeleeRules.IsTakedown(s, true, true, 136f, false));
            Assert.IsFalse(MeleeRules.IsTakedown(s, true, true, 120f, false), "beside it is not behind it");
            Assert.IsFalse(MeleeRules.IsTakedown(s, true, true, 0f, false), "face to face");
            Assert.IsFalse(MeleeRules.IsTakedown(s, true, true, 180f, true), "it has seen you lately");
            Assert.IsFalse(MeleeRules.IsTakedown(s, false, true, 180f, false), "dead already");
            Assert.IsFalse(MeleeRules.IsTakedown(s, true, false, 180f, false), "no head left on it");
            Assert.IsFalse(MeleeRules.IsTakedown(MeleeCatalog.FryingPan(), true, true, 180f, false), "only a stab");
        }

        [Test]
        public void ATakedown_StrikesTheHead_AndKills_EvenATougherHead()
        {
            MeleeSpec s = MeleeCatalog.ButterflyKnife();
            var body = Body(1.4f);   // head 126
            bool cue = MeleeRules.IsTakedown(s, true, body.OnBody(BodyPartId.Head), 170f, false);
            Assert.IsTrue(cue, "the HUD's cue");
            // The stab asks the very same question and passes its answer on.
            MeleeBlow blow = MeleeRules.Blow(s, BodyPartId.Torso, body.Hp(BodyPartId.Head), true, false, cue);
            Assert.IsTrue(blow.Takedown);
            Assert.AreEqual(BodyPartId.Head, blow.Part);
            Assert.IsTrue(Apply(body, blow).Killed);

            var front = Body();
            Assert.IsFalse(Apply(front, MeleeRules.Blow(s, BodyPartId.Head, 90f, true, false, false)).Killed, "a head stab from the front is 25");
        }

        [Test]
        public void ThePan_OnAHead_Rings_Startles_AndFreezesLonger_UpToTheCap()
        {
            MeleeSpec s = MeleeCatalog.FryingPan();
            MeleeBlow head = MeleeRules.Blow(s, BodyPartId.Head, 90f, true, false, false);
            Assert.IsTrue(head.Clang);
            Assert.IsTrue(head.Startle);
            Assert.AreEqual(100f, head.ExtraHitstopMs);
            Assert.AreEqual(0.12f, MeleeRules.HitstopSeconds(s, false, head.ExtraHitstopMs, 1), 1e-5, "60 + 100 is capped at 120 ms");
            MeleeBlow torso = MeleeRules.Blow(s, BodyPartId.Torso, 240f, true, false, false);
            Assert.IsFalse(torso.Clang);
            Assert.IsFalse(torso.Startle);
            Assert.IsFalse(MeleeRules.Blow(s, BodyPartId.Head, 90f, false, false, false).Clang, "a loose head does not ring");
        }

        [Test]
        public void TheFreeze_IsNeverOver120ms_AndShorterWhenOneSwingHitsSeveral()
        {
            MeleeSpec box = MeleeCatalog.Mailbox();
            Assert.AreEqual(0.12f, MeleeRules.HitstopSeconds(box, false, 0f, 1), 1e-5);
            Assert.AreEqual(0.08f, MeleeRules.HitstopSeconds(box, false, 0f, 3), 1e-5);
            Assert.Less(MeleeRules.HitstopSeconds(box, false, 0f, 3), MeleeRules.HitstopSeconds(box, false, 0f, 2));
            MeleeSpec silly = MeleeCatalog.FryingPan();
            silly.HitstopMs = 5000f;
            Assert.AreEqual(MeleeRules.HitstopCapMs / 1000f, MeleeRules.HitstopSeconds(silly, true, 9000f, 1), 1e-6);
        }

        // ------------------------------------------------------------------ the sword's heat

        [Test]
        public void TheSword_OverheatsOnItsFifthQuickSwing_AndLocksFor2Point5Seconds()
        {
            MeleeSpec s = MeleeCatalog.LaserSword();
            var heat = new MeleeHeat(s);
            for (int i = 1; i <= 4; i++)
            {
                Assert.IsFalse(heat.Swung(), "swing " + i);
                heat.Tick(s.Cycle);   // back to back, as fast as it swings
            }
            Assert.IsTrue(heat.Swung(), "the fifth overheats it");
            Assert.IsTrue(heat.Locked);
            Assert.AreEqual(1f, heat.Fill, 1e-6);
            heat.Tick(2.4);
            Assert.IsTrue(heat.Locked);
            heat.Tick(0.11);
            Assert.IsFalse(heat.Locked);
            Assert.AreEqual(0.0, heat.Heat, 1e-9, "cool again after the lock");
        }

        [Test]
        public void TheSword_CoolsWhenRested_SoMeasuredSwingingNeverLocks()
        {
            MeleeSpec s = MeleeCatalog.LaserSword();
            var heat = new MeleeHeat(s);
            for (int i = 0; i < 40; i++)
            {
                Assert.IsFalse(heat.Swung(), "swing " + i);
                heat.Tick(1.5);   // a breath between swings
            }
            var none = new MeleeHeat(MeleeCatalog.BaseballBat());
            for (int i = 0; i < 100; i++) Assert.IsFalse(none.Swung());
            Assert.IsFalse(none.Overheats);
        }

        // ------------------------------------------------------------------ zero-G, noise, input

        [Test]
        public void InZeroG_TheHeavierTheThingHit_TheMoreYouArePushedBack_Capped()
        {
            float limb = MeleeRules.PushBack(4f, 120f, 9f, 3f);
            float body = MeleeRules.PushBack(70f, 120f, 9f, 3f);
            float wall = MeleeRules.PushBack(float.PositiveInfinity, 120f, 9f, 3f);
            Assert.Less(limb, body);
            Assert.Less(limb, 0.5f, "swatting a loose limb barely moves you");
            Assert.AreEqual(3f, body, 1e-5, "capped");
            Assert.AreEqual(3f, wall, 1e-5);
            Assert.AreEqual(0f, MeleeRules.PushBack(0f, 120f, 9f, 3f), "a miss pushes nothing");
        }

        [Test]
        public void AShovedBody_SlidesToAStop()
        {
            float speed = 8f;
            int steps = 0;
            while (speed > 0f && steps < 1000) { speed = MeleeRules.SlideAfter(speed, 0.02f, 6f); steps++; }
            Assert.AreEqual(0f, speed);
            Assert.Less(steps * 0.02f, 1.5f);
            Assert.LessOrEqual(MeleeRules.Blow(MeleeCatalog.BaseballBat(), BodyPartId.Torso, 1f, true, true, false).Shove, MeleeRules.MaxShove);
        }

        [Test]
        public void TheSwordsHum_IsHeardWhateverYourStance()
        {
            Assert.AreEqual(1f, MeleeRules.HeardFrom(0f, MeleeCatalog.LaserSword()), "keeping still with it lit is still heard");
            Assert.AreEqual(0.3f, MeleeRules.HeardFrom(0.3f, MeleeCatalog.ButterflyKnife()), 1e-6, "the knife makes no sound");
            Assert.AreEqual(2f, MeleeRules.HeardFrom(2f, MeleeCatalog.LaserSword()), "never quieter than a shot");
            Assert.AreEqual(0.4f, MeleeRules.HeardFrom(0.4f, null), 1e-6);
        }

        [Test]
        public void TheSharedPadButton_RollsWhileFloatingFree_AndSwingsEverywhereElse()
        {
            Assert.IsTrue(MeleeRules.SwingFromPress(sharedWithRoll: true, floatingFree: false));
            Assert.IsFalse(MeleeRules.SwingFromPress(sharedWithRoll: true, floatingFree: true));
            Assert.IsTrue(MeleeRules.SwingFromPress(sharedWithRoll: false, floatingFree: true), "a rebound melee button always swings");
        }

        [Test]
        public void SoundEvents_AreNamedTheAudioWay_AndTheWordsDropThePlaceholderTag()
        {
            Assert.AreEqual("melee.laser_sword.hum", MeleeRules.SoundEvent(MeleeCatalog.LaserSwordId, "hum"));
            Assert.AreEqual("melee.frying_pan.clang", MeleeRules.SoundEvent(MeleeCatalog.FryingPanId, "clang"));
            Assert.AreEqual("melee.mailbox.hit", MeleeRules.SoundEvent(MeleeCatalog.MailboxId, "hit"));
            Assert.AreEqual("Mailbox", MeleeWords.Short("Mailbox (name TBD)"));
            StringAssert.Contains("OVERHEATED", MeleeWords.HeldLine(MeleeCatalog.LaserSword(), "V", true));
            StringAssert.StartsWith("ON THE GROUND: FRYING PAN.", MeleeWords.Note(MeleeCatalog.FryingPan(), true, "E"));
        }

        // ------------------------------------------------------------------ where they lie

        [Test]
        public void AboutOneBaseInFour_CouldHoldAMeleeItem()
        {
            int yes = 0;
            const int n = 20000;
            for (int i = 0; i < n; i++) if (MeleeLoot.BaseHoldsOne(i * 7919 + 13)) yes++;
            Assert.AreEqual(0.25, yes / (double)n, 0.02);
        }

        [Test]
        public void TheMeleeBase_IsTheSameForASeed_AndNeverTheRareGunsBase()
        {
            var rng = new SeededRandom(42);
            for (int world = 0; world < 500; world++)
            {
                var seeds = new List<int>();
                int count = rng.Range(1, 9);
                for (int i = 0; i < count; i++) seeds.Add(rng.Next());
                int rare = Combat.RareGuns.PickBase(seeds);
                int melee = MeleeLoot.PickBase(seeds, rare);
                Assert.AreEqual(melee, MeleeLoot.PickBase(seeds, rare), "same seeds, same base");
                if (melee >= 0)
                {
                    Assert.AreNotEqual(rare, melee);
                    Assert.IsTrue(MeleeLoot.BaseHoldsOne(seeds[melee]));
                    for (int i = 0; i < melee; i++) Assert.IsTrue(i == rare || !MeleeLoot.BaseHoldsOne(seeds[i]), "the first that qualifies");
                }
            }
            Assert.AreEqual(-1, MeleeLoot.PickBase(null, -1));
        }

        [Test]
        public void ItLiesAtTheLastLootPoint_NeverOnTheRareGunsFirstOne()
        {
            Assert.AreEqual(3, MeleeLoot.LootPoint(4, false));
            Assert.AreEqual(3, MeleeLoot.LootPoint(4, true));
            Assert.AreEqual(0, MeleeLoot.LootPoint(1, false));
            Assert.AreEqual(-1, MeleeLoot.LootPoint(1, true), "no room beside the rare gun");
            Assert.AreEqual(-1, MeleeLoot.LootPoint(0, false));
        }

        [Test]
        public void WhichItem_FollowsTheWeights_AndRepeatsForASeed()
        {
            var specs = MeleeCatalog.Ids.Select(MeleeCatalog.Defaults).ToList();
            var counts = MeleeCatalog.Ids.ToDictionary(id => id, id => 0);
            var rng = SeededRandom.ForStream(7, "melee.loot.Mars");
            for (int i = 0; i < 40000; i++) counts[MeleeLoot.PickItem(rng.NextDouble(), specs)]++;
            Assert.Greater(counts[MeleeCatalog.FryingPanId], counts[MeleeCatalog.ButterflyKnifeId]);
            Assert.Greater(counts[MeleeCatalog.BaseballBatId], counts[MeleeCatalog.MailboxId]);
            Assert.Greater(counts[MeleeCatalog.ButterflyKnifeId], counts[MeleeCatalog.MailboxId]);
            Assert.Greater(counts[MeleeCatalog.MailboxId], counts[MeleeCatalog.LaserSwordId]);
            Assert.Greater(counts[MeleeCatalog.LaserSwordId], 0);
            Assert.AreEqual(0.05, counts[MeleeCatalog.LaserSwordId] / 40000.0, 0.01);

            double a = SeededRandom.ForStream(99, "melee.loot.Moon").NextDouble();
            double b = SeededRandom.ForStream(99, "melee.loot.Moon").NextDouble();
            Assert.AreEqual(MeleeLoot.PickItem(a, specs), MeleeLoot.PickItem(b, specs));

            specs.ForEach(x => x.RarityWeight = 0f);
            Assert.IsNull(MeleeLoot.PickItem(0.5, specs), "nothing with weight, nothing picked");
        }
    }
}
