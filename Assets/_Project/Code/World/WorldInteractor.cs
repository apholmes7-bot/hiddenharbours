using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    /// </summary>
    public sealed class WorldInteractor : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("The on-foot player's transform (proximity is measured from here).")]
        [SerializeField] private Transform _player;
        [SerializeField] private DialoguePresenter _presenter;
        [Tooltip("Everything the player can walk up to and interact with.")]
        [SerializeField] private Interactable[] _interactables;

        [Header("Tuning")]
        [Tooltip("How close (m) the player must be to an interactable for the prompt to show.")]
        [SerializeField] private float _radius = 1.8f;

        // Onboarding flags, backed by the save file (VS-08) so the 'met before' variants persist across reload.
        private OnboardingFlags _flags;
        private Text _prompt;
        private Interactable _nearest;

        private void Awake()
        {
            _flags = new OnboardingFlags(new SaveFlagStore());   // VS-08: persisted via the save file, not PlayerPrefs
            BuildPrompt();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            bool interact = kb != null && kb.eKey.wasPressedThisFrame;

            // While a conversation is up, the key advances it (and no prompt shows).
            if (_presenter != null && _presenter.IsShowing)
            {
                if (interact) _presenter.Advance();
                ShowPrompt(null);
                return;
            }

            _nearest = FindNearest();
            ShowPrompt(_nearest);
            if (_nearest != null && interact) Begin(_nearest);
        }

        // ---- interaction --------------------------------------------------------------------

        private Interactable FindNearest()
        {
            if (_player == null || _interactables == null) return null;
            Vector2 p = _player.position;
            Interactable best = null;
            float bestSq = _radius * _radius;
            for (int i = 0; i < _interactables.Length; i++)
            {
                var it = _interactables[i];
                if (it == null) continue;
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
            string[] text = it.HasNpcData
                ? it.DialogueLines(metBefore)
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
