using System;
using UnityEngine;
using UnityEngine.InputSystem;
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
    ///
    /// <para><b>The clock is a WATCH, not a line of text</b> (the owner's 2026-08-06 rig drop). The
    /// upper-left readout is the art director's digital watch face, drawn live in C# by
    /// <see cref="WatchRigRender"/> (ADR 0025 Option A — nothing is baked, because the face's state
    /// space is the whole calendar). It carries everything the old label did, and more besides: hours
    /// and minutes, the weekday, the day of season, the season tag, the year, a market-day flag, and a
    /// green backlight after dark. It obeys the same law the label did — it repaints ONLY when the
    /// displayed minute, day or season changes, never per frame. The text path is kept behind
    /// <c>_useTextClock</c> for debugging.</para>
    ///
    /// <para>It also carries the tide table's opener (<see cref="TidePanelInput"/>, VS-06) — the deeper
    /// read the glanceable tide line here can't give you. The band stays a band; the table is paper you
    /// take out.</para>
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

        [Header("Watch face (the upper-left clock)")]
        [Tooltip("Height of the watch in HUD REFERENCE units (HudBandLayout.RefH = 720). Width follows " +
                 "the rig's own 340×356 aspect, so the face is never distorted. 190 fits inside the " +
                 "220-unit band with air above and below.")]
        [SerializeField] private float _watchHeightRef = 190f;
        [Tooltip("Gap in reference units between the watch's right edge and the tide line beside it.")]
        [SerializeField] private float _watchGapRef = 16f;
        [Tooltip("24-hour face. Off shows a 12-hour face with the rig's AM/PM tag and a blank leading " +
                 "cell for hours 1-9. Display-only: it is not clock-derived.")]
        [SerializeField] private bool _watchUse24 = true;
        [Tooltip("Light the LCD's seconds cells. OFF by default and deliberately so: one in-game second " +
                 "is ~21 real ms at the shipped SecondsPerDay, so a live seconds field would repaint the " +
                 "face ~48×/second and blow rule 7. Off, the cells sit dark (an unlit LCD field) rather " +
                 "than frozen on a stale number. Turn on only with a slower clock.")]
        [SerializeField] private bool _watchShowSeconds;
        [Tooltip("DEBUG: go back to the plain-text clock label the watch replaced. The formatting path " +
                 "(HudFormat.ClockHHMM + HudStrings.Season) is kept intact behind this toggle so the " +
                 "old readout is one checkbox away, not a revert.")]
        [SerializeField] private bool _useTextClock;

        // ---- runtime labels (built in Awake) ------------------------------------------------
        private GameObject _canvasGo;       // the whole band, hidden while the shell's title page is up
        private Text _clockLabel;           // the pre-watch text readout — built, but off unless _useTextClock

        // The watch face that replaced the text clock (ADR 0025 Option A: a live C# rig renderer, no
        // bake). One reused surface + texture, repainted only when UpdateClock's change-detection fires.
        private RawImage _watchImage;
        private DrawSurface _watchSurface;
        private Texture2D _watchTexture;

        // The watch as a hide-only WINDOW (2026-08-07 windowing ruling): a hover × on the face hides
        // it; the boat-UI hide-all toggle hides and restores it with everything else, and its restore
        // half is the recovery path back from an individual hide.
        private RectTransform _watchRect;          // the face (hover test)
        private Text _watchHideBtn;                // the hover ×
        private bool _watchWindowShown = true;     // change detection for the hidden gate
        private readonly Vector3[] _watchBtnCorners = new Vector3[4];   // reused; no per-frame alloc
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

        // Where the nav cluster currently is. Applied only on a CHANGE, so the labels' enabled flags
        // and anchors are touched on a transition (boarding, taking a helm) and never per frame.
        private NavClusterPlacement _navPlacement = NavClusterPlacement.Hidden;

        // The nav cluster's two homes, captured at build time so a move can go back exactly.
        private RectTransform[] _navRects;
        private Vector2[] _navHomeAnchorMin, _navHomeAnchorMax, _navHomePos;
        private TextAnchor[] _navHomeAlign;

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
            EnsureTidePanelInput();
            if (_persistAcrossScenes)
                DontDestroyOnLoad(gameObject);

            // A HUD band across the top of the title page would read as a broken screen, not a game that
            // hasn't started. The shell's phase decides (M1 §7.8); on a rig with no shell running the
            // phase is Playing, so the band is up exactly as it always was.
            ApplyShellPhase(ShellFlow.Phase);
        }

        // The tide table's opener rides along with the always-on HUD (VS-06). Installing it here rather
        // than in the scene builders means it exists wherever the HUD does — the persistent core, the
        // dev cores, and any scene a builder assembles later — without a cross-lane edit to App/Editor.
        // Idempotent: a builder or prefab that adds its own (to rebind the key) is left alone.
        private void EnsureTidePanelInput()
        {
            if (GetComponent<TidePanelInput>() == null)
                gameObject.AddComponent<TidePanelInput>();
        }

        private void OnEnable()  => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            // The watch's texture is created by us (DrawSurface.ToTexture), so it is ours to release —
            // the HUD is DontDestroyOnLoad, but a torn-down rig/test still has to not leak it.
            if (_watchTexture != null) Destroy(_watchTexture);
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            EventBus.Subscribe<MoneyChanged>(OnMoneyChanged);
            EventBus.Subscribe<CatchSold>(OnCatchSold);
            EventBus.Subscribe<FishCaught>(OnFishCaught);
            EventBus.Subscribe<ShellPhaseChanged>(OnShellPhaseChanged);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            EventBus.Unsubscribe<MoneyChanged>(OnMoneyChanged);
            EventBus.Unsubscribe<CatchSold>(OnCatchSold);
            EventBus.Unsubscribe<FishCaught>(OnFishCaught);
            EventBus.Unsubscribe<ShellPhaseChanged>(OnShellPhaseChanged);
            _subscribed = false;
        }

        private void OnShellPhaseChanged(ShellPhaseChanged e) => ApplyShellPhase(e.Phase);

        /// <summary>Show the band in the world, hide it at the title. Toggling the CANVAS (not this
        /// component) keeps every label, cache and subscription intact, so coming back out of the shell
        /// costs nothing and rebuilds nothing.</summary>
        private void ApplyShellPhase(ShellPhase phase)
        {
            if (_canvasGo == null) return;
            bool show = phase != ShellPhase.Title;
            if (_canvasGo.activeSelf != show) _canvasGo.SetActive(show);
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
            UpdateWatchWindow();      // the hidden gate + hover × (2026-08-07 windowing ruling)
            UpdateEnvironmentThrottled();
            UpdateMoney();            // event-driven, but reconcile once services exist (boot balance)
            TickPayoutFlash();
            TickCatchCard();
        }

        /// <summary>
        /// The watch face's WINDOW behaviour (2026-08-07 ruling): hide-only. Gates the face on
        /// <see cref="BoatUiWindows.IsShown"/> (its own × and the hide-all toggle both land there),
        /// shows the hover × while the pointer is over the face, and publishes the ×'s rect so the
        /// helm cards never read a press on it as their own click-away. Everything here is
        /// change-detected rect/enabled writes — no per-frame allocation (rule 7).
        /// </summary>
        private void UpdateWatchWindow()
        {
            if (_watchImage == null) return;

            bool shown = BoatUiWindows.IsShown(BoatUiWindowId.Watch);
            if (shown != _watchWindowShown)
            {
                _watchWindowShown = shown;
                if (!shown && _watchImage.enabled) _watchImage.enabled = false;
                // Coming back after time passed hidden: the face is stale — force a repaint.
                if (shown) _lastMinuteOfDay = int.MinValue;
            }
            if (!shown || _watchHideBtn == null)
            {
                if (_watchHideBtn != null && _watchHideBtn.enabled) _watchHideBtn.enabled = false;
                BoatUiWindows.ClearChrome(BoatUiWindowId.Watch);
                return;
            }

            var mouse = Mouse.current;
            bool hover = false;
            Vector2 pos = default;
            if (mouse != null && _watchImage.enabled)
            {
                pos = mouse.position.ReadValue();
                // ScreenSpaceOverlay: no camera — screen point tests directly against the rect.
                hover = RectTransformUtility.RectangleContainsScreenPoint(_watchRect, pos);
            }
            if (_watchHideBtn.enabled != hover) _watchHideBtn.enabled = hover;
            if (!hover)
            {
                BoatUiWindows.ClearChrome(BoatUiWindowId.Watch);
                return;
            }

            var btnRt = (RectTransform)_watchHideBtn.transform;
            btnRt.GetWorldCorners(_watchBtnCorners);   // overlay canvas: world == screen px
            var btnRect = new Rect(_watchBtnCorners[0].x, _watchBtnCorners[0].y,
                                   _watchBtnCorners[2].x - _watchBtnCorners[0].x,
                                   _watchBtnCorners[2].y - _watchBtnCorners[0].y);
            BoatUiWindows.PublishChrome(BoatUiWindowId.Watch, btnRect);

            if (mouse.leftButton.wasPressedThisFrame && btnRect.Contains(pos))
                BoatUiWindows.SetHidden(BoatUiWindowId.Watch, true);
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

            // The watch is the clock now. It repaints HERE — behind the same minute/day/season
            // change-detection the text label used — so the face costs one struct compare per frame in
            // the steady state and a repaint only when a shown quantum actually moved (rule 7). At the
            // shipped SecondsPerDay that is ~0.8 Hz.
            if (!_useTextClock)
            {
                PaintWatch(clock);
                return;
            }

            string text = HudFormat.ClockHHMM(clock.HourOfDay)
                        + "  " + HudStrings.Season(season)
                        + " d" + day;
            _clockCache = text;
            _clockLabel.text = text;
        }

        /// <summary>
        /// Repaint the watch face from the live clock. The clock is read through
        /// <see cref="WatchFaceState.FromClock"/> — Core's one mapper, which already carries the calendar
        /// canon and the 06:00/19:00 night rule — so nothing here re-derives a date or a second time.
        /// </summary>
        private void PaintWatch(IGameClock clock)
        {
            if (_watchImage == null) return;
            // Hidden (its × or hide-all): no raster and no re-enable. The unhide transition forces
            // the next repaint by resetting the minute cache (UpdateWatchWindow).
            if (!BoatUiWindows.IsShown(BoatUiWindowId.Watch)) return;

            var state = new WatchRigState(WatchFaceState.FromClock(clock),
                                          _watchUse24, light: false, showSeconds: _watchShowSeconds);

            if (_watchSurface == null)
                _watchSurface = new DrawSurface(WatchRigRender.W, WatchRigRender.H);

            WatchRigRender.Render(_watchSurface, in state);
            _watchSurface.ToTexture(ref _watchTexture);
            _watchImage.texture = _watchTexture;
            if (!_watchImage.enabled) _watchImage.enabled = true;   // hidden until it has a time to show
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
            if (boat == null || !boat.HasActiveBoat) { SetNavPlacement(NavClusterPlacement.Hidden); return; }

            BoatKinematics k = boat.Sample();
            if (!k.HasBoat) { SetNavPlacement(NavClusterPlacement.Hidden); return; }

            // S4.5: the cluster yields to a helm dash — hidden where the dash's own compass already
            // says it, moved clear where it does not. Keyed on the live FIT through Core, so a bought
            // or dev-cycled compass moves the HUD with no list of hulls to keep in step. HelmControl
            // is optional in the same way ActiveBoat is (ashore, EditMode) — a null one is simply "no
            // dash", which leaves the cluster exactly where VS-19 put it.
            IHelmControl helm = GameServices.HelmControl;
            bool atHelm = helm != null && helm.HasHelm;
            NavClusterPlacement placement = HelmHudSuppression.NavCluster(
                aboard: true,
                atHelm ? helm.Style : HelmControlStyle.None,
                atHelm ? helm.Fit : HelmFit.None);
            SetNavPlacement(placement);
            if (placement == NavClusterPlacement.Hidden) return;   // nothing to format into hidden labels

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

        /// <summary>
        /// Put the nav cluster where <see cref="HelmHudSuppression"/> says it belongs. Only a CHANGE
        /// does any work, so this costs one enum compare per sample in the steady state (rule 7).
        ///
        /// <para>Moved, the five labels swap to a LEFT-anchored, left-aligned column at the screen
        /// edge — every read intact, out from under the bottom-centre dash card. Their vertical
        /// stacking (and so the whole cluster's reading order) is untouched: only the horizontal
        /// anchoring moves, and it moves back to the captured home exactly.</para>
        /// </summary>
        private void SetNavPlacement(NavClusterPlacement placement)
        {
            if (_navPlacement == placement) return;
            _navPlacement = placement;

            bool shown = placement != NavClusterPlacement.Hidden;
            if (_compassLabel != null)       _compassLabel.enabled = shown;
            if (_compassRibbonLabel != null) _compassRibbonLabel.enabled = shown;
            if (_compassNeedleLabel != null) _compassNeedleLabel.enabled = shown;
            if (_setDriftLabel != null)      _setDriftLabel.enabled = shown;
            if (_apparentWindLabel != null)  _apparentWindLabel.enabled = shown;
            if (!shown || _navRects == null) return;

            bool clear = placement == NavClusterPlacement.ClearOfTheDash;
            for (int i = 0; i < _navRects.Length; i++)
            {
                RectTransform rt = _navRects[i];
                if (rt == null) continue;
                if (clear)
                {
                    rt.anchorMin = new Vector2(0f, _navHomeAnchorMin[i].y);
                    rt.anchorMax = new Vector2(NavClearWidth01, _navHomeAnchorMax[i].y);
                    rt.anchoredPosition = new Vector2(NavClearMarginX, _navHomePos[i].y);
                }
                else
                {
                    rt.anchorMin = _navHomeAnchorMin[i];
                    rt.anchorMax = _navHomeAnchorMax[i];
                    rt.anchoredPosition = _navHomePos[i];
                }
                var text = rt.GetComponent<Text>();
                if (text != null)
                    text.alignment = clear ? TextAnchor.LowerLeft : _navHomeAlign[i];
            }
        }

        // The moved cluster's box, in HUD reference units / fractions of the canvas width. 0.42 is
        // where the SMALL dash card's left edge lands at the shipped scales (a 600-wide rig at
        // DashSmallScale 0.5, centred), so the cluster's column stops short of it with room to spare;
        // left-aligned text then grows rightward from the margin only as far as its own length.
        private const float NavClearWidth01 = 0.34f;
        private const float NavClearMarginX = 16f;

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
            // The watch says this by staying DARK — it is built disabled and only PaintWatch switches it
            // on — which is the same "no wrong numbers" answer as the label's "--", in the watch's own
            // language. A blank-faced watch is honest; a watch reading 00:00 would not be.
            SetIfChanged(ref _clockCache, HudStrings.Unknown, _clockLabel);
            SetIfChanged(ref _tideCache,  HudStrings.Unknown, _tideLabel);
            SetIfChanged(ref _windCache,  HudStrings.Unknown, _windLabel);
            SetIfChanged(ref _seaCache,   HudStrings.Unknown, _seaLabel);
            SetIfChanged(ref _moneyCache, HudStrings.MoneyPrefix + HudStrings.Unknown, _moneyLabel);
            SetNavPlacement(NavClusterPlacement.Hidden); // no boat at boot → keep the cluster hidden
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
            _canvasGo = canvasGo;   // the handle the shell hides the band by

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
            //
            // These four numbers live in HudBandLayout because the helm overlays have to keep clear
            // of the band they describe (S4.5) — one copy, or the dash's reserve drifts from the
            // band's actual size and the band goes back to drawing over the wheelhouse.
            scaler.referenceResolution = new Vector2(HudBandLayout.RefW, HudBandLayout.RefH);
            scaler.matchWidthOrHeight = HudBandLayout.MatchWidthOrHeight;

            // A top band anchored across the top, inset for the safe area at runtime.
            var band = new GameObject("TopBand", typeof(RectTransform));
            band.transform.SetParent(canvasGo.transform, false);
            var bandRt = band.GetComponent<RectTransform>();
            bandRt.anchorMin = new Vector2(0f, 1f);
            bandRt.anchorMax = new Vector2(1f, 1f);
            bandRt.pivot = new Vector2(0.5f, 1f);
            bandRt.anchoredPosition = new Vector2(0f, -SafeAreaTopInset());
            bandRt.sizeDelta = new Vector2(-HudBandLayout.SidePaddingRef, HudBandLayout.BandHeightRef);

            // Left column: the watch (top-left) + tide (highest-stakes — kept visually distinct, larger).
            //
            // The watch REPLACED the text clock here. The old label is still built — same anchor, same
            // size — but stays disabled unless _useTextClock is set, so the formatting path is a
            // checkbox away for debugging rather than a revert (and there are never two clocks).
            _clockLabel = MakeLabel(bandRt, "Clock", TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0.6f, 1f), 0f, -4f, 40);
            _clockLabel.enabled = _useTextClock;

            float watchW = 0f;
            if (!_useTextClock)
            {
                // Sized off the rig's own 340×356 so the face is never distorted, and pivoted top-left
                // like every other band element (MakeLabel's convention).
                watchW = _watchHeightRef * WatchRigRender.W / WatchRigRender.H;
                _watchImage = MakeWatch(bandRt, 0f, -4f, watchW, _watchHeightRef);
                _watchRect = (RectTransform)_watchImage.transform;

                // The hover × that hides the watch (2026-08-07 windowing ruling) — the face's one
                // piece of chrome, shown only while the pointer is over it. A glyph, not a string
                // (nothing to localize). Restored by the hide-all toggle's restore half.
                _watchHideBtn = MakeLabel(bandRt, "WatchHide", TextAnchor.MiddleCenter,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), watchW - 26f, -6f, 22);
                var hideRt = (RectTransform)_watchHideBtn.transform;
                hideRt.sizeDelta = new Vector2(24f, 24f);
                _watchHideBtn.text = "×";
                _watchHideBtn.enabled = false;
            }

            // The tide line clears the watch horizontally: the face is a block where a single text line
            // used to be, so the read beside it is indented by the face's width rather than moved down
            // the band (its vertical order — and so the band's reading order — is unchanged). With the
            // text clock on, the indent is zero and the band is exactly as it shipped.
            float tideIndent = _useTextClock ? 0f : watchW + _watchGapRef;
            _tideLabel  = MakeLabel(bandRt, "Tide", TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0.7f, 1f), tideIndent, -56f, 52); // bigger: most important read

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
            // (UpdateNavReads toggles it; hidden ashore). S4.5: at a helm dash it yields — hidden where
            // the dash's own compass says the same thing, moved bottom-LEFT where it does not, because
            // the dash card is anchored bottom-centre too (SetNavPlacement / HelmHudSuppression).
            // Parented to the canvas root, stacked upward:
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

            // Capture the cluster's authored home so S4.5's move to bottom-left is exactly reversible
            // (the numbers above stay the single source of where it lives; nothing is duplicated).
            CaptureNavHome(_apparentWindLabel, _setDriftLabel, _compassRibbonLabel,
                           _compassNeedleLabel, _compassLabel);

            // Start quiet until services are ready.
            ShowPlaceholder();
        }

        // Remember each nav label's built anchors, position and alignment, so SetNavPlacement can
        // move the cluster clear of the dash and put it back without a second copy of the numbers.
        private void CaptureNavHome(params Text[] labels)
        {
            _navRects = new RectTransform[labels.Length];
            _navHomeAnchorMin = new Vector2[labels.Length];
            _navHomeAnchorMax = new Vector2[labels.Length];
            _navHomePos = new Vector2[labels.Length];
            _navHomeAlign = new TextAnchor[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                var rt = (RectTransform)labels[i].transform;
                _navRects[i] = rt;
                _navHomeAnchorMin[i] = rt.anchorMin;
                _navHomeAnchorMax[i] = rt.anchorMax;
                _navHomePos[i] = rt.anchoredPosition;
                _navHomeAlign[i] = labels[i].alignment;
            }
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

        /// <summary>
        /// The watch face's <see cref="RawImage"/>, anchored top-left of the band exactly where the
        /// clock label was. Built DISABLED — <see cref="PaintWatch"/> switches it on once it has a real
        /// time to show, so the band never flashes an empty frame at boot.
        ///
        /// <para>Pixel discipline: the texture is point-filtered (<see cref="DrawSurface.ToTexture"/>
        /// sets that) and the rect is a whole multiple of nothing in particular — the HUD canvas is
        /// ScaleWithScreenSize, so the band's own scaler decides the final size, and this element scales
        /// with it like every other one. That is the same treatment the helm rigs get; no second
        /// pixel-snapping scheme is introduced here.</para>
        /// </summary>
        private static RawImage MakeWatch(RectTransform parent, float x, float y, float w, float h)
        {
            var go = new GameObject("Watch", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);

            var img = go.GetComponent<RawImage>();
            img.raycastTarget = false;   // the HUD is read-only; never eat clicks meant for gameplay
            img.enabled = false;         // shown on the first paint
            return img;
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
