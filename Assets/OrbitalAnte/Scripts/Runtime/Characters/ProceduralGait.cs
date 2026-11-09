using UnityEngine;

namespace OrbitalAnte.Characters
{
    /// <summary>
    /// Placeholder procedural animation for every movement state, so the degraded states can
    /// be playtested before real animation clips exist. If an Animator Controller is assigned
    /// later, disable this component and the Humanoid rig takes the clips as normal.
    /// Also tells the brain how hard the body is pushing this frame (hops and crawls surge).
    /// </summary>
    [RequireComponent(typeof(Mannequin))]
    public class ProceduralGait : MonoBehaviour
    {
        [Tooltip("How fast poses blend when the state changes (higher = snappier).")]
        public float poseBlend = 12f;

        Mannequin m;
        MannequinRig rig;
        float phase;
        float desire;
        float headRoll;
        float smoothedDesire;
        /// <summary>0 = arms follow the gait, 1 = aiming upright, 2 = aiming while crawling.</summary>
        int aimState;

        /// <summary>Arm holding a gun points it forward (set by DummyGunner).</summary>
        public bool AimMain;
        /// <summary>0 = left hand holds the gun, 1 = right.</summary>
        public int AimSide = 1;
        /// <summary>The other hand reaches in to support the gun.</summary>
        public bool AimSupport;
        /// <summary>Degrees up (+) or down (-) toward the target.</summary>
        public float AimPitch;

        /// <summary>Speed multiplier for this frame: 1 = steady walk, surges for hops and crawls.</summary>
        public float MovePulse { get; private set; } = 1f;

        void Awake()
        {
            m = GetComponent<Mannequin>();
        }

        void Start() => rig = m.Rig;

        /// <summary>0 = stand still, 1 = trying to move.</summary>
        public void SetDesire(float d) => desire = Mathf.Clamp01(d);

        void LateUpdate()
        {
            if (rig == null || !m.IsAlive) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            smoothedDesire = Mathf.MoveTowards(smoothedDesire, desire, dt * 3f);
            var state = m.State;
            phase += dt * Mathf.PI * 2f * m.Config.CadenceFor(state) * Mathf.Max(smoothedDesire, 0.0f);
            if (phase > 1000f) phase -= Mathf.PI * 200f;
            float k = 1f - Mathf.Exp(-poseBlend * dt);
            float a = smoothedDesire;
            aimState = AimMain ? (state == LocomotionState.Crawl ? 2 : state == LocomotionState.Normal || state == LocomotionState.Limp ? 1 : 0) : 0;

            switch (state)
            {
                case LocomotionState.Normal: Walk(a, k); MovePulse = 1f; ApplyAim(k, false); break;
                case LocomotionState.Limp: Limp(a, k); ApplyAim(k, false); break;
                case LocomotionState.Crawl: Crawl(a, k); ApplyAim(k, true); break;
                case LocomotionState.Wriggle: Wriggle(a, k); break;
                case LocomotionState.HeadOnly: HeadHop(a, k, dt); break;
            }
        }

        // ---- poses -------------------------------------------------------------------------

        void Walk(float a, float k)
        {
            float s = Mathf.Sin(phase);
            float c = Mathf.Cos(phase);
            SetHips(new Vector3(0, rig.HipsRestLocal.y - 0.015f + 0.03f * Mathf.Abs(c) * a, 0), Quaternion.Euler(0, 5f * s * a, 0), k);
            Set(rig.Spine, Quaternion.Euler(3f * a, -4f * s * a, 0), k);
            Set(rig.Chest, Quaternion.identity, k);
            Set(rig.Neck, Quaternion.identity, k);
            Set(rig.Head, Quaternion.identity, k);
            Arm(0, 78f, -24f * s * a, 12f + 10f * a, k);
            Arm(1, 78f, 24f * s * a, 12f + 10f * a, k);
            Leg(0, 28f * s * a, a * (6f + 38f * Mathf.Max(0f, -c)), k);
            Leg(1, -28f * s * a, a * (6f + 38f * Mathf.Max(0f, c)), k);
            ResetFeet(k);
        }

