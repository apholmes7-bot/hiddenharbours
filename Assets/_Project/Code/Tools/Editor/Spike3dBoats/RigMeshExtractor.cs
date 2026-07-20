// ⚠️⚠️ THROWAWAY SPIKE CODE — spike/3d-boats. NOT A PIPELINE. NOT FOR MERGE AS-IS. ⚠️⚠️
// Gated behind HH_3D_BOAT_SPIKE (see the asmdef defineConstraints) so main compiles as if this
// folder were not here. Answers ONE question: can a real-time 3D hull look like the baked sprites?
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HiddenHarbours.Tools.RigBaking;
using UnityEngine;

namespace HiddenHarbours.Tools.Spike3dBoats
{
    /// <summary>One flat-shaded facet, exactly as the rig's face list holds it.</summary>
    public sealed class RigFace
    {
        public Vector3[] V;      // rig-space object coords, metres, z-up
        public int Mat;          // index into <see cref="RigMaterial"/>
        public float B;          // per-face shade bias, the rig's f.b
        public float Db;         // per-face depth bias toward camera, the rig's f.db
    }

    /// <summary>A MATS entry: a palette ramp plus a constant index offset.</summary>
    public sealed class RigMaterial
    {
        public string Name;
        public Color32[] Ramp;
        public int Off;
    }

    public sealed class RigMeshData
    {
        public List<RigFace> Faces = new();
        public List<RigMaterial> Materials = new();
        public Vector3 LightN;          // the rig's LN, normalised
        public float Gain, Bias;
        public Color32 Keyline;
        public int W, H;
        public float PivotX, PivotY;    // cell pixels from TOP-LEFT
        public int PxPerMetre;
        public float DefaultElev;
    }

    /// <summary>
    /// Pulls the STATIC face list out of an art-director rig.
    ///
    /// ⚠️ THE ONE UGLY THING IN THIS SPIKE. The rigs are IIFEs and `F` is closure-private — the
    /// public API (render, ROCK, helmSeat, tubMounts, …) deliberately does not expose it. So this
    /// does a targeted IN-MEMORY string substitution on the source to widen the returned object
    /// literal, and executes THAT. The file on disk is never touched and must never be:
    /// docs/art/rigs/** is the art director's source (ADR 0021 §5).
    ///
    /// In production this hack does not exist. The art director adds ONE property to the exported
    /// literal (`F,`) and this class becomes a plain read. That is the entire delta.
    /// </summary>
    public static class RigMeshExtractor
    {
        public static RigMeshData Extract(IRigScriptHost host, string rigKey)
        {
            var entry = RigCatalog.Get(rigKey);
            return ExtractFrom(host, entry.ScriptPath, entry.GlobalName);
        }

        /// <summary>
        /// Same, for a rig not yet in <see cref="RigCatalog"/>. The side dragger is not, and adding
        /// her would mean editing a tools-editor-owned file from a spike branch for no gain — in
        /// production she is one catalog entry, exactly as the lobster boat is.
        /// </summary>
        public static RigMeshData ExtractFrom(IRigScriptHost host, string scriptPath, string globalName)
        {
            string full = Path.Combine(RigCatalog.RepoRoot, scriptPath);
            if (!File.Exists(full))
                throw new FileNotFoundException($"Rig source missing at {full}.", full);
            // Read unmodified except for the one widening below; the file on disk is never written.
            string src = File.ReadAllText(full);
            string g = globalName;

            // --- the spike-only widening. Loud, single-site, and asserted. -------------------
            string needle = $"root.{g} = {{";
            int at = src.IndexOf(needle, StringComparison.Ordinal);
            if (at < 0)
                throw new InvalidOperationException(
                    $"Could not find `{needle}` in {scriptPath}. This spike assumes the rig " +
                    "ends with a single object literal assigned to root.<Global>. If the rig " +
                    "changed shape, this hack must be re-aimed — or better, retired in favour of " +
                    "the art director exporting F.");
            src = src.Insert(at + needle.Length,
                " __spikeF:F, __spikeMATS:MATS, __spikeGAIN:GAIN, __spikeBIAS:BIAS, __spikeLN:LN,");
            host.Execute(src);

            if (!host.EvaluateBool($"typeof {g}.__spikeF === 'object' && {g}.__spikeF.length > 0"))
                throw new InvalidOperationException(
                    $"{g}.__spikeF did not survive the in-memory widening.");

            var data = new RigMeshData
            {
                W = (int)host.EvaluateNumber($"{g}.W"),
                H = (int)host.EvaluateNumber($"{g}.H"),
                PivotX = (float)host.EvaluateNumber($"{g}.pivot.x"),
                PivotY = (float)host.EvaluateNumber($"{g}.pivot.y"),
                PxPerMetre = (int)host.EvaluateNumber($"{g}.PX"),
                DefaultElev = (float)host.EvaluateNumber($"{g}.defaultElev"),
                Gain = (float)host.EvaluateNumber($"{g}.__spikeGAIN"),
                Bias = (float)host.EvaluateNumber($"{g}.__spikeBIAS"),
                LightN = new Vector3(
                    (float)host.EvaluateNumber($"{g}.__spikeLN[0]"),
                    (float)host.EvaluateNumber($"{g}.__spikeLN[1]"),
                    (float)host.EvaluateNumber($"{g}.__spikeLN[2]")),
                Keyline = ParseHex(host.EvaluateString($"{g}.KEY")),
            };

            // --- materials: name|off|#rrggbb,#rrggbb,… ; … ----------------------------------
            string matBlob = host.EvaluateString(
                $"(function(){{var M={g}.__spikeMATS,o=[];for(var k in M)" +
                "o.push(k+'|'+(M[k].off||0)+'|'+M[k].ramp.join(','));return o.join(';');})()");
            var matIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string part in matBlob.Split(';'))
            {
                string[] f = part.Split('|');
                var ramp = Array.ConvertAll(f[2].Split(','), ParseHex);
                matIndex[f[0]] = data.Materials.Count;
                data.Materials.Add(new RigMaterial
                {
                    Name = f[0],
                    Off = int.Parse(f[1], CultureInfo.InvariantCulture),
                    Ramp = ramp,
                });
            }

