#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;     // FishCategory, Rarity
using HiddenHarbours.Fishing;  // FishSpeciesDef, Gear, SeasonMask

namespace HiddenHarbours.Tools.Editor
{
    /// <summary>
    /// VS-29 — a friendly authoring inspector for <see cref="FishSpeciesDef"/> so designers can
    /// shape *where & when a fish bites* without fiddling raw enum masks and paired float fields.
    ///
    /// EDITOR-ONLY and presentation-only: every widget reads/writes the SAME serialized fields the
    /// runtime already uses (via <see cref="SerializedObject"/>, so Undo / multi-edit / prefab
    /// overrides all work for free). It adds NO data and changes NO runtime behaviour — it only
    /// dresses the existing fields in readable controls:
    ///   • tide window as a min/max slider,
    ///   • the time-of-day window as twin hour sliders + a 24-hour band that shows the
    ///     wrap-past-midnight ("night biter") case plainly,
    ///   • the season mask and gear flags as labelled toggle grids,
    ///   • weight range as a min/max slider, and value / elasticity / spawn-weight grouped sensibly.
    ///
    /// The "Authoring hints" box at the bottom is DISPLAY-ONLY and explicitly NOT the source of
    /// truth: the authoritative content-validation rule lives with qa-test. These hints just catch
    /// the obvious "empty window" mistakes at a glance while authoring.
    /// </summary>
    [CustomEditor(typeof(FishSpeciesDef))]
    [CanEditMultipleObjects]
    public class FishSpeciesDefEditor : UnityEditor.Editor
    {
        // Colours kept local so the inspector reads the same in light/dark editor skins
        // (matches TideScrubberWindow's palette so the tooling looks of-a-piece).
        private static readonly Color PanelColor  = new Color(0.16f, 0.18f, 0.20f);
        private static readonly Color ActiveColor = new Color(0.30f, 0.70f, 1.00f);
        private static readonly Color GridColor   = new Color(1f, 1f, 1f, 0.12f);

        // Serialized fields (paths match the public field names on FishSpeciesDef).
        private SerializedProperty _id, _displayName, _category, _rarity, _flavor, _sprite;
        private SerializedProperty _regionIds, _allowedGear, _seasons;
        private SerializedProperty _minTide, _maxTide, _startHour, _endHour;
        private SerializedProperty _minWeight, _maxWeight, _baseValue, _supplyElasticity, _spawnWeight;

        // Single-bit options for the flag grids, discovered from the enums so they never drift.
        private (int bit, GUIContent label)[] _gearOptions;
        private (int bit, GUIContent label)[] _seasonOptions;

        private void OnEnable()
        {
            _id               = serializedObject.FindProperty("Id");
            _displayName      = serializedObject.FindProperty("DisplayName");
            _category         = serializedObject.FindProperty("Category");
            _rarity           = serializedObject.FindProperty("Rarity");
            _flavor           = serializedObject.FindProperty("Flavor");
            _sprite           = serializedObject.FindProperty("Sprite");

            _regionIds        = serializedObject.FindProperty("RegionIds");
            _allowedGear      = serializedObject.FindProperty("AllowedGear");
            _seasons          = serializedObject.FindProperty("Seasons");

            _minTide          = serializedObject.FindProperty("MinTide");
            _maxTide          = serializedObject.FindProperty("MaxTide");
            _startHour        = serializedObject.FindProperty("StartHour");
            _endHour          = serializedObject.FindProperty("EndHour");

            _minWeight        = serializedObject.FindProperty("MinWeightKg");
            _maxWeight        = serializedObject.FindProperty("MaxWeightKg");
            _baseValue        = serializedObject.FindProperty("BaseValue");
            _supplyElasticity = serializedObject.FindProperty("SupplyElasticity");
            _spawnWeight      = serializedObject.FindProperty("SpawnWeight");

            _gearOptions   = BuildFlagOptions(typeof(Gear));
            _seasonOptions = BuildFlagOptions(typeof(SeasonMask));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawIdentity();
            DrawArt();
            DrawWhereAndWhen();
            DrawCatchAndValue();
            DrawAuthoringHints();

            serializedObject.ApplyModifiedProperties();
        }

