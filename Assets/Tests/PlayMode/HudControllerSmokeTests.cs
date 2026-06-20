using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// VS-17 — a PlayMode smoke test for the always-on HUD: it builds its own Canvas with no prefab,
    /// reflects the live money/payout labels, and asserts they respond to <see cref="MoneyChanged"/>
    /// and <see cref="CatchSold"/> events published on the <see cref="EventBus"/>. This is the
    /// closest automated check to "verify in Play" for the event-driven readouts.
    /// </summary>
    public class HudControllerSmokeTests
    {
        // ---- in-file fakes for the Core contracts -------------------------------------------
        private sealed class FakeClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 6f;
            public float DayFraction => 0.25f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private sealed class FakeEnv : IEnvironmentService
        {
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; } = TideProfile.CoddleCove;
            public EnvironmentSample Sample()
                => new EnvironmentSample(new Vector2(0f, 3f), Vector2.zero, 1.2f, SeaState.Calm, 1f);
            public float TideHeightAt(double totalSeconds) => (float)Mathf.Sin((float)totalSeconds);
        }

        private sealed class FakeWallet : IWallet
        {
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount) { if (amount > Money) return false; Money -= amount; return true; }
        }

        private GameObject _hudGo;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<MoneyChanged>();
            EventBus.Clear<CatchSold>();
            GameServices.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<MoneyChanged>();
            EventBus.Clear<CatchSold>();
            GameServices.Reset();
            if (_hudGo != null) Object.Destroy(_hudGo);
        }

        private static Text Label(HudController hud, string field)
        {
            var f = typeof(HudController).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"field '{field}' not found on HudController");
            return (Text)f.GetValue(hud);
        }

        private HudController MakeHud()
        {
            _hudGo = new GameObject("HUD");
            var hud = _hudGo.AddComponent<HudController>(); // Awake builds the Canvas + labels
            return hud;
        }

        [UnityTest]
        public IEnumerator Hud_BuildsItsOwnCanvas_AndLabels()
        {
            var hud = MakeHud();
            yield return null; // let Awake/OnEnable run

            Assert.IsNotNull(_hudGo.GetComponentInChildren<Canvas>(), "HUD must build its own Canvas");
            Assert.IsNotNull(Label(hud, "_moneyLabel"), "money label should exist");
            Assert.IsNotNull(Label(hud, "_tideLabel"),  "tide label should exist");
            Assert.IsNotNull(Label(hud, "_windLabel"),  "wind label should exist");
        }

        [UnityTest]
        public IEnumerator MoneyChanged_UpdatesTheMoneyLabel()
        {
            GameServices.Clock = new FakeClock();
            GameServices.Environment = new FakeEnv();
            GameServices.Wallet = new FakeWallet();

            var hud = MakeHud();
            yield return null;

            EventBus.Publish(new MoneyChanged(newBalance: 1240, delta: 1240));
            yield return null;

            Assert.AreEqual("₲1,240", Label(hud, "_moneyLabel").text,
                "the money readout should reflect the MoneyChanged balance");
        }

        [UnityTest]
        public IEnumerator CatchSold_FlashesThePayout()
        {
            GameServices.Clock = new FakeClock();
            GameServices.Environment = new FakeEnv();
            GameServices.Wallet = new FakeWallet();

            var hud = MakeHud();
            yield return null;

            var payout = Label(hud, "_payoutLabel");
            Assert.IsFalse(payout.enabled, "payout flash starts hidden");

            EventBus.Publish(new CatchSold(totalPaid: 48, count: 3));
            yield return null;

            Assert.IsTrue(payout.enabled, "a sale should show the payout flash");
            Assert.AreEqual("+₲48", payout.text, "the flash shows the gain with a '+'");
        }

        [UnityTest]
        public IEnumerator BootSafe_NoServices_DoesNotThrow()
        {
            // No GameServices set → !Ready. The HUD must run its Update without throwing.
            var hud = MakeHud();
            yield return null;
            yield return null;
            Assert.IsNotNull(hud, "HUD survives frames with no services wired (boot safety)");
        }
    }
}
