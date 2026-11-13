using UnityEditor;
using UnityEngine;

namespace OrbitalAnte.EditorTools
{
    /// <summary>
    /// Sets how Unity stores each of the game's audio files, automatically, the moment a file is imported, so
    /// nobody has to click through import settings and the Steam Deck memory budget is kept by construction.
    ///
    /// - Music stems and cues: kept compressed in memory (Vorbis) and decoded as they play. A stem is over a
    ///   minute long; unpacked it would be 20 MB or more, and there are several per piece.
    /// - Long looping beds (wind, cabin, engine): the same.
    /// - Short sound effects: unpacked when loaded, so a gunshot starts the instant it is asked for with no
    ///   decoding cost, stored as ADPCM (a third of the size, no audible cost on percussive sounds).
    /// - Reward and interface chimes stay uncompressed (they are tonal, where ADPCM can be heard).
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        const string Root = "Assets/OrbitalAnte/Resources/Audio/";

        void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Root)) return;
            var importer = (AudioImporter)assetImporter;
            // Only decide once, the first time Unity sees the file: after that, anything changed by hand in the
            // Inspector is left alone.
            if (!importer.importSettingsMissing || importer.userData == "oa-audio-1") return;
            importer.userData = "oa-audio-1";

            bool music = path.Contains("/Music/");
            bool longBed = path.EndsWith(".ogg") && !music;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            if (music || longBed)
            {
                s.loadType = AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = music ? 0.7f : 0.55f;
                // Loaded up front, not in the background: a stem that is still loading when its piece starts would
                // start late and put the piece out of step. Compressed loading only reads the bytes, so it is quick.
                importer.loadInBackground = false;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                // ADPCM is a third of the size and inaudible on bangs and clicks, but adds a faint hiss to clean
                // held notes, so the chimes (rewards, interface) stay uncompressed.
                bool tonal = path.Contains("/Sfx/rewards/") || path.Contains("/Sfx/ui/");
                s.compressionFormat = tonal ? AudioCompressionFormat.PCM : AudioCompressionFormat.ADPCM;
                importer.loadInBackground = false;
            }
            // Never "force to mono" here: Unity would also re-normalise the file and undo its designed level.
            s.preloadAudioData = true;
            importer.defaultSampleSettings = s;
        }
    }
}
