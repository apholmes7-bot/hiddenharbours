using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Drop this on a hull and it churns the advected foam buffer (ADR 0027 #6) where it works
    /// against the water. It is the item's INJECTION seam, and it reads BOTH components of
    /// hull-vs-water relative motion:
    ///
    /// <list type="number">
    /// <item><b>Horizontal</b> — speed through the water (world velocity minus the tidal current, so
    /// a boat drifting WITH the stream churns nothing while a boat holding station against it
    /// does). The classic wake case.</item>
    /// <item><b>Vertical</b> — the hull's heave RATE relative to the local wave surface. This is the
    /// one nothing served before, and it is why the owner moved this item forward from the fleet era
    /// (2026-08-01): <i>"The hull doesn't create realistic foam when bobbing etc."</i> A dory at her
    /// mooring in a chop has zero speed and should still churn.</item>
    /// </list>
    ///
    /// <para><b>How the bob signal is obtained, stated plainly.</b> A hull that tracked the surface
    /// perfectly would displace nothing and churn nothing — the churn IS the hull's inertia losing
    /// the race with the wave face. So this samples the same displaced sea the ride already samples
    /// (<c>WaveFieldAnimator</c> + <see cref="ShoreFadeMath.DisplacedHeight"/>, through the
    /// <see cref="DisplacedSea"/> Core seam, at the surface's own <c>FreqScale</c> so it reads the sea
    /// as DRAWN) and models the hull's response as a first-order lag,
    /// <see cref="FoamBuffer.FollowSurface"/>. <b>It never writes anything back to Boats</b> — no
    /// force, no pose, no heave — and it holds no reference to a boat class at all, so a buoy, a raft
    /// or a swimmer can carry one later without touching this file. Setting
    /// <c>hullResponseSeconds</c> to 0 makes the hull track the surface exactly and the bob channel
    /// falls silent, which is a real off switch rather than an approximation of one.</para>
    ///
    /// <para><b>Zero cost when idle.</b> Off water (the hull hauled out, or over dry ground at low
    /// tide) it unregisters, so <see cref="FoamInjectionRegistry.Count"/> falls to 0 and
    /// <see cref="IsoFacetHullFeature"/> records no pass at all. Disabled likewise.</para>
    ///
    /// <para><b>Presentation only</b> (rule 5): it reads the sim, feeds no sim, and saves nothing.
    /// Everything downstream is accumulated visual state — see <see cref="FoamBuffer"/>'s
    /// determinism-boundary note.</para>
    ///
    /// <para>ℹ️ This does NOT replace <c>BoatWakeEmitter</c>. The emitter's pooled sprite trail is the
    /// young, bright churn right at the stern; the buffer is the mark left on the sea that persists
    /// and drifts downwind after the boat has gone, plus the bobbing case the emitter has no signal
    /// for. They add.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hidden Harbours/Art/Foam Injector (advected foam buffer)")]
    public sealed class FoamInjector : MonoBehaviour
    {
        [Header("Master")]
        [Tooltip("This hull's contribution scale. 0 = it churns nothing (but still costs its slot — " +
                 "disable the component instead to give the slot back).")]
        [Range(0f, 4f)] [SerializeField] private float _strength = 1f;

        [Tooltip("How much foam a fully-churning hull lays per SECOND (0..1 of a cell's capacity). " +
                 "dt-scaled, so the deposit is frame-rate independent.")]
        [Range(0.1f, 10f)] [SerializeField] private float _depositPerSecond = 2.5f;

        [Tooltip("Half-width of the churned band, metres. Roughly the hull's beam — a dory wants " +
                 "~0.9, a coastal packet several times that.")]
        [Min(0.05f)] [SerializeField] private float _radiusMeters = 0.9f;

        [Tooltip("Metres from the hull's ORIGIN back to her transom, where the churn is actually shed. " +
                 "0 lays the trail at the origin — amidships — which is the owner's 2026-09-04 defect: " +
                 "\"the foam seem to come from the cetnre of a boat when turning and not accuratly from " +
                 "the stern\". On a turn the centre traces a tighter arc than the transom, so the trail " +
                 "springs from her middle. Configured from the hull def by the presentation service; a " +
                 "hull whose stern has never been measured keeps 0 and is unchanged (absence is data).")]
        [Min(0f)] [SerializeField] private float _sternOffsetMeters;

        [Tooltip("The elevation the hull's art was baked at, degrees. The stern is a distance ON THE " +
                 "WATER, and a 3/4 camera draws that distance shorter to the north than to the east — so " +
                 "the offset above is foreshortened exactly as the artwork is, or the trail would breathe " +
                 "toward and away from the transom as she turns. 90 = plan view = no foreshortening.")]
        [Range(1f, 90f)] [SerializeField] private float _bakeElevationDegrees = 90f;

        [Tooltip("Move further than this in ONE frame and it is treated as a teleport (a scene load, " +
                 "a respawn, a debug warp), not as way through the water: the history re-primes and " +
                 "nothing is laid. Without it a teleport paints one capsule of foam straight across " +
                 "the map. Well above any sailed frame step — the fastest hull at 60fps moves ~0.2 m.")]
        [Min(0.5f)] [SerializeField] private float _teleportMeters = 8f;

        [Header("Wake channel — speed through the water")]
        [Tooltip("Speed (m/s) at which the wake channel saturates. The dory's cruise, near enough.")]
        [Min(0.01f)] [SerializeField] private float _wakeSpeedKnee = 3f;
        [Tooltip("Shaping exponent for speed. 1 = linear (a wake builds evenly with way).")]
        [Min(1f)] [SerializeField] private float _wakeExponent = 1f;
        [Tooltip("Weight of the wake channel in the sum. 0 = bobbing only.")]
        [Range(0f, 2f)] [SerializeField] private float _wakeWeight = 1f;

        [Header("Dispersal — the trail widens and thins with age (PR 11b)")]
        [Tooltip("HOW FAST THE CHURN SPREADS ABEAM, as a fraction of the Kelvin slope — the " +
                 "physical outer limit a hull's disturbance can spread at (tan 19.5 degrees per " +
                 "metre of track). The churn inside the arms spreads slower, so this is below 1. " +
                 "0 IS THE PASSTHROUGH: no dispersal, and this hull lays exactly the trail PR 11a " +
                 "shipped, bit for bit. The owner, 2026-09-06: \"the foam always stays to the " +
                 "original foam path, it doesnt widen over time and fade away.\"")]
        [Range(0f, 1f)] [SerializeField] private float _spreadKelvinFraction = 0.8f;

        [Tooltip("THE ENVELOPE the churn spreads to, in multiples of the band's own half-width — " +
                 "where the widening band's outer taper reaches exactly zero, and therefore how far " +
                 "astern the dispersal keeps working. A multiple rather than metres so a dory and a " +
                 "tanker disperse alike (rule 6). 1 = no dispersal, same as a spread of 0.\n\n" +
                 "Its ceiling is not taste: the buffer is 8 bits per channel, and a deposit thinner " +
                 "than half a code is rounded away entirely. See docs/design/water-rendering.md " +
                 "section 35 for where that floor lands.")]
        [Range(1f, 6f)] [SerializeField] private float _spreadEnvelopeHalfBeams = 2.5f;

        [Header("Bob channel — the owner's ask (hull vs wave surface)")]
        [Tooltip("The hull's vertical INERTIA: the time constant of its lag behind the wave surface. " +
                 "0 = the hull tracks the surface exactly and never slaps, so this channel goes " +
                 "silent. The default matches BoatWaveMotion's own motion smoothing.")]
        [Min(0f)] [SerializeField] private float _hullResponseSeconds = 0.2f;
        [Tooltip("Relative vertical speed (m/s) at which the bob channel saturates.")]
        [Min(0.01f)] [SerializeField] private float _slapRateKnee = 0.5f;
        [Tooltip("Shaping exponent for the bob. ABOVE 1 on purpose: a hard slap must churn " +
                 "disproportionately more than a gentle rise-and-fall, which is a super-linear curve, " +
                 "not a steeper straight line.")]
        [Min(1f)] [SerializeField] private float _slapExponent = 2.5f;
        [Tooltip("Weight of the bob channel in the sum. 0 = wake only (today's behaviour).")]
        [Range(0f, 2f)] [SerializeField] private float _slapWeight = 1f;

        // The injector's own eased view of the shared field — the SeaweedPresenter / BoatWakeEmitter
        // pattern (each floater owns one; they all read the same GameServices settings, so they agree).
        private readonly WaveFieldAnimator _animator = new WaveFieldAnimator();

        private bool _registered;
        private bool _primed;
        private Vector2 _previousPosition;
        private float _hullY;
        private float _surfaceY;
        private float _previousHullY;
        private float _previousSurfaceY;

        private FoamInjection _pending;
        private bool _hasPending;
        private int _pendingFrame = -1;

        // ---- the transom's recent TRACK, which is what the dispersal edge rides ----------------
        // A ring of positions stamped at a fixed distance apart, plus the clock reading at each, so
        // the six nodes the shader is given can be resampled at equal ARC LENGTH astern. Distance,
        // not time: the edge's law is a slope, so its reach is the same at any speed and the spacing
        // can be a constant. Allocation-free and fixed size (rule 7); cleared on a teleport with the
        // rest of the history, because a track that jumps is not a track.
        private const int TrailCapacity = 64;
        private readonly Vector2[] _trailPos = new Vector2[TrailCapacity];
        private readonly float[] _trailTime = new float[TrailCapacity];
        private int _trailCount;
        private int _trailNewest;
        private float _elapsed;

        /// <summary>The churned band's half-width (m) — read by the feature when it packs the slot.</summary>
        public float RadiusMeters => Mathf.Max(0.05f, _radiusMeters);

        /// <summary>
        /// Set the churned band's half-width from the hull that carries it.
        /// <see cref="IsoFacetHullPresentationService"/> calls this with the def's
        /// <c>WatertightHalfBeamMeters</c> when it fits an injector to a mesh hull, so the band is the
        /// hull's own beam rather than a constant repeated per boat (rule 6). Floored, so a bad number
        /// degrades to a thin ribbon rather than to a divide.
        /// </summary>
        public void ConfigureRadius(float halfBeamMeters)
        {
            _radiusMeters = Mathf.Max(0.05f, halfBeamMeters);
        }

        /// <summary>
        /// Where this hull sheds her churn: <paramref name="sternOffsetMeters"/> back from the origin along
        /// her heading, projected at <paramref name="bakeElevationDegrees"/>. Configured from the hull def
        /// the same way the radius is — Art must not reach into Boats for a hull's geometry (rule 4), so
        /// the number arrives as data.
        /// </summary>
        public void ConfigureStern(float sternOffsetMeters, float bakeElevationDegrees)
        {
            _sternOffsetMeters = Mathf.Max(0f, sternOffsetMeters);
            _bakeElevationDegrees = Mathf.Clamp(bakeElevationDegrees, 1f, 90f);
        }

        /// <summary>
        /// The point the trail is laid at this frame: her TRANSOM, in world space.
        ///
        /// <para>The offset is foreshortened in Y by <c>sin(bakeElevation)</c> — the same projection the
        /// hull art itself is drawn under. Without it the anchor would sit the full distance astern when
        /// she heads east and too far astern when she heads north, and the gap between hull and foam would
        /// open and close through every turn. (The plume anchor paid for this lesson already: "not even
        /// connected to it and way off to the stern".)</para>
        ///
        /// <para>At <c>_sternOffsetMeters</c> 0 this returns <c>transform.position</c> exactly, which is
        /// the shipped behaviour for any hull whose stern has not been measured.</para>
        /// </summary>
        private Vector2 SternWorld()
            => FoamBuffer.SternWorld((Vector2)transform.position, (Vector2)transform.up,
                                     _sternOffsetMeters, _bakeElevationDegrees);

        private void OnEnable()
        {
            _primed = false;
            _hasPending = false;
            _pendingFrame = -1;
            _trailCount = 0;
            _elapsed = 0f;
        }

        private void OnDisable()
        {
            Leave();
            _primed = false;
            _hasPending = false;
            _pendingFrame = -1;
            _trailCount = 0;
        }

        // LateUpdate: after physics has moved the boat and after BoatWaveMotion has run, so the
        // position this reads is the one the frame actually drew.
        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            // ⚠️ The TRANSOM, not the origin. This one line is half of the owner's 2026-09-04 defect:
            // the churn is shed where the hull leaves the water, and on a turn her centre and her transom
            // trace different arcs. Everything downstream (the depth gate, the speed-through-water, the
            // swept capsule) is unchanged and simply follows the point that is now correct.
            var position = SternWorld();

            var env = GameServices.Environment;
            IGameClock clock = GameServices.Clock;
            ITidalTerrain terrain = GameServices.TidalTerrain;
            if (env == null || clock == null || terrain == null)
            {
                Leave();
                _primed = false;
                return;
            }

            double now = clock.TotalSeconds;
            // The game's one depth rule, computed from Core directly (Art cannot reach Boats'
            // BoatCrossing, and must not — rule 4).
            float depth = TidalExposure.WaterDepth(env.WaterLevelAt(now), terrain.ElevationAt(position));
            if (depth <= 0f)
            {
                // Off the water: hauled out, or over ground that has bared on the ebb. Give the slot
                // back so a scene whose only injector is ashore records no pass at all.
                Leave();
                _primed = false;
                return;
            }
            Join();

            // ---- the sea this hull rides, sampled as the surface DRAWS it -------------------------
            EnvironmentSample sample = env.Sample();
            WaveFieldSettings field = GameServices.WaveField;
            WaveFieldAnimatorSettings smoothing = GameServices.WaveFieldAnimator;
            _animator.Tick(dt, WaveFieldAnimator.GameTimeSeconds,
                           sample.WindVector, sample.SeaState01, in field, in smoothing);

            float exaggeration = 1f;
            float bandMeters = 0f;
            float freqScale = 1f;
            if (DisplacedSea.TryGet(out DisplacedSeaState sea))
            {
                exaggeration = sea.Exaggeration;
                bandMeters = sea.ShoreFadeBandMeters;
                // ⚠️ The DRAWN sea's wavelengths, not the raw field's. Sampling at scale 1 while the
                // surface draws at 2.8 reads a different wave entirely — the documented
                // _OceanSwellScale defect class (see DisplacedSeaState.FreqScale).
                freqScale = sea.FreqScale;
            }
            Vector2 samplePos = freqScale == 1f ? position : position * freqScale;
            float rawHeight = _animator.Sample(samplePos, GameServices.FetchEnvelopeAt(position)).Height;
            float surfaceY = ShoreFadeMath.DisplacedHeight(rawHeight, depth, bandMeters, exaggeration);

            // A teleport is not way through the water. Re-prime rather than laying one capsule of
            // foam straight across the map (scene load, respawn, a debug warp).
            bool teleported = _primed &&
                              (position - _previousPosition).sqrMagnitude >
                              _teleportMeters * _teleportMeters;

            if (!_primed || teleported || dt <= 0f)
            {
                // First tick after (re)entering the water: seed the history so the hull does not
                // register a phantom slap from a standing start, and lay nothing this frame.
                _previousPosition = position;
                _surfaceY = surfaceY;
                _hullY = surfaceY;
                _previousSurfaceY = surfaceY;
                _previousHullY = surfaceY;
                _primed = true;
                _hasPending = false;
                // A track that jumps is not a track: the dispersal edge would ride a chord across
                // the map. Start it again from where she actually is.
                _trailCount = 0;
                AppendTrail(position);
                return;
            }

            _elapsed += dt;
            _previousSurfaceY = _surfaceY;
            _previousHullY = _hullY;
            _surfaceY = surfaceY;
            _hullY = FoamBuffer.FollowSurface(_previousHullY, surfaceY, _hullResponseSeconds, dt);

            // ---- the two motion channels ----------------------------------------------------------
            // Speed THROUGH THE WATER, not over the ground: a boat carried along by the stream is
            // stationary relative to the water she floats on and leaves no wake in it.
            Vector2 groundVelocity = (position - _previousPosition) / dt;
            float horizontalSpeed = (groundVelocity - sample.CurrentVector).magnitude;
            float verticalRate = FoamBuffer.RelativeHeaveRate(_surfaceY, _hullY,
                                                              _previousSurfaceY, _previousHullY, dt);

            float rate01 = FoamBuffer.Injection01(horizontalSpeed, verticalRate,
                                                  _wakeSpeedKnee, _wakeExponent, _wakeWeight,
                                                  _slapRateKnee, _slapExponent, _slapWeight);
            float amount = rate01 * Mathf.Max(_depositPerSecond, 0f) * dt * Mathf.Max(_strength, 0f);
            // The FRESHNESS mark (the buffer's age channel) is the dt-INDEPENDENT rate, scaled by this
            // hull's own contribution dial but never by the frame's dt — a clock reset, not a deposit.
            float vigour = Mathf.Clamp01(rate01 * Mathf.Max(_strength, 0f));

            // ---- the DISPERSAL (PR 11b) ---------------------------------------------------------
            // The share of this hull's churn that leaves her band is taken OUT of the stamp above and
            // handed to the widening edge, so the two together lay exactly what PR 11a laid. The
            // share is DERIVED from the envelope (FoamBuffer.DispersingShare), never dialled — which
            // is also why a spread of 0 leaves the line above untouched, bit for bit.
            AppendTrail(position);
            // ⚠️ Only the churn she makes by MAKING WAY forms a trail that disperses astern. A hull
            // slapping at anchor churns IN PLACE, and the edge's whole premise — that it lays the
            // envelope once, as its rim sweeps past a parcel — needs the rim to be moving: with no way
            // on, the rim stands still and the same ring is painted into the same water every frame
            // until it saturates. So the edge is fed the WAKE channel alone, and the share is taken
            // out of only that part of the stamp, which keeps the two conserved against each other at
            // any mix of the two channels. At rest the bob channel's churn is untouched.
            float wake01 = Mathf.Min(rate01,
                FoamBuffer.Shape01(horizontalSpeed, _wakeSpeedKnee, _wakeExponent)
                * Mathf.Max(_wakeWeight, 0f));
            float depositRate = wake01 * Mathf.Max(_depositPerSecond, 0f) * Mathf.Max(_strength, 0f);
            FoamDispersal dispersal = BuildDispersal(position, depositRate, horizontalSpeed, dt,
                                                     out float share);
            if (share > 0f && rate01 > 1e-6f) amount *= 1f - share * (wake01 / rate01);

            // The capsule from last frame's position to this one — continuous at any frame rate.
            _pending = new FoamInjection(_previousPosition, position, RadiusMeters,
                                         Mathf.Clamp01(amount), vigour, in dispersal);
            // The dispersal can outlive the stamp it was taken from: a gently-working hull whose
            // amount rounds toward nothing may still have a live edge, and dropping the injection
            // would drop that too.
            _hasPending = amount > 1e-5f || dispersal.IsActive;
            _pendingFrame = Time.frameCount;
            _previousPosition = position;
        }

        /// <summary>
        /// This frame's deposit, or false if there is none. Read by the feature once per camera —
        /// deliberately NOT consumed, because a second camera rendering the same frame owns its own
        /// buffer and needs the same deposit. The frame stamp is what stops a stale deposit being
        /// re-applied on a frame this injector never ticked.
        /// </summary>
        internal bool TryTakeInjection(out FoamInjection injection)
        {
            injection = _pending;
            return _hasPending && _pendingFrame == Time.frameCount;
        }

        /// <summary>
        /// The distance between stored track samples, metres — the reach the edge can ever want,
        /// cut into 24. Constant at runtime (both dials are serialized), floored at two world cells
        /// so a sample can never land inside the one before it.
        /// </summary>
        private float TrailStepMeters()
        {
            float slope = FoamBuffer.SpreadSlope(_spreadKelvinFraction);
            float r0 = RadiusMeters;
            float wMax = r0 * Mathf.Max(1f, _spreadEnvelopeHalfBeams);
            float reach = slope > 0f ? (wMax - r0) / slope : 0f;
            return Mathf.Max(2f * FoamBuffer.CellSize, reach / 24f);
        }

        /// <summary>Stamp the transom's position onto the track once it has moved a whole step from
        /// the last stamp. The CURRENT position is always the polyline's head and is not stored, so
        /// node 0 is exact at every frame rate.</summary>
        private void AppendTrail(Vector2 position)
        {
            if (_trailCount > 0)
            {
                float step = TrailStepMeters();
                if ((position - _trailPos[_trailNewest]).sqrMagnitude < step * step) return;
                _trailNewest = (_trailNewest + 1) % TrailCapacity;
                if (_trailCount < TrailCapacity) _trailCount++;
            }
            else
            {
                _trailNewest = 0;
                _trailCount = 1;
            }
            _trailPos[_trailNewest] = position;
            _trailTime[_trailNewest] = _elapsed;
        }

        /// <summary>The i-th stored sample counting BACK from the newest (0 = newest).</summary>
        private Vector2 TrailPos(int back)
        {
            int i = _trailNewest - back;
            while (i < 0) i += TrailCapacity;
            return _trailPos[i];
        }

        private float TrailTime(int back)
        {
            int i = _trailNewest - back;
            while (i < 0) i += TrailCapacity;
            return _trailTime[i];
        }

        /// <summary>How much track is actually behind her, metres — measured from the live transom
        /// position back through every stored sample. A wake that has only just started is SHORT, and
        /// the dispersal is sized to what is there rather than to what it would like.</summary>
        private float TrailLength(Vector2 head)
        {
            float total = 0f;
            Vector2 prev = head;
            for (int i = 0; i < _trailCount; i++)
            {
                Vector2 next = TrailPos(i);
                total += (next - prev).magnitude;
                prev = next;
            }
            return total;
        }

        /// <summary>
        /// The point <paramref name="astern"/> metres back along the track, and how long ago she was
        /// there. Walks the polyline and interpolates within the segment it lands in; past the oldest
        /// sample it clamps, which only happens when the caller has already shortened the reach to
        /// <see cref="TrailLength"/>.
        /// </summary>
        private void SampleTrail(Vector2 head, float astern, out Vector2 point, out float ageSeconds)
        {
            point = head;
            ageSeconds = 0f;
            if (astern <= 0f) return;
            float walked = 0f;
            Vector2 prev = head;
            float prevTime = _elapsed;
            for (int i = 0; i < _trailCount; i++)
            {
                Vector2 next = TrailPos(i);
                float nextTime = TrailTime(i);
                float len = (next - prev).magnitude;
                if (len > 1e-6f && walked + len >= astern)
                {
                    float t = (astern - walked) / len;
                    point = Vector2.Lerp(prev, next, t);
                    ageSeconds = Mathf.Max(0f, _elapsed - Mathf.Lerp(prevTime, nextTime, t));
                    return;
                }
                walked += len;
                prev = next;
                prevTime = nextTime;
            }
            point = prev;
            ageSeconds = Mathf.Max(0f, _elapsed - prevTime);
        }

        /// <summary>
        /// This frame's dispersal edge, and the share of the churn it carries (0 when there is none,
        /// which is what keeps the stamp above bit-identical to PR 11a's).
        ///
        /// <para>The envelope is cut down to what her available track can reach, so the share, the
        /// envelope integral and the gain all describe the SAME annulus and conservation holds while
        /// a wake is still being born — not only once it is full length.</para>
        /// </summary>
        private FoamDispersal BuildDispersal(Vector2 head, float depositRate, float speed, float dt,
                                             out float share)
        {
            share = 0f;
            float slope = FoamBuffer.SpreadSlope(_spreadKelvinFraction);
            float r0 = RadiusMeters;
            if (slope <= 0f || depositRate <= 0f || dt <= 0f) return default;

            float wWanted = r0 * Mathf.Max(1f, _spreadEnvelopeHalfBeams);
            if (wWanted <= r0) return default;

            // A wake still being born has a short track, so the envelope is cut down to what she
            // can actually reach — the share, the envelope integral and the gain then all describe
            // the SAME annulus and conservation holds while she is getting under way, not only once
            // the trail is full length. Below a couple of cells there is no annulus to speak of.
            float reach = Mathf.Min((wWanted - r0) / slope, TrailLength(head));
            if (reach < 2f * FoamBuffer.CellSize) return default;
            float wMax = r0 + slope * reach;
            if (wMax <= r0) return default;

            share = FoamBuffer.DispersingShare(r0, wMax);
            if (share <= 0f) return default;

            // The edge advances at slope x speed; its soft width is floored on the world grid so it
            // can never fall between texels, and on its own advance so it cannot outrun itself. The
            // gain divides by it, so the frames a parcel spends under the sweeping edge SUM to the
            // envelope rather than to a multiple of it.
            float edgeWidth = FoamBuffer.EdgeWidth(slope * speed, dt);
            float gain = FoamBuffer.EdgeGain(depositRate, r0, wMax, _spreadKelvinFraction, dt,
                                             edgeWidth);
            if (gain <= 0f) { share = 0f; return default; }

            const float span = FoamDispersal.Nodes - 1;
            SampleTrail(head, reach * (1f / span), out Vector2 n1, out _);
            SampleTrail(head, reach * (2f / span), out Vector2 n2, out _);
            SampleTrail(head, reach * (3f / span), out Vector2 n3, out _);
            SampleTrail(head, reach * (4f / span), out Vector2 n4, out _);
            SampleTrail(head, reach, out Vector2 n5, out float tailAge);

            float tailMark = FoamBuffer.AgeMark(tailAge, FoamInjectionRegistry.AgeHalfLifeSeconds);
            return new FoamDispersal(head, n1, n2, n3, n4, n5, r0, wMax, gain, edgeWidth, tailMark);
        }

        private void Join()
        {
            if (_registered) return;
            FoamInjectionRegistry.Register(this);
            _registered = true;
        }

        private void Leave()
        {
            if (!_registered) return;
            FoamInjectionRegistry.Unregister(this);
            _registered = false;
            _hasPending = false;
        }
    }
}
