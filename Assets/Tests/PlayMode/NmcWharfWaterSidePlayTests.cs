#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class NmcWharfWaterSidePlayTests
    {
        const string Evidence="Evidence~/nmc-h5/phase-b";
        WharfNightStage stage;
        PlayerWalkController player;
        Rigidbody2D body;
        CircleCollider2D foot;
        HeldWalkIntents held;
        IEnvironmentService previousEnvironment;
        IWallet previousWallet;
        readonly List<GameObject> spawned=new List<GameObject>();
        readonly List<Object> assets=new List<Object>();
        PinnedWater water;
        string walkLabel="walk";

        static string ScenePath(Transform at)
        {
            var names=new List<string>();
            for(var current=at;current!=null;current=current.parent)names.Add(current.name);
            names.Reverse();return string.Join("/",names);
        }
        object WalkField(string name)=>typeof(PlayerWalkController)
            .GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(player);
        float WalkFloat(string name)=>(float)WalkField(name);
        void TraceWalk(int frame,Vector2 goal)
        {
            File.AppendAllText(Evidence+"/gangway-stall-trace.csv",string.Join(",",new[]{
                walkLabel,water.Level.ToString(CultureInfo.InvariantCulture),frame.ToString(),
                Feet.x.ToString("R",CultureInfo.InvariantCulture),Feet.y.ToString("R",CultureInfo.InvariantCulture),
                body.position.x.ToString("R",CultureInfo.InvariantCulture),body.position.y.ToString("R",CultureInfo.InvariantCulture),
                body.linearVelocity.x.ToString("R",CultureInfo.InvariantCulture),body.linearVelocity.y.ToString("R",CultureInfo.InvariantCulture),
                Vector2.Distance(Feet,goal).ToString("R",CultureInfo.InvariantCulture)})+"\n");
        }
        void DumpWalkStall(Vector2 goal,int frames)
        {
            var text=new System.Text.StringBuilder();
            Vector2 direction=(goal-Feet).normalized;
            text.AppendLine("# Gangway stall diagnosis");
            text.AppendLine("Observed "+DateTime.UtcNow.ToString("o")+"; "+walkLabel+"; frames "+frames+"; water "+water.Level);
            text.AppendLine("Feet "+Feet+"; body "+body.position+"; goal "+goal+"; velocity "+body.linearVelocity);
            text.AppendLine("Player "+ScenePath(player.transform)+"; enabled "+player.enabled+"; active "+player.gameObject.activeInHierarchy+
                "; water state "+player.WaterState+"; body type "+body.bodyType+"; simulated "+body.simulated+"; constraints "+body.constraints);
            foreach(string name in new[]{"_moveInput","_tideGatedWalk","_wadeProbeDistance","_wadeDepth","_swimLimit","_wadeSlowFactor","_swimSlowFactor","_moveSpeed"})
                text.AppendLine("Controller "+name+" = "+WalkField(name));
            var input=(Vector2)WalkField("_moveInput");
            var desired=PlayerWalkController.VelocityFor(input,WalkFloat("_moveSpeed"));
            var gated=PlayerWalkController.ApplyWaterEdge(desired,body.position,TidalWalkability.DepthNow,
                WalkFloat("_wadeProbeDistance"),WalkFloat("_wadeDepth"),WalkFloat("_swimLimit"),
                WalkFloat("_wadeSlowFactor"),WalkFloat("_swimSlowFactor"),p=>HullPresences.WithinReachNow(p,player.SwimBoardReach));
            text.AppendLine("Replayed controller water-edge gate: desired "+desired+"; gated "+gated+"; alongside hull "+HullPresences.WithinReachNow(body.position,player.SwimBoardReach));
            text.AppendLine("No public blocked/slope/edge state is exposed; the cached inputs and public pure gate above are recorded without changing the controller.");
            foreach(var sample in new[]{Feet,Feet+direction,body.position,body.position+direction*WalkFloat("_wadeProbeDistance")})
            {
                text.AppendLine("\nSample "+sample+"; ground "+GameServices.TidalTerrain?.ElevationAt(sample)+"; depth "+TidalWalkability.DepthNow(sample));
                bool hasDeck=StandableSurfaces.TryGetDeckElevationNow(sample,out float deck);
                text.AppendLine("Registered highest deck valid "+hasDeck+"; elevation "+deck);
                foreach(var surface in StandableSurfaces.Active)
                {
                    bool valid=surface.TryGetDeckElevation(sample,out float elevation);
                    text.AppendLine("Provider "+surface.Id+"; valid "+valid+"; elevation "+elevation+"; path "+(surface is Component c?ScenePath(c.transform):surface.GetType().FullName));
                }
                foreach(var brow in Object.FindObjectsByType<GangwayPlatform>())
                {
                    bool valid=brow.TryGetDeckElevation(sample,out float elevation);
                    bool landed=brow.Landing!=null&&brow.Landing.TryGetDeckElevation(sample,out _);
                    text.AppendLine("Brow "+brow.Id+"; shore "+brow.ShoreEnd+"; toe "+brow.FloatEnd+"; deck valid "+valid+"; elevation "+elevation+"; landing contains sample "+landed);
                }
            }
            float radius=foot.radius*Mathf.Max(Mathf.Abs(foot.transform.lossyScale.x),Mathf.Abs(foot.transform.lossyScale.y));
            text.AppendLine("\nFoot circle radius "+radius+"; offset "+foot.offset);
            foreach(var sample in new[]{Feet,Feet+direction})
            {
                text.AppendLine("Overlapping at "+sample);
                foreach(var collider in Physics2D.OverlapCircleAll(sample,radius).OrderBy(c=>ScenePath(c.transform)))
                    AppendCollider(text,collider,sample);
            }
            foreach(var hit in Physics2D.CircleCastAll(Feet,radius,direction,1f).OrderBy(h=>h.distance))
            {
                text.AppendLine("One-metre sweep hit distance "+hit.distance+"; normal "+hit.normal);
                AppendCollider(text,hit.collider,Feet);
            }
            text.AppendLine("\nONE BLOCKER: not inferred automatically; inspect this dump and the movement trace before any production edit.");
            File.WriteAllText(Evidence+"/gangway-stall-diagnosis.md",text.ToString());
        }
        static void AppendCollider(System.Text.StringBuilder text,Collider2D collider,Vector2 sample)
        {
            text.AppendLine(collider.GetType().Name+"; owner "+ScenePath(collider.transform)+"; enabled "+collider.enabled+
                "; active "+collider.gameObject.activeInHierarchy+"; trigger "+collider.isTrigger+"; bounds "+collider.bounds+
                "; closest "+collider.ClosestPoint(sample));
            foreach(var component in collider.GetComponents<MonoBehaviour>())
            {
                if(component==null)continue;
                string path=AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(component));
                text.AppendLine("Owner component "+component.GetType().FullName+"; script "+path+"; guid "+AssetDatabase.AssetPathToGUID(path));
            }
        }

        sealed class PinnedWater:IEnvironmentService
        {
            public float Level;
            public IEnvironmentService Original;
            public int WorldSeed=>Original.WorldSeed;
            public TideProfile ActiveTideProfile {get;set;}
            public EnvironmentSample Sample()=>Original.Sample();
            public float TideHeightAt(double at)=>Level;
            public float WaterLevelAt(double at)=>Level;
        }
        sealed class Purse:IWallet
        {
            public int Money {get;private set;}=1000;
            public void Add(int amount)=>Money+=amount;
            public bool TrySpend(int amount){if(amount>Money)return false;Money-=amount;return true;}
        }
        IEnumerator Load()
        {
            string path=System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable);
            Assert.That(path,Is.Not.Null.And.Not.Empty,"the test-run save must be armed before play");
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."))+Path.DirectorySeparatorChar;
            Assert.That(Path.GetFullPath(path).StartsWith(project,StringComparison.OrdinalIgnoreCase),Is.True,"the run's save stays in this box");
            var save=Object.FindAnyObjectByType<SaveService>();
            Assert.That(save,Is.Not.Null);
            string opened=(string)typeof(SaveService).GetProperty("SavePath",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(save);
            Assert.That(opened,Is.EqualTo(path),"the live save service actually opened the private run file");
            stage=new WharfNightStage("NineMileCreek","NmcWharfH5");
            yield return stage.Load();yield return stage.SetNight(14);
            ShellFlow.Reset();InteractionGate.Reset();
            foreach(var old in Object.FindObjectsByType<ControlSwitcher>(FindObjectsInactive.Include))old.enabled=false;
            player=Object.FindAnyObjectByType<PlayerWalkController>(FindObjectsInactive.Include);
            Assert.That(player,Is.Not.Null);player.gameObject.SetActive(true);player.enabled=true;
            body=player.GetComponent<Rigidbody2D>();foot=player.GetComponent<CircleCollider2D>();
            Assert.That(body,Is.Not.Null);Assert.That(foot,Is.Not.Null);
            held=new HeldWalkIntents();player.ConfigureWalkInput(held);
            previousEnvironment=GameServices.Environment;previousWallet=GameServices.Wallet;
            Assert.That(previousEnvironment,Is.Not.Null);
            water=new PinnedWater{Original=previousEnvironment,ActiveTideProfile=previousEnvironment.ActiveTideProfile};
            GameServices.Environment=water;Time.timeScale=4;
            Directory.CreateDirectory(Evidence);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            held?.Release();if(player!=null)player.ConfigureWalkInput(null);
            if(previousEnvironment!=null)GameServices.Environment=previousEnvironment;
            if(previousWallet!=null)GameServices.Wallet=previousWallet;
            foreach(var go in spawned)if(go!=null)Object.Destroy(go);spawned.Clear();
            foreach(var asset in assets)if(asset!=null)Object.Destroy(asset);assets.Clear();
            Time.timeScale=1;
            if(stage!=null)yield return stage.TearDown();stage=null;
        }
        GameObject New(string name,Vector2 at)
        {
            var go=new GameObject(name);go.transform.position=at;spawned.Add(go);return go;
        }
        void Stand(Vector2 at)
        {
            held.Release();body.position=at-foot.offset;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
        }
        Vector2 Feet=>body.position+foot.offset;
        (ControlSwitcher control,BoatController boat,GameObject root) Rig(WharfLadder ladder,Vector2? arrival=null,Transform dock=null,Transform landing=null)
        {
            var owner=NineMileCreekMooredFleet.LoadOwners().Single(o=>o.Id=="owner.bernard_celeste");
            var hull=owner.Boat;Assert.That(hull,Is.Not.Null);
            GameServices.Save.Current.OwnedBoats??=new List<string>();
            if(!GameServices.Save.Current.OwnedBoats.Contains(hull.Id))GameServices.Save.Current.OwnedBoats.Add(hull.Id);
            RepairLedger.MarkRepaired(GameServices.Save.Current,hull.Id);
            Vector2 at=arrival??(ladder.WorldPosition-Vector2.up*1.5f);
            var boatGo=New("H5 controlled proof dory",at);boatGo.transform.rotation=Quaternion.Euler(0,0,-90);
            var boat=boatGo.AddComponent<BoatController>();boat.SetHull(hull);
            // The boarding fixture supplies a held berth, rather than an unmanned drifting hull.
            var berthBody=boatGo.GetComponent<Rigidbody2D>();
            berthBody.constraints=RigidbodyConstraints2D.FreezeAll;boat.enabled=false;
            var input=boatGo.AddComponent<DevBoatInput>();
            var controlGo=New("H5 boarding proof",at);var control=controlGo.AddComponent<ControlSwitcher>();
            dock??=New("H5 proof dock",at).transform;
            landing??=New("H5 proof landing",ladder.WorldPosition+Vector2.up*ladder.ClimbTopScreenOffset).transform;
            control.Configure(player,boat,input,dock,3.5f,landing);
            control.ConfigureBoardingMove(true,3,.8f,.55f,.35f);
            control.ConfigureInteractVerb(false,180);control.ConfigureHelm(new Vector2(100,100),.1f);
            control.ConfigureDeckFacing(()=>270); // deck-relative on an east-heading hull: compass north, toward the wall.
            return(control,boat,controlGo);
        }
        IEnumerator FinishMove(ControlSwitcher control,BoatController boat)
        {
            float spent=0;
            while(control.IsBoardingMove&&spent<40){yield return null;spent+=Time.deltaTime;}
            yield return null;
            File.AppendAllText(Evidence+"/ladder-move-diagnostics.txt",control.Mode+"; can step="+control.CanStepAshore()+"; dock="+control.InDockZone()+"; boat="+boat.transform.position+"; player="+player.transform.position+"\n");
        }
        [UnityTest,Timeout(300000)]
        public IEnumerator AllNineRegisteredLadders_AreClimbedDownAndUp_AtBothTides()
        {
            yield return Load();
            var ladders=Object.FindObjectsByType<WharfLadder>().OrderBy(l=>l.Id).ToArray();
            Assert.That(ladders.Length,Is.EqualTo(9));
            string proof=Evidence+"/ladder-climbs.csv";File.WriteAllText(proof,"id,water,gap,down_route,up_route,final_mode\n");
            foreach(float level in new[]{-2.2f,2.2f})foreach(var ladder in ladders)
            {
                water.Level=level;Stand(ladder.WorldPosition+Vector2.up*ladder.ClimbTopScreenOffset);
                var rig=Rig(ladder);yield return null;yield return null;
                Assert.That(rig.control.BeginInteract(),Is.True,ladder.Id+" starts boarding at "+level);
                bool down=rig.control.IsLadderBoarding;float gap=rig.control.LadderGapMetres;
                for(var move=FinishMove(rig.control,rig.boat);move.MoveNext();)yield return move.Current;
                Assert.That(down,Is.True,ladder.Id+" uses its ladder down at "+level);
                Assert.That(rig.control.IsBoardingMove,Is.False,ladder.Id+" finished descending");
                Assert.That(rig.control.Mode,Is.EqualTo(ControlMode.OnDeck));
                Assert.That(rig.control.BeginInteract(),Is.True,ladder.Id+" starts ashore at "+level);
                bool up=rig.control.IsLadderBoarding;
                for(var move=FinishMove(rig.control,rig.boat);move.MoveNext();)yield return move.Current;
                Assert.That(up,Is.True,ladder.Id+" uses its ladder up at "+level);
                Assert.That(rig.control.IsBoardingMove,Is.False);Assert.That(rig.control.Mode,Is.EqualTo(ControlMode.OnFoot));
                File.AppendAllText(proof,ladder.Id+","+level.ToString(CultureInfo.InvariantCulture)+","+gap.ToString(CultureInfo.InvariantCulture)+",ladder,ladder,"+rig.control.Mode+"\n");
                Object.Destroy(rig.control.gameObject);Object.Destroy(rig.boat.gameObject);yield return null;
            }
        }
        IEnumerator WalkTo(Vector2 goal,float radius=.12f)
        {
            int frames=0;
            TraceWalk(0,goal);
            while(Vector2.Distance(Feet,goal)>radius&&frames++<1800)
            {
                held.Walk((goal-Feet).normalized);yield return new WaitForFixedUpdate();
                if(frames%100==0)TraceWalk(frames,goal);
                Assert.That(player.WaterState,Is.EqualTo(OnFootWaterState.Dry),"wet tread at "+Feet);
            }
            if(Vector2.Distance(Feet,goal)>radius+.04f)DumpWalkStall(goal,frames);
            held.Release();yield return null;
            Assert.That(Vector2.Distance(Feet,goal),Is.LessThanOrEqualTo(radius+.04f),"real walk did not reach "+goal+"; stopped at "+Feet);
        }
        [UnityTest,Timeout(300000)]
        public IEnumerator AllFourNativeGangways_AreWalkedToTheirFloats_AtBothTides()
        {
            yield return Load();
            var brows=Object.FindObjectsByType<GangwayPlatform>().OrderBy(g=>g.Id).ToArray();Assert.That(brows.Length,Is.EqualTo(4));
            string proof=Evidence+"/gangway-walks.csv";File.WriteAllText(proof,"id,water,shore_x,shore_y,toe_x,toe_y,end_x,end_y\n");
            File.WriteAllText(Evidence+"/gangway-stall-trace.csv","id,water,frame,feet_x,feet_y,body_x,body_y,velocity_x,velocity_y,remaining\n");
            foreach(float level in new[]{-2.2f,2.2f})foreach(var brow in brows)
            {
                walkLabel=brow.Id;
                water.Level=level;Vector2 start=brow.ShoreEnd+Vector2.up,goal=brow.FloatEnd-Vector2.up;
                Stand(start);yield return null;yield return null;
                for(var walk=WalkTo(goal);walk.MoveNext();)yield return walk.Current;
                Assert.That(brow.Landing.TryGetDeckElevation(Feet,out _),Is.True,brow.Id+" ends on its real float");
                File.AppendAllText(proof,string.Join(",",new[]{brow.Id,level.ToString(CultureInfo.InvariantCulture),brow.ShoreEnd.x.ToString(CultureInfo.InvariantCulture),brow.ShoreEnd.y.ToString(CultureInfo.InvariantCulture),brow.FloatEnd.x.ToString(CultureInfo.InvariantCulture),brow.FloatEnd.y.ToString(CultureInfo.InvariantCulture),Feet.x.ToString(CultureInfo.InvariantCulture),Feet.y.ToString(CultureInfo.InvariantCulture)})+"\n");
            }
        }
        IEnumerator Purchase(string grade)
        {
            yield return Load();water.Level=2.2f;
            var purse=new Purse();GameServices.Wallet=purse;
            var pump=Object.FindObjectsByType<FuelPump>().Single(p=>p.Id=="fixture.nmc.wharf_pump_"+grade);
            Assert.That(pump.isActiveAndEnabled,Is.True);Stand(pump.WorldPosition+Vector2.up*2.5f);yield return null;
            for(var walk=WalkTo(pump.WorldPosition+Vector2.up*.7f);walk.MoveNext();)yield return walk.Current;
            var hands=player.GetComponent<CarryHands>();Assert.That(hands,Is.Not.Null);
            var def=ScriptableObject.CreateInstance<FuelContainerDef>();assets.Add(def);
            def.Id="fuelstore.h5_proof_"+grade;def.Grade=grade;def.Carriable=true;def.Facings=8;def.FillFractions=new[]{0f,.25f,.5f,.75f,1f};def.CapacityLitres=20;
            var go=New("H5 proof empty can",player.transform.position);var presenter=go.AddComponent<FuelLevelPresenter>();presenter.Container=def;presenter.Fill=0;
            go.AddComponent<YSortSprite>();var can=go.AddComponent<CarriableFuelContainer>();yield return null;
            Assert.That(hands.TryPickUp(can),Is.EqualTo(CarryRefusal.None));int before=purse.Money;
            Assert.That(pump.TryFill(),Is.True,pump.Id+" fills the can through its existing wallet provider");
            Assert.That(pump.LastLitresDelivered,Is.GreaterThan(0));Assert.That(before-purse.Money,Is.GreaterThan(0));
            File.WriteAllText(Evidence+"/purchase-"+grade+".csv","id,grade,litres,paid,water\n"+pump.Id+","+grade+","+pump.LastLitresDelivered.ToString(CultureInfo.InvariantCulture)+","+(before-purse.Money)+",2.2\n");
        }
        [UnityTest,Timeout(180000)]public IEnumerator TheExistingWharfGasPedestal_SellsIntoTheHeldCan(){for(var test=Purchase("gas");test.MoveNext();)yield return test.Current;}
        [UnityTest,Timeout(180000)]public IEnumerator TheExistingWharfDieselPedestal_SellsIntoTheHeldCan(){for(var test=Purchase("diesel");test.MoveNext();)yield return test.Current;}

        [UnityTest,Timeout(180000)]
        public IEnumerator BerthZero_DocksAndStepsOff_AtTheUnchangedSceneAnchors()
        {
            yield return Load();water.Level=2.2f;
            var dock=GameObject.Find("NineMileCreekDockZone").transform;
            var landing=GameObject.Find("NineMileCreekDisembark").transform;
            var arrival=GameObject.Find("NineMileCreekArrival").transform;
            Assert.That((Vector2)dock.position,Is.EqualTo(new Vector2(86.2f,85.5f)));
            Assert.That((Vector2)landing.position,Is.EqualTo(new Vector2(86.2f,87.35f)));
            Assert.That((Vector2)arrival.position,Is.EqualTo(new Vector2(86.2f,83.1f)));
            var ladder=Object.FindObjectsByType<WharfLadder>().Single(l=>l.Id=="ladder.nmc_berth0");
            Stand(landing.position);var rig=Rig(ladder,arrival.position,dock,landing);
            // Arrival is the design hull's centre; a narrower hull lies further in on the same fender line.
            var berthBody=rig.boat.GetComponent<Rigidbody2D>();
            berthBody.position=new Vector2(arrival.position.x,dock.position.y-HullPresence.HalfBeamOf(rig.boat.Hull));
            Physics2D.SyncTransforms();yield return null;yield return null;
            File.WriteAllText(Evidence+"/berth-zero-premise.txt","Hull "+rig.boat.Hull.Id+"; actual half-beam "+HullPresence.HalfBeamOf(rig.boat.Hull)+
                "; centre "+berthBody.position+"; fender "+dock.position+"; player "+player.transform.position+
                "; within board reach "+rig.control.WithinBoardReach()+"; boardable "+rig.control.BoardableNow()+"; dock "+rig.control.InDockZone()+"\n");
            Assert.That(rig.control.InDockZone(),Is.True);Assert.That(rig.control.BeginInteract(),Is.True);
            for(var move=FinishMove(rig.control,rig.boat);move.MoveNext();)yield return move.Current;
            Assert.That(rig.control.Mode,Is.EqualTo(ControlMode.OnDeck));
            Assert.That(rig.control.BeginInteract(),Is.True);
            for(var move=FinishMove(rig.control,rig.boat);move.MoveNext();)yield return move.Current;
            Assert.That(rig.control.Mode,Is.EqualTo(ControlMode.OnFoot));
            Assert.That(Vector2.Distance(player.transform.position,landing.position),Is.LessThan(.15f));
            File.WriteAllText(Evidence+"/berth-zero.txt","Dock (86.2,85.5); arrival (86.2,83.1); actual stepped-off position "+player.transform.position+"; native ladder "+ladder.Id+"; spring high 2.2.\n");
        }

        [Serializable] sealed class QuayEnvelope
        {
            public string id="",category="";
            public float x0=0,y0=0,x1=0,y1=0;
        }
        static object Member(object value,string key)=>((Dictionary<string,object>)value)[key];
        static Vector2 Point(object value)
        {
            var xy=(List<object>)value;
            return new Vector2(Convert.ToSingle(xy[0],CultureInfo.InvariantCulture),Convert.ToSingle(xy[1],CultureInfo.InvariantCulture));
        }
        static Vector2 Curve(Vector2[] points,int k,float t)
        {
            var a=points[Mathf.Max(0,k-1)];var b=points[k];var c=points[k+1];var d=points[Mathf.Min(points.Length-1,k+2)];
            return .5f*(2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
        }
        static Rect HullBounds(HullFootprint footprint)
        {
            var corners=Enumerable.Range(0,4).Select(footprint.Corner).ToArray();
            return Rect.MinMaxRect(corners.Min(p=>p.x),corners.Min(p=>p.y),corners.Max(p=>p.x),corners.Max(p=>p.y));
        }
        static float RectDistance(Rect a,Rect b)=>new Vector2(
            Mathf.Max(b.xMin-a.xMax,a.xMin-b.xMax,0),Mathf.Max(b.yMin-a.yMax,a.yMin-b.yMax,0)).magnitude;
        static QuayEnvelope[] NativeQuays()
        {
            var result=new List<QuayEnvelope>();
            foreach(Dictionary<string,object> part in (List<object>)Member(NineMileCreekWharfWaterSide.ReadToday(),"parts"))
            {
                if(part.TryGetValue("floating",out object moving)&&moving is bool floating&&floating)continue;
                if(part.TryGetValue("gangway",out object gangway)&&gangway!=null)continue;
                if(!part.TryGetValue("walkPolygon",out object walk)||!(walk is List<object> polygon)||polygon.Count<3)continue;
                if(!part.TryGetValue("bounds",out object bounds))continue;
                Vector2 at=Point(part["at"]),lo=Point(Member(bounds,"min")),hi=Point(Member(bounds,"max"));
                result.Add(new QuayEnvelope{id="quay."+part["key"],category="quay",x0=at.x+lo.x,y0=at.y+lo.y,x1=at.x+hi.x,y1=at.y+hi.y});
            }
            return result.ToArray();
        }

        [UnityTest,Timeout(300000)]
        public IEnumerator TheLiveDesignHull_TracesEveryRegisteredTrack_WithOneMetreClearance()
        {
            yield return Load();water.Level=2.2f;
            var owner=NineMileCreekMooredFleet.LoadOwners().Single(o=>o.Id=="owner.gallant_marie");
            var go=New("H5 guided design hull",new Vector2(137,-45));
            var controller=go.AddComponent<BoatController>();controller.SetHull(owner.Boat);controller.enabled=false;
            var hull=go.GetComponent<HullPresence>();Assert.That(hull,Is.Not.Null);
            Assert.That(hull.LengthMetres,Is.EqualTo(12.9f).Within(.0001f));
            Assert.That(2*hull.HalfBeamMetres,Is.EqualTo(4.8f).Within(.0001f));
            var motion=go.GetComponent<Rigidbody2D>();motion.bodyType=RigidbodyType2D.Kinematic;motion.rotation=0;
            var floats=Object.FindObjectsByType<FloatingPlatform>();Assert.That(floats.Length,Is.EqualTo(4));
            var residents=Object.FindObjectsByType<MooredBoat>();Assert.That(residents.Length,Is.EqualTo(8));
            foreach(var resident in residents)Assert.That(resident.GetComponent<HullPresence>(),Is.Not.Null,resident.Owner.Id+" publishes its actual hull");
            var quay=NativeQuays();Assert.That(quay.Length,Is.GreaterThan(0));
            var register=NineMileCreekWharfWaterSide.Register();
            var tracks=(Dictionary<string,object>)Member(register,"tracks");
            var ownFinger=((List<object>)Member(register,"fingers"))[0];
            var limits=(List<object>)Member(ownFinger,"y");float south=Convert.ToSingle(limits[0]),north=Convert.ToSingle(limits[1]);
            string csv=Evidence+"/s12-live-design-hull.csv";
            File.WriteAllText(csv,"track,segment,t,x,y,half_length,half_beam,own_exempt,minimum_required_clearance\n");
            var summary=new System.Text.StringBuilder("Guided kinematic Rigidbody2D traverse of the real design BoatController and its installed HullPresence, north heading throughout under the standing envelope ruling. No production route controller added. Quay envelopes use the full native fixed-module bounds including submerged footings; float and eight resident hull bounds are sampled live. Full curve calculation is recorded separately.\n");
            foreach(string name in new[]{"in","outAstern","out"})
            {
                var points=((List<object>)tracks[name]).Select(Point).ToArray();var minima=new Dictionary<string,float>();int samples=0;
                for(int k=0;k<points.Length-1;k++)
                {
                    int count=Mathf.Max(32,Mathf.CeilToInt(Vector2.Distance(points[k],points[k+1])/.02f));
                    for(int i=0;i<=count;i++)
                    {
                        float t=(float)i/count;Vector2 requested=Curve(points,k,t);
                        motion.MovePosition(requested);yield return new WaitForFixedUpdate();
                        Assert.That(Vector2.Distance(motion.position,requested),Is.LessThan(.01f),"the real body follows the registered curve");
                        Rect actual=HullBounds(hull.Footprint);bool exempt=actual.yMax>=south&&actual.yMin<=north;float required=float.PositiveInfinity;
                        void Check(string group,float gap,bool waived=false)
                        {
                            if(!minima.ContainsKey(group)||gap<minima[group])minima[group]=gap;
                            if(!waived){required=Mathf.Min(required,gap);Assert.That(gap,Is.GreaterThanOrEqualTo(1f),name+" "+group+" at "+motion.position);}
                        }
                        foreach(var platform in floats)
                        {
                            bool own=platform.Id=="float.nmc_finger_a";
                            Check(own?(exempt?"own_EXEMPT":"own_outside"):"other_float",RectDistance(actual,platform.Footprint),own&&exempt);
                        }
                        foreach(var resident in residents)Check("other_hull",RectDistance(actual,HullBounds(resident.GetComponent<HullPresence>().Footprint)));
                        foreach(var box in quay)Check("quay",RectDistance(actual,Rect.MinMaxRect(box.x0,box.y0,box.x1,box.y1)));
                        if(samples++%10==0)File.AppendAllText(csv,string.Join(",",new[]{name,k.ToString(),t.ToString("R",CultureInfo.InvariantCulture),motion.position.x.ToString("R",CultureInfo.InvariantCulture),motion.position.y.ToString("R",CultureInfo.InvariantCulture),hull.Footprint.HalfLength.ToString("R",CultureInfo.InvariantCulture),hull.HalfBeamMetres.ToString("R",CultureInfo.InvariantCulture),exempt.ToString(),required.ToString("R",CultureInfo.InvariantCulture)})+"\n");
                    }
                }
                summary.AppendLine(name+"; physics samples "+samples+"; "+string.Join("; ",minima.Select(p=>p.Key+"="+p.Value.ToString("R",CultureInfo.InvariantCulture))));
            }
            File.WriteAllText(Evidence+"/s12-live-design-hull.txt",summary.ToString());
        }

        [Serializable] sealed class PlateMetadata
        {
            public string state,view,utc,graphics,craneSprite;
            public double time;
            public float hour,water,cameraSize,profileAmplitude;
            public int worldSeed;
            public Vector3 camera;
            public Color tint;
            public int width,height;
            public string[] floatDecks;
        }
        [Serializable] sealed class PerformanceMetadata
        {
            public int frames,textureCount;
            public double meanMs,medianMs,p95Ms,baselineMeanMs,deltaMeanMs;
            public long textureBytes,baselineTextureBytes,deltaTextureBytes;
            public float cameraSize;
            public Vector3 camera;
            public string graphics,utc;
        }
        const string Plates=Evidence+"/s12-plates";
        IEnumerator FramePlate(Vector2 at,float size)
        {
            Time.timeScale=1;
            if(stage.Camera!=null&&stage.Camera.targetTexture!=null)
            {
                var old=stage.Camera.targetTexture;stage.Camera.targetTexture=null;old.Release();Object.DestroyImmediate(old);
            }
            var follow=Object.FindAnyObjectByType<CameraFollow>();follow.enabled=true;
            Camera.main.aspect=1600f/900;
            yield return stage.FrameOn(at);
            Time.timeScale=1;follow.enabled=false;
            if(size!=14.0625f)foreach(var pixels in stage.Camera.GetComponents<Behaviour>())
                if(pixels.GetType().Name=="PixelPerfectCamera")pixels.enabled=false;
            stage.Camera.transform.position=new Vector3(at.x,at.y,stage.Camera.transform.position.z);
            stage.Camera.orthographicSize=size;
            for(int i=0;i<8;i++)yield return null;
            Time.timeScale=0;
            for(int i=0;i<2;i++)yield return null;
        }
        void WritePlate(string name)
        {
            Directory.CreateDirectory(Plates);
            var texture=new Texture2D(stage.Width,stage.Height,TextureFormat.RGBA32,false);
            texture.LoadRawTextureData(stage.Capture());texture.Apply();
            File.WriteAllBytes(Plates+"/"+name+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
            var crane=Object.FindAnyObjectByType<NmcWharfCraneVisual>();
            var metadata=new PlateMetadata{state=walkLabel,view=name,utc=DateTime.UtcNow.ToString("o"),graphics=SystemInfo.graphicsDeviceType.ToString(),
                time=GameServices.Clock.TotalSeconds,hour=GameServices.Clock.HourOfDay,water=GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds),
                camera=stage.Camera.transform.position,cameraSize=stage.Camera.orthographicSize,tint=Shader.GetGlobalColor("_DayNightTint"),width=stage.Width,height=stage.Height,
                worldSeed=GameServices.Environment.WorldSeed,profileAmplitude=GameServices.Environment.ActiveTideProfile.Amplitude,
                craneSprite=crane!=null?crane.GetComponent<SpriteRenderer>().sprite.name:"missing",
                floatDecks=Object.FindObjectsByType<FloatingPlatform>().OrderBy(f=>f.Id).Select(f=>f.Id+"="+f.DeckElevationNow().ToString("R",CultureInfo.InvariantCulture)).ToArray()};
            File.WriteAllText(Plates+"/"+name+".json",JsonUtility.ToJson(metadata,true));
            File.AppendAllText(Plates+"/INDEX.md","- "+name+".png — hour "+metadata.hour.ToString("F3",CultureInfo.InvariantCulture)+"; water "+metadata.water.ToString("F5",CultureInfo.InvariantCulture)+" m; camera "+metadata.camera+"; size "+metadata.cameraSize+"; crane "+metadata.craneSprite+"\n");
        }
        IEnumerator MeasureMainFraming()
        {
            yield return stage.SetNight(14);
            yield return FramePlate(new Vector2(120,80),14.0625f);
            Time.timeScale=1;
            for(int i=0;i<60;i++)yield return null;
            var elapsed=System.Diagnostics.Stopwatch.StartNew();double previous=elapsed.Elapsed.TotalMilliseconds;
            var frames=new List<double>();
            for(int i=0;i<300;i++)
            {
                yield return null;double now=elapsed.Elapsed.TotalMilliseconds;frames.Add(now-previous);previous=now;
            }
            var textures=Resources.FindObjectsOfTypeAll<Texture2D>();long bytes=textures.Sum(t=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));frames.Sort();
            var result=new PerformanceMetadata{frames=300,meanMs=frames.Average(),medianMs=frames[frames.Count/2],p95Ms=frames[(int)(frames.Count*.95)],
                textureBytes=bytes,textureCount=textures.Length,baselineTextureBytes=1081563601,deltaTextureBytes=bytes-1081563601,
                baselineMeanMs=16.803906,deltaMeanMs=frames.Average()-16.803906,camera=stage.Camera.transform.position,cameraSize=stage.Camera.orthographicSize,
                graphics=SystemInfo.graphicsDeviceType.ToString(),utc=DateTime.UtcNow.ToString("o")};
            File.WriteAllText(Evidence+"/s12-performance.json",JsonUtility.ToJson(result,true));
            Time.timeScale=0;walkLabel="baseline-day14";WritePlate("performance-main-framing");
        }
        IEnumerator CaptureWaterState(bool night,bool high,bool performance)
        {
            yield return Load();
            var plateEnvironment=Object.FindObjectsByType<HiddenHarbours.Environment.EnvironmentService>()
                .Single(e=>e.gameObject.scene.name=="NineMileCreek");
            File.WriteAllText(Evidence+"/s12-plate-environment.txt","Previously published amplitude "+previousEnvironment.ActiveTideProfile.Amplitude+
                "; selected scene "+plateEnvironment.gameObject.scene.name+"; scene amplitude "+plateEnvironment.ActiveTideProfile.Amplitude+"\n");
            GameServices.Environment=plateEnvironment;Time.timeScale=1;
            Directory.CreateDirectory(Plates);
            if(performance)yield return MeasureMainFraming();
            Time.timeScale=1;float requestedHour=night?2:14;yield return stage.SetNight(requestedHour);
            double chosen=0;float best=high?float.NegativeInfinity:float.PositiveInfinity;
            for(int day=0;day<60;day++)for(int minute=-300;minute<=(night?174:120);minute+=6)
            {
                double at=(day+(requestedHour+minute/60.0)/24.0)*GameServices.Config.SecondsPerDay;
                if(at<0)continue;
                float level=((IEnvironmentService)plateEnvironment).WaterLevelAt(at);
                if(high?level>best:level<best){best=level;chosen=at;}
            }
            GameServices.Clock.TimeScale=1;GameServices.Clock.SeekTo(chosen);Time.timeScale=1;
            Color last=Shader.GetGlobalColor("_DayNightTint");int still=0,settled=0;
            for(;settled<600&&still<8;settled++)
            {
                yield return null;Color now=Shader.GetGlobalColor("_DayNightTint");
                float delta=Mathf.Abs(now.r-last.r)+Mathf.Abs(now.g-last.g)+Mathf.Abs(now.b-last.b);still=delta<1e-4f?still+1:0;last=now;
            }
            Assert.That(settled,Is.LessThan(600));GameServices.Clock.TimeScale=0;
            yield return new WaitForSecondsRealtime(1.2f);
            walkLabel=(night?"night":"day")+"-"+(high?"spring-high":"low");
            float actual=GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds);
            Assert.That(high?actual:-actual,Is.GreaterThan(2f),"the real tide reaches the requested end of its range");
            foreach(var view in new[]{("working",new Vector2(63,76),16f),("fleet",new Vector2(128,72),23f),("recreation",new Vector2(181,76),16f),("overview",new Vector2(124,40),65f)})
            {
                yield return FramePlate(view.Item2,view.Item3);WritePlate(walkLabel+"-"+view.Item1);
            }
        }
        [UnityTest,Timeout(180000)]public IEnumerator H5Plate_DayLow()
        {
            WharfNightStage.RequireAGraphicsDevice();
            for(var capture=CaptureWaterState(false,false,true);capture.MoveNext();)yield return capture.Current;
        }
        [UnityTest,Timeout(180000)]public IEnumerator H5Plate_DaySpringHigh()
        {
            WharfNightStage.RequireAGraphicsDevice();
            for(var capture=CaptureWaterState(false,true,false);capture.MoveNext();)yield return capture.Current;
        }
        [UnityTest,Timeout(180000)]public IEnumerator H5Plate_NightLow()
        {
            WharfNightStage.RequireAGraphicsDevice();
            for(var capture=CaptureWaterState(true,false,false);capture.MoveNext();)yield return capture.Current;
        }
        [UnityTest,Timeout(180000)]public IEnumerator H5Plate_NightSpringHigh()
        {
            WharfNightStage.RequireAGraphicsDevice();
            for(var capture=CaptureWaterState(true,true,false);capture.MoveNext();)yield return capture.Current;
        }
    }
}
#endif
