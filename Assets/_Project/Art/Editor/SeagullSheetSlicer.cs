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
    /// Slices the baked seagull sheet — the fleet's first CREATURE sheet — into its 384 named
    /// sprites. One page, every state: 48 columns (the rig's own <c>sheetOrder()</c>) × 8 facing
    /// rows of 64×64, i.e. 3072×512.
    ///
    /// <para><b>Every number is READ FROM THE COMMITTED CONTRACT</b> that <c>SeagullBaker</c> emits
    /// beside the PNG. Nothing about the grid is restated here, so a sheet and its slice cannot
    /// disagree unless the PNG on disk is stale — which is a dimension mismatch and fails loudly
    /// below rather than slicing garbage at the wrong stride.</para>
    ///
    /// <para><b>⚠️ Why the contract is re-declared below instead of imported.</b> The asmdef edge
    /// runs <c>HiddenHarbours.Tools.RigBaking.Editor → HiddenHarbours.Art.Editor</c>, never back, so
    /// this assembly cannot see <c>SeagullBaker</c>. Same reason <see cref="NavBuoySheetSlicer"/> and
    /// <see cref="IsoPackSheetSlicer"/> keep private mirrors of their contracts. The mirror is a
    /// READER of the committed JSON, not a second source of truth: every value still originates in
    /// the bake, and a shape drift shows up as one of the refusals in <see cref="SliceAll"/>.</para>
    ///
    /// <para><b>⚠️ THE CAP FIRST — this is the sheet the cap was written for.</b> Unity's default
    /// <c>maxTextureSize</c> is 2048 and this sheet is 3072 px wide, so on a fresh import it comes in
    /// DOWNSCALED unless the cap is lifted before anything reads the texture. The downscale is
    /// SILENT: the rects are authored in source pixels, the reimport refits them to the smaller
    /// texture, and they come back alpha-trimmed with the pivot thrown away — while the sprite COUNT
    /// still reads 384. The contract states the size it needs (<c>requiredMaxTextureSize</c>, 4096);
    /// it is not advice. This is also why <c>SeagullBaker</c> carries its own 4096-capped sheet
    /// writer rather than reusing <c>FishingKitBaker.WriteSheet</c>, whose cap is 2048.</para>
    ///
    /// <para><b>⚠️ THE PIVOT IS A CONTACT POINT, NOT A CENTRE.</b> (32,46) top-left in cell space —
    /// the ground under the bird when it stands, walks or perches, the water surface when it floats,
    /// and the point directly below it when airborne. It is read from the contract and converted by
    /// ADR 0026's rule (Unity's normalised pivot is bottom-origin: <c>y = (h − pivotY) / h</c>);
    /// getting that flip upside-down is an easy and silent mistake — the bird then hangs by its
    /// head. Sliding altitude into the pivot instead is the other one: <b>altitude is a screen
    /// OFFSET, never a scale</b>. Strict world scale at every altitude (32 px = 1 m; a 1.40 m span is
    /// 45 px on the wharf and 45 px at 25 m), the shadow stays at the pivot, the bird descends onto
    /// it. Owner's ruling of 2026-09-09 — there are no giant gulls crossing the camera.</para>
    ///
    /// <para><b>Slice names are <c>gull_&lt;state&gt;_&lt;frame&gt;_d&lt;row&gt;</c></b> — the order
    /// the charter names, which is NOT this repo's usual <c>&lt;Stem&gt;_d&lt;row&gt;_f&lt;col&gt;</c>
    /// (see <see cref="FishingSheetSlicer"/>). A creature's sheet is indexed by BEHAVIOUR first
    /// because that is how the state machine will ask for it; the facing is the suffix. The state and
    /// frame come from the contract's <c>order</c> table, never from a README, so a column cannot be
    /// mislabelled here without the bake being wrong first. <c>d&lt;row&gt;</c> is a facing INDEX,
    /// not a compass name: row <c>r</c> depicts heading <c>+45°·r</c> from north because this rig is
    /// CLOCKWISE — measured from the bill's own pixels by <c>SeagullRigAzimuthProbe</c>, and the
    /// minority convention in this repo. Nothing in the sheet says so; read it from the contract's
    /// <c>facingsAreCounterClockwise: false</c>. This lane has shipped mislabelled compass art five
    /// times.</para>
    ///
    /// <para><b>All 384 rects are always emitted, and all 384 are real art.</b> Measured at intake:
    /// 384/384 cells non-blank, and every anim's frames differ from their neighbours in all 8 rows.
    /// Do not collapse rows on a symmetry argument.</para>
    /// </summary>
    public static class SeagullSheetSlicer
    {
        /// <summary>⚠️ Must match <c>SeagullBaker.DefaultOutputFolder</c> / <c>.SheetName</c> /
        /// <c>.ContractFileName</c>. Duplicated only because the asmdef edge forbids the import
        /// (see remarks).</summary>
        public const string SheetFolder = "Assets/_Project/Art/Sprites/Creatures";

        public const string ContractPath = SheetFolder + "/Seagull.contract.json";

        /// <summary>The scale standard (canon, ADR 0005 / Art bible §9.2): 32 sprite-px = 1 m.</summary>
        public const float PixelsPerUnit = 32f;

        /// <summary>The facing rows the bake writes. A contract that says anything else is a drop
        /// this slicer has not read, and it refuses rather than slicing 8 rows out of it.</summary>
        public const int Directions = 8;

        // ---- the contract, as JsonUtility sees it -------------------------------------------------

        [Serializable]
        public sealed class ColumnEntry
        {
            public int col;
            public string anim;
            public int frame;
        }

        [Serializable]
        public sealed class CellSize
        {
            public int w, h;
        }

        [Serializable]
        public sealed class PivotPx
        {
            public float x, y;
        }

        [Serializable]
        public sealed class Contract
        {
            public string sheet;
            public CellSize cell;
            public PivotPx pivotTopLeft;
            public int rows, columns, pixelsPerUnit, requiredMaxTextureSize;
            public bool facingsAreCounterClockwise;
            public List<ColumnEntry> order = new List<ColumnEntry>();

            public int SheetWidth => columns * (cell?.w ?? 0);
            public int SheetHeight => rows * (cell?.h ?? 0);

            /// <summary>ADR 0026's conversion: a TOP-LEFT-origin contract pivot to Unity's
            /// BOTTOM-origin normalised pivot. The y flip happens exactly once, here.</summary>
            public Vector2 NormalisedPivot =>
                new Vector2(pivotTopLeft.x / cell.w, (cell.h - pivotTopLeft.y) / cell.h);
        }

        // ---- entry points ---------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Slice Seagull Sheet", priority = 213)]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int skipped, out int failed);
            Debug.Log($"[SeagullSheetSlicer] Sliced {n} sheet(s), skipped {skipped}, failed {failed}.");
        }

        /// <summary>Batch-mode entry: non-zero exit on any failure, so a headless bake cannot report
        /// green over a sheet that did not slice.</summary>
        public static void SliceAllFromCommandLine()
        {
            AssetDatabase.Refresh();
            int n = SliceAll(out int skipped, out int failed);
            Debug.Log($"[SeagullSheetSlicer] Sliced {n}, skipped {skipped}, failed {failed}.");
            if (failed > 0) EditorApplication.Exit(1);
        }

        /// <summary>
        /// Reads the committed contract, refuses anything it does not fully understand, and slices
        /// the sheet it names.
        ///
        /// <para>Every refusal below is a way this could otherwise half-succeed. <c>JsonUtility</c>
        /// returns a ZEROED object rather than throwing on a shape mismatch, so an empty
        /// <c>order</c>, a zero cell or a missing cap are all indistinguishable from a parse failure
        /// unless they are asserted — and a zero cell slices 384 empty rects onto a real texture.</para>
        /// </summary>
        public static int SliceAll(out int skipped, out int failed)
        {
            skipped = 0; failed = 0;

            string contractAbs = Path.Combine(RepoRoot, ContractPath);
            if (!File.Exists(contractAbs))
            {
                Debug.LogError($"[SeagullSheetSlicer] No contract at '{ContractPath}'. Run " +
                               "\"Hidden Harbours/Art/Bake Seagull\" first — the bake emits it beside " +
                               "the sheet, and this slicer takes every number from it.");
                failed++;
                return 0;
            }

            var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(contractAbs));
            if (!Validate(contract, out string why))
            {
                Debug.LogError($"[SeagullSheetSlicer] '{ContractPath}' {why} Not slicing.");
                failed++;
                return 0;
            }

            string sheetPath = string.IsNullOrEmpty(contract.sheet)
                               ? SheetFolder + "/Seagull.png" : contract.sheet;

            // A contract with no sheet beside it is NOT an error the same way a broken contract is:
            // the bake writes both, so an absent PNG means this checkout has the contract and not the
            // art (an LFS pointer left unfetched, say). Say which, and skip.
            if (!File.Exists(Path.Combine(RepoRoot, sheetPath)))
            {
                Debug.LogWarning($"[SeagullSheetSlicer] '{sheetPath}' is not on disk (contract is). " +
                                 "If this is a fresh clone, run `git lfs pull`. Skipping.");
                skipped++;
                return 0;
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                if (SliceSheet(sheetPath, contract)) return 1;
                failed++;
                return 0;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// The refusals, separated so the tests can drive them without a texture on disk. Returns
        /// false with a sentence naming what is wrong; the caller adds the file and "Not slicing".
        /// </summary>
        public static bool Validate(Contract contract, out string why)
        {
            if (contract == null) { why = "did not parse."; return false; }

            if (contract.cell == null || contract.cell.w <= 0 || contract.cell.h <= 0)
            {
                why = "has no cell size. A zero cell slices 384 empty rects onto a real texture, " +
                      "which imports clean and draws nothing.";
                return false;
            }
            if (contract.pivotTopLeft == null)
            {
                why = "has no pivot. Defaulting it to (0,0) would plant every gull by its top-left " +
                      "corner — the pivot is a CONTACT POINT and is not guessable.";
                return false;
            }
            if (contract.pivotTopLeft.x < 0 || contract.pivotTopLeft.x > contract.cell.w ||
                contract.pivotTopLeft.y < 0 || contract.pivotTopLeft.y > contract.cell.h)
            {
                why = $"places the pivot at ({contract.pivotTopLeft.x},{contract.pivotTopLeft.y}) " +
                      $"outside its own {contract.cell.w}×{contract.cell.h} cell.";
                return false;
            }
            if (contract.rows != Directions)
            {
                why = $"declares {contract.rows} facing rows, not {Directions}. This slicer names " +
                      "sprites d0..d7 and would silently drop or invent rows.";
                return false;
            }
            if (contract.columns <= 0 || contract.order == null || contract.order.Count == 0)
            {
                why = "parsed to no columns. JsonUtility zeroes an object it cannot shape rather " +
                      "than throwing, so this is what a moved contract looks like.";
                return false;
            }
            if (contract.order.Count != contract.columns)
            {
                why = $"says {contract.columns} columns but lists {contract.order.Count} in `order`. " +
                      "The two statements of the sheet's width must be the same statement.";
                return false;
            }
            if (contract.requiredMaxTextureSize <= 0)
            {
                why = "has no requiredMaxTextureSize. Reading a missing cap back as 0 would import " +
                      "this 3072 px sheet at Unity's 2048 default — silently downscaled, with the " +
                      "right sprite count.";
                return false;
            }
            if (contract.pixelsPerUnit > 0 &&
                !Mathf.Approximately(contract.pixelsPerUnit, PixelsPerUnit))
            {
                why = $"bakes at {contract.pixelsPerUnit} PPU against the canon {PixelsPerUnit}. " +
                      "Scale is the whole ruling here — a gull's pixels are its metres × 32 at every " +
                      "altitude.";
                return false;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < contract.order.Count; i++)
            {
                var c = contract.order[i];
                if (c == null || string.IsNullOrEmpty(c.anim))
                {
                    why = $"has no state name on column {i}.";
                    return false;
                }
                if (c.col != i)
                {
                    why = $"lists column index {c.col} in slot {i}. The `order` table IS the " +
                          "column→(state,frame) map; out of order it maps the wrong picture to the " +
                          "wrong name, and every cell still slices.";
                    return false;
                }
                if (c.frame < 0)
                {
                    why = $"gives column {i} ({c.anim}) frame {c.frame}.";
                    return false;
                }
                if (!names.Add($"{c.anim}_{c.frame}"))
                {
                    why = $"names ({c.anim}, frame {c.frame}) twice. Two columns sharing a sprite " +
                          "name is 8 rects Unity keeps only one of — a quietly short sheet.";
                    return false;
                }
            }

            why = null;
            return true;
        }

        public static bool SliceSheet(string assetPath, Contract contract)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                Debug.LogError($"[SeagullSheetSlicer] '{assetPath}' has no TextureImporter — skipping.");
                return false;
            }

            // ⚠️ THE CAP FIRST, before anything reads the texture. See the class remarks: over the
            // importer's maxTextureSize the sheet imports DOWNSCALED and SILENTLY, rects refitted and
            // alpha-trimmed with the pivot thrown away, and the sprite count still reads right.
            int needed = Mathf.NextPowerOfTwo(Mathf.Max(contract.SheetWidth, contract.SheetHeight));
            if (needed > contract.requiredMaxTextureSize)
            {
                Debug.LogError(
                    $"[SeagullSheetSlicer] '{assetPath}' is {contract.SheetWidth}×" +
                    $"{contract.SheetHeight} and needs a {needed} px import, over the contract's " +
                    $"{contract.requiredMaxTextureSize} px. Re-pack the bake into more rows rather " +
                    "than raising the cap. Not slicing.");
                return false;
            }
            if (importer.maxTextureSize < needed)
            {
                Debug.Log($"[SeagullSheetSlicer] '{assetPath}' is {contract.SheetWidth}×" +
                          $"{contract.SheetHeight} but the importer caps at " +
                          $"{importer.maxTextureSize} — raising maxTextureSize to {needed} so the " +
                          "sheet imports at native resolution.");
                importer.maxTextureSize = needed;
                importer.SaveAndReimport();
            }

            LockImportSettings(importer);

            // Load AFTER the reimport above — a mid-build import invalidates any texture read before it.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
            {
                Debug.LogError($"[SeagullSheetSlicer] '{assetPath}' failed to load as Texture2D.");
                return false;
            }
            if (tex.width != contract.SheetWidth || tex.height != contract.SheetHeight)
            {
                Debug.LogError(
                    $"[SeagullSheetSlicer] '{assetPath}' is {tex.width}×{tex.height} but its " +
                    $"contract plans {contract.SheetWidth}×{contract.SheetHeight} " +
                    $"({contract.columns}×{contract.rows} of {contract.cell.w}×{contract.cell.h}). " +
                    "Sheet and contract disagree — re-bake, or the import is still downscaled. " +
                    "Not slicing.");
                return false;
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // ⚠️ Re-use the spriteID any already-sliced name carries. Generating fresh GUIDs on every
            // run rewrites all 384 spriteIDs in the .meta — pure diff noise that buries real changes,
            // and re-slicing unchanged art should produce a byte-identical .meta. Same fix as
            // FishingSheetSlicer, CharacterSheetSlicer (#218) and IsoPackSheetSlicer.
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(gr => gr.Key, gr => gr.First().spriteID);

            var rects = BuildRects(contract, existingIds);
            dp.SetSpriteRects(rects);

            var nameIds = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIds?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[SeagullSheetSlicer] '{assetPath}' → {rects.Length} sprites " +
                      $"({contract.columns} columns × {contract.rows} facings), pivot " +
                      $"({contract.pivotTopLeft.x},{contract.pivotTopLeft.y}) top-left = " +
                      $"{contract.NormalisedPivot} normalised, {PixelsPerUnit} PPU, Point, no mips.");
            return true;
        }

        /// <summary>
        /// The locked import settings for this sheet: Sprite/Multiple, PPU 32, Point filter, no
        /// compression, no mips, alpha-is-transparency, Clamp.
        ///
        /// <para><b>Why restate what <see cref="ArtImportPipeline"/> already stamps.</b> That
        /// postprocessor applies the canon lock only when <c>importSettingsMissing</c> is true — the
        /// FIRST import, before any .meta exists — deliberately, so a later hand-tuned Inspector
        /// change survives. Which means the settings are guaranteed on a fresh clone and merely
        /// likely on this one. The charter asks for these settings IN THE COMMITTED META, so the
        /// slicer states them itself and the meta is the same either way.</para>
        /// </summary>
        public static void LockImportSettings(TextureImporter importer)
        {
            // Sprite-level fields go through TextureImporterSettings; read-modify-write so nothing
            // else in there is reset. Done BEFORE the direct properties so those win on any overlap.
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            s.sRGBTexture = true;
            s.alphaIsTransparency = true;
            s.spriteMeshType = SpriteMeshType.FullRect;   // predictable pixel-art quads
            s.spriteExtrude = 1;
            s.wrapMode = TextureWrapMode.Clamp;
            importer.SetTextureSettings(s);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;                            // crisp pixels, no AA
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
        }

        /// <summary>
        /// One rect per (facing row, column), named <c>gull_&lt;state&gt;_&lt;frame&gt;_d&lt;row&gt;</c>
        /// with the state and frame taken from the contract's <c>order</c> table.
        ///
        /// <para>The bake blits row-major from the TOP-LEFT cell; Unity's rects are BOTTOM-origin, so
        /// row 0 maps to the HIGHEST y. That flip is the one in <c>(rows − 1 − row)</c> — and it is a
        /// different flip from the pivot's, which lives in <see cref="Contract.NormalisedPivot"/>.
        /// Doing either twice looks like art that is nearly right.</para>
        /// </summary>
        public static SpriteRect[] BuildRects(Contract contract,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            Vector2 pivot = contract.NormalisedPivot;
            var rects = new List<SpriteRect>(contract.rows * contract.columns);

            for (int row = 0; row < contract.rows; row++)
                for (int col = 0; col < contract.columns; col++)
                {
                    var c = contract.order[col];
                    string name = SpriteName(c.anim, c.frame, row);
                    rects.Add(new SpriteRect
                    {
                        name = name,
                        spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                                   ? id : GUID.Generate(),
                        rect = new Rect(col * contract.cell.w,
                                        (contract.rows - 1 - row) * contract.cell.h,
                                        contract.cell.w, contract.cell.h),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                        border = Vector4.zero,
                    });
                }

            return rects.ToArray();
        }

        /// <summary>The one place the sprite name is spelled. <c>d</c> is a facing INDEX, not a
        /// compass name — row <c>r</c> is heading <c>+45°·r</c> on this CLOCKWISE rig.</summary>
        public static string SpriteName(string anim, int frame, int row) =>
            $"gull_{anim}_{frame}_d{row}";

        /// <summary>
        /// Verifies the sheet imported at native resolution and carries the rects the contract
        /// describes.
        ///
        /// <para><b>spriteMode Multiple is NOT the same as sliced.</b> A fresh import is Multiple
        /// with ZERO rects, and every downstream <c>LoadAllAssetsAtPath</c> then returns nothing
        /// while the importer reports exactly what you asked for. This is the check that tells the
        /// two apart.</para>
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Verify Seagull Slices", priority = 156)]
        public static void VerifyMenu()
        {
            if (Verify(out string report)) Debug.Log($"[SeagullSheetSlicer] {report}");
            else Debug.LogError($"[SeagullSheetSlicer] {report}");
        }

        public static bool Verify(out string report)
        {
            string contractAbs = Path.Combine(RepoRoot, ContractPath);
            if (!File.Exists(contractAbs)) { report = $"no contract at '{ContractPath}'."; return false; }

            var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(contractAbs));
            if (!Validate(contract, out string why)) { report = $"'{ContractPath}' {why}"; return false; }

            string sheetPath = string.IsNullOrEmpty(contract.sheet)
                               ? SheetFolder + "/Seagull.png" : contract.sheet;
            var problems = new List<string>();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            if (tex == null) problems.Add($"{sheetPath}: does not load as a Texture2D.");
            else if (tex.width != contract.SheetWidth || tex.height != contract.SheetHeight)
                problems.Add($"{sheetPath}: imported {tex.width}×{tex.height}, contract plans " +
                             $"{contract.SheetWidth}×{contract.SheetHeight} — the cap lift did not " +
                             "take, so the rects are refitted and the pivots are gone.");

            if (AssetImporter.GetAtPath(sheetPath) is TextureImporter imp)
            {
                if (imp.spriteImportMode != SpriteImportMode.Multiple)
                    problems.Add($"{sheetPath}: spriteImportMode is {imp.spriteImportMode}, not Multiple.");
                if (!Mathf.Approximately(imp.spritePixelsPerUnit, PixelsPerUnit))
                    problems.Add($"{sheetPath}: imported at {imp.spritePixelsPerUnit} PPU, want {PixelsPerUnit}.");
                if (imp.filterMode != FilterMode.Point)
                    problems.Add($"{sheetPath}: filterMode is {imp.filterMode}, not Point.");
                if (imp.mipmapEnabled) problems.Add($"{sheetPath}: mip maps are on.");
                if (imp.textureCompression != TextureImporterCompression.Uncompressed)
                    problems.Add($"{sheetPath}: compression is {imp.textureCompression}, not Uncompressed.");
            }
            else problems.Add($"{sheetPath}: no TextureImporter.");

            int want = contract.rows * contract.columns;
            var sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().ToList();
            if (sprites.Count != want)
                problems.Add($"{sheetPath}: {sprites.Count} sprites, want {want} " +
                             $"({contract.columns} × {contract.rows}). Multiple-mode with no rects " +
                             "reads as a correctly configured, empty sheet.");

            var byName = sprites.ToDictionary(sp => sp.name, sp => sp, StringComparer.Ordinal);
            Vector2 pivot = contract.NormalisedPivot;
            foreach (var c in contract.order)
                for (int row = 0; row < contract.rows; row++)
                {
                    string name = SpriteName(c.anim, c.frame, row);
                    if (!byName.TryGetValue(name, out var sp)) { problems.Add($"missing sprite '{name}'."); continue; }
                    Vector2 got = new Vector2(sp.pivot.x / sp.rect.width, sp.pivot.y / sp.rect.height);
                    if (!Mathf.Approximately(got.x, pivot.x) || !Mathf.Approximately(got.y, pivot.y))
                        problems.Add($"'{name}': pivot {got}, want {pivot}.");
                }

            report = problems.Count == 0
                ? $"{want} sprites on '{sheetPath}', all at pivot {pivot}, {PixelsPerUnit} PPU, " +
                  $"Point, no mips, imported at {contract.SheetWidth}×{contract.SheetHeight}."
                : $"{problems.Count} problem(s):\n  " + string.Join("\n  ", problems.Take(20));
            return problems.Count == 0;
        }

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;
    }
}
#endif
