namespace OrbitalAnte.Look
{
    /// <summary>
    /// The picture's colour grade (22 Nov 2026, Matthew: the game should not "seem like it was created by AI").
    /// The first grade pushed contrast and saturation up and split the picture teal in the shadows and amber in
    /// the lights, over a dark vignette: the house look of generated images and stock trailers. Real photographs
    /// of the Moon, Mars and the ISS are flatter and truer, so the grade now only seasons the picture. Plain C#.
    /// </summary>
    public static class GradeDefaults
    {
        /// <summary>Raise when any number below changes, so assets made earlier take the new grade.</summary>
        public const int Version = 1;
        public const float Contrast = 5f;
        public const float Saturation = 0f;
        public const float SplitTone = 0.18f;
        public const float Vignette = 0.1f;
    }
}
