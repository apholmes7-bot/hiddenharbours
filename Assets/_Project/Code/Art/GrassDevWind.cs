using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// DEV/DEMO ONLY — feeds a gentle, slowly-VEERING test wind into the grass shader's global <c>_WindWorld</c>
    /// so the demo patch (Hidden Harbours ▸ Build Grass Test) sways and the lean WANDERS even though the bare
    /// demo scene has no environment sim. It only runs while <see cref="GameServices.Environment"/> is null; the
    /// moment the real sim is present (St Peters / Bootstrap), <see cref="GrassWindBridge"/> takes over and this
    /// stands down, so the two never fight over the global. Not for shipping scenes — the builder adds it only
    /// when no sim is wired.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrassDevWind : MonoBehaviour
    {
        private static readonly int IdWindWorld = Shader.PropertyToID("_WindWorld");

        [Tooltip("Test wind strength as the shader sees it (0..1, same scale GrassWindBridge publishes).")]
        [Range(0f, 1f)] [SerializeField] private float _strength = 0.5f;

        [Tooltip("How fast the test wind direction veers (radians/sec). Small = a lazy wander.")]
        [SerializeField] private float _veerSpeed = 0.15f;

        [Tooltip("How much the strength breathes up and down (0 = steady).")]
        [Range(0f, 1f)] [SerializeField] private float _gustBreath = 0.4f;

        private float _angle = 0.6f;

        private void Update()
        {
            // Stand down the instant a real sim exists — GrassWindBridge owns _WindWorld then.
            if (GameServices.Environment != null) return;

            _angle += _veerSpeed * Time.deltaTime;
            float breath = 1f - _gustBreath * 0.5f * (1f - Mathf.Cos(Time.time * 0.7f));
            float s = Mathf.Clamp01(_strength * breath);
            var dir = new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle));
            Shader.SetGlobalVector(IdWindWorld, new Vector4(dir.x * s, dir.y * s, 0f, 0f));
        }
    }
}
