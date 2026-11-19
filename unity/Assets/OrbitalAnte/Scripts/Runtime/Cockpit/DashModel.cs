using System;
using System.Globalization;

namespace OrbitalAnte.Cockpit
{
    /// <summary>What the ship knows at one instant, handed to the dash.</summary>
    public struct DashInputs
    {
        /// <summary>Speed against the body you are matching (the locked target, else the body you are near), metres a second.</summary>
        public float Speed;
        /// <summary>Which way that motion points, in the ship's own space (x right, y up, z nose). Any length.</summary>
        public float DriftX, DriftY, DriftZ;
        /// <summary>The pilot's push in the ship's own space, each -1 to 1.</summary>
        public float ThrustX, ThrustY, ThrustZ;
        public bool Boost, Matching;
        /// <summary>Metres above the ground, or negative when there is none near.</summary>
        public float Altitude;
        /// <summary>Climbing (+) or sinking (-), metres a second. Only means anything with an altitude.</summary>
        public float Vertical;
        public float Fuel, Oxygen;
        public bool FuelLow, FuelOut, OxygenLow, Filling;
        /// <summary>How hard the main engines are burning, 0 to 1.</summary>
        public float Burn;
        /// <summary>True during the build-up before lift-off.</summary>
        public bool Igniting;
        /// <summary>The body your speed is measured against, in capitals.</summary>
        public string Against;
        public string CameraName;
        /// <summary>What to press to look down at the camera screen, already in words ("C: LOOK DOWN").</summary>
        public string ScreenHint;
        /// <summary>A line from another system (the autopilot), shown when nothing more urgent is.</summary>
        public string Note;
        /// <summary>The fastest the ship can touch down without damage, metres per second. 0 if unknown.</summary>
        public float SafeSink;
        /// <summary>Nobody is strapped into the seat.</summary>
        public bool Loose;
    }

    /// <summary>What the dash shows: the inputs, settled so needles glide and words do not flicker.</summary>
    public sealed class DashReadings
    {
        public float Speed, Altitude, Vertical, Fuel = 1f, Oxygen = 1f, Burn;
        /// <summary>Unit length, or zero when hardly moving.</summary>
        public float DriftX, DriftY, DriftZ;
        public float ThrustX, ThrustY, ThrustZ;
        public bool Boost, Matching, FuelLow, FuelOut, OxygenLow, Filling, Igniting, Moving;
        /// <summary>Seconds, for anything that blinks.</summary>
        public float Clock;
        public string SpeedText = "0", SpeedUnit = "m/s", AltitudeText = "", VerticalText = "", FuelText = "100", OxygenText = "100";
        public string FuelLabel = "100%", OxygenLabel = "100%";
        public string Against = "", SpeedLabel = "SPEED", CameraName = "", ScreenHint = "", Notice = "";
        /// <summary>Coming down too fast to land safely, close to the ground.</summary>
        public bool SinkTooFast;
        /// <summary>On for half of every second: for warnings.</summary>
        public bool Blink => Clock % 1f < 0.5f;
    }

    /// <summary>
    /// Turns what the ship knows into what the dash shows. Plain C#, no Unity types. Needles ease toward
    /// their value rather than jumping, and the words are only rewritten a few times a second (and only
    /// when the number they show has changed), so nothing flickers and no rubbish is made each frame.
    /// </summary>
    public sealed class DashModel
    {
        public readonly DashReadings Readings = new DashReadings();
        /// <summary>Seconds between rewrites of the words.</summary>
        /// <summary>The landing warning speaks when you would reach the ground within this many seconds at your present sink rate.</summary>
        public const float LandingWarningSeconds = 6f;
        /// <summary>Moving faster than this with nobody strapped in, the dash says so.</summary>
        public const float LooseWarningSpeed = 5f;
        float _safeShown = -1f;
        string _tooFastWords = string.Empty;
        public float TextInterval = 0.125f;
        /// <summary>Below this speed the direction ball shows no dot: you are as good as still.</summary>
        public float StillSpeed = 0.3f;

        float _nextText;
        int _speedShown = int.MinValue, _altitudeShown = int.MinValue, _verticalShown = int.MinValue, _fuelShown = -1, _oxygenShown = -1;
        static readonly CultureInfo Commas = CultureInfo.InvariantCulture;

