using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Compatibility adapter for the existing solo services. Read on every lookup so a load, reset or
    /// scene change cannot leave a cached pair of hands. Explicit guest identities never use this path.
    /// </summary>
    public sealed class SinglePlayerActorServices : IActorServices
    {
        public bool TryResolve(in InteractActor actor, out ActorServices services)
        {
            services = default;
            if (!string.Equals(actor.PlayerKey, InteractActor.LocalPlayerKey, StringComparison.Ordinal))
                return false;

            services = new ActorServices(GameServices.Hands, GameServices.BagHands, GameServices.Wallet);
            return true;
        }
    }
}
