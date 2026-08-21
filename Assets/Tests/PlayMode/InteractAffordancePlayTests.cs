using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The interact affordance, in a running scene — <b>the right renderer lights, and only while the
    /// offer names it</b>.
    ///
    /// <para><b>What the EditMode rules cannot cover.</b> Those pin the DECISION (armed / disarmed) as
    /// arithmetic. They cannot see the half that actually fails in practice: resolving an offer id to a
    /// renderer through the Core registry, and letting go of it again. A presenter that armed correctly
    /// and then lit the wrong bed would pass every pure test in the suite.</para>
    ///
    /// <para><b>Frames, not seconds</b> — the presenter does its work in <c>LateUpdate</c>, so every
    /// assertion here is made after <c>yield return null</c> has let one run. Nothing in this suite waits
    /// on wall-clock time (memory: frames are not time).</para>
    /// </summary>
    public class InteractAffordancePlayTests
    {
        readonly List<GameObject> _spawned = new();
        InteractAffordancePresenter _presenter;
        InteractAffordanceVariant _variantBefore;

        static readonly Vector2 BedPos = new(3f, 1f);
        static readonly Vector2 StairPos = new(-4f, 2f);

        /// <summary>A fixture that is a real Component, which is what the Core lookup requires — the
        /// plain-object registrants the resolver's own tests use have no transform to draw on.</summary>
        sealed class TestFixture : MonoBehaviour, IInteractable
        {
            public string FixtureId = "fixture.bed";
            public string Id => FixtureId;
            public string VerbLabel => "Sleep";
            public Vector2 WorldPosition => transform.position;
            public float ReachMeters => 2f;
            public int Priority => InteractPriority.Fixture;
            public InteractContext Contexts => InteractContext.OnFoot;
            public bool RequiresFacing => false;
            public bool IsAvailable => true;
            public void Interact(in InteractActor actor) { }
        }

        [SetUp]
        public void SetUp()
        {
            // The presenter logs loudly if the shader is missing. That is the right behaviour in the game
            // and the wrong way to fail a test — the assertions below say what actually broke.
            LogAssert.ignoreFailingMessages = true;

            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            InteractOffer.Reset();
            InteractActorProbe.Reset();

            _variantBefore = InteractAffordancePresenter.Variant;

            var go = new GameObject("InteractAffordance_Test");
            _spawned.Add(go);
            _presenter = go.AddComponent<InteractAffordancePresenter>();
        }

        [TearDown]
        public void TearDown()
        {
            InteractAffordancePresenter.Variant = _variantBefore;

            Interactables.Clear();
            InteractOffer.Reset();
            InteractionGate.Reset();
            InteractActorProbe.Reset();

            foreach (GameObject go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            _presenter = null;

            LogAssert.ignoreFailingMessages = false;
        }

        SpriteRenderer SpawnFixture(string id, Vector2 at)
        {
            var go = new GameObject(id);
            _spawned.Add(go);
            go.transform.position = at;

            var sr = go.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, mipChain: false);
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0f), 32f);

            var fixture = go.AddComponent<TestFixture>();
            fixture.FixtureId = id;
            Interactables.Register(fixture);

            return sr;
        }

        static void OfferFor(string id, Vector2 at) =>
            InteractOffer.Set(InteractOfferSource.Fixture, id, "Sleep", at, 1f);

        [UnityTest]
        public IEnumerator FacingAFixture_LightsThatFixturesRenderer()
        {
            SpriteRenderer bed = SpawnFixture("fixture.bed", BedPos);

            OfferFor("fixture.bed", BedPos);
            yield return null;

            Assert.IsTrue(_presenter.IsShowing,
                "An offer is standing and nothing is suppressing it, so something must be lit — this is " +
                "the whole feature.");
            Assert.AreSame(bed, _presenter.Target,
                "The affordance must land on the renderer belonging to the id the offer named.");
        }

        [UnityTest]
        public IEnumerator TurningAway_DropsTheHighlight()
        {
            SpawnFixture("fixture.bed", BedPos);

            OfferFor("fixture.bed", BedPos);
            yield return null;
            Assert.IsTrue(_presenter.IsShowing, "precondition: it lit");

            // What "turning away" is on this channel: the feeder states that it has nothing.
            InteractOffer.Clear(InteractOfferSource.Fixture);
            yield return null;

            Assert.IsFalse(_presenter.IsShowing,
                "A highlight that outlives its offer is pointing at something the key no longer works.");
            Assert.IsNull(_presenter.Target);
        }

        [UnityTest]
        public IEnumerator WithTwoFixturesStanding_OnlyTheOfferedOneLights()
        {
            SpriteRenderer bed = SpawnFixture("fixture.bed", BedPos);
            SpriteRenderer stair = SpawnFixture("fixture.stair", StairPos);

            OfferFor("fixture.stair", StairPos);
            yield return null;

            Assert.AreSame(stair, _presenter.Target,
                "Two registrants are live and the offer names one of them. Lighting the other — or both — " +
                "is the failure a single-fixture test cannot see.");
            Assert.AreNotSame(bed, _presenter.Target);
        }

        [UnityTest]
        public IEnumerator MovingBetweenFixtures_FollowsTheOffer()
        {
            SpriteRenderer bed = SpawnFixture("fixture.bed", BedPos);
            SpriteRenderer stair = SpawnFixture("fixture.stair", StairPos);

            OfferFor("fixture.bed", BedPos);
            yield return null;
            Assert.AreSame(bed, _presenter.Target, "precondition: the bed lit");

            OfferFor("fixture.stair", StairPos);
            yield return null;

            Assert.AreSame(stair, _presenter.Target,
                "The overlay must let go of the old fixture and take the new one — staying stuck on the " +
                "first thing faced is the bug this asserts against.");
        }

        [UnityTest]
        public IEnumerator AModalDropsTheHighlight_AndReleasingItBringsItBack()
        {
            SpawnFixture("fixture.bed", BedPos);

            OfferFor("fixture.bed", BedPos);
            yield return null;
            Assert.IsTrue(_presenter.IsShowing, "precondition: it lit");

            // A dialogue bubble or a picker owns the key. The feeders do not go quiet, so this is the
            // suppression doing the work — exactly as it does for the popup.
            InteractionGate.IsBlocked = true;
            yield return null;

            Assert.IsFalse(_presenter.IsShowing,
                "Under a modal the press cannot happen, so nothing may advertise it.");

            InteractionGate.IsBlocked = false;
            yield return null;

            Assert.IsTrue(_presenter.IsShowing,
                "The offer never went away, so closing the modal must bring the highlight back without " +
                "the player having to step away and turn around.");
        }

        [UnityTest]
        public IEnumerator TheHighlightAndThePopupNeverDisagree()
        {
            SpawnFixture("fixture.bed", BedPos);
            OfferFor("fixture.bed", BedPos);
            yield return null;

            // Both surfaces read InteractOfferVisibility. Walking the gate through both states pins that
            // they move together rather than merely both being right once.
            for (int i = 0; i < 2; i++)
            {
                InteractionGate.IsBlocked = i == 0;
                yield return null;

                Assert.AreEqual(InteractOfferVisibility.ShouldPresentCurrent(), _presenter.IsShowing,
                    $"gate blocked = {InteractionGate.IsBlocked}: the lit fixture and the named fixture " +
                    "are one statement to the player and must rise and fall together.");
            }
        }

        [UnityTest]
        public IEnumerator AnOfferNoFixtureOwns_LightsNothing_AndDoesNotThrow()
        {
            SpawnFixture("fixture.bed", BedPos);

            // A transit offer: keyed by ACTION, not by registrant. Nothing in the fixture registry answers
            // to it, and that is the honest, documented gap in this lane — it must be quiet, not a crash
            // and not a wrong guess at the nearest thing.
            InteractOffer.Set(InteractOfferSource.Transit, "board", "Climb aboard", BedPos, 1f);
            yield return null;

            Assert.IsFalse(_presenter.IsShowing);
            Assert.IsNull(_presenter.Target,
                "Guessing a renderer for an id nobody registered would light the wrong object, which is " +
                "worse than lighting none.");
        }

        [UnityTest]
        public IEnumerator ADestroyedFixture_DropsTheHighlightRatherThanHoldingADeadRenderer()
        {
            SpriteRenderer bed = SpawnFixture("fixture.bed", BedPos);

            OfferFor("fixture.bed", BedPos);
            yield return null;
            Assert.IsTrue(_presenter.IsShowing, "precondition: it lit");

            // Take the GameObject FIRST: reading `bed.gameObject` after the destroy throws
            // MissingReferenceException, which would fail this test for the wrong reason entirely.
            GameObject go = bed.gameObject;
            _spawned.Remove(go);
            Object.DestroyImmediate(go);
            yield return null;

            Assert.IsFalse(_presenter.IsShowing,
                "A region can unload under a standing offer. Holding a destroyed renderer would throw on " +
                "the next pose sync (memory: fake-null is not null).");
        }

        [UnityTest]
        public IEnumerator EveryVariantDrawsSomething()
        {
            SpawnFixture("fixture.bed", BedPos);
            OfferFor("fixture.bed", BedPos);

            foreach (InteractAffordanceVariant variant in new[]
                     {
                         InteractAffordanceVariant.ShadeLift,
                         InteractAffordanceVariant.OutlinePulse,
                         InteractAffordanceVariant.Both,
                     })
            {
                InteractAffordancePresenter.Variant = variant;
                yield return null;

                Assert.IsTrue(_presenter.IsShowing,
                    $"{variant} must draw. A variant that silently renders nothing would look to the " +
                    "owner like a considered option, and they would rule on an empty screen.");
            }
        }
    }
}
