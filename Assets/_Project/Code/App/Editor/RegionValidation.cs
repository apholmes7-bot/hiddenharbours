#if UNITY_EDITOR
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The PURE maths behind the <see cref="RegionValidatorWindow"/> (owner level-design toolkit,
    /// Phase 1) — small static predicates the window composes into its plain-English checklist, kept
    /// engine-light so EditMode tests can assert the tide-dryness logic headlessly (no scene).
    ///
    /// <para><b>One shared rule, never a divergent copy.</b> Every wet/dry question here defers to the
    /// Core exposure seam (<see cref="TidalExposure"/> over <see cref="ITidalTerrain.ElevationAt"/>) —
    /// the SAME single rule the on-foot walkability gate, the clam-baring, the boat grounding and the
    /// water shader read (ADR 0009). So the validator's "this floods at high tide" is by construction
    /// the sim's "this floods at high tide"; the tool cannot drift from the game (the tools-editor
    /// charter's don't-duplicate-the-rule guardrail).</para>
    ///
    /// <para><b>The tide swing is data, not a constant.</b> High/low water are derived from the
    /// authored <c>RegionDef</c> tide profile (mean ± amplitude — the spring envelope's extremes of
    /// <c>TideModel</c>, whose carrier and spring/neap envelope both peak at 1), never hard-coded
    /// (CLAUDE.md rule 6). <see cref="WidestSwing"/> exists because nothing re-points the live tide
    /// per region yet — the START scene's profile is what actually runs everywhere (the Greywick
    /// builder's documented caveat), so a region must be checked against the widest envelope that can
    /// reach it.</para>
    ///
    /// <para>READ-ONLY by nature: pure functions of authored values; nothing here (or in the window)
    /// mutates a scene or asset.</para>
    /// </summary>
    public static class RegionValidation
    {
        /// <summary>
        /// A region's tide envelope: the lowest and highest water level (metres above chart datum) the
        /// swing can reach. Normalised so <see cref="Low"/> &lt;= <see cref="High"/> whatever order the
        /// inputs arrive in.
        /// </summary>
        public readonly struct TideSwing
        {
            /// <summary>Lowest water of the swing (spring low), m above chart datum.</summary>
            public readonly float Low;
            /// <summary>Highest water of the swing (spring high), m above chart datum.</summary>
            public readonly float High;

            public TideSwing(float low, float high)
            {
                Low = Mathf.Min(low, high);
                High = Mathf.Max(low, high);
            }
        }

        /// <summary>
        /// The tide envelope of a region profile: mean ± |amplitude| — the extremes <c>TideModel</c>
        /// can reach (its sinusoidal carrier and its spring/neap envelope both peak at exactly 1, so
        /// spring high water is <c>mean + amplitude</c> and spring low is <c>mean − amplitude</c>).
        /// Amplitude is taken absolute so a negative authoring typo can't invert the swing.
        /// </summary>
        public static TideSwing SwingOf(float meanLevel, float amplitude)
        {
            float a = Mathf.Abs(amplitude);
            return new TideSwing(meanLevel - a, meanLevel + a);
        }

        /// <summary>
        /// The widest envelope of two swings — lowest low, highest high. Used to fold the START
        /// region's live tide into a region's own authored profile, because the environment service
        /// runs the start scene's profile everywhere until per-region re-pointing lands (the
        /// GreywickBuilder caveat): a Greywick authored at ±0.8 m still LIVES under St Peters' ±3.5 m,
        /// so its land must be checked against the wider swing.
        /// </summary>
        public static TideSwing WidestSwing(TideSwing a, TideSwing b)
            => new TideSwing(Mathf.Min(a.Low, b.Low), Mathf.Max(a.High, b.High));

        /// <summary>
        /// Is the ground at <paramref name="worldPos"/> dry (exposed) when the water surface sits at
        /// <paramref name="waterLevel"/>? Exactly <see cref="TidalExposure.IsExposed(float,float)"/>
        /// over the terrain's authored elevation — the sim's own rule, not a copy. A null terrain is
        /// treated as open water (never dry), mirroring the Core seam's safe default (ADR 0009); the
        /// window only asks this when a terrain exists.
        /// </summary>
        public static bool IsDryAt(ITidalTerrain terrain, Vector2 worldPos, float waterLevel)
        {
            if (terrain == null) return false;
            return TidalExposure.IsExposed(waterLevel, terrain.ElevationAt(worldPos));
        }

        /// <summary>
        /// Water depth (m) over <paramref name="worldPos"/> at <paramref name="waterLevel"/> —
        /// <see cref="TidalExposure.WaterDepth"/> over the terrain's elevation (≤ 0 means dry).
        /// Null terrain = open water = effectively bottomless (positive infinity).
        /// </summary>
        public static float DepthAt(ITidalTerrain terrain, Vector2 worldPos, float waterLevel)
            => terrain == null ? float.PositiveInfinity
                               : TidalExposure.WaterDepth(waterLevel, terrain.ElevationAt(worldPos));

        /// <summary>
        /// True when an authored elevation sits strictly INSIDE the tide swing — ground that both
        /// bares (as the tide falls below it) and floods (as it rises above it). This is what a
        /// tide-gated feature (the St Peters sandbar crest) must satisfy: at or above the high mark it
        /// NEVER floods (the gate never closes), at or below the low mark it NEVER bares (the walk
        /// path never opens).
        /// </summary>
        public static bool IsIntertidal(float elevation, TideSwing swing)
            => elevation > swing.Low && elevation < swing.High;

        /// <summary>
        /// Does the axis-aligned outer rectangle (centre + size) fully contain the inner one, with
        /// <paramref name="toleranceMeters"/> of slack per edge? Used for "the water plane covers the
        /// play area" and "the seabed bake rect covers the water plane". Sizes are taken absolute.
        /// </summary>
        public static bool RectCovers(Vector2 outerCenter, Vector2 outerSize,
                                      Vector2 innerCenter, Vector2 innerSize, float toleranceMeters)
        {
            Vector2 oHalf = new Vector2(Mathf.Abs(outerSize.x), Mathf.Abs(outerSize.y)) * 0.5f;
            Vector2 iHalf = new Vector2(Mathf.Abs(innerSize.x), Mathf.Abs(innerSize.y)) * 0.5f;
            Vector2 oMin = outerCenter - oHalf, oMax = outerCenter + oHalf;
            Vector2 iMin = innerCenter - iHalf, iMax = innerCenter + iHalf;
            return iMin.x >= oMin.x - toleranceMeters
                && iMin.y >= oMin.y - toleranceMeters
                && iMax.x <= oMax.x + toleranceMeters
                && iMax.y <= oMax.y + toleranceMeters;
        }

        /// <summary>
        /// Sample the terrain's elevation over a world rectangle on an n×n grid and report the min/max
        /// found — used to check the water shader's baked elevation range (<c>_HeightMin/_HeightMax</c>)
        /// actually brackets the authored seabed (a clamped bake draws the wrong depth/shoreline).
        /// Returns false (min/max = 0) when there is nothing to sample.
        /// </summary>
        public static bool SampleElevationRange(ITidalTerrain terrain, Vector2 center, Vector2 size,
                                                int samplesPerAxis, out float min, out float max)
        {
            min = 0f; max = 0f;
            if (terrain == null || samplesPerAxis < 2) return false;

            Vector2 half = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y)) * 0.5f;
            bool any = false;
            for (int iy = 0; iy < samplesPerAxis; iy++)
            for (int ix = 0; ix < samplesPerAxis; ix++)
            {
                float u = ix / (float)(samplesPerAxis - 1);
                float v = iy / (float)(samplesPerAxis - 1);
                var p = new Vector2(center.x - half.x + u * half.x * 2f,
                                    center.y - half.y + v * half.y * 2f);
                float e = terrain.ElevationAt(p);
                if (!any) { min = max = e; any = true; }
                else { min = Mathf.Min(min, e); max = Mathf.Max(max, e); }
            }
            return any;
        }
    }
}
#endif
