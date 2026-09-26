using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One painted material as rig 9's <c>shadingContract</c> states it: its own gain, bias
    /// and tone window (lo..hi) over its own ramp, or one fixed colour.</summary>
    public sealed class RigMaterial9
    {
        public string Name;
        public bool Fixed;
        public Color32[] Ramp;
        public string[] RampHex;
        public int Off;
        public double Gain = 1.0;
        public double Bias;
        public int Lo;
        public int Hi;

        /// <summary>The same material in the shape the reference rasterizer and the mesh builder
        /// read. Only the ramp colours and the count matter there: the turntable sign is read on
        /// the silhouette, never on the shade.</summary>
        public RigMaterial ToRigMaterial() => new RigMaterial
        {
            Name = Name,
            Ramp = Ramp,
            RampHex = RampHex,
            Off = Off,
            Gain = Gain,
            Bias = Bias,
            OrderedDither = false,
            FixedIndex = -1,
        };
    }

    // Rig 9: Claude Design's character-rig kit v9.2 (docs/art/rigs/character/rig9/). This half
    // READS it, into the shapes rig 7's reader already hands the baker (RigBone, RigSkinning,
    // RigSkinClip), so the def, the mesh builder and the skin attach code stay shared and only the
    // reading differs. What differs from rig 7, and why each difference is met here and not by
    // editing the rig:
    //  - The body IS the skinned export. There is no rig 6 to agree with: bindMesh, skeleton and
    //    clip are rig 9's own, and so is its shading contract (each material's gain, bias, lo, hi).
    //  - Its face is composed by FACE GROUP (eyes.*, brows.*, mouth.*). The bind mesh keeps the
    //    ungrouped faces plus each slot's REST group, FACE_SLOTS[slot][0], which is what baseIntent
    //    rests every preset on. That is asserted per preset, not assumed.
    //  - It exports no TOL and no BAYER. The tolerance is the one its own checks gate on
    //    (checks.js, "gate 1e-6 m") and the dither matrix is the canonical one.
    //  - normBuild falls back to the fisher without a word, so every preset is checked against
    //    CAST before anything is read under its name.
    //  - Its poses are a second file that must run after the body, and its checks a third.
    public static partial class CharacterSkinExtractor
    {
        public const string V9CatalogKey = "characterRig9";
        public const string V9RigName = "characterIsoRig9";
        public const string V9Revision = "9.2";
        public const int V9Pass = 9;

        /// <summary>The tolerance rig 9's own checks gate on (checks.js, "gate 1e-6 m"). Rig 9
        /// exports no TOL, and borrowing rig 7's would judge one rig by another's bar.</summary>
        public const double V9Tolerance = 1e-6;

        public static RigEntry V9Entry => RigCatalog.Get(V9CatalogKey);
        public static string V9ScriptPath => V9Entry.ScriptPath;
        public static string V9GlobalName => V9Entry.GlobalName;
        public static string V9PosesPath => V9Sidecar("poses");
        public static string V9ChecksPath => V9Sidecar("checks");

        static string V9Sidecar(string part)
        {
            string body = V9ScriptPath;
            if (!body.EndsWith(".js", StringComparison.Ordinal))
                throw new InvalidOperationException($"Rig 9's body '{body}' is not a .js file.");
            return body.Substring(0, body.Length - 3) + "." + part + ".js";
        }

        static string ReadRepoText(string repoRelativePath) =>
            File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, repoRelativePath));

        public static string SourceSha256V9() => LfSha256(V9ScriptPath);
        public static string PosesSha256V9() => LfSha256(V9PosesPath);

        // ---------------------------------------------------------------------------------------
        // Loading
        // ---------------------------------------------------------------------------------------

        /// <summary>Install rig 9 and its poses in <paramref name="host"/>, once. A host that already
        /// holds revision 9.2 with its poses is left as it is; re-running the body would wipe what a
        /// sidecar added (the checks) for nothing.</summary>
        public static void Load9(IRigScriptHost host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (host.EvaluateBool(Loaded9Js())) return;

            RigCatalog.InstallModule(host, V9Entry);
            host.Execute(ReadRepoText(V9PosesPath));

            string g = V9GlobalName;
            if (!host.EvaluateBool(Loaded9Js()))
                throw new InvalidOperationException(
                    $"{V9PosesPath} ran and {g} says posesLoaded=" +
                    host.EvaluateString($"String({g}.posesLoaded)") + ", revision " +
                    host.EvaluateString($"String({g}.revision)") + $". This reader is written for rig 9 " +
                    $"revision {V9Revision} with its poses installed; a rig without its poses bakes " +
                    "clips that do not move.");
            string rig = host.EvaluateString($"String({g}.rig)");
            int pass = (int)host.EvaluateNumber($"{g}.pass");
            if (rig != V9RigName || pass != V9Pass)
                throw new InvalidOperationException(
                    $"{V9ScriptPath} says it is '{rig}' pass {pass}; this reader is written for " +
                    $"'{V9RigName}' pass {V9Pass}.");
        }

        /// <summary>Also install rig 9's own checks (runChecks), for the guards that replay them.
        /// Never loaded by the bake.</summary>
        public static void Load9Checks(IRigScriptHost host)
        {
            Load9(host);
            string g = V9GlobalName;
            if (host.EvaluateBool($"typeof {g}.runChecks==='function'")) return;
            host.Execute(ReadRepoText(V9ChecksPath));
            if (!host.EvaluateBool($"typeof {g}.runChecks==='function'"))
                throw new InvalidOperationException($"{V9ChecksPath} ran and {g}.runChecks is still missing.");
        }

        static string Loaded9Js()
        {
            string g = V9GlobalName;
            return $"typeof {g}==='object'&&{g}!==null&&{g}.posesLoaded===true&&" +
                   $"String({g}.revision)==={Js(V9Revision)}";
        }

        /// <summary>The cast rig 9 declares, in its own order.</summary>
        public static string[] Presets9(IRigScriptHost host)
        {
            Load9(host);
            return SplitList(host.EvaluateString($"{V9GlobalName}.CAST.join(',')"));
        }

        /// <summary>Refuse a preset rig 9 does not declare. Its normBuild would quietly answer with
        /// the fisher, and the bake would write the fisher under a cast member's name.</summary>
        public static void AssertPreset9(IRigScriptHost host, string preset)
        {
            if (string.IsNullOrEmpty(preset)) throw new ArgumentNullException(nameof(preset));
            if (!host.EvaluateBool($"{V9GlobalName}.CAST.indexOf({Js(preset)})>=0"))
                throw new ArgumentException(
                    $"{V9GlobalName}.CAST has no preset '{preset}'. Rig 9's normBuild falls back to " +
                    "the fisher without a word, so this would bake the fisher under that name.");
        }

        // ---------------------------------------------------------------------------------------
        // The face at rest
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// The face groups the bind mesh keeps for <paramref name="preset"/>: one per face slot, the
        /// slot's FIRST group (<c>FACE_SLOTS[slot][0]</c>). Refuses a preset whose rest face
        /// (<c>baseIntent(buildOf(p).D).face</c>) is a different group, because then the bind
        /// mesh would wear a face that preset never rests on.
        /// </summary>
        public static string[] DefaultFaceGroups9(IRigScriptHost host, string preset)
        {
            AssertPreset9(host, preset);
            string g = V9GlobalName;
            string[] slots = SplitList(host.EvaluateString($"Object.keys({g}.FACE_SLOTS).join(',')"));
            if (slots.Length == 0)
                throw new InvalidOperationException($"{g}.FACE_SLOTS names no face slot.");
            var groups = new string[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                string first = host.EvaluateString($"String({g}.FACE_SLOTS[{Js(slots[i])}][0])");
                string rest = host.EvaluateString(
                    $"String({g}.baseIntent({g}.buildOf({Js(preset)}).D).face[{Js(slots[i])}])");
                if (!string.Equals(first, rest, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"'{preset}' rests its {slots[i]} on '{rest}' and FACE_SLOTS lists '{first}' " +
                        "first. The bind mesh keeps the first group of each slot, so it would wear a " +
                        "face this preset never rests on.");
                groups[i] = slots[i] + "." + first;
            }
            return groups;
        }

        /// <summary>The JS that yields <paramref name="preset"/>'s bind mesh with its rest face: every
        /// ungrouped face, plus each slot's rest group. It is handed, unchanged, to the skinning
        /// reader, the face reader and the material reader, so all three read the same faces in
        /// the same order.</summary>
        public static string DefaultFaceMeshJs9(IRigScriptHost host, string preset)
        {
            var sb = new StringBuilder(V9GlobalName).Append(".bindMesh(").Append(Js(preset))
                .Append(").filter(function(f){return !f.group");
            foreach (string grp in DefaultFaceGroups9(host, preset))
                sb.Append("||f.group===").Append(Js(grp));
            return sb.Append(";})").ToString();
        }

        // ---------------------------------------------------------------------------------------
        // Skeleton
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Rig 9's skeleton for <paramref name="preset"/>, from ONE <c>skeleton(p)</c> call, into
        /// rig 7's <see cref="RigBone"/>: <c>rest</c> is local to the parent and <c>bind</c> is the
        /// world pose. The checks are rig 7's: the ids agree with the packed count, every parent is
        /// an earlier index, and bone 0 is the root.
        /// </summary>
        public static RigBone[] ReadSkeleton9(IRigScriptHost host, string preset)
        {
            AssertPreset9(host, preset);
            string g = V9GlobalName;
            host.Execute($"globalThis.__hhSkel9={g}.skeleton({Js(preset)});");
            string[] ids = SplitList(host.EvaluateString(
                "globalThis.__hhSkel9.map(function(b){return String(b.id);}).join(',')"));

            // [i32 n] then per bone [i32 parent][f64 rest.pos x3][f64 rest.rot x4]
            //                       [f64 bind.pos x3][f64 bind.rot x4]
            host.Execute(
                "globalThis.__hhSkel9Pack=(function(){var S=globalThis.__hhSkel9;" +
                "var buf=new ArrayBuffer(4+S.length*(4+14*8));var dv=new DataView(buf);var p=0;" +
                "function put(a,n,what){if(!a||a.length!==n)throw new Error(what+' has '+" +
                "(a?a.length:'no')+' components, not '+n);" +
                "for(var k=0;k<n;k++){dv.setFloat64(p,a[k],true);p+=8;}}" +
                "dv.setInt32(p,S.length,true);p+=4;" +
                "for(var i=0;i<S.length;i++){var b=S[i];" +
                "if(typeof b.parent!=='number'||b.parent!==Math.floor(b.parent))" +
                "throw new Error('bone '+b.id+' names parent '+b.parent+', which is not an index');" +
                "dv.setInt32(p,b.parent,true);p+=4;" +
                "put(b.rest&&b.rest.pos,3,b.id+' rest.pos');put(b.rest&&b.rest.rot,4,b.id+' rest.rot');" +
                "put(b.bind&&b.bind.pos,3,b.id+' bind.pos');put(b.bind&&b.bind.rot,4,b.id+' bind.rot');}" +
                "return new Uint8ClampedArray(buf);})();");
            byte[] blob = host.EvaluateBytes("globalThis.__hhSkel9Pack");

            int off = 0;
            int n = BitConverter.ToInt32(blob, off); off += 4;
            if (n != ids.Length)
                throw new InvalidOperationException(
                    $"Rig 9's skeleton('{preset}') packed {n} bones and named {ids.Length}.");
            var bones = new RigBone[n];
            for (int i = 0; i < n; i++)
            {
                var b = new RigBone { Id = ids[i], Parent = BitConverter.ToInt32(blob, off) };
                off += 4;
                b.RestPos = ReadV3(blob, ref off);
                b.RestRx = D(blob, ref off); b.RestRy = D(blob, ref off);
                b.RestRz = D(blob, ref off); b.RestRw = D(blob, ref off);
                b.WorldPos = ReadV3(blob, ref off);
                b.WorldRx = D(blob, ref off); b.WorldRy = D(blob, ref off);
                b.WorldRz = D(blob, ref off); b.WorldRw = D(blob, ref off);
                if (b.Parent >= i || b.Parent < -1)
                    throw new InvalidOperationException(
                        $"Rig 9 bone {i} '{b.Id}' names parent {b.Parent}, which is not an earlier " +
                        "index. The def walks parents before children and would read an unset pose.");
                bones[i] = b;
            }
            if (off != blob.Length)
                throw new InvalidOperationException(
                    $"Rig 9's skeleton blob was {blob.Length} bytes and {off} were read: packer and reader disagree.");
            if (n == 0 || bones[0].Parent != -1)
                throw new InvalidOperationException($"Rig 9's bone 0 for '{preset}' is not the root.");
            return bones;
        }

        // ---------------------------------------------------------------------------------------
        // Materials and faces
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// The painted materials the faces of <paramref name="facesJs"/> use, in the order rig 9's
        /// shading contract declares them. This is the count the engine pays one ramp slot for
        /// each, fixed colours included. Refuses, by name, a face whose material the contract does
        /// not declare, a ramped material with no tone window (lo or hi missing), a window the def
        /// cannot carry (lo below 0 or hi below lo) and a gain or bias that is not a finite number.
        /// Never invents one: a default here would recolour a character without a word.
        /// </summary>
        public static List<RigMaterial9> ReadMaterials9(IRigScriptHost host, string preset, string facesJs)
        {
            AssertPreset9(host, preset);
            string g = V9GlobalName;
            string rows = host.EvaluateString(
                "(function(){var C=" + g + ".shadingContract(" + Js(preset) + ").materials;" +
                "var F=" + facesJs + ";var used={};for(var i=0;i<F.length;i++)used[F[i].mat]=1;" +
                "var stray=Object.keys(used).filter(function(k){" +
                "return !Object.prototype.hasOwnProperty.call(C,k);});" +
                "if(stray.length)return '!'+stray.join(',');" +
                "function s(x){return x==null?'':String(x);}" +
                "return Object.keys(C).filter(function(k){return used[k];}).map(function(k){var c=C[k];" +
                "return [k,c.fixed?1:0,c.fixed?s(c.color):(c.ramp||[]).join(','),s(c.off),s(c.gain)," +
                "s(c.bias),s(c.lo),s(c.hi)].join('|');}).join('\\n');})()");
            if (rows.StartsWith("!", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"'{preset}' paints faces with {rows.Substring(1)}, which its shading contract does " +
                    "not declare. A face with no declared material has no colour the engine could give it.");

            var list = new List<RigMaterial9>();
            foreach (string row in rows.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = row.Split('|');
                if (p.Length != 8)
                    throw new InvalidOperationException($"Rig 9 material row '{row}' has {p.Length} fields, not 8.");
                var m = new RigMaterial9 { Name = p[0], Fixed = p[1] == "1" };
                string where = $"'{preset}' material '{m.Name}'";
                if (m.Fixed)
                {
                    m.RampHex = new[] { p[2] };
                    m.Ramp = new[] { ParseHex9(p[2], where + " colour") };
                }
                else
                {
                    m.RampHex = p[2].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (m.RampHex.Length == 0)
                        throw new InvalidOperationException($"{where} has an empty ramp.");
                    m.Ramp = new Color32[m.RampHex.Length];
                    for (int k = 0; k < m.Ramp.Length; k++)
                        m.Ramp[k] = ParseHex9(m.RampHex[k], $"{where} ramp[{k}]");
                    m.Off = p[3].Length == 0 ? 0 : Int9(p[3], where + " off");
                    m.Gain = Finite9(p[4], where + " gain");
                    m.Bias = Finite9(p[5], where + " bias");
                    if (p[6].Length == 0 || p[7].Length == 0)
                        throw new InvalidOperationException(
                            $"{where} has no tone window (lo '{p[6]}', hi '{p[7]}'). The v9 tone rule " +
                            "clamps every step to lo..hi; the bake refuses rather than invent one.");
                    m.Lo = Int9(p[6], where + " lo");
                    m.Hi = Int9(p[7], where + " hi");
                    if (m.Lo < 0 || m.Hi < m.Lo)
                        throw new InvalidOperationException(
                            $"{where} has the tone window {m.Lo}..{m.Hi}; the def carries 0 <= lo <= hi.");
                }
                list.Add(m);
            }
            return list;
        }

        /// <summary>The faces of <paramref name="facesJs"/>, with each material as an index into
        /// <paramref name="materials"/>. The same packed layout as rig 6's pose reader.</summary>
        public static List<RigFace> ReadFaces9(IRigScriptHost host, string facesJs,
                                               List<RigMaterial9> materials, string label)
        {
            var matOrder = new StringBuilder();
            foreach (RigMaterial9 m in materials) matOrder.Append(Js(m.Name)).Append(',');

            host.Execute(
                "globalThis.__hhFaces9Pack=(function(){var F=" + facesJs + ";" +
                "var order=[" + matOrder.ToString().TrimEnd(',') + "];" +
                "var ix={};order.forEach(function(n,i){ix[n]=i;});" +
                "var n=0;for(var i=0;i<F.length;i++)n+=F[i].v.length*3;" +
                "var buf=new ArrayBuffer(4+F.length*(8+16)+n*8);var dv=new DataView(buf);var p=0;" +
                "dv.setInt32(p,F.length,true);p+=4;" +
                "for(var i=0;i<F.length;i++){var f=F[i];" +
                "var mi=ix[f.mat];if(mi==null)throw new Error('face '+i+' uses material '+f.mat+" +
                "' which is not in this mesh\\'s own material list');" +
                "dv.setInt32(p,f.v.length,true);p+=4;dv.setInt32(p,mi,true);p+=4;" +
                "dv.setFloat64(p,f.b||0,true);p+=8;dv.setFloat64(p,f.db||0,true);p+=8;" +
                "for(var k=0;k<f.v.length;k++){var v=f.v[k];" +
                "dv.setFloat64(p,v[0],true);p+=8;dv.setFloat64(p,v[1],true);p+=8;" +
                "dv.setFloat64(p,v[2],true);p+=8;}}" +
                "return new Uint8ClampedArray(buf);})();");
            byte[] blob = host.EvaluateBytes("globalThis.__hhFaces9Pack");

            int off = 0;
            int faceCount = BitConverter.ToInt32(blob, off); off += 4;
            var faces = new List<RigFace>(faceCount);
            for (int i = 0; i < faceCount; i++)
            {
                int nv = BitConverter.ToInt32(blob, off); off += 4;
                int mat = BitConverter.ToInt32(blob, off); off += 4;
                double b = D(blob, ref off);
                double db = D(blob, ref off);
                if (nv < 3)
                    throw new InvalidOperationException($"{label} face {i} has {nv} vertices.");
                var vs = new Vector3d[nv];
                for (int k = 0; k < nv; k++) vs[k] = ReadV3(blob, ref off);
                faces.Add(new RigFace { V = vs, Mat = mat, B = b, Db = db });
            }
            if (off != blob.Length)
                throw new InvalidOperationException(
                    $"{label} face blob was {blob.Length} bytes and {off} were read: packer and reader disagree.");
            return faces;
        }

        /// <summary>
        /// A <see cref="RigMeshData"/> of rig 9's faces, for the mesh builder (the bind mesh) and
        /// for the reference rasterizer (the turntable sign's oracle). It carries rig 9's cell,
        /// pivot, scale and elevation, its screen key light, gain 1 and bias 0 (each material
        /// carries its own) and the canonical dither, since rig 9 exports none.
        /// </summary>
        public static RigMeshData NewData9(IRigScriptHost host, string preset, string label,
                                           string facesJs, List<RigMaterial9> materials)
        {
            string g = V9GlobalName;
            var data = new RigMeshData
            {
                RigKey = label,
                GlobalName = g,
                SourceFaceExpression = facesJs,
                W = (int)host.EvaluateNumber($"{g}.W"),
                H = (int)host.EvaluateNumber($"{g}.H"),
                PivotX = host.EvaluateNumber($"{g}.pivot.x"),
                PivotY = host.EvaluateNumber($"{g}.pivot.y"),
                PxPerMetre = (int)host.EvaluateNumber($"{g}.PX"),
                DefaultElev = host.EvaluateNumber($"{g}.ELEV"),
                Gain = 1.0,
                Bias = 0.0,
                LightN = V9Shading3(host, "key"),
                Keyline = ParseHex9(host.EvaluateString($"String({g}.SHADING.keyline)"), "SHADING.keyline"),
                BayerWasExported = false,
            };
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    data.Bayer[x, y] = (RigMeshSymbols.CanonicalBayer[x, y] + 0.5) / 16.0;
            foreach (RigMaterial9 m in materials) data.Materials.Add(m.ToRigMaterial());
            data.Faces.AddRange(ReadFaces9(host, facesJs, materials, $"{label} ({preset})"));
            return data;
        }

        /// <summary>A three-number field of rig 9's SHADING block (key, fleetKey).</summary>
        public static Vector3d V9Shading3(IRigScriptHost host, string field)
        {
            string s = $"{V9GlobalName}.SHADING[{Js(field)}]";
            if (!host.EvaluateBool($"Array.isArray({s})&&{s}.length===3&&{s}.every(function(x){{return isFinite(x);}})"))
                throw new InvalidOperationException($"Rig 9's SHADING.{field} is not three finite numbers.");
            return new Vector3d(host.EvaluateNumber($"{s}[0]"), host.EvaluateNumber($"{s}[1]"),
                                host.EvaluateNumber($"{s}[2]"));
        }

        /// <summary>A one-number field of rig 9's SHADING block (form, formMid).</summary>
        public static double V9ShadingNumber(IRigScriptHost host, string field)
        {
            string s = $"{V9GlobalName}.SHADING[{Js(field)}]";
            if (!host.EvaluateBool($"typeof {s}==='number'&&isFinite({s})"))
                throw new InvalidOperationException($"Rig 9's SHADING.{field} is not a finite number.");
            return host.EvaluateNumber(s);
        }

        // ---------------------------------------------------------------------------------------
        // Clips
        // ---------------------------------------------------------------------------------------

        /// <summary>Every clip rig 9 ships, by the name <c>clip(name, p)</c> takes.</summary>
        public static string[] ClipNames9(IRigScriptHost host) =>
            SplitList(host.EvaluateString($"{V9GlobalName}.clipNames().join(',')"));

        /// <summary>The ANIMS rows (the plain clips), for the turntable sign's frame plan.</summary>
        public static string[] Anims9(IRigScriptHost host) =>
            SplitList(host.EvaluateString($"Object.keys({V9GlobalName}.ANIMS).join(',')"));

        public static int FrameCount9(IRigScriptHost host, string anim) =>
            (int)host.EvaluateNumber($"{V9GlobalName}.ANIMS[{Js(anim)}].frames");

        /// <summary>
        /// One rig 9 clip for <paramref name="preset"/>, read by rig 7's clip reader. The clip's own
        /// <c>anim</c> names it, not the name it was asked for: <c>helm_idle</c> is the idle with the
        /// helm carried. So <paramref name="stateKey"/> is <see cref="CharacterState"/>'s key of
        /// (anim, carry), and the bone order is checked against the skeleton's, because the def
        /// stores indices.
        /// </summary>
        public static RigSkinClip ReadClip9(IRigScriptHost host, string preset, string clipName,
                                            RigBone[] bones, out string stateKey)
        {
            AssertPreset9(host, preset);
            host.Execute($"globalThis.__hhClip={V9GlobalName}.clip({Js(clipName)},{Js(preset)});");
            string anim = host.EvaluateString("String(globalThis.__hhClip.anim||'')");
            if (anim.Length == 0)
                throw new InvalidOperationException($"Rig 9 clip '{clipName}' names no anim.");
            string label = $"rig 9 clip '{clipName}' ({preset})";
            RigSkinClip rc = ReadLoadedClip(host, label, anim, bones.Length);
            string[] order = SplitList(host.EvaluateString("globalThis.__hhClip.bones.join(',')"));
            for (int b = 0; b < order.Length; b++)
                if (!string.Equals(order[b], bones[b].Id, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"{label} packs bone {b} as '{order[b]}' and the skeleton has '{bones[b].Id}'. " +
                        "The def stores INDICES, so a permuted clip would animate the right skeleton " +
                        "with the wrong limbs and never throw.");
            stateKey = string.IsNullOrEmpty(rc.Carry) ? rc.Anim : new CharacterState(rc.Anim, null, rc.Carry).Key;
            return rc;
        }

        // ---------------------------------------------------------------------------------------
        // Truth
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Rig 9's own render of <paramref name="clip"/> frame <paramref name="frame"/> facing
        /// <paramref name="dir"/>, as RGBA, and the posed faces it painted (world space) as
        /// <paramref name="posed"/>, with their own materials. The head snap is off: the engine
        /// draws the head where the skeleton puts it, so the truth it is compared with must too.
        /// </summary>
        public static byte[] RenderTruth9(IRigScriptHost host, string preset, string clip, int frame,
                                          int dir, out RigMeshData posed)
        {
            AssertPreset9(host, preset);
            string g = V9GlobalName;
            var c = CultureInfo.InvariantCulture;
            host.Execute($"globalThis.__hhR9={g}.render({{clip:{Js(clip)},frame:{frame.ToString(c)}," +
                         $"dir:{dir.ToString(c)},build:{Js(preset)},snapHead:false}});");
            byte[] rgba = host.EvaluateBytes("globalThis.__hhR9.rgba");
            const string faces = "globalThis.__hhR9.faces";
            posed = NewData9(host, preset, $"{g}:{preset}:{clip}:{frame}:dir {dir}", faces,
                             ReadMaterials9(host, preset, faces));
            if (rgba.Length != posed.W * posed.H * 4)
                throw new InvalidOperationException(
                    $"Rig 9 rendered {rgba.Length} bytes for a {posed.W}x{posed.H} cell.");
            return rgba;
        }

        /// <summary>A one-line census for the bake log.</summary>
        public static string Census9(IRigScriptHost host, string preset)
        {
            string g = V9GlobalName;
            var c = CultureInfo.InvariantCulture;
            return $"{preset}: rig 9 rev {host.EvaluateString($"String({g}.revision)")}, " +
                   ((int)host.EvaluateNumber($"({DefaultFaceMeshJs9(host, preset)}).length")).ToString(c) +
                   " bind faces with the rest face, " +
                   ((int)host.EvaluateNumber($"{g}.skeleton({Js(preset)}).length")).ToString(c) + " bones, " +
                   ClipNames9(host).Length.ToString(c) + " clips";
        }

        // ---------------------------------------------------------------------------------------
        // Small parsers
        // ---------------------------------------------------------------------------------------

        static string[] SplitList(string joined) =>
            joined.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        static Color32 ParseHex9(string hex, string where)
        {
            if (hex == null || hex.Length != 7 || hex[0] != '#')
                throw new InvalidOperationException($"{where} is '{hex}', not a #rrggbb colour.");
            byte H(int i) => byte.Parse(hex.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Color32(H(1), H(3), H(5), 255);
        }

        static int Int9(string s, string where)
        {
            if (!int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v))
                throw new InvalidOperationException($"{where} is '{s}', not a whole number.");
            return v;
        }

        static double Finite9(string s, string where)
        {
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ||
                double.IsNaN(v) || double.IsInfinity(v))
                throw new InvalidOperationException($"{where} is '{s}', not a finite number.");
            return v;
        }
    }
}
