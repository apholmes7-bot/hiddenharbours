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

        // Which wash used soap: the station id and the stretch start it began at. Session-only on purpose: a
        // wash never survives a load (CleanlinessMath.Rebase ends it), and the soap it used is already spent
        // in the saved supply locker, so nothing about a soaped wash needs saving. Matching the stretch start
        // too means a flag left from an earlier wash can never soap a later one.
        private string _soapedStationId;
        private double _soapedSince = NeedStateDto.Unstamped;

        // Clothing (store clothing, pass 1): the dirt scale his worn garments put on a stretch of work, taken
        // as the stretch BEGINS and kept for that stretch, so a stretch is one line at one rate and a change of
        // clothes takes effect from the next one. Session-only on purpose: a load ends every stretch
        // (CleanlinessMath.Rebase), so a snapshot never outlives the stretch it was taken for.
        private readonly Func<IClothingService> _clothing;
        private DirtActivity _clothedKind;
        private double _clothedSince = NeedStateDto.Unstamped;
        private float _clothedScale = 1f;
        private DirtSourceRate[] _scaledFrom, _scaledSources;
        private DirtActivity _scaledKind;
        private float _scaledBy = 1f;

        public CleanlinessService(Func<ISaveService> save = null, Func<IGameClock> clock = null,
                                  Func<CleanlinessSettings> settings = null, Func<WashStationDef[]> catalog = null,
                                  Func<double> secondsPerGameHour = null,
                                  Func<IClothingService> clothing = null)
        {
            _clothing = clothing ?? (() => GameServices.Clothing);
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
                CleanlinessSettings s = Live(state);
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

        public bool WashSoaped
        {
            get
            {
                PlayerCleanlinessDto state = State();
                return state != null && IsSoaped(state);
            }
        }

        public bool HasSoap => SoapStock.Has(_save()?.Current, _settings());

        public bool SoapMatters(WashStationDef station) => CleanlinessMath.SoapMatters(station, _settings());

        public float CeilingFor(WashStationDef station)
        {
            if (station == null) return 0f;
            CleanlinessSettings s = _settings();
            PlayerCleanlinessDto state = State();
            bool soaped = state != null && state.IsWashing &&
                          string.Equals(state.WashStationId, station.Id, StringComparison.Ordinal)
                ? IsSoaped(state)
                : SoapStock.Has(_save()?.Current, s);
            return CleanlinessMath.CeilingOf(station, s, soaped);
        }

        public void BeginActivity(DirtActivity kind)
        {
            if (kind == DirtActivity.None) return;
            PlayerCleanlinessDto state = State();
            if (state == null) return;
            if (!state.IsWashing && state.ActivityKind == kind && state.StretchSinceGameSeconds >= 0d) return;
            WashStationDef was = state.IsWashing ? FindStation(state.WashStationId) : null;
            bool wasSoaped = IsSoaped(state);
            double now = SettleNow(state);
            state.WashStationId = "";
            state.Activity = (int)kind;
            state.StretchSinceGameSeconds = now;
            _clothedKind = kind;
            _clothedSince = now;
            _clothedScale = WornScale(kind);
            ForgetSoap();
            if (was != null) EventBus.Publish(new PlayerWashChanged(false, was.Kind, was.Id, wasSoaped, false));
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
            float bump = CleanlinessMath.BumpOf(kind, _settings()) * WornScale(kind);
            if (!(bump > 0f)) return;
            SettleNow(state);
            state.Dirt = CleanlinessMath.Rise(state.Dirt, bump);
        }

        public void ReportActivity(DirtActivity kind, double gameHours)
        {
            if (!(gameHours > 0d) || double.IsInfinity(gameHours)) return;
            PlayerCleanlinessDto state = State();
            if (state == null) return;
            float rate = CleanlinessMath.RateOf(kind, _settings()) * WornScale(kind);
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
            bool wasSoaped = IsSoaped(state);
            double now = SettleNow(state);   // the old wash settles at its own soap before the new one starts
            state.Activity = 0;
            state.WashStationId = station.Id ?? "";
            state.StretchSinceGameSeconds = now;

            // Soap: only where it changes the ceiling, one wash's worth, taken as the wash begins.
            CleanlinessSettings s = _settings();
            bool matters = CleanlinessMath.SoapMatters(station, s);
            bool soaped = matters && SoapStock.TryUseWash(_save()?.Current, s);
            if (soaped) { _soapedStationId = state.WashStationId; _soapedSince = now; }
            else ForgetSoap();

            if (was != null) EventBus.Publish(new PlayerWashChanged(false, was.Kind, was.Id, wasSoaped, false));
            EventBus.Publish(new PlayerWashChanged(true, station.Kind, station.Id, soaped, matters && !soaped));
        }

        public void EndWash()
        {
            PlayerCleanlinessDto state = State();
            if (state == null || !state.IsWashing) return;
            WashStationDef was = FindStation(state.WashStationId);
            bool wasSoaped = IsSoaped(state);
            SettleNow(state);
            string id = state.WashStationId;
            state.WashStationId = "";
            state.StretchSinceGameSeconds = NeedStateDto.Unstamped;
            ForgetSoap();
            EventBus.Publish(new PlayerWashChanged(false, was != null ? was.Kind : WashStationKind.Sink, id, wasSoaped, false));
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
            CleanlinessMath.Settle(state, Live(state), PullOf(state), clock.TotalSeconds, _secondsPerGameHour());
        }

        public void Tick()
        {
            PlayerCleanlinessDto state = State();
            if (state == null) return;
            IGameClock clock = _clock();
            if (clock == null) return;
            double now = clock.TotalSeconds;
            if (!state.IsStamped) { state.SinceGameSeconds = now; return; }

            CleanlinessSettings s = Live(state);
            double sph = _secondsPerGameHour();
            if (!CleanlinessMath.StretchExpired(state, s, now, sph)) return;
            // Past the guard window the stretch adds nothing, so closing it changes no level.
            WashStationDef was = state.IsWashing ? FindStation(state.WashStationId) : null;
            bool wasSoaped = IsSoaped(state);
            CleanlinessMath.Settle(state, s, PullOf(state), now, sph);
            string id = state.WashStationId;
            state.Activity = 0;
            state.WashStationId = "";
            state.StretchSinceGameSeconds = NeedStateDto.Unstamped;
            ForgetSoap();
            if (!string.IsNullOrEmpty(id))
                EventBus.Publish(new PlayerWashChanged(false, was != null ? was.Kind : WashStationKind.Sink, id, wasSoaped, false));
        }

        /// <summary>Settle at the clock now (anchoring a fresh state there) and return that instant.</summary>
        private double SettleNow(PlayerCleanlinessDto state)
        {
            IGameClock clock = _clock();
            double now = clock != null ? clock.TotalSeconds : (state.IsStamped ? state.SinceGameSeconds : 0d);
            if (state.IsStamped)
                CleanlinessMath.Settle(state, Live(state), PullOf(state), now, _secondsPerGameHour());
            else
                state.SinceGameSeconds = now;
            return now;
        }

        /// <summary>What is left of the dirt <paramref name="kind"/> adds with what the local player wears now:
        /// exactly 1 with nothing on (store clothing, pass 1).</summary>
        private float WornScale(DirtActivity kind)
        {
            PlayerWardrobe worn = _clothing()?.Local;
            return worn != null ? worn.DirtScale(kind) : 1f;
        }

        /// <summary>
        /// The settings the saved line runs on: the config's, with the current stretch's rate scaled by the
        /// clothes he had on when it began. The config's own settings, untouched, whenever no clothed stretch
        /// is going on (so a player with no clothes reads bit for bit what he read before). The scaled rows
        /// are rebuilt only when the stretch, its scale or the config's rows change, never per read.
        /// </summary>
        private CleanlinessSettings Live(PlayerCleanlinessDto state)
        {
            CleanlinessSettings s = _settings();
            if (state == null || state.IsWashing || !(_clothedScale < 1f) || s.Sources == null) return s;
            if (state.ActivityKind != _clothedKind || state.StretchSinceGameSeconds != _clothedSince) return s;
            if (_scaledSources == null || _scaledKind != _clothedKind || _scaledBy != _clothedScale ||
                !SameRows(s.Sources, _scaledFrom))
            {
                _scaledFrom = (DirtSourceRate[])s.Sources.Clone();
                _scaledSources = (DirtSourceRate[])s.Sources.Clone();
                for (int i = 0; i < _scaledSources.Length; i++)
                    if (_scaledSources[i].Kind == _clothedKind) _scaledSources[i].RisePerGameHour *= _clothedScale;
                _scaledKind = _clothedKind;
                _scaledBy = _clothedScale;
            }
            s.Sources = _scaledSources;
            return s;
        }

        private static bool SameRows(DirtSourceRate[] a, DirtSourceRate[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].Kind != b[i].Kind || a[i].RisePerGameHour != b[i].RisePerGameHour ||
                    a[i].PerAction != b[i].PerAction) return false;
            return true;
        }

        /// <summary>The pull of the wash going on: its station, up to the ceiling its soap allows.</summary>
        private WashPull PullOf(PlayerCleanlinessDto state)
        {
            if (!state.IsWashing) return default;
            WashStationDef station = FindStation(state.WashStationId);
            return CleanlinessMath.PullOf(station, CleanlinessMath.CeilingOf(station, _settings(), IsSoaped(state)));
        }

        /// <summary>True when the wash going on in <paramref name="state"/> is the one that used soap.</summary>
        private bool IsSoaped(PlayerCleanlinessDto state)
            => state.IsWashing && _soapedStationId != null &&
               string.Equals(state.WashStationId, _soapedStationId, StringComparison.Ordinal) &&
               state.StretchSinceGameSeconds == _soapedSince;

        private void ForgetSoap()
        {
            _soapedStationId = null;
            _soapedSince = NeedStateDto.Unstamped;
        }

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
