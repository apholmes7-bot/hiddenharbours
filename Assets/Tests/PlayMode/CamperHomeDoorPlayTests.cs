using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>WALKING UP TO THE CAMPER AND TRYING THE DOOR</b> — the live half of the home purchase, which
    /// EditMode cannot reach: the component's own lifecycle (<c>OnEnable</c> arms
    /// <see cref="StallReach"/>; EditMode never fires it), the per-frame key read, and above all the
    /// PROXIMITY GATE. The EditMode suite calls <see cref="HomeDoor.Interact"/> directly and therefore
    /// proves nothing about whether standing on the other side of the island can buy you a house.
    ///
    /// <para>The camper stays a SHELL here as everywhere: the walk-up beat this pins is "buy it, then be
    /// told honestly that the inside is not built", because that is the beat the player actually gets.</para>
    /// </summary>
    public class CamperHomeDoorPlayTests
    {
        const string HomeId = "home.play_camper";
        static readonly Vector3 DoorPos = new Vector3(0f, 0f, 0f);
        static readonly Vector3 FarAway = new Vector3(80f, 0f, 0f);

        private sealed class FakeWallet : IWallet
        {
            public FakeWallet(int starting) { Money = starting; }
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        private sealed class FlagSaveService : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new();
            public FlagSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => _flags.TryGetValue(key, out var v) && v;
            public void SetFlag(string key, bool value) { if (!string.IsNullOrEmpty(key)) _flags[key] = value; }
            public void Save() { }
        }

        private readonly List<Object> _spawned = new();
        private readonly List<HomePurchased> _events = new();

        private GameObject _player;
        private HomeDoor _door;
        private FakeWallet _wallet;
        private Keyboard _testKeyboard;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _events.Clear();
            EventBus.Clear<HomePurchased>();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Subscribe<HomePurchased>(_events.Add);

            GameServices.Save = new FlagSaveService(new SaveData());
            _wallet = new FakeWallet(1000);
            GameServices.Wallet = _wallet;

            // StallReach resolves the on-foot player by NAME when no explicit ref is wired — that is the
            // whole point of the pattern (no builder wiring), so the fixture stands one up called
            // "Player" exactly as the region does.
            _player = Spawn("Player");
            _player.transform.position = FarAway;

            // ⚠ An AudioListener, because a listener-less play scene logs a warning EVERY FRAME and a
            // full suite writes it well over a million times.
            Spawn("Listener").AddComponent<AudioListener>();

            var home = ScriptableObject.CreateInstance<HomeDef>();
            home.Id = HomeId;
            home.DisplayName = "Play-test camper";
            home.Price = 400;
            home.Acquisition = HomeDef.Mode.Purchasable;
            home.Variant = "clipper";
            _spawned.Add(home);

            // ⚠ INACTIVE → Configure → SetActive: a component configured after OnEnable has already run
            // is a different object from one the builder made, and that difference has hidden real bugs
            // in this repo before.
            var doorGo = new GameObject("Camper");
            doorGo.SetActive(false);
            _spawned.Add(doorGo);
            doorGo.transform.position = DoorPos;
            _door = doorGo.AddComponent<HomeDoor>();
            _door.Configure(home, interiorStood: false);
            doorGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_testKeyboard != null && _testKeyboard.added) InputSystem.RemoveDevice(_testKeyboard);
            _testKeyboard = null;

            // Cleared rather than unsubscribed: `_events.Add` is a fresh delegate each time it is
            // written, so an Unsubscribe with the same method group would not match the subscription.
            EventBus.Clear<HomePurchased>();
            EventBus.Clear<ControlModeChanged>();
            GameServices.Reset();

            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        /// <summary>
        /// 🔴 <b>You cannot buy a house from across the island.</b> The most expensive purchase in the
        /// opening, gated on standing at its door — and the gate only exists at runtime, because
        /// <c>OnEnable</c> is what arms it.
        /// </summary>
        [UnityTest]
        public IEnumerator PressingTheKeyFromAcrossTheIsland_BuysNothing()
        {
            _testKeyboard = InputSystem.AddDevice<Keyboard>();
            yield return null;

            // Direct state write, not a queued event: an unfocused batchmode app drops keyboard EVENTS
            // for non-background devices, but InputState.Change lands regardless.
            InputState.Change(_testKeyboard, new KeyboardState(Key.F));
            yield return null;
            if (!_testKeyboard.fKey.isPressed)
                Assert.Ignore("virtual key press not deliverable here (unfocused headless input) — the " +
                              "gate itself is covered by TheDoorIsOnlyInReachWhileYouAreStandingAtIt, " +
                              "which needs no keyboard");

            yield return null;
            Assert.AreEqual(1000, _wallet.Money, "a press from 80 m away bought the camper");
            Assert.IsFalse(_door.IsOwned);
            Assert.IsEmpty(_events);
        }

        /// <summary>
        /// The beat as the player gets it: walk up, press the key, the camper is yours — and press it
        /// again and it tells you the truth about the inside rather than doing nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingUpToTheDoorAndPressingTheKey_BuysTheCamper_WhichIsStillAShell()
        {
            _testKeyboard = InputSystem.AddDevice<Keyboard>();
            yield return null;

            _player.transform.position = DoorPos;       // standing at the step
            yield return null;

            InputState.Change(_testKeyboard, new KeyboardState(Key.F));
            yield return null;
            if (!_testKeyboard.fKey.isPressed)
                Assert.Ignore("virtual key press not deliverable here (unfocused headless input)");

            yield return null;
            Assert.AreEqual(600, _wallet.Money, "the price did not come out of the purse");
            Assert.IsTrue(_door.IsOwned, "standing at the door and pressing the key did not buy it");
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual(HomeDoor.Outcome.Bought, _door.LastOutcome);

            // Release and press again — the shell answer, and NOT a second charge.
            InputState.Change(_testKeyboard, new KeyboardState());
            yield return null;
            InputState.Change(_testKeyboard, new KeyboardState(Key.F));
            yield return null;
            yield return null;

            Assert.AreEqual(HomeDoor.Outcome.OwnedButNotEnterable, _door.LastOutcome,
                "an owned camper with no room baked must say so — it is a shell, and the door is honest");
            Assert.AreEqual(600, _wallet.Money, "the door charged twice for one camper");
            Assert.AreEqual(1, _events.Count);
        }

        /// <summary>
        /// 🔴 <b>THE PROXIMITY GATE ITSELF, without a keyboard.</b> The two tests above self-ignore on
        /// this machine — a virtual key press is not deliverable to an unfocused headless editor, which
        /// is why three other PlayMode classes in this repo carry the same escape hatch — so THIS is the
        /// test that actually holds the rule "you cannot buy a house from across the island" here.
        ///
        /// <para>It also pins that the gate is LIVE state and not a one-shot: <see cref="StallReach"/>
        /// caches the player transform on first resolve, so a gate that answered from a stale position
        /// would pass a naive walk-up test and fail the walk-away.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheDoorIsOnlyInReachWhileYouAreStandingAtIt()
        {
            yield return null;

            Assert.IsFalse(_door.PlayerIsAtTheDoor,
                "the door is in reach from 80 m away — the proximity gate is not gating");

            _player.transform.position = DoorPos;
            yield return null;
            Assert.IsTrue(_door.PlayerIsAtTheDoor,
                "the door is out of reach while the player is standing on its step");

            _player.transform.position = FarAway;
            yield return null;
            Assert.IsFalse(_door.PlayerIsAtTheDoor,
                "walking away left the door in reach — StallReach is answering from a cached position");
        }

        /// <summary>
        /// Buying is idempotent across the walk-away and back: the second try is the shell answer, not a
        /// second charge. Driven through <see cref="HomeDoor.Interact"/> rather than the key so it runs
        /// where a virtual press does not — the double-charge guard is the subject, the keyboard is not.
        /// </summary>
        [UnityTest]
        public IEnumerator BuyingThenLeavingAndComingBack_NeverChargesTwice()
        {
            yield return null;

            _player.transform.position = DoorPos;
            yield return null;
            Assert.AreEqual(HomeDoor.Outcome.Bought, _door.Interact());

            _player.transform.position = FarAway;
            yield return null;
            _player.transform.position = DoorPos;
            yield return null;

            Assert.AreEqual(HomeDoor.Outcome.OwnedButNotEnterable, _door.Interact(),
                "the second try must be the shell answer — the camper is already his");
            Assert.AreEqual(600, _wallet.Money, "walking away and back re-charged for the camper");
            Assert.AreEqual(1, _events.Count);
        }
    }
}
