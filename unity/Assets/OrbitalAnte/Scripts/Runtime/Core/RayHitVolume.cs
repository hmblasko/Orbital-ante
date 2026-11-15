using System;

namespace OrbitalAnte.Core
{
    /// <summary>Ray entry distances for logical hit volumes, independent of Unity's physics objects.</summary>
    public static class RayHitVolume
    {
        /// <summary>
        /// Finds the first entry into a sphere, including its surface, within the inclusive range.
        /// Direction need not be normalized. An inside start returns zero; invalid inputs return false.
        /// </summary>
        public static bool Sphere(Vec3d origin, Vec3d direction, double range, Vec3d center,
            double radius, out double distance)
        {
            distance = 0;
            if (!Valid(origin) || !Valid(center) || !ValidRange(range, radius)
                || !Unit(direction, out Vec3d ray)) return false;
            Vec3d relative = center - origin;
            if (!Valid(relative)) return false;
            double scale = Math.Max(Largest(relative), radius);
            if (scale == 0) return true;
            double entry = SphereEntry(relative / scale, ray, radius / scale);
            return Finish(entry, scale, range, out distance);
        }

        /// <summary>
        /// Finds the first entry into the capsule swept by a sphere of radius along segment a to b.
        /// Its axis may point in any direction. Equal endpoints give a sphere; inside starts return zero.
        /// Range and radius must be finite and nonnegative, and direction finite and nonzero.
        /// </summary>
        public static bool Capsule(Vec3d origin, Vec3d direction, double range, Vec3d a, Vec3d b,
            double radius, out double distance)
        {
            distance = 0;
            if (!Valid(origin) || !Valid(a) || !Valid(b) || !ValidRange(range, radius)
                || !Unit(direction, out Vec3d ray)) return false;
            a -= origin;
            b -= origin;
            if (!Valid(a) || !Valid(b)) return false;
            // Work near the origin with bounded squared terms, then restore world-distance units.
            double scale = Math.Max(Math.Max(Largest(a), Largest(b)), radius);
            if (scale == 0) return true;
            a /= scale;
            b /= scale;
            radius /= scale;

            Vec3d segment = b - a;
            double length = segment.Magnitude;
            if (length == 0) return Finish(SphereEntry(a, ray, radius), scale, range, out distance);
            Vec3d axis = segment / length;
            double height = Vec3d.Dot(-a, axis);
            Vec3d nearest = a + axis * Math.Max(0, Math.Min(length, height));
            if (nearest.SqrMagnitude <= radius * radius) return true;

            // A capsule is the union of the finite cylinder and its two endpoint spheres.
            double entry = Math.Min(SphereEntry(a, ray, radius), SphereEntry(b, ray, radius));
            double along = Vec3d.Dot(ray, axis);
            Vec3d radialOrigin = -a - axis * height;
            Vec3d radialDirection = ray - axis * along;
            double quadratic = radialDirection.SqrMagnitude;
            // No arbitrary near-parallel cutoff: a shallow ray can still enter a long capsule's side.
            if (quadratic > 0)
            {
                double middle = -Vec3d.Dot(radialOrigin, radialDirection) / quadratic;
                if (Valid(middle))
                {
                    Vec3d closest = radialOrigin + radialDirection * middle;
                    double squared = RadiusRemainder(radius, closest.SqrMagnitude);
                    if (squared >= 0)
                    {
                        // These are the two roots of the radial quadratic, expressed around closest approach.
                        double half = Math.Sqrt(squared / quadratic);
                        SideEntry(middle - half, height, along, length, ref entry);
                        SideEntry(middle + half, height, along, length, ref entry);
                    }
                }
            }
            return Finish(entry, scale, range, out distance);
        }

        static double SphereEntry(Vec3d center, Vec3d ray, double radius)
        {
            if (center.SqrMagnitude <= radius * radius) return 0;
            double middle = Vec3d.Dot(center, ray);
            if (middle < 0) return double.PositiveInfinity;
            // Using perpendicular distance avoids subtracting two huge, almost equal quadratic terms.
            Vec3d closest = center - ray * middle;
            double squared = RadiusRemainder(radius, closest.SqrMagnitude);
            if (squared < 0) return double.PositiveInfinity;
            return Math.Max(0, middle - Math.Sqrt(squared));
        }

        static double RadiusRemainder(double radius, double closestSquared)
        {
            double radiusSquared = radius * radius;
            double remainder = radiusSquared - closestSquared;
            // Roundoff at a tangent must not turn a contact into a miss. This is relative arithmetic slack,
            // not an extra world-space hit radius; a resolvable near miss remains outside.
            double roundoff = 16 * 2.2204460492503131e-16 * Math.Max(radiusSquared, closestSquared);
            return remainder < 0 && remainder >= -roundoff ? 0 : remainder;
        }

        static void SideEntry(double candidate, double height, double along, double length, ref double entry)
        {
            if (!Valid(candidate) || candidate < 0 || candidate >= entry) return;
            double at = height + candidate * along;
            if (at >= 0 && at <= length) entry = candidate;
        }

        static bool Finish(double entry, double scale, double range, out double distance)
        {
            double candidate = entry * scale;
            distance = 0;
            if (!Valid(candidate) || candidate < 0 || candidate > range) return false;
            distance = candidate;
            return true;
        }

        static bool Unit(Vec3d value, out Vec3d unit)
        {
            unit = Vec3d.Zero;
            if (!Valid(value)) return false;
            double largest = Largest(value);
            if (largest == 0) return false;
            value /= largest;
            unit = value / value.Magnitude;
            return true;
        }

        static double Largest(Vec3d value) => Math.Max(Math.Abs(value.X), Math.Max(Math.Abs(value.Y), Math.Abs(value.Z)));
        static bool ValidRange(double range, double radius) => Valid(range) && range >= 0 && Valid(radius) && radius >= 0;
        static bool Valid(Vec3d value) => Valid(value.X) && Valid(value.Y) && Valid(value.Z);
        static bool Valid(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
