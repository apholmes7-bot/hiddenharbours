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
        /// A hull whose rig carries a paint axis. Three of them now, across TWO different paint APIs
        /// — which is the design working: a second and third hull cost a line each and no new baker.
        /// <see cref="AssetPrefix"/> keeps two hulls' tables from colliding on disk, and
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
            /// differently needs no branch here.
            ///
            /// <para>⚠️ Only the HEAD of these expressions is prefixed with the rig's global. Inside a
            /// callback a bare <c>SCHEMES</c> is a reference to a global that does not exist — the rig
            /// keeps it in its closure — and the bake dies with <c>ReferenceError: SCHEMES is not
            /// defined</c>. Write <c>{G}</c> for the global name anywhere the expression needs to
            /// reach back into the rig's exports; <see cref="Expand"/> substitutes it.</para></summary>
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

            // The two small craft, whose axis has existed since the 2026-07-25 kit and is only now
            // wired. They need NO shim: both export `SCHEMES`, `schemeIds`, `defaultScheme` and
            // `palette` from their own literal, which is why these are one line each.
            //
            // The second paint API in the repo, and the reason MatsExpr is a parameter: this kit
            // resolves `palette({scheme:id}).mats` where the lobster's resolves `matsFor(id).MATS`.
            // Both are the rig's OWN resolver at its own scheme — neither is a transcription.
            //
            // ⚠️ `palette({})` — the expression RigMeshSymbols.Reconstructions bakes the MESH from —
            // is the same call with no colourway, which each rig resolves to its own DEFAULT_SCHEME
            // ('harbour-white' on both). MEASURED equal to `palette({scheme:'harbour-white'}).mats`,
            // material for material and in the same KEY ORDER, which is what makes "unset scheme =
            // today's boat" a fact here as it is for the lobster. See the PR body's A/B tables.
            //
            // The label and note come from the rig's own SCHEMES block rather than being left blank —
            // they are the art director's words for what each colourway IS ("never finished, always
            // working"), and they are what an assignment gets argued from.
            new PaintedHull("punt", "hullmesh.punt_iso", "PuntIso", "paint.punt_",
                            matsExpr: "palette({scheme:{0}}).mats",
                            schemeListExpr: "schemeIds.map(function(id){var s={G}.SCHEMES[id];" +
                                            "return [id,s.name||'',s.note||''];})",
                            defaultSchemeExpr: "defaultScheme"),

            // 'console', not 'skiff': the fleet holds THREE skiff rigs (console, sport, skiffMotor)
            // and this table is hull-specific, so a `paint.skiff_` id would be ambiguous the day the
            // sport skiff gets an axis — and ids are append-only, so it could not be fixed then.
            // Matches her every other identifier: consoleSkiff / ConsoleIso / hullmesh.console_iso.
            new PaintedHull("consoleSkiff", "hullmesh.console_iso", "ConsoleIso", "paint.console_",
                            matsExpr: "palette({scheme:{0}}).mats",
                            schemeListExpr: "schemeIds.map(function(id){var s={G}.SCHEMES[id];" +
                                            "return [id,s.name||'',s.note||''];})",
                            defaultSchemeExpr: "defaultScheme"),

            // The Cape Islander has no paint axis at all (a plain `MATS` const), so she cannot join
            // until the art director gives her one. That is a rig change, not a line here.
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
        /// The fleet entry whose MESH these schemes repaint — and therefore the rig they must be
        /// read from.
        ///
        /// <para>⚠️ Resolved through <see cref="HullMeshFleet"/>, NOT <see cref="RigCatalog"/>, and
        /// that is load-bearing. <c>RigMeshAssetBaker</c> bakes the hull mesh from
        /// <c>FleetHull.ScriptPath</c>/<c>GlobalName</c>; if the paint bake read a different table
        /// the two could point at different files and "unset scheme = today's boat" would be
        /// comparing a scheme against a mesh baked from something else. One table, so they cannot
        /// drift. (It is also the only table the console skiff is in — she has no
        /// <see cref="RigCatalog"/> entry, because the catalog registers rigs that need an azimuth
        /// DECLARATION and her mesh bake never needed one.)</para>
        /// </summary>
        public static FleetHull RigFor(in PaintedHull hull)
        {
            // Copied out of the `in` parameter first: a readonly-ref cannot be captured by the lambda.
            string rigKey = hull.RigKey, meshId = hull.HullMeshId;

            var fleet = HullMeshFleet.Hulls.FirstOrDefault(h => h.Key == rigKey);
            if (string.IsNullOrEmpty(fleet.Key))
                throw new ArgumentException(
                    $"No hull '{rigKey}' in HullMeshFleet. Known: " +
                    $"{string.Join(", ", HullMeshFleet.Hulls.Select(h => h.Key))}.");

            if (fleet.MeshId != meshId)
                throw new InvalidOperationException(
                    $"'{rigKey}' says it repaints '{meshId}' but HullMeshFleet bakes " +
                    $"'{fleet.MeshId}' from that rig. A scheme table is matched to a hull BY INDEX, " +
                    "so pointing these two at different meshes recolours the wrong materials.");
            return fleet;
        }

        /// <summary>
        /// Bakes every scheme the hull's rig declares. Returns how many assets were written.
        /// </summary>
        public static int Bake(in PaintedHull hull, StringBuilder log)
        {
            if (!Directory.Exists(SchemeFolder)) Directory.CreateDirectory(SchemeFolder);

            var fleet = RigFor(hull);
            string g = fleet.GlobalName;

            using IRigScriptHost host = RigScriptHostFactory.Create();

            // The lobster rig exports PAINTS and paintRamps but keeps its resolver private, so widen
            // exactly the symbols this hull declares — the same in-memory, never-written shim the mesh
            // extractor uses. (The punt and the console export theirs and declare none.)
            // Reading paintRamps and rebuilding the material table here would be a transcription of
            // the rig's role mapping — which ramp is 'hull', which is 'blue', where the negative
            // offsets go — and that is the class of claim this pipeline does not make.
            string source = ReadRigSource(fleet.ScriptPath);
            host.Execute(hull.ShimSymbols.Length == 0
                ? source
                : RigMeshExtractor.WidenExportedLiteral(source, g, hull.ShimSymbols, fleet.ScriptPath));

            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException(
                    $"Rig '{fleet.ScriptPath}' ran but did not install globalThis.{g}.");

            var schemes = ReadPaintList(host, g, hull);
            string defaultPaint = host.EvaluateString($"String({g}.{hull.DefaultSchemeExpr} || '')");
            if (string.IsNullOrEmpty(defaultPaint))
                throw new InvalidOperationException(
                    $"'{fleet.ScriptPath}' declares no default scheme ({hull.DefaultSchemeExpr}). The " +
                    "default is what the mesh bake is pinned to, so without it 'unset scheme = " +
                    "today's boat' cannot be checked.");

            log.AppendLine($"{hull.RigKey}: {schemes.Count} schemes from {fleet.ScriptPath} " +
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
                def.SourceRigPath = fleet.ScriptPath;
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
                $"(function(){{var L={g}.{Expand(hull.SchemeListExpr, g)};" +
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
                    throw new InvalidOperationException(
                        $"{g}.{Expand(hull.SchemeListExpr, g)} entry '{rec}' is not id/label/note.");
                outp.Add(new PaintListing(f[0], f[1], f[2]));
            }
            if (outp.Count == 0)
                throw new InvalidOperationException($"{g}.{Expand(hull.SchemeListExpr, g)} is empty.");
            return outp;
        }

        /// <summary>
        /// One scheme's material table, in the rig's own key order.
        ///
        /// <para>⚠️ The JS below is <c>RigMeshExtractor.ReadMaterials</c>'s expression with
        /// <c>MATS</c> swapped for the hull's own <see cref="PaintedHull.MatsExpr"/>
        /// (<c>matsFor(id).MATS</c>, or <c>palette({scheme:id}).mats</c>). Keeping the two the same shape is what
        /// makes the baked scheme table index-compatible with the hull def's — ramps are matched to
        /// materials positionally, so an order that differed by one would repaint the glass with the
        /// boot-top. <c>HullPaintSchemeBakeTests</c> pins the two orders against each other.</para>
        /// </summary>
        public static HullMeshDef.Ramp[] ReadMaterialTable(IRigScriptHost host, string g,
                                                           in PaintedHull hull, string paintId)
        {
            string blob = host.EvaluateString(
                $"(function(){{var M={g}.{Mats(hull, paintId, g)},o=[];for(var k in M)" +
                "o.push(k+'|'+(M[k].off||0)+'|'+M[k].ramp.join(','));return o.join(';');})()");

            var ramps = new List<HullMeshDef.Ramp>();
            foreach (string part in blob.Split(';'))
            {
                string[] f = part.Split('|');
                if (f.Length != 3)
                    throw new InvalidOperationException(
                        $"{g}.{Mats(hull, paintId, g)} entry '{part}' is not name|off|ramp.");
                string[] hex = f[2].Split(',');
                ramps.Add(new HullMeshDef.Ramp
                {
                    Colors = Array.ConvertAll(hex, ParseHex),
                    Offset = int.Parse(f[1], CultureInfo.InvariantCulture),
                });
            }

            if (ramps.Count == 0)
                throw new InvalidOperationException($"{g}.{Mats(hull, paintId, g)} is empty.");
            if (ramps.Count > 16)
                throw new InvalidOperationException(
                    $"{g}.{Mats(hull, paintId, g)} has {ramps.Count} materials; the facet shader's " +
                    "_RampMeta holds 16.");
            return ramps.ToArray();
        }

        /// <summary>The material NAMES of a scheme's table, in the rig's key order — for the test
        /// that pins scheme order against the hull def's.</summary>
        public static string[] ReadMaterialNames(IRigScriptHost host, string g,
                                                 in PaintedHull hull, string paintId)
        {
            string blob = host.EvaluateString(
                $"(function(){{var M={g}.{Mats(hull, paintId, g)},o=[];for(var k in M)" +
                "o.push(k);return o.join(',');})()");
            return blob.Split(',');
        }

        /// <summary>The hull's material-table expression with the scheme id substituted in, quoted.</summary>
        static string Mats(in PaintedHull hull, string paintId, string g) =>
            Expand(hull.MatsExpr.Replace("{0}", Quote(paintId)), g);

        /// <summary>Substitutes the rig's global name for <c>{G}</c>. Needed because only the HEAD of
        /// a hull's expression is prefixed with the global — anything inside a callback has to name it
        /// itself, and a bare closure symbol there is a <c>ReferenceError</c> at bake time.</summary>
        static string Expand(string expr, string g) => expr.Replace("{G}", g);

        /// <summary>The rig source, UNMODIFIED (ADR 0021 §5) — the same read
        /// <see cref="RigCatalog.ReadSource"/> does, by path, because a hull's rig is named by
        /// <see cref="HullMeshFleet"/> rather than by a catalog entry.</summary>
        public static string ReadRigSource(string scriptPath)
        {
            string full = Path.Combine(RigCatalog.RepoRoot, scriptPath);
            if (!File.Exists(full))
                throw new FileNotFoundException(
                    $"Rig source missing at {full}. The rigs are committed under docs/art/rigs/.", full);
            return File.ReadAllText(full);
        }

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
