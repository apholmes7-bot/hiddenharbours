using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// v12 → v13: WHERE THE PLAYER WENT TO SLEEP (ADR 0037). Four appended fields, and the three
    /// obligations of a schema bump — an older save survives untouched, the new fields mean the right
    /// thing when they are absent, and the thing they were added for actually works.
    ///
    /// <para><b>The one worth writing down is the STOREY.</b> A rest anchor that carried only a region
    /// and a position would pass every test a reviewer would think to write and still be wrong, because
    /// ADR 0036 made a second floor a LAYER over the same footprint: the game's one authored player bed
    /// is upstairs at Ginny's, directly above her front room, so a position-only anchor wakes the player
    /// in the wrong room at exactly the right coordinates. <c>ARestUpstairs_IsRememberedAsUpstairs</c>
    /// is what stops that being written back out.</para>
    /// </summary>
    public class SaveMigrationV13Tests
    {
        const string StP = "region.st_peters";
        const string NMC = "region.nine_mile_creek";
        const string PlayersBed = "your bed at Ginny's";

        static SaveData V12(string json = null) =>
            json == null
                ? new SaveData { SchemaVersion = 12 }
                : JsonUtility.FromJson<SaveData>(json);

        /// <summary>A save service whose write gate behaves like the real one's in the only respect the
        /// rest contract reads: <see cref="ISaveService.LoadedExistingSave"/> goes true once a write has
        /// landed.</summary>
        sealed class FakeSaveService : ISaveService
        {
            public int Saves;
            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool LoadedExistingSave { get; private set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { Saves++; LoadedExistingSave = true; }
        }

        GameObject _player;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<GameSaved>();
            EventBus.Clear<PlayerWokeAt>();
            EventBus.Clear<GameLoaded>();
            GameServices.Save = null;
            GameServices.CurrentRegionId = null;
            GameServices.PlayerTransform = null;
        }

        [TearDown]
        public void TearDown()
        {
            RestSaveResponder.Uninstall();
            RestWakeRestorer.Uninstall();
            GameServices.Save = null;
            GameServices.CurrentRegionId = null;
            GameServices.PlayerTransform = null;
            EventBus.Clear<GameSaved>();
            EventBus.Clear<PlayerWokeAt>();
            EventBus.Clear<GameLoaded>();
            if (_player != null) { Object.DestroyImmediate(_player); _player = null; }
        }

        /// <summary>Stand a player rig somewhere and publish it, at a non-zero Z so the restorer's
        /// promise to leave the depth alone is actually testable.</summary>
        Transform StandPlayerAt(Vector2 at)
        {
            _player = new GameObject("player");
            _player.transform.position = new Vector3(at.x, at.y, -3.5f);
            GameServices.PlayerTransform = _player.transform;
            return _player.transform;
        }

        // =============================================================================
        //  the schema
        // =============================================================================

        /// <summary>
        /// <b>The one deliberately version-EXACT test in this file</b>, and the only one the next schema
        /// bump should have to touch. Everything else below asserts
        /// <see cref="SaveMigration.CurrentVersion"/> wherever it means "migrated to current", so a v14
        /// lands without reddening a single one of them.
        ///
        /// <para>That convention is not free advice — it was paid for here. This file's own bump turned
        /// FIVE <c>SaveMigrationV12Tests</c> cases red at once, purely because they pinned <c>== 12</c>
        /// where they meant "current", while <c>SaveMigrationV11Tests</c> (which used
        /// <c>CurrentVersion</c>) passed untouched. ✅ <b>Done for v14</b> (the fuel tank, 2026-08-20):
        /// the assertion below is now a <c>GreaterOrEqual</c>, and it was the ONLY case in this file
        /// that had to move — which is the convention paying for itself. Whoever writes v15 should
        /// find nothing here to change.</para>
        /// </summary>
        [Test]
        public void CurrentVersion_IsAtLeastThirteen()
        {
            // Converted from `AreEqual(13, ...)` when v14 (the fuel tank) landed — exactly as this file's
            // own remarks above instruct, and for the reason they give: this assertion means "the rest
            // anchor moved the schema", not "the schema stops here", and pinning equality makes every
            // future bump red a test that has nothing to do with it.
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 13,
                "The rest anchor appended four fields to SaveData, so the schema moved.");
        }

        [Test]
        public void ANewGame_HasNeverRested()
        {
            var data = SaveMigration.NewGame();

            Assert.AreEqual(SaveMigration.CurrentVersion, data.SchemaVersion);
            Assert.AreEqual("", data.RestRegion,
                "An empty region reads as 'has never turned in', under which the player wakes at the " +
                "authored spawn — which is what a new game must do.");
            Assert.IsFalse(RestLocker.Anchor(data).IsSet);
        }

        [Test]
        public void AV12Save_GainsNoAnchor_AndIsStampedCurrent()
        {
            var data = SaveMigration.Migrate(V12());

            Assert.AreEqual(SaveMigration.CurrentVersion, data.SchemaVersion);
            Assert.AreEqual("", data.RestRegion,
                "A v12 save was written when a bed did nothing but write the file. Nobody is " +
                "retroactively put to bed somewhere they never slept.");
            Assert.IsFalse(RestLocker.Anchor(data).IsSet,
                "so it wakes at the spawn, exactly as it did before this field existed");
        }

        [Test]
        public void AV12Save_KeepsEverythingElseItHad()
        {
            var before = new SaveData
            {
                SchemaVersion = 12,
                WorldSeed = 4242,
                GameTimeSeconds = 98765.5,
                Money = 913,
                DayIndex = 7,
                ActiveHullId = "boat.punt",
                WornOutfitId = "outfit.oilskins",
            };
            before.OwnedBoats.Add("boat.dory");
            before.OwnedLicenses.Add("license.cod");

            var after = SaveMigration.Migrate(before);

            Assert.AreEqual(4242, after.WorldSeed);
            Assert.AreEqual(98765.5, after.GameTimeSeconds, 1e-9);
            Assert.AreEqual(913, after.Money);
            Assert.AreEqual(7, after.DayIndex);
            Assert.AreEqual("boat.punt", after.ActiveHullId);
            Assert.AreEqual("outfit.oilskins", after.WornOutfitId);
            CollectionAssert.AreEqual(new[] { "boat.dory" }, after.OwnedBoats);
            CollectionAssert.AreEqual(new[] { "license.cod" }, after.OwnedLicenses);
        }

        [Test]
        public void AJsonBlobWithNoAnchorFields_ReadsAsNeverRested()
        {
            // Exactly what a v12 file on disk looks like: the fields simply are not there.
            var data = SaveMigration.Migrate(V12("{\"SchemaVersion\":12,\"Money\":50}"));

            Assert.AreEqual(SaveMigration.CurrentVersion, data.SchemaVersion);
            Assert.AreEqual(50, data.Money);
            Assert.IsNotNull(data.RestRegion, "An absent string field must never surface as null.");
            Assert.AreEqual("", data.RestRegion);
            Assert.AreEqual(0, data.RestLevel);
        }

        [Test]
        public void AV13Save_KeepsTheBedItWasWrittenWith()
        {
            var data = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = 13,
                RestRegion = StP, RestPosX = 12.5f, RestPosY = -3.25f, RestLevel = 1,
            });

            RestAnchor anchor = RestLocker.Anchor(data);
            Assert.IsTrue(anchor.IsSet, "Migrating a current save must not put the player out of bed.");
            Assert.AreEqual(StP, anchor.Region);
            Assert.AreEqual(new Vector2(12.5f, -3.25f), anchor.Position);
            Assert.AreEqual(1, anchor.Level);
        }

        [Test]
        public void ItRoundTripsThroughJson()
        {
            var before = SaveMigration.NewGame();
            RestLocker.Stamp(before, new RestAnchor(StP, new Vector2(-8.75f, 41.5f), 1));

            var after = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(before));

            Assert.AreEqual(SaveMigration.CurrentVersion, after.SchemaVersion);
            RestAnchor anchor = RestLocker.Anchor(after);
            Assert.AreEqual(StP, anchor.Region);
            Assert.AreEqual(new Vector2(-8.75f, 41.5f), anchor.Position);
            Assert.AreEqual(1, anchor.Level);
        }

        /// <summary>
        /// The migrator drops what cannot be a position, and repairs nothing else. A NaN has no honest
        /// replacement — putting it at the origin would wake the player in the corner of the map with
        /// nothing in the file to say why — so the anchor is cleared and they wake at the spawn, which
        /// is a place the game already knows how to be.
        /// </summary>
        [Test]
        public void AnUnreadablePosition_IsDropped_NotMovedToTheOrigin()
        {
            var data = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = 12,
                RestRegion = StP, RestPosX = float.NaN, RestPosY = 4f, RestLevel = 1,
            });

            Assert.AreEqual("", data.RestRegion, "an anchor that cannot be read is not an anchor");
            Assert.IsFalse(RestLocker.Anchor(data).IsSet);
        }

        /// <summary>
        /// ...and the mirror of it: an anchor that is merely SUSPICIOUS is left completely alone. A
        /// region no scene claims today may be a region that loads tomorrow, and a loader that erased it
        /// would throw the player's bed away on the one build where the content failed to import. This
        /// is the same line the v11→v12 step draws around an unresolvable outfit id.
        /// </summary>
        [Test]
        public void ARegionTheLoaderCannotVouchFor_IsNotHealedAway()
        {
            var data = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = 12,
                RestRegion = "region.somewhere_a_later_drop_removed",
                RestPosX = 3f, RestPosY = 4f, RestLevel = 1,
            });

            Assert.AreEqual("region.somewhere_a_later_drop_removed", data.RestRegion,
                "Declining belongs at the point of use, where it costs one session instead of the save.");
        }

        // =============================================================================
        //  the locker over the top of it
        // =============================================================================

        [Test]
        public void TheLocker_ReportsNoAnchorForABareOrAbsentSave()
        {
            Assert.IsFalse(RestLocker.Anchor(new SaveData()).IsSet);
            Assert.IsFalse(RestLocker.Anchor((SaveData)null).IsSet,
                "No save is 'never rested', not a null the caller has to guard.");
        }

        [Test]
        public void TheLocker_ReportsWhetherSomethingActuallyMoved()
        {
            var save = SaveMigration.NewGame();
            var bed = new RestAnchor(StP, new Vector2(2f, 3f), 1);

            Assert.IsTrue(RestLocker.Stamp(save, bed));
            Assert.IsFalse(RestLocker.Stamp(save, bed),
                "Turning in twice in the same bed is not a change.");

            Assert.IsTrue(RestLocker.Stamp(save, new RestAnchor(StP, new Vector2(2f, 3f), 0)),
                "Same place, different STOREY, is a different bed — and the one this feature exists for.");
        }

        /// <summary>
        /// An unset anchor is refused rather than written, and that asymmetry is deliberate: a caller
        /// that could not answer the question must not be able to erase an answer that was already good.
        /// </summary>
        [Test]
        public void TheLocker_RefusesAnUnsetAnchorRatherThanErasingAGoodOne()
        {
            var save = SaveMigration.NewGame();
            RestLocker.Stamp(save, new RestAnchor(StP, new Vector2(2f, 3f), 1));

            Assert.IsFalse(RestLocker.Stamp(save, RestAnchor.None));
            Assert.IsFalse(RestLocker.Stamp(save, new RestAnchor("", new Vector2(9f, 9f), 0)));

            Assert.AreEqual(StP, RestLocker.Anchor(save).Region, "the bed survives both");
            Assert.AreEqual(new Vector2(2f, 3f), RestLocker.Anchor(save).Position);

            Assert.IsTrue(RestLocker.Forget(save), "erasure has its own door, and this is it");
            Assert.IsFalse(RestLocker.Anchor(save).IsSet);
            Assert.IsFalse(RestLocker.Forget(save), "forgetting twice is not a change");
        }

        // =============================================================================
        //  the write path — what the responder stamps
        // =============================================================================

        [Test]
        public void TheResponder_StampsTheRegionOntoTheBedsOwnSpotAndStorey()
        {
            var request = new RestSaveRequested(PlayersBed, new Vector2(-2.1f, -2.3f), 1);

            RestAnchor anchor = RestSaveResponder.AnchorFor(request, StP);

            Assert.IsTrue(anchor.IsSet);
            Assert.AreEqual(StP, anchor.Region, "the region comes from Core — a bed has no idea");
            Assert.AreEqual(new Vector2(-2.1f, -2.3f), anchor.Position, "the spot comes from the request");
            Assert.AreEqual(1, anchor.Level, "and so does the storey");
        }

        [Test]
        public void TheResponder_RecordsNothingWhenNoRegionHasReported()
        {
            RestAnchor anchor = RestSaveResponder.AnchorFor(
                new RestSaveRequested("somewhere", new Vector2(5f, 5f), 0), null);

            Assert.IsFalse(anchor.IsSet,
                "EditMode, a bare region scene, a test rig — contexts with no honest answer. Guessing " +
                "one would put a real save's bed in a region that does not exist.");
        }

        [Test]
        public void ARest_WritesTheAnchorIntoTheBlobBeforeTheFileIsWritten()
        {
            var fake = new FakeSaveService();
            GameServices.Save = fake;
            GameServices.CurrentRegionId = StP;
            RestSaveResponder.Install();

            EventBus.Publish(new RestSaveRequested(PlayersBed, new Vector2(-2.1f, -2.3f), 1));

            Assert.AreEqual(1, fake.Saves, "the write still happens");
            RestAnchor anchor = RestLocker.Anchor(fake.Current);
            Assert.IsTrue(anchor.IsSet, "and the blob it wrote already knew where the bed was");
            Assert.AreEqual(StP, anchor.Region);
            Assert.AreEqual(1, anchor.Level);
        }

        [Test]
        public void ARest_InAContextWithNoRegion_StillSavesTheDayAndLeavesTheAnchorAlone()
        {
            var fake = new FakeSaveService();
            RestLocker.Stamp(fake.Current, new RestAnchor(StP, new Vector2(1f, 2f), 1));
            GameServices.Save = fake;
            GameServices.CurrentRegionId = null;
            RestSaveResponder.Install();

            EventBus.Publish(new RestSaveRequested("nowhere in particular"));

            Assert.AreEqual(1, fake.Saves, "the day is still kept");
            Assert.AreEqual(StP, RestLocker.Anchor(fake.Current).Region,
                "and the bed the player DOES have is not thrown away by a rest that could not name one");
        }

        // =============================================================================
        //  the read path — what the restorer does with it
        // =============================================================================

        [Test]
        public void ARestUpstairs_IsRememberedAsUpstairs_AndWakesThere()
        {
            var fake = new FakeSaveService();
            GameServices.Save = fake;
            GameServices.CurrentRegionId = StP;
            RestSaveResponder.Install();
            RestWakeRestorer.Install();

            // Turn in at the player's bed: upstairs at Ginny's, at the spot they walked to.
            EventBus.Publish(new RestSaveRequested(PlayersBed, new Vector2(-2.1f, -2.3f), 1));

            // ...come back to a fresh session standing at the authored spawn, and load.
            Transform player = StandPlayerAt(new Vector2(120f, 40f));
            var wakes = new List<PlayerWokeAt>();
            EventBus.Subscribe<PlayerWokeAt>(wakes.Add);

            RestAnchor applied = RestWakeRestorer.Wake();

            Assert.IsTrue(applied.IsSet, "the anchor was honoured");
            Assert.AreEqual(-2.1f, player.position.x, 1e-4f);
            Assert.AreEqual(-2.3f, player.position.y, 1e-4f);
            Assert.AreEqual(-3.5f, player.position.z, 1e-4f,
                "Z is the rig's own; the sort band reads Y and the anchor never knew a depth.");

            Assert.AreEqual(1, wakes.Count, "and the wake is announced exactly once");
            Assert.AreEqual(1, wakes[0].Anchor.Level,
                "carrying the STOREY, which is the whole reason BuildingInterior can open upstairs");
        }

        [Test]
        public void APlayerWhoHasNeverRested_WakesAtTheSpawn_Quietly()
        {
            GameServices.Save = new FakeSaveService();
            GameServices.CurrentRegionId = StP;
            Transform player = StandPlayerAt(new Vector2(120f, 40f));

            var wakes = new List<PlayerWokeAt>();
            EventBus.Subscribe<PlayerWokeAt>(wakes.Add);

            Assert.IsFalse(RestWakeRestorer.Wake().IsSet);
            Assert.AreEqual(new Vector3(120f, 40f, -3.5f), player.position, "nothing moved");
            Assert.AreEqual(0, wakes.Count,
                "and nothing was announced — a wake signal means a wake HAPPENED");
        }

        /// <summary>
        /// The scope line this slice draws, asserted rather than described. An anchor in another region
        /// is DECLINED and SAID OUT LOUD — never applied. Silence is the one unacceptable outcome:
        /// St Peters coordinates applied inside Nine Mile Creek put the player in the sea, and a player
        /// waking at the spawn cannot otherwise tell that from "the anchor was never written".
        /// </summary>
        [Test]
        public void AnAnchorInAnotherRegion_IsDeclinedOutLoud_AndTheSaveIsLeftIntact()
        {
            var fake = new FakeSaveService();
            RestLocker.Stamp(fake.Current, new RestAnchor(StP, new Vector2(-2.1f, -2.3f), 1));
            GameServices.Save = fake;
            GameServices.CurrentRegionId = NMC;
            Transform player = StandPlayerAt(new Vector2(500f, 900f));

            LogAssert.Expect(LogType.Warning, new Regex("RestWakeRestorer.*st_peters.*nine_mile_creek"));

            Assert.IsFalse(RestWakeRestorer.Wake().IsSet);
            Assert.AreEqual(new Vector3(500f, 900f, -3.5f), player.position,
                "the player stays where this region put them");
            Assert.AreEqual(StP, RestLocker.Anchor(fake.Current).Region,
                "and the bed is KEPT — declining is a read-side choice, never a write");
        }

        [Test]
        public void NoPlayerPublished_IsCarryOn_NeverAThrow()
        {
            var fake = new FakeSaveService();
            RestLocker.Stamp(fake.Current, new RestAnchor(StP, new Vector2(1f, 2f), 0));
            GameServices.Save = fake;
            GameServices.CurrentRegionId = StP;
            GameServices.PlayerTransform = null;

            Assert.IsFalse(RestWakeRestorer.Wake().IsSet,
                "EditMode, a bare art scene, a region opened for review — all valid, none a fault");
        }

        [Test]
        public void TheRestorerInstallsOnce_SoOneLoadIsOneWake()
        {
            var fake = new FakeSaveService();
            RestLocker.Stamp(fake.Current, new RestAnchor(StP, new Vector2(1f, 2f), 0));
            GameServices.Save = fake;
            GameServices.CurrentRegionId = StP;
            StandPlayerAt(Vector2.zero);

            var wakes = new List<PlayerWokeAt>();
            EventBus.Subscribe<PlayerWokeAt>(wakes.Add);

            RestWakeRestorer.Install();
            RestWakeRestorer.Install();          // a service that survives quit-to-title can be re-awoken
            Assert.IsTrue(RestWakeRestorer.Installed);

            EventBus.Publish(new GameLoaded());

            Assert.AreEqual(1, wakes.Count, "two subscriptions would wake the player twice for one load");
        }
    }
}
