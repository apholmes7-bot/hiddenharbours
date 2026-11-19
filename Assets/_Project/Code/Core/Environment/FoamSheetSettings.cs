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

        [Header("Every sheet")]
        [Tooltip("How hard the sheet tears apart as its foam ages: 0 = it fades as one even mat, 1 = foam " +
                 "half way through its age keeps only its densest hearts. The wake also reads the hull " +
                 "waves' own FoamAgeTear; the larger wins.")]
        [Range(0f, 1f)] public float AgeTear;

        /// <summary>The shader's property defaults, value for value (<c>FoamSheetTests</c> pins the pair).</summary>
        public static FoamSheetSettings Default => new FoamSheetSettings
        {
            Shore = 1f,
            ShoreLineSharpness = 2.5f,
            ShoreAgeAcross = 0.5f,
            ShoreBackwashAge = 0.6f,

            Caps = 1f,
            CapAmount = 0.6f,
            Surf = 1f,
            SurfAmount = 1f,

            AgeTear = 1f,
        };

        /// <summary>The shore line's dials as the shader reads them (<c>_FoamSheetShore</c>).</summary>
        public Vector4 ShoreVector => new Vector4(Mathf.Clamp01(Shore), Mathf.Max(ShoreLineSharpness, 0.1f),
                                                  Mathf.Clamp01(ShoreAgeAcross), Mathf.Clamp01(ShoreBackwashAge));

        /// <summary>The caps' and surf's dials as the shader reads them (<c>_FoamSheetCaps</c>).</summary>
        public Vector4 CapsVector => new Vector4(Mathf.Clamp01(Caps), Mathf.Max(CapAmount, 0f),
                                                 Mathf.Clamp01(Surf), Mathf.Max(SurfAmount, 0f));
    }
}
