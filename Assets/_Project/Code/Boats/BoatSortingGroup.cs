using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Each boat sorts as one object, by where she lies</b> (ADR 0059 step 3). The boat root carries a
    /// <see cref="SortingGroup"/> whose order is <see cref="HullSorting.OrderFor(Vector2, IHullSortingInput)"/>
    /// at her waterline pivot: the decor band's Y-sort there, one rung behind, unless a situational rule on her
    /// root (the St Peters pier) decides otherwise. Against other boats, piers, rocks, grass, the player ashore
    /// and the free effects (spray, splashes, buoys: Core <c>EffectSorting</c>) she is one picture at one order.
    ///
    /// <para><b>Everything aboard keeps its order inside the group</b>: hull 1 (<see cref="BoatVisualDef.SortingOrder"/>),
    /// oars and outboard relative to it, the interior room, deck gear and containers, the telltale, the skipper,
    /// and the player while he is parented aboard, with his rod line, held props and lamp. Those orders are now
    /// local: they say what draws over what on this deck, and nothing about the scenery. Boarding, stepping
    /// across to another hull and climbing out are reparenting, so a figure joins or leaves a group with no
    /// extra code. What must clear all decor wherever she lies (the mooring rope, the anchor rode, the trap-haul
    /// rope) escapes through <see cref="HullSorting.EscapeTheBoat"/>.</para>
    ///
    /// <para><b>The one writer of her order</b> (ADR 0059 decision 4). Installed by <see cref="BoatHullSkinner"/>
    /// on both the sprite and the mesh path, so the player's boats, the ambient fleet, NPC pot boats, moored
    /// boats and the beached wreck all take it from here.</para>
    ///
    /// <para><b>Cost</b> (rule 7): one rounding and a compare per boat per <c>LateUpdate</c>, and a write only when
    /// the order changes, which is once per quarter metre of northing or of tide. No allocation. The pivot is the
    /// root, which the swell does not move (it lifts only her visual child), so she never flickers against a rock.</para>
    ///
    /// <para><b>The tide moves her sort point; the waves do not.</b> The tide carries her picture up or down the
    /// screen from her root (Core <see cref="IHullTideRide.ScreenRiseNow"/>), so she sorts at the root plus that
    /// rise (<see cref="HullSorting.SortPoint"/>): where her picture's waterline is drawn, against the scenery it
    /// is drawn beside. The situational rule is asked at the same point. The ride is read through the Core seam,
    /// found once per wiring; a hull with no ride sorts at her root as before. A ride that arrives later tells
    /// this writer to look again (<see cref="LookAgain"/>).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoatSortingGroup : MonoBehaviour
    {
        private SortingGroup _group;
        private IHullSortingInput _input;
        private IHullTideRide _ride;
        private Object _rideComponent;   // the same ride as a Unity object, so a destroyed one compares null
        private bool _inputResolved;
        private int _order;
        private bool _written;

        /// <summary>The order her group sorts at, as last written.</summary>
        public int SortingOrder => _group != null ? _group.sortingOrder : _order;

        /// <summary>Her root sorting group (null before the first install).</summary>
        public SortingGroup Group => _group;

        /// <summary>
        /// Give <paramref name="root"/> her sorting group and this writer (idempotent: a re-skin keeps both),
        /// and sort her once now so she never draws a frame at a stale order. A sorting group already on the
        /// root (an authored scene's) is adopted, not doubled.
        /// </summary>
        public static BoatSortingGroup Install(GameObject root)
        {
            if (root == null) return null;
            var writer = root.GetComponent<BoatSortingGroup>();
            if (writer == null) writer = root.AddComponent<BoatSortingGroup>();
            writer._inputResolved = false;   // a rule added since the last install is heard
            writer.Sort();
            return writer;
        }

        private void OnEnable()
        {
            _inputResolved = false;
            Sort();
        }

        private void LateUpdate() => Sort();

        /// <summary>Forget the cached rule and tide ride and sort again now: something on her root changed
        /// (a <see cref="IHullTideRide"/> was added after she was installed).</summary>
        public void LookAgain()
        {
            _inputResolved = false;
            Sort();
        }

        /// <summary>How far up-screen the tide holds her picture right now (0 with no ride, or a destroyed one).</summary>
        public float TideScreenRise
        {
            get
            {
                if (!_inputResolved) Resolve();
                return _rideComponent != null ? _ride.ScreenRiseNow() : 0f;
            }
        }

        private void Resolve()
        {
            _input = GetComponent<IHullSortingInput>();
            _ride = GetComponent<IHullTideRide>();
            _rideComponent = _ride as Object;
            _inputResolved = true;
        }

        /// <summary>Write her order from where she lies now. Public so a test or a teleport can settle it at once.</summary>
        public void Sort()
        {
            if (_group == null)
            {
                _group = GetComponent<SortingGroup>();
                if (_group == null) _group = gameObject.AddComponent<SortingGroup>();
            }
            if (!_inputResolved) Resolve();

            Vector3 root = transform.position;
            float rise = _rideComponent != null ? _ride.ScreenRiseNow() : 0f;
            int order = HullSorting.OrderFor(HullSorting.SortPoint(new Vector2(root.x, root.y), rise), _input);
            if (_written && order == _order && _group.sortingOrder == order) return;
            _group.sortingOrder = order;
            _order = order;
            _written = true;
        }
    }
}
