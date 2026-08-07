using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>Ladder boarding, live</b> — through the real component lifecycle, the real
    /// <see cref="ControlSwitcher"/> transition and the real Core registry.
    ///
    /// <para>These are PlayMode because every claim here is about a MOVE: which route the tide chose, how
    /// far down the body had got at a given moment, and what was on the renderer while it did. And they
    /// are driven in <b>SECONDS</b>, never frame counts — the climb's length is a function of the gap in
    /// metres and the clip's baked loop in seconds, so a slow runner takes fewer frames to the same
    /// landing and that must not be a failure.</para>
    ///
    /// <para>The load-bearing one is <see cref="TheDescent_FollowsTheStair_NotARamp"/>: it samples the
    /// fisher's actual transform through a real climb and measures the descent's SHAPE — a stretch where
    /// she is still, a stretch where she drops fast. The EditMode suite proves the rig's curve is
    /// reproduced correctly; this proves the fisher is actually being driven by it and not by a rate.</para>
    /// </summary>
    public class LadderBoardingPlayTests
    {
        private const float WharfDeck = 3.0f;      // the quay's deck height above chart datum (m)
        private const float PixelsPerMetre = 32f;  // the locked art scale

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private readonly List<Object> _spawned = new();

        /// <summary>One water level for the whole fixture, so a test can build two rigs and still ask the
        /// tide a different question of each — the level is read ONCE, at the key-press.</summary>
        private FlatEnv _env;

        [SetUp]
        public void SetUp()
        {
            _env = null;
            GameServices.Reset();
            InteractionGate.Reset();
            BoardingLadders.Clear();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            BoardingLadders.Clear();
            InteractionGate.Reset();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private GameObject NewGo(string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            _spawned.Add(g);
            return g;
        }

        private sealed class Rig
        {
            public ControlSwitcher Switcher;
            public GameObject PlayerGo;
            public CharacterClipPlayer ClipPlayer;
            public WharfLadder Ladder;
            public LadderBoardingSettings Config;
        }

        /// <summary>A skin carrying the three clips this slice plays, at the frame counts the 6.2 rig
        /// declares. Body sheets are left out so every assertion is about the CLIP.</summary>
        private CharacterVisualDef ClipSkin()
        {
            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);

            Sprite[] SheetOf(int count)
            {
                var set = new Sprite[count];
                for (int i = 0; i < count; i++)
                {
                    set[i] = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.1f), 32f);
                    set[i].name = $"cell_{i}";
                    _spawned.Add(set[i]);
                }
                return set;
            }

            var def = ScriptableObject.CreateInstance<CharacterVisualDef>();
            def.Id = "visual.ladder_clip_test";
            def.FacingCount = 8;
            def.BoardClip = new CharacterClipSheets
            { FrameCount = 10, FramesPerSecond = 1000f / 90f, Loops = false, Sheet = SheetOf(8 * 10) };
            def.BoardDownClip = new CharacterClipSheets
            { FrameCount = 6, FramesPerSecond = 1000f / 95f, Loops = false, Sheet = SheetOf(8 * 6) };
            def.LadderDownClip = new CharacterClipSheets
            { FrameCount = 10, FramesPerSecond = 1000f / 110f, Loops = true, Sheet = SheetOf(8 * 10) };
            _spawned.Add(def);
            return def;
        }

        /// <summary>
        /// A fisher on the wharf, a boat alongside, and a LADDER at the lip beside her — plus the
        /// deterministic water level that decides which route boarding takes.
        /// </summary>
        /// <param name="waterLevel">m above chart datum. Low water opens the gap; high water closes it.</param>
        /// <param name="withLadder">false builds the same wharf with no ladder on it — the fallback case.</param>
        /// <param name="origin">Where this whole rig sits, so a test can stand two of them up far enough
        /// apart that neither's ladder is within reach of the other's berth.</param>
        private Rig Build(float waterLevel, bool withLadder = true, Vector3 origin = default)
        {
            if (_env == null) { _env = new FlatEnv(); GameServices.Environment = _env; }
            _env.Level = waterLevel;

            // One config for the fixture — the untouched ship defaults, so these tests pin the shipped
            // behaviour rather than a tuning invented here.
            if (GameServices.Config == null)
            {
                var config = ScriptableObject.CreateInstance<GameConfig>();
                _spawned.Add(config);
                GameServices.Config = config;
            }

            var playerGo = NewGo("Player", origin + new Vector3(2.6f, 0f, 0f));
            var walk = playerGo.AddComponent<PlayerWalkController>();
            var deckWalk = playerGo.AddComponent<DeckWalkController>();
            deckWalk.enabled = false;
            var clipPlayer = playerGo.AddComponent<CharacterClipPlayer>();

            var boatGo = NewGo("Boat", origin);
            var boat = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();

            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.dory";                       // the id the repair gate keys off
            hull.DisplayName = "Ladder Boarding Test";
            hull.CameraWorldHeightMeters = 14f;
            _spawned.Add(hull);
            boat.SetHull(hull);

            walk.enabled = true; boat.enabled = false; input.enabled = false;

            WharfLadder ladder = null;
            if (withLadder)
            {
                // At the fisher's own feet, on the lip, looking south over the water.
                var ladderGo = NewGo("Ladder", origin + new Vector3(2.6f, 0f, 0f));
                ladder = ladderGo.AddComponent<WharfLadder>();
                ladder.Configure($"wharf.test.ladder_{origin.x:0}", WharfDeck, faceHeadingDegrees: 180f);
            }

            var dock = NewGo("DockZone", origin + new Vector3(0f, 2f, 0f));
            var disembark = NewGo("Disembark", origin + new Vector3(0f, 4f, 0f));
            var swGo = NewGo("Switcher", origin);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dock.transform, 3f, disembark.transform);
            sw.ConfigureBoardingMove(enabled: true, approachSpeed: 3f, approachMaxSeconds: 0.8f,
                                     vaultSeconds: 0.5f, vaultHopMeters: 0.35f);

            return new Rig { Switcher = sw, PlayerGo = playerGo, ClipPlayer = clipPlayer,
                             Ladder = ladder, Config = GameServices.LadderBoarding };
        }

        /// <summary>Pump real frames until the move lands, in SECONDS. Assertion-free on purpose: it is
        /// yielded as a NESTED coroutine, and Unity can turn an exception thrown inside one into a logged
        /// error rather than a failure. Callers state their own post-conditions.</summary>
        private static IEnumerator RunTheMoveOut(Rig r, float limitSeconds = 20f)
        {
            float spent = 0f;
            while (r.Switcher.IsBoardingMove && spent < limitSeconds)
            {
                yield return null;
                spent += Time.deltaTime;
            }
            yield return null;
        }

        // =============================================================================================
        //  1. THE TIDE CHOOSES THE ROUTE
        // =============================================================================================

        /// <summary>
        /// <b>The headline claim.</b> The same wharf, the same boat and the same key: a STEP across at
        /// high water, and a CLIMB down at low. Nothing about the rig changes between these two but the
        /// deterministic water level.
        /// </summary>
        [UnityTest]
        public IEnumerator AtLowWater_BoardingTakesTheLadder()
        {
            var r = Build(waterLevel: 0f);
            r.ClipPlayer.Configure(ClipSkin());
            yield return null;

            r.Switcher.BeginInteract();

            Assert.IsTrue(r.Switcher.IsBoardingMove, "the move started");
            Assert.IsTrue(r.Switcher.IsLadderBoarding,
                $"a {r.Switcher.LadderGapMetres:0.00} m gap should be a climb, not a step across");
            Assert.Greater(r.Switcher.LadderGapMetres, r.Config.BoardClampMetres);
        }

        [UnityTest]
        public IEnumerator AtHighWater_TheSameWharfIsStillAStepAcross()
        {
            // The deck rides up to just under the planks: the gap closes inside the clamp.
            var r = Build(waterLevel: WharfDeck - 0.25f);
            r.ClipPlayer.Configure(ClipSkin());
            yield return null;

            r.Switcher.BeginInteract();

            Assert.IsTrue(r.Switcher.IsBoardingMove, "the move still starts");
            Assert.IsFalse(r.Switcher.IsLadderBoarding,
                "at high water the boat's deck is within a step of the planks — sending the fisher down " +
                "a ladder there would be absurd");
        }

        /// <summary>A wharf with no ladder is a valid wharf: the fisher steps across a gap too big for it,
        /// exactly as they do on main today. The feature must not strand anyone.</summary>
        [UnityTest]
        public IEnumerator WithNoLadderOnTheWharf_TheMoveIsUnchanged()
        {
            var r = Build(waterLevel: 0f, withLadder: false);
            r.ClipPlayer.Configure(ClipSkin());
            yield return null;

            r.Switcher.BeginInteract();
            Assert.IsFalse(r.Switcher.IsLadderBoarding, "there is no ladder to take");

            yield return RunTheMoveOut(r);
            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode,
                "and she still gets aboard — a missing ladder must never leave the fisher on the wharf");
        }

        // =============================================================================================
        //  2. THE DESCENT ITSELF
        // =============================================================================================

        /// <summary>
        /// <b>The load-bearing movement claim: the fisher descends in STEPS, not at a rate.</b>
        ///
        /// <para>It samples the real transform through a real climb and measures the descent's SIGNATURE
        /// rather than comparing it point-for-point against the curve. That is deliberate: a point-for-point
        /// comparison has to know exactly when the rungs began, which is only knowable to within one frame,
        /// and at a low or uneven CI frame rate that slop is the same size as the effect. The signature is
        /// not frame-rate sensitive and it is the thing that actually matters —</para>
        /// <list type="bullet">
        ///   <item>a stretch where she is essentially STILL (both feet planted — the flat tread), and</item>
        ///   <item>a stretch where she drops far faster than her average (a leg extending).</item>
        /// </list>
        /// <para>A constant-rate descent — the shortcut this whole curve exists to rule out — can produce
        /// neither. Its speed is the same in every window by definition.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheDescent_FollowsTheStair_NotARamp()
        {
            var r = Build(waterLevel: 0f);
            r.ClipPlayer.Configure(ClipSkin());
            yield return null;

            r.Switcher.BeginInteract();
            float gap = r.Switcher.LadderGapMetres;
            Assert.Greater(gap, 1.2f, "the fixture must actually produce a climb worth measuring");

            float loop = r.Config.ClimbLoopSeconds;
            float rate = LadderBoardingMath.StepZFor(r.Config.RungMetres) / loop;  // the ruled-out shortcut

            var times = new List<float>();
            var heights = new List<float>();
            float spent = 0f;
            while (r.Switcher.IsBoardingMove && spent < 20f)
            {
                yield return null;
                spent += Time.deltaTime;
                if (!(r.ClipPlayer.IsPlaying && r.ClipPlayer.Clip == CharacterClip.LadderDown)) continue;
                times.Add(spent);
                heights.Add(r.PlayerGo.transform.position.y);
            }

            Assert.Greater(times.Count, 20,
                "too few samples on the rungs to say anything about the shape of the descent");

            // Speeds over windows of about an eighth of a loop — short enough to sit inside one tread,
            // long enough not to be measuring a single frame's jitter.
            // The END of the climb is excluded: the last part-rung is clamped to the gap so the fisher
            // cannot overshoot the deck, and that clamp stops ANY implementation — stair or ramp — dead.
            // Counting it would let a ramp pass the flat-tread assertion for free. There are five loops in
            // this climb, so there is no shortage of treads left to find.
            float tailCutoff = times[times.Count - 1] - 0.25f;

            float window = loop / 8f;
            float slowest = float.MaxValue, fastest = 0f;
            for (int i = 0, j = 0; i < times.Count; i++)
            {
                while (j < times.Count && times[j] - times[i] < window) j++;
                if (j >= times.Count) break;
                if (times[j] > tailCutoff) break;
                float dt = times[j] - times[i];
                float speed = (heights[i] - heights[j]) / dt;      // descending, so this is positive
                slowest = Mathf.Min(slowest, speed);
                fastest = Mathf.Max(fastest, speed);
            }

            Assert.AreNotEqual(float.MaxValue, slowest, "no window was long enough to measure");

            Assert.Less(slowest, rate * 0.35f,
                $"the slowest window of the descent still ran at {slowest:0.00} m/s against an average of " +
                $"{rate:0.00} — the body never stops, so there is no flat tread and she is being driven " +
                "down a RAMP. A climber only drops when a leg extends.");

            Assert.Greater(fastest, rate * 1.4f,
                $"the fastest window only reached {fastest:0.00} m/s against an average of {rate:0.00} — " +
                "nothing here is a step down onto a rung");

            // …and the whole climb still crosses exactly the gap the tide opened, whatever shape it took.
            float travelled = heights[0] - heights[heights.Count - 1];
            Assert.Less(Mathf.Abs(travelled - gap) * PixelsPerMetre, 8f,
                $"the climb covered {travelled:0.00} m of a {gap:0.00} m gap — the stair must still land " +
                "her on the deck, not somewhere near it");
        }

        /// <summary>A deeper gap takes LONGER. The climb is never rate-scaled to a fixed duration the way
        /// the vault is — real rungs are a real distance apart, and that is what makes the tide legible.
        /// Measured in seconds, because that is what "longer" means.</summary>
        [UnityTest]
        public IEnumerator ADeeperGap_TakesLongerToClimb()
        {
            // Two rigs, a hundred metres apart so neither's ladder is anywhere near the other's berth,
            // sharing one water level — which is read ONCE per move, at the key-press.
            var shallowRig = Build(waterLevel: 1.4f, withLadder: true, origin: Vector3.zero);
            var deepRig = Build(waterLevel: 1.4f, withLadder: true, origin: new Vector3(100f, 0f, 0f));
            shallowRig.ClipPlayer.Configure(ClipSkin());
            deepRig.ClipPlayer.Configure(ClipSkin());
            yield return null;

            shallowRig.Switcher.BeginInteract();
            Assert.IsTrue(shallowRig.Switcher.IsLadderBoarding, "the shallow case must still be a climb");
            float shallowGap = shallowRig.Switcher.LadderGapMetres;
            float shallowSeconds = 0f;
            while (shallowRig.Switcher.IsBoardingMove && shallowSeconds < 20f)
            { yield return null; shallowSeconds += Time.deltaTime; }

            _env.Level = 0f;                      // the tide has run out; the same wharf is now a long climb
            deepRig.Switcher.BeginInteract();
            float deepGap = deepRig.Switcher.LadderGapMetres;
            float deepSeconds = 0f;
            while (deepRig.Switcher.IsBoardingMove && deepSeconds < 20f)
            { yield return null; deepSeconds += Time.deltaTime; }

            Assert.Greater(deepGap, shallowGap, "the fixture must actually be comparing two gaps");
            Assert.Greater(deepSeconds, shallowSeconds,
                $"a {deepGap:0.00} m climb took {deepSeconds:0.00} s and a {shallowGap:0.00} m one took " +
                $"{shallowSeconds:0.00} s — the climb is being scaled to a duration instead of being " +
                "measured in rungs");
        }

        /// <summary>The climber is seated the MEASURED standoff off the ladder plane. #454's contract is
        /// explicit that this is not to be eyeballed: on the plane, the fisher's hands are in the wall.</summary>
        [UnityTest]
        public IEnumerator TheClimber_IsSeatedOffTheLadderPlane()
        {
            var r = Build(waterLevel: 0f);
            r.ClipPlayer.Configure(ClipSkin());
            yield return null;

            r.Switcher.BeginInteract();

            bool sampled = false;
            float spent = 0f;
            while (r.Switcher.IsBoardingMove && spent < 20f)
            {
                yield return null;
                spent += Time.deltaTime;
                if (!(r.ClipPlayer.IsPlaying && r.ClipPlayer.Clip == CharacterClip.LadderDown)) continue;

                Vector2 expected = r.Ladder.WorldPosition
                                 + LadderBoardingMath.SeatOffset(r.Ladder.FaceHeadingDegrees,
                                                                 r.Ladder.StandoffMetres);
                Assert.AreEqual(expected.x, r.PlayerGo.transform.position.x, 1e-3f,
                    "on the rungs the fisher must stand at the ladder's own x — she does not travel " +
                    "across while she is on it");

                // The standoff shows in the OTHER axis as the seat's offset from the ladder line; the
                // climb then carries her down from there, so only the sign is checkable per-frame.
                Assert.LessOrEqual(r.PlayerGo.transform.position.y, expected.y + 1e-3f,
                    "she must be seated off the wall over the water and descending, never back on the deck");
                sampled = true;
            }

            Assert.IsTrue(sampled, "the fisher never got onto the rungs");
        }

        // =============================================================================================
        //  3. THE CLIP, AND THE TWO ENDS THE KIT DOES NOT AUTHOR
        // =============================================================================================

        /// <summary>The rungs draw the rig's LADDER clip — not walk frames, and not the boarding step.</summary>
        [UnityTest]
        public IEnumerator TheRungs_PlayTheLadderClip()
        {
            var r = Build(waterLevel: 0f);
            r.ClipPlayer.Configure(ClipSkin());
            yield return null;

            r.Switcher.BeginInteract();

            bool sawLadder = false;
            int highestFrame = -1;
            float spent = 0f;
            while (r.Switcher.IsBoardingMove && spent < 20f)
            {
                yield return null;
                spent += Time.deltaTime;
                if (r.ClipPlayer.IsPlaying && r.ClipPlayer.Clip == CharacterClip.LadderDown)
                {
                    sawLadder = true;
                    highestFrame = Mathf.Max(highestFrame, r.ClipPlayer.Frame);
                }
            }

            Assert.IsTrue(sawLadder, "the climb drew something other than the ladder clip");
            // It LOOPS at its baked rate rather than being squeezed into the move, so a climb of several
            // loops must walk the whole sheet — a clip stuck on frame 0 is one being restarted every tick.
            Assert.GreaterOrEqual(highestFrame, 8,
                $"the ladder clip only ever reached frame {highestFrame}/9 — it is being re-Played every " +
                "tick instead of started once and left to loop");
        }

        /// <summary>
        /// <b>It LOOPS, so landing must Stop() it</b> (#454's contract says so outright). A clip left
        /// running would leave the fisher climbing an imaginary ladder while she walks the deck.
        /// </summary>
        [UnityTest]
        public IEnumerator WhenTheClimbLands_TheLoopingClipIsOffTheRenderer()
        {
            var r = Build(waterLevel: 0f);
            r.ClipPlayer.Configure(ClipSkin());
            yield return null;

            r.Switcher.BeginInteract();
            Assert.IsTrue(r.Switcher.IsLadderBoarding);
            yield return RunTheMoveOut(r);

            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode, "she got aboard");
            Assert.IsFalse(r.ClipPlayer.IsPlaying,
                "the LOOPING ladder clip is still on the renderer after the climb landed");
            Assert.AreEqual(CharacterClip.None, r.ClipPlayer.Clip);
            Assert.IsFalse(r.Switcher.IsLadderBoarding);
        }

        /// <summary>
        /// The kit authors neither the turn-around at the top nor the step off at the bottom. Both are
        /// covered with the authored <c>boardDown</c> step rather than a hard cut — the kit's own
        /// suggestion, and it says the turn-around is the one players notice. So a descent must show
        /// boardDown BEFORE the rungs and again AFTER them.
        /// </summary>
        [UnityTest]
        public IEnumerator TheUnauthoredEnds_AreCoveredWithTheAuthoredStep()
        {
            var r = Build(waterLevel: 0f);
            r.ClipPlayer.Configure(ClipSkin());
            yield return null;

            r.Switcher.BeginInteract();

            bool coverBeforeRungs = false, sawRungs = false, coverAfterRungs = false;
            float spent = 0f;
            while (r.Switcher.IsBoardingMove && spent < 20f)
            {
                yield return null;
                spent += Time.deltaTime;
                if (!r.ClipPlayer.IsPlaying) continue;

                if (r.ClipPlayer.Clip == CharacterClip.LadderDown) { sawRungs = true; continue; }
                if (r.ClipPlayer.Clip != CharacterClip.BoardDown) continue;
                if (sawRungs) coverAfterRungs = true; else coverBeforeRungs = true;
            }

            Assert.IsTrue(coverBeforeRungs,
                "the TURN-AROUND at the top is uncovered — the kit says it is the gap players notice");
            Assert.IsTrue(sawRungs, "the fisher never got onto the rungs");
            Assert.IsTrue(coverAfterRungs,
                "the STEP OFF at the bottom onto the gunwale is uncovered");
        }

        // =============================================================================================
        //  4. IT IS STILL THE SAME MOVE
        // =============================================================================================

        /// <summary>
        /// The climb lands the fisher exactly where boarding has always landed her, through the very same
        /// <c>BoardDeck()</c> behind the very same gates. A new route must not become a new transition.
        /// </summary>
        [UnityTest]
        public IEnumerator TheClimbLands_ThroughTheOrdinaryBoardingTransition()
        {
            var r = Build(waterLevel: 0f);
            r.ClipPlayer.Configure(ClipSkin());
            yield return null;

            var seen = new List<ControlMode>();
            EventBus.Subscribe<ControlModeChanged>(e => seen.Add(e.Mode));

            r.Switcher.BeginInteract();
            Assert.IsTrue(r.Switcher.IsLadderBoarding);
            Assert.AreEqual(ControlMode.OnFoot, r.Switcher.Mode,
                "the mode must NOT flip while she is still on the rungs");

            yield return RunTheMoveOut(r);

            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode);
            Assert.Contains(ControlMode.OnDeck, seen,
                "the climb must land through the ordinary transition, publishing the ordinary signal");
        }

        /// <summary>The move holds the interact key for its whole length — a climb is longer than a vault,
        /// so this window is bigger and matters more — and gives it back when it lands.</summary>
        [UnityTest]
        public IEnumerator TheClimb_HoldsTheInteractGateAndGivesItBack()
        {
            var r = Build(waterLevel: 0f);
            r.ClipPlayer.Configure(ClipSkin());
            yield return null;

            r.Switcher.BeginInteract();
            Assert.IsTrue(r.Switcher.IsLadderBoarding);
            Assert.IsTrue(InteractionGate.IsBlocked,
                "the move must own the interact key while the fisher is on the rungs");

            yield return RunTheMoveOut(r);

            Assert.IsFalse(InteractionGate.IsBlocked, "and hand it back when she lands");
        }
    }
}
