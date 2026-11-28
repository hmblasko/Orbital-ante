using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Playtest
{
    /// <summary>
    /// When something happened, on the two clocks the playtest log keeps (Ralph loop pass 9, design review): real seconds
    /// from the run's start, pauses included, so every line lines up with a screen recording started with the run; and
    /// game seconds, which stop for pauses and slow with Focus and a hitstop, as the creatures', the guns' and the
    /// readout's own clocks do. Windows (a strike's grace, a fight's hold) and how long things took (a kill, a dodge) are
    /// measured in game seconds, so Focus never stretches them. A plain number is both (the rule tests, which have no
    /// game clock).
    /// </summary>
    public readonly struct Moment
    {
        public readonly float Real, Game;

        public Moment(float real, float game)
        {
            Real = real;
            Game = game;
        }

        public static implicit operator Moment(float t) => new Moment(t, t);
    }

    /// <summary>
    /// What counts as the player moving out of a creature's way (pass 2's protocol: "move on the mark"): their speed
    /// across the line between them and the creature, along the ground, smoothed so that a footstep's bob or one long
    /// frame is not a move. The numbers are a first guess (walking is 5 m/s, a crouch 2.75, sneaking 2.25, prone 1.1):
    /// a deliberate step aside from a walk or a crouch passes it, standing still, turning and lying prone do not.
    /// Plain C#.
    /// </summary>
    public static class DodgeRules
    {
        /// <summary>Metres a (game) second across the line: over this, the player is moving out of its way.</summary>
        public const float SidestepSpeed = 2f;
        /// <summary>Seconds over which the speed is smoothed.</summary>
        public const float Smoothing = 0.08f;

        /// <summary>
        /// The player's speed across the line to a creature, from where the player stood against it a frame ago and now
        /// (<c>player minus creature</c>, so a re-centre of the world and the world's own motion cancel out), in metres a
        /// second. Along the ground: what is along <paramref name="up"/> is left out; a zero <paramref name="up"/> (a
        /// swimmer, where across the water counts in every direction) leaves nothing out.
        /// </summary>
        public static double AcrossSpeed(Vec3d before, Vec3d now, Vec3d up, double dt)
        {
            if (!(dt > 1e-5)) return 0.0;
            Vec3d v = (now - before) / dt;
            Vec3d line = now;
            double upLength = up.Magnitude;
            if (upLength > 1e-9)
            {
                Vec3d u = up / upLength;
                v -= u * Vec3d.Dot(v, u);
                line -= u * Vec3d.Dot(line, u);
            }
            double length = line.Magnitude;
            if (length < 1e-6) return v.Magnitude;
            line /= length;
            return (v - line * Vec3d.Dot(v, line)).Magnitude;
        }

        /// <summary>The smoothed speed after one frame of <paramref name="dt"/> game seconds at <paramref name="speed"/>.</summary>
        public static float Smooth(float smoothed, float speed, float dt)
        {
            if (!(dt > 0f)) return smoothed;
            return smoothed + (speed - smoothed) * (1f - (float)Math.Exp(-dt / Smoothing));
        }
    }

    /// <summary>
    /// Whether the ground is between the player's eye and something on a world (design review, pass 9: an animal woken
    /// 70 to 110 m off can be in the frame of the view and still under the horizon, a Strider's top from 49 m on the
    /// Moon). Points are in the world's own space, metres from its middle; <c>groundRadius</c> says how far the ground is
    /// from the middle in a direction (the game gives it the ground really built). Hills and the horizon are checked;
    /// rocks, trees, buildings, night and water are not. Plain C#.
    /// </summary>
    public static class SightLine
    {
        /// <summary>Metres between the points looked at along the line.</summary>
        public const double Step = 2.0;
        /// <summary>At most this many points are looked at, however long the line.</summary>
        public const int MostPoints = 64;

        /// <summary>
        /// True when no point along the line from <paramref name="eye"/> to <paramref name="target"/> (the two ends left
        /// out) is under the ground by more than <paramref name="give"/> metres.
        /// </summary>
        public static bool ClearOfGround(Vec3d eye, Vec3d target, Func<Vec3d, double> groundRadius, double give = 0.0)
        {
            if (groundRadius == null) return true;
            Vec3d d = target - eye;
            double length = d.Magnitude;
            if (length < Step) return true;
            int n = (int)Math.Ceiling(length / Step);
            if (n > MostPoints) n = MostPoints;
            for (int i = 1; i < n; i++)
            {
                Vec3d p = eye + d * (i / (double)n);
                double r = p.Magnitude;
                if (r < 1e-9) return false;
                if (r + give < groundRadius(p / r)) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// The last second or so of frame times, for a baseline beside the frame a creature woke in (design review, pass 9:
    /// a development build's frames are slower than a release's, so the wake is told against the frames just before it).
    /// Plain C#; nothing is made while it runs.
    /// </summary>
    public sealed class FrameTimes
    {
        public const int Size = 64;
        readonly float[] _ms = new float[Size];
        readonly float[] _sorted = new float[Size];
        int _next, _count;

        public int Count => _count;

        public void Add(float ms)
        {
            _ms[_next] = ms;
            _next = (_next + 1) % Size;
            if (_count < Size) _count++;
        }

        /// <summary>The middle of the frames kept (the mean of the middle two for an even count); -1 with none.</summary>
        public float Median()
        {
            if (_count == 0) return -1f;
            // Only the frames kept: the newest _count of them.
            int start = (_next - _count + Size) % Size;
            for (int i = 0; i < _count; i++) _sorted[i] = _ms[(start + i) % Size];
            Array.Sort(_sorted, 0, _count);
            int mid = _count / 2;
            return _count % 2 == 1 ? _sorted[mid] : 0.5f * (_sorted[mid - 1] + _sorted[mid]);
        }

        public void Clear()
        {
            _next = 0;
            _count = 0;
        }
    }
}
