using System;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The PURE, engine-light <b>planner</b> for the ambient fisher fleet (canon M2-33, P3): where each
    /// NPC boat's buoy spots are, derived deterministically from
    /// <c>(worldSeed, fleetId, boatIndex, dayIndex)</c> and gated by the same painted seabed the whole
    /// shoreline reads. Split out (like <c>TrapPlacement</c>/<c>TrapSoak</c>) so every rule is
    /// EditMode-testable headless — pure functions, no RNG object, nothing saved (rule 5): the same
    /// seed and day always plan the same grounds, this run and every future run.
    ///
    /// <para><b>The depth gate — safe at EVERY tide, by construction.</b> Spots and the travel legs
    /// between them are accepted only where <c>minWaterLevel − elevation ≥ minDepthMeters</c>, where
    /// <c>minWaterLevel</c> is the LOWEST water the tide can ever reach (spring low,
    /// <c>TideProfile.MeanLevel − Amplitude</c> — the tide model's hard floor). Because every other
    /// tide phase sits at or above that floor, a planned route can never be stranded by a falling tide
    /// — the exact inverse of how <c>TrapPlacement</c> depth-gates the player's pots, made
    /// tide-proof instead of tide-of-the-moment. The live steering adds a look-ahead probe on top
    /// (<see cref="AmbientFleetSteering.DepthAvoid"/>) for detours pushed off the planned route.</para>
    ///
    /// <para><b>Seeding.</b> FNV-1a + avalanche over the raw input facts — the same process-stable
    /// constants as the Fishing lane's <c>StableHash</c> and the clam scatter (<c>StPetersBuilder.Hash01</c>);
    /// re-derived here because Boats may not reference the Fishing module (rule 4) and the helper is a
    /// dozen lines. No <c>UnityEngine.Random</c>, no <c>string.GetHashCode</c> (process-randomized).</para>
    ///
    /// <para><b>Two ways to say where.</b> The original <c>PlanFleet(…, Rect grounds, …)</c> draws spots
    /// inside one rectangle. Since #886 the presenter plans on the ACCESSIBLE water instead
    /// (<see cref="PlanFleetDay"/>, over an <see cref="AmbientFleetWater"/>): every spot joins the open water
    /// the player arrives by, and every spot and leg keeps clear of the places the player works. The
    /// rectangle overload is kept as it was, with its tests.</para>
    /// </summary>
    public static class AmbientFleetPlan
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        // Never relax the depth margin below "there is water at all".
        private const float MarginFloor = 0.05f;

        // ---- deterministic hashing (process-stable — mirrors Fishing.StableHash) -----------------

        /// <summary>Fold a string into the running FNV-1a hash (UTF-16 code units, low byte then high).</summary>
        public static uint Fold(uint hash, string s)
        {
            unchecked
            {
                if (s != null)
                {
                    for (int i = 0; i < s.Length; i++)
                    {
                        char c = s[i];
                        hash = (hash ^ (byte)(c & 0xFF)) * FnvPrime;
                        hash = (hash ^ (byte)(c >> 8)) * FnvPrime;
                    }
                }
                return hash;
            }
        }

        /// <summary>Fold an int into the running FNV-1a hash (four bytes, little-endian order).</summary>
        public static uint Fold(uint hash, int value)
        {
            unchecked
            {
                uint u = (uint)value;
                hash = (hash ^ (u & 0xFF)) * FnvPrime;
                hash = (hash ^ ((u >> 8) & 0xFF)) * FnvPrime;
                hash = (hash ^ ((u >> 16) & 0xFF)) * FnvPrime;
                hash = (hash ^ ((u >> 24) & 0xFF)) * FnvPrime;
                return hash;
            }
        }

        /// <summary>The avalanche finalizer (same mix as the clam scatter) so close inputs diverge well.</summary>
        public static uint Finalize(uint hash)
        {
            unchecked
            {
                hash ^= hash >> 15;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                return hash;
            }
        }

        /// <summary>
        /// A boat's stable identity seed: <c>(worldSeed, fleetId, boatIndex)</c>. Deliberately does NOT
        /// fold the day — a boat's speed/phase/buoy colour are who she is, stable across days; fold
        /// <c>dayIndex</c> on top (via <see cref="Fold(uint,int)"/>) for things that shift daily (spots).
        /// </summary>
        public static uint BoatSeed(int worldSeed, string fleetId, int boatIndex)
        {
            uint h = FnvOffsetBasis;
            h = Fold(h, worldSeed);
            h = Fold(h, fleetId);
            h = Fold(h, boatIndex);
            return h;
        }

        /// <summary>Deterministic uniform value in [0, 1) from a seed and a stream/index pair —
        /// the planner's only "random" primitive. Same inputs ⇒ same value, forever.</summary>
        public static float Hash01(uint seed, int stream, int index)
        {
            uint h = Fold(seed, stream);
            h = Fold(h, index);
            h = Finalize(h);
            return (h & 0x00FFFFFF) / (float)0x01000000;   // 24 mantissa bits → [0, 1)
        }

        // ---- the depth gate ------------------------------------------------------------------

        /// <summary>
        /// Is <paramref name="worldPos"/> deep enough to plan through? True when the water over it at
        /// the tide's all-time floor (<paramref name="minWaterLevel"/>, spring low) keeps at least
        /// <paramref name="minDepthMeters"/>. Pure — the elevation comes through the sampler so tests
        /// can feed a synthetic seabed and the runtime feeds <c>ITidalTerrain.ElevationAt</c>.
        /// </summary>
        public static bool IsPlannable(Vector2 worldPos, Func<Vector2, float> elevationAt,
                                       float minWaterLevel, float minDepthMeters)
            => minWaterLevel - elevationAt(worldPos) >= minDepthMeters;

        /// <summary>
        /// Is the straight travel leg <paramref name="from"/> → <paramref name="to"/> plannable? Samples
        /// every <paramref name="stepMeters"/> along the segment (endpoints included) and requires every
        /// sample to pass <see cref="IsPlannable"/> — so a route never cuts across a bar that bares.
        /// </summary>
        public static bool IsLegClear(Vector2 from, Vector2 to, Func<Vector2, float> elevationAt,
                                      float minWaterLevel, float minDepthMeters, float stepMeters)
        {
            float dist = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / Mathf.Max(0.01f, stepMeters)));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
                if (!IsPlannable(p, elevationAt, minWaterLevel, minDepthMeters)) return false;
            }
            return true;
        }

        // ---- spot planning ---------------------------------------------------------------------

        /// <summary>
        /// Plan the whole fleet's buoy spots for one game day. Returns one array of spots per boat
        /// (some may be shorter than <paramref name="spotsPerBoat"/>, or empty, where the grounds can't
        /// yield valid water — the caller idles those boats). Deterministic: a pure function of the
        /// seeds and the authored seabed; boats are planned in index order so later boats deterministically
        /// avoid earlier boats' spots.
        ///
        /// <para>Acceptance rules per candidate: (1) inside <paramref name="grounds"/> by construction;
        /// (2) passes the spring-low depth gate; (3) at least <paramref name="spotSpacingMeters"/> from
        /// every spot already accepted fleet-wide; (4) the travel leg from the boat's previous spot is
        /// clear, and — for the boat's last spot — the closing leg back to her first spot is clear (the
        /// work cycle wraps). If <paramref name="maxTries"/> candidates can't satisfy the margin, the
        /// margin is halved (repeatedly, to a small floor) and the search rerun — a fisher still works
        /// the deepest gut on a hard shore — keeping the fallback deterministic too.</para>
        /// </summary>
        public static Vector2[][] PlanFleet(int worldSeed, string fleetId, int dayIndex,
                                            int boatCount, int spotsPerBoat, Rect grounds,
                                            Func<Vector2, float> elevationAt, float minWaterLevel,
                                            float minDepthMeters, float spotSpacingMeters,
                                            float legSampleStepMeters, int maxTries)
        {
            var fleet = new Vector2[Mathf.Max(0, boatCount)][];
            var accepted = new System.Collections.Generic.List<Vector2>(boatCount * spotsPerBoat);

            for (int b = 0; b < boatCount; b++)
            {
                uint daySeed = Fold(BoatSeed(worldSeed, fleetId, b), dayIndex);
                fleet[b] = PlanBoatSpots(daySeed, spotsPerBoat, grounds, elevationAt, minWaterLevel,
                                         minDepthMeters, spotSpacingMeters, legSampleStepMeters,
                                         maxTries, accepted);
                accepted.AddRange(fleet[b]);
            }
            return fleet;
        }

        /// <summary>One boat's spots for the day (see <see cref="PlanFleet"/> for the rules).</summary>
        private static Vector2[] PlanBoatSpots(uint daySeed, int spotsPerBoat, Rect grounds,
                                               Func<Vector2, float> elevationAt, float minWaterLevel,
                                               float minDepthMeters, float spotSpacingMeters,
                                               float legSampleStepMeters, int maxTries,
                                               System.Collections.Generic.List<Vector2> fleetAccepted)
        {
            float margin = Mathf.Max(MarginFloor, minDepthMeters);

            while (true)
            {
                var spots = TrySpots(daySeed, spotsPerBoat, grounds, elevationAt, minWaterLevel,
                                     margin, spotSpacingMeters, legSampleStepMeters, maxTries, fleetAccepted);
                if (spots.Length >= spotsPerBoat || margin <= MarginFloor) return spots;
                margin = Mathf.Max(MarginFloor, margin * 0.5f);   // relax deterministically and retry
            }
        }

        private static Vector2[] TrySpots(uint daySeed, int spotsPerBoat, Rect grounds,
                                          Func<Vector2, float> elevationAt, float minWaterLevel,
                                          float margin, float spotSpacingMeters, float legSampleStepMeters,
                                          int maxTries, System.Collections.Generic.List<Vector2> fleetAccepted)
        {
            var mine = new System.Collections.Generic.List<Vector2>(spotsPerBoat);
            float spacingSq = spotSpacingMeters * spotSpacingMeters;

            for (int m = 0; m < maxTries && mine.Count < spotsPerBoat; m++)
            {
                var p = new Vector2(
                    Mathf.Lerp(grounds.xMin, grounds.xMax, Hash01(daySeed, 10, m)),
                    Mathf.Lerp(grounds.yMin, grounds.yMax, Hash01(daySeed, 11, m)));

                if (!IsPlannable(p, elevationAt, minWaterLevel, margin)) continue;
                if (TooClose(p, fleetAccepted, spacingSq) || TooClose(p, mine, spacingSq)) continue;
                if (mine.Count > 0 &&
                    !IsLegClear(mine[mine.Count - 1], p, elevationAt, minWaterLevel, margin, legSampleStepMeters))
                    continue;
                // The last spot must also close the cycle back to the first.
                if (mine.Count == spotsPerBoat - 1 && spotsPerBoat > 1 &&
                    !IsLegClear(p, mine[0], elevationAt, minWaterLevel, margin, legSampleStepMeters))
                    continue;

                mine.Add(p);
            }
            return mine.ToArray();
        }

        private static bool TooClose(Vector2 p, System.Collections.Generic.List<Vector2> others, float spacingSq)
        {
            for (int i = 0; i < others.Count; i++)
                if ((others[i] - p).sqrMagnitude < spacingSq) return true;
            return false;
        }

        // ---- spot planning on the accessible water (#886) ------------------------------------------

        // A spot's all-round probe: this many bearings at the live probe's look-ahead. A shoal edge running
        // straight across the ring cannot slip between two bearings nearer the spot than cos(180° / 16), 98 %
        // of the reach, so a boat lying-to at her spot never probes shoal water, whatever her heading.
        private const int RingBearings = 16;
        private static readonly Vector2[] RingDirections = BuildRing();

        private static Vector2[] BuildRing()
        {
            var ring = new Vector2[RingBearings];
            for (int k = 0; k < RingBearings; k++)
            {
                float a = 2f * Mathf.PI * k / RingBearings;
                ring[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            return ring;
        }

        /// <summary>
        /// One day's plan for <paramref name="def"/>'s fleet on its accessible water, every rule read off the
        /// Def. The presenter and <c>AmbientFleetGroundsTests</c> both make this call, so the guard measures
        /// the wiring that ships.
        /// </summary>
        public static Vector2[][] PlanFleetDay(AmbientFleetDef def, AmbientFleetWater water, int worldSeed,
                                               int dayIndex, int boatCount, float secondsPerDay,
                                               float[] marginPerBoat = null)
            => PlanFleet(worldSeed, def.Id, dayIndex, boatCount, Mathf.Max(1, def.SpotsPerBoat), water,
                         def.MinDepthMeters, def.SpotSpacingMeters, def.LegSampleStepMeters,
                         def.MaxCandidateTries, MaxLegMeters(def, secondsPerDay), def.DepthLookAheadMeters,
                         def.DepthProbeSideDegrees, marginPerBoat);

        /// <summary>
        /// The longest leg <paramref name="def"/>'s fleet plans: the slowest boat's cruise speed times
        /// <see cref="AmbientFleetSchedule.TransitSeconds"/>, so even she is on her next mark before its work
        /// window opens.
        /// </summary>
        public static float MaxLegMeters(AmbientFleetDef def, float secondsPerDay)
        {
            // The presenter's own floor under the work window's end: a window always has some length.
            float workEnd = Mathf.Max(def.WorkWindowEndFraction, def.WorkWindowStartFraction + 0.01f);
            return def.MinSpeedMetersPerSecond *
                   AmbientFleetSchedule.TransitSeconds(secondsPerDay, def.SlotsPerDay,
                                                       def.WorkWindowStartFraction, workEnd);
        }

        /// <summary>
        /// Plan the whole fleet's buoy spots for one game day on the ACCESSIBLE water (#886). Returns one
        /// array of spots per boat. Deterministic from <c>(worldSeed, fleetId, dayIndex)</c> and the measured
        /// water; boats are planned in index order so later boats avoid earlier boats' spots.
        ///
        /// <para>A spot is accepted when (1) its cell of <paramref name="water"/> joins the water's seed at
        /// the margin; (2) it stands the water's clearance inside its bounds and off every keep-clear area,
        /// checked exactly; (3) it keeps the margin at spring low, there and all round it at
        /// <paramref name="probeReachMeters"/>, the live probe's look-ahead; and (4) it stands
        /// <paramref name="spotSpacingMeters"/> from every spot already accepted fleet-wide. A leg is
        /// accepted when it is at most <paramref name="maxLegMeters"/> long, keeps the clearance off every
        /// area and inside the bounds, and keeps the margin across the live probe's width
        /// (<paramref name="probeReachMeters"/> × sin <paramref name="probeSideDegrees"/> each side), sampled
        /// every <paramref name="legSampleStepMeters"/>. The closing leg back to the first spot is a leg like
        /// any other.</para>
        ///
        /// <para>The search: a boat's first spot is a hashed candidate cell, at a hashed offset inside it; each
        /// next spot is a hashed point within one leg of the last. When <paramref name="maxTries"/> draws cannot
        /// close her loop, that first spot is dropped and the next one drawn, up to <paramref name="maxTries"/>
        /// first spots. Only when none closes does the margin halve, as on the rectangle, with reach measured
        /// again at the relaxed margin: a hard shore still gets a fleet.</para>
        ///
        /// <para><paramref name="marginPerBoat"/>, when given, receives the margin each boat was planned at:
        /// <paramref name="minDepthMeters"/> unless her water forced a relaxation.</para>
        /// </summary>
        public static Vector2[][] PlanFleet(int worldSeed, string fleetId, int dayIndex,
                                            int boatCount, int spotsPerBoat, AmbientFleetWater water,
                                            float minDepthMeters, float spotSpacingMeters,
                                            float legSampleStepMeters, int maxTries, float maxLegMeters,
                                            float probeReachMeters, float probeSideDegrees,
                                            float[] marginPerBoat = null)
        {
            var fleet = new Vector2[Mathf.Max(0, boatCount)][];
            var accepted = new System.Collections.Generic.List<Vector2>(fleet.Length * Mathf.Max(1, spotsPerBoat));
            var rules = new WaterRules(water, spotSpacingMeters, legSampleStepMeters, maxTries, maxLegMeters,
                                       probeReachMeters, probeSideDegrees);

            for (int b = 0; b < fleet.Length; b++)
            {
                float used = Mathf.Max(MarginFloor, minDepthMeters);
                if (water == null || spotsPerBoat <= 0)
                {
                    fleet[b] = Array.Empty<Vector2>();
                }
                else
                {
                    uint daySeed = Fold(BoatSeed(worldSeed, fleetId, b), dayIndex);
                    fleet[b] = PlanBoatOnWater(daySeed, spotsPerBoat, minDepthMeters, rules, accepted, out used);
                }
                if (marginPerBoat != null && b < marginPerBoat.Length) marginPerBoat[b] = used;
                accepted.AddRange(fleet[b]);
            }
            return fleet;
        }

        /// <summary>
        /// Is the leg plannable across a width? As <see cref="IsLegClear"/>, and along the two lines
        /// <paramref name="halfWidthMeters"/> either side too. A shoal edge running straight across the leg
        /// cannot reach its middle line between two samples without covering one of the side samples, so the
        /// line she steers holds between the samples, not only at them.
        /// </summary>
        public static bool IsCorridorClear(Vector2 from, Vector2 to, Func<Vector2, float> elevationAt,
                                           float minWaterLevel, float minDepthMeters, float stepMeters,
                                           float halfWidthMeters)
        {
            Vector2 along = to - from;
            float dist = along.magnitude;
            Vector2 side = dist > 0f ? new Vector2(-along.y, along.x) * (halfWidthMeters / dist) : Vector2.zero;
            bool wide = halfWidthMeters > 0f && dist > 0f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / Mathf.Max(0.01f, stepMeters)));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
                if (!IsPlannable(p, elevationAt, minWaterLevel, minDepthMeters)) return false;
                if (wide && (!IsPlannable(p + side, elevationAt, minWaterLevel, minDepthMeters) ||
                             !IsPlannable(p - side, elevationAt, minWaterLevel, minDepthMeters))) return false;
            }
            return true;
        }

        private readonly struct WaterRules
        {
            public readonly AmbientFleetWater Water;
            public readonly float SpacingSq;
            public readonly float LegStep;
            public readonly int MaxTries;
            public readonly float MaxLeg;
            public readonly float ProbeReach;
            public readonly float CorridorHalfWidth;

            public WaterRules(AmbientFleetWater water, float spacingMeters, float legStepMeters, int maxTries,
                              float maxLegMeters, float probeReachMeters, float probeSideDegrees)
            {
                Water = water;
                SpacingSq = spacingMeters * spacingMeters;
                LegStep = legStepMeters;
                MaxTries = Mathf.Max(1, maxTries);
                MaxLeg = Mathf.Max(0f, maxLegMeters);
                ProbeReach = Mathf.Max(0f, probeReachMeters);
                CorridorHalfWidth = ProbeReach * Mathf.Sin(Mathf.Clamp(probeSideDegrees, 0f, 90f) * Mathf.Deg2Rad);
            }
        }

        private static Vector2[] PlanBoatOnWater(uint daySeed, int spotsPerBoat, float minDepthMeters,
                                                 in WaterRules rules,
                                                 System.Collections.Generic.List<Vector2> fleetAccepted,
                                                 out float marginUsed)
        {
            float margin = Mathf.Max(MarginFloor, minDepthMeters);
            while (true)
            {
                var spots = TryLoopOnWater(daySeed, spotsPerBoat, margin, rules, fleetAccepted);
                if (spots.Length >= spotsPerBoat || margin <= MarginFloor)
                {
                    marginUsed = margin;
                    return spots;
                }
                margin = Mathf.Max(MarginFloor, margin * 0.5f);   // relax deterministically and retry
            }
        }

        private static Vector2[] TryLoopOnWater(uint daySeed, int spotsPerBoat, float margin, in WaterRules rules,
                                                System.Collections.Generic.List<Vector2> fleetAccepted)
        {
            AmbientFleetWater water = rules.Water;
            int[] cells = water.EligibleCells(margin);
            if (cells.Length == 0) return Array.Empty<Vector2>();

            var mine = new System.Collections.Generic.List<Vector2>(spotsPerBoat);
            Vector2[] best = Array.Empty<Vector2>();

            for (int m0 = 0; m0 < rules.MaxTries; m0++)
            {
                int k = Mathf.Min(cells.Length - 1, (int)(Hash01(daySeed, 12, m0) * cells.Length));
                Vector2 first = water.CellCentre(cells[k]) +
                                new Vector2(Hash01(daySeed, 13, m0) - 0.5f, Hash01(daySeed, 14, m0) - 0.5f) *
                                water.CellMeters;
                mine.Clear();
                if (!water.IsEligible(first, margin) || !SpotHolds(first, margin, rules, fleetAccepted, mine)) continue;
                mine.Add(first);

                uint loopSeed = Fold(daySeed, m0);
                for (int m = 0; m < rules.MaxTries && mine.Count < spotsPerBoat; m++)
                {
                    Vector2 prev = mine[mine.Count - 1];
                    float r = rules.MaxLeg * Mathf.Sqrt(Hash01(loopSeed, 15, m));   // uniform over the disc
                    float a = 2f * Mathf.PI * Hash01(loopSeed, 16, m);
                    Vector2 p = prev + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;

                    if (!water.IsEligible(p, margin) || !SpotHolds(p, margin, rules, fleetAccepted, mine)) continue;
                    if (!LegHolds(prev, p, margin, rules)) continue;
                    // The last spot must close the loop back to the first. With two spots that is the leg
                    // just checked, sailed the other way.
                    if (mine.Count == spotsPerBoat - 1 && spotsPerBoat > 2 && !LegHolds(p, mine[0], margin, rules))
                        continue;
                    mine.Add(p);
                }

                if (mine.Count >= spotsPerBoat) return mine.ToArray();
                if (mine.Count > best.Length)
                {
                    Vector2[] closed = ClosedPrefix(mine, margin, rules);
                    if (closed.Length > best.Length) best = closed;
                }
            }
            return best;
        }

        private static bool SpotHolds(Vector2 p, float margin, in WaterRules rules,
                                      System.Collections.Generic.List<Vector2> fleetAccepted,
                                      System.Collections.Generic.List<Vector2> mine)
        {
            if (TooClose(p, fleetAccepted, rules.SpacingSq) || TooClose(p, mine, rules.SpacingSq)) return false;
            AmbientFleetWater water = rules.Water;
            if (!water.IsClear(p)) return false;
            Func<Vector2, float> elevationAt = water.ElevationAt;
            if (!IsPlannable(p, elevationAt, water.MinWaterLevel, margin)) return false;
            for (int k = 0; k < RingDirections.Length; k++)
                if (!IsPlannable(p + RingDirections[k] * rules.ProbeReach, elevationAt, water.MinWaterLevel, margin))
                    return false;
            return true;
        }

        private static bool LegHolds(Vector2 from, Vector2 to, float margin, in WaterRules rules)
        {
            if ((to - from).sqrMagnitude > rules.MaxLeg * rules.MaxLeg) return false;
            AmbientFleetWater water = rules.Water;
            if (!water.IsLegKeptClear(from, to)) return false;
            return IsCorridorClear(from, to, water.ElevationAt, water.MinWaterLevel, margin, rules.LegStep,
                                   rules.CorridorHalfWidth);
        }

        /// <summary>The longest start of <paramref name="spots"/> whose closing leg holds (one spot always
        /// does): a short loop a relaxed search may fall back on, never one with no way back to its start.</summary>
        private static Vector2[] ClosedPrefix(System.Collections.Generic.List<Vector2> spots, float margin,
                                              in WaterRules rules)
        {
            int n = spots.Count;
            while (n > 2 && !LegHolds(spots[n - 1], spots[0], margin, rules)) n--;
            var prefix = new Vector2[n];
            for (int i = 0; i < n; i++) prefix[i] = spots[i];
            return prefix;
        }
    }
}
