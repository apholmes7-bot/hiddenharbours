using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>An idle day pose and a stowed night pose; the crane has no controls.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class NmcWharfCraneVisual : MonoBehaviour
    {
        [SerializeField] Sprite _day, _night;
        SpriteRenderer _renderer;
        float _next;
        void OnEnable() { _renderer = GetComponent<SpriteRenderer>(); Apply(); }
        void Update() { if (Time.unscaledTime >= _next) Apply(); }
        void Apply()
        {
            _next = Time.unscaledTime + 1f;
            float hour = GameServices.Clock != null ? GameServices.Clock.HourOfDay : 14f;
            Sprite wanted = hour >= 5f && hour <= 20f ? _day : _night;
            if (_renderer != null && wanted != null && _renderer.sprite != wanted) _renderer.sprite = wanted;
        }
    }
}
