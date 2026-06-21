#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.UI; // TideReadout/TideState — the SAME derivation the HUD uses (no divergent copy).

namespace HiddenHarbours.Tools.Editor
{
    /// <summary>
    /// VS-29 — an editor-only Time/Tide scrubber so designers can scrub <c>gameTime</c> and *see*
    /// the deterministic sea respond without entering play mode (Pillar 1, "The Sea Has Moods").
    ///
    /// EDITOR-ONLY and read-only: it computes everything by calling the existing pure functions
    /// directly — <see cref="TideModel"/> for height/rate, <see cref="TideReadout"/> (the HUD's own
    /// derivation) for rising/falling + time-to-next-turn — and mirrors <c>GameClock</c>'s tiny
    /// calendar formulae for the day/hour breakdown (there is no pure calendar helper to call).
    /// Nothing here is referenced by runtime code and it never ships in a build.
    ///
    /// Menu: <b>Hidden Harbours ▸ Tools ▸ Tide Scrubber</b>.
    /// </summary>
    public class TideScrubberWindow : EditorWindow
    {
        // --- Persisted scrub state (survives domain reload via the window's serialization) ---
        [SerializeField] private GameConfig _config;
        [SerializeField] private double _totalSeconds;          // the master clock value being scrubbed
        [SerializeField] private float _curveHours = 48f;       // tide-curve horizon (24..48 h)

        // Editable tide profile (defaults to the greybox Coddle Cove shape).
        [SerializeField] private float _meanLevel = 0f;
        [SerializeField] private float _amplitude = 1.6f;
        [SerializeField] private float _phaseHours = 0f;

        [SerializeField] private Vector2 _scroll;

        // Colours kept local so the tool reads the same in light/dark editor skins.
        private static readonly Color CurveColor  = new Color(0.30f, 0.70f, 1.00f);
        private static readonly Color DatumColor  = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color GridColor   = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color NowColor    = new Color(1f, 0.85f, 0.25f);
        private static readonly Color HighColor   = new Color(0.45f, 1f, 0.55f);
        private static readonly Color LowColor    = new Color(1f, 0.55f, 0.45f);
        private static readonly Color PanelColor  = new Color(0.16f, 0.18f, 0.20f);

        [MenuItem("Hidden Harbours/Tools/Tide Scrubber")]
        public static void Open()
        {
            var win = GetWindow<TideScrubberWindow>("Tide Scrubber");
            win.minSize = new Vector2(420f, 520f);
            win.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Time / Tide Scrubber", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Scrub the master clock and watch the deterministic tide respond. Editor-only (VS-29).",
                EditorStyles.miniLabel);
            EditorGUILayout.Space();

