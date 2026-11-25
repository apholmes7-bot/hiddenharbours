using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Which owned boat</b> (kitchens slice 1, so a cooler belongs to ONE boat, not to a hull). Main's
    /// fleet identifies an owned boat by its stable hull id: <see cref="SaveData.OwnedBoats"/>,
    /// <see cref="SaveData.ActiveHullId"/>, the purchase signal and every per-boat record (fuel,
    /// instruments, sounder) are keyed by it, and a purchase adds the id once, so today a player owns at
    /// most one boat of a hull. This names that rule so a per-boat record can key by the INSTANCE now:
    ///
    /// <list type="bullet">
    /// <item><description>The first (today the only) boat of a hull has the owned-boat id
    /// <see cref="FirstOf"/> = the hull id itself, so it lines up with every record main already
    /// keeps.</description></item>
    /// <item><description>A second boat of the same hull, when a fleet can own one, is minted
    /// <c>&lt;hull id&gt;#2</c>, then <c>#3</c> (<see cref="Mint"/>): the lowest suffix no owned boat holds.
    /// The fleet records the minted id with the boat; <see cref="HullOf"/> reads the hull back.</description></item>
    /// </list>
    ///
    /// An owned-boat id never contains <see cref="KeySeparator"/>, so a key built as
    /// <c>&lt;owned boat id&gt;/&lt;rest&gt;</c> can be matched by prefix with no collision between hull ids
    /// that nest (<c>boat.cape</c> and <c>boat.cape.x</c>).
    /// </summary>
    public static class OwnedBoatId
    {
        /// <summary>Between the hull id and the instance number of a second or later boat of one hull.</summary>
        public const char InstanceSeparator = '#';

        /// <summary>Ends the owned-boat part of a key built on it; never part of an owned-boat id.</summary>
        public const char KeySeparator = '/';

        /// <summary>The instance number a minted id starts at (the first boat carries no number).</summary>
        public const int FirstMintedInstance = 2;

        /// <summary>The owned-boat id of the first boat of <paramref name="hullId"/>: the hull id itself.
        /// Empty for no hull.</summary>
        public static string FirstOf(string hullId) => IsValid(hullId) ? hullId : "";

        /// <summary>
        /// A fresh owned-boat id for another boat of <paramref name="hullId"/>, unique among
        /// <paramref name="taken"/> (the ids every owned boat already holds): the hull id when no owned boat
        /// holds it, else <c>&lt;hull id&gt;#n</c> for the lowest n from <see cref="FirstMintedInstance"/>
        /// that is free. Deterministic (no clock, no randomness). Empty for no hull.
        /// </summary>
        public static string Mint(string hullId, IEnumerable<string> taken)
        {
            if (!IsValid(hullId)) return "";
            var held = new HashSet<string>(StringComparer.Ordinal);
            if (taken != null)
                foreach (string id in taken)
                    if (!string.IsNullOrEmpty(id)) held.Add(id);
            if (!held.Contains(hullId)) return hullId;
            for (int n = FirstMintedInstance; ; n++)
            {
                string id = hullId + InstanceSeparator + n;
                if (!held.Contains(id)) return id;
            }
        }

        /// <summary>The hull id an owned-boat id is a boat of (the id itself for a first boat).</summary>
        public static string HullOf(string ownedBoatId)
        {
            if (string.IsNullOrEmpty(ownedBoatId)) return "";
            int cut = ownedBoatId.LastIndexOf(InstanceSeparator);
            if (cut <= 0 || cut == ownedBoatId.Length - 1) return ownedBoatId;
            for (int i = cut + 1; i < ownedBoatId.Length; i++)
                if (ownedBoatId[i] < '0' || ownedBoatId[i] > '9') return ownedBoatId;   // not an instance number
            return ownedBoatId.Substring(0, cut);
        }

        /// <summary>True for a non-empty id with no <see cref="KeySeparator"/> in it.</summary>
        public static bool IsValid(string ownedBoatId)
            => !string.IsNullOrEmpty(ownedBoatId) && ownedBoatId.IndexOf(KeySeparator) < 0;
    }
}
