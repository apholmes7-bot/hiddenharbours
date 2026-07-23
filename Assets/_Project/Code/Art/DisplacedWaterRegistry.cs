using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The live set of displaced water surfaces (ADR 0023 phase 2) — the water-side mirror of
    /// <see cref="IsoFacetHullRegistry"/>, for the same two narrow reasons:
    ///
    /// <list type="number">
    /// <item><b>The zero-cost guarantee.</b> <see cref="IsoFacetHullFeature"/> consults
    /// <see cref="Count"/> and records no water pass when no displaced surface is active — so the
    /// A/B toggle OFF (and every scene without the surface) pays exactly nothing, and the flat
    /// water renders precisely as it does today (CLAUDE.md rule 7; the toggle's contract).</item>
    /// <item><b>The unbound-texture fallback.</b> The in-scene WaterOverlay quad samples the
    /// global <c>_HHWaterScreenTex</c>; before the feature has ever run (or with it disabled) a
    /// 1×1 CLEAR fallback is bound so the quad draws nothing instead of Unity's grey unbound
    /// placeholder — the <see cref="IsoFacetHullRegistry"/> pattern verbatim.</item>
    /// </list>
    /// </summary>
    public static class DisplacedWaterRegistry
    {
        /// <summary>
        /// The rendering-layer bit the off-screen water renderer list filters on. The displaced
        /// chunk MeshRenderers carry ONLY this bit; everything else keeps Unity's default layer 1.
        /// This is what keeps OTHER renderers whose material also carries the water shader's
        /// HHWater pass (the flat Sea sprite, the owner's preset-derived materials) out of the
        /// off-screen pass — membership is explicit, not an accident of sharing a shader.
        /// </summary>
        public const uint RenderingLayer = 1u << 30;

        static readonly List<DisplacedWaterSurface> s_Live = new List<DisplacedWaterSurface>();
        static Texture2D s_ClearFallback;

        /// <summary>How many displaced surfaces are live and toggled on. The feature's cheap gate.</summary>
        public static int Count => s_Live.Count;

        internal static void Register(DisplacedWaterSurface surface)
        {
            if (surface == null || s_Live.Contains(surface)) return;
            s_Live.Add(surface);
            EnsureFallbackBound();
        }

        internal static void Unregister(DisplacedWaterSurface surface)
        {
            s_Live.Remove(surface);
        }

        /// <summary>
        /// Bind a fully-transparent 1×1 as the global water-screen texture so an overlay quad that
        /// renders before the feature has ever run draws nothing instead of sampling Unity's grey
        /// unbound-texture placeholder (the <see cref="IsoFacetHullRegistry"/> pattern).
        /// </summary>
        static void EnsureFallbackBound()
        {
            if (s_ClearFallback != null) return;
            s_ClearFallback = new Texture2D(1, 1, TextureFormat.RGBA32, false, false)
            {
                name = "HHWaterTexFallback",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
            };
            s_ClearFallback.SetPixel(0, 0, Color.clear);
            s_ClearFallback.Apply(false, true);
            Shader.SetGlobalTexture(IsoFacetShaderIds.WaterScreenTex, s_ClearFallback);
        }
    }
}
