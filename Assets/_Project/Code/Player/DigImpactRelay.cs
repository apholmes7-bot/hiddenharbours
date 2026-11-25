using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// Reads the dig's blade as it was drawn: the ground point straight under the shovel's tip, the tip's
    /// height above it in the picture, and where the digger stands. Implemented by <see cref="CarryHands"/>
    /// (the shovel mesh is its); a test stands in a fake.
    /// </summary>
    public interface IDigBladeReader
    {
        /// <summary>False while the shovel is not a mesh aimed by the dig clip (sprite body, carried, slung).</summary>
        bool TryReadDigBlade(out Vector2 ground, out float lift, out Vector2 feet);
    }

    /// <summary>
    /// <b>The dirt lands when the blade does, and leaves from the blade.</b> The dig resolves its clam on
    /// the press and publishes <see cref="JuiceMoment.DigStrike"/> there; the body clip starts on that cue,
    /// but the shovel is still in the air for the stab frame. This host hears the press and re-publishes it
    /// as <see cref="JuiceMoment.DigImpact"/> after the dig timing's
    /// <see cref="ActionTimingDef.ImpactDelaySeconds"/> (on UNSCALED time, the clip's own clock), so the
    /// sand chunks and the strike sound land on the frame the blade enters the ground; then, after
    /// <c>GameConfig.Juice.DigTossDelaySeconds</c> more, it publishes <see cref="JuiceMoment.DigToss"/> for
    /// the lift and toss.
    ///
    /// <para><b>From the blade.</b> Both cues are read off the drawn shovel when the mesh body digs
    /// (<see cref="IDigBladeReader"/>): the impact lands where the blade tip meets the ground, and the toss
    /// leaves the tip where it is at the lift, thrown outward from the digger through the blade (the rig's
    /// lift swings the blade out to his side, so that is the side he throws to). On the sprite body there is
    /// no blade to read and both cues keep the press's point, as before.</para>
    ///
    /// <para><b>Presentation only.</b> The clam, the hole, the effort and the save were all settled on the
    /// press; nothing here reads or writes them. <b>Never late on a stale state:</b> a pending impact or toss
    /// is dropped when the player stops being on foot (<see cref="ControlModeChanged"/>), when a game is
    /// loaded (<see cref="GameLoaded"/>), when a new press supersedes it (the clip restarts, so the old
    /// blade never lands or lifts), and when this host is disabled. One pending impact and one pending toss
    /// at most; no allocation per frame (the idle cost is one compare).</para>
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

        private bool _tossArmed;
        private float _tossElapsed;
        private float _tossStrength;
        private Vector2 _tossFallback;     // the impact's point: the toss's origin when no blade can be read
        private Vector2 _holeForTurn;      // the press's point: the side he faces when the blade cannot say

        private float? _tossDelayOverride; // tests pin it; production reads GameServices.Config.Juice
        private IDigBladeReader _readerOverride;
        private bool _readerOverridden;

        private static bool _installed;

        /// <summary>An impact is waiting for the blade (tests).</summary>
        public bool Armed => _armed;

        /// <summary>A toss is waiting for the lift (tests).</summary>
        public bool TossArmed => _tossArmed;

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

        /// <summary>Pin the toss delay (tests). Null = <c>GameConfig.Juice.DigTossDelaySeconds</c>.</summary>
        public void ConfigureTossDelay(float? seconds) => _tossDelayOverride = seconds;

        /// <summary>Read the blade from <paramref name="reader"/> (tests; null = no blade at all). Call
        /// <see cref="UseLiveBladeReader"/> to go back to the player's hands.</summary>
        public void ConfigureBladeReader(IDigBladeReader reader)
        {
            _readerOverride = reader;
            _readerOverridden = true;
        }

        /// <summary>Read the blade off the player's hands again (<see cref="GameServices.Hands"/>).</summary>
        public void UseLiveBladeReader()
        {
            _readerOverride = null;
            _readerOverridden = false;
        }

        private IDigBladeReader Reader => _readerOverridden ? _readerOverride : GameServices.Hands as IDigBladeReader;

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

        private float TossDelay
        {
            get
            {
                if (_tossDelayOverride.HasValue) return Mathf.Max(0f, _tossDelayOverride.Value);
                JuiceSettings j = GameServices.Config != null ? GameServices.Config.Juice : JuiceSettings.Default;
                return Mathf.Max(0f, j.DigTossDelaySeconds);
            }
        }

        /// <summary>Public so tests drive the same path the bus does. Only the dig press arms an impact.</summary>
        public void OnMoment(JuiceMomentCue e)
        {
            if (e.Kind != JuiceMoment.DigStrike) return;
            // A press while an impact or a toss is pending supersedes it: the clip restarts on this press, so
            // the earlier blade never reaches the ground or lifts, and its dirt must not appear.
            _tossArmed = false;
            _pending = new JuiceMomentCue(JuiceMoment.DigImpact, e.X, e.Y, e.Strength);
            _holeForTurn = e.At;
            _elapsed = 0f;
            _armed = true;
            if (Delay <= 0f) FireImpact();
        }

        /// <summary>The dig is interrupted: a pending impact and toss are dropped, never fired late.</summary>
        public void Cancel()
        {
            _armed = false;
            _tossArmed = false;
        }

        /// <summary>Boarding, taking a helm or driving off ends the dig. Public so tests drive the bus path.</summary>
        public void OnControlModeChanged(ControlModeChanged e) => Cancel();
        /// <summary>A load replaces the world the press happened in. Public so tests drive the bus path.</summary>
        public void OnGameLoaded(GameLoaded e) => Cancel();

        private void Update()
        {
            if (!_armed && !_tossArmed) return;   // the idle cost: one compare
            TickFrame(Time.unscaledDeltaTime);
        }

        /// <summary>One frame toward the blade on the caller's UNSCALED delta. Public for tests.</summary>
        public void TickFrame(float unscaledDt)
        {
            float dt = Mathf.Max(0f, unscaledDt);
            if (_armed)
            {
                _elapsed += dt;
                if (_elapsed < Delay) return;
                // The toss counts from the frame the impact lands; the rest of this frame is not carried over.
                FireImpact();
                return;
            }
            if (!_tossArmed) return;
            _tossElapsed += dt;
            if (_tossElapsed >= TossDelay) FireToss();
        }

        private void FireImpact()
        {
            _armed = false;
            JuiceMomentCue cue = _pending;
            IDigBladeReader reader = Reader;
            if (reader != null && reader.TryReadDigBlade(out Vector2 ground, out float lift, out Vector2 feet))
                cue = new JuiceMomentCue(JuiceMoment.DigImpact, ground, _pending.Strength, lift,
                                         DigBladeMath.ThrowDirection(feet, ground, _holeForTurn));
            _tossFallback = cue.At;
            _tossStrength = cue.Strength;
            _tossElapsed = 0f;
            _tossArmed = true;
            EventBus.Publish(cue);
            if (TossDelay <= 0f) FireToss();
        }

        private void FireToss()
        {
            _tossArmed = false;
            IDigBladeReader reader = Reader;
            JuiceMomentCue cue = reader != null && reader.TryReadDigBlade(out Vector2 ground, out float lift, out Vector2 feet)
                ? new JuiceMomentCue(JuiceMoment.DigToss, ground, _tossStrength, lift,
                                     DigBladeMath.ThrowDirection(feet, ground, _holeForTurn))
                : new JuiceMomentCue(JuiceMoment.DigToss, _tossFallback, _tossStrength);
            EventBus.Publish(cue);
        }
    }

    /// <summary>The pure geometry behind the toss: EditMode-tested on its own.</summary>
    public static class DigBladeMath
    {
        /// <summary>Below this many ground metres a vector has no direction worth throwing along.</summary>
        public const float MinDirectionMetres = 0.02f;

        /// <summary>
        /// The way the dirt is thrown, across the GROUND (unit length, or zero): outward from his
        /// <paramref name="feet"/> through the blade's ground point <paramref name="bladeGround"/>. When the
        /// blade is right over his feet that says nothing, so it falls back to his right-hand side as he
        /// faces <paramref name="hole"/> (a right-handed digger throws to his right); zero when neither can
        /// be read. World deltas are un-squashed first (<see cref="IsoGround.ToGround"/>).
        /// </summary>
        public static Vector2 ThrowDirection(Vector2 feet, Vector2 bladeGround, Vector2 hole)
        {
            Vector2 outward = IsoGround.ToGround(bladeGround - feet);
            if (outward.magnitude >= MinDirectionMetres) return outward.normalized;
            Vector2 facing = IsoGround.ToGround(hole - feet);
            if (facing.magnitude < MinDirectionMetres) return Vector2.zero;
            facing.Normalize();
            return new Vector2(facing.y, -facing.x);   // a quarter turn clockwise: his right
        }
    }
}
