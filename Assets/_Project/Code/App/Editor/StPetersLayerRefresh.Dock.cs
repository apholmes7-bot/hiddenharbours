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
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class StPetersLayerRefresh
    {
        static string DockContract => File.ReadAllText(StPetersLandingDeckBaker.ContractPath);
        static float DeckNumber(string key) => MiniJson.Float(MiniJson.Parse(DockContract), key);
        static ScriptRef DockScript(string type)
        {
            string[] paths = Directory.GetFiles("Assets/_Project/Code", type + ".cs", SearchOption.AllDirectories);
            if (paths.Length != 1) throw new Refusal("dock: script is not unique: " + type);
            string guid = Regex.Match(File.ReadAllText(paths[0] + ".meta"), @"(?m)^guid: (\w+)").Groups[1].Value;
            string module = type == "HullTideRide" ? "Boats" : type == "StPetersPierBoatSorting" ? "App" : "Art";
            return new ScriptRef(new ObjRef(11500000, guid, 3), "HiddenHarbours." + module + "::HiddenHarbours." + module + "." + type);
        }
        static long DockId(string key)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("st-peters-dock/" + key));
            return (BitConverter.ToInt64(bytes, 0) & 0x3fffffffffffffffL) + 1;
        }
        static Doc DockNamed(SceneYaml scene, string name) => scene.Docs.Single(d =>
            d.ClassId == GameObjectClass && d.Field("m_Name") == name);
        static Doc DockComponent(SceneYaml scene, Doc go, string kind) => scene.ComponentsOf(go)
            .Single(d => (d.Field("m_EditorClassIdentifier") ?? "").EndsWith("." + kind, StringComparison.Ordinal));
        static void DockPut(SceneYaml scene, LayerPatch patch, string text, string name)
        {
            text = text.Replace("\n  m_Name: \n", "\n  m_Name:\n");
            var desired = new Doc(text);
            Doc current = scene.Get(desired.FileId);
            if (current == null) patch.Add(text, name);
            else
            {
                if (current.ClassId != desired.ClassId || current.FieldRef("m_GameObject") != desired.FieldRef("m_GameObject"))
                    throw new Refusal("dock: generated document collides with another owner: " + name);
                if (current.ClassId == GameObjectClass && current.Field("m_Name") != desired.Field("m_Name"))
                    throw new Refusal("dock: generated object collides with another name: " + name);
                patch.Edit(current, text, name);
            }
        }
        static string DockComponents(Doc go, IEnumerable<long> extra) => Regex.Replace(go.Text,
            @"(?m)^  m_Component:\n(?:  - component: [^\n]+\n)*",
            "  m_Component:\n" + string.Join("", go.FieldRefs("m_Component").Concat(extra).Distinct()
                .Select(id => "  - component: {fileID: " + Id(id) + "}\n")));
        static void DockMove(SceneYaml scene, LayerPatch patch, Doc go, Vector2 at)
        {
            Doc tr = scene.TransformOf(go);
            patch.Edit(tr, SetField(tr.Text, "m_LocalPosition", Vec3Yaml(at.x, at.y, 0f)), go.Field("m_Name") + ": placement");
        }

        public static LayerPatch LandingDock(SceneYaml scene)
        {
            Doc root = scene.RootNamed(StPetersWharf.RootName), rootTr = scene.TransformOf(root);
            var patch = new LayerPatch("landing-dock", StPetersWharf.RootName, root.FileId) { AddAfter = rootTr.FileId };
            Rect floor = StPetersWharf.DeckFootprint();
            Doc platform = DockComponent(scene, root, "StandablePlatform");
            string platformText = Regex.Replace(platform.Text, @"(?m)^  _footprint:\n(?:    [^\n]+\n)*",
                "  _footprint:\n    serializedVersion: 2\n    x: " + F(floor.x) + "\n    y: " + F(floor.y) +
                "\n    width: " + F(floor.width) + "\n    height: " + F(floor.height) + "\n");
            patch.Edit(platform, platformText, "StPetersWharf: measured standing surface");

            var fittings = StPetersWharf.Fittings();
            var fittingRoot = scene.ChildNamed(rootTr, "Fittings");
            foreach (var group in fittings.GroupBy(f => f.Name))
            {
                var wanted = group.OrderBy(f => f.Position.x).ThenBy(f => f.Position.y).ToArray();
                var current = scene.TransformOf(fittingRoot).FieldRefs("m_Children").Select(id => scene.Require(id, "fitting transform"))
                    .Select(tr => scene.Require(tr.FieldRef("m_GameObject"), "fitting object"))
                    .Where(go => go.Field("m_Name") == "Fitting_" + group.Key)
                    .OrderBy(go => ParseVec3(scene.TransformOf(go).Field("m_LocalPosition")).x)
                    .ThenBy(go => ParseVec3(scene.TransformOf(go).Field("m_LocalPosition")).y).ToArray();
                if (wanted.Length != current.Length) throw new Refusal("dock: fitting count changed: " + group.Key);
                for (int i = 0; i < wanted.Length; i++) DockMove(scene, patch, current[i], wanted[i].Position);
            }
            int tie = 0;
            foreach (var f in StPetersWharf.MooringFittings())
                DockMove(scene, patch, DockNamed(scene, "Cleat_" + f.Name + "_" + tie++), f.Position);
            DockMove(scene, patch, DockNamed(scene, "Ladder_0"), StPetersWharf.LadderPosition());

            Doc visual = DockNamed(scene, LandingDeckVisualName);
            long face = DockId("height-waterline");
            patch.Edit(visual, DockComponents(visual, new[] { face }), "WharfPass2Deck: retained-height component");
            Doc renderer = scene.ComponentsOf(visual).Single(d => d.ClassId == SpriteRendererClass);
            string materialGuid = Regex.Match(File.ReadAllText("Assets/_Project/Resources/TidalFace.mat.meta"), @"(?m)^guid: (\w+)").Groups[1].Value;
            string sr = Regex.Replace(renderer.Text, @"(?m)^  - \{fileID: [^\n]+\}$", "  - " + new ObjRef(2100000, materialGuid, 2).Yaml, RegexOptions.None);
            sr = SetField(sr, "m_SortingOrder", Int(StPetersWharf.BakedDeckSortingOrder));
            patch.Edit(renderer, sr, "WharfPass2Deck: existing waterline material and pier order");
            string heightGuid = Regex.Match(File.ReadAllText(StPetersLandingDeckBaker.HeightPath + ".meta"), @"(?m)^guid: (\w+)").Groups[1].Value;
            DockPut(scene, patch, MonoBehaviourYaml(face, visual.FileId, DockScript("TidalFaceWaterline"), new[]
            {
                "  _lipWorldY: " + F(floor.yMin), "  _lipElevation: " + F(StPetersLandingLayout.DatumElevation),
                "  _ridesTheTide: 1", "  _configured: 1",
                "  _heightTexture: " + new ObjRef(2800000, heightGuid, 3).Yaml,
                "  _heightDecode: " + Vec3Yaml(DeckNumber("heightMinRigZ"), DeckNumber("heightRange"), DeckNumber("datumShift")),
            }), "WharfPass2Deck: TidalFaceWaterline");
            DockLamps(scene, patch, rootTr);
            return patch.Seal(scene);
        }

        static string RectYaml(Rect value) => "{serializedVersion: 2, x: " + F(value.x) + ", y: " + F(value.y) +
            ", width: " + F(value.width) + ", height: " + F(value.height) + "}";

        static void DockLamps(SceneYaml scene, LayerPatch patch, Doc rootTr)
        {
            long lampsGo = DockId("lamps"), lampsTr = DockId("lamps/transform");
            var sites = StPetersWharf.LampPostSites();
            var children = new List<long>();
            string sheet = "Assets/_Project/Art/Sprites/Wharf/Decor/lanternPost.png";
            string meta = File.ReadAllText(sheet + ".meta").Replace("\r\n", "\n");
            string guid = Regex.Match(meta, @"(?m)^guid: (\w+)").Groups[1].Value;
            long spriteId = long.Parse(Regex.Match(meta, @"(?s)name: lanternPost_4\n.*?internalID: (-?\d+)").Groups[1].Value);
            object pack = MiniJson.Parse(File.ReadAllText("Assets/_Project/Art/Sprites/Wharf/Decor/wharfDecorRig.contract.json"));
            object cell = MiniJson.List(pack, "cells").Single(c => MiniJson.String(c, "key") == LampPosts.LanternPost);
            float ppu = MiniJson.Float(MiniJson.Dict(pack, "projection"), "ppu");
            var sprite = new SpriteRef(new ObjRef(spriteId, guid, 3),
                new Vector2(MiniJson.Float(cell, "cellW") / ppu, MiniJson.Float(cell, "cellH") / ppu));
            Doc baseRenderer = scene.ComponentsOf(DockNamed(scene, LandingDeckVisualName)).Single(d => d.ClassId == SpriteRendererClass);
            ObjRef material = ObjRef.Parse(Regex.Match(baseRenderer.Text, @"  m_Materials:\n  - (\{[^\n]+\})").Groups[1].Value);
            // The existing deck material becomes tidal; the posts keep the ordinary sprite material.
            if (material.Guid == Regex.Match(File.ReadAllText("Assets/_Project/Resources/TidalFace.mat.meta"), @"(?m)^guid: (\w+)").Groups[1].Value)
            {
                Doc fitting = scene.Docs.First(d => d.ClassId == GameObjectClass && d.Field("m_Name") == "Fitting_bollard");
                Doc sr = scene.ComponentsOf(fitting).Single(d => d.ClassId == SpriteRendererClass);
                material = ObjRef.Parse(Regex.Match(sr.Text, @"  m_Materials:\n  - (\{[^\n]+\})").Groups[1].Value);
            }
            var kind = LampPosts.PresetFor(LampPosts.LanternPost);
            var look = LightPresets.For(kind);
            for (int i = 0; i < sites.Count; i++)
            {
                string name = "lanternPost_" + i;
                long go = DockId(name), tr = DockId(name + "/transform"), sr = DockId(name + "/sprite"),
                    sort = DockId(name + "/sort"), shadow = DockId(name + "/shadow"), light = DockId(name + "/light"), preset = DockId(name + "/preset");
                children.Add(tr);
                DockPut(scene, patch, GameObjectYaml(go, name, new[] { tr, sr, sort, shadow, light, preset }), "Lamps/" + name);
                DockPut(scene, patch, TransformYaml(tr, go, sites[i].Position, Array.Empty<long>(), lampsTr), name + ": Transform");
                int order = YSortSprite.OrderFor(sites[i].Position.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                    SortingBands.DecorFloor, SortingBands.DecorCeiling);
                DockPut(scene, patch, SpriteRendererYaml(sr, go, material, sprite, order), name + ": SpriteRenderer");
                DockPut(scene, patch, MonoBehaviourYaml(sort, go, DockScript("YSortSprite"), new[] {
                    "  _dynamic: 0", "  _baseOrder: " + F(SortingBands.DecorBase), "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre),
                    "  _minOrder: " + Int(SortingBands.DecorFloor), "  _maxOrder: " + Int(SortingBands.DecorCeiling), "  _pivotYOffset: 0" }), name + ": YSortSprite");
                DockPut(scene, patch, MonoBehaviourYaml(shadow, go, DockScript("SpriteShadow"), new[] {
                    "  _sortingOffset: -1", "  _pixelSnap: 1", "  _pixelsPerUnit: 32", "  _castsGroundContact: 0",
                    "  _footOffset: 0", "  _fallbackHour: 10", "  _refreshHz: 10", "  _profile: {fileID: 0}" }), name + ": SpriteShadow");
                DockPut(scene, patch, MonoBehaviourYaml(light, go, DockScript("SceneLight"), new[] {
                    "  _shape: " + Int((int)look.Shape), "  _coneHalfAngle: 30",
                    "  _range: " + F(LightPresets.BloomForFitting(LampPosts.LanternPostFittingWidthMetres)),
                    "  _color: {r: " + F(look.Color.r) + ", g: " + F(look.Color.g) + ", b: " + F(look.Color.b) + ", a: " + F(look.Color.a) + "}",
                    "  _intensity: " + F(look.Intensity), "  _edgeSoftness: " + F(look.EdgeSoftness),
                    "  _angularSoftness: 0.4", "  _coreBoost: 1", "  _gateThreshold: 0.12", "  _gateSoftness: 0.35", "  _gateFallback: 1",
                    "  _flickerAmount: " + F(look.FlickerAmount), "  _flickerSpeed: 1", "  _sortingOrder: " + Int(SceneLight.MaxSortingOrder),
                    "  _cameraDepthOffset: " + F(SceneLight.DefaultCameraDepthOffset), "  _castsShadows: 1",
                    "  _lampHeightMeters: " + F(MiniJson.Float(cell, "heightM")), "  _bloomLiftMetres: " + F(LampPosts.LanternPostFittingHeightMetres),
                    "  _reachMetres: " + F(LightPresets.ReachMetres(kind)), "  _originOffset: " + Vec2Yaml(look.OriginOffset), "  _refreshHz: 20" }), name + ": SceneLight");
                DockPut(scene, patch, MonoBehaviourYaml(preset, go, DockScript("PreconfiguredLight"), new[] {
                    "  _preset: " + Int((int)kind), "  _intensityScale: 1", "  _fittingWidthMetres: " + F(LampPosts.LanternPostFittingWidthMetres),
                    "  _reachOverrideMetres: " + F(LightPresets.ReachMetres(kind)) }), name + ": PreconfiguredLight");
            }
            DockPut(scene, patch, GameObjectYaml(lampsGo, "Lamps", new[] { lampsTr }), "StPetersWharf/Lamps");
            DockPut(scene, patch, TransformYaml(lampsTr, lampsGo, Vector2.zero, children, rootTr.FileId), "Lamps: Transform");
            patch.Edit(rootTr, SetRefList(rootTr.Text, "m_Children", rootTr.FieldRefs("m_Children").Concat(new[] { lampsTr }).Distinct().ToList()), "StPetersWharf: owned lamps child");
        }

        public static LayerPatch LandingDory(SceneYaml scene)
        {
            Doc go = scene.RootNamed("Dory");
            var patch = new LayerPatch("landing-dory", "Dory", go.FileId) { AddAfter = scene.TransformOf(go).FileId };
            DockMove(scene, patch, go, StPetersBuilder.DoryMooredPos);
            long ride = DockId("hull-ride"), group = DockId("hull-group"), sort = DockId("hull-sort");
            patch.Edit(go, DockComponents(go, new[] { ride, group, sort }), "Dory: authored picture components");
            DockPut(scene, patch, MonoBehaviourYaml(ride, go.FileId, DockScript("HullTideRide"), new[]
            {
                "  _bakedWaterlineElevation: " + F(StPetersLandingLayout.HullPictureDatum(StPetersBuilder.DoryMooredY, StPetersWharf.NorthFaceY)),
                "  _draughtOverrideMetres: -1",
            }), "Dory: HullTideRide");
            DockPut(scene, patch, string.Join("\n", Header(210, group, "SortingGroup"), Unlinked,
                "  m_GameObject: {fileID: " + Id(go.FileId) + "}", "  m_Enabled: 1", "  m_SortingLayerID: 0", "  m_SortingLayer: 0",
                "  m_SortingOrder: -1", "  m_SortAtRoot: 0"), "Dory: SortingGroup");
            DockPut(scene, patch, MonoBehaviourYaml(sort, go.FileId, DockScript("StPetersPierBoatSorting"), new[]
            {
                "  _pier: " + RectYaml(StPetersWharf.DeckFootprint()), "  _frontOrder: 1", "  _behindOrder: -1",
            }), "Dory: pier sorting");
            return patch.Seal(scene);
        }

        public static LayerPatch LandingPocket(SceneYaml scene)
        {
            Doc component = scene.Docs.Single(d => (d.Field("m_EditorClassIdentifier") ?? "").EndsWith(".TidalTerrain", StringComparison.Ordinal));
            Doc go = scene.Require(component.FieldRef("m_GameObject"), "terrain owner");
            var patch = new LayerPatch("landing-pocket", go.Field("m_Name"), go.FileId);
            string text = component.Text;
            foreach (var pair in new[] {
                ("_northPocketFrom", Vec2Yaml(StPetersBuilder.NorthPocketFrom)), ("_northPocketTo", Vec2Yaml(StPetersBuilder.NorthPocketTo)),
                ("_northPocketHalfWidth", F(StPetersBuilder.NorthPocketHalfWidth)),
                ("_northPocketThalwegHalfWidth", F(StPetersBuilder.NorthPocketThalwegHalfWidth)),
                ("_northPocketBedElevation", F(StPetersBuilder.NorthPocketBedElevation)) })
                text = Regex.IsMatch(text, @"(?m)^  " + pair.Item1 + ":") ? SetField(text, pair.Item1, pair.Item2) : text + "\n  " + pair.Item1 + ": " + pair.Item2;
            patch.Edit(component, text, "TidalTerrain: local north pocket only");
            return patch.Seal(scene);
        }

        static string DockVectorArray(string text, string field, IEnumerable<Vector2> points) => Regex.Replace(text,
            @"(?m)^  " + field + @":(?: \[\])?\n(?:  - \{x: [^\n]+\n)*",
            "  " + field + ":\n" + string.Join("", points.Select(p => "  - " + Vec2Yaml(p) + "\n")));
        static string DockInts(int[] values) => string.Concat(values.Select(v =>
            string.Concat(BitConverter.GetBytes(v).Select(b => b.ToString("x2")))));
        static Vector2 DockVec2(string value) => ParseVec3(value.Replace("}", ", z: 0}"));

        public static IEnumerable<LayerPatch> LandingConsumers(SceneYaml scene)
        {
            foreach (var move in new[] {
                ("BasilSamson", StPetersLandingLayout.Basil), ("StPetersArrival", (Vector2)StPetersBuilder.ArrivalPos),
                ("StPetersDockZone", (Vector2)StPetersBuilder.DockZonePos), ("StPetersDisembark", (Vector2)StPetersBuilder.DisembarkPos) })
            {
                Doc go = DockNamed(scene, move.Item1);
                var patch = new LayerPatch("landing-placement", move.Item1, go.FileId);
                DockMove(scene, patch, go, move.Item2);
                yield return patch.Seal(scene);
            }
            Doc arrivalGo = scene.RootNamed("StPetersArrivalOpening");
            Doc arrival = DockComponent(scene, arrivalGo, "ArrivalOpening");
            var arrivalPatch = new LayerPatch("landing-arrival", "StPetersArrivalOpening", arrivalGo.FileId);
            string a = DockVectorArray(arrival.Text, "_route", StPetersArrivalOpening.Route());
            a = SetField(a, "_berth", Vec2Yaml(StPetersArrivalOpening.Berth()));
            a = SetField(a, "_stepAshore", Vec2Yaml(StPetersArrivalOpening.StepAshore()));
            string datum = F(StPetersLandingLayout.HullPictureDatum(StPetersBuilder.AlongsideBerthY, StPetersWharf.MooringFaceY));
            a = Regex.IsMatch(a, @"(?m)^  _hullPictureDatum:") ? SetField(a, "_hullPictureDatum", datum) : a + "\n  _hullPictureDatum: " + datum;
            arrivalPatch.Edit(arrival, a, "ArrivalOpening: berth, step and hull picture datum");
            yield return arrivalPatch.Seal(scene);

            Doc lanesGo = DockNamed(scene, "RoutineLanes");
            Doc lanes = DockComponent(scene, lanesGo, "RoutineLanes");
            var lanePatch = new LayerPatch("landing-walk", "RoutineLanes", lanesGo.FileId);
            var table = StPetersRoutines.BuildLaneTable();
            string l = DockVectorArray(lanes.Text, "_nodePositions", table.Positions);
            l = DockVectorArray(l, "_via", table.Via);
            l = SetField(l, "_viaStart", DockInts(table.ViaStart));
            l = SetField(l, "_viaCount", DockInts(table.ViaCount));
            lanePatch.Edit(lanes, l, "RoutineLanes: west entry and wharf-head tail");
            yield return lanePatch.Seal(scene);

            Doc stationsGo = DockNamed(scene, "RoutineStations");
            Doc stations = DockComponent(scene, stationsGo, "RoutineStations");
            var stationPatch = new LayerPatch("landing-stations", "RoutineStations", stationsGo.FileId);
            string s = stations.Text;
            foreach (var station in new[] { (StPetersRoutines.StationSlipHead, StPetersLandingLayout.Entry),
                                           (StPetersRoutines.StationWharfHead, StPetersLandingLayout.Basil) })
            {
                string pattern = @"(?m)(^  - Id: " + Regex.Escape(station.Item1) + @"\n    Position: )[^\n]+";
                if (Regex.Matches(s, pattern).Count != 1) throw new Refusal("dock: station is absent or duplicated: " + station.Item1);
                s = Regex.Replace(s, pattern, m => m.Groups[1].Value + Vec2Yaml(station.Item2));
                if (station.Item1 == StPetersRoutines.StationSlipHead)
                    s = Regex.Replace(s, @"(?ms)(^  - Id: " + Regex.Escape(station.Item1) + @"\n.*?^    StandHeadingDegrees: )[^\n]+",
                        m => m.Groups[1].Value + F(StPetersRoutines.HeadingTo(StPetersLandingLayout.Entry, StPetersLandingLayout.Basil)));
            }
            stationPatch.Edit(stations, s, "RoutineStations: slip and wharf positions");
            yield return stationPatch.Seal(scene);

        }

        [Serializable] public sealed class DockOperation
        {
            public string kind, name;
            public long id;
        }
        [Serializable] public sealed class DockResult
        {
            public bool success;
            public string error;
            public long beforeBytes, afterBytes;
            public int pocketTexels, secondPassOperations;
            public DockOperation[] operations;
        }

        public static SceneYaml PlanLandingDock(SceneYaml scene, ITidalTerrain terrain, INavMarkAssets nav,
            List<DockOperation> operations)
        {
            void Apply(LayerPatch patch)
            {
                operations.AddRange(patch.Ops.Select(o => new DockOperation { kind = o.Kind.ToString(), id = o.FileId, name = o.Name }));
                scene = patch.Apply(scene);
            }
            Apply(LandingDock(scene));
            Apply(LandingDory(scene));
            Apply(LandingPocket(scene));
            foreach (var patch in LandingConsumers(scene)) Apply(patch);
            Apply(NavMarks(scene, terrain, nav));
            // Let the shared step derive every renderer/sorter field for the rows that moved.
            var rows = KeyScenePieces(scene, LoadStPetersKeyScenes(), new EditorKeySceneAssets());
            var moved = new HashSet<long>();
            foreach (string name in new[] { "prop.stp_landing_traps", "prop.stp_landing_totes",
                "prop.stp_landing_cart", "prop.stp_landing_bait_barrel", "prop.stp_landing_rope", "prop.stp_landing_gull" })
                moved.UnionWith(scene.SubtreeOf(DockNamed(scene, name).FileId));
            foreach (var op in rows.Ops)
                if (op.Kind != OpKind.Edit || !moved.Contains(op.FileId))
                    throw new Refusal("dock: key-scene change is outside a moved landing row: " + op.Name);
            Apply(rows);
            return scene;
        }

        /// <summary>Patch the banked file without rebuilding or saving the painted region.</summary>
        public static void ApplyLandingDockBatch()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-stpDockResult");
            if (at < 0 || at + 1 >= args.Length) throw new Refusal("dock: a results path is required.");
            Directory.SetCurrentDirectory(RigCatalog.RepoRoot);
            string output = Path.GetFullPath(args[at + 1]);
            string evidence = Path.GetFullPath("Evidence~/stp-kits/k0/ruling-5") + Path.DirectorySeparatorChar;
            if (!output.StartsWith(evidence, StringComparison.OrdinalIgnoreCase)) throw new Refusal("dock: results must stay in this lane's evidence.");
            var result = new DockResult();
            GameObject temp = null;
            int code = 1;
            try
            {
                temp = new GameObject("LandingDockProfile");
                var terrain = temp.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(terrain);
                string before = File.ReadAllText(ScenePath);
                var operations = new List<DockOperation>();
                var nav = new EditorNavMarkAssets();
                var after = PlanLandingDock(SceneYaml.Parse(before), terrain, nav, operations);
                var repeat = new List<DockOperation>();
                PlanLandingDock(after, terrain, nav, repeat);
                result.secondPassOperations = repeat.Count;
                if (repeat.Count != 0) throw new Refusal("dock: a second scene patch still changes documents.");
                result.beforeBytes = new FileInfo(ScenePath).Length;
                result.operations = operations.ToArray();
                result.pocketTexels = StPetersNorthPocket.Apply();
                if (StPetersNorthPocket.Apply() != 0) throw new Refusal("dock: a second painted pocket cut still changes texels.");
                File.WriteAllText(ScenePath, after.Render(), new System.Text.UTF8Encoding(false));
                result.afterBytes = new FileInfo(ScenePath).Length;
                result.success = true;
                code = 0;
            }
            catch (Exception error) { result.error = error.ToString(); Debug.LogError(error); }
            finally
            {
                if (temp != null) UnityEngine.Object.DestroyImmediate(temp);
                File.WriteAllText(output, JsonUtility.ToJson(result, true) + "\n", new System.Text.UTF8Encoding(false));
                UnityEditor.EditorApplication.Exit(code);
            }
        }
    }
}
#endif
