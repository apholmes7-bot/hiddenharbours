using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.App
{
    /// <summary>Group the persistent hull behind this pier only while she lies on its north side.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SortingGroup))]
    public sealed class StPetersPierBoatSorting : MonoBehaviour
    {
        [SerializeField] private Rect _pier;
        [SerializeField] private int _frontOrder = 1;
        [SerializeField] private int _behindOrder = -1;
        private SortingGroup _group;

        public void Configure(Rect pier, int frontOrder, int behindOrder)
        {
            _pier = pier; _frontOrder = frontOrder; _behindOrder = behindOrder;
            Apply();
        }
        public static bool Behind(Rect pier, Vector2 at) =>
            at.x >= pier.xMin && at.x <= pier.xMax && at.y > pier.yMax;
        void OnEnable() => Apply();
        void LateUpdate() => Apply();
        void Apply()
        {
            if (_group == null) _group = GetComponent<SortingGroup>();
            int order = Behind(_pier, transform.position) ? _behindOrder : _frontOrder;
            if (_group != null && _group.sortingOrder != order) _group.sortingOrder = order;
        }
    }
}
