using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// A fish species, as data (ADR 0003). The 100 species are assets like this, not code. Create
    /// via Assets &gt; Create &gt; Hidden Harbours &gt; Fish Species into Data/Fish. Gating fields
    /// (region/tide/time/season/gear) are what make the same ground fish differently across a day
    /// and a year (Pillar 1). Full schema: design/fish-and-content.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fish Species", fileName = "Fish")]
    public class FishSpeciesDef : ScriptableObject
    {
        [Header("Identity")]
        public string Id = "fish.atlantic_cod";
        public string DisplayName = "Atlantic Cod";
        public FishCategory Category = FishCategory.InshoreGroundfish;
        public Rarity Rarity = Rarity.Common;
        [TextArea] public string Flavor;

        [Header("Art")]
        [Tooltip("Optional species sprite (icon/haul art). Attached by art-pipeline later; never required.")]
        public Sprite Sprite;

        [Header("Where & when it bites")]
        public string[] RegionIds = { "region.coddle_cove" };
        public Gear AllowedGear = Gear.Handline | Gear.Longline;
        public SeasonMask Seasons = SeasonMask.AllYear;
        [Tooltip("Tide window (metres rel. datum): only bites when Min ≤ tide ≤ Max.")]
        public float MinTide = -10f;
        public float MaxTide = 10f;
        [Tooltip("Hour window 0..24. If Start > End the window wraps past midnight (e.g. a night biter).")]
        public float StartHour = 0f;
        public float EndHour = 24f;

        [Header("Depth (Rod Fishing v2 — a WEIGHT on the roll, never a wall)")]
        [Tooltip("The depth zones this species lives in (canon depthBand, fish-and-content §3.1). When the " +
                 "player HOLDS a weighted rig in one of these zones the species is weighted UP in the catch " +
                 "roll; outside them it's damped — never zeroed. Leave None = depth-neutral (bites the same " +
                 "at any held depth), exactly as every species behaved before this field existed.")]
        public FishDepthBand DepthBands = FishDepthBand.None;

        [Tooltip("Behaviour flags (canon behaviorFlags). Bottom = a floor-dweller: weighted UP while the rig " +
                 "is held just off the floor (bottom out, then reel up slightly — the bottom-fishing sweet " +
                 "spot). Other canon flags are appended to the enum as later systems wire them.")]
        public FishFlags BehaviorFlags = FishFlags.None;

        [Header("Rod fight (Rod Fishing v2 — opt-in, append-only)")]
        [Tooltip("The v2 fight personality this species opts into (RodFightDef, data not code — design/" +
                 "rod-fishing-v2-brainstorm.md §5). Leave EMPTY and the species keeps the simple/legacy " +
                 "tension fight (FishFight / FishingPhase.Fighting) exactly as today — the same opt-in " +
                 "shape as TrapDef→DeckWorkDef. Authored per species by content agents (Wave 3).")]
        public RodFightDef RodFight;

        [Header("Catch")]
        public float MinWeightKg = 1f;
        public float MaxWeightKg = 8f;
        [Tooltip("Value in ₲ at a neutral market.")]
        public int BaseValue = 12;
        [Range(0f, 1f)] [Tooltip("How much landing it depresses the price (perishables higher).")]
        public float SupplyElasticity = 0.2f;
        [Tooltip("Relative likelihood among matching fish (rarer = lower).")]
        public float SpawnWeight = 1f;

        // --- gating helpers (used by CatchResolver and testable on their own) ---
        public bool RegionAllowed(string regionId)
        {
            if (RegionIds == null) return false;
            for (int i = 0; i < RegionIds.Length; i++)
                if (RegionIds[i] == regionId) return true;
            return false;
        }

        public bool GearAllowed(Gear gear) => (AllowedGear & gear) != 0;

        /// <summary>True when this species carries the <see cref="FishFlags.Bottom"/> flag — the
        /// depth-weighting's "boost it just off the floor" read (design §2.3).</summary>
        public bool IsBottomFish => (BehaviorFlags & FishFlags.Bottom) != 0;

        public bool TideAllowed(float tide) => tide >= MinTide && tide <= MaxTide;

        public bool SeasonAllowed(Season season) => (Seasons & ToMask(season)) != 0;

        public bool TimeAllowed(float hour)
        {
            if (Mathf.Approximately(StartHour, EndHour)) return true;      // all day
            return StartHour < EndHour
                ? hour >= StartHour && hour < EndHour
                : hour >= StartHour || hour < EndHour;                     // wraps midnight
        }

        private static SeasonMask ToMask(Season s) => s switch
        {
            Season.EarlySpring => SeasonMask.EarlySpring,
            Season.HighSummer  => SeasonMask.HighSummer,
            Season.TheTurn     => SeasonMask.TheTurn,
            _                  => SeasonMask.HardWinter
        };
    }
}
