using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// How the TASKS note stands on the HUD. The numbers are what the player's settings store, so they
    /// are append-only: never renumber one.
    /// </summary>
    public enum TasksNoteMode
    {
        /// <summary>The note, open in its corner: the step written out.</summary>
        Shown = 0,

        /// <summary>Folded down to its head: a chip reading "TASKS 1" that flashes on a change.</summary>
        Chip = 1,

        /// <summary>Put away entirely: nothing drawn, no flash, no box. The Notebook's TASKS page is
        /// where the tasks are read.</summary>
        Hidden = 2,
    }

    /// <summary>Raised when the player changes how the TASKS note stands. Never raised for a
    /// re-statement of the same mode.</summary>
    public readonly struct TasksNoteFoldChanged
    {
        public readonly TasksNoteMode Mode;

        public TasksNoteFoldChanged(TasksNoteMode mode)
        {
            Mode = mode;
        }
    }

    /// <summary>Where the TASKS note's mode is remembered. The shipped store is the player's settings
    /// (<see cref="GameSettings"/>, PlayerPrefs); a test hands in <see cref="MemoryTasksNoteFoldStore"/>
    /// so it never writes the machine's prefs.</summary>
    public interface ITasksNoteFoldStore
    {
        /// <summary>The stored mode; false (and <see cref="TasksNoteMode.Shown"/>) when nothing was ever
        /// stored.</summary>
        bool TryRead(out TasksNoteMode mode);

        void Write(TasksNoteMode mode);
    }

    /// <summary>The player's settings, as the mode's store.</summary>
    public sealed class SettingsTasksNoteFoldStore : ITasksNoteFoldStore
    {
        public bool TryRead(out TasksNoteMode mode) => GameSettings.TryReadTasksNoteMode(out mode);

        public void Write(TasksNoteMode mode) => GameSettings.StoreTasksNoteMode(mode);
    }

    /// <summary>A store that lives for the run only: for tests and headless rigs.</summary>
    public sealed class MemoryTasksNoteFoldStore : ITasksNoteFoldStore
    {
        private bool _has;
        private TasksNoteMode _mode;

        public int Writes { get; private set; }

        public bool TryRead(out TasksNoteMode mode)
        {
            mode = _mode;
            return _has;
        }

        public void Write(TasksNoteMode mode)
        {
            _has = true;
            _mode = mode;
            Writes++;
        }
    }

    /// <summary>
    /// <b>How the TASKS note stands: shown, folded to its chip, or hidden</b> (owner asks 2026-11-16:
    /// "there should be a way to minimize hide the current tasks ui", then "i would like that you can
    /// hide it entirely").
    ///
    /// <para>The note is World's (<c>QuestPanelPresenter</c>) and the press is read by the HUD's one
    /// reader in UI (<c>BoatUiWindowInput</c>, the <c>Hud</c> map, ADR 0043); the settings sheet (UI)
    /// sets it too. None may reference another (rule 4), so the mode lives here: the reader and the
    /// sheet set it, the note reads it, and the note states the box a click must land in
    /// (<see cref="SetClickTarget"/>) the same way it states its footprint
    /// (<see cref="QuestNoteFootprint"/>).</para>
    ///
    /// <para><b>The press cycles, a click only folds and opens.</b> T or the pad's west button goes
    /// shown, chip, hidden, shown. A click on the open note's head folds it to the chip and a click on
    /// the chip opens it; hidden draws nothing, so there is nothing to click, and a mouse-only player
    /// can never put it away by accident. The key and the settings sheet bring it back.</para>
    ///
    /// <para><b>Remembered as a setting, never in the save.</b> How a note stands is a choice about the
    /// screen, like the volume, so it survives New Game and quitting through the player's settings
    /// (<see cref="GameSettings.TasksNoteModeKey"/>). It is read once, lazily; it is written only when
    /// the player changes it.</para>
    /// </summary>
    public static class TasksNoteFold
    {
        private static ITasksNoteFoldStore _store;
        private static bool _loaded;
        private static TasksNoteMode _mode;

        /// <summary>The box (screen pixels, bottom-left origin) a pointer press must land in to fold or
        /// open the note: the open note's head, or the chip. Zero-sized when nothing is drawn (hidden, or
        /// no current task).</summary>
        public static Rect ClickTarget { get; private set; }

        /// <summary>How the note stands now.</summary>
        public static TasksNoteMode Mode
        {
            get
            {
                EnsureLoaded();
                return _mode;
            }
        }

        /// <summary>Set how the note stands. Remembered, and announced on the bus, only on a change.</summary>
        public static void SetMode(TasksNoteMode mode)
        {
            mode = Sanitize((int)mode);
            EnsureLoaded();
            if (_mode == mode) return;
            _mode = mode;
            Store.Write(mode);
            EventBus.Publish(new TasksNoteFoldChanged(mode));
        }

        /// <summary>The press: shown, chip, hidden, and round to shown.</summary>
        public static void Cycle() => SetMode(Next(Mode));

        /// <summary>The mode after <paramref name="mode"/> in the press's cycle. Pure.</summary>
        public static TasksNoteMode Next(TasksNoteMode mode)
        {
            switch (mode)
            {
                case TasksNoteMode.Shown: return TasksNoteMode.Chip;
                case TasksNoteMode.Chip: return TasksNoteMode.Hidden;
                default: return TasksNoteMode.Shown;
            }
        }

        /// <summary>A stored number as a mode; anything unknown (a hand-edited pref, a later build's
        /// value) reads as shown, so the note is never lost to a bad setting. Pure.</summary>
        public static TasksNoteMode Sanitize(int raw)
        {
            return raw == (int)TasksNoteMode.Chip || raw == (int)TasksNoteMode.Hidden
                ? (TasksNoteMode)raw
                : TasksNoteMode.Shown;
        }

        /// <summary>The note states where a click folds or opens it. Zero-sized = nothing to click.</summary>
        public static void SetClickTarget(Rect rectPx)
        {
            ClickTarget = QuestNoteFootprint.IsEmpty(rectPx) ? default : rectPx;
        }

        /// <summary>
        /// Does this frame of HUD intents change how the note stands, and to what? The bound press always
        /// does: the next mode in the cycle. A click does only when it lands on
        /// <paramref name="targetPx"/>: the open note's head folds it to the chip, the chip opens it. A
        /// click never hides the note and never reaches a hidden one. Nothing does while something else
        /// owns the controls (<paramref name="interactionBlocked"/>: a conversation, the notebook, the
        /// chooser, the character creator, a boarding move), so a pad's X in the creator stays the
        /// creator's. Pure.
        /// </summary>
        public static bool TryChoose(in HudIntents intents, bool interactionBlocked, Rect targetPx,
                                     TasksNoteMode current, out TasksNoteMode next)
        {
            next = current;
            if (interactionBlocked) return false;
            if (intents.ToggleTasks)
            {
                next = Next(current);
                return true;
            }

            if (!intents.Click || QuestNoteFootprint.IsEmpty(targetPx) || !targetPx.Contains(intents.Point))
                return false;
            if (current == TasksNoteMode.Shown) next = TasksNoteMode.Chip;
            else if (current == TasksNoteMode.Chip) next = TasksNoteMode.Shown;
            else return false;
            return true;
        }

        /// <summary>Hand in another store (tests); null restores the player's settings. Forgets what was
        /// read, so the next ask reads the new store.</summary>
        public static void ConfigureStore(ITasksNoteFoldStore store)
        {
            _store = store;
            _loaded = false;
            _mode = TasksNoteMode.Shown;
        }

        /// <summary>Back to "not read yet, nothing to click", with no event: domain reload and tests.</summary>
        public static void Reset()
        {
            _store = null;
            _loaded = false;
            _mode = TasksNoteMode.Shown;
            ClickTarget = default;
        }

        private static ITasksNoteFoldStore Store => _store ??= DefaultStore();

        /// <summary>
        /// The player's settings, except where a test runs: in the editor outside play mode (EditMode
        /// tests) and under a PlayMode test run (<see cref="SaveStore.TestRunSaveVariable"/>, the same
        /// signal that gives a run its own save) the mode lives for the run only, so no test reads a mode
        /// someone left in the editor's prefs, and none writes one.
        /// </summary>
        private static ITasksNoteFoldStore DefaultStore()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying
                || !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable)))
                return new MemoryTasksNoteFoldStore();
#endif
            return new SettingsTasksNoteFoldStore();
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _mode = Store.TryRead(out TasksNoteMode mode) ? Sanitize((int)mode) : TasksNoteMode.Shown;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
