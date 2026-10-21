using UnityEngine;

namespace HiddenHarbours.Art
{
    [DisallowMultipleComponent]
    public sealed class FoamTransportContact : MonoBehaviour
    {
        [SerializeField] private FoamTransportContactDef _definition;
        [SerializeField, Tooltip("Offset of the authored interval above chart datum; NOT the projected sprite Z.")]
        private float _levelOffset;
        public void Configure(FoamTransportContactDef definition, float levelOffset = 0)
        { _definition = definition; _levelOffset = levelOffset; }
        private void OnEnable() => FoamTransportContacts.Register(this);
        private void OnDisable() => FoamTransportContacts.Unregister(this);

        internal bool TrySnapshot(out FoamTransportContacts.Shape shape)
        {
            shape = default;
            if (_definition == null || string.IsNullOrWhiteSpace(_definition.Id)) return false;
            Vector3 p = transform.TransformPoint(_definition.Centre);
            Vector3 x = transform.TransformVector(Vector3.right), y = transform.TransformVector(Vector3.up);
            Vector2 xx = new Vector2(x.x, x.y), yy = new Vector2(y.x, y.y);
            float sx = xx.magnitude, sy = yy.magnitude;
            // A projected/sheared transform is not an oriented rectangle. Refuse rather than miss its solid.
            if (sx <= 0 || sy <= 0 || !FoamTransport.Finite(p) ||
                !FoamTransport.Finite(xx) || !FoamTransport.Finite(yy) ||
                Mathf.Abs(Vector2.Dot(xx / sx, yy / sy)) > 1e-4f) return false;
            bool circle = _definition.Shape == FoamTransportContactDef.Outline.Circle;
            if (!circle && _definition.Shape != FoamTransportContactDef.Outline.Box) return false;
            Vector2 half = circle
                ? Vector2.one * (_definition.HalfSize.x * Mathf.Max(sx, sy))
                : new Vector2(_definition.HalfSize.x * sx, _definition.HalfSize.y * sy);
            shape = new FoamTransportContacts.Shape(new Vector2(p.x, p.y), xx / sx, half,
                _definition.MinLevel + _levelOffset, _definition.MaxLevel + _levelOffset,
                circle, FoamTransportContacts.StableKey(_definition.Id));
            return shape.Valid;
        }
    }
}
