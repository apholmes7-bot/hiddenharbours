using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class SaveReadyLifetimeTests
    {
        private GameObject _host;
        private IGameClock _previousClock;

        [SetUp]
        public void SetUp()
        {
            ShellFlow.Reset();
            _previousClock = GameServices.Clock;
            GameServices.Clock = null;
            EventBus.Clear<GameLoaded>();
            _host = new GameObject("SaveReady lifetime test");
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            EventBus.Clear<GameLoaded>();
            ShellFlow.Reset();
            GameServices.Clock = _previousClock;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DestroyedHost_DoesNotRunOrSubscribe(bool atTitle)
        {
            if (atTitle) ShellFlow.EnterTitle();
            var host = _host.AddComponent<SaveReadyLifetimeHost>();
            Object.DestroyImmediate(_host);
            int calls = 0;

            SaveReady.Run(host, () => calls++);
            EventBus.Publish(default(GameLoaded));

            Assert.That(calls, Is.Zero);
            Assert.That(EventBus.HasSubscribers<GameLoaded>(), Is.False);
        }

        [Test]
        public void LiveHost_InWorld_RunsImmediatelyWithoutSubscribing()
        {
            int calls = 0;
            SaveReady.Run(_host.AddComponent<SaveReadyLifetimeHost>(), () => calls++);

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(EventBus.HasSubscribers<GameLoaded>(), Is.False);
        }

        [Test]
        public void HostDestroyedWhileWaiting_DropsWorkAndRemovesListener()
        {
            ShellFlow.EnterTitle();
            int calls = 0;
            SaveReady.Run(_host.AddComponent<SaveReadyLifetimeHost>(), () => calls++);
            Assert.That(EventBus.HasSubscribers<GameLoaded>(), Is.True);
            Object.DestroyImmediate(_host);

            EventBus.Publish(default(GameLoaded));

            Assert.That(calls, Is.Zero);
            Assert.That(EventBus.HasSubscribers<GameLoaded>(), Is.False);
        }

        [Test]
        public void NestedLoad_BeforeOuterSnapshotReachesListener_RunsOnce()
        {
            ShellFlow.EnterTitle();
            bool nested = false;
            int calls = 0;
            EventBus.Subscribe<GameLoaded>(_ =>
            {
                if (nested) return;
                nested = true;
                EventBus.Publish(default(GameLoaded));
            });
            SaveReady.Run(_host.AddComponent<SaveReadyLifetimeHost>(), () => calls++);

            EventBus.Publish(default(GameLoaded));
            EventBus.Publish(default(GameLoaded));

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void NullAction_DoesNotSubscribe()
        {
            ShellFlow.EnterTitle();
            SaveReady.Run(_host.AddComponent<SaveReadyLifetimeHost>(), null);
            Assert.That(EventBus.HasSubscribers<GameLoaded>(), Is.False);
        }
    }

    public class SaveReadyLifetimeHost : MonoBehaviour { }
}
