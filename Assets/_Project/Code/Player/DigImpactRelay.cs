using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The dirt lands when the blade does.</b> The dig resolves its clam on the press and publishes
    /// <see cref="JuiceMoment.DigStrike"/> there; the body clip starts on that cue, but the shovel is still
    /// in the air for the stab frame. This host hears the press and re-publishes the same cue as
    /// <see cref="JuiceMoment.DigImpact"/> after the dig timing's
    /// <see cref="ActionTimingDef.ImpactDelaySeconds"/> (on UNSCALED time, the clip's own clock), so the
    /// sand chunks and the strike sound land on the frame the blade enters the ground.
    ///
    /// <para><b>Presentation only.</b> The clam, the hole, the effort and the save were all settled on the
    /// press; nothing here reads or writes them. <b>Never late on a stale state:</b> a pending impact is
    /// dropped when the player stops being on foot (<see cref="ControlModeChanged"/>), when a game is
    /// loaded (<see cref="GameLoaded"/>), when a new press supersedes it (the clip restarts, so the old
    /// blade never lands), and when this host is disabled. One pending impact at most; no allocation per
    /// frame (the idle cost is one compare).</para>
    /// </summary>
    public sealed class DigImpactRelay : MonoBehaviour
    {
        [Tooltip("The dig's timing (its ImpactDelaySeconds). Empty = loaded from Resources/" +
                 LiftFeedback.DigTimingResource + ".")]
        [SerializeField] private ActionTimingDef _digTiming;

        private bool _delayResolved;
        private float _delay;
        private bool _armed;
        private float _elapsed;
        private JuiceMomentCue _pending;

        private static bool _installed;

        /// <summary>An impact is waiting for the blade (tests).</summary>
        public bool Armed => _armed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("DigImpactRelay") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<DigImpactRelay>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<JuiceMomentCue>(OnMoment);
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<JuiceMomentCue>(OnMoment);
            EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
            Cancel();
        }

        /// <summary>Wire a timing directly (tests / an editor builder). Null = the serialized/Resources lookup.</summary>
        public void ConfigureTiming(ActionTimingDef def)
        {
            _digTiming = def;
            _delayResolved = false;
        }

        private float Delay
        {
            get
            {
                if (_delayResolved) return _delay;
                _delayResolved = true;
                ActionTimingDef def = _digTiming != null ? _digTiming : Resources.Load<ActionTimingDef>(LiftFeedback.DigTimingResource);
                _delay = def != null ? Mathf.Max(0f, def.ImpactDelaySeconds) : 0f;
                return _delay;
            }
        }

        /// <summary>Public so tests drive the same path the bus does. Only the dig press arms an impact.</summary>
        public void OnMoment(JuiceMomentCue e)
        {
            if (e.Kind != JuiceMoment.DigStrike) return;
            // A press while an impact is pending supersedes it: the clip restarts on this press, so the
            // earlier blade never reaches the ground and its dirt must not appear.
            _pending = new JuiceMomentCue(JuiceMoment.DigImpact, e.X, e.Y, e.Strength);
            _elapsed = 0f;
            _armed = true;
            if (Delay <= 0f) Fire();
        }

        /// <summary>The dig is interrupted: a pending impact is dropped, never fired late.</summary>
        public void Cancel() => _armed = false;

        /// <summary>Boarding, taking a helm or driving off ends the dig. Public so tests drive the bus path.</summary>
        public void OnControlModeChanged(ControlModeChanged e) => Cancel();
        /// <summary>A load replaces the world the press happened in. Public so tests drive the bus path.</summary>
        public void OnGameLoaded(GameLoaded e) => Cancel();

        private void Update()
        {
            if (!_armed) return;   // the idle cost: one compare
            TickFrame(Time.unscaledDeltaTime);
        }

        /// <summary>One frame toward the blade on the caller's UNSCALED delta. Public for tests.</summary>
        public void TickFrame(float unscaledDt)
        {
            if (!_armed) return;
            _elapsed += Mathf.Max(0f, unscaledDt);
            if (_elapsed >= Delay) Fire();
        }

        private void Fire()
        {
            _armed = false;
            EventBus.Publish(_pending);
        }
    }
}
