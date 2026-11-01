using System.Collections;
using System.Linq;
using HiddenHarbours.Boats;
using HiddenHarbours.CoopSpike;
using HiddenHarbours.Core;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.CoopSpike
{
    public sealed partial class CoopSpikeNetworkTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;
        private sealed class Clock : IGameClock
        {
            public double TotalSeconds { get; private set; }
            public int Seeks;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => HiddenHarbours.Core.Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => HiddenHarbours.Core.Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 6;
            public float DayFraction => 0.25f;
            public bool IsPaused { get; set; } = true;
            public float TimeScale { get; set; } = 1;
            public void SeekTo(double seconds) { TotalSeconds = seconds; Seeks++; }
        }
        private Clock _hostClock, _clientClock;
        private GameObject _localBoat;

        protected override void OnCreatePlayerPrefab()
        {
            var motion = m_PlayerPrefab.AddComponent<NetworkTransform>();
            motion.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            motion.Interpolate = false;
            m_PlayerPrefab.AddComponent<CoopBoatReplica>();
        }
        protected override IEnumerator OnServerAndClientsConnected()
        {
            _hostClock = new Clock(); _hostClock.SeekTo(1234.5);
            _clientClock = new Clock(); _clientClock.SeekTo(17);
            foreach (var obj in m_ServerNetworkManager.SpawnManager.SpawnedObjects.Values)
                obj.GetComponent<CoopBoatReplica>().BindClock(_hostClock, 12345);
            foreach (var obj in m_ClientNetworkManagers[0].SpawnManager.SpawnedObjects.Values)
                obj.GetComponent<CoopBoatReplica>().BindClock(_clientClock, 12345);
            yield return null;
        }
        protected override IEnumerator OnTearDown()
        {
            if (_localBoat != null) Object.Destroy(_localBoat);
            yield return TearDownDayTwo();
            yield return null;
        }
        private CoopBoatReplica Client => m_ClientNetworkManagers[0].LocalClient.PlayerObject.GetComponent<CoopBoatReplica>();
        private CoopBoatReplica ClientAtHost => m_ServerNetworkManager.SpawnManager.SpawnedObjects[
            Client.NetworkObjectId].GetComponent<CoopBoatReplica>();

        [UnityTest]
        public IEnumerator TwoOwnedBoatsAndTheHostClockReachTheClient()
        {
            Assert.That(m_ServerNetworkManager.SpawnManager.SpawnedObjects.Count, Is.EqualTo(2));
            Assert.That(m_ClientNetworkManagers[0].SpawnManager.SpawnedObjects.Count, Is.EqualTo(2));
            Assert.That(Client.IsOwner, Is.True);
            Assert.That(ClientAtHost.IsOwner, Is.False);
            yield return WaitForConditionOrTimeOut(() => Client.ClockAccepted && _clientClock.TotalSeconds == 1234.5);
            Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False);
            Assert.That(_clientClock.Seeks, Is.GreaterThan(1));
            Client.transform.position = new Vector3(7, -16, 0);
            yield return WaitForConditionOrTimeOut(() => Vector3.Distance(ClientAtHost.transform.position,
                new Vector3(7, -16, 0)) < 0.05f);
            Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False, "Owner movement must cross the transport.");
        }

        [UnityTest]
        public IEnumerator ClientFChangesTheHullThatTheHostDraws()
        {
            var settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            Assert.That(settings, Is.Not.Null, "Run the spike asset plan before PlayMode.");
            _localBoat = new GameObject("ClientBoatFixture");
            _localBoat.AddComponent<Rigidbody2D>().gravityScale = 0;
            var boat = _localBoat.AddComponent<BoatController>();
            boat.SetHull(settings.Roster[0]);
            var hold = _localBoat.AddComponent<ShipHold>();
            var renderer = _localBoat.AddComponent<SpriteRenderer>();
            var picker = _localBoat.AddComponent<DevBoatPicker>();
            picker.Configure(settings.Roster, boat, hold, renderer);
            Client.BindLocal(_localBoat.transform, null, () => boat.Hull.Id, () => string.Empty);
            var input = new InputTestFixture();
            input.Setup();
            try
            {
                var keyboard = InputSystem.AddDevice<Keyboard>();
                Assert.That(picker.IsAtHelm, Is.True, "The input fixture must be driving its boat.");
                Assert.That(Keyboard.current, Is.SameAs(keyboard));
                // Use the SDK's isolated player input runtime, not native editor focus/state.
                // Queue for the player loop so the real picker Update consumes exactly one edge.
                input.Press(keyboard.fKey, queueEventOnly: true);
                yield return null;
                Assert.That(boat.Hull.Id, Is.EqualTo(settings.Roster[1].Id));
                input.Release(keyboard.fKey);
                yield return WaitForConditionOrTimeOut(() => ClientAtHost.Boat.Value.HullId.ToString() == settings.Roster[1].Id);
                Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False);
                Assert.That(ClientAtHost.GetComponent<CoopReplicaView>().DrawnHullId, Is.EqualTo(settings.Roster[1].Id));
            }
            finally { input.TearDown(); }
        }
    }
}
