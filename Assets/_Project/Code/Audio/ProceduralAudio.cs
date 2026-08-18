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

        // Rowing: one unhurried stroke. The loop is an exact whole number of these.
        private const float StrokeSeconds = 2.6f;
        // Outboard: a small two-stroke at a steady mid throttle, and a loop that is a whole
        // number of firings so the engine seams cleanly (engines expose a seam worse than anything).
        private const float FiringHz = 41f;
        private const float FiringCycles = 82f;   // → 2.0 s exactly
        private const float LopeCycles = 3f;      // whole lope cycles inside that loop

        /// <summary>The always-on calm-sea wash. Brown-ish noise through a moving low-pass, swelled by
        /// three incommensurate LFOs so no wave ever lands on the same beat twice, with a quiet foam layer
        /// that only opens on the swell peaks. Long and eventless by design: the director thins this bed as
        /// the wind tell rises, so anything that pokes through a duck is a defect (bible §8.1).</summary>
        public static AudioClip CalmSeaBed() => BuildBuffer("ph_calm_bed", 11.3f, true, data =>
        {
            var pink = new Pink();
            var body = new OnePole();
            var foamHp = new OnePole();
            for (int n = 0; n < data.Length; n++)
            {
                float t = (float)n / SampleRate;
                // Three slow swells at deliberately non-harmonic rates — their sum never repeats inside the loop.
                float swell = 0.40f + 0.24f * Sine(t * 0.083f) + 0.20f * Sine(t * 0.117f) + 0.16f * Sine(t * 0.051f);
                swell = Mathf.Clamp01(swell);

                float w = pink.Next(Noise(n));
                // Breaking water gets brighter as it swells: 190 Hz at rest → ~700 Hz at the peak.
                float wash = body.Low(w, 190f + 520f * swell);
                // Foam is high-passed hiss that only exists near the top of a swell.
                float foam = (w - foamHp.Low(w, 1100f)) * Mathf.Pow(swell, 3.2f) * 0.30f;
                data[n] = (wash * (0.55f + 0.45f * swell) + foam) * 0.42f;
            }
        });

        /// <summary>Sparse gull calls over true silence. Each call is a harmonic stack (gulls are bright and
        /// harsh, not sinusoidal) with a rasp of noise, bent through a pitch contour and shaped by a formant
        /// band — a long wail, then the short barking "laugh" that follows it.</summary>
        public static AudioClip GullCalls() => BuildBuffer("ph_gulls", 9.7f, true, data =>
        {
            var formant = new Svf();
            // (start, kind) — kind 0 = the long wail, 1 = a short bark. Placed off the grid so the loop
            // does not announce itself; the tail is left empty for the seam crossfade to work into.
            var calls = new (float at, int kind, float gain)[]
            {
                (0.62f, 0, 1.00f), (1.31f, 1, 0.72f), (1.52f, 1, 0.60f), (1.71f, 1, 0.48f),
                (3.94f, 0, 0.55f),
                (5.28f, 0, 0.85f), (5.98f, 1, 0.62f), (6.19f, 1, 0.52f),
                (8.10f, 1, 0.40f),
            };
            for (int n = 0; n < data.Length; n++)
            {
                float t = (float)n / SampleRate;
                float src = 0f, env = 0f, centre = 1800f;
                foreach (var c in calls)
                {
                    float u = t - c.at;
                    float dur = c.kind == 0 ? 0.52f : 0.11f;
                    if (u < 0f || u > dur) continue;
                    float k = u / dur;
                    // The wail climbs then falls away; the bark just falls.
                    float f0 = c.kind == 0
                        ? 620f + 340f * Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.55f)) - 150f * k
                        : 760f - 230f * k;
                    // A rich stack — the odd harmonics are what make it read as a bird and not a flute.
                    float ph = f0 * u;
                    float tone = Sine(ph) + 0.62f * Sine(ph * 2f) + 0.44f * Sine(ph * 3f)
                               + 0.26f * Sine(ph * 4f) + 0.17f * Sine(ph * 5f);
                    // Gulls have a distinct throat rattle — a fast tremolo on the wail only.
                    float rattle = c.kind == 0 ? 0.82f + 0.18f * Sine(t * 34f) : 1f;
                    float shape = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.7f));   // soft bell, no click
                    src += (tone * 0.55f + Noise(n) * 0.30f) * shape * rattle * c.gain;
                    env = Mathf.Max(env, shape * c.gain);
                    centre = c.kind == 0 ? 2100f : 2600f;
                }
                // The filter runs ALWAYS so its state stays continuous — gating the call instead of
                // the output would freeze it mid-ring and resume stale at the next gull. Silence
                // between calls stays true silence, which is what hides the loop point.
                float voiced = formant.Band(src, centre, 1.7f) * 0.34f;
                data[n] = env <= 0f ? 0f : voiced;
            }
        });

        /// <summary>Aboard a rowed hull: two full oar strokes per loop. Each is a rowlock creak (two wood
        /// resonances bent by the load), the blade's catch and pull as filtered water noise, and the drip off
        /// the feathered blade. Loop length is an exact whole number of strokes or the rowing limps.</summary>
        public static AudioClip HullRow() => BuildBuffer("ph_hull_row", 2f * StrokeSeconds, true, data =>
        {
            var creakA = new Svf();
            var creakB = new Svf();
            var pull = new Svf();
            var drip = new Svf();
            for (int n = 0; n < data.Length; n++)
            {
                float t = (float)n / SampleRate;
                float u = Mathf.Repeat(t, StrokeSeconds);
                float w = Noise(n);

                // The catch: blade bites, load comes on over ~0.35 s, then releases.
                float load = u < 0.9f ? Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(u / 0.9f), 0.8f)) : 0f;
                // Wood under load creaks UP in pitch as it tightens — that bend is the whole tell.
                float exc = (w * 0.5f + Sine(t * 41f) * 0.2f) * load * load;
                float creak = creakA.Band(exc, 178f + 46f * load, 6.5f) * 0.85f
                            + creakB.Band(exc, 437f + 88f * load, 5.0f) * 0.40f;

                // The pull: broadband water noise swelling with the load, low and heavy.
                float water = pull.Band(w, 300f + 260f * load, 0.85f) * load * 0.55f;

                // The release: a few drips off the blade as it feathers clear.
                float d = 0f;
                for (int i = 0; i < 3; i++)
                {
                    float du = u - (1.02f + i * 0.085f);
                    if (du > 0f && du < 0.06f)
                        d += drip.Band(w, 2400f - i * 300f, 3f) * Mathf.Exp(-du * 90f) * 0.30f;
                }
                data[n] = (creak * 0.42f + water + d) * 0.9f;
            }
        });

        /// <summary>Aboard an engine boat: a small outboard at a steady mid throttle. A firing impulse train
        /// through the exhaust's resonance, plus intake noise gated by the firing phase and a steady wash of
        /// prop water. Deliberately mid-RPM and even — the director rides its PITCH and volume with speed over
        /// ground, so any rev baked in here fights that.</summary>
        public static AudioClip OutboardEngine() => BuildBuffer("ph_outboard_engine", FiringCycles / FiringHz, true, data =>
        {
            var pipe = new Svf();
            var body = new OnePole();
            var intake = new Svf();
            var wash = new OnePole();
            for (int n = 0; n < data.Length; n++)
            {
                float t = (float)n / SampleRate;
                float phase = Mathf.Repeat(t * FiringHz, 1f);       // 0..1 across one firing
                // A slow lope that completes a whole number of cycles inside the loop, so it still seams.
                float lope = 0.88f + 0.12f * Sine(t * (LopeCycles / (FiringCycles / FiringHz)));

                // The firing impulse: sharp, then the exhaust pipe rings it out.
                float fire = Mathf.Exp(-phase * 26f) * (1f - phase * 0.3f);
                float exhaust = pipe.Band(fire, 148f, 1.9f) * 1.6f + body.Low(fire, 320f) * 0.7f;
                // Intake draw — noise that only opens on the induction half of the cycle.
                float draw = intake.Band(Noise(n), 900f, 1.1f) * Mathf.Max(0f, Sine(phase * 0.5f)) * 0.28f;
                // Prop and hull water, always there under it.
                float water = wash.Low(Noise(n), 420f) * 0.20f;

                data[n] = (exhaust * 0.52f + draw + water) * lope * 0.46f;
            }
        });

        /// <summary>The canon-sacred rising-wind tell (bible §8.4 — you can hear trouble coming). Pink noise
        /// through two resonant bands: the low moan a shroud makes, and the higher edge-tone that rides over
        /// it. Deliberately EVEN — the director drives loudness from wind strength, so a gust recorded in here
        /// would fight the curve. It drifts; it never gusts.</summary>
        public static AudioClip WindTell() => BuildBuffer("ph_wind_tell", 13.1f, true, data =>
        {
            var pink = new Pink();
            var moan = new Svf();
            var edge = new Svf();
            var rumble = new OnePole();
            for (int n = 0; n < data.Length; n++)
            {
                float t = (float)n / SampleRate;
                float w = pink.Next(Noise(n));
                // Slow, shallow drift on non-harmonic rates: alive, but nothing that reads as an event.
                float driftA = Sine(t * 0.071f), driftB = Sine(t * 0.113f), driftC = Sine(t * 0.037f);

                float low = moan.Band(w, 240f + 60f * driftA, 1.35f) * (0.80f + 0.20f * driftB);
                float high = edge.Band(w, 1250f + 320f * driftB, 2.4f) * (0.55f + 0.25f * driftC);
                float floorRumble = rumble.Low(Noise(n), 110f) * 0.35f;

                data[n] = (low * 1.15f + high * 0.55f + floorRumble) * 0.40f;
            }
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

        // How much material the loop seam crossfades over (~23 ms). Long enough to hide a noise
        // seam, short enough not to smear a rhythmic one.
        private const int SeamFade = 1024;

        /// <summary>Per-sample build, for cues simple enough to be a pure function of (t, n).</summary>
        private static AudioClip Build(string name, float seconds, bool loop, Func<float, int, float> sample)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            int fade = loop ? Mathf.Min(SeamFade, count / 4) : 0;

            // Render PAST the loop end: the overhang is what the seam crossfades in from.
            var work = new float[count + fade];
            for (int n = 0; n < work.Length; n++)
                work[n] = Mathf.Clamp(sample((float)n / SampleRate, n), -1f, 1f);

            return Finish(name, work, count, fade);
        }

        /// <summary>Whole-buffer build, for beds that need filter state (anything with a real
        /// resonance or a moving cutoff — which is most of what water and wind actually are).</summary>
        private static AudioClip BuildBuffer(string name, float seconds, bool loop, Action<float[]> fill)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            int fade = loop ? Mathf.Min(SeamFade, count / 4) : 0;

            var work = new float[count + fade];
            fill(work);
            for (int n = 0; n < work.Length; n++) work[n] = Mathf.Clamp(work[n], -1f, 1f);

            return Finish(name, work, count, fade);
        }

        private static AudioClip Finish(string name, float[] work, int count, int fade)
        {
            var data = new float[count];
            Array.Copy(work, data, count);

            // Wrap the overhang back over the head. The tail is left untouched and flows straight
            // into data[0] — which IS its natural continuation — so the loop is continuous.
            // (Fading BOTH ends to zero, as this used to, punches a hole at every loop point.)
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] = data[i] * k + work[count + i] * (1f - k);
            }

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

        /// <summary>Sine by CYCLES rather than radians — every generator here thinks in Hz × seconds.</summary>
        private static float Sine(float cycles) => Mathf.Sin(cycles * 2f * Mathf.PI);

        private static float Chirp(float t, float at, float freq, float dur)
        {
            float u = t - at;
            if (u < 0f || u > dur) return 0f;
            float window = Mathf.Sin(Mathf.PI * (u / dur)); // a soft bell so it doesn't click
            return Mathf.Sin(u * 2f * Mathf.PI * freq) * window;
        }

        // ---- minimal DSP -----------------------------------------------------------------------
        // Three primitives are enough for every bed above. All carry state, which is exactly what
        // the old per-sample signature could not express — and filtered noise IS sea and wind.

        /// <summary>One-pole low-pass with a per-sample cutoff (so a bed can open and close).</summary>
        private sealed class OnePole
        {
            private float _y;
            public float Low(float x, float hz)
            {
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Clamp(hz, 10f, SampleRate * 0.45f) / SampleRate);
                _y += (x - _y) * a;
                return _y;
            }
        }

        /// <summary>Chamberlin state-variable filter, band output — the resonance that turns flat
        /// noise into a moan, a creak or an exhaust note. Cutoff and Q are clamped to keep the
        /// two-pole topology unconditionally stable (f + damp &lt; 2).</summary>
        private sealed class Svf
        {
            private float _low, _band;
            public float Band(float x, float hz, float q)
            {
                float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Clamp(hz, 20f, SampleRate * 0.1f) / SampleRate);
                float damp = Mathf.Clamp(1f / Mathf.Max(q, 0.1f), 0.05f, 1.2f);
                _low += f * _band;
                float high = x - _low - damp * _band;
                _band += f * high;
                return _band;
            }
        }

        /// <summary>Pink noise (Paul Kellet's filter bank). White is too hissy for water and wind —
        /// natural broadband sound falls off with frequency, and the ear hears the difference at once.</summary>
        private sealed class Pink
        {
            private float _b0, _b1, _b2, _b3, _b4, _b5, _b6;
            public float Next(float w)
            {
                _b0 = 0.99886f * _b0 + w * 0.0555179f;
                _b1 = 0.99332f * _b1 + w * 0.0750759f;
                _b2 = 0.96900f * _b2 + w * 0.1538520f;
                _b3 = 0.86650f * _b3 + w * 0.3104856f;
                _b4 = 0.55000f * _b4 + w * 0.5329522f;
                _b5 = -0.7616f * _b5 - w * 0.0168980f;
                float p = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + w * 0.5362f;
                _b6 = w * 0.115926f;
                return p * 0.11f;
            }
        }
    }
}
