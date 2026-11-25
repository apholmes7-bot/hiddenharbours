using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>The TASKS note folds</b> (owner ask 2026-11-16: "there should be a way to minimize hide the
    /// current tasks ui"). Folded, the note is its own head, a chip in the same corner at the same scale
    /// that still counts the current tasks; a new or finished task flashes the chip and never opens the
    /// note by itself; the open note's head and the folded chip are what a click toggles. Hidden (owner,
    /// same day: "hide it entirely"), nothing is drawn, nothing flashes, and no box is stated.
    /// </summary>
    public sealed class QuestPanelFoldTests
    {
        private const int W = 1920, H = 1080;
        private GameObject _go;
        private MemoryTasksNoteFoldStore _store;

        [SetUp]
        public void SetUp()
        {
            TasksNoteFold.Reset();
            _store = new MemoryTasksNoteFoldStore();
            TasksNoteFold.ConfigureStore(_store);
            QuestNoteFootprint.Reset();
            HelmFootprint.Reset();
            HudScreen.Override(new Vector2Int(W, H));
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            TasksNoteFold.Reset();
            QuestNoteFootprint.Reset();
            HelmFootprint.Reset();
            HudScreen.Reset();
        }

        private QuestPanelPresenter Presenter()
        {
            _go = new GameObject("QuestPanelFoldTests");
            return _go.AddComponent<QuestPanelPresenter>();
        }

        // ---- geometry --------------------------------------------------------------------------------

        [Test]
        public void TheChip_SitsInTheNotesOwnCorner_AtTheNotesOwnScale()
        {
            QuestPanelFit fit = QuestPanelLayout.Fit(1, W, H);
            Rect note = QuestPanelLayout.NoteScreenRect(in fit, W, H);
            Rect chip = QuestPanelLayout.ChipScreenRect(fit.Scale, default, W, H);

            Assert.AreEqual(note.xMax, chip.xMax, 0.01f, "folding never moves the corner: same right edge");
            Assert.AreEqual(note.yMin, chip.yMin, 0.01f, "...and the same bottom edge");
            Assert.AreEqual(QuestPanelLayout.ChipWidthPx * fit.Scale, chip.width, 0.01f);
            Assert.AreEqual(NotebookKit.HeadHeight * fit.Scale, chip.height, 0.01f,
                "the chip is the note folded to its head");
            Assert.Less(chip.width, note.width, "a chip, smaller than the note it folds");
            Assert.Less(chip.height, note.height);
        }

        [Test]
        public void TheChipsAnchoredPosition_PutsItWhereItsScreenRectSays()
        {
            QuestPanelFit fit = QuestPanelLayout.Fit(2, W, H);
            Rect chip = QuestPanelLayout.ChipScreenRect(fit.Scale, default, W, H);
            Vector2 a = QuestPanelLayout.ChipAnchoredPosition(fit.Scale, default, W, H);

            // Anchored and pivoted lower-right: the rect's lower-right corner, relative to the screen's.
            Assert.AreEqual(chip.xMax - W, a.x, 0.01f);
            Assert.AreEqual(chip.yMin, a.y, 0.01f);
            Assert.AreEqual(QuestPanelLayout.AnchoredPosition(in fit), a,
                "with nothing covered, exactly the note's own corner position");
        }

        [Test]
        public void TheChip_RisesClearOfTheHelmBand_LikeTheNote()
        {
            const float bandH = 120f;
            HelmFootprintArea band = HelmFootprintArea.FullWidthBand(bandH);
            QuestPanelFit fit = QuestPanelLayout.Fit(1, W, H);

            Rect chip = QuestPanelLayout.ChipScreenRect(fit.Scale, in band, W, H);
            Assert.GreaterOrEqual(chip.yMin, bandH, "the chip never sits on the helm's band");
            Assert.AreEqual(QuestPanelLayout.NoteScreenRect(in fit, in band, W, H).yMin, chip.yMin, 0.01f,
                "it rises by the note's own rule, so the corner stays the corner");
        }

        [Test]
        public void TheHead_IsTheTopStripOfTheNote_NotTheWholeLeaf()
        {
            QuestPanelFit fit = QuestPanelLayout.Fit(3, W, H);
            Rect note = QuestPanelLayout.NoteScreenRect(in fit, default, W, H);
            Rect head = QuestPanelLayout.HeadScreenRect(in fit, default, W, H);

            Assert.AreEqual(note.x, head.x, 0.01f);
            Assert.AreEqual(note.width, head.width, 0.01f);
            Assert.AreEqual(note.yMax, head.yMax, 0.01f, "the head is at the TOP of the leaf");
            Assert.AreEqual(NotebookKit.HeadHeight * fit.Scale, head.height, 0.01f);
            Assert.IsFalse(head.Contains(new Vector2(note.center.x, note.yMin + 1f)),
                "a click on the step's words is not a click on the head");
        }

        [Test]
        public void TheChip_HoldsItsLabel_TheCount_AndTheOpenMark()
        {
            string label = NotebookContent.LabelQuests + " 1";
            Assert.LessOrEqual(label.Length + 2, QuestPanelLayout.ChipCols,
                "'TASKS 1', a space, and the open mark fit the chip's columns");
            Assert.AreEqual(1, WorldStrings.TasksFoldMark.Length);
            Assert.AreEqual(1, WorldStrings.TasksUnfoldMark.Length);
        }

        // ---- the signal --------------------------------------------------------------------------------

        [Test]
        public void ASignal_IsCalledOnlyAsAChip_AndOnlyForARealChange()
        {
            const TasksNoteMode chip = TasksNoteMode.Chip;
            Assert.IsTrue(QuestChipSignal.Calls(chip, null, "Dig clams."), "a task added");
            Assert.IsTrue(QuestChipSignal.Calls(chip, "Dig clams.", null), "a task finished");
            Assert.IsTrue(QuestChipSignal.Calls(chip, "Dig clams.", "Buy a rod."), "one step to the next");
            Assert.IsFalse(QuestChipSignal.Calls(chip, "Dig clams.", "Dig clams."), "a re-statement");
            Assert.IsFalse(QuestChipSignal.Calls(chip, null, ""), "nothing to nothing");
            Assert.IsFalse(QuestChipSignal.Calls(TasksNoteMode.Shown, "Dig clams.", "Buy a rod."),
                "open, the note itself shows the change");
            Assert.IsFalse(QuestChipSignal.Calls(TasksNoteMode.Hidden, "Dig clams.", "Buy a rod."),
                "hidden, a task change is silent");
            Assert.IsFalse(QuestChipSignal.Calls(TasksNoteMode.Hidden, null, "Dig clams."));
        }

        [Test]
        public void ASignal_RunsForItsDuration_ThenStops()
        {
            var s = new QuestChipSignal();
            Assert.IsFalse(s.Active, "no flash until one is started");

            s.Start(1f);
            Assert.IsTrue(s.Active);
            s.Tick(0.6f);
            Assert.IsTrue(s.Active);
            s.Tick(0.6f);
            Assert.IsFalse(s.Active, "over after its seconds");

            s.Start(1f);
            s.Stop();
            Assert.IsFalse(s.Active, "opening the note stops it at once");
        }

        [Test]
        public void TheFlash_BeatsOnAndOff_StartingOn()
        {
            const float beat = 0.3f;
            Assert.IsTrue(QuestPanelLayout.SignalLit(0f, beat), "the frame the task changes, the chip answers");
            Assert.IsTrue(QuestPanelLayout.SignalLit(0.29f, beat));
            Assert.IsFalse(QuestPanelLayout.SignalLit(0.31f, beat));
            Assert.IsTrue(QuestPanelLayout.SignalLit(0.61f, beat));
            Assert.IsTrue(QuestPanelLayout.SignalLit(5f, 0f), "a zero beat is steady, never a divide by zero");
        }

        [Test]
        public void TheSignalDefaults_AreTheConfigsOwn()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.AreEqual(GameConfig.DefaultTasksChipSignalSeconds, config.TasksChipSignalSeconds);
                Assert.AreEqual(GameConfig.DefaultTasksChipBlinkSeconds, config.TasksChipBlinkSeconds);
                Assert.Greater(config.TasksChipSignalSeconds, config.TasksChipBlinkSeconds,
                    "a signal long enough to flash more than once");
            }
            finally { Object.DestroyImmediate(config); }
        }

        // ---- the presenter -----------------------------------------------------------------------------

        [Test]
        public void Open_TheNoteShows_AndItsHeadIsTheClickTarget()
        {
            QuestPanelPresenter p = Presenter();
            p.Show("Dig clams.");

            Assert.IsTrue(p.IsNoteShowing);
            Assert.IsFalse(p.IsChipShowing);
            QuestPanelFit fit = p.Fit;
            Assert.AreEqual(QuestPanelLayout.HeadScreenRect(in fit, default, W, H), TasksNoteFold.ClickTarget);
            Assert.AreEqual(QuestPanelLayout.NoteScreenRect(in fit, default, W, H), QuestNoteFootprint.Current);
        }

        [Test]
        public void Folded_TheChipShows_StillCountingTheTask_AndItIsTheClickTarget()
        {
            QuestPanelPresenter p = Presenter();
            p.Show("Dig clams.");

            TasksNoteFold.SetMode(TasksNoteMode.Chip);
            p.SyncFold();

            Assert.IsTrue(p.IsChipShowing, "folded to its chip");
            Assert.IsFalse(p.IsNoteShowing);
            Assert.IsTrue(p.IsShowing, "the task is still current");
            Assert.AreEqual(1, p.TaskCount, "and the chip still counts it");
            Rect chip = QuestPanelLayout.ChipScreenRect(p.Fit.Scale, default, W, H);
            Assert.AreEqual(chip, TasksNoteFold.ClickTarget, "a click on the chip opens it again");
            Assert.AreEqual(chip, QuestNoteFootprint.Current,
                "the chip states its box, so the interaction prompt keeps off it too");
        }

        [Test]
        public void ReOpened_TheNoteIsBack_AsItWas()
        {
            QuestPanelPresenter p = Presenter();
            p.Show("Dig clams.");
            QuestPanelFit before = p.Fit;

            TasksNoteFold.SetMode(TasksNoteMode.Chip);
            p.SyncFold();
            TasksNoteFold.SetMode(TasksNoteMode.Shown);
            p.SyncFold();

            Assert.IsTrue(p.IsNoteShowing);
            Assert.AreEqual("Dig clams.", p.Text);
            Assert.AreEqual(before.Scale, p.Fit.Scale);
            Assert.AreEqual(before.Lines, p.Fit.Lines);
            Assert.AreEqual(QuestPanelLayout.NoteScreenRect(in before, default, W, H), QuestNoteFootprint.Current);
        }

        [Test]
        public void ATaskChangingWhileFolded_FlashesTheChip_AndNeverOpensTheNote()
        {
            QuestPanelPresenter p = Presenter();
            p.Show("Dig clams.");
            TasksNoteFold.SetMode(TasksNoteMode.Chip);
            p.SyncFold();
            Assert.IsFalse(p.Signal.Active, "folding is not a change of task");

            p.Show("Buy a fishing ROD from the shop.");

            Assert.IsTrue(p.Signal.Active, "the step moved on: the chip flashes");
            Assert.IsTrue(p.IsChipShowing, "and stays a chip: the note never opens itself");
            Assert.AreEqual(TasksNoteMode.Chip, TasksNoteFold.Mode);
        }

        [Test]
        public void TheLastTaskFinishingWhileFolded_KeepsTheChipForItsFlash()
        {
            QuestPanelPresenter p = Presenter();
            p.Show("Sail your dory home.");
            TasksNoteFold.SetMode(TasksNoteMode.Chip);
            p.SyncFold();

            p.Hide();

            Assert.IsTrue(p.Signal.Active, "finished: the chip flashes");
            Assert.IsTrue(p.IsChipShowing, "it stays on screen for the flash, counting nothing");
            Assert.AreEqual(0, p.TaskCount);
        }

        [Test]
        public void ATaskChangingWhileOpen_DoesNotFlash()
        {
            QuestPanelPresenter p = Presenter();
            p.Show("Dig clams.");
            p.Show("Buy a fishing ROD from the shop.");

            Assert.IsFalse(p.Signal.Active, "open, the note itself shows the change");
            Assert.IsTrue(p.IsNoteShowing);
        }

        [Test]
        public void OpeningTheNote_StopsAFlash()
        {
            QuestPanelPresenter p = Presenter();
            p.Show("Dig clams.");
            TasksNoteFold.SetMode(TasksNoteMode.Chip);
            p.SyncFold();
            p.Show("Buy a fishing ROD from the shop.");

            TasksNoteFold.SetMode(TasksNoteMode.Shown);
            p.SyncFold();

            Assert.IsFalse(p.Signal.Active);
            Assert.IsTrue(p.IsNoteShowing);
        }

        // ---- hidden ------------------------------------------------------------------------------------

        [Test]
        public void Hidden_DrawsNothing_AndStatesNoBox()
        {
            QuestPanelPresenter p = Presenter();
            p.Show("Dig clams.");

            TasksNoteFold.SetMode(TasksNoteMode.Hidden);
            p.SyncFold();

            Assert.IsFalse(p.IsDrawing, "no note, no chip: nothing at all");
            Assert.IsFalse(p.IsNoteShowing);
            Assert.IsFalse(p.IsChipShowing);
            Assert.IsTrue(p.IsShowing, "the task is still current; it is the Notebook's TASKS page that reads it");
            Assert.IsTrue(QuestNoteFootprint.IsNone, "no box for the interaction prompt to keep off");
            Assert.IsTrue(QuestNoteFootprint.IsEmpty(TasksNoteFold.ClickTarget), "and nothing for a click to land on");
            Assert.IsNull(_go.GetComponentInChildren<Canvas>(), "the note's canvas is off");
        }

        [Test]
        public void Hidden_ATaskChange_IsSilent()
        {
            QuestPanelPresenter p = Presenter();
            p.Show("Dig clams.");
            TasksNoteFold.SetMode(TasksNoteMode.Hidden);
            p.SyncFold();

            p.Show("Buy a fishing ROD from the shop.");
            Assert.IsFalse(p.Signal.Active, "no flash");
            Assert.IsFalse(p.IsDrawing, "no chip springs up");

            p.Hide();
            Assert.IsFalse(p.Signal.Active);
            Assert.IsFalse(p.IsDrawing);

            p.Show("Sail your dory home.");
            Assert.IsFalse(p.IsDrawing, "a new task arriving does not bring it back either");
            Assert.IsTrue(QuestNoteFootprint.IsNone);
        }

        [Test]
        public void HidingAFlashingChip_StopsTheFlash_AndDrawsNothing()
        {
            QuestPanelPresenter p = Presenter();
            p.Show("Dig clams.");
            TasksNoteFold.SetMode(TasksNoteMode.Chip);
            p.SyncFold();
            p.Show("Buy a fishing ROD from the shop.");
            Assert.IsTrue(p.Signal.Active);

            TasksNoteFold.SetMode(TasksNoteMode.Hidden);
            p.SyncFold();

            Assert.IsFalse(p.Signal.Active);
            Assert.IsFalse(p.IsDrawing);
            Assert.IsTrue(QuestNoteFootprint.IsNone);
        }

        [Test]
        public void TheFullCycle_ShownChipHiddenShown_DrawsEachInTurn()
        {
            QuestPanelPresenter p = Presenter();
            p.Show("Dig clams.");
            QuestPanelFit fit = p.Fit;
            Assert.IsTrue(p.IsNoteShowing);

            TasksNoteFold.Cycle();
            p.SyncFold();
            Assert.IsTrue(p.IsChipShowing);

            TasksNoteFold.Cycle();
            p.SyncFold();
            Assert.IsFalse(p.IsDrawing);

            TasksNoteFold.Cycle();
            p.SyncFold();
            Assert.IsTrue(p.IsNoteShowing, "round to shown: the note, as it was");
            Assert.AreEqual(QuestPanelLayout.NoteScreenRect(in fit, default, W, H), QuestNoteFootprint.Current);
            Assert.AreEqual(QuestPanelLayout.HeadScreenRect(in fit, default, W, H), TasksNoteFold.ClickTarget);
        }

        [Test]
        public void StartingHidden_FromTheSettings_NeverDrawsTheNote()
        {
            var remembered = new MemoryTasksNoteFoldStore();
            remembered.Write(TasksNoteMode.Hidden);
            TasksNoteFold.ConfigureStore(remembered);

            QuestPanelPresenter p = Presenter();
            p.Show("Dig clams.");

            Assert.IsFalse(p.IsDrawing, "the player hid it last session; it stays hidden");
            Assert.IsTrue(QuestNoteFootprint.IsNone);
        }

        [Test]
        public void NoTask_NothingDrawn_NothingToClick()
        {
            QuestPanelPresenter p = Presenter();
            p.Show("Dig clams.");
            p.Hide();

            Assert.IsFalse(p.IsNoteShowing);
            Assert.IsFalse(p.IsChipShowing);
            Assert.IsTrue(QuestNoteFootprint.IsEmpty(TasksNoteFold.ClickTarget));
            Assert.IsTrue(QuestNoteFootprint.IsNone);
        }
    }
}
