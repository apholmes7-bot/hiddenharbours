using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Terrain pass 9's plate (docs/design/st-peters-terrain-pass-9.md, PR 2): the drop's five example
    /// scenes laid out from their JSON on the engine's ground, in a scratch scene that is never saved
    /// (GrassTestBuilder is the precedent: nothing committed changes), and rendered under each of the
    /// scene's weather presets beside the drop's own pictures.
    ///
    /// <para><b>What it lays.</b> The ground: a <see cref="TerrainSplatSurface"/> the scene's size, one
    /// screen pixel to the kit's pixel, on the committed detail and relight arrays, painted with the
    /// scene's materials. A texel's material is read from the scene's unlit map (the JSON's first map),
    /// which draws every material in its own palette's bands, so a colour names its material among the
    /// scene's, by the palettes the bake recorded (<see cref="TerrainTexArrayBuilder.RelightJsonPath"/>).
    /// A colour two of the scene's materials share, and the pond water the unlit draws over its ground,
    /// take the material around them. Each material is painted whole, channel value 1, which the
    /// engine's ladder reads as the _Hi rung: the rung rides on the coverage, while the scenes paint
    /// most materials at the base rung, and plate.txt says which. The ground stands flat and high, so it
    /// neither clips at the paint floor nor darkens in the wet band.</para>
    ///
    /// <para><b>What it leaves out,</b> and says so per scene: the trees and plants (their PlantRig5 and
    /// TreeRig4 sprites land with PR 3), the water (the sea plane and PR 4's still water own it), ground
    /// snow (the plan's decision 4), and the rain and snow particles.</para>
    ///
    /// <para><b>The light.</b> Each preset's sky is the one TerrainLight6's twin fixture records for
    /// it: the preset's WeatherSky, the sky the drop rendered under (checked value for value on Node).
    /// It is set on the plate's own material. The day/night cycle's _SunDir is zeroed while the plate
    /// renders, so the preset's sun is the one used, and put back after. The wind loop is held at frame
    /// 0, the drop's frame.</para>
    ///
    /// <para><b>What it writes,</b> under Evidence~/plate: each render named as the drop names it, its
    /// absolute difference from the drop's, times four, beside it, and plate.txt with the pixel
    /// comparison of every render and each scene's material reading. Nothing under Assets changes, and
    /// no scene is saved.</para>
    ///
    /// <para>Menu: <b>Hidden Harbours ▸ Art ▸ Terrain Pass 9 Plate</b>. Batch:
    /// <c>-executeMethod HiddenHarbours.App.Editor.TerrainPass9Plate.RenderDefault</c>.</para>
    /// </summary>
    public static class TerrainPass9Plate
    {
        public const string MenuPath = "Hidden Harbours/Art/Terrain Pass 9 Plate";

        /// <summary>The drop's scenes where the lane unzips it, from the project root.</summary>
        public const string DefaultScenesDir = "Evidence~/drop/Export/pixel-terrain-pass-9/scenes";

        /// <summary>Where the plate writes, from the project root: evidence, never committed.</summary>
        public const string DefaultOutDir = "Evidence~/plate";

        /// <summary>TerrainLight6's twin fixture, whose cases carry each preset's sky.</summary>
        public const string SkyFixturePath = "docs/art/rigs/terrain/pass9/fixtures/terrainLight6.twin.json";

        private const string ShaderName = "HiddenHarbours/TerrainSplat";

        // The paint: whole, one material a texel. The brush's channel value, and so the ladder's _Hi rung.
        private const float PaintValue = 1f;

        // The flat ground's height: above the edit-mode preview tide (the surface's slider tops out at
        // 6 m) and its wet band, and far above the paint floor.
        private const float GroundMetres = 7f;

        // An unnamed layer (ProjectSettings/TagManager.asset names 0 to 5), so the plate's camera sees
        // only the plate, whatever else is open.
        private const int PlateLayer = 31;

        // The difference picture's gain.
        private const int DiffGain = 4;

        // The six splat maps the surface takes (A to F).
        private const int SplatMaps = 6;

        // A texel's reading, before or instead of a slot.
        private const int Unread = -1;
        private const int NoSlot = -2;

        private static readonly string[] RungNames = { "_Lo", "base", "_Hi" };

        private static readonly int IdSunDir = Shader.PropertyToID("_SunDir");
        private static readonly int IdPixelsPerMetre = Shader.PropertyToID("_PixelsPerMetre");
        private static readonly int IdSunF = Shader.PropertyToID("_TLSunF");
        private static readonly int IdSunI = Shader.PropertyToID("_TLSunI");
        private static readonly int IdSkyI = Shader.PropertyToID("_TLSkyI");
        private static readonly int IdExpo = Shader.PropertyToID("_TLExpo");
        private static readonly int IdWet = Shader.PropertyToID("_TLWet");
        private static readonly int IdRain = Shader.PropertyToID("_TLRain");
        private static readonly int IdFog = Shader.PropertyToID("_TLFog");
        private static readonly int IdWind = Shader.PropertyToID("_TLWind");
        private static readonly int IdGrade = Shader.PropertyToID("_TLGrade");
        private static readonly int IdKey = Shader.PropertyToID("_TLKey");
        private static readonly int IdAmbient = Shader.PropertyToID("_TLAmbient");
        private static readonly int IdWash = Shader.PropertyToID("_TLWash");
        private static readonly int IdFogColour = Shader.PropertyToID("_TLFogColour");
        private static readonly int IdSkyColour = Shader.PropertyToID("_TLSkyColour");
        private static readonly int IdGradeWeights = Shader.PropertyToID("_TLGradeWeights");
        private static readonly int IdSeaDir = Shader.PropertyToID("_TLSeaDir");
        private static readonly int IdFrameRate = Shader.PropertyToID("_TLFrameRate");
        private static readonly int IdOrigin = Shader.PropertyToID("_TLOrigin");

        [MenuItem(MenuPath, priority = 26)]
        public static void RenderMenu()
        {
            string scenes = Path.GetFullPath(DefaultScenesDir);
            if (!Directory.Exists(scenes))
            {
                scenes = EditorUtility.OpenFolderPanel("The drop's scenes (Export/pixel-terrain-pass-9/scenes)", "", "");
                if (string.IsNullOrEmpty(scenes)) return;
            }
            Render(scenes, Path.GetFullPath(DefaultOutDir));
        }

        /// <summary>The batch entry: the default folders, and no dialogs.</summary>
        public static void RenderDefault() =>
            Render(Path.GetFullPath(DefaultScenesDir), Path.GetFullPath(DefaultOutDir));

        /// <summary>Lays out and renders every scene under <paramref name="scenesDir"/> (a folder per
        /// scene, holding its <c>key.scene.json</c>), and writes the renders, their differences and
        /// plate.txt under <paramref name="outDir"/>. Returns the renders written: 0, with a warning,
        /// when the plate cannot run.</summary>
        public static int Render(string scenesDir, string outDir)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return Refuse("leave Play mode first.");
            if (!Directory.Exists(scenesDir)) return Refuse($"no scenes folder at {scenesDir}.");
            var sceneDirs = new List<string>();
            foreach (string dir in Directory.GetDirectories(scenesDir))
                if (File.Exists(SceneJson(dir))) sceneDirs.Add(dir);
            sceneDirs.Sort(StringComparer.Ordinal);
            if (sceneDirs.Count == 0) return Refuse($"no key/key.scene.json under {scenesDir}.");

            Shader shader = Shader.Find(ShaderName);
            if (shader == null) return Refuse($"the {ShaderName} shader is missing.");
            Dictionary<string, Sky> skies = LoadSkies(out string why);
            if (skies == null) return Refuse(why);
            RelightTile[] tiles = LoadPalettes(out why);
            if (tiles == null) return Refuse(why);
            var arrays = new Arrays
            {
                Detail = AssetDatabase.LoadAssetAtPath<Texture2DArray>(TerrainTexArrayBuilder.Array256Path),
                Normal = AssetDatabase.LoadAssetAtPath<Texture2DArray>(TerrainTexArrayBuilder.RelightArrayPaths[0]),
                Light = AssetDatabase.LoadAssetAtPath<Texture2DArray>(TerrainTexArrayBuilder.RelightArrayPaths[1]),
                Marks = AssetDatabase.LoadAssetAtPath<Texture2DArray>(TerrainTexArrayBuilder.RelightArrayPaths[2]),
                Ramp = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainTexArrayBuilder.RelightRampPath),
            };
            if (arrays.Detail == null) return Refuse($"no detail array at {TerrainTexArrayBuilder.Array256Path}.");

            Scene previous = SceneManager.GetActiveScene();
            bool additive = previous.IsValid() && !string.IsNullOrEmpty(previous.path);
            if (!additive && previous.IsValid() && previous.isDirty)
                return Refuse("the open scene is untitled and has changes; save or discard them first, the plate will not.");

            Directory.CreateDirectory(outDir);
            var report = new StringBuilder();
            report.AppendLine("Terrain pass 9 plate: the drop's example scenes on the engine's ground.");
            report.AppendLine($"Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}).");
            report.AppendLine($"Scenes: {scenesDir}");
            report.AppendLine($"Sky: {SkyFixturePath} ({skies.Count} skies). Palettes: {TerrainTexArrayBuilder.RelightJsonPath} ({tiles.Length} tiles).");
            report.AppendLine($"Arrays: detail {arrays.Detail.depth} slices; relight normal {Depth(arrays.Normal)}, light "
                              + $"{Depth(arrays.Light)}, detail {Depth(arrays.Marks)}; ramp "
                              + (arrays.Ramp != null ? $"{arrays.Ramp.width}x{arrays.Ramp.height}" : "none") + ".");
            report.AppendLine($"Paint: value {PaintValue} (the _Hi rung). Ground: flat at {GroundMetres} m. Frame 0. "
                              + "Comparison: RGB only; differences shown times " + DiffGain + ".");

            Vector4 sunDir = Shader.GetGlobalVector(IdSunDir);
            Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                                                        additive ? NewSceneMode.Additive : NewSceneMode.Single);
            if (!scratch.IsValid()) return Refuse("Unity would not open a scratch scene beside the open ones.");
            int written = 0;
            try
            {
                SceneManager.SetActiveScene(scratch);
                Shader.SetGlobalVector(IdSunDir, Vector4.zero);
                foreach (string dir in sceneDirs)
                    written += RenderScene(dir, outDir, shader, skies, tiles, arrays, report);
            }
            finally
            {
                Shader.SetGlobalVector(IdSunDir, sunDir);
                if (additive)
                {
                    if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scratch, true);
                }
            }

            string summary = Path.Combine(outDir, "plate.txt");
            File.WriteAllText(summary, report.ToString());
            Debug.Log($"[TerrainPass9Plate] {written} renders of {sceneDirs.Count} scenes written under {outDir}; "
                      + $"the comparison is in {summary}. No scene was saved.");
            return written;
        }

        // =============================================== ONE SCENE ===============================================

        private static int RenderScene(string dir, string outDir, Shader shader, Dictionary<string, Sky> skies,
                                       RelightTile[] tiles, Arrays arrays, StringBuilder report)
        {
            string key = Path.GetFileName(dir);
            string raw = File.ReadAllText(SceneJson(dir));
            SceneDto sc;
            try { sc = JsonUtility.FromJson<SceneDto>(raw); }
            catch (ArgumentException e) { report.AppendLine($"\n{key}: its scene.json is not JSON the plate reads ({e.Message})."); return 0; }
            if (sc == null || sc.width <= 0 || sc.height <= 0 || sc.materials == null || sc.maps == null
                || sc.maps.Length == 0 || !sc.maps[0].EndsWith("_unlit.png", StringComparison.Ordinal) || sc.renders == null)
            {
                report.AppendLine($"\n{key}: its scene.json lacks a size, materials, an unlit map first, or renders.");
                return 0;
            }

            int w = sc.width, h = sc.height, n = w * h;
            Color32[] unlit = LoadPng(Path.Combine(dir, sc.maps[0]), w, h, out string why);
            if (unlit == null) { report.AppendLine($"\n{key}: {why}"); return 0; }

            // The scene's palettes: each colour's candidate slots, and its rungs (slot * steps + step).
            int steps = TerrainTexArrayBuilder.LadderSteps.Length;
            var sceneKeys = new HashSet<string>(sc.materials, StringComparer.Ordinal);
            var slotLists = new Dictionary<int, List<int>>();
            var rungLists = new Dictionary<int, List<int>>();
            var unslottedColours = new HashSet<int>();
            var slotOfKey = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (RelightTile t in tiles)
            {
                if (t == null || t.palettes == null || !sceneKeys.Contains(t.key)) continue;
                string name = EngineName(t);
                int slot = name == null ? -1 : Array.IndexOf(TerrainSplatBrush.MaterialNames, name);
                slotOfKey[t.key] = slot;
                foreach (string hex in t.palettes)
                {
                    int rgb = Rgb(hex);
                    if (rgb < 0) continue;
                    if (slot < 0) { unslottedColours.Add(rgb); continue; }
                    AddOnce(slotLists, rgb, slot);
                    AddOnce(rungLists, rgb, slot * steps + t.step);
                }
            }
            var candidates = new Dictionary<int, int[]>();
            foreach (var kv in slotLists)
            {
                kv.Value.Sort();
                candidates.Add(kv.Key, kv.Value.ToArray());
            }

            int[] slots = ReadMaterials(unlit, w, h, candidates, unslottedColours, out Reading reading);

            // Each material's texels, and the rung the scene painted it at, where a colour tells.
            int slotCount = TerrainSplatBrush.MaterialNames.Length;
            var texels = new int[slotCount];
            var rungVotes = new int[slotCount * steps];
            for (int i = 0; i < n; i++)
            {
                int s = slots[i];
                if (s < 0) continue;
                texels[s]++;
                if (!rungLists.TryGetValue(RgbOf(unlit[i]), out List<int> rungs)) continue;
                int only = -1;
                foreach (int r in rungs)
                    if (r / steps == s) only = only == -1 ? r : -2;
                if (only >= 0) rungVotes[only]++;
            }

            // The paint, through the brush, so the channels are packed as the painter packs them.
            var layers = new Color[TerrainSplatBrush.TextureCount][];
            for (int t = 0; t < layers.Length; t++) layers[t] = new Color[n];
            var coverage = new float[n];
            for (int s = 0; s < slotCount; s++)
            {
                if (texels[s] == 0) continue;
                for (int i = 0; i < n; i++) coverage[i] = slots[i] == s ? 1f : 0f;
                TerrainSplatBrush.PaintField(layers, w, h, s, PaintValue, coverage, false);
            }

            var made = new List<Object>();
            int written = 0;
            try
            {
                var splats = new Texture2D[SplatMaps];
                for (int t = 0; t < layers.Length && t < SplatMaps; t++)
                {
                    splats[t] = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
                    {
                        name = $"TerrainPass9Plate {key} splat {(char)('A' + t)}",
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp,
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    made.Add(splats[t]);
                    splats[t].SetPixels(layers[t]);
                    splats[t].Apply(false, false);
                }
                var flat = new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
                    { name = "TerrainPass9Plate flat ground", hideFlags = HideFlags.HideAndDontSave };
                made.Add(flat);
                flat.SetPixel(0, 0, Color.white);
                flat.Apply(false, false);

                var material = new Material(shader) { name = "TerrainPass9Plate " + key, hideFlags = HideFlags.HideAndDontSave };
                made.Add(material);
                float ppm = material.GetFloat(IdPixelsPerMetre);
                if (ppm <= 0f) { report.AppendLine($"\n{key}: the shader's _PixelsPerMetre is {ppm}."); return 0; }
                var size = new Vector2(w / ppm, h / ppm);
                material.SetVector(IdOrigin, new Vector4(0f, size.y, 0f, 0f));   // the rig's pixel (0, 0): the scene's top left
                material.SetFloat(IdFrameRate, 0f);                               // the loop held at frame 0
                material.SetFloat(IdSeaDir, 0f);                                  // unset, as the drop renders

                var root = new GameObject("TerrainPass9Plate " + key);
                made.Add(root);
                root.SetActive(false);
                var surface = root.AddComponent<TerrainSplatSurface>();
                surface.Configure(size * 0.5f, size, material, TerrainSplatSurface.DefaultSortingOrder);
                surface.ConfigureHeightMap(flat, GroundMetres, GroundMetres);
                surface.ConfigureDetail(arrays.Detail, null);
                surface.ConfigureSplat(splats[0], splats[1], splats[2], splats[3], splats[4], splats[5]);
                surface.ConfigureRelight(arrays.Normal, arrays.Light, arrays.Marks, arrays.Ramp);
                root.SetActive(true);   // OnEnable builds the quad and pushes every map
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = PlateLayer;
                bool relit = surface.RelightLoaded;

                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    { name = "TerrainPass9Plate " + key, filterMode = FilterMode.Point, antiAliasing = 1 };
                made.Add(rt);
                var camGo = new GameObject("TerrainPass9Plate camera") { layer = PlateLayer };
                made.Add(camGo);
                camGo.transform.position = new Vector3(size.x * 0.5f, size.y * 0.5f, -10f);
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = size.y * 0.5f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 100f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.magenta;   // any gap in the ground shows
                cam.allowMSAA = false;
                cam.allowHDR = false;
                cam.cullingMask = 1 << PlateLayer;
                cam.targetTexture = rt;
                cam.aspect = (float)w / h;

                report.AppendLine();
                report.AppendLine($"{key} ({sc.name}): {w}x{h} px, {Metres(size.x)} x {Metres(size.y)} m. "
                                  + (relit ? "Relight on." : "Relight OFF: the maps are missing or do not fit, so this is the albedo ground."));
                report.AppendLine($"  materials: {string.Join(", ", sc.materials)}");
                report.AppendLine($"  read from {Path.GetFileName(sc.maps[0])}: {reading.Unique} texels by one palette, {reading.Shared} by a "
                                  + $"shared colour, {reading.Unknown} by none (pond water); {reading.FromNeighbours} took their "
                                  + $"neighbours' material, {reading.FirstCandidate} their first candidate, {reading.Unpainted} none.");
                report.AppendLine("  " + Unplaced(sc, slotOfKey, reading));
                report.AppendLine("  painted (value 1, the _Hi rung; in brackets the rung the scene painted): " + Painted(texels, rungVotes, steps));
                report.AppendLine("  not laid: " + NotLaid(sc, raw));

                var shot = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                made.Add(shot);
                var diffTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                made.Add(diffTex);
                var diff = new Color32[n];
                string sceneOut = Path.Combine(outDir, key);
                Directory.CreateDirectory(sceneOut);

                foreach (RenderDto r in sc.renders)
                {
                    if (r == null || string.IsNullOrEmpty(r.file)) continue;
                    if (!skies.TryGetValue(r.preset ?? "", out Sky sky))
                    {
                        report.AppendLine($"  {r.file}: no sky named '{r.preset}' in the fixture; not rendered.");
                        continue;
                    }
                    ApplySky(material, sky);
                    if (!RenderSettled(cam))
                    {
                        report.AppendLine($"  {r.file}: shaders were still compiling after two minutes; not rendered.");
                        continue;
                    }
                    RenderTexture active = RenderTexture.active;
                    RenderTexture.active = rt;
                    shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    shot.Apply(false, false);
                    RenderTexture.active = active;

                    Color32[] mine = shot.GetPixels32();
                    for (int i = 0; i < n; i++) mine[i].a = 255;
                    shot.SetPixels32(mine);
                    File.WriteAllBytes(Path.Combine(sceneOut, r.file), shot.EncodeToPNG());
                    written++;

                    Color32[] theirs = LoadPng(Path.Combine(dir, r.file), w, h, out why);
                    if (theirs == null) { report.AppendLine($"  {r.file} [{r.preset} {r.clock}]: rendered; {why}"); continue; }
                    string line = Compare(mine, theirs, diff);
                    diffTex.SetPixels32(diff);
                    string diffName = Path.GetFileNameWithoutExtension(r.file) + "_diff.png";
                    File.WriteAllBytes(Path.Combine(sceneOut, diffName), diffTex.EncodeToPNG());
                    report.AppendLine($"  {r.file} [{r.preset} {r.clock}]: {line}");
                }
            }
            finally
            {
                for (int i = made.Count - 1; i >= 0; i--)
                    if (made[i] != null) Object.DestroyImmediate(made[i]);
            }
            return written;
        }

        // ============================================ READING THE GROUND ============================================

        /// <summary>What <see cref="ReadMaterials"/> found, texel by texel.</summary>
        private struct Reading
        {
            public int Unique, Shared, Unknown, Unslotted, FromNeighbours, FirstCandidate, Unpainted;
        }

        /// <summary>
        /// Each texel's slot, read from the scene's unlit map: the one slot whose palettes hold the
        /// texel's colour, among the scene's materials (<paramref name="candidates"/>, each list in slot
        /// order). A colour that only a material with no slot paints is left unpainted. A colour two
        /// materials share, or one no palette holds (the pond water the unlit draws over its ground),
        /// takes the commonest slot among its eight neighbours, among its own candidates when it has
        /// some, a ring at a time from the texels already read. Each ring is decided from the one before,
        /// so the order the texels are visited in cannot change the answer, and ties go to the lower
        /// slot. What no ring reaches takes its first candidate, or stays unpainted.
        /// </summary>
        private static int[] ReadMaterials(Color32[] unlit, int width, int height,
                                           Dictionary<int, int[]> candidates, HashSet<int> unslotted,
                                           out Reading reading)
        {
            reading = default;
            int n = width * height;
            var slot = new int[n];
            var among = new int[n][];
            for (int i = 0; i < n; i++)
            {
                int rgb = RgbOf(unlit[i]);
                candidates.TryGetValue(rgb, out int[] c);
                if (c != null && c.Length == 1) { slot[i] = c[0]; reading.Unique++; continue; }
                if (c == null && unslotted.Contains(rgb)) { slot[i] = NoSlot; reading.Unslotted++; continue; }
                slot[i] = Unread;
                among[i] = c;
                if (c == null) reading.Unknown++;
                else reading.Shared++;
            }

            int most = 0;
            foreach (int[] c in candidates.Values)
                foreach (int s in c) most = Math.Max(most, s + 1);
            var votes = new int[most];
            var next = (int[])slot.Clone();
            for (bool changed = true; changed;)
            {
                changed = false;
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        if (slot[i] != Unread) continue;
                        int s = Vote(slot, width, height, x, y, among[i], votes);
                        if (s < 0) continue;
                        next[i] = s;
                        reading.FromNeighbours++;
                        changed = true;
                    }
                Array.Copy(next, slot, n);
            }

            for (int i = 0; i < n; i++)
            {
                if (slot[i] == NoSlot) { slot[i] = Unread; continue; }
                if (slot[i] != Unread) continue;
                if (among[i] != null) { slot[i] = among[i][0]; reading.FirstCandidate++; }
                else reading.Unpainted++;
            }
            return slot;
        }

        // The commonest read slot among a texel's eight neighbours (and among its candidates, when it
        // has some); the lower slot on a tie; -1 when no neighbour qualifies.
        private static int Vote(int[] slot, int width, int height, int x, int y, int[] among, int[] votes)
        {
            int best = -1, bestVotes = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int s = slot[ny * width + nx];
                        if (s < 0 || s >= votes.Length || (among != null && Array.IndexOf(among, s) < 0)) continue;
                        if (pass == 1) { votes[s] = 0; continue; }   // the second pass only clears the count
                        int v = ++votes[s];
                        if (v > bestVotes || (v == bestVotes && s < best)) { best = s; bestVotes = v; }
                    }
            return best;
        }

        // ================================================ THE LIGHT ================================================

        private static void ApplySky(Material m, Sky s)
        {
            m.SetVector(IdSunF, new Vector4(s.sunF[0], s.sunF[1], s.sunF[2], 0f));
            m.SetFloat(IdSunI, s.sunI);
            m.SetFloat(IdSkyI, s.skyI);
            m.SetFloat(IdExpo, s.expo);
            m.SetFloat(IdWet, s.wet);
            m.SetFloat(IdRain, s.rain);
            m.SetFloat(IdFog, s.fog);
            m.SetFloat(IdWind, s.wind);
            m.SetFloat(IdGrade, s.grade ? 1f : 0f);
            m.SetVector(IdKey, Bytes(s.kc));
            m.SetVector(IdAmbient, Bytes(s.ac));
            m.SetVector(IdWash, Bytes(s.wash));
            m.SetVector(IdFogColour, Bytes(s.fogC));
            m.SetVector(IdSkyColour, Bytes(s.skyC));
            m.SetVector(IdGradeWeights, new Vector4(s.aa, s.amb, s.ka, s.wa));
        }

        // Renders until no shader is compiling, then once more to be read (a cold shader cache has faked
        // a regression before: LampShadowRenderTests). False when compiling outlasts two minutes.
        private static bool RenderSettled(Camera cam)
        {
            for (int i = 0; i < 10; i++)
            {
                cam.Render();
                if (!ShaderUtil.anythingCompiling)
                {
                    cam.Render();
                    return true;
                }
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (ShaderUtil.anythingCompiling && clock.Elapsed.TotalSeconds < 120)
                    System.Threading.Thread.Sleep(25);
            }
            return false;
        }

        // ============================================ THE COMPARISON ============================================

        private static string Compare(Color32[] mine, Color32[] theirs, Color32[] diff)
        {
            int n = mine.Length, differ = 0, largest = 0;
            long dr = 0, dg = 0, db = 0, mr = 0, mg = 0, mb = 0, tr = 0, tg = 0, tb = 0;
            for (int i = 0; i < n; i++)
            {
                Color32 a = mine[i], b = theirs[i];
                int r = Math.Abs(a.r - b.r), g = Math.Abs(a.g - b.g), bl = Math.Abs(a.b - b.b);
                if ((r | g | bl) != 0) differ++;
                largest = Math.Max(largest, Math.Max(r, Math.Max(g, bl)));
                dr += r; dg += g; db += bl;
                mr += a.r; mg += a.g; mb += a.b;
                tr += b.r; tg += b.g; tb += b.b;
                diff[i] = new Color32((byte)Math.Min(255, r * DiffGain), (byte)Math.Min(255, g * DiffGain),
                                      (byte)Math.Min(255, bl * DiffGain), 255);
            }
            double k = 1.0 / n;
            return string.Format(CultureInfo.InvariantCulture,
                "{0} of {1} pixels differ ({2:0.0}%), the largest channel difference {3}; mean |d| RGB {4:0.0} {5:0.0} {6:0.0}; "
                + "mean RGB plate {7:0.0} {8:0.0} {9:0.0}, drop {10:0.0} {11:0.0} {12:0.0}",
                differ, n, 100.0 * differ * k, largest, dr * k, dg * k, db * k, mr * k, mg * k, mb * k, tr * k, tg * k, tb * k);
        }

        // ============================================ plate.txt lines ============================================

        private static string Unplaced(SceneDto sc, Dictionary<string, int> slotOfKey, Reading reading)
        {
            var noTiles = new List<string>();
            var noSlot = new List<string>();
            foreach (string k in sc.materials)
            {
                if (!slotOfKey.TryGetValue(k, out int s)) noTiles.Add(k);
                else if (s < 0) noSlot.Add(k);
            }
            if (noTiles.Count == 0 && noSlot.Count == 0) return "every material has a slot and baked tiles.";
            string line = "";
            if (noSlot.Count > 0)
                line += $"no slot, so unpainted ({reading.Unslotted} texels): {string.Join(", ", noSlot)}. ";
            if (noTiles.Count > 0)
                line += $"no baked tiles, so read by neighbours: {string.Join(", ", noTiles)}.";
            return line.TrimEnd();
        }

        private static string Painted(int[] texels, int[] rungVotes, int steps)
        {
            var parts = new List<string>();
            for (int s = 0; s < texels.Length; s++)
            {
                if (texels[s] == 0) continue;
                int told = 0, top = 0;
                for (int st = 0; st < steps; st++)
                {
                    int v = rungVotes[s * steps + st];
                    told += v;
                    if (v > rungVotes[s * steps + top]) top = st;
                }
                string rung = told == 0
                    ? "rung untold"
                    : string.Format(CultureInfo.InvariantCulture, "{0} {1:0}%", RungNames[Math.Min(top, RungNames.Length - 1)],
                                    100.0 * rungVotes[s * steps + top] / told);
                parts.Add($"{TerrainSplatBrush.MaterialNames[s]} {texels[s]} ({rung})");
            }
            return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
        }

        private static string NotLaid(SceneDto sc, string raw)
        {
            int trees = 0, plants = 0;
            if (sc.sprites != null)
                foreach (SpriteDto s in sc.sprites)
                {
                    if (s == null) continue;
                    if (s.kind == "tree") trees++;
                    else plants++;
                }
            Match level = Regex.Match(raw, "\"waterLevelM\"\\s*:\\s*(null|-?[0-9.]+)");
            Match water = Regex.Match(raw, "\"water\"\\s*:\\s*(null|\\{[^}]*\\})");
            string sea = !level.Success || level.Groups[1].Value == "null"
                ? "no water"
                : $"water at {level.Groups[1].Value} m {(water.Success ? water.Groups[1].Value : "")}";
            return $"{trees} trees and {plants} plants (PR 3's sprites); {sea} (the sea plane and PR 4); "
                   + "ground snow (decision 4); the rain and snow particles.";
        }

        // =============================================== LOADING ===============================================

        private static Dictionary<string, Sky> LoadSkies(out string why)
        {
            why = null;
            string path = Path.GetFullPath(SkyFixturePath);
            if (!File.Exists(path)) { why = $"no sky fixture at {SkyFixturePath}."; return null; }
            SkyFixture fx;
            try { fx = JsonUtility.FromJson<SkyFixture>(File.ReadAllText(path)); }
            catch (ArgumentException e) { why = $"{SkyFixturePath} is not JSON the plate reads: {e.Message}"; return null; }
            var skies = new Dictionary<string, Sky>(StringComparer.Ordinal);
            if (fx?.cases != null)
                foreach (SkyCase c in fx.cases)
                {
                    Sky s = c?.sky;
                    if (s == null || string.IsNullOrEmpty(s.name) || skies.ContainsKey(s.name)) continue;
                    // A sky the relight's uniforms cannot hold whole is left out; its preset says so.
                    if (s.sunF == null || s.sunF.Length != 3 || !s.hasSkyI || !s.hasWind || Rgb(s.kc) < 0
                        || Rgb(s.ac) < 0 || Rgb(s.wash) < 0 || Rgb(s.fogC) < 0 || Rgb(s.skyC) < 0)
                        continue;
                    skies.Add(s.name, s);
                }
            if (skies.Count > 0) return skies;
            why = $"{SkyFixturePath} holds no sky the plate can use.";
            return null;
        }

        private static RelightTile[] LoadPalettes(out string why)
        {
            why = null;
            string path = Path.GetFullPath(TerrainTexArrayBuilder.RelightJsonPath);
            if (!File.Exists(path))
            {
                why = $"no {TerrainTexArrayBuilder.RelightJsonPath}: the relight maps and their manifest are not committed yet.";
                return null;
            }
            RelightManifest m;
            try { m = JsonUtility.FromJson<RelightManifest>(File.ReadAllText(path)); }
            catch (ArgumentException e) { why = $"{TerrainTexArrayBuilder.RelightJsonPath} is not JSON the plate reads: {e.Message}"; return null; }
            if (m?.tiles != null && m.tiles.Length > 0) return m.tiles;
            why = $"{TerrainTexArrayBuilder.RelightJsonPath} lists no tiles.";
            return null;
        }

        private static Color32[] LoadPng(string path, int width, int height, out string why)
        {
            why = null;
            if (!File.Exists(path)) { why = $"no {Path.GetFileName(path)} beside the scene."; return null; }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(path))) { why = $"{Path.GetFileName(path)} is not an image Unity reads."; return null; }
                if (tex.width != width || tex.height != height)
                {
                    why = $"{Path.GetFileName(path)} is {tex.width}x{tex.height}, not the scene's {width}x{height}.";
                    return null;
                }
                return tex.GetPixels32();
            }
            finally { Object.DestroyImmediate(tex); }
        }

        // ================================================ helpers ================================================

        private static int Refuse(string why)
        {
            Debug.LogWarning("[TerrainPass9Plate] not rendered: " + why);
            return 0;
        }

        private static string SceneJson(string dir)
        {
            string key = Path.GetFileName(dir);
            return Path.Combine(dir, key + ".scene.json");
        }

        /// <summary>The engine's name for a baked tile: its name less its ladder step's suffix (the
        /// builder's naming, <see cref="TerrainTexArrayBuilder.LadderSteps"/>), or null when they disagree.</summary>
        private static string EngineName(RelightTile t)
        {
            string[] steps = TerrainTexArrayBuilder.LadderSteps;
            if (t.name == null || t.step < 0 || t.step >= steps.Length
                || !t.name.EndsWith(steps[t.step], StringComparison.Ordinal))
                return null;
            return t.name.Substring(0, t.name.Length - steps[t.step].Length);
        }

        private static void AddOnce(Dictionary<int, List<int>> map, int key, int value)
        {
            if (!map.TryGetValue(key, out List<int> list)) map.Add(key, list = new List<int>());
            if (!list.Contains(value)) list.Add(value);
        }

        private static int Rgb(string hex)
        {
            if (hex == null || hex.Length != 7 || hex[0] != '#') return -1;
            return int.TryParse(hex.Substring(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int rgb)
                ? rgb : -1;
        }

        private static int RgbOf(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        private static Vector4 Bytes(string hex)
        {
            int c = Rgb(hex);
            return new Vector4((c >> 16) & 255, (c >> 8) & 255, c & 255, 0f);
        }

        private static string Depth(Texture2DArray a) => a != null ? a.depth.ToString(CultureInfo.InvariantCulture) : "none";

        private static string Metres(float m) => m.ToString("0.###", CultureInfo.InvariantCulture);

        private sealed class Arrays
        {
            public Texture2DArray Detail, Normal, Light, Marks;
            public Texture2D Ramp;
        }

        // ---- the JSON the plate reads (JsonUtility: public fields, the files' own names) ----

        [Serializable] sealed class SceneDto
        {
            public string name;
            public int width;
            public int height;
            public string[] materials;
            public string[] maps;
            public RenderDto[] renders;
            public SpriteDto[] sprites;
        }

        [Serializable] sealed class RenderDto
        {
            public string file;
            public string preset;
            public string clock;
        }

        [Serializable] sealed class SpriteDto
        {
            public string kind;
        }

        [Serializable] sealed class SkyFixture
        {
            public SkyCase[] cases;
        }

        [Serializable] sealed class SkyCase
        {
            public Sky sky;
        }

        [Serializable] sealed class Sky
        {
            public string name;
            public float[] sunF;
            public float sunI, expo, wet, rain, fog;
            public bool grade;
            public string kc, ac, wash, fogC, skyC;
            public float aa, amb, ka, wa;
            public bool hasSkyI;
            public float skyI;
            public bool hasWind;
            public float wind;
        }

        [Serializable] sealed class RelightManifest
        {
            public RelightTile[] tiles;
        }

        [Serializable] sealed class RelightTile
        {
            public string key;
            public string name;
            public int step;
            public string[] palettes;
        }
    }
}
