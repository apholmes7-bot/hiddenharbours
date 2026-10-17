using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>A static offer to the flock. Registers only on enable/disable; it draws nothing.</summary>
    public sealed class GullPerch : MonoBehaviour, IGullPerch
    {
        [SerializeField] string _id = "";
        [SerializeField] Vector2 _groundPoint;
        [SerializeField] float _sortY;
        Vector2 _screenPoint;

        public string Id => _id;
        public Vector2 GroundPoint => _groundPoint;
        public Vector2 ScreenPoint => _screenPoint;
        public float SortY => _sortY;

        void OnEnable()
        {
            _screenPoint = transform.position;
            GullPerches.Register(this);
        }
        void OnDisable() => GullPerches.Unregister(this);
    }
}
