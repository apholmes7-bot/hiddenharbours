#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;        // GameConfig, TideProfile
using HiddenHarbours.Environment; // TideModel
using HiddenHarbours.UI;          // TideReadout / TideState — the HUD's own derivation (no fork)

namespace HiddenHarbours.Tools.Editor
{
    /// <summary>
    /// VS-06 — a readable Tide Table: today and tomorrow's high/low waters (clock time + height), a
    /// "now" marker, and the 48-hour tide curve. EDITOR-ONLY and read-only.
    ///
    /// It does NOT fork the tide derivation. Heights come straight from <see cref="TideModel.Height"/>,
    /// and every high/low turn is found by walking <see cref="TideReadout.Derive"/> — the SAME
    /// rising/turn logic the HUD uses — so the table, the HUD and the Tide Scrubber can never disagree.
    /// It mirrors the Scrubber's GameConfig/TideProfile resolution so the two tools feel of-a-piece.
    ///
    /// Menu: <b>Hidden Harbours ▸ Tools ▸ Tide Table</b>.
    /// </summary>
    public class TideTableWindow : EditorWindow
    {
        // --- Persisted state (survives domain reload via the window's serialization) ---
        [SerializeField] private GameConfig _config;
        [SerializeField] private double _totalSeconds;   // the master-clock "now"
        // Editable tide profile (defaults to the greybox Coddle Cove shape) — matches the Scrubber.
        [SerializeField] private float _meanLevel = 0f;
        [SerializeField] private float _amplitude = 1.6f;
        [SerializeField] private float _phaseHours = 0f;
        [SerializeField] private Vector2 _scroll;

        // Colours kept local so the tool reads the same in light/dark editor skins.
        private static readonly Color CurveColor = new Color(0.30f, 0.70f, 1.00f);
        private static readonly Color DatumColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color GridColor  = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color NowColor   = new Color(1f, 0.85f, 0.25f);
        private static readonly Color HighColor  = new Color(0.45f, 1f, 0.55f);
        private static readonly Color LowColor   = new Color(1f, 0.55f, 0.45f);
        private static readonly Color PanelColor = new Color(0.16f, 0.18f, 0.20f);

        private struct TideEvent { public double Seconds; public float Height; public bool High; }

