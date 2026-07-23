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
    /// <summary>
    /// The CALIBRATED iso-depth convention of the shared private z-buffer (ADR 0023 phase 3,
    /// step 1) — the one frame in which hull planking and the displaced sea z-test truthfully
    /// per pixel:
    ///
    /// <code>z(point) = BaseZ + (groundAnchorY − ReferenceY) · CosElev − heightAboveStillWater · SinElev</code>
    ///
    /// The WATER already computes exactly this per vertex (the HHWaterDisplaced vertex stage:
    /// groundAnchorY = the undisplaced vertex world y, height = the seam-faded lift, ReferenceY =
    /// <c>_HeightWorldMin.y</c>, Cos/SinElev = <c>_WaterIsoDepth</c>). A HULL joins it as ONE
    /// per-hull constant z translation (groundAnchorY = the boat root's world y, height = heave
    /// metres) applied by <see cref="IsoFacetHullRenderer"/> — constant, so every intra-hull
    /// depth relation (the rig's own <c>ry·cos − rz·sin</c> self-occlusion, the deck contract,
    /// the keyline's depth-difference darkening) is bit-preserved; only the hull-vs-water
    /// comparison gains meaning. At the contact line the ground terms of hull and adjacent water
    /// agree, so the compare reduces to heights: water covers exactly the planking below the
    /// lifted surface — the waterline.
    ///
    /// Published by the active <see cref="DisplacedWaterSurface"/> from ITS live material (the
    /// same uniforms the water shader reads — the two sides cannot disagree by construction) and
    /// cleared when that surface unregisters: no displaced water, no frame, no hull offset, and
    /// the water-off render stays byte-identical to today (the A/B contract).
    /// </summary>
    public readonly struct WaterIsoDepthFrame
    {
        /// <summary>The shared ground-y reference (<c>_HeightWorldMin.y</c> — one constant for
        /// the whole sea, so chunked meshes and every hull share one continuous depth ramp).</summary>
        public readonly float ReferenceY;
        /// <summary>cos(iso elevation) — <c>_WaterIsoDepth.x</c> (0.766 at the fleet's 40°).</summary>
        public readonly float CosElev;
        /// <summary>sin(iso elevation) — <c>_WaterIsoDepth.y</c> (0.643 at the fleet's 40°).</summary>
        public readonly float SinElev;
        /// <summary>World z of the undisplaced water plane (the chunk meshes' resting z).</summary>
        public readonly float BaseZ;

        public WaterIsoDepthFrame(float referenceY, float cosElev, float sinElev, float baseZ)
        {
            ReferenceY = referenceY;
            CosElev = cosElev;
            SinElev = sinElev;
            BaseZ = baseZ;
        }
    }

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
        static DisplacedWaterSurface s_FrameOwner;
        static WaterIsoDepthFrame s_Frame;

        /// <summary>How many displaced surfaces are live and toggled on. The feature's cheap gate.</summary>
        public static int Count => s_Live.Count;

        /// <summary>
        /// The live calibrated iso-depth frame (ADR 0023 phase 3) — true only while an active
        /// displaced surface has published one. Read by <see cref="IsoFacetHullRenderer"/> every
        /// pose push; false ⇒ hulls apply NO z offset and render exactly as before phase 3
        /// (the displaced-OFF byte-identity contract).
        /// </summary>
        public static bool TryGetIsoDepthFrame(out WaterIsoDepthFrame frame)
        {
            frame = s_Frame;
            return s_FrameOwner != null;
        }

        /// <summary>Publish the calibrated frame (the active surface, each throttled uniform
        /// tick — values read from the SAME live material the water shader samples). One sea per
        /// region: with multiple registered surfaces the last publisher wins.</summary>
        internal static void PublishIsoDepthFrame(DisplacedWaterSurface owner, in WaterIsoDepthFrame frame)
        {
            if (owner == null) return;
            s_FrameOwner = owner;
            s_Frame = frame;
        }

        internal static void Register(DisplacedWaterSurface surface)
        {
            if (surface == null || s_Live.Contains(surface)) return;
            s_Live.Add(surface);
            EnsureFallbackBound();
        }

        internal static void Unregister(DisplacedWaterSurface surface)
        {
            s_Live.Remove(surface);
            // The frame dies with its publisher: no active displaced sea ⇒ no hull offset.
            if (ReferenceEquals(s_FrameOwner, surface))
            {
                s_FrameOwner = null;
                s_Frame = default;
            }
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
