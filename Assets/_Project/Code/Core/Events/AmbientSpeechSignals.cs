using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// What KIND of unprompted speech a bubble is carrying. The owner's 2026-09-06 ruling puts two
    /// things down one channel — <i>"npcs to engage in conversation with each other and the
    /// conversations to be visible bubbles"</i> and <i>"these speech bubbles can also be used by the
    /// player to narrate their internal dialogue"</i> — so the kind is a field on ONE signal rather than
    /// two parallel systems that would drift apart the first time the bubble's look changed.
    /// </summary>
    public enum AmbientSpeechKind
    {
        /// <summary>One villager saying something to another. Drawn exactly like the modal bubble the
        /// player gets when they press Talk, because it is the same coast talking.</summary>
        NpcToNpc = 0,

        /// <summary>The player's own thought — <i>"approaching a broken item and saying 'I could fix
        /// this' or other clues to the player"</i>. Anchored at her, and visibly hers: see
        /// <c>AmbientSpeechPresenter</c> for the owner's 2026-09-06 ruling on how (tailless, and a shade
        /// cooler than a spoken bubble).</summary>
        InnerVoice = 1,
    }

    /// <summary>Why an ambient bubble stopped being on screen. A conversation director needs the
    /// difference: a line that <see cref="Dwelled"/> is the cue to speak the next one, while one that was
    /// <see cref="Displaced"/> or <see cref="Cancelled"/> means the exchange is over and the next line
    /// must NOT follow it into a bubble that is no longer there.</summary>
    public enum AmbientSpeechEndReason
    {
        /// <summary>It filled, it stood for its voice's read pause, and it closed on its own. The ONLY
        /// ending that means the line was delivered.</summary>
        Dwelled = 0,

        /// <summary>The pool was full and this was the oldest bubble in it — a newer line took its slot.
        /// See <c>AmbientSpeechPresenter.MaxConcurrent</c> for why a cap exists at all.</summary>
        Displaced = 1,

        /// <summary>Something took the speaker away mid-line: the player pressed Talk on them (the modal
        /// wins), the anchor was destroyed, or the region unloaded.</summary>
        Cancelled = 2,
    }

    /// <summary>
    /// <b>SOMEBODY IS SAYING SOMETHING NOBODY ASKED THEM TO</b> — the ambient half of the speech bubble,
    /// ruled by the owner on 2026-09-06: two villagers talk to each other where you can see it, and the
    /// same bubble carries the player's inner voice as a clue channel.
    ///
    /// <para><b>The law this signal exists to keep.</b> An ambient bubble takes NOTHING from the player:
    /// <i>"the player can still walk and run as normal"</i>. So this is a publish, never a call — the
    /// presenter draws it, dwells it and closes it by itself, and no press is involved anywhere. It never
    /// raises <see cref="InteractionGate"/> and never claims the move axis; the modal Talk conversation
    /// keeps both of those and its one slot. That is a TEST, not a comment.</para>
    ///
    /// <para><b>Why it crosses through Core.</b> The publishers are everywhere — World routines, the
    /// player's own approach to a thing, later Fishing and Boats — and the presenter that draws it is one
    /// component in World. None of them may reference each other (CLAUDE.md rule 4), so the request is
    /// data on the bus, exactly as <see cref="ArrivalCompleted"/> already carries a line and a
    /// <see cref="Transform"/> across the same seam.</para>
    ///
    /// <para><b>A publish with no presenter is silence, and that is the point.</b> A region built before
    /// the bubble kit imported, a headless fixture, an EditMode check — none of them have a canvas, and
    /// nothing about the caller's own work depends on the bubble having been drawn.</para>
    /// </summary>
    public readonly struct AmbientSpeechRequested
    {
        /// <summary>This line's handle, minted by <see cref="AmbientSpeechId.Next"/>. It comes back on
        /// <see cref="AmbientSpeechEnded"/>, which is how a caller with several lines in flight knows
        /// WHICH one finished. Ids are unique within a session and mean nothing across one.</summary>
        public readonly int Id;

        /// <summary>Who is talking, in the world — the bubble hangs over them and tracks them as they
        /// walk, so a refused stop still reads (the bubble travels with them). Null parks the bubble at
        /// the screen's speaking position, the same fallback the modal bubble has.</summary>
        public readonly Transform Anchor;

        /// <summary>Head height (and any lateral nudge) above <see cref="Anchor"/>, world metres. Zero is
        /// legal and hangs the bubble off the anchor's own origin.</summary>
        public readonly Vector3 AnchorOffset;

        /// <summary>The speaker's stable id (an <c>NpcDef.Id</c>), or null. Rides
        /// <see cref="DialogueTypewriterTick"/> so the audio lane can key a timbre off an ambient line
        /// exactly as it does off a modal one — an overheard voice should not become anonymous just
        /// because you did not press anything.</summary>
        public readonly string SpeakerId;

        /// <summary>The speaker's display name, or null. Drawn on the bubble's shoulder chip under the
        /// same rule the modal uses (a name AND the gold art, else nothing), and never for an
        /// <see cref="AmbientSpeechKind.InnerVoice"/> line, which has no speaker but her.</summary>
        public readonly string SpeakerName;

        /// <summary>What is said. Authored content in every case (rule 2) — never composed at the call
        /// site. Empty is a no-op rather than an empty bubble.</summary>
        public readonly string Line;

        /// <summary>The <c>DialogueVoiceDef.Id</c> to fill at, or null for the default cadence. Resolved
        /// on the World side, so Core carries an id and never a voice.</summary>
        public readonly string VoiceId;

        /// <summary>Overheard speech, or the player's own thought.</summary>
        public readonly AmbientSpeechKind Kind;

        public AmbientSpeechRequested(int id, Transform anchor, Vector3 anchorOffset, string speakerId,
                                      string speakerName, string line, string voiceId,
                                      AmbientSpeechKind kind)
        {
            Id = id;
            Anchor = anchor;
            AnchorOffset = anchorOffset;
            SpeakerId = speakerId;
            SpeakerName = speakerName;
            Line = line;
            VoiceId = voiceId;
            Kind = kind;
        }
    }

    /// <summary>
    /// <b>An ambient bubble is off the screen</b> — published by the presenter for every request it
    /// accepted, exactly once, whatever ended it.
    ///
    /// <para>This is the beat a multi-line exchange is sequenced on: a conversation director speaks line
    /// 1, waits for this, then speaks line 2 from the other villager. Sequencing on a timer computed at
    /// the call site would be a second copy of <c>AmbientDwell</c> that goes stale the day somebody
    /// re-tunes a voice.</para>
    ///
    /// <para><b>Every accepted request ends.</b> A displaced or cancelled bubble publishes too — see
    /// <see cref="AmbientSpeechEndReason"/> — so a waiting caller can never hang on a line that was
    /// quietly dropped. A request the presenter REFUSED (an empty line, no presenter in the scene at
    /// all) publishes nothing, because nothing was ever accepted.</para>
    /// </summary>
    public readonly struct AmbientSpeechEnded
    {
        /// <summary>The <see cref="AmbientSpeechRequested.Id"/> this closes.</summary>
        public readonly int Id;

        /// <summary>Whether the line was delivered, or taken off the screen.</summary>
        public readonly AmbientSpeechEndReason Reason;

        /// <summary>True only when the line was read to the end — the one thing a sequencing caller
        /// should branch on.</summary>
        public bool Delivered => Reason == AmbientSpeechEndReason.Dwelled;

        public AmbientSpeechEnded(int id, AmbientSpeechEndReason reason)
        {
            Id = id;
            Reason = reason;
        }
    }

    /// <summary>
    /// The one place ambient speech ids are minted. A plain counter rather than a GUID or a hash of the
    /// line: ids are compared, never stored, never saved and never shown, so the cheapest thing that
    /// cannot collide within a session is the right thing.
    ///
    /// <para>Starts at 1, so <c>default(AmbientSpeechRequested).Id</c> — zero — is never a live handle.
    /// <see cref="Reset"/> exists for test fixtures that want a session to start from a known place; the
    /// game never calls it.</para>
    /// </summary>
    public static class AmbientSpeechId
    {
        private static int _next;

        /// <summary>The next unused id. Never zero.</summary>
        public static int Next() => ++_next;

        /// <summary>Start counting again from 1 (fixtures only).</summary>
        public static void Reset() => _next = 0;
    }
}
