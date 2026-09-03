using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>THE ARRIVAL ACTUALLY HAPPENS</b> — the opening driven end to end with a real boat, a real
    /// rigidbody and a real clock, because everything interesting about it is a thing that only exists
    /// while the game is running: she has to answer her helm, carry her way off, come alongside, and hand
    /// the player the controls after the player has asked for them.
    ///
    /// <para><b>⚠ Driven through the component's own API, never through a keypress.</b> A virtual key
    /// press is undeliverable to the New Input System from a test, and an opening that can only be
    /// started by the shell can only be tested by booting the shell. <c>TryBegin()</c> and
    /// <c>StepAshore()</c> are public for exactly this reason — and they are the same calls the shell's
    /// <c>ShellPhaseChanged</c> and the interact verb make, so the test drives the shipping path rather
    /// than a test-only one.</para>
    ///
    /// <para><b>⚠ Every wait is on a STATE with a wall-clock ceiling, never on a frame count.</b> Frames
    /// are not time: a fixture that yields sixty times has waited for whatever sixty frames happened to
    /// cost on that machine, which is a different amount of simulated sea on every run.</para>
    ///
    /// <para><b>The route and the come-alongside are the fixture's, not the region's.</b> A 60 m run in
    /// open water with a tightened-up gate, so the test costs seconds rather than the minute the real
    /// 146 m approach takes — and the REGION's own numbers are held against the region's own water by
    /// <c>ArrivalOverRealTerrainPlayTests</c>. What is under test here is the SEQUENCE.</para>
    ///
    /// <para><b>⛔ And what it now asserts instead of the two snaps.</b> These tests used to prove the
    /// teleports: <i>she came to rest within a metre of the berth</i> (true because <c>TieUp</c> wrote her
    /// there) and <i>the player was put down within a centimetre of the landing</i> (true because
    /// <c>HandOver</c> wrote her there). Both writes are deleted, so both assertions have been turned into
    /// the honest question: did the PILOT produce that pose, and did the PLAYER walk there.</para>
    /// </summary>
    public class ArrivalOpeningPlayTests
    {
        /// <summary>⚠ Sized against the pacing, not guessed. She enters at 5 m/s over a 60 m fixture,
        /// sheds to the fixture's berthing speed, runs the gate and then her own length alongside — call
        /// it thirty seconds, plus the settling beat and the step. Doubled, so a loaded machine does not
        /// produce a red that is really a stopwatch.</summary>
        private const float TimeoutSeconds = 75f;

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
        // ⚠ 60 m rather than the old 20: the come-alongside needs the gate's capture range and a hull
        // length after it, and a route shorter than the manoeuvre cannot show the manoeuvre.
        private static readonly Vector2 Start = new Vector2(0f, 60f);
        private static readonly Vector2 Berth = new Vector2(0f, 0f);
        private const float BerthHeading = 180f;                  // she lies pointing south
        private static readonly Vector2 Ashore = new Vector2(3f, 0f);   // the planks: east of the berth

        /// <summary>
        /// The fixture's come-alongside: the shipped shape at tightened numbers, so a manoeuvre that takes
        /// half a minute at the region's tuning takes a few seconds here. Every ratio the test cares about
        /// (a gate astern and off the line, a capped set rate, a pose tolerance) is preserved.
        /// </summary>
        private static BerthPilot.Settings FixtureAlongside()
        {
            BerthPilot.Settings s = BerthPilot.Settings.Default;
            s.BerthingSpeedMetresPerSecond = 2f;     // a brisk berthing speed, so the leg is seconds
            s.SetRateMetresPerSecond = 0.8f;         // …and a set rate that can close in the same seconds
            s.GateStandoffMetres = 1.5f;
            s.GateCaptureMetres = 8f;
            return s;
        }

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

            // ⭐ THE WHARF SHE TIES UP TO. One bollard, which is what the region's own wharf places for
            // every mooring fitting it carries — without it there is nothing to make a line fast to, and
            // "the lines take the last half-metre" cannot be asserted at all.
            //
            // ⚠ Set BACK from the landing on purpose. The honest scope is the SPAN at the berth pose, and
            // BoatMooring clamps it into the config's [MinScope, MaxScope]: a bollard right at her rail
            // gives a 0.86 m span, which clamps UP to the 2 m minimum and leaves the line permanently
            // slack — a fixture in which the rope can never demonstrate holding anything.
            MooringCleats.Clear();
            var bollard = new GameObject("Bollard");
            bollard.transform.SetParent(_root.transform);
            bollard.transform.position = new Vector3(Ashore.x + 2f, Ashore.y, 0f);
            bollard.AddComponent<ShoreCleat>().Configure("fixture.bollard", elevationMeters: 1.5f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            MooringCleats.Clear();
            Interactables.Clear();
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
            opening.Configure(_skipper, new[] { Start, Berth }, Berth, BerthHeading, Ashore,
                              channelBedElevation: -4f);

            // The region's own pacing, unmodified — the fixture's route is long enough that she enters at
            // cruise and has all of it off by the gate, which is the interesting part of the approach.
            opening.ConfigurePilot(ArrivalPilot.Settings.Default);
            opening.ConfigureAlongside(FixtureAlongside());

            // ⚠ ON DECK for this fixture, deliberately. The shipped opening now starts the player BELOW in
            // the skipper's cabin and she comes up through his aft door — but this suite's own remarks say
            // what it is for: "What is under test here is the SEQUENCE." Leaving her below would make every
            // wait in it a wait on a door press the sequence has nothing to do with. The cabin, the door
            // and the journey through them are IntroCabinPassagePlayTests', which drives this same
            // component from below and then runs this same sequence out to the planks.
            opening.ConfigureCabin(startBelowDecks: false);

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
                $"{_opening.Current}/{_opening.Pilotage}. " + Where() +
                " (Check the pilot's ease-down against the fixture's route length: a boat that never " +
                "gets inside the gate's capture range circles the berth forever.)");
        }

        /// <summary>Where she actually is, for a failure that has to be diagnosable from the log alone —
        /// a timeout that says only "stuck" cannot tell a boat steering the wrong way from one that
        /// never got a throttle.</summary>
        private string Where()
        {
            if (_opening == null || _opening.Boat == null) return "There is no boat.";
            Transform t = _opening.Boat.transform;
            return $"She is at ({t.position.x:F2}, {t.position.y:F2}) heading " +
                   $"{ArrivalPilot.HeadingOf(t):F0}° (the berth lies on {BerthHeading:F0}°), making " +
                   $"{_opening.Boat.Velocity.magnitude:F2} m/s, throttle {_opening.Boat.Throttle:F2}, " +
                   $"steer {_opening.Boat.Steer:F2}; the berth is {Berth} " +
                   $"({Vector2.Distance(t.position, Berth):F2} m away), the gate {_opening.ApproachGate}.";
        }

        /// <summary>Run her in and then, once the wharf is offered, press the exit key. Split out because
        /// EVERY finish goes through the player's own press now — there is no auto-handover to wait
        /// for.</summary>
        private IEnumerator PutHerAshore(ArrivalOpening opening)
        {
            yield return Until(() => opening.CanStepAshore, "tied up and offered the planks");
            Assert.IsTrue(opening.StepAshore(), "the offer was standing but the press was refused");
            yield return Until(() => opening.Current == ArrivalOpening.Phase.HandedOver,
                               "landed and handed the controls back");
        }

        // =============================================================================================

        /// <summary>
        /// ⭐ The whole thing: a fresh save is brought in, she comes alongside <b>on her own helm</b>, her
        /// line goes over, and the player — when SHE asks — steps ashore with the controls.
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

            yield return Until(() => opening.Current == ArrivalOpening.Phase.Moored, "tied up");

            // ⚠ …and then let the LINES finish. They are the last of the manoeuvre (§2.2), so the pose
            // is measured once they have come up taut rather than on the frame they went over.
            // CanStepAshore is that moment as a state: moored, settled, planks offered.
            yield return Until(() => opening.CanStepAshore, "settled on her lines");

            // ⭐ …and she got there UNDER HER OWN HELM. The settle fallback can also end in a tie-up, and
            // with the snap gone the two are told apart only by this flag: honest means
            // BerthingPilot.ReadyForLines said alongside, stopped and in the pose.
            Assert.IsTrue(opening.TiedUpHonestly,
                "she was tied up by the settle fallback rather than by the come-alongside — the hull " +
                "never got herself into her pose. " + Where());

            // ⛔ THE POSE, PRODUCED RATHER THAN WRITTEN. This is the assertion the snap used to satisfy
            // for free: TieUp wrote her onto the berth, so "she is at the berth" was true of a boat that
            // had sailed past it sideways. Nothing writes her pose now, so every one of these is a claim
            // about what the PILOT did.
            Transform hull = opening.Boat.transform;
            BerthPilot.Berth pose = BerthPilot.Berth.FromShorePoint(
                Berth, BerthHeading, Ashore, _skipper.Boat.LengthMeters);
            float lateral = BerthPilot.LateralOffset(hull.position, pose);
            float along = BerthPilot.AlongTrackTo(hull.position, Berth, BerthHeading);
            float heading = ArrivalPilot.Wrap180(ArrivalPilot.HeadingOf(hull) - BerthHeading);

            Assert.Less(opening.Boat.Velocity.magnitude, 0.5f,
                $"she is still making {opening.Boat.Velocity.magnitude:F2} m/s at her own berth. " + Where());

            // ⭐ ALONGSIDE is a claim about the LATERAL offset, and it is the come-alongside's whole
            // product: a fender's gap off her line, not a distance to a point.
            Assert.Less(Mathf.Abs(lateral), 1.5f,
                $"she came to rest {lateral:F2} m off her berth LINE — that is not alongside, and it is " +
                "exactly what the snap used to cover up. " + Where());

            // …and along the wharf she stops inside the pilot's own stop band (plus a margin for the
            // astern settle), which is where a boat asked for a standstill actually comes to rest.
            Assert.Less(Mathf.Abs(along), ArrivalPilot.Settings.Default.StopMetres + 2f,
                $"she stopped {along:F2} m short of (or past) her berth along the wharf. " + Where());

            Assert.Less(Mathf.Abs(heading), BerthPilot.Settings.Default.HeadingToleranceDegrees + 5f,
                $"she is lying {heading:F0}° ACROSS her berth rather than alongside it — the heading is " +
                "the half of a pose a mark cannot give you, and it is what the snap was faking. " + Where());

            // ⭐ …and what holds her there is the LINE.
            Assert.IsTrue(opening.LinesAreFast,
                "she is tied up and no line is made fast — MooringLineMath is the constraint that " +
                "replaced the snap, so a tie-up without one is the snap having simply been deleted");

            // 🔴 SHE STAYS ABOARD. The owner's Q1 ruling: no timer hands her over.
            Vector3 aboard = _player.transform.position;
            float watchUntil = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < watchUntil)
            {
                Assert.AreNotEqual(ArrivalOpening.Phase.HandedOver, opening.Current,
                    "something handed the player over without her asking — the hand-over is hers now");
                yield return null;
            }
            Assert.Less(Vector2.Distance(_player.transform.position, aboard), 1.5f,
                "she was moved off the deck without a press; the passenger rides until she says so");

            // …and then she asks, and walks off.
            yield return PutHerAshore(opening);

            Assert.Less(Vector2.Distance(_player.transform.position, Ashore), 0.1f,
                $"she stepped ashore and landed at {(Vector2)_player.transform.position} rather than on " +
                $"the planks at {Ashore}");

            // And the save now says so, so this never happens to them twice.
            var save = (FakeSave)GameServices.Save;
            Assert.IsTrue(save.GetFlag(ArrivalOpening.ArrivedFlagKey),
                "the arrival did not record itself — the next boot would land the player all over again");
            Assert.Greater(save.Saves, 0,
                "…and it was not written to disk, so a crash on the walk up to Ginny replays the opening");
        }

        /// <summary>
        /// 🔴 <b>SHE CAN ACTUALLY TURN — and for the whole life of this opening she could not.</b>
        ///
        /// <para>The arrival used to size her collider to the hull's real 4.77 × 12.9 m while every boat
        /// the player sails carries <c>PersistentCoreBuilder</c>'s fixed 1.7 × 4.0 m capsule. Unity
        /// derives a rigidbody's moment of inertia from its collider and inertia goes as the SQUARE of
        /// the dimensions, so the arrival hull's was ten times the player's: 51.5 N·m of rudder against
        /// <c>I ≈ 946</c> and <c>angularDamping 2.5</c> is <b>1.25 °/s</b> — a 177 m turning radius on a
        /// 12.9 m boat. She could not round St Peters' 65° fairway corner, so she never did: she ran
        /// straight through it, passed the berth 22 m off, and the SNAP put her on her berth. The green
        /// test was measuring the teleport.</para>
        ///
        /// <para><b>So this asserts the property, not the mechanism.</b> Full helm, cruise, measure her.
        /// A collider change, a mass change, a rudder retune or a damping change all move this number,
        /// and any of them that makes her a barge again fails here rather than four minutes later in a
        /// timeout that says only "stuck".</para>
        /// </summary>
        [UnityTest]
        public IEnumerator SheRunsInUnderHerNavigationLights_AndDousesThemWhenHerLinesGoFast()
        {
            // ⭐⭐ THE REGRESSION THIS EXISTS FOR (boat-lights PR 2a, ADR 0016). Her hull is built with a
            // MooredBoat — deliberately, because that component is the game's DRAWER: it skins her,
            // stands her skipper on the deck and puts her on the published sea. It is NOT a claim that
            // she is moored, and ArrivalOpening's own comment says so ("She is not moored yet"). But its
            // default is a berth's, because that is what it is for everywhere else — so a lamp regime
            // that read "she has a MooredBoat" as "she is lying still" would put the intro's whole light
            // show out and show an anchor light instead. That is exactly what the owner's 2026-08-27
            // ruling asked for and exactly what would have shipped: measured live at 06:13 before this
            // was pinned, she was showing a cabin glow and an anchor light, with her beam dark.
            var opening = Build(hasRestAnchor: false, alreadyArrived: false);
            Assert.IsTrue(opening.TryBegin(), "a fresh save must be brought in");
            yield return new WaitForFixedUpdate();

            var boat = opening.Boat;
            Assert.IsNotNull(boat, "precondition: there is a hull under her");
            var drawer = boat.GetComponent<MooredBoat>();
            Assert.IsNotNull(drawer, "precondition: her drawer is a MooredBoat — that is the point");

            Assert.AreEqual(VesselWay.UnderWay, drawer.Way,
                "she is running in before dawn: her sidelights, stern light and masthead say so, and " +
                "her searchlight is working the water ahead. Anything else and the intro's light show " +
                "— the thing the ruling is about — does not happen.");

            yield return Until(() => opening.Current == ArrivalOpening.Phase.Moored, "her lines to go fast");

            Assert.AreEqual(VesselWay.Moored, drawer.Way,
                "and the moment she is secured they go out, and an anchor light comes on. This is the " +
                "regime's only live transition in the shipped game, and it is the one worth having.");
        }

        [UnityTest]
        public IEnumerator SheTurnsLikeTheBoatThePlayerIsAboutToBeHanded()
        {
            var opening = Build(hasRestAnchor: false, alreadyArrived: false);
            Assert.IsTrue(opening.TryBegin());
            yield return new WaitForFixedUpdate();

            // Take the pilot's hand off the helm and put it hard over ourselves, so this measures the
            // HULL rather than whatever the approach happens to be asking for.
            opening.enabled = false;
            var boat = opening.Boat;
            Assert.IsNotNull(boat, "precondition: there is a hull to measure");

            float from = ArrivalPilot.HeadingOf(boat.transform);
            float began = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - began < 4f)
            {
                boat.SetControl(0f, 1f);                      // full helm, no throttle change
                yield return new WaitForFixedUpdate();
            }

            float turned = Mathf.Abs(ArrivalPilot.Wrap180(ArrivalPilot.HeadingOf(boat.transform) - from));
            float rate = turned / (Time.realtimeSinceStartup - began);

            Assert.Greater(rate, 4f,
                $"she turned {turned:F1}° in {Time.realtimeSinceStartup - began:F1} s — {rate:F2}°/s — at " +
                "FULL helm. The shipping capsule gives about 12.5°/s and a 17.7 m turning radius; a " +
                "hull-sized collider gives 1.25°/s and 177 m, and a boat that cannot turn cannot be " +
                "piloted anywhere. Check the collider on the spawned hull against " +
                "PersistentCoreBuilder's 1.7 x 4.0 m capsule.");
        }

        /// <summary>
        /// 🔴 <b>THE STEP ASHORE IS A MOVE, NOT A TELEPORT.</b> <c>HandOver</c> used to open with
        /// <c>_player.position = _stepAshore</c> and the passenger simply appeared on the planks. The
        /// deletion is only honest if what replaced it takes TIME and passes through the space between —
        /// so this watches the move happen rather than only checking where she ended up.
        /// </summary>
        [UnityTest]
        public IEnumerator TheStepAshoreIsAMove_NotARelocatedTeleport()
        {
            var opening = Build(hasRestAnchor: false, alreadyArrived: false);
            Assert.IsTrue(opening.TryBegin());

            yield return Until(() => opening.CanStepAshore, "tied up and offered the planks");
            Vector3 fromTheDeck = _player.transform.position;
            Assert.Greater(Vector2.Distance(fromTheDeck, Ashore), 0.5f,
                "precondition: she must actually be out on the deck, or there is no gap to cross");

            Assert.IsTrue(opening.StepAshore(), "the press must start the move");
            Assert.IsTrue(opening.IsSteppingAshore, "…and the move must be in the air, not already landed");
            Assert.AreNotEqual(ArrivalOpening.Phase.HandedOver, opening.Current,
                "the controls come back when she LANDS, not when she presses");

            // Somewhere in between: neither where she started nor where she is going.
            bool caughtMidAir = false;
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (opening.Current != ArrivalOpening.Phase.HandedOver
                   && Time.realtimeSinceStartup < deadline)
            {
                float travelled = Vector2.Distance(_player.transform.position, fromTheDeck);
                float remaining = Vector2.Distance(_player.transform.position, Ashore);
                if (travelled > 0.05f && remaining > 0.05f) caughtMidAir = true;
                yield return null;
            }

            Assert.AreEqual(ArrivalOpening.Phase.HandedOver, opening.Current,
                "the move never landed — " + Where());
            Assert.IsTrue(caughtMidAir,
                "she was never observed between the deck and the planks: whatever put her ashore did it " +
                "in one frame, which is the teleport with a press in front of it");
            Assert.Less(Vector2.Distance(_player.transform.position, Ashore), 0.1f,
                "…and the landing is the move's end, not a corrective write");
        }

        /// <summary>
        /// 🔴 <b>Held off her stop, she is tied up anyway — and NOT snapped onto her berth.</b>
        /// The owner's 2026-08-21 playtest: "alongside … taking the last of it off astern" logged and
        /// "tied up" never did. The stop is a rigidbody reading, and a hull with her bow on the pier's
        /// collider is nudged back every physics step, never under 0.1 m/s. This fixture has no wharf, so
        /// the hold is stood in for by a current that keeps her at 0.3 m/s no matter what the pilot does.
        ///
        /// <para>⛔ What changed: the fallback used to end in the same snap as the good path, so a boat
        /// tied up by the stopwatch was indistinguishable from one that had arrived. It ties her up WHERE
        /// SHE IS now — the assertion below is that her body was never written, and the arrival still
        /// finishes.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator HeldOffHerStop_SheIsTiedUpWhereSheIs_AndNeverSnappedOntoTheBerth()
        {
            var opening = Build(hasRestAnchor: false, alreadyArrived: false);
            Assert.IsTrue(opening.TryBegin(), "the arrival must start on a fresh save");

            yield return Until(() => opening.Current == ArrivalOpening.Phase.Docking, "came in to dock");

            var body = opening.Boat.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(body, "precondition: the arrival hull is a rigidbody");
            var hold = opening.Boat.gameObject.AddComponent<HoldOffHerStop>();
            hold.Body = body;

            // She must not be allowed to "settle" by the stop arm — the hold is doing its job.
            float probeUntil = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < probeUntil)
            {
                Assert.AreNotEqual(ArrivalOpening.Phase.Moored, opening.Current,
                    "the hold failed: she read STOPPED while being pushed at 0.3 m/s, so this test " +
                    "would pass without the fallback and prove nothing");
                yield return null;
            }

            yield return Until(() => opening.Current == ArrivalOpening.Phase.Moored,
                               "tied up by the settle fallback");

            // ⛔ NOT SNAPPED. Her body is wherever the sea and the hold left it — no write put her on the
            // berth, and the proof is that she is still being shoved and still moving.
            Vector2 whereSheWasLeft = body.position;
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.AreNotEqual(RigidbodyType2D.Kinematic, body.bodyType,
                "she was frozen kinematic — a moored boat is RESTRAINED by her rope, not switched off " +
                "(BoatMooring's own rule, and rule 5's)");

            // …and the player still gets ashore, by her own press, from wherever the boat ended up.
            yield return PutHerAshore(opening);
            Assert.Less(Vector2.Distance(_player.transform.position, Ashore), 0.1f,
                $"the player is put ashore after a fallback tie-up exactly as after an honest one; she " +
                $"landed at {(Vector2)_player.transform.position} rather than {Ashore}. She was left at " +
                $"{whereSheWasLeft}.");
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
