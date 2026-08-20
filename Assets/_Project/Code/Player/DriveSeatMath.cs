using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>WHERE A SEATED CHARACTER IS DRAWN ON A MACHINE</b> — the one piece of arithmetic between the
    /// rig-6.5 <c>drive</c> sheets and a truck standing in the world.
    ///
    /// <para><b>The problem it solves.</b> Every clip above the off-deck four is something you do while
    /// STANDING, so the character's own ground pivot places it and there is nothing to compute. The
    /// drive sheets are not: they are baked with the fisher SITTING, on a seat
    /// <c>CharacterOffDeckMountsDef.DriveSeatZ</c> (0.40 m) above the pivot, and the machine she is
    /// being drawn on has a seat of her own at whatever height her rig published. Drop the cell at her
    /// root and the fisher sits 0.40 m off the ground with the seat nowhere near her.</para>
    ///
    /// <para><b>The rule: align the SEATS, not the floors.</b> The sidecar's note for the sleep sheets
    /// says it for both — <i>"a bed of another height needs the sprite lifted by the difference, not a
    /// re-bake"</i> — and the seat is the contact that matters: it is the only place the figure and the
    /// machine actually touch, and a bum floating over a cushion reads instantly. Everything else about
    /// the pose then follows within half a pixel on the machine this shipped for; see
    /// <see cref="LiftMeters"/>.</para>
    ///
    /// <para><b>A HEIGHT is not a place</b> (ADR 0034's neighbouring trap). The seat's ground point
    /// swings as the machine turns and is resolved on her transform; the seat's height never turns at
    /// all and is a screen offset, <c>× cos 40°</c> like every other height in the game
    /// (<see cref="SpriteLightMath.HeightScale"/>). Keeping the two apart is why this takes the ground
    /// point and the height as separate arguments rather than one tidy-looking Vector3.</para>
    ///
    /// <para><b>Rules.</b> Pure, static, allocation-free and deterministic, like <see cref="CarryMath"/>
    /// beside it — so the placement is EditMode-testable with no scene, no machine and no art. Visual
    /// only (rule 5): nothing here feeds the sim.</para>
    /// </summary>
    public static class DriveSeatMath
    {
        /// <summary>
        /// How far UP THE SCREEN, in world units, the drive cell has to be lifted so its baked seat
        /// lands on the machine's — the difference between the two seat heights, drawn at the shared
        /// ¾ camera's height scale.
        ///
        /// <para><b>Signed, and the sign is the point.</b> A machine whose seat sits higher than the
        /// bake lifts the figure; a lower one drops her. The Otter is the first case in the game and she
        /// is the first kind: her front bench is 0.76 m up against the pose's 0.40, so the fisher rises
        /// 0.36 m of height — 0.28 world units, about 9 px at PPU 32. Left out, she would be drawn
        /// sitting in her cockpit floor.</para>
        ///
        /// <para><b>What the residual is, and why it is allowed to exist.</b> Aligning the seats does
        /// not also align the hands, because the pose was baked at ONE seat-to-wheel geometry and every
        /// machine has her own. It is worth stating the size of that on the machine this shipped for:
        /// the pose puts the grips 0.315 m forward of the seat and 0.255 m above it, and the Otter's
        /// centred handlebar is 0.30 m forward and 0.27 m above her bench — 0.015 m out in each, which
        /// at 32 px/m is HALF A PIXEL. The rig can be re-baked per machine if one ever needs it
        /// (<c>OffDeck_mounts.json</c>: <i>"opts.seatZ/wheelZ/wheelY; soft-clamped"</i>); nothing needs
        /// it yet.</para>
        /// </summary>
        /// <param name="machineSeatHeightMeters">The machine's own seat, metres above her ground plane
        /// (<c>IDriveSeat.DriverSeatHeightMeters</c>, off her published art).</param>
        /// <param name="bakedSeatHeightMeters">The seat the CLIP was baked on
        /// (<c>CharacterOffDeckMountsDef.DriveSeatZ</c>). Never a constant here — a re-bake changes it
        /// and this must follow the asset, not a number typed into code (rule 6).</param>
        public static float LiftMeters(float machineSeatHeightMeters, float bakedSeatHeightMeters)
            => (machineSeatHeightMeters - bakedSeatHeightMeters) * SpriteLightMath.HeightScale;

        /// <summary>
        /// Where the drive cell's PIVOT goes in the world: the seat's ground point, raised by
        /// <see cref="LiftMeters"/>.
        ///
        /// <para><paramref name="depthZ"/> is passed through untouched and is the character's own —
        /// the iso sort band lives on z (ADR 0032) and a seating that wrote it would quietly re-layer
        /// the fisher. It is the same reason <c>PlayerSleepPresenter</c> carries the sleeper's z
        /// across the move to the mattress.</para>
        /// </summary>
        public static Vector3 SeatPivotWorld(Vector2 seatGroundWorld, float machineSeatHeightMeters,
                                             float bakedSeatHeightMeters, float depthZ)
            => new Vector3(seatGroundWorld.x,
                           seatGroundWorld.y + LiftMeters(machineSeatHeightMeters, bakedSeatHeightMeters),
                           depthZ);
    }
}
