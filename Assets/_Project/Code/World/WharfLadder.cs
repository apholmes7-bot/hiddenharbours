using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A fixed ladder down a wharf face</b> — the way aboard once the tide has dropped the boat too far
    /// below the planks to step across — published to the boarding gameplay as a Core
    /// <see cref="IBoardingLadder"/>. Drop it on the fitting, give it the deck height it is bolted to and
    /// the way its face looks, and it registers itself on enable: the same self-installing, scene-scoped
    /// pattern as <see cref="ShoreCleat"/> and <see cref="StandablePlatform"/>, so the player module reads
    /// it through Core and never references this class (rule 4).
    ///
    /// <para><b>Its top does NOT move with the tide, and that is the entire mechanic.</b> The ladder is
    /// bolted to a wall at a fixed height above chart datum; the boat at its foot floats. The growing gap
    /// between them as the water falls is what turns a step aboard into a climb — see
    /// <see cref="LadderBoardingMath.VerticalGap"/>. So author <see cref="TopElevationMeters"/> from the
    /// deck the ladder is bolted to (the wharf builders MEASURE it off the terrain, never a literal) and
    /// the tide does the rest.</para>
    ///
    /// <para><b>The geometry is the KIT's, not a designer's.</b> <see cref="RungMetres"/> and
    /// <see cref="StandoffMetres"/> default to the published fitting/rig numbers
    /// (<c>WharfIso.FIT.ladder.rung</c>, <c>ladderMount().standoff</c>) because the climb art was authored
    /// against them: the clip plants soles on rungs that spacing apart, and the pose was drawn with the
    /// body that far off the stringers. They are serialized so a differently-built ladder is data rather
    /// than code (rule 6), not so they can be nudged to taste.</para>
    ///
    /// <para><b>Placement is expected to move; consumers are not.</b> Placed today by the region builders
    /// from the same fittings table that positions the ladder sprite — so the ladder you can see is the
    /// ladder you can climb. When the wharf rig grows real mount symbols, only the PLACEMENT changes.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WharfLadder : MonoBehaviour, IBoardingLadder
    {
        [Header("Identity")]
        [Tooltip("Stable id of this ladder (e.g. 'wharf.nine_mile_creek.ladder_0') — diagnostics only. " +
                 "Never a lookup key: the registry is walked, not hashed.")]
        [SerializeField] private string _id = "ladder.unnamed";

        [Header("Height")]
        [Tooltip("The height of the ladder's TOP in METRES ABOVE CHART DATUM — the same frame the tide " +
                 "model, the terrain and StandablePlatform all speak. Normally the deck elevation of the " +
                 "wall it is bolted to. MEASURE it off that structure rather than picking a number: this " +
                 "is the fixed end of the tide gap, and a guessed one quietly mistunes the water level at " +
                 "which boarding stops being a step.")]
        [SerializeField] private float _topElevationMeters;

        [Tooltip("How far ABOVE this fitting's transform the cap of the climb stands, in screen metres. " +
                 "Leaves the plan point, reach and berth unchanged. Zero keeps older ladders unchanged; " +
                 "a raised cap also draws a runtime ladder span down to the waterline.")]
        [Min(0f)]
        [SerializeField] private float _climbTopScreenOffset;

        [Tooltip("Published WharfIso.FIT.ladder.w: distance between the two rail centres, in metres.")]
        [Min(0.01f)]
        [SerializeField] private float _spanWidthMetres = 0.45f;

        [Tooltip("Real seconds between tide-driven span updates; the ambient slow-tick cadence.")]
        [Min(0.1f)]
        [SerializeField] private float _spanSlowTickSeconds = 0.5f;

        private SpriteRenderer _span;
        private float _nextSpanTick;
        // The unaltered kit tile's rails occupy columns 1 and 7 (nine pixels wide).
        private const float TileRailCentreSpanPixels = 6f;

        [Header("Facing")]
        [Tooltip("Compass heading the ladder's FACE looks along (0 = North, 90 = East, clockwise) — out " +
                 "from the wall, over the water. The climber faces the reciprocal, back to the sea. On a " +
                 "quay whose mooring edge runs east–west with the water to the south, this is 180.")]
        [Range(0f, 360f)]
        [SerializeField] private float _faceHeadingDegrees = 180f;

        [Header("Kit geometry (MEASURED — see the class doc before touching)")]
        [Tooltip("Rung spacing in metres — the wharf kit's WharfIso.FIT.ladder.rung. The tread of the " +
                 "descent stair, and what the clip's foot placement was authored against. Changing this " +
                 "without a re-bake puts the fisher's feet between the rungs.")]
        [Min(0.01f)]
        [SerializeField] private float _rungMetres = LadderBoardingMath.RigRungMetres;

        [Tooltip("How far the climber's pivot sits off the ladder plane in metres — the rig's measured " +
                 "ladderMount().standoff. Seating the sprite on the ladder line instead puts its hands " +
                 "inside the wall.")]
        [Min(0f)]
        [SerializeField] private float _standoffMetres = LadderBoardingMath.RigStandoffMetres;

        /// <inheritdoc/>
        public string Id => _id;

        /// <summary>Where the ladder meets the wall, in world metres — this transform's own position.
        /// Shore furniture does not move, so there is nothing to project and nothing to recompute.</summary>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float TopElevationMeters => _topElevationMeters;

        /// <inheritdoc/>
        public float ClimbTopScreenOffset => _climbTopScreenOffset;

        /// <inheritdoc/>
        public float FaceHeadingDegrees => _faceHeadingDegrees;

        /// <inheritdoc/>
        public float RungMetres => _rungMetres;

        /// <inheritdoc/>
        public float StandoffMetres => _standoffMetres;

        /// <summary>Author the ladder in one call (region builders; the direct-configure convention).
        /// The kit geometry keeps its measured defaults — pass them only for a ladder the kit did not
        /// build.</summary>
        public void Configure(string id, float topElevationMeters, float faceHeadingDegrees)
        {
            _id = id;
            _topElevationMeters = topElevationMeters;
            _faceHeadingDegrees = faceHeadingDegrees;
        }

        /// <inheritdoc cref="Configure(string,float,float)"/>
        public void Configure(string id, float topElevationMeters, float faceHeadingDegrees,
                              float rungMetres, float standoffMetres)
        {
            Configure(id, topElevationMeters, faceHeadingDegrees);
            _rungMetres = rungMetres;
            _standoffMetres = standoffMetres;
        }

        /// <summary>Author the cap separately from the plan fitting, so a rebuild cannot move its berth.</summary>
        public void ConfigureClimbTop(float screenOffset)
        {
            _climbTopScreenOffset = Mathf.Max(0f, screenOffset);
            if (Application.isPlaying && isActiveAndEnabled) RefreshSpan();
        }

        private void OnEnable()
        {
            BoardingLadders.Register(this);
            if (Application.isPlaying) RefreshSpan();
        }

        private void OnDisable()
        {
            BoardingLadders.Unregister(this);
            if (_span != null) _span.enabled = false;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextSpanTick) return;
            RefreshSpan();
        }

        private void RefreshSpan()
        {
            _nextSpanTick = Time.unscaledTime + Mathf.Max(0.1f, _spanSlowTickSeconds);
            if (_climbTopScreenOffset <= 0f)
            {
                if (_span != null) _span.enabled = false;
                return;
            }

            if (_span == null)
            {
                // A 9 x 4 px repeat cut from WharfOverlays_ladder, beginning on a rung.
                // No new painted pixels or rig edit; the first sole pin sits at that rung under the cap.
                Sprite tile = Resources.Load<Sprite>("WharfLadderTile");
                if (tile == null)
                {
                    Debug.LogError("The raised wharf ladder needs Resources/WharfLadderTile.", this);
                    enabled = false;
                    return;
                }
                var picture = new GameObject("Ladder span") { hideFlags = HideFlags.DontSave };
                picture.transform.SetParent(transform, false);
                _span = picture.AddComponent<SpriteRenderer>();
                _span.sprite = tile;
                _span.drawMode = SpriteDrawMode.Tiled;
                _span.tileMode = SpriteTileMode.Continuous;
                // North wall = -3, hull composite = WharfDeckMax (1). The span sits between them.
                _span.sortingOrder = SortingBands.WharfDeckMax - 1;
            }

            IEnvironmentService environment = GameServices.Environment;
            float water = environment != null
                ? environment.WaterLevelAt(GameServices.Clock?.TotalSeconds ?? 0.0) : 0f;
            float height = Mathf.Max(0f, TidalRide.ScreenRise(_topElevationMeters, water));
            // One repeat is one physical rung; project height once, irrespective of tile pixel density.
            float scaleY = Mathf.Max(0.01f, _rungMetres) * IsoGround.HeightScale / _span.sprite.bounds.size.y;
            float scaleX = _spanWidthMetres / (TileRailCentreSpanPixels / _span.sprite.pixelsPerUnit);
            _span.transform.position = transform.position + Vector3.up * _climbTopScreenOffset;
            _span.transform.rotation = Quaternion.identity;
            _span.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            _span.size = new Vector2(_span.sprite.bounds.size.x, height / scaleY);
            _span.enabled = height > 0f;
        }
    }
}
