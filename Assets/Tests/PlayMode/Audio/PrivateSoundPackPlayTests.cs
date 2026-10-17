using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Audio;

namespace HiddenHarbours.Tests.Audio
{
    /// <summary>
    /// The private sound pack, played (<c>docs/audio/private-sound-pack.md</c>). Both cases hold in every
    /// checkout. Without the pack (CI, a fresh clone, every lane box) the players hold the public set's
    /// clips and the music bus stays silent. With it installed, each slot the pack fills plays the pack's
    /// clip, every other slot plays the public one, and the music bus starts the first song and moves on
    /// to the second when the first ends. Each expectation is read from the two clip set ASSETS, never
    /// from <see cref="AudioClipSets"/>, the code under test. Logs carry counts and slot names only.
    /// </summary>
    public class PrivateSoundPackPlayTests
    {
        private const float StartBudgetSeconds = 20f;   // a streamed song's first frames, on a cold machine
        private const float EndBudgetSeconds = 30f;     // the seeked tail plus the gap, with room
        private const float SeekToEndSeconds = 2f;

        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            foreach (var go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        private T Player<T>() where T : MonoBehaviour
        {
            var found = Object.FindAnyObjectByType<T>();
            if (found != null) return found;
            var go = new GameObject("[test " + typeof(T).Name + "]");
            _made.Add(go);
            return go.AddComponent<T>();
        }

        private static IEnumerable<FieldInfo> Slots() =>
            typeof(AudioClipSetDef).GetFields(BindingFlags.Public | BindingFlags.Instance)
                                   .Where(f => f.FieldType == typeof(AudioClip));

        private static object Field(object owner, string name) =>
            owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(owner);

        private static AudioClipSetDef PublicSet() => Resources.Load<AudioClipSetDef>(AudioClipSets.PublicSetName);
        private static AudioClipSetDef PrivateSet() => Resources.Load<AudioClipSetDef>(AudioClipSets.PrivateSetName);

        private static AudioClip[] NonNull(AudioClip[] songs) =>
            songs == null ? new AudioClip[0] : songs.Where(s => s != null).ToArray();

        [UnityTest]
        public IEnumerator EverySlot_PlaysTheEffectiveSet()
        {
            yield return null;
            var publicSet = PublicSet();
            var privateSet = PrivateSet();
            Assert.IsNotNull(publicSet, "The public clip set must load by name.");

            if (privateSet == null)
                Assert.AreSame(publicSet, AudioClipSets.Effective(), "Without the pack the players read the public asset itself.");
            else
                Assert.AreEqual(HideFlags.DontSave, AudioClipSets.Effective().hideFlags & HideFlags.DontSave,
                    "With the pack the players read an overlay that is never saved.");

            var players = new MonoBehaviour[] { Player<AudioDirector>(), Player<FishingAudio>() };
            int fromPack = 0, fromPublic = 0, placeholders = 0;
            foreach (var slot in Slots())
            {
                var fieldName = "_" + char.ToLowerInvariant(slot.Name[0]) + slot.Name.Substring(1);
                var owner = players.FirstOrDefault(p => p.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance) != null);
                Assert.IsNotNull(owner, $"{slot.Name}: neither player has a {fieldName} field.");
                var playing = (AudioClip)Field(owner, fieldName);

                var packClip = privateSet != null ? (AudioClip)slot.GetValue(privateSet) : null;
                var publicClip = (AudioClip)slot.GetValue(publicSet);
                if (packClip != null)
                {
                    Assert.AreSame(packClip, playing, $"{slot.Name}: the pack fills this slot, so its clip must play.");
                    fromPack++;
                }
                else if (publicClip != null)
                {
                    Assert.AreSame(publicClip, playing, $"{slot.Name}: the pack leaves this slot, so the public clip must play.");
                    fromPublic++;
                }
                else
                {
                    placeholders++;
                }
                Debug.Log($"[Audio test] {slot.Name}: " + (packClip != null ? "pack" : publicClip != null ? "public" : "placeholder or none"));
            }
            Debug.Log($"[Audio test] slots: {fromPack} from the pack, {fromPublic} public, {placeholders} placeholder or none.");
        }

        [UnityTest]
        public IEnumerator MusicBus_FollowsTheSongCycle()
        {
            yield return null;
            var publicSet = PublicSet();
            var privateSet = PrivateSet();
            var packSongs = privateSet != null ? NonNull(privateSet.Songs) : new AudioClip[0];
            var songs = packSongs.Length > 0 ? packSongs : NonNull(publicSet.Songs);

            var director = Player<AudioDirector>();
            var music = (AudioSource)Field(director, "_music");
            Assert.IsNotNull(music, "The director builds its music bus in Awake.");

            if (songs.Length == 0)
            {
                for (int i = 0; i < 30; i++)
                {
                    yield return null;
                    Assert.IsNull(music.clip, "No songs: the music bus holds no clip, as before the pack.");
                    Assert.IsFalse(music.isPlaying, "No songs: the music bus stays silent.");
                }
                Debug.Log("[Audio test] music: no songs, the bus is silent.");
                yield break;
            }

            // Song 1 starts at once (no gap before the first).
            float t0 = Time.realtimeSinceStartup;
            while (!(music.clip == songs[0] && music.isPlaying) && Time.realtimeSinceStartup - t0 < StartBudgetSeconds)
                yield return null;
            Assert.AreSame(songs[0], music.clip, "The music bus starts the first song.");
            Assert.IsTrue(music.isPlaying, $"The first song was not heard playing within {StartBudgetSeconds} s.");
            Debug.Log($"[Audio test] music: song 1 of {songs.Length} playing after {Time.realtimeSinceStartup - t0:0.00} s.");

            // Skip to its last seconds, then wait for the end, the gap and song 2.
            var first = music.clip;
            music.timeSamples = Mathf.Max(0, first.samples - Mathf.RoundToInt(SeekToEndSeconds * first.frequency));
            float tEnd = -1f, t1 = Time.realtimeSinceStartup;
            var second = songs[1 % songs.Length];
            while (Time.realtimeSinceStartup - t1 < EndBudgetSeconds)
            {
                yield return null;
                if (tEnd < 0f && !music.isPlaying) tEnd = Time.realtimeSinceStartup;
                if (tEnd >= 0f && music.isPlaying) break;
            }
            Assert.Greater(tEnd, 0f, $"The first song did not end within {EndBudgetSeconds} s of the seek.");
            Assert.AreSame(second, music.clip, "When the first song ends, the music bus moves on to the second.");
            Assert.IsTrue(music.isPlaying, $"The second song was not heard playing within {EndBudgetSeconds} s.");
            float silence = Time.realtimeSinceStartup - tEnd;
            Assert.GreaterOrEqual(silence, publicSet.SongGapSeconds - 0.25f,
                "The gap between songs is the public set's SongGapSeconds.");
            Debug.Log($"[Audio test] music: song 2 of {songs.Length} playing after {silence:0.00} s of silence " +
                      $"(SongGapSeconds {publicSet.SongGapSeconds}).");
        }
    }
}
