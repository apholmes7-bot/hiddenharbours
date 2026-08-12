using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// One <b>aid to navigation</b> — a mark that tells the player where the channel, the rock or the
    /// mooring is. Content is data, not code (ADR 0003): a new mark is a new asset with a stable id,
    /// never a C# case.
    ///
    /// <para><b>⚠️ NOT the lobster pot float.</b> That is <see cref="TrapBuoyPresenter"/>'s buoy —
    /// 1.2 m of foam in a fisher's colours saying "my pots are here". This is steel channel furniture,
    /// up to 6.6 m tall, and it says "the channel is here". They share one thing on purpose:
    /// <see cref="BuoyWaveVisual"/>, so both ride the ONE shared wave field (P1) rather than two
    /// bobs that drift apart.</para>
    ///
    /// <para><b>One asset per mark TYPE, with the size ladder inside it.</b> The kit's real structure
    /// is mark × diameter — a north cardinal is the same mark at 1.2 m in a creek mouth and 3.0 m off
    /// a headland — so the sizes are <see cref="Sizes"/> entries rather than ten near-duplicate
    /// assets. Each entry carries its own facings AND its own float geometry, because a 3 m hull does
    /// not sit in the water like a 1.2 m one.</para>
    ///
    /// <para><b>Decor tier, never saved (rule 5).</b> A mark drives no gameplay in this slice: no
    /// collision, no chart, no light. It is furniture that floats correctly. Everything a future
    /// slice needs — the IALA meaning, the light character, the water depth the size is rated for —
    /// is carried here as DATA so wiring it later needs no re-bake.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Nav Buoy", fileName = "NavBuoy")]
    public class NavBuoyDef : ScriptableObject
    {
        /// <summary>One rung of the mark's size ladder: the art, and how that hull floats.</summary>
        [Serializable]
        public class SizeEntry
        {
            [Tooltip("The kit's size id — s12 / s18 / s20 / s24 / s30.")]
            public string SizeId = "s18";

            [Tooltip("Hull diameter (m). 1.2 harbour · 1.75 the working default · 2.0 main channel · " +
                     "2.4 shipping · 3.0 landfall.")]
            public float DiameterMeters = 1.75f;

            [Tooltip("Water depth this size is rated for, as the kit states it (e.g. \"5–20 m\"). " +
                     "Data for the owner's mapping session; nothing reads it yet.")]
            public string RatedWater = "5–20 m";

            [Tooltip("The 8 facings, cell order N NE E SE S SW W NW. ⚠️ The kit is CLOCKWISE " +
                     "(measured): cell i depicts heading +45°·i. Do not re-order to match a boat.")]
            public Sprite[] Facings = new Sprite[8];

            [Header("How this hull floats (fed to BuoyWaveVisual — NOT hand-guessed)")]
            [Tooltip("Total painted height of the sprite in metres. The crest climb is measured " +
                     "against this, so a tall steel mark reads the same crest as a SMALLER fraction " +
                     "of its side than a little can does.")]
            [Min(0.05f)] public float SpriteHeightMeters = 2.6f;

            [Tooltip("Where the still waterline sits up the sprite (0 = bottom, 1 = top). ⚠️ This is " +
                     "the sprite's own normalised pivot y — the nav-buoy sheets pivot ON the " +
                     "waterline — so it is DERIVED from the bake, never tuned by eye.")]
            [Range(0f, 1f)] public float FloatLineFraction = 0.2f;

            [Tooltip("Slope-follow share from the rig's own hydrostatics: BM/(BM+BG). A flat can is " +
                     "~0.63 and rolls its guts out; a counterweighted steel buoy is ~0.13 and stands " +
                     "up; a spar is ~0.002. Scales the bob so a steel pillar does not cork about.")]
            [Range(0f, 1f)] public float SlopeFollow = 0.6f;
        }

        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): type.snake_case, e.g. buoy.cardinal_north.")]
        public string Id = "buoy.unnamed";

        [Tooltip("The rig's own type key — CardinalN, PortCan, StbdLit … Ties this asset back to the " +
                 "baked sheets and the kit contract.")]
        public string MarkType = "PortCan";

        [Tooltip("What a skipper calls it: \"North cardinal\", \"Port hand\", \"Mooring\".")]
        public string DisplayName = "Port hand";

        [Tooltip("What the mark MEANS. IALA Region B as the Canadian Coast Guard flies it. Data for a " +
                 "future chart/inspect feature — nothing reads it yet.")]
        [TextArea(2, 3)]
        public string Gloss = "Keep on your LEFT going upstream. Odd numbers. The SHAPE is the mark.";

        [Header("Light (DATA ONLY — nothing flashes yet; that is its own feature)")]
        [Tooltip("The light character id from the kit — FlG4, Q9, Mo(A)… Empty means an unlit mark.")]
        public string LightCharacter = "FlG4";

        [Tooltip("Human-readable light character, e.g. \"Fl G 4s\". Empty means unlit.")]
        public string LightText = "Fl G 4s";

        [Header("The size ladder (one rung per baked diameter)")]
        [Tooltip("Ordered smallest → largest. Each rung carries its own 8 facings and float geometry.")]
        public List<SizeEntry> Sizes = new List<SizeEntry>();

        [Tooltip("Which rung a placement uses when it does not name one. Index into Sizes.")]
        [Min(0)] public int DefaultSizeIndex = 1;

        /// <summary>The rung for a size id, or null. Ordinal — these are asset keys, not prose.</summary>
        public SizeEntry Size(string sizeId)
        {
            if (Sizes == null) return null;
            for (int i = 0; i < Sizes.Count; i++)
                if (string.Equals(Sizes[i]?.SizeId, sizeId, StringComparison.Ordinal))
                    return Sizes[i];
            return null;
        }

        /// <summary>The rung <see cref="DefaultSizeIndex"/> names, clamped, or null if there are none.</summary>
        public SizeEntry DefaultSize()
        {
            if (Sizes == null || Sizes.Count == 0) return null;
            return Sizes[Mathf.Clamp(DefaultSizeIndex, 0, Sizes.Count - 1)];
        }

        /// <summary>Is this mark lit? Data only — no light is rendered in this slice.</summary>
        public bool IsLit => !string.IsNullOrEmpty(LightCharacter);
    }
}