        [MenuItem("Hidden Harbours/Tools/Tide Table")]
        public static void Open()
        {
            var win = GetWindow<TideTableWindow>("Tide Table");
            win.minSize = new Vector2(440f, 560f);
            win.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Tide Table", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Today & tomorrow's high and low waters, with the live tide curve. Editor-only (VS-06).",
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

            DrawProfileSection();
            EditorGUILayout.Space();
            DrawNowControls();
            EditorGUILayout.Space();

            TideProfile profile = BuildProfile();
            List<TideEvent> events = FindEvents(profile);

            DrawNowReadout(profile);
            EditorGUILayout.Space();
            DrawDayTables(events);
            EditorGUILayout.Space();
            DrawCurve(profile, events);

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

        private void DrawProfileSection()
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

        // ------------------------------------------------------------------ "now" controls

        private void DrawNowControls()
        {
            EditorGUILayout.LabelField("“Now”", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                double secPerDay = _config.SecondsPerDay;
                int day = (int)Math.Floor(_totalSeconds / secPerDay);
                float hourOfDay = (float)((_totalSeconds / secPerDay - day) * 24.0);

                EditorGUI.BeginChangeCheck();
                float newHour = EditorGUILayout.Slider(
                    new GUIContent("Hour of day", "Move 'now' within the current in-game day."), hourOfDay, 0f, 24f);
                if (EditorGUI.EndChangeCheck()) SetTime((day + newHour / 24.0) * secPerDay);

                EditorGUI.BeginChangeCheck();
                int newDay = EditorGUILayout.IntField(
                    new GUIContent("Day index", "Absolute in-game day (0-based)."), day);
                if (EditorGUI.EndChangeCheck()) SetTime((newDay + hourOfDay / 24.0) * secPerDay);

                _totalSeconds = Math.Max(0.0, EditorGUILayout.DoubleField(
                    new GUIContent("gameTime (s)", "Raw master clock value (IGameClock.TotalSeconds)."), _totalSeconds));

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

        // ------------------------------------------------------------------ event finding (reuses TideReadout)

        /// <summary>
        /// All high/low turns across the 48 h window [start-of-today, +2 days), located by walking the
        /// HUD's own <see cref="TideReadout.Derive"/> turn-finder forward — no separate extrema maths.
        /// </summary>
        private List<TideEvent> FindEvents(TideProfile profile)
        {
            var list = new List<TideEvent>();
            double secPerDay = _config.SecondsPerDay;
            double dayStart = Math.Floor(_totalSeconds / secPerDay) * secPerDay;
            double windowEnd = dayStart + 2.0 * secPerDay;

            double risingDt = _config.SecondsPerHour * 0.05;  // mirror TideModel/TideReadout (~3 in-game min)
            double scanStep = _config.SecondsPerHour * 0.10;
            double horizon  = _config.SecondsPerHour * _config.TidalPeriodHours;
            Func<double, float> heightAt = s => TideModel.Height(s, profile, _config);

            double t = dayStart;
            int guard = 0;
            while (t < windowEnd && guard++ < 64)
            {
                TideState st = TideReadout.Derive(heightAt, t, risingDt, scanStep, horizon);
                if (!st.HasTurn) break;                       // no turn within a period (shouldn't happen)
                double turnT = t + st.SecondsToTurn;
                if (turnT >= windowEnd) break;
                // Rising into the turn ⇒ it's a high water; falling ⇒ a low.
                list.Add(new TideEvent { Seconds = turnT, Height = heightAt(turnT), High = st.Rising });
                t = turnT + risingDt * 4.0;                   // step just past the turn to find the next
            }
            return list;
        }

        // ------------------------------------------------------------------ readouts & tables

        private void DrawNowReadout(TideProfile profile)
        {
            double risingDt = _config.SecondsPerHour * 0.05;
            double scanStep = _config.SecondsPerHour * 0.10;
            double horizon  = _config.SecondsPerHour * _config.TidalPeriodHours;
            TideState st = TideReadout.Derive(s => TideModel.Height(s, profile, _config), _totalSeconds,
                                              risingDt, scanStep, horizon);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Now", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"Day {DayOf(_totalSeconds)} · {FormatClock(ClockOf(_totalSeconds))}   ·   " +
                    $"{st.HeightMeters:0.00} m (rel. datum)   ·   {(st.Rising ? "rising (flood)" : "falling (ebb)")}");
                if (st.HasTurn)
                {
                    float turnClock = ClockOf(_totalSeconds + st.SecondsToTurn);
                    EditorGUILayout.LabelField(
                        $"Next {(st.Rising ? "high" : "low")} water in {FormatDuration(st.SecondsToTurn)} (≈ {FormatClock(turnClock)}).");
                }
            }
        }

        private void DrawDayTables(List<TideEvent> events)
        {
            int day0 = DayOf(_totalSeconds);
            DrawOneDay($"Today — Day {day0}", events, day0);
            DrawOneDay($"Tomorrow — Day {day0 + 1}", events, day0 + 1);
        }

        private void DrawOneDay(string title, List<TideEvent> events, int dayIndex)
        {
            double secPerDay = _config.SecondsPerDay;
            bool nowOnThisDay = DayOf(_totalSeconds) == dayIndex;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

                bool any = false, nowShown = false;
                foreach (var e in events)
                {
                    if ((int)Math.Floor(e.Seconds / secPerDay) != dayIndex) continue;
                    any = true;
                    if (nowOnThisDay && !nowShown && _totalSeconds <= e.Seconds)
                    {
                        DrawNowRow();
                        nowShown = true;
                    }
                    DrawEventRow(e);
                }
                if (nowOnThisDay && !nowShown) DrawNowRow(); // now is after this day's last turn
                if (!any) EditorGUILayout.LabelField("   (no turns this day)", EditorStyles.miniLabel);
            }
        }

