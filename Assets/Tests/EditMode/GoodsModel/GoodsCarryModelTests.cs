using System;
using NUnit.Framework;
using HiddenHarbours.Tools.GoodsModel;

namespace HiddenHarbours.Tests.GoodsModel
{
    public class GoodsCarryModelTests
    {
        private GoodsCarryModel _model;
        private GoodsContainerSpec _pan;

        [SetUp]
        public void SetUp()
        {
            _model = new GoodsCarryModel(GoodsModelFixtures.Limits());
            _pan = GoodsModelFixtures.Pan();
            _model.RegisterContainer(GoodsModelFixtures.Shell(_pan), _pan, "boat");
        }

        private void Hold(GoodsPiece piece, bool left = true)
        {
            _model.Register(piece, "boat");
            Assert.That(_model.Take(piece.Id, "boat", left), Is.EqualTo(GoodsResult.Success));
        }

        [Test]
        public void PutThenTake_PreservesExactItemAndFreesOnlyItsHand()
        {
            Hold(new GoodsPiece("rod", "goods.rod", GoodsKind.Tool, new GoodsBox(400, 50, 50), 500, false));
            Hold(GoodsModelFixtures.Fish());
            Assert.That(_model.LeftHand, Is.EqualTo("rod"));
            Assert.That(_model.RightHand, Is.EqualTo("fish_a"));
            Assert.That(_model.Put("fish_a", "pan"), Is.EqualTo(GoodsResult.Success));
            Assert.That(_model.LeftHand, Is.EqualTo("rod"));
            Assert.That(_model.RightHand, Is.Null);
            Assert.That(_model.Contents("pan"), Is.EqualTo(new[] { "fish_a" }));
            Assert.That(_model.GrossMassG("pan"), Is.EqualTo(2000));
            Assert.That(_model.Take("fish_a", "pan", false), Is.EqualTo(GoodsResult.Success));
            Assert.That(_model.RightHand, Is.EqualTo("fish_a"));
            Assert.That(_model.Contents("pan"), Is.Empty);
        }

        [Test]
        public void ASecondCatch_DoesNotDisplaceTheFirst()
        {
            Hold(GoodsModelFixtures.Fish());
            _model.Register(GoodsModelFixtures.Fish("fish_b"), "boat");
            Assert.That(_model.Take("fish_b", "boat", false), Is.EqualTo(GoodsResult.SameHandKind));
            Assert.That(_model.LeftHand, Is.EqualTo("fish_a"));
            Assert.That(_model.RightHand, Is.Null);
            Assert.That(_model.Location("fish_b"), Is.EqualTo("boat"));
        }

        [Test]
        public void LegalMaximumCod_UsesBothHands_AndSmallPanRefusesItsLength()
        {
            Hold(GoodsModelFixtures.Fish("cod", 1092, 12000));
            Assert.That(_model.LeftHand, Is.EqualTo("cod"));
            Assert.That(_model.RightHand, Is.EqualTo("cod"));
            Assert.That(_model.Put("cod", "pan"), Is.EqualTo(GoodsResult.DoesNotFit));
            Assert.That(_model.Contents("pan"), Is.Empty);
            Assert.That(_model.RightHand, Is.EqualTo("cod"));
        }

        [Test]
        public void BothHandsRequired_DoesNotSlingTheRod()
        {
            Hold(new GoodsPiece("rod", "goods.rod", GoodsKind.Tool, new GoodsBox(400, 50, 50), 500, false));
            _model.Register(GoodsModelFixtures.Fish("cod", 1092, 12000), "boat");
            Assert.That(_model.Take("cod", "boat", false), Is.EqualTo(GoodsResult.HandsOccupied));
            Assert.That(_model.LeftHand, Is.EqualTo("rod"));
            Assert.That(_model.RightHand, Is.Null);
        }

        [TestCase(1151, 12000)]
        [TestCase(1092, 12001)]
        public void OversizeOrOverweightCatch_RequiresDevice(int length, long mass)
        {
            _model.Register(GoodsModelFixtures.Fish("cod", length, mass), "boat");
            Assert.That(_model.Take("cod", "boat", true), Is.EqualTo(GoodsResult.NeedsDevice));
            Assert.That(_model.LeftHand, Is.Null);
            Assert.That(_model.Location("cod"), Is.EqualTo("boat"));
        }

