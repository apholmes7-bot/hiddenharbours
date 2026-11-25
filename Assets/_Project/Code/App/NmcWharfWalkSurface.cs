using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>The delivered wharf's convex fixed decks and sloping slipway panels.</summary>
    [DisallowMultipleComponent]
    public sealed class NmcWharfWalkSurface : MonoBehaviour, IStandableSurface, IStandableOutline
    {
        [SerializeField] string _id = "";
        [SerializeField] Vector2[] _polygon = System.Array.Empty<Vector2>();
        [SerializeField] Vector2 _slopeFrom, _slopeTo;
        [SerializeField] float _fromElevation, _toElevation;
        public string Id => _id;
        void OnEnable() => StandableSurfaces.Register(this);
        void OnDisable() => StandableSurfaces.Unregister(this);

        float ElevationAt(Vector2 at)
        {
            Vector2 axis = _slopeTo - _slopeFrom;
            float along = axis.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(at - _slopeFrom, axis) / axis.sqrMagnitude) : 0f;
            return Mathf.Lerp(_fromElevation, _toElevation, along);
        }

        /// <summary>The calm mirror's outline: the kit draws each corner its own deck height up the screen.</summary>
        public int GetDrawnOutline(Vector2[] drawn, float[] deckElevation)
        {
            if (_polygon == null || _polygon.Length < 3 || drawn == null || deckElevation == null) return 0;
            int n = Mathf.Min(_polygon.Length, Mathf.Min(drawn.Length, deckElevation.Length));
            for (int i = 0; i < n; i++)
            {
                float e = ElevationAt(_polygon[i]);
                drawn[i] = new Vector2(_polygon[i].x, _polygon[i].y + StandableOutline.Lift(e));
                deckElevation[i] = e;
            }
            return n;
        }

        public bool TryGetDeckElevation(Vector2 at, out float elevation)
        {
            elevation = ElevationAt(at);
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
