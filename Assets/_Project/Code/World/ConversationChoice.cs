using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The villager the player is facing, as the chooser's one thing</b>: "Talk" first (the default
    /// row), and one "Give &lt;item&gt;" row beneath it per giftable item TYPE he has anywhere (his hands,
    /// what he carries, his storage: <see cref="GiftInventory"/>). With nothing giftable there is one action
    /// and no list: the press simply talks, as it always did.
    ///
    /// <para><b>Why a thing of its own.</b> Villagers are found by <see cref="WorldInteractor"/> (its own
    /// reach and facing filter), not registered with <see cref="Interactables"/>, so the chooser is handed
    /// this one object (<see cref="InteractChoiceList.BuildActionsOf"/>,
    /// <see cref="InteractChooser.OpenUnregistered"/>). The chooser's confirm re-checks it live through the
    /// <see cref="IInteractable"/> half (she is still there, still in reach, still in front of him) and
    /// then calls back into the interactor, which owns the talk and the gift.</para>
    ///
    /// <para>One instance per interactor, rebound per press (<see cref="Bind"/>, <see cref="AddGive"/>) into
    /// fixed buffers: nothing allocates while the player simply stands in front of somebody (rule 7).</para>
    /// </summary>
    public sealed class ConversationChoice : IInteractable, IMultiActionInteractable
    {
        /// <summary>The talk row: always present, always first, the default.</summary>
        public const int TalkAction = 0;

        /// <summary>The first gift row; gift row <c>n</c> is action <c>FirstGiveAction + n</c>.</summary>
        public const int FirstGiveAction = 1;

        /// <summary>The most gift rows: the chooser's nine, less Talk.</summary>
        public const int MaxGiveRows = InteractChoiceList.HardCap - 1;

        private readonly Action<Interactable> _talk;
        private readonly Action<Interactable, string> _give;
        private readonly string[] _giveIds = new string[MaxGiveRows];
        private readonly string[] _giveLabels = new string[MaxGiveRows];
        private int _giveCount;
        private Interactable _target;
        private float _reach;

        /// <param name="talk">Start the conversation with her.</param>
        /// <param name="give">Give her ONE of the item type whose id is passed.</param>
        public ConversationChoice(Action<Interactable> talk, Action<Interactable, string> give)
        {
            _talk = talk;
            _give = give;
        }

        /// <summary>Point at <paramref name="target"/> with no gift rows yet (<see cref="AddGive"/> adds
        /// them).</summary>
        public void Bind(Interactable target, float reachMeters)
        {
            _target = target;
            _reach = reachMeters;
            for (int i = 0; i < _giveCount; i++) { _giveIds[i] = null; _giveLabels[i] = null; }
            _giveCount = 0;
        }

        /// <summary>Add a gift row for item type <paramref name="itemId"/>, reading
        /// <paramref name="label"/>. False (nothing added) for an empty id or label, or when the rows are
        /// full.</summary>
        public bool AddGive(string itemId, string label)
        {
            if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(label) || _giveCount >= MaxGiveRows)
                return false;
            _giveIds[_giveCount] = itemId;
            _giveLabels[_giveCount] = label;
            _giveCount++;
            return true;
        }

        /// <summary>Who this points at, or null.</summary>
        public Interactable Target => _target;

        /// <summary>How many gift rows are on offer.</summary>
        public int GiveCount => _giveCount;

        /// <summary>True when at least one gift row is on offer.</summary>
        public bool OffersGive => _giveCount > 0;

        /// <summary>The item type action <paramref name="index"/> gives, or null for Talk / out of range.</summary>
        public string GiveItemId(int index)
        {
            int row = index - FirstGiveAction;
            return row >= 0 && row < _giveCount ? _giveIds[row] : null;
        }

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

        public int ActionCount => FirstGiveAction + _giveCount;

        public string ActionLabel(int index)
        {
            if (index == TalkAction) return WorldStrings.TalkVerb;
            int row = index - FirstGiveAction;
            return row >= 0 && row < _giveCount ? _giveLabels[row] : null;
        }

        public bool ActionAvailable(int index) => index == TalkAction || GiveItemId(index) != null;

        public void Interact(in InteractActor actor, int index)
        {
            if (_target == null) return;
            string itemId = GiveItemId(index);
            if (itemId != null) _give?.Invoke(_target, itemId);
            else _talk?.Invoke(_target);
        }
    }
}
