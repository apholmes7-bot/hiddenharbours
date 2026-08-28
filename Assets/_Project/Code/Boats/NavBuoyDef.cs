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
    /// <para><b>She is MOORED now, and she pushes back (2026-08-27).</b> The decor tier ended at the
    /// owner's word after he watched a skipper drive through the buoyed entrance: a mark carries a
    /// displacement, a girth and a watch circle, and <see cref="NavBuoyMooring"/> turns them into a
    /// buoy that yields, rebounds and settles home. Still no chart and no light — those are their
    /// own slices, and the data for both is already carried here so wiring them needs no re-bake.</para>
    ///
    /// <para><b>Never saved (rule 5).</b> Where a knocked mark is at this instant is transient and
    /// recomputed from her anchor; nothing about a collision survives a reload or is random.</para>
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

            [Header("How this hull takes a knock (fed to NavBuoyMooring)")]
            [Tooltip("Her girth in the water in metres — the radius a hull actually meets. Her " +
                     "own diameter halved, so the thing you collide with is the thing you see.")]
            [Min(0.05f)] public float CollisionRadiusMeters = 0.875f;

            [Tooltip("⭐ Her displacement in kg, and THE mass-response knob. A struck hull's " +
                     "deflection is m_buoy/(m_buoy + m_hull) of the closing speed, so this one " +
                     "number sets the whole ladder: a punt shouldered off her line, a cape " +
                     "islander barely noticing, a tanker not at all. On the fleet's own scale " +
                     "(BoatHullDef.MassKg), because momentum between two hulls in different " +
                     "units is nonsense.")]
            [Min(1f)] public float MooredMassKg = 300f;

            [Tooltip("How far from her anchor she may swing before the chain comes taut, in " +
                     "metres. Bigger marks carry more scope. She can be shoved this far and no " +
                     "further — a mark dragged across a harbour marks nothing.")]
            [Min(0f)] public float WatchRadiusMeters = 3f;
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

        [Header("The chain (one law for every rung of this mark - rule 6)")]
        [Tooltip("Spring stiffness in 1/s^2, how hard her mooring hauls her home. The undamped " +
                 "period is 2*pi/sqrt(k): 4 gives about three seconds, which is a buoy rather " +
                 "than a bath toy.")]
        [Min(0f)] public float MooringSpringPerSecondSquared = 4f;

        [Tooltip("Damping as a fraction of CRITICAL. Below 1 she rebounds once or twice and " +
                 "settles; 1 walks her home with no overshoot at all; above 1 she oozes. A " +
                 "ratio rather than a raw coefficient, because a damping number means nothing " +
                 "without the spring beside it.")]
        [Range(0f, 2f)] public float MooringDampingRatio = 0.5f;

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
