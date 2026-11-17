using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Items;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>The envelope's pictures and its screen: shapes that must fit together, and a show that stays sane at every moment.</summary>
    public sealed class EnvelopeLookTests
    {
        [Test]
        public void TheClosedFlap_CoversTheWholeOpening()
        {
            for (int x = 0; x < EnvelopeArt.Width; x++)
            {
                float edge = EnvelopeArt.PocketEdge(x + 0.5f);
                for (int y = 0; y + 1 < edge; y++)
                    Assert.Less(EnvelopeArt.FlapDistance(x + 0.5f, y + 0.5f), 0f, "the lining shows at " + x + "," + y + " with the flap shut");
            }
        }

        [Test]
        public void TheSeal_SitsOnTheFlap()
        {
            Assert.Less(EnvelopeArt.FlapDistance(EnvelopeArt.SealX, EnvelopeArt.SealY), 0f);
        }

        [Test]
        public void TheSealsHalves_MakeTheWholeSeal()
        {
            ArtImage whole = EnvelopeArt.Seal(0), left = EnvelopeArt.Seal(-1), right = EnvelopeArt.Seal(1);
            int c = EnvelopeArt.SealSize / 2;
            int checkedPixels = 0;
            for (int y = 10; y < EnvelopeArt.SealSize - 10; y += 3)
            for (int x = 10; x < EnvelopeArt.SealSize - 10; x += 3)
            {
                if (Math.Abs(x + 0.5f - EnvelopeArt.CrackX(y)) < 2f) continue;
                float w = whole.Alpha(x, y);
                if (w < 0.99f) continue;
                float l = left.Alpha(x, y), r = right.Alpha(x, y);
                Assert.AreEqual(1f, Math.Max(l, r), 0.02f, "a hole in the halves at " + x + "," + y);
                Assert.IsTrue(l < 0.02f || r < 0.02f, "both halves at " + x + "," + y);
                checkedPixels++;
            }
            Assert.Greater(checkedPixels, 500);
        }

        [Test]
        public void TheCard_FitsInsideTheEnvelope()
        {
            Assert.Less(EnvelopeArt.CardWidth, EnvelopeArt.Width);
            Assert.Less(EnvelopeArt.CardHeight, EnvelopeArt.Height);
            ArtImage card = EnvelopeArt.Card(ItemCatalog.CreateDefaults()[0], ItemRarity.Rare, null);
            Assert.AreEqual(EnvelopeArt.CardWidth, card.Width);
            Assert.AreEqual(1f, card.Alpha(EnvelopeArt.CardWidth / 2, EnvelopeArt.CardHeight / 2), 0.001f);
            Assert.AreEqual(0f, card.Alpha(0, 0), 0.001f, "the card's corners are rounded");
        }

        [Test]
        public void EveryPiece_HasPixelsInRange()
        {
            foreach (var piece in EnvelopeArt.Pieces())
            {
                ArtImage img = piece.Value();
                float sumA = 0f;
                foreach (float v in img.Pixels)
                {
                    Assert.IsFalse(float.IsNaN(v), piece.Key + " has a broken pixel");
                    Assert.That(v, Is.InRange(-0.0001f, 1.0001f), piece.Key);
                }
                for (int i = 3; i < img.Pixels.Length; i += 4) sumA += img.Pixels[i];
                Assert.Greater(sumA, 0f, piece.Key + " is empty");
            }
        }

        static EnvelopeShow Reveal(ItemRarity rarity) => new EnvelopeShow
        {
            mode = EnvelopeMode.Reveal, serial = 3, rarity = rarity, cardTexture = "Card", itemName = "Thing", kindLine = "Rare thing",
            blurb = "A line.", prompt = "Press A", effectName = rarity == ItemRarity.Celestial ? "Aurora" : "",
            effectColour = rarity == ItemRarity.Celestial ? new[] { 0.3f, 1f, 0.6f } : null,
        };

        [Test]
        public void TheRevealsMoments_ComeInOrder()
        {
            for (int r = 0; r < ItemLook.RarityCount; r++)
            {
                EnvelopeScene.Beats b = EnvelopeScene.BeatsFor((ItemRarity)r);
                float[] order = { b.starsStart, b.shakeStart, b.crackStart, b.splitStart, b.flapStart, b.flapEnd, b.riseEnd, b.moveEnd, b.textEnd };
                for (int i = 1; i < order.Length; i++) Assert.Greater(order[i], order[i - 1], (ItemRarity)r + " beat " + i);
                Assert.Less(b.Done, 10f, "the whole reveal stays under ten seconds");
            }
            Assert.Greater(EnvelopeScene.BeatsFor(ItemRarity.Legendary).crackStart, EnvelopeScene.BeatsFor(ItemRarity.Common).crackStart,
                "rarer prizes build up longer");
        }

        [Test]
        public void EveryFrame_IsWellFormed()
        {
            var items = new List<DrawItem>();
            var shows = new List<EnvelopeShow> { new EnvelopeShow { mode = EnvelopeMode.RunStart, serial = 1, prompt = "Go" } };
            for (int r = 0; r < ItemLook.RarityCount; r++) shows.Add(Reveal((ItemRarity)r));
            foreach (EnvelopeShow show in shows)
            {
                for (float t = 0f; t < 12f; t += 1f / 30f)
                {
                    EnvelopeScene.Frame(show, t, items);
                    var keys = new HashSet<string>();
                    var groups = new HashSet<string>();
                    foreach (DrawItem d in items)
                    {
                        Assert.IsTrue(keys.Add(d.key), "two items called " + d.key + " at " + t);
                        foreach (float v in new[] { d.x, d.y, d.w, d.h, d.sx, d.sy, d.rotation, d.a })
                            Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), d.key + " at " + t);
                        Assert.That(d.a, Is.InRange(0f, 1f), d.key);
                        if (!string.IsNullOrEmpty(d.parent)) Assert.IsTrue(groups.Contains(d.parent), d.key + " comes before its group");
                        if (d.kind == DrawKind.Group) groups.Add(d.key);
                        Assert.AreNotEqual("cardslot", d.key, "the card's place holder is always filled");
                    }
                }
            }
        }

        [Test]
        public void WhileInside_TheCardIsBetweenTheLiningAndThePocket()
        {
            EnvelopeShow show = Reveal(ItemRarity.Rare);
            EnvelopeScene.Beats b = EnvelopeScene.BeatsFor(ItemRarity.Rare);
            var items = new List<DrawItem>();
            EnvelopeScene.Frame(show, (b.riseStart + b.riseEnd) * 0.5f, items);
            int lining = items.FindIndex(i => i.key == "lining"), card = items.FindIndex(i => i.key == "card"), pocket = items.FindIndex(i => i.key == "pocket");
            Assert.That(lining, Is.LessThan(card));
            Assert.That(card, Is.LessThan(pocket));
        }

        [Test]
        public void TheResult_ShowsTheName_TheRarity_AndThePrompt()
        {
            EnvelopeShow show = Reveal(ItemRarity.Celestial);
            var items = new List<DrawItem>();
            EnvelopeScene.Frame(show, EnvelopeScene.BeatsFor(show.rarity).Done + 1f, items);
            Assert.IsTrue(items.Exists(i => i.key == "name" && i.text == "Thing"));
            Assert.IsTrue(items.Exists(i => i.key == "rarity" && i.text == "CELESTIAL"));
            Assert.IsTrue(items.Exists(i => i.key == "effect"));
            Assert.IsTrue(items.Exists(i => i.key == "prompt"));
            Assert.IsFalse(items.Exists(i => i.key == "pocket"), "the envelope has gone by the end");
        }

        [Test]
        public void TheRunStartScreen_PrintsTheRule_AndTheOdds()
        {
            var show = new EnvelopeShow { mode = EnvelopeMode.RunStart, serial = 1, oddsLine = EnvelopeScene.OddsLine(new PrizeOdds()), prompt = "Go" };
            var items = new List<DrawItem>();
            EnvelopeScene.Frame(show, 3f, items);
            Assert.IsTrue(items.Exists(i => i.key == "rule" && i.text.Contains("Ante 9")));
            DrawItem odds = items.Find(i => i.key == "odds");
            StringAssert.Contains("Celestial 0.5%", odds.text);
            StringAssert.Contains("Common 40%", odds.text);
        }

        [Test]
        public void EveryRevealCue_FallsWithinTheShow()
        {
            for (int r = 0; r < ItemLook.RarityCount; r++)
            {
                EnvelopeShow show = Reveal((ItemRarity)r);
                List<EnvelopeCue> cues = EnvelopeScene.Cues(show);
                for (int i = 1; i < cues.Count; i++) Assert.GreaterOrEqual(cues[i].time, cues[i - 1].time, "cues must be in time order");
                Assert.LessOrEqual(cues[cues.Count - 1].time, EnvelopeScene.BeatsFor(show.rarity).SkipTo, "the prize's sound plays before the skip point");
                Assert.IsTrue(cues[cues.Count - 1].eventId.StartsWith("envelope.reveal"));
            }
        }
    }
}
