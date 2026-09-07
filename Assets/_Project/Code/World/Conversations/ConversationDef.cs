using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// One turn in a two-hander: who says it, what they say, and in whose voice.
    ///
    /// <para>The speaker is an INDEX into <see cref="ConversationDef.Participants"/> rather than an id,
    /// so a line cannot name somebody who is not in the conversation, and re-casting an exchange is one
    /// edit at the top instead of one per line.</para>
    /// </summary>
    [Serializable]
    public struct ConversationLine
    {
        [Tooltip("Which participant speaks this line — 0 or 1, indexing ConversationDef.Participants. " +
                 "Out of range and the line is skipped rather than spoken by nobody.")]
        [Min(0)] public int SpeakerIndex;

        [TextArea(2, 3)]
        [Tooltip("What they say. One bubble's worth.")]
        public string Text;

        [Tooltip("The cadence this line fills at. Leave empty and it reads at the speaker's own " +
                 "NpcDef voice, or the default if they have none.")]
        public DialogueVoiceDef Voice;
    }

    /// <summary>
    /// <b>TWO VILLAGERS TALKING, AS DATA</b> — the owner's 2026-09-06 ruling: <i>"I also want npcs to
    /// engage in conversation with each other and the conversations to be visible bubbles as they
    /// talk"</i>.
    ///
    /// <para>One asset per exchange under <c>Data/NPCs/Conversations</c>, keyed by a stable append-only
    /// <see cref="Id"/> (<c>conversation.snake_case</c>), gathered by <see cref="ConversationLibrary"/>
    /// and run by <see cref="NpcConversationDirector"/>. Adding an exchange is a new asset and nothing
    /// else — no code, no scene edit, no change to anybody's routine (rule 2).</para>
    ///
    /// <para><b>Meeting is where two routines ALREADY put two people.</b> This def does not schedule
    /// anybody's day and does not move anybody: it names a place and an hour window, and the director
    /// starts the exchange only if both participants happen to be standing near that place inside it.
    /// Nothing here can make a villager late, take a detour, or be somewhere the clock does not put them
    /// — a routine stays a pure function of the clock (rule 5) and this is an observation of one, never
    /// an input to it.</para>
    ///
    /// <para><b>What it deliberately is NOT.</b> No NPC-to-NPC awareness beyond "both at the station" —
    /// no avoidance, no relationships, no memory of who spoke to whom. Those are unshipped by design
    /// (<c>npcs-and-routines.md</c> §2.3/§5) and a conversation system that quietly grew them would be
    /// the wrong place for it to happen.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Conversation", fileName = "Conversation")]
    public sealed class ConversationDef : ScriptableObject
    {
        /// <summary>How many people are in a two-hander. Named because three of the guards below and
        /// two of the tests read it, and "2" scattered across them would be a number nobody could
        /// change.</summary>
        public const int ParticipantCount = 2;

        [Header("Identity")]
        [Tooltip("Stable id, append-only (conversation.snake_case, e.g. " +
                 "conversation.st_peters.store_midday). Content-validated for uniqueness, and it seeds " +
                 "the minute this exchange starts at — so renaming one moves it.")]
        public string Id = "conversation.example";

        [Tooltip("The two who talk, in order. Line speaker indices point at this array. Exactly two — a " +
                 "one-sided conversation is a line, and three is a crowd this system does not model.")]
        public NpcDef[] Participants = Array.Empty<NpcDef>();

        [Tooltip("What is said, in order, alternating or not as the writing wants. Each line waits for " +
                 "the one before it to be READ, not for a timer — see NpcConversationDirector.")]
        public ConversationLine[] Lines = Array.Empty<ConversationLine>();

        [Header("Where they meet")]
        [Tooltip("Which region this exchange belongs to (region.snake_case). Station positions are " +
                 "region-local, so without this a station id could resolve somewhere else entirely.")]
        public string RegionId = "";

        [Tooltip("The station they meet AT (a station id from the region's own table, e.g. " +
                 "station.st_peters.store_counter). It is the MEETING POINT, not necessarily either " +
                 "person's own station — two people at neighbouring spots in the same shop are still " +
                 "near the counter.")]
        public string StationId = "";

        [Tooltip("How near that station each of them must be, in metres, for the exchange to start. " +
                 "3 m is the shipped figure: it spans the counter and its customer slots, and the green " +
                 "and its places, without reaching across the village.")]
        [Min(0.5f)] public float RadiusMetres = 3f;

        [Header("When")]
        [Tooltip("The earliest game hour (0..24) this may start.")]
        [Range(0f, 24f)] public float EarliestHour = 12f;

        [Tooltip("The latest game hour (0..24) this may start. The exchange begins at a seeded minute " +
                 "INSIDE this window — same world seed and same day, same minute, every time.")]
        [Range(0f, 24f)] public float LatestHour = 13f;

        [Header("How often")]
        [Tooltip("OFF: they have this exchange once per day at most. ON: it may come round again, no " +
                 "sooner than the gap below.")]
        public bool Repeatable = false;

        [Tooltip("Repeatable exchanges only: the shortest gap before this may run again, in GAME " +
                 "seconds. Game seconds, not real ones, so it follows the owner's day-length knob.")]
        [Min(0f)] public float GapSeconds = 3600f;

        /// <summary>True when this def has everything the director needs. A def that fails this is
        /// skipped at load with no error — content validation is where an author is told.</summary>
        public bool IsRunnable =>
            !string.IsNullOrWhiteSpace(Id) &&
            Participants != null && Participants.Length == ParticipantCount &&
            Participants[0] != null && Participants[1] != null &&
            Participants[0] != Participants[1] &&
            Lines != null && Lines.Length > 0 &&
            !string.IsNullOrWhiteSpace(RegionId) &&
            !string.IsNullOrWhiteSpace(StationId);

        /// <summary>The npc id of a participant, or null when the index is out of range.</summary>
        public string ParticipantId(int index)
            => Participants != null && index >= 0 && index < Participants.Length &&
               Participants[index] != null
                ? Participants[index].Id
                : null;

        /// <summary>
        /// The voice id line <paramref name="lineIndex"/> should fill at: the line's own voice when it
        /// names one, else the speaking participant's <c>NpcDef.Voice</c>, else null for the default
        /// cadence. Null is always safe — it means "the default", never "silent".
        /// </summary>
        public string VoiceIdFor(int lineIndex)
        {
            if (Lines == null || lineIndex < 0 || lineIndex >= Lines.Length) return null;

            ConversationLine line = Lines[lineIndex];
            if (line.Voice != null) return line.Voice.Id;

            NpcDef speaker = SpeakerOf(lineIndex);
            return speaker != null && speaker.Voice != null ? speaker.Voice.Id : null;
        }

        /// <summary>Who speaks line <paramref name="lineIndex"/>, or null when the line's speaker index
        /// does not name a participant (which is how a mis-authored line is skipped rather than put in
        /// somebody else's mouth).</summary>
        public NpcDef SpeakerOf(int lineIndex)
        {
            if (Lines == null || lineIndex < 0 || lineIndex >= Lines.Length) return null;
            int who = Lines[lineIndex].SpeakerIndex;
            return Participants != null && who >= 0 && who < Participants.Length
                ? Participants[who]
                : null;
        }
    }
}
