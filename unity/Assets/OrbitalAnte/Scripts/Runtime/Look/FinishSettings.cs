using System;

namespace OrbitalAnte.Look
{
    /// <summary>A Unity-free snapshot of every runtime picture-finish dial. Enum values are passed as integers.</summary>
    public readonly struct FinishSettings : IEquatable<FinishSettings>
    {
        public readonly bool Finish;
        public readonly float Bloom, BloomThreshold, Exposure, Contrast, Saturation, Warmth, SplitTone, Vignette;
        public readonly int ToneMapping, EdgeSmoothing;

        public FinishSettings(bool finish, float bloom, float bloomThreshold, int toneMapping, float exposure,
            float contrast, float saturation, float warmth, float splitTone, float vignette, int edgeSmoothing)
        {
            Finish = finish; Bloom = bloom; BloomThreshold = bloomThreshold; ToneMapping = toneMapping;
            Exposure = exposure; Contrast = contrast; Saturation = saturation; Warmth = warmth;
            SplitTone = splitTone; Vignette = vignette; EdgeSmoothing = edgeSmoothing;
        }

        public bool Equals(FinishSettings other) => Finish == other.Finish && Bloom.Equals(other.Bloom)
            && BloomThreshold.Equals(other.BloomThreshold) && ToneMapping == other.ToneMapping
            && Exposure.Equals(other.Exposure) && Contrast.Equals(other.Contrast) && Saturation.Equals(other.Saturation)
            && Warmth.Equals(other.Warmth) && SplitTone.Equals(other.SplitTone) && Vignette.Equals(other.Vignette)
            && EdgeSmoothing == other.EdgeSmoothing;
        public override bool Equals(object obj) => obj is FinishSettings other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Finish.GetHashCode();
                hash = hash * 31 + Bloom.GetHashCode(); hash = hash * 31 + BloomThreshold.GetHashCode();
                hash = hash * 31 + ToneMapping; hash = hash * 31 + Exposure.GetHashCode();
                hash = hash * 31 + Contrast.GetHashCode(); hash = hash * 31 + Saturation.GetHashCode();
                hash = hash * 31 + Warmth.GetHashCode(); hash = hash * 31 + SplitTone.GetHashCode();
                hash = hash * 31 + Vignette.GetHashCode(); return hash * 31 + EdgeSmoothing;
            }
        }
    }
}
