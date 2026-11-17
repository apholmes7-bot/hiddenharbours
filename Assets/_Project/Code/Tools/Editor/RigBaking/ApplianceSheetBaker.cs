using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Bakes the bathroom and kitchen appliances</b> out of <c>appliance-kit/applianceIsoRig.js</c> (owner ask
    /// 2026-11-17) into one sheet per piece: a row per state (idle, the fill, the suds, the stream, the flush,
    /// and the near layers drawn in front of a figure using it), a column per facing. The table of rows is
    /// <see cref="ApplianceSheets"/>, shared with the runtime, so the two cannot drift.
    ///
    /// <para>Every row of a sheet is cropped to ONE rectangle (the union over all states and facings), so the
    /// pivot (the floor centre of the piece) is the same pixel in every sprite and a state change never makes
    /// the piece jump. The convention is measured against the room on the first row (the same refusal every
    /// interior prop bake makes); the other rows render on the same host and turntable.</para>
    ///
    /// <para><see cref="WirePrefabs"/> then hangs the art on the bathroom and kitchen prefabs: the tinted
    /// placeholder squares come off, an <see cref="ApplianceArt"/> goes on the root with a far and a near
    /// renderer. Scenes are not touched; they pick the change up through the prefabs.</para>
    /// </summary>
    public static class ApplianceSheetBaker
    {
        public const string RigKey = "appliance";
        public const string OutputFolder = "Assets/_Project/Art/Resources/" + ApplianceSheets.ResourcesFolder;
        public const string CompletionLine = "[ApplianceSheetBaker] DONE";

        /// <summary>The prefab each piece dresses, by kind (null: no prefab yet).</summary>
        public static readonly (ApplianceKind Kind, string Prefab)[] Prefabs =
        {
            (ApplianceKind.Bath, "Assets/_Project/Prefabs/Washrooms/Bath.prefab"),
            (ApplianceKind.Toilet, "Assets/_Project/Prefabs/Washrooms/Toilet.prefab"),
            (ApplianceKind.Basin, "Assets/_Project/Prefabs/Washrooms/WashStand.prefab"),
            (ApplianceKind.Shower, "Assets/_Project/Prefabs/Washrooms/Shower.prefab"),
            (ApplianceKind.WharfShower, "Assets/_Project/Prefabs/Washrooms/WharfShower.prefab"),
            (ApplianceKind.CookStove, "Assets/_Project/Prefabs/Props/Kitchen/Stove.prefab"),
            (ApplianceKind.PrepCounter, "Assets/_Project/Prefabs/Props/Kitchen/PrepCounter.prefab"),
            (ApplianceKind.Microwave, "Assets/_Project/Prefabs/Props/Kitchen/Microwave.prefab"),
        };

        /// <summary>The rig options that draw one row of a sheet.</summary>
        public static string OptsFor(in ApplianceLayout l, int row)
        {
            var parts = new List<string>();
            float fill = 0f; bool pour = false, near = false; int froth = 0, run = 0, flush = 0;

            if (row == l.FillLow) fill = 0.33f;
            if (row == l.FillMid) fill = l.FillLow >= 0 ? 0.66f : 0.5f;
            if (row == l.Full || row == l.NearFull) fill = 1f;
            if (row == l.PourLow) { fill = 0.33f; pour = true; }
            if (row == l.PourMid) { fill = l.PourLow >= 0 ? 0.66f : 0.5f; pour = true; }
            if (InLoop(row, l.FrothFirst)) { fill = 1f; froth = row - l.FrothFirst + 1; }
            if (InLoop(row, l.NearFrothFirst)) { fill = 1f; froth = row - l.NearFrothFirst + 1; near = true; }
            if (row == l.NearFull) near = true;
            if (InLoop(row, l.RunFirst)) run = row - l.RunFirst + 1;
            if (InLoop(row, l.NearRunFirst)) { run = row - l.NearRunFirst + 1; near = true; }
            if (InLoop(row, l.FlushFirst)) flush = row - l.FlushFirst + 1;

            if (fill > 0f) parts.Add("fill:" + fill.ToString("0.##", CultureInfo.InvariantCulture));
            if (pour) parts.Add("pour:true");
            if (froth > 0) parts.Add("froth:" + froth);
            if (run > 0) parts.Add("run:" + run);
            if (flush > 0) parts.Add("flush:" + flush);
            if (near) parts.Add("split:'near'");
            return "{" + string.Join(",", parts) + "}";
        }

        static bool InLoop(int row, int first) => first >= 0 && row >= first && row < first + ApplianceLayout.LoopFrames;

        // ---- the bake ----------------------------------------------------------------------------------------

        public sealed class SheetReport
        {
            public string Stem, AssetPath;
            public int Rows, CellWidth, CellHeight, PngBytes, PixelsPerUnit;
            public double PivotX, PivotY;
            public override string ToString() =>
                $"{Stem}: {Rows}×{ApplianceSheets.Facings} cells of {CellWidth}×{CellHeight} px, pivot " +
                $"({PivotX:0.#},{PivotY:0.#}), {PngBytes / 1024.0:F1} KiB → {AssetPath}";
        }

        public static List<SheetReport> BakeAll()
        {
            var reports = new List<SheetReport>();
            using IRigScriptHost host = RigScriptHostFactory.Create();
            foreach (ApplianceLayout layout in ApplianceSheets.All) reports.Add(BakeOne(host, layout));
            return reports;
        }

        public static SheetReport BakeOne(IRigScriptHost host, in ApplianceLayout l)
        {
            int facings = ApplianceSheets.Facings;

            // Row 0 through the interior bake: it installs the rig, refuses an unknown name, and MEASURES the
            // convention against the room. Every other row renders on the same host from there.
            var first = InteriorBakeRequest.PropOf(RigKey, l.RigName, OptsFor(l, 0), l.Stem + "_s0",
                                                   OutputFolder, l.Stem, facings, ApplianceSheetSlicer.ImportSizeCap);
            InteriorCellSet set = InteriorRigBaker.RenderCells(first, host);
            string g = RigCatalog.Get(RigKey).GlobalName;
            int w = set.Geometry.Width, h = set.Geometry.Height;

            var rows = new byte[l.Rows][][];
            rows[0] = set.Cells;
            for (int r = 1; r < l.Rows; r++)
            {
                var req = InteriorBakeRequest.PropOf(RigKey, l.RigName, OptsFor(l, r), $"{l.Stem}_s{r}",
                                                     OutputFolder, l.Stem, facings, ApplianceSheetSlicer.ImportSizeCap);
                rows[r] = new byte[facings][];
                for (int cell = 0; cell < facings; cell++)
                {
                    double dir = RigBaker.DirForCell(cell, facings, set.Convention);
                    rows[r][cell] = host.EvaluateBytes(req.RenderJs(g, dir));
                    if (rows[r][cell].Length != w * h * 4)
                        throw new InvalidOperationException($"{l.Stem} row {r} cell {cell} came back the wrong size.");
                }
            }

            // One crop for the whole sheet.
            var all = new List<byte[]>();
            foreach (byte[][] row in rows) all.AddRange(row);
            BuildingRigBaker.UnionAlphaBounds(all, w, h, out int x0, out int y0, out int x1, out int y1);
            if (x1 < x0) throw new InvalidOperationException($"{l.Stem} drew nothing in any state.");
            const int pad = 2;
            x0 = Mathf.Max(0, x0 - pad); y0 = Mathf.Max(0, y0 - pad);
            x1 = Mathf.Min(w - 1, x1 + pad); y1 = Mathf.Min(h - 1, y1 + pad);
            int cw = x1 - x0 + 1, ch = y1 - y0 + 1;

            int pw = facings * cw, ph = l.Rows * ch;
            if (pw > ApplianceSheetSlicer.ImportSizeCap || ph > ApplianceSheetSlicer.ImportSizeCap)
                throw new InvalidOperationException($"{l.Stem} packs to {pw}×{ph}, over the import cap.");

            var pixels = new Color32[pw * ph];
            for (int r = 0; r < l.Rows; r++)
            for (int cell = 0; cell < facings; cell++)
                BuildingRigBaker.BlitCropped(rows[r][cell], w, h, x0, y0, cw, ch, pixels, pw, ph, col: cell, rowFromTop: r);

            string assetPath = $"{OutputFolder}/{l.Stem}.png";
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, OutputFolder));
            var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, mipChain: false, linear: false);
            byte[] png;
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, assetPath), png);
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }

            return new SheetReport
            {
                Stem = l.Stem, AssetPath = assetPath, Rows = l.Rows, CellWidth = cw, CellHeight = ch,
                PivotX = set.Geometry.PivotX - x0, PivotY = set.Geometry.PivotY - y0, PngBytes = png.Length,
                PixelsPerUnit = set.PixelsPerMetre,
            };
        }

        public static void SliceAll(List<SheetReport> reports)
        {
            AssetDatabase.Refresh();
            foreach (SheetReport r in reports)
            {
                ApplianceLayout l = Find(r.Stem);
                if (!ApplianceSheetSlicer.Slice(r.AssetPath, r.Rows, ApplianceSheets.Facings, r.CellWidth, r.CellHeight,
                                                new Vector2((float)r.PivotX, (float)r.PivotY), r.PixelsPerUnit,
                                                (row, cell) => l.SpriteName(row, cell)))
                    throw new InvalidOperationException($"Slicing {r.Stem} failed; see above.");
            }
            AssetDatabase.SaveAssets();
        }

        static ApplianceLayout Find(string stem)
        {
            foreach (ApplianceLayout l in ApplianceSheets.All) if (l.Stem == stem) return l;
            throw new ArgumentException(stem);
        }

        // ---- the prefabs -------------------------------------------------------------------------------------

        /// <summary>The material and sorting layer the placeholder renderers used (kept for the new ones).</summary>
        const string SpriteLitMaterialGuid = "a97c105638bdf8b4a8650670310a4cd3";

        public static void WirePrefabs()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(SpriteLitMaterialGuid));
            foreach ((ApplianceKind kind, string path) in Prefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // Everything under the root was placeholder art (tinted squares); none of it carries logic.
                    int sortingLayer = 0;
                    for (int i = root.transform.childCount - 1; i >= 0; i--)
                    {
                        Transform child = root.transform.GetChild(i);
                        var sr = child.GetComponent<SpriteRenderer>();
                        if (sr != null) sortingLayer = sr.sortingLayerID;
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                    var rootSr = root.GetComponent<SpriteRenderer>();
                    if (rootSr != null) { sortingLayer = rootSr.sortingLayerID; UnityEngine.Object.DestroyImmediate(rootSr); }
                    var rootSort = root.GetComponent("YSortSprite") as Component;
                    if (rootSort != null) UnityEngine.Object.DestroyImmediate(rootSort);

                    SpriteRenderer far = NewRenderer(root.transform, "Art", material, sortingLayer);
                    SpriteRenderer near = NewRenderer(root.transform, "ArtNear", material, sortingLayer);
                    near.enabled = false;

                    // The idle, unturned picture, so the piece shows in the editor before Play picks its wall.
                    ApplianceLayout l = ApplianceSheets.For(kind);
                    far.sprite = ApplianceArt.SheetFor(l)[0];

                    var art = root.GetComponent<ApplianceArt>();
                    if (art == null) art = root.AddComponent<ApplianceArt>();
                    var so = new SerializedObject(art);
                    so.FindProperty("_kind").intValue = (int)kind;
                    so.FindProperty("_far").objectReferenceValue = far;
                    so.FindProperty("_near").objectReferenceValue = near;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }

        static SpriteRenderer NewRenderer(Transform parent, string name, Material material, int sortingLayer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (material != null) sr.sharedMaterial = material;
            sr.sortingLayerID = sortingLayer;
            return sr;
        }

        // ---- entry points ------------------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Bake Appliances (bathroom + kitchen)", priority = 73)]
        public static void BakeSliceWireMenu() => BakeSliceWire();

        /// <summary>The whole step, headless: <c>-executeMethod HiddenHarbours.Tools.RigBaking.ApplianceSheetBaker.BakeSliceWire</c>.</summary>
        public static void BakeSliceWire()
        {
            try
            {
                List<SheetReport> reports = BakeAll();
                SliceAll(reports);
                WirePrefabs();
                var sb = new StringBuilder(CompletionLine).AppendLine();
                foreach (SheetReport r in reports) sb.AppendLine("  " + r);
                Debug.Log(sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[ApplianceSheetBaker] FAILED: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

    }
}
