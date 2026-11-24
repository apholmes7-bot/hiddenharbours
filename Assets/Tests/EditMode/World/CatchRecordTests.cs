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
        const string Smelt = "fish.rainbow_smelt";

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
            log.RecordEnteredHold(Catch(Cod, 3.2f), 1);
            log.RecordEnteredHold(Catch(Wolffish, 4f, "Wolffish"), 2);
            log.RecordEnteredHold(Catch(Cod, 7.5f), 3);
            log.RecordEnteredHold(Catch(Cod, 5.1f), 4);

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
            // The stow: the hand gives it up and the hold takes it, in one call on one tick.
            log.RecordTakenFromHand(cod, 5);
            log.RecordEnteredHold(cod, 5);

            Assert.That(log.Entries[0].Count, Is.EqualTo(1));

            log.RecordEnteredHold(Catch(Cod, 2.4f), 5);   // a second fish of the same weight, same tick
            Assert.That(log.Entries[0].Count, Is.EqualTo(2), "the handoff is spent, not reused");
        }

        [Test]
        public void AHandCatchNeverStowedStillCounts()
        {
            var log = new CatchRecordLog();
            log.RecordLandedInHand(Catch(Cod, 2.4f));
            Assert.That(log.Entries[0].Count, Is.EqualTo(1), "sold or given from the hand, it was still caught");
        }

        [Test]
        public void AHandCatchEatenOrGivenNeverSwallowsALaterIdenticalCatch()
        {
            // The reproduced case: a 0.04 kg smelt to the hand, taken from the hand (eaten), then a
            // separate 0.04 kg smelt straight into a hold. That second smelt is a catch.
            var log = new CatchRecordLog();
            CatchItem first = Catch(Smelt, 0.04f, "Rainbow Smelt");

            log.RecordLandedInHand(first);
            log.RecordTakenFromHand(first, 10);   // eaten: nothing enters a hold on this tick

            log.RecordEnteredHold(Catch(Smelt, 0.04f, "Rainbow Smelt"), 11);

            Assert.That(log.Entries[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void ATakeOfSomethingNeverCountedInHandArmsNothing()
        {
            var log = new CatchRecordLog();
            CatchItem cod = Catch(Cod, 2.4f);

            log.RecordTakenFromHand(cod, 3);   // e.g. a catch put in the hand from a hold, then stowed
            log.RecordEnteredHold(cod, 3);

            Assert.That(log.Entries[0].Count, Is.EqualTo(1), "never counted in hand, so the hold counts it");
        }

        [Test]
        public void TheHandMemoryIsBounded()
        {
            var log = new CatchRecordLog();
            for (int i = 0; i < CatchRecordLog.MaxPendingHandCatches + 1; i++)
                log.RecordLandedInHand(Catch(Cod, 1f + i));

            // The oldest was forgotten, so stowing it now arms no handoff and counts it as fresh.
            CatchItem oldest = Catch(Cod, 1f);
            log.RecordTakenFromHand(oldest, 7);
            log.RecordEnteredHold(oldest, 7);
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
            log.RecordEnteredHold(Catch("", 1f), 1);
            log.RecordLandedInHand(Catch(null, 1f));
            log.RecordReleased(Catch(null, 1f));
            Assert.That(log.SpeciesCount, Is.EqualTo(0));
        }

        [Test]
        public void ADifferentGameStartsTheRecordAfresh()
        {
            var log = new CatchRecordLog();
            object gameA = new object(), gameB = new object();

            log.RecordEnteredHold(Catch(Cod, 1f), 1, gameA);
            Assert.That(log.EntriesFor(gameA).Count, Is.EqualTo(1));
            Assert.That(log.EntriesFor(gameB), Is.Empty, "another save never shows this one's catches");
            Assert.That(log.EntriesFor(gameA), Is.Empty, "and the stale record is gone, not hidden");
        }

        [Test]
        public void TheRunningRecordIsFedByTheEvents()
        {
            CatchRecords.Install();
            CatchRecords.Install();   // idempotent: a second install must not double-count

            // All on one EditMode frame, as a stow is on one game frame.
            CatchItem inHand = Catch(Cod, 3f);
            EventBus.Publish(new CatchLanded(inHand));
            EventBus.Publish(new CatchTakenFromHand(inHand));          // the stow: hand cleared...
            EventBus.Publish(new FishCaught(inHand));                  // ...hold entered: not a second catch
            EventBus.Publish(new FishCaught(Catch(Cod, 8f)));          // straight into a hold
            EventBus.Publish(new CatchReleased(Catch(Wolffish, 5f, "Wolffish")));

            var rows = CatchRecords.Current();
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0].Count, Is.EqualTo(2));
            Assert.That(rows[0].RecordWeightKg, Is.EqualTo(8f));
            Assert.That(rows[1].ReleasedCount, Is.EqualTo(1));
        }

        [Test]
        public void SameTickHandoff_RequiresBothSpeciesAndExactWeight()
        {
            var log = new CatchRecordLog();
            var hand = Catch(Cod, 2f);
            log.RecordLandedInHand(hand);
            log.RecordTakenFromHand(hand, 7);
            log.RecordEnteredHold(Catch(Cod, 3f), 7);
            log.RecordEnteredHold(hand, 7);
            Assert.AreEqual(3, log.Entries[0].Count);
            Assert.AreEqual(3f, log.Entries[0].RecordWeightKg);

            log.RecordLandedInHand(hand);
            log.RecordTakenFromHand(hand, 8);
            log.RecordEnteredHold(Catch(Smelt, 2f), 8);
            log.RecordEnteredHold(hand, 8);
            Assert.AreEqual(5, log.Entries[0].Count);
            Assert.AreEqual(1, log.Entries[1].Count);
        }

        [Test]
        public void EqualAndSmallerReleasedCatches_CountWithoutLoweringTheRecord()
        {
            var log = new CatchRecordLog();
            log.RecordEnteredHold(Catch(Cod, 5f), 1);
            log.RecordReleased(Catch(Cod, 5f));
            log.RecordReleased(Catch(Cod, 2f));
            Assert.AreEqual(1, log.SpeciesCount);
            Assert.AreEqual(3, log.Entries[0].Count);
            Assert.AreEqual(2, log.Entries[0].ReleasedCount);
            Assert.AreEqual(5f, log.Entries[0].RecordWeightKg);
        }

        [Test]
        public void MissingName_IsFilledOnce_AndSpeciesIdsRemainCaseSensitive()
        {
            var log = new CatchRecordLog();
            log.RecordEnteredHold(Catch(Cod, 0f, ""), 1);
            log.RecordEnteredHold(Catch(Cod, 2f, "Cod"), 2);
            log.RecordEnteredHold(Catch(Cod, 1f, "Later name"), 3);
            log.RecordEnteredHold(Catch("FISH.ATLANTIC_COD", -1f), 4);
            Assert.AreEqual(2, log.SpeciesCount);
            Assert.AreEqual("Cod", log.Entries[0].DisplayName);
            Assert.AreEqual(3, log.Entries[0].Count);
            Assert.AreEqual(0f, log.Entries[1].RecordWeightKg);
            Assert.IsFalse(log.TryGet("fish.missing", out _));
        }

        [Test]
        public void ChangingSession_ClearsAnArmedHandoff()
        {
            var log = new CatchRecordLog();
            var hand = Catch(Cod, 2f);
            var first = new object();
            var second = new object();
            log.RecordLandedInHand(hand, first);
            log.RecordTakenFromHand(hand, 7, first);
            log.RecordEnteredHold(hand, 7, second);
            Assert.AreEqual(1, log.SpeciesCount);
            Assert.AreEqual(1, log.Entries[0].Count);
            Assert.AreEqual(2f, log.Entries[0].RecordWeightKg);
        }

        [Test]
        public void AnUninstalledRecordHearsNothing()
        {
            CatchRecords.Install();
            CatchRecords.Uninstall();

            EventBus.Publish(new FishCaught(Catch(Cod, 1f)));
            EventBus.Publish(new CatchLanded(Catch(Cod, 2f)));
            EventBus.Publish(new CatchReleased(Catch(Wolffish, 3f)));
            EventBus.Publish(new CatchTakenFromHand(Catch(Cod, 2f)));

            Assert.That(CatchRecords.Current(), Is.Empty);
            Assert.That(CatchRecords.IsInstalled, Is.False);
        }
    }
}
