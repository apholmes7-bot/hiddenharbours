using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>The tunables the whole scheme is spaced and gated by. No magic numbers live in the
    /// planner (rule 6); every one of these is a knob the owner can turn in the region's own table.</summary>
    [Serializable]
    public sealed class NavMarkTuning
    {
        /// <summary>⭐ THE FLOOR. A mark must have MORE water than this under it at the LOWEST water
        /// of the biggest spring tide. A floating mark standing on bared mud reads as a bug, not as
        /// a harbour, so a station that cannot clear this gets no mark at all.</summary>
        public float MinDepthAtSpringLowMetres = 0.6f;

        /// <summary>How far apart singles stand along a straight (m). Pairs go at the ends and the
        /// turns regardless — this only paces the run between them.</summary>
        public float StraightSpacingMetres = 90f;

        /// <summary>A vertex counts as a TURN, and so earns a pair, when the course changes by more
        /// than this (degrees).</summary>
        public float TurnThresholdDegrees = 20f;

        /// <summary>Step (m) the thalweg search walks across the corridor. Finer finds a narrower
        /// gut; coarser is cheaper. The corridor is <see cref="NavChannel.SearchHalfWidthMetres"/>.</summary>
        public float SnapStepMetres = 2f;

        /// <summary>How far (m) the derived centreline may slew from one station to the next. Stops
        /// a per-station "deepest point" search from zig-zagging a fairway no boat would sail.</summary>
        public float MaxSnapSlewMetres = 8f;

        /// <summary>Step (m) the navigability check walks the derived centreline at. Coarse enough
        /// to be cheap, fine enough that a 15 m gut cannot hide between two samples.</summary>
        public float FairwayProbeStepMetres = 5f;
    }

    /// <summary>One mark the plan wants placed. A pure record — no scene, no asset, no component.</summary>
    public struct PlannedNavMark
    {
        /// <summary>The channel or danger this mark belongs to (its id).</summary>
        public string OwnerId;
        /// <summary>Stable per-mark id, derived from the owner and the station.</summary>
        public string Id;
        /// <summary>The kit's own type key — <c>PortCan</c>, <c>StbdLit</c>, <c>CardinalN</c>…</summary>
        public string MarkType;
        /// <summary>Which rung of the size ladder.</summary>
        public string SizeId;
        /// <summary>Where it floats (world XY).</summary>
        public Vector2 At;
        /// <summary>Which of the 8 baked facings — DERIVED from the bearing a skipper meets it on.</summary>
        public int Facing;
        /// <summary>Water over the seabed here at the LOWEST water of the biggest spring tide (m).</summary>
        public float SpringLowDepthMetres;
        /// <summary>Which hand of the channel it stands on. Meaningless for a cardinal.</summary>
        public NavChannelHand Hand;
        /// <summary>True for a lateral (a channel edge), false for a cardinal.</summary>
        public bool IsLateral;
        /// <summary>Distance (m) along the derived centreline from the SEAWARD end.</summary>
        public float AlongMetres;
    }

    /// <summary>A mark the plan wanted and would not ship, and why. Logged on every build so a
    /// refusal cannot go quiet.</summary>
    public struct NavMarkRefusal
    {
        public string OwnerId;
        public Vector2 At;
        public string Reason;
    }

    /// <summary>The derived centreline of one channel, station by station — what the marks were hung
    /// off and what the fairway test measures.</summary>
    public sealed class NavChannelFairway
    {
        public string ChannelId;
        /// <summary>The centreline AFTER the seabed had its say. Same count as
        /// <see cref="AuthoredStations"/>, station for station.</summary>
        public List<Vector2> Stations = new List<Vector2>();
        /// <summary>The line as authored, before the snap — kept so a test can prove the snap only
        /// ever found DEEPER water.</summary>
        public List<Vector2> AuthoredStations = new List<Vector2>();
        /// <summary>Distance along the authored polyline of each station (m).</summary>
        public List<float> Along = new List<float>();
        /// <summary>Course (unit) at each station, seaward → harbour.</summary>
        public List<Vector2> Course = new List<Vector2>();
    }

    /// <summary>Everything one region's nav-mark pass produced.</summary>
    public sealed class NavMarkPlanResult
    {
        public List<PlannedNavMark> Marks = new List<PlannedNavMark>();
        public List<NavMarkRefusal> Refusals = new List<NavMarkRefusal>();
        public List<NavChannelFairway> Fairways = new List<NavChannelFairway>();

        /// <summary>The fairway of a channel by id, or null.</summary>
        public NavChannelFairway Fairway(string channelId)
        {
            for (int i = 0; i < Fairways.Count; i++)
                if (string.Equals(Fairways[i].ChannelId, channelId, StringComparison.Ordinal))
                    return Fairways[i];
            return null;
        }
    }

    /// <summary>
    /// <b>Where the marks go — derived, never typed.</b> Feed it the authored channels, the authored
    /// dangers and the SAME seabed the sim reads, and it answers with a list of marks: what type,
    /// what size, which facing, and how much water each one floats in at the worst tide of the month.
    ///
    /// <para><b>⭐ IT PLACES NOTHING AND DRAWS NOTHING.</b> This is a pure function of (channels,
    /// dangers, elevation, tide, tuning) — a POCO with no Unity object in sight — so an EditMode test
    /// can assert the whole scheme headlessly and the builder that instantiates the prefabs has no
    /// arithmetic of its own to get wrong. That split is what lets the sabotage test (flip a
    /// channel's direction of buoyage and watch the colours swap) run in milliseconds.</para>
    ///
    /// <para><b>⭐ THE ELEVATION SOURCE IS THE SIM'S OWN.</b> The caller passes
    /// <c>ITidalTerrain.ElevationAt</c> — the same read the boat's depth gate makes — so "paint = sail"
    /// (ADR 0014) extends to "paint = sail = marked". If the owner paints a deeper gut, the marks move
    /// to it on the next build; if he fills one in, the marks that no longer float are REFUSED rather
    /// than left standing on mud.</para>
    ///
    /// <para><b>⚠ REFUSED, NEVER NUDGED.</b> A mark that cannot float where the geometry wants it is
    /// dropped and recorded in <see cref="NavMarkPlanResult.Refusals"/>. Quietly sliding it into
    /// deeper water would put a channel edge somewhere the channel's edge is not, which is worse than
    /// a missing buoy — the same argument <c>NineMileCreekMooredFleet</c> makes about berths.</para>
    /// </summary>
    public static class NavMarkPlan
    {
        /// <summary>
        /// Plan one region's marks.
        /// </summary>
        /// <param name="channels">Authored fairways, each carrying its own direction of buoyage.</param>
        /// <param name="cardinals">Authored dangers and the quadrant that guards each.</param>
        /// <param name="elevationAt">The sim's seabed read (m above chart datum).</param>
        /// <param name="tideMean">The region's mean water level (m above datum).</param>
        /// <param name="tideAmplitude">The region's spring amplitude (m).</param>
        /// <param name="tuning">Spacing and floors. Null takes the defaults.</param>
        public static NavMarkPlanResult Plan(
            IReadOnlyList<NavChannel> channels,
            IReadOnlyList<NavCardinal> cardinals,
            Func<Vector2, float> elevationAt,
            float tideMean,
            float tideAmplitude,
            NavMarkTuning tuning = null)
        {
            var result = new NavMarkPlanResult();
            if (elevationAt == null) return result;
            tuning = tuning ?? new NavMarkTuning();
            float springLow = tideMean - tideAmplitude;

            if (channels != null)
                for (int i = 0; i < channels.Count; i++)
                    PlanChannel(channels[i], elevationAt, springLow, tuning, result);

            if (cardinals != null)
                for (int i = 0; i < cardinals.Count; i++)
                    PlanCardinal(cardinals[i], elevationAt, springLow, tuning, result);

            return result;
        }

        /// <summary>Water over the seabed at a position for a given water level (m). Negative = dry.</summary>
        public static float DepthAt(Func<Vector2, float> elevationAt, Vector2 at, float waterLevel) =>
            waterLevel - elevationAt(at);

        // =============================================================================================
        //  CHANNELS
        // =============================================================================================

        private static void PlanChannel(NavChannel channel, Func<Vector2, float> elevationAt,
                                        float springLow, NavMarkTuning tuning, NavMarkPlanResult result)
        {
            if (channel == null || channel.Waypoints == null || channel.Waypoints.Length < 2) return;

            NavChannelFairway fairway = DeriveFairway(channel, elevationAt, tuning);
            result.Fairways.Add(fairway);

            // Which stations earn a PAIR: the two ends and every turn. A straight's stations get a
            // single, alternating hands, so a run reads as a channel without becoming a picket fence.
            bool[] pair = PairStations(channel, fairway, tuning);

            int singleCount = 0;
            for (int s = 0; s < fairway.Stations.Count; s++)
            {
                Vector2 centre = fairway.Stations[s];
                Vector2 course = fairway.Course[s];

                // The landfall pair — the first station, the one you meet coming in from sea — is the
                // only one that carries a light. Derived from the position in the list, so re-siting
                // the seaward end moves the light with it.
                bool lit = channel.LitLandfall && s == 0;

                if (pair[s])
                {
                    TryPlaceLateral(channel, fairway, s, centre, course, NavChannelHand.Port, lit,
                                    elevationAt, springLow, tuning, result);
                    TryPlaceLateral(channel, fairway, s, centre, course, NavChannelHand.Starboard, lit,
                                    elevationAt, springLow, tuning, result);
                }
                else
                {
                    // Alternate, starting to starboard: the red side is the one a skipper coming home
                    // holds to, so a lone mark on a straight is more useful there.
                    NavChannelHand hand = (singleCount % 2 == 0) ? NavChannelHand.Starboard : NavChannelHand.Port;
                    singleCount++;
                    TryPlaceLateral(channel, fairway, s, centre, course, hand, lit,
                                    elevationAt, springLow, tuning, result);
                }
            }
        }

        /// <summary>
        /// Stations along the authored route: both ends, every turn, and a paced sequence down each
        /// straight. Distances are measured on the AUTHORED polyline (the snap moves points sideways,
        /// not along), so spacing stays what the owner asked for.
        /// </summary>
        private static List<float> StationDistances(NavChannel channel, NavMarkTuning tuning,
                                                    out List<bool> isCorner)
        {
            var distances = new List<float>();
            isCorner = new List<bool>();

            Vector2[] w = channel.Waypoints;
            float total = NavChannelGeometry.PolylineLength(w);

            // The channel's own pacing if it states one, else the region's. See NavChannel for why a
            // gut needs to be able to say "no singles".
            float spacing = channel.StraightSpacingMetres > 0f
                ? channel.StraightSpacingMetres
                : tuning.StraightSpacingMetres;

            // Vertex distances, and whether each is a real turn.
            var vertexAlong = new List<float> { 0f };
            float run = 0f;
            for (int i = 1; i < w.Length; i++)
            {
                run += Vector2.Distance(w[i - 1], w[i]);
                vertexAlong.Add(run);
            }

            for (int v = 0; v < vertexAlong.Count; v++)
            {
                bool corner = v == 0 || v == vertexAlong.Count - 1;
                if (!corner)
                {
                    Vector2 inbound = (w[v] - w[v - 1]).normalized;
                    Vector2 outbound = (w[v + 1] - w[v]).normalized;
                    corner = Vector2.Angle(inbound, outbound) > tuning.TurnThresholdDegrees;
                }
                if (!corner) continue;

                distances.Add(vertexAlong[v]);
                isCorner.Add(true);
            }

            // Fill the straights between consecutive marked stations.
            var filledDistances = new List<float>();
            var filledCorners = new List<bool>();
            for (int i = 0; i < distances.Count; i++)
            {
                filledDistances.Add(distances[i]);
                filledCorners.Add(isCorner[i]);
                if (i == distances.Count - 1) break;

                float a = distances[i];
                float b = distances[i + 1];
                float gap = b - a;
                if (gap <= spacing * 1.5f) continue;

                int steps = Mathf.Max(1, Mathf.RoundToInt(gap / spacing) - 1);
                for (int k = 1; k <= steps; k++)
                {
                    filledDistances.Add(a + gap * k / (steps + 1f));
                    filledCorners.Add(false);
                }
            }

            // Keep them ordered seaward → harbour; the fill appended out of order by design.
            var order = new List<int>();
            for (int i = 0; i < filledDistances.Count; i++) order.Add(i);
            order.Sort((x, y) => filledDistances[x].CompareTo(filledDistances[y]));

            var outDistances = new List<float>();
            var outCorners = new List<bool>();
            for (int i = 0; i < order.Count; i++)
            {
                outDistances.Add(Mathf.Clamp(filledDistances[order[i]], 0f, total));
                outCorners.Add(filledCorners[order[i]]);
            }
            isCorner = outCorners;
            return outDistances;
        }

        private static bool[] PairStations(NavChannel channel, NavChannelFairway fairway, NavMarkTuning tuning)
        {
            StationDistances(channel, tuning, out List<bool> corners);
            var pair = new bool[fairway.Stations.Count];
            for (int i = 0; i < pair.Length && i < corners.Count; i++) pair[i] = corners[i];
            return pair;
        }

        /// <summary>
        /// ⭐ <b>THE ROUTE IS A PROPOSAL; THE SEABED DECIDES — but only where the seabed has something
        /// to say.</b> Each station samples the bed across its corridor and moves to the deepest water
        /// it finds there, clamped so the line cannot slew more than
        /// <see cref="NavMarkTuning.MaxSnapSlewMetres"/> from the station before it.
        ///
        /// <para><b>⚠⚠ A SLOPE IS NOT A GUT, AND A NAIVE DEEPEST-POINT SEARCH CANNOT TELL THEM
        /// APART.</b> This is the defect the first draft shipped and the probe caught. Every approach
        /// in this world runs at a shore, so the bed under it SLOPES: at any station off St Peters the
        /// deepest water in a ±18 m corridor is simply the sample furthest from the island, and a
        /// search that took it dragged the fairway sideways off the slip it was supposed to reach —
        /// downhill, station after station, exactly like water. The discriminator is where the minimum
        /// SITS: a real gut has its deepest point in the MIDDLE of the corridor, a slope has it at the
        /// EDGE. So a minimum found on either edge sample is declined and the authored line stands.
        /// That is not a fudge — it is the difference between "follow the painted channel" and "walk
        /// downhill", and only one of those is what a fairway does.</para>
        ///
        /// <para><b>⚠ THE END STATIONS DO NOT MOVE.</b> The landfall and the destination are
        /// DECLARATIONS — where the owner says the channel begins and what it is for — not proposals.
        /// A snap that slid the last station off the slip would have produced a channel that goes
        /// almost home.</para>
        ///
        /// <para>On a flat approach every candidate ties, the tie-break to the authored line wins, and
        /// the route comes out exactly as drawn. That is the correct answer, not a broken search:
        /// where the owner has not painted a gut there is no gut to follow, and the log says so.</para>
        /// </summary>
        public static NavChannelFairway DeriveFairway(NavChannel channel, Func<Vector2, float> elevationAt,
                                                      NavMarkTuning tuning)
        {
            tuning = tuning ?? new NavMarkTuning();
            var fairway = new NavChannelFairway { ChannelId = channel.Id };

            List<float> stations = StationDistances(channel, tuning, out _);
            float previousOffset = 0f;

            for (int i = 0; i < stations.Count; i++)
            {
                Vector2 authored = NavChannelGeometry.PointAlong(channel.Waypoints, stations[i], out Vector2 course);
                Vector2 port = NavChannelGeometry.PortNormal(course);

                bool isEnd = i == 0 || i == stations.Count - 1;
                float offset = isEnd
                    ? 0f
                    : SnapOffset(authored, port, channel, elevationAt, tuning, previousOffset);

                previousOffset = offset;
                fairway.AuthoredStations.Add(authored);
                fairway.Stations.Add(authored + port * offset);
                fairway.Along.Add(stations[i]);
                fairway.Course.Add(course);
            }

            return fairway;
        }

        /// <summary>
        /// How far across its corridor one station should move to sit in the deepest water — 0 when
        /// the corridor is a slope rather than a gut. See <see cref="DeriveFairway"/> for why the
        /// edge test is the whole algorithm.
        /// </summary>
        public static float SnapOffset(Vector2 authored, Vector2 port, NavChannel channel,
                                       Func<Vector2, float> elevationAt, NavMarkTuning tuning,
                                       float previousOffset)
        {
            float search = Mathf.Max(0f, channel.SearchHalfWidthMetres);
            if (search <= 0f) return 0f;

            float step = Mathf.Max(0.25f, tuning.SnapStepMetres);
            int half = Mathf.Max(1, Mathf.RoundToInt(search / step));

            // Sample the whole corridor symmetrically, so "was the minimum on an edge?" is a question
            // about the SEABED and not about where a clamp happened to stop the loop.
            int count = half * 2 + 1;
            int best = half;                                   // the authored line, index of offset 0
            float bestElevation = elevationAt(authored);

            for (int k = 0; k < count; k++)
            {
                float o = (k - half) * step;
                float e = elevationAt(authored + port * o);
                if (e < bestElevation - 1e-4f ||
                    (Mathf.Abs(e - bestElevation) <= 1e-4f && Mathf.Abs(o) < Mathf.Abs((best - half) * step)))
                {
                    bestElevation = e;
                    best = k;
                }
            }

            // ⚠ The minimum sits on an edge → this corridor is a slope, not a channel. Decline it.
            if (best == 0 || best == count - 1) return 0f;

            float offset = (best - half) * step;
            return Mathf.Clamp(offset,
                               previousOffset - tuning.MaxSnapSlewMetres,
                               previousOffset + tuning.MaxSnapSlewMetres);
        }

        private static void TryPlaceLateral(NavChannel channel, NavChannelFairway fairway, int station,
                                            Vector2 centre, Vector2 course, NavChannelHand hand, bool lit,
                                            Func<Vector2, float> elevationAt, float springLow,
                                            NavMarkTuning tuning, NavMarkPlanResult result)
        {
            Vector2 at = centre + NavChannelGeometry.Normal(course, hand) * channel.HalfWidthMetres;
            float depth = DepthAt(elevationAt, at, springLow);

            if (depth <= tuning.MinDepthAtSpringLowMetres)
            {
                result.Refusals.Add(new NavMarkRefusal
                {
                    OwnerId = channel.Id,
                    At = at,
                    Reason = $"{hand} mark at station {station} would stand in {depth:0.00} m at spring " +
                             $"low (floor {tuning.MinDepthAtSpringLowMetres:0.00} m) — it would sit on " +
                             "bared ground for part of every big tide. Refused, not nudged.",
                });
                return;
            }

            // ⚠️ The facing is the bearing a skipper MEETS the mark on — i.e. looking back down the
            // channel toward the sea she came from. Derived from the course, so re-routing turns every
            // mark on the leg. The kit is CLOCKWISE; see NavChannelGeometry.FacingCell.
            float bearing = NavChannelGeometry.CompassBearing(-course);

            result.Marks.Add(new PlannedNavMark
            {
                OwnerId = channel.Id,
                Id = $"{channel.Id}.{(hand == NavChannelHand.Port ? "p" : "s")}{station}",
                MarkType = NavChannelGeometry.LateralMarkType(hand, lit),
                SizeId = channel.SizeId,
                At = at,
                Facing = NavChannelGeometry.FacingCell(bearing),
                SpringLowDepthMetres = depth,
                Hand = hand,
                IsLateral = true,
                AlongMetres = fairway.Along[station],
            });
        }

        // =============================================================================================
        //  CARDINALS
        // =============================================================================================

        /// <summary>
        /// A cardinal is only worth placing if the seabed agrees with it: the SAFE side must be
        /// genuinely deeper than the danger side. Probed rather than trusted, because a quadrant typed
        /// beside a coordinate is exactly the kind of thing that survives a re-site of the danger.
        /// </summary>
        public static bool QuadrantAgreesWithSeabed(NavCardinal cardinal, Func<Vector2, float> elevationAt,
                                                    out float safeElevation, out float dangerElevation)
        {
            Vector2 safeDir = NavChannelGeometry.SafeDirection(cardinal.Quadrant);
            safeElevation = elevationAt(cardinal.At + safeDir * cardinal.ProbeMetres);
            dangerElevation = elevationAt(cardinal.At - safeDir * cardinal.ProbeMetres);
            return safeElevation < dangerElevation;
        }

        private static void PlanCardinal(NavCardinal cardinal, Func<Vector2, float> elevationAt,
                                         float springLow, NavMarkTuning tuning, NavMarkPlanResult result)
        {
            if (cardinal == null) return;

            float depth = DepthAt(elevationAt, cardinal.At, springLow);
            if (depth <= tuning.MinDepthAtSpringLowMetres)
            {
                result.Refusals.Add(new NavMarkRefusal
                {
                    OwnerId = cardinal.Id,
                    At = cardinal.At,
                    Reason = $"cardinal '{cardinal.DisplayName}' would stand in {depth:0.00} m at spring " +
                             $"low (floor {tuning.MinDepthAtSpringLowMetres:0.00} m). Refused — a mark on " +
                             "dry ground guards nothing.",
                });
                return;
            }

            if (!QuadrantAgreesWithSeabed(cardinal, elevationAt, out float safe, out float danger))
            {
                result.Refusals.Add(new NavMarkRefusal
                {
                    OwnerId = cardinal.Id,
                    At = cardinal.At,
                    Reason = $"cardinal '{cardinal.DisplayName}' is authored {cardinal.Quadrant}, but " +
                             $"{cardinal.ProbeMetres:0} m to its {cardinal.Quadrant} the bed is {safe:0.00} m " +
                             $"and on the far side {danger:0.00} m — the SAFE side is the shallower one. " +
                             "Refused: it would send a skipper onto the thing it is guarding.",
                });
                return;
            }

            // A cardinal has no channel to look down, so it faces its own safe quadrant — the side a
            // skipper passes it on, and therefore the side she sees.
            float bearing = NavChannelGeometry.CompassBearing(NavChannelGeometry.SafeDirection(cardinal.Quadrant));

            result.Marks.Add(new PlannedNavMark
            {
                OwnerId = cardinal.Id,
                Id = cardinal.Id,
                MarkType = NavChannelGeometry.CardinalMarkType(cardinal.Quadrant),
                SizeId = cardinal.SizeId,
                At = cardinal.At,
                Facing = NavChannelGeometry.FacingCell(bearing),
                SpringLowDepthMetres = depth,
                Hand = NavChannelHand.Port,
                IsLateral = false,
                AlongMetres = 0f,
            });
        }

        // =============================================================================================
        //  THE CLAIM A ROUTE MAKES
        // =============================================================================================

        /// <summary>
        /// Does the derived fairway actually carry what the channel claims — the declared draught,
        /// plus its keel clearance, at the declared state of tide? Walks the whole centreline, not
        /// just the stations, so a bar between two marks cannot hide.
        ///
        /// <para>⚠ <b>This is the test that stops a channel marking mud as safe.</b> It is asserted at
        /// the tide the route CLAIMS rather than at low water, because a channel into a drying harbour
        /// is a real channel that is simply not open all day — pretending otherwise would delete both
        /// harbours in this world.</para>
        /// </summary>
        public static bool FairwayCarriesItsClaim(NavChannel channel, NavChannelFairway fairway,
                                                  Func<Vector2, float> elevationAt,
                                                  float tideMean, float tideAmplitude,
                                                  NavMarkTuning tuning,
                                                  out Vector2 worstAt, out float worstDepth)
        {
            tuning = tuning ?? new NavMarkTuning();
            float water = tideMean + channel.NavigableTideFraction * tideAmplitude;
            float needed = channel.DeclaredDraughtMetres + channel.KeelClearanceMetres;

            worstAt = Vector2.zero;
            worstDepth = float.MaxValue;
            if (fairway == null || fairway.Stations.Count < 2) return false;

            float step = Mathf.Max(0.5f, tuning.FairwayProbeStepMetres);
            for (int i = 1; i < fairway.Stations.Count; i++)
            {
                Vector2 a = fairway.Stations[i - 1];
                Vector2 b = fairway.Stations[i];
                float leg = Vector2.Distance(a, b);
                int samples = Mathf.Max(1, Mathf.CeilToInt(leg / step));
                for (int k = 0; k <= samples; k++)
                {
                    Vector2 p = Vector2.Lerp(a, b, k / (float)samples);
                    float d = DepthAt(elevationAt, p, water);
                    if (d < worstDepth) { worstDepth = d; worstAt = p; }
                }
            }

            return worstDepth >= needed;
        }
    }
}
