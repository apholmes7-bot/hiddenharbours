using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class RicePointGroundTests
    {
        const string Folder = RicePointBuilder.GroundFolder;
        static GroundFileIntake.RicePointRaster Read() => GroundFileIntake.ReadRicePointFolder(Folder);

        [Test]
        public void RicePointGround_DecodesTheBase_TexelForTexel()
        {
            var r = Read();
            Assert.That(r.Heights.Length, Is.EqualTo(1094400));
            Assert.That(r.Width, Is.EqualTo(1520)); Assert.That(r.Height, Is.EqualTo(720));
            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            foreach (ushort code in r.Heights)
            {
                double value = r.Decode(code);
                Assert.That(value, Is.EqualTo(-6d + 12d * code / 65535d));
                Assert.That(Math.Round((value + 6d) * 65535d / 12d), Is.EqualTo(code));
                lo = Math.Min(lo, value); hi = Math.Max(hi, value);
            }
            Assert.That(lo, Is.EqualTo(-5.749).Within(0.0005)); Assert.That(hi, Is.EqualTo(6));
            Assert.That(GroundFileIntake.RasterCodeHash(r.Heights), Is.EqualTo("9feb4cd2fa2221cf863be2725206cb084a0b1eb3314edb6840c5f281e3818840"));
        }

        [Test]
        public void RicePointStillWater_CodeZeroIsNone_ThePoolsKeepTheirLevels()
        {
            var r = Read();
            Assert.That(r.StillLevel(0), Is.EqualTo(double.NegativeInfinity));
            Assert.That(StillWaterLevels.DecodeCode01(0, -6, 6), Is.EqualTo(float.NegativeInfinity));
            Assert.That(r.Still.Count(v => v != 0), Is.EqualTo(8576));
            var water = (Dictionary<string, object>)r.Document["water"];
            var pools = (List<object>)((Dictionary<string, object>)water["pools"])["list"];
            Assert.That(pools.Count, Is.EqualTo(5));
            foreach (Dictionary<string, object> pool in pools)
            {
                var measure = (Dictionary<string, object>)pool["measured"];
                var at = (List<object>)measure["deepest_at"];
                int col = (int)Math.Floor(((double)at[0] + r.Size[0] / 2) * r.PerMetre);
                int row = (int)Math.Floor((r.Size[1] / 2 - (double)at[1]) * r.PerMetre);
                var code = r.Still[row * r.Width + col];
                Assert.That(code, Is.Not.Zero, (string)pool["id"]);
                Assert.That(r.StillLevel(code), Is.EqualTo((double)pool["level"]).Within(12d / 65535d));
                Assert.That(r.Decode(r.Heights[row * r.Width + col]), Is.LessThan(r.StillLevel(code)));
            }
        }

        [Test]
        public void RicePointSeam_Column1519_CarriesNmcThirdIssueColumn0()
        {
            var r = Read();
            foreach (string kind in new[] { "Height", "Still" })
            {
                var nmc = TerrainPlanPng.ReadFile(Folder + "/SeamEvidence/NineMileCreekSeabed_" + kind + "Tex_plan.png");
                Assert.That(nmc.Width, Is.EqualTo(1520)); Assert.That(nmc.Height, Is.EqualTo(1120));
                var codes = nmc.Channel(0);
                string hash = kind == "Height" ? "5924a6428d593bc80bc3eabe6c3887b39b05abe6aa5142b7ef9a0d6f9e8b02b0"
                    : "2c796a61961309de81777224c5c7f089aa2d1e1d35797895808bbcb8841e0d64";
                Assert.That(GroundFileIntake.RasterCodeHash(codes), Is.EqualTo(hash));
                var ours = kind == "Height" ? r.Heights : r.Still;
                for (int row = 0; row < r.Height; row++)
                    Assert.That(ours[row * r.Width + r.Width - 1], Is.EqualTo(codes[(row + 400) * nmc.Width]), kind + " row " + row);
            }
        }

        [Test]
        public void RicePointIntake_RejectsOtherProfilesAndChangedPixels()
        {
            string json = File.ReadAllText(Folder + "/plan.json");
            byte[] h = File.ReadAllBytes(Folder + "/RicePointSeabed_HeightTex_plan.png");
            byte[] s = File.ReadAllBytes(Folder + "/RicePointSeabed_StillTex_plan.png");
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadRicePoint(json.Replace(GroundFileIntake.RicePointSchema, GroundFileIntake.Schema), h, s));
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadRicePoint(json, s, h));
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadRicePoint(json.Replace("\"perMetre\": 2", "\"perMetre\": 3"), h, s));
        }

        [Test]
        public void RicePointPaint_CoversEveryTexelWithoutRoadPlacement()
        {
            var r = Read();
            var maps = Enumerable.Range(0, 6).Select(i => TerrainPlanPng.ReadFile(Folder + "/RicePointSplat" + (char)('A' + i) + ".png")).ToArray();
            var expected = GroundFileIntake.RicePointStarterPaint(r, File.ReadAllText(Folder + "/surface-rules.json"));
            for (int m = 0; m < maps.Length; m++)
                Assert.That(maps[m].Samples.Select(v => (byte)v).ToArray(), Is.EqualTo(expected[m]), "Deterministic paint map " + m);
            foreach (var map in maps) { Assert.That(map.Width, Is.EqualTo(r.Width)); Assert.That(map.Height, Is.EqualTo(r.Height)); }
            for (int i = 0; i < r.Heights.Length; i++)
            {
                int sum = 0;
                foreach (var map in maps) for (int c = 0; c < 4; c++) sum += map.Samples[i * 4 + c];
                if (sum != 255) Assert.Fail("Paint must select one substrate at texel " + i);
                if (maps[5].Samples[i * 4] != 0) Assert.Fail("Paths belong to RPT2");
            }
        }

        [Test]
        public void RicePointRoadAccess_RequiresKnownOnFootController()
        {
            Assert.That(HiddenHarbours.App.LandPassageAccess.Allows(false, ControlMode.OnFoot), Is.False);
            foreach (ControlMode mode in Enum.GetValues(typeof(ControlMode)))
                Assert.That(HiddenHarbours.App.LandPassageAccess.Allows(true, mode), Is.EqualTo(mode == ControlMode.OnFoot));
        }
    }
}
