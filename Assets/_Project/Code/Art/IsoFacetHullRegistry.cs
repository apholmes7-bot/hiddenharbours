using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The live set of mesh hulls (ADR 0022 phase 3). Exists for two narrow reasons:
    ///
    /// <list type="number">
    /// <item><b>The zero-cost guarantee.</b> <see cref="IsoFacetHullFeature"/> is on the project's
    /// 2D renderer for EVERY camera; it consults <see cref="Count"/> and enqueues nothing at all
    /// when no hull is alive, so scenes without mesh hulls pay nothing (CLAUDE.md rule 7).</item>
    /// <item><b>Hull ids.</b> Every hull carries a stable id in [1, 255] written into the facet
    /// buffer's alpha channel, so each hull's screen overlay re-composes only ITS OWN pixels —
    /// two overlapping hulls must not paint each other's image at each other's sorting position.
    /// 0 is reserved for "no hull here".</item>
    /// </list>
    /// </summary>
    public static class IsoFacetHullRegistry
    {
        static readonly List<IsoFacetHullRenderer> s_Hulls = new List<IsoFacetHullRenderer>();
        static readonly Stack<int> s_FreeIds = new Stack<int>();
        static int s_NextId = 1;
        static Texture2D s_ClearFallback;

        /// <summary>How many hulls are live. The renderer feature's cheap gate.</summary>
        public static int Count => s_Hulls.Count;

        internal static int Register(IsoFacetHullRenderer hull)
        {
            s_Hulls.Add(hull);
            EnsureFallbackBound();
            if (s_FreeIds.Count > 0) return s_FreeIds.Pop();
            if (s_NextId > 255)
            {
                // 255 simultaneous mesh hulls would be a fleet nobody budgeted for; collapsing
                // onto id 255 degrades overlap separation for the surplus, nothing worse.
                Debug.LogWarning("[IsoFacetHullRegistry] More than 255 live mesh hulls; ids exhausted.");
                return 255;
            }
            return s_NextId++;
        }

        internal static void Unregister(IsoFacetHullRenderer hull, int id)
        {
            if (s_Hulls.Remove(hull) && id >= 1 && id < 255)
                s_FreeIds.Push(id);
        }

        /// <summary>
        /// Bind a fully-transparent 1×1 as the global hull-screen texture so an overlay quad that
        /// renders before the feature has ever run (or with the feature disabled) discards every
        /// pixel instead of sampling Unity's grey unbound-texture placeholder.
        /// </summary>
        static void EnsureFallbackBound()
        {
            if (s_ClearFallback != null) return;
            s_ClearFallback = new Texture2D(1, 1, TextureFormat.RGBA32, false, false)
            {
                name = "HHHullTexFallback",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
            };
            s_ClearFallback.SetPixel(0, 0, Color.clear);
            s_ClearFallback.Apply(false, true);
            Shader.SetGlobalTexture(IsoFacetShaderIds.HullScreenTex, s_ClearFallback);
        }
    }

    /// <summary>Shader property ids shared by the component, the feature and the shaders.</summary>
    public static class IsoFacetShaderIds
    {
        public static readonly int HullScreenTex = Shader.PropertyToID("_HHHullScreenTex");
        /// <summary>The displaced water surface's resolved screen texture (ADR 0023 phase 2) —
        /// written by the feature's water pass, sampled by the in-scene WaterOverlay quad.</summary>
        public static readonly int WaterScreenTex = Shader.PropertyToID("_HHWaterScreenTex");
        public static readonly int FacetTex = Shader.PropertyToID("_HHFacetTex");
        public static readonly int DarkTex = Shader.PropertyToID("_HHDarkTex");
        public static readonly int KeyTex = Shader.PropertyToID("_HHKeyTex");
        public static readonly int DepthTex = Shader.PropertyToID("_HHDepthTex");

        public static readonly int RampTex = Shader.PropertyToID("_RampTex");
        public static readonly int DarkRampTex = Shader.PropertyToID("_DarkRampTex");
        public static readonly int RampMeta = Shader.PropertyToID("_RampMeta");
        public static readonly int Bayer = Shader.PropertyToID("_Bayer");
        public static readonly int LightN = Shader.PropertyToID("_LN");
        public static readonly int Gain = Shader.PropertyToID("_Gain");
        public static readonly int Bias = Shader.PropertyToID("_Bias");
        public static readonly int KeyColor = Shader.PropertyToID("_KeyColor");
        public static readonly int HullOrigin = Shader.PropertyToID("_HullOrigin");
        public static readonly int PivotPx = Shader.PropertyToID("_PivotPx");
        public static readonly int PixelsPerMetre = Shader.PropertyToID("_PixelsPerMetre");
        public static readonly int HullId = Shader.PropertyToID("_HullId");
    }
}
