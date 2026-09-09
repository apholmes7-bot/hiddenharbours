using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>
    /// <b>The landing frame's hit-stop</b> (juice charter §4.1). Self-installing, hears one cue
    /// (<see cref="JuiceMomentCue"/> of kind <c>Landing</c>) and dips <see cref="Time.timeScale"/> to
    /// <c>Juice.LandingHitStopScale</c> for <c>Juice.LandingHitStopSeconds</c> of UNSCALED time, then
    /// writes back EXACTLY the scale it found. The maths is the <see cref="HitStop"/> POCO; this is the
    /// one place in the game that writes <c>Time.timeScale</c>.
    ///
    /// <para><b>What it freezes.</b> <c>GameClock</c> integrates <c>Time.deltaTime × TimeScale</c>, so the
    /// world clock pauses for the stop — the charter's ruling is that a ~0.1 s pause is fine. Every sim
    /// step that reads <c>Time.deltaTime</c> pauses with it (reported in the PR, not changed). Feel
    /// timers — this host, the bursts, the notebook's count-ups, the lift toast — run on
    /// <c>Time.unscaledDeltaTime</c> and keep moving, which is the whole trick.</para>
    ///
    /// <para><b>The restore is the found value, not 1.</b> <c>ShellPause</c> does not use the time scale,
    /// but nothing forbids a future owner of it; a stop that ends by writing 1 would silently unpause.
    /// So the host remembers what it found and puts THAT back, and a second landing inside a stop
    /// extends the stop rather than re-capturing the dipped value.</para>
    /// </summary>
    public sealed class HitStopHost : MonoBehaviour
    {
        private readonly HitStop _stop = new HitStop();
        private float _restore = 1f;
        private bool _holding;
        private JuiceSettings? _override;

        private static bool _installed;

        /// <summary>A stop is live and this host owns the time scale until it ends.</summary>
        public bool Holding => _holding;

        /// <summary>The time scale that will be written back when the stop ends (meaningful while <see cref="Holding"/>).</summary>
        public float Restore => _restore;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("HitStopHost") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<HitStopHost>();
        }

        private JuiceSettings Settings => _override ?? (GameServices.Config != null ? GameServices.Config.Juice : JuiceSettings.Default);

        /// <summary>Tests pin the knobs without a GameConfig asset; null returns to the live config.</summary>
        public void OverrideSettings(JuiceSettings? settings) => _override = settings;

        private void OnEnable() => EventBus.Subscribe<JuiceMomentCue>(OnMoment);

        private void OnDisable()
        {
            EventBus.Unsubscribe<JuiceMomentCue>(OnMoment);
            Release();   // disabling mid-stop hands the clock back (never a stuck 5 % world)
        }

        /// <summary>Public so tests drive the same path the bus does.</summary>
        public void OnMoment(JuiceMomentCue e)
        {
            if (e.Kind != JuiceMoment.Landing) return;
            JuiceSettings j = Settings;
            if (!j.MomentsEnabled || j.LandingHitStopSeconds <= 0f) return;

            if (!_holding)
            {
                _restore = Time.timeScale;   // the found value — put back exactly, whatever it was
                _holding = true;
            }
            _stop.Trigger(j.LandingHitStopScale, j.LandingHitStopSeconds);
            Time.timeScale = _restore * _stop.Scale;
        }

        private void Update()
        {
            if (!_holding) return;   // the idle cost: one compare
            TickFrame(Time.unscaledDeltaTime);
        }

        /// <summary>One frame of the stop on the caller's UNSCALED delta. Public for tests.</summary>
        public void TickFrame(float unscaledDt)
        {
            if (!_holding) return;
            float scale = _stop.Tick(unscaledDt);
            if (!_stop.Active) { Release(); return; }
            Time.timeScale = _restore * scale;
        }

        private void Release()
        {
            if (!_holding) return;
            _holding = false;
            _stop.Reset();
            Time.timeScale = _restore;
        }
    }
}
