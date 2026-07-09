using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// PLACEHOLDER one-thumb fishing input for the greybox: Space is the single fishing action.
    /// Press it to cast; after a bite, HOLD to reel and RELEASE to ease — pulse to land the fish before
    /// the line snaps. Feeds the held state to <see cref="FishingController.Tick"/> every frame. Replace
    /// with the real touch/Action button via the InputService later (ui-ux, the Haul(hold/release) intent).
    ///
    /// <para><b>Build 5 gating (why this component is no longer dumb).</b> Handline fishing is a DECK
    /// action like the trap loop — you cast from the boat, not while walking the shore or steering at the
    /// helm. So the cast is gated to <see cref="ControlMode.OnDeck"/> exactly the way
    /// <see cref="DevTrapInput"/>/<see cref="TrapHaulController"/> gate their keys (subscribe to
    /// <see cref="ControlModeChanged"/>, live only on deck and not under a modal <see cref="InteractionGate"/>).
    /// AND — because Space is ALSO the trap-haul pull key on the same GameObject — the cast stands down while
    /// a <see cref="TrapHaulController"/> haul is live, so pressing Space during a haul pulls the rope and
    /// never also casts a line (the owner's blocking bug: Space double-bound cast + pull).</para>
    ///
    /// <para><b>The haul is now a HOLD — the release latch (Build 6, the resurrected bug).</b> The trap haul
    /// was redesigned from a tap into a <b>hold</b> (hold Space with the swell), so the player is very likely
    /// STILL HOLDING Space at the instant the haul ends. The cast gate reopens on that same frame, and
    /// <see cref="FishingController"/> starts a cast on the RISING EDGE of <c>held</c> — so the held key would
    /// flip false→true and immediately fling out a handline (exactly the owner's bug, resurrected by the hold).
    /// This component kills it with a <b>latch</b>: any tick during which a haul is live ARMS a
    /// "require-release" flag, and once armed the cast swallows the still-held key until it sees a genuine
    /// RELEASE. So after a haul a new cast needs an actual release-then-press, never the carried-over hold.
    /// (The off-deck / modal gates do NOT arm the latch — only a live haul does — so coming on deck with the
    /// key already down still casts, the established behaviour.)</para>
    ///
    /// <para><b>No stranded casts.</b> When the gate is closed we do NOT stop calling <see cref="FishingController.Tick"/>
    /// — that would freeze a cast/fight already in flight. We keep ticking with <c>held=false</c>, which
    /// (a) can never START a new cast (a fresh cast needs a rising edge, impossible while held is forced
    /// false) and (b) lets any in-flight bite/fight ease to its cozy resolution (a slack line throws the
    /// hook, no penalty) instead of hanging mid-cast.</para>
    /// </summary>
    [RequireComponent(typeof(FishingController))]
    public sealed class DevFishingInput : MonoBehaviour
    {
        private FishingController _fishing;
        private TrapHaulController _haul;   // sibling on the same Dory GO; may be null on a fishing-only rig
        // Handline fishing is a DECK action (owner's Build-5 split): the cast lives only while standing ON
        // DECK — never at the helm (you're steering) and never on foot (ControlModeChanged, via Core).
        private bool _onDeck;
        // The hold-haul release latch (Build 6): true once a live haul has been seen, until the pull key is
        // released. While set, a still-held key is swallowed so the haul's carried-over hold can never flip
        // into a fresh cast the instant the haul ends. Only a live haul arms it (not the deck/modal gates).
        private bool _requireReleaseBeforeCast;

        /// <summary>The handline FSM this drives (required sibling). Resolved lazily + cached so it works
        /// whether Awake has run (play mode) or not (EditMode AddComponent doesn't fire Awake).</summary>
        private FishingController Fishing => _fishing != null ? _fishing : (_fishing = GetComponent<FishingController>());

        /// <summary>The sibling haul controller (may be null on a fishing-only rig). Resolved lazily and
        /// cached: the editor builders add DevFishingInput before the TrapHaulController, so an Awake-time
        /// lookup would miss it — this self-heals whenever the sibling appears (a cheap, alloc-free lookup
        /// on the one boat object until it resolves).</summary>
        private TrapHaulController Haul => _haul != null ? _haul : (_haul = GetComponent<TrapHaulController>());

        private void OnEnable()
        {
            // Fresh components start un-decked; every transition (and the region-arrival re-assert)
            // republishes the mode, which keeps this correct across scene hops.
            _onDeck = false;
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
        }

        private void OnDisable() => EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);

        /// <summary>Public so tests can drive the deck gate through the same path the bus uses.</summary>
        public void OnControlModeChanged(ControlModeChanged e) => _onDeck = e.Mode == ControlMode.OnDeck;

        /// <summary>True while the handline cast is worked — ON DECK, not under a modal dialogue, and not
        /// while a trap haul owns the Space key. Public + input-free so the gate itself is EditMode-testable.</summary>
        public bool FishingLive => _onDeck && !InteractionGate.IsBlocked && !(Haul != null && Haul.IsHauling);

        private void Update()
        {
            bool rawHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            TickFishing(Time.deltaTime, rawHeld);
        }

        /// <summary>Advance the handline FSM by <paramref name="dt"/>, applying the deck/haul gate AND the
        /// hold-haul release latch to the raw Space state. When the gate is closed the fishing tick still runs
        /// but with <c>held=false</c> — it can never start a fresh cast (needs a rising edge) yet lets any
        /// cast/fight in flight ease to its cozy resolution rather than strand. The latch (see the class doc)
        /// additionally swallows a key still held from a just-ended HAUL until it is released, so the haul's
        /// carried-over hold never flips into a cast. Public so a test can drive it without real key input.</summary>
        public void TickFishing(float dt, bool rawHeld)
        {
            // Arm the release latch for as long as a haul is (or has just been) live — any haul tick sets it,
            // so however the haul ends the still-held pull key is caught before it can become a cast.
            if (Haul != null && Haul.IsHauling) _requireReleaseBeforeCast = true;

            // Once armed, require a genuine RELEASE before the key counts as a press again. A still-held key
            // is swallowed (held=false); seeing it up re-arms the cast.
            if (_requireReleaseBeforeCast)
            {
                if (rawHeld) { Fishing.Tick(dt, false); return; }
                _requireReleaseBeforeCast = false;
            }

            bool held = FishingLive && rawHeld;
            Fishing.Tick(dt, held);
        }
    }
}
