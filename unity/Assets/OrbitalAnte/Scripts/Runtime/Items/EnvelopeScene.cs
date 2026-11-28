using System;
using System.Collections.Generic;
using System.Text;

namespace OrbitalAnte.Items
{
    /// <summary>Which of the two envelope screens is showing.</summary>
    public enum EnvelopeMode
    {
        /// <summary>The start of a run: the sealed envelope, what it takes to open it, and the odds.</summary>
        RunStart = 0,
        /// <summary>Ante 9 cleared (or a kept envelope opened from the menu): the seal breaks and the prize comes out.</summary>
        Reveal = 1,
    }

    public enum DrawKind { Image = 0, Text = 1, Group = 2 }

    public enum TextAlign { Left = 0, Centre = 1, Right = 2 }

    /// <summary>
    /// One thing to draw this frame, in the 1280 x 800 reference space with y DOWN and angles CLOCKWISE (screen
    /// terms). (<see cref="x"/>, <see cref="y"/>) is where the item's pivot goes; the pivot (<see cref="px"/>,
    /// <see cref="py"/>) is measured across the item from its top left (0 to 1). Scale and rotation are about the
    /// pivot. An item with a <see cref="parent"/> is placed in that group's own space instead (its top left is 0,0),
    /// and a group with <see cref="clip"/> hides whatever of its children falls outside it.
    /// </summary>
    public struct DrawItem
    {
        public string key;
        public DrawKind kind;
        public string parent;
        public bool clip;
        public string texture;
        public string text;
        public float x, y, w, h, px, py, sx, sy, rotation;
        public float r, g, b, a;
        public int fontSize;
        public bool bold, italic;
        public TextAlign align;
    }

    /// <summary>A sound to play when the reveal reaches a moment.</summary>
    public struct EnvelopeCue
    {
        public float time;
        public string eventId;
        /// <summary>Played instead when <see cref="eventId"/> has no sound yet.</summary>
        public string fallbackId;
        public float pitch;
        public float volume;
    }

    /// <summary>Everything the envelope screen needs to know. Words are filled in by the caller.</summary>
    public sealed class EnvelopeShow
    {
        public EnvelopeMode mode;
        public int serial;
        /// <summary>Stars lit round the seal at the start of the run (0 to 9).</summary>
        public int antesCleared;
        public string oddsLine = "";
        public string prompt = "";

        // The prize, for a reveal.
        public ItemRarity rarity;
        public string cardTexture = "";
        public string itemName = "";
        public string kindLine = "";
        public string blurb = "";
        public string upLine = "";
        public string downLine = "";
        public string effectName = "";
        public float[] effectColour;
        public int ownedCount = 1;
        public bool fromTest;
        /// <summary>Where the prize went into use, or how to put it to use (<c>Locker.RevealWords</c>). Empty for a painting.</summary>
        public string useLine = "";
        /// <summary>True when the prize went into use: the use line is drawn in the Collection's IN USE green.</summary>
        public bool inUse;
        /// <summary>Said after the effect's name in brackets: where the effect is seen in the game (<c>Locker.EffectWhere</c>).</summary>
        public string effectNote = "";
        /// <summary>The Celestial effect's id: its own beads of light run round the prize card (<c>CelestialPlate</c>).</summary>
        public string effectId = "";
    }

    /// <summary>
    /// The envelope screens as a list of things to draw at any moment: where every piece of the envelope, the seal,
    /// the card, the light and the words are. Plain C#: <c>EnvelopeScreen</c> in the game and
    /// <c>tools/envelope_view</c> offline both draw exactly this list, so the offline pictures show the real
    /// layout and timing. Nothing here is random: sparks come from fixed hashes, so a frame is the same every time.
    /// </summary>
    public static class EnvelopeScene
    {
        public const float ScreenW = 1280f, ScreenH = 800f;

        public const string TexLining = "Lining", TexPocket = "Pocket", TexFlapOut = "FlapOut", TexFlapIn = "FlapIn";
        public const string TexSeal = "Seal", TexSealLeft = "SealLeft", TexSealRight = "SealRight", TexCrack = "Crack";
        public const string TexRays = "Rays", TexGlint = "Glint", TexSheen = "Sheen", TexBackdrop = "Backdrop", TexSolid = "Solid", TexShadow = "Shadow";
        public const string TexBead = "Bead";
        /// <summary>An effect line on the reveal longer than this many letters is drawn a size smaller, so it stays on one line.</summary>
        public const int EffectLineLong = 44;

        /// <summary>How long the envelope takes to rise into view.</summary>
        public const float RiseTime = 0.9f;

