using System;
using NUnit.Framework;
using OrbitalAnte.Cockpit;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Tests
{
    public class CockpitRulesTests
    {
        // ------------------------------------------------------------------ fuel and air

        [Test]
        public void CoastingCostsNoFuel_PushingDoes_BoostCostsMore()
        {
            var coast = new ShipSupplies();
            for (int i = 0; i < 500; i++) coast.Tick(0.02f, 0f, false, false, 0);
            Assert.AreEqual(1f, coast.Fuel, 1e-6);

            var push = new ShipSupplies();
            var boost = new ShipSupplies();
            for (int i = 0; i < 500; i++)
            {
                push.Tick(0.02f, 1f, false, false, 0);
                boost.Tick(0.02f, 1f, true, false, 0);
            }
            Assert.AreEqual(1f - 10f / push.Rules.fuelSeconds, push.Fuel, 1e-4);
            Assert.AreEqual(1f - 10f * push.Rules.boostCost / push.Rules.fuelSeconds, boost.Fuel, 1e-4);
        }

        [Test]
        public void ADryTank_LeavesAWeakReserve_NeverNothing()
        {
            var s = new ShipSupplies();
            Assert.AreEqual(1f, s.PushFactor);
            s.Set(0.001f, 1f);
            for (int i = 0; i < 200; i++) s.Tick(0.02f, 1f, true, false, 0);
            Assert.IsTrue(s.FuelOut);
            Assert.IsTrue(s.FuelLow);
            Assert.AreEqual(s.Rules.reservePush, s.PushFactor, 1e-6);
            Assert.Greater(s.PushFactor, 0.1f, "a dry tank must slow you, not strand you");
        }

        [Test]
        public void ThePadFillsBothTanks_ButNotWhileTheEnginesPush()
        {
            var s = new ShipSupplies();
            s.Set(0.2f, 0.3f);
            s.Tick(0.5f, 1f, false, true, 0);
            Assert.IsFalse(s.Filling);
            Assert.Less(s.Fuel, 0.2f);
            for (int i = 0; i < 400; i++) s.Tick(0.02f, 0f, false, true, 0);
            Assert.AreEqual(1f, s.Fuel, 1e-6);
            Assert.AreEqual(1f, s.Oxygen, 1e-6);
            Assert.IsFalse(s.Filling, "full tanks are not being filled");
        }

        [Test]
        public void ASealedCabinKeepsItsAir_ForNow_AndBrokenGlassLeaks()
        {
            var sealedCabin = new ShipSupplies();
            var holed = new ShipSupplies();
            var breathing = new ShipSupplies(new SupplyRules { sealedCabinUsesAir = true });
            float minutes = sealedCabin.Rules.oxygenMinutes;
            for (int i = 0; i < 600; i++)
            {
                sealedCabin.Tick(minutes * 0.05f, 0f, false, false, 0);
                holed.Tick(minutes * 0.05f, 0f, false, false, 2);
                breathing.Tick(minutes * 0.05f, 0f, false, false, 0);
            }
            Assert.AreEqual(1f, sealedCabin.Oxygen, 1e-6, "until the suit exists, a sealed cabin must not run down");
            Assert.AreEqual(0.5f, breathing.Oxygen, 1e-3, "with breathing switched on, half the stated minutes should use half the air");
            Assert.AreEqual(0f, holed.Oxygen, 1e-6);
            Assert.IsTrue(holed.OxygenLow);
        }

        [Test]
        public void StandingStillAnywhere_RefillsSlowly_ThePadQuickly()
        {
            var field = new ShipSupplies();
            var pad = new ShipSupplies();
            field.Set(0f, 1f);
            pad.Set(0f, 1f);
            for (int i = 0; i < 100; i++)
            {
                field.Tick(0.02f, 0f, false, false, 0, true);
                pad.Tick(0.02f, 0f, false, true, 0, true);
            }
            Assert.AreEqual(2f / field.Rules.fieldRefuelSeconds, field.Fuel, 1e-3);
            Assert.AreEqual(2f / pad.Rules.refuelSeconds, pad.Fuel, 1e-3);
            Assert.IsTrue(field.Filling);
            Assert.Greater(field.Rules.boostCost, 2.5f, "boost must cost more fuel than the extra push it gives (the craft's boost is 2.5 times)");
        }

        [Test]
        public void NothingMovesWhenTimeStandsStill([Values(0f, -1f)] float dt)
        {
            var s = new ShipSupplies();
            s.Set(0.5f, 0.5f);
            s.Tick(dt, 1f, true, true, 3);
            Assert.AreEqual(0.5f, s.Fuel);
            Assert.AreEqual(0.5f, s.Oxygen);
        }

        // ------------------------------------------------------------------ the readings

        static DashModel Settle(DashInputs input, float seconds = 3f)
        {
            var m = new DashModel();
            for (float t = 0f; t < seconds; t += 0.02f) m.Tick(0.02f, input);
            return m;
        }

        [Test]
        public void TheDot_IsUnitLength_AndGoesWhenYouAreStill()
        {
            DashModel m = Settle(new DashInputs { Speed = 50f, DriftX = 3f, DriftY = -4f, DriftZ = 12f, Altitude = -1f });
            DashReadings r = m.Readings;
            Assert.IsTrue(r.Moving);
            Assert.AreEqual(1f, Math.Sqrt(r.DriftX * r.DriftX + r.DriftY * r.DriftY + r.DriftZ * r.DriftZ), 1e-3);
            Assert.AreEqual(3f / 13f, r.DriftX, 0.01f);
            Assert.AreEqual(-4f / 13f, r.DriftY, 0.01f);

            // Turned right round in one step (a bounce, a hard brake through zero): the dot must follow.
            for (int i = 0; i < 100; i++) m.Tick(0.02f, new DashInputs { Speed = 50f, DriftX = -3f, DriftY = 4f, DriftZ = -12f, Altitude = -1f });
            Assert.AreEqual(-12f / 13f, m.Readings.DriftZ, 0.01f, "the dot stayed pointing the old way after the motion reversed");
            Assert.AreEqual(-3f / 13f, m.Readings.DriftX, 0.01f);

            m.Tick(0.02f, new DashInputs { Speed = 0.1f, DriftZ = 0.1f, Altitude = -1f });
            Assert.IsFalse(m.Readings.Moving, "a crawl should read as holding still");
        }

        [Test]
        public void NeedlesGlide_TheyDoNotJump()
        {
            var m = new DashModel();
            m.Tick(0.02f, new DashInputs { Fuel = 1f, Oxygen = 1f, Altitude = -1f });
            float before = m.Readings.Fuel;
            m.Tick(0.02f, new DashInputs { Fuel = 0f, Oxygen = 1f, Altitude = -1f });
            Assert.Less(before - m.Readings.Fuel, 0.3f, "the fuel needle jumped");
            for (int i = 0; i < 100; i++) m.Tick(0.02f, new DashInputs { Fuel = 0f, Oxygen = 1f, Altitude = -1f });
            Assert.AreEqual(0f, m.Readings.Fuel, 0.01f);
        }

        [TestCase(0f, "0", "m/s")]
        [TestCase(86.4f, "86", "m/s")]
        [TestCase(1240f, "1,240", "m/s")]
        [TestCase(9999f, "9,999", "m/s")]
        [TestCase(12480f, "12.5", "km/s")]
        public void SpeedIsWrittenPlainly(float speed, string words, string unit)
        {
            DashModel m = Settle(new DashInputs { Speed = speed, Altitude = -1f }, 0.5f);
            Assert.AreEqual(words, m.Readings.SpeedText);
            Assert.AreEqual(unit, m.Readings.SpeedUnit);
        }

        [TestCase(-1f, "")]
        [TestCase(0f, "0 m")]
        [TestCase(412f, "412 m")]
        [TestCase(1840f, "1.8 km")]
        [TestCase(250000f, "250 km")]
        public void HeightIsWrittenPlainly_AndBlankWithNoGround(float height, string words)
        {
            DashModel m = Settle(new DashInputs { Altitude = height }, 0.5f);
            Assert.AreEqual(words, m.Readings.AltitudeText);
        }

        [Test]
        public void TheWords_AreOnlyRewrittenWhenTheNumberChanges()
        {
            var m = new DashModel();
            var input = new DashInputs { Speed = 120.2f, Fuel = 0.5f, Oxygen = 0.5f, Altitude = 300.2f, Vertical = 4.1f };
            for (int i = 0; i < 50; i++) m.Tick(0.02f, input);
            string speed = m.Readings.SpeedText, height = m.Readings.AltitudeText, fuel = m.Readings.FuelText;
            input.Speed = 120.3f;
            input.Altitude = 300.3f;
            for (int i = 0; i < 50; i++) m.Tick(0.02f, input);
            Assert.AreSame(speed, m.Readings.SpeedText, "a new string was made for the same number");
            Assert.AreSame(height, m.Readings.AltitudeText);
            Assert.AreSame(fuel, m.Readings.FuelText);
        }

        [Test]
        public void OneNoticeAtATime_TheMostUrgentFirst()
        {
            Assert.AreEqual(string.Empty, Settle(new DashInputs { Altitude = -1f }, 0.5f).Readings.Notice);
            StringAssert.Contains("MATCHING", Settle(new DashInputs { Matching = true, Altitude = -1f }, 0.5f).Readings.Notice);
            StringAssert.Contains("FUEL LOW", Settle(new DashInputs { Matching = true, FuelLow = true, Altitude = -1f }, 0.5f).Readings.Notice);
            StringAssert.Contains("AIR LOW", Settle(new DashInputs { OxygenLow = true, Matching = true, Altitude = -1f }, 0.5f).Readings.Notice);
            StringAssert.Contains("FUEL LOW", Settle(new DashInputs { FuelLow = true, OxygenLow = true, Altitude = -1f }, 0.5f).Readings.Notice, "fuel can hurt you today; air cannot yet");
            StringAssert.Contains("AIR LOW", Settle(new DashInputs { FuelLow = true, OxygenLow = true, Altitude = -1f }, 0.5f).Readings.Notice, "and when both are low the notice says both");
            StringAssert.Contains("FUEL OUT", Settle(new DashInputs { FuelLow = true, FuelOut = true, OxygenLow = true, Altitude = -1f }, 0.5f).Readings.Notice);
        }

        // ------------------------------------------------------------------ the designs

        static DashInputs Busy => new DashInputs
        {
            Speed = 310f, DriftX = 0.5f, DriftY = -0.6f, DriftZ = -0.6f, ThrustX = -1f, ThrustY = 0.6f, ThrustZ = 0.4f, Boost = true,
            Fuel = 0.12f, Oxygen = 0.16f, FuelLow = true, OxygenLow = true, Altitude = 1840f, Vertical = -52f, CameraName = "LANDING", Against = "MOON", ScreenHint = "C: LOOK DOWN",
        };

        [Test]
        public void EveryDesign_StaysOnItsPanel_AndWithinBudget([Values] DashStyle style)
        {
            var d = new DashDrawing();
            foreach (DashInputs input in new[] { new DashInputs { Fuel = 1f, Oxygen = 1f, Altitude = -1f }, Busy })
            {
                DashModel m = Settle(input, 1f);
                DashDesigns.Paint(style, m.Readings, d);
                Assert.Greater(d.VertexCount, 300, "hardly anything was drawn");
                Assert.AreEqual(0, d.VertexCount % 3);
                Assert.LessOrEqual(d.VertexCount, 3600, "docs/performance_budget.md caps a dash at 1,200 triangles");
                Assert.LessOrEqual(d.Texts.Count, 24, "too many words to read at a glance");
                for (int i = 0; i < d.VertexCount; i++)
                {
                    Assert.IsFalse(float.IsNaN(d.X[i]) || float.IsNaN(d.Y[i]));
                    Assert.That(d.X[i], Is.InRange(-1f, DashDrawing.Width + 1f), "drawn off the side of the panel");
                    Assert.That(d.Y[i], Is.InRange(-1f, DashDrawing.Height + 1f), "drawn off the top or bottom of the panel");
                }
                foreach (DashText t in d.Texts)
                {
                    Assert.That(t.X, Is.InRange(0f, DashDrawing.Width));
                    Assert.That(t.Y, Is.InRange(8f, DashDrawing.Height - 8f));
                    Assert.GreaterOrEqual(t.Size, DashDrawing.MinTextSize, "\"" + t.Text + "\" is too small to read on a Steam Deck");
                }
                Assert.Greater(d.CameraWidth, 0f, "no window for the camera picture");
                Assert.AreEqual(1.6f, d.CameraWidth / d.CameraHeight, 0.01f, "the camera window should match the screen's shape");
            }
        }

        [Test]
        public void ABlinkingWarning_NeverShufflesTheOtherWords([Values] DashStyle style)
        {
            // The real dash matches words to screen objects by their place in the list. If a blink added or
            // removed a word, every word after it would be rebuilt twice a second for the last fifth of a tank.
            var lit = new DashDrawing();
            var dark = new DashDrawing();
            DashInputs input = Busy;
            input.Boost = true;
            input.OxygenLow = true;
            input.ThrustY = 1f;
            DashModel m = Settle(input, 1f);
            m.Readings.Clock = 0.2f;
            bool first = m.Readings.Blink;
            DashDesigns.Paint(style, m.Readings, lit);
            m.Readings.Clock = 0.7f;
            Assert.AreNotEqual(first, m.Readings.Blink, "the two moments should be either side of a blink");
            DashDesigns.Paint(style, m.Readings, dark);

            Assert.AreEqual(lit.Texts.Count, dark.Texts.Count);
            int changed = 0;
            for (int i = 0; i < lit.Texts.Count; i++)
            {
                Assert.AreEqual(lit.Texts[i].X, dark.Texts[i].X, 1e-3, "word " + i + " moved");
                Assert.AreEqual(lit.Texts[i].Y, dark.Texts[i].Y, 1e-3, "word " + i + " moved");
                if (lit.Texts[i].Text != dark.Texts[i].Text) changed++;
            }
            Assert.LessOrEqual(changed, 3, "only the blinking words themselves should change");
            Assert.AreNotEqual(lit.Fingerprint(), dark.Fingerprint(), "a blink is a different picture");
        }

        [Test]
        public void TheSamePicture_HasTheSameFingerprint([Values] DashStyle style)
        {
            var a = new DashDrawing();
            var b = new DashDrawing();
            DashModel m = Settle(new DashInputs { Fuel = 0.8f, Oxygen = 1f, Speed = 40f, DriftZ = 40f, Altitude = -1f }, 1f);
            DashDesigns.Paint(style, m.Readings, a);
            DashDesigns.Paint(style, m.Readings, b);
            Assert.AreEqual(a.Fingerprint(), b.Fingerprint());
            // (The Flight deck screen leaves fuel to the console's real tube, so the direction changes too.)
            m.Tick(0.5f, new DashInputs { Fuel = 0.4f, Oxygen = 1f, Speed = 40f, DriftX = 30f, DriftZ = 40f, Altitude = -1f });
            DashDesigns.Paint(style, m.Readings, b);
            Assert.AreNotEqual(a.Fingerprint(), b.Fingerprint(), "the fuel bar and the direction dot moved");
        }

        [Test]
        public void EveryDesign_ShowsTheSameFacts([Values] DashStyle style)
        {
            var d = new DashDrawing();
            DashModel m = Settle(Busy, 1f);
            m.Readings.Clock = 0.2f;
            DashDesigns.Paint(style, m.Readings, d);
            string all = string.Empty;
            foreach (DashText t in d.Texts) all += t.Text + "|";
            StringAssert.Contains("310", all, "speed");
            StringAssert.Contains("1.8 km", all, "height");
            StringAssert.Contains("-52", all, "sink rate");
            StringAssert.Contains("MOON", all, "what the speed is measured against");
            StringAssert.Contains("LANDING", all, "which camera");
            StringAssert.Contains("FUEL", all);
            // The Flight deck screen carries fewer words on purpose: its fuel and air are pictures (see
            // TheFlightDeckScreen_ShowsFuelAndAir_AsPicturesThatFollowTheTanks). Its notice still says FUEL LOW.
            if (style == DashStyle.FlightDeck) return;
            StringAssert.Contains("LOOK DOWN", all, "which button shows the camera");
            StringAssert.Contains("AIR", all);
            StringAssert.Contains("16", all, "air left");
        }

        [Test]
        public void TheFlightDeckScreen_AlwaysHasTheSameSevenWords_AndNoCheapOnes()
        {
            var d = new DashDrawing();
            foreach (DashInputs input in new[] { new DashInputs { Fuel = 1f, Oxygen = 1f, Altitude = -1f }, Busy, new DashInputs { Speed = 40f, DriftZ = -1f, Altitude = 12f, Vertical = -3f, Against = "EARTH" } })
            {
                DashModel m = Settle(input, 1f);
                DashDesigns.Paint(DashStyle.FlightDeck, m.Readings, d);
                Assert.AreEqual(7, d.Texts.Count, "a word was added or skipped: words must keep their place in the list");
                foreach (DashText t in d.Texts)
                {
                    foreach (string cheap in new[] { "PROPERTY", "LOOK DOWN", "PUSH", "HOLDING", "HEIGHT", "CLIMB", "SINK", "SPEED vs", "%" })
                        StringAssert.DoesNotContain(cheap, t.Text);
                }
            }
        }

        [Test]
        public void TheFlightDeckScreen_ShowsFuelAndAir_AsPicturesThatFollowTheTanks()
        {
            // The console's tube and dial sit out to the sides, past the edge of the view from the seat, and
            // are not there at all if the Console model is missing. So the screen itself must show both.
            var d = new DashDrawing();
            int Picture(float fuel, float air, bool fuelLow = false, bool airLow = false, float clock = 0.2f)
            {
                var r = new DashReadings { Fuel = fuel, Oxygen = air, FuelLow = fuelLow, OxygenLow = airLow, Clock = clock };
                DashDesigns.Paint(DashStyle.FlightDeck, r, d);
                return d.Fingerprint();
            }
            int full = Picture(1f, 1f);
            int corners = d.VertexCount;
            Assert.AreNotEqual(full, Picture(0.6f, 1f), "less fuel must change the picture");
            Assert.AreEqual(corners, d.VertexCount, "a gauge moving must never add or drop corners");
            Assert.AreNotEqual(full, Picture(1f, 0.6f), "less air must change the picture");
            Assert.AreEqual(corners, d.VertexCount);
            Assert.AreNotEqual(Picture(0.6f, 1f), Picture(0.58f, 1f), "the fuel tube is fine enough to show two parts in a hundred");
            Assert.AreNotEqual(Picture(1f, 0.6f), Picture(1f, 0.58f), "the air bubbles are fine enough to show two parts in a hundred");
            Assert.AreNotEqual(Picture(0.1f, 1f), Picture(0.1f, 1f, fuelLow: true), "low fuel must look different from the same level without the warning");
            Assert.AreNotEqual(Picture(1f, 0.1f), Picture(1f, 0.1f, airLow: true), "low air must look different too");
            Assert.AreNotEqual(Picture(0.1f, 1f, fuelLow: true, clock: 0.2f), Picture(0.1f, 1f, fuelLow: true, clock: 0.7f), "and it blinks");
            Picture(0f, 0f, true, true);
            Assert.AreEqual(corners, d.VertexCount, "empty tanks keep every corner too");
            Assert.AreEqual(7, d.Texts.Count, "and still no words for them");
        }

        // ------------------------------------------------------------------ the console's real instruments

        [Test]
        public void ConsoleGauges_FollowTheReadings()
        {
            var g = new ConsoleGauges();
            var r = new DashReadings { Fuel = 0.4f, Oxygen = 0.25f };
            g.Set(r, locked: false, autopilot: false);
            Assert.AreEqual(0.4f, g.FuelLevel, 1e-5f);
            Assert.AreEqual(0.25f, g.AirTurn, 1e-5f);
            for (int i = 0; i < ConsoleGauges.LampCount; i++) Assert.IsFalse(g.Lamps[i], "nothing to report, so no lamp is lit");

            r.Fuel = 0f; r.Oxygen = 2f;
            g.Set(r, false, false);
            Assert.AreEqual(ConsoleGauges.FuelSliver, g.FuelLevel, 1e-6f, "a dry tank still shows a sliver, so the tube never vanishes");
            Assert.AreEqual(1f, g.AirTurn, 1e-6f);
        }

        [Test]
        public void ConsoleLamps_SayWhatTheShipIsDoing()
        {
            var g = new ConsoleGauges();
            var r = new DashReadings { Matching = true };
            g.Set(r, locked: true, autopilot: true);
            Assert.IsTrue(g.Lit(ConsoleLamp.Lock));
            Assert.IsTrue(g.Lit(ConsoleLamp.Auto));
            Assert.IsTrue(g.Lit(ConsoleLamp.Match));
            Assert.IsFalse(g.Lit(ConsoleLamp.Fuel));
            Assert.IsFalse(g.Lit(ConsoleLamp.Air));

            // Low blinks in step with the screen's notice; out burns steadily, so it is never caught dark.
            r = new DashReadings { FuelLow = true, OxygenLow = true, Clock = 0.2f };
            g.Set(r, false, false);
            Assert.IsTrue(g.Lit(ConsoleLamp.Fuel));
            Assert.IsTrue(g.Lit(ConsoleLamp.Air));
            r.Clock = 0.7f;
            g.Set(r, false, false);
            Assert.IsFalse(g.Lit(ConsoleLamp.Fuel));
            Assert.IsFalse(g.Lit(ConsoleLamp.Air));
            r.FuelOut = true;
            g.Set(r, false, false);
            Assert.IsTrue(g.Lit(ConsoleLamp.Fuel), "an empty tank's lamp stays on");
            Assert.IsFalse(g.Lit(ConsoleLamp.Air), "air that is only low still blinks");
            r.Oxygen = 0f;
            g.Set(r, false, false);
            Assert.IsTrue(g.Lit(ConsoleLamp.Air), "with no air left the lamp stays on too");

            // The same from raw numbers, for the instruments seen through the glass with nobody aboard.
            g.Set(0.5f, 0.5f, false, false, false, true, true, false, false);
            Assert.AreEqual(0.5f, g.FuelLevel, 1e-6f);
            Assert.IsTrue(g.Lit(ConsoleLamp.Lock));
            Assert.IsFalse(g.Lit(ConsoleLamp.Fuel));
            Assert.IsFalse(g.Lit(ConsoleLamp.Air));
            Assert.AreEqual("lampFuelGlow", ConsoleGauges.PieceOf(ConsoleLamp.Fuel));
        }

        [Test]
        public void PaintingAgain_ReusesTheSameArrays([Values] DashStyle style)
        {
            var d = new DashDrawing();
            DashModel m = Settle(Busy, 1f);
            DashDesigns.Paint(style, m.Readings, d);
            float[] x = d.X;
            int count = d.VertexCount;
            DashDesigns.Paint(style, m.Readings, d);
            Assert.AreSame(x, d.X, "the drawing's arrays were thrown away and remade");
            Assert.AreEqual(count, d.VertexCount);
        }

        [Test]
        public void MorePush_DrawsMoreFlame()
        {
            var d = new DashDrawing();
            DashModel idle = Settle(new DashInputs { Fuel = 1f, Oxygen = 1f, Altitude = -1f }, 1f);
            DashModel pushing = Settle(new DashInputs { Fuel = 1f, Oxygen = 1f, Altitude = -1f, ThrustY = 1f, ThrustX = 1f, ThrustZ = -1f }, 1f);
            DashDesigns.Paint(DashStyle.FlightDeck, idle.Readings, d);
            int quiet = d.VertexCount;
            DashDesigns.Paint(DashStyle.FlightDeck, pushing.Readings, d);
            Assert.AreEqual(quiet + 3 * 6, d.VertexCount, "three jets of two triangles each");
        }

        // ------------------------------------------------------------------ cameras and looking down

        [Test]
        public void ThreeCameras_AllOutsideTheCabin_AndLookingAtTheRightThing()
        {
            Assert.AreEqual(3, CameraRig.Count);
            int landing = 0;
            for (int i = 0; i < CameraRig.Count; i++)
            {
                CameraSpot spot = CameraRig.Spot(i);
                Assert.IsFalse(string.IsNullOrEmpty(spot.Name));
                Assert.IsFalse(ShipLayout.InsideCabin(spot.Lens), spot.Name + " is inside the cabin");
                P3 look = spot.LookAt - spot.Lens;
                Assert.Greater(look.Length, 1f);
                if (spot.Landing)
                {
                    landing++;
                    Assert.Less(look.Y / look.Length, -0.99f, "the landing camera must look straight down");
                    Assert.Greater(spot.Lens.Y, ShipLayout.GroundY + 1f, "the landing camera would be crushed on landing");
                }
                else
                {
                    Assert.Greater(Math.Abs(spot.Lens.X), 3f, "a pole camera should stand well clear of the hull");
                    Assert.Greater(look.Z, 0f, "a pole camera looks back at the ship from behind");
                    Assert.Less(spot.LookAt.Y, 0f, "a pole camera aims below the cabin floor, so the long legs are in the picture");
                    Assert.Greater(spot.LookAt.Y, ShipLayout.BellyY, "but not so low that it loses the cabin");
                }
            }
            Assert.AreEqual(1, landing);
            Assert.AreEqual(0, CameraRig.Next(CameraRig.Count - 1));
        }

        [Test]
        public void LookingDown_TakesAMoment_ThenTheCameraTakesOver_AndTheSameButtonUndoesIt()
        {
            var v = new ScreenView();
            v.Tick(0.02f, true, false, true, 0.28f);
            Assert.AreEqual(ScreenState.Lowering, v.State);
            Assert.IsFalse(v.Watching);
            for (int i = 0; i < 20; i++) v.Tick(0.02f, false, false, true, 0.28f);
            Assert.IsTrue(v.Watching);
            Assert.AreEqual(1f, v.Glance);

            v.Tick(0.02f, false, true, true, 0.28f);
            Assert.AreEqual(1, v.Camera);
            Assert.IsTrue(v.Watching, "changing camera should not look away");

            v.Tick(0.02f, true, false, true, 0.28f);
            Assert.IsFalse(v.Watching, "the view goes back to your own eyes the moment you start to look up");
            for (int i = 0; i < 20; i++) v.Tick(0.02f, false, false, true, 0.28f);
            Assert.AreEqual(ScreenState.Up, v.State);
            Assert.AreEqual(0f, v.Glance);
        }

        [Test]
        public void LeavingTheSeat_PutsYourHeadUpAtOnce()
        {
            var v = new ScreenView();
            v.Tick(0.02f, true, false, true, 0.28f);
            for (int i = 0; i < 20; i++) v.Tick(0.02f, false, false, true, 0.28f);
            Assert.IsTrue(v.Watching);
            v.Tick(0.02f, false, false, false, 0.28f);
            Assert.AreEqual(ScreenState.Up, v.State);
            Assert.AreEqual(0f, v.Glance);
        }
    }
}
