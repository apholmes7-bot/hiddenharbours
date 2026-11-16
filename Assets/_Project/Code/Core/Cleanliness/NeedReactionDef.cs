using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>WHAT A VILLAGER SAYS WHEN A NEED SHOWS, AS DATA</b> (cleanliness slice 1, owner ask 2026-11-15: "a
    /// dirty/stinky player will be noticeable by npcs and they will react negatively"). One asset per
    /// reaction under <c>Data/Resources/NeedReactions/</c>, id <c>reaction.snake_case</c>. The text lives
    /// here, never in logic: the dialogue lane picks a line when <see cref="NeedsNotice.ToneNow"/> returns this
    /// reaction's <see cref="Tone"/> (a bubble, Animal Crossing style, no portrait).
    ///
    /// <para>Nothing reads these in slice 1: villager talk on main has no needs hook yet. The join is one
    /// call in the World talk path (see <c>docs/world/washrooms-plan.md</c>).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Need Reaction", fileName = "NeedReaction")]
    public sealed class NeedReactionDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (reaction.snake_case, e.g. reaction.stinky).")]
        public string Id = "reaction.example";

        [Tooltip("The need this reaction answers.")]
        public NeedKind Need = NeedKind.Dirtiness;

        [Tooltip("The stage of that need at which villagers start saying these.")]
        public NeedStage AtStage = NeedStage.Strong;

        [Tooltip("The reply tone this reaction belongs to.")]
        public ReplyTone Tone = ReplyTone.PutOff;

        [Tooltip("What a villager may say, one bubble each. Picked by the dialogue lane, deterministically.")]
        [TextArea(1, 3)] public string[] Lines = new string[0];
    }
}
