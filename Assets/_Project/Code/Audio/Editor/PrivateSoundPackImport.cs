using System;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Audio.Editor
{
    /// <summary>
    /// Import settings for the private sound pack's copies, applied before their first import so nothing
    /// is encoded twice. A slot pick imports like the public clips (PCM, decompressed on load, its own
    /// rate, no force-to-mono: the content tests' bar). A song streams as Vorbis (rule 7): minutes of
    /// stereo never sit decoded in memory.
    /// </summary>
    internal sealed class PrivateSoundPackImport : AssetPostprocessor
    {
        // Vorbis quality for the songs (0..1): transparent for this music at several times under the WAV's size.
        private const float SongQuality = 0.7f;

        private void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;
            if (assetPath.StartsWith(PrivateSoundPackInstaller.ClipsFolder + "/", StringComparison.Ordinal))
                Apply(importer, AudioClipLoadType.DecompressOnLoad, AudioCompressionFormat.PCM, 1f);
            else if (assetPath.StartsWith(PrivateSoundPackInstaller.MusicFolder + "/", StringComparison.Ordinal))
                Apply(importer, AudioClipLoadType.Streaming, AudioCompressionFormat.Vorbis, SongQuality);
        }

        private static void Apply(AudioImporter importer, AudioClipLoadType load, AudioCompressionFormat format, float quality)
        {
            importer.forceToMono = false;
            importer.loadInBackground = false;
            importer.ambisonic = false;
            var s = importer.defaultSampleSettings;
            s.loadType = load;
            s.compressionFormat = format;
            s.quality = quality;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            s.preloadAudioData = false;
            importer.defaultSampleSettings = s;
        }
    }
}
