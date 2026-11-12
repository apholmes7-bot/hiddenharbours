using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>The container in the player's hands, as the hold a counter sale reads</b>: the delivered pail of the
    /// owner's 2026-11-11 ruling ("delivered pails, with the empty pail retained"). A counter that sells what is
    /// carried asks this, never the boat's hold, so catch reaches the counter in hand or not at all.
    ///
    /// <para><b>Through Core only.</b> The hands are Core's <see cref="ICarrier"/> (published on
    /// <c>GameServices.Hands</c>) and the container is Core's <see cref="IHold"/>, so Economy never names the
    /// Player lane's pail class.</para>
    ///
    /// <para><b>Deterministic.</b> The answer depends only on what is in which hand: right hand first, then left
    /// (the fixed order of <see cref="CarriedItem.Both"/>). A held thing that is not a container (a cradled fish,
    /// a rod, a fuel can) is passed over; nothing held, or no hands published, answers null, and a sale handed
    /// null sells nothing.</para>
    /// </summary>
    public static class CarriedHold
    {
        /// <summary>The first container held, right hand first, or null.</summary>
        public static IHold From(ICarrier hands)
        {
            CarriedItem.Both(hands, out ICarriable first, out ICarriable second);
            return AsHold(first) ?? AsHold(second);
        }

        /// <summary>⚠ Explicit fake-null check: a destroyed pail is not a container to sell from, and an
        /// interface reference does not carry Unity's overloaded <c>==</c> (the CarriedItem lesson).</summary>
        private static IHold AsHold(ICarriable held)
        {
            if (held is UnityEngine.Object o && o == null) return null;
            return held as IHold;
        }
    }
}
