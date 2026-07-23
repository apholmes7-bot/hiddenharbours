using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// PLACEHOLDER fishing input for the greybox: Space OR the left mouse button is the single fishing
    /// action, and the MOUSE aims the flick-cast (Rod Fishing v2 §2.2 — mouse-first, the owner's call).
    /// HOLD to start the wind-back, drag the mouse behind the character, sweep it forward past them and
    /// RELEASE to let the spool loose; after a bite, HOLD to reel and RELEASE to ease — pulse to land the
    /// fish before the line snaps. Feeds the held state + the pointer (world space, under the main
    /// camera) to <see cref="FishingController.Tick"/> every frame. Replace with the real Action binding
    /// via the InputService later (ui-ux, the Haul(hold/release) intent).
    ///
    /// <para><b>GAMEPAD SEAM (deliberately not built).</b> The pad fallback maps the RIGHT STICK to the
    /// gesture: stick pulled back = wind-back, snapped forward = the sweep, trigger release = the spool.
    /// When it lands it slots in HERE — synthesize a pointer offset around the character from the stick
    /// and feed the same <c>(held, pointerWorld, pointerValid)</c> triple; the controller and the maths
    /// need no change (they only ever see samples).</para>
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
        private PotDeckWorkController _deckWork;   // Build-7 deck-work sibling; may be null / appear late
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

        /// <summary>The Build-7 deck-work sibling (spawned on demand by the haul controller when the first
        /// deck-working pot surfaces — so this MUST stay lazy and self-healing, same as <see cref="Haul"/>).
        /// While a pot is aboard being worked, Space belongs to the DECK (pick/band/bait holds): the cast
        /// stands down and, because those are HOLDS, the release latch arms exactly as it does for the
        /// haul — the deck's carried-over hold can never flip into a cast when the pot clears.</summary>
        private PotDeckWorkController DeckWork
            => _deckWork != null ? _deckWork : (_deckWork = GetComponent<PotDeckWorkController>());

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
        /// while a trap haul OR a deck pot being worked (Build 7) owns the Space key. Public + input-free
        /// so the gate itself is EditMode-testable.</summary>
        public bool FishingLive => _onDeck && !InteractionGate.IsBlocked
                                   && !(Haul != null && Haul.IsHauling)
                                   && !(DeckWork != null && DeckWork.HasPotAboard);

        // The camera that maps the mouse into the world (cached; Camera.main is a tag lookup).
        private Camera _cam;

        private void Update()
        {
            bool keyHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            bool mouseHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool pointerValid = TryPointerWorld(out Vector2 pointerWorld);
            TickFishing(Time.deltaTime, keyHeld || mouseHeld, pointerWorld, pointerValid);
        }

        /// <summary>The mouse in world space under the main camera, or false when either is missing
        /// (headless / no camera yet) — the flick then simply can't start, nothing throws.</summary>
        private bool TryPointerWorld(out Vector2 world)
        {
            world = default;
            var mouse = Mouse.current;
            if (mouse == null) return false;
            if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
            if (_cam == null) return false;
            Vector2 screen = mouse.position.ReadValue();
            Vector3 w = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_cam.transform.position.z));
            world = new Vector2(w.x, w.y);
            return true;
        }

        /// <summary>Pointer-less tick (legacy/test path): the gate + latch behave identically, but with no
        /// pointer a fresh wind-back can never start — in-flight phases still advance.</summary>
        public void TickFishing(float dt, bool rawHeld) => TickFishing(dt, rawHeld, default, false);

        /// <summary>Advance the handline FSM by <paramref name="dt"/>, applying the deck/haul gate AND the
        /// hold-haul release latch to the raw action state. When the gate is closed the fishing tick still runs
        /// but with <c>held=false</c> — it can never start a fresh cast (needs a rising edge) yet lets any
        /// cast/fight in flight ease to its cozy resolution rather than strand; an UN-FLOWN wind-back is the
        /// one exception and is aborted outright (<see cref="FishingController.CancelCastGesture"/>) — the
        /// forced release must not fling a half-made gesture. The latch (see the class doc) additionally
        /// swallows a key still held from a just-ended HAUL until it is released, so the haul's carried-over
        /// hold never flips into a cast. Public so a test can drive it without real key input.</summary>
        public void TickFishing(float dt, bool rawHeld, Vector2 pointerWorld, bool pointerValid)
        {
            // Arm the release latch for as long as a haul is (or has just been) live — any haul tick sets it,
            // so however the haul ends the still-held pull key is caught before it can become a cast. Build 7:
            // a pot being worked on the deck arms it the same way (its pick/band/bait actions are HOLDS on
            // the same key; the pot clearing — set, or auto-resolved — must not turn a held key into a cast).
            if ((Haul != null && Haul.IsHauling) || (DeckWork != null && DeckWork.HasPotAboard))
                _requireReleaseBeforeCast = true;

            // Whenever this tick will NOT deliver the raw hold (the gate is closed, or the latch is about
            // to swallow it), a wind-back in progress is ABORTED rather than force-released: the controller
            // reads held=false as "the player let go" and would EVALUATE the half-made gesture into a flung
            // cast. Nothing was in the water yet, so the cozy outcome is simply "you stood back up". Later
            // phases are untouched (CancelCastGesture is a WindBack-only no-op) and keep easing under
            // held=false — the established no-stranded-cast behaviour.
            bool live = FishingLive;
            if (!live || (_requireReleaseBeforeCast && rawHeld)) Fishing.CancelCastGesture();

            // Once armed, require a genuine RELEASE before the key counts as a press again. A still-held key
            // is swallowed (held=false); seeing it up re-arms the cast.
            if (_requireReleaseBeforeCast)
            {
                if (rawHeld) { Fishing.Tick(dt, false, pointerWorld, pointerValid); return; }
                _requireReleaseBeforeCast = false;
            }

            bool held = live && rawHeld;
            Fishing.Tick(dt, held, pointerWorld, pointerValid);
        }
    }
}
