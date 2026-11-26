using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>Somebody has a supply put by, and will give it if asked.</b> One asset per giving: who gives it
    /// (<see cref="GiverNpcId"/>), the row the player picks to ask (<see cref="OptionId"/> and
    /// <see cref="RowLabel"/>), what goes into the paper bag (<see cref="ItemIds"/>, all or none) and what
    /// is said, for the giving and for each way it can be refused.
    ///
    /// <para><b>No "given" flag.</b> A supply counts as received while he owns any of each item, or once a
    /// job that used it is done (<see cref="ReceivedFlags"/>, the mending jobs' own completion flags). So
    /// the row cannot be asked twice for one hank of oakum, and nothing new is saved.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Supplies/Supply Gift", fileName = "SupplyGift")]
    public sealed class SupplyGiftDef : ScriptableObject
    {
        [Tooltip("Stable, append-only id (supplygift.snake_case).")]
        public string Id = "supplygift.example";

        [Tooltip("The npc id of who gives it (npc.snake_case). The row appears only in their conversation.")]
        public string GiverNpcId = "";

        [Tooltip("The row's stable id (option.snake_case), carried by the DialogueOptionPicked signal.")]
        public string OptionId = "";

        [Tooltip("What the row says, in the player's voice.")]
        public string RowLabel = "";

        [Tooltip("What the giver says as they hand it over.")]
        [TextArea] public string[] ReplyLines = new string[0];

        [Tooltip("Supply ids that go into the paper bag, one of each, all or none.")]
        public string[] ItemIds = new string[0];

        [Tooltip("Save-flag keys that mean the supply has already been received and used (the mending " +
                 "jobs' completion flags). Any one set counts as received.")]
        public string[] ReceivedFlags = new string[0];

        [Tooltip("The shopping bag kind it goes into (shoppingbag.snake_case).")]
        public string BagId = "shoppingbag.paper";

        [Tooltip("Said when he has no bag in hand and no free hand for one. Nothing is given.")]
        [TextArea] public string[] HandsFullLines = new string[0];

        [Tooltip("Said when the bag in his hand has no room. Nothing is given.")]
        [TextArea] public string[] BagFullLines = new string[0];

        [Tooltip("Said when his bag is set down somewhere else. Nothing is given.")]
        [TextArea] public string[] BagElsewhereLines = new string[0];

        /// <summary>True when the player already has this supply: he owns some of every item, or a job
        /// that used it is done. A null save reads as not received.</summary>
        public bool Received(SaveData data, IFlagStore flags)
        {
            if (flags != null && ReceivedFlags != null)
                for (int i = 0; i < ReceivedFlags.Length; i++)
                    if (!string.IsNullOrEmpty(ReceivedFlags[i]) && flags.Get(ReceivedFlags[i])) return true;
            if (data == null || ItemIds == null || ItemIds.Length == 0) return false;
            for (int i = 0; i < ItemIds.Length; i++)
                if (SupplyLocker.Owned(data, ItemIds[i]) <= 0) return false;
            return true;
        }

        /// <summary>The words for an outcome (null: say nothing and end).</summary>
        public string[] LinesFor(SupplyGiftOutcome outcome)
        {
            switch (outcome)
            {
                case SupplyGiftOutcome.Given: return ReplyLines;
                case SupplyGiftOutcome.HandsFull: return HandsFullLines;
                case SupplyGiftOutcome.BagFull: return BagFullLines;
                case SupplyGiftOutcome.BagElsewhere: return BagElsewhereLines;
                default: return null;
            }
        }
    }
}
