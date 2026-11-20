using System.Collections;
using System.Collections.Generic;
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
    /// <b>The Gear page, opened for real</b>: the installed book answers the Notebook signal, the KIT
    /// stub is on its fore-edge, and turning to it letters a known owned item with its icon from the
    /// icon library the game boots. Driven through the component API (no virtual key presses, which
    /// never reach the Input System headless), on the installed book (see <c>NotebookPlayTests</c>).
    /// </summary>
    public class NotebookGearPlayTests
    {
        const string SceneName = "NotebookGear_Test";

        Scene _scene;
        NotebookPresenter _book;

        sealed class OwnsTheRod : INotebookGearSource
        {
            public void Collect(List<NotebookGearHolding> into)
            {
                into.Clear();
                into.Add(new NotebookGearHolding("gear.rod", NotebookGearCount.Presence));
                into.Add(new NotebookGearHolding("trap.lobster", NotebookGearCount.Stock, 4, 1));
            }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _scene = SceneManager.CreateScene(SceneName);
            SceneManager.SetActiveScene(_scene);
            new GameObject("Rig", typeof(AudioListener));

            _book = NotebookPresenter.Instance;
            Assert.NotNull(_book, "the book self-installs at AfterSceneLoad");
            _book.enabled = false;   // re-arm the NotebookRequested subscription (see NotebookPlayTests)
            _book.enabled = true;

            InteractionGate.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_book != null)
            {
                _book.Close();
                _book.WireGear(null);   // the running game's source and page back
            }
            InteractionGate.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
            if (_scene.IsValid() && _scene.isLoaded) yield return HiddenHarbours.Tests.Support.SceneSettle.Unload(_scene);
        }

        Text Lettered(string s) => _book.GetComponentsInChildren<Text>(false).FirstOrDefault(t => t.text == s);

        [UnityTest]
        public IEnumerator The_Notebook_opens_onto_a_Gear_page_that_shows_an_owned_item_with_its_icon()
        {
            _book.WireGear(new OwnsTheRod(), NotebookContentSource.GearPage());

            EventBus.Publish(new NotebookRequested("test"));
            yield return null;
            Assert.IsTrue(_book.IsOpen, "the Notebook signal opened the book");

            int gear = _book.TabIndexOf(NotebookContent.TabGear);
            Assert.GreaterOrEqual(gear, 0, "the KIT stub (the Gear page) is on the fore-edge");
            _book.SelectTab(gear);
            yield return null;

            Assert.NotNull(Lettered("Fishing Rod"), "the owned rod is lettered with its Def's own name");
            Assert.NotNull(Lettered("RODS AND TACKLE"), "under its heading");
            Assert.NotNull(Lettered("Have 4, 1 set"), "and the pots carry their count");

            Sprite rodIcon = IconRegistry.Get("gear.rod");
            Assert.NotNull(rodIcon, "the icon library the game boots holds the rod's icon");
            Image[] icons = _book.GetComponentsInChildren<Image>(false)
                                 .Where(i => i.name == NotebookPresenter.IconPieceName).ToArray();
            Assert.IsTrue(icons.Any(i => i.sprite == rodIcon), "⭐ the rod's icon shows on the page");
        }

        [UnityTest]
        public IEnumerator With_nothing_wired_the_page_reads_the_running_games_save()
        {
            Assume.That(GameServices.Save?.Current, Is.Not.Null, "needs the self-installing save service");

            _book.WireGear(null);
            _book.Open();
            yield return null;

            int gear = _book.TabIndexOf(NotebookContent.TabGear);
            Assert.GreaterOrEqual(gear, 0, "the watch is worn from the start, so the page is always there");
            _book.SelectTab(gear);
            yield return null;

            Assert.IsTrue(_book.GetComponentsInChildren<Text>(false).Any(t => t.text.StartsWith("Harbour")),
                          "the starting watch, read through Core's watch selection, never from another module");
        }
    }
}
