namespace HiddenHarbours.Core
{
    /// <summary>
    /// A deliberately tiny service locator. The composition root (GameRoot, in the App
    /// assembly) constructs the services at boot and assigns them here; feature modules read
    /// them through the Core interfaces. This is the "start simple" wiring noted in
    /// docs/architecture/tech-architecture.md §2 — a full DI container can replace it later
    /// without changing call sites.
    /// </summary>
    public static class GameServices
    {
        public static IGameClock Clock { get; set; }
        public static IEnvironmentService Environment { get; set; }
        public static IWallet Wallet { get; set; }

        public static bool Ready => Clock != null && Environment != null;

        /// <summary>Clear references (scene teardown / tests).</summary>
        public static void Reset()
        {
            Clock = null;
            Environment = null;
            Wallet = null;
        }
    }
}
