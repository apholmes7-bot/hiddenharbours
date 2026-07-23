using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HiddenHarbours.Tools.RigBaking;
using UnityEngine;

namespace HiddenHarbours.SpikeDeckCharacterMesh.Editor
{
    /// <summary>
    /// ⚠️ SPIKE (deck-character-mesh, draft ADR 0024). Extracts PER-POSE face lists from the
    /// character rig — the character counterpart of <see cref="RigMeshExtractor"/>, which cannot
    /// serve here and the difference is the architectural finding of this spike:
    ///
    /// <para><b>The boat rigs build ONE static face list at load</b> (<c>const F = []</c>) and
    /// apply heading/rock as transforms — that is ADR 0022's load-bearing fact and why a hull is
    /// one mesh. <b>The character rig builds its face list PER POSE</b>
    /// (<c>facesOf(pose(anim, u, build))</c>): the skeleton's FK/IK result is baked into the vertex
    /// positions, so there is no static F to widen in. What IS still true: for a FIXED
    /// (anim, frame, build) the face list is deterministic and the heading/rock/heave remain
    /// transforms (<c>camBasis</c> — the same family as the boats). So the spike snapshots one mesh
    /// per pose frame and keeps rotation live.</para>
    ///
    /// <para><b>How it reaches the closure.</b> The same loudly-marked in-memory widening the mesh
    /// extractor uses (<see cref="RigMeshExtractor.WidenExportedLiteral"/> — shared, not copied),
    /// but widening FUNCTIONS (<c>facesOf</c>, <c>pose</c>, <c>makeMats</c>) plus the shading
    /// constants, then assigning <c>CharacterIso.F</c> / <c>CharacterIso.MATS</c> per pose before
    /// reading them back through the same packed-blob layout as <see cref="RigMeshExtractor"/>.
    /// ⚠️ <c>docs/art/rigs/**</c> is READ-ONLY here as everywhere; the file on disk is never
    /// written, and the spike test asserts byte-identity after a run. If this graduates, the export
    /// contract grows the pose surface officially and the widening dies (ADR 0022 open question #4's
    /// pattern).</para>
    /// </summary>
    public static class CharacterPoseMeshExtractor
    {
        public const string ScriptPath = "docs/art/rigs/characterIsoRig.js";
        public const string GlobalName = "CharacterIso";

        /// <summary>The closure symbols the pose extraction needs widened into the export.</summary>
        public static readonly string[] WidenedSymbols =
            { "facesOf", "pose", "makeMats", "GAIN", "BIAS", "LN", "BAYER" };

        /// <summary>
        /// Load the rig into the host with the pose surface widened in. Run once per host; the
        /// rig's public API (render/anchors/…) is unchanged by the widening, so golden renders can
        /// come from the same host.
        /// </summary>
        public static void LoadWidened(IRigScriptHost host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            string full = Path.Combine(RigCatalog.RepoRoot, ScriptPath);
            if (!File.Exists(full))
                throw new FileNotFoundException($"Rig source missing at {full}.", full);

            // READ ONLY — never written back.
            string source = File.ReadAllText(full);
            string widened = RigMeshExtractor.WidenExportedLiteral(
                source, GlobalName, WidenedSymbols, ScriptPath);
            host.Execute(widened);

            foreach (string sym in WidenedSymbols)
                if (!host.EvaluateBool($"typeof {GlobalName}.{sym} !== 'undefined'"))
                    throw new InvalidOperationException(
                        $"Widening {ScriptPath} did not surface '{sym}' — the rig's shape changed. " +
                        "Re-aim the spike extractor (and do NOT edit docs/art/rigs/**).");
        }

        /// <summary>
        /// Extract one pose's faces + the shared render facts as a <see cref="RigMeshData"/> —
        /// the exact input <see cref="RigMeshBuilder"/> and the reference rasterizer already accept,
        /// which is what lets the spike reuse the whole ADR 0022 tail end unchanged.
        /// </summary>
        /// <param name="build">A BUILDS key ('fisher', 'skipper', 'ginny').</param>
        /// <param name="anim">An ANIMS key ('idle', 'hold', …).</param>
        /// <param name="frame">Frame index within the anim's cycle.</param>
        public static RigMeshData ExtractPose(IRigScriptHost host, string build, string anim, int frame)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            string g = GlobalName;
            var c = CultureInfo.InvariantCulture;

            if (!host.EvaluateBool($"typeof {g}.ANIMS[{Js(anim)}] === 'object'"))
                throw new ArgumentException($"{g}.ANIMS has no '{anim}'.", nameof(anim));

            // Bake THIS pose's face list + material table onto the global, from the rig's own
            // functions. u = frame/frames, exactly the rig's resolveOpts arithmetic.
            host.Execute(
                "(function(){var C=" + g + ";" +
                "var b=Object.assign({}, C.BUILDS[" + Js(build) + "]||C.BUILDS.fisher);" +
                "var A=C.ANIMS[" + Js(anim) + "];" +
                "var u=(((" + frame.ToString(c) + ")%A.frames+A.frames)%A.frames)/A.frames;" +
                "C.MATS=C.makeMats(b).MATS;" +
                "C.F=C.facesOf(C.pose(" + Js(anim) + ", u, b, 'short', null, null), b);})()");

