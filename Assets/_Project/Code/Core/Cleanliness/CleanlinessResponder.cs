namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The signals already on the bus, turned into dirt</b> (cleanliness slice 1). Installed once by the
    /// save service beside <see cref="RestSaveResponder"/> (it must outlive any one region). It reads only
    /// Core signals and calls only <see cref="GameServices.Cleanliness"/> (rule 4):
    /// <list type="bullet">
    /// <item><description><see cref="FishCaught"/>: one <see cref="DirtActivity.FishHandling"/> action.</description></item>
    /// <item><description><see cref="TrapHaulStateChanged"/>: hauling is a stretch of
    /// <see cref="DirtActivity.HardWork"/>; a pot breaking the surface is one <see cref="DirtActivity.Bait"/>
    /// action (rebaiting it).</description></item>
    /// <item><description><see cref="OnFootWaterStateChanged"/>: swimming (and wading, when the config says)
    /// is washing at the sea station; leaving the water ends it.</description></item>
    /// </list>
    /// Clam digging, farm work, engine work and the gym have no signal on main yet; they arrive through the
    /// body lane's activity entry point (see <c>docs/world/washrooms-plan.md</c>).
    /// </summary>
    public static class CleanlinessResponder
    {
        private static bool _installed;

        public static bool Installed => _installed;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            EventBus.Subscribe<FishCaught>(OnFishCaught);
            EventBus.Subscribe<TrapHaulStateChanged>(OnTrapHaul);
            EventBus.Subscribe<OnFootWaterStateChanged>(OnWater);
        }

        public static void Uninstall()
        {
            if (!_installed) return;
            _installed = false;
            EventBus.Unsubscribe<FishCaught>(OnFishCaught);
            EventBus.Unsubscribe<TrapHaulStateChanged>(OnTrapHaul);
            EventBus.Unsubscribe<OnFootWaterStateChanged>(OnWater);
        }

        internal static void OnFishCaught(FishCaught e) => GameServices.Cleanliness.ReportAction(DirtActivity.FishHandling);

        internal static void OnTrapHaul(TrapHaulStateChanged e)
        {
            ICleanlinessService c = GameServices.Cleanliness;
            switch (e.State.Phase)
            {
                case TrapHaulPhase.Hauling:
                    c.BeginActivity(DirtActivity.HardWork);   // re-beginning the same stretch is a no-op
                    break;
                case TrapHaulPhase.Surfaced:
                    c.EndActivity(DirtActivity.HardWork);
                    c.ReportAction(DirtActivity.Bait);
                    break;
                default:
                    c.EndActivity(DirtActivity.HardWork);
                    break;
            }
        }

        internal static void OnWater(OnFootWaterStateChanged e)
        {
            ICleanlinessService c = GameServices.Cleanliness;
            CleanlinessSettings s = GameServices.CleanlinessSettings;
            bool inSea = e.State == OnFootWaterState.Swim || (s.WadeCountsAsSea && e.State == OnFootWaterState.Wade);
            WashStationDef sea = c.FindStation(string.IsNullOrEmpty(s.SeaStationId)
                                                   ? CleanlinessSettings.DefaultSeaStationId : s.SeaStationId);
            if (sea == null) return;
            if (inSea) c.BeginWash(sea);
            else if (c.WashingAt == sea) c.EndWash();
        }
    }
}
