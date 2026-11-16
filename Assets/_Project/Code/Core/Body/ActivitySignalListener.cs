using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Work the game already announces, counted as effort</b> (body slice 2, owner ask 2026-11-15: "work
    /// related tasks burn energy faster"). Listens on the EventBus only (rule 4: it never touches Fishing,
    /// Boats or the vehicles) and turns each stretch of a signalled job into game minutes reported through the
    /// one door, <see cref="IBodyService.ReportActivity"/>, against the <see cref="ActivityDef"/> bound to
    /// that signal (<see cref="ActivityDef.Signal"/>; data picks the def, not this class).
    ///
    /// <para><b>The jobs it can hear</b>: a rod fight or hand-gather tend (<see cref="FishingStateChanged"/>,
    /// <see cref="FishingState.IsFightPhase"/>), a pot coming up (<see cref="TrapHaulStateChanged"/>,
    /// <see cref="TrapHaulPhase.Hauling"/>), working a pot on the stern deck (<see cref="DeckLoopStateChanged"/>,
    /// any stage but None) and driving (<see cref="ControlModeChanged"/>, <see cref="ControlMode.Driving"/>).
    /// A stretch is reported when it ends, from the game clock (deterministic: the same play gives the same
    /// minutes), capped at <see cref="StrengthSettings.MaxSignalWindowGameMinutes"/>.</para>
    ///
    /// <para>Installed once by the game clock; <see cref="StrengthSettings.ReportWorkFromSignals"/> off makes
    /// it count nothing.</para>
    /// </summary>
    public sealed class ActivitySignalListener
    {
        private static ActivitySignalListener _installed;

        private readonly Func<IBodyService> _body;
        private readonly Func<IGameClock> _clock;
        private readonly Func<StrengthSettings> _settings;
        private readonly Func<ActivitySignal, ActivityDef> _resolve;
        private readonly Func<double> _secondsPerGameDay;

        // One open stretch per signal (index = (int)ActivitySignal); negative = not open.
        private readonly double[] _openedAt = { -1d, -1d, -1d, -1d, -1d };
        private bool _subscribed;

        public ActivitySignalListener(Func<IBodyService> body = null, Func<IGameClock> clock = null,
                                      Func<StrengthSettings> settings = null,
                                      Func<ActivitySignal, ActivityDef> resolve = null,
                                      Func<double> secondsPerGameDay = null)
        {
            _body = body ?? (() => GameServices.Body);
            _clock = clock ?? (() => GameServices.Clock);
            _settings = settings ?? (() => GameServices.StrengthSettings);
            _resolve = resolve ?? (signal => ActivityCatalog.ForSignal(signal));
            _secondsPerGameDay = secondsPerGameDay ?? (() => GameServices.SecondsPerDay);
        }

        /// <summary>Install the game's listener (idempotent).</summary>
        public static void Install()
        {
            _installed ??= new ActivitySignalListener();
            _installed.Subscribe();
        }

        /// <summary>Remove the game's listener (idempotent). Open stretches are dropped, not reported.</summary>
        public static void Uninstall()
        {
            _installed?.Unsubscribe();
            _installed = null;
        }

        public void Subscribe()
        {
            if (_subscribed) return;
            EventBus.Subscribe<FishingStateChanged>(OnFishing);
            EventBus.Subscribe<TrapHaulStateChanged>(OnTrapHaul);
            EventBus.Subscribe<DeckLoopStateChanged>(OnDeckLoop);
            EventBus.Subscribe<ControlModeChanged>(OnControlMode);
            _subscribed = true;
        }

        public void Unsubscribe()
        {
            if (!_subscribed) return;
            EventBus.Unsubscribe<FishingStateChanged>(OnFishing);
            EventBus.Unsubscribe<TrapHaulStateChanged>(OnTrapHaul);
            EventBus.Unsubscribe<DeckLoopStateChanged>(OnDeckLoop);
            EventBus.Unsubscribe<ControlModeChanged>(OnControlMode);
            _subscribed = false;
            for (int i = 0; i < _openedAt.Length; i++) _openedAt[i] = -1d;
        }

        private void OnFishing(FishingStateChanged e) => Mark(ActivitySignal.FishFight, e.State.IsFightPhase);

        private void OnTrapHaul(TrapHaulStateChanged e) => Mark(ActivitySignal.TrapHaul, e.State.Phase == TrapHaulPhase.Hauling);

        // The deck's Hauling stage is the pot haul TrapHaulStateChanged already counts: never both.
        private void OnDeckLoop(DeckLoopStateChanged e) =>
            Mark(ActivitySignal.DeckWork, e.State.Stage != SternDeckStage.None && e.State.Stage != SternDeckStage.Hauling);

        private void OnControlMode(ControlModeChanged e) => Mark(ActivitySignal.Driving, e.Mode == ControlMode.Driving);

        /// <summary>Open the stretch when the job starts, report it when it stops; repeats change nothing.</summary>
        private void Mark(ActivitySignal signal, bool active)
        {
            IGameClock clock = _clock();
            if (clock == null) return;
            int i = (int)signal;
            double now = clock.TotalSeconds;
            if (active)
            {
                if (_openedAt[i] < 0d) _openedAt[i] = now;
                return;
            }
            if (_openedAt[i] < 0d) return;
            double from = _openedAt[i];
            _openedAt[i] = -1d;

            StrengthSettings st = _settings();
            if (!st.ReportWorkFromSignals) return;
            double perMinute = _secondsPerGameDay() / (24d * 60d);
            if (!(perMinute > 0d) || now <= from) return;
            double minutes = Math.Min((now - from) / perMinute, Math.Max(1f, st.MaxSignalWindowGameMinutes));
            ActivityDef def = _resolve(signal);
            if (def == null) return;
            _body()?.ReportActivity(def, (float)minutes);
        }
    }
}
