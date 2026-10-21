using System;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    [TestFixture("350", 2651, 14)]
    [TestFixture("2500", 2757, 15)]
    public class ModernTruckKitProbeTests
    {
        readonly string number;
        readonly int faces, ramps;
        public ModernTruckKitProbeTests(string number, int faces, int ramps)
        { this.number = number; this.faces = faces; this.ramps = ramps; }
        string Key => "modern" + number;
        string Global => "ModernTruck" + number;
        string Rig => "docs/art/rigs/" + Key + "-kit/" + Key + ".rig.js";
        VehicleRigFleet.Vehicle Truck => VehicleRigFleet.Vehicles.Single(v => v.Key == Key);
        string Full(string path) => Path.Combine(RigCatalog.RepoRoot, path);
        IRigScriptHost Host()
        {
            var host = RigScriptHostFactory.Create();
            host.Execute(RigMeshExtractor.WidenExportedLiteral(File.ReadAllText(Full(Rig)), Global,
                new[] { "MATS", "build", "makeMats" }, Rig));
            host.Execute("var T=" + Global + @";
                function faces(o){return T.build(T.resolve(o||{}));}
                function used(o){return Array.from(new Set(faces(o).map(f=>f.mat)));}
                function bound(c,hi){var v=faces({}).flatMap(f=>f.v.map(p=>p[c]));return hi?Math.max(...v):Math.min(...v);}
                function hingeError(pose,pivot,vertical,deg){
                  var a=faces({}),b=faces(pose),count=0,worst=0,r=deg*Math.PI/180;
                  for(var i=0;i<a.length;i++)for(var j=0;j<a[i].v.length;j++){
                    var p=a[i].v[j],q=b[i].v[j];
                    if(Math.max(...p.map((v,k)=>Math.abs(v-q[k])))<1e-9)continue;
                    count++;
                    var u=vertical?0:1,v=vertical?1:2;
                    var x=p[u]-pivot[u],y=p[v]-pivot[v],want=p.slice();
                    want[u]=pivot[u]+x*Math.cos(r)-y*Math.sin(r);
                    want[v]=pivot[v]+x*Math.sin(r)+y*Math.cos(r);
                    worst=Math.max(worst,...want.map((n,k)=>Math.abs(n-q[k])));
                  }
                  if(count===0)throw Error('No moving vertices');
                  return worst;
                }");
            return host;
        }

        [Test]
        public void MatsIsReconstructedAndAllBakeSymbolsExecute()
        {
            Assert.That(RigMeshSymbols.IsReconstructed(Rig, "MATS"), Is.True);
            using var host = Host();
            Assert.That(host.EvaluateBool("['KEY','GAIN','BIAS','LN','build','MATS'].every(k=>typeof T[k]!=='undefined')"), Is.True);
        }

        [Test]
        public void DayAndNightFitTheShaderAndOnlyGlowIsUnpaintedByDay()
        {
            using var host = Host();
            Assert.That(HullMeshDef.HullRampSlots, Is.EqualTo(16));
            Assert.That(host.EvaluateNumber("faces({}).length"), Is.EqualTo(faces));
            Assert.That(host.EvaluateNumber("used({}).length"), Is.EqualTo(ramps));
            Assert.That(host.EvaluateNumber("used({night:true}).length"), Is.EqualTo(ramps));
            Assert.That(ramps, Is.LessThanOrEqualTo(HullMeshDef.HullRampSlots));
            Assert.That(host.EvaluateNumber("Object.keys(T.makeMats(T.resolve({}))).length"), Is.EqualTo(ramps + 1));
            Assert.That(host.EvaluateString("Object.keys(T.makeMats(T.resolve({}))).filter(k=>!used({}).includes(k)).sort().join(',')"), Is.EqualTo("glow"));
            Assert.That(host.EvaluateNumber("Object.keys(T.MATS).length"), Is.EqualTo(ramps));
            Assert.That(host.EvaluateString("Object.keys(T.MATS)[0]"), Is.EqualTo("paint"));
        }

        [Test]
        public void ColliderSitsInsideTheRestMeshWithinHalfOfOneMillimetre()
        {
            var sidecar = "docs/art/rigs/gameplay/vehicles/" + Key + ".rig.gameplay.json";
            var f = VehicleSidecarFacts.Read(File.ReadAllText(Full(sidecar)), sidecar);
            Assert.That(f.Errors, Is.Empty);
            Assert.That(f.HasCollider, Is.True);
            using var host = Host();
            double[] lo = { f.ColliderMin.x, f.ColliderMin.y, f.ColliderMin.z };
            double[] hi = { f.ColliderMax.x, f.ColliderMax.y, f.ColliderMax.z };
            for (int c = 0; c < 3; c++)
            {
                Assert.That(lo[c], Is.GreaterThanOrEqualTo(host.EvaluateNumber($"bound({c},false)") - 0.0005));
                Assert.That(hi[c], Is.LessThanOrEqualTo(host.EvaluateNumber($"bound({c},true)") + 0.0005));
            }
        }

        [Test]
        public void DeclaredHingesTurnEveryMovingVertexAroundTheirOwnPins()
        {
            using var host = Host();
            foreach (var axis in Truck.Axes.Where(a => a.HingeAxis != VehicleHingeAxis.None))
            {
                string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);
                string pivot = $"[{F(axis.Pivot.x)},{F(axis.Pivot.y)},{F(axis.Pivot.z)}]";
                string vertical = axis.HingeAxis == VehicleHingeAxis.Vertical ? "true" : "false";
                Assert.That(host.EvaluateNumber($"hingeError({axis.Probe},{pivot},{vertical},{F(axis.SweepDegrees)})"), Is.LessThan(0.000001), axis.Slot);
            }
        }
    }
}
