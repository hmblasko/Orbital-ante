using System.IO;
using NUnit.Framework;
using OrbitalAnte.Planets;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Real light and the Sun's cast shadows (26 Nov 2026): the sums the ground is lit by, and that the shaders
    /// which cast and catch shadows keep the pieces a build needs.
    /// </summary>
    public class GroundLightTests
    {
        [Test]
        public void Sunlight_FallsOnlyOnWhatFacesTheSun_AndNotInAShadow()
        {
            Assert.AreEqual(1, GroundLight.Direct(1, 1), 1e-9);
            Assert.AreEqual(0, GroundLight.Direct(-0.04, 1), 1e-9, "a face turned from the Sun gets none: no wrap-round");
            Assert.AreEqual(0, GroundLight.Direct(-1, 1), 1e-9);
            Assert.AreEqual(0, GroundLight.Direct(1, 0), 1e-9, "in full cast shadow there is no sunlight");
            Assert.Less(GroundLight.Direct(0.2, 1), GroundLight.Direct(0.6, 1));
        }

        [Test]
        public void TheEdgeOfNight_IsHardWithNoAir_AndSoftWithAir()
        {
            double airless = OrbitLook.AirlessSoft;
            Assert.AreEqual(0, GroundLight.Reached(-airless, airless), 1e-9);
            Assert.AreEqual(1, GroundLight.Reached(airless * 2.5, airless), 1e-9);
            // Where an airless world is already in full day, one with air is still coming out of the night.
            Assert.Less(GroundLight.Reached(0.02, 0.08), 0.5);
            Assert.AreEqual(1, GroundLight.Reached(0.02, airless), 1e-9);
            for (double up = -0.3; up < 1; up += 0.01)
                Assert.GreaterOrEqual(GroundLight.Reached(up + 0.01, 0.08), GroundLight.Reached(up, 0.08), "never darker as the Sun climbs");
        }

        [Test]
        public void LowSunlight_Reddens_AndHighSunlightBarelyChanges()
        {
            foreach (string id in new[] { "Earth", "Mars", "Titan", "Venus" })
            {
                PlanetProfile profile = null;
                foreach (PlanetProfile p in PlanetProfiles.Defaults()) if (p.bodyId == id) profile = p;
                Assert.IsNotNull(profile, id);
                AirLook air = OrbitLook.Air(profile, 400, false);
                double redLow = GroundLight.Reddened(0.02, air.ReddenR), blueLow = GroundLight.Reddened(0.02, air.ReddenB);
                Assert.Greater(redLow, blueLow, id + ": low sunlight keeps more of its red than its blue");
                Assert.Greater(GroundLight.Reddened(1, air.ReddenB), 0.9, id + ": overhead sunlight is nearly white");
                Assert.Greater(redLow, 0.5, id + ": a low Sun still lights the ground");
            }
            Assert.AreEqual(1, GroundLight.Reddened(0, 0), 1e-12, "no air, no reddening");
        }

        [Test]
        public void WhereTheViewerStands_TheGroundsFillIsTheViewersFill()
        {
            // Whatever the weather has done to the viewer's fill, ground under the same sky gets exactly that.
            foreach (double bright in new[] { GroundLight.NightLeast, 0.3, 1.0 })
                foreach (double viewerFill in new[] { 0.02, 0.2, 0.5 })
                    Assert.AreEqual(viewerFill, GroundLight.Fill(viewerFill, bright, 1, 0.45, 0.06, GroundLight.SpaceShareInAir, bright), 1e-9);
        }

        [Test]
        public void GroundFarOff_GoesDarkWhereItIsNightThere_AndBrightWhereItIsDay()
        {
            double least = GroundLight.NightLeast, dayFill = 0.45, space = 0.06, share = GroundLight.SpaceShareInAir;
            // The viewer stands in daylight; the ground over the hill is in the night.
            double viewerDay = dayFill + space * share;
            double farNight = GroundLight.Fill(viewerDay, 1, 1, dayFill, space, share, GroundLight.SkyBrightness(-0.3, least));
            Assert.Less(farNight, viewerDay * 0.06);
            Assert.Greater(farNight, 0, "never pure black from the ground: eyes are used to the dark");
            // The viewer stands in the night; the ground over the hill is in daylight.
            double viewerNight = dayFill * least + space * share;
            double farDay = GroundLight.Fill(viewerNight, least, 1, dayFill, space, share, GroundLight.SkyBrightness(0.6, least));
            Assert.Greater(farDay, dayFill * 0.9);
            Assert.Less(farDay, dayFill * 1.2, "the viewer's night fill is not multiplied up into a glare");
        }

        [Test]
        public void FromAboveTheAir_TheGroundIsFilledByItsOwnSky_NotTheViewersBlackOne()
        {
            double dayFill = 0.45, space = 0.06;
            // Above the air the viewer's fill is only space's faint light, with no sky over them.
            double day = GroundLight.Fill(space, 1, 0, dayFill, space, GroundLight.SpaceShareInAir, 1);
            Assert.AreEqual(dayFill + space * GroundLight.SpaceShareInAir, day, 1e-9);
            // A world with no air has no sky to fill a shadow: space's light, the same by night and by day.
            Assert.AreEqual(space, GroundLight.Fill(space, 1, 0, 0, space, 1, GroundLight.SkyBrightness(-0.5, 1)), 1e-9);
            Assert.AreEqual(space, GroundLight.Fill(space, 1, 0, 0, space, 1, GroundLight.SkyBrightness(0.8, 1)), 1e-9);
        }

        [Test]
        public void NightGround_KeepsItsFillOnFoot_AndMatchesTheFarBallFromHighUp()
        {
            const double radius = 500, nightSide = 0.15;
            Assert.AreEqual(1, GroundLight.FarNight(0, radius, nightSide), 1e-9);
            Assert.AreEqual(1, GroundLight.FarNight(200, radius, nightSide), 1e-9, "anywhere a jetpack reaches, eyes are used to the dark");
            // The real ground takes over from the far ball inside four radii of the centre, three above the ground.
            Assert.AreEqual(nightSide, GroundLight.FarNight(radius * GroundLight.AdaptTo, radius, nightSide), 1e-9);
            Assert.AreEqual(nightSide, GroundLight.FarNight(radius * 10, radius, nightSide), 1e-9);
            for (double h = 0; h < radius * 4; h += 25)
                Assert.GreaterOrEqual(GroundLight.FarNight(h, radius, nightSide), GroundLight.FarNight(h + 25, radius, nightSide), "never brighter with height");
        }

        [Test]
        public void ShadowSettings_AreCrispAtYourFeet_AtAnyDistanceTheDialAllows()
        {
            Assert.AreEqual(SunShadowRules.LeastDistance, SunShadowRules.Distance(0));
            Assert.AreEqual(SunShadowRules.MostDistance, SunShadowRules.Distance(9999));
            Assert.AreEqual(70f, SunShadowRules.Distance(70f));
            // At the default, one dot of the near shadow picture covers about 4 cm of ground.
            Assert.Less(SunShadowRules.NearTexel(70f), 0.05f);
            Assert.Less(SunShadowRules.NearTexel(SunShadowRules.MostDistance), 0.16f, "still no coarser than a hand's width at the furthest setting");
        }

        // ------------------------------------------------------------------ the shaders

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
        public void TheGroundAndModelShaders_CastShadows_AndKeepTheSwitchesThatCatchThem()
        {
            foreach (string name in new[] { "OA_Terrain.shader", "OA_Model.shader" })
            {
                string text = Shader(name);
                Assert.IsNotNull(text, name + " is missing");
                StringAssert.Contains("\"LightMode\" = \"ShadowCaster\"", text, name + " must draw into the Sun's shadow picture");
                StringAssert.Contains("ApplyShadowBias", text, name + ": without the bias every face shadows itself");
                StringAssert.DoesNotContain("ApplyShadowClamping", text, name + ": not every URP 17 has it; the clamp is written out");
                // The switch must be a multi_compile: URP sets it for the whole game, and a shader_feature would be
                // dropped from a build because no saved material turns it on.
                StringAssert.Contains("#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE", text, name);
                StringAssert.Contains("_SHADOWS_SOFT", text, name);
                int define = text.IndexOf("#define OA_RECEIVES_SHADOWS"), include = text.IndexOf("OA_Look.hlsl\"");
                Assert.GreaterOrEqual(define, 0, name + " must ask the shared look for cast shadows");
                Assert.Less(define, include, name + ": the request must come before the shared look is included");
                StringAssert.Contains("OA_LightAt(", text, name);
            }
        }

        [Test]
        public void TheSharedLook_DoesTheSameSumsAsTheRules()
        {
            string look = Shader("OA_Look.hlsl");
            Assert.IsNotNull(look);
            // If one of these lines changes, GroundLight must change with it (and the other way round).
            StringAssert.Contains("smoothstep(-_OA_Place.x, _OA_Place.x * 2.5, sunUp)", look);
            StringAssert.Contains("exp(-_OA_Redden.rgb * min(1.0 / max(sunUp + 0.06, 0.03), 14.0) * lerp(_OA_Place.y, 1.0, grounded))", look);
            StringAssert.Contains("saturate((facing + 0.04) / 1.04) * shade", look);
            StringAssert.Contains("lerp(_OA_Place.w, 1.0, smoothstep(-0.1, 0.25, sunUp))", look);
            StringAssert.Contains("fill * min(here / max(_OA_Place.z, 0.001), 1.0) + _OA_GroundFill.rgb * (max(here - _OA_Place.z, 0.0) * _OA_Place.y)", look);
            // The far ball's edge of night and reddening are the same sums, so the ground can take over from it.
            string body = Shader("OA_Body.shader");
            StringAssert.Contains("smoothstep(-_Soft, _Soft * 2.5, mu)", body);
            StringAssert.Contains("exp(-_Redden.rgb * min(1.0 / max(mu + 0.06, 0.03), 14.0))", body);
            // Shadows are only read by a shader that asks, and only when URP has made the picture.
            StringAssert.Contains("#if defined(OA_RECEIVES_SHADOWS)", look);
            foreach (string word in new[] { " real ", " real;", " smooth ", " smooth;", " sample ", " point " })
                foreach (string kind in new[] { "half", "half3", "half4", "float", "float3", "bool" })
                    StringAssert.DoesNotContain(kind + word, look, "'" + word.Trim() + "' is not a safe name in a shader");
        }
    }
}
