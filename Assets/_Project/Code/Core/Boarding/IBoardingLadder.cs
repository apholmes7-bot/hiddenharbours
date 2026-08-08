using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A fixed ladder down a wharf face</b> — the way aboard once the tide has opened a gap the boarding
    /// step cannot cross. Published to the boarding gameplay as a Core contract so the player module reads
    /// it without ever referencing the World component that supplies it (rule 4), exactly as
    /// <see cref="IMooringCleat"/> and <see cref="IStandableSurface"/> do.
    ///
    /// <para><b>Its top does NOT move with the tide, and that is the whole mechanic.</b> A ladder is bolted
    /// to a wall at a fixed height above chart datum; the boat at its foot floats. The gap between them is
    /// therefore tide-driven — see <see cref="LadderBoardingMath.VerticalGap"/> — so author
    /// <see cref="TopElevationMeters"/> from the deck the ladder is bolted to (the wharf builders MEASURE
    /// it off the terrain, never a literal) and the tide does the rest.</para>
    ///
    /// <para><b>Placement is expected to move; consumers are not.</b> These are placed today by the region
    /// builders from the same fittings table that positions the ladder sprite. When the wharf rig grows
    /// real mount symbols, only the PLACEMENT changes: this interface and everything reading it stay as
    /// they are.</para>
    /// </summary>
    public interface IBoardingLadder
    {
        /// <summary>Stable id of this ladder (e.g. <c>wharf.nine_mile_creek.ladder_0</c>) — diagnostics
        /// only. Never a lookup key: the registry is walked, not hashed.</summary>
        string Id { get; }

        /// <summary>Where the ladder meets the wall, in world metres (plan). The climber does not stand
        /// HERE — they sit <see cref="StandoffMetres"/> out along <see cref="FaceHeadingDegrees"/>.</summary>
        Vector2 WorldPosition { get; }

        /// <summary>The height of the ladder's TOP above chart datum (m) — the deck it serves. Fixed: the
        /// wall does not float.</summary>
        float TopElevationMeters { get; }

        /// <summary>Compass heading the ladder's face looks along (0 = North, 90 = East, clockwise) — out
        /// from the wall, over the water. A climber faces the reciprocal
        /// (<see cref="LadderBoardingMath.ClimberHeadingFor"/>), back to the sea.</summary>
        float FaceHeadingDegrees { get; }

        /// <summary>Rung spacing (m) — the tread of the descent stair. From the kit's own fitting table
        /// (<c>WharfIso.FIT.ladder.rung</c>), never guessed, because the clip's foot placement was
        /// authored against it.</summary>
        float RungMetres { get; }

        /// <summary>How far the climber's pivot sits off the ladder plane (m) — the rig's MEASURED
        /// <c>ladderMount().standoff</c>. Seating the sprite on the ladder line instead puts its hands
        /// inside the wall.</summary>
        float StandoffMetres { get; }
    }
}
