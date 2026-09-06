using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Turns every <c>*.sailing.json</c> in <c>docs/art/rigs/gameplay/</c> into a
    /// <see cref="SailPolarDef"/> asset — content as data (ADR 0003), nothing transcribed.
    ///
    /// <para>The same shape as <see cref="DeckSidecarImporter"/>, and deliberately: one sweep, one
    /// asset per sidecar, a refusal counted and named rather than a silent skip, and the rig's
    /// staleness hash enforced on every read. If the hull is reshaped, the polar refuses to import
    /// rather than shipping speeds cut from a different boat.</para>
    ///
    /// <para><b>⚠️ It writes the PROVENANCE, not just the numbers.</b> The kit's polar flags itself
    /// <c>REFERENCE, art-side</c>; that sentence lands verbatim on
    /// <see cref="SailPolarDef.StatusNote"/> and sets <see cref="SailPolarStatus.Reference"/>. A
    /// table of speeds with its provenance stripped is indistinguishable from a measurement, and the
    /// first consumer to read it will treat it as one.</para>
    ///
    /// <para><b>⚠️ It also COUNTS the disagreement between the two no-go angles</b> rather than
    /// leaving it as prose. The polar's model returns zero below twa 35; the rig draws flogging
    /// sails below awa 25. <see cref="SailPolarDef.IronsRowCount"/> is the number of cells in the
    /// shipped grid where the polar gives a speed and the sprite says she is in irons — measured
    /// from the rows, so the owner's ruling has a size attached to it.</para>
    /// </summary>
    public static class SailPolarImporter
    {
        const string SidecarFolder = "docs/art/rigs/gameplay";
        const string PolarFolder = "Assets/_Project/Data/Boats/SailPolars";

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        [MenuItem("Hidden Harbours/Dev/Import Sail Polars", priority = 226)]
        public static void ImportMenu() => Debug.Log(Import());

        /// <summary>Headless entry (-executeMethod).</summary>
        public static void ImportCli()
        {
            try
            {
                string log = Import();
                Debug.Log(log);
                EditorApplication.Exit(log.Contains("✗") ? 1 : 0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[sail-polar] CLI import FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        public static string Import()
        {
            var log = new StringBuilder("[sail-polar] import\n");
            string root = RepoRoot;
            string dir = Path.Combine(root, SidecarFolder);
            if (!Directory.Exists(dir)) return log.Append($"  sidecar folder not found: {dir}").ToString();

            EnsureFolder(PolarFolder);

            string[] files = Directory.GetFiles(dir, "*.sailing.json").OrderBy(f => f, StringComparer.Ordinal).ToArray();
            int wrote = 0, refused = 0;

            foreach (string file in files)
            {
                string who = Path.GetFileName(file);
                string stem = who.Replace(".sailing.json", "");
                string json = File.ReadAllText(file);

                // The rig is read from the FILE, not from the name, and resolved flat-then-tree —
                // the same two rules the deck importer follows, for the same two reasons.
                string rigFile = DeckSidecarJson.String(
                    DeckSidecarJson.Member(SafeParse(json), "rig")) ?? (stem + ".js");
                string rigPath = DeckSidecarReader.ResolveRigPath(root, rigFile);
                if (rigPath == null)
                {
                    refused++;
                    log.Append($"  ✗ {stem}: the rig it names ({rigFile}) is nowhere under docs/art/rigs.\n");
                    continue;
                }

                SailingSidecarRead read = SailingSidecarReader.Read(
                    json, $"{SidecarFolder}/{who}", File.ReadAllBytes(rigPath));
                if (!read.Ok)
                {
                    refused++;
                    log.Append($"  ✗ {stem}: {string.Join(" | ", read.Errors)}\n");
                    continue;
                }
                foreach (string note in read.Notes) log.Append($"  · {stem}: {note}\n");

                SailPolarDef def = Write(stem, read, out string assetPath);
                wrote++;
                log.Append($"  ✓ {stem} → {assetPath}: {def.TrueWindAngleDeg.Length}×{def.TrueWindKn.Length} " +
                           $"= {def.RowCount} cells, {def.IronsRowCount} of them the sprite calls IN IRONS " +
                           $"({(def.RowCount > 0 ? 100f * def.IronsRowCount / def.RowCount : 0f):0.#}%), " +
                           $"status {def.Status}, hull speed {def.HullSpeedKn:0.00} kn.\n");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            log.Append($"  {wrote} written, {refused} refused, of {files.Length} sailing sidecar(s).");
            return log.ToString();
        }

        static object SafeParse(string json)
        {
            try { return DeckSidecarJson.Parse(json); }
            catch (Exception) { return null; }
        }

        static SailPolarDef Write(string stem, SailingSidecarRead read, out string assetPath)
        {
            string name = char.ToUpperInvariant(stem[0]) + stem.Substring(1);
            assetPath = $"{PolarFolder}/{name}.asset";

            SailPolarDef def = AssetDatabase.LoadAssetAtPath<SailPolarDef>(assetPath);
            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<SailPolarDef>();

            // ⚠️ The id is set only on CREATION. Ids are append-only and stable (rule 2), so a
            // re-import must never rename an asset something already points at.
            if (created) def.Id = "sailpolar." + ToSnake(read.ExportSymbol.Length > 0 ? read.ExportSymbol : name);
            def.DisplayName = read.RigFileName;

            def.Status = SailPolarStatus.Reference;
            def.StatusNote = read.PolarStatusNote;
            def.Model = read.PolarModel;
            def.SourceSidecarPath = read.SidecarPath;
            def.DerivedFromRigSha256 = read.ExpectedRigSha;

            def.TrueWindKn = read.PolarTrueWindKn.ToArray();
            def.TrueWindAngleDeg = read.PolarTrueWindAngleDeg.ToArray();

            int nA = def.TrueWindAngleDeg.Length, nW = def.TrueWindKn.Length, cells = nA * nW;
            def.BoatSpeedKn = new float[cells];
            def.ApparentWindAngleDeg = new float[cells];
            def.ApparentWindKn = new float[cells];
            def.HeelDeg = new float[cells];

            // Indexed by LOOKUP, not by row order. The rows happen to arrive angle-major, but a
            // polar that silently transposed itself because the writer's loop order changed would
            // read plausible speeds at every wrong angle — the failure would look like bad balance.
            var angleAt = new Dictionary<float, int>();
            for (int a = 0; a < nA; a++) angleAt[def.TrueWindAngleDeg[a]] = a;
            var windAt = new Dictionary<float, int>();
            for (int w = 0; w < nW; w++) windAt[def.TrueWindKn[w]] = w;

            int irons = 0, placed = 0;
            foreach (SailingPolarRow r in read.PolarRows)
            {
                if (!angleAt.TryGetValue(r.TrueWindAngleDeg, out int ai)) continue;
                if (!windAt.TryGetValue(r.TrueWindKn, out int wi)) continue;
                int i = ai * nW + wi;
                def.BoatSpeedKn[i] = r.BoatSpeedKn;
                def.ApparentWindAngleDeg[i] = r.ApparentWindAngleDeg;
                def.ApparentWindKn[i] = r.ApparentWindKn;
                def.HeelDeg[i] = r.HeelDeg;
                placed++;
                if (string.Equals(r.Mode, "in irons", StringComparison.Ordinal)) irons++;
            }

            def.RowCount = placed;
            def.IronsRowCount = irons;
            def.NoGoTrueWindDeg = read.NoGoTrueWindDeg;
            def.SpriteIronsApparentDeg = read.IronsApparentDeg;
            def.HullSpeedKn = read.HullSpeedKn;
            def.SailAreaDisplacementRatio = read.SailAreaDisplacementRatio;
            def.UpwindSailAreaM2 = read.UpwindSailAreaM2;

            if (created) AssetDatabase.CreateAsset(def, assetPath);
            EditorUtility.SetDirty(def);
            return def;
        }

        static string ToSnake(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsUpper(c))
                {
                    if (i > 0) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }
    }
}
