using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Audio;

namespace HiddenHarbours.Tests.Audio
{
    /// <summary>
    /// The audio lane's manifest slots as DATA. These tests are the gate on the
    /// FILES, not on the mix: every recording in
    /// <c>Assets/_Project/Audio/Resources/AudioClipSet.asset</c> has to be the shape the runtime
    /// assumes, has to be small enough to live in a public repository, and has to be able to name its
    /// licence.
    ///
    /// <para>The bars come from the charter and <c>docs/audio/foley-production-guide.md</c>:</para>
    /// <list type="bullet">
    ///   <item>mono, 44,100 Hz — the manifest's one standard, so nothing resamples at load,</item>
    ///   <item>a loop lasts at most 30 s and its wrap is CONTINUOUS: the sample step across the join is
    ///   no larger than the median step inside the loop, which is the only definition of "seamless"
    ///   that can be checked by a machine,</item>
    ///   <item>a one-shot lasts at most 3 s,</item>
    ///   <item>the committed audio stays under the per-PR byte ceiling,</item>
    ///   <item>every file has a row in <c>LICENSES.md</c>. The repository is public, so a clip whose
    ///   provenance is not written down is not shippable however good it sounds.</item>
    /// </list>
    ///
    /// <para>A NULL slot is not a failure — it is the honest state for a sound we have no
    /// correctly-licensed source for, and the procedural placeholder covers it. What the tests refuse
    /// is a slot going quietly empty after we filled it: <see cref="OnlyDocumentedSlotsAreHeldNull"/>
    /// pins the four we know about by name.</para>
    /// </summary>
    public class AudioClipSetContentTests
    {
        private const int RequiredHz = 44100;
        private const float LoopMaxSeconds = 30f;
        private const float OneShotMaxSeconds = 3f;
        private const long ByteCeiling = 6L * 1024 * 1024;   // the charter's per-PR audio ceiling

        /// <summary>A step below -80 dBFS is not a seam. It is the floor for the join comparison so a
        /// clip built over true silence, whose median interior step is zero, is not judged against
        /// zero.</summary>
        private const float SeamFloor = 1e-4f;

        /// <summary>Slots the runtime loops. Everything else in the set is a one-shot cue.</summary>
        private static readonly HashSet<string> LoopSlots = new HashSet<string>
        {
            "CalmBed", "Gulls", "HullRow", "OutboardEngine", "WindTell",
            "RodCreakLoop", "PayoutTickLoop", "StrainGroanLoop", "ReelClickLoop", "SurfaceThrashLoop",
        };

        /// <summary>The slots deliberately left empty — and they are empty for two different reasons.
        /// Three are UNSOURCED: no CC0 recording of oars working in water could be found (HullRow), and
        /// the two musical cues wait until there is a score for them to sit inside (foley guide
        /// section 9). CastEntry is the opposite case — it is ALREADY VOICED. FishingController
        /// publishes JuiceMomentCue(CastEntry) in the same call that emits Cast -> Waiting, which
        /// FishingAudioLogic turns into the real _splashDown recording; a clip in this slot would land
        /// on top of an identical hit rather than under it, so what it needs is a shared bus and a
        /// level, not a file. Filling one later is fine; emptying a filled one is not, which is what
        /// pinning the list by name catches.</summary>
        private static readonly HashSet<string> DocumentedHeldSlots = new HashSet<string>
        {
            "HullRow", "CatchSting", "HomeWarmth", "CastEntry",
        };

        private const int ExpectedFilledSlots = 20;

        // ---- fixture ----------------------------------------------------------------------------

        private static AudioClipSetDef LoadSet()
        {
            var set = Resources.Load<AudioClipSetDef>("AudioClipSet");
            Assert.IsNotNull(set,
                "Resources.Load<AudioClipSetDef>(\"AudioClipSet\") found nothing. The asset must live at " +
                "Assets/_Project/Audio/Resources/AudioClipSet.asset — both audio players load it by that " +
                "name before they build their sources.");
            return set;
        }

        private static IEnumerable<FieldInfo> ClipFields()
        {
            return typeof(AudioClipSetDef)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(AudioClip));
        }

        /// <summary>Every slot that actually carries a recording, as (slot name, clip).</summary>
        private static List<(string Name, AudioClip Clip)> FilledSlots()
        {
            var set = LoadSet();
            // A slot whose guid no longer resolves comes back as Unity's "missing" reference, which
            // compares equal to null here — so a broken link reads as an empty slot and is caught by
            // OnlyDocumentedSlotsAreHeldNull rather than dying further down.
            return ClipFields()
                .Select(f => (Name: f.Name, Clip: (AudioClip)f.GetValue(set)))
                .Where(t => t.Clip != null)
                .ToList();
        }

        private static float[] Samples(AudioClip clip, string slot)
        {
            if (clip.loadState != AudioDataLoadState.Loaded)
                Assert.IsTrue(clip.LoadAudioData(), $"{slot}: could not load the audio data for {clip.name}.");

            var data = new float[clip.samples * clip.channels];
            Assert.IsTrue(clip.GetData(data, 0), $"{slot}: AudioClip.GetData failed for {clip.name}.");

            var peak = 0f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            Assert.Greater(peak, 0f,
                $"{slot}: {clip.name} decoded to pure silence. A slot filled with silence is worse than a " +
                "null slot, because the placeholder no longer covers it.");

            return data;
        }

        // ---- the slot inventory -----------------------------------------------------------------

        [Test]
        public void SetHasTwentyFourSlots_OneForEachManifestRow()
        {
            Assert.AreEqual(24, ClipFields().Count(),
                "The manifest has 24 slots — 7 on the director, 4 for the three moments, 13 on the rod fight. AudioClipSetDef must " +
                "carry exactly one AudioClip field per slot.");
        }

