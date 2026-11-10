using System;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// A 3D vector in double precision. The solar system is tens of kilometres across and Unity's own vectors
    /// (single precision) start to wobble at that range, so the simulation keeps its sums in these and only
    /// converts to Unity's vectors at the very end, close to the player. No Unity types.
    /// </summary>
    public struct Vec3d : IEquatable<Vec3d>
    {
        public double X;
        public double Y;
        public double Z;

        public Vec3d(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Vec3d Zero = new Vec3d(0, 0, 0);
        public static readonly Vec3d Up = new Vec3d(0, 1, 0);
        public static readonly Vec3d Right = new Vec3d(1, 0, 0);
        public static readonly Vec3d Forward = new Vec3d(0, 0, 1);

        public double SqrMagnitude => X * X + Y * Y + Z * Z;
        public double Magnitude => Math.Sqrt(SqrMagnitude);

        public Vec3d Normalized
        {
            get
            {
                double m = Magnitude;
                return m > 1e-300 ? new Vec3d(X / m, Y / m, Z / m) : Zero;
            }
        }

        public static Vec3d operator +(Vec3d a, Vec3d b) => new Vec3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3d operator -(Vec3d a, Vec3d b) => new Vec3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3d operator -(Vec3d a) => new Vec3d(-a.X, -a.Y, -a.Z);
        public static Vec3d operator *(Vec3d a, double s) => new Vec3d(a.X * s, a.Y * s, a.Z * s);
        public static Vec3d operator *(double s, Vec3d a) => new Vec3d(a.X * s, a.Y * s, a.Z * s);
        public static Vec3d operator /(Vec3d a, double s) => new Vec3d(a.X / s, a.Y / s, a.Z / s);

        public static double Dot(Vec3d a, Vec3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3d Cross(Vec3d a, Vec3d b)
        {
            return new Vec3d(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }

        public static double Distance(Vec3d a, Vec3d b) => (a - b).Magnitude;

        /// <summary>
        /// This vector turned by <paramref name="radians"/> about <paramref name="unitAxis"/>. Uses the same
        /// convention as Unity's Quaternion.AngleAxis, so the two can be mixed.
        /// </summary>
        public Vec3d RotatedAround(Vec3d unitAxis, double radians)
        {
            double c = Math.Cos(radians);
            double s = Math.Sin(radians);
            return this * c + Cross(unitAxis, this) * s + unitAxis * (Dot(unitAxis, this) * (1.0 - c));
        }

        /// <summary>Any unit vector at right angles to this one.</summary>
        public Vec3d AnyPerpendicular()
        {
            Vec3d other = Math.Abs(Y) < 0.9 * Magnitude ? Up : Right;
            return Cross(this, other).Normalized;
        }

        public bool Equals(Vec3d other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is Vec3d other && Equals(other);
        public override int GetHashCode() => X.GetHashCode() ^ (Y.GetHashCode() * 397) ^ (Z.GetHashCode() * 7919);
        public override string ToString() => "(" + X.ToString("0.###") + ", " + Y.ToString("0.###") + ", " + Z.ToString("0.###") + ")";
    }

    /// <summary>A rotation in double precision. Same conventions as Unity's Quaternion. No Unity types.</summary>
    public struct QuatD
    {
        public double X;
        public double Y;
        public double Z;
        public double W;

        public QuatD(double x, double y, double z, double w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static readonly QuatD Identity = new QuatD(0, 0, 0, 1);

        public static QuatD AngleAxis(Vec3d unitAxis, double radians)
        {
            double s = Math.Sin(radians * 0.5);
            return new QuatD(unitAxis.X * s, unitAxis.Y * s, unitAxis.Z * s, Math.Cos(radians * 0.5));
        }

        /// <summary>The shortest rotation that turns unit vector <paramref name="from"/> onto unit vector <paramref name="to"/>.</summary>
        public static QuatD FromTo(Vec3d from, Vec3d to)
        {
            double dot = Vec3d.Dot(from, to);
            if (dot > 0.999999999) return Identity;
            if (dot < -0.999999999) return AngleAxis(from.AnyPerpendicular(), Math.PI);
            Vec3d axis = Vec3d.Cross(from, to).Normalized;
            return AngleAxis(axis, Math.Acos(Math.Max(-1.0, Math.Min(1.0, dot))));
        }

        /// <summary>Combined rotation: first <paramref name="b"/>, then <paramref name="a"/> (as in Unity).</summary>
        public static QuatD operator *(QuatD a, QuatD b)
        {
            return new QuatD(
                a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
                a.W * b.Y + a.Y * b.W + a.Z * b.X - a.X * b.Z,
                a.W * b.Z + a.Z * b.W + a.X * b.Y - a.Y * b.X,
                a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);
        }

        public QuatD Inverse => new QuatD(-X, -Y, -Z, W);

        public QuatD Normalized
        {
            get
            {
                double m = Math.Sqrt(X * X + Y * Y + Z * Z + W * W);
                return m > 1e-300 ? new QuatD(X / m, Y / m, Z / m, W / m) : Identity;
            }
        }

        public Vec3d Rotate(Vec3d v)
        {
            var q = new Vec3d(X, Y, Z);
            Vec3d t = 2.0 * Vec3d.Cross(q, v);
            return v + W * t + Vec3d.Cross(q, t);
        }
    }
}
