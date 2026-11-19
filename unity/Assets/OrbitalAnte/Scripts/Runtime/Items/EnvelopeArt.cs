using System;
using System.Collections.Generic;

namespace OrbitalAnte.Items
{
    /// <summary>
    /// A picture as plain numbers: straight (not premultiplied) RGBA, 0 to 1, row 0 at the TOP. Unity's textures
    /// start at the bottom, so <c>EnvelopeTextures</c> flips the rows when it uploads one.
    /// </summary>
    public sealed class ArtImage
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float[] Pixels;

        public ArtImage(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new float[width * height * 4];
        }

        public float Alpha(int x, int y) => Pixels[(y * Width + x) * 4 + 3];

        public void Set(int x, int y, float r, float g, float b, float a)
        {
            int i = (y * Width + x) * 4;
            Pixels[i] = r; Pixels[i + 1] = g; Pixels[i + 2] = b; Pixels[i + 3] = a;
        }

        /// <summary>Lays a colour over the pixel with coverage <paramref name="a"/> (the usual "over").</summary>
        public void Over(int x, int y, float r, float g, float b, float a)
        {
            if (a <= 0f || x < 0 || y < 0 || x >= Width || y >= Height) return;
            if (a > 1f) a = 1f;
            int i = (y * Width + x) * 4;
            float da = Pixels[i + 3];
            float oa = a + da * (1f - a);
            if (oa <= 0f) return;
            Pixels[i] = (r * a + Pixels[i] * da * (1f - a)) / oa;
            Pixels[i + 1] = (g * a + Pixels[i + 1] * da * (1f - a)) / oa;
            Pixels[i + 2] = (b * a + Pixels[i + 2] * da * (1f - a)) / oa;
            Pixels[i + 3] = oa;
        }