        void Limp(float a, float k)
        {
            // Hop on the good leg, arms out for balance, tilted toward the good side.
            int good = MannequinAnatomy.LegIntact(m.Attached, true) ? 0 : 1;
            float side = good == 0 ? -1f : 1f;
            float hop = Mathf.Max(0f, Mathf.Sin(phase));
            MovePulse = 0.25f + 1.6f * hop;
            SetHips(new Vector3(side * 0.04f, rig.HipsRestLocal.y - 0.06f + 0.13f * hop * a, 0),
                Quaternion.Euler(0, 0, -side * 7f), k);
            Set(rig.Spine, Quaternion.Euler(8f, 0, side * 4f), k);
            Set(rig.Chest, Quaternion.identity, k);
            Set(rig.Neck, Quaternion.identity, k);
            Set(rig.Head, Quaternion.Euler(0, 0, side * 5f), k);
            float flail = Mathf.Sin(phase * 2f) * 18f * a;
            Arm(0, 55f, flail, 25f, k);
            Arm(1, 55f, -flail, 25f, k);
            Leg(good, -14f * Mathf.Sin(phase) * a, 18f + 30f * (1f - hop) * a, k);
            Leg(1 - good, 25f, 40f, k); // the stump side tucks up
            ResetFeet(k);
        }

        void Crawl(float a, float k)
        {
            // Face down, pulling forward with whatever arms are left; legs drag.
            float pullL = 0.5f + 0.5f * Mathf.Sin(phase);
            float pullR = 0.5f + 0.5f * Mathf.Sin(phase + Mathf.PI);
            MovePulse = 0.3f + 1.4f * Mathf.Max(0f, Mathf.Cos(phase * 2f));
            SetHips(new Vector3(0, 0.2f, -0.45f), Quaternion.Euler(84f, 3f * Mathf.Sin(phase) * a, 0), k);
            Set(rig.Spine, Quaternion.Euler(0, 0, 6f * Mathf.Sin(phase) * a), k);
            Set(rig.Chest, Quaternion.identity, k);
            Set(rig.Neck, Quaternion.Euler(-35f, 0, 0), k);
            Set(rig.Head, Quaternion.Euler(-20f, 0, 0), k);
            Arm(0, -80f + 55f * pullL * a, 0f, 15f + 55f * pullL * a, k);
            Arm(1, -80f + 55f * pullR * a, 0f, 15f + 55f * pullR * a, k);
            Leg(0, -4f, 8f, k);
            Leg(1, -4f, 8f, k);
            ResetFeet(k);
        }

        void Wriggle(float a, float k)
        {
            // No arms, no legs: inch forward like a caterpillar.
            float w = Mathf.Sin(phase * 2f);
            MovePulse = 2f * Mathf.Max(0f, w);
            SetHips(new Vector3(0, 0.16f + 0.05f * Mathf.Max(0f, w) * a, -0.45f), Quaternion.Euler(84f - 10f * w * a, 0, 12f * Mathf.Sin(phase) * a), k);
            Set(rig.Spine, Quaternion.Euler(-14f * w * a, 0, -10f * Mathf.Sin(phase + 1f) * a), k);
            Set(rig.Chest, Quaternion.Euler(-8f * w * a, 0, 0), k);
            Set(rig.Neck, Quaternion.Euler(-35f, 0, 0), k);
            Set(rig.Head, Quaternion.Euler(-15f + 10f * w, 0, 0), k);
            ResetFeet(k);
        }

