using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One starting tie between two villagers</b> (relationships slice 1, owner ruling 2026-11-14: "some
    /// start with friends some with none"). One asset per tie under
    /// <c>Data/Resources/Relationships/Ties/</c> (rule 2). A villager with no tie asset starts a stranger to
    /// everyone; the player's actions can change that.
    ///
    /// <para><b>One tie per pair, written one way.</b> <see cref="NpcA"/> sorts before <see cref="NpcB"/>
    /// (ordinal) and the id is <c>tie.&lt;a&gt;__&lt;b&gt;</c> with each villager's <c>npc.</c> prefix dropped,
    /// so the same pair can never be authored twice. The content validation test holds every asset to that
    /// and to real villager ids.</para>
    ///
    /// <para>Read only through <see cref="IRelationshipService"/>; the strength is never shown to the player.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character/Relationship Tie", fileName = "Tie")]
    public sealed class RelationshipTieDef : ScriptableObject
    {
        [Tooltip("Stable id, `tie.<a>__<b>` (tie.aunt_ginny__basil_samson). Append-only.")]
        public string Id;

        [Tooltip("The first villager's NpcDef id (npc.snake_case). Sorts before NpcB.")]
        public string NpcA;

        [Tooltip("The second villager's NpcDef id. Sorts after NpcA.")]
        public string NpcB;

        [Tooltip("Where the pair start, inside the config's strength bounds. Positive is warm, negative cool. " +
                 "The stage it reads as comes from the config's thresholds. Never shown to the player.")]
        public int StartingStrength;

        [Tooltip("One line for writers: why they start this way (grew up on the same lane). Not shown in game.")]
        [TextArea] public string Note;
    }
}
