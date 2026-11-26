using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>One person's line for one rung of a clue ladder.</summary>
    [System.Serializable]
    public struct ClueHint
    {
        [Tooltip("Who says it (npc.snake_case).")]
        public string NpcId;

        [Tooltip("What they say.")]
        [TextArea] public string Line;
    }

    /// <summary>One rung: the flag that records it was heard, and who can say it.</summary>
    [System.Serializable]
    public struct ClueRung
    {
        [Tooltip("Stable, append-only save-flag key set when this rung is spoken.")]
        public string Flag;

        [Tooltip("Who holds this rung, and their words.")]
        public ClueHint[] Hints;
    }

    /// <summary>
    /// <b>Three rungs of hints toward one supply's giver.</b> Rung 1 says only that somebody has it, rung 2
    /// narrows it to a kind of person or a part of the island, rung 3 names the person and the place. A
    /// rung is spoken only once the rung below it has been heard (from anybody), so the island leads the
    /// player in, one person at a time. <see cref="ClueLadder"/> holds the rule.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Supplies/Clue Ladder", fileName = "ClueLadder")]
    public sealed class ClueLadderDef : ScriptableObject
    {
        [Tooltip("Stable, append-only id (clueladder.snake_case).")]
        public string Id = "clueladder.example";

        [Tooltip("The supply this ladder leads to. While the player has it, the ladder is silent.")]
        public SupplyGiftDef Gift;

        [Tooltip("The rungs, lowest first.")]
        public ClueRung[] Rungs = new ClueRung[0];
    }
}
