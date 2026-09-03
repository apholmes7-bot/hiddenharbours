using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Builds the <see cref="DriftWeedKit"/> — the table that finally connects the painted drift-weed
    /// art to the running <see cref="SeaweedPresenter"/>. Reads the kit's own sidecar for the one fact
    /// the sprites cannot carry (<b>the size each clump was drawn at</b>), pairs it with the sliced
    /// sprites, and writes the asset the beds point at.
    ///
    /// <para><b>Why a tool and not a hand-authored asset.</b> 4 species × 3–4 variants × 3 ramp rows is
    /// 42 sprite references, each of which has to line up with the right <c>sizeM</c>. Hand-wiring that
    /// in the inspector is 42 chances to put a 2 m sugar kelp on a 0.45 m row, and nothing downstream
    /// would say so — it would just look wrong. Generated from the sidecar, a re-drop of the art
    /// re-derives the table (ADR 0021 §4's principle: the facts come from the kit, not from a
    /// hand-maintained list).</para>
    ///
    /// <para><b>Run order:</b> Slice Drift Weed Sheets → Build Drift Weed Kit. The slicer stamps each
    /// variant's buoyancy-centre pivot; this reads the sprites it produced. Running out of order fails
    /// loudly on the missing slices rather than writing a half-table.</para>
    /// </summary>
    public static class DriftWeedKitBuilder
    {
        public const string KitAssetPath = "Assets/_Project/Data/Decor/DriftWeedKit.asset";

        /// <summary>Ramp rows, top to bottom — must agree with <c>DriftWeedSheetSlicer.RampRows</c>.</summary>
        private const int RampRows = 3;

        // ---- the sidecar model -------------------------------------------------------------------
        //
        // Mirrors DriftWeedSheetSlicer's model deliberately, including the closed species set as FIELDS
        // (JsonUtility cannot read a dictionary). Two tools reading one contract two different ways is
        // how they drift apart. The additions are params.sizeM (the drawn size) and, since round 2, the
        // per-variant anchors — buoy / snags[] / dragTail, each a cell pixel — which the slicer reads
        // only for the buoy pivot.

        [Serializable] private class Params { public float sizeM; }
        [Serializable] private class Anchor { public int[] px; public float[] m; }
        [Serializable] private class Variant
        {
            public int cell;
            public Params @params;
            public Anchor buoy;
            public Anchor[] snags;
            public Anchor dragTail;
        }

        [Serializable]
        private class Species
        {
            public string sheet;
            public int cellW, cellH, cols, rows;
            public Variant[] variants;
        }

        [Serializable]
        private class SpeciesSet
        {
            public Species Bladderwrack, SugarKelp, Eelgrass, TornMat;
        }

        [Serializable]
        private class Sidecar
        {
            public SpeciesSet species;
            public string[] ramp_rows;
            public string derivedFromRigSha256;
            public float scale_px_per_m;
            public float y_foreshorten;
        }

        /// <summary>
        /// A sidecar anchor as the RUNTIME needs it: metres from the sprite pivot (the variant's buoy
        /// pixel, which the slicer stamped as the pivot) at scale 1, +y up — cell pixels over the
        /// sidecar's pixels-per-metre, y flipped because cell pixels count down the screen and Unity's
        /// sprite frame counts up. The sidecar's own <c>m</c> is deliberately NOT used: it is plane
        /// metres (y ÷ y_foreshorten), the rig's geometry, and a flat sprite's frond tip sits where its
        /// pixel is, not where the un-foreshortened plane point would be (ADR 0042: the squash is baked
        /// into the pixels). Null when the anchor is missing or lies outside the cell.
        /// </summary>
        private static Vector2? AnchorLocal(Anchor a, Anchor buoy, float pixelsPerMeter, int cellW, int cellH)
        {
            if (a?.px == null || a.px.Length != 2 || buoy?.px == null || buoy.px.Length != 2) return null;
            if (a.px[0] < 0 || a.px[0] >= cellW || a.px[1] < 0 || a.px[1] >= cellH) return null;
            return new Vector2((a.px[0] - buoy.px[0]) / pixelsPerMeter,
                               -(a.px[1] - buoy.px[1]) / pixelsPerMeter);
        }

        /// <summary>Species in sidecar order — the order <see cref="DriftWeedKit.Species"/> records,
        /// so <c>Entry.SpeciesIndex</c> means something stable.</summary>
        private static IEnumerable<(string name, Species sp)> Ordered(SpeciesSet s) => new[]
        {
            ("Bladderwrack", s.Bladderwrack), ("SugarKelp", s.SugarKelp),
            ("Eelgrass", s.Eelgrass), ("TornMat", s.TornMat),
        };

        // ---- entry points ------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Build Drift Weed Kit", priority = 234)]
        public static void BuildMenu()
        {
            if (Build(out string report)) Debug.Log(report);
            else Debug.LogError(report);
        }

        /// <summary>Batch entry point for <c>-executeMethod</c>; exits non-zero on failure.</summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                if (Build(out string report)) { Debug.Log(report); return; }
                Debug.LogError(report);
                EditorApplication.Exit(1);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DriftWeedKitBuilder] threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---- the work ----------------------------------------------------------------------------

        /// <summary>
        /// Rebuilds <see cref="KitAssetPath"/> from the sidecar + sliced sprites, then points every
        /// <see cref="SeaweedDef"/> that has no art yet at it. Returns false with a diagnostic on any
        /// missing slice — a partial table would render some clumps and silently blob the rest.
        /// </summary>
        public static bool Build(out string report)
        {
            string sidecarPath = DriftWeedSheetSlicer.SidecarPath;
            if (!File.Exists(sidecarPath))
            {
                report = $"[DriftWeedKitBuilder] No sidecar at '{sidecarPath}' — it carries the drawn " +
                         "sizes, so nothing can be built without it.";
                return false;
            }

            var sidecar = JsonUtility.FromJson<Sidecar>(File.ReadAllText(sidecarPath));
            if (sidecar?.species == null)
            {
                report = $"[DriftWeedKitBuilder] '{sidecarPath}' parsed but carries no species block.";
                return false;
            }

            float pixelsPerMeter = sidecar.scale_px_per_m > 0f ? sidecar.scale_px_per_m : 32f;
            float yForeshorten = sidecar.y_foreshorten > 0f ? sidecar.y_foreshorten : 0.72f;

            var speciesNames = new List<string>();
            var entries = new List<DriftWeedKit.Entry>();
            var problems = new List<string>();
            var perSpecies = new List<string>();

            foreach (var (name, sp) in Ordered(sidecar.species))
            {
                if (sp == null)
                {
                    problems.Add($"{name}: absent from the sidecar (a species was renamed or dropped — " +
                                 "this tool's closed species set needs updating alongside the slicer's).");
                    continue;
                }
                if (sp.variants == null || sp.variants.Length == 0)
                {
                    problems.Add($"{name}: no variants.");
                    continue;
                }
                if (sp.rows != RampRows)
                {
                    problems.Add($"{name}: declares {sp.rows} rows, expected {RampRows} ramp rows.");
                    continue;
                }

                string stem = Path.GetFileNameWithoutExtension(sp.sheet);
                string texPath = DriftWeedSheetSlicer.DriftRoot + sp.sheet;

                // ⚠️ The sheets import as spriteMode Multiple, so LoadAssetAtPath<Sprite> returns null.
                // Every sub-sprite comes back only from LoadAllAssetsAtPath.
                var byName = AssetDatabase.LoadAllAssetsAtPath(texPath)
                                          .OfType<Sprite>()
                                          .ToDictionary(s => s.name, s => s);
                if (byName.Count == 0)
                {
                    problems.Add($"{name}: '{texPath}' has no sliced sprites — run " +
                                 "Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Slice Drift " +
                                 "Weed Sheets first.");
                    continue;
                }

                int speciesIndex = speciesNames.Count;
                speciesNames.Add(name);
                int added = 0;

                for (int r = 0; r < RampRows; r++)
                {
                    foreach (var v in sp.variants)
                    {
                        // Index by the variant's OWN cell field, never array order — the slicer laid
                        // the sheet out that way and this must match it exactly.
                        if (v.cell < 0 || v.cell >= sp.cols)
                        {
                            problems.Add($"{name}: variant cell {v.cell} outside 0..{sp.cols - 1}.");
                            continue;
                        }

                        string spriteName = $"{stem}_{r * sp.cols + v.cell}";
                        if (!byName.TryGetValue(spriteName, out var sprite))
                        {
                            problems.Add($"{name}: no sliced sprite '{spriteName}' — re-slice the sheet.");
                            continue;
                        }

                        float sizeM = v.@params?.sizeM ?? 0f;
                        if (sizeM <= 0f)
                        {
                            problems.Add($"{name} cell {v.cell}: params.sizeM is {sizeM}. The drawn size " +
                                         "is what lets the presenter avoid rescaling the art; it cannot " +
                                         "be defaulted.");
                            continue;
                        }

                        // The drawn size and the anchors are metre-true at scale 1 ONLY if the sheet
                        // imports at the sidecar's own pixels-per-metre. Check it where the number is.
                        if (Mathf.Abs(sprite.pixelsPerUnit - pixelsPerMeter) > 1e-3f)
                        {
                            problems.Add($"{name}: '{spriteName}' imports at {sprite.pixelsPerUnit} PPU " +
                                         $"but the sidecar's scale_px_per_m is {pixelsPerMeter} — at " +
                                         "scale 1 neither the drawn size nor the anchors would be metres.");
                            continue;
                        }

                        // The anchors (round 2) — every variant must carry ≥1 snag tip and a drag tail;
                        // a clump with none would silently fall back to the round-1 radius rest.
                        var snags = new List<Vector2>(3);
                        if (v.snags != null)
                            foreach (var s in v.snags)
                            {
                                Vector2? local = AnchorLocal(s, v.buoy, pixelsPerMeter, sp.cellW, sp.cellH);
                                if (local.HasValue) snags.Add(local.Value);
                                else problems.Add($"{name} cell {v.cell}: a snag anchor is missing its px or lies outside the {sp.cellW}×{sp.cellH} cell.");
                            }
                        if (snags.Count == 0)
                        {
                            problems.Add($"{name} cell {v.cell}: no snag anchors — the rig publishes 2–3 " +
                                         "frond tips per variant; without them the clump cannot hook a line.");
                            continue;
                        }
                        Vector2? tail = AnchorLocal(v.dragTail, v.buoy, pixelsPerMeter, sp.cellW, sp.cellH);
                        if (!tail.HasValue)
                        {
                            problems.Add($"{name} cell {v.cell}: no dragTail anchor (or it lies outside the cell).");
                            continue;
                        }

                        entries.Add(new DriftWeedKit.Entry
                        {
                            Sprite = sprite,
                            SizeMeters = sizeM,
                            SpeciesIndex = speciesIndex,
                            RampIndex = r,
                            VariantCell = v.cell,
                            Snags = snags.ToArray(),
                            DragTail = tail.Value,
                        });
                        added++;
                    }
                }
                perSpecies.Add($"{name} ×{added} ({string.Join("/", sp.variants.Select(v => v.@params?.sizeM ?? 0f))} m × {RampRows} rows)");
            }

            if (problems.Count > 0)
            {
                report = "[DriftWeedKitBuilder] refusing to write a partial kit:\n  " +
                         string.Join("\n  ", problems);
                return false;
            }
            if (entries.Count == 0)
            {
                report = "[DriftWeedKitBuilder] nothing to write — no entries resolved.";
                return false;
            }

            var kit = AssetDatabase.LoadAssetAtPath<DriftWeedKit>(KitAssetPath);
            bool created = kit == null;
            if (created)
            {
                kit = ScriptableObject.CreateInstance<DriftWeedKit>();
                AssetDatabase.CreateAsset(kit, KitAssetPath);
            }

            kit.Species = speciesNames.ToArray();
            kit.Ramps = sidecar.ramp_rows != null && sidecar.ramp_rows.Length > 0
                ? sidecar.ramp_rows : new[] { "living", "golden", "bleached" };
            kit.Entries = entries.ToArray();
            kit.BuiltFromRigSha256 = sidecar.derivedFromRigSha256 ?? "";
            kit.PixelsPerMeter = pixelsPerMeter;
            kit.YForeshorten = yForeshorten;
            kit.InvalidateViews();
            EditorUtility.SetDirty(kit);

            int wired = WireBedsWithoutArt(kit, out var wiredNames);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report = $"[DriftWeedKitBuilder] {(created ? "Created" : "Rebuilt")} '{KitAssetPath}': " +
                     $"{entries.Count} clumps across {speciesNames.Count} species " +
                     $"({string.Join(", ", kit.Ramps)}).\n  " + string.Join("\n  ", perSpecies) +
                     (wired > 0 ? $"\n  Wired {wired} bed(s) that had no art: {string.Join(", ", wiredNames)}"
                                : "\n  All beds already reference a kit (none re-pointed).");
            return true;
        }

        /// <summary>
        /// Points every <see cref="SeaweedDef"/> with no <c>WeedArt</c> at the kit. Beds that already
        /// name a kit are left alone — an owner who deliberately pointed a bed elsewhere (or cleared it
        /// back to greybox to compare) keeps that choice.
        /// </summary>
        private static int WireBedsWithoutArt(DriftWeedKit kit, out List<string> names)
        {
            names = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:SeaweedDef"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<SeaweedDef>(path);
                if (def == null || def.WeedArt != null) continue;

                def.WeedArt = kit;
                EditorUtility.SetDirty(def);
                names.Add(def.name);
            }
            return names.Count;
        }
    }
}