            ResolveConfig();
            if (_config == null)
            {
                EditorGUILayout.HelpBox(
                    "No GameConfig found. Assign one (or create Assets > Hidden Harbours > Game Config) " +
                    "so tide and calendar math has its tunables.", MessageType.Warning);
                _config = (GameConfig)EditorGUILayout.ObjectField("Game Config", _config, typeof(GameConfig), false);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawConfigSection();
            EditorGUILayout.Space();
            DrawTimeControls();
            EditorGUILayout.Space();

            TideProfile profile = BuildProfile();
            DrawReadouts(profile);
            EditorGUILayout.Space();
            DrawTideCurve(profile);

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ config / profile

        private void ResolveConfig()
        {
            if (_config != null) return;
            string[] guids = AssetDatabase.FindAssets("t:GameConfig");
            if (guids.Length > 0)
                _config = AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private TideProfile BuildProfile() =>
            new TideProfile { MeanLevel = _meanLevel, Amplitude = _amplitude, PhaseHours = _phaseHours };

        private void DrawConfigSection()
        {
            EditorGUILayout.LabelField("Region tide profile", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                _config = (GameConfig)EditorGUILayout.ObjectField("Game Config", _config, typeof(GameConfig), false);
                _meanLevel = EditorGUILayout.FloatField(
                    new GUIContent("Mean Level (m)", "Mean water level relative to chart datum."), _meanLevel);
                _amplitude = EditorGUILayout.FloatField(
                    new GUIContent("Amplitude (m)", "Half the spring high-to-low range."), _amplitude);
                _phaseHours = EditorGUILayout.FloatField(
                    new GUIContent("Phase (h)", "High-water phase offset so regions don't all peak together."),
                    _phaseHours);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Presets");
                    if (GUILayout.Button("Coddle Cove")) LoadProfile(TideProfile.CoddleCove);
                    if (GUILayout.Button("Fundy Rips")) LoadProfile(TideProfile.FundyRips);
                }
            }
        }

        private void LoadProfile(TideProfile p)
        {
            _meanLevel = p.MeanLevel;
            _amplitude = p.Amplitude;
            _phaseHours = p.PhaseHours;
            GUI.FocusControl(null); // so the edited fields refresh to the preset values
        }

        // ------------------------------------------------------------------ time controls

        private void DrawTimeControls()
        {
            EditorGUILayout.LabelField("Scrub time", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                double secPerDay = _config.SecondsPerDay;
                int day = (int)Math.Floor(_totalSeconds / secPerDay);
                float hourOfDay = (float)((_totalSeconds / secPerDay - day) * 24.0);

                // Hour-of-day slider — the main "watch it respond" control.
                EditorGUI.BeginChangeCheck();
                float newHour = EditorGUILayout.Slider(
                    new GUIContent("Hour of day", "Scrub within the current in-game day."),
                    hourOfDay, 0f, 24f);
                if (EditorGUI.EndChangeCheck())
                    SetTime((day + newHour / 24.0) * secPerDay);

                // Day index — step across days to see the spring/neap envelope evolve.
                EditorGUI.BeginChangeCheck();
                int newDay = EditorGUILayout.IntField(
                    new GUIContent("Day index", "Absolute in-game day (0-based)."), day);
                if (EditorGUI.EndChangeCheck())
                    SetTime((newDay + hourOfDay / 24.0) * secPerDay);

                // Sweep across roughly two lunar months so spring↔neap is visible while dragging.
                float maxSweepDays = Mathf.Max(1f, _config.LunarMonthDays * 2f);
                EditorGUI.BeginChangeCheck();
                float dayFloat = EditorGUILayout.Slider(
                    new GUIContent("Sweep (days)", "Drag across the lunar cycle to see spring/neap swing."),
                    (float)(_totalSeconds / secPerDay), 0f, maxSweepDays);
                if (EditorGUI.EndChangeCheck())
                    SetTime(dayFloat * secPerDay);

                _totalSeconds = Math.Max(0.0, EditorGUILayout.DoubleField(
                    new GUIContent("gameTime (s)", "Raw master clock value (IGameClock.TotalSeconds)."),
                    _totalSeconds));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("-1 d")) SetTime(_totalSeconds - secPerDay);
                    if (GUILayout.Button("-1 h")) SetTime(_totalSeconds - _config.SecondsPerHour);
                    if (GUILayout.Button("+1 h")) SetTime(_totalSeconds + _config.SecondsPerHour);
                    if (GUILayout.Button("+1 d")) SetTime(_totalSeconds + secPerDay);
                    if (GUILayout.Button("Reset")) SetTime(0.0);
                }
            }
        }

        private void SetTime(double seconds)
        {
            _totalSeconds = Math.Max(0.0, seconds);
            Repaint();
        }

        // ------------------------------------------------------------------ readouts

