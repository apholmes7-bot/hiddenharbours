using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// One bubble per speaker. A speaker who already has an ambient bubble up and says something new
    /// gets that bubble re-filled with the new line; a second bubble never pops up beside the first.
    /// Other speakers are untouched, and the pool cap still holds.
    ///
    /// <para>Drives a real <see cref="AmbientSpeechPresenter"/> in EditMode: its private <c>Awake</c>
    /// (which builds the pooled canvas) and its request handler are invoked by reflection, because the
    /// self-installing host only exists in Play mode. The handler is called directly rather than through
    /// the bus so the test does not depend on (or disturb) bus subscriptions; the end signal it publishes
    /// is observed on the bus.</para>
    /// </summary>
    public class OneBubblePerSpeakerTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        GameObject _host;
        AmbientSpeechPresenter _presenter;
        MethodInfo _onRequested;
        AmbientSpeechPresenter _previousInstance;
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<AmbientSpeechEnded> _ends = new List<AmbientSpeechEnded>();

        [SetUp]
        public void SetUp()
        {
            _previousInstance = AmbientSpeechPresenter.Instance;

            _host = new GameObject("OneBubblePerSpeakerTests_Presenter");
            _presenter = _host.AddComponent<AmbientSpeechPresenter>();
            typeof(AmbientSpeechPresenter).GetMethod("Awake", Private).Invoke(_presenter, null);
            _onRequested = typeof(AmbientSpeechPresenter).GetMethod("OnRequested", Private);
            Assert.IsNotNull(_onRequested, "the presenter's request handler was renamed; update this fixture");

            EventBus.Subscribe<AmbientSpeechEnded>(OnEnded);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<AmbientSpeechEnded>(OnEnded);
            _ends.Clear();
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            if (_host != null) Object.DestroyImmediate(_host);

            // Awake claimed the singleton; hand it back to whatever held it before.
            typeof(AmbientSpeechPresenter).GetProperty(nameof(AmbientSpeechPresenter.Instance))
                .SetValue(null, _previousInstance);
        }

        void OnEnded(AmbientSpeechEnded e) => _ends.Add(e);

        Transform Body(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.transform;
        }

        int Say(string line, Transform anchor, string speakerId = "npc.test",
                AmbientSpeechKind kind = AmbientSpeechKind.NpcToNpc)
        {
            int id = AmbientSpeechId.Next();
            var request = new AmbientSpeechRequested(id, anchor, new Vector3(0f, 2f, 0f), speakerId, null,
                                                     line, null, kind);
            _onRequested.Invoke(_presenter, new object[] { request });
            return id;
        }

        [Test]
        public void SameSpeakerTwice_IsOneBubbleCarryingTheNewLine()
        {
            Transform ned = Body("Ned");
            int first = Say("Fog's coming in.", ned);
            int second = Say("Best get the dory up.", ned);

            Assert.That(_presenter.ActiveCount, Is.EqualTo(1), "a second bubble popped up beside the first");
            Assert.IsFalse(_presenter.IsShowing(first), "the old line is still on screen");
            Assert.IsTrue(_presenter.IsShowing(second));
            Assert.That(_presenter.LineOf(second), Is.EqualTo("Best get the dory up."));
            Assert.That(_presenter.VisibleTextOf(second), Is.Empty, "the new line should fill from the start");

            Assert.That(_ends.Count, Is.EqualTo(1), "the replaced line owes exactly one end signal");
            Assert.That(_ends[0].Id, Is.EqualTo(first));
            Assert.That(_ends[0].Reason, Is.EqualTo(AmbientSpeechEndReason.Displaced));
        }

        [Test]
        public void ThePlayersOwnThoughtTwice_IsOneBubble()
        {
            // The inner-voice paths (EffortGate, CookingStationFixture, CleanlinessResponder, the
            // InnerVoiceDirector) publish with no speaker id, anchored on the player.
            Transform player = Body("Player");
            Say("My back's killing me.", player, null, AmbientSpeechKind.InnerVoice);
            int second = Say("Too dark to mend this.", player, null, AmbientSpeechKind.InnerVoice);

            Assert.That(_presenter.ActiveCount, Is.EqualTo(1));
            Assert.That(_presenter.LineOf(second), Is.EqualTo("Too dark to mend this."));
        }

        [Test]
        public void AnAnchorlessThoughtTwice_IsOneBubble()
        {
            Say("Hm.", null, null, AmbientSpeechKind.InnerVoice);
            int second = Say("Right.", null, null, AmbientSpeechKind.InnerVoice);

            Assert.That(_presenter.ActiveCount, Is.EqualTo(1));
            Assert.That(_presenter.LineOf(second), Is.EqualTo("Right."));
        }

        [Test]
        public void TwoSpeakers_AreTwoBubbles_AndAReplacementLeavesTheOtherAlone()
        {
            // Same speaker id on purpose: two bodies are two people even if they share an id.
            Transform a = Body("A");
            Transform b = Body("B");
            int fromA = Say("Some weather.", a);
            int fromB = Say("It is that.", b);

            Assert.That(_presenter.ActiveCount, Is.EqualTo(2));
            Assert.IsTrue(_presenter.IsShowing(fromA));
            Assert.IsTrue(_presenter.IsShowing(fromB));

            int againA = Say("Worse by noon.", a);
            Assert.That(_presenter.ActiveCount, Is.EqualTo(2));
            Assert.IsTrue(_presenter.IsShowing(fromB), "B's bubble was disturbed by A's new line");
            Assert.That(_presenter.LineOf(fromB), Is.EqualTo("It is that."));
            Assert.That(_presenter.LineOf(againA), Is.EqualTo("Worse by noon."));
        }

        [Test]
        public void ThePoolCapStillHolds_AndTheOldestGivesWay()
        {
            int over = AmbientSpeechPresenter.MaxConcurrent + 2;
            var ids = new List<int>();
            for (int i = 0; i < over; i++) ids.Add(Say($"Line {i}.", Body($"Speaker{i}")));

            Assert.That(_presenter.ActiveCount, Is.EqualTo(AmbientSpeechPresenter.MaxConcurrent));
            for (int i = 0; i < over - AmbientSpeechPresenter.MaxConcurrent; i++)
            {
                Assert.IsFalse(_presenter.IsShowing(ids[i]), $"line {i} should have been displaced");
                int id = ids[i];
                Assert.IsTrue(_ends.Exists(e => e.Id == id && e.Reason == AmbientSpeechEndReason.Displaced));
            }
            for (int i = over - AmbientSpeechPresenter.MaxConcurrent; i < over; i++)
                Assert.IsTrue(_presenter.IsShowing(ids[i]), $"line {i} should still be up");
        }

        [Test]
        public void ASpeakerAtTheCap_ReplacesHisOwnBubble_NotTheOldest()
        {
            var bodies = new List<Transform>();
            var ids = new List<int>();
            for (int i = 0; i < AmbientSpeechPresenter.MaxConcurrent; i++)
            {
                bodies.Add(Body($"Speaker{i}"));
                ids.Add(Say($"Line {i}.", bodies[i]));
            }

            Transform last = bodies[bodies.Count - 1];
            int again = Say("And another thing.", last);

            Assert.That(_presenter.ActiveCount, Is.EqualTo(AmbientSpeechPresenter.MaxConcurrent));
            Assert.IsTrue(_presenter.IsShowing(ids[0]), "the oldest bubble was displaced by a speaker who already had one");
            Assert.IsTrue(_presenter.IsShowing(again));
        }
    }
}
