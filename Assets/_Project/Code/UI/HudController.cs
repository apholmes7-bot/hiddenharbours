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
        [Tooltip("How long the catch celebration card stays up before fading out (real seconds).")]
        [SerializeField] private float _catchCardSeconds = 1.5f;
        [Tooltip("Persist across scene loads like the services. The HUD is always-on.")]
        [SerializeField] private bool _persistAcrossScenes = true;

        // ---- runtime labels (built in Awake) ------------------------------------------------
        private Text _clockLabel;
        private Text _tideLabel;
        private Text _windLabel;
        private Text _seaLabel;
        private Text _moneyLabel;
        private Text _payoutLabel;
        private Text _catchCardLabel;       // brief celebratory card on a landed fish (VS-14)
        private Outline _catchCardOutline;  // faded alongside the text so the card fades cleanly
        private Image _catchCardIcon;       // the caught species' icon, resolved by id via IconRegistry
        private Image _moneyIcon;           // a coin glyph beside the money read (ui.coin)

        // VS-19 nav cluster (built in Awake): the heading compass + set-&-drift read, shown only at sea.
        private Text _compassLabel;        // "↗ 045°  NE" — arrow + degrees + cardinal (redundant coding)
        private Text _compassRibbonLabel;  // the scrolling rose tape — the SHAPE channel
        private Text _compassNeedleLabel;  // a fixed centre needle the tape scrolls under
        private Text _setDriftLabel;       // "COG 050°  → 8° stbd" — track vs heading (crabbing read)
        private Text _apparentWindLabel;   // "Apparent ↗ 45° stbd bow" — true wind relative to the bow

        // ---- cached displayed values (change-detection → no per-frame string building) ------
        private string _clockCache;
        private string _tideCache;
        private string _windCache;
        private string _seaCache;
        private string _moneyCache;
        private string _compassCache;
        private string _ribbonCache;
        private string _setDriftCache;
        private string _apparentWindCache;

        // Whether the nav cluster is currently shown (at sea). Toggled, so labels flip enabled only on change.
        private bool _navShown;

        // Clock change-detection (avoid building the clock string when the displayed minute is unchanged).
        private int _lastMinuteOfDay = -1;
        private int _lastDay = -1;
        private Season _lastSeason = (Season)(-1);

        // Last displayed balance. _moneyPainted forces the first paint; int.MinValue means "no wallet".
        private int _lastMoney;
        private bool _moneyPainted;

        private float _envSampleTimer;
        private float _payoutTimer;
        private float _catchCardTimer;
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
            EventBus.Subscribe<FishCaught>(OnFishCaught);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            EventBus.Unsubscribe<MoneyChanged>(OnMoneyChanged);
            EventBus.Unsubscribe<CatchSold>(OnCatchSold);
            EventBus.Unsubscribe<FishCaught>(OnFishCaught);
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
            TickCatchCard();
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
            UpdateNavReads(sample.WindVector);   // VS-19: compass + set-&-drift + apparent wind (only at sea)
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

            // Redundant coding (accessibility §8): direction reads as an arrow SHAPE + a cardinal
            // WORD; strength reads as barb LENGTH + a knots NUMBER + a Beaufort LABEL — never colour
            // alone. e.g. "↗ NE  ▮▪ 17 kt  F5".
            string text = WindReadout.ArrowGlyph(windVector) + " " + cardinal
                        + "  " + HudFormat.WindBarbs(knots)
                        + " " + knots.ToString(System.Globalization.CultureInfo.InvariantCulture) + " kt"
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

        // ---- VS-19 nav reads (heading compass + set-&-drift), built on the Core heading seam ---------
        // Read-only through Core (GameServices.ActiveBoat / BoatKinematics) — the UI never references the
        // Boats module (ADR 0007). Shown only while aboard; hidden ashore. Strings are change-detected
        // against a cache (same discipline as UpdateWind/UpdateTide) so an unchanged read repaints nothing.

        private void UpdateNavReads(Vector2 windVector)
        {
            // ActiveBoat is OPTIONAL (null on foot / before a boat is aboard, like Wallet) — null-check it.
            var boat = GameServices.ActiveBoat;
            if (boat == null || !boat.HasActiveBoat) { SetNavShown(false); return; }

            BoatKinematics k = boat.Sample();
            if (!k.HasBoat) { SetNavShown(false); return; }
            SetNavShown(true);

            // Compass: arrow SHAPE + degrees NUMBER + cardinal WORD (redundant coding, §8). Cross-checked
            // against WindReadout's bearing so the compass and the wind arrow agree on North (ADR 0007).
            string compass = CompassReadout.HeadingArrow(k.HeadingDegrees) + " "
                           + CompassReadout.Degrees(k.HeadingDegrees) + "  "
                           + CompassReadout.Cardinal(k.HeadingDegrees);
            if (compass != _compassCache) { _compassCache = compass; _compassLabel.text = compass; }

            // Ribbon: the rose tape that scrolls under the fixed needle (the SHAPE channel).
            string ribbon = CompassReadout.Ribbon(k.HeadingDegrees);
            if (ribbon != _ribbonCache) { _ribbonCache = ribbon; _compassRibbonLabel.text = ribbon; }

            // Set-&-drift: the boat's true course-over-ground vs its heading — so the player sees it crab.
            string set = CompassReadout.SetAndDrift(k.HeadingDegrees, k.CourseOverGroundDegrees, k.SpeedOverGround);
            if (set != _setDriftCache) { _setDriftCache = set; _setDriftLabel.text = set; }

            // Apparent wind: the true wind RELATIVE to the bow (off which bow/beam/quarter it's hitting),
            // composed from the same heading seam + the environment wind via BoatKinematics — so the
            // player reads the wind on the boat, not just its absolute compass direction (VS-19).
            string apparent = ApparentWindReadout.Format(k.HeadingDegrees, windVector);
            if (apparent != _apparentWindCache) { _apparentWindCache = apparent; _apparentWindLabel.text = apparent; }
        }

        // Show the nav cluster at sea, hide it ashore. Flips the labels' enabled state only on a change.
        private void SetNavShown(bool shown)
        {
            if (_navShown == shown) return;
            _navShown = shown;
            if (_compassLabel != null)       _compassLabel.enabled = shown;
            if (_compassRibbonLabel != null) _compassRibbonLabel.enabled = shown;
            if (_compassNeedleLabel != null) _compassNeedleLabel.enabled = shown;
            if (_setDriftLabel != null)      _setDriftLabel.enabled = shown;
            if (_apparentWindLabel != null)  _apparentWindLabel.enabled = shown;
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

        // ---- catch card (VS-14: a brief celebration on landing a fish) ----------------------
        // ADDITIVE: this is a separate label and timer; it never touches the money/payout path.

        private void OnFishCaught(FishCaught e) => ShowCatchCard(e.Item);

        private void ShowCatchCard(in CatchItem item)
        {
            if (_catchCardLabel == null) return;
            _catchCardLabel.text = HudFormat.CatchCard(item.DisplayName, item.WeightKg, item.BaseValue);

            // Show the caught species' icon beside the card text, resolved by id through the Core
            // IconRegistry (so the UI never references the Fishing/FishSpeciesDef def). Null icon
            // (none registered / EditMode) → hide the image and let the text carry it alone (§8).
            if (_catchCardIcon != null)
            {
                Sprite icon = IconRegistry.Get(item.SpeciesId);
                _catchCardIcon.sprite = icon;
                _catchCardIcon.enabled = icon != null;
            }

            SetCatchCardAlpha(1f);
            _catchCardLabel.enabled = true;
            _catchCardTimer = _catchCardSeconds;
        }

        private void TickCatchCard()
        {
            if (_catchCardTimer <= 0f) return;
            _catchCardTimer -= Time.unscaledDeltaTime;

            // Hold full, then fade the alpha over the back half of the lifetime (no per-frame alloc).
            float fadeOver = _catchCardSeconds > 0f ? _catchCardSeconds * 0.5f : 0f;
            if (fadeOver > 0f && _catchCardTimer < fadeOver)
                SetCatchCardAlpha(Mathf.Clamp01(_catchCardTimer / fadeOver));

            if (_catchCardTimer <= 0f)
            {
                if (_catchCardLabel != null) _catchCardLabel.enabled = false;
                if (_catchCardIcon != null)  _catchCardIcon.enabled = false;
            }
        }

        // Fade the text, its outline, and the icon together so the card dissolves cleanly (no lingering edge).
        private void SetCatchCardAlpha(float a)
        {
            if (_catchCardLabel == null) return;
            var c = _catchCardLabel.color; c.a = a; _catchCardLabel.color = c;
            if (_catchCardOutline != null)
            {
                var oc = _catchCardOutline.effectColor; oc.a = 0.85f * a; _catchCardOutline.effectColor = oc;
            }
            if (_catchCardIcon != null)
            {
                var ic = _catchCardIcon.color; ic.a = a; _catchCardIcon.color = ic;
            }
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
            SetNavShown(false); // no boat at boot → keep the nav cluster hidden
        }

        private static void SetIfChanged(ref string cache, string value, Text label)
        {
            if (value == cache) return;
            cache = value;
            if (label != null) label.text = value;
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
            // PC-first legibility bump (gameplay-systems, flagged for ui-ux): a smaller LANDSCAPE
            // reference makes the whole code-drawn HUD scale up uniformly (~1.5× at 1920×1080) so
            // clock/tide/money/hold read at a glance on a desktop window. This is a minimal scale
            // tweak only — the real HUD pass (sizing, density, layout) is ui-ux's VS-19. Was the
            // portrait 1080×1920 mobile reference (pre-ADR-0005).
            scaler.referenceResolution = new Vector2(1280f, 720f);
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
            // A coin glyph just left of the money read (ui.coin), resolved by id via IconRegistry.
            // The money TEXT still carries the value (icon is reinforcement, never the only channel, §8);
            // hidden when no icon is registered (EditMode / stripped build).
            _moneyIcon = MakeIcon(bandRt, "MoneyIcon", "ui.coin", TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), -150f, -6f, 36f);
            _payoutLabel = MakeLabel(bandRt, "Payout", TextAnchor.UpperRight,
                new Vector2(0.6f, 1f), new Vector2(1f, 1f), 0f, -52f, 38);
            _payoutLabel.color = new Color(0.55f, 0.95f, 0.55f); // green flash — but text+sign carry it too
            _payoutLabel.enabled = false;

            _windLabel = MakeLabel(bandRt, "Wind", TextAnchor.UpperRight,
                new Vector2(0.55f, 1f), new Vector2(1f, 1f), 0f, -100f, 34);
            _seaLabel  = MakeLabel(bandRt, "Sea", TextAnchor.UpperRight,
                new Vector2(0.55f, 1f), new Vector2(1f, 1f), 0f, -140f, 30);

            // Catch card: a brief, centred celebration on a landed fish (VS-14). Parented to the
            // canvas root (not the top band) so it reads as a centre-screen flourish, above the
            // gameplay HUD. Styled like the payout flash — outlined text, a warm celebratory tint —
            // and faded out by TickCatchCard. Text+content carry it (never colour alone, §8).
            var canvasRt = (RectTransform)canvasGo.transform;
            _catchCardLabel = MakeLabel(canvasRt, "CatchCard", TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.5f), 0f, 120f, 56);
            _catchCardLabel.color = new Color(1f, 0.92f, 0.55f); // warm gold "nice catch!" flash
            _catchCardOutline = _catchCardLabel.GetComponent<Outline>();
            _catchCardLabel.enabled = false;

            // The caught species' icon, centred just above the card text (set per-catch in ShowCatchCard,
            // resolved by id via IconRegistry). Built hidden; shown only when an icon resolves for the catch.
            _catchCardIcon = MakeIcon(canvasRt, "CatchCardIcon", null, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0f, 184f, 64f);
            _catchCardIcon.enabled = false;

            // VS-19 nav cluster (heading compass + set-&-drift). A sailing read, so it sits BOTTOM-CENTRE
            // (a natural compass spot, clear of the top conditions band) and is shown only while aboard
            // (UpdateNavReads toggles it; hidden ashore). Parented to the canvas root, stacked upward:
            // set-&-drift, the rose ribbon, the fixed needle, then the heading line. Redundant-coded — a
            // degrees number + a cardinal word + the ribbon/arrow SHAPE — never colour alone (§8).
            _apparentWindLabel = MakeLabel(canvasRt, "ApparentWind", TextAnchor.LowerCenter,
                new Vector2(0.2f, 0f), new Vector2(0.8f, 0f), 0f, 40f, 28);
            _setDriftLabel = MakeLabel(canvasRt, "SetDrift", TextAnchor.LowerCenter,
                new Vector2(0.2f, 0f), new Vector2(0.8f, 0f), 0f, 70f, 28);
            _compassRibbonLabel = MakeLabel(canvasRt, "CompassRibbon", TextAnchor.LowerCenter,
                new Vector2(0.2f, 0f), new Vector2(0.8f, 0f), 0f, 118f, 30);
            _compassNeedleLabel = MakeLabel(canvasRt, "CompassNeedle", TextAnchor.LowerCenter,
                new Vector2(0.2f, 0f), new Vector2(0.8f, 0f), 0f, 146f, 26);
            _compassNeedleLabel.text = "▾"; // fixed needle — the ribbon's centre column (the heading) sits under it
            _compassLabel = MakeLabel(canvasRt, "Compass", TextAnchor.LowerCenter,
                new Vector2(0.2f, 0f), new Vector2(0.8f, 0f), 0f, 188f, 34);

            // Built hidden; UpdateNavReads shows them once aboard (HasActiveBoat).
            _apparentWindLabel.enabled = false;
            _setDriftLabel.enabled = false;
            _compassRibbonLabel.enabled = false;
            _compassNeedleLabel.enabled = false;
            _compassLabel.enabled = false;

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

        // A square HUD icon Image. If <paramref name="iconId"/> is non-null it resolves the sprite from
        // the Core IconRegistry now (built once at Awake — no per-frame lookup); a null id means the
        // caller sets the sprite later (e.g. the per-catch card icon). The icon is reinforcement only —
        // every read it sits beside also has its text/number channel (accessibility §8). Read-only
        // (never eats touches), pivoted top-left to match MakeLabel's anchoring math.
        private static Image MakeIcon(RectTransform parent, string name, string iconId, TextAnchor align,
                                      Vector2 anchorMin, Vector2 anchorMax, float x, float y, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = align == TextAnchor.MiddleCenter ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;          // HUD is read-only
            img.preserveAspect = true;          // icons aren't square (fish are 48×32) — don't stretch
            if (iconId != null)
            {
                Sprite sprite = IconRegistry.Get(iconId);
                img.sprite = sprite;
                img.enabled = sprite != null;   // hide cleanly when none is registered
            }
            return img;
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
