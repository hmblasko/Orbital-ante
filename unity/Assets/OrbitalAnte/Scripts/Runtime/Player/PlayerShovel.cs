using System;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Weapons;
using UnityEngine;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// The shovel (Matthew, 17 Nov 2026: "give the player the ability to shovel holes and dig in the ground ... cave
    /// around your enemies"). Always carried on the suit, separate from the gun and the melee slot. Hold Dig and the
    /// blade bites the ground in front of the eye, again and again (<see cref="ShovelSwing"/>): dig down for a
    /// foxhole, or straight ahead into a hillside for a tunnel. Each bite is one scoop of <see cref="DigRules"/>
    /// taken out through <see cref="GroundDamage.Shovel"/>; rock, trees, buildings and the ship cannot be dug.
    /// Digging is heard (<see cref="Noisy"/>). The tool is the game's own <see cref="Model"/> (tools/blender/build_shovel.py);
    /// the primitive stand-in is kept for when that file is missing.
    /// </summary>
    public sealed class PlayerShovel : MonoBehaviour
    {
        /// <summary>The model's file in Resources/Models: origin at the grip, +Z along the shaft to the blade, +Y out of the blade's hollow face.</summary>
        public const string Model = "Shovel";
        /// <summary>True when the game's own model is being shown, false for the stand-in. For tests and the debug readout.</summary>
        public bool UsesModel { get; private set; }

        Camera _eye;
        Transform _model;
        readonly ShovelSwing _swing = new ShovelSwing();
        float _lastBite = -10f;

        /// <summary>True when digging is allowed now (on foot outside, alive, not dazed).</summary>
        public Func<bool> CanDig;
        /// <summary>The controller's default Dig button is Roll Left too: true while floating free, when it rolls instead.</summary>
        public Func<bool> FloatingFree;
        /// <summary>True for a moment after each bite: digging is heard (see <see cref="ShovelSwing.HeardFrom"/>).</summary>
        public bool Noisy => Time.time - _lastBite < ShovelSwing.HeardFor;
        /// <summary>Bites that took ground, for tests and the debug readout.</summary>
        public int Bites { get; private set; }
        /// <summary>Holds the Dig button down as if pressed. For tests.</summary>
        public bool ForceHeld;
        /// <summary>Where the digger's feet are (world space), or null when floating: tunnels keep their floor there.</summary>
        public Func<Vector3?> Feet;
        /// <summary>Why the last bite would not go in, for <see cref="NoteSeconds"/> after it; null otherwise. Shown on the suit display.</summary>
        public string Note => Time.time - _refusedAt < NoteSeconds ? ShovelSwing.Words(_refusal) : null;
        const float NoteSeconds = 2.5f;
        DigRefusal _refusal;
        float _refusedAt = -10f;

        public void Initialize(Camera eye)
        {
            _eye = eye;
            BuildModel();
        }

        void Update()
        {
            if (_eye == null) return;
            bool allowed = CanDig == null || CanDig();
            bool held = allowed && (ForceHeld || (GameInput.Dig != null && GameInput.Dig.IsPressed() && !RollsInstead()));
            if (!allowed && _swing.Busy) _swing.Cancel();
            if (_swing.Tick(Time.deltaTime, held)) Bite();
            Pose();
        }

        bool RollsInstead()
        {
            if (FloatingFree == null || !FloatingFree() || GameInput.SuitRollLeft == null) return false;
            var control = GameInput.Dig.activeControl;
            if (control == null) return false;
            var roll = GameInput.SuitRollLeft.controls;
            for (int i = 0; i < roll.Count; i++) if (roll[i] == control) return true;
            return false;
        }

        void Bite()
        {
            Transform eye = _eye.transform;
            if (!Physics.Raycast(eye.position, eye.forward, out RaycastHit hit, ShovelSwing.Reach, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return;
            WeaponFx fx = WeaponFx.Instance;
            ShovelResult result = GroundDamage.Shovel(hit, eye.forward, Feet?.Invoke(), out DigRefusal why);
            if (result == ShovelResult.Dug)
            {
                Bites++;
                _lastBite = Time.time;
                if (fx != null) fx.Sparks(hit.point, hit.normal, 3);
                OrbitalAnte.Audio.AudioDirector.Play("impact.dirt", hit.point);
            }
            else if (result == ShovelResult.Refused)
            {
                // Ground, but it will not give: a dull, low thud (never the ring of rock) and the reason on the visor.
                _lastBite = Time.time;
                _refusal = why;
                if (why != DigRefusal.AlreadyDug) _refusedAt = Time.time;
                OrbitalAnte.Audio.AudioDirector.Play("impact.dirt", hit.point, 0.55f, 0.55f);
            }
            else
            {
                // Rock, a tree, a building, or ground dug as deep as it goes: the blade rings off it.
                if (fx != null) fx.Sparks(hit.point, hit.normal, 6);
                OrbitalAnte.Audio.AudioDirector.Play("impact.ricochet", hit.point);
            }
        }

        // ------------------------------------------------------------------ the stand-in

        void BuildModel()
        {
            var root = new GameObject("Shovel");
            root.layer = gameObject.layer;
            _model = root.transform;
            _model.SetParent(_eye.transform, false);
            UsesModel = OrbitalAnte.Look.ModelLibrary.Build(Model, _model);
            if (UsesModel)
            {
                // Held in front of the eye, like the guns: it casts no shadow of its own.
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else BuildStandIn();
            root.SetActive(false);
        }

        void BuildStandIn()
        {
            Material wood = Mat.Lit(new Color(0.46f, 0.36f, 0.24f), 0.25f);
            Material steel = Mat.Lit(new Color(0.42f, 0.43f, 0.45f), 0.55f);
            Piece(PrimitiveType.Cylinder, wood, new Vector3(0f, 0f, 0.30f), new Vector3(0.032f, 0.30f, 0.032f), Quaternion.Euler(90f, 0f, 0f));
            Piece(PrimitiveType.Cube, wood, new Vector3(0f, 0f, 0.0f), new Vector3(0.11f, 0.03f, 0.03f), Quaternion.identity);
            Piece(PrimitiveType.Cube, steel, new Vector3(0f, 0f, 0.71f), new Vector3(0.20f, 0.02f, 0.24f), Quaternion.Euler(-12f, 0f, 0f));
        }

        void Piece(PrimitiveType type, Material m, Vector3 centre, Vector3 scale, Quaternion turn)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = "StandIn";
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            go.layer = gameObject.layer;
            go.transform.SetParent(_model, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = turn;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static readonly Vector3 HeldBack = new Vector3(0.28f, -0.42f, 0.14f), HeldThrust = new Vector3(0.22f, -0.40f, 0.30f);
        static readonly Vector3 TurnedBack = new Vector3(-8f, -10f, -20f), TurnedThrust = new Vector3(-25f, -14f, -8f);

        void Pose()
        {
            if (_model == null) return;
            bool shown = _swing.Busy;
            if (_model.gameObject.activeSelf != shown) _model.gameObject.SetActive(shown);
            if (!shown) return;
            float f = _swing.Pose;
            // Held low at the right with the grip below the edge of the view (there are no hands to show), the blade's
            // hollow face up; the thrust drives the point at the middle of the view, where the bite lands. The same
            // numbers are in tools/blender/build_shovel.py (HELD_BACK, HELD_THRUST), which draws both ends of the swing.
            _model.localPosition = Vector3.Lerp(HeldBack, HeldThrust, f);
            _model.localRotation = Quaternion.Euler(Vector3.Lerp(TurnedBack, TurnedThrust, f));
        }
    }
}
