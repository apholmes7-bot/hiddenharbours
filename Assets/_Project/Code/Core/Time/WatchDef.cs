using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HiddenHarbours.Core
{
    // Serialized values are append-only. New faces need their own drawing slice.
    public enum WatchFaceKind
    {
        HarbourDigital = 0
    }

    [Flags]
    public enum WatchReadouts
    {
        None = 0,
        Time = 1,
        Weekday = 2,
        Date = 4,
        Season = 8,
        Year = 16,
        MarketDay = 32,
        NightLight = 64
    }

    /// <summary>One watch's authored identity and capabilities. Ownership uses SaveData.OwnedGear;
    /// these fields neither equip a watch nor add a field to the save.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Watch", fileName = "Watch")]
    public sealed class WatchDef : ScriptableObject
    {
        private static readonly Regex StableId = new Regex(@"^watch\.[a-z][a-z0-9_]*$");
        private const WatchReadouts KnownReadouts = WatchReadouts.Time | WatchReadouts.Weekday
            | WatchReadouts.Date | WatchReadouts.Season | WatchReadouts.Year
            | WatchReadouts.MarketDay | WatchReadouts.NightLight;

        [Tooltip("Stable, append-only id stored in OwnedGear. Never rename or reuse.")]
        public string Id;
        public string DisplayName;
        public WatchFaceKind Face;
        public WatchReadouts Readouts;

        [Min(0), Tooltip("Higher tiers take precedence when choosing among owned watches.")]
        public int Tier;

        [Tooltip("Starting equipment, granted without a purchase by the ownership integration.")]
        public bool StartsOwned;

        [Tooltip("Earlier owned-gear id accepted for this same watch. Empty for a new identity.")]
        public string LegacyGearId;

        [Min(0), Tooltip("Authored price in the game's currency. Starting-only equipment costs zero.")]
        public int Price;

        [Tooltip("Stable seller id. Empty for starting-only equipment; this does not create a shop listing.")]
        public string SellerId;

        /// <summary>Shared authoring rules for tests and future editor validation.</summary>
        public bool TryValidate(out string error)
        {
            if (string.IsNullOrEmpty(Id) || !StableId.IsMatch(Id))
                error = "Id must be watch.snake_case.";
            else if (string.IsNullOrWhiteSpace(DisplayName))
                error = "DisplayName is required.";
            else if (!Enum.IsDefined(typeof(WatchFaceKind), Face))
                error = "Face must name an implemented watch face.";
            else if ((Readouts & WatchReadouts.Time) == 0 || (Readouts & ~KnownReadouts) != 0)
                error = "Readouts must include time and contain only known capabilities.";
            else if (Tier < 0)
                error = "Tier cannot be negative.";
            else if (Price < 0)
                error = "Price cannot be negative.";
            else if (string.IsNullOrWhiteSpace(SellerId) && (!StartsOwned || Price != 0))
                error = "A watch without a seller must be free starting equipment.";
            else if (LegacyGearId != null && LegacyGearId.Length > 0
                && (string.IsNullOrWhiteSpace(LegacyGearId) || LegacyGearId == Id))
                error = "LegacyGearId must be a distinct earlier gear id or empty.";
            else
            {
                error = null;
                return true;
            }
            return false;
        }
    }
}
