#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The wharf's lamps at night.</b> Nine Mile Creek's wharf had no lamp that carried a light: the key scene's lamp
    /// poles were pictures and nothing stood along the wall. This step lights it the way St Peters' wharf is lit
    /// (<c>StPetersLayerRefresh.DockLamps</c>): lantern posts from the wharf decor kit, each with the same picture, sort,
    /// shadow, light and preset components, standing on the deck a metre back from the lip, so each throws a warm pool
    /// that spills over the lip onto the wall face and the water under it. The wharf's existing lamp poles carry the
    /// fuel stop's floodlight head (<see cref="NineMileCreekFuelStopPlan.LampRuleFor"/>). Where every lamp stands is
    /// <see cref="WharfLightingDef"/> data (<see cref="LightingPath"/>), never a hand edit.
    ///
    /// <para>The lights' own darkness gate turns them on at dusk and night and off by day; the step writes no runtime.
    /// It writes one root, <see cref="LampsRootName"/>, whole: a second plan is empty.</para>
    /// </summary>
    public static partial class NineMileCreekWharfLandSide
    {
        public const string LampsStep = "20-wharf-lamps";
        public const string LampsRootName = "NmcWharfLamps";
        public const string LampsPatchFolder = "artifacts/nmc-lamps";
        public const string LightingPath = "Assets/_Project/Data/Regions/NineMileCreek/WharfLighting.asset";
        const string LampPoleKitPiece = "lampPole";

        /// <summary>The wharf's lighting record.</summary>
        public static WharfLightingDef Lighting() => AssetDatabase.LoadAssetAtPath<WharfLightingDef>(LightingPath)
            ?? throw new Refusal(LampsStep + ": no wharf lighting record at " + LightingPath + ".");

        /// <summary>Where each lit pole's light hangs: the pole's row plus the fuel stop rule's head offset, as the fuel
        /// stop lights its own poles.</summary>
        public static IEnumerable<(string id, Vector2 at)> LitPoleHeads(WharfLightingDef lighting)
        {
            NineMileCreekFuelStopPlan.LampRule rule = NineMileCreekFuelStopPlan.LampRuleFor(LampPoleKitPiece)
                ?? throw new Refusal(LampsStep + ": the fuel stop has no lamp pole rule.");
            KeyScenePiece[] rows = KeyScene().Pieces;
            foreach (string id in lighting.LitPoleIds)
            {
                KeyScenePiece row = rows.SingleOrDefault(r => r.Id == id) ?? throw new Refusal(LampsStep + ": no key scene row " + id + ".");
                if (row.Piece != LampPoleKitPiece) throw new Refusal(LampsStep + ": " + id + " is a " + row.Piece + ", not a lamp pole.");
                yield return (id, row.At + SetPieceDef.ToWorld(rule.Local, row.Dir));
            }
        }

        public static LayerPatch PlaceWharfLamps(SceneYaml scene)
        {
            WharfLightingDef lighting = Lighting();
            const string name = LampsRootName;
            Doc current = ExistingRoot(scene, name);
            var ids = new IdAllocator(scene, current == null ? new HashSet<long>() : scene.SubtreeOf(current.FileId));
            long Alloc(string key) => ids.Next("nmc-lamps/" + name + "/" + key);
            long go = Alloc("go"), tr = Alloc("tr");
            var wanted = new List<(string name, string text)>();
            var children = new List<long>();
            var material = new EditorKeySceneAssets().SpriteMaterial;

            // The lantern posts: St Peters' wharf lantern, component for component.
            LightPresets.Kind lantern = LampPosts.PresetFor(LampPosts.LanternPost);
            LightPresets.Config lanternLook = LightPresets.For(lantern);
            Sprite post = IsoPackSprites.Facing(LampPosts.DecorFamily, LampPosts.LanternPost,
                IsoPackSprites.FacingForHeading(LampPosts.DecorFamily, LanternHeadingDegrees));
            SpriteRef postSprite = SpriteAsset(post);
            float postHeight = LampPosts.HeadHeightMetres(LampPosts.DecorFamily, LampPosts.LanternPost);
            ScriptRef sortScript = Script("Assets/_Project/Code/Art/YSortSprite.cs", "HiddenHarbours.Art", "HiddenHarbours.Art.YSortSprite");
            ScriptRef shadowScript = Script("Assets/_Project/Code/Art/SpriteShadow.cs", "HiddenHarbours.Art", "HiddenHarbours.Art.SpriteShadow");
            ScriptRef lightScript = Script("Assets/_Project/Code/Art/SceneLight.cs", "HiddenHarbours.Art", "HiddenHarbours.Art.SceneLight");
            ScriptRef presetScript = Script("Assets/_Project/Code/Art/PreconfiguredLight.cs", "HiddenHarbours.Art", "HiddenHarbours.Art.PreconfiguredLight");
            for (int i = 0; i < lighting.LanternSites.Length; i++)
            {
                Vector2 at = lighting.LanternSites[i];
                string key = "lanternPost_" + i;
                long pg = Alloc(key + "/go"), pt = Alloc(key + "/tr"), sr = Alloc(key + "/sprite"), sort = Alloc(key + "/sort"),
                    shadow = Alloc(key + "/shadow"), light = Alloc(key + "/light"), preset = Alloc(key + "/preset");
                children.Add(pt);
                wanted.Add((key + "/go", GameObjectYaml(pg, key, new[] { pt, sr, sort, shadow, light, preset })));
                wanted.Add((key + "/tr", TransformYaml(pt, pg, at, Array.Empty<long>(), tr)));
                int order = YSortSprite.OrderFor(at.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre, SortingBands.DecorFloor, SortingBands.DecorCeiling);
                wanted.Add((key + "/sprite", SpriteRendererYaml(sr, pg, material, postSprite, order)));
                wanted.Add((key + "/sort", MonoBehaviourYaml(sort, pg, sortScript, new[] {
                    "  _dynamic: 0", "  _baseOrder: " + F(SortingBands.DecorBase), "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre),
                    "  _minOrder: " + Int(SortingBands.DecorFloor), "  _maxOrder: " + Int(SortingBands.DecorCeiling), "  _pivotYOffset: 0" })));
                wanted.Add((key + "/shadow", MonoBehaviourYaml(shadow, pg, shadowScript, new[] {
                    "  _sortingOffset: -1", "  _pixelSnap: 1", "  _pixelsPerUnit: 32", "  _castsGroundContact: 0",
                    "  _footOffset: 0", "  _fallbackHour: 10", "  _refreshHz: 10", "  _profile: {fileID: 0}" })));
                wanted.Add((key + "/light", MonoBehaviourYaml(light, pg, lightScript, LightFields(lanternLook,
                    LightPresets.BloomForFitting(LampPosts.LanternPostFittingWidthMetres), drawsBloom: true, lighting.LanternsCastShadows,
                    postHeight, LampPosts.LanternPostFittingHeightMetres, LightPresets.ReachMetres(lantern)))));
                wanted.Add((key + "/preset", MonoBehaviourYaml(preset, pg, presetScript, new[] {
                    "  _preset: " + Int((int)lantern), "  _intensityScale: 1",
                    "  _fittingWidthMetres: " + F(LampPosts.LanternPostFittingWidthMetres),
                    "  _reachOverrideMetres: " + F(LightPresets.ReachMetres(lantern)) })));
            }

            // The poles already standing: a light alone at each drawn head, the fuel stop's floodlight.
            NineMileCreekFuelStopPlan.LampRule rule = NineMileCreekFuelStopPlan.LampRuleFor(LampPoleKitPiece);
            LightPresets.Config poleLook = LightPresets.For(rule.Kind);
            foreach (var (id, at) in LitPoleHeads(lighting))
            {
                string key = "pole/" + id;
                long pg = Alloc(key + "/go"), pt = Alloc(key + "/tr"), light = Alloc(key + "/light");
                children.Add(pt);
                wanted.Add((key + "/go", GameObjectYaml(pg, "lamp_" + id, new[] { pt, light })));
                wanted.Add((key + "/tr", TransformYaml(pt, pg, at, Array.Empty<long>(), tr)));
                wanted.Add((key + "/light", MonoBehaviourYaml(light, pg, lightScript, LightFields(poleLook,
                    LightPresets.BloomForFitting(rule.FittingWidth), drawsBloom: true, lighting.PolesCastShadows,
                    rule.Height, rule.Height * IsoGround.HeightScale, LightPresets.ReachMetres(rule.Kind)))));
            }

            wanted.Insert(0, (name + "/go", GameObjectYaml(go, name, new[] { tr })));
            wanted.Insert(1, (name + "/tr", TransformYaml(tr, go, Vector2.zero, children, 0)));
            return WriteRoot(scene, name, go, wanted, LampsStep);
        }

        /// <summary>The heading the wall's lanterns face: south, to the water, as St Peters' do.</summary>
        public const float LanternHeadingDegrees = 180f;

        static string ColorYaml(Color c) => "{r: " + F(c.r) + ", g: " + F(c.g) + ", b: " + F(c.b) + ", a: " + F(c.a) + "}";

        static string[] LightFields(LightPresets.Config look, float bloomRadius, bool drawsBloom, bool castsShadows, float lampHeight,
            float bloomLift, float reach) => new[]
        {
            "  _shape: " + Int((int)look.Shape), "  _coneHalfAngle: 30",
            "  _range: " + StPetersLayerRefresh.F(bloomRadius),
            "  _color: {r: " + StPetersLayerRefresh.F(look.Color.r) + ", g: " + StPetersLayerRefresh.F(look.Color.g) + ", b: " + StPetersLayerRefresh.F(look.Color.b) + ", a: " + StPetersLayerRefresh.F(look.Color.a) + "}",
            "  _intensity: " + StPetersLayerRefresh.F(look.Intensity), "  _edgeSoftness: " + StPetersLayerRefresh.F(look.EdgeSoftness),
            "  _drawsBloom: " + (drawsBloom ? "1" : "0"),
            "  _angularSoftness: 0.4", "  _coreBoost: 1", "  _gateThreshold: 0.12", "  _gateSoftness: 0.35", "  _gateFallback: 1",
            "  _flickerAmount: " + StPetersLayerRefresh.F(look.FlickerAmount), "  _flickerSpeed: 1", "  _sortingOrder: " + Int(SceneLight.MaxSortingOrder),
            "  _cameraDepthOffset: " + StPetersLayerRefresh.F(SceneLight.DefaultCameraDepthOffset), "  _castsShadows: " + (castsShadows ? "1" : "0"),
            "  _lampHeightMeters: " + StPetersLayerRefresh.F(lampHeight), "  _bloomLiftMetres: " + StPetersLayerRefresh.F(bloomLift),
            "  _reachMetres: " + StPetersLayerRefresh.F(reach), "  _originOffset: " + Vec2Yaml(look.OriginOffset), "  _refreshHz: 20",
        };

        // =====================================================================================================
        //  The menu and the bake
        // =====================================================================================================

        /// <summary>The step on <paramref name="sceneText"/>: the water side's re-placed harbour (its harbour lights' marker
        /// glow and the slipway's cut come from there), then the lamps. Returns the patches and, in <paramref name="after"/>,
        /// the scene they write.</summary>
        public static List<LayerPatch> PlanLamps(string sceneText, out string after)
        {
            var patches = new List<LayerPatch>();
            after = sceneText;
            foreach (Func<SceneYaml, LayerPatch> plan in new Func<SceneYaml, LayerPatch>[] { s => PlaceWaterSide(s, true), PlaceWharfLamps })
            {
                SceneYaml scene = SceneYaml.Parse(after);
                LayerPatch patch = plan(scene);
                patches.Add(patch);
                after = patch.Apply(scene).Render();
            }
            return patches;
        }

        [MenuItem("Hidden Harbours/Nine Mile Creek Layers/Wharf lamps (dry run)")]
        public static void LampsDryRun() => RunLamps(apply: false);

        [MenuItem("Hidden Harbours/Nine Mile Creek Layers/Apply wharf lamps")]
        public static void LampsApply() => RunLamps(apply: true);

        /// <summary>The batch entry: applies the step, then plans it again on what it wrote, which must be empty. Exits 1 on
        /// any refusal, so a half-written scene never reports success.</summary>
        public static void LampsApplyCli()
        {
            try
            {
                RunLamps(apply: true);
                List<LayerPatch> again = PlanLamps(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath), out _);
                if (again.Any(p => !p.IsEmpty)) throw new Refusal(LampsStep + ": a second plan is not empty.");
                Debug.Log("[" + LampsStep + "] APPLIED; a second plan is empty.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static List<LayerPatch> RunLamps(bool apply)
        {
            string path = NineMileCreekLayerRefresh.ScenePath;
            if (apply)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).path == path)
                        throw new Refusal("NineMileCreek is open. Close it before applying text patches.");
            string before = File.ReadAllText(path);
            List<LayerPatch> patches = PlanLamps(before, out string after);
            Directory.CreateDirectory(LampsPatchFolder);
            var utf8 = new UTF8Encoding(false);
            foreach (LayerPatch p in patches)
                File.WriteAllText(Path.Combine(LampsPatchFolder, NineMileCreekGroundSteps.PatchFileName(p)), p.ToYaml(), utf8);
            File.WriteAllText(Path.Combine(LampsPatchFolder, LampsStep + ".summary.txt"), string.Join("\n", patches.Select(p => p.Summary())) + "\n", utf8);
            if (apply && after != before) File.WriteAllText(path, after, utf8);
            Debug.Log("[" + LampsStep + "] " + (apply ? "Applied" : "Dry run") + ":\n" + string.Join("\n", patches.Select(p => p.Summary())));
            return patches;
        }
    }
}
#endif
