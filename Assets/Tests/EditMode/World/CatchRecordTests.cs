using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>THE CATCH RECORD</b>: one row per species, counted once per catch whichever way it came up,
    /// with the heaviest weight; released protected fish count as caught; bound to one game.
    /// </summary>
    public class CatchRecordTests
    {
        const string Cod = "fish.atlantic_cod";
        const string Wolffish = "fish.atlantic_wolffish";

        static CatchItem Catch(string id, float kg, string name = "Cod")
            => new CatchItem(id, name, FishCategory.InshoreGroundfish, kg, 0, 0f);

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            CatchRecords.Uninstall();
            CatchRecords.Log.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            CatchRecords.Uninstall();
            CatchRecords.Log.Clear();
            GameServices.Reset();
        }

        [Test]
        public void EachSpeciesIsOneRowCountedWithItsHeaviestWeight()
        {
            var log = new CatchRecordLog();
            log.RecordEnteredHold(Catch(Cod, 3.2f));
            log.RecordEnteredHold(Catch(Wolffish, 4f, "Wolffish"));
            log.RecordEnteredHold(Catch(Cod, 7.5f));
            log.RecordEnteredHold(Catch(Cod, 5.1f));

            Assert.That(log.SpeciesCount, Is.EqualTo(2));
            Assert.That(log.Entries[0].SpeciesId, Is.EqualTo(Cod), "first-caught order, nothing sorts");
            Assert.That(log.Entries[0].Count, Is.EqualTo(3));
            Assert.That(log.Entries[0].RecordWeightKg, Is.EqualTo(7.5f), "the record is the heaviest");
            Assert.That(log.Entries[0].ReleasedCount, Is.EqualTo(0));
            Assert.That(log.Entries[1].DisplayName, Is.EqualTo("Wolffish"));
        }

        [Test]
        public void AHandCatchStowedInAHoldIsCountedOnce()
        {
            var log = new CatchRecordLog();
            CatchItem cod = Catch(Cod, 2.4f);

            log.RecordLandedInHand(cod);
            log.RecordEnteredHold(cod);   // the same item, moved from her hand into the pail

            Assert.That(log.Entries[0].Count, Is.EqualTo(1));

            log.RecordEnteredHold(Catch(Cod, 2.4f));   // a second fish of the same weight, straight to the hold
            Assert.That(log.Entries[0].Count, Is.EqualTo(2), "the hand match is spent, not reused");
        }

        [Test]
        public void AHandCatchNeverStowedStillCounts()
        {
            var log = new CatchRecordLog();
            log.RecordLandedInHand(Catch(Cod, 2.4f));
            Assert.That(log.Entries[0].Count, Is.EqualTo(1), "sold or given from the hand, it was still caught");
        }

        [Test]
        public void TheHandMemoryIsBounded()
        {
            var log = new CatchRecordLog();
            for (int i = 0; i < CatchRecordLog.MaxPendingHandCatches + 1; i++)
                log.RecordLandedInHand(Catch(Cod, 1f + i));

            // The oldest was forgotten, so stowing it now counts it as a fresh catch.
            log.RecordEnteredHold(Catch(Cod, 1f));
            Assert.That(log.Entries[0].Count, Is.EqualTo(CatchRecordLog.MaxPendingHandCatches + 2));
        }

        [Test]
        public void AReleasedProtectedFishIsCaughtAndReleased()
        {
            var log = new CatchRecordLog();
            log.RecordReleased(Catch(Wolffish, 6f, "Wolffish"));
            log.RecordReleased(Catch(Wolffish, 9f, "Wolffish"));

            Assert.That(log.Entries[0].Count, Is.EqualTo(2));
            Assert.That(log.Entries[0].ReleasedCount, Is.EqualTo(2));
            Assert.That(log.Entries[0].RecordWeightKg, Is.EqualTo(9f));
        }

        [Test]
        public void ACatchWithNoSpeciesIsNotFiled()
        {
            var log = new CatchRecordLog();
            log.RecordEnteredHold(Catch("", 1f));
            log.RecordLandedInHand(Catch(null, 1f));
            log.RecordReleased(Catch(null, 1f));
            Assert.That(log.SpeciesCount, Is.EqualTo(0));
        }

        [Test]
        public void ADifferentGameStartsTheRecordAfresh()
        {
            var log = new CatchRecordLog();
            object gameA = new object(), gameB = new object();

            log.RecordEnteredHold(Catch(Cod, 1f), gameA);
            Assert.That(log.EntriesFor(gameA).Count, Is.EqualTo(1));
            Assert.That(log.EntriesFor(gameB), Is.Empty, "another save never shows this one's catches");
            Assert.That(log.EntriesFor(gameA), Is.Empty, "and the stale record is gone, not hidden");
        }

        [Test]
        public void TheRunningRecordIsFedByTheThreeEvents()
        {
            CatchRecords.Install();
            CatchRecords.Install();   // idempotent: a second install must not double-count

            CatchItem inHand = Catch(Cod, 3f);
            EventBus.Publish(new CatchLanded(inHand));
            EventBus.Publish(new FishCaught(inHand));                 // stowed: not a second catch
            EventBus.Publish(new FishCaught(Catch(Cod, 8f)));          // straight into a hold
            EventBus.Publish(new CatchReleased(Catch(Wolffish, 5f, "Wolffish")));

            var rows = CatchRecords.Current();
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0].Count, Is.EqualTo(2));
            Assert.That(rows[0].RecordWeightKg, Is.EqualTo(8f));
            Assert.That(rows[1].ReleasedCount, Is.EqualTo(1));
        }

        [Test]
        public void AnUninstalledRecordHearsNothing()
        {
            CatchRecords.Install();
            CatchRecords.Uninstall();

            EventBus.Publish(new FishCaught(Catch(Cod, 1f)));
            EventBus.Publish(new CatchLanded(Catch(Cod, 2f)));
            EventBus.Publish(new CatchReleased(Catch(Wolffish, 3f)));

            Assert.That(CatchRecords.Current(), Is.Empty);
            Assert.That(CatchRecords.IsInstalled, Is.False);
        }
    }
}
