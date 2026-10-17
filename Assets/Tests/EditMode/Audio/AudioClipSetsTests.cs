using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Audio;

namespace HiddenHarbours.Tests.Audio
{
    /// <summary>
    /// The private sound pack's seam (<see cref="AudioClipSets"/>). The public clip set is what every
    /// checkout has; the private set exists only where the owner installed the pack. These tests hold the
    /// rule both ways and keep the folder out of git. The licence gate (AudioClipSetContentTests) is not
    /// touched: it loads the public set by name and never sees the private one.
    /// </summary>
    public class AudioClipSetsTests
    {
        private const string PrivateFolder = "Assets/_Project/Audio/Private/";
        private const string PublicSetPath = "Assets/_Project/Audio/Resources/AudioClipSet.asset";

        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private static IEnumerable<FieldInfo> Slots() =>
            typeof(AudioClipSetDef).GetFields(BindingFlags.Public | BindingFlags.Instance)
                                   .Where(f => f.FieldType == typeof(AudioClip));

        private T Make<T>(T o) where T : Object { _made.Add(o); return o; }
        private AudioClipSetDef NewSet() => Make(ScriptableObject.CreateInstance<AudioClipSetDef>());
        private AudioClip NewClip(string name) => Make(AudioClip.Create(name, 64, 1, 44100, false));

        [Test]
        public void NoPrivateSet_EffectiveSetIsThePublicSet_SlotBySlot()
        {
            var publicSet = Resources.Load<AudioClipSetDef>("AudioClipSet");
            Assert.IsNotNull(publicSet, "The public clip set must load by name, as both players load it.");
            Assert.AreEqual("AudioClipSet", AudioClipSets.PublicSetName, "the name the players load");
            Assert.AreEqual("AudioClipSetPrivate", AudioClipSets.PrivateSetName,
                "the name the installer writes and docs/audio/private-sound-pack.md gives");

            var result = AudioClipSets.Overlay(publicSet, null, out int overridden);

            Assert.AreSame(publicSet, result, "With no private set the players must get the public asset itself.");
            Assert.AreEqual(0, overridden);
            foreach (var slot in Slots())
                Assert.AreSame(slot.GetValue(publicSet), slot.GetValue(result), slot.Name);
        }

        [Test]
        public void PrivateSet_OverridesOnlyItsNonNullSlots()
        {
            var publicSet = NewSet();
            var privateSet = NewSet();
            var publicCalm = NewClip("public_calm");
            var publicEngine = NewClip("public_engine");
            var privateEngine = NewClip("private_engine");
            var privateOars = NewClip("private_oars");

            publicSet.CalmBed = publicCalm;          // private leaves it null: the public one stays
            publicSet.OutboardEngine = publicEngine; // both hold one: the private one wins
            privateSet.OutboardEngine = privateEngine;
            privateSet.HullRow = privateOars;        // null in public, as on main: the private one fills it

            var merged = Make(AudioClipSets.Overlay(publicSet, privateSet, out int overridden));

            Assert.AreEqual(2, overridden, "The count is the private set's non-null slots.");
            Assert.AreSame(publicCalm, merged.CalmBed);
            Assert.AreSame(privateEngine, merged.OutboardEngine);
            Assert.AreSame(privateOars, merged.HullRow);
            foreach (var slot in Slots().Where(s => s.Name != "CalmBed" && s.Name != "OutboardEngine" && s.Name != "HullRow"))
                Assert.IsNull(slot.GetValue(merged), $"{slot.Name}: null in both sets must stay null (the placeholder plays).");

            Assert.AreNotSame(publicSet, merged, "The overlay is a new set.");
            Assert.AreSame(publicEngine, publicSet.OutboardEngine, "The public set must never be written.");
            Assert.IsNull(publicSet.HullRow, "The public set must never be written.");

            // Every slot, not just the three named above: a private clip in each one overrides each one.
            var full = NewSet();
            foreach (var slot in Slots()) slot.SetValue(full, NewClip("private_" + slot.Name));
            var all = Make(AudioClipSets.Overlay(publicSet, full, out int every));
            Assert.AreEqual(Slots().Count(), every);
            foreach (var slot in Slots())
                Assert.AreSame(slot.GetValue(full), slot.GetValue(all), slot.Name);
        }

        [Test]
        public void Overlay_IsNeverSavedWithTheProject()
        {
            var merged = Make(AudioClipSets.Overlay(NewSet(), NewSet(), out _));
            Assert.AreEqual(HideFlags.DontSave, merged.hideFlags & HideFlags.DontSave,
                "The overlay is built at runtime and owned by no file: it must never be written into a scene or an asset.");
        }

        [Test]
        public void PrivateSongs_ReplaceThePublicOnes_AndTheGapStaysPublic()
        {
            var publicSong = NewClip("public_song");
            var one = NewClip("private_one");
            var two = NewClip("private_two");

            var publicSet = NewSet();
            publicSet.Songs = new[] { publicSong };
            publicSet.SongGapSeconds = 3.5f;
            var privateSet = NewSet();
            privateSet.Songs = new[] { one, null, two };
            privateSet.SongGapSeconds = 9f;

            var merged = Make(AudioClipSets.Overlay(publicSet, privateSet, out _));
            CollectionAssert.AreEqual(new[] { one, two }, merged.Songs, "the private songs, in order, without the empty entry");
            Assert.AreEqual(3.5f, merged.SongGapSeconds, "the gap is the public set's tunable");

            var noSongs = Make(AudioClipSets.Overlay(publicSet, NewSet(), out _));
            CollectionAssert.AreEqual(new[] { publicSong }, noSongs.Songs, "a private set without songs keeps the public ones");

            CollectionAssert.AreEqual(new[] { publicSong }, publicSet.Songs, "The public set must never be written.");
            Assert.AreEqual(3.5f, publicSet.SongGapSeconds, "The public set must never be written.");
        }

        [Test]
        public void GitIgnore_CoversThePrivateFolderAndItsMeta()
        {
            // The editor's working directory is the project root, which is the repository root here.
            var lines = File.ReadAllLines(Path.Combine(Directory.GetCurrentDirectory(), ".gitignore"))
                            .Select(l => l.Trim()).ToList();
            CollectionAssert.Contains(lines, "/[Aa]ssets/_Project/Audio/Private/",
                "The private sound pack's folder must be gitignored: its sounds may never reach this public repository.");
            CollectionAssert.Contains(lines, "/[Aa]ssets/_Project/Audio/Private.meta",
                "The folder's .meta must be ignored too, or a commit carries a folder nobody else has.");
        }

        [Test]
        public void PublicSet_NeverPointsIntoThePrivateFolder()
        {
            var yaml = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), PublicSetPath));
            foreach (Match m in Regex.Matches(yaml, @"guid: ([0-9a-f]{32})"))
            {
                var path = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                Assert.IsNotEmpty(path,
                    $"AudioClipSet.asset points at guid {m.Groups[1].Value}, which this checkout does not have. " +
                    "A clip from the private pack dragged onto the PUBLIC set reads this way everywhere but the owner's PC.");
                StringAssert.DoesNotStartWith(PrivateFolder, path,
                    "The public clip set must never reference the private pack. Put the clip on AudioClipSetPrivate.");
            }
        }
    }
}
