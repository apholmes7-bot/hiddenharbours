using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The live set of renderers the sea draws UNDER its surface (W4-4c, the props in the shallows). A
    /// shore plant wholly under the live water joins it, and the sea's own composite then reads the plant
    /// through the water column exactly as it reads the floor beside it: absorbed per channel by the
    /// mood's depths, lit by the caustics, graded. One optical law, in the water shader, and no prop's
    /// shader carries a copy of it.
    ///
    /// <list type="number">
    /// <item><b>The zero-cost guarantee.</b> <see cref="IsoFacetHullFeature"/> records the under-water
    /// pass only while <c>GameConfig.ShallowsSubmergedProps</c> is on AND <see cref="Count"/> is above 0.
    /// With the switch off (the ship default) or nothing under the water, nothing is recorded, the sea's
    /// read stays lowered and the sea draws today's pixels.</item>
    /// <item><b>Membership is EXPLICIT.</b> A renderer joins the list only by carrying
    /// <see cref="RenderingLayer"/>, which only <see cref="ShorePlantTideView"/> sets, through
    /// <see cref="ShorePlantTideMath.UnderWaterMask"/>. The <c>HHUnderWater</c> LightMode tag alone is
    /// not enough: every lit sprite carries that pass, the shrubs and the houses too.</item>
    /// <item><b>One waterline.</b> The bit follows the plant's own submerged test (the tide over its own
    /// ground against its drawn height), the test that already sorts it under the sea plane, so the list
    /// and the sorting cannot disagree. A rock has no such test yet and never joins (water-rendering.md
    /// §17.13 says what its own would read).</item>
    /// </list>
    ///
    /// <para><b>Visual only.</b> Nothing here is saved or read by the simulation (rule 5): the bit is a
    /// play-mode state the plant recomputes from the tide and its ground on the slow tick.</para>
    /// </summary>
    public static class UnderWaterRegistry
    {
        /// <summary>
        /// The rendering-layer bit that puts a renderer under the water. Beside the other explicit bits
        /// (<see cref="SilhouetteRegistry"/> 28, <see cref="ReflectionRegistry"/> 29,
        /// <see cref="DisplacedWaterRegistry"/> 30) and as explicit: the shader tag says "this renderer
        /// KNOWS how to draw under the water", this bit says "…and it is under it now".
        /// </summary>
        public const uint RenderingLayer = 1u << 27;

        static readonly HashSet<Renderer> s_Live = new HashSet<Renderer>();
        static Texture2D s_ClearFallback;

        /// <summary>How many renderers are under the water now. The feature's cheap gate: 0 means the
        /// pass is never recorded.</summary>
        public static int Count => s_Live.Count;

        internal static void Register(Renderer renderer)
        {
            if (renderer != null) s_Live.Add(renderer);
        }

        // No null test: a renderer being destroyed is still its own key, and it must leave the set.
        internal static void Unregister(Renderer renderer) => s_Live.Remove(renderer);

        /// <summary>
        /// Bind a clear 1×1 as the props texture, so a sea that reads it before the pass has run, or
        /// after it stopped, reads "nothing under the water here" instead of Unity's grey placeholder.
        /// The sea also reads it only while the pass's own flag is raised; this is the second half of
        /// the same promise, the shape <see cref="ReflectionRegistry.BindIdle"/> keeps.
        /// </summary>
        internal static void BindIdle()
        {
            if (s_ClearFallback == null)
            {
                s_ClearFallback = new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
                {
                    name = "HHUnderWaterTexFallback",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                s_ClearFallback.SetPixel(0, 0, Color.clear);
                s_ClearFallback.Apply(false, true);
            }
            Shader.SetGlobalTexture(UnderWaterShaderIds.UnderWaterTex, s_ClearFallback);
        }
    }

    /// <summary>Shader property ids shared by the renderer feature's under-water pass and the water
    /// shader (W4-4c). Both are globals and never material properties, so no preset or material can
    /// carry a stale one.</summary>
    public static class UnderWaterShaderIds
    {
        /// <summary>The props under the water: every renderer in <see cref="UnderWaterRegistry"/>, drawn
        /// by its <c>HHUnderWater</c> pass where it stands, albedo only, premultiplied, at camera
        /// resolution. <c>ARGB32</c> sRGB: unlit albedo never exceeds 1.</summary>
        public static readonly int UnderWaterTex = Shader.PropertyToID("_HHUnderWaterTex");

        /// <summary>1 once this camera's under-water pass has drawn, 0 otherwise: lowered per camera
        /// before every early return and raised only by that pass, so the sea never reads another
        /// camera's props, nor a texture nobody drew this frame.</summary>
        public static readonly int UnderWaterLive = Shader.PropertyToID("_HHUnderWaterLive");
    }
}
