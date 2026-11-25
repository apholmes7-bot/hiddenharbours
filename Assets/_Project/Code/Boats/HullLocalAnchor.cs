using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Parks a node at a HULL-LOCAL point, in the frame the hull is actually DRAWN in</b> — screen
    /// aligned, and following her round as she turns.
    ///
    /// <para><b>Why this has to exist at all.</b> The boat's root transform carries her physics YAW, so a
    /// plain child of it swings with her heading — which is right for anything bolted to the deck and
    /// wrong for anything drawn from a pre-baked, screen-axis-aligned cell. The hull's own visual child is
    /// screen-aligned, but it is also where her RIDE is applied, so hanging off it would take her heave a
    /// second time. Neither existing parent is correct for a cell that must sit at the hull pivot,
    /// square to the screen, and take only the motion its owner chooses to give it.</para>
    ///
    /// <para><b>What it does, exactly:</b> world rotation identity, and world position =
    /// the boat root's position plus <see cref="RigLocalMetres"/> projected through the rig camera for
    /// the heading currently drawn. With the default zero point that is just "the hull's pivot, square to
    /// the screen" — which is the parent <see cref="BoatInterior"/>'s own class doc asks for.</para>
    ///
    /// <para><b>The projection is the rigs' own</b> (<see cref="MountedRockPoseMath.Project"/>), so a
    /// point on the boat lands where the art puts it rather than where a flat 2D rotation would. The
    /// turntable angle is derived from the DRAWN heading and the hull's MEASURED handedness — never from
    /// a facing index, because a mesh hull has none and the two lineages disagree about which way their
    /// cells run (this hull's exterior compass and her interior sheets genuinely differ; see the S0
    /// spike). Heading is the only thing both of them mean the same way.</para>
    ///
    /// <para><b>⚠ Posed at the LEVEL pose, deliberately — no roll, no pitch, no ride.</b> This places
    /// gameplay points: where a door is, where you must stand to work it. A reach that bobbed with the
    /// swell would make a threshold harder to hit at the crest than in the trough, which is rule 5's
    /// boundary read the other way round — the sea may move the picture, not the rules. Anything that
    /// should visibly ride takes its own pose from the hull's published phase, as
    /// <see cref="BoatInterior"/> does.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)]   // after the hull poses herself (−110), before her readers (−100)
    public sealed class HullLocalAnchor : MonoBehaviour
    {
        [Tooltip("The boat this point belongs to. Empty falls back to this node's own root.")]
        [SerializeField] private Transform _boatRoot;

        [Tooltip("Where on her, in the rig's own metres: +x starboard, +y bow, +z up from the keel — " +
                 "the frame every authored deck polygon, fitting pivot and door threshold already " +
                 "speaks. Zero = the hull's pivot.")]
        [SerializeField] private Vector3 _rigLocalMetres;

        [Tooltip("Tick when this hull's art turns COUNTER-CLOCKWISE with the rig's dir argument — the " +
                 "MEASURED convention (HullMeshDef.AzimuthCounterClockwise / " +
                 "BoatVisualDef.FacingsAreCounterClockwise), never a guess. Getting it wrong mirrors " +
                 "the point end for end across the boat.")]
        [SerializeField] private bool _azimuthCounterClockwise = true;

        [Tooltip("Elevation of the camera this hull's art is presented at (40° for the iso rigs). " +
                 "Seeded from the presenter's own BakeElevationDegrees.")]
        [SerializeField] private float _bakeElevationDegrees = 40f;

        /// <summary>Where on her this node sits, rig metres. See the field tooltip for the frame.</summary>
        public Vector3 RigLocalMetres => _rigLocalMetres;

        private IBoatHullPresenter _hull;
        private BoatHullPresenterHost _host;
        private bool _resolved;

        /// <summary>Wire this anchor from the installer. Applies immediately, so a node is never seen at
        /// the origin for a frame before its first LateUpdate — the same reason
        /// <c>BoatInterior.Configure</c> applies.</summary>
        public void Configure(Transform boatRoot, Vector3 rigLocalMetres,
                              bool azimuthCounterClockwise, float bakeElevationDegrees)
        {
            _boatRoot = boatRoot;
            _rigLocalMetres = rigLocalMetres;
            _azimuthCounterClockwise = azimuthCounterClockwise;
            _bakeElevationDegrees = bakeElevationDegrees;
            _resolved = false;
            _host = null;
            Apply();
        }

        /// <summary>Place the node now, exactly as its next LateUpdate would. For tests and tooling that
        /// change what the hull presents between frames.</summary>
        public void Reapply() => Apply();

        private void OnEnable()
        {
            // A hop may have re-skinned her, and the presenter is a POCO the skinner swaps out.
            _resolved = false;
            _host = null;
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            Transform root = _boatRoot != null ? _boatRoot : transform.root;
            if (root == null) return;

            Resolve(root);

            // Square to the screen, whatever the body below is doing. Written every tick rather than
            // once: the parent's yaw changes continuously, and a world rotation is not inherited as a
            // constant.
            transform.rotation = Quaternion.identity;

            Vector2 offset = Vector2.zero;
            IBoatHullPresenter hull = LiveHull(root);
            if (_rigLocalMetres != Vector3.zero && hull != null)
            {
                // CELL space from the compass, through the measured handedness: dir d depicts heading
                // −45°·d on counter-clockwise art, so the mapping negates. This is the one number the
                // sprite and mesh paths can both answer.
                float heading = hull.DrawnHeadingDegrees();
                float theta = (_azimuthCounterClockwise ? -heading : heading) * Mathf.Deg2Rad;
                offset = MountedRockPoseMath.Project(_rigLocalMetres, theta, 0f, 0f,
                                                     _bakeElevationDegrees * Mathf.Deg2Rad);
            }

            Vector3 at = root.position;
            transform.position = new Vector3(at.x + offset.x, at.y + offset.y, transform.position.z);
        }

        /// <summary>Find the hull presenter once per enable, never on the hot path. Plain
        /// <c>!= null</c>: a destroyed boat is fake-null and must degrade to "no projection" rather than
        /// throw.</summary>
        private void Resolve(Transform root)
        {
            if (_resolved) return;
            _resolved = true;
            _hull = BoatHullPresenterHost.Resolve(root.gameObject);
        }

        /// <summary>
        /// The presenter to read THIS frame: the host's current one whenever the skinner has published
        /// one, else the one resolved at enable. The deck walk's and the deck containers' discipline
        /// (<c>DeckWalkController.LiveHull</c>), for the same reason.
        ///
        /// <para><b>Why a cache alone is stale after a hull swap.</b> <c>OwnedFleet.ApplyHull</c> calls
        /// <see cref="BoatController.SetHull"/> FIRST, which rebuilds the cabin through
        /// <see cref="BoatInteriorInstaller.Rebuild"/>, and every new door and bunk anchor resolves in its
        /// <see cref="Configure"/> while the host still holds the OLD hull's presenter. The skinner
        /// publishes the new one on the very next line, but nothing re-enables the anchors, so a cache
        /// read only at enable would keep the old presenter, whose component the skin swap may have
        /// destroyed (it then answers heading 0 and every door and berth is placed as if she lay due
        /// north) until the next region hop.</para>
        ///
        /// <para>No allocation: one non-allocating component lookup while no host exists, then a
        /// reference read.</para>
        /// </summary>
        private IBoatHullPresenter LiveHull(Transform root)
        {
            if (_host == null) root.TryGetComponent(out _host);
            if (_host != null && _host.Presenter != null) return _host.Presenter;
            return _hull;
        }
    }
}
