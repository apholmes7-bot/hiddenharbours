using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HiddenHarbours.Audio
{
    /// <summary>
    /// Where both players get their clips. The PUBLIC set (<c>Resources/AudioClipSet</c>) is what the
    /// repository ships; the PRIVATE set (<c>Resources/AudioClipSetPrivate</c>, in the gitignored
    /// <c>Assets/_Project/Audio/Private/</c>) is laid over it in a checkout that has the owner's sound pack
    /// (Hidden Harbours ▸ Audio ▸ Install Private Sound Pack; <c>docs/audio/private-sound-pack.md</c>).
    ///
    /// <para><b>The override rule.</b> A private slot that holds a clip replaces the public one; a null
    /// private slot means "use the public one", and a slot null in both keeps the player's procedural
    /// placeholder. Private <see cref="AudioClipSetDef.Songs"/>, when there are any, replace the public
    /// ones; the gap between songs is always the public set's tunable. With no private set the public
    /// asset itself comes back, untouched, so a fresh clone, CI and every lane box behave exactly as they
    /// did before the pack existed.</para>
    ///
    /// <para><b>The public asset is never written.</b> In the editor <c>Resources.Load</c> hands back the
    /// asset on disk, and a field set on it in play mode would be saved into a public file. The overlay
    /// is a new in-memory set, and <see cref="HideFlags.DontSave"/> keeps it out of every scene and
    /// asset.</para>
    /// </summary>
    public static class AudioClipSets
    {
        public const string PublicSetName = "AudioClipSet";
        public const string PrivateSetName = "AudioClipSetPrivate";

        private static readonly FieldInfo[] Slots = ClipSlots();

        private static AudioClipSetDef _effective;
        private static bool _resolved;

        /// <summary>The set both players copy from: built once, so the pack is logged once.</summary>
        public static AudioClipSetDef Effective()
        {
            if (_resolved) return _effective;
            _resolved = true;

            var publicSet = Resources.Load<AudioClipSetDef>(PublicSetName);
            var privateSet = Resources.Load<AudioClipSetDef>(PrivateSetName);
            _effective = Overlay(publicSet, privateSet, out int overridden);

            // Counts only, never a file name: this line reaches Player.log and CI logs.
            if (privateSet != null)
                Debug.Log($"[Audio] private sound pack: {overridden} of {Slots.Length} slots override the public " +
                          $"clip set; {SongsOf(_effective).Length} songs in the music cycle.");

            return _effective;
        }

        /// <summary>The override rule. Returns <paramref name="publicSet"/> itself when there is no
        /// private set; otherwise a new set holding each private clip over the public one.</summary>
        public static AudioClipSetDef Overlay(AudioClipSetDef publicSet, AudioClipSetDef privateSet, out int overridden)
        {
            overridden = 0;
            if (privateSet == null) return publicSet;

            var merged = ScriptableObject.CreateInstance<AudioClipSetDef>();
            merged.name = PublicSetName + " (private pack)";
            merged.hideFlags = HideFlags.DontSave;
            if (publicSet != null)
            {
                merged.Id = publicSet.Id;
                merged.SongGapSeconds = publicSet.SongGapSeconds;
                merged.Songs = SongsOf(publicSet);
                CopyNonNullSlots(publicSet, merged);
            }
            overridden = CopyNonNullSlots(privateSet, merged);

            var privateSongs = SongsOf(privateSet);
            if (privateSongs.Length > 0) merged.Songs = privateSongs;
            return merged;
        }

        /// <summary>Copies every non-null clip slot of <paramref name="from"/> onto <paramref name="into"/>
        /// and returns how many it copied.</summary>
        public static int CopyNonNullSlots(AudioClipSetDef from, AudioClipSetDef into)
        {
            int copied = 0;
            foreach (var slot in Slots)
            {
                var clip = (AudioClip)slot.GetValue(from);
                if (clip == null) continue;
                slot.SetValue(into, clip);
                copied++;
            }
            return copied;
        }

        /// <summary>The set's songs in cycle order, without empty entries: a new array, never the set's
        /// own, and an empty one for a null set.</summary>
        public static AudioClip[] SongsOf(AudioClipSetDef set)
        {
            if (set == null || set.Songs == null) return Array.Empty<AudioClip>();
            var songs = new List<AudioClip>(set.Songs.Length);
            foreach (var song in set.Songs)
                if (song != null) songs.Add(song);
            return songs.ToArray();
        }

        /// <summary>Every <see cref="AudioClip"/> field of the set, so a slot added later is covered
        /// without touching this file (the content tests count the same fields).</summary>
        private static FieldInfo[] ClipSlots()
        {
            var slots = new List<FieldInfo>();
            foreach (var f in typeof(AudioClipSetDef).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (f.FieldType == typeof(AudioClip)) slots.Add(f);
            return slots.ToArray();
        }

        // Domain reload is on today (EditorSettings m_EnterPlayModeOptions: 0); this keeps the cache
        // honest if it is ever turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlay()
        {
            _effective = null;
            _resolved = false;
        }
    }
}
