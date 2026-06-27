using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The PURE elevation→colour ramp for the Terrain Paint Tool's <b>edit-mode height overlay</b> (ADR 0014)
    /// — a plain static helper (no <see cref="MonoBehaviour"/>, no <see cref="ScriptableObject"/>, no editor
    /// dependency) that maps a metres-above-datum elevation to a readable "hypsometric tint" the owner can SEE
    /// while shaping the coast: deep blue → cyan shallows → sand → green → brown/rock. It also classifies an
    /// elevation against the preview waterline (submerged vs dry) so the legend / overlay can mark where the
    /// coast is at the current tide.
    ///
    /// <para><b>What it is NOT.</b> A DESIGNER AID, not game art — nothing here renders in Play or a build. The
    /// ramp is a separate, deliberately-loud false-colour scheme; the SHIPPED look is the water shader + the
    /// painted tiles, never this. Keeping the ramp here (in World, as a pure function) means the
    /// editor-only overlay drawing can be thin (just paint the cells) and the colour LOGIC is unit-tested
    /// headless — the determinism guard (rule 5): a pure function of elevation, no RNG, nothing saved.</para>
    ///
    /// <para><b>Why World, not the editor tool.</b> So the narrow <c>Tests.World.EditMode</c> asmdef can
    /// exercise the ramp without pulling in the editor assembly, and so any future runtime debug view (gated
    /// off by default) could reuse it. It depends only on <see cref="Color"/> / <see cref="Mathf"/>.</para>
    /// </summary>
    public static class TerrainHeightPalette
    {
        // Hypsometric stops (elevation in metres above datum → colour), low to high. Chosen to bracket the
        // canon St Peters span (deep -4 .. land +6) with clear bands the eye reads at a glance:
        //   deep navy (abyss) → blue → cyan (shallows) → pale sand (the waterline band) → green (low land)
        //   → olive → brown (cliff/rock). The exact stops are a designer-aid choice, not a balance tunable —
        //   they only colour the editor overlay, so they live as code constants (rule 6 is about GAME values).
        private static readonly float[] StopElev =
        {
            -4f,   // deep floor
            -2f,   // mid water
            -0.5f, // shallows
             0.2f, // wet sand / just below the mean waterline
             0.8f, // beach / drying flat
             1.6f, // sandbar crest
             3.0f, // low land
             6.0f, // island / plateau
             8.0f, // cliff / rock
        };

        private static readonly Color[] StopColor =
        {
            new Color(0.04f, 0.07f, 0.22f),  // deep navy
            new Color(0.07f, 0.20f, 0.45f),  // blue
            new Color(0.20f, 0.55f, 0.70f),  // cyan shallows
            new Color(0.55f, 0.72f, 0.74f),  // teal-grey waterline
            new Color(0.85f, 0.78f, 0.55f),  // pale sand
            new Color(0.80f, 0.70f, 0.42f),  // tan / sandbar
            new Color(0.45f, 0.62f, 0.30f),  // green low land
            new Color(0.32f, 0.46f, 0.22f),  // darker green plateau
            new Color(0.45f, 0.36f, 0.28f),  // brown cliff/rock
        };

        /// <summary>
        /// The false-colour for an elevation (m above datum) on the hypsometric ramp — linearly interpolated
        /// between the nearest stops, clamped flat below the lowest / above the highest stop. Deterministic and
        /// allocation-free. The overlay tints each height-field cell with this so the owner SEES the shape.
        /// </summary>
        public static Color ColorForElevation(float elevation)
        {
            if (elevation <= StopElev[0]) return StopColor[0];
            int last = StopElev.Length - 1;
            if (elevation >= StopElev[last]) return StopColor[last];

            for (int i = 0; i < last; i++)
            {
                float lo = StopElev[i], hi = StopElev[i + 1];
                if (elevation >= lo && elevation <= hi)
                {
                    float span = Mathf.Max(hi - lo, 1e-4f);
                    float t = (elevation - lo) / span;
                    return Color.Lerp(StopColor[i], StopColor[i + 1], t);
                }
            }
            return StopColor[last];   // unreachable given the bracketing above; defensive
        }

        /// <summary>
        /// True when an elevation is SUBMERGED at a given preview water level (elevation strictly below the
        /// waterline). The overlay/legend uses this to show what's underwater at the current preview tide —
        /// the same <c>depth = waterLevel − elevation</c> the sim/shader read, so the false-colour view and the
        /// real coast agree (P1). Pure; presentation only.
        /// </summary>
        public static bool IsSubmerged(float elevation, float waterLevel) => elevation < waterLevel;

        /// <summary>
        /// The legend entries (elevation, colour) the overlay draws so the owner can read "which colour ≈ which
        /// elevation". Returns the ramp's own stops in order (low → high). Stable; pure.
        /// </summary>
        public static (float elevation, Color color)[] LegendStops()
        {
            var stops = new (float, Color)[StopElev.Length];
            for (int i = 0; i < StopElev.Length; i++) stops[i] = (StopElev[i], StopColor[i]);
            return stops;
        }

        /// <summary>The lowest elevation the ramp's stops cover (m above datum).</summary>
        public static float MinStopElevation => StopElev[0];
        /// <summary>The highest elevation the ramp's stops cover (m above datum).</summary>
        public static float MaxStopElevation => StopElev[StopElev.Length - 1];
    }
}
