using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The AGE RAMP of churned water — the pure, headless-testable law behind the owner's 2026-08-27 ask:
    /// <i>"the wakes behind the boat are still a solid white foam from wherever the boat interacts with,
    /// this should churn through different shades of blue, distort and fade into the ambient ocean over
    /// time."</i>
    ///
    /// <para><b>The defect this retires.</b> Every wake element shipped so far draws at ONE serialized
    /// colour for its whole life and varies only its ALPHA — white at birth, the same white at death, just
    /// more transparent. Age existed (a particle's <c>life01</c>; the foam buffer's decayed coverage) and
    /// simply never reached COLOUR. So the churn read as a solid white sheet dissolving, never as water.</para>
    ///
    /// <para><b>The law.</b> Foam is born at the sea's own FOAM anchor (white, only at the moment of churn),
    /// then walks DOWN the water's ramp — foam → shallow → mid — over its life. It is never given a colour
    /// of its own: every value returned is a convex combination of the live
    /// <see cref="SeaPaletteState"/> anchors (ADR 0015), so a preset swap or a mood turn moves the wake's
    /// blues with the sea's, and no hex is ever invented on a particle component. The last leg into the
    /// TRUE local sea is left to the existing alpha fade — the ambient ocean at that spot is whatever depth
    /// and light make it, and dissolving into it beats guessing it.</para>
    ///
    /// <para><b>Why the SCATTER is the load-bearing term.</b> The other half of the same ask is <i>"everything
    /// looks very organized and shader-like and not particle like"</i>. A ramp alone does not cure that: if
    /// every puff of one churn is the same age it is still one sheet, merely a bluer one. So each element
    /// carries a deterministic per-particle OFFSET along the ramp (<see cref="WakeAgeRamp.AgeScatter"/>) —
    /// at any instant a churn holds foam at many different ages at once, which is what a real churn is. Per-
    /// thing variance, not more pattern.</para>
    ///
    /// <para><b>Determinism &amp; purity (rule 5).</b> Every function here is a pure function of its
    /// arguments; the per-particle variation comes from the particle's own birth-baked seed through a stable
    /// integer avalanche, never <see cref="System.Random"/>. Presentation only: colour feeds no simulation
    /// and enters no save. <see cref="WakeAgeRamp.Strength"/> = 0 returns the caller's legacy colour
    /// BIT-EXACTLY, so the whole feature is one knob's worth of A/B (this repo's standing contract).</para>
    ///
    /// <para><b>The shader twin.</b> <c>HiddenHarboursWater.shader</c>'s advected-foam compose carries a
    /// transcription of <see cref="Age01FromFreshness"/>, <see cref="Knots"/> and <see cref="Ramp3"/> for
    /// the buffer's own age — its FRESHNESS channel. Change one, change BOTH in the same PR; a
    /// source-scrape test reads the shader and fails red on drift, the same discipline
    /// <c>ShoreFadeMath</c>/<c>Fade01</c> keeps.</para>
    /// </summary>
    public static class WakeFoamAgeing
    {
        /// <summary>Rec.601 luma — the SAME weights the water shader and <c>WaterPaletteGrade</c> use, so
        /// "the foam never brightens as it ages" is measured on the sea's own scale.</summary>
        public static float Luminance(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

        /// <summary>
        /// A second, decorrelated 0..1 value from a particle's birth seed. The seed is quantised to its 24
        /// significant bits and run through the same xorshift-multiply avalanche
        /// <c>WakeParticleSystem.Hash01</c> uses, salted per channel — so one seed yields as many
        /// independent per-particle variations as an element needs (its ramp offset, its shade jitter)
        /// without any of them correlating into a visible pattern. Pure, stable, allocation-free.
        /// </summary>
        public static float Decorrelate(float seed01, uint salt)
        {
            unchecked
            {
                uint x = (uint)(Mathf.Clamp01(seed01) * 16777215f) ^ (salt * 0x9E3779B9u);
                x ^= x >> 16; x *= 0x7feb352du;
                x ^= x >> 15; x *= 0x846ca68bu;
                x ^= x >> 16;
                return (x & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>
        /// Where along the three-stop ramp this element sits: <b>0 = the foam anchor</b> (the white of the
        /// churn itself), <b>0.5 = the shallow anchor</b>, <b>1 = the mid anchor</b>.
        ///
        /// <para>Piecewise-linear through three knots so each leg is independently tunable by the owner:
        /// it holds pure foam until <see cref="WakeAgeRamp.WhiteHold"/> of its life (the churn), reaches the
        /// shallow blue at <see cref="WakeAgeRamp.BlueReach"/>, and lands on the mid blue at
        /// <see cref="WakeAgeRamp.DeepReach"/>, holding there for whatever life remains. Non-decreasing in
        /// <paramref name="life01"/> by construction — water that has aged never gets younger.</para>
        ///
        /// <para><paramref name="seed01"/> shifts the whole curve by ±<see cref="WakeAgeRamp.AgeScatter"/>,
        /// deterministically per particle: the scatter that makes a churn read as many things rather than
        /// one sheet. Scatter 0 restores the exact shared curve.</para>
        /// </summary>
        public static float Age01(float life01, float seed01, in WakeAgeRamp ramp)
        {
            float t = Mathf.Clamp01(life01);

            float scatter = Mathf.Clamp01(ramp.AgeScatter);
            if (scatter > 0f) t = Mathf.Clamp01(t + (Decorrelate(seed01, 0x51u) - 0.5f) * 2f * scatter);

            return Knots(t, ramp.WhiteHold, ramp.BlueReach, ramp.DeepReach);
        }

        /// <summary>
        /// The three-knot piecewise-linear curve alone, with no per-particle scatter — <b>the half the water
        /// shader transcribes</b>.
        ///
        /// <para>The shader's advected foam buffer has no particles and therefore no seeds: its age proxy is
        /// the buffer's own decayed COVERAGE, one value per texel. So the scatter (which needs a per-thing
        /// seed) lives in <see cref="Age01"/> and this knot curve is the shared law both sides run. Keeping
        /// them as two functions is what makes the twin test able to compare like with like instead of
        /// comparing a particle law against a field law and calling the mismatch a tolerance.</para>
        ///
        /// <para>The knots are re-ordered defensively before use, so a mis-tuned config can never invert the
        /// ramp or divide by zero. Non-decreasing in <paramref name="t"/> for any ordering of the inputs.</para>
        /// </summary>
        public static float Knots(float t01, float whiteHold, float blueReach, float deepReach)
        {
            float t = Mathf.Clamp01(t01);
            float hold = Mathf.Clamp01(whiteHold);
            float blue = Mathf.Clamp(blueReach, hold + 1e-4f, 1f);
            float deep = Mathf.Clamp(deepReach, blue + 1e-4f, 1f + 1e-3f);

            if (t <= hold) return 0f;
            if (t < blue) return 0.5f * (t - hold) / (blue - hold);
            if (t < deep) return 0.5f + 0.5f * (t - blue) / (deep - blue);
            return 1f;
        }

        /// <summary>
        /// How OLD a patch of buffered wake foam is, from the advected buffer's FRESHNESS channel —
        /// <b>the half the water shader transcribes</b>, and the piece round 2 replaced.
        ///
        /// <para><b>What it replaced, and why measurement decided it.</b> #665 derived the buffer's age
        /// from its COVERAGE: <c>age = 1 − coverage/freshCover</c>, on the reasoning that a decaying
        /// buffer's surviving coverage is its freshness. The owner's eyeball (2026-08-27) found the
        /// band still solid white, and the arithmetic says why — coverage <b>saturates</b> (a dory at 3
        /// m/s pins a texel at 1.000 within 36 frames of deposit) and is then <b>thresholded and
        /// posterized</b> by the compose, so the value the proxy actually received could only ever be
        /// one of three: {0, 0.425, 0.85}. 72–81% of the visible band drew at age exactly 0 at every
        /// speed. Retuning the threshold cannot recover a gradient from three values — it only chooses
        /// which single flat colour the band is. <c>WakeFoamAgeingMeasurementTests</c> holds that
        /// measurement so the compression cannot come back unnoticed.</para>
        ///
        /// <para><b>What it is now.</b> The buffer carries a second channel that is a CLOCK: churn
        /// resets it (a max, never an add — see <c>FoamBuffer.Freshness</c>), and it decays on its own
        /// half-life. It cannot clamp, so it is monotone in time-since-churn by construction, and it
        /// reads 1.0 at the moment of churn for every hull at every speed — so the ramp means the same
        /// thing everywhere.</para>
        ///
        /// <para><paramref name="freshFloor"/> is the freshness at or above which water still reads as
        /// churning right now (age 0). At the shipped 1 that is the instant of churn alone and the
        /// white HOLD is left to <see cref="Knots"/>, where it is one knob rather than two ways of
        /// spelling the same thing. Floored off zero so a mis-tuned material divides by nothing.</para>
        /// </summary>
        public static float Age01FromFreshness(float freshness, float freshFloor)
        {
            float fresh = Mathf.Max(freshFloor, 1e-4f);
            return Mathf.Clamp01(1f - freshness / fresh);
        }

        /// <summary>
        /// The three-stop colour lookup: <paramref name="age01"/> 0 → <paramref name="foam"/>,
        /// 0.5 → <paramref name="shallow"/>, 1 → <paramref name="mid"/>. Alpha is not touched — the
        /// element's own life fade owns that, and this must never fight it.
        ///
        /// <para>Every returned value is a convex combination of the three anchors, which is the ADR 0015
        /// guarantee in its strongest form: the wake cannot leave the sea's palette even at a mis-tuned
        /// <paramref name="age01"/>.</para>
        /// </summary>
        public static Color Ramp3(float age01, Color foam, Color shallow, Color mid)
        {
            float t = Mathf.Clamp01(age01);
            return t <= 0.5f
                ? Color.Lerp(foam, shallow, t * 2f)
                : Color.Lerp(shallow, mid, (t - 0.5f) * 2f);
        }

        /// <summary>
        /// THE ENTRY POINT the renderers call: the RGB an element of this age should be drawn at.
        ///
        /// <para><paramref name="legacy"/> is the component's serialized colour — what shipped before this
        /// ramp existed. At <see cref="WakeAgeRamp.Strength"/> 0 it is returned BIT-EXACTLY (every channel
        /// equal, not "close"), which is the A/B contract: one knob to 0 and the sea draws exactly what it
        /// drew yesterday. At 1 the element is pure palette.</para>
        ///
        /// <para><see cref="WakeAgeRamp.ShadeJitter"/> adds a per-particle value nudge on top — two puffs of
        /// the same age are still not the same puff. It is a MULTIPLY on the ramp colour, so it scales
        /// within the palette rather than dragging chroma somewhere the palette does not go, and it is
        /// bounded: every channel stays inside <c>[minAnchor·(1−jitter), maxAnchor·(1+jitter)]</c>, the
        /// bound the guard-rail test pins.</para>
        ///
        /// <para>Alpha is passed through from <paramref name="legacy"/> untouched — the caller has already
        /// computed the life fade, and colour must never quietly restate it.</para>
        /// </summary>
        public static Color Shade(Color legacy, float life01, float seed01, in WakeAgeRamp ramp,
                                  in SeaPaletteState palette)
        {
            float strength = Mathf.Clamp01(ramp.Strength);
            if (strength <= 0f) return legacy;

            Color aged = Ramp3(Age01(life01, seed01, in ramp), palette.Foam, palette.Shallow, palette.Mid);

            float jitter = Mathf.Clamp01(ramp.ShadeJitter);
            if (jitter > 0f)
            {
                float k = 1f + (Decorrelate(seed01, 0xA7u) - 0.5f) * 2f * jitter;
                aged.r *= k; aged.g *= k; aged.b *= k;
            }

            return new Color(Mathf.Lerp(legacy.r, aged.r, strength),
                             Mathf.Lerp(legacy.g, aged.g, strength),
                             Mathf.Lerp(legacy.b, aged.b, strength),
                             legacy.a);
        }

        /// <summary>
        /// The age ramp applied as a <b>MULTIPLY</b>, for an element whose sprite carries its own shading
        /// in its RGB — the wake WAVE's crests, which are drawn with a lit crest above the waterline and a
        /// darker hollow below it.
        ///
        /// <para><b>Why this exists and <see cref="Shade"/> would not do.</b> <see cref="Shade"/> LERPS
        /// toward a single aged colour, which is right for a foam puff (one flat tone) and destroys a
        /// crest (every texel converges on the same value and the profile flattens into a painted line —
        /// the exact thing the wave sprite replaced). That is why #665 excluded the crests from the ramp
        /// altogether, and it is why the owner's next look found them still reading as <i>"a sprite baked
        /// statically … never manipulated"</i>: they were the one wake stream that never changed colour at
        /// all. A multiply is the operator that fits the case — it SCALES the sprite's own light and dark
        /// together, so the crest keeps every bit of its internal contrast while the whole thing walks
        /// down the sea's blues.</para>
        ///
        /// <para><paramref name="strength"/> 0 multiplies by pure white, so it returns
        /// <paramref name="legacy"/> BIT-EXACTLY (x·1 is exact in IEEE) — the A/B, on the same terms as
        /// every other knob in this file. Alpha is passed through untouched; the caller's life fade owns
        /// it.</para>
        /// </summary>
        public static Color ShadeMultiply(Color legacy, float life01, float seed01, float strength,
                                          in WakeAgeRamp ramp, in SeaPaletteState palette)
        {
            float k = Mathf.Clamp01(strength) * Mathf.Clamp01(ramp.Strength);
            if (k <= 0f) return legacy;

            Color aged = Ramp3(Age01(life01, seed01, in ramp), palette.Foam, palette.Shallow, palette.Mid);
            return new Color(legacy.r * Mathf.Lerp(1f, aged.r, k),
                             legacy.g * Mathf.Lerp(1f, aged.g, k),
                             legacy.b * Mathf.Lerp(1f, aged.b, k),
                             legacy.a);
        }

        /// <summary>
        /// The shade of water that is ALWAYS fresh — for the boat-attached churn sprites (the transom plume,
        /// the bow spray sheet), which are not particles and have no age of their own: they are the moment of
        /// contact, continuously.
        ///
        /// <para>It is the ramp's zero end, the sea's own FOAM anchor. The value it replaces is a component
        /// constant that happened to be white; the value it installs is the white THIS sea is using, so a
        /// preset swap or a mood turn carries the churn with it (ADR 0015) instead of leaving one hard-coded
        /// near-white sitting in a re-graded sea.</para>
        ///
        /// <para><b>What this deliberately does NOT do:</b> give those sprites an age gradient along their
        /// length. A plume sprite is one authored image with one tint, and the age of the water it depicts
        /// varies from its apex to its tail — a gradient that lives in the ARTWORK, not in a tint. The aged
        /// read astern is the deposited trail's job, and it now does it. Scatter and jitter are not applied
        /// either: there is one plume, and a plume that flickered its own shade would be a defect, not
        /// variety.</para>
        /// </summary>
        public static Color ShadeFresh(Color legacy, in WakeAgeRamp ramp, in SeaPaletteState palette)
        {
            float strength = Mathf.Clamp01(ramp.Strength);
            if (strength <= 0f) return legacy;
            return new Color(Mathf.Lerp(legacy.r, palette.Foam.r, strength),
                             Mathf.Lerp(legacy.g, palette.Foam.g, strength),
                             Mathf.Lerp(legacy.b, palette.Foam.b, strength),
                             legacy.a);
        }
    }

    /// <summary>
    /// Every tunable of the age ramp, in one struct so the maths carries no magic numbers (rule 6). The
    /// owner edits an instance on <c>BoatWakeEmitter</c>; the shipped defaults are the look this lane was
    /// built to deliver, and <see cref="Strength"/> 0 is the revert.
    /// </summary>
    [System.Serializable]
    public struct WakeAgeRamp
    {
        [Tooltip("Master: 0 = the legacy solid colour, bit-exact (the A/B revert). 1 = pure sea palette.")]
        [Range(0f, 1f)] public float Strength;

        [Tooltip("Fraction of life the foam stays WHITE - the churn itself. Small: white is the moment of contact, not the trail.")]
        [Range(0f, 1f)] public float WhiteHold;

        [Tooltip("Life fraction at which the foam has reached the sea's SHALLOW blue.")]
        [Range(0f, 1f)] public float BlueReach;

        [Tooltip("Life fraction at which the foam has reached the sea's MID blue and stops descending.")]
        [Range(0f, 1f)] public float DeepReach;

        [Tooltip("+/- per-particle offset along the ramp, so one churn holds many ages at once instead of reading as a single sheet. 0 = every puff the same age.")]
        [Range(0f, 0.5f)] public float AgeScatter;

        [Tooltip("+/- per-particle value nudge on the ramp colour, so two puffs of the same age still differ.")]
        [Range(0f, 0.5f)] public float ShadeJitter;

        /// <summary>
        /// The shipped feel. <see cref="WhiteHold"/> 0.12 keeps white to the churn's first eighth of life;
        /// the shallow blue lands at 0.45 and the mid blue at 0.85, so most of a trail's visible length is
        /// spent walking through the sea's blues rather than sitting on white. Scatter 0.22 is roughly a
        /// half-leg of the ramp: enough that neighbouring puffs are visibly different ages, not so much that
        /// fresh churn stops reading as fresh.
        /// </summary>
        public static WakeAgeRamp Default => new WakeAgeRamp
        {
            Strength    = 1f,
            WhiteHold   = 0.12f,
            BlueReach   = 0.45f,
            DeepReach   = 0.85f,
            AgeScatter  = 0.22f,
            ShadeJitter = 0.10f,
        };

        /// <summary>The OFF side of the A/B: the legacy solid colour, bit-exact.</summary>
        public static WakeAgeRamp Off => new WakeAgeRamp
        {
            Strength    = 0f,
            WhiteHold   = 0.12f,
            BlueReach   = 0.45f,
            DeepReach   = 0.85f,
            AgeScatter  = 0.22f,
            ShadeJitter = 0.10f,
        };
    }
}
