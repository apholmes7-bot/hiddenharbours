using System;
using Unity.Collections;
using Unity.Netcode;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>Chat on the existing co-op transport; host validates and echoes each accepted line once.</summary>
    public sealed class CoopChatSession : IDisposable
    {
        public const string SubmitMessageName = "hh.chat.submit.v1";
        public const string RelayMessageName = "hh.chat.relay.v1";
        private readonly NetworkManager _manager;
        private readonly CustomMessagingManager _messages;
        private readonly CoopChatPolicy _policy;
        private readonly CoopChatPolicy _outgoing;
        private readonly Func<double> _elapsedSeconds;
        private bool _disposed;
        public event Action<CoopChatMessage> Received;

        public CoopChatSession(NetworkManager manager, CoopChatLimits limits, Func<double> elapsedSeconds)
        {
            if (manager == null || !manager.IsListening) throw new ArgumentException("Start the transport first.", nameof(manager));
            _policy = new CoopChatPolicy(limits);
            _outgoing = new CoopChatPolicy(limits);
            _elapsedSeconds = elapsedSeconds ?? throw new ArgumentNullException(nameof(elapsedSeconds));
            _manager = manager;
            _messages = manager.CustomMessagingManager;
            _messages.RegisterNamedMessageHandler(SubmitMessageName, OnSubmit);
            _messages.RegisterNamedMessageHandler(RelayMessageName, OnRelay);
            _manager.OnClientDisconnectCallback += OnDisconnected;
            _manager.OnServerStopped += OnStopped;
            _manager.OnClientStopped += OnStopped;
        }

        // True means submitted; only Received confirms host acceptance. A client never echoes locally.
        public bool Send(string text)
        {
            if (_disposed || !_manager.IsListening || !_manager.IsConnectedClient ||
                !_policy.TryNormalize(text, out string normalized)) return false;
            if (_manager.IsServer) return Accept(_manager.LocalClientId, normalized);
            if (!_outgoing.TryAccept(_manager.LocalClientId, normalized, _elapsedSeconds(), out var message)) return false;
            using var writer = new FastBufferWriter(CoopChatWire.MaxPacketBytes, Allocator.Temp);
            CoopChatWire.Write(writer, message);
            _messages.SendNamedMessage(SubmitMessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            return true;
        }

        private void OnSubmit(ulong transportSender, FastBufferReader reader)
        {
            if (_disposed || !_manager.IsServer || !_manager.ConnectedClients.ContainsKey(transportSender)) return;
            if (CoopChatWire.TryRead(reader, out var message))
                Accept(transportSender, message.Text); // Never trust the sender id claimed in a payload.
        }

        private bool Accept(ulong senderId, string text)
        {
            if (!_policy.TryAccept(senderId, text, _elapsedSeconds(), out var message)) return false;
            using var writer = new FastBufferWriter(CoopChatWire.MaxPacketBytes, Allocator.Temp);
            CoopChatWire.Write(writer, message);
            var clients = _manager.ConnectedClientsIds;
            for (int i = 0; i < clients.Count; i++)
                if (clients[i] != NetworkManager.ServerClientId)
                    _messages.SendNamedMessage(RelayMessageName, clients[i], writer, NetworkDelivery.ReliableSequenced);
            Received?.Invoke(message);
            return true;
        }

        private void OnRelay(ulong transportSender, FastBufferReader reader)
        {
            if (_disposed || _manager.IsServer || transportSender != NetworkManager.ServerClientId) return;
            if (CoopChatWire.TryRead(reader, out var message) && _policy.TryNormalize(message.Text, out string normalized))
                Received?.Invoke(new CoopChatMessage(message.SenderId, normalized));
        }

        private void OnDisconnected(ulong senderId)
        {
            _policy.RemoveSender(senderId);
            if (!_manager.IsServer && senderId == _manager.LocalClientId) Dispose();
        }

        private void OnStopped(bool wasHost) => Dispose();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _messages.UnregisterNamedMessageHandler(SubmitMessageName);
            _messages.UnregisterNamedMessageHandler(RelayMessageName);
            if (_manager != null)
            {
                _manager.OnClientDisconnectCallback -= OnDisconnected;
                _manager.OnServerStopped -= OnStopped;
                _manager.OnClientStopped -= OnStopped;
            }
            Received = null;
        }
    }
}
