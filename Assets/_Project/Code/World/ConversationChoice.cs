using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The villager the player is facing, as the chooser's one thing</b>: "Talk" first (the default
    /// row), and "Give &lt;item&gt;" beneath it when a giftable catch is in his hand. With nothing giftable
    /// held there is one action and no list: the press simply talks, as it always did.
    ///
    /// <para><b>Why a thing of its own.</b> Villagers are found by <see cref="WorldInteractor"/> (its own
    /// reach and facing filter), not registered with <see cref="Interactables"/>, so the chooser is handed
    /// this one object (<see cref="InteractChoiceList.BuildActionsOf"/>,
    /// <see cref="InteractChooser.OpenUnregistered"/>). The chooser's confirm re-checks it live through the
    /// <see cref="IInteractable"/> half — she is still there, still in reach, still in front of him — and
    /// then calls back into the interactor, which owns the talk and the gift.</para>
    ///
    /// <para>One instance per interactor, rebound per press (<see cref="Bind"/>): nothing allocates while
    /// the player simply stands in front of somebody (rule 7).</para>
    /// </summary>
    public sealed class ConversationChoice : IInteractable, IMultiActionInteractable
    {
        /// <summary>The talk row: always present, always first, the default.</summary>
        public const int TalkAction = 0;

        /// <summary>The gift row: present only while something giftable is in hand.</summary>
        public const int GiveAction = 1;

        private readonly Action<Interactable> _talk;
        private readonly Action<Interactable> _give;
        private Interactable _target;
        private string _giveLabel;
        private float _reach;

        /// <param name="talk">Start the conversation with her.</param>
        /// <param name="give">Give her what is in his hand.</param>
        public ConversationChoice(Action<Interactable> talk, Action<Interactable> give)
        {
            _talk = talk;
            _give = give;
        }

        /// <summary>Point at <paramref name="target"/>. <paramref name="giveLabel"/> null or empty means
        /// nothing giftable is held, so only "Talk" is offered.</summary>
        public void Bind(Interactable target, string giveLabel, float reachMeters)
        {
            _target = target;
            _giveLabel = giveLabel;
            _reach = reachMeters;
        }

        /// <summary>Who this points at, or null.</summary>
        public Interactable Target => _target;

        /// <summary>True when the gift row is on offer.</summary>
        public bool OffersGive => !string.IsNullOrEmpty(_giveLabel);

        // ---- IInteractable: what the chooser's confirm re-checks --------------------------------

        public string Id
        {
            get
            {
                if (_target == null) return null;
                NpcDef npc = _target.Npc;
                return npc != null && !string.IsNullOrEmpty(npc.Id) ? npc.Id : _target.ConversationId;
            }
        }

        public string VerbLabel => WorldStrings.TalkVerb;

        public Vector2 WorldPosition => _target != null ? (Vector2)_target.transform.position : Vector2.zero;

        public float ReachMeters => _reach;

        public int Priority => InteractPriority.Fixture;

        // The interactor never asked where the player stood (a villager on the wharf answers him on deck
        // too), so the re-check does not either.
        public InteractContext Contexts => InteractContext.OnFoot | InteractContext.OnDeck;

        public bool RequiresFacing => true;

        // Explicit Unity null: a villager whose region was torn down is fake-null, not null.
        public bool IsAvailable => _target != null && _target.isActiveAndEnabled;

        public void Interact(in InteractActor actor) => Interact(actor, TalkAction);

        // ---- IMultiActionInteractable: the rows --------------------------------------------------

        public int ActionCount => OffersGive ? 2 : 1;

        public string ActionLabel(int index)
        {
            if (index == TalkAction) return WorldStrings.TalkVerb;
            if (index == GiveAction && OffersGive) return _giveLabel;
            return null;
        }

        public bool ActionAvailable(int index) => index == TalkAction || (index == GiveAction && OffersGive);

        public void Interact(in InteractActor actor, int index)
        {
            if (_target == null) return;
            if (index == GiveAction && OffersGive) _give?.Invoke(_target);
            else _talk?.Invoke(_target);
        }
    }
}
