using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Session-owned bindings for separately addressed hands and bags, with one shared purse.
    /// No binding falls back to the solo globals. Gameplay state remains owned by the supplied services.
    /// </summary>
    public sealed class ActorServicesByPlayer : IActorServices
    {
        private readonly IWallet _purse;
        private readonly Dictionary<string, ActorServices> _players =
            new Dictionary<string, ActorServices>(StringComparer.Ordinal);

        public ActorServicesByPlayer(IWallet purse)
        {
            _purse = purse ?? throw new ArgumentNullException(nameof(purse));
        }

        /// <summary>
        /// Bind or replace the live services for an established key. The session assigns keys and retains
        /// a departing guest's held state; rebinding the same key does not create or transfer any goods.
        /// </summary>
        public void Bind(string playerKey, ICarrier hands, IShoppingBagHands bagHands)
        {
            if (string.IsNullOrWhiteSpace(playerKey))
                throw new ArgumentException("A player key must be supplied.", nameof(playerKey));

            _players[playerKey] = new ActorServices(hands, bagHands, _purse);
        }

        /// <summary>Remove a live binding only. Does not discard the player's held state.</summary>
        public bool Unbind(string playerKey) => playerKey != null && _players.Remove(playerKey);

        /// <summary>End these session bindings. The supplied service owners handle their own teardown.</summary>
        public void Clear() => _players.Clear();

        public bool TryResolve(in InteractActor actor, out ActorServices services)
            => _players.TryGetValue(actor.PlayerKey, out services);
    }
}
