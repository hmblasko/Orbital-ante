using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Cockpit;
using OrbitalAnte.Items;
using OrbitalAnte.Ship;
using OrbitalAnte.Spacewalk;
using OrbitalAnte.Weapons;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// What the Locker's prizes do in the game (Ralph loop pass 4, 28 Nov 2026; Items/PrizeLooks.cs, Ship/HullPaint.cs,
    /// the skin layer in OA_Model.shader): every upgrade is a sidegrade whose cost is applied as surely as its gain,
    /// a ship paint repaints the hull and leaves its dark panels dark, every skin pattern is one the model shader
    /// draws, and the suit's pieces never make the suit plate hard to read, a jet look like damage, or the visor's
    /// tint strong enough to change what colours mean. Plain C#.
    /// </summary>
    public class PrizeLookTests
    {
        static readonly List<ItemSpec> Catalog = ItemCatalog.CreateDefaults();

        static ItemSpec Find(string id) => Catalog.Find(i => i.id == id);

        static IEnumerable<ItemSpec> Of(ItemCategory category) => Catalog.FindAll(i => i.category == category);

        // ------------------------------------------------------------------ upgrades

        static float Read(RunMods m, UpgradeStat stat)
        {
            switch (stat)
            {
                case UpgradeStat.GunDamage: return m.GunDamage;
                case UpgradeStat.MoveSpeed: return m.Move;
                case UpgradeStat.SuitAir: return m.Oxygen;
                case UpgradeStat.SuitArmour: return m.MaxHealth;
                case UpgradeStat.JetFuel: return m.JetFuel;
                case UpgradeStat.Focus: return m.FocusGain;
                // A faster reload is a shorter one: read as speed.
                case UpgradeStat.ReloadSpeed: return 1f / m.ReloadTime;
                case UpgradeStat.ShipFuel: return m.ShipFuel;
                case UpgradeStat.MeleeDamage: return m.MeleeDamage;
                default: return float.NaN;
            }
        }

        static readonly UpgradeStat[] Stats =
        {
            UpgradeStat.GunDamage, UpgradeStat.MoveSpeed, UpgradeStat.SuitAir, UpgradeStat.SuitArmour, UpgradeStat.JetFuel,
            UpgradeStat.Focus, UpgradeStat.ReloadSpeed, UpgradeStat.ShipFuel, UpgradeStat.MeleeDamage,
        };

        [Test]
        public void EveryUpgrade_IsASidegrade_WithItsGainAndItsCostOnTwoDifferentNumbers()
        {
            foreach (ItemSpec u in Of(ItemCategory.Upgrade))
            {
                Assert.AreNotEqual(UpgradeStat.None, u.upgradeUp, u.id);
                Assert.AreNotEqual(UpgradeStat.None, u.upgradeDown, u.id + ": Part B section 4, sidegrades only");
                Assert.AreNotEqual(u.upgradeUp, u.upgradeDown, u.id);
                Assert.Greater(UpgradeEffects.Factor(u, u.upgradeUp), 1f, u.id);
                Assert.Less(UpgradeEffects.Factor(u, u.upgradeDown), 1f, u.id);
            }
        }

        [Test]
        public void AnUpgrade_MovesOnlyTheTwoNumbersItNames_EachByItsOwnAmount()
        {
            foreach (ItemSpec u in Of(ItemCategory.Upgrade))
            {
                var m = new RunMods();
                UpgradeEffects.Apply(m, u);
                foreach (UpgradeStat stat in Stats)
                {
                    float expected = stat == u.upgradeUp ? 1f + u.upgradeUpAmount : stat == u.upgradeDown ? 1f - u.upgradeDownAmount : 1f;
                    Assert.AreEqual(expected, Read(m, stat), 1e-5f, u.id + " " + stat);
                }
            }
        }

        [Test]
        public void HotLoads_MakesEveryReloadLonger_ByItsCost()
        {
            var m = new RunMods();
            UpgradeEffects.Apply(m, Find("UpgradeHotLoads"));
            Assert.AreEqual(1f / 0.88f, m.ReloadTime, 1e-5f);
            Assert.AreEqual(1.10f, m.GunDamage, 1e-5f);
        }

        [Test]
        public void TheRunsBuild_TakesTheLockerUpgradeOnTopOfItsPerks()
        {
            var perks = new PerkLoadout();
            Assert.IsTrue(perks.Add(RoguelitePerks.Create().Find(p => p.id == "perk.glass_cannon")));
            RunMods without = RunMods.From(perks, null, Hazard.None, new AnteBalance());
            RunMods with = RunMods.From(perks, null, Hazard.None, new AnteBalance(), false, Find("UpgradeHeavyHands"));
            Assert.AreEqual(without.MeleeDamage * 1.25f, with.MeleeDamage, 1e-5f);
            Assert.AreEqual(without.GunDamage * 0.92f, with.GunDamage, 1e-5f, "the cost is applied as surely as the gain");
            Assert.AreEqual(without.MaxHealth, with.MaxHealth, 1e-5f);
            RunMods none = RunMods.From(perks, null, Hazard.None, new AnteBalance(), false, null);
            Assert.AreEqual(without.GunDamage, none.GunDamage, 1e-6f);
            Assert.AreEqual(1f, none.ReloadTime);
            Assert.AreEqual(1f, RunMods.Neutral.ReloadTime, "the shared neutral numbers are never touched");
        }

        [Test]
        public void NoAsset_CanMakeAnUpgradeMoveANumberPastItsLimits()
        {
            var wild = new ItemSpec { id = "Wild", category = ItemCategory.Upgrade, upgradeUp = UpgradeStat.GunDamage, upgradeUpAmount = 50f, upgradeDown = UpgradeStat.MoveSpeed, upgradeDownAmount = 5f };
            Assert.AreEqual(UpgradeEffects.Most, UpgradeEffects.Factor(wild, UpgradeStat.GunDamage), 1e-6f);
            Assert.AreEqual(UpgradeEffects.Least, UpgradeEffects.Factor(wild, UpgradeStat.MoveSpeed), 1e-6f);
            Assert.AreEqual(1f, UpgradeEffects.Factor(Find("PaintMoonDust"), UpgradeStat.GunDamage), "only an upgrade changes numbers");
            Assert.AreEqual(1f, UpgradeEffects.Factor(null, UpgradeStat.GunDamage));
        }

        [Test]
        public void ABiggerJetTank_LastsLonger_AndASmallerShipTank_Shorter()
        {
            var rules = new SuitRules();
            var plain = new SuitSupplies(rules);
            var bigger = new SuitSupplies(rules) { FuelScale = 1.2f };
            for (int i = 0; i < 100; i++)
            {
                plain.Tick(0.1f, false, false, 1f, 1f);
                bigger.Tick(0.1f, false, false, 1f, 1f);
            }
            Assert.AreEqual(1f - 10f / rules.fuelSeconds, plain.Fuel, 1e-4f);
            Assert.AreEqual(1f - 10f / (rules.fuelSeconds * 1.2f), bigger.Fuel, 1e-4f);

            var shipRules = new SupplyRules();
            var ship = new ShipSupplies(shipRules) { FuelScale = 0.9f };
            var shipPlain = new ShipSupplies(shipRules);
            ship.Tick(10f, 1f, false, false, 0);
            shipPlain.Tick(10f, 1f, false, false, 0);
            Assert.AreEqual(1f - 10f / shipRules.fuelSeconds, shipPlain.Fuel, 1e-4f);
            Assert.AreEqual(1f - 10f / (shipRules.fuelSeconds * 0.9f), ship.Fuel, 1e-4f);
        }

        [Test]
        public void AReloadScale_ChangesOnlyHowLongTheReloadTakes()
        {
            var spec = new FirearmSpec { Automatic = true, ShotsPerSecond = 10f, MagazineSize = 30, ReloadSeconds = 2f, OneHandReloadMultiplier = 2f, OneHandCanReload = true };
            var gun = new FirearmState(spec, 10);
            Assert.IsTrue(gun.StartReload(2, 1.25f));
            Assert.IsFalse(gun.Tick(2.4f), "a quarter longer: not done at 2.4 s");
            Assert.IsTrue(gun.Tick(0.2f));
            Assert.AreEqual(30, gun.Ammo);

            var clamped = new FirearmState(spec, 10);
            Assert.IsTrue(clamped.StartReload(2, 50f));
            Assert.IsFalse(clamped.Tick(3.9f), "never more than twice as long");
            Assert.IsTrue(clamped.Tick(0.2f));
            var odd = new FirearmState(spec, 10);
            Assert.IsTrue(odd.StartReload(2, float.NaN));
            Assert.IsTrue(odd.Tick(2.05f), "a broken number reloads as the gun does");
        }

        // ------------------------------------------------------------------ ship paint

        static readonly string[] PlainColours = { "#D8D5CC", "#9A9EA6", "#2A2C31", "#C8962E", "#CFCBC0" };

        static bool Near(float[] colours, int corner, P3 c) =>
            Math.Abs(colours[corner * 3] - c.X) < 1e-4f && Math.Abs(colours[corner * 3 + 1] - c.Y) < 1e-4f && Math.Abs(colours[corner * 3 + 2] - c.Z) < 1e-4f;

        [Test]
        public void TheUnpaintedHull_IsTheLandersOwnWhiteGreyAndGold()
        {
            ShipMesh hull = ShipShell.Hull();
            ShipMesh none = ShipShell.Hull(null);
            CollectionAssert.AreEqual(hull.Colors, none.Colors);
            var found = new HashSet<string>();
            for (int i = 0; i < hull.Colors.Length / 3; i++)
            {
                string match = null;
                foreach (string web in PlainColours) if (Near(hull.Colors, i, MeshBuilder.Web(web))) match = web;
                Assert.IsNotNull(match, "corner " + i + " is not one of the lander's own colours");
                found.Add(match);
            }
            Assert.AreEqual(PlainColours.Length, found.Count, "every one of the lander's colours is still on the hull");
        }

        [Test]
        public void EveryShipPaint_RepaintsTheHull_AndLeavesItsDarkPanelsDark()
        {
            ShipMesh plain = ShipShell.Hull();
            P3 dark = MeshBuilder.Web("#2A2C31");
            foreach (ItemSpec item in Of(ItemCategory.ShipPaint))
            {
                HullPaint paint = PrizeLooks.Paint(item);
                Assert.IsNotNull(paint, item.id);
                ShipMesh painted = ShipShell.Hull(paint);
                Assert.AreEqual(plain.Colors.Length, painted.Colors.Length, item.id + ": the shape is the same");
                CollectionAssert.AreEqual(plain.Positions, painted.Positions, item.id);
                int changed = 0;
                for (int i = 0; i < plain.Colors.Length / 3; i++)
                {
                    bool wasDark = Near(plain.Colors, i, dark);
                    if (wasDark) Assert.IsTrue(Near(painted.Colors, i, dark), item.id + ": corner " + i + " was dark (a window band, the skirt or a dark panel)");
                    else if (!Near(painted.Colors, i, new P3(plain.Colors[i * 3], plain.Colors[i * 3 + 1], plain.Colors[i * 3 + 2]))) changed++;
                    for (int k = 0; k < 3; k++) Assert.That(painted.Colors[i * 3 + k], Is.InRange(0f, 1f), item.id);
                }
                Assert.Greater(changed, plain.Colors.Length / 3 / 2, item.id + " must visibly repaint the hull");
            }
            Assert.IsNull(PrizeLooks.Paint(Find("SkinHazard")), "only a ship paint paints the ship");
        }

        [Test]
        public void ThePaintsStyles_AreTheOnesTheirWordsPromise()
        {
            Assert.AreEqual(HullPaintStyle.Band, PrizeLooks.Paint(Find("PaintRacingGreen")).Style, "with a cream stripe down the hull");
            Assert.AreEqual(HullPaintStyle.TwoTone, PrizeLooks.Paint(Find("PaintTwoTone")).Style, "teal over cream");
            HullPaint twoTone = PrizeLooks.Paint(Find("PaintTwoTone"));
            Assert.AreEqual(twoTone.Main.X, twoTone.Panel(HullPanelKind.Roof, false).X, 1e-6f, "the roof in the paint");
            Assert.AreEqual(twoTone.Accent.X, twoTone.Panel(HullPanelKind.WallLight, false).X, 1e-6f, "the walls in the accent");
            HullPaint green = PrizeLooks.Paint(Find("PaintRacingGreen"));
            Assert.AreEqual(green.Accent.Y, green.Panel(HullPanelKind.Band, false).Y, 1e-6f, "the band round the hull is the stripe");
            Assert.AreEqual(HullPaintStyle.Plain, PrizeLooks.Paint(Find("PaintMidnightFoil")).Style);
        }

        // ------------------------------------------------------------------ gun skins

        static string Shader(string name)
        {
            string folder = TestContext.CurrentContext.TestDirectory;
            for (int up = 0; up < 10 && folder != null; up++)
            {
                foreach (string head in new[] { Path.Combine("unity", "Assets", "OrbitalAnte"), Path.Combine("Assets", "OrbitalAnte") })
                {
                    string candidate = Path.Combine(folder, head, "Resources", "Shaders", name);
                    if (File.Exists(candidate)) return File.ReadAllText(candidate);
                }
                folder = Path.GetDirectoryName(folder);
            }
            string fromHere = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "OrbitalAnte", "Resources", "Shaders", name);
            return File.Exists(fromHere) ? File.ReadAllText(fromHere) : null;
        }

        [Test]
        public void EverySkinPattern_IsOneTheModelShaderDraws_ByTheSameNumber()
        {
            string text = Shader("OA_Model.shader");
            Assert.IsNotNull(text, "OA_Model.shader is missing");
            foreach (SkinPattern pattern in Enum.GetValues(typeof(SkinPattern)))
            {
                string name = "OA_SKIN_" + pattern.ToString().ToUpperInvariant();
                Match m = Regex.Match(text, Regex.Escape(name) + @"\s+(\d+)");
                Assert.IsTrue(m.Success, "the shader does not list " + name);
                Assert.AreEqual((int)pattern, int.Parse(m.Groups[1].Value), name);
            }
            // Made at run time, so the variant must be kept in a build whatever the saved materials say.
            StringAssert.Contains("#pragma multi_compile_local _ _OA_SKIN", text);
            StringAssert.DoesNotContain("shader_feature_local _ _OA_SKIN", text);
            // In the one material buffer every variant shares (the SRP batcher needs one layout).
            int start = text.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal);
            int end = text.IndexOf("CBUFFER_END", start, StringComparison.Ordinal);
            string buffer = text.Substring(start, end - start);
            foreach (string field in new[] { "_SkinColour", "_SkinAccent", "_SkinSet" })
                StringAssert.Contains(field, buffer, field + " must be declared for every variant");
            StringAssert.DoesNotContain("#if", buffer, "nothing in the material buffer may depend on a keyword");
        }

        [Test]
        public void EverySkin_HasALook_AndTheSpecialOnesTheirOwnPattern()
        {
            foreach (ItemSpec item in Of(ItemCategory.WeaponSkin))
            {
                SkinLook look = PrizeLooks.Skin(item);
                Assert.AreEqual(3, look.Colour.Length, item.id);
                Assert.AreEqual(3, look.Accent.Length, item.id);
                Assert.IsNotEmpty(look.Key, item.id);
            }
            Assert.AreEqual(SkinPattern.Grain, PrizeLooks.Skin(Find("SkinWoodPanel")).Pattern);
            Assert.AreEqual(SkinPattern.Stripes, PrizeLooks.Skin(Find("SkinHazard")).Pattern);
            Assert.AreEqual(SkinPattern.Pearl, PrizeLooks.Skin(Find("SkinPistolPearl")).Pattern);
            Assert.AreEqual(SkinPattern.Flake, PrizeLooks.Skin(Find("SkinShotgunCandy")).Pattern);
            Assert.AreEqual(SkinPattern.Stars, PrizeLooks.Skin(Find("SkinRifleStarChart")).Pattern);
            Assert.AreEqual(SkinPattern.Gilt, PrizeLooks.Skin(Find("SkinSniperGilded")).Pattern);
            Assert.AreEqual(SkinPattern.Paint, PrizeLooks.Skin(new ItemSpec { id = "SkinNew", category = ItemCategory.WeaponSkin }).Pattern,
                "a skin Matthew adds gets the plain look in its own colours");
            Assert.AreNotEqual(PrizeLooks.Skin(Find("SkinWoodPanel")).Key, PrizeLooks.Skin(Find("SkinHazard")).Key, "two skins never share a material");
        }

        // ------------------------------------------------------------------ the suit

        static float Linear(float c) => c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4f);

        static float Luminance(float[] c) => 0.2126f * Linear(c[0]) + 0.7152f * Linear(c[1]) + 0.0722f * Linear(c[2]);

        static float Contrast(float[] a, float[] b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05f) / (Math.Min(la, lb) + 0.05f);
        }

        // HullKit.Paint and HullKit.Dim, the plate's words (held to HullKit below).
        static readonly float[] PlateWords = { 0.937f, 0.902f, 0.824f };
        static readonly float[] PlateDimWords = { 0.663f, 0.635f, 0.580f };

        [Test]
        public void WithNothingWorn_TheSuitLooksAsItAlwaysDid()
        {
            SuitLook look = PrizeLooks.Suit(null, null, null, null, null);
            CollectionAssert.AreEqual(SuitLook.StandardShade, look.Shade);
            Assert.AreEqual(1f, look.ShadeStrength);
            Assert.IsFalse(look.Tinted);
            CollectionAssert.AreEqual(SuitLook.StandardFace, look.PlateFace);
            CollectionAssert.AreEqual(SuitLook.StandardEdge, look.PlateEdge);
            Assert.IsNull(look.Jet, "the suit's own jet colour");
            Assert.IsFalse(look.Badge);

            // The standard numbers are the game's own: the plate's colours in HullKit, the shade's in SuitHud.
            string kit = Source("UI", "HullKit.cs"), hud = Source("Spacewalk", "SuitHud.cs");
            StringAssert.Contains("PlateFace = new Color(0.118f, 0.122f, 0.129f", kit);
            StringAssert.Contains("PlateEdge = new Color(0.278f, 0.282f, 0.294f", kit);
            StringAssert.Contains("Paint = new Color(0.937f, 0.902f, 0.824f)", kit);
            StringAssert.Contains("Dim = new Color(0.663f, 0.635f, 0.580f)", kit);
            StringAssert.Contains("new Color(0.04f, 0.03f, 0.03f)", hud);
        }

        static string Source(string folder, string file)
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int up = 0; up < 10 && dir != null; up++)
            {
                foreach (string head in new[] { Path.Combine("unity", "Assets", "OrbitalAnte"), Path.Combine("Assets", "OrbitalAnte") })
                {
                    string candidate = Path.Combine(dir, head, "Scripts", "Runtime", folder, file);
                    if (File.Exists(candidate)) return File.ReadAllText(candidate);
                }
                dir = Path.GetDirectoryName(dir);
            }
            string fromHere = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "OrbitalAnte", "Scripts", "Runtime", folder, file);
            Assert.IsTrue(File.Exists(fromHere), file + " not found");
            return File.ReadAllText(fromHere);
        }

        [Test]
        public void EverySuit_KeepsThePlatesWordsEasyToRead()
        {
            foreach (ItemSpec item in Catalog.FindAll(i => i.category == ItemCategory.Cosmetic && i.slot == CosmeticSlot.Suit))
            {
                SuitLook look = PrizeLooks.Suit(null, null, item, null, null);
                Assert.GreaterOrEqual(Contrast(PlateWords, look.PlateFace), 7f, item.id + ": the plate's words");
                Assert.GreaterOrEqual(Contrast(PlateDimWords, look.PlateFace), 4.5f, item.id + ": the plate's dim labels");
                float moved = 0f;
                for (int k = 0; k < 3; k++) moved += Math.Abs(look.PlateFace[k] - SuitLook.StandardFace[k]) + Math.Abs(look.PlateEdge[k] - SuitLook.StandardEdge[k]);
                Assert.Greater(moved, 0.05f, item.id + " must show on the plate");
            }
            // Even the palest suit there could be.
            SuitLook white = PrizeLooks.Suit(null, null, new ItemSpec { category = ItemCategory.Cosmetic, slot = CosmeticSlot.Suit, colour = new[] { 1f, 1f, 1f }, accent = new[] { 1f, 1f, 1f } }, null, null);
            Assert.GreaterOrEqual(Contrast(PlateWords, white.PlateFace), 7f);
        }

        [Test]
        public void NoJetpack_GivesAFlameThatReadsAsDamage()
        {
            foreach (float[] colour in new[] { new[] { 1f, 0.2f, 0.05f }, new[] { 0.9f, 0.45f, 0.05f }, new[] { 0.5f, 0f, 0f }, Find("PackRacingStripes").colour })
            {
                SuitLook look = PrizeLooks.Suit(null, null, null, new ItemSpec { category = ItemCategory.Cosmetic, slot = CosmeticSlot.Pack, colour = colour }, null);
                Assert.IsNotNull(look.Jet);
                float least = Math.Min(look.Jet[0], Math.Min(look.Jet[1], look.Jet[2]));
                Assert.GreaterOrEqual(least, 0.35f - 1e-5f, "white-hot toward its colour, never a deep red or orange (" + string.Join(",", colour) + ")");
            }
        }

        [Test]
        public void AVisorTint_IsGentle_AndAHelmetsEdge_StaysDark()
        {
            foreach (float[] colour in new[] { new[] { 0f, 0f, 0f }, Find("VisorSunset").colour, new[] { 0.2f, 1f, 0.2f } })
            {
                SuitLook look = PrizeLooks.Suit(null, new ItemSpec { category = ItemCategory.Cosmetic, slot = CosmeticSlot.Visor, colour = colour }, null, null, null);
                Assert.IsTrue(look.Tinted || (colour[0] >= 0.999f && colour[1] >= 0.999f && colour[2] >= 0.999f));
                foreach (float c in look.VisorTint) Assert.GreaterOrEqual(c, 1f - PrizeLooks.VisorTintShare - 1e-5f, "the world's colours still mean what they mean");
            }
            foreach (ItemSpec helmet in Catalog.FindAll(i => i.category == ItemCategory.Cosmetic && i.slot == CosmeticSlot.Helmet))
            {
                SuitLook look = PrizeLooks.Suit(helmet, null, null, null, null);
                foreach (float c in look.Shade) Assert.LessOrEqual(c, 0.25f, helmet.id + ": the gauges sit on something darker than a bright sky");
                Assert.That(look.ShadeStrength, Is.InRange(0.4f, 1.2f), helmet.id);
            }
            Assert.Less(PrizeLooks.Suit(Find("HelmetFishbowl"), null, null, null, null).ShadeStrength, 1f, "a clear dome shades the edge less");
            Assert.IsTrue(PrizeLooks.Suit(null, null, null, null, Find("BadgeMissionPatch")).Badge);
        }

        // ------------------------------------------------------------------ the reveal

        [Test]
        public void TheReveal_SaysWhereThePrizeWentToWork_BelowTheRestAndAboveThePrompt()
        {
            foreach (bool upgrade in new[] { false, true })
            {
                var show = new EnvelopeShow
                {
                    mode = EnvelopeMode.Reveal, serial = 3, rarity = ItemRarity.Rare, cardTexture = "Card", itemName = "Thing", kindLine = "Rare thing",
                    blurb = "A line.", prompt = "Press A", upLine = upgrade ? "+10% gun damage" : "", downLine = upgrade ? "-12% reload speed" : "",
                    useLine = upgrade ? Locker.RevealWords(Find("UpgradeHotLoads"), false) : Locker.RevealWords(Find("PaintMoonDust"), true),
                };
                var items = new List<DrawItem>();
                EnvelopeScene.Frame(show, EnvelopeScene.BeatsFor(show.rarity).Done + 1f, items);
                int at = items.FindIndex(i => i.key == "use");
                Assert.GreaterOrEqual(at, 0, "the use line is drawn");
                DrawItem use = items[at], owned = items.Find(i => i.key == "owned"), prompt = items.Find(i => i.key == "prompt");
                Assert.IsNotEmpty(owned.text);
                Assert.IsNotEmpty(prompt.text);
                Assert.AreEqual(show.useLine, use.text);
                Assert.GreaterOrEqual(use.y - use.h * 0.5f, owned.y + owned.h * 0.5f, "below the owned line, not over it");
                Assert.Less(use.y + use.h * 0.5f, prompt.y - prompt.h * 0.5f, "above the prompt");
                Assert.AreEqual(upgrade ? 0.96f : 0.624f, use.r, 1e-3f, upgrade ? "a hint in cream" : "in use, in the Collection's green");
            }
        }
    }
}
