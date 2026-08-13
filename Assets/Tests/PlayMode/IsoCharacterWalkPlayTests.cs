using System.Collections;
using System.Collections.Generic;
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
    ///
    /// <para><b>BOTH bake conventions are driven here, because both are live.</b> The fixture def is the
    /// SHIPPED one — <c>FisherIso.asset</c> carries <c>FacingsAreCounterClockwise: 0</c>, the character rig
    /// having been corrected at source and all twelve body sheets re-baked — so every test below runs the
    /// configuration the game actually ships in.
    /// <see cref="WalkingEast_ThenSwappingToMIRRORED_Art_MovesTheRow"/> then swaps a deliberately
    /// counter-clockwise FIXTURE def onto the same live fisher for the other convention, which the iso
    /// BOAT kits still declare.</para>
    ///
    /// <para><b>What these tests claim, and what they do not.</b> No test here asserts a row because "that
    /// is how the art is baked" — they assert that the presenter routes through whatever the DEF declares,
    /// which is the only thing this component promises. Which way the shipped art ACTUALLY runs is proved
    /// against the PIXELS in <c>CharacterIsoFacingTests</c> (EditMode), never here and never against a
    /// constant: asserting a mapping against the mapping is exactly how the mirrored boat art shipped
    /// green and cost the owner six playtest defects.</para>
    /// </summary>
    public class IsoCharacterWalkPlayTests
    {
        const int Directions = 8, IdleFrames = 6, WalkFrames = 8, RunFrames = 6;

        // The row heading 90° (East) lands on under each bake convention. IsoFacing mirrors the lookup as
        // idx → count − idx, so East is row 2 clockwise and row 8 − 2 = 6 counter-clockwise.
        //
        // East is the heading worth asserting precisely BECAUSE a mirror swaps only the east/west rows:
        // North and South are their own mirrors and read identically either way, which is how the original
        // bake defect stayed hidden for so long. A test that walked South would pass under both.
        const int EastRowClockwise = 2, EastRowMirrored = 6;

        GameObject _go;
        IsoCharacterSprite _iso;
        CharacterVisualDef _def;

        // Every def and sheet-texture handed out below, so the second skin a test builds is torn down too.
        readonly List<Object> _spawned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            // The SHIPPED convention: FisherIso.asset declares FacingsAreCounterClockwise: 0.
            _def = NewVisual(counterClockwise: false);

            _go = new GameObject("Fisher");
            _go.AddComponent<SpriteRenderer>();
            _iso = _go.AddComponent<IsoCharacterSprite>();
            _iso.Configure(_def);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            // Reverse order, so a def always goes before the sheets it points at.
            for (int i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        /// <summary>A complete 8-way skin over throwaway sprites, declared baked whichever way is asked for.</summary>
        CharacterVisualDef NewVisual(bool counterClockwise)
        {
            var def = ScriptableObject.CreateInstance<CharacterVisualDef>();
            def.FacingCount = Directions;
            def.FacingsAreCounterClockwise = counterClockwise;
            def.IdleFrameCount = IdleFrames; def.WalkFrameCount = WalkFrames; def.RunFrameCount = RunFrames;
            def.IdleSheet = Fill(Directions * IdleFrames);
            def.WalkSheet = Fill(Directions * WalkFrames);
            def.RunSheet = Fill(Directions * RunFrames);
            def.WalkSpeedThreshold = 0.35f;
            def.RunSpeedThreshold = 4.5f;
            _spawned.Add(def);
            return def;
        }

        Sprite[] Fill(int n)
        {
            var tex = new Texture2D(2, 2);
            _spawned.Add(tex);
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
            Assert.AreEqual(EastRowClockwise, _iso.FacingRow,
                "on clockwise-baked art — which every shipped character kit now is — East IS the row " +
                "labelled 'E'");
            Assert.AreEqual(CharacterGait.Walk, _iso.Gait, "3 m/s is a walk, not a run");
        }

        /// <summary>
        /// The MIRRORED lookup, DRIVEN — and driven through the SAME fisher, mid-walk, so both conventions
        /// are measured off one live presenter rather than compared across two fixtures.
        ///
        /// <para>⚠️ The def swapped in here is a deliberately counter-clockwise <b>fixture</b>. It is NOT a
        /// picture of the shipped Fisher art — that is the fixture def, and it is clockwise. The mirrored
        /// path is kept covered because it still ships: the iso BOAT kits were never re-baked and
        /// <c>BoatVisualDef.FacingsAreCounterClockwise</c> stays true for them, so the mirror inside
        /// <c>IsoFacing.HeadingToFacingIndex</c> is live code that a character def could legally ask for
        /// again the day a kit arrives baked that way.</para>
        ///
        /// <para>Same object, same heading, no re-Awake — only the def changed, and the row moved. That is
        /// the component's whole claim: the convention lives in the DEF and the presenter holds none of its
        /// own, not even one cached at startup.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingEast_ThenSwappingToMIRRORED_Art_MovesTheRow()
        {
            yield return Travel(90f, 3f, 0.5f);
            int rowOnShippedBake = _iso.FacingRow;
            Assert.AreEqual(EastRowClockwise, rowOnShippedBake, "still walking East on the shipped bake");

            // Same fisher, still walking East — only the artwork's declared bake direction changes.
            var mirrored = NewVisual(counterClockwise: true);
            _iso.Configure(mirrored);
            yield return Travel(90f, 3f, 0.2f);

            Assert.AreEqual(mirrored.FacingRowFor(90f), _iso.FacingRow,
                "the presenter must land on the row the def says depicts East — whichever way that def " +
                "declares its art was baked");
            Assert.AreEqual(EastRowMirrored, _iso.FacingRow,
                "mirrored art bakes East at row 6 — NOT the row labelled 'E'");
            Assert.AreNotEqual(rowOnShippedBake, _iso.FacingRow,
                "the row MUST move when the bake direction flips under a fisher walking the same way. If " +
                "these ever agree, the mirror in IsoFacing has stopped being consulted and every " +
                "counter-clockwise kit is drawn reversed.");
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
