using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Data definition for one <b>shoreline plant species</b> — the sprites its five baked tide states
    /// wear, the ladder those states were baked at, and the two numbers the tide response turns on.
    /// Content is data, not code (ADR 0003): a species becomes placeable by authoring one of these
    /// assets, with no code change.
    ///
    /// <para><b>⭐ WHY THE LADDER IS ON THE ASSET.</b> <c>ShorePlantCatalog</c> owns the contract, but
    /// it is <c>#if UNITY_EDITOR</c> — the runtime cannot read it, and should not: a build must not
    /// depend on a JSON file in <c>Art/Sprites/</c> parsing correctly at load. So the contract's zone
    /// base and tide steps are BAKED ONTO THIS ASSET by <c>ShorePlantDefBuilder</c>, in the one form
    /// the runtime actually needs — <see cref="TideLadderOverM"/>, the water-over-own-ground each
    /// column was rendered at. The builder recomputes it from the contract, so it cannot be
    /// hand-drifted into disagreeing with the pixels.</para>
    ///
    /// <para><b>Sway row 0, deliberately.</b> The kit bakes four sway frames per state and this Def
    /// carries the RESTING row. The rig's sway curve is <c>sin(frame/4 × 2π)</c>, so rows 1 and 3 are
    /// the two extremes of a lean and row 2 is neutral again — a static plant has to be one standing
    /// still, not one frozen mid-gust. Same call <c>StPetersWoodsPlanter.RestSwayRow</c> makes for
    /// shrubs. The other three rows are baked and on disk; they are what a later wind pass reads.</para>
    ///
    /// <para><b>Decor tier, never saved (rule 5).</b> A plant's visual state is recomputed every slow
    /// tick from the deterministic water level and the authored seabed; nothing about it enters the
    /// save or the sim.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Shore Plant", fileName = "ShorePlant")]
    public class ShorePlantDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): type.snake_case, e.g. shoreplant.sugar_kelp.")]
        public string Id = "shoreplant.sugar_kelp";

        [Tooltip("The kit's own species key — SugarKelp, KnottedWrack, MarramGrass… Must match the " +
                 "contract, and is what the Def builder matches on.")]
        public string SpeciesKey = "SugarKelp";

        [Tooltip("Which rung of the tidal staircase this species belongs to: fringe (subtidal), mid " +
                 "(intertidal), low marsh, high marsh, upland. The scatter pass places by this.")]
        public string Zone = "fringe";

        [Header("The two numbers the tide response turns on")]
        [Tooltip("Nominal ground height of the species' zone, metres above chart datum — what the " +
                 "states were BAKED at. The live plant stands on the painted seabed instead; this is " +
                 "only what converts the baked ladder into water-over-own-ground.")]
        public float ZoneBaseM = 0.15f;

        [Tooltip("TRUE standing height in metres (32 px = 1 m). This is what decides SUBMERGED: a " +
                 "2.75 m kelp under 1 m of water is lying across the surface, not under it.")]
        public float StandingHeightM = 2.75f;

        [Tooltip("Algae collapse when the water leaves them — they concertina onto the substrate and " +
                 "puddle downslope. Marsh and upland plants stand up dry. Carried from the kit; the " +
                 "states already show it, this is here so a scatter can reason about it.")]
        public bool Algae;

        [Header("The five baked tide states, low → high (sway row 0, resting)")]
        [Tooltip("Sprites for the low/ebb/half/flood/high columns of the tide-axis sheet. All five " +
                 "share ONE ground-contact pivot — that is what makes a state swap anchored instead " +
                 "of a 1 px hop the moment the water crosses a threshold.")]
        public Sprite[] TideSprites = new Sprite[ShorePlantTideMath.TideStates];

        [Tooltip("Water height over this species' OWN ground, in metres, that each column above was " +
                 "baked at. Written by the Def builder from the contract — do not hand-edit; a ladder " +
                 "that disagrees with the pixels makes plants wear the wrong tide.")]
        public float[] TideLadderOverM = new float[ShorePlantTideMath.TideStates];

        [Header("Rendering")]
        [Tooltip("Sorting order while SUBMERGED. Must sit BELOW the Sea plane (−5) and ABOVE the " +
                 "painted seabed (−21…−18): that gap is where the water's own shallow transparency " +
                 "reveals the bed. −8 leaves room either side.")]
        public int SubmergedSortingOrder = -8;

        [Tooltip("Pre-sort order while EMERGENT. YSortSprite OWNS the order above water — this is " +
                 "only the value before it first runs.")]
        public int EmergentSortingOrder = 3;

        /// <summary>The sprite for a baked column, or null when the Def is half-built.</summary>
        public Sprite SpriteFor(int state) =>
            TideSprites != null && state >= 0 && state < TideSprites.Length ? TideSprites[state] : null;

        /// <summary>True when this Def carries everything the runtime view needs.</summary>
        public bool IsComplete()
        {
            if (TideSprites == null || TideSprites.Length != ShorePlantTideMath.TideStates) return false;
            if (TideLadderOverM == null || TideLadderOverM.Length != ShorePlantTideMath.TideStates) return false;
            for (int i = 0; i < TideSprites.Length; i++) if (TideSprites[i] == null) return false;
            return true;
        }
    }
}
