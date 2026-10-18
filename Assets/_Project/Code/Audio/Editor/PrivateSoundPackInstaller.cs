using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Audio.Editor
{
    /// <summary>
    /// Hidden Harbours ▸ Audio ▸ Install Private Sound Pack: copies the owner's picked sounds into the
    /// gitignored <c>Assets/_Project/Audio/Private/</c> and points <c>Resources/AudioClipSetPrivate</c> at
    /// them, which <see cref="AudioClipSets"/> lays over the public clip set
    /// (<c>docs/audio/private-sound-pack.md</c>).
    ///
    /// <para><b>The pack folder</b> (chosen once, remembered in EditorPrefs) holds one file per slot,
    /// named by its <see cref="AudioClipSetDef"/> field (<c>&lt;Field&gt;.wav</c> or <c>&lt;Field&gt;.ogg</c>),
    /// and a <c>Music</c> folder whose files are the song cycle, in name order. A slot pick must be mono
    /// at 44,100 Hz (the clip-set bar); one that is not is named and refused, and its slot stays as it
    /// was. Songs keep their own channels and rate.</para>
    ///
    /// <para><b>It mirrors the pack.</b> A pick or song gone from the pack folder is removed from the
    /// private set and its copy deleted; nothing outside <c>Private/</c> is ever touched. An empty pack
    /// folder installs an empty private set, which plays exactly like the public one. It works in edit
    /// mode only. Remove Private Sound Pack deletes the folder, and play goes back to the public set.</para>
    /// </summary>
    public static class PrivateSoundPackInstaller
    {
        public const string PrivateRoot = "Assets/_Project/Audio/Private";
        public const string ClipsFolder = PrivateRoot + "/Clips";
        public const string MusicFolder = PrivateRoot + "/Music";
        private const string ResourcesFolder = PrivateRoot + "/Resources";
        private const string SetPath = ResourcesFolder + "/" + AudioClipSets.PrivateSetName + ".asset";
        private const string PrivateSetId = "audio.clip_set_private";

        private const string PackSongsFolder = "Music";
        private const string FolderPref = "HiddenHarbours.Audio.PrivateSoundPackFolder";
        private const string FolderArg = "-soundPackFolder";
        private const string Title = "Private Sound Pack";
        private const int SlotChannels = 1;
        private const int SlotRate = 44100;
        private static readonly string[] Extensions = { ".wav", ".ogg" };

        /// <summary>What one install did: the slots it filled, the songs it listed, the copies it
        /// removed, and each file it refused or skipped.</summary>
        public sealed class Report
        {
            public readonly List<string> Slots = new List<string>();
            public readonly List<string> Problems = new List<string>();
            public int Songs;
            public int Removed;

            public string Summary
            {
                get
                {
                    var sb = new StringBuilder();
                    sb.Append($"Installed {Slots.Count} slot pick(s)");
                    if (Slots.Count > 0) sb.Append($" ({string.Join(", ", Slots)})");
                    sb.Append($" and {Songs} song(s); removed {Removed} copy(ies) no longer in the pack.");
                    foreach (var p in Problems) sb.Append("\n- ").Append(p);
                    return sb.ToString();
                }
            }
        }

        // ---- menu ---------------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Audio/Install Private Sound Pack")]
        private static void InstallFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(Title, "Leave play mode first: the pack installs in edit mode only.", "OK");
                return;
            }

            var folder = EditorPrefs.GetString(FolderPref, string.Empty);
            if (!Directory.Exists(folder))
            {
                var why = string.IsNullOrEmpty(folder) ? "No sound pack folder is chosen yet." : $"{folder} was not found.";
                if (!EditorUtility.DisplayDialog(Title,
                        why + "\n\nChoose the pack's picks folder: one file per slot, named by its " +
                        "AudioClipSetDef field (<Field>.wav), and a Music folder holding the songs.", "Choose folder…", "Cancel"))
                    return;
                folder = EditorUtility.OpenFolderPanel("The sound pack's picks folder", string.Empty, string.Empty);
                if (string.IsNullOrEmpty(folder)) return;
                EditorPrefs.SetString(FolderPref, folder);
            }

            EditorUtility.DisplayDialog(Title, Install(folder).Summary, "OK");
        }

        [MenuItem("Hidden Harbours/Audio/Remove Private Sound Pack")]
        private static void RemoveFromMenu()
        {
            if (!AssetDatabase.IsValidFolder(PrivateRoot))
            {
                EditorUtility.DisplayDialog(Title, "No private sound pack is installed.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog(Title,
                    $"Delete {PrivateRoot} and everything in it? The pack folder itself is not touched.", "Delete", "Cancel"))
                return;
            if (!Remove()) EditorUtility.DisplayDialog(Title, "Nothing was removed: leave play mode first.", "OK");
        }

        // ---- batch: -executeMethod HiddenHarbours.Audio.Editor.PrivateSoundPackInstaller.InstallBatch -soundPackFolder <folder>

        public static void InstallBatch()
        {
            var report = Install(ArgAfter(FolderArg));
            Debug.Log("[Audio] Install Private Sound Pack: " + report.Summary);
            if (report.Problems.Count > 0) EditorApplication.Exit(1);
        }

        public static void RemoveBatch()
        {
            bool removed = Remove();
            Debug.Log("[Audio] Remove Private Sound Pack: " + (removed ? "done." : "refused in play mode."));
            if (!removed) EditorApplication.Exit(1);
        }

        // ---- the work -----------------------------------------------------------------------------

        /// <summary>Installs the pack in <paramref name="packFolder"/>, mirroring it into
        /// <see cref="PrivateRoot"/>.</summary>
        public static Report Install(string packFolder)
        {
            var report = new Report();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                report.Problems.Add("Leave play mode first: the pack installs in edit mode only.");
                return report;
            }
            if (string.IsNullOrEmpty(packFolder) || !Directory.Exists(packFolder))
            {
                report.Problems.Add($"The pack folder \"{packFolder}\" was not found. Nothing was changed.");
                return report;
            }

            var slots = typeof(AudioClipSetDef).GetFields(BindingFlags.Public | BindingFlags.Instance)
                                               .Where(f => f.FieldType == typeof(AudioClip))
                                               .ToDictionary(f => f.Name, StringComparer.Ordinal);

            // The slot picks: <Field>.wav or <Field>.ogg, and only one file per slot.
            var picks = new Dictionary<string, string>(StringComparer.Ordinal);
            var refused = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in AudioFiles(packFolder).GroupBy(Path.GetFileNameWithoutExtension, StringComparer.Ordinal))
            {
                var field = group.Key;
                var file = group.First();
                if (!slots.ContainsKey(field))
                {
                    foreach (var f in group) report.Problems.Add($"{Path.GetFileName(f)} matches no slot of AudioClipSetDef; skipped.");
                    continue;
                }
                if (group.Count() > 1)
                {
                    report.Problems.Add($"{field} has {group.Count()} files in the pack; refused, the slot stays as it was.");
                    refused.Add(field);
                    continue;
                }
                if (!ReadFormat(file, out int channels, out int rate))
                {
                    report.Problems.Add($"{Path.GetFileName(file)} is not a WAV or Ogg Vorbis file this menu can read; refused, the slot stays as it was.");
                    refused.Add(field);
                    continue;
                }
                if (channels != SlotChannels || rate != SlotRate)
                {
                    report.Problems.Add($"{Path.GetFileName(file)} is {channels} channel(s) at {rate} Hz; a slot takes mono at " +
                                        $"{SlotRate} Hz. Refused, the slot stays as it was.");
                    refused.Add(field);
                    continue;
                }
                picks[field] = file;
            }

            // The songs: every audio file in the pack's Music folder, in name order.
            var songsFolder = Path.Combine(packFolder, PackSongsFolder);
            var songs = Directory.Exists(songsFolder) ? AudioFiles(songsFolder).ToList() : new List<string>();

            EnsureFolder(PrivateRoot);
            EnsureFolder(ClipsFolder);
            EnsureFolder(MusicFolder);
            EnsureFolder(ResourcesFolder);

            var keepClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pick in picks)
            {
                var dest = ClipsFolder + "/" + pick.Key + Path.GetExtension(pick.Value).ToLowerInvariant();
                CopyIfChanged(pick.Value, dest);
                keepClips.Add(dest);
            }
            foreach (var field in refused)
                foreach (var ext in Extensions)
                    keepClips.Add(ClipsFolder + "/" + field + ext);
            report.Removed += DeleteAllBut(ClipsFolder, keepClips);

            var songPaths = new List<string>();
            foreach (var song in songs)
            {
                var dest = MusicFolder + "/" + Path.GetFileName(song);
                CopyIfChanged(song, dest);
                songPaths.Add(dest);
            }
            report.Removed += DeleteAllBut(MusicFolder, new HashSet<string>(songPaths, StringComparer.OrdinalIgnoreCase));

            // Imports the copies; PrivateSoundPackImport sets their import settings first.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var set = AssetDatabase.LoadAssetAtPath<AudioClipSetDef>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<AudioClipSetDef>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            set.Id = PrivateSetId;
            foreach (var slot in slots.Values)
            {
                if (refused.Contains(slot.Name)) continue;
                AudioClip clip = null;
                if (picks.TryGetValue(slot.Name, out var file))
                {
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipsFolder + "/" + slot.Name + Path.GetExtension(file).ToLowerInvariant());
                    if (clip == null) report.Problems.Add($"{slot.Name}: the copy did not import as an AudioClip.");
                }
                slot.SetValue(set, clip);
                if (clip != null) report.Slots.Add(slot.Name);
            }
            var songClips = new List<AudioClip>();
            foreach (var path in songPaths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) songClips.Add(clip);
                else report.Problems.Add($"{Path.GetFileName(path)} did not import as an AudioClip; it is left out of the cycle.");
            }
            set.Songs = songClips.ToArray();
            report.Songs = songClips.Count;

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return report;
        }

        /// <summary>Deletes <see cref="PrivateRoot"/> and its .meta. False in play mode.</summary>
        public static bool Remove()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (!AssetDatabase.IsValidFolder(PrivateRoot)) return true;
            return AssetDatabase.DeleteAsset(PrivateRoot);
        }

        // ---- helpers ------------------------------------------------------------------------------

        private static IEnumerable<string> AudioFiles(string folder) =>
            Directory.GetFiles(folder)
                     .Where(f => Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                     .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal);

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            var parent = assetFolder.Substring(0, assetFolder.LastIndexOf('/'));
            AssetDatabase.CreateFolder(parent, assetFolder.Substring(parent.Length + 1));
        }

        private static string FullPath(string assetPath) =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);

        private static void CopyIfChanged(string source, string destAsset)
        {
            var dest = FullPath(destAsset);
            if (File.Exists(dest) && new FileInfo(dest).Length == new FileInfo(source).Length &&
                File.ReadAllBytes(dest).SequenceEqual(File.ReadAllBytes(source)))
                return;
            File.Copy(source, dest, overwrite: true);
        }

        /// <summary>Deletes every asset directly in <paramref name="assetFolder"/> that is not in
        /// <paramref name="keep"/>, and returns how many it deleted.</summary>
        private static int DeleteAllBut(string assetFolder, HashSet<string> keep)
        {
            int deleted = 0;
            foreach (var file in Directory.GetFiles(FullPath(assetFolder)))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                var asset = assetFolder + "/" + Path.GetFileName(file);
                if (keep.Contains(asset)) continue;
                if (AssetDatabase.DeleteAsset(asset)) deleted++;
            }
            return deleted;
        }

        private static string ArgAfter(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag) return args[i + 1];
            return null;
        }

        /// <summary>Channels and sample rate from a RIFF WAVE "fmt " chunk or an Ogg Vorbis
        /// identification header, read before anything is copied.</summary>
        public static bool ReadFormat(string path, out int channels, out int rate)
        {
            channels = 0;
            rate = 0;
            byte[] head;
            using (var f = File.OpenRead(path))
            {
                head = new byte[Math.Min(f.Length, 1 << 16)];
                int read = 0;
                while (read < head.Length)
                {
                    int n = f.Read(head, read, head.Length - read);
                    if (n <= 0) break;
                    read += n;
                }
            }

            if (Tag(head, 0, "RIFF") && Tag(head, 8, "WAVE"))
            {
                for (int at = 12; at + 8 <= head.Length;)
                {
                    int size = BitConverter.ToInt32(head, at + 4);
                    if (Tag(head, at, "fmt ") && at + 16 <= head.Length)
                    {
                        channels = BitConverter.ToUInt16(head, at + 10);
                        rate = BitConverter.ToInt32(head, at + 12);
                        return channels > 0 && rate > 0;
                    }
                    if (size < 0) return false;
                    at += 8 + size + (size & 1);
                }
                return false;
            }

            // Ogg: the first page's first packet is the Vorbis identification header.
            if (Tag(head, 0, "OggS") && head.Length > 27)
            {
                int packet = 27 + head[26];
                if (packet + 16 <= head.Length && head[packet] == 1 && Tag(head, packet + 1, "vorbis"))
                {
                    channels = head[packet + 11];
                    rate = BitConverter.ToInt32(head, packet + 12);
                    return channels > 0 && rate > 0;
                }
            }
            return false;
        }

        private static bool Tag(byte[] b, int at, string tag)
        {
            if (at + tag.Length > b.Length) return false;
            for (int i = 0; i < tag.Length; i++)
                if (b[at + i] != tag[i]) return false;
            return true;
        }
    }
}
