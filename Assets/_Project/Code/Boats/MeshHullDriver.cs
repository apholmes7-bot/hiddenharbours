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
    /// </list>
    ///
    /// <para><b>Wired by <see cref="BoatHullSkinner"/> only</b> — a scene-serialised instance with
    /// no renderer idles harmlessly (the skinner reconfigures on every hull apply). Allocation-free
    /// per frame (rule 7): every write below is a float into a dirty-checked property.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-110)]   // after BoatWaveMotion (−120), before the overlay readers (−100)
    public class MeshHullDriver : MonoBehaviour
    {
        private IHullMeshRenderer _renderer;
        private Transform _visual;
        private float _zeroHeadingDegrees;
        private bool _azimuthCounterClockwise;
        private float _elevationDegrees = 90f;
        private float _rockRollDegrees, _rockPitchDegrees, _rockHeavePixels;
        private int _pxPerMetre = 32;
        private float _restingDraftMeters;

        private bool _rockLevel = true;
        private float _rockPhaseDegrees;
        private int _rockFrame = MountedRockPoseMath.LevelRockFrame;
        private float _displacedHeaveMeters;

        /// <summary>The visual child the renderer draws under (kept screen-identity). Null = idle.</summary>
        public Transform Visual => _visual;

        /// <summary>The bake elevation of the def being presented (an art fact — see
        /// <see cref="IBoatHullPresenter.BakeElevationDegrees"/>). 90 (plan view) until configured.</summary>
        public float ElevationDegrees => _elevationDegrees;

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

        /// <summary>The resting draft (metres) of the def being presented — the design-waterline
        /// sink applied while the displaced sea is active (<see cref="HullMeshDef.RestingDraftMeters"/>).</summary>
        public float RestingDraftMeters => _restingDraftMeters;

        /// <summary>
        /// The metre-scale displaced-sea ride under this hull (ADR 0023 phase 3 step 2 — the
        /// shared heave), written by <see cref="BoatWaveMotion"/> each tick through the presenter
        /// seam (<see cref="IBoatHullPresenter.SetDisplacedHeaveMeters"/>). Composed into the
        /// renderer's heave-pixels channel by <see cref="Drive"/> ONLY while
        /// <see cref="DisplacedSea.IsActive"/> — so the screen lift and the calibrated waterline z
        /// ride together, and the flat-water pose stays byte-identical (the A/B contract).
        /// </summary>
        public void SetDisplacedHeaveMeters(float heaveMeters) => _displacedHeaveMeters = heaveMeters;

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
                _rockRollDegrees = def.RockRollDegrees;
                _rockPitchDegrees = def.RockPitchDegrees;
                _rockHeavePixels = def.RockHeavePixels;
                _pxPerMetre = Mathf.Max(1, def.PxPerMetre);
                _restingDraftMeters = Mathf.Max(0f, def.RestingDraftMeters);
            }
            _rockLevel = true;
            _rockFrame = MountedRockPoseMath.LevelRockFrame;
            VisualTiltDegrees = 0f;
            _displacedHeaveMeters = 0f;
        }

        private void LateUpdate() => Drive();

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

            // (3) The rock pose (level when calm), plus the tilt hook as extra roll.
            float roll = 0f, pitch = 0f, heave = 0f;
            if (!_rockLevel)
                HullMeshMath.RockPose(_rockPhaseDegrees, _rockRollDegrees, _rockPitchDegrees,
                                      _rockHeavePixels, out roll, out pitch, out heave);

            // (4) The SHARED HEAVE (ADR 0023 phase 3 step 2): while the displaced sea is live,
            // the hull rides it — the metre-scale displaced lift BoatWaveMotion sampled under the
            // hull this frame, minus the resting draft that sinks the keel-origin rig to its
            // design waterline. Composed into the SAME heave-pixels channel as the rig's own rock
            // heave, so the renderer's screen lift AND its calibrated iso z (HullDepthBias's
            // heave term) move together by construction — the waterline stays truthful for free.
            // Displaced OFF ⇒ the term is exactly 0 and this line is byte-inert (the A/B
            // contract extends to boats). The gate is the Core seam, not the stored ride, so a
            // becalmed or motion-less hull still sits AT its waterline while the sea is on.
            if (DisplacedSea.IsActive)
                heave += (_displacedHeaveMeters - _restingDraftMeters) * _pxPerMetre;

            _renderer.RollDegrees = roll + VisualTiltDegrees;
            _renderer.PitchDegrees = pitch;
            _renderer.HeavePixels = heave;
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
