using System;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The single home for every player-facing string the world layer shows — NPC conversations,
    /// interaction prompts, and the onboarding nudges (VS-21). Same convention as
    /// <c>HudStrings</c>: there is no runtime localization system wired yet (a cross-cutting
    /// lead-architect call), so centralising the copy here is the seam — when loc tables land, these
    /// accessors route to them and no call site changes. Flagged as a known DoD gap in the PR.
    ///
    /// Voice: warm, Maritime, skimmable. Short lines that read in a beat (design/world tone, P5
    /// "cozy but with teeth" — bittersweet about Ned, hopeful about the dory).
    /// </summary>
    public static class WorldStrings
    {
        // ---- speaker names ------------------------------------------------------------------
        // Cove (legacy stand-in) speakers.
        public const string GinnyName     = "Aunt Ginny";
        public const string NeighbourName = "Bram Tully";
        public const string LogbookName   = "Ned's Logbook";
        // St Peters opening speakers. Ned is now a remembered presence (his LETTER on the island), not an
        // inherited boat — the dory is bought and repaired (canon §5.8). Names mirror the NpcDef assets.
        public const string NedLetterName = "Ned's Letter";

        // ---- conversation ids (stable; used to look copy up) --------------------------------
        public const string ConvoGinny     = "ginny";
        public const string ConvoNeighbour = "neighbour";
        public const string ConvoLogbook   = "logbook";

        // ---- prompts ------------------------------------------------------------------------
        /// <summary>⚠ The interact key's letter, kept ONLY for <see cref="ContinueHint"/> — the one place a
        /// key is still named, and it is named inside the dialogue bubble rather than floating over the
        /// world. Nothing else may spell a key at the player: the 2026-08-19 ruling retired every
        /// "E: …" surface in favour of the interaction popup, which names the ACTION.</summary>
        public const string InteractKey  = "E";
        public const string ContinueHint = "E ▸";   // "press E to continue", shown on the bubble

        /// <summary>
        /// The always-last row of every option bubble — the Animal Crossing convention, made a guarantee
        /// by <see cref="DialogueOptionPicker.RowsFor"/> rather than left to content: there is exactly one
        /// safe way out of a conversation and it is always in the same place, so a player never has to
        /// read four rows to find the exit.
        /// </summary>
        public const string CloseConversationOption = "See you later.";

        /// <summary>The cursor drawn against the selected option row. ⚑ OWNER TASTE — the art lane's
        /// bubble kit may replace this glyph with a drawn motif; one const, one edit.</summary>
        public const string OptionCursor = "▸ ";

        /// <summary>
        /// The one line the interaction popup shows for an interactable you are facing — "Talk — Aunt
        /// Ginny", "Read — Ned's Letter".
        ///
        /// <para><b>It no longer names the key</b> (the owner's 2026-08-19 ruling). There is one interact
        /// key; a floating "E: …" over the world was the plain-text habit the popup replaced, and the
        /// popup's whole job is to say what is on offer, not which button to hunt for. The em dash carries
        /// the verb/subject split without punctuation the eye has to parse — the same shape the shell's
        /// value rows use.</para>
        ///
        /// <para><b>⚠ This allocates</b>, and it is on the offer path, which is read every frame there is a
        /// candidate — so <c>WorldInteractor</c> caches the result per speaker rather than calling it per
        /// frame (rule 7). See <c>WorldInteractor.OfferLabelFor</c>.</para>
        /// </summary>
        public static string OfferLabel(InteractKind kind, string who)
        {
            string verb = kind == InteractKind.Read ? ReadVerb : TalkVerb;
            return string.IsNullOrEmpty(who) ? verb : verb + LabelSeparator + who;
        }

        /// <summary>The popup's verb for a person. ⚑ OWNER TASTE — one const, one edit.</summary>
        public const string TalkVerb = "Talk";

        /// <summary>The popup's verb for a thing you read — a letter, a logbook.</summary>
        public const string ReadVerb = "Read";

        /// <summary>What sits between the verb and who/what it is about.</summary>
        public const string LabelSeparator = " — ";

        // ---- conversations ------------------------------------------------------------------
        // Each returns the lines for a single-speaker exchange. `metBefore` gives a shorter, warmer
        // repeat once you've met them, so the full inheritance opening plays only the first time.

        public static string[] Conversation(string id, bool metBefore) => id switch
        {
            ConvoGinny     => metBefore ? GinnyAgain  : GinnyFirst,
            ConvoNeighbour => Neighbour,
            ConvoLogbook   => metBefore ? LogbookAgain : LogbookFirst,
            _              => Array.Empty<string>()
        };

        private static readonly string[] GinnyFirst =
        {
            "Oh, child — you came. I'm so glad. Your uncle Ned thought the world of you, you know.",
            "He's gone to the deep now, rest him. But he left you his dory — she's moored at the dock's end.",
            "She's small, and she's stubborn, but she's honest. Ned hand-lined a living from her for forty years.",
            "Go on — take her out past the dock and drop a line. See what bites. The sea will teach you the rest.",
            "When your hold's full, bring it back and sell at the wharf. We'll make a fisher of you yet."
        };

        private static readonly string[] GinnyAgain =
        {
            "Back already? Good. The cove's kind today — take Ned's dory out and wet a line.",
            "Sell what you land at the wharf. Little by little, that's how a harbour gets built."
        };

        private static readonly string[] LogbookFirst =
        {
            "Ned's logbook lies open on the sill — the last pages, in a careful, weathered hand.",
            "\"Squally out of the sou'west. Came home light. Some days the sea keeps her own.\"",
            "\"The young one's coming to the island this spring. I've kept the dory sound for them.\"",
            "\"If you're reading this — she's yours now. Mind the tide, watch the sky, and don't fish angry.\"",
            "\"There's a whole coast out there waiting. Go and meet it. — Ned\""
        };

        private static readonly string[] LogbookAgain =
        {
            "Ned's logbook. \"Mind the tide, watch the sky, and go and meet the coast. — Ned\""
        };

        private static readonly string[] Neighbour =
        {
            "Mornin'. You'll be Ned's kin — you've the look of him about the eyes.",
            "He mended my nets more times than I can count and never once took a coin for it. Good man.",
            "Anything wants hauling or hammering, you give Bram a shout. Welcome to the cove."
        };

        // ---- onboarding nudges — the St Peters BUY-AND-REPAIR opening (one gentle line per step) ----
        // The new arc (canon §5.8): dig CLAMS at low water → walk the bared sandbar to Nine Mile Creek → buy a
        // cod LICENCE + a ROD, sell clams, save → BUY the damaged dory + PAY to REPAIR her → sail home to
        // Coddle Cove. The dory is EARNED, never inherited. Each line points at the next beat; the director
        // (OnboardingDirector) picks which one to show from the opening flags + Core signals.
        public const string OnboardTalkGinny = "Find Aunt Ginny on the island and press E — she'll set you straight.";
        public const string OnboardDigClams  = "Wait for the tide to drop, then dig clams on the bared flats (E by a hole).";
        public const string OnboardCrossBar  = "Walk the bared sandbar east to Nine Mile Creek before the tide floods it back.";
        public const string OnboardBuyLicence= "At Nine Mile Creek: sell your clams, then buy a COD LICENCE to fish.";
        public const string OnboardBuyRod    = "Buy a fishing ROD from the shop — then you can hand-line cod.";
        public const string OnboardBuyDory   = "Save up, then BUY the damaged dory at the Nine Mile Creek shipwright.";
        public const string OnboardRepairDory= "She's yours but broken — pay the shipwright to REPAIR her.";
        public const string OnboardSailHome  = "Seaworthy at last — sail your dory home to Coddle Cove.";
        public const string OnboardDone      = "You earned her, plank and rope. Fair winds, skipper!";
    }
}
