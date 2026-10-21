using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>ONE PATCH TO ONE ROOT, EVERY DELETION NAMED, AND A SECOND WRITE CHANGES NOTHING</b>
    /// (terrain pass 9, PR 4b). Subject: <see cref="StPetersLayerRefresh"/>'s patches.
    ///
    /// <para>Each step is planned against a COPY of the committed scene, from an input changed on
    /// purpose so that its patch deletes, moves and adds: one object dropped, one nudged, one new. The
    /// patch is written into the copy's file as the menu writes it, then written again. Read back with
    /// the test's own reader: every document outside the step's root is byte-identical and in its
    /// order; the documents that left the scene are exactly the patch's deletions, and each deletion's
    /// name says which object it was; the second write changes nothing; and the step, planned again on
    /// the patched copy, has nothing left to do. A patch planned before the file changed refuses to
    /// write over it. The committed scene is never written: it is hashed before each test and after.</para>
    /// </summary>
    public class StPetersLayerRefreshPatchTests
    {
        static readonly string CopyPath =
            Path.Combine(Path.GetTempPath(), "hh-st-peters-layer-refresh-tests", "StPeters.copy.unity");

        string _committedHash;
        GameObject _terrainGo;

        [SetUp]
        public void SetUp()
        {
            _committedHash = LayerRefreshPatchChecks.Sha256(StPetersLayerRefreshTests.ScenePath);
            Directory.CreateDirectory(Path.GetDirectoryName(CopyPath));
            File.Copy(StPetersLayerRefreshTests.ScenePath, CopyPath, true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            if (File.Exists(CopyPath)) File.Delete(CopyPath);
        }

        void CommittedSceneUnchanged() =>
            Assert.AreEqual(_committedHash, LayerRefreshPatchChecks.Sha256(StPetersLayerRefreshTests.ScenePath),
                            "the committed scene changed; the tests write only to their copy");

        [Test]
        public void ClamHolesPatch_StaysInItsRoot_NamesEveryDeletion_AndWritesOnce()
        {
            List<Vector2> holes = LayerRefreshPatchChecks.ChangedOnPurpose(
                LayerRefreshPatchChecks.CommittedHoles(File.ReadAllText(CopyPath)));
            LayerRefreshPatchChecks.PatchHoldsToItsRoot(CopyPath, StPetersLayerRefresh.ClamHolesRootName, positioned: true,
                text => StPetersLayerRefresh.ClamHoles(StPetersLayerRefresh.SceneYaml.Parse(text), holes));
            CommittedSceneUnchanged();
        }

        [Test]
        public void ShorelinePatch_StaysInItsRoot_NamesEveryDeletion_AndWritesOnce()
        {
            string committed = File.ReadAllText(CopyPath);
            List<StPetersLayerRefresh.PlacedRock> rocks = LayerRefreshPatchChecks.ChangedOnPurpose(
                LayerRefreshPatchChecks.CommittedRocks(committed));
            var sprites = new ShoreRockSwapChecks.TestShoreRockSprites(committed);
            LayerRefreshPatchChecks.PatchHoldsToItsRoot(CopyPath, StPetersShorePainter.RootName, positioned: true,
                text => StPetersLayerRefresh.Shoreline(StPetersLayerRefresh.SceneYaml.Parse(text), rocks, sprites,
                                                       Array.Empty<ShoreRockDef>()));
            CommittedSceneUnchanged();
        }

        [Test]
        public void NavMarksPatch_StaysInItsRoot_NamesEveryDeletion_AndWritesOnce()
        {
            _terrainGo = new GameObject("TidalTerrain_LayerRefreshPatchTest");
            var terrain = _terrainGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);   // the same zones the scene is built with
            NavMarkPlanResult plan = LayerRefreshPatchChecks.ChangedOnPurpose(StPetersNavMarks.Plan(terrain),
                                                                              File.ReadAllText(CopyPath));
            var assets = new StPetersLayerRefresh.EditorNavMarkAssets();
            LayerRefreshPatchChecks.PatchHoldsToItsRoot(CopyPath, StPetersNavMarks.RootName, positioned: false,
                text => StPetersLayerRefresh.NavMarks(StPetersLayerRefresh.SceneYaml.Parse(text), plan, assets));
            CommittedSceneUnchanged();
        }
    }

    /// <summary>
    /// The patch checks, as plain functions of a scene file and a planning function, so the same checks
    /// can be run against a deliberately broken step outside Unity.
    /// </summary>
    internal static class LayerRefreshPatchChecks
    {
        /// <summary>A nudge well inside the steps' same-object tolerance (a millimetre): the object
        /// moves, it is not replaced.</summary>
        static readonly Vector2 Nudge = new Vector2(0.0004f, 0f);

        /// <summary>Where the new object goes, from the last one: well clear of every other.</summary>
        static readonly Vector2 NewOffset = new Vector2(0.37f, 0.21f);

        static readonly Regex AtRx = new Regex(@" @\((-?[0-9][^,()]*), (-?[0-9][^,()]*)\)", RegexOptions.CultureInvariant);

        internal static string Sha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream file = File.OpenRead(path))
                return string.Concat(sha.ComputeHash(file).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }

        static float Num(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        // ---- the inputs, changed on purpose ------------------------------------------------------------

        internal static List<Vector2> CommittedHoles(string sceneText)
        {
            var t = new LayerRefreshSceneText(sceneText);
            return t.ChildGameObjects(t.RootGameObject(StPetersLayerRefresh.ClamHolesRootName))
                    .Select(go => (Vector2)t.Vec3(t.TransformOf(go), "m_LocalPosition"))
                    .ToList();
        }

        internal static List<StPetersLayerRefresh.PlacedRock> CommittedRocks(string sceneText)
        {
            var t = new LayerRefreshSceneText(sceneText);
            long rocks = t.ChildNamed(t.RootGameObject(StPetersShorePainter.RootName), StPetersShorePainter.RocksRootName);
            return t.ChildGameObjects(rocks).Select(go =>
            {
                string name = t.Field(go, "m_Name");
                return new StPetersLayerRefresh.PlacedRock
                {
                    Name = name,
                    Position = t.Vec3(t.TransformOf(go), "m_LocalPosition"),
                    SpriteKey = name.Substring(name.LastIndexOf('_') + 1),   // the painter names a rock <prefix>_<key>
                };
            }).ToList();
        }

        /// <summary>The first hole dropped, the second nudged, and a new one past the last.</summary>
        internal static List<Vector2> ChangedOnPurpose(List<Vector2> holes)
        {
            Assert.That(holes.Count, Is.GreaterThanOrEqualTo(2), "the committed root holds too few holes to change");
            List<Vector2> input = holes.Skip(1).ToList();
            input[0] += Nudge;
            input.Add(holes[holes.Count - 1] + NewOffset);
            return input;
        }

        /// <summary>The first rock dropped, the second nudged, and a new one like the last, past it.</summary>
        internal static List<StPetersLayerRefresh.PlacedRock> ChangedOnPurpose(List<StPetersLayerRefresh.PlacedRock> rocks)
        {
            Assert.That(rocks.Count, Is.GreaterThanOrEqualTo(2), "the committed root holds too few rocks to change");
            List<StPetersLayerRefresh.PlacedRock> input = rocks.Skip(1).ToList();
            StPetersLayerRefresh.PlacedRock nudged = input[0];
            nudged.Position += Nudge;
            input[0] = nudged;
            StPetersLayerRefresh.PlacedRock fresh = rocks[rocks.Count - 1];
            fresh.Position += NewOffset;
            input.Add(fresh);
            return input;
        }

        /// <summary>
        /// Today's plan with its last mark dropped (unless <paramref name="drop"/> is false), another
        /// moved two metres, and a new mark: a copy of the one the root holds first, three metres north
        /// of it (a new mark is copied from the root's first, so it must wear that mark's dressing).
        /// </summary>
        internal static NavMarkPlanResult ChangedOnPurpose(NavMarkPlanResult today, string sceneText, bool drop = true)
        {
            List<PlannedNavMark> marks = today.Marks.ToList();
            Assert.That(marks.Count, Is.GreaterThanOrEqualTo(3), "today's plan holds too few marks to change");
            var t = new LayerRefreshSceneText(sceneText);
            string first = t.ObjectName(t.ChildInstances(t.RootGameObject(StPetersNavMarks.RootName))[0]);
            int template = marks.FindIndex(m => "NavMark_" + m.Id == first);
            Assert.That(template, Is.GreaterThanOrEqualTo(0), $"the root's first mark, {first}, is not in today's plan");

            PlannedNavMark copy = marks[template];
            copy.Id += "_copy";
            copy.At += new Vector2(0f, 3f);
            int dropAt = !drop ? -1 : template == marks.Count - 1 ? 0 : marks.Count - 1;
            int move = Enumerable.Range(0, marks.Count).First(i => i != dropAt && i != template);
            PlannedNavMark moved = marks[move];
            moved.At += new Vector2(2f, 0f);
            marks[move] = moved;
            if (dropAt >= 0) marks.RemoveAt(dropAt);
            marks.Add(copy);
            return new NavMarkPlanResult { Marks = marks };
        }

        // ---- the checks --------------------------------------------------------------------------------

        /// <summary>
        /// Plan the step on the copy, write its patch into the copy as the menu writes it, then again,
        /// and check what the file holds after.
        /// </summary>
        internal static void PatchHoldsToItsRoot(string copyPath, string rootName, bool positioned,
                                                 Func<string, StPetersLayerRefresh.LayerPatch> plan)
        {
            string before = File.ReadAllText(copyPath);
            StPetersLayerRefresh.LayerPatch patch = plan(before);
            Assert.AreEqual(rootName, patch.Root, "the root the patch is for");
            Assert.That(patch.Ops.Count(o => o.Kind == StPetersLayerRefresh.OpKind.Delete), Is.GreaterThan(0),
                        "the input drops an object, and the patch deletes nothing");
            Assert.That(patch.Ops.Count(o => o.Kind == StPetersLayerRefresh.OpKind.Edit), Is.GreaterThan(0),
                        "the input moves an object, and the patch edits nothing");
            Assert.That(patch.Ops.Count(o => o.Kind == StPetersLayerRefresh.OpKind.Add), Is.GreaterThan(0),
                        "the input adds an object, and the patch adds nothing");

            Write(copyPath, patch.ApplyTo(before));
            string once = File.ReadAllText(copyPath);
            Write(copyPath, patch.ApplyTo(once));
            string twice = File.ReadAllText(copyPath);
            Assert.IsTrue(twice == once, "written a second time, the patch changed the copy again");
            Assert.IsTrue(plan(once).IsEmpty, "planned again on the patched copy, the step still has work to do");

            var a = new LayerRefreshSceneText(before);
            var b = new LayerRefreshSceneText(once);
            OnlyItsRoot(before, once, a, b, rootName);
            EveryDeletionNamed(a, b, patch, rootName, positioned);
            StaleRefused(before, patch);
        }

        /// <summary>As the menu writes the scene: UTF-8, no byte-order mark.</summary>
        static void Write(string path, string text) => File.WriteAllText(path, text, new UTF8Encoding(false));

        static string Preamble(string text) => text.Substring(0, text.IndexOf("--- !u!", StringComparison.Ordinal));

        /// <summary>Every document outside the root: still there, byte-identical, in its order; nothing
        /// new outside it. The root is walked here, by the test's reader, before and after.</summary>
        static void OnlyItsRoot(string before, string after, LayerRefreshSceneText a, LayerRefreshSceneText b, string rootName)
        {
            Assert.AreEqual(Preamble(before), Preamble(after), "the file's preamble");
            Assert.IsTrue(after.EndsWith("\n", StringComparison.Ordinal), "the file no longer ends in a newline");
            long rootA = a.RootGameObject(rootName), rootB = b.RootGameObject(rootName);
            Assert.AreEqual(rootA, rootB, $"'{rootName}' is no longer the same GameObject");

            HashSet<long> mine = a.Subtree(rootA);
            mine.UnionWith(b.Subtree(rootB));
            foreach (long id in a.Order)
            {
                if (mine.Contains(id)) continue;
                if (!b.Has(id)) Assert.Fail($"{a.ObjectName(id)} (&{id}), outside '{rootName}', left the scene");
                if (a.Text(id) != b.Text(id)) Assert.Fail($"{a.ObjectName(id)} (&{id}), outside '{rootName}', changed");
            }
            foreach (long id in b.Order)
                if (!mine.Contains(id) && !a.Has(id))
                    Assert.Fail($"{b.ObjectName(id)} (&{id}) was added outside '{rootName}'");
            CollectionAssert.AreEqual(a.Order.Where(i => !mine.Contains(i)).ToList(), b.Order.Where(i => !mine.Contains(i)).ToList(),
                                      $"the documents outside '{rootName}' did not keep their order");
        }

        /// <summary>
        /// The documents that left the scene are the patch's deletions, and each deletion is named: its
        /// name holds its object's name — and, for objects that share a name, where it stood — enough to
        /// pick out that one object under the root; and the written patch lists it by that name.
        /// </summary>
        static void EveryDeletionNamed(LayerRefreshSceneText a, LayerRefreshSceneText b, StPetersLayerRefresh.LayerPatch patch,
                                       string rootName, bool positioned)
        {
            List<long> gone = a.Order.Where(id => !b.Has(id)).ToList();
            List<StPetersLayerRefresh.Op> deletions = patch.Deletions.ToList();
            CollectionAssert.AreEquivalent(gone, deletions.Select(o => o.FileId).ToList(),
                                           "the documents that left the scene are not the patch's deletions");

            string written = patch.ToYaml();
            int from = written.IndexOf("\ndeletions:", StringComparison.Ordinal), to = written.IndexOf("\nedits:", StringComparison.Ordinal);
            Assert.IsTrue(from >= 0 && to > from, "the written patch does not list its deletions before its edits");
            string listed = written.Substring(from, to - from);

            HashSet<long> root = a.Subtree(a.RootGameObject(rootName));
            foreach (StPetersLayerRefresh.Op op in deletions)
            {
                long id = op.FileId;
                string obj = a.ObjectName(id);
                Assert.That(obj, Is.Not.Null.And.Not.Empty, $"&{id} belongs to no named object");
                StringAssert.Contains(obj, op.Name, $"the deletion of &{id} does not name its object");
                Assert.AreEqual(a.Text(id), op.Before, $"the deletion of &{id} does not record what it deletes");
                StringAssert.Contains($"- fileID: {id.ToString(CultureInfo.InvariantCulture)}\n  name: \"{op.Name}\"\n", listed,
                                      $"the written patch does not list the deletion of &{id} by its name");

                int alike;
                if (positioned)
                {
                    Match at = AtRx.Match(op.Name);
                    Assert.IsTrue(at.Success, $"the deletion of &{id} ('{op.Name}') does not say where its object stood");
                    float x = Num(at.Groups[1].Value), y = Num(at.Groups[2].Value);
                    long go = a.ClassOf(id) == 1 ? id : a.Ref(id, "m_GameObject");
                    Vector3 p = a.Vec3(a.TransformOf(go), "m_LocalPosition");
                    Assert.IsTrue(p.x == x && p.y == y, $"the deletion of &{id} ('{op.Name}') names a place its object is not at ({p.x}, {p.y})");
                    alike = root.Count(g => a.ClassOf(g) == 1 && !a.IsStripped(g) && a.Field(g, "m_Name") == obj &&
                                            a.Vec3(a.TransformOf(g), "m_LocalPosition") is Vector3 q && q.x == x && q.y == y);
                }
                else alike = root.Count(d => a.ClassOf(d) == 1001 && a.ObjectName(d) == obj);
                Assert.AreEqual(1, alike, $"the deletion of &{id} ('{op.Name}') fits {alike} objects under '{rootName}'");
            }
        }

        /// <summary>A patch written over a file whose document changed after it was planned refuses,
        /// for an edit and for a deletion alike.</summary>
        static void StaleRefused(string before, StPetersLayerRefresh.LayerPatch patch)
        {
            foreach (StPetersLayerRefresh.OpKind kind in new[] { StPetersLayerRefresh.OpKind.Edit, StPetersLayerRefresh.OpKind.Delete })
            {
                StPetersLayerRefresh.Op op = patch.Ops.First(o => o.Kind == kind);
                string doc = op.Before + "\n";
                int at = before.IndexOf(doc, StringComparison.Ordinal);
                Assert.IsTrue(at >= 0 && before.IndexOf(doc, at + 1, StringComparison.Ordinal) < 0, $"&{op.FileId} is not in the copy once");
                string stale = before.Substring(0, at) + op.Before + "\n  m_StaleProbe: 1\n" + before.Substring(at + doc.Length);
                var refusal = Assert.Throws<StPetersLayerRefresh.Refusal>(() => patch.ApplyTo(stale),
                                                                          $"the patch wrote over &{op.FileId}, which changed after it was planned");
                StringAssert.Contains("re-plan it", refusal.Message);
            }
        }
    }
}
