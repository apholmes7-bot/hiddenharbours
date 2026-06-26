using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The tiny PLAYER-POSITION bridge for the grass shader. Drop it on whatever moves through the grass (the
    /// player, an NPC, a cart) and it publishes that transform's world position to the GLOBAL shader vector
    /// <c>_PlayerWorld</c> each frame with <see cref="Shader.SetGlobalVector(int, Vector4)"/>. The grass shader
    /// bends each tuft AWAY from <c>_PlayerWorld</c> by <c>(1 - smoothstep(0, radius, dist))</c>, so the grass
    /// parts as you walk through and SPRINGS BACK once you leave.
    ///
    /// <para><b>No per-blade state (rule 5).</b> Recovery is automatic because the shader tracks the LIVE
    /// position every frame — nothing is stored per tuft, nothing is saved. There is intentionally only ONE
    /// <c>_PlayerWorld</c> (a v1 single-bender soft radius that follows you); a lingering "trail you stomped
    /// down" is a deliberate later upgrade. If several benders exist, the last to update each frame wins —
    /// fine for the single-player on-foot case.</para>
    ///
    /// <para><b>Performance (rule 7).</b> One global vector set per frame, no allocation, independent of how many
    /// tufts read it. <b>Seam discipline (rule 4):</b> it only sets a shader global — it references no other
    /// feature module. Visual-only.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrassFootstep : MonoBehaviour
    {
        private static readonly int IdPlayerWorld = Shader.PropertyToID("_PlayerWorld");

        [Tooltip("Optional explicit source to track. Leave empty to track THIS object's transform (the usual " +
                 "case: put this component on the player).")]
        [SerializeField] private Transform _source;

        [Tooltip("Push _PlayerWorld far away in OnDisable so any lingering grass relaxes when the bender leaves " +
                 "(e.g. boarding a boat). Off keeps the last position so the grass holds its parting.")]
        [SerializeField] private bool _resetOnDisable = true;

        private Transform Source => _source != null ? _source : transform;

        private void OnEnable() => Publish(Source.position);

        private void LateUpdate() => Publish(Source.position);

        private void OnDisable()
        {
            // Park the bend point far off-world so no tuft thinks the player is still standing in it.
            if (_resetOnDisable)
                Shader.SetGlobalVector(IdPlayerWorld, FarAway);
        }

        private static void Publish(Vector3 worldPos)
        {
            Shader.SetGlobalVector(IdPlayerWorld, new Vector4(worldPos.x, worldPos.y, 0f, 0f));
        }

        // A position effectively at infinity so the smoothstep footstep falloff reads 0 everywhere.
        private static Vector4 FarAway => new Vector4(1e9f, 1e9f, 0f, 0f);

        // ==== PURE mirror of the shader's footstep falloff (testable headless) ============================

        /// <summary>
        /// The footstep bend factor the shader applies: <c>1 - smoothstep(0, radius, dist)</c> — 1 right at the
        /// player's feet, easing to 0 at and beyond <paramref name="radius"/>. Mirrors the HLSL exactly so the
        /// recovery curve is unit-tested without opening Unity. Monotonic non-increasing in distance; 0 for any
        /// distance at or past the radius (the bend never reaches outside its circle).
        /// </summary>
        public static float FootstepFalloff(float distance, float radius)
        {
            float r = Mathf.Max(radius, 1e-3f);
            float t = Mathf.Clamp01(distance / r);
            float s = t * t * (3f - 2f * t);   // smoothstep(0,1,t)
            return 1f - s;
        }
    }
}
