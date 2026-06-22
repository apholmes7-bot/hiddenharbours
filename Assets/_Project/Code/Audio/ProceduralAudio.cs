using System;
using UnityEngine;

namespace HiddenHarbours.Audio
{
    /// <summary>
    /// Cheap PROCEDURAL placeholder audio so the adaptive mix is audible end-to-end before the owner's
    /// real SFX land (those slot into <see cref="AudioDirector"/>'s serialized clip refs — see
    /// <c>Assets/_Project/Audio/AUDIO-MANIFEST.md</c>). Deterministic (index-hashed noise, no RNG
    /// state) and generated once at boot — never per frame.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        /// <summary>A soft, looping calm-sea wash (slow-swelling low noise).</summary>
        public static AudioClip CalmSeaBed() => Build("ph_calm_bed", 3f, true, (t, n) =>
        {
            float swell = 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * 0.15f);
            return Noise(n) * 0.18f * swell;
        });

        /// <summary>Sparse gull calls over mostly-silence.</summary>
        public static AudioClip GullCalls() => Build("ph_gulls", 4f, true, (t, n) =>
            (Chirp(t, 0.6f, 1400f, 0.12f) + Chirp(t, 2.7f, 1800f, 0.10f)) * 0.25f);

        /// <summary>A low rhythmic hull-slap / oar pull (~one per 0.6 s).</summary>
        public static AudioClip HullRow() => Build("ph_hull_row", 1.2f, true, (t, n) =>
        {
            float phase = Mathf.Repeat(t, 0.6f);
            float env = Mathf.Exp(-phase * 12f);
            return Mathf.Sin(phase * 2f * Mathf.PI * 90f) * env * 0.3f;
        });

        /// <summary>An airy rising-wind whistle bed — the director scales its loudness by the tell.</summary>
        public static AudioClip WindTell() => Build("ph_wind_tell", 2f, true, (t, n) =>
        {
            float whistle = Mathf.Sin(t * 2f * Mathf.PI * 320f) * 0.5f + Noise(n) * 0.5f;
            float warble = 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 3f);
            return whistle * 0.22f * warble;
        });

        /// <summary>A bright little catch sting (rising two-note ding).</summary>
        public static AudioClip CatchSting() => Build("ph_catch_sting", 0.5f, false, (t, n) =>
        {
            float env = Mathf.Exp(-t * 6f);
            float a = Mathf.Sin(t * 2f * Mathf.PI * 880f);
            float b = Mathf.Sin(Mathf.Max(0f, t - 0.12f) * 2f * Mathf.PI * 1320f);
            return (a + b) * 0.25f * env;
        });

        /// <summary>A warm "made it home" chord swell (A minor-ish triad).</summary>
        public static AudioClip HomeWarmth() => Build("ph_home_warmth", 1.2f, false, (t, n) =>
        {
            float env = Mathf.Min(1f, t * 4f) * Mathf.Exp(-t * 1.5f);
            float chord = Mathf.Sin(t * 2f * Mathf.PI * 220f)
                        + Mathf.Sin(t * 2f * Mathf.PI * 277.18f)
                        + Mathf.Sin(t * 2f * Mathf.PI * 329.63f);
            return chord * 0.12f * env;
        });

        // ---- helpers ------------------------------------------------------------------------

        private static AudioClip Build(string name, float seconds, bool loop, Func<float, int, float> sample)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            var data = new float[count];
            for (int n = 0; n < count; n++)
            {
                float t = (float)n / SampleRate;
                data[n] = Mathf.Clamp(sample(t, n), -1f, 1f);
            }
            if (loop) CrossfadeSeam(data);

            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Deterministic value noise in [-1, 1] from the sample index (no RNG state).
        private static float Noise(int n)
        {
            uint x = (uint)(n * 1664525 + 1013904223);
            x ^= x >> 13; x *= 0x5bd1e995u; x ^= x >> 15;
            return (x / (float)uint.MaxValue) * 2f - 1f;
        }

        private static float Chirp(float t, float at, float freq, float dur)
        {
            float u = t - at;
            if (u < 0f || u > dur) return 0f;
            float window = Mathf.Sin(Mathf.PI * (u / dur)); // a soft bell so it doesn't click
            return Mathf.Sin(u * 2f * Mathf.PI * freq) * window;
        }

        // Fade a loop's head/tail so the seam doesn't click.
        private static void CrossfadeSeam(float[] data)
        {
            int fade = Mathf.Min(1024, data.Length / 8);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] *= k;
                data[data.Length - 1 - i] *= k;
            }
        }
    }
}