        void HeadHop(float a, float k, float dt)
        {
            // Hips sink below the floor so the head sits on the ground; the head rolls and hops.
            float hop = Mathf.Max(0f, Mathf.Sin(phase));
            MovePulse = 0.2f + 1.8f * hop;
            float speed = m.Config.headHopSpeed * MovePulse * a;
            headRoll += speed * dt / 0.13f * Mathf.Rad2Deg;
            float y = 0.13f - rig.HeadCentreAboveHips + 0.12f * hop * a;
            SetHips(new Vector3(0, y, 0), Quaternion.identity, 1f);
            Set(rig.Spine, Quaternion.identity, 1f);
            Set(rig.Chest, Quaternion.identity, 1f);
            Set(rig.Neck, Quaternion.identity, 1f);
            rig.Head.localRotation = Quaternion.Euler(headRoll, 0, 8f * Mathf.Sin(phase * 0.5f));
        }

        /// <summary>Gun arm points at the target; support arm reaches across to the gun.</summary>
        void ApplyAim(float k, bool prone)
        {
            if (!AimMain) return;
            int main = AimSide;
            float sign = main == 0 ? 1f : -1f;
            if (prone)
            {
                // Lying face down: reach the gun out ahead, overhead in body space.
                Set(rig.UpperArm[main], Quaternion.Euler(0, 0, sign * -82f), k);
                Set(rig.Forearm[main], Quaternion.identity, k);
                Set(rig.Hand[main], Quaternion.identity, k);
                return;
            }
            float pitch = Mathf.Clamp(AimPitch, -60f, 60f);
            Set(rig.UpperArm[main], Quaternion.Euler(-pitch, 0, 0) * Quaternion.Euler(0, sign * 88f, 0), k);
            Set(rig.Forearm[main], Quaternion.identity, k);
            Set(rig.Hand[main], Quaternion.identity, k);
            if (AimSupport)
            {
                int other = 1 - main;
                float os = other == 0 ? 1f : -1f;
                Set(rig.UpperArm[other], Quaternion.Euler(-pitch, 0, 0) * Quaternion.Euler(0, os * 112f, os * 8f), k);
                Set(rig.Forearm[other], Quaternion.Euler(0, os * 38f, 0), k);
                Set(rig.Hand[other], Quaternion.identity, k);
            }
        }

        // ---- helpers -----------------------------------------------------------------------

        void SetHips(Vector3 localPos, Quaternion localRot, float k)
        {
            rig.Hips.localPosition = Vector3.Lerp(rig.Hips.localPosition, localPos, k);
            rig.Hips.localRotation = Quaternion.Slerp(rig.Hips.localRotation, localRot, k);
        }

        static void Set(Transform t, Quaternion target, float k)
        {
            t.localRotation = Quaternion.Slerp(t.localRotation, target, k);
        }

        /// <summary>
        /// down: degrees from T-pose toward the hips (negative reaches overhead).
        /// forward: swing forward (+) or back (-). elbow: forearm bend toward the front.
        /// </summary>
        void Arm(int side, float down, float forward, float elbow, float k)
        {
            // An arm that is aiming a gun is posed by ApplyAim instead; blending both would leave
            // it halfway between the two poses.
            if (aimState != 0 && (side == AimSide || (aimState == 1 && AimSupport))) return;
            float sign = side == 0 ? 1f : -1f;
            Set(rig.UpperArm[side], Quaternion.Euler(-forward, 0, 0) * Quaternion.Euler(0, 0, sign * down), k);
            Set(rig.Forearm[side], Quaternion.Euler(0, sign * elbow, 0), k);
            Set(rig.Hand[side], Quaternion.identity, k);
        }

        /// <summary>forward: thigh swing forward (+). knee: shin bend backward (+).</summary>
        void Leg(int side, float forward, float knee, float k)
        {
            Set(rig.Thigh[side], Quaternion.Euler(-forward, 0, 0), k);
            Set(rig.Shin[side], Quaternion.Euler(knee, 0, 0), k);
        }

        void ResetFeet(float k)
        {
            Set(rig.Foot[0], Quaternion.identity, k);
            Set(rig.Foot[1], Quaternion.identity, k);
        }
    }
}
