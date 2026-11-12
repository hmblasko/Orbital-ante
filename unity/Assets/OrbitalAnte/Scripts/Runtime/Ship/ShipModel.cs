using System;
using System.Collections.Generic;

namespace OrbitalAnte.Ship
{
    public enum PartKind { Hull, Leg, Engine, Thruster, Dish, Window, Hatch }

    /// <summary>
    /// Sound: working normally. Damaged: working, less well. Broken: not working, but can be mended.
    /// Lost: torn off for good.
    /// </summary>
    public enum PartState { Sound, Damaged, Broken, Lost }

    public sealed class PartSpec
    {
        public string Id;
        public string Name;
        public PartKind Kind;
        public float MaxHealth;
        /// <summary>Whether enough punishment tears it off the ship for good.</summary>
        public bool CanFallOff;
        /// <summary>For legs, engines and thrusters: which one (0 to 3).</summary>
        public int Index;
    }

    public struct DamageResult
    {
        public float Dealt;
        public PartState Before, After;
        /// <summary>Health as a fraction after the blow, 0 to 1.</summary>
        public float Fraction;
        public bool Broke => After == PartState.Broken && Before != PartState.Broken && Before != PartState.Lost;
        public bool FellOff => After == PartState.Lost && Before != PartState.Lost;
        public bool Changed => After != Before;
    }

    /// <summary>The tunable numbers behind ship damage. Held in the ShipConfig asset.</summary>
    [Serializable]
    public sealed class DamageRules
    {
        public float hullHealth = 600f;
        public float legHealth = 220f;
        public float engineHealth = 260f;
        public float thrusterHealth = 120f;
        public float dishHealth = 90f;
        public float windowHealth = 240f;
        public float hatchHealth = 200f;

        /// <summary>A single blow this many times a part's full health tears it straight off.</summary>
        public float tearOffBlow = 1.5f;
        /// <summary>Once a part is broken, this many times its full health in further damage tears it off.</summary>
        public float tearOffAfterBroken = 1f;

        /// <summary>Touching down slower than this (metres per second) does no damage at all.</summary>
        public float safeImpactSpeed = 3f;
        /// <summary>Damage for each (metre per second above the safe speed) squared.</summary>
        public float impactDamage = 6f;

        /// <summary>Seconds to mend a part that is only scratched.</summary>
        public float repairSecondsLeast = 0.8f;
        /// <summary>Seconds to mend a part that is completely broken.</summary>
        public float repairSecondsMost = 2.2f;

        /// <summary>Health fraction below which a part counts as Damaged.</summary>
        public float damagedBelow = 0.66f;
    }

    /// <summary>
    /// The state of the ship's parts: how hurt each one is, what that does to the ship, and mending.
    ///
    /// The rules, which the player can learn and rely on:
    /// - Every part has health. Below two thirds it is Damaged and works less well; at zero it is Broken and
    ///   stops working.
    /// - Anything Damaged or Broken can be mended completely, in a couple of seconds.
    /// - Legs, engines, thrusters and the dish can be torn off for good, by one very heavy blow or by going on
    ///   battering a part that is already broken. Lost parts cannot be mended.
    /// - Windows and the hull never fall off. A broken window or hull means the cabin is no longer sealed.
    /// - Nothing ever leaves you with no thrust at all: broken engines still limp.
    /// No Unity types.
    /// </summary>
    public sealed class ShipModel
    {
        public readonly DamageRules Rules;
        public readonly PartSpec[] Parts;
        readonly float[] _health;
        readonly float[] _overkill;
        readonly bool[] _lost;
        readonly float[] _repair;
        readonly Dictionary<string, int> _byId = new Dictionary<string, int>();

        public int Count => Parts.Length;