        [Test]
        public void CountFull_LeavesIncomingCatchHeld_UntilExplicitSupportedSetDown()
        {
            foreach (string id in new[] { "a", "b" })
            {
                Hold(GoodsModelFixtures.Fish(id));
                Assert.That(_model.Put(id, "pan"), Is.EqualTo(GoodsResult.Success));
            }
            Hold(GoodsModelFixtures.Fish("c"));
            Assert.That(_model.Put("c", "pan"), Is.EqualTo(GoodsResult.CountFull));
            Assert.That(_model.LeftHand, Is.EqualTo("c"));
            Assert.That(_model.Contents("pan"), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(_model.SetDown("c", "wharf_floor", false), Is.EqualTo(GoodsResult.NoSupport));
            Assert.That(_model.LeftHand, Is.EqualTo("c"));
            Assert.That(_model.SetDown("c", "wharf_floor", true), Is.EqualTo(GoodsResult.Success));
            Assert.That(_model.Location("c"), Is.EqualTo("wharf_floor"));
            Assert.That(_model.Take("c", "wharf_floor", true), Is.EqualTo(GoodsResult.Success));
            Assert.That(_model.LeftHand, Is.EqualTo("c"));
        }

        [Test]
        public void VolumeCeiling_AlsoRejectsWithFreePlacements()
        {
            _pan.UsableVolumeMl = 2000;
            Hold(GoodsModelFixtures.Fish());
            Assert.That(_model.Put("fish_a", "pan"), Is.EqualTo(GoodsResult.DoesNotFit));
            Assert.That(_model.Contents("pan"), Is.Empty);
        }

        [Test]
        public void ItemMustFitItsAuthoredSlot_NotOnlyTheWholeContainer()
        {
            Hold(GoodsModelFixtures.Fish(length: 400));
            Assert.That(_model.Put("fish_a", "pan"), Is.EqualTo(GoodsResult.DoesNotFit));
            Assert.That(_model.LeftHand, Is.EqualTo("fish_a"));
        }

        [Test]
        public void GrossLimit_IncludesShellAndContents()
        {
            _pan.MaxGrossMassG = 1500;
            Hold(GoodsModelFixtures.Fish());
            Assert.That(_model.Put("fish_a", "pan"), Is.EqualTo(GoodsResult.TooHeavy));
            Assert.That(_model.GrossMassG("pan"), Is.EqualTo(1000));
        }

        [Test]
        public void PortableContainer_KeepsItsContentsWhenCarriedAndSetDown()
        {
            Hold(GoodsModelFixtures.Fish());
            Assert.That(_model.Put("fish_a", "pan"), Is.EqualTo(GoodsResult.Success));
            Assert.That(_model.Take("pan", "boat", true), Is.EqualTo(GoodsResult.Success));
            Assert.That(_model.LeftHand, Is.EqualTo("pan"));
            Assert.That(_model.RightHand, Is.EqualTo("pan"));
            Assert.That(_model.SetDown("pan", "cart_bed", true), Is.EqualTo(GoodsResult.Success));
            Assert.That(_model.Location("fish_a"), Is.EqualTo("pan"));
            Assert.That(_model.GrossMassG("pan"), Is.EqualTo(2000));
        }

        [Test]
        public void LidBlocksPutAndTake_AndAddsItsOwnMass()
        {
            _pan.HasLid = true;
            _pan.LidMassG = 200;
            Hold(GoodsModelFixtures.Fish());
            Assert.That(_model.SetClosed("pan", true), Is.EqualTo(GoodsResult.Success));
            Assert.That(_model.GrossMassG("pan"), Is.EqualTo(1200));
            Assert.That(_model.Put("fish_a", "pan"), Is.EqualTo(GoodsResult.Closed));
            Assert.That(_model.SetClosed("pan", false), Is.EqualTo(GoodsResult.Success));
            Assert.That(_model.Put("fish_a", "pan"), Is.EqualTo(GoodsResult.Success));
            Assert.That(_model.SetClosed("pan", true), Is.EqualTo(GoodsResult.Success));
            Assert.That(_model.Take("fish_a", "pan", true), Is.EqualTo(GoodsResult.Closed));
            Assert.That(_model.Location("fish_a"), Is.EqualTo("pan"));
        }

        [Test]
        public void WrongSourceAndDuplicateIdentity_CannotDuplicateGoods()
        {
            _model.Register(GoodsModelFixtures.Fish(), "boat");
            Assert.Throws<ArgumentException>(() => _model.Register(GoodsModelFixtures.Fish(), "boat"));
            Assert.That(_model.Take("fish_a", "counter", true), Is.EqualTo(GoodsResult.WrongSource));
            Assert.That(_model.Put("fish_a", "pan"), Is.EqualTo(GoodsResult.WrongSource));
            Assert.That(_model.Location("fish_a"), Is.EqualTo("boat"));
            Assert.That(_model.Contents("pan"), Is.Empty);
        }

        [Test]
        public void IncompatibleProfileOrContainer_NeverCreatesNestedStorage()
        {
            Hold(new GoodsPiece("bait", "goods.bait", GoodsKind.Catch, new GoodsBox(100, 100, 100), 500, false));
            Assert.That(_model.Put("bait", "pan"), Is.EqualTo(GoodsResult.Incompatible));
            Assert.That(_model.SetDown("bait", "deck", true), Is.EqualTo(GoodsResult.Success));
            Assert.That(_model.Take("pan", "boat", true), Is.EqualTo(GoodsResult.Success));
            Assert.That(_model.Put("pan", "pan"), Is.EqualTo(GoodsResult.Incompatible));
        }

        [Test]
        public void RepeatedInputs_SelectTheSameSlotOrder()
        {
            for (int cycle = 0; cycle < 3; cycle++)
            {
                SetUp();
                foreach (string id in new[] { "z", "a" })
                {
                    Hold(GoodsModelFixtures.Fish(id));
                    Assert.That(_model.Put(id, "pan"), Is.EqualTo(GoodsResult.Success));
                }
                Assert.That(_model.Contents("pan"), Is.EqualTo(new[] { "z", "a" }));
            }
        }

        [Test]
        public void RegistrationCannotBypassPlacements_OrCreateASupportCycle()
        {
            Assert.Throws<ArgumentException>(() => _model.Register(GoodsModelFixtures.Shell(_pan, "untyped_pan"), "boat"));
            Assert.Throws<ArgumentException>(() => _model.Register(GoodsModelFixtures.Fish(), "pan"));
            Assert.Throws<ArgumentException>(() => _model.Register(GoodsModelFixtures.Fish("boat"), "boat"));
            Assert.Throws<ArgumentException>(() => _model.Register(GoodsModelFixtures.Fish("boat"), "wharf"));
            Assert.Throws<ArgumentException>(() => _model.Register(GoodsModelFixtures.Fish(), "@hands"));
            Assert.That(_model.Contents("pan"), Is.Empty);
        }

        [Test]
        public void FillingAHeldPail_CannotSecretlyTurnItIntoATwoHandLoad()
        {
            var pail = GoodsModelFixtures.Pan();
            pail.Outside = new GoodsBox(600, 450, 160);
            pail.RequiredHands = 1;
            pail.MaxHandGrossMassG = 8000;
            _model.RegisterContainer(GoodsModelFixtures.Shell(pail, "pail"), pail, "boat");
            Assert.That(_model.Take("pail", "boat", true), Is.EqualTo(GoodsResult.Success));
            Hold(GoodsModelFixtures.Fish("heavy", mass: 7500), false);
            Assert.That(_model.Put("heavy", "pail"), Is.EqualTo(GoodsResult.TooHeavy));
            Assert.That(_model.LeftHand, Is.EqualTo("pail"));
            Assert.That(_model.RightHand, Is.EqualTo("heavy"));
            Assert.That(_model.Contents("pail"), Is.Empty);
        }
    }
}
