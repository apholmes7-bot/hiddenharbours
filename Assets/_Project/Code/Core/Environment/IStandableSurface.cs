using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// A <b>standable structure</b> — something BUILT that the on-foot fisher stands on, whose standing
    /// height is its own rather than the seabed's. The seam the project was missing: <see cref="ITidalTerrain"/>
    /// publishes the authored GROUND, and every on-foot read composed walkability straight out of
    /// (ground vs water level), so a wharf deck standing over a dredged slip was invisible to the
    /// simulation — the St Peters pier is built over a −1.0 m berth bed in a tide-gated region, so the sim
    /// saw 4.5 m of water at the ratified disembark point for most of every tide.
    ///
    /// <para><b>The one rule.</b> Where a registered surface is present, the on-foot <em>standing
    /// elevation</em> is the surface's deck rather than the ground beneath it
    /// (<see cref="StandableSurfaces.StandingElevation"/>). Everything downstream is unchanged: the depth
    /// is still <c>waterLevel − standingElevation</c> via <see cref="TidalExposure.WaterDepth"/>, so the
    /// walk gate, the sprint gate, the wade bands and the body's waterline all keep reading ONE number and
    /// can never disagree (the render==sim discipline of ADR 0010). A deck authored clear of the highest
    /// water is therefore dry at every tide — walkable regardless of the tide, which is the point — while a
    /// deck the sea genuinely reaches still reads honestly instead of lying.</para>
    ///
    /// <para><b>ON FOOT ONLY.</b> This never touches the water render, the boat-cross/grounding depth
    /// (<c>BoatCrossing</c>), the clam-baring or the seabed bake: a pier does not make the slip beneath it
    /// shallow, and a hull must still float in the water the terrain authored. Only the reads about where a
    /// PERSON'S feet are consult this.</para>
    ///
    /// <para><b>Why a query and not a <see cref="Rect"/> property.</b> Today's only registrant is a static
    /// rectangle (the wharf deck), but the M2 walkable-deck vision needs boat decks and washboards — which
    /// MOVE and ROTATE under the player (design: <c>boat-deck-cleats-interact-vision</c>). Asking the
    /// surface "am I over you, and if so how high is your deck?" makes a moving registrant an
    /// implementation (transform the position into hull space) rather than a change to this contract. The
    /// two answers come back from ONE call for the same reason: a footprint test and a height lookup that
    /// could be asked separately are two things that can disagree.</para>
    ///
    /// <para><b>Deterministic, never saved.</b> Like <see cref="ITidalTerrain"/>, a surface is authored
    /// geometry: a pure function of world position with no RNG and nothing serialized at runtime
    /// (CLAUDE.md rule 5). Registrants are scene-scoped and register/unregister themselves through
    /// <see cref="StandableSurfaces"/> — pull, not push; nothing is precomputed per tile per tick.</para>
    /// </summary>
    public interface IStandableSurface
    {
        /// <summary>Stable id of the structure (e.g. <c>"wharf.st_peters"</c>) — for diagnostics and for a
        /// consumer that needs to know WHICH surface it is standing on. Never used as a lookup key.</summary>
        string Id { get; }

        /// <summary>
        /// Is <paramref name="worldPos"/> over this surface right now, and if so what is the surface's
        /// <paramref name="deckElevation"/> there — the standing height in <b>metres above chart datum</b>,
        /// the same frame <see cref="ITidalTerrain.ElevationAt"/> and the tide model use.
        /// </summary>
        /// <param name="worldPos">World-space XY position to test (world units).</param>
        /// <param name="deckElevation">The deck height at that position (m above datum); undefined when
        /// the method returns <c>false</c>.</param>
        /// <returns><c>true</c> when the position is within this surface's footprint.</returns>
        bool TryGetDeckElevation(Vector2 worldPos, out float deckElevation);
    }
}
