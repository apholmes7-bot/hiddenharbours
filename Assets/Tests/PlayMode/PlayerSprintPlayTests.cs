using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// HOLD SHIFT AND THE FISHER RUNS — the join, in a real frame loop.
    ///
    /// <para><b>What was broken.</b> <see cref="PlayerWalkController"/> moved at a flat 3 m/s while
    /// <see cref="IsoCharacterSprite"/> breaks into a run at the skin's 4.5 m/s threshold, so the run sheet
    /// was art that could never play. Sprint is expressed as SPEED and nothing else — there is deliberately
    /// no "is running" flag to assert on — so the only honest evidence is the gait the presenter
    /// INDEPENDENTLY arrives at from the motion it measures. That is what these tests assert.</para>
    ///
    /// <para><b>Why this can't be an EditMode test.</b> The presenter reads motion off its own transform in
    /// <c>LateUpdate</c> and smooths it: until something actually moves for a real stretch of time, no gait
    /// is ever chosen. The speeds are not restated here either — they are read off the REAL controller's
    /// serialized fields, so re-tuning <c>_sprintSpeed</c> re-times these tests and dropping it under the
    /// run threshold turns them red (which is exactly the sabotage that proves they mean something).</para>
    ///
    /// <para><b>⚠️ What these tests do NOT cover: the literal key scan.</b> Headless batch mode has no
    /// focused view, so the Input System resets every device's state each frame
    /// (<c>ResetAndDisableNonBackgroundDevices</c>) — a virtual keyboard press CAN be injected, but it is
    /// wiped before the next <c>Update</c> reads it, and <c>IgnoreFocus</c> does not save it (measured, not
    /// assumed). So a HELD key is not reachable in this harness. What is left uncovered is exactly one
    /// branchless line — <c>kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed</c>; everything it feeds
    /// is covered here and in <c>PlayerWalkTests</c>. Verify the key itself by hand (hold Shift, watch her
    /// run) or in a focused editor Play session.</para>
    ///
    /// <para>⚠️ Frame count is NOT time here. Headless, 60 <c>yield return null</c>s can pass in ~45 ms, so
    /// these spin on a CONDITION with a real-seconds budget (the <c>PilotableFleetPlayTests</c> pattern).</para>
    /// </summary>
    public class PlayerSprintPlayTests
    {
        const int Directions = 8, IdleFrames = 6, WalkFrames = 8, RunFrames = 6;

        // How long a gait change may take to settle: the presenter smooths the measured speed. Generous —
        // it is a deadline, not a sleep; every spin returns the instant its condition is met.
        const float SettleBudget = 3f;

        GameObject _go;
        Rigidbody2D _rb;
        IsoCharacterSprite _iso;
        CharacterVisualDef _def;
        PlayerWalkController _walk;

        [SetUp]
        public void SetUp()
        {
            // A synthetic skin rather than the committed FisherIso asset: these tests are about the
            // CONTROLLER clearing the threshold, and must not go red because an art PR is editing that
            // asset. The thresholds mirror the shipped def's.
            _def = ScriptableObject.CreateInstance<CharacterVisualDef>();
            _def.FacingCount = Directions;
            _def.IdleFrameCount = IdleFrames; _def.WalkFrameCount = WalkFrames; _def.RunFrameCount = RunFrames;
            _def.IdleSheet = Fill(Directions * IdleFrames);
            _def.WalkSheet = Fill(Directions * WalkFrames);
            _def.RunSheet = Fill(Directions * RunFrames);

            _go = new GameObject("Fisher");
            _go.AddComponent<SpriteRenderer>();
            _rb = _go.AddComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _iso = _go.AddComponent<IsoCharacterSprite>();
            _iso.Configure(_def);
            // The REAL controller, for its REAL serialized speeds (see Tunable) — but DISABLED. Left
            // running it would drive the rigidbody itself, and with no keys held headless (see the class
            // note) that means writing zero velocity every physics step: the fisher would stand rooted to
            // the spot and every gait would read Idle. The tests move her at the speeds it declares.
            _walk = _go.AddComponent<PlayerWalkController>();
            _walk.enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_def != null) Object.DestroyImmediate(_def);
        }

        static Sprite[] Fill(int n)
        {
            var tex = new Texture2D(2, 2);
            var set = new Sprite[n];
            for (int i = 0; i < n; i++)
                set[i] = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0f), 32f);
            return set;
        }

        /// <summary>
        /// Read an owner tunable off the REAL component rather than restating it here. A test that
        /// re-declared 5.5 would still be green the day the owner re-tuned it — and green for a reason that
        /// no longer matches the product. Read it, and a re-tune re-times the test for free.
        /// </summary>
        float Tunable(string field)
        {
#if UNITY_EDITOR
            var prop = new SerializedObject(_walk).FindProperty(field);
            Assert.IsNotNull(prop,
                $"PlayerWalkController.{field} was renamed or removed. These tests DERIVE the speeds from " +
                "the real serialized tunables on purpose — re-point this read; do not hard-code a number.");
            return prop.floatValue;
#else
            Assert.Ignore("needs the editor to read serialized tunables");
            return 0f;
#endif
        }

        /// <summary>Move the fisher at a speed for a real-seconds stretch, through real frames.</summary>
        IEnumerator TravelAt(float speed, float seconds)
        {
            _rb.linearVelocity = new Vector2(0f, -speed);   // heading south; the gait is what's under test
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>Spin until <paramref name="done"/> or the real-seconds budget runs out.</summary>
        static IEnumerator SpinUntil(System.Func<bool> done, float budgetSeconds)
        {
            float deadline = Time.realtimeSinceStartup + budgetSeconds;
            while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        [UnityTest]
        public IEnumerator TheSprintSpeed_ActuallyLightsTheRunSheet()
        {
            float sprint = PlayerWalkController.SpeedFor(true, Tunable("_moveSpeed"), Tunable("_sprintSpeed"),
                                                         float.NegativeInfinity, Tunable("_wadeDepth"));
            Assert.Greater(sprint, _def.RunSpeedThreshold,
                "the controller's sprint speed must clear the skin's run threshold — this is the whole fix");

            yield return TravelAt(sprint, 0.5f);

            Assert.AreEqual(CharacterGait.Run, _iso.Gait,
                "travelling at the controller's sprint speed, the presenter must select the RUN sheet — it " +
                "chooses on measured speed alone, so this is the only thing that can make the run art play");
        }

        [UnityTest]
        public IEnumerator TheWalkSpeed_StillReadsAsAWalk()
        {
            // The regression guard: don't 'fix' the run sheet by making everything a run.
            float walk = PlayerWalkController.SpeedFor(false, Tunable("_moveSpeed"), Tunable("_sprintSpeed"),
                                                       float.NegativeInfinity, Tunable("_wadeDepth"));
            yield return TravelAt(walk, 0.5f);

            Assert.AreEqual(CharacterGait.Walk, _iso.Gait, "the ordinary walk must stay a walk");
            Assert.Less(walk, _def.RunSpeedThreshold, "…and stay under the run threshold, which is why " +
                                                      "sprint means something");
        }

        [UnityTest]
        public IEnumerator ReleasingTheSprint_DropsHerBackToAWalk_WithoutStopping()
        {
            float walk = Tunable("_moveSpeed");
            float sprint = PlayerWalkController.SpeedFor(true, walk, Tunable("_sprintSpeed"),
                                                         float.NegativeInfinity, Tunable("_wadeDepth"));

            yield return TravelAt(sprint, 0.5f);
            Assert.AreEqual(CharacterGait.Run, _iso.Gait, "running before we let go");

            // Let the sprint go but KEEP walking — it must fall back to the walk cycle, not to idle.
            _rb.linearVelocity = new Vector2(0f, -walk);
            yield return SpinUntil(() => _iso.Gait == CharacterGait.Walk, SettleBudget);

            Assert.AreEqual(CharacterGait.Walk, _iso.Gait, "releasing the sprint falls back to the walk sheet");
            Assert.AreNotEqual(CharacterGait.Idle, _iso.Gait, "she is still walking, not stopped");
        }

        [UnityTest]
        public IEnumerator SprintingIntoTheWadeBand_ReadsAsAWalkAgain_TheWaterWins()
        {
            // Sprint is allowed while wading, but ApplyWaterEdge's slow-factor multiplies it DOWN — and at
            // the deep edge of the wade band that lands back under the run threshold all by itself. No
            // special case anywhere: the fisher visibly drops to a walk as the water takes her legs.
            float wadeDepth = Tunable("_wadeDepth");
            float sprint = PlayerWalkController.SpeedFor(true, Tunable("_moveSpeed"), Tunable("_sprintSpeed"),
                                                         wadeDepth, wadeDepth);
            var slowed = PlayerWalkController.ApplyWaterEdge(new Vector2(0f, -sprint), Vector2.zero,
                             _ => wadeDepth, 1f, wadeDepth, Tunable("_swimLimit"),
                             Tunable("_wadeSlowFactor"), Tunable("_swimSlowFactor"));

            yield return TravelAt(slowed.magnitude, 0.5f);

            Assert.AreEqual(CharacterGait.Walk, _iso.Gait,
                "a sprint through deep wade water reads as a walk — the wade factor must still bite");
        }
    }
}
