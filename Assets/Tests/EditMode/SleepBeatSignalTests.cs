using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The bed's two signals: the save request that #574/#580 shipped, and the presentation beat added
    /// beside it.
    ///
    /// <para><b>What is really under test is that the save half did not move.</b> Adding a pose to the
    /// rest beat is only safe if the day is kept exactly as before — same signal, same fields, and
    /// published BEFORE anything can be shown, so an interrupted or artless beat still saves.</para>
    ///
    /// <para>⚠️ No <c>EventBus.Clear</c>: it unsubscribes every LIVE handler on the channel, not just
    /// this fixture's, and test order is not guaranteed. These subscribe and unsubscribe their own
    /// handlers, which is the only way to leave the bus as it was found.</para>
    /// </summary>
    public class SleepBeatSignalTests
    {
        GameObject _go;
        InteriorBed _bed;
        readonly List<RestSaveRequested> _saves = new();
        readonly List<SleepBeatRequested> _beats = new();

        void OnSave(RestSaveRequested e) => _saves.Add(e);
        void OnBeat(SleepBeatRequested e) => _beats.Add(e);

        [SetUp]
        public void SetUp()
        {
            _saves.Clear();
            _beats.Clear();
            EventBus.Subscribe<RestSaveRequested>(OnSave);
            EventBus.Subscribe<SleepBeatRequested>(OnBeat);

            _go = new GameObject("SleepBeatSignalTests.Bed");
            _go.transform.position = new Vector3(12f, -5f, 3f);
            _bed = _go.AddComponent<InteriorBed>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<RestSaveRequested>(OnSave);
            EventBus.Unsubscribe<SleepBeatRequested>(OnBeat);
            if (_go != null) Object.DestroyImmediate(_go);
            foreach (GameObject go in _rooms) if (go != null) Object.DestroyImmediate(go);
            _rooms.Clear();
        }

        [Test]
        public void TurningIn_PublishesTheSaveRequest_ThenTheBeat()
        {
            _bed.Configure("fixture.test.bed", isPlayerBed: true, "you", "the cabin", 1.2f);

            Assert.IsTrue(_bed.Rest(new Vector2(11f, -6f)), "the player's own bed must accept a rest");
            Assert.AreEqual(1, _saves.Count, "the save request must still fire, exactly once");
            Assert.AreEqual(1, _beats.Count, "the presentation beat must fire, exactly once");
        }

        [Test]
        public void TheBeatCarriesTheMATTRESS_WhileTheSaveCarriesTheWAKESpot()
        {
            // ⚠️ The distinction the whole second signal exists for, and the one that would be quietly
            // lost by adding a field to the save request instead. They are DIFFERENT points on purpose:
            // you wake on your feet at the bedside, and you sleep on the mattress.
            _bed.Configure("fixture.test.bed", isPlayerBed: true, "you", "the cabin", 1.2f);
            var wake = new Vector2(11f, -6f);

            _bed.Rest(wake);

            Assert.AreEqual(wake, _saves[0].WakePosition, "the save must keep the player's own feet");
            Assert.AreEqual(new Vector2(12f, -5f), _beats[0].BedPosition,
                            "the beat must carry the BED's transform, not the wake spot");
            Assert.AreNotEqual(_saves[0].WakePosition, _beats[0].BedPosition,
                               "these are two different points and must not collapse into one");
        }

        [Test]
        public void ARefusedBed_PublishesNeither()
        {
            // Ginny's bed. The refusal is untouched by this arc, and must not leak a sleeping pose.
            _bed.Configure("fixture.test.ginny_bed", isPlayerBed: false, "Ginny", "the cabin", 1.2f);

            Assert.IsFalse(_bed.Rest(new Vector2(11f, -6f)));
            Assert.IsEmpty(_saves, "a refused rest saves nothing");
            Assert.IsEmpty(_beats, "and shows nothing");
        }

        [Test]
        public void TheBeatMirrorsTheBedsStoreyAndPlace()
        {
            _bed.Configure("fixture.test.bed", isPlayerBed: true, "you", "the cabin", 1.2f);
            _bed.Rest(new Vector2(11f, -6f));

            Assert.AreEqual(_bed.Level, _beats[0].Level, "the beat must agree with the bed's own storey");
            Assert.AreEqual("the cabin", _beats[0].Place);
        }

        [Test]
        public void APlaceOfNull_ReadsAsEmpty_RatherThanNull()
        {
            // A presenter formatting a notice must never have to null-check this.
            var beat = new SleepBeatRequested(Vector2.zero, 0, null);
            Assert.AreEqual(string.Empty, beat.Place);
        }

        // =============================================================================
        //  WHICH WAY ROUND THE BED IS
        // =============================================================================

        /// <summary>A room, stood at <paramref name="facing"/>, for the bed to read its frame off.
        /// Nulls throughout: the pillow needs the room's YAW and its SQUASH, and neither is a
        /// renderer.</summary>
        BuildingInterior Room(int facing)
        {
            var go = new GameObject("SleepBeatSignalTests.Room");
            _rooms.Add(go);
            var interior = go.AddComponent<BuildingInterior>();
            interior.Configure(null, null, null, 6.6f, 8.05f, facing, 8,
                               IsoGround.GroundDepthScale, 0.3f, 1.4f);
            return interior;
        }

        readonly List<GameObject> _rooms = new();

        [Test]
        public void TheBeatCarriesTheBedsOwnPillowDeclaration()
        {
            // ⭐ The fix, at the seam. The pose cannot work this out for itself — the bed is the only thing
            // that knows — so it has to travel, and it travels on the presentation half.
            _bed.Configure("fixture.test.bed", isPlayerBed: true, "you", "the cabin", 1.2f,
                           interior: null, pillowSide: PillowSide.MinusY, pillowReachMetres: 0.705f);

            _bed.Rest(new Vector2(11f, -6f));

            Assert.IsTrue(_beats[0].Pillow.IsDeclared, "the beat must carry the declaration");
            Assert.AreEqual(PillowSide.MinusY, _beats[0].Pillow.Side);
            Assert.AreEqual(0.705f, _beats[0].Pillow.ReachMetres, 1e-4f);
        }

        [Test]
        public void TheHeadLandsOnThePillowSide_NotInTheMiddleOfTheMattress()
        {
            _bed.Configure("fixture.test.bed", isPlayerBed: true, "you", "the cabin", 1.2f,
                           interior: null, pillowSide: PillowSide.MinusY, pillowReachMetres: 0.705f);

            _bed.Rest(new Vector2(11f, -6f));

            // The bed sits at (12, −5) and its head end is −y, so the head is BELOW it on the screen by
            // the reach, squashed by the ¾ camera's depth term. Nothing moves across.
            Vector2 head = _beats[0].HeadPosition;
            Assert.AreEqual(12f, head.x, 1e-4f, "a -y pillow moves the head along the room, not across it");
            Assert.AreEqual(-5f - 0.705f * IsoGround.GroundDepthScale, head.y, 1e-4f);
            Assert.AreEqual(head, _bed.HeadWorldPosition,
                            "the bed and its beat must not compute the head two different ways");
        }

        [Test]
        public void TheBedReadsItsFrameOffTheROOMItStandsIn()
        {
            // ⭐ THE DEFECT. The same bed, the same declaration, in a building the builder faced
            // differently — and the sleeper has to turn with the room. The old pose played one fixed
            // heading and was therefore right in exactly one of these eight cases.
            for (int facing = 0; facing < 8; facing++)
            {
                _beats.Clear();
                _bed.Configure("fixture.test.bed", isPlayerBed: true, "you", "the cabin", 1.2f,
                               interior: Room(facing), pillowSide: PillowSide.MinusY,
                               pillowReachMetres: 0.705f);

                _bed.Rest(new Vector2(11f, -6f));

                Assert.IsTrue(_beats[0].Pillow.TryHeadingDegrees(out float heading));
                Assert.AreEqual(0f, Mathf.DeltaAngle(180f + 45f * facing, heading), 1e-3f,
                                $"a -y pillow in a room at facing {facing}");
            }
        }

        [Test]
        public void ABedThatDeclaresNothingCarriesNoPillow_AndItsHeadStaysOnTheBed()
        {
            // Degrades whole: an undeclared bed still saves the day and still shows a beat. What it does
            // NOT do is hand the pose a plausible wrong heading — see PlayerSleepPresenter.HeadingFor.
            _bed.Configure("fixture.test.bed", isPlayerBed: true, "you", "the cabin", 1.2f);

            _bed.Rest(new Vector2(11f, -6f));

            Assert.IsFalse(_beats[0].Pillow.IsDeclared);
            Assert.AreEqual(_beats[0].BedPosition, _beats[0].HeadPosition,
                            "with nothing declared the head stays in the middle of the mattress");
        }

        [Test]
        public void TheOldThreeArgumentBeatStillMeansExactlyWhatItDid()
        {
            // The save half of this arc is untouched and so is every existing publisher: a beat built the
            // way #580 built one carries no pillow and behaves as it always has.
            var beat = new SleepBeatRequested(new Vector2(1f, 2f), 1, "somewhere");
            Assert.IsFalse(beat.Pillow.IsDeclared);
            Assert.AreEqual(new Vector2(1f, 2f), beat.HeadPosition);
        }

        [Test]
        public void ThePoseFollowsTheDeclaration_AndKeepsItsFallbackWithoutOne()
        {
            var go = new GameObject("SleepBeatSignalTests.Sleeper");
            _rooms.Add(go);
            var presenter = go.AddComponent<HiddenHarbours.Player.PlayerSleepPresenter>();

            // Declared → the bed's own bearing, whatever the presenter's serialized number says.
            var turned = new PillowAnchor(PillowSide.MinusY, BedPillow.YawDegreesFor(2, 8), 0.705f,
                                          IsoGround.GroundDepthScale);
            Assert.AreEqual(0f, Mathf.DeltaAngle(270f, presenter.HeadingFor(turned)), 1e-3f,
                            "a -y pillow in a room at facing 2 lies west");

            // Undeclared → the shipped fallback, unchanged. A missing declaration must never cost the
            // player their rest; content validation is what stops one reaching a build.
            Assert.AreEqual(0f, Mathf.DeltaAngle(180f, presenter.HeadingFor(PillowAnchor.None)), 1e-3f);
        }
    }
}
