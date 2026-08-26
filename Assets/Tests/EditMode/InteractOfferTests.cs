using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The offer register</b> — the Core seam the one interaction popup rides, and the second
    /// subscriber the affordance lane will take. Three feeders, one arbitrated answer, published on
    /// CHANGE only.
    ///
    /// <para>Pure statics and values throughout: no components stood up, no lifecycle assumed (EditMode
    /// fires none). What is proved here is the whole contract — the ladder, the edge-triggering, and the
    /// two ways a feeder can say "nothing".</para>
    /// </summary>
    public class InteractOfferTests
    {
        private readonly List<InteractOfferChanged> _published = new();

        [SetUp]
        public void SetUp()
        {
            InteractOffer.Reset();
            _published.Clear();
            EventBus.Clear<InteractOfferChanged>();
            EventBus.Subscribe<InteractOfferChanged>(_published.Add);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<InteractOfferChanged>();
            InteractOffer.Reset();
        }

        private static void Set(InteractOfferSource src, string id, string label,
                                float x = 0f, float y = 0f, float distSq = 1f)
            => InteractOffer.Set(src, id, label, new Vector2(x, y), distSq);

        // =====================================================================================
        //  NOTHING, AND SAYING SO
        // =====================================================================================

        [Test]
        public void WithNoFeeder_NothingIsOffered_AndThatIsNotAFault()
        {
            Assert.IsFalse(InteractOffer.Current.Has);
            Assert.AreEqual(InteractOfferSource.None, InteractOffer.Current.Source);
            Assert.IsEmpty(_published, "a register nobody has written to publishes nothing");
        }

        [Test]
        public void AnEmptyLabelIsTheSameAsClearing()
        {
            // A feeder must be able to say "nothing" the same way it says everything else. Both forms are
            // supported because the call sites read better one way in a switch and the other in a guard.
            Set(InteractOfferSource.Transit, "transit.board", "Climb aboard");
            Assert.IsTrue(InteractOffer.Current.Has);

            Set(InteractOfferSource.Transit, "transit.board", null);
            Assert.IsFalse(InteractOffer.Current.Has);

            Set(InteractOfferSource.Transit, "transit.board", "Climb aboard");
            Set(InteractOfferSource.Transit, "transit.board", "");
            Assert.IsFalse(InteractOffer.Current.Has);
        }

        [Test]
        public void ClearingASlotThatIsAlreadyEmpty_PublishesNothing()
        {
            // The feeders call Clear EVERY frame they have no candidate. If that republished, standing in
            // an empty field would be a per-frame event storm — the exact rule-7 failure the edge trigger
            // exists to prevent.
            InteractOffer.Clear(InteractOfferSource.Fixture);
            InteractOffer.Clear(InteractOfferSource.Fixture);
            InteractOffer.Clear(InteractOfferSource.Conversation);
            Assert.IsEmpty(_published);
        }

        // =====================================================================================
        //  EDGE-TRIGGERED PUBLISHING
        // =====================================================================================

        [Test]
        public void RestatingTheSameOffer_PublishesOnce()
        {
            for (int i = 0; i < 60; i++)
                Set(InteractOfferSource.Conversation, "npc.ginny", "Talk — Aunt Ginny", 4f, 2f, 1.44f);

            Assert.AreEqual(1, _published.Count, "sixty frames of standing still is one event");
            Assert.AreEqual("Talk — Aunt Ginny", _published[0].Label);
        }

        [Test]
        public void AMovingCandidate_Republishes_BecauseTheAffordanceLaneDrawsAtIt()
        {
            // The one place this signal deliberately differs from InteractCandidateChanged, whose remarks
            // refuse a position for exactly the reason that does not apply here.
            Set(InteractOfferSource.Fixture, "fixture.pail", "Pick up the pail", 1f, 0f);
            Set(InteractOfferSource.Fixture, "fixture.pail", "Pick up the pail", 1.2f, 0f);
            Assert.AreEqual(2, _published.Count);
            Assert.AreEqual(new Vector2(1.2f, 0f), _published[1].WorldPosition);
        }

        [Test]
        public void ChangingOnlyTheLabel_Republishes()
        {
            // A carriable's label flips between "Pick up" and "Set down" without the id or the position
            // changing at all. If the compare skipped the label the popup would keep the wrong verb.
            Set(InteractOfferSource.Fixture, "fixture.shovel", "Pick up the shovel", 2f, 0f);
            Set(InteractOfferSource.Fixture, "fixture.shovel", "Set the shovel down", 2f, 0f);
            Assert.AreEqual(2, _published.Count);
            Assert.AreEqual("Set the shovel down", InteractOffer.Current.Label);
        }

        [Test]
        public void GoingEmpty_PublishesTheClearingChange()
        {
            Set(InteractOfferSource.Transit, "transit.board", "Climb aboard");
            InteractOffer.Clear(InteractOfferSource.Transit);

            Assert.AreEqual(2, _published.Count);
            Assert.IsFalse(_published[1].Has, "the popup is told to hide, not merely left alone");
        }

        [Test]
        public void Reset_ClearsEverySlot_AndPublishesTheClearing()
        {
            Set(InteractOfferSource.Fixture, "a", "A");
            Set(InteractOfferSource.Transit, "b", "B");
            _published.Clear();

            InteractOffer.Reset();
            Assert.IsFalse(InteractOffer.Current.Has);
            Assert.AreEqual(1, _published.Count);
            Assert.IsFalse(_published[0].Has);
        }

        [Test]
        public void ResetOnAnAlreadyEmptyRegister_PublishesNothing()
        {
            InteractOffer.Reset();
            Assert.IsEmpty(_published, "test teardown must not be an event source");
        }

        // =====================================================================================
        //  THE LADDER — source rank, then nearness, then ordinal id
        // =====================================================================================

        [Test]
        public void TransitOutranksConversationOutranksFixture_WhateverTheDistances()
        {
            // These ranks are ControlSwitcher.BeginInteract's own dispatch order written down: boarding is
            // answered before anything, and the registry is consulted after it. The popup must not
            // advertise a press the game will not perform — which is also why the ONE transit that no
            // longer wins outright (stepping ashore, which yields to a resolving fixture since
            // 2026-08-25) stands its own offer down there rather than being demoted here. The rank means
            // what it says: an offer that IS made is the press.
            Set(InteractOfferSource.Fixture, "f", "Fixture", distSq: 0.01f);
            Set(InteractOfferSource.Conversation, "c", "Conversation", distSq: 100f);
            Assert.AreEqual("Conversation", InteractOffer.Current.Label);

            Set(InteractOfferSource.Transit, "t", "Transit", distSq: 400f);
            Assert.AreEqual("Transit", InteractOffer.Current.Label,
                            "a far transit still beats a conversation at your elbow");
        }

        [Test]
        public void TheLosingSlotIsStillHeld_AndWinsBackWhenTheWinnerClears()
        {
            Set(InteractOfferSource.Fixture, "f", "Fixture");
            Set(InteractOfferSource.Transit, "t", "Transit");
            Assert.AreEqual("Transit", InteractOffer.Current.Label);

            InteractOffer.Clear(InteractOfferSource.Transit);
            Assert.AreEqual("Fixture", InteractOffer.Current.Label,
                            "stepping away from the rail hands the popup back to what is at your feet");
        }

        [Test]
        public void WithinOneRank_TheNearerWins()
        {
            // Two sources cannot today hold the same rank — there is one slot each — so this is asserted
            // through the exposed ladder, which is what a fourth feeder would be ranked by.
            var near = new InteractOfferChanged(InteractOfferSource.Fixture, "a", "A", Vector2.zero, 1f);
            var far  = new InteractOfferChanged(InteractOfferSource.Fixture, "b", "B", Vector2.zero, 9f);
            Assert.IsTrue(InteractOffer.Beats(near, far));
            Assert.IsFalse(InteractOffer.Beats(far, near));
        }

        [Test]
        public void AtEqualRankAndDistance_TheLowerIdWins_Ordinally()
        {
            var a = new InteractOfferChanged(InteractOfferSource.Fixture, "aaa", "A", Vector2.zero, 4f);
            var b = new InteractOfferChanged(InteractOfferSource.Fixture, "bbb", "B", Vector2.zero, 4f);
            Assert.IsTrue(InteractOffer.Beats(a, b));
            Assert.IsFalse(InteractOffer.Beats(b, a), "a total order, so it cannot be true both ways");
        }

        [Test]
        public void TheWinnerDoesNotDependOnTheOrderTheFeedersRan()
        {
            // The whole point of an exact, total ladder: three Updates in undefined order must not be able
            // to change which line the player reads.
            Set(InteractOfferSource.Transit, "t", "Transit");
            Set(InteractOfferSource.Conversation, "c", "Conversation");
            Set(InteractOfferSource.Fixture, "f", "Fixture");
            string forwards = InteractOffer.Current.Label;

            InteractOffer.Reset();
            Set(InteractOfferSource.Fixture, "f", "Fixture");
            Set(InteractOfferSource.Conversation, "c", "Conversation");
            Set(InteractOfferSource.Transit, "t", "Transit");
            Assert.AreEqual(forwards, InteractOffer.Current.Label);
        }

        // =====================================================================================
        //  THE VALUE ITSELF
        // =====================================================================================

        [Test]
        public void SameAs_ComparesEveryField()
        {
            var v = new InteractOfferChanged(InteractOfferSource.Fixture, "id", "Label", new Vector2(1f, 2f), 3f);
            Assert.IsTrue(v.SameAs(v));
            Assert.IsFalse(v.SameAs(new InteractOfferChanged(InteractOfferSource.Transit, "id", "Label", new Vector2(1f, 2f), 3f)));
            Assert.IsFalse(v.SameAs(new InteractOfferChanged(InteractOfferSource.Fixture, "other", "Label", new Vector2(1f, 2f), 3f)));
            Assert.IsFalse(v.SameAs(new InteractOfferChanged(InteractOfferSource.Fixture, "id", "Other", new Vector2(1f, 2f), 3f)));
            Assert.IsFalse(v.SameAs(new InteractOfferChanged(InteractOfferSource.Fixture, "id", "Label", new Vector2(9f, 2f), 3f)));
            Assert.IsFalse(v.SameAs(new InteractOfferChanged(InteractOfferSource.Fixture, "id", "Label", new Vector2(1f, 2f), 9f)));
        }

        [Test]
        public void NoneIsNotOffering()
        {
            Assert.IsFalse(InteractOfferChanged.None.Has);
            Assert.AreEqual(InteractOfferSource.None, InteractOfferChanged.None.Source);
        }

        [Test]
        public void AnOfferWithNoId_IsStillAnOffer()
        {
            // The LABEL is what the popup needs; the id is for a listener that has something to match. A
            // legacy cove interactable with neither NpcDef nor conversation id must still be nameable.
            Set(InteractOfferSource.Conversation, null, "Read — Ned's Letter");
            Assert.IsTrue(InteractOffer.Current.Has);
            Assert.IsNull(InteractOffer.Current.Id);
        }

        // =====================================================================================
        //  THE VERB'S HALF — it feeds the Fixture slot from the SAME resolution it highlights
        // =====================================================================================

        private sealed class Fake : IInteractable
        {
            public string Id { get; set; } = "fixture.bed";
            public Vector2 WorldPosition { get; set; }
            public float ReachMeters { get; set; } = 3f;
            public int Priority { get; set; } = InteractPriority.Fixture;
            public InteractContext Contexts { get; set; } = InteractContext.OnFoot;
            public bool RequiresFacing { get; set; }
            public bool IsAvailable { get; set; } = true;
            public string VerbLabel { get; set; } = "Sleep";
            public void Interact(in InteractActor actor) { }
        }

        [Test]
        public void PublishCandidate_OffersTheResolvedCandidatesOwnWords()
        {
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            InteractOffer.Reset();
            InteractActorProbe.Reset();
            try
            {
                var bed = new Fake { WorldPosition = new Vector2(1f, 0f) };
                Interactables.Register(bed);

                InteractVerb.PublishCandidate(
                    InteractActor.For(Vector2.zero, Vector2.right, ControlMode.OnFoot),
                    InteractResolver.DefaultFacingArcDegrees);

                Assert.AreEqual(InteractOfferSource.Fixture, InteractOffer.Current.Source);
                Assert.AreEqual("Sleep", InteractOffer.Current.Label);
                Assert.AreEqual("fixture.bed", InteractOffer.Current.Id);
                Assert.AreEqual(new Vector2(1f, 0f), InteractOffer.Current.WorldPosition);
                Assert.AreEqual(1f, InteractOffer.Current.DistanceSqr, 1e-4f);
            }
            finally
            {
                Interactables.Clear();
                InteractVerb.Reset();
            }
        }

        [Test]
        public void ACandidateWithNoWords_OffersNothing_ButStillResolves()
        {
            // A registrant that has not been given a label is not advertised. It is still a candidate and
            // the press still reaches it — this governs only whether the player is TOLD.
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            InteractOffer.Reset();
            InteractActorProbe.Reset();
            try
            {
                var mute = new Fake { Id = "fixture.mute", VerbLabel = null, WorldPosition = new Vector2(1f, 0f) };
                Interactables.Register(mute);

                var actor = InteractActor.For(Vector2.zero, Vector2.right, ControlMode.OnFoot);
                InteractVerb.PublishCandidate(actor, InteractResolver.DefaultFacingArcDegrees);

                Assert.IsFalse(InteractOffer.Current.Has, "nothing to say, so nothing is said");
                Assert.AreEqual("fixture.mute", InteractVerb.CurrentCandidateId, "but it IS the candidate");
            }
            finally
            {
                Interactables.Clear();
                InteractVerb.Reset();
            }
        }

        [Test]
        public void AModalGateStandsTheOfferDown_JustAsItStandsTheHighlightDown()
        {
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            InteractOffer.Reset();
            InteractActorProbe.Reset();
            try
            {
                Interactables.Register(new Fake { WorldPosition = new Vector2(1f, 0f) });
                var actor = InteractActor.For(Vector2.zero, Vector2.right, ControlMode.OnFoot);

                InteractVerb.PublishCandidate(actor, InteractResolver.DefaultFacingArcDegrees);
                Assert.IsTrue(InteractOffer.Current.Has);

                InteractionGate.IsBlocked = true;
                InteractVerb.PublishCandidate(actor, InteractResolver.DefaultFacingArcDegrees);
                Assert.IsFalse(InteractOffer.Current.Has,
                               "an offer that outlives its own actionability teaches the player a lie");
            }
            finally
            {
                InteractionGate.Reset();
                Interactables.Clear();
                InteractVerb.Reset();
            }
        }

        // =====================================================================================
        //  THE SHIPPED COPY — the popup's cap is a tripwire on authoring, and it lives across
        //  three assemblies, so it is asserted HERE rather than in the UI-only layout suite.
        // =====================================================================================

        [Test]
        public void EveryShippedLabelFitsWithoutBeingCut()
        {
            // The cap is a tripwire on authored copy, not a layout engine. If one of these ever has to be
            // trimmed, the copy wanted rewriting — and this is where that gets noticed, in CI rather than
            // in a screenshot.
            string[] shipped =
            {
                HiddenHarbours.Player.ControlStrings.Board,
                HiddenHarbours.Player.ControlStrings.TakeHelm,
                HiddenHarbours.Player.ControlStrings.LeaveHelm,
                HiddenHarbours.Player.ControlStrings.Dock,
                HiddenHarbours.Player.ControlStrings.StepAshore,
                HiddenHarbours.Player.ControlStrings.GetOut,
                HiddenHarbours.World.WorldStrings.OfferLabel(HiddenHarbours.World.InteractKind.Talk,
                                                             HiddenHarbours.World.WorldStrings.GinnyName),
                HiddenHarbours.World.WorldStrings.OfferLabel(HiddenHarbours.World.InteractKind.Read,
                                                             HiddenHarbours.World.WorldStrings.NedLetterName),
                HiddenHarbours.World.WorldStrings.OfferLabel(HiddenHarbours.World.InteractKind.Talk,
                                                             HiddenHarbours.World.WorldStrings.NeighbourName),
            };

            foreach (string s in shipped)
                Assert.AreSame(s, InteractPopupLayout.Fit(s),
                               $"\"{s}\" is {s.Length} characters against a cap of {InteractPopupLayout.MaxCharacters}");
        }

        [Test]
        public void TheOfferLabelNeverNamesAKey()
        {
            // The whole point of the ruling: the popup says what you will DO. "E: Talk to Aunt Ginny" was
            // the habit it replaced, and this is the guard that stops it creeping back in through copy.
            string talk = HiddenHarbours.World.WorldStrings.OfferLabel(
                HiddenHarbours.World.InteractKind.Talk, HiddenHarbours.World.WorldStrings.GinnyName);
            Assert.IsFalse(talk.Contains("E:"));
            Assert.IsFalse(HiddenHarbours.Player.ControlStrings.Board.Contains("E:"));
            Assert.IsFalse(HiddenHarbours.Player.ControlStrings.Dock.Contains("E:"));
        }

        [Test]
        public void TheOfferLabelNamesTheSubjectWhenThereIsOne_AndOnlyTheVerbWhenThereIsNot()
        {
            Assert.AreEqual("Talk — Aunt Ginny",
                            HiddenHarbours.World.WorldStrings.OfferLabel(
                                HiddenHarbours.World.InteractKind.Talk, "Aunt Ginny"));
            Assert.AreEqual("Read — Ned's Logbook",
                            HiddenHarbours.World.WorldStrings.OfferLabel(
                                HiddenHarbours.World.InteractKind.Read, "Ned's Logbook"));
            Assert.AreEqual(HiddenHarbours.World.WorldStrings.TalkVerb,
                            HiddenHarbours.World.WorldStrings.OfferLabel(
                                HiddenHarbours.World.InteractKind.Talk, ""),
                            "an unnamed speaker gets the bare verb, never a dangling dash");
        }
        [Test]
        public void ResettingTheVerb_ReleasesItsSlot()
        {
            // Teardown must not leave the popup naming a fixture in a region that is gone.
            Set(InteractOfferSource.Fixture, "fixture.ghost", "Haunt");
            InteractVerb.Reset();
            Assert.IsFalse(InteractOffer.Current.Has);
        }
    }
}
