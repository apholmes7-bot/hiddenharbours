using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>WHERE THE PLAYER WENT TO SLEEP</b> — the three facts that together name a place a person can
    /// wake up standing in: which region, which storey of the building, and where in it. The runtime
    /// form of <see cref="SaveData.RestRegion"/> + <see cref="SaveData.RestPosX"/>/<c>Y</c> +
    /// <see cref="SaveData.RestLevel"/>, handed out and taken in by <see cref="RestLocker"/>.
    ///
    /// <para><b>Why all three travel together, and never a bare position.</b> A world position is not a
    /// place on its own — this file's siblings already say so once (<c>NavWaypointDto</c> is
    /// region-stamped because "a world position only means anything inside its own region's frame"),
    /// and ADR 0036 made it true a second time: a second storey is a LAYER over the SAME footprint, so
    /// one (x, y) names two different rooms in a house with an upstairs. The one authored player bed in
    /// the game is upstairs at Ginny's, directly above her front room. An anchor that carried only the
    /// position would wake the player in her kitchen and look exactly like the feature working — which
    /// is the failure mode a struct exists to make unrepresentable.</para>
    ///
    /// <para><b><see cref="None"/> is a real answer, not a missing one.</b> It means "has never turned
    /// in", which is what a new game is, what every save written before v13 is, and what a save whose
    /// anchor could not be read is. Under it the player wakes at the authored spawn — the behaviour the
    /// game had before any of this existed — so there is no null to guard at the point of waking.</para>
    /// </summary>
    public readonly struct RestAnchor
    {
        /// <summary>Stable region id (e.g. <c>region.st_peters</c>). Empty for <see cref="None"/>, and
        /// emptiness is the ONE test for "is there an anchor" — see <see cref="IsSet"/>.</summary>
        public readonly string Region;

        /// <summary>The spot in that region the player stood on to turn in, world metres.</summary>
        public readonly Vector2 Position;

        /// <summary>Which storey that spot is on: 0 the ground floor and everywhere outdoors, 1 the floor
        /// above. Matches <c>BuildingInterior.Level</c>, whose ladder this rides.</summary>
        public readonly int Level;

        public RestAnchor(string region, Vector2 position, int level)
        {
            Region = string.IsNullOrEmpty(region) ? string.Empty : region;
            Position = position;
            Level = level;
        }

        /// <summary>"The player has never turned in." The default value, deliberately — so a
        /// <c>default(RestAnchor)</c> and an unset save agree without anyone having to remember to
        /// construct this.</summary>
        public static RestAnchor None => default;

        /// <summary>
        /// Is there an anchor here to honour? Region non-empty AND the position a real number — the two
        /// conditions under which a caller may act on it without a further check.
        ///
        /// <para><b>The storey is deliberately NOT validated here.</b> Whether a building still has an
        /// upstairs is a content question that can be answered differently by two builds of the same
        /// game, and this struct has no way to ask it. It is settled where the storey is applied, by
        /// whoever owns the building — the same division the outfit locker keeps with the wardrobe.</para>
        /// </summary>
        public bool IsSet => !string.IsNullOrEmpty(Region)
                          && !float.IsNaN(Position.x) && !float.IsInfinity(Position.x)
                          && !float.IsNaN(Position.y) && !float.IsInfinity(Position.y);

        /// <summary>Is this anchor in <paramref name="regionId"/>? Ordinal, and false for an unset anchor
        /// or an empty question — the one place the comparison is written, so the writer and the reader
        /// can never disagree about what "the same region" means.</summary>
        public bool IsIn(string regionId) =>
            IsSet && !string.IsNullOrEmpty(regionId)
                  && string.Equals(Region, regionId, System.StringComparison.Ordinal);

        public override string ToString() =>
            IsSet ? $"{Region} level {Level} at ({Position.x:0.##}, {Position.y:0.##})" : "(no rest anchor)";
    }
}
