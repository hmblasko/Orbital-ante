using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using UnityEngine;

namespace OrbitalAnte.Spacewalk
{
    /// <summary>
    /// What the suit's jets and tether look and sound like. The rules are in <see cref="JetGlow"/> and
    /// <see cref="Tether"/>; this only draws them.
    ///
    /// Six pairs of small nozzles sit round the backpack. Each pair's flame points away from the push it gives,
    /// so pushing yourself backward lights the pair you can see low in front of you, and going up lights the
    /// pair by your hips. The tether is a thin bright line from the ship's belly to your harness, drawn slack
    /// when it is slack and straight when it holds you.
    /// </summary>
    public sealed class SuitFx : MonoBehaviour
    {
        const int TetherSegments = 14, TetherSides = 4;
        const float TetherRadius = 0.018f;

        Astronaut _astronaut;
        SuitConfig _config;
        public SuitAudio Sounds { get; private set; }

        Transform _pack;
        readonly Transform[] _flames = new Transform[JetGlow.Count * 2];
        Mesh _flameMesh, _tetherMesh;
        Material _flameMaterial, _tetherMaterial;
        GameObject _tether;
        Vector3[] _tetherPoints;
        AudioSource _jet, _voice;
        bool _wasTaut, _wasAlarm;
        float _nextWarn;

        // Where each nozzle sits, measured from the eyes (right, up, forward), and which way its flame points.
        // The second of each pair is the mirror image across the body.
        static readonly Vector3[] NozzleAt =
        {
            new Vector3(0.44f, -0.34f, -0.06f),   // Right: flame points right
            new Vector3(-0.44f, -0.34f, -0.06f),  // Left
            new Vector3(0.34f, -0.10f, -0.14f),   // Up
            new Vector3(0.34f, -0.72f, -0.12f),   // Down
            new Vector3(0.40f, -0.44f, 0.10f),    // Front
            new Vector3(0.30f, -0.40f, -0.26f),   // Back
        };
        static readonly Vector3[] NozzleWay =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        public static SuitFx Build(Astronaut astronaut, SuitConfig config, Transform world)
        {
            var fx = astronaut.gameObject.AddComponent<SuitFx>();
            fx._astronaut = astronaut;
            fx._config = config;
            fx.Sounds = SuitAudio.Build();

            var pack = new GameObject("SuitJets");
            pack.transform.SetParent(astronaut.transform, false);
            pack.transform.localPosition = new Vector3(0f, ShipLayout.EyeHeight, 0f);
            fx._pack = pack.transform;
            fx._flameMesh = Cone(8);
            fx._flameMaterial = new Material(PlanetMaterials.VertexColour(config.jetColour, 0f, 2f, false));
            fx._flameMaterial.name = "OA_SuitJet";
            for (int i = 0; i < fx._flames.Length; i++)
            {
                int nozzle = i / 2;
                Vector3 at = NozzleAt[nozzle];
                // Side nozzles are one above the other; the rest are a left and right pair.
                if (i % 2 == 1) at = nozzle < 2 ? at + Vector3.down * 0.3f : new Vector3(-at.x, at.y, at.z);
                var go = new GameObject("Jet" + i);
                go.layer = Layers.Default;
                go.transform.SetParent(pack.transform, false);
                go.transform.localPosition = at;
                // The cone's own tip is along +Y.
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, NozzleWay[nozzle]);
                go.AddComponent<MeshFilter>().sharedMesh = fx._flameMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = fx._flameMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                fx._flames[i] = go.transform;
            }

            fx._tether = new GameObject("Tether");
            fx._tether.layer = Layers.Default;
            fx._tether.transform.SetParent(world, false);
            fx._tetherMesh = new Mesh { name = "Tether" };
            fx._tetherMesh.MarkDynamic();
            fx._tetherPoints = new Vector3[(TetherSegments + 1) * TetherSides];
            var colours = new Color[fx._tetherPoints.Length];
            for (int i = 0; i < colours.Length; i++) colours[i] = Color.white;
            var triangles = new int[TetherSegments * TetherSides * 6];
            int t = 0;
            for (int s = 0; s < TetherSegments; s++)
            {
                for (int k = 0; k < TetherSides; k++)
                {
                    int a = s * TetherSides + k, b = s * TetherSides + (k + 1) % TetherSides;
                    int c = a + TetherSides, d = b + TetherSides;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            }
            fx._tetherMesh.vertices = fx._tetherPoints;
            fx._tetherMesh.colors = colours;
            fx._tetherMesh.triangles = triangles;
            fx._tether.AddComponent<MeshFilter>().sharedMesh = fx._tetherMesh;
            var line = fx._tether.AddComponent<MeshRenderer>();
            // Lit by itself, so the line home can be found against a black sky (and needs no normals).
            fx._tetherMaterial = new Material(PlanetMaterials.VertexColour(config.tetherColour, 0f, 1.1f, false));
            fx._tetherMaterial.name = "OA_Tether";
            line.sharedMaterial = fx._tetherMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            fx._tether.SetActive(false);

            fx._jet = astronaut.gameObject.AddComponent<AudioSource>();
            fx._jet.clip = fx.Sounds.Jet;
            fx._jet.loop = true;
            fx._jet.playOnAwake = false;
            fx._jet.spatialBlend = 0f;
            fx._jet.volume = 0f;
            fx._voice = astronaut.gameObject.AddComponent<AudioSource>();
            fx._voice.playOnAwake = false;
            fx._voice.spatialBlend = 0f;
            return fx;
        }

