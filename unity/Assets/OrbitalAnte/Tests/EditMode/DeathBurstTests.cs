using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Tests.EditMode
{
    public class DeathBurstTests
    {
        static BodyModel FreshBody() => new BodyModel(Enumerable.Repeat(30f, BodyLayout.Count).ToArray());

        [Test]
        public void TheBurst_KillsAndLeavesEveryPartOnItsOwn()
        {
            var body = FreshBody();
            DeathBurst.ApplyAll(body);
            Assert.That(body.Alive, Is.False, "the player is dead after the burst");
            var groups = new HashSet<int>();
            for (int i = 0; i < BodyLayout.Count; i++) groups.Add(body.Group((BodyPartId)i));
            Assert.That(groups.Count, Is.EqualTo(BodyLayout.Count), "all 14 parts fly as pieces of their own");
            Assert.That(groups.Contains(0), Is.False, "nothing is left on the body");
        }

        [Test]
        public void TheHead_GoesInTheFirstMoment_SoTheViewIsFlyingAtOnce()
        {
            Assert.That(DeathBurst.Steps.Any(s => s.Part == BodyPartId.Head && s.At == 0f), Is.True);
            Assert.That(DeathBurst.Steps.Any(s => s.Part == BodyPartId.Torso && s.At == 0f), Is.True);
        }

        [Test]
        public void TheSteps_AreInOrder_AndAllOverBeforeTheDeathPage()
        {
            for (int i = 1; i < DeathBurst.Steps.Length; i++)
                Assert.That(DeathBurst.Steps[i].At, Is.GreaterThanOrEqualTo(DeathBurst.Steps[i - 1].At));
            Assert.That(DeathBurst.Steps[DeathBurst.Steps.Length - 1].At, Is.LessThan(DeathBurst.Hold - 1.5f),
                "the last piece has more than a second and a half in the air before the page comes up");
        }

        [Test]
        public void TheCage_NeverCutsIntoTheCabinsAir()
        {
            List<DeathBurst.Box> boxes = DeathBurst.CageBoxes();
            const float Slack = 0.05f;
            for (float h = 0.01f; h < ShipLayout.CeilingPeak; h += 0.05f)
            {
                float r = ShipLayout.InnerRadiusAt(h) - Slack;
                if (r <= 0f) continue;
                for (int a = 0; a < 72; a++)
                {
                    double angle = a * Math.PI * 2.0 / 72.0;
                    for (float f = 0f; f <= 1f; f += 0.25f)
                    {
                        var p = new P3(Math.Sin(angle) * r * f, h, Math.Cos(angle) * r * f);
                        foreach (var box in boxes)
                            Assert.That(box.Contains(p), Is.False, $"a cage box is in the cabin's air at height {h:0.00}, {r * f:0.00} m out");
                    }
                }
            }
        }

        [Test]
        public void TheCage_HasNoGapAPieceCouldLeaveBy()
        {
            List<DeathBurst.Box> boxes = DeathBurst.CageBoxes();
            float far = ShipLayout.CabinRadius + DeathBurst.CageThickness + 0.2f;
            // Straight out from the middle at every height and every way round: some box is met first.
            for (float h = 0.01f; h < ShipLayout.CeilingPeak; h += 0.03f)
            {
                for (int a = 0; a < 180; a++)
                {
                    double angle = a * Math.PI * 2.0 / 180.0;
                    bool met = false;
                    for (float r = 0f; r <= far && !met; r += 0.01f)
                        met = boxes.Any(b => b.Contains(new P3(Math.Sin(angle) * r, h, Math.Cos(angle) * r)));
                    Assert.That(met, Is.True, $"a piece could leave the cabin at height {h:0.00}, angle {angle:0.00}");
                }
            }
            // Straight up and straight down from anywhere on the floor: the lid and the floor.
            for (float x = -2f; x <= 2f; x += 0.25f)
            for (float z = -2f; z <= 2f; z += 0.25f)
            {
                Assert.That(boxes.Any(b => b.Contains(new P3(x, -0.1f, z))), Is.True, "the floor");
                Assert.That(boxes.Any(b => b.Contains(new P3(x, ShipLayout.CeilingPeak + 0.1f, z))), Is.True, "the lid");
            }
        }
    }
}
