using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Core;

namespace HiddenHarbours.App
{
    /// <summary>
    /// The binding point a region scene exposes to the persistent core (VS-22 travel). Each region scene
    /// (Coddle Cove, Nine Mile Creek) places ONE of these, naming where the persistent player/boat should
    /// appear on arrival (<see cref="ArrivalPoint"/>) and which transforms are this region's boarding zone
    /// and disembark spot (<see cref="DockZone"/> / <see cref="DisembarkPoint"/>). On arrival the
    /// <see cref="RegionTravelCoordinator"/> repositions the rig here and re-points the control switcher's
    /// dock to these, so boarding/disembarking work in whichever region you're standing in.
    ///
    /// Self-registers into a static list so the coordinator can find the anchor in the freshly-active
    /// scene without a scene-wide search. Authored by the region builders; flagged with the travel approach.
    /// </summary>
    public sealed class RegionAnchor : MonoBehaviour
    {
        [Tooltip("Stable region id this anchor belongs to (matches the RegionDef.Id), e.g. region.coddle_cove.")]
        [SerializeField] private string _regionId;
        [Tooltip("Where the persistent boat/player appears on arriving in this region (defaults to this transform).")]
        [SerializeField] private Transform _arrivalPoint;
        [Tooltip("This region's board/dock zone (the control switcher is re-pointed here on arrival).")]
        [SerializeField] private Transform _dockZone;
        [Tooltip("Where the on-foot player is placed when disembarking in this region.")]
        [SerializeField] private Transform _disembarkPoint;

        // This region's authored extent, relayed to consumers that outlive the region scene (the
        // persistent camera's bounds clamp). The values COME FROM RegionDef.WorldCenter /
        // WorldSizeMeters — the region builder copies them here exactly as it copies the same pair
        // into WaterSurface.ConfigurePaintedHeightMap, the flat backdrop and the displaced mesh.
        //
        // ⚠️ Why a copy rather than a RegionDef reference: RegionDef lives in HiddenHarbours.World and
        // App does not reference it (rule 4) — the travel rig deliberately works in scene names and
        // ids. Adding a module reference to save one builder-propagated pair would buy tidiness with
        // a coupling the architecture spent effort avoiding. Builder-propagated is the sanctioned
        // route for this number and every other consumer of the extent already takes it.
        [Tooltip("Centre of this region's world rectangle (m) — RegionDef.WorldCenter, set by the " +
                 "region builder. Do not author here.")]
        [SerializeField] private Vector2 _worldCenter = Vector2.zero;

        [Tooltip("Size of this region's world rectangle (m) — RegionDef.WorldSizeMeters, set by the " +
                 "region builder. ZERO means this region reports no extent, and consumers behave as " +
                 "they did before the bounds rig existed (the camera stays unclamped).")]
        [SerializeField] private Vector2 _worldSizeMeters = Vector2.zero;

        public string RegionId => _regionId;

        /// <summary>This region's authored world rectangle, or a zero-size rect when the builder has
        /// not published one (which reads downstream as "unbounded").</summary>
        public Rect WorldBounds => _worldSizeMeters.x > 0f && _worldSizeMeters.y > 0f
            ? new Rect(_worldCenter - _worldSizeMeters * 0.5f, _worldSizeMeters)
            : default;
        public Transform ArrivalPoint => _arrivalPoint != null ? _arrivalPoint : transform;
        public Transform DockZone => _dockZone;
        public Transform DisembarkPoint => _disembarkPoint;

        // ---- live registry (lets the coordinator find the anchor for the active scene) -------

        private static readonly List<RegionAnchor> _live = new();
        public static IReadOnlyList<RegionAnchor> Live => _live;

        // The anchor also REPORTS its region as the current one (GameServices.CurrentRegionId — the
        // travel-aware region seam gameplay resolves catches against). Region scenes are TOGGLED by the
        // RegionTravelCoordinator (previous roots deactivated BEFORE the next activate), so exactly one
        // anchor is enabled at a time and OnEnable/OnDisable order makes the handoff clean; at BOOT the
        // start scene's own anchor enables and seeds the id with no travel event needed. OnDisable only
        // clears the id if it still owns it, so the next region's report is never stomped.
        private void OnEnable()
        {
            if (!_live.Contains(this)) _live.Add(this);
            if (!string.IsNullOrEmpty(_regionId)) GameServices.CurrentRegionId = _regionId;

            // The region's EXTENT rides the same report, for the same reason: consumers that outlive
            // the region scene (the persistent camera's bounds clamp) cannot carry a rectangle of
            // their own without it being the start region's forever.
            Rect bounds = WorldBounds;
            if (bounds.size != Vector2.zero) GameServices.CurrentRegionBounds = bounds;
        }

        private void OnDisable()
        {
            _live.Remove(this);
            // Only clear what this anchor still OWNS — the next region's report must never be stomped
            // (region roots toggle, and the incoming anchor enables before this one disables in some
            // orders). Same discipline for both values.
            if (GameServices.CurrentRegionId == _regionId) GameServices.CurrentRegionId = null;
            if (GameServices.CurrentRegionBounds == WorldBounds) GameServices.CurrentRegionBounds = default;
        }

        /// <summary>The first live anchor whose GameObject lives in <paramref name="scene"/>, or null.</summary>
        public static RegionAnchor ForScene(Scene scene)
        {
            for (int i = 0; i < _live.Count; i++)
                if (_live[i] != null && _live[i].gameObject.scene == scene) return _live[i];
            return null;
        }

        /// <summary>Wire the anchor in one call (tests / editor builder).</summary>
        public void Configure(string regionId, Transform arrivalPoint, Transform dockZone, Transform disembarkPoint)
        {
            _regionId = regionId;
            _arrivalPoint = arrivalPoint;
            _dockZone = dockZone;
            _disembarkPoint = disembarkPoint;
        }

        /// <summary>
        /// Publish this region's authored extent — pass <c>RegionDef.WorldCenter</c> /
        /// <c>WorldSizeMeters</c> straight through, the same pair the sea sprite, the backdrop, the
        /// height bake and the displaced mesh receive. A zero size means "this region reports no
        /// extent", leaving downstream consumers unbounded.
        ///
        /// <para>Takes effect on the next enable; call it before the anchor enables (the builder does)
        /// or re-publish live for a rebuild.</para>
        /// </summary>
        public void ConfigureExtent(Vector2 worldCenter, Vector2 worldSizeMeters)
        {
            _worldCenter = worldCenter;
            _worldSizeMeters = new Vector2(Mathf.Max(0f, worldSizeMeters.x),
                                           Mathf.Max(0f, worldSizeMeters.y));
            if (isActiveAndEnabled)
            {
                Rect bounds = WorldBounds;
                if (bounds.size != Vector2.zero) GameServices.CurrentRegionBounds = bounds;
            }
        }
    }
}
