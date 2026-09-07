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

        [Tooltip("A SECOND daily window, for a species the seed table gives two of — a striped bass " +
                 "hunts Dawn AND Dusk, which one window cannot say. Leave both 0 = no second window, " +
                 "exactly as every species behaved before this field existed. Wraps midnight like the " +
                 "first.")]
        public float SecondStartHour = 0f;

        [Tooltip("End of the second daily window. See SecondStartHour.")]
        public float SecondEndHour = 0f;

        [Tooltip("MOVING WATER ONLY (owner ruling 2026-09-06). A striped bass hunts the rips on the " +
                 "run of the tide and goes off the feed at slack — a fact about the tide's RATE, not " +
                 "its height, which MinTide/MaxTide cannot express. When set, the species is barred " +
                 "while the water is slacker than GameConfig.FishSchools.MovingWaterMetresPerHour. " +
                 "Unset = no gate, bit-for-bit today's behaviour.")]
        public bool MovingWaterOnly = false;

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

        [Header("School size (owner ruling 2026-09-06: density is per SPECIES, not one global number)")]
        [Tooltip("Fewest fish a school of this species shows. A herring shoal is not a flounder: the " +
                 "owner's ruling is that how many fish are together is a fact about the SPECIES, not one " +
                 "global MaxMarks. Leave 0 = unstated, and the school falls back to GameConfig's global " +
                 "range exactly as every species behaved before this field existed.")]
        [Min(0)] public int MinSchoolMarks = 0;

        [Tooltip("Most fish a school of this species shows. See MinSchoolMarks. NOTE this is ONE number " +
                 "with two jobs (FishSchool.MarkCount): how many swimmers the water draws AND the bite " +
                 "rate. Past ~6 the bite-rate multiplier saturates at its cap, so a big shoal reads " +
                 "bigger without fishing faster.")]
        [Min(0)] public int MaxSchoolMarks = 0;

        /// <summary>Does this species state its own school size, or fall back to the global range?</summary>
        public bool StatesSchoolSize => MinSchoolMarks > 0 && MaxSchoolMarks > 0;

        [Tooltip("How far across the shoal's own lazy loop reaches, in METRES. This is the SPREAD the " +
                 "owner's 2026-09-06 ruling asks for: a mackerel shoal ranges loose and fast, a cod " +
                 "school holds tight, and a flounder scatter barely moves. The rig's own shoal is " +
                 "authored at 1.2 m (ShoalMath.DefaultRadiusMetres) and that is the reference. " +
                 "Leave 0 = unstated, and the school falls back to GameConfig's global spread. " +
                 "NOTE this is a length in metres, NOT a fraction of the school's radius. A school is " +
                 "22-55 m across because that is how far a BOAT may be and still be on it; the fish " +
                 "inside it swim a loop of THIS size, which is a different quantity entirely.")]
        [Min(0f)] public float ShoalSpreadMetres = 0f;

        [Tooltip("How many schools of this species a square kilometre of suitable water holds at once " +
                 "(owner ruling 2026-09-06: density is per SPECIES). Cod are many and tight; a striped " +
                 "bass is a scarcer fish. Leave 0 = unstated, and the species falls back to " +
                 "GameConfig's global BaseAppearanceChance01 exactly as it behaved before this field " +
                 "existed. The number is a density over WATER, before weather and season scale it: " +
                 "the same fish is leaner in hard winter and generous in high summer.")]
        [Min(0f)] public float SchoolsPerSquareKilometre = 0f;

        /// <summary>Does this species state its own shoal spread, or fall back to the global one?</summary>
        public bool StatesShoalSpread => ShoalSpreadMetres > 0f;

        /// <summary>Does this species state its own density, or fall back to the global chance?</summary>
        public bool StatesSchoolDensity => SchoolsPerSquareKilometre > 0f;

        [Header("The fight (owner ruling 2026-09-06: a hooked jumper jumps too)")]
        [Tooltip("How often a HOOKED fish of this species tries a jump while she is up at the surface, " +
                 "in seconds. Only ever read for a species that carries the Jumps behaviour flag - a " +
                 "cod does not jump on the line however short a period is authored here. Leave 0 = " +
                 "unstated, and the fight presenter uses its own authored fallback, which is what every " +
                 "species did before this field existed.")]
        [Min(0f)] public float FightJumpPeriodSeconds = 0f;

        /// <summary>Does this species CLEAR THE WATER (owner's ruling 2026-09-06)? The one read behind
        /// both the shoal's jump and the hooked fish's, so a flounder can never do either.</summary>
        public bool Jumps => (BehaviorFlags & FishFlags.Jumps) != 0;

        /// <summary>
        /// A CLAM CANNOT SWIM. Shellfish are in a region's pool because they are caught there (clam
        /// beds, lobster and crab pots), not because they shoal in midwater — so they are excluded from
        /// the swimming-school pool the water draws (owner's Q1 default, 2026-09-06). They keep their
        /// beds and their digging untouched; this flag only says "do not build a swimming school of
        /// these", which is the honest reading of <see cref="FishCategory.Shellfish"/> rather than a
        /// second hand-kept list of ids to fall out of step with the data.
        /// </summary>
        public bool IsShellfish => Category == FishCategory.Shellfish;

        [Header("What tempts it (a WEIGHT on the roll, never a wall)")]
        [Tooltip("Which lure PRESENTATIONS this species chases. A mackerel hits feathers and flash; a " +
                 "pollock chases anything worked; a haddock is fussy and wants real bait, so leave its " +
                 "mask thin. When the tackle tied on matches, the species is weighted UP in the catch " +
                 "roll; when it doesn't, damped — never zeroed. None = indifferent to tackle, which is " +
                 "exactly how every species behaved before this field existed.")]
        public LureTag FavoredLures = LureTag.None;

        /// <summary>True when the tackle tied on is one this species chases. <see cref="LureTag.None"/>
        /// on either side is "no opinion" — false, so the caller applies its neutral weight rather than
        /// a bonus (an unauthored species is never accidentally favoured by everything).</summary>
        public bool LureFavored(LureTag lure)
            => lure != LureTag.None && (FavoredLures & lure) != 0;

        [Header("Bite (owner drop §10 — opt-in, append-only)")]
        [Tooltip("This species' BITE personality (BiteDef, data not code — brainstorm §10.2: how many " +
                 "teasing false nibbles, how wide the true take's strike window, how readily she " +
                 "returns after a miss). Leave EMPTY and the species keeps today's forgiving auto-hook " +
                 "bite exactly — the same opt-in shape as RodFight below. Shipped values are " +
                 "placeholder personalities the owner tunes in play.")]
        public BiteDef Bite;

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
        [Min(0f)]
        [Tooltip("Freshness (M1 §7.3): spoil accrued per in-game day left IN THE OPEN (1 = ruined in " +
                 "one day; 0 = never rots). Fast for oily fish (mackerel), slow for hardy shellfish. " +
                 "PLACEHOLDER pacing — §7.4's pacing model (economy-sim) owns the real numbers.")]
        public float SpoilPerDay = 0.75f;
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
            // Window one keeps its old meaning: ends equal = ALL DAY (that is how every shipped species
            // says "no time gate"). Window two is additive, so ends equal there means simply "unset".
            if (Mathf.Approximately(StartHour, EndHour)) return true;
            return InWindow(hour, StartHour, EndHour) || InWindow(hour, SecondStartHour, SecondEndHour);
        }

        /// <summary>One window, or false when it is unset. A window whose ends are equal is "unset" for
        /// the SECOND window and "all day" for the first — see <see cref="TimeAllowed"/>, which is why
        /// this is private and the two callers pass their own meaning in.</summary>
        private static bool InWindow(float hour, float startHour, float endHour)
        {
            if (Mathf.Approximately(startHour, endHour)) return false;     // unset / empty
            return startHour < endHour
                ? hour >= startHour && hour < endHour
                : hour >= startHour || hour < endHour;                     // wraps midnight
        }

        /// <summary>
        /// Is the water moving enough for this species? True for everything that does not ask
        /// (<see cref="MovingWaterOnly"/> unset), and true whenever the rate is UNKNOWN — a rig or a
        /// fixture that cannot sample the tide must not silently starve the roll.
        /// </summary>
        /// <param name="tideRateMetresPerHour">Signed rate of change of the water level; flood is
        /// positive, ebb negative, and it is the MAGNITUDE that matters.</param>
        /// <param name="thresholdMetresPerHour">The owner's slack-water bar
        /// (<c>GameConfig.FishSchools.MovingWaterMetresPerHour</c>).</param>
        public bool MovingWaterAllowed(float tideRateMetresPerHour, float thresholdMetresPerHour)
        {
            if (!MovingWaterOnly) return true;
            if (float.IsNaN(tideRateMetresPerHour) || float.IsInfinity(tideRateMetresPerHour)) return true;
            return Mathf.Abs(tideRateMetresPerHour) >= Mathf.Max(0f, thresholdMetresPerHour);
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
