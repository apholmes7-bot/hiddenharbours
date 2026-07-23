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

        /// <summary>A looping low outboard-engine bed (a small "putt-putt" motor). The director lifts
        /// its pitch + volume with the boat's speed over ground, so this is just the idle texture.</summary>
        public static AudioClip OutboardEngine() => Build("ph_outboard_engine", 1f, true, (t, n) =>
        {
            float chug = 0.7f + 0.3f * Mathf.Sin(t * 2f * Mathf.PI * 6f);   // ~6 Hz combustion chug
            float body = Mathf.Sin(t * 2f * Mathf.PI * 70f) * 0.6f          // fundamental
                       + Mathf.Sin(t * 2f * Mathf.PI * 140f) * 0.25f;       // octave
            float grit = Noise(n) * 0.12f;
            return (body + grit) * 0.28f * chug;
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

        // ---- rod-fishing v2 fight set (played by FishingAudio; design §2–3) ------------------

        /// <summary>The wind-back rod creak — slow fibrous working of loaded wood (loop; the
        /// director deepens its gain as the rod loads).</summary>
        public static AudioClip RodCreak() => Build("ph_rod_creak", 1.2f, true, (t, n) =>
        {
            float work  = 0.55f + 0.45f * Mathf.Sin(t * 2f * Mathf.PI * (2f / 1.2f));  // 2 slow works per loop
            float fibre = Mathf.Sin(t * 2f * Mathf.PI * 130f + 2.5f * Mathf.Sin(t * 2f * Mathf.PI * 9f));
            return (fibre * 0.5f + Noise(n) * 0.35f) * 0.2f * work;
        });

        /// <summary>The flick released — rod whip whoosh with a rising line whistle riding it.</summary>
        public static AudioClip CastWhoosh() => Build("ph_cast_whoosh", 0.6f, false, (t, n) =>
        {
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.45f));
            float whoosh = Noise(n) * env * env * 0.3f;
            // Linear chirp 900→2200 Hz over the clip: phase = 2π(f0·t + (Δf/2T)·t²).
            float whistle = Mathf.Sin(2f * Mathf.PI * (900f * t + (1300f / (2f * 0.6f)) * t * t)) * 0.07f * env;
            return whoosh + whistle;
        });

        /// <summary>Splash-down as the cast line lands (the sound of the art lane's SplashBurst).</summary>
        public static AudioClip SplashDown() => Build("ph_splash_down", 0.5f, false, (t, n) =>
        {
            float body = Noise(n) * Mathf.Exp(-t * 9f) * 0.32f;
            float plop = Mathf.Sin(2f * Mathf.PI * (320f * t - 480f * t * t)) * Mathf.Exp(-t * 12f) * 0.25f;
            return body + plop;
        });

        /// <summary>The cast-path bite tell — the bobber's little pitch-dropping blub.</summary>
        public static AudioClip BobberPlop() => Build("ph_bobber_plop", 0.3f, false, (t, n) =>
        {
            float blub = Mathf.Sin(2f * Mathf.PI * (620f * t - 1700f * t * t)) * Mathf.Exp(-t * 18f);
            float drop = Noise(n) * Mathf.Exp(-t * 60f) * 0.15f;
            return blub * 0.35f + drop;
        });

        /// <summary>The depth-path bite tell — two damped low knocks that feel IN the rod, not the UI.</summary>
        public static AudioClip RodKnock() => Build("ph_rod_knock", 0.35f, false, (t, n) =>
        {
            float Knock(float u) => u < 0f ? 0f
                : (Mathf.Sin(2f * Mathf.PI * 82f * u) * 0.8f + Mathf.Sin(2f * Mathf.PI * 250f * u) * 0.3f)
                  * Mathf.Exp(-u * 30f);
            return (Knock(t) + Knock(t - 0.13f) * 0.85f) * 0.5f;
        });

        /// <summary>The sinking reel pay-out — a soft tick train (~12/s at pitch 1; the director
        /// slows its pitch as the rig nears bottom, §2.3).</summary>
        public static AudioClip PayoutTick() => Build("ph_payout_tick", 0.5f, true, (t, n) =>
        {
            float phase = Mathf.Repeat(t, 1f / 12f);
            return Noise(n) * Mathf.Exp(-phase * 220f) * 0.35f;
        });

        /// <summary>The "you felt bottom" note — a soft low thub as the rig settles slack on the floor.</summary>
        public static AudioClip BottomSettle() => Build("ph_bottom_settle", 0.45f, false, (t, n) =>
        {
            float thub = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 18f) * 0.5f;
            float note = Mathf.Sin(2f * Mathf.PI * (330f * t - 110f * t * t)) * Mathf.Exp(-t * 7f) * 0.12f;
            return thub + note;
        });

        /// <summary>The line-strain groan (loop) — the continuous "ease off!" voice; the director
        /// scales gain and tightens pitch with Tension01.</summary>
        public static AudioClip StrainGroan() => Build("ph_strain_groan", 1.5f, true, (t, n) =>
        {
            float vib = 1f + 0.04f * Mathf.Sin(t * 2f * Mathf.PI * 4.5f);
            float body = Mathf.Sin(t * 2f * Mathf.PI * 62f * vib)
                       + Mathf.Sin(t * 2f * Mathf.PI * 124f * vib) * 0.4f;
            float grit  = Noise(n) * 0.18f;
            float surge = 0.7f + 0.3f * Mathf.Sin(t * 2f * Mathf.PI * (2f / 1.5f)); // 2 surges per loop
            return (body + grit) * 0.2f * surge;
        });

        /// <summary>Reel clicks while gaining line (loop) — a brighter ratchet (~14 clicks/s).</summary>
        public static AudioClip ReelClicks() => Build("ph_reel_clicks", 0.42f, true, (t, n) =>
        {
            float phase = Mathf.Repeat(t, 1f / 14f);
            float ping = Mathf.Sin(phase * 2f * Mathf.PI * 1800f) * 0.5f + Noise(n) * 0.5f;
            return ping * Mathf.Exp(-phase * 300f) * 0.45f;
        });

        /// <summary>The mid-fight slack window opening — a soft downward twang: the line just went
        /// loose (the diegetic "PULL now").</summary>
        public static AudioClip SlackRelease() => Build("ph_slack_release", 0.35f, false, (t, n) =>
        {
            float twang = Mathf.Sin(2f * Mathf.PI * (540f * t - 700f * t * t)) * Mathf.Exp(-t * 10f) * 0.3f;
            float air = Noise(n) * Mathf.Exp(-t * 24f) * 0.18f;
            return twang + air;
        });

        /// <summary>The surface thrash churn (loop) — splashy bursts; the director swells it with
        /// how hard she's darting and pans it on her offset.</summary>
        public static AudioClip SurfaceThrash() => Build("ph_surface_thrash", 1f, true, (t, n) =>
        {
            float churn = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * 3f), 2.2f);
            float slap  = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * 1f + 1.3f), 6f);
            return Noise(n) * (0.12f + 0.5f * churn + 0.6f * slap) * 0.45f;
        });

        /// <summary>She threw the hook — a COZY sting (soft string ping + a little sag), never a
        /// punishment sound (§7: a lost fish is a shrug).</summary>
        public static AudioClip SnapSting() => Build("ph_snap_sting", 0.55f, false, (t, n) =>
        {
            float ping = Mathf.Sin(2f * Mathf.PI * 1180f * t) * Mathf.Exp(-t * 22f) * 0.28f;
            float sag = Mathf.Sin(2f * Mathf.PI * (400f * t - 125f * t * t)) * Mathf.Exp(-t * 8f) * 0.15f;
            float poff = Noise(n) * Mathf.Exp(-t * 40f) * 0.15f;
            return ping + sag + poff;
        });

        /// <summary>Landed — a warm little two-note flourish and the wet slap on the boards.</summary>
        public static AudioClip LandedFlourish() => Build("ph_landed_flourish", 0.9f, false, (t, n) =>
        {
            float a = Mathf.Sin(2f * Mathf.PI * 392f * t) * Mathf.Exp(-t * 4f);            // G4
            float ub = t - 0.16f;
            float b = ub > 0f ? Mathf.Sin(2f * Mathf.PI * 523.25f * ub) * Mathf.Exp(-ub * 4f) : 0f; // C5
            float us = t - 0.45f;
            float slap = us > 0f
                ? Noise(n) * Mathf.Exp(-us * 30f) * 0.24f + Mathf.Sin(2f * Mathf.PI * 95f * us) * Mathf.Exp(-us * 20f) * 0.2f
                : 0f;
            return (a + b) * 0.16f + slap;
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
