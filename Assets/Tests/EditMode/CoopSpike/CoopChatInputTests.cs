using System.Collections.Generic;
using HiddenHarbours.CoopSpike;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.CoopSpike
{
    public sealed class CoopChatInputTests
    {
        [Test]
        public void EnterOpensThenSendsTheTrimmedLineAndReadsEachFrameOnce()
        {
            var input = new HeldChatIntents();
            var sent = new List<string>();
            using var composer = new CoopChatComposer(input, text => { sent.Add(text); return true; });
            input.Set(new ChatIntents(true, false)); composer.Tick();
            Assert.That(composer.IsOpen, Is.True);
            Assert.That(sent.Count, Is.Zero);
            input.Release(); composer.SetDraft("  crew aboard  "); composer.Tick();
            Assert.That(sent.Count, Is.Zero);
            input.Set(new ChatIntents(true, false)); composer.Tick();
            Assert.That(sent, Is.EqualTo(new[] { "crew aboard" }));
            Assert.That(composer.IsOpen, Is.False);
            Assert.That(composer.Draft, Is.Empty);
            Assert.That(input.Reads, Is.EqualTo(3));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void EnterOnAnEmptyLineJustCloses(string text)
        {
            var input = new HeldChatIntents();
            int sends = 0;
            using var composer = new CoopChatComposer(input, _ => { sends++; return true; });
            input.Set(new ChatIntents(true, false)); composer.Tick();
            composer.SetDraft(text); composer.Tick();
            Assert.That(composer.IsOpen, Is.False);
            Assert.That(sends, Is.Zero);
        }

        [Test]
        public void EscapeWinsOverSubmitAndDiscardsTheDraft()
        {
            var input = new HeldChatIntents();
            int sends = 0;
            using var composer = new CoopChatComposer(input, _ => { sends++; return true; });
            input.Set(new ChatIntents(true, false)); composer.Tick();
            composer.SetDraft("discard");
            input.Set(new ChatIntents(true, true)); composer.Tick();
            Assert.That(composer.IsOpen, Is.False);
            Assert.That(composer.Draft, Is.Empty);
            Assert.That(sends, Is.Zero);
        }

        [Test]
        public void RejectedSubmissionKeepsTheDraftAndTypingGateUntilCancelled()
        {
            var input = new HeldChatIntents();
            using var composer = new CoopChatComposer(input, _ => false);
            input.Set(new ChatIntents(true, false)); composer.Tick();
            composer.SetDraft("too soon"); composer.Tick();
            Assert.That(composer.IsOpen, Is.True);
            Assert.That(composer.Draft, Is.EqualTo("too soon"));
            input.Set(new ChatIntents(false, true)); composer.Tick();
            Assert.That(composer.IsOpen, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WalkAndDeckIntentsAreBlockedWhileTypingAndRestoredAfter(bool cancel)
        {
            var input = new HeldChatIntents();
            using var composer = new CoopChatComposer(input, _ => true);
            var walk = new HeldWalkIntents();
            var deck = new HeldDeckIntents();
            var expectedWalk = new WalkIntents(Vector2.right, true, true, true);
            var expectedDeck = new DeckIntents(Vector2.up, true);
            walk.Set(expectedWalk); deck.Set(expectedDeck);
            var gatedWalk = composer.Gate(walk);
            var gatedDeck = composer.Gate(deck);
            Assert.That(gatedWalk.Read(), Is.EqualTo(expectedWalk));
            Assert.That(gatedDeck.Read(), Is.EqualTo(expectedDeck));
            input.Set(new ChatIntents(true, false)); composer.Tick();
            Assert.That(gatedWalk.Read(), Is.EqualTo(WalkIntents.None));
            Assert.That(gatedDeck.Read(), Is.EqualTo(DeckIntents.None));
            Assert.That(walk.Reads, Is.EqualTo(1));
            Assert.That(deck.Reads, Is.EqualTo(1));
            composer.SetDraft("send");
            input.Set(new ChatIntents(!cancel, cancel)); composer.Tick();
            Assert.That(gatedWalk.Read(), Is.EqualTo(expectedWalk));
            Assert.That(gatedDeck.Read(), Is.EqualTo(expectedDeck));
            Assert.That(walk.Reads, Is.EqualTo(2));
            Assert.That(deck.Reads, Is.EqualTo(2));
        }

        [Test]
        public void TeardownReleasesTheSourceAndCannotReopenTheComposer()
        {
            var input = new HeldChatIntents();
            var composer = new CoopChatComposer(input, _ => true);
            var walk = new HeldWalkIntents(); walk.Walk(Vector2.left);
            var gated = composer.Gate(walk);
            input.Set(new ChatIntents(true, false)); composer.Tick();
            Assert.That(gated.Read(), Is.EqualTo(WalkIntents.None));
            composer.Dispose(); composer.Dispose(); composer.Tick();
            Assert.That(composer.IsOpen, Is.False);
            Assert.That(gated.Read(), Is.EqualTo(walk.Current));
            Assert.That(input.Reads, Is.EqualTo(1));
        }
    }
}
