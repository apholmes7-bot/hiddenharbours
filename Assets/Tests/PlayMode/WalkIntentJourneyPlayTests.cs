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
    /// ⭐ <b>THE FISHER WALKS ON A HELD INTENT, THROUGH THE REAL CONTROLLER</b> (ADR 0043, PR 0) — the
    /// on-foot twin of <c>RoadFleetJourneyPlayTests</c>: a scripted source is handed to the shipped
    /// <see cref="PlayerWalkController"/>, whose own <c>Update</c> reads it and whose own
    /// <c>FixedUpdate</c> moves the rigidbody on what it read. Nothing here writes a velocity.
    ///
    /// <para><b>Why this exists.</b> Before the seam the only way to move her headless was to write the
    /// rigidbody directly with the controller DISABLED (the sprint fixture's own words: with no key held
    /// it "would stand rooted to the spot"). That proved the presenter; it could not prove the
    /// controller. A held source retires that at the root: the controller runs, is consulted every
    /// frame (<see cref="HeldWalkIntents.Reads"/> is the anti-vacuous number), and covers ground at the
    /// speed it declares.</para>
    ///
    /// <para><b>The read is in <c>Update</c>, so an intent set from a coroutine lands one frame LATER, and
    /// a frame runs its physics steps BEFORE its Update.</b> Every leg here sets the intent, yields one
    /// frame, and only THEN starts the clock (memory
    /// <c>a-scripted-driver-lands-the-demand-and-passes-the-waypoint</c>).</para>
    ///
    /// <para>⚠️ Frames are not time: the legs run on <see cref="Time.time"/> (the physics clock) against
    /// a wall-clock ceiling, and the speeds are READ off the real controller's serialized tunables so a
    /// re-tune re-times the test rather than failing it.</para>
    /// </summary>
    public class WalkIntentJourneyPlayTests
    {
        private const float LegRealSeconds = 0.6f;
        private const float WallClockCeiling = 10f;

        private GameObject _root;
        private GameObject _fisher;
        private Rigidbody2D _rb;
        private PlayerWalkController _walk;
        private HeldWalkIntents _held;

        [SetUp]
        public void SetUp()
        {
            MoveActionClaim.Reset();
            ShellPause.Reset();

            _root = new GameObject("WalkIntentJourney");
            _root.AddComponent<AudioListener>();   // one listener, or Unity logs on every frame

            _fisher = new GameObject("Fisher");
            _fisher.transform.SetParent(_root.transform);
            _fisher.transform.position = Vector3.zero;
            _fisher.AddComponent<SpriteRenderer>();
            _rb = _fisher.AddComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            // The REAL controller, ENABLED, on the greybox fallbacks (no GameConfig, no tide gate, no
            // frames): it reads its source in Update and writes the rigidbody in FixedUpdate.
            _walk = _fisher.AddComponent<PlayerWalkController>();
            _held = new HeldWalkIntents();
            _walk.ConfigureWalkInput(_held);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            MoveActionClaim.Reset();
            ShellPause.Reset();
        }

        /// <summary>Read an owner tunable off the REAL component rather than restating it (the sprint
        /// fixture's discipline).</summary>
        private float Tunable(string field)
        {
#if UNITY_EDITOR
            SerializedProperty prop = new SerializedObject(_walk).FindProperty(field);
            Assert.IsNotNull(prop, $"PlayerWalkController.{field} was renamed or removed — re-point this read.");
            return prop.floatValue;
#else
            Assert.Ignore("needs the editor to read serialized tunables");
            return 0f;
#endif
        }

        /// <summary>
        /// Hold an intent for a stretch of physics time and return the distance she covered per second of
        /// it. Sets the intent, lands it (one frame), THEN measures from the position and the clock at
        /// that moment — so the frame the intent was still in flight is not on the stopwatch.
        /// </summary>
        private IEnumerator Leg(Vector2 move, bool sprint, System.Action<float> metresPerSecond)
        {
            _held.Walk(move, sprint);
            yield return null;   // the read is in Update: land the intent on the controller first

            int readsBefore = _held.Reads;
            Vector2 from = _rb.position;
            float t0 = Time.time;
            float deadline = Time.realtimeSinceStartup + LegRealSeconds;
            int frames = 0;
            while (Time.realtimeSinceStartup < deadline)
            {
                Assert.Less(Time.realtimeSinceStartup - (deadline - LegRealSeconds), WallClockCeiling);
                yield return null;
                frames++;
            }
            float elapsed = Time.time - t0;
            Assert.Greater(elapsed, 0.1f, "the physics clock barely moved — the leg measured nothing.");
            Assert.GreaterOrEqual(_held.Reads - readsBefore, frames,
                "the controller did not ask the source on every frame of the leg — the seam is not being consulted.");
            metresPerSecond(Vector2.Distance(from, _rb.position) / elapsed);
        }

        [UnityTest]
        public IEnumerator SheWalksEast_AtTheWalkSpeed_AndFacesTheWayShe_IsGoing()
        {
            float walkSpeed = Tunable("_moveSpeed");
            float step = walkSpeed * Time.fixedDeltaTime;

            yield return null;   // Awake, and the first (idle) read
            Assert.AreEqual(Vector2.zero, _rb.position, "she moved before anybody asked her to.");

            float rate = 0f;
            yield return Leg(Vector2.right, false, r => rate = r);

            Assert.AreEqual(walkSpeed, rate, step * 2f + 0.05f,
                $"holding east she covered {rate:F2} m/s against a declared walk of {walkSpeed:F2} — the " +
                "intent is not reaching the rigidbody at the speed the controller declares.");
            Assert.Greater(_rb.position.x, 0.5f, "she did not go EAST.");
            Assert.AreEqual(0f, _rb.position.y, 0.01f, "she drifted off the line.");
            Assert.AreEqual(Facing.Right, _walk.CurrentFacing, "she is walking east and not facing it.");
        }

        [UnityTest]
        public IEnumerator SprintingOnTheIntent_IsTheSprintSpeed_NotTheWalk()
        {
            float walkSpeed = Tunable("_moveSpeed");
            float sprintSpeed = Tunable("_sprintSpeed");
            float step = sprintSpeed * Time.fixedDeltaTime;
            Assert.Greater(sprintSpeed, walkSpeed, "the tunables no longer make sprint faster than a walk.");

            yield return null;
            float rate = 0f;
            yield return Leg(Vector2.up, true, r => rate = r);

            Assert.AreEqual(sprintSpeed, rate, step * 2f + 0.05f,
                $"sprinting north she covered {rate:F2} m/s against a declared sprint of {sprintSpeed:F2}.");
            Assert.AreEqual(Facing.Up, _walk.CurrentFacing);
        }

        [UnityTest]
        public IEnumerator ADiagonalOnTheIntent_IsClampedByTheController_NotDoubled()
        {
            // The composite hands the walk (±1, ±1) for a diagonal — the OLD summed keys — and the
            // controller clamps it to unit length itself (VelocityFor). A held source hands the same
            // unclamped vector in, so this is the clamp proved on the real path.
            float walkSpeed = Tunable("_moveSpeed");
            float step = walkSpeed * Time.fixedDeltaTime;

            yield return null;
            float rate = 0f;
            yield return Leg(new Vector2(1f, 1f), false, r => rate = r);

            Assert.AreEqual(walkSpeed, rate, step * 2f + 0.05f,
                $"a (1, 1) intent moved her at {rate:F2} m/s — the diagonal must be clamped to the walk " +
                $"speed ({walkSpeed:F2}), not run at √2 of it.");
        }

        [UnityTest]
        public IEnumerator ReleasingTheIntent_StopsHer_OnTheNextPhysicsStep()
        {
            yield return null;
            float rate = 0f;
            yield return Leg(Vector2.left, false, r => rate = r);
            Assert.Greater(rate, 0.5f, "she never got going.");

            _held.Release();
            yield return null;                      // land the release
            yield return new WaitForFixedUpdate();  // the step that writes the zero
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(0f, _rb.linearVelocity.magnitude, 1e-3f,
                "released, and she is still moving — a released key IS a zero, and so is a released intent.");

            Vector2 rest = _rb.position;
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(0f, Vector2.Distance(rest, _rb.position), 1e-3f, "she crept after the release.");
        }
    }
}
