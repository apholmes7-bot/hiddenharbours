using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.CoopSpike
{
    /// <summary>Network peers use the same local-scene contract as the opt-in trial.</summary>
    public abstract class CoopNetworkTest : NetcodeIntegrationTest
    {
        private bool _tornDown;

        // Own the per-case lifecycle so the guard runs after SDK cleanup. Do not add
        // a second attributed teardown: Unity discovers hidden base methods too.
        protected override NetworkManagerInstatiationMode OnSetIntegrationTestMode()
            => NetworkManagerInstatiationMode.DoNotCreate;

        protected sealed override IEnumerator OnSetup()
        {
            _tornDown = false;
            AssertNoNetworkManagers();
            yield return base.OnSetup();
            CreateServerAndClients();
            yield return StartServerAndClients();
        }

        protected override void OnServerAndClientsCreated()
        {
            base.OnServerAndClientsCreated();
            // Each peer loads its own authored region. Never synchronize the runner's
            // transient scene (which has no build-settings path) during approval.
            foreach (var manager in m_NetworkManagers)
                manager.NetworkConfig.EnableSceneManagement = false;
        }

        protected virtual IEnumerator OnTrialTearDown() { yield break; }

        protected sealed override IEnumerator OnTearDown()
        {
            if (!_tornDown)
            {
                try { yield return OnTrialTearDown(); }
                finally
                {
                    ShutdownAndCleanUp();
                    _tornDown = true;
                }
                yield return null;
            }
            AssertNoNetworkManagers();
        }

        [UnityTest]
        public IEnumerator NetworkPeersAreRemovedByTearDown()
        {
            Assert.That(m_NetworkManagers.Length, Is.EqualTo(NumberOfClients + 1));
            foreach (var manager in m_NetworkManagers)
            {
                Assert.That(manager.IsListening, Is.True);
                Assert.That(manager.NetworkConfig.EnableSceneManagement, Is.False);
            }
            yield return OnTearDown();
            yield return null;
            AssertNoNetworkManagers();
        }

        public static void AssertNoNetworkManagers()
        {
            var managers = Object.FindObjectsByType<NetworkManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(managers.Count(manager => manager.IsListening), Is.Zero,
                "No host or connecting client may keep listening after a trial test.");
            Assert.That(managers, Is.Empty, "Even stopped or inactive managers must be destroyed.");
            Assert.That(NetworkManager.Singleton, Is.Null, "The next solo boot must not inherit a session.");
        }
    }
}
