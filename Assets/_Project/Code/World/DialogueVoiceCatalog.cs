using System.Collections.Generic;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>Voice id → cadence, for the one path that cannot carry the asset.</b>
    ///
    /// <para><b>Why it has to exist.</b> A modal conversation resolves its voice by walking an asset
    /// reference (<c>NpcDef.Voice</c> → <see cref="DialogueVoiceDef"/>) and never needs an id. Ambient
    /// speech cannot: the request crosses Core (<c>AmbientSpeechRequested</c>), and Core may not name a
    /// World type (CLAUDE.md rule 4), so what travels is a string. This is the only place that string is
    /// turned back into numbers.</para>
    ///
    /// <para><b>A session index, NOT a content source.</b> Nothing here loads anything. Whoever holds the
    /// asset — the inner-voice library on wake, a region's NPC roster, a fixture — registers it, and the
    /// registration is a copy of the def's already-laundered cadence. So there is no second answer to
    /// "what does this character sound like": the asset is still the only authored fact (rule 2), and an
    /// id nobody registered resolves to <see cref="DialogueVoice.Default"/> rather than to silence or a
    /// stalled bubble — the same forgiving failure <see cref="DialogueVoiceDef.VoiceOf"/> already
    /// chooses.</para>
    ///
    /// <para><b>Static, and that is the same bargain <c>StandableSurfaces</c> makes.</b> The registrant
    /// and the reader are in different objects with different lifetimes and neither may hold the other.
    /// Re-registering an id overwrites it (an asset re-imported, a fixture re-run);
    /// <see cref="Clear"/> is for test teardown, so one fixture's voices cannot leak into the next.</para>
    /// </summary>
    public static class DialogueVoiceCatalog
    {
        private static readonly Dictionary<string, DialogueVoice> _voices =
            new Dictionary<string, DialogueVoice>(8, System.StringComparer.Ordinal);

        /// <summary>How many ids are known. Zero is a legal, working state — everything speaks at the
        /// default cadence.</summary>
        public static int Count => _voices.Count;

        /// <summary>
        /// Make <paramref name="def"/>'s cadence findable by its id. A null def, or one with no id, is
        /// ignored rather than throwing: a library with a hole in it should cost a default voice, not a
        /// broken wake-up.
        /// </summary>
        public static void Register(DialogueVoiceDef def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.Id)) return;
            _voices[def.Id] = def.Resolved;
        }

        /// <summary>Register a cadence directly, under <paramref name="voiceId"/> — the seam a fixture
        /// uses to get a slow voice without authoring an asset for it.</summary>
        public static void Register(string voiceId, in DialogueVoice voice)
        {
            if (string.IsNullOrWhiteSpace(voiceId)) return;
            _voices[voiceId] = voice.Sanitised();
        }

        /// <summary>
        /// The cadence registered under <paramref name="voiceId"/>, or
        /// <see cref="DialogueVoice.Default"/> when the id is null, empty or unknown. Never throws and
        /// never returns something a bubble cannot finish filling.
        /// </summary>
        public static DialogueVoice VoiceOf(string voiceId)
        {
            if (!string.IsNullOrWhiteSpace(voiceId) && _voices.TryGetValue(voiceId, out DialogueVoice v))
                return v;
            return DialogueVoice.Default;
        }

        /// <summary>True when <paramref name="voiceId"/> has been registered. For content checks that
        /// want to say WHICH id is missing rather than silently getting the default.</summary>
        public static bool Knows(string voiceId)
            => !string.IsNullOrWhiteSpace(voiceId) && _voices.ContainsKey(voiceId);

        /// <summary>Forget every registered voice (test teardown, and a full session restart).</summary>
        public static void Clear() => _voices.Clear();
    }
}
