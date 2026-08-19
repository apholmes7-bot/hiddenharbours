using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE SKIPPER'S ONE LINE</b> — the World side of the arrival's <see cref="ArrivalCompleted"/>
    /// signal. It hears that the player is ashore and puts the line in a bubble over the man who said
    /// it, through the same <see cref="DialoguePresenter"/> every other conversation in the game uses.
    ///
    /// <para><b>⭐ THIS IS THE WHOLE OF THE OPENING'S "GUIDANCE".</b> No quest opens, no marker is
    /// drawn, no arrow appears. The path is cut, the cottage is at the end of it, and a man tells you
    /// once — which is the diegetic-UI law taken literally
    /// (<c>design/diegetic-ui-and-inventory.md</c>: information is EARNED and lives in the world). If a
    /// playtester walks the wrong way, the answer is a better path, not a HUD.</para>
    ///
    /// <para><b>⚠ It is deliberately unconditional and one-shot.</b> The line is not an interaction, so
    /// there is nothing to press and nothing to gate — it plays once, when the signal comes, and the
    /// signal only ever comes on the arrival's own terms (a fresh save, at the moment control lands).
    /// Nothing here re-decides that; a second opinion about whether the opening happened is exactly how
    /// two systems come to disagree about it.</para>
    ///
    /// <para>Wired by the region builder beside the dialogue view it drives. With no presenter in the
    /// scene it logs the line and does nothing else, so a fixture without the bubble kit still finishes
    /// the arrival.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArrivalLineSpeaker : MonoBehaviour
    {
        /// <summary>How far above the speaker's own transform the bubble hangs, in world metres — the
        /// same head height <see cref="Interactable.BubbleAnchorOffset"/> falls back to, read from there
        /// so a re-tune of "where a head is" moves both.</summary>
        [Tooltip("Head height above the skipper's transform, in metres. Defaults to the same figure an " +
                 "un-anchored Interactable uses.")]
        [SerializeField] private float _bubbleHeightMetres = Interactable.DefaultBubbleHeightMetres;

        [Tooltip("The dialogue view. Left empty, it is found in the scene on the first line — a region " +
                 "builds its DialogueUI and this in either order.")]
        [SerializeField] private DialoguePresenter _presenter;

        private bool _subscribed;

        /// <summary>True once the line has been spoken — the claim a PlayMode test reads.</summary>
        public bool HasSpoken { get; private set; }

        /// <summary>Wire it in one call (the builder / tests).</summary>
        public void Configure(DialoguePresenter presenter) => _presenter = presenter;

        private void OnEnable()
        {
            if (_subscribed) return;
            EventBus.Subscribe<ArrivalCompleted>(OnArrived);
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed) EventBus.Unsubscribe<ArrivalCompleted>(OnArrived);
            _subscribed = false;
        }

        private void OnArrived(ArrivalCompleted e)
        {
            HasSpoken = true;

            if (string.IsNullOrWhiteSpace(e.Line)) return;

            var view = _presenter != null
                ? _presenter
                : (_presenter = FindAnyObjectByType<DialoguePresenter>(FindObjectsInactive.Exclude));

            if (view == null)
            {
                Debug.Log($"[ArrivalLineSpeaker] no dialogue view in this scene — the skipper's line goes " +
                          $"unspoken: \"{e.Line}\"");
                return;
            }

            var lines = new List<DialogueLine> { new DialogueLine(e.SpeakerName, e.Line) };
            view.Play(new DialogueRequest(lines, e.Speaker,
                                          e.Speaker != null
                                              ? new Vector3(0f, _bubbleHeightMetres, 0f)
                                              : Vector3.zero,
                                          DialogueVoice.Default));
        }
    }
}
