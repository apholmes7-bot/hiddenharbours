using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// One rectangular feature of a mainland region — the shoal a harbour stands on, a barachois pond
    /// behind the shore, a wharf deck, a breakwater crest, a gravel spit. Flat at
    /// <see cref="Elevation"/> inside the rectangle and easing (smoothstep) to the surrounding ground
    /// across <see cref="Falloff"/> metres outside it.
    ///
    /// <para><b>One primitive, two uses.</b> The same struct is a CARVE (composed downward — it can only
    /// lower the ground) or a FILL (composed upward — it can only raise it), decided by which list it is
    /// authored into on <see cref="MainlandTidalTerrain"/>. That split is deliberate: a wharf deck must be
    /// unable to lower the seabed under it, and a pond must be unable to lift a road out of itself.</para>
    /// </summary>
    [System.Serializable]
    public struct MainlandZone
    {
        [Tooltip("Centre of the rectangular feature (world XY).")]
        public Vector2 Center;
        [Tooltip("Half-size (m) of the flat part either side of the centre (x = half-width, y = half-height).")]
        public Vector2 HalfSize;
        [Tooltip("Elevation (m above chart datum) of the flat part. A carve cuts DOWN to it; a fill " +
                 "builds UP to it.")]
        public float Elevation;
        [Tooltip("How far (m) outside the rectangle the feature eases back into the surrounding ground — " +
                 "a dredged edge is steep, a pond's margin is gentle.")]
        public float Falloff;

        public MainlandZone(Vector2 center, Vector2 halfSize, float elevation, float falloff)
        {
            Center = center; HalfSize = halfSize; Elevation = elevation; Falloff = falloff;
        }
    }

    /// <summary>
    /// A CHANNEL — a lane of water cut along a polyline, deepest on its centre-line (the thalweg) and
    /// easing back up to the surrounding ground at <see cref="HalfWidthMetres"/>. The third primitive
    /// after the carve and the fill, and the only one that is not a rectangle.
    ///
    /// <para><b>Why it cannot be a carve.</b> A carve composes BEFORE the fills, which is right for a
    /// pond behind the shore and useless for a harbour: at Nine Mile Creek the basin's water is a FILL
    /// (the shoal that raises the −6 m bay to the ruled −1.6 m gate), so a carve authored through it is
    /// max-composed straight back out again — the mirror image of the defect that made the first draft's
    /// basin four metres too deep. A channel is what cuts made ground, so it composes LAST.</para>
    ///
    /// <para><b>Why it stops at <see cref="CuttableCeiling"/>.</b> Dredging removes seabed, never
    /// structure. Ground already standing above the ceiling — a quay deck, a breakwater crest, the
    /// spit's yard — is left exactly where it is, so a channel may be authored hard against a wharf
    /// without eating the wharf. That guard is what lets the route hug the apron the float hangs off.</para>
    ///
    /// <para><b>Why the profile is smoothstep rather than a hard V.</b> It is the gut's own idiom
    /// (<see cref="MainlandTidalTerrain.SmoothFalloff"/>, the cut through the tidal bar), and it earns
    /// its keep twice: the trough is flat enough that a snapped fairway station finds an INTERIOR
    /// minimum rather than sliding to an edge, and the banks feather into the flats instead of ending in
    /// a crease no seabed has.</para>
    ///
    /// <para><b>The width shrinks with the tide BY CONSTRUCTION.</b> Nothing animates: the waterline is
    /// wherever the falling water meets the sloping bank, so a channel whose bed is below the lowest
    /// water narrows as the tide drops and never closes. What the ebb reveals is geometry, not state.</para>
    /// </summary>
    [System.Serializable]
    public class MainlandChannel
    {
        [Tooltip("The channel's centre-line (world XY), in order. Fewer than two points = ABSENT.")]
        public Vector2[] Waypoints = new Vector2[0];
        [Tooltip("Thalweg elevation (m above chart datum) — the bed on the centre-line, and the deepest " +
                 "the channel ever cuts.")]
        public float BedElevation;
        [Tooltip("Half-width (m): how far either side of the centre-line the cut reaches before the bed " +
                 "is back at the surrounding ground. 0 = ABSENT.")]
        public float HalfWidthMetres;
        [Tooltip("The channel only cuts ground at or below this elevation (m). Anything higher is " +
                 "STRUCTURE — a deck, a crest, a yard — and a channel does not dredge a wharf away.")]
        public float CuttableCeiling;

        public MainlandChannel() { }

        public MainlandChannel(Vector2[] waypoints, float bedElevation, float halfWidthMetres,
                               float cuttableCeiling)
        {
            Waypoints = waypoints ?? new Vector2[0];
            BedElevation = bedElevation;
            HalfWidthMetres = halfWidthMetres;
            CuttableCeiling = cuttableCeiling;
        }
    }

    /// <summary>
    /// The authored <b>height map</b> for a MAINLAND region — the concrete <see cref="ITidalTerrain"/>
    /// Nine Mile Creek stands on, and the third member of the family after <see cref="TidalTerrain"/>
    /// (an island: a plateau on an ellipse, ringed by a coast plan) and <see cref="RectTidalTerrain"/>
    /// (a strip: axis-aligned quays over a dredged floor).
    ///
    /// <para><b>What a mainland is, as geometry.</b> Not a shape you can be outside of — a SIDE. The
    /// coastline is an open run (<see cref="MainlandCoast"/>): land to landward of it all the way to the
    /// region edge, sea to seaward, and the coast plan says what the shore between them IS at every
    /// point of the run. Everything else the place needs — the tidal bar that crosses to the island, the
    /// harbour shoal the wharf stands on, the barachois behind the spit — composes on top of that one
    /// side.</para>
    ///
    /// <para><b>The composition order is load-bearing, and it is this:</b></para>
    /// <list type="number">
    /// <item><description>the deep floor;</description></item>
    /// <item><description><b>max</b> the natural coast profile (the plateau and its shore);</description></item>
    /// <item><description><b>max</b> the tidal bar's ridge, then cut its gut back through it;</description></item>
    /// <item><description><b>min</b> every CARVE — the ponds and lagoons behind the shore — never
    /// below the floor;</description></item>
    /// <item><description><b>max</b> every FILL — the harbour shoal, the spit, the wharf decks, the
    /// breakwater;</description></item>
    /// <item><description><b>min</b> every CHANNEL — the lanes dredged or scoured back through the
    /// made ground, and the only step that composes after the fills.</description></item>
    /// </list>
    /// <para>Carves before fills is the whole reason a deck can stand over water the tide has already
    /// claimed: take the ground DOWN first, then build on it. Fills before carves would hand a pond a
    /// deck to eat. (<see cref="TidalTerrain"/> has the same hazard and solves it the same way, with the
    /// berth carve clamped by <c>Mathf.Min</c> so it can only cut.)</para>
    ///
    /// <para><b>Channels after fills, for the opposite reason</b>, and the two are not in tension: a
    /// carve is ground the tide got to BEFORE anyone built, a channel is a lane cut THROUGH what was
    /// built. Authoring a harbour's channel as a carve does nothing at all, because the harbour's own
    /// water is a fill that max-composes it away again. <see cref="MainlandChannel.CuttableCeiling"/> is
    /// what keeps the last word from being a destructive one: a channel may only cut seabed, so it
    /// cannot dredge away the wharf it runs alongside.</para>
    ///
    /// <para><b>Deterministic, never saved.</b> Elevation is a pure function of world position — no RNG,
    /// nothing serialized at runtime; the field is reconstructed geometry, recomputed not persisted
    /// (CLAUDE.md rule 5). Registers itself into <see cref="GameServices.TidalTerrain"/> on enable and
    /// relinquishes on disable (guarded, so an additive scene swap's last writer wins), the same
    /// self-installing pattern as its two siblings.</para>
    ///
    /// <para><b>Tide-FRACTION elevations, never metres.</b> The cliff toe and the ledge bench are
    /// authored as fractions of the region's tide amplitude, for the reason the 2026-08-01 pacing pass
    /// proved: when the owner retunes the amplitude, an absolute metre silently strands or drowns every
    /// ledge in the region, and a fraction moves with the water instead.</para>
    /// </summary>
    public sealed class MainlandTidalTerrain : MonoBehaviour, ITidalTerrain
    {
        [Header("Deep floor (the open bay everywhere no feature reaches)")]
        [Tooltip("Seabed elevation of the open water, metres above chart datum. Well below the lowest " +
                 "tide so the bay never bares and a boat never grounds out there.")]
        [SerializeField] private float _deepElevation = -6f;

        [Header("The coastline (an OPEN run; the SEA is on the RIGHT of the authored walk direction)")]
        [Tooltip("The shoreline as a polyline, authored end to end across the region. Nine Mile Creek " +
                 "runs it SOUTH → NORTH with the water EAST, which puts the sea on the right hand.")]
        [SerializeField] private Vector2[] _coastPoints = new Vector2[0];

        [Tooltip("The coast plan: what each stretch of the run IS, keyed by distance along it. EMPTY = " +
                 "soft shore everywhere, so a region that authors no plan cannot be moved by cliffs.")]
        [SerializeField] private CoastRunSector[] _coastSectors = new CoastRunSector[0];

        [Tooltip("Metres of run over which two neighbouring sectors' profiles are blended. Without it a " +
                 "cliff meeting a beach is a step ACROSS the shore — a wall running out to sea. Every " +
                 "sector must be at least twice this long or it never reaches its own class.")]
        [SerializeField] private float _coastFeatherMetres = 8f;

        [Header("The land behind the shore")]
        [Tooltip("Elevation (m above datum) of the fields inland of the shore. Above the highest water " +
                 "at every tide.")]
        [SerializeField] private float _landElevation = 6f;
        [Tooltip("How far (m) SEAWARD of the shoreline the soft shore takes to fall from the plateau to " +
                 "the shelf below it — the beach band the tide's waterline sweeps across.")]
        [SerializeField] private float _shoreFalloff = 24f;

        [Header("The foreshore shelf (the shoal the bay shallows over)")]
        [Tooltip("Width (m) of the shallow shelf beyond the beach. 0 = ABSENT — the beach drops straight " +
                 "to the deep floor.")]
        [SerializeField] private float _shelfWidth = 60f;
        [Tooltip("Shelf bed at its INNER edge (m above datum) — the shallow side, against the beach.")]
        [SerializeField] private float _shelfInnerElevation = -1f;
        [Tooltip("Shelf bed at its OUTER edge (m above datum), where it drops away to the deep floor.")]
        [SerializeField] private float _shelfOuterElevation = -2.6f;

        [Header("Cliff profile (see CoastClass for what each class means)")]
        [Tooltip("How far (m seaward of the shoreline) the plateau takes to fall from its full height to " +
                 "the cliff's foot. This is the WALL: small is vertical.")]
        [SerializeField] private float _cliffPlungeWidth = 3f;
        [Tooltip("How far BELOW the lowest spring water a plain cliff's foot sits, as a fraction of the " +
                 "tide's amplitude. A FRACTION, never metres — see the type note.")]
        [SerializeField] private float _cliffToeTideFraction = 0.45f;
        [Tooltip("How far ABOVE the lowest spring water the low-tide ledge's bench sits, as a fraction " +
                 "of the amplitude. Small = bares only on the big tides.")]
        [SerializeField] private float _ledgeBenchTideFraction = 0.25f;
        [Tooltip("Width (m) of the ledge bench — a shelf the tide lends you, not a beach.")]
        [SerializeField] private float _ledgeBenchWidth = 4f;

        [Header("Tide REFERENCE for authoring (not the sim's tide — the fractions are measured on it)")]
        [Tooltip("Mean water level (m above datum) the tide-fraction elevations are measured from.")]
        [SerializeField] private float _tideMean = 0f;
        [Tooltip("Spring amplitude (m) the tide-fraction elevations are measured in. Must match the " +
                 "region's authored tide; a test asserts it does.")]
        [SerializeField] private float _tideAmplitude = 2.2f;

        [Header("The tidal bar (this region's HALF of the crossing to St Peters)")]
        [Tooltip("Shoreward end of the bar's centre-line (world XY) — on the beach, so the ridge joins " +
                 "the land.")]
        [SerializeField] private Vector2 _barFrom = Vector2.zero;
        [Tooltip("Seaward end of the bar's centre-line (world XY) — the SEAM, where the region hands the " +
                 "crossing to St Peters.")]
        [SerializeField] private Vector2 _barTo = Vector2.zero;
        [Tooltip("Half-width (m) of the bar either side of its centre-line — the flats bare out to here. " +
                 "0 = ABSENT (a region with no crossing).")]
        [SerializeField] private float _barHalfWidth = 0f;
        [Tooltip("Elevation (m above datum) the bar's flanks fall to at the half-width — the bar's " +
                 "SHOULDER, before the bottom drops away to the region's own floor.\n\n" +
                 "⚠ This is what lets a crossing SPAN A REGION SEAM without visibly narrowing. The bared " +
                 "width of a bar at a given tide is set by how fast its flanks fall, so a bar tapering " +
                 "straight to a -6 m bay bares narrower than the same bar tapering to a -4 m one. Author " +
                 "the shoulder equal on both sides of the seam and the flats are the same width across " +
                 "the load, whatever each region's floor is doing underneath.")]
        [SerializeField] private float _barFlankToeElevation = -4f;
        [Tooltip("How far (m) beyond the half-width the shoulder takes to fall away to the region's deep " +
                 "floor. Past this the bar contributes nothing.")]
        [SerializeField] private float _barFlankFalloff = 40f;
        [Tooltip("Crest elevation of the bar (m above datum), authored JUST BELOW the region's high " +
                 "water so it covers at high tide and bares as the tide falls. MUST match the other " +
                 "half of the crossing or the bar is exposed on one side of the seam and drowned on " +
                 "the other.")]
        [SerializeField] private float _barCrestElevation = 0.88f;
        [Tooltip("Where the gut crosses the bar, as a fraction (0..1) along the From→To centre-line.")]
        [Range(0f, 1f)]
        [SerializeField] private float _gutAlong = 0.45f;
        [Tooltip("Half-width (m) of the gut cut through the bar. 0 = ABSENT.")]
        [SerializeField] private float _gutHalfWidth = 0f;
        [Tooltip("Gut-bed elevation (m above datum). Below the crest so water lingers in it, shallower " +
                 "than the deep floor so it shoals as the tide drops.")]
        [SerializeField] private float _gutBedElevation = -0.6f;

        [Header("Carves (ponds, lagoons, cuts — these can only LOWER the ground)")]
        [SerializeField] private MainlandZone[] _carves = new MainlandZone[0];

        [Header("Fills (shoals, spits, wharf decks, breakwaters — these can only RAISE it)")]
        [SerializeField] private MainlandZone[] _fills = new MainlandZone[0];

        [Header("Channels (lanes cut back through the fills — these can only LOWER it, and only seabed)")]
        [SerializeField] private MainlandChannel[] _channels = new MainlandChannel[0];

        // The run's length is needed by EVERY elevation sample (the blend needs to know where the last
        // sector ends) and it is a sum over the whole polyline. A painted-seabed bake at 2 px/m over
        // this region is 1.7 million samples, so recomputing it per sample is 1.7 million redundant
        // passes over fourteen segments (rule 7). Cached, and invalidated wherever the plan can change.
        // Not serialized — a plain private field keeps its initialiser through Unity's deserialisation.
        private float _runLength = -1f;

        private float RunLength
        {
            get
            {
                if (_runLength < 0f) _runLength = MainlandCoast.RunLength(_coastPoints);
                return _runLength;
            }
        }

        private void OnEnable()
        {
            _runLength = -1f;                       // a scene-deserialised plan recomputes on activation
            GameServices.TidalTerrain = this;
        }

        private void OnDisable()
        {
            // Only relinquish the accessor if it still points at us (don't stomp a region that
            // registered after we did during an additive scene swap) — mirrors both siblings.
            if (ReferenceEquals(GameServices.TidalTerrain, this))
                GameServices.TidalTerrain = null;
        }

        /// <inheritdoc/>
        public float ElevationAt(Vector2 worldPos) => ElevationAtZones(worldPos);

        // ---- authoring seam (the builder pushes the whole plan in one call) ---------------------------

        /// <summary>The authored coastline, for the builder's push and for a test that reads the plan the
        /// scene actually carries rather than the one the builder meant to write.</summary>
        public Vector2[] CoastPoints
        {
            get => _coastPoints;
            set { _coastPoints = value ?? new Vector2[0]; _runLength = -1f; }
        }

        /// <inheritdoc cref="CoastPoints"/>
        public CoastRunSector[] CoastSectors
        {
            get => _coastSectors;
            set => _coastSectors = value ?? new CoastRunSector[0];
        }

        /// <summary>Metres of run the sector joins are feathered over.</summary>
        public float CoastFeatherMetres => _coastFeatherMetres;

        /// <summary>Total length (m) of the authored coast run.</summary>
        public float CoastRunLength => RunLength;

        /// <summary>The reference tide these elevations were authored against — read by the test that
        /// holds it equal to the region's own authored tide, so the two cannot drift apart.</summary>
        public float TideMean => _tideMean;
        /// <inheritdoc cref="TideMean"/>
        public float TideAmplitude => _tideAmplitude;

        /// <summary>The lowest water of the biggest spring tide (m above datum) — the datum the cliff
        /// classes' tide-FRACTION elevations are measured from. An AUTHORING reference, not a tide:
        /// nothing here samples the clock (rule 5).</summary>
        public float SpringLowWater => _tideMean - _tideAmplitude;

        /// <summary>The highest water of the biggest spring tide (m above datum).</summary>
        public float SpringHighWater => _tideMean + _tideAmplitude;

        /// <summary>The plain cliff's foot: below the lowest spring water by a fraction of the amplitude,
        /// so no part of it ever bares, at any tide, in any week.</summary>
        public float CliffToeElevation => SpringLowWater - _cliffToeTideFraction * _tideAmplitude;

        /// <summary>The low-tide ledge's bench: just ABOVE the lowest spring water, so the tide covers it
        /// for most of the cycle and hands it back near dead low.</summary>
        public float LedgeBenchElevation => SpringLowWater + _ledgeBenchTideFraction * _tideAmplitude;

        /// <summary>The deep-shore face's foot — the bay floor itself, so a hull lies close alongside at
        /// any state of tide.</summary>
        public float DeepShoreToeElevation => _deepElevation;

        /// <summary>The bar's crest (m above datum) — read by the seam test that holds this region's half
        /// of the crossing at the same height as St Peters'.</summary>
        public float BarCrestElevation => _barCrestElevation;

        /// <summary>
        /// The steepest seabed gradient (|Δ elevation| per metre) any stretch of THIS coast plan can
        /// actually produce — the number the displaced sea's shore-fade band is sized from.
        ///
        /// <para><b>Why it is derived and not typed.</b> The fade band exists so the displaced mesh does not
        /// TEAR where the ground climbs out from under it, and its width is
        /// <c>coefficient × envelope × exaggeration × gradient</c>. The component's serialized default is
        /// 0.5, which is St Peters' softly-shelving island — a plateau easing to a reef shelf over 20 m. A
        /// mainland whose plan stands a deep-shore cliff straight off the bay floor falls twelve metres in
        /// three, an order of magnitude harder, and the field's own tooltip says which way the error hurts:
        /// overestimating only widens the band, underestimating risks the tear. So the plan answers for
        /// itself, and a future retune of any profile constant moves this with it.</para>
        ///
        /// <para><b>Exact, not sampled.</b> Every band in every profile is a <see cref="Lerped"/> —
        /// a smoothstep — and a smoothstep's gradient peaks at exactly <c>1.5×</c> its mean across the
        /// band (the same fact <c>StPetersStarterSplat</c>'s talus threshold is pinned to). Finite
        /// differences would under-read that peak, and under-reading is the direction that tears.</para>
        ///
        /// <para><b>Only classes the plan DECLARES are counted.</b> A coast of nothing but beach must not
        /// inherit a cliff's number because the cliff profile happens to exist in this file. With no
        /// sectors authored the coast is soft everywhere (<see cref="CoastProfileAt"/>), which is what is
        /// measured then.</para>
        /// </summary>
        public float MaxShoreGradient()
        {
            float worst = 0f;

            if (_coastSectors == null || _coastSectors.Length == 0)
            {
                return SoftGradient();
            }

            for (int i = 0; i < _coastSectors.Length; i++)
            {
                float g = ClassGradient(_coastSectors[i].Class);
                if (g > worst) worst = g;
            }
            return worst;
        }

        /// <summary>The steepest gradient one coast CLASS produces — the per-class half of
        /// <see cref="MaxShoreGradient"/>, split out so a test can name the class it is checking.</summary>
        public float ClassGradient(CoastClass coast)
        {
            float plunge = Mathf.Max(0.01f, _cliffPlungeWidth);
            float outRun = Mathf.Max(1f, _shoreFalloff);   // the run every cliff foot falls away over
            switch (coast)
            {
                // A wall to the toe, then the bottom carrying on away to the floor.
                case CoastClass.Cliff:
                    return Mathf.Max(SmoothPeak(_landElevation, CliffToeElevation, plunge),
                                     SmoothPeak(CliffToeElevation, _deepElevation, outRun));
                // The same wall, and its toe IS the floor — so the band beyond it is flat.
                case CoastClass.DeepShoreCliff:
                    return SmoothPeak(_landElevation, DeepShoreToeElevation, plunge);
                // Wall to a bench, flat across the bench, then away to the floor.
                case CoastClass.LedgeCliff:
                    return Mathf.Max(SmoothPeak(_landElevation, LedgeBenchElevation, plunge),
                                     SmoothPeak(LedgeBenchElevation, _deepElevation, outRun));
                // Beach, Dune and Access differ in what is PAINTED on them, never in how high they are.
                default:
                    return SoftGradient();
            }
        }

        /// <summary>The soft chain's steepest band: beach, then the shelf, then the drop-off. With no
        /// shelf authored the chain collapses to beach → floor, exactly as <see cref="ShoreProfile"/>
        /// composes it.</summary>
        private float SoftGradient()
        {
            bool hasShelf = _shelfWidth > 0f;
            float beachOuter = hasShelf ? _shelfInnerElevation : _deepElevation;
            float worst = SmoothPeak(_landElevation, beachOuter, Mathf.Max(1e-4f, _shoreFalloff));
            if (!hasShelf) return worst;

            worst = Mathf.Max(worst, SmoothPeak(_shelfInnerElevation, _shelfOuterElevation, _shelfWidth));
            worst = Mathf.Max(worst, SmoothPeak(_shelfOuterElevation, _deepElevation,
                                                Mathf.Max(1f, _shoreFalloff)));
            return worst;
        }

        /// <summary>Peak |gradient| of one smoothstep band: 1.5 × the mean slope across it. See
        /// <see cref="MaxShoreGradient"/> for why 1.5 is exact rather than a safety factor.</summary>
        public static float SmoothPeak(float fromElevation, float toElevation, float widthMetres)
            => 1.5f * Mathf.Abs(toElevation - fromElevation) / Mathf.Max(widthMetres, 1e-4f);

        /// <summary>Which class of coast stands at a world position, unfeathered — what the plan SAYS.
        /// The decider the ground paint, the cliff walls and the decor all read, so a dead classifier
        /// moves every one of them together instead of quietly moving none.</summary>
        public CoastClass CoastClassAt(Vector2 worldPos)
        {
            MainlandCoast.Project(_coastPoints, worldPos, out float along, out _);
            return MainlandCoast.ClassAt(_coastSectors, along);
        }

        /// <summary>
        /// Author the whole plan in one call (the region builder's push, before the scene is saved — the
        /// same direct-configure convention as <c>RegionAnchor.Configure</c> and
        /// <see cref="RectTidalTerrain.Configure"/>). Everything is a plain value, so an EditMode test can
        /// build the authored terrain without a scene.
        /// </summary>
        public void Configure(float deepElevation,
                              Vector2[] coastPoints, CoastRunSector[] coastSectors, float coastFeatherMetres,
                              float landElevation, float shoreFalloff,
                              float shelfWidth, float shelfInnerElevation, float shelfOuterElevation,
                              float cliffPlungeWidth, float cliffToeTideFraction,
                              float ledgeBenchTideFraction, float ledgeBenchWidth,
                              float tideMean, float tideAmplitude,
                              Vector2 barFrom, Vector2 barTo, float barHalfWidth, float barCrestElevation,
                              float barFlankToeElevation, float barFlankFalloff,
                              float gutAlong, float gutHalfWidth, float gutBedElevation,
                              MainlandZone[] carves, MainlandZone[] fills,
                              MainlandChannel[] channels = null)
        {
            _deepElevation = deepElevation;
            _coastPoints = coastPoints ?? new Vector2[0];
            _runLength = -1f;
            _coastSectors = coastSectors ?? new CoastRunSector[0];
            _coastFeatherMetres = coastFeatherMetres;
            _landElevation = landElevation;
            _shoreFalloff = shoreFalloff;
            _shelfWidth = shelfWidth;
            _shelfInnerElevation = shelfInnerElevation;
            _shelfOuterElevation = shelfOuterElevation;
            _cliffPlungeWidth = cliffPlungeWidth;
            _cliffToeTideFraction = cliffToeTideFraction;
            _ledgeBenchTideFraction = ledgeBenchTideFraction;
            _ledgeBenchWidth = ledgeBenchWidth;
            _tideMean = tideMean;
            _tideAmplitude = tideAmplitude;
            _barFrom = barFrom;
            _barTo = barTo;
            _barHalfWidth = barHalfWidth;
            _barCrestElevation = barCrestElevation;
            _barFlankToeElevation = barFlankToeElevation;
            _barFlankFalloff = barFlankFalloff;
            _gutAlong = Mathf.Clamp01(gutAlong);
            _gutHalfWidth = gutHalfWidth;
            _gutBedElevation = gutBedElevation;
            _carves = carves ?? new MainlandZone[0];
            _fills = fills ?? new MainlandZone[0];
            _channels = channels ?? new MainlandChannel[0];
        }

        // ---- the composition (pure; no Unity object state beyond the serialized plan) ------------------

        /// <summary>
        /// The pure zone composition (no scene, no RNG) — exposed so an EditMode test can assert the
        /// authored coast without loading anything. See the type note for why the order is what it is.
        /// </summary>
        public float ElevationAtZones(Vector2 worldPos)
        {
            float e = _deepElevation;

            // 1. The natural coast: fields inland, the plan's own profile seaward.
            float coast = CoastProfileAt(worldPos);
            if (coast > e) e = coast;

            // 2. The bar's ridge, and the gut cut back through it.
            if (_barHalfWidth > 0f)
            {
                // Crest → shoulder across the half-width, then shoulder → floor beyond it. TWO disjoint
                // bands composed as a chain, never as a max of two Lerpeds: Lerped holds its outer value
                // forever, so a max would pin the whole bay at the shoulder's -4 m.
                float dBar = DistanceToSegment(worldPos, _barFrom, _barTo);
                float bar = dBar <= _barHalfWidth
                    ? Lerped(dBar, 0f, _barHalfWidth, _barCrestElevation, _barFlankToeElevation)
                    : Lerped(dBar, _barHalfWidth, Mathf.Max(1f, _barFlankFalloff),
                             _barFlankToeElevation, _deepElevation);
                if (bar > e) e = bar;

                if (_gutHalfWidth > 0f)
                {
                    Vector2 crossing = Vector2.Lerp(_barFrom, _barTo, _gutAlong);
                    float dGut = DistanceAlongAxisFrom(worldPos, _barFrom, _barTo, crossing);
                    if (dGut < _gutHalfWidth && e > _gutBedElevation)
                    {
                        float carve = SmoothFalloff(dGut, _gutHalfWidth);
                        e = Mathf.Max(Mathf.Lerp(e, _gutBedElevation, carve), _deepElevation);
                    }
                }
            }

            // 3. Carves: they can only ever cut DOWN (Mathf.Min), and never below the floor.
            for (int i = 0; i < _carves.Length; i++)
            {
                MainlandZone z = _carves[i];
                if (e <= z.Elevation) continue;
                float d = DistanceOutsideRect(worldPos, z.Center, z.HalfSize);
                float w = SmoothFalloff(d, z.Falloff);
                if (w <= 0f) continue;
                float carved = Mathf.Lerp(e, z.Elevation, w);
                e = Mathf.Max(Mathf.Min(e, carved), _deepElevation);
            }

            // 4. Fills: they can only ever build UP.
            for (int i = 0; i < _fills.Length; i++)
            {
                MainlandZone z = _fills[i];
                float d = DistanceOutsideRect(worldPos, z.Center, z.HalfSize);
                float built = Lerped(d, 0f, z.Falloff, z.Elevation, _deepElevation);
                if (built > e) e = built;
            }

            // 5. Channels: the lanes cut back through the made ground. LAST, because the harbour's own
            //    water is a fill and a cut composed before it would simply be built over again — and
            //    only ever DOWN (Mathf.Min), never below the floor, exactly like a carve.
            for (int i = 0; i < _channels.Length; i++)
            {
                MainlandChannel c = _channels[i];
                if (c == null || c.HalfWidthMetres <= 0f) continue;
                if (c.Waypoints == null || c.Waypoints.Length < 2) continue;

                // ⚠ THE GUARD THAT MAKES "LAST" SAFE: a channel dredges SEABED. Ground standing above
                // the ceiling is structure — a deck, a crest, a yard — and stays exactly where it is,
                // so a route may be authored hard against a quay without eating the quay.
                if (e > c.CuttableCeiling) continue;
                if (e <= c.BedElevation) continue;          // already deeper than this channel asks for

                float d = DistanceToPolyline(c.Waypoints, worldPos);
                float w = SmoothFalloff(d, c.HalfWidthMetres);
                if (w <= 0f) continue;
                float cut = Mathf.Lerp(e, c.BedElevation, w);
                e = Mathf.Max(Mathf.Min(e, cut), _deepElevation);
            }

            return e;
        }

        /// <summary>
        /// The natural coast's cross-section at a world position: the plateau inland, and seaward of the
        /// shoreline the radial profile of whichever coast class stands there, blended with its neighbour
        /// across the joins.
        ///
        /// <para>With no plan authored this is the SOFT profile everywhere, which is the same
        /// plateau → beach → shelf → drop-off chain <c>TidalTerrain.ShoreProfile</c> draws around the
        /// island — so the two regions shelve identically and a hull crossing from one to the other reads
        /// the same ground.</para>
        /// </summary>
        public float CoastProfileAt(Vector2 worldPos)
        {
            if (!MainlandCoast.IsRun(_coastPoints)) return _deepElevation;

            MainlandCoast.Project(_coastPoints, worldPos, out float along, out float seaward);
            return ProfileAt(along, seaward);
        }

        /// <summary>
        /// The natural coast's cross-section in the run's OWN frame — the same blend
        /// <see cref="CoastProfileAt"/> composes, addressed by <paramref name="along"/> the run and
        /// <paramref name="seaward"/> of the shoreline instead of by a world point.
        ///
        /// <para><b>⭐ ONE LAW, TWO CONSUMERS — and this overload is why the second one can exist.</b> A
        /// cliff-wall generator has to ask for the profile at an EXACT distance seaward of an exact metre
        /// of shore (the foot of the plunge), and the world-point form cannot answer that: stepping out
        /// along the normal and re-projecting lands at a slightly different <c>along</c> wherever the coast
        /// turns, so the drawn wall would be sampled off a station the terrain never had. Splitting the
        /// frame out means the picture is taken from literally the method the walk gate and the seabed bake
        /// read, which is the property that keeps a wall from disagreeing with the ground under it — the
        /// same arrangement <c>StPetersCliffWalls</c> makes with <c>TidalTerrain.IslandProfileAt</c>.</para>
        ///
        /// <para>The sector FEATHER therefore comes through for free: a run's drop tapers as it approaches
        /// a beach, so a cliff dies into the sand instead of ending in a step.</para>
        /// </summary>
        public float ProfileAt(float along, float seaward)
        {
            if (seaward <= 0f) return _landElevation;                    // inland: the fields

            if (_coastSectors == null || _coastSectors.Length == 0) return ShoreProfile(seaward);

            MainlandCoast.BlendAt(_coastSectors, along, RunLength,
                                  _coastFeatherMetres,
                                  out CoastClass primary, out CoastClass secondary, out float weight);
            if (weight >= 1f || primary == secondary) return ClassProfile(seaward, primary);
            return Mathf.Lerp(ClassProfile(seaward, secondary), ClassProfile(seaward, primary), weight);
        }

        /// <summary>
        /// The seaward cross-section for one coast CLASS, by distance seaward of the shoreline.
        ///
        /// <para>The three soft classes all fall through to <see cref="ShoreProfile"/>: <c>Beach</c>,
        /// <c>Dune</c> and <c>Access</c> differ in what is PAINTED and PLANTED on them, not in how high
        /// they are — the same statement <see cref="CoastClass"/> makes for the island.</para>
        /// </summary>
        public float ClassProfile(float seaward, CoastClass coast)
        {
            if (seaward <= 0f) return _landElevation;
            switch (coast)
            {
                case CoastClass.Cliff:          return CliffProfile(seaward, CliffToeElevation);
                case CoastClass.DeepShoreCliff: return CliffProfile(seaward, DeepShoreToeElevation);
                case CoastClass.LedgeCliff:     return LedgeProfile(seaward);
                default:                        return ShoreProfile(seaward);
            }
        }

        /// <summary>
        /// The SOFT coast, as one explicit chain of disjoint bands: <b>shore → beach → shelf →
        /// drop-off → deep floor</b>. With no shelf authored the chain collapses to beach → floor.
        ///
        /// <para><b>⚠ Disjoint bands, composed as a chain and never with min/max.</b>
        /// <see cref="Lerped"/> holds its OUTER value for every distance past its band, so combining the
        /// bands with <c>max</c> pins the whole sea at the beach's outer value — the defect that spread
        /// St Peters' reef shelf to the horizon the first time that profile was written.</para>
        /// </summary>
        public float ShoreProfile(float seaward)
        {
            float beachEnd = _shoreFalloff;
            bool hasShelf = _shelfWidth > 0f;
            float beachOuter = hasShelf ? _shelfInnerElevation : _deepElevation;

            if (seaward <= beachEnd || !hasShelf)
                return Lerped(seaward, 0f, _shoreFalloff, _landElevation, beachOuter);

            float shelfEnd = beachEnd + _shelfWidth;
            if (seaward <= shelfEnd)
                return Lerped(seaward, beachEnd, _shelfWidth, _shelfInnerElevation, _shelfOuterElevation);

            return Lerped(seaward, shelfEnd, Mathf.Max(1f, _shoreFalloff),
                          _shelfOuterElevation, _deepElevation);
        }

        /// <summary>A standing wall: the plateau holds its full height to the very shoreline, then falls
        /// to <paramref name="toeElevation"/> in <c>_cliffPlungeWidth</c> metres, and the ground carries
        /// on away to the deep floor beyond. No beach, no shelf.</summary>
        private float CliffProfile(float seaward, float toeElevation)
        {
            float plunge = Mathf.Max(0.01f, _cliffPlungeWidth);
            if (seaward <= plunge)
                return Lerped(seaward, 0f, plunge, _landElevation, toeElevation);

            // Past the foot the seabed falls away over the beach's own width, so a cliff base shoals no
            // faster than the coast it interrupts. (A toe already AT the floor — the deep-shore class —
            // makes this band flat, which is what "lie close alongside" means.)
            return Lerped(seaward, plunge, Mathf.Max(1f, _shoreFalloff), toeElevation, _deepElevation);
        }

        /// <summary>The low-tide ledge: the same wall, stopped on a narrow bench a little above the lowest
        /// water, which then drops away. The bench is the whole point — drowned for most of the cycle,
        /// walkable near dead low.</summary>
        private float LedgeProfile(float seaward)
        {
            float plunge = Mathf.Max(0.01f, _cliffPlungeWidth);
            float bench = LedgeBenchElevation;
            if (seaward <= plunge) return Lerped(seaward, 0f, plunge, _landElevation, bench);

            float benchEnd = plunge + Mathf.Max(0f, _ledgeBenchWidth);
            if (seaward <= benchEnd) return bench;

            return Lerped(seaward, benchEnd, Mathf.Max(1f, _shoreFalloff), bench, _deepElevation);
        }

        // --- pure helpers (static, testable; the same shapes TidalTerrain uses, so the two agree) -------

        /// <summary>A plateau-with-falloff profile by distance: <paramref name="inner"/> at/inside
        /// <paramref name="flatRadius"/>, easing (smoothstep) to <paramref name="outer"/> by
        /// <c>flatRadius + falloff</c>, then flat at <paramref name="outer"/> beyond.</summary>
        public static float Lerped(float distance, float flatRadius, float falloff, float inner, float outer)
        {
            if (distance <= flatRadius) return inner;
            if (falloff <= 0f) return outer;
            float u = Mathf.Clamp01((distance - flatRadius) / falloff);
            return Mathf.Lerp(inner, outer, Mathf.SmoothStep(0f, 1f, u));
        }

        /// <summary>1 at d = 0, smoothly easing to 0 at d = half (and 0 beyond).</summary>
        public static float SmoothFalloff(float d, float half)
        {
            if (half <= 0f) return 0f;
            float u = Mathf.Clamp01(d / half);
            return 1f - Mathf.SmoothStep(0f, 1f, u);
        }

        /// <summary>Shortest distance from <paramref name="p"/> to the segment a→b.</summary>
        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + t * ab);
        }

        /// <summary>Shortest distance from <paramref name="p"/> to a polyline — the minimum over its
        /// segments. <see cref="float.MaxValue"/> for an empty route, so "no route" reads as
        /// "infinitely far" and every caller's falloff resolves to nothing rather than to everything.
        /// </summary>
        public static float DistanceToPolyline(Vector2[] points, Vector2 p)
        {
            if (points == null || points.Length == 0) return float.MaxValue;
            if (points.Length == 1) return Vector2.Distance(points[0], p);

            float best = float.MaxValue;
            for (int i = 0; i < points.Length - 1; i++)
            {
                float d = DistanceToSegment(p, points[i], points[i + 1]);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>How far "along" the a→b axis <paramref name="p"/> is from
        /// <paramref name="crossing"/> — what gives the bar's gut its width ACROSS the bar.</summary>
        public static float DistanceAlongAxisFrom(Vector2 p, Vector2 a, Vector2 b, Vector2 crossing)
        {
            Vector2 axis = b - a;
            if (axis.sqrMagnitude <= 1e-6f) return Vector2.Distance(p, crossing);
            axis.Normalize();
            return Mathf.Abs(Vector2.Dot(p - crossing, axis));
        }

        /// <summary>Distance (m) from <paramref name="p"/> to the edge of the axis-aligned rectangle
        /// (<paramref name="center"/> ± <paramref name="halfSize"/>); 0 anywhere inside it.</summary>
        public static float DistanceOutsideRect(Vector2 p, Vector2 center, Vector2 halfSize)
        {
            float dx = Mathf.Max(Mathf.Abs(p.x - center.x) - Mathf.Max(halfSize.x, 0f), 0f);
            float dy = Mathf.Max(Mathf.Abs(p.y - center.y) - Mathf.Max(halfSize.y, 0f), 0f);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
