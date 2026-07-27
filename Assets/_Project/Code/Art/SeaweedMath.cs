using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The PURE maths behind the drifting seaweed (owner ask 2026-07-08: "seaweed clumps that can get
    /// stuck on things and group together from the waves" — P1 the sea moves things, P3 a working coast
    /// has weed on the buoy lines). Like <see cref="AmbientParticleMath"/> every feel-decision lives
    /// here as <b>pure, side-effect-free, EditMode-testable</b> statics so the drift/merge/snag/strand
    /// behaviour is verified headless and the <see cref="SeaweedPresenter"/> shell stays thin — the
    /// <c>AmbientFleetSteering</c> precedent.
    ///
    /// <para><b>Determinism honesty (rule 5).</b> The weed is presentation-only decor: it drives no
    /// sim, saves nothing, and is recreated per session. Placement variety comes from the stable
    /// <see cref="AmbientParticleMath.Hash01(int,int)"/> — never <see cref="System.Random"/> — and the
    /// drift reads only the deterministic shared signals (the sim current, the shared
    /// <c>_WindWorld</c>, and the ONE shared wave field's slope), so identical inputs reproduce
    /// identical motion. Nothing here feeds anything deterministic-consumed.</para>
    /// </summary>
    public static class SeaweedMath
    {
        // ---- piece states (byte-packed; the presenter's parallel arrays + the tests share these) ----

        /// <summary>Riding free on the water — drifting with current + wind + wave convergence.</summary>
        public const byte StateDrifting = 0;
        /// <summary>Fouled on a player trap buoy — anchored at the snag point, wobbling with the wave.</summary>
        public const byte StateSnagged = 1;
        /// <summary>Beached on ground the tide has left too shallow — stranded until the tide refloats it.</summary>
        public const byte StateStranded = 2;
        /// <summary>Absorbed into a bigger clump (or recycled) — hidden, waiting to respawn.</summary>
        public const byte StateDormant = 3;

        // ==== seeded placement ============================================================================

        /// <summary>
        /// A stable per-bed seed folding the world seed with the bed's string id (FNV-1a over the
        /// chars), so two beds in one world scatter differently and the same world re-seeds the same.
        /// Deterministic, allocation-free.
        /// </summary>
        public static uint BedSeed(int worldSeed, string bedId)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (bedId != null)
                    for (int i = 0; i < bedId.Length; i++)
                        h = (h ^ bedId[i]) * 16777619u;
                return h ^ (uint)worldSeed;
            }
        }

        /// <summary>
        /// The seeded candidate spawn point for a piece: a deterministic scatter across the bed rect,
        /// keyed by (bedSeed, pieceIndex, attempt) so a rejected candidate (too shallow, on a buoy)
        /// hashes to a fresh spot on the next attempt. Pure; the presenter applies the depth gate.
        /// </summary>
        public static Vector2 SpawnPoint(uint bedSeed, int pieceIndex, int attempt, Rect bed)
        {
            unchecked
            {
                int key = (int)bedSeed + pieceIndex * 8191 + attempt * 131071;
                float hx = AmbientParticleMath.Hash01(key, 19);
                float hy = AmbientParticleMath.Hash01(key, 43);
                return new Vector2(bed.xMin + hx * bed.width, bed.yMin + hy * bed.height);
            }
        }

        // ==== drift (current + wind + wave convergence — never a private random walk) =====================

        /// <summary>
        /// The weed's drift velocity (m/s): the tidal <paramref name="flow"/> set (the sim
        /// <c>CurrentVector</c>, m/s) scaled by <paramref name="flowResponse"/>, plus the shared scene
        /// wind (the 0..1 <c>_WindWorld</c> global the grass/mist read) scaled by
        /// <paramref name="windResponse"/> (m/s per unit), plus the wave-convergence term: weed slides
        /// DOWN the local surface slope (<c>-slope · troughSeek</c>) so pieces gather in the troughs —
        /// the cheap honest read of "the waves grouped them". The sum is clamped to
        /// <paramref name="maxSpeed"/> so a freak gale can't fling the wrack across the harbour. Pure.
        /// </summary>
        public static Vector2 DriftVelocity(Vector2 flow, float flowResponse,
                                            Vector2 wind, float windResponse,
                                            Vector2 waveSlope, float troughSeek, float maxSpeed)
        {
            Vector2 v = flow * flowResponse + wind * windResponse - waveSlope * troughSeek;
            float max = Mathf.Max(0f, maxSpeed);
            float sq = v.sqrMagnitude;
            if (sq > max * max && sq > 1e-12f) v *= max / Mathf.Sqrt(sq);
            return v;
        }

        // ==== stranding (beach at a falling tide, refloat on the flood — with hysteresis) =================

        /// <summary>
        /// The strand/refloat transition, with hysteresis so a piece never flickers on the waterline:
        /// a FLOATING piece strands when the water under it thins to <paramref name="strandDepth"/> or
        /// less; a STRANDED piece refloats only when the tide has risen to at least
        /// <paramref name="refloatDepth"/> (keep it above the strand depth — the gap is the
        /// hysteresis). Depth is <c>waterLevel − elevation</c>, the one number the whole tidal seam
        /// compares (ITidalTerrain). Pure — the state transition the tests pin.
        /// </summary>
        public static bool NextStranded(bool stranded, float depth, float strandDepth, float refloatDepth)
            => stranded ? depth < refloatDepth : depth <= strandDepth;

        // ==== snagging on the player's gear ===============================================================

        /// <summary>
        /// Index of the nearest point within <paramref name="radius"/> of <paramref name="pos"/>, or −1
        /// when nothing is in reach. Only the first <paramref name="count"/> entries are live (the
        /// presenter's packed buoy buffer — the AmbientFleetPresenter read). Pure, allocation-free.
        /// </summary>
        public static int NearestWithin(Vector2 pos, Vector2[] points, int count, float radius)
        {
            int best = -1;
            float bestSq = radius * radius;
            for (int i = 0; i < count; i++)
            {
                float d = (points[i] - pos).sqrMagnitude;
                if (d <= bestSq) { bestSq = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Where a snagged piece comes to rest against a buoy: on the rim of
        /// <paramref name="restRadius"/> around <paramref name="buoyPos"/>, along the direction it
        /// drifted in from — it reads as wrack wrapped against the buoy line, not weed ON the float.
        /// Degenerate (piece exactly on the buoy) rests due south of it so the sprite never lands at
        /// NaN. Pure.
        /// </summary>
        public static Vector2 SnagAnchor(Vector2 piecePos, Vector2 buoyPos, float restRadius)
        {
            Vector2 dir = piecePos - buoyPos;
            float mag = dir.magnitude;
            if (mag < 1e-5f) dir = Vector2.down; else dir /= mag;
            return buoyPos + dir * Mathf.Max(0f, restRadius);
        }

        // ==== bounds recycling ============================================================================

        /// <summary>True when <paramref name="pos"/> has drifted beyond the bed rect grown by
        /// <paramref name="padding"/> on every side — the presenter recycles such a piece so the bed
        /// never bleeds its whole stock out of the region. Pure.</summary>
        public static bool OutsideBounds(Vector2 pos, Rect bed, float padding)
        {
            float p = Mathf.Max(0f, padding);
            return pos.x < bed.xMin - p || pos.x > bed.xMax + p ||
                   pos.y < bed.yMin - p || pos.y > bed.yMax + p;
        }

        // ==== the wave-borne look (bob + wobble) ==========================================================

        /// <summary>Screen-vertical lift (world units) as the crest passes under the weed — the
        /// <c>BuoyWaveMath.BobOffset</c> idea, gentler (weed lies IN the surface): linear in the wave
        /// height, hard-capped at ±<paramref name="maxBob"/>. Pure.</summary>
        public static float BobOffset(float waveHeight, float bobPerMeter, float maxBob)
            => Mathf.Clamp(waveHeight * bobPerMeter, -Mathf.Abs(maxBob), Mathf.Abs(maxBob));

        /// <summary>
        /// The clump's rocking (degrees about z) as the swell works it: proportional to the local wave
        /// height normalised by the field's <paramref name="totalAmplitude"/> envelope, capped at
        /// ±<paramref name="maxDegrees"/>. Exactly 0 on dead glass (zero envelope) — a becalmed harbour
        /// shows still wrack. Pure.
        /// </summary>
        public static float Wobble(float waveHeight, float totalAmplitude, float maxDegrees)
        {
            if (totalAmplitude <= 1e-5f) return 0f;
            return Mathf.Clamp(waveHeight / totalAmplitude, -1f, 1f) * maxDegrees;
        }

        // ==== clumping (the neighbour merge — swap N small for 1 big; split not required) =================

        /// <summary>
        /// One slow-tick merge pass over the bed: any two live pieces within
        /// <paramref name="mergeRadius"/> merge — the absorbed piece goes <see cref="StateDormant"/>
        /// (the presenter respawns it later; pool-friendly, no allocation) and the absorber GROWS one
        /// size tier (capped at <paramref name="maxTier"/>), so converging weed visibly becomes a
        /// bigger clump. Who absorbs whom:
        /// <list type="bullet">
        /// <item>an ANCHORED piece (snagged/stranded) absorbs a drifting one — the wrack collects on
        /// the buoy line / the beach, and the anchor point never moves;</item>
        /// <item>two anchored pieces never merge (each is stuck to its own thing);</item>
        /// <item>two drifting pieces: the bigger tier absorbs; on a tie the lower index does.</item>
        /// </list>
        /// Mutates <paramref name="state"/>/<paramref name="tier"/> in place and records each absorbed
        /// piece's absorber in <paramref name="absorbedBy"/> (−1 = untouched) so the presenter can
        /// start respawn timers. Only the first <paramref name="count"/> entries are live. Returns the
        /// number of merges. Deterministic, allocation-free, O(n²) over a Def-bounded pool.
        /// </summary>
        public static int MergePass(Vector2[] pos, byte[] state, int[] tier, int count,
                                    float mergeRadius, int maxTier, int[] absorbedBy)
        {
            for (int i = 0; i < count; i++) absorbedBy[i] = -1;

            float radiusSq = mergeRadius * mergeRadius;
            int merges = 0;

            for (int i = 0; i < count; i++)
            {
                if (state[i] == StateDormant || absorbedBy[i] >= 0) continue;
                for (int j = i + 1; j < count; j++)
                {
                    if (state[j] == StateDormant || absorbedBy[j] >= 0) continue;

                    bool iDrifts = state[i] == StateDrifting;
                    bool jDrifts = state[j] == StateDrifting;
                    if (!iDrifts && !jDrifts) continue;                       // both stuck to their own thing
                    if ((pos[j] - pos[i]).sqrMagnitude > radiusSq) continue;

                    int absorber, absorbed;
                    if (!iDrifts) { absorber = i; absorbed = j; }             // the anchor collects the drifter
                    else if (!jDrifts) { absorber = j; absorbed = i; }
                    else if (tier[j] > tier[i]) { absorber = j; absorbed = i; }
                    else { absorber = i; absorbed = j; }

                    tier[absorber] = Mathf.Min(maxTier, Mathf.Max(tier[i], tier[j]) + 1);
                    state[absorbed] = StateDormant;
                    absorbedBy[absorbed] = absorber;
                    merges++;

                    if (absorbed == i) break;                                 // i is gone — stop pairing it
                }
            }
            return merges;
        }

        // ==== painted-weed art selection ==================================================================
        //
        // Which clump of the drift-weed kit a piece wears. Kept here, pure and over plain arrays, for the
        // same reason the drift maths is: it is a feel decision, and it must be verifiable headless
        // without importing a single sprite.

        /// <summary>
        /// Two clumps whose drawn sizes are within this (metres) count as the same size for selection,
        /// so the nearest-size rule still leaves room for variety. ~2 px at PPU 32 — below the point
        /// where an eye reads two clumps as different sizes at all.
        /// </summary>
        public const float ArtSizeTieMeters = 0.06f;

        /// <summary>
        /// Picks a ramp row (0 living, 1 golden, 2 bleached) by relative weight, seeded off the piece's
        /// spawn key so a clump keeps its colour for life — the owner's 2026-07-23 ruling is that all
        /// four species ship their golden rows and the runtime may weight them.
        ///
        /// <para>Weights shorter than <paramref name="rampCount"/> leave the missing rows at zero,
        /// which is the predictable reading: <c>{1}</c> means "living only". All-zero or null weights
        /// fall back to row 0 rather than drawing nothing.</para>
        /// </summary>
        public static int PickRamp(float[] weights, int rampCount, int key)
        {
            if (rampCount <= 0) return -1;

            float total = 0f;
            for (int r = 0; r < rampCount; r++) total += WeightAt(weights, r);
            if (total <= 0f) return 0;

            float pick = AmbientParticleMath.Hash01(key, 23) * total;
            float acc = 0f;
            for (int r = 0; r < rampCount; r++)
            {
                acc += WeightAt(weights, r);
                if (pick < acc) return r;
            }
            return rampCount - 1;      // only reachable on float slop at the very top of the range
        }

        private static float WeightAt(float[] w, int i) =>
            w != null && i >= 0 && i < w.Length ? Mathf.Max(0f, w[i]) : 0f;

        /// <summary>
        /// Picks which painted clump a piece should wear: the one drawn NEAREST the tier footprint the
        /// bed asked for, chosen among equals by the piece's seeded key. Returns an index into the
        /// kit's flat arrays, or −1 when there is no art at all.
        ///
        /// <para><b>Size wins over species continuity, deliberately.</b> A clump that merges climbs a
        /// tier and may therefore change species — Eelgrass tops out at 0.9 m and cannot serve a
        /// 1.15 m tier, so holding species fixed would force a rescale and break the pixel grid. A
        /// clump that grew by absorbing a neighbour reading as a different mix of weed is honest;
        /// resampled pixel art is not.</para>
        ///
        /// <para>Falls back in order: the chosen ramp within the allowed species → any ramp within the
        /// allowed species (a species need not ship every row) → the whole kit. The last step means a
        /// bed whose filter and ramp weights between them exclude everything still draws weed rather
        /// than vanishing.</para>
        /// </summary>
        /// <param name="targetSizeMeters">The tier footprint the bed wants.</param>
        /// <param name="speciesAllowed">Per-species allow mask, or null for "all".</param>
        /// <param name="ramp">Preferred ramp row, or −1 for "any".</param>
        public static int PickWeedArt(float targetSizeMeters,
                                      float[] sizes, int[] speciesIndex, int[] rampIndex, int count,
                                      bool[] speciesAllowed, int ramp, int key)
        {
            if (sizes == null || count <= 0) return -1;
            count = Mathf.Min(count, sizes.Length);

            int hit = NearestArt(targetSizeMeters, sizes, speciesIndex, rampIndex, count,
                                 speciesAllowed, ramp, key);
            if (hit >= 0) return hit;

            if (ramp >= 0)
            {
                hit = NearestArt(targetSizeMeters, sizes, speciesIndex, rampIndex, count,
                                 speciesAllowed, -1, key);
                if (hit >= 0) return hit;
            }

            return speciesAllowed == null
                ? -1
                : NearestArt(targetSizeMeters, sizes, speciesIndex, rampIndex, count, null, -1, key);
        }

        private static int NearestArt(float target,
                                      float[] sizes, int[] speciesIndex, int[] rampIndex, int count,
                                      bool[] speciesAllowed, int ramp, int key)
        {
            float bestDelta = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!ArtEligible(i, speciesIndex, rampIndex, speciesAllowed, ramp)) continue;
                float d = Mathf.Abs(sizes[i] - target);
                if (d < bestDelta) bestDelta = d;
            }
            if (bestDelta == float.MaxValue) return -1;      // nothing eligible

            float cutoff = bestDelta + ArtSizeTieMeters;
            int ties = 0;
            for (int i = 0; i < count; i++)
                if (ArtEligible(i, speciesIndex, rampIndex, speciesAllowed, ramp) &&
                    Mathf.Abs(sizes[i] - target) <= cutoff) ties++;

            // Hash01 can return exactly 1f, so fold the product back into range.
            int wanted = ties > 1 ? (int)(AmbientParticleMath.Hash01(key, 29) * ties) % ties : 0;
            int seen = 0;
            for (int i = 0; i < count; i++)
            {
                if (!ArtEligible(i, speciesIndex, rampIndex, speciesAllowed, ramp)) continue;
                if (Mathf.Abs(sizes[i] - target) > cutoff) continue;
                if (seen == wanted) return i;
                seen++;
            }
            return -1;
        }

        private static bool ArtEligible(int i, int[] speciesIndex, int[] rampIndex,
                                        bool[] speciesAllowed, int ramp)
        {
            if (ramp >= 0)
            {
                if (rampIndex == null || i >= rampIndex.Length || rampIndex[i] != ramp) return false;
            }
            if (speciesAllowed != null)
            {
                int s = speciesIndex != null && i < speciesIndex.Length ? speciesIndex[i] : -1;
                if (s < 0 || s >= speciesAllowed.Length || !speciesAllowed[s]) return false;
            }
            return true;
        }
    }
}
