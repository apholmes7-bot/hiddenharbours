using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.App
{
    /// <summary>
    /// The binding point a region scene exposes to the persistent core (VS-22 travel). Each region scene
    /// (Coddle Cove, Port Greywick) places ONE of these, naming where the persistent player/boat should
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

        public string RegionId => _regionId;
        public Transform ArrivalPoint => _arrivalPoint != null ? _arrivalPoint : transform;
        public Transform DockZone => _dockZone;
        public Transform DisembarkPoint => _disembarkPoint;

        // ---- live registry (lets the coordinator find the anchor for the active scene) -------

        private static readonly List<RegionAnchor> _live = new();
        public static IReadOnlyList<RegionAnchor> Live => _live;

        private void OnEnable() { if (!_live.Contains(this)) _live.Add(this); }
        private void OnDisable() => _live.Remove(this);

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
    }
}
