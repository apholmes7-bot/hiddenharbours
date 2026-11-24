using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>What a Talk with a gate's islander turns out to be.</summary>
    public enum StoryGateTalkKind
    {
        /// <summary>Their ordinary talk; the gate has nothing to add.</summary>
        Normal,
        /// <summary>Their ordinary talk, and one line about the first thing still to do.</summary>
        Hint,
        /// <summary>The word: every fact holds. Heard to its end, it sets the gate's flag.</summary>
        Word,
    }

    /// <summary>The answer of <see cref="StoryGate.Talk"/>.</summary>
    public readonly struct StoryGateTalk
    {
        public readonly StoryGateTalkKind Kind;
        /// <summary>The word's lines, or the one hint line. Null for <see cref="StoryGateTalkKind.Normal"/>.</summary>
        public readonly string[] Lines;
        /// <summary>The flag the word sets. Null unless this is the word.</summary>
        public readonly string SetsFlag;

        public StoryGateTalk(StoryGateTalkKind kind, string[] lines, string setsFlag)
        {
            Kind = kind; Lines = lines; SetsFlag = setsFlag;
        }

        public static StoryGateTalk Normal => default;
    }

    /// <summary>
    /// The rule of a story gate, with no scene, clock or save service in it: given the gate's data,
    /// the saved flags and a reading of the facts, what does a Talk become and what does the Tasks
    /// note say.
    /// </summary>
    public static class StoryGate
    {
        /// <summary>
        /// True while the gate has work to do: the flag it waits on (if it names one) is set and the flag
        /// it sets is not. A save that never had the first written never meets the gate; a save that has
        /// the second is past it for good.
        /// </summary>
        public static bool IsLive(StoryGateDef gate, IFlagStore flags)
        {
            if (gate == null || flags == null || string.IsNullOrEmpty(gate.SetsFlag)) return false;
            if (flags.Get(gate.SetsFlag)) return false;
            return string.IsNullOrEmpty(gate.LiveWhileFlag) || flags.Get(gate.LiveWhileFlag);
        }

        /// <summary>
        /// What a Talk with the gate's islander is. The word only when every fact holds; before that,
        /// one line about the FIRST missing fact (the reading's own order), so asking twice gives the
        /// same answer while nothing has changed.
        /// </summary>
        public static StoryGateTalk Talk(StoryGateDef gate, IFlagStore flags, in IslandWeekSnapshot facts)
        {
            if (!IsLive(gate, flags)) return StoryGateTalk.Normal;

            if (facts.AllHold)
            {
                string[] word = gate.Word != null ? gate.Word.FirstLines : null;
                // A gate with nothing to say stays shut: the flag is only ever set by a word he heard.
                if (word == null || word.Length == 0) return StoryGateTalk.Normal;
                return new StoryGateTalk(StoryGateTalkKind.Word, word, gate.SetsFlag);
            }

            return TryFirstMissing(gate, in facts, out StoryGateNote note) && !string.IsNullOrWhiteSpace(note.SpokenHint)
                ? new StoryGateTalk(StoryGateTalkKind.Hint, new[] { note.SpokenHint }, null)
                : StoryGateTalk.Normal;
        }

        /// <summary>The Tasks note for the first missing fact, or null (not live, all hold, or no row).</summary>
        public static string TaskNote(StoryGateDef gate, IFlagStore flags, in IslandWeekSnapshot facts)
        {
            if (!IsLive(gate, flags) || facts.AllHold) return null;
            return TryFirstMissing(gate, in facts, out StoryGateNote note) && !string.IsNullOrWhiteSpace(note.TaskNote)
                ? note.TaskNote
                : null;
        }

        private static bool TryFirstMissing(StoryGateDef gate, in IslandWeekSnapshot facts, out StoryGateNote note)
        {
            IReadOnlyList<string> missing = facts.MissingTextKeys;
            if (missing.Count > 0) return gate.TryNote(missing[0], out note);
            note = default;
            return false;
        }
    }

    /// <summary>
    /// The shipped story gates and the live reading behind them: the one place the talk and the
    /// Tasks note ask. The facts come through <see cref="GameServices.IslandWeek"/>, the day from the
    /// Core clock, the flags from the caller's own store.
    /// </summary>
    public static class StoryGates
    {
        /// <summary>The Resources folder every gate asset lives in.</summary>
        public const string ResourcesFolder = "StoryGates";

        private static StoryGateDef[] _gates;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _gates = null;

        /// <summary>Every gate, in id order.</summary>
        public static IReadOnlyList<StoryGateDef> All
        {
            get
            {
                if (_gates == null) Take(Resources.LoadAll<StoryGateDef>(ResourcesFolder));
                return _gates;
            }
        }

        /// <summary>Replace the gates (fixtures). Null puts the shipped set back on the next read.</summary>
        public static void Load(params StoryGateDef[] gates)
        {
            if (gates == null) _gates = null;
            else Take((StoryGateDef[])gates.Clone());
        }

        private static void Take(StoryGateDef[] gates)
        {
            System.Array.Sort(gates, (a, b) => string.CompareOrdinal(a != null ? a.Id : null, b != null ? b.Id : null));
            _gates = gates;
        }

        /// <summary>The live gate carried by <paramref name="npcId"/>'s talk, or null.</summary>
        public static StoryGateDef LiveFor(string npcId, IFlagStore flags)
        {
            if (string.IsNullOrEmpty(npcId)) return null;
            IReadOnlyList<StoryGateDef> all = All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].NpcId == npcId && StoryGate.IsLive(all[i], flags)) return all[i];
            return null;
        }

        /// <summary>The first live gate, or null. Two flag reads per gate and no allocation: cheap enough
        /// to ask every frame before paying for a reading of the facts.</summary>
        public static StoryGateDef FirstLive(IFlagStore flags)
        {
            IReadOnlyList<StoryGateDef> all = All;
            for (int i = 0; i < all.Count; i++)
                if (StoryGate.IsLive(all[i], flags)) return all[i];
            return null;
        }

        /// <summary>What a Talk with <paramref name="npcId"/> is, right now.</summary>
        public static StoryGateTalk TalkWith(string npcId, IFlagStore flags)
        {
            StoryGateDef gate = LiveFor(npcId, flags);
            if (gate == null) return StoryGateTalk.Normal;
            IslandWeekSnapshot facts = ReadFacts();
            return StoryGate.Talk(gate, flags, in facts);
        }

        /// <summary>The Tasks note of the first live gate, right now, or null. It reads the facts, which
        /// allocates: a per-frame caller asks <see cref="FirstLive"/> first and keeps the answer.</summary>
        public static string TaskNote(IFlagStore flags)
        {
            StoryGateDef gate = FirstLive(flags);
            if (gate == null) return null;
            IslandWeekSnapshot facts = ReadFacts();
            return StoryGate.TaskNote(gate, flags, in facts);
        }

        private static IslandWeekSnapshot ReadFacts()
        {
            IGameClock clock = GameServices.Clock;
            return GameServices.IslandWeek.Read(GameServices.Save?.Current, clock != null ? clock.DayIndex : 0);
        }
    }
}
