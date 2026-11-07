using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HiddenHarbours.App.Editor;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public class NmcWharfWaterSideIntakeTests
    {
        [TestCase("marinaKit.js", 37741, "8a261b943dd697eac31ba2d34828afa9b3b4c39ecfdfc01a25b7895906856211")]
        [TestCase("marinaRig.js", 56016, "7c4733acd839b831d36cc18040b9f02de4b8236f2e96647e040a8133cde48602")]
        [TestCase("marinaBoats.js", 8588, "888ed44486475890354e41502a6685cbc87f193392c0554838382435beb08229")]
        [TestCase("nmcWharf.js", 60356, "2e42f3877fcc22ff1e433c409b3642a19c8d2f50db50af6302d5b336de8138cd")]
        public void AuthorizedSourceBytesStayPinned(string file, int length, string expected)
        {
            byte[] bytes = File.ReadAllBytes("docs/art/rigs/nmc-marina-kit/" + file);
            Assert.That(bytes.Length, Is.EqualTo(length));
            using var sha = SHA256.Create();
            Assert.That(string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2"))), Is.EqualTo(expected));
        }

        [Test]
        public void FiveHarboursUseExistingDefsAndRetainAllRegisteredLadderAnchors()
        {
            var defs = NineMileCreekWharfWaterSide.HarbourKeys.Select(NineMileCreekWharfWaterSide.Definition).ToArray();
            CollectionAssert.AreEquivalent(new[] { "harbour.west", "harbour.fleet", "harbour.east", "harbour.arm_w", "harbour.arm_e" }, defs.Select(d => d.Id));
            Assert.That(defs.All(d => d.HasArt), Is.True);
            string[] anchors = defs.SelectMany(d => d.Anchors).Select(a => a.Name).ToArray();
            string[] ladders = { "ladder.nmc_berth0", "ladder.nmc_berth1", "ladder.nmc_berth2", "ladder.nmc_berth3", "ladder.nmc_berth4", "ladder.nmc_berth5", "ladder.nmc_east", "ladder.nmc_unload_w", "ladder.nmc_unload_e" };
            foreach (string ladder in ladders) Assert.That(anchors.Count(a => a == ladder), Is.EqualTo(1), ladder);
        }

        [Test]
        public void ThePlayerSlotAndStepOffKeepTheirRegisteredPositions()
        {
            var slot = NineMileCreekWharfWaterSide.WallSlot(0);
            Assert.That(slot.x, Is.EqualTo(78.6f)); Assert.That(slot.y, Is.EqualTo(93.8f));
            Assert.That(NineMileCreekWharfWaterSide.WallCentres(), Has.Length.EqualTo(6));
            Assert.That(NineMileCreekGroundPlan.DisembarkPos.x, Is.EqualTo(86.2f));
            Assert.That(NineMileCreekGroundPlan.DisembarkPos.y, Is.EqualTo(87.35f));
        }

        [Test]
        public void EachFloatOwnerUsesTheRowThatNamesThatOwner()
        {
            var rows = NineMileCreekWharfWaterSide.FloatBoatRows();
            Assert.That(rows, Has.Length.EqualTo(3));
            foreach (var owner in NineMileCreekMooredFleet.LoadOwners().Where(o => o.LiesAtTheFloat))
            {
                var row = NineMileCreekWharfWaterSide.FloatRowForOwner(owner.Id);
                Assert.That(NineMileCreekWharf.FloatBerthPos(owner.BerthIndex, null), Is.EqualTo(row.At), owner.Id);
            }
        }
    }
}
