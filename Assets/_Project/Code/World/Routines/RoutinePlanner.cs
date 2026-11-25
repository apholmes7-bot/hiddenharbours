using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Turns a <see cref="RoutineDef"/> plus a region's <see cref="RoutineStations"/> and
    /// <see cref="RoutineLanes"/> into the flat, sample-ready <see cref="RoutinePlan"/> — the geometry half
    /// of the routine engine, and the sibling of <c>AmbientFleetPlan</c> (which plans the fleet's grounds
    /// while <c>AmbientFleetSchedule</c> reads the clock). Everything time-shaped is in
    /// <see cref="RoutineSchedule"/>; everything shaped like a path is here.
    ///
    /// <para><b>It runs ONCE, at spawn.</b> Resolving a station id, walking a lane tree and measuring a
    /// polyline are all cheap, but none of them is free, and none of them changes: a day's routes are the
    /// same routes all day. So this allocates the plan's arrays and is never called again — the per-frame
    /// path is <see cref="RoutinePlan.SampleAt"/>, which allocates nothing (rule 7).</para>
    ///
    /// <para><b>Every failure is REPORTED and SURVIVABLE.</b> A station id that does not exist, a lane node
    /// that does not exist, a routine with one block — each returns no plan and a sentence naming the
    /// offender, and the villager keeps standing exactly where the builder put them. That is the
    /// null-safe-greybox rule applied to content: a half-authored routine must never be able to put
    /// somebody in the sea or throw halfway through a region load.</para>
    /// </summary>
    public static class RoutinePlanner
    {
        /// <summary>
        /// Build the plan. Returns null and fills <paramref name="problem"/> when the content cannot be
        /// walked; on success <paramref name="blockInteriors"/> carries the <see cref="BuildingInterior"/>
        /// of each block's destination (null for an outdoor one), which is what the runtime asks
        /// "is the player in here with me?".
        /// </summary>
        public static RoutinePlan Build(RoutineDef routine, RoutineStations stations, RoutineLanes lanes,
                                        int worldSeed, out BuildingInterior[] blockInteriors,
                                        out string problem)
        {
            blockInteriors = System.Array.Empty<BuildingInterior>();
            RoutineStop[] stops = ResolveStops(routine, stations, lanes, worldSeed, out problem);
            if (stops == null) return null;
            return BuildFromStops(routine.Id, stops, lanes, routine.WalkSpeedMetresPerSecond,
                                  routine.CatchUpSpeedMetresPerSecond, out blockInteriors, out problem);
        }

        /// <summary>
        /// The first half of <see cref="Build"/>: every block of the authored day resolved to a
        /// <see cref="RoutineStop"/> (its station found in the region's table, its departure hour
        /// jittered by this person's own seed). Returns null and a sentence naming the offender when the
        /// content cannot be walked.
        ///
        /// <para>Split out so a day can be VARIED without re-deriving it: a conditional day (the beach on
        /// a fine evening) is these same stops with one block replaced, handed to
        /// <see cref="BuildFromStops"/>. The untouched blocks keep the very hours they have on an
        /// ordinary day, because their jitter was computed here, once, from the authored table.</para>
        /// </summary>
        public static RoutineStop[] ResolveStops(RoutineDef routine, RoutineStations stations,
                                                 RoutineLanes lanes, int worldSeed, out string problem)
        {
            problem = string.Empty;

            if (routine == null) { problem = "no routine asset"; return null; }
            if (routine.Entries == null || routine.Entries.Length < 2)
            {
                problem = $"'{routine.Id}' has " +
                          $"{(routine.Entries == null ? 0 : routine.Entries.Length)} block(s); a day needs " +
                          "at least two, or it is a person standing still";
                return null;
            }
            if (stations == null) { problem = $"'{routine.Id}': the region has no station table"; return null; }
            if (lanes == null) { problem = $"'{routine.Id}': the region has no lane network"; return null; }

            string laneFault = lanes.Validate();
            if (!string.IsNullOrEmpty(laneFault))
            {
                problem = $"'{routine.Id}': the region's lane network is unwalkable — {laneFault}";
                return null;
            }

            int n = routine.Entries.Length;

            // ---- resolve every block's station and lane node up front, so a bad id fails before any
            //      geometry is built and the message names the block that is wrong.
            var stops = new RoutineStop[n];
            for (int i = 0; i < n; i++)
            {
                string id = routine.Entries[i].StationId;
                if (!stations.TryFind(id, out stops[i].Station))
                {
                    problem = $"'{routine.Id}' block {i} sends them to station '{id}', which this region " +
                              $"does not declare ({stations.Count} station(s) available)";
                    return null;
                }
                if (lanes.IndexOfNode(stops[i].Station.LaneNodeName) < 0)
                {
                    problem = $"'{routine.Id}' block {i}: station '{id}' hangs off lane node " +
                              $"'{stops[i].Station.LaneNodeName}', which the lane network does not have";
                    return null;
                }
                stops[i].Activity = routine.Entries[i].Activity;
                stops[i].Interruptible = routine.Entries[i].Interruptible;
            }

            // ---- the day's departure hours (authored, then jittered by this person's own seed).
            var authored = new float[n];
            for (int i = 0; i < n; i++) authored[i] = RoutineSchedule.Wrap24(routine.Entries[i].StartHour);

            uint seed = RoutineSchedule.RoutineSeed(worldSeed, routine.Id);
            for (int i = 0; i < n; i++)
                stops[i].DepartureHour = RoutineSchedule.DepartureHour(authored, i, seed,
                                                                       routine.ScheduleJitterMinutes);
            return stops;
        }

        /// <summary>
        /// The second half of <see cref="Build"/>: the geometry. Stop <c>i</c>'s route runs from stop
        /// <c>i − 1</c>'s station to its own, so the day closes on itself.
        ///
        /// <para>A stop may carry an <see cref="RoutineStop.Approach"/> (the ground between its lane node
        /// and the station, for a place the lanes do not reach) and a <see cref="RoutineStop.Strand"/>
        /// (two consecutive stops on one strand are walked straight between, never back through the
        /// lanes). A stop with neither is exactly a block of the authored day, and a day made only of
        /// those comes out exactly as it always has.</para>
        /// </summary>
        public static RoutinePlan BuildFromStops(string routineId, RoutineStop[] stops, RoutineLanes lanes,
                                                 float walkSpeedMetresPerSecond,
                                                 float catchUpSpeedMetresPerSecond,
                                                 out BuildingInterior[] blockInteriors, out string problem)
        {
            blockInteriors = System.Array.Empty<BuildingInterior>();
            problem = string.Empty;

            if (stops == null || stops.Length < 2)
            {
                problem = $"'{routineId}' has {(stops == null ? 0 : stops.Length)} stop(s); a day needs at " +
                          "least two";
                return null;
            }
            if (lanes == null) { problem = $"'{routineId}': the region has no lane network"; return null; }

            int n = stops.Length;
            var station = new RoutineStationEntry[n];
            var node = new int[n];
            var departures = new float[n];
            for (int i = 0; i < n; i++)
            {
                station[i] = stops[i].Station;
                departures[i] = stops[i].DepartureHour;
                node[i] = lanes.IndexOfNode(station[i].LaneNodeName);
                if (node[i] < 0)
                {
                    problem = $"'{routineId}' block {i}: station '{station[i].Id}' hangs off lane node " +
                              $"'{station[i].LaneNodeName}', which the lane network does not have";
                    return null;
                }
            }

            // ---- the routes. Block i runs from block (i−1)'s station to block i's, so the day closes:
            //      the last block's destination is where the first block sets off from.
            int nodeCount = lanes.NodeCount;
            var nodePathBuffer = new int[nodeCount * 2 + 2];
            var polylineBuffer = new Vector2[nodeCount + lanes.Via.Length + 8];

            var waypoints = new List<Vector2>(n * 8);
            var routeStart = new int[n];
            var routeCount = new int[n];
            var routeLength = new float[n];
            var activities = new RoutineActivity[n];
            var interruptible = new bool[n];
            var standHeading = new float[n];
            var exitAt = new float[n];
            var enterAt = new float[n];
            var interiors = new BuildingInterior[n];
            var hidden = new bool[n];

            for (int i = 0; i < n; i++)
            {
                int from = (i - 1 + n) % n;
                hidden[i] = station[i].OutOfSight;
                activities[i] = stops[i].Activity;
                interruptible[i] = stops[i].Interruptible;
                standHeading[i] = station[i].StandHeadingDegrees;
                interiors[i] = station[i].Interior;

                if (station[from].OutOfSight && station[i].OutOfSight)
                {
                    routeStart[i] = waypoints.Count;
                    waypoints.Add(station[i].Position);
                    routeCount[i] = 1;
                    continue;
                }

                // ALONG ONE STRAND: two places on the same stretch of beach are walked straight between.
                // Going back up to the lane and down again to move ten metres along the sand would be the
                // lane tree answering a question nobody asked it.
                if (!string.IsNullOrEmpty(stops[i].Strand) &&
                    string.Equals(stops[from].Strand, stops[i].Strand, System.StringComparison.Ordinal))
                {
                    int strandSlice = waypoints.Count;
                    routeStart[i] = strandSlice;
                    Append(waypoints, strandSlice, station[from].Position);
                    Append(waypoints, strandSlice, station[i].Position);
                    routeCount[i] = waypoints.Count - strandSlice;
                    continue;
                }

                int pathCount = RoutineLaneTree.WriteNodePath(lanes.NodeParents, node[from], node[i],
                                                              nodePathBuffer);
                if (pathCount < 0)
                {
                    problem = $"'{routineId}' block {i}: no lane path from '{station[from].LaneNodeName}' " +
                              $"to '{station[i].LaneNodeName}'";
                    return null;
                }

                int polyCount = RoutineLaneTree.WritePolyline(
                    lanes.NodeParents, lanes.NodePositions, lanes.ViaStart, lanes.ViaCount, lanes.Via,
                    nodePathBuffer, pathCount, polylineBuffer);
                if (polyCount < 0)
                {
                    problem = $"'{routineId}' block {i}: the lane polyline from " +
                              $"'{station[from].LaneNodeName}' to '{station[i].LaneNodeName}' overran its " +
                              "buffer — the lane table's via slices are inconsistent";
                    return null;
                }

                int slice = waypoints.Count;
                routeStart[i] = slice;

                // OUT of wherever they were: an indoor station is left through its own drawn door.
                float exitDistance = 0f;
                if (station[from].OutOfSight)
                {
                    Append(waypoints, slice, station[from].Position);
                    exitDistance = Vector2.Distance(station[from].Position, lanes.NodePositions[node[from]]);
                }
                else if (station[from].Indoors)
                {
                    Append(waypoints, slice, station[from].Position);
                    Vector2 door = station[from].Interior.DoorWorld;
                    Append(waypoints, slice, door);
                    exitDistance = Vector2.Distance(station[from].Position, door);
                }
                else
                {
                    Append(waypoints, slice, station[from].Position);
                }

                // BACK UP an approach, station end first (it is authored lane node → station).
                Vector2[] leave = stops[from].Approach;
                if (leave != null)
                    for (int k = leave.Length - 1; k >= 0; k--) Append(waypoints, slice, leave[k]);

                // ALONG the lanes.
                for (int k = 0; k < polyCount; k++) Append(waypoints, slice, polylineBuffer[k]);

                // DOWN an approach, lane end first.
                Vector2[] arrive = stops[i].Approach;
                if (arrive != null)
                    for (int k = 0; k < arrive.Length; k++) Append(waypoints, slice, arrive[k]);

                // IN to wherever they are going — through the real drawn door, never the wall centre.
                if (station[i].Indoors && !station[i].OutOfSight)
                {
                    Append(waypoints, slice, station[i].Interior.DoorWorld);
                    Append(waypoints, slice, station[i].Position);
                }
                else
                {
                    Append(waypoints, slice, station[i].Position);
                }

                routeCount[i] = waypoints.Count - routeStart[i];
                exitAt[i] = exitDistance;
            }

            // ---- lengths and thresholds, measured off the finished array (never off the pieces, so the
            //      duplicate-collapsing above cannot make the two disagree).
            Vector2[] flat = waypoints.ToArray();
            for (int i = 0; i < n; i++)
            {
                routeLength[i] = RoutineLaneTree.PolylineLength(flat, routeStart[i], routeCount[i]);

                if (station[i].OutOfSight)
                {
                    float nodeToStation = Vector2.Distance(lanes.NodePositions[node[i]], station[i].Position);
                    enterAt[i] = Mathf.Clamp(routeLength[i] - nodeToStation, 0f, routeLength[i]);
                }
                else if (station[i].Indoors)
                {
                    // The threshold is the door, which is the last-but-one point of the route: they are
                    // inside from there to the end. Measured from the STATION geometry rather than by
                    // indexing the array, so a door that collapsed onto the room point (a mis-measured
                    // anchor) narrows the crossing rather than reading the wrong pair of points.
                    float doorToInside =
                        Vector2.Distance(station[i].Interior.DoorWorld, station[i].Position);
                    enterAt[i] = Mathf.Clamp(routeLength[i] - doorToInside, 0f, routeLength[i]);
                }
                else
                {
                    enterAt[i] = float.PositiveInfinity;   // never crosses in — there is nothing to cross
                }

                // A door that collapsed onto the room point (a mis-measured anchor) would make the exit
                // threshold zero-width; clamp it inside the route so it can never exceed the walk.
                exitAt[i] = Mathf.Clamp(exitAt[i], 0f, routeLength[i]);
            }

            blockInteriors = interiors;
            return new RoutinePlan(departures, flat, routeStart, routeCount, routeLength, activities,
                                   standHeading, exitAt, enterAt, walkSpeedMetresPerSecond,
                                   interruptible, catchUpSpeedMetresPerSecond, hidden);
        }

        /// <summary>
        /// Append unless it repeats the previous point of <b>this route</b> — the same 1 mm rule
        /// <see cref="RoutineLaneTree"/> uses, for the same reason: a dooryard that IS its lane node would
        /// otherwise leave a zero-length hop in the middle of a walk.
        ///
        /// <para><b>⭐ <paramref name="sliceStart"/> is load-bearing, and leaving it out was a real bug.</b>
        /// The routes are flattened into one array, so "the previous point" without a slice bound is the last
        /// point of the PREVIOUS block's route — and a block almost always sets off from exactly where the
        /// last one arrived. So the collapse fired across the boundary and silently dropped each route's
        /// FIRST waypoint: the slice started one point late, and at the departure hour the villager
        /// teleported from where she was standing to the second point of her walk. Measured on the test rig
        /// as a 20 m jump. Caught by <c>WhileWalking_TheFacingFollowsTheRoute</c> (the first segment's
        /// bearing came back 90° out) and pinned since by
        /// <c>EveryBlockSetsOffFromWhereTheLastOneArrived</c>.</para>
        /// </summary>
        static void Append(List<Vector2> into, int sliceStart, Vector2 p)
        {
            const float SameSq = 1e-6f;
            if (into.Count > sliceStart && (into[into.Count - 1] - p).sqrMagnitude <= SameSq) return;
            into.Add(p);
        }
    }
}
