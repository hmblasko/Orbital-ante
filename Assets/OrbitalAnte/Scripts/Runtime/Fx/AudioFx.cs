using UnityEngine;

namespace OrbitalAnte.Fx
{
    /// <summary>Pooled one-shot 3D sounds at a position.</summary>
    public class AudioFx : MonoBehaviour
    {
        static AudioFx instance;
        AudioSource[] sources;
        int next;

        static AudioFx Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("AudioFx").AddComponent<AudioFx>();
                return instance;
            }
        }

        void Awake()
        {
            sources = new AudioSource[16];
            for (int i = 0; i < sources.Length; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 2f;
                s.maxDistance = 60f;
                s.dopplerLevel = 0f;
                sources[i] = s;
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>spatial: 0 = flat in both ears (feels like "your" sound), 1 = fully positioned.</summary>
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float spatial = 0.7f)
        {
            if (clip == null) return;
            var i = Instance;
            var s = i.sources[i.next];
            i.next = (i.next + 1) % i.sources.Length;
            s.transform.position = position;
            s.pitch = pitch;
            s.volume = volume;
            s.spatialBlend = spatial;
            s.clip = clip;
            s.Play();
        }
    }
}
