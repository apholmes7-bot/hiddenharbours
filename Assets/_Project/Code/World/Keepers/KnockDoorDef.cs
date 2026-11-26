using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A door that can be knocked on, as data.</b> The keeper's cottage door on the Whelps: the player knocks,
    /// and what comes back depends only on whether the one who lives there is indoors. Indoors means it is
    /// night and she is abed, so nobody answers; out means she is at her post, and the door says so. The words
    /// are inner-voice lines keyed on the two cue ids below, so the owner can rewrite them without code.
    /// Nothing about a knock is saved.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Knock Door", fileName = "KnockDoor_")]
    public sealed class KnockDoorDef : ScriptableObject
    {
        [Tooltip("Stable id, door.snake_case. Append-only.")]
        public string Id = "door.example";

        [Tooltip("What the prompt offers at the door.")]
        public string VerbLabel = "Knock";

        [Tooltip("How close the player must stand to knock, metres.")]
        [Min(0.3f)] public float ReachMetres = 1.1f;

        [Tooltip("The inner-voice cue said when the one who lives here is indoors (night): nobody answers.")]
        public string IndoorsCueId = "cue.door.example.nobody_answers";

        [Tooltip("The inner-voice cue said when the one who lives here is out at her post.")]
        public string OutCueId = "cue.door.example.out";

        /// <summary>The cue a knock raises, from whether the household is indoors. Empty when the Def names none.</summary>
        public string CueFor(bool householdIndoors) => (householdIndoors ? IndoorsCueId : OutCueId) ?? string.Empty;
    }
}
