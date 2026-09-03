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
        /// A piece's base colour, before the shared day/night tint and the fade alpha.
        ///
        /// <para>⚠️ <b>The palette is FOR THE GREYBOX BLOB.</b> That blob is generated white-with-alpha
        /// precisely so this colour multiplies through it and gives a bed of code-built shapes some
        /// tonal variety. The painted drift-weed clumps already carry the art director's own banded
        /// ramps — living, golden and bleached, each with its wet-surface glint — so multiplying a dark
        /// olive over them would mud every clump in the bed and throw away the work. Painted art
        /// therefore rides <b>white</b>.</para>
        ///
        /// <para>This is a two-line decision that is invisible when wrong (the weed simply looks
        /// murky), which is exactly why it lives here as a tested static rather than inline in the
        /// spawn path.</para>
        /// </summary>
        public static Color PieceTint(bool paintedArt, Color[] palette, int key)
        {
            if (paintedArt || palette == null || palette.Length == 0) return Color.white;
            return palette[(int)(AmbientParticleMath.Hash01(key, 7) * palette.Length) % palette.Length];
        }

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

        // ==== the art's own anchors (round 2 — the frond hooks the line, the tail trails the sea) ======
        //
        // Every drift-weed clump publishes, in its own sprite frame (metres from the pivot, scale 1,
        // +y up), 2–3 SNAG tips (the outer frond ends that catch on a line) and ONE DRAG TAIL (the end
        // that trails when it drifts). These rules turn those anchors into a pose. All pure, all
        // allocation-free, all knob-0-identical to the round-1 hashed-rotation / radius-rest behaviour.

        /// <summary>
        /// Below this transport speed (m/s) the sea has no direction worth aligning to, so the drag
        /// alignment holds its current rotation rather than chasing float noise. A numerical guard, not
        /// a feel knob: at 1 cm/s a clump takes a minute and a half to cross a metre.
        /// </summary>
        public const float TransportDeadBandMetersPerSecond = 0.01f;

        /// <summary>Rotate a sprite-local offset by <paramref name="degrees"/> about z — the ONE
        /// rotation every anchor rule below applies, so an anchor's world position and the sprite's
        /// drawn rotation can never disagree about handedness.</summary>
        public static Vector2 Rotate(Vector2 local, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(local.x * c - local.y * s, local.x * s + local.y * c);
        }

        /// <summary>Direction of a vector in degrees about z (0 = +x, 90 = +y) — the frame
        /// <c>Quaternion.Euler(0,0,deg)</c> rotates in.</summary>
        public static float DirectionDegrees(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        /// <summary>
        /// The drag alignment: ease a DRIFTING clump's rotation toward the one at which its
        /// <paramref name="tailLocal"/> trails — points AGAINST the <paramref name="transport"/> that is
        /// carrying it, the way anything towed streams out behind its motion (the rig's own words:
        /// "the end that trails when drifting"). Eases at <paramref name="degreesPerSecond"/> along the
        /// shorter arc; a rate of 0, a zero dt, a tail at the pivot or a transport under the dead band
        /// all return <paramref name="currentDegrees"/> unchanged — so a becalmed sea, or a knob at 0,
        /// keeps round 1's hashed rotation exactly. Pure, deterministic.
        /// </summary>
        public static float TailAlignedRotation(Vector2 transport, float currentDegrees, Vector2 tailLocal,
                                                float degreesPerSecond, float dt)
        {
            if (degreesPerSecond <= 0f || dt <= 0f) return currentDegrees;
            if (transport.sqrMagnitude < TransportDeadBandMetersPerSecond * TransportDeadBandMetersPerSecond) return currentDegrees;
            if (tailLocal.sqrMagnitude < 1e-10f) return currentDegrees;

            float target = DirectionDegrees(-transport) - DirectionDegrees(tailLocal);
            return Mathf.MoveTowardsAngle(currentDegrees, target, degreesPerSecond * dt);
        }

        /// <summary>
        /// The hung clump's counterpart of <see cref="TailAlignedRotation"/>: ease a HOOKED clump's hang
        /// rotation toward <see cref="HangRotation"/>(<paramref name="anchorLocal"/>, transport) at
        /// <paramref name="degreesPerSecond"/>, so when the set changes — the tide turns — the body
        /// swings round to lie down-transport of the tip that holds it. The same guards: rate 0, zero
        /// dt, a transport under the dead band or an anchor at the pivot hold the current rotation.
        /// Pure, deterministic.
        /// </summary>
        public static float HangAlignedRotation(Vector2 transport, float currentDegrees, Vector2 anchorLocal,
                                                float degreesPerSecond, float dt)
        {
            if (degreesPerSecond <= 0f || dt <= 0f) return currentDegrees;
            if (transport.sqrMagnitude < TransportDeadBandMetersPerSecond * TransportDeadBandMetersPerSecond) return currentDegrees;
            if (anchorLocal.sqrMagnitude < 1e-10f) return currentDegrees;

            return Mathf.MoveTowardsAngle(currentDegrees, HangRotation(anchorLocal, transport), degreesPerSecond * dt);
        }

        /// <summary>
        /// Which frond tip hooks the line: of the clump's <paramref name="snagsLocal"/> (as drawn, at
        /// its current <paramref name="rotationDegrees"/>), the one reaching furthest along the
        /// <paramref name="approach"/> direction — the tip that meets the line first as the sea carries
        /// the clump onto it. Ties (within 1e-5 m) go to the lower index so the pick is deterministic
        /// under float slop; −1 for a clump with no anchors (the greybox blob, a legacy sprite).
        /// Only the first <paramref name="count"/> entries are live. Pure, allocation-free.
        /// </summary>
        public static int PickSnagAnchor(Vector2[] snagsLocal, int count, float rotationDegrees, Vector2 approach)
        {
            if (snagsLocal == null || count <= 0) return -1;
            count = Mathf.Min(count, snagsLocal.Length);
            float mag = approach.magnitude;
            Vector2 dir = mag > 1e-6f ? approach / mag : Vector2.down;

            int best = -1;
            float bestReach = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                float reach = Vector2.Dot(Rotate(snagsLocal[i], rotationDegrees), dir);
                if (reach > bestReach + 1e-5f) { bestReach = reach; best = i; }
            }
            return best;
        }

        /// <summary>
        /// The rotation at which a clump hooked by <paramref name="anchorLocal"/> hangs off the line
        /// with its body streaming <paramref name="downTransport"/> — the sea pushes the clump past the
        /// line and it swings to lie downstream of the tip that caught. Geometrically: the pivot (the
        /// clump's buoyancy centre) sits at <c>−anchorLocal</c> from the tip, so that vector is turned
        /// to point down-transport. A degenerate direction hangs the clump due south (screen-down) of
        /// the line, never at NaN; an anchor AT the pivot has no lever arm and returns 0. Pure.
        /// </summary>
        public static float HangRotation(Vector2 anchorLocal, Vector2 downTransport)
        {
            if (anchorLocal.sqrMagnitude < 1e-10f) return 0f;
            Vector2 d = downTransport.sqrMagnitude > 1e-12f ? downTransport : Vector2.down;
            return DirectionDegrees(d) - DirectionDegrees(-anchorLocal);
        }

        /// <summary>
        /// Where the sprite's pivot goes so that <paramref name="anchorLocal"/>, drawn at
        /// <paramref name="rotationDegrees"/>, sits exactly on <paramref name="contact"/>: the tip is
        /// nailed to the line and the body swings about it. Pure — the inverse of
        /// <c>contact = pivot + Rotate(anchorLocal, rot)</c>, to float precision.
        /// </summary>
        public static Vector2 PivotForAnchor(Vector2 contact, Vector2 anchorLocal, float rotationDegrees)
            => contact - Rotate(anchorLocal, rotationDegrees);

        /// <summary>
        /// The point on a snag target's rim the drifter actually touches: the target's own centre for
        /// a buoy line (radius 0), or the point on a hull's half-beam circle facing the drifter — weed
        /// fouls on the planking, not at the keel. A drifter dead on the centre touches the south rim.
        /// Pure.
        /// </summary>
        public static Vector2 ContactPoint(Vector2 piecePos, Vector2 targetPos, float targetRadius)
        {
            if (targetRadius <= 0f) return targetPos;
            Vector2 dir = piecePos - targetPos;
            float mag = dir.magnitude;
            if (mag < 1e-5f) dir = Vector2.down; else dir /= mag;
            return targetPos + dir * targetRadius;
        }

        /// <summary>
        /// Index of the nearest snag target whose RIM is within <paramref name="reach"/> of
        /// <paramref name="pos"/> (distance to the centre minus the target's own radius), or −1. With
        /// every radius 0 this is exactly <see cref="NearestWithin(Vector2,Vector2[],int,float)"/>,
        /// which is what keeps the player's buoy-only case byte-identical to round 1. Only the first
        /// <paramref name="count"/> entries are live. Pure, allocation-free.
        /// </summary>
        public static int NearestWithin(Vector2 pos, Vector2[] points, float[] radii, int count, float reach)
        {
            int best = -1;
            float bestGap = reach;
            for (int i = 0; i < count; i++)
            {
                float r = radii != null && i < radii.Length ? radii[i] : 0f;
                float gap = (points[i] - pos).magnitude - r;
                if (gap <= bestGap) { bestGap = gap; best = i; }
            }
            return best;
        }

        /// <summary>
        /// The wave-energy release: a hooked clump lets go when the swell at its anchor lifts or drops
        /// the surface by <paramref name="breakWaveMeters"/> or more. 0 = never (the shipped default —
        /// the timed release and the haul remain the ways off a line). Pure, deterministic from the
        /// field.
        /// </summary>
        public static bool BreaksFree(float waveHeightAtAnchor, float breakWaveMeters)
            => breakWaveMeters > 0f && Mathf.Abs(waveHeightAtAnchor) >= breakWaveMeters;
    }
}
