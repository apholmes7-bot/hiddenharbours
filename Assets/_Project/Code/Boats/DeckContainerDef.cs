using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// Data definition for a DECK CATCH CONTAINER — the diegetic "how full am I?" read that sits on the
    /// boat's deck and visibly fills as the hold fills (owner canon: FILL-STATE SPRITES — you read
    /// roughly how full a tray/tote is by LOOKING at it; no HUD, no counter — the tray IS the readout).
    ///
    /// <para>The container LADDER is data (ADR 0003): small boats carry a fish TRAY
    /// (<c>container.fish_tray</c>); big hulls take the real-world North Atlantic blue fish TOTES in M2 —
    /// a bigger boat gets a bigger container by referencing a DIFFERENT one of these assets from its
    /// <see cref="BoatHullDef.DeckContainer"/>, never by code. This asset is the first rung; totes drop
    /// in later as new assets.</para>
    ///
    /// <para>Rendered by <see cref="DeckContainerPresenter"/>, a pure READ of the hold — it owns no
    /// state, gates nothing, and saves nothing.</para>
    ///
    /// Create via Assets &gt; Create &gt; Hidden Harbours &gt; Deck Container, save in Data/Boats/Containers.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Deck Container", fileName = "DeckContainer")]
    public class DeckContainerDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id (append-only), e.g. \"container.fish_tray\" now, \"container.blue_tote\" in M2.")]
        public string Id = "container.fish_tray";

        [Tooltip("Player-facing name, e.g. \"Fish tray\". Flavor for future tooltips/merchant talk.")]
        public string DisplayName = "Fish tray";

        [Header("Fill states (owner canon, 'important': the sprite VISIBLY changes with contents)")]
        [Tooltip("The painted fill states, ordered EMPTY first → BRIM-FULL last (any count ≥ 2). The " +
                 "presenter maps hold fullness onto them: the FIRST sprite is pinned to an empty hold, " +
                 "the LAST to a full hold, and the rest spread evenly across the partial range. Leave " +
                 "EMPTY to use the code-built greybox silhouettes (4 states: empty / low / half / brim) " +
                 "until the painted art lands — the art drops in here with ZERO code change.")]
        public Sprite[] FillSprites;
    }
}
