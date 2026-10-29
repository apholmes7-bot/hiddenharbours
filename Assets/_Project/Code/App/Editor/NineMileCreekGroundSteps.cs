#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using G = HiddenHarbours.App.Editor.NineMileCreekGroundPlan;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// PR 2's activation of the third-issue ground (G3) at Nine Mile Creek, as six named N1 steps planned in
    /// order, each on the text the step before it leaves: 04 the ground, 05 the wharf's support on it, 06 the
    /// essential roads, 07 the loop's wall contacts, 08 the loop's contacts, 09 the nav marks. Each step is one
    /// or more one-root text patches (<see cref="LayerPatch"/>): every edited document keeps its id, every new
    /// one is named and hangs under its root, nothing is deleted, and planning the result again is empty.
    /// Steps 01 and 02 are unchanged; 03 is never planned; 10 is held for Dan's farmhouse.
    ///
    /// <para>H3 adds 11, the cliff walls by their real ids on G3: the <c>CliffWalls</c> root written from the walls'
    /// Defs (<see cref="NineMileCreekCliffWalls.WallsFolder"/>) by St Peters' own step,
    /// <see cref="StPetersLayerRefresh.CliffWalls"/>. It is the one step that deletes: a retired wall's four documents,
    /// each named, its Def keeping the id.</para>
    ///
    /// <para>H4 adds 12, the roads in kit v4 (<c>NineMileCreekGroundSteps.RoadsV4.cs</c>): the road floor's streamer on
    /// the splat, the network's three decks for 06's one platform, and the planting off the new carriageways. 06 is
    /// re-lined, not replaced: it paints only what kit v3 keeps (<see cref="G.PaveKeptV3"/>), so planning both again
    /// on what they wrote is empty.</para>
    /// </summary>
    public static partial class NineMileCreekGroundSteps
    {
        public const string GroundStep = "04-ground-g3";
        public const string WharfStep = "05-wharf-support-g3";
        public const string RoadsStep = "06-essential-roads-g3";
        public const string CliffStep = "07-loop-wall-contacts-g3";
        public const string ContactsStep = "08-loop-contacts-g3";
        public const string MarksStep = "09-nav-marks-g3";
        public const string WallsStep = "11-cliff-walls-g3";
        public static readonly string[] Steps =
            { GroundStep, WharfStep, RoadsStep, CliffStep, ContactsStep, MarksStep, WallsStep, RoadsV4Step };

        public const string PaintedTerrainScriptPath = "Assets/_Project/Code/World/PaintedTidalTerrain.cs";
        public const string TerrainRootName = "TidalTerrain";
        public const string SplatRootName = "TerrainSplat";
        public const string SeaRootName = "Sea";
        public const string ArrivalName = "NineMileCreekArrival";
        public const string DockZoneName = "NineMileCreekDockZone";
        public const string DisembarkName = "NineMileCreekDisembark";
        public const string AnchorName = "NineMileCreekRegionAnchor";
        public const string BerthLadderFittingName = "Fitting_ladder_berth0";

        /// <summary>The eight cliff chunks the scope selects for PR 2 (walls disposition, selectedForPR2): the
        /// ramp opening's obsolete face. Every other chunk stays for the later H3 decoration.</summary>
        public static readonly string[] Pr2CliffChunks =
        {
            "CliffWall_DeepShoreCliff_E_steep_069", "CliffWall_DeepShoreCliff_E_steep_070",
            "CliffWall_DeepShoreCliff_E_steep_071", "CliffWall_DeepShoreCliff_E_steep_077",
            "CliffWall_DeepShoreCliff_E_steep_078", "CliffWall_DeepShoreCliff_E_steep_079",
            "CliffWall_DeepShoreCliff_E_steep_080", "CliffWall_DeepShoreCliff_E_steep_081",
        };

        const int PolygonCollider2DClass = 60, EdgeCollider2DClass = 68, SpriteRendererClass = 212,
                  PrefabInstanceClass = 1001, TilemapClass = 1839735485;

        // =====================================================================================================
        //  Inputs: the ground, the files on disk, and the fleet register
        // =====================================================================================================

        /// <summary>The moored fleet's owner register, as the wall-berth patch needs it.</summary>
        public interface IWallBerths
        {
            /// <summary>The ids of every owner the register lays at the wall.</summary>
            IReadOnlyCollection<string> WallOwnerIds { get; }

            /// <summary>Resolve a moored hull's owner asset: its id, and its wall berth. False when she lies at
            /// the float.</summary>
            bool TryWallBerth(string ownerGuid, out string ownerId, out Vector2 berth);
        }

        /// <summary>The register through the asset database, as the builder reads it.</summary>
        public sealed class EditorBerths : IWallBerths
        {
            public IReadOnlyCollection<string> WallOwnerIds { get; }

            public EditorBerths() => WallOwnerIds = NineMileCreekMooredFleet.LoadOwners()
                .Where(o => !o.LiesAtTheFloat && o.IsPresentable()).Select(o => o.Id).ToList();

            public bool TryWallBerth(string ownerGuid, out string ownerId, out Vector2 berth)
            {
                var owner = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatOwnerDef>(
                    AssetDatabase.GUIDToAssetPath(ownerGuid));
                if (owner == null) throw new Refusal("A fleet hull has an unresolved owner reference.");
                ownerId = owner.Id;
                berth = owner.LiesAtTheFloat ? default
                    : WallBerth(owner.BerthIndex, NineMileCreekMooredFleet.HalfBeamOf(owner));
                return !owner.LiesAtTheFloat;
            }
        }

        /// <summary>A wall berth on the G3 north face: the builder's berth for that hull, moved with the face.</summary>
        public static Vector2 WallBerth(int index, float halfBeamMetres) =>
            NineMileCreekMainland.BerthPos(index, halfBeamMetres) + new Vector2(0f, G.WallBerthShiftMetres);

        /// <summary>One road tile on disk: its asset, its sprite and its flags.</summary>
        public sealed class RoadTile
        {
            public readonly ObjRef Asset, Sprite;
            public readonly int Flags;
            public RoadTile(ObjRef asset, ObjRef sprite, int flags) { Asset = asset; Sprite = sprite; Flags = flags; }
        }

        /// <summary>The nav buoy prefab as its instances' records see it: its guid, the fileIDs the records
        /// target, and the def and size rung an instance without those records wears.</summary>
        public sealed class NavBuoyPrefab
        {
            public readonly string Guid;
            public readonly long GameObject, Transform, Visual, Renderer;
            public readonly ObjRef Def;
            public readonly string SizeId;

            NavBuoyPrefab(string guid, long go, long tr, long visual, long renderer, ObjRef def, string sizeId)
            {
                Guid = guid; GameObject = go; Transform = tr; Visual = visual; Renderer = renderer; Def = def; SizeId = sizeId;
            }

            internal static NavBuoyPrefab Read(Inputs inputs)
            {
                string path = NineMileCreekNavMarks.PrefabPath;
                SceneYaml prefab = SceneYaml.Parse(File.ReadAllText(Path.Combine(inputs.ProjectRoot, path)).Replace("\r\n", "\n"));
                Doc go = prefab.RootNamed("NavBuoy");
                Doc visual = One(prefab.ComponentsOf(go).Where(d => d.IsScript("HiddenHarbours.Boats.NavBuoyVisual")), MarksStep, path + "'s NavBuoyVisual");
                Doc renderer = One(prefab.Docs.Where(d => d.ClassId == SpriteRendererClass), MarksStep, path + "'s renderer");
                if (go.Field("m_IsActive") != "1") throw new Refusal($"{MarksStep}: {path}'s root is not active.");
                return new NavBuoyPrefab(inputs.Guid(path), go.FileId, prefab.TransformOf(go).FileId, visual.FileId, renderer.FileId,
                                         ObjRef.Parse(visual.Field("_def")), visual.Field("_sizeId"));
            }
        }

        /// <summary>One nav-mark def on disk: its reference, and each size rung's sprites in facing order.</summary>
        public sealed class NavMarkDef
        {
            public readonly ObjRef Asset;
            public readonly IReadOnlyDictionary<string, List<ObjRef>> Facings;

            NavMarkDef(ObjRef asset, IReadOnlyDictionary<string, List<ObjRef>> facings) { Asset = asset; Facings = facings; }

            /// <summary>The sprite <c>NavBuoyVisual.Apply</c> dresses a mark of this def in, at a rung and facing.</summary>
            public ObjRef Sprite(string sizeId, int facing)
            {
                if (!Facings.TryGetValue(sizeId, out List<ObjRef> sprites) || sprites.Count == 0)
                    throw new Refusal($"{MarksStep}: the def {Asset.Guid} has no sprites for the size rung {sizeId}.");
                return sprites[Mathf.Clamp(facing, 0, sprites.Count - 1)];
            }

            /// <summary>Every def under the mark folder, by its mark type.</summary>
            internal static Dictionary<string, NavMarkDef> ReadAll(Inputs inputs)
            {
                var defs = new Dictionary<string, NavMarkDef>(StringComparer.Ordinal);
                string folder = NineMileCreekNavMarks.DefFolder;
                foreach (string file in Directory.GetFiles(Path.Combine(inputs.ProjectRoot, folder), "*.asset")
                                                 .OrderBy(f => f, StringComparer.Ordinal))
                {
                    string[] lines = File.ReadAllText(file).Replace("\r\n", "\n").Split('\n');
                    string type = null, size = null;
                    var facings = new Dictionary<string, List<ObjRef>>(StringComparer.Ordinal);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].StartsWith("  MarkType: ", StringComparison.Ordinal)) type = lines[i].Substring(12).Trim();
                        else if (lines[i].StartsWith("  - SizeId: ", StringComparison.Ordinal)) size = lines[i].Substring(12).Trim();
                        else if (lines[i] == "    Facings:" && size != null)
                        {
                            var sprites = new List<ObjRef>();
                            while (i + 1 < lines.Length && lines[i + 1].StartsWith("    - {", StringComparison.Ordinal))
                                sprites.Add(ObjRef.Parse(lines[++i]));
                            facings[size] = sprites;
                        }
                    }
                    if (type == null) continue;
                    if (defs.ContainsKey(type)) throw new Refusal($"{MarksStep}: two defs of the mark type {type} under {folder}.");
                    defs.Add(type, new NavMarkDef(new ObjRef(11400000, inputs.Guid(folder + "/" + Path.GetFileName(file)), 2), facings));
                }
                return defs;
            }
        }

        /// <summary>What the steps read besides the scene. Files are read from <see cref="ProjectRoot"/>, so the
        /// same plan runs in the editor and without it.</summary>
        public sealed class Inputs
        {
            public readonly ITidalTerrain Ground;
            public readonly IWallBerths Berths;
            public readonly string ProjectRoot;
            readonly Dictionary<string, RoadTile> _tiles = new Dictionary<string, RoadTile>(StringComparer.Ordinal);
            NavBuoyPrefab _navPrefab;
            Dictionary<string, NavMarkDef> _navDefs;
            IReadOnlyList<CliffWallDef> _walls;

            public Inputs(ITidalTerrain ground, IWallBerths berths, string projectRoot = ".",
                          IReadOnlyList<CliffWallDef> walls = null)
            {
                Ground = ground ?? throw new ArgumentNullException(nameof(ground));
                Berths = berths ?? throw new ArgumentNullException(nameof(berths));
                ProjectRoot = projectRoot;
                _walls = walls;
            }

            /// <summary>The region's wall Defs, live, retired and held (11): the ones handed in, else the walls
            /// folder's, read once.</summary>
            public IReadOnlyList<CliffWallDef> Walls => _walls ?? (_walls = NineMileCreekCliffWalls.LoadDefs());

            /// <summary>The GUID an asset's .meta records.</summary>
            public string Guid(string assetPath)
            {
                string meta = Path.Combine(ProjectRoot, assetPath + ".meta");
                if (!File.Exists(meta)) throw new Refusal($"{assetPath} has no .meta to take its GUID from.");
                Match m = Regex.Match(File.ReadAllText(meta), @"(?m)^guid: ([0-9a-f]{32})\r?$");
                if (!m.Success) throw new Refusal($"{assetPath}.meta records no GUID.");
                return m.Groups[1].Value;
            }

            public ObjRef PaintedTerrainScript => new ObjRef(11500000, Guid(PaintedTerrainScriptPath), 3);
            public ObjRef GroundMap => new ObjRef(11400000, Guid(NineMileCreekGround.MapPath), 2);
            public ObjRef GroundHeight => new ObjRef(2800000, Guid(NineMileCreekGround.HeightPath), 3);

            /// <summary>The road tile <paramref name="tileName"/>. A tile not on disk stops the step: the road
            /// kit has not been built for that surface (Build Road Kit v3 Tiles).</summary>
            public RoadTile Tile(string tileName)
            {
                if (_tiles.TryGetValue(tileName, out RoadTile tile)) return tile;
                string path = RoadTileLibrary.TilePath(tileName);
                string file = Path.Combine(ProjectRoot, path);
                if (!File.Exists(file)) throw new Refusal($"{RoadsStep}: the road tile {path} is not on disk; build the road kit first.");
                string text = File.ReadAllText(file);
                Match sprite = Regex.Match(text, @"(?m)^  m_Sprite: (.*?)\r?$");
                Match flags = Regex.Match(text, @"(?m)^  m_Flags: (\d+)\r?$");
                if (!sprite.Success || !flags.Success) throw new Refusal($"{RoadsStep}: {path} is not a tile asset.");
                tile = new RoadTile(new ObjRef(11400000, Guid(path), 2), ObjRef.Parse(sprite.Groups[1].Value),
                    int.Parse(flags.Groups[1].Value, CultureInfo.InvariantCulture));
                _tiles.Add(tileName, tile);
                return tile;
            }

            /// <summary>The nav buoy prefab, read once.</summary>
            public NavBuoyPrefab NavPrefab => _navPrefab ?? (_navPrefab = NavBuoyPrefab.Read(this));

            /// <summary>The def of one mark type. A type with no def on disk stops the step: the kit has not been
            /// baked for it.</summary>
            public NavMarkDef NavDef(string markType)
            {
                if (_navDefs == null) _navDefs = NavMarkDef.ReadAll(this);
                return _navDefs.TryGetValue(markType, out NavMarkDef def) ? def
                    : throw new Refusal($"{MarksStep}: no def of the mark type {markType} under {NineMileCreekNavMarks.DefFolder}.");
            }
        }

        // =====================================================================================================
        //  The plan
        // =====================================================================================================

        /// <summary>One named step: its one-root patches, in the order they were planned and applied.</summary>
        public sealed class Step
        {
            public readonly string Name;
            public readonly List<LayerPatch> Patches = new List<LayerPatch>();
            public Step(string name) => Name = name;
            public bool IsEmpty => Patches.All(p => p.IsEmpty);
            public int Deletions => Patches.Sum(p => p.Deletions.Count());
            public string Summary() => string.Join("\n", Patches.Select(p => p.Summary()));
        }

        /// <summary>Plan 04 to 09, then 11 and 12, in order, each patch on the text the one before it leaves; returns the steps
        /// and, in <paramref name="after"/>, the scene they write.</summary>
        public static List<Step> PlanAll(string sceneText, Inputs inputs, out string after)
        {
            var planners = new (string step, Func<SceneYaml, LayerPatch> plan)[]
            {
                (GroundStep, s => Ground(s, inputs)),
                (GroundStep, s => Splat(s, inputs)),
                (GroundStep, s => Sea(s, inputs)),
                (WharfStep, s => Wharf(s, inputs)),
                (WharfStep, s => Face(s, inputs)),
                (WharfStep, ApronGear),
                (WharfStep, WharfPumps),
                (WharfStep, s => WallBerths(s, WharfStep, inputs.Berths)),
                (RoadsStep, s => Roads(s, inputs)),
                (CliffStep, s => Cliffs(s, inputs)),
                (ContactsStep, s => Marker(s, ArrivalName, G.ArrivalPos, G.ArrivalRotation)),
                (ContactsStep, s => Marker(s, DockZoneName, G.DockZonePos, null)),
                (ContactsStep, s => Marker(s, DisembarkName, G.DisembarkPos, null)),
                (ContactsStep, Anchor),
                (ContactsStep, Otter),
                (MarksStep, s => Marks(s, inputs)),
                (WallsStep, s => Walls(s, inputs)),
            };
            var steps = new List<Step>();
            string text = sceneText;
            foreach (var (name, plan) in planners.Concat(RoadsV4Planners(inputs)))
            {
                if (steps.Count == 0 || steps[steps.Count - 1].Name != name) steps.Add(new Step(name));
                SceneYaml scene = SceneYaml.Parse(text);
                LayerPatch patch = plan(scene);
                steps[steps.Count - 1].Patches.Add(patch);
                text = patch.Apply(scene).Render();
            }
            after = text;
            return steps;
        }

        /// <summary>The file a patch is written to under the N1 patch folder.</summary>
        public static string PatchFileName(LayerPatch patch) => $"{patch.Step}.{patch.Root.Replace('/', '-')}.patch.yaml";

        // =====================================================================================================
        //  04: the ground
        // =====================================================================================================

        /// <summary>The terrain becomes the painted G3 field on its kept component id.</summary>
        static LayerPatch Ground(SceneYaml scene, Inputs inputs)
        {
            Doc go = scene.RootNamed(TerrainRootName);
            var patch = new LayerPatch(GroundStep, TerrainRootName, go.FileId);
            Doc terrain = One(scene.ComponentsOf(go).Where(d => d.IsScript("HiddenHarbours.World.MainlandTidalTerrain") ||
                                                                d.IsScript("HiddenHarbours.World.PaintedTidalTerrain")),
                              GroundStep, "the region's tidal terrain");
            string text = $"--- !u!114 &{terrain.FileId}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n" +
                          "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n" +
                          $"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {Ref(go.FileId)}\n  m_Enabled: 1\n" +
                          $"  m_EditorHideFlags: 0\n  m_Script: {inputs.PaintedTerrainScript.Yaml}\n  m_Name: \n" +
                          "  m_EditorClassIdentifier: HiddenHarbours.World::HiddenHarbours.World.PaintedTidalTerrain\n" +
                          $"  _map: {inputs.GroundMap.Yaml}\n  _fallbackElevation: {N(-10f)}";
            patch.Edit(terrain, text, "TidalTerrain/terrain component (painted G3)");
            patch.Note(TerrainRootName, "ground", "MainlandTidalTerrain replaced by PaintedTidalTerrain on the G3 map; component id kept");
            return patch.Seal(scene);
        }

        /// <summary>The ground's splat reads its height from G3.</summary>
        static LayerPatch Splat(SceneYaml scene, Inputs inputs)
        {
            Doc go = scene.RootNamed(SplatRootName);
            var patch = new LayerPatch(GroundStep, SplatRootName, go.FileId);
            Doc splat = One(scene.ComponentsOf(go).Where(d => d.IsScript("HiddenHarbours.Art.TerrainSplatSurface")),
                            GroundStep, "the splat surface");
            RequireRange(splat, GroundStep);
            patch.Edit(splat, Set(splat.Text, "_heightTexture", inputs.GroundHeight.Yaml), "TerrainSplat/height texture");
            return patch.Seal(scene);
        }

        /// <summary>The sea's depth comes from the painted G3 height, as St Peters' does.</summary>
        static LayerPatch Sea(SceneYaml scene, Inputs inputs)
        {
            Doc go = scene.RootNamed(SeaRootName);
            var patch = new LayerPatch(GroundStep, SeaRootName, go.FileId);
            Doc water = One(scene.ComponentsOf(go).Where(d => d.IsScript("HiddenHarbours.Art.WaterSurface")),
                            GroundStep, "the sea surface");
            RequireRange(water, GroundStep);
            if (water.Field("_bakeHeightMap") != "1" || water.Field("_heightWorldCenter") != "{x: 0, y: 0}" ||
                water.Field("_heightWorldSize") != Vec2(new Vector2(760f, 560f)))
                throw new Refusal($"{GroundStep}: the sea's height bake is not the region's 760 by 560 field.");
            string text = Set(water.Text, "_depthSource", ((int)WaterSurface.DepthSource.PaintedHeightMap).ToString(CultureInfo.InvariantCulture));
            text = Set(text, "_paintedHeightTex", inputs.GroundHeight.Yaml);
            patch.Edit(water, text, "Sea/depth from the painted G3 height");
            return patch.Seal(scene);
        }

        static void RequireRange(Doc doc, string step)
        {
            if (ParseFloat(doc.Field("_heightMin")) != NineMileCreekGround.Minimum ||
                ParseFloat(doc.Field("_heightMax")) != NineMileCreekGround.Maximum)
                throw new Refusal($"{step}: &{doc.FileId} decodes height over another range than G3's.");
        }

        // =====================================================================================================
        //  05: the wharf's support on G3
        // =====================================================================================================

        static LayerPatch Wharf(SceneYaml scene, Inputs inputs)
        {
            const string step = WharfStep;
            ITidalTerrain g3 = inputs.Ground;
            Doc root = scene.RootNamed(NineMileCreekWharf.RootName);
            Doc rootTr = scene.TransformOf(root);
            RequireIdentity(rootTr, step);
            var patch = new LayerPatch(step, NineMileCreekWharf.RootName, root.FileId);
            var ids = new IdAllocator(scene);

            // Row 1: two narrow engineered caps on the kept platforms and their stable ids.
            Cap(scene, patch, rootTr, "NorthWall", "wharf.nine_mile_creek", G.NorthCap);
            Cap(scene, patch, rootTr, "WestWall", "wharf.nine_mile_creek.apron", G.CraneCap);

            // Rows 1 and 3: the quay blockers. PropertyBoundary's runs are the authored fact and its colliders
            // the physics, so both are re-lined together; the cut is the gangway's 1.2 m opening.
            Doc walls = RequireChild(scene, rootTr, NineMileCreekWalls.RootName, step);
            Doc wallsTr = scene.TransformOf(walls);
            RequireIdentity(wallsTr, step);
            Doc boundary = One(scene.ComponentsOf(walls).Where(d => d.IsScript("HiddenHarbours.World.PropertyBoundary")),
                               step, "QuayWalls' boundary");
            float half = ParseFloat(boundary.Field("_thicknessMetres")) * 0.5f;
            Vector2[][] runs = QuayRuns(half);
            patch.Edit(boundary, SetBlock(boundary.Text, "_runs", RunsYaml(runs)), "QuayWalls/PropertyBoundary runs");
            Doc colliders = RequireChild(scene, wallsTr, PropertyBoundary.CollidersChildName, step);
            RequireIdentity(scene.TransformOf(colliders), step);
            List<Doc> quads = scene.ComponentsOf(colliders).Where(d => d.ClassId == PolygonCollider2DClass).ToList();
            if (quads.Count != 8) throw new Refusal($"{step}: QuayWalls/Walls holds {quads.Count} blockers, not the eight the scope re-lines.");
            // In component order the blockers are the old north, its east return, the west apron's five and
            // its south return. The north keeps its id as two paths either side of the opening; the old south
            // return carries the crane face and the old apron corner the step; the five basin ghosts retire.
            patch.Edit(quads[0], Blocker(quads[0].Text, true, Quad(runs[0][2], runs[0][3], half), Quad(runs[1][0], runs[1][1], half)),
                       "QuayWalls/Walls north blocker (two paths)");
            patch.Edit(quads[7], Blocker(quads[7].Text, true, Quad(runs[0][0], runs[0][1], half)), "QuayWalls/Walls crane-face blocker");
            patch.Edit(quads[6], Blocker(quads[6].Text, true, Quad(runs[0][1], runs[0][2], half)), "QuayWalls/Walls step blocker");
            for (int i = 1; i <= 5; i++)
                patch.Edit(quads[i], Set(quads[i].Text, "m_Enabled", "0"), $"QuayWalls/Walls retired blocker {i}");

            // Row 3: the breakwater's edge on the source arms, the kept component on the south arm.
            Doc breakwater = RequireChild(scene, rootTr, "Breakwater", step);
            RequireIdentity(scene.TransformOf(breakwater), step);
            List<Doc> edges = scene.ComponentsOf(breakwater).Where(d => d.ClassId == EdgeCollider2DClass).ToList();
            if (edges.Count < 1 || edges.Count > 2) throw new Refusal($"{step}: the breakwater holds {edges.Count} edges.");
            patch.Edit(edges[0], Edge(edges[0].Text, G.SouthArm), "Breakwater/edge south arm");
            long eastId = edges.Count == 2 ? edges[1].FileId : ids.Next(step + "/Breakwater/edge east arm");
            Put(patch, edges.Count == 2 ? edges[1] : null, Edge(Header(edges[0].Text, eastId), G.EastArm), "Breakwater/edge east arm");
            patch.Edit(breakwater, WithListed(breakwater.Text, "m_Component", eastId, "component: "), "Breakwater/components");
            // The old arm's drawn cribs stand across G3's dredged basin and the TRACK: they stop drawing, ids kept
            // (the owner's ruling), as the old quay's ghost courses do.
            int cribs = 0;
            foreach (Doc crib in Children(scene, scene.TransformOf(breakwater)))
            {
                string name = crib.Field("m_Name");
                if (!name.StartsWith("Crib_", StringComparison.Ordinal)) throw new Refusal($"{step}: the breakwater holds '{name}', not a crib.");
                patch.Edit(crib, Set(crib.Text, "m_IsActive", "0"), $"Breakwater/{name} retired");
                cribs++;
            }

            // Row 4: the float's bed from its footprint; the gangway to the north face.
            Doc dock = RequireChild(scene, rootTr, "FloatingDock", step);
            Doc dockTr = scene.TransformOf(dock);
            RequireIdentity(dockTr, step);
            Doc floatGo = RequireChild(scene, dockTr, "Float", step);
            Doc floating = One(scene.ComponentsOf(floatGo).Where(d => d.IsScript("HiddenHarbours.World.FloatingPlatform")),
                               step, "the float");
            if (ReadRect(floating, "_footprint") != NineMileCreekWharf.FloatFootprint())
                throw new Refusal($"{step}: the float's footprint is not the builder's.");
            patch.Edit(floating, Set(floating.Text, "_bedElevation", N(NineMileCreekWharf.FloatBedElevationFrom(g3))),
                       "Float/bed elevation");
            Doc gangwayGo = RequireChild(scene, dockTr, "Gangway", step);
            Doc gangwayTr = scene.TransformOf(gangwayGo);
            Doc gangway = One(scene.ComponentsOf(gangwayGo).Where(d => d.IsScript("HiddenHarbours.World.GangwayPlatform")),
                              step, "the gangway");
            string g = Set(gangway.Text, "_shoreEnd", Vec2(G.GangwayShoreEnd));
            g = Set(g, "_floatEnd", Vec2(G.GangwayFloatEnd));
            g = Set(g, "_abutmentElevation", N(G.DeckElevation));
            patch.Edit(gangway, g, "Gangway/ends and abutment");
            patch.Edit(gangwayTr, MovedTo(gangwayTr, (G.GangwayShoreEnd + G.GangwayFloatEnd) * 0.5f), "Gangway/Transform");

            // Row 5: the wharf ladder to the cap, and exactly one player-berth ladder.
            Doc ladders = RequireChild(scene, rootTr, "Ladders", step);
            Doc laddersTr = scene.TransformOf(ladders);
            RequireIdentity(laddersTr, step);
            Doc ladder = RequireChild(scene, laddersTr, "Ladder_0", step);
            Doc ladderTr = scene.TransformOf(ladder);
            Doc climb = One(scene.ComponentsOf(ladder).Where(d => d.IsScript("HiddenHarbours.World.WharfLadder")), step, "Ladder_0");
            patch.Edit(ladderTr, MovedTo(ladderTr, G.WharfLadderPos), "Ladder_0/Transform");
            patch.Edit(climb, Set(climb.Text, "_topElevationMeters", N(G.DeckElevation)), "Ladder_0/top");
            Doc berthLadder = Child(scene, laddersTr, G.BerthLadderName, step);
            Doc berthTr = berthLadder != null ? scene.TransformOf(berthLadder) : null;
            Doc berthClimb = berthLadder != null
                ? One(scene.ComponentsOf(berthLadder).Where(d => d.IsScript("HiddenHarbours.World.WharfLadder")), step, G.BerthLadderName)
                : null;
            long bGo = berthLadder?.FileId ?? ids.Next(step + "/" + G.BerthLadderName + "/go");
            long bTr = berthTr?.FileId ?? ids.Next(step + "/" + G.BerthLadderName + "/transform");
            long bClimb = berthClimb?.FileId ?? ids.Next(step + "/" + G.BerthLadderName + "/ladder");
            Put(patch, berthLadder, NamedGameObject(ladder.Text, bGo, G.BerthLadderName, bTr, bClimb), G.BerthLadderName);
            Put(patch, berthTr, ChildTransform(ladderTr.Text, bTr, bGo, G.BerthLadderPos, laddersTr.FileId), G.BerthLadderName + "/Transform");
            string bc = Set(Header(climb.Text, bClimb), "m_GameObject", Ref(bGo));
            bc = Set(bc, "_id", G.BerthLadderId);
            bc = Set(bc, "_topElevationMeters", N(G.DeckElevation));
            bc = Set(bc, "_faceHeadingDegrees", N(180f));
            Put(patch, berthClimb, bc, G.BerthLadderName + "/WharfLadder");
            patch.Edit(laddersTr, WithListed(laddersTr.Text, "m_Children", bTr), "Ladders/children");

            // Rows 4 and 5: the fittings with the face and the cleat row, and the berth ladder's picture.
            Doc fittings = RequireChild(scene, rootTr, "Fittings", step);
            Doc fittingsTr = scene.TransformOf(fittings);
            RequireIdentity(fittingsTr, step);
            Doc ladderPicture = null, berthPicture = null;
            foreach (Doc go in Children(scene, fittingsTr))
            {
                string name = go.Field("m_Name");
                if (name == BerthLadderFittingName) { berthPicture = go; continue; }
                if (name == "Fitting_ladder") ladderPicture = ladderPicture == null ? go
                    : throw new Refusal($"{step}: two Fitting_ladder pictures.");
                Doc tr = scene.TransformOf(go);
                Vector3 at = Position(tr);
                Vector2? to = null;
                if (name == "Fitting_cleat") to = G.WestCleat;
                else if ((name == "Fitting_bollard" || name == "Fitting_pilehead") && at.y == G.OldNorthFaceY + 0.5f)
                    to = new Vector2(at.x, G.CleatRowY);
                else if ((name == "Fitting_tyre" || name == "Fitting_ladder") && at.y == G.OldNorthFaceY)
                    to = new Vector2(at.x, G.NorthFaceY);
                if (to == null) continue;
                string label = $"Fittings/{name}@{N(at.x)}";
                patch.Edit(tr, MovedTo(tr, to.Value), label + "/Transform");
                Order(scene, patch, go, to.Value.y, label);
            }
            if (ladderPicture == null) throw new Refusal($"{step}: no Fitting_ladder to picture the berth ladder.");
            var pictureDocs = scene.ComponentsOf(ladderPicture);
            Doc pTr = scene.TransformOf(ladderPicture);
            Doc pSr = One(pictureDocs.Where(d => d.ClassId == SpriteRendererClass), step, "Fitting_ladder renderer");
            Doc pSort = One(pictureDocs.Where(d => d.IsScript("HiddenHarbours.Art.YSortSprite")), step, "Fitting_ladder y-sort");
            var berthPictureDocs = berthPicture != null ? scene.ComponentsOf(berthPicture) : null;
            Doc qTr = berthPicture != null ? scene.TransformOf(berthPicture) : null;
            Doc qSr = berthPictureDocs?.Single(d => d.ClassId == SpriteRendererClass);
            Doc qSort = berthPictureDocs?.Single(d => d.IsScript("HiddenHarbours.Art.YSortSprite"));
            long qGo = berthPicture?.FileId ?? ids.Next(step + "/" + BerthLadderFittingName + "/go");
            long qTrId = qTr?.FileId ?? ids.Next(step + "/" + BerthLadderFittingName + "/transform");
            long qSrId = qSr?.FileId ?? ids.Next(step + "/" + BerthLadderFittingName + "/sprite");
            long qSortId = qSort?.FileId ?? ids.Next(step + "/" + BerthLadderFittingName + "/ysort");
            Put(patch, berthPicture, NamedGameObject(ladderPicture.Text, qGo, BerthLadderFittingName, qTrId, qSrId, qSortId),
                BerthLadderFittingName);
            Put(patch, qTr, ChildTransform(pTr.Text, qTrId, qGo, G.BerthLadderPos, fittingsTr.FileId), BerthLadderFittingName + "/Transform");
            string qs = Set(Header(pSr.Text, qSrId), "m_GameObject", Ref(qGo));
            qs = Set(qs, "m_SortingOrder", OrderOf(pSort, G.BerthLadderPos.y).ToString(CultureInfo.InvariantCulture));
            Put(patch, qSr, qs, BerthLadderFittingName + "/SpriteRenderer");
            Put(patch, qSort, Set(Header(pSort.Text, qSortId), "m_GameObject", Ref(qGo)), BerthLadderFittingName + "/YSortSprite");
            patch.Edit(fittingsTr, WithListed(fittingsTr.Text, "m_Children", qTrId), "Fittings/children");

            // Row 5: the shore cleats to the cap's row, their elevations from G3.
            Doc cleats = RequireChild(scene, rootTr, "MooringCleats", step);
            Doc cleatsTr = scene.TransformOf(cleats);
            RequireIdentity(cleatsTr, step);
            int west = 0;
            foreach (Doc go in Children(scene, cleatsTr))
            {
                string name = go.Field("m_Name");
                Doc tr = scene.TransformOf(go);
                Vector3 at = Position(tr);
                Vector2 to;
                if (name.StartsWith("Cleat_bollard_", StringComparison.Ordinal))
                    to = at.y == G.OldNorthFaceY + 0.5f ? new Vector2(at.x, G.CleatRowY) : (Vector2)at;
                else if (name.StartsWith("Cleat_cleat_", StringComparison.Ordinal) && west++ == 0)
                    to = G.WestCleat;
                else throw new Refusal($"{step}: an unowned shore cleat '{name}'.");
                Doc cleat = One(scene.ComponentsOf(go).Where(d => d.IsScript("HiddenHarbours.World.ShoreCleat")), step, name);
                patch.Edit(tr, MovedTo(tr, to), $"MooringCleats/{name}/Transform");
                patch.Edit(cleat, Set(cleat.Text, "_elevationMeters", N(g3.ElevationAt(to))), $"MooringCleats/{name}/elevation");
            }

            patch.Note(NineMileCreekWharf.RootName, "support on G3",
                "caps y86/y76 at deck 3.4; blockers re-lined with their runs; breakwater edge on the source arms; " +
                $"{cribs} old cribs inactive; float bed from G3; gangway to the north face; ladder and berth ladder; " +
                "fittings and cleats to the cap");
            return patch.Seal(scene);
        }

        /// <summary>The G3 quay line as PropertyBoundary runs: the crane face, the step and the north face to the
        /// gangway opening, then the north face beyond it. The opening's ends stand a blocker's half thickness
        /// back, so its clear width is the gangway's opening exactly.</summary>
        public static Vector2[][] QuayRuns(float halfThickness)
        {
            float west = G.GangwayShoreEnd.x - G.GangwayOpeningHalfWidth - halfThickness;
            float east = G.GangwayShoreEnd.x + G.GangwayOpeningHalfWidth + halfThickness;
            return new[]
            {
                new[]
                {
                    new Vector2(G.CraneFaceWestX, G.CraneFaceY), new Vector2(G.FaceStepX, G.CraneFaceY),
                    new Vector2(G.FaceStepX, G.NorthFaceY), new Vector2(west, G.NorthFaceY),
                },
                new[] { new Vector2(east, G.NorthFaceY), new Vector2(G.NorthFaceEastX, G.NorthFaceY) },
            };
        }

        /// <summary>One blocker quad, exactly as <see cref="PropertyBoundary.Rebuild"/> computes it.</summary>
        public static Vector2[] Quad(Vector2 a, Vector2 b, float half)
        {
            Vector2 along = b - a;
            float len = along.magnitude;
            along /= len;
            Vector2 side = new Vector2(-along.y, along.x) * half;
            Vector2 ext = along * half;
            return new[] { a - ext + side, b + ext + side, b + ext - side, a - ext - side };
        }

        static void Cap(SceneYaml scene, LayerPatch patch, Doc parentTr, string name, string id, Rect footprint)
        {
            Doc go = RequireChild(scene, parentTr, name, patch.Step);
            Doc tr = scene.TransformOf(go);
            Doc platform = One(scene.ComponentsOf(go).Where(d => d.IsScript("HiddenHarbours.World.StandablePlatform")),
                               patch.Step, name);
            if (platform.Field("_id") != id) throw new Refusal($"{patch.Step}: {name} is not {id}.");
            patch.Edit(tr, MovedTo(tr, footprint.center), name + "/Transform");
            patch.Edit(platform, Platform(platform.Text, footprint, G.DeckElevation), name + "/footprint and deck");
        }

        static string Platform(string text, Rect r, float deck) =>
            Set(SetBlock(text, "_footprint", new[]
            {
                "    serializedVersion: 2", $"    x: {N(r.x)}", $"    y: {N(r.y)}", $"    width: {N(r.width)}",
                $"    height: {N(r.height)}",
            }), "_deckElevation", N(deck));

        static Rect ReadRect(Doc doc, string key)
        {
            string[] lines = doc.Lines;
            int at = Array.IndexOf(lines, "  " + key + ":");
            if (at < 0 || at + 5 >= lines.Length) throw new Refusal($"&{doc.FileId} has no {key}.");
            float Value(int i, string name) => lines[at + i].StartsWith("    " + name + ": ", StringComparison.Ordinal)
                ? ParseFloat(lines[at + i].Substring(name.Length + 6))
                : throw new Refusal($"&{doc.FileId}'s {key} is not a serialized Rect.");
            return new Rect(Value(2, "x"), Value(3, "y"), Value(4, "width"), Value(5, "height"));
        }

        static IEnumerable<string> RunsYaml(Vector2[][] runs)
        {
            foreach (Vector2[] run in runs)
            {
                yield return "  - Points:";
                foreach (Vector2 p in run) yield return "    - " + Vec2(p);
            }
        }

        static string Blocker(string text, bool enabled, params Vector2[][] paths)
        {
            var body = new List<string> { "    m_Paths:" };
            foreach (Vector2[] path in paths)
                for (int i = 0; i < path.Length; i++)
                    body.Add((i == 0 ? "    - - " : "      - ") + Vec2(path[i]));
            return Set(SetBlock(text, "m_Points", body), "m_Enabled", enabled ? "1" : "0");
        }

        static string Edge(string text, Vector2[] points) =>
            SetBlock(text, "m_Points", points.Select(p => "  - " + Vec2(p)).ToList());

        static void Order(SceneYaml scene, LayerPatch patch, Doc go, float y, string label)
        {
            var docs = scene.ComponentsOf(go);
            Doc sr = One(docs.Where(d => d.ClassId == SpriteRendererClass), patch.Step, label + " renderer");
            Doc sort = One(docs.Where(d => d.IsScript("HiddenHarbours.Art.YSortSprite")), patch.Step, label + " y-sort");
            patch.Edit(sr, Set(sr.Text, "m_SortingOrder", OrderOf(sort, y).ToString(CultureInfo.InvariantCulture)),
                       label + "/sorting order");
        }

        /// <summary>The order a static <see cref="YSortSprite"/> gives its renderer at <paramref name="y"/>.</summary>
        static int OrderOf(Doc sort, float y) => YSortSprite.OrderFor(y + ParseFloat(sort.Field("_pivotYOffset")),
            ParseFloat(sort.Field("_baseOrder")), ParseFloat(sort.Field("_orderPerUnit")),
            ParseInt(sort.Field("_minOrder")), ParseInt(sort.Field("_maxOrder")));

        /// <summary>Row 2: the nine north courses keep their picture and move with the face to y 86, their
        /// waterline measured on G3; every other course is a ghost of the old quay and retires.</summary>
        static LayerPatch Face(SceneYaml scene, Inputs inputs)
        {
            const string step = WharfStep;
            Doc dressing = scene.RootNamed(NineMileCreekDressing.RootName);
            Doc dressingTr = scene.TransformOf(dressing);
            RequireIdentity(dressingTr, step);
            Doc face = RequireChild(scene, dressingTr, NineMileCreekDressing.FaceRootName, step);
            Doc faceTr = scene.TransformOf(face);
            RequireIdentity(faceTr, step);
            var patch = new LayerPatch(step, "NineMileCreekDressing/QuayFace", face.FileId);
            int kept = 0, retired = 0;
            foreach (Doc go in Children(scene, faceTr))
            {
                string name = go.Field("m_Name");
                if (!name.StartsWith(NineMileCreekDressing.NorthWallRun + "_", StringComparison.Ordinal))
                {
                    patch.Edit(go, Set(go.Text, "m_IsActive", "0"), $"QuayFace/{name} retired");
                    retired++;
                    continue;
                }
                Doc tr = scene.TransformOf(go);
                Doc tide = One(scene.ComponentsOf(go).Where(d => d.IsScript("HiddenHarbours.Art.TidalFaceWaterline")), step, name);
                Vector3 pivot = Position(tr);
                float rise = ParseFloat(tide.Field("_lipWorldY")) - pivot.y;
                var lip = new Vector2(pivot.x, G.NorthFaceY);
                patch.Edit(tr, MovedTo(tr, new Vector2(pivot.x, G.NorthFaceY - rise)), $"QuayFace/{name}/Transform");
                // NineMileCreekDressing.FaceLipElevation: the ground half a course width inboard of the lip.
                float elevation = inputs.Ground.ElevationAt(lip + Vector2.up * (NineMileCreekQuayFace.FaceCourseWidthMetres * 0.5f));
                string t = Set(tide.Text, "_lipWorldY", N(G.NorthFaceY));
                patch.Edit(tide, Set(t, "_lipElevation", N(elevation)), $"QuayFace/{name}/waterline");
                kept++;
            }
            patch.Note("QuayFace", "north face on G3", $"{kept} north courses to the y86 lip; {retired} ghost courses inactive");
            return patch.Seal(scene);
        }

        /// <summary>The apron's working gear stood on the old west apron, which is G3's dredged basin: all of it
        /// stops drawing, ids kept (the owner's ruling). The davit hoist's id stays reserved for the crane.</summary>
        static LayerPatch ApronGear(SceneYaml scene)
        {
            const string step = WharfStep;
            Doc dressing = scene.RootNamed(NineMileCreekDressing.RootName);
            Doc dressingTr = scene.TransformOf(dressing);
            RequireIdentity(dressingTr, step);
            Doc gear = RequireChild(scene, dressingTr, NineMileCreekDressing.ApronRootName, step);
            var patch = new LayerPatch(step, NineMileCreekDressing.RootName + "/" + NineMileCreekDressing.ApronRootName, gear.FileId);
            int retired = 0;
            foreach (Doc go in Children(scene, scene.TransformOf(gear)))
            {
                patch.Edit(go, Set(go.Text, "m_IsActive", "0"), $"{NineMileCreekDressing.ApronRootName}/{go.Field("m_Name")} retired");
                retired++;
            }
            patch.Note(NineMileCreekDressing.ApronRootName, "over the dredged basin", $"{retired} props inactive, ids kept");
            return patch.Seal(scene);
        }

        /// <summary>The wharf's two fuel pumps stood on that apron too: both stand down with their ids kept (the owner's
        /// ruling) until H5's drawn wharf re-sites them, and boats fuel by can from Route 91. One edit stops the pedestals
        /// and their hoses together; the step refuses unless both ids are under it.</summary>
        static LayerPatch WharfPumps(SceneYaml scene)
        {
            const string step = WharfStep;
            Doc fuel = scene.RootNamed(NineMileCreekStation.RootName);
            Doc pumps = RequireChild(scene, scene.TransformOf(fuel), NineMileCreekStation.WharfRootName, step);
            var patch = new LayerPatch(step, NineMileCreekStation.RootName + "/" + NineMileCreekStation.WharfRootName, pumps.FileId);
            HashSet<long> under = scene.SubtreeOf(pumps.FileId);
            List<string> ids = scene.Docs.Where(d => under.Contains(d.FileId) && d.IsScript("HiddenHarbours.Economy.FuelPump"))
                .Select(d => d.Field("_id")).ToList();
            if (ids.Count != NineMileCreekStation.WharfPumpGrades.Length || ids.Any(string.IsNullOrEmpty))
                throw new Refusal($"{step}: '{NineMileCreekStation.WharfRootName}' holds {ids.Count} pumps; the step stands " +
                                  $"down {NineMileCreekStation.WharfPumpGrades.Length}, each on its id.");
            patch.Edit(pumps, Set(pumps.Text, "m_IsActive", "0"), $"{NineMileCreekStation.WharfRootName} stood down");
            patch.Note(NineMileCreekStation.WharfRootName, "over the dredged basin", $"{ids.Count} pumps inactive, ids kept: {string.Join(", ", ids)}");
            return patch.Seal(scene);
        }

        /// <summary>Row 6: the wall boats lie at their berths on the G3 face. Shared with the berth refresh
        /// menu, so neither undoes the other.</summary>
        public static LayerPatch WallBerths(SceneYaml scene, string step, IWallBerths berths)
        {
            Doc root = scene.RootNamed(NineMileCreekMooredFleet.RootName);
            Doc rootTr = scene.TransformOf(root);
            if (rootTr.Field("m_LocalPosition") != "{x: 0, y: 0, z: 0}" ||
                rootTr.Field("m_LocalRotation") != "{x: 0, y: 0, z: 0, w: 1}" ||
                rootTr.Field("m_LocalScale") != "{x: 1, y: 1, z: 1}")
                throw new Refusal("The fleet root must retain its builder transform.");
            var patch = new LayerPatch(step, NineMileCreekMooredFleet.RootName, root.FileId);
            var mine = scene.SubtreeOf(root.FileId);
            var found = new HashSet<string>(StringComparer.Ordinal);
            var expected = new HashSet<string>(berths.WallOwnerIds, StringComparer.Ordinal);
            foreach (Doc boat in scene.Docs.Where(d => mine.Contains(d.FileId) && d.IsScript("HiddenHarbours.Boats.MooredBoat")))
            {
                if (!berths.TryWallBerth(ObjRef.Parse(boat.Field("_owner")).Guid, out string id, out Vector2 at)) continue;
                if (!expected.Contains(id) || !found.Add(id)) throw new Refusal("Unknown or repeated wall owner: " + id);
                Doc go = scene.Require(boat.FieldRef("m_GameObject"), id);
                Doc tr = scene.TransformOf(go);
                if (tr.FieldRef("m_Father") != rootTr.FileId) throw new Refusal(id + " is not a direct fleet child.");
                patch.Edit(tr, MovedTo(tr, at), NineMileCreekMooredFleet.RootName + "/" + id);
            }
            if (!found.SetEquals(expected)) throw new Refusal("The serialized wall fleet differs from its owner register.");
            return patch.Seal(scene);
        }

        // =====================================================================================================
        //  06: the essential roads
        // =====================================================================================================

        /// <summary>Rows 7 and 8: both road layers repainted with what kit v3 keeps of the G3 paving (the wharf's
        /// stub of Wharf Road, the turning pad, the wharf's and the station's pads). Re-lined by H4: kit v4's road
        /// floor draws every other road, and step 12 lays the bridges, so this step no longer places a deck.</summary>
        static LayerPatch Roads(SceneYaml scene, Inputs inputs)
        {
            const string step = RoadsStep;
            Doc root = scene.RootNamed(NineMileCreekRoadPainter.RootName);
            Doc rootTr = scene.TransformOf(root);
            RequireIdentity(rootTr, step);
            var patch = new LayerPatch(step, NineMileCreekRoadPainter.RootName, root.FileId);

            NineMileCreekRoads.Paving paving = G.PaveKeptV3(inputs.Ground);
            if (paving.Cells.Count == 0) throw new Refusal($"{step}: the G3 paving laid no cells.");
            var tops = new List<KeyValuePair<Vector2Int, RoadTile>>(paving.Cells.Count);
            var skirts = new List<KeyValuePair<Vector2Int, RoadTile>>(paving.Cells.Count);
            foreach (var cell in paving.Cells)
            {
                int index = NineMileCreekRoads.AtlasIndexAt(paving, cell.Cell.x, cell.Cell.y);
                tops.Add(new KeyValuePair<Vector2Int, RoadTile>(cell.Cell, inputs.Tile(RoadTileLibrary.TopTileName(cell.Surface, index))));
                skirts.Add(new KeyValuePair<Vector2Int, RoadTile>(cell.Cell, inputs.Tile(RoadTileLibrary.SkirtTileName(cell.Surface, index))));
            }
            Layer(scene, patch, rootTr, RoadKitV3.TopLayerName, tops);
            Layer(scene, patch, rootTr, RoadKitV3.SkirtLayerName, skirts);

            patch.Note(NineMileCreekRoadPainter.RootName, "G3 paving kept in kit v3",
                $"{paving.Cells.Count} cells ({string.Join(", ", paving.PerSurface.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => s.Key + " " + s.Value))}); " +
                $"{paving.Trimmed} trimmed by the water");
            return patch.Seal(scene);
        }

        static void Layer(SceneYaml scene, LayerPatch patch, Doc rootTr, string name,
                          List<KeyValuePair<Vector2Int, RoadTile>> cells)
        {
            Doc go = RequireChild(scene, rootTr, name, patch.Step);
            RequireIdentity(scene.TransformOf(go), patch.Step);
            Doc map = One(scene.ComponentsOf(go).Where(d => d.ClassId == TilemapClass), patch.Step, name + " tilemap");
            patch.Edit(map, Tilemap(map.Text, cells), name + "/Tilemap");
        }

        /// <summary>
        /// A tilemap document holding exactly <paramref name="cells"/>, written as the editor writes a map one
        /// SetTiles call painted: the cells in (y, x) order, the tile and sprite arrays in first-use order with
        /// their reference counts, one identity matrix and one white colour, and the bounds tight on the cells.
        /// Everything before m_Tiles and after the colour array is the document's own.
        /// </summary>
        public static string Tilemap(string text, IReadOnlyList<KeyValuePair<Vector2Int, RoadTile>> cells)
        {
            string[] lines = text.Split('\n');
            int head = Array.FindIndex(lines, l => l == "  m_Tiles:" || l == "  m_Tiles: {}");
            int tail = Array.FindIndex(lines, l => l.StartsWith("  m_TileObjectToInstantiateArray:", StringComparison.Ordinal));
            if (head < 0 || tail < head) throw new Refusal("A road layer is not a serialized Tilemap.");
            var ordered = cells.OrderBy(c => c.Key.y).ThenBy(c => c.Key.x).ToList();
            for (int i = 1; i < ordered.Count; i++)
                if (ordered[i].Key == ordered[i - 1].Key) throw new Refusal($"Two tiles at {ordered[i].Key}.");
            var tileIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            var spriteIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            var tileRefs = new List<string>(); var tileCounts = new List<int>();
            var spriteRefs = new List<string>(); var spriteCounts = new List<int>();
            var sb = new StringBuilder();
            for (int i = 0; i < head; i++) sb.Append(lines[i]).Append('\n');
            if (ordered.Count == 0) sb.Append("  m_Tiles: {}\n");
            else sb.Append("  m_Tiles:\n");
            foreach (var cell in ordered)
            {
                int t = IndexOf(tileIndex, tileRefs, tileCounts, cell.Value.Asset.Yaml);
                int s = IndexOf(spriteIndex, spriteRefs, spriteCounts, cell.Value.Sprite.Yaml);
                sb.Append("  - first: {x: ").Append(cell.Key.x.ToString(CultureInfo.InvariantCulture))
                  .Append(", y: ").Append(cell.Key.y.ToString(CultureInfo.InvariantCulture)).Append(", z: 0}\n")
                  .Append("    second:\n      serializedVersion: 2\n")
                  .Append("      m_TileIndex: ").Append(t.ToString(CultureInfo.InvariantCulture)).Append('\n')
                  .Append("      m_TileSpriteIndex: ").Append(s.ToString(CultureInfo.InvariantCulture)).Append('\n')
                  .Append("      m_TileMatrixIndex: 0\n      m_TileColorIndex: 0\n")
                  .Append("      m_TileObjectToInstantiateIndex: 65535\n      dummyAlignment: 0\n")
                  .Append("      m_AllTileFlags: ").Append(cell.Value.Flags.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            sb.Append("  m_AnimatedTiles: {}\n");
            Counted(sb, "m_TileAssetArray", tileRefs, tileCounts);
            Counted(sb, "m_TileSpriteArray", spriteRefs, spriteCounts);
            string n = ordered.Count.ToString(CultureInfo.InvariantCulture);
            if (ordered.Count == 0) sb.Append("  m_TileMatrixArray: []\n  m_TileColorArray: []\n");
            else
            {
                sb.Append("  m_TileMatrixArray:\n  - serializedVersion: 2\n    m_RefCount: ").Append(n).Append("\n    m_Data:\n");
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                        sb.Append("      e").Append(r).Append(c).Append(": ").Append(r == c ? '1' : '0').Append('\n');
                sb.Append("  m_TileColorArray:\n  - serializedVersion: 2\n    m_RefCount: ").Append(n)
                  .Append("\n    m_Data: {r: 1, g: 1, b: 1, a: 1}\n");
            }
            int minX = 0, minY = 0, maxX = -1, maxY = -1;
            if (ordered.Count > 0)
            {
                minX = ordered.Min(c => c.Key.x); maxX = ordered.Max(c => c.Key.x);
                minY = ordered[0].Key.y; maxY = ordered[ordered.Count - 1].Key.y;
            }
            for (int i = tail; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("  m_Origin: ", StringComparison.Ordinal))
                    line = $"  m_Origin: {{x: {minX}, y: {minY}, z: 0}}";
                else if (line.StartsWith("  m_Size: ", StringComparison.Ordinal))
                    line = $"  m_Size: {{x: {maxX - minX + 1}, y: {maxY - minY + 1}, z: 1}}";
                sb.Append(line);
                if (i < lines.Length - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        static int IndexOf(Dictionary<string, int> index, List<string> refs, List<int> counts, string key)
        {
            if (!index.TryGetValue(key, out int i))
            {
                i = refs.Count;
                index.Add(key, i);
                refs.Add(key);
                counts.Add(0);
            }
            counts[i]++;
            return i;
        }

        static void Counted(StringBuilder sb, string key, List<string> refs, List<int> counts)
        {
            if (refs.Count == 0) { sb.Append("  ").Append(key).Append(": []\n"); return; }
            sb.Append("  ").Append(key).Append(":\n");
            for (int i = 0; i < refs.Count; i++)
                sb.Append("  - serializedVersion: 2\n    m_RefCount: ").Append(counts[i].ToString(CultureInfo.InvariantCulture))
                  .Append("\n    m_Data: ").Append(refs[i]).Append('\n');
        }

        /// <summary>The cells a serialized tilemap holds, with their tiles, read back from its arrays.</summary>
        public static List<KeyValuePair<Vector2Int, RoadTile>> TilesOf(string text, IReadOnlyDictionary<string, int> flagsByAsset = null)
        {
            string[] lines = text.Split('\n');
            List<string> Array(string key)
            {
                var list = new List<string>();
                int at = System.Array.IndexOf(lines, "  " + key + ":");
                if (at < 0) return list;
                for (int i = at + 1; i + 2 < lines.Length && lines[i] == "  - serializedVersion: 2"; i += 3)
                    list.Add(lines[i + 2].Substring("    m_Data: ".Length));
                return list;
            }
            List<string> assets = Array("m_TileAssetArray"), sprites = Array("m_TileSpriteArray");
            var cells = new List<KeyValuePair<Vector2Int, RoadTile>>();
            var first = new Regex(@"^  - first: \{x: (-?\d+), y: (-?\d+), z: 0\}$");
            for (int i = 0; i < lines.Length; i++)
            {
                Match m = first.Match(lines[i]);
                if (!m.Success) continue;
                int t = ParseInt(lines[i + 3].Substring("      m_TileIndex: ".Length));
                int s = ParseInt(lines[i + 4].Substring("      m_TileSpriteIndex: ".Length));
                int flags = ParseInt(lines[i + 9].Substring("      m_AllTileFlags: ".Length));
                cells.Add(new KeyValuePair<Vector2Int, RoadTile>(
                    new Vector2Int(ParseInt(m.Groups[1].Value), ParseInt(m.Groups[2].Value)),
                    new RoadTile(ObjRef.Parse(assets[t]), ObjRef.Parse(sprites[s]), flags)));
            }
            return cells;
        }

        // =====================================================================================================
        //  07: the loop's wall contacts
        // =====================================================================================================

        /// <summary>Row 9: the eight selected chunks' obsolete face inside the ramp opening stops drawing. Their
        /// ids, definitions and material GUIDs stay; every other face is the later H3 decoration. A chunk the scene
        /// no longer holds is passed over only where its Def is retired: 11 deleted it, and the Def keeps the id.</summary>
        static LayerPatch Cliffs(SceneYaml scene, Inputs inputs)
        {
            Doc root = scene.RootNamed(NineMileCreekCliffWalls.RootName);
            Doc rootTr = scene.TransformOf(root);
            var patch = new LayerPatch(CliffStep, NineMileCreekCliffWalls.RootName, root.FileId);
            int retired = 0;
            foreach (string name in Pr2CliffChunks)
            {
                Doc go = Child(scene, rootTr, name, CliffStep);
                if (go == null && IsRetired(inputs, name)) { retired++; continue; }
                go = go ?? RequireChild(scene, rootTr, name, CliffStep);
                patch.Edit(go, Set(go.Text, "m_IsActive", "0"), name);
            }
            patch.Note(NineMileCreekCliffWalls.RootName, "ramp opening", retired == 0
                ? $"{Pr2CliffChunks.Length} chunks inactive, ids kept"
                : $"{Pr2CliffChunks.Length - retired} chunks inactive and {retired} retired by {WallsStep}, ids kept");
            return patch.Seal(scene);
        }

        /// <summary>A chunk whose wall Def (by the last three digits of its name) is retired.</summary>
        static bool IsRetired(Inputs inputs, string chunk)
        {
            string realId = chunk.Substring(Math.Max(0, chunk.Length - 3));
            return inputs.Walls.Any(d => d != null && d.RealId == realId && d.Status == CliffWallStatus.Retired);
        }

        // =====================================================================================================
        //  11: the cliff walls by their real ids on G3
        // =====================================================================================================

        /// <summary>The <c>CliffWalls</c> root as the walls' Defs have it, by St Peters' step under this step's name:
        /// a standing wall edited in place on its kept ids, a split's new wall copied from its parent under named new
        /// ids, a retired wall's four documents deleted and named. Cut at the span the walls were cut at.</summary>
        static LayerPatch Walls(SceneYaml scene, Inputs inputs) =>
            CliffWalls(scene, inputs.Walls, NineMileCreekCliffWalls.ChunkToeSpanMetres, WallsStep);

        // =====================================================================================================
        //  08: the loop's contacts
        // =====================================================================================================

        /// <summary>Row 12: a marker to its G3 place, ids kept.</summary>
        static LayerPatch Marker(SceneYaml scene, string name, Vector2 at, Quaternion? rotation)
        {
            Doc go = scene.RootNamed(name);
            Doc tr = scene.TransformOf(go);
            var patch = new LayerPatch(ContactsStep, name, go.FileId);
            string text = MovedTo(tr, at);
            if (rotation != null)
            {
                Quaternion q = rotation.Value;
                text = Set(text, "m_LocalRotation", $"{{x: {N(q.x)}, y: {N(q.y)}, z: {N(q.z)}, w: {N(q.w)}}}");
                // A compass heading turns clockwise; the hint is Unity's counter-clockwise z.
                text = Set(text, "m_LocalEulerAnglesHint", $"{{x: 0, y: 0, z: {N(-G.BerthHeadingDegrees)}}}");
            }
            patch.Edit(tr, text, name + "/Transform");
            return patch.Seal(scene);
        }

        /// <summary>Row 12: the region's arrival is an opt-in alongside berth laid for the design hull.</summary>
        static LayerPatch Anchor(SceneYaml scene)
        {
            Doc go = scene.RootNamed(AnchorName);
            var patch = new LayerPatch(ContactsStep, AnchorName, go.FileId);
            Doc anchor = One(scene.ComponentsOf(go).Where(d => d.IsScript("HiddenHarbours.App.RegionAnchor")), ContactsStep, AnchorName);
            string text = anchor.Text;
            string beam = N(G.DesignHalfBeamMetres);
            if (anchor.Field("_alongsideBerth") == null)
                text = InsertAfter(text, "_worldSizeMeters", "  _alongsideBerth: 1", "  _alongsideHalfBeamMetres: " + beam);
            else
                text = Set(Set(text, "_alongsideBerth", "1"), "_alongsideHalfBeamMetres", beam);
            patch.Edit(anchor, text, AnchorName + "/alongside berth");
            return patch.Seal(scene);
        }

        /// <summary>Row 14, the owner's ruling (2026-10-15): the Otter, parked safe for now. G3 has no ramp to
        /// launch her from, so she keeps her spot and turns inland; ids, name and components kept, and she stays
        /// active and drivable.</summary>
        static LayerPatch Otter(SceneYaml scene)
        {
            Doc go = scene.RootNamed(NineMileCreekOtterLanding.OtterName);
            Doc tr = scene.TransformOf(go);
            var patch = new LayerPatch(ContactsStep, NineMileCreekOtterLanding.OtterName, go.FileId);
            Quaternion q = G.OtterParkRotation;
            string text = MovedTo(tr, G.OtterParkPos);
            text = Set(text, "m_LocalRotation", $"{{x: {N(q.x)}, y: {N(q.y)}, z: {N(q.z)}, w: {N(q.w)}}}");
            text = Set(text, "m_LocalEulerAnglesHint", $"{{x: 0, y: 0, z: {N(G.OtterParkHeadingDegrees)}}}");
            patch.Edit(tr, text, NineMileCreekOtterLanding.OtterName + "/Transform");
            return patch.Seal(scene);
        }

        // =====================================================================================================
        //  09: the nav marks
        // =====================================================================================================

        /// <summary>
        /// The marks row (the owner's ruling): the marks follow the G3 mouth. The plan is the builder's own on
        /// G3 (<see cref="NineMileCreekNavMarks.Plan"/>). A mark it keeps moves and turns on its id; a mark it no
        /// longer asks for stands down, inactive on its id and never deleted; a new one is copied from a mark of
        /// its own def and size rung. A turn is the facing and the sprite <c>NavBuoyVisual.Apply</c> dresses that
        /// facing in: these marks carry no mooring, and their float line comes from the def and rung alone, so
        /// nothing else follows it. A plan that would change a kept mark's def or rung is refused, for the placer.
        /// </summary>
        static LayerPatch Marks(SceneYaml scene, Inputs inputs)
        {
            const string step = MarksStep;
            NavBuoyPrefab prefab = inputs.NavPrefab;
            NavMarkPlanResult plan = NineMileCreekNavMarks.Plan(inputs.Ground);
            Doc root = scene.RootNamed(NineMileCreekNavMarks.RootName);
            Doc rootTr = scene.TransformOf(root);
            RequireIdentity(rootTr, step);
            var patch = new LayerPatch(step, NineMileCreekNavMarks.RootName, root.FileId);
            var ids = new IdAllocator(scene);

            List<PlacedMark> placed = ReadMarks(scene, rootTr, prefab);
            var byName = new Dictionary<string, PlacedMark>(StringComparer.Ordinal);
            foreach (PlacedMark m in placed)
                if (!byName.ContainsKey(m.Name)) byName.Add(m.Name, m);
                else throw new Refusal($"{step}: two marks are named '{m.Name}'.");

            var planned = new HashSet<string>(StringComparer.Ordinal);
            List<long> children = rootTr.FieldRefs("m_Children");
            foreach (PlannedNavMark mark in plan.Marks)
            {
                if (NineMileCreekNavMarks.RetiredIds.Contains(mark.Id))
                    throw new Refusal($"{step}: the plan names {mark.Id}, a retired id no mark takes again.");
                NavMarkDef def = inputs.NavDef(mark.MarkType);
                string name = NavMarkNamePrefix + mark.Id;
                if (!planned.Add(name)) throw new Refusal($"{step}: the plan names {name} twice.");
                if (byName.TryGetValue(name, out PlacedMark hit))
                {
                    if (!hit.Def.SameTarget(def.Asset) || hit.SizeId != mark.SizeId)
                        throw new Refusal($"{step}: the plan dresses {name} as {mark.MarkType} {mark.SizeId}, and the scene's wears " +
                                          $"{hit.Def.Yaml} {hit.SizeId}: re-dress it with the placer.");
                    patch.Edit(hit.Pi, Placed(hit.Pi.Text, prefab, name, mark, def), $"{name} placed");
                    continue;
                }
                PlacedMark template = placed.FirstOrDefault(m => m.Def.SameTarget(def.Asset) && m.SizeId == mark.SizeId)
                    ?? throw new Refusal($"{step}: no {mark.MarkType} {mark.SizeId} mark to copy {name} from; place it with the placer.");
                long pi = ids.Next($"{step}/{mark.Id}/PrefabInstance"), tr = ids.Next($"{step}/{mark.Id}/Transform");
                patch.Add(Placed(Header(template.Pi.Text, pi), prefab, name, mark, def), $"{name} added");
                patch.Add(Set(Header(template.Tr.Text, tr), "m_PrefabInstance", Ref(pi)), $"{name}/Transform (stripped)");
                children.Add(tr);
                patch.Note(name, "added", $"at {Vec2(mark.At)}, copied from {template.Name}");
            }
            foreach (PlacedMark m in placed)
            {
                if (planned.Contains(m.Name)) continue;
                patch.Edit(m.Pi, Active(m.Pi.Text, prefab, false), $"{m.Name} retired");
                patch.Note(m.Name, "retired", "inactive, id kept");
            }
            patch.Edit(rootTr, SetList(rootTr.Text, "m_Children", children), NineMileCreekNavMarks.RootName + "/children");
            patch.Note(NineMileCreekNavMarks.RootName, "marks on G3", $"{plan.Marks.Count} planned, {plan.Refusals.Count} refused");
            return patch.Seal(scene);
        }

        /// <summary>One mark in the scene: its instance and stripped transform, and the name, def and rung it
        /// loads with.</summary>
        sealed class PlacedMark
        {
            public Doc Pi, Tr;
            public string Name, SizeId;
            public ObjRef Def;
        }

        static List<PlacedMark> ReadMarks(SceneYaml scene, Doc rootTr, NavBuoyPrefab prefab)
        {
            const string step = MarksStep;
            var marks = new List<PlacedMark>();
            foreach (long id in rootTr.FieldRefs("m_Children"))
            {
                Doc tr = scene.Require(id, "a nav mark's transform");
                Doc pi = tr.Stripped ? scene.Require(tr.FieldRef("m_PrefabInstance"), "a nav mark's prefab instance") : null;
                if (pi == null || pi.ClassId != PrefabInstanceClass || ObjRef.Parse(pi.Field("m_SourcePrefab")).Guid != prefab.Guid ||
                    ParseRef(InstanceField(pi, "m_TransformParent")) != rootTr.FileId ||
                    ParseRef(tr.Field("m_CorrespondingSourceObject")) != prefab.Transform)
                    throw new Refusal($"{step}: &{id} under {NineMileCreekNavMarks.RootName} is not a nav buoy instance hung from the root.");
                foreach (string list in new[] { "m_RemovedComponents", "m_RemovedGameObjects", "m_AddedGameObjects", "m_AddedComponents" })
                    if (InstanceField(pi, list) != "[]")
                        throw new Refusal($"{step}: &{pi.FileId} carries {list}; the step will not touch hand work.");
                if (scene.Docs.Any(d => d.Stripped && d.FileId != tr.FileId && d.FieldRef("m_PrefabInstance") == pi.FileId))
                    throw new Refusal($"{step}: &{pi.FileId} carries components added after the prefab (a mooring), whose anchor " +
                                      "a move would leave behind: re-place it with the placer.");
                List<Record> records = Records(pi.Lines);
                string Loaded(long target, string path, string prefabValue) =>
                    records.LastOrDefault(x => x.Target == target && x.Path == path) is Record r
                        ? (r.Reference != "{fileID: 0}" ? r.Reference : r.Value) : prefabValue;
                marks.Add(new PlacedMark
                {
                    Pi = pi, Tr = tr,
                    Name = Loaded(prefab.GameObject, "m_Name", null) ?? throw new Refusal($"{step}: &{pi.FileId} is not named."),
                    Def = ObjRef.Parse(Loaded(prefab.Visual, "_def", prefab.Def.Yaml)),
                    SizeId = Loaded(prefab.Visual, "_sizeId", prefab.SizeId),
                });
            }
            return marks;
        }

        /// <summary>A mark's instance re-placed: its name, the planned spot, and the facing with the sprite Apply
        /// dresses that facing in; active. Every one of these is a record the placer wrote: the step re-places what
        /// it recorded and never invents one.</summary>
        static string Placed(string text, NavBuoyPrefab prefab, string name, PlannedNavMark mark, NavMarkDef def)
        {
            int facing = Mathf.Clamp(mark.Facing, 0, 7);
            text = SetRecord(text, prefab.GameObject, "m_Name", name, null);
            text = SetRecord(text, prefab.Transform, "m_LocalPosition.x", N(mark.At.x), null);
            text = SetRecord(text, prefab.Transform, "m_LocalPosition.y", N(mark.At.y), null);
            text = SetRecord(text, prefab.Visual, "_facing", facing.ToString(CultureInfo.InvariantCulture), null);
            text = SetRecord(text, prefab.Renderer, "m_Sprite", null, def.Sprite(mark.SizeId, facing).Yaml);
            return Active(text, prefab, true);
        }

        /// <summary>A mark's instance active or stood down. Standing down adds the one record the placer never
        /// wrote, after the root's own records, as the editor keeps an instance's records in target order.</summary>
        static string Active(string text, NavBuoyPrefab prefab, bool active)
        {
            string value = active ? "1" : "0";
            List<string> lines = text.Split('\n').ToList();
            List<Record> records = Records(lines);
            Record flag = records.LastOrDefault(r => r.Target == prefab.GameObject && r.Path == "m_IsActive");
            if (flag != null) return SetRecord(text, prefab.GameObject, "m_IsActive", value, null);
            if (active) return text;
            Record last = records.LastOrDefault(r => r.Target == prefab.GameObject)
                          ?? throw new Refusal($"{MarksStep}: a mark with no record on its root.");
            lines.InsertRange(last.Line + 4, new[]
            {
                $"    - target: {{fileID: {prefab.GameObject.ToString(CultureInfo.InvariantCulture)}, guid: {prefab.Guid}, type: 3}}",
                "      propertyPath: m_IsActive",
                "      value: 0",
                "      objectReference: {fileID: 0}",
            });
            return string.Join("\n", lines);
        }

        /// <summary>One record under an instance's <c>m_Modifications</c>: four lines from <see cref="Line"/>.</summary>
        sealed class Record
        {
            public long Target;
            public string Path, Value, Reference;
            public int Line;
        }

        static List<Record> Records(IList<string> lines)
        {
            int head = lines.IndexOf("    m_Modifications:");
            if (head < 0) throw new Refusal($"{MarksStep}: a nav mark instance records nothing.");
            var records = new List<Record>();
            for (int i = head + 1; i < lines.Count && lines[i].StartsWith("    - target: ", StringComparison.Ordinal); i += 4)
            {
                if (i + 3 >= lines.Count || !lines[i + 1].StartsWith("      propertyPath: ", StringComparison.Ordinal) ||
                    !lines[i + 2].StartsWith("      value:", StringComparison.Ordinal) ||
                    !lines[i + 3].StartsWith("      objectReference: ", StringComparison.Ordinal))
                    throw new Refusal($"{MarksStep}: a record that is not four lines, at line {i + 1} of an instance.");
                records.Add(new Record
                {
                    Target = ParseRef(lines[i]),
                    Path = lines[i + 1].Substring("      propertyPath: ".Length),
                    Value = lines[i + 2].Length > "      value: ".Length ? lines[i + 2].Substring("      value: ".Length) : "",
                    Reference = lines[i + 3].Substring("      objectReference: ".Length),
                    Line = i,
                });
            }
            return records;
        }

        /// <summary>One record's value or reference rewritten. The record must be there.</summary>
        static string SetRecord(string text, long target, string path, string value, string reference)
        {
            string[] lines = text.Split('\n');
            Record r = Records(lines).LastOrDefault(x => x.Target == target && x.Path == path)
                       ?? throw new Refusal($"{MarksStep}: a mark records no {path}; re-place it with the placer.");
            if (value != null) lines[r.Line + 2] = "      value: " + value;
            if (reference != null) lines[r.Line + 3] = "      objectReference: " + reference;
            return string.Join("\n", lines);
        }

        /// <summary>A prefab instance's own field under <c>m_Modification</c> (four-space indent).</summary>
        static string InstanceField(Doc pi, string key)
        {
            string prefix = "    " + key + ": ";
            foreach (string line in pi.Lines)
                if (line.StartsWith(prefix, StringComparison.Ordinal)) return line.Substring(prefix.Length);
            return null;
        }

        // =====================================================================================================
        //  YAML helpers (the N1 helpers' discipline: exact fields, refusals, never a guess)
        // =====================================================================================================

        static Doc One(IEnumerable<Doc> docs, string step, string what)
        {
            var list = docs.ToList();
            if (list.Count != 1) throw new Refusal($"{step}: {list.Count} documents where {what} should be one.");
            return list[0];
        }

        static List<Doc> Children(SceneYaml scene, Doc parentTr) =>
            parentTr.FieldRefs("m_Children")
                .Select(id => scene.Require(scene.Require(id, "child transform").FieldRef("m_GameObject"), "child"))
                .ToList();

        static Doc Child(SceneYaml scene, Doc parentTr, string name, string step)
        {
            var named = Children(scene, parentTr).Where(go => go.Field("m_Name") == name).ToList();
            if (named.Count > 1) throw new Refusal($"{step}: two children named '{name}'.");
            return named.Count == 0 ? null : named[0];
        }

        static Doc RequireChild(SceneYaml scene, Doc parentTr, string name, string step) =>
            Child(scene, parentTr, name, step) ?? throw new Refusal($"{step}: no child named '{name}'.");

        static void RequireIdentity(Doc tr, string step)
        {
            if (tr.Field("m_LocalPosition") != "{x: 0, y: 0, z: 0}" ||
                tr.Field("m_LocalRotation") != "{x: 0, y: 0, z: 0, w: 1}" ||
                tr.Field("m_LocalScale") != "{x: 1, y: 1, z: 1}")
                throw new Refusal($"{step}: parent &{tr.FileId} is transformed; the builder places in world coordinates.");
        }

        static void Put(LayerPatch patch, Doc before, string after, string name)
        {
            if (before == null) patch.Add(after, name); else patch.Edit(before, after, name);
        }

        static Vector3 Position(Doc tr) => ParseVec3(tr.Field("m_LocalPosition"));

        static string MovedTo(Doc tr, Vector2 at) =>
            Set(tr.Text, "m_LocalPosition", $"{{x: {N(at.x)}, y: {N(at.y)}, z: {N(Position(tr).z)}}}");

        static string NamedGameObject(string template, long id, string name, params long[] components) =>
            SetList(Set(Header(template, id), "m_Name", name), "m_Component", components, "component: ");

        static string ChildTransform(string template, long id, long go, Vector2 at, long father)
        {
            string text = Set(Header(template, id), "m_GameObject", Ref(go));
            text = Set(text, "m_LocalPosition", $"{{x: {N(at.x)}, y: {N(at.y)}, z: 0}}");
            text = SetList(text, "m_Children", Array.Empty<long>());
            return Set(text, "m_Father", Ref(father));
        }

        static string Vec2(Vector2 v) => $"{{x: {N(v.x)}, y: {N(v.y)}}}";

        /// <summary>
        /// A float as the editor's YAML writer spells it: the fewest significant digits that read back to the
        /// same float, in plain decimal. <see cref="StPetersLayerRefresh.F"/> stops at the first G-precision
        /// that round-trips, and .NET writes that precision in E notation whenever the exponent reaches it, so
        /// it answers "5E+01" for 50 and "1.8E+02" for 180: values this step writes, and the editor never would.
        /// </summary>
        public static string N(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) throw new Refusal($"a non-finite number ({v}).");
            if (v == 0f) return BitConverter.GetBytes(v)[3] >= 0x80 ? "-0" : "0";
            if (Math.Abs(v) < 1e-4f) throw new Refusal($"{v.ToString("R", CultureInfo.InvariantCulture)} is below the plain spelling this step writes.");
            for (int digits = 1; digits <= 9; digits++)
            {
                string s = v.ToString("G" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                if (ParseFloat(s) == v) return Plain(s);
            }
            throw new Refusal($"no spelling of {v.ToString("R", CultureInfo.InvariantCulture)} reads back.");
        }

        /// <summary>A G-format number with its E notation, if any, written out in plain decimal.</summary>
        static string Plain(string s)
        {
            int e = s.IndexOf('E');
            if (e < 0) return s;
            int exp = int.Parse(s.Substring(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            string mantissa = s.Substring(0, e);
            bool negative = mantissa.StartsWith("-", StringComparison.Ordinal);
            if (negative) mantissa = mantissa.Substring(1);
            int dot = mantissa.IndexOf('.');
            string digits = dot < 0 ? mantissa : mantissa.Remove(dot, 1);
            int point = (dot < 0 ? mantissa.Length : dot) + exp;
            string plain = point <= 0 ? "0." + new string('0', -point) + digits
                : point >= digits.Length ? digits + new string('0', point - digits.Length)
                : digits.Substring(0, point) + "." + digits.Substring(point);
            return negative ? "-" + plain : plain;
        }
        static string Ref(long id) => "{fileID: " + id.ToString(CultureInfo.InvariantCulture) + "}";
        static string Header(string text, long id) =>
            Regex.Replace(text, @"^--- !u!(\d+) &\d+", m => "--- !u!" + m.Groups[1].Value + " &" + id.ToString(CultureInfo.InvariantCulture));

        /// <summary>Replace a two-space field's one-line value. The field must appear exactly once.</summary>
        static string Set(string text, string key, string value)
        {
            var rx = new Regex(@"(?m)^  " + Regex.Escape(key) + @":(?: .*)?$");
            int count = rx.Matches(text).Count;
            if (count != 1) throw new Refusal($"{count} '{key}' fields where one is written.");
            return rx.Replace(text, _ => "  " + key + ": " + value);
        }

        /// <summary>Replace the body of a two-space block field: the lines under it indented four, or listed at
        /// two ("  - ").</summary>
        static string SetBlock(string text, string key, IEnumerable<string> body)
        {
            string[] lines = text.Split('\n');
            int at = -1;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i] == "  " + key + ":")
                {
                    if (at >= 0) throw new Refusal($"two '{key}' blocks.");
                    at = i;
                }
            if (at < 0) throw new Refusal($"no '{key}' block.");
            int end = at + 1;
            while (end < lines.Length && (lines[end].StartsWith("    ", StringComparison.Ordinal) ||
                                          lines[end].StartsWith("  - ", StringComparison.Ordinal))) end++;
            return string.Join("\n", lines.Take(at + 1).Concat(body).Concat(lines.Skip(end)));
        }

        /// <summary>Write a list of local references (<c>m_Children</c>, <c>m_Component</c>).</summary>
        static string SetList(string text, string key, IEnumerable<long> ids, string prefix = "")
        {
            var entries = ids.Select(id => "  - " + prefix + Ref(id)).ToArray();
            string value = entries.Length == 0 ? "  " + key + ": []" : "  " + key + ":\n" + string.Join("\n", entries);
            var rx = new Regex(@"(?m)^  " + Regex.Escape(key) + @":(?: \[\])?(?:\n  - [^\n]*)*");
            if (rx.Matches(text).Count != 1) throw new Refusal($"no single '{key}' list.");
            return rx.Replace(text, _ => value);
        }

        /// <summary>The list with <paramref name="id"/> appended unless it already holds it.</summary>
        static string WithListed(string text, string key, long id, string prefix = "")
        {
            var doc = new Doc(text);
            List<long> ids = key == "m_Component" ? ComponentIds(doc) : doc.FieldRefs(key);
            if (!ids.Contains(id)) ids.Add(id);
            return SetList(text, key, ids, prefix);
        }

        static List<long> ComponentIds(Doc go) =>
            go.Lines.Where(l => l.StartsWith("  - component: ", StringComparison.Ordinal))
              .Select(l => ParseRef(l.Substring("  - component: ".Length))).ToList();

        static string InsertAfter(string text, string key, params string[] lines)
        {
            string[] all = text.Split('\n');
            int at = Array.FindIndex(all, l => l.StartsWith("  " + key + ":", StringComparison.Ordinal));
            if (at < 0 || at + 1 < all.Length && all[at + 1].StartsWith("    ", StringComparison.Ordinal))
                throw new Refusal($"no one-line '{key}' to follow.");
            return string.Join("\n", all.Take(at + 1).Concat(lines).Concat(all.Skip(at + 1)));
        }
    }
}
#endif
