using System;

namespace OrbitalAnte.Look
{
    /// <summary>One star of the sky: where it is (a direction of length 1 in the sky's own frame), how bright, what colour.</summary>
    public struct SkyStar
    {
        public float X, Y, Z;
        /// <summary>Apparent magnitude, the astronomers' scale: smaller is brighter. Sirius is -1.46; the eye stops near 6.5.</summary>
        public float Magnitude;
        /// <summary>The star's tint, each part 0 to 1, the brightest part 1. Pale: blue-white through white to orange.</summary>
        public float R, G, B;
    }

    /// <summary>
    /// The stars the game draws as points (Matthew, 26 Nov 2026: space should "look more like real space"). The file
    /// is <c>Resources/Sky/Stars.bytes</c>, written by <c>tools/sky/bake_sky.py</c>: first the real stars of the
    /// Bright Star Catalogue (public domain) at their true places, brightness and colour, brightest first, then
    /// fainter ones past the catalogue's reach, scattered by a seed and thicker along the Milky Way. The sky's
    /// frame is the game's: Y is the north pole of the planets' plane and X points at the March equinox, so the
    /// planets move through the constellations they really move through. Plain C#, no Unity types.
    ///
    /// Layout ("OAS1", little endian): the four letters, the number of stars, how many of them are real, then
    /// for each star three floats of direction, one float of magnitude, and four bytes: red, green, blue, spare.
    /// </summary>
    public sealed class StarCatalogue
    {
        public const string ResourcePath = "Sky/Stars";
        public const int BytesPerStar = 20;
        public const int HeaderBytes = 12;
        /// <summary>More stars than this is a mistake in the bake: every star is four corners on the graphics card.</summary>
        public const int MostStars = 40000;

        public SkyStar[] Stars = Array.Empty<SkyStar>();
        /// <summary>How many stars, from the start of the list, are real ones from the catalogue.</summary>
        public int RealCount;

        /// <summary>Reads the file. Returns null and says why in <paramref name="problem"/> if it is not a star file.</summary>
        public static StarCatalogue Parse(byte[] bytes, out string problem)
        {
            problem = null;
            if (bytes == null || bytes.Length < HeaderBytes) { problem = "the file is empty or cut short"; return null; }
            if (bytes[0] != (byte)'O' || bytes[1] != (byte)'A' || bytes[2] != (byte)'S' || bytes[3] != (byte)'1')
            {
                problem = "the file does not start with OAS1";
                return null;
            }
            int count = BitConverter.ToInt32(bytes, 4);
            int real = BitConverter.ToInt32(bytes, 8);
            if (count < 0 || count > MostStars) { problem = "it claims " + count + " stars (at most " + MostStars + ")"; return null; }
            if (real < 0 || real > count) { problem = "it claims " + real + " real stars out of " + count; return null; }
            if (bytes.Length != HeaderBytes + count * BytesPerStar) { problem = "its length does not fit " + count + " stars"; return null; }

            var stars = new SkyStar[count];
            for (int i = 0; i < count; i++)
            {
                int o = HeaderBytes + i * BytesPerStar;
                float x = BitConverter.ToSingle(bytes, o);
                float y = BitConverter.ToSingle(bytes, o + 4);
                float z = BitConverter.ToSingle(bytes, o + 8);
                float magnitude = BitConverter.ToSingle(bytes, o + 12);
                double length = Math.Sqrt((double)x * x + (double)y * y + (double)z * z);
                if (!(length > 0.99 && length < 1.01) || float.IsNaN(magnitude))
                {
                    problem = "star " + i + " has no direction or no brightness";
                    return null;
                }
                stars[i] = new SkyStar
                {
                    X = (float)(x / length), Y = (float)(y / length), Z = (float)(z / length),
                    Magnitude = magnitude,
                    R = bytes[o + 16] / 255f, G = bytes[o + 17] / 255f, B = bytes[o + 18] / 255f,
                };
            }
            return new StarCatalogue { Stars = stars, RealCount = real };
        }
    }

    /// <summary>
    /// How a star of a given magnitude is drawn: a soft round dot whose size is in screen dots, so a star is as
    /// crisp on any screen, and whose brightness follows the star's real light, squeezed so the faintest still
    /// show and the brightest bloom without turning into lamps. Plain sums, shared by the game and its tests;
    /// <c>tools/sky/preview_sky.py</c> keeps a copy for its pictures.
    /// </summary>
    public static class StarLook
    {
        /// <summary>Half the width of the faintest star's square, in screen dots at 800 dots of height.</summary>
        public const float SmallestHalfSize = 1.6f;
        public const float LargestHalfSize = 4f;
        /// <summary>
        /// The brightest a star's middle is drawn, as a multiple of white (above 1 it blooms). Held low, with
        /// the size above, so that no star outshines a far-off planet: a planet is the thing to find in the sky.
        /// </summary>
        public const float BrightestPeak = 4f;

        /// <summary>Half the width of the star's square in screen dots. Only stars brighter than magnitude 3.5 grow.</summary>
        public static float HalfSize(float magnitude)
        {
            float size = SmallestHalfSize + 0.55f * (3.5f - magnitude);
            return size < SmallestHalfSize ? SmallestHalfSize : size > LargestHalfSize ? LargestHalfSize : size;
        }

        /// <summary>
        /// The brightness of the star's middle (1 is white). Real light falls by 2.512 times a magnitude; here it
        /// falls by about 1.9 times, so the sky keeps a real night's huge spread of brightness without the faint
        /// stars vanishing on a small screen.
        /// </summary>
        public static float Peak(float magnitude)
        {
            double light = Math.Pow(10.0, -0.4 * (magnitude - 6.5));
            double peak = 0.12 * Math.Pow(light, 0.7);
            return (float)(peak > BrightestPeak ? BrightestPeak : peak);
        }
    }
}
