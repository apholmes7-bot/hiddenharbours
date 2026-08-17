using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The live set of SILHOUETTE SOURCES — the bodies that read through foliage (owner ruling,
    /// 2026-08-16: "slight occlusion, you always know where you are, never lose your character").
    /// In the shipped game that set is exactly one: the fisher.
    ///
    /// <para>Exists for the same two narrow reasons <see cref="ReflectionRegistry"/> does:</para>
    ///
    /// <list type="number">
    /// <item><b>The zero-cost guarantee.</b> <see cref="SilhouetteMaskFeature"/> consults
    /// <see cref="Count"/> and enqueues the mask pass only when something is actually silhouetted, so
    /// a scene with no player — the menu, a bare art scene, every EditMode fixture — pays nothing at
    /// all (CLAUDE.md rule 7).</item>
    /// <item><b>Membership is EXPLICIT.</b> A renderer joins the mask list only by carrying
    /// <see cref="RenderingLayer"/>, which only <see cref="SilhouetteMaskSource"/> sets. The
    /// <c>HHSilhouette</c> LightMode tag alone would be *sufficient today* — nothing but the mask
    /// material uses that shader — and that is precisely why the bit is here anyway: "sufficient
    /// today" is what the displaced water believed about the water shader before the flat Sea sprite
    /// shared it, and what the reflection pass believed before every tree turned out to carry an
    /// HHReflect pass. The bit costs one assignment and makes the set impossible to join by
    /// accident.</item>
    /// </list>
    ///
    /// <para><b>Why there is no cap.</b> <see cref="ReflectionRegistry"/> needs
    /// <c>MaxReflectors</c> because "reflective" is a property someone can put on any prefab and the
    /// list is unbounded by nature. This set is bounded by construction instead: the only thing that
    /// creates a source is <see cref="PlayerSilhouetteInstaller"/>, which attaches exactly one, to
    /// exactly one object, once. If that ever stops being true — villagers opted in, a second
    /// playable body — the cap argument comes back with it, and so does the budget alarm.</para>
    /// </summary>
    public static class SilhouetteRegistry
    {
        /// <summary>
        /// The rendering-layer bit that puts a renderer in the mask pass. Deliberately adjacent to
        /// <see cref="ReflectionRegistry.RenderingLayer"/> (1 &lt;&lt; 29) and
        /// <c>DisplacedWaterRegistry.RenderingLayer</c> (1 &lt;&lt; 30), and equally explicit: the
        /// shader tag says "this renderer KNOWS how to write a mask", this bit says "…and it should".
        /// </summary>
        public const uint RenderingLayer = 1u << 28;

        static readonly List<SilhouetteMaskSource> s_Live = new List<SilhouetteMaskSource>();
        static Texture2D s_ClearFallback;

        /// <summary>How many silhouette sources are live. The feature's cheap gate — 0 means the pass
        /// is never enqueued and the foliage's own read is switched off at the global (see
        /// <see cref="SilhouetteShaderIds.Params"/>).</summary>
        public static int Count => s_Live.Count;

        internal static void Register(SilhouetteMaskSource source)
        {
            if (source == null || s_Live.Contains(source)) return;
            s_Live.Add(source);
            EnsureFallbackBound();
        }

        internal static void Unregister(SilhouetteMaskSource source)
        {
            if (source == null) return;
            s_Live.Remove(source);
            if (s_Live.Count == 0) BindIdle();
        }

        /// <summary>
        /// Bind a 1x1 CLEAR texture as the mask, and zero the foliage's read strength.
        ///
        /// <para><b>⚠️ BOTH HALVES, and the second is the one that matters.</b> The
        /// rendering-guards-rot lesson, verbatim: a buffer needs a SOURCE and a zero must be a
        /// STRENGTH, not an absence. Leaving a stale mask bound would freeze the fisher's last
        /// silhouette into every tree on screen; leaving the strength up with a cleared mask would
        /// cost every foliage fragment a texture read that can only ever return 0. So the idle state
        /// says both things: there is nothing to read, and do not read it.</para>
        /// </summary>
        public static void BindIdle()
        {
            EnsureFallbackBound();
            Shader.SetGlobalVector(SilhouetteShaderIds.Params, Vector4.zero);
        }

        static void EnsureFallbackBound()
        {
            if (s_ClearFallback == null)
            {
                s_ClearFallback = new Texture2D(1, 1, TextureFormat.R8, mipChain: false)
                {
                    name = "HHSilhouetteMaskClearFallback",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                s_ClearFallback.SetPixel(0, 0, Color.clear);
                s_ClearFallback.Apply(updateMipmaps: false);
            }
            // Bound unconditionally rather than "if nothing is bound": there is no way to ask whether
            // a global texture is set, and a shader that samples an unbound Texture2D reads garbage on
            // some backends rather than the black every platform's docs promise.
            Shader.SetGlobalTexture(SilhouetteShaderIds.MaskTex, s_ClearFallback);
        }
    }

    /// <summary>The shader property ids the silhouette pass and its foliage consumers share. Twin of
    /// <c>IsoFacetShaderIds</c>; ids are cached because both sides write them per frame.</summary>
    public static class SilhouetteShaderIds
    {
        /// <summary>The screen-space coverage mask: the fisher's sprite alpha at her own screen pixel,
        /// 0 everywhere else. Written by <see cref="SilhouetteMaskFeature"/> before any sprite draws,
        /// sampled by every foliage fragment through <c>SilhouetteThrough</c>.</summary>
        public static readonly int MaskTex = Shader.PropertyToID("_HHSilhouetteMaskTex");

        /// <summary>The owner's dial, packed: <c>rgb</c> = the silhouette tint,
        /// <c>a</c> = strength in [0,1]. <b>a is the gate as well as the amount</b> — at 0 the foliage
        /// skips the texture read entirely, which is what makes "off" genuinely free rather than
        /// merely invisible.</summary>
        public static readonly int Params = Shader.PropertyToID("_HHSilhouetteParams");
    }
}