        // ------------------------------------------------------------------ identity

        private void DrawIdentity()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_id, new GUIContent(
                    "Id", "Stable, append-only id (e.g. fish.atlantic_cod). Save & market state key off " +
                          "it — rename the display name freely, but never reuse or change an id."));
                EditorGUILayout.PropertyField(_displayName, new GUIContent("Display Name", "Player-facing name."));
                EditorGUILayout.PropertyField(_category, new GUIContent("Category", "Catch category — drives UI grouping and market buyers."));
                EditorGUILayout.PropertyField(_rarity, new GUIContent("Rarity", "Rarity tier — guides spawn-weight and value bands."));
                EditorGUILayout.PropertyField(_flavor, new GUIContent("Flavor", "Short, warm, Maritime-voiced almanac copy."));
            }
        }

        // ------------------------------------------------------------------ art

        private void DrawArt()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Art", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(_sprite, new GUIContent(
                        "Sprite", "Optional species sprite (icon / haul art). Attached by art-pipeline later; never required."));

                    // Small live preview so designers can confirm they picked the right art.
                    var sprite = _sprite.objectReferenceValue as Sprite;
                    if (sprite != null && sprite.texture != null)
                    {
                        Rect r = GUILayoutUtility.GetRect(48f, 48f, GUILayout.Width(48f), GUILayout.Height(48f));
                        DrawSpritePreview(r, sprite);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ where & when it bites

        private void DrawWhereAndWhen()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Where & when it bites", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "The gating fields — these make the same ground fish differently across a day and a year (Pillar 1).",
                    EditorStyles.miniLabel);
                EditorGUILayout.Space(2);

                // Regions — the default array UI is already clear; keep it, just give it a label/tooltip.
                EditorGUILayout.PropertyField(_regionIds, new GUIContent(
                    "Region Ids", "Region ids this fish appears in (e.g. region.coddle_cove). Empty = appears nowhere."), true);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Gear that can take it", EditorStyles.miniBoldLabel);
                DrawFlagsGrid(_allowedGear, _gearOptions, columns: 3);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Seasons it bites", EditorStyles.miniBoldLabel);
                DrawFlagsGrid(_seasons, _seasonOptions, columns: 2);
                DrawSeasonQuickButtons();

                EditorGUILayout.Space(4);
                DrawTideWindow();

                EditorGUILayout.Space(4);
                DrawTimeWindow();
            }
        }

        /// <summary>Tide window as a paired min/max slider + exact float fields (metres rel. datum).</summary>
        private void DrawTideWindow()
        {
            EditorGUILayout.LabelField(new GUIContent(
                "Tide window (m, rel. datum)", "Only bites when Min ≤ tide ≤ Max."), EditorStyles.miniBoldLabel);

            DrawMinMaxRow(_minTide, _maxTide, softLo: -4f, softHi: 4f, fieldWidth: 56f, snap: false);

            if (!_minTide.hasMultipleDifferentValues && !_maxTide.hasMultipleDifferentValues)
            {
                float lo = _minTide.floatValue, hi = _maxTide.floatValue;
                string msg = hi < lo
                    ? "Min is above Max — the tide window is empty (nothing bites)."
                    : (lo <= -8f && hi >= 8f)
                        ? $"Bites across roughly any tide ({lo:0.#}–{hi:0.#} m) — effectively tide-agnostic."
                        : $"Bites when the tide is between {lo:0.#} m and {hi:0.#} m.";
                EditorGUILayout.LabelField("   " + msg, EditorStyles.miniLabel);
            }
        }

        /// <summary>
        /// Time-of-day window: twin hour sliders plus a 24-hour band that shows the wrap-past-midnight
        /// (night-biter) case plainly. Mirrors <see cref="FishSpeciesDef.TimeAllowed"/>'s semantics
        /// for the readout — it does not re-implement the gate, only describes it.
        /// </summary>
        private void DrawTimeWindow()
        {
            EditorGUILayout.LabelField(new GUIContent(
                "Time-of-day window (hour 0–24)",
                "If Start > End the window wraps past midnight (e.g. a night biter). Start ≈ End = all day."),
                EditorStyles.miniBoldLabel);

            EditorGUI.showMixedValue = _startHour.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            float start = EditorGUILayout.Slider("Start hour", _startHour.floatValue, 0f, 24f);
            if (EditorGUI.EndChangeCheck()) _startHour.floatValue = start;
            EditorGUI.showMixedValue = false;

            EditorGUI.showMixedValue = _endHour.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            float end = EditorGUILayout.Slider("End hour", _endHour.floatValue, 0f, 24f);
            if (EditorGUI.EndChangeCheck()) _endHour.floatValue = end;
            EditorGUI.showMixedValue = false;

            if (_startHour.hasMultipleDifferentValues || _endHour.hasMultipleDifferentValues)
            {
                EditorGUILayout.LabelField("   (multiple values — select one fish to preview the band)", EditorStyles.miniLabel);
                return;
            }

            float s = _startHour.floatValue, e = _endHour.floatValue;
            Rect band = EditorGUILayout.GetControlRect(false, 22f);
            DrawTimeBand(band, s, e);

            bool allDay = Mathf.Approximately(s, e) || (s <= 0f && e >= 24f);
            string msg;
            if (allDay)
                msg = "Bites all day (00:00–24:00).";
            else if (s < e)
                msg = $"Bites {FormatClock(s)}–{FormatClock(e)}.";
            else
                msg = $"Bites {FormatClock(s)}–24:00 and 00:00–{FormatClock(e)} — wraps past midnight (a night biter).";
            EditorGUILayout.LabelField("   " + msg, EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------------ catch & value

        private void DrawCatchAndValue()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Catch & value", EditorStyles.boldLabel);

                EditorGUILayout.LabelField(new GUIContent(
                    "Weight range (kg)", "Caught weight is rolled within this range."), EditorStyles.miniBoldLabel);
                DrawMinMaxRow(_minWeight, _maxWeight, softLo: 0f, softHi: 10f, fieldWidth: 56f, snap: false, clampLo: 0f);

                if (!_minWeight.hasMultipleDifferentValues && !_maxWeight.hasMultipleDifferentValues)
                {
                    float lo = _minWeight.floatValue, hi = _maxWeight.floatValue;
                    string msg = hi < lo
                        ? "Min weight is above Max — the range is empty."
                        : $"Catches weigh {lo:0.#}–{hi:0.#} kg.";
                    EditorGUILayout.LabelField("   " + msg, EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(_baseValue, new GUIContent(
                    "Base Value (₲)", "Reference price at a neutral market; the economy re-prices on top."));

                EditorGUI.showMixedValue = _supplyElasticity.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                float elasticity = EditorGUILayout.Slider(new GUIContent(
                    "Supply Elasticity", "How fast landing it depresses the price. 0 = holds value, 1 = crashes fast."),
                    _supplyElasticity.floatValue, 0f, 1f);
                if (EditorGUI.EndChangeCheck()) _supplyElasticity.floatValue = elasticity;
                EditorGUI.showMixedValue = false;

                if (!_supplyElasticity.hasMultipleDifferentValues)
                {
                    float el = _supplyElasticity.floatValue;
                    string tag = el <= 0.25f ? "holds its value" : el >= 0.55f ? "gluts hard when over-supplied" : "moderate";
                    EditorGUILayout.LabelField($"   Price behaviour: {tag}.", EditorStyles.miniLabel);
                }

                EditorGUILayout.PropertyField(_spawnWeight, new GUIContent(
                    "Spawn Weight", "Relative likelihood among matching fish (rarer = lower)."));
            }
        }

        // ------------------------------------------------------------------ authoring hints (display-only)

        private void DrawAuthoringHints()
        {
            EditorGUILayout.Space(2);

            // Hints are a single-target convenience; with a multi-selection we don't second-guess.
            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.HelpBox(
                    "Select a single fish to see authoring hints. (Hints are display-only; the content " +
                    "validator is the source of truth.)", MessageType.None);
                return;
            }

            var warnings = new List<string>();
            var notes = new List<string>();

            if (_regionIds.arraySize == 0) warnings.Add("No regions set — this fish appears nowhere.");
            if (_allowedGear.intValue == 0) warnings.Add("No gear selected — nothing can catch it.");
            if (_seasons.intValue == 0) warnings.Add("No seasons selected — it never bites.");
            if (_maxTide.floatValue < _minTide.floatValue) warnings.Add("Tide window is inverted (Min > Max) — empty window.");
            if (_maxWeight.floatValue < _minWeight.floatValue) warnings.Add("Weight range is inverted (Min > Max).");

            if (_sprite.objectReferenceValue == null) notes.Add("No sprite yet — fine during greybox; art lands later.");

            string body;
            MessageType type;
            if (warnings.Count > 0)
            {
                body = "• " + string.Join("\n• ", warnings);
                if (notes.Count > 0) body += "\n• " + string.Join("\n• ", notes);
                type = MessageType.Warning;
            }
            else if (notes.Count > 0)
            {
                body = "• " + string.Join("\n• ", notes);
                type = MessageType.Info;
            }
            else
            {
                body = "Nothing obviously missing.";
                type = MessageType.Info;
            }

            EditorGUILayout.HelpBox(
                "Authoring hints (display-only — the content validator is the source of truth):\n" + body, type);
        }

        // ------------------------------------------------------------------ shared widgets

        /// <summary>A row of [min field] [min/max slider] [max field] driven by two float properties.</summary>
        private void DrawMinMaxRow(SerializedProperty minProp, SerializedProperty maxProp,
                                   float softLo, float softHi, float fieldWidth, bool snap, float? clampLo = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.showMixedValue = minProp.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                float newMin = EditorGUILayout.FloatField(minProp.floatValue, GUILayout.Width(fieldWidth));
                if (EditorGUI.EndChangeCheck()) minProp.floatValue = clampLo.HasValue ? Mathf.Max(clampLo.Value, newMin) : newMin;
                EditorGUI.showMixedValue = false;

                // Expand slider bounds to comfortably contain current values so we never clamp authored data.
                float a = minProp.floatValue, b = maxProp.floatValue;
                float boundLo = Mathf.Min(softLo, Mathf.Floor(Mathf.Min(a, b)));
                float boundHi = Mathf.Max(softHi, Mathf.Ceil(Mathf.Max(a, b)));
                if (clampLo.HasValue) boundLo = Mathf.Max(clampLo.Value, boundLo);

                bool mixed = minProp.hasMultipleDifferentValues || maxProp.hasMultipleDifferentValues;
                using (new EditorGUI.DisabledScope(mixed))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.MinMaxSlider(ref a, ref b, boundLo, boundHi);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (snap) { a = Mathf.Round(a); b = Mathf.Round(b); }
                        minProp.floatValue = a;
                        maxProp.floatValue = b;
                    }
                }

                EditorGUI.showMixedValue = maxProp.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                float newMax = EditorGUILayout.FloatField(maxProp.floatValue, GUILayout.Width(fieldWidth));
                if (EditorGUI.EndChangeCheck()) maxProp.floatValue = newMax;
                EditorGUI.showMixedValue = false;
            }
        }

        /// <summary>Labelled toggle grid over a [Flags] enum's single-bit values, writing the int mask.</summary>
        private void DrawFlagsGrid(SerializedProperty maskProp, (int bit, GUIContent label)[] options, int columns)
        {
            int value = maskProp.intValue;
            bool mixed = maskProp.hasMultipleDifferentValues;

            using (new EditorGUI.IndentLevelScope())
            {
                int i = 0;
                while (i < options.Length)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (int c = 0; c < columns && i < options.Length; c++, i++)
                        {
                            var opt = options[i];
                            bool on = (value & opt.bit) != 0;
                            EditorGUI.showMixedValue = mixed;
                            EditorGUI.BeginChangeCheck();
                            bool now = EditorGUILayout.ToggleLeft(opt.label, on, GUILayout.MinWidth(110f));
                            if (EditorGUI.EndChangeCheck())
                            {
                                int v = mixed ? 0 : value; // resolving a mixed selection starts from a clean mask
                                v = now ? (v | opt.bit) : (v & ~opt.bit);
                                maskProp.intValue = v;
                                value = v;
                                mixed = false;
                            }
                            EditorGUI.showMixedValue = false;
                        }
                    }
                }
            }
        }

        private void DrawSeasonQuickButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth);
                if (GUILayout.Button("All year", EditorStyles.miniButtonLeft, GUILayout.Width(70f)))
                {
                    int all = 0;
                    foreach (var o in _seasonOptions) all |= o.bit;
                    _seasons.intValue = all;
                }
                if (GUILayout.Button("None", EditorStyles.miniButtonRight, GUILayout.Width(70f)))
                    _seasons.intValue = 0;
                GUILayout.FlexibleSpace();
            }
        }

        // ------------------------------------------------------------------ drawing helpers

        private void DrawTimeBand(Rect r, float start, float end)
        {
            EditorGUI.DrawRect(r, PanelColor);

            start = Mathf.Clamp(start, 0f, 24f);
            end = Mathf.Clamp(end, 0f, 24f);
            bool allDay = Mathf.Approximately(start, end) || (start <= 0f && end >= 24f);

            if (allDay)
            {
                EditorGUI.DrawRect(r, ActiveColor);
            }
            else if (start < end)
            {
                DrawHourSpan(r, start, end);
            }
            else // wraps past midnight
            {
                DrawHourSpan(r, start, 24f);
                DrawHourSpan(r, 0f, end);
            }

            // 6-hour ticks + labels so the band is readable.
            for (int h = 0; h <= 24; h += 6)
            {
                float x = r.x + r.width * (h / 24f);
                EditorGUI.DrawRect(new Rect(x, r.y, 1f, r.height), GridColor);
                var lbl = new Rect(x - 14f, r.yMax - 13f, 30f, 12f);
                GUI.Label(lbl, $"{h:00}", EditorStyles.miniLabel);
            }
        }

        private void DrawHourSpan(Rect r, float fromHour, float toHour)
        {
            float x0 = r.x + r.width * (fromHour / 24f);
            float x1 = r.x + r.width * (toHour / 24f);
            EditorGUI.DrawRect(new Rect(x0, r.y, Mathf.Max(0f, x1 - x0), r.height), ActiveColor);
        }

        private static void DrawSpritePreview(Rect r, Sprite sprite)
        {
            // Draw only the sprite's sub-rect of its texture (handles atlased sprites).
            Texture tex = sprite.texture;
            Rect tr = sprite.textureRect;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            GUI.DrawTextureWithTexCoords(r, tex, uv, alphaBlend: true);
        }

        // ------------------------------------------------------------------ small utilities

        /// <summary>
        /// Single-bit flag values discovered from a [Flags] enum, with display labels spaced out of
        /// PascalCase. Skips 0 (None) and composite values (e.g. AllYear) so the grid is just the
        /// real, independent toggles — and it tracks the enum if bits are ever added.
        /// </summary>
        private static (int bit, GUIContent label)[] BuildFlagOptions(Type enumType)
        {
            var list = new List<(int, GUIContent)>();
            foreach (var v in Enum.GetValues(enumType))
            {
                int bit = Convert.ToInt32(v);
                if (bit == 0 || (bit & (bit - 1)) != 0) continue; // skip None and composites
                list.Add((bit, new GUIContent(Prettify(Enum.GetName(enumType, v)))));
            }
            return list.ToArray();
        }

        /// <summary>"EarlySpring" → "Early Spring", "Handline" → "Handline".</summary>
        private static string Prettify(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var sb = new System.Text.StringBuilder(name.Length + 4);
            sb.Append(name[0]);
            for (int i = 1; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && name[i - 1] != ' ') sb.Append(' ');
                sb.Append(name[i]);
            }
            return sb.ToString();
        }

        private static string FormatClock(float hourOfDay)
        {
            hourOfDay = Mathf.Repeat(hourOfDay, 24f);
            int hh = Mathf.FloorToInt(hourOfDay);
            int mm = Mathf.FloorToInt((hourOfDay - hh) * 60f);
            return $"{hh:00}:{mm:00}";
        }
    }
}
#endif
