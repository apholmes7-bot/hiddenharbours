using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>An inside door surface, distinct from the cabin's indexed room soles.</summary>
    public interface ICabinEntryLanding
    {
        BoatInteriorDoor EntryLandingDoor { get; }
        Vector3 EntryLandingArrivalPoint { get; }
        bool TryEnterLanding(BoatInteriorDoor door);
        bool TryGoToLanding(BoatInteriorDoor door);
        bool TryTakeEntryStair(string routeId);
        Vector2 ClampToEntryLanding(Vector2 wanted, Vector2 fallback);
    }
}
