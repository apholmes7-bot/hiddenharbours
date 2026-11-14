using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class CharacterSkinAssetBaker
    {
        public const string SeatedHelmContractPath = "docs/art/rigs/character/rig10/seated-helm.contract.json";

        static string[] WithSeatedHelmStates(string[] legacy)
        {
            var states = new List<string>(legacy);
            foreach (var row in ReadSeatedHelmContract().rows) states.Add(row.state);
            return states.ToArray();
        }

        [Serializable] public sealed class SeatedHelmRow
        {
            public string hull, visual, state, walk, walkSha256;
            /// <summary>The rig clip this row calibrates. Empty means the cockpit <c>drive</c> clip.</summary>
            public string clip;
            /// <summary>For an <c>astride</c> row: the machine's seat, grips and footwells in the figure's frame.</summary>
            public SaddlePins saddle;
            public string Clip => string.IsNullOrEmpty(clip) ? "drive" : clip;
            public bool IsAstride => Clip == "astride";
            public Vector3 floorOrigin, seat, wheel;
            public float seatZ, wheelZ, wheelY;
            public string OptionsJson => IsAstride
                ? "{\"saddle\":{\"seat\":" + Triple(saddle.seat) + ",\"gripL\":" + Triple(saddle.gripL) +
                  ",\"gripR\":" + Triple(saddle.gripR) + ",\"pegL\":" + Triple(saddle.pegL) +
                  ",\"pegR\":" + Triple(saddle.pegR) + "}}"
                : "{\"seatZ\":" + seatZ.ToString("R", CultureInfo.InvariantCulture) +
                ",\"wheelZ\":" + wheelZ.ToString("R", CultureInfo.InvariantCulture) +
                ",\"wheelY\":" + wheelY.ToString("R", CultureInfo.InvariantCulture) + "}";
        }
        [Serializable] public sealed class SaddlePins { public Vector3 seat, gripL, gripR, pegL, pegR; }
        static string Triple(Vector3 v) => "[" + v.x.ToString("R", CultureInfo.InvariantCulture) + "," +
            v.y.ToString("R", CultureInfo.InvariantCulture) + "," + v.z.ToString("R", CultureInfo.InvariantCulture) + "]";
        [Serializable] public sealed class SeatedHelmContract
        {
            public string schema, rig, poses, rigSha256, posesSha256;
            public SeatedHelmRow[] rows;
        }

        public static SeatedHelmContract ReadSeatedHelmContract()
        {
            string abs = Path.Combine(RigCatalog.RepoRoot, SeatedHelmContractPath);
            var c = JsonUtility.FromJson<SeatedHelmContract>(File.ReadAllText(abs));
            if (c == null || c.schema != "hidden-harbours/seated-helm@1" || c.rows == null || c.rows.Length == 0)
                throw new InvalidOperationException("Invalid seated helm contract.");
            if (CharacterSkinExtractor.LfSha256File(Path.Combine(RigCatalog.RepoRoot, c.rig)) != c.rigSha256 ||
                CharacterSkinExtractor.LfSha256File(Path.Combine(RigCatalog.RepoRoot, c.poses)) != c.posesSha256)
                throw new InvalidOperationException("Seated helm source pins are stale.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in c.rows)
                if (string.IsNullOrEmpty(row.state) || !keys.Add(row.state) || (row.IsAstride && row.saddle == null) ||
                    CharacterSkinExtractor.LfSha256File(Path.Combine(RigCatalog.RepoRoot, row.walk)) != row.walkSha256)
                    throw new InvalidOperationException("Seated helm walk pin/state is stale or duplicated.");
            return c;
        }

        static void AppendSeatedHelms(IRigScriptHost host, CharacterSkinDef def, RigBone[] bones, bool enable)
        {
            var contract = ReadSeatedHelmContract();
            string sha = CharacterSkinExtractor.LfSha256File(Path.Combine(RigCatalog.RepoRoot, SeatedHelmContractPath));
            var clips = new List<CharacterSkinDef.SkinClip>(def.Clips ?? Array.Empty<CharacterSkinDef.SkinClip>());
            var states = new List<string>(def.MeshStates ?? Array.Empty<string>());
            foreach (var row in contract.rows)
            {
                var rc = CharacterSkinExtractor.ReadClip9(host, def.Preset, row.Clip, bones, out _, row.OptionsJson);
                float worst = 0; string at = "";
                var clip = ToClip(rc, row.state, bones.Length, ref worst, ref at);
                clip.Face = rc.Face; clip.Tool = rc.Tool; clip.PoseContractSha256 = sha;
                clip.SeatPins = new Vector3[rc.Frames]; clip.HandLeftPins = new Vector3[rc.Frames];
                clip.HandRightPins = new Vector3[rc.Frames]; clip.FootLeftPins = new Vector3[rc.Frames];
                clip.FootRightPins = new Vector3[rc.Frames];
                string g = CharacterSkinExtractor.KitOf(host).GlobalName;
                for (int f = 0; f < rc.Frames; f++)
                {
                    string u = (f / (double)rc.Frames).ToString("R", CultureInfo.InvariantCulture);
                    string packed = host.EvaluateString("(function(){var C=" + g + ",P=C.pinsOf(C.evalClip('" + row.Clip + "'," +
                        u + ",globalThis.__hhClip.build," + row.OptionsJson + "));" +
                        "return P.handL.concat(P.handR,P.footL,P.footR).join(',');})()");
                    var v = packed.Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                    if (v.Length != 12) throw new InvalidOperationException("Seated helm pins are incomplete.");
                    clip.SeatPins[f] = row.seat - row.floorOrigin;
                    clip.HandLeftPins[f] = new Vector3(v[0], v[1], v[2]);
                    clip.HandRightPins[f] = new Vector3(v[3], v[4], v[5]);
                    clip.FootLeftPins[f] = new Vector3(v[6], v[7], v[8]);
                    clip.FootRightPins[f] = new Vector3(v[9], v[10], v[11]);
                }
                clips.RemoveAll(c => c.StateKey == row.state); clips.Add(clip);
                if (enable && !states.Contains(row.state)) states.Add(row.state);
            }
            def.Clips = clips.ToArray();
            if (enable) def.MeshStates = StatesWithClips(def, states.ToArray());
        }

        /// <summary>The batch entry: bake, then leave the editor (0 on success), so a lease is not held idle.</summary>
        public static void BakeSeatedHelmsBatchCli()
        {
            try { BakeSeatedHelmsCli(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Add calibrated clips/pins without rebuilding the ten unchanged bind meshes.</summary>
        public static void BakeSeatedHelmsCli()
        {
            var contract = ReadSeatedHelmContract();
            int skins = 0;
            using (var host = RigScriptHostFactory.Create())
            {
                CharacterSkinExtractor.Load9(host, CharacterRigKit.Rig10);
                foreach (var entry in CastBakeOrder())
                {
                    var def = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(AssetPathFor(entry.preset));
                    if (def == null) throw new InvalidOperationException("Missing supported skin: " + entry.preset);
                    if (def.SourceRigSha256 != contract.rigSha256 || def.BaseRigSha256 != contract.posesSha256)
                        throw new InvalidOperationException("Committed skin source pins are stale: " + entry.preset);
                    var bones = CharacterSkinExtractor.ReadSkeleton9(host, def.Preset);
                    if (bones.Length != def.BoneCount) throw new InvalidOperationException("Skin skeleton changed.");
                    for (int i = 0; i < bones.Length; i++)
                        if (bones[i].Id != def.Bones[i].Id) throw new InvalidOperationException("Skin bone order changed.");
                    AppendSeatedHelms(host, def, bones, enable: true);
                    def.ReachPx = CharacterSkinPose.MeasureReach(def);
                    EditorUtility.SetDirty(def); skins++;
                }
            }
            foreach (var row in contract.rows)
            {
                var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(row.visual);
                if (visual == null) throw new InvalidOperationException("Missing hull visual: " + row.hull);
                visual.SeatedHelmState = row.state; visual.SeatedHelmFloorOrigin = row.floorOrigin;
                EditorUtility.SetDirty(visual);
            }
            AssetDatabase.SaveAssets();
            string report = Path.Combine(RigCatalog.RepoRoot, "Evidence~", "rig-facts", "production", "seated-bake.json");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllText(report, "{\"skins\":" + skins + ",\"hulls\":" + contract.rows.Length + ",\"clips\":" + skins * contract.rows.Length + "}");
            Debug.Log("[seated-helm] baked " + skins + " skins, " + contract.rows.Length + " hulls; unchanged bind meshes.");
        }
    }
}
