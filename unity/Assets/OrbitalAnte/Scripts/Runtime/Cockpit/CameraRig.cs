using OrbitalAnte.Ship;

namespace OrbitalAnte.Cockpit
{
    /// <summary>One of the ship's outside cameras: where its lens is and what it looks at, in ship space.</summary>
    public struct CameraSpot
    {
        public string Name;
        /// <summary>The lens. Also the name of the socket in the Lander model that can move it.</summary>
        public string Socket;
        /// <summary>The piece of the Lander model that is this camera's hardware.</summary>
        public string Piece;
        public P3 Lens, LookAt, Up;
        public bool Landing;
    }

    /// <summary>
    /// The ship's three outside cameras. Plain C#, no Unity types. A landing camera under the belly looking
    /// straight down (nose at the top of the picture), and one on a pole each side looking back at the ship,
    /// so you can see your own legs, flames and damage without ever leaving first person.
    /// </summary>
    public static class CameraRig
    {
        public const int Count = 3;
        /// <summary>The landing camera hangs under the belly; this is its glass, as built in the Lander model.</summary>
        public const float LandingLensY = -2.31f;
        /// <summary>
        /// The height the two pole cameras aim at, in ship space: below the cabin floor, so the picture holds
        /// the cabin and most of the long legs and not the cabin with empty sky over it.
        /// </summary>
        public const float PoleAimY = -0.7f;

        public static CameraSpot Spot(int index)
        {
            switch (index)
            {
                case 0:
                    return new CameraSpot
                    {
                        Name = "LANDING", Socket = "LandingCam", Piece = "landingcam", Landing = true,
                        Lens = new P3(0.9f, LandingLensY, 0.9f), LookAt = new P3(0.9f, LandingLensY - 10f, 0.9f), Up = new P3(0f, 0f, 1f),
                    };
                case 1:
                    return new CameraSpot
                    {
                        Name = "LEFT POLE", Socket = "PoleCam0", Piece = "pole0",
                        Lens = new P3(-5.2f, 2.6f, -3.2f), LookAt = new P3(0f, PoleAimY, 0.3f), Up = P3.Up,
                    };
                default:
                    return new CameraSpot
                    {
                        Name = "RIGHT POLE", Socket = "PoleCam1", Piece = "pole1",
                        Lens = new P3(5.2f, 2.6f, -3.2f), LookAt = new P3(0f, PoleAimY, 0.3f), Up = P3.Up,
                    };
            }
        }

        /// <summary>The next camera round the ring.</summary>
        public static int Next(int index) => (index + 1) % Count;
    }

    public enum ScreenState
    {
        /// <summary>Eyes front, flying.</summary>
        Up,
        /// <summary>Head tipping down to the screen.</summary>
        Lowering,
        /// <summary>The camera's picture fills your view.</summary>
        Watching,
        /// <summary>Head coming back up.</summary>
        Raising,
    }

    /// <summary>
    /// Looking down at the dash's camera screen and back up. Plain C#. One button tips your head down; when it
    /// gets there the camera's picture takes over the view. The same button, or leaving the seat, brings you back.
    /// You keep flying throughout: this only changes what you see.
    /// </summary>
    public sealed class ScreenView
    {
        public ScreenState State { get; private set; } = ScreenState.Up;
        /// <summary>How far the head has tipped, 0 to 1.</summary>
        public float Glance { get; private set; }
        public int Camera { get; private set; }
        public bool Watching => State == ScreenState.Watching;

        /// <param name="toggle">The look-down button was pressed this step.</param>
        /// <param name="next">The next-camera button was pressed this step.</param>
        /// <param name="seated">Buckled into the seat. Anything else puts your head back up at once.</param>
        public void Tick(float dt, bool toggle, bool next, bool seated, float seconds)
        {
            if (next) Camera = CameraRig.Next(Camera);
            if (!seated)
            {
                State = ScreenState.Up;
                Glance = 0f;
                return;
            }
            if (toggle)
            {
                State = State == ScreenState.Up || State == ScreenState.Raising ? ScreenState.Lowering : ScreenState.Raising;
            }
            float step = seconds > 0.01f ? dt / seconds : 1f;
            if (State == ScreenState.Lowering)
            {
                Glance += step;
                if (Glance >= 1f) { Glance = 1f; State = ScreenState.Watching; }
            }
            else if (State == ScreenState.Raising)
            {
                Glance -= step;
                if (Glance <= 0f) { Glance = 0f; State = ScreenState.Up; }
            }
        }
    }
}
