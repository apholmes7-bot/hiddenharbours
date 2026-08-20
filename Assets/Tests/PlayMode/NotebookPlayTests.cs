using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The notebook, taken out and put away for real.</b> The real pause menu, its real Notebook row,
    /// the real Core signal, and the real presenter answering it — end to end, with nothing stubbed
    /// between the click and the book.
    ///
    /// <para><b>What this proves that EditMode cannot.</b> Four things, and the first two are the whole
    /// reason the file exists. (1) The book <b>installs itself</b>: <c>[RuntimeInitializeOnLoadMethod]</c>
    /// does not run in EditMode, so only here can "something answers the row" be asserted rather than
    /// assumed — before this landed, the row published into an empty room. (2) The two modules are
    /// actually wired: UI publishes, World answers, and neither references the other. (3) The cancel-key
    /// latch EXPIRES — that needs a frame to pass, and a frame is exactly what EditMode does not have.
    /// (4) The holds are released across real <c>Update</c>s rather than inside one call stack.</para>
    ///
    /// <para><b>⚠ Driven through the component API, never through keys.</b> A virtual key press is
    /// undeliverable to the new Input System's device state in a headless run, so a test that "pressed
    /// Esc" would be asserting on a press that never happened. <see cref="NotebookPresenter.Close"/> is
    /// the same method this component's own <c>Update</c> calls on that press, so what is driven here is
    /// what the press does.</para>
    ///
    /// <para><b>⚠ The book under test is the INSTALLED one.</b> It self-installs onto a
    /// <c>DontDestroyOnLoad</c> host and destroys any rival, so standing up a second would be testing an
    /// object torn down underneath the assertions. <see cref="NotebookPresenter.Instance"/> is that
    /// one.</para>
    ///
    /// <para>The scene is created here and unloaded in teardown — a scene left resident reds other files
    /// with failures that read like gameplay bugs.</para>
    /// </summary>
    public class NotebookPlayTests
    {
        const string SceneName = "Notebook_Test";

        Scene _scene;
        NotebookPresenter _book;

        static Button FindButton(string name)
            => Object.FindObjectsByType<Button>()
                     .FirstOrDefault(b => b.gameObject.activeInHierarchy && b.name == name);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _scene = SceneManager.CreateScene(SceneName);
            SceneManager.SetActiveScene(_scene);

            // One listener, so a play-mode scene does not log "there are no audio listeners" every frame.
            new GameObject("Rig", typeof(AudioListener));

            _book = NotebookPresenter.Instance;
            Assert.NotNull(_book, "⭐ the book self-installs at AfterSceneLoad; if it did not, NOTHING in " +
                                  "the game answers the pause menu's Notebook row");

            // ⚠️ RE-ARM THE SUBSCRIPTION. The presenter subscribes once, in OnEnable, at install — and any
            // test in the suite that calls EventBus.Clear<NotebookRequested>() to isolate its own
            // assertion would drop every handler on that channel including this one. Whether that has
            // happened depends on test order, so this cycles OnDisable/OnEnable to put it back rather
            // than betting on running first. (Close on the way down is a no-op while shut.)
            _book.enabled = false;
            _book.enabled = true;

            InteractionGate.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();

            yield return null;   // let Awake/OnEnable/Start land before anything is driven
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_book != null) _book.Close();
            PauseMenu.CloseIfOpen();
            ShellPause.Reset();

            InteractionGate.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();

            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        // =============================================================================
        //  opening it
        // =============================================================================

        [UnityTest]
        public IEnumerator The_pause_menus_Notebook_row_actually_opens_the_book()
        {
            PauseMenu.Open();
            yield return null;
            Assert.IsTrue(PauseMenu.IsOpen, "…the menu is up to be clicked");

            Button row = FindButton("Notebook");
            Assert.NotNull(row, "the row the whole binding hangs off");

            row.onClick.Invoke();
            yield return null;

            Assert.IsFalse(PauseMenu.IsOpen,
                           "the menu goes away first — the book is a thing she holds standing in the " +
                           "world, not another page of the menu");
            Assert.IsTrue(_book.IsOpen,
                          "⭐ AND THE BOOK IS UP. UI published, World answered, and neither names the other");
        }

        [UnityTest]
        public IEnumerator An_open_book_owns_the_verb_the_axis_and_the_cancel_key()
        {
            EventBus.Publish(new NotebookRequested("test"));
            yield return null;

            Assert.IsTrue(_book.IsOpen);
            Assert.IsTrue(InteractionGate.IsBlocked,
                          "so the press that turns a page cannot also board a boat");
            Assert.IsTrue(MoveActionClaim.IsClaimed,
                          "⭐ the walk keys are dead: PlayerWalkController.ReadInput returns zero while " +
                          "this is up, which is what stops a page turn walking her off the wharf");
            Assert.IsTrue(NotebookPresenter.OwnsCancelKey, "and Esc is the book's");
        }

        [UnityTest]
        public IEnumerator The_book_stays_up_across_frames_rather_than_being_shut_by_the_press_that_opened_it()
        {
            EventBus.Publish(new NotebookRequested("test"));

            // Three frames of the real Update. Nothing is pressed, so nothing may close it — and in
            // particular the _openedFrame guard must not be the only thing holding it open.
            yield return null;
            yield return null;
            yield return null;

            Assert.IsTrue(_book.IsOpen, "a book that shut itself would be a book nobody ever saw");
            Assert.IsTrue(InteractionGate.IsBlocked, "and the holds are still up with it");
            Assert.IsTrue(MoveActionClaim.IsClaimed);
        }

        // =============================================================================
        //  shutting it — the race the wardrobe lane measured
        // =============================================================================

        [UnityTest]
        public IEnumerator Shutting_it_hands_everything_back_and_the_pause_menu_does_not_open_behind_it()
        {
            EventBus.Publish(new NotebookRequested("test"));
            yield return null;
            Assert.IsTrue(_book.IsOpen);

            _book.Close();

            // ⭐ NO YIELD. This is the frame the Esc press landed on, and it is the whole point:
            // ShellPresenter reads the same key in its own Update, in an order Unity does not define, so
            // on this frame the answer must still be "the book's" even though the book has already gone.
            // Without it, whether Esc also opened the pause menu on the way out would be a coin toss on
            // component order.
            Assert.IsFalse(_book.IsOpen, "…the book really is shut");
            Assert.IsTrue(NotebookPresenter.OwnsCancelKey,
                          "⭐ but the press is spent — which is what CanTogglePause reads, and why it declines");
            Assert.IsFalse(PauseMenu.IsOpen, "so no pause menu appeared behind the closing book");

            Assert.IsFalse(InteractionGate.IsBlocked, "the world's keys are the world's again");
            Assert.IsFalse(MoveActionClaim.IsClaimed, "⭐ and she can walk");

            yield return null;
            Assert.IsFalse(NotebookPresenter.OwnsCancelKey,
                           "⭐ and by the NEXT frame Esc is the shell's again — the latch is one frame " +
                           "wide, or it would wedge the pause menu off for good");
            Assert.IsFalse(PauseMenu.IsOpen);
        }

        [UnityTest]
        public IEnumerator A_second_ask_puts_it_away_as_surely_as_the_first_took_it_out()
        {
            EventBus.Publish(new NotebookRequested("test"));
            yield return null;
            Assert.IsTrue(_book.IsOpen);

            EventBus.Publish(new NotebookRequested("test"));
            yield return null;

            Assert.IsFalse(_book.IsOpen, "the same signal is the toggle — a desk, a bedside table, the row");
            Assert.IsFalse(InteractionGate.IsBlocked);
            Assert.IsFalse(MoveActionClaim.IsClaimed);
        }

        [UnityTest]
        public IEnumerator A_book_left_open_when_its_host_is_disabled_still_gives_the_player_back()
        {
            EventBus.Publish(new NotebookRequested("test"));
            yield return null;
            Assert.IsTrue(MoveActionClaim.IsClaimed);

            // The one nobody presses: the component going away with the book still up.
            _book.enabled = false;
            yield return null;

            Assert.IsFalse(_book.IsOpen);
            Assert.IsFalse(InteractionGate.IsBlocked);
            Assert.IsFalse(MoveActionClaim.IsClaimed, "⭐ a torn-down book cannot leave her unable to move");

            _book.enabled = true;   // put the installed one back for whoever runs next
            yield return null;
        }

        // =============================================================================
        //  what is actually on the page
        // =============================================================================

        [UnityTest]
        public IEnumerator It_opens_onto_the_games_own_contents_with_nobody_having_wired_them()
        {
            EventBus.Publish(new NotebookRequested("test"));
            yield return null;

            // Nothing called WireContent — the installed book resolved its own sources. The permanent
            // stubs are the readable proof that it did: TASKS, DONE and MINE are furniture, and their
            // presence means Build() ran over real suppliers rather than over four nulls.
            Assert.GreaterOrEqual(_book.Tabs.Count, 3,
                                  "the three permanent stubs at least — the book found its own contents");
            Assert.LessOrEqual(_book.Tabs.Count, NotebookKit.MaxTabs,
                               "and never more stubs than the fore-edge holds");

            Assert.Greater(_book.Fit.Cols, 0, "it laid out at a real size for this screen");
            Assert.Greater(_book.Fit.Lines, 0);
        }

        [UnityTest]
        public IEnumerator It_dresses_itself_from_Resources_since_no_builder_can_hand_it_anything()
        {
            EventBus.Publish(new NotebookRequested("test"));
            yield return null;

            // The face is the readable half of "did the runtime art path work at all" — it is a single
            // asset, loaded by key, through the same mechanism as all 26 pieces. A null here in a project
            // where the bake IS committed means the Resources root moved, and the whole book would be
            // drawn in grey rectangles and the built-in legacy font.
            Assert.IsTrue(_book.HasKitFace,
                          "⭐ the book is set in the kit's own hand, resolved at runtime with nothing " +
                          "wired — if this is red, check that the baked face is still inside a folder " +
                          "named 'Resources' (HarbourType.ResourceKey)");
        }
    }
}
