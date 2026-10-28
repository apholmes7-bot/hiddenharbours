using System.Collections.Generic;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The enabled curl hosts (one per region scene that has picked sites). The renderer feature
    /// reads it to decide whether to record the curl's passes at all, and to draw the live slots.
    /// A host leaves on disable, so an unloaded scene leaves nothing behind.
    /// </summary>
    public static class CurlingBreakerRegistry
    {
        private const int ExpectedHosts = 4;
        private static readonly List<CurlingBreakerRegion> s_Hosts = new List<CurlingBreakerRegion>(ExpectedHosts);

        public static int Count => s_Hosts.Count;

        public static CurlingBreakerRegion Get(int index) => s_Hosts[index];

        /// <summary>Live slots across every host — 0 means no curl pass is recorded.</summary>
        public static int LiveCurls
        {
            get
            {
                int live = 0;
                for (int i = 0; i < s_Hosts.Count; i++)
                    if (s_Hosts[i] != null) live += s_Hosts[i].LiveCount;
                return live;
            }
        }

        internal static void Register(CurlingBreakerRegion host)
        {
            if (host != null && !s_Hosts.Contains(host)) s_Hosts.Add(host);
        }

        internal static void Unregister(CurlingBreakerRegion host) => s_Hosts.Remove(host);
    }
}