        [Test]
        public void OnlyDocumentedSlotsAreHeldNull()
        {
            var set = LoadSet();
            var empty = ClipFields()
                .Where(f => (AudioClip)f.GetValue(set) == null)
                .Select(f => f.Name)
                .ToList();

            var undocumented = empty.Where(n => !DocumentedHeldSlots.Contains(n)).ToList();
            Assert.IsEmpty(undocumented,
                "These slots are empty but are not among the ones we documented as held: " +
                string.Join(", ", undocumented) +
                ". Either slot a correctly-licensed clip, or say so in LICENSES.md and add the slot to " +
                "DocumentedHeldSlots — an empty slot is honest only when it is written down.");

            Assert.GreaterOrEqual(ClipFields().Count() - empty.Count, ExpectedFilledSlots,
                $"The lane has shipped {ExpectedFilledSlots} filled slots; this build has " +
                $"{ClipFields().Count() - empty.Count}. A slot does not go back to null without a " +
                "reason written down here and in LICENSES.md.");
        }

        // ---- the shape the runtime assumes ------------------------------------------------------

        [Test]
        public void EveryFilledSlot_IsMonoAt44100Hz()
        {
            foreach (var (name, clip) in FilledSlots())
            {
                Assert.AreEqual(1, clip.channels,
                    $"{name}: {clip.name} has {clip.channels} channels. The manifest is mono — a stereo " +
                    "source carries its own width and fights the positional mix.");
                Assert.AreEqual(RequiredHz, clip.frequency,
                    $"{name}: {clip.name} is {clip.frequency} Hz, not {RequiredHz} Hz.");
                Assert.Greater(clip.samples, 0, $"{name}: {clip.name} has no samples.");
            }
        }

        [Test]
        public void LoopSlots_AreShortEnoughToLoop()
        {
            foreach (var (name, clip) in FilledSlots().Where(t => LoopSlots.Contains(t.Name)))
                Assert.LessOrEqual(clip.length, LoopMaxSeconds,
                    $"{name}: {clip.name} is {clip.length:F2} s. A bed longer than {LoopMaxSeconds} s is " +
                    "memory we are not spending on a sound the player hears as a loop anyway.");
        }

        [Test]
        public void OneShotSlots_AreShortEnoughToBeCues()
        {
            foreach (var (name, clip) in FilledSlots().Where(t => !LoopSlots.Contains(t.Name)))
                Assert.LessOrEqual(clip.length, OneShotMaxSeconds,
                    $"{name}: {clip.name} is {clip.length:F2} s. A cue that outlasts {OneShotMaxSeconds} s " +
                    "stops reading as a response to the thing that fired it.");
        }

        // ---- the seam ---------------------------------------------------------------------------

        [Test]
        public void LoopSlots_WrapWithoutAStep()
        {
            foreach (var (name, clip) in FilledSlots().Where(t => LoopSlots.Contains(t.Name)))
            {
                var data = Samples(clip, name);
                Assert.Greater(data.Length, 2, $"{name}: too few samples to have a seam.");

                var join = Mathf.Abs(data[0] - data[data.Length - 1]);

                var steps = new float[data.Length - 1];
                for (int i = 1; i < data.Length; i++) steps[i - 1] = Mathf.Abs(data[i] - data[i - 1]);
                Array.Sort(steps);
                var median = steps[steps.Length / 2];

                Assert.LessOrEqual(join, Mathf.Max(median, SeamFloor),
                    $"{name}: {clip.name} steps {join:E3} across the loop join but only {median:E3} " +
                    "between neighbouring samples inside it. That step is the click the player hears " +
                    "every time the bed wraps.");
            }
        }

        // ---- what a public repository costs -----------------------------------------------------

        [Test]
        public void CommittedAudioStaysUnderTheByteCeiling()
        {
            long total = 0;
            foreach (var (name, clip) in FilledSlots())
            {
                var assetPath = AssetDatabase.GetAssetPath(clip);
                Assert.IsNotEmpty(assetPath, $"{name}: {clip.name} has no asset path.");

                var full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
                var info = new FileInfo(full);
                Assert.IsTrue(info.Exists, $"{name}: {assetPath} is not on disk.");

                // An unfetched Git LFS pointer is a ~130-byte text file. Without this the byte total
                // would pass by being empty, which is the most comfortable way for a test to say nothing.
                Assert.Greater(info.Length, 4096,
                    $"{name}: {assetPath} is only {info.Length} bytes — that is an unfetched LFS pointer, " +
                    "not audio. Run `git lfs pull` before trusting anything else in this fixture.");

                total += info.Length;
            }

            Assert.LessOrEqual(total, ByteCeiling,
                $"The committed audio is {total / 1024f / 1024f:F3} MB against a " +
                $"{ByteCeiling / 1024 / 1024} MB ceiling. Every byte here is redistributed to everyone " +
                "who clones a public repository.");
        }

        // ---- provenance -------------------------------------------------------------------------

        [Test]
        public void EveryFilledSlot_HasALicenceRow()
        {
            const string ledger = "Assets/_Project/Audio/LICENSES.md";
            var full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ledger);
            Assert.IsTrue(File.Exists(full), $"{ledger} is missing. It is the attribution ledger; " +
                                             "without it none of these files may be redistributed.");

            var text = File.ReadAllText(full);
            foreach (var (name, clip) in FilledSlots())
            {
                var file = Path.GetFileName(AssetDatabase.GetAssetPath(clip));
                Assert.IsTrue(text.Contains(file),
                    $"{name}: {file} has no row in {ledger}. A file without a row does not merge — the " +
                    "repository is public, so every committed byte is redistributed under some licence " +
                    "and we have to be able to name it.");
            }
        }
    }
}
