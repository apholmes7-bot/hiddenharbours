using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>The helm refuses a cast while steering; explain the existing route to the deck.
    /// Reads the same Core hands seam as the fishing gate, without referencing Fishing.</summary>
    public static class HelmFishingHint
    {
        public static bool ShouldShow(ControlMode mode, ICarrier hands, string rodId)
            => mode == ControlMode.Aboard && CarriedItem.InHand(hands, rodId);
    }
}