        private void DrawReadouts(TideProfile profile)
        {
            double t = _totalSeconds;
            Cal cal = Breakdown(t);

            // Tide state via the HUD's own derivation, so scrubber and HUD never disagree.
            double risingDt = _config.SecondsPerHour * 0.05;
            double scanStep = _config.SecondsPerHour * 0.10;
            double horizon  = _config.SecondsPerHour * _config.TidalPeriodHours;
            TideState state = TideReadout.Derive(s => TideModel.Height(s, profile, _config), t,
                                                 risingDt, scanStep, horizon);

            float ratePerHour = TideModel.Rate(t, profile, _config) * _config.SecondsPerHour;
            bool slack = IsSlack(t, profile, ratePerHour);

            string stateText;
            if (slack)
                stateText = state.HeightMeters > _meanLevel ? "Slack — high water" : "Slack — low water";
            else
                stateText = state.Rising ? "Rising (flood)" : "Falling (ebb)";

            string turnText = state.HasTurn
                ? $"{(state.Rising ? "High" : "Low")} water in {FormatDuration(state.SecondsToTurn)} " +
                  $"(at {FormatClock((cal.HourOfDay + (float)(state.SecondsToTurn / _config.SecondsPerHour)) % 24f)})"
                : "no turn within a tidal period";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Calendar", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"Year {cal.Year} · {Pretty(cal.Season)} · Day {cal.DayOfSeason}/{_config.DaysPerSeason} · {cal.Weekday}"
                    + (cal.IsMarketDay ? "   [MARKET DAY]" : ""));
                EditorGUILayout.LabelField($"Clock {FormatClock(cal.HourOfDay)}   ·   Day index {cal.TotalDays}");

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Tide", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Height:  {state.HeightMeters:0.00} m  (rel. datum)");
                EditorGUILayout.LabelField($"State:   {stateText}");
                EditorGUILayout.LabelField($"Rate:    {ratePerHour:+0.00;-0.00;0.00} m/h");
                EditorGUILayout.LabelField($"Next:    {turnText}");

                EditorGUILayout.Space(2);
                DrawSpringNeap(t, profile);
            }
        }

        /// <summary>
        /// Slack water = the rate is near zero relative to this cycle's peak rate. Robust to the
        /// spring/neap envelope because the reference peak is *sampled* from the model (no copy of
        /// the envelope math). Mirrors the design: max |rate| at mid-tide, ~zero at the turns.
        /// </summary>
        private bool IsSlack(double t, TideProfile profile, float ratePerHour)
        {
            double horizon = _config.SecondsPerHour * _config.TidalPeriodHours;
            double step = _config.SecondsPerHour * 0.1;
            float peak = 0f;
            for (double s = t; s <= t + horizon; s += step)
                peak = Mathf.Max(peak, Mathf.Abs(TideModel.Rate(s, profile, _config) * _config.SecondsPerHour));
            return peak > 0f && Mathf.Abs(ratePerHour) < 0.06f * peak;
        }

        private void DrawSpringNeap(double t, TideProfile profile)
        {
            // Derive "how spring-like is today" from the actual tide range over the next 24 h,
            // interpolated between the model's neap and spring ranges (data only, no envelope copy).
            float todayRange = DayRange(t, profile);
            float springRange = 2f * Mathf.Abs(_amplitude);
            float neapRange = springRange * _config.NeapAmplitudeFraction;
            float u = springRange > neapRange ? Mathf.InverseLerp(neapRange, springRange, todayRange) : 0f;
            string label = u > 0.8f ? "Spring" : u < 0.2f ? "Neap" : "Mid";

            EditorGUILayout.LabelField("Lunar envelope", EditorStyles.boldLabel);
            Rect r = EditorGUILayout.GetControlRect(false, 14f);
            EditorGUI.DrawRect(r, PanelColor);
            var fill = new Rect(r.x, r.y, r.width * Mathf.Clamp01(u), r.height);
            EditorGUI.DrawRect(fill, CurveColor);
            EditorGUI.LabelField(r, $"  Neap ◄ {label} ► Spring   (range today ≈ {todayRange:0.0} m)",
                EditorStyles.miniBoldLabel);
        }

        // ------------------------------------------------------------------ tide curve graph

        private void DrawTideCurve(TideProfile profile)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Tide curve", EditorStyles.boldLabel, GUILayout.Width(80f));
                if (GUILayout.Button("24 h", _curveHours <= 24f ? EditorStyles.miniButtonMid : EditorStyles.miniButton, GUILayout.Width(50f)))
                    _curveHours = 24f;
                if (GUILayout.Button("48 h", _curveHours >= 48f ? EditorStyles.miniButtonMid : EditorStyles.miniButton, GUILayout.Width(50f)))
                    _curveHours = 48f;
                GUILayout.FlexibleSpace();
            }

            Rect area = GUILayoutUtility.GetRect(100f, 190f, GUILayout.ExpandWidth(true));
            area = new RectOffset(4, 4, 2, 18).Remove(area); // leave a strip at the bottom for time labels
            EditorGUI.DrawRect(area, PanelColor);

            if (Event.current.type != EventType.Repaint) return;

            double now = _totalSeconds;
            double windowSeconds = _curveHours * _config.SecondsPerHour;
            const int N = 256;

            // Sample heights and find the value range for vertical normalisation.
            var h = new float[N];
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < N; i++)
            {
                double s = now + windowSeconds * (i / (double)(N - 1));
                h[i] = TideModel.Height(s, profile, _config);
                min = Mathf.Min(min, h[i]);
                max = Mathf.Max(max, h[i]);
            }
            float pad = Mathf.Max(0.05f, (max - min) * 0.1f);
            min -= pad; max += pad;

            float X(int i) => area.x + area.width * (i / (float)(N - 1));
            float Y(float v) => area.yMax - (v - min) / (max - min) * area.height;

            // Vertical 6-hour gridlines + hour-of-day labels along the bottom strip.
            Cal cal = Breakdown(now);
            Handles.color = GridColor;
            for (float hourMark = 0f; hourMark <= _curveHours + 0.001f; hourMark += 6f)
            {
                float gx = area.x + area.width * (hourMark / _curveHours);
                Handles.DrawLine(new Vector3(gx, area.y), new Vector3(gx, area.yMax));
                float clock = (cal.HourOfDay + hourMark) % 24f;
                var lblRect = new Rect(gx - 16f, area.yMax + 2f, 40f, 14f);
                GUI.Label(lblRect, FormatClock(clock), EditorStyles.miniLabel);
            }

            // Datum (mean-level) line.
            if (_meanLevel >= min && _meanLevel <= max)
            {
                Handles.color = DatumColor;
                float dy = Y(_meanLevel);
                Handles.DrawLine(new Vector3(area.x, dy), new Vector3(area.xMax, dy));
            }

            // The curve.
            var pts = new Vector3[N];
            for (int i = 0; i < N; i++) pts[i] = new Vector3(X(i), Y(h[i]), 0f);
            Handles.color = CurveColor;
            Handles.DrawAAPolyLine(2.5f, pts);

            // High/low turn markers (local extrema of the sampled curve).
            for (int i = 1; i < N - 1; i++)
            {
                bool high = h[i] > h[i - 1] && h[i] >= h[i + 1];
                bool low  = h[i] < h[i - 1] && h[i] <= h[i + 1];
                if (!high && !low) continue;
                Handles.color = high ? HighColor : LowColor;
                var p = new Vector3(X(i), Y(h[i]), 0f);
                Handles.DrawSolidDisc(p, Vector3.forward, 3f);
                float clk = (cal.HourOfDay + _curveHours * (i / (float)(N - 1))) % 24f;
                GUI.Label(new Rect(p.x - 18f, p.y + (high ? -16f : 4f), 60f, 14f),
                    $"{(high ? "H" : "L")} {FormatClock(clk)}", EditorStyles.miniLabel);
            }

            // "Now" marker at the left edge + current-height dot.
            Handles.color = NowColor;
            Handles.DrawLine(new Vector3(area.x, area.y), new Vector3(area.x, area.yMax));
            Handles.DrawSolidDisc(new Vector3(X(0), Y(h[0]), 0f), Vector3.forward, 3.5f);

            // Y range labels.
            GUI.Label(new Rect(area.x + 2f, area.y, 80f, 14f), $"{max:0.0} m", EditorStyles.miniLabel);
            GUI.Label(new Rect(area.x + 2f, area.yMax - 14f, 80f, 14f), $"{min:0.0} m", EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------------ math helpers

        /// <summary>Max-minus-min tide height across the in-game day starting at <paramref name="t"/>.</summary>
        private float DayRange(double t, TideProfile profile)
        {
            float min = float.MaxValue, max = float.MinValue;
            const int n = 96;
            for (int i = 0; i <= n; i++)
            {
                float v = TideModel.Height(t + _config.SecondsPerDay * (i / (double)n), profile, _config);
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }
            return max - min;
        }

        /// <summary>
        /// Calendar breakdown for a master-clock value. Mirrors <c>GameClock</c>'s formulae exactly
        /// (the source of truth) — kept here because that breakdown lives on the runtime MonoBehaviour
        /// and there is no pure helper to call. If GameClock's derivation changes, update this with it.
        /// </summary>
        private Cal Breakdown(double t)
        {
            int totalDays = (int)(t / _config.SecondsPerDay);
            float dayFraction = (float)(t % _config.SecondsPerDay / _config.SecondsPerDay);
            return new Cal
            {
                TotalDays   = totalDays,
                HourOfDay   = dayFraction * 24f,
                Season      = (Season)(totalDays / _config.DaysPerSeason % 4),
                Year        = totalDays / _config.DaysPerSeason / 4 + 1,
                DayOfSeason = totalDays % _config.DaysPerSeason + 1,
                Weekday     = (Weekday)(totalDays % _config.DaysPerWeek),
                IsMarketDay = totalDays % _config.DaysPerWeek == _config.MarketDayIndex
            };
        }

        private struct Cal
        {
            public int TotalDays;
            public float HourOfDay;
            public Season Season;
            public int Year;
            public int DayOfSeason;
            public Weekday Weekday;
            public bool IsMarketDay;
        }

        private static string FormatClock(float hourOfDay)
        {
            hourOfDay = Mathf.Repeat(hourOfDay, 24f);
            int hh = Mathf.FloorToInt(hourOfDay);
            int mm = Mathf.FloorToInt((hourOfDay - hh) * 60f);
            return $"{hh:00}:{mm:00}";
        }

        private string FormatDuration(double inGameSeconds)
        {
            double hours = inGameSeconds / _config.SecondsPerHour;
            int hh = (int)Math.Floor(hours);
            int mm = (int)Math.Round((hours - hh) * 60.0);
            if (mm == 60) { hh++; mm = 0; }
            return hh > 0 ? $"{hh}h {mm:00}m" : $"{mm}m";
        }

        private static string Pretty(Season s) => s switch
        {
            Season.EarlySpring => "Early Spring",
            Season.HighSummer  => "High Summer",
            Season.TheTurn     => "The Turn",
            Season.HardWinter  => "Hard Winter",
            _ => s.ToString()
        };
    }
}
#endif
