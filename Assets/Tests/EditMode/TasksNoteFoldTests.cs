using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The TASKS note's mode and its press</b> (owner asks 2026-11-16: minimise it, then "hide it
    /// entirely"). The mode is Core's, so the HUD's input reader and the settings sheet (UI) and the note
    /// (World) share it without knowing each other; it is remembered as a setting (never the save) and
    /// only when the player changes it. The press (T, the pad's west button) cycles shown, chip, hidden;
    /// a click on the head folds and a click on the chip opens, and no click ever hides it. All of it is
    /// deaf while something else owns the controls.
    /// </summary>
    public sealed class TasksNoteFoldTests
    {
        private MemoryTasksNoteFoldStore _store;
        private readonly List<TasksNoteFoldChanged> _changes = new List<TasksNoteFoldChanged>();

        [SetUp]
        public void SetUp()
        {
            TasksNoteFold.Reset();
            _store = new MemoryTasksNoteFoldStore();
            TasksNoteFold.ConfigureStore(_store);
            _changes.Clear();
            EventBus.Subscribe<TasksNoteFoldChanged>(OnChanged);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<TasksNoteFoldChanged>(OnChanged);
            TasksNoteFold.Reset();
        }

        private void OnChanged(TasksNoteFoldChanged e) => _changes.Add(e);

        [Test]
        public void TheNote_StartsShown_WhenNothingWasEverStored()
        {
            Assert.AreEqual(TasksNoteMode.Shown, TasksNoteFold.Mode, "a first run shows the note");
            Assert.AreEqual(0, _store.Writes, "and asking writes nothing");
        }

        [Test]
        public void ThePressCycles_ShownChipHidden_AndRoundToShown()
        {
            Assert.AreEqual(TasksNoteMode.Chip, TasksNoteFold.Next(TasksNoteMode.Shown));
            Assert.AreEqual(TasksNoteMode.Hidden, TasksNoteFold.Next(TasksNoteMode.Chip));
            Assert.AreEqual(TasksNoteMode.Shown, TasksNoteFold.Next(TasksNoteMode.Hidden));
        }

        [Test]
        public void Cycling_RemembersEachChange_AndSaysSoOnTheBus()
        {
            TasksNoteFold.Cycle();
            Assert.AreEqual(TasksNoteMode.Chip, TasksNoteFold.Mode);
            TasksNoteFold.Cycle();
            Assert.AreEqual(TasksNoteMode.Hidden, TasksNoteFold.Mode);
            Assert.IsTrue(_store.TryRead(out TasksNoteMode stored) && stored == TasksNoteMode.Hidden,
                "remembered hidden");
            TasksNoteFold.Cycle();
            Assert.AreEqual(TasksNoteMode.Shown, TasksNoteFold.Mode);

            Assert.AreEqual(3, _store.Writes);
            Assert.AreEqual(3, _changes.Count);
            Assert.AreEqual(TasksNoteMode.Chip, _changes[0].Mode);
            Assert.AreEqual(TasksNoteMode.Hidden, _changes[1].Mode);
            Assert.AreEqual(TasksNoteMode.Shown, _changes[2].Mode);
        }

        [Test]
        public void SayingTheSameModeAgain_WritesNothing_AndRaisesNothing()
        {
            TasksNoteFold.SetMode(TasksNoteMode.Shown);
            Assert.AreEqual(0, _store.Writes);
            Assert.AreEqual(0, _changes.Count);
        }

        [TestCase(TasksNoteMode.Chip)]
        [TestCase(TasksNoteMode.Hidden)]
        public void AStoredMode_IsWhatTheNextSessionStartsWith(TasksNoteMode mode)
        {
            var remembered = new MemoryTasksNoteFoldStore();
            remembered.Write(mode);
            TasksNoteFold.ConfigureStore(remembered);

            Assert.AreEqual(mode, TasksNoteFold.Mode, "the player chose it last time; it starts that way");
        }

        [Test]
        public void AnUnknownStoredNumber_ReadsAsShown_SoTheNoteIsNeverLostToABadSetting()
        {
            Assert.AreEqual(TasksNoteMode.Shown, TasksNoteFold.Sanitize(0));
            Assert.AreEqual(TasksNoteMode.Chip, TasksNoteFold.Sanitize(1));
            Assert.AreEqual(TasksNoteMode.Hidden, TasksNoteFold.Sanitize(2));
            Assert.AreEqual(TasksNoteMode.Shown, TasksNoteFold.Sanitize(3));
            Assert.AreEqual(TasksNoteMode.Shown, TasksNoteFold.Sanitize(-1));

            var odd = new MemoryTasksNoteFoldStore();
            odd.Write((TasksNoteMode)7);
            TasksNoteFold.ConfigureStore(odd);
            Assert.AreEqual(TasksNoteMode.Shown, TasksNoteFold.Mode);
        }

        [Test]
        public void TheModeNumbers_AreThePinnedSettingsValues()
        {
            // Stored in the player's prefs as numbers: append-only, never renumbered.
            Assert.AreEqual(0, (int)TasksNoteMode.Shown);
            Assert.AreEqual(1, (int)TasksNoteMode.Chip);
            Assert.AreEqual(2, (int)TasksNoteMode.Hidden);
        }

        [Test]
        public void TheMode_LivesInTheSettings_NotTheSave()
        {
            StringAssert.StartsWith("hh.", GameSettings.TasksNoteModeKey, "a settings key, prefixed like the rest");
            Assert.IsNull(typeof(SaveData).GetField("TasksNoteMode"),
                "no save field: how a note stands is a choice about the screen, not the harbour");
            Assert.IsNull(typeof(SaveData).GetField("TasksNoteFolded"));
        }

        // ---- the press ---------------------------------------------------------------------------------

        private static readonly Rect Target = new Rect(1500f, 40f, 300f, 48f);

        private static bool Choose(in HudIntents intents, bool blocked, Rect target, TasksNoteMode current,
                                   out TasksNoteMode next)
            => TasksNoteFold.TryChoose(in intents, blocked, target, current, out next);

        [Test]
        public void TheBoundPress_StepsTheCycle_WhereverThePointerIs()
        {
            var press = new HudIntents(false, true, false, Vector2.zero);
            Assert.IsTrue(Choose(in press, false, Target, TasksNoteMode.Shown, out TasksNoteMode n1));
            Assert.AreEqual(TasksNoteMode.Chip, n1);
            Assert.IsTrue(Choose(in press, false, Target, TasksNoteMode.Chip, out TasksNoteMode n2));
            Assert.AreEqual(TasksNoteMode.Hidden, n2);
            Assert.IsTrue(Choose(in press, false, default, TasksNoteMode.Hidden, out TasksNoteMode n3),
                "hidden draws nothing to click, and the press still brings it back");
            Assert.AreEqual(TasksNoteMode.Shown, n3);
        }

        [Test]
        public void AClickOnTheHead_FoldsToTheChip_AndAClickOnTheChip_OpensIt()
        {
            var on = new HudIntents(false, false, true, Target.center);
            Assert.IsTrue(Choose(in on, false, Target, TasksNoteMode.Shown, out TasksNoteMode fromShown));
            Assert.AreEqual(TasksNoteMode.Chip, fromShown, "a click folds; it never hides");
            Assert.IsTrue(Choose(in on, false, Target, TasksNoteMode.Chip, out TasksNoteMode fromChip));
            Assert.AreEqual(TasksNoteMode.Shown, fromChip, "a click on the chip opens it, never hides it");
        }

        [Test]
        public void AClick_NeverReachesAHiddenNote()
        {
            var on = new HudIntents(false, false, true, Target.center);
            Assert.IsFalse(Choose(in on, false, default, TasksNoteMode.Hidden, out TasksNoteMode next),
                "hidden states no box");
            Assert.AreEqual(TasksNoteMode.Hidden, next);
            Assert.IsFalse(Choose(in on, false, Target, TasksNoteMode.Hidden, out _),
                "even a stale box does not let a click act on a hidden note");
        }

        [Test]
        public void AClick_ActsOnlyOnTheTarget()
        {
            var off = new HudIntents(false, false, true, new Vector2(10f, 10f));
            var hover = new HudIntents(false, false, false, Target.center);
            var on = new HudIntents(false, false, true, Target.center);

            Assert.IsFalse(Choose(in off, false, Target, TasksNoteMode.Shown, out _), "a click on the world is the world's");
            Assert.IsFalse(Choose(in hover, false, Target, TasksNoteMode.Shown, out _), "a pointer resting there is no click");
            Assert.IsFalse(Choose(in on, false, default, TasksNoteMode.Shown, out _), "no note drawn, nothing to click");
        }

        [Test]
        public void NothingChanges_WhileSomethingElseOwnsTheControls()
        {
            var press = new HudIntents(false, true, false, Vector2.zero);
            var click = new HudIntents(false, false, true, Target.center);
            Assert.IsFalse(Choose(in press, true, Target, TasksNoteMode.Shown, out _),
                "a conversation, the notebook, the chooser or the creator has the buttons");
            Assert.IsFalse(Choose(in click, true, Target, TasksNoteMode.Shown, out _));
        }

        [Test]
        public void TheHideAll_IsNotTheTasksPress()
        {
            var hideAll = new HudIntents(true);
            Assert.IsFalse(hideAll.ToggleTasks);
            Assert.IsFalse(Choose(in hideAll, false, Target, TasksNoteMode.Shown, out _));
        }

        [Test]
        public void TheClickTarget_IsWhatTheNoteStates_AndEmptyIsNone()
        {
            TasksNoteFold.SetClickTarget(Target);
            Assert.AreEqual(Target, TasksNoteFold.ClickTarget);
            TasksNoteFold.SetClickTarget(new Rect(5f, 5f, 0f, 10f));
            Assert.AreEqual(default(Rect), TasksNoteFold.ClickTarget, "a box that covers nothing is no target");
        }

        [Test]
        public void TheScriptedSource_PressesAndClicksAsEdges()
        {
            var held = new HeldHudIntents();
            held.PressToggleTasks();
            Assert.IsTrue(held.Read().ToggleTasks);
            held.Release();
            Assert.IsFalse(held.Read().ToggleTasks);

            held.ClickAt(Target.center);
            HudIntents read = held.Read();
            Assert.IsTrue(read.Click);
            Assert.AreEqual(Target.center, read.Point);
        }

        // ---- the settings sheet's row --------------------------------------------------------------

        [Test]
        public void TheSettingsRow_NamesEachMode()
        {
            Assert.AreEqual("Tasks note", ShellStrings.TasksNoteLabel);
            Assert.AreEqual("Shown", ShellStrings.TasksNoteModeWord(TasksNoteMode.Shown));
            Assert.AreEqual("Chip", ShellStrings.TasksNoteModeWord(TasksNoteMode.Chip));
            Assert.AreEqual("Hidden", ShellStrings.TasksNoteModeWord(TasksNoteMode.Hidden));
        }
    }
}
