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
    }
}
