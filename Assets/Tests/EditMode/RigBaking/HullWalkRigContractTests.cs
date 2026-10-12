using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    public sealed class HullWalkRigContractTests
    {
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