        static readonly float[] Gold = { 0.98f, 0.86f, 0.55f };
        static readonly float[] Cream = { 0.96f, 0.92f, 0.82f };
        static readonly float[] Muted = { 0.70f, 0.68f, 0.72f };
        /// <summary>HullKit.Good: the colour that says a prize is in use, here and in the Collection.</summary>
        static readonly float[] InUse = { 0.624f, 0.878f, 0.478f };

        /// <summary>The moments of a reveal, in seconds from the start. Rarer prizes build up longer.</summary>
        public struct Beats
        {
            public float starsStart, starStep, shakeStart, crackStart, splitStart, flapStart, flapEnd;
            public float riseStart, riseEnd, moveStart, moveEnd, burst, textStart, textEnd;

            /// <summary>When the player may skip to: the prize is already theirs, so skipping only shortens the show.</summary>
            public float SkipTo => textStart;
            public float Done => textEnd;
        }

        public static Beats BeatsFor(ItemRarity rarity)
        {
            var b = new Beats();
            b.starsStart = 1.0f;
            b.starStep = 0.12f;
            b.shakeStart = b.starsStart + 9 * b.starStep + 0.15f;
            float anticipation = 1.0f + 0.25f * Math.Max(0, (int)rarity - 1);
            b.crackStart = b.shakeStart + anticipation;
            b.splitStart = b.crackStart + 0.40f;
            b.flapStart = b.splitStart + 0.25f;
            b.flapEnd = b.flapStart + 0.65f;
            b.riseStart = b.flapEnd - 0.10f;
            b.riseEnd = b.riseStart + 1.10f;
            b.moveStart = b.riseEnd;
            b.moveEnd = b.moveStart + 0.85f;
            b.burst = b.moveStart + 0.20f;
            b.textStart = b.moveEnd - 0.20f;
            b.textEnd = b.textStart + 0.90f;
            return b;
        }

        /// <summary>The sounds of a reveal. Envelope sounds fall back to the existing reward sounds until they are made.</summary>
        public static List<EnvelopeCue> Cues(EnvelopeShow show)
        {
            var cues = new List<EnvelopeCue>();
            if (show.mode == EnvelopeMode.RunStart)
            {
                cues.Add(new EnvelopeCue { time = 0.15f, eventId = "envelope.present", fallbackId = "reward.discovery", pitch = 1f, volume = 0.6f });
                return cues;
            }
            Beats b = BeatsFor(show.rarity);
            for (int i = 0; i < 9; i++)
                cues.Add(new EnvelopeCue { time = b.starsStart + i * b.starStep, eventId = "envelope.star", fallbackId = "reward.points.tick",
                    pitch = (float)Math.Pow(2.0, i / 12.0 * 2.0), volume = 0.8f });
            cues.Add(new EnvelopeCue { time = b.shakeStart, eventId = "envelope.rattle", fallbackId = "", pitch = 1f, volume = 1f });
            cues.Add(new EnvelopeCue { time = b.crackStart, eventId = "envelope.seal_crack", fallbackId = "ui.select", pitch = 0.7f, volume = 1f });
            cues.Add(new EnvelopeCue { time = b.splitStart, eventId = "envelope.seal_break", fallbackId = "ui.back", pitch = 0.8f, volume = 1f });
            cues.Add(new EnvelopeCue { time = b.flapStart, eventId = "envelope.flap", fallbackId = "", pitch = 1f, volume = 1f });
            cues.Add(new EnvelopeCue { time = b.riseStart, eventId = "envelope.card_slide", fallbackId = "", pitch = 1f, volume = 1f });
            cues.Add(new EnvelopeCue { time = b.burst, eventId = "envelope.reveal." + ItemLook.RarityName(show.rarity).ToLowerInvariant(),
                fallbackId = RewardSoundFor(show.rarity), pitch = 1f, volume = 1f });
            return cues;
        }

