using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The world's authored <b>height map</b> for a region — the concrete <see cref="ITidalTerrain"/>
    /// the St Peters opening hangs on. It publishes a per-position ground/seabed elevation (metres above
    /// chart datum, higher = drier) and registers itself into <see cref="GameServices.TidalTerrain"/> on
    /// enable so gameplay (the on-foot walkability sim) and the future depth-gradient water shader read it
    /// through Core WITHOUT referencing the World module (CLAUDE.md rule 4; ADR 0009). It clears the
    /// accessor on disable so a region teardown leaves "open water" (a null terrain) behind.
    ///
    /// <para><b>Authored ELEVATION ZONES (deterministic, never saved).</b> Elevation is a pure function of
    /// world position composed from a few authored zones — there is <b>no RNG</b> and <b>nothing is
    /// serialized at runtime</b> (the field is reconstructed geometry, recomputed not persisted — CLAUDE.md
    /// rule 5). The St Peters showcase, evaluated against the deterministic water level
    /// (<see cref="IEnvironmentService.WaterLevelAt"/>) via <see cref="TidalExposure"/>:</para>
    /// <list type="bullet">
    /// <item><description><b>Island</b> — a high plateau (always exposed; you can't tide it under).</description></item>
    /// <item><description><b>Sandbar</b> — a ridge crest just BELOW high water that bridges the island to
    /// Nine Mile Creek: covered at high tide, exposing as the tide falls (widest walkable flat at low water). The
    /// showcase's walker path.</description></item>
    /// <item><description><b>Channel</b> — a deeper trough cut THROUGH the sandbar: boat-crossable at higher
    /// tide, narrowing as the tide falls. The showcase's boat passage — inverse of the flats over the tide.</description></item>
    /// <item><description><b>Deep harbour</b> — a low seabed everywhere else (never bares; always boatable).</description></item>
    /// <item><description><b>Dredged approach</b> — a flat-bottomed cut from the drop-off in to the
    /// berth pocket at the dock. Unlike every zone above it it NEVER bares: it is a dredge, authored
    /// deep enough to carry its region's arrival hull at the lowest spring water, so it NARROWS
    /// rather than dries as the tide falls.</description></item>
    /// <item><description><b>Berth pocket</b> — a short flat-bottomed cut ALONGSIDE a wharf face, at the
    /// approach's own depth, holding the hull that lies there. Separate from the approach because the
    /// pier stands over the approach's centre-line, so its water is under the planks rather than where
    /// a hull alongside actually floats.</description></item>
    /// </list>
    /// The zones are smoothly blended (the ridge and channel are raised-cosine bumps, not hard steps) so the
    /// shoreline and channel banks creep across the flats continuously as the tide moves — the continuous
    /// low→high transformation §7 calls for, not a staircase.
    ///
    /// <para><b>Pull, not push.</b> Sampled on demand by <see cref="ElevationAt"/>; nothing is precomputed
    /// per tile per tick. Authored geometry lives in the serialized zone fields so the owner can tune the
    /// showcase from the inspector (no magic numbers in code — CLAUDE.md rule 6).</para>
    /// </summary>
    public sealed class TidalTerrain : MonoBehaviour, ITidalTerrain
    {
        [Header("Deep harbour (the floor everywhere else)")]
        [Tooltip("Seabed elevation of the open/deep water, metres above chart datum. Well below the lowest " +
                 "tide so the harbour never bares and a boat never grounds there.")]
        [SerializeField] private float _deepHarbourElevation = -4f;

        [Header("Island (the high home ground)")]
        [Tooltip("Centre of the island plateau (world XY).")]
        [SerializeField] private Vector2 _islandCenter = new Vector2(-40f, 0f);
        [Tooltip("Radius (m) of the flat island plateau along X (inside this it sits at the plateau height).")]
        [SerializeField] private float _islandRadius = 22f;
        [Tooltip("Radius (m) along Y. 0 = circular (use the X radius on both axes) — the original " +
                 "greybox shape. Set it to make the island an ELLIPSE: a real island is longer than it " +
                 "is wide, and St Peters is ruled at ~450 × 260 m (scene-sizing §5.1), which no disc " +
                 "can express.")]
        [SerializeField] private float _islandRadiusY = 0f;
        [Tooltip("How far (m) the island's beach slopes down from the plateau edge into the sea.")]
        [SerializeField] private float _islandFalloff = 10f;
        [Tooltip("Island plateau elevation, metres above chart datum. High enough to stay dry at every " +
                 "tide (always exposed).")]
        [SerializeField] private float _islandElevation = 6f;

        [Header("Reef shelf (the ring that makes landing hard for all but shallow draught)")]
        [Tooltip("Width (m) of the shallow shelf that rings the island beyond its beach. 0 = ABSENT — the " +
                 "beach drops straight to the deep floor, the original greybox profile. A shelf turns " +
                 "'reefs make landing hard' into authored terrain instead of decoration: draught is " +
                 "already real data and the tide already decides depth, so the ring gates hulls for free.")]
        [SerializeField] private float _reefShelfWidth = 0f;
        [Tooltip("Shelf bed at its INNER edge (m above datum) — the shallow side, against the beach.")]
        [SerializeField] private float _reefShelfInnerElevation = -1.0f;
        [Tooltip("Shelf bed at its OUTER edge (m above datum), where it drops away to the deep floor.")]
        [SerializeField] private float _reefShelfOuterElevation = -1.5f;

        [Header("Berth (the ONE door in the reef — the beach slip a boat comes home through)")]
        [Tooltip("Half-width (m) of the berth channel. 0 = ABSENT. This is a LOCAL DEPRESSION carved from " +
                 "deep water to the shore, so it cuts a door through the reef ring; without it a ringed " +
                 "island has no way in for anything that floats.")]
        [SerializeField] private float _berthHalfWidth = 0f;
        [Tooltip("Seaward end of the berth channel's centre-line (world XY) — out in deep water.")]
        [SerializeField] private Vector2 _berthFrom = Vector2.zero;
        [Tooltip("Shoreward end of the berth channel's centre-line (world XY) — at the shoreline.")]
        [SerializeField] private Vector2 _berthTo = Vector2.zero;
        [Tooltip("Berth bed elevation (m above datum). The BEACH slip's own bed: shallow enough that it " +
                 "dries near spring low, so wading out to it is a tide-gated thing, and deep enough to " +
                 "clear the skiff/punt tier for most of the cycle. The DOCK's water is the dredged " +
                 "approach below, which is a separate cut and never dries.")]
        [SerializeField] private float _berthBedElevation = -1.0f;
        [Tooltip("Half-width (m) of the berth's FLAT bottom. 0 = the original single-slope trough, " +
                 "byte-identical to the profile that predates the dredged approach. Above 0 the cut " +
                 "becomes a trapezoid: flat at the bed out to here, then shouldering up to the natural " +
                 "seabed by the half-width — which is what gives a channel a navigable WIDTH at low " +
                 "water instead of a single deep point.")]
        [SerializeField] private float _berthThalwegHalfWidth = 0f;

        [Header("Dredged approach (the way to the dock — the cut that never dries)")]
        [Tooltip("Half-width (m) of the dredged approach channel. 0 = ABSENT, and the region is exactly " +
                 "the one that predates it. This is a SECOND cut on the same line as the berth: the " +
                 "berth slip is the beach landing, this is the working boat's way to the wharf. Both " +
                 "only ever cut DOWN, so the deeper of the two wins wherever they overlap and the pair " +
                 "still reads as ONE door through the reef.")]
        [SerializeField] private float _approachHalfWidth = 0f;
        [Tooltip("Seaward end of the approach's centre-line (world XY) — authored ON the contour where " +
                 "the natural seabed is already at the design depth, so the cut lands flush on the " +
                 "harbour floor rather than ending in a sill nothing can cross.")]
        [SerializeField] private Vector2 _approachFrom = Vector2.zero;
        [Tooltip("Shoreward end of the approach's centre-line (world XY) — the head of the berth " +
                 "pocket, far enough in that the hull it is dredged for lies wholly in dredged water.")]
        [SerializeField] private Vector2 _approachTo = Vector2.zero;
        [Tooltip("Half-width (m) of the approach's FLAT dredged bottom — the navigable width the " +
                 "channel keeps at the lowest water. Sized from the beam of the hull it is cut for.")]
        [SerializeField] private float _approachThalwegHalfWidth = 0f;
        [Tooltip("Approach bed elevation (m above datum). Authored as spring low MINUS (the arrival " +
                 "hull's draught + her under-keel clearance), so the channel carries her at EVERY state " +
                 "of tide — the owner's 2026-08-19 ruling that the east dock always has water.")]
        [SerializeField] private float _approachBedElevation = -4.0f;

        [Header("Alongside berth pocket (the water the hull lies IN, beside the wharf face)")]
        [Tooltip("Half-width (m) of the berth pocket. 0 = ABSENT, and the region is bit-for-bit the one " +
                 "that predates it — every region without an alongside berth is untouched by this field. " +
                 "A THIRD cut on the same only-ever-downward rule as the berth slip and the approach. " +
                 "Why it exists: a channel dredged down the middle of a finger pier puts its water UNDER " +
                 "the planks, where nothing floats. A hull lying ALONGSIDE lies off the pier's face, a " +
                 "beam's width out — ground the channel's own flat bottom never reaches. This cut is that " +
                 "berth, and nothing else: it is deliberately short, so the channel's narrowest section " +
                 "(which is what gates the fleet) is somewhere else entirely and does not move.")]
        [SerializeField] private float _berthPocketHalfWidth = 0f;
        [Tooltip("One end of the pocket's centre-line (world XY) — authored ALONG the wharf face the hull " +
                 "lies against, offset off it by (her half-beam + her fendering gap), so the centre-line " +
                 "is literally where her keel goes.")]
        [SerializeField] private Vector2 _berthPocketFrom = Vector2.zero;
        [Tooltip("Other end of the pocket's centre-line (world XY) — the hull's other end. The pair is her " +
                 "own footprint at the berth, never a length picked by eye.")]
        [SerializeField] private Vector2 _berthPocketTo = Vector2.zero;
        [Tooltip("Half-width (m) of the pocket's FLAT bottom — her half-beam plus the room she is steered " +
                 "in with. The flat has to hold her WHOLE beam: a hull half over the shoulder is a hull " +
                 "that touches on one side as the tide falls.")]
        [SerializeField] private float _berthPocketThalwegHalfWidth = 0f;
        [Tooltip("Pocket bed elevation (m above datum) — the SAME dredge as the approach that feeds it. " +
                 "A berth shallower than its own channel is a berth you can reach and not lie in.")]
        [SerializeField] private float _berthPocketBedElevation = -4.0f;

        [Header("Sandbar ridge (the tide-gated walking path to Nine Mile Creek)")]
        [Tooltip("One end of the sandbar's centre-line (world XY) — toward the island.")]
        [SerializeField] private Vector2 _sandbarFrom = new Vector2(-22f, 0f);
        [Tooltip("Other end of the sandbar's centre-line (world XY) — toward Nine Mile Creek.")]
        [SerializeField] private Vector2 _sandbarTo = new Vector2(34f, 0f);
        [Tooltip("Half-width (m) of the sandbar either side of its centre-line — the flats bare out to here.")]
        [SerializeField] private float _sandbarHalfWidth = 9f;
        [Tooltip("Crest elevation of the sandbar, metres above chart datum. Authored JUST BELOW the " +
                 "region's high water so the bar covers at high tide and emerges as the tide falls — the " +
                 "widest walkable flat at low water.")]
        [SerializeField] private float _sandbarCrestElevation = 1.6f;

        [Header("Coast plan (which stretch of shoreline is cliff, beach, dune, trail)")]
        [Tooltip("The shoreline as a closed ring of sectors, clockwise by bearing (N = 0) in the " +
                 "island's ELLIPSE-NORMALISED frame. EMPTY = the original radial coast everywhere, " +
                 "byte-identical to the profile that predates cliffs — so a region that authors no plan " +
                 "cannot be moved by this feature.")]
        [SerializeField] private CoastSector[] _coastSectors = new CoastSector[0];

        [Tooltip("Degrees of bearing over which two neighbouring sectors' profiles are blended. Without " +
                 "it a cliff meeting a beach is a 6.5 m STEP across the shore — a wall running out to " +
                 "sea. With it a cliff run tapers into the beach at each end, the way a headland does. " +
                 "Every sector must be at least twice this wide or it never reaches its own class.")]
        [SerializeField] private float _coastBlendDegrees = 3f;

        [Header("Cliff profile (the plunge; see CoastClass for what each class means)")]
        [Tooltip("How far (m, in elliptical distance) the plateau takes to fall from its full height to " +
                 "the cliff's foot. This is the WALL: small is vertical. The beach next door spends 20 m " +
                 "on a seventh of the same drop.")]
        [SerializeField] private float _cliffPlungeWidth = 3f;

        [Tooltip("How far BELOW the lowest spring water a plain cliff's foot sits, as a fraction of the " +
                 "tide's amplitude. Authored as a tide FRACTION, never as metres: an amplitude retune " +
                 "then moves the foot with the water instead of silently stranding it above the tide.")]
        [SerializeField] private float _cliffToeTideFraction = 0.25f;

        [Tooltip("How far ABOVE the lowest spring water the low-tide ledge's bench sits, as a fraction " +
                 "of the tide's amplitude. Small = bares only on the big tides. Also a tide FRACTION, " +
                 "and for the same reason — an absolute metre here drowns or strands every ledge in the " +
                 "region on the owner's next tide-pacing pass.")]
        [SerializeField] private float _ledgeBenchTideFraction = 0.25f;

        [Tooltip("Width (m, elliptical distance) of the ledge bench — the walkable foreshore at the " +
                 "cliff's foot. Narrow on purpose: it is a shelf the tide lends you, not a beach.")]
        [SerializeField] private float _ledgeBenchWidth = 4f;

        [Header("Tide REFERENCE for authoring (not the sim's tide — see the note on SpringLowWater)")]
        [Tooltip("Mean water level (m above datum) the tide-fraction elevations above are measured from.")]
        [SerializeField] private float _tideMean = 0f;
        [Tooltip("Spring amplitude (m) the tide-fraction elevations above are measured in. Must match " +
                 "the region's authored tide; a test asserts it does.")]
        [SerializeField] private float _tideAmplitude = 2.2f;

        [Header("Channel (the boat passage cut through the sandbar)")]
        [Tooltip("Where the channel crosses the sandbar, as a fraction (0..1) along the From→To centre-line.")]
        [Range(0f, 1f)]
        [SerializeField] private float _channelAlong = 0.62f;
        [Tooltip("Half-width (m) of the channel cut. Boat-crossable at higher tide; the flats either side " +
                 "bare first as the tide falls, narrowing the safe gap.")]
        [SerializeField] private float _channelHalfWidth = 4.5f;
        [Tooltip("Channel-bed elevation, metres above chart datum. Below the crest (so water lingers in the " +
                 "gut), but shallower than the deep harbour so it narrows / shoals as the tide drops.")]
        [SerializeField] private float _channelBedElevation = -0.6f;

        private void OnEnable() => GameServices.TidalTerrain = this;

        private void OnDisable()
        {
            // Only relinquish the accessor if it still points at us (don't stomp a region that
            // registered after we did during an additive scene swap).
            if (ReferenceEquals(GameServices.TidalTerrain, this))
                GameServices.TidalTerrain = null;
        }

        /// <inheritdoc/>
        public float ElevationAt(Vector2 worldPos) => ElevationAtZones(worldPos);

        /// <summary>
        /// The pure zone composition (no Unity calls, no RNG) — exposed so an EditMode test can assert the
        /// authored zones without a scene. Composes the deep-harbour floor with the island plateau, the
        /// sandbar ridge, and the channel trough by taking the max ground (whichever feature is highest at
        /// the position wins), then carving the channel back down through the bar and the
        /// berth and its dredged approach back down through the reef.
        /// </summary>
        public float ElevationAtZones(Vector2 worldPos)
        {
            // Deep harbour is the floor; the island and sandbar raise the ground above it where present.
            float e = _deepHarbourElevation;

            // Island: plateau → beach → (reef shelf → drop-off, if a shelf is authored) → deep floor.
            float dIsland = IslandDistance(worldPos, _islandCenter, _islandRadius, _islandRadiusY);
            float island = IslandProfileAt(dIsland, worldPos);
            if (island > e) e = island;

            // Sandbar: a ridge along the From→To segment. Raise toward the crest near the centre-line,
            // falling to the deep floor at the half-width edge (so the flats' shoreline creeps as tide moves).
            float dBar = DistanceToSegment(worldPos, _sandbarFrom, _sandbarTo);
            float bar = Lerped(dBar, 0f, _sandbarHalfWidth, _sandbarCrestElevation, _deepHarbourElevation);
            if (bar > e) e = bar;

            // Channel: a trough cut across the bar at the crossing point. Where the cut applies, pull the
            // ground DOWN toward the channel bed (a boat-crossable gut) — but never below the deep floor.
            Vector2 crossing = Vector2.Lerp(_sandbarFrom, _sandbarTo, _channelAlong);
            float dChannel = DistanceToSegmentPerpendicular(worldPos, _sandbarFrom, _sandbarTo, crossing);
            if (dChannel < _channelHalfWidth && e > _channelBedElevation)
            {
                // 1 at the channel centre-line, easing to 0 at the channel edge — carve smoothly.
                float carve = SmoothFalloff(dChannel, _channelHalfWidth);
                float carved = Mathf.Lerp(e, _channelBedElevation, carve);
                e = Mathf.Max(carved, _deepHarbourElevation);
            }

            // Berth: the ONE door in the reef. A local depression along a short centre-line running from
            // deep water to the shore, carved the same way the channel is — so a hull can reach the slip
            // without crossing the shelf, while everywhere else the ring still gates it. Only ever cuts
            // DOWN (Mathf.Min), so it cannot raise the seabed anywhere, and never below the deep floor.
            //
            // ⭐ TWO CUTS ON ONE LINE, and that is the ruling wearing geometry's clothes. The BERTH is
            // the beach slip: it bares near spring low, which is what makes wading out to it mean
            // reading the tide. The APPROACH is the dredged channel and berth pocket that serves the
            // WHARF, and the owner ruled (2026-08-19) that it always holds water — it is the game's
            // front door, and being piloted in at dead low tide must not run you aground on your own
            // arrival. Both only ever cut DOWN, so the deeper wins where they overlap and the pair
            // reads as one door rather than two.
            e = Carve(e, worldPos, _berthFrom, _berthTo,
                      _berthHalfWidth, _berthThalwegHalfWidth, _berthBedElevation);
            e = Carve(e, worldPos, _approachFrom, _approachTo,
                      _approachHalfWidth, _approachThalwegHalfWidth, _approachBedElevation);
            // ⭐ AND THE BERTH ITSELF. The approach above is the way IN; this is the water she lies in
            // once she is there. They are separate because a finger pier stands ON the channel's
            // centre-line, so the channel's flat bottom is under the PLANKS — and a hull moored
            // alongside lies off the pier's face, a beam's width outboard of anything the channel
            // dredged. Widening the channel until it reached her would have widened the DOOR, which
            // is the region's draught gate and the owner's to move; a pocket is local, so the
            // channel's narrowest section — the thing that actually gates the fleet — is untouched.
            e = Carve(e, worldPos, _berthPocketFrom, _berthPocketTo,
                      _berthPocketHalfWidth, _berthPocketThalwegHalfWidth, _berthPocketBedElevation);

            return e;
        }

        /// <summary>
        /// Cut a channel along <paramref name="from"/>→<paramref name="to"/> into the ground at
        /// <paramref name="e"/>. <b>The one carve</b> — the berth slip and the dredged approach are the
        /// same shape at different numbers, and two copies of this arithmetic is two places for a
        /// dredge to disagree with itself.
        ///
        /// <para><b>The section is a TRAPEZOID</b>: flat at <paramref name="bed"/> out to
        /// <paramref name="thalwegHalf"/>, then shouldering up to the ground it found by
        /// <paramref name="half"/>. With <c>thalwegHalf = 0</c> the flat vanishes and the shape is
        /// exactly the single-slope trough that predates the approach, so a channel that authors no
        /// thalweg answers bit-for-bit what it answered before.</para>
        ///
        /// <para><b>Why a flat bottom is the whole point.</b> A pure smoothstep trough has its design
        /// depth at ONE point — the centre-line — so the width that actually carries a hull at low
        /// water is whatever the slope happens to give, and for this island that was 3.7 m against a
        /// 4.8 m beam. The flat states the navigable width instead of leaving it to fall out of a
        /// falloff curve, and the shoulders still narrow it as the tide drops, which is the
        /// "shrinks but never dries" reading the ruling asks for.</para>
        ///
        /// <para>Only ever cuts DOWN (<see cref="Mathf.Min"/>), so it cannot raise the seabed anywhere.
        /// The floor is the deep harbour OR the bed itself, whichever is lower: a dredged cut is
        /// allowed to be deeper than the natural floor around it — that is what dredging is — and
        /// clamping it at the ambient floor would silently cap a future retune.</para>
        /// </summary>
        private float Carve(float e, Vector2 worldPos, Vector2 from, Vector2 to,
                            float half, float thalwegHalf, float bed)
        {
            if (half <= 0f || e <= bed) return e;

            float d = DistanceToSegment(worldPos, from, to);
            if (d >= half) return e;

            float floor = Mathf.Min(_deepHarbourElevation, bed);
            float flat = Mathf.Clamp(thalwegHalf, 0f, half);

            // ⚠ The flat bottom returns the BED ITSELF, not Lerp(e, bed, 1). They differ by an ULP
            // or so — Mathf.Lerp is a + (b − a) · t, which is not exactly b at t = 1 — and an ULP is
            // enough to turn "carries 1.80 m" into "carries 1.7999994 m" and fail a depth test that
            // is right. A dredged bottom is flat AT the depth it was dredged to; say so exactly.
            if (d <= flat) return Mathf.Max(Mathf.Min(e, bed), floor);

            float carved = Mathf.Lerp(e, bed, SmoothFalloff(d - flat, half - flat));
            return Mathf.Max(Mathf.Min(e, carved), floor);
        }

        /// <summary>
        /// The island's cross-section by elliptical distance alone — <b>the radial signature, kept</b>.
        ///
        /// <para><b>⭐ THE ADDITIVE GUARANTEE.</b> The coast became bearing-dependent when cliffs landed,
        /// and this overload is how that stayed a widening rather than a rewrite: it delegates to
        /// <see cref="IslandProfile(float, CoastClass)"/> with <see cref="CoastClass.Beach"/>, which
        /// falls through to <see cref="ShoreProfile"/> — the pre-cliff method, moved down one level and
        /// not otherwise edited. Every existing caller compiles unchanged and every unchanged sector
        /// answers bit-for-bit what it answered before. Callers that want the coast the region actually
        /// authored want <see cref="IslandProfileAt"/>, which knows where it is.</para>
        /// </summary>
        public float IslandProfile(float dIsland) => IslandProfile(dIsland, CoastClass.Beach);

        // =============================================================================================
        //  THE COAST PLAN — where the island stands up
        // =============================================================================================

        /// <summary>The authored sectors, for the builder's push and for a test that reads the plan the
        /// scene actually carries rather than the one the builder meant to write.</summary>
        public CoastSector[] CoastSectors
        {
            get => _coastSectors;
            set => _coastSectors = value ?? new CoastSector[0];
        }

        /// <summary>Degrees of bearing the sector joins are feathered over (see the field's tooltip and
        /// <see cref="CoastPlan.BlendAt"/> for why a hard join is a bug rather than a cliff).</summary>
        public float CoastBlendDegrees => _coastBlendDegrees;

        /// <summary>
        /// The lowest water of the biggest spring tide, in metres above datum — the datum the cliff
        /// classes' tide-FRACTION elevations are measured from.
        ///
        /// <para><b>⚠ This is an AUTHORING reference, not a tide.</b> Nothing here samples the clock or
        /// the environment service; the sim's water level is still recomputed from
        /// <c>(worldSeed, gameTime)</c> through <see cref="IEnvironmentService.WaterLevelAt"/> and
        /// nothing about the tide is saved (rule 5). These two numbers only say what the region's tide
        /// was DESIGNED as, so a ledge authored "a quarter of the amplitude above the lowest water"
        /// still means that after the owner retunes the amplitude — which is precisely what the
        /// 2026-08-01 pacing pass proved absolute metres cannot survive.</para>
        /// </summary>
        public float SpringLowWater => _tideMean - _tideAmplitude;

        /// <summary>The reference tide these elevations were authored against — read by the test that
        /// holds it equal to the region's own authored tide, so the two cannot drift apart.</summary>
        public float TideMean => _tideMean;
        /// <inheritdoc cref="TideMean"/>
        public float TideAmplitude => _tideAmplitude;

        /// <summary>The plain cliff's foot: below the lowest spring water by a fraction of the amplitude,
        /// so no part of it ever bares, at any tide, in any week.</summary>
        public float CliffToeElevation => SpringLowWater - _cliffToeTideFraction * _tideAmplitude;

        /// <summary>The low-tide ledge's bench: just ABOVE the lowest spring water, so the tide covers it
        /// for most of the cycle and hands it back near dead low.</summary>
        public float LedgeBenchElevation => SpringLowWater + _ledgeBenchTideFraction * _tideAmplitude;

        /// <summary>The deep-shore face's foot — the harbour floor itself, so a hull lies close alongside
        /// at any state of tide and the sounder reads the base as deep water rather than as shoal.</summary>
        public float DeepShoreToeElevation => _deepHarbourElevation;

        /// <summary>Which class of coast stands at a world position, unfeathered — what the plan SAYS.
        /// The decider the cliff-share measure, the ground paint and the decor all read, so a dead
        /// classifier moves every one of them together instead of quietly moving none.</summary>
        public CoastClass CoastClassAt(Vector2 worldPos) =>
            CoastPlan.ClassAt(_coastSectors, Bearing(worldPos));

        /// <summary>This position's bearing in the island's normalised frame — hoisted so the profile,
        /// the classifier and the tests all measure the coast in exactly one way.</summary>
        public float Bearing(Vector2 worldPos) =>
            CoastPlan.BearingAt(worldPos, _islandCenter, _islandRadius, _islandRadiusY);

        /// <summary>
        /// The island's cross-section at a WORLD POSITION — the radial profile of whichever coast class
        /// stands there, blended with its neighbour across the sector joins.
        ///
        /// <para>With no plan authored this is exactly <see cref="IslandProfile(float)"/>, which is
        /// exactly the profile that predates cliffs. That is the additive guarantee: an unchanged sector
        /// — and an entire unchanged region — comes out bit-for-bit as it did before.</para>
        /// </summary>
        public float IslandProfileAt(float dIsland, Vector2 worldPos)
        {
            if (_coastSectors == null || _coastSectors.Length == 0) return IslandProfile(dIsland);

            CoastPlan.BlendAt(_coastSectors, Bearing(worldPos), _coastBlendDegrees,
                              out CoastClass primary, out CoastClass secondary, out float weight);
            if (weight >= 1f || primary == secondary) return IslandProfile(dIsland, primary);
            return Mathf.Lerp(IslandProfile(dIsland, secondary), IslandProfile(dIsland, primary), weight);
        }

        /// <summary>
        /// The island's cross-section for one coast CLASS, by elliptical distance from the centre.
        ///
        /// <para><b>The soft classes are the original chain, untouched.</b> <see cref="CoastClass.Beach"/>,
        /// <see cref="CoastClass.Dune"/> and <see cref="CoastClass.Access"/> all fall through to
        /// <see cref="ShoreProfile"/>, which is the pre-cliff method moved down a level and not otherwise
        /// edited — so the widening cannot move ground it was not asked to move.</para>
        /// </summary>
        public float IslandProfile(float dIsland, CoastClass coast)
        {
            if (dIsland <= _islandRadius) return _islandElevation;

            switch (coast)
            {
                case CoastClass.Cliff:          return CliffProfile(dIsland, CliffToeElevation);
                case CoastClass.DeepShoreCliff: return CliffProfile(dIsland, DeepShoreToeElevation);
                case CoastClass.LedgeCliff:     return LedgeProfile(dIsland);
                default:                        return ShoreProfile(dIsland);
            }
        }

        /// <summary>
        /// A standing wall: the plateau holds its full height to the very edge, then falls to
        /// <paramref name="toeElevation"/> in <c>_cliffPlungeWidth</c> metres, and the ground carries on
        /// away to the deep floor beyond. No beach, no shelf — that is the difference the owner asked
        /// for, stated as geometry rather than as decoration.
        /// </summary>
        private float CliffProfile(float dIsland, float toeElevation)
        {
            float plungeEnd = _islandRadius + Mathf.Max(0.01f, _cliffPlungeWidth);
            if (dIsland <= plungeEnd)
                return Lerped(dIsland, _islandRadius, Mathf.Max(0.01f, _cliffPlungeWidth),
                              _islandElevation, toeElevation);

            // Past the foot the seabed falls away to the harbour floor over the beach's own width, so a
            // cliff base shoals no faster than the coast it interrupts. (A toe already AT the floor —
            // the deep-shore class — makes this band flat, which is what "lie close alongside" means.)
            return Lerped(dIsland, plungeEnd, Mathf.Max(1f, _islandFalloff),
                          toeElevation, _deepHarbourElevation);
        }

        /// <summary>
        /// The low-tide ledge: the same wall, stopped on a narrow flat bench a little above the lowest
        /// water, which then drops away to the floor. The bench is the whole point — it is under water
        /// for most of the cycle and walkable near dead low, so the coast hands the player a strip of
        /// ground twice a month and takes it back.
        /// </summary>
        private float LedgeProfile(float dIsland)
        {
            float plunge = Mathf.Max(0.01f, _cliffPlungeWidth);
            float plungeEnd = _islandRadius + plunge;
            float bench = LedgeBenchElevation;

            if (dIsland <= plungeEnd)
                return Lerped(dIsland, _islandRadius, plunge, _islandElevation, bench);

            float benchEnd = plungeEnd + Mathf.Max(0f, _ledgeBenchWidth);
            if (dIsland <= benchEnd) return bench;          // the flat the tide works

            return Lerped(dIsland, benchEnd, Mathf.Max(1f, _islandFalloff),
                          bench, _deepHarbourElevation);
        }

        /// <summary>
        /// The SOFT coast's cross-section as one explicit chain of bands, by elliptical distance from the
        /// centre: <b>plateau → beach → reef shelf → drop-off → deep floor</b>. With no shelf authored
        /// (<c>_reefShelfWidth</c> = 0) the chain collapses to plateau → beach → floor, byte-identical to
        /// the original greybox profile.
        ///
        /// <para><b>⚠ Why a chain and not a set of <see cref="Lerped"/> calls combined with min/max.</b>
        /// <c>Lerped</c> holds its OUTER value for every distance past its band, so each band's term is a
        /// constant across the whole rest of the sea. Combining them with <c>max</c> then pins the entire
        /// seabed at the beach's outer value — the reef shelf's −1.0 m spread to the horizon in the first
        /// version of this method, and every hull in the game could suddenly cross anywhere. <c>min</c>
        /// fails the mirror-image way, flattening the apron. The bands are disjoint, so the composition
        /// has to be too.</para>
        /// </summary>
        private float ShoreProfile(float dIsland)
        {
            float beachEnd = _islandRadius + _islandFalloff;
            bool hasShelf = _reefShelfWidth > 0f;
            float beachOuter = hasShelf ? _reefShelfInnerElevation : _deepHarbourElevation;

            if (dIsland <= beachEnd || !hasShelf)
                return Lerped(dIsland, _islandRadius, _islandFalloff, _islandElevation, beachOuter);

            // §5.1a: "≈ −1.0 to −1.5 m around the rest of the coast, shallowing to the beaches."
            float shelfEnd = beachEnd + _reefShelfWidth;
            if (dIsland <= shelfEnd)
                return Lerped(dIsland, beachEnd, _reefShelfWidth,
                              _reefShelfInnerElevation, _reefShelfOuterElevation);

            // The drop-off, over the beach's own width so it reads as a slope rather than a cliff.
            return Lerped(dIsland, shelfEnd, Mathf.Max(1f, _islandFalloff),
                          _reefShelfOuterElevation, _deepHarbourElevation);
        }

        // --- pure helpers (static, testable) ----------------------------------------------------------

        /// <summary>
        /// Distance from an island centre, in units of the X radius — so the same
        /// <see cref="Lerped"/> plateau/falloff profile draws an ELLIPSE rather than only a disc.
        ///
        /// <para>The Y offset is scaled by <c>radiusX / radiusY</c> before the magnitude is taken, so a
        /// point exactly on the ellipse returns <paramref name="radiusX"/> and the beach falloff beyond
        /// it is measured in the same units on both axes. <paramref name="radiusY"/> ≤ 0 means
        /// "circular" and returns the plain distance — byte-identical to the original greybox
        /// behaviour, so no existing scene or test moves.</para>
        /// </summary>
        public static float IslandDistance(Vector2 worldPos, Vector2 center, float radiusX, float radiusY)
        {
            Vector2 d = worldPos - center;
            if (radiusY <= 0f || radiusX <= 0f) return d.magnitude;
            return new Vector2(d.x, d.y * (radiusX / radiusY)).magnitude;
        }

        /// <summary>
        /// A plateau-with-falloff profile by distance: <paramref name="inner"/> at/inside
        /// <paramref name="flatRadius"/>, easing (smoothstep) to <paramref name="outer"/> by
        /// <c>flatRadius + falloff</c>, then flat at <paramref name="outer"/> beyond.
        /// </summary>
        private static float Lerped(float distance, float flatRadius, float falloff, float inner, float outer)
        {
            if (distance <= flatRadius) return inner;
            if (falloff <= 0f) return outer;
            float u = Mathf.Clamp01((distance - flatRadius) / falloff);
            return Mathf.Lerp(inner, outer, Mathf.SmoothStep(0f, 1f, u));
        }

        /// <summary>1 at d=0, smoothly easing to 0 at d=half (and 0 beyond). A raised-cosine-ish bump.</summary>
        private static float SmoothFalloff(float d, float half)
        {
            if (half <= 0f) return 0f;
            float u = Mathf.Clamp01(d / half);
            return 1f - Mathf.SmoothStep(0f, 1f, u);
        }

        /// <summary>Shortest distance from <paramref name="p"/> to the segment a→b.</summary>
        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            Vector2 proj = a + t * ab;
            return Vector2.Distance(p, proj);
        }

        /// <summary>Distance from <paramref name="p"/> to the line that crosses the bar at
        /// <paramref name="crossing"/> PERPENDICULAR to the bar's a→b axis — i.e. how far "along" the bar
        /// the point is from the channel cut. This is what gives the channel its width ACROSS the bar.</summary>
        private static float DistanceToSegmentPerpendicular(Vector2 p, Vector2 a, Vector2 b, Vector2 crossing)
        {
            Vector2 axis = (b - a);
            if (axis.sqrMagnitude <= 1e-6f) return Vector2.Distance(p, crossing);
            axis.Normalize();
            // Signed distance along the bar axis from the crossing point.
            return Mathf.Abs(Vector2.Dot(p - crossing, axis));
        }
    }
}
