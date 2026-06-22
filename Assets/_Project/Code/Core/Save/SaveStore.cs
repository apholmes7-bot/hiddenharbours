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
        /// <summary>Default save file name under <see cref="Application.persistentDataPath"/>.</summary>
        public const string DefaultFileName = "savegame.json";

        /// <summary>The default save path for the running game.</summary>
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, DefaultFileName);

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
            if (File.Exists(path)) File.Replace(tmp, path, destinationBackupFileName: null);
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
