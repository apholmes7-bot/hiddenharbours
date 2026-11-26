using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A question the player can ask around</b>, and the clue ladders it climbs. The row appears in the
    /// conversation of everybody with a <see cref="Fallbacks"/> line (the people who can be asked); each
    /// ask speaks at most one rung per missing supply, in <see cref="Ladders"/> order, and the speaker's
    /// fallback when there is nothing new they can say.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Supplies/Clue Topic", fileName = "ClueTopic")]
    public sealed class ClueTopicDef : ScriptableObject
    {
        [Tooltip("Stable, append-only id (cluetopic.snake_case).")]
        public string Id = "cluetopic.example";

        [Tooltip("The row's stable id (option.snake_case).")]
        public string OptionId = "";

        [Tooltip("What the row says, in the player's voice.")]
        public string RowLabel = "";

        [Tooltip("The ladders climbed, in the order their lines are spoken.")]
        public ClueLadderDef[] Ladders = new ClueLadderDef[0];

        [Tooltip("Who can be asked, and what each says when they have nothing new.")]
        public ClueHint[] Fallbacks = new ClueHint[0];

        /// <summary>True when <paramref name="npcId"/> can be asked this question.</summary>
        public bool AskedOf(string npcId) => FallbackFor(npcId) != null;

        /// <summary>The speaker's fallback line, or null when they cannot be asked.</summary>
        public string FallbackFor(string npcId)
        {
            if (string.IsNullOrEmpty(npcId) || Fallbacks == null) return null;
            for (int i = 0; i < Fallbacks.Length; i++)
                if (Fallbacks[i].NpcId == npcId) return Fallbacks[i].Line ?? "";
            return null;
        }
    }
}
