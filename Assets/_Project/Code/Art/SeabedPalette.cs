using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The BOTTOM's albedo as a function of authored elevation — the seabed a region has before
    /// anyone paints one (ADR 0027 #7 activation). Feeds <c>SeabedBakeTool</c>'s terrain source and
    /// therefore <c>_SeabedTex</c>, which the water shader then absorbs per channel.
    ///
    /// <para><b>Why this is not <c>World.TerrainHeightPalette</c>.</b> That palette is the editor's
    /// hypsometric DESIGNER OVERLAY: its underwater stops are navy and blue, because it is showing
    /// you how deep something is. Feed it to <c>_SeabedTex</c> and you paint the water's own colour
    /// onto the bottom — the blue doubles up, absorption acts on a blue bottom, and the whole point
    /// of the feature (a warm bottom losing its red with depth) never happens. A seabed is
    /// sand, silt and rock; the BLUE is the water's job, and the water already does it. Two
    /// different quantities, deliberately two different ramps.</para>
    ///
    /// <para><b>What the stops mean.</b> Bright dry sand at and above the waterline, damp sand just
    /// under it, silt and weed-darkened bottom through the mid depths, and a dark cold floor in the
    /// deep. Nothing here is saturated: a bake is an ALBEDO the shader tints and absorbs, so a
    /// vividly-coloured bottom would fight the sea rather than sit under it.</para>
    ///
    /// <para><b>These stops are a bake-time art default, not a game balance value.</b> They colour a
    /// texture the owner is free to repaint, replace with a painted tilemap bake, or re-bake with a
    /// different ramp — exactly the status <c>TerrainHeightPalette</c>'s own comment claims for
    /// itself (rule 6 is about values the game reads at runtime; this one is consumed once, in the
    /// editor, and committed as pixels).</para>
    ///
    /// <para>Pure and headless-testable. Nothing here runs at runtime, nothing is saved.</para>
    /// </summary>
    public static class SeabedPalette
    {
        // Elevation (m above chart datum) → bottom albedo, low to high. The span brackets the same
        // −4 .. +6 the height maps use.
        private static readonly float[] StopElev =
        {
            -4.0f,   // the deep floor: cold, dark, almost lightless
            -2.0f,   // mid water: silt over rock
            -0.8f,   // the weed line
            -0.2f,   // damp sand, just under the mean waterline
             0.6f,   // the drying flat
             2.0f,   // dry sand / shingle above the tide
        };

        private static readonly Color[] StopColor =
        {
            new Color(0.16f, 0.16f, 0.15f),   // near-black silt
            new Color(0.28f, 0.26f, 0.22f),   // dark silty rock
            new Color(0.36f, 0.35f, 0.25f),   // weed-darkened bottom
            new Color(0.55f, 0.49f, 0.36f),   // damp sand
            new Color(0.72f, 0.65f, 0.49f),   // drying sand
            new Color(0.82f, 0.75f, 0.60f),   // dry sand
        };

        /// <summary>The lowest elevation the ramp describes (below it the deep floor colour holds).</summary>
        public static float MinElevation => StopElev[0];

        /// <summary>The highest elevation the ramp describes (above it the dry sand colour holds).</summary>
        public static float MaxElevation => StopElev[StopElev.Length - 1];

        /// <summary>
        /// The bottom's albedo at an authored elevation, linearly interpolated between stops and
        /// CLAMPED outside the ramp (a cliff at +8 m is still dry ground, not an extrapolated
        /// colour). Monotone in brightness: deeper is always darker, which is what makes the
        /// absorbed result read as depth rather than as noise.
        /// </summary>
        public static Color Albedo(float elevation)
        {
            if (elevation <= StopElev[0]) return StopColor[0];
            int last = StopElev.Length - 1;
            if (elevation >= StopElev[last]) return StopColor[last];

            for (int i = 1; i <= last; i++)
            {
                if (elevation > StopElev[i]) continue;
                float span = StopElev[i] - StopElev[i - 1];
                float t = span > 1e-6f ? (elevation - StopElev[i - 1]) / span : 0f;
                return Color.Lerp(StopColor[i - 1], StopColor[i], t);
            }
            return StopColor[last];
        }

        /// <summary>
        /// The bake's per-texel answer: the bottom's albedo with <b>coverage in alpha</b> — 1
        /// wherever the ramp describes a bottom at all. Elevation is always defined (the height map
        /// covers the whole rect), so a terrain-sourced bake has full coverage by construction, and
        /// what confines the visible effect is <b>absorption</b>: past a few metres the transmission
        /// is ~0 and the bottom simply is not seen. That is the physically right way for it to stop,
        /// and it is why this source does not try to guess a coverage mask.
        /// </summary>
        public static Color Sample(float elevation)
        {
            Color c = Albedo(elevation);
            c.a = 1f;
            return c;
        }
    }
}
