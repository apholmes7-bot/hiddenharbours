using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The sea and the sky at any game time, as much of them as a beach cares about. An interface so the
    /// planning below is plain arithmetic a test can feed (the director's own implementation reads
    /// <see cref="GameServices.Environment"/> through Core's at-time seams).
    /// </summary>
    public interface IBeachSky
    {
        /// <summary>Water level in metres on the terrain's own datum.</summary>
        float WaterLevelAt(double totalSeconds);
        /// <summary>Wind strength, in the wind law's own units.</summary>
        float WindStrengthAt(double totalSeconds);
        /// <summary>0 calm .. 1 storm.</summary>
        float SeaState01At(double totalSeconds);
        /// <summary>1 clear .. 0 thick fog.</summary>
        float Visibility01At(double totalSeconds);
    }

    /// <summary>Which day is being planned, and how long its hours are.</summary>
    public readonly struct BeachDay
    {
        public readonly int WorldSeed;
        public readonly int DayIndex;
        public readonly Season Season;
        /// <summary>Game seconds at 00:00 of the day.</summary>
        public readonly double DayStartSeconds;
        public readonly float SecondsPerGameHour;

        public BeachDay(int worldSeed, int dayIndex, Season season, float secondsPerGameHour)
        {
            WorldSeed = worldSeed;
            DayIndex = dayIndex;
            Season = season;
            SecondsPerGameHour = secondsPerGameHour;
            DayStartSeconds = (double)dayIndex * secondsPerGameHour * RoutineSchedule.HoursPerDay;
        }

        /// <summary>Game seconds at <paramref name="hourOfDay"/> of this day.</summary>
        public double TimeAt(float hourOfDay) => DayStartSeconds + (double)hourOfDay * SecondsPerGameHour;
    }

    /// <summary>What was decided for one villager's day: which activity took which block, and when.
    /// For tests, tools and (later) the things that dress an outing. Derived, never saved.</summary>
    public struct BeachOuting
    {
        public BeachActivityDef Activity;
        /// <summary>The authored block that was given to the beach.</summary>
        public int AuthoredBlock;
        /// <summary>Index of the first beach stop in the varied day, and how many there are.</summary>
        public int FirstStop;
        public int StopCount;
        public float DepartureHour;
        public float ArrivalHour;
        /// <summary>When they leave the beach.</summary>
        public float LeaveHour;
        /// <summary>True when they leave before their next block is due (a gate failed, or the
        /// activity's own last hour came) and go back to where the authored day had them.</summary>
        public bool GoesBackFirst;
        /// <summary>The ground height nobody was placed below: the highest water of the stay plus the
        /// activity's clearance.</summary>
        public float KeepAboveMetres;

        /// <summary>A gathering: the one place everybody is round. Meaningless otherwise.</summary>
        public Vector2 GatherAt;
    }

    /// <summary>
    /// <b>WHO IS ON THE BEACH TODAY, AND WHERE</b> — pure arithmetic over a villager's authored stops,
    /// the ground, and the sea and sky at the hours of the day.
    ///
    /// <para><b>Deterministic</b> (rule 5): everything is a function of the arguments. The only "random"
    /// is <see cref="RoutineSchedule.Hash01"/> on a seed folded from (world seed, person, activity, day).
    /// Nothing is remembered between calls and nothing is saved.</para>
    ///
    /// <para><b>The tide decides where people can be.</b> Every place is found by walking a line down
    /// the sand from ground that is always dry until the ground drops below the HIGHEST water of the
    /// whole stay plus a clearance. So nobody is ever put below the water, and nobody has to move
    /// because it came up: they were placed for the top of the flood before they left the house.</para>
    /// </summary>
    public static class BeachPlanning
    {
        /// <summary>The most places one walk along the tide line can have. A rail against a mis-authored
        /// zero linger turning one evening into a thousand blocks, not a tuning.</summary>
        public const int MaxStrollStops = 16;

        /// <summary>Water is read this many times per gate sample when looking for the highest water
        /// of a stay, so a fast tide cannot slip a peak between two readings.</summary>
        public const int WaterReadsPerGateSample = 4;

        private const int StreamChance = 1;
        private const int StreamDepth = 2;
        private const int StreamRotation = 3;
        private const int StreamWade = 4;
        private const int StreamGatherLine = 5;

        /// <summary>How long after its latest hour the ground of a gathering is still kept above the
        /// water, in hours: the walk off the sand.</summary>
        public const float GatherLeaveMarginHours = 1f;

        /// <summary>
        /// The seed of the things about a gathering that are the same for everybody: whether it is on
        /// today, and how far down the sand.
        /// </summary>
        public static uint GatheringSeed(BeachActivityDef activity, in BeachDay day) =>
            RoutineSchedule.Finalize(RoutineSchedule.Fold(
                RoutineSchedule.RoutineSeed(day.WorldSeed, activity.Id), day.DayIndex));

        /// <summary>Is this gathering on today, for everybody: its season, and the one roll of the day.</summary>
        public static bool GatheringIsOn(BeachActivityDef activity, in BeachDay day) =>
            activity.InSeason(day.Season) &&
            RoutineSchedule.Hash01(GatheringSeed(activity, day), StreamChance, 0) < activity.ChancePerDay;

        /// <summary>
        /// Where the gathering of the day is: one place for everybody, so it is found from the hours of
        /// the activity and not from the stay of any one person. The whole ring is on ground above the
        /// highest water from the earliest hour until an hour after the latest. False when no line has
        /// room today.
        /// </summary>
        public static bool TryGatheringCentre(BeachActivityDef activity, ITidalTerrain terrain,
                                              IBeachSky sky, in BeachDay day, out Vector2 centre)
        {
            centre = default;
            if (activity == null || activity.Beach == null || terrain == null || sky == null) return false;
            BeachTransect[] lines = activity.Beach.Gatherings;
            if (lines == null || lines.Length == 0) return false;

            float step = Mathf.Max(1f, activity.GateSampleMinutes) / 60f / WaterReadsPerGateSample;
            float keepAbove = HighestWater(sky, day, activity.EarliestHour,
                                           Mathf.Min(24f, activity.LatestHour + GatherLeaveMarginHours),
                                           step) + activity.ClearanceMetres;

            uint seed = GatheringSeed(activity, day);
            float depth01 = Mathf.Lerp(activity.MinDepth01, activity.MaxDepth01,
                                       RoutineSchedule.Hash01(seed, StreamDepth, 0));
            int first = Mathf.Min(lines.Length - 1,
                                  (int)(RoutineSchedule.Hash01(seed, StreamGatherLine, 0) * lines.Length));
            int places = Mathf.Max(2, activity.GatherPlaces);
            float sample = activity.Beach.SampleStepMetres;

            for (int k = 0; k < lines.Length; k++)
            {
                BeachTransect line = lines[(first + k) % lines.Length];
                // The middle keeps the width of the ring back from the wet, so the seaward places are
                // dry too.
                if (!TryPlace(line, depth01, activity.GatherRadiusMetres, activity.MinDryMetres, terrain,
                              keepAbove, sample, out Vector2 at))
                    continue;

                bool room = terrain.ElevationAt(at) >= keepAbove;
                for (int p = 0; p < places && room; p++)
                    room = terrain.ElevationAt(GatherPlace(activity, at, p)) >= keepAbove;
                if (!room) continue;

                centre = at;
                return true;
            }
            return false;
        }

        /// <summary>Where the person of this <paramref name="rank"/> is, round <paramref name="centre"/>.</summary>
        public static Vector2 GatherPlace(BeachActivityDef activity, Vector2 centre, int rank)
        {
            int places = Mathf.Max(2, activity.GatherPlaces);
            int ring = Mathf.Max(0, rank) / places;
            // Shared out evenly, starting half a place round from the open side.
            float bearing = activity.GatherOpenBearingDegrees +
                            ((Mathf.Max(0, rank) % places) + 0.5f + ring * 0.5f) * (360f / places);
            float radians = bearing * Mathf.Deg2Rad;
            float radius = activity.GatherRadiusMetres + ring * activity.PersonSpacingMetres;
            return centre + new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * radius;
        }

        /// <summary>
        /// Vary <paramref name="authored"/> with at most one beach outing. Returns false (and the day is
        /// the authored day) when no activity's gates pass.
        ///
        /// <para>Only a block strictly inside the day is ever taken: never the first or the last. The
        /// day therefore still starts and ends where it was authored to, which is what lets a villager
        /// change from one day's plan to the next at midnight without moving.</para>
        /// </summary>
        /// <param name="rank">This villager's place among the people who could be on the same beach
        /// (0, 1, 2 ...). It spreads them over different lines so no two stand on one spot.</param>
        public static bool TryPlanDay(string routineId, RoutineStop[] authored, RoutineLanes lanes,
                                      float walkSpeedMetresPerSecond,
                                      IReadOnlyList<BeachActivityDef> activities, string regionId,
                                      int rank, ITidalTerrain terrain, IBeachSky sky, in BeachDay day,
                                      out RoutineStop[] stops, out BeachOuting outing)
        {
            stops = null;
            outing = default;
            if (authored == null || authored.Length < 3 || lanes == null || activities == null ||
                terrain == null || sky == null || day.SecondsPerGameHour <= 0f ||
                walkSpeedMetresPerSecond <= 0f)
                return false;

            // One gathering a day: the first in the library that is on today. The others are off for
            // everybody, so two rings never want the same sand and nobody sits at a fire alone while
            // the village is at supper further along.
            int gathering = -1;
            for (int a = 0; a < activities.Count && gathering < 0; a++)
            {
                BeachActivityDef activity = activities[a];
                if (activity != null && activity.IsRunnable && activity.Kind == BeachActivityKind.Gather &&
                    string.Equals(activity.Beach.RegionId, regionId, System.StringComparison.Ordinal) &&
                    GatheringIsOn(activity, day))
                    gathering = a;
            }

            for (int block = 1; block < authored.Length - 1; block++)
            {
                for (int a = 0; a < activities.Count; a++)
                {
                    BeachActivityDef activity = activities[a];
                    if (activity == null || !activity.IsRunnable) continue;
                    if (!string.Equals(activity.Beach.RegionId, regionId, System.StringComparison.Ordinal))
                        continue;
                    if (activity.Kind == BeachActivityKind.Gather && a != gathering) continue;
                    if (TryOuting(routineId, authored, block, lanes, walkSpeedMetresPerSecond, activity,
                                  rank, terrain, sky, day, out stops, out outing))
                        return true;
                }
            }
            return false;
        }

        /// <summary>Do the sea and sky allow <paramref name="activity"/> at this hour of the day.</summary>
        public static bool GatesPass(BeachActivityDef activity, IBeachSky sky, in BeachDay day,
                                     float hourOfDay)
        {
            double t = day.TimeAt(hourOfDay);
            float water = sky.WaterLevelAt(t);
            if (water < activity.MinWaterLevelMetres || water > activity.MaxWaterLevelMetres) return false;
            float wind = sky.WindStrengthAt(t);
            if (wind < activity.MinWindStrength || wind > activity.MaxWindStrength) return false;
            if (sky.SeaState01At(t) > activity.MaxSeaState01) return false;
            if (sky.Visibility01At(t) < activity.MinVisibility01) return false;
            return true;
        }

        /// <summary>The highest water between two hours of the day.</summary>
        public static float HighestWater(IBeachSky sky, in BeachDay day, float fromHour, float toHour,
                                         float stepHours)
        {
            if (stepHours <= 0f) stepHours = 1f / 60f;
            float highest = sky.WaterLevelAt(day.TimeAt(fromHour));
            for (float h = fromHour + stepHours; h < toHour; h += stepHours)
                highest = Mathf.Max(highest, sky.WaterLevelAt(day.TimeAt(h)));
            return Mathf.Max(highest, sky.WaterLevelAt(day.TimeAt(toHour)));
        }

        /// <summary>
        /// How far down <paramref name="line"/> the ground stays at or above
        /// <paramref name="keepAboveMetres"/>, walking from its top. False when even the top is too low.
        /// The walk stops at the FIRST low reading, so a dry bank out past a wet gully is not "dry":
        /// nobody is sent somewhere they would have to wade to.
        /// </summary>
        public static bool TryDrySpan(ITidalTerrain terrain, in BeachTransect line, float keepAboveMetres,
                                      float stepMetres, out float dryMetres)
        {
            dryMetres = 0f;
            if (terrain.ElevationAt(line.Top) < keepAboveMetres) return false;

            float length = Vector2.Distance(line.Top, line.Toe);
            if (length <= 0f) return true;
            if (stepMetres <= 0f) stepMetres = 0.25f;
            Vector2 dir = (line.Toe - line.Top) / length;

            for (float d = stepMetres; ; d += stepMetres)
            {
                float at = Mathf.Min(d, length);
                if (terrain.ElevationAt(line.Top + dir * at) < keepAboveMetres) return true;
                dryMetres = at;
                if (at >= length) return true;
            }
        }

        /// <summary>Is the straight line between two points on ground at or above
        /// <paramref name="keepAboveMetres"/> all the way.</summary>
        public static bool IsDryLeg(ITidalTerrain terrain, Vector2 from, Vector2 to, float keepAboveMetres,
                                    float stepMetres)
        {
            float length = Vector2.Distance(from, to);
            if (stepMetres <= 0f) stepMetres = 0.25f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / stepMetres));
            for (int i = 0; i <= steps; i++)
                if (terrain.ElevationAt(Vector2.Lerp(from, to, i / (float)steps)) < keepAboveMetres)
                    return false;
            return true;
        }

        // ---- one outing ---------------------------------------------------------------------------

        private static bool TryOuting(string routineId, RoutineStop[] authored, int block,
                                      RoutineLanes lanes, float walkSpeed, BeachActivityDef activity,
                                      int rank, ITidalTerrain terrain, IBeachSky sky, in BeachDay day,
                                      out RoutineStop[] stops, out BeachOuting outing)
        {
            stops = null;
            outing = default;

            RoutineStop taken = authored[block];
            if (taken.Activity != activity.Replaces) return false;
            // An indoor or out-of-sight block is somebody's kitchen or a shift nobody sees: not free
            // time on the green, whatever it is tagged.
            if (taken.Station.Indoors || taken.Station.OutOfSight) return false;
            if (!activity.Admits(routineId) || !activity.InSeason(day.Season)) return false;

            float departure = taken.DepartureHour;
            float next = authored[block + 1].DepartureHour;
            if (!(next > departure)) return false;

            uint person = RoutineSchedule.Finalize(RoutineSchedule.Fold(
                RoutineSchedule.Fold(RoutineSchedule.RoutineSeed(day.WorldSeed, routineId), activity.Id),
                day.DayIndex));
            // A gathering is on or off for everybody at once; anything else is each person deciding.
            bool together = activity.Kind == BeachActivityKind.Gather;
            if (together)
            {
                if (!GatheringIsOn(activity, day)) return false;
            }
            else if (RoutineSchedule.Hash01(person, StreamChance, block) >= activity.ChancePerDay) return false;

            BeachDef beach = activity.Beach;
            int node = lanes.IndexOfNode(beach.LaneNodeName);
            if (node < 0) return false;

            Vector2[] approach = beach.Approach != null && beach.Approach.Length > 0 ? beach.Approach : null;
            Vector2 nodeAt = lanes.NodePositions[node];
            Vector2 head = nodeAt;
            float approachMetres = 0f;
            if (approach != null)
                for (int i = 0; i < approach.Length; i++)
                {
                    approachMetres += Vector2.Distance(head, approach[i]);
                    head = approach[i];
                }

            if (!TryLaneMetres(authored[block - 1], beach.LaneNodeName, nodeAt, lanes, out float metresOut))
                return false;
            if (!TryLaneMetres(taken, beach.LaneNodeName, nodeAt, lanes, out float metresBack))
                return false;
            metresOut += approachMetres;
            metresBack += approachMetres;

            float perHour = day.SecondsPerGameHour;
            float atHead = departure + RoutineSchedule.TravelHours(metresOut, walkSpeed, perHour);
            if (atHead < activity.EarliestHour) return false;

            // ---- how long the sea and sky let them stay: from arriving to the first hour a gate fails.
            float hardEnd = Mathf.Min(next, activity.LatestHour);
            if (atHead >= hardEnd) return false;
            if (!GatesPass(activity, sky, day, atHead)) return false;

            float step = Mathf.Max(1f, activity.GateSampleMinutes) / 60f;
            float leave = atHead;
            while (leave < hardEnd)
            {
                float t = Mathf.Min(leave + step, hardEnd);
                if (!GatesPass(activity, sky, day, t)) break;
                leave = t;
            }

            bool cutShort = leave < next - 1e-4f;
            float minStay = activity.MinStayMinutes / 60f;
            float waterStep = step / WaterReadsPerGateSample;
            uint beachDay = RoutineSchedule.Finalize(RoutineSchedule.Fold(
                RoutineSchedule.RoutineSeed(day.WorldSeed, beach.Id), day.DayIndex));
            float depth01 = Mathf.Lerp(activity.MinDepth01, activity.MaxDepth01,
                                       RoutineSchedule.Hash01(person, StreamDepth, block));

            float offSand = RoutineSchedule.TravelHours(FarthestReach(activity, beach, head), walkSpeed,
                                                       perHour);

            Vector2 gatherAt = default;
            if (together && !TryGatheringCentre(activity, terrain, sky, day, out gatherAt)) return false;

            // Two passes at most. The first assumes that somebody whose stay was cut short walks back
            // to where their authored day had them. If there is no time for that walk before their next
            // block, the second keeps them on the beach until it (placed for the water of that longer
            // stay), because a walk that cannot finish would end in a jump.
            var beachStops = new List<RoutineStop>(4);
            for (int pass = 0; pass < 2; pass++)
            {
                bool goesBack = cutShort && pass == 0;
                float until = goesBack ? leave : next;
                // From reaching the head of the beach (the walk there is on the lanes, above every
                // tide) until they are off the sand again: the stay, one more gate sample, and the walk
                // back up from the farthest place the activity could have put them.
                float keepAbove = HighestWater(sky, day, atHead, until + step + offSand, waterStep)
                                  + activity.ClearanceMetres;

                beachStops.Clear();
                if (!TryPlaces(activity, beach, approach, head, depth01, rank, beachDay, terrain,
                               keepAbove, departure, metresOut, until, walkSpeed, perHour, gatherAt,
                               beachStops, out float arrival, out Vector2 last) ||
                    (activity.Kind == BeachActivityKind.Wade &&
                     // In the water only while the gates hold, even on an evening they stay on the
                     // sand afterwards because there is no time to walk back.
                     !TryWade(activity, beach, approach, rank, person, terrain, sky, day, waterStep,
                              arrival, Mathf.Min(until, leave), walkSpeed, perHour, beachStops)))
                {
                    if (goesBack) continue;
                    return false;
                }
                if (until - arrival < minStay)
                {
                    if (goesBack) continue;
                    return false;
                }

                if (goesBack)
                {
                    float back = RoutineSchedule.TravelHours(metresBack + Vector2.Distance(last, head),
                                                             walkSpeed, perHour);
                    if (leave + back > next) continue;   // no time to get back: stay (second pass)
                }

                int n = authored.Length;
                stops = new RoutineStop[n - 1 + beachStops.Count + (goesBack ? 1 : 0)];
                int w = 0;
                for (int i = 0; i < block; i++) stops[w++] = authored[i];
                int first = w;
                for (int i = 0; i < beachStops.Count; i++) stops[w++] = beachStops[i];
                if (goesBack)
                {
                    RoutineStop home = taken;
                    home.DepartureHour = leave;
                    stops[w++] = home;
                }
                for (int i = block + 1; i < n; i++) stops[w++] = authored[i];

                outing = new BeachOuting
                {
                    Activity = activity,
                    AuthoredBlock = block,
                    FirstStop = first,
                    StopCount = beachStops.Count,
                    DepartureHour = departure,
                    ArrivalHour = arrival,
                    LeaveHour = until,
                    GoesBackFirst = goesBack,
                    KeepAboveMetres = keepAbove,
                    GatherAt = gatherAt,
                };
                return true;
            }
            return false;
        }

        /// <summary>The farthest from the head of the beach any line of the activity reaches.</summary>
        private static float FarthestReach(BeachActivityDef activity, BeachDef beach, Vector2 head)
        {
            BeachTransect[] lines = LinesFor(activity, beach);
            float far = 0f;
            if (lines != null)
                for (int i = 0; i < lines.Length; i++)
                    far = Mathf.Max(far, Mathf.Max(Vector2.Distance(head, lines[i].Top),
                                                   Vector2.Distance(head, lines[i].Toe)));
            return far;
        }

        private static BeachTransect[] LinesFor(BeachActivityDef activity, BeachDef beach) =>
            activity.Kind == BeachActivityKind.Stroll ? beach.Promenade
            : activity.Kind == BeachActivityKind.Dig ? beach.Flats
            : activity.Kind == BeachActivityKind.Wade ? beach.Shallows
            : activity.Kind == BeachActivityKind.Gather ? beach.Gatherings
            : beach.Spots;

        /// <summary>
        /// The wade: from the dry place <see cref="TryPlaces"/> gave them (the one stop already in
        /// <paramref name="into"/>), in to the shallows, a few standing places that follow the water's
        /// edge as the tide moves it, and back up to the dry place for the rest of the stay.
        ///
        /// <para>Every place is found for the water AT THE TIME they stand there, and is only taken if
        /// the water over the ground stays at or under <see cref="BeachActivityDef.DeepestWadeMetres"/>
        /// from setting off for it until they could be back on the dry sand. False when not even one
        /// place can be taken: then this is not a wading day.</para>
        /// </summary>
        private static bool TryWade(BeachActivityDef activity, BeachDef beach, Vector2[] approach, int rank,
                                    uint person, ITidalTerrain terrain, IBeachSky sky, in BeachDay day,
                                    float waterStep, float arrival, float until, float walkSpeed,
                                    float perHour, List<RoutineStop> into)
        {
            if (into.Count != 1 || beach.Shallows == null || beach.Shallows.Length == 0) return false;

            Vector2 dry = into[0].Station.Position;
            // The same line TryPlaces chose: its id carries the index, but the dry place is on it, so
            // the nearest line is that line.
            BeachTransect line = beach.Shallows[0];
            float nearest = float.MaxValue;
            for (int i = 0; i < beach.Shallows.Length; i++)
            {
                float d = DistanceToSegment(dry, beach.Shallows[i].Top, beach.Shallows[i].Toe);
                if (d < nearest) { nearest = d; line = beach.Shallows[i]; }
            }

            float length = Vector2.Distance(line.Top, line.Toe);
            if (length <= 0f) return false;
            Vector2 down = (line.Toe - line.Top) / length;
            var alongShore = new Vector2(-down.y, down.x);
            float sample = beach.SampleStepMetres;
            float linger = Mathf.Max(1f, activity.LingerMinutes) / 60f;
            float deepest = activity.DeepestWadeMetres;

            float setOff = arrival + linger;                       // things down, shoes off
            float outBy = Mathf.Min(setOff + activity.WadeMinutes / 60f, until - linger);
            Vector2 last = dry;
            int places = 0;

            while (into.Count < MaxStrollStops - 1)
            {
                float side = (places % 2 == 0 ? -1f : 1f) * activity.WadeWanderMetres;
                Vector2 top = line.Top + alongShore * side;
                float want = Mathf.Lerp(activity.MinWadeDepthMetres, activity.MaxWadeDepthMetres,
                                        RoutineSchedule.Hash01(person, StreamWade, places));

                // Where the ground is `want` under the water at the middle of the time they stand
                // there. The time depends on the walk and the walk on the place: two rounds settle it.
                Vector2 place = last;
                float there = setOff;
                bool found = false;
                for (int round = 0; round < 2; round++)
                {
                    float level = sky.WaterLevelAt(day.TimeAt(there + linger * 0.5f)) - want;
                    found = false;
                    for (float d = 0f; d <= length; d += sample)
                    {
                        Vector2 p = top + down * d;
                        if (terrain.ElevationAt(p) > level) continue;
                        place = p;
                        found = true;
                        break;
                    }
                    if (!found) break;
                    there = setOff + RoutineSchedule.TravelHours(Vector2.Distance(last, place), walkSpeed,
                                                                 perHour);
                }
                if (!found) break;
                if (there + linger > outBy) break;

                // In over their depth at no moment: on the way in, standing, and on the way back out.
                float backOut = RoutineSchedule.TravelHours(Vector2.Distance(place, dry), walkSpeed, perHour);
                float highest = HighestWater(sky, day, setOff, there + linger + backOut, waterStep);
                if (!IsDryLeg(terrain, last, place, highest - deepest, sample) ||
                    !IsDryLeg(terrain, place, dry, highest - deepest, sample))
                    break;

                into.Add(Stop(activity, beach, approach, 0, into.Count, place, line.SeawardHeadingDegrees,
                              setOff));
                last = place;
                setOff = there + linger;
                places++;
            }

            if (places == 0) return false;
            into.Add(Stop(activity, beach, approach, 0, into.Count, dry, line.SeawardHeadingDegrees, setOff));
            return true;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>The places of one outing, as stops appended to <paramref name="into"/>.</summary>
        private static bool TryPlaces(BeachActivityDef activity, BeachDef beach, Vector2[] approach,
                                      Vector2 head, float depth01, int rank, uint beachDay,
                                      ITidalTerrain terrain, float keepAbove, float departure,
                                      float metresToHead, float until, float walkSpeed, float perHour,
                                      Vector2 gatherAt,
                                      List<RoutineStop> into, out float arrival, out Vector2 last)
        {
            arrival = departure;
            last = head;

            BeachTransect[] lines = LinesFor(activity, beach);
            if (lines == null || lines.Length == 0) return false;

            if (activity.Kind == BeachActivityKind.Gather)
            {
                // Their place in the ring, facing the middle. The place was found for the whole
                // gathering; it must also be dry for THIS stay and the walk to it.
                Vector2 at = GatherPlace(activity, gatherAt, rank);
                if (terrain.ElevationAt(at) < keepAbove ||
                    !IsDryLeg(terrain, head, at, keepAbove, beach.SampleStepMetres))
                    return false;

                Vector2 inward = gatherAt - at;
                float facing = Mathf.Atan2(inward.x, inward.y) * Mathf.Rad2Deg;
                if (facing < 0f) facing += 360f;

                arrival = departure + RoutineSchedule.TravelHours(metresToHead + Vector2.Distance(head, at),
                                                                  walkSpeed, perHour);
                into.Add(Stop(activity, beach, approach, rank, into.Count, at, facing, departure));
                last = at;
                return true;
            }

            int rotation = Mathf.Min(lines.Length - 1,
                (int)(RoutineSchedule.Hash01(beachDay, StreamRotation, (int)activity.Kind) * lines.Length));
            int index = ((rank % lines.Length) + rotation) % lines.Length;
            float sample = beach.SampleStepMetres;

            // Nobody stands on somebody else. People on a walk all use the same lines, so each keeps
            // their own distance up from the water's edge; people who each have a line only share one
            // when there are more of them than lines.
            // Walkers keep abreast: each has a lane of their own to one side of the authored line, so
            // two of them are apart whatever the tide does to either one's stay (their stays differ,
            // so "a little further up the sand" alone can put two people on one spot).
            bool stroll = activity.Kind == BeachActivityKind.Stroll;
            int lanesAbreast = Mathf.Max(1, activity.WalkersAbreast);
            int abreast = stroll ? rank / lanesAbreast : rank / lines.Length;
            float backOff = Mathf.Max(0, abreast) * activity.PersonSpacingMetres;
            float aside = stroll
                ? ((rank % lanesAbreast) - (lanesAbreast - 1) * 0.5f) * activity.PersonSpacingMetres
                : 0f;

            if (!TryPlace(Aside(lines[index], aside), depth01, backOff, activity.MinDryMetres, terrain,
                          keepAbove, sample, out Vector2 place))
                return false;
            if (!IsDryLeg(terrain, head, place, keepAbove, sample)) return false;

            arrival = departure + RoutineSchedule.TravelHours(metresToHead + Vector2.Distance(head, place),
                                                              walkSpeed, perHour);
            into.Add(Stop(activity, beach, approach, index, into.Count, place,
                          lines[index].SeawardHeadingDegrees, departure));
            last = place;

            if (activity.Kind != BeachActivityKind.Stroll || lines.Length < 2) return true;

            // ---- the walk along the tide line: out along the authored order, back again, and so on
            //      until it is time to leave. Each next point is only taken if the way to it, and the
            //      way off the beach from it, is dry for the whole stay.
            float linger = activity.LingerMinutes / 60f;
            float reached = arrival;
            int direction = 1;
            while (into.Count < MaxStrollStops)
            {
                // Carry on the way they were going; if that way is the end of the beach, or under water
                // today, turn round instead. Neither way open: the walk is over.
                int to = -1;
                Vector2 onward = last;
                for (int attempt = 0; attempt < 2 && to < 0; attempt++)
                {
                    int candidate = index + direction;
                    if (candidate >= 0 && candidate < lines.Length &&
                        TryPlace(Aside(lines[candidate], aside), depth01, backOff, activity.MinDryMetres, terrain,
                                 keepAbove, sample, out onward) &&
                        IsDryLeg(terrain, last, onward, keepAbove, sample) &&
                        IsDryLeg(terrain, onward, head, keepAbove, sample))
                        to = candidate;
                    else
                        direction = -direction;
                }
                if (to < 0) break;

                float setOff = reached + linger;
                float there = setOff + RoutineSchedule.TravelHours(Vector2.Distance(last, onward),
                                                                  walkSpeed, perHour);
                if (!(setOff > into[into.Count - 1].DepartureHour) || there + linger > until) break;

                into.Add(Stop(activity, beach, approach, to, into.Count, onward,
                              lines[to].SeawardHeadingDegrees, setOff));
                last = onward;
                reached = there;
                index = to;
            }
            return true;
        }

        /// <summary>The same line, moved sideways (along the shore) by <paramref name="metres"/>.</summary>
        private static BeachTransect Aside(in BeachTransect line, float metres)
        {
            if (metres == 0f) return line;
            Vector2 down = line.Toe - line.Top;
            float length = down.magnitude;
            if (length <= 0f) return line;
            Vector2 shift = new Vector2(-down.y, down.x) / length * metres;
            return new BeachTransect { Top = line.Top + shift, Toe = line.Toe + shift, Why = line.Why };
        }

        private static bool TryPlace(in BeachTransect line, float depth01, float backOffMetres,
                                     float minDryMetres,
                                     ITidalTerrain terrain, float keepAbove, float sample,
                                     out Vector2 place)
        {
            place = line.Top;
            if (!TryDrySpan(terrain, line, keepAbove, sample, out float dry)) return false;
            if (dry < minDryMetres) return false;
            float length = Vector2.Distance(line.Top, line.Toe);
            float down = Mathf.Max(0f, dry * Mathf.Clamp01(depth01) - backOffMetres);
            if (length > 0f) place = line.Top + (line.Toe - line.Top) / length * down;
            return true;
        }

        private static RoutineStop Stop(BeachActivityDef activity, BeachDef beach, Vector2[] approach,
                                        int line, int ordinal, Vector2 place, float heading,
                                        float departureHour)
        {
            return new RoutineStop
            {
                DepartureHour = departureHour,
                Station = new RoutineStationEntry
                {
                    Id = beach.Id + ":" + activity.Id + ":" + line + ":" + ordinal,
                    Position = place,
                    StandHeadingDegrees = heading,
                    LaneNodeName = beach.LaneNodeName,
                    Why = activity.DisplayName,
                },
                Activity = activity.Tag,
                Interruptible = true,
                Approach = approach,
                Strand = beach.Id,
            };
        }

        /// <summary>
        /// Metres of walking between a stop and a lane node, exactly as the planner will lay it (doors
        /// and all), found by asking the planner for a two-stop day and reading the leg.
        /// </summary>
        private static bool TryLaneMetres(in RoutineStop from, string nodeName, Vector2 nodeAt,
                                          RoutineLanes lanes, out float metres)
        {
            metres = 0f;
            var probe = new RoutineStop[2];
            probe[0] = from;
            probe[0].DepartureHour = 0f;
            probe[1] = new RoutineStop
            {
                DepartureHour = 12f,
                Station = new RoutineStationEntry { Id = "beach:probe", Position = nodeAt,
                                                    LaneNodeName = nodeName },
                Interruptible = true,
            };
            RoutinePlan plan = RoutinePlanner.BuildFromStops("beach:probe", probe, lanes, 1f, 1f, out _, out _);
            if (plan == null) return false;
            metres = plan.RouteLengthMetres[1];
            return true;
        }
    }
}
