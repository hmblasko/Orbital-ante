using System.Collections.Generic;
using UnityEngine;

namespace OrbitalAnte.Weapons
{
    /// <summary>
    /// Placeholder gun sounds, generated in code so no audio files are needed yet: a shot per weapon (pitched by
    /// its data), magazine clicks, a dry click, a metallic clang for a gun being shot, and a thud for the player
    /// being hit. A small pool of audio sources plays them in the world.
    /// </summary>
    public sealed class WeaponAudio
    {
        const int SampleRate = 44100;
        const int SourceCount = 10;

        readonly AudioSource[] _sources = new AudioSource[SourceCount];
        readonly Dictionary<WeaponData, AudioClip> _shots = new Dictionary<WeaponData, AudioClip>();
        int _next;
        AudioClip _click, _clang, _thud;

        public void Build(Transform parent)
        {
            _click = MakeClick();
            _clang = MakeClang();
            _thud = MakeThud();

            for (int i = 0; i < SourceCount; i++)
            {
                var go = new GameObject("WeaponAudioSource");
                go.transform.SetParent(parent, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0.6f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 4f;
                source.maxDistance = 70f;
                source.dopplerLevel = 0f;
                _sources[i] = source;
            }
        }

        public void PlayShot(WeaponData data, Vector3 position)
        {
            if (data == null) return;
            if (!_shots.TryGetValue(data, out AudioClip clip) || clip == null)
            {
                clip = MakeShot(data.name, Mathf.Clamp(data.shotPitch, 0.4f, 3f));
                _shots[data] = clip;
            }
            Play(clip, position, data.shotVolume, 0.96f, 1.05f);
        }

        /// <summary>A mechanical click. Pitch below 1 for "magazine out", above for "magazine in" or a dry trigger.</summary>
        public void PlayClick(Vector3 position, float pitch, float volume = 0.5f) => Play(_click, position, volume, pitch * 0.97f, pitch * 1.03f);

        public void PlayClang(Vector3 position) => Play(_clang, position, 0.8f, 0.9f, 1.15f);

        public void PlayThud(Vector3 position) => Play(_thud, position, 0.7f, 0.9f, 1.1f);

        void Play(AudioClip clip, Vector3 position, float volume, float minPitch, float maxPitch)
        {
            if (clip == null || volume <= 0f) return;
            AudioSource source = _sources[_next];
            _next = (_next + 1) % SourceCount;
            if (source == null) return;
            source.transform.position = position;
            source.pitch = Random.Range(minPitch, maxPitch);
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        // ------------------------------------------------------------------ synthesis

        static AudioClip MakeShot(string name, float pitch)
        {
            // A sharp noise crack over a short low thump. Higher pitch = shorter, snappier.
            float seconds = 0.20f / Mathf.Sqrt(pitch);
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            var rng = new System.Random(77);
            float lowpassed = 0f;
            float cutoff = Mathf.Clamp01(0.35f * pitch);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float k = i / (float)count;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lowpassed += (noise - lowpassed) * cutoff;
                float crack = lowpassed * Mathf.Exp(-k * 9f);
                float thumpHz = 150f * pitch * Mathf.Exp(-k * 3f);
                float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t) * Mathf.Exp(-k * 6f);
                data[i] = SoftClip((crack * 1.5f + thump * 0.8f) * 1.2f);
            }
            return Clip("Shot_" + name, data);
        }

        static AudioClip MakeClick()
        {
            int count = Mathf.CeilToInt(0.035f * SampleRate);
            var data = new float[count];
            var rng = new System.Random(78);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float k = i / (float)count;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float ring = Mathf.Sin(2f * Mathf.PI * 2300f * t);
                data[i] = SoftClip((noise * 0.6f + ring * 0.6f) * Mathf.Exp(-k * 8f));
            }
            return Clip("Click", data);
        }

        static AudioClip MakeClang()
        {
            // A few tones that do not line up musically ring like struck metal.
            int count = Mathf.CeilToInt(0.32f * SampleRate);
            var data = new float[count];
            float[] hz = { 1180f, 1870f, 2950f, 4100f };
            float[] gain = { 0.5f, 0.35f, 0.25f, 0.15f };
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float k = i / (float)count;
                float sample = 0f;
                for (int h = 0; h < hz.Length; h++) sample += Mathf.Sin(2f * Mathf.PI * hz[h] * t) * gain[h];
                data[i] = SoftClip(sample * Mathf.Exp(-k * 7f));
            }
            return Clip("Clang", data);
        }

        static AudioClip MakeThud()
        {
            int count = Mathf.CeilToInt(0.14f * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float k = i / (float)count;
                float hz = Mathf.Lerp(130f, 55f, k);
                data[i] = SoftClip(Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-k * 5f) * 1.4f);
            }
            return Clip("Thud", data);
        }

        static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float SoftClip(float x)
        {
            return x / (1f + Mathf.Abs(x));
        }
    }
}
