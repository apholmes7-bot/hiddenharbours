using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The live SEA PALETTE the water is drawing itself with this tick — the four art-directed anchor
    /// colours of the ADR 0015 palette guard-rail (<c>_PaletteDeep</c> / <c>_PaletteMid</c> /
    /// <c>_PaletteShallow</c> / <c>_PaletteFoam</c>), as the ACTIVE surface has them after its mood ease.
    ///
    /// <para><b>Why this exists.</b> The wake's foam is not a decal painted on the sea, it is water — so
    /// when it ages it must walk down <b>the water's own ramp</b>, never a set of hexes invented on a
    /// particle component (ADR 0015's whole point: the sea's output stays inside an art-directed palette,
    /// and a preset swap moves the palette). The foam particles are drawn by Boats and the palette is
    /// owned by Art, so the two meet HERE (CLAUDE.md rule 4) exactly as <see cref="DisplacedSea"/> carries
    /// the shared exaggeration. Since W4-1 (design 3A) it also carries the WATER ITSELF
    /// (<see cref="SeaWaterBody"/>) and the two wake dials, so an aged element ends as the water at its
    /// own depth rather than on an anchor.</para>
    ///
    /// <para><b>Publish the instance, never a copy.</b> The anchors are MOOD-EASED — <c>WaterSurface</c>
    /// blends them between preset materials as the weather turns, every frame. A consumer that read the
    /// material itself (or cached a copy at Awake) would disagree with the drawn sea whenever the mood
    /// moved, which is the stale-twin bug this repo has paid for more than once. The surface publishes its
    /// OWN eased values each push; consumers read THIS every tick and cache nothing.</para>
    ///
    /// <para><b>Presentation only (rule 5).</b> Colours feed no simulation, enter no save, and are
    /// recomputed by their publisher from the live weather each tick. Absent state is the OFF contract:
    /// a consumer with no published palette falls back to its own serialized colour and draws exactly
    /// what it drew before this seam existed.</para>
    /// </summary>
    public readonly struct SeaPaletteState
    {
        /// <summary>The DEEP-water anchor (<c>_PaletteDeep</c>) — the darkest blue in the ramp.</summary>
        public readonly Color Deep;

        /// <summary>The MID-water anchor (<c>_PaletteMid</c>).</summary>
        public readonly Color Mid;

        /// <summary>The SHALLOW-water anchor (<c>_PaletteShallow</c>).</summary>
        public readonly Color Shallow;

        /// <summary>The FOAM / highlight anchor (<c>_PaletteFoam</c>) — the white a churn is born at.</summary>
        public readonly Color Foam;

        /// <summary>The wake walk's mid stop, as a multiple of its water's brightness
        /// (<c>_WakeBodyLift</c>, mood-eased; W4-1, design 3A).</summary>
        public readonly float BodyLift;

        /// <summary>How far that mid stop is whitened toward <see cref="Foam"/>
        /// (<c>_WakeBodyWhiten</c>, mood-eased; W4-1, design 3A).</summary>
        public readonly float BodyWhiten;

        /// <summary>The water itself: what the sea draws at a depth before any light layer — the place an
        /// aged wake ends (W4-1, design 3A). <c>default</c> when the surface could not read it.</summary>
        public readonly SeaWaterBody Water;

        /// <summary>The four anchors alone: no lift (×1, unwhitened) and no water body, so
        /// <see cref="BodyAt"/> answers <see cref="Mid"/> — the walk's end before 3A.</summary>
        public SeaPaletteState(Color deep, Color mid, Color shallow, Color foam)
            : this(deep, mid, shallow, foam, 1f, 0f, default)
        {
        }

        public SeaPaletteState(Color deep, Color mid, Color shallow, Color foam,
                               float bodyLift, float bodyWhiten, in SeaWaterBody water)
        {
            Deep = deep;
            Mid = mid;
            Shallow = shallow;
            Foam = foam;
            BodyLift = bodyLift;
            BodyWhiten = bodyWhiten;
            Water = water;
        }

        /// <summary>The colour of the water at <paramref name="depth"/> metres (gamma, like the anchors):
        /// <see cref="SeaWaterBody.ColorAt"/> when the surface published its water body, else
        /// <see cref="Mid"/>, the anchor the walk ended on before W4-1.</summary>
        public Color BodyAt(float depth) => Water.IsKnown ? Water.ColorAt(depth) : Mid;
    }

    /// <summary>
    /// THE WATER ITSELF at a depth (W4-1, design 3A, owner ruling 2026-10-10): the colour the water shader
    /// settles a pixel to before any light layer, which is where an aged wake ends. Twin of
    /// <c>HiddenHarboursWater.shader</c>'s frag body block — the depth fraction <c>dt</c>, its posterize,
    /// the <c>_DepthRamp</c> sample (point, clamped, the row at v = 0.5) or the
    /// <c>_ShallowColor</c>/<c>_DeepColor</c> lerp when the material has no ramp, then the deep-blue pull —
    /// computed in LINEAR colour as the shader does and handed back in gamma like every palette anchor.
    ///
    /// <para><b>Not twinned: the seabed.</b> The shader composites the bed into the same body
    /// (<c>_USE_SEABEDTEX</c>, <c>_Turbidity</c> &gt; 0); the CPU has no bed texture, so where a bed shows
    /// through the band ends on water-plus-bed and a sprite on the water alone.</para>
    ///
    /// <para>Presentation only (rule 5): a pure function of the published material values and a depth
    /// recomputed from the tide each tick. Nothing here is saved.</para>
    /// </summary>
    public readonly struct SeaWaterBody
    {
        // The ramp row the shader samples, shallow end at [0]. Read once by the publisher when the ramp
        // texture changes, never per tick; null = the material draws the two-colour lerp instead.
        private readonly Color32[] _ramp;

        /// <summary><c>_ShallowColor</c> / <c>_DeepColor</c>: the body when there is no ramp.</summary>
        public readonly Color ShallowColor, DeepColor;

        /// <summary><c>_ShallowDepth</c> / <c>_DeepDepth</c> (m) and <c>_DepthBands</c> (0 = smooth).</summary>
        public readonly float ShallowDepth, DeepDepth, DepthBands;

        /// <summary><c>_DeepBlueColor</c>, <c>_DeepBlueStart</c>, <c>_DeepBlueStrength</c>: the navy pull.</summary>
        public readonly Color DeepBlueColor;
        public readonly float DeepBlueStart, DeepBlueStrength;

        /// <summary>False for <c>default</c>: nothing was published, so there is no water to end on.</summary>
        public readonly bool IsKnown;

        public SeaWaterBody(Color32[] ramp, Color shallowColor, Color deepColor,
                            float shallowDepth, float deepDepth, float depthBands,
                            Color deepBlueColor, float deepBlueStart, float deepBlueStrength)
        {
            _ramp = ramp;
            ShallowColor = shallowColor;
            DeepColor = deepColor;
            ShallowDepth = shallowDepth;
            DeepDepth = deepDepth;
            DepthBands = depthBands;
            DeepBlueColor = deepBlueColor;
            DeepBlueStart = deepBlueStart;
            DeepBlueStrength = deepBlueStrength;
            IsKnown = true;
        }

        /// <summary>Texels in the published ramp row (0 = the two-colour lerp).</summary>
        public int RampTexels => _ramp != null ? _ramp.Length : 0;

        /// <summary>The water's colour at <paramref name="depth"/> metres, in gamma, opaque. Open water
        /// (+∞, <c>BoatCrossing.DepthAt</c>'s answer off the terrain) is the deep end.</summary>
        public Color ColorAt(float depth)
        {
            float dt = Mathf.Clamp01((depth - ShallowDepth) / Mathf.Max(DeepDepth - ShallowDepth, 1e-3f));
            if (DepthBands >= 1f)
                dt = Mathf.Floor(dt * DepthBands + 0.5f) / DepthBands;

            Color col;
            int n = RampTexels;
            if (n > 0)
            {
                // Point + clamp: the texel whose span holds u = dt, the last one at u = 1. The int cast
                // of a NaN is out of range either way, so the clamp also keeps a bad depth inside the row.
                int i = (int)(dt * n);
                col = SeaPalette.ToLinear(_ramp[i < 0 ? 0 : (i > n - 1 ? n - 1 : i)]);
            }
            else
            {
                col = Color.Lerp(SeaPalette.ToLinear(ShallowColor), SeaPalette.ToLinear(DeepColor), dt);
            }

            if (DeepBlueStrength > 0.001f)
            {
                float deepT = SmoothStep(Mathf.Min(Mathf.Clamp01(DeepBlueStart), 0.99f), 1f, dt);
                col = Color.Lerp(col, SeaPalette.ToLinear(DeepBlueColor), deepT * Mathf.Clamp01(DeepBlueStrength));
            }

            Color body = SeaPalette.ToGamma(col);
            body.a = 1f;
            return body;
        }

        // HLSL's smoothstep, transcribed (Mathf.SmoothStep is a different curve: it interpolates between
        // two VALUES, it does not map x across two edges).
        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>
    /// The Core seam between the Art-side water surface (publisher) and its colour consumers — today the
    /// Boats-side wake foam, tomorrow any other water-riding visual that must age into the sea rather than
    /// into transparency. Mirrors <see cref="DisplacedSea"/> in shape and in ownership discipline: one
    /// publisher at a time, last-writer-wins, and only the current owner may clear.
    /// </summary>
    public static class SeaPalette
    {
        private static object s_Owner;
        private static SeaPaletteState s_State;

        /// <summary>True while an active water surface has published a palette.</summary>
        public static bool IsActive => s_Owner != null;

        /// <summary>The live palette; false (and <c>default</c>) when no surface has published one.</summary>
        public static bool TryGet(out SeaPaletteState state)
        {
            state = s_State;
            return s_Owner != null;
        }

        /// <summary>Publish the active surface's eased anchors (each uniform push — re-publishing is how a
        /// mood turn or a preset swap reaches the consumers).</summary>
        public static void Publish(object owner, in SeaPaletteState state)
        {
            if (owner == null) return;
            s_Owner = owner;
            s_State = state;
        }

        /// <summary>Clear the palette — only by its current owner, so a stale publisher going away cannot
        /// kill a newer sea's state. No palette ⇒ consumers draw their serialized fallback: the OFF
        /// contract.</summary>
        public static void Clear(object owner)
        {
            if (!ReferenceEquals(s_Owner, owner)) return;
            s_Owner = null;
            s_State = default;
        }

        /// <summary>The exact sRGB transfer, gamma → linear, per channel; alpha untouched. The project
        /// renders in linear, so the shader does its colour maths there; the CPU twins convert to match.
        /// Pure managed maths (not <c>Color.linear</c>), so it runs and answers the same everywhere.</summary>
        public static Color ToLinear(Color c)
            => new Color(ToLinear(c.r), ToLinear(c.g), ToLinear(c.b), c.a);

        /// <summary>The exact sRGB transfer, linear → gamma, per channel; alpha untouched.</summary>
        public static Color ToGamma(Color c)
            => new Color(ToGamma(c.r), ToGamma(c.g), ToGamma(c.b), c.a);

        private static float ToLinear(float v)
            => v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);

        private static float ToGamma(float v)
            => v <= 0.0031308f ? v * 12.92f : 1.055f * Mathf.Pow(v, 1f / 2.4f) - 0.055f;
    }
}
