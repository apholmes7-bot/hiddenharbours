namespace HiddenHarbours.Core
{
    /// <summary>
    /// Resolves the player making an interaction. An unknown player returns false and an empty result;
    /// callers must refuse the action instead of falling back to another player's services.
    /// </summary>
    public interface IActorServices
    {
        bool TryResolve(in InteractActor actor, out ActorServices services);
    }

    /// <summary>
    /// The interfaces an interaction may use. Hands and bag hands belong to the addressed player;
    /// the purse belongs to the session and can be shared. This is a view of services, not saved state.
    /// </summary>
    public readonly struct ActorServices
    {
        private readonly ICarrier _hands;
        private readonly IShoppingBagHands _bagHands;
        private readonly IWallet _purse;

        public ICarrier Hands => Live(_hands);
        public IShoppingBagHands BagHands => Live(_bagHands);
        public IWallet Purse => Live(_purse);

        public ActorServices(ICarrier hands, IShoppingBagHands bagHands, IWallet purse)
        {
            _hands = hands;
            _bagHands = bagHands;
            _purse = purse;
        }

        // Interface references must also reject a component destroyed since the lookup.
        private static T Live<T>(T service) where T : class
            => service is UnityEngine.Object producer && producer == null ? null : service;
    }
}
