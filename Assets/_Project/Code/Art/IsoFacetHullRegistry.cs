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
        static readonly Stack<int> s_FreeForeBlocks = new Stack<int>();
        static int s_NextId = 1;
        static Texture2D s_ClearFallback;
        static Texture2D s_GuardFallback;

        /// <summary>How many hulls are live. The renderer feature's cheap gate.</summary>
        public static int Count => s_Hulls.Count;

        internal static int Register(IsoFacetHullRenderer hull)
        {
            s_Hulls.Add(hull);
            EnsureFallbackBound();
            return TakeId();
        }

        /// <summary>
        /// A hull's FORE BLOCK — <paramref name="count"/> CONTIGUOUS ids reserved for the
        /// deck-occupant split (owner playtest 2026-08-07), one per occupancy band. Returns the base
        /// of the block, or <b>0</b> when the pool cannot spare one. Taken from the same pool as the
        /// hull's own id, because they live in the same 8-bit alpha channel and must not be able to
        /// collide with another boat's.
        ///
        /// <para><b>Why contiguous.</b> The facet pass writes <c>base + band</c>, where the band is
        /// how many occupants a pixel is in front of; consecutive ids are what let an occupant's
        /// hiding region be one RANGE test in the sprite shader instead of one compare per band.
        /// Nothing else needs them adjacent, and nothing may assume the base is stable across a
        /// re-registration.</para>
        ///
        /// <para>It does NOT add the hull to <see cref="Count"/>: that number is the render feature's
        /// zero-cost gate ("is there anything to draw?"), and one boat is one boat however many ids
        /// she wears. Counting her twice would be harmless today and wrong the first time anything
        /// budgeted off it.</para>
        ///
        /// <para><b>The budget is the real cost of the slot count.</b> One id plus a block of
        /// <c>count</c> per hull means <c>255 / (count + 1)</c> simultaneous mesh hulls — at the
        /// shipped twelve slots, <b>19</b>, against a roadmap whose largest scene is a harbour of a
        /// dozen. Returning 0 rather than collapsing onto a shared block is deliberate: a shared FORE
        /// block would make one boat's crew discard against another boat's planking, which is a
        /// wrong picture, where 0 is merely the pre-split one.</para>
        /// </summary>
        internal static int RegisterForeBlock(int count) => TakeIdBlock(count);

        internal static void Unregister(IsoFacetHullRenderer hull, int id)
        {
            if (s_Hulls.Remove(hull) && id >= 1 && id < 255)
                s_FreeIds.Push(id);
        }

        /// <summary>Give a FORE block back. Not paired with the hull list — the hull's own
        /// <see cref="Unregister"/> owns that; this only returns the numbers.</summary>
        internal static void UnregisterForeBlock(int baseId, int count)
        {
            if (baseId >= 1 && count >= 1 && baseId + count - 1 <= 255)
                s_FreeForeBlocks.Push(baseId);
        }

        static int TakeId()
        {
            if (s_FreeIds.Count > 0) return s_FreeIds.Pop();
            if (s_NextId > 255)
            {
                // 255 ids — shared between hull ids and their fore blocks — would be a fleet nobody
                // budgeted for; collapsing onto id 255 degrades overlap separation for the surplus,
                // nothing worse.
                Debug.LogWarning("[IsoFacetHullRegistry] Facet hull ids exhausted (255 in use); " +
                                 "further hulls share id 255 and may composite over one another.");
                return 255;
            }
            return s_NextId++;
        }

        /// <summary>
        /// <paramref name="count"/> contiguous ids, or 0 if the pool cannot spare them.
        ///
        /// <para>Freed blocks are reused whole, never split: every block this hands out is the same
        /// size, so a returned one always fits the next request exactly and the pool cannot
        /// fragment.</para>
        /// </summary>
        static int TakeIdBlock(int count)
        {
            if (count < 1) return 0;
            if (s_FreeForeBlocks.Count > 0) return s_FreeForeBlocks.Pop();
            if (s_NextId + count - 1 > 255)
            {
                Debug.LogWarning(
                    $"[IsoFacetHullRegistry] Facet hull ids exhausted ({s_NextId - 1} in use); this " +
                    $"hull gets NO deck-occupant block ({count} contiguous ids needed). Anything " +
                    "standing on her deck will draw over her cabins instead of behind them. Each " +
                    "hull costs 1 + " + count + " ids out of 255.");
                return 0;
            }
            int baseId = s_NextId;
            s_NextId += count;
            return baseId;
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
            EnsureGuardFallbackBound();
        }

        /// <summary>
        /// Bind a 1×1 <b>BLACK</b> texture as the global interior-guard so the displaced water's
        /// discard reads "exterior everywhere" before the guard pass has ever run.
        ///
        /// <para>⚠️ <b>Black, not clear, and this is load-bearing.</b> The water fragment discards
        /// where the guard reads <c>&gt; 0.5</c>. An UNBOUND sampler returns Unity's grey
        /// placeholder — approximately 0.5 — so leaving it unbound makes "does the entire sea
        /// disappear this frame?" a coin flip decided by a driver's rounding. A clear (alpha-0)
        /// texture is no safer: the guard is single-channel and only .r is read, and clear's red
        /// IS 0, but naming it black is what keeps the next reader from "tidying" it to a
        /// transparent white.</para>
        /// </summary>
        static void EnsureGuardFallbackBound()
        {
            if (s_GuardFallback != null) return;
            s_GuardFallback = new Texture2D(1, 1, TextureFormat.RGBA32, false, false)
            {
                name = "HHHullGuardFallback",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
            };
            s_GuardFallback.SetPixel(0, 0, Color.black);
            s_GuardFallback.Apply(false, true);
            Shader.SetGlobalTexture(IsoFacetShaderIds.GuardTex, s_GuardFallback);
        }
    }

    /// <summary>Shader property ids shared by the component, the feature and the shaders.</summary>
    public static class IsoFacetShaderIds
    {
        public static readonly int HullScreenTex = Shader.PropertyToID("_HHHullScreenTex");
        /// <summary>The displaced water surface's resolved screen texture (ADR 0023 phase 2) —
        /// written by the feature's water pass, sampled by the in-scene WaterOverlay quad.</summary>
        public static readonly int WaterScreenTex = Shader.PropertyToID("_HHWaterScreenTex");
        /// <summary>The per-pixel INTERIOR GUARD (ADR 0023, the per-face interior mask): 1 where the
        /// nearest hull surface at that pixel is an open interior, 0 everywhere else. Written by the
        /// facet shader's HHHullGuard pass into its own depth buffer, read by the displaced water's
        /// fragment, which discards against it — so the sea can never draw inside a boat, at any
        /// heading, with no footprint scan and no duplicated wave maths.</summary>
        public static readonly int GuardTex = Shader.PropertyToID("_HHHullGuardTex");
        public static readonly int FacetTex = Shader.PropertyToID("_HHFacetTex");
        public static readonly int DarkTex = Shader.PropertyToID("_HHDarkTex");
        public static readonly int KeyTex = Shader.PropertyToID("_HHKeyTex");
        public static readonly int DepthTex = Shader.PropertyToID("_HHDepthTex");
        /// <summary>The keyline gate (ADR 0031 — the keyline retirement): 1 = the resolve floods the
        /// legacy 1 px outline (its rule 2), 0 = the flood is retired and empty pixels STAY empty.
        /// Gates ONLY the flood — the depth-edge darkening (rule 1) never reads it. Written per frame
        /// by <see cref="IsoFacetKeylineGate.Apply"/> from <c>GameServices.HullKeylineFlood</c>; the
        /// shader property defaults to 0 so a bypassed write fails to the SHIPPED (outline-free)
        /// style, never back to outlines.</summary>
        public static readonly int KeylineFlood = Shader.PropertyToID("_HHKeylineFlood");

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

        /// <summary>The BASE of a hull's contiguous FORE BLOCK (already /255) — the deck-occupant
        /// split. Geometry nearer the camera than the occupants standing on her deck is written with
        /// <c>base + band - 1</c> instead of <see cref="HullId"/>, the band being how many of them
        /// that pixel is in front of, so their own shaders can discard behind it. The overlay
        /// composes the WHOLE block, so the boat's own picture is unchanged. 0 = no split.</summary>
        public static readonly int HullIdFore = Shader.PropertyToID("_HullIdFore");

        /// <summary>How many ids that fore block holds — what the overlay needs to accept all of a
        /// hull's bands as hers with one range test. 1 reproduces the single-plane split exactly.</summary>
        public static readonly int HullIdForeSpan = Shader.PropertyToID("_HullIdForeSpan");

        /// <summary>The occupant SLOTS: one float4 each, x = that occupant's view depth in the same
        /// world z the facet fragment carries, w = 1 only while the slot is claimed and standing.
        /// Always written full-length — a shorter array would leave the tail of a previous hull's
        /// values live on the GPU.</summary>
        public static readonly int DeckOccupant = Shader.PropertyToID("_DeckOccupant");

        /// <summary>How many slots are live. 0 — every hull with nobody aboard — is the early-out
        /// that leaves the facet alpha byte-identical to before the split existed, and it is why the
        /// slot loop costs nothing on the hulls that are nearly all of them.</summary>
        public static readonly int DeckOccupantCount = Shader.PropertyToID("_DeckOccupantCount");

        /// <summary>Per RENDERER on an occludable sprite: the LOWEST id that hides it — the fore id
        /// of its own occupancy rank on the hull it stands on — already /255. 0 = nothing occludes
        /// this sprite, which is every sprite in the game bar those on a deck.</summary>
        public static readonly int DeckOccluderId = Shader.PropertyToID("_HHDeckOccluderId");

        /// <summary>Per RENDERER on an occludable sprite: the TOP of that hull's fore block, /255.
        /// Bounds the discard range to the hull the sprite is actually standing on — without it a
        /// fisher could be cut out by another boat's id.</summary>
        public static readonly int DeckOccluderIdTop = Shader.PropertyToID("_HHDeckOccluderIdTop");
    }
}
