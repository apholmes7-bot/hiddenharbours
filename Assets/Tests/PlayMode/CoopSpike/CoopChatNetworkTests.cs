using System.Collections;
using System.Collections.Generic;
using HiddenHarbours.CoopSpike;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.CoopSpike
{
    public sealed class CoopChatNetworkTests : CoopNetworkTest
    {
        protected override int NumberOfClients => 1;
        private CoopChatSession _host, _client;
        private readonly List<CoopChatMessage> _atHost = new(), _atClient = new();
        private double _now;

        protected override IEnumerator OnServerAndClientsConnected()
        {
            _atHost.Clear(); _atClient.Clear(); _now = 0;
            var limits = new CoopChatLimits { MessagesPerWindow = 1 };
            _host = new CoopChatSession(m_ServerNetworkManager, limits, () => _now);
            _client = new CoopChatSession(m_ClientNetworkManagers[0], limits, () => _now);
            _host.Received += _atHost.Add;
            _client.Received += _atClient.Add;
            yield break;
        }

        protected override IEnumerator OnTrialTearDown()
        {
            _host?.Dispose(); _client?.Dispose();
            yield break;
        }

        [UnityTest]
        public IEnumerator BothPeersReceiveBothSpeakersExactlyOnce()
        {
            Assert.That(_client.Send("  hello host  "), Is.True);
            Assert.That(_client.Send("too soon"), Is.False, "The client also caps outgoing submissions.");
            Assert.That(_atClient.Count, Is.Zero, "No optimistic echo before the host accepts.");
            yield return WaitForConditionOrTimeOut(() => _atHost.Count == 1 && _atClient.Count == 1);
            Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False);
            Assert.That(_host.Send("hello crew"), Is.True);
            yield return WaitForConditionOrTimeOut(() => _atHost.Count == 2 && _atClient.Count == 2);
            Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False);
            for (int i = 0; i < 2; i++)
            {
                Assert.That(_atHost[i].Text, Is.EqualTo(i == 0 ? "hello host" : "hello crew"));
                Assert.That(_atClient[i].Text, Is.EqualTo(_atHost[i].Text));
                Assert.That(_atHost[i].SenderId, Is.EqualTo(i == 0 ? m_ClientNetworkManagers[0].LocalClientId : NetworkManager.ServerClientId));
                Assert.That(_atClient[i].SenderId, Is.EqualTo(_atHost[i].SenderId));
            }
        }

        [UnityTest]
        public IEnumerator HostIgnoresForgedSenderAndDropsExcessMessages()
        {
            SubmitRaw(NetworkManager.ServerClientId, "first");
            SubmitRaw(777, "excess");
            yield return HostBarrier();
            yield return WaitForConditionOrTimeOut(() => _atClient.Count == 1);
            Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False);
            // The barrier has proved that the host processed both submissions before this assertion.
            Assert.That(_host.Send("fence"), Is.True);
            yield return WaitForConditionOrTimeOut(() => _atClient.Count == 2);
            Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False);
            Assert.That(_atHost.Count, Is.EqualTo(2));
            Assert.That(_atClient[0].SenderId, Is.EqualTo(m_ClientNetworkManagers[0].LocalClientId));
            Assert.That(_atClient[1].Text, Is.EqualTo("fence"));
            _now = 10;
            SubmitRaw(777, "after limit");
            yield return WaitForConditionOrTimeOut(() => _atClient.Count == 3);
            Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False);
            Assert.That(_atHost.Count, Is.EqualTo(3));
            Assert.That(_atClient[2].Text, Is.EqualTo("after limit"));
        }

        [UnityTest]
        public IEnumerator MalformedAndMarkupPacketsDoNotBreakTheNextValidLine()
        {
            using (var writer = new FastBufferWriter(CoopChatWire.MaxPacketBytes, Allocator.Temp))
            {
                writer.WriteValueSafe(0UL); writer.WriteValueSafe(ushort.MaxValue);
                m_ClientNetworkManagers[0].CustomMessagingManager.SendNamedMessage(CoopChatSession.SubmitMessageName,
                    NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
            SubmitRaw(0, "<b>hidden</b>");
            SubmitRaw(0, "good");
            yield return HostBarrier();
            yield return WaitForConditionOrTimeOut(() => _atClient.Count == 1);
            Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False);
            Assert.That(_atHost.Count, Is.EqualTo(1));
            Assert.That(_atClient[0].Text, Is.EqualTo("good"));
        }

        [UnityTest]
        public IEnumerator DisposedSessionsCanBeReplacedWithoutDuplicateDelivery()
        {
            _client.Dispose(); _client.Dispose();
            Assert.That(_client.Send("closed"), Is.False);
            _client = new CoopChatSession(m_ClientNetworkManagers[0], new CoopChatLimits(), () => _now);
            _client.Received += _atClient.Add;
            Assert.That(_host.Send("new session"), Is.True);
            yield return WaitForConditionOrTimeOut(() => _atClient.Count == 1);
            Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False);
            Assert.That(_atClient.Count, Is.EqualTo(1));
            Assert.That(_atHost.Count, Is.EqualTo(1));
        }

        private IEnumerator HostBarrier()
        {
            const string name = "hh.chat.test.barrier";
            bool reached = false;
            m_ServerNetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(name, (_, __) => reached = true);
            try
            {
                using (var writer = new FastBufferWriter(1, Allocator.Temp))
                {
                    writer.WriteValueSafe((byte)1);
                    m_ClientNetworkManagers[0].CustomMessagingManager.SendNamedMessage(name,
                        NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
                }
                yield return WaitForConditionOrTimeOut(() => reached);
                Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False, "The reliable host receive queue must reach the barrier.");
            }
            finally { m_ServerNetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(name); }
        }

        private void SubmitRaw(ulong claimedSender, string text)
        {
            using var writer = new FastBufferWriter(CoopChatWire.MaxPacketBytes, Allocator.Temp);
            CoopChatWire.Write(writer, new CoopChatMessage(claimedSender, text));
            m_ClientNetworkManagers[0].CustomMessagingManager.SendNamedMessage(CoopChatSession.SubmitMessageName,
                NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }
    }
}
