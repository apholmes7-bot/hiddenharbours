using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>The delivered wharf's convex fixed decks and sloping slipway panels.</summary>
    [DisallowMultipleComponent]
    public sealed class NmcWharfWalkSurface : MonoBehaviour, IStandableSurface
    {
        [SerializeField] string _id = "";
        [SerializeField] Vector2[] _polygon = System.Array.Empty<Vector2>();
        [SerializeField] Vector2 _slopeFrom, _slopeTo;
        [SerializeField] float _fromElevation, _toElevation;
        public string Id => _id;
        void OnEnable() => StandableSurfaces.Register(this);
        void OnDisable() => StandableSurfaces.Unregister(this);
        public bool TryGetDeckElevation(Vector2 at, out float elevation)
        {
            Vector2 axis = _slopeTo - _slopeFrom;
            float along = axis.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(at - _slopeFrom, axis) / axis.sqrMagnitude) : 0f;
            elevation = Mathf.Lerp(_fromElevation, _toElevation, along);
            if (_polygon == null || _polygon.Length < 3) return false;
            int sign = 0;
            for (int i = 0; i < _polygon.Length; i++)
            {
                Vector2 edge = _polygon[(i + 1) % _polygon.Length] - _polygon[i], delta = at - _polygon[i];
                float cross = edge.x * delta.y - edge.y * delta.x;
                if (Mathf.Abs(cross) <= 1e-5f) continue;
                int next = cross > 0 ? 1 : -1;
                if (sign != 0 && sign != next) return false;
                sign = next;
            }
            return true;
        }
    }
}
