using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.App
{
    /// <summary>
    /// The CAMERA's share of the rod fight's strain read (owner's ruling 2026-07-23 — the fight has no UI,
    /// so the rod, the line, the sound and the camera carry all of it). As the line loads toward parting,
    /// the view starts to tremble; when she gives, it settles at once. It is the last-ditch tell: it only
    /// wakes near the snap, so a calm fight is a perfectly still camera.
    ///
    /// <para><b>Rides ON the camera, alongside <see cref="CameraFollow"/>, without fighting it.</b> The
    /// follow lerps toward its goal from wherever the transform currently is, so an offset left in place
    /// would leak into that lerp and compound frame after frame. This component therefore takes its offset
    /// back OUT in <c>Update</c> (before the follow runs in its <c>LateUpdate</c>) and re-applies a fresh
    /// one in its own later <c>LateUpdate</c> — the follow only ever sees a clean transform, and the shake
    /// is pure decoration on top.</para>
    ///
    /// <para><b>Position only, never the zoom.</b> The framing is the discrete, pixel-perfect step table
    /// (<see cref="CameraZoomPolicy"/>); a strain "push in" would fight it and break pixel-perfect. The
    /// tremor is a few pixels of translation, which reads on a 32-PPU sprite without touching framing.</para>
    ///
    /// <para>Cross-module through Core only (rule 4): it reads <see cref="FishingStateChanged"/> and knows
    /// nothing about the Fishing module. Allocation-free per frame (rule 7).</para>
    /// </summary>
    [DefaultExecutionOrder(200)]   // after CameraFollow's LateUpdate has placed the camera for this frame
    [DisallowMultipleComponent]
    public sealed class FightStrainCamera : MonoBehaviour
    {
        [Tooltip("Line tension below which the camera is perfectly still. The tremor is the LAST warning " +
                 "before the line parts, not an ambient wobble — keep it high.")]
        [SerializeField, Range(0f, 1f)] private float _startsAt01 = 0.6f;

        [Tooltip("Tremor amplitude (world m) at the very edge of a snap. At 32 pixels-per-unit, 0.06 m is " +
                 "about two pixels — enough to feel, small enough never to spoil the aim.")]
        [SerializeField, Min(0f)] private float _maxShakeMetres = 0.06f;

        [Tooltip("Tremor frequency (Hz) at full strain. Fast and tight, like a line humming — not a rumble.")]
        [SerializeField, Min(0f)] private float _shakeHz = 24f;

        [Tooltip("How fast the tremor eases in and out (per second). High enough that the shake DIES the " +
                 "instant she gives — that drop is itself the tell that you can reel again.")]
        [SerializeField, Min(0.1f)] private float _responsePerSec = 9f;

        private float _strain01;          // the smoothed strain the tremor is driven by
        private Vector3 _applied;         // the offset currently baked into the transform
        private bool _fighting;
        private float _tension01;

        private void OnEnable() => EventBus.Subscribe<FishingStateChanged>(OnFishingState);
        private void OnDisable()
        {
            EventBus.Unsubscribe<FishingStateChanged>(OnFishingState);
            Unapply();
        }

        private void OnFishingState(FishingStateChanged e)
        {
            _fighting = e.State.IsFightPhase;
            _tension01 = e.State.Tension01;
        }

        /// <summary>Take last frame's tremor back out BEFORE <see cref="CameraFollow"/> runs, so the follow
        /// always lerps from the camera's true position and the offset can never compound.</summary>
        private void Update() => Unapply();

        private void LateUpdate()
        {
            // How close to parting are we? Nothing at all below the threshold — a fight that is going well
            // has a rock-steady camera, which is what makes the tremor mean something.
            float target = _fighting && _tension01 > _startsAt01
                ? Mathf.Clamp01((_tension01 - _startsAt01) / Mathf.Max(1e-4f, 1f - _startsAt01))
                : 0f;

            float dt = Time.unscaledDeltaTime;
            _strain01 = Mathf.Lerp(_strain01, target, 1f - Mathf.Exp(-_responsePerSec * dt));
            if (_strain01 < 1e-3f) { _strain01 = 0f; return; }

            // Two detuned axes so it trembles rather than slides along one line.
            float t = Time.unscaledTime * _shakeHz * (2f * Mathf.PI);
            float amp = _maxShakeMetres * _strain01;
            _applied = new Vector3(Mathf.Sin(t) * amp, Mathf.Sin(t * 1.37f + 1.1f) * amp * 0.75f, 0f);
            transform.position += _applied;
        }

        private void Unapply()
        {
            if (_applied == Vector3.zero) return;
            transform.position -= _applied;
            _applied = Vector3.zero;
        }
    }
}
