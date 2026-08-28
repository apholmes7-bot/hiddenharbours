using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The one dialogue row that hands off and comes back</b> — a real <see cref="DialoguePresenter"/>
    /// over real frames, exercising the deferred-terminal row the shop talk is built on: picking it opens
    /// a seller's wares book, the conversation HOLDS rather than ending, and closing the book puts the
    /// same rows back so browse → sell → "See you later." is one conversation with one person (owner
    /// ruling on R2, 2026-08-27).
    ///
    /// <para><b>Why the hold is the thing under test and not the book.</b> The book is Economy's and
    /// draws nothing here; what World owes it is exactly two guarantees — that a catalog row does not
    /// reach <c>Finish()</c> (which would clear the interaction gate, drop the anchor and hide the
    /// bubble: the player walking away mid-book), and that <c>CatalogClosed</c> re-arms the picker on the
    /// rows that went down. Both are STATE, so both are assertable without drawing a pixel.</para>
    ///
    /// <para><b>⚠️ No key presses anywhere</b>, for the reason <c>DialogueBubblePlayTests</c> gives:
    /// headless input cannot deliver one and a self-skipping key-driven test is a hole. Every case drives
    /// <c>Advance()</c> / <c>MoveSelection(axis)</c>, which is precisely what a press calls.</para>
    ///
    /// <para><b>⚠️ Headless-safe by construction (do not relax).</b> Nothing renders, reads pixels or
    /// calls <c>Camera.Render</c> — CI runs with a null graphics device, where a ReadPixels PlayMode test
    /// does not fail, it kills the editor with no results XML at all.</para>
    /// </summary>
    public class DialogueCatalogHoldPlayTests
    {
        const string Seller = "seller.leblancs";
        const string Section = "gear";

        readonly List<Object> _spawned = new();
        readonly List<CatalogViewRequested> _views = new();
        readonly List<DialogueOptionPicked> _picks = new();

        DialoguePresenter _presenter;
        Transform _speaker;

        static DialogueVoice SlowVoice => new DialogueVoice
        {
            CharactersPerSecond = 4f,
            CharactersPerTick = 1,
            PunctuationPauseSeconds = 0f,
            TimbreId = "timbre.test",
        };

        [SetUp]
        public void SetUp()
        {
            // ⚠️ ONE AUDIO LISTENER: Unity logs "There are no audio listeners in the scene" every frame
            // of a listener-less play-mode scene, and these tests advance real frames on purpose.
            Spawn("AudioListener").AddComponent<AudioListener>();

            InteractionGate.Reset();

            var camGo = Spawn("TestCamera");
            camGo.tag = "MainCamera";                 // the presenter's Camera.main fallback
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            _speaker = Spawn("Speaker").transform;
            _speaker.position = Vector3.zero;

            _presenter = Spawn("DialoguePresenter").AddComponent<DialoguePresenter>();

            EventBus.Subscribe<CatalogViewRequested>(OnView);
            EventBus.Subscribe<DialogueOptionPicked>(OnPicked);
        }

        [TearDown]
        public void TearDown()
        {
            // ⚠️ Unsubscribe, never EventBus.Clear<T>(): Clear drops EVERY handler on the channel,
            // including the presenter's own CatalogClosed subscription, which would silently disarm
            // the thing these tests exist to cover.
            EventBus.Unsubscribe<CatalogViewRequested>(OnView);
            EventBus.Unsubscribe<DialogueOptionPicked>(OnPicked);
            _views.Clear();
            _picks.Clear();

            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            InteractionGate.Reset();
        }

        void OnView(CatalogViewRequested v) => _views.Add(v);
        void OnPicked(DialogueOptionPicked p) => _picks.Add(p);

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        static DialogueLine[] Lines(params string[] texts)
        {
            var lines = new DialogueLine[texts.Length];
            for (int i = 0; i < texts.Length; i++) lines[i] = new DialogueLine("Marguerite", texts[i]);
            return lines;
        }

        DialogueRequest Request(DialogueLine[] lines, IReadOnlyList<DialogueOption> options)
            => new DialogueRequest(lines, _speaker, new Vector3(0f, 2.1f, 0f), SlowVoice, options,
                                   "dialogue.test", "npc.marguerite_leblanc");

        /// <summary>Two frames — a test coroutine resumes during Update in an order Unity does not
        /// define, so anything written in a LateUpdate has certainly not happened after only one.</summary>
        static IEnumerator Settle()
        {
            yield return null;
            yield return null;
        }

        /// <summary>The shop rows: what have you got (the catalog row), a question, and the way out the
        /// picker appends itself.</summary>
        static IReadOnlyList<DialogueOption> ShopRows() => DialogueOptionPicker.RowsFor(new[]
        {
            new DialogueOption
            {
                Id = "option.browse",
                Label = "What have you got?",
                ReplyLines = System.Array.Empty<string>(),
                CatalogSellerId = Seller,
                CatalogSection = Section,
            },
            new DialogueOption
            {
                Id = "option.ask_cod",
                Label = "Any word on the cod?",
                ReplyLines = new[] { "Off the ledge, they say." },
            },
        });

        /// <summary>Talk to her and get as far as the rows being up.</summary>
        IEnumerator PlayToTheRows()
        {
            _presenter.Play(Request(Lines("Morning. Tide's dropping."), ShopRows()));
            yield return Settle();
            _presenter.Advance();     // fill the line
            _presenter.Advance();     // past it, into the rows
            yield return Settle();
        }

        /// <summary>Pick row 0 — the catalog row. The cursor starts there, so this is one confirm.</summary>
        IEnumerator PickBrowse()
        {
            yield return PlayToTheRows();
            Assert.That(_presenter.SelectedOption, Is.Zero, "the cursor starts on the first row");
            _presenter.Advance();     // confirm
            yield return Settle();
        }

        CanvasGroup Group() => _presenter.GetComponentInChildren<CanvasGroup>(true);

        // ---- the hold -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ACatalogRow_HoldsTheConversation_InsteadOfEndingIt()
        {
            yield return PickBrowse();

            Assert.That(_presenter.IsAwaitingCatalog, Is.True, "the book is open and she is waiting");
            Assert.That(_presenter.IsShowing, Is.True,
                        "the conversation has NOT ended — she is still standing there mid-sentence");
            Assert.That(_presenter.IsChoosing, Is.False, "the rows go down while the book is up");
            Assert.That(InteractionGate.IsBlocked, Is.True,
                        "or the Interact press that turns a page would also board the dory underneath");
            Assert.That(_presenter.BubbleIsVisible, Is.True, "the book does not replace her");
        }

        [UnityTest]
        public IEnumerator ACatalogRow_PublishesTheView_NamingTheSellerAndTheSpeaker()
        {
            yield return PickBrowse();

            Assert.That(_views.Count, Is.EqualTo(1), "exactly one book was asked for");
            Assert.That(_views[0].SellerId, Is.EqualTo(Seller));
            Assert.That(_views[0].Section, Is.EqualTo(Section));
            Assert.That(_views[0].SpeakerId, Is.EqualTo("npc.marguerite_leblanc"),
                        "whose counter you are standing at, so the book can head itself with her");

            Assert.That(_picks.Count, Is.EqualTo(1), "the pick is still reported like any other row");
            Assert.That(_picks[0].OptionId, Is.EqualTo("option.browse"));
        }

        [UnityTest]
        public IEnumerator WhileTheBookIsOpen_AdvanceIsRefused()
        {
            yield return PickBrowse();

            // THE REGRESSION THIS FILE EXISTS FOR. With the picker null and the runner still open, an
            // unguarded Advance() falls through to the runner and can END the conversation under the
            // open panel — leaving the player reading a book belonging to somebody who has stopped
            // talking to them.
            Assert.That(_presenter.Advance(), Is.False, "the press belongs to the book, not the bubble");
            Assert.That(_presenter.Advance(), Is.False);
            yield return Settle();

            Assert.That(_presenter.IsShowing, Is.True, "still mid-conversation");
            Assert.That(_presenter.IsAwaitingCatalog, Is.True, "still holding");
            Assert.That(InteractionGate.IsBlocked, Is.True);
        }

        [UnityTest]
        public IEnumerator TheBubbleFadesBack_WhileTheBookIsOpen_AndComesForwardOnClose()
        {
            yield return PlayToTheRows();
            Assert.That(Group().alpha, Is.EqualTo(1f).Within(0.001f), "full ink while she is talking");

            _presenter.Advance();                       // confirm the catalog row
            yield return Settle();
            Assert.That(Group().alpha, Is.EqualTo(DialoguePresenter.DimmedAlpha).Within(0.001f),
                        "faded back — the book is plainly what you are reading now");

            EventBus.Publish(new CatalogClosed(Seller));
            yield return Settle();
            Assert.That(Group().alpha, Is.EqualTo(1f).Within(0.001f), "and forward again when it shuts");
        }

        // ---- coming back ----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ClosingTheBook_PutsTheSameRowsBack_WithTheWayOutStillLast()
        {
            yield return PickBrowse();

            EventBus.Publish(new CatalogClosed(Seller));
            yield return Settle();

            Assert.That(_presenter.IsAwaitingCatalog, Is.False, "the hold is over");
            Assert.That(_presenter.IsChoosing, Is.True, "and the picker is live again");
            Assert.That(_presenter.IsShowing, Is.True);
            Assert.That(_presenter.Options.Count, Is.EqualTo(3),
                        "the rows that went down are the rows that came back");
            Assert.That(_presenter.Options[0].Id, Is.EqualTo("option.browse"));
            Assert.That(_presenter.Options[2].Id, Is.EqualTo(DialogueOption.CloseId),
                        "the way out is still last — the picker's guarantee survives the round trip");
        }

        [UnityTest]
        public IEnumerator AfterTheBookCloses_TheWayOutStillEndsTheConversation()
        {
            // The whole point of the ruling: browse, come back, and leave — without walking up to her
            // a second time.
            yield return PickBrowse();
            EventBus.Publish(new CatalogClosed(Seller));
            yield return Settle();

            _presenter.MoveSelection(-1f);   // down onto "Any word on the cod?"
            _presenter.MoveSelection(0f);    // let the axis latch fall back
            _presenter.MoveSelection(-1f);   // down onto "See you later."
            Assert.That(_presenter.SelectedOption, Is.EqualTo(2));

            _presenter.Advance();            // confirm the way out
            yield return Settle();

            Assert.That(_presenter.IsShowing, Is.False, "the conversation ends normally");
            Assert.That(InteractionGate.IsBlocked, Is.False, "and hands interaction back");
        }

        [UnityTest]
        public IEnumerator ABrowseThenASecondBrowse_HoldsAgain()
        {
            yield return PickBrowse();
            EventBus.Publish(new CatalogClosed(Seller));
            yield return Settle();

            _presenter.Advance();            // the cursor is back on row 0 — browse again
            yield return Settle();

            Assert.That(_presenter.IsAwaitingCatalog, Is.True, "a second look is just as legal");
            Assert.That(_views.Count, Is.EqualTo(2));
        }

        // ---- what must NOT have changed --------------------------------------------------------

        [UnityTest]
        public IEnumerator AnOrdinaryRow_IsStillTerminal()
        {
            yield return PlayToTheRows();

            _presenter.MoveSelection(-1f);   // down onto the plain question
            Assert.That(_presenter.SelectedOption, Is.EqualTo(1));

            _presenter.Advance();            // confirm it
            yield return Settle();

            Assert.That(_presenter.IsAwaitingCatalog, Is.False, "no book was asked for");
            Assert.That(_views, Is.Empty);
            Assert.That(_presenter.IsShowing, Is.True, "its reply is playing");

            _presenter.Advance();            // fill the reply
            _presenter.Advance();            // past it
            yield return Settle();
            Assert.That(_presenter.IsShowing, Is.False, "and then the conversation ends, as it always did");
        }

        [UnityTest]
        public IEnumerator ACatalogClosed_WithNoBookOpen_IsIgnored()
        {
            yield return PlayToTheRows();
            Assert.That(_presenter.IsChoosing, Is.True);

            EventBus.Publish(new CatalogClosed(Seller));   // nobody asked for a book
            yield return Settle();

            Assert.That(_presenter.IsChoosing, Is.True, "the rows were already up and stay exactly as they were");
            Assert.That(_presenter.SelectedOption, Is.Zero);
            Assert.That(_presenter.IsAwaitingCatalog, Is.False);
        }

        [UnityTest]
        public IEnumerator AConversationWalkedAwayFrom_MidBook_LeavesNothingWedged()
        {
            yield return PickBrowse();

            _presenter.Close();              // the player walked off / the region unloaded
            yield return Settle();

            Assert.That(_presenter.IsShowing, Is.False);
            Assert.That(_presenter.IsAwaitingCatalog, Is.False, "the hold went with the conversation");
            Assert.That(InteractionGate.IsBlocked, Is.False, "and the gate came back down");

            // A book closing AFTER that must not resurrect a picker on a conversation that has ended.
            EventBus.Publish(new CatalogClosed(Seller));
            yield return Settle();
            Assert.That(_presenter.IsChoosing, Is.False);
            Assert.That(_presenter.IsShowing, Is.False);
        }
    }
}
