using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for the inner-voice clue channel (owner ruling 2026-09-06). Mirrors
    /// <see cref="NpcContentValidationTests"/>'s rules over the real assets in <c>Data/</c>: every
    /// <see cref="InnerVoiceLineDef"/> has a non-empty, unique, namespaced id and something to say, and
    /// every one of them is actually REACHABLE.
    ///
    /// <para><b>The reachability check is the one that earns its keep.</b> The director loads its lines
    /// from <see cref="InnerVoiceLibrary"/> in Resources, so a clue asset that nobody added to the
    /// library is authored, committed, reviewed — and dead. Nothing else in the project would ever say
    /// so: it does not break a build, fail a scene, or log a warning. It just never happens.</para>
    ///
    /// <para>Lives in the TOP-LEVEL EditMode folder because it loads real Data/ assets through
    /// <c>AssetDatabase</c>, the same placement as the other content validators.</para>
    /// </summary>
    public class InnerVoiceContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";

        private static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { DataRoot }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        private static InnerVoiceLibrary Library()
        {
            var lib = Resources.Load<InnerVoiceLibrary>(InnerVoiceLibrary.ResourcesPath);
            Assert.IsNotNull(lib,
                $"Resources/{InnerVoiceLibrary.ResourcesPath}.asset is missing — the self-installing " +
                "InnerVoiceDirector loads its lines from there and would have nothing to say");
            return lib;
        }

        // ---- the lines --------------------------------------------------------------------------

        [Test]
        public void Lines_Exist_AndHaveNonEmptyUniqueNamespacedIds()
        {
            var lines = LoadAll<InnerVoiceLineDef>();
            Assert.IsNotEmpty(lines, "the clue channel must ship at least one authored line");

            var seen = new Dictionary<string, string>();
            foreach (InnerVoiceLineDef d in lines)
            {
                string path = AssetDatabase.GetAssetPath(d);
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.Id), $"{path}: InnerVoiceLineDef has an empty id");
                Assert.IsTrue(d.Id.StartsWith("innervoice."),
                    $"{path}: id '{d.Id}' must be namespaced 'innervoice.snake_case' — it is also the " +
                    "save-flag key, so it is append-only and stable");
                Assert.IsFalse(seen.ContainsKey(d.Id),
                    $"duplicate InnerVoiceLineDef id '{d.Id}' in '{path}' and " +
                    $"'{(seen.TryGetValue(d.Id, out string o) ? o : "?")}' — two clues sharing one id " +
                    "share one save flag, so hearing either silences both");
                seen[d.Id] = path;
            }
        }

        [Test]
        public void Lines_HaveSomethingToSay()
        {
            foreach (InnerVoiceLineDef d in LoadAll<InnerVoiceLineDef>())
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.Line),
                    $"{AssetDatabase.GetAssetPath(d)}: '{d.Id}' has no line — the director skips it " +
                    "silently, which is a clue that looks authored and never fires");
        }

        [Test]
        public void EachTriggerHasTheFieldsItReads()
        {
            foreach (InnerVoiceLineDef d in LoadAll<InnerVoiceLineDef>())
            {
                string path = AssetDatabase.GetAssetPath(d);
                if (d.Trigger == InnerVoiceTrigger.InteractOffered)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(d.SubjectId),
                        $"{path}: '{d.Id}' fires on an interact offer but names no SubjectId, so nothing " +
                        "can ever match it");
                }
                else
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(d.RegionId),
                        $"{path}: '{d.Id}' fires on proximity but names no RegionId — positions are " +
                        "region-local, so an unregioned point would fire in the wrong place or nowhere");
                    Assert.That(d.RadiusMeters, Is.GreaterThan(0f),
                        $"{path}: '{d.Id}' has a zero radius");
                }
            }
        }

        // ---- reachability -------------------------------------------------------------------------

        [Test]
        public void EveryAuthoredLine_IsInTheLibraryTheDirectorLoads()
        {
            InnerVoiceLibrary lib = Library();
            var listed = new HashSet<string>();
            foreach (InnerVoiceLineDef d in lib.Lines)
                if (d != null) listed.Add(d.Id);

            foreach (InnerVoiceLineDef d in LoadAll<InnerVoiceLineDef>())
                Assert.IsTrue(listed.Contains(d.Id),
                    $"{AssetDatabase.GetAssetPath(d)}: '{d.Id}' is authored but is not in " +
                    $"Resources/{InnerVoiceLibrary.ResourcesPath} — the director will never load it and " +
                    "nothing else in the project would ever say so");
        }

        [Test]
        public void TheLibraryHasNoHoles()
        {
            InnerVoiceLibrary lib = Library();
            Assert.IsNotEmpty(lib.Lines, "the library lists no lines");
            for (int i = 0; i < lib.Lines.Length; i++)
                Assert.IsNotNull(lib.Lines[i],
                    $"Resources/{InnerVoiceLibrary.ResourcesPath}: entry {i} is a missing reference — " +
                    "usually a def that was deleted or moved without updating the library");
        }

        // ---- the voices they name --------------------------------------------------------------------

        /// <summary>
        /// ⭐ <b>Every voice asset must actually LOAD as a voice def.</b> This is not paranoia — it is the
        /// bug this PR hit, and it is invisible from every other angle.
        ///
        /// <para>Unity gives a <c>.cs</c> file exactly ONE MonoScript, and it is the type whose name
        /// matches the FILE. <c>DialogueVoiceDef</c> lived in <c>DialogueVoice.cs</c> beside the struct,
        /// so an asset's script reference resolved to the STRUCT and the asset loaded as <b>null</b> —
        /// with an assert in the log and nothing else. The def had shipped with no assets authored
        /// against it, so nothing had ever caught it, and every OTHER content check in this file passed:
        /// they read <c>d.Voice</c>, which was null, and null is a legal "no voice authored".</para>
        ///
        /// <para>Loading by PATH is the whole point. <c>FindAssets("t:DialogueVoiceDef")</c> would not
        /// have found a broken one at all, and an empty sweep is a green.</para>
        /// </summary>
        [Test]
        public void EveryAssetInTheVoicesFolder_LoadsAsAVoiceDef()
        {
            const string folder = DataRoot + "/NPCs/Voices";
            Assert.IsTrue(System.IO.Directory.Exists(folder), $"{folder} does not exist");

            string[] files = System.IO.Directory.GetFiles(folder, "*.asset");
            Assert.IsNotEmpty(files, $"{folder} holds no voice assets");

            foreach (string file in files)
            {
                string path = file.Replace('\\', '/');
                var voice = AssetDatabase.LoadAssetAtPath<DialogueVoiceDef>(path);
                Assert.IsNotNull(voice,
                    $"{path} did not load as a DialogueVoiceDef. The usual cause is that the type is " +
                    "not in a .cs file of its own name, so the asset's m_Script resolves to a different " +
                    "type in the same file and the asset silently deserialises to null.");
                Assert.IsTrue(voice.Id != null && voice.Id.StartsWith("voice."),
                    $"{path}: id '{voice.Id}' must be namespaced 'voice.snake_case'");
            }
        }

        /// <summary>
        /// The other half of the same trap: a line that NAMES a voice must have it resolve. A broken
        /// reference makes <c>Voice</c> null, which every other check here reads as the legal
        /// "unauthored", so this asserts against the file's own bytes instead.
        /// </summary>
        [Test]
        public void EveryLineThatReferencesAVoice_ActuallyResolvesIt()
        {
            foreach (InnerVoiceLineDef d in LoadAll<InnerVoiceLineDef>())
            {
                string path = AssetDatabase.GetAssetPath(d);
                string yaml = System.IO.File.ReadAllText(path);

                // "Voice: {fileID: 0}" is an authored empty; anything else is a reference that has to work.
                bool referencesOne = yaml.Contains("Voice: {fileID: 11400000");
                if (!referencesOne) continue;

                Assert.IsNotNull(d.Voice,
                    $"{path}: '{d.Id}' names a voice asset in its YAML but the loaded object's Voice is " +
                    "null — the reference is broken, and the line would silently read at the default " +
                    "cadence instead of the one it was authored with");
            }
        }

        [Test]
        public void EveryVoiceALineNames_HasANamespacedId()
        {
            foreach (InnerVoiceLineDef d in LoadAll<InnerVoiceLineDef>())
            {
                if (d.Voice == null) continue;      // legal: that line reads at the default cadence
                Assert.IsTrue(d.Voice.Id != null && d.Voice.Id.StartsWith("voice."),
                    $"{AssetDatabase.GetAssetPath(d)}: '{d.Id}' names a voice whose id " +
                    $"('{d.Voice.Id}') is not 'voice.snake_case' — the id is what crosses Core, so an " +
                    "unnamespaced one silently resolves to the default cadence");
            }
        }

        // ---- the shipped subjects still exist ---------------------------------------------------------

        /// <summary>
        /// ⭐ A clue keyed to an interact id that nothing publishes is the quiet failure this channel is
        /// most prone to: the def is valid, the library lists it, the director loads it, and the line
        /// never fires because the thing was renamed or retired. There is no registry of live interact
        /// ids to check against, so this greps the code that mints them — which is exactly where a rename
        /// would show up.
        /// </summary>
        [Test]
        public void EveryOfferSubject_IsStillMintedSomewhereInTheProject()
        {
            // Read the tree ONCE. Re-reading it per def would be a few hundred files times a few clues,
            // and this runs on every CI job.
            var all = new System.Text.StringBuilder();
            foreach (string file in System.IO.Directory.GetFiles("Assets/_Project/Code", "*.cs",
                                                                 System.IO.SearchOption.AllDirectories))
                all.Append(System.IO.File.ReadAllText(file)).Append('\n');
            string corpus = all.ToString();

            foreach (InnerVoiceLineDef d in LoadAll<InnerVoiceLineDef>())
            {
                if (d.Trigger != InnerVoiceTrigger.InteractOffered) continue;

                Assert.IsTrue(corpus.Contains(d.SubjectId),
                    $"'{d.Id}' waits on the interact id '{d.SubjectId}', which no file under " +
                    "Assets/_Project/Code mentions any more — the thing was renamed or retired and the " +
                    "clue is now dead");
            }
        }

        // ---- the pool that draws them -------------------------------------------------------------------

        [Test]
        public void TheAmbientBubbleCap_CanExpressAPairTalking()
        {
            Assert.That(DialogueBubbleKit.AmbientBubbleCap, Is.GreaterThanOrEqualTo(2),
                "two is the floor: with a cap of one, the second speaker would evict the first " +
                "mid-word and an exchange could never read as an exchange");
            Assert.That(AmbientSpeechPresenter.MaxConcurrent,
                        Is.EqualTo(DialogueBubbleKit.AmbientBubbleCap),
                        "the pool size and the kit's declared cap must be the same number");
        }

        [Test]
        public void TheAmbientCanvasDrawsUnderTheModalBubble()
        {
            // 110 is DialoguePresenter's own sorting order. An overheard line must never cover the
            // conversation the player chose to be in.
            Assert.That(AmbientSpeechPresenter.SortingOrder, Is.LessThan(110));
        }
    }
}
