using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>What part of the day a <see cref="RoutineWhereabouts"/> describes. Append-only.</summary>
    public enum RoutineLegKind
    {
        /// <summary>Living a block in one region: walking its lanes or standing at its station. The
        /// region's own <see cref="RoutinePlan"/> knows the exact spot; the resolver does not.</summary>
        InRegion = 0,

        /// <summary>At the near end of a crossing, waiting for the gate to open (the bar to bare).</summary>
        HeldAtCrossing = 1,

        /// <summary>On the crossing itself, between one region's station and the next region's.</summary>
        Crossing = 2,
    }

    /// <summary>The inputs a cross-region routine is resolved against, besides the routine and the time.
    /// All of them are facts of the world, never of what was asked before (rule 5).</summary>
    public struct RoutineLegContext
    {
        /// <summary>The world seed: the person's departure jitter and the weekly variation hang off it.</summary>
        public int WorldSeed;

        /// <summary>Real seconds per game hour (<c>GameConfig.SecondsPerHour</c>), which converts metres
        /// at metres per second into game hours.</summary>
        public float SecondsPerGameHour;

        /// <summary>Days in a week (<c>GameConfig.DaysPerWeek</c>), so a weekday means what the clock says.</summary>
        public int DaysPerWeek;

        /// <summary>The crossing gate, or null for always open.</summary>
        public ICrossingGate Gate;
    }

    /// <summary>
    /// <b>Where a person is, across regions</b>, at one instant: the one region they are in, what they are
    /// doing, and (on a crossing) exactly where on it. See <see cref="RoutineLegs.Resolve"/>.
    /// </summary>
    public struct RoutineWhereabouts
    {
        /// <summary>The ONE region they are in. Never empty for a routine with a region.</summary>
        public string RegionId;

        /// <summary>Which part of the day this is.</summary>
        public RoutineLegKind Kind;

        /// <summary>What they are doing (the running block's tag).</summary>
        public RoutineActivity Activity;

        /// <summary>The absolute day whose blocks this came from (it can be yesterday, before today's first
        /// departure).</summary>
        public int Day;

        /// <summary>The running block's index in that day's entries, or −1 for a routine with no day.</summary>
        public int Block;

        /// <summary>The station the running block is going to or standing at (region-local id).</summary>
        public string StationId;

        /// <summary>True when <see cref="Position"/> is known here: on a crossing or held at one. In a
        /// region the position is that region's plan to give (stations live in its scene).</summary>
        public bool HasPosition;

        /// <summary>Where they are, in <see cref="RegionId"/>'s frame, when <see cref="HasPosition"/>.</summary>
        public Vector2 Position;

        /// <summary>The ground heading of travel (compass degrees) when <see cref="HasPosition"/>.</summary>
        public float HeadingDegrees;

        /// <summary>The crossing being walked or waited at, or null.</summary>
        public RegionCrossingDef Crossing;
    }

    /// <summary>
    /// One stretch of a resolved day, in absolute game hours. A day is a list of these in start order;
    /// each runs until the next one starts, except a <see cref="RoutineLegKind.Crossing"/>, which ends at
    /// its own <see cref="End"/> (the arrival, where an in-region segment for the same block begins).
    /// </summary>
    public struct RoutineSegment
    {
        public double Start;
        public double End;
        public RoutineLegKind Kind;
        public int Day;
        public int Block;
        public string RegionId;
        public RoutineActivity Activity;
        public string StationId;
        public RegionCrossingDef Crossing;
        public bool Reversed;
    }

    /// <summary>
    /// <b>THE CROSS-REGION RESOLVER</b>: a pure, deterministic function from <c>(routine, game time,
    /// world seed, gate)</c> to <see cref="RoutineWhereabouts"/> (owner ask 2026-11-15: NPCs cross scenes
    /// for work, errands and recreation; St Peters islanders cross the bar to Nine Mile Creek only while
    /// it is open, at an honest pace).
    ///
    /// <para><b>The rules, in order.</b></para>
    /// <list type="number">
    /// <item>The day lived is <see cref="RoutineDef.Entries"/>, or the matching
    /// <see cref="RoutineWeeklyDay"/> when the clock's weekday has one.</item>
    /// <item>Departures are the person's own, jittered exactly as <see cref="RoutinePlanner.ResolveStops"/>
    /// jitters them (so an ordinary day resolves to the very blocks it always did), plus, on a weekly day
    /// only, a per-day shift seeded from <c>(worldSeed, npc id, day)</c> and bounded by
    /// <see cref="RoutineWeeklyDay.DayJitterMinutes"/>.</item>
    /// <item>A block whose region equals the region they are in is an in-region block: it runs from its
    /// departure, and where exactly they are is the region plan's business.</item>
    /// <item>A block whose region differs is a crossing over the routine's
    /// <see cref="RegionCrossingDef"/> joining the two. At its departure they ask the gate; if it is shut
    /// they are HELD at the near end, in the near region, until it opens. Then they cross, for
    /// <c>length / speed</c> game hours, in the near region up to the seam and in the far one after it,
    /// and arrive at the far station.</item>
    /// <item>A crossing PUSHES: any later departure that falls before the arrival waits for it (a person
    /// cannot start their next block mid-bar). In-region blocks never push, which is the old engine's
    /// rule kept.</item>
    /// <item>The day before is resolved too, so a crossing that runs past midnight carries into today.
    /// One day of carry is the bound: a hold that runs longer than a whole day is not represented
    /// (the tide opens the bar twice a day).</item>
    /// </list>
    ///
    /// <para><b>Exactly one region at every instant</b> follows from the shape: every instant falls in
    /// exactly one segment, every segment has one region, and a crossing hands over at one seam distance.
    /// No hidden randomness: every variation is a hash of named facts (rule 5).</para>
    /// </summary>
    public static class RoutineLegs
    {
        /// <summary>Hash stream for the weekly day's per-day departure shift, distinct from
        /// <see cref="RoutineSchedule.JitterStream"/> so neither can re-roll the other.</summary>
        public const int DayJitterStream = 23;

        /// <summary>Days of carry resolved before the asked day (see rule 6 in the class remarks).</summary>
        public const int CarryDays = 1;

        /// <summary>Hours in a day, the clock's own.</summary>
        const double HoursPerDay = RoutineSchedule.HoursPerDay;

        // ---- which day ---------------------------------------------------------------------------

        /// <summary>The weekday index of <paramref name="dayIndex"/>, as <c>GameClock.Weekday</c> derives it.</summary>
        public static int WeekdayIndex(int dayIndex, int daysPerWeek)
        {
            int n = Math.Max(1, daysPerWeek);
            return ((dayIndex % n) + n) % n;
        }

        /// <summary>
        /// The blocks lived on <paramref name="dayIndex"/>: the matching weekly day's, or the ordinary
        /// <see cref="RoutineDef.Entries"/>. <paramref name="weeklyIndex"/> is the weekly day used, or −1.
        /// </summary>
        public static RoutineEntry[] EntriesFor(RoutineDef routine, int dayIndex, int daysPerWeek,
                                                out int weeklyIndex)
        {
            weeklyIndex = -1;
            if (routine == null) return Array.Empty<RoutineEntry>();
            RoutineWeeklyDay[] weekly = routine.WeeklyDays;
            if (weekly != null && weekly.Length > 0)
            {
                int wd = WeekdayIndex(dayIndex, daysPerWeek);
                for (int i = 0; i < weekly.Length; i++)
                {
                    if ((int)weekly[i].Weekday != wd) continue;
                    if (weekly[i].Entries == null || weekly[i].Entries.Length < 2) continue;
                    weeklyIndex = i;
                    return weekly[i].Entries;
                }
            }
            return routine.Entries ?? Array.Empty<RoutineEntry>();
        }

        // ---- when ---------------------------------------------------------------------------------

        /// <summary>The seed of one person's one day: <c>(worldSeed, npc id, day)</c>. The NPC's id, not the
        /// routine's, because the variation is the person's (owner ask: "nobody drives perfectly the same
        /// every time"); the routine id stands in when no NPC is set.</summary>
        public static uint DaySeed(int worldSeed, RoutineDef routine, int dayIndex)
        {
            string who = routine != null && routine.Npc != null && !string.IsNullOrEmpty(routine.Npc.Id)
                ? routine.Npc.Id
                : routine != null ? routine.Id : string.Empty;
            uint h = RoutineSchedule.RoutineSeed(worldSeed, who);
            return RoutineSchedule.Fold(h, dayIndex);
        }

        /// <summary>
        /// The departure hour of each of <paramref name="entries"/> on <paramref name="dayIndex"/>, written
        /// into <paramref name="into"/> (length ≥ entries). Hours are in the day's own frame and NOT wrapped
        /// (a 00:05 block jittered early reads −0.02, never 23.98), so they stay in order.
        ///
        /// <para>An ordinary day gives exactly <see cref="RoutineSchedule.DepartureHour"/>'s hours
        /// (unwrapped). A weekly day adds the per-day shift, whose amplitude is the room the person's own
        /// jitter leaves under the same order-preserving clamp, so the two together can never reorder it.</para>
        /// </summary>
        public static void DepartureHours(RoutineDef routine, RoutineEntry[] entries, int weeklyIndex,
                                          int dayIndex, int worldSeed, float[] into)
        {
            int n = entries != null ? entries.Length : 0;
            if (n == 0 || routine == null) return;

            var authored = new float[n];
            for (int i = 0; i < n; i++) authored[i] = RoutineSchedule.Wrap24(entries[i].StartHour);

            uint seed = RoutineSchedule.RoutineSeed(worldSeed, routine.Id);
            float jitter = routine.ScheduleJitterMinutes;
            float dayJitter = weeklyIndex >= 0 && routine.WeeklyDays != null && weeklyIndex < routine.WeeklyDays.Length
                ? routine.WeeklyDays[weeklyIndex].DayJitterMinutes
                : 0f;
            uint daySeed = dayJitter > 0f ? DaySeed(worldSeed, routine, dayIndex) : 0u;

            for (int i = 0; i < n; i++)
            {
                float wrapped = RoutineSchedule.DepartureHour(authored, i, seed, jitter);
                float shift = wrapped - authored[i];
                if (shift > 12f) shift -= 24f;
                else if (shift < -12f) shift += 24f;
                float hour = authored[i] + shift;

                if (dayJitter > 0f && n > 1)
                {
                    float room = DayShiftRoomHours(authored, i, jitter);
                    float amp = Mathf.Min(dayJitter / 60f, room);
                    hour += RoutineSchedule.HashSigned(daySeed, DayJitterStream, i) * amp;
                }
                into[i] = hour;
            }
        }

        /// <summary>The largest per-day shift block <paramref name="i"/> may take on top of the person's own
        /// jitter without the two together leaving the clamp that keeps the day in order.</summary>
        public static float DayShiftRoomHours(float[] authored, int i, float jitterMinutes)
        {
            int n = authored.Length;
            float before = RoutineSchedule.GapHours(authored, (i - 1 + n) % n);
            float after = RoutineSchedule.GapHours(authored, i);
            float room = Mathf.Max(0f, Mathf.Min(before, after) * 0.5f - RoutineSchedule.MinBlockMarginHours);
            float own = Mathf.Min(Mathf.Abs(jitterMinutes) / 60f, room);
            return Mathf.Max(0f, room - own);
        }

        /// <summary>Game hours to cross <paramref name="crossing"/> for block <paramref name="entry"/>:
        /// its length at the block's carrier speed, or the person's walk.</summary>
        public static double CrossingHours(RoutineDef routine, in RoutineEntry entry, RegionCrossingDef crossing,
                                           float secondsPerGameHour)
        {
            if (crossing == null || routine == null) return 0d;
            float speed = entry.CrossingSpeedMetresPerSecond > 0f
                ? entry.CrossingSpeedMetresPerSecond
                : routine.WalkSpeedMetresPerSecond;
            return RoutineSchedule.TravelHours(crossing.LengthMetres, speed, secondsPerGameHour);
        }

        /// <summary>The routine's crossing that joins <paramref name="from"/> to <paramref name="to"/>, or null.</summary>
        public static RegionCrossingDef FindCrossing(RoutineDef routine, string from, string to, out bool reversed)
        {
            reversed = false;
            RegionCrossingDef[] all = routine != null ? routine.Crossings : null;
            if (all == null) return null;
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].IsWalkable && all[i].Joins(from, to, out reversed)) return all[i];
            reversed = false;
            return null;
        }

        // ---- the day, resolved ---------------------------------------------------------------------

        /// <summary>
        /// Resolve one day into <paramref name="into"/> (appended, in start order).
        /// <paramref name="busyUntil"/> and <paramref name="region"/> carry in from the day before (where they
        /// are and until when a crossing holds them) and are updated to carry out.
        /// </summary>
        public static void BuildDay(RoutineDef routine, int dayIndex, in RoutineLegContext ctx,
                                    List<RoutineSegment> into, ref double busyUntil, ref string region)
        {
            if (routine == null || into == null) return;
            RoutineEntry[] entries = EntriesFor(routine, dayIndex, ctx.DaysPerWeek, out int weekly);
            int n = entries.Length;
            if (n == 0) return;

            var deps = new float[n];
            DepartureHours(routine, entries, weekly, dayIndex, ctx.WorldSeed, deps);
            ICrossingGate gate = ctx.Gate ?? AlwaysOpenCrossingGate.Instance;
            double dayStart = dayIndex * HoursPerDay;
            if (string.IsNullOrEmpty(region)) region = routine.RegionId;

            for (int k = 0; k < n; k++)
            {
                double dep = dayStart + deps[k];
                if (dep < busyUntil) dep = busyUntil;
                if (double.IsPositiveInfinity(dep)) break;   // held forever at a gate that never opens

                RoutineEntry e = entries[k];
                string target = routine.RegionOf(e);
                var seg = new RoutineSegment
                {
                    Day = dayIndex, Block = k, Activity = e.Activity, StationId = e.StationId,
                    End = double.NaN,
                };

                if (target == region)
                {
                    seg.Start = dep; seg.Kind = RoutineLegKind.InRegion; seg.RegionId = region;
                    into.Add(seg);
                    continue;
                }

                RegionCrossingDef crossing = FindCrossing(routine, region, target, out bool reversed);
                if (crossing == null)
                {
                    // A content fault (RoutineCrossRegionContentTests forbids it): no way across. They change
                    // region at the departure, which still leaves them in exactly one region.
                    seg.Start = dep; seg.Kind = RoutineLegKind.InRegion; seg.RegionId = target;
                    into.Add(seg);
                    region = target;
                    continue;
                }

                seg.Crossing = crossing;
                seg.Reversed = reversed;

                double open = gate.NextOpenHour(crossing.Id, dep);
                if (double.IsNaN(open) || open < dep) open = dep;
                if (open > dep)
                {
                    seg.Start = dep; seg.End = open; seg.Kind = RoutineLegKind.HeldAtCrossing;
                    seg.RegionId = region;
                    seg.StationId = crossing.NearStationId(reversed);
                    into.Add(seg);
                    if (double.IsPositiveInfinity(open)) { busyUntil = open; return; }
                }

                double arrive = open + CrossingHours(routine, e, crossing, ctx.SecondsPerGameHour);
                seg.Start = open; seg.End = arrive; seg.Kind = RoutineLegKind.Crossing;
                seg.RegionId = region;   // the near side; the far side is reported past the seam
                seg.StationId = e.StationId;
                into.Add(seg);

                region = target;
                busyUntil = arrive;
                into.Add(new RoutineSegment
                {
                    Start = arrive, End = double.NaN, Kind = RoutineLegKind.InRegion, Day = dayIndex, Block = k,
                    RegionId = target, Activity = e.Activity, StationId = e.StationId,
                });
            }
        }

        /// <summary>
        /// Resolve <paramref name="dayIndex"/> together with the <see cref="CarryDays"/> before it (whose
        /// last segments run until today's first departure) into <paramref name="into"/>, cleared first.
        /// </summary>
        public static void BuildWindow(RoutineDef routine, int dayIndex, in RoutineLegContext ctx,
                                       List<RoutineSegment> into)
        {
            into.Clear();
            if (routine == null) return;
            int first = dayIndex - CarryDays;
            // Where they woke on the first day of the window: where the day before it ended.
            RoutineEntry[] before = EntriesFor(routine, first - 1, ctx.DaysPerWeek, out _);
            string region = before.Length > 0 ? routine.RegionOf(before[before.Length - 1]) : routine.RegionId;
            double busy = double.NegativeInfinity;
            for (int d = first; d <= dayIndex; d++)
                BuildDay(routine, d, ctx, into, ref busy, ref region);
        }

        /// <summary>
        /// Where they are at <paramref name="gameHour"/> (absolute game hours), given a window built by
        /// <see cref="BuildWindow"/> for the day that hour falls in. Allocation-free.
        /// </summary>
        public static RoutineWhereabouts Sample(RoutineDef routine, List<RoutineSegment> window, double gameHour,
                                                in RoutineLegContext ctx)
        {
            var w = new RoutineWhereabouts { RegionId = routine != null ? routine.RegionId : string.Empty, Block = -1 };
            if (window == null || window.Count == 0) return w;

            int at = 0;
            for (int i = 0; i < window.Count; i++)
            {
                if (window[i].Start <= gameHour) at = i;
                else break;
            }
            RoutineSegment s = window[at];
            // A crossing whose arrival has passed hands over to the in-region segment that follows it;
            // the list holds that one next, so this only matters for an hour exactly at the arrival.
            if (s.Kind == RoutineLegKind.Crossing && gameHour >= s.End && at + 1 < window.Count) s = window[at + 1];

            w.Kind = s.Kind;
            w.Day = s.Day;
            w.Block = s.Block;
            w.Activity = s.Activity;
            w.StationId = s.StationId;
            w.RegionId = s.RegionId;
            w.Crossing = s.Crossing;

            switch (s.Kind)
            {
                case RoutineLegKind.HeldAtCrossing:
                    w.HasPosition = true;
                    w.Position = s.Crossing.NearPoint(s.Reversed);
                    s.Crossing.PointAt(0f, s.Reversed, out _, out w.HeadingDegrees);
                    break;

                case RoutineLegKind.Crossing:
                {
                    double span = s.End - s.Start;
                    float t01 = span > 0d ? (float)Math.Min(1d, Math.Max(0d, (gameHour - s.Start) / span)) : 1f;
                    float metres = t01 * s.Crossing.LengthMetres;
                    w.HasPosition = true;
                    w.Position = s.Crossing.PointAt(metres, s.Reversed, out string region, out w.HeadingDegrees);
                    w.RegionId = region;
                    break;
                }
            }
            return w;
        }

        /// <summary>
        /// The whole question in one call: where <paramref name="routine"/>'s person is at absolute game hour
        /// <paramref name="gameHour"/>. Allocates the day window; a caller asking every frame keeps a
        /// <see cref="RoutineItinerary"/> instead.
        /// </summary>
        public static RoutineWhereabouts Resolve(RoutineDef routine, double gameHour, in RoutineLegContext ctx)
        {
            var window = new List<RoutineSegment>();
            BuildWindow(routine, DayOf(gameHour), ctx, window);
            return Sample(routine, window, gameHour, ctx);
        }

        /// <summary>The absolute day an absolute game hour falls in.</summary>
        public static int DayOf(double gameHour) => (int)Math.Floor(gameHour / HoursPerDay);
    }

    /// <summary>
    /// <see cref="RoutineLegs.Resolve"/> for a caller that asks every frame: the day window is built once
    /// per day (and again if the seed, the day length, the week or the gate changes) and sampled without
    /// allocating. Holds no state the answer depends on beyond that cache, so it can never drift.
    /// </summary>
    public sealed class RoutineItinerary
    {
        private readonly RoutineDef _routine;
        private readonly List<RoutineSegment> _window = new List<RoutineSegment>(32);
        private int _day = int.MinValue;
        private RoutineLegContext _built;

        public RoutineItinerary(RoutineDef routine) { _routine = routine; }

        /// <summary>The routine resolved.</summary>
        public RoutineDef Routine => _routine;

        /// <summary>Where they are at absolute game hour <paramref name="gameHour"/>.</summary>
        public RoutineWhereabouts Resolve(double gameHour, in RoutineLegContext ctx)
        {
            int day = RoutineLegs.DayOf(gameHour);
            if (day != _day || ctx.WorldSeed != _built.WorldSeed || ctx.DaysPerWeek != _built.DaysPerWeek ||
                ctx.SecondsPerGameHour != _built.SecondsPerGameHour || !ReferenceEquals(ctx.Gate, _built.Gate))
            {
                RoutineLegs.BuildWindow(_routine, day, ctx, _window);
                _day = day;
                _built = ctx;
            }
            return RoutineLegs.Sample(_routine, _window, gameHour, ctx);
        }
    }
}
