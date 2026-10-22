using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    public sealed class HullWalkRigContractTests
    {
        static RigMeshData WithGeometryUnits(string units)
        {
            using var host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, HullWalkFixture.Rig)));
            // Vary only the synthetic fixture's returned geometry, in memory.
            if (units != null)
                host.Execute("const originalGeometry = BoxWalk.geometry; " +
                    "BoxWalk.geometry = () => Object.assign({}, originalGeometry(), {units:'" + units + "'});");
            return HullWalkRigExtractor.Extract(host, HullWalkFixture.Rig, "BoxWalk", "BoxWalk",
                Path.Combine(RigCatalog.RepoRoot, HullWalkFixture.Walk));
        }

        static object[] GeometrySnapshot(RigMeshData data)
        {
            object[] Faces(IEnumerable<RigFace> faces) => faces.Select(f => (object)new object[] {
                f.V.Select(v => new[] { v.X, v.Y, v.Z }).ToArray(), f.Mat, f.B, f.Db,
                f.Level, f.Role, f.LevelName, f.Interior, f.TexKind, f.TexPeriod, f.Uv }).ToArray();
            object[] Paints(IEnumerable<RigMaterial> paints) => paints.Select(m => (object)new object[] {
                m.Name, m.Ramp, m.RampHex, m.Off, m.Gain, m.Bias, m.OrderedDither, m.FixedIndex }).ToArray();
            return new object[] { data.RigKey, data.GlobalName, data.SourceFaceExpression,
                data.W, data.H, data.PxPerMetre, data.PivotX, data.PivotY, data.DefaultElev,
                data.Gain, data.Bias, data.LightN, data.Keyline, data.Bayer, data.BayerWasExported,
                Faces(data.Faces), Faces(data.WalkInteriorFaces), Paints(data.Materials), Paints(data.InteriorMaterials),
                data.LevelIds, data.Levels.Select(l => new object[] {
                    l.Id, l.Tag, l.DeckId, l.SoleZ, l.Enclosed, l.CeilingKind, l.CeilingZ }).ToArray(),
                data.LiftSets.Select(c => new object[] {
                    c.Key, c.DeckId, c.Level, c.LiftLevels, c.ShownRoles, c.RoleMasks }).ToArray(), data.Walk.Fields };
        }

        [Test]
        public void GeometryMetres_KeepsTheFixtureExtraction()
        {
            var original = WithGeometryUnits(null);
            var explicitMetres = WithGeometryUnits("metres");
            Assert.That(GeometrySnapshot(explicitMetres), Is.EqualTo(GeometrySnapshot(original)));
            Assert.That(explicitMetres.WalkGeometry["units"], Is.EqualTo("metres"));
        }

        [Test]
        public void GeometryM_EqualsMetresWithoutRewritingFields()
        {
            var metres = WithGeometryUnits("metres");
            var shorthand = WithGeometryUnits("m");
            Assert.That(GeometrySnapshot(shorthand), Is.EqualTo(GeometrySnapshot(metres)));
            Assert.That(shorthand.WalkGeometry["units"], Is.EqualTo("m"));
            Assert.That(shorthand.Walk.Fields["units"], Is.EqualTo("metres"));
        }

        [Test]
        public void GeometryFeet_RefusesWithHullAndField()
        {
            var error = Assert.Throws<FormatException>(() => WithGeometryUnits("feet"));
            Assert.That(error.Message, Is.EqualTo("box_walk: geometry.units: unsupported unit \"feet\""));
        }

        [Test]
        public void LiftSets_KeepWholeLevelsAndExactRoleSubsets()
        {
            using var host = RigScriptHostFactory.Create();
            var data = HullWalkFixture.Extract(host);
            var room = data.LiftSets.Single(c => c.Key == "room");
            Assert.That(room.LiftLevels, Is.EqualTo(1 << 3));
            Assert.That(room.ShownRoles, Is.EqualTo((1 << 0) | (1 << 1)));
            Assert.That(data.LiftSets.Single(c => c.Key == "top").ShownRoles, Is.EqualTo(1 << 1));
            var cutaway = HullLayoutJson.Child(data.WalkGeometry, "cutaway");
            HullLayoutJson.Child(cutaway, "gate")["deck"] = new List<object> { "top:roof" };
            HullWalkRigExtractor.ReadGeometry(data, data.WalkGeometry);
            var deck = data.LiftSets.Single(c => c.Key == "deck");
            Assert.That(deck.Level, Is.Zero);
            Assert.That(deck.LiftLevels, Is.Zero);
            Assert.That(deck.RoleMasks[3], Is.EqualTo(1 << 1));
        }

        [Test]
        public void GateCannotLiftFewerWholeLevelsThanGhost()
        {
            using var host = RigScriptHostFactory.Create();
            var data = HullWalkFixture.Extract(host);
            HullLayoutJson.Child(HullLayoutJson.Child(data.WalkGeometry, "cutaway"), "gate")["room"] = new List<object> { "room:roof" };
            var error = Assert.Throws<FormatException>(() => HullWalkRigExtractor.ReadGeometry(data, data.WalkGeometry));
            StringAssert.Contains("geometry.cutaway.gate.room", error.Message);
        }

        [Test]
        public void UnknownRoleAndShownLevelAreRefused()
        {
            using var host = RigScriptHostFactory.Create();
            var data = HullWalkFixture.Extract(host);
            var cut = HullLayoutJson.Child(data.WalkGeometry, "cutaway");
            HullLayoutJson.Child(cut, "gate")["deck"] = new List<object> { "top:missing" };
            StringAssert.Contains("unknown role", Assert.Throws<FormatException>(() => HullWalkRigExtractor.ReadGeometry(data, data.WalkGeometry)).Message);
            HullLayoutJson.Child(cut, "gate")["deck"] = new List<object>();
            HullLayoutJson.Child(cut, "shows")["room"] = "deck";
            StringAssert.Contains("expected hard level", Assert.Throws<FormatException>(() => HullWalkRigExtractor.ReadGeometry(data, data.WalkGeometry)).Message);
        }
    }
}
