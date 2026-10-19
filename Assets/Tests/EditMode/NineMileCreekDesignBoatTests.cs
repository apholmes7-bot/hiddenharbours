using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using NUnit.Framework;
using static HiddenHarbours.Tests.NineMileCreekGroundImportTests;

namespace HiddenHarbours.Tests
{
    /// <summary>N4 on the third issue. Pure file reads bind the hull, its visual/mesh GUID chain and tide
    /// without an AssetDatabase or scene. Whole bilinear cells bound the continuous swept rectangle.
    /// This is a depth proof, not a collision, manoeuvring dynamics or imported GPU texture proof.</summary>
    public class NineMileCreekDesignBoatTests
    {
        internal static readonly double[][][] Track = {
            new[] { P(137,-45),P(137,-10),P(136.2,5),P(131,16.5),P(121,23.5),P(108,27),P(98.5,31.5),P(94.5,38),P(96.5,44),P(100.8,47.6),P(104.6,50.6),P(106.26,54.02) },
            new[] { P(106.26,54.02),P(106.1,46),P(106,38.5) },
            new[] { P(106,38.5),P(108.2,31.5),P(115,25.5),P(126,21),P(133.5,13),P(136.6,2),P(137,-10),P(137,-45) }
        };
        // TRACK and mouth are transcribed unchanged from third-issue nmcWharf.js.
        // Its 12.8 m length is an OBSOLETE PREMISE: the live CapeIslander.asset owns length and draught.
        const double ObsoleteSourceLength = 12.8;
        double length, draught, halfBeam, low;
        TerrainPlanPng.Image image;
        [OneTimeSetUp]
        public void ReadLiveBoatAndTide()
        {
            string hull = Read("Assets/_Project/Data/Boats/CapeIslander.asset");
            length = Number(hull, "LengthMeters"); draught = Number(hull, "DraughtMeters");
            string visual = Read(Resolve(hull, "Visual"));
            string mesh = Read(Resolve(visual, "HullMesh"));
            double authored = Number(mesh, "WatertightHalfBeamMeters");
            halfBeam = authored > 0 ? authored : length * HiddenHarbours.Boats.HullPresence.BeamFraction / 2;
            string region = Read("Assets/_Project/Data/Regions/NineMileCreek.asset");
            low = Number(region, "TideMeanLevel") - Number(region, "TideAmplitude");
            image = TerrainPlanPng.ReadFile(Path.Combine(Root, NineMileCreekGround.HeightPath));
        }
        static string Resolve(string text, string field)
        {
            string guid = Regex.Match(text, @"(?m)^  " + field + @": \{fileID: [-\d]+, guid: (\w+)").Groups[1].Value;
            Assert.That(guid.Length, Is.EqualTo(32));
            foreach (string meta in Directory.EnumerateFiles(Path.Combine(Root, "Assets/_Project/Data/Boats"), "*.meta", SearchOption.AllDirectories))
                if (File.ReadAllText(meta).Contains("guid: " + guid)) return meta.Substring(0, meta.Length - 5);
            throw new FileNotFoundException("Boat reference " + guid);
        }
        [Test]
        public void TheDeclaredNumbersAreHers()
        {
            Assert.That(length, Is.EqualTo(12.9)); Assert.That(length, Is.Not.EqualTo(ObsoleteSourceLength));
            Assert.That(draught, Is.EqualTo(1.4)); Assert.That(low, Is.EqualTo(-2.2));
            Assert.That(halfBeam, Is.EqualTo(2.4));
            Assert.That(low - draught - .5, Is.EqualTo(-4.1).Within(1e-10));
        }

        [Test]
        public void TheCapeIslanderFloatsHerWholeTrackAtSpringLow()
        {
            double radius = Math.Sqrt(length * length / 4 + halfBeam * halfBeam), worst = -6;
            var cells = new HashSet<int>();
            foreach (var points in Track) for (int s = 0; s < points.Length - 1; s++)
            {
                var a = points[Math.Max(0, s - 1)]; var b = points[s]; var c = points[s + 1]; var d = points[Math.Min(points.Length - 1, s + 2)];
                var q1 = Mul(Sub(c,a), .5); var q2 = Mul(Add(Sub(Mul(a,2),Mul(b,5)),Sub(Mul(c,4),d)), .5);
                var q3 = Mul(Add(Sub(Mul(b,3),a),Sub(d,Mul(c,3))), .5);
                // Cubic derivative bounds on [0,1], triangle inequality. 512 intervals are finite,
                // but each is enclosed continuously, including translation and tangent rotation.
                double speed = Norm(q1) + 2 * Norm(q2) + 3 * Norm(q3), accel = 2 * Norm(q2) + 6 * Norm(q3);
                const int steps = 512; double dt = .5 / steps;
                for (int j = 0; j < steps; j++)
                {
                    double t = (j + .5) / steps;
                    var pos = Add(b, Add(Mul(q1,t),Add(Mul(q2,t*t),Mul(q3,t*t*t))));
                    var vel = Add(q1,Add(Mul(q2,2*t),Mul(q3,3*t*t)));
                    double v = Norm(vel), lower = v - accel * dt;
                    Assert.That(lower, Is.GreaterThan(0), "A tangent reversal needs a different enclosure.");
                    double margin = speed * dt + radius * accel * dt / lower + 1e-5;
                    var u = Mul(vel,1/v); var n = P(-u[1],u[0]);
                    double reach = radius + margin + 1;
                    int x0 = Col(pos[0]-reach), x1 = Col(pos[0]+reach), y0 = Row(pos[1]+reach), y1 = Row(pos[1]-reach);
                    for (int r=y0;r<=y1;r++) for(int col=x0;col<=x1;col++)
                    {
                        var delta = P(-380+(col+1)*.5-pos[0],280-(r+1)*.5-pos[1]);
                        // Every .5 m bilinear cell lies within .25*(|ux|+|uy|) of its centre on either axis.
                        double padding = margin + .25*(Math.Abs(u[0])+Math.Abs(u[1]));
                        if (Math.Abs(Dot(delta,u))<=length/2+padding && Math.Abs(Dot(delta,n))<=halfBeam+padding) cells.Add(r*1520+col);
                    }
                }
            }
            foreach(int cell in cells) worst=Math.Max(worst,CellMax(cell%1520,cell/1520));
            TestContext.WriteLine("Continuous swept hull: " + cells.Count + " cells; bed upper bound " + worst + "; keel clearance lower bound " + (low-draught-worst));
            Assert.That(worst, Is.LessThanOrEqualTo(Math.Min(-4.1, low-draught-.5)));
        }

