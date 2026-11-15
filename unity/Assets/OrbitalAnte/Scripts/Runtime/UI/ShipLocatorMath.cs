using System;
using System.Globalization;
using OrbitalAnte.Core;

namespace OrbitalAnte.UI
{
    /// <summary>A point on the locator's canvas, measured from its centre. No Unity types.</summary>
    public struct ShipLocatorPoint
    {
        public double X, Y, Angle;
        public bool AtTarget, Valid;
    }

    /// <summary>The rounded reading is also its cache key, so an unchanged label needs no new string.</summary>
    public struct ShipLocatorDistance : IEquatable<ShipLocatorDistance>
    {
        public long Value;
        public bool Kilometres, Valid;

        public bool Equals(ShipLocatorDistance other) => Value == other.Value && Kilometres == other.Kilometres && Valid == other.Valid;
        public override bool Equals(object other) => other is ShipLocatorDistance reading && Equals(reading);
        public override int GetHashCode() => Value.GetHashCode() ^ (Kilometres ? 1 : 0) ^ (Valid ? 2 : 0);

        public override string ToString()
        {
            if (!Valid) return string.Empty;
            if (!Kilometres) return Value.ToString(CultureInfo.InvariantCulture) + " m";
            return (Value / 10).ToString(CultureInfo.InvariantCulture) + "." + (Value % 10).ToString(CultureInfo.InvariantCulture) + " km";
        }
    }

    /// <summary>Projection and distance rounding for the on-foot ship locator. No scene or Unity dependencies.</summary>
    public static class ShipLocatorMath
    {
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static ShipLocatorDistance Distance(double metres)
        {
            if (!Finite(metres) || metres < 0) return default;
            bool km = metres >= 999.5;
            double rounded = Math.Round(km ? metres / 100.0 : metres, MidpointRounding.AwayFromZero);
            if (rounded >= long.MaxValue) return default;
            return new ShipLocatorDistance { Valid = true, Kilometres = km, Value = (long)rounded };
        }

        /// <summary>
        /// Perspective projection into a safe rectangle. Behind the eye, use its sideways/upwards bearing
        /// rather than the mirrored pinhole position. A target exactly aft gets a stable downward turn cue.
        /// Margins include the entire label and leave the lower suit readout clear.
        /// </summary>
        public static ShipLocatorPoint Project(Vec3d eyeOffset, double verticalFovDegrees, double width, double height,
            double left, double right, double bottom, double top)
        {
            if (!Finite(eyeOffset.X) || !Finite(eyeOffset.Y) || !Finite(eyeOffset.Z)
                || !Finite(width) || !Finite(height) || width <= 0 || height <= 0
                || !Finite(verticalFovDegrees) || verticalFovDegrees <= 0 || verticalFovDegrees >= 180
                || !Finite(left) || !Finite(right) || !Finite(bottom) || !Finite(top)
                || left < 0 || right < 0 || bottom < 0 || top < 0) return default;

            double minX = -width * 0.5 + left, maxX = width * 0.5 - right;
            double minY = -height * 0.5 + bottom, maxY = height * 0.5 - top;
            // The centre must be inside, so every bearing can meet a safe edge.
            if (minX >= 0 || maxX <= 0 || minY >= 0 || maxY <= 0) return default;

            // Bearing is independent of range. Normalize by a component rather than length, which can
            // overflow (or underflow) even for a finite vector.
            double largest = Math.Max(Math.Abs(eyeOffset.X), Math.Max(Math.Abs(eyeOffset.Y), Math.Abs(eyeOffset.Z)));
            if (largest > 0) eyeOffset /= largest;
            double focal = height * 0.5 / Math.Tan(verticalFovDegrees * Math.PI / 360.0);
            double depth = Math.Max(0.000001, Math.Abs(eyeOffset.Z));
            double x = eyeOffset.X / depth * focal, y = eyeOffset.Y / depth * focal;
            bool ahead = eyeOffset.Z > 0 || largest == 0;
            bool atTarget = ahead && x >= minX && x <= maxX && y >= minY && y <= maxY;
            if (!atTarget)
            {
                if (Math.Abs(x) + Math.Abs(y) < 1e-10) y = -1;
                double scaleX = x > 0 ? maxX / x : x < 0 ? minX / x : double.PositiveInfinity;
                double scaleY = y > 0 ? maxY / y : y < 0 ? minY / y : double.PositiveInfinity;
                double scale = Math.Min(scaleX, scaleY);
                x *= scale;
                y *= scale;
            }
            return new ShipLocatorPoint { X = x, Y = y, Angle = Math.Atan2(y, x) * 180.0 / Math.PI, AtTarget = atTarget, Valid = true };
        }
    }
}