            // --- face list, as a packed binary blob through the BULK ReadBytes path ---------
            // Layout: [i32 faceCount] then per face [i32 nv][i32 matId][f32 b][f32 db][nv*3 f32].
            // Material names are resolved to ids JS-side against the same key order used above.
            var matOrder = new StringBuilder();
            foreach (var m in data.Materials) matOrder.Append('"').Append(m.Name).Append('"').Append(',');
            string packer =
                $"globalThis.__spikePack=(function(){{var F={g}.__spikeF;" +
                $"var order=[{matOrder.ToString().TrimEnd(',')}];" +
                "var ix={};order.forEach(function(n,i){ix[n]=i;});" +
                "var n=0;for(var i=0;i<F.length;i++)n+=4+F[i].v.length*3;" +
                "var buf=new ArrayBuffer(4+n*4);var dv=new DataView(buf);var p=0;" +
                "dv.setInt32(p,F.length,true);p+=4;" +
                "for(var i=0;i<F.length;i++){var f=F[i];" +
                "dv.setInt32(p,f.v.length,true);p+=4;" +
                "dv.setInt32(p,(ix[f.mat]==null?ix['hull']:ix[f.mat]),true);p+=4;" +
                "dv.setFloat32(p,f.b||0,true);p+=4;dv.setFloat32(p,f.db||0,true);p+=4;" +
                "for(var k=0;k<f.v.length;k++){var v=f.v[k];" +
                "dv.setFloat32(p,v[0],true);p+=4;dv.setFloat32(p,v[1],true);p+=4;" +
                "dv.setFloat32(p,v[2],true);p+=4;}}" +
                "return new Uint8ClampedArray(buf);})();";
            host.Execute(packer);
            byte[] blob = host.EvaluateBytes("globalThis.__spikePack");

            int off = 0;
            int faceCount = BitConverter.ToInt32(blob, off); off += 4;
            for (int i = 0; i < faceCount; i++)
            {
                int nv = BitConverter.ToInt32(blob, off); off += 4;
                int mat = BitConverter.ToInt32(blob, off); off += 4;
                float b = BitConverter.ToSingle(blob, off); off += 4;
                float db = BitConverter.ToSingle(blob, off); off += 4;
                var vs = new Vector3[nv];
                for (int k = 0; k < nv; k++)
                {
                    vs[k] = new Vector3(BitConverter.ToSingle(blob, off),
                                        BitConverter.ToSingle(blob, off + 4),
                                        BitConverter.ToSingle(blob, off + 8));
                    off += 12;
                }
                data.Faces.Add(new RigFace { V = vs, Mat = mat, B = b, Db = db });
            }
            return data;
        }

        static Color32 ParseHex(string hex)
        {
            hex = hex.Trim().TrimStart('#');
            return new Color32(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16), 255);
        }
    }
}
