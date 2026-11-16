using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>WHERE AND WHEN THE PLAYER LAST SLEPT</b> (owner ruling 2026-11-15: "Reloading restarts the day
    /// where you last slept. A sleep is a save."). The whole answer to "where do I wake on a reload", as
    /// one value: the <see cref="RestAnchor"/> (region, region-local position, storey) the player turned in
    /// on, WHICH bed or bunk it was, the game time they woke at, and, when the bed was a bunk aboard, the
    /// boat's own pose at that moment.
    ///
    /// <para><b>Region-local coordinates, said once.</b> Every position here is in the frame of the
    /// region scene it names (<see cref="RestAnchor.Region"/> for the player, <see cref="BoatRegion"/>
    /// for the boat): each region is its own scene with its own origin (ADR 0004), which is the frame
    /// <c>GameServices.PlayerTransform.position</c> reads in today. A shared world frame (a per-region
    /// world offset) maps these with one addition per region and needs NO new field here: that is why
    /// the region id always travels with the position and never on its own.</para>
    ///
    /// <para><b>The boat's pose is recorded, not yet honoured.</b> Waking aboard on a reload needs the
    /// boat placed at <see cref="BoatPosition"/> and the player boarded into her cabin, which is the
    /// travel rig's and the control switcher's work (App), not the save's. Until that lands
    /// <see cref="RestWakeRestorer"/> declines an aboard place out loud and the player wakes at the
    /// region's spawn, exactly as a bunk's rest did before; the file already holds everything the
    /// honouring step needs, so it is a read-side change and not another migration.</para>
    ///
    /// <para><see cref="None"/> (the default) is "has never slept", the state of a new game and of every
    /// save written before v13. Read and written only through <see cref="RestLocker"/>.</para>
    /// </summary>
    public readonly struct SleepPlace
    {
        /// <summary>Where the player stood to turn in: region, region-local position and storey.</summary>
        public readonly RestAnchor Anchor;

        /// <summary>The stable fixture id of the bed or bunk (<c>fixture.interior.bed</c>,
        /// <c>fixture.boat.bunk</c>). Empty when the caller could not name it. Informational: nothing
        /// is refused for an id no fixture claims.</summary>
        public readonly string BedId;

        /// <summary>The game time the sleep ENDED (<see cref="IGameClock.TotalSeconds"/>): the instant a
        /// reload restarts the day from. Equal to the save's own <see cref="SaveData.GameTimeSeconds"/>
        /// when the sleep's write was the last one.</summary>
        public readonly double WokeAtSeconds;

        /// <summary>True when the bed was a bunk aboard a boat. The four boat fields below mean
        /// something only then.</summary>
        public readonly bool Aboard;

        /// <summary>The hull id of the boat slept aboard (<c>boat.cape_islander</c>).</summary>
        public readonly string BoatHullId;

        /// <summary>The region the boat lay in. Always the anchor's region in play today (the player
        /// is aboard her); carried separately so the honouring step never has to assume it.</summary>
        public readonly string BoatRegion;

        /// <summary>The boat root's position in <see cref="BoatRegion"/>'s frame, world metres.</summary>
        public readonly Vector2 BoatPosition;

        /// <summary>The boat root's rotation about Z in degrees, exactly as its transform held it
        /// (<c>transform.eulerAngles.z</c>), so writing it back is the inverse of reading it.</summary>
        public readonly float BoatRotationDegrees;

        /// <summary>A place ashore (a bed).</summary>
        public SleepPlace(in RestAnchor anchor, string bedId, double wokeAtSeconds)
            : this(anchor, bedId, wokeAtSeconds, false, "", "", Vector2.zero, 0f) { }

        /// <summary>A place in full, aboard or ashore.</summary>
        public SleepPlace(in RestAnchor anchor, string bedId, double wokeAtSeconds, bool aboard,
                          string boatHullId, string boatRegion, Vector2 boatPosition, float boatRotationDegrees)
        {
            Anchor = anchor;
            BedId = bedId ?? "";
            WokeAtSeconds = double.IsNaN(wokeAtSeconds) || wokeAtSeconds < 0d ? 0d : wokeAtSeconds;
            Aboard = aboard;
            BoatHullId = aboard ? boatHullId ?? "" : "";
            BoatRegion = aboard ? boatRegion ?? "" : "";
            BoatPosition = aboard ? boatPosition : Vector2.zero;
            BoatRotationDegrees = aboard ? boatRotationDegrees : 0f;
        }

        /// <summary>"Has never slept." The default value.</summary>
        public static SleepPlace None => default;

        /// <summary>Is there a place here at all: the anchor's own test (region set, position a real
        /// number).</summary>
        public bool IsSet => Anchor.IsSet;

        public override string ToString() =>
            !IsSet ? "(never slept)"
                   : Aboard ? $"{Anchor} aboard {BoatHullId} at ({BoatPosition.x:0.##}, {BoatPosition.y:0.##})"
                            : $"{Anchor} in {(string.IsNullOrEmpty(BedId) ? "an unnamed bed" : BedId)}";
    }
}
