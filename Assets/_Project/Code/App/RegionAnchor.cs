using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Core;

namespace HiddenHarbours.App
{
    /// <summary>
    /// ONE WAY IN to a region: a name, and where the rig lands when the player arrives by it.
    ///
    /// <para>Nine Mile Creek's mainland is the first region you can enter two different ways — sail into
    /// the wharf, or walk in over the tidal bar — and the two land 400 m apart. A <c>RegionPassage</c> in
    /// the region you LEAVE names the way in by <see cref="Key"/>; the destination's
    /// <see cref="RegionAnchor"/> resolves that name against its own table here. A name rather than a
    /// reference because the two live in different scenes and the destination may not be loaded yet.</para>
    ///
    /// <para><b>Both transforms are OPTIONAL and fall back INDEPENDENTLY</b> to the region's own
    /// <see cref="RegionAnchor.ArrivalPoint"/> / <see cref="RegionAnchor.DisembarkPoint"/>. That is what
    /// makes the walk-in case one field rather than two: the bar landing sets only
    /// <see cref="DisembarkPoint"/>, so the player steps ashore on the bar while the boat still parks at
    /// the wharf where it was left. A water entrance that wanted its own berth would set only
    /// <see cref="ArrivalPoint"/>.</para>
    ///
    /// <para><b>Namespace-level, deliberately not nested</b> — the same CS0426 lesson
    /// <c>CoastClass</c> and <c>CoastRunSector</c> carry.</para>
    /// </summary>
    [System.Serializable]
    public struct NamedArrival
    {
        [Tooltip("The name a RegionPassage uses to ask for this arrival (e.g. 'bar'). Case-insensitive; " +
                 "must be non-empty to be reachable.")]
        public string Key;
        [Tooltip("Where the BOAT parks when the player arrives this way. Leave empty to use the region's " +
                 "own arrival point.")]
        public Transform ArrivalPoint;
        [Tooltip("Where the ON-FOOT player is placed when the player arrives this way. Leave empty to " +
                 "use the region's own disembark point.")]
        public Transform DisembarkPoint;

        public NamedArrival(string key, Transform arrivalPoint, Transform disembarkPoint)
        {
            Key = key;
            ArrivalPoint = arrivalPoint;
            DisembarkPoint = disembarkPoint;
        }
    }

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

        [Tooltip("The region's OTHER ways in, if it has any — one entry per passage that lands somewhere " +
                 "other than the default above. EMPTY is the normal case and is exactly the behaviour " +
                 "every region had before this table existed.")]
        [SerializeField] private NamedArrival[] _arrivals = new NamedArrival[0];

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

        /// <summary>The region's named ways in (empty for a region with a single arrival).</summary>
        public NamedArrival[] Arrivals => _arrivals;

        // ---- the BERTH the region's own scene authored for the player's boat (2026-09-08) ----------

        /// <summary>
        /// ⭐ <b>Where this region's own scene laid the player's boat</b> — remembered the first time the
        /// region is entered without having sailed in, and used by every later arrival so she comes back
        /// to her mooring rather than to the standoff a passage would park her on.
        ///
        /// <para><b>Why it is REMEMBERED rather than authored as a field.</b> The boat in a region scene
        /// is not a marker the region owns — she carries <c>PersistentObject</c>, so the very first
        /// <c>Awake</c> promotes her out of the scene into <c>DontDestroyOnLoad</c> and she never comes
        /// back with it. There is no twin left behind to read on a later visit, and no
        /// <c>MooredBoat</c> marker either: the pose exists in the scene ASSET and for exactly one moment
        /// in the running game. This catches that moment. Nothing is authored twice, no scene changes,
        /// and a region that is first entered BY SEA (Nine Mile Creek, West Water) never records one and
        /// falls through to the arrival point exactly as it always has.</para>
        ///
        /// <para>Held on the ANCHOR rather than in a table on the coordinator because the anchor already
        /// IS the per-region object, and it outlives a visit: a region hop deactivates a scene's roots,
        /// it does not unload them.</para>
        /// </summary>
        public bool HasAuthoredBoatBerth { get; private set; }

        /// <summary>Where she lies at that berth (world). Meaningless unless
        /// <see cref="HasAuthoredBoatBerth"/>.</summary>
        public Vector3 AuthoredBoatBerthPosition { get; private set; }

        /// <summary>The heading she lies on there — the whole pose, because a berth is a position AND a
        /// heading and leaving the second to the identity is what laid a 4.5 m boat athwart this very
        /// fairway on 2026-09-02.</summary>
        public Quaternion AuthoredBoatBerthRotation { get; private set; } = Quaternion.identity;

