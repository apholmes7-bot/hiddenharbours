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
    ///
    /// <para><b>No soap.</b> It also hears <see cref="PlayerWashChanged"/>: a wash that began wanting soap the
    /// player does not have (<see cref="PlayerWashChanged.SoapMissing"/>) makes him think
    /// <see cref="CleanlinessSettings.NoSoapLine"/>, in the inner-voice bubble every station shares,
    /// whichever station or anchor began it.</para>
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
            EventBus.Subscribe<PlayerWashChanged>(OnWash);
        }

        public static void Uninstall()
        {
            if (!_installed) return;
            _installed = false;
            EventBus.Unsubscribe<FishCaught>(OnFishCaught);
            EventBus.Unsubscribe<TrapHaulStateChanged>(OnTrapHaul);
            EventBus.Unsubscribe<OnFootWaterStateChanged>(OnWater);
            EventBus.Unsubscribe<PlayerWashChanged>(OnWash);
        }

        internal static void OnWash(PlayerWashChanged e)
        {
            if (e.Washing && e.SoapMissing) SayNoSoap();
        }

        /// <summary>
        /// The player thinks the no-soap line: an inner-voice bubble anchored at him, the channel the effort
        /// gate's refusal lines use. Nothing with no line authored. Returns true when a line went out.
        /// </summary>
        public static bool SayNoSoap()
        {
            string line = GameServices.CleanlinessSettings.NoSoapLine;
            if (string.IsNullOrEmpty(line)) return false;
            EventBus.Publish(new AmbientSpeechRequested(
                AmbientSpeechId.Next(), GameServices.PlayerTransform,
                new UnityEngine.Vector3(0f, EffortGate.Settings.BubbleHeightMetres, 0f),
                speakerId: null, speakerName: null, line: line, voiceId: null, kind: AmbientSpeechKind.InnerVoice));
            return true;
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
