using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>THE BOOK CAN BE PUT DOWN — and everything it took while it was up is given back.</b>
    ///
    /// <para><b>Why this file exists.</b> The book raises three global holds when it opens: the interact
    /// verb (<see cref="InteractionGate"/>), the move axis (<see cref="MoveActionClaim"/>) and the cancel
    /// key (<see cref="CancelKeyClaim"/>). Each of them, left up, is a player who cannot act, cannot walk,
    /// or cannot open the pause menu — and every one of those reads as the game having hung rather than as
    /// a book still being open. So the assertions here are almost all about the RELEASE, on every way out
    /// there is, including the one nobody presses (the object going away).</para>
    ///
    /// <para><b>Driven through the component API, never through keys.</b> A virtual key press is
    /// undeliverable to the new Input System's device state, so a test that "pressed Esc" would be
    /// asserting on a press that never happened. <see cref="NotebookPresenter.Open"/>,
    /// <see cref="NotebookPresenter.Close"/> and <see cref="NotebookPresenter.DriveAxis"/> are the same
    /// methods this component's own <c>Update</c> calls, so what is driven here is what a press does.</para>
    ///
    /// <para><b>The content is wired to nothing, deliberately.</b> These tests are about the holds, not
    /// the pages, and an empty book flows and draws exactly as a full one does — while a book reading the
    /// shipped quest assets would go red the day the owner authored a quest, for no reason connected to
    /// what is under test.</para>
    ///
    /// <para><b>⚠ Nothing here asserts on LIFECYCLE — EditMode fires none of it.</b> A plain
    /// MonoBehaviour added in an EditMode test gets no <c>Awake</c>, no <c>OnEnable</c> and no
    /// <c>OnDisable</c>, so <c>Instance</c> registration, the <c>NotebookRequested</c> subscription and
    /// the torn-down/disabled close funnel CANNOT be honestly tested in this file — a first draft tried
    /// and produced four reds against a correct product. Those four claims live in
    /// <c>NotebookPlayTests</c>, where the real lifecycle runs them: the pause row opening the book, a
    /// second ask shutting it, and a disabled host giving the player back.</para>
    /// </summary>
    public class NotebookHoldsTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            InteractionGate.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();

            InteractionGate.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
        }

        /// <summary>A book with no contents and no art wired — see the class remarks.</summary>
        NotebookPresenter StandABook()
        {
            var go = new GameObject("book");
            _spawned.Add(go);

            var book = go.AddComponent<NotebookPresenter>();
            book.WireContent(() => new List<QuestDef>(),
                             () => new List<KnowledgePageDef>(),
                             () => new List<NotebookNote>(),
                             new InMemoryFlagStore());
            return book;
        }

        // =============================================================================
        //  the three holds
        // =============================================================================

        [Test]
        public void Opening_takes_the_verb_the_axis_and_the_cancel_key()
        {
            var book = StandABook();

            Assert.IsFalse(InteractionGate.IsBlocked, "the control arm: nothing is held before it opens");
            Assert.IsFalse(MoveActionClaim.IsClaimed);
            Assert.IsFalse(NotebookPresenter.OwnsCancelKey);

            book.Open();

            Assert.IsTrue(InteractionGate.IsBlocked,
                          "or turning a page would also board a boat");
            Assert.IsTrue(MoveActionClaim.IsClaimed,
                          "or turning a page would walk her off the wharf while she read");
            Assert.IsTrue(NotebookPresenter.OwnsCancelKey,
                          "or Esc would reach the pause menu instead of the book");
        }

        [Test]
        public void Closing_gives_all_three_back()
        {
            var book = StandABook();
            book.Open();
            book.Close();

            Assert.IsFalse(book.IsOpen);
            Assert.IsFalse(InteractionGate.IsBlocked, "the world's own keys work again");
            Assert.IsFalse(MoveActionClaim.IsClaimed, "⭐ and she can walk");
        }

        [Test]
        public void Opening_over_another_modals_hold_restores_it_rather_than_clearing_it()
        {
            var book = StandABook();

            // Something else already owns the interact key — a conversation mid-sentence, a fixture's
            // panel. The book is allowed over it; what it may not do is hand the key back to the world.
            InteractionGate.IsBlocked = true;

            book.Open();
            book.Close();

            Assert.IsTrue(InteractionGate.IsBlocked,
                          "⭐ RESTORED, not cleared — the modal underneath still owns the key it took");
        }

        [Test]
        public void Opening_over_a_claimed_axis_restores_that_too()
        {
            var book = StandABook();
            MoveActionClaim.IsClaimed = true;

            book.Open();
            book.Close();

            Assert.IsTrue(MoveActionClaim.IsClaimed,
                          "the same discipline as the gate, and for the same reason: whoever was steering " +
                          "before the book opened is still steering after it shuts");
        }

        [Test]
        public void A_book_shut_and_reopened_leaves_the_world_exactly_as_it_found_it()
        {
            var book = StandABook();

            book.Open();
            book.Close();
            book.Open();
            book.Close();

            Assert.IsFalse(InteractionGate.IsBlocked);
            Assert.IsFalse(MoveActionClaim.IsClaimed);
            Assert.IsFalse(book.IsOpen);
        }

        // =============================================================================
        //  the cancel key, which the pause menu also wants
        // =============================================================================

        [Test]
        public void The_book_owns_the_cancel_key_while_it_is_up_AND_on_the_frame_it_shuts()
        {
            var book = StandABook();

            Assert.IsFalse(NotebookPresenter.OwnsCancelKey, "nothing is up, so Esc belongs to the shell");

            book.Open();
            Assert.IsTrue(NotebookPresenter.OwnsCancelKey, "the book's Esc puts the book away");

            book.Close();

            // ⭐ THE WHOLE POINT. ShellPresenter reads the same key in its own Update, in an order Unity
            // does not define, so on the frame the press lands the answer must still be "mine" even
            // though the book has already gone. Without it, whether Esc also opened the pause menu on the
            // way out would be a coin toss on component order. (That the latch EXPIRES the following
            // frame needs a frame to pass and is asserted in PlayMode.)
            Assert.IsFalse(book.IsOpen, "…the book really is shut");
            Assert.IsTrue(NotebookPresenter.OwnsCancelKey,
                          "⭐ but the press is spent — the shell must not open the pause menu behind it");
        }

        [Test]
        public void The_shells_own_read_of_the_key_is_the_same_bit_the_book_sets()
        {
            var book = StandABook();

            // ShellPresenter.CanTogglePause cannot ask NotebookPresenter anything — UI does not
            // reference World (rule 4). What it asks is this, and this is what the book must have set.
            Assert.IsFalse(CancelKeyClaim.OwnedThisFrame, "the control arm");

            book.Open();
            Assert.IsTrue(CancelKeyClaim.OwnedThisFrame, "which is what the shell will read");
            Assert.IsTrue(CancelKeyClaim.IsHeld, "and held for the duration, not merely latched");

            book.Close();
            Assert.IsFalse(CancelKeyClaim.IsHeld, "no longer held…");
            Assert.IsTrue(CancelKeyClaim.OwnedThisFrame, "…but still spent, this frame");
        }

        // =============================================================================
        //  steering
        // =============================================================================

        [Test]
        public void A_held_push_steps_once_and_a_released_one_can_step_again()
        {
            var book = StandABook();
            book.Open();

            int tabs = book.Tabs.Count;
            Assert.Greater(tabs, 1, "an empty book still has its permanent stubs to step between");
            Assert.AreEqual(0, book.TabIndex);

            book.DriveAxis(new Vector2(0f, -1f));      // push down
            Assert.AreEqual(1, book.TabIndex, "one push, one stub");

            book.DriveAxis(new Vector2(0f, -1f));      // still held
            Assert.AreEqual(1, book.TabIndex, "⭐ a held key is not a stub per frame");

            book.DriveAxis(Vector2.zero);              // released
            book.DriveAxis(new Vector2(0f, -1f));      // pushed again
            Assert.AreEqual(2, book.TabIndex, "and letting go re-arms it");
        }

        [Test]
        public void The_axis_latch_does_not_survive_the_book_being_shut()
        {
            var book = StandABook();
            book.Open();

            book.DriveAxis(new Vector2(0f, -1f));
            Assert.AreEqual(1, book.TabIndex);

            // Shut with the key still down — she pressed Esc without letting go of S.
            book.Close();
            book.Open();

            // The same push, on a book that has just opened. Read as still-held it would do nothing;
            // read fresh it steps, which is what a player who is still holding the key means by it.
            book.DriveAxis(new Vector2(0f, -1f));
            Assert.AreEqual(2, book.TabIndex,
                            "⭐ the latch is the book's, and it goes when the book does");
        }
    }
}
