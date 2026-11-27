#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The landing pier's fender strings (step <see cref="FendersStep"/>): the two kit tyres that hung at the
    /// lip, where no hull reaches, give way to one fender string per hanger of the south face's fender run
    /// (<see cref="StPetersWharf.FenderRun"/>). Each string keeps the fitting name <c>Fitting_tyre</c>, so
    /// <see cref="LandingDock"/> still owns its placement, and is cut at the live tide by its own height map.
    /// Idempotent: a second plan on the result is empty.
    /// </summary>
    public static partial class StPetersLayerRefresh
    {
        public const string FendersStep = "landing-fenders";
        const string FenderFittingName = "Fitting_tyre";

        internal static ObjRef TidalFaceMaterialRef() => new ObjRef(2100000,
            Regex.Match(File.ReadAllText("Assets/_Project/Resources/TidalFace.mat.meta"), @"(?m)^guid: (\w+)").Groups[1].Value, 2);

        internal static SpriteRef FenderSprite(Sprite sprite)
        {
            if (sprite == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id))
                throw new Refusal("fenders: the fender art has no imported sprite.");
            return new SpriteRef(new ObjRef(id, guid, 3), sprite.rect.size / sprite.pixelsPerUnit);
        }

        internal static ObjRef TextureRef(Texture2D texture)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture));
            if (string.IsNullOrEmpty(guid)) throw new Refusal("fenders: the height map is not imported.");
            return new ObjRef(2800000, guid, 3);
        }

        /// <summary>The tidal-face fields of a picture hung from a deck lip, cut by its own height map.</summary>
        internal static string[] HungFaceFields(float lipWorldY, float deckElevation, WharfFenderBaker.FaceArt art) => new[]
        {
            "  _lipWorldY: " + F(lipWorldY), "  _lipElevation: " + F(deckElevation),
            "  _ridesTheTide: 1", "  _configured: 1",
            "  _heightTexture: " + TextureRef(art.Height).Yaml,
            "  _heightDecode: " + Vec3Yaml(art.MinimumLocalZ, art.HeightRange, -deckElevation),
        };

        public static LayerPatch LandingFenders(SceneYaml scene)
        {
            Doc root = scene.RootNamed(StPetersWharf.RootName), rootTr = scene.TransformOf(root);
            Doc fittings = scene.ChildNamed(rootTr, "Fittings"), fittingsTr = scene.TransformOf(fittings);
            var patch = new LayerPatch(FendersStep, StPetersWharf.RootName, root.FileId) { AddAfter = rootTr.FileId };
            WharfFenderRunDef run = StPetersWharf.FenderRun();
            if (Mathf.Abs(run.LipWorldY - StPetersWharf.DeckFootprint().yMin) > 1e-4f)
                throw new Refusal($"{FendersStep}: {run.Id} hangs from y {F(run.LipWorldY)}, not the pier's south lip.");
            var art = WharfFenderBaker.FaceArtFor(run.ArtKey);
            ObjRef material = TidalFaceMaterialRef();
            SpriteRef sprite = FenderSprite(art.Sprite);
            int order = StPetersWharf.SortingOrderMax + 1;

            var wanted = new HashSet<long>();
            var children = fittingsTr.FieldRefs("m_Children").ToList();
            var xs = run.HangerXs.OrderBy(x => x).ToArray();
            for (int i = 0; i < xs.Length; i++)
            {
                string key = "fender/" + run.Id + "/" + i;
                long go = DockId(key), tr = DockId(key + "/transform"), sr = DockId(key + "/sprite"), face = DockId(key + "/waterline");
                wanted.Add(go);
                string label = $"Fittings/{FenderFittingName}@{F(xs[i])}";
                DockPut(scene, patch, GameObjectYaml(go, FenderFittingName, new[] { tr, sr, face }), label);
                DockPut(scene, patch, TransformYaml(tr, go, new Vector2(xs[i], run.LipWorldY), Array.Empty<long>(), fittingsTr.FileId), label + ": Transform");
                DockPut(scene, patch, SpriteRendererYaml(sr, go, material, sprite, order), label + ": SpriteRenderer");
                DockPut(scene, patch, MonoBehaviourYaml(face, go, DockScript("TidalFaceWaterline"),
                    HungFaceFields(run.LipWorldY, run.DeckElevationMetres, art)), label + ": TidalFaceWaterline");
                if (!children.Contains(tr)) children.Add(tr);
            }
            // Every other Fitting_tyre (the kit's lip tyres, or a string the run no longer names) goes.
            foreach (long child in fittingsTr.FieldRefs("m_Children"))
            {
                Doc go = scene.Require(scene.Require(child, "fitting transform").FieldRef("m_GameObject"), "fitting object");
                if (go.Field("m_Name") != FenderFittingName || wanted.Contains(go.FileId)) continue;
                string label = $"retired {FenderFittingName}@{F(ParseVec3(scene.TransformOf(go).Field("m_LocalPosition")).x)}";
                foreach (Doc c in scene.ComponentsOf(go)) patch.Delete(c, label + ": " + (c.ClassId == TransformClass ? "Transform" : c.ClassId.ToString()));
                patch.Delete(go, label);
                children.Remove(child);
            }
            patch.Edit(fittingsTr, SetRefList(fittingsTr.Text, "m_Children", children), "Fittings: fender strings");
            return patch.Seal(scene);
        }

        /// <summary>One batchmode run: bake the wharf fender kit, then patch St Peters and Nine Mile Creek.
        /// <c>-wharfFenderResult &lt;file&gt;</c> receives the receipt.</summary>
        public static void BakeAndApplyFendersFromCommandLine()
        {
            int code = 1;
            try
            {
                string result = WharfFenderBaker.ResultPath();
                File.WriteAllText(result, "");
                WharfFenderBaker.BakeImportBuild();
                File.AppendAllText(result, "BAKE ok\n");
                ApplyLandingFendersBatch();
                NineMileCreekWharfLandSide.ApplyFendersBatch();
                File.AppendAllText(result, "DONE\n");
                code = 0;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                try { File.AppendAllText(WharfFenderBaker.ResultPath(), "FAILED " + error + "\n"); } catch (Exception) { }
            }
            EditorApplication.Exit(code);
        }

        /// <summary>Patch St Peters (a no-op once applied) and Nine Mile Creek with the kit already baked.
        /// <c>-wharfFenderResult &lt;file&gt;</c> receives the receipt.</summary>
        public static void ApplyFendersFromCommandLine()
        {
            int code = 1;
            try
            {
                File.WriteAllText(WharfFenderBaker.ResultPath(), "");
                ApplyLandingFendersBatch();
                NineMileCreekWharfLandSide.ApplyFendersBatch();
                File.AppendAllText(WharfFenderBaker.ResultPath(), "DONE\n");
                code = 0;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                try { File.AppendAllText(WharfFenderBaker.ResultPath(), "FAILED " + error + "\n"); } catch (Exception) { }
            }
            EditorApplication.Exit(code);
        }

        /// <summary>Patch the banked St Peters file with the fender strings only, then prove the landing dock's
        /// own builders (<see cref="PlanLandingDock"/>) find nothing left to change.</summary>
        public static void ApplyLandingFendersBatch()
        {
            string result = WharfFenderBaker.ResultPath();
            int code = 1;
            GameObject temp = null;
            try
            {
                Directory.SetCurrentDirectory(RigCatalog.RepoRoot);
                temp = new GameObject("LandingFendersProfile");
                var terrain = temp.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(terrain);
                string before = File.ReadAllText(ScenePath);
                var patch = LandingFenders(SceneYaml.Parse(before));
                SceneYaml after = patch.Apply(SceneYaml.Parse(before));
                if (!LandingFenders(after).IsEmpty) throw new Refusal("fenders: a second plan still changes documents.");
                var dock = new List<DockOperation>();
                PlanLandingDock(after, LoadPlanGround(out _), new EditorNavMarkAssets(), dock);   // the marks' own ground
                if (dock.Count != 0)
                    throw new Refusal("fenders: the landing dock builders disagree: " + string.Join(", ", dock.Select(o => o.kind + " " + o.name)));
                File.WriteAllText(ScenePath, after.Render(), new System.Text.UTF8Encoding(false));
                File.AppendAllText(result, $"STP_FENDERS ops={patch.Ops.Count} bytes {before.Length}->{after.Render().Length}\n" +
                    string.Join("\n", patch.Ops.Select(o => "  " + o.Kind + " " + o.Name)) + "\n");
                code = 0;
            }
            catch (Exception error) { File.AppendAllText(result, "STP_FENDERS FAILED " + error + "\n"); Debug.LogException(error); }
            finally { if (temp != null) UnityEngine.Object.DestroyImmediate(temp); }
            if (code != 0) throw new Refusal("fenders: St Peters patch failed.");
        }
    }
}
#endif
