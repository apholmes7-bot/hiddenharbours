using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b><see cref="ICleanlinessService"/> over the save, the clock, the config and the shipped stations</b>
    /// (cleanliness slice 1). The <see cref="NeedsService"/> shape: every seam is injectable so a test drives
    /// it with its own save, clock, numbers and stations; each defaults to its <see cref="GameServices"/>
    /// accessor, read at each call.
    ///
    /// <para><b>Nothing ticks a level.</b> Every change first settles the saved line at the clock now, then
    /// starts the new one, so a change is exact whenever it happens and the slow tick's cadence is never in
    /// the arithmetic.</para>
    /// </summary>
    public sealed class CleanlinessService : ICleanlinessService
    {
        /// <summary>The folder under any <c>Resources</c> root the shipped stations live in.</summary>
        public const string ResourcesFolder = "WashStations";

        private readonly Func<ISaveService> _save;
        private readonly Func<IGameClock> _clock;
        private readonly Func<CleanlinessSettings> _settings;
        private readonly Func<WashStationDef[]> _catalogSource;
        private readonly Func<double> _secondsPerGameHour;
        private WashStationDef[] _catalog;

        public CleanlinessService(Func<ISaveService> save = null, Func<IGameClock> clock = null,
                                  Func<CleanlinessSettings> settings = null, Func<WashStationDef[]> catalog = null,
                                  Func<double> secondsPerGameHour = null)
        {
            _save = save ?? (() => GameServices.Save);
            _clock = clock ?? (() => GameServices.Clock);
            _settings = settings ?? (() => GameServices.CleanlinessSettings);
            _catalogSource = catalog ?? (() => Resources.LoadAll<WashStationDef>(ResourcesFolder));
            _secondsPerGameHour = secondsPerGameHour ?? (() => GameServices.SecondsPerDay / 24d);
        }

        public float Dirt
        {
            get
            {
                PlayerCleanlinessDto state = State();
                if (state == null) return 0f;
                CleanlinessSettings s = _settings();
                return CleanlinessMath.DirtAt(state, s, PullOf(state), NowOr(state.SinceGameSeconds),
                                              _secondsPerGameHour());
            }
        }

        public DirtActivity Activity
        {
            get
            {
                PlayerCleanlinessDto state = State();
                return state == null || state.IsWashing ? DirtActivity.None : state.ActivityKind;
            }
        }

        public WashStationDef WashingAt
        {
            get
            {
                PlayerCleanlinessDto state = State();
                return state != null && state.IsWashing ? FindStation(state.WashStationId) : null;
            }
        }

        public void BeginActivity(DirtActivity kind)
        {
            if (kind == DirtActivity.None) return;
            PlayerCleanlinessDto state = State();
            if (state == null) return;
            if (!state.IsWashing && state.ActivityKind == kind && state.StretchSinceGameSeconds >= 0d) return;
            WashStationDef was = state.IsWashing ? FindStation(state.WashStationId) : null;
            double now = SettleNow(state);
            state.WashStationId = "";
            state.Activity = (int)kind;
            state.StretchSinceGameSeconds = now;
            if (was != null) EventBus.Publish(new PlayerWashChanged(false, was.Kind, was.Id));
        }

        public void EndActivity(DirtActivity kind)
        {
            PlayerCleanlinessDto state = State();
            if (state == null || state.IsWashing || state.ActivityKind != kind || kind == DirtActivity.None) return;
            SettleNow(state);
            state.Activity = 0;
            state.StretchSinceGameSeconds = NeedStateDto.Unstamped;
        }

        public void ReportAction(DirtActivity kind)
        {
            PlayerCleanlinessDto state = State();
            if (state == null) return;
            float bump = CleanlinessMath.BumpOf(kind, _settings());
            if (!(bump > 0f)) return;
            SettleNow(state);
            state.Dirt = CleanlinessMath.Rise(state.Dirt, bump);
        }

        public void ReportActivity(DirtActivity kind, double gameHours)
        {
            if (!(gameHours > 0d) || double.IsInfinity(gameHours)) return;
            PlayerCleanlinessDto state = State();
            if (state == null) return;
            float rate = CleanlinessMath.RateOf(kind, _settings());
            if (!(rate > 0f) || float.IsInfinity(rate)) return;
            SettleNow(state);
            state.Dirt = CleanlinessMath.Rise(state.Dirt, rate * gameHours);
        }

        public void BeginWash(WashStationDef station)
        {
            if (station == null || !station.Washes) return;
            PlayerCleanlinessDto state = State();
            if (state == null) return;
            if (string.Equals(state.WashStationId, station.Id, StringComparison.Ordinal)) return;
            WashStationDef was = state.IsWashing ? FindStation(state.WashStationId) : null;
            double now = SettleNow(state);
            state.Activity = 0;
            state.WashStationId = station.Id ?? "";
            state.StretchSinceGameSeconds = now;
            if (was != null) EventBus.Publish(new PlayerWashChanged(false, was.Kind, was.Id));
            EventBus.Publish(new PlayerWashChanged(true, station.Kind, station.Id));
        }

        public void EndWash()
        {
            PlayerCleanlinessDto state = State();
            if (state == null || !state.IsWashing) return;
            WashStationDef was = FindStation(state.WashStationId);
            SettleNow(state);
            string id = state.WashStationId;
            state.WashStationId = "";
            state.StretchSinceGameSeconds = NeedStateDto.Unstamped;
            EventBus.Publish(new PlayerWashChanged(false, was != null ? was.Kind : WashStationKind.Sink, id));
        }

        public WashStationDef FindStation(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            WashStationDef[] all = Catalog();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && string.Equals(all[i].Id, id, StringComparison.Ordinal)) return all[i];
            return null;
        }

        public void Settle()
        {
            PlayerCleanlinessDto state = State();
            if (state == null || !state.IsStamped) return;
            IGameClock clock = _clock();
            if (clock == null) return;
            CleanlinessMath.Settle(state, _settings(), PullOf(state), clock.TotalSeconds, _secondsPerGameHour());
        }

        public void Tick()
        {
            PlayerCleanlinessDto state = State();
            if (state == null) return;
            IGameClock clock = _clock();
            if (clock == null) return;
            double now = clock.TotalSeconds;
            if (!state.IsStamped) { state.SinceGameSeconds = now; return; }

            CleanlinessSettings s = _settings();
            double sph = _secondsPerGameHour();
            if (!CleanlinessMath.StretchExpired(state, s, now, sph)) return;
            // Past the guard window the stretch adds nothing, so closing it changes no level.
            WashStationDef was = state.IsWashing ? FindStation(state.WashStationId) : null;
            CleanlinessMath.Settle(state, s, PullOf(state), now, sph);
            string id = state.WashStationId;
            state.Activity = 0;
            state.WashStationId = "";
            state.StretchSinceGameSeconds = NeedStateDto.Unstamped;
            if (!string.IsNullOrEmpty(id))
                EventBus.Publish(new PlayerWashChanged(false, was != null ? was.Kind : WashStationKind.Sink, id));
        }

        /// <summary>Settle at the clock now (anchoring a fresh state there) and return that instant.</summary>
        private double SettleNow(PlayerCleanlinessDto state)
        {
            IGameClock clock = _clock();
            double now = clock != null ? clock.TotalSeconds : (state.IsStamped ? state.SinceGameSeconds : 0d);
            if (state.IsStamped)
                CleanlinessMath.Settle(state, _settings(), PullOf(state), now, _secondsPerGameHour());
            else
                state.SinceGameSeconds = now;
            return now;
        }

        private WashPull PullOf(PlayerCleanlinessDto state)
            => state.IsWashing ? CleanlinessMath.PullOf(FindStation(state.WashStationId)) : default;

        private double NowOr(double fallback)
        {
            IGameClock clock = _clock();
            return clock != null ? clock.TotalSeconds : fallback;
        }

        private WashStationDef[] Catalog() => _catalog ??= _catalogSource() ?? new WashStationDef[0];

        private PlayerCleanlinessDto State()
        {
            SaveData data = _save()?.Current;
            if (data == null) return null;
            return data.PlayerCleanliness ??= new PlayerCleanlinessDto();
        }
    }
}
