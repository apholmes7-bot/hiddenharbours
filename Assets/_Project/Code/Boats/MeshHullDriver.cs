using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>The MonoBehaviour that steers a mesh hull (ADR 0022 phase 4).</b> Lives on the boat's
    /// PHYSICS ROOT — exactly where <see cref="DirectionalBoatSprite"/> lives on the sprite path,
    /// and for the same reason: heading is a fact about the root, and everything that follows the
    /// bow rides the root. Each LateUpdate it:
    ///
    /// <list type="number">
    ///   <item><b>Stomps the visual child's world rotation</b> back to screen-identity (plus the
    ///   additive <see cref="VisualTiltDegrees"/> hook), exactly as the sprite path stomps its
    ///   child. The child must not inherit the body's physics yaw: the hull's on-screen turn is the
    ///   MESH rotating under the rig projection, not the picture rotating in screen space —
    ///   inheriting both would double the turn.</item>
    ///   <item><b>Maps the true compass heading onto rig dir units</b>
    ///   (<see cref="HullMeshMath.HeadingToDirUnits"/>, with the def's MEASURED azimuth convention)
    ///   and writes it to the Art-side renderer through the Core seam. Continuous — no facing grid,
    ///   no snap. This is what the spike's verdict bought.</item>
    ///   <item><b>Poses the rock</b> from the wave phase <see cref="BoatWaveMotion"/> wrote this
    ///   frame (execution order: wave −120 → this −110 → the renderer applies at its default 0),
    ///   using the rig's own rock amplitudes (<see cref="HullMeshMath.RockPose"/>). −1 / calm =
    ///   the level pose, exactly like a sprite hull's RockFrame −1.</item>
    ///   <item><b>Adds her TRIM</b> (owner 2026-09-21 — the bow answers her speed): the target the
    ///   boat's physics publishes through <see cref="IHullTrimSource"/>, eased by the hull's own lag
    ///   (<see cref="HullTrimMath.Step"/>) over the game clock, then added to the pitch channel. It
    ///   is composed HERE and only here, so the renderer, <see cref="AppliedPitchDegrees"/> (her deck
    ///   riders) and <see cref="TryGetWakePose"/> (her wake) all read the one drawn attitude. No
    ///   source, the policy off, or a hull that authors none = exactly the pre-trim pose.</item>
    /// </list>
    ///
    /// <para><b>Wired by <see cref="BoatHullSkinner"/> only</b> — a scene-serialised instance with
    /// no renderer idles harmlessly (the skinner reconfigures on every hull apply). Allocation-free
    /// per frame (rule 7): every write below is a float into a dirty-checked property.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-110)]   // after BoatWaveMotion (−120), before the overlay readers (−100)
    public class MeshHullDriver : MonoBehaviour, IHullWakePoseSource
    {
        private IHullMeshRenderer _renderer;
        private Transform _visual;
        private float _zeroHeadingDegrees;
        private bool _azimuthCounterClockwise;
        private float _elevationDegrees = 90f;
        // Row 29: the two rig facts every family of foam reads, carried here beside the elevation for
        // exactly the same reason — they are art facts of the baked hull and the wake knows her only
        // through the presenter seam.
        private float _wakeSternOffsetMeters;
        private float _watertightHalfBeamMeters;
        private float _rockRollDegrees, _rockPitchDegrees, _rockHeavePixels;
        private float _appliedRollDegrees, _appliedPitchDegrees, _appliedHeaveMeters;
        private int _pxPerMetre = 32;
        private float _designWaterlineMeters;

        private bool _rockLevel = true;
        private float _rockPhaseDegrees;
        private int _rockFrame = MountedRockPoseMath.LevelRockFrame;
        private float _displacedHeaveMeters;
        private float _drawnRideMeters;

        // The storm rock channel (ADR 0018 B2.5) — written by BoatWaveMotion through the presenter
        // seam each tick. Scale 1 / extras 0 is the exact neutral (the calm byte-identity), so a
        // driver nothing writes to draws precisely the pre-B2.5 pose.
        private float _stormAmplitudeScale = 1f;
        private float _stormExtraRollDegrees;
        private float _stormExtraPitchDegrees;

        // Her trim (owner 2026-09-21): where the target comes from (the BoatController on this same
        // root, found at Configure), the eased angle actually drawn, the rest serial last seen, and
        // the game-clock time of the last ease. Visual state only — recomputed, never saved.
        private IHullTrimSource _trimSource;
        private float _trimDegrees;
        private int _trimRestSerial;
        private bool _trimHasLastTime;
        private double _trimLastTimeSeconds;

        /// <summary>
        /// <b>The trim this driver added to her pitch on its last <see cref="Drive"/></b>, degrees
        /// (+ = bow up) — already inside <see cref="AppliedPitchDegrees"/>, reported apart so a test or
        /// a debug read can see the trim share alone. 0 with no <see cref="IHullTrimSource"/> on the
        /// root, before the first drive after a <see cref="Configure"/>, and on the drive after a rest.
        /// </summary>
        public float TrimDegrees => _trimDegrees;

        /// <summary>The visual child the renderer draws under (kept screen-identity). Null = idle.</summary>
        public Transform Visual => _visual;

        /// <summary>The bake elevation of the def being presented (an art fact — see
        /// <see cref="IBoatHullPresenter.BakeElevationDegrees"/>). 90 (plan view) until configured.</summary>
        public float ElevationDegrees => _elevationDegrees;

        /// <summary>Row 29: the rig-lofted transom offset (<see cref="HullMeshDef.WakeSternOffsetMeters"/>),
        /// published to the wake through <see cref="IBoatHullPresenter.WakeSternOffsetMeters"/>. 0 until
        /// configured, which reads as "no rig".</summary>
        public float WakeSternOffsetMeters => _wakeSternOffsetMeters;

        /// <summary>Row 29: the hull's watertight half-beam — the one width law's source.</summary>
        public float WatertightHalfBeamMeters => _watertightHalfBeamMeters;

        /// <summary>Additive visual tilt (degrees, +CCW about z) — the
        /// <see cref="IBoatHullPresenter.VisualTiltDegrees"/> hook. Composed as EXTRA ROLL on the
        /// mesh pose: a mesh hull's screen image comes off the facet pass, so rotating the overlay
        /// quad would only clip its window; the rig's roll channel is the honest analogue (equal to
        /// a screen-z tilt at the cardinal headings, and a true deck lean everywhere). Nothing
        /// writes it on the shipped path (the wave motion uses the continuous rock channel).</summary>
        public float VisualTiltDegrees { get; set; }

        /// <summary>The rock frame analogue (−1 = level) — see
        /// <see cref="IBoatHullPresenter.RockFrame"/>. A non-negative frame is mapped onto the rock
        /// cycle as <c>phase = frame·45°</c> (the 8-frame baseline every rig's rockMotion is stated
        /// in) so a legacy frame writer still rocks the hull; the canonical continuous input is
        /// <see cref="SetRockPhaseDegrees"/>.</summary>
        public int RockFrame
        {
            get => _rockFrame;
            set
            {
                _rockFrame = value;
                if (value < 0) _rockLevel = true;
                else { _rockLevel = false; _rockPhaseDegrees = value * 45f; }
            }
        }

        /// <summary>Pose the rock from a reconstructed wave phase (crest = 90°) — the continuous
        /// channel <see cref="BoatWaveMotion"/> drives (<see cref="IBoatHullPresenter.SetRockPhaseDegrees"/>).</summary>
        public void SetRockPhaseDegrees(float phaseDegrees)
        {
            _rockLevel = false;
            _rockPhaseDegrees = phaseDegrees;
        }

        /// <summary>The DESIGN WATERLINE (metres above the keel) of the def being presented — how far
        /// up her planking the sea stands at rest (<see cref="HullMeshDef.RestingDraftMeters"/>).
        /// <b>Not the sink</b>: <see cref="HullSettleMath.AppliedSinkMeters"/> turns it into one, and
        /// the two differ by the iso projection's gain — 0.9056 at the fleet's 40° bake (ADR 0033;
        /// 1.1457 before it).</summary>
        public float DesignWaterlineMeters => _designWaterlineMeters;

        /// <summary>
        /// The metre-scale displaced-sea ride under this hull (ADR 0023 phase 3 step 2 — the
        /// shared heave), written by <see cref="BoatWaveMotion"/> each tick through the presenter
        /// seam (<see cref="IBoatHullPresenter.SetDisplacedHeaveMeters"/>). Composed into the
        /// renderer's heave-pixels channel by <see cref="Drive"/> ONLY while
        /// <see cref="DisplacedSea.IsActive"/> — so the screen lift and the calibrated waterline z
        /// ride together, and the flat-water pose stays byte-identical (the A/B contract).
        /// </summary>
        public void SetDisplacedHeaveMeters(float heaveMeters) => _displacedHeaveMeters = heaveMeters;

        /// <summary>
        /// <b>The ride this driver ACTUALLY APPLIED on its last <see cref="Drive"/></b>, in world
        /// metres — the sea's lift less this hull's settle sink, gated on
        /// <see cref="DisplacedSea.IsActive"/>, i.e. exactly <c>RidePixels / PxPerMetre</c>. The
        /// presenter seam's <see cref="IBoatHullPresenter.DrawnRideMeters"/>, and the number anything
        /// standing on this deck must move with (owner, 2026-08-11).
        ///
        /// <para>Reported by the DRIVER rather than by <see cref="BoatWaveMotion"/> because the driver
        /// is the applier: it owns the sink and it owns the gate, so a mesh hull with no wave motion
        /// wired at all still reports the waterline she is genuinely drawn at instead of claiming to
        /// be level. Same reason the sink lives here in the first place.</para>
        ///
        /// <para>Stored rather than recomputed on read, and stored ONLY here: it is the very float
        /// that went into the renderer, so the two cannot drift.</para>
        /// </summary>
        public float DrawnRideMeters => _drawnRideMeters;

        public bool TryGetWakePose(out HullWakePose pose)
        {
            pose = default;
            if (!isActiveAndEnabled || _renderer == null || _visual == null) return false;
            // The rig's measured transom at its authored design waterline, projected using
            // the attitude actually handed to the renderer. Never use the visual child's up:
            // Drive deliberately holds that child at screen identity.
            Vector2 projected = MountedRockPoseMath.Project(
                new Vector3(0f, -_wakeSternOffsetMeters, _designWaterlineMeters),
                CurrentDirUnits * Mathf.PI / 4f, _appliedRollDegrees * Mathf.Deg2Rad,
                _appliedPitchDegrees * Mathf.Deg2Rad, _elevationDegrees * Mathf.Deg2Rad);
            Vector2 drawn = (Vector2)_visual.position + projected + Vector2.up * _appliedHeaveMeters;
            pose = new HullWakePose(drawn, transform.up, _visual.position.y - transform.position.y);
            return true;
        }

        /// <summary>
        /// ⭐ <b>THE ATTITUDE THIS HULL IS ACTUALLY DRAWN AT</b> — the roll and pitch handed to the
        /// renderer this frame, degrees, and the heave folded into the same channel, metres.
        ///
        /// <para>Reported for the same reason and under the same law as
        /// <see cref="DrawnRideMeters"/>: anything that must stay ON this deck needs the numbers the
        /// PICTURE moved by, and exactly one thing knows them — whoever moved the picture. The rock
        /// amplitudes here are the def's scaled by the storm, plus the slope-decomposed extras; a
        /// rider re-deriving them from its own serialized amplitudes agrees with the hull only by
        /// coincidence, and measured on the cape it does not: 6.1 px at her transom in a FLAT CALM.</para>
        ///
        /// <para>⚠ Roll and pitch are the RIG's, not the screen's: a deck point becomes a screen
        /// offset only through <c>MountedRockPoseMath.Project</c>, which is where the lever arm lives
        /// — the term that makes the same attitude move the foredeck and the transom by different
        /// amounts in different directions.</para>
        /// </summary>
        public float AppliedRollDegrees => _appliedRollDegrees;

        /// <inheritdoc cref="AppliedRollDegrees"/>
        public float AppliedPitchDegrees => _appliedPitchDegrees;

        /// <inheritdoc cref="AppliedRollDegrees"/>
        public float AppliedHeaveMeters => _appliedHeaveMeters;

        /// <summary>
        /// The storm rock channel (ADR 0018 B2.5 —
        /// <see cref="IBoatHullPresenter.SetStormRock"/>): <paramref name="amplitudeScale"/>
        /// multiplies the def's canned rock amplitudes (roll, pitch AND the canned heave — the whole
        /// tuned cycle grows coherently), and the extras are real additional attitude degrees
        /// composed onto the posed rock. (1, 0, 0) — and everything a skinner
        /// <see cref="Configure"/> resets to — is the exact pre-B2.5 pose.
        /// </summary>
        public void SetStormRock(float amplitudeScale, float extraRollDegrees, float extraPitchDegrees)
        {
            _stormAmplitudeScale = amplitudeScale;
            _stormExtraRollDegrees = extraRollDegrees;
            _stormExtraPitchDegrees = extraPitchDegrees;
        }

        /// <summary>
        /// Somebody is standing on this deck at <paramref name="rigLocalMeters"/> — handed straight
        /// to the drawer, which is the only thing that can act on it (only the facet pass holds the
        /// hull's depth). See <see cref="IBoatHullPresenter.SetDeckOccupant"/> for what it buys.
        /// Written every tick by the deck rider; inert with no mesh renderer installed.
        /// </summary>
        public void SetDeckOccupant(Vector3 rigLocalMeters, bool active)
        {
            if (_renderer != null) _renderer.SetDeckOccupant(rigLocalMeters, active);
        }

        /// <summary>The id an occludable sprite discards against to be hidden by this hull; 0 when
        /// nothing is being split (no occupant set, or no mesh renderer at all).</summary>
        public float DeckOccluderId => _renderer != null ? _renderer.DeckOccluderId : 0f;

        /// <summary>The hull's deck-occupant slots — where a rider, a trap stack or a piece of gear
        /// claims its own depth. Never null: with no mesh renderer installed there is nothing that
        /// can split an image, and the refusing null object says so without a branch here.</summary>
        public HiddenHarbours.Core.IDeckOccupantSlots DeckOccupants
            => _renderer != null ? _renderer.DeckOccupants : HiddenHarbours.Core.NoDeckOccupantSlots.Instance;

        /// <summary>The rig dir units currently being presented — the live turntable angle the
        /// anchors project through. Derived from the transform, so it is correct before the first
        /// LateUpdate, same as <see cref="DirectionalBoatSprite.CurrentFacingIndex"/>.</summary>
        public float CurrentDirUnits =>
            HullMeshMath.HeadingToDirUnits(
                DirectionalBoatSprite.HeadingDegreesFromBow(transform.up),
                _zeroHeadingDegrees, _azimuthCounterClockwise);

        /// <summary>
        /// Wire the driver — the skinner's path. <paramref name="renderer"/> is the Art-side
        /// renderer installed through <see cref="HullMeshPresentation.Service"/>;
        /// <paramref name="def"/> supplies the per-artwork pose facts;
        /// <paramref name="zeroHeadingDegrees"/> comes off the <see cref="BoatVisualDef"/> like every
        /// other art fact. Passing nulls parks the driver (a hull swap away from mesh).
        /// </summary>
        public void Configure(Transform visual, IHullMeshRenderer renderer, HullMeshDef def,
                              float zeroHeadingDegrees)
        {
            _visual = visual;
            _renderer = renderer;
            _zeroHeadingDegrees = zeroHeadingDegrees;
            if (def != null)
            {
                _azimuthCounterClockwise = def.AzimuthCounterClockwise;
                _elevationDegrees = def.ElevationDeg;
                _wakeSternOffsetMeters = def.WakeSternOffsetMeters;
                _watertightHalfBeamMeters = def.WatertightHalfBeamMeters;
                _rockRollDegrees = def.RockRollDegrees;
                _rockPitchDegrees = def.RockPitchDegrees;
                _rockHeavePixels = def.RockHeavePixels;
                _pxPerMetre = Mathf.Max(1, def.PxPerMetre);
                _designWaterlineMeters = Mathf.Max(0f, def.RestingDraftMeters);
            }
            _rockLevel = true;
            _rockFrame = MountedRockPoseMath.LevelRockFrame;
            VisualTiltDegrees = 0f;
            _displacedHeaveMeters = 0f;
            _drawnRideMeters = 0f;   // a re-skinned hull draws no ride until she is next driven
            _stormAmplitudeScale = 1f;
            _stormExtraRollDegrees = 0f;
            _stormExtraPitchDegrees = 0f;
            // A re-skinned hull starts level and re-finds her trim source (the skinner's apply is a
            // hull swap, never a per-frame call).
            _trimSource = GetComponent<IHullTrimSource>();
            _trimDegrees = 0f;
            _trimHasLastTime = false;
        }

        private void LateUpdate() => Drive();

        /// <summary>
        /// Ease her drawn trim toward the target her physics published, over the game clock (the
        /// <see cref="BoatWaveMotion"/> precedent: a paused clock is dt 0 and her bow holds with the
        /// sea; a clock stepped backwards is a negative dt, which holds for one drive). The first
        /// drive after <see cref="Configure"/>, and the first after the source's rest serial moves (a
        /// stop, a teleport, a hull swap, a load), put her level AT ONCE and start the clock there.
        /// Allocation-free: floats and one interface read.
        /// </summary>
        private float StepTrim()
        {
            if (_trimSource == null) return 0f;
            int serial = _trimSource.TrimRestSerial;
            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : Time.timeAsDouble;
            if (!_trimHasLastTime || serial != _trimRestSerial)
            {
                _trimRestSerial = serial;
                _trimHasLastTime = true;
                _trimLastTimeSeconds = now;
                _trimDegrees = 0f;
                return 0f;
            }
            float dt = (float)(now - _trimLastTimeSeconds);
            _trimLastTimeSeconds = now;
            _trimDegrees = HullTrimMath.Step(_trimDegrees, _trimSource.TrimTargetDegrees, dt,
                                             _trimSource.TrimResponseSeconds);
            return _trimDegrees;
        }

        /// <summary>One pose push — the LateUpdate body, callable directly so EditMode tests (where
        /// the player loop does not run) can drive the exact production path.</summary>
        public void Drive()
        {
            if (_renderer == null || _visual == null) return;

            // (1) The stomp: cancel the body's physics yaw on the visual child so the mesh's own
            // rig-projection rotation is the ONLY turn on screen, then compose the additive tilt
            // hook's screen-z component… which for a mesh is expressed as roll (see the property
            // doc), so the child itself sits at exact screen identity.
            _visual.rotation = Quaternion.identity;

            // (2) Continuous heading, mapped through the measured convention.
            _renderer.HeadingDirUnits = CurrentDirUnits;

            // (3) The rock pose (level when calm), plus the tilt hook as extra roll. The storm
            // channel (ADR 0018 B2.5) scales the def's canned amplitudes — the whole tuned cycle
            // grows with the sea instead of drawing a gale at the chop's fixed attitude — and its
            // extras add the REAL slope-decomposed attitude on top. Scale 1 / extras 0 (the neutral
            // every Configure resets to, and the calm band's exact value) is the pre-B2.5 pose.
            float roll = 0f, pitch = 0f, heave = 0f;
            if (!_rockLevel)
                HullMeshMath.RockPose(_rockPhaseDegrees,
                                      _rockRollDegrees * _stormAmplitudeScale,
                                      _rockPitchDegrees * _stormAmplitudeScale,
                                      _rockHeavePixels * _stormAmplitudeScale,
                                      out roll, out pitch, out heave);

            // (4) The SHARED HEAVE (ADR 0023 phase 3 step 2): while the displaced sea is live,
            // the hull rides it — the metre-scale displaced lift BoatWaveMotion sampled under the
            // hull this frame, less the sink that settles the keel-origin rig at her design
            // waterline. Composed into the SAME heave-pixels channel as the rig's own rock
            // heave, so the renderer's screen lift AND its calibrated iso z (HullDepthBias's
            // heave term) move together by construction — the waterline stays truthful for free.
            // Displaced OFF ⇒ the term is exactly 0 and this line is byte-inert (the A/B
            // contract extends to boats). The gate is the Core seam, not the stored ride, so a
            // becalmed or motion-less hull still sits AT its waterline while the sea is on.
            // The RIDE is reported separately as well as folded into the total: it is a world
            // translation of the whole boat, not an in-cell animation like the rig's rock, so the
            // drawer has to carry the hull's compositing window with it (see IHullMeshRenderer.
            // RidePixels). Same number, told twice — HeavePixels keeps its exact meaning.
            //
            // ⚠️ THE SINK IS NOT THE WATERLINE (owner playtest 2026-08-07, "generally they should
            // level out at the boats water line"). This line used to subtract the def's waterline
            // RAW, and the shared z-buffer then drew the sea climbing a PROJECTION GAIN of planking
            // for every metre of sink — so the whole fleet floated at a multiple of what its own
            // data said. It is a pure MEAN-level error: a constant, not a wobble, so no amount of
            // heave averages it out, and every test stayed green because they all pinned this line
            // against itself. (The gain is sin·(cos+sin) = 0.9056 at the fleet's 40° bake since
            // ADR 0033 re-derived it out of the same z-test law; it was 1.1457 before.)
            // HullSettleMath inverts the projection off the def's own ElevationDeg, so the number an
            // owner types is the number the sea draws.
            float ride = 0f;
            float rideMeters = 0f;
            if (DisplacedSea.IsActive)
            {
                float sink = HullSettleMath.AppliedSinkMeters(_designWaterlineMeters, _elevationDegrees);
                rideMeters = _displacedHeaveMeters - sink;
                ride = rideMeters * _pxPerMetre;
                heave += ride;
            }
            // Published for her PASSENGERS, from the one place that knows it (see DrawnRideMeters).
            // Written unconditionally, so the sea going off puts a rider back down rather than
            // leaving them held at the last crest.
            _drawnRideMeters = rideMeters;

            _appliedRollDegrees = roll + VisualTiltDegrees + _stormExtraRollDegrees;
            _appliedPitchDegrees = pitch + _stormExtraPitchDegrees;
            // (5) Her TRIM, on the same pitch channel (see the class doc). Added only when non-zero, so
            // a boat that is not trimming draws the pre-trim float bit for bit.
            float trim = StepTrim();
            if (trim != 0f) _appliedPitchDegrees += trim;
            _appliedHeaveMeters = heave / Mathf.Max(1e-4f, _pxPerMetre);

            _renderer.RollDegrees = _appliedRollDegrees;
            _renderer.PitchDegrees = _appliedPitchDegrees;
            _renderer.HeavePixels = heave;
            _renderer.RidePixels = ride;
        }
    }

    /// <summary>
    /// <b>The discoverable end of the presenter seam.</b> <see cref="IBoatHullPresenter"/> is a
    /// POCO, but half its consumers (the deck-walk clamp, the deck containers, the wake) bind to a
    /// boat they only know as a GameObject — they need a component to find. The skinner writes the
    /// current presenter here on every hull apply (and clears it on remove), so a consumer's resolve
    /// is one GetComponent instead of a concrete <see cref="DirectionalBoatSprite"/> reach — which
    /// is the phase-4 repointing ADR 0022 phase 1 deferred.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoatHullPresenterHost : MonoBehaviour
    {
        /// <summary>The presenter for the hull currently worn; null when unskinned.</summary>
        public IBoatHullPresenter Presenter { get; internal set; }

        /// <summary>
        /// Resolve the presenter for a boat root: the host's, when the skinner has written one;
        /// otherwise a wrap of a found <see cref="DirectionalBoatSprite"/> (a scene-serialised rig
        /// the skinner has not touched yet); otherwise null — which consumers treat as "smooth
        /// hull", exactly as they treated a missing DirectionalBoatSprite before.
        /// </summary>
        public static IBoatHullPresenter Resolve(GameObject boatRoot)
        {
            if (boatRoot == null) return null;
            var host = boatRoot.GetComponent<BoatHullPresenterHost>();
            if (host != null && host.Presenter != null) return host.Presenter;
            var directional = boatRoot.GetComponentInChildren<DirectionalBoatSprite>(true);
            return directional != null ? new SpriteHullPresenter(directional) : null;
        }
    }
}
