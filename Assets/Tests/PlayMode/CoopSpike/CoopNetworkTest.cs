using Unity.Netcode.TestHelpers.Runtime;

namespace HiddenHarbours.Tests.CoopSpike
{
    /// <summary>Network peers use the same local-scene contract as the opt-in trial.</summary>
    public abstract class CoopNetworkTest : NetcodeIntegrationTest
    {
        protected override void OnServerAndClientsCreated()
        {
            base.OnServerAndClientsCreated();
            // Each peer loads its own authored region. Never synchronize the runner's
            // transient scene (which has no build-settings path) during approval.
            foreach (var manager in m_NetworkManagers)
                manager.NetworkConfig.EnableSceneManagement = false;
        }
    }
}
