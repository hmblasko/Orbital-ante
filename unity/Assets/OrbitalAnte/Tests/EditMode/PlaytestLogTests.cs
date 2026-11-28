using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Audio;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.Playtest;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The playtest log (Ralph loop pass 9, 28 Nov 2026): what one run was like, written to a file by the editor and
    /// development builds so the Deck playtests the Ralph loop's reports ask for can be counted. These hold its counting
    /// to the protocols: each creature warning ends one way (hurt, missed, killed during it, called off, or still under
    /// way), a creature's strike is put down to the warning of its own kind, a hit on a creature is counted by where it
    /// landed, a fight is joined by a camp only while it is under way, and the report gives counts, never percentages.
    /// </summary>
    public class PlaytestLogTests
    {
        sealed class Thing
        {
            public readonly string Name;
            public Thing(string name) => Name = name;
            public override string ToString() => Name;
        }

        static PlaytestLog NewLog()
        {
            var log = new PlaytestLog();
            log.Begin("solar system", 12345, "2026-11-28 10:02:15", "test build", null, null, true);
            return log;
        }

        static void Warn(PlaytestLog log, object who, string kind, float t, WindupStage was, WindupStage now, float facing = 10f, float range = 20f)
            => log.Warning(t, who, kind, "Moon", 1, null, was, now, facing, range);

        /// <summary>A full warning: wind-up at t, aim at t + 0.5, the strike at t + 0.8, over at t + 1.</summary>
        static void FullWarning(PlaytestLog log, object who, string kind, float t, float facingAtMark = 30f)
        {
            Warn(log, who, kind, t, WindupStage.None, WindupStage.Winding);
            Warn(log, who, kind, t + 0.5f, WindupStage.Winding, WindupStage.Aimed, facingAtMark);
            Warn(log, who, kind, t + 0.8f, WindupStage.Aimed, WindupStage.Striking, facingAtMark);
            Warn(log, who, kind, t + 1f, WindupStage.Striking, WindupStage.None, facingAtMark);
        }

        // ------------------------------------------------------------------ what the game hands it

        [Test]
        public void StrikeWords_NameTheKindOfEveryStrike_AndNothingElse()
        {
            var named = new HashSet<string>();
            foreach (string words in StrikeWords.All)
            {
                string kind = StrikeWords.KindOf(words);
                Assert.IsNotNull(kind, "\"" + words + "\" names no kind");
                Assert.Contains(kind, PlaytestLog.Kinds, "\"" + words + "\" names " + kind + ", which the log does not know");
                named.Add(kind);
            }
            foreach (string kind in PlaytestLog.Kinds) Assert.IsTrue(named.Contains(kind), kind + " has no strike words, so its hurts would go to nobody");
            Assert.AreEqual(WalkerLayout.Strider.Model, StrikeWords.KindOf(StrikeWords.Claw));
            Assert.AreEqual(WalkerLayout.Sprinter.Model, StrikeWords.KindOf(StrikeWords.Lunge));
            Assert.AreEqual(RollerLayout.Model, StrikeWords.KindOf(StrikeWords.Butt));
            Assert.IsNull(StrikeWords.KindOf(null));
            Assert.IsNull(StrikeWords.KindOf("Shot down outside the ship."));
        }

        [Test]
        public void EveryKind_IsNamedForTheAnimalItIsDrawnFrom()
        {
            foreach (string kind in PlaytestLog.Kinds) Assert.IsFalse(string.IsNullOrEmpty(PlaytestLog.AnimalOf(kind)), kind);
            Assert.IsNull(PlaytestLog.AnimalOf("Trooper"));
        }

        [Test]
        public void AWindupChange_SaysWhatTheStageWasAndIs()
        {
            foreach (WindupStage was in Enum.GetValues(typeof(WindupStage)))
            foreach (WindupStage now in Enum.GetValues(typeof(WindupStage)))
            {
                WindupChange c = Windups.Change(was, now);
                Assert.AreEqual(was != now, c.Changed, was + " to " + now);
                if (was == now) continue;
                Assert.AreEqual(was, c.Was, was + " to " + now);
                Assert.AreEqual(now, c.Now, was + " to " + now);
            }
        }

        // ------------------------------------------------------------------ warnings (pass 2)

        [Test]
        public void AWarningThatHurts_IsCountedAsHurt_WithWhereTheCreatureWasAtItsMark()
        {
            PlaytestLog log = NewLog();
            var strider = new Thing("strider");
            Warn(log, strider, "Strider", 1f, WindupStage.None, WindupStage.Winding, 12f);
            Warn(log, strider, "Strider", 1.5f, WindupStage.Winding, WindupStage.Aimed, 150f, 9f);
            Warn(log, strider, "Strider", 1.8f, WindupStage.Aimed, WindupStage.Striking, 150f, 9f);
            log.Hurt(1.9f, 25f, StrikeWords.Claw);
            Warn(log, strider, "Strider", 2f, WindupStage.Striking, WindupStage.None, 150f, 9f);

            Assert.AreEqual(1, log.Warnings.Count);
            WarningRecord w = log.Warnings[0];
            Assert.IsTrue(w.Marked);
            Assert.AreEqual(WarningEnd.Hurt, w.End);
            Assert.AreEqual(12f, w.FacingAtBegin);
            Assert.AreEqual(150f, w.FacingAtMark);
            Assert.AreEqual(9f, w.RangeAtMark);
            Assert.AreEqual("behind", PlaytestLog.FacingWords(w.FacingAtMark));
            Assert.AreEqual(25f, w.Damage);
            Assert.AreEqual(1, w.Beats);
            Assert.AreEqual(25f, log.Animals[0].DamageToYou);
            Assert.AreEqual(1, log.Animals[0].Warnings);
            Assert.AreEqual(0, log.UnwarnedHurts);
        }

        [Test]
        public void AWarningCalledOffBeforeItsMark_AndOneThatStrikesAndMisses_AreToldApart()
        {
            PlaytestLog log = NewLog();
            var roller = new Thing("roller");
            Warn(log, roller, "Roller", 1f, WindupStage.None, WindupStage.Winding);
            Warn(log, roller, "Roller", 1.4f, WindupStage.Winding, WindupStage.None);
            FullWarning(log, roller, "Roller", 5f);

            Assert.AreEqual(2, log.Warnings.Count);
            Assert.AreEqual(WarningEnd.CalledOff, log.Warnings[0].End);
            Assert.IsFalse(log.Warnings[0].Marked);
            Assert.AreEqual(WarningEnd.Missed, log.Warnings[1].End);
            Assert.AreEqual(2, log.Animals[0].Warnings, "one animal, two warnings");
        }

        [Test]
        public void AWindUpStraightAfterAStrike_IsANewWarning_AndTheStrikesHurtStaysWithTheFirst()
        {
            PlaytestLog log = NewLog();
            var strider = new Thing("strider");
            Warn(log, strider, "Strider", 1f, WindupStage.None, WindupStage.Winding);
            Warn(log, strider, "Strider", 1.5f, WindupStage.Winding, WindupStage.Aimed);
            Warn(log, strider, "Strider", 1.8f, WindupStage.Aimed, WindupStage.Striking);
            Warn(log, strider, "Strider", 2f, WindupStage.Striking, WindupStage.Winding);
            log.Hurt(2.05f, 30f, StrikeWords.Claw);
            Warn(log, strider, "Strider", 2.5f, WindupStage.Winding, WindupStage.Aimed);
            Warn(log, strider, "Strider", 2.9f, WindupStage.Aimed, WindupStage.None);

            Assert.AreEqual(2, log.Warnings.Count, "two warnings, as the visor told them");
            Assert.AreEqual(2f, log.Warnings[0].OverAt);
            Assert.AreEqual(WarningEnd.Hurt, log.Warnings[0].End, "the hurt is the first strike's");
            Assert.AreEqual(2f, log.Warnings[1].BeganAt);
            Assert.AreEqual(2.5f, log.Warnings[1].AimedAt);
            Assert.AreEqual(WarningEnd.Stopped, log.Warnings[1].End, "it took aim and never struck");
            Assert.AreEqual(2, log.Animals[0].Warnings);
        }

        [Test]
        public void AWarningThatLetsGoOfItsAimAndWindsUpAgain_IsCountedAsTheVisorTellsIt()
        {
            // The visor's arc begins again when an aim goes back to a wind-up (WindupChange.Began): so does the count.
            PlaytestLog log = NewLog();
            var stalker = new Thing("stalker");
            Warn(log, stalker, "Stalker", 1f, WindupStage.None, WindupStage.Winding);
            Warn(log, stalker, "Stalker", 1.5f, WindupStage.Winding, WindupStage.Aimed);
            Warn(log, stalker, "Stalker", 1.7f, WindupStage.Aimed, WindupStage.Winding);
            Assert.AreEqual(Windups.Change(WindupStage.Aimed, WindupStage.Winding).Began ? 2 : 1, log.Warnings.Count);
        }

        [Test]
        public void ACreatureAimedFromTheStart_IsMarkedAtOnce()
        {
            // A Sprinter with no arms is Aimed from the first frame: its mark is its warning.
            PlaytestLog log = NewLog();
            var sprinter = new Thing("sprinter");
            Warn(log, sprinter, "Sprinter", 3f, WindupStage.None, WindupStage.Aimed, 5f);
            Assert.AreEqual(1, log.Warnings.Count);
            Assert.AreEqual(3f, log.Warnings[0].AimedAt);
            Assert.AreEqual(WarningEnd.Open, log.Warnings[0].End, "under way until it ends");
        }

        [Test]
        public void AKillDuringAWarning_EndsItAsKilled_NotAsStillUnderWay()
        {
            // A creature that dies stops thinking, so it never ends its own warning.
            PlaytestLog log = NewLog();
            var screamer = new Thing("screamer");
            Warn(log, screamer, "Screamer", 1f, WindupStage.None, WindupStage.Winding);
            log.CreatureHit(1.3f, screamer, "Screamer", "Mars", 1, null, "Screamer Abdomen", HitZone.Vital, false, true, "Rifle", 12f, "creature:Mars.land1");
            Assert.AreEqual(WarningEnd.Killed, log.Warnings[0].End);
            Assert.AreEqual(1.3f, log.Warnings[0].OverAt);

            // ...and a strike that hurt before the kill stays a hurt.
            var other = new Thing("screamer 2");
            Warn(log, other, "Screamer", 10f, WindupStage.None, WindupStage.Striking);
            log.Hurt(10.2f, 8f, StrikeWords.Scream);
            log.CreatureHit(10.4f, other, "Screamer", "Mars", 1, null, "Screamer Abdomen", HitZone.Vital, false, true, "Rifle", 12f, "creature:Mars.land2");
            Assert.AreEqual(WarningEnd.Hurt, log.Warnings[1].End);
        }

        [Test]
        public void AHurtJustAfterAStrike_CountsForIt_AHoldsLaterBitesGoToTheCreature_AndLaterStillToNobody()
        {
            PlaytestLog log = NewLog();
            var ambusher = new Thing("ambusher");
            FullWarning(log, ambusher, "Ambusher", 1f);   // over at 2 s
            log.Hurt(2f + PlaytestLog.HurtGrace * 0.5f, 10f, StrikeWords.Burrow);
            Assert.AreEqual(WarningEnd.Hurt, log.Warnings[0].End, "within the grace, the strike's own hurt");

            log.Hurt(2f + PlaytestLog.HoldGrace * 0.5f, 6f, StrikeWords.Burrow);
            Assert.AreEqual(10f, log.Warnings[0].Damage, "a later bite is not the strike's");
            Assert.AreEqual(16f, log.Animals[0].DamageToYou, "but it is the creature's");
            Assert.AreEqual(0, log.UnwarnedHurts);

            log.Hurt(2f + PlaytestLog.HoldGrace + 5f, 6f, StrikeWords.Burrow);
            Assert.AreEqual(1, log.UnwarnedHurts, "long after any warning of its kind");
            Assert.AreEqual(16f, log.Animals[0].DamageToYou);
        }

        [Test]
        public void AStrikeIsOnlyEverPutDownToAWarningOfItsOwnKind()
        {
            PlaytestLog log = NewLog();
            var roller = new Thing("roller");
            Warn(log, roller, "Roller", 1f, WindupStage.None, WindupStage.Striking);
            log.Hurt(1.2f, 20f, StrikeWords.Claw);
            Assert.AreEqual(0f, log.Warnings[0].Damage);
            Assert.AreEqual(1, log.UnwarnedHurts);
        }

        [Test]
        public void AShotsHurt_IsNeverPutDownToACreature_ButIsTallied()
        {
            PlaytestLog log = NewLog();
            var strider = new Thing("strider");
            Warn(log, strider, "Strider", 1f, WindupStage.None, WindupStage.Striking);
            log.Hurt(1.1f, 10f, null);
            log.Hurt(1.2f, 5f, "Shot down outside the ship.");
            log.Hurt(1.3f, 0f, StrikeWords.Claw);
            Assert.AreEqual(0f, log.Warnings[0].Damage);
            Assert.AreEqual(0, log.UnwarnedHurts);
            Assert.AreEqual(1, log.HurtBy[PlaytestLog.ShotsAndOther].Count);
            Assert.AreEqual(10.0, log.HurtBy[PlaytestLog.ShotsAndOther].Total);
            Assert.AreEqual(1, log.HurtBy["Shot down outside the ship."].Count);
            Assert.IsFalse(log.HurtBy.ContainsKey(StrikeWords.Claw), "no hurt, nothing counted");
        }

        [Test]
        public void Facing_IsAheadToTheSideOrBehind()
        {
            Assert.AreEqual("ahead", PlaytestLog.FacingWords(0f));
            Assert.AreEqual("ahead", PlaytestLog.FacingWords(PlaytestLog.AheadMost));
            Assert.AreEqual("side", PlaytestLog.FacingWords(PlaytestLog.AheadMost + 1f));
            Assert.AreEqual("side", PlaytestLog.FacingWords(PlaytestLog.BehindLeast - 1f));
            Assert.AreEqual("behind", PlaytestLog.FacingWords(PlaytestLog.BehindLeast));
            Assert.AreEqual("behind", PlaytestLog.FacingWords(180f));
            Assert.AreEqual("unknown", PlaytestLog.FacingWords(-1f), "no eye to measure from");
        }

        // ------------------------------------------------------------------ hits (pass 7)

        [Test]
        public void HitsOnACreature_AreCountedByWhereTheyLanded_AndItsKillSaysWhereAndWhatItPaid()
        {
            PlaytestLog log = NewLog();
            var strider = new Thing("strider");
            const string Claim = "creature:Moon.land3";
            log.CreatureHit(10f, strider, "Strider", "Moon", 2, null, "Strider Carapace", HitZone.Armour, false, false, "Rifle", 30f);
            log.CreatureHit(11f, strider, "Strider", "Moon", 2, null, "Strider Carapace", HitZone.Armour, false, false, "Rifle", 30f);
            log.CreatureHit(12f, strider, "Strider", "Moon", 2, null, "Strider Arm R0", HitZone.WeaponOff, true, false, "Rifle", 28f);
            log.CreatureHit(14.5f, strider, "Strider", "Moon", 2, null, "Strider Belly", HitZone.Vital, false, true, "Rifle", 25f, Claim);

            AnimalRecord a = log.Animals[0];
            Assert.AreEqual(2, a.Hits[(int)HitZone.Armour]);
            Assert.AreEqual(1, a.Hits[(int)HitZone.WeaponOff]);
            Assert.AreEqual(1, a.Hits[(int)HitZone.Vital]);
            Assert.AreEqual(4, a.TotalHits);
            Assert.AreEqual(1, a.PiecesOff);
            Assert.AreEqual(AnimalEnd.KilledByYou, a.End);
            Assert.AreEqual(HitZone.Vital, a.KillZone);
            Assert.AreEqual("belly", a.KillPiece, "the piece in its own words, without the kind in front");
            Assert.AreEqual(Claim, a.Claim);
            Assert.AreEqual(-1, a.PaidPoints, "nothing heard from the ante yet");
            log.Points(14.5f, 2, "CreatureKill", "KILL", 140, true, "Awarded", 0, Claim);
            Assert.AreEqual(140, a.PaidPoints, "the ante's own answer on its claim");
            Assert.IsTrue(a.Paid);
            Assert.AreEqual(1, log.Paid["creature KILL"].Count, "a creature's kill is kept apart from a camp's");
            Assert.AreEqual(10f, a.FirstHitAt);
            Assert.AreEqual(2, a.Ante);
            Assert.AreEqual("Moon", a.World);
            Assert.AreEqual(4, log.Guns["Rifle"].Hits);
            Assert.AreEqual(1, log.Guns["Rifle"].Kills);

            // A hit on a dead creature changes nothing about its kill.
            log.CreatureHit(15f, strider, "Strider", "Moon", 2, null, "Strider Belly", HitZone.Armour, false, true, "Pistol", 25f, "creature:other");
            Assert.AreEqual(HitZone.Vital, a.KillZone);
            Assert.AreEqual(Claim, a.Claim);
            Assert.AreEqual(0, log.Guns["Pistol"].Kills, "a creature is killed once");
        }

        [Test]
        public void EachKind_IsNumberedInTheOrderItIsMet()
        {
            PlaytestLog log = NewLog();
            AnimalRecord s1 = log.Meet(new Thing("a"), "Strider", "Moon", 1, null);
            AnimalRecord r1 = log.Meet(new Thing("b"), "Roller", "Moon", 1, null);
            AnimalRecord s2 = log.Meet(new Thing("c"), "Strider", "Moon", 1, null);
            Assert.AreEqual("Strider #1", s1.Name);
            Assert.AreEqual("Roller #1", r1.Name);
            Assert.AreEqual("Strider #2", s2.Name);
        }

        [Test]
        public void APacksFish_AddToThePack_WhichIsMetWithItsFirstFish_InThePracticeRangeToo()
        {
            PlaytestLog log = NewLog();
            var shoal = new Thing("shoal");
            var fish1 = new Thing("fish 1");
            var fish2 = new Thing("fish 2");
            log.CreatureHit(1f, fish1, "Biter", "practice range", 0, shoal, "Biter Body", HitZone.Vital, false, true, "Smg", 6f);
            log.CreatureHit(2f, fish2, "Biter", "practice range", 0, shoal, "Biter Fin", HitZone.Plain, true, false, "Smg", 6f);

            AnimalRecord pack = log.Find(shoal);
            Assert.IsNotNull(pack);
            Assert.IsTrue(pack.IsGroup);
            Assert.AreEqual(PlaytestLog.PackOf("Biter"), pack.Kind);
            Assert.AreEqual("Biter", PlaytestLog.MemberOf(pack.Kind));
            Assert.AreSame(pack, log.Find(fish1).Group);
            Assert.AreSame(pack, log.Find(fish2).Group);
            Assert.AreEqual(1, pack.Kills);
            Assert.AreEqual(1, pack.Hits[(int)HitZone.Vital]);
            Assert.AreEqual(1, pack.Hits[(int)HitZone.Plain]);
            Assert.AreEqual("Biter #1", log.Find(fish1).Name);
            Assert.AreEqual("Biter #2", log.Find(fish2).Name);

            // On a world the spawner wakes the pack first, and its fish join that record.
            PlaytestLog world = NewLog();
            var wild = new Thing("wild shoal");
            world.Woke(0f, wild, PlaytestLog.PackOf("Biter"), "Earth", 3, 90f, true);
            var wildFish = new Thing("wild fish");
            world.Warning(4f, wildFish, "Biter", "Earth", 3, wild, WindupStage.None, WindupStage.Winding, 20f, 8f);
            Assert.AreEqual(2, world.Animals.Count);
            Assert.AreSame(world.Find(wild), world.Animals[1].Group);
            Assert.AreEqual(4f, world.Find(wild).FightFrom, "a fish's warning is the pack's fight");

            // A fish's bite hurts on the pack's line too, and a fish that kills the player is the pack that did.
            world.Warning(4.9f, wildFish, "Biter", "Earth", 3, wild, WindupStage.Winding, WindupStage.Striking, 20f, 2f);
            world.Hurt(5f, 30f, StrikeWords.Shoal);
            world.Died(5.1f, StrikeWords.Shoal);
            Assert.AreEqual(30f, world.Find(wildFish).DamageToYou);
            Assert.AreEqual(30f, world.Find(wild).DamageToYou);
            Assert.AreEqual(AnimalEnd.KilledYou, world.Find(wildFish).End);
            Assert.AreEqual(AnimalEnd.KilledYou, world.Find(wild).End);
            StringAssert.Contains("1 warning (1 marked: 1 hurt you", PlaytestReport.Write(world), "the pack's line counts its fish's warnings");
        }

        // ------------------------------------------------------------------ the animals met (pass 8)

        [Test]
        public void AnAnimalWokenSeenAndLeft_SaysWhenAndHowFar()
        {
            PlaytestLog log = NewLog();
            var roller = new Thing("roller");
            log.Woke(5f, roller, "Roller", "Mars", 2, 95f, false);
            log.Seen(8f, roller, 80f);
            log.Seen(9f, roller, 40f);
            log.Noticed(11f, roller);
            log.Noticed(11.25f, roller);
            AnimalRecord a = log.Find(roller);
            log.PutAway(30f, roller);
            log.Noticed(31f, roller);

            Assert.IsNull(log.Find(roller), "once put away the log lets go of it (it is destroyed straight after)");
            Assert.AreEqual(5f, a.WokeAt);
            Assert.AreEqual(95f, a.WokeFrom);
            Assert.AreEqual(8f, a.SeenAt, "the first time only");
            Assert.AreEqual(80f, a.SeenFrom);
            Assert.AreEqual(11f, a.NoticedAt);
            Assert.AreEqual(11.25f, a.ActiveAt);
            Assert.AreEqual(11f, a.FightFrom);
            Assert.AreEqual(AnimalEnd.PutAway, a.End);
            Assert.AreEqual(30f, a.EndAt);
            Assert.AreEqual(11.25f, a.ActiveAt, "nothing after it is put away");
            Assert.AreEqual(0, a.TotalHits, "left alone");
        }

        [Test]
        public void AnEnemySeeingYou_JoinsOnlyAFightStillUnderWay()
        {
            PlaytestLog log = NewLog();
            var fighting = new Thing("fighting");
            var asleep = new Thing("asleep");
            var over = new Thing("over");
            log.Woke(0f, fighting, "Strider", "Moon", 1, 100f, false);
            log.Woke(0f, asleep, "Roller", "Moon", 1, 100f, false);
            log.Woke(0f, over, "Sprinter", "Moon", 1, 100f, false);
            log.Noticed(2f, over);
            log.Noticed(10f, fighting);
            log.EnemySawYou(10f + PlaytestLog.FightHold - 1f);

            Assert.IsTrue(log.Find(fighting).EnemySawYou);
            Assert.IsFalse(log.Find(asleep).EnemySawYou, "never in a fight");
            Assert.IsFalse(log.Find(over).EnemySawYou, "its fight was over");
        }

        [Test]
        public void ADeathByAStrike_MarksTheCreatureThatKilledYou_AndAShotMarksNone()
        {
            PlaytestLog log = NewLog();
            var stalker = new Thing("stalker");
            FullWarning(log, stalker, "Stalker", 1f);
            log.Hurt(2.3f, 40f, StrikeWords.Heard);
            log.Died(2.5f, StrikeWords.Heard);
            Assert.AreEqual(AnimalEnd.KilledYou, log.Find(stalker).End);
            Assert.AreEqual(1, log.Deaths.Count);

            PlaytestLog shot = NewLog();
            var roller = new Thing("roller");
            FullWarning(shot, roller, "Roller", 1f);
            shot.Hurt(2.3f, 40f, StrikeWords.Roll);
            shot.Died(2.5f, "Shot down outside the ship.");
            Assert.AreEqual(AnimalEnd.StillThere, shot.Find(roller).End);
        }

        [Test]
        public void TheAimLine_MarksTheCreatureItNamed_AndKeepsWhereItLay()
        {
            PlaytestLog log = NewLog();
            var fought = new Thing("fought");
            var otherKind = new Thing("other kind");
            var notFought = new Thing("not fought");
            log.Woke(0f, notFought, "Strider", "Moon", 1, 100f, false);
            log.CreatureHit(5f, fought, "Strider", "Moon", 1, null, "Strider Carapace", HitZone.Armour, false, false, "Rifle", 30f);
            log.CreatureHit(5f, otherKind, "Roller", "Moon", 1, null, "Roller Plate", HitZone.Armour, false, false, "Rifle", 30f);
            log.AimLine(5.1f, "Strider", fought, "Moon", 1, null);
            Assert.IsTrue(log.Find(fought).AimLineShown);
            Assert.IsFalse(log.Find(otherKind).AimLineShown);
            Assert.IsFalse(log.Find(notFought).AimLineShown, "another of its kind was not the one shot");
            Assert.AreEqual(1, log.AimLines["Strider"]);

            // Each look along the line's words while it shows: over the creature, over its weak place.
            log.AimLineLook(true, false);
            log.AimLineLook(true, true);
            log.AimLineLook(false, false);
            AimLineRecord r = log.AimLineRecords[0];
            Assert.AreEqual("Strider #2", r.Name);
            Assert.AreEqual(3, r.Looks);
            Assert.AreEqual(2, r.OverIt);
            Assert.AreEqual(1, r.OverWeak);
            StringAssert.Contains("3 looks; it lay over the creature in 2, over its weak place in 1", PlaytestReport.Write(log));

            // A line with no creature to name still counts for its kind.
            log.AimLine(9f, "Roller");
            Assert.AreEqual(1, log.AimLines["Roller"]);
            Assert.AreEqual("Roller", log.AimLineRecords[1].Name);
        }

        // ------------------------------------------------------------------ points, music, the run

        [Test]
        public void Points_AreTalliedByWhatPaid_AndRefusalsByWhy()
        {
            PlaytestLog log = NewLog();
            log.Points(10f, 1, "Kill", "KILL", 50, true, "Awarded", 0, "kill.1");
            log.Points(12f, 1, "Kill", "KILL", 70, true, "Awarded", 1, "kill.2");
            log.Points(14f, 1, "Kill", "KILL", 0, false, "DuplicateClaim", 0, "kill.2");
            log.Note(15f, "TARGET MET");
            log.Note(16f, "TARGET MET");
            log.Note(17f, "");
            log.Note(17.5f, "A PLAIN NOTE REPLACED BEFORE IT SHOWED");
            log.NoteShown(15f, "TARGET MET");
            log.NoteShown(16f, "TARGET MET");
            log.NoteShown(17f, "");
            Assert.AreEqual(2, log.Awards);
            Assert.AreEqual(120, log.PointsAwarded);
            Assert.AreEqual(2, log.Paid["KILL"].Count);
            Assert.AreEqual(120.0, log.Paid["KILL"].Total);
            Assert.AreEqual(1, log.Refused["DuplicateClaim"].Count);
            Assert.AreEqual(2, log.Notes["TARGET MET"].Count);
            Assert.AreEqual(2, log.NotesShown, "an empty note is no note");
            Assert.AreEqual(3, log.NotesOffered, "offered, shown or not");
            Assert.IsFalse(log.Notes.ContainsKey("A PLAIN NOTE REPLACED BEFORE IT SHOWED"), "only what showed is tallied by its words");
        }

        [Test]
        public void TheAntes_SayWhenEachBeganMetItsTargetWentIntoOvertimeAndWasCleared()
        {
            PlaytestLog log = NewLog();
            log.AnteBegan(0f, 1, 1200);
            log.ClockStarted(40f, 1);
            log.ClockStarted(41f, 1);
            log.Overtime(340f, 1, true);
            log.TargetMet(370f, 1, 1210, 0f);
            log.Overtime(370f, 1, false);
            log.PerkTaken(372f, 1, "Glass Cannon");
            log.PerkTaken(373f, 1, null);
            log.AnteCleared(380f, 1, 1250, 0f);
            log.AnteCleared(381f, 1, 1300);
            log.AnteBegan(400f, 2, 1900);
            log.ClockStarted(400f, 2);
            log.TargetMet(520f, 2, 1950, 185.5f);
            Assert.AreEqual(2, log.Antes.Count);
            AnteRecord a = log.Antes[0];
            Assert.AreEqual(40f, a.ClockFrom, "ante 1's clock waits on the pad; the first start counts");
            Assert.AreEqual(1, a.Perks.Count);
            Assert.AreEqual("Glass Cannon", a.Perks[0]);
            Assert.AreEqual(0f, a.LeftAtTarget);
            Assert.AreEqual(185.5f, log.Antes[1].LeftAtTarget);
            StringAssert.Contains("Perks taken: Glass Cannon.", PlaytestReport.Write(log));
            StringAssert.Contains("its clock started 0:40", PlaytestReport.Write(log));
            Assert.AreEqual(340f, a.OvertimeAt);
            Assert.AreEqual(370f, a.TargetMetAt);
            Assert.AreEqual(380f, a.ClearedAt, "cleared once");
            Assert.AreEqual(1250, a.Banked);
            Assert.AreEqual(-1f, log.Antes[1].ClearedAt);
        }

        [Test]
        public void TheMusic_CountsTimeInEachBand_ButNotWhilePaused()
        {
            PlaytestLog log = NewLog();
            log.Music(0f, IntensityBand.Exploration, "Deep Orbit", 2, false);
            log.Music(10f, IntensityBand.Tension, "Deep Orbit", 3, false);
            log.Music(20f, IntensityBand.Tension, "Deep Orbit", 3, false, true);
            log.Music(50f, IntensityBand.Tension, "Deep Orbit", 3, false, true);
            log.Music(55f, IntensityBand.Tension, "Deep Orbit", 3, false);
            log.Music(60f, IntensityBand.Combat, "Deep Orbit", 5, true);
            log.Music(61f, IntensityBand.Combat, "Deep Orbit", 5, true);
            log.Music(62f, IntensityBand.Combat, "Night Shift", 5, false);
            log.Finish(70f, RunEnd.Died, "Screamed down.", 2, 3000);

            Assert.AreEqual(10.0, log.SecondsIn(IntensityBand.Exploration), 1e-4);
            Assert.AreEqual(15.0, log.SecondsIn(IntensityBand.Tension), 1e-4, "10 s before the pause and 5 s after it");
            Assert.AreEqual(10.0, log.SecondsIn(IntensityBand.Combat), 1e-4);
            Assert.AreEqual(2, log.BandChanges);
            Assert.AreEqual(1, log.ThinTimes, "thinned once, however long");
            Assert.AreEqual(5, log.MostLayers);
            Assert.AreEqual(2, log.Pieces.Count);
        }

        [Test]
        public void AnEarlyWrite_CountsTheMusicSoFar_WithoutCountingAnySecondTwice()
        {
            PlaytestLog log = NewLog();
            log.Music(0f, IntensityBand.Combat, "Deep Orbit", 5, false);
            log.Snapshot(30f);
            Assert.AreEqual(30.0, log.SecondsIn(IntensityBand.Combat), 1e-4);
            Assert.AreEqual(30f, log.Length);
            Assert.IsFalse(log.Finished);
            log.Finish(50f, RunEnd.Left, null, 1, 0);
            Assert.AreEqual(50.0, log.SecondsIn(IntensityBand.Combat), 1e-4);
        }

        [Test]
        public void ARunEndsOnce()
        {
            PlaytestLog log = NewLog();
            log.Finish(100f, RunEnd.Died, "Caught in a giant claw.", 3, 4567);
            log.Finish(120f, RunEnd.Left, "back to the title screen", 3, 9999);
            Assert.AreEqual(RunEnd.Died, log.End);
            Assert.AreEqual(100f, log.Length);
            Assert.AreEqual(4567, log.Score);
            int ends = 0;
            foreach (LogLine line in log.Lines) if (line.Text.Contains("run ends")) ends++;
            Assert.AreEqual(1, ends);
        }

        [Test]
        public void TheLog_IsBounded_AndCountsWhatItDropped()
        {
            PlaytestLog log = NewLog();
            for (int i = 0; i < PlaytestLog.MostLines + 10; i++) log.NoteShown(i, "NOTE " + (i % (PlaytestLog.MostNames + 10)));
            Assert.AreEqual(PlaytestLog.MostLines, log.Lines.Count);
            Assert.AreEqual(11, log.LinesDropped, "the run's first line and ten notes");
            Assert.AreEqual(PlaytestLog.MostNames + 1, log.Notes.Count, "the rest are counted as other");
            Assert.IsTrue(log.Notes.ContainsKey("other"));

            PlaytestLog many = NewLog();
            for (int i = 0; i < PlaytestLog.MostAnimals + 5; i++) many.Meet(new Thing("a" + i), "Roller", "Moon", 1, null);
            Assert.AreEqual(PlaytestLog.MostAnimals, many.Animals.Count);
            Assert.AreEqual(5, many.AnimalsDropped);

            PlaytestLog warnings = NewLog();
            var roller = new Thing("roller");
            for (int i = 0; i < PlaytestLog.MostWarnings + 3; i++)
            {
                warnings.Warning(i, roller, "Roller", "Moon", 1, null, WindupStage.None, WindupStage.Winding, 10f, 10f);
                warnings.Warning(i + 0.5f, roller, "Roller", "Moon", 1, null, WindupStage.Winding, WindupStage.None, 10f, 10f);
            }
            Assert.AreEqual(PlaytestLog.MostWarnings, warnings.Warnings.Count);
            Assert.AreEqual(3, warnings.WarningsDropped);
        }

        [Test]
        public void TheClock_IsMinutesSecondsAndTenths_NeverRoundedUp()
        {
            Assert.AreEqual("0:00.0", PlaytestLog.Clock(0f));
            Assert.AreEqual("0:00.0", PlaytestLog.Clock(-3f));
            Assert.AreEqual("0:00.7", PlaytestLog.Clock(0.7f));
            Assert.AreEqual("0:59.9", PlaytestLog.Clock(59.99f));
            Assert.AreEqual("1:05.2", PlaytestLog.Clock(65.25f));
            Assert.AreEqual("61:01.0", PlaytestLog.Clock(3661f));
            Assert.AreEqual("1:05", PlaytestLog.Minutes(65.9));
            Assert.AreEqual("0:00", PlaytestLog.Minutes(double.NaN));
        }

        // ------------------------------------------------------------------ the written report

        static PlaytestLog BusyRun()
        {
            PlaytestLog log = NewLog();
            log.AnteBegan(0f, 1, 1200);
            log.Points(30f, 1, "Kill", "KILL", 80, true, "Awarded", 1, "kill.1");
            log.Note(31f, "TARGET MET");
            log.NoteShown(31f, "TARGET MET");
            log.UnlockMet(40f, "Moonwalker", true, false);
            log.UnlockShown(52f, "Moonwalker", 12f);
            var strider = new Thing("strider");
            log.Woke(60f, strider, "Strider", "Moon", 1, 105f, false);
            log.Seen(62f, strider, 90f);
            log.Noticed(65f, strider);
            FullWarning(log, strider, "Strider", 70f);
            log.Hurt(71.3f, 20f, StrikeWords.Claw);
            log.CreatureHit(72f, strider, "Strider", "Moon", 1, null, "Strider Carapace", HitZone.Armour, false, false, "Rifle", 30f);
            log.AimLine(72f, "Strider", strider, "Moon", 1, null);
            log.AimLineLook(true, false);
            log.CreatureHit(75f, strider, "Strider", "Moon", 1, null, "Strider Belly", HitZone.Vital, false, true, "Rifle", 25f, "creature:Moon.land1");
            log.Points(75f, 1, "CreatureKill", "KILL", 140, true, "Awarded", 0, "creature:Moon.land1");
            log.Slips(strider, 2);
            log.WakeFrame(strider, 9.5f, 21.5f, 8.2f, true);
            log.Shot(80f, "Rifle");
            log.PersonHit(81f, "Rifle", "Left Shin", false, true, false, false, false, 40f);
            log.Spotted(82f, true);
            log.SquadSent(90f);
            log.Music(0f, IntensityBand.Exploration, "Deep Orbit", 2, false);
            log.Music(65f, IntensityBand.Combat, "Deep Orbit", 5, false);
            log.Died(100f, "Shot down outside the ship.");
            log.Finish(100f, RunEnd.Died, "Shot down outside the ship.", 1, 80);
            return log;
        }

        [Test]
        public void TheReport_HasASectionForEachProtocol_AndCountsNeverPercentages()
        {
            string report = PlaytestReport.Write(BusyRun());
            foreach (string heading in new[]
                     {
                         PlaytestReport.Title, "ANTES", "POINTS", "UNLOCKS", "CREATURE WARNINGS", "THE DODGE", "HITS ON CREATURES", "CREATURES MET",
                         "PEOPLE AND GUNS", "YOUR HURT AND DEATHS", "MUSIC", "EVERY WARNING", "EVERY EVENT",
                     })
                StringAssert.Contains(heading, report);
            Assert.IsFalse(report.Contains("%"), "counts, never percentages");
            Assert.IsFalse(report.Contains("NaN") || report.Contains("Infinity"));
            StringAssert.Contains("Strider #1 (Japanese spider crab) on Moon", report);
            StringAssert.Contains("killed by you", report);
            StringAssert.Contains("feet slipped 2 times", report);
            StringAssert.Contains("seed 12345", report);
            StringAssert.Contains("it paid 140 points", report);
            StringAssert.Contains("its wake frame 9.5 ms, the longest in the half second after 21.5 ms, the middle frame of the second before 8.2 ms (the first of its kind this session)", report);
        }

        [Test]
        public void AnEmptyRun_StillWritesAReport()
        {
            var log = new PlaytestLog();
            log.Begin("practice range", 1, "", "", null, null, false);
            log.Finish(0f, RunEnd.Left, null, 0, 0);
            string report = PlaytestReport.Write(log);
            StringAssert.Contains(PlaytestReport.Title, report);
            StringAssert.Contains("EVERY EVENT", report);
            Assert.IsFalse(report.Contains("CREATURE WARNINGS"), "a section with nothing in it is left out");
        }

        [Test]
        public void TheReport_ReadsTheSameEachTimeItIsWritten()
        {
            PlaytestLog log = BusyRun();
            Assert.AreEqual(PlaytestReport.Write(log), PlaytestReport.Write(log));
        }

        // ------------------------------------------------------------------ the dodge (pass 2), on game time

        [Test]
        public void TheSpeedAcrossTheLine_LeavesOutWhatIsAlongItAndWhatIsUp()
        {
            var up = new Vec3d(0, 1, 0);
            // The player 10 m in front of it along z, stepping 0.1 m sideways in a tenth of a second: 1 m/s across.
            Assert.AreEqual(1.0, DodgeRules.AcrossSpeed(new Vec3d(0, 0, 10), new Vec3d(0.1, 0, 10), up, 0.1), 0.01);
            // Straight back along the line: nothing across.
            Assert.AreEqual(0.0, DodgeRules.AcrossSpeed(new Vec3d(0, 0, 10), new Vec3d(0, 0, 10.5), up, 0.1), 1e-9);
            // A jump is up, not across.
            Assert.AreEqual(0.0, DodgeRules.AcrossSpeed(new Vec3d(0, 0, 10), new Vec3d(0, 0.3, 10), up, 0.1), 1e-9);
            // In a sea there is no up: rising across the line counts.
            Assert.AreEqual(3.0, DodgeRules.AcrossSpeed(new Vec3d(0, 0, 10), new Vec3d(0, 0.3, 10), new Vec3d(0, 0, 0), 0.1), 0.01);
            // A paused frame says nothing.
            Assert.AreEqual(0.0, DodgeRules.AcrossSpeed(new Vec3d(0, 0, 10), new Vec3d(5, 0, 10), up, 0.0));
        }

        [Test]
        public void TheSpeedIsSmoothed_SoAFootstepOrOneLongFrameIsNotAMove()
        {
            float s = 0f;
            s = DodgeRules.Smooth(s, 10f, 0.016f);
            Assert.Less(s, DodgeRules.SidestepSpeed, "one frame's jump of the eye is not a step aside");
            for (int i = 0; i < 30; i++) s = DodgeRules.Smooth(s, 4f, 0.016f);
            Assert.AreEqual(4f, s, 0.05f, "half a second of walking across reads as walking across");
            Assert.AreEqual(s, DodgeRules.Smooth(s, 0f, 0f), "no time, no change");
        }

        static void Across(PlaytestLog log, object who, float from, float to, float speed)
        {
            for (float t = from; t < to - 1e-4f; t += 0.02f) log.Across(t, who, speed, 0.02f);
        }

        [Test]
        public void TheDodge_SaysWhetherTheyMovedOnTheMark_BeforeIt_OnlyAfterTheStrike_OrNotAtAll()
        {
            PlaytestLog log = NewLog();
            var onMark = new Thing("on the mark");
            var already = new Thing("already");
            var late = new Thing("late");
            var still = new Thing("still");

            // Stood still through the wind-up, stepped aside 0.1 s after the mark (0.5 s), before the strike (0.8 s).
            Warn(log, onMark, "Strider", 0f, WindupStage.None, WindupStage.Winding);
            Across(log, onMark, 0f, 0.5f, 0f);
            Warn(log, onMark, "Strider", 0.5f, WindupStage.Winding, WindupStage.Aimed);
            Across(log, onMark, 0.5f, 0.6f, 0f);
            Across(log, onMark, 0.6f, 0.8f, 5f);
            Warn(log, onMark, "Strider", 0.8f, WindupStage.Aimed, WindupStage.Striking);
            Warn(log, onMark, "Strider", 1f, WindupStage.Striking, WindupStage.None);
            Assert.IsFalse(log.Across(1.1f, onMark, 5f, 0.02f), "over: nothing more to follow");

            // Walking across the whole time.
            Warn(log, already, "Strider", 10f, WindupStage.None, WindupStage.Winding);
            Across(log, already, 10f, 10.5f, 5f);
            Warn(log, already, "Strider", 10.5f, WindupStage.Winding, WindupStage.Aimed);
            Warn(log, already, "Strider", 10.8f, WindupStage.Aimed, WindupStage.Striking);
            Warn(log, already, "Strider", 11f, WindupStage.Striking, WindupStage.None);

            // Moved only once it struck.
            Warn(log, late, "Strider", 20f, WindupStage.None, WindupStage.Winding);
            Warn(log, late, "Strider", 20.5f, WindupStage.Winding, WindupStage.Aimed);
            Across(log, late, 20.5f, 20.8f, 0f);
            Warn(log, late, "Strider", 20.8f, WindupStage.Aimed, WindupStage.Striking);
            Across(log, late, 20.8f, 21f, 6f);
            Warn(log, late, "Strider", 21f, WindupStage.Striking, WindupStage.None);

            // Never moved across; and a warning called off has no mark to dodge.
            FullWarning(log, still, "Strider", 30f);
            Warn(log, still, "Strider", 40f, WindupStage.None, WindupStage.Winding);
            Across(log, still, 40f, 40.3f, 5f);
            Warn(log, still, "Strider", 40.3f, WindupStage.Winding, WindupStage.None);

            Assert.AreEqual(MoveOnMark.OnTheMark, log.Warnings[0].Move);
            Assert.Greater(log.Warnings[0].MovedGame - log.Warnings[0].AimedGame, 0.1f);
            Assert.Less(log.Warnings[0].MovedGame, log.Warnings[0].StruckGame);
            Assert.AreEqual(MoveOnMark.AlreadyMoving, log.Warnings[1].Move);
            Assert.AreEqual(MoveOnMark.AfterTheStrike, log.Warnings[2].Move);
            Assert.AreEqual(MoveOnMark.NotAcross, log.Warnings[3].Move);
            Assert.AreEqual(MoveOnMark.NoMark, log.Warnings[4].Move);

            string report = PlaytestReport.Write(log);
            StringAssert.Contains("THE DODGE", report);
            StringAssert.Contains("moved across 0.1 s after its mark", report);
            StringAssert.Contains("already moving across at its mark", report);
            StringAssert.Contains("moved across only after it struck", report);
            StringAssert.Contains("did not move across", report);
        }

        [Test]
        public void WhereItWasAtItsMark_AndShotsDuringIt_AreKept()
        {
            PlaytestLog log = NewLog();
            var behind = new Thing("behind");
            log.Warning(0f, behind, "Roller", "Moon", 1, null, WindupStage.None, WindupStage.Winding, 170f, 20f);
            log.Shot(0.2f, "Rifle");
            log.Warning(0.6f, behind, "Roller", "Moon", 1, null, WindupStage.Winding, WindupStage.Aimed, 20f, 15f, true);
            log.Shot(0.7f, "Rifle");
            log.Warning(1f, behind, "Roller", "Moon", 1, null, WindupStage.Aimed, WindupStage.None, 20f, 15f, true);
            log.Shot(2f, "Rifle");
            WarningRecord w = log.Warnings[0];
            Assert.IsTrue(w.InFrameAtMark);
            Assert.AreEqual(2, w.Shots, "a shot after it is over is not its");
            Assert.AreEqual(3, log.Guns["Rifle"].Shots);
            Assert.AreEqual(WarningEnd.Stopped, w.End, "it took aim and never struck");
            StringAssert.Contains("in view", PlaytestReport.Write(log));
        }

        [Test]
        public void TheGracesAreGameTime_SoAPauseOrFocusNeverStretchesThem()
        {
            PlaytestLog log = NewLog();
            var strider = new Thing("strider");
            log.Warning(new Moment(1f, 1f), strider, "Strider", "Moon", 1, null, WindupStage.None, WindupStage.Winding, 10f, 9f);
            log.Warning(new Moment(1.5f, 1.5f), strider, "Strider", "Moon", 1, null, WindupStage.Winding, WindupStage.Aimed, 10f, 9f);
            log.Warning(new Moment(1.8f, 1.8f), strider, "Strider", "Moon", 1, null, WindupStage.Aimed, WindupStage.Striking, 10f, 9f);
            log.Warning(new Moment(2f, 2f), strider, "Strider", "Moon", 1, null, WindupStage.Striking, WindupStage.None, 10f, 9f);
            // The game was paused for half a minute between the strike's end and its hurt landing: still its hurt.
            log.Hurt(new Moment(32.5f, 2.5f), 20f, StrikeWords.Claw);
            Assert.AreEqual(WarningEnd.Hurt, log.Warnings[0].End);

            // How long a kill took is game time too.
            log.CreatureHit(new Moment(40f, 10f), strider, "Strider", "Moon", 1, null, "Strider Carapace", HitZone.Armour, false, false, "Rifle", 20f);
            log.CreatureHit(new Moment(80f, 14f), strider, "Strider", "Moon", 1, null, "Strider Belly", HitZone.Vital, false, true, "Rifle", 20f, "creature:Moon.land1");
            AnimalRecord a = log.Find(strider);
            Assert.AreEqual(40f, a.FirstHitAt, "lines keep the real clock, for the recording");
            Assert.AreEqual(4f, a.EndGame - a.FirstHitGame, 1e-4f);
            StringAssert.Contains("4.0 s of game time after your first hit", PlaytestReport.Write(log));
        }

        [Test]
        public void OnlyTheKindsThatHoldOn_BiteAgainAfterTheirWarning()
        {
            // A Roller's butt after its roll is over is no warning's: it has none (the critique loop's open item).
            PlaytestLog log = NewLog();
            var roller = new Thing("roller");
            FullWarning(log, roller, "Roller", 1f);
            log.Hurt(2f + PlaytestLog.HurtGrace + 2f, 10f, StrikeWords.Butt);
            Assert.AreEqual(1, log.UnwarnedHurts);
            Assert.AreEqual(0f, log.Find(roller).DamageToYou);
            Assert.IsFalse(PlaytestLog.HoldsOn("Roller"));
            Assert.IsTrue(PlaytestLog.HoldsOn("Ambusher"));
            Assert.IsTrue(PlaytestLog.HoldsOn("Maw"));
        }

        [Test]
        public void AWarningPutAway_OrWhosePackIsGone_IsClosed_AndTheObjectsAreLetGo()
        {
            PlaytestLog log = NewLog();
            var strider = new Thing("strider");
            log.Woke(0f, strider, "Strider", "Moon", 1, 100f, false);
            Warn(log, strider, "Strider", 5f, WindupStage.None, WindupStage.Winding);
            Warn(log, strider, "Strider", 5.5f, WindupStage.Winding, WindupStage.Aimed);
            AnimalRecord s = log.Find(strider);
            log.PutAway(6f, strider);
            Assert.AreEqual(WarningEnd.Stopped, log.Warnings[0].End, "taken aim, never struck: put away");
            Assert.AreEqual(6f, log.Warnings[0].OverAt);
            Assert.AreEqual(AnimalEnd.PutAway, s.End);

            var shoal = new Thing("shoal");
            var fish = new Thing("fish");
            var other = new Thing("other fish");
            log.Woke(10f, shoal, PlaytestLog.PackOf("Biter"), "Earth", 1, 80f, true);
            log.CreatureHit(11f, other, "Biter", "Earth", 1, shoal, "Biter Head", HitZone.Vital, false, true, "Smg", 5f, "creature:Earth.h0.p1");
            log.Warning(12f, fish, "Biter", "Earth", 1, shoal, WindupStage.None, WindupStage.Winding, 30f, 6f);
            AnimalRecord pack = log.Find(shoal);
            Assert.AreSame(pack, log.Find(fish).Group);
            log.GroupGone(13f, shoal);
            Assert.AreEqual(AnimalEnd.AllDead, pack.End);
            Assert.AreEqual(WarningEnd.CalledOff, log.Warnings[1].End, "its fish's warning ends with it");
            Assert.IsNull(log.Find(shoal));
            Assert.IsNull(log.Find(fish), "the pack's fish are let go with it");
            Assert.IsNull(log.Find(other));
            StringAssert.Contains("every fish dead by", PlaytestReport.Write(log));
        }

        [Test]
        public void ACreaturesPay_IsTheAntesOwnAnswer_HeardBeforeOrAfterItsKill()
        {
            PlaytestLog log = NewLog();
            var first = new Thing("pay heard after the kill");
            var second = new Thing("pay heard before the kill");
            var refused = new Thing("refused");
            var away = new Thing("out of reach");
            var range = new Thing("practice range");

            log.CreatureHit(1f, first, "Roller", "Mars", 1, null, "Roller Soft", HitZone.Vital, false, true, "Rifle", 9f, "creature:Mars.land1");
            log.Points(1f, 1, "CreatureKill", "KILL", 90, true, "Awarded", 0, "creature:Mars.land1");
            log.Points(2f, 1, "CreatureKill", "ONE SHOT", 130, true, "Awarded", 1, "creature:Mars.land2");
            log.CreatureHit(2f, second, "Roller", "Mars", 1, null, "Roller Soft", HitZone.Vital, false, true, "Rifle", 9f, "creature:Mars.land2");
            log.Points(3f, 1, "CreatureKill", "KILL", 0, false, "DuplicateClaim", 0, "creature:Mars.land3");
            log.CreatureHit(3f, refused, "Roller", "Mars", 1, null, "Roller Soft", HitZone.Vital, false, true, "Rifle", 9f, "creature:Mars.land3");
            log.CreatureHit(4f, away, "Maw", "Neptune", 1, null, "Maw Head", HitZone.Armour, false, true, "Rifle", 30f, "creature:Neptune.h2.p0", false);
            log.CreatureHit(5f, range, "Roller", "practice range", 0, null, "Roller Soft", HitZone.Vital, false, true, "Rifle", 9f);

            Assert.AreEqual(90, log.Find(first).PaidPoints);
            Assert.AreEqual(130, log.Find(second).PaidPoints);
            Assert.AreEqual(0, log.Find(refused).PaidPoints);
            Assert.AreEqual("DuplicateClaim", log.Find(refused).PayRefused);
            Assert.IsTrue(log.Find(away).OutOfReach);
            Assert.IsTrue(log.Find(range).NoCredit);
            StringAssert.Contains("it paid 90 points", PlaytestReport.PayWords(log.Find(first)));
            StringAssert.Contains("the ante refused it (DuplicateClaim)", PlaytestReport.PayWords(log.Find(refused)));
            StringAssert.Contains("it could not answer you there", PlaytestReport.PayWords(log.Find(away)));
            StringAssert.Contains(Tasks.CreatureTiers.NoPayNote("Maw"), PlaytestReport.PayWords(log.Find(away)));
            StringAssert.Contains("nothing pays for it here", PlaytestReport.PayWords(log.Find(range)));
        }

        [Test]
        public void WhetherPeopleOrTheCreatureCameFirst_IsKept()
        {
            PlaytestLog log = NewLog();
            var afterPeople = new Thing("after people");
            var first = new Thing("first");
            log.Woke(0f, afterPeople, "Strider", "Moon", 1, 100f, false);
            log.Woke(0f, first, "Roller", "Moon", 1, 100f, false);
            log.Noticed(10f, first);
            log.EnemySawYou(30f);
            log.Noticed(30f + PlaytestLog.FightHold - 1f, afterPeople);
            log.EnemySawYou(30f + PlaytestLog.FightHold);
            Assert.IsTrue(log.Find(afterPeople).PeopleFirst, "a camp was after the player as it began");
            Assert.IsTrue(log.Find(afterPeople).EnemySawYou);
            Assert.IsFalse(log.Find(first).PeopleFirst);
        }

        [Test]
        public void Pauses_AreCountedAndTimed_OnTheRealClock()
        {
            PlaytestLog log = NewLog();
            log.Paused(10f, true);
            log.Paused(12f, true);
            log.Paused(25f, false);
            log.Paused(26f, false);
            log.Paused(40f, true);
            log.Snapshot(45f);
            Assert.AreEqual(2, log.Pauses);
            Assert.AreEqual(20f, log.PausedSeconds, 1e-4f, "15 s, and 5 s of a pause still on as the log was written");
            StringAssert.Contains("paused 2 times", PlaytestReport.Write(log));
        }

        [Test]
        public void TheMusicsFightEntriesAndLayers_AreKept()
        {
            PlaytestLog log = NewLog();
            log.MusicLayers(1f, "pad + bass");
            log.MusicState(1f, false, false);
            log.MusicState(5f, true, true);
            log.MusicState(5.25f, true, true);
            log.MusicEntry(5.5f, "Night Shift", 17);
            log.MusicState(40f, false, true);
            log.MusicState(60f, true, true);
            log.MusicLayers(60f, null);
            Assert.AreEqual(2, log.FightMusicTimes);
            Assert.AreEqual(1, log.SomeoneAfterTimes);
            Assert.AreEqual(2, log.LayerChanges);
            Assert.AreEqual(1, log.FightEntries.Count);
            StringAssert.Contains("Night Shift, bar 17", log.FightEntries[0]);
            log.Music(0f, IntensityBand.Combat, "Night Shift", 4, false);
            StringAssert.Contains("Night Shift, bar 17", PlaytestReport.Write(log));
        }

        [Test]
        public void TheNoteBoard_CountsWhatItShows_NotWhatIsOffered()
        {
            var board = new Ante.NoteBoard();
            board.Offer("FIRST", false);
            Assert.AreEqual(1, board.Showings);
            board.Offer("TARGET MET", true);
            Assert.AreEqual(2, board.Showings);
            board.Offer("WAITS", false);
            board.Offer("WAITS AND IS REPLACED FIRST", false);
            Assert.AreEqual(2, board.Showings, "a plain note waits out the important one's hold");
            board.Tick(Ante.NoteBoard.HoldSeconds + 0.1f);
            Assert.AreEqual(3, board.Showings, "the one still waiting shows; the one it replaced never does");
            Assert.AreEqual("WAITS AND IS REPLACED FIRST", board.Text);
            board.Offer("", false);
            Assert.AreEqual(3, board.Showings);
        }

        // ------------------------------------------------------------------ the horizon and the frames

        [Test]
        public void TheSightLine_IsHiddenByTheCurveOfAWorld_AndByAHill()
        {
            const double R = 1000.0;
            var eye = new Vec3d(0, R + 1.6, 0);
            Func<double, Vec3d> onTheGround = s => new Vec3d(Math.Sin(s / R), Math.Cos(s / R), 0) * (R + 1.0);
            Func<Vec3d, double> flat = d => R;
            Assert.IsTrue(SightLine.ClearOfGround(eye, onTheGround(20), flat));
            Assert.IsTrue(SightLine.ClearOfGround(eye, onTheGround(80), flat), "80 m off on a world 1 km across: in sight");
            Assert.IsFalse(SightLine.ClearOfGround(eye, onTheGround(150), flat), "150 m off: under its horizon");
            Func<Vec3d, double> hill = d =>
            {
                double s = Math.Atan2(d.X, d.Y) * R;
                return s >= 30 && s <= 40 ? R + 10 : R;
            };
            Assert.IsFalse(SightLine.ClearOfGround(eye, onTheGround(80), hill), "a hill between");
            Assert.IsTrue(SightLine.ClearOfGround(eye, onTheGround(20), hill), "nearer than the hill");
            Assert.IsTrue(SightLine.ClearOfGround(eye, onTheGround(150), null), "no ground known: nothing in the way");
            Assert.IsTrue(SightLine.ClearOfGround(eye, eye + new Vec3d(1, 0, 0), flat), "closer than a step");
        }

        [Test]
        public void TheFrameTimes_GiveTheMiddleOfTheLastSecondOrSo()
        {
            var frames = new FrameTimes();
            Assert.AreEqual(-1f, frames.Median());
            frames.Add(16f);
            frames.Add(40f);
            frames.Add(17f);
            Assert.AreEqual(17f, frames.Median());
            frames.Add(18f);
            Assert.AreEqual(17.5f, frames.Median());
            for (int i = 0; i < FrameTimes.Size; i++) frames.Add(10f);
            Assert.AreEqual(FrameTimes.Size, frames.Count);
            Assert.AreEqual(10f, frames.Median(), "only the newest are kept");
            frames.Clear();
            Assert.AreEqual(-1f, frames.Median());
        }

        [Test]
        public void AWakeThatCouldNotBeTimed_SaysSo()
        {
            PlaytestLog log = NewLog();
            var roller = new Thing("roller");
            log.Woke(1f, roller, "Roller", "Mars", 1, 100f, false);
            log.WakeUntimed(roller);
            StringAssert.Contains("its wake not timed", PlaytestReport.Write(log));
        }

        // ------------------------------------------------------------------ the files

        [Test]
        public void ARunsFile_IsNamedByWhenItBegan()
        {
            Assert.AreEqual("run_2026-11-28_100215_solar-system_seed12345.txt",
                PlaytestFiles.NameFor(new DateTime(2026, 11, 28, 10, 2, 15), "solar system", 12345));
            Assert.AreEqual("run_2026-01-02_030405_practice-range_seed-7.txt",
                PlaytestFiles.NameFor(new DateTime(2026, 1, 2, 3, 4, 5), "Practice range!", -7));
            Assert.AreEqual("run_2026-01-02_030405_run_seed1.txt", PlaytestFiles.NameFor(new DateTime(2026, 1, 2, 3, 4, 5), null, 1));
        }

        [Test]
        public void Writing_KeepsTheRunAndTheLatest_AndTrimsTheOldestRuns()
        {
            string folder = Path.Combine(Path.GetTempPath(), "oa-playtest-" + Guid.NewGuid().ToString("N"));
            try
            {
                string path = PlaytestFiles.Write(folder, "run_2026-11-28_100000_solar-system_seed1.txt", "first");
                Assert.AreEqual("first", File.ReadAllText(path));
                Assert.AreEqual("first", File.ReadAllText(Path.Combine(folder, PlaytestFiles.Latest)));
                PlaytestFiles.Write(folder, "run_2026-11-28_110000_solar-system_seed2.txt", "second");
                Assert.AreEqual("second", File.ReadAllText(Path.Combine(folder, PlaytestFiles.Latest)));

                for (int i = 0; i < 5; i++) File.WriteAllText(Path.Combine(folder, "run_2026-11-2" + i + "_000000_x_seed0.txt"), "old " + i);
                File.WriteAllText(Path.Combine(folder, "notes.txt"), "Matthew's own file");
                int deleted = PlaytestFiles.Trim(folder, 3);
                Assert.AreEqual(4, deleted, "seven runs, three kept");
                string[] left = Directory.GetFiles(folder, "run_*.txt");
                Array.Sort(left, StringComparer.Ordinal);
                Assert.AreEqual(3, left.Length);
                StringAssert.EndsWith("seed1.txt", left[1]);
                StringAssert.EndsWith("seed2.txt", left[2]);
                Assert.IsTrue(File.Exists(Path.Combine(folder, PlaytestFiles.Latest)), "latest.txt is never trimmed");
                Assert.IsTrue(File.Exists(Path.Combine(folder, "notes.txt")), "nor anything that is not a run");
                Assert.AreEqual(0, PlaytestFiles.Trim(Path.Combine(folder, "missing"), 3));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }
    }
}
