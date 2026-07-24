using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.App
{
    /// <summary>
    /// The CAMERA's share of the rod fight (owner's ruling 2026-07-23 — the fight has no UI, so the rod,
    /// the line, the sound and the camera carry all of it). Two distinct jobs:
    ///
    /// <para><b>The strain tremor</b> — as the line loads toward parting the view begins to tremble, and
    /// settles the moment she gives. Last-ditch: it only wakes near the snap, so a fight that is going
    /// well has a perfectly still camera, which is what makes the tremble mean something.</para>
    ///
    /// <para><b>The one-shot kicks</b> (owner ask 2026-07-24) — a hard jolt on the three beats worth
    /// feeling: the <b>hook-set</b> (thrown toward her as she bolts), the <b>land</b> (the lift aboard),
    /// and the <b>snap</b> (thrown back as everything lets go). Each fires once, on the phase transition,
    /// and decays in a third of a second — a punch, not a wobble.</para>
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

        [Header("The one-shot kicks (the two moments)")]
        [Tooltip("The HOOK-SET jolt (world m) the instant she's on and bolting — the shock of a fish " +
                 "taking the hook. Fired once per fight, on the strike.")]
        [SerializeField, Min(0f)] private float _hookSetKickMetres = 0.22f;

        [Tooltip("The kick when she comes aboard (world m) — the payoff thump on the land beat.")]
        [SerializeField, Min(0f)] private float _landedKickMetres = 0.14f;

        [Tooltip("The kick when the line parts (world m) — the recoil of everything letting go at once.")]
        [SerializeField, Min(0f)] private float _snappedKickMetres = 0.3f;

        [Tooltip("How long a kick takes to die away (s). Short: a kick is a punch, not a wobble.")]
        [SerializeField, Min(0.05f)] private float _kickDecaySeconds = 0.35f;

        [Tooltip("How fast a kick oscillates (Hz) as it decays.")]
        [SerializeField, Min(0f)] private float _kickHz = 17f;

        private float _strain01;          // the smoothed strain the tremor is driven by
        private Vector3 _applied;         // the offset currently baked into the transform
        private bool _fighting;
        private float _tension01;

        // The live one-shot kick: amplitude (m) and how far through its decay it is.
        private float _kickAmp;
        private float _kickClock;
        private Vector2 _kickDir = Vector2.up;
        private FishingPhase _lastPhase = FishingPhase.Idle;

        private void OnEnable() => EventBus.Subscribe<FishingStateChanged>(OnFishingState);
        private void OnDisable()
        {
            EventBus.Unsubscribe<FishingStateChanged>(OnFishingState);
            Unapply();
        }

        private void OnFishingState(FishingStateChanged e)
        {
            FishingState s = e.State;
            _fighting = s.IsFightPhase;
            _tension01 = s.Tension01;

            // THE TWO MOMENTS (owner ask 2026-07-24) — fired on the TRANSITION, so exactly once each.
            if (s.Phase != _lastPhase)
            {
                bool wasFighting = _lastPhase == FishingPhase.Fighting
                                || _lastPhase == FishingPhase.FightDeep
                                || _lastPhase == FishingPhase.FightSurface
                                || _lastPhase == FishingPhase.Tending;

                if (_fighting && !wasFighting)
                    // She's ON. Kick along the line — the rod is being yanked toward her.
                    Kick(_hookSetKickMetres, new Vector2(s.FishOffsetX, s.FishOffsetY));
                else if (s.Phase == FishingPhase.Landed)
                    Kick(_landedKickMetres, Vector2.up);        // the lift aboard
                else if (s.Phase == FishingPhase.Snapped && wasFighting)
                    // Everything lets go at once — the recoil throws BACK, away from where she was.
                    Kick(_snappedKickMetres, -new Vector2(s.FishOffsetX, s.FishOffsetY));

                _lastPhase = s.Phase;
            }
        }

        /// <summary>Fire a one-shot kick of <paramref name="metres"/> along <paramref name="direction"/>
        /// (zero/NaN falls back to a vertical thump). A new kick overrides a weaker one in flight rather
        /// than summing, so back-to-back beats can never stack into a lurch.</summary>
        private void Kick(float metres, Vector2 direction)
        {
            if (metres <= 0f) return;
            if (_kickAmp > 0f && metres < _kickAmp * KickDecay01()) return;   // don't weaken a live kick

            float len = direction.magnitude;
            _kickDir = (len > 1e-4f && !float.IsNaN(len)) ? direction / len : Vector2.up;
            _kickAmp = metres;
            _kickClock = 0f;
        }

        /// <summary>How much of the live kick is left, 1 → 0 across <see cref="_kickDecaySeconds"/>.</summary>
        private float KickDecay01() => Mathf.Clamp01(1f - _kickClock / Mathf.Max(0.05f, _kickDecaySeconds));

        /// <summary>Take last frame's tremor back out BEFORE <see cref="CameraFollow"/> runs, so the follow
        /// always lerps from the camera's true position and the offset can never compound.</summary>
        private void Update() => Unapply();

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            // How close to parting are we? Nothing at all below the threshold — a fight that is going well
            // has a rock-steady camera, which is what makes the tremor mean something.
            float target = _fighting && _tension01 > _startsAt01
                ? Mathf.Clamp01((_tension01 - _startsAt01) / Mathf.Max(1e-4f, 1f - _startsAt01))
                : 0f;
            _strain01 = Mathf.Lerp(_strain01, target, 1f - Mathf.Exp(-_responsePerSec * dt));
            if (_strain01 < 1e-3f) _strain01 = 0f;

            Vector3 offset = Vector3.zero;

            // The continuous strain tremor: two detuned axes, so it trembles rather than sliding.
            if (_strain01 > 0f)
            {
                float t = Time.unscaledTime * _shakeHz * (2f * Mathf.PI);
                float amp = _maxShakeMetres * _strain01;
                offset += new Vector3(Mathf.Sin(t) * amp, Mathf.Sin(t * 1.37f + 1.1f) * amp * 0.75f, 0f);
            }

            // The one-shot kick: a decaying oscillation ALONG a direction (hook-set, landed, snapped).
            if (_kickAmp > 0f)
            {
                _kickClock += dt;
                float decay = KickDecay01();
                if (decay <= 0f) { _kickAmp = 0f; }
                else
                {
                    float swing = Mathf.Sin(_kickClock * _kickHz * (2f * Mathf.PI)) * _kickAmp * decay * decay;
                    offset += new Vector3(_kickDir.x * swing, _kickDir.y * swing, 0f);
                }
            }

            if (offset == Vector3.zero) return;
            _applied = offset;
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
