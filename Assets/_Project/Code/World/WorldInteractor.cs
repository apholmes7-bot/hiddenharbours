using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The on-foot INTERACT driver for world things (VS-21): when the player is near an
    /// <see cref="Interactable"/> (Aunt Ginny, the neighbour, Ned's logbook), it shows a floating
    /// "E: …" prompt and, on the Interact key, starts that conversation in the
    /// <see cref="DialoguePresenter"/>. While a conversation is up it forwards the key to advance the
    /// lines.
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
        [Tooltip("How close (m) the player must be to an interactable for the prompt to show.")]
        [SerializeField] private float _radius = 1.8f;

        // Onboarding flags, backed by the save file (VS-08) so the 'met before' variants persist across reload.
        // The STORE itself is kept beside them because a DialogueDef's conditional beat is gated on an
        // arbitrary authored key, not just the three named onboarding flags — see Begin().
        private IFlagStore _flagStore;
        private OnboardingFlags _flags;
        private Text _prompt;
        private Interactable _nearest;

        private void Awake()
        {
            _flagStore = new SaveFlagStore();                    // VS-08: persisted via the save file, not PlayerPrefs
            _flags = new OnboardingFlags(_flagStore);
            BuildPrompt();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            bool interact = kb != null && kb.eKey.wasPressedThisFrame;

            // While a conversation is up, the key advances it (and no prompt shows).
            if (_presenter != null && _presenter.IsShowing)
            {
                Claim(true);
                _nearest = null;
                ShowPrompt(null);
                if (interact) BeginInteract();
                return;
            }

            _nearest = FindNearest();
            Claim(_nearest != null);
            ShowPrompt(_nearest);
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

        private void OnDisable() => InteractActionClaim.Reset();

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

        private Interactable FindNearest()
        {
            Transform player = ResolvePlayer();
            if (player == null || _interactables == null) return null;
            Vector2 p = player.position;
            Interactable best = null;
            float bestSq = _radius * _radius;
            for (int i = 0; i < _interactables.Length; i++)
            {
                var it = _interactables[i];
                // ⭐ SWITCHED OFF MEANS NOT THERE. You cannot talk to somebody you cannot see, and since
                // routines landed there are people who genuinely are not there: a villager inside a house
                // the player is not in is hidden, and her spot is a metre past a door the player can stand
                // at — so without this you would get an "E: Talk" prompt on an invisible neighbour through
                // her own wall, and she would answer. VillagerRoutine switches her Interactable with her
                // renderer; this is the half that makes that mean something.
                if (it == null || !it.isActiveAndEnabled) continue;
                float sq = ((Vector2)it.transform.position - p).sqrMagnitude;
                if (sq <= bestSq) { bestSq = sq; best = it; }
            }
            return best;
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
                lines.Add(new DialogueLine(it.Speaker, it.Portrait, text[i]));

            string flag = it.CompletionFlag;
            _presenter.Play(lines, () =>
            {
                if (!string.IsNullOrEmpty(flag)) _flags.Set(flag, true);
            });
        }

        /// <summary>Wire the interactor in one call (editor / tests).</summary>
        public void Configure(Transform player, DialoguePresenter presenter, Interactable[] interactables, float radius)
        {
            _player = player;
            _presenter = presenter;
            _interactables = interactables;
            _radius = radius;
        }

        // ---- floating prompt ----------------------------------------------------------------

        private void ShowPrompt(Interactable target)
        {
            if (_prompt == null) return;
            bool show = target != null;
            if (_prompt.enabled != show) _prompt.enabled = show;
            if (show)
            {
                string text = WorldStrings.Prompt(target.Kind, target.Speaker);
                if (_prompt.text != text) _prompt.text = text;
            }
        }

        private void BuildPrompt()
        {
            var canvasGo = new GameObject("WorldInteract_Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 96;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("Prompt", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 230f); // above the board/dock hint and onboarding line
            rt.sizeDelta = new Vector2(560f, 50f);

            _prompt = go.GetComponent<Text>();
            _prompt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _prompt.fontSize = 30;
            _prompt.alignment = TextAnchor.LowerCenter;
            _prompt.color = Color.white;
            _prompt.horizontalOverflow = HorizontalWrapMode.Overflow;
            _prompt.verticalOverflow = VerticalWrapMode.Overflow;
            _prompt.raycastTarget = false;

            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            _prompt.enabled = false;
        }
    }
}
