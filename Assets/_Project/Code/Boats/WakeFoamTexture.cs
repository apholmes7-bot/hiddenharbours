using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The PURE, headless-testable generator behind the wake's drawn matter: the AERATED BUBBLE RAFT
    /// the foam is made of, and the CREST PROFILE the wake wave is made of. It returns plain float /
    /// colour fields; <see cref="BoatWakeEmitter"/> bands + dithers them into the shared sprites.
    ///
    /// <para><b>Why this exists (owner playtest 2026-08-06): "the wake foam should BUBBLE, not paint
    /// a solid line".</b> The trail's PLACEMENT was already right — deposits laid per metre of track,
    /// arm streaks long enough to overlap by construction (<see cref="WakeTrailMath.ArmStreakLength"/>),
    /// churn puffs big enough to merge. What was wrong was the MATTER those rules placed: one solid
    /// round disc and one solid tapered bar, both with a clean outline. Overlap solid discs densely
    /// enough to satisfy "never a dotted row" (the 2026-07-23 ask) and you have necessarily painted a
    /// continuous stripe — the two asks pull against each other as long as the unit of foam is
    /// opaque.</para>
    ///
    /// <para><b>So the fix is in the MATTER, not the tuning.</b> Keep every placement law exactly as
    /// it is — the arms stay continuous, the churn band stays dense — and make the thing being placed
    /// a raft of BUBBLES with real holes through it. Dense overlap of torn mats reads as aerated white
    /// water; dense overlap of discs reads as paint. Re-tuning the spacing back toward gaps would have
    /// swung straight back to the dotted read the last pass fixed, which is why nothing here touches
    /// <see cref="WakeTrailConfig"/>'s spacing or overlap factor.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Every bubble position, radius and rim weight is a
    /// deterministic hash of (variant, index) — no <see cref="Random"/>, no time. The same variant
    /// always produces the same field, which is what lets a test pin the shape headless.</para>
    ///
    /// <para><b>Cost (rule 7).</b> A fixed, tiny number of variants is generated ONCE at boot and
    /// shared by every particle of every boat; a pool slot keeps the variant (and mirror) it was
    /// built with, so nothing is assigned per frame and the whole wake still batches.</para>
    /// </summary>
    public static class WakeFoamTexture
    {
        /// <summary>How many distinct bubble rafts are generated. Small on purpose: with the per-slot
        /// mirroring below, four flips × this many rafts is plenty of variety at foam scale, and every
        /// sprite still lives in the batched shared set.</summary>
        public const int VariantCount = 6;

        /// <summary>Bubbles in one raft. Enough to leave real gaps between them at puff scale; few
        /// enough that each bubble is still several pixels across on a 16 px puff.</summary>
        public const int BubblesPerRaft = 7;

        /// <summary>
        /// Rungs on the AERATION LADDER: how many progressively-more-open rafts are baked per variant so a
        /// puff can visibly OPEN UP as it ages (owner playtest 2026-08-07, "dispersing back to water over
        /// time") instead of only dimming.
        ///
        /// <para>Four is chosen against the two costs. Texture memory: 6 variants × 4 rungs × 16² px = 6 KB
        /// total, built once at boot and shared by every particle of every boat. Per-frame writes: a puff
        /// crosses a rung three times in a whole lifetime, and the renderer only assigns
        /// <c>sprite</c> on a crossing — so this stays the "nothing per frame" the raft pass paid for
        /// (rule 7). Fewer rungs and the opening-up steps visibly; more and the memory buys nothing the eye
        /// can read at 16 px. When <c>WakeTrailConfig.FoamAgeErosion</c> is 0 only ONE rung is built, so
        /// the off switch costs no memory either.</para>
        /// </summary>
        public const int AgeStages = 4;

        /// <summary>Native pixel size of a foam puff (square). 16 px at PPU 32 = half a metre.</summary>
        public const int PuffPixels = 16;

        /// <summary>Native long-axis pixels of the wake-wave crest sprite.</summary>
        public const int WavePixelsX = 32;

        /// <summary>Native cross-axis pixels of the wake-wave crest sprite. Taller than the old 5 px
        /// crest LINE because this sprite has to carry a crest AND the trough under it.</summary>
        public const int WavePixelsY = 12;

        /// <summary>Where the undisturbed waterline sits in the wave sprite, 0 = bottom, 1 = top. The
        /// pivot goes here, so placing the sprite at a deposit puts flat water at the deposit and lets
        /// the crest stand above it and the trough fall below.</summary>
        public const float WaveWaterlineV = 0.5f;

        // ==== the AERATED BUBBLE RAFT ==============================================================

        /// <summary>
        /// One raft's coverage field, row-major, <paramref name="size"/> × <paramref name="size"/>,
        /// each value 0..1.
        ///
        /// <para><paramref name="aeration01"/> is the A/B dial and the honest off switch: at <b>0</b>
        /// this returns <see cref="SolidPuffCoverage"/> BIT-FOR-BIT (the shipped solid disc), so the
        /// whole change can be reverted from one number. Above 0 the disc is replaced by a raft of
        /// bubble films — each bubble bright at its RIM and thin through its middle, which is what a
        /// bubble actually is and what leaves holes for the water to show through.</para>
        /// </summary>
        public static float[] BuildRaftCoverage(int variant, int size, float aeration01)
        {
            int n = Mathf.Max(2, size);
            float aeration = Mathf.Clamp01(aeration01);
            if (aeration <= 0f) return SolidPuffCoverage(n);

            var field = new float[n * n];
            float centre = (n - 1) * 0.5f;
            float radius = n * 0.5f;

            // Bubble centres are laid on a deterministic spiral inside the unit disc rather than by
            // rejection sampling: it guarantees they SPREAD (a hashed scatter clumps, and a clumped
            // raft is a disc again with a ragged edge) while the hashes still decorrelate the variants.
            for (int b = 0; b < BubblesPerRaft; b++)
            {
                uint key = (uint)(variant * 0x9E3779B1) + (uint)(b * 0x85EBCA6B) + 0x27D4EB2Fu;
                float jitterA = WakeParticleSystem.Hash01(key) - 0.5f;
                float jitterR = WakeParticleSystem.Hash01(key * 2654435761u + 11u) - 0.5f;
                float jitterS = WakeParticleSystem.Hash01(key * 40503u + 7u);

                // Golden-angle spiral: even angular spread, radius growing as sqrt so area is even too.
                float angle = b * 2.399963f + jitterA * 1.1f;
                float ring = Mathf.Sqrt((b + 0.5f) / BubblesPerRaft) * 0.62f + jitterR * 0.10f;
                float bx = Mathf.Cos(angle) * ring;
                float by = Mathf.Sin(angle) * ring;
                // Smaller bubbles further out — the raft's edge frays instead of ending in a rim.
                float br = Mathf.Lerp(0.34f, 0.20f, Mathf.Clamp01(ring / 0.72f)) * (0.75f + jitterS * 0.5f);

                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - centre) / radius - bx;
                    float dy = (y - centre) / radius - by;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d >= br) continue;

                    // The FILM: bright at the rim, thin through the middle. `rim` is 1 at the bubble's
                    // edge and falls toward its centre; `body` keeps a little fill so a bubble is a
                    // bubble and not an outline. Aeration dials how much of the middle is opened up.
                    float t = d / Mathf.Max(br, 1e-4f);          // 0 centre .. 1 rim
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(t - 0.82f) / 0.34f);
                    float body = 1f - t * t;
                    float v = Mathf.Lerp(body, Mathf.Max(rim, body * 0.35f), aeration);
                    int i = y * n + x;
                    if (v > field[i]) field[i] = v;               // rafts COMPOSE by max, never by sum
                }
            }

            return field;
        }

        /// <summary>
        /// One rung of the AGE LADDER: the raft with <paramref name="erosion01"/> of its film DISSOLVED —
        /// <c>(v − e) / (1 − e)</c>, clamped. The owner's 2026-08-07 read, <i>"dispersing back to water
        /// over time"</i>.
        ///
        /// <para><b>Why erosion and not more aeration.</b> The obvious move — walk
        /// <see cref="BuildRaftCoverage"/>'s aeration up with age — was tried and MEASURED, and it does the
        /// opposite: from the shipped 0.85 to 1.0 the raft's mean coverage RISES (0.172 → 0.179 on variant
        /// 0) and its hole count is not even monotone, because that axis brightens bubble RIMS faster than
        /// it hollows bubble middles. Aeration turns a disc into a raft; it was never a dial for how much
        /// raft there is. Erosion is: every texel loses the same absolute coverage, so the thin film
        /// between bubbles reaches zero first and the holes eat outward from it, while the bright rims
        /// survive longest and stay crisp. That is foam breaking into separate bubbles and then into water
        /// — and, unlike fading alpha, it changes the SHAPE, which is what still read as painted.</para>
        ///
        /// <para>Monotone by construction: raising the erosion can only lower a texel and can only add
        /// holes. <paramref name="erosion01"/> = 0 returns the field unchanged. Pure + static.</para>
        /// </summary>
        public static float[] ErodeCoverage(float[] field, float erosion01)
        {
            if (field == null) return System.Array.Empty<float>();
            float e = Mathf.Clamp01(erosion01);
            var result = new float[field.Length];
            if (e <= 0f)
            {
                System.Array.Copy(field, result, field.Length);
                return result;
            }
            float denominator = Mathf.Max(1e-4f, 1f - e);
            for (int i = 0; i < field.Length; i++)
                result[i] = Mathf.Clamp01((field[i] - e) / denominator);
            return result;
        }

        /// <summary>
        /// ⚠️ <b>The shipped solid disc, kept as the A/B floor and as what the tests measure the raft
        /// against</b> (the <c>FoamBuffer.CameraRelativeOrigin</c> idiom): a round falloff with a
        /// tightened core, monotonically non-increasing from the centre outward. That monotonicity is
        /// exactly the property a raft must NOT have — it is what makes a dense trail of these read as
        /// one painted stripe.
        /// </summary>
        public static float[] SolidPuffCoverage(int size)
        {
            int n = Mathf.Max(2, size);
            var field = new float[n * n];
            float centre = (n - 1) * 0.5f;
            float radius = n * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - centre) / radius, dy = (y - centre) / radius;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                field[y * n + x] = a * a;                          // tighten the core
            }
            return field;
        }

        /// <summary>
        /// How many times coverage REVERSES direction walking outward along rays from the centre —
        /// the threshold-free measure of "is this a raft or a disc".
        ///
        /// <para>A solid disc falls monotonically from its centre, so it reports (near) zero however
        /// it is scaled or tinted. A bubble raft crosses rims and gaps, so it reports many. The test
        /// asserts the PROPERTY rather than restating the generator's own formula back at it.</para>
        /// </summary>
        public static int RadialReversals(float[] field, int size, int rays = 32)
        {
            int n = Mathf.Max(2, size);
            if (field == null || field.Length < n * n) return 0;
            float centre = (n - 1) * 0.5f;
            float radius = n * 0.5f;
            int reversals = 0;

            for (int r = 0; r < rays; r++)
            {
                float angle = r * (Mathf.PI * 2f / rays);
                float cx = Mathf.Cos(angle), cy = Mathf.Sin(angle);
                int lastSign = 0;
                float previous = -1f;
                for (int s = 0; s <= n; s++)
                {
                    float t = s / (float)n;                        // 0 centre .. 1 edge
                    int x = Mathf.Clamp(Mathf.RoundToInt(centre + cx * t * radius), 0, n - 1);
                    int y = Mathf.Clamp(Mathf.RoundToInt(centre + cy * t * radius), 0, n - 1);
                    float v = field[y * n + x];
                    if (previous >= 0f)
                    {
                        float delta = v - previous;
                        // A dead band, so texel-rounding noise on a smooth disc is not read as a hole.
                        int sign = delta > 0.02f ? 1 : (delta < -0.02f ? -1 : 0);
                        if (sign != 0 && lastSign != 0 && sign != lastSign) reversals++;
                        if (sign != 0) lastSign = sign;
                    }
                    previous = v;
                }
            }
            return reversals;
        }

        /// <summary>Which raft a pool slot draws. Hashed (not <c>slot % VariantCount</c>) so a long
        /// trail never marches through the variants in a visible cycle.</summary>
        public static int VariantForSlot(int slot)
            => (int)(WakeParticleSystem.Hash01((uint)slot * 0x9E3779B1u + 0x1Bu) * VariantCount) % VariantCount;

        /// <summary>Whether a pool slot mirrors its raft in X / Y. Four flips × <see cref="VariantCount"/>
        /// rafts is the variety the wake actually shows, at zero extra texture memory.</summary>
        public static void MirrorForSlot(int slot, out bool flipX, out bool flipY)
        {
            flipX = WakeParticleSystem.Hash01((uint)slot * 0x85EBCA6Bu + 0x3Du) < 0.5f;
            flipY = WakeParticleSystem.Hash01((uint)slot * 0xC2B2AE35u + 0x77u) < 0.5f;
        }

        // ==== the WAKE WAVE's crest profile ========================================================

        /// <summary>
        /// The wake wave's cross-section, as a coverage / shade pair per row of the sprite:
        /// <paramref name="v"/> runs ACROSS the direction the wave travels — 0 behind it, 1 ahead of
        /// it — with the undisturbed waterline at <see cref="WaveWaterlineV"/>.
        ///
        /// <para><b>Why the sprite carries a TROUGH and not just a white line.</b> The thing this
        /// replaces was an authored white V decal — foam pretending to be a wave. Water displaced by a
        /// hull is a crest with a HOLLOW behind it, and in a ¾ plan view that reads exactly the way the
        /// sea shader already draws its own swell: light and shade laid across the travel direction,
        /// not a bright bar. So the returned <c>shade</c> runs from +1 (a lit crest, lightening the
        /// sea) through 0 at the waterline to −1 (a hollow, darkening it), and the caller bakes that
        /// into the sprite's RGB. The sprite is then tinted near-white and the PROFILE is what reads —
        /// which is also why the emitter has to point each ribbon's cross axis the right way
        /// (<see cref="WakeWaveMath.ArmCrestOrientDeg"/>).</para>
        /// </summary>
        /// <param name="v">Row position across the sprite, 0..1.</param>
        /// <param name="opacity">Coverage 0..1 at this row (0 at both extremes — the wave has soft feet).</param>
        /// <param name="shade">−1 trough .. 0 waterline .. +1 lit crest.</param>
        public static void WaveProfile(float v, out float opacity, out float shade)
        {
            float t = Mathf.Clamp01(v);
            float w = Mathf.Clamp01(WaveWaterlineV);

            if (t >= w)
            {
                // The CREST: rises out of the waterline, brightest just under its lip, and the very
                // top edge softens so the crest does not end in a ruled line.
                float up = Mathf.Clamp01((t - w) / Mathf.Max(1f - w, 1e-4f));   // 0 waterline .. 1 lip
                opacity = Mathf.Sin(up * Mathf.PI * 0.92f);                     // 0 at the base, 0 at the lip
                shade = Mathf.Clamp01(up * 1.15f);
            }
            else
            {
                // The TROUGH: the hollow the crest was lifted out of. Shallower and softer than the
                // crest — a wake's back face is a shadow, not a second crest.
                float down = Mathf.Clamp01((w - t) / Mathf.Max(w, 1e-4f));      // 0 waterline .. 1 bottom
                opacity = Mathf.Sin(down * Mathf.PI) * 0.62f;
                shade = -Mathf.Clamp01(down * 1.3f);
            }
        }

        /// <summary>
        /// How much the crest's height varies ALONG its length at <paramref name="u"/> (0..1) —
        /// a bounded, deterministic undulation, returned as a multiplier around 1.
        ///
        /// <para>A wake crest is not a ruler. Without this the wave sprite would be a perfectly
        /// straight bar and the arms would read as drawn lines again, which is the defect this whole
        /// pass exists to leave behind. Bounded to [1 − amount, 1 + amount], so it can shape the crest
        /// and never erase it.</para>
        /// </summary>
        public static float CrestUndulation(float u, float amount)
        {
            float a = Mathf.Clamp01(amount);
            if (a <= 0f) return 1f;
            float s = Mathf.Sin(u * Mathf.PI * 6.28f) * 0.6f + Mathf.Sin(u * Mathf.PI * 11.3f + 1.7f) * 0.4f;
            return 1f + s * a;
        }

        /// <summary>The along-length taper of the crest: 1 through the middle, 0 at both ends, so
        /// consecutive overlapping streaks fuse into one crest instead of stacking visible seams.</summary>
        public static float LengthTaper(float u)
        {
            float x = Mathf.Clamp01(u) * 2f - 1f;                  // −1 .. 1
            float taper = Mathf.Clamp01(1f - x * x);
            return taper * taper;
        }
    }
}