        public ShipModel(DamageRules rules = null)
        {
            Rules = rules ?? new DamageRules();
            var parts = new List<PartSpec>();
            parts.Add(new PartSpec { Id = "cabin", Name = "Cabin hull", Kind = PartKind.Hull, MaxHealth = Rules.hullHealth });
            parts.Add(new PartSpec { Id = "base", Name = "Base", Kind = PartKind.Hull, MaxHealth = Rules.hullHealth, Index = 1 });
            string[] corner = { "Front right", "Back right", "Back left", "Front left" };
            for (int i = 0; i < ShipLayout.Legs; i++)
            {
                parts.Add(new PartSpec { Id = "leg_" + i, Name = corner[i] + " leg", Kind = PartKind.Leg, MaxHealth = Rules.legHealth, CanFallOff = true, Index = i });
            }
            parts.Add(new PartSpec { Id = "engines", Name = "Engines", Kind = PartKind.Engine, MaxHealth = Rules.engineHealth, CanFallOff = true });
            for (int i = 0; i < 4; i++)
            {
                parts.Add(new PartSpec { Id = "thruster_" + i, Name = corner[i] + " thrusters", Kind = PartKind.Thruster, MaxHealth = Rules.thrusterHealth, CanFallOff = true, Index = i });
            }
            parts.Add(new PartSpec { Id = "dish", Name = "Dish", Kind = PartKind.Dish, MaxHealth = Rules.dishHealth, CanFallOff = true });
            for (int i = 0; i < ShipLayout.Windows.Length; i++)
            {
                parts.Add(new PartSpec { Id = ShipLayout.Windows[i].Id, Name = ShipLayout.Windows[i].Name, Kind = PartKind.Window, MaxHealth = Rules.windowHealth, Index = i });
            }
            parts.Add(new PartSpec { Id = "hatch", Name = "Hatch", Kind = PartKind.Hatch, MaxHealth = Rules.hatchHealth });

            Parts = parts.ToArray();
            _health = new float[Parts.Length];
            _overkill = new float[Parts.Length];
            _lost = new bool[Parts.Length];
            _repair = new float[Parts.Length];
            for (int i = 0; i < Parts.Length; i++)
            {
                _health[i] = Parts[i].MaxHealth;
                _byId[Parts[i].Id] = i;
            }
        }

        public int IndexOf(string id) => _byId.TryGetValue(id, out int i) ? i : -1;

        public int Find(PartKind kind, int index = 0)
        {
            for (int i = 0; i < Parts.Length; i++)
            {
                if (Parts[i].Kind == kind && Parts[i].Index == index) return i;
            }
            return -1;
        }

        public float Health(int part) => _health[part];
        public float Fraction(int part) => _lost[part] ? 0f : _health[part] / Parts[part].MaxHealth;

        public PartState State(int part)
        {
            if (_lost[part]) return PartState.Lost;
            if (_health[part] <= 0f) return PartState.Broken;
            return Fraction(part) < Rules.damagedBelow ? PartState.Damaged : PartState.Sound;
        }

        /// <summary>True when the part has any damage that mending would put right.</summary>
        public bool NeedsRepair(int part) => !_lost[part] && _health[part] < Parts[part].MaxHealth;

        /// <summary>How cracked a window looks: 0 clean, 1 one crack, 2 cracked, 3 badly cracked, 4 gone.</summary>
        public int CrackStage(int part)
        {
            float f = Fraction(part);
            if (f >= 1f) return 0;
            if (f <= 0f) return 4;
            if (f >= Rules.damagedBelow) return 1;
            return f >= 0.33f ? 2 : 3;
        }

        public DamageResult Damage(int part, float amount)
        {
            var result = new DamageResult { Before = State(part), After = State(part), Fraction = Fraction(part) };
            if (amount <= 0f || _lost[part]) return result;
            PartSpec spec = Parts[part];
            result.Dealt = amount;
            _repair[part] = 0f;

            bool wasBroken = _health[part] <= 0f;
            float spare = amount - _health[part];
            _health[part] = Math.Max(0f, _health[part] - amount);
            if (spec.CanFallOff)
            {
                if (wasBroken) _overkill[part] += amount;
                else if (spare > 0f) _overkill[part] += spare * 0.5f;
                bool oneBlow = amount >= spec.MaxHealth * Rules.tearOffBlow;
                bool battered = _overkill[part] >= spec.MaxHealth * Rules.tearOffAfterBroken;
                if (oneBlow || battered)
                {
                    _lost[part] = true;
                    _health[part] = 0f;
                }
            }
            result.After = State(part);
            result.Fraction = Fraction(part);
            return result;
        }

