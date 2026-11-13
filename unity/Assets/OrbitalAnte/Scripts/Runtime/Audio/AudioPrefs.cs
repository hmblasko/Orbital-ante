using UnityEngine;

namespace OrbitalAnte.Audio
{
    /// <summary>The player's volume settings, changed from the pause menu and remembered between sessions.</summary>
    public static class AudioPrefs
    {
        static readonly string[] Keys = { "oa.audio.master", "oa.audio.music", "oa.audio.effects", "oa.audio.dialogue" };
        static readonly float[] Defaults = { 1f, 0.8f, 1f, 1f };
        static readonly float[] Cache = { -1f, -1f, -1f, -1f };

        /// <summary>Slider position, 0 to 1.</summary>
        public static float Get(AudioVolume which)
        {
            int i = (int)which;
            if (Cache[i] < 0f) Cache[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(Keys[i], Defaults[i]));
            return Cache[i];
        }

        public static void Set(AudioVolume which, float slider)
        {
            int i = (int)which;
            Cache[i] = Mathf.Clamp01(slider);
            PlayerPrefs.SetFloat(Keys[i], Cache[i]);
            PlayerPrefs.Save();
        }

        /// <summary>The gain a bus gets from the player's sliders (not counting master, which is applied to everything at once).</summary>
        public static float BusGain(AudioBus bus) => MixMath.SliderToGain(Get(MixMath.SliderFor(bus)));

        public static float MasterGain => MixMath.SliderToGain(Get(AudioVolume.Master));

        public static void ResetStatics()
        {
            for (int i = 0; i < Cache.Length; i++) Cache[i] = -1f;
        }
    }
}
