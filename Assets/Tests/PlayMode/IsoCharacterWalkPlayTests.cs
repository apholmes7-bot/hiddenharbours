using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The iso presenter DRIVING, in a real frame loop — the part EditMode can't see: it reads motion off
    /// its own transform in LateUpdate, so nothing proves it works until something actually moves.
    ///
    /// <para>⚠️ Frame count is NOT time here. Headless, 60 <c>yield return null</c>s can pass in ~45 ms, so
    /// these spin on a CONDITION with a real-seconds budget (the <c>PilotableFleetPlayTests</c> pattern)
    /// rather than counting frames and hoping.</para>
    /// </summary>
    public class IsoCharacterWalkPlayTests
    {
        const int Directions = 8, IdleFrames = 6, WalkFrames = 8, RunFrames = 6;

        GameObject _go;
        IsoCharacterSprite _iso;
        CharacterVisualDef _def;

        [SetUp]
        public void SetUp()
        {
            _def = ScriptableObject.CreateInstance<CharacterVisualDef>();
            _def.FacingCount = Directions;
            _def.FacingsAreCounterClockwise = true;   // as the shipped Fisher art is baked
            _def.IdleFrameCount = IdleFrames; _def.WalkFrameCount = WalkFrames; _def.RunFrameCount = RunFrames;
            _def.IdleSheet = Fill(Directions * IdleFrames);
            _def.WalkSheet = Fill(Directions * WalkFrames);
            _def.RunSheet = Fill(Directions * RunFrames);
            _def.WalkSpeedThreshold = 0.35f;
            _def.RunSpeedThreshold = 4.5f;

            _go = new GameObject("Fisher");
            _go.AddComponent<SpriteRenderer>();
            _iso = _go.AddComponent<IsoCharacterSprite>();
            _iso.Configure(_def);
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

        /// <summary>Spin until <paramref name="done"/> or the real-seconds budget runs out.</summary>
        static IEnumerator SpinUntil(System.Func<bool> done, float budgetSeconds)
        {
            float deadline = Time.realtimeSinceStartup + budgetSeconds;
            while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>Walk the object at a constant speed on a WORLD-XY heading for a real-seconds stretch —
        /// deliberately how <c>PlayerWalkController</c> moves the fisher (uniform speed in world XY), not
        /// a ground-metres walk. Off the cardinals the GROUND bearing this travels is a different number;
        /// that is the whole point of <see cref="WalkingAWorldDiagonal_ShowsTheRowThatDepictsItsGroundBearing"/>.</summary>
        IEnumerator Travel(float headingDeg, float speed, float seconds)
        {
            var dir = new Vector2(Mathf.Sin(headingDeg * Mathf.Deg2Rad), Mathf.Cos(headingDeg * Mathf.Deg2Rad));
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                _go.transform.localPosition += (Vector3)(dir * speed * Time.deltaTime);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator WalkingEast_ShowsTheRowThatDepictsEast_AndTheWalkCycle()
        {
            yield return Travel(90f, 3f, 0.5f);

            Assert.AreEqual(_def.FacingRowFor(90f), _iso.FacingRow,
                "the presenter must land on the row the def says depicts East");
            Assert.AreEqual(6, _iso.FacingRow,
                "the art bakes counter-clockwise, so East is row 6 — not the row labelled 'E'");
            Assert.AreEqual(CharacterGait.Walk, _iso.Gait, "3 m/s is a walk, not a run");
        }

        [UnityTest]
        public IEnumerator WalkingAWorldDiagonal_ShowsTheRowThatDepictsItsGroundBearing()
        {
            // World XY is the SQUASHED ground plane, and the baked rows are evenly-spaced GROUND bearings
            // (measured — see IsoGroundTests). So a 28° world walk is really an 18.9° ground walk, and the
            // fisher must be drawn very nearly facing NORTH. Before the un-squash she was turned a whole
            // cell to the north-east. Driven through the real frame loop, because the read that used to be
            // wrong is the one LateUpdate takes off the transform.
            yield return Travel(28f, 3f, 0.5f);

            float ground = IsoGround.BearingDegrees(
                new Vector2(Mathf.Sin(28f * Mathf.Deg2Rad), Mathf.Cos(28f * Mathf.Deg2Rad)));

            Assert.AreEqual(ground, _iso.HeadingDegrees, 0.5f,
                "the presenter publishes the GROUND bearing it is travelling, not the world-XY angle");
            Assert.AreEqual(_def.FacingRowFor(ground), _iso.FacingRow,
                "…and picks the row that depicts it");
            Assert.AreNotEqual(_def.FacingRowFor(28f), _iso.FacingRow,
                "the un-corrected read lands on the neighbouring row — if these ever agree, the " +
                "un-squash has been removed or the bands have moved");
        }

        [UnityTest]
        public IEnumerator Running_SwitchesToTheRunSheet_OnceThresholdSpeedIsSustained()
        {
            yield return Travel(180f, 6f, 0.5f);
            Assert.AreEqual(CharacterGait.Run, _iso.Gait, "6 m/s is over the 4.5 run threshold");
        }

        [UnityTest]
        public IEnumerator StoppingHOLDSTheFacing_AndDropsToIdle()
        {
            yield return Travel(270f, 3f, 0.5f);
            int facingWhileWalking = _iso.FacingRow;
            Assert.AreEqual(_def.FacingRowFor(270f), facingWhileWalking, "walking West");

            // Stand perfectly still and let the smoothed speed settle out.
            yield return SpinUntil(() => _iso.Gait == CharacterGait.Idle, 2f);

            Assert.AreEqual(CharacterGait.Idle, _iso.Gait, "standing still is idle");
            Assert.AreEqual(facingWhileWalking, _iso.FacingRow,
                "a fisher who stops keeps looking where they were going — never snapping back to North");
        }

        [UnityTest]
        public IEnumerator MotionIsReadRELATIVEToTheParent_SoADriftingDeckDoesNotMakeTheFisherStride()
        {
            var boat = new GameObject("BoatRoot");
            try
            {
                _go.transform.SetParent(boat.transform, worldPositionStays: false);
                yield return SpinUntil(() => _iso.Gait == CharacterGait.Idle, 2f);
                Assert.AreEqual(CharacterGait.Idle, _iso.Gait, "standing on the deck to begin with");

                // The boat motors off; the fisher stands perfectly still ON it.
                float deadline = Time.realtimeSinceStartup + 0.5f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    boat.transform.position += Vector3.right * 8f * Time.deltaTime;
                    yield return null;
                }

                Assert.AreEqual(CharacterGait.Idle, _iso.Gait,
                    "the fisher never moved relative to the deck — reading WORLD motion here would have " +
                    "them sprinting on the spot every time the boat drifted underneath them");
            }
            finally { Object.DestroyImmediate(boat); }
        }
    }
}
