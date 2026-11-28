using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The shipped idle fidgets, as authored</b>: the library under <c>Resources</c> and every
    /// <see cref="IdleFidgetDef"/> under <c>Data</c>. One entity per file, stable ids, every fidget listed
    /// once, and each one drawable the way its channel says (a Look fidget's gaze track starts and ends on
    /// the figure's own look; a Clip fidget names its clip).
    /// </summary>
    public sealed class IdleFidgetContentValidationTests
    {
        private const string LibraryPath = "Assets/_Project/Resources/IdleFidgetLibrary.asset";
        private static readonly string[] DataRoots = { "Assets/_Project/Data" };
        private static readonly Regex IdShape = new Regex(@"^idle\.[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);

        /// <summary>A skin's state key is the rig's ANIMS name (<c>castBack</c>, <c>mountUp</c>), so an idle
        /// variant is <c>idle</c> + a capitalised word.</summary>
        private static readonly Regex ClipShape = new Regex(@"^idle[A-Z][A-Za-z]*$", RegexOptions.CultureInvariant);

        /// <summary>The fidgets the owner asked for by name (2026-11-26): they must exist, drawable or not.</summary>
        private static readonly string[] Asked = { "idle.weight_shift", "idle.look_around", "idle.scratch_head" };

        /// <summary>The no-repeat rule needs three drawable fidgets (<see cref="IdleFidgetSchedule"/>), and
        /// only Look fidgets are drawable on every rig today.</summary>
        private const int LeastLookFidgets = 3;

        private static IdleFidgetLibraryDef LoadLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<IdleFidgetLibraryDef>(LibraryPath);
            Assert.IsNotNull(lib, $"no IdleFidgetLibraryDef at {LibraryPath}, so nobody fidgets");
            return lib;
        }

        private static List<IdleFidgetDef> AllDefs()
        {
            var all = new List<IdleFidgetDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:IdleFidgetDef", DataRoots))
            {
                var d = AssetDatabase.LoadAssetAtPath<IdleFidgetDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (d != null) all.Add(d);
            }
            return all;
        }

        [Test]
        public void TheLibraryLoadsWhereTheRuntimeLooksAndItsPaceIsSane()
        {
            IdleFidgetLibraryDef lib = LoadLibrary();
            Assert.AreEqual("idlefidgets.library", lib.Id);
            Assert.AreEqual(LibraryPath, "Assets/_Project/Resources/" + IdleFidgetLibraryDef.ResourcesPath + ".asset",
                "the runtime's Resources path and the asset's place disagree");
            Assert.IsTrue(lib.Enabled, "the shipped library is switched off");
            Assert.GreaterOrEqual(lib.MinGapSeconds, 0f);
            Assert.GreaterOrEqual(lib.MaxGapSeconds, lib.MinGapSeconds, "the most gap is under the least");
            Assert.GreaterOrEqual(lib.SettleSeconds, 0f);
            Assert.Greater(lib.LookDistanceMetres, 0f);
        }

        [Test]
        public void EveryFidgetIsListedOnceAndEveryListedFidgetIsADataAsset()
        {
            IdleFidgetLibraryDef lib = LoadLibrary();
            List<IdleFidgetDef> all = AllDefs();
            Assert.Greater(all.Count, 0, "no IdleFidgetDef under Data, so nothing was checked");
            Assert.IsNotNull(lib.Fidgets);

            var listed = new HashSet<IdleFidgetDef>();
            foreach (IdleFidgetDef d in lib.Fidgets)
            {
                Assert.IsNotNull(d, "the library lists a missing fidget");
                Assert.IsTrue(listed.Add(d), $"the library lists {d.Id} twice");
            }
            foreach (IdleFidgetDef d in all)
                Assert.IsTrue(listed.Contains(d), $"{AssetDatabase.GetAssetPath(d)} ({d.Id}) is not in the library");
            Assert.AreEqual(all.Count, listed.Count, "the library lists a fidget that is not under Data");
        }

        [Test]
        public void IdsAreStableShapedUniqueAndTheAskedOnesExist()
        {
            var ids = new HashSet<string>();
            foreach (IdleFidgetDef d in AllDefs())
            {
                Assert.IsTrue(IdShape.IsMatch(d.Id ?? ""), $"{AssetDatabase.GetAssetPath(d)}: id '{d.Id}' is not idle.snake_case");
                Assert.IsTrue(ids.Add(d.Id), $"id {d.Id} is used twice");
            }
            foreach (string id in Asked)
                Assert.IsTrue(ids.Contains(id), $"the asked-for fidget {id} is missing");
        }

        [Test]
        public void EachFidgetIsDrawableTheWayItsChannelSays()
        {
            int looks = 0;
            foreach (IdleFidgetDef d in AllDefs())
            {
                string where = $"{d.Id} ({AssetDatabase.GetAssetPath(d)})";
                Assert.Greater(d.Weight, 0f, $"{where}: weight 0 ships a fidget nobody does");
                Assert.Greater(d.DurationSeconds, 0f, $"{where}: no length");

                if (d.Kind == IdleFidgetDef.Channel.Look)
                {
                    looks++;
                    Assert.IsNotNull(d.Gaze, where);
                    Assert.GreaterOrEqual(d.Gaze.Length, 2, $"{where}: a gaze track needs a start and an end");
                    Assert.IsTrue(string.IsNullOrEmpty(d.ClipKey), $"{where}: a Look fidget names a clip");
                    IdleFidgetDef.GazeKey first = d.Gaze[0], last = d.Gaze[d.Gaze.Length - 1];
                    Assert.AreEqual(0f, first.AtSeconds, $"{where}: the track does not start at 0 s");
                    Assert.IsTrue(first.YawDegrees == 0f && first.UpDegrees == 0f,
                        $"{where}: the track does not start on the figure's own look, so it would snap in");
                    Assert.IsTrue(last.YawDegrees == 0f && last.UpDegrees == 0f,
                        $"{where}: the track does not end on the figure's own look, so it would snap out");
                    Assert.LessOrEqual(last.AtSeconds, d.DurationSeconds, $"{where}: the track outlasts the fidget");
                    for (int i = 1; i < d.Gaze.Length; i++)
                        Assert.Greater(d.Gaze[i].AtSeconds, d.Gaze[i - 1].AtSeconds, $"{where}: key {i} is not after key {i - 1}");
                }
                else
                {
                    Assert.IsFalse(string.IsNullOrEmpty(d.ClipKey), $"{where}: a Clip fidget names no clip");
                    Assert.IsTrue(ClipShape.IsMatch(d.ClipKey),
                        $"{where}: clip '{d.ClipKey}' is not named as the rig names its clips (camelCase, idle + Variant)");
                    Assert.IsTrue(d.Gaze == null || d.Gaze.Length == 0, $"{where}: a Clip fidget carries a gaze track");
                }
            }
            Assert.GreaterOrEqual(looks, LeastLookFidgets,
                "fewer Look fidgets than the no-repeat rule needs on a rig with no fidget clips");
        }
    }
}
