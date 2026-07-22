using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One flat-shaded facet, exactly as the rig's own face list holds it.</summary>
    public sealed class RigFace
    {
        /// <summary>Rig-space object coordinates, metres, z-up. Doubles because the rig is
        /// JavaScript and every number in it is a double; quantising to float is a decision the
        /// MESH makes (see <see cref="RigMeshBuilder"/>), not one extraction should make for it.</summary>
        public Vector3d[] V;
        /// <summary>Index into <see cref="RigMeshData.Materials"/>.</summary>
        public int Mat;
        /// <summary>Per-face shade bias — the rig's <c>f.b</c>. Also the flag for the rig's
        /// interior/backface rescue: <c>b &lt;= -1</c> opts a face into it.</summary>
        public double B;
        /// <summary>Per-face depth bias toward the camera — the rig's <c>f.db</c>.</summary>
        public double Db;
    }

    /// <summary>A MATS entry: a palette ramp plus a constant index offset.</summary>
    public sealed class RigMaterial
    {
        public string Name;
        public Color32[] Ramp;
        /// <summary>The raw hex strings, in ramp order. Kept because the rig's keyline pass
        /// resolves colours by IDENTITY against its ramp table, so the reference rasteriser needs
        /// to dedupe ramps by content — see <see cref="RigMeshReferenceRasterizer"/>.</summary>
        public string[] RampHex;
        public int Off;
    }

    /// <summary>
    /// Everything the rig's renderer is: static geometry, palettes, one fixed light, and two
    /// scalars. This is the whole input to the facet shader — if something the rig draws is not in
    /// here, the golden master will say so in pixels.
    /// </summary>
    public sealed class RigMeshData
    {
        public string RigKey;
        public string GlobalName;
        public List<RigFace> Faces = new List<RigFace>();
        public List<RigMaterial> Materials = new List<RigMaterial>();

        /// <summary>The rig's LN, already normalised by the rig itself.</summary>
        public Vector3d LightN;
        public double Gain, Bias;

        /// <summary>The rig's 4×4 ordered-dither matrix, already in the rig's <c>(v+0.5)/16</c>
        /// form, indexed <c>[x &amp; 3][y &amp; 3]</c>.</summary>
        public double[,] Bayer = new double[4, 4];
        /// <summary>True when <see cref="Bayer"/> came from the rig, false when it fell back to the
        /// canonical matrix. Reported, never assumed — see <see cref="RigMeshSymbols"/>.</summary>
        public bool BayerWasExported;

        public Color32 Keyline;
        public int W, H;
        /// <summary>Pivot in cell pixels from the TOP-LEFT — the rigs' screen origin, and the
        /// origin the dither grid is phased against (<c>_DitherPhase</c>, ADR 0022).</summary>
        public double PivotX, PivotY;
        public int PxPerMetre;
        public double DefaultElev;

        /// <summary>Which symbols had to be shimmed in. Empty means the art director exported
        /// everything and <see cref="RigMeshExtractor"/>'s widening never ran.</summary>
        public IReadOnlyList<string> ShimmedSymbols = Array.Empty<string>();

        public int VertexCount
        {
            get { int n = 0; foreach (var f in Faces) n += f.V.Length; return n; }
        }

        /// <summary>Fan triangulation, which is what the rig itself does in <c>_paint</c>.</summary>
        public int TriangleCount
        {
            get { int n = 0; foreach (var f in Faces) n += Mathf.Max(0, f.V.Length - 2); return n; }
        }

        public override string ToString() =>
            $"{RigKey}: {Faces.Count} faces / {TriangleCount} tris / {Materials.Count} materials, " +
            $"cell {W}×{H}, pivot ({PivotX},{PivotY}), {PxPerMetre} px/m, elev {DefaultElev}°, " +
            (ShimmedSymbols.Count == 0
                ? "all symbols EXPORTED by the rig"
                : $"SHIMMED: {string.Join(",", ShimmedSymbols)}");
    }

    /// <summary>
    /// A double-precision 3-vector. The rig is JavaScript; every coordinate in it is a double, and
    /// the golden master is only meaningful if extraction is lossless. <see cref="Vector3"/> is
    /// float and is introduced deliberately, once, when the Mesh is built.
    /// </summary>
    public readonly struct Vector3d
    {
        public readonly double X, Y, Z;
        public Vector3d(double x, double y, double z) { X = x; Y = y; Z = z; }
        public Vector3 ToVector3() => new Vector3((float)X, (float)Y, (float)Z);
        public override string ToString() =>
            $"({X.ToString("R", CultureInfo.InvariantCulture)}, " +
            $"{Y.ToString("R", CultureInfo.InvariantCulture)}, " +
            $"{Z.ToString("R", CultureInfo.InvariantCulture)})";
    }

    /// <summary>
    /// The closure-private symbols a mesh needs but the rigs' public API does not (yet) expose.
    ///
    /// ⚠️ ADR 0022 open question #4 says the delta is "one property (<c>F,</c>)". MEASURED
    /// 2026-07-20 against lobsterBoatIsoRig.js, sideDraggerIsoRig.js, puntIsoRig.js and
    /// capeIslanderIsoRig.js: it is FIVE. <c>F</c> is private, and so are <c>MATS</c>, <c>GAIN</c>,
    /// <c>BIAS</c> and <c>LN</c>. The individual ramps (HULL, BOOT, …) ARE exported, but the MATS
    /// mapping is not — and the <c>blk</c>/<c>dark</c> aliases exist ONLY as MATS entries with a
    /// negative <c>off</c>, so the exported ramps alone cannot reconstruct the material table.
    /// </summary>
    public static class RigMeshSymbols
    {
        /// <summary>What the art director must export, per rig, for the shim to become dead code.</summary>
        public static readonly string[] Required = { "F", "MATS", "GAIN", "BIAS", "LN" };

        /// <summary>Preferred from the rig when exported, otherwise the canonical matrix below.
        /// Optional because it is identical in every rig inspected — and because the golden master
        /// adjudicates it: a wrong dither matrix is not subtle, it is a visibly different image.</summary>
        public const string OptionalBayer = "BAYER";

        /// <summary>The rig's matrix, already in <c>(v+0.5)/16</c> form.</summary>
        public static readonly int[,] CanonicalBayer =
        {
            { 0, 8, 2, 10 },
            { 12, 4, 14, 6 },
            { 3, 11, 1, 9 },
            { 15, 7, 13, 5 },
        };
    }

    /// <summary>
    /// Pulls the STATIC face list, material table and lighting constants out of an art-director rig
    /// so they can become a real mesh (ADR 0022 phase 2).
    ///
    /// <para><b>How it gets at them.</b> Two paths, probed in order, and the first one that works
    /// wins:</para>
    /// <list type="number">
    /// <item><b>The exported path.</b> Run the rig UNMODIFIED (ADR 0021 §5) and read
    /// <c>Global.F</c>, <c>Global.MATS</c>, … straight off the public object. This is the only path
    /// that should exist long-term.</item>
    /// <item><b>The shim.</b> If — and only if — a symbol is missing, re-run a MODIFIED IN-MEMORY
    /// COPY of the source whose exported object literal is widened with exactly the missing
    /// properties. ⚠️ The file on disk is NEVER written. <c>docs/art/rigs/**</c> is the art
    /// director's source and ours to read only. <c>RigMeshExtractionTests</c> asserts the rig files
    /// are byte-identical after a run.</item>
    /// </list>
    ///
    /// <para>The shim is scoped per SYMBOL, not all-or-nothing, so partial adoption works: the day
    /// <c>F,</c> lands in a rig, F stops being shimmed with no edit here, and the day the last of
    /// the five lands, <see cref="RigMeshData.ShimmedSymbols"/> comes back empty and the widening
    /// never executes. That is the intended end state — the code below is designed to become
    /// unreachable rather than to be deleted.</para>
    /// </summary>
    public static class RigMeshExtractor
    {
        /// <summary>Extracts from a catalogued rig, in its own throwaway host.</summary>
        public static RigMeshData Extract(string rigKey)
        {
            var entry = RigCatalog.Get(rigKey);
            using IRigScriptHost host = RigScriptHostFactory.Create();
            var data = ExtractFrom(host, entry.ScriptPath, entry.GlobalName);
            data.RigKey = rigKey;
            return data;
        }

        /// <summary>Extracts from a catalogued rig into a host the caller owns.</summary>
        public static RigMeshData Extract(IRigScriptHost host, string rigKey)
        {
            var entry = RigCatalog.Get(rigKey);
            var data = ExtractFrom(host, entry.ScriptPath, entry.GlobalName);
            data.RigKey = rigKey;
            return data;
        }

        /// <summary>
        /// Extracts from a rig that is not in <see cref="RigCatalog"/>. The catalog deliberately
        /// lists only the rigs the sprite baker actually bakes (CLAUDE.md rule 8 — importing source
        /// is not a licence to wire content), and adding the side dragger there would offer the
        /// owner a 433 MiB sprite bake that ADR 0022 exists to avoid. So mesh extraction takes an
        /// explicit path instead.
        /// </summary>
        /// <param name="scriptPath">Repo-relative, e.g. "docs/art/rigs/sideDraggerIsoRig.js".</param>
        /// <param name="globalName">The global the IIFE installs, e.g. "SideDraggerIso".</param>
        public static RigMeshData ExtractFrom(IRigScriptHost host, string scriptPath, string globalName)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            string full = Path.Combine(RigCatalog.RepoRoot, scriptPath);
            if (!File.Exists(full))
                throw new FileNotFoundException(
                    $"Rig source missing at {full}. The rigs are committed under docs/art/rigs/ — " +
                    "if this fired, the branch predates that import.", full);

            // READ ONLY. Nothing below ever writes this path. See the class doc.
            string source = File.ReadAllText(full);
            string g = globalName;

            // ---- pass 1: run the rig UNMODIFIED and see what it already gives us -------------
            host.Execute(source);
            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException(
                    $"Rig '{scriptPath}' ran but did not install globalThis.{g}. Either the global " +
                    "name is wrong or the rig changed shape.");

            var missing = new List<string>();
            foreach (string sym in RigMeshSymbols.Required)
                if (!HasSymbol(host, g, sym)) missing.Add(sym);

            bool bayerExported = HasSymbol(host, g, RigMeshSymbols.OptionalBayer);

            // ---- pass 2: the shim, only for what is actually missing -------------------------
            if (missing.Count > 0)
            {
                string widened = WidenExportedLiteral(source, g, missing, scriptPath);
                host.Execute(widened);

                var stillMissing = new List<string>();
                foreach (string sym in missing)
                    if (!HasSymbol(host, g, sym)) stillMissing.Add(sym);

                if (stillMissing.Count > 0)
                    throw new InvalidOperationException(
                        $"In-memory widening of '{scriptPath}' did not take: {g}." +
                        $"{{{string.Join(",", stillMissing)}}} is still missing after inserting it " +
                        $"into the exported literal. The rig's shape has changed and this shim must " +
                        "be re-aimed — or, far better, retired by exporting " +
                        $"{string.Join(", ", RigMeshSymbols.Required)} from {scriptPath} directly. " +
                        "⚠️ Do NOT fix this by editing docs/art/rigs/**; that is the art director's source.");

                Debug.LogWarning(
                    $"[rig-mesh] {scriptPath}: shimmed {string.Join(", ", missing)} via an IN-MEMORY " +
                    "widening because the rig does not export them (ADR 0022 open question #4). The " +
                    "file on disk was not touched. Ask the art director to add " +
                    $"`{string.Join(", ", missing)},` to the exported literal and this warning — and the " +
                    "shim — disappear on their own.");
            }

            var data = new RigMeshData
            {
                RigKey = g,
                GlobalName = g,
                W = (int)host.EvaluateNumber($"{g}.W"),
                H = (int)host.EvaluateNumber($"{g}.H"),
                PivotX = host.EvaluateNumber($"{g}.pivot.x"),
                PivotY = host.EvaluateNumber($"{g}.pivot.y"),
                PxPerMetre = (int)host.EvaluateNumber($"{g}.PX"),
                DefaultElev = host.EvaluateNumber($"{g}.defaultElev"),
                Gain = host.EvaluateNumber($"{g}.GAIN"),
                Bias = host.EvaluateNumber($"{g}.BIAS"),
                LightN = new Vector3d(host.EvaluateNumber($"{g}.LN[0]"),
                                      host.EvaluateNumber($"{g}.LN[1]"),
                                      host.EvaluateNumber($"{g}.LN[2]")),
                Keyline = ParseHex(host.EvaluateString($"{g}.KEY")),
                BayerWasExported = bayerExported,
                ShimmedSymbols = missing,
            };

            ReadBayer(host, g, bayerExported, data);
            ReadMaterials(host, g, data);
            ReadFaces(host, g, data);

            if (data.Faces.Count == 0)
                throw new InvalidOperationException(
                    $"{g}.F is present but empty. The rig builds its face list once at load " +
                    "(`(function build(){…})`); an empty list means build() did not run.");

            return data;
        }

        // ⚠️ ONE interpolated string, deliberately. Splitting it across a `$"…" + "…"` concat is how
        // the brace escaping goes wrong: `}}` only collapses to `}` inside an INTERPOLATED string,
        // so a plain second fragment emits a stray brace and V8 answers "SyntaxError: Unexpected
        // token '}'" with no hint that C# built the script wrong. Cost a full test cycle.
        static bool HasSymbol(IRigScriptHost host, string g, string symbol) =>
            host.EvaluateBool(
                $"(function(){{var v={g}.{symbol};return v!==undefined&&v!==null&&" +
                $"!(Array.isArray(v)&&v.length===0);}})()");

        /// <summary>
        /// ⚠️⚠️ THE ONE UGLY THING, AND IT IS DELIBERATELY SMALL AND LOUD. ⚠️⚠️
        ///
        /// Inserts <c>F:F, MATS:MATS, …</c> — only the missing names, under their CANONICAL names,
        /// so the read path above is identical whether the rig exported them or we widened them —
        /// immediately after the opening brace of <c>root.&lt;Global&gt; = {</c>.
        ///
        /// Single site, anchored on a regex that must match EXACTLY ONCE, operating on a string in
        /// memory. It never returns a path and nothing here opens a file for writing.
        /// </summary>
        /// <remarks>Public only so the tests can hit it without an engine — it is not an entry
        /// point, and the day the rigs export their symbols it should be deleted outright.</remarks>
        public static string WidenExportedLiteral(
            string source, string globalName, IReadOnlyList<string> missingSymbols, string scriptPathForMessages)
        {
            if (missingSymbols == null || missingSymbols.Count == 0) return source;

            var anchor = new Regex(@"root\." + Regex.Escape(globalName) + @"\s*=\s*\{",
                                   RegexOptions.CultureInvariant);
            var matches = anchor.Matches(source);
            if (matches.Count != 1)
                throw new InvalidOperationException(
                    $"Expected exactly one `root.{globalName} = {{` in {scriptPathForMessages}, found " +
                    $"{matches.Count}. The shim widens a SINGLE exported object literal; it will not " +
                    "guess which of several to aim at. Export " +
                    $"{string.Join(", ", RigMeshSymbols.Required)} from the rig instead.");

            var insert = new StringBuilder();
            foreach (string sym in missingSymbols) insert.Append(' ').Append(sym).Append(':').Append(sym).Append(',');

            var m = matches[0];
            return source.Insert(m.Index + m.Length, insert.ToString());
        }

        static void ReadBayer(IRigScriptHost host, string g, bool exported, RigMeshData data)
        {
            if (!exported)
            {
                for (int x = 0; x < 4; x++)
                    for (int y = 0; y < 4; y++)
                        data.Bayer[x, y] = (RigMeshSymbols.CanonicalBayer[x, y] + 0.5) / 16.0;
                return;
            }

            string blob = host.EvaluateString(
                $"(function(){{var o=[];for(var x=0;x<4;x++)for(var y=0;y<4;y++)" +
                $"o.push({g}.BAYER[x][y]);return o.join(',');}})()");
            string[] parts = blob.Split(',');
            if (parts.Length != 16)
                throw new InvalidOperationException(
                    $"{g}.BAYER is exported but is not 4×4 ({parts.Length} values).");
            for (int i = 0; i < 16; i++)
                data.Bayer[i / 4, i % 4] = double.Parse(parts[i], CultureInfo.InvariantCulture);
        }

        static void ReadMaterials(IRigScriptHost host, string g, RigMeshData data)
        {
            // name|off|#rrggbb,#rrggbb,… ; …  — key order is the JS object's own enumeration order,
            // which is also the order the face packer resolves names against, below.
            string blob = host.EvaluateString(
                $"(function(){{var M={g}.MATS,o=[];for(var k in M)" +
                "o.push(k+'|'+(M[k].off||0)+'|'+M[k].ramp.join(','));return o.join(';');})()");

            foreach (string part in blob.Split(';'))
            {
                string[] f = part.Split('|');
                if (f.Length != 3)
                    throw new InvalidOperationException(
                        $"{g}.MATS entry '{part}' is not name|off|ramp. MATS must be " +
                        "{name:{ramp:[…],off:n}}.");
                string[] hex = f[2].Split(',');
                data.Materials.Add(new RigMaterial
                {
                    Name = f[0],
                    Off = int.Parse(f[1], CultureInfo.InvariantCulture),
                    RampHex = hex,
                    Ramp = Array.ConvertAll(hex, ParseHex),
                });
            }

            if (data.Materials.Count == 0)
                throw new InvalidOperationException($"{g}.MATS is empty.");
        }

        static void ReadFaces(IRigScriptHost host, string g, RigMeshData data)
        {
            // The face list comes across as ONE packed binary blob through the bulk ReadBytes path.
            // Per-property marshalling of ~1,400 faces × 4 verts would erase the engine advantage
            // ADR 0021 was decided on (see IRigScriptHost.EvaluateBytes).
            //
            // Layout, little-endian:
            //   [i32 faceCount]  then per face  [i32 nv][i32 matId][f64 b][f64 db][nv × 3 × f64]
            //
            // f64, not f32: extraction must be lossless. Quantisation to float belongs to the Mesh.
            var matOrder = new StringBuilder();
            foreach (var m in data.Materials)
                matOrder.Append(JsStringLiteral(m.Name)).Append(',');

            string packer =
                $"globalThis.__hhRigMeshPack=(function(){{var F={g}.F;" +
                $"var order=[{matOrder.ToString().TrimEnd(',')}];" +
                "var ix={};order.forEach(function(n,i){ix[n]=i;});" +
                "var n=0;for(var i=0;i<F.length;i++)n+=F[i].v.length*3;" +
                "var buf=new ArrayBuffer(4+F.length*(8+16)+n*8);var dv=new DataView(buf);var p=0;" +
                "dv.setInt32(p,F.length,true);p+=4;" +
                "for(var i=0;i<F.length;i++){var f=F[i];" +
                "var mi=ix[f.mat];if(mi==null)mi=ix['hull'];if(mi==null)mi=0;" +
                "dv.setInt32(p,f.v.length,true);p+=4;" +
                "dv.setInt32(p,mi,true);p+=4;" +
                "dv.setFloat64(p,f.b||0,true);p+=8;dv.setFloat64(p,f.db||0,true);p+=8;" +
                "for(var k=0;k<f.v.length;k++){var v=f.v[k];" +
                "dv.setFloat64(p,v[0],true);p+=8;dv.setFloat64(p,v[1],true);p+=8;" +
                "dv.setFloat64(p,v[2],true);p+=8;}}" +
                "return new Uint8ClampedArray(buf);})();";
            host.Execute(packer);
            byte[] blob = host.EvaluateBytes("globalThis.__hhRigMeshPack");

            int off = 0;
            int faceCount = BitConverter.ToInt32(blob, off); off += 4;
            for (int i = 0; i < faceCount; i++)
            {
                int nv = BitConverter.ToInt32(blob, off); off += 4;
                int mat = BitConverter.ToInt32(blob, off); off += 4;
                double b = BitConverter.ToDouble(blob, off); off += 8;
                double db = BitConverter.ToDouble(blob, off); off += 8;
                if (nv < 3)
                    throw new InvalidOperationException(
                        $"{g}.F[{i}] has {nv} vertices. A face the rig can fan-triangulate has at least 3.");
                var vs = new Vector3d[nv];
                for (int k = 0; k < nv; k++)
                {
                    vs[k] = new Vector3d(BitConverter.ToDouble(blob, off),
                                         BitConverter.ToDouble(blob, off + 8),
                                         BitConverter.ToDouble(blob, off + 16));
                    off += 24;
                }
                data.Faces.Add(new RigFace { V = vs, Mat = mat, B = b, Db = db });
            }

            if (off != blob.Length)
                throw new InvalidOperationException(
                    $"Face blob for {g} was {blob.Length} bytes but {off} were consumed. The packer " +
                    "and the reader disagree about layout.");
        }

        static string JsStringLiteral(string s) =>
            "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        static Color32 ParseHex(string hex)
        {
            hex = hex.Trim().TrimStart('#');
            if (hex.Length < 6)
                throw new FormatException($"'{hex}' is not a #rrggbb colour.");
            return new Color32(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16), 255);
        }
    }
}
