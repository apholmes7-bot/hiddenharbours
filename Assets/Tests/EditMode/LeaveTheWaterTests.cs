using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Climbing out of a swim onto a deck ends the swim. The on-foot water band is published on
    /// transitions only and the walk stops ticking while the player is aboard, so before this nothing
    /// ever said "dry": the sea rinse (which ends on that edge) kept washing him as he stood on deck.
    /// <see cref="PlayerWalkController.LeaveTheWater"/> is what the walk's disable now calls.
    /// </summary>
    public class LeaveTheWaterTests
    {
        private readonly List<OnFootWaterStateChanged> _events = new List<OnFootWaterStateChanged>();
        private GameObject _go;
        private PlayerWalkController _walk;

        private static readonly FieldInfo State =
            typeof(PlayerWalkController).GetField("_waterState", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Init =
            typeof(PlayerWalkController).GetField("_waterStateInit", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<OnFootWaterStateChanged>();
            EventBus.Subscribe<OnFootWaterStateChanged>(OnWater);
            _events.Clear();
            _go = new GameObject("LeaveTheWaterTests.Player");
            _walk = _go.AddComponent<PlayerWalkController>();   // auto-adds Rigidbody2D + SpriteRenderer
            Assert.IsNotNull(State, "the walk's water band field");
            Assert.IsNotNull(Init, "the walk's water band init flag");
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<OnFootWaterStateChanged>(OnWater);
            EventBus.Clear<OnFootWaterStateChanged>();
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private void OnWater(OnFootWaterStateChanged e) => _events.Add(e);

        private void Put(OnFootWaterState state)
        {
            State.SetValue(_walk, state);
            Init.SetValue(_walk, true);
        }

        [TestCase(OnFootWaterState.Swim)]
        [TestCase(OnFootWaterState.Wade)]
        public void LeavingTheWater_AnnouncesDryOnce(OnFootWaterState from)
        {
            Put(from);
            _walk.LeaveTheWater();

            Assert.AreEqual(OnFootWaterState.Dry, _walk.WaterState);
            Assert.AreEqual(1, _events.Count, "one edge back to dry");
            Assert.AreEqual(OnFootWaterState.Dry, _events[0].State);
            Assert.AreEqual(from, _events[0].Previous);
            Assert.IsFalse(_events[0].Deepening);

            _walk.LeaveTheWater();
            Assert.AreEqual(1, _events.Count, "already dry: nothing more is said");
        }

        [Test]
        public void AlreadyDry_OrNeverPlaced_SaysNothing()
        {
            _walk.LeaveTheWater();
            Assert.AreEqual(0, _events.Count, "never placed: no band to leave");

            Put(OnFootWaterState.Dry);
            _walk.LeaveTheWater();
            Assert.AreEqual(0, _events.Count, "dry on the planks: no edge");
        }
    }
}
