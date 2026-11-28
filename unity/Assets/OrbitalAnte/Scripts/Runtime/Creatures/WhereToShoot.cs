using System;
using System.Collections.Generic;
using System.Text;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// Where to shoot each creature, and how the game says it (Ralph loop pass 7, 28 Nov 2026). Every creature has one
    /// weak place that kills it quickest, armour that is the slow way, and pieces it fights with that can be shot off
    /// ("one weak place to find and one strike to read", <c>Tasks.CreatureTiers</c>). Before this pass only a knock and
    /// the blood told them apart, and the hit bell rang the same for all of them, armour included, so the game's reward
    /// went as much to the shots that did least.
    ///
    /// - Each creature says, for every hit, where it landed (<see cref="HitZone"/>, from the one function per kind here),
    ///   and the hit marker and its sound say it back (<c>UI.HitConfirm</c>): armour that held is a small grey X and a dull
    ///   knock with no bell; the weak place is the gold X and the higher bell of a person's head; a claw, a jaw or a drum
    ///   shot off is the cyan X and the bell of a disarm. So the lesson of the people ("the head") carries over as "the
    ///   place that rings gold", whatever that place is on a body that is not a person's.
    /// - The first time a kind's armour turns the player's shots, and only until they have hit its weak place, one short
    ///   line under the crosshair says where to aim (<see cref="AimLine"/>; when, <see cref="AimLines"/>).
    ///
    /// The weak place is always the quickest kill of the pieces that kill (rule test
    /// <c>EveryCreature_HasOneWeakPlace_AndItIsTheQuickestKill</c>).
    /// </summary>
    public static class WhereToShoot
    {
        /// <summary>The first word of every aim line, drawn in the glance's grey so the line and the mark read as one.</summary>
        public const string ArmourWord = "ARMOUR";

        /// <summary>
        /// A Strider or a Sprinter. <paramref name="soft"/> is its soft piece (the Strider's belly, the Sprinter's
        /// abdomen), <paramref name="armoured"/> its armoured one (the Strider's lid, the Sprinter's head).
        /// <paramref name="weaponLost"/>: this hit took off a piece it strikes with (<see cref="WalkerWeapon"/>).
        /// </summary>
        public static HitZone Walker(bool soft, bool armoured, bool weaponLost)
        {
            if (weaponLost) return HitZone.WeaponOff;
            if (soft) return HitZone.Vital;
            return armoured ? HitZone.Armour : HitZone.Plain;
        }

        /// <summary>
        /// True if losing <paramref name="limb"/> takes a piece a walker strikes with: a jaw, for a kind that bites; a claw
        /// arm, for a kind that strikes with claws (the Strider). A Sprinter's arms only feel and warn; its jaws bite.
        /// </summary>
        public static bool WalkerWeapon(WalkerLayout layout, int limb)
        {
            if (limb >= WalkerLayout.Limbs) return true;
            return limb >= 0 && WalkerLayout.IsArm(limb) && layout != null && !layout.HasJaws;
        }

        /// <summary>
        /// A Roller. Its head, plates and tail fan are armour until they break (<paramref name="brokeOff"/>); its soft body
        /// is the weak place. <paramref name="rollLost"/>: this hit left it too few plates to close into a ball, so it can
        /// no longer charge: its weapon is gone.
        /// </summary>
        public static HitZone Roller(RollerPiece piece, bool brokeOff, bool rollLost)
        {
            if (rollLost) return HitZone.WeaponOff;
            switch (piece)
            {
                case RollerPiece.Soft: return HitZone.Vital;
                case RollerPiece.Head: return HitZone.Armour;
                case RollerPiece.Plate:
                case RollerPiece.Tail: return brokeOff ? HitZone.Plain : HitZone.Armour;
                default: return HitZone.Plain;
            }
        }

        /// <summary>A Cave hunter: the armoured head, the soft fore body (the first <see cref="HunterBody.ForeSegs"/> segments).</summary>
        public static HitZone Hunter(HunterPiece piece, int index)
        {
            if (piece == HunterPiece.Head) return HitZone.Armour;
            return piece == HunterPiece.Seg && index < HunterBody.ForeSegs ? HitZone.Vital : HitZone.Plain;
        }

        /// <summary>A Screamer: the armoured fore body, the soft abdomen, and the drums it screams with.</summary>
        public static HitZone Screamer(ScreamerPiece piece, bool brokeOff)
        {
            switch (piece)
            {
                case ScreamerPiece.Body: return HitZone.Armour;
                case ScreamerPiece.Abdomen: return HitZone.Vital;
                case ScreamerPiece.Drum: return brokeOff ? HitZone.WeaponOff : HitZone.Plain;
                default: return HitZone.Plain;
            }
        }

        /// <summary>An Ambusher: the armoured head and jaws (a jaw is armour until it breaks, and then a weapon lost), the body.</summary>
        public static HitZone Ambusher(AmbusherPiece piece, bool brokeOff)
        {
            switch (piece)
            {
                case AmbusherPiece.Head: return HitZone.Armour;
                case AmbusherPiece.Jaw: return brokeOff ? HitZone.WeaponOff : HitZone.Armour;
                case AmbusherPiece.Seg: return HitZone.Vital;
                default: return HitZone.Plain;
            }
        }

        /// <summary>A Blind stalker: the skull knocks; the trunk (not the tail) is the weak place.</summary>
        public static HitZone Stalker(StalkerPiece piece, int index)
        {
            if (piece == StalkerPiece.Head) return HitZone.Armour;
            return piece == StalkerPiece.Seg && !StalkerLayout.IsTail(index) ? HitZone.Vital : HitZone.Plain;
        }

        /// <summary>
        /// A Drifting maw: the skull knocks; the way down its open throat is the weak place (its body takes the throat's
        /// hits at <see cref="MawBody.ThroatTimes"/>); the rest of its body bleeds but is the long way; the jaw bites.
        /// </summary>
        public static HitZone Maw(MawPiece piece, bool brokeOff)
        {
            switch (piece)
            {
                case MawPiece.Head: return HitZone.Armour;
                case MawPiece.Throat: return HitZone.Vital;
                case MawPiece.Jaw: return brokeOff ? HitZone.WeaponOff : HitZone.Plain;
                default: return HitZone.Plain;
            }
        }

        /// <summary>A Pack biter: no armour; the head is the weak place; the jaw bites.</summary>
        public static HitZone Biter(BiterPiece piece, bool brokeOff)
        {
            switch (piece)
            {
                case BiterPiece.Head: return HitZone.Vital;
                case BiterPiece.Jaw: return brokeOff ? HitZone.WeaponOff : HitZone.Plain;
                default: return HitZone.Plain;
            }
        }

        /// <summary>
        /// Where to aim instead, said once when a kind's armour turns the player's shots: in the words of what can be seen
        /// (its underside, the end behind, the open throat), short enough to read in the second it is up. Null for a kind
        /// with no armour (the Pack biter) or none written yet, which then says nothing.
        /// </summary>
        public static string AimLine(string kind)
        {
            switch (kind)
            {
                case "Strider": return "AIM UNDER IT, AT THE BELLY";
                case "Sprinter": return "AIM FOR THE ABDOMEN BEHIND";
                case "Roller": return "SHOOT UNDERNEATH WHEN IT FLIPS";
                case "Hunter": return "AIM JUST BEHIND THE HEAD";
                case "Screamer": return "AIM AT THE ABDOMEN OR SIDE DRUMS";
                case "Ambusher": return "AIM FOR THE BODY BEHIND THE HEAD";
                case "Stalker": return "AIM FOR THE BODY, NOT THE HEAD";
                case "Maw": return "SHOOT DOWN ITS THROAT WHEN IT OPENS";
                default: return null;
            }
        }

        /// <summary>The whole line as shown: the armour word, a dot, and where to aim.</summary>
        public static string Line(string kind)
        {
            string aim = AimLine(kind);
            return aim == null ? null : ArmourWord + " · " + aim;
        }
    }

    /// <summary>
    /// When to say where to aim at a creature (<see cref="WhereToShoot.Line"/>), and what to remember of it per profile.
    /// Plain C#: the hit marker hands it hits and keeps its memory in PlayerPrefs.
    ///
    /// - Only when a shot glances off that kind's armour: the line answers what the player just did, never a creature
    ///   merely seen.
    /// - Never once the player has hit that kind's weak place (they know it), however they found it.
    /// - At most <see cref="MostTold"/> times a kind, ever, and never within <see cref="Gap"/> seconds of the last line of
    ///   any kind, so a fight against armour is never a stream of words.
    /// - Not at all with the new-player tips switched off (the hit marker asks <c>FeelSettings.GuideTips</c>), and all of
    ///   it forgotten when the player asks for the tips again.
    /// </summary>
    public sealed class AimLines
    {
        /// <summary>Times a kind's line can ever be shown: a second chance for a line missed in the noise of a fight.</summary>
        public const int MostTold = 2;
        /// <summary>Seconds between two lines of any kind.</summary>
        public const float Gap = 40f;
        /// <summary>Seconds a line is up: held at full, then a short fade.</summary>
        public const float Seconds = 3.2f;
        /// <summary>Of <see cref="Seconds"/>, the last this many fade out.</summary>
        public const float FadeSeconds = 0.5f;

        readonly Dictionary<string, int> _told = new Dictionary<string, int>();
        readonly HashSet<string> _found = new HashSet<string>();
        float _lastTold = float.NegativeInfinity;

        /// <summary>
        /// A shot glanced off <paramref name="kind"/>'s armour at <paramref name="now"/> (seconds, any steady clock). True
        /// if its line should show now, which counts it as told; false otherwise, and then nothing changes.
        /// </summary>
        public bool Glanced(string kind, float now)
        {
            if (string.IsNullOrEmpty(kind) || WhereToShoot.AimLine(kind) == null || _found.Contains(kind)) return false;
            if (Told(kind) >= MostTold) return false;
            if (now - _lastTold < Gap) return false;
            _told[kind] = Told(kind) + 1;
            _lastTold = now;
            return true;
        }

        /// <summary>The player hit <paramref name="kind"/>'s weak place. True if that is news (worth remembering).</summary>
        public bool Found(string kind) => !string.IsNullOrEmpty(kind) && _found.Add(kind);

        public bool HasFound(string kind) => kind != null && _found.Contains(kind);

        public int Told(string kind) => kind != null && _told.TryGetValue(kind, out int n) ? n : 0;

        /// <summary>How much of a line shown <paramref name="age"/> seconds ago is seen, 1 to 0.</summary>
        public static float Alpha(float age)
        {
            if (!(age >= 0f) || age >= Seconds) return 0f;
            float fadeFrom = Seconds - FadeSeconds;
            return age <= fadeFrom ? 1f : 1f - (age - fadeFrom) / FadeSeconds;
        }

        /// <summary>The memory as one line of text for PlayerPrefs: "Strider=1,Roller=found".</summary>
        public string Store()
        {
            var kinds = new List<string>();
            foreach (string k in _told.Keys) if (!kinds.Contains(k)) kinds.Add(k);
            foreach (string k in _found) if (!kinds.Contains(k)) kinds.Add(k);
            kinds.Sort(StringComparer.Ordinal);
            var text = new StringBuilder();
            foreach (string k in kinds)
            {
                if (text.Length > 0) text.Append(',');
                text.Append(k).Append('=').Append(_found.Contains(k) ? "found" : Told(k).ToString());
            }
            return text.ToString();
        }

        /// <summary>Reads <see cref="Store"/>'s text back; anything it cannot read is left out (the line just shows again).</summary>
        public static AimLines Parse(string stored)
        {
            var lines = new AimLines();
            if (string.IsNullOrEmpty(stored)) return lines;
            foreach (string entry in stored.Split(','))
            {
                int eq = entry.IndexOf('=');
                if (eq <= 0 || eq == entry.Length - 1) continue;
                string kind = entry.Substring(0, eq).Trim(), value = entry.Substring(eq + 1).Trim();
                if (kind.Length == 0) continue;
                if (value == "found") lines._found.Add(kind);
                else if (int.TryParse(value, out int n) && n > 0) lines._told[kind] = Math.Min(n, MostTold);
            }
            return lines;
        }
    }
}
