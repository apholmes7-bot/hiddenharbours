#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Nine Mile Creek's fender strings and full-length ladders (step <see cref="FendersStep"/>).
    ///
    /// <para><b>Fenders.</b> The seven kit tyres that hung at the cap, where no hull reaches, give way to
    /// one fender string per hanger of each face's fender run (<see cref="FenderRunPaths"/>), under a
    /// <c>Fenders</c> child of the wharf root. Each string is cut at the live tide by its own height map.</para>
    ///
    /// <para><b>Ladders.</b> Every wharf ladder gets the baked full-length picture (deck to below spring
    /// low), hung where its climb starts, and is told its kind and drawn length, which turns off the
    /// runtime kit-tile span that drew the old small ladder. The old berth-0 kit picture goes.</para>
    ///
    /// <para>Three patches, one per root they touch; each is empty on a second plan.</para>
    /// </summary>
    public static partial class NineMileCreekWharfLandSide
    {
        public const string FendersStep = "19-wharf-fenders-and-ladders";
        public const string WharfRootName = "NineMileCreekWharf";
        public const string FendersNodeName = "Fenders";
        public const string LadderArtKey = "wharfLadderNmc";
        public const string LadderKindPath = "Assets/_Project/Data/Regions/Ladders/WharfLadder_Galvanised.asset";
        public static readonly string[] FenderRunPaths =
        {
            "Assets/_Project/Data/Regions/Fenders/FenderRun_NmcNorthFace.asset",
            "Assets/_Project/Data/Regions/Fenders/FenderRun_NmcUnloadFace.asset",
        };
        const string RetiredTyreName = "Fitting_tyre";
        const string RetiredBerthLadderPicture = "Fitting_ladder_berth0";
        const string WharfLadderType = "HiddenHarbours.World.WharfLadder";

        /// <summary>Fender strings and ladder pictures sort over the cut quay face
        /// (<see cref="NineMileCreekWharf.FixedWaterSideSortingOrder"/>) and under every hull alongside.</summary>
        public static int FenderSortingOrder => NineMileCreekWharf.FixedWaterSideSortingOrder + 1;

        public static IReadOnlyList<WharfFenderRunDef> FenderRuns() => FenderRunPaths
            .Select(p => AssetDatabase.LoadAssetAtPath<WharfFenderRunDef>(p) ?? throw new Refusal(FendersStep + ": no fender run at " + p))
            .ToArray();

        public static IEnumerable<(string step, Func<SceneYaml, LayerPatch> plan)> FenderPlanners()
        {
            yield return (FendersStep, PlaceFendersAndBerthLadder);
            yield return (FendersStep, DressWaterSideLadders);
        }

        static long FenderId(string key)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("nmc-fenders/" + key));
            return (BitConverter.ToInt64(bytes, 0) & 0x3fffffffffffffffL) + 1;
        }

        static void FenderPut(SceneYaml scene, LayerPatch patch, string text, string name)
        {
            text = text.Replace("\n  m_Name: \n", "\n  m_Name:\n");
            var desired = new Doc(text);
            Doc current = scene.Get(desired.FileId);
            if (current == null) { patch.Add(text, name); return; }
            if (current.ClassId != desired.ClassId || current.FieldRef("m_GameObject") != desired.FieldRef("m_GameObject") ||
                (current.ClassId == GameObjectClass && current.Field("m_Name") != desired.Field("m_Name")))
                throw new Refusal(FendersStep + ": a generated document collides with another owner: " + name);
            patch.Edit(current, text, name);
        }

        static string FenderRefList(string text, string key, IList<long> ids)
        {
            var entries = ids.Select(id => "  - {fileID: " + Id(id) + "}").ToArray();
            string value = entries.Length == 0 ? "  " + key + ": []" : "  " + key + ":\n" + string.Join("\n", entries);
            var rx = new Regex(@"(?m)^  " + Regex.Escape(key) + @":(?: \[\])?(?:\n  - [^\n]*)*");
            if (rx.Matches(text).Count != 1) throw new Refusal(FendersStep + ": missing unique list " + key);
            return rx.Replace(text, _ => value);
        }

        static Vector2 WorldOf(SceneYaml scene, Doc tr)
        {
            Vector2 at = Vector2.zero;
            for (Doc t = tr; t != null; t = scene.Get(t.FieldRef("m_Father")))
            {
                if (t.Field("m_LocalRotation") != "{x: 0, y: 0, z: 0, w: 1}" || t.Field("m_LocalScale") != "{x: 1, y: 1, z: 1}")
                    throw new Refusal(FendersStep + ": a ladder hangs under a rotated or scaled parent &" + t.FileId);
                at += (Vector2)ParseVec3(t.Field("m_LocalPosition"));
            }
            return at;
        }

        /// <summary>The run whose face a ladder at <paramref name="at"/> stands on, by the face its climb
        /// starts from.</summary>
        static WharfFenderRunDef FaceOf(IReadOnlyList<WharfFenderRunDef> runs, Vector2 at, float climbTop)
        {
            var run = runs.OrderBy(r => Mathf.Abs(r.LipWorldY - (at.y + climbTop))).First();
            if (Mathf.Abs(run.LipWorldY - (at.y + climbTop)) > 0.01f)
                throw new Refusal($"{FendersStep}: the ladder at ({F(at.x)}, {F(at.y)}) starts on no fender run's lip.");
            return run;
        }

        static float LadderArtLength(WharfFenderBaker.FaceArt art) =>
            JsonUtility.FromJson<LadderBakeOptions>(art.Def.BakedOptionsJson).height;
        [Serializable] sealed class LadderBakeOptions { public float height, top; }

        /// <summary>The kind and drawn-length fields every Nine Mile Creek ladder carries, in the order this step
        /// appends them; the water side's placement (step 18) writes the same two lines, so the steps agree.</summary>
        internal static string[] LadderKindFields()
        {
            var (_, kind, length) = LadderArt();
            return new[] { "  _kind: " + WaterAsset(AssetDatabase.GetAssetPath(kind)).Yaml, "  _lengthMetres: " + F(length) };
        }

        static string LadderFields(string text, WharfLadderDef kind, float length)
        {
            string kindRef = WaterAsset(AssetDatabase.GetAssetPath(kind)).Yaml;
            foreach (var (key, value) in new[] { ("_kind", kindRef), ("_lengthMetres", F(length)) })
                text = Regex.IsMatch(text, @"(?m)^  " + key + ":") ? SetField(text, key, value) : text + "\n  " + key + ": " + value;
            return text;
        }

        static IEnumerable<(Doc go, Doc ladder, Vector2 at, float climbTop)> Ladders(SceneYaml scene, long rootGo)
        {
            var mine = scene.SubtreeOf(rootGo);
            foreach (Doc d in scene.Docs.Where(d => mine.Contains(d.FileId) && d.IsScript(WharfLadderType)))
            {
                Doc go = scene.Require(d.FieldRef("m_GameObject"), "a ladder object");
                float climbTop = d.Field("_climbTopScreenOffset") is string c ? float.Parse(c, System.Globalization.CultureInfo.InvariantCulture) : 0f;
                yield return (go, d, WorldOf(scene, scene.TransformOf(go)), climbTop);
            }
        }

        static (WharfFenderBaker.FaceArt ladder, WharfLadderDef kind, float length) LadderArt()
        {
            var art = WharfFenderBaker.FaceArtFor(LadderArtKey);
            var kind = AssetDatabase.LoadAssetAtPath<WharfLadderDef>(LadderKindPath) ?? throw new Refusal(FendersStep + ": no ladder kind");
            return (art, kind, LadderArtLength(art));
        }

        static string HungFace(long id, long go, float lip, float deck, WharfFenderBaker.FaceArt art) =>
            MonoBehaviourYaml(id, go, WaterScript("Assets/_Project/Code/Art/TidalFaceWaterline.cs", "HiddenHarbours.Art", "HiddenHarbours.Art.TidalFaceWaterline"), new[]
            {
                "  _lipWorldY: " + F(lip), "  _lipElevation: " + F(deck), "  _ridesTheTide: 1", "  _configured: 1",
                "  _heightTexture: " + TextureRef(art.Height).Yaml,
                "  _heightDecode: " + Vec3Yaml(art.MinimumLocalZ, art.HeightRange, -deck),
            });

        /// <summary>The wharf root: retire the cap tyres and the berth-0 kit picture, hang every fender string
        /// and every ladder's full-length picture under one <c>Fenders</c> node, and tell the berth-0 ladder its
        /// kind and length.</summary>
        public static LayerPatch PlaceFendersAndBerthLadder(SceneYaml scene)
        {
            Doc root = scene.RootNamed(WharfRootName), rootTr = scene.TransformOf(root);
            var patch = new LayerPatch(FendersStep, WharfRootName, root.FileId) { AddAfter = rootTr.FileId };
            var runs = FenderRuns();
            var (ladderArt, kind, length) = LadderArt();
            ObjRef material = TidalFaceMaterialRef();
            int order = FenderSortingOrder;

            long nodeGo = FenderId("node"), nodeTr = FenderId("node/transform");
            var nodeChildren = new List<long>();
            foreach (var run in runs)
            {
                if (run.RegionScene != Path.GetFileNameWithoutExtension(NineMileCreekLayerRefresh.ScenePath))
                    throw new Refusal($"{FendersStep}: {run.Id} belongs to {run.RegionScene}.");
                var art = WharfFenderBaker.FaceArtFor(run.ArtKey);
                SpriteRef sprite = FenderSprite(art.Sprite);
                float baked = JsonUtility.FromJson<LadderBakeOptions>(art.Def.BakedOptionsJson).top;
                if (Mathf.Abs(baked - run.DeckElevationMetres) > 1e-4f)
                    throw new Refusal($"{FendersStep}: {run.Id} stands on a {F(run.DeckElevationMetres)} m deck but {run.ArtKey} is baked for {F(baked)} m.");
                float[] xs = run.HangerXs.OrderBy(x => x).ToArray();
                for (int i = 0; i < xs.Length; i++)
                {
                    string key = run.Id + "/" + i, name = "Fender_" + run.Id.Substring(run.Id.IndexOf('.') + 1) + "_" + i;
                    long go = FenderId(key), tr = FenderId(key + "/transform"), sr = FenderId(key + "/sprite"), face = FenderId(key + "/waterline");
                    nodeChildren.Add(tr);
                    FenderPut(scene, patch, GameObjectYaml(go, name, new[] { tr, sr, face }), "Fenders/" + name);
                    FenderPut(scene, patch, TransformYaml(tr, go, new Vector2(xs[i], run.LipWorldY), Array.Empty<long>(), nodeTr), name + ": Transform");
                    FenderPut(scene, patch, SpriteRendererYaml(sr, go, material, sprite, order), name + ": SpriteRenderer");
                    FenderPut(scene, patch, HungFace(face, go, run.LipWorldY, run.DeckElevationMetres, art), name + ": TidalFaceWaterline");
                }
            }

            // Every wharf ladder's picture, the berth-0 ladder's (this root) and the water side's.
            SpriteRef ladderSprite = FenderSprite(ladderArt.Sprite);
            var ladders = Ladders(scene, root.FileId).ToList();
            Doc waterRoot = scene.RootNamed(NineMileCreekWharfWaterSide.RootName);
            ladders.AddRange(Ladders(scene, waterRoot.FileId));
            foreach (var (go0, ladder, at, climbTop) in ladders.OrderBy(l => l.at.x))
            {
                string id = ladder.Field("_id"), name = "Ladder_" + id;
                var run = FaceOf(runs, at, climbTop);
                long go = FenderId("ladder/" + id), tr = FenderId("ladder/" + id + "/transform"),
                     sr = FenderId("ladder/" + id + "/sprite"), face = FenderId("ladder/" + id + "/waterline");
                nodeChildren.Add(tr);
                FenderPut(scene, patch, GameObjectYaml(go, name, new[] { tr, sr, face }), "Fenders/" + name);
                FenderPut(scene, patch, TransformYaml(tr, go, new Vector2(at.x, run.LipWorldY), Array.Empty<long>(), nodeTr), name + ": Transform");
                FenderPut(scene, patch, SpriteRendererYaml(sr, go, material, ladderSprite, order), name + ": SpriteRenderer");
                FenderPut(scene, patch, HungFace(face, go, run.LipWorldY, run.DeckElevationMetres, ladderArt), name + ": TidalFaceWaterline");
                if (scene.SubtreeOf(root.FileId).Contains(ladder.FileId))
                    patch.Edit(ladder, LadderFields(ladder.Text, kind, length), id + ": kind and drawn length");
            }
            FenderPut(scene, patch, GameObjectYaml(nodeGo, FendersNodeName, new[] { nodeTr }), WharfRootName + "/" + FendersNodeName);
            FenderPut(scene, patch, TransformYaml(nodeTr, nodeGo, Vector2.zero, nodeChildren, rootTr.FileId), FendersNodeName + ": Transform");

            // The retired kit pictures under Fittings.
            Doc fittingsTr = scene.TransformOf(scene.ChildNamed(rootTr, "Fittings"));
            var fittingChildren = fittingsTr.FieldRefs("m_Children").ToList();
            foreach (long child in fittingsTr.FieldRefs("m_Children"))
            {
                Doc go = scene.Require(scene.Require(child, "fitting transform").FieldRef("m_GameObject"), "fitting object");
                string name = go.Field("m_Name");
                if (name != RetiredTyreName && name != RetiredBerthLadderPicture) continue;
                string label = $"retired {name}@{F(ParseVec3(scene.TransformOf(go).Field("m_LocalPosition")).x)}";
                foreach (Doc c in scene.ComponentsOf(go)) patch.Delete(c, label + ": " + c.ClassId + " &" + c.FileId);
                patch.Delete(go, label);
                fittingChildren.Remove(child);
            }
            patch.Edit(fittingsTr, FenderRefList(fittingsTr.Text, "m_Children", fittingChildren), "Fittings: retired cap tyres and berth-0 picture");
            patch.Edit(rootTr, FenderRefList(rootTr.Text, "m_Children", rootTr.FieldRefs("m_Children").Concat(new[] { nodeTr }).Distinct().ToList()),
                WharfRootName + ": fenders child");
            return patch.Seal(scene);
        }

        /// <summary>The water side's ladders: each told its kind and drawn length.</summary>
        public static LayerPatch DressWaterSideLadders(SceneYaml scene)
        {
            Doc root = scene.RootNamed(NineMileCreekWharfWaterSide.RootName);
            var patch = new LayerPatch(FendersStep, NineMileCreekWharfWaterSide.RootName, root.FileId);
            var (_, kind, length) = LadderArt();
            foreach (var (_, ladder, _, _) in Ladders(scene, root.FileId))
                patch.Edit(ladder, LadderFields(ladder.Text, kind, length), ladder.Field("_id") + ": kind and drawn length");
            return patch.Seal(scene);
        }

        /// <summary>Apply only this step to the banked file, prove a second plan is empty, and prove the whole
        /// Nine Mile Creek refresh (every planner, this one included) leaves the result unchanged.</summary>
        public static void ApplyFendersBatch()
        {
            string result = WharfFenderBaker.ResultPath();
            string path = NineMileCreekLayerRefresh.ScenePath;
            string before = File.ReadAllText(path), text = before;
            var lines = new List<string>();
            foreach (var (name, plan) in FenderPlanners())
            {
                var patch = plan(SceneYaml.Parse(text));
                lines.Add($"  {patch.Root}: {patch.Ops.Count} ops");
                lines.AddRange(patch.Ops.Select(o => "    " + o.Kind + " " + o.Name));
                text = patch.ApplyTo(text);
            }
            foreach (var (name, plan) in FenderPlanners())
                if (!plan(SceneYaml.Parse(text)).IsEmpty) throw new Refusal(FendersStep + ": a second plan still changes documents.");
            // The whole refresh (ground steps included) is the strongest proof, but it refuses on the banked
            // scene before this step runs when its ground rows are stale (the floating dock it names is gone),
            // so the receipt records that and falls back to every wharf land-side planner, this step's too.
            string again = null, refresh;
            try
            {
                NineMileCreekGroundSteps.PlanAll(text, new NineMileCreekGroundSteps.Inputs(
                    NineMileCreekGround.Load(), new NineMileCreekGroundSteps.EditorBerths()), out again);
                refresh = "whole";
            }
            catch (Refusal stale)
            {
                bool staleOnBase = false;
                try
                {
                    NineMileCreekGroundSteps.PlanAll(before, new NineMileCreekGroundSteps.Inputs(
                        NineMileCreekGround.Load(), new NineMileCreekGroundSteps.EditorBerths()), out _);
                }
                catch (Refusal) { staleOnBase = true; }
                if (!staleOnBase) throw;
                refresh = "landSideOnly (ground steps refuse on the banked scene too: " + stale.Message + ")";
                again = text;
                foreach (var (name, plan) in Planners())
                {
                    var p = plan(SceneYaml.Parse(text));
                    if (!p.IsEmpty) { again = p.ApplyTo(text); lines.Add($"  NOT EMPTY {name} on {p.Root}: {p.Ops.Count} ops"); break; }
                }
            }
            bool fixedPoint = again == text;
            lines.Insert(0, "  refresh=" + refresh);
            File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
            File.AppendAllText(result, $"NMC_FENDERS bytes {before.Length}->{text.Length} refreshFixedPoint={fixedPoint}\n" + string.Join("\n", lines) + "\n");
            if (!fixedPoint)
            {
                File.WriteAllText(Path.ChangeExtension(result, ".nmc-refresh.unity"), again, new System.Text.UTF8Encoding(false));
                throw new Refusal(FendersStep + ": the Nine Mile Creek refresh would change the patched scene.");
            }
        }
    }
}
#endif
