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
    /// <b>The anchored bubble, over real frames</b> — a real <see cref="DialoguePresenter"/> with a real
    /// camera and a speaker who walks, so the seams that only exist at runtime are exercised: the bubble
    /// tracking a moving speaker, the text filling on the clock, the option rows opening after the last
    /// line, and the <see cref="InteractionGate"/> being raised and — the part that matters — always
    /// handed back.
    ///
    /// <para><b>⚠️ No key presses anywhere.</b> Headless input cannot deliver one, and a key-driven test
    /// that self-skips is a hole (the #555 lesson). Every case here drives the presenter's own decision
    /// surface — <c>Advance()</c>, <c>MoveSelection(axis)</c> — which is precisely what a press calls, so
    /// what is covered is what a press does. The one thing not covered here is the keyboard read itself,
    /// which is three lines in <c>WorldInteractor.ReadMoveAxis</c>.</para>
    ///
    /// <para><b>Headless-safe by construction (⚠️ do not relax).</b> Nothing renders, reads pixels or
    /// calls <c>Camera.Render</c> — CI runs with a null graphics device, where a ReadPixels PlayMode test
    /// does not fail, it KILLS the editor with no results XML at all. Every assertion is on STATE: an
    /// anchored position, a flag, a string, a published signal.</para>
    /// </summary>
    public class DialogueBubblePlayTests
    {
        readonly List<Object> _spawned = new();
        DialoguePresenter _presenter;
        Transform _speaker;

        readonly List<DialogueOptionPicked> _picks = new();
        readonly List<DialogueTypewriterTick> _ticks = new();

        // A cadence slow enough that "it is still filling" is a real frame and not a race.
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
            // ⚠️ ONE AUDIO LISTENER: Unity logs "There are no audio listeners in the scene" every frame of
            // a listener-less play-mode scene, and these tests advance real frames on purpose.
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

            EventBus.Subscribe<DialogueOptionPicked>(OnPicked);
            EventBus.Subscribe<DialogueTypewriterTick>(OnTick);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<DialogueOptionPicked>(OnPicked);
            EventBus.Unsubscribe<DialogueTypewriterTick>(OnTick);
            _picks.Clear();
            _ticks.Clear();

            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            InteractionGate.Reset();
        }

        void OnPicked(DialogueOptionPicked p) => _picks.Add(p);
        void OnTick(DialogueTypewriterTick t) => _ticks.Add(t);

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        static DialogueLine[] Lines(params string[] texts)
        {
            var lines = new DialogueLine[texts.Length];
            for (int i = 0; i < texts.Length; i++) lines[i] = new DialogueLine("Aunt Ginny", texts[i]);
            return lines;
        }

        DialogueRequest Request(DialogueLine[] lines, IReadOnlyList<DialogueOption> options = null)
            => new DialogueRequest(lines, _speaker, new Vector3(0f, 2.1f, 0f), SlowVoice, options,
                                   "dialogue.test", "npc.test");

        /// <summary>Two frames, for the same reason <c>VillagerRoutinePlayTests.At</c> takes two: a test
        /// coroutine resumes during Update in an order Unity does not define, so anything written in a
        /// LateUpdate (the bubble's tracking) has certainly not happened after only one.</summary>
        static IEnumerator Settle()
        {
            yield return null;
            yield return null;
        }

        // ---- the gate -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WhileTalking_TheInteractionGateIsUp_AndItComesBackDownOnClose()
        {
            Assert.That(InteractionGate.IsBlocked, Is.False);

            _presenter.Play(Request(Lines("Hello, love.")));
            yield return Settle();

            Assert.That(_presenter.IsShowing, Is.True);
            Assert.That(InteractionGate.IsBlocked, Is.True,
                        "or the one Interact press would also board the dory underneath");

            _presenter.Close();
            Assert.That(_presenter.IsShowing, Is.False);
            Assert.That(InteractionGate.IsBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator ABubbleTornDownMidLine_NeverLeavesInteractionWedgedOff()
        {
            _presenter.Play(Request(Lines("Mid-sentence…")));
            yield return Settle();
            Assert.That(InteractionGate.IsBlocked, Is.True);

            _presenter.enabled = false;      // the region unloaded under it
            yield return null;

            Assert.That(InteractionGate.IsBlocked, Is.False,
                        "a presenter disabled mid-line must hand the key back — otherwise the player " +
                        "cannot interact with anything, anywhere, ever again");
        }

        [UnityTest]
        public IEnumerator AnEmptyConversation_ShowsNothingAndStillCallsBack()
        {
            bool done = false;
            _presenter.Play(Request(new DialogueLine[0]), () => done = true);
            yield return Settle();

            Assert.That(_presenter.IsShowing, Is.False);
            Assert.That(done, Is.True, "no caller should have two endings to handle");
            Assert.That(InteractionGate.IsBlocked, Is.False);
        }

        /// <summary>
        /// The bubble cannot be positioned at all on a screen with no size, and a screen with no size is
        /// an ENVIRONMENT fact rather than a defect in this code — so say which one it is up front,
        /// rather than leaving a tracking assertion to fail with a message about the wrong thing.
        /// </summary>
        void AssertTheScreenHasSize()
        {
            Assert.That(Screen.width, Is.GreaterThan(0),
                        "ENVIRONMENT: the player has no screen size, so no canvas has a rect and nothing " +
                        "can be placed on it. Run without -nographics (the documented verification " +
                        "posture) rather than treating this as a bubble defect.");
            Assert.That(Screen.height, Is.GreaterThan(0), "ENVIRONMENT: see above.");
        }

        // ---- the anchor ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheBubbleHangsOverTheSpeaker_AndFollowsThemWhenTheyWalk()
        {
            AssertTheScreenHasSize();
            _presenter.Play(Request(Lines("Off the ledge, this week.")));
            yield return Settle();

            Assert.That(_presenter.BubbleIsVisible, Is.True);
            Assert.That(_presenter.TailIsVisible, Is.True, "there is somebody to point at");

            Vector2 before = _presenter.BubblePosition;
            Vector2 tailBefore = _presenter.TailPosition;

            _speaker.position = new Vector3(3f, 0f, 0f);
            yield return Settle();

            Assert.That(_presenter.BubblePosition.x, Is.GreaterThan(before.x + 1f),
                        "⭐ the bubble is welded to the speaker — walking right moves it right");
            Assert.That(_presenter.TailPosition.x, Is.GreaterThan(tailBefore.x + 1f),
                        "and the tail went with them");
        }

        [UnityTest]
        public IEnumerator TheTailSitsOnTheBubblesEdge_NotFloatingBesideIt()
        {
            AssertTheScreenHasSize();
            _presenter.Play(Request(Lines("A short one.")));
            yield return Settle();

            // The tail's root must land within the bubble's own width, whatever the clamp did.
            float dx = Mathf.Abs(_presenter.TailPosition.x - _presenter.BubblePosition.x);
            Assert.That(dx, Is.LessThan(400f),
                        "a tail further out than any bubble is wide is a tail drawn on nothing");
        }

        [UnityTest]
        public IEnumerator WithNobodyToPointAt_ThereIsNoTail_AndTheBubbleStillShows()
        {
            // The legacy string path: a conversation with no body in the world.
            _presenter.Play(Lines("The logbook is water-stained."));
            yield return Settle();

            Assert.That(_presenter.IsShowing, Is.True);
            Assert.That(_presenter.BubbleIsVisible, Is.True);
            Assert.That(_presenter.TailIsVisible, Is.False,
                        "pointing at nobody in particular would be worse than not pointing");
        }

        [UnityTest]
        public IEnumerator ASpeakerWhoWalksOutOfShot_TakesTheBubbleWithThem()
        {
            AssertTheScreenHasSize();
            _presenter.Play(Request(Lines("Still talking…")));
            yield return Settle();
            Assert.That(_presenter.BubbleIsVisible, Is.True);

            _speaker.position = new Vector3(0f, 400f, 0f);   // well past the top of any framing
            yield return Settle();

            Assert.That(_presenter.BubbleIsVisible, Is.False,
                        "a bubble left hanging at the screen edge would be pointing at nobody");
            Assert.That(_presenter.IsShowing, Is.True,
                        "…but the conversation itself is still up — this is a VIEW decision, not an end");
        }

        // ---- the fill -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheLineFillsACharacterAtATime_AndAPressFillsItAtOnce()
        {
            const string line = "There's a licence you'll be wanting first.";
            _presenter.Play(Request(Lines(line)));
            yield return Settle();

            Assert.That(_presenter.IsFilling, Is.True, "at four characters a second it is nowhere near done");
            Assert.That(_presenter.VisibleText.Length, Is.LessThan(line.Length));

            Assert.That(_presenter.Advance(), Is.True);
            Assert.That(_presenter.IsFilling, Is.False, "the impatient press fills the line");
            Assert.That(_presenter.VisibleText, Is.EqualTo(line));
            Assert.That(_presenter.IsShowing, Is.True, "and it does NOT also skip the line");
        }

        [UnityTest]
        public IEnumerator TheFillPublishesTheAudioTick_WithTheSpeakerAndTheVoiceOnIt()
        {
            _presenter.Play(Request(Lines("Hello.")));
            yield return Settle();
            yield return new WaitForSecondsRealtime(0.6f);   // ~2 characters at four a second
            yield return null;

            Assert.That(_ticks.Count, Is.GreaterThan(0),
                        "the fill IS the audio event — with nothing published there is nothing to hear");
            Assert.That(_ticks[0].SpeakerId, Is.EqualTo("npc.test"));
            Assert.That(_ticks[0].VoiceId, Is.EqualTo("timbre.test"));
            Assert.That(char.IsWhiteSpace(_ticks[0].Character), Is.False);
        }

        [UnityTest]
        public IEnumerator PressingThrough_WalksTheLinesAndThenEnds()
        {
            bool done = false;
            _presenter.Play(Request(Lines("One.", "Two.")), () => done = true);
            yield return Settle();

            _presenter.Advance();                 // fill line 1
            _presenter.Advance();                 // to line 2
            Assert.That(_presenter.IsShowing, Is.True);
            Assert.That(done, Is.False);

            _presenter.Advance();                 // fill line 2
            _presenter.Advance();                 // past the last line
            Assert.That(_presenter.IsShowing, Is.False);
            Assert.That(done, Is.True);
            Assert.That(InteractionGate.IsBlocked, Is.False);
        }

        // ---- the options --------------------------------------------------------------------

        static IReadOnlyList<DialogueOption> TwoQuestions() => DialogueOptionPicker.RowsFor(new[]
        {
            new DialogueOption { Id = "option.ask_grounds", Label = "Where's the cod running?",
                                 ReplyLines = new[] { "Off the ledge." } },
            new DialogueOption { Id = "option.front_fee", Label = "Can you front me the fee?",
                                 ReplyLines = System.Array.Empty<string>() },
        });

        IEnumerator PlayToTheOptions()
        {
            _presenter.Play(Request(Lines("Anything else, love?"), TwoQuestions()));
            yield return Settle();
            _presenter.Advance();     // fill the line
            _presenter.Advance();     // past it, into the rows
            yield return Settle();
        }

        [UnityTest]
        public IEnumerator AfterTheLastLine_TheRowsComeUpWithTheWayOutLast()
        {
            yield return PlayToTheOptions();

            Assert.That(_presenter.IsChoosing, Is.True);
            Assert.That(_presenter.IsShowing, Is.True, "the conversation has not ended, it has asked");
            Assert.That(_presenter.Options.Count, Is.EqualTo(3), "two questions plus the way out");
            Assert.That(_presenter.Options[2].Id, Is.EqualTo(DialogueOption.CloseId));
            Assert.That(_presenter.SelectedOption, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TheMoveAxisChoosesARow_AndConfirmingLandsOnTheOneItIsOn()
        {
            yield return PlayToTheOptions();

            Assert.That(_presenter.MoveSelection(-1f), Is.True, "down one row");
            Assert.That(_presenter.SelectedOption, Is.EqualTo(1));

            Assert.That(_presenter.MoveSelection(-1f), Is.False, "held is not held-again");
            _presenter.MoveSelection(0f);
            Assert.That(_presenter.MoveSelection(-1f), Is.True);
            Assert.That(_presenter.SelectedOption, Is.EqualTo(2), "now on the way out");

            _presenter.MoveSelection(0f);
            Assert.That(_presenter.MoveSelection(1f), Is.True, "back up one");
            Assert.That(_presenter.SelectedOption, Is.EqualTo(1));

            _presenter.Advance();      // confirm
            Assert.That(_picks.Count, Is.EqualTo(1));
            Assert.That(_picks[0].OptionId, Is.EqualTo("option.front_fee"),
                        "⭐ THE SABOTAGE ARM: it confirmed the row the cursor was on and no other");
            Assert.That(_picks[0].DialogueId, Is.EqualTo("dialogue.test"));
            Assert.That(_picks[0].SpeakerId, Is.EqualTo("npc.test"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ARowWithNoReply_EndsTheConversation()
        {
            yield return PlayToTheOptions();

            _presenter.MoveSelection(-1f);            // row 1: "can you front me the fee?", no reply
            _presenter.Advance();

            Assert.That(_presenter.IsShowing, Is.False);
            Assert.That(_presenter.IsChoosing, Is.False);
            Assert.That(InteractionGate.IsBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator ARowWithAReply_IsAnswered_AndThenTheConversationEnds()
        {
            yield return PlayToTheOptions();

            _presenter.Advance();                     // confirm row 0: the question with an answer
            yield return Settle();

            Assert.That(_presenter.IsShowing, Is.True, "she answers before she is done");
            Assert.That(_presenter.IsChoosing, Is.False, "and the rows have gone");

            _presenter.Advance();                     // fill the reply
            Assert.That(_presenter.VisibleText, Is.EqualTo("Off the ledge."));

            _presenter.Advance();                     // past it
            Assert.That(_presenter.IsShowing, Is.False,
                        "one round only — a reply never leads to a second picker");
            Assert.That(InteractionGate.IsBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator TakingTheWayOut_JustEnds()
        {
            yield return PlayToTheOptions();

            _presenter.MoveSelection(1f);             // wrap up from row 0 onto the last row
            Assert.That(_presenter.SelectedOption, Is.EqualTo(2));

            _presenter.Advance();

            Assert.That(_presenter.IsShowing, Is.False);
            Assert.That(_picks.Count, Is.EqualTo(1));
            Assert.That(_picks[0].OptionId, Is.EqualTo(DialogueOption.CloseId),
                        "the way out reports itself like any other row — uniform beats special");
            yield return null;
        }

        // ---- the greybox --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WithNoArtImported_TheBubbleStillDraws()
        {
            _presenter.Play(Request(Lines("Greybox.")));
            yield return Settle();

            Assert.That(_presenter.HasBubbleArt, Is.False,
                        "if this fails the bubble kit has landed — see DialogueBubbleArtTests for the flip");
            Assert.That(_presenter.BubbleIsVisible, Is.True,
                        "and with no sprite at all it must still be a bubble you can read, not a hole");
        }
    }
}
