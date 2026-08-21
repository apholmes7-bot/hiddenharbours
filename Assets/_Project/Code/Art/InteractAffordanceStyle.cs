using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Which of the three looks the affordance wears</b> — the owner picks one on a walk, and the two
    /// that lose are DELETED rather than left as dead toggles (the lane's own acceptance term).
    ///
    /// <para>Values are deliberately NOT append-only, unlike a Core contract: this enum is scaffolding
    /// with a known end date. Nothing serializes it but the dev menu's session-local cycle.</para>
    /// </summary>
    public enum InteractAffordanceVariant
    {
        /// <summary>The candidate lifts one palette step — brighter, same hue family, no outline.</summary>
        ShadeLift = 0,

        /// <summary>A 1px outline in the sprite's own darkest step, breathing slowly. No lift.</summary>
        OutlinePulse = 1,

        /// <summary>Both, each at half strength.</summary>
        Both = 2,
    }

    /// <summary>
    /// <b>The affordance's rules, with no scene in them</b> — what is armed, and how strong each half is
    /// this instant. Separated from <see cref="InteractAffordancePresenter"/> so the behaviour can be
    /// tested as arithmetic rather than by standing a room up and looking at it.
    /// </summary>
    public static class InteractAffordanceStyle
    {
        /// <summary>How many variants there are, for the dev menu's cycle. Derived, so adding one to the
        /// enum cannot leave the cycle a variant short.</summary>
        public const int VariantCount = 3;

        /// <summary>The next variant in the cycle, wrapping. Written with a modulo over
        /// <see cref="VariantCount"/> rather than a switch so it cannot fall out of step with the
        /// enum.</summary>
        public static InteractAffordanceVariant Next(InteractAffordanceVariant current)
            => (InteractAffordanceVariant)(((int)current + 1) % VariantCount);

        /// <summary>
        /// How fast the outline breathes, in hertz. The brief says "slowly (~0.5 Hz)" — a two-second
        /// cycle, which reads as breath rather than as a blinking alert.
        /// </summary>
        public const float PulseHz = 0.5f;

        /// <summary>
        /// The floor of the pulse. The outline never goes fully out: a highlight that vanishes on the
        /// downbeat reads as a flicker, and the point of the affordance is that the thing is CONTINUOUSLY
        /// legible while it is the candidate. So it breathes between this and full.
        /// </summary>
        public const float PulseFloor = 0.45f;

        /// <summary>
        /// The pulse's value at a given time — <c>PulseFloor…1</c>, a cosine so it starts at FULL.
        ///
        /// <para><b>Starting at full is the point of using cosine here.</b> The pulse is armed the instant
        /// a candidate is picked up, and phase is measured from that instant (see
        /// <see cref="InteractAffordancePresenter"/>), so the first thing the player sees when they turn to
        /// face a thing is the outline at its strongest. A sine would arm it at the floor and fade UP,
        /// which reads as the highlight arriving late.</para>
        /// </summary>
        public static float Pulse(float secondsSinceArmed)
        {
            float phase = Mathf.Cos(secondsSinceArmed * PulseHz * 2f * Mathf.PI);   // 1 → −1 → 1
            float unit = 0.5f + 0.5f * phase;                                       // 1 → 0 → 1
            return Mathf.Lerp(PulseFloor, 1f, unit);
        }

        /// <summary>
        /// How much of the shade lift this variant wants, 0…1. <see cref="InteractAffordanceVariant.Both"/>
        /// runs each half at half strength — the brief's own term — so the two together land at about the
        /// weight of either alone rather than shouting.
        /// </summary>
        public static float LiftStrength(InteractAffordanceVariant variant) => variant switch
        {
            InteractAffordanceVariant.ShadeLift => 1f,
            InteractAffordanceVariant.Both => 0.5f,
            _ => 0f,
        };

        /// <summary>How much of the outline this variant wants, 0…1. See <see cref="LiftStrength"/>.</summary>
        public static float OutlineStrength(InteractAffordanceVariant variant) => variant switch
        {
            InteractAffordanceVariant.OutlinePulse => 1f,
            InteractAffordanceVariant.Both => 0.5f,
            _ => 0f,
        };

        /// <summary>True if this variant draws anything behind the fixture (the outline pass).</summary>
        public static bool UsesOutline(InteractAffordanceVariant variant) => OutlineStrength(variant) > 0f;

        /// <summary>True if this variant draws anything over the fixture (the lift pass).</summary>
        public static bool UsesLift(InteractAffordanceVariant variant) => LiftStrength(variant) > 0f;
    }

    /// <summary>
    /// <b>What the affordance should be doing right now</b>, as a value — armed or not, and on which id.
    ///
    /// <para>The whole of the lane's state machine, expressed as a pure function of the two things that
    /// decide it. The brief asks for exactly these rules and they are testable without a scene:
    /// a candidate arrives → armed; it leaves → disarmed; a modal comes up → disarmed.</para>
    /// </summary>
    public readonly struct InteractAffordanceState
    {
        /// <summary>True when something should be lit.</summary>
        public readonly bool Armed;

        /// <summary>The id to light, or null when <see cref="Armed"/> is false.</summary>
        public readonly string Id;

        private InteractAffordanceState(bool armed, string id) { Armed = armed; Id = id; }

        /// <summary>Nothing lit.</summary>
        public static InteractAffordanceState Disarmed => new InteractAffordanceState(false, null);

        /// <summary>
        /// The rule, with the suppression handed in — <b>armed exactly when there is an offer and nothing
        /// is holding it down</b>. This overload takes no statics, which is what lets the whole state
        /// machine be tested as arithmetic (a modal is just <c>suppressed: true</c>).
        /// </summary>
        public static InteractAffordanceState For(in InteractOfferChanged offer, bool suppressed)
            => offer.Has && !suppressed
                ? new InteractAffordanceState(true, offer.Id)
                : Disarmed;

        /// <summary>
        /// The rule, reading the live suppression itself — what the presenter calls.
        ///
        /// <para><b>Armed exactly when the popup is showing.</b> Both go through
        /// <see cref="InteractOfferVisibility"/>, so the lit fixture and the named fixture cannot
        /// disagree; that is the whole reason the decision was lifted into Core.</para>
        /// </summary>
        public static InteractAffordanceState For(in InteractOfferChanged offer)
            => For(offer, InteractOfferVisibility.Suppressed);

        /// <summary>Is this the same armed state as <paramref name="other"/>? Ordinal id compare, matching
        /// the offer channel's own equality.</summary>
        public bool SameAs(in InteractAffordanceState other)
            => Armed == other.Armed && string.Equals(Id, other.Id, System.StringComparison.Ordinal);
    }
}
