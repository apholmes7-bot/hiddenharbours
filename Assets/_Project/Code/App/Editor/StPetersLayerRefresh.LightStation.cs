#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.App.Editor
{
    // The north-east light station: the tower, the keeper's house and the shed as the station rig drew them, the
    // keeper's lamp, and the lantern that turns. One root in StPeters.unity, written as plain documents.
    public static partial class StPetersLayerRefresh
    {
        public const string LightStationStep = "LightStation";
        public const string LightStationRootName = "StPetersLightStation";
        public const string LightStationFolder = "Assets/_Project/Data/Regions/StPetersLightStation";
        public const string LightStationSprites = "Assets/_Project/Art/Sprites/StPeters/LightStation";
        public const string LightStationContractPath = LightStationSprites + "/LightStation.json";

        /// <summary>The baked station's contract (tools/rig-recipes/light-station.mjs writes it).</summary>
        [Serializable]
        public sealed class LightStationContract
        {
            public string schema;
            public float pixelsPerMetre;
            public Part[] parts;
            public Lantern lantern;
            public Character character;

            [Serializable] public sealed class Character { public string name; public float flashEverySeconds; public int beams; public float turnSeconds; }
            [Serializable] public struct Xy { public float x, y; public Vector2 V => new Vector2(x, y); }
            [Serializable] public sealed class Plan { public string name; public Xy centreMetres; public Xy halfMetres; }
            [Serializable] public sealed class Part
            {
                public string name; public string sprite; public Xy pivot; public Xy offsetMetres; public Plan[] footprints;
            }
            [Serializable] public sealed class Lantern { public float focalMetres, riseMetres, galleryRiseMetres, apexRiseMetres; }

            public const string Schema = "light-station-sprites@1";

            public static LightStationContract Parse(string json)
            {
                var c = JsonUtility.FromJson<LightStationContract>(json);
                if (c == null || c.schema != Schema) throw new Refusal($"{LightStationStep}: the contract is not {Schema}.");
                if (c.parts == null || c.parts.Length == 0 || c.lantern == null || !(c.lantern.riseMetres > 0f))
                    throw new Refusal($"{LightStationStep}: the contract names no parts or no lantern.");
                return c;
            }
        }

        /// <summary>One station to stand in the scene: its light list entry and where its tower's ground is.</summary>
        public sealed class LightStationPlacement
        {
            public LightStationDef Def;
            public ObjRef DefRef;
            public Vector2 TowerAt;
        }

        public interface ILightStationAssets
        {
            SpriteRef SpriteFor(string spriteFile);
            ObjRef Material { get; }
            ScriptRef ScriptFor(Type type);
        }

        public sealed class EditorLightStationAssets : ILightStationAssets
        {
            readonly EditorKeySceneAssets _shared = new EditorKeySceneAssets();
            readonly EditorVillageLightAssets _scripts = new EditorVillageLightAssets();
            public ObjRef Material => _shared.SpriteMaterial;
            public ScriptRef ScriptFor(Type type) => _scripts.ScriptFor(type);
            public SpriteRef SpriteFor(string spriteFile)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LightStationSprites + "/" + spriteFile);
                if (sprite == null) throw new Refusal($"{LightStationStep}: no baked sprite {spriteFile}.");
                return EditorShoreRockSprites.RefOf(sprite);
            }
        }

        static string PartObjectName(string part) => char.ToUpperInvariant(part[0]) + part.Substring(1);

        static string ColourYaml(Color c) => "{r: " + F(c.r) + ", g: " + F(c.g) + ", b: " + F(c.b) + ", a: " + F(c.a) + "}";

        // A lamp written as the scene keeps one. The lantern's lamps sit AT the lantern, so they carry no lift
        // and throw no ground pool; the keeper's lamp stands on the ground like a doorstep lantern.
        static string[] SceneLightFields(SceneLight.LightShape shape, float coneHalfAngle, float range, Color colour,
            float intensity, float edgeSoftness, float flicker, float lampHeight, float bloomLift, float reach, float refreshHz) => new[]
        {
            "  _shape: " + Int((int)shape), "  _coneHalfAngle: " + F(coneHalfAngle), "  _range: " + F(range),
            "  _color: " + ColourYaml(colour), "  _intensity: " + F(intensity), "  _edgeSoftness: " + F(edgeSoftness),
            "  _angularSoftness: 0.4", "  _coreBoost: 1", "  _gateThreshold: 0.12", "  _gateSoftness: 0.35", "  _gateFallback: 1",
            "  _flickerAmount: " + F(flicker), "  _flickerSpeed: 1", "  _sortingOrder: " + Int(SceneLight.MaxSortingOrder),
            "  _cameraDepthOffset: " + F(SceneLight.DefaultCameraDepthOffset), "  _castsShadows: 0",
            "  _lampHeightMeters: " + F(lampHeight), "  _bloomLiftMetres: " + F(bloomLift), "  _reachMetres: " + F(reach),
            "  _originOffset: {x: 0, y: 0}", "  _refreshHz: " + F(refreshHz),
        };

        /// <summary>The frames a second the turning lamps repose at: every frame the game draws.</summary>
        public const float TurningLampRefreshHz = 60f;

        /// <summary>The patch that stands the stations under <see cref="LightStationRootName"/>.</summary>
        public static LayerPatch LightStation(SceneYaml scene, IReadOnlyList<LightStationPlacement> stations,
            LightStationContract contract, ILightStationAssets assets)
        {
            if (stations.Select(s => s.Def.Id).Distinct(StringComparer.Ordinal).Count() != stations.Count)
                throw new Refusal($"{LightStationStep}: stations have duplicate ids.");
            const string rootName = LightStationRootName;
            var root = scene.Docs.Any(d => d.ClassId == GameObjectClass && d.Field("m_Name") == rootName)
                ? scene.RootNamed(rootName) : null;
            var owned = root == null ? new HashSet<long>() : scene.SubtreeOf(root.FileId);
            var ids = new IdAllocator(scene, owned);
            long Next(string name, string kind) => ids.Next(rootName + "/" + name + "/" + kind);
            long rootGo = Next("", "GameObject"), rootTr = Next("", "Transform");
            if (root != null && root.FileId != rootGo) throw new Refusal($"{LightStationStep}: the root has unexpected ids.");

            var wanted = new List<Wanted>();
            var rootChildren = new List<long>();
            var none = new List<long>();
            foreach (var station in stations.OrderBy(s => s.Def.Id, StringComparer.Ordinal))
            {
                LightStationDef def = station.Def;
                string name = def.Id;
                if (def.Beams < 1 || !(def.TurnSeconds > 0f) || !(def.BeamReachMetres > 0f))
                    throw new Refusal($"{LightStationStep}: {name} has no beam, no turn or no reach.");
                long go = Next(name, "GameObject"), tr = Next(name, "Transform"), lampScript = Next(name, "LightStationLamp");
                var children = new List<long>();

                // ---- the buildings, each sorting on its own ground ----
                foreach (var part in contract.parts)
                {
                    string child = name + "/" + PartObjectName(part.name);
                    long pg = Next(child, "GameObject"), pt = Next(child, "Transform"), sr = Next(child, "SpriteRenderer");
                    long sort = Next(child, "YSortSprite"), walls = Next(child, "PolygonCollider2D");
                    SpriteRef sprite = assets.SpriteFor(part.sprite);
                    if (string.IsNullOrEmpty(sprite.Sprite.Guid)) throw new Refusal($"{LightStationStep}: {part.sprite} has no sprite asset.");
                    Vector2 local = part.offsetMetres.V;
                    int order = YSortSprite.OrderFor(station.TowerAt.y + local.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                        SortingBands.DecorFloor, SortingBands.DecorCeiling);
                    var paths = new List<Vector2[]>();
                    foreach (var plan in part.footprints ?? Array.Empty<LightStationContract.Plan>())
                    {
                        Vector2 c = plan.centreMetres.V, h = plan.halfMetres.V;
                        paths.Add(new[]
                        {
                            new Vector2(c.x - h.x, c.y - h.y), new Vector2(c.x + h.x, c.y - h.y),
                            new Vector2(c.x + h.x, c.y + h.y), new Vector2(c.x - h.x, c.y + h.y),
                        });
                    }
                    if (paths.Count == 0) throw new Refusal($"{LightStationStep}: {part.name} stands on nothing.");
                    wanted.Add(Want(GameObjectYaml(pg, PartObjectName(part.name), new[] { pt, sr, sort, walls }), child, "GameObject"));
                    wanted.Add(Want(TransformYaml(pt, pg, local, none, tr), child, "Transform"));
                    wanted.Add(Want(SpriteRendererYaml(sr, pg, assets.Material, sprite, order), child, "SpriteRenderer"));
                    wanted.Add(Want(MonoBehaviourYaml(sort, pg, assets.ScriptFor(typeof(YSortSprite)), new[]
                    {
                        "  _dynamic: 0", "  _baseOrder: " + F(SortingBands.DecorBase),
                        "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre), "  _minOrder: " + Int(SortingBands.DecorFloor),
                        "  _maxOrder: " + Int(SortingBands.DecorCeiling), "  _pivotYOffset: 0",
                    }), child, "YSortSprite"));
                    wanted.Add(Want(PolygonColliderYaml(walls, pg, paths), child, "PolygonCollider2D"));
                    children.Add(pt);
                }

                // ---- the lantern: a steady glow, the flash, and the beams, all at the lens ----
                var lantern = new Vector2(0f, contract.lantern.riseMetres);
                long Lamp(string part, string[] fields)
                {
                    string child = name + "/" + part;
                    long lg = Next(child, "GameObject"), lt = Next(child, "Transform"), light = Next(child, "SceneLight");
                    wanted.Add(Want(GameObjectYaml(lg, part, new[] { lt, light }), child, "GameObject"));
                    wanted.Add(Want(TransformYaml(lt, lg, lantern, none, tr), child, "Transform"));
                    wanted.Add(Want(MonoBehaviourYaml(light, lg, assets.ScriptFor(typeof(SceneLight)), fields), child, "SceneLight"));
                    children.Add(lt);
                    return light;
                }
                Lamp("Lantern", SceneLightFields(SceneLight.LightShape.Radial, 180f, def.LanternGlowMetres, def.Colour,
                    def.LanternIntensity, 0.8f, 0f, 0f, 0f, 0f, 20f));
                long flash = Lamp("Flash", SceneLightFields(SceneLight.LightShape.Radial, 180f, def.FlashGlowMetres, def.Colour,
                    def.FlashIntensity, 0.9f, 0f, 0f, 0f, 0f, TurningLampRefreshHz));
                var beamTransforms = new List<long>();
                for (int i = 0; i < def.Beams; i++)
                {
                    Lamp("Beam" + Int(i), SceneLightFields(SceneLight.LightShape.Cone, def.BeamHalfAngleDegrees, def.BeamReachMetres,
                        def.Colour, def.BeamIntensity, 0.85f, 0f, 0f, 0f, 0f, TurningLampRefreshHz));
                    beamTransforms.Add(children[children.Count - 1]);
                }

                // ---- the keeper's lamp at the house door, a lantern like any doorstep's ----
                {
                    string child = name + "/KeepersLamp";
                    long lg = Next(child, "GameObject"), lt = Next(child, "Transform");
                    long light = Next(child, "SceneLight"), preset = Next(child, "PreconfiguredLight");
                    var look = LightPresets.For(LightPresets.Kind.Lantern);
                    var at = new Vector2(def.KeepersLampAt.x, def.KeepersLampAt.y * IsoGround.GroundDepthScale);
                    wanted.Add(Want(GameObjectYaml(lg, "KeepersLamp", new[] { lt, light, preset }), child, "GameObject"));
                    wanted.Add(Want(TransformYaml(lt, lg, at, none, tr), child, "Transform"));
                    wanted.Add(Want(MonoBehaviourYaml(light, lg, assets.ScriptFor(typeof(SceneLight)), SceneLightFields(look.Shape, 30f,
                        look.Range, look.Color, look.Intensity, look.EdgeSoftness, look.FlickerAmount, def.KeepersLampAt.z,
                        def.KeepersLampAt.z * IsoGround.HeightScale, def.KeepersLampReachMetres, 20f)), child, "SceneLight"));
                    wanted.Add(Want(MonoBehaviourYaml(preset, lg, assets.ScriptFor(typeof(PreconfiguredLight)), new[]
                    {
                        "  _preset: " + Int((int)LightPresets.Kind.Lantern), "  _intensityScale: 1", "  _fittingWidthMetres: 0",
                        "  _reachOverrideMetres: " + F(def.KeepersLampReachMetres),
                    }), child, "PreconfiguredLight"));
                    children.Add(lt);
                }

                var lampFields = new List<string> { "  _def: " + station.DefRef.Yaml, "  _beams:" };
                foreach (long b in beamTransforms) lampFields.Add($"  - {{fileID: {Id(b)}}}");
                lampFields.Add($"  _flash: {{fileID: {Id(flash)}}}");
                wanted.Add(Want(GameObjectYaml(go, name, new[] { tr, lampScript }), name, "GameObject"));
                wanted.Add(Want(TransformYaml(tr, go, station.TowerAt, children, rootTr), name, "Transform"));
                wanted.Add(Want(MonoBehaviourYaml(lampScript, go, assets.ScriptFor(typeof(LightStationLamp)), lampFields), name, "LightStationLamp"));
                rootChildren.Add(tr);
            }

            wanted.Insert(0, Want(GameObjectYaml(rootGo, rootName, new[] { rootTr }), rootName, "GameObject"));
            wanted.Insert(1, Want(TransformYaml(rootTr, rootGo, Vector2.zero, rootChildren, 0), rootName, "Transform"));
            var patch = new LayerPatch(LightStationStep, rootName, rootGo);
            var desired = new HashSet<long>(wanted.Select(w => w.FileId));
            foreach (var doc in scene.Docs.Where(d => owned.Contains(d.FileId) && !desired.Contains(d.FileId)))
                patch.Delete(doc, NameOfExisting(scene, doc));
            foreach (var want in wanted)
            {
                var doc = scene.Get(want.FileId);
                if (doc == null) patch.Add(want.Text, want.Name);
                else patch.Edit(doc, want.Text, want.Name);
            }
            if (root == null)
            {
                var list = SceneRootsOf(scene);
                patch.Edit(list, WithRootListed(list.Text, rootTr), rootName + " joins SceneRoots");
            }
            return patch.Seal(scene);
        }

        /// <summary>The stations as their Defs and key scenes have them, by id.</summary>
        public static List<LightStationPlacement> LoadLightStations()
        {
            var scenes = new List<KeySceneDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(KeySceneDef), new[] { KeySceneFolder }))
            {
                var k = AssetDatabase.LoadAssetAtPath<KeySceneDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (k != null) scenes.Add(k);
            }
            var stations = new List<LightStationPlacement>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(LightStationDef), new[] { LightStationFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<LightStationDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null) continue;
                KeySceneDef scene = scenes.SingleOrDefault(k => k.Id == def.KeySceneId)
                    ?? throw new Refusal($"{LightStationStep}: {def.Id} names no key scene ({def.KeySceneId}).");
                KeyScenePiece tower = scene.Pieces.SingleOrDefault(p => p.Id == def.TowerPieceId)
                    ?? throw new Refusal($"{LightStationStep}: {def.Id} names no tower row ({def.TowerPieceId}).");
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(def, out string defGuid, out long defId))
                    throw new Refusal($"{LightStationStep}: {def.Id} is not an asset.");
                stations.Add(new LightStationPlacement { Def = def, DefRef = new ObjRef(defId, defGuid, 2), TowerAt = tower.At });
            }
            stations.Sort((a, b) => string.CompareOrdinal(a.Def.Id, b.Def.Id));
            return stations;
        }

        public static LightStationContract LoadLightStationContract() =>
            LightStationContract.Parse(File.ReadAllText(LightStationContractPath));

        [MenuItem("Hidden Harbours/World/St Peters Layer Refresh/Write the Light Station Patch (dry run)")]
        static void WriteLightStationMenu() => LightStationFromMenu(apply: false);

        [MenuItem("Hidden Harbours/World/St Peters Layer Refresh/Apply the Light Station Patch to StPeters.unity")]
        static void ApplyLightStationMenu()
        {
            if (EditorUtility.DisplayDialog("St Peters light station",
                    $"Stand the light station under the '{LightStationRootName}' root in the StPeters.unity FILE?\n\n" +
                    "The scene must be closed. The patch is written to " + PatchFolder + " first.", "Apply", "Cancel"))
                LightStationFromMenu(apply: true);
        }

        /// <summary>The dry run for <c>-executeMethod</c>: a refusal fails the run instead of logging.</summary>
        public static void WriteLightStationPatchBatch() => RunLightStation(apply: false);

        /// <summary>The apply for <c>-executeMethod</c>, the menu's own path.</summary>
        public static void ApplyLightStationPatchBatch() => RunLightStation(apply: true);

        static void LightStationFromMenu(bool apply)
        {
            try { RunLightStation(apply); }
            catch (Refusal r) { Debug.LogError(r.Message); }
        }

        public static LayerPatch RunLightStation(bool apply)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (apply && string.Equals(SceneManager.GetSceneAt(i).path, ScenePath, StringComparison.Ordinal))
                    throw new Refusal("StPeters is open in the editor. Close it first: an open scene would overwrite the file on its next save.");

            string text = File.ReadAllText(ScenePath);
            LayerPatch patch = LightStation(SceneYaml.Parse(text), LoadLightStations(), LoadLightStationContract(),
                new EditorLightStationAssets());
            Directory.CreateDirectory(PatchFolder);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(PatchFolder, patch.Step + ".patch.yaml"), patch.ToYaml(), utf8);
            string after = patch.ApplyTo(text);
            if (apply && after != text) File.WriteAllText(ScenePath, after, utf8);
            Debug.Log($"[StPetersLayerRefresh] {(apply ? "applied to " + ScenePath : "dry run, nothing written to the scene")} " +
                      $"(patch in {PatchFolder}):\n{patch.Summary()}");
            return patch;
        }
    }
}
#endif