        /// <summary>
        /// Record this region's authored berth for the player's boat — <b>the pose the region's own scene
        /// SERIALIZED, not the pose the boat happens to be standing in</b>.
        ///
        /// <para><b>⚠ The distinction is the whole rule, and getting it wrong shipped a defect on
        /// 2026-09-08.</b> The first draft of this remembered "wherever she was the first time we saw
        /// her". In a fixture — and in any boot order where the boat exists before her transform is
        /// applied — that is the ORIGIN, so the region dutifully recorded (0,0,0) as her berth and every
        /// later arrival parked her there. Four PlayMode fixtures caught it, all with the same tell: she
        /// arrives at (0,0,0), 131 m from the mark. "Where she was first seen" is not "where the region
        /// authored her", and no amount of guarding against the origin makes it so — a boat legitimately
        /// authored at (0,0,0) would then be refused, and the rule would still be a guess.</para>
        ///
        /// <para>So the source of truth is <see cref="PersistentObject"/>'s record, taken in its own
        /// <c>Awake</c> the instant before promotion, of <b>which scene serialized it and where</b>.
        /// A boat a fixture merely spawned carries no such record and none is remembered — which is
        /// exactly the fallback those four fixtures encode.</para>
        ///
        /// <para>Idempotent by design — <b>only the FIRST successful call takes</b>, so a later arrival
        /// (which has already moved her) can never overwrite the berth with the standoff it just parked
        /// her on.</para>
        /// </summary>
        /// <param name="boat">The persistent boat.</param>
        /// <param name="regionSceneName">The scene this anchor's region lives in. The berth is recorded
        /// only when the boat was authored in THAT scene — a boat another region serialized, or none did,
        /// is not this region's mooring.</param>
        public void RememberBoatBerth(Transform boat, string regionSceneName)
        {
            if (HasAuthoredBoatBerth || boat == null || string.IsNullOrEmpty(regionSceneName)) return;

            var persistent = boat.GetComponent<PersistentObject>();
            if (persistent == null || !persistent.HasAuthoredPose) return;
            if (!string.Equals(persistent.AuthoredInScene, regionSceneName,
                               System.StringComparison.Ordinal)) return;

            HasAuthoredBoatBerth = true;
            AuthoredBoatBerthPosition = persistent.AuthoredPosition;
            AuthoredBoatBerthRotation = persistent.AuthoredRotation;
        }

        // ---- per-passage arrivals -----------------------------------------------------------

        /// <summary>
        /// Find the named arrival <paramref name="key"/> asks for. PURE — no scene, no component state —
        /// so the fallback rule is EditMode-assertable on its own, which is the whole reason it is a
        /// static rather than a loop inside the accessors below.
        ///
        /// <para>A null/empty key, an empty table and an unmatched key all report FALSE, and every one of
        /// them means the same thing to the caller: use the region's own default. Matching is
        /// case-insensitive and ignores entries with a blank key (an authored-but-unfinished row must not
        /// silently swallow a blank lookup).</para>
        /// </summary>
        public static bool TryFindArrival(NamedArrival[] arrivals, string key, out NamedArrival found)
        {
            found = default;
            if (arrivals == null || string.IsNullOrEmpty(key)) return false;
            for (int i = 0; i < arrivals.Length; i++)
            {
                if (string.IsNullOrEmpty(arrivals[i].Key)) continue;
                if (!string.Equals(arrivals[i].Key, key, System.StringComparison.OrdinalIgnoreCase)) continue;
                found = arrivals[i];
                return true;
            }
            return false;
        }

        /// <summary>True if this region authors a way in under that name.</summary>
        public bool HasArrival(string key) => TryFindArrival(_arrivals, key, out _);

        /// <summary>Where the BOAT parks arriving by <paramref name="key"/> — the named arrival's own
        /// point if it sets one, else this region's <see cref="ArrivalPoint"/>.</summary>
        public Transform ArrivalPointFor(string key) =>
            TryFindArrival(_arrivals, key, out NamedArrival a) && a.ArrivalPoint != null
                ? a.ArrivalPoint
                : ArrivalPoint;

        /// <summary>Where the ON-FOOT player lands arriving by <paramref name="key"/> — the named
        /// arrival's own point if it sets one, else this region's <see cref="DisembarkPoint"/>.
        ///
        /// <para>⚠ This is where you ARRIVE, which is not the same place as where you step off the boat.
        /// The dock's own step-off spot stays <see cref="DisembarkPoint"/> for the whole visit — see the
        /// note in <c>RegionTravelCoordinator.ApplyArrival</c>.</para></summary>
        public Transform DisembarkPointFor(string key) =>
            TryFindArrival(_arrivals, key, out NamedArrival a) && a.DisembarkPoint != null
                ? a.DisembarkPoint
                : DisembarkPoint;

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

        /// <summary>Publish this region's OTHER ways in (the region builder's push). Null or empty leaves
        /// the region with a single arrival, which is what it had before this table existed.</summary>
        public void ConfigureArrivals(params NamedArrival[] arrivals) =>
            _arrivals = arrivals ?? new NamedArrival[0];

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
