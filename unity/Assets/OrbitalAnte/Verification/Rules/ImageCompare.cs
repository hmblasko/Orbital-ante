using System;

namespace OrbitalAnte.Verification
{
    /// <summary>Numbers describing one picture, used to catch blank, pink or black frames before anyone scores them.</summary>
    public struct ImageStats
    {
        public double MeanLuma;        // 0..1
        public double LumaSpread;      // standard deviation, 0..0.5
        public double BlackFraction;   // pixels darker than 2%
        public double MagentaFraction; // Unity's "shader failed" pink
        public bool LooksBlank => LumaSpread < 0.004;
        public bool LooksPink => MagentaFraction > 0.02;
    }

    /// <summary>The difference between a baseline picture and a candidate taken from the same camera.</summary>
    public struct ImageDifference
    {
        public bool SameSize;
        /// <summary>Root mean square error over all channels, 0 (identical) to 1.</summary>
        public double Rmse;
        /// <summary>Share of pixels whose colour moved by more than the tolerance.</summary>
        public double ChangedFraction;
        /// <summary>Structural similarity of brightness, 1 = same structure, lower = shapes moved or changed.</summary>
        public double Ssim;
        /// <summary>The box (in pixels, origin at the first row of the data) round everything that changed. Zero size if nothing did.</summary>
        public int MinX, MinY, MaxX, MaxY;
        /// <summary>How far the outline of the non-background shape moved, as a share of the picture (0 = same silhouette).</summary>
        public double SilhouetteChange;
    }

    /// <summary>
    /// Picture comparison on plain RGB bytes (3 per pixel, row after row), with no Unity types. The numbers are
    /// supporting evidence for the Critic, never a verdict: a big difference can be the improvement that was
    /// asked for, and a tiny one can hide a real defect.
    /// </summary>
    public static class ImageCompare
    {
        static double Luma(byte[] rgb, int i) => (0.2126 * rgb[i] + 0.7152 * rgb[i + 1] + 0.0722 * rgb[i + 2]) / 255.0;

        public static ImageStats Stats(byte[] rgb, int width, int height)
        {
            int n = width * height;
            if (rgb == null || n <= 0 || rgb.Length < n * 3) throw new ArgumentException("Picture data does not match its size.");
            double sum = 0, sumSq = 0;
            int black = 0, magenta = 0;
            for (int p = 0, i = 0; p < n; p++, i += 3)
            {
                double y = Luma(rgb, i);
                sum += y;
                sumSq += y * y;
                if (y < 0.02) black++;
                if (rgb[i] > 230 && rgb[i + 1] < 40 && rgb[i + 2] > 230) magenta++;
            }
            double mean = sum / n;
            return new ImageStats
            {
                MeanLuma = mean,
                LumaSpread = Math.Sqrt(Math.Max(0, sumSq / n - mean * mean)),
                BlackFraction = black / (double)n,
                MagentaFraction = magenta / (double)n,
            };
        }

        /// <param name="tolerance">How far one channel may move (0..255) before a pixel counts as changed.</param>
        /// <param name="backgroundRgb">Studio backdrop colour for the silhouette measure, or null to skip it.</param>
        public static ImageDifference Compare(byte[] a, byte[] b, int width, int height, int tolerance = 8, byte[] backgroundRgb = null)
        {
            var d = new ImageDifference { MinX = 0, MinY = 0, MaxX = -1, MaxY = -1 };
            int n = width * height;
            if (a == null || b == null || a.Length != b.Length || a.Length < n * 3) return d;
            d.SameSize = true;

            double sq = 0;
            int changed = 0, silhouette = 0;
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0, i = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++, i += 3)
                {
                    int dr = a[i] - b[i], dg = a[i + 1] - b[i + 1], db = a[i + 2] - b[i + 2];
                    sq += dr * dr + dg * dg + db * db;
                    if (Math.Abs(dr) > tolerance || Math.Abs(dg) > tolerance || Math.Abs(db) > tolerance)
                    {
                        changed++;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                    if (backgroundRgb != null && IsBackground(a, i, backgroundRgb) != IsBackground(b, i, backgroundRgb)) silhouette++;
                }
            }
            d.Rmse = Math.Sqrt(sq / (n * 3.0)) / 255.0;
            d.ChangedFraction = changed / (double)n;
            d.SilhouetteChange = silhouette / (double)n;
            if (maxX >= 0) { d.MinX = minX; d.MinY = minY; d.MaxX = maxX; d.MaxY = maxY; }
            d.Ssim = Ssim(a, b, width, height);
            return d;
        }

