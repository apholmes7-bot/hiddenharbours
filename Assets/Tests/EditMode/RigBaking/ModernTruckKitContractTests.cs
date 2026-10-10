using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    [TestFixture("350", "9457a399d041e2b2590fb702305fdccf658ee7f1291bfe48664ee48ca361a8fb", 48)]
    [TestFixture("2500", "7b483a56d32329f4841f7f5678cadb18ff2dcdbbbf8c4ed68727c912f025df3b", 50)]
    public class ModernTruckKitContractTests
    {
        readonly string number, pin;
        readonly int hoodSweep;
        public ModernTruckKitContractTests(string number, string pin, int hoodSweep)
        { this.number = number; this.pin = pin; this.hoodSweep = hoodSweep; }

        string Key => "modern" + number;
        string Kit => "docs/art/rigs/" + Key + "-kit";
        string Rig => Kit + "/" + Key + ".rig.js";
        string Sidecar => "docs/art/rigs/gameplay/vehicles/" + Key + ".rig.gameplay.json";
        string Full(string path) => Path.Combine(RigCatalog.RepoRoot, path);
        string SumsFile => Directory.GetFiles(Full(Kit), "SHA256SUMS.txt", SearchOption.AllDirectories).Single();
        VehicleRigFleet.Vehicle Truck => VehicleRigFleet.Vehicles.Single(v => v.Key == Key);
        object Json(string path) => DeckSidecarJson.Parse(File.ReadAllText(Full(path)));
        static string Digest(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }
        static string ContentDigest(byte[] bytes)
        {
            if (bytes.Length < 1024)
            {
                var text = Encoding.UTF8.GetString(bytes);
                if (text.StartsWith("version https://git-lfs.github.com/spec/v1", StringComparison.Ordinal))
                    return text.Split('\n').Single(l => l.StartsWith("oid sha256:", StringComparison.Ordinal)).Substring(11).Trim();
            }
            return Digest(bytes);
        }
        Dictionary<string, string> Sums() => File.ReadAllLines(SumsFile)
            .Where(l => l.Length > 0 && l[0] != '#')
            .ToDictionary(l => l.Substring(66), l => l.Substring(0, 64));

        [Test]
        public void RigContractAndSidecarAgreeOnTheFullPin()
        {
            Assert.That(Digest(File.ReadAllBytes(Full(Rig))), Is.EqualTo(pin));
            Assert.That(DeckSidecarJson.String(DeckSidecarJson.Member(Json(Kit + "/" + Key + ".contract.json"), "rigSha256")), Is.EqualTo(pin));
            Assert.That(DeckSidecarJson.String(DeckSidecarJson.Member(Json(Sidecar), "derivedFromRigSha256")), Is.EqualTo(pin));
        }

        [Test]
        public void EveryTextDigestMatchesAndAllTextUsesLf()
        {
            var sums = Sums();
            Assert.That(sums.Count, Is.EqualTo(15));
            foreach (var entry in sums)
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(SumsFile), entry.Key));
                Assert.That(ContentDigest(bytes), Is.EqualTo(entry.Value), entry.Key);
                if (!entry.Key.EndsWith(".png", StringComparison.Ordinal))
                    Assert.That(bytes.Count(b => b == 13), Is.Zero, entry.Key);
            }
        }

        [Test]
        public void SheetCensusMatchesTheManifestAndHasNoExtras()
        {
            var sheets = DeckSidecarJson.AsArray(DeckSidecarJson.Member(Json(Kit + "/" + Key + ".contract.json"), "sheets"));
            string[] names = sheets.Select(s => DeckSidecarJson.String(DeckSidecarJson.Member(s, "file"))).OrderBy(s => s).ToArray();
            Assert.That(names.Length, Is.EqualTo(10));
            var folder = Path.GetDirectoryName(SumsFile);
            Assert.That(Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(s => s),
                Is.EqualTo(names.Concat(new[] { "SHA256SUMS.txt" }).OrderBy(s => s)));
            Assert.That(Sums().Keys.Where(s => s.EndsWith(".png", StringComparison.Ordinal)).OrderBy(s => s), Is.EqualTo(names));
        }

        [Test]
        public void PreviewContainsTheExactRig()
        {
            string rig = File.ReadAllText(Full(Rig));
            Assert.That(File.ReadAllText(Full(Kit + "/preview.html")), Does.Contain(rig));
        }

        [Test]
        public void KitTextIsPinnedByNameAndPngsKeepLfs()
        {
            var lines = File.ReadAllLines(Full(".gitattributes")).Select(l => l.Trim()).Where(l => l.Length > 0 && l[0] != '#').ToArray();
            Assert.That(lines.Any(l => l.StartsWith("*.png", StringComparison.Ordinal) && l.Contains("lfs")), Is.True);
            foreach (var entry in Sums().Keys.Where(s => !s.EndsWith(".png", StringComparison.Ordinal)))
            {
                string file = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SumsFile), entry));
                string rel = file.Substring(Path.GetFullPath(RigCatalog.RepoRoot).TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/');
                Assert.That(lines.Any(l => l.StartsWith(rel + " ", StringComparison.Ordinal) && l.Contains("text eol=lf")), Is.True, rel);
            }
            Assert.That(lines.Any(l => l.StartsWith(Kit + "/", StringComparison.Ordinal) && l.Split(' ', '\t')[0].Contains("*") && l.Contains("text")), Is.False);
        }

        [Test]
        public void FleetRowUsesThisTrucksPathsIdsAndQualifiedBuilder()
        {
            var t = Truck;
            Assert.That(t.ScriptPath, Is.EqualTo(Rig));
            Assert.That(t.SidecarPath, Is.EqualTo(Sidecar));
            Assert.That(t.GlobalName, Is.EqualTo("ModernTruck" + number));
            Assert.That(t.MeshId, Is.EqualTo("vehiclemesh.modern_" + number));
            Assert.That(t.VehicleId, Is.EqualTo("vehicle.modern_" + number));
            Assert.That(t.MeshAssetPath, Is.EqualTo("Assets/_Project/Data/Vehicles/Meshes/Modern" + number + "VehicleMesh.asset"));
            Assert.That(t.VehicleDefPath, Is.EqualTo("Assets/_Project/Data/Vehicles/Modern" + number + ".asset"));
            Assert.That(t.Extraction.FaceExpression, Is.EqualTo("build(ModernTruck" + number + ".resolve({}))"));
            Assert.That(VehicleRigFleet.Baked, Does.Contain(Key));
            Assert.That(VehicleRigFleet.NotBaked.ContainsKey(Key), Is.False);
            Assert.That(VehicleRigFleet.SidecarHashRefused.ContainsKey(Key), Is.False);
        }

        [Test]
        public void SixLeavesHaveSignedSweepsAndWheelsPrecedeKnuckles()
        {
            var expected = new[] { ("DoorFL", -68), ("DoorFR", 68), ("DoorRL", -68), ("DoorRR", 68), ("Hood", hoodSweep), ("Gate", 92) };
            foreach (var (slot, degrees) in expected)
            {
                var axis = Truck.Axes.Single(a => a.Slot == slot);
                Assert.That(axis.SweepDegrees, Is.EqualTo(degrees));
                Assert.That(axis.HingeAxis, Is.EqualTo(slot.StartsWith("Door", StringComparison.Ordinal) ? VehicleHingeAxis.Vertical : VehicleHingeAxis.Lateral));
            }
            var slots = Truck.Axes.Select(a => a.Slot).ToArray();
            Assert.That(slots.Length, Is.EqualTo(12));
            foreach (string wheel in new[] { "WheelFL", "WheelFR", "WheelRL", "WheelRR" })
                Assert.That(Array.IndexOf(slots, wheel), Is.InRange(0, Array.IndexOf(slots, "KnuckleFL") - 1));
        }

        [Test]
        public void SidecarIsAHardCabWithDoorsOutsideItsCollider()
        {
            Assert.That(DeckSidecarJson.String(DeckSidecarJson.Member(Json(Sidecar), "kind")), Is.EqualTo("road_vehicle"));
            var f = VehicleSidecarFacts.Read(File.ReadAllText(Full(Sidecar)), Sidecar);
            Assert.That(f.Errors, Is.Empty);
            Assert.That(f.HasCollider && f.HasDriveDoor, Is.True);
            Assert.That(f.HasDriverSeat || f.HasFlotation || f.HasFifthWheel || f.HasKingpin, Is.False);
            Assert.That(f.Absences.Count, Is.EqualTo(4));
            Assert.That(f.DriveDoorLocal, Is.EqualTo(f.ReachPoints["drive"]));
            foreach (string id in new[] { "drive", "ride", "ride_rear_street", "ride_rear_curb" })
            {
                var p = f.ReachPoints[id];
                Assert.That(p.x < f.ColliderMin.x || p.x > f.ColliderMax.x || p.y < f.ColliderMin.y || p.y > f.ColliderMax.y, Is.True, id);
            }
        }
    }
}
