using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using OrbitalAnte.Tasks;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>
    /// Where the creatures of the land live: on dry, gentle ground, well apart, clear of the launch pad, the camps
    /// and the bases, each world with its own animals, more of them each ante, the same for a seed every time.
    /// </summary>
    public class LandRulesTests
    {
        const double Radius = 500;

        // A world whose southern third is sea.
        static bool Dry(Vec3d d) => d.Y > -0.35;

        static double Apart(Vec3d a, Vec3d b, double radius) => Math.Acos(Math.Max(-1, Math.Min(1, Vec3d.Dot(a, b)))) * radius;

        static readonly LandMix Mixed = new LandMix(1, 1, 1, 1);

        [Test]
        public void Haunts_AreOnGroundThatFits_WellApart_AndClearOfThePadAndTheCamps()
        {
            var rules = new LandFieldRules();
            Vec3d pad = new Vec3d(0, 1, 0);
            var camps = new List<Vec3d> { new Vec3d(1, 0, 0), new Vec3d(0, 0, 1), new Vec3d(-1, 0.2, 0.3).Normalized, new Vec3d(0.4, 0.5, -0.7).Normalized };
            List<LandHaunt> haunts = LandField.Build(rules, new SeededRandom(11), Radius, Mixed, Dry, pad, camps);
            Assert.Greater(haunts.Count, 8, "a world this size, mostly land, has many");
            Assert.LessOrEqual(haunts.Count, rules.mostHaunts);
            for (int i = 0; i < haunts.Count; i++)
            {
                Vec3d d = haunts[i].Direction;
                Assert.AreEqual(1.0, d.Magnitude, 1e-9);
                Assert.IsTrue(Dry(d), "never in the sea");
                Assert.GreaterOrEqual(Apart(d, pad, Radius), rules.padClear - 1e-6, "the first minutes of a run are the player's");
                for (int c = 0; c < camps.Count; c++)
                    Assert.GreaterOrEqual(Apart(d, camps[c], Radius), rules.campClear - 1e-6, "a creature's fight never starts on top of a camp's");
                for (int j = 0; j < i; j++)
                    Assert.GreaterOrEqual(Apart(d, haunts[j].Direction, Radius), rules.spacing - 1e-6, "no two haunts crowd each other");
                Assert.GreaterOrEqual(haunts[i].Lot, 0.0);
                Assert.Less(haunts[i].Lot, 1.0);
            }
        }

        [Test]
        public void TheSameSeed_GivesTheSameHaunts_AndAnotherSeedOthers()
        {
            var rules = new LandFieldRules();
            List<LandHaunt> a = LandField.Build(rules, new SeededRandom(9), Radius, Mixed, Dry, new Vec3d(0, 1, 0), null);
            List<LandHaunt> b = LandField.Build(rules, new SeededRandom(9), Radius, Mixed, Dry, new Vec3d(0, 1, 0), null);
            List<LandHaunt> c = LandField.Build(rules, new SeededRandom(10), Radius, Mixed, Dry, new Vec3d(0, 1, 0), null);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Direction.X, b[i].Direction.X, 0.0);
                Assert.AreEqual(a[i].Seed, b[i].Seed);
                Assert.AreEqual(a[i].Kind, b[i].Kind);
                Assert.AreEqual(a[i].Lot, b[i].Lot, 0.0);
            }
            Assert.Greater(a.Count, 0);
            Assert.Greater(c.Count, 0);
            Assert.Greater(Math.Abs(a[0].Direction.X - c[0].Direction.X), 1e-9, "another seed puts them elsewhere");
        }

        [Test]
        public void TheCount_FollowsTheWorldsSize_AndAWorldTooSmallOrWithNoAnimalsHasNone()
        {
            var rules = new LandFieldRules();
            Assert.AreEqual(0, LandField.HauntCount(rules, 40, Mixed), "Phobos is too small for a 7 m crab");
            Assert.AreEqual(0, LandField.HauntCount(rules, 500, LandMix.None), "a world with no animals has none");
            int moon = LandField.HauntCount(rules, 150, Mixed), mars = LandField.HauntCount(rules, 350, Mixed);
            Assert.GreaterOrEqual(moon, rules.leastHaunts);
            Assert.Greater(mars, moon, "a bigger world has more");
            Assert.LessOrEqual(LandField.HauntCount(rules, 5000, Mixed), rules.mostHaunts);
            rules.creaturesOnLand = false;
            Assert.AreEqual(0, LandField.HauntCount(rules, 350, Mixed), "the switch leaves the ground to the camps");
            Assert.AreEqual(0, LandField.Build(rules, new SeededRandom(1), 350, Mixed, null, Vec3d.Zero, null).Count);
        }

        [Test]
        public void ASmallWorldFullOfCamps_StillHasRoom()
        {
            var rules = new LandFieldRules();
            // The Moon (150 m) with camps all round it: the distances asked for cannot all fit, and it still gets its least.
            var camps = new List<Vec3d>();
            var random = new SeededRandom(4);
            for (int i = 0; i < 12; i++)
            {
                double y = random.NextDouble() * 2 - 1, a = random.NextDouble() * Math.PI * 2, r = Math.Sqrt(1 - y * y);
                camps.Add(new Vec3d(r * Math.Cos(a), y, r * Math.Sin(a)));
            }
            List<LandHaunt> haunts = LandField.Build(rules, new SeededRandom(7), 150, new LandMix(3, 0, 1, 0), null, new Vec3d(0, 1, 0), camps);
            Assert.GreaterOrEqual(haunts.Count, rules.leastHaunts);
        }

        [Test]
        public void MoreOfThemEachAnte_AndOneThereAtAnAnteIsThereAtEveryLaterOne()
        {
            var rules = new LandFieldRules();
            Assert.AreEqual(0.0, LandField.ShareAt(rules, 0), 1e-12, "before a run: nobody");
            Assert.AreEqual(0.5, LandField.ShareAt(rules, 1), 1e-12);
            Assert.AreEqual(0.75, LandField.ShareAt(rules, 2), 1e-12);
            Assert.AreEqual(1.0, LandField.ShareAt(rules, 3), 1e-12);
            Assert.AreEqual(1.0, LandField.ShareAt(rules, 9), 1e-12);

            int[] lived = new int[5];
            int total = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                List<LandHaunt> haunts = LandField.Build(rules, new SeededRandom(seed), Radius, Mixed, null, Vec3d.Zero, null);
                for (int h = 0; h < haunts.Count; h++)
                {
                    total++;
                    for (int ante = 1; ante < 5; ante++)
                    {
                        bool now = LandField.Inhabited(rules, haunts[h], ante);
                        if (now) lived[ante]++;
                        if (ante > 1 && LandField.Inhabited(rules, haunts[h], ante - 1)) Assert.IsTrue(now, "nobody moves out as the stakes rise");
                    }
                }
            }
            Assert.Greater(total, 200);
            Assert.AreEqual(0.5, lived[1] / (double)total, 0.07, "about half at the first ante");
            Assert.AreEqual(0.75, lived[2] / (double)total, 0.07, "about three quarters at the second");
            Assert.AreEqual(total, lived[3], "every one from the third");
        }

        [Test]
        public void AHauntsKind_FollowsItsWorldsWeights_AndNeverOneTheWorldHasNot()
        {
            var mix = new LandMix(1, 3, 0, 0);
            var random = new SeededRandom(21);
            int striders = 0, sprinters = 0;
            for (int i = 0; i < 4000; i++)
            {
                LandKind kind = mix.Pick(random.Next());
                Assert.IsTrue(kind == LandKind.Strider || kind == LandKind.Sprinter, "a kind with no weight is never picked");
                if (kind == LandKind.Strider) striders++; else sprinters++;
            }
            Assert.AreEqual(0.25, striders / 4000.0, 0.03);
            Assert.AreEqual(0.75, sprinters / 4000.0, 0.03);
        }

        /// <summary>
        /// What a player can learn of where the animals live (from the design review of pass 8: with a handful of haunts on
        /// a moon nobody can tell a 3:1 mix from a 2:1): the deserts are led by the Sprinter, the other airless worlds by
        /// the Strider, the other worlds with air by the Screamer, and the Roller is everywhere but never more than a third.
        /// </summary>
        [Test]
        public void EachKindOfWorld_IsLedByItsOwnAnimal_TheRollerEverywhere_TheScreamerOnlyUnderAir()
        {
            var rules = new LandFieldRules();
            var radii = new Dictionary<string, double>();
            foreach (BodySpec body in SolarSystemDefaults.Bodies()) radii[body.id] = body.radius;
            var deserts = new HashSet<string> { "Mercury", "Venus", "Mars", "Io" };
            int worlds = 0;
            var leaders = new HashSet<LandKind>();
            foreach (PlanetProfile profile in PlanetProfiles.Defaults())
            {
                string id = profile.bodyId;
                LandMix mix = LandHabitats.For(id);
                double radius = profile.surfaceRadius > 0 ? profile.surfaceRadius : radii[id];
                if (mix.Of(LandKind.Screamer) > 0) Assert.IsTrue(profile.hasAtmosphere, id + ": the Screamer flies and screams, so it needs air");
                if (!mix.Any) continue;
                worlds++;
                Assert.GreaterOrEqual(radius, rules.leastRadius, id + " is listed with animals but is too small for any");
                Assert.Greater(LandField.HauntCount(rules, radius, mix), 0, id);
                LandKind expected = deserts.Contains(id) ? LandKind.Sprinter : profile.hasAtmosphere ? LandKind.Screamer : LandKind.Strider;
                Assert.AreEqual(expected, LandHabitats.Leader(mix), id + " is led by its kind of world's animal, and by it alone");
                leaders.Add(expected);
                int all = mix.Strider + mix.Sprinter + mix.Roller + mix.Screamer;
                Assert.Greater(mix.Of(LandKind.Roller), 0, id + ": the Roller lives anywhere");
                Assert.LessOrEqual(mix.Of(LandKind.Roller) * 3, all, id + ": never more than a third of the haunts are the Roller's");
            }
            Assert.GreaterOrEqual(worlds, 14, "every world big enough with ground to walk on has animals");
            Assert.AreEqual(3, leaders.Count, "three kinds of world, each with its own leader");
            Assert.IsFalse(LandHabitats.For("Neptune").Any, "Neptune's ground is the bottom of its ocean, which has the swimmers");
            Assert.IsFalse(LandHabitats.For("Phobos").Any);
            Assert.IsFalse(LandHabitats.For(SolarSystemDefaults.SunId).Any);
            Assert.IsFalse(LandHabitats.For("Asteroid 3").Any);
            Assert.IsNull(LandHabitats.Leader(new LandMix(2, 2, 1, 0)), "a tie leads nobody");
            Assert.IsNull(LandHabitats.Leader(LandMix.None));
        }

        [Test]
        public void EveryKind_IsDrawnWithItsOwnModel_AndPaysAsThatCreature()
        {
            var models = new HashSet<string>();
            foreach (LandKind kind in Enum.GetValues(typeof(LandKind)))
            {
                string model = LandHabitats.ModelOf(kind);
                Assert.IsTrue(models.Add(model), model + " twice");
                CollectionAssert.Contains(CreatureTiers.Kinds, model, "its kill pays as a creature of that kind");
                Assert.IsFalse(CreatureTiers.Swims(model), "a land creature is never paid as a swimmer");
            }
            Assert.IsTrue(LandHabitats.Flies(LandKind.Screamer));
            Assert.IsFalse(LandHabitats.Flies(LandKind.Strider));
        }

        [Test]
        public void ALandCreaturesClaim_IsNeverASeaCreaturesClaim()
        {
            var claims = new HashSet<string>();
            for (int h = 0; h < 30; h++)
            {
                Assert.IsTrue(claims.Add(TaskIds.LandCreature("Earth", h)));
                for (int p = 0; p < 8; p++) Assert.IsTrue(claims.Add(TaskIds.Creature("Earth", h, p)), "a sea haunt's number is not a land haunt's");
            }
            Assert.AreNotEqual(TaskIds.LandCreature("Earth", 1), TaskIds.LandCreature("Mars", 1));
            StringAssert.StartsWith("creature:", TaskIds.LandCreature("Moon", 0), "it pays through the creatures' claims");
        }

        /// <summary>
        /// On a world's slopes, up and down, gently and as steeply as each kind will walk, at a walk and at its full pace
        /// (from the design review of pass 8: the test ran only gently, and a leg as drawn was never held to its foot): a
        /// foot that is down is on the ground and the leg as drawn reaches it, so no foot is ever seen in the air. On
        /// steep ground a foot that could not step in time for its neighbours slips along the ground toward the leg
        /// (<see cref="WalkerAnimator.Slips"/>): seldom on gentle ground, never in the practice range (whose ground is
        /// flat and given no ground to read).
        /// </summary>
        [Test]
        public void OnASlope_AFootThatIsDown_IsOnTheGround_AndTheLegAsDrawnReachesIt()
        {
            var rules = new LandFieldRules();
            var kinds = new[] { (WalkerLayout.Strider, LandKind.Strider, (double)new WalkerRules().pace), (WalkerLayout.Sprinter, LandKind.Sprinter, (double)WalkerRules.ForSprinter().pace) };
            foreach ((WalkerLayout kind, LandKind land, double pace) in kinds)
            foreach (double slope in new[] { 0.3, -0.3, rules.SlopeFor(land), -rules.SlopeFor(land) })
            foreach (double speed in new[] { 1.6, pace })
            {
                string what = kind.Model + " at " + slope + ", " + speed + " m/s";
                var body = new WalkerBody(kind);
                var animator = new WalkerAnimator(kind);
                var pose = new WalkerPose();
                Func<double, double, double> ground = (x, z) => slope * z;
                const double dt = 1.0 / 60.0;
                int checks = 0, slipsBefore = 0;
                for (double t = 0; t < 6.0; t += dt)
                {
                    animator.Update(body, new WalkerAnimInput { Velocity = new Vec3d(0, 0, speed), StrikeSide = -1, Ground = ground }, dt, pose);
                    if (t < 1.0) { slipsBefore = animator.Slips; continue; }
                    for (int limb = 0; limb < WalkerLayout.Limbs; limb++)
                    {
                        if (WalkerLayout.IsArm(limb) || !pose.Planted[limb]) continue;
                        Vec3d foot = pose.End[limb];
                        Assert.AreEqual(ground(foot.X, foot.Z), foot.Y, 1e-6, what + ", leg " + limb + ": a foot that is down is on the ground, never floating or sunk");
                        int last = limb * WalkerLayout.Segments + WalkerLayout.Segments - 1;
                        Vec3d tip = pose.Position[last] + pose.Rotation[last].Rotate(Vec3d.Forward) * kind.Length(limb, WalkerLayout.Segments - 1);
                        Assert.Less((tip - foot).Magnitude, 0.03, what + ", leg " + limb + ": the leg as drawn reaches its foot, never short of it in the air");
                        checks++;
                    }
                }
                Assert.Greater(checks, 500, what);
                double slipped = (animator.Slips - slipsBefore) / (double)checks;
                Assert.Less(slipped, Math.Abs(slope) < 0.35 ? 0.05 : 0.2, what + ": a foot slips only now and then, and seldom on gentle ground");
                // It rides over the middle of its feet: bending down to reach going up, a little higher at a run going down.
                Assert.That(pose.BodyPosition.Y, Is.InRange(kind.BellyHeight, kind.StandHeight * 1.4), what + ": it stands over the ground under it");
                Vec3d nose = pose.BodyRotation.Rotate(Vec3d.Forward);
                if (slope > 0) Assert.Greater(nose.Y, 0.05, what + ": it lies along the ground, its nose up the slope");
                else Assert.Less(nose.Y, -0.05, what + ": it lies along the ground, its nose down the slope");
            }
        }

        [Test]
        public void InThePracticeRange_NoGroundToRead_TheWalkIsAsItWas()
        {
            // Given no ground (the practice range's flat floor), no foot ever slips: the walk there is unchanged.
            foreach (WalkerLayout kind in new[] { WalkerLayout.Strider, WalkerLayout.Sprinter })
            {
                var body = new WalkerBody(kind);
                var animator = new WalkerAnimator(kind);
                var pose = new WalkerPose();
                for (double t = 0; t < 6.0; t += 1.0 / 60.0)
                    animator.Update(body, new WalkerAnimInput { Velocity = new Vec3d(0, 0, 2.0), TurnRate = 0.8, StrikeSide = -1 }, 1.0 / 60.0, pose);
                Assert.AreEqual(0, animator.Slips, kind.Model);
            }
        }

        // One frame of a creature kept from or after the player: reach 4 m along the ground and 3 m up (the Strider's).
        static void Run(LandReach reach, double seconds, bool after, bool refused, double across, double rise, double speedToward)
        {
            const double dt = 1.0 / 60.0;
            for (double t = 0; t < seconds; t += dt) reach.Step(after, refused, across, rise, speedToward * dt, 4.0, 3.0, dt);
        }

        [Test]
        public void ACreatureWalkingIn_OrFollowingSomeoneWhoBacksAway_CanAlwaysAnswerThem()
        {
            var reach = new LandReach();
            Run(reach, 20, true, false, 30, 0, 2.0);
            Assert.IsTrue(reach.InReach, "walking in from 30 m");
            // Kiting: the player backs away faster than it walks, so the gap grows, but it is still coming for them.
            Run(reach, 20, true, false, 40, 0, 1.2);
            Assert.IsTrue(reach.InReach, "following someone who backs away is answering them");
            // Refused now and then (a rock on the way) while it still gains ground.
            for (int i = 0; i < 20; i++)
            {
                Run(reach, 0.5, true, true, 30, 0, 0.6);
                Run(reach, 0.5, true, false, 30, 0, 2.0);
            }
            Assert.IsTrue(reach.InReach, "a step refused on the way is not being kept away");
            // A dashing kind stops for a breath between dashes: a second's window still holds most of a dash.
            for (int i = 0; i < 20; i++)
            {
                Run(reach, 0.55, true, false, 30, 0, 0.0);
                Run(reach, 1.3, true, false, 30, 0, 6.2);
            }
            Assert.IsTrue(reach.InReach, "the Sprinter's stops are not being kept away");
        }

        [Test]
        public void KeptBelowALedge_ItCanNoLongerAnswer_AndCanAgainOnceItGainsGround()
        {
            var reach = new LandReach();
            // The player stands 6 m up on a ledge, 2 m across: near along the ground, but out of its climb.
            Run(reach, LandReach.KeptSeconds - 1.0, true, true, 2, 6, 0);
            Assert.IsTrue(reach.InReach, "not yet: a moment's refusal is no ledge");
            Run(reach, 1.5, true, true, 2, 6, 0);
            Assert.IsFalse(reach.InReach, "kept from them this long, a kill pays nothing");
            Assert.GreaterOrEqual(reach.KeptFor, LandReach.KeptSeconds);
            Run(reach, 1.0, true, false, 12, 0, 2.0);
            Assert.IsTrue(reach.InReach, "the player came down and it closed on them: it can answer again");

            // Within its reach along the ground and its climb: always.
            Run(reach, 30, true, true, 4.0 + LandReach.Margin - 0.1, 2.5, 0);
            Assert.IsTrue(reach.InReach, "within its strike and climb it can answer, refused or not");
            Run(reach, 30, true, true, 4.0 + LandReach.Margin - 0.1, -3.5, 0);
            Assert.IsFalse(reach.InReach, "below it counts as much as above: over a drop it cannot climb down");
            Run(reach, 0.1, false, true, 50, 9, 0);
            Assert.IsTrue(reach.InReach, "no longer after the player: nothing to answer");
        }

        [Test]
        public void ItsOwnOpening_NeverTakesItsPayAway()
        {
            // On its back after a roll, out of breath after a scream, dragging itself without legs: no step refused and
            // no ground gained neither counts nor clears.
            var reach = new LandReach();
            Run(reach, 30, true, false, 26, 0, 0);
            Assert.IsTrue(reach.InReach, "lying on its back 26 m off for half a minute");
            Run(reach, 30, true, false, 26, 0, 0.2);
            Assert.IsTrue(reach.InReach, "dragging itself slowly");
            Run(reach, 2.5, true, true, 2, 6, 0);
            Run(reach, 20, true, false, 2, 6, 0);
            Assert.IsTrue(reach.InReach, "a pause holds what it had, it does not add to it");
            Assert.That(reach.KeptFor, Is.InRange(2.0, 3.1), "the refused seconds, and the second that was under way");
            Run(reach, 2.0, true, true, 2, 6, 0);
            Assert.IsFalse(reach.InReach, "and once it tries again and is refused, the time carries on");
            reach.Reset();
            Assert.IsTrue(reach.InReach);
        }

        [Test]
        public void AStepSteeperThanTheCreatureTakes_IsAWall_UpOrDown()
        {
            Assert.IsFalse(LandStep.TooSteep(0.5, 1.0, 0.6));
            Assert.IsTrue(LandStep.TooSteep(0.7, 1.0, 0.6));
            Assert.IsTrue(LandStep.TooSteep(-0.7, 1.0, 0.6), "a drop is as much a wall as a climb");
            Assert.IsFalse(LandStep.TooSteep(0.0, 0.0, 0.6), "standing still on the level is no step");
            Assert.IsTrue(LandStep.TooSteep(0.01, 0.0, 0.6), "a step too short to measure is held to the same slope");
            Assert.IsFalse(LandStep.TooSteep(5.0, 1.0, 0.0), "no limit set: anything goes");
        }

        [Test]
        public void OnASlope_ItStandsByTheGround_ButNeverLeansFurtherThanItCan()
        {
            Vec3d up = new Vec3d(0, 1, 0);
            Vec3d plane = LandStep.PlaneUp(new Vec3d(0, 0, 1), new Vec3d(1, 0, 0), up);
            Assert.AreEqual(1.0, plane.Y, 1e-12, "level ground: straight up");
            Assert.AreEqual(-1.0, LandStep.PlaneUp(new Vec3d(0, 0, 1), new Vec3d(1, 0, 0), -up).Y, 1e-12, "on the side the creature stands");
            Assert.AreEqual(1.0, LandStep.PlaneUp(new Vec3d(1, 0, 0), new Vec3d(0, 0, 1), up).Y, 1e-12, "whichever way round the two lines come");

            // A slope rising 0.3 to the front.
            Vec3d along = new Vec3d(0, 0.3, 1).Normalized, across = new Vec3d(1, 0, 0);
            Vec3d ground = LandStep.PlaneUp(along, across, up);
            Assert.AreEqual(0.0, Vec3d.Dot(ground, along), 1e-12);
            Assert.AreEqual(0.0, Vec3d.Dot(ground, across), 1e-12);
            Vec3d lean = LandStep.Lean(up, ground, 0.6);
            Assert.AreEqual(ground.X, lean.X, 1e-12, "a gentle slope: it stands square to the ground");
            Assert.AreEqual(ground.Y, lean.Y, 1e-12);
            Vec3d capped = LandStep.Lean(up, ground, 0.1);
            Assert.AreEqual(0.1, Math.Acos(Vec3d.Dot(capped, up)), 1e-9, "never more than it can lean");
            Assert.AreEqual(1.0, capped.Magnitude, 1e-9);
            Assert.Less(capped.Z, 0.0, "leaning the same way as the ground, back from the rise");
            Assert.AreEqual(up.Y, LandStep.Lean(up, Vec3d.Zero, 0.6).Y, 1e-12, "no ground read: upright");
        }
    }
}