        static bool IsBackground(byte[] rgb, int i, byte[] bg)
            => Math.Abs(rgb[i] - bg[0]) <= 10 && Math.Abs(rgb[i + 1] - bg[1]) <= 10 && Math.Abs(rgb[i + 2] - bg[2]) <= 10;

        /// <summary>Mean structural similarity of brightness over 8 x 8 blocks.</summary>
        public static double Ssim(byte[] a, byte[] b, int width, int height)
        {
            const int block = 8;
            const double c1 = 0.01 * 0.01, c2 = 0.03 * 0.03;
            double total = 0;
            int blocks = 0;
            for (int by = 0; by + block <= height; by += block)
            {
                for (int bx = 0; bx + block <= width; bx += block)
                {
                    double sa = 0, sb = 0, saa = 0, sbb = 0, sab = 0;
                    for (int y = by; y < by + block; y++)
                    {
                        int i = (y * width + bx) * 3;
                        for (int x = 0; x < block; x++, i += 3)
                        {
                            double la = Luma(a, i), lb = Luma(b, i);
                            sa += la; sb += lb; saa += la * la; sbb += lb * lb; sab += la * lb;
                        }
                    }
                    const double m = block * block;
                    double ma = sa / m, mb = sb / m;
                    double va = saa / m - ma * ma, vb = sbb / m - mb * mb, cov = sab / m - ma * mb;
                    total += (2 * ma * mb + c1) * (2 * cov + c2) / ((ma * ma + mb * mb + c1) * (va + vb + c2));
                    blocks++;
                }
            }
            return blocks == 0 ? 1 : total / blocks;
        }

        /// <summary>
        /// A picture of where two pictures differ: the candidate in dim grey, with changed pixels in red
        /// (stronger red = bigger change). Same layout as the inputs.
        /// </summary>
        public static byte[] DifferenceImage(byte[] a, byte[] b, int width, int height, int tolerance = 8)
        {
            int n = width * height;
            var o = new byte[n * 3];
            if (a == null || b == null || a.Length != b.Length || a.Length < n * 3) return o;
            for (int p = 0, i = 0; p < n; p++, i += 3)
            {
                int diff = Math.Max(Math.Abs(a[i] - b[i]), Math.Max(Math.Abs(a[i + 1] - b[i + 1]), Math.Abs(a[i + 2] - b[i + 2])));
                byte grey = (byte)(Luma(b, i) * 110);
                if (diff > tolerance)
                {
                    o[i] = (byte)Math.Min(255, 120 + diff);
                    o[i + 1] = (byte)(grey / 3);
                    o[i + 2] = (byte)(grey / 3);
                }
                else o[i] = o[i + 1] = o[i + 2] = grey;
            }
            return o;
        }

        /// <summary>A plain-words reading of a difference, for the comparison report. Still only a flag for a person or the Critic to interpret.</summary>
        public static string Describe(ImageDifference d)
        {
            if (!d.SameSize) return "different size: cannot compare";
            if (d.ChangedFraction < 0.0005) return "no visible change";
            if (d.ChangedFraction < 0.01 && d.Ssim > 0.98) return "tiny local change";
            if (d.Ssim > 0.9) return "moderate change, same overall structure";
            return "large change: shapes, lighting or camera differ";
        }
    }
}
