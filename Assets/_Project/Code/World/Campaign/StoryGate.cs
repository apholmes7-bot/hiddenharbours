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
        /// <summary>Their ordinary talk, and one line more: the first thing still to do, a word to come
        /// back at the right hour, or a reminder after the word.</summary>
        Hint,
        /// <summary>The word: every fact holds and the hour is right. Heard to its end, it is given.</summary>
        Word,
    }

    /// <summary>The answer of <see cref="StoryGate.Talk"/>.</summary>
    public readonly struct StoryGateTalk
    {
        public readonly StoryGateTalkKind Kind;
        /// <summary>The word's lines, or the one added line. Null for <see cref="StoryGateTalkKind.Normal"/>.</summary>
        public readonly string[] Lines;
        /// <summary>The gate that answered. Null for <see cref="StoryGateTalkKind.Normal"/>.</summary>
        public readonly StoryGateDef Gate;

        public StoryGateTalk(StoryGateTalkKind kind, string[] lines, StoryGateDef gate)
        {
            Kind = kind; Lines = lines; Gate = gate;
        }

        public static StoryGateTalk Normal => default;
    }

    /// <summary>
    /// The rule of a story gate, with no scene, clock or save service in it: given the gate's data,
    /// the saved flags, a reading of the facts, the hour and whether this is a demo, what does a Talk
    /// become and what does the Tasks note say. The same inputs always give the same answer.
    /// </summary>
    public static class StoryGate
    {
        /// <summary>
        /// True while the word is still to be given: the flag the gate waits on (if it names one) is set
        /// and the flag it sets is not. A save that never had the first written never meets the gate; a
        /// save that has the second has heard the word.
        /// </summary>
        public static bool IsLive(StoryGateDef gate, IFlagStore flags)
        {
            if (gate == null || flags == null || string.IsNullOrEmpty(gate.SetsFlag)) return false;
            if (flags.Get(gate.SetsFlag)) return false;
            return string.IsNullOrEmpty(gate.LiveWhileFlag) || flags.Get(gate.LiveWhileFlag);
        }

        /// <summary>True once the word has been given in this save.</summary>
        public static bool IsGiven(StoryGateDef gate, IFlagStore flags)
            => gate != null && flags != null && !string.IsNullOrEmpty(gate.SetsFlag) && flags.Get(gate.SetsFlag);

        /// <summary>
        /// What a Talk with the gate's islander is. The word only when every fact holds AND the hour is
        /// inside the gate's window; with the facts but not the hour, a line to come back; before that,
        /// one line about the FIRST missing fact (the reading's own order), so asking twice gives the
        /// same answer while nothing has changed. After the word the full game adds the authored
        /// reminder until passage opens; the demo uses its own line instead.
        /// </summary>
        public static StoryGateTalk Talk(StoryGateDef gate, IFlagStore flags, in IslandWeekSnapshot facts,
                                         float hourOfDay, bool demo)
        {
            if (IsGiven(gate, flags))
            {
                if (demo) return Added(gate, gate.DemoAfterLine);
                if (!string.IsNullOrWhiteSpace(gate.PendingUntilFlag) && !flags.Get(gate.PendingUntilFlag))
                {
                    string[] lines = gate.PendingDialogue != null ? gate.PendingDialogue.FirstLines : null;
                    if (lines != null && lines.Length > 0)
                        return new StoryGateTalk(StoryGateTalkKind.Hint, lines, gate);
                }
                return StoryGateTalk.Normal;
            }
            if (!IsLive(gate, flags)) return StoryGateTalk.Normal;

            if (facts.AllHold)
            {
                if (!gate.InHours(hourOfDay)) return Added(gate, gate.OutsideHoursLine);
                string[] word = gate.WordLines(demo);
                // A gate with nothing to say stays shut: the flag is only ever set by a word he heard.
                return word == null ? StoryGateTalk.Normal : new StoryGateTalk(StoryGateTalkKind.Word, word, gate);
            }

            return TryFirstMissing(gate, in facts, out StoryGateNote note)
                ? Added(gate, note.SpokenHint)
                : StoryGateTalk.Normal;
        }

        /// <summary>The Tasks note: the first missing fact's, or the gate's own once every fact holds.
        /// Null when the gate is not live or has nothing to say.</summary>
        public static string TaskNote(StoryGateDef gate, IFlagStore flags, in IslandWeekSnapshot facts)
        {
            if (!IsLive(gate, flags)) return null;
            if (facts.AllHold) return string.IsNullOrWhiteSpace(gate.AllHoldNote) ? null : gate.AllHoldNote;
            return TryFirstMissing(gate, in facts, out StoryGateNote note) && !string.IsNullOrWhiteSpace(note.TaskNote)
                ? note.TaskNote
                : null;
        }

        private static StoryGateTalk Added(StoryGateDef gate, string line)
            => string.IsNullOrWhiteSpace(line)
                ? StoryGateTalk.Normal
                : new StoryGateTalk(StoryGateTalkKind.Hint, new[] { line }, gate);

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
    /// Tasks note ask, and the one place a word is given. The facts come through
    /// <see cref="GameServices.IslandWeek"/>, the day and hour from the Core clock, the flags from the
    /// caller's own store.
    ///
    /// <para><b>The word belongs to the household.</b> The flags are the save's, and the save is shared:
    /// whoever talks to her hears the word, and it is given for everyone. Nothing here names a player.
    /// With guests in the game, a guest's <see cref="Give"/> has to be carried to the host, who owns
    /// the save; until there are guests the caller's store IS the host's.</para>
    /// </summary>
    public static class StoryGates
    {
        /// <summary>The Resources folder every gate asset lives in.</summary>
        public const string ResourcesFolder = "StoryGates";

        private static StoryGateDef[] _gates;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _gates = null;
            ListenForMorning();
        }

        /// <summary>Listen once, including after a bus reset. No player state is kept here.</summary>
        public static void ListenForMorning()
        {
            EventBus.Unsubscribe<SleepReachedMorning>(OnMorning);
            EventBus.Subscribe<SleepReachedMorning>(OnMorning);
        }

        /// <summary>Only the host changes shared passage flags. The demo still ends at the word.</summary>
        private static void OnMorning(SleepReachedMorning morning)
        {
            if (DemoBuild.Enabled || (DemoWeek.IsHost != null && !DemoWeek.IsHost())) return;
            ISaveService save = GameServices.Save;
            if (save == null || save.Current == null) return;
            IReadOnlyList<StoryGateDef> all = All;
            for (int i = 0; i < all.Count; i++)
            {
                StoryGateDef gate = all[i];
                if (gate == null || string.IsNullOrWhiteSpace(gate.MorningSetsFlag) ||
                    string.IsNullOrWhiteSpace(gate.SetsFlag) || !save.GetFlag(gate.SetsFlag) ||
                    save.GetFlag(gate.MorningSetsFlag)) continue;
                save.SetFlag(gate.MorningSetsFlag, true);
            }
        }

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

        /// <summary>The first live gate, or null. Two flag reads per gate and no allocation: cheap enough
        /// to ask every frame before paying for a reading of the facts.</summary>
        public static StoryGateDef FirstLive(IFlagStore flags)
        {
            IReadOnlyList<StoryGateDef> all = All;
            for (int i = 0; i < all.Count; i++)
                if (StoryGate.IsLive(all[i], flags)) return all[i];
            return null;
        }

        /// <summary>What a Talk with <paramref name="npcId"/> is, right now. The first of their gates
        /// with anything to say answers.</summary>
        public static StoryGateTalk TalkWith(string npcId, IFlagStore flags)
        {
            if (string.IsNullOrEmpty(npcId) || flags == null) return StoryGateTalk.Normal;
            IReadOnlyList<StoryGateDef> all = All;
            for (int i = 0; i < all.Count; i++)
            {
                StoryGateDef gate = all[i];
                if (gate == null || gate.NpcId != npcId) continue;
                if (!StoryGate.IsLive(gate, flags) && !StoryGate.IsGiven(gate, flags)) continue;
                StoryGateTalk talk = Now(gate, flags);
                if (talk.Kind != StoryGateTalkKind.Normal) return talk;
            }
            return StoryGateTalk.Normal;
        }

        /// <summary>
        /// The word was heard to its end: give it. Asked again here, so a word begun at the last minute
        /// of the window, or under facts that no longer hold, is not given. True when it was. It writes
        /// the gate's flag once and raises <see cref="StoryWordGiven"/> once; a second call does nothing.
        /// </summary>
        public static bool Give(StoryGateDef gate, IFlagStore flags)
        {
            if (gate == null || flags == null) return false;
            if (Now(gate, flags).Kind != StoryGateTalkKind.Word) return false;
            flags.Set(gate.SetsFlag, true);
            EventBus.Publish(new StoryWordGiven(gate.Id, gate.SetsFlag, gate.EndsDemo));
            return true;
        }

        /// <summary>The Tasks note of the first live gate, right now, or null. It reads the facts, which
        /// allocates: a per-frame caller asks <see cref="FirstLive"/> first and keeps the answer.</summary>
        public static string TaskNote(IFlagStore flags)
        {
            StoryGateDef gate = FirstLive(flags);
            if (gate == null) return null;
            IslandWeekSnapshot facts = ReadFacts(out _);
            return StoryGate.TaskNote(gate, flags, in facts);
        }

        private static StoryGateTalk Now(StoryGateDef gate, IFlagStore flags)
        {
            IslandWeekSnapshot facts = ReadFacts(out float hour);
            return StoryGate.Talk(gate, flags, in facts, hour, DemoBuild.Enabled);
        }

        private static IslandWeekSnapshot ReadFacts(out float hourOfDay)
        {
            IGameClock clock = GameServices.Clock;
            hourOfDay = clock != null ? clock.HourOfDay : 0f;
            return GameServices.IslandWeek.Read(GameServices.Save?.Current, clock != null ? clock.DayIndex : 0);
        }
    }
}
