#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Slices the St Peters set-piece sheets from the bake's contract, one sprite per cell, each with its
    /// OWN pivot.
    ///
    /// <para><b>Why per-cell pivots.</b> The sheets are a lattice: every cell the size of the largest frame
    /// box, and each frame keeps the ground point the rig gave it inside its own box. The nav-buoy kit
    /// instead crops every facing to one union around one shared pivot; here that union makes the trolley
    /// line's cell 788×517, which fits no grid under the 2048 import cap. Per-frame pivots bring it to
    /// 395×265 (4×2, 1580×530) and cost nothing at runtime: a sprite's pivot is per sprite anyway.</para>
    ///
    /// <para><b>The contract lives here, not in the baker</b>, because rule 4 lets RigBaking.Editor see
    /// Art.Editor and never the reverse. The baker writes this type with JsonUtility; the slicer and the
    /// Def builder read it back.</para>
    /// </summary>
    public static class SetPieceSheetSlicer
    {
        public const string SheetFolder = "Assets/_Project/Art/Sprites/StPeters/SetPieces";
        public const string ContractPath = SheetFolder + "/stPetersSetPieces.contract.json";

        /// <summary>The bake's input: the options each piece is baked at (committed, generated on
        /// Node from the key scenes' today variant).</summary>
        public const string TodayPath = SheetFolder + "/stPetersSetPieces.today.json";

        public const string ContractSchema = "hidden-harbours/st-peters-set-pieces-contract@1";
        public const string SheetPrefix = "SetPiece_";

        public static string SheetName(string key) => SheetPrefix + key;

        public static string SheetPath(string key) => $"{SheetFolder}/{SheetName(key)}.png";

        public static string SpriteName(string key, int cell) => $"{SheetName(key)}_{cell}";

        // ---- the contract, as JsonUtility sees it --------------------------------------------------------

        /// <summary>One cell: where it was cut from the rig's own cell, and where its ground point sits in
        /// the lattice cell (top-left pixels). <c>heading</c> is −1 for a facing, else the step.</summary>
        [Serializable]
        public sealed class Frame
        {
            public int cell, rigDir, heading;
            public int cropX, cropY, pivotX, pivotY;
            public int nativeW, nativeH, nativePivotX, nativePivotY;
            public int opaque;
        }

        [Serializable]
        public sealed class Piece
        {
            public string key, id, sheet;
            public int cells, headings, cellW, cellH, cols, rows, sheetW, sheetH;
            public List<Frame> frames = new List<Frame>();

            /// <summary>The options the kit resolved for this bake, as JSON.</summary>
            public string optionsJson;
        }

        [Serializable]
        public sealed class Contract
        {
            public string schema;
            public int importSizeCap, facings;
            public List<Piece> pieces = new List<Piece>();

            public string kitVersion;
            public string todaySha256Lf;
            public string skyJson;
            public float bakedLit;
            public bool outline;
            public float pixelsPerMetre, elevationDeg;
        }

        /// <summary>The contract, or null with the reason. JsonUtility returns a ZEROED object rather than
        /// throwing on a shape mismatch, so every field that matters is asserted.</summary>
        public static Contract LoadContract(out string error)
        {
            error = null;
            string abs = Path.Combine(RepoRoot, ContractPath);
            if (!File.Exists(abs))
            {
                error = $"No contract at '{ContractPath}'. Run \"Hidden Harbours/Art/Bake St Peters Set " +
                        "Pieces (today)\" first.";
                return null;
            }

            var c = JsonUtility.FromJson<Contract>(File.ReadAllText(abs));
            if (c == null || c.schema != ContractSchema)
                error = $"'{ContractPath}' is not a {ContractSchema} document.";
            else if (c.importSizeCap <= 0 || c.facings <= 0)
                error = $"'{ContractPath}' has no importSizeCap or facings; refusing to guess either.";
            else if (c.pieces == null || c.pieces.Count == 0)
                error = $"'{ContractPath}' parsed to no pieces.";
            else
                foreach (var p in c.pieces)
                {
                    error = Validate(p, c);
                    if (error != null) break;
                }
            return error == null ? c : null;
        }

        static string Validate(Piece p, Contract c)
        {
            if (string.IsNullOrEmpty(p.key) || p.sheet != SheetName(p.key))
                return $"piece '{p.key}' names sheet '{p.sheet}'.";
            if (p.cells != c.facings + p.headings || p.frames == null || p.frames.Count != p.cells)
                return $"{p.key}: {p.frames?.Count ?? 0} frames for {p.cells} cells ({c.facings} facings + " +
                       $"{p.headings} headings).";
            if (p.cols * p.rows != p.cells || p.sheetW != p.cols * p.cellW || p.sheetH != p.rows * p.cellH)
                return $"{p.key}: a {p.cols}×{p.rows} plan of {p.cellW}×{p.cellH} is not {p.sheetW}×{p.sheetH} " +
                       $"for {p.cells} cells.";
            if (Mathf.Max(p.sheetW, p.sheetH) > c.importSizeCap)
                return $"{p.key}: {p.sheetW}×{p.sheetH} is over the {c.importSizeCap} px cap.";
            for (int i = 0; i < p.frames.Count; i++)
            {
                var f = p.frames[i];
                if (f.cell != i) return $"{p.key}: frame {i} says it is cell {f.cell}.";
                if (f.pivotX != f.nativePivotX - f.cropX || f.pivotY != f.nativePivotY - f.cropY)
                    return $"{p.key} cell {i}: pivot ({f.pivotX},{f.pivotY}) is not the native pivot less the crop.";
                if (f.pivotX < 0 || f.pivotY < 0 || f.pivotX >= p.cellW || f.pivotY >= p.cellH)
                    return $"{p.key} cell {i}: pivot ({f.pivotX},{f.pivotY}) is outside its {p.cellW}×{p.cellH} cell.";
            }
            return null;
        }

        // ---- entry points ------------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Slice St Peters Set Piece Sheets", priority = 219)]
        public static void SliceAllMenu()
        {
            var problems = new List<string>();
            int n = SliceAll(problems);
            if (problems.Count > 0)
                Debug.LogError($"[SetPieceSheetSlicer] Sliced {n}; {problems.Count} problem(s):\n  " +
                               string.Join("\n  ", problems));
            else
                Debug.Log($"[SetPieceSheetSlicer] Sliced {n} sheet(s).");
        }

        /// <summary>Headless entry: exits 1 on any problem, so a batch run cannot read green by default.</summary>
        public static void SliceAllFromCommandLine()
        {
            var problems = new List<string>();
            int n = SliceAll(problems);
            Debug.Log($"[SetPieceSheetSlicer] Sliced {n}, problems {problems.Count}.");
            foreach (string p in problems) Debug.LogError("[SetPieceSheetSlicer] " + p);
            EditorApplication.Exit(problems.Count > 0 ? 1 : 0);
        }

        /// <summary>
        /// Slices every sheet the contract names. Unlike the nav-buoy slicer, a missing sheet is a
        /// PROBLEM: every piece in this contract was baked, so none may be quietly skipped. Runs outside
        /// <c>StartAssetEditing</c>: each importer change reimports synchronously before the next read.
        /// </summary>
        public static int SliceAll(List<string> problems)
        {
            var contract = LoadContract(out string error);
            if (contract == null)
            {
                problems.Add(error);
                return 0;
            }

            int sliced = 0;
            foreach (var piece in contract.pieces)
                if (SliceOne(piece, contract.importSizeCap, problems)) sliced++;
            return sliced;
        }

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        static bool SliceOne(Piece piece, int importSizeCap, List<string> problems)
        {
            string assetPath = SheetPath(piece.key);
            if (!File.Exists(Path.Combine(RepoRoot, assetPath)))
            {
                problems.Add($"{piece.key}: no sheet at '{assetPath}'. Bake before slicing.");
                return false;
            }
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                problems.Add($"{piece.key}: '{assetPath}' has no TextureImporter (not imported yet?).");
                return false;
            }

            // The lock (ArtImportPipeline) is stamped on FIRST import only. A sheet off it is refused,
            // not re-stamped: someone set it by hand, and that is theirs to explain.
            if (importer.textureType != TextureImporterType.Sprite ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, ArtImportPipeline.PixelsPerUnit) ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.mipmapEnabled)
            {
                problems.Add($"{piece.key}: '{assetPath}' is off the import lock (Sprite, PPU " +
                             $"{ArtImportPipeline.PixelsPerUnit}, Point, Uncompressed, no mips).");
                return false;
            }

            // Cap FIRST, before anything reads the texture: a sheet over maxTextureSize imports
            // DOWNSCALED, silently, and the rects come back refitted with the pivots thrown away.
            int needed = Mathf.NextPowerOfTwo(Mathf.Max(piece.sheetW, piece.sheetH));
            if (needed > importSizeCap)
            {
                problems.Add($"{piece.key}: {piece.sheetW}×{piece.sheetH} needs a {needed} px import, over " +
                             $"the {importSizeCap} px cap.");
                return false;
            }
            if (importer.maxTextureSize < needed)
            {
                importer.maxTextureSize = needed;
                importer.SaveAndReimport();
            }

            // Load AFTER any reimport above.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null || tex.width != piece.sheetW || tex.height != piece.sheetH)
            {
                problems.Add($"{piece.key}: '{assetPath}' is {(tex == null ? "unloadable" : tex.width + "×" + tex.height)}" +
                             $" but the contract plans {piece.sheetW}×{piece.sheetH}. Re-bake.");
                return false;
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // Re-use the spriteID an already-sliced name carries, so re-slicing unchanged art writes a
            // byte-identical .meta (the same fix as CharacterSheetSlicer, #218).
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            SpriteRect[] rects = BuildRects(piece, existingIds);
            dp.SetSpriteRects(rects);
            dp.GetDataProvider<ISpriteNameFileIdDataProvider>()
              ?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>Unity wants the pivot normalised from the BOTTOM-left; the rig reports it from the
        /// TOP-left. ADR 0026: the y term is (H − pivotY)/H.</summary>
        public static Vector2 NormalisedPivot(int cellW, int cellH, int pivotX, int pivotY) =>
            new Vector2(pivotX / (float)cellW, (cellH - pivotY) / (float)cellH);

        /// <summary>The lattice rects, row-major from the top-left; each cell's pivot is its own.</summary>
        public static SpriteRect[] BuildRects(Piece piece, IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            var rects = new SpriteRect[piece.cells];
            for (int i = 0; i < piece.cells; i++)
            {
                var f = piece.frames[i];
                int col = i % piece.cols, rowFromTop = i / piece.cols;
                string name = SpriteName(piece.key, i);
                rects[i] = new SpriteRect
                {
                    name = name,
                    spriteID = existingIds != null && existingIds.TryGetValue(name, out GUID id)
                        ? id : GUID.Generate(),
                    rect = new Rect(col * piece.cellW, piece.sheetH - (rowFromTop + 1) * piece.cellH,
                                    piece.cellW, piece.cellH),
                    alignment = SpriteAlignment.Custom,
                    pivot = NormalisedPivot(piece.cellW, piece.cellH, f.pivotX, f.pivotY),
                    border = Vector4.zero,
                };
            }
            return rects;
        }
    }
}
#endif