        /// <summary>The existing reward sound that best fits a rarity (made by the audio tools).</summary>
        public static string RewardSoundFor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return "reward.loot.common";
                case ItemRarity.Uncommon: return "reward.tier2";
                case ItemRarity.Rare: return "reward.loot.rare";
                case ItemRarity.Exotic: return "reward.tier3";
                default: return "reward.tier4";
            }
        }

        /// <summary>"Common 40%   Uncommon 30% ..." with each name in its rarity's colour (rich text).</summary>
        public static string OddsLine(PrizeOdds odds)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < ItemLook.RarityCount; i++)
            {
                var r = (ItemRarity)i;
                if (i > 0) sb.Append("   ");
                sb.Append("<color=").Append(ItemLook.RarityHex(r)).Append('>').Append(ItemLook.RarityName(r)).Append(' ')
                  .Append(odds.PercentText(r)).Append("</color>");
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the frame

        struct Env
        {
            public float x, y, rot, scale, alpha;
        }

        /// <summary>Fills <paramref name="items"/> with what to draw at <paramref name="t"/> seconds into the screen.</summary>
        public static void Frame(EnvelopeShow show, float t, List<DrawItem> items)
        {
            items.Clear();
            if (show.mode == EnvelopeMode.RunStart) RunStartFrame(show, t, items);
            else RevealFrame(show, t, items);
        }

        static void RunStartFrame(EnvelopeShow show, float t, List<DrawItem> items)
        {
            float rise = Clamp01(t / RiseTime);
            var env = new Env
            {
                x = 640f,
                y = 372f + 170f * (1f - EaseOutBack(rise)) + 5f * (float)Math.Sin(t * 1.3f),
                rot = 0.5f * (float)Math.Sin(t * 0.9f) + 3f * (1f - EaseOut(rise)),
                scale = 1f,
                alpha = EaseOut(Clamp01(t / 0.5f)),
            };
            Backdrop(items, 0.92f * Clamp01(t / 0.45f));
            EnvelopeBody(items, env, flap: 1f, show.antesCleared, sealSplit: 0f, crack: 0f, sealHalo: 0f, cardOffset: float.NaN, show, cardInFront: false);
            Sheen(items, env, ((t - 0.6f) % 3.8f + 3.8f) % 3.8f / 1.15f, 0.20f);
            Glints(items, env, t, 1f);
            SerialNumber(items, env, show.serial);

            float words = Smooth(0.35f, 0.95f, t);
            Text(items, "header", "THIS RUN'S ENVELOPE", 640f, 96f, 900f, 30f, 18, Muted, words, bold: true);
            Text(items, "rule", "Opens when you clear Ante 9. A run that ends sooner leaves it sealed.", 640f, 650f, 1100f, 40f, 24, Cream, Smooth(0.6f, 1.2f, t), bold: true);
            Text(items, "odds", show.oddsLine, 640f, 694f, 1200f, 28f, 17, Cream, Smooth(0.8f, 1.4f, t));
            Text(items, "prompt", show.prompt, 640f, 748f, 1000f, 26f, 16, Muted,
                Smooth(1.2f, 1.8f, t) * (0.6f + 0.3f * (float)Math.Sin(t * 3f)));
        }

        static void RevealFrame(EnvelopeShow show, float t, List<DrawItem> items)
        {
            Beats b = BeatsFor(show.rarity);
            int rank = (int)show.rarity;
            float[] rc = ItemLook.RarityColour(show.rarity);
            float[] light = Mix(rc, new[] { 1f, 1f, 1f }, 0.35f);

            float rise = Clamp01(t / RiseTime);
            float drop = 310f * EaseInOut(Clamp01((t - b.riseStart) / (b.riseEnd - b.riseStart)))
                         + 260f * EaseIn(Clamp01((t - b.moveStart) / (b.moveEnd - b.moveStart)));
            var env = new Env
            {
                x = 640f,
                y = 400f + 170f * (1f - EaseOutBack(rise)) + drop + 4f * (float)Math.Sin(t * 1.3f) * (1f - Clamp01((t - b.shakeStart) * 2f)),
                rot = 3f * (1f - EaseOut(rise)),
                scale = 1f,
                alpha = EaseOut(Clamp01(t / 0.5f)) * (1f - Clamp01((t - b.moveStart - 0.3f) / 0.5f)),
            };
            // The rattle: grows as the seal strains, and stops as it breaks.
            float strain = Clamp01((t - b.shakeStart) / (b.crackStart + 0.3f - b.shakeStart));
            float shaking = t > b.shakeStart && t < b.splitStart + 0.15f ? strain * strain * (3f + 1.4f * rank) : 0f;
            env.x += shaking * ((float)Math.Sin(t * 53f) + 0.5f * (float)Math.Sin(t * 91f + 1f)) / 1.5f;
            env.y += shaking * 0.6f * (float)Math.Sin(t * 67f + 2f);
            env.rot += shaking * 0.25f * (float)Math.Sin(t * 41f);

            float flapU = Clamp01((t - b.flapStart) / (b.flapEnd - b.flapStart));
            float flap = (float)Math.Cos(Math.PI * EaseInOut(flapU));
            float crack = t < b.crackStart ? 0f : Clamp01((t - b.crackStart) / 0.25f) * (0.85f + 0.15f * (float)Math.Sin(t * 60f)) * (1f - Clamp01((t - b.splitStart) / 0.2f));
            float split = Clamp01((t - b.splitStart) / 0.6f);
            int lit = 0;
            for (int i = 0; i < 9; i++) if (t >= b.starsStart + i * b.starStep) lit = i + 1;

            // The card: inside the envelope, rising out of it, then free in front.
            float riseU = EaseInOut(Clamp01((t - b.riseStart) / (b.riseEnd - b.riseStart)));
            float moveU = EaseInOut(Clamp01((t - b.moveStart) / (b.moveEnd - b.moveStart)));
            bool cardFree = t >= b.moveStart;

            float raysAlpha = (0.30f + 0.06f * rank) * Smooth(b.riseStart, b.riseEnd, t);
            float flashT = t - b.burst;
            float flash = flashT > 0f ? (0.16f + 0.06f * rank) * (float)Math.Exp(-flashT * 5.5f) : 0f;

            Backdrop(items, 0.92f * Clamp01(t / 0.45f) + 0.05f * Smooth(b.moveStart, b.moveEnd, t));

            // Where the card is, in the screen.
            Local(env, EnvelopeArt.Width * 0.5f, 38f + EnvelopeArt.CardHeight * 0.5f - 450f * riseU, out float inX, out float inY);
            float cx = inX + (430f - inX) * moveU, cy = inY + (400f - inY) * moveU;
            float cScale = 1f + 0.25f * moveU;
            float cRot = cardFree ? -4f * (1f - moveU) * (float)Math.Cos(moveU * 6f) : env.rot;
            float idle = Math.Max(0f, t - b.moveEnd);
            cy += 5f * (float)Math.Sin(idle * 1.4f) * Smooth(0f, 0.6f, idle);
            cRot += 0.8f * (float)Math.Sin(idle * 0.8f) * Smooth(0f, 0.6f, idle);

            if (raysAlpha > 0.001f)
            {
                Image(items, "rays", TexRays, cx, cy, 760f, 760f, cScale, cScale, t * 8f, light, raysAlpha + flash * 0.6f);
                Image(items, "rays2", TexRays, cx, cy, 560f, 560f, cScale, cScale, -t * 5f + 11f, light, raysAlpha * 0.6f);
            }

            float sealHalo = t > b.shakeStart && t < b.splitStart + 0.3f ? 0.45f * strain * (1f - Clamp01((t - b.splitStart) / 0.3f)) : 0f;
            EnvelopeBody(items, env, flap, lit, split, crack, sealHalo, cardFree ? float.NaN : riseU, show, cardInFront: riseU > 0.97f, rc);
            if (!cardFree) Card(items, show, inX, inY, env.scale, env.rot, env.alpha, t, b, false);
            if (t < b.shakeStart) Sheen(items, env, (t - 0.5f) / 1.1f, 0.20f);
            Glints(items, env, t, 1f - Clamp01((t - b.crackStart) / 0.4f));
            SerialNumber(items, env, show.serial);

            if (cardFree)
            {
                Card(items, show, cx, cy, cScale, cRot, 1f, t, b, true);
                Sparks(items, show, cx, cy, t - b.burst, rank, rc);
                if (show.rarity == ItemRarity.Celestial) EffectBeads(items, show, t - b.burst);
            }
            if (flash > 0.002f) Image(items, "flash", TexSolid, 640f, 400f, ScreenW, ScreenH, 1f, 1f, 0f, Mix(rc, new[] { 1f, 1f, 1f }, 0.5f), flash);

            // Words.
            float head = Smooth(0.3f, 0.9f, t) * (1f - Smooth(b.riseStart, b.riseStart + 0.4f, t));
            Text(items, "header", show.fromTest ? "TEST ENVELOPE" : "ANTE 9 CLEARED", 640f, 96f, 900f, 34f, 22, Gold, head, bold: true);
            if (t >= b.textStart - 0.01f)
            {
                float s0 = t - b.textStart;
                RevealLine(items, "rarity", ItemLook.RarityName(show.rarity).ToUpperInvariant(), 205f, 46f, 26, rc, s0, 0, bold: true);
                RevealLine(items, "name", show.itemName, 262f, 104f, 40, Cream, s0, 1, bold: true);
                RevealLine(items, "kind", show.kindLine, 330f, 30f, 20, Muted, s0, 2);
                float y = 370f;
                if (!string.IsNullOrEmpty(show.effectName))
                {
                    string note = string.IsNullOrEmpty(show.effectNote) ? "" : " (" + show.effectNote + ")";
                    string line = "Effect: " + show.effectName + note;
                    // One line, never two (a second would run into the blurb): the longest (49 letters) is about 520 of
                    // the 540 pixels at 22, so a long one drops a size.
                    RevealLine(items, "effect", line, y, 30f, line.Length > EffectLineLong ? 20 : 22, show.effectColour ?? light, s0, 3, bold: true);
                    y += 40f;
                }
                RevealLine(items, "blurb", show.blurb, y + 8f, 58f, 20, Cream, s0, 4, italic: true);
                y += 76f;
                if (!string.IsNullOrEmpty(show.upLine))
                {
                    RevealLine(items, "up", show.upLine, y, 28f, 20, new[] { 0.60f, 0.92f, 0.60f }, s0, 5, bold: true);
                    RevealLine(items, "down", show.downLine, y + 30f, 28f, 20, new[] { 1.00f, 0.72f, 0.38f }, s0, 5, bold: true);
                    y += 66f;
                }
                string owned = show.ownedCount > 1 ? "You now have " + show.ownedCount + " of these." : "Added to your collection.";
                if (show.fromTest) owned += " (Test item: can be cleared from the collection.)";
                RevealLine(items, "owned", owned, y + 14f, 50f, 18, Muted, s0, 6);
                // What the prize does now: green when it went to work by itself (the Collection's IN USE colour).
                RevealLine(items, "use", show.useLine, y + 66f, 50f, 18, show.inUse ? InUse : Cream, s0, 7);
                Text(items, "prompt", show.prompt, 640f, 748f, 1000f, 26f, 16, Muted,
                    Smooth(0.6f, 1.2f, s0) * (0.6f + 0.3f * (float)Math.Sin(t * 3f)));
            }
        }

        // ------------------------------------------------------------------ pieces

        static void Backdrop(List<DrawItem> items, float alpha)
        {
            Image(items, "backdrop", TexBackdrop, 640f, 400f, ScreenW, ScreenH, 1f, 1f, 0f, new[] { 1f, 1f, 1f }, Clamp01(alpha));
        }

        /// <summary>
        /// The envelope itself. <paramref name="flap"/> is 1 closed, 0 edge on, -1 folded fully open.
        /// <paramref name="cardOffset"/> is NaN when the card is not in the envelope (drawn elsewhere).
        /// </summary>
        static void EnvelopeBody(List<DrawItem> items, Env env, float flap, int litStars, float sealSplit, float crack, float sealHalo,
            float cardOffset, EnvelopeShow show, bool cardInFront, float[] crackColour = null)
        {
            float a = env.alpha;
            float[] white = { 1f, 1f, 1f };
            bool flapOpen = flap < 0f;
            // The shadow texture's rectangle is 3/4 of its width and 5/8 of its height; stretched to match the envelope.
            Piece(items, "shadow", TexShadow, env, EnvelopeArt.Width * 0.5f + 10f, EnvelopeArt.Height * 0.5f + 18f,
                EnvelopeArt.Width * 240f / 180f, EnvelopeArt.Height * 160f / 100f, 0.5f, 0.5f, 1f, 1f, 0f, white, 0.8f * a);
            float flapTone = flapOpen ? 0.62f + 0.38f * -flap : 0.70f + 0.30f * flap;
            float flapWide = 1f + 0.035f * (1f - Math.Abs(flap));
            if (flapOpen) Piece(items, "flap", TexFlapIn, env, EnvelopeArt.Width * 0.5f, 0f, EnvelopeArt.Width, EnvelopeArt.FlapTextureHeight, 0.5f, 0f, flapWide, flap, 0f, Grey(flapTone), a);
            Piece(items, "lining", TexLining, env, EnvelopeArt.Width * 0.5f, EnvelopeArt.Height * 0.5f, EnvelopeArt.Width, EnvelopeArt.Height, 0.5f, 0.5f, 1f, 1f, 0f, white, a);
            // The card waits inside, between the lining and the pocket (it is added by the caller in the right place).
            if (!float.IsNaN(cardOffset) && !cardInFront) CardSlot(items);
            Piece(items, "pocket", TexPocket, env, EnvelopeArt.Width * 0.5f, EnvelopeArt.Height * 0.5f, EnvelopeArt.Width, EnvelopeArt.Height, 0.5f, 0.5f, 1f, 1f, 0f, white, a);
            if (!float.IsNaN(cardOffset) && cardInFront) CardSlot(items);
            if (!flapOpen) Piece(items, "flap", TexFlapOut, env, EnvelopeArt.Width * 0.5f, 0f, EnvelopeArt.Width, EnvelopeArt.FlapTextureHeight, 0.5f, 0f, flapWide, flap, 0f, Grey(flapTone), a);

            // The seal rides the flap's tip until it breaks; then its halves fall away.
            float sx = EnvelopeArt.SealX, sy = EnvelopeArt.SealY;
            if (sealHalo > 0.001f)
                Piece(items, "halo", TexRays, env, sx, sy, 300f, 300f, 0.5f, 0.5f, 1f, 1f, 0f, new[] { 1f, 0.85f, 0.6f }, sealHalo * a);
            if (sealSplit <= 0f)
            {
                Piece(items, "seal", TexSeal, env, sx, sy, EnvelopeArt.SealSize, EnvelopeArt.SealSize, 0.5f, 0.5f, 1f, 1f, 0f, white, a);
            }
            else if (sealSplit < 1f)
            {
                float u = sealSplit;
                float fade = a * (1f - (float)Math.Pow(u, 1.5));
                Piece(items, "sealL", TexSealLeft, env, sx - 10f - 50f * u, sy + 20f * u + 190f * u * u, EnvelopeArt.SealSize, EnvelopeArt.SealSize, 0.5f, 0.5f, 1f, 1f, -38f * u, white, fade);
                Piece(items, "sealR", TexSealRight, env, sx + 10f + 55f * u, sy + 26f * u + 210f * u * u, EnvelopeArt.SealSize, EnvelopeArt.SealSize, 0.5f, 0.5f, 1f, 1f, 42f * u, white, fade);
            }
            if (crack > 0.001f)
                Piece(items, "crack", TexCrack, env, sx, sy, EnvelopeArt.SealSize, EnvelopeArt.SealSize, 0.5f, 0.5f, 1f, 1f, 0f, crackColour ?? white, crack * a);

            // The nine stars: lit for each ante cleared.
            if (sealSplit <= 0f)
            {
                for (int i = 0; i < Math.Min(9, litStars); i++)
                {
                    EnvelopeArt.StarPosition(i, out float lx, out float ly);
                    Piece(items, "star" + i, TexGlint, env, lx, ly, 34f, 34f, 0.5f, 0.5f, 1f, 1f, 0f, Gold, 0.95f * a);
                }
            }
        }

        static void CardSlot(List<DrawItem> items)
        {
            // A marker the caller replaces: keeps the card between the lining and the pocket while inside.
            items.Add(new DrawItem { key = "cardslot", kind = DrawKind.Group, w = 0f, h = 0f, a = 0f });
        }

        static void Card(List<DrawItem> items, EnvelopeShow show, float x, float y, float scale, float rot, float alpha, float t, Beats b, bool free)
        {
            var group = new DrawItem
            {
                key = "card", kind = DrawKind.Group, x = x, y = y, w = EnvelopeArt.CardWidth, h = EnvelopeArt.CardHeight,
                px = 0.5f, py = 0.5f, sx = scale, sy = scale, rotation = rot, r = 1f, g = 1f, b = 1f, a = alpha,
            };
            int slot = items.FindIndex(i => i.key == "cardslot");
            var cardItems = new List<DrawItem> { group };
            cardItems.Add(new DrawItem
            {
                key = "cardImage", kind = DrawKind.Image, parent = "card", texture = show.cardTexture,
                x = 0f, y = 0f, w = EnvelopeArt.CardWidth, h = EnvelopeArt.CardHeight, px = 0f, py = 0f, sx = 1f, sy = 1f,
                r = 1f, g = 1f, b = 1f, a = 1f,
            });
            float[] rc = ItemLook.RarityColour(show.rarity);
            cardItems.Add(TextItem("cardName", "card", show.itemName, EnvelopeArt.CardWidth * 0.5f, 302f, 236f, 58f, 19, Cream, 1f, true, false));
            cardItems.Add(TextItem("cardRarity", "card", ItemLook.RarityName(show.rarity).ToUpperInvariant(), EnvelopeArt.CardWidth * 0.5f, 350f, 236f, 20f, 13, rc, 1f, true, false));

            // A sheen across the card, clipped to it.
            float since = t - b.moveEnd;
            if (free && since > 0f)
            {
                float u = (since % 3.2f) / 1.0f;
                if (u < 1f)
                {
                    cardItems.Add(new DrawItem
                    {
                        key = "cardSheen", kind = DrawKind.Group, clip = true, x = x, y = y, w = EnvelopeArt.CardWidth, h = EnvelopeArt.CardHeight,
                        px = 0.5f, py = 0.5f, sx = scale, sy = scale, rotation = rot, r = 1f, g = 1f, b = 1f, a = alpha,
                    });
                    cardItems.Add(new DrawItem
                    {
                        key = "cardSheenBand", kind = DrawKind.Image, parent = "cardSheen", texture = TexSheen,
                        x = -120f + u * (EnvelopeArt.CardWidth + 240f), y = EnvelopeArt.CardHeight * 0.5f, w = 160f, h = 700f,
                        px = 0.5f, py = 0.5f, sx = 1f, sy = 1f, rotation = 22f, r = 1f, g = 1f, b = 1f,
                        a = show.rarity >= ItemRarity.Rare ? 0.38f : 0.24f,
                    });
                }
            }
            if (slot >= 0)
            {
                items.RemoveAt(slot);
                items.InsertRange(slot, cardItems);
            }
            else items.AddRange(cardItems);
        }

        static void Sheen(List<DrawItem> items, Env env, float u, float alpha)
        {
            if (u < 0f || u > 1f) return;
            Local(env, EnvelopeArt.Width * 0.5f, EnvelopeArt.Height * 0.5f, out float x, out float y);
            items.Add(new DrawItem
            {
                key = "envSheen", kind = DrawKind.Group, clip = true, x = x, y = y, w = EnvelopeArt.Width, h = EnvelopeArt.Height,
                px = 0.5f, py = 0.5f, sx = env.scale, sy = env.scale, rotation = env.rot, r = 1f, g = 1f, b = 1f, a = env.alpha,
            });
            items.Add(new DrawItem
            {
                key = "envSheenBand", kind = DrawKind.Image, parent = "envSheen", texture = TexSheen,
                x = -260f + u * (EnvelopeArt.Width + 520f), y = EnvelopeArt.Height * 0.5f, w = 240f, h = 1100f,
                px = 0.5f, py = 0.5f, sx = 1f, sy = 1f, rotation = 24f, r = 1f, g = 1f, b = 1f, a = alpha,
            });
        }

        static readonly float[] GlintSpots =
        {
            45f, 45f, 675f, 45f, 45f, 415f, 675f, 415f, 360f, 368f, 190f, 112f, 548f, 140f, 600f, 300f, 128f, 268f,
        };

        static void Glints(List<DrawItem> items, Env env, float t, float strength)
        {
            if (strength <= 0f) return;
            int n = GlintSpots.Length / 2;
            for (int i = 0; i < n; i++)
            {
                float phase = ((t * 0.37f + i * 0.61f) % 1f + 1f) % 1f;
                // A short twinkle once a cycle.
                float k = phase < 0.12f ? (float)Math.Sin(phase / 0.12f * Math.PI) : 0f;
                if (k <= 0.01f) continue;
                float size = 26f + 14f * ((i * 7) % 3) / 2f;
                Piece(items, "glint" + i, TexGlint, env, GlintSpots[i * 2], GlintSpots[i * 2 + 1], size, size, 0.5f, 0.5f, 0.6f + 0.6f * k, 0.6f + 0.6f * k,
                    k * 30f, new[] { 1f, 0.94f, 0.78f }, k * strength * env.alpha);
            }
        }

        static void SerialNumber(List<DrawItem> items, Env env, int serial)
        {
            if (serial <= 0) return;
            Local(env, EnvelopeArt.Width - 128f, EnvelopeArt.Height - 46f, out float x, out float y);
            var item = TextItem("serial", null, "No. " + serial.ToString("0000"), x, y, 120f, 22f, 14, Gold, 0.85f * env.alpha, true, false);
            item.rotation = env.rot;
            item.align = TextAlign.Right;
            items.Add(item);
        }

        static void Sparks(List<DrawItem> items, EnvelopeShow show, float cx, float cy, float since, int rank, float[] rc)
        {
            if (since < 0f) return;
            int count = 14 + 8 * rank;
            for (int i = 0; i < count; i++)
            {
                float h1 = Hash01(i, 1), h2 = Hash01(i, 2), h3 = Hash01(i, 3), h4 = Hash01(i, 4);
                float life = 0.9f + 0.7f * h3;
                if (since > life) continue;
                float ang = h1 * 6.2832f;
                float speed = 260f + 360f * h2;
                float x = cx + (float)Math.Cos(ang) * speed * since;
                float y = cy + (float)Math.Sin(ang) * speed * since * 0.8f + 0.5f * 420f * since * since;
                float u = since / life;
                float size = 18f + 26f * h4;
                float[] c = Mix(rc, new[] { 1f, 1f, 1f }, 0.25f + 0.5f * h3);
                Image(items, "spark" + i, TexGlint, x, y, size, size, 1f - 0.4f * u, 1f - 0.4f * u, since * 200f * (h1 - 0.5f), c, 1f - u * u);
            }
        }

        /// <summary>A Celestial card keeps its effect's sparks drifting round it.</summary>
        /// <summary>How far outside the card's edge its effect's beads run, and how much larger than the suit plate's they are drawn.</summary>
        const float BeadGap = 8f, BeadGrow = 1.5f;

        /// <summary>
        /// A Celestial prize's own effect, as beads of light running round the card (the motion the suit's plate gives it,
        /// <c>CelestialPlate.Light</c>), in the effect's own colours, larger and brighter than on the plate and clear of the
        /// card's frame: each of the six moves its own way on the reveal. Children of the card, so they turn and grow with
        /// it; they come up over the first 0.8 s after the burst.
        /// </summary>
        static void EffectBeads(List<DrawItem> items, EnvelopeShow show, float since)
        {
            if (since <= 0f) return;
            ItemEffect effect = CelestialLooks.Effect(show.effectId);
            if (effect == null) return;
            CelestialMotion motion = CelestialLooks.MotionOf(effect.id);
            float[] colour = effect.colour != null && effect.colour.Length == 3 ? effect.colour : new[] { 1f, 1f, 1f };
            float[] second = CelestialLooks.SecondOf(effect.id, colour);
            float up = Smooth(0f, 0.8f, since);
            float width = EnvelopeArt.CardWidth + 2f * BeadGap, height = EnvelopeArt.CardHeight + 2f * BeadGap;
            List<CelestialPlate.Bead> path = CelestialPlate.Path(width, height);
            float round = 2f * (width + height);
            for (int i = 0; i < path.Count; i++)
            {
                CelestialPlate.Bead bead = path[i];
                float v = CelestialPlate.Light(motion, bead.Along, round, since, out float w);
                if (v * up <= 0.001f) continue;
                float bw = (bead.Upright ? CelestialPlate.BeadWidth : CelestialPlate.BeadLength) * BeadGrow;
                float bh = (bead.Upright ? CelestialPlate.BeadLength : CelestialPlate.BeadWidth) * BeadGrow;
                items.Add(new DrawItem
                {
                    key = "bead" + i, kind = DrawKind.Image, parent = "card", texture = TexBead,
                    x = bead.X - BeadGap, y = bead.Y - BeadGap, w = bw, h = bh, px = 0.5f, py = 0.5f, sx = 1f, sy = 1f,
                    r = colour[0] + (second[0] - colour[0]) * w, g = colour[1] + (second[1] - colour[1]) * w,
                    b = colour[2] + (second[2] - colour[2]) * w, a = Clamp01(v / CelestialPlate.Most * up),
                });
            }
        }

        static void RevealLine(List<DrawItem> items, string key, string text, float y, float h, int size, float[] colour, float since, int order,
            bool bold = false, bool italic = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            float u = EaseOut(Clamp01((since - order * 0.07f) / 0.5f));
            if (u <= 0f) return;
            var item = TextItem(key, null, text, 690f + 30f * (1f - u), y, 540f, h, size, colour, u, bold, italic);
            item.px = 0f;
            item.align = TextAlign.Left;
            items.Add(item);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A point in envelope pixels (top left 0,0) to the screen.</summary>
        static void Local(Env env, float lx, float ly, out float x, out float y)
        {
            float dx = (lx - EnvelopeArt.Width * 0.5f) * env.scale, dy = (ly - EnvelopeArt.Height * 0.5f) * env.scale;
            double rad = env.rot * Math.PI / 180.0;
            float c = (float)Math.Cos(rad), s = (float)Math.Sin(rad);
            // Clockwise on a y-down screen.
            x = env.x + dx * c - dy * s;
            y = env.y + dx * s + dy * c;
        }

        static void Piece(List<DrawItem> items, string key, string tex, Env env, float lx, float ly, float w, float h, float px, float py,
            float sx, float sy, float rot, float[] tint, float alpha)
        {
            Local(env, lx, ly, out float x, out float y);
            Image(items, key, tex, x, y, w, h, sx * env.scale, sy * env.scale, env.rot + rot, tint, alpha, px, py);
        }

        static void Image(List<DrawItem> items, string key, string tex, float x, float y, float w, float h, float sx, float sy, float rot,
            float[] tint, float alpha, float px = 0.5f, float py = 0.5f)
        {
            if (alpha <= 0.001f) return;
            items.Add(new DrawItem
            {
                key = key, kind = DrawKind.Image, texture = tex, x = x, y = y, w = w, h = h, px = px, py = py, sx = sx, sy = sy,
                rotation = rot, r = tint[0], g = tint[1], b = tint[2], a = Clamp01(alpha),
            });
        }

        static void Text(List<DrawItem> items, string key, string text, float x, float y, float w, float h, int size, float[] colour, float alpha,
            bool bold = false, bool italic = false)
        {
            if (alpha <= 0.001f || string.IsNullOrEmpty(text)) return;
            items.Add(TextItem(key, null, text, x, y, w, h, size, colour, alpha, bold, italic));
        }

        static DrawItem TextItem(string key, string parent, string text, float x, float y, float w, float h, int size, float[] colour, float alpha,
            bool bold, bool italic)
        {
            return new DrawItem
            {
                key = key, kind = DrawKind.Text, parent = parent, text = text, x = x, y = y, w = w, h = h, px = 0.5f, py = 0.5f,
                sx = 1f, sy = 1f, r = colour[0], g = colour[1], b = colour[2], a = Clamp01(alpha), fontSize = size, bold = bold,
                italic = italic, align = TextAlign.Centre,
            };
        }

        static float[] Grey(float v) => new[] { v, v, v };

        static float[] Mix(float[] a, float[] b, float t) => new[] { a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t };

        static float Hash01(int i, int salt)
        {
            unchecked
            {
                uint h = (uint)(i * 747796405 + salt * 2891336453u + 12345);
                h = (h ^ (h >> 15)) * 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        static float Smooth(float a, float b, float v)
        {
            float t = Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        static float EaseIn(float t) => t * t * t;
        static float EaseInOut(float t) => t * t * (3f - 2f * t);

        static float EaseOutBack(float t)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
