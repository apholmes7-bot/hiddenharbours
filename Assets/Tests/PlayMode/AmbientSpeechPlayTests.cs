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
    /// <b>THE LAW, AS A TEST</b> — the owner's 2026-09-06 ruling in one sentence: <i>"the player can
    /// still walk and run as normal"</i>. An ambient bubble takes nothing. It never raises
    /// <see cref="InteractionGate"/>, never waits for a press, and goes away by itself.
    ///
    /// <para>A comment claiming "non-blocking" is worth nothing; this drives real frames with a player
    /// who is actually moving and asserts she never stopped, that the gate was never set on any frame,
    /// and that the bubble closed with nothing pressed. Everything else here — the pool cap, the
    /// handover to the modal, the tailless cool-tinted inner voice — is the same shape: state
    /// assertions over real frames.</para>
    ///
    /// <para><b>Headless-safe by construction (⚠ do not relax).</b> Nothing renders, reads pixels or
    /// calls <c>Camera.Render</c> — CI runs with a null graphics device, where a ReadPixels PlayMode test
    /// does not fail, it KILLS the editor with no results XML at all.</para>
    ///
    /// <para><b>⚠ The presenter under test is the SELF-INSTALLED one</b>, not a fresh component. It
    /// installs itself before the first scene and subscribes on the shared bus, so a second instance
    /// would draw every line twice and double every end signal. Finding it is therefore also the test
    /// that it installed at all.</para>
    /// </summary>
    public class AmbientSpeechPlayTests
    {
        readonly List<Object> _spawned = new();
        readonly List<AmbientSpeechEnded> _ends = new();

        AmbientSpeechPresenter _presenter;
        Transform _speaker;
        Camera _camera;

        /// <summary>A cadence slow enough that "it is still up" is a real span of frames rather than a
        /// race, with a read pause short enough that waiting it out does not take a test minute.</summary>
        const string TestVoiceId = "voice.test_ambient";
        static DialogueVoice SlowVoice => new DialogueVoice
        {
            CharactersPerSecond = 10f,
            CharactersPerTick = 1,
            PunctuationPauseSeconds = 0f,
            ReadPauseSeconds = 0.4f,
            TimbreId = "timbre.test",
        };

        [SetUp]
        public void SetUp()
        {
            // ⚠ ONE AUDIO LISTENER: Unity logs "There are no audio listeners in the scene" every frame of
            // a listener-less play-mode scene, and these tests advance real frames on purpose.
            Spawn("AudioListener").AddComponent<AudioListener>();

            InteractionGate.Reset();
            DialogueVoiceCatalog.Clear();
            DialogueVoiceCatalog.Register(TestVoiceId, SlowVoice);

            var camGo = Spawn("TestCamera");
            camGo.tag = "MainCamera";
            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 8f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);

            _speaker = Spawn("Speaker").transform;
            _speaker.position = Vector3.zero;

            // The SELF-INSTALLED one, by name rather than by a scene query: the host is hidden and
            // DontDestroyOnLoad, and this fixture must be talking to the same pool the game uses (a
            // second instance would draw every line twice and double every end signal).
            _presenter = AmbientSpeechPresenter.Instance;
            Assert.IsNotNull(_presenter,
                "AmbientSpeechPresenter did not install itself — every clue and every overheard line " +
                "in the game depends on that host existing with no scene wiring");
            _presenter.SetCamera(_camera);

            // ⚠ The presenter outlives every fixture in this file, so a line still standing from the
            // previous test would be counted by the next one. Start each from an empty screen.
            _presenter.CancelAll();

            // Safe to clear: nothing in the shipped game subscribes to the END signal yet, so this cannot
            // unsubscribe a live component the way clearing the REQUEST channel would.
            EventBus.Clear<AmbientSpeechEnded>();
            EventBus.Subscribe<AmbientSpeechEnded>(OnEnded);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<AmbientSpeechEnded>(OnEnded);
            _ends.Clear();

            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();

            if (_presenter != null) { _presenter.CancelAll(); _presenter.SetCamera(null); }
            DialogueVoiceCatalog.Clear();
            InteractionGate.Reset();
        }

        void OnEnded(AmbientSpeechEnded e) => _ends.Add(e);

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        int Say(string line, Transform anchor, AmbientSpeechKind kind = AmbientSpeechKind.NpcToNpc,
                string speakerName = null)
        {
            int id = AmbientSpeechId.Next();
            EventBus.Publish(new AmbientSpeechRequested(
                id, anchor, new Vector3(0f, 2.1f, 0f), "npc.test", speakerName, line, TestVoiceId, kind));
            return id;
        }

        /// <summary>Two frames: a test coroutine resumes during Update in an order Unity does not define,
        /// so anything written in a LateUpdate (the bubble's tracking) has certainly not happened after
        /// only one.</summary>
        static IEnumerator Settle()
        {
            yield return null;
            yield return null;
        }

        // ---- THE LAW ------------------------------------------------------------------------------

        /// <summary>
        /// ⭐ The whole feature in one test. She walks the entire time a bubble is up, at a constant
        /// speed, and the assertion is per FRAME: her position advanced, and the interaction gate was
        /// never raised on any of them. Then the bubble closes with nothing pressed.
        /// </summary>
        [UnityTest]
        public IEnumerator AnAmbientBubbleTakesNothingFromThePlayer()
        {
            Transform player = Spawn("Player").transform;
            player.position = new Vector3(-3f, 0f, 0f);

            const string line = "She'd swim again.";
            int id = Say(line, player, AmbientSpeechKind.InnerVoice);
            yield return null;
            Assert.IsTrue(_presenter.IsShowing(id), "the bubble never came up");

            // ⚠ BOUND THE WAIT ON SECONDS, NOT ON FRAMES. Headless batchmode renders nothing and runs
            // uncapped: this whole nine-test fixture completes in about two seconds of wall clock, so
            // 600 frames is a fraction of the 2.1 s this line actually dwells for. A frame budget here
            // fails as "the bubble never closed" while the bubble is behaving perfectly.
            float dwell = AmbientDwell.Seconds(line, SlowVoice);
            float deadline = dwell * 3f + 1f;

            float lastX = player.position.x;
            float elapsed = 0f;
            int frames = 0;

            while (_presenter.IsShowing(id) && elapsed < deadline)
            {
                // She keeps walking, exactly as she would with nothing on screen.
                player.position += new Vector3(2f * Time.deltaTime, 0f, 0f);
                yield return null;
                elapsed += Time.deltaTime;
                frames++;

                Assert.Greater(player.position.x, lastX,
                    $"frame {frames}: the player stopped moving while an ambient bubble was up");
                lastX = player.position.x;

                Assert.IsFalse(InteractionGate.IsBlocked,
                    $"frame {frames}: an ambient bubble raised InteractionGate — it must never claim " +
                    "the interact key, which is the whole of the owner's non-blocking law");
            }

            Assert.Less(elapsed, deadline,
                $"the bubble never closed on its own — it should have dwelled for {dwell:0.##} s and " +
                $"was still up after {elapsed:0.##} s ({frames} frames)");
            Assert.Greater(frames, 1, "no frames actually ran, so nothing was tested");
            Assert.IsFalse(_presenter.IsShowing(id));
            Assert.IsFalse(InteractionGate.IsBlocked);

            Assert.That(_ends.Count, Is.EqualTo(1), "exactly one end signal per accepted line");
            Assert.That(_ends[0].Id, Is.EqualTo(id));
            Assert.IsTrue(_ends[0].Delivered,
                "it should have ENDED by dwelling — nothing displaced or cancelled it");
        }

        [UnityTest]
        public IEnumerator ABubbleFillsAndThenStands_BeforeItCloses()
        {
            const string line = "Well now.";
            int id = Say(line, _speaker);
            yield return null;

            Assert.That(_presenter.VisibleTextOf(id), Is.Not.EqualTo(line),
                "the line was fully visible on the first frame — it is meant to fill at the voice's " +
                "cadence, the same as a spoken one");

            float fill = AmbientDwell.FillSeconds(line, SlowVoice);
            float t = 0f;
            while (t < fill + 0.05f) { yield return null; t += Time.deltaTime; }

            Assert.IsTrue(_presenter.IsShowing(id),
                "it closed the moment it finished filling — the read pause is what makes it readable");
            Assert.That(_presenter.VisibleTextOf(id), Is.EqualTo(line));
        }

        [UnityTest]
        public IEnumerator AnEmptyLineIsRefusedOutright_AndOwesNoEndSignal()
        {
            int id = Say("   ", _speaker);
            yield return Settle();

            Assert.IsFalse(_presenter.IsShowing(id));
            Assert.That(_ends, Is.Empty,
                "nothing was accepted, so nothing ended — a caller sequencing on the end signal must " +
                "not be handed one for a line that was never drawn");
        }

        // ---- the pool -------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ThePoolNeverExceedsItsCap_AndTheOldestIsWhatGivesWay()
        {
            var ids = new List<int>();
            int over = AmbientSpeechPresenter.MaxConcurrent + 2;
            for (int i = 0; i < over; i++) ids.Add(Say($"Line {i} of {over}.", _speaker));

            yield return Settle();

            Assert.That(_presenter.ActiveCount, Is.LessThanOrEqualTo(AmbientSpeechPresenter.MaxConcurrent),
                $"{over} lines arrived at once and more than {AmbientSpeechPresenter.MaxConcurrent} " +
                "bubbles are on screen");

            // The two earliest gave way, and said so — a waiting caller learns its line was dropped
            // rather than hanging on an end signal that never comes.
            for (int i = 0; i < over - AmbientSpeechPresenter.MaxConcurrent; i++)
            {
                Assert.IsFalse(_presenter.IsShowing(ids[i]), $"line {i} should have been displaced");
                Assert.IsTrue(_ends.Exists(e => e.Id == ids[i] &&
                                                e.Reason == AmbientSpeechEndReason.Displaced),
                              $"line {i} was displaced without publishing AmbientSpeechEnded");
            }

            // ...and the newest are the ones still up.
            for (int i = over - AmbientSpeechPresenter.MaxConcurrent; i < over; i++)
                Assert.IsTrue(_presenter.IsShowing(ids[i]), $"line {i} should still be up");
        }

        [UnityTest]
        public IEnumerator TwoSpeakersCanTalkAtOnce()
        {
            // The floor the cap exists to clear: a pair, both visible, both filling.
            Transform other = Spawn("Other").transform;
            other.position = new Vector3(3f, 0f, 0f);

            int a = Say("Some weather.", _speaker);
            int b = Say("It is that.", other);
            yield return Settle();

            Assert.IsTrue(_presenter.IsShowing(a));
            Assert.IsTrue(_presenter.IsShowing(b));
            Assert.That(_presenter.ActiveCount, Is.EqualTo(2));
        }

        // ---- the look -------------------------------------------------------------------------------

        /// <summary>
        /// Owner ruling 2026-09-06 on how the player's own thought reads: tailless, and a shade cooler
        /// than a spoken bubble. Asserted as STATE (the tail's enabled flag, the panel's colour) because
        /// CI has no graphics device to screenshot with.
        /// </summary>
        [UnityTest]
        public IEnumerator AnInnerVoiceBubbleIsTaillessAndCooler_WhereASpokenOneIsNot()
        {
            int spoken = Say("Some weather.", _speaker);
            yield return Settle();

            Assert.IsTrue(_presenter.TailIsVisibleFor(spoken),
                "an overheard line points at whoever said it");
            Color speechColour = _presenter.PanelColourOf(spoken);

            Transform player = Spawn("Player").transform;
            int thought = Say("I could put that right.", player, AmbientSpeechKind.InnerVoice);
            yield return Settle();

            Assert.IsFalse(_presenter.TailIsVisibleFor(thought),
                "a thought is not spoken, so nothing points at her");

            Color thoughtColour = _presenter.PanelColourOf(thought);
            Assert.That(thoughtColour.b / Mathf.Max(thoughtColour.r, 1e-4f),
                        Is.GreaterThan(speechColour.b / Mathf.Max(speechColour.r, 1e-4f)),
                        "the thought bubble should read COOLER than the spoken one (more blue against " +
                        "its red), which is the half of the distinction the missing tail does not carry");
        }

        // ---- handover -------------------------------------------------------------------------------

        /// <summary>
        /// ⭐ The modal wins (owner Q2). One person never has two bubbles: pressing Talk on somebody who
        /// is mid-line ends their ambient one, and the conversation the player chose is what is left.
        /// </summary>
        [UnityTest]
        public IEnumerator PressingTalkOnASpeakerMidLine_EndsTheirAmbientBubble()
        {
            var modal = Spawn("DialoguePresenter").AddComponent<DialoguePresenter>();

            int id = Say("Some weather we are having.", _speaker);
            yield return Settle();
            Assert.IsTrue(_presenter.IsShowing(id));

            modal.Play(new DialogueRequest(
                new[] { new DialogueLine("Aunt Ginny", "There you are.") },
                _speaker, new Vector3(0f, 2.1f, 0f), SlowVoice, null, "dialogue.test", "npc.test"));

            // The presenter re-scans for the modal on a throttle, so give it long enough to notice.
            float t = 0f;
            while (_presenter.IsShowing(id) && t < 2f) { yield return null; t += Time.deltaTime; }

            Assert.IsFalse(_presenter.IsShowing(id),
                "the ambient bubble survived the player pressing Talk on the same speaker");
            Assert.IsTrue(_ends.Exists(e => e.Id == id && e.Reason == AmbientSpeechEndReason.Cancelled),
                "it must end as CANCELLED, so a sequencing caller stops rather than speaking the next line");
            Assert.IsTrue(modal.IsShowing, "the conversation the player chose is what is left");

            modal.Close();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ASpeakerDestroyedMidLine_EndsTheBubbleRatherThanOrphaningIt()
        {
            var doomed = Spawn("Doomed");
            int id = Say("I was just saying—", doomed.transform);
            yield return Settle();
            Assert.IsTrue(_presenter.IsShowing(id));

            Object.DestroyImmediate(doomed);
            yield return Settle();

            Assert.IsFalse(_presenter.IsShowing(id));
            Assert.IsTrue(_ends.Exists(e => e.Id == id && e.Reason == AmbientSpeechEndReason.Cancelled));
        }

        // ---- the audio seam ---------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AnOverheardLineTicksTheAudioSeam_TheSameWayASpokenOneDoes()
        {
            var ticks = new List<DialogueTypewriterTick>();
            void Collect(DialogueTypewriterTick t) => ticks.Add(t);
            EventBus.Subscribe<DialogueTypewriterTick>(Collect);
            try
            {
                Say("Ayuh.", _speaker);
                float t = 0f;
                while (t < AmbientDwell.FillSeconds("Ayuh.", SlowVoice) + 0.1f)
                {
                    yield return null;
                    t += Time.deltaTime;
                }

                Assert.IsNotEmpty(ticks,
                    "an overheard voice went silent — the fill IS the sound, and the audio lane keys " +
                    "off this signal for a spoken line already");
                Assert.That(ticks[0].SpeakerId, Is.EqualTo("npc.test"),
                    "the tick must carry WHO is talking, or an overheard voice becomes anonymous");
                Assert.That(ticks[0].VoiceId, Is.EqualTo("timbre.test"));
            }
            finally
            {
                EventBus.Unsubscribe<DialogueTypewriterTick>(Collect);
            }
        }
    }
}
