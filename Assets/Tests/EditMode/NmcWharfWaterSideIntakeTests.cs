using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HiddenHarbours.App.Editor;
using NUnit.Framework;
using UnityEngine;

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
        public void EachFloatOwnerHasTheOneRowThatNamesThatOwner()
        {
            var rows = NineMileCreekWharfWaterSide.FloatBoatRows();
            Assert.That(rows, Has.Length.EqualTo(3));
            var floatOwners = NineMileCreekMooredFleet.LoadOwners().Where(o => o.LiesAtTheFloat).ToArray();
            Assert.That(floatOwners, Has.Length.EqualTo(3));
            var named = floatOwners.Select(o => NineMileCreekWharfWaterSide.FloatRowForOwner(o.Id).Id).ToArray();
            CollectionAssert.AreEquivalent(rows.Select(r => r.Id), named);
        }

        // ---- the register's own guards --------------------------------------------------------------------
        // The analytic plan (NineMileCreekMainland, the builder's wharf) keeps its packed berth line and its own
        // float, and main's guards hold those unchanged. The scene's boats lie where the wharf's register says, on
        // the G3 ground: these cases hold the register.

        static bool FitsHerSlot(float centre, float halfSpan, Vector2 slot) =>
            centre - halfSpan >= slot.x - 1e-3f && centre + halfSpan <= slot.y + 1e-3f;

        [Test]
        public void TheRegisteredWallSlotsDoNotLap_AndEachHullWithHerFendersFitsHerOwn()
        {
            float[] centres = NineMileCreekWharfWaterSide.WallCentres();
            float edge = NineMileCreekWharfWaterSide.WallSlot(0).x;
            for (int i = 0; i < centres.Length; i++)
            {
                Vector2 slot = NineMileCreekWharfWaterSide.WallSlot(i);
                Assert.That(slot.x, Is.GreaterThanOrEqualTo(edge - 1e-3f),
                    $"registered berth {i} starts at {slot.x:0.00}, before the previous slot ends at {edge:0.00}");
                Assert.That(slot.y, Is.GreaterThan(slot.x), $"registered berth {i} has no length");
                Assert.That(slot.x, Is.GreaterThanOrEqualTo(NineMileCreekGroundPlan.FaceStepX - 1e-3f), $"berth {i} starts west of the north face");
                Assert.That(slot.y, Is.LessThanOrEqualTo(NineMileCreekGroundPlan.NorthFaceEastX + 1e-3f), $"berth {i} ends east of the north face");
                edge = slot.y;
            }

            var wallOwners = NineMileCreekMooredFleet.LoadOwners().Where(o => !o.LiesAtTheFloat && o.IsPresentable()).ToArray();
            Assert.That(wallOwners, Has.Length.EqualTo(centres.Length - 1), "one wall owner for each berth but the player's");
            CollectionAssert.AreEquivalent(Enumerable.Range(1, centres.Length - 1), wallOwners.Select(o => o.BerthIndex),
                "each wall owner has a registered berth of her own, and none has the player's berth 0");
            foreach (var owner in wallOwners)
            {
                Vector2 slot = NineMileCreekWharfWaterSide.WallSlot(owner.BerthIndex);
                float halfSpan = NineMileCreekMainland.BerthHalfSpanFor(NineMileCreekMooredFleet.LengthOf(owner));
                Assert.That(FitsHerSlot(centres[owner.BerthIndex], halfSpan, slot), Is.True,
                    $"{owner.Id}: her hull and both fenders ({2f * halfSpan:0.00} m) about x {centres[owner.BerthIndex]:0.00} " +
                    $"leave registered slot x {slot.x:0.00} to {slot.y:0.00}");
                // The control: the same hull four metres longer than her slot does not fit it.
                Assert.That(FitsHerSlot(centres[owner.BerthIndex], (slot.y - slot.x) * 0.5f + 2f, slot), Is.False, owner.Id);
                Assert.That(NineMileCreekGroundSteps.WallBerth(owner.BerthIndex, NineMileCreekMooredFleet.HalfBeamOf(owner)).x,
                    Is.EqualTo(centres[owner.BerthIndex]), owner.Id + " lies at the centre of her registered slot");
            }

            // The player's reserve lies inside the player's slot, so no owner's slot reaches into it.
            Vector2 players = NineMileCreekWharfWaterSide.WallSlot(0);
            float reserve = NineMileCreekMainland.BerthHalfSpanFor(NineMileCreekMainland.PlayerBerthReserveMetres);
            Assert.That(FitsHerSlot(centres[0], reserve, players), Is.True,
                $"the player's reserve ({2f * reserve:0.00} m) leaves registered slot x {players.x:0.00} to {players.y:0.00}");
        }

        [Test]
        public void ThePlayersRegisteredBerthIsWhereSheArrivesAndStepsOff()
        {
            Vector2 slot = NineMileCreekWharfWaterSide.WallSlot(0);
            Assert.That(NineMileCreekGroundPlan.ArrivalPos.x, Is.EqualTo(NineMileCreekWharfWaterSide.WallCentres()[0]).Within(1e-3f),
                "she arrives at the centre of the player's registered slot");
            Assert.That(NineMileCreekGroundPlan.DisembarkPos.x, Is.InRange(slot.x, slot.y), "she steps off abreast of her own slot");
        }

        [Test]
        public void EveryRegisteredFloatBerthLiesAlongsideTheWorkingFloat_AndCarriesItsResidentAtSpringLow()
        {
            Rect working = NineMileCreekWharfWaterSide.WorkingFloat();
            var terrain = NineMileCreekGround.Load();
            var residents = NineMileCreekMooredFleet.LoadOwners().Where(o => o.LiesAtTheFloat).ToArray();
            Assert.That(residents, Has.Length.EqualTo(3));
            foreach (var resident in residents)
            {
                var row = NineMileCreekWharfWaterSide.FloatRowForOwner(resident.Id);
                Assert.That(row.At.x, Is.InRange(working.xMin, working.xMax), row.Id + " lies abreast of the working float");
                Assert.That(working.Contains(row.At), Is.False, row.Id + " lies on the water, not on the float");
                Assert.That(resident.Boat, Is.Not.Null, row.Id);
                float depth = NineMileCreekMainland.SpringLowWater - terrain.ElevationAt(row.At);
                float carries = depth - NineMileCreekMainland.ChannelKeelClearanceMetres;
                Assert.That(carries, Is.GreaterThan(resident.Boat.DraughtMeters),
                    $"{row.Id} at ({row.At.x:0.00}, {row.At.y:0.00}): depth {depth:0.000000} m at spring low carries " +
                    $"{carries:0.000000} m, and her resident draws {resident.Boat.DraughtMeters:0.00} m");
            }
        }
    }
}