            var data = new RigMeshData
            {
                RigKey = $"{g}:{build}:{anim}:{frame}",
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
                BayerWasExported = true,   // widened in — read below, never assumed canonical
                ShimmedSymbols = WidenedSymbols,
            };

            ReadBayer(host, g, data);
            ReadMaterials(host, g, data);
            ReadFaces(host, g, data);

            if (data.Faces.Count == 0)
                throw new InvalidOperationException(
                    $"{g}.facesOf produced no faces for {build}/{anim}[{frame}].");
            return data;
        }

        /// <summary>The anim's frame count, off the rig's own table.</summary>
        public static int FrameCount(IRigScriptHost host, string anim) =>
            (int)host.EvaluateNumber($"{GlobalName}.ANIMS[{Js(anim)}].frames");

        /// <summary>The anim's per-frame milliseconds, off the rig's own table.</summary>
        public static double FrameMs(IRigScriptHost host, string anim) =>
            host.EvaluateNumber($"{GlobalName}.ANIMS[{Js(anim)}].ms");

        /// <summary>The rig's own render — the golden truth the extraction is measured against.</summary>
        public static byte[] RenderTruth(IRigScriptHost host, int dir, string anim, int frame) =>
            host.EvaluateBytes(
                $"{GlobalName}.render({dir.ToString(CultureInfo.InvariantCulture)}," +
                $"{{anim:{Js(anim)},frame:{frame.ToString(CultureInfo.InvariantCulture)}}})");

        // ---------------------------------------------------------------------------------------
        // The packed-blob readers below mirror RigMeshExtractor's private ReadBayer/ReadMaterials/
        // ReadFaces byte-for-byte in layout. Copied rather than shared because they are private
        // there and this is a SPIKE — if the spike graduates, RigMeshExtractor's readers get
        // surfaced and these copies die (noted in the draft ADR).
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
                data.Bayer[i / 4, i % 4] = double.Parse(parts[i], CultureInfo.InvariantCulture);
        }

        static void ReadMaterials(IRigScriptHost host, string g, RigMeshData data)
        {
            string blob = host.EvaluateString(
                $"(function(){{var M={g}.MATS,o=[];for(var k in M)" +
                "o.push(k+'|'+(M[k].off||0)+'|'+M[k].ramp.join(','));return o.join(';');})()");

            foreach (string part in blob.Split(';'))
            {
                string[] f = part.Split('|');
                if (f.Length != 3)
                    throw new InvalidOperationException($"{g}.MATS entry '{part}' is not name|off|ramp.");
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
            // Layout, little-endian (RigMeshExtractor's contract):
            //   [i32 faceCount] then per face [i32 nv][i32 matId][f64 b][f64 db][nv × 3 × f64]
            var matOrder = new StringBuilder();
            foreach (var m in data.Materials)
                matOrder.Append(Js(m.Name)).Append(',');

            string packer =
                $"globalThis.__hhSpikePoseMeshPack=(function(){{var F={g}.F;" +
                $"var order=[{matOrder.ToString().TrimEnd(',')}];" +
                "var ix={};order.forEach(function(n,i){ix[n]=i;});" +
                "var n=0;for(var i=0;i<F.length;i++)n+=F[i].v.length*3;" +
                "var buf=new ArrayBuffer(4+F.length*(8+16)+n*8);var dv=new DataView(buf);var p=0;" +
                "dv.setInt32(p,F.length,true);p+=4;" +
                "for(var i=0;i<F.length;i++){var f=F[i];" +
                "var mi=ix[f.mat];if(mi==null)mi=0;" +
                "dv.setInt32(p,f.v.length,true);p+=4;" +
                "dv.setInt32(p,mi,true);p+=4;" +
                "dv.setFloat64(p,f.b||0,true);p+=8;dv.setFloat64(p,f.db||0,true);p+=8;" +
                "for(var k=0;k<f.v.length;k++){var v=f.v[k];" +
                "dv.setFloat64(p,v[0],true);p+=8;dv.setFloat64(p,v[1],true);p+=8;" +
                "dv.setFloat64(p,v[2],true);p+=8;}}" +
                "return new Uint8ClampedArray(buf);})();";
            host.Execute(packer);
            byte[] blob = host.EvaluateBytes("globalThis.__hhSpikePoseMeshPack");

            int off = 0;
            int faceCount = BitConverter.ToInt32(blob, off); off += 4;
            for (int i = 0; i < faceCount; i++)
            {
                int nv = BitConverter.ToInt32(blob, off); off += 4;
                int mat = BitConverter.ToInt32(blob, off); off += 4;
                double b = BitConverter.ToDouble(blob, off); off += 8;
                double db = BitConverter.ToDouble(blob, off); off += 8;
                if (nv < 3)
                    throw new InvalidOperationException($"{g}.F[{i}] has {nv} vertices.");
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
