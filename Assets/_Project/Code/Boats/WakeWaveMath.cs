using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The PURE, engine-light math of the WAKE WAVE — the owner's 2026-08-06 ask, verbatim: <i>"a real
    /// WAKE WAVE should replace the old wake sprite"</i>, meaning displaced water responding to the
    /// hull rather than a decal hung off the stern.
    ///
    /// <para><b>What the old wake sprite was.</b> One authored PNG of a whole Kelvin V
    /// (<c>WakeSpriteLibrary</c>'s graded plume), pinned apex-at-the-transom and swung with the boat.
    /// Everything true of a decal was true of it: it could not bend (so a hard turn had to FADE it —
    /// <see cref="WakeTrailMath.TurnFade01"/> exists only to hide that), it could not persist where it
    /// was laid, it could not ride a swell, and it was made of foam-white paint at a place where the
    /// sea is not white but LIFTED.</para>
    ///
    /// <para><b>What replaces it.</b> The wave is deposited, exactly like the rest of the trail
    /// (<see cref="WakeTrailMath"/>): each deposit lays crest segments in WORLD space — two along the
    /// emergent V arms, one across the transom (the stern roll) — each carrying an AMPLITUDE in
    /// metres, born from the hull's own size/weight/speed grade. The amplitude then does two things
    /// nothing painted can:
    /// <list type="number">
    /// <item>it LIFTS the drawn crest up-screen (<see cref="ScreenLiftMeters"/>), through the same
    /// exaggeration the displaced sea lifts its own swell by — so hull wave and sea wave are the same
    /// size of thing, and retuning one never silently leaves the other behind;</item>
    /// <item>it SIZES the drawn crest (<see cref="CrestHeightMeters"/>), so a bigger, faster hull
    /// throws a taller wave rather than a brighter decal.</item>
    /// </list>
    /// The wave then COLLAPSES over its life (<see cref="AmplitudeAt"/>) — a wake wave far astern is
    /// a ripple, not a faded V — and, because it was laid in the world, it curves through turns with
    /// nothing to fade.</para>
    ///
    /// <para><b>⚠️ What this deliberately does NOT do: move vertices.</b> The drawn sea is a real
    /// displaced mesh (ADR 0023), and the hull's watertight clamp bounds boarding water by evaluating
    /// the published wave field (<c>DisplacedWaterMath.WatertightZHeaveMeters</c>). Adding a wake term
    /// to that surface without adding it to the clamp's sampler would put lift on screen that the
    /// clamp cannot see — precisely the <c>_OceanSwellScale</c> defect class this repo has already
    /// paid for once ("a visual knob became geometry"). The wake wave therefore displaces the wake's
    /// OWN drawn water and leaves the sea mesh — and the clamp that reads it — untouched. Closing that
    /// gap is a hull-clamp change and belongs with it.</para>
    ///
    /// <para><b>Determinism &amp; purity (rule 5).</b> Every function here is static and
    /// side-effect-free; no time, no <see cref="Random"/>. <b>No magic numbers (rule 6):</b> every
    /// tunable arrives through <see cref="WakeWaveConfig"/>.</para>
    /// </summary>
    public static class WakeWaveMath
    {
        /// <summary>
        /// The wave's speed-onset ramp, 0..1: nothing at rest (a boat lying still displaces no water),
        /// rising to 1 once she is clearly making way. Monotonic non-decreasing in speed. Pure.
        /// </summary>
        public static float SpeedOnset01(float speed, in WakeWaveConfig c)
        {
            float range = Mathf.Max(1e-3f, c.SpeedOnsetRange);
            return Mathf.Clamp01((speed - c.SpeedOnset) / range);
        }

        /// <summary>
        /// The crest AMPLITUDE (metres) a deposit is born with: the hull grade's amplitude range
        /// (<see cref="WakeGrading.Magnitude01"/> blends size + weight + speed) gated by the speed
        /// onset, so a rowed dory raises a few centimetres and a heavy hull driven hard raises a real
        /// wave. Never negative. Pure.
        /// </summary>
        public static float BirthAmplitudeMeters(float magnitude01, float speed, in WakeWaveConfig c)
        {
            float graded = Mathf.Lerp(c.AmplitudeAtMagnitude0, c.AmplitudeAtMagnitude1,
                                      Mathf.Clamp01(magnitude01));
            return Mathf.Max(0f, graded) * SpeedOnset01(speed, in c);
        }

        /// <summary>
        /// The amplitude left at a moment in a crest's life, <paramref name="life01"/> 0 → 1.
        /// It reaches EXACTLY 0 at the end of life: a wake wave spreads and collapses, and a crest
        /// that was still standing when its sprite vanished would pop. The exponent shapes how long
        /// it holds its height before letting go (&gt;1 = holds, then collapses late). Pure.
        /// </summary>
        public static float AmplitudeAt(float birthAmplitudeMeters, float life01, in WakeWaveConfig c)
        {
            float remaining = 1f - Mathf.Clamp01(life01);
            float shaped = Mathf.Pow(remaining, Mathf.Max(0.05f, c.CollapsePower));
            return Mathf.Max(0f, birthAmplitudeMeters) * shaped;
        }

        /// <summary>
        /// How far UP-SCREEN (world units) a crest of this amplitude draws.
        ///
        /// <para><b>The exaggeration is borrowed, never re-declared.</b> The displaced sea publishes
        /// ONE exaggeration through the Core <c>DisplacedSea</c> seam and every consumer — the drawn
        /// surface, the hull ride, the wake's own swell ride — reads it live. A wake crest is water on
        /// that same sea, so it is lifted by the same factor: retune the sea and the hull's wave
        /// follows, which is the whole point of the shared seam (and the overlay-pose lesson: a cached
        /// copy of a shared scale is how a 1.6 m gap once opened in this very wake). An inactive
        /// displaced sea publishes nothing, and the caller passes 1 — the flat plane draws the wave at
        /// its honest metres. Pure.</para>
        /// </summary>
        public static float ScreenLiftMeters(float amplitudeMeters, float exaggeration)
            => Mathf.Max(0f, amplitudeMeters) * Mathf.Max(0f, exaggeration);

        /// <summary>
        /// The DRAWN FOOTPRINT (metres, across the sprite's short axis) of a crest at this amplitude —
        /// the lit crest and the hollow behind it together, measured ACROSS the direction the wave
        /// travels. A bigger wave is a broader one, so this is proportional to amplitude, with a floor
        /// so a nearly-collapsed crest thins to a ripple instead of vanishing through a zero-height
        /// quad (which would flicker as the scale crossed a texel). Pure.
        /// </summary>
        public static float CrestHeightMeters(float amplitudeMeters, in WakeWaveConfig c)
        {
            float h = Mathf.Max(0f, amplitudeMeters) * Mathf.Max(0f, c.CrestHeightPerAmplitude);
            return Mathf.Max(Mathf.Max(0f, c.MinCrestHeightMeters), h);
        }

        /// <summary>
        /// The render rotation (deg, sprite long axis = +X) of an ARM crest, chosen so the sprite's
        /// CROSS axis points OUTWARD from the track — the direction that arm's wave actually travels.
        ///
        /// <para><b>Why the cross axis has a required direction at all.</b> The crest sprite is not
        /// symmetric: it carries a lit crest on one side of its waterline and a darker hollow on the
        /// other, which is a PLAN-view read of a wave exactly as the sea shader's own swell bands are —
        /// light and shade laid across the direction of travel. Point that the wrong way and the arm
        /// reads as a wave rolling INTO the boat's track. A crest ribbon is symmetric along its length,
        /// so turning it end-for-end costs nothing except the side its shading falls on, which is the
        /// whole correction. Pure.</para>
        /// </summary>
        public static float ArmCrestOrientDeg(Vector2 trackDir, int side, float kelvinHalfAngleDeg)
        {
            Vector2 dir = WakeTrailMath.ArmDir(trackDir, side, kelvinHalfAngleDeg);
            Vector2 outward = WakeTrailMath.Lateral(trackDir, side);
            // The sprite's +Y at this orientation is the ribbon's own left-perpendicular.
            if (Vector2.Dot(WakeTrailMath.Lateral(dir, +1), outward) < 0f) dir = -dir;
            if (dir.sqrMagnitude <= 1e-8f) return 0f;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The render rotation (deg, sprite long axis = +X) of a crest laid ACROSS the track — the
        /// STERN ROLL, the raised water the transom drags behind it. It is the one piece of the wake
        /// the two diverging arms cannot express, and the piece the retired plume decal was really
        /// drawing.
        ///
        /// <para>The ribbon runs along <c>Lateral(trackDir, +1)</c>, whose OWN left-perpendicular is
        /// exactly <c>−trackDir</c> — dead astern, which is where the stern roll travels. So the
        /// crest/hollow shading falls the right way round by construction here, with no flip to
        /// choose. Degenerate track directions read 0 rather than NaN. Pure.</para>
        /// </summary>
        public static float TransomOrientDeg(Vector2 trackDir)
        {
            Vector2 lateral = WakeTrailMath.Lateral(trackDir, +1);
            if (lateral.sqrMagnitude <= 1e-8f) return 0f;
            return Mathf.Atan2(lateral.y, lateral.x) * Mathf.Rad2Deg;
        }
    }

    /// <summary>
    /// Every tunable of the WAKE WAVE in one serialized struct (rule 6; <see cref="BoatWakeEmitter"/>
    /// serializes an owner-editable instance). Defaults raise a clearly readable wave behind the
    /// greybox fleet without flooding the pools: the dory's rowed top speed only reaches the bottom of
    /// the onset ramp, so she pushes a low roll, and the faster hulls earn the standing crest.
    /// </summary>
    [System.Serializable]
    public struct WakeWaveConfig
    {
        [Header("Master switch")]
        [Tooltip("Draw the deposited WAKE WAVE (crest + trough, lifted by its own amplitude). Off = " +
                 "the arms fall back to the flat white crest LINES they used to be — the A/B.")]
        public bool Enabled;

        [Header("Amplitude (metres of displaced water, graded by the hull)")]
        [Tooltip("Crest amplitude (m) at wake magnitude 0 — the smallest, slowest hull's roll.")]
        public float AmplitudeAtMagnitude0;
        [Tooltip("Crest amplitude (m) at wake magnitude 1 — a big hull driven hard.")]
        public float AmplitudeAtMagnitude1;
        [Tooltip("Boat speed (m/s) at which the wave BEGINS to stand. Below it the hull displaces " +
                 "nothing worth drawing.")]
        public float SpeedOnset;
        [Tooltip("Speed range (m/s) over which the wave ramps from just-standing to full amplitude.")]
        public float SpeedOnsetRange;
        [Tooltip("How the crest collapses over its life. 1 = a straight fall to nothing; ABOVE 1 holds " +
                 "its height near the boat and lets go late, which is how a wake actually behaves.")]
        public float CollapsePower;

        [Header("How the wave DRAWS")]
        [Tooltip("Drawn crest height (m across the sprite) per metre of amplitude — the crest and the " +
                 "hollow under it together. Above 1 because a wave reads taller than it lifts.")]
        public float CrestHeightPerAmplitude;
        [Tooltip("Floor on the drawn crest height (m) so a nearly-collapsed crest thins to a ripple " +
                 "rather than flickering through a zero-height quad.")]
        public float MinCrestHeightMeters;
        [Tooltip("Master opacity of the drawn wave (0..1). The sprite already carries its own crest/" +
                 "trough profile; this dims the whole thing so the wave stays water, not paint.")]
        public float Opacity;
        [Tooltip("How much the crest's height wanders ALONG its length (0 = a ruled bar). Small — this " +
                 "is what stops long overlapping crests reading as drawn lines.")]
        public float CrestUndulation;

        [Header("The STERN ROLL (what the retired plume decal was really drawing)")]
        [Tooltip("Lay a crest ACROSS the track at each deposit — the raised water the transom drags. " +
                 "Off = arms only.")]
        public bool TransomCrest;
        [Tooltip("Stern-roll amplitude as a multiple of the arm crests'. Above 1: it is the biggest " +
                 "wave the hull makes.")]
        public float TransomAmplitudeScale;
        [Tooltip("Stern-roll lifetime as a fraction of the arm crests'. SHORT on purpose — the roll " +
                 "belongs to the transom and collapses just astern of it, handing the read to the arms.")]
        public float TransomLifetimeScale;
        [Tooltip("Stern-roll drawn length as a fraction of the churn band's full width. It spans the " +
                 "transom, not the whole trail.")]
        public float TransomLengthFraction;

        /// <summary>The greybox default wave — readable, pool-safe, dory-gentle. The owner tunes here.</summary>
        public static WakeWaveConfig Default => new WakeWaveConfig
        {
            Enabled                 = true,

            AmplitudeAtMagnitude0   = 0.06f,   // m — the rowed dory's low roll
            AmplitudeAtMagnitude1   = 0.34f,   // m — a heavy hull driven hard
            SpeedOnset              = 0.5f,    // just above the foam's 0.4 idle threshold
            SpeedOnsetRange         = 2.2f,
            CollapsePower           = 1.6f,    // holds near the boat, lets go late

            CrestHeightPerAmplitude = 2.4f,    // a wave reads taller than it lifts
            MinCrestHeightMeters    = 0.10f,
            Opacity                 = 0.72f,   // water, not paint
            CrestUndulation         = 0.22f,

            TransomCrest            = true,
            TransomAmplitudeScale   = 1.45f,   // the stern roll is the biggest wave she makes
            TransomLifetimeScale    = 0.5f,    // and the shortest-lived
            TransomLengthFraction   = 2.2f,    // spans the transom (x the churn HALF-width)
        };
    }
}
