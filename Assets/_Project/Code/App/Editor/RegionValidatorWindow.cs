#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.Player;        // PlayerWalkController — the on-foot spawn the dryness check samples
using HiddenHarbours.Boats;         // BoatController — moored boats (low-water grounding heads-up)
using HiddenHarbours.Environment;   // EnvironmentService — the live tide profile cross-check
using HiddenHarbours.Art;           // WaterSurface — the sim-driven water plane (ADR 0010/0012)

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Region Validator</b> (owner level-design toolkit, Phase 1) — one click answers "is the
    /// region scene I'm looking at LEGAL and playable?" in plain English, so the owner can design and
    /// sanity-check regions himself without reading code. Menu: <b>Hidden Harbours ▸ Validate Region</b>.
    ///
    /// <para><b>Strictly READ-ONLY.</b> The tool inspects the open scene(s) and the Data assets and
    /// reports PASS / WARN / FAIL per check — it never modifies, saves, or dirties anything.
    /// <c>SerializedObject</c> is used only to READ private fields (never Apply); no scene is marked
    /// dirty, no asset written.</para>
    ///
    /// <para><b>What it checks</b> (each derived from how the converged St Peters/Greywick region model
    /// actually wires — the builders are the reference):
    /// region data (a <see cref="RegionDef"/> naming this scene, registered in Build Settings) ·
    /// the tidal seabed (an <see cref="ITidalTerrain"/> present — else walkability/boat-grounding are
    /// dead while the water still LOOKS tidal, the #150 gate; painted maps CPU-readable) ·
    /// the water surface (present, the Water shader, plane covers the play area, the seabed bake rect
    /// and elevation range cover/bracket the terrain, terrain-before-Sea root order — the known
    /// toggle-return bake quirk) ·
    /// high-water safety (town land zones / the island stay DRY at the region's spring high water;
    /// spawn, NPCs and the disembark point stay dry; the boat arrival stays WET; tide-gated features
    /// sit inside the swing — all via the sim's own <see cref="TidalExposure"/> rule, so the report
    /// can't disagree with gameplay) ·
    /// travel (passages wired to a target + loader, target scene in Build Settings, a matching
    /// <see cref="RegionAnchor"/>) ·
    /// scene hygiene (missing scripts, empty sprites) · sorting (mesh-vs-sprite draw order).</para>
    ///
    /// <para>The tide swing is read from the region's own authored profile WIDENED by the start
    /// region's (the live tide is the start scene's until per-region re-pointing lands — the
    /// GreywickBuilder caveat). No thresholds are invented: wet/dry is the sim's rule over authored
    /// values; the few layout tolerances are named editor-slack constants below.</para>
    /// </summary>
    public sealed class RegionValidatorWindow : EditorWindow
    {
        // ---- editor-tool slack (layout/report tuning only — NOT gameplay values; the wet/dry maths
        // ---- uses the sim's own rule with no threshold at all) ------------------------------------
        /// <summary>Slack (m) allowed when testing "rect A covers rect B" — forgives sub-tile nudges.</summary>
        private const float CoverageToleranceMeters = 0.5f;
        /// <summary>Grid density for sampling a terrain's min/max elevation under the water bake rect.</summary>
        private const int ElevationSampleGrid = 48;
        /// <summary>A tiled/stretched sprite covering at least this fraction of the water plane while
        /// sorting AT/ABOVE it is probably hiding the sea (the retired-overlay class of bug) — heuristic,
        /// report-only.</summary>
        private const float SpriteOverWaterAreaFraction = 0.25f;
        /// <summary>Cap per-object findings (missing scripts, empty sprites) so one broken import
        /// doesn't flood the report.</summary>
        private const int MaxListedOffenders = 20;

        private enum Verdict { Pass, Warn, Fail, Skip }

        private sealed class Finding
        {
            public Verdict Verdict;
            public string Section;
            public string Title;
            public string Detail;
            public Object Ping;
        }

        private readonly List<Finding> _findings = new List<Finding>();
        private string _headline = "";
        private string _subline = "";
        private string _validatedAt = "";
        private Vector2 _scroll;

        [MenuItem("Hidden Harbours/Validate Region", priority = 45)]
        public static void Open()
        {
            var win = GetWindow<RegionValidatorWindow>("Region Validator");
            win.minSize = new Vector2(520f, 420f);
            win.Show();
            win.RunAllChecks();
        }

        private void OnEnable()
        {
            if (_findings.Count == 0) RunAllChecks();
        }

        // =====================================================================================
        //  UI
        // =====================================================================================

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Validate now", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                    RunAllChecks();
                if (GUILayout.Button("Copy report", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                    GUIUtility.systemCopyBuffer = BuildTextReport();
                GUILayout.FlexibleSpace();
                GUILayout.Label(_validatedAt, EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(_headline, HeadlineStyle());
            if (!string.IsNullOrEmpty(_subline))
                EditorGUILayout.LabelField(_subline, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(
                "Read-only: this tool never changes your scene or assets. Re-run after you edit.",
                EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(2);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            string section = null;
            foreach (var f in _findings)
            {
                if (f.Section != section)
                {
                    section = f.Section;
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField(section, EditorStyles.boldLabel);
                }
                DrawFinding(f);
            }
            EditorGUILayout.EndScrollView();
        }

        private GUIStyle HeadlineStyle()
        {
            var s = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, wordWrap = true };
            int fails = _findings.Count(x => x.Verdict == Verdict.Fail);
            int warns = _findings.Count(x => x.Verdict == Verdict.Warn);
            s.normal.textColor = fails > 0 ? FailColor : warns > 0 ? WarnColor : PassColor;
            return s;
        }

        private static readonly Color PassColor = new Color(0.42f, 0.80f, 0.45f);
        private static readonly Color WarnColor = new Color(0.95f, 0.72f, 0.25f);
        private static readonly Color FailColor = new Color(0.96f, 0.42f, 0.38f);
        private static readonly Color SkipColor = new Color(0.62f, 0.62f, 0.62f);

        private void DrawFinding(Finding f)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var tag = new GUIStyle(EditorStyles.miniBoldLabel);
                tag.normal.textColor = f.Verdict switch
                {
                    Verdict.Pass => PassColor,
                    Verdict.Warn => WarnColor,
                    Verdict.Fail => FailColor,
                    _ => SkipColor,
                };
                GUILayout.Label(f.Verdict switch
                {
                    Verdict.Pass => "PASS",
                    Verdict.Warn => "WARN",
                    Verdict.Fail => "FAIL",
                    _ => "SKIP",
                }, tag, GUILayout.Width(38f));

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(f.Title, EditorStyles.wordWrappedLabel);
                    if (!string.IsNullOrEmpty(f.Detail))
                        EditorGUILayout.LabelField(f.Detail, EditorStyles.wordWrappedMiniLabel);
                }

                if (f.Ping != null && GUILayout.Button("Show me", GUILayout.Width(70f)))
                {
                    EditorGUIUtility.PingObject(f.Ping);
                    Selection.activeObject = f.Ping;
                }
            }
        }

        private string BuildTextReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine(_headline);
            sb.AppendLine(_subline);
            string section = null;
            foreach (var f in _findings)
            {
                if (f.Section != section) { section = f.Section; sb.AppendLine().AppendLine("## " + section); }
                sb.Append(f.Verdict.ToString().ToUpperInvariant()).Append(" — ").AppendLine(f.Title);
                if (!string.IsNullOrEmpty(f.Detail)) sb.Append("      ").AppendLine(f.Detail);
            }
            return sb.ToString();
        }

        // =====================================================================================
        //  The checklist
        // =====================================================================================

        private void RunAllChecks()
        {
            _findings.Clear();
            _validatedAt = "validated " + System.DateTime.Now.ToString("HH:mm:ss");

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                _headline = "Open a region scene to validate";
                _subline = "You are editing a prefab right now. Close the prefab stage, open a region " +
                           "scene (e.g. StPeters), then press Validate.";
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.name))
            {
                _headline = "No scene open";
                _subline = "Open a region scene (File ▸ Open Scene ▸ Assets/_Project/Scenes/…), then Validate.";
                return;
            }

            // ---- gather everything once (read-only) ------------------------------------------
            var allDefs = LoadAllRegionDefs();
            RegionDef def = CheckRegionData(scene, allDefs, out string buildWarnNote);

            var enabledBuildScenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => Path.GetFileNameWithoutExtension(s.path))
                .ToList();
            string startSceneName = enabledBuildScenes.FirstOrDefault();
            RegionDef startDef = allDefs.FirstOrDefault(d => d != null && d.SceneName == startSceneName);

            var terrains = FindInScene<MonoBehaviour>(scene).Where(m => m is ITidalTerrain).ToList();
            ITidalTerrain terrain = terrains.Count > 0 ? (ITidalTerrain)terrains[0] : null;
            var waters = FindInScene<WaterSurface>(scene);
            WaterSurface water = waters.FirstOrDefault();

            // The tide swing this scene really lives under: its own authored profile widened by the
            // start region's (the live tide is the start scene's until per-region re-pointing lands).
            bool haveSwing = def != null || startDef != null;
            RegionValidation.TideSwing swing = default;
            string swingSource = "";
            if (def != null)
            {
                swing = RegionValidation.SwingOf(def.TideMeanLevel, def.TideAmplitude);
                swingSource = $"this region's tide (mean {def.TideMeanLevel:0.##} m, amplitude {def.TideAmplitude:0.##} m)";
            }
            if (startDef != null && startDef != def)
            {
                var startSwing = RegionValidation.SwingOf(startDef.TideMeanLevel, startDef.TideAmplitude);
                if (def == null)
                {
                    swing = startSwing;
                    swingSource = $"the start region's tide ({startDef.DisplayName}) — this scene has no region data of its own";
                }
                else
                {
                    var widened = RegionValidation.WidestSwing(swing, startSwing);
                    if (widened.High > swing.High || widened.Low < swing.Low)
                        swingSource += $", widened by the start region's ({startDef.DisplayName}: ±{Mathf.Abs(startDef.TideAmplitude):0.##} m) — " +
                                       "the live tide is the start scene's until travel re-points it per region";
                    swing = widened;
                }
            }

            // ---- the sections ---------------------------------------------------------------
            CheckSceneInBuildSettings(scene, enabledBuildScenes, buildWarnNote);
            CheckTerrain(scene, terrains, terrain, water);
            CheckWaterSurface(scene, terrain, terrains, water, waters, haveSwing ? swing : default, haveSwing);
            CheckHighWaterSafety(scene, terrain, terrains, haveSwing, swing, swingSource);
            CheckTravel(scene, def);
            CheckLiveTideProfile(def);
            CheckHygiene();
            CheckSorting(scene, water);

            // ---- headline --------------------------------------------------------------------
            int pass = _findings.Count(f => f.Verdict == Verdict.Pass);
            int warn = _findings.Count(f => f.Verdict == Verdict.Warn);
            int fail = _findings.Count(f => f.Verdict == Verdict.Fail);
            int skip = _findings.Count(f => f.Verdict == Verdict.Skip);
            string regionName = def != null ? def.DisplayName : scene.name;
            string verdict = fail > 0 ? "NOT PLAYABLE YET — fix the failures below"
                           : warn > 0 ? "Playable, with warnings worth a look"
                           : "Legal and playable — all checks passed";
            _headline = $"{regionName} — {pass} passed · {warn} warning{(warn == 1 ? "" : "s")} · " +
                        $"{fail} failure{(fail == 1 ? "" : "s")}   ({verdict})";
            _subline = $"Scene '{scene.name}'" +
                       (SceneManager.sceneCount > 1 ? $" (+{SceneManager.sceneCount - 1} more loaded)" : "") +
                       (haveSwing ? $" · checked against high water {swing.High:+0.00;-0.00} m / low water {swing.Low:+0.00;-0.00} m from {swingSource}" : "") +
                       (skip > 0 ? $" · {skip} check{(skip == 1 ? "" : "s")} skipped (see below)" : "");
            Repaint();
        }

        // ---- helpers to record findings ------------------------------------------------------

        private void Add(Verdict v, string section, string title, string detail = null, Object ping = null)
            => _findings.Add(new Finding { Verdict = v, Section = section, Title = title, Detail = detail, Ping = ping });

        private const string SecRegion  = "Region data";
        private const string SecTide    = "Tide & seabed";
        private const string SecWater   = "Water surface";
        private const string SecDry     = "High-water safety (nothing important drowns)";
        private const string SecTravel  = "Travel";
        private const string SecHygiene = "Scene hygiene";
        private const string SecSorting = "Sorting & draw order";

        // =====================================================================================
        //  Region data
        // =====================================================================================

        private static List<RegionDef> LoadAllRegionDefs()
            => AssetDatabase.FindAssets("t:RegionDef")
                .Select(g => AssetDatabase.LoadAssetAtPath<RegionDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null)
                .ToList();

        private RegionDef CheckRegionData(Scene scene, List<RegionDef> allDefs, out string buildWarnNote)
        {
            buildWarnNote = null;
            var matches = allDefs.Where(d => d.SceneName == scene.name).ToList();
            if (matches.Count == 0)
            {
                Add(Verdict.Fail, SecRegion,
                    $"No region data points at this scene ('{scene.name}').",
                    "Every region needs a RegionDef asset (Data/Regions) whose Scene Name matches this " +
                    "scene — without one, travel can't reach it and it has no tide profile. Create one via " +
                    "Assets ▸ Create ▸ Hidden Harbours ▸ Region, or re-run the region's builder.");
                return null;
            }

            var def = matches[0];
            if (matches.Count > 1)
                Add(Verdict.Warn, SecRegion,
                    $"{matches.Count} region assets all claim scene '{scene.name}'.",
                    "Travel and tide checks will use the first one found (" + def.name + "). Give each " +
                    "region its own scene, or delete the duplicate.",
                    matches[1]);

            if (string.IsNullOrWhiteSpace(def.Id) || !def.Id.StartsWith("region."))
                Add(Verdict.Warn, SecRegion,
                    $"Region id '{def.Id}' doesn't follow the 'region.snake_case' convention.",
                    "Stable ids are how saves and travel refer to regions (data-model.md §5). Fix it " +
                    "BEFORE anything ships in a save — ids are append-only afterwards.",
                    def);
            else
                Add(Verdict.Pass, SecRegion,
                    $"Region data found: '{def.DisplayName}' (id {def.Id}).",
                    "Asset: " + AssetDatabase.GetAssetPath(def), def);

            return def;
        }

        private void CheckSceneInBuildSettings(Scene scene, List<string> enabledBuildScenes, string note)
        {
            if (enabledBuildScenes.Contains(scene.name))
                Add(Verdict.Pass, SecRegion, "Scene is registered in Build Settings.",
                    "The travel loader can only load scenes listed there.");
            else
                Add(Verdict.Fail, SecRegion, "Scene is NOT in Build Settings.",
                    "Travelling here will silently fail (the loader warns and stays put). The region " +
                    "builders register their scenes automatically — add it via File ▸ Build Profiles, or " +
                    "re-run this region's builder.");
            if (!string.IsNullOrEmpty(note))
                Add(Verdict.Warn, SecRegion, note);
        }

        // =====================================================================================
        //  Tide & seabed
        // =====================================================================================

        private void CheckTerrain(Scene scene, List<MonoBehaviour> terrains, ITidalTerrain terrain, WaterSurface water)
        {
            if (terrain == null)
            {
                if (water != null)
                    Add(Verdict.Fail, SecTide,
                        "The water LOOKS tidal but no seabed (tidal terrain) is in the scene.",
                        "Without a TidalTerrain / RectTidalTerrain / PaintedTidalTerrain, on-foot " +
                        "walkability and boat grounding are switched OFF while the shader still draws " +
                        "moving water — what you see stops matching what you can walk/sail (the exact gap " +
                        "the Greywick/Cove convergence closed). Add one, or re-run this region's builder.",
                        water);
                else
                    Add(Verdict.Warn, SecTide,
                        "No tidal terrain and no water surface in this scene.",
                        "If this is meant to be a coastal region, it's missing its whole water model — " +
                        "compare with StPeters (Hidden Harbours ▸ Build St Peters Scene). If it's a " +
                        "deliberate inland/test scene, ignore this.");
                return;
            }

            var mb = (MonoBehaviour)terrain;
            Add(Verdict.Pass, SecTide,
                $"Tidal seabed present: {mb.GetType().Name} on '{mb.gameObject.name}'.",
                "It registers itself when the scene runs, so the tide gates walkability, clams and boat " +
                "grounding here.", mb);

            if (terrains.Count > 1)
                Add(Verdict.Warn, SecTide,
                    $"{terrains.Count} tidal terrains in one scene — only ONE can be live.",
                    "They register last-writer-wins, so which one the game uses depends on activation " +
                    "order. A region carries exactly one seabed; remove the extras.",
                    terrains[1]);

            if (terrain is PaintedTidalTerrain painted)
            {
                var map = painted.Map;
                if (map == null)
                    Add(Verdict.Fail, SecTide,
                        "The painted seabed has NO height-map asset assigned.",
                        "It will report open water everywhere (nothing walkable, nothing bares). Assign " +
                        "the region's PaintedHeightMap, or repaint via Hidden Harbours ▸ Tools ▸ Terrain " +
                        "Paint Tool and Adopt.", (Object)painted);
                else if (map.HeightTexture == null)
                    Add(Verdict.Fail, SecTide,
                        $"The painted height map '{map.name}' has no texture.",
                        "Repaint and save via the Terrain Paint Tool.", map);
                else if (!map.HeightTexture.isReadable)
                    Add(Verdict.Fail, SecTide,
                        $"The painted height texture '{map.HeightTexture.name}' is not CPU-readable.",
                        "The sim can't sample it, so the region plays as open water while the shader " +
                        "still SHOWS your painted coast — painted ≠ sailed. Re-save it via the Terrain " +
                        "Paint Tool (which sets Read/Write on).", map.HeightTexture);
                else
                    Add(Verdict.Pass, SecTide,
                        $"Painted height map '{map.name}' is readable — render and sim share the same bytes.",
                        null, map);
            }
        }

        // =====================================================================================
        //  Water surface
        // =====================================================================================

        private void CheckWaterSurface(Scene scene, ITidalTerrain terrain, List<MonoBehaviour> terrains,
                                       WaterSurface water, List<WaterSurface> waters,
                                       RegionValidation.TideSwing swing, bool haveSwing)
        {
            if (water == null)
            {
                if (terrain != null)
                    Add(Verdict.Fail, SecWater,
                        "A tidal seabed exists but there is NO water surface to show it.",
                        "The tide would run invisibly (the 'frozen tide' class of report). Add a Sea " +
                        "plane with the Water material + a WaterSurface component — or re-run this " +
                        "region's builder, which wires it.", (Object)(terrains.FirstOrDefault()));
                else
                    Add(Verdict.Skip, SecWater, "No water surface — water checks skipped.",
                        "See the Tide & seabed section above.");
                return;
            }

            if (waters.Count > 1)
                Add(Verdict.Warn, SecWater,
                    $"{waters.Count} WaterSurface components in this scene.",
                    "One Sea plane per region is the pattern; extras fight over the shared shader globals.",
                    waters[1]);

            // The Water shader (else the plane is a plain backdrop — the pre-import fallback).
            var renderer = water.GetComponent<Renderer>();
            var mat = renderer != null ? renderer.sharedMaterial : null;
            if (mat == null || mat.shader == null || mat.shader.name != "HiddenHarbours/Water")
                Add(Verdict.Warn, SecWater,
                    "The Sea isn't using the layered Water shader.",
                    $"Material: {(mat != null ? mat.name : "none")} — the sea will look flat/static. " +
                    "Usually means the builder ran before Water.mat imported; re-run the region's builder.",
                    water);
            else
                Add(Verdict.Pass, SecWater, "The Sea runs the layered sim-driven Water shader.", null, water);

            // Read the bake settings (read-only) straight off the component.
            var so = new SerializedObject(water);
            SerializedProperty pBake = so.FindProperty("_bakeHeightMap");
            SerializedProperty pSource = so.FindProperty("_depthSource");
            SerializedProperty pCenter = so.FindProperty("_heightWorldCenter");
            SerializedProperty pSize = so.FindProperty("_heightWorldSize");
            SerializedProperty pMin = so.FindProperty("_heightMin");
            SerializedProperty pMax = so.FindProperty("_heightMax");
            SerializedProperty pPaintedTex = so.FindProperty("_paintedHeightTex");
            if (pBake == null || pSource == null || pCenter == null || pSize == null)
            {
                Add(Verdict.Skip, SecWater, "Couldn't read the WaterSurface bake settings.",
                    "The component's fields have changed since this tool was written — flag tools-editor.");
                return;
            }

            // Coverage: the water plane over the play area, and the seabed bake rect over the plane.
            Bounds planeBounds = renderer != null ? renderer.bounds : new Bounds(water.transform.position, Vector3.zero);
            var planeCenter = new Vector2(planeBounds.center.x, planeBounds.center.y);
            var planeSize = new Vector2(planeBounds.size.x, planeBounds.size.y);

            if (TryComputePlayBounds(scene, terrain, out Vector2 playCenter, out Vector2 playSize, out string playDesc))
            {
                if (RegionValidation.RectCovers(planeCenter, planeSize, playCenter, playSize, CoverageToleranceMeters))
                    Add(Verdict.Pass, SecWater,
                        "The water plane covers the whole play area.",
                        $"Play area {playDesc}; sea plane {planeSize.x:0.#}×{planeSize.y:0.#} m around ({planeCenter.x:0.#}, {planeCenter.y:0.#}).");
                else
                    Add(Verdict.Warn, SecWater,
                        "The water plane does NOT cover the whole play area — there's an un-watered gap at the edges.",
                        $"Play area {playDesc}, but the sea plane is {planeSize.x:0.#}×{planeSize.y:0.#} m around " +
                        $"({planeCenter.x:0.#}, {planeCenter.y:0.#}). Land/points outside the plane sit on bare " +
                        "background. Enlarge the Sea sprite's size (or move the outlying content in).",
                        water);
            }
            else
            {
                Add(Verdict.Skip, SecWater, "Play-area coverage skipped — nothing found to measure the play area from.",
                    "No terrain features or gameplay points to bound. " + playDesc);
            }

            bool bakes = pBake.boolValue;
            var source = (WaterSurface.DepthSource)pSource.intValue;
            Vector2 bakeCenter = pCenter.vector2Value;
            Vector2 bakeSize = pSize.vector2Value;

            if (!bakes)
            {
                Add(Verdict.Warn, SecWater,
                    "The Sea's seabed height-map bake is OFF.",
                    "The shader falls back to uniform deep water: no shoreline, no shallows, no wet/dry " +
                    "reveal. Tick 'Bake Height Map' on the WaterSurface (the builders leave it on).",
                    water);
            }
            else
            {
                if (RegionValidation.RectCovers(bakeCenter, bakeSize, planeCenter, planeSize, CoverageToleranceMeters))
                    Add(Verdict.Pass, SecWater, "The seabed bake rectangle covers the whole sea plane.");
                else
                    Add(Verdict.Warn, SecWater,
                        "The seabed bake rectangle is smaller than the sea plane.",
                        $"Bake rect {bakeSize.x:0.#}×{bakeSize.y:0.#} m around ({bakeCenter.x:0.#}, {bakeCenter.y:0.#}) vs " +
                        $"plane {planeSize.x:0.#}×{planeSize.y:0.#} m. Outside the rect the shader re-reads its edge " +
                        "pixels, so the shoreline/depth smears at the borders. Match the bake rect to the plane " +
                        "(the builders set them equal).", water);

                // The baked elevation range must bracket the authored seabed, or depth/shoreline clamp wrong.
                if (terrain != null && source != WaterSurface.DepthSource.PaintedHeightMap && pMin != null && pMax != null)
                {
                    if (RegionValidation.SampleElevationRange(terrain, bakeCenter, bakeSize, ElevationSampleGrid,
                                                              out float tMin, out float tMax))
                    {
                        float hMin = pMin.floatValue, hMax = pMax.floatValue;
                        if (tMin < hMin - 0.01f || tMax > hMax + 0.01f)
                            Add(Verdict.Warn, SecWater,
                                "The Sea's height range doesn't bracket the authored seabed — depths will clamp.",
                                $"Terrain spans {tMin:0.##}…{tMax:0.##} m but the bake maps {hMin:0.##}…{hMax:0.##} m. " +
                                "Set Height Min below the deepest seabed and Height Max above the highest land " +
                                "on the WaterSurface.", water);
                        else
                            Add(Verdict.Pass, SecWater,
                                $"The Sea's height range ({hMin:0.##}…{hMax:0.##} m) brackets the seabed ({tMin:0.##}…{tMax:0.##} m).");
                    }
                }

                // Painted mode: render and sim must read the SAME map (the ADR 0014 invariant).
                if (terrain is PaintedTidalTerrain painted && painted.Map != null &&
                    (source == WaterSurface.DepthSource.PaintedHeightMap || source == WaterSurface.DepthSource.Auto))
                {
                    var map = painted.Map;
                    var wsTex = pPaintedTex != null ? pPaintedTex.objectReferenceValue as Texture2D : null;
                    if (source == WaterSurface.DepthSource.PaintedHeightMap && wsTex != map.HeightTexture)
                        Add(Verdict.Warn, SecWater,
                            "The Sea's painted texture is NOT the same one the sim samples.",
                            "Render and gameplay would read different coasts (painted ≠ sailed). Re-run the " +
                            "Terrain Paint Tool's Adopt step, which wires both from one asset.", water);
                    else if (source == WaterSurface.DepthSource.PaintedHeightMap &&
                             (pMin == null || pMax == null ||
                              !Mathf.Approximately(pMin.floatValue, map.MinElevation) ||
                              !Mathf.Approximately(pMax.floatValue, map.MaxElevation) ||
                              (bakeCenter - map.WorldCenter).magnitude > CoverageToleranceMeters ||
                              (bakeSize - map.WorldSize).magnitude > CoverageToleranceMeters))
                        Add(Verdict.Warn, SecWater,
                            "The Sea's painted rect/elevation range drifted from the painted map asset.",
                            $"Map: {map.WorldSize.x:0.#}×{map.WorldSize.y:0.#} m around ({map.WorldCenter.x:0.#}, " +
                            $"{map.WorldCenter.y:0.#}), {map.MinElevation:0.##}…{map.MaxElevation:0.##} m. Re-run " +
                            "the paint tool's Adopt step so the shader reads the same frame as the sim.", water);
                    else
                        Add(Verdict.Pass, SecWater, "Painted mode: the Sea and the sim read the same painted map.");
                }
            }

            // Creation-order sanity (the known toggle-return quirk): the terrain root should sit ABOVE
            // the Sea root so, when a visited region's scene is toggled back on, the terrain registers
            // BEFORE the Sea re-bakes its seabed (scene roots activate in order — the Greywick builder
            // creates them terrain-first for exactly this reason).
            if (terrain != null && bakes &&
                (source == WaterSurface.DepthSource.Auto || source == WaterSurface.DepthSource.TidalTerrain))
            {
                var terrainRoot = ((MonoBehaviour)terrain).transform.root;
                var waterRoot = water.transform.root;
                if (terrainRoot != waterRoot && terrainRoot.gameObject.scene == waterRoot.gameObject.scene)
                {
                    if (terrainRoot.GetSiblingIndex() < waterRoot.GetSiblingIndex())
                        Add(Verdict.Pass, SecWater,
                            "Seabed-before-Sea order is correct in the Hierarchy.",
                            "On a return visit the terrain registers before the Sea re-bakes.");
                    else
                        Add(Verdict.Warn, SecWater,
                            "The Sea sits ABOVE the seabed in the Hierarchy — a return visit can bake the wrong coast.",
                            $"When this region is toggled back on, '{waterRoot.name}' wakes before " +
                            $"'{terrainRoot.name}', so the Sea bakes its seabed while no terrain is registered " +
                            "yet (falls back to a guessed coast). Often fine on first load; risky on travel. " +
                            "Fix: drag the seabed object above the Sea in the Hierarchy (and tell the builder's " +
                            "owner if a builder made this scene).", waterRoot.gameObject);
                }
            }
        }

        /// <summary>The region's play bounds, computed from what's actually authored: the terrain's land
        /// features plus every gameplay point (spawn, NPCs, anchors, passages, boats).</summary>
        private bool TryComputePlayBounds(Scene scene, ITidalTerrain terrain,
                                          out Vector2 center, out Vector2 size, out string desc)
        {
            bool any = false;
            Vector2 min = Vector2.zero, max = Vector2.zero;

            void Include(Vector2 lo, Vector2 hi)
            {
                if (!any) { min = lo; max = hi; any = true; return; }
                min = Vector2.Min(min, lo);
                max = Vector2.Max(max, hi);
            }
            void IncludePoint(Vector3 p) => Include(new Vector2(p.x, p.y), new Vector2(p.x, p.y));

            // Terrain land features (read-only SerializedObject over the authored zones).
            if (terrain is TidalTerrain analytic)
            {
                var so = new SerializedObject(analytic);
                var c = so.FindProperty("_islandCenter"); var r = so.FindProperty("_islandRadius");
                var f = so.FindProperty("_islandFalloff");
                var a = so.FindProperty("_sandbarFrom"); var b = so.FindProperty("_sandbarTo");
                var hw = so.FindProperty("_sandbarHalfWidth");
                if (c != null && r != null && f != null)
                {
                    float reach = r.floatValue + f.floatValue;
                    Include(c.vector2Value - Vector2.one * reach, c.vector2Value + Vector2.one * reach);
                }
                if (a != null && b != null && hw != null)
                {
                    Vector2 lo = Vector2.Min(a.vector2Value, b.vector2Value) - Vector2.one * hw.floatValue;
                    Vector2 hi = Vector2.Max(a.vector2Value, b.vector2Value) + Vector2.one * hw.floatValue;
                    Include(lo, hi);
                }
            }
            else if (terrain is RectTidalTerrain rect)
            {
                var so = new SerializedObject(rect);
                var zones = so.FindProperty("_zones");
                if (zones != null)
                    for (int i = 0; i < zones.arraySize; i++)
                    {
                        var z = zones.GetArrayElementAtIndex(i);
                        Vector2 zc = z.FindPropertyRelative("Center").vector2Value;
                        Vector2 zh = z.FindPropertyRelative("HalfSize").vector2Value;
                        float zf = z.FindPropertyRelative("Falloff").floatValue;
                        Include(zc - zh - Vector2.one * zf, zc + zh + Vector2.one * zf);
                    }
            }
            else if (terrain is PaintedTidalTerrain painted && painted.Map != null)
            {
                Include(painted.Map.WorldCenter - painted.Map.WorldSize * 0.5f,
                        painted.Map.WorldCenter + painted.Map.WorldSize * 0.5f);
            }

            // Gameplay points.
            foreach (var p in FindInScene<PlayerWalkController>(scene)) IncludePoint(p.transform.position);
            foreach (var i in FindInScene<Interactable>(scene)) IncludePoint(i.transform.position);
            foreach (var g in FindInScene<RegionPassage>(scene)) IncludePoint(g.transform.position);
            foreach (var b in FindInScene<BoatController>(scene)) IncludePoint(b.transform.position);
            foreach (var anchor in FindInScene<RegionAnchor>(scene))
            {
                IncludePoint(anchor.ArrivalPoint.position);
                if (anchor.DockZone != null) IncludePoint(anchor.DockZone.position);
                if (anchor.DisembarkPoint != null) IncludePoint(anchor.DisembarkPoint.position);
            }

            center = (min + max) * 0.5f;
            size = max - min;
            desc = any
                ? $"{size.x:0.#}×{size.y:0.#} m around ({center.x:0.#}, {center.y:0.#}) — from the authored land features and gameplay points"
                : "(no terrain features or gameplay points found)";
            return any;
        }

        // =====================================================================================
        //  High-water safety — the sim's own exposure rule over the authored swing
        // =====================================================================================

        private void CheckHighWaterSafety(Scene scene, ITidalTerrain terrain, List<MonoBehaviour> terrains,
                                          bool haveSwing, RegionValidation.TideSwing swing, string swingSource)
        {
            if (terrain == null)
            {
                Add(Verdict.Skip, SecDry, "No tidal seabed — flood checks skipped.",
                    "With no terrain nothing can flood (the tide gate is off in this scene).");
                return;
            }
            if (!haveSwing)
            {
                Add(Verdict.Skip, SecDry, "No region tide data — flood checks skipped.",
                    "Fix the Region data section first, then re-validate.");
                return;
            }

            var terrainObj = (Object)(MonoBehaviour)terrain;

            // --- authored land features: town land must clear spring high water ------------------
            if (terrain is RectTidalTerrain rect)
            {
                var so = new SerializedObject(rect);
                var zones = so.FindProperty("_zones");
                int flooded = 0;
                if (zones != null)
                {
                    for (int i = 0; i < zones.arraySize; i++)
                    {
                        var z = zones.GetArrayElementAtIndex(i);
                        Vector2 zc = z.FindPropertyRelative("Center").vector2Value;
                        float ze = z.FindPropertyRelative("Elevation").floatValue;
                        if (!TidalExposure.IsExposed(swing.High, ze))
                        {
                            flooded++;
                            Add(Verdict.Fail, SecDry,
                                $"Town land zone {i + 1} (around {zc.x:0.#}, {zc.y:0.#}) FLOODS at high water.",
                                $"Its flat top sits at {ze:0.##} m but spring high water reaches {swing.High:0.##} m " +
                                $"({swingSource}). Streets, stalls and walkers there go underwater twice a day. " +
                                "Raise the zone's Elevation above high water — or lower the region's tide amplitude.",
                                rect);
                        }
                    }
                    if (zones.arraySize > 0 && flooded == 0)
                        Add(Verdict.Pass, SecDry,
                            $"All {zones.arraySize} town land zone{(zones.arraySize == 1 ? "" : "s")} stay dry at spring high water ({swing.High:0.##} m).",
                            null, rect);
                }
            }
            else if (terrain is TidalTerrain analytic)
            {
                var so = new SerializedObject(analytic);
                var pIsland = so.FindProperty("_islandElevation");
                var pCrest = so.FindProperty("_sandbarCrestElevation");
                if (pIsland != null)
                {
                    if (TidalExposure.IsExposed(swing.High, pIsland.floatValue))
                        Add(Verdict.Pass, SecDry,
                            $"The island (home ground, {pIsland.floatValue:0.##} m) stays dry at spring high water ({swing.High:0.##} m).",
                            null, analytic);
                    else
                        Add(Verdict.Fail, SecDry,
                            "The ISLAND floods at high water.",
                            $"Island plateau {pIsland.floatValue:0.##} m vs spring high water {swing.High:0.##} m " +
                            $"({swingSource}) — the home ground, cottage and NPCs go under. Raise the island " +
                            "elevation above high water.", analytic);
                }
                if (pCrest != null)
                {
                    float crest = pCrest.floatValue;
                    if (RegionValidation.IsIntertidal(crest, swing))
                        Add(Verdict.Pass, SecDry,
                            $"The sandbar crest ({crest:0.##} m) sits inside the tide swing — it bares AND floods, as a tide gate should.");
                    else if (crest >= swing.High)
                        Add(Verdict.Warn, SecDry,
                            "The sandbar NEVER floods — the tide gate is dead.",
                            $"Crest {crest:0.##} m is at/above spring high water {swing.High:0.##} m, so the bar is a " +
                            "permanent causeway and the tide never cuts it. Lower the crest below high water if the " +
                            "crossing is meant to be tide-gated.", analytic);
                    else
                        Add(Verdict.Warn, SecDry,
                            "The sandbar NEVER bares — the walk path never opens.",
                            $"Crest {crest:0.##} m is at/below spring low water {swing.Low:0.##} m, so even the lowest " +
                            "tide leaves it underwater. Raise the crest above low water if walkers are meant to cross.",
                            analytic);
                }
            }
            else if (terrain is PaintedTidalTerrain)
            {
                Add(Verdict.Skip, SecDry,
                    "Painted seabed: no named 'town land' features to test — the point checks below cover it.",
                    "A painted map has no labelled zones, so this tool checks the actual gameplay points " +
                    "(spawn, NPCs, disembark) against the painted heights instead.");
            }

            // --- gameplay points that must stay DRY at spring high water -------------------------
            foreach (var p in FindInScene<PlayerWalkController>(scene))
                CheckDryPoint(terrain, swing, p.transform.position, p.gameObject,
                    "The player spawn", "you'd start the game standing in the sea");
            foreach (var i in FindInScene<Interactable>(scene))
                CheckDryPoint(terrain, swing, i.transform.position, i.gameObject,
                    $"'{i.gameObject.name}' (NPC / interaction point)", "talking to them means wading at high tide");

            var anchors = FindInScene<RegionAnchor>(scene);
            foreach (var anchor in anchors)
            {
                if (anchor.DisembarkPoint != null)
                    CheckDryPoint(terrain, swing, anchor.DisembarkPoint.position, anchor.DisembarkPoint.gameObject,
                        "The disembark spot", "stepping off the boat would drop you into the water");

                // --- and the boat side: the arrival must be afloat -------------------------------
                Vector3 arr = anchor.ArrivalPoint.position;
                float depthHigh = RegionValidation.DepthAt(terrain, new Vector2(arr.x, arr.y), swing.High);
                float depthLow = RegionValidation.DepthAt(terrain, new Vector2(arr.x, arr.y), swing.Low);
                if (depthHigh <= 0f)
                    Add(Verdict.Fail, SecDry,
                        $"The boat ARRIVAL point ({arr.x:0.#}, {arr.y:0.#}) is on dry ground even at HIGH water.",
                        $"Ground there sits {-depthHigh:0.##} m above spring high water ({swing.High:0.##} m, " +
                        $"{swingSource}) — a boat sailing in is parked on land and can never float off. Move the " +
                        "arrival point into water deep enough to float a hull.",
                        anchor.ArrivalPoint.gameObject);
                else if (depthLow <= 0f)
                    Add(Verdict.Warn, SecDry,
                        $"The boat arrival dries out at LOW water (depth {depthHigh:0.##} m at high, dry at low).",
                        "Sailing in/out of this region is tide-gated: arrive at low tide and the boat grounds. " +
                        "Deliberate tide-gating is good P1 — just make sure it's on purpose (deep harbours " +
                        "shouldn't do this).", anchor.ArrivalPoint.gameObject);
                else
                    Add(Verdict.Pass, SecDry,
                        $"The boat arrival stays afloat across the whole tide (depth {depthLow:0.##}–{depthHigh:0.##} m).",
                        null, anchor.ArrivalPoint.gameObject);
            }

            // Moored boats: heads-up only (a dory taking the ground at low water can be intended).
            foreach (var boat in FindInScene<BoatController>(scene))
            {
                Vector3 bp = boat.transform.position;
                float depthHigh = RegionValidation.DepthAt(terrain, new Vector2(bp.x, bp.y), swing.High);
                if (depthHigh <= 0f)
                    Add(Verdict.Warn, SecDry,
                        $"The moored boat '{boat.gameObject.name}' sits on ground that is dry even at HIGH water.",
                        $"Ground {-depthHigh:0.##} m above spring high ({swing.High:0.##} m) — she can never float " +
                        "there. Moor her a little further out.", boat.gameObject);
            }
        }

        private void CheckDryPoint(ITidalTerrain terrain, RegionValidation.TideSwing swing,
                                   Vector3 pos, Object ping, string what, string consequence)
        {
            var p = new Vector2(pos.x, pos.y);
            if (RegionValidation.IsDryAt(terrain, p, swing.High))
                Add(Verdict.Pass, SecDry, $"{what} stays dry at spring high water.", null, ping);
            else
            {
                float depth = RegionValidation.DepthAt(terrain, p, swing.High);
                Add(Verdict.Fail, SecDry,
                    $"{what} at ({pos.x:0.#}, {pos.y:0.#}) FLOODS at high water — {consequence}.",
                    $"Water is {depth:0.##} m deep there at spring high ({swing.High:0.##} m). Move it onto " +
                    "higher ground, or raise the ground under it.", ping);
            }
        }

        // =====================================================================================
        //  Travel
        // =====================================================================================

        private void CheckTravel(Scene scene, RegionDef def)
        {
            var passages = FindInScene<RegionPassage>(scene);
            var enabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled)
                .Select(s => Path.GetFileNameWithoutExtension(s.path)).ToHashSet();

            if (passages.Count == 0)
                Add(Verdict.Warn, SecTravel,
                    "No travel passage in this scene — there is no way to LEAVE this region.",
                    "Regions connect through RegionPassage triggers (the sandbar end / the harbour mouth). " +
                    "Fine for a one-off test scene; a real region needs at least one way out.");

            foreach (var passage in passages)
            {
                var so = new SerializedObject(passage);
                var target = so.FindProperty("_target")?.objectReferenceValue as RegionDef;
                var loader = so.FindProperty("_loader")?.objectReferenceValue as RegionSceneLoader;
                string label = $"Passage '{passage.gameObject.name}'";

                if (target == null)
                {
                    Add(Verdict.Fail, SecTravel, $"{label} has NO destination region.",
                        "Walking/sailing into it does nothing (it logs a warning). Wire its Target to a " +
                        "RegionDef.", passage);
                    continue;
                }
                if (loader == null)
                {
                    Add(Verdict.Fail, SecTravel, $"{label} has no scene loader wired.",
                        "It knows where to go but not how. Wire its Loader to the RegionSceneLoader " +
                        "(the builders do this automatically).", passage);
                    continue;
                }
                if (!target.HasScene)
                {
                    Add(Verdict.Fail, SecTravel,
                        $"{label} points at '{target.DisplayName}', which names NO scene.",
                        "Set the region's Scene Name, or build its scene first.", target);
                    continue;
                }
                if (!enabledScenes.Contains(target.SceneName))
                {
                    Add(Verdict.Fail, SecTravel,
                        $"{label} leads to '{target.DisplayName}', but scene '{target.SceneName}' is not in Build Settings.",
                        "Travel will no-op with a warning. Run that region's builder (it registers the " +
                        "scene), or add the scene by hand.", passage);
                    continue;
                }

                var col = passage.GetComponent<Collider2D>();
                if (col == null || !col.isTrigger)
                    Add(Verdict.Warn, SecTravel,
                        $"{label} has no trigger collider — nothing can ever enter it.",
                        "Add a BoxCollider2D with 'Is Trigger' ticked across the passage band.", passage);
                else
                    Add(Verdict.Pass, SecTravel,
                        $"{label} → {target.DisplayName} (scene '{target.SceneName}') is fully wired.",
                        null, passage);

                // The loader's region list backs id-based travel; a passage works without it, but keep them aligned.
                var loaderSo = new SerializedObject(loader);
                var regions = loaderSo.FindProperty("_regions");
                bool listed = false;
                if (regions != null)
                    for (int i = 0; i < regions.arraySize && !listed; i++)
                        listed = regions.GetArrayElementAtIndex(i).objectReferenceValue == target;
                if (!listed)
                    Add(Verdict.Warn, SecTravel,
                        $"The scene loader doesn't LIST '{target.DisplayName}' in its regions.",
                        "This passage still works (it hands the region over directly), but anything that " +
                        "travels by region id won't know it. Add the region to the loader's list.", loader);
            }

            // The anchor: how the persistent player/boat binds here when SAILING IN.
            var anchors = FindInScene<RegionAnchor>(scene);
            if (anchors.Count == 0)
            {
                Add(Verdict.Warn, SecTravel,
                    "No RegionAnchor — sailing INTO this region has nowhere to place the player/boat.",
                    "Each region scene places one anchor naming its arrival point, dock zone and disembark " +
                    "spot. Without it, arriving by travel can't bind here (a start scene still plays fine " +
                    "standalone).");
            }
            else
            {
                var anchor = anchors[0];
                if (anchors.Count > 1)
                    Add(Verdict.Warn, SecTravel, $"{anchors.Count} RegionAnchors — the coordinator uses the first it finds.",
                        "Keep exactly one per region scene.", anchors[1]);

                if (def != null && anchor.RegionId != def.Id)
                    Add(Verdict.Fail, SecTravel,
                        $"The RegionAnchor's id '{anchor.RegionId}' doesn't match this region's id '{def.Id}'.",
                        "Arrivals match by id, so this anchor is silently never used — the classic typo. " +
                        "Fix the anchor's Region Id.", anchor);
                else
                    Add(Verdict.Pass, SecTravel,
                        $"RegionAnchor present{(def != null ? $" and matches id '{def.Id}'" : "")}.",
                        null, anchor);

                if (anchor.DockZone == null || anchor.DisembarkPoint == null)
                    Add(Verdict.Warn, SecTravel,
                        "The RegionAnchor is missing its dock zone and/or disembark point.",
                        "After sailing in you couldn't board/disembark here — wire both transforms on the anchor.",
                        anchor);
            }
        }

        /// <summary>If this scene carries the live tide service (start scenes do), its running profile
        /// should match the region's authored data — otherwise Play won't behave like the data says.</summary>
        private void CheckLiveTideProfile(RegionDef def)
        {
            if (def == null) return;
            var env = Object.FindObjectsByType<EnvironmentService>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                            .FirstOrDefault();
            if (env == null) return;   // the tide service arrives with the persistent core — nothing to check here

            var so = new SerializedObject(env);
            var profile = so.FindProperty("_activeTideProfile");
            if (profile == null) return;
            float mean = profile.FindPropertyRelative("MeanLevel")?.floatValue ?? 0f;
            float amp = profile.FindPropertyRelative("Amplitude")?.floatValue ?? 0f;
            if (!Mathf.Approximately(mean, def.TideMeanLevel) || !Mathf.Approximately(amp, def.TideAmplitude))
                Add(Verdict.Warn, SecRegion,
                    "The LIVE tide service in this scene runs a different tide than the region's data.",
                    $"Service: mean {mean:0.##} m, amplitude {amp:0.##} m — region data: mean " +
                    $"{def.TideMeanLevel:0.##} m, amplitude {def.TideAmplitude:0.##} m. In Play the water " +
                    "won't swing the way this region's data promises. Re-run the start-scene builder (it " +
                    "copies the region's tide onto the service), or align the numbers.", env);
            else
                Add(Verdict.Pass, SecRegion,
                    $"The live tide service matches the region's data (mean {mean:0.##} m, amplitude {amp:0.##} m).",
                    null, env);
        }

        // =====================================================================================
        //  Hygiene (all loaded scenes) & sorting
        // =====================================================================================

        private void CheckHygiene()
        {
            int missing = 0, emptySprites = 0;
            for (int si = 0; si < SceneManager.sceneCount; si++)
            {
                Scene s = SceneManager.GetSceneAt(si);
                if (!s.isLoaded) continue;
                foreach (var root in s.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (n > 0)
                    {
                        missing++;
                        if (missing <= MaxListedOffenders)
                            Add(Verdict.Fail, SecHygiene,
                                $"'{t.gameObject.name}' has {n} missing script{(n == 1 ? "" : "s")} (scene '{s.name}').",
                                "A component's script no longer exists (deleted/renamed/failed import) — " +
                                "whatever it did is silently off. Fix or remove the dead component.",
                                t.gameObject);
                    }

                    var sr = t.GetComponent<SpriteRenderer>();
                    if (sr != null && sr.enabled && sr.sprite == null)
                    {
                        emptySprites++;
                        if (emptySprites <= MaxListedOffenders)
                            Add(Verdict.Warn, SecHygiene,
                                $"'{t.gameObject.name}' has an enabled SpriteRenderer with NO sprite (scene '{s.name}').",
                                "It draws nothing — usually art that didn't import yet or a broken reference. " +
                                "Re-run the builder after the art imports, or assign the sprite.",
                                t.gameObject);
                    }
                }
            }
            if (missing > MaxListedOffenders)
                Add(Verdict.Fail, SecHygiene, $"…and {missing - MaxListedOffenders} more objects with missing scripts.");
            if (emptySprites > MaxListedOffenders)
                Add(Verdict.Warn, SecHygiene, $"…and {emptySprites - MaxListedOffenders} more empty sprite renderers.");
            if (missing == 0)
                Add(Verdict.Pass, SecHygiene, "No missing scripts in any loaded scene.");
            if (emptySprites == 0)
                Add(Verdict.Pass, SecHygiene, "No empty sprite renderers.");

            Add(Verdict.Skip, SecHygiene,
                "Git LFS coverage of textures is not checked here.",
                "That rule lives in .gitattributes and is enforced at commit/CI time (qa-test's lane) — " +
                "this tool only inspects the scene.");
        }

        private void CheckSorting(Scene scene, WaterSurface water)
        {
            // Mesh renderers don't sort against sprites by sorting order alone (they fall back to world
            // Z) — the boat-spotlight-over-water class of bug. A SortingGroup ('sort as 2D') fixes it.
            int meshWarns = 0;
            foreach (var mr in FindInScene<MeshRenderer>(scene))
            {
                if (mr.GetComponentInParent<SortingGroup>(true) != null) continue;
                meshWarns++;
                if (meshWarns <= MaxListedOffenders)
                    Add(Verdict.Warn, SecSorting,
                        $"'{mr.gameObject.name}' is a MESH renderer with no SortingGroup.",
                        "Meshes don't obey sprite sorting order on their own — this object can pop in front " +
                        "of/behind the water and sprites unpredictably. Add a SortingGroup component " +
                        "('sort as 2D').", mr.gameObject);
            }
            if (meshWarns == 0)
                Add(Verdict.Pass, SecSorting, "No un-grouped mesh renderers (mesh-vs-sprite sorting is safe).");

            // A huge sprite sorting at/above the Sea would mask the water reveal (the retired-overlay bug).
            if (water != null)
            {
                var seaRenderer = water.GetComponent<Renderer>();
                var seaSr = seaRenderer as SpriteRenderer;
                if (seaSr != null)
                {
                    Bounds sea = seaSr.bounds;
                    float seaArea = Mathf.Max(0.0001f, sea.size.x * sea.size.y);
                    int covers = 0;
                    foreach (var sr in FindInScene<SpriteRenderer>(scene))
                    {
                        if (sr == seaSr || !sr.enabled || sr.sprite == null) continue;
                        if (sr.sortingLayerID != seaSr.sortingLayerID || sr.sortingOrder < seaSr.sortingOrder) continue;
                        Bounds b = sr.bounds;
                        float ox = Mathf.Max(0f, Mathf.Min(b.max.x, sea.max.x) - Mathf.Max(b.min.x, sea.min.x));
                        float oy = Mathf.Max(0f, Mathf.Min(b.max.y, sea.max.y) - Mathf.Max(b.min.y, sea.min.y));
                        if (ox * oy / seaArea < SpriteOverWaterAreaFraction) continue;
                        covers++;
                        Add(Verdict.Warn, SecSorting,
                            $"'{sr.gameObject.name}' covers most of the sea and sorts AT/ABOVE it (order {sr.sortingOrder} vs {seaSr.sortingOrder}).",
                            "A big sprite over the water hides the tide reveal (the old overlay-grid bug). " +
                            "Ground that the tide should cover must sort BELOW the Sea.", sr.gameObject);
                    }
                    if (covers == 0)
                        Add(Verdict.Pass, SecSorting, "Nothing large draws over the Sea — the tide reveal stays visible.");
                }
            }
        }

        // =====================================================================================
        //  Scene scanning (read-only)
        // =====================================================================================

        private static List<T> FindInScene<T>(Scene scene) where T : Component
            => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(c => c.gameObject.scene == scene)
                     .ToList();

        private List<MonoBehaviour> FindInSceneMonoBehaviours(Scene scene)
            => FindInScene<MonoBehaviour>(scene);
    }
}
#endif
