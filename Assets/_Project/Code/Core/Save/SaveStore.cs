using System.IO;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The on-disk side of saving: where the file lives and how it is written/read safely. Kept separate
    /// from <see cref="SaveSerialization"/> (format) and <see cref="SaveService"/> (lifetime) so each is
    /// independently testable — tests write/read a temp path without a running game.
    ///
    /// <para><b>Atomic writes</b> (tech-architecture.md §6): we write to a sibling <c>.tmp</c> then swap
    /// it into place, so a process killed mid-write can never leave a half-written, corrupt save — the
    /// previous good file survives until the new one is complete.</para>
    /// </summary>
    public static class SaveStore
    {
#if UNITY_EDITOR
        // Test-only fault at the replacement boundary, after the temp file is complete.
        // A test must filter its own destination path and restore this hook in finally/teardown.
        // No override of a successful write and no player-build persistence behavior change.
        internal static System.Action<string> BeforeReplaceForTests;
#endif
        /// <summary>Default save file name under <see cref="Application.persistentDataPath"/>.</summary>
        public const string DefaultFileName = "savegame.json";

        /// <summary>The default save path for the running game.</summary>
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, DefaultFileName);

        /// <summary>
        /// The process environment variable that names a test run's own save. The PlayMode tests' run hook
        /// sets it in the editor before the runner enters play mode and clears it when the run leaves; only
        /// the editor ever reads it (<see cref="ActivePath"/>).
        /// </summary>
        public const string TestRunSaveVariable = "HH_TEST_RUN_SAVE";

        /// <summary>
        /// The save the running game opens: <see cref="DefaultPath"/>, except under the editor's test runner,
        /// which gives each PlayMode run a save of its own (<see cref="TestRunSaveVariable"/>), so no test reads
        /// or writes the player's.
        ///
        /// <para><b>Why the environment.</b> <see cref="SaveService"/> reads its file in Awake, at
        /// BeforeSceneLoad, before any test or run callback exists, and entering play mode reloads the
        /// scripting domain, so no static set beforehand survives to that read. The process environment does:
        /// the runner's setup sets it in the editor domain and the play session reads it here. A built player
        /// compiles the branch out and opens <see cref="DefaultPath"/>, as does the editor whenever no run has
        /// set one.</para>
        /// </summary>
        public static string ActivePath
        {
            get
            {
#if UNITY_EDITOR
                string testRun = System.Environment.GetEnvironmentVariable(TestRunSaveVariable);
                if (!string.IsNullOrEmpty(testRun)) return testRun;
#endif
                return DefaultPath;
            }
        }

        /// <summary>
        /// How many replaced saves are kept beside the save (<see cref="KeepReplaced"/>). Three, so a game a
        /// mistaken New Game erased survives two more New Games before it is dropped. Each copy is one save
        /// file, a few KB.
        /// </summary>
        public const int KeptGameCount = 3;

        /// <summary>
        /// Kept copy <paramref name="n"/> of the save at <paramref name="path"/>: <c>savegame.json</c> keeps
        /// <c>savegame.kept-1.json</c> (the newest) up to <c>savegame.kept-3.json</c> (the oldest).
        /// </summary>
        public static string KeptPath(string path, int n) =>
            Path.Combine(Path.GetDirectoryName(path) ?? string.Empty,
                         $"{Path.GetFileNameWithoutExtension(path)}.kept-{n}{Path.GetExtension(path)}");

        /// <summary>
        /// Keep the file at <paramref name="path"/>, whole, before a write replaces it. It is copied to kept
        /// copy 1, older copies move up one, and the copy past <see cref="KeptGameCount"/> is dropped. The
        /// copy is byte for byte, so a file that could not be read as a save is kept exactly as it was. The
        /// save itself is left in place for <see cref="Write"/>'s atomic swap. Returns the kept copy's path,
        /// or null when there is no file to keep.
        /// </summary>
        public static string KeepReplaced(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            string oldest = KeptPath(path, KeptGameCount);
            if (File.Exists(oldest)) File.Delete(oldest);
            for (int n = KeptGameCount - 1; n >= 1; n--)
            {
                string from = KeptPath(path, n);
                if (File.Exists(from)) File.Move(from, KeptPath(path, n + 1));
            }

            string kept = KeptPath(path, 1);
            File.Copy(path, kept);
            return kept;
        }

        /// <summary>Serialize and write <paramref name="data"/> to <paramref name="path"/> atomically.</summary>
        public static void Write(SaveData data, string path)
        {
            if (data == null || string.IsNullOrEmpty(path)) return;

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string tmp = path + ".tmp";
            File.WriteAllText(tmp, SaveSerialization.ToJson(data));

            // Swap temp → final. File.Replace is atomic on the same volume when the target exists;
            // first write is a plain move.
            if (File.Exists(path))
            {
#if UNITY_EDITOR
                BeforeReplaceForTests?.Invoke(path);
#endif
                File.Replace(tmp, path, destinationBackupFileName: null);
            }
            else File.Move(tmp, path);
        }

        /// <summary>
        /// Read and migrate the save at <paramref name="path"/>. Returns null when there is no file or
        /// it can't be read/parsed — a missing or corrupt save is "no save", not an exception, so launch
        /// degrades to a new game instead of crashing.
        /// </summary>
        public static SaveData Read(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            try
            {
                return SaveSerialization.FromJson(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }
    }
}
