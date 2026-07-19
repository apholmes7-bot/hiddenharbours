using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// HOLD SHIFT AND THE FISHER RUNS — end to end, on a real keyboard and real physics.
    ///
    /// <para><b>Why this can't be an EditMode test.</b> The feature is a CHAIN, and every link of it only
    /// exists once the engine is running: a key press → <c>Update</c> → the sprint speed → <c>FixedUpdate</c>
    /// writing <c>Rigidbody2D.linearVelocity</c> → Unity's integrator actually moving the transform →
    /// <see cref="IsoCharacterSprite"/> MEASURING that motion in <c>LateUpdate</c> and choosing a sheet. The
    /// pure seam (<see cref="PlayerWalkController.SpeedFor"/>) is unit-tested next door; what is proved HERE
    /// is that the speed the controller picks is the speed the presenter measures — the join nothing else
    /// covers, and the exact join that was broken (a flat 3 m/s could never reach a 4.5 m/s run threshold,
    /// so the run sheet was dead art).</para>
    ///
    /// <para><b>There is deliberately no "is running" flag to assert on.</b> Sprint is expressed as SPEED
    /// and nothing else; the presenter's ordinary speed→gait ladder does the rest. So this test asserts on
    /// the gait the presenter independently arrived at, which is the only honest evidence that the two
    /// halves agree.</para>
    ///
    /// <para>⚠️ Frame count is NOT time here. Headless, 60 <c>yield return null</c>s can pass in ~45 ms, so
    /// these spin on a CONDITION with a real-seconds budget (the <c>PilotableFleetPlayTests</c> pattern).</para>
    /// </summary>
    public class PlayerSprintPlayTests
    {
        const int Directions = 8, IdleFrames = 6, WalkFrames = 8, RunFrames = 6;

        // How long a gait change may take to settle: the presenter smooths the measured speed, and the
        // rigidbody needs a physics step or two. Generous — it is a deadline, not a sleep; the spins
        // return the instant the condition is met.
        const float SettleBudget = 3f;

        GameObject _go;
        Rigidbody2D _rb;
        IsoCharacterSprite _iso;
        CharacterVisualDef _def;
        Keyboard _keyboard;

        [SetUp]
        public void SetUp()
        {
            // A synthetic skin rather than the committed FisherIso asset: this test is about the CONTROLLER
            // clearing the threshold, and it must not go red because a concurrent art PR is editing that
            // asset. The thresholds mirror the shipped def's (walk 0.35, run 4.5).
            _def = ScriptableObject.CreateInstance<CharacterVisualDef>();
            _def.FacingCount = Directions;
            _def.IdleFrameCount = IdleFrames; _def.WalkFrameCount = WalkFrames; _def.RunFrameCount = RunFrames;
            _def.IdleSheet = Fill(Directions * IdleFrames);
            _def.WalkSheet = Fill(Directions * WalkFrames);
            _def.RunSheet = Fill(Directions * RunFrames);

            _go = new GameObject("Fisher");
            _go.AddComponent<SpriteRenderer>();
            _rb = _go.AddComponent<Rigidbody2D>();
            _iso = _go.AddComponent<IsoCharacterSprite>();
            _iso.Configure(_def);
            // The REAL controller with its REAL serialized defaults — the walk speed and the sprint speed
            // are read from the component, never restated here, so re-tuning either re-times this test.
            _go.AddComponent<PlayerWalkController>();

            // A virtual keyboard, so the test presses the same keys the player does rather than reaching
            // past the input read. Batch-mode has no real keyboard; this becomes Keyboard.current.
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
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

        /// <summary>Hold exactly these keys (a full keyboard state — anything absent is released).</summary>
        void Hold(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        /// <summary>Spin until <paramref name="done"/> or the real-seconds budget runs out.</summary>
        static IEnumerator SpinUntil(System.Func<bool> done, float budgetSeconds)
        {
            float deadline = Time.realtimeSinceStartup + budgetSeconds;
            while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        [UnityTest]
        public IEnumerator HoldingShift_PushesTheFisherPastTheRunThreshold_AndTheRunSheetPlays()
        {
            // Walk first: W alone. This is the regression the fix must not undo — the ordinary walk must
            // still read as a WALK, or we'd have "fixed" the run sheet by making everything a run.
            Hold(Key.W);
            yield return SpinUntil(() => _iso.Gait == CharacterGait.Walk, SettleBudget);
            Assert.AreEqual(CharacterGait.Walk, _iso.Gait, "W alone is a walk");
            Assert.Less(_rb.linearVelocity.magnitude, _def.RunSpeedThreshold,
                "the plain walk must stay UNDER the run threshold — that is what makes sprint mean something");

            // Now the feature: Shift + W.
            Hold(Key.W, Key.LeftShift);
            yield return SpinUntil(() => _iso.Gait == CharacterGait.Run, SettleBudget);

            Assert.Greater(_rb.linearVelocity.magnitude, _def.RunSpeedThreshold,
                "holding Shift must raise the ACTUAL movement speed past the skin's run threshold — that is " +
                "the entire mechanism; the presenter only ever sees speed");
            Assert.AreEqual(CharacterGait.Run, _iso.Gait,
                "…and the presenter, choosing on measured speed alone, must therefore select the RUN sheet");
        }

        [UnityTest]
        public IEnumerator ReleasingShift_DropsHerBackToAWalk_WithoutStopping()
        {
            Hold(Key.W, Key.LeftShift);
            yield return SpinUntil(() => _iso.Gait == CharacterGait.Run, SettleBudget);
            Assert.AreEqual(CharacterGait.Run, _iso.Gait, "running before we let go");

            // Let go of Shift but KEEP walking — the interesting release, because it must fall back to the
            // walk cycle rather than to idle.
            Hold(Key.W);
            yield return SpinUntil(() => _iso.Gait == CharacterGait.Walk, SettleBudget);

            Assert.AreEqual(CharacterGait.Walk, _iso.Gait, "releasing Shift falls back to the walk sheet");
            Assert.Greater(_rb.linearVelocity.magnitude, _def.WalkSpeedThreshold,
                "…and she is still walking, not stopped — only the sprint was released");
            Assert.Less(_rb.linearVelocity.magnitude, _def.RunSpeedThreshold, "back under the run threshold");
        }

        [UnityTest]
        public IEnumerator ShiftAlone_DoesNothing_SprintIsAMultiplierOnAMoveYouAreAlreadyMaking()
        {
            Hold(Key.LeftShift);
            yield return SpinUntil(() => _iso.Gait == CharacterGait.Idle, SettleBudget);

            Assert.AreEqual(Vector2.zero, _rb.linearVelocity, "Shift is not a move key");
            Assert.AreEqual(CharacterGait.Idle, _iso.Gait, "a fisher standing still with Shift held is idle");
        }
    }
}
