using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Bakes a hull's paint schemes into <see cref="HullPaintSchemeDef"/> assets — ramp tables
    /// only, no geometry, no sheets.</b>
    ///
    /// <para><b>Why there is nothing to render here.</b> A rig mesh carries its material INDEX per
    /// vertex and resolves colour at draw time out of the def's ramp table, so a repaint is a table
    /// and the geometry never changes. MEASURED on the lobster boat's paint kit (V8 harness,
    /// 2026-08-12): her 676-face list is byte-identical with and without paint — every vertex to
    /// 1e-9 — and all 12 schemes share one alpha mask at every one of the 8 headings. The sheet road
    /// was priced before it was rejected: her sheet family is 30.6 M pixels, so a per-scheme re-bake
    /// is 116.9 MB of RGBA32 per paint job and 1.37 GB for twelve, against ~250 bytes each here.</para>
    ///
    /// <para><b>The table is READ from the rig, never transcribed.</b> Every ramp comes out of the
    /// rig's own resolver through the SAME JS expression <c>RigMeshExtractor.ReadMaterials</c> uses
    /// (<c>for (var k in MATS)</c>), so the key order — which is load-bearing, because ramps are
    /// matched to materials BY INDEX — is the rig's own enumeration order in both places. Reading a
    /// rig and declaring what its colours mean is precisely how this project has shipped miscoloured
    /// boats before; the one number typed in this file is the count it asserts.</para>
    ///
    /// <para><b>The default scheme is baked like any other, and that is the A/B control.</b>
    /// <c>gelcoat</c> goes through the same path as <c>harbour</c>, so
    /// <c>HullPaintSchemeBakeTests</c> can assert the baked gelcoat table equals the hull def's own
    /// ramps entry for entry. If that ever parts, "unset scheme = today's boat" has stopped being
    /// true and the bake says so instead of the game.</para>
    /// </summary>
    public static class HullPaintSchemeBaker
    {
        /// <summary>Where the baked tables live. One asset per scheme, one entity per file (rule 2).</summary>
        public const string SchemeFolder = "Assets/_Project/Data/Boats/PaintSchemes";

        /// <summary>
        /// A hull whose rig carries a paint axis. Deliberately a list of one: the lobster boat is the
        /// only hull whose rig has schemes today, and the day a second lands it is a line here, not a
        /// new baker. <see cref="AssetPrefix"/> keeps two hulls' tables from colliding on disk, and
        /// <see cref="IdPrefix"/> keeps their ids apart (ids are append-only and stable, CLAUDE.md §5).
        /// </summary>
        public readonly struct PaintedHull
        {
            public readonly string RigKey, HullMeshId, AssetPrefix, IdPrefix;

            /// <summary>Closure-private symbols this hull's resolver needs widened. Empty when the rig
            /// already exports everything (the punt and the console skiff export theirs).</summary>
            public readonly string[] ShimSymbols;

            /// <summary>
            /// JS returning ONE scheme's material table, with <c>{0}</c> replaced by the quoted
            /// scheme id. Parameterised because the two paint APIs in the repo differ and neither is
            /// wrong: the lobster kit resolves <c>matsFor(id).MATS</c>, while the 2026-07-25
            /// small-craft kit resolves <c>palette({{scheme:id}}).mats</c>. Adding a hull is then a
            /// line in <see cref="Fleet"/> rather than a second baker.
            /// </summary>
            public readonly string MatsExpr;

            /// <summary>JS returning the schemes in swatch order as <c>[id, label, note]</c> triples.
            /// One expression rather than an id list plus fallbacks, so a rig that labels its schemes
            /// differently needs no branch here.</summary>
            public readonly string SchemeListExpr;

            /// <summary>JS returning the rig's own default scheme id — the one the mesh bake is
            /// pinned to, and therefore the one whose table must equal the hull def's.</summary>
            public readonly string DefaultSchemeExpr;

            public PaintedHull(string rigKey, string hullMeshId, string assetPrefix, string idPrefix,
                               string matsExpr, string schemeListExpr, string defaultSchemeExpr,
                               string[] shimSymbols = null)
            {
                RigKey = rigKey; HullMeshId = hullMeshId; AssetPrefix = assetPrefix; IdPrefix = idPrefix;
                MatsExpr = matsExpr; SchemeListExpr = schemeListExpr; DefaultSchemeExpr = defaultSchemeExpr;
                ShimSymbols = shimSymbols ?? System.Array.Empty<string>();
            }
        }

        public static readonly PaintedHull[] Fleet =
        {
            // 12 schemes over the first mesh hull in the game. The ids are hull-qualified because the
            // TABLE is hull-specific (arity and material order must match the hull's own), even
            // though the COLOUR is not — the variants rig paints its whole range from this same
            // OKLCH table. RigPaintId on the asset carries the shared colour identity, so a second
            // hull's 'harbour' can be recognised as the same navy without the ids having to collide.
            new PaintedHull("lobsterBoat", "hullmesh.lobster_boat_iso", "LobsterBoatIso", "paint.lobster_",
                            matsExpr: "matsFor({0}).MATS",
                            schemeListExpr: "PAINTS.map(function(p){return [p.id,p.label||'',p.note||''];})",
                            defaultSchemeExpr: "defaultPaint",
                            shimSymbols: new[] { "matsFor" }),

            // NOT YET, and deliberately: the punt and the console skiff have carried a paint axis
            // since the 2026-07-25 small-craft kit and already EXPORT theirs (`SCHEMES`, `schemeIds`,
            // `defaultScheme`, `palette`), so they would need no shim at all —
            //   new PaintedHull("punt", "hullmesh.punt_iso", "PuntIso", "paint.punt_",
            //                   "palette({scheme:{0}}).mats",
            //                   "schemeIds.map(function(id){return [id,'',''];})", "defaultScheme")
            // — but their mesh bakes are currently PINNED to 'harbour-white' and flipping them is a
            // second hull's worth of A/B, not a free line. The Cape Islander has no paint axis at all
            // (a plain `MATS` const), so she cannot join until the art director gives her one.
        };

        [MenuItem("Hidden Harbours/Dev/3D Hulls/Bake hull PAINT SCHEMES…", priority = 41)]
        public static void BakeAll()
        {
            var log = new StringBuilder();
            int total = 0;
            try
            {
                foreach (var hull in Fleet) total += Bake(hull, log);
            }
            catch (Exception e)
            {
                Debug.LogError($"[HullPaintSchemeBaker] FAILED\n{log}\n{e}");
                throw;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[HullPaintSchemeBaker] {total} scheme(s) baked.\n{log}");
        }

        /// <summary>
        /// Bakes every scheme the hull's rig declares. Returns how many assets were written.
        /// </summary>
        public static int Bake(in PaintedHull hull, StringBuilder log)
        {
            if (!Directory.Exists(SchemeFolder)) Directory.CreateDirectory(SchemeFolder);

            var entry = RigCatalog.Get(hull.RigKey);
            string g = entry.GlobalName;

            using IRigScriptHost host = RigScriptHostFactory.Create();

            // The lobster rig exports PAINTS and paintRamps but keeps its resolver private, so widen
            // exactly the symbols this hull declares — the same in-memory, never-written shim the mesh
            // extractor uses. (The punt and the console export theirs and would declare none.)
            // Reading paintRamps and rebuilding the material table here would be a transcription of
            // the rig's role mapping — which ramp is 'hull', which is 'blue', where the negative
            // offsets go — and that is the class of claim this pipeline does not make.
            string source = RigCatalog.ReadSource(entry);
            host.Execute(hull.ShimSymbols.Length == 0
                ? source
                : RigMeshExtractor.WidenExportedLiteral(source, g, hull.ShimSymbols, entry.ScriptPath));

            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException(
                    $"Rig '{entry.ScriptPath}' ran but did not install globalThis.{g}.");

            var schemes = ReadPaintList(host, g, hull);
            string defaultPaint = host.EvaluateString($"String({g}.{hull.DefaultSchemeExpr} || '')");
            if (string.IsNullOrEmpty(defaultPaint))
                throw new InvalidOperationException(
                    $"'{entry.ScriptPath}' declares no default scheme ({hull.DefaultSchemeExpr}). The " +
                    "default is what the mesh bake is pinned to, so without it 'unset scheme = " +
                    "today's boat' cannot be checked.");

            log.AppendLine($"{hull.RigKey}: {schemes.Count} schemes from {entry.ScriptPath} " +
                           $"(default '{defaultPaint}')");

            int written = 0;
            foreach (var s in schemes)
            {
                var ramps = ReadMaterialTable(host, g, hull, s.Id);
                string path = $"{SchemeFolder}/{hull.AssetPrefix}_{Pascal(s.Id)}.asset";

                // ⚠️ Load-or-create, then ALWAYS overwrite the baked fields. An initialise-only path
                // would leave a shipped asset frozen at its first bake and report success — the
                // failure mode that has bitten this repo before.
                var def = AssetDatabase.LoadAssetAtPath<HullPaintSchemeDef>(path);
                bool fresh = def == null;
                if (fresh) def = ScriptableObject.CreateInstance<HullPaintSchemeDef>();

                def.Id = hull.IdPrefix + Snake(s.Id);
                def.Label = s.Label;
                def.Note = s.Note;
                def.HullMeshId = hull.HullMeshId;
                def.SourceRigPath = entry.ScriptPath;
                def.RigPaintId = s.Id;
                def.Ramps = ramps;

                if (fresh) AssetDatabase.CreateAsset(def, path);
                else EditorUtility.SetDirty(def);

                written++;
                log.AppendLine($"  {def.Id,-24} {ramps.Length} ramps, " +
                               $"{ramps.Sum(r => r.Colors.Length)} colours -> {Path.GetFileName(path)}" +
                               (s.Id == defaultPaint ? "   [DEFAULT — the A/B control]" : ""));
            }

            return written;
        }

        // ---- reading the rig -------------------------------------------------------------------

        public readonly struct PaintListing
        {
            public readonly string Id, Label, Note;
            public PaintListing(string id, string label, string note) { Id = id; Label = label; Note = note; }
        }

        /// <summary>The rig's scheme table, in its own swatch order.</summary>
        public static List<PaintListing> ReadPaintList(IRigScriptHost host, string g, in PaintedHull hull)
        {
            // Unit separator between fields, record separator between entries — a label or note is
            // free text ("PEARL & GOLD", notes with commas and dashes) and must not need escaping.
            string blob = host.EvaluateString(
                $"(function(){{var L={g}.{hull.SchemeListExpr};" +
                "return L.map(function(t){return [t[0],t[1]||'',t[2]||''].join('\\u001f');})" +
                ".join('\\u001e');})()");

            // Escapes, not literals: an invisible separator in source is exactly what an
            // editorconfig trim sweep eats, and it would fail as a silent mis-split.
            const char RS = '', US = '';
            var outp = new List<PaintListing>();
            foreach (string rec in blob.Split(RS))
            {
                if (rec.Length == 0) continue;
                string[] f = rec.Split(US);
                if (f.Length != 3)
                    throw new InvalidOperationException($"PAINTS entry '{rec}' is not id/label/note.");
                outp.Add(new PaintListing(f[0], f[1], f[2]));
            }
            if (outp.Count == 0) throw new InvalidOperationException($"{g}.PAINTS is empty.");
            return outp;
        }

        /// <summary>
        /// One scheme's material table, in the rig's own key order.
        ///
        /// <para>⚠️ The JS below is <c>RigMeshExtractor.ReadMaterials</c>'s expression with
        /// <c>MATS</c> swapped for <c>matsFor(id).MATS</c>. Keeping the two the same shape is what
        /// makes the baked scheme table index-compatible with the hull def's — ramps are matched to
        /// materials positionally, so an order that differed by one would repaint the glass with the
        /// boot-top. <c>HullPaintSchemeBakeTests</c> pins the two orders against each other.</para>
        /// </summary>
        public static HullMeshDef.Ramp[] ReadMaterialTable(IRigScriptHost host, string g,
                                                           in PaintedHull hull, string paintId)
        {
            string blob = host.EvaluateString(
                $"(function(){{var M={g}.{Mats(hull, paintId)},o=[];for(var k in M)" +
                "o.push(k+'|'+(M[k].off||0)+'|'+M[k].ramp.join(','));return o.join(';');})()");

            var ramps = new List<HullMeshDef.Ramp>();
            foreach (string part in blob.Split(';'))
            {
                string[] f = part.Split('|');
                if (f.Length != 3)
                    throw new InvalidOperationException(
                        $"matsFor('{paintId}').MATS entry '{part}' is not name|off|ramp.");
                string[] hex = f[2].Split(',');
                ramps.Add(new HullMeshDef.Ramp
                {
                    Colors = Array.ConvertAll(hex, ParseHex),
                    Offset = int.Parse(f[1], CultureInfo.InvariantCulture),
                });
            }

            if (ramps.Count == 0)
                throw new InvalidOperationException($"matsFor('{paintId}').MATS is empty.");
            if (ramps.Count > 16)
                throw new InvalidOperationException(
                    $"matsFor('{paintId}').MATS has {ramps.Count} materials; the facet shader's " +
                    "_RampMeta holds 16.");
            return ramps.ToArray();
        }

        /// <summary>The material NAMES of a scheme's table, in the rig's key order — for the test
        /// that pins scheme order against the hull def's.</summary>
        public static string[] ReadMaterialNames(IRigScriptHost host, string g,
                                                 in PaintedHull hull, string paintId)
        {
            string blob = host.EvaluateString(
                $"(function(){{var M={g}.{Mats(hull, paintId)},o=[];for(var k in M)" +
                "o.push(k);return o.join(',');})()");
            return blob.Split(',');
        }

        /// <summary>The hull's material-table expression with the scheme id substituted in, quoted.</summary>
        static string Mats(in PaintedHull hull, string paintId) =>
            hull.MatsExpr.Replace("{0}", Quote(paintId));

        // ---- small helpers ---------------------------------------------------------------------

        static Color32 ParseHex(string hex)
        {
            if (hex == null || hex.Length != 7 || hex[0] != '#')
                throw new FormatException($"'{hex}' is not a #rrggbb colour.");
            return new Color32(
                Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16),
                255);
        }

        static string Quote(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        /// <summary>'tarblack' -> 'Tarblack' for the file name. The rig's ids are single lower-case
        /// words today; a compound one would come through with its separators dropped, which is why
        /// the ASSET ID uses <see cref="Snake"/> and only the file name uses this.</summary>
        static string Pascal(string id)
        {
            var sb = new StringBuilder();
            bool up = true;
            foreach (char c in id)
            {
                if (c == '_' || c == '-' || c == ' ') { up = true; continue; }
                sb.Append(up ? char.ToUpperInvariant(c) : c);
                up = false;
            }
            return sb.ToString();
        }

        static string Snake(string id)
        {
            var sb = new StringBuilder();
            foreach (char c in id)
                sb.Append(c == '-' || c == ' ' ? '_' : char.ToLowerInvariant(c));
            return sb.ToString();
        }
    }
}
