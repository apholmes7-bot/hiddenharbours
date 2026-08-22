using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The current quest left the bottom banner and became a note in her hand</b> — the owner's
    /// 2026-08-22 diegetic ruling, end to end.
    ///
    /// <para><b>What this proves that EditMode cannot.</b> The director's step logic runs in
    /// <c>Update</c> against live Core signals, and the note it feeds builds real canvases — so "the
    /// banner is gone and the note is up" is only assertable with frames passing. The words themselves
    /// are unchanged and still <see cref="WorldStrings"/>'s; what changed is who draws them and
    /// where.</para>
    ///
    /// <para><b>⚠ The negative half is the point.</b> A test that only checked the note exists would
    /// pass with the old banner still underneath it — which is exactly the failure the ruling was made
    /// against. So the bottom-centre banner is asserted GONE, by position rather than by name.</para>
    /// </summary>
    public class QuestNotePlayTests
    {
        const string SceneName = "QuestNote_Test";

        Scene _scene;
        GameObject _host;
        QuestPanelPresenter _note;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // A save service left wired by another file would carry ITS onboarding flags in here, and
            // "she has already met Ginny" is a different step and a different sentence.
            GameServices.Reset();

            _scene = SceneManager.CreateScene(SceneName);
            SceneManager.SetActiveScene(_scene);
            new GameObject("Rig", typeof(AudioListener));

            // No save service is wired, and that is fine: SaveFlagStore falls back to an in-memory
            // store, so this is a fresh game — she has not met Ginny, which is step one.
            _host = new GameObject("Onboarding");
            _host.AddComponent<OnboardingDirector>();
            yield return null;   // Awake + the first Update

            _note = _host.GetComponent<QuestPanelPresenter>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            GameServices.Reset();
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [UnityTest]
        public IEnumerator The_director_writes_the_step_onto_a_note()
        {
            yield return null;

            Assert.NotNull(_note, "⭐ the director draws through the note now — it makes one if there is none");
            Assert.IsTrue(_note.IsShowing, "a fresh game is on step one, so there is something to say");
            Assert.AreEqual(WorldStrings.OnboardTalkGinny, _note.Text,
                            "the WORDS are unchanged — only the hand they are written in");
        }

        [UnityTest]
        public IEnumerator The_note_is_pinned_lower_right_and_the_bottom_banner_is_gone()
        {
            yield return null;

            RectTransform rect = _host.GetComponentsInChildren<RectTransform>(true)
                                      .FirstOrDefault(r => r.name == "QuestNote");
            Assert.NotNull(rect, "the note's own rect");

            Assert.AreEqual(new Vector2(1f, 0f), rect.anchorMin, "anchored to the lower-RIGHT corner");
            Assert.AreEqual(new Vector2(1f, 0f), rect.anchorMax);
            Assert.AreEqual(new Vector2(1f, 0f), rect.pivot,
                            "pivoted there too, so the note grows up and left rather than off-screen");

            // ⚠ THE NEGATIVE HALF. The banner it replaced was anchored bottom-CENTRE — the nav
            // cluster's and the helm dash's ground. Nothing this director draws may sit there.
            foreach (RectTransform r in _host.GetComponentsInChildren<RectTransform>(true))
            {
                if (r.GetComponent<Text>() == null) continue;
                Assert.IsFalse(Mathf.Approximately(r.anchorMin.x, 0.5f) && Mathf.Approximately(r.anchorMin.y, 0f),
                               $"'{r.name}' is still drawing a bottom-centre banner");
            }
        }

        [UnityTest]
        public IEnumerator The_note_is_set_in_the_books_own_hand()
        {
            yield return null;

            Text[] lettering = _host.GetComponentsInChildren<Text>(true);
            Assert.IsNotEmpty(lettering, "the note letters its line");

            Font face = NotebookPresenter.LoadFace();
            if (face == null)
                Assert.Ignore("the notebook kit's face is not baked in this checkout — nothing to compare");

            Assert.IsTrue(lettering.All(t => t.font == face),
                          "⭐ the note is in the NOTEBOOK's hand, not a system font — that is the whole " +
                          "reason it stopped being a banner");
        }

        [UnityTest]
        public IEnumerator The_note_carries_the_TASKS_head_and_the_current_step_mark()
        {
            yield return null;

            Text[] lettering = _host.GetComponentsInChildren<Text>(true);

            Assert.IsTrue(lettering.Any(t => t.text == NotebookContent.LabelQuests),
                          "the head says which page this came off, so it reads as torn from the book");
            Assert.IsTrue(_host.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "Current"),
                          "and the current-step arrow marks the live step, as it does inside the book");
        }

        [UnityTest]
        public IEnumerator An_unchanged_step_redraws_nothing()
        {
            yield return null;

            RectTransform rect = _host.GetComponentsInChildren<RectTransform>(true)
                                      .FirstOrDefault(r => r.name == "QuestNote");
            Transform first = rect.GetChild(0);

            // The director hands the same line over every frame (rule 7 lives in Show's change
            // detection). If a frame rebuilt the note, this child would have been replaced.
            yield return null;
            yield return null;

            Assert.AreSame(first, rect.GetChild(0),
                           "a held step must cost a string compare, not a rebuild, every frame");
        }

        [UnityTest]
        public IEnumerator Clearing_the_step_takes_the_note_down()
        {
            yield return null;
            Assert.IsTrue(_note.IsShowing, "precondition");

            // ⚠ Stop the director first. It re-states the live step every frame, so a note hidden
            // underneath a running director would simply be put back up before the assert ran.
            _host.GetComponent<OnboardingDirector>().enabled = false;
            _note.Hide();
            yield return null;

            Assert.IsFalse(_note.IsShowing);
            Assert.IsFalse(_host.GetComponentsInChildren<Canvas>(true)
                                .First(c => c.name == "QuestNoteCanvas").gameObject.activeSelf,
                           "nothing to say means nothing on screen — not an empty page");
        }
    }
}
