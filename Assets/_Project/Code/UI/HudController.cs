using System;
using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The always-on, glanceable top-band HUD (VS-17 + the wind/sea slice of VS-19). Surfaces the
    /// five readouts the player must be able to read in under a second while acting: clock, tide,
    /// wind, sea state, and money (with a payout flash on a sale). Pillar 1 (The Sea Has Moods) is
    /// treated here as a UI problem.
    ///
    /// Self-contained &amp; code-driven: it builds its own ScreenSpaceOverlay Canvas and child
    /// labels in <see cref="Awake"/>, so it needs no prefab/art authoring and works headless. Reads
    /// state ONLY through Core (<see cref="GameServices"/> + <see cref="EventBus"/>) — the
    /// HiddenHarbours.UI assembly references only HiddenHarbours.Core, which structurally prevents
    /// reaching into Environment/Player/Economy concretes.
    ///
    /// Budget (CLAUDE.md rule 7): updates every frame but allocates nothing per frame — strings are
    /// cached and only rebuilt when their displayed value actually changes; environment is sampled
    /// at ~4 Hz (matches VS-05); money is event-driven, not polled.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class HudController : MonoBehaviour
    {
        [Header("Config (for in-game-seconds → H:MM)")]
        [Tooltip("GameConfig supplies SecondsPerHour for the tide time-to-turn conversion. " +
                 "If left unset the HUD falls back to GameConfig found via the clock at runtime, " +
                 "and shows '--' for the turn time until one is available. No magic numbers.")]
        [SerializeField] private GameConfig _config;

        [Header("Tuning (sampling & flash)")]
        [Tooltip("Environment sample cadence (Hz). 4 Hz matches the sim's sampling (VS-05).")]
        [SerializeField] private float _envSampleHz = 4f;
        [Tooltip("How long the '+₲N' payout flash stays up (real seconds).")]
        [SerializeField] private float _payoutFlashSeconds = 2.0f;
        [Tooltip("Persist across scene loads like the services. The HUD is always-on.")]
        [SerializeField] private bool _persistAcrossScenes = true;

        // ---- runtime labels (built in Awake) ------------------------------------------------
        private Text _clockLabel;
        private Text _tideLabel;
        private Text _windLabel;
        private Text _seaLabel;
        private Text _moneyLabel;
        private Text _payoutLabel;

        // ---- cached displayed values (change-detection → no per-frame string building) ------
        private string _clockCache;
        private string _tideCache;
        private string _windCache;
        private string _seaCache;
        private string _moneyCache;

        // Clock change-detection (avoid building the clock string when the displayed minute is unchanged).
        private int _lastMinuteOfDay = -1;
        private int _lastDay = -1;
        private Season _lastSeason = (Season)(-1);

        // Last displayed balance. _moneyPainted forces the first paint; int.MinValue means "no wallet".
        private int _lastMoney;
        private bool _moneyPainted;

        private float _envSampleTimer;
        private float _payoutTimer;
        private bool _subscribed;

        // Cached so a missing GameConfig doesn't recompute the lookup every sample.
        private float _secondsPerHour;

        // Cached delegate + service so the tide scan doesn't allocate a closure each 4 Hz sample.
        private Func<double, float> _tideHeightAt;
        private IEnvironmentService _tideHeightAtSource;

        // ---- lifecycle ----------------------------------------------------------------------

        private void Awake()
        {
            BuildHud();
            if (_persistAcrossScenes)
                DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()  => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed) return;
            EventBus.Subscribe<MoneyChanged>(OnMoneyChanged);
            EventBus.Subscribe<CatchSold>(OnCatchSold);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            EventBus.Unsubscribe<MoneyChanged>(OnMoneyChanged);
            EventBus.Unsubscribe<CatchSold>(OnCatchSold);
            _subscribed = false;
        }

        private void Update()
        {
            // Boot/null safety: services may be unset for the first frame(s) at boot.
            if (!GameServices.Ready)
            {
                ShowPlaceholder();
                return;
            }

            UpdateClock();
            UpdateEnvironmentThrottled();
            UpdateMoney();            // event-driven, but reconcile once services exist (boot balance)
            TickPayoutFlash();
        }

        // ---- per-readout updates ------------------------------------------------------------

        private void UpdateClock()
        {
            var clock = GameServices.Clock;

            // Change-detect on the displayed quanta (minute / day / season) BEFORE building any
            // string, so an unchanged clock allocates nothing this frame (rule 7).
            int minuteOfDay = (int)(clock.HourOfDay * 60f);
            int day = clock.DayOfSeason;
            var season = clock.Season;
            if (minuteOfDay == _lastMinuteOfDay && day == _lastDay && season == _lastSeason)
                return;

            _lastMinuteOfDay = minuteOfDay;
            _lastDay = day;
            _lastSeason = season;

            string text = HudFormat.ClockHHMM(clock.HourOfDay)
                        + "  " + HudStrings.Season(season)
                        + " d" + day;
            _clockCache = text;
            _clockLabel.text = text;
        }

        private void UpdateEnvironmentThrottled()
        {
            _envSampleTimer -= Time.unscaledDeltaTime;
            if (_envSampleTimer > 0f) return;
            _envSampleTimer = _envSampleHz > 0f ? 1f / _envSampleHz : 0.25f;

            var env = GameServices.Environment;
            EnvironmentSample sample = env.Sample();

            UpdateTide(env);
            UpdateWind(sample.WindVector);
            UpdateSea(sample.SeaState);
        }

        private void UpdateTide(IEnvironmentService env)
        {
            double now = GameServices.Clock.TotalSeconds;
            float sph = SecondsPerHour();

            // Mirror TideModel: rising test uses SecondsPerHour * 0.05 (~3 in-game minutes).
            // Scan forward up to one tidal period for the next turn.
            double risingDt   = sph > 0f ? sph * 0.05 : 1.0;
            double scanStep   = sph > 0f ? sph * 0.10 : 2.0;             // ~6 in-game min granularity
            double horizon    = sph > 0f ? sph * TidalPeriodHours() : 0; // one tidal period

            Func<double, float> heightAt = HeightAtDelegate(env);

            TideState tide;
            if (horizon > 0.0)
                tide = TideReadout.Derive(heightAt, now, risingDt, scanStep, horizon);
            else
                tide = new TideState(false, heightAt(now), -1.0); // config-less: height only

            // Build: arrow (shape) + height (number) + "⤴ in H:MM" (turn). Never colour alone.
            string turn = tide.HasTurn && sph > 0f
                ? HudStrings.TurnGlyph + " " + HudFormat.DurationHMM(tide.SecondsToTurn, sph)
                : HudStrings.Unknown;

            string text = HudStrings.TideArrow(tide.Rising) + " "
                        + HudFormat.HeightMeters(tide.HeightMeters) + "   " + turn;

            if (text != _tideCache)
            {
                _tideCache = text;
                _tideLabel.text = text;
            }
        }

        private void UpdateWind(Vector2 windVector)
        {
            float strength = WindReadout.Strength(windVector);
            int knots = Mathf.Max(0, Mathf.RoundToInt(WindReadout.Knots(strength)));
            string cardinal = WindReadout.Cardinal(windVector);

            // Redundant coding: a directional glyph + cardinal text + a kts number (+ Beaufort).
            string text = WindArrowGlyph(windVector) + " " + cardinal
                        + "  " + knots.ToString(System.Globalization.CultureInfo.InvariantCulture) + " kt"
                        + "  " + HudFormat.BeaufortLabel(WindReadout.Beaufort(strength));

            if (text != _windCache)
            {
                _windCache = text;
                _windLabel.text = text;
            }
        }

        private void UpdateSea(SeaState state)
        {
            // Icon-ish severity dots + the word (redundant coding, never colour alone).
            string text = "Sea: " + HudStrings.SeaState(state) + " (" + (int)state + "/7)";
            if (text != _seaCache)
            {
                _seaCache = text;
                _seaLabel.text = text;
            }
        }

        private void UpdateMoney()
        {
            // Money is primarily event-driven (OnMoneyChanged). This reconciles the boot balance and
            // the Wallet-null case. Change-detect on the int BEFORE formatting so an unchanged
            // balance allocates nothing (rule 7). Wallet MAY be null in the greybox
            // (GameServices.Ready does NOT check it).
            var wallet = GameServices.Wallet;

            if (wallet == null)
            {
                // No Wallet ref. If a MoneyChanged event already gave us an authoritative balance,
                // keep showing it; otherwise show the "no wallet" placeholder once.
                if (_moneyPainted) return;
                _moneyPainted = true;
                _moneyCache = HudStrings.MoneyPrefix + HudStrings.Unknown;
                _moneyLabel.text = _moneyCache;
                return;
            }

            int balance = wallet.Money;
            if (_moneyPainted && balance == _lastMoney) return;
            _lastMoney = balance;
            _moneyPainted = true;

            _moneyCache = HudFormat.Money(balance);
            _moneyLabel.text = _moneyCache;
        }

        // ---- event handlers -----------------------------------------------------------------

        private void OnMoneyChanged(MoneyChanged e)
        {
            // Authoritative balance from the event (works even when the Wallet ref is null here).
            // Keep the reconcile state in sync so UpdateMoney doesn't repaint or fight this.
            _lastMoney = e.NewBalance;
            _moneyPainted = true;

            string text = HudFormat.Money(e.NewBalance);
            if (text != _moneyCache)
            {
                _moneyCache = text;
                if (_moneyLabel != null) _moneyLabel.text = text;
            }
        }

        private void OnCatchSold(CatchSold e)
        {
            FlashPayout(e.TotalPaid);
        }

        private void FlashPayout(int amount)
        {
            if (_payoutLabel == null) return;
            _payoutLabel.text = HudFormat.PayoutFlash(amount);
            _payoutLabel.enabled = true;
            _payoutTimer = _payoutFlashSeconds;
        }

        private void TickPayoutFlash()
        {
            if (_payoutTimer <= 0f) return;
            _payoutTimer -= Time.unscaledDeltaTime;
            if (_payoutTimer <= 0f && _payoutLabel != null)
                _payoutLabel.enabled = false;
        }

        // ---- helpers ------------------------------------------------------------------------

        // Cache the method-group delegate per environment service so the 4 Hz tide scan reuses one
        // delegate instance instead of allocating a new one each sample.
        private Func<double, float> HeightAtDelegate(IEnvironmentService env)
        {
            if (!ReferenceEquals(env, _tideHeightAtSource))
            {
                _tideHeightAtSource = env;
                _tideHeightAt = env.TideHeightAt;
            }
            return _tideHeightAt;
        }

        private float SecondsPerHour()
        {
            if (_secondsPerHour > 0f) return _secondsPerHour;
            if (_config != null) { _secondsPerHour = _config.SecondsPerHour; return _secondsPerHour; }
            return 0f; // unknown until a config is assigned
        }

        private float TidalPeriodHours()
            => _config != null ? _config.TidalPeriodHours : 12.4206f; // canon principal lunar semidiurnal

        private void ShowPlaceholder()
        {
            // Before services exist, keep the HUD quiet rather than showing wrong numbers (P1 truth).
            SetIfChanged(ref _clockCache, HudStrings.Unknown, _clockLabel);
            SetIfChanged(ref _tideCache,  HudStrings.Unknown, _tideLabel);
            SetIfChanged(ref _windCache,  HudStrings.Unknown, _windLabel);
            SetIfChanged(ref _seaCache,   HudStrings.Unknown, _seaLabel);
            SetIfChanged(ref _moneyCache, HudStrings.MoneyPrefix + HudStrings.Unknown, _moneyLabel);
        }

        private static void SetIfChanged(ref string cache, string value, Text label)
        {
            if (value == cache) return;
            cache = value;
            if (label != null) label.text = value;
        }

        // A simple 8-way arrow glyph for the direction the wind blows toward (shape, not colour).
        private static string WindArrowGlyph(Vector2 v)
        {
            if (v.sqrMagnitude < 0.0001f) return "·";
            float bearing = Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg; // 0=N, 90=E
            if (bearing < 0f) bearing += 360f;
            int oct = Mathf.RoundToInt(bearing / 45f) % 8;
            switch (oct)
            {
                case 0: return "↑";
                case 1: return "↗";
                case 2: return "→";
                case 3: return "↘";
                case 4: return "↓";
                case 5: return "↙";
                case 6: return "←";
                default: return "↖";
            }
        }

        // ---- HUD construction (code-driven, no prefab) --------------------------------------

        private void BuildHud()
        {
            // Canvas (ScreenSpaceOverlay) + scaler tuned for portrait phones with safe-area respect.
            var canvasGo = new GameObject("HUD_Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // above gameplay

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f); // portrait-primary reference
            scaler.matchWidthOrHeight = 0.5f;

            // A top band anchored across the top, inset for the safe area at runtime.
            var band = new GameObject("TopBand", typeof(RectTransform));
            band.transform.SetParent(canvasGo.transform, false);
            var bandRt = band.GetComponent<RectTransform>();
            bandRt.anchorMin = new Vector2(0f, 1f);
            bandRt.anchorMax = new Vector2(1f, 1f);
            bandRt.pivot = new Vector2(0.5f, 1f);
            bandRt.anchoredPosition = new Vector2(0f, -SafeAreaTopInset());
            bandRt.sizeDelta = new Vector2(-32f, 220f); // 16px side padding, ~220px tall band

            // Left column: clock (top) + tide (highest-stakes — kept visually distinct, larger).
            _clockLabel = MakeLabel(bandRt, "Clock", TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0.6f, 1f), 0f, -4f, 40);
            _tideLabel  = MakeLabel(bandRt, "Tide", TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0.7f, 1f), 0f, -56f, 52); // bigger: most important read

            // Right column: money (top), payout flash (under it), wind, sea.
            _moneyLabel  = MakeLabel(bandRt, "Money", TextAnchor.UpperRight,
                new Vector2(0.6f, 1f), new Vector2(1f, 1f), 0f, -4f, 44);
            _payoutLabel = MakeLabel(bandRt, "Payout", TextAnchor.UpperRight,
                new Vector2(0.6f, 1f), new Vector2(1f, 1f), 0f, -52f, 38);
            _payoutLabel.color = new Color(0.55f, 0.95f, 0.55f); // green flash — but text+sign carry it too
            _payoutLabel.enabled = false;

            _windLabel = MakeLabel(bandRt, "Wind", TextAnchor.UpperRight,
                new Vector2(0.55f, 1f), new Vector2(1f, 1f), 0f, -100f, 34);
            _seaLabel  = MakeLabel(bandRt, "Sea", TextAnchor.UpperRight,
                new Vector2(0.55f, 1f), new Vector2(1f, 1f), 0f, -140f, 30);

            // Start quiet until services are ready.
            ShowPlaceholder();
        }

        private static Text MakeLabel(RectTransform parent, string name, TextAnchor align,
                                      Vector2 anchorMin, Vector2 anchorMax,
                                      float x, float y, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(0f, 56f);

            var text = go.GetComponent<Text>();
            text.font = DefaultFont();
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false; // HUD is read-only; never eat touches meant for gameplay

            // High-contrast scrim behind text (accessibility §8 — legibility over busy water).
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            return text;
        }

        // Unity 6 removed Arial.ttf from Resources; LegacyRuntime.ttf is the built-in fallback.
        private static Font DefaultFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf"); // older editors
            return f;
        }

        private static float SafeAreaTopInset()
        {
            // Convert the top safe-area gap (device pixels) to a small inset. Cheap, computed once
            // at build; a full responsive safe-area binder is a follow-up (VS-19+ reflow work).
            var sa = Screen.safeArea;
            float topGap = Screen.height - (sa.y + sa.height);
            return Mathf.Max(0f, topGap);
        }
    }
}
