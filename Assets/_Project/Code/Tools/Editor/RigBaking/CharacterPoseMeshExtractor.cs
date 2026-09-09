using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Extracts PER-POSE face lists from the shipped pass-6 character rig — the character
    /// counterpart of <see cref="RigMeshExtractor"/>, which cannot serve here, and the difference is
    /// the load-bearing fact of ADR 0044:
    ///
    /// <para><b>The boat rigs build ONE static face list at load</b> (<c>const F = []</c>) and apply
    /// heading/rock as transforms — that is ADR 0022's premise and why a hull is one mesh. <b>The
    /// character rig builds its face list PER POSE</b> (<c>facesOf(pose(anim, u, build))</c>): the
    /// skeleton's FK/IK result is baked into the vertex positions, so there is no static <c>F</c> to
    /// widen into. What IS still true: for a fixed (preset, anim, frame, carry, power) the face list
    /// is deterministic, and the heading stays a transform. So the bake snapshots one mesh per pose
    /// frame and keeps rotation live.</para>
    ///
    /// <para><b>It asks the rig the question the rig's own renderer asks.</b> <c>render()</c> is
    /// <c>resolveOpts → makeMats → gridHead(pose(...)) → facesOf</c>; this runs the same chain
    /// through the rig's own <c>resolveOpts</c> (reached by the single documented inner widening,
    /// <see cref="RigMeshSymbols.InnerWidenings"/>) rather than re-deriving the build merge, the
    /// <c>settle</c> denominator or the carry whitelist in C#. Two steps of that chain are
    /// deliberately NOT carried, and both are recorded on the def rather than hidden:</para>
    /// <list type="number">
    ///   <item><b><c>gridHead</c></b> — rev 6.8 nudges the head centre by up to half a pixel so it
    ///   projects onto a pixel CENTRE, computed from the camera basis and therefore DIFFERENT PER
    ///   DIRECTION. A single mesh rotated live cannot carry a per-direction sub-pixel raster trick.
    ///   Measured cost against the rig's own render: 2.28–15.00% differing inked pixels.</item>
    ///   <item><b>the head STAMP</b> — the face (eyes, brows, lashes, lips, iris) is painted by the
    ///   head rig after the polygons, not built from them. A mesh character has no face until a
    ///   presenter re-adds one. Measured cost: 0.00–2.82%.</item>
    /// </list>
    ///
    /// <para><b>The rig file is never touched.</b> Prerequisites (eye → head) load through
    /// <see cref="RigCatalog.InstallModule"/> unmodified; the body is widened IN MEMORY by the same
    /// mechanism the sport fisher uses, anchored to match exactly once. ADR 0021 §5.</para>
    /// </summary>
    public static class CharacterPoseMeshExtractor
    {
        /// <summary>The catalog key for the shipped character body — <c>characterIsoRig6.js</c>,
        /// global <c>CharacterIso6</c>, prerequisites eye → head. Read from the catalog rather than
        /// spelled here so the mesh path can never bake a different rig from the sheet path.</summary>
        public const string CatalogKey = "character";

        public static RigEntry Entry => RigCatalog.Get(CatalogKey);
        public static string ScriptPath => Entry.ScriptPath;
        public static string GlobalName => Entry.GlobalName;

        /// <summary>The one symbol the rig does not export and the bake cannot do without.</summary>
        public const string WidenedSymbol = "resolveOpts";

        // ---------------------------------------------------------------------------------------
        // Loading
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Loads the character kit into the host: eye and head through the catalog UNMODIFIED, then
        /// the body with its single documented widening applied in memory. Run once per host; the
        /// rig's public API is unchanged by the widening, so golden renders come from the same host.
        /// </summary>
        public static void Load(IRigScriptHost host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            RigEntry entry = Entry;

            // The kit's prerequisites are a DEPENDENCY, not a nicety: the pass-6 body asks
            // root.HeadIso for the hat table and silently falls back to a local one when the head
            // rig is absent — it renders the wrong art rather than failing. Loaded through the
            // catalog so the chain (eye → head) and the idempotence come from one place.
            foreach (string key in entry.Prerequisites)
            {
                RigEntry dep = RigCatalog.Get(key);
                if (host.EvaluateBool($"typeof {dep.GlobalName} === 'object' && {dep.GlobalName} !== null"))
                    continue;
                RigCatalog.InstallModule(host, dep);
            }

            string source = RigCatalog.ReadSource(entry);           // READ ONLY, never written back
            host.Execute(RigMeshExtractor.ApplyInnerWidenings(source, entry.ScriptPath));

            string g = entry.GlobalName;
            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException(
                    $"{entry.ScriptPath} ran but did not install globalThis.{g}.");
            if (!host.EvaluateBool($"typeof {g}.{WidenedSymbol} === 'function'"))
                throw new InvalidOperationException(
                    $"The in-memory widening did not surface {g}.{WidenedSymbol} — the rig's export " +
                    "literal changed shape. Re-aim the InnerWidening anchor in RigMeshSymbols " +
                    "(and do NOT edit docs/art/rigs/**).");
            if (!host.EvaluateBool($"typeof {g}.facesOf === 'function' && typeof {g}.pose === 'function' " +
                                   $"&& typeof {g}.makeMats === 'function'"))
                throw new InvalidOperationException(
                    $"{g} does not export facesOf/pose/makeMats. The mesh bake reads the rig's own " +
                    "geometry surface; without it there is nothing to extract.");
        }

        /// <summary>The rig's own revision string, for the def's provenance.</summary>
        public static string Revision(IRigScriptHost host) =>
            host.EvaluateString($"String({GlobalName}.revision||'')");

        /// <summary>
        /// SHA-256 of the rig source with line endings normalised to LF — the def's stale-bake
        /// guard. LF-normalised because this repo has mixed endings and a checkout that differs only
        /// in CRLF is the same rig; a bake that failed on that would cry wolf until it was ignored.
        /// </summary>
        public static string SourceSha256()
        {
            string full = Path.Combine(RigCatalog.RepoRoot, ScriptPath);
            byte[] raw = File.ReadAllBytes(full);
            var lf = new List<byte>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == (byte)'\r' && i + 1 < raw.Length && raw[i + 1] == (byte)'\n') continue;
                lf.Add(raw[i]);
            }
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(lf.ToArray());
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        // ---------------------------------------------------------------------------------------
        // The rig's own tables
        // ---------------------------------------------------------------------------------------

        /// <summary>Every ANIMS key, in the rig's own declaration order.</summary>
        public static string[] Anims(IRigScriptHost host) =>
            host.EvaluateString($"Object.keys({GlobalName}.ANIMS).join(',')")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>Every BUILDS preset key, in the rig's own declaration order.</summary>
        public static string[] Presets(IRigScriptHost host) =>
            host.EvaluateString($"Object.keys({GlobalName}.BUILDS).join(',')")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        public static int FrameCount(IRigScriptHost host, string anim) =>
            (int)host.EvaluateNumber($"{GlobalName}.ANIMS[{Js(anim)}].frames");

        public static double FrameMs(IRigScriptHost host, string anim) =>
            host.EvaluateNumber($"{GlobalName}.ANIMS[{Js(anim)}].ms");

        /// <summary>True when the rig marks this clip <c>settle</c> — it spans its frames
        /// INCLUSIVELY. Carried onto the def because a player that loops a settle clip plays a
        /// different pose on the last frame than the bake did.</summary>
        public static bool IsSettle(IRigScriptHost host, string anim) =>
            host.EvaluateBool($"!!{GlobalName}.ANIMS[{Js(anim)}].settle");

        /// <summary>
        /// Every material name <c>makeMats</c> declares for a preset, in the rig's own key order —
        /// the ordering authority for a flipbook's shared ramp table. Thirty-six for the fisher,
        /// of which twelve are referenced by any polygon; the rest colour the head rig's raster
        /// stamp, which a mesh does not carry.
        /// </summary>
        public static string[] MaterialOrder(IRigScriptHost host, string preset) =>
            host.EvaluateString(
                $"(function(){{var C={GlobalName};" +
                $"var R=C.{WidenedSymbol}(0,{{build:{{preset:{Js(preset)}}}}});" +
                "return Object.keys(C.makeMats(R.b).MATS).join(',');})()")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        // ---------------------------------------------------------------------------------------
        // Extraction
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Extract one pose's faces + the shared render facts as a <see cref="RigMeshData"/> — the
        /// exact input <see cref="RigMeshBuilder"/> and <see cref="RigMeshReferenceRasterizer"/>
        /// already accept, which is what lets the character path reuse the whole ADR 0022 tail end.
        ///
        /// <para><see cref="RigMeshData.Materials"/> holds ONLY the materials THIS pose's faces
        /// reference, in the rig's own MATS order, and <see cref="RigFace.Mat"/> indexes it. A
        /// flipbook needs one table across every frame; the baker unions these and remaps
        /// (<see cref="RemapMaterials"/>). Emitting all 36 would blow the shader's sixteen ramp
        /// slots on a character that in fact uses twelve.</para>
        /// </summary>
        /// <param name="preset">A BUILDS key ('fisher', 'ginny', 'skipper', …).</param>
        /// <param name="anim">An ANIMS key ('idle', 'walk', 'haul', …).</param>
        /// <param name="frame">Frame index within the clip.</param>
        /// <param name="carry">A CARRIES key, or null for the free-handed pose.</param>
        /// <param name="power">The rig's power opt ('short' / 'long'), or null on an anim that has
        /// no power axis. Null and 'short' pose IDENTICALLY (the rig defaults to short); they differ
        /// only in the state KEY, and that difference is the sheet baker's vocabulary, kept here so a
        /// def clip and a baked sheet can be lined up by name.</param>
        /// <param name="rest">A REST height ('ground' / 'stowV' / 'stowH') for the set-down clip, or
        /// null. It never reaches resolveOpts' own return; it rides in the opts bag <c>o</c> and is
        /// read downstream by <c>reachOf</c> — which is exactly why the bag is passed through whole
        /// instead of being rebuilt from the four values resolveOpts names.</param>
        /// <param name="dir">The direction handed to the rig's resolveOpts. The POSE must not depend
        /// on it (the heading is a live transform); passing it through rather than hard-coding lets
        /// the guard prove that by extracting the same pose at two dirs and comparing.</param>
        public static RigMeshData ExtractPose(IRigScriptHost host, string preset, string anim,
                                              int frame, string carry = null,
                                              string power = null, string rest = null,
                                              int dir = 0)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            string g = GlobalName;
            var c = CultureInfo.InvariantCulture;

            if (!host.EvaluateBool($"typeof {g}.ANIMS[{Js(anim)}] === 'object'"))
                throw new ArgumentException($"{g}.ANIMS has no '{anim}'.", nameof(anim));
            if (!host.EvaluateBool($"typeof {g}.BUILDS[{Js(preset)}] === 'object'"))
                throw new ArgumentException($"{g}.BUILDS has no preset '{preset}'.", nameof(preset));

            // The rig's OWN resolver, then the rig's own chain — minus gridHead (per-direction
            // sub-pixel raster nudge, uncarryable by a rotated mesh) and minus the head stamp
            // (a raster pass, not geometry). Both are recorded on the def, not hidden.
            host.Execute(
                "(function(){var C=" + g + ";" +
                "var R=C." + WidenedSymbol + "(" + dir.ToString(c) + "," +
                    OptsJs(preset, anim, frame, carry, power, rest) + ");" +
                "C.__hhPoseMats=C.makeMats(R.b).MATS;" +
                "C.__hhPoseFaces=C.facesOf(C.pose(R.anim,R.u,R.b,R.power,R.carry,R.o),R.b);})()");

            var data = new RigMeshData
            {
                RigKey = $"{g}:{preset}:{StateKey(anim, power, carry, rest)}:{frame}",
                GlobalName = g,
                SourceFaceExpression =
                    $"facesOf(pose(resolveOpts({dir}, {OptsJs(preset, anim, frame, carry, power, rest)})))",
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
                BayerWasExported = true,
                ShimmedSymbols = new[] { WidenedSymbol },
            };

            ReadBayer(host, g, data);
            ReadMaterials(host, g, data);
            ReadFaces(host, g, data);

            if (data.Faces.Count == 0)
                throw new InvalidOperationException(
                    $"{g}.facesOf produced no faces for {data.RigKey}.");
            return data;
        }

        /// <summary>Extract one pose of a recipe state — the same <see cref="CharacterState"/> the
        /// SHEET baker enumerates, so the mesh path and the sprite path can be compared state for
        /// state (ADR 0041 retires a sheet only at parity, and parity needs one vocabulary).</summary>
        public static RigMeshData ExtractPose(IRigScriptHost host, string preset,
                                              in CharacterState state, int frame, int dir = 0) =>
            ExtractPose(host, preset, state.Anim, frame, state.Carry,
                        state.Power, state.Rest, dir);

        /// <summary>The rig's own render — the golden truth extraction is measured against. Same
        /// opts bag as <see cref="ExtractPose"/>, so the two cannot drift apart.</summary>
        public static byte[] RenderTruth(IRigScriptHost host, int dir, string preset, string anim,
                                         int frame, string carry = null, string power = null,
                                         string rest = null) =>
            host.EvaluateBytes(
                $"{GlobalName}.render({dir.ToString(CultureInfo.InvariantCulture)}," +
                OptsJs(preset, anim, frame, carry, power, rest) + ")");

        /// <summary>The rig's own render of a recipe state.</summary>
        public static byte[] RenderTruth(IRigScriptHost host, int dir, string preset,
                                         in CharacterState state, int frame) =>
            RenderTruth(host, dir, preset, state.Anim, frame, state.Carry,
                        state.Power, state.Rest);

        /// <summary>The def's clip key for a state — <c>walk</c>, <c>cast_long</c>,
        /// <c>walk_buckets</c>, <c>reach_stowV</c>. Deliberately <see cref="CharacterState.Key"/>'s
        /// spelling, so a def clip and a baked sheet name the same thing.</summary>
        public static string StateKey(string anim, string power, string carry, string rest) =>
            new CharacterState(anim, power, carry, rest).Key;

        /// <summary>
        /// Rewrites a pose's face material indices onto a SHARED material table, so every mesh in a
        /// flipbook indexes one ramp array. Throws on a material the shared table does not carry —
        /// the alternative (resolve to index 0) is how this repo has shipped wrongly-coloured art.
        /// </summary>
        public static void RemapMaterials(RigMeshData data, IReadOnlyList<RigMaterial> shared)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (shared == null) throw new ArgumentNullException(nameof(shared));

            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < shared.Count; i++) index[shared[i].Name] = i;

            var map = new int[data.Materials.Count];
            for (int i = 0; i < data.Materials.Count; i++)
            {
                if (!index.TryGetValue(data.Materials[i].Name, out int to))
                    throw new InvalidOperationException(
                        $"{data.RigKey} uses material '{data.Materials[i].Name}', which the shared " +
                        "flipbook table does not carry. The union was built from a different set of " +
                        "poses than the bake extracted.");
                map[i] = to;
            }

            foreach (var f in data.Faces)
            {
                if (f.Mat < 0 || f.Mat >= map.Length)
                    throw new InvalidOperationException(
                        $"{data.RigKey} has a face whose material index {f.Mat} is outside its own " +
                        $"table of {map.Length}.");
                f.Mat = map[f.Mat];
            }

            data.Materials.Clear();
            data.Materials.AddRange(shared);
        }

        // ---------------------------------------------------------------------------------------
        // Readers. The face blob layout is RigMeshExtractor's contract, byte for byte; the material
        // blob is WIDER than the hull one because the character rig shades per material.
        // ---------------------------------------------------------------------------------------

        static void ReadBayer(IRigScriptHost host, string g, RigMeshData data)
        {
            string blob = host.EvaluateString(
                "(function(){var o=[];for(var x=0;x<4;x++)for(var y=0;y<4;y++)" +
                $"o.push({g}.BAYER[x][y]);return o.join(',');}})()");
            string[] parts = blob.Split(',');
            if (parts.Length != 16)
                throw new InvalidOperationException($"{g}.BAYER is not 4×4 ({parts.Length} values).");
            for (int i = 0; i < 16; i++)
                data.Bayer[i / 4, i % 4] = Num(parts[i]);
        }

        static void ReadMaterials(IRigScriptHost host, string g, RigMeshData data)
        {
            // Only the materials THIS pose's faces reference, in the rig's own MATS key order —
            // the zodiac's lesson (she declares eighteen and her hull uses fourteen, and the shader
            // has sixteen ramp slots), applied to a rig that declares thirty-six and uses twelve.
            string blob = host.EvaluateString(
                $"(function(){{var M={g}.__hhPoseMats,F={g}.__hhPoseFaces,used={{}},o=[];" +
                "for(var i=0;i<F.length;i++)used[F[i].mat]=1;" +
                "for(var k in M){if(!used[k])continue;var m=M[k];" +
                "o.push(k+'|'+(m.off||0)+'|'+(m.gain==null?'':m.gain)+'|'+(m.bias==null?'':m.bias)" +
                "+'|'+(m.dith?1:0)+'|'+(m.idx==null?-1:m.idx)+'|'+m.ramp.join(','));}" +
                "return o.join(';');})()");

            foreach (string part in blob.Split(';'))
            {
                if (part.Length == 0) continue;
                string[] f = part.Split('|');
                if (f.Length != 7)
                    throw new InvalidOperationException(
                        $"{g}.MATS entry '{part}' is not name|off|gain|bias|dith|idx|ramp.");
                string[] hex = f[6].Split(',');
                data.Materials.Add(new RigMaterial
                {
                    Name = f[0],
                    Off = int.Parse(f[1], CultureInfo.InvariantCulture),
                    Gain = f[2].Length == 0 ? 1.0 : Num(f[2]),
                    Bias = f[3].Length == 0 ? double.NaN : Num(f[3]),
                    OrderedDither = f[4] == "1",
                    FixedIndex = int.Parse(f[5], CultureInfo.InvariantCulture),
                    RampHex = hex,
                    Ramp = Array.ConvertAll(hex, ParseHex),
                });
            }

            if (data.Materials.Count == 0)
                throw new InvalidOperationException(
                    $"{data.RigKey}: no material is referenced by any face, which cannot be true of " +
                    "a pose that produced faces.");
        }

        static void ReadFaces(IRigScriptHost host, string g, RigMeshData data)
        {
            // Layout, little-endian (RigMeshExtractor's contract):
            //   [i32 faceCount] then per face [i32 nv][i32 matId][f64 b][f64 db][nv × 3 × f64]
            var matOrder = new StringBuilder();
            foreach (var m in data.Materials)
                matOrder.Append(Js(m.Name)).Append(',');

            string packer =
                $"globalThis.__hhPoseMeshPack=(function(){{var F={g}.__hhPoseFaces;" +
                $"var order=[{matOrder.ToString().TrimEnd(',')}];" +
                "var ix={};order.forEach(function(n,i){ix[n]=i;});" +
                "var n=0;for(var i=0;i<F.length;i++)n+=F[i].v.length*3;" +
                "var buf=new ArrayBuffer(4+F.length*(8+16)+n*8);var dv=new DataView(buf);var p=0;" +
                "dv.setInt32(p,F.length,true);p+=4;" +
                "for(var i=0;i<F.length;i++){var f=F[i];" +
                // NOT `|| 0`: an unmapped material is a bug in the union, and resolving it to the
                // first ramp is exactly how mis-coloured art has shipped here before.
                "var mi=ix[f.mat];if(mi==null)throw new Error('face '+i+' uses material '+f.mat+" +
                "' which is not in this pose\\'s own material list');" +
                "dv.setInt32(p,f.v.length,true);p+=4;" +
                "dv.setInt32(p,mi,true);p+=4;" +
                "dv.setFloat64(p,f.b||0,true);p+=8;dv.setFloat64(p,f.db||0,true);p+=8;" +
                "for(var k=0;k<f.v.length;k++){var v=f.v[k];" +
                "dv.setFloat64(p,v[0],true);p+=8;dv.setFloat64(p,v[1],true);p+=8;" +
                "dv.setFloat64(p,v[2],true);p+=8;}}" +
                "return new Uint8ClampedArray(buf);})();";
            host.Execute(packer);
            byte[] blob = host.EvaluateBytes("globalThis.__hhPoseMeshPack");

            int off = 0;
            int faceCount = BitConverter.ToInt32(blob, off); off += 4;
            for (int i = 0; i < faceCount; i++)
            {
                int nv = BitConverter.ToInt32(blob, off); off += 4;
                int mat = BitConverter.ToInt32(blob, off); off += 4;
                double b = BitConverter.ToDouble(blob, off); off += 8;
                double db = BitConverter.ToDouble(blob, off); off += 8;
                if (nv < 3)
                    throw new InvalidOperationException($"{data.RigKey} face {i} has {nv} vertices.");
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
                    $"Pose face blob was {blob.Length} bytes but {off} consumed — packer/reader disagree.");
        }

        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// The opts bag, written once so extraction and the golden render cannot diverge.
        ///
        /// <para>⚠️ <c>build</c> is an OBJECT with a <c>preset</c> key, never a bare string. The
        /// rig's <c>resolveBuild</c> reads <c>opts.build.preset</c>; a bare string goes through
        /// <c>Object.assign({}, DEFAULT_BUILD, null, 'fisher')</c>, which spreads the string
        /// character by character and silently renders the DEFAULT man. Measured in this lane's V8
        /// harness on 2026-09-09, and it produced a completely self-consistent delta table of the
        /// wrong character.</para>
        /// </summary>
        static string OptsJs(string preset, string anim, int frame, string carry, string power,
                             string rest)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("{anim:").Append(Js(anim))
                .Append(",frame:").Append(frame.ToString(c))
                .Append(",build:{preset:").Append(Js(preset)).Append('}')
                .Append(",power:").Append(Js(power == "long" ? "long" : "short"));
            if (!string.IsNullOrEmpty(carry)) sb.Append(",carry:").Append(Js(carry));
            if (!string.IsNullOrEmpty(rest)) sb.Append(",rest:").Append(Js(rest));
            return sb.Append('}').ToString();
        }

        static double Num(string s) =>
            double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        static string Js(string s) =>
            "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        static Color32 ParseHex(string hex)
        {
            hex = hex.Trim().TrimStart('#');
            if (hex.Length < 6) throw new FormatException($"'{hex}' is not #rrggbb.");
            return new Color32(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16), 255);
        }
    }
}
