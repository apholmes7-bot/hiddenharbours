using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The one interact verb's decision half</b> (M2-39) — the whole "which single thing does this press
    /// belong to?" rule, with no scene, no components, no input device and no frame loop.
    ///
    ///   • THE FILTERS — context, the candidate's own gate, its own reach, and the forward arc. Each one
    ///     alone can remove a candidate, and each is asserted alone.
    ///   • THE LADDER — priority first, then distance, then id. Pinned as a table, because this is the
    ///     rule every future feature will register against and it must not drift silently.
    ///   • DETERMINISM (rule 5) — the load-bearing claim, and the reason the ladder ends in an id: the
    ///     winner is the same for EVERY permutation of the candidate list, so which component happened to
    ///     enable first can never decide what a press does.
    ///   • THE STAND-DOWNS — the modal gate and the older-consumer claim, which are different things and
    ///     stop the verb for different reasons.
    ///   • THE HIGHLIGHT — published only when the answer CHANGES, because a per-frame publish is what
    ///     rule 7 rules out and a stale highlight is what the minimal-HUD canon cannot afford.
    ///
    /// <para>The registration/dispatch JOIN — that a real component in a running scene registers itself,
    /// and that a real key press reaches this rule through <c>ControlSwitcher</c> — is PlayMode's
    /// (<c>InteractVerbPlayTests</c>). Everything provable without a frame is proved here.</para>
    /// </summary>
    public class InteractVerbTests
    {
        /// <summary>A candidate as a plain object — the point of the seam being POCO-shaped is that this
        /// needs no GameObject.</summary>
        private sealed class Fake : IInteractable
        {
            public string Id { get; set; } = "fake";
            public Vector2 WorldPosition { get; set; }
            public float ReachMeters { get; set; } = 2f;
            public int Priority { get; set; } = InteractPriority.Fixture;
            public InteractContext Contexts { get; set; } = InteractContext.OnFoot;
            public bool RequiresFacing { get; set; }
            public bool IsAvailable { get; set; } = true;

            public int Calls;
            public InteractActor LastActor;
            public void Interact(in InteractActor actor) { Calls++; LastActor = actor; }
        }

        private static Fake At(string id, float x, float y, float reach = 2f,
                               int priority = InteractPriority.Fixture)
            => new Fake { Id = id, WorldPosition = new Vector2(x, y), ReachMeters = reach, Priority = priority };

        /// <summary>An on-foot actor at the origin, facing +X (screen right) unless told otherwise.</summary>
        private static InteractActor Actor(Vector2 pos = default, Vector2 facing = default,
                                           ControlMode mode = ControlMode.OnFoot)
            => InteractActor.For(pos, facing == default ? Vector2.right : facing, mode);

        private static bool Resolve(IReadOnlyList<IInteractable> list, InteractActor actor,
                                    out IInteractable best,
                                    float arc = InteractResolver.DefaultFacingArcDegrees)
            => InteractResolver.TryResolve(list, actor, arc, out best);

        private readonly List<InteractCandidateChanged> _candidateEvents = new();
        private readonly List<InteractPerformed> _performed = new();

        [SetUp]
        public void SetUp()
        {
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            _candidateEvents.Clear();
            _performed.Clear();
            EventBus.Clear<InteractCandidateChanged>();
            EventBus.Clear<InteractPerformed>();
            EventBus.Subscribe<InteractCandidateChanged>(_candidateEvents.Add);
            EventBus.Subscribe<InteractPerformed>(_performed.Add);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<InteractCandidateChanged>();
            EventBus.Clear<InteractPerformed>();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
        }

        // =====================================================================================
        //  THE FILTERS — four independent reasons a candidate is not a candidate
        // =====================================================================================

        [Test]
        public void NothingRegistered_ResolvesNothing_AndThatIsNotAFault()
        {
            // The property the whole migration rests on: with an empty registry the verb never fires, so
            // every INTERACT behaviour that predates the seam is reached exactly as it was.
            Assert.IsFalse(Resolve(null, Actor(), out IInteractable fromNull), "a null list is empty, not a crash");
            Assert.IsNull(fromNull);
            Assert.IsFalse(Resolve(new List<IInteractable>(), Actor(), out _), "an empty list resolves nothing");
            Assert.IsFalse(Resolve(new List<IInteractable> { null, null }, Actor(), out _),
                           "null entries are skipped, not dereferenced");
        }

        [Test]
        public void Reach_IsTheCandidatesOwn_AndInclusiveAtTheBoundary()
        {
            var near = At("a", 1.9f, 0f, reach: 2f);
            var far = At("b", 2.1f, 0f, reach: 2f);
            var list = new List<IInteractable> { near, far };

            Assert.IsTrue(Resolve(list, Actor(), out IInteractable best));
            Assert.AreSame(near, best, "only the one inside its own reach survives");

            // Inclusive, matching the rule StallGate/ControlSwitcher already use (distance <= radius) —
            // which is what lets a proximity interaction migrate onto the seam without moving its edge.
            var exact = At("c", 2f, 0f, reach: 2f);
            Assert.IsTrue(Resolve(new List<IInteractable> { exact }, Actor(), out _),
                          "a candidate exactly at its reach is in reach");
        }

        [Test]
        public void ANegativeReach_IsUnreachable_NeverUniversallyReachable()
        {
            // An un-tuned registrant must fail closed. The opposite mistake — treating a negative reach as
            // "no limit" — would make a mis-authored prefab steal every press in the region.
            var bad = At("a", 0.01f, 0f, reach: -5f);
            Assert.IsFalse(Resolve(new List<IInteractable> { bad }, Actor(), out _));
        }

        [Test]
        public void Context_MustMatchWhereTheActorIs()
        {
            var deckOnly = At("deck", 0.5f, 0f);
            deckOnly.Contexts = InteractContext.OnDeck;
            var list = new List<IInteractable> { deckOnly };

            Assert.IsFalse(Resolve(list, Actor(mode: ControlMode.OnFoot), out _),
                           "a deck-only candidate is not offered to someone standing on the wharf");
            Assert.IsTrue(Resolve(list, Actor(mode: ControlMode.OnDeck), out _),
                          "…and is offered to someone standing on the deck");

            var both = At("both", 0.5f, 0f);
            both.Contexts = InteractContext.OnFoot | InteractContext.OnDeck;
            Assert.IsTrue(Resolve(new List<IInteractable> { both }, Actor(mode: ControlMode.OnFoot), out _));
            Assert.IsTrue(Resolve(new List<IInteractable> { both }, Actor(mode: ControlMode.OnDeck), out _));

            var never = At("never", 0.5f, 0f);
            never.Contexts = InteractContext.None;
            Assert.IsFalse(Resolve(new List<IInteractable> { never }, Actor(), out _),
                           "None is an explicit off switch, not a wildcard");
        }

        [Test]
        public void ContextFor_MapsAboardToTheHelm_TheEnumsOneTrap()
        {
            // ControlMode.Aboard predates the deck state and kept its name for save compatibility — it is
            // the HELM. Getting this backwards would make every helm candidate resolve on deck and vice
            // versa, silently, so the mapping is pinned where it is written.
            Assert.AreEqual(InteractContext.OnFoot, InteractActor.ContextFor(ControlMode.OnFoot));
            Assert.AreEqual(InteractContext.OnDeck, InteractActor.ContextFor(ControlMode.OnDeck));
            Assert.AreEqual(InteractContext.AtHelm, InteractActor.ContextFor(ControlMode.Aboard));
        }

        [Test]
        public void AnUnavailableCandidate_IsNotOfferedAtAll()
        {
            var shut = At("a", 0.5f, 0f);
            shut.IsAvailable = false;
            Assert.IsFalse(Resolve(new List<IInteractable> { shut }, Actor(), out _));

            shut.IsAvailable = true;
            Assert.IsTrue(Resolve(new List<IInteractable> { shut }, Actor(), out _),
                          "the gate is read LIVE — a thing that becomes available this frame is offered this frame");
        }

        // =====================================================================================
        //  THE FORWARD ARC — and the three cases where it must NOT bite
        // =====================================================================================

        [Test]
        public void Facing_ExcludesWhatIsBehindYou_ButOnlyForCandidatesThatAskForIt()
        {
            var behind = At("behind", -1f, 0f);
            behind.RequiresFacing = true;
            var omni = At("omni", -1f, 0f);          // same spot, does not care about facing

            // Facing +X; both sit a metre astern.
            Assert.IsFalse(Resolve(new List<IInteractable> { behind }, Actor(), out _),
                           "a facing-aware candidate behind you is not your candidate");
            Assert.IsTrue(Resolve(new List<IInteractable> { omni }, Actor(), out _),
                          "…while a spot you operate by standing at it is reachable from any angle");

            // Turn round and the facing-aware one comes back.
            Assert.IsTrue(Resolve(new List<IInteractable> { behind }, Actor(facing: Vector2.left), out _));
        }

        [Test]
        public void Facing_Unknown_SkipsTheArcEntirely()
        {
            // Vector2.zero is the contract's "unknown" — what the switcher reports on deck, where the deck
            // walker carries no facing. It must fail OPEN: a guessed heading would make deck candidates
            // silently unreachable from whichever way the fisher last walked ashore.
            var behind = At("behind", -1f, 0f);
            behind.RequiresFacing = true;
            var unknownFacing = new InteractActor(Vector2.zero, Vector2.zero, InteractContext.OnFoot);
            Assert.IsTrue(Resolve(new List<IInteractable> { behind }, unknownFacing, out _));
        }

        [Test]
        public void StandingOnIt_IsAlwaysInArc_BecauseTheDirectionIsMeaningless()
        {
            var underfoot = At("underfoot", 0f, 0f);
            underfoot.RequiresFacing = true;
            Assert.IsTrue(Resolve(new List<IInteractable> { underfoot }, Actor(), out _),
                          "facing cannot exclude a thing you are standing on");
        }

        [Test]
        public void TheArcWidth_IsAFullAngle_AndIsClampedAtBothEnds()
        {
            var quartering = At("q", 1f, 1f);   // 45° off a +X facing
            quartering.RequiresFacing = true;
            var list = new List<IInteractable> { quartering };

            Assert.IsTrue(Resolve(list, Actor(), out _, arc: 180f), "180° is ±90° — the half-plane you face");
            Assert.IsFalse(Resolve(list, Actor(), out _, arc: 60f), "60° is ±30°, which 45° is outside");
            Assert.IsTrue(Resolve(list, Actor(), out _, arc: 360f), "a full circle cannot exclude anything");
            Assert.IsTrue(Resolve(list, Actor(), out _, arc: 1000f), "…and over-wide arcs clamp there rather than wrap");
            Assert.IsFalse(Resolve(list, Actor(), out _, arc: -10f), "a negative arc clamps to dead-ahead-only, not to open");
        }

        [Test]
        public void DefaultArc_IsGenerousEnoughForFourWayFacing()
        {
            // The fisher is drawn in four facings, so a candidate on the exact diagonal sits 45° off
            // whichever of the two adjacent facings she is in. At the default 180° both reach it — there is
            // no dead wedge between two facings, which is the whole reason the default is that wide.
            var diagonal = At("d", 1f, 1f);
            diagonal.RequiresFacing = true;
            var list = new List<IInteractable> { diagonal };
            Assert.IsTrue(Resolve(list, Actor(facing: Vector2.right), out _));
            Assert.IsTrue(Resolve(list, Actor(facing: Vector2.up), out _));
        }

        [Test]
        public void FacingUnitVector_MatchesTheEnumsScreenAxes()
        {
            // The bridge between the Player lane's four-way facing and Core's unit vector. Down is −Y: get
            // this inverted and every fixture becomes reachable only from the wrong side.
            Assert.AreEqual(Vector2.down, PlayerWalkController.FacingUnitVector(Facing.Down));
            Assert.AreEqual(Vector2.up, PlayerWalkController.FacingUnitVector(Facing.Up));
            Assert.AreEqual(Vector2.left, PlayerWalkController.FacingUnitVector(Facing.Left));
            Assert.AreEqual(Vector2.right, PlayerWalkController.FacingUnitVector(Facing.Right));
        }

        // =====================================================================================
        //  THE LADDER — priority, then distance, then id. The table, as a table.
        // =====================================================================================

        [Test]
        public void Priority_BeatsDistance_Outright()
        {
            var farButHeld = At("far", 1.9f, 0f, priority: InteractPriority.Held);
            var nearFixture = At("near", 0.1f, 0f, priority: InteractPriority.Fixture);

            Assert.IsTrue(Resolve(new List<IInteractable> { nearFixture, farButHeld }, Actor(), out IInteractable best));
            Assert.AreSame(farButHeld, best,
                           "what is in your hands is the most specific answer to 'act', however near the wharf fitting is");
            Assert.Less(InteractPriority.Fixture, InteractPriority.Portable, "the ladder's rungs are ordered…");
            Assert.Less(InteractPriority.Portable, InteractPriority.Held, "…all the way up");
        }

        [Test]
        public void AtEqualPriority_TheNearerWins()
        {
            var near = At("near", 0.5f, 0f);
            var far = At("far", 1.5f, 0f);
            Assert.IsTrue(Resolve(new List<IInteractable> { far, near }, Actor(), out IInteractable best));
            Assert.AreSame(near, best);
        }

        [Test]
        public void AnExactTie_IsBrokenByIdAndNeverByRegistrationOrder()
        {
            // Perfectly symmetric: same priority, mirrored either side of the actor, so the distances are
            // bit-equal. Something has to decide, and it must not be "whoever enabled first".
            var west = At("aaa", -1f, 0f);
            var east = At("zzz", 1f, 0f);

            Assert.IsTrue(Resolve(new List<IInteractable> { west, east }, Actor(), out IInteractable a));
            Assert.IsTrue(Resolve(new List<IInteractable> { east, west }, Actor(), out IInteractable b));
            Assert.AreSame(a, b, "the same winner whichever way round the list is");
            Assert.AreSame(west, a, "and it is the lower id by ordinal comparison");
        }

        [Test]
        public void TheWinnerIsTheSameForEVERYPermutation_TheDeterminismClaim()
        {
            // The load-bearing test of the whole seam (rule 5). The three-rung ladder is a TOTAL order over
            // candidates with distinct ids, so a single-pass scan for the maximum is order-independent —
            // this is the assertion that says so, over every ordering of a deliberately nasty set: two at
            // equal priority and equal distance, one nearer but lower priority, one out of reach.
            var set = new List<IInteractable>
            {
                At("m_north", 0f, 1f, reach: 3f),
                At("m_south", 0f, -1f, reach: 3f),                                  // ties m_north exactly
                At("a_close", 0.25f, 0f, reach: 3f),                                // nearest, lowest rung
                At("z_high", 2.5f, 0f, reach: 3f, priority: InteractPriority.Held),  // outranks the lot
                At("x_outofreach", 9f, 0f, reach: 1f),
            };

            var actor = new InteractActor(Vector2.zero, Vector2.zero, InteractContext.OnFoot);
            IInteractable expected = null;
            int permutations = 0;

            foreach (var order in Permutations(set))
            {
                permutations++;
                Assert.IsTrue(InteractResolver.TryResolve(order, actor,
                                                          InteractResolver.DefaultFacingArcDegrees,
                                                          out IInteractable best));
                expected ??= best;
                Assert.AreSame(expected, best,
                               "the resolved candidate must not depend on the order of the registry");
            }

            Assert.AreEqual(120, permutations, "all 5! orderings were tried");
            Assert.AreEqual("z_high", expected.Id, "and priority is what actually won");
        }

        [Test]
        public void DuplicateIds_KeepTheIncumbent_TheOneStatedHole()
        {
            // Documented in IInteractable.Id as an authoring error rather than a design allowance. Pinned
            // so that if it is ever fixed properly, this test is what says the behaviour moved.
            var first = At("same", -1f, 0f);
            var second = At("same", 1f, 0f);
            var actor = new InteractActor(Vector2.zero, Vector2.zero, InteractContext.OnFoot);

            Assert.IsTrue(InteractResolver.TryResolve(new List<IInteractable> { first, second }, actor,
                                                      360f, out IInteractable best));
            Assert.AreSame(first, best, "with equal ids the earlier registrant is kept — order decides, as documented");
        }

        [Test]
        public void Beats_IsTheLadder_AndIsExposedForExactlyThisAssertion()
        {
            var a = At("a", 0f, 0f);
            var b = At("b", 0f, 0f);
            Assert.IsTrue(InteractResolver.Beats(a, 10, 4f, b, 0, 0.1f), "priority first");
            Assert.IsFalse(InteractResolver.Beats(a, 0, 0.1f, b, 10, 4f), "…both ways round");
            Assert.IsTrue(InteractResolver.Beats(a, 0, 0.1f, b, 0, 4f), "then distance");
            Assert.IsFalse(InteractResolver.Beats(b, 0, 4f, a, 0, 0.1f), "…both ways round");
            Assert.IsTrue(InteractResolver.Beats(a, 0, 1f, b, 0, 1f), "then id, ascending ordinal");
            Assert.IsFalse(InteractResolver.Beats(b, 0, 1f, a, 0, 1f), "…both ways round");
        }

        // =====================================================================================
        //  THE REGISTRY
        // =====================================================================================

        [Test]
        public void Registration_IsIdempotent_AndUnregisteringSomethingUnknownIsSafe()
        {
            var one = At("a", 0f, 0f);
            Interactables.Register(one);
            Interactables.Register(one);
            Assert.AreEqual(1, Interactables.Count,
                            "a component enabled twice without a disable between must not be weighed twice");

            Interactables.Register(null);
            Assert.AreEqual(1, Interactables.Count, "null is a no-op, not an entry");

            Interactables.Unregister(At("never-registered", 0f, 0f));
            Interactables.Unregister(null);
            Assert.AreEqual(1, Interactables.Count, "a teardown never has to check first");

            Interactables.Unregister(one);
            Assert.AreEqual(0, Interactables.Count);
        }

        // =====================================================================================
        //  THE VERB — dispatch, the two stand-downs, and the highlight's change-detection
        // =====================================================================================

        [Test]
        public void TryPerform_ActsOnExactlyOneCandidate_AndSaysWhich()
        {
            var winner = At("near", 0.2f, 0f);
            var loser = At("far", 1.5f, 0f);
            Interactables.Register(loser);
            Interactables.Register(winner);

            Assert.IsTrue(InteractVerb.TryPerform(Actor(), InteractResolver.DefaultFacingArcDegrees));
            Assert.AreEqual(1, winner.Calls, "exactly one candidate acted");
            Assert.AreEqual(0, loser.Calls, "and exactly one — 'the nearest' is not 'everything in reach'");
            Assert.AreEqual(1, _performed.Count);
            Assert.AreEqual("near", _performed[0].Id, "…and it said which, so audio/QA need not guess");
            Assert.AreEqual(InteractContext.OnFoot, winner.LastActor.Context,
                            "the candidate is told who reached it");
        }

        [Test]
        public void TryPerform_SpendsNothing_WhenNothingResolves()
        {
            Interactables.Register(At("miles-away", 50f, 0f));
            Assert.IsFalse(InteractVerb.TryPerform(Actor(), InteractResolver.DefaultFacingArcDegrees),
                           "false means 'the press was not spent' — the caller's own behaviours must still run");
            Assert.IsEmpty(_performed);
        }

        [Test]
        public void TheModalGate_AndTheOlderConsumersClaim_EachStandTheVerbDown()
        {
            var candidate = At("near", 0.2f, 0f);
            Interactables.Register(candidate);

            InteractionGate.IsBlocked = true;
            Assert.IsFalse(InteractVerb.TryPerform(Actor(), 360f), "a modal dialogue owns the key outright");
            InteractionGate.Reset();

            InteractActionClaim.IsClaimed = true;
            Assert.IsFalse(InteractVerb.TryPerform(Actor(), 360f),
                           "…and a press already spoken for by an older direct reader is not spent twice");
            InteractActionClaim.Reset();

            Assert.AreEqual(0, candidate.Calls);
            Assert.IsTrue(InteractVerb.TryPerform(Actor(), 360f), "with both clear, the verb fires");
            Assert.AreEqual(1, candidate.Calls);
        }

        [Test]
        public void TheHighlight_IsPublishedOnCHANGE_NeverPerFrame()
        {
            var spot = At("spot", 0.5f, 0f, reach: 1f);
            Interactables.Register(spot);
            var arc = InteractResolver.DefaultFacingArcDegrees;

            // Walk in.
            InteractVerb.PublishCandidate(Actor(), arc);
            Assert.AreEqual(1, _candidateEvents.Count);
            Assert.AreEqual("spot", _candidateEvents[0].Id);
            Assert.IsTrue(_candidateEvents[0].Has);
            Assert.AreEqual("spot", InteractVerb.CurrentCandidateId, "…and a late subscriber can read it");

            // Stand still for a few frames: the answer has not changed, so nothing is published.
            for (int i = 0; i < 5; i++) InteractVerb.PublishCandidate(Actor(), arc);
            Assert.AreEqual(1, _candidateEvents.Count, "standing still costs one comparison a frame, not an event");

            // Walk out.
            InteractVerb.PublishCandidate(Actor(new Vector2(10f, 0f)), arc);
            Assert.AreEqual(2, _candidateEvents.Count);
            Assert.IsNull(_candidateEvents[1].Id, "no candidate — the signal to drop the outline");
            Assert.IsFalse(_candidateEvents[1].Has);

            // …and staying out is likewise silent.
            InteractVerb.PublishCandidate(Actor(new Vector2(10f, 0f)), arc);
            Assert.AreEqual(2, _candidateEvents.Count);
        }

        [Test]
        public void TheHighlight_GoesOutWhenTheVerbCannotFire()
        {
            // A highlight that outlives its own actionability teaches the player a lie — the one thing a
            // diegetic affordance with no text label cannot afford.
            var spot = At("spot", 0.5f, 0f);
            Interactables.Register(spot);
            var arc = InteractResolver.DefaultFacingArcDegrees;

            InteractVerb.PublishCandidate(Actor(), arc);
            Assert.AreEqual("spot", InteractVerb.CurrentCandidateId);

            InteractionGate.IsBlocked = true;
            InteractVerb.PublishCandidate(Actor(), arc);
            Assert.IsNull(InteractVerb.CurrentCandidateId, "under a modal, nothing is highlighted");
            InteractionGate.Reset();

            InteractActionClaim.IsClaimed = true;
            InteractVerb.PublishCandidate(Actor(), arc);
            Assert.IsNull(InteractVerb.CurrentCandidateId, "and likewise while an older consumer holds the press");
            InteractActionClaim.Reset();

            InteractVerb.PublishCandidate(Actor(), arc);
            Assert.AreEqual("spot", InteractVerb.CurrentCandidateId, "…and it comes back when the press does");

            InteractVerb.ClearCandidate();
            Assert.IsNull(InteractVerb.CurrentCandidateId, "the driver's explicit drop (pause, boarding move, teardown)");
        }

        // ---- helpers ----------------------------------------------------------------------------

        /// <summary>Every ordering of a list, so "order-independent" can be asserted rather than assumed.</summary>
        private static IEnumerable<List<IInteractable>> Permutations(List<IInteractable> source)
        {
            if (source.Count <= 1) { yield return new List<IInteractable>(source); yield break; }
            for (int i = 0; i < source.Count; i++)
            {
                var rest = new List<IInteractable>(source);
                IInteractable head = rest[i];
                rest.RemoveAt(i);
                foreach (var tail in Permutations(rest))
                {
                    tail.Insert(0, head);
                    yield return tail;
                }
            }
        }
    }
}
