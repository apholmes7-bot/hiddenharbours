using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE PLAYER'S LOOK, AS A SEAM</b> (character PR 2b, ADR 0051): <see cref="PlayerAppearance"/>
    /// over a real <see cref="SaveService"/> on the test's own temp save.
    ///
    /// <list type="bullet">
    /// <item>A preview is shown (published) and never written; cancelling shows the kept recipe again by
    /// the same route.</item>
    /// <item>A commit writes the save once, announces once and ends the preview; keeping the look
    /// already kept writes nothing and announces nothing.</item>
    /// <item>A recipe the builder refuses is neither shown nor kept; the empty recipe is never refused
    /// and never asks the builder.</item>
    /// <item>What the seam hands out are copies, and a reset drops an open preview.</item>
    /// </list>
    ///
    /// <para>The builder is a stand-in here (the refusal is injected), so these say what the SEAM does
    /// with an answer. That the shipped builder answers as the rig does is
    /// <c>CharacterBuilderOracleTests</c>'s.</para>
    /// </summary>
    public class PlayerAppearanceTests
    {
        private const string Bogus = "bogus";

        private string _dir;
        private string _save;
        private readonly List<GameObject> _made = new List<GameObject>();
        private readonly List<CharacterRecipe> _changed = new List<CharacterRecipe>();
        private readonly List<CharacterRecipe> _previewed = new List<CharacterRecipe>();
        private int _asked;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hh_player_appearance_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _save = Path.Combine(_dir, SaveStore.DefaultFileName);
            GameServices.Reset();
            ShellFlow.Reset();
            _asked = 0;
            EventBus.Subscribe<PlayerAppearanceChanged>(OnChanged);
            EventBus.Subscribe<PlayerAppearancePreviewRequested>(OnPreviewed);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<PlayerAppearanceChanged>(OnChanged);
            EventBus.Unsubscribe<PlayerAppearancePreviewRequested>(OnPreviewed);
            _changed.Clear();
            _previewed.Clear();
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            GameServices.Reset();
            ShellFlow.Reset();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        // ---- preview -----------------------------------------------------------------------------

        [Test]
        public void APreview_IsShown_AndNeverWritten()
        {
            Counted save = OpenSave();
            PlayerAppearance seam = Seam(save);
            byte[] before = File.ReadAllBytes(_save);

            Assert.IsTrue(seam.Preview(Teal(), out string refusal), refusal);

            Assert.IsTrue(seam.IsPreviewing);
            Assert.AreEqual(1, _previewed.Count, "the preview was not published");
            Assert.IsTrue(_previewed[0].SameAs(Teal()), $"published {_previewed[0]}");
            Assert.IsTrue(seam.Showing.SameAs(Teal()));
            Assert.IsTrue(seam.Committed.IsEmpty, "a preview reached the save");
            Assert.IsTrue(save.Current.PlayerRecipe.IsEmpty, "a preview reached the save");
            Assert.AreEqual(0, save.Saves, "a preview wrote the file");
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_save), "a preview changed the file");
            Assert.IsEmpty(_changed, "a preview announced a change");
        }

        [Test]
        public void Cancelling_ShowsTheKeptRecipeAgain_ByTheSameRoute_AndWritesNothing()
        {
            Counted save = OpenSave();
            PlayerAppearance seam = Seam(save);
            Assert.IsTrue(seam.Commit(Ginny(), out string refusal), refusal);
            int saves = save.Saves;
            _previewed.Clear();

            seam.Preview(Teal(), out _);
            seam.CancelPreview();

            Assert.IsFalse(seam.IsPreviewing);
            Assert.AreEqual(2, _previewed.Count, "a cancel ends a preview the way it began: with a preview");
            Assert.IsTrue(_previewed[1].SameAs(Ginny()), $"the cancel showed {_previewed[1]}, not the kept recipe");
            Assert.IsTrue(seam.Showing.SameAs(Ginny()));
            Assert.AreEqual(saves, save.Saves, "a cancel wrote the file");

            seam.CancelPreview();
            Assert.AreEqual(2, _previewed.Count, "a cancel with no preview open published something");
        }

        // ---- commit ------------------------------------------------------------------------------

        [Test]
        public void Committing_WritesTheSave_AnnouncesOnce_AndEndsThePreview()
        {
            Counted save = OpenSave();
            PlayerAppearance seam = Seam(save);
            seam.Preview(Teal(), out _);

            Assert.IsTrue(seam.Commit(Teal(), out string refusal), refusal);

            Assert.IsFalse(seam.IsPreviewing, "a commit left the preview open");
            Assert.AreEqual(1, save.Saves, "a commit must persist, once");
            Assert.IsTrue(save.Current.PlayerRecipe.SameAs(Teal()));
            Assert.IsTrue(SaveStore.Read(_save).PlayerRecipe.SameAs(Teal()), "the file does not hold the kept recipe");
            Assert.AreEqual(1, _changed.Count);
            Assert.IsTrue(_changed[0].SameAs(Teal()), $"announced {_changed[0]}");
        }

        [Test]
        public void KeepingTheLookAlreadyKept_WritesNothing_AndAnnouncesNothing()
        {
            Counted save = OpenSave();
            PlayerAppearance seam = Seam(save);
            Assert.IsTrue(seam.Commit(Teal(), out _));

            Assert.IsTrue(seam.Commit(Teal(), out string refusal), refusal);

            Assert.AreEqual(1, save.Saves, "re-keeping the look already worn wrote the file again");
            Assert.AreEqual(1, _changed.Count, "re-keeping the look already worn announced a change");
        }

        [Test]
        public void CommittingTheKeptLook_OverAPreview_PutsTheKeptLookBack()
        {
            Counted save = OpenSave();
            PlayerAppearance seam = Seam(save);
            Assert.IsTrue(seam.Commit(Ginny(), out _));
            _previewed.Clear();
            seam.Preview(Teal(), out _);

            Assert.IsTrue(seam.Commit(Ginny(), out _));

            Assert.IsFalse(seam.IsPreviewing);
            Assert.AreEqual(2, _previewed.Count, "the preview of teal was left on screen");
            Assert.IsTrue(_previewed[1].SameAs(Ginny()));
            Assert.AreEqual(1, save.Saves);
        }

        [Test]
        public void AKeptRecipe_SurvivesAReload()
        {
            Assert.IsTrue(Seam(OpenSave()).Commit(Ginny(), out string refusal), refusal);

            PlayerAppearance reopened = Seam(OpenSave());

            Assert.IsTrue(reopened.Committed.SameAs(Ginny()), $"reloaded {reopened.Committed}");
        }

        // ---- refusals ----------------------------------------------------------------------------

        [Test]
        public void ARefusedRecipe_IsNeitherShownNorKept()
        {
            Counted save = OpenSave();
            PlayerAppearance seam = Seam(save);
            CharacterRecipe bad = Teal();
            bad.Outfit = Bogus;

            Assert.IsFalse(seam.Preview(bad, out string previewRefusal));
            StringAssert.Contains(Bogus, previewRefusal, "the refusal does not name what it refused");
            Assert.IsFalse(seam.IsPreviewing);
            Assert.IsEmpty(_previewed, "a refused recipe was shown");

            Assert.IsFalse(seam.Commit(bad, out string commitRefusal));
            StringAssert.Contains(Bogus, commitRefusal);
            Assert.AreEqual(0, save.Saves, "a refused recipe was written");
            Assert.IsTrue(save.Current.PlayerRecipe.IsEmpty);
            Assert.IsEmpty(_changed);
        }

        [Test]
        public void TheEmptyRecipe_IsNeverRefused_AndNeverAsksTheBuilder()
        {
            Counted save = OpenSave();
            PlayerAppearance seam = Seam(save);

            Assert.IsTrue(seam.Preview(new CharacterRecipe(), out string a), a);
            Assert.IsTrue(seam.Commit(new CharacterRecipe(), out string b), b);

            Assert.AreEqual(0, _asked, "the body as baked needs no builder");
            Assert.AreEqual(0, save.Saves, "keeping the empty recipe over an empty save is no change");
        }

        [Test]
        public void AFitNamedOnItsOwn_IsStillChecked()
        {
            PlayerAppearance seam = Seam(OpenSave());

            seam.Commit(new CharacterRecipe { FitId = "fit.character_v10" }, out _);

            Assert.AreEqual(1, _asked, "a recipe naming only a fit was kept without asking the builder");
        }

        [Test]
        public void WithNoSaveLoaded_NothingIsKept()
        {
            var seam = new PlayerAppearance(() => null, Refuse);

            Assert.IsFalse(seam.Commit(Teal(), out string refusal));
            Assert.IsNotEmpty(refusal);
            Assert.IsTrue(seam.Committed.IsEmpty);
            Assert.IsEmpty(_changed);
        }

        [Test]
        public void ANullRecipe_IsRefused_NotThrownOn()
        {
            PlayerAppearance seam = Seam(OpenSave());

            Assert.IsFalse(seam.Preview(null, out string a));
            Assert.IsFalse(seam.Commit(null, out string b));
            Assert.IsNotEmpty(a);
            Assert.IsNotEmpty(b);
        }

        // ---- copies and lifetime -----------------------------------------------------------------

        [Test]
        public void WhatTheSeamHandsOut_AreCopies()
        {
            Counted save = OpenSave();
            PlayerAppearance seam = Seam(save);
            CharacterRecipe mine = Teal();
            Assert.IsTrue(seam.Commit(mine, out _));

            mine.Outfit = "red";
            seam.Committed.Outfit = "red";
            seam.Showing.Outfit = "red";
            _changed[0].Outfit = "red";

            Assert.AreEqual(Teal().Outfit, save.Current.PlayerRecipe.Outfit,
                "the kept recipe was edited through something the seam handed out");
        }

        [Test]
        public void TheGamesSeam_IsMadeOnFirstAsk_AndAResetDropsAnOpenPreview()
        {
            Assert.IsNotNull(GameServices.Appearance, "the seam must never be null: nothing wires it");
            Assert.AreSame(GameServices.Appearance, GameServices.Appearance, "the seam is made once, not per ask");

            Counted save = OpenSave();
            GameServices.Save = save;
            var seam = new PlayerAppearance(refuse: Refuse);
            GameServices.Appearance = seam;
            Assert.IsTrue(seam.Commit(Teal(), out string refusal), refusal);
            Assert.AreEqual(1, save.Saves, "the default save seam is not GameServices.Save");
            seam.Preview(Ginny(), out _);

            GameServices.Reset();

            Assert.AreNotSame(seam, GameServices.Appearance, "a reset kept the seam");
            Assert.IsFalse(GameServices.Appearance.IsPreviewing, "a preview outlived the session that opened it");
        }

        // ---- helpers -----------------------------------------------------------------------------

        private void OnChanged(PlayerAppearanceChanged e) => _changed.Add(e.Recipe);
        private void OnPreviewed(PlayerAppearancePreviewRequested e) => _previewed.Add(e.Recipe);

        /// <summary>The builder's stand-in: refuses an outfit named <see cref="Bogus"/>, by name.</summary>
        private string Refuse(CharacterRecipe recipe)
        {
            _asked++;
            return recipe.Outfit == Bogus ? $"The recipe's outfit '{Bogus}' is not one of the rig's options." : null;
        }

        private PlayerAppearance Seam(ISaveService save) => new PlayerAppearance(() => save, Refuse);

        private static CharacterRecipe Teal() => new CharacterRecipe { Outfit = "teal", Shirt = "white" };

        private static CharacterRecipe Ginny() => new CharacterRecipe
        {
            BuildId = "char_build.ginny",
            Skin = "tan",
            Outfit = "navy",
        };

        /// <summary>A real <see cref="SaveService"/> on the temp save, with a file already on disk, and
        /// a count of the writes the seam asked it for.</summary>
        private Counted OpenSave()
        {
            var go = new GameObject(nameof(PlayerAppearanceTests));
            _made.Add(go);
            var service = go.AddComponent<SaveService>();
            service.Open(_save);
            service.Save();
            return new Counted(service);
        }

        private sealed class Counted : ISaveService
        {
            private readonly SaveService _inner;
            public int Saves { get; private set; }
            public Counted(SaveService inner) { _inner = inner; }
            public SaveData Current => _inner.Current;
            public bool LoadedExistingSave => _inner.LoadedExistingSave;
            public bool GetFlag(string key) => _inner.GetFlag(key);
            public void SetFlag(string key, bool value) => _inner.SetFlag(key, value);
            public void Save() { Saves++; _inner.Save(); }
            public void BeginNewGame() => _inner.BeginNewGame();
        }
    }
}
