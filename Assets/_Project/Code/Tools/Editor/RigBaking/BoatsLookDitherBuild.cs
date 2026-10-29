using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>Explicit editor entry for the dither evidence and selective bake. No menu or startup hook.</summary>
    public static class BoatsLookDitherBuild
    {
        const int Width = 1920, Height = 1080, Layer = 31;
        const string Evidence = "artifacts/boats-look/pr1/b";
        [Serializable] public sealed class Entry
        {
            public string path, id, rig, type, mainSha256;
            public bool weighted;
        }
        [Serializable] public sealed class Manifest { public string baseRevision; public Entry[] entries; }
        [Serializable] public sealed class Shot
        {
            public string id, asset, side, image, control, imageSha256, controlSha256, utc;
            public int heading, frame, ordinal, visiblePixels;
            public float cameraX, cameraY, cameraSize;
        }
        [Serializable] public sealed class Checkpoint { public string side, utc; public int batch; public long freeBytesBefore; public Shot[] shots; }
        [Serializable] public sealed class NeutralRow { public string id; public int heading, changed; public string baselineHash, candidateHash; }
        [Serializable] public sealed class NeutralTable { public NeutralRow[] rows; }
        [Serializable] public sealed class ParityRow
        {
            public string id, reference, rigValues;
            public int heading, before, after, visibleChange;
        }
        [Serializable] public sealed class ParityTable { public ParityRow[] rows; }
        static string Root => RigCatalog.RepoRoot;
        static string PreviousFolder => Path.Combine(Root, Evidence);
        static string Folder => Path.Combine(PreviousFolder, "b1r");
        static int _ordinal;
        static readonly Dictionary<string, HullMeshDef> Hulls = new Dictionary<string, HullMeshDef>();

        public static void Run()
        {
            try
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    throw new InvalidOperationException("The plate entry requires a graphics device.");
                Directory.CreateDirectory(Folder);
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(PreviousFolder, "manifest.json")));
                if (manifest.entries.Length != 573 || manifest.entries.Select(e => e.path).Distinct().Count() != 573)
                    throw new InvalidOperationException("Frozen manifest must contain exactly 573 unique assets.");
                foreach (var e in manifest.entries)
                    if (!e.weighted && Hash(File.ReadAllBytes(Path.Combine(Root, e.path))) != e.mainSha256)
                        throw new InvalidOperationException("Asset differs from the frozen main blob: " + e.path);
                foreach (string guid in AssetDatabase.FindAssets("t:HullMeshDef"))
                {
                    var h = AssetDatabase.LoadAssetAtPath<HullMeshDef>(AssetDatabase.GUIDToAssetPath(guid));
                    Hulls[h.Id] = h;
                }
                var neutral = manifest.entries.Where(e => !e.weighted).OrderBy(e => e.path, StringComparer.Ordinal).ToArray();
                var weighted = manifest.entries.Where(e => e.weighted).OrderBy(e => e.path, StringComparer.Ordinal).ToArray();
                if (neutral.Length != 509 || weighted.Length != 64)
                    throw new InvalidOperationException("Frozen manifest split differs from 509 / 64.");
                var savedConfig = GameServices.Config;
                var config = savedConfig != null ? Object.Instantiate(savedConfig) : ScriptableObject.CreateInstance<GameConfig>();
                GameServices.Config = config;
                config.HullKeylineFlood = false;
                try
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    Capture("baseline-neutral", neutral, true);
                    Capture("baseline-weighted", weighted, true);
                    Capture("candidate-neutral", neutral, false);
                    Capture("candidate-weighted", weighted, false);
                    VerifyNeutral(neutral);
                    Parity(weighted);
                }
                finally { GameServices.Config = savedConfig; Object.DestroyImmediate(config); }
                Debug.Log("BOATS_LOOK_B1 COMPLETE: 1018 neutral comparisons and 128 parity views.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, "harness-stop.txt"), DateTime.UtcNow.ToString("o") + "\n" + error);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        static void DiskFloor()
        {
            if (new DriveInfo(Path.GetPathRoot(Root)).AvailableFreeSpace < 45_000_000_000L)
                throw new IOException("The batch disk floor is 45 GB; no files were deleted.");
        }

        static string Key(Entry entry, int heading) => entry.id + "-" + heading.ToString(CultureInfo.InvariantCulture);
        static string FileFor(string side, Entry entry, int heading, string suffix)
            => Path.Combine(Folder, side, Key(entry, heading) + suffix);

        static void Capture(string side, Entry[] entries, bool baseline)
        {
            Directory.CreateDirectory(Path.Combine(Folder, side));
            for (int start = 0, batch = 0; start < entries.Length; start += 32, batch++)
            {
                DiskFloor();
                long freeBytesBefore = new DriveInfo(Path.GetPathRoot(Root)).AvailableFreeSpace;
                string checkpoint = Path.Combine(Folder, side, "batch-" + batch.ToString("D2") + ".json");
                if (File.Exists(checkpoint))
                {
                    var kept = JsonUtility.FromJson<Checkpoint>(File.ReadAllText(checkpoint));
                    foreach (var s in kept.shots)
                        if (Hash(File.ReadAllBytes(Path.Combine(Root, s.image))) != s.imageSha256 ||
                            Hash(File.ReadAllBytes(Path.Combine(Root, s.control))) != s.controlSha256)
                            throw new InvalidDataException("Checkpoint PNG hash differs: " + s.id);
                    continue;
                }
                var shots = new List<Shot>();
                foreach (var entry in entries.Skip(start).Take(32))
                    foreach (int heading in new[] { 45, 225 })
                    {
                        using var stage = new Stage(entry, heading, baseline);
                        string metadata = FileFor(side, entry, heading, ".json");
                        Shot before = null;
                        string cameraRecord = baseline ? Path.Combine(PreviousFolder, side, Key(entry, heading) + ".json") :
                            FileFor(side.Replace("candidate", "baseline"), entry, heading, ".json");
                        before = JsonUtility.FromJson<Shot>(File.ReadAllText(cameraRecord));
                        stage.Frame(before);
                        byte[] pixels = stage.Read();
                        byte[] control = stage.Hidden();
                        int visible = Changed(pixels, control);
                        if (visible == 0 || Border(pixels, control))
                            throw new InvalidDataException("Subject is absent or clipped: " + entry.id);
                        string image = FileFor(side, entry, heading, ".png"), hidden = FileFor(side, entry, heading, "-hidden.png");
                        SavePng(image, pixels, Width, Height); SavePng(hidden, control, Width, Height);
                        var shot = new Shot
                        {
                            id = entry.id, asset = entry.path, heading = heading, side = side,
                            image = Relative(image), control = Relative(hidden),
                            imageSha256 = Hash(File.ReadAllBytes(image)), controlSha256 = Hash(File.ReadAllBytes(hidden)),
                            visiblePixels = visible, cameraX = stage.Camera.transform.position.x,
                            cameraY = stage.Camera.transform.position.y, cameraSize = stage.Camera.orthographicSize,
                            utc = DateTime.UtcNow.ToString("o"), frame = Time.renderedFrameCount, ordinal = ++_ordinal,
                        };
                        File.WriteAllText(metadata, JsonUtility.ToJson(shot, true)); shots.Add(shot);
                    }
                File.WriteAllText(checkpoint, JsonUtility.ToJson(new Checkpoint { side = side, batch = batch,
                    utc = DateTime.UtcNow.ToString("o"), freeBytesBefore = freeBytesBefore, shots = shots.ToArray() }, true));
                Debug.Log("BOATS_LOOK checkpoint " + side + " " + batch + " subjects=" + shots.Count / 2);
            }
        }

        static void Bake(Entry[] weighted)
        {
            string checkpoint = Path.Combine(Folder, "bake-complete.json");
            if (File.Exists(checkpoint)) return;
            DiskFloor();
            int hulls = 0, schemes = 0;
            foreach (var entry in weighted.Where(e => e.type == "HullMeshDef"))
            {
                var h = HullMeshFleet.Hulls.Single(x => x.MeshAssetPath == entry.path);
                RigMeshAssetBaker.Bake(h.ScriptPath, h.GlobalName, h.MeshAssetPath, h.MeshId, h.Extraction);
                hulls++;
            }
            var ids = new HashSet<string>(weighted.Where(e => e.type == "HullPaintSchemeDef")
                .Select(e => AssetDatabase.LoadAssetAtPath<HullPaintSchemeDef>(e.path).HullMeshId));
            var log = new System.Text.StringBuilder();
            foreach (var h in HullPaintSchemeBaker.Fleet.Where(h => ids.Contains(h.HullMeshId))) schemes += HullPaintSchemeBaker.Bake(h, log);
            if (hulls != 9 || schemes != 55) throw new InvalidOperationException("Selective bake count differs: " + hulls + "/" + schemes);
            AssetDatabase.SaveAssets();
            Hulls.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:HullMeshDef"))
            {
                var h = AssetDatabase.LoadAssetAtPath<HullMeshDef>(AssetDatabase.GUIDToAssetPath(guid)); Hulls[h.Id] = h;
            }
            File.WriteAllText(Path.Combine(Folder, "bake.txt"), log.ToString());
            File.WriteAllText(checkpoint, "{\"hulls\":9,\"schemes\":55,\"utc\":\"" + DateTime.UtcNow.ToString("o") + "\"}");
        }

        static void VerifyNeutral(Entry[] entries)
        {
            var rows = new List<NeutralRow>();
            foreach (var e in entries) foreach (int heading in new[] { 45, 225 })
            {
                string a = FileFor("baseline-neutral", e, heading, ".png"), b = FileFor("candidate-neutral", e, heading, ".png");
                int changed = Changed(LoadPng(a), LoadPng(b));
                rows.Add(new NeutralRow { id = e.id, heading = heading, changed = changed,
                    baselineHash = Hash(File.ReadAllBytes(a)), candidateHash = Hash(File.ReadAllBytes(b)) });
                if (changed != 0) throw new InvalidDataException("Neutrality failed: " + e.id + " heading " + heading + " pixels " + changed);
            }
            File.WriteAllText(Path.Combine(Folder, "neutrality.json"), JsonUtility.ToJson(new NeutralTable { rows = rows.ToArray() }, true));
        }

        static void Parity(Entry[] entries)
        {
            Directory.CreateDirectory(Path.Combine(Folder, "reference"));
            var rows = new List<ParityRow>();
            foreach (var e in entries)
            {
                DiskFloor();
                var scheme = e.type == "HullPaintSchemeDef" ? AssetDatabase.LoadAssetAtPath<HullPaintSchemeDef>(e.path) : null;
                var h = HullMeshFleet.Hulls.Single(x => scheme != null ? x.MeshId == scheme.HullMeshId : x.MeshAssetPath == e.path);
                using var host = RigScriptHostFactory.Create();
                var data = RigMeshExtractor.ExtractFrom(host, h.ScriptPath, h.GlobalName, hull: h.Extraction);
                string scope = h.Extraction?.ScopeOr(h.GlobalName) ?? h.GlobalName;
                foreach (int heading in new[] { 45, 225 })
                {
                    var shot = JsonUtility.FromJson<Shot>(File.ReadAllText(FileFor("baseline-weighted", e, heading, ".json")));
                    string options = h.Extraction?.ViewOptions ?? "{}";
                    string paint = scheme != null ? ",scheme:'" + scheme.RigPaintId + "'" : "";
                    string args = (heading / 45d).ToString("R", CultureInfo.InvariantCulture) +
                        ",Object.assign({}," + options + ",{elev:" + data.DefaultElev.ToString("R", CultureInfo.InvariantCulture) +
                        ",roll:0,pitch:0,heave:0" + paint + "})";
                    byte[] truth = host.EvaluateBytes(scope + ".render(" + args + ")");
                    if (truth.Length != data.W * data.H * 4) throw new InvalidDataException("Rig render has wrong size: " + e.id);
                    byte[] aligned = Align(truth, data, shot);
                    string reference = FileFor("reference", e, heading, ".png"); SavePng(reference, aligned, Width, Height);
                    byte[] before = LoadPng(FileFor("baseline-weighted", e, heading, ".png"));
                    byte[] after = LoadPng(FileFor("candidate-weighted", e, heading, ".png"));
                    rows.Add(new ParityRow { id = e.id, heading = heading, reference = Relative(reference),
                        before = Changed(before, aligned), after = Changed(after, aligned), visibleChange = Changed(before, after),
                        rigValues = string.Join(";", data.Materials.Concat(data.InteriorMaterials).Select(m => m.Name + "=" + m.DitherWeight.ToString("R", CultureInfo.InvariantCulture))) });
                }
            }
            File.WriteAllText(Path.Combine(Folder, "parity.json"), JsonUtility.ToJson(new ParityTable { rows = rows.ToArray() }, true));
        }

        static byte[] Align(byte[] topLeft, RigMeshData data, Shot shot)
        {
            var pixels = new byte[Width * Height * 4];
            double targetPpu = Height / (2d * shot.cameraSize), sourcePpu = data.PxPerMetre;
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            {
                double wx = shot.cameraX + (x + 0.5 - Width / 2d) / targetPpu;
                double wy = shot.cameraY + (y + 0.5 - Height / 2d) / targetPpu;
                int sx = (int)Math.Floor(wx * sourcePpu + data.PivotX);
                int sy = (int)Math.Floor(data.PivotY - wy * sourcePpu);
                if (sx >= 0 && sx < data.W && sy >= 0 && sy < data.H)
                    Buffer.BlockCopy(topLeft, (sy * data.W + sx) * 4, pixels, (y * Width + x) * 4, 4);
            }
            return pixels;
        }

        sealed class Stage : IDisposable
        {
            public readonly Camera Camera;
            readonly GameObject _subject, _camera;
            readonly RenderTexture _target;
            readonly IsoFacetHullSetup _setup;
            readonly IsoFacetPropRenderer _fitting;
            bool _warm;
            public Stage(Entry e, int heading, bool baseline)
            {
                _subject = new GameObject("DitherSubject");
                HullPropMeshDef part = null;
                bool reverseHeading = false;
                if (e.type == "HullMeshDef") _setup = IsoFacetHullPresentationService.ToSetup(AssetDatabase.LoadAssetAtPath<HullMeshDef>(e.path));
                else if (e.type == "HullPaintSchemeDef")
                {
                    var paint = AssetDatabase.LoadAssetAtPath<HullPaintSchemeDef>(e.path); _setup = IsoFacetHullPresentationService.ToSetup(Hulls[paint.HullMeshId], paint);
                }
                else if (e.type == "VehicleMeshDef") _setup = IsoFacetVehiclePresentationService.ToSetup(AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(e.path));
                else if (e.type == "HullPropMeshDef")
                {
                    part = AssetDatabase.LoadAssetAtPath<HullPropMeshDef>(e.path);
                    _setup = Adapt(part.Mesh, part.Ramps, part.LightN, part.Gain, part.Bias, part.Bayer16, part.Keyline,
                        part.PivotPx, part.PxPerMetre, part.CellW, part.CellH, part.ElevationDeg);
                }
                else
                {
                    var held = AssetDatabase.LoadAssetAtPath<HeldPropMeshDef>(e.path);
                    reverseHeading = held.AzimuthCounterClockwise;
                    _setup = Adapt(held.Mesh, held.Ramps, held.LightN, held.Gain, held.Bias, held.Bayer16, held.Keyline,
                        held.PivotPx, held.PxPerMetre, held.CellW, held.CellH, held.ElevationDeg);
                }
                if (baseline) { _setup.RampDitherWeights = null; _setup.InteriorRampDitherWeights = null; }
                var hull = _subject.AddComponent<IsoFacetHullRenderer>(); hull.Configure(_setup);
                hull.HeadingDirUnits = (reverseHeading ? -heading : heading) / 45f;
                hull.RollDegrees = hull.PitchDegrees = hull.HeavePixels = 0; hull.ApplyPose();
                if (part != null)
                {
                    hull.PosedMesh.GetComponent<MeshRenderer>().enabled = false;
                    var fitting = new GameObject("DitherFitting"); fitting.transform.SetParent(hull.PosedMesh, false);
                    var setup = IsoFacetHullPresentationService.ToPropSetup(part); if (baseline) setup.RampDitherWeights = null;
                    var renderer = fitting.AddComponent<IsoFacetPropRenderer>();
                    _fitting = renderer;
                    renderer.Configure(setup);
                    renderer.WriteHullProperties();
                }
                foreach (var t in _subject.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                _camera = new GameObject("DitherCamera"); Camera = _camera.AddComponent<Camera>();
                Camera.orthographic = true; Camera.nearClipPlane = 1; Camera.farClipPlane = 400;
                Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = Color.clear;
                Camera.cullingMask = 1 << Layer; Camera.allowHDR = false; Camera.allowMSAA = false;
                _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
                Camera.targetTexture = _target;
            }
            public void Frame(Shot kept)
            {
                Camera.transform.position = kept != null ? new Vector3(kept.cameraX, kept.cameraY, -100) :
                    new Vector3((_setup.CellW / 2f - _setup.PivotPx.x) / _setup.PxPerMetre,
                        (_setup.PivotPx.y - _setup.CellH / 2f) / _setup.PxPerMetre, -100);
                Camera.orthographicSize = kept != null ? kept.cameraSize :
                    Mathf.Max(_setup.CellH, _setup.CellW * Height / (float)Width) / (2f * _setup.PxPerMetre) * 1.08f;
            }
            public byte[] Read()
            {
                Camera.Render();
                if (!_warm)
                {
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    while (ShaderUtil.anythingCompiling && clock.Elapsed.TotalSeconds < 600)
                    { System.Threading.Thread.Sleep(25); Camera.Render(); }
                    if (ShaderUtil.anythingCompiling) throw new TimeoutException("Shader warm-up exceeded 600 seconds.");
                    Camera.Render(); _warm = true;
                }
                var old = RenderTexture.active; RenderTexture.active = _target;
                var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                try { texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply(); return texture.GetRawTextureData<byte>().ToArray(); }
                finally { RenderTexture.active = old; Object.DestroyImmediate(texture); }
            }
            public byte[] Hidden()
            {
                var renderers = _subject.GetComponentsInChildren<Renderer>(); var enabled = renderers.Select(r => r.enabled).ToArray();
                for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = false;
                _fitting?.WriteHullProperties();
                try { return Read(); } finally { for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = enabled[i]; }
            }
            public void Dispose() { Camera.targetTexture = null; _target.Release(); Object.DestroyImmediate(_target); Object.DestroyImmediate(_camera); Object.DestroyImmediate(_subject); }
        }

        static IsoFacetHullSetup Adapt(Mesh mesh, HullMeshDef.Ramp[] ramps, Vector3 light, float gain, float bias,
            float[] bayer, Color32 key, Vector2 pivot, int ppu, int w, int h, float elevation) => new IsoFacetHullSetup
        {
            Mesh = mesh, Ramps = ramps.Select(r => r.Colors).ToArray(), RampOffsets = ramps.Select(r => r.Offset).ToArray(),
            RampDitherWeights = ramps.Select(r => r.DitherWeight).ToArray(), LightN = light, Gain = gain, Bias = bias,
            Bayer16 = bayer, Keyline = key, PivotPx = pivot, PxPerMetre = ppu, CellW = w, CellH = h, ElevationDeg = elevation,
        };
        static bool Border(byte[] a, byte[] b)
        {
            for (int x = 0; x < Width; x++) if (PixelChanged(a, b, x) || PixelChanged(a, b, (Height - 1) * Width + x)) return true;
            for (int y = 0; y < Height; y++) if (PixelChanged(a, b, y * Width) || PixelChanged(a, b, y * Width + Width - 1)) return true;
            return false;
        }
        static bool PixelChanged(byte[] a, byte[] b, int pixel)
        { int i = pixel * 4; return a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3]; }
        static int Changed(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) throw new InvalidDataException("Image sizes differ.");
            int count = 0; for (int i = 0; i < a.Length / 4; i++) if (PixelChanged(a, b, i)) count++; return count;
        }
        static void SavePng(string path, byte[] rgba, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try { texture.LoadRawTextureData(rgba); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
        }
        static byte[] LoadPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try { if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path))) throw new InvalidDataException(path); return texture.GetRawTextureData<byte>().ToArray(); }
            finally { Object.DestroyImmediate(texture); }
        }
        static string Relative(string path) => path.Substring(Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1).Replace('\\', '/');
        static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
