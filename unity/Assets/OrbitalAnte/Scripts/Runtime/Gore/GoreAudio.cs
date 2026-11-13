using OrbitalAnte.Dismemberment;
using UnityEngine;

namespace OrbitalAnte.Gore
{
    /// <summary>
    /// Placeholder "pop" sounds, generated in code so no audio files are needed yet. Each size of body part gets
    /// its own pop (small, medium, big, torso, head) plus a soft splat for landing chunks. A small pool of
    /// audio sources plays them where the wound is.
    /// </summary>
    public sealed class GoreAudio
    {
        const int SampleRate = 44100;
        const int SourceCount = 8;

        readonly AudioSource[] _sources = new AudioSource[SourceCount];
        int _next;
        float _lastSplatTime = -10f;

        AudioClip _small, _medium, _big, _torso, _head, _splat;

        public void Build(Transform parent)
        {
            // Downward chirps read as a "pop"; the bigger the part, the lower and longer the sound.
            _small = MakePop("PopSmall", 1500f, 520f, 0.07f, 0.25f, 0f, 0f, 11);
            _medium = MakePop("PopMedium", 950f, 280f, 0.09f, 0.30f, 0f, 0f, 12);
            _big = MakePop("PopBig", 620f, 140f, 0.13f, 0.45f, 0f, 0f, 13);
            _torso = MakePop("PopTorso", 230f, 48f, 0.30f, 0.90f, 0f, 0f, 14);
            _head = MakePop("PopHead", 1150f, 300f, 0.10f, 0.20f, 1900f, 0.35f, 15);
            _splat = MakeSplat("Splat", 0.14f, 16);

            for (int i = 0; i < SourceCount; i++)
            {
                var go = new GameObject("GoreAudioSource");
                go.transform.SetParent(parent, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0.55f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 3f;
                source.maxDistance = 45f;
                source.dopplerLevel = 0f;
                _sources[i] = source;
            }
        }

        public void PlayPop(Detachment detachment, Vector3 position, float volume)
        {
            if (detachment.Reason == DetachReason.Corpse) return; // the head pop already marks the kill
            Audio.AudioDirector director = Audio.AudioDirector.Instance;
            string pop = PopEvent(detachment);
            if (director != null && director.Has(pop))
            {
                Audio.AudioDirector.Play(pop, position, Mathf.Clamp01(volume));
                return;
            }
            AudioClip clip;
            switch (detachment.Reason)
            {
                case DetachReason.HeadDestroyed:
                    clip = _head;
                    break;
                case DetachReason.TorsoGib:
                    clip = _torso;
                    break;
                case DetachReason.Corpse:
                    return; // the head pop already marks the kill
                default:
                    clip = ClipForPart(detachment.Root);
                    break;
            }
            Play(clip, position, volume, 0.92f, 1.10f);
        }

        static string PopEvent(Detachment detachment)
        {
            if (detachment.Reason == DetachReason.HeadDestroyed) return "gore.pop.head";
            if (detachment.Reason == DetachReason.TorsoGib) return "gore.pop.torso";
            switch (detachment.Root)
            {
                case BodyPartId.LeftHand:
                case BodyPartId.RightHand:
                case BodyPartId.LeftFoot:
                case BodyPartId.RightFoot:
                    return "gore.pop.small";
                case BodyPartId.LeftForearm:
                case BodyPartId.RightForearm:
                case BodyPartId.LeftShin:
                case BodyPartId.RightShin:
                    return "gore.pop.medium";
                case BodyPartId.Torso:
                    return "gore.pop.torso";
                case BodyPartId.Head:
                    return "gore.pop.head";
                default:
                    return "gore.pop.big";
            }
        }

        AudioClip ClipForPart(BodyPartId id)
        {
            switch (id)
            {
                case BodyPartId.LeftHand:
                case BodyPartId.RightHand:
                case BodyPartId.LeftFoot:
                case BodyPartId.RightFoot:
                    return _small;
                case BodyPartId.LeftForearm:
                case BodyPartId.RightForearm:
                case BodyPartId.LeftShin:
                case BodyPartId.RightShin:
                    return _medium;
                case BodyPartId.Torso:
                    return _torso;
                case BodyPartId.Head:
                    return _head;
                default:
                    return _big;
            }
        }

        public void PlaySplat(Vector3 position, float volume)
        {
            // Several chunks landing together should not stack into a roar.
            if (Time.unscaledTime - _lastSplatTime < 0.05f) return;
            _lastSplatTime = Time.unscaledTime;
            Audio.AudioDirector director = Audio.AudioDirector.Instance;
            if (director != null && director.Has("gore.splat"))
            {
                Audio.AudioDirector.Play("gore.splat", position, Mathf.Clamp01(volume));
                return;
            }
            Play(_splat, position, volume, 0.8f, 1.25f);
        }

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

        static AudioClip MakePop(string name, float startHz, float endHz, float seconds, float noise,
            float ringHz, float ringAmount, int seed)
        {
            int count = Mathf.Max(64, Mathf.RoundToInt(seconds * SampleRate));
            var data = new float[count];
            var rng = new System.Random(seed);
            double phase = 0.0;
            float lowpassed = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float k = i / (float)count;

                // Pitch falls quickly at first, then settles.
                float hz = startHz * Mathf.Pow(endHz / startHz, Mathf.Sqrt(k));
                phase += 2.0 * System.Math.PI * hz / SampleRate;

                float attack = Mathf.Clamp01(t / 0.0015f);
                float body = (float)System.Math.Sin(phase) * Mathf.Exp(-k * 5.5f);

                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lowpassed += (white - lowpassed) * 0.35f;
                float crack = lowpassed * noise * Mathf.Exp(-k * 14f);

                float ring = ringAmount > 0f
                    ? Mathf.Sin(2f * Mathf.PI * ringHz * t) * ringAmount * Mathf.Exp(-k * 4f)
                    : 0f;

                float release = Mathf.Clamp01((1f - k) / 0.08f);
                float sample = (body + crack + ring) * attack * release;
                data[i] = SoftClip(sample * 0.9f);
            }

            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeSplat(string name, float seconds, int seed)
        {
            int count = Mathf.Max(64, Mathf.RoundToInt(seconds * SampleRate));
            var data = new float[count];
            var rng = new System.Random(seed);
            float lowpassed = 0f;

            for (int i = 0; i < count; i++)
            {
                float k = i / (float)count;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                // A heavier low-pass makes it wet rather than hissy; it opens briefly at the start.
                float cutoff = Mathf.Lerp(0.22f, 0.04f, Mathf.Sqrt(k));
                lowpassed += (white - lowpassed) * cutoff;
                float attack = Mathf.Clamp01(k / 0.02f);
                data[i] = SoftClip(lowpassed * 2.2f * attack * Mathf.Exp(-k * 6f));
            }

            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float SoftClip(float x)
        {
            // Smoothly limits peaks so nothing crackles.
            return x / (1f + Mathf.Abs(x));
        }
    }
}
