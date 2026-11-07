#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    public static partial class NineMileCreekWharfLandSide
    {
        const string WaterStep = "18-wharf-water-side";
        static object WM(object value, string key) => DeckSidecarJson.Member(value, key);
        static IReadOnlyList<object> WA(object value) => DeckSidecarJson.AsArray(value);
        static float WN(object value) => DeckSidecarJson.Float(value);
        static string WS(object value) => DeckSidecarJson.String(value);
        static bool WB(object value) => value is bool b && b;
        static Vector2 WP(object value) { var a = WA(value); return new Vector2(WN(a[0]), WN(a[1])); }
        static ObjRef WaterAsset(string path, long fileId = 11400000, int type = 2)
        {
            string guid=AssetDatabase.AssetPathToGUID(path);
            if(string.IsNullOrEmpty(guid))throw new Refusal("H5 asset is not imported: "+path);
            return new ObjRef(fileId,guid,type);
        }
        static ScriptRef WaterScript(string path,string assembly,string name) =>
            new ScriptRef(WaterAsset(path,11500000,3),assembly+"::"+name);

        sealed class WaterNode
        {
            public string Name, Key;
            public long Go, Tr, Parent;
            public Vector2 At;
            public List<long> Components = new List<long>(), Children = new List<long>();
            public string Rotation;
        }
        sealed class WaterWriter
        {
            public readonly List<(string name, string text)> Docs = new List<(string, string)>();
            public readonly List<WaterNode> Nodes = new List<WaterNode>();
            readonly IdAllocator ids;
            readonly ObjRef material = new EditorKeySceneAssets().SpriteMaterial;
            public WaterWriter(SceneYaml scene)
            {
                var root = ExistingRoot(scene, NineMileCreekWharfWaterSide.RootName);
                ids = new IdAllocator(scene, root == null ? new HashSet<long>() : scene.SubtreeOf(root.FileId));
            }
            long Id(string key) => ids.Next("nmc-h5/" + key);
            public WaterNode Node(string key, string name, Vector2 at, WaterNode parent)
            {
                var n = new WaterNode { Key=key, Name=name, Go=Id(key+"/go"), Tr=Id(key+"/tr"), At=at, Parent=parent?.Tr ?? 0 };
                n.Components.Add(n.Tr); parent?.Children.Add(n.Tr); Nodes.Add(n); return n;
            }
            public long Mono(WaterNode node, string key, string path, string assembly, string type, IEnumerable<string> fields)
            {
                long id=Id(node.Key+"/"+key);node.Components.Add(id);
                Docs.Add((node.Name+"/"+key,MonoBehaviourYaml(id,node.Go,WaterScript(path,assembly,type),fields)));return id;
            }
            public void Picture(WaterNode n, Sprite sprite, int order)
            {
                long id=Id(n.Key+"/sprite");n.Components.Add(id);
                Docs.Add((n.Name+"/sprite",SpriteRendererYaml(id,n.Go,material,SpriteAsset(sprite),order)));
            }
            public void Source(WaterNode node, SetPieceDef def, string path, string row, int dir) => Mono(node,"source",
                "Assets/_Project/Code/Art/SetPiecePlacement.cs","HiddenHarbours.Art","HiddenHarbours.Art.SetPiecePlacement",
                new[]{"  _def: "+WaterAsset(path).Yaml,"  _definitionId: "+def.Id,"  _placementId: "+row,"  _rigDir: "+dir});
            public void Sort(WaterNode n) => Mono(n,"sort","Assets/_Project/Code/Art/YSortSprite.cs","HiddenHarbours.Art","HiddenHarbours.Art.YSortSprite",
                new[]{"  _dynamic: 0","  _baseOrder: "+SortingBands.DecorBase,"  _orderPerUnit: "+F(SortingBands.OrdersPerMetre),
                    "  _minOrder: "+SortingBands.DecorFloor,"  _maxOrder: "+SortingBands.DecorCeiling,"  _pivotYOffset: 0"});
            public void Walls(WaterNode n,List<Vector2[]> walls)
            {
                if(walls.Count==0)return;long id=Id(n.Key+"/walls");n.Components.Add(id);Docs.Add((n.Name+"/walls",PolygonColliderYaml(id,n.Go,walls)));
            }
            public void Drive(SceneYaml scene, WaterNode node, string row, ObjRef definition) =>
                AddTruckDriveParts(scene,ids,row,node.Go,definition,node.Components,Docs);
            public LayerPatch Finish(SceneYaml scene,WaterNode root)
            {
                foreach(var n in Nodes){Docs.Add((n.Name,GameObjectYaml(n.Go,n.Name,n.Components)));
                    string text=TransformYaml(n.Tr,n.Go,n.At,n.Children,n.Parent);if(n.Rotation!=null)text=SetField(text,"m_LocalRotation",n.Rotation);
                    Docs.Add((n.Name+"/transform",text));}
                return WriteRoot(scene,root.Name,root.Go,Docs,WaterStep);
            }
        }

        static Sprite WaterSprite(SetPieceDef def, string name, int frame)
        {
            if(name==null)return def.SpriteFor(frame);
            return def.Variants.Single(v=>v.Name==name).Frames[frame].Sprite;
        }
        static string RectFields(Rect r) => "    serializedVersion: 2\n    x: "+F(r.x)+"\n    y: "+F(r.y)+"\n    width: "+F(r.width)+"\n    height: "+F(r.height);

        public static LayerPatch PlaceWaterSide(SceneYaml scene, bool crib)
        {
            var writer=new WaterWriter(scene);
            var root=writer.Node("root",NineMileCreekWharfWaterSide.RootName,Vector2.zero,null);
            var today=NineMileCreekWharfWaterSide.ReadToday();
            var parts=WA(WM(today,"parts"));var native=WA(WM(today,"pieces"));
            var ground=NineMileCreekGround.Load();
            // Alma was absent from the inherited scene; the other seven resident objects are moved in place.
            foreach(var row in NineMileCreekWharfWaterSide.PlacementRows().Where(r=>r.Kind!="boat"||r.Id=="boat.nmc_alma"))
            {
                var rowNode=writer.Node(row.Id,row.Id,row.At,root);
                if(row.Kind=="harbour")
                {
                    string key=row.Id.Substring("harbour.".Length);var def=NineMileCreekWharfWaterSide.Definition(key);
                    writer.Source(rowNode,def,"Assets/_Project/Data/SetPieces/SetPiece_nmc_"+key+".asset",row.Id,row.Dir);
                    object piece=native.Single(p=>WS(WM(p,"key"))==key);
                    var keys=WA(WM(piece,"parts")).Select(WS).ToArray();
                    var floats=new Dictionary<string,long>();
                    // Surfaces first: each brow and moving cleat references the same float.
                    foreach(var p in parts.Where(p=>WS(WM(p,"harbour"))==key && WB(WM(p,"floating"))))
                    {
                        string name=WS(WM(p,"name")),id=name=="float"?"float.nmc_working":name=="recFloat"?"float.nmc_recreation":name=="fingerA"?"float.nmc_finger_a":"float.nmc_finger_b";
                        Vector2[] poly=WA(WM(p,"walkPolygon")).Select(WP).ToArray();
                        Rect rect=Rect.MinMaxRect(poly.Min(q=>q.x),poly.Min(q=>q.y),poly.Max(q=>q.x),poly.Max(q=>q.y));
                        float bed=float.NegativeInfinity;
                        for(float x=rect.xMin;x<=rect.xMax+.001f;x+=.2f)for(float y=rect.yMin;y<=rect.yMax+.001f;y+=.2f)bed=Mathf.Max(bed,ground.ElevationAt(new Vector2(x,y)));
                        var surface=writer.Node(row.Id+"/surface/"+name,id,Vector2.zero,root);
                        floats[name]=writer.Mono(surface,"surface","Assets/_Project/Code/World/FloatingPlatform.cs","HiddenHarbours.World","HiddenHarbours.World.FloatingPlatform",
                            new[]{"  _id: "+id,"  _footprint:",RectFields(rect),"  _freeboardMetres: "+F(WN(WM(p,"freeboard"))),"  _draughtMetres: "+F(WN(WM(p,"draught"))),"  _bedElevation: "+F(bed)});
                    }
                    foreach(string partKey in keys.OrderBy(k=>k=="fleet_crib"))
                    {
                        var p=parts.Single(p=>WS(WM(p,"key"))==partKey);
                        if(partKey=="fleet_crib"&&!crib)continue;
                        string name=WS(WM(p,"name"));object gang=WM(p,"gangway");
                        if(gang!=null&&WN(WM(gang,"step"))!=0)continue;
                        Vector2 at=WP(WM(p,"at"));float elevation=WN(WM(p,"elevation"));
                        var node=writer.Node(row.Id+"/"+name,name,at-row.At+Vector2.up*(elevation*IsoGround.HeightScale),rowNode);
                        Sprite sprite=WaterSprite(def,partKey==keys[0]?null:name,0);
                        string layer=WS(WM(p,"motionLayer"));
                        int order=NineMileCreekWharf.FloatSortingOrder+(layer=="back"?-1:layer=="front"?1:0);
                        writer.Picture(node,sprite,order);
                        if(WB(WM(p,"floating")))writer.Mono(node,"ride","Assets/_Project/Code/World/FloatingPlatformVisual.cs","HiddenHarbours.World","HiddenHarbours.World.FloatingPlatformVisual",
                            new[]{"  _platform: {fileID: "+floats[name]+"}","  _bakedDeckElevation: "+F(WN(WM(p,"freeboard"))),"  _planY: "+F(at.y)});
                        else if(gang!=null)
                        {
                            string id="gangway.nmc_"+key+"_"+WS(WM(gang,"id"));
                            var surface=writer.Node(row.Id+"/surface/"+id,id,Vector2.zero,root);
                            long brow=writer.Mono(surface,"brow","Assets/_Project/Code/World/GangwayPlatform.cs","HiddenHarbours.World","HiddenHarbours.World.GangwayPlatform",
                                new[]{"  _id: "+id,"  _shoreEnd: "+Vec2Yaml(WP(WM(gang,"hinge"))),"  _floatEnd: "+Vec2Yaml(WP(WM(gang,"landing"))),
                                    "  _halfWidthMetres: "+F(WN(WM(gang,"width"))*.5f),"  _abutmentElevation: "+F(elevation),"  _landing: {fileID: "+floats[WS(WM(gang,"floatModule"))]+"}"});
                            var poses=parts.Where(q=>WS(WM(q,"harbour"))==key&&WM(q,"gangway")!=null&&WS(WM(WM(q,"gangway"),"id"))==WS(WM(gang,"id"))).OrderBy(q=>WN(WM(WM(q,"gangway"),"step"))).ToArray();
                            writer.Mono(node,"slope","Assets/_Project/Code/World/GangwayVisual.cs","HiddenHarbours.World","HiddenHarbours.World.GangwayVisual",
                                new[]{"  _brow: {fileID: "+brow+"}","  _rungs:"}.Concat(poses.Select(q=>"  - "+SpriteAsset(WaterSprite(def,WS(WM(q,"name")),0)).Sprite.Yaml))
                                .Concat(new[]{"  _rungDrops:"}).Concat(poses.Select(q=>"  - "+F(WN(WM(WM(q,"gangway"),"drop"))))));
                        }
                        else if(WM(p,"walkPolygon")!=null)
                        {
                            var surface=writer.Node(row.Id+"/surface/"+name,name+".walk",Vector2.zero,root);
                            var poly=WA(WM(p,"walkPolygon")).Select(WP).ToArray();float toe=elevation;
                            object module=WM(p,"module"),options=WM(module,"o");object toeRaw=WM(options,"toeZ");
                            if(toeRaw!=null)toe=WN(toeRaw)-2.2f;
                            Vector2 from=new Vector2(at.x,poly.Max(q=>q.y)),to=new Vector2(at.x,poly.Min(q=>q.y));
                            writer.Mono(surface,"walk","Assets/_Project/Code/App/NmcWharfWalkSurface.cs","HiddenHarbours.App","HiddenHarbours.App.NmcWharfWalkSurface",
                                new[]{"  _id: "+row.Id+"."+name,"  _polygon:"}.Concat(poly.Select(q=>"  - "+Vec2Yaml(q))).Concat(new[]{"  _slopeFrom: "+Vec2Yaml(from),"  _slopeTo: "+Vec2Yaml(to),"  _fromElevation: "+F(elevation),"  _toElevation: "+F(toe)}));
                        }
                    }
                    foreach(var anchor in def.Anchors.Where(a=>a.Name.StartsWith("cleat.")||a.Name.Contains(".cleat.")))
                    {
                        Vector2 at=row.At+(Vector2)anchor.Position;var n=writer.Node(row.Id+"/"+anchor.Name,anchor.Name,at,root);
                        string floatName=null;
                        if(key=="fleet"&&(anchor.Name.Contains("finger_a")||anchor.Name.Contains("_fingerA_")))floatName="fingerA";
                        else if(key=="fleet"&&(anchor.Name.Contains("finger_b")||anchor.Name.Contains("_fingerB_")))floatName="fingerB";
                        else if(key=="west"&&anchor.Name.Contains("_float_"))floatName="float";
                        else if(key=="east"&&anchor.Name.Contains("_recFloat_"))floatName="recFloat";
                        if(floatName!=null)writer.Mono(n,"cleat","Assets/_Project/Code/World/FloatCleat.cs","HiddenHarbours.World","HiddenHarbours.World.FloatCleat",new[]{"  _id: "+anchor.Name,"  _platform: {fileID: "+floats[floatName]+"}"});
                        else writer.Mono(n,"cleat","Assets/_Project/Code/World/ShoreCleat.cs","HiddenHarbours.World","HiddenHarbours.World.ShoreCleat",new[]{"  _id: "+anchor.Name,"  _elevationMeters: "+F(anchor.Position.z)});
                    }
                    foreach(var anchor in def.Anchors.Where(a=>a.Name.StartsWith("ladder.")&&a.Name!="ladder.nmc_berth0"))
                    {
                        var n=writer.Node(row.Id+"/"+anchor.Name,anchor.Name,row.At+(Vector2)anchor.Position,root);
                        writer.Mono(n,"climb","Assets/_Project/Code/World/WharfLadder.cs","HiddenHarbours.World","HiddenHarbours.World.WharfLadder",
                            new[]{"  _id: "+anchor.Name,"  _topElevationMeters: "+F(anchor.Position.z),"  _climbTopScreenOffset: "+F(NineMileCreekGroundPlan.LadderClimbTopScreenOffset),
                                "  _spanWidthMetres: 0.45","  _spanSlowTickSeconds: 0.5","  _faceHeadingDegrees: 180","  _rungMetres: "+F(LadderBoardingMath.RigRungMetres),"  _standoffMetres: "+F(LadderBoardingMath.RigStandoffMetres)});
                    }
                }
                else if(row.Kind=="boat")
                {
                    var owner=NineMileCreekMooredFleet.LoadOwners().Single(o=>o.Id=="owner.boudreau_alma");
                    if(owner.Boat.DraughtMeters>NineMileCreekWharf.DeepestDraughtAFloatBerthCarries(owner.BerthIndex,ground))
                        throw new Refusal("Alma's registered berth does not carry her draught.");
                    writer.Mono(rowNode,"resident","Assets/_Project/Code/Boats/MooredBoat.cs","HiddenHarbours.Boats","HiddenHarbours.Boats.MooredBoat",
                        new[]{"  _owner: "+WaterAsset(AssetDatabase.GetAssetPath(owner)).Yaml,"  _headingDegrees: "+F(KitDirHeading(row.Dir))});
                }
                else if(row.Kind=="vehicle")
                {
                    string path=ParkedPickupDefinition(row.Id);
                    writer.Mono(rowNode,"parked","Assets/_Project/Code/Vehicles/ParkedVehicle.cs","HiddenHarbours.Vehicles","HiddenHarbours.Vehicles.ParkedVehicle",new[]{"  _vehicle: "+WaterAsset(path).Yaml,"  _drivable: 1"});
                    writer.Drive(scene,rowNode,row.Id,WaterAsset(path));
                    double angle=-KitDirHeading(row.Dir)*Math.PI/360;rowNode.Rotation="{x: 0, y: 0, z: "+F((float)Math.Sin(angle))+", w: "+F((float)Math.Cos(angle))+"}";
                }
                else if(row.Kind==KeySceneDef.KindGull)
                {
                    rowNode.At+=Vector2.up*((row.Z-ground.ElevationAt(row.At))*IsoGround.HeightScale);
                    writer.Mono(rowNode,"perch","Assets/_Project/Code/World/GullPerch.cs","HiddenHarbours.World","HiddenHarbours.World.GullPerch",new[]{"  _id: "+row.Id,"  _groundPoint: "+Vec2Yaml(row.At),"  _sortY: "+F(row.At.y)});
                }
                else
                {
                    Sprite sprite;SetPieceDef def=null;
                    if(row.Kit=="nmcSetPieces")
                    {
                        def=Definition(row.Piece);sprite=def.SpriteFor(SetPieceDef.FrameForRigDir(row.Dir));
                        writer.Source(rowNode,def,"Assets/_Project/Data/SetPieces/SetPiece_"+row.Piece+".asset",row.Id,row.Dir);
                        writer.Walls(rowNode,WallsOf(def.Colliders,SetPieceDef.FrameForRigDir(row.Dir),row.Id));
                    }
                    else if(row.Kit=="wharfDecor")sprite=IsoPackSprites.Facing(row.Kit,row.Piece,IsoPackSprites.FacingForHeading(row.Kit,KitDirHeading(row.Dir)));
                    else throw new Refusal("H5 has no placement route for "+row.Id+" / "+row.Kit);
                    writer.Picture(rowNode,sprite,YSortSprite.OrderFor(row.At.y,SortingBands.DecorBase,SortingBands.OrdersPerMetre,SortingBands.DecorFloor,SortingBands.DecorCeiling));writer.Sort(rowNode);
                    if(row.Piece=="wharfCrane")writer.Mono(rowNode,"day-night","Assets/_Project/Code/App/NmcWharfCraneVisual.cs","HiddenHarbours.App","HiddenHarbours.App.NmcWharfCraneVisual",
                        new[]{"  _day: "+SpriteAsset(sprite).Sprite.Yaml,"  _night: "+SpriteAsset(WaterSprite(def,"stowed",SetPieceDef.FrameForRigDir(row.Dir))).Sprite.Yaml});
                    if(row.Piece=="harbourLight")
                    {
                        bool west=row.Id.EndsWith("west");writer.Mono(rowNode,"lamp","Assets/_Project/Code/Art/SceneLight.cs","HiddenHarbours.Art","HiddenHarbours.Art.SceneLight",
                            new[]{"  _shape: 1","  _range: 6","  _color: "+(west?"{r: 0.1, g: 1, b: 0.25, a: 1}":"{r: 1, g: 0.1, b: 0.05, a: 1}"),"  _intensity: 1.2","  _edgeSoftness: 0.6","  _gateThreshold: 0.12","  _gateSoftness: 0.35","  _gateFallback: 1","  _sortingOrder: 32767"});
                    }
                }
            }
            return writer.Finish(scene,root);
        }

        public static LayerPatch MoveWaterFleet(SceneYaml scene)
        {
            var root=scene.RootNamed("NineMileCreekFleet");var patch=new LayerPatch(WaterStep,"NineMileCreekFleet",root.FileId);
            var ground=NineMileCreekGround.Load();
            foreach(var owner in NineMileCreekMooredFleet.LoadOwners())
            {
                if(owner.Id=="owner.boudreau_alma")continue; // the previously absent resident is H5's named new row.
                string row=owner.LiesAtTheFloat?NineMileCreekWharfWaterSide.FloatRowForOwner(owner.Id).Id:null;
                var go=scene.Docs.Single(d=>d.ClassId==1&&(d.Field("m_Name")=="Moored_"+owner.Id||(row!=null&&d.Field("m_Name")==row)));
                var transform=scene.TransformOf(go);Vector2 at=owner.LiesAtTheFloat?NineMileCreekWharf.FloatBerthPos(owner.BerthIndex,ground):NineMileCreekMainland.BerthPos(owner.BerthIndex,NineMileCreekMooredFleet.HalfBeamOf(owner));
                patch.Edit(transform,SetField(transform.Text,"m_LocalPosition",Vec3Yaml(at.x,at.y,0)),owner.Id+" / registered berth");
                var mono=scene.ComponentsOf(go).Single(d=>d.IsScript("HiddenHarbours.Boats.MooredBoat"));
                patch.Edit(mono,SetField(mono.Text,"_headingDegrees",F(owner.LiesAtTheFloat?NineMileCreekWharf.FloatMooredHeadingDegrees():NineMileCreekMooredFleet.MooredHeadingDegrees())),owner.Id+" / heading");
                if(row!=null)patch.Edit(go,SetField(go.Text,"m_Name",row),row+" / Decision 4");
            }
            return patch.Seal(scene);
        }
        public static LayerPatch BindPlayerWaterLadder(SceneYaml scene)
        {
            var go=scene.Docs.Single(d=>d.ClassId==1&&d.Field("m_Name")=="Ladder_Berth0");var patch=new LayerPatch(WaterStep,"Ladder_Berth0",go.FileId);
            var ladder=scene.ComponentsOf(go).Single(d=>d.IsScript("HiddenHarbours.World.WharfLadder"));
            patch.Edit(ladder,SetField(ladder.Text,"_id","ladder.nmc_berth0"),"the existing player ladder / registered id");return patch.Seal(scene);
        }
        public static IEnumerable<LayerPatch> WaterRetirements(SceneYaml scene)
        {
            yield return RemoveChildren(scene,"NineMileCreekWharf",name=>name=="FloatingDock"||name=="MooringCleats"||name=="Ladder_0",3,WaterStep);
        }
        public static void ApplyWaterSide(string evidence, bool apply = false)
        {
            Directory.CreateDirectory(evidence);
            if(EditorApplication.isPlaying)throw new Refusal("Leave play before the water-side patch.");
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            string text=File.ReadAllText(NineMileCreekLayerRefresh.ScenePath);var yaml=SceneYaml.Parse(text);var patches=new List<LayerPatch>();
            void Add(LayerPatch patch){patches.Add(patch);yaml=patch.Apply(yaml);}
            Add(PlaceWaterSide(yaml,false));Add(MoveWaterFleet(yaml));Add(BindPlayerWaterLadder(yaml));
            foreach(var patch in WaterRetirements(yaml).ToArray())Add(patch);
            Add(RemoveChildren(yaml,"NineMileCreekDressing",n=>n=="fuelPump_10"||n=="oilTank_11",2,WaterStep));
            Add(OpenWaterGangwayHeads(yaml));Add(PlaceWaterFuel(yaml));
            // The owner's westward crib is the final scene placement operation.
            Add(PlaceWaterSide(yaml,true));
            File.WriteAllText(evidence+"/placement-patches.yaml",string.Join("\n",patches.Select(p=>p.ToYaml())));
            File.WriteAllText(evidence+"/candidate.unity",yaml.Render());
            var repeat=PlaceWaterSide(yaml,true).Apply(yaml);repeat=MoveWaterFleet(repeat).Apply(repeat);repeat=BindPlayerWaterLadder(repeat).Apply(repeat);
            repeat=OpenWaterGangwayHeads(repeat).Apply(repeat);repeat=PlaceWaterFuel(repeat).Apply(repeat);
            if(repeat.Render()!=yaml.Render())throw new Refusal("H5's second apply changes a scene byte.");
            File.WriteAllText(evidence+"/repeat-identical.txt","The second apply changes no scene byte.\n");
            if(!apply)return;
            File.WriteAllText(NineMileCreekLayerRefresh.ScenePath,yaml.Render());
            string key=File.ReadAllText(KeyScenePath);var ids=KeyScene().PlacementIds.Concat(NineMileCreekWharfWaterSide.PlacementRows().Select(r=>r.Id)).Distinct().ToArray();
            key=Regex.Replace(key,@"(?m)^  PlacementIds:(?: \[\])?(?:\r?\n  - [^\r\n]+)*","  PlacementIds:\n"+string.Join("\n",ids.Select(id=>"  - "+id)));
            File.WriteAllText(KeyScenePath,key);AssetDatabase.Refresh();
        }
    }
}
#endif