        static Mesh Cone(int sides)
        {
            var vertices = new Vector3[sides + 1];
            var colours = new Color[sides + 1];
            var triangles = new int[sides * 3];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                vertices[i] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                // White-hot at the nozzle, the jet's own colour toward the tip.
                colours[i] = new Color(1.6f, 1.6f, 1.6f, 1f);
                triangles[i * 3] = i;
                triangles[i * 3 + 1] = (i + 1) % sides;
                triangles[i * 3 + 2] = sides;
            }
            vertices[sides] = new Vector3(0f, 1f, 0f);
            colours[sides] = new Color(0.7f, 0.7f, 0.7f, 1f);
            var mesh = new Mesh { name = "SuitJet" };
            mesh.vertices = vertices;
            mesh.colors = colours;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Play(AudioClip clip, float volume = 1f)
        {
            if (clip != null && _voice != null) _voice.PlayOneShot(clip, volume * _config.volume);
        }

        // After the astronaut has moved and posed this frame.
        void LateUpdate()
        {
            if (_astronaut == null) return;
            bool outside = _astronaut.Mode == AstronautMode.Outside && !_astronaut.Dead;
            JetGlow jets = _astronaut.Jets;

            for (int i = 0; i < _flames.Length; i++)
            {
                float level = outside ? jets.Level(i / 2) : 0f;
                bool on = level > 0.03f;
                GameObject go = _flames[i].gameObject;
                if (go.activeSelf != on) go.SetActive(on);
                if (!on) continue;
                // A live flame is never quite still. (Looks only, so Unity's own random is right here.)
                float flicker = 0.85f + 0.3f * Random.value;
                float width = 0.035f + 0.03f * level;
                _flames[i].localScale = new Vector3(width, _config.jetLength * level * flicker, width);
            }

            float loud = outside ? jets.Loudness : 0f;
            float want = loud * _config.volume;
            _jet.volume = Mathf.MoveTowards(_jet.volume, want, Time.deltaTime * 6f);
            _jet.pitch = 0.9f + 0.25f * loud;
            if (_jet.volume > 0.001f && !_jet.isPlaying) _jet.Play();
            else if (_jet.volume <= 0.001f && _jet.isPlaying) _jet.Stop();
            if (outside && jets.Lit) Play(Sounds.Puff, 0.7f);

            bool taut = outside && _astronaut.TetherTaut;
            if (taut && !_wasTaut) Play(Sounds.Taut, 0.9f);
            _wasTaut = taut;

            // A new danger is heard once, then again every so often for as long as it lasts.
            bool alarm = _astronaut.SuitAlarm && !_astronaut.Dead;
            if (alarm && (!_wasAlarm || Time.unscaledTime > _nextWarn))
            {
                Play(Sounds.Warn, 0.8f);
                _nextWarn = Time.unscaledTime + 12f;
            }
            _wasAlarm = alarm;

            DrawTether(outside && _astronaut.Tethered);
        }

        void DrawTether(bool show)
        {
            if (_tether.activeSelf != show) _tether.SetActive(show);
            if (!show) return;
            Transform root = _tether.transform;
            Vector3 from = root.InverseTransformPoint(_astronaut.TetherAnchor);
            // The line clips on at the left hip of the harness.
            Vector3 to = root.InverseTransformPoint(_pack.TransformPoint(new Vector3(-0.22f, -0.62f, -0.05f)));
            Vector3 along = to - from;
            float distance = along.magnitude;
            if (distance < 0.01f) return;
            float slack = Tether.Slack(_config.rules, distance);
            // A slack line hangs the way things fall; with nothing pulling, it bows away from the ship's belly.
            Vector3 down = root.InverseTransformDirection(_astronaut.TetherHangs);
            Vector3 forward = along / distance;
            Vector3 side = Vector3.Cross(forward, Mathf.Abs(forward.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 over = Vector3.Cross(forward, side);
            int v = 0;
            for (int s = 0; s <= TetherSegments; s++)
            {
                float t = s / (float)TetherSegments;
                Vector3 centre = from + along * t + down * (slack * 4f * t * (1f - t));
                for (int k = 0; k < TetherSides; k++)
                {
                    float a = k * Mathf.PI * 2f / TetherSides;
                    _tetherPoints[v++] = centre + (side * Mathf.Cos(a) + over * Mathf.Sin(a)) * TetherRadius;
                }
            }
            _tetherMesh.vertices = _tetherPoints;
            _tetherMesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (Sounds != null) Sounds.Destroy();
            if (_flameMesh != null) Destroy(_flameMesh);
            if (_tetherMesh != null) Destroy(_tetherMesh);
            if (_flameMaterial != null) Destroy(_flameMaterial);
            if (_tetherMaterial != null) Destroy(_tetherMaterial);
            if (_tether != null) Destroy(_tether);
        }
    }
}
