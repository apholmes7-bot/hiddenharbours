using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>THE RECORD OF WHAT WAS LET GO</b>: one row per protected species, counted, with its first and
    /// last release, fed only by the <see cref="CatchReleased"/> event and bound to one game.
    /// </summary>
    public class ReleasedCatchLogTests
    {
        const string Wolffish = "fish.atlantic_wolffish";
        const string Shark = "fish.porbeagle";

        static CatchItem Catch(string id, string name = "Wolffish")
            => new CatchItem(id, name, FishCategory.InshoreGroundfish, 4f, 0, 0f);

        sealed class StubClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            ReleasedCatches.Uninstall();
            ReleasedCatches.Log.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ReleasedCatches.Uninstall();
            ReleasedCatches.Log.Clear();
            GameServices.Reset();
        }

        [Test]
        public void EachSpeciesIsOneRowCountedWithItsFirstAndLastRelease()
        {
            var log = new ReleasedCatchLog();
            log.Record(Catch(Wolffish), 100d);
            log.Record(Catch(Shark, "Porbeagle"), 200d);
            log.Record(Catch(Wolffish), 900d);

            Assert.That(log.SpeciesCount, Is.EqualTo(2));
            Assert.That(log.Entries[0].SpeciesId, Is.EqualTo(Wolffish), "first-release order, nothing sorts");
            Assert.That(log.Entries[0].Count, Is.EqualTo(2));
            Assert.That(log.Entries[0].FirstSeconds, Is.EqualTo(100d));
            Assert.That(log.Entries[0].LastSeconds, Is.EqualTo(900d));
            Assert.That(log.Entries[1].DisplayName, Is.EqualTo("Porbeagle"));
            Assert.That(log.Entries[1].Count, Is.EqualTo(1));
        }

        [Test]
        public void ACatchWithNoSpeciesIsNotFiled()
        {
            var log = new ReleasedCatchLog();
            log.Record(Catch(""), 5d);
            log.Record(Catch(null), 5d);
            Assert.That(log.SpeciesCount, Is.EqualTo(0));
        }

        [Test]
        public void TheFirstNameGivenIsKeptAndAMissingOneIsFilledIn()
        {
            var log = new ReleasedCatchLog();
            log.Record(Catch(Wolffish, ""), 1d);
            log.Record(Catch(Wolffish, "Wolffish"), 2d);
            log.Record(Catch(Wolffish, "Other"), 3d);
            Assert.That(log.Entries[0].DisplayName, Is.EqualTo("Wolffish"));
        }

        [Test]
        public void ADifferentGameStartsTheRecordAfresh()
        {
            var log = new ReleasedCatchLog();
            object gameA = new object(), gameB = new object();

            log.Record(Catch(Wolffish), 1d, gameA);
            Assert.That(log.EntriesFor(gameA).Count, Is.EqualTo(1), "the same game reads its own record");
            Assert.That(log.EntriesFor(gameB), Is.Empty, "another save never shows this one's releases");
            Assert.That(log.EntriesFor(gameA), Is.Empty, "and the stale record is gone, not hidden");

            log.Record(Catch(Shark), 2d, gameB);
            log.Record(Catch(Shark), 3d, gameB);
            Assert.That(log.EntriesFor(gameB)[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void TheRunningRecordIsFedByTheEventAndStampedByTheClock()
        {
            var clock = new StubClock { TotalSeconds = 4500d };
            GameServices.Clock = clock;
            ReleasedCatches.Install();
            ReleasedCatches.Install();   // idempotent: a second install must not double-count

            EventBus.Publish(new CatchReleased(Catch(Wolffish)));
            clock.TotalSeconds = 9000d;
            EventBus.Publish(new CatchReleased(Catch(Wolffish)));

            Assert.That(ReleasedCatches.Current().Count, Is.EqualTo(1));
            Assert.That(ReleasedCatches.Current()[0].Count, Is.EqualTo(2));
            Assert.That(ReleasedCatches.Current()[0].FirstSeconds, Is.EqualTo(4500d));
            Assert.That(ReleasedCatches.Current()[0].LastSeconds, Is.EqualTo(9000d));
        }

        [Test]
        public void AnUninstalledRecordHearsNothing()
        {
            ReleasedCatches.Install();
            ReleasedCatches.Uninstall();

            EventBus.Publish(new CatchReleased(Catch(Wolffish)));

            Assert.That(ReleasedCatches.Current(), Is.Empty);
            Assert.That(ReleasedCatches.IsInstalled, Is.False);
        }

        [Test]
        public void NoClockStillCountsTheRelease()
        {
            ReleasedCatches.Install();
            EventBus.Publish(new CatchReleased(Catch(Wolffish)));

            Assert.That(ReleasedCatches.Current().Count, Is.EqualTo(1));
            Assert.That(ReleasedCatches.Current()[0].FirstSeconds, Is.EqualTo(0d));
        }
    }
}