        [Test]
        public void ThereAreThirtyMetresOfWaterSouthOfTheFingers()
        {
            // The full 60 by 30 m rectangle, not merely 241 sampled north/south chords.
            double worst=-6;
            for(int r=Row(43.9);r<=Row(13.9);r++) for(int c=Col(96);c<=Col(156);c++) worst=Math.Max(worst,CellMax(c,r));
            TestContext.WriteLine("Continuous turning rectangle [96,156] x [13.9,43.9], bed upper bound " + worst);
            Assert.That(worst, Is.LessThanOrEqualTo(Math.Min(-4.1,low-draught-.5)));
        }

        [Test]
        public void TheMouthIsNavigableAtSpringLow()
        {
            // Source mouth [116,2] -> [158,-6]. Split a central 22 m chord at EVERY texel-centre
            // line. Within each bilinear cell the bed along this oblique line is a quadratic:
            // endpoints plus its stationary point give the continuous maximum (no sample gaps).
            var a=P(116,2); var u=Mul(P(42,-8),1/Math.Sqrt(42*42+64));
            var cuts=new List<double>{10.3,32.3};
            for(int c=0;c<1520;c++) {double t=(-379.75+c*.5-a[0])/u[0];if(t>10.3&&t<32.3)cuts.Add(t);}
            for(int r=0;r<1120;r++) {double t=(279.75-r*.5-a[1])/u[1];if(t>10.3&&t<32.3)cuts.Add(t);}
            cuts.Sort(); double worst=-6;
            for(int i=1;i<cuts.Count;i++)
            {
                double left=Bed(Add(a,Mul(u,cuts[i-1]))),right=Bed(Add(a,Mul(u,cuts[i])));
                double mid=Bed(Add(a,Mul(u,(cuts[i-1]+cuts[i])/2)));
                double qa=2*(right+left-2*mid),qb=right-left-qa;
                worst=Math.Max(worst,Math.Max(left,right));
                if(qa<0) {double t=-qb/(2*qa);if(t>0&&t<1)worst=Math.Max(worst,qa*t*t+qb*t+left);}
            }
            TestContext.WriteLine("Continuous 22 m mouth chord, bed upper bound " + worst);
            Assert.That(worst,Is.LessThanOrEqualTo(Math.Min(-4.1,low-draught-.5)));
        }
        static int Col(double x)=>(int)Math.Floor((x+380)*2-.5);
        static int Row(double y)=>(int)Math.Floor((280-y)*2-.5);
        double Bed(double[] p)
        {
            int c=Col(p[0]),r=Row(p[1]),k=r*1520+c;
            double x=(p[0]+380)*2-.5-c,y=(280-p[1])*2-.5-r;
            double code=(image.Samples[k]*(1-x)+image.Samples[k+1]*x)*(1-y)
                +(image.Samples[k+1520]*(1-x)+image.Samples[k+1521]*x)*y;
            return -6+12*code/65535;
        }
        double CellMax(int c,int r)
        {
            Assert.That(c,Is.InRange(0,1518));Assert.That(r,Is.InRange(0,1118));
            int k=r*1520+c;return -6+12d*Math.Max(Math.Max(image.Samples[k],image.Samples[k+1]),Math.Max(image.Samples[k+1520],image.Samples[k+1521]))/65535;
        }
        static double[] P(double x,double y)=>new[]{x,y};
        static double[] Add(double[]a,double[]b)=>P(a[0]+b[0],a[1]+b[1]);
        static double[] Sub(double[]a,double[]b)=>P(a[0]-b[0],a[1]-b[1]);
        static double[] Mul(double[]a,double s)=>P(a[0]*s,a[1]*s);
        static double Dot(double[]a,double[]b)=>a[0]*b[0]+a[1]*b[1];
        static double Norm(double[]a)=>Math.Sqrt(Dot(a,a));
    }
}
