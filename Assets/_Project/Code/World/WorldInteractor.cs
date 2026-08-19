using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The on-foot INTERACT driver for world things (VS-21): when the player is near an
    /// <see cref="Interactable"/> (Aunt Ginny, the neighbour, Ned's logbook) <b>and looking at them</b>,
    /// it OFFERS that conversation — and, on the Interact key, starts it in the
    /// <see cref="DialoguePresenter"/>. While a conversation is up it forwards the key to advance the
    /// lines.
    ///
    /// <para><b>It no longer draws anything.</b> The floating "E: Talk to …" label this component used to
    /// build and park over the world was retired by the owner's 2026-08-19 ruling: the whole of the UI is
    /// four surfaces plus one small popup, lower right, naming the interaction on the trigger you are
    /// facing. So the answer goes out as a Core offer (<see cref="InteractOffer"/>) on the
    /// <see cref="InteractOfferSource.Conversation"/> slot and <c>InteractPopup</c> — in the UI lane,
    /// which this module may not name (rule 4) — draws it. <b>This component stays the single computer of
    /// conversational candidacy</b>; the popup and, later, the affordance highlight are subscribers to the
    /// one answer, never second opinions about it.</para>
    ///
    /// <para><b>FACING is a filter, not a tie-break</b> (see <see cref="_facingArcDegrees"/>). Standing
    /// between Aunt Ginny and her freezer, the one you are looking at is the one offered — which is the
    /// whole reason the popup can show a single line without lying. When the player's facing is unknown
    /// (nothing published an actor — see <see cref="InteractActorProbe"/>) the filter passes everything and
    /// this behaves exactly as it did before facing existed: proximity only, failing open.</para>
    ///
    /// Context-aware by PROXIMITY so it never fights the dock's board/disembark (also E): NPCs/logbook
    /// sit up by the cottage, the dock zone is down at the water, and the two ranges don't overlap, so
    /// only one is ever in range. Belt-and-braces, while a dialogue is open the presenter raises the
    /// shared <see cref="InteractionGate"/>, which the <c>ControlSwitcher</c> (Player) honours — so even
    /// standing in the dock zone, E advances the dialogue instead of boarding. Coordinate point flagged
    /// for gameplay-systems in the PR.
    ///
    /// Cross-module clean: it holds only a player <see cref="Transform"/> (no Player-module type) and
    /// talks to the rest of the game through Core (InteractionGate) — same discipline as the HUD.
    ///
    /// <para><b>Who it measures FROM is resolved, not just wired</b> — the builder's own reference where
    /// there is one and Core's <see cref="GameServices.PlayerTransform"/> where there is not (see
    /// <see cref="ResolvePlayer"/>), exactly as <see cref="BuildingInterior"/> resolves its occupant. A
    /// region scene is saved long before the persistent player exists, so an interactor that could only
    /// be wired at build time was an interactor that worked in the start scene and nowhere else.</para>
    /// </summary>
    public sealed class WorldInteractor : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("The on-foot player, AS THIS SCENE'S BUILDER KNEW THEM — proximity is measured from " +
                 "here. OPTIONAL: a region scene has no persistent player to name at build time, so " +
                 "this is either empty or a dev stand-in that the travel path destroys, and the player " +
                 "is then resolved from Core at runtime. See ResolvePlayer.")]
        [SerializeField] private Transform _player;
        [SerializeField] private DialoguePresenter _presenter;
        [Tooltip("Everything the player can walk up to and interact with.")]
        [SerializeField] private Interactable[] _interactables;

        [Header("Tuning")]
        [Tooltip("How close (m) the player must be to an interactable for it to be offered.")]
        [SerializeField] private float _radius = 1.8f;

        [Tooltip("The FULL forward arc (degrees) an interactable must lie within to be offered — the " +
                 "half-angle is half of this, centred on the way the fisher is facing. See " +
                 "DefaultFacingArcDegrees for why the shipped value is 120.")]
        [SerializeField, Range(0f, 360f)] private float _facingArcDegrees = DefaultFacingArcDegrees;

        /// <summary>
        /// The shipped forward arc for a conversation: <b>120° full, so a half-angle of ±60°</b>.
        ///
        /// <para><b>Why not the resolver's 180.</b> <see cref="InteractResolver.DefaultFacingArcDegrees"/>
        /// is deliberately generous — ±90° is the whole half-plane in front of you, which is right for a
        /// fixture you operate by standing at it. A conversation is different: the popup shows ONE line,
        /// and at ±90° two people standing either side of you are both dead ahead, so the line would be
        /// decided by a centimetre of distance rather than by where you are looking. The filter has to be
        /// tight enough to actually pick.</para>
        ///
        /// <para><b>Why not tighter than 120 either.</b> The fisher has FOUR facings, so the quadrants tile
        /// at exactly ±45°. An arc of 90 would leave a candidate sitting on a diagonal on the knife-edge
        /// between two facings, flickering as she turns. ±60° overlaps each quadrant boundary by 15°, which
        /// is a forgiving margin (P5 cozy) that still excludes anything abeam or behind — you cannot talk
        /// to somebody by turning your back on them, and you do not have to be pixel-aligned either.</para>
        ///
        /// <para>Named, serialized and tunable per interactor (rule 6): set it to
        /// <see cref="InteractResolver.DefaultFacingArcDegrees"/> for the old half-plane behaviour, or to
        /// 360 to switch the filter off entirely.</para>
        /// </summary>
        public const float DefaultFacingArcDegrees = 120f;

        // Onboarding flags, backed by the save file (VS-08) so the 'met before' variants persist across reload.
        // The STORE itself is kept beside them because a DialogueDef's conditional beat is gated on an
        // arbitrary authored key, not just the three named onboarding flags — see Begin().
        private IFlagStore _flagStore;
        private OnboardingFlags _flags;
        private Interactable _nearest;

        // The offered line, cached against the speaker it was built for: WorldStrings.OfferLabel
        // interpolates, and the offer is stated every frame there is a candidate, so building it per frame
        // would be one allocation a frame for as long as you stand in front of a neighbour (rule 7).
        private Interactable _labelledFor;
        private string _label;

        // Who is currently talking, and their day — looked up ONCE when the conversation opens rather
        // than per frame (rule 7). Null for an interactable with no routine (a letter, a logbook, an
        // islander who simply waits where they were placed), which is a perfectly good speaker who
        // simply has nothing to stop doing.
        private Interactable _speaker;
        private VillagerRoutine _speakerRoutine;

        private void Awake()
        {
            _flagStore = new SaveFlagStore();                    // VS-08: persisted via the save file, not PlayerPrefs
            _flags = new OnboardingFlags(_flagStore);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            bool interact = kb != null && kb.eKey.wasPressedThisFrame;

            // While a conversation is up, the key advances it (and nothing is offered — the bubble IS the
            // surface, and a popup beside it would be naming the press the bubble has already taken).
            if (_presenter != null && _presenter.IsShowing)
            {
                Claim(true);
                _nearest = null;
                Offer(null);
                // ⚠️ THE DEV-KEY LEDGER IS EXHAUSTED A–Z (sweep 2026-08-17), so choosing an option binds
                // NOTHING new: it is the move axis the player already walks with, and Interact confirms.
                // The presenter ignores the axis unless rows are actually up, so this is inert in an
                // ordinary conversation. See the coordination note on ReadMoveAxis.
                _presenter.MoveSelection(ReadMoveAxis(kb));
                if (interact) BeginInteract();
                else if (HasWalkedAway()) _presenter.Close();
                return;
            }

            // A modal that is not ours is up (the wardrobe picker). Offer nothing and claim nothing while
            // it is: a popup naming a conversation over its panel would be advertising a press that panel
            // has already taken. BeginInteract refuses the press too — this is the half the player sees.
            if (InteractionGate.IsBlocked)
            {
                _nearest = null;
                Claim(false);
                Offer(null);
                return;
            }

            _nearest = FindNearest();
            Claim(_nearest != null);
            Offer(_nearest);
            if (interact) BeginInteract();
        }

        /// <summary>
        /// Who the "E: …" prompt is offering right now, or null when nobody is in reach — the
        /// affordance as STATE, recomputed every frame by <see cref="Update"/>.
        ///
        /// <para>Public because this is the readable half of "can you talk to them", and the only half
        /// that does not need a keypress: the prompt itself is a <c>Text</c> on a canvas, and a test
        /// that read it would be reading pixels' next-of-kin rather than the decision.</para>
        /// </summary>
        public Interactable Nearest => _nearest;

        /// <summary>
        /// Take an interact press: advance the conversation that is already up, or start one with
        /// whoever is in reach. Returns true when the press was SPENT (there was something to say or
        /// something to advance), false when it found nobody and the press belongs to somebody else.
        ///
        /// <para>Input-free on purpose, in the <c>ControlSwitcher.BeginInteract</c> shape this codebase
        /// already uses for the same job: the decision is separable from the device that triggers it,
        /// so it can be driven by a test — or later by the M2-39 interact verb, when NPCs become
        /// <c>IInteractable</c> candidates and this component's private key handling retires — without
        /// a keyboard in the loop. <see cref="Update"/> calls it, so what a test drives is what a press
        /// does.</para>
        /// </summary>
        public bool BeginInteract()
        {
            if (_presenter != null && _presenter.IsShowing) { _presenter.Advance(); return true; }

            // ⭐ A MODAL THAT IS NOT OURS OWNS THIS PRESS. Unlike every other reader of the interact key
            // (ControlSwitcher, InteractVerb), this component never consulted the gate — it only RAISES
            // it, through the presenter. That was harmless while the dialogue was the only modal, because
            // the branch above already covers "our own bubble is up". It stopped being harmless when a
            // second modal arrived: the wardrobe picker confirms on E while you stand at a fixture, and
            // Aunt Ginny is demonstrably within this component's 1.8 m radius of her own furniture — so
            // one press would both wear a coat and start a conversation.
            //
            // ⚠ ORDER IS load-BEARING: this sits BELOW the advance branch on purpose. The presenter
            // raises the gate itself while a conversation is running, so a flag read placed above would
            // refuse to advance the very dialogue that set it.
            if (InteractionGate.IsBlocked) return false;

            // ⭐ SAME FindNearest THE OFFER USES, facing filter and all — so the press and the popup can
            // never disagree. That is a real behaviour change and it is the ruling's whole point: you now
            // have to be LOOKING at somebody to talk to them. A popup that named one neighbour while the
            // key started a conversation with the other would be worse than no popup at all.
            Interactable target = FindNearest();
            if (target == null) return false;
            Begin(target);
            return true;
        }

        /// <summary>
        /// Tell the M2-39 interact verb that this press is already spoken for
        /// (<see cref="InteractActionClaim"/>) — raised by PROXIMITY, every frame, from the answer this
        /// component has already computed, never by the press itself.
        ///
        /// <para><b>Why the claim, when the header above says the ranges don't overlap.</b> Because at St
        /// Peters they now demonstrably do: Aunt Ginny stands 1.80 m from her freezer against this
        /// component's 1.8 m radius, and the seawater spot's 4 m reach overlaps Junior Poirier's. Those
        /// were harmless while the fixtures were on their own key (F); with the interact verb they land on
        /// E, and one press would both start a conversation AND work the fixture. This closes that by
        /// construction instead of by assertion — and it is why the coordination point this file's header
        /// flagged for gameplay-systems is finally a mechanism rather than a comment.</para>
        ///
        /// <para>Exactly ONE claimant today (this component). The flag is transitional and dies when NPCs
        /// become <c>IInteractable</c> candidates and the resolver arbitrates them like everything else —
        /// see <see cref="InteractActionClaim"/>. Released on disable so a torn-down region cannot leave
        /// the verb wedged off.</para>
        /// </summary>
        private void Claim(bool hasTarget) => InteractActionClaim.IsClaimed = hasTarget;

        private void OnDisable()
        {
            InteractActionClaim.Reset();
            // ...and a torn-down region must not leave the popup naming a neighbour who is no longer in
            // the scene. Same reason the claim is released here: a dead reader that holds a global on is
            // worse than one that never spoke.
            Offer(null);
            // A region torn down mid-conversation must not leave a villager standing in one forever.
            ReleaseSpeaker();
        }

        // ---- interaction --------------------------------------------------------------------

        /// <summary>
        /// Who this interactor measures proximity FROM, right now — the builder's own reference while
        /// it is alive, and otherwise whoever Core says is walking the world
        /// (<see cref="GameServices.PlayerTransform"/>). Null when there is nobody out there at all,
        /// under which this component simply offers nothing.
        ///
        /// <para><b>Why the serialized reference wins.</b> It is the more specific answer and it is
        /// right wherever it exists: in the START scene it names the real persistent player (the
        /// builder stands the core up in that same scene), and in a region scene played DIRECTLY for
        /// review it names that scene's dev stand-in, who is the only player there is. Preferring it
        /// means neither path can be perturbed by this — where the old code worked, it still measures
        /// from the same transform on the same frame.</para>
        ///
        /// <para><b>Why the fallback has to exist.</b> A region scene cannot name the persistent player
        /// at build time — it does not exist yet, and Unity does not serialize references across scenes
        /// regardless. So a travelled-in region's interactor is pointed at a dev stand-in that
        /// <c>DevRegionBootstrap</c> DESTROYS on arrival, and before this fallback that meant
        /// <see cref="FindNearest"/> returned null forever: Nine Mile Creek's two were MUTE for any
        /// player who sailed in, prompt and all.</para>
        ///
        /// <para><b>Resolved per tick, never cached</b> — two <c>UnityEngine.Object</c> null checks and
        /// a static read, no allocation (rule 7), and staying stateless is what keeps this correct
        /// across a shell restart, which replaces the persistent player with a different transform.</para>
        ///
        /// <para><b>⚠ Explicit <c>!= null</c>, never <c>??</c>/<c>?.</c>.</b> The reference this exists
        /// to survive is a DESTROYED one, and a destroyed <c>UnityEngine.Object</c> is fake-null: the
        /// null-propagating operators bypass the overloaded <c>==</c> and sail straight past it. A
        /// <c>_player ?? GameServices.PlayerTransform</c> here would compile clean, never take the
        /// fallback, and throw on the next dereference.</para>
        /// </summary>
        private Transform ResolvePlayer()
        {
            if (_player != null) return _player;
            return GameServices.PlayerTransform;   // already laundered to a REAL null by the accessor
        }

        /// <summary>
        /// The nearest interactable <b>inside the forward arc</b>, or null.
        ///
        /// <para><b>The order is filter-then-rank</b>, the same shape <see cref="InteractResolver"/> uses:
        /// switched-off, then reach, then the arc, and only survivors compete on distance. Facing is a
        /// FILTER and never a tie-break — a neighbour behind you does not win by being a centimetre
        /// nearer.</para>
        ///
        /// <para><b>The arc's cosine is computed once for the whole scan</b>, never per candidate (rule 7),
        /// which is exactly why <see cref="InteractArc"/> splits into two calls.</para>
        ///
        /// <para><b>Where the facing comes from.</b> <see cref="InteractActorProbe"/> — the Player lane
        /// publishes the same <see cref="InteractActor"/> it hands the interact verb, so the conversation
        /// filter and the verb cannot disagree about which way she is looking, and this module never names
        /// a Player type (rule 4). With no probe the facing is <see cref="Vector2.zero"/>, which
        /// <see cref="InteractArc"/> reads as "unknown" and passes — so a region scene played directly, with
        /// no persistent rig, behaves exactly as it did before facing existed.</para>
        ///
        /// <para><b>Position still comes from <see cref="ResolvePlayer"/>, not from the probe.</b> The
        /// probe is written by a different component's <c>Update</c> in undefined order, so its position
        /// could be a frame stale — invisible for a facing, but for a reach test measured in centimetres it
        /// would put the edge of the radius one frame behind the player. The transform is authoritative and
        /// free; only the facing has to be relayed.</para>
        /// </summary>
        private Interactable FindNearest()
        {
            Transform player = ResolvePlayer();
            if (player == null || _interactables == null) return null;
            Vector2 p = player.position;
            Vector2 facing = InteractActorProbe.Has ? InteractActorProbe.Current.Facing : Vector2.zero;
            float cosHalfArc = InteractArc.CosHalfArc(_facingArcDegrees);
            Interactable best = null;
            float bestSq = _radius * _radius;
            for (int i = 0; i < _interactables.Length; i++)
            {
                var it = _interactables[i];
                // ⭐ SWITCHED OFF MEANS NOT THERE. You cannot talk to somebody you cannot see, and since
                // routines landed there are people who genuinely are not there: a villager inside a house
                // the player is not in is hidden, and her spot is a metre past a door the player can stand
                // at — so without this the popup would offer a conversation with an invisible neighbour
                // through her own wall, and she would answer. VillagerRoutine switches her Interactable with her
                // renderer; this is the half that makes that mean something.
                if (it == null || !it.isActiveAndEnabled) continue;
                Vector2 delta = (Vector2)it.transform.position - p;
                float sq = delta.sqrMagnitude;
                if (sq > bestSq) continue;

                // ⭐ FACING, and it is a filter: the one you are LOOKING at, not the one you are marginally
                // nearer to. Without it the popup standing between two fixtures would name whichever
                // happened to be a centimetre closer, which is a coin toss dressed up as an affordance.
                if (!InteractArc.Contains(delta, facing, cosHalfArc)) continue;

                bestSq = sq; best = it;
            }
            return best;
        }

        /// <summary>
        /// How much further than <see cref="_radius"/> the player may drift before a conversation gives
        /// up on them. Hysteresis, not owner feel: without it, standing exactly on the edge of reach
        /// would open and close the bubble on alternate frames, and a half-step back mid-sentence would
        /// read as being hung up on.
        /// </summary>
        public const float DisengageSlack = 1.35f;

        /// <summary>
        /// Has the player left the conversation? True when they are past
        /// <see cref="_radius"/> × <see cref="DisengageSlack"/> from whoever is talking, or when that
        /// speaker has stopped being there at all (a villager whose routine took her inside a house the
        /// player is not in switches her <see cref="Interactable"/> off with her renderer).
        ///
        /// <para>Walking off is a real way to end a conversation and it must not leave anything wedged:
        /// the presenter's <c>Close</c> lowers the <see cref="InteractionGate"/> and fires the same
        /// completion callback a normal ending does, and that callback is what releases the villager
        /// back onto her day. One ending, not two.</para>
        /// </summary>
        private bool HasWalkedAway()
        {
            if (_speaker == null) return false;              // nobody in particular is talking
            if (!_speaker.isActiveAndEnabled) return true;   // she went in through her own door

            Transform player = ResolvePlayer();
            if (player == null) return false;

            float reach = _radius * DisengageSlack;
            return ((Vector2)_speaker.transform.position - (Vector2)player.position).sqrMagnitude
                   > reach * reach;
        }

        private void Begin(Interactable it)
        {
            if (_presenter == null) return;

            bool metBefore = _flags.Get(it.CompletionFlag);
            // Content as DATA first (CLAUDE.md rule 2): an NpcDef → DialogueDef supplies the lines when
            // wired; only fall back to the legacy WorldStrings table for the older string-driven cove
            // interactables that have no NpcDef.
            //
            // The flag store goes in with it so the asset's own conditional beat can fire: the key is
            // authored in the DialogueDef, and the same save-backed store that remembers 'met before'
            // answers it. That is how Ginny knows she fronted the licence fee without this module ever
            // naming the economy component that fronted it (rule 4).
            string[] text = it.HasNpcData
                ? it.DialogueLines(metBefore, _flagStore)
                : WorldStrings.Conversation(it.ConversationId, metBefore);
            if (text == null || text.Length == 0) return;

            var lines = new List<DialogueLine>(text.Length);
            for (int i = 0; i < text.Length; i++)
                lines.Add(new DialogueLine(it.Speaker, text[i]));

            // The bubble hangs at the SPEAKER (owner ruling 2026-07-30) and fills at THEIR cadence; the
            // rows they offer are data on the same DialogueDef. Every one of the three is optional —
            // no anchor parks the bubble, no voice fills at the default, no options ends on the last
            // line — so an interactable authored before any of this existed behaves exactly as it did.
            NpcDef npc = it.Npc;
            var request = new DialogueRequest(
                lines,
                it.BubbleAnchorTransform,
                it.BubbleAnchorOffset,
                DialogueVoiceDef.VoiceOf(npc != null ? npc.Voice : null),
                it.HasNpcData ? DialogueOptionPicker.RowsFor(npc.Dialogue.Options) : null,
                it.HasNpcData ? npc.Dialogue.Id : it.ConversationId,
                npc != null ? npc.Id : null);

            // ⭐ SHE STOPS TO TALK — if she can. The interruptibility is authored per routine block, so
            // this asks rather than decides; a refused stop is not a refused conversation, she just
            // answers on the move and the bubble travels with her.
            _speaker = it;
            _speakerRoutine = it.GetComponent<VillagerRoutine>();
            if (_speakerRoutine != null) _speakerRoutine.TryHoldForConversation(ResolvePlayer());

            string flag = it.CompletionFlag;
            _presenter.Play(request, () =>
            {
                if (!string.IsNullOrEmpty(flag)) _flags.Set(flag, true);
                ReleaseSpeaker();
            });
        }

        /// <summary>Let whoever was talking get back to their day. Called from the presenter's single
        /// completion callback, so every way a conversation can end — the last line, an option, the
        /// player walking off — releases them by the same path.</summary>
        private void ReleaseSpeaker()
        {
            if (_speakerRoutine != null) _speakerRoutine.ReleaseFromConversation();
            _speakerRoutine = null;
            _speaker = null;
        }

        /// <summary>
        /// This frame's vertical move axis, from the SAME keys that walk the fisher
        /// (<c>PlayerWalkController.ReadInput</c>) — +1 up, -1 down.
        ///
        /// <para><b>⚑ Coordination point for gameplay-systems, flagged in the PR.</b> While the option
        /// rows are up this axis does two things at once: it moves the cursor AND it still walks the
        /// player, because <see cref="InteractionGate"/> gates the interact VERB and nothing gates
        /// movement (its semantics are deliberately unchanged here). In practice that is legible rather
        /// than broken — the picker is edge-latched, so a TAP steps a row and nudges the fisher a
        /// centimetre, while a HOLD walks them out of conversation range, which closes the bubble and is
        /// a perfectly good way to leave. The clean fix is a movement claim in the shape of
        /// <see cref="InteractActionClaim"/>, which is gameplay-systems' to own and out of this lane.</para>
        /// </summary>
        private static float ReadMoveAxis(Keyboard kb)
        {
            if (kb == null) return 0f;
            float axis = 0f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) axis += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) axis -= 1f;
            return axis;
        }

        /// <summary>Wire the interactor in one call (editor / tests). The arc keeps its shipped default
        /// so every existing caller compiles and behaves as it did — except that it now filters by facing,
        /// which is the point.</summary>
        public void Configure(Transform player, DialoguePresenter presenter, Interactable[] interactables,
                              float radius, float facingArcDegrees = DefaultFacingArcDegrees)
        {
            _player = player;
            _presenter = presenter;
            _interactables = interactables;
            _radius = radius;
            _facingArcDegrees = Mathf.Clamp(facingArcDegrees, 0f, 360f);
        }

        /// <summary>The forward arc this interactor is filtering on, in degrees (full angle). Public so a
        /// test can assert the shipped default rather than restate it.</summary>
        public float FacingArcDegrees => _facingArcDegrees;

        // ---- the offer (what the popup draws) -------------------------------------------------

        /// <summary>
        /// State this component's answer on the Core offer channel — <b>the only thing it now publishes to
        /// the player</b>, and the replacement for the floating "E: Talk to …" label the 2026-08-19 ruling
        /// retired.
        ///
        /// <para><b>Null means "nothing", and it is said out loud every frame it is true.</b> A feeder that
        /// simply went quiet would leave its slot holding the last neighbour it saw — see
        /// <see cref="InteractOffer.Set"/>. So this is called on every path <c>ShowPrompt(null)</c> was,
        /// gate included, and on disable.</para>
        ///
        /// <para><b>Rule 7.</b> The label interpolates, so it is cached against the speaker it was built
        /// for; standing in front of Aunt Ginny costs a reference compare a frame and nothing else. The
        /// publish itself is edge-triggered inside <see cref="InteractOffer"/>.</para>
        /// </summary>
        private void Offer(Interactable target)
        {
            if (target == null) { InteractOffer.Clear(InteractOfferSource.Conversation); return; }

            Vector2 at = target.transform.position;
            Transform player = ResolvePlayer();
            float distSq = player != null ? ((Vector2)player.position - at).sqrMagnitude : 0f;

            InteractOffer.Set(InteractOfferSource.Conversation, OfferIdFor(target), OfferLabelFor(target),
                              at, distSq);
        }

        /// <summary>
        /// The offered line for an interactable, built ONCE per speaker.
        ///
        /// <para>Cached on the reference rather than on the composed string: the two things it is made of
        /// (kind and speaker name) come off an <see cref="NpcDef"/> that does not change under a live
        /// interactable, and the reference changing is the only way the answer can. Same shape as the
        /// carriables' label pair, for the same rule-7 reason.</para>
        /// </summary>
        private string OfferLabelFor(Interactable target)
        {
            if (!ReferenceEquals(target, _labelledFor))
            {
                _labelledFor = target;
                _label = WorldStrings.OfferLabel(target.Kind, target.Speaker);
            }
            return _label;
        }

        /// <summary>
        /// A stable id for the offered interactable, for the affordance lane to match its own registrant
        /// on — the <see cref="NpcDef"/>'s id where there is one, else the legacy conversation id.
        ///
        /// <para>Allocation-free: both are authored strings read straight off the asset, never composed.
        /// An interactable with neither returns null, which the offer carries happily — the LABEL is what
        /// the popup needs, and the id is for a listener that has something to match.</para>
        /// </summary>
        private static string OfferIdFor(Interactable target)
        {
            NpcDef npc = target.Npc;
            if (npc != null && !string.IsNullOrEmpty(npc.Id)) return npc.Id;
            return target.ConversationId;
        }
    }
}
