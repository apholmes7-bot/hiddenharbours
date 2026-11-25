using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Why an action was refused for the player's needs. Append-only.</summary>
    public enum NeedsRefusal
    {
        None = 0,
        TooTired = 1,
        TooHungry = 2,
        TooTiredAndHungry = 3,
    }

    /// <summary>
    /// <b>How worn out or hungry is too much for one action</b>. Each half names the stage AT which the
    /// action is refused; <see cref="NeedStage.None"/> means never (an action that does not care).
    /// <c>default</c> is therefore "never refuses", which is what every action is until it opts in.
    /// </summary>
    [Serializable]
    public struct NeedsRequirement
    {
        [Tooltip("Refused at this tiredness stage or worse. None = tiredness never refuses this action.")]
        public NeedStage RefusedAtTiredness;

        [Tooltip("Refused at this hunger stage or worse. None = hunger never refuses this action.")]
        public NeedStage RefusedAtHunger;

        public NeedsRequirement(NeedStage refusedAtTiredness, NeedStage refusedAtHunger)
        {
            RefusedAtTiredness = refusedAtTiredness;
            RefusedAtHunger = refusedAtHunger;
        }

        /// <summary>Never refuses.</summary>
        public static NeedsRequirement Never => default;

        /// <summary>The owner's tunable bar for heavy work (<see cref="GameConfig.HeavyWorkNeeds"/>), with
        /// the documented fallback for an unwired config.</summary>
        public static NeedsRequirement HeavyWork(GameConfig config)
            => config != null ? config.HeavyWorkNeeds : GameConfig.DefaultHeavyWorkNeeds;
    }

    /// <summary>
    /// <b>An action that can be refused for tiredness or hunger.</b> Optional, beside
    /// <see cref="IInteractable"/>: <see cref="InteractVerb"/> asks <see cref="NeedsGate"/> before acting,
    /// and a refusal is spoken by the player (<see cref="NeedsGate.TryRefuse"/>) instead of the action.
    /// </summary>
    public interface INeedsGated
    {
        /// <summary>The bar for this action, read live (a requirement may depend on what is held).</summary>
        NeedsRequirement NeedsRequirement { get; }
    }

    /// <summary>
    /// <b>"I'm too tired", "I'm too hungry", or both</b> (owner ask 2026-11-15: "when a player is too tired
    /// to do an action they will say im too tired or im too hungry or both").
    ///
    /// <para><b>The words are data.</b> A refusal publishes an <see cref="InnerVoiceCue"/>; the line, its
    /// voice and its cooldown are <c>InnerVoiceLineDef</c> assets keyed on the cue ids below, spoken by
    /// World's inner-voice director in her own bubble. Nothing here holds a string the player reads.</para>
    ///
    /// <para><b>⚠ This is opt-in, and on main nothing has opted in.</b> The needs ruling of 2026-11-14
    /// says a need "never stops" the character; a refusal is a stop for one action, so which actions take a
    /// bar is the owner's call, action by action. Until then this is the helper, the lines and the tests,
    /// and <see cref="InteractVerb"/> consults it for any registrant that implements
    /// <see cref="INeedsGated"/>.</para>
    /// </summary>
    public static class NeedsGate
    {
        /// <summary>The pure rule: is a player at these stages refused by <paramref name="requirement"/>?</summary>
        public static NeedsRefusal Judge(NeedStage tiredness, NeedStage hunger, in NeedsRequirement requirement)
        {
            bool tired = requirement.RefusedAtTiredness != NeedStage.None && tiredness >= requirement.RefusedAtTiredness;
            bool hungry = requirement.RefusedAtHunger != NeedStage.None && hunger >= requirement.RefusedAtHunger;
            if (tired && hungry) return NeedsRefusal.TooTiredAndHungry;
            if (tired) return NeedsRefusal.TooTired;
            return hungry ? NeedsRefusal.TooHungry : NeedsRefusal.None;
        }

        /// <summary><see cref="Judge"/> against the local player's needs now.</summary>
        public static NeedsRefusal JudgeNow(in NeedsRequirement requirement)
        {
            INeedsService needs = GameServices.Needs;
            if (needs == null) return NeedsRefusal.None;
            return Judge(needs.Stage(NeedKind.Tiredness), needs.Stage(NeedKind.Hunger), requirement);
        }

        /// <summary>The inner-voice cue for a refusal, or null for <see cref="NeedsRefusal.None"/>.</summary>
        public static string CueFor(NeedsRefusal refusal)
        {
            switch (refusal)
            {
                case NeedsRefusal.TooTired:          return InnerVoiceCues.TooTired;
                case NeedsRefusal.TooHungry:         return InnerVoiceCues.TooHungry;
                case NeedsRefusal.TooTiredAndHungry: return InnerVoiceCues.TooTiredAndHungry;
                default:                             return null;
            }
        }

        /// <summary>
        /// Judge now and, if refused, have the player say so (the cue) and announce
        /// <see cref="NeedsRefused"/>. True when the action must not go ahead.
        /// </summary>
        public static bool TryRefuse(in NeedsRequirement requirement)
        {
            NeedsRefusal refusal = JudgeNow(requirement);
            if (refusal == NeedsRefusal.None) return false;
            EventBus.Publish(new InnerVoiceCue(CueFor(refusal)));
            EventBus.Publish(new NeedsRefused(refusal));
            return true;
        }
    }

    /// <summary>An action was refused for the player's needs. For a test, the audio lane, or a later
    /// look on the figure; nothing makes a gameplay decision off it.</summary>
    public readonly struct NeedsRefused
    {
        public readonly NeedsRefusal Refusal;

        public NeedsRefused(NeedsRefusal refusal) { Refusal = refusal; }
    }
}
