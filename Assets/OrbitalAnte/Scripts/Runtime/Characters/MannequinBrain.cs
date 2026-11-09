using OrbitalAnte.Core;
using OrbitalAnte.Player;
using UnityEngine;

namespace OrbitalAnte.Characters
{
    public enum DummyWalkMode
    {
        Stand,
        Wander,
        Approach,
        /// <summary>Approach to a firing distance and shoot at the player.</summary>
        Attack,
    }

    /// <summary>
    /// Test-only movement brain for the playtest dummies: stand, wander, or shuffle toward the
    /// player, using whatever body is left. Real enemy AI arrives with the factions in Phase 2.
    /// </summary>
    [RequireComponent(typeof(Mannequin), typeof(CharacterController))]
    public class MannequinBrain : MonoBehaviour
    {
        public static DummyWalkMode GlobalMode = DummyWalkMode.Stand;

        public int SpawnIndex;
        public Vector3 HomePosition;
        /// <summary>Set each frame by other components (e.g. walk to a dropped gun). Wins over the mode.</summary>
        public Vector3? MoveOverride;

        Mannequin m;
        CharacterController cc;
        ProceduralGait gait;
        SeededRandom rng;
        Vector3 waypoint;
        float verticalSpeed;
        float repathTimer;

        void Awake()
        {
            m = GetComponent<Mannequin>();
            cc = GetComponent<CharacterController>();
            gait = GetComponent<ProceduralGait>();
        }

        void Start()
        {
            HomePosition = transform.position;
            rng = RunSeed.CreateStream("dummy.wander." + SpawnIndex);
            PickWaypoint();
        }

        void Update()
        {
            if (!m.IsAlive) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 desired = Vector3.zero;
            if (MoveOverride.HasValue)
            {
                desired = Flat(MoveOverride.Value - transform.position);
            }
            else switch (GlobalMode)
            {
                case DummyWalkMode.Wander:
                    repathTimer -= dt;
                    Vector3 toWp = Flat(waypoint - transform.position);
                    if (toWp.magnitude < 0.8f || repathTimer <= 0f) PickWaypoint();
                    desired = toWp;
                    break;
                case DummyWalkMode.Approach:
                    var player = FirstPersonController.Instance;
                    if (player != null)
                    {
                        Vector3 toPlayer = Flat(player.transform.position - transform.position);
                        if (toPlayer.magnitude > m.Config.approachStopDistance) desired = toPlayer;
                        else FaceTowards(toPlayer, dt);
                    }
                    break;
                case DummyWalkMode.Attack:
                    var target = FirstPersonController.Instance;
                    if (target != null)
                    {
                        Vector3 toTarget = Flat(target.transform.position - transform.position);
                        float stop = Combat.CombatRulesConfig.Get().dummyAttackStopDistance;
                        if (toTarget.magnitude > stop) desired = toTarget;
                        else FaceTowards(toTarget, dt);
                    }
                    break;
            }

            bool moving = desired.sqrMagnitude > 0.01f;
            gait.SetDesire(moving ? 1f : 0f);
            if (moving) FaceTowards(desired, dt);

            float speed = moving ? m.Config.SpeedFor(m.State) * gait.MovePulse : 0f;
            Vector3 move = transform.forward * speed;

            if (cc.isGrounded && verticalSpeed < 0f) verticalSpeed = -1f;
            verticalSpeed += Physics.gravity.y * dt;
            move.y = verticalSpeed;
            cc.Move(move * dt);
        }

        void FaceTowards(Vector3 flatDir, float dt)
        {
            if (flatDir.sqrMagnitude < 0.0001f) return;
            float turn = m.Config.turnSpeedDegrees;
            if (m.State != LocomotionState.Normal) turn *= 0.5f;
            var target = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turn * dt);
        }

        void PickWaypoint()
        {
            Vector2 p = rng.InsideUnitCircle() * m.Config.wanderRadius;
            waypoint = new Vector3(p.x, HomePosition.y, p.y + 6f);
            repathTimer = 12f;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
