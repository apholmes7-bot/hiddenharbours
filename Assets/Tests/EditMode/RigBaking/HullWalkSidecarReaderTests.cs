using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    public static class HullWalkFixture
    {
        public const string Folder = "Assets/Tests/EditMode/RigBaking/Fixtures/HullWalk/";
        public const string Rig = Folder + "boxWalkRig.js";
        public const string Walk = Folder + "boxWalkRig.box_walk.walk.json";
        public static string Json => File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, Walk));
        public static byte[] Bytes => File.ReadAllBytes(Path.Combine(RigCatalog.RepoRoot, Rig));
        public static HullWalkRead Read(string json = null) => HullWalkSidecarReader.Read(json ?? Json, Walk, Bytes);
        public static RigMeshData Extract(IRigScriptHost host)
        {
            host.Execute(File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, Rig)));
            return HullWalkRigExtractor.Extract(host, Rig, "BoxWalk", "BoxWalk", Path.Combine(RigCatalog.RepoRoot, Walk));
        }
        public static string WithLeafless => Json.Replace("\"house\": {", "\"house\": {\"doors\":[" +
            "{\"id\":\"open_arch\",\"leaf\":null,\"open\":true,\"reason\":\"No leaf on this arch\"," +
            "\"face\":\"aft\",\"y\":2,\"x0\":-0.5,\"x1\":0.5,\"z0\":2,\"z1\":3,\"sillZ\":2}],");
    }

    public sealed class HullWalkSidecarReaderTests
    {
        // The synthetic fixture is a cabin. Make the open fixture in memory only;
        // retain its house so the individual cases change exactly one premise.
        static string OpenFixtureJson => HullWalkFixture.Json.Replace("\"kind\": \"cabin\"", "\"kind\": \"open\"");

        static string WithoutHouse(string json)
        {
            var house = new Regex(@"(?ms)^  ""house"": \{.*?^  \},\r?\n");
            Assert.That(house.Matches(json).Count, Is.EqualTo(1), "remove exactly the fixture's root house");
            return house.Replace(json, "", 1);
        }

        [Test]
        public void OpenHullWithoutHouse_ReadsWithNoHouse()
        {
            var read = HullWalkFixture.Read(WithoutHouse(OpenFixtureJson));
            Assert.That(read.Ok, Is.True, string.Join(";", read.Errors));
            Assert.That(read.Kind, Is.EqualTo("open"));
            Assert.That(read.House, Is.Null);
            Assert.That(read.Fields.ContainsKey("house"), Is.False);
        }

        [Test]
        public void CabinHullWithoutHouse_RefusesRequiredField()
        {
            var read = HullWalkFixture.Read(WithoutHouse(HullWalkFixture.Json));
            Assert.That(read.Ok, Is.False);
            Assert.That(read.Errors.Single(), Is.EqualTo("box_walk: house: required field missing"));
        }

        [Test]
        public void OpenHullWithHouseObject_RefusesExpectedNull()
        {
            var read = HullWalkFixture.Read(OpenFixtureJson);
            Assert.That(read.Ok, Is.False);
            Assert.That(read.Errors.Single(), Is.EqualTo("box_walk: house: expected null on open hull"));
        }

        [Test]
        public void CompleteWalk_KeepsPolygonsHolesClimbsAndUnusedFields()
        {
            var read = HullWalkFixture.Read();
            Assert.That(read.Ok, Is.True, string.Join(";", read.Errors));
            Assert.That(read.Areas.Count, Is.EqualTo(5));
            Assert.That(read.Areas[0].Holes.Length, Is.EqualTo(1));
            Assert.That(read.Areas.Last().HeightAt(new Vector2(0, -2.5f)), Is.EqualTo(.5f));
            Assert.That(read.Areas.Last().HeightAt(new Vector2(0, -4)), Is.Zero);
            Assert.That(read.Areas.Last().HeightAt(new Vector2(0, 0)), Is.EqualTo(1));
            Assert.That(read.Fields.ContainsKey("qa"), Is.True);
            Assert.That(read.Anchors.ContainsKey("berths"), Is.True);
        }

        [TestCase("hidden-harbours/hull-walk@1", "hidden-harbours/hull-walk@7", "schema")]
        [TestCase("\"loa_m\":", "\"missing_loa\":", "loa_m")]
        [TestCase("\"poly\":", "\"missing_poly\":", "poly")]
        [TestCase("\"stepTol\":", "\"missing_step\":", "walk.stepTol")]
        public void BadField_RefusesWithHullAndField(string before, string after, string field)
        {
            var read = HullWalkFixture.Read(HullWalkFixture.Json.Replace(before, after));
            Assert.That(read.Ok, Is.False);
            StringAssert.Contains("box_walk", read.Errors.Single());
            StringAssert.Contains(field, read.Errors.Single());
        }

        [Test]
        public void Sha_MustBe64HexAndMustMatchRig()
        {
            string sha = HullWalkFixture.Read().RigSha256;
            foreach (string bad in new[] { "abc", new string('g', 64), new string('0', 64) })
            {
                var read = HullWalkFixture.Read(HullWalkFixture.Json.Replace(sha, bad));
                Assert.That(read.Ok, Is.False);
                StringAssert.Contains("derivedFromRigSha256", read.Errors.Single());
            }
            var crlf = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(HullWalkFixture.Bytes).Replace("\n", "\r\n"));
            Assert.That(HullWalkSidecarReader.Read(HullWalkFixture.Json, HullWalkFixture.Walk, crlf).HashMatch,
                Is.EqualTo(RigHashMatch.LineEndingNormalized));
        }

        [Test]
        public void LeaflessDoorway_KeepsPassageAndReasonWhole()
        {
            var read = HullWalkFixture.Read(HullWalkFixture.WithLeafless);
            Assert.That(read.Ok, Is.True, string.Join(";", read.Errors));
            var door = HullLayoutJson.Object(HullLayoutJson.Array(read.House, "doors").Single(), "door");
            Assert.That(door["leaf"], Is.Null);
            Assert.That(door["open"], Is.EqualTo(true));
            Assert.That(door["reason"], Is.EqualTo("No leaf on this arch"));
            Assert.That(door["sillZ"], Is.EqualTo(2d));
            Assert.That(door["x0"], Is.EqualTo(-.5d));
            Assert.That(door.Count, Is.EqualTo(11));
        }
    }
}
