using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Which side of the water a cleat sits on. A mooring line always runs between the two —
    /// you throw from the boat to the shore, or from the shore to the boat, and never boat-to-boat
    /// (rafting is explicitly out of M2-38's scope).</summary>
    public enum CleatSide
    {
        /// <summary>Fixed to the land: a wharf bollard, a quay cleat, a ring on a piling. Its elevation
        /// does NOT move with the tide — that asymmetry is the whole mechanic.</summary>
        Shore = 0,
        /// <summary>A fitting on a hull: the rig's own <c>CLEATS</c>. It rides the water, so its
        /// elevation moves with every tide.</summary>
        Boat = 1,
    }

    /// <summary>
    /// <b>A tie-off point a mooring line can be made fast to</b> — the Core contract behind M2-38's ropes.
    /// Boats publish their rig's <c>CLEATS</c> through it; wharves publish their bollards through it; the
    /// rope gameplay reads only this, so neither side ever references the other (rule 4).
    ///
    /// <para><b>Why an interface and not a struct of positions.</b> A boat's cleat MOVES — every tick, with
    /// her heading, her rock, and the tide. A wharf's does not. Both are read fresh at the moment the line
    /// is worked, exactly as <see cref="IMooringAnchor"/>'s live <c>Position</c> read already does for the
    /// held/rooted rope. Baking positions would re-open the hand-transcribed-constant failure the deck
    /// sidecars were built to close.</para>
    ///
    /// <para><b><see cref="ElevationMeters"/> is the load-bearing one.</b> It is metres above chart datum —
    /// the SAME frame <see cref="IEnvironmentService.WaterLevelAt"/>, <c>ITidalTerrain.ElevationAt</c> and
    /// <c>StandablePlatform.DeckElevation</c> all speak. A shore cleat's is a constant (the deck it is
    /// bolted to). A boat cleat's is <c>waterLevel + heightAboveWaterline</c> — it rides the tide, because
    /// the hull floats. The gap between the two is what a falling tide eats out of a short line, and
    /// <see cref="MooringLineMath.HorizontalReach"/> is where that becomes gameplay.</para>
    ///
    /// <para><b>Registration is scene-scoped</b> (<see cref="MooringCleats"/>), the same self-installing
    /// pattern as <see cref="IStandableSurface"/>: a component registers on enable and relinquishes on
    /// disable, so a region teardown leaves an empty registry and "no cleats" simply means nothing can be
    /// made fast here.</para>
    /// </summary>
    public interface IMooringCleat
    {
        /// <summary>Stable id for diagnostics and save/restore of a made-fast line
        /// (<c>wharf.st_peters.bollard_3</c>, <c>boat.dory.bow_1</c>). Never a lookup key into a table —
        /// the registry is walked, not hashed.</summary>
        string Id { get; }

        /// <summary>Shore or boat. A line is only ever made fast between opposing sides.</summary>
        CleatSide Side { get; }

        /// <summary>Where the fitting is right now, in world metres. Read LIVE — a boat's cleat swings
        /// with her heading and drifts with her hull.</summary>
        Vector2 WorldPosition { get; }

        /// <summary>The fitting's height in metres above chart datum. Fixed for a shore cleat; for a boat
        /// cleat it rides <see cref="IEnvironmentService.WaterLevelAt"/>. See the interface doc.</summary>
        float ElevationMeters { get; }
    }
}
