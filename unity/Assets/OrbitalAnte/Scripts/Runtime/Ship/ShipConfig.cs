using OrbitalAnte.Ship;
using UnityEngine;

namespace OrbitalAnte.Ship
{
    /// <summary>
    /// The starter ship's tunable numbers. Edit the asset at Assets/OrbitalAnte/Resources/Config/ShipConfig;
    /// if it is missing the values written here are used.
    /// </summary>
    public sealed class ShipConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/ShipConfig";

        [Header("Body")]
        [Tooltip("Weight in kilograms. Affects how hard it hits things, not how it flies.")]
        public float mass = 3200f;
        [Range(0f, 1f)] public float bounce = 0.28f;
        [Range(0f, 1.5f)] public float grip = 0.9f;

        [Header("Damage and repair")]
        public DamageRules damage = new DamageRules();
        [Tooltip("How close (metres) you must be to a part to mend it.")]
        public float repairReach = 3.2f;
        [Tooltip("How far away you can mend from outside, in metres. Never less than it takes to reach the thrusters from the ground (Ship.MendReachOf); the suit's jets take you up to the dish.")]
        public float spacewalkRepairReach = 3.5f;
        [Tooltip("How closely (degrees) you must be looking at a part to mend it.")]
        public float repairAim = 22f;

        [Header("Inside the cabin")]
        [Tooltip("The pull of the cabin's own gravity, in metres per second squared. Earth's is 9.8.")]
        public float cabinGravity = 9f;
        public float walkSpeed = 3.2f;
        public float jumpHeight = 0.45f;
        [Tooltip("Field of view of the player's eyes, in degrees.")]
        public float fieldOfView = 80f;

        [Header("Seat")]
        [Tooltip("Seconds to sit down and buckle in.")]
        public float buckleSeconds = 1.1f;
        public float unbuckleSeconds = 0.6f;
        [Tooltip("How far (metres) from the seat you can be to buckle in.")]
        public float seatReach = 1.7f;
        [Tooltip("How far the view dips to watch the belt go across, in degrees.")]
        public float buckleGlance = 34f;

        [Header("Hatch and lift")]
        public float doorSeconds = 0.5f;
        [Tooltip("Speed of the gravity lift, in metres per second. Never slower than a 1.4 second ride up the legs (Ship.LiftSpeedOf).")]
        public float liftSpeed = 2.6f;
        [Tooltip("How far below the belly the lift can reach, in metres.")]
        public float liftReach = 6.5f;
        [Tooltip("How far from the ship (metres) the hatch button on your suit still works.")]
        public float remoteHatchRange = 12f;

        [Header("On foot outside (bare minimum until the suit and jetpack milestone)")]
        public float outsideWalkSpeed = 3.4f;
        [Tooltip("Height of a jump under Earth gravity, in metres. Lower gravity gives higher jumps.")]
        public float outsideJumpHeight = 0.7f;

        [Header("Glass")]
        public Color glassTint = new Color(0.62f, 0.84f, 0.92f, 0.10f);

        [Header("Lighting")]
        [Tooltip("Used only if the baked lighting file is missing or out of date: how carefully to work it out while the game starts. Higher is smoother and slower.")]
        public int fallbackBakePaths = 48;

        [Header("Test equipment")]
        [Tooltip("Gives you the test pistol when you are out of the seat, so the glass can be shot. Not part of the finished game's loadout rules.")]
        public bool testPistol = true;

        [Header("Sound")]
        [Range(0f, 1f)] public float volume = 0.7f;

        [Header("Crashes: what a knock does to the person inside")]
        public OrbitalAnte.Navigation.CrashRules crash = new OrbitalAnte.Navigation.CrashRules();

        static ShipConfig _cached;

        public static ShipConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<ShipConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<ShipConfig>();
                _cached.name = "ShipConfig (defaults)";
            }
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
