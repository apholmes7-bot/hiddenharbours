using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.UI.EditMode
{
    public class CharacterCreatorModelTests
    {
        private const string Body = "char_build.ginny";
        private const string Outfit = "outfit.harbour_teal";
        private CharacterBuilderDef _builder;
        private CharacterWardrobeDef _wardrobe;
        private CharacterCreatorModel _model;
        private GameObject _host;
        private SaveService _service;
        private CountingSave _save;
        private string _path;
        private string _previousPath;
        private FieldInfo _writeHook;
        private object _previousHook;
        private int _writes;
        private bool _refuse;
        private readonly List<string> _events = new List<string>();
        private readonly List<string> _outfitPreviews = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "hh_creator_" + Guid.NewGuid().ToString("N"), "fixture.json");
            _previousPath = Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable);
            Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable, _path);
            GameServices.Reset();
            ShellFlow.Reset();
            _builder = CharacterBuilderDef.LoadShipped();
            _wardrobe = Resources.Load<CharacterWardrobeDef>(CharacterWardrobeDef.ResourcesPath);
            Assert.IsNotNull(_builder);
            Assert.IsNotNull(_wardrobe);
            _host = new GameObject(nameof(CharacterCreatorModelTests));
            _host.SetActive(false);
            _service = _host.AddComponent<SaveService>();
            // Use the existing internal temp-path entry point without widening Core's friend list.
            typeof(SaveService).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_service, new object[] { _path });
            _service.Save();
            _save = new CountingSave(_service);
            GameServices.Save = _save;
            _refuse = false;
            GameServices.Appearance = new PlayerAppearance(() => _save, recipe => _refuse
                ? "Recipe refused: the selected body is unavailable."
                : CharacterBuilder.Refuse(_builder, recipe, CharacterAudience.Player));
            _writeHook = typeof(SaveStore).GetField("BeforeReplaceForTests", BindingFlags.Static | BindingFlags.NonPublic);
            _previousHook = _writeHook.GetValue(null);
            _writeHook.SetValue(null, (Action<string>)(path => { if (path == _path) _writes++; }));
            _writes = 0;
            _events.Clear();
            _outfitPreviews.Clear();
            EventBus.Subscribe<PlayerAppearanceChanged>(OnAppearance);
            EventBus.Subscribe<OutfitChanged>(OnOutfit);
            EventBus.Subscribe<OutfitPreviewRequested>(OnPreview);
            _model = new CharacterCreatorModel(_builder, _wardrobe);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<PlayerAppearanceChanged>(OnAppearance);
            EventBus.Unsubscribe<OutfitChanged>(OnOutfit);
            EventBus.Unsubscribe<OutfitPreviewRequested>(OnPreview);
            if (_writeHook != null) _writeHook.SetValue(null, _previousHook);
            if (_host != null) Object.DestroyImmediate(_host);
            GameServices.Reset();
            ShellFlow.Reset();
            Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable, _previousPath);
        }

        [Test]
        public void PreviewNeverWrites_AnyOfTheThreeChoices()
        {
            byte[] before = File.ReadAllBytes(_path);
            Choose();
            Assert.IsTrue(_model.SelectColour("hair", "black"));
            Assert.IsTrue(GameServices.Appearance.IsPreviewing);
            Assert.IsTrue(_save.Current.PlayerRecipe.IsEmpty);
            Assert.AreEqual("", _save.Current.WornOutfitId);
            Assert.AreEqual("", _save.Current.PlayerName);
            Assert.AreEqual(Outfit, _outfitPreviews[_outfitPreviews.Count - 1]);
            Assert.AreEqual(0, _save.Requests.Count);
            Assert.AreEqual(0, _writes);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_path));
        }

        [Test]
        public void CancelRestoresAllThree_WithoutWriting()
        {
            byte[] before = File.ReadAllBytes(_path);
            Choose();
            _model.Cancel();
            Assert.IsFalse(_model.IsOpen);
            Assert.IsFalse(GameServices.Appearance.IsPreviewing);
            Assert.IsTrue(_model.Recipe.IsEmpty);
            Assert.IsTrue(_save.Current.PlayerRecipe.IsEmpty);
            Assert.AreEqual("", _model.OutfitId);
            Assert.AreEqual("", _model.Name);
            Assert.AreEqual("", _save.Current.WornOutfitId);
            Assert.AreEqual("", _save.Current.PlayerName);
            Assert.AreEqual("", _outfitPreviews[_outfitPreviews.Count - 1]);
            Assert.AreEqual(0, _writes);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_path));
        }

        [Test]
        public void CommitWritesRecipeOutfitNameOnceInOrder_InOneFileWrite()
        {
            Choose();
            Assert.IsTrue(_model.Commit(), _model.Refusal);
            Assert.AreEqual(3, _save.Requests.Count);
            CollectionAssert.AreEqual(new[] { Body, "", "" }, _save.Requests[0]);
            CollectionAssert.AreEqual(new[] { Body, Outfit, "" }, _save.Requests[1]);
            CollectionAssert.AreEqual(new[] { Body, Outfit, "Skipper" }, _save.Requests[2]);
            CollectionAssert.AreEqual(new[] { "appearance", "outfit" }, _events);
            Assert.AreEqual(1, _writes);
            SaveData written = SaveStore.Read(_path);
            Assert.AreEqual(Body, written.PlayerRecipe.BuildId);
            Assert.AreEqual(Outfit, written.WornOutfitId);
            Assert.AreEqual("Skipper", written.PlayerName);
            Assert.IsFalse(_model.Commit(), "a second confirm must not write again");
            Assert.AreEqual(1, _writes);
        }

        [Test]
        public void RefusedRecipeWritesNone_AndShowsTheSeamRefusal()
        {
            Choose();
            _refuse = true;
            Assert.IsFalse(_model.Commit());
            StringAssert.Contains("selected body", _model.Refusal);
            Assert.IsTrue(_save.Current.PlayerRecipe.IsEmpty);
            Assert.AreEqual("", _save.Current.WornOutfitId);
            Assert.AreEqual("", _save.Current.PlayerName);
            Assert.AreEqual(0, _save.Requests.Count);
            Assert.AreEqual(0, _writes);
        }

        [Test]
        public void BlankNameSavesNone()
        {
            Assert.IsTrue(_model.SelectBody(Body));
            Assert.IsTrue(_model.SetName(" \t "));
            Assert.IsTrue(_model.Commit(), _model.Refusal);
            Assert.AreEqual("", SaveStore.Read(_path).PlayerName);
            Assert.AreEqual(1, _writes);
            Assert.AreEqual(1, _save.Requests.Count);
        }

        [Test]
        public void OverlongNameIsRefusedBeforeAnyWriter_AndItsReasonIsShown()
        {
            Assert.IsFalse(_model.SetName(new string('x', GameServices.PlayerNameMaxLength + 1)));
            Assert.IsNotEmpty(_model.Refusal);
            Choose();
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.PlayerNameMaxLength = 3;
                GameServices.Config = config;
                Assert.IsFalse(_model.Commit());
                StringAssert.Contains("3", _model.Refusal);
                Assert.AreEqual(0, _save.Requests.Count);
                Assert.AreEqual(0, _writes);
                Assert.IsTrue(_save.Current.PlayerRecipe.IsEmpty);
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void ResetRestoresOpeningChoices_WithoutWriting()
        {
            Choose();
            _model.Reset();
            Assert.IsTrue(_model.IsOpen);
            Assert.IsTrue(_model.Recipe.IsEmpty);
            Assert.AreEqual("", _model.OutfitId);
            Assert.AreEqual("", _model.Name);
            Assert.AreEqual(0, _writes);
        }

        [Test]
        public void UnchangedChoicesWriteNothing()
        {
            byte[] before = File.ReadAllBytes(_path);
            Assert.IsTrue(_model.Commit(), _model.Refusal);
            Assert.AreEqual(0, _save.Requests.Count);
            Assert.AreEqual(0, _writes);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_path));
        }

        [Test]
        public void ReopeningStartsFromCommittedLookOutfitAndName()
        {
            Choose();
            Assert.IsTrue(_model.Commit());
            var reopened = new CharacterCreatorModel(_builder, _wardrobe);
            Assert.AreEqual(Body, reopened.Recipe.BuildId);
            Assert.AreEqual(Outfit, reopened.OutfitId);
            Assert.AreEqual("Skipper", reopened.Name);
            reopened.Cancel();
            Assert.AreEqual("Skipper", _save.Current.PlayerName);
            Assert.AreEqual(1, _writes);
        }

        [Test]
        public void BodiesComeFromBuilder_ChildrenAreRefusedByName()
        {
            CollectionAssert.AreEquivalent(new[] { "char_build.fisher", "char_build.ginny", "char_build.skipper",
                "char_build.nan", "char_build.deckboss", "char_build.packer", "char_build.cutter", "char_build.hand" }, _model.Bodies);
            Assert.AreEqual(2, _model.UnavailableBodies.Count);
            Assert.IsFalse(_model.SelectBody("char_build.boy"));
            StringAssert.Contains("boy", _model.Refusal);
            Assert.IsFalse(_model.SelectBody("char_build.girl"));
            StringAssert.Contains("girl", _model.Refusal);
        }

        [Test]
        public void DrawnKeyboardTypesChangesCaseBackspacesAndFinishes()
        {
            _model.Focus(_model.NameRow);
            _model.Activate();
            _model.PressKey(0);
            _model.PressKey(38);
            _model.PressKey(0);
            _model.PressKey(36);
            _model.PressKey(37);
            _model.PressKey(39);
            Assert.AreEqual("aA", _model.Name);
            Assert.IsFalse(_model.NameEditing);
            Assert.AreEqual(0, _writes);
        }

        [Test]
        public void NavigationWaitsForRelease_AndMovesOncePerPush()
        {
            _model.Input(0f, -1f, false, false);
            Assert.AreEqual(0, _model.FocusRow);
            _model.Input(0f, 0f, false, false);
            _model.Input(0f, -1f, false, false);
            _model.Input(0f, -1f, false, false);
            Assert.AreEqual(1, _model.FocusRow);
            _model.Input(0f, 0f, false, false);
            _model.Input(0f, -1f, false, false);
            Assert.AreEqual(2, _model.FocusRow);
        }

        private void Choose()
        {
            Assert.IsTrue(_model.SelectBody(Body), _model.Refusal);
            Assert.IsTrue(_model.SelectOutfit(Outfit), _model.Refusal);
            Assert.IsTrue(_model.SetName("  Skipper  "), _model.Refusal);
        }

        private void OnAppearance(PlayerAppearanceChanged signal) => _events.Add("appearance");
        private void OnOutfit(OutfitChanged signal) => _events.Add("outfit");
        private void OnPreview(OutfitPreviewRequested signal) => _outfitPreviews.Add(signal.OutfitId);

        private sealed class CountingSave : ISaveService
        {
            private readonly SaveService _inner;
            public readonly List<string[]> Requests = new List<string[]>();
            public CountingSave(SaveService inner) { _inner = inner; }
            public SaveData Current => _inner.Current;
            public bool GetFlag(string key) => _inner.GetFlag(key);
            public void SetFlag(string key, bool value) => _inner.SetFlag(key, value);
            public IDisposable DeferWrites() => _inner.DeferWrites();
            public void Save()
            {
                Requests.Add(new[] { Current.PlayerRecipe.BuildId, Current.WornOutfitId, Current.PlayerName });
                _inner.Save();
            }
        }
    }
}
