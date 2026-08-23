using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The sleeping beat end to end: the player is laid on the mattress, the sleep clip runs, and
    /// everything is put back.
    ///
    /// <para><b>Putting it back is the half worth guarding.</b> The beat moves the player's transform
    /// and switches their controller off; anything that leaves either in the wrong state strands the
    /// fisher in the mattress with no controls, and it would look like a save bug rather than a
    /// presentation one.</para>
    ///
    /// <para>⚠️ No <c>EventBus.Clear</c> — it unsubscribes live components, and test order is not
    /// guaranteed. Teardown destroys the fixture's own object.</para>
    /// </summary>
    public class SleepBeatPlayTests
    {
        const string FisherVisual = "Assets/_Project/Data/Characters/FisherIso.asset";
        static readonly Vector3 StandingAt = new Vector3(4f, 4f, 7f);
        static readonly Vector2 Mattress = new Vector2(9f, -2f);

        GameObject _go;
        CharacterClipPlayer _clips;
        PlayerSleepPresenter _sleep;
        int _ended;
        bool _lastCompleted;

        void OnEnded(SleepBeatEnded e) { _ended++; _lastCompleted = e.Completed; }

        [SetUp]
        public void SetUp()
        {
            var visual = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(FisherVisual);
            Assert.IsNotNull(visual, $"no character visual at '{FisherVisual}'");
            Assert.IsTrue(visual.HasClip(CharacterClip.Sleep), "the fisher's sleep clip is not wired");

            _ended = 0;
            _lastCompleted = false;
            EventBus.Subscribe<SleepBeatEnded>(OnEnded);

            _go = new GameObject("SleepBeatPlayTests.Player");
            _go.SetActive(false);
            _go.transform.position = StandingAt;
            _go.AddComponent<SpriteRenderer>();
            _go.AddComponent<IsoCharacterSprite>().Configure(visual);
            _clips = _go.AddComponent<CharacterClipPlayer>();
            _clips.Configure(visual);
            _sleep = _go.AddComponent<PlayerSleepPresenter>();
            _go.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<SleepBeatEnded>(OnEnded);
            if (_go != null) Object.DestroyImmediate(_go);
        }

        static void Turn(Vector2 bed) => EventBus.Publish(new SleepBeatRequested(bed, 1, "the cabin"));

        /// <summary>Turn in at a bed that says which end its pillow is on, in a room at
        /// <paramref name="roomFacing"/>.</summary>
        static void TurnOnADeclaredBed(Vector2 bed, PillowSide side, int roomFacing) =>
            EventBus.Publish(new SleepBeatRequested(
                bed, 1, "the cabin",
                new PillowAnchor(side, BedPillow.YawDegreesFor(roomFacing, 8), 0.705f,
                                 IsoGround.GroundDepthScale)));

        [UnityTest]
        public IEnumerator TheSLEEPINGSPRITEFacesTheWayTheBedSaysItsPillowIs()
        {
            // ⭐ THE DEFECT, end to end and against real baked art: the clip's facing ROW — the actual
            // sprite the owner will look at — has to come from the bed's declaration. EditMode proves the
            // bearing; only this proves that the bearing reaches the sheet.
            TurnOnADeclaredBed(Mattress, PillowSide.MinusY, roomFacing: 0);
            yield return null;

            Assert.IsTrue(_sleep.IsSleeping);
            CharacterVisualDef visual = _clips.ResolvedVisual();
            Assert.AreEqual(visual.ClipFacingRowFor(CharacterClip.Sleep, 180f), _clips.FacingRow,
                            "a -y pillow in an unturned room lies south");
        }

        [UnityTest]
        public IEnumerator TurningTheROOMTurnsTheSleeperWithIt()
        {
            // The same bed and the same declaration in a building the builder faced differently. Before
            // the declaration the pose played one fixed heading, so these two came out on the SAME row —
            // which is a fisher asleep across her own bed, and reads as bad art rather than as a bug.
            CharacterVisualDef visual = _clips.ResolvedVisual();

            TurnOnADeclaredBed(Mattress, PillowSide.MinusY, roomFacing: 0);
            yield return null;
            int unturned = _clips.FacingRow;

            // Let the beat finish before asking for another — the presenter runs one at a time.
            float waited = 0f;
            while (_sleep.IsSleeping && waited < 8f) { waited += Time.deltaTime; yield return null; }
            Assert.IsFalse(_sleep.IsSleeping, "the first beat never ended");

            TurnOnADeclaredBed(Mattress, PillowSide.MinusY, roomFacing: 2);
            yield return null;
            int turned = _clips.FacingRow;

            Assert.AreEqual(visual.ClipFacingRowFor(CharacterClip.Sleep, 270f), turned,
                            "a -y pillow in a room at facing 2 lies west");

            // A quarter turn only has to land on a DIFFERENT cell if the clip has four or more of them.
            // The shipped kit bakes eight; a later kit that ships a two-row sleep would make the pair
            // legitimately equal, and this test must not then be a false alarm about the bearing.
            if (visual.ClipFacingCount(CharacterClip.Sleep) >= 4)
                Assert.AreNotEqual(unturned, turned,
                                   "the sleeper must turn with the room she is lying in — south and west " +
                                   "are a quarter turn apart, and drawing one cell for both is the bug");
        }

        [UnityTest]
        public IEnumerator ABedThatDeclaresNothingStillRests_OnThePresentersOwnFallback()
        {
            // Degrades whole. Content validation is what stops an undeclared bed reaching a build; if one
            // ever does, the player still gets their rest and their beat.
            Turn(Mattress);
            yield return null;

            Assert.IsTrue(_sleep.IsSleeping, "an undeclared bed must still show a beat");
            Assert.AreEqual(CharacterClip.Sleep, _clips.Clip);
        }

        [UnityTest]
        public IEnumerator TurningIn_LaysTheFisherOnTheMattressAndPlaysTheSleepClip()
        {
            Turn(Mattress);
            yield return null;

            Assert.IsTrue(_sleep.IsSleeping, "the beat should be running");
            Assert.AreEqual(CharacterClip.Sleep, _clips.Clip);
            Assert.AreEqual(Mattress.x, _go.transform.position.x, 0.001f, "not laid on the mattress");
            Assert.AreEqual(Mattress.y, _go.transform.position.y, 0.001f, "not laid on the mattress");
            Assert.AreEqual(StandingAt.z, _go.transform.position.z, 0.001f,
                            "z was flattened — the iso sort band lives there");
        }

        [UnityTest]
        public IEnumerator TheBeatEnds_AndPutsThePlayerBackExactlyWhereTheyStood()
        {
            Turn(Mattress);
            yield return null;
            Assert.IsTrue(_sleep.IsSleeping);

            float waited = 0f;
            while (_sleep.IsSleeping && waited < 8f) { waited += Time.deltaTime; yield return null; }

            Assert.IsFalse(_sleep.IsSleeping, "the beat never ended");
            Assert.AreEqual(CharacterClip.None, _clips.Clip, "the sleep clip must be stopped, not held");
            Assert.AreEqual(StandingAt, _go.transform.position,
                            "the player must be put back exactly where they stood");
            Assert.AreEqual(1, _ended, "SleepBeatEnded must fire exactly once");
            Assert.IsTrue(_lastCompleted, "a beat that ran its course reports Completed");
        }

        [UnityTest]
        public IEnumerator ASecondRequestMidBeat_IsIgnoredRatherThanRestarting()
        {
            // Two beats running at once would each restore a position, and the second would restore the
            // MATTRESS as if it were the standing spot — stranding the fisher in the bed for good.
            Turn(Mattress);
            yield return null;
            Turn(new Vector2(99f, 99f));
            yield return null;

            Assert.IsTrue(_sleep.IsSleeping);
            Assert.AreEqual(Mattress.x, _go.transform.position.x, 0.001f,
                            "the second request moved the sleeper");

            float waited = 0f;
            while (_sleep.IsSleeping && waited < 8f) { waited += Time.deltaTime; yield return null; }

            Assert.AreEqual(StandingAt, _go.transform.position, "restored to the wrong place");
            Assert.AreEqual(1, _ended, "only one beat ran, so only one end");
        }

        [UnityTest]
        public IEnumerator BeingDisabledMidBeat_StillPutsThePlayerBack()
        {
            // The region unloads, the object is pooled, the scene changes. None of those may leave the
            // fisher lying in the mattress.
            Turn(Mattress);
            yield return null;
            Assert.IsTrue(_sleep.IsSleeping);

            _go.SetActive(false);
            yield return null;

            Assert.IsFalse(_sleep.IsSleeping);
            Assert.AreEqual(StandingAt, _go.transform.position, "a cut-short beat must still restore");
            Assert.AreEqual(1, _ended);
            Assert.IsFalse(_lastCompleted, "a cut-short beat reports NOT completed");
        }

        [UnityTest]
        public IEnumerator WithNoSleepArt_NothingHappensAtAll_AndTheRestIsUnchanged()
        {
            // The all-or-nothing gate, from the outside: an artless def must leave the player standing
            // exactly where they were, which is #580's shipped behaviour.
            var bare = ScriptableObject.CreateInstance<CharacterVisualDef>();
            bare.Id = "visual.test_bare";
            _clips.Configure(bare);

            Turn(Mattress);
            yield return null;

            Assert.IsFalse(_sleep.IsSleeping, "no art must mean no beat");
            Assert.AreEqual(CharacterClip.None, _clips.Clip);
            Assert.AreEqual(StandingAt, _go.transform.position, "the player must not have been moved");
            Assert.AreEqual(0, _ended, "a beat that never started must not report an end");

            Object.DestroyImmediate(bare);
        }
    }
}