        /// <summary>Mixes the pixel's own colour toward a colour without changing its coverage.</summary>
        public void Tint(int x, int y, float r, float g, float b, float amount)
        {
            if (amount <= 0f || x < 0 || y < 0 || x >= Width || y >= Height) return;
            if (amount > 1f) amount = 1f;
            int i = (y * Width + x) * 4;
            Pixels[i] += (r - Pixels[i]) * amount;
            Pixels[i + 1] += (g - Pixels[i + 1]) * amount;
            Pixels[i + 2] += (b - Pixels[i + 2]) * amount;
        }
    }

    /// <summary>
    /// Paints the envelope and the prize card from code: midnight paper with a woven grain, gold foil borders, an
    /// orbit emblem, a star-chart lining and an oxblood wax seal stamped with a ringed planet and nine stars (one
    /// for each ante). No textures ship: everything is drawn once when first needed and kept. Plain C#, so the
    /// pictures can be made and checked with no engine (<c>tools/envelope_view</c> draws them to PNG files).
    ///
    /// Sizes are in pixels at the game's 1280 x 800 reference, where the envelope is drawn one to one.
    /// </summary>
    public static class EnvelopeArt
    {
        public const int Width = 720;
        public const int Height = 460;
        /// <summary>How far the closed flap's tip reaches down the envelope.</summary>
        public const float FlapDepth = 276f;
        public const int FlapTextureHeight = 300;
        /// <summary>Where the side flaps meet: the bottom of the opening's V.</summary>
        public const float PocketDepth = 222f;
        /// <summary>The seal's middle, in envelope pixels.</summary>
        public const float SealX = Width * 0.5f;
        public const float SealY = 258f;
        public const int SealSize = 200;
        public const float SealRadius = 74f;
        /// <summary>The nine stars stamped round the seal, this far from its middle.</summary>
        public const float StarRing = 46f;
        public const int CardWidth = 280;
        public const int CardHeight = 392;
        /// <summary>The window on the card where the item's picture goes.</summary>
        public const float CardArtTop = 26f, CardArtHeight = 218f, CardArtInset = 22f;
        /// <summary>The plate under the picture where the name is written (the game writes the words).</summary>
        public const float CardPlateTop = 262f, CardPlateBottom = 372f;

        // ------------------------------------------------------------------ palette

        static readonly float[] PaperDeep = { 0.055f, 0.070f, 0.150f };
        static readonly float[] PaperLift = { 0.105f, 0.130f, 0.255f };
        static readonly float[] LiningA = { 0.090f, 0.050f, 0.200f };
        static readonly float[] LiningB = { 0.030f, 0.170f, 0.230f };
        static readonly float[] Wax = { 0.40f, 0.05f, 0.09f };

        // ------------------------------------------------------------------ the pieces

        /// <summary>The inside of the envelope, seen through the opening: a star chart on indigo.</summary>
        public static ArtImage Lining()
        {
            var img = new ArtImage(Width, Height);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float[] c = LiningAt(x, y, Width * 0.5f, -Height * 0.35f, 11);
                // Deeper inside is darker: the front of the pocket shades it.
                float shade = 1f - 0.55f * Smooth(40f, Height, y);
                img.Set(x, y, c[0] * shade, c[1] * shade, c[2] * shade, 1f);
            }
            Stars(img, 0, 0, Width, Height, 170, 23, 0.9f);
            return img;
        }

        /// <summary>
        /// The front of the pocket (the back of the envelope as you hold it): the two side flaps, the bottom flap
        /// with its orbit emblem, a double gold border and art-deco corners. Transparent in the V of the opening.
        /// </summary>
        public static ArtImage Pocket()
        {
            var img = new ArtImage(Width, Height);
            float bottomApex = Height * 0.53f;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float vEdge = PocketEdge(x);
                float cover = Clamp01(y - vEdge + 0.5f);
                if (cover <= 0f) continue;
                float[] p = PaperAt(x, y, 0);
                float bottomEdge = BottomFlapEdge(x, bottomApex);
                bool onBottom = y >= bottomEdge;
                float tone;
                if (onBottom) tone = 1.06f + 0.05f * (1f - (y - bottomEdge) / (Height - bottomEdge));
                else
                {
                    // Side flaps: a little darker, and darker still just above the bottom flap (its shadow).
                    tone = 0.92f - 0.04f * Smooth(0f, Height, y);
                    float gap = bottomEdge - y;
                    if (gap < 14f) tone *= 0.72f + 0.28f * Smooth(0f, 14f, gap);
                    // The fold where the two side flaps meet the edges of the opening catches the light.
                    float rim = y - vEdge;
                    if (rim < 3f) tone *= 1.18f - 0.06f * rim;
                }
                img.Set(x, y, p[0] * tone, p[1] * tone, p[2] * tone, cover);
            }

            // The paper edge of the bottom flap: a thin light line, then gold just inside it.
            for (int x = 0; x < Width; x++)
            {
                float e = BottomFlapEdge(x, bottomApex);
                for (int dy = -1; dy <= 1; dy++) img.Tint(x, (int)e + dy, 0.30f, 0.34f, 0.52f, 0.35f);
            }
            FoilPath(img, x => BottomFlapEdge(x, bottomApex) + 11f, 26f, Width - 26f, 1.4f);

            // Double border and corners.
            FoilRect(img, 16f, 16f, Width - 16f, Height - 16f, 1.6f);
            FoilRect(img, 23f, 23f, Width - 23f, Height - 23f, 0.8f);
            Corners(img, 23f, 23f, Width - 23f, Height - 23f);

            // A sprinkle of gold stars and two small constellations on the side flaps.
            var rng = new Hash(91);
            for (int i = 0; i < 70; i++)
            {
                float x = 34f + rng.Next01() * (Width - 68f), y = 34f + rng.Next01() * (Height - 68f);
                if (y < PocketEdge(x) + 10f || y > BottomFlapEdge(x, bottomApex) - 8f) continue;
                FoilDot(img, x, y, 0.6f + rng.Next01() * 1.1f, 0.35f + 0.5f * rng.Next01());
            }
            Constellation(img, new[] { 92f, 300f, 128f, 268f, 168f, 290f, 196f, 252f, 236f, 270f }, 0.55f);
            Constellation(img, new[] { 520f, 284f, 556f, 300f, 600f, 274f, 628f, 300f, 590f, 330f }, 0.55f);

            Emblem(img, Width * 0.5f, Height * 0.80f);
            return img;
        }

        /// <summary>The flap as it lies closed: paper with a gold edge, and its soft shadow on the pocket below.</summary>
        public static ArtImage FlapOutside()
        {
            var img = new ArtImage(Width, FlapTextureHeight);
            // Shadow first (it sits under the flap, a little lower).
            for (int y = 0; y < FlapTextureHeight; y++)
            for (int x = 0; x < Width; x++)
            {
                float d = FlapDistance(x, y - 7f);
                if (d < 0f) continue;
                float a = 0.55f * (1f - Smooth(0f, 16f, d));
                if (a > 0f) img.Set(x, y, 0.01f, 0.01f, 0.03f, a);
            }
            for (int y = 0; y < FlapTextureHeight; y++)
            for (int x = 0; x < Width; x++)
            {
                float d = FlapDistance(x, y);
                float cover = Clamp01(0.5f - d);
                if (cover <= 0f) continue;
                float[] p = PaperAt(x, y, 7);
                // Lighter toward the tip, darker at the fold; a soft rise along the slanted edges.
                float tone = 1.02f + 0.10f * Smooth(0f, FlapDepth, y) - 0.10f * (1f - Smooth(0f, 30f, y));
                float edge = -d;
                if (edge < 2.5f) tone *= 1.25f;
                img.Over(x, y, p[0] * tone, p[1] * tone, p[2] * tone, cover);
            }
            // Gold along both slanted edges and across the fold.
            FoilFlapEdge(img, 13f, 1.6f);
            FoilFlapEdge(img, 20f, 0.8f);
            for (int x = 30; x < Width - 30; x++)
            {
                float y = 16f;
                if (FlapDistance(x, y) < -14f) FoilPixelLine(img, x, y, 1.6f);
                if (FlapDistance(x, 23f) < -21f) FoilPixelLine(img, x, 23f, 0.8f);
            }
            // A fan of fine debossed rays from the tip, very faint, like engraved card stock.
            for (int y = 0; y < FlapTextureHeight; y++)
            for (int x = 0; x < Width; x++)
            {
                if (FlapDistance(x, y) > -24f) continue;
                float ang = (float)Math.Atan2(y - FlapDepth, x - Width * 0.5f);
                float ray = (float)Math.Abs(Math.Sin(ang * 36f));
                if (ray > 0.985f) img.Tint(x, y, 0.20f, 0.24f, 0.42f, 0.18f * (1f - Smooth(0f, FlapDepth, FlapDepth - y)));
            }
            return img;
        }

        /// <summary>The inside of the flap, seen once it is folded up: the star-chart lining with a gold hairline.</summary>
        public static ArtImage FlapInside()
        {
            var img = new ArtImage(Width, FlapTextureHeight);
            for (int y = 0; y < FlapTextureHeight; y++)
            for (int x = 0; x < Width; x++)
            {
                float d = FlapDistance(x, y);
                float cover = Clamp01(0.5f - d);
                if (cover <= 0f) continue;
                float[] c = LiningAt(x, y, Width * 0.5f, FlapDepth * 1.25f, 37);
                float shade = 0.75f + 0.25f * Smooth(0f, FlapDepth, y);
                img.Set(x, y, c[0] * shade, c[1] * shade, c[2] * shade, cover);
            }
            Stars(img, 0, 0, Width, FlapTextureHeight, 90, 41, 0.8f, (x, y) => FlapDistance(x, y) < -4f);
            FoilFlapEdge(img, 9f, 1.0f);
            return img;
        }

        /// <summary>
        /// The wax seal, lit from above left: an irregular poured edge, a raised rim where the stamp pressed, and the
        /// stamp's design picked out in gold. <paramref name="half"/> -1 gives only the left of the crack, 1 only the
        /// right, 0 the whole seal.
        /// </summary>
        public static ArtImage Seal(int half = 0)
        {
            var img = new ArtImage(SealSize, SealSize);
            float c = SealSize * 0.5f;
            // Soft shadow under the wax.
            if (half == 0 || half == -1 || half == 1)
            {
                for (int y = 0; y < SealSize; y++)
                for (int x = 0; x < SealSize; x++)
                {
                    if (half != 0 && Math.Sign(x - CrackX(y)) != half) continue;
                    float r = Len(x - c - 3f, y - c - 6f);
                    float a = 0.6f * (1f - Smooth(SealRadius - 6f, SealRadius + 12f, r));
                    if (a > 0f) img.Set(x, y, 0.02f, 0.0f, 0.01f, a);
                }
            }
            float[] lightDir = Normalise(-0.45f, -0.62f, 0.64f);
            float[] halfway = Normalise(lightDir[0], lightDir[1], lightDir[2] + 1f);
            for (int y = 0; y < SealSize; y++)
            for (int x = 0; x < SealSize; x++)
            {
                if (half != 0)
                {
                    float side = x + 0.5f - CrackX(y);
                    if (half < 0 ? side > 0.5f : side < -0.5f) continue;
                }
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float r = Len(dx, dy);
                float edge = SealEdge((float)Math.Atan2(dy, dx));
                float cover = Clamp01(edge - r + 0.5f);
                if (cover <= 0f) continue;
                // Height and its slope give the surface direction.
                float h = SealHeight(dx, dy);
                float hx = SealHeight(dx + 0.7f, dy) - SealHeight(dx - 0.7f, dy);
                float hy = SealHeight(dx, dy + 0.7f) - SealHeight(dx, dy - 0.7f);
                float[] n = Normalise(-hx * 34f, -hy * 34f, 1f);
                float diffuse = Math.Max(0f, Dot(n, lightDir));
                float spec = (float)Math.Pow(Math.Max(0f, Dot(n, halfway)), 46.0);
                float rim = (float)Math.Pow(1f - Math.Max(0f, n[2]), 2.0);
                float design = SealDesign(dx, dy);
                float[] baseCol = Wax;
                float wr = baseCol[0], wg = baseCol[1], wb = baseCol[2];
                // A little marbling in the wax.
                float marble = Fbm(x * 0.045f, y * 0.045f, 5, 3) - 0.5f;
                wr *= 1f + 0.35f * marble; wg *= 1f + 0.25f * marble; wb *= 1f + 0.25f * marble;
                float lit = 0.30f + 0.95f * diffuse;
                float cr = wr * lit + spec * 0.85f + rim * 0.10f;
                float cg = wg * lit + spec * 0.60f + rim * 0.03f;
                float cb = wb * lit + spec * 0.58f + rim * 0.04f;
                if (design > 0f)
                {
                    // Gilded relief: foil coloured by how it faces the light.
                    float[] gold = FoilColour(0.5f + 0.6f * (n[0] * -0.7f + n[1] * -0.7f) + 0.1f * marble);
                    float gl = 0.55f + 0.7f * diffuse + spec * 0.9f;
                    cr += (gold[0] * gl - cr) * design;
                    cg += (gold[1] * gl - cg) * design;
                    cb += (gold[2] * gl - cb) * design;
                }
                img.Over(x, y, Clamp01(cr), Clamp01(cg), Clamp01(cb), cover);
            }
            return img;
        }

        /// <summary>The light that leaks through the seal as it cracks. White, so the game can tint it the prize's colour.</summary>
        public static ArtImage CrackGlow()
        {
            var img = new ArtImage(SealSize, SealSize);
            float c = SealSize * 0.5f;
            for (int y = 0; y < SealSize; y++)
            for (int x = 0; x < SealSize; x++)
            {
                float r = Len(x - c, y - c);
                float inside = 1f - Smooth(SealRadius - 10f, SealRadius + 6f, r);
                float d = Math.Abs(x + 0.5f - CrackX(y));
                float core = Clamp01(1.6f - d);
                float glow = (float)Math.Exp(-d * d / 60f) * 0.7f;
                float a = Math.Min(1f, core + glow) * inside;
                if (a > 0.003f) img.Set(x, y, 1f, 1f, 1f, a);
            }
            return img;
        }

        /// <summary>Where the crack runs across the seal at row <paramref name="y"/> (seal pixels).</summary>
        public static float CrackX(float y)
        {
            float c = SealSize * 0.5f;
            float t = y / SealSize;
            return c + 7f * (float)Math.Sin(t * 19f + 0.4f) + 4f * (float)Math.Sin(t * 47f + 2f) - 5f * (t - 0.5f);
        }

        /// <summary>
        /// The prize card: a foil frame in the rarity's colour, a window with the item's stand-in picture, a plate
        /// for its name, and (rare and above) a holographic sheen. Celestial cards carry their effect's sparks.
        /// </summary>
        public static ArtImage Card(ItemSpec item, ItemRarity rarity, float[] effectColour)
        {
            var img = new ArtImage(CardWidth, CardHeight);
            float[] rc = ItemLook.RarityColour(rarity);
            float[] ic = item != null ? item.colour : new[] { 0.6f, 0.6f, 0.6f };
            float[] ac = item != null ? item.accent : new[] { 0.9f, 0.8f, 0.5f };
            float artL = CardArtInset, artR = CardWidth - CardArtInset, artT = CardArtTop, artB = CardArtTop + CardArtHeight;
            float acx = (artL + artR) * 0.5f, acy = (artT + artB) * 0.5f;
            bool holo = rarity >= ItemRarity.Rare;
            for (int y = 0; y < CardHeight; y++)
            for (int x = 0; x < CardWidth; x++)
            {
                float d = RoundRect(x + 0.5f, y + 0.5f, 0f, 0f, CardWidth, CardHeight, 18f);
                float cover = Clamp01(0.5f - d);
                if (cover <= 0f) continue;
                float inner = -d;
                float r, g, b;
                if (inner < 9f)
                {
                    // The frame: the rarity's colour as brushed metal.
                    float u = (x * 0.55f + y) / CardHeight + 0.08f * (Fbm(x * 0.5f, y * 0.02f, 2, 11) - 0.5f);
                    float shine = 0.55f + 0.45f * (float)Math.Sin(u * 9f + 1f);
                    r = rc[0] * (0.45f + 0.75f * shine); g = rc[1] * (0.45f + 0.75f * shine); b = rc[2] * (0.45f + 0.75f * shine);
                    if (inner < 1.5f || (inner > 7f && inner < 9f)) { r *= 0.6f; g *= 0.6f; b *= 0.6f; }
                }
                else
                {
                    // The body: near black, warmed by the rarity's colour toward the middle.
                    float glow = 1f - Smooth(0f, CardHeight * 0.75f, Len(x - CardWidth * 0.5f, y - acy));
                    r = 0.04f + rc[0] * 0.10f * glow; g = 0.04f + rc[1] * 0.10f * glow; b = 0.06f + rc[2] * 0.12f * glow;
                    float grain = (Fbm(x * 0.9f, y * 0.9f, 2, 17) - 0.5f) * 0.03f;
                    r += grain; g += grain; b += grain;
                }
                img.Set(x, y, Clamp01(r), Clamp01(g), Clamp01(b), cover);
            }

            // The picture window: the item's colour falling off to dark.
            for (int y = (int)artT; y < (int)artB; y++)
            for (int x = (int)artL; x < (int)artR; x++)
            {
                float d = RoundRect(x + 0.5f, y + 0.5f, artL, artT, artR, artB, 10f);
                float cover = Clamp01(0.5f - d);
                if (cover <= 0f) continue;
                float fall = Smooth(0f, 150f, Len(x - acx, y - acy * 0.95f));
                float k = 0.55f * (1f - fall) + 0.08f;
                img.Over(x, y, ic[0] * k, ic[1] * k, ic[2] * k, cover);
                // A fine hairline round the window in the rarity's colour.
                if (d > -1.6f) img.Tint(x, y, rc[0], rc[1], rc[2], 0.7f);
            }

            if (item != null) Icon(img, item.category, acx, acy + 4f, ic, ac);

            if (effectColour != null)
            {
                var rng = new Hash(7);
                for (int i = 0; i < 60; i++)
                {
                    float px = artL + 8f + rng.Next01() * (artR - artL - 16f), py = artT + 8f + rng.Next01() * (artB - artT - 16f);
                    Glow(img, px, py, 1.2f + rng.Next01() * 3.4f, effectColour, 0.4f + rng.Next01() * 0.5f);
                }
                // A soft halo of the effect round the picture.
                for (int y = (int)artT; y < (int)artB; y++)
                for (int x = (int)artL; x < (int)artR; x++)
                {
                    float d = RoundRect(x + 0.5f, y + 0.5f, artL, artT, artR, artB, 10f);
                    if (d > 0f) continue;
                    float a = 0.35f * (float)Math.Exp(d / 14f) * (0.6f + 0.4f * Fbm(x * 0.05f, y * 0.05f, 3, 5));
                    img.Over(x, y, effectColour[0], effectColour[1], effectColour[2], a);
                }
            }

            if (holo)
            {
                // Holographic foil over the window: thin rainbow bands on the diagonal.
                for (int y = (int)artT; y < (int)artB; y++)
                for (int x = (int)artL; x < (int)artR; x++)
                {
                    if (RoundRect(x + 0.5f, y + 0.5f, artL, artT, artR, artB, 10f) > 0f) continue;
                    float u = (x + y * 0.6f) * 0.035f + 0.6f * Fbm(x * 0.02f, y * 0.02f, 2, 29);
                    float[] hue = Rainbow(u);
                    float band = 0.5f + 0.5f * (float)Math.Sin(u * 2.3f);
                    img.Over(x, y, hue[0], hue[1], hue[2], 0.10f * band);
                }
            }

            // The name plate.
            for (int y = (int)CardPlateTop; y < (int)CardPlateBottom; y++)
            for (int x = 18; x < CardWidth - 18; x++)
            {
                float d = RoundRect(x + 0.5f, y + 0.5f, 18f, CardPlateTop, CardWidth - 18f, CardPlateBottom, 8f);
                float cover = Clamp01(0.5f - d);
                if (cover <= 0f) continue;
                img.Over(x, y, 0.02f, 0.02f, 0.03f, 0.55f * cover);
                if (d > -1.2f) img.Tint(x, y, rc[0], rc[1], rc[2], 0.55f);
            }
            // The rarity gem at the top of the frame.
            for (int y = 0; y < 24; y++)
            for (int x = (int)(CardWidth * 0.5f) - 12; x < (int)(CardWidth * 0.5f) + 12; x++)
            {
                float dx = Math.Abs(x + 0.5f - CardWidth * 0.5f), dy = Math.Abs(y + 0.5f - 6f);
                float d = (dx + dy) - 8f;
                float cover = Clamp01(0.5f - d);
                if (cover <= 0f) continue;
                float lit = 0.75f + 0.5f * ((CardWidth * 0.5f - x) + (6f - y)) / 16f;
                img.Over(x, y, Clamp01(rc[0] * lit + 0.15f), Clamp01(rc[1] * lit + 0.15f), Clamp01(rc[2] * lit + 0.15f), cover);
            }
            return img;
        }

        /// <summary>Soft light rays round a bright middle, white for tinting. Spins slowly behind the card.</summary>
        public static ArtImage Rays(int size = 512, int count = 14)
        {
            var img = new ArtImage(size, size);
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float r = Len(dx, dy) / c;
                if (r >= 1f) continue;
                float ang = (float)Math.Atan2(dy, dx);
                float ray = (float)Math.Pow(Math.Abs(Math.Cos(ang * count * 0.5f)), 10.0);
                float thin = (float)Math.Pow(Math.Abs(Math.Cos(ang * count * 0.5f + 1.3f)), 40.0) * 0.6f;
                float fade = (1f - r) * (1f - r);
                float core = (float)Math.Exp(-r * r * 30f);
                float a = Math.Min(1f, (ray * 0.55f + thin * 0.45f) * fade + core * 0.9f);
                if (a > 0.002f) img.Set(x, y, 1f, 1f, 1f, a);
            }
            return img;
        }

        /// <summary>A four-point glint with a bright middle. White, for tinting: twinkles on the foil and flies off as sparks.</summary>
        public static ArtImage Glint(int size = 64)
        {
            var img = new ArtImage(size, size);
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Math.Abs(x + 0.5f - c) / c, dy = Math.Abs(y + 0.5f - c) / c;
                float arms = (float)(Math.Exp(-dx * 9f) * Math.Exp(-dy * 120f) + Math.Exp(-dy * 9f) * Math.Exp(-dx * 120f));
                float diag = (float)(Math.Exp(-(dx + dy) * 7f) * Math.Exp(-Math.Abs(dx - dy) * 60f)) * 0.35f;
                float core = (float)Math.Exp(-(dx * dx + dy * dy) * 40f);
                float a = Math.Min(1f, arms + diag + core);
                if (a > 0.003f) img.Set(x, y, 1f, 1f, 1f, a);
            }
            return img;
        }

        /// <summary>A soft band of light, swept over the envelope and the card.</summary>
        public static ArtImage Sheen()
        {
            var img = new ArtImage(256, 16);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 256; x++)
            {
                float u = (x + 0.5f - 128f) / 128f;
                float a = (float)Math.Exp(-u * u * 9f) * 0.8f + (float)Math.Exp(-(u - 0.42f) * (u - 0.42f) * 300f) * 0.35f;
                img.Set(x, y, 1f, 0.97f, 0.90f, a);
            }
            return img;
        }

        /// <summary>The darkening behind the envelope: deep navy, heavier at the edges.</summary>
        public static ArtImage Backdrop()
        {
            var img = new ArtImage(320, 200);
            for (int y = 0; y < 200; y++)
            for (int x = 0; x < 320; x++)
            {
                float dx = (x + 0.5f - 160f) / 160f, dy = (y + 0.5f - 100f) / 100f;
                float r2 = dx * dx * 0.8f + dy * dy;
                img.Set(x, y, 0.010f, 0.012f, 0.030f, Clamp01(0.62f + 0.33f * r2));
            }
            return img;
        }

        /// <summary>The envelope's soft shadow on whatever is behind it.</summary>
        public static ArtImage Shadow()
        {
            var img = new ArtImage(240, 160);
            for (int y = 0; y < 160; y++)
            for (int x = 0; x < 240; x++)
            {
                float d = RoundRect(x + 0.5f, y + 0.5f, 30f, 30f, 210f, 130f, 6f);
                float a = 0.75f * (1f - Smooth(-14f, 28f, d));
                img.Set(x, y, 0f, 0f, 0.01f, a);
            }
            return img;
        }

        /// <summary>A small white square, for the flash and plain shapes.</summary>
        public static ArtImage Solid()
        {
            var img = new ArtImage(4, 4);
            for (int i = 0; i < 16; i++) img.Set(i % 4, i / 4, 1f, 1f, 1f, 1f);
            return img;
        }

        /// <summary>Every fixed piece, by the name the scene uses. Cards are made per prize with <see cref="Card"/>.</summary>
        public static Dictionary<string, Func<ArtImage>> Pieces()
        {
            return new Dictionary<string, Func<ArtImage>>
            {
                { EnvelopeScene.TexLining, Lining },
                { EnvelopeScene.TexPocket, Pocket },
                { EnvelopeScene.TexFlapOut, FlapOutside },
                { EnvelopeScene.TexFlapIn, FlapInside },
                { EnvelopeScene.TexSeal, () => Seal(0) },
                { EnvelopeScene.TexSealLeft, () => Seal(-1) },
                { EnvelopeScene.TexSealRight, () => Seal(1) },
                { EnvelopeScene.TexCrack, CrackGlow },
                { EnvelopeScene.TexRays, () => Rays() },
                { EnvelopeScene.TexGlint, () => Glint() },
                { EnvelopeScene.TexSheen, Sheen },
                { EnvelopeScene.TexBackdrop, Backdrop },
                { EnvelopeScene.TexShadow, Shadow },
                { EnvelopeScene.TexSolid, Solid },
            };
        }

        // ------------------------------------------------------------------ shapes of the envelope

        /// <summary>The edge of the opening: a V from the top corners down to the middle.</summary>
        public static float PocketEdge(float x)
        {
            float u = Math.Abs(x - Width * 0.5f) / (Width * 0.5f);
            // A soft curve, not a straight cut, like a real side flap.
            return PocketDepth * (1f - u) * (1f - 0.12f * u);
        }

        static float BottomFlapEdge(float x, float apex)
        {
            float u = (x - Width * 0.5f) / (Width * 0.5f);
            return apex + (Height - apex) * (float)Math.Pow(Math.Abs(u), 1.6) * 0.92f;
        }

        /// <summary>Signed distance to the flap's outline (negative inside): a triangle with a rounded tip.</summary>
        public static float FlapDistance(float x, float y)
        {
            float cx = Width * 0.5f;
            float side = x < cx ? -1f : 1f;
            // The slanted edge on x's side runs from the top corner to the tip; its outward normal points down and out.
            float len = Len(cx, FlapDepth);
            float nx = side * FlapDepth / len, ny = cx / len;
            float edge = (x - (cx + side * cx)) * nx + y * ny;
            float dist = Math.Max(edge, -y);
            // Round the tip with a circle that touches both edges.
            const float tipR = 34f;
            float sinHalf = cx / len;
            float ccy = FlapDepth - tipR / sinHalf;
            // Past the point where the circle meets the edge (along the edge, toward the tip).
            float ex = -side * cx / len, ey = FlapDepth / len;
            if ((x - cx) * ex + (y - ccy) * ey > 0f) dist = Len(x - cx, y - ccy) - tipR;
            return dist;
        }

        // ------------------------------------------------------------------ the seal's relief

        static float SealEdge(float angle)
        {
            float r = SealRadius + 3.2f * (float)Math.Sin(angle * 5f + 0.7f) + 2.1f * (float)Math.Sin(angle * 11f + 2.1f)
                      + 1.2f * (float)Math.Sin(angle * 23f);
            // Three poured lobes.
            r += Lobe(angle, 0.6f, 9f) + Lobe(angle, 2.5f, 6f) + Lobe(angle, 4.4f, 11f);
            return r;
        }

        static float Lobe(float angle, float at, float size)
        {
            float d = (float)Math.Atan2(Math.Sin(angle - at), Math.Cos(angle - at));
            return size * (float)Math.Exp(-d * d / 0.04f);
        }

        static float SealHeight(float dx, float dy)
        {
            float r = Len(dx, dy);
            float edge = SealEdge((float)Math.Atan2(dy, dx));
            float t = r / edge;
            if (t >= 1f) return 0f;
            // Poured dome falling off at the edge...
            float h = (float)Math.Sqrt(Math.Max(0f, 1f - t * t * t * t)) * 0.55f;
            // ...a raised ring where the stamp's edge squeezed the wax up...
            float ring = (float)Math.Exp(-(r - 60f) * (r - 60f) / 30f) * 0.20f;
            // ...a flat, pressed face inside it...
            float face = 1f - Smooth(52f, 58f, r);
            h = h * (1f - face) + 0.42f * face + ring;
            // ...with the design standing proud of the face.
            h += 0.09f * SealDesign(dx, dy) * face;
            return h;
        }

        /// <summary>How much of the stamp's design covers a point (0 to 1): a ringed planet, a circle and nine stars.</summary>
        static float SealDesign(float dx, float dy)
        {
            float r = Len(dx, dy);
            float a = 0f;
            // The circle near the face's edge.
            a = Math.Max(a, Clamp01(1.4f - Math.Abs(r - 52f)));
            // The planet.
            float planet = Clamp01(14.5f - r);
            // Its ring: an ellipse band tilted 20 degrees, broken where the planet hides it.
            float ca = (float)Math.Cos(-0.35), sa = (float)Math.Sin(-0.35);
            float rx = dx * ca - dy * sa, ry = dx * sa + dy * ca;
            float er = Len(rx / 27f, ry / 8f);
            float band = Clamp01(1f - Math.Abs(er - 1f) * 9f);
            bool behind = ry < 0f && r < 15.5f;
            if (behind) band = 0f;
            // A dark gap where the ring passes in front of the planet.
            float gap = (!behind && r < 15.5f) ? Clamp01(1f - Math.Abs(er - 1f) * 5f) : 0f;
            a = Math.Max(a, Math.Max(planet * (1f - gap), band));
            // Nine stars, one for each ante, starting at the top.
            for (int i = 0; i < 9; i++)
            {
                float ang = -(float)Math.PI * 0.5f + i * (float)Math.PI * 2f / 9f;
                float sx = (float)Math.Cos(ang) * StarRing, sy = (float)Math.Sin(ang) * StarRing;
                a = Math.Max(a, StarShape(dx - sx, dy - sy, 4.6f));
            }
            return a;
        }

        /// <summary>Where the stars on the seal are, in envelope pixels, for the lights the scene puts on them.</summary>
        public static void StarPosition(int index, out float x, out float y)
        {
            float ang = -(float)Math.PI * 0.5f + index * (float)Math.PI * 2f / 9f;
            x = SealX + (float)Math.Cos(ang) * StarRing;
            y = SealY + (float)Math.Sin(ang) * StarRing;
        }

        static float StarShape(float x, float y, float size)
        {
            float r = Len(x, y);
            float ang = (float)Math.Atan2(y, x) + (float)Math.PI * 0.5f;
            float k = (float)Math.Cos(ang * 5f);
            float edge = size * (0.55f + 0.45f * k * k * k * Math.Sign(k)) + size * 0.25f;
            return Clamp01(edge - r + 0.5f);
        }

        // ------------------------------------------------------------------ paper, lining, foil

        static float[] PaperAt(int x, int y, int seed)
        {
            // Mottling, then fibres stretched along the paper.
            float mott = Fbm(x * 0.012f, y * 0.012f, 4, 3 + seed);
            float fibre = Fbm(x * 0.35f, y * 0.04f, 3, 13 + seed);
            float fine = Fbm(x * 0.9f, y * 0.9f, 2, 19 + seed);
            float t = 0.35f + 0.5f * mott;
            float lift = Clamp01(0.8f - Len(x - Width * 0.5f, y - Height * 0.45f) / (Width * 0.75f));
            float[] c = new float[3];
            for (int i = 0; i < 3; i++)
            {
                c[i] = PaperDeep[i] + (PaperLift[i] - PaperDeep[i]) * (t * 0.6f + lift * 0.5f);
                c[i] *= 0.94f + 0.12f * fibre + 0.06f * (fine - 0.5f);
            }
            return c;
        }

        static float[] LiningAt(int x, int y, float cx, float cy, int seed)
        {
            float r = Len(x - cx, y - cy);
            float u = Clamp01(r / (Width * 0.9f));
            float[] c = new float[3];
            for (int i = 0; i < 3; i++) c[i] = LiningA[i] + (LiningB[i] - LiningA[i]) * u;
            float neb = Fbm(x * 0.006f, y * 0.006f, 4, seed);
            c[0] += 0.06f * neb * neb; c[2] += 0.08f * neb;
            // The chart: circles round a pole above the envelope, and lines out from it.
            float circ = Math.Abs(((r / 42f) % 1f) - 0.5f) * 2f;
            float line = Clamp01(1f - (1f - circ) * 42f * 0.5f);
            float ang = (float)Math.Atan2(y - cy, x - cx);
            float spoke = Math.Abs((float)Math.Sin(ang * 18f));
            float spokeLine = Clamp01(1f - spoke * r * 0.5f);
            float chart = Math.Max(line, spokeLine) * 0.22f;
            c[0] += (0.85f - c[0]) * chart * 0.55f;
            c[1] += (0.70f - c[1]) * chart * 0.55f;
            c[2] += (0.42f - c[2]) * chart * 0.55f;
            return c;
        }

        static void Stars(ArtImage img, int x0, int y0, int x1, int y1, int count, int seed, float bright, Func<float, float, bool> where = null)
        {
            var rng = new Hash(seed);
            for (int i = 0; i < count; i++)
            {
                float x = x0 + rng.Next01() * (x1 - x0), y = y0 + rng.Next01() * (y1 - y0);
                float size = 0.5f + (float)Math.Pow(rng.Next01(), 3.0) * 2.2f;
                float warm = rng.Next01();
                if (where != null && !where(x, y)) continue;
                Glow(img, x, y, size, new[] { 0.95f, 0.88f - 0.12f * warm, 0.70f - 0.3f * warm }, bright * (0.4f + 0.6f * rng.Next01()));
            }
        }

        static void Glow(ArtImage img, float x, float y, float size, float[] c, float a)
        {
            int rad = (int)Math.Ceiling(size * 3f) + 1;
            for (int py = (int)y - rad; py <= (int)y + rad; py++)
            for (int px = (int)x - rad; px <= (int)x + rad; px++)
            {
                float d = Len(px + 0.5f - x, py + 0.5f - y);
                float k = Clamp01(size * 0.5f + 0.5f - d) + 0.35f * (float)Math.Exp(-d * d / (size * size * 2.5f));
                img.Over(px, py, c[0], c[1], c[2], Math.Min(1f, k) * a);
            }
        }

        /// <summary>Metallic gold: dark, warm, bright, warm, dark along <paramref name="u"/>, repeating.</summary>
        static float[] FoilColour(float u)
        {
            u = u - (float)Math.Floor(u);
            float[][] stops =
            {
                new[] { 0.46f, 0.31f, 0.11f }, new[] { 0.83f, 0.63f, 0.29f }, new[] { 0.99f, 0.92f, 0.68f },
                new[] { 0.76f, 0.56f, 0.23f }, new[] { 0.52f, 0.36f, 0.14f }, new[] { 0.46f, 0.31f, 0.11f },
            };
            float f = u * (stops.Length - 1);
            int i = Math.Min(stops.Length - 2, (int)f);
            float t = f - i;
            t = t * t * (3f - 2f * t);
            return new[]
            {
                stops[i][0] + (stops[i + 1][0] - stops[i][0]) * t,
                stops[i][1] + (stops[i + 1][1] - stops[i][1]) * t,
                stops[i][2] + (stops[i + 1][2] - stops[i][2]) * t,
            };
        }

        static void FoilPixel(ArtImage img, int x, int y, float a)
        {
            float u = (x * 0.8f + y * 1.3f) / 260f + 0.15f * Fbm(x * 0.03f, y * 0.03f, 2, 61);
            float[] c = FoilColour(u);
            img.Over(x, y, c[0], c[1], c[2], a * img.Alpha(Math.Max(0, Math.Min(img.Width - 1, x)), Math.Max(0, Math.Min(img.Height - 1, y))));
        }

        static void FoilPixelLine(ArtImage img, int x, float y, float width)
        {
            for (int py = (int)(y - width - 1); py <= (int)(y + width + 1); py++)
            {
                float a = Clamp01(width * 0.5f + 0.5f - Math.Abs(py + 0.5f - y));
                if (a > 0f) FoilPixel(img, x, py, a);
            }
        }

        static void FoilPath(ArtImage img, Func<float, float> yOf, float x0, float x1, float width)
        {
            for (int x = (int)x0; x < (int)x1; x++)
            {
                float y = yOf(x + 0.5f);
                float slope = yOf(x + 1.5f) - yOf(x - 0.5f);
                float w = width * (float)Math.Sqrt(1f + slope * slope * 0.25f);
                FoilPixelLine(img, x, y, w);
            }
        }

        static void FoilRect(ArtImage img, float l, float t, float r, float b, float width)
        {
            for (int x = (int)l; x <= (int)r; x++) { FoilPixelLine(img, x, t, width); FoilPixelLine(img, x, b, width); }
            for (int y = (int)t; y <= (int)b; y++)
            for (int px = (int)(l - width - 1); px <= (int)(l + width + 1); px++)
            {
                float a = Clamp01(width * 0.5f + 0.5f - Math.Abs(px + 0.5f - l));
                if (a > 0f) FoilPixel(img, px, y, a);
                float a2 = Clamp01(width * 0.5f + 0.5f - Math.Abs(px + (r - l) + 0.5f - r));
                if (a2 > 0f) FoilPixel(img, px + (int)(r - l), y, a2);
            }
        }

        static void FoilFlapEdge(ArtImage img, float inset, float width)
        {
            for (int y = 0; y < FlapTextureHeight; y++)
            for (int x = 0; x < Width; x++)
            {
                float d = -FlapDistance(x + 0.5f, y + 0.5f);
                if (d <= 0f || y < inset + 1f) continue;
                float a = Clamp01(width * 0.5f + 0.5f - Math.Abs(d - inset));
                if (a > 0f) FoilPixel(img, x, y, a);
            }
        }

        static void FoilDot(ArtImage img, float x, float y, float radius, float a)
        {
            int rad = (int)Math.Ceiling(radius) + 2;
            for (int py = (int)y - rad; py <= (int)y + rad; py++)
            for (int px = (int)x - rad; px <= (int)x + rad; px++)
            {
                float k = Clamp01(radius + 0.5f - Len(px + 0.5f - x, py + 0.5f - y));
                if (k > 0f && px >= 0 && py >= 0 && px < img.Width && py < img.Height) FoilPixel(img, px, py, k * a);
            }
        }

        static void FoilSegment(ArtImage img, float x0, float y0, float x1, float y1, float width, float a)
        {
            int minX = (int)Math.Min(x0, x1) - 3, maxX = (int)Math.Max(x0, x1) + 3;
            int minY = (int)Math.Min(y0, y1) - 3, maxY = (int)Math.Max(y0, y1) + 3;
            for (int py = minY; py <= maxY; py++)
            for (int px = minX; px <= maxX; px++)
            {
                if (px < 0 || py < 0 || px >= img.Width || py >= img.Height) continue;
                float d = SegmentDistance(px + 0.5f, py + 0.5f, x0, y0, x1, y1);
                float k = Clamp01(width * 0.5f + 0.5f - d);
                if (k > 0f) FoilPixel(img, px, py, k * a);
            }
        }

        static void Constellation(ArtImage img, float[] points, float a)
        {
            for (int i = 0; i + 3 < points.Length; i += 2)
                FoilSegment(img, points[i], points[i + 1], points[i + 2], points[i + 3], 0.6f, a * 0.6f);
            for (int i = 0; i + 1 < points.Length; i += 2) FoilDot(img, points[i], points[i + 1], 1.9f, a + 0.3f);
        }

        static void Corners(ArtImage img, float l, float t, float r, float b)
        {
            float[][] corners = { new[] { l, t, 1f, 1f }, new[] { r, t, -1f, 1f }, new[] { l, b, 1f, -1f }, new[] { r, b, -1f, -1f } };
            foreach (float[] c in corners)
            {
                float x = c[0], y = c[1], sx = c[2], sy = c[3];
                // A stepped art-deco bracket and a small diamond.
                FoilSegment(img, x + sx * 8f, y + sy * 8f, x + sx * 42f, y + sy * 8f, 1.0f, 1f);
                FoilSegment(img, x + sx * 8f, y + sy * 8f, x + sx * 8f, y + sy * 42f, 1.0f, 1f);
                FoilSegment(img, x + sx * 14f, y + sy * 14f, x + sx * 28f, y + sy * 14f, 0.8f, 1f);
                FoilSegment(img, x + sx * 14f, y + sy * 14f, x + sx * 14f, y + sy * 28f, 0.8f, 1f);
                float dx = x + sx * 22f, dy = y + sy * 22f;
                FoilSegment(img, dx, dy - 4f, dx + 4f, dy, 1f, 1f);
                FoilSegment(img, dx + 4f, dy, dx, dy + 4f, 1f, 1f);
                FoilSegment(img, dx, dy + 4f, dx - 4f, dy, 1f, 1f);
                FoilSegment(img, dx - 4f, dy, dx, dy - 4f, 1f, 1f);
            }
        }

        /// <summary>A small ringed planet with two tilted orbits and their moons.</summary>
        static void Emblem(ArtImage img, float cx, float cy)
        {
            Ellipse(img, cx, cy, 120f, 26f, -0.10f, 0.9f, 0.85f);
            Ellipse(img, cx, cy, 84f, 18f, 0.14f, 0.9f, 0.85f);
            for (int y = (int)(cy - 16f); y <= (int)(cy + 16f); y++)
            for (int x = (int)(cx - 16f); x <= (int)(cx + 16f); x++)
            {
                float d = Len(x + 0.5f - cx, y + 0.5f - cy);
                float k = Clamp01(11.5f - d);
                if (k > 0f) FoilPixel(img, x, y, k);
            }
            Ellipse(img, cx, cy, 22f, 6f, -0.30f, 1.6f, 1f);
            float m = 0.9f;
            FoilDot(img, cx + 120f * (float)Math.Cos(0.6) , cy + 26f * (float)Math.Sin(0.6) - 12f * (float)Math.Cos(0.6) * 0.10f, 3.2f, m);
            FoilDot(img, cx - 84f * (float)Math.Cos(0.9), cy - 18f * (float)Math.Sin(0.9) - 12f * 0.14f, 2.4f, m);
            FoilDot(img, cx - 150f, cy - 4f, 1.6f, 0.8f);
            FoilDot(img, cx + 152f, cy + 3f, 1.6f, 0.8f);
        }

        static void Ellipse(ArtImage img, float cx, float cy, float rx, float ry, float tilt, float width, float a)
        {
            int steps = (int)(rx * 8f);
            float ct = (float)Math.Cos(tilt), st = (float)Math.Sin(tilt);
            float px = 0f, py = 0f;
            for (int i = 0; i <= steps; i++)
            {
                double ang = i * Math.PI * 2.0 / steps;
                float ex = rx * (float)Math.Cos(ang), ey = ry * (float)Math.Sin(ang);
                float x = cx + ex * ct - ey * st, y = cy + ex * st + ey * ct;
                // Leave a gap where the orbit passes behind the planet.
                bool hidden = Len(x - cx, y - cy) < 14f && Math.Sin(ang) < 0;
                if (i > 0 && !hidden) FoilSegment(img, px, py, x, y, width, a);
                px = x; py = y;
            }
        }

        // ------------------------------------------------------------------ card icons (stand-ins for Fable's art)

        static void Icon(ArtImage img, ItemCategory category, float cx, float cy, float[] main, float[] accent)
        {
            // A very dark item colour would vanish into the dark window: lift it toward the accent.
            float lum = 0.3f * main[0] + 0.59f * main[1] + 0.11f * main[2];
            if (lum < 0.3f)
            {
                float k = 0.5f * (0.3f - lum) / 0.3f + 0.25f;
                main = new[] { main[0] + (accent[0] - main[0]) * k, main[1] + (accent[1] - main[1]) * k, main[2] + (accent[2] - main[2]) * k };
            }
            for (int y = (int)(cy - 100f); y < (int)(cy + 100f); y++)
            for (int x = (int)(cx - 110f); x < (int)(cx + 110f); x++)
            {
                if (x < 0 || y < 0 || x >= img.Width || y >= img.Height) continue;
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float shadow = IconShape(category, dx - 4f, dy - 6f, out _);
                if (shadow > 0f) img.Over(x, y, 0f, 0f, 0f, shadow * 0.45f);
            }
            for (int y = (int)(cy - 100f); y < (int)(cy + 100f); y++)
            for (int x = (int)(cx - 110f); x < (int)(cx + 110f); x++)
            {
                if (x < 0 || y < 0 || x >= img.Width || y >= img.Height) continue;
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float k = IconShape(category, dx, dy, out float accentShare);
                if (k <= 0f) continue;
                // Lit from above left, so it reads as an object, not a sticker.
                float light = 0.80f + 0.35f * Clamp01((-dx - dy) / 140f + 0.5f) - 0.15f;
                float r = (main[0] + (accent[0] - main[0]) * accentShare) * light;
                float g = (main[1] + (accent[1] - main[1]) * accentShare) * light;
                float b = (main[2] + (accent[2] - main[2]) * accentShare) * light;
                img.Over(x, y, Clamp01(r), Clamp01(g), Clamp01(b), k);
            }
        }

        /// <summary>Coverage of the stand-in icon at a point (pixels from the icon's middle), and how much of it is accent.</summary>
        static float IconShape(ItemCategory category, float x, float y, out float accentShare)
        {
            accentShare = 0f;
            switch (category)
            {
                case ItemCategory.Cosmetic:
                {
                    // A suit helmet: dome, visor, neck ring.
                    float dome = Clamp01(56f - Len(x, y + 6f) + 0.5f);
                    float visor = Clamp01(0.5f - RoundRect(x, y, -38f, -26f, 38f, 16f, 18f));
                    float ring = Clamp01(0.5f - RoundRect(x, y, -46f, 44f, 46f, 62f, 8f));
                    accentShare = Math.Max(visor, ring * 0.6f);
                    return Math.Max(dome, ring);
                }
                case ItemCategory.WeaponSkin:
                {
                    float body = Clamp01(0.5f - RoundRect(x, y, -78f, -22f, 44f, 6f, 6f));
                    float barrel = Clamp01(0.5f - RoundRect(x, y, 40f, -16f, 86f, -2f, 3f));
                    float grip = Clamp01(0.5f - Quad(x, y, -60f, 2f, -34f, 2f, -42f, 52f, -70f, 48f));
                    float mag = Clamp01(0.5f - Quad(x, y, -14f, 4f, 6f, 4f, 10f, 46f, -6f, 48f));
                    float stripe = Clamp01(0.5f - RoundRect(x, y, -70f, -14f, 30f, -6f, 3f));
                    accentShare = Math.Max(stripe, mag * 0.7f);
                    return Math.Max(Math.Max(body, barrel), Math.Max(grip, mag));
                }
                case ItemCategory.Upgrade:
                {
                    float disc = Clamp01(60f - Len(x, y) + 0.5f) - Clamp01(50f - Len(x, y) + 0.5f);
                    float c1 = Chevron(x, y + 14f), c2 = Chevron(x, y - 16f);
                    accentShare = Math.Max(c1, c2);
                    return Math.Max(disc, Math.Max(c1, c2));
                }
                case ItemCategory.ShipPaint:
                {
                    // A paint drop above a brushed stroke.
                    float drop = Clamp01(30f - Len(x, y + 4f) + 0.5f);
                    float tip = Clamp01(0.5f - Quad(x, y, -21f, -16f, 21f, -16f, 2f, -60f, -2f, -60f));
                    float stroke = Clamp01(0.5f - RoundRect(x, y, -80f, 40f, 80f, 60f, 10f)) * (0.75f + 0.25f * (float)Math.Sin(x * 0.4f));
                    accentShare = stroke > 0.5f ? 1f : 0f;
                    return Math.Max(Math.Max(drop, tip), stroke);
                }
                default:
                {
                    // A framed picture: frame, hills, a sun.
                    float outer = Clamp01(0.5f - RoundRect(x, y, -78f, -60f, 78f, 60f, 4f));
                    float inner = Clamp01(0.5f - RoundRect(x, y, -64f, -46f, 64f, 46f, 2f));
                    float frame = outer * (1f - inner);
                    float hill = inner * Clamp01(y - (20f - 18f * (float)Math.Cos(x * 0.05f)) + 0.5f);
                    float sun = inner * Clamp01(12f - Len(x - 30f, y + 18f) + 0.5f);
                    accentShare = Math.Max(frame, sun);
                    return Math.Max(frame, Math.Max(hill, sun));
                }
            }
        }

        static float Chevron(float x, float y)
        {
            float d = Math.Abs(Math.Abs(x) * 0.8f + y) - 7f;
            return Math.Abs(x) < 34f ? Clamp01(0.5f - d) : 0f;
        }

        // ------------------------------------------------------------------ small maths

        static float RoundRect(float x, float y, float l, float t, float r, float b, float radius)
        {
            float cx = (l + r) * 0.5f, cy = (t + b) * 0.5f;
            float hx = (r - l) * 0.5f - radius, hy = (b - t) * 0.5f - radius;
            float qx = Math.Abs(x - cx) - hx, qy = Math.Abs(y - cy) - hy;
            float outside = Len(Math.Max(qx, 0f), Math.Max(qy, 0f));
            return outside + Math.Min(Math.Max(qx, qy), 0f) - radius;
        }

        /// <summary>Signed distance to a convex four-sided shape given clockwise or anticlockwise.</summary>
        static float Quad(float x, float y, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3)
        {
            float[] px = { x0, x1, x2, x3 }, py = { y0, y1, y2, y3 };
            float area = 0f;
            for (int i = 0; i < 4; i++) area += px[i] * py[(i + 1) % 4] - px[(i + 1) % 4] * py[i];
            float s = area > 0f ? 1f : -1f;
            float d = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float ex = px[(i + 1) % 4] - px[i], ey = py[(i + 1) % 4] - py[i];
                float len = Len(ex, ey);
                float side = ((x - px[i]) * ey - (y - py[i]) * ex) / len * s;
                d = Math.Max(d, side);
            }
            return d;
        }

        static float SegmentDistance(float x, float y, float x0, float y0, float x1, float y1)
        {
            float vx = x1 - x0, vy = y1 - y0;
            float l2 = vx * vx + vy * vy;
            float t = l2 > 0f ? Clamp01(((x - x0) * vx + (y - y0) * vy) / l2) : 0f;
            return Len(x - (x0 + vx * t), y - (y0 + vy * t));
        }

        static float[] Rainbow(float u)
        {
            u = u - (float)Math.Floor(u);
            return new[]
            {
                0.5f + 0.5f * (float)Math.Cos(6.2832f * (u + 0.00f)),
                0.5f + 0.5f * (float)Math.Cos(6.2832f * (u + 0.33f)),
                0.5f + 0.5f * (float)Math.Cos(6.2832f * (u + 0.67f)),
            };
        }

        static float Len(float x, float y) => (float)Math.Sqrt(x * x + y * y);
        static float Dot(float[] a, float[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        static float[] Normalise(float x, float y, float z)
        {
            float l = (float)Math.Sqrt(x * x + y * y + z * z);
            return new[] { x / l, y / l, z / l };
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        static float Smooth(float a, float b, float v)
        {
            float t = Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Value noise summed over octaves, 0 to 1. Integer hashing only, so it is the same everywhere.</summary>
        static float Fbm(float x, float y, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(x, y, seed + o * 101);
                norm += amp;
                x *= 2.03f; y *= 2.03f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        static float ValueNoise(float x, float y, int seed)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float fx = x - xi, fy = y - yi;
            float a = Lattice(xi, yi, seed), b = Lattice(xi + 1, yi, seed);
            float c = Lattice(xi, yi + 1, seed), d = Lattice(xi + 1, yi + 1, seed);
            float ux = fx * fx * (3f - 2f * fx), uy = fy * fy * (3f - 2f * fy);
            return (a + (b - a) * ux) + ((c + (d - c) * ux) - (a + (b - a) * ux)) * uy;
        }

        static float Lattice(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144269504);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>A tiny repeatable random source for scattering stars (the picture must be the same everywhere).</summary>
        struct Hash
        {
            uint _s;
            public Hash(int seed) { _s = (uint)seed * 2654435761u + 1u; }

            public float Next01()
            {
                unchecked
                {
                    _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
                    return (_s & 0xFFFFFF) / 16777216f;
                }
            }
        }
    }
}
