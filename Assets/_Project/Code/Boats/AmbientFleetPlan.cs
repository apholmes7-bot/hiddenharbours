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
    /// </summary>
    public static class AmbientFleetPlan
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

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
            const float MarginFloor = 0.05f;   // never relax below "there is water at all"
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
    }
}
