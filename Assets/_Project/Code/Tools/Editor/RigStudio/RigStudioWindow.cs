#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigStudio
{
    /// <summary>
    /// <b>Rig Studio</b> — one window over every imported rig kit: browse a kit's parameter space,
    /// see the actual pixels live, and mint exactly what is on screen into the placeable pool with
    /// one button. Menu: <c>Hidden Harbours ▸ Art ▸ Rig Studio</c>.
    ///
    /// <para><b>The studio knows NOTHING about any specific rig.</b> Each kit teaches the studio
    /// about itself through an <see cref="IRigStudioAdapter"/> (lead-architect ruling, 2026-07-30):
    /// the adapter enumerates the axes, renders the preview through the kit's own bake path, and
    /// delegates the bake to the kit's existing baker + slicer + catalog refresh. A new kit import
    /// ships an adapter and this window grows a tab — zero changes here.</para>
    ///
    /// <para><b>What the preview is.</b> Not an approximation: the same render entry the kit's baker
    /// uses, bit-identical to what a bake at that point produces, pinned by a test per adapter. The
    /// owner is approving the pixels that ship. Rendered on parameter CHANGE only — never per
    /// frame — point-filtered (pixel art — no smoothing), scaled to fit with a 1:1 toggle.</para>
    ///
    /// <para><b>What "Bake this" is.</b> The kit's own menu bake, driven from here: output lands in
    /// the WORKING TREE exactly where a menu bake would put it, and the studio never commits
    /// anything (bake-to-order). Kit guards hold — a held-back species stays unbakeable, and a
    /// slicer refusal surfaces verbatim, never swallowed.</para>
    /// </summary>
    public sealed class RigStudioWindow : EditorWindow
    {
        const float AxesPanelWidth = 300f;
        const float PreviewPad = 8f;

        // Priority 37 heads the Art menu's unbroken 38–60 studio/bake/slice run, one above the
        // Building Studio — inside the group's existing separator, so the command cannot read as
        // missing (the trap "Bake Buildings" hit at 23), and above the per-kit commands it fronts.
        [MenuItem("Hidden Harbours/Art/Rig Studio", priority = 37)]
        public static void Open()
        {
            var w = GetWindow<RigStudioWindow>("Rig Studio");
            w.minSize = new Vector2(820, 560);
            w.Show();
        }

        // ---- per-tab state --------------------------------------------------------------------

        /// <summary>Everything one kit's tab holds. The point/preview/report live per tab so
        /// switching kits never loses where the owner was.</summary>
        sealed class Tab
        {
            public IRigStudioAdapter Adapter;
            public string Title;

            /// <summary>Adapter construction/describe failure, verbatim. A broken adapter is a
            /// visible broken tab — never a silently absent kit.</summary>
            public string LoadError;

            public RigStudioSchema Schema;
            public Dictionary<string, object> Point;

            public Texture2D Preview;
            public string Caption;
            public bool HasPivot;
            public double PivotX, PivotY;
            public RigStudioGuide[] Guides;

            /// <summary>Last PREVIEW failure, verbatim — the kit refused to render this point.</summary>
            public string Error;

            /// <summary>Last bake's outcome — a report on success, the refusal (plus everything the
            /// kit's chain logged as an error) on failure. Kept apart from <see cref="Error"/> so a
            /// failed bake neither hides a valid preview nor locks the bake button.</summary>
            public string ReportText;
            public bool ReportIsError;

            /// <summary>The preview needs re-rendering. Set on parameter change, cleared after ONE
            /// render — the only mechanism by which this window ever renders.</summary>
            public bool Dirty = true;

            // Cached GUI content, built once per schema load — OnGUI runs every repaint and must
            // not rebuild strings (no per-frame allocations).
            public GUIContent[] AxisLabels;
            public string[] ExcludedSummaries;
        }

        List<Tab> _tabs;
        string[] _tabTitles = Array.Empty<string>();
        int _active;

        bool _oneToOne;
        bool _showPivot = true;
        Vector2 _scrollAxes, _scrollPreview, _scrollReport;

        // ---- lifecycle ------------------------------------------------------------------------

        void OnEnable()
        {
            _tabs = new List<Tab>();

            foreach (var entry in RigStudioAdapterRegistry.CreateAll())
            {
                var tab = new Tab { Adapter = entry.Adapter, Title = entry.Type.Name };

                if (entry.Error != null)
                {
                    tab.LoadError = entry.Error;
                }
                else
                {
                    try
                    {
                        tab.Schema = entry.Adapter.Describe();
                        tab.Title = tab.Schema.KitName;
                        tab.Point = RigStudioPoints.Defaults(tab.Schema);
                        BuildAxisGuiCache(tab);
                    }
                    catch (Exception e)
                    {
                        tab.LoadError = e.Message;
                    }
                }

                _tabs.Add(tab);
            }

            _tabs = _tabs.OrderBy(t => t.Title, StringComparer.Ordinal).ToList();
            _tabTitles = _tabs.Select(t => t.Title).ToArray();
            _active = Mathf.Clamp(_active, 0, Mathf.Max(0, _tabs.Count - 1));
        }

        void OnDisable()
        {
            if (_tabs == null) return;
            foreach (var tab in _tabs)
            {
                try { tab.Adapter?.Dispose(); }
                catch (Exception e) { Debug.LogError($"[rig-studio] disposing '{tab.Title}': {e.Message}"); }

                if (tab.Preview != null) DestroyImmediate(tab.Preview);
                tab.Preview = null;
            }
            _tabs = null;
        }

        static void BuildAxisGuiCache(Tab tab)
        {
            var axes = tab.Schema.Axes;
            tab.AxisLabels = new GUIContent[axes.Length];
            tab.ExcludedSummaries = new string[axes.Length];

            for (int i = 0; i < axes.Length; i++)
            {
                tab.AxisLabels[i] = new GUIContent(axes[i].Label ?? axes[i].Key);

                if (axes[i].Excluded is { Count: > 0 } excluded)
                    tab.ExcludedSummaries[i] = string.Join("\n",
                        excluded.Select(kv => $"⛔ {kv.Key} — {kv.Value}"));
            }
        }

        // ---- GUI ------------------------------------------------------------------------------

        void OnGUI()
        {
            if (_tabs == null || _tabs.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No rig kit adapters found. A kit joins this window by shipping an " +
                    "IRigStudioAdapter (editor-only, public parameterless constructor) — see the " +
                    "interface docs. Every kit import is expected to bring one.", MessageType.Info);
                return;
            }

            DrawToolbar();

            Tab tab = _tabs[_active];

            if (tab.LoadError != null)
            {
                EditorGUILayout.HelpBox(
                    $"This kit's adapter failed to load.\n\n{tab.LoadError}", MessageType.Error);
                return;
            }

            if (!string.IsNullOrEmpty(tab.Schema.About))
                EditorGUILayout.LabelField(tab.Schema.About, EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            DrawAxesPanel(tab);
            DrawPreviewPanel(tab);
            EditorGUILayout.EndHorizontal();

            DrawBakeRow(tab);
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            int next = GUILayout.Toolbar(_active, _tabTitles, EditorStyles.toolbarButton);
            if (next != _active)
            {
                _active = next;
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();
            _showPivot = GUILayout.Toggle(_showPivot, "pivot", EditorStyles.toolbarButton,
                                          GUILayout.Width(50));
            _oneToOne = GUILayout.Toggle(_oneToOne, "1:1", EditorStyles.toolbarButton,
                                         GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
        }

        // ---- axes -----------------------------------------------------------------------------

        void DrawAxesPanel(Tab tab)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(AxesPanelWidth));
            _scrollAxes = EditorGUILayout.BeginScrollView(_scrollAxes);

            EditorGUI.BeginChangeCheck();

            for (int i = 0; i < tab.Schema.Axes.Length; i++)
                DrawAxis(tab, i);

            if (EditorGUI.EndChangeCheck())
            {
                tab.Dirty = true;
                Repaint();      // the render itself waits for the next Layout pass
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawAxis(Tab tab, int index)
        {
            RigStudioAxis axis = tab.Schema.Axes[index];

            switch (axis.Kind)
            {
                case RigStudioAxisKind.Keys:
                    DrawKeysAxis(tab, index, axis);
                    break;

                case RigStudioAxisKind.IntRange:
                    tab.Point[axis.Key] = EditorGUILayout.IntSlider(
                        tab.AxisLabels[index], (int)tab.Point[axis.Key], axis.Min, axis.Max);
                    break;

                case RigStudioAxisKind.Seed:
                    DrawSeedAxis(tab, index, axis);
                    break;
            }

            if (!string.IsNullOrEmpty(axis.Note))
                EditorGUILayout.LabelField(axis.Note, EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
        }

        /// <summary>
        /// A Keys axis as a wrapping grid of CHIPS — every value visible at once, so picking a
        /// species or a preset is a glance, not a dropdown hunt. Excluded values are drawn too,
        /// greyed out with the kit's own reason underneath: hidden, a hold-back looks like missing
        /// art; greyed, it looks like what it is — a decision.
        /// </summary>
        void DrawKeysAxis(Tab tab, int index, RigStudioAxis axis)
        {
            EditorGUILayout.LabelField(tab.AxisLabels[index], EditorStyles.miniBoldLabel);

            string current = (string)tab.Point[axis.Key];
            int idx = Array.IndexOf(axis.Keys, current);

            int perRow = Mathf.Max(1, Mathf.FloorToInt((AxesPanelWidth - 20f) / 92f));
            int next = GUILayout.SelectionGrid(idx, axis.Keys, perRow, EditorStyles.miniButton);
            if (next != idx && next >= 0)
                tab.Point[axis.Key] = axis.Keys[next];

            if (tab.ExcludedSummaries[index] != null)
                EditorGUILayout.HelpBox(tab.ExcludedSummaries[index], MessageType.None);
        }

        /// <summary>A Seed axis: the number is always visible and always the owner's — there is no
        /// hidden randomness anywhere in this window, only a "next" step.</summary>
        void DrawSeedAxis(Tab tab, int index, RigStudioAxis axis)
        {
            EditorGUILayout.BeginHorizontal();

            int value = Mathf.Clamp(
                EditorGUILayout.IntField(tab.AxisLabels[index], (int)tab.Point[axis.Key]),
                axis.Min, axis.Max);

            if (GUILayout.Button("next ▶", EditorStyles.miniButton, GUILayout.Width(52)))
            {
                value = value >= axis.Max ? axis.Min : value + 1;
                GUI.changed = true;
            }

            tab.Point[axis.Key] = value;
            EditorGUILayout.EndHorizontal();
        }

        // ---- preview --------------------------------------------------------------------------

        void DrawPreviewPanel(Tab tab)
        {
            EditorGUILayout.BeginVertical();

            RefreshPreviewIfDirty(tab);

            if (tab.Caption != null)
                EditorGUILayout.LabelField(tab.Caption, EditorStyles.boldLabel);

            if (tab.Error != null)
            {
                // Verbatim, and prominent: the kit refused, and its own words say why. Rendering
                // any fallback instead of this box is the defect class this studio retires.
                EditorGUILayout.HelpBox(tab.Error, MessageType.Error);
            }

            Rect area = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true),
                                                 GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(area, new Color(0.16f, 0.17f, 0.19f));

            if (tab.Preview != null && tab.Error == null)
            {
                if (_oneToOne) DrawOneToOne(tab, area);
                else DrawFitted(tab, area);
            }

            EditorGUILayout.EndVertical();
        }

        void DrawFitted(Tab tab, Rect area)
        {
            Rect fit = FitRect(tab.Preview.width, tab.Preview.height, area, PreviewPad);
            GUI.DrawTexture(fit, tab.Preview, ScaleMode.StretchToFill, alphaBlend: true);

            if (_showPivot && tab.HasPivot)
                DrawPivotCross(tab, fit);
            DrawGuides(tab, fit);
        }

        void DrawOneToOne(Tab tab, Rect area)
        {
            // Native pixels inside a scroll view — the "is that dither right" inspection mode.
            GUI.BeginGroup(area);
            var inner = new Rect(0, 0, area.width, area.height);
            var content = new Rect(0, 0, tab.Preview.width + PreviewPad * 2,
                                   tab.Preview.height + PreviewPad * 2);

            _scrollPreview = GUI.BeginScrollView(inner, _scrollPreview, content);
            var at = new Rect(PreviewPad, PreviewPad, tab.Preview.width, tab.Preview.height);
            GUI.DrawTexture(at, tab.Preview, ScaleMode.StretchToFill, alphaBlend: true);

            if (_showPivot && tab.HasPivot)
                DrawPivotCross(tab, at);
            DrawGuides(tab, at);

            GUI.EndScrollView();
            GUI.EndGroup();
        }

        /// <summary>
        /// A kit's annotation lines — placement-time facts about the cell (the shrub kit's snow
        /// surface) drawn OVER the pixels, never into them: the pixels stay bit-identical to the
        /// bake at every parameter point, which is the studio's whole promise.
        /// </summary>
        void DrawGuides(Tab tab, Rect fit)
        {
            if (tab.Guides == null) return;

            var line = new Color(0.55f, 0.75f, 1f, 0.9f);
            foreach (var guide in tab.Guides)
            {
                if (guide == null) continue;
                float gy = fit.y + fit.height * (float)(guide.Y / tab.Preview.height);
                EditorGUI.DrawRect(new Rect(fit.x, gy - 0.5f, fit.width, 1), line);

                if (!string.IsNullOrEmpty(guide.Label))
                    GUI.Label(new Rect(fit.x + 2, gy - 16, fit.width - 4, 14), guide.Label,
                              EditorStyles.miniLabel);
            }
        }

        void DrawPivotCross(Tab tab, Rect fit)
        {
            // The pivot is the thing's GROUND POINT — where it plants when placed. Showing it is
            // the difference between "looks about right" and knowing where it will actually stand.
            float px = fit.x + fit.width * (float)(tab.PivotX / tab.Preview.width);
            float py = fit.y + fit.height * (float)(tab.PivotY / tab.Preview.height);

            var c = new Color(1f, 0.4f, 0.3f, 0.9f);
            EditorGUI.DrawRect(new Rect(px - 8, py - 0.5f, 16, 1), c);
            EditorGUI.DrawRect(new Rect(px - 0.5f, py - 8, 1, 16), c);
        }

        /// <summary>
        /// The ONLY place a preview is rendered — on a dirty flag set by a parameter change (or first
        /// show), never per frame. One adapter call, one texture upload, then quiet until the owner
        /// moves a dial again.
        ///
        /// <para>Gated to the LAYOUT pass: a refresh can change which controls exist (caption, error
        /// box), and IMGUI throws if the control list shifts between a Layout pass and the event pass
        /// it measured. On Layout, every pass that follows sees the same state.</para>
        /// </summary>
        void RefreshPreviewIfDirty(Tab tab)
        {
            if (!tab.Dirty || Event.current.type != EventType.Layout) return;
            tab.Dirty = false;

            try
            {
                RigStudioPreview preview = tab.Adapter.Preview(tab.Point);
                tab.Error = null;
                tab.Caption = preview.Caption;
                tab.HasPivot = preview.HasPivot;
                tab.PivotX = preview.PivotX;
                tab.PivotY = preview.PivotY;
                tab.Guides = preview.Guides;

                if (tab.Preview != null) DestroyImmediate(tab.Preview);
                tab.Preview = ToTexture(preview);
            }
            catch (Exception e)
            {
                tab.Error = e.Message;
                tab.Caption = null;
            }
        }

        /// <summary>Top-left-origin RGBA rows (the rigs' convention) into a bottom-origin Unity
        /// texture, POINT-filtered — pixel art is never smoothed here.</summary>
        static Texture2D ToTexture(RigStudioPreview preview)
        {
            var tex = new Texture2D(preview.Width, preview.Height, TextureFormat.RGBA32,
                                    mipChain: false, linear: false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var px = new Color32[preview.Width * preview.Height];
            for (int y = 0; y < preview.Height; y++)
            {
                int src = y * preview.Width * 4;
                int dst = (preview.Height - 1 - y) * preview.Width;
                for (int x = 0; x < preview.Width; x++)
                {
                    int s = src + x * 4;
                    px[dst + x] = new Color32(preview.Rgba[s], preview.Rgba[s + 1],
                                              preview.Rgba[s + 2], preview.Rgba[s + 3]);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        // ---- bake -----------------------------------------------------------------------------

        void DrawBakeRow(Tab tab)
        {
            EditorGUILayout.Space(4);

            if (!string.IsNullOrEmpty(tab.Schema.BakeNote))
                EditorGUILayout.HelpBox(tab.Schema.BakeNote, MessageType.None);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(tab.Error != null || tab.Preview == null))
            {
                if (GUILayout.Button("Bake this", GUILayout.Height(26), GUILayout.Width(160)))
                {
                    RunBake(tab);
                    GUIUtility.ExitGUI();   // the report box changes the control list — end this
                                            // pass cleanly rather than let IMGUI measure a mismatch
                }
            }
            GUILayout.Label("lands in the working tree like a menu bake — nothing is committed",
                            EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (tab.ReportText != null)
            {
                _scrollReport = EditorGUILayout.BeginScrollView(_scrollReport,
                                                                GUILayout.MaxHeight(140));
                EditorGUILayout.HelpBox(tab.ReportText,
                                        tab.ReportIsError ? MessageType.Error : MessageType.Info);
                EditorGUILayout.EndScrollView();
            }
        }

        void RunBake(Tab tab)
        {
            tab.ReportText = null;
            tab.ReportIsError = false;

            using var capture = new ErrorCapture();
            try
            {
                EditorUtility.DisplayProgressBar("Rig Studio",
                    $"Baking {tab.Title} — the kit's own baker is running…", 0.4f);

                RigStudioBakeReport report = tab.Adapter.Bake(tab.Point);
                tab.ReportText = FormatReport(report);
                Debug.Log($"[rig-studio] {tab.Title}: bake complete.\n{tab.ReportText}");
            }
            catch (Exception e)
            {
                // The kit's refusal, verbatim — plus anything its chain logged as an error while we
                // were inside the call (slicer refusals report through Debug.LogError), so the owner
                // reads the reason in this window rather than hunting the Console.
                tab.ReportText = capture.Lines.Length == 0
                    ? e.Message
                    : $"{e.Message}\n\n{string.Join("\n", capture.Lines)}";
                tab.ReportIsError = true;
                Debug.LogError($"[rig-studio] {tab.Title}: bake FAILED — {e}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static string FormatReport(RigStudioBakeReport report)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(report.PlaceableNote)) lines.Add(report.PlaceableNote);
            if (report.Minted is { Length: > 0 })
            {
                lines.Add("");
                lines.Add("Minted (working tree — review and commit when happy):");
                lines.AddRange(report.Minted.Select(p => "  " + p));
            }
            if (!string.IsNullOrEmpty(report.Summary))
            {
                lines.Add("");
                lines.Add(report.Summary.TrimEnd());
            }
            return string.Join("\n", lines);
        }

        /// <summary>Collects everything logged as an Error/Exception while in scope — how a kit
        /// chain's own <c>Debug.LogError</c> refusals reach the owner's eyes verbatim instead of
        /// scrolling past in the Console.</summary>
        sealed class ErrorCapture : IDisposable
        {
            readonly List<string> _lines = new List<string>();

            public ErrorCapture() { Application.logMessageReceived += OnLog; }
            public void Dispose() { Application.logMessageReceived -= OnLog; }

            void OnLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception) _lines.Add(condition);
            }

            public string[] Lines => _lines.ToArray();
        }

        static Rect FitRect(int w, int h, Rect area, float pad)
        {
            float aw = Mathf.Max(1, area.width - pad * 2);
            float ah = Mathf.Max(1, area.height - pad * 2);

            // Scale to FIT, but never scale UP past 1:1 by a fractional amount — pixel art scaled
            // by 1.37 shimmers. Upscale only in whole multiples; downscale continuously.
            float scale = Mathf.Min(aw / w, ah / h);
            if (scale > 1f) scale = Mathf.Max(1f, Mathf.Floor(scale));

            float fw = w * scale, fh = h * scale;
            return new Rect(area.x + (area.width - fw) * 0.5f,
                            area.y + (area.height - fh) * 0.5f, fw, fh);
        }
    }
}
#endif
