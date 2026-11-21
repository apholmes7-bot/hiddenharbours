using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The owner's dials for the sea's ONE foam sheet (the owner, 2026-11-19: "wake foam should be the
    /// same as any foam ... there needs to be consistency amongst the in game foam"). Every foam on the
    /// water (the wake, the shore's foam line, whitecaps, surf whitewater, storm streaks and convergence
    /// foam) keeps its own logic for WHERE and HOW MUCH foam there is, and draws it through the same lace,
    /// the same bands and the same colour-and-ageing walk, so they read as one substance in different
    /// amounts. These dials say how each source joins that sheet; 0 on a source's join dial is its older
    /// separate look, for an A/B.
    ///
    /// <para>The water shader carries the same values as its property defaults (a water with no config
    /// wired draws exactly these); <c>WaterSurface</c> pushes them every tick. The sheet's own lace,
    /// threshold and bands are the water material's <c>_WakeFoam*</c> dials and its blob size is the
    /// material's <c>_FoamBlobScale</c>: one dial for every foam.</para>
    ///
    /// <para>The oar dials are not shader properties: the oars lay their foam through
    /// <see cref="FoamSplashes"/> into the same buffer the wake fills, and the sheet ages it.</para>
    ///
    /// <para>Presentation only: no simulation state, nothing saved and no force reads them.</para>
    /// </summary>
    [Serializable]
    public struct FoamSheetSettings
    {
        [Header("Shore foam line")]
        [Tooltip("How fully the shore's foam line is drawn as the one foam sheet: 1 = the same lace, bands " +
                 "and ageing colour as the wake's foam, 0 = the older separate fringe (flat white, its own " +
                 "blobs).")]
        [Range(0f, 1f)] public float Shore;
        [Tooltip("How tightly the line's foam hugs the wet edge. Higher = a thinner line; lower = foam " +
                 "reaches further out across the band.")]
        [Range(0.5f, 8f)] public float ShoreLineSharpness;
        [Tooltip("How aged the seaward side of the line reads: 0 = all fresh white, 1 = the far side has " +
                 "aged into lace and the water's own colour.")]
        [Range(0f, 1f)] public float ShoreAgeAcross;
        [Tooltip("How aged the line reads on the backwash, against the run-up: 0 = no difference, 1 = the " +
                 "foam left as the water draws back is old, torn lace.")]
        [Range(0f, 1f)] public float ShoreBackwashAge;

        [Header("Whitecaps and surf")]
        [Tooltip("How fully the whitecaps are drawn as the one foam sheet: 1 = the same lace, bands and " +
                 "ageing tear as the wake's foam, 0 = the older soft-alpha caps.")]
        [Range(0f, 1f)] public float Caps;
        [Tooltip("How much of a cap's opacity is handed to the sheet. Lower = smaller, sparser caps (the " +
                 "sheet's threshold eats the faint ones), higher = bigger, denser caps.")]
        [Range(0.1f, 2f)] public float CapAmount;
        [Tooltip("How fully the surf's whitewater and lip are drawn as the one foam sheet: 1 = the same lace, " +
                 "bands and ageing tear as every other foam, 0 = the surf's own bands. The wash on the sand " +
                 "keeps its smooth sheet either way.")]
        [Range(0f, 1f)] public float Surf;
        [Tooltip("How much of the surf's whitewater is handed to the sheet. Lower = the faint edges of the " +
                 "whitewater drop out, higher = the whitewater spreads.")]
        [Range(0.1f, 2f)] public float SurfAmount;

        [Header("Storm streaks")]
        [Tooltip("How fully the storm's downwind streaks are drawn as the one foam sheet: 1 = the same lace, " +
                 "bands and ageing colour as every other foam, 0 = the older faint additive tint.")]
        [Range(0f, 1f)] public float Storm;
        [Tooltip("How much of the streaks' lane amount is handed to the sheet. Higher = fuller, longer " +
                 "streaks; lower = only their brightest veins.")]
        [Range(0.1f, 6f)] public float StormAmount;
        [Tooltip("How old the streaks' foam reads: they are foam strung out by the wind, so older = more " +
                 "broken into lace and closer to the water's own colour.")]
        [Range(0f, 1f)] public float StormAge;

        [Header("Convergence foam")]
        [Tooltip("How fully the foam where wave trains pinch together is drawn as the one foam sheet: 1 = it " +
                 "is the same foam as the caps (lace, bands, ageing colour) at full body, 0 = the older milky " +
                 "texture borrowed from the caps.")]
        [Range(0f, 1f)] public float Convergence;
        [Tooltip("How old convergence foam reads: foam gathered where trains meet is collected, not freshly " +
                 "broken, so older = more lace and closer to the water's own colour.")]
        [Range(0f, 1f)] public float ConvergenceAge;

        [Header("The wash")]
        [Tooltip("How fully the run-up's wash (the sheet of water the surf sends up the sand and back) is " +
                 "drawn as the one foam sheet: 1 = the same lace, tear, bands and ageing colour as every other " +
                 "foam, 0 = the older smooth sheet.")]
        [Range(0f, 1f)] public float Wash;
        [Tooltip("How much of the wash's foam is handed to the sheet. Lower = the thin wash drops out and " +
                 "only its thicker foam shows; higher = the wash reads fuller.")]
        [Range(0.1f, 6f)] public float WashAmount;
        [Tooltip("How much older the wash's foam reads as the water drains back down the sand: 0 = no " +
                 "difference, 1 = the backwash leaves old, torn lace.")]
        [Range(0f, 1f)] public float WashBackwashAge;

        [Header("Every sheet")]
        [Tooltip("How hard the sheet tears apart as its foam ages: 0 = it fades as one even mat, 1 = foam " +
                 "half way through its age keeps only its densest hearts. The wake also reads the hull " +
                 "waves' own FoamAgeTear; the larger wins.")]
        [Range(0f, 1f)] public float AgeTear;

        [Header("Open-water edge")]
        [Tooltip("How far the open-water foam (wake and oars) is softened when it is read from the foam " +
                 "buffer (m). The buffer's cells are 1/8 m, so 0 reads them hard and square; about 0.2 m " +
                 "blends neighbouring cells into a soft edge. 0 here and in the warp = the older hard read.")]
        [Range(0f, 1f)] public float EdgeSoftenMetres;
        [Tooltip("How far the read point wanders (m) so a foam patch's edge is ragged rather than " +
                 "following the buffer's grid. 0 = straight cell edges.")]
        [Range(0f, 0.5f)] public float EdgeWarpMetres;
        [Tooltip("The size of the ragged wander (m): smaller = finer, lacier tearing at the edge.")]
        [Range(0.1f, 4f)] public float EdgeWarpSizeMetres;

        [Header("Band edges (every sheet)")]
        [Tooltip("How the sheet's tone steps are broken where one tone meets the next: 0 = the world Bayer " +
                 "cell (a regular 4-pixel checker, which reads as a 1/8 m grid across a thin foam mat), 1 = a " +
                 "fixed torn noise, so the steps meet in ragged lace instead. Every foam on the sea takes it.")]
        [Range(0f, 1f)] public float BandNoise;
        [Tooltip("The size of that torn noise's lumps (m): smaller = finer, lacier step edges.")]
        [Range(0.03f, 1f)] public float BandNoiseSizeMetres;

        [Header("Oar splash")]
        [Tooltip("How the oars' foam is drawn: 1 = where a blade goes in, works and comes out it lays foam " +
                 "into the one foam sheet (the same buffer, lace and ageing as the wake), and the oars draw no " +
                 "foam pictures or rings of their own; 0 = the older drawn puffs and rings, for an A/B.")]
        [Range(0f, 1f)] public float Oar;
        [Tooltip("Half-width of the foam a blade lays (m). The dory's blade is about 0.5 m long.")]
        [Range(0.05f, 1f)] public float OarRadius;
        [Tooltip("Half-width of the burst a blade lays where it goes in and where it comes out (m): the " +
                 "small torn white of the catch, wider than the smear of the pull.")]
        [Range(0.05f, 1f)] public float OarCatchRadius;
        [Tooltip("Foam a blade lays per second while it is pulling through the water, at full strength. " +
                 "Higher = a whiter puddle where each stroke works.")]
        [Range(0f, 10f)] public float OarPerSecond;
        [Tooltip("Foam laid at once where a blade goes in, at full strength: the white of the catch.")]
        [Range(0f, 1f)] public float OarCatch;
        [Tooltip("Foam laid at once where a blade comes out, at full strength: the puddle it leaves.")]
        [Range(0f, 1f)] public float OarRelease;
        [Tooltip("Crest height (m) of the ring of real water waves a blade sends out where it goes in, at full " +
                 "strength. Drawn by the water itself (lit faces and troughs, no picture). 0 = no ring.")]
        [Range(0f, 0.2f)] public float OarRingHeightMetres;
        [Tooltip("Crest spacing (m) of that ring. Longer = wider, slower rings that spread further before they fade.")]
        [Range(0.1f, 2f)] public float OarRingWavelengthMetres;
        [Tooltip("The ring where a blade comes out, as a share of the catch ring's height (0..1). 0 = none.")]
        [Range(0f, 1f)] public float OarReleaseRingShare;

        /// <summary>The shader's property defaults, value for value (<c>FoamSheetTests</c> pins the pair).</summary>
        public static FoamSheetSettings Default => new FoamSheetSettings
        {
            Shore = 1f,
            ShoreLineSharpness = 2.5f,
            ShoreAgeAcross = 0.5f,
            ShoreBackwashAge = 0.6f,

            Caps = 1f,
            CapAmount = 1.3f,
            Surf = 1f,
            SurfAmount = 1f,

            Storm = 1f,
            StormAmount = 4f,
            StormAge = 0.4f,

            Convergence = 1f,
            ConvergenceAge = 0.5f,

            Wash = 1f,
            WashAmount = 3f,
            WashBackwashAge = 0.6f,

            AgeTear = 1f,

            EdgeSoftenMetres = 0.2f,
            EdgeWarpMetres = 0.18f,
            EdgeWarpSizeMetres = 0.5f,

            BandNoise = 1f,
            BandNoiseSizeMetres = 0.12f,

            // The owner's ruling of 11-19, "Yes, small burst": each catch shows a small, clearly visible
            // burst of the sheet's lace that fades within a few seconds, with a short smear along the pull
            // (was 0.3 m, 0.5/s, 0.1, 0.08 with no separate catch size: a few invisible specks).
            Oar = 1f,
            // The owner's 11-21 reshoot ("Yes, fix then reshoot"): the 11-19 values (0.15 m, 0.25 m, 0.8/s,
            // 0.4, 0.16) read as pale specks at game zoom, noon worst. A whiter, slightly wider catch and a
            // firmer puddle, still well under the old pink blobs' size.
            OarRadius = 0.16f,
            OarCatchRadius = 0.3f,
            OarPerSecond = 1f,
            OarCatch = 0.6f,
            OarRelease = 0.26f,
            // Each catch reads again as a splash with a ring spreading from it, the ring being the sea's own
            // waves rather than a drawn picture.
            OarRingHeightMetres = 0.05f,
            OarRingWavelengthMetres = 0.5f,
            OarReleaseRingShare = 0.6f,
        };

        /// <summary>The band edges as the shader's <c>_FoamSheetDither</c> vector (noise share, lump size m,
        /// 0, 0).</summary>
        public Vector4 DitherVector => new Vector4(Mathf.Clamp01(BandNoise), Mathf.Max(BandNoiseSizeMetres, 0.03f), 0f, 0f);

        /// <summary>The open-water edge read as the shader's <c>_FoamSheetEdge</c> vector
        /// (soften m, warp m, warp size m, 0).</summary>
        public Vector4 EdgeVector => new Vector4(Mathf.Max(EdgeSoftenMetres, 0f), Mathf.Max(EdgeWarpMetres, 0f),
            Mathf.Max(EdgeWarpSizeMetres, 0.05f), 0f);

        /// <summary>True when the oars lay their foam into the sheet rather than drawing their own.</summary>
        public bool OarJoinsSheet => Oar >= 0.5f;

        /// <summary>The shore line's dials as the shader reads them (<c>_FoamSheetShore</c>).</summary>
        public Vector4 ShoreVector => new Vector4(Mathf.Clamp01(Shore), Mathf.Max(ShoreLineSharpness, 0.1f),
                                                  Mathf.Clamp01(ShoreAgeAcross), Mathf.Clamp01(ShoreBackwashAge));

        /// <summary>The caps' and surf's dials as the shader reads them (<c>_FoamSheetCaps</c>).</summary>
        public Vector4 CapsVector => new Vector4(Mathf.Clamp01(Caps), Mathf.Max(CapAmount, 0f),
                                                 Mathf.Clamp01(Surf), Mathf.Max(SurfAmount, 0f));

        /// <summary>The storm streaks' dials as the shader reads them (<c>_FoamSheetStorm</c>).</summary>
        public Vector4 StormVector => new Vector4(Mathf.Clamp01(Storm), Mathf.Max(StormAmount, 0f),
                                                  Mathf.Clamp01(StormAge), 0f);

        /// <summary>The convergence foam's dials as the shader reads them (<c>_FoamSheetConv</c>).</summary>
        public Vector4 ConvVector => new Vector4(Mathf.Clamp01(Convergence), Mathf.Clamp01(ConvergenceAge), 0f, 0f);

        /// <summary>The wash's dials as the shader reads them (<c>_FoamSheetWash</c>).</summary>
        public Vector4 WashVector => new Vector4(Mathf.Clamp01(Wash), Mathf.Max(WashAmount, 0f),
                                                 Mathf.Clamp01(WashBackwashAge), 0f);
    }
}
