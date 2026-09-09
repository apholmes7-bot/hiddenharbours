using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The silent dig, answered</b> (#803, juice charter §4.4). Before this, a clam lifted into her
    /// hand said nothing on screen: <c>CatchLanded</c> was published, the hand drew the clam, and no
    /// presenter turned it into a sentence — <c>FishCaught</c> (which the toast listens to) is
    /// deliberately NOT published on the in-hand path, because the hand is not a hold. So the one
    /// press that succeeded looked exactly like the presses that did not.
    ///
    /// <para>This host hears <c>CatchLanded</c> and publishes exactly ONE <see cref="DevNotice"/> —
    /// <see cref="LiftLine.For"/>: "Lifted out a soft-shell clam — it's in your hand." — on the beat the
    /// dig's <see cref="ActionTimingDef"/> puts the follow-through (after its pre-hold + strike, on
    /// UNSCALED time), so the words land as the shovel does. The notebook's register keeps the same
    /// line from the same cue. <b>Guard:</b> <c>CatchLanded</c> has one on-screen consequence, here;
    /// the hand's clam is the other half of the picture and was already drawn (<c>CarriableCatch</c>
    /// registers the species icon) — the defect was feedback, not the hands.</para>
    /// </summary>
    public sealed class LiftFeedback : MonoBehaviour
    {
        /// <summary>Where the dig's timing lives when no reference is wired (Resources root: <c>Data/Resources</c>).</summary>
        public const string DigTimingResource = "ActionTiming/DigTiming";

        [Tooltip("The dig's anticipation/follow-through timing. Empty = loaded from Resources/" + DigTimingResource + ".")]
        [SerializeField] private ActionTimingDef _digTiming;

        private bool _timingResolved;
        private ActionTiming _timing;
        private string _line;
        private float _wait;
        private float _elapsed;
        private bool _armed;

        private static bool _installed;

        /// <summary>A line is waiting for its beat (tests).</summary>
        public bool Armed => _armed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("LiftFeedback") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<LiftFeedback>();
        }

        private void OnEnable() => EventBus.Subscribe<CatchLanded>(OnLanded);
        private void OnDisable() => EventBus.Unsubscribe<CatchLanded>(OnLanded);

        /// <summary>Wire a timing directly (tests / an editor builder). Null = the serialized/Resources lookup.</summary>
        public void ConfigureTiming(ActionTimingDef def)
        {
            _digTiming = def;
            _timingResolved = false;
        }

        private ActionTiming Timing
        {
            get
            {
                if (_timingResolved) return _timing;
                _timingResolved = true;
                ActionTimingDef def = _digTiming != null ? _digTiming : Resources.Load<ActionTimingDef>(DigTimingResource);
                _timing = def != null ? def.Timing : new ActionTiming(0f, 0f, 0f, 0f);
                return _timing;
            }
        }

        /// <summary>Public so tests drive the same path the bus does.</summary>
        public void OnLanded(CatchLanded e)
        {
            _line = LiftLine.For(e.Item);   // built ONCE per catch, never per frame
            ActionTiming t = Timing;
            _wait = t.PreHoldSeconds + t.StrikeSeconds;
            _elapsed = 0f;
            _armed = true;
            if (_wait <= 0f) Fire();
        }

        private void Update()
        {
            if (!_armed) return;   // the idle cost: one compare
            TickFrame(Time.unscaledDeltaTime);
        }

        /// <summary>One frame toward the beat on the caller's UNSCALED delta. Public for tests.</summary>
        public void TickFrame(float unscaledDt)
        {
            if (!_armed) return;
            _elapsed += Mathf.Max(0f, unscaledDt);
            if (_elapsed >= _wait) Fire();
        }

        private void Fire()
        {
            _armed = false;
            EventBus.Publish(new DevNotice(_line));
        }
    }
}
