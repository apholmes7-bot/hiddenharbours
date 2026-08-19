using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The opening has put the player ashore.</b> Published the instant control comes back on the
    /// dock, carrying the one thing the skipper leaves them with.
    ///
    /// <para><b>Why it crosses through Core.</b> The sequence lives in <c>App</c> (it holds a boat, a
    /// player and a save) and the speech bubble lives in <c>World</c>, and neither may reference the
    /// other (CLAUDE.md rule 4). So the arrival says WHAT is said and WHO says it; the World side owns
    /// what a said thing looks like — exactly the split <see cref="DialogueTypewriterTick"/> already
    /// makes in the other direction.</para>
    ///
    /// <para><b>And why it is a signal rather than a call.</b> The opening must work in a scene with no
    /// dialogue view at all — a PlayMode fixture, a headless check, a region built before the bubble kit
    /// imported. A publish nobody has subscribed to is silence; a call into a missing presenter is a
    /// null. The arrival is finished either way, which is the property that matters: the player is
    /// ashore with the controls whether or not anyone drew a bubble.</para>
    /// </summary>
    public readonly struct ArrivalCompleted
    {
        /// <summary>What the skipper says as the player steps off. Authored on the region (and the
        /// owner's to veto) — never composed here.</summary>
        public readonly string Line;

        /// <summary>Who says it, in the world — the bubble hangs over them and its tail points at them.
        /// Null is legal and parks the bubble at the screen's speaking position.</summary>
        public readonly Transform Speaker;

        /// <summary>The speaker's display name, for the line's attribution.</summary>
        public readonly string SpeakerName;

        public ArrivalCompleted(string line, Transform speaker, string speakerName)
        {
            Line = line;
            Speaker = speaker;
            SpeakerName = speakerName;
        }
    }
}