        private void DrawEventRow(TideEvent e)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Color prev = GUI.color;
                GUI.color = e.High ? HighColor : LowColor;
                EditorGUILayout.LabelField(e.High ? "  ▲ High water" : "  ▼ Low water", GUILayout.Width(140f));
                GUI.color = prev;
                EditorGUILayout.LabelField(FormatClock(ClockOf(e.Seconds)), GUILayout.Width(64f));
                EditorGUILayout.LabelField($"{e.Height:+0.00;-0.00;0.00} m", GUILayout.Width(80f));
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawNowRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Color prev = GUI.color;
                GUI.color = NowColor;
                EditorGUILayout.LabelField("  ◀ now", GUILayout.Width(140f));
                EditorGUILayout.LabelField(FormatClock(ClockOf(_totalSeconds)), GUILayout.Width(64f));
                GUI.color = prev;
                GUILayout.FlexibleSpace();
            }
        }

        // ------------------------------------------------------------------ 48 h curve

        private void DrawCurve(TideProfile profile, List<TideEvent> events)
        {
            EditorGUILayout.LabelField("Tide curve — 48 h from start of today", EditorStyles.boldLabel);

            Rect area = GUILayoutUtility.GetRect(100f, 200f, GUILayout.ExpandWidth(true));
            area = new RectOffset(4, 4, 2, 18).Remove(area); // leave a strip at the bottom for time labels
            EditorGUI.DrawRect(area, PanelColor);
            if (Event.current.type != EventType.Repaint) return;

            double secPerDay = _config.SecondsPerDay;
            double dayStart = Math.Floor(_totalSeconds / secPerDay) * secPerDay;
            double windowSeconds = 2.0 * secPerDay;
            double windowHours = windowSeconds / _config.SecondsPerHour;
            const int N = 256;

            var h = new float[N];
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < N; i++)
            {
                double s = dayStart + windowSeconds * (i / (double)(N - 1));
                h[i] = TideModel.Height(s, profile, _config);
                min = Mathf.Min(min, h[i]);
                max = Mathf.Max(max, h[i]);
            }
            float pad = Mathf.Max(0.05f, (max - min) * 0.1f);
            min -= pad; max += pad;

            float Xfrac(double frac) => area.x + area.width * (float)frac;
            float Y(float v) => area.yMax - (v - min) / (max - min) * area.height;

            // 6-hour gridlines + hour-of-day labels along the bottom strip.
            Handles.color = GridColor;
            for (double hh = 0; hh <= windowHours + 0.001; hh += 6)
            {
                float gx = Xfrac(hh / windowHours);
                Handles.DrawLine(new Vector3(gx, area.y), new Vector3(gx, area.yMax));
                float clock = ClockOf(dayStart + hh * _config.SecondsPerHour);
                GUI.Label(new Rect(gx - 16f, area.yMax + 2f, 44f, 14f), FormatClock(clock), EditorStyles.miniLabel);
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
            for (int i = 0; i < N; i++) pts[i] = new Vector3(area.x + area.width * (i / (float)(N - 1)), Y(h[i]), 0f);
            Handles.color = CurveColor;
            Handles.DrawAAPolyLine(2.5f, pts);

            // High/low markers from the same events the table lists (no re-detection).
            foreach (var e in events)
            {
                double frac = (e.Seconds - dayStart) / windowSeconds;
                if (frac < 0.0 || frac > 1.0) continue;
                float x = Xfrac(frac), y = Y(e.Height);
                Handles.color = e.High ? HighColor : LowColor;
                Handles.DrawSolidDisc(new Vector3(x, y, 0f), Vector3.forward, 3f);
                GUI.Label(new Rect(x - 18f, y + (e.High ? -16f : 4f), 64f, 14f),
                    $"{(e.High ? "H" : "L")} {FormatClock(ClockOf(e.Seconds))}", EditorStyles.miniLabel);
            }

            // "Now" marker at its real position + current-height dot.
            double nowFrac = (_totalSeconds - dayStart) / windowSeconds;
            if (nowFrac >= 0.0 && nowFrac <= 1.0)
            {
                float nx = Xfrac(nowFrac);
                Handles.color = NowColor;
                Handles.DrawLine(new Vector3(nx, area.y), new Vector3(nx, area.yMax));
                float ny = Y(TideModel.Height(_totalSeconds, profile, _config));
                Handles.DrawSolidDisc(new Vector3(nx, ny, 0f), Vector3.forward, 3.5f);
            }

            // Y range labels.
            GUI.Label(new Rect(area.x + 2f, area.y, 80f, 14f), $"{max:0.0} m", EditorStyles.miniLabel);
            GUI.Label(new Rect(area.x + 2f, area.yMax - 14f, 80f, 14f), $"{min:0.0} m", EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------------ small helpers

        /// <summary>Absolute in-game day index for a master-clock value.</summary>
        private int DayOf(double seconds) => (int)Math.Floor(seconds / _config.SecondsPerDay);

        /// <summary>Hour-of-day (0–24) for a master-clock value.</summary>
        private float ClockOf(double seconds)
        {
            double d = seconds / _config.SecondsPerDay;
            return (float)((d - Math.Floor(d)) * 24.0);
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
    }
}
#endif
