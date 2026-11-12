using System.Collections;
using NUnit.Framework;
using OrbitalAnte.AI;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Weapons;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Verification.Capture
{
    /// <summary>
    /// Studio captures of the characters and the things they carry: the playtest mannequin in every movement
    /// state, in motion, and the guns. Target ids: "mannequin", "weapons".
    /// </summary>
    public class CharacterCaptures : CaptureSuite
    {
        const float Big = 100000f;

        protected override void Cleanup()
        {
            GibManager.DestroyInstance();
            ContentDatabase.ResetStatics();
        }

        Mannequin Spawn(string name, out MannequinLocomotion locomotion)
        {
            Mannequin m = MannequinFactory.Create(Vector3.zero, Quaternion.identity, Root.transform, MannequinConfig.Load(), name);
            locomotion = m.GetComponent<MannequinLocomotion>();
            locomotion.Wander = false;
            // The fighting brain would overrule where the capture tells the character to go.
            m.GetComponent<MannequinCombat>().enabled = false;
            return m;
        }

        static void Remove(Mannequin m, params BodyPartId[] parts)
        {
            foreach (BodyPartId part in parts) m.DamagePart(part, Big, new Vector3(0.3f, 0.2f, -1f).normalized);
        }

        [UnityTest, Category(Category), Timeout(600000)]
        public IEnumerator MannequinStudio()
        {
            Begin("mannequin");
            Layers.Configure();
            BuildStudio();

            Mannequin m = Spawn("Mannequin", out MannequinLocomotion loco);
            yield return Frames(4);
            Session.CheckScene("mannequin, intact, studio");
            ShootStudio(m.transform, Session.Full ? InspectionCameras.HeroSet() : InspectionCameras.QuickSet(), "intact", "standing pose");
            if (!Session.Full) yield break;

            // One desktop-size picture of the main view.
            InspectionCameras.Place(Cam, InspectionCameras.BoundsOf(m.transform), m.transform.rotation, InspectionCameras.FrontThreeQuarter, ViewDistance.Medium, 16f / 9f);
            Session.Shoot(Cam, "front34", "Medium", "intact", "studio", StudioLighting, false, "standing pose", ScreenshotCapture.DesktopWidth, ScreenshotCapture.DesktopHeight);

            // Holding a rifle, since that is how the player nearly always sees one.
            Weapon rifle = Weapon.Create(WeaponData.Rifle, Root.transform, true);
            rifle.transform.position = m.transform.position + new Vector3(0.3f, 1.0f, 0.3f);
            MannequinCombat combat = m.GetComponent<MannequinCombat>();
            if (combat.TryPickUp(rifle))
            {
                combat.enabled = true;   // lets the arms take up the hold; with no player about it just stands
                yield return Frames(20);
                combat.enabled = false;
                ShootStudio(m.transform, new[]
                {
                    new InspectionCameras.Shot(InspectionCameras.FrontThreeQuarter, ViewDistance.Medium),
                    new InspectionCameras.Shot(InspectionCameras.Right, ViewDistance.Medium),
                    new InspectionCameras.Shot(InspectionCameras.FrontThreeQuarter, ViewDistance.Close),
                }, "armed-rifle", "holding rifle");
            }
            else Session.Skip("mannequin armed-rifle views", "the mannequin would not pick the rifle up");

            // ---- motion, intact
            float aspectRadius = 1.35f;
            System.Func<Vector3> chest = () => m.transform.position + Vector3.up * 0.9f;
            yield return Sequence("idle-side", "intact", chest, aspectRadius, Quaternion.identity, InspectionCameras.Left, 8, 4, "Standing still.");
            loco.Wander = true;
            loco.Destination = new Vector3(0f, 0f, 150f);
            yield return Frames(20);
            yield return Sequence("walk-side", "intact", chest, aspectRadius, Quaternion.identity, InspectionCameras.Left, 16, 2, "Walking in a straight line.");
            yield return Sequence("walk-front34", "intact", chest, aspectRadius, Quaternion.identity, InspectionCameras.FrontThreeQuarter, 16, 2, "Walking in a straight line.");
            loco.Hurry = true;
            yield return Frames(20);
            yield return Sequence("run-side", "intact", chest, aspectRadius, Quaternion.identity, InspectionCameras.Left, 16, 2, "Sprinting in a straight line.");
            // A turn: send it back the way it came.
            loco.Hurry = false;
            loco.Destination = new Vector3(0f, 0f, -150f);
            yield return Sequence("turn-high34", "intact", chest, aspectRadius * 1.3f, Quaternion.identity, InspectionCameras.HighThreeQuarter, 16, 2, "Walking, then told to turn round and walk back.");
            Object.Destroy(m.gameObject);
            Object.Destroy(rifle.gameObject);
            yield return null;

            // ---- a limb coming off, frame by frame
            Mannequin pop = Spawn("Mannequin_LimbPop", out _);
            yield return Frames(4);
            yield return Sequence("limb-pop", "right arm shot off on frame 2", () => pop.transform.position + Vector3.up * 1.1f, 2.2f, Quaternion.identity,
                InspectionCameras.FrontThreeQuarter, 12, 1, "The right upper arm takes a killing hit; arm, forearm and hand leave as one piece.", null,
                i => { if (i == 2) pop.DamagePart(BodyPartId.RightUpperArm, Big, new Vector3(-0.4f, 0.3f, -1f).normalized); });
            ShootStudio(pop.transform, new[] { new InspectionCameras.Shot(InspectionCameras.FrontThreeQuarter, ViewDistance.Close) }, "stump-right-arm", "standing, right arm gone");
            Object.Destroy(pop.gameObject);
            GibManager.DestroyInstance();
            yield return null;

            // ---- each damaged way of moving
            yield return Damaged("one-leg", "Hopping on one leg.", 0.9f, BodyPartId.LeftThigh);
            yield return Damaged("crawl", "Crawling on the arms, no legs.", 0.45f, BodyPartId.LeftThigh, BodyPartId.RightThigh);
            yield return Damaged("wriggle", "Torso wriggle: no legs, no arms.", 0.35f, BodyPartId.LeftThigh, BodyPartId.RightThigh, BodyPartId.LeftUpperArm, BodyPartId.RightUpperArm);
            yield return Damaged("head-only", "Head rolling and hopping: torso gone.", 0.25f, BodyPartId.Torso);
            Session.CheckScene("mannequin, after damage states");
        }

        IEnumerator Damaged(string state, string note, float focusHeight, params BodyPartId[] removed)
        {
            Mannequin m = Spawn("Mannequin_" + state, out MannequinLocomotion loco);
            yield return Frames(3);
            Remove(m, removed);
            yield return Frames(45);   // let the loose parts fly clear and the body settle into its new stance
            ShootStudio(m.transform, new[]
            {
                new InspectionCameras.Shot(InspectionCameras.FrontThreeQuarter, ViewDistance.Medium),
                new InspectionCameras.Shot(InspectionCameras.Left, ViewDistance.Medium),
            }, state, note);
            loco.Wander = true;
            loco.Destination = new Vector3(0f, 0f, 150f);
            yield return Frames(15);
            yield return Sequence(state + "-side", state, () => m.transform.position + Vector3.up * focusHeight, 1.35f, Quaternion.identity, InspectionCameras.Left, 16, 2, note);
            Object.Destroy(m.gameObject);
            GibManager.DestroyInstance();
            yield return null;
        }

        [UnityTest, Category(Category), Timeout(300000)]
        public IEnumerator WeaponsStudio()
        {
            Begin("weapons");
            Layers.Configure();
            BuildStudio(false);
            foreach (WeaponData data in new[] { WeaponData.Rifle, WeaponData.Pistol })
            {
                if (data == null) { Session.Skip("a weapon", "its data asset was not found"); continue; }
                Weapon weapon = Weapon.Create(data, Root.transform, true);
                weapon.Body.isKinematic = true;
                weapon.transform.SetPositionAndRotation(new Vector3(0f, 1.2f, 0f), Quaternion.identity);
                yield return Frames(3);
                Session.CheckScene("weapon " + data.name);
                var shots = Session.Full ? InspectionCameras.BackgroundSet() : InspectionCameras.QuickSet();
                if (Session.Full)
                {
                    shots.Add(new InspectionCameras.Shot(InspectionCameras.Right, ViewDistance.Medium));
                    shots.Add(new InspectionCameras.Shot(InspectionCameras.Front, ViewDistance.Medium));
                    shots.Add(new InspectionCameras.Shot(InspectionCameras.Top, ViewDistance.Medium));
                }
                ShootStudio(weapon.transform, shots, data.name);
                Object.Destroy(weapon.gameObject);
                yield return null;
            }
        }
    }
}
