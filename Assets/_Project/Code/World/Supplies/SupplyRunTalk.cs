using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>What the supply runs add to a conversation</b>: the teller's extra lines, the givers' rows and
    /// the ask-around row. <see cref="WorldInteractor"/> asks this as a conversation opens; the rows answer
    /// at the moment they are picked (<see cref="DialogueOption.ReplyAtPick"/>), so what is said is read
    /// from the save as it stands then, never as it stood when the bubble went up.
    ///
    /// <para><b>Nothing here before he owns her, or after she is mended</b> (<see cref="IsUnderway"/>):
    /// every existing conversation is untouched outside that window. Talking and saying goodbye changes
    /// nothing; only picking a row does.</para>
    /// </summary>
    public static class SupplyRunTalk
    {
        /// <summary>Where the runs come from. Null (the default) loads them from Resources; a test hands in
        /// its own.</summary>
        public static Func<IReadOnlyList<SupplyRunDef>> RunsSource { get; set; }

        /// <summary>How a bag kind is found by id. Null (the default) reads the shipped kinds.</summary>
        public static Func<string, ShoppingBagDef> BagLookup { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            RunsSource = null;
            BagLookup = null;
        }

        private static IReadOnlyList<SupplyRunDef> Runs()
            => RunsSource != null ? RunsSource() : Resources.LoadAll<SupplyRunDef>(SupplyRunDef.ResourcesFolder);

        private static ShoppingBagDef FindBag(string id)
            => BagLookup != null ? BagLookup(id) : ShoppingBagBook.FromResources().Find(id);

        // ---- the window --------------------------------------------------------------------------

        /// <summary>True while the run's boat is his, still on the slip, and not yet mended.</summary>
        public static bool IsUnderway(SupplyRunDef run, ISaveService save)
        {
            SaveData data = save?.Current;
            if (run == null || data == null || string.IsNullOrEmpty(run.BoatId)) return false;
            if (!SlipDoryStory.IsOnTheSlip(save)) return false;
            if (data.OwnedBoats == null || !data.OwnedBoats.Contains(run.BoatId)) return false;
            return data.RepairedBoats == null || !data.RepairedBoats.Contains(run.BoatId);
        }

        /// <summary>True once the teller has named what she wants.</summary>
        public static bool IsNamed(SupplyRunDef run, IFlagStore flags)
            => run != null && !string.IsNullOrEmpty(run.NamedFlag) && flags != null && flags.Get(run.NamedFlag);

        /// <summary>True when every supply of the run is in hand (owned, or already used).</summary>
        public static bool AllInHand(SupplyRunDef run, SaveData data, IFlagStore flags)
        {
            if (run?.Gifts == null) return true;
            for (int i = 0; i < run.Gifts.Length; i++)
                if (run.Gifts[i] != null && !run.Gifts[i].Received(data, flags)) return false;
            return true;
        }

        // ---- the teller --------------------------------------------------------------------------

        /// <summary>
        /// <paramref name="lines"/> with the teller's run lines appended when <paramref name="npcId"/> tells
        /// a run that is underway. <paramref name="setWhenDone"/> is the flag to set when the conversation
        /// ends (the named flag, the first time), or null.
        /// </summary>
        public static string[] WithTellerLines(string npcId, string[] lines, ISaveService save, IFlagStore flags,
                                               out string setWhenDone)
        {
            setWhenDone = null;
            if (string.IsNullOrEmpty(npcId)) return lines;
            IReadOnlyList<SupplyRunDef> runs = Runs();
            if (runs == null) return lines;
            for (int r = 0; r < runs.Count; r++)
            {
                SupplyRunDef run = runs[r];
                if (run == null || run.TellerNpcId != npcId || !IsUnderway(run, save)) continue;
                string[] extra;
                if (AllInHand(run, save.Current, flags)) extra = run.AllInHandLines;
                else if (IsNamed(run, flags)) extra = run.MissingLines;
                else { extra = run.NamingLines; setWhenDone = run.NamedFlag; }
                return Append(lines, extra);
            }
            return lines;
        }

        private static string[] Append(string[] a, string[] b)
        {
            if (b == null || b.Length == 0) return a;
            if (a == null || a.Length == 0) return (string[])b.Clone();
            var all = new string[a.Length + b.Length];
            Array.Copy(a, all, a.Length);
            Array.Copy(b, 0, all, a.Length, b.Length);
            return all;
        }

        // ---- the rows ----------------------------------------------------------------------------

        /// <summary>
        /// <paramref name="rows"/> (a picker list, close row last, or null) with this speaker's run rows
        /// added before the close row: the giver's "would you have..." row while the supply is still
        /// wanted, and the ask-around row while anything is. Null in, nothing to add: null out. The rows'
        /// answers are worked out when picked, against <paramref name="hands"/> as it stands then.
        /// </summary>
        public static List<DialogueOption> WithRows(string npcId, List<DialogueOption> rows, ISaveService save,
                                                    IFlagStore flags, Func<IShoppingBagHands> hands)
        {
            if (string.IsNullOrEmpty(npcId)) return rows;
            IReadOnlyList<SupplyRunDef> runs = Runs();
            if (runs == null) return rows;
            for (int r = 0; r < runs.Count; r++)
            {
                SupplyRunDef run = runs[r];
                if (run == null || !IsUnderway(run, save) || !IsNamed(run, flags)) continue;
                SaveData data = save.Current;

                if (run.Gifts != null)
                    for (int g = 0; g < run.Gifts.Length; g++)
                    {
                        SupplyGiftDef gift = run.Gifts[g];
                        if (gift == null || gift.GiverNpcId != npcId || gift.Received(data, flags)) continue;
                        rows = Insert(rows, GiftRow(gift, save, flags, hands));
                    }

                ClueTopicDef topic = run.Topic;
                if (topic != null && topic.AskedOf(npcId) && !AllInHand(run, data, flags))
                    rows = Insert(rows, TopicRow(topic, npcId, save, flags));
            }
            return rows;
        }

        private static List<DialogueOption> Insert(List<DialogueOption> rows, DialogueOption row)
        {
            if (rows == null)
            {
                rows = new List<DialogueOption>(2) { row, DialogueOption.Close };
                return rows;
            }
            int at = rows.Count > 0 && rows[rows.Count - 1].Id == DialogueOption.CloseId ? rows.Count - 1 : rows.Count;
            rows.Insert(at, row);
            return rows;
        }

        private static DialogueOption GiftRow(SupplyGiftDef gift, ISaveService save, IFlagStore flags,
                                              Func<IShoppingBagHands> hands)
            => new DialogueOption
            {
                Id = gift.OptionId,
                Label = gift.RowLabel,
                ReplyLines = Array.Empty<string>(),
                NothingToSellLines = Array.Empty<string>(),
                ReplyAtPick = () => Give(gift, save, flags, hands != null ? hands() : null),
            };

        /// <summary>Pick the giver's row: hand the supply over (or refuse, changing nothing) and return
        /// the words for it.</summary>
        public static string[] Give(SupplyGiftDef gift, ISaveService save, IFlagStore flags, IShoppingBagHands hands)
        {
            SupplyGiftOutcome outcome = SupplyGiftResponder.TryGive(
                gift, save, hands, gift != null ? FindBag(gift.BagId) : null, flags, SlipDoryStory.HostHere);
            return gift != null ? gift.LinesFor(outcome) : null;
        }

        private static DialogueOption TopicRow(ClueTopicDef topic, string npcId, ISaveService save, IFlagStore flags)
            => new DialogueOption
            {
                Id = topic.OptionId,
                Label = topic.RowLabel,
                ReplyLines = Array.Empty<string>(),
                NothingToSellLines = Array.Empty<string>(),
                ReplyAtPick = () => AskAround(topic, npcId, save, flags),
            };

        /// <summary>Pick the ask-around row: climb the ladders one rung each where this speaker can, set
        /// the flags of what was said, and return the words.</summary>
        public static string[] AskAround(ClueTopicDef topic, string npcId, ISaveService save, IFlagStore flags)
        {
            if (topic == null) return null;
            SaveData data = save?.Current;
            var lines = new List<string>(3);
            var spoken = new List<string>(3);
            ClueLadder.Ask(npcId, topic.Ladders,
                           ladder => ladder.Gift == null || !ladder.Gift.Received(data, flags),
                           key => flags != null && flags.Get(key),
                           topic.FallbackFor(npcId), lines, spoken);
            if (flags != null)
                for (int i = 0; i < spoken.Count; i++) flags.Set(spoken[i], true);
            return lines.ToArray();
        }
    }
}
