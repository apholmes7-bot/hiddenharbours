using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>THE ARRIVAL ACTUALLY HAPPENS</b> — the opening driven end to end with a real boat, a real
    /// rigidbody and a real clock, because everything interesting about it is a thing that only exists
    /// while the game is running: she has to answer her helm, carry her way off, come to rest, and hand
    /// the player the controls ashore.
    ///
    /// <para><b>⚠ Driven through the component's own API, never through a keypress.</b> A virtual key
    /// press is undeliverable to the New Input System from a test, and an opening that can only be
    /// started by the shell can only be tested by booting the shell. <c>TryBegin()</c> is public for
    /// exactly this reason — and it is the same call the shell's <c>ShellPhaseChanged</c> makes, so the
    /// test drives the shipping path rather than a test-only one.</para>
    ///
    /// <para><b>⚠ Every wait is on a STATE with a wall-clock ceiling, never on a frame count.</b> Frames
    /// are not time: a fixture that yields sixty times has waited for whatever sixty frames happened to
    /// cost on that machine, which is a different amount of simulated sea on every run.</para>
    ///
    /// <para><b>The route is the fixture's, not the region's.</b> A 30 m run in open water, so the test
    /// costs a second rather than the half-minute the real 200 m approach takes at harbour speed — and
    /// the REGION's route is held against the region's own water by <c>ArrivalOpeningTests</c>, where it
    /// costs nothing. What is under test here is the SEQUENCE.</para>
    /// </summary>
    public class ArrivalOpeningPlayTests
    {
        /// <summary>⚠ Sized against the pacing, not guessed. She enters at 3 m/s and takes the way off
        /// at 0.3 m/s², so 20 m of fixture is about a second at cruise, six seconds decelerating to the
        /// arrive radius, and three more taking the last of it off astern — call it ten. Double it, so a
        /// loaded machine does not produce a red that is really a stopwatch.</summary>
        private const float TimeoutSeconds = 30f;

        private sealed class FakeSave : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
            public SaveData Current { get; } = new SaveData();
            public int Saves;
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool v) && v;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() => Saves++;
        }

        private GameObject _root;
        private GameObject _player;
        private ArrivalOpening _opening;
        private BoatOwnerDef _skipper;

        // Open water, well clear of anything — this fixture is about the sequence, not the coast.
        private static readonly Vector2 Start = new Vector2(0f, 20f);
        private static readonly Vector2 Berth = new Vector2(0f, 0f);
        private static readonly Vector2 Ashore = new Vector2(3f, 0f);

        [SetUp]
        public void SetUp()
        {
            // One listener, so a play-mode scene does not log "there are no audio listeners" on EVERY
            // frame — a full suite writes that line over a million times and turns the log binary.
            _root = new GameObject("ArrivalFixture");
            _root.AddComponent<AudioListener>();

            _player = new GameObject("Player");
            _player.transform.SetParent(_root.transform);
            _player.transform.position = new Vector3(999f, 999f, 0f);   // nowhere near the berth yet
            GameServices.PlayerTransform = _player.transform;

            _skipper = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(
                "Assets/_Project/Data/Boats/Skippers/StPetersArrivalSkipper.asset");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            GameServices.Save = null;
            GameServices.PlayerTransform = null;
            GameServices.Reset();
        }

        private ArrivalOpening Build(bool hasRestAnchor, bool alreadyArrived)
        {
            var save = new FakeSave();
            if (alreadyArrived) save.SetFlag(ArrivalOpening.ArrivedFlagKey, true);
            // The anchor goes on through its own locker, never by writing the four fields — they only
            // mean anything together, which is the reason RestLocker exists (ADR 0037).
            if (hasRestAnchor)
                RestLocker.Stamp(save.Current,
                                 new RestAnchor("region.st_peters", new Vector2(12f, 34f), level: 1));
            GameServices.Save = save;

            var go = new GameObject("ArrivalOpening");
            go.transform.SetParent(_root.transform);
            go.SetActive(false);
            var opening = go.AddComponent<ArrivalOpening>();
            opening.Configure(_skipper, new[] { Start, Berth }, Berth, 180f, Ashore,
                              channelBedElevation: -4f);

            // The region's own pacing, unmodified — the fixture's route is short enough that she enters
            // at cruise and has all of it off by the berth, which is the interesting part of the
            // approach and the only part 20 m can show.
            opening.ConfigurePilot(ArrivalPilot.Settings.Default);

            go.SetActive(true);
            _opening = opening;
            return opening;
        }

        /// <summary>Wait until <paramref name="reached"/> or give up loudly — never a frame count.</summary>
        private IEnumerator Until(System.Func<bool> reached, string what)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!reached() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(reached(),
                $"the arrival never {what} within {TimeoutSeconds:F0} s — it is stuck in " +
                $"{_opening.Current}. " + Where() +
                " (Check the pilot's ease-down against the fixture's route length: a boat that never " +
                "gets inside the arrive radius circles the berth forever.)");
        }

        /// <summary>Where she actually is, for a failure that has to be diagnosable from the log alone —
        /// a timeout that says only "stuck" cannot tell a boat steering the wrong way from one that
        /// never got a throttle.</summary>
        private string Where()
        {
            if (_opening == null || _opening.Boat == null) return "There is no boat.";
            Transform t = _opening.Boat.transform;
            return $"She is at ({t.position.x:F2}, {t.position.y:F2}) heading " +
                   $"{HiddenHarbours.App.ArrivalPilot.HeadingOf(t):F0}°, making " +
                   $"{_opening.Boat.Velocity.magnitude:F2} m/s, throttle {_opening.Boat.Throttle:F2}, " +
                   $"steer {_opening.Boat.Steer:F2}; the berth is {Berth} " +
                   $"({Vector2.Distance(t.position, Berth):F2} m away).";
        }

        // =============================================================================================

        /// <summary>
        /// ⭐ The whole thing: a fresh save is brought in, she is tied up at the berth, and the player
        /// ends up ashore with the controls — which is the only state from which the rest of the game
        /// can start.
        /// </summary>
        [UnityTest]
        public IEnumerator AFreshSave_IsPilotedIn_AndEndsAshoreWithTheControls()
        {
            Assert.IsNotNull(_skipper, "the arrival skipper Def must exist for this to mean anything");
            var opening = Build(hasRestAnchor: false, alreadyArrived: false);

            Assert.IsTrue(opening.TryBegin(), "a fresh save must be brought in");
            Assert.AreEqual(ArrivalOpening.Phase.Approaching, opening.Current,
                "she is under way the moment the arrival begins");
            Assert.IsNotNull(opening.Boat, "…and there is a boat under her");

            // Her helm is being worked — the sequencer holds the SAME control surface the player is
            // about to be handed, so if this is zero the arrival is moving her some other way.
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.Greater(Mathf.Abs(opening.Boat.Throttle), 0f,
                "nobody is working her throttle — the approach is not being driven through the helm");

            yield return Until(() => opening.Current == ArrivalOpening.Phase.Moored ||
                                     opening.Current == ArrivalOpening.Phase.HandedOver, "tied up");

            yield return Until(() => opening.Current == ArrivalOpening.Phase.HandedOver,
                               "handed the controls back");

            // Moored: her way is off and she lies where the berth is.
            Assert.Less(opening.Boat.Velocity.magnitude, 0.5f,
                $"she is still making {opening.Boat.Velocity.magnitude:F2} m/s at her own berth");
            Assert.Less(Vector2.Distance(opening.Boat.transform.position, Berth), 1f,
                $"she came to rest at {(Vector2)opening.Boat.transform.position} rather than at the " +
                $"berth {Berth}");

            // Ashore: the player is ON the landing, and free to walk off it.
            Assert.Less(Vector2.Distance(_player.transform.position, Ashore), 0.01f,
                $"the player was put down at {(Vector2)_player.transform.position}, not on the landing " +
                $"at {Ashore}");

            // And the save now says so, so this never happens to them twice.
            var save = (FakeSave)GameServices.Save;
            Assert.IsTrue(save.GetFlag(ArrivalOpening.ArrivedFlagKey),
                "the arrival did not record itself — the next boot would land the player all over again");
            Assert.Greater(save.Saves, 0,
                "…and it was not written to disk, so a crash on the walk up to Ginny replays the opening");
        }

        /// <summary>⭐ The seam with #580: a player who went to bed somewhere is WOKEN there by
        /// <see cref="RestWakeRestorer"/>, and the arrival must not also move her. Only one thing may
        /// place a loading player.</summary>
        /// <summary>
        /// 🔴 <b>Held off her stop, she is tied up anyway — and the player still gets ashore.</b>
        /// The owner's 2026-08-21 playtest: "alongside … taking the last of it off astern" logged and
        /// "tied up" never did. The stop is a rigidbody reading, and a hull with her bow on the pier's
        /// collider is nudged back every physics step, never under 0.1 m/s. This fixture has no wharf,
        /// so the hold is stood in for by a current that keeps her at 0.3 m/s no matter what the pilot
        /// does — the honest shape of "she cannot settle", not a wall she would simply stop against.
        /// </summary>
        [UnityTest]
        public IEnumerator HeldOffHerStop_SheIsTiedUpAnyway_AndThePlayerStillGetsAshore()
        {
            var opening = Build(hasRestAnchor: false, alreadyArrived: false);
            Assert.IsTrue(opening.TryBegin(), "the arrival must start on a fresh save");

            yield return Until(() => opening.Current == ArrivalOpening.Phase.Docking, "came alongside");

            var body = opening.Boat.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(body, "precondition: the arrival hull is a rigidbody");
            var hold = opening.Boat.gameObject.AddComponent<HoldOffHerStop>();
            hold.Body = body;

            // She must not be allowed to "settle" by the stop arm — the hold is doing its job.
            float probeUntil = Time.time + 1.5f;
            while (Time.time < probeUntil)
            {
                Assert.AreNotEqual(ArrivalOpening.Phase.Moored, opening.Current,
                    "the hold failed: she read STOPPED while being pushed at 0.3 m/s, so this test " +
                    "would pass without the fallback and prove nothing");
                yield return null;
            }

            yield return Until(() => opening.Current == ArrivalOpening.Phase.HandedOver,
                               "tied up by the fallback and handed over");

            Assert.AreEqual(RigidbodyType2D.Kinematic, body.bodyType,
                "tied up means kinematic — the sea moves her, her engine does not");
            Assert.Less(Vector2.Distance(body.position, Berth), 0.5f,
                $"the fallback tie-up must still snap her onto the berth {Berth}; she lies at {body.position}");
            Assert.Less(Vector2.Distance(_player.transform.position, Ashore), 0.01f,
                "the player is put ashore by the fallback exactly as by the stop");
        }

        /// <summary>
        /// 🔴 <b>The passenger is shown no helm</b> — and now BY CONSTRUCTION rather than by a
        /// workaround. The owner's 2026-08-21 playtest saw the helm card, wheel and gauges drawn for a
        /// boat the skipper was steering, because the Core helm slot went to whichever relay enabled
        /// LAST and this hull's did. The opening used to pre-empt that by adding her relay DISABLED;
        /// this test now asserts the opposite of that workaround — her relay is ordinary and ENABLED,
        /// registered like every other hull's, and the slot is STILL empty, because nobody has declared
        /// the passenger to be piloting her.
        ///
        /// <para>The stronger claim is the second half: a live registration that is not granted. That is
        /// the seam working, rather than the one hull that bit us being kept away from it.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ThePassengerIsShownNoHelm_AndTheHelmSlotIsNotTaken()
        {
            GameServices.Helm.Reset();
            var opening = Build(hasRestAnchor: false, alreadyArrived: false);
            Assert.IsTrue(opening.TryBegin(), "the arrival must start on a fresh save");
            yield return null;   // Awake/OnEnable on the spawned hull have run
            yield return null;

            var relay = opening.Boat.GetComponent<HelmControlRelay>();
            Assert.IsNotNull(relay, "BoatController still self-installs a relay on every hull");
            Assert.IsTrue(relay.enabled,
                "no workaround left: her relay is an ordinary, enabled one. If a future change disables " +
                "it again, this test stops proving what it is here to prove");
            Assert.AreEqual(1, GameServices.Helm.RegisteredCount,
                "and it really did register — this is a LIVE request that loses, not an absent one");

            Assert.IsNull(GameServices.HelmControl,
                "the passenger is not piloting her, so no relay holds the helm — the card, the wheel and " +
                "the gauges all draw nothing");
            Assert.IsNull(GameServices.HelmInstruments, "and neither does the glass");
            Assert.IsFalse(relay.IsPlayerHelm,
                "asked directly, her own relay says the same: an engine hull under way (HasHelm) that " +
                "the player is not at the helm of");
            Assert.IsTrue(relay.HasHelm,
                "⚠ and HasHelm IS true — the two questions are genuinely different, which is why reading " +
                "the engine one as the player one drew a passenger somebody else's wheel");
        }

        /// <summary>A current no pilot can take off: whatever she does, she makes 0.3 m/s along +x.
        /// Runs in FixedUpdate after the controller so it has the last word on the body each step.</summary>
        private sealed class HoldOffHerStop : MonoBehaviour
        {
            public Rigidbody2D Body;
            private void FixedUpdate()
            {
                if (Body == null || Body.bodyType != RigidbodyType2D.Dynamic) return;
                if (Body.linearVelocity.sqrMagnitude < 0.09f) Body.linearVelocity = new Vector2(0.3f, 0f);
            }
        }

        [UnityTest]
        public IEnumerator APlayerWithARestAnchor_IsLeftToTheWakeRestorer()
        {
            var opening = Build(hasRestAnchor: true, alreadyArrived: false);
            Vector3 wherePlayerWas = _player.transform.position;

            Assert.IsFalse(opening.TryBegin(),
                "she turned in somewhere — landing her on the wharf would undo #580");
            Assert.AreEqual(ArrivalOpening.Phase.Dormant, opening.Current);
            Assert.IsNull(opening.Boat, "no boat should have been spawned for a save that already lives here");

            // Give it a few frames to prove it stays declined rather than starting late.
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(ArrivalOpening.Phase.Dormant, opening.Current);
            Assert.AreEqual(wherePlayerWas, _player.transform.position,
                "the player was moved by an arrival that was not supposed to run");
        }

        /// <summary>…and for the player who has landed but has never slept — an hour ashore, a save on
        /// disk (a shop wrote it), and no anchor. Gating on the anchor alone would pick her up off
        /// Ginny's doorstep and put her back on a boat.</summary>
        [UnityTest]
        public IEnumerator AnAlreadyArrivedSave_IsNotLandedAgain()
        {
            var opening = Build(hasRestAnchor: false, alreadyArrived: true);
            Assert.IsFalse(opening.TryBegin(),
                "the flag says this player has already been brought in");
            Assert.AreEqual(ArrivalOpening.Phase.Dormant, opening.Current);
            yield return null;
        }
    }
}
