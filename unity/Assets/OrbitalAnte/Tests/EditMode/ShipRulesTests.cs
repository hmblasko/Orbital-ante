using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>The rules of the ship that need no engine: walking about the cabin, damage and mending, the hatch and lift, the seat belt, and the baked light.</summary>
    public sealed class ShipRulesTests
    {
        // ------------------------------------------------------------------ walking about the cabin

        [Test]
        public void Walker_NeverLeavesTheFloorPlan_WhateverYouPush()
        {
            var rng = new Random(7);
            var walker = new InteriorWalker();
            walker.Place(-0.78f, 1.0f);
            float wx = 0f, wz = 0f;
            for (int step = 0; step < 20000; step++)
            {
                if (step % 40 == 0) { wx = (float)rng.NextDouble() * 2f - 1f; wz = (float)rng.NextDouble() * 2f - 1f; }
                walker.Step(1f / 60f, wx, wz, rng.Next(50) == 0, false);
                Assert.IsTrue(InteriorWalker.CanStand(walker.X, walker.Z), "left the floor plan at step " + step + ": " + walker.X + ", " + walker.Z);
                Assert.GreaterOrEqual(walker.Y, 0f);
                float d = (float)Math.Sqrt(walker.X * walker.X + walker.Z * walker.Z);
                Assert.LessOrEqual(walker.Y + ShipLayout.PersonHeight, ShipLayout.CeilingAt(d + 0.2f) + 0.02f, "head through the ceiling");
            }
        }

        [Test]
        public void Walker_SlidesAlongTheWall_InsteadOfSticking()
        {
            var walker = new InteriorWalker();
            walker.Place(-1.9f, 0f);
            float startZ = walker.Z;
            // Pushing into the left wall and a little forward: you should still make ground forward.
            for (int i = 0; i < 60; i++) walker.Step(1f / 60f, -1f, 0.5f, false, false);
            Assert.Greater(walker.Z - startZ, 0.5f);
        }

        [Test]
        public void Walker_JumpReachesTheStatedHeight()
        {
            var walker = new InteriorWalker { JumpHeight = 0.45f };
            walker.Place(-1f, 0f);
            walker.Step(1f / 240f, 0f, 0f, true, false);
            float top = 0f;
            for (int i = 0; i < 480 && !walker.Grounded; i++) { walker.Step(1f / 240f, 0f, 0f, false, false); top = Math.Max(top, walker.Y); }
            Assert.AreEqual(0.45f, top, 0.04f);
            Assert.IsTrue(walker.Grounded);
        }

        [Test]
        public void Walker_TheWellOnlyTakesYou_WhenTheHatchIsOpen()
        {
            var walker = new InteriorWalker();
            walker.Place(0f, 0f);
            walker.Step(1f / 60f, 0f, 0f, false, false);
            Assert.IsFalse(walker.OverOpenWell, "the closed hatch is floor");
            walker.Step(1f / 60f, 0f, 0f, false, true);
            Assert.IsTrue(walker.OverOpenWell);
            walker.Place(-1.2f, 0f);
            walker.Step(1f / 60f, 0f, 0f, false, true);
            Assert.IsFalse(walker.OverOpenWell, "standing beside the well is safe");
        }

        [Test]
        public void SeatAndLever_CanBeReachedOnFoot()
        {
            // Where you stand up from the seat, and a spot within arm's reach of the lever, are both standable.
            Assert.IsTrue(InteriorWalker.CanStand(-0.78f, 1.0f));
            bool found = false;
            for (float x = -2f; x <= 2f && !found; x += 0.05f)
            {
                for (float z = -2f; z <= 2f && !found; z += 0.05f)
                {
                    float dx = x - ShipLayout.HatchLever.X, dz = z - ShipLayout.HatchLever.Z;
                    if (InteriorWalker.CanStand(x, z) && dx * dx + dz * dz < 0.9f * 0.9f) found = true;
                }
            }
            Assert.IsTrue(found, "nowhere to stand near the hatch lever");
        }

        // ------------------------------------------------------------------ damage and mending

        [Test]
        public void Model_HasEveryPartTheShipIsBuiltFrom()
        {
            var model = new ShipModel();
            Assert.GreaterOrEqual(model.IndexOf("cabin"), 0);
            Assert.GreaterOrEqual(model.IndexOf("base"), 0);
            Assert.GreaterOrEqual(model.Find(PartKind.Engine), 0);
            Assert.GreaterOrEqual(model.Find(PartKind.Dish), 0);
            Assert.GreaterOrEqual(model.Find(PartKind.Hatch), 0);
            for (int i = 0; i < 4; i++)
            {
                Assert.GreaterOrEqual(model.Find(PartKind.Leg, i), 0);
                Assert.GreaterOrEqual(model.Find(PartKind.Thruster, i), 0);
            }
            for (int i = 0; i < ShipLayout.Windows.Length; i++) Assert.GreaterOrEqual(model.Find(PartKind.Window, i), 0);
            var ids = new HashSet<string>();
            foreach (PartSpec p in model.Parts) Assert.IsTrue(ids.Add(p.Id), "two parts called " + p.Id);
        }

        [Test]
        public void Damage_GoesSoundDamagedBroken_AndMendingMakesGoodAsNew()
        {
            var model = new ShipModel();
            int dish = model.Find(PartKind.Dish);
            float max = model.Parts[dish].MaxHealth;
            Assert.AreEqual(PartState.Sound, model.State(dish));
            model.Damage(dish, max * 0.5f);
            Assert.AreEqual(PartState.Damaged, model.State(dish));
            Assert.IsTrue(model.DishWorks, "a damaged dish still works");
            DamageResult broke = model.Damage(dish, max * 0.5f);
            Assert.IsTrue(broke.Broke);
            Assert.IsFalse(model.DishWorks);

            float needed = model.RepairSeconds(dish);
            Assert.LessOrEqual(needed, model.Rules.repairSecondsMost + 1e-4f, "repairs must be quick");
            bool done = false;
            float spent = 0f;
            while (!done && spent < 10f) { done = model.Repair(dish, 0.05f); spent += 0.05f; }
            Assert.IsTrue(done);
            Assert.AreEqual(needed, spent, 0.11f);
            Assert.AreEqual(PartState.Sound, model.State(dish));
            Assert.IsTrue(model.DishWorks);
        }

        [Test]
        public void HeavyDamage_TearsPartsOffForGood()
        {
            var model = new ShipModel();
            int leg = model.Find(PartKind.Leg, 2);
            float max = model.Parts[leg].MaxHealth;
            Assert.IsTrue(model.Parts[leg].CanFallOff);

            // Broken is not lost: it can still be mended.
            model.Damage(leg, max);
            Assert.AreEqual(PartState.Broken, model.State(leg));
            Assert.IsTrue(model.NeedsRepair(leg));
            Assert.AreEqual(4, model.LegsStanding);

            // Keep battering a broken part and it comes away.
            DamageResult last = default;
            for (int i = 0; i < 20 && model.State(leg) != PartState.Lost; i++) last = model.Damage(leg, max * 0.25f);
            Assert.AreEqual(PartState.Lost, model.State(leg));
            Assert.IsTrue(last.FellOff);
            Assert.IsFalse(model.NeedsRepair(leg), "a lost part cannot be mended");
            Assert.IsFalse(model.Repair(leg, 100f));
            Assert.AreEqual(3, model.LegsStanding);

            // One enormous blow does it at once.
            int thruster = model.Find(PartKind.Thruster, 0);
            DamageResult blow = model.Damage(thruster, model.Parts[thruster].MaxHealth * model.Rules.tearOffBlow);
            Assert.IsTrue(blow.FellOff);
        }

        [Test]
        public void TheCabinAndWindows_NeverFallOff()
        {
            var model = new ShipModel();
            for (int i = 0; i < model.Count; i++)
            {
                PartSpec p = model.Parts[i];
                if (p.Kind != PartKind.Window && p.Id != "cabin" && p.Id != "base" && p.Kind != PartKind.Hatch && p.Kind != PartKind.Dish) continue;
                model.Damage(i, p.MaxHealth * 50f);
                Assert.AreEqual(PartState.Broken, model.State(i), p.Id);
                Assert.IsTrue(model.NeedsRepair(i), p.Id + " must stay mendable");
            }
            Assert.IsFalse(model.CabinIntact);
        }

        [Test]
        public void ThereIsRoomBetweenBrokenAndLost()
        {
            // "Repairable until heavy damage": a landing that breaks a leg must be well short of one that tears it off.
            var model = new ShipModel();
            float legHealth = model.Parts[model.Find(PartKind.Leg, 0)].MaxHealth;
            float breaks = 0f, tears = 0f;
            for (float v = 0f; v < 40f; v += 0.05f)
            {
                float d = model.ImpactDamage(v);
                if (breaks == 0f && d >= legHealth) breaks = v;
                if (tears == 0f && d >= legHealth * model.Rules.tearOffBlow) { tears = v; break; }
            }
            Assert.Greater(breaks, model.Rules.safeImpactSpeed + 4f, "a leg breaks too easily");
            Assert.Greater(tears - breaks, 3f, "broken at " + breaks + " m/s, torn off at " + tears + " m/s");
        }

        [Test]
        public void ABrokenPartShowsHowCloseItIsToComingOff()
        {
            var model = new ShipModel();
            int leg = model.Find(PartKind.Leg, 0);
            float max = model.Parts[leg].MaxHealth;
            model.Damage(leg, max);
            Assert.AreEqual(0f, model.TearProgress(leg), 1e-4f);
            model.Damage(leg, max * 0.6f);
            Assert.Greater(model.TearProgress(leg), 0.5f);
            Assert.AreEqual(PartState.Broken, model.State(leg));
            while (!model.Repair(leg, 0.1f)) { }
            Assert.AreEqual(0f, model.TearProgress(leg), "mending puts it firmly back on");
            Assert.AreEqual(0f, model.TearProgress(model.Find(PartKind.Window, 0)));
        }

        [Test]
        public void Glass_IsStrongButNotUnbreakable_AndCracksInStages()
        {
            var model = new ShipModel();
            int window = model.Find(PartKind.Window, 0);
            float max = model.Parts[window].MaxHealth;
            Assert.AreEqual(0, model.CrackStage(window));
            // A rifle round does about 30: one must not break it, and it must not take a silly number.
            float bullet = 30f;
            int shots = 0, lastStage = 0;
            while (model.State(window) != PartState.Broken && shots < 100)
            {
                model.Damage(window, bullet);
                shots++;
                int stage = model.CrackStage(window);
                Assert.GreaterOrEqual(stage, lastStage, "cracks only get worse");
                lastStage = stage;
            }
            Assert.GreaterOrEqual(shots, 4);
            Assert.LessOrEqual(shots, 14);
            Assert.AreEqual(4, model.CrackStage(window));
            Assert.IsFalse(model.CabinIntact);
            while (!model.Repair(window, 0.1f)) { }
            Assert.AreEqual(0, model.CrackStage(window));
            Assert.IsTrue(model.CabinIntact);
            Assert.AreEqual(max, model.Health(window), 1e-3f);
        }

        [Test]
        public void GentleLandingsAreFree_HardOnesHurtMoreThanInProportion()
        {
            var model = new ShipModel();
            Assert.AreEqual(0f, model.ImpactDamage(model.Rules.safeImpactSpeed));
            Assert.AreEqual(0f, model.ImpactDamage(1f));
            float a = model.ImpactDamage(model.Rules.safeImpactSpeed + 4f);
            float b = model.ImpactDamage(model.Rules.safeImpactSpeed + 8f);
            Assert.Greater(a, 0f);
            Assert.Greater(b, a * 3f);
        }

        [Test]
        public void DamageSlowsTheShip_ButNeverStrandsIt()
        {
            var model = new ShipModel();
            Assert.AreEqual(1f, model.ThrustFactor);
            Assert.AreEqual(1f, model.TurnFactor);
            int engine = model.Find(PartKind.Engine);
            model.Damage(engine, model.Parts[engine].MaxHealth * 100f);
            for (int i = 0; i < 4; i++) model.Damage(model.Find(PartKind.Thruster, i), 1e6f);
            Assert.Less(model.ThrustFactor, 0.5f);
            Assert.GreaterOrEqual(model.ThrustFactor, 0.3f);
            Assert.Less(model.TurnFactor, 0.5f);
            Assert.GreaterOrEqual(model.TurnFactor, 0.2f);
        }

        [Test]
        public void MendingLeftAlone_SlipsBack()
        {
            var model = new ShipModel();
            int dish = model.Find(PartKind.Dish);
            model.Damage(dish, 40f);
            model.Repair(dish, 0.4f);
            float before = model.RepairProgress(dish);
            Assert.Greater(before, 0f);
            model.RestRepair(dish, 0.4f);
            Assert.Less(model.RepairProgress(dish), before);
            model.RestRepair(dish, 10f);
            Assert.AreEqual(0f, model.RepairProgress(dish));
        }

        // ------------------------------------------------------------------ hatch and lift

        static HatchLift OpenHatch()
        {
            var hatch = new HatchLift();
            Assert.IsTrue(hatch.Set(true, true));
            for (int i = 0; i < 120; i++) hatch.Tick(1f / 60f);
            Assert.IsTrue(hatch.Open);
            return hatch;
        }

        [Test]
        public void Hatch_OpensAndShutsInItsStatedTime_AndABrokenOneWillNotMove()
        {
            var hatch = new HatchLift { DoorSeconds = 0.5f };
            Assert.IsTrue(hatch.Shut);
            Assert.IsFalse(hatch.Set(true, hatchWorks: false));
            Assert.IsFalse(hatch.WantOpen);
            hatch.Set(true, true);
            float t = 0f;
            while (!hatch.Open && t < 5f) { hatch.Tick(0.01f); t += 0.01f; }
            Assert.AreEqual(0.5f, t, 0.06f);
            hatch.Toggle(true);
            for (int i = 0; i < 100; i++) hatch.Tick(0.01f);
            Assert.IsTrue(hatch.Shut);
        }

        [Test]
        public void Lift_CarriesYouUp_AndTheHatchClosesBehindYou()
        {
            HatchLift hatch = OpenHatch();
            var feet = new P3(0.3f, ShipLayout.GroundY, 0.2f);
            Assert.IsTrue(hatch.CanPickUp(feet));
            hatch.BeginUp(feet);
            LiftRide finished = LiftRide.None;
            float lastY = feet.Y;
            for (int i = 0; i < 2000 && finished == LiftRide.None; i++)
            {
                finished = hatch.Tick(1f / 60f);
                Assert.GreaterOrEqual(hatch.Rider.Y, lastY - 1e-5f, "the lift never drops you on the way up");
                lastY = hatch.Rider.Y;
                // Inside the shaft you are clear of its wall.
                if (hatch.Rider.Y > ShipLayout.BellyY - ShipLayout.PersonHeight && hatch.Rider.Y < 0f)
                {
                    float off = (float)Math.Sqrt(hatch.Rider.X * hatch.Rider.X + hatch.Rider.Z * hatch.Rider.Z);
                    Assert.Less(off + ShipLayout.PersonRadius, ShipLayout.ShaftRadius, "shoulders in the shaft wall");
                }
            }
            Assert.AreEqual(LiftRide.Up, finished);
            Assert.AreEqual(0f, hatch.Rider.Y, 1e-4f);
            Assert.IsFalse(hatch.WantOpen, "the hatch closes behind you");
        }

        [Test]
        public void Lift_SetsYouDown_AndWaitsForYouToStepAway()
        {
            HatchLift hatch = OpenHatch();
            hatch.BeginDown(new P3(0.2f, 0f, 0.1f), ShipLayout.GroundY);
            Assert.IsFalse(hatch.Set(false, true), "the doors will not close on someone in the shaft");
            LiftRide finished = LiftRide.None;
            for (int i = 0; i < 2000 && finished == LiftRide.None; i++) finished = hatch.Tick(1f / 60f);
            Assert.AreEqual(LiftRide.Down, finished);
            Assert.AreEqual(ShipLayout.GroundY, hatch.Rider.Y, 1e-3f);
            Assert.IsTrue(hatch.WantOpen, "the hatch stays open for you to come back");

            Assert.IsFalse(hatch.CanPickUp(hatch.Rider), "it must not snatch you straight back up");
            Assert.IsFalse(hatch.CanPickUp(new P3(3f, ShipLayout.GroundY, 0f)));
            Assert.IsTrue(hatch.CanPickUp(new P3(0f, ShipLayout.GroundY, 0f)), "walk back under the ring and it takes you");
        }

        [Test]
        public void Lift_OnlyReachesSoFar_AndNeedsOpenDoors()
        {
            HatchLift hatch = OpenHatch();
            Assert.IsFalse(hatch.CanPickUp(new P3(0f, ShipLayout.BellyY - hatch.Reach - 1f, 0f)), "too far below");
            Assert.IsFalse(hatch.CanPickUp(new P3(ShipLayout.ShaftRadius + 0.5f, ShipLayout.GroundY, 0f)), "not under the ring");
            var shut = new HatchLift();
            Assert.IsFalse(shut.CanPickUp(new P3(0f, ShipLayout.GroundY, 0f)));
        }

        // ------------------------------------------------------------------ seat belt

        [Test]
        public void Belt_BucklesInItsStatedTime_ClicksOnce_AndReleases()
        {
            var belt = new SeatBelt { BuckleSeconds = 1.1f, UnbuckleSeconds = 0.6f };
            Assert.IsFalse(belt.Release(), "nothing to release yet");
            Assert.IsTrue(belt.Begin());
            Assert.IsFalse(belt.Begin());
            int clicks = 0;
            float t = 0f;
            while (!belt.Buckled && t < 5f) { belt.Tick(0.01f); t += 0.01f; if (belt.Clicked) clicks++; }
            Assert.AreEqual(1.1f, t, 0.03f);
            belt.Tick(0.01f);
            Assert.AreEqual(1, clicks);
            Assert.IsFalse(belt.Clicked);
            Assert.AreEqual(1f, belt.Sit, 1e-4f);
            Assert.AreEqual(1f, belt.Draw, 1e-4f);

            Assert.IsTrue(belt.Release());
            t = 0f;
            while (belt.Seated && t < 5f) { belt.Tick(0.01f); t += 0.01f; }
            Assert.AreEqual(0.6f, t, 0.03f);
            Assert.AreEqual(BeltState.Free, belt.State);
        }

        [Test]
        public void Belt_YouSitFirst_ThenTheStrapComesAcross()
        {
            var belt = new SeatBelt();
            belt.Begin();
            float lastSit = 0f, lastDraw = 0f;
            for (int i = 0; i < 200 && !belt.Buckled; i++)
            {
                belt.Tick(0.01f);
                Assert.GreaterOrEqual(belt.Sit, lastSit - 1e-5f);
                Assert.GreaterOrEqual(belt.Draw, lastDraw - 1e-5f);
                Assert.GreaterOrEqual(belt.Sit, belt.Draw - 1e-5f);
                lastSit = belt.Sit; lastDraw = belt.Draw;
            }
        }

        // ------------------------------------------------------------------ the shell

        static double Volume(ShipMesh m)
        {
            double v = 0;
            for (int t = 0; t < m.Triangles.Length; t += 3)
            {
                P3 a = m.Position(m.Triangles[t]), b = m.Position(m.Triangles[t + 1]), c = m.Position(m.Triangles[t + 2]);
                v += (a.X * ((double)b.Y * c.Z - (double)b.Z * c.Y) - a.Y * ((double)b.X * c.Z - (double)b.Z * c.X) + a.Z * ((double)b.X * c.Y - (double)b.Y * c.X)) / 6.0;
            }
            return v;
        }

        [Test]
        public void Meshes_AreWellFormed()
        {
            var meshes = new List<ShipMesh> { ShipShell.Interior(), ShipShell.Hull(), ShipShell.CabinCollision(), ShipShell.BaseCollision() };
            foreach (WindowSpec w in ShipLayout.Windows) meshes.Add(ShipShell.Glass(w));
            foreach (ShipMesh m in meshes)
            {
                Assert.Greater(m.Triangles.Length, 0);
                Assert.AreEqual(0, m.Triangles.Length % 3);
                Assert.AreEqual(m.Positions.Length, m.Colors.Length);
                Assert.AreEqual(m.VertexCount, m.Glow.Length);
                foreach (int i in m.Triangles) Assert.IsTrue(i >= 0 && i < m.VertexCount);
                foreach (float v in m.Positions) Assert.IsFalse(float.IsNaN(v));
            }
        }

        [Test]
        public void MeshBuilder_SolidsFaceOutward()
        {
            var lathe = new MeshBuilder();
            lathe.Lathe(P3.Zero, new[] { 1f, 1f }, new[] { 0f, 2f }, 24, capBottom: true, capTop: true);
            Assert.AreEqual(Math.PI * 2.0, Volume(lathe.Finish()), 0.12, "a capped lathe is a solid seen from outside");
            var ball = new MeshBuilder();
            ball.Ball(P3.Zero, new P3(1f, 1f, 1f), 24, 16);
            Assert.AreEqual(4.0 / 3.0 * Math.PI, Volume(ball.Finish()), 0.2);
            var box = new MeshBuilder();
            box.Box(P3.Zero, new P3(1f, 2f, 3f));
            Assert.Greater(Volume(box.Finish()), 0.0);
        }

        [Test]
        public void CollisionShapes_AreSolidsSeenFromOutside()
        {
            Assert.Greater(Volume(ShipShell.CabinCollision()), 20.0);
            Assert.Greater(Volume(ShipShell.BaseCollision()), 5.0);
        }

        [Test]
        public void InteriorWall_FacesIntoTheRoom_AndLeavesTheWindowsOpen()
        {
            ShipMesh m = ShipShell.Interior();
            // Seen from the middle of the room, nearly all of the wall's area faces you.
            var eye = new P3(0f, 1.2f, 0f);
            double toward = 0, away = 0;
            for (int t = 0; t < m.Triangles.Length; t += 3)
            {
                P3 a = m.Position(m.Triangles[t]), b = m.Position(m.Triangles[t + 1]), c = m.Position(m.Triangles[t + 2]);
                if (a.Y < 0.02f && b.Y < 0.02f && c.Y < 0.02f) continue;   // the shaft below the floor is seen from inside itself
                P3 n = P3.Cross(b - a, c - a);
                double facing = P3.Dot(n, eye - (a + b + c) * (1f / 3f));
                double area = n.Length * 0.5;
                if (facing > 0) toward += area; else away += area;
            }
            Assert.Greater(toward, away * 6.0, "toward " + toward + " away " + away);

            // No wall where a window is: a ray from the room through the middle of each pane meets nothing.
            var bake = new LightBake();
            bake.Add(m);
            bake.PrepareForRays();
            foreach (WindowSpec w in ShipLayout.Windows)
            {
                ShipLayout.WindowCentre(w, out P3 centre, out P3 _);
                P3 dir = (centre - eye).Unit();
                int hit = bake.Trace(eye, dir, (centre - eye).Length + 0.5f, out float _);
                Assert.Less(hit, 0, w.Id + " is walled over");
            }
            // And there is wall between the windows: straight back, behind the lockers.
            Assert.GreaterOrEqual(bake.Trace(eye, new P3(0f, 0f, -1f), 10f, out float __), 0);
        }

        [Test]
        public void Windows_AreWhereThePilotCanSeeThroughThem()
        {
            // The two front windows are ahead of the seat and within a comfortable glance.
            int ahead = 0;
            foreach (WindowSpec w in ShipLayout.Windows)
            {
                ShipLayout.WindowCentre(w, out P3 centre, out P3 _);
                P3 to = (centre - ShipLayout.SeatedEye).Unit();
                if (to.Z > 0.6f) ahead++;
            }
            Assert.GreaterOrEqual(ahead, 2);
            Assert.AreEqual(5, ShipLayout.Windows.Length);
        }

        // ------------------------------------------------------------------ baked light

        static LightBake.Settings Quick() => new LightBake.Settings { Paths = 6, LampChecks = 4, Bounces = 2, Smoothing = 1 };

        [Test]
        public void Bake_IsTheSameEveryTime_AndLightsEveryPiece()
        {
            List<ShipPiece> pieces = ShipAssembly.InteriorPieces();
            List<float[]> a = ShipAssembly.Bake(pieces, Quick());
            List<float[]> b = ShipAssembly.Bake(ShipAssembly.InteriorPieces(), Quick());
            Assert.AreEqual(pieces.Count, a.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(pieces[i].Mesh.Positions.Length, a[i].Length, pieces[i].Name);
                CollectionAssert.AreEqual(a[i], b[i], pieces[i].Name + " baked differently the second time");
                double sum = 0;
                foreach (float v in a[i]) { Assert.IsFalse(float.IsNaN(v)); Assert.GreaterOrEqual(v, 0f); sum += v; }
                Assert.Greater(sum / a[i].Length, 0.01, pieces[i].Name + " is in the dark");
            }
        }

        [Test]
        public void Bake_SurvivesBeingPacked_AndIsRefusedForADifferentShip()
        {
            List<ShipPiece> pieces = ShipAssembly.InteriorPieces();
            List<float[]> flat = ShipAssembly.FlatLight(pieces);
            byte[] bytes = ShipAssembly.Pack(pieces, flat);
            List<float[]> back = ShipAssembly.Unpack(bytes, pieces);
            Assert.IsNotNull(back);
            Assert.AreEqual(flat.Count, back.Count);
            for (int i = 0; i < flat.Count; i++)
            {
                Assert.AreEqual(flat[i].Length, back[i].Length);
                for (int k = 0; k < flat[i].Length; k += 37) Assert.AreEqual(flat[i][k], back[i][k], 0.01f);
            }
            Assert.AreEqual(ShipAssembly.Fingerprint(pieces), ShipAssembly.Fingerprint(ShipAssembly.InteriorPieces()));

            // Change the ship and the old bake no longer fits.
            pieces[1].Mesh.Positions[0] += 0.01f;
            Assert.IsNull(ShipAssembly.Unpack(bytes, pieces));
            Assert.IsNull(ShipAssembly.Unpack(new byte[] { 1, 2, 3 }, ShipAssembly.InteriorPieces()));
            Assert.IsNull(ShipAssembly.Unpack(null, ShipAssembly.InteriorPieces()));
        }

        [Test]
        public void Bake_IsRefusedWhenOnlyAPieceHasMoved()
        {
            List<ShipPiece> pieces = ShipAssembly.InteriorPieces();
            byte[] bytes = ShipAssembly.Pack(pieces, ShipAssembly.FlatLight(pieces));
            Assert.IsNotNull(ShipAssembly.Unpack(bytes, pieces));
            int moved = pieces.FindIndex(p => p.Offset.X != 0f || p.Offset.Y != 0f || p.Offset.Z != 0f);
            if (moved < 0) moved = 0;
            P3 o = pieces[moved].Offset;
            pieces[moved].Offset = new P3(o.X, o.Y + 0.05f, o.Z);
            Assert.IsNull(ShipAssembly.Unpack(bytes, pieces), "a bake made with " + pieces[moved].Name + " elsewhere must not be used");
        }

        [Test]
        public void ABrokenHatch_StopsWhereItIs_EvenHalfwayOpen()
        {
            var hatch = new HatchLift { DoorSeconds = 0.5f };
            hatch.Set(true, true);
            for (int i = 0; i < 25; i++) hatch.Tick(0.01f);
            float half = hatch.Openness;
            Assert.That(half, Is.InRange(0.3f, 0.7f));
            for (int i = 0; i < 200; i++) hatch.Tick(0.01f, hatchWorks: false);
            Assert.AreEqual(half, hatch.Openness, 1e-5f, "a broken hatch kept opening");
            // Mended, it carries on to where it was going.
            for (int i = 0; i < 100; i++) hatch.Tick(0.01f);
            Assert.IsTrue(hatch.Open);
        }

        [Test]
        public void ABrokenHatch_StopsWhereItIs_EvenHalfwayShut()
        {
            HatchLift hatch = OpenHatch();
            hatch.Set(false, true);
            for (int i = 0; i < 2; i++) hatch.Tick(0.125f);
            float half = hatch.Openness;
            Assert.That(half, Is.InRange(0.3f, 0.7f));
            for (int i = 0; i < 200; i++) hatch.Tick(0.01f, hatchWorks: false);
            Assert.AreEqual(half, hatch.Openness, 1e-5f, "a broken hatch kept closing");
            Assert.IsFalse(hatch.Set(true, hatchWorks: false));
        }

        [Test]
        public void AShotInsideTheCabin_MeetsTheWall_TheGlass_TheFloor_OrTheWell()
        {
            var back = new P3(0f, 1.62f, -1.15f);
            Assert.IsTrue(ShipLayout.CabinExit(back, new P3(0f, 0f, -1f), out float d, out P3 at, out ShipLayout.CabinExitKind kind, out int window));
            Assert.AreEqual(ShipLayout.CabinExitKind.Wall, kind);
            Assert.AreEqual(-1, window);
            Assert.AreEqual(ShipLayout.InnerRadiusAt(1.62f) - 1.15f, d, 0.01f);
            Assert.AreEqual(-ShipLayout.InnerRadiusAt(1.62f), at.Z, 0.01f);

            for (int w = 0; w < ShipLayout.Windows.Length; w++)
            {
                ShipLayout.WindowCentre(ShipLayout.Windows[w], out P3 centre, out _);
                var from = new P3(0f, 1.3f, 0f);
                Assert.IsTrue(ShipLayout.CabinExit(from, centre - from, out _, out _, out kind, out window));
                Assert.AreEqual(ShipLayout.CabinExitKind.Window, kind, ShipLayout.Windows[w].Name);
                Assert.AreEqual(w, window, ShipLayout.Windows[w].Name);
            }

            Assert.IsTrue(ShipLayout.CabinExit(new P3(1.3f, 1f, 0f), new P3(0f, -1f, 0f), out d, out _, out kind, out _));
            Assert.AreEqual(ShipLayout.CabinExitKind.Floor, kind);
            Assert.AreEqual(1f, d, 0.01f);
            Assert.IsTrue(ShipLayout.CabinExit(new P3(0f, 1f, 0f), new P3(0f, -1f, 0f), out _, out _, out kind, out _));
            Assert.AreEqual(ShipLayout.CabinExitKind.Shaft, kind);

            Assert.IsFalse(ShipLayout.CabinExit(new P3(0f, 4f, 0f), new P3(0f, -1f, 0f), out _, out _, out _, out _), "outside the cabin");
        }
    }
}
