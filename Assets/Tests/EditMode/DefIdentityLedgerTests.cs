using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class DefIdentityLedgerTests
    {
        private const string FishType = "Tests.FishDef";
        private const string BoatType = "Tests.BoatDef";
        private const string BaselineGuid = "11111111111111111111111111111111";
        private const string AdditionGuid = "22222222222222222222222222222222";
        private const string OtherGuid = "33333333333333333333333333333333";
        private string _root;
        private string _baseline;
        private string _additions;

        [SetUp]
        public void CreateInventory()
        {
            _root = Path.GetFullPath(Path.Combine("Temp", "DefIdentityLedgerTests_" + Guid.NewGuid().ToString("N")));
            _baseline = Path.Combine(_root, "baseline.json");
            _additions = Path.Combine(_root, "additions");
            Directory.CreateDirectory(_additions);
            File.WriteAllText(_baseline, "{\"Types\":[\"" + FishType + "\"],\"Entries\":["
                + Identity(FishType, BaselineGuid, "fish.cod") + "]}");
        }

        [TearDown]
        public void DeleteInventory()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void EmptyAdditions_PreserveEveryCheckedInBaselineTuple()
        {
            var baseline = JsonUtility.FromJson<DefIdentityLedger.Ledger>(File.ReadAllText(DefIdentityLedger.BaselinePath));
            var actual = DefIdentityLedger.Load(DefIdentityLedger.BaselinePath, _additions);
            CollectionAssert.AreEquivalent(baseline.Types, actual.Types);
            CollectionAssert.AreEquivalent(baseline.Entries.Select(Record), actual.Entries.Select(Record));
        }

        [Test]
        public void Addition_IsReadAlongsideTheUnchangedBaseline()
        {
            string original = File.ReadAllText(_baseline);
            Write(AdditionGuid + ".json", Identity(FishType, AdditionGuid, "fish.haddock"));
            var actual = Load();
            CollectionAssert.AreEqual(new[] { FishType }, actual.Types);
            CollectionAssert.AreEqual(new[]
            {
                FishType + "|fish.cod|" + BaselineGuid,
                FishType + "|fish.haddock|" + AdditionGuid,
            }, actual.Entries.Select(Record));
            Assert.That(File.ReadAllText(_baseline), Is.EqualTo(original));
        }

        [Test]
        public void TypeDeclaration_IsReadEvenWithoutAnIdentity()
        {
            Write(BoatType + ".type.json", "{\"Type\":\"" + BoatType + "\"}");
            var actual = Load();
            CollectionAssert.AreEqual(new[] { BoatType, FishType }, actual.Types);
            Assert.That(actual.Entries.Length, Is.EqualTo(1));
        }

        [Test]
        public void EnumerationOrder_DoesNotChangeTheOrderedResult()
        {
            string identity = Write(AdditionGuid + ".json", Identity(BoatType, AdditionGuid, "boat.dory"));
            string declaration = Write(BoatType + ".type.json", "{\"Type\":\"" + BoatType + "\"}");
            string other = Write(OtherGuid + ".json", Identity(FishType, OtherGuid, "fish.haddock"));
            var paths = new[] { other, declaration, identity };
            var forward = DefIdentityLedger.Read(_baseline, paths);
            var backward = DefIdentityLedger.Read(_baseline, paths.Reverse());
            CollectionAssert.AreEqual(new[] { BoatType, FishType }, forward.Types);
            CollectionAssert.AreEqual(new[]
            {
                BoatType + "|boat.dory|" + AdditionGuid,
                FishType + "|fish.cod|" + BaselineGuid,
                FishType + "|fish.haddock|" + OtherGuid,
            }, forward.Entries.Select(Record));
            CollectionAssert.AreEqual(forward.Types, backward.Types);
            CollectionAssert.AreEqual(forward.Entries.Select(Record), backward.Entries.Select(Record));
        }

        [TestCase("Guid")]
        [TestCase("Id")]
        [TestCase("Type")]
        public void DuplicateAgainstBaseline_FailsWithBothSourcesNamed(string kind)
        {
            string path = kind == "Type"
                ? Write(FishType + ".type.json", "{\"Type\":\"" + FishType + "\"}")
                : Write((kind == "Guid" ? BaselineGuid : AdditionGuid) + ".json",
                    Identity(FishType, kind == "Guid" ? BaselineGuid : AdditionGuid,
                        kind == "Guid" ? "fish.haddock" : "fish.cod"));
            AssertFailure(path, "duplicate", _baseline, kind == "Type" ? "type declaration" : kind);
        }

        [TestCase("Guid")]
        [TestCase("Id")]
        [TestCase("Type")]
        public void DuplicateBetweenAdditions_FailsWithBothSourcesNamed(string kind)
        {
            string first, second;
            if (kind == "Type")
            {
                first = Write("a/" + BoatType + ".type.json", "{\"Type\":\"" + BoatType + "\"}");
                second = Write("b/" + BoatType + ".type.json", "{\"Type\":\"" + BoatType + "\"}");
            }
            else
            {
                first = Write("a/" + AdditionGuid + ".json", Identity(FishType, AdditionGuid, "fish.haddock"));
                string guid = kind == "Guid" ? AdditionGuid : OtherGuid;
                second = Write("b/" + guid + ".json", Identity(FishType, guid,
                    kind == "Guid" ? "fish.hake" : "fish.haddock"));
            }
            AssertFailure(second, "duplicate", first, kind == "Type" ? "type declaration" : kind);
        }

        [Test]
        public void IdenticalRepeatedRow_IsRejectedRatherThanDeduplicated()
        {
            string path = Write(BaselineGuid + ".json", Identity(FishType, BaselineGuid, "fish.cod"));
            AssertFailure(path, "duplicate Guid", _baseline, BaselineGuid);
        }

        [TestCase("{")]
        [TestCase("null")]
        [TestCase("[]")]
        [TestCase("{}")]
        [TestCase("{\"Type\":\"Tests.FishDef\"}")]
        [TestCase("{\"Type\":\"Tests.FishDef\",\"Guid\":null,\"Id\":\"fish.haddock\"}")]
        [TestCase("{\"Type\":\"Tests.FishDef\",\"Guid\":7,\"Id\":\"fish.haddock\"}")]
        [TestCase("{\"Type\":\"Tests.FishDef\",\"Guid\":\"22222222222222222222222222222222\",\"Id\":\"\"}")]
        [TestCase("{\"Type\":\"Tests.FishDef\",\"Guid\":\"22222222222222222222222222222222\",\"Id\":\"fish.haddock\",\"Extra\":\"x\"}")]
        [TestCase("{\"Type\":\"Tests.FishDef\",\"Guid\":\"22222222222222222222222222222222\",\"Id\":\"fish.haddock\",\"Id\":\"fish.hake\"}")]
        [TestCase("{\"Type\":\"Tests.FishDef\",\"Guid\":\"22222222222222222222222222222222\",\"Id\":\"fish.haddock\",\"\\u0049d\":\"fish.hake\"}")]
        [TestCase("{\"Type\":\"Tests.FishDef\",\"Guid\":\"22222222222222222222222222222222\",\"Id\":\"fish.haddock\",}")]
        [TestCase("{\"Type\":\"Tests.FishDef\",\"Guid\":\"22222222222222222222222222222222\",\"Id\":\"fish.haddock\"} trailing")]
        [TestCase("{\"Type\":\"Tests.FishDef\",\"Guid\":\"22222222222222222222222222222222\",\"Id\":\"fish.\\qhaddock\"}")]
        [TestCase("{\"Type\":\"Tests.FishDef\",\"Guid\":\"22222222222222222222222222222222\",\"Id\":\"fish.\\nhaddock\"}")]
        public void MalformedAddition_FailsWithItsFilename(string text)
        {
            string path = Write(AdditionGuid + ".json", text);
            AssertFailure(path);
        }

        [TestCase("short")]
        [TestCase("ABCDEFABCDEFABCDEFABCDEFABCDEFAB")]
        [TestCase("gggggggggggggggggggggggggggggggg")]
        public void InvalidGuid_FailsWithItsFilename(string guid)
        {
            string path = Write(guid + ".json", Identity(FishType, guid, "fish.haddock"));
            AssertFailure(path, "32 lowercase hexadecimal");
        }

        [Test]
        public void IdentityFilename_MustMatchTheRecordedGuid()
        {
            string path = Write(OtherGuid + ".json", Identity(FishType, AdditionGuid, "fish.haddock"));
            AssertFailure(path, "filename must match Guid");
        }

        [Test]
        public void TypeFilename_MustMatchTheDeclaredType()
        {
            string path = Write("Other.type.json", "{\"Type\":\"" + BoatType + "\"}");
            AssertFailure(path, "filename must match Type");
        }

        [Test]
        public void UndeclaredType_FailsWithItsIdentityFileNamed()
        {
            string path = Write(AdditionGuid + ".json", Identity(BoatType, AdditionGuid, "boat.dory"));
            AssertFailure(path, "undeclared Def type", BoatType);
        }

        [Test]
        public void MissingAdditionsDirectory_FailsRatherThanLosingRecordedAdditions()
        {
            string missing = Path.Combine(_additions, "missing");
            Assert.Throws<DirectoryNotFoundException>(() => DefIdentityLedger.Load(_baseline, missing));
        }

        [Test]
        public void MetadataAndReadme_AreNotParsedAsAdditions()
        {
            Write("README.md", "authoring notes");
            Write(AdditionGuid + ".json.meta", "fileFormatVersion: 2");
            Assert.That(Load().Entries.Length, Is.EqualTo(1));
        }

        private DefIdentityLedger.Ledger Load() => DefIdentityLedger.Load(_baseline, _additions);

        private string Write(string name, string text)
        {
            string path = Path.GetFullPath(Path.Combine(_additions, name));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            return path;
        }

        private void AssertFailure(string path, params string[] details)
        {
            var error = Assert.Throws<FormatException>(() => Load());
            Assert.That(error.Message, Does.Contain(path));
            foreach (string detail in details) Assert.That(error.Message, Does.Contain(detail));
        }

        private static string Identity(string type, string guid, string id) =>
            "{\"Type\":\"" + type + "\",\"Guid\":\"" + guid + "\",\"Id\":\"" + id + "\"}";

        private static string Record(DefIdentityLedger.Identity row) => row.Type + "|" + row.Id + "|" + row.Guid;
    }
}