        public void Tick(float dt, in DashInputs input)
        {
            DashReadings r = Readings;
            if (dt < 0f) dt = 0f;
            r.Clock += dt;
            float ease = 1f - (float)Math.Exp(-dt * 10f);
            r.Speed += (Math.Max(0f, input.Speed) - r.Speed) * ease;
            r.Fuel += (Clamp01(input.Fuel) - r.Fuel) * ease;
            r.Oxygen += (Clamp01(input.Oxygen) - r.Oxygen) * ease;
            r.Burn += (Clamp01(input.Burn) - r.Burn) * ease;
            r.Altitude = input.Altitude;
            r.Vertical += (input.Vertical - r.Vertical) * ease;

            float length = (float)Math.Sqrt(input.DriftX * input.DriftX + input.DriftY * input.DriftY + input.DriftZ * input.DriftZ);
            r.Moving = input.Speed > StillSpeed && length > 1e-6f;
            if (r.Moving)
            {
                // The dot glides too, but along the ball, so it never cuts through the middle.
                float tx = input.DriftX / length, ty = input.DriftY / length, tz = input.DriftZ / length;
                float x = r.DriftX + (tx - r.DriftX) * ease;
                float y = r.DriftY + (ty - r.DriftY) * ease;
                float z = r.DriftZ + (tz - r.DriftZ) * ease;
                float n = (float)Math.Sqrt(x * x + y * y + z * z);
                // Straight to the new direction when there is nothing to glide from, and when the motion has
                // turned right round: gliding between opposite points only shrinks the dot toward the middle
                // and is then stretched back to where it was, so it would never get there.
                bool turnedRound = r.DriftX * tx + r.DriftY * ty + r.DriftZ * tz < -0.9f;
                if (n < 1e-4f || turnedRound) { x = tx; y = ty; z = tz; n = 1f; }
                r.DriftX = x / n; r.DriftY = y / n; r.DriftZ = z / n;
            }
            else { r.DriftX = 0f; r.DriftY = 0f; r.DriftZ = 0f; }

            float quick = 1f - (float)Math.Exp(-dt * 18f);
            r.ThrustX += (Clamp(input.ThrustX) - r.ThrustX) * quick;
            r.ThrustY += (Clamp(input.ThrustY) - r.ThrustY) * quick;
            r.ThrustZ += (Clamp(input.ThrustZ) - r.ThrustZ) * quick;
            r.Boost = input.Boost;
            r.Matching = input.Matching;
            r.FuelLow = input.FuelLow;
            r.FuelOut = input.FuelOut;
            r.OxygenLow = input.OxygenLow;
            r.Filling = input.Filling;
            r.Igniting = input.Igniting;
            string against = input.Against ?? string.Empty;
            if (!ReferenceEquals(against, r.Against) && against != r.Against)
            {
                // Said in full, so a speed is never read against the wrong world.
                r.Against = against;
                r.SpeedLabel = against.Length > 0 ? "SPEED vs " + against : "SPEED";
            }
            r.CameraName = input.CameraName ?? string.Empty;
            r.ScreenHint = input.ScreenHint ?? string.Empty;

            _nextText -= dt;
            if (_nextText > 0f) return;
            _nextText = TextInterval;

            float speed = Math.Max(0f, input.Speed);
            bool fast = speed >= 10000f;
            int speedKey = fast ? 1000000 + (int)Math.Round(speed / 100f) : (int)Math.Round(speed);
            if (speedKey != _speedShown)
            {
                _speedShown = speedKey;
                r.SpeedText = fast ? (speed / 1000f).ToString("0.0", Commas) : ((int)Math.Round(speed)).ToString("#,0", Commas);
                r.SpeedUnit = fast ? "km/s" : "m/s";
            }
            int altitudeKey = input.Altitude < 0f ? -1 : input.Altitude < 1000f ? (int)Math.Round(input.Altitude) : 100000 + (int)Math.Round(input.Altitude / 100f);
            if (altitudeKey != _altitudeShown)
            {
                _altitudeShown = altitudeKey;
                r.AltitudeText = input.Altitude < 0f ? string.Empty
                    : input.Altitude < 1000f ? ((int)Math.Round(input.Altitude)).ToString(Commas) + " m"
                    : input.Altitude < 100000f ? (input.Altitude / 1000f).ToString("0.0", Commas) + " km"
                    : ((int)Math.Round(input.Altitude / 1000f)).ToString("#,0", Commas) + " km";
            }
            int verticalKey = input.Altitude < 0f ? int.MinValue + 1 : (int)Math.Round(input.Vertical);
            if (verticalKey != _verticalShown)
            {
                _verticalShown = verticalKey;
                r.VerticalText = input.Altitude < 0f ? string.Empty : (verticalKey > 0 ? "+" : string.Empty) + verticalKey.ToString(Commas);
            }
            int fuel = (int)Math.Round(Clamp01(input.Fuel) * 100f), oxygen = (int)Math.Round(Clamp01(input.Oxygen) * 100f);
            if (fuel != _fuelShown) { _fuelShown = fuel; r.FuelText = fuel.ToString(Commas); r.FuelLabel = r.FuelText + "%"; }
            if (oxygen != _oxygenShown) { _oxygenShown = oxygen; r.OxygenText = oxygen.ToString(Commas); r.OxygenLabel = r.OxygenText + "%"; }

            // One notice at a time, the most urgent first.
            // Too fast to land: close to the ground and coming down faster than the legs can take. The nearer the
            // ground, the sooner it speaks, and it names the number so the limit is learned, not guessed.
            bool tooFast = input.SafeSink > 0f && input.Altitude >= 0f && input.Vertical < -input.SafeSink
                && input.Altitude < -input.Vertical * LandingWarningSeconds;
            if (tooFast && input.SafeSink != _safeShown)
            {
                _safeShown = input.SafeSink;
                _tooFastWords = "TOO FAST TO LAND: OVER " + Math.Round(input.SafeSink).ToString(Commas) + " m/s";
            }
            r.SinkTooFast = tooFast;
            r.Notice = input.Loose && input.Speed > LooseWarningSpeed ? "NOT BUCKLED IN"
                : tooFast ? _tooFastWords
                : input.FuelOut ? "FUEL OUT: WEAK PUSH. LAND TO REFILL"
                : input.FuelLow && input.OxygenLow ? "FUEL LOW, AIR LOW"
                : input.FuelLow ? "FUEL LOW"
                : input.OxygenLow ? "AIR LOW"
                : !string.IsNullOrEmpty(input.Note) ? input.Note
                : input.Filling ? "FILLING TANKS"
                : input.Igniting ? "ENGINES BUILDING"
                : input.Matching ? "MATCHING SPEED"
                : string.Empty;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        static float Clamp(float v) => v < -1f ? -1f : v > 1f ? 1f : v;
    }
}
