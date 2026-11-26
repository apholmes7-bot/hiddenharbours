namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>An islander's word has been given.</b> Raised once, the step the word was heard to its end and
    /// its flag was written to the household's saved flags. It is a fact about the HOUSEHOLD, not about
    /// whoever was standing there: it names no player, and everything that follows from it (a way
    /// opening, a demo ending) is the same for everyone in the game.
    ///
    /// <para>Never raised on load. A save that already holds the flag is simply past the word; a
    /// listener that needs to know asks the flag.</para>
    /// </summary>
    public readonly struct StoryWordGiven
    {
        /// <summary>The gate whose word it was (story_gate.snake_case).</summary>
        public readonly string GateId;
        /// <summary>The saved flag the word set.</summary>
        public readonly string Flag;
        /// <summary>True when the gate's data says a demo ends at this word. The full game ignores it.</summary>
        public readonly bool EndsDemo;

        public StoryWordGiven(string gateId, string flag, bool endsDemo)
        {
            GateId = gateId ?? string.Empty;
            Flag = flag ?? string.Empty;
            EndsDemo = endsDemo;
        }
    }
}
