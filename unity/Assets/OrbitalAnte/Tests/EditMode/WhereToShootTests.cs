using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.Tasks;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Where to shoot each creature (Ralph loop pass 7): every kind has one weak place that rings gold and is its quickest
    /// kill, armour that only knocks, pieces it fights with that ring as a disarm when they come off (and are never grey
    /// while they hold), and a short line that names the piece that turned the shot and says where to aim, the first time
    /// its armour turns the player's shots.
    /// </summary>
    public class WhereToShootTests
    {
        /// <summary>A piece of a kind: what it is called here, the zone it says when hit and held, and the damage it takes to kill through it (0: it does not kill).</summary>
        struct Piece
        {
            public string Name;
            public HitZone Zone;
            public double KillCost;

            public Piece(string name, HitZone zone, double killCost = 0)
            {
                Name = name;
                Zone = zone;
                KillCost = killCost;
            }
        }

        /// <summary>Every piece of every kind, hit and holding, with the hit points that kill through it, from the kinds' own numbers.</summary>
        static Dictionary<string, List<Piece>> Kinds()
        {
            var kinds = new Dictionary<string, List<Piece>>();
            foreach (WalkerLayout l in new[] { WalkerLayout.Strider, WalkerLayout.Sprinter })
            {
                WalkerHp hp = l == WalkerLayout.Strider ? WalkerHp.Strider : WalkerHp.Sprinter;
                var pieces = new List<Piece>
                {
                    new Piece(l.Soft, WhereToShoot.Walker(true, false, false), hp.Belly),
                    new Piece(l.Armoured, WhereToShoot.Walker(false, true, false), hp.Carapace),
                };
                for (int limb = 0; limb < WalkerLayout.Limbs; limb++) pieces.Add(new Piece("limb " + limb, WhereToShoot.Walker(false, false, false)));
                if (l.HasJaws) pieces.Add(new Piece("jaw", WhereToShoot.Walker(false, false, false)));
                kinds[l.Model] = pieces;
            }

            RollerHp roller = RollerHp.Default;
            kinds["Roller"] = new List<Piece>
            {
                new Piece("head", WhereToShoot.Roller(RollerPiece.Head, false, false), roller.Head),
                new Piece("soft", WhereToShoot.Roller(RollerPiece.Soft, false, false), roller.Soft),
                new Piece("plate", WhereToShoot.Roller(RollerPiece.Plate, false, false)),
                new Piece("tail", WhereToShoot.Roller(RollerPiece.Tail, false, false)),
                new Piece("leg", WhereToShoot.Roller(RollerPiece.Leg, false, false)),
            };

            HunterHp hunter = HunterHp.Default;
            var hunterPieces = new List<Piece>
            {
                new Piece("head", WhereToShoot.Hunter(HunterPiece.Head, 0), hunter.Head),
                new Piece("leg", WhereToShoot.Hunter(HunterPiece.Leg, 0)),
            };
            for (int seg = 0; seg < HunterLayout.Segs; seg++)
            {
                bool fore = seg < HunterBody.ForeSegs;
                hunterPieces.Add(new Piece("seg " + seg, WhereToShoot.Hunter(HunterPiece.Seg, seg), fore ? hunter.Fore : 0));
            }
            kinds["Hunter"] = hunterPieces;

            ScreamerHp screamer = ScreamerHp.Default;
            kinds["Screamer"] = new List<Piece>
            {
                new Piece("fore body", WhereToShoot.Screamer(ScreamerPiece.Body, false), screamer.Body),
                new Piece("abdomen", WhereToShoot.Screamer(ScreamerPiece.Abdomen, false), screamer.Abdomen),
                new Piece("drum", WhereToShoot.Screamer(ScreamerPiece.Drum, false)),
                new Piece("wing", WhereToShoot.Screamer(ScreamerPiece.Wing, false)),
                new Piece("leg", WhereToShoot.Screamer(ScreamerPiece.Leg, false)),
            };

            AmbusherHp ambusher = AmbusherHp.Default;
            kinds["Ambusher"] = new List<Piece>
            {
                new Piece("head", WhereToShoot.Ambusher(AmbusherPiece.Head, false), ambusher.Head),
                new Piece("body", WhereToShoot.Ambusher(AmbusherPiece.Seg, false), ambusher.Seg),
                new Piece("jaw", WhereToShoot.Ambusher(AmbusherPiece.Jaw, false)),
                new Piece("feeler", WhereToShoot.Ambusher(AmbusherPiece.Feeler, false)),
            };

            StalkerHp stalker = StalkerHp.Default;
            var stalkerPieces = new List<Piece>
            {
                new Piece("skull", WhereToShoot.Stalker(StalkerPiece.Head, 0), stalker.Head),
                new Piece("leg", WhereToShoot.Stalker(StalkerPiece.Leg, 0)),
                new Piece("gill", WhereToShoot.Stalker(StalkerPiece.Gill, 0)),
            };
            for (int seg = 0; seg < StalkerLayout.Segs; seg++)
            {
                bool trunk = !StalkerLayout.IsTail(seg);
                stalkerPieces.Add(new Piece("seg " + seg, WhereToShoot.Stalker(StalkerPiece.Seg, seg), trunk ? stalker.Trunk : 0));
            }
            kinds["Stalker"] = stalkerPieces;

            MawHp maw = MawHp.Default;
            kinds["Maw"] = new List<Piece>
            {
                new Piece("skull", WhereToShoot.Maw(MawPiece.Head, false), maw.Head),
                new Piece("throat", WhereToShoot.Maw(MawPiece.Throat, false), maw.Trunk / MawBody.ThroatTimes),
                new Piece("body", WhereToShoot.Maw(MawPiece.Seg, false), maw.Trunk),
                new Piece("jaw", WhereToShoot.Maw(MawPiece.Jaw, false)),
                new Piece("fin", WhereToShoot.Maw(MawPiece.Fin, false)),
                new Piece("back fin", WhereToShoot.Maw(MawPiece.BackFin, false)),
                new Piece("tail fin", WhereToShoot.Maw(MawPiece.TailFin, false)),
            };

            BiterHp biter = BiterHp.Default;
            kinds["Biter"] = new List<Piece>
            {
                new Piece("head", WhereToShoot.Biter(BiterPiece.Head, false), biter.Head),
                new Piece("body", WhereToShoot.Biter(BiterPiece.Seg, false), biter.Trunk),
                new Piece("jaw", WhereToShoot.Biter(BiterPiece.Jaw, false)),
                new Piece("fin", WhereToShoot.Biter(BiterPiece.Fin, false)),
                new Piece("back fin", WhereToShoot.Biter(BiterPiece.BackFin, false)),
                new Piece("tail fin", WhereToShoot.Biter(BiterPiece.TailFin, false)),
            };
            return kinds;
        }

        [Test]
        public void EveryCreatureKind_IsCovered()
        {
            Dictionary<string, List<Piece>> kinds = Kinds();
            foreach (string kind in CreatureTiers.Kinds) Assert.IsTrue(kinds.ContainsKey(kind), kind + " has no zones");
            Assert.AreEqual(CreatureTiers.Kinds.Length, kinds.Count);
        }

        [Test]
        public void EveryCreature_HasOneWeakPlace_AndItIsTheQuickestKill()
        {
            foreach (KeyValuePair<string, List<Piece>> kind in Kinds())
            {
                var vitalNames = new HashSet<string>();
                double vitalCost = double.MaxValue;
                foreach (Piece p in kind.Value)
                {
                    if (p.Zone != HitZone.Vital) continue;
                    // The Cave hunter's and the Blind stalker's soft lengths are one place, one shared number.
                    vitalNames.Add(p.Name.StartsWith("seg ") ? "seg" : p.Name);
                    Assert.Greater(p.KillCost, 0.0, kind.Key + "'s " + p.Name + " rings gold but cannot kill it");
                    vitalCost = System.Math.Min(vitalCost, p.KillCost);
                }
                Assert.AreEqual(1, vitalNames.Count, kind.Key + " must have exactly one weak place");
                foreach (Piece p in kind.Value)
                {
                    if (p.Zone == HitZone.Vital || p.KillCost <= 0) continue;
                    Assert.Less(vitalCost, p.KillCost, kind.Key + ": its weak place must kill quicker than its " + p.Name);
                }
            }
        }

        [Test]
        public void ArmourThatComesOff_KnocksWhileItHolds_ThenIsAPieceOff()
        {
            // Knock while they hold, then plain (or a weapon) once broken: a plate shot off rings as a piece off.
            Assert.AreEqual(HitZone.Armour, WhereToShoot.Roller(RollerPiece.Plate, false, false));
            Assert.AreEqual(HitZone.Plain, WhereToShoot.Roller(RollerPiece.Plate, true, false), "a plate shot off is a piece off, not a glance");
            Assert.AreEqual(HitZone.Plain, WhereToShoot.Roller(RollerPiece.Tail, true, false));
        }

        [Test]
        public void APieceToShootOff_IsNeverGrey_WhileItHolds()
        {
            // Grey means "the slow way". A jaw that holds the player is shot off to make it let go, and a drum to choke a
            // scream, so neither may ever be marked as armour (game-designer review, pass 7: the Ambusher's jaw was).
            Assert.AreEqual(HitZone.Plain, WhereToShoot.Ambusher(AmbusherPiece.Jaw, false), "the Ambusher's jaw, holding the player or not");
            Assert.AreEqual(HitZone.WeaponOff, WhereToShoot.Ambusher(AmbusherPiece.Jaw, true));
            Assert.AreEqual(HitZone.Plain, WhereToShoot.Maw(MawPiece.Jaw, false));
            Assert.AreEqual(HitZone.Plain, WhereToShoot.Biter(BiterPiece.Jaw, false));
            Assert.AreEqual(HitZone.Plain, WhereToShoot.Screamer(ScreamerPiece.Drum, false));
            Assert.AreEqual(HitZone.Plain, WhereToShoot.Walker(false, false, false), "a walker's claw arm or jaw that is still on");
            foreach (KeyValuePair<string, List<Piece>> kind in Kinds())
                foreach (Piece p in kind.Value)
                    if (p.Name == "jaw" || p.Name == "drum")
                        Assert.AreNotEqual(HitZone.Armour, p.Zone, kind.Key + "'s " + p.Name + " is marked as the slow way");
        }

        [Test]
        public void APieceItFightsWith_ShotOff_IsAWeaponOff()
        {
            WalkerLayout strider = WalkerLayout.Strider, sprinter = WalkerLayout.Sprinter;
            Assert.IsTrue(WhereToShoot.WalkerWeapon(strider, 0), "the Strider strikes with the claws on its arms");
            Assert.IsTrue(WhereToShoot.WalkerWeapon(strider, WalkerLayout.PerSide), "both arms");
            Assert.IsFalse(WhereToShoot.WalkerWeapon(strider, 1), "a leg is not a weapon");
            Assert.IsFalse(WhereToShoot.WalkerWeapon(sprinter, 0), "the Sprinter's arms only feel and warn");
            Assert.IsTrue(WhereToShoot.WalkerWeapon(sprinter, WalkerLayout.Limbs), "its jaws bite");
            Assert.IsTrue(WhereToShoot.WalkerWeapon(sprinter, WalkerLayout.Limbs + 1));
            Assert.AreEqual(HitZone.WeaponOff, WhereToShoot.Walker(false, false, true));
            Assert.AreEqual(HitZone.WeaponOff, WhereToShoot.Screamer(ScreamerPiece.Drum, true), "a drum is its scream");
            Assert.AreEqual(HitZone.Plain, WhereToShoot.Screamer(ScreamerPiece.Drum, false));
            Assert.AreEqual(HitZone.WeaponOff, WhereToShoot.Maw(MawPiece.Jaw, true));
            Assert.AreEqual(HitZone.WeaponOff, WhereToShoot.Biter(BiterPiece.Jaw, true));
            Assert.AreEqual(HitZone.WeaponOff, WhereToShoot.Roller(RollerPiece.Plate, true, true), "the plate that ends its rolling");
            // Fins, wings and tails cripple; they are limbs off, not a weapon.
            Assert.AreEqual(HitZone.Plain, WhereToShoot.Biter(BiterPiece.TailFin, true));
            Assert.AreEqual(HitZone.Plain, WhereToShoot.Screamer(ScreamerPiece.Wing, true));
        }

        [Test]
        public void TheMarkerSaysIt_GlanceWeakPlaceAndWeaponOff()
        {
            HitKind Hit(HitZone zone, bool severed = false, bool killed = false) =>
                HitConfirm.Classify(true, true, false, severed, false, killed, false, zone);

            Assert.AreEqual(HitKind.Glance, Hit(HitZone.Armour));
            Assert.AreEqual(HitKind.Head, Hit(HitZone.Vital), "the weak place rings like a head");
            Assert.AreEqual(HitKind.HeadshotKill, Hit(HitZone.Vital, killed: true), "a kill through it is the best kill");
            Assert.AreEqual(HitKind.Kill, Hit(HitZone.Armour, killed: true), "a kill through the armour is a plain kill");
            Assert.AreEqual(HitKind.Disarm, Hit(HitZone.WeaponOff, severed: true));
            Assert.AreEqual(HitKind.Limb, Hit(HitZone.Plain, severed: true));
            Assert.AreEqual(HitKind.Body, Hit(HitZone.Plain));
            // People are unchanged: no zone, the old flags.
            Assert.AreEqual(HitKind.Body, HitConfirm.Classify(true, true, false, false, false, false, false));
            Assert.AreEqual(HitKind.Head, HitConfirm.Classify(true, true, true, false, false, false, false));
            Assert.AreEqual(HitKind.Disarm, HitConfirm.Classify(true, false, false, false, true, false, false));
            // A hit on something that is not a living enemy is never confirmed, whatever zone it says.
            Assert.AreEqual(HitKind.None, HitConfirm.Classify(true, false, false, false, false, false, false, HitZone.Vital));
        }

        [Test]
        public void AGlance_IsSmallGreyFlatAndShort_AndNeverRingsTheBell()
        {
            Assert.AreEqual(MarkerTone.Grey, HitConfirm.Tone(HitKind.Glance));
            Assert.Less(HitConfirm.TickLength(HitKind.Glance), HitConfirm.TickLength(HitKind.Body));
            Assert.Less(HitConfirm.TickWidth(HitKind.Glance), HitConfirm.TickWidth(HitKind.Body));
            Assert.Less(HitConfirm.Seconds(HitKind.Glance), HitConfirm.Seconds(HitKind.Body));
            Assert.AreEqual(1f, HitConfirm.Scale(HitKind.Glance, 0f), "it does not pop: nothing much happened");
            Assert.IsFalse(HitConfirm.OuterRing(HitKind.Glance));
            Assert.AreEqual(HitConfirm.GlanceDing, HitConfirm.Ding(HitKind.Glance));
            foreach (string bell in new[] { HitConfirm.BodyDing, HitConfirm.KillDing, HitConfirm.HeadshotDing })
                Assert.AreNotEqual(bell, HitConfirm.Ding(HitKind.Glance), "a glance is a knock, never the bell");
            Assert.AreEqual(1f, HitConfirm.DingPitch(HitKind.Glance));
            // A glance never cuts a real hit's marker short, and any real hit takes over from a glance.
            Assert.IsFalse(HitConfirm.Replaces(HitKind.Body, 0.02f, HitKind.Glance));
            Assert.IsTrue(HitConfirm.Replaces(HitKind.Glance, 0.02f, HitKind.Body));
            Assert.IsTrue(HitConfirm.Replaces(HitKind.Glance, 0.02f, HitKind.Glance), "a burst on armour keeps it up");
            Assert.Less(HitConfirm.Rank(HitKind.Glance), HitConfirm.Rank(HitKind.Body));
            Assert.Greater(HitConfirm.Rank(HitKind.Glance), HitConfirm.Rank(HitKind.None));
        }

        [Test]
        public void EveryKindWithArmour_HasAShortLine_AndAKindWithoutSaysNothing()
        {
            foreach (KeyValuePair<string, List<Piece>> kind in Kinds())
            {
                bool armoured = false;
                foreach (Piece p in kind.Value) armoured |= p.Zone == HitZone.Armour;
                string line = WhereToShoot.Line(kind.Key);
                if (!armoured)
                {
                    Assert.IsNull(line, kind.Key + " has no armour, so nothing turns its shots");
                    continue;
                }
                Assert.IsNotNull(line, kind.Key + " has armour but no line saying where to aim");
                string shell = WhereToShoot.ShellWord(kind.Key);
                StringAssert.StartsWith(shell + " · ", line);
                Assert.IsTrue(Regex.IsMatch(shell, "^[A-Z]{3,5}$"), kind.Key + ": the piece that turned the shot is one short word, " + shell);
                // "ARMOUR" is the player's own suit on the suit plate (ARMOUR FAILING, NO ARMOUR LEFT): never here.
                StringAssert.DoesNotContain("ARMOUR", line, kind.Key);
                StringAssert.DoesNotContain("ARMOR", line, kind.Key);
                // Read in the second it is up, over the crosshair, at 15 px: at most 44 letters.
                Assert.LessOrEqual(line.Length, 44, kind.Key + ": " + line);
                Assert.AreEqual(line.ToUpperInvariant(), line, "the plates' words are capitals");
                string aim = WhereToShoot.AimLine(kind.Key);
                StringAssert.Contains("AIM", aim.Replace("SHOOT", "AIM"), "it says where to aim");
                // One place to aim at: a drum or a claw is taught by its own cyan mark.
                Assert.IsFalse(Regex.IsMatch(aim, @"\bOR\b"), kind.Key + " names two places: " + aim);
            }
            Assert.IsNull(WhereToShoot.Line("SomethingNew"), "a kind added later says nothing until it has its line");
            Assert.IsNull(WhereToShoot.ShellWord("Biter"), "the Pack biter has no armour");
        }

        [Test]
        public void TheLines_SayWhenTheWeakPlaceCanBeShot_WhereItIsHidden()
        {
            // The first glance off an Ambusher nearly always lands while only its head is out, and it ducks when hit, so its
            // line says when its body can be shot (game-designer review, pass 7).
            StringAssert.Contains("WHEN IT REARS", WhereToShoot.AimLine("Ambusher"));
            // A Roller's soft side shows only on its back ("flips" also fits its curl, and "underneath" faces up then).
            StringAssert.Contains("ON ITS BACK", WhereToShoot.AimLine("Roller"));
            StringAssert.DoesNotContain("FLIP", WhereToShoot.AimLine("Roller"));
            StringAssert.DoesNotContain("UNDERNEATH", WhereToShoot.AimLine("Roller"));
            // The Drifting maw's weak place is there only while its mouth is open.
            StringAssert.Contains("WHEN IT OPENS", WhereToShoot.AimLine("Maw"));
        }

        [Test]
        public void TheBestKill_IsTheSameForTheMarkerAndTheSlowMo()
        {
            foreach (HitZone zone in new[] { HitZone.Plain, HitZone.Armour, HitZone.Vital, HitZone.WeaponOff })
                foreach (bool killed in new[] { false, true })
                    foreach (bool headshot in new[] { false, true })
                    {
                        bool best = HitZones.BestKill(killed, headshot, zone);
                        HitKind shown = HitConfirm.Classify(true, true, false, false, false, killed, headshot, zone);
                        Assert.AreEqual(best, shown == HitKind.HeadshotKill, zone + (killed ? " killed" : "") + (headshot ? " headshot" : ""));
                    }
            Assert.IsTrue(HitZones.BestKill(true, false, HitZone.Vital), "a creature killed through its weak place");
            Assert.IsTrue(HitZones.BestKill(true, true, HitZone.Plain), "a person's headshot kill, as before");
            Assert.IsFalse(HitZones.BestKill(true, false, HitZone.Armour), "killed through its armour: a plain kill");
            Assert.IsFalse(HitZones.BestKill(false, false, HitZone.Vital), "a hit on the weak place that does not kill");
        }

        [Test]
        public void AnAimLine_ShowsOnlyOnAGlance_UntilTheWeakPlaceIsFound()
        {
            var lines = new AimLines();
            Assert.IsTrue(lines.Glanced("Strider", 10f), "the first glance off its lid says where to aim");
            Assert.IsFalse(lines.Glanced("Strider", 11f), "not again straight away");
            Assert.IsFalse(lines.Glanced("Roller", 11f + AimLines.Gap * 0.5f), "nor another kind's, inside the gap");
            Assert.IsTrue(lines.Glanced("Roller", 10f + AimLines.Gap + 0.1f));
            Assert.IsTrue(lines.Found("Strider"), "news: it hit the belly");
            Assert.IsFalse(lines.Found("Strider"), "not news twice");
            Assert.IsFalse(lines.Glanced("Strider", 1000f), "once found, never said again");
            Assert.IsFalse(lines.Glanced("Biter", 1000f), "a kind with no armour says nothing");
            Assert.IsFalse(lines.Glanced(null, 1000f));
            Assert.IsFalse(lines.Glanced(string.Empty, 1000f));
        }

        [Test]
        public void AnAimLine_IsSaidAtMostTwice_AndTheMemoryOutlivesTheSession()
        {
            var lines = new AimLines();
            float now = 0f;
            int shown = 0;
            for (int i = 0; i < 6; i++, now += AimLines.Gap + 1f)
                if (lines.Glanced("Maw", now)) shown++;
            Assert.AreEqual(AimLines.MostTold, shown);
            lines.Found("Stalker");

            AimLines back = AimLines.Parse(lines.Store());
            Assert.AreEqual(AimLines.MostTold, back.Told("Maw"));
            Assert.IsTrue(back.HasFound("Stalker"));
            Assert.IsFalse(back.Glanced("Maw", 1e6f), "told twice in an earlier session");
            Assert.IsFalse(back.Glanced("Stalker", 1e6f), "found in an earlier session");
            Assert.IsTrue(back.Glanced("Hunter", 1e6f), "a kind not met yet still has its line");
            Assert.AreEqual(lines.Store(), AimLines.Parse(lines.Store()).Store(), "stores the same way twice");

            // Whatever it cannot read it leaves out: the line just shows again.
            AimLines junk = AimLines.Parse("Maw=,=3,Roller=x,,Hunter=9,Strider=found");
            Assert.AreEqual(0, junk.Told("Maw"));
            Assert.AreEqual(0, junk.Told("Roller"));
            Assert.AreEqual(AimLines.MostTold, junk.Told("Hunter"), "clamped");
            Assert.IsTrue(junk.HasFound("Strider"));
            Assert.AreEqual(0, AimLines.Parse(null).Told("Maw"));
        }

        [Test]
        public void AnAimLine_HoldsThenFades()
        {
            Assert.AreEqual(1f, AimLines.Alpha(0f));
            Assert.AreEqual(1f, AimLines.Alpha(AimLines.Seconds - AimLines.FadeSeconds));
            Assert.AreEqual(0f, AimLines.Alpha(AimLines.Seconds));
            Assert.AreEqual(0f, AimLines.Alpha(-0.1f));
            Assert.Greater(AimLines.Alpha(AimLines.Seconds - AimLines.FadeSeconds * 0.5f), 0f);
            Assert.Less(AimLines.Alpha(AimLines.Seconds - AimLines.FadeSeconds * 0.5f), 1f);
            // Long enough to read eight words, short enough not to sit over a fight.
            Assert.GreaterOrEqual(AimLines.Seconds, 2.5f);
            Assert.LessOrEqual(AimLines.Seconds, 4f);
        }
    }
}
