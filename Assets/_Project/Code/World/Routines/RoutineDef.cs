using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// One entry in a villager's day: <b>at this hour, set out for that station and do that there</b> —
    /// the "time block" of <c>design/npcs-and-routines.md</c> §2.2, in the one shape a closed-form
    /// schedule can be read out of.
    ///
    /// <para><b>Why a START hour and no end hour.</b> The doc's sketch writes blocks as
    /// <c>{from, to, anchor}</c>, which stores the same instant twice — one block's <c>to</c> is the
    /// next one's <c>from</c> — and a pair of copies of one number is a pair that will stop agreeing
    /// (#345's standing lesson, in miniature). An entry's block therefore runs from its own
    /// <see cref="StartHour"/> to the NEXT entry's, and the last entry's runs through midnight to the
    /// first. There is no way to author a gap or an overlap, because neither is expressible.</para>
    ///
    /// <para><b><see cref="StartHour"/> is a DEPARTURE, not an arrival.</b> At that hour the villager
    /// leaves wherever they were and starts walking; they arrive when the walk is done. That is what
    /// makes the lanes get used — an arrival-time schedule would have to teleport them backwards to
    /// start the walk on time.</para>
    /// </summary>
    [System.Serializable]
    public struct RoutineEntry
    {
        [Tooltip("Game hour (0..24) this block begins — the hour they SET OUT for the station below. " +
                 "Entries are authored in ascending order; the last one runs through midnight to the first.")]
        [Range(0f, 24f)] public float StartHour;

        [Tooltip("Which station they walk to (a station id from the region's own table, e.g. " +
                 "station.st_peters.store_counter). The position is the REGION's to derive; a routine " +
                 "never carries a coordinate.")]
        public string StationId;

        [Tooltip("What they are doing there. A tag for presentation and dialogue context — nothing in " +
                 "the engine branches on it (see RoutineActivity).")]
        public RoutineActivity Activity;

        [Tooltip("One line on WHY, for the owner reading the table. Authoring note only.")]
        public string Why;

        [Tooltip("Tick this and they will NOT stop to talk during this block — they keep walking and " +
                 "answer on the move. LEAVE IT OFF for almost everything: stopping is the default and " +
                 "the exception is authored, not guessed. Reach for it on a block that must not be " +
                 "interrupted in the middle — a lane crossing, a timed beat — where a villager frozen " +
                 "mid-stride would read as broken rather than polite.")]
        public bool Uninterruptible;

        [Tooltip("Which region this block's station is in (region.snake_case). LEAVE EMPTY for the " +
                 "routine's own region, which is what every block authored before cross-region routines " +
                 "means. A block whose region differs from the block before it is a CROSSING: at its hour " +
                 "they set out over one of the routine's Crossings and arrive at that crossing's far station, " +
                 "which must be this block's station.")]
        public string RegionId;

        [Tooltip("CROSSING blocks only: what carries them over (an id, e.g. vehicle.utility_quad), or " +
                 "empty for on foot. A label for presentation; the time is set by the speed below.")]
        public string CrossingCarrierId;

        [Tooltip("CROSSING blocks only: metres per second over the crossing. 0 = their own walk speed. " +
                 "A quad is honestly faster than a walker; a laden cart is honestly slower.")]
        [Min(0f)] public float CrossingSpeedMetresPerSecond;

        [Tooltip("What they DO at the station, as data (an NPC activity Def, e.g. npc_activity.wharf_rod for " +
                 "fishing off the wharf). LEAVE EMPTY for a block that is only its tag above, which is every " +
                 "block authored before activities existed.")]
        public NpcActivityDef Doing;

        /// <summary>True when a conversation may stop this block — the default, because the field
        /// records the EXCEPTION. A routine authored before this existed deserializes to false here and
        /// is therefore interruptible, which is what it always was.</summary>
        public bool Interruptible => !Uninterruptible;
    }

    /// <summary>
    /// <b>One day of the week that differs from the ordinary one</b>: the weekly errand, the market run.
    /// On a matching day these blocks are lived INSTEAD of <see cref="RoutineDef.Entries"/>; every other
    /// day is exactly the ordinary day it always was.
    ///
    /// <para>The weekday is the clock's own (<c>DayIndex % GameConfig.DaysPerWeek</c>, as
    /// <c>GameClock.Weekday</c> computes it), so "Wednesday" means the same thing here as on the HUD.
    /// Its first and last blocks must be in the routine's own region, so the day starts and ends where
    /// the ordinary days do and midnight is never a seam.</para>
    /// </summary>
    [System.Serializable]
    public struct RoutineWeeklyDay
    {
        [Tooltip("Which day of the week this replaces the ordinary day on.")]
        public Weekday Weekday;

        [Tooltip("One line on WHY, for the owner reading the table. Authoring note only.")]
        public string Why;

        [Tooltip("How many GAME minutes, ±, this day's departures may move from one week to the next, " +
                 "seeded from (worldSeed, day, npc id): nobody leaves at the same minute every week. " +
                 "Clamped with the person's own jitter so it can never reorder the day.")]
        [Min(0f)] public float DayJitterMinutes;

        [Tooltip("The blocks of this day, as Entries: ascending StartHour, the last wraps midnight.")]
        public RoutineEntry[] Entries;
    }

    /// <summary>
    /// <b>A villager's day, as DATA</b> (ADR 0003 / CLAUDE.md rule 2): one asset per person under
    /// <c>Data/Routines</c>, keyed by a stable, append-only <see cref="Id"/> (<c>routine.snake_case</c>).
    /// Who works where and when is the owner's to change, and every change here is a one-line edit in an
    /// inspector — no code, no rebuild of anything but the scene.
    ///
    /// <para><b>What this deliberately is NOT, yet.</b> <c>design/npcs-and-routines.md</c> §2.2 wants a
    /// <i>prioritised list of conditional schedules</i> — a storm day, a market day, a rest day — resolved
    /// against world-state. This is the <b>default schedule only</b>: one day, always. The reactivity
    /// engine is the next phase, and this shape is what it grows out of rather than something it has to
    /// replace: a conditional set is a list of these plus a <c>when</c>, and every field here keeps its
    /// meaning inside one. Shipping the conditional matrix first would have meant six variants per person
    /// with no way to see whether one honest day even reads right.</para>
    ///
    /// <para><b>Nothing here is saved and nothing here ticks (rule 5).</b> Where the villager IS at time
    /// <c>t</c> is a pure function of this table, the region's geometry, the world seed and the clock —
    /// recomputed on load, never restored. See <see cref="RoutinePlan"/>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Routine", fileName = "Routine")]
    public class RoutineDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (routine.snake_case, e.g. routine.rose_macisaac). " +
                 "Content-validated for uniqueness.")]
        public string Id = "routine.example";

        [Tooltip("Which region this day belongs to (region.snake_case). Station positions are " +
                 "region-local, so without this a station id could resolve somewhere else entirely.")]
        public string RegionId = "";

        [Tooltip("Whose day this is. The region builder matches a placed villager to their routine " +
                 "through this reference, so a routine with no NPC is authored but unused — which is a " +
                 "legal work-in-progress state and is reported, not thrown on.")]
        public NpcDef Npc;

        [Header("The day (ascending StartHour; the last block wraps midnight)")]
        [Tooltip("The blocks of the day. At least two — a day with one block is a person standing still, " +
                 "which is what NOT having a routine already does.")]
        public RoutineEntry[] Entries = System.Array.Empty<RoutineEntry>();

        [Header("Feel (rule 6 — no magic numbers in code)")]
        [Tooltip("How fast this person walks, in metres per second. PER PERSON on purpose: an old skipper " +
                 "and a young clam digger do not cross the green at the same speed, and the gait the " +
                 "sprite draws is chosen from this number by CharacterVisualDef's own thresholds " +
                 "(walk above 0.35 m/s, run above 4.5 — so anything sane here walks).")]
        [Min(0.05f)] public float WalkSpeedMetresPerSecond = 1.15f;

        [Tooltip("How many GAME minutes this person's departures are shifted by, ±, so the village does " +
                 "not move as one body on the hour. Seeded from (worldSeed, routine id, entry index) — " +
                 "so it is stable across days and across sessions: the postmistress is always a few " +
                 "minutes late in the same way, which is a personality and not noise. Keep it under " +
                 "half the smallest gap between two of this person's blocks, which " +
                 "RoutineContentTests checks.")]
        [Min(0f)] public float ScheduleJitterMinutes = 6f;

        [Tooltip("How much faster than their normal walk this person moves when they are LATE — after a " +
                 "conversation held them up, hurrying back onto the day the clock says they should be " +
                 "having. PER PERSON, like the walk speed: an old skipper's version of hurrying is not a " +
                 "clam digger's. It must be above 1 or they can never catch a target that is itself " +
                 "walking away at their own speed.")]
        [Min(1f)] public float CatchUpSpeedMultiplier = 1.6f;

        /// <summary>Metres per second this person moves while catching up after a conversation — their
        /// walk, hurried. Not a stored field: one number times another, so the two can never drift.</summary>
        [Header("Other days and other regions (owner ask 2026-11-15)")]
        [Tooltip("Days of the week that differ from the one above, e.g. a weekly errand to Nine Mile " +
                 "Creek. Empty: every day is the day above, exactly as before.")]
        public RoutineWeeklyDay[] WeeklyDays = System.Array.Empty<RoutineWeeklyDay>();

        [Tooltip("The crossings this person uses when a block names another region. Each region change in " +
                 "any of their days must be joined by one of these (RoutineCrossRegionContentTests).")]
        public RegionCrossingDef[] Crossings = System.Array.Empty<RegionCrossingDef>();

        public float CatchUpSpeedMetresPerSecond =>
            WalkSpeedMetresPerSecond * Mathf.Max(1f, CatchUpSpeedMultiplier);

        /// <summary>The region <paramref name="entry"/> is in: its own <see cref="RoutineEntry.RegionId"/>,
        /// or this routine's when that is empty.</summary>
        public string RegionOf(in RoutineEntry entry) =>
            string.IsNullOrEmpty(entry.RegionId) ? RegionId : entry.RegionId;

        /// <summary>
        /// True when any day of this routine leaves its own region. A routine that never does is resolved
        /// by exactly the code it always was; this is the switch that keeps every existing villager on it.
        /// </summary>
        public bool SpansRegions
        {
            get
            {
                if (SpansRegionsIn(Entries)) return true;
                if (WeeklyDays == null) return false;
                for (int i = 0; i < WeeklyDays.Length; i++)
                    if (SpansRegionsIn(WeeklyDays[i].Entries)) return true;
                return false;
            }
        }

        /// <summary>True when any of <paramref name="entries"/> names a region other than this routine's.</summary>
        public bool SpansRegionsIn(RoutineEntry[] entries)
        {
            if (entries == null) return false;
            for (int i = 0; i < entries.Length; i++)
                if (RegionOf(entries[i]) != RegionId) return true;
            return false;
        }

        /// <summary>True when this table can actually be walked: two blocks or more, and an NPC to walk
        /// them. Everything downstream is null-tolerant, so a half-authored routine is a reported
        /// absence rather than an exception.</summary>
        public bool IsWalkable => Npc != null && Entries != null && Entries.Length >= 2;
    }
}
