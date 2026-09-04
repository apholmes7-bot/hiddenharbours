using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Who is behind which wheel</b> — the registry that lets somebody who is NOT the player occupy an
    /// <see cref="IDriveSeat"/> (ADR 0035, extended for NPC drivers 2026-09-04).
    ///
    /// <para><b>Why a registry and not a flag on the seat.</b> This is the shape
    /// <see cref="HelmSlot"/> took when the intro skipper needed a boat's wheel without going through the
    /// player's control switcher: occupancy is a fact about the WORLD, asked by several parties who have
    /// no business naming each other. The village's driver claims it; <c>VehicleDoor</c> asks it before
    /// offering "Climb in"; <c>ControlSwitcher</c> asks it before honouring a press. A bool on the seat
    /// would work for exactly as long as there was one claimant.</para>
    ///
    /// <para><b>The player is deliberately NOT a claimant.</b> The switcher already owns "the player is
    /// driving" and storing it twice is how two answers start disagreeing. This registry answers one
    /// question only — <i>is somebody else already at this wheel?</i> — and a truck the player is driving
    /// is simply not in it.</para>
    ///
    /// <para><b>Scene-scoped, claim-on-enable / release-on-disable</b>, the same contract
    /// <see cref="Interactables"/> and <see cref="MooringCleats"/> keep, and with the same property: an
    /// empty registry is a valid answer and is what makes every machine that predates NPC drivers behave
    /// exactly as it did.</para>
    ///
    /// <para>⚠️ <b>A seat is a <c>UnityEngine.Object</c> in practice, so it can be FAKE-null</b> — see the
    /// warning on <see cref="IDriveSeat"/>. Entries whose seat has died are dropped on the next read
    /// rather than trusted: a truck destroyed under her driver would otherwise hold her wheel for ever
    /// and nothing would ever be able to claim it again.</para>
    /// </summary>
    public static class DriveSeats
    {
        private readonly struct Claim
        {
            public readonly IDriveSeat Seat;
            public readonly object Driver;
            public Claim(IDriveSeat seat, object driver) { Seat = seat; Driver = driver; }
        }

        // A village has a handful of moving machines at a time.
        private static readonly List<Claim> Claims = new List<Claim>(4);

        /// <summary>How many wheels are held by somebody other than the player.</summary>
        public static int Count { get { Prune(); return Claims.Count; } }

        /// <summary>
        /// Take a wheel on behalf of <paramref name="driver"/> — an NPC, a script, anything that is not
        /// the player. Idempotent for the same pair; re-claiming a seat somebody else holds is REFUSED
        /// (false) rather than silently stealing it, because two drivers in one cab is the kind of thing
        /// that shows up as a truck driving two routes at once and reads as a physics bug.
        /// </summary>
        public static bool TryClaim(IDriveSeat seat, object driver)
        {
            if (seat == null || !seat.IsAlive || driver == null) return false;
            Prune();
            for (int i = 0; i < Claims.Count; i++)
            {
                if (!ReferenceEquals(Claims[i].Seat, seat)) continue;
                return ReferenceEquals(Claims[i].Driver, driver);
            }
            Claims.Add(new Claim(seat, driver));
            return true;
        }

        /// <summary>Give the wheel up. Releasing a seat nobody holds is a no-op — a teardown must never
        /// have to check first.</summary>
        public static void Release(IDriveSeat seat)
        {
            if (seat == null) return;
            for (int i = Claims.Count - 1; i >= 0; i--)
                if (ReferenceEquals(Claims[i].Seat, seat)) Claims.RemoveAt(i);
        }

        /// <summary>Release every wheel <paramref name="driver"/> holds — what a driver's own
        /// <c>OnDisable</c> calls, so a machine is never left claimed by a component that has gone.</summary>
        public static void ReleaseAllFor(object driver)
        {
            if (driver == null) return;
            for (int i = Claims.Count - 1; i >= 0; i--)
                if (ReferenceEquals(Claims[i].Driver, driver)) Claims.RemoveAt(i);
        }

        /// <summary>Is somebody other than the player at this wheel?</summary>
        public static bool IsOccupied(IDriveSeat seat)
        {
            if (seat == null) return false;
            Prune();
            for (int i = 0; i < Claims.Count; i++)
                if (ReferenceEquals(Claims[i].Seat, seat)) return true;
            return false;
        }

        /// <summary>Who is at this wheel, or null. Held as <c>object</c> so Core need not name whatever
        /// module the driver lives in (rule 4) — the caller knows what it put there.</summary>
        public static object DriverOf(IDriveSeat seat)
        {
            if (seat == null) return null;
            Prune();
            for (int i = 0; i < Claims.Count; i++)
                if (ReferenceEquals(Claims[i].Seat, seat)) return Claims[i].Driver;
            return null;
        }

        /// <summary>Empty the registry — a test fixture's, and a new game's, clean slate.</summary>
        public static void Reset() => Claims.Clear();

        /// <summary>Drop claims on seats that have been destroyed. See the fake-null warning on the class:
        /// the interface-typed <c>==</c> sails straight past a destroyed component, so the honest test is
        /// the seat's own <see cref="IDriveSeat.IsAlive"/>.</summary>
        private static void Prune()
        {
            for (int i = Claims.Count - 1; i >= 0; i--)
            {
                IDriveSeat seat = Claims[i].Seat;
                if (seat == null || !seat.IsAlive) Claims.RemoveAt(i);
            }
        }
    }
}
