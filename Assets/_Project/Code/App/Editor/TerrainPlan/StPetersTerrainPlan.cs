using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>ST PETERS' TERRAIN PLAN, IN THE EDITOR</b> (terrain PR 5). Where the plan's files live, and the editor half of
    /// each step the window runs: gather the sources from the game, freeze them beside the Defs, load them back with
    /// today's analytic ground sampled, load the plan and its Defs, derive, and fill the guards' input from the engine.
    ///
    /// <para>Everything it computes goes through the pure classes (<see cref="TerrainPlanGather"/>,
    /// <see cref="TerrainPlanDerivation"/>, <see cref="TerrainPlanGuards"/>), so the headless runs check the same code;
    /// this class only reads the engine. <b>The sources are frozen once</b>, before PR 5 writes its maps
    /// (<see cref="TerrainPlanSourcesJson"/> says why): gathering after the write would read the plan's own paint back
    /// as today's. So <see cref="Freeze"/> refuses to overwrite the file unless told to, and every derivation reads the
    /// frozen file (<see cref="LoadFrozen"/>). Only a walls' patch touches it after that, and only to drop the cliff walls
    /// that no longer stand (<see cref="KeepStandingWalls"/>).</para>
    ///
    /// <para><b>Today's ground is sampled, never frozen</b>: the analytic <see cref="TidalTerrain"/> the builder
    /// configures, at every cell's centre, in the sim's own float. A headless run uses a float64 port of the same
    /// function, so its derivation hash is its own; the determinism test compares Unity with Unity.</para>
    /// </summary>
    public static class StPetersTerrainPlan
    {
        public const string PlanId = "terrain_plan.st_peters";
        public const string TerrainDir = "Assets/_Project/Data/Terrain";
        public const string DefsFolder = TerrainDir + "/StPetersPlan";
        public const string PlanPath = DefsFolder + "/TerrainPlan_StPeters.asset";
        public const string SourcesPath = TerrainDir + "/StPetersPlan.sources.json";
        /// <summary>The ground file's home: its Def, its base, its asks and their patches (terrain PR 5 B).</summary>
        public const string GroundFolder = TerrainDir + "/StPetersGround";
        public const string ManifestPath = TerrainDir + "/StPetersPlan.manifest.json";
        public const string SeabedPath = TerrainDir + "/StPetersSeabed.asset";
        public const string StillPath = TerrainDir + "/StPetersStillWater.png";
        public const string ScenePath = "Assets/_Project/Scenes/StPeters.unity";
        public const string FleetPath = "Assets/_Project/Data/Boats/StPetersAmbientFleet.asset";
        public const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        /// <summary>The village's Defs (V1): its plan, lots, yards, routes and lights, which the village's ground checks read.</summary>
        public const string VillageFolder = "Assets/_Project/Data/Regions/StPetersVillage";

        /// <summary>What the manifest says the base ground was read from.</summary>
        public const string BaseFrom = "TidalTerrain.ElevationAt after StPetersBuilder.ConfigureTidalTerrain, float32, at each cell centre";

        /// <summary>How far a gathered number may sit from the frozen file's and still be the same (m or degrees): the
        /// sim's float against the file's decimal.</summary>
        public const double CompareTolerance = 1e-5;

        // ---- the plan and its Defs ---------------------------------------------------------------------------------------

        public static RegionTerrainPlanDef LoadPlan()
        {
            var plan = AssetDatabase.LoadAssetAtPath<RegionTerrainPlanDef>(PlanPath);
            if (plan == null) throw new FileNotFoundException("[TerrainPlan] no RegionTerrainPlanDef at " + PlanPath + ".");
            return plan;
        }

        /// <summary>
        /// Every Def in the plan's folder, in path order: each ScriptableObject in each .asset file with its file's name
        /// (a file holding two shows twice, which <see cref="TerrainPlanValidation.Validate"/> refuses; a file holding
        /// none shows as null, which it names).
        /// </summary>
        public static List<ScriptableObject> LoadDefs(out List<string> files)
        {
            var defs = new List<ScriptableObject>();
            files = new List<string>();
            var paths = Directory.GetFiles(DefsFolder, "*.asset", SearchOption.TopDirectoryOnly)
                                 .Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal);
            foreach (string path in paths)
            {
                var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<ScriptableObject>().ToList();
                if (all.Count == 0) { defs.Add(null); files.Add(Path.GetFileName(path)); continue; }
                foreach (var so in all) { defs.Add(so); files.Add(Path.GetFileName(path)); }
            }
            return defs;
        }

        /// <summary>The plan's problems (<see cref="TerrainPlanValidation.Validate"/> over its folder); empty when all hold.</summary>
        public static List<string> Validate()
        {
            var defs = LoadDefs(out var files);
            return TerrainPlanValidation.Validate(LoadPlan(), defs, files);
        }

        // ---- the sources -------------------------------------------------------------------------------------------------

        public static PaintedHeightMap LoadSeabed()
        {
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(SeabedPath);
            if (map == null) throw new FileNotFoundException("[TerrainPlan] no PaintedHeightMap at " + SeabedPath + ".");
            if (map.HeightTexture == null) throw new InvalidDataException("[TerrainPlan] " + SeabedPath + " names no height texture.");
            return map;
        }

        /// <summary>The plan's grid: the height map's texels over its world rect, row 0 north.</summary>
        public static TerrainPlanGrid GridOf(PaintedHeightMap map)
        {
            var tex = map.HeightTexture;
            double w = TerrainPlanMath.Num(map.WorldSize.x), h = TerrainPlanMath.Num(map.WorldSize.y), mpp = w / tex.width;
            if (Math.Abs(h / tex.height - mpp) > 1e-9)
                throw new InvalidDataException("[TerrainPlan] the height map's cells are not square (" + w / tex.width + " × " + h / tex.height + " m).");
            return new TerrainPlanGrid(tex.width, tex.height, mpp, TerrainPlanMath.Num(map.WorldCenter.x) - w / 2, TerrainPlanMath.Num(map.WorldCenter.y) + h / 2);
        }

        /// <summary>The gather's input as the game holds it now: the builder's shapes, the scene and today's maps.</summary>
        public static TerrainPlanGatherInput GatherInput()
        {
            var map = LoadSeabed();
            var fleet = AssetDatabase.LoadAssetAtPath<AmbientFleetDef>(FleetPath);
            if (fleet == null) throw new FileNotFoundException("[TerrainPlan] no AmbientFleetDef at " + FleetPath + ".");
            string heightPath = AssetDatabase.GetAssetPath(map.HeightTexture);
            var splats = new string[TerrainPlanZones.SplatSlots / 4];
            for (int i = 0; i < splats.Length; i++) splats[i] = TerrainSplatAssets.PathOf(i);

            var x = new TerrainPlanGatherInput
            {
                Grid = GridOf(map),
                SceneText = File.ReadAllText(ScenePath),
                WharfRoot = StPetersPlaces.WharfRoot,
                HeightPng = File.ReadAllBytes(heightPath),
                HeightMin = map.MinElevation,
                HeightMax = map.MaxElevation,
                SplatPngs = splats.Select(p => File.ReadAllBytes(p)).ToArray(),
                Bands = new PlanShaderBands(),                        // the band rule today's paint was read by (terrain pass 9)
                StampFleet = false,                                   // the grounds are region-wide; the fleet's day plan is re-checked
                BerthSlip = Cap(StPetersBuilder.BerthFrom, StPetersBuilder.BerthTo, StPetersBuilder.BerthHalfWidth),
                ApproachCut = Cap(StPetersBuilder.ApproachFrom, StPetersBuilder.ApproachTo, StPetersBuilder.ApproachHalfWidth),
                Pocket = Cap(StPetersBuilder.BerthPocketFrom, StPetersBuilder.BerthPocketTo, StPetersBuilder.BerthPocketHalfWidth),
                Sandbar = Cap(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo, StPetersBuilder.SandbarHalfWidth),
                BarGut = Gut(),
                Entrance = TerrainPlanMath.Num(StPetersNavMarks.Entrance.Waypoints),
                EntranceHalfWidth = TerrainPlanMath.Num(StPetersNavMarks.Entrance.HalfWidthMetres),
                FleetMin = TerrainPlanMath.Num(fleet.GroundsCenter - fleet.GroundsSize * 0.5f),
                FleetMax = TerrainPlanMath.Num(fleet.GroundsCenter + fleet.GroundsSize * 0.5f),
                IslandCentre = TerrainPlanMath.Num(StPetersBuilder.IslandCenter),
                IslandRadiusX = TerrainPlanMath.Num(StPetersBuilder.IslandRadius),
                IslandRadiusY = TerrainPlanMath.Num(StPetersBuilder.IslandRadiusY),
            };
            foreach (var s in StPetersPlaces.Buildings) x.Buildings.Add(StPetersPlaces.Place(s, Member));
            foreach (var s in StPetersPlaces.DockPoints) x.DockPoints.Add(StPetersPlaces.Place(s, Member));
            foreach (var s in StPetersPlaces.Passages) x.Passages.Add(StPetersPlaces.Place(s, Member));
            foreach (string line in StPetersPlaces.Lines) x.Lines[line] = TerrainPlanMath.Num(Line(line));
            foreach (var sec in StPetersBuilder.CoastSectors) x.Sectors.Add(new PlanSector(TerrainPlanMath.Num(sec.FromBearing), sec.Class.ToString()));

            x.Provenance["gatheredBy"] = "StPetersTerrainPlan.Gather, Unity " + Application.unityVersion;
            x.Provenance["scene"] = ScenePath + " " + FileSha256(ScenePath);
            x.Provenance["height"] = heightPath + " " + FileSha256(heightPath);
            for (int i = 0; i < splats.Length; i++) x.Provenance["splat" + "ABCDE"[i]] = splats[i] + " " + FileSha256(splats[i]);
            x.Provenance["seabed"] = SeabedPath + " " + FileSha256(SeabedPath);
            x.Provenance["fleet"] = FleetPath + " " + FileSha256(FleetPath);
            x.Provenance["base"] = BaseFrom + "; recomputed at every derivation, not frozen";
            return x;
        }

        /// <summary>The sources as the game holds them now, today's ground sampled.</summary>
        public static TerrainPlanSources Gather()
        {
            var s = TerrainPlanGather.Assemble(GatherInput());
            s.Base = SampleBase(s.Grid);
            AttachGround(s, LoadPlan());
            return s;
        }

        /// <summary>
        /// Write the sources beside the Defs, as canonical text (LF, UTF-8, no BOM). Refuses to overwrite a frozen file
        /// unless <paramref name="overwrite"/>: the file is gathered once, before the maps are written.
        /// </summary>
        public static string Freeze(TerrainPlanSources s, bool overwrite)
        {
            if (File.Exists(SourcesPath) && !overwrite)
                throw new InvalidOperationException("[TerrainPlan] " + SourcesPath + " is frozen already; compare instead, or overwrite on purpose.");
            string text = TerrainPlanSourcesJson.Write(s, PlanId);
            File.WriteAllText(SourcesPath, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(SourcesPath, ImportAssetOptions.ForceSynchronousImport);
            return TerrainPlanSourcesJson.Sha256(text);
        }

        /// <summary>
        /// The frozen sources after a walls' patch (terrain PR 5w). The cliff walls' points of today's that a wall in
        /// <paramref name="sceneText"/> still stands on are kept, and the rest dropped (<see cref="TerrainPlanGather.KeepStanding"/>);
        /// every other root stays as frozen. Part 1 holds today's paint round today's walls: its keep and the guards' cliff
        /// discs read <see cref="TerrainPlanKeep.CliffRoot"/>, and its barren rule reads it beside the south's live walls (each
        /// at its first brow). So a wall the patch retires or moves would leave that paint where it stood: today's grass on the
        /// opened beach, where 044 to 055 were. The plan's own walls keep their paint by their footprints (part 2's south) and
        /// never enter the file, which stays today's.
        /// Writes only when a point drops and <paramref name="write"/>; returns how many drop.
        /// </summary>
        public static int KeepStandingWalls(string sceneText, bool write)
        {
            string text = File.ReadAllText(SourcesPath);
            string root = (LoadPlan().Keep ?? new TerrainPlanKeep()).CliffRoot;
            string o = TerrainPlanGather.KeepStanding(text, PlanId, sceneText, root, out int dropped);
            if (write && o != text)
            {
                File.WriteAllText(SourcesPath, o, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(SourcesPath, ImportAssetOptions.ForceSynchronousImport);
            }
            return dropped;
        }

        /// <summary>The frozen sources, today's ground sampled.</summary>
        public static TerrainPlanSources LoadFrozen()
        {
            if (!File.Exists(SourcesPath)) throw new FileNotFoundException("[TerrainPlan] no frozen sources at " + SourcesPath + " (gather and freeze them first).");
            var s = TerrainPlanSourcesJson.Read(File.ReadAllText(SourcesPath), out string planId);
            if (planId != PlanId) throw new InvalidDataException("[TerrainPlan] " + SourcesPath + " is the sources of '" + planId + "', not '" + PlanId + "'.");
            s.Base = SampleBase(s.Grid);
            AttachGround(s, LoadPlan());
            return s;
        }

        // ---- the ground file -----------------------------------------------------------------------------------------------

        /// <summary>
        /// The plan's ground file imported (terrain PR 5 B): its base and each patch read from their PNGs as the files
        /// hold them, the asks laid in order on the plan's height range, a channel hold keeping its bed below the spring
        /// low (−<see cref="RegionTerrainPlanDef.SpringM"/>). Null when the plan has no ground file.
        /// </summary>
        public static GroundFileImport.Result Import(RegionTerrainPlanDef plan)
        {
            var f = plan != null ? plan.Ground : null;
            if (f == null) return null;
            if (f.Base == null) throw new InvalidDataException("[TerrainPlan] " + f.Id + " has no base texture.");
            var img = TerrainPlanPng.ReadFile(AssetDatabase.GetAssetPath(f.Base));
            if (img.BitDepth != 16 || img.Channels != 1) throw new InvalidDataException("[TerrainPlan] " + f.Id + "'s base is not 16-bit grey.");
            var b = GroundFileImport.BaseOf(f, img.Channel(0), img.Width, img.Height);
            var asks = GroundFileImport.AsksOf(f, GroundFileIntake.PatchOf);
            return GroundFileImport.Run(b, asks, -TerrainPlanMath.Num(plan.SpringM), plan.HeightRange.x, plan.HeightRange.y);
        }

        /// <summary>The ground file's import on the sources, its grid the plan's own (or the import stops).</summary>
        static void AttachGround(TerrainPlanSources s, RegionTerrainPlanDef plan)
        {
            var r = Import(plan);
            if (r == null) return;
            var a = r.Grid; var b = s.Grid;
            if (a.W != b.W || a.H != b.H || a.Mpp != b.Mpp || a.X0 != b.X0 || a.Y1 != b.Y1)
                throw new InvalidDataException("[TerrainPlan] the ground file's grid (" + a.W + " × " + a.H + ") is not the plan's (" + b.W + " × " + b.H + ").");
            s.Import = r;
            s.Ground = TerrainPlanMaps.GroundOf(r, plan.HeightRange.x, plan.HeightRange.y);
        }

        /// <summary>How the game's sources now differ from the frozen file's (empty when they agree).</summary>
        public static List<string> CompareWithFrozen()
        {
            var now = TerrainPlanGather.Assemble(GatherInput());
            var frozen = TerrainPlanSourcesJson.Read(File.ReadAllText(SourcesPath), out _);
            return TerrainPlanGather.Compare(now, frozen, CompareTolerance);
        }

        /// <summary>Today's analytic ground at each cell centre (row-major, row 0 north), as the sim reads it.</summary>
        public static double[] SampleBase(TerrainPlanGrid g)
        {
            var go = new GameObject("TerrainPlan.Base") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var t = go.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(t);
                var e = new double[g.Count];
                for (int r = 0; r < g.H; r++)
                {
                    float y = (float)g.YG(r);
                    for (int c = 0; c < g.W; c++) e[r * g.W + c] = t.ElevationAt(new Vector2((float)g.XG(c), y));
                }
                return e;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---- a derivation and its guards ------------------------------------------------------------------------------

        /// <summary>A derivation from the frozen sources.</summary>
        public static TerrainPlanResult Derive(TerrainPlanStage last = TerrainPlanStage.Final) => TerrainPlanDerivation.Derive(LoadPlan(), LoadFrozen(), last);

        /// <summary>The village's buildings and yards, derived from its Defs as the village builder derives them.</summary>
        public static VillagePlanDerivation.Result LoadVillage()
        {
            var plans = VillageDefs<VillagePlanDef>();
            if (plans.Length != 1) throw new FileNotFoundException("[TerrainPlan] " + plans.Length + " village plans in " + VillageFolder + ", not one.");
            return VillagePlanDerivation.Derive(plans[0], VillageDefs<LotDef>(), VillageDefs<YardDef>(), VillageDefs<RouteDef>(), VillageDefs<LightPostDef>());
        }

        static T[] VillageDefs<T>() where T : Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { VillageFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).ToArray();

        /// <summary>The guards' input: the derivation, the scene as it stands, the ground file, the village, and the numbers
        /// the game keeps elsewhere.</summary>
        public static TerrainPlanGuardInput GuardInput(RegionTerrainPlanDef plan, TerrainPlanSources src, TerrainPlanResult r)
        {
            string scene = File.ReadAllText(ScenePath);
            var map = LoadSeabed();
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null) throw new FileNotFoundException("[TerrainPlan] no GameConfig at " + ConfigPath + ".");
            return new TerrainPlanGuardInput
            {
                Plan = plan, Sources = src, Result = r,
                Items = TerrainPlanSceneScan.Items(scene), SceneText = scene,
                Ground = plan.Ground, Village = LoadVillage(),
                WaterScriptGuid = ScriptGuid(typeof(WaterSurface)), SplatScriptGuid = ScriptGuid(typeof(TerrainSplatSurface)),
                MapMin = map.MinElevation, MapMax = map.MaxElevation,
                SpringLow = TerrainPlanMath.Num(StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude),
                SpringHigh = TerrainPlanMath.Num(StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude),
                NavFloor = TerrainPlanMath.Num(StPetersNavMarks.Tuning.MinDepthAtSpringLowMetres),
                WadeDepth = TerrainPlanMath.Num(config.WadeDepth),
                TidalPeriodHours = TerrainPlanMath.Num(config.TidalPeriodHours),
            };
        }

        // ---- helpers -----------------------------------------------------------------------------------------------------

        public static string FileSha256(string path)
        {
            using (var h = SHA256.Create()) return TerrainPlanResult.Hex(h.ComputeHash(File.ReadAllBytes(path)));
        }

        /// <summary>The guid of the MonoScript that defines <paramref name="t"/>.</summary>
        public static string ScriptGuid(Type t)
        {
            foreach (string guid in AssetDatabase.FindAssets(t.Name + " t:MonoScript"))
            {
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (ms != null && ms.GetClass() == t) return guid;
            }
            throw new FileNotFoundException("[TerrainPlan] no MonoScript defines " + t.FullName + ".");
        }

        static PlanCapsule Cap(Vector2 a, Vector2 b, float halfWidth) =>
            new PlanCapsule(TerrainPlanMath.Num(a), TerrainPlanMath.Num(b), TerrainPlanMath.Num(halfWidth));

        /// <summary>The gut across the bar, as the nav marks lay it (centre ± the approach along the axis), south end
        /// first, at the builder's channel half width.</summary>
        static PlanCapsule Gut()
        {
            Vector2 c = StPetersNavMarks.GutCentre(), across = StPetersNavMarks.GutAxis() * StPetersNavMarks.GutApproachMetres;
            PlanPoint a = TerrainPlanMath.Num(c - across), b = TerrainPlanMath.Num(c + across);
            if (a.Y > b.Y) { var t = a; a = b; b = t; }
            return new PlanCapsule(a, b, TerrainPlanMath.Num(StPetersBuilder.ChannelHalfWidth));
        }

        static PlanPoint P(Vector3 v) => new PlanPoint(TerrainPlanMath.Num(v.x), TerrainPlanMath.Num(v.y));

        /// <summary>The builder member a place is typed from (<see cref="StPetersPlaces"/> names them).</summary>
        static PlanPoint Member(string name)
        {
            switch (name)
            {
                case "StPetersBuilder.SchoolPos": return P(StPetersBuilder.SchoolPos);
                case "StPetersBuilder.GeneralStorePos": return P(StPetersBuilder.GeneralStorePos);
                case "StPetersBuilder.WhiteFarmhousePos": return P(StPetersBuilder.WhiteFarmhousePos);
                case "StPetersBuilder.RedSaltboxPos": return P(StPetersBuilder.RedSaltboxPos);
                case "StPetersBuilder.SageCottagePos": return P(StPetersBuilder.SageCottagePos);
                case "StPetersBuilder.VillageHearthPos": return P(StPetersBuilder.VillageHearthPos);
                case "StPetersBuilder.StartSpawnPos": return P(StPetersBuilder.StartSpawnPos);
                case "StPetersBuilder.WetBucketPos": return P(StPetersBuilder.WetBucketPos);
                case "StPetersBuilder.DockZonePos": return P(StPetersBuilder.DockZonePos);
                case "StPetersBuilder.DisembarkPos": return P(StPetersBuilder.DisembarkPos);
                case "StPetersBuilder.ArrivalPos": return P(StPetersBuilder.ArrivalPos);
                case "StPetersBuilder.DoryMooredPos": return P(StPetersBuilder.DoryMooredPos);
                case "StPetersBuilder.ToNineMileCreekPassagePos": return P(StPetersBuilder.ToNineMileCreekPassagePos);
                case "StPetersBuilder.ToWestWaterPassagePos": return P(StPetersBuilder.ToWestWaterPassagePos);
                case "StPetersBuilder.ToEastWaterPassagePos": return P(StPetersBuilder.ToEastWaterPassagePos);
                default: throw new ArgumentException("[TerrainPlan] the gather reads no member '" + name + "'.");
            }
        }

        static Vector2[] Line(string name)
        {
            switch (name)
            {
                case "StPetersStarterSplat.VillageToSlipPath": return StPetersStarterSplat.VillageToSlipPath();
                case "StPetersStarterSplat.VillageToBarHeadPath": return StPetersStarterSplat.VillageToBarHeadPath();
                default: throw new ArgumentException("[TerrainPlan] the gather reads no line '" + name + "'.");
            }
        }
    }
}