        /// <summary>Damage from hitting something at a given speed (metres per second, measured at the point of contact).</summary>
        public float ImpactDamage(float speed)
        {
            float over = speed - Rules.safeImpactSpeed;
            return over <= 0f ? 0f : over * over * Rules.impactDamage;
        }

        /// <summary>Seconds of mending this part needs from scratch, given how hurt it is now.</summary>
        public float RepairSeconds(int part)
        {
            float hurt = 1f - Fraction(part);
            return Rules.repairSecondsLeast + (Rules.repairSecondsMost - Rules.repairSecondsLeast) * hurt;
        }

        /// <summary>How far through mending this part is, 0 to 1.</summary>
        public float RepairProgress(int part) => NeedsRepair(part) ? Math.Min(1f, _repair[part] / RepairSeconds(part)) : 0f;

        /// <summary>Mends for a moment. Returns true at the moment the part becomes good as new.</summary>
        public bool Repair(int part, float dt)
        {
            if (!NeedsRepair(part) || dt <= 0f) return false;
            _repair[part] += dt;
            if (_repair[part] < RepairSeconds(part)) return false;
            _health[part] = Parts[part].MaxHealth;
            _overkill[part] = 0f;
            _repair[part] = 0f;
            return true;
        }

        /// <summary>Mending that is left alone slips back, so it cannot be done in tiny dabs from safety.</summary>
        public void RestRepair(int part, float dt)
        {
            if (_repair[part] > 0f) _repair[part] = Math.Max(0f, _repair[part] - dt * 0.5f);
        }

        // ------------------------------------------------------------------ what the damage does

        static float Working(PartState state, float damaged, float broken, float lost)
        {
            switch (state)
            {
                case PartState.Sound: return 1f;
                case PartState.Damaged: return damaged;
                case PartState.Broken: return broken;
                default: return lost;
            }
        }

        /// <summary>How much of full thrust the engines give, never less than 0.3.</summary>
        public float ThrustFactor => Working(State(Find(PartKind.Engine)), 0.8f, 0.45f, 0.3f);

        /// <summary>How much of full turning and sideways push the four thruster clusters give, never less than 0.2.</summary>
        public float TurnFactor
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < 4; i++) total += Working(State(Find(PartKind.Thruster, i)), 0.8f, 0.4f, 0.2f);
                return total / 4f;
            }
        }

        /// <summary>The dish feeds the map. Broken or lost, the map goes dark until it is mended (or for good).</summary>
        public bool DishWorks
        {
            get
            {
                PartState s = State(Find(PartKind.Dish));
                return s == PartState.Sound || s == PartState.Damaged;
            }
        }

        public bool HatchWorks => State(Find(PartKind.Hatch)) != PartState.Broken;

        /// <summary>False when a window or the cabin hull is broken, so the cabin is open to whatever is outside.</summary>
        public bool CabinIntact
        {
            get
            {
                if (State(IndexOf("cabin")) == PartState.Broken) return false;
                for (int i = 0; i < Parts.Length; i++)
                {
                    if (Parts[i].Kind == PartKind.Window && State(i) == PartState.Broken) return false;
                }
                return true;
            }
        }

        public int LegsStanding
        {
            get
            {
                int n = 0;
                for (int i = 0; i < ShipLayout.Legs; i++)
                {
                    if (State(Find(PartKind.Leg, i)) != PartState.Lost) n++;
                }
                return n;
            }
        }

        /// <summary>The worst-off part that can still be mended, or -1 when everything is fine.</summary>
        public int WorstPart()
        {
            int worst = -1;
            float lowest = 1f;
            for (int i = 0; i < Parts.Length; i++)
            {
                if (!NeedsRepair(i)) continue;
                float f = Fraction(i);
                if (f < lowest) { lowest = f; worst = i; }
            }
            return worst;
        }
    }
}
