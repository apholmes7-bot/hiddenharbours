using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class CumberlandGroundTests
    {
        const string Folder = CumberlandBuilder.GroundFolder;
        static GroundFileIntake.CumberlandRaster Read() => GroundFileIntake.ReadCumberlandFolder(Folder);

        [Test]
        public void CumberlandGround_DecodesTheBase_TexelForTexel()
        {
            var r = Read();
            Assert.That(r.Heights.Length, Is.EqualTo(1702400));
            Assert.That(r.Width, Is.EqualTo(1520)); Assert.That(r.Height, Is.EqualTo(1120));
            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            foreach (ushort code in r.Heights)
            {
                double value = r.Decode(code);
                Assert.That(value, Is.EqualTo(-6d + 12d * code / 65535d));
                Assert.That(Math.Round((value + 6d) * 65535d / 12d), Is.EqualTo(code));
                lo = Math.Min(lo, value); hi = Math.Max(hi, value);
            }
            Assert.That(lo, Is.EqualTo(-6)); Assert.That(hi, Is.EqualTo(6));
            Assert.That(GroundFileIntake.RasterCodeHash(r.Heights), Is.EqualTo("bf4196bee986bd29273ff735df4ab981cb4c32205fc89dd786fb9c02d98b0623"));
        }

        [Test]
        public void CumberlandStillWater_CodeZeroIsNone_TheBrookAndPondsKeepTheirLevels()
        {
            var r = Read();
            Assert.That(r.StillLevel(0), Is.EqualTo(double.NegativeInfinity));
            Assert.That(StillWaterLevels.DecodeCode01(0, -6, 6), Is.EqualTo(float.NegativeInfinity));
            Assert.That(r.Still.Count(v => v != 0), Is.EqualTo(18053));
            var water = (Dictionary<string, object>)r.Document["water"];
            var ponds = (List<object>)water["ponds"];
            Assert.That(ponds.Count, Is.EqualTo(3));
            foreach (Dictionary<string, object> pond in ponds)
            {
                int i = Index(r, (List<object>)pond["c"]);
                Assert.That(r.Still[i], Is.Not.Zero);
                Assert.That(r.StillLevel(r.Still[i]), Is.EqualTo((double)pond["level"]).Within(12d / 65535d));
                Assert.That(r.Decode(r.Heights[i]), Is.LessThan(r.StillLevel(r.Still[i])));
            }
            foreach (Dictionary<string, object> point in (List<object>)r.Document["stillpoints"])
            {
                var code = r.Still[Index(r, (List<object>)point["at"])];
                Assert.That(code, Is.Not.Zero);
                Assert.That(r.StillLevel(code), Is.EqualTo((double)point["plan"]).Within((double)point["tol"]));
            }
        }

        static int Index(GroundFileIntake.CumberlandRaster r, List<object> at) =>
            (int)Math.Floor((r.Size[1] / 2 - (double)at[1]) * r.PerMetre) * r.Width +
            (int)Math.Floor(((double)at[0] + r.Size[0] / 2) * r.PerMetre);

        [Test]
        public void CumberlandSeam_Row1119_CarriesNmcThirdIssueRow0()
        {
            var r = Read();
            foreach (string kind in new[] { "Height", "Still" })
            {
                var nmc = TerrainPlanPng.ReadFile("Assets/_Project/Data/Terrain/NineMileCreekGround/NineMileCreekGround_" + kind + ".png");
                Assert.That(nmc.Width, Is.EqualTo(1520)); Assert.That(nmc.Height, Is.EqualTo(1120));
                var codes = nmc.Channel(0);
                string hash = kind == "Height" ? "5924a6428d593bc80bc3eabe6c3887b39b05abe6aa5142b7ef9a0d6f9e8b02b0"
                    : "2c796a61961309de81777224c5c7f089aa2d1e1d35797895808bbcb8841e0d64";
                Assert.That(GroundFileIntake.RasterCodeHash(codes), Is.EqualTo(hash));
                var ours = kind == "Height" ? r.Heights : r.Still;
                for (int col = 0; col < r.Width; col++)
                    Assert.That(ours[(r.Height - 1) * r.Width + col], Is.EqualTo(codes[col]), kind + " column " + col);
            }
        }

        [Test]
        public void CumberlandIntake_RejectsOtherProfilesAndChangedPixels()
        {
            string json = File.ReadAllText(Folder + "/plan.json");
            byte[] h = File.ReadAllBytes(Folder + "/CumberlandSeabed_HeightTex_plan.png");
            byte[] s = File.ReadAllBytes(Folder + "/CumberlandSeabed_StillTex_plan.png");
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadCumberland(json.Replace(GroundFileIntake.CumberlandSchema, GroundFileIntake.Schema), h, s));
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadCumberland(json.Replace("terrain_plan.cmb", "terrain_plan.rpt"), h, s));
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadCumberland(json, s, h));
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadCumberland(json.Replace("\"perMetre\": 2", "\"perMetre\": 3"), h, s));
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadCumberland(json.Replace("\"amplitude\": 2.2", "\"amplitude\": 0"), h, s));
            var changed = (byte[])h.Clone(); changed[changed.Length / 2] ^= 1;
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadCumberland(json, changed, s));
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadCumberland(json.Replace("bf4196be", "af4196be"), h, s));
        }

        [Test]
        public void CumberlandPaint_CoversEveryTexelWithoutRoadPlacement()
        {
            var r = Read();
            var maps = Enumerable.Range(0, 6).Select(i => TerrainPlanPng.ReadFile(Folder + "/CumberlandSplat" + (char)('A' + i) + ".png")).ToArray();
            var expected = GroundFileIntake.CumberlandStarterPaint(r, File.ReadAllText(Folder + "/surface-rules.json"));
            for (int m = 0; m < maps.Length; m++)
            {
                Assert.That(maps[m].Samples.Select(v => (byte)v).ToArray(), Is.EqualTo(expected[m]), "Deterministic paint map " + m);
                Assert.That(maps[m].Width, Is.EqualTo(r.Width)); Assert.That(maps[m].Height, Is.EqualTo(r.Height));
            }
            for (int i = 0; i < r.Heights.Length; i++)
            {
                int sum = 0;
                foreach (var map in maps) for (int c = 0; c < 4; c++) sum += map.Samples[i * 4 + c];
                if (sum != 255) Assert.Fail("Paint weights must sum to one at texel " + i);
                if (maps[5].Samples[i * 4] != 0) Assert.Fail("Paths belong to CMB2");
                if (r.Still[i] != 0 && maps[4].Samples[i * 4 + 3] != 255) Assert.Fail("Still water must keep its mud bed");
            }
        }

        [Test]
        public void CumberlandCourse_MowsStripesGreensAndTees_KeepsRoughWaterAndBunkers()
        {
            var r = Read();
            var maps = GroundFileIntake.CumberlandStarterPaint(r, File.ReadAllText(Folder + "/surface-rules.json"));
            int At(double x, double y) => Index(r, new List<object> { x, y });
            byte Paint(int channel, double x, double y) => maps[channel / 4][At(x, y) * 4 + channel % 4];
            Assert.That(Paint(18, -95, 102), Is.EqualTo(255), "green");
            Assert.That(Paint(18, 205, 96), Is.EqualTo(255), "tee");
            Assert.That(Paint(18, 170, 100), Is.EqualTo(160).Or.EqualTo(208), "fairway stripe");
            Assert.That(Paint(18, 166, 100), Is.Not.EqualTo(Paint(18, 170, 100)), "four-metre stripes");
            Assert.That(Paint(0, 0, 210), Is.EqualTo(255), "rough keeps grass");
            Assert.That(Paint(19, -110, 228), Is.EqualTo(255), "pond overrides mowing");
            Assert.That(Paint(2, -80, 92), Is.EqualTo(255), "bunker is sand paint");
            string rules = File.ReadAllText(Folder + "/surface-rules.json");
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.CumberlandStarterPaint(r, rules.Replace("\"stripeMetres\": 4", "\"stripeMetres\": 0")));
        }

        [Test]
        public void CumberlandRoadAccess_RequiresKnownOnFootController()
        {
            Assert.That(HiddenHarbours.App.LandPassageAccess.Allows(false, ControlMode.OnFoot), Is.False);
            foreach (ControlMode mode in Enum.GetValues(typeof(ControlMode)))
                Assert.That(HiddenHarbours.App.LandPassageAccess.Allows(true, mode), Is.EqualTo(mode == ControlMode.OnFoot));
        }
    }
}
