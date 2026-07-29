using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The live set of REFLECTIVE renderers (ADR 0027 #8). Exists for the same two narrow reasons
    /// <see cref="IsoFacetHullRegistry"/> does, and one more that is specific to this item:
    ///
    /// <list type="number">
    /// <item><b>The zero-cost guarantee.</b> <see cref="IsoFacetHullFeature"/> consults
    /// <see cref="Count"/> and enqueues the reflection pass only when something is actually
    /// reflective, so a scene with no reflectors pays nothing at all (CLAUDE.md rule 7). The shipped
    /// state IS that scene: nothing carries <see cref="RenderingLayer"/> until the owner opts a
    /// prefab in, so zero-cost-when-idle is also this feature's passthrough proof.</item>
    /// <item><b>Membership is EXPLICIT.</b> A renderer joins the reflection list only by carrying
    /// <see cref="RenderingLayer"/>, which only <see cref="ReflectiveObject"/> sets. The
    /// <c>HHReflect</c> LightMode tag alone is not enough: the tree shader carries that pass, so
    /// EVERY tree in the scene would otherwise ride into the off-screen pass by accident — the exact
    /// trap ADR 0023 hit with the flat Sea sprite sharing the water shader.</item>
    /// <item><b>The set stays BOUNDED.</b> An unbounded reflective set is this item's perf failure
    /// mode (ADR 0027 open question). See <see cref="MaxReflectors"/>.</item>
    /// </list>
    ///
    /// <para><b>The bounding rule, stated (rule 6 — both halves are data, not literals here).</b>
    /// A renderer is in the reflection list only if ALL of:
    /// <list type="bullet">
    /// <item><b>Layer:</b> it carries <see cref="RenderingLayer"/> — i.e. someone put a
    /// <see cref="ReflectiveObject"/> on it deliberately;</item>
    /// <item><b>Distance:</b> its ground-contact pivot is within its own
    /// <c>ReflectiveObject.MaxHeightAboveWater</c> of the water level. A tree on a clifftop does not
    /// reflect in the harbour below it, and the check is throttled, not per-frame;</item>
    /// <item><b>Frustum:</b> Unity's own culling drops off-screen reflectors from the renderer list
    /// for free — the pass is built from <c>cullResults</c> like every other list.</item>
    /// </list>
    /// Worst case is therefore <see cref="MaxReflectors"/> renderers, one extra filtered renderer
    /// list and one <c>ARGBHalf</c> render target at camera resolution — and in practice frustum
    /// culling leaves far fewer.</para>
    /// </summary>
    public static class ReflectionRegistry
    {
        /// <summary>
        /// The rendering-layer bit that MAKES a renderer reflective. Deliberately adjacent to
        /// <see cref="DisplacedWaterRegistry.RenderingLayer"/> (1 &lt;&lt; 30) and equally explicit:
        /// the shader tag says "this renderer KNOWS how to draw a reflection", this bit says
        /// "…and it should".
        /// </summary>
        public const uint RenderingLayer = 1u << 29;

        /// <summary>
        /// The hard cap on the reflective set — the bounded-list half of the rule above. 64 is far
        /// above any plausible authored set (a harbour is a boat, a wharf and a treeline, not a
        /// forest) and far below where one extra filtered list would matter; exceeding it logs once
        /// rather than silently degrading, because a silent cap reads as "everything is covered"
        /// when it is not.
        /// </summary>
        public const int MaxReflectors = 64;

        static readonly List<ReflectiveObject> s_Live = new List<ReflectiveObject>();
        static Texture2D s_ClearFallback;
        static bool s_WarnedOverCap;

        /// <summary>How many reflectors are live and within their distance gate. The feature's
        /// cheap gate — 0 means the pass is never enqueued.</summary>
        public static int Count => s_Live.Count;

        internal static void Register(ReflectiveObject reflector)
        {
            if (reflector == null || s_Live.Contains(reflector)) return;
            s_Live.Add(reflector);
            EnsureFallbackBound();
            if (s_Live.Count > MaxReflectors && !s_WarnedOverCap)
            {
                s_WarnedOverCap = true;
                Debug.LogWarning(
                    $"[ReflectionRegistry] {s_Live.Count} reflectors live, over the {MaxReflectors} " +
                    "cap. They all still draw — the cap is a budget alarm, not a truncation — but " +
                    "the reflective set was meant to stay small (ADR 0027 #8).");
            }
        }

        internal static void Unregister(ReflectiveObject reflector)
        {
            s_Live.Remove(reflector);
            if (s_Live.Count <= MaxReflectors) s_WarnedOverCap = false;
        }

        /// <summary>
        /// Bind a fully-transparent 1×1 as the global reflection texture so the water shader — which
        /// samples it every frame the feature is dialled in — reads "no reflection anywhere" before
        /// the pass has ever run, instead of Unity's grey unbound-texture placeholder.
        ///
        /// <para>⚠️ The grey placeholder is not a cosmetic problem here: the water composites by the
        /// sampled ALPHA, and grey's alpha is ~0.5, so an unbound sampler would smear a flat grey
        /// half-mirror across the entire sea on the first frame of every scene. Same lesson, same
        /// shape, as the interior guard's black 1×1.</para>
        /// </summary>
        static void EnsureFallbackBound()
        {
            if (s_ClearFallback != null) return;
            s_ClearFallback = new Texture2D(1, 1, TextureFormat.RGBAHalf, false, true)
            {
                name = "HHReflectTexFallback",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            s_ClearFallback.SetPixel(0, 0, Color.clear);
            s_ClearFallback.Apply(false, true);
            Shader.SetGlobalTexture(ReflectionShaderIds.ReflectTex, s_ClearFallback);
        }
    }

    /// <summary>Shader property ids shared by <see cref="ReflectiveObject"/>, the renderer feature,
    /// the <c>HHReflect</c> passes and the water shader.</summary>
    public static class ReflectionShaderIds
    {
        /// <summary>The reflection render target, published globally by the feature's pass and
        /// sampled by the water shader. <c>ARGBHalf</c>: a night-lit source writes its colour
        /// PRE-COMPENSATED for the day/night multiply (far above 1), and an 8-bit target would
        /// clamp it — the same reason <c>_HHWaterScreenTex</c> is half float.</summary>
        public static readonly int ReflectTex = Shader.PropertyToID("_HHReflectTex");

        /// <summary>The per-renderer MIRROR AXIS: <c>xy</c> = the ground-contact pivot in world
        /// space (ADR 0026), <c>w</c> = 1 meaning "published". 🔴 It cannot be derived in-shader —
        /// <c>unity_ObjectToWorld</c> is IDENTITY for a SpriteRenderer, so every sprite would mirror
        /// about the world origin. See <see cref="WaterReflectionWarp"/>.</summary>
        public static readonly int ReflectOrigin = Shader.PropertyToID("_HHReflectOrigin");

        /// <summary>Per-renderer flag: 1 = this source is LIGHT content at night (a lit wheelhouse,
        /// a lamp), so its <c>HHReflect</c> pass pre-compensates for the day/night multiply and the
        /// water routes it to the post-grade bucket (§11.6). 0 = ordinary reflected surface.</summary>
        public static readonly int ReflectLit = Shader.PropertyToID("_HHReflectLit");
    }
}
