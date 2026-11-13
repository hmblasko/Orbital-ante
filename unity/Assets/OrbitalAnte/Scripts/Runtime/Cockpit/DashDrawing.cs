using System;
using System.Collections.Generic;

namespace OrbitalAnte.Cockpit
{
    public struct DashColor
    {
        public float R, G, B, A;
        public DashColor(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }
        public DashColor Fade(float a) => new DashColor(R, G, B, A * a);
        public static DashColor Mix(DashColor a, DashColor b, float t) =>
            new DashColor(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
    }

    public enum DashAlign { Left, Centre, Right }

    public struct DashText
    {
        public float X, Y, Size;
        public DashAlign Align;
        public DashColor Color;
        public string Text;
        public bool Bold;
    }

    /// <summary>
    /// One picture of the dash: a list of coloured triangles and a list of words, in the dash's own flat
    /// space (x right, y up, <see cref="Width"/> by <see cref="Height"/>). Plain C#, no Unity types, so the
    /// same picture can be shown in the game and drawn to a file for a preview. Cleared and refilled each time;
    /// its arrays are kept, so drawing makes no rubbish once warm.
    /// </summary>
    public sealed class DashDrawing
    {
        public const float Width = 1000f;
        public const float Height = 300f;
        public const float MinTextSize = 14f;

        public float[] X = new float[3000], Y = new float[3000];
        public DashColor[] Colors = new DashColor[3000];
        public int VertexCount { get; private set; }
        public readonly List<DashText> Texts = new List<DashText>(48);
        /// <summary>Where the camera picture goes, if this design has a window for it (width 0 if not).</summary>
        public float CameraX, CameraY, CameraWidth, CameraHeight;

        public void Clear()
        {
            VertexCount = 0;
            Texts.Clear();
            CameraWidth = 0f;
        }

        /// <summary>
        /// A number that changes when the picture does. Lets the real dash skip the work of redrawing a picture
        /// that is the same as the last one, which is most of them when you are coasting.
        /// </summary>
        public int Fingerprint()
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < VertexCount; i++)
                {
                    h = (h ^ (uint)BitConverter.SingleToInt32Bits(X[i])) * 16777619u;
                    h = (h ^ (uint)BitConverter.SingleToInt32Bits(Y[i])) * 16777619u;
                    DashColor c = Colors[i];
                    h = (h ^ (uint)BitConverter.SingleToInt32Bits(c.R + c.G * 3f + c.B * 7f + c.A * 13f)) * 16777619u;
                }
                h = (h ^ (uint)VertexCount) * 16777619u;
                h = (h ^ (uint)BitConverter.SingleToInt32Bits(CameraX + CameraWidth * 3f)) * 16777619u;
                return (int)h;
            }
        }

        void Push(float x, float y, DashColor c)
        {
            if (VertexCount == X.Length)
            {
                Array.Resize(ref X, X.Length * 2);
                Array.Resize(ref Y, Y.Length * 2);
                Array.Resize(ref Colors, Colors.Length * 2);
            }
            X[VertexCount] = x;
            Y[VertexCount] = y;
            Colors[VertexCount] = c;
            VertexCount++;
        }

        public void Triangle(float ax, float ay, float bx, float by, float cx, float cy, DashColor c)
        {
            Push(ax, ay, c);
            Push(bx, by, c);
            Push(cx, cy, c);
        }

        public void Quad(float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy, DashColor c)
        {
            Triangle(ax, ay, bx, by, cx, cy, c);
            Triangle(ax, ay, cx, cy, dx, dy, c);
        }

        public void Rect(float x, float y, float w, float h, DashColor c) => Quad(x, y, x + w, y, x + w, y + h, x, y + h, c);

        /// <summary>A box with its corners rounded off.</summary>
        public void Panel(float x, float y, float w, float h, float r, DashColor c)
        {
            r = Math.Min(r, Math.Min(w, h) * 0.5f);
            Rect(x + r, y, w - 2f * r, h, c);
            Rect(x, y + r, r, h - 2f * r, c);
            Rect(x + w - r, y + r, r, h - 2f * r, c);
            Pie(x + r, y + r, r, 180f, 270f, c, 5);
            Pie(x + w - r, y + r, r, 270f, 360f, c, 5);
            Pie(x + w - r, y + h - r, r, 0f, 90f, c, 5);
            Pie(x + r, y + h - r, r, 90f, 180f, c, 5);
        }

        public void Disc(float cx, float cy, float r, DashColor c, int steps = 28) => Pie(cx, cy, r, 0f, 360f, c, steps);

        /// <summary>A filled wedge. Angles in degrees, anticlockwise from "east".</summary>
        public void Pie(float cx, float cy, float r, float from, float to, DashColor c, int steps = 16)
        {
            for (int i = 0; i < steps; i++)
            {
                double a = (from + (to - from) * i / steps) * Math.PI / 180.0, b = (from + (to - from) * (i + 1) / steps) * Math.PI / 180.0;
                Triangle(cx, cy, cx + r * (float)Math.Cos(a), cy + r * (float)Math.Sin(a), cx + r * (float)Math.Cos(b), cy + r * (float)Math.Sin(b), c);
            }
        }

        /// <summary>Part of a ring. Angles in degrees, anticlockwise from "east".</summary>
        public void Arc(float cx, float cy, float inner, float outer, float from, float to, DashColor c, int steps = 24)
        {
            for (int i = 0; i < steps; i++)
            {
                double a = (from + (to - from) * i / steps) * Math.PI / 180.0, b = (from + (to - from) * (i + 1) / steps) * Math.PI / 180.0;
                float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a), cb = (float)Math.Cos(b), sb = (float)Math.Sin(b);
                Quad(cx + inner * ca, cy + inner * sa, cx + outer * ca, cy + outer * sa, cx + outer * cb, cy + outer * sb, cx + inner * cb, cy + inner * sb, c);
            }
        }

        public void Ring(float cx, float cy, float inner, float outer, DashColor c, int steps = 32) => Arc(cx, cy, inner, outer, 0f, 360f, c, steps);

        public void Line(float ax, float ay, float bx, float by, float width, DashColor c)
        {
            float dx = bx - ax, dy = by - ay;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-4f) return;
            float nx = -dy / length * width * 0.5f, ny = dx / length * width * 0.5f;
            Quad(ax - nx, ay - ny, bx - nx, by - ny, bx + nx, by + ny, ax + nx, ay + ny, c);
        }

        /// <summary>A needle from a hub: thick at the hub, a point at the tip. Angle in degrees, anticlockwise from "east".</summary>
        public void Needle(float cx, float cy, float length, float angle, float width, DashColor c)
        {
            double a = angle * Math.PI / 180.0;
            float ux = (float)Math.Cos(a), uy = (float)Math.Sin(a);
            float nx = -uy * width * 0.5f, ny = ux * width * 0.5f;
            Triangle(cx - nx - ux * width, cy - ny - uy * width, cx + ux * length, cy + uy * length, cx + nx - ux * width, cy + ny - uy * width, c);
        }

        public void Text(float x, float y, float size, string text, DashColor c, DashAlign align = DashAlign.Centre, bool bold = true)
        {
            // A blank word still takes its place in the list: the real dash matches words to screen objects by
            // their place, so a word that comes and goes must not shuffle the ones after it.
            if (text == null) text = "";
            // Nothing smaller than this survives a Steam Deck screen at arm's length.
            if (size < MinTextSize) size = MinTextSize;
            Texts.Add(new DashText { X = x, Y = y, Size = size, Text = text, Color = c, Align = align, Bold = bold });
        }
    }
}
