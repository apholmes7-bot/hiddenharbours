using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;
using HiddenHarbours.UI;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Drive actual draw methods in EditMode. Lifecycle is invoked explicitly because these
    /// ordinary MonoBehaviours do not receive Awake/OnEnable when a fixture adds them outside Play.</summary>
    public class PlayerNamePresenterTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private readonly List<Component> _enabled = new List<Component>();
        private MemorySave _save;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
            ShellFlow.Reset();
            InteractActorProbe.Reset();
            _save = new MemorySave();
            GameServices.Save = _save;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _enabled.Count - 1; i >= 0; i--) Call(_enabled[i], "OnDisable");
            _enabled.Clear();
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            GameServices.Reset();
            ShellFlow.Reset();
            InteractActorProbe.Reset();
            InteractionGate.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
        }

        private T Make<T>(bool awake = true, bool enable = false) where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            _objects.Add(go);
            T component = go.AddComponent<T>();
            if (awake) Call(component, "Awake");
            if (enable)
            {
                Call(component, "OnEnable");
                _enabled.Add(component);
            }
            return component;
        }

        private T Def<T>() where T : ScriptableObject
        {
            T def = ScriptableObject.CreateInstance<T>();
            _objects.Add(def);
            return def;
        }

        private static object Call(object target, string method, params object[] args)
            => target.GetType().GetMethod(method, Private)?.Invoke(target, args);

        private static T Field<T>(object target, string name)
            => (T)target.GetType().GetField(name, Private | BindingFlags.Public).GetValue(target);

        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, Private | BindingFlags.Public).SetValue(target, value);

        private static string Drawn(Component presenter)
            => string.Join("\n", presenter.GetComponentsInChildren<Text>(true).Select(t => t.text));

        [TestCase("Rowan", "Rowan")]
        [TestCase("", "skipper")]
        public void DialogueDrawsResolvedBodyAndOptions(string name, string expected)
        {
            _save.Current.PlayerName = name;
            var presenter = Make<DialoguePresenter>();
            var lines = new[] { new DialogueLine("Neighbour", "Hello, {player}.") };
            var options = new[] { new DialogueOption { Id = "option.test", Label = "Ask {player}" } };
            presenter.Play(new DialogueRequest(lines, null, Vector3.zero, DialogueVoice.Default, options));
            presenter.Advance(); // Complete the current typewriter through the same action as a press.
            Assert.AreEqual("Hello, " + expected + ".", presenter.VisibleText);
            Assert.AreEqual("Hello, " + expected + ".", Field<Text>(presenter, "_bodyText").text);
            presenter.Advance();
            StringAssert.Contains("Ask " + expected, Drawn(presenter));
            presenter.Close();
        }

        [Test]
        public void RenameRedrawsStandingOptions_AndKeepsTheLineAlreadySaid()
        {
            _save.Current.PlayerName = "Rowan";
            var presenter = Make<DialoguePresenter>(enable: true);
            presenter.Play(new DialogueRequest(
                new[] { new DialogueLine("Neighbour", "Hello, {player}.") }, null, Vector3.zero,
                DialogueVoice.Default, new[] { new DialogueOption { Id = "option.test", Label = "Ask {player}" } }));
            presenter.Advance();
            presenter.Advance();
            Assert.IsTrue(PlayerName.Set("River", out string refusal), refusal);
            Assert.AreEqual("Hello, Rowan.", Field<Text>(presenter, "_bodyText").text);
            StringAssert.Contains("Ask River", Drawn(presenter));
            presenter.Close();
        }

        private AmbientSpeechPresenter Ambient() => Make<AmbientSpeechPresenter>(enable: true);

        private static string CompleteAmbient(AmbientSpeechPresenter presenter, int id)
        {
            foreach (object slot in Field<Array>(presenter, "_slots"))
                if (Field<int>(slot, "Id") == id)
                    Field<DialogueTypewriter>(slot, "Typewriter").CompleteNow();
            return presenter.VisibleTextOf(id);
        }

        [TestCase("Rowan", "Rowan", AmbientSpeechKind.NpcToNpc)]
        [TestCase("", "skipper", AmbientSpeechKind.NpcToNpc)]
        [TestCase("Rowan", "Rowan", AmbientSpeechKind.InnerVoice)]
        [TestCase("", "skipper", AmbientSpeechKind.InnerVoice)]
        public void AmbientAndInnerVoiceUseTheFinalDrawResolver(string name, string expected, AmbientSpeechKind kind)
        {
            _save.Current.PlayerName = name;
            var presenter = Ambient();
            int id = AmbientSpeechId.Next();
            EventBus.Publish(new AmbientSpeechRequested(id, null, Vector3.zero, "npc.test", "Neighbour",
                "Hello, {player}.", null, kind));
            Assert.AreEqual("Hello, " + expected + ".", CompleteAmbient(presenter, id));
            Assert.IsTrue(PlayerName.Set("River", out string refusal), refusal);
            Assert.AreEqual("Hello, " + expected + ".", presenter.VisibleTextOf(id));
        }

        [TestCase("Rowan", "Rowan")]
        [TestCase("", "skipper")]
        public void InnerVoiceDefReachesTheAmbientDraw(string name, string expected)
        {
            _save.Current.PlayerName = name;
            var ambient = Ambient();
            var director = Make<InnerVoiceDirector>(awake: false);
            var line = Def<InnerVoiceLineDef>();
            line.Id = "innervoice.test";
            line.Line = "Mind the tide, {player}.";
            line.OnceOnly = false;
            int id = 0;
            void Capture(AmbientSpeechRequested request) => id = request.Id;
            EventBus.Subscribe<AmbientSpeechRequested>(Capture);
            try
            {
                Assert.AreEqual(true, Call(director, "TrySpeak", line));
                Assert.AreNotEqual(0, id);
                Assert.AreEqual("Mind the tide, " + expected + ".", CompleteAmbient(ambient, id));
                Assert.AreEqual(0, _save.Saves);
            }
            finally { EventBus.Unsubscribe<AmbientSpeechRequested>(Capture); }
        }

        [TestCase("Rowan", "Rowan")]
        [TestCase("", "skipper")]
        public void ConversationDefReachesTheAmbientDraw(string name, string expected)
        {
            _save.Current.PlayerName = name;
            var ambient = Ambient();
            var director = Make<NpcConversationDirector>(awake: false);
            var speaker = Def<NpcDef>();
            speaker.Id = "npc.test";
            speaker.DisplayName = "Neighbour";
            var conversation = Def<ConversationDef>();
            conversation.Participants = new[] { speaker, speaker };
            conversation.Lines = new[] { new ConversationLine { SpeakerIndex = 0, Text = "There goes {player}." } };
            Set(director, "_running", conversation);
            Set(director, "_speakerA", Make<VillagerRoutine>(awake: false));
            Call(director, "SpeakNext");
            int id = Field<int>(director, "_pendingSpeechId");
            Assert.AreNotEqual(0, id);
            Assert.AreEqual("There goes " + expected + ".", CompleteAmbient(ambient, id));
        }

        private NotebookPresenter Book()
        {
            var book = Make<NotebookPresenter>(enable: true);
            var quest = Def<QuestDef>();
            quest.Title = "For {player}";
            quest.Context = "A word for {player}";
            quest.Steps = new List<QuestStep> { new QuestStep { Label = "Find {player}", Flag = "test.done" } };
            var page = Def<KnowledgePageDef>();
            page.TabId = "tab.test";
            page.TabLabel = "{player}";
            page.Head = "Dear {player}";
            page.Body = new List<string> { "Keep an eye out, {player}." };
            book.WireContent(() => new[] { quest }, () => new[] { page },
                () => new[] { new NotebookNote("I wrote {player} myself.", false, false) }, new InMemoryFlagStore());
            book.Open();
            return book;
        }

        [TestCase("Rowan", "Rowan")]
        [TestCase("", "skipper")]
        public void NotebookDrawsOwnershipAndEveryAuthoredField(string name, string expected)
        {
            _save.Current.PlayerName = name;
            var book = Book();
            Assert.AreEqual(expected + "'s notebook", book.Title);
            StringAssert.Contains(expected + "'s notebook", Drawn(book));
            StringAssert.Contains("For " + expected, Drawn(book));
            StringAssert.Contains("A word for " + expected, Drawn(book));
            StringAssert.Contains("Find " + expected, Drawn(book));
            book.SelectTab(2);
            StringAssert.Contains("Dear " + expected, Drawn(book));
            StringAssert.Contains("Keep an eye out, " + expected + ".", Drawn(book).Replace("\n", " "));
            Assert.AreEqual(expected, book.Tabs[2].Label);
            book.SelectTab(book.Tabs.Count - 1);
            StringAssert.Contains("I wrote {player} myself.", Drawn(book).Replace("\n", " "));
            Assert.AreEqual(0, _save.Saves);
            book.Close();
        }

        [Test]
        public void OpenNotebookRedrawsOnTheSavedNameEvent_AndLongNamesWrapAtTheKitWidth()
        {
            _save.Current.PlayerName = "Rowan";
            var book = Book();
            string longName = new string('x', GameServices.PlayerNameMaxLength);
            Assert.IsTrue(PlayerName.Set(longName, out string refusal), refusal);
            Assert.AreEqual(longName + "'s notebook", book.Title);
            NotebookBlock title = book.Tabs[0].Blocks[0];
            Assert.Greater(title.Rows.Count, 1);
            foreach (NotebookRow row in title.Rows)
                Assert.LessOrEqual(row.Text.Length, NotebookKit.TitleColsFor(book.Fit.Cols));
            Assert.AreEqual(1, _save.Saves);
            book.Close();
            Assert.IsTrue(PlayerName.Set("River", out refusal), refusal);
            book.Open();
            StringAssert.Contains("River's notebook", Drawn(book));
            book.Close();
        }

        [TestCase("Rowan", "Rowan")]
        [TestCase("", "skipper")]
        public void SignsResolveAtReadWithoutRewritingTheTable(string name, string expected)
        {
            _save.Current.PlayerName = name;
            var words = Def<WordsTableDef>();
            words.Entries = new[] { new WordsEntry { Id = "words.test", Text = "Welcome, {player}." } };
            Assert.IsTrue(words.TryGetText("words.test", out string text));
            Assert.AreEqual("Welcome, " + expected + ".", text);
            Assert.AreEqual("Welcome, {player}.", words.Entries[0].Text);
            Assert.IsFalse(words.TryGetText("words.absent", out text));
            Assert.IsNull(text);
        }

        [TestCase("Rowan", "Rowan")]
        [TestCase("", "skipper")]
        public void QuestNoteDrawsAndRedraws_WithoutAllocationOnUnchangedFrames(string name, string expected)
        {
            _save.Current.PlayerName = name;
            var note = Make<QuestPanelPresenter>(enable: true);
            note.Show("Find {player}.");
            Assert.AreEqual("Find " + expected + ".", note.Text);
            StringAssert.Contains("Find " + expected + ".", Drawn(note));
            for (int i = 0; i < 32; i++) note.Show("Find {player}.");
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) note.Show("Find {player}.");
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
            Assert.IsTrue(PlayerName.Set("River", out string refusal), refusal);
            Assert.AreEqual("Find River.", note.Text);
        }

        [TestCase("Rowan", "Rowan")]
        [TestCase("", "skipper")]
        public void InteractionPopupResolvesBeforeFitting_AndCachesTheQuietDraw(string name, string expected)
        {
            _save.Current.PlayerName = name;
            var popup = Make<InteractPopup>(enable: true);
            Call(popup, "OnOffer", new InteractOfferChanged(InteractOfferSource.Fixture, "test.fixture",
                "Find {player}", Vector2.zero, 0f));
            StringAssert.Contains("Find " + expected, Drawn(popup));
            var apply = (Action)Delegate.CreateDelegate(typeof(Action), popup,
                typeof(InteractPopup).GetMethod("Apply", Private));
            for (int i = 0; i < 32; i++) apply();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) apply();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
            Assert.IsTrue(PlayerName.Set("River", out string refusal), refusal);
            StringAssert.Contains("Find River", Drawn(popup));
        }

        [TestCase("Rowan", "Rowan")]
        [TestCase("", "skipper")]
        public void SellerBookResolvesBeforeUppercasingAndWrapping(string name, string expected)
        {
            _save.Current.PlayerName = name;
            var presenter = Make<CatalogBookPresenter>(enable: true);
            var model = new CatalogBook(12);
            model.SetRows(new[] { new BuyRow(null, null, "gear.test", "For {player}",
                "A gift for {player}.", "Held for {player}.",
                new BuyQuote(BuyRowKind.Gear, 1, false, true), CatalogSection.Gear) }, "Welcome {player}", 10);
            Set(presenter, "_book", model);
            Set(presenter, "_showing", true);
            Call(presenter, "Draw");
            string text = Drawn(presenter);
            StringAssert.Contains("Welcome " + expected, text);
            StringAssert.Contains(("For " + expected).ToUpperInvariant(), text);
            StringAssert.Contains("A gift for " + expected + ".", text);
            StringAssert.Contains("Held for " + expected + ".", text);
            Assert.IsTrue(PlayerName.Set("River", out string refusal), refusal);
            StringAssert.Contains("Welcome River", Drawn(presenter));
            presenter.Close();
        }

        [TestCase("Rowan", "Rowan")]
        [TestCase("", "skipper")]
        public void SellScreenDrawsResolvedSpeciesNamesAndRedrawsOnRename(string name, string expected)
        {
            _save.Current.PlayerName = name;
            var screen = Make<SellScreen>(awake: false, enable: true);
            Call(screen, "Build");
            var hold = new MemoryHold();
            hold.TryAdd(new CatchItem("fish.test", "For {player}", FishCategory.InshoreGroundfish, 1f, 1, 0f));
            Set(screen, "_hold", hold);
            Call(screen, "RebuildSpeciesList");
            Call(screen, "SelectSpecies", "fish.test");
            StringAssert.Contains("For " + expected, Drawn(screen));
            StringAssert.Contains("Sell all For " + expected, Drawn(screen));
            Assert.IsTrue(PlayerName.Set("River", out string refusal), refusal);
            StringAssert.Contains("Sell all For River", Drawn(screen));
        }

        [TestCase("Rowan", "Rowan")]
        [TestCase("", "skipper")]
        public void CatchCardAndToastResolveWhenDrawn(string name, string expected)
        {
            _save.Current.PlayerName = name;
            var hud = Make<HudController>(awake: false);
            var label = new GameObject("CatchLabel", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(hud.transform, false);
            Set(hud, "_catchCardLabel", label.GetComponent<Text>());
            Call(hud, "ShowCatchCard", new CatchItem("fish.test", "For {player}",
                FishCategory.InshoreGroundfish, 1f, 1, 0f));
            StringAssert.Contains("For " + expected, Drawn(hud));
            var toast = Make<DevToast>();
            toast.Show("Hello, {player}.");
            StringAssert.Contains("Hello, " + expected + ".", Drawn(toast));
        }

        [TestCase("Rowan", "Rowan")]
        [TestCase("", "skipper")]
        public void RegionArrivalCardResolvesAtDrawAndOnTheNameEvent(string name, string expected)
        {
            _save.Current.PlayerName = name;
            var overlay = Make<RegionFadeOverlay>(awake: false, enable: true);
            var label = new GameObject("ArrivalLabel", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(overlay.transform, false);
            Set(overlay, "_arrivalLabel", label.GetComponent<Text>());
            Set(overlay, "_arrivalTitle", "Welcome {player}");
            EventBus.Publish(new PlayerNameChanged("", name));
            Assert.AreEqual("Welcome " + expected, label.GetComponent<Text>().text);
            Assert.IsTrue(PlayerName.Set("River", out string refusal), refusal);
            Assert.AreEqual("Welcome River", label.GetComponent<Text>().text);
        }

        [TestCase("Rowan", "Rowan")]
        [TestCase("", "skipper")]
        public void WardrobeDrawsAuthoredOutfitLabelsAndRedrawsOnRename(string name, string expected)
        {
            _save.Current.PlayerName = name;
            var wardrobe = Def<CharacterWardrobeDef>();
            var outfit = Def<CharacterOutfitDef>();
            outfit.Id = "outfit.test";
            outfit.Label = "For {player}";
            wardrobe.Outfits = new[] { outfit };
            wardrobe.DefaultOutfitId = outfit.Id;
            var picker = Make<WardrobePicker>(enable: true);
            picker.Configure(wardrobe, null);
            Assert.IsTrue(picker.Open("fixture.test"));
            StringAssert.Contains("For " + expected, Drawn(picker));
            Assert.IsTrue(PlayerName.Set("River", out string refusal), refusal);
            StringAssert.Contains("For River", Drawn(picker));
        }

        private sealed class MemoryHold : IHold
        {
            private readonly List<CatchItem> _items = new List<CatchItem>();
            public int CapacityUnits => 10;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class MemorySave : ISaveService
        {
            public SaveData Current { get; } = SaveMigration.NewGame();
            public int Saves;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() => Saves++;
        }
    }
}
