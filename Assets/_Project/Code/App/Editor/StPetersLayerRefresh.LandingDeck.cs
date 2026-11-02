#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class StPetersLayerRefresh
    {
        public const string LandingDeckVisualName = "WharfPass2Deck";

        public static void ColdImportCheckFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-stpK0Result");
            if (at < 0 || at + 1 >= args.Length) throw new Refusal("landing-deck: cold import needs a results path.");
            File.WriteAllText(args[at + 1], "COLD_IMPORT_SUCCESS\n", new UTF8Encoding(false));
            Debug.Log("[StPetersLandingDeck] COLD_IMPORT_SUCCESS");
            EditorApplication.Exit(0);
        }

        /// <summary>Replace only the old visual deck. The floor, cleats, fittings and lamps are outside this root.</summary>
        public static LayerPatch LandingDeck(SceneYaml scene, SpriteRef sprite, Vector2 origin)
        {
            Doc wharf = scene.RootNamed(StPetersWharf.RootName);
            Doc deck = scene.ChildNamed(scene.TransformOf(wharf), "Deck");
            Doc transform = scene.TransformOf(deck);
            RequireIdentity(transform, "landing visual deck");
            var childDocs = scene.SubtreeOf(deck.FileId);
            childDocs.Remove(deck.FileId); childDocs.Remove(transform.FileId);
            var children = transform.FieldRefs("m_Children").Select(id => scene.Require(id, "deck child"))
                .Select(tr => scene.Require(tr.FieldRef("m_GameObject"), "deck child object")).ToArray();
            bool migrated = children.Length == 1 && children[0].Field("m_Name") == LandingDeckVisualName;
            if (!migrated && (children.Length != StPetersWharf.LengthCells * StPetersWharf.WidthCells ||
                children.Any(go => !Regex.IsMatch(go.Field("m_Name") ?? "", @"^Deck_\d+_-?\d+$"))))
                throw new Refusal("landing-deck: the visual deck is neither the recorded 186 atlas cells nor the pass-two pier.");
            foreach (Doc go in children)
                if (scene.ComponentsOf(go).Count != 2 ||
                    scene.ComponentsOf(go).Any(d => d.ClassId != TransformClass && d.ClassId != SpriteRendererClass))
                    throw new Refusal("landing-deck: a visual deck child holds an unrecorded component: " + go.Field("m_Name"));
            Doc oldRenderer = scene.ComponentsOf(children[0]).Single(d => d.ClassId == SpriteRendererClass);
            Match material = Regex.Match(oldRenderer.Text, @"  m_Materials:\n  - (\{[^\n]+\})");
            if (!material.Success) throw new Refusal("landing-deck: the current visual has no material.");
            var ids = new IdAllocator(scene, childDocs);
            long goId = ids.Next("landing-deck/visual"), trId = ids.Next("landing-deck/transform"), srId = ids.Next("landing-deck/renderer");
            var desired = new Dictionary<long, string>
            {
                [goId] = GameObjectYaml(goId, LandingDeckVisualName, new[] { trId, srId }),
                [trId] = TransformYaml(trId, goId, origin, Array.Empty<long>(), transform.FileId),
                [srId] = SpriteRendererYaml(srId, goId, ObjRef.Parse(material.Groups[1].Value), sprite, StPetersWharf.SortingOrderMin),
            };
            var patch = new LayerPatch("landing-deck", "StPetersWharf/Deck", deck.FileId) { AddAfter = transform.FileId };
            foreach (long id in childDocs)
                if (!desired.ContainsKey(id))
                {
                    Doc doc = scene.Require(id, "old deck document");
                    string name = doc.ClassId == GameObjectClass ? doc.Field("m_Name") :
                        scene.Require(doc.FieldRef("m_GameObject"), "old deck component owner").Field("m_Name");
                    patch.Delete(doc, "StPetersWharf/Deck/" + name + " (" + doc.ClassId + ")");
                }
            patch.Edit(transform, SetRefList(transform.Text, "m_Children", new[] { trId }), "StPetersWharf/Deck children");
            foreach (var doc in desired)
            {
                Doc current = scene.Get(doc.Key);
                if (current == null) patch.Add(doc.Value, "StPetersWharf/Deck/" + LandingDeckVisualName);
                else patch.Edit(current, doc.Value, "StPetersWharf/Deck/" + LandingDeckVisualName);
            }
            patch.Note("StPetersWharf/Deck", "pass-two pier replaces visual atlas cells", "floor, fittings and lamps retained");
            return patch.Seal(scene);
        }

        public static LayerPatch RunLandingDeck(bool apply)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(StPetersLandingDeckBaker.SheetPath);
            if (sprite == null) throw new Refusal("landing-deck: bake/import the St Peters pier first.");
            var contract = StPetersLandingDeckBaker.ReadContract();
            string text = File.ReadAllText(ScenePath);
            LayerPatch patch = LandingDeck(SceneYaml.Parse(text), EditorShoreRockSprites.RefOf(sprite),
                new Vector2(contract.originX, contract.originY));
            Directory.CreateDirectory(PatchFolder);
            File.WriteAllText(Path.Combine(PatchFolder, "landing-deck.patch.yaml"), patch.ToYaml(), new UTF8Encoding(false));
            if (apply) File.WriteAllText(ScenePath, patch.ApplyTo(text), new UTF8Encoding(false));
            Debug.Log("[StPetersLayerRefresh] " + patch.Summary());
            return patch;
        }

        /// <summary>The one lease bake: measure the game's bed, bake the recipe, then run the existing apply path.</summary>
        public static void BakeLandingAndApplyFromCommandLine()
        {
            int code = 1;
            GameObject terrainObject = null;
            try
            {
                terrainObject = new GameObject("LandingBakeTerrain");
                var terrain = terrainObject.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(terrain);
                var contract = StPetersLandingDeckBaker.Bake(terrain);
                ApplyKeyScenesPatchBatch();
                Debug.Log($"[StPetersLandingDeck] BAKE_COMPLETE {contract.width}x{contract.height}; " + contract.tideFinding);
                code = 0;
            }
            catch (Exception ex) { Debug.LogError("[StPetersLandingDeck] bake/apply failed: " + ex); }
            finally
            {
                if (terrainObject != null) UnityEngine.Object.DestroyImmediate(terrainObject);
                EditorApplication.Exit(code);
            }
        }
    }
}
#endif
