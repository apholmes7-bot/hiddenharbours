#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// NMC H9, one gas bar: Route 91's forecourt on the fuel stop kit (pass 6). One patch rooted at <c>CreekFuel</c>
    /// (Decision 1, lean a): the old forecourt <c>Route91Station</c> is retired by name with every document under it, and
    /// the new one, <see cref="ForecourtName"/>, takes its place in <c>CreekFuel</c>'s children. <c>WharfPumps</c> is
    /// never touched. The forecourt holds the fuel stop's 28 kit rows and two gull perches at their rows' places, and
    /// one <see cref="Economy.FuelPump"/> per drawn nozzle that sells a grade the station sells, on the id map
    /// (Decision 2: the four old ids carried over by machine, side and grade; the rest new, none reused).
    /// The eleven wharf rows that waited for this kit are placed by <see cref="PlaceRows"/> under the wharf's own root.
    /// </summary>
    public static partial class NineMileCreekWharfLandSide
    {
        public const string FuelStopStep = "19-fuel-stop";
        public const string ForecourtName = "Route91FuelStop";
        public const string FuelStopKeyScenePath = "Assets/_Project/Data/KeyScenes/KeyScene_nmc_fuel_stop.asset";
        public const string RoadStationPath = "Assets/_Project/Data/FuelStations/Route91Station.asset";
        const string FuelStopKitPrefix = "fuelStopRig";

        /// <summary>
        /// The id map (Decision 2, `Evidence~/nmc-h9/id-map.md`): the drawn nozzle (row, anchor) → the fixture id and the
        /// grade it sells. The canopy's three machines carry three nozzles a side (regular, super, diesel); super sells
        /// mixed and KERO sells stove oil, both grades the station already sells. DEF is drawn only: no pump.
        /// </summary>
        public static readonly IReadOnlyList<(string Row, string Anchor, string Id, string Grade)> FuelPumps = BuildPumpMap();

        static List<(string, string, string, string)> BuildPumpMap()
        {
            var map = new List<(string, string, string, string)>();
            string[] grade = { "gas", "mixed", "diesel" }, kind = { "reg", "sup", "dsl" };
            for (int n = 0; n < 18; n++)
            {
                int machine = n / 6 + 1, onSide = n % 6;
                bool west = onSide < 3;
                string g = grade[onSide % 3];
                map.Add(("station.nmc_canopy", $"nozzle.{kind[onSide % 3]}.{(west ? "W" : "E")}.{n}",
                    $"fixture.route_91.pump{machine}{(west ? "a" : "b")}_{g}", g));
            }
            map.Add(("station.nmc_canopy", "nozzle.kero", "fixture.route_91.kero_stove_oil", "stove_oil"));
            map.Add(("station.nmc_semi_lane", "nozzle.dsl.W.0", "fixture.route_91.semi_a1_diesel", "diesel"));
            map.Add(("station.nmc_semi_lane", "nozzle.dsl.W.1", "fixture.route_91.semi_a2_diesel", "diesel"));
            map.Add(("station.nmc_semi_lane", "nozzle.dsl.E.2", "fixture.route_91.semi_b1_diesel", "diesel"));
            map.Add(("station.nmc_semi_lane", "nozzle.dsl.E.3", "fixture.route_91.semi_b2_diesel", "diesel"));
            map.Add(("station.nmc_semi_lane", "nozzle.satellite", "fixture.route_91.semi_satellite_diesel", "diesel"));
            return map;
        }

        /// <summary>
        /// <b>The ground the stop stands on</b>: one rectangle, from the rows that are placed. West to east it spans
        /// the lot's own lights (the kit lights the lot it paves); south it meets Wharf Road's kerb, the road the
        /// stop fronts, read from the road network; north it runs under the store to the store's own ground point.
        /// (Stopping it at the door anchor left grass under the awning, between the walk and the wall: the kit's
        /// bounds are the awning's, and the wall stands behind them.) ⚠ It is the kit v3 PAD the old station had (<see cref="NineMileCreekRoads.Pads"/>),
        /// moved: one concrete surface. The kit's own lot (asphalt, five slab pads, the store walk, stall lines) is
        /// drawn only in the kit's scene source and is in neither the key scene asset nor the road network; laying
        /// that is the road lane's, and this pad is what keeps the meadow from growing between the pumps until then.
        /// </summary>
        public static Rect ForecourtArea()
        {
            if (_forecourtArea.HasValue) return _forecourtArea.Value;
            KeySceneDef scene = FuelStopKeyScene();
            var poles = scene.Pieces.Where(p => OnFuelStopKit(p) && p.Piece == "lampPole").ToList();
            KeyScenePiece store = scene.Pieces.Single(p => p.Id == "station.nmc_store");
            if (poles.Count < 2) throw new Refusal(FuelStopStep + ": the lot has fewer than two lights to span.");
            float half = (Definition("lampPole").BoundsMax.x - Definition("lampPole").BoundsMin.x) * 0.5f;
            float west = poles.Min(p => p.At.x) - half, east = poles.Max(p => p.At.x) + half;
            float north = store.At.y;
            float south = float.NegativeInfinity;
            foreach (var road in NineMileCreekRoadNetwork.Load().All(WharfRoadId))
                for (int i = 1; i < road.Points.Length; i++)
                {
                    Vector2 a = road.Points[i - 1], b = road.Points[i];
                    float lo = Mathf.Min(a.x, b.x), hi = Mathf.Max(a.x, b.x);
                    if (hi < west || lo > east || Mathf.Approximately(a.x, b.x)) continue;
                    foreach (float x in new[] { Mathf.Clamp(west, lo, hi), Mathf.Clamp(east, lo, hi) })
                        south = Mathf.Max(south, Mathf.Lerp(a.y, b.y, (x - a.x) / (b.x - a.x)) + road.HalfWidth);
                }
            if (float.IsNegativeInfinity(south) || south >= north) throw new Refusal(FuelStopStep + ": Wharf Road does not pass south of the stop.");
            _forecourtArea = Rect.MinMaxRect(west, south, east, north);
            return _forecourtArea.Value;
        }
        // Read once per domain: the pad table asks for it per cell.
        static Rect? _forecourtArea;
        const string WharfRoadId = "road.nmc_wharf_road";

        static KeySceneDef FuelStopKeyScene() => AssetDatabase.LoadAssetAtPath<KeySceneDef>(FuelStopKeyScenePath)
            ?? throw new Refusal("The fuel stop key scene is unavailable.");

        static bool OnFuelStopKit(KeyScenePiece row) => row.Kit != null && row.Kit.StartsWith(FuelStopKitPrefix, StringComparison.Ordinal);

        /// <summary>The wharf's rows on the fuel stop kit (Decision 5): H6's ten and H5's one, placed under the wharf's root.</summary>
        public static KeyScenePiece[] FuelStopWharfRows() => KeyScene().Pieces.Where(OnFuelStopKit).ToArray();

        /// <summary>A fuel stop row's Def: its kind's (one Def per kind; the seven shared kinds carry prop ids).</summary>
        static SetPieceDef FuelStopDefinition(KeyScenePiece row)
        {
            SetPieceDef def = Definition(row.Piece);
            if (def.Id != NineMileCreekFuelStopPlan.DefinitionId(row) || def.RigKey != row.Piece || !def.HasArt)
                throw new Refusal(row.Id + ": its fuel stop Def is missing, unsliced or not the ruled kind identity.");
            return def;
        }

        /// <summary>A drawn point of a placed row, in world metres (x, y) with its height over the ground (z).</summary>
        static Vector3 RowAnchor(KeyScenePiece row, string anchor)
        {
            if (!FuelStopDefinition(row).TryGetAnchor(anchor, out Vector3 local)) throw new Refusal(row.Id + " has no " + anchor + " anchor.");
            Vector2 at = row.At + SetPieceDef.ToWorld(new Vector2(local.x, local.y), row.Dir);
            return new Vector3(at.x, at.y, local.z);
        }

        /// <summary>A set piece's picture, sorting, placement record and walls, as <see cref="PlaceRows"/> writes its own.</summary>
        static void SetPieceParts(KeyScenePiece row, SetPieceDef def, Func<string, long> allocate, long pieceGo, ObjRef material,
            List<long> comps, List<(string name, string text)> parts)
        {
            int frame = SetPieceDef.FrameForRigDir(row.Dir);
            SpriteRef sprite = SpriteAsset(def.SpriteFor(frame));
            var walls = WallsOf(def.Colliders, frame, row.Id);
            walls.AddRange(WallsOf(AuthoredWalls(row, def), frame, row.Id));
            long source = allocate("source"), renderer = allocate("sprite"), sort = allocate("sort");
            comps.Add(source); comps.Add(renderer); comps.Add(sort);
            parts.Add((row.Id + "/source", MonoBehaviourYaml(source, pieceGo,
                Script("Assets/_Project/Code/Art/SetPiecePlacement.cs", "HiddenHarbours.Art", "HiddenHarbours.Art.SetPiecePlacement"),
                new[] { "  _def: " + Asset("Assets/_Project/Data/SetPieces/SetPiece_" + row.Piece + ".asset").Yaml,
                    "  _definitionId: " + def.Id, "  _placementId: " + row.Id, "  _rigDir: " + row.Dir })));
            int order = YSortSprite.OrderFor(row.At.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre, SortingBands.DecorFloor, SortingBands.DecorCeiling);
            parts.Add((row.Id + "/sprite", SpriteRendererYaml(renderer, pieceGo, material, sprite, order)));
            parts.Add((row.Id + "/sort", MonoBehaviourYaml(sort, pieceGo,
                Script("Assets/_Project/Code/Art/YSortSprite.cs", "HiddenHarbours.Art", "HiddenHarbours.Art.YSortSprite"),
                new[] { "  _dynamic: 0", "  _baseOrder: " + SortingBands.DecorBase, "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre),
                    "  _minOrder: " + SortingBands.DecorFloor, "  _maxOrder: " + SortingBands.DecorCeiling, "  _pivotYOffset: 0" })));
            if (walls.Count > 0)
            {
                long collider = allocate("walls"); comps.Add(collider);
                parts.Add((row.Id + "/walls", PolygonColliderYaml(collider, pieceGo, walls)));
            }
        }

        /// <summary>
        /// The forecourt patch. Rooted at <c>CreekFuel</c>: deletes <c>Route91Station</c>'s whole subtree, writes
        /// <see cref="ForecourtName"/> whole (its rows, perches and pumps) and lists it where the old forecourt stood in
        /// <c>CreekFuel</c>'s children. A second plan over its own output changes nothing.
        /// </summary>
        public static LayerPatch PlaceForecourt(SceneYaml scene)
        {
            Doc fuel = scene.RootNamed(NineMileCreekStation.RootName);
            Doc fuelTr = scene.TransformOf(fuel);
            Doc ChildNamed(string name) => scene.Docs.SingleOrDefault(d => d.ClassId == 1 && !d.Stripped && d.Field("m_Name") == name &&
                scene.TransformOf(d).FieldRef("m_Father") == fuelTr.FileId);
            Doc old = ChildNamed(NineMileCreekStation.ForecourtRootName), current = ChildNamed(ForecourtName);
            if (scene.Docs.Count(d => d.ClassId == 1 && !d.Stripped && d.Field("m_Name") == NineMileCreekStation.ForecourtRootName) != (old == null ? 0 : 1))
                throw new Refusal(FuelStopStep + ": a Route91Station stands outside CreekFuel.");

            HashSet<long> retiring = old == null ? new HashSet<long>() : scene.SubtreeOf(old.FileId);
            HashSet<long> mine = current == null ? new HashSet<long>() : scene.SubtreeOf(current.FileId);
            var ids = new IdAllocator(scene, mine);
            long Alloc(string key) => ids.Next("nmc-h9/" + ForecourtName + "/" + key);
            long go = Alloc("go"), tr = Alloc("tr");
            if (current != null && current.FileId != go) throw new Refusal(FuelStopStep + ": the forecourt has an unexpected identity.");

            KeySceneDef keyScene = FuelStopKeyScene();
            var material = new EditorKeySceneAssets().SpriteMaterial;
            var ground = NineMileCreekGround.Load();
            var wanted = new List<(string name, string text)>();
            var children = new List<long>();
            var rows = keyScene.Pieces.Where(p => OnFuelStopKit(p) || p.Kind == KeySceneDef.KindGull).ToList();
            if (rows.Count != 30 || rows.Count(OnFuelStopKit) != 28) throw new Refusal(FuelStopStep + ": the fuel stop has " + rows.Count + " rows, not 28 and two perches.");
            foreach (KeyScenePiece row in rows)
            {
                if (ground.ElevationAt(row.At) <= NineMileCreekBuilder.SpringHighWater)
                    throw new Refusal(row.Id + ": the fuel stop's ground is under at spring high.");
                long pieceGo = Alloc(row.Id + "/go"), pieceTr = Alloc(row.Id + "/tr");
                var comps = new List<long> { pieceTr };
                var parts = new List<(string name, string text)>();
                Vector2 at = row.At;
                if (row.Kind == KeySceneDef.KindGull)
                {
                    at.y += (row.Z - ground.ElevationAt(row.At)) * IsoGround.HeightScale;
                    long perch = Alloc(row.Id + "/perch"); comps.Add(perch);
                    parts.Add((row.Id + "/perch", MonoBehaviourYaml(perch, pieceGo,
                        Script("Assets/_Project/Code/World/GullPerch.cs", "HiddenHarbours.World", "HiddenHarbours.World.GullPerch"),
                        new[] { "  _id: " + row.Id, "  _groundPoint: " + Vec2Yaml(row.At), "  _sortY: " + F(row.At.y) })));
                }
                else SetPieceParts(row, FuelStopDefinition(row), k => Alloc(row.Id + "/" + k), pieceGo, material, comps, parts);
                wanted.Add((row.Id + "/go", GameObjectYaml(pieceGo, row.Id, comps)));
                wanted.Add((row.Id + "/tr", TransformYaml(pieceTr, pieceGo, at, Array.Empty<long>(), tr)));
                wanted.AddRange(parts); children.Add(pieceTr);
                if (OnFuelStopKit(row)) children.AddRange(Lamps(row, FuelStopDefinition(row), Alloc, wanted, tr));
            }

            // The pumps: one per selling nozzle, at the nozzle the kit draws, charging through the wallet the old pumps used.
            ObjRef station = Asset(RoadStationPath);
            long wallet = scene.Docs.Single(d => d.IsScript("HiddenHarbours.Economy.FuelPump") &&
                                                  d.Field("_id") == "fixture.nmc.wharf_pump_gas").FieldRef("_walletProvider");
            if (wallet == 0) throw new Refusal(FuelStopStep + ": the wharf gas pedestal names no wallet.");
            var byRow = rows.ToDictionary(r => r.Id);
            foreach (var pump in FuelPumps)
            {
                Vector3 nozzle = RowAnchor(byRow[pump.Row], pump.Anchor);
                string name = "fitting_" + pump.Anchor;
                long pumpGo = Alloc(pump.Id + "/go"), pumpTr = Alloc(pump.Id + "/tr"), hose = Alloc(pump.Id + "/pump");
                wanted.Add((pump.Id + "/go", GameObjectYaml(pumpGo, name, new[] { pumpTr, hose })));
                wanted.Add((pump.Id + "/tr", TransformYaml(pumpTr, pumpGo, new Vector2(nozzle.x, nozzle.y), Array.Empty<long>(), tr)));
                wanted.Add((pump.Id + "/pump", MonoBehaviourYaml(hose, pumpGo,
                    Script("Assets/_Project/Code/Economy/FuelPump.cs", "HiddenHarbours.Economy", "HiddenHarbours.Economy.FuelPump"),
                    new[] { "  _station: " + station.Yaml, "  _grade: " + pump.Grade, "  _walletProvider: {fileID: " + Id(wallet) + "}",
                        "  _id: " + pump.Id, "  _reachMeters: 2", "  _requiresFacing: 0", "  _onFilled:", "    m_PersistentCalls:",
                        "      m_Calls: []" })));
                children.Add(pumpTr);
            }
            children.Add(LoanerCans(byRow["station.nmc_store"], rows, material, ground, Alloc, wanted, tr));
            wanted.Insert(0, (ForecourtName + "/go", GameObjectYaml(go, ForecourtName, new[] { tr })));
            wanted.Insert(1, (ForecourtName + "/tr", TransformYaml(tr, go, Vector2.zero, children, fuelTr.FileId)));

            var patch = new LayerPatch(FuelStopStep, NineMileCreekStation.RootName, fuel.FileId);
            var keep = new HashSet<long>(scene.SubtreeOf(fuel.FileId).Where(id => !retiring.Contains(id)));
            patch.AddAfter = scene.Docs.Last(d => keep.Contains(d.FileId) && !mine.Contains(d.FileId)).FileId;
            foreach (long id in retiring.OrderBy(id => id))
                patch.Delete(scene.Require(id, NineMileCreekStation.ForecourtRootName), NineMileCreekStation.ForecourtRootName + "/retired &" + Id(id));
            var wantIds = new HashSet<long>(wanted.Select(w => new Doc(w.text).FileId));
            foreach (long id in mine.Where(id => !wantIds.Contains(id)).OrderBy(id => id))
                patch.Delete(scene.Require(id, ForecourtName), ForecourtName + "/retired &" + Id(id));
            foreach (var w in wanted)
            {
                Doc doc = new Doc(w.text), was = scene.Get(doc.FileId);
                if (was == null) patch.Add(w.text, w.name);
                else if (!mine.Contains(doc.FileId)) throw new Refusal(FuelStopStep + ": an id is taken outside the forecourt: " + w.name);
                else patch.Edit(was, w.text, w.name);
            }
            // CreekFuel lists the new forecourt where the old one stood.
            long oldTr = old == null ? 0 : scene.TransformOf(old).FileId;
            List<long> listed = fuelTr.FieldRefs("m_Children");
            var next = listed.Select(c => c == oldTr ? tr : c).Distinct().ToList();
            if (!next.Contains(tr)) next.Add(tr);
            if (!next.SequenceEqual(listed)) patch.Edit(fuelTr, WithChildren(fuelTr.Text, next), NineMileCreekStation.RootName + "/children");
            return patch.Seal(scene);
        }

        /// <summary>
        /// The loaner cans (charter step 5), laid again: two gas and one diesel, empty, on the apron beside the new
        /// store's door, the spawn menu's four components each (picture, level, sorting, carry) with a stable id.
        /// They stand off the door's stand by more than a can's reach, so the door keeps its press, and on dry ground
        /// clear of every row's walls. Returns the cans' parent transform.
        /// </summary>
        static long LoanerCans(KeyScenePiece store, IList<KeyScenePiece> rows, ObjRef material, NineMileCreekGround.PlanGround ground,
            Func<string, long> alloc, List<(string name, string text)> wanted, long father)
        {
            Vector3 door = RowAnchor(store, "door"), stand = RowAnchor(store, "door.stand");
            Vector2 standAt = new Vector2(stand.x, stand.y), outward = (standAt - new Vector2(door.x, door.y)).normalized;
            Vector2 side = new Vector2(outward.y, -outward.x);
            const float firstOff = 2.2f;
            long go = alloc("cans/go"), tr = alloc("cans/tr");
            var cans = new List<long>();
            string[] defIds = NineMileCreekStation.LoanerCanDefIds;
            for (int i = 0; i < defIds.Length; i++)
            {
                FuelContainerDef def = StationFuelCans.LoadContainer(defIds[i])
                    ?? throw new Refusal(FuelStopStep + ": no FuelContainerDef " + defIds[i] + ".");
                if (!def.Carriable) throw new Refusal(FuelStopStep + ": " + defIds[i] + " is not carriable.");
                Vector2 at = standAt + side * (firstOff + i * NineMileCreekStation.LoanerCanSpacingMetres);
                if (ground.ElevationAt(at) <= NineMileCreekBuilder.SpringHighWater)
                    throw new Refusal(FuelStopStep + ": a loaner can would stand under spring high.");
                foreach (KeyScenePiece row in rows.Where(OnFuelStopKit))
                {
                    SetPieceDef rowDef = FuelStopDefinition(row);
                    foreach (Vector2[] wall in WallsOf(rowDef.Colliders, SetPieceDef.FrameForRigDir(row.Dir), row.Id))
                        if (Inside(wall, at - row.At)) throw new Refusal(FuelStopStep + ": a loaner can would stand inside " + row.Id + ".");
                }
                Sprite frame = def.Frame(FuelLevelPresenter.NearestFillIndex(def.FillFractions, 0f), 0);
                if (frame == null) throw new Refusal(FuelStopStep + ": " + defIds[i] + " has no empty frame.");
                string id = def.Id + ".at_" + (at.x.ToString("0.##", CultureInfo.InvariantCulture) + "_" +
                    at.y.ToString("0.##", CultureInfo.InvariantCulture)).Replace('.', 'p').Replace('-', 'm');
                string key = "cans/" + def.Id;
                long canGo = alloc(key + "/go"), canTr = alloc(key + "/tr"), renderer = alloc(key + "/sprite"),
                    level = alloc(key + "/level"), sort = alloc(key + "/sort"), carry = alloc(key + "/carry");
                wanted.Add((key + "/go", GameObjectYaml(canGo, "EmptyCan_" + def.Id, new[] { canTr, renderer, level, sort, carry })));
                wanted.Add((key + "/tr", TransformYaml(canTr, canGo, at, Array.Empty<long>(), tr)));
                int order = YSortSprite.OrderFor(at.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre, SortingBands.DecorFloor, SortingBands.DecorCeiling);
                wanted.Add((key + "/sprite", SpriteRendererYaml(renderer, canGo, material, SpriteAsset(frame), order)));
                wanted.Add((key + "/level", MonoBehaviourYaml(level, canGo,
                    Script("Assets/_Project/Code/Art/FuelLevelPresenter.cs", "HiddenHarbours.Art", "HiddenHarbours.Art.FuelLevelPresenter"),
                    new[] { "  _container: " + Asset(AssetDatabase.GetAssetPath(def)).Yaml, "  _fill: 0", "  _facing: 0" })));
                wanted.Add((key + "/sort", MonoBehaviourYaml(sort, canGo,
                    Script("Assets/_Project/Code/Art/YSortSprite.cs", "HiddenHarbours.Art", "HiddenHarbours.Art.YSortSprite"),
                    new[] { "  _dynamic: 0", "  _baseOrder: " + SortingBands.DecorBase, "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre),
                        "  _minOrder: " + SortingBands.DecorFloor, "  _maxOrder: " + SortingBands.DecorCeiling, "  _pivotYOffset: 0" })));
                wanted.Add((key + "/carry", MonoBehaviourYaml(carry, canGo,
                    Script("Assets/_Project/Code/Player/CarriableFuelContainer.cs", "HiddenHarbours.Player", "HiddenHarbours.Player.CarriableFuelContainer"),
                    new[] { "  _id: " + id, "  _reachMeters: " + F(HiddenHarbours.Player.CarriableFuelContainer.LooseReachMetres), "  _requiresFacing: 0" })));
                cans.Add(canTr);
            }
            wanted.Add(("cans/go", GameObjectYaml(go, NineMileCreekStation.LoanerCansRootName, new[] { tr })));
            wanted.Add(("cans/tr", TransformYaml(tr, go, Vector2.zero, cans, father)));
            return tr;
        }

        /// <summary>Clearance kept between an authored wall and the stand a press is made from, metres.</summary>
        const float StandClearance = 0.3f;
        /// <summary>How far a machine's block reaches past its outermost nozzles, metres.</summary>
        const float MachinePad = 0.25f;
        /// <summary>
        /// The walls the kit leaves to the scene phase (its README: the exported footprint is a bounding polygon, not a
        /// walk blocker). The store: its box between the door's wall and the service door's wall, both stands outside.
        /// The canopy and the semi lane: each machine's block around its own nozzles, every stand left outside. Piece
        /// metres, as the kit's own colliders are; <see cref="WallsOf"/> turns them with the row.
        /// </summary>
        static SetPieceCollider[] AuthoredWalls(KeyScenePiece row, SetPieceDef def)
        {
            var walls = new List<SetPieceCollider>();
            Vector2 Local(string anchor) => def.TryGetAnchor(anchor, out Vector3 v) ? new Vector2(v.x, v.y)
                : throw new Refusal(row.Id + " has no " + anchor + " anchor.");
            void Box(Vector2 min, Vector2 max, IEnumerable<string> stands)
            {
                var pts = new[] { new Vector2(max.x, max.y), new Vector2(min.x, max.y), new Vector2(min.x, min.y), new Vector2(max.x, min.y) };
                foreach (string stand in stands)
                {
                    Vector2 at = Local(stand);
                    if (at.x > min.x - StandClearance && at.x < max.x + StandClearance && at.y > min.y - StandClearance && at.y < max.y + StandClearance)
                        throw new Refusal(row.Id + ": an authored wall would take the stand " + stand + ".");
                }
                walls.Add(new SetPieceCollider { Type = PolygonShape, Blocks = BlocksWalk, Points = pts });
            }
            if (row.Piece == "store")
            {
                float front = Local("door").y, back = Local("serviceDoor").y;
                Box(new Vector2(def.BoundsMin.x, Mathf.Min(front, back)), new Vector2(def.BoundsMax.x, Mathf.Max(front, back)),
                    new[] { "door.stand", "serviceDoor.stand" });
            }
            foreach (var machine in FuelPumps.Where(f => f.Row == row.Id).GroupBy(f => MachineOf(f.Anchor)).Where(g => g.Key >= 0))
            {
                var nozzles = machine.Select(f => Local(f.Anchor)).ToList();
                Vector2 min = new Vector2(nozzles.Min(n => n.x), nozzles.Min(n => n.y)) - Vector2.one * MachinePad;
                Vector2 max = new Vector2(nozzles.Max(n => n.x), nozzles.Max(n => n.y)) + Vector2.one * MachinePad;
                Box(min, max, FuelPumps.Where(f => f.Row == row.Id).Select(f => f.Anchor + ".stand"));
            }
            return walls.ToArray();
        }

        /// <summary>The machine a drawn nozzle hangs on: the canopy's three by six nozzles each, the semi lane's one by
        /// its four lane nozzles (machine 0); -1 for a nozzle on no machine (KERO, the satellite).</summary>
        static int MachineOf(string anchor)
        {
            string[] parts = anchor.Split('.');
            if (parts.Length != 4 || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return -1;
            return n / 6;
        }

        /// <summary>
        /// The lit pieces' lamps, to the project's lamp look (<see cref="LightPresets"/>; the 2026-09-04 ruling: a
        /// bloom is the size of the lit FITTING, the ground light is the pool <see cref="LampPoolSystem"/> draws from
        /// the reach). <see cref="NineMileCreekFuelStopPlan.LampRules"/> says what each lit kind is as a lamp; a lit
        /// kind with no rule (a sign) gets no ground light. Every value is the preset's, stamped as text because the
        /// patch writes text. The light's own shader gate scales it by the frame's darkness (ADR 0016), so day, dusk
        /// and night come from the sky with no runtime of H9's. Returns the transforms to list under the forecourt.
        /// </summary>
        static IEnumerable<long> Lamps(KeyScenePiece row, SetPieceDef def, Func<string, long> alloc,
            List<(string name, string text)> wanted, long father)
        {
            var lit = new List<long>();
            if (!def.HasLight || def.Light.Emitters.Length == 0) return lit;
            NineMileCreekFuelStopPlan.LampRule rule = NineMileCreekFuelStopPlan.LampRuleFor(row.Piece);
            if (rule == null) return lit;
            var at = new List<Vector2>();
            if (rule.PerMachine)
                foreach (var machine in FuelPumps.Where(f => f.Row == row.Id).GroupBy(f => MachineOf(f.Anchor)).Where(g => g.Key >= 0).OrderBy(g => g.Key))
                {
                    Vector2 sum = Vector2.zero; int n = 0;
                    foreach (var f in machine) { Vector3 nozzle = RowAnchor(row, f.Anchor); sum += new Vector2(nozzle.x, nozzle.y); n++; }
                    at.Add(sum / n);
                }
            else if (rule.Anchor != null) { Vector3 a = RowAnchor(row, rule.Anchor); at.Add(new Vector2(a.x, a.y)); }
            else at.Add(row.At + SetPieceDef.ToWorld(rule.Local, row.Dir));
            if (at.Count == 0) throw new Refusal(row.Id + ": its lamp rule finds nothing to light.");

            LightPresets.Config look = LightPresets.For(rule.Kind);
            float height = rule.Height > 0f ? rule.Height : def.BoundsMax.z;
            bool bloom = rule.FittingWidth > 0f;
            for (int i = 0; i < at.Count; i++)
            {
                // The first lamp keeps the key the row's one glow had, so its documents keep their ids.
                string key = row.Id + "/lamp" + (i == 0 ? "" : "." + i.ToString(CultureInfo.InvariantCulture));
                long go = alloc(key + "/go"), tr = alloc(key + "/tr"), lamp = alloc(key + "/light");
                wanted.Add((key + "/go", GameObjectYaml(go, "lamp_" + row.Id + (i == 0 ? "" : "_" + i.ToString(CultureInfo.InvariantCulture)), new[] { tr, lamp })));
                wanted.Add((key + "/tr", TransformYaml(tr, go, at[i], Array.Empty<long>(), father)));
                wanted.Add((key + "/light", MonoBehaviourYaml(lamp, go,
                    Script("Assets/_Project/Code/Art/SceneLight.cs", "HiddenHarbours.Art", "HiddenHarbours.Art.SceneLight"),
                    new[]
                    {
                        "  _shape: " + (int)look.Shape,
                        "  _range: " + F(bloom ? LightPresets.BloomForFitting(rule.FittingWidth) : look.Range),
                        "  _color: {r: " + F(look.Color.r) + ", g: " + F(look.Color.g) + ", b: " + F(look.Color.b) + ", a: " + F(look.Color.a) + "}",
                        "  _intensity: " + F(look.Intensity),
                        "  _edgeSoftness: " + F(look.EdgeSoftness),
                        "  _drawsBloom: " + (bloom ? "1" : "0"),
                        "  _flickerAmount: " + F(look.FlickerAmount),
                        "  _castsShadows: 0",
                        "  _lampHeightMeters: " + F(height),
                        "  _bloomLiftMetres: " + F(bloom ? height * IsoGround.HeightScale : 0f),
                        "  _reachMetres: " + F(LightPresets.ReachMetres(rule.Kind)),
                        "  _originOffset: " + Vec2Yaml(look.OriginOffset),
                    })));
                lit.Add(tr);
            }
            return lit;
        }

        static bool Inside(Vector2[] polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                if ((polygon[i].y > p.y) != (polygon[j].y > p.y) &&
                    p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    inside = !inside;
            return inside;
        }

        static string WithChildren(string transformText, IList<long> children)
        {
            var lines = transformText.Split('\n').ToList();
            int head = lines.FindIndex(l => l == "  m_Children:" || l == "  m_Children: []");
            if (head < 0) throw new Refusal(FuelStopStep + ": CreekFuel's transform has no children list.");
            int end = head + 1;
            while (end < lines.Count && lines[end].StartsWith("  - ", StringComparison.Ordinal)) end++;
            lines.RemoveRange(head, end - head);
            lines.InsertRange(head, children.Count == 0 ? new[] { "  m_Children: []" }
                : new[] { "  m_Children:" }.Concat(children.Select(c => "  - {fileID: " + Id(c) + "}")));
            return string.Join("\n", lines);
        }

        /// <summary>
        /// The key scenes record what stands (Decision 5's record too): the fuel stop is placed with its 30 rows, the wharf's
        /// selection gains the eleven, and the owners record clears their <c>waits</c> (owners stay as they are).
        /// </summary>
        public static void RecordFuelStopPlacement()
        {
            string fuel = File.ReadAllText(FuelStopKeyScenePath);
            string[] thirty = FuelStopKeyScene().Pieces.Where(p => OnFuelStopKit(p) || p.Kind == KeySceneDef.KindGull).Select(p => p.Id).ToArray();
            string next = Regex.Replace(fuel, @"(?m)^  Placed: [01]$", "  Placed: 1");
            next = Regex.Replace(next, @"(?m)^  PlacementIds:(?: \[\])?(?:\n  - [^\n]+)*", "  PlacementIds:\n" + string.Join("\n", thirty.Select(id => "  - " + id)));
            if (next != fuel) File.WriteAllText(FuelStopKeyScenePath, next);

            string wharf = File.ReadAllText(KeyScenePath);
            var eleven = FuelStopWharfRows().Select(r => r.Id).ToArray();
            var ids = KeyScene().PlacementIds.Concat(eleven).Distinct().ToArray();
            string wharfNext = Regex.Replace(wharf, @"(?m)^  PlacementIds:(?: \[\])?(?:\n  - [^\n]+)*", "  PlacementIds:\n" + string.Join("\n", ids.Select(id => "  - " + id)));
            if (wharfNext != wharf) File.WriteAllText(KeyScenePath, wharfNext);

            string record = File.ReadAllText(RecordPath);
            string recordNext = record;
            foreach (string id in eleven)
                recordNext = Regex.Replace(recordNext,
                    "(\"id\": \"" + Regex.Escape(id) + "\"[^}]*?),\\s*\"waits\": \"fuelStopRig intake \\(H9\\)\"", "$1");
            if (recordNext != record) File.WriteAllText(RecordPath, recordNext);
            AssetDatabase.ImportAsset(FuelStopKeyScenePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(KeyScenePath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// H9's scene step, applied the way the vendor anchors were: the wharf's rows (now with the eleven), then the
        /// forecourt, on the committed scene. Writes the patches under <paramref name="evidence"/>, proves a second apply
        /// changes no byte, and writes the scene and the records only when <paramref name="apply"/> is set.
        /// </summary>
        public static void ApplyFuelStop(string evidence, bool apply = false)
        {
            ApplyCounterPatches(evidence, "fuel-stop", "The fuel stop", new Func<SceneYaml, LayerPatch>[]
            {
                PlaceRows, PlaceForecourt,
                // The ground under it (H9 phase C): the forecourt pad moved with the stop, so what kit v3 paints is
                // painted again, the meadow steps off the pad and the planting on it is retired.
                NineMileCreekGroundSteps.EssentialRoads,
                s => NineMileCreekGroundSteps.PadMeadow(s, ForecourtArea(), FuelStopStep),
                s => NineMileCreekGroundSteps.PadPlanting(s, "CreekHedgerows", ForecourtArea(), FuelStopStep, "on the fuel stop's forecourt"),
                s => NineMileCreekGroundSteps.PadPlanting(s, "CreekTrees", ForecourtArea(), FuelStopStep, "on the fuel stop's forecourt"),
                s => NineMileCreekGroundSteps.PadPlanting(s, "CreekUnderstorey", ForecourtArea(), FuelStopStep, "on the fuel stop's forecourt"),
                s => NineMileCreekGroundSteps.PadPlanting(s, "CreekMarsh", ForecourtArea(), FuelStopStep, "on the fuel stop's forecourt"),
            }, apply);
            if (apply) RecordFuelStopPlacement();
        }
        public static void FuelStopDryRunFromCommandLine() => ApplyFuelStop("Evidence~/nmc-h9/patch/dry-run");
        public static void FuelStopApplyFromCommandLine()
        {
            ApplyFuelStop("Evidence~/nmc-h9/patch/dry-run");
            ApplyFuelStop("Evidence~/nmc-h9/patch/apply", true);
            ApplyFuelStop("Evidence~/nmc-h9/patch/after-apply");
            AssetDatabase.Refresh();
        }
    }
}
#endif
