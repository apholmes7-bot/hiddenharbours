using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>THE ARRIVAL OVER THE REGION'S OWN WATER — the test that was missing.</b>
    ///
    /// <para><c>ArrivalOpeningPlayTests</c> drives the sequence over a 20 m synthetic route in an empty
    /// scene, and it passes. The owner then built St Peters in a real editor and the boat sailed NORTH
    /// past everything and never docked. Both were true at once, which is the whole lesson: the fixture
    /// proved the STATE MACHINE and nothing about the PASSAGE. A synthetic straight line 20 m long, with
    /// no terrain under it, no environment around it and no turn in it, cannot fail the way a 200 m
    /// buoyed fairway across a real tide does.</para>
    ///
    /// <para>So this one changes exactly the things the fixture faked: the <b>real route</b>
    /// (<see cref="StPetersArrivalOpening.Route"/>), the <b>real seabed</b>
    /// (<see cref="StPetersBuilder.ConfigureTidalTerrain"/>), and a <b>real tide</b> under her — and it
    /// asserts the owner's own acceptance: she gets to the berth, in about the time he was promised.</para>
    ///
    /// <para><b>⚠ It still is not the editor.</b> Nothing here draws, so it cannot catch a pose, a
    /// sorting order or a camera. What it can catch is a boat that does not arrive, which is what it is
    /// for; the eyeball stays the owner's.</para>
    /// </summary>
    public class ArrivalOverRealTerrainPlayTests
    {
        /// <summary>The owner was promised "~30 s passage" and the measured one is ~45; harbour speed
        /// inside the wharf line adds about six seconds, and the come-alongside adds the gate's capture
        /// range and a hull length at the berthing speed on top. Give the whole thing three times its
        /// measured length before calling it a failure — this is a regression guard on ARRIVING, not a
        /// stopwatch on the pacing, and a red that is really a loaded machine teaches nobody anything.</summary>
        private const float TimeoutSeconds = 150f;

        private sealed class FakeSave : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
            public SaveData Current { get; } = new SaveData();
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool v) && v;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() { }
        }

        /// <summary>A still tide at a chosen level — the region's own swing, held where a test wants it,
        /// so "she arrives at low water" and "she arrives at high water" are two runs rather than a
        /// coin toss.</summary>
        private sealed class HeldTide : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() =>
                new EnvironmentSample(Vector2.zero, Vector2.zero, Level, SeaState.Calm, 1f, 0f);
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        /// <summary>
        /// A sea with WEATHER in it, unlike <see cref="HeldTide"/> — the same still tide, plus a real
        /// wind vector and a real sea state under her. The calm fixture is the honest bed for "does the
        /// state machine run", but it cannot answer "does she ever actually come to REST", because
        /// coming to rest is decided by whatever is still pushing her when the throttle asks for zero.
        /// The owner's editor has weather in it; this is that, held still so a run is repeatable.
        /// </summary>
        private sealed class HeldWeather : IEnvironmentService
        {
            public float Level;
            public Vector2 Wind = Vector2.zero;
            public float SeaState01;
            public SeaState State = SeaState.Calm;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() =>
                new EnvironmentSample(Wind, Vector2.zero, Level, State, 1f, SeaState01);
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private GameObject _root;
        private GameObject _player;
        private TidalTerrain _terrain;
        private ArrivalOpening _opening;
        private ModeProbe _probe;
        private IsoCharacterSprite _skin;
        private CameraFollow _camera;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ArrivalRealFixture");
            _root.AddComponent<AudioListener>();

            var terrainGo = new GameObject("TidalTerrain");
            terrainGo.transform.SetParent(_root.transform);
            _terrain = terrainGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
            GameServices.TidalTerrain = _terrain;

            // ⭐⭐ SHE HAS A BODY, and that is the whole reason this fixture exists beside the other one.
            // PersistentCoreBuilder gives the player a Rigidbody2D and a 0.35 m foot collider; the older
            // arrival fixture gives her a bare transform. The arrival plants her INSIDE the hull's own
            // capsule every frame, so with a body there is a contact for the solver to resolve and
            // without one there is not — which is exactly why a green fixture and a boat that sailed off
            // the map were both true at once. Same shape as the real core, so the same physics happens.
            _player = new GameObject("Player");
            _player.transform.SetParent(_root.transform);
            var prb = _player.AddComponent<Rigidbody2D>();
            prb.gravityScale = 0f;
            var foot = _player.AddComponent<CircleCollider2D>();
            foot.radius = 0.35f;
            foot.offset = new Vector2(0f, -0.7f);
            GameServices.PlayerTransform = _player.transform;
            GameServices.Save = new FakeSave();

            // ⭐ THE SKIN THE REAL CORE GIVES HER (PersistentCoreBuilder wires exactly this def onto
            // exactly this component). Without it the walk-in-place question cannot even be ASKED here:
            // the cell the player is drawn in is chosen by IsoCharacterSprite off her own motion, and a
            // fixture whose player is a bare transform has no drawer left to be wrong.
            _skin = _player.AddComponent<IsoCharacterSprite>();
            _skin.Configure(UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(FisherIsoPath));

            // ⭐ THE WHARF'S OWN TIE-OFF POINTS. The pier itself needs an imported art kit and is not
            // built here, but its MOORING FITTINGS are pure geometry — so the same fittings the region
            // builder turns into ShoreCleats are registered directly. Without them the skipper's line has
            // nothing to go over, and "the lines take the last half-metre" is untestable over the real
            // berth. Derived from StPetersWharf.Fittings(), never a hand-placed bollard, so a re-sited
            // pier moves them here too.
            MooringCleats.Clear();
            var cleats = new GameObject("WharfCleats");
            cleats.transform.SetParent(_root.transform);
            float deckElevation = StPetersWharf.DeckElevationFrom(_terrain);
            int n = 0;
            foreach (StPetersWharf.Fitting f in StPetersWharf.MooringFittings())
            {
                var go = new GameObject($"Cleat_{f.Name}_{n}");
                go.transform.SetParent(cleats.transform);
                go.transform.position = new Vector3(f.Position.x, f.Position.y, 0f);
                go.AddComponent<ShoreCleat>().Configure($"wharf.st_peters.{f.Name}_{n}", deckElevation);
                n++;
            }

            // …and a camera, because the third defect the owner watched is a FRAMING. It follows the
            // player (which is what the camera does on foot and on a deck alike) and takes its framing
            // from the Core signals the arrival publishes — it references the arrival not at all.
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(_root.transform);
            camGo.AddComponent<Camera>().orthographic = true;
            _camera = camGo.AddComponent<CameraFollow>();
            _camera.Target = _player.transform;
        }

        /// <summary>The player's 8-direction skin — the def the persistent core wires.</summary>
        private const string FisherIsoPath = "Assets/_Project/Data/Characters/FisherIso.asset";

        [TearDown]
        public void TearDown()
        {
            // Stop the probe HERE rather than at the end of the test, so a failing assert cannot leave a
            // handler subscribed to a static bus for the rest of the run.
            if (_probe != null) { _probe.Stop(); _probe = null; }

            if (_root != null) Object.DestroyImmediate(_root);
            MooringCleats.Clear();
            Interactables.Clear();
            GameServices.Save = null;
            GameServices.PlayerTransform = null;
            GameServices.Environment = null;
            GameServices.Reset();
        }

        private ArrivalOpening Begin(float tideLevel) => Begin(new HeldTide { Level = tideLevel });

        private ArrivalOpening Begin(IEnvironmentService weather)
        {
            GameServices.Environment = weather;

            var go = new GameObject("ArrivalOpening");
            go.transform.SetParent(_root.transform);
            go.SetActive(false);
            _opening = go.AddComponent<ArrivalOpening>();
            _opening.Configure(
                UnityEditor.AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(
                    StPetersArrivalOpening.SkipperPath),
                StPetersArrivalOpening.Route(),
                StPetersArrivalOpening.Berth(),
                StPetersArrivalOpening.BerthHeadingDegrees(),
                StPetersArrivalOpening.StepAshore(),
                StPetersBuilder.ApproachBedElevation);

            // ⚠ ON DECK for this fixture, deliberately — the same call the region builder makes, with the
            // opposite answer. What this suite exists to prove is the PASSAGE OVER THE REAL TERRAIN: that
            // she runs the region's own fairway, at the region's own tides, and ends alongside the region's
            // own wharf. The cabin is hull-local and region-independent, so starting her below would add a
            // door press to fifteen long tests and prove nothing new about the water. The journey through
            // the cabin is IntroCabinPassagePlayTests'.
            _opening.ConfigureCabin(startBelowDecks: false);

            go.SetActive(true);

            Assert.IsTrue(_opening.TryBegin(), "the arrival must start on a fresh save");
            return _opening;
        }

        /// <summary>One second of the passage: where she was, and which phase her skipper was in.</summary>
        private readonly struct Fix
        {
            public readonly Vector2 At;
            public readonly PilotagePhase Phase;
            public Fix(Vector2 at, PilotagePhase phase) { At = at; Phase = phase; }
        }

        /// <summary>How many fixes the printed track is decimated to. ⚠ CI truncates a test message at
        /// 800 characters, so a track printed one-per-line eats the whole budget and the DIAGNOSIS — the
        /// pose, the phase, the abort count — is what gets cut. Compact and decimated, the whole passage
        /// fits and still leaves room for the sentence that says what went wrong.</summary>
        private const int TrackFixes = 26;

        /// <summary>Her track — the thing that tells you WHERE it went wrong rather than only that it did,
        /// with the PHASE she was in written in at every change. A timeout that says "stuck" cannot
        /// distinguish a boat steering the wrong way from one that never got a throttle; a track that says
        /// <c>A</c> forty metres seaward of the gate says which.</summary>
        private string Track(List<Fix> track)
        {
            var sb = new System.Text.StringBuilder(
                "\n  her track ~1 s apart (P=passage A=approach G=gate S=alongside M=moored):\n   ");
            int step = Mathf.Max(1, Mathf.CeilToInt(track.Count / (float)TrackFixes));
            PilotagePhase last = (PilotagePhase)(-1);
            int onThisLine = 0;

            for (int i = 0; i < track.Count; i++)
            {
                bool turned = track[i].Phase != last;
                if (!turned && i % step != 0 && i != track.Count - 1) continue;

                if (turned) { sb.Append($" {Letter(track[i].Phase)}"); last = track[i].Phase; }
                sb.Append($" {track[i].At.x:F0},{track[i].At.y:F0}");
                if (++onThisLine % 6 == 0) sb.Append("\n   ");
            }
            return sb.ToString();
        }

        private static char Letter(PilotagePhase phase) => phase switch
        {
            PilotagePhase.Passage => 'P',
            PilotagePhase.Approach => 'A',
            PilotagePhase.Gate => 'G',
            PilotagePhase.Alongside => 'S',
            _ => 'M',
        };

        private IEnumerator RunToBerth(float tideLevel, string what)
        {
            ArrivalOpening opening = Begin(tideLevel);
            Vector2 berth = StPetersArrivalOpening.Berth();

            var track = new List<Fix>();
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            float nextSample = 0f;

            // ⚠ WAIT FOR THE LINES, NOT FOR THE HAND-OVER. The hand-over is the PLAYER's now (the owner's
            // Q1 ruling), so a fixture that waits for it is waiting for a press nobody made.
            while (opening.Current != ArrivalOpening.Phase.Moored &&
                   Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= nextSample && opening.Boat != null)
                {
                    track.Add(new Fix(opening.Boat.transform.position, opening.Pilotage));
                    nextSample = Time.realtimeSinceStartup + 1f;
                }
                yield return null;
            }

            Assert.AreEqual(ArrivalOpening.Phase.Moored, opening.Current,
                $"at {what} the arrival never tied up — it is stuck in " +
                $"{opening.Current}/{opening.Pilotage} after {opening.Aborts} abort(s). " +
                (opening.Boat != null
                    ? $"She is at {(Vector2)opening.Boat.transform.position}, " +
                      $"{Vector2.Distance(opening.Boat.transform.position, berth):F0} m from the berth " +
                      $"{berth}, heading {ArrivalPilot.HeadingOf(opening.Boat.transform):F0}°, making " +
                      $"{opening.Boat.Velocity.magnitude:F2} m/s, throttle {opening.Boat.Throttle:F2}, " +
                      $"steer {opening.Boat.Steer:F2}, aground={opening.Boat.IsAground}."
                    : "There is no boat.") + Track(track));

            // ⚠ MEASURE AFTER THE LINES HAVE COME UP TAUT, not at the instant the phase flips. The lines
            // ARE the last of the manoeuvre (§2.2) — MooringLineMath's tether hauls her the remaining
            // gap in over the next couple of seconds — so a pose sampled on the frame the lines go over
            // is a pose sampled before the thing being asserted has happened. CanStepAshore is that
            // moment as a STATE rather than a stopwatch: moored, the settling beat done, the planks
            // offered.
            float settled = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!opening.CanStepAshore && opening.Current == ArrivalOpening.Phase.Moored
                   && Time.realtimeSinceStartup < settled) yield return null;

            // ⛔ THE POSE, PRODUCED RATHER THAN WRITTEN. TieUp used to place her on the berth, so this
            // used to be an assertion about an assignment. It is an assertion about the PILOT now, and it
            // is stated in the berth's own axes, because "alongside" is a lateral claim and "at her berth"
            // is an along-track one — a single radius conflates them and passes for a boat lying across
            // her own wharf.
            BerthPilot.Berth pose = BerthPilot.Berth.FromShorePoint(
                berth, StPetersArrivalOpening.BerthHeadingDegrees(), StPetersArrivalOpening.StepAshore(),
                ArrivalHull().LengthMeters);
            Transform hull = opening.Boat.transform;
            float lateral = BerthPilot.LateralOffset(hull.position, pose);
            float along = BerthPilot.AlongTrackTo(hull.position, berth, pose.HeadingDegrees);
            float heading = ArrivalPilot.Wrap180(ArrivalPilot.HeadingOf(hull) - pose.HeadingDegrees);

            string lies = $" She lies {lateral:F2} m off her line, {along:F2} m along it, {heading:F0}° " +
                          $"off her heading, in {opening.Pilotage} after {opening.Aborts} abort(s), " +
                          $"lines fast={opening.LinesAreFast}, honest={opening.TiedUpHonestly}.";

            // ⭐ THE CLAIM THE SLICE IS ACTUALLY MAKING. The pose assertions below state the TOLERANCE;
            // this one states that the come-alongside CONVERGED — the lines went over because
            // BerthingPilot.ReadyForLines said alongside, stopped and in the pose, not because the settle
            // stopwatch ran out and tied her up wherever she was lying. With the snap gone those are the
            // only two ways to be moored, and only one of them is an arrival.
            Assert.IsTrue(opening.TiedUpHonestly,
                $"at {what} she was tied up by the SETTLE FALLBACK, not by the come-alongside — the hull " +
                "never got herself into her pose, which is the one thing this slice deleted the snap to " +
                "prove she could." + lies + Track(track));

            Assert.Less(Mathf.Abs(lateral), 2f,
                $"at {what} she came to rest {lateral:F2} m off her berth LINE — that is not alongside, " +
                "and it is exactly what the snap covered up." + lies + Track(track));
            Assert.Less(Mathf.Abs(along), ArrivalPilot.Settings.Default.StopMetres + 3f,
                $"at {what} she stopped {along:F2} m short of (or past) her berth along the wharf." +
                lies + Track(track));
            Assert.Less(Mathf.Abs(heading), BerthPilot.Settings.Default.HeadingToleranceDegrees + 5f,
                $"at {what} she lies {heading:F0}° ACROSS her berth rather than alongside it." +
                lies + Track(track));
            Assert.IsTrue(opening.LinesAreFast,
                $"at {what} she is tied up with no line made fast — MooringLineMath is the constraint " +
                "that replaced the snap." + lies + Track(track));

            // …and only then, the player's own press.
            yield return PutHerAshore(opening);

            Debug.Log($"[arrival/real] at {what}: alongside {lateral:F2} m off her line, {along:F2} m " +
                      $"along it, {heading:F1}° off her heading, lines fast — after " +
                      $"{TimeoutSeconds - (deadline - Time.realtimeSinceStartup):F1} s of real time, " +
                      $"{track.Count} s of track." + Track(track));
        }

        /// <summary>The arrival hull, for the geometry the assertions are stated in.</summary>
        private static BoatHullDef ArrivalHull() =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(
                StPetersArrivalOpening.SkipperPath).Boat;

        /// <summary>
        /// ⭐ <b>The player's own press.</b> The owner ruled (Q1, 2026-08-26) that she rides until she uses
        /// the exit key, so every finish in this fixture goes through it. Driven through
        /// <c>StepAshore()</c> rather than a synthesised key: a virtual press is undeliverable to the New
        /// Input System from a test, and this is the same call the interact verb makes on the candidate
        /// the arrival registers.
        /// </summary>
        private IEnumerator PutHerAshore(ArrivalOpening opening)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!opening.CanStepAshore && opening.Current != ArrivalOpening.Phase.HandedOver
                   && Time.realtimeSinceStartup < deadline) yield return null;

            if (opening.Current != ArrivalOpening.Phase.HandedOver)
            {
                Assert.IsTrue(opening.CanStepAshore,
                    "the planks were never offered — " + Stuck(opening));
                Assert.IsTrue(opening.StepAshore(), "the offer was standing but the press was refused");
            }

            while (opening.Current != ArrivalOpening.Phase.HandedOver
                   && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(ArrivalOpening.Phase.HandedOver, opening.Current,
                "the step ashore never landed — " + Stuck(opening));
        }

        // =============================================================================================

        /// <summary>⭐ The owner's own acceptance, at the tide that makes the passage hardest: she runs
        /// the fairway and ties up at the east berth.</summary>
        [UnityTest]
        public IEnumerator SheRunsTheRealFairwayAndDocks_AtSpringLow()
        {
            yield return RunToBerth(StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude,
                                    "spring low");
        }

        /// <summary>…and at the top of the tide, where the reef is flooded and there is nothing to hold
        /// her to the channel but the helm.</summary>
        [UnityTest]
        public IEnumerator SheRunsTheRealFairwayAndDocks_AtSpringHigh()
        {
            yield return RunToBerth(StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude,
                                    "spring high");
        }

        /// <summary>
        /// 🔴 <b>The passenger is CARGO for the passage, not a body.</b> She keeps a rigidbody and a
        /// foot collider, and the arrival plants her inside the hull's capsule — so unless she is taken
        /// out of the simulation the solver spends the whole passage shoving a 60 kg boat apart from
        /// her, every fixed step. That is a boat thrown off her track and a passenger pinned in place:
        /// the two defects reported off the owner's walk, from one cause.
        /// </summary>
        [UnityTest]
        public IEnumerator ThePassengerIsTakenOutOfThePhysicsWorld_SoSheCannotShoveTheBoat()
        {
            var body = _player.GetComponent<Rigidbody2D>();
            Assert.IsTrue(body.simulated, "precondition: she starts as a simulated body");

            ArrivalOpening opening = Begin(0f);
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(body.simulated,
                "the passenger is still simulated while being carried — she is inside the hull's " +
                "collider, so every fixed step the solver will shove the boat off her track");

            // …and she is handed back a body when she steps ashore, or she can never walk again.
            bool ashore = false;
            yield return RunAshore(opening, r => ashore = r);

            Assert.IsTrue(ashore, "the arrival had to finish for the release to mean anything — " +
                          Stuck(opening));
            Assert.IsTrue(body.simulated,
                "she was left un-simulated after stepping ashore — she would walk through the world");
        }

        /// <summary>
        /// 🔴 <b>She points where she is GOING, from the first frame.</b> A boat spawned on the default
        /// identity rotation points due NORTH, and with the helm's authority scaling on way she would
        /// run a long way north before she could turn — which is exactly what the owner watched. Pinned
        /// against the route rather than against a number, so re-routing the fairway re-checks it.
        /// </summary>
        [UnityTest]
        public IEnumerator SheStartsPointedDownTheRoute_NotDueNorth()
        {
            ArrivalOpening opening = Begin(0f);
            yield return null;

            Vector2[] route = StPetersArrivalOpening.Route();
            float wanted = ArrivalPilot.CompassOf(route[1] - route[0]);
            float actual = ArrivalPilot.HeadingOf(opening.Boat.transform);

            Assert.Less(Mathf.Abs(ArrivalPilot.Wrap180(actual - wanted)), 5f,
                $"she starts heading {actual:F1}° when the first leg bears {wanted:F1}° — " +
                (Mathf.Abs(ArrivalPilot.Wrap180(actual)) < 5f
                    ? "and DUE NORTH is the identity rotation, i.e. nothing set her heading at all."
                    : "something set it wrong."));

            Assert.Greater(Vector2.Dot(opening.Boat.Velocity.normalized,
                                       (route[1] - route[0]).normalized), 0.9f,
                "…and she must be making way ALONG the first leg, not across it");
        }

        // =============================================================================================
        //  the release - and who is still entitled to speak for the control mode
        // =============================================================================================

        /// <summary>Every <see cref="ControlModeChanged"/> the bus carries, in order, until stopped.
        ///
        /// <para>⚠⚠ <b>It SUBSCRIBES and nothing else - never <c>EventBus.Clear&lt;T&gt;()</c>
        /// for a clean slate first.</b> <c>Clear</c> nulls the whole channel, dropping the handlers that
        /// live MonoBehaviours registered in their own <c>OnEnable</c> - which in a fixture like this
        /// one means the components under test. That has already turned a fixture bug into what read
        /// like a production bug once (#580). A fresh list and a fresh subscription is the whole
        /// need.</para></summary>
        private sealed class ModeProbe
        {
            public readonly List<ControlMode> Heard = new List<ControlMode>();
            private System.Action<ControlModeChanged> _handler;

            public ModeProbe() { _handler = e => Heard.Add(e.Mode); EventBus.Subscribe(_handler); }

            public void Stop()
            {
                if (_handler == null) return;
                EventBus.Unsubscribe(_handler);
                _handler = null;
            }

            public string What => Heard.Count == 0 ? "nothing" : string.Join(", ", Heard);
        }

        /// <summary>
        /// 🔴 <b>ONCE SHE IS ASHORE, THIS COMPONENT NO LONGER SPEAKS FOR THE CONTROL MODE.</b>
        ///
        /// <para>The arrival lives in the StPeters scene until the region UNLOADS - and the region
        /// unloads at the moment the player <i>sails away</i>, which is to say while she is
        /// <see cref="ControlMode.Aboard"/> her own boat. <c>OnDisable</c> calls the same release the
        /// handover does, so unguarded it publishes <see cref="ControlMode.OnFoot"/> straight over the
        /// top of the mode she is really in. <c>PlayerSubmergeVisual.DrivesSubmersion</c> is precisely
        /// <c>mode == OnFoot</c>, so she would read as a wading body while standing on her own deck at
        /// sea. That is every departure from St Peters, forever - the standard path, not an edge.</para>
        ///
        /// <para><b>⚠ Why the probe listens from before she is aboard.</b> The claim under test is
        /// a NEGATIVE one - "nothing more was said" - and a negative assertion passes just as happily
        /// when the probe is deaf as when the code is right. So it first has to hear the two words the
        /// arrival is SUPPOSED to say (<c>OnDeck</c> when she is carried, <c>OnFoot</c> when she is put
        /// ashore). By the time the silence is asserted, the microphone has been proven live.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator OnceSheIsAshore_DisablingTheArrivalSaysNothingMoreAboutTheControlMode()
        {
            _probe = new ModeProbe();                 // listening from before she is even aboard

            ArrivalOpening opening = Begin(0f);
            bool ashore = false;
            yield return RunAshore(opening, r => ashore = r);

            Assert.IsTrue(ashore,
                "she had to get ashore first - a release that never happened cannot happen twice. " +
                Stuck(opening));

            // The arrival's OWN two words, which are correct - and which prove this probe can hear.
            // Asserted as CONTAINS rather than as an exact sequence on purpose: a stray publish from
            // some other fixture left resident in the shared player loop is not what is on trial here
            // and should not red this test. What IS on trial is the DELTA across the disable, below.
            CollectionAssert.Contains(_probe.Heard, ControlMode.OnDeck,
                $"she was never announced as carried; the bus carried: {_probe.What}");
            CollectionAssert.Contains(_probe.Heard, ControlMode.OnFoot,
                $"she was never announced as ashore; the bus carried: {_probe.What}");

            int saidByTheTimeSheWasAshore = _probe.Heard.Count;

            opening.enabled = false;                  // what a region unload does to this component
            yield return null;

            Assert.AreEqual(saidByTheTimeSheWasAshore, _probe.Heard.Count,
                $"disabling the finished arrival published {_probe.What} - one word too many. She is " +
                "ashore and this component has already handed her back; on the real path she is " +
                "ABOARD her own boat when St Peters unloads, so this publish would put her back on " +
                "foot and flood her to the waist at sea.");
        }

        /// <summary>
        /// ...and the other half of that guard - the half a careless fix breaks. An arrival cut short
        /// <b>mid-passage</b> is the case <c>OnDisable</c> exists for at all: she is un-simulated and
        /// her walking is switched off while she is carried, so if the region unloads under her and
        /// nobody undoes it, she arrives in the next region a frozen ghost with no component left alive
        /// to notice. Guarding the release must not cost this.
        /// </summary>
        [UnityTest]
        public IEnumerator DisabledMidPassage_SheStillGetsHerBodyAndHerFeetBack()
        {
            var body = _player.GetComponent<Rigidbody2D>();

            ArrivalOpening opening = Begin(0f);
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(body.simulated, "precondition: she is cargo while she is being carried");
            Assert.AreNotEqual(ArrivalOpening.Phase.HandedOver, opening.Current,
                "precondition: this is about being cut off UNDER WAY, so she must still be");

            _probe = new ModeProbe();
            opening.enabled = false;
            yield return null;

            Assert.IsTrue(body.simulated,
                "the region unloaded under her mid-passage and left her out of the physics world - " +
                "she would walk through the next region rather than in it");
            CollectionAssert.Contains(_probe.Heard, ControlMode.OnFoot,
                "a passage cut short must still put her back on her feet; the bus carried: " +
                _probe.What);
        }

        // =============================================================================================
        //  THE ARRIVAL ENDS ASHORE — the three things the owner watched go wrong on his first sail in
        // =============================================================================================

        /// <summary>Advance a few frames — the passage is long, and one frame proves nothing about a
        /// pose that is re-decided every LateUpdate.</summary>
        private IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>Run her in, press the exit key when the planks are offered, and report whether she
        /// made it ashore. ⚠ The press is not optional scaffolding — it is how the arrival ENDS now.</summary>
        private IEnumerator RunAshore(ArrivalOpening opening, System.Action<bool> done)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!opening.CanStepAshore && opening.Current != ArrivalOpening.Phase.HandedOver
                   && Time.realtimeSinceStartup < deadline) yield return null;

            if (opening.CanStepAshore) opening.StepAshore();

            while (opening.Current != ArrivalOpening.Phase.HandedOver &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            done(opening.Current == ArrivalOpening.Phase.HandedOver);
        }

        /// <summary>What she was doing when she stopped doing it — for a timeout message that names the
        /// defect instead of only reporting that time ran out.</summary>
        private static string Stuck(ArrivalOpening opening)
        {
            if (opening.Boat == null) return $"stuck in {opening.Current}; there is no boat";
            return $"stuck in {opening.Current}, boat at {(Vector2)opening.Boat.transform.position}, " +
                   $"{Vector2.Distance(opening.Boat.transform.position, StPetersArrivalOpening.Berth()):F1} m " +
                   $"off the berth, still making {opening.Boat.Velocity.magnitude:F3} m/s (the hand-over " +
                   $"wants under 0.10), throttle {opening.Boat.Throttle:F2}, aground={opening.Boat.IsAground}";
        }

        /// <summary>
        /// 🔴 <b>SHE IS A PASSENGER, NOT A PEDESTRIAN — the walk-in-place the owner watched.</b>
        ///
        /// <para>The arrival carries her by writing her world position every LateUpdate, and
        /// <see cref="IsoCharacterSprite"/> chooses her cell by MEASURING her own local-position step.
        /// Those two facts together are the defect: a passenger standing still on a hull doing five
        /// knots is measured at five knots and drawn at a dead run, on the spot, for the whole passage.
        /// Nothing was lying — the drawer was asked the wrong question.</para>
        ///
        /// <para><b>⚠ The microphone is proven BEFORE the claim is made.</b> "The gait is Idle" passes
        /// just as happily on a skin with no art wired, whose gait never leaves Idle whatever it is
        /// asked. So the test first MOVES her itself and watches the gait leave Idle; only a drawer
        /// that has demonstrably answered Walk/Run is allowed to be asked for Idle.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator SheIsNotDrawnWalking_WhileTheBoatCarriesHer()
        {
            Assert.IsNotNull(_skin.Visual, "precondition: the fisher's iso def must load, or this " +
                             "fixture is asking a drawer that cannot answer");

            // --- the microphone: move her by hand and watch the drawer report a gait ---------------
            for (int i = 0; i < 12; i++)
            {
                _player.transform.position += new Vector3(0.25f, 0f, 0f);   // ~ walking pace at 60 Hz
                yield return null;
            }
            Assert.AreNotEqual(CharacterGait.Idle, _skin.Gait,
                "the fixture's own skin never leaves Idle even when the player is visibly moving — " +
                "this microphone is deaf, so any 'she is not walking' below would be a false green");

            // --- the claim: while the boat carries her, she is not travelling ----------------------
            ArrivalOpening opening = Begin(0f);
            yield return Frames(30);

            Assert.AreEqual(ArrivalOpening.Phase.Approaching, opening.Current,
                "precondition: she must still be under way for this to be about the passage");
            Assert.Greater(opening.Boat.Velocity.magnitude, 1f,
                "precondition: the boat has to be MAKING WAY, or there is no motion to mis-read");

            Assert.AreEqual(CharacterGait.Idle, _skin.Gait,
                $"she is drawn at a {_skin.Gait} while standing on a deck doing " +
                $"{opening.Boat.Velocity.magnitude:F1} m/s — the boat's way is being read as her own " +
                $"stride. Her measured speed is {_skin.Speed:F2} m/s and speed-held={_skin.IsSpeedHeld}; " +
                "whoever moves a character in a frame that moves must STATE the speed (HoldSpeed).");

            Assert.IsTrue(_skin.IsSpeedHeld,
                "nobody is stating the passenger's travelling speed, so the drawer is left measuring " +
                "the hull's way as her stride — the exact seam HoldSpeed exists for");
        }

        /// <summary>
        /// ⭐ <b>THE OWNER'S Q1 RULING, OVER THE REAL BERTH: she is aboard until SHE says otherwise.</b>
        /// <i>"The player is on the boat until they use the exit key to step onto dock."</i> (2026-08-26.)
        /// So the skipper ties up, the planks are offered, and then nothing happens — no timer, no
        /// auto-handover — for as long as she likes. The old opening counted 2.5 s and put her ashore.
        ///
        /// <para>⚠ The wait is deliberately several times the old beat. A ruling that "no timer fires" is
        /// only tested by outliving the timer that used to.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator SheRidesTheTiedUpBoatUntilShePressesTheExitKey()
        {
            ArrivalOpening opening = Begin(0f);

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!opening.CanStepAshore && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(opening.CanStepAshore, "she never got tied up and offered the planks — " +
                          Stuck(opening));
            Assert.AreEqual(ArrivalOpening.Phase.Moored, opening.Current,
                "the offer stands while she is MOORED, not after some other transition");

            Vector3 aboard = _player.transform.position;
            Vector2 deck = opening.Boat.transform.position;

            float watchUntil = Time.realtimeSinceStartup + 10f;      // four times the old moored beat
            while (Time.realtimeSinceStartup < watchUntil)
            {
                Assert.AreNotEqual(ArrivalOpening.Phase.HandedOver, opening.Current,
                    "something handed her over without a press — the exit is HERS now, and a timer here " +
                    "is the deleted teleport with a delay in front of it");
                yield return null;
            }

            Assert.Less(Vector2.Distance(_player.transform.position, aboard), 2f,
                $"she drifted off the deck without a press: aboard at {(Vector2)aboard}, now at " +
                $"{(Vector2)_player.transform.position} (the boat is at {deck})");
            Assert.Less(Vector2.Distance(_player.transform.position,
                                         StPetersArrivalOpening.StepAshore()), 30f,
                "sanity: she should still be beside the wharf, riding the boat");

            // …and the moment she asks, she goes.
            Assert.IsTrue(opening.StepAshore(), "the standing offer must answer the press");
            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (opening.Current != ArrivalOpening.Phase.HandedOver
                   && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.AreEqual(ArrivalOpening.Phase.HandedOver, opening.Current,
                "the step ashore never landed — " + Stuck(opening));
            Assert.Less(Vector2.Distance(_player.transform.position,
                                         StPetersArrivalOpening.StepAshore()), 0.2f,
                $"she landed at {(Vector2)_player.transform.position} rather than on the region's own " +
                $"disembark point {StPetersArrivalOpening.StepAshore()}");
        }

        /// <summary>
        /// 🔴 <b>SHE CAN WALK AWAY — the "no way to get off the boat" the owner reported.</b>
        ///
        /// <para>There is no exit BUTTON in this opening and there was never meant to be: the machine
        /// ties up, waits a beat and hands over. So "no way to exit" is not a missing key — it is the
        /// machine not finishing, or finishing and still holding her. Both end the same way for the
        /// player: she is put down and something puts her straight back.</para>
        ///
        /// <para><b>The assertion is a PUSH, not a poll.</b> Reading a phase says the code thinks it let
        /// go; shoving her and finding her still there next frame is the player's own test. The walk
        /// controller reads the New Input System and a virtual key press is undeliverable in a test —
        /// so she is moved through her body, which is what her keys would have moved anyway.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator OnceAshore_SheCanActuallyWalkAway()
        {
            var body = _player.GetComponent<Rigidbody2D>();

            ArrivalOpening opening = Begin(0f);
            bool ashore = false;
            yield return RunAshore(opening, r => ashore = r);
            Assert.IsTrue(ashore, "she never got ashore — " + Stuck(opening) +
                          ". A machine that never hands over IS 'there is no way off the boat'.");

            Vector3 before = _player.transform.position;
            for (int i = 0; i < 30; i++)
            {
                body.linearVelocity = new Vector2(1.5f, 0f);       // what her keys would have asked for
                yield return new WaitForFixedUpdate();
            }
            float moved = Vector2.Distance(_player.transform.position, before);

            Assert.Greater(moved, 0.5f,
                $"she was put ashore and then moved {moved:F2} m under a steady 1.5 m/s — something is " +
                "putting her back where it wants her every frame. That is what 'there is no way to get " +
                "off the boat' looks like from the inside.");
        }

        /// <summary>
        /// 🔴 <b>THE PASSAGE IS FRAMED FOR A BOAT, NOT FOR DECK WORK.</b>
        ///
        /// <para>The arrival says <see cref="ControlMode.OnDeck"/>, which is true and load-bearing — it
        /// is what keeps her drawn dry on planking. But the camera's zoom policy reads that mode as
        /// <i>deck work</i> and frames 6.75 m of world height, which is half the LENGTH of the hull she
        /// is standing on: the player's first two minutes of this game are spent inside a boat she
        /// cannot see, watching a fairway she cannot read.</para>
        ///
        /// <para>The claim is the whole vessel and her water: the hull's own authored framing
        /// (<c>BoatHullDef.CameraWorldHeightMeters</c>) while she is carried, and the walker's framing
        /// the moment she is put ashore.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ThePassageIsFramedForTheHullSheIsRidingOn_AndTheWalkerAfterwards()
        {
            var hull = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(
                StPetersArrivalOpening.SkipperPath).Boat;

            ArrivalOpening opening = Begin(0f);
            yield return Frames(30);

            Assert.AreEqual(ArrivalOpening.Phase.Approaching, opening.Current,
                "precondition: she must still be under way for this to be about the passage");
            Assert.IsTrue(_camera.FramingKnown,
                "the camera never committed a framing at all — nothing told it the control state");

            Assert.AreEqual(CameraFraming.Boat, _camera.Framing,
                $"the passage is framed as {_camera.Framing} — " +
                $"{_camera.WorldHeightFor(_camera.Framing):F2} m of world height around a " +
                $"{hull.LengthMeters:F1} m boat");

            Assert.GreaterOrEqual(_camera.WorldHeightFor(_camera.Framing), hull.LengthMeters,
                "she cannot see the whole boat she is standing on");

            bool ashore = false;
            yield return RunAshore(opening, r => ashore = r);
            Assert.IsTrue(ashore, "she had to get ashore for the hand-back to mean anything — " +
                          Stuck(opening));
            yield return Frames(10);

            Assert.AreEqual(CameraFraming.OnFoot, _camera.Framing,
                $"she stepped ashore and the view stayed at {_camera.Framing} — the boat framing must " +
                "be handed back with the boat");
        }

        /// <summary>
        /// 🔴 <b>SHE COMES TO REST IN WEATHER, NOT ONLY IN A FLAT CALM.</b>
        ///
        /// <para>Every other test in this fixture runs her over a dead-still sea, and the hand-over is
        /// gated on her VELOCITY falling under a threshold. That pairing is a blind spot with a shape:
        /// a boat that never quite stops never ties up, never hands over, and leaves the player held on
        /// a deck she cannot leave — the owner's second and first reports, from one cause. So run the
        /// same passage with wind and a sea running and hold the machine to the same finish.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator SheStillTiesUpAndHandsOver_WithWindAndASeaRunning()
        {
            ArrivalOpening opening = Begin(new HeldWeather
            {
                Level = StPetersBuilder.TideMean,
                Wind = new Vector2(-9f, 3f),       // a fresh onshore breeze, straight up the fairway
                State = SeaState.Moderate,
                SeaState01 = 0.5f,
            });

            bool ashore = false;
            yield return RunAshore(opening, r => ashore = r);
            Assert.IsTrue(ashore, "in weather the arrival never finished — " + Stuck(opening) + ".");
        }

        /// <summary>
        /// 📏 <b>WHERE SHE ACTUALLY LIES, measured against the planks.</b> Not an assertion about what
        /// is right — the berth geometry is the owner's and this lane does not move it — but a
        /// measurement, logged, so "the boat drives over the dock" is answered in metres instead of in
        /// opinions. It fails only if she comes to rest somewhere other than the berth she was sent to,
        /// which would be a pilot defect rather than an authoring one.
        /// </summary>
        [UnityTest]
        public IEnumerator WhereSheLiesAgainstTheWharf_IsMeasuredAndReported()
        {
            var hull = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(
                StPetersArrivalOpening.SkipperPath).Boat;

            ArrivalOpening opening = Begin(0f);
            bool ashore = false;
            yield return RunAshore(opening, r => ashore = r);
            Assert.IsTrue(ashore, "she has to finish before there is a resting place to measure — " +
                          Stuck(opening));

            Rect deck = StPetersWharf.DeckFootprint();
            Vector2 berth = StPetersArrivalOpening.Berth();
            Transform boat = opening.Boat.transform;
            Vector2 bow = (Vector2)boat.position + (Vector2)(boat.up * (hull.LengthMeters * 0.5f));
            Vector2 stern = (Vector2)boat.position - (Vector2)(boat.up * (hull.LengthMeters * 0.5f));

            Debug.Log($"[arrival/berth] deck x {deck.xMin}..{deck.xMax}, y {deck.yMin}..{deck.yMax}; " +
                      $"berth {berth}; she lies centre {(Vector2)boat.position} heading " +
                      $"{ArrivalPilot.HeadingOf(boat):F0}°, bow {bow}, stern {stern}. " +
                      $"bow over the planks = {deck.Contains(bow)}; centre over the planks = " +
                      $"{deck.Contains((Vector2)boat.position)}; the player is put down at " +
                      $"{StPetersArrivalOpening.StepAshore()} (on the planks = " +
                      $"{deck.Contains(StPetersArrivalOpening.StepAshore())}).");

            // Stated in the berth's own axes, because "alongside" is a lateral claim and a single radius
            // cannot tell a boat lying neatly alongside from one lying across her own wharf.
            BerthPilot.Berth pose = BerthPilot.Berth.FromShorePoint(
                berth, StPetersArrivalOpening.BerthHeadingDegrees(), StPetersArrivalOpening.StepAshore(),
                hull.LengthMeters);
            Assert.Less(Mathf.Abs(BerthPilot.LateralOffset(boat.position, pose)), 2f,
                "she came to rest off her berth LINE — she is not alongside the wharf she was sent to");
            Assert.Less(Mathf.Abs(BerthPilot.AlongTrackTo(boat.position, berth, pose.HeadingDegrees)),
                        ArrivalPilot.Settings.Default.StopMetres + 3f,
                "she came to rest somewhere else along the wharf than the berth she was sent to");
        }
    }
}
