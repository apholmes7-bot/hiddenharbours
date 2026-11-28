using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class DemoContentClosureTests
    {
        const string BudgetPath = "Assets/Tests/EditMode/Fixtures/DemoContentBudget.json";
        const string GuidA = "11112222333344445555666677778888";
        const string GuidB = "aaaabbbbccccddddeeeeffff00001111";

        [Test]
        public void IslandSourceClosure_StaysWithinBudget_AndNamesEveryForeignReference()
        {
            var budget = JsonUtility.FromJson<DemoContentClosure.Budget>(File.ReadAllText(BudgetPath));
            CollectionAssert.AreEqual(new[] { "Assets/_Project/Scenes/StPeters.unity" }, budget.sceneRoots,
                "DemoContent.SceneRootsChanged: review the island-only build contract before changing this list.");
            Assert.Greater(budget.recordedBytes, 0, "DemoContent.EmptyBudget");
            Assert.Greater(budget.foreignFolderPrefixes.Length, 0, "DemoContent.NoForeignFolders");
            var index = RegionSceneTileRefsTests.IndexMetas(RegionSceneTileRefsTests.MetaRoots);
            var report = DemoContentClosure.Build(Directory.GetCurrentDirectory(), budget, index);
            var problems = DemoContentClosure.Violations(report, budget);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(report.Parent.ContainsKey("Assets/_Project/Resources/BoatInteriorCells/interior_tanker_iso.asset"), Is.True,
                "DemoContent.ResourcesIncomplete: all Resources content ships even if the player cannot buy that hull.");
            Assert.That(report.Parent.ContainsKey("Assets/_Project/Scenes/NineMileCreek.unity"), Is.False,
                "DemoContent.OtherSceneIncluded: editor scene lists are not demo roots.");
            Assert.That(report.Parent.Keys.Count + report.Assets.Keys.Count(p => !report.Parent.ContainsKey(p)), Is.EqualTo(report.Assets.Count));
            TestContext.WriteLine("DemoContent: {0} reachable files / {1} bytes; {2} unreachable files / {3} bytes; {4} pointer sizes.",
                report.Parent.Count, report.ReachableBytes, report.Assets.Count - report.Parent.Count, report.UnreachableBytes,
                report.Assets.Values.Count(a => a.Pointer));
        }

        [Test]
        public void Closure_FollowsPrefabMaterialSpriteChain_CountsSharedAssetsOnce_AndTerminatesCycles()
        {
            var report = new DemoContentClosure.Report();
            Add(report, "scene", 10, "prefab", "sprite");
            Add(report, "prefab", 20, "material");
            Add(report, "material", 30, "sprite", "prefab");
            Add(report, "sprite", 40);
            Add(report, "other-region", 50);
            report.Roots.Add("scene", "scene");
            DemoContentClosure.Traverse(report);
            CollectionAssert.AreEquivalent(new[] { "scene", "prefab", "material", "sprite" }, report.Parent.Keys);
            Assert.That(report.ReachableBytes, Is.EqualTo(100));
            Assert.That(report.UnreachableBytes, Is.EqualTo(50));
            Assert.That(report.Chain("material"), Is.EqualTo("scene -> prefab -> material"));
            Assert.That(report.Unresolved, Is.Empty);
        }

        [TestCase("Assets/Art/Resources/later-place.ogg", true)]
        [TestCase("Assets/StreamingAssets/NineMileCreek/floor.bin", true)]
        [TestCase("Assets/Editor/Resources/preview.png", false)]
        [TestCase("Assets/Tests/Resources/fixture.png", false)]
        [TestCase("Assets/Art/Evidence~/plate.png", false)]
        [TestCase("Assets/Art/.cache/plate.png", false)]
        [TestCase("Assets/Art/leaf.png.meta", false)]
        [TestCase("Assets/Art/leaf.tmp", false)]
        public void PlayerInventory_ExcludesEditorTestsAndIgnoredFiles(string path, bool included)
        {
            Assert.That(DemoContentClosure.IsPlayerSource(path), Is.EqualTo(included));
        }

        [Test]
        public void LfsPointer_UsesDeclaredObjectBytes_NotThePointerLength()
        {
            string pointer = "version https://git-lfs.github.com/spec/v1\noid sha256:" + new string('a', 64) + "\nsize 4294967296\n";
            Assert.That(DemoContentClosure.TryPointerSize(pointer, out long bytes), Is.True);
            Assert.That(bytes, Is.EqualTo(4294967296L));
            Assert.That(DemoContentClosure.TryPointerSize(pointer.Replace("\n", "\r\n"), out bytes), Is.True);
            Assert.That(bytes, Is.EqualTo(4294967296L));
            Assert.That(DemoContentClosure.TryPointerSize("size 42\n", out bytes), Is.False);
            Assert.Throws<InvalidDataException>(() => DemoContentClosure.TryPointerSize(pointer.Replace("size 4294967296", "size -1"), out _));
        }

        [Test]
        public void Reader_TracksEverySceneObject_DeduplicatesOnlyWithinThatObject_AndIgnoresBuiltins()
        {
            var refs = DemoContentClosure.ReadReferences("island.unity", new[]
            {
                "--- !u!212 &42", "  m_Sprite: {fileID: 1, guid: " + GuidA + ", type: 3}",
                "  another: {guid: " + GuidA + "}", "--- !u!212 &43", "  m_Sprite: {guid: " + GuidA + "}",
                "  m_Material: {guid: 0000000000000000f000000000000000}"
            }, Index());
            Assert.That(refs.Count, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { "42", "43" }, refs.Select(r => r.ObjectId));
            Assert.That(refs[0].Target, Is.EqualTo("Assets/Art/NineMileCreek/wall.png"));
            Assert.That(refs[0].Line, Is.EqualTo(2));
        }

        [Test]
        public void ImporterReferences_AreFollowed_ButItsOwnGuidIsNotAReference()
        {
            var refs = DemoContentClosure.ReadReferences("sheet.png.meta", new[]
                { "guid: " + GuidA, "  secondaryTexture: {guid: " + GuidB + "}" }, Index());
            Assert.That(refs.Count, Is.EqualTo(1));
            Assert.That(refs[0].Target, Is.EqualTo("Assets/Art/shared.png"));
        }

        [Test]
        public void BuildSettings_OnlyRootsConfigObjects_NotOtherEnabledScenes()
        {
            var refs = DemoContentClosure.ReadReferences("ProjectSettings/EditorBuildSettings.asset", new[]
            {
                "  m_Scenes:", "  - enabled: 1", "    guid: " + GuidA,
                "  m_configObjects:", "    input: {guid: " + GuidB + "}"
            }, Index());
            Assert.That(refs.Count, Is.EqualTo(1));
            Assert.That(refs[0].Guid, Is.EqualTo(GuidB));
        }

        [Test]
        public void JsonAssetReferences_AreFollowed()
        {
            var refs = DemoContentClosure.ReadReferences("graph.shadergraph", new[] { "{\"m_AssetGUID\": \"" + GuidB + "\"}" }, Index());
            Assert.That(refs.Single().Target, Is.EqualTo("Assets/Art/shared.png"));
        }

        [Test]
        public void Budget_AllowsExactlyTheRecordedMargin_FailsAtOneByteOver_WithNamedMessage()
        {
            var budget = EmptyBudget();
            budget.recordedBytes = 100;
            budget.growthMarginBytes = 10;
            var report = new DemoContentClosure.Report();
            Add(report, "scene", 110);
            report.Roots.Add("scene", "scene");
            DemoContentClosure.Traverse(report);
            Assert.That(DemoContentClosure.Violations(report, budget), Is.Empty);
            report.Assets["scene"].Bytes++;
            StringAssert.StartsWith("DemoContent.ReachableSizeGrew: 111 bytes exceeds 110", DemoContentClosure.Violations(report, budget).Single());
        }

        [Test]
        public void ForeignFolderGuard_UsesFolderSegments_NotAnAssetNameOrPartialWord()
        {
            string[] prefixes = { "NineMileCreek", "Cumberland" };
            Assert.That(DemoContentClosure.IsForeignFolder("Assets/Art/NineMileCreekRoads/tile.asset", prefixes), Is.True);
            Assert.That(DemoContentClosure.IsForeignFolder("Assets/Art/Cumberland/wall.png", prefixes), Is.True);
            Assert.That(DemoContentClosure.IsForeignFolder("Assets/Shared/NineMileCreek.asset", prefixes), Is.False);
            Assert.That(DemoContentClosure.IsForeignFolder("Assets/Art/NotCumberland/wall.png", prefixes), Is.False);
        }

        [Test]
        public void KnownException_OnlyPermitsTheExactObjectAndTarget_AndBecomesStaleWhenRemoved()
        {
            var budget = EmptyBudget();
            var report = new DemoContentClosure.Report();
            var reference = new DemoContentClosure.Reference { Source = "island", ObjectId = "42", Target = "foreign", Line = 7 };
            report.ForeignSceneReferences.Add(reference);
            StringAssert.Contains("DemoContent.ForeignSceneReference: island:7 object &42 -> foreign", DemoContentClosure.Violations(report, budget).Single());
            budget.knownExceptions = new[] { new DemoContentClosure.KnownException
                { rule = "ForeignSceneReference", source = "island", objectId = "42", target = "foreign", reason = "Shared wall already wired in the island scene." } };
            Assert.That(DemoContentClosure.Violations(report, budget), Is.Empty);
            report.ForeignSceneReferences.Add(new DemoContentClosure.Reference { Source = "island", ObjectId = "43", Target = "foreign" });
            StringAssert.Contains("object &43", DemoContentClosure.Violations(report, budget).Single());
            report.ForeignSceneReferences.Clear();
            StringAssert.StartsWith("DemoContent.StaleKnownException:", DemoContentClosure.Violations(report, budget).Single());
        }

        [Test]
        public void MissingDependencyAndMissingRoot_CannotSilentlyShrinkTheClosure()
        {
            var report = new DemoContentClosure.Report();
            Add(report, "scene", 0, "gone");
            report.Roots.Add("scene", "scene");
            DemoContentClosure.Traverse(report);
            Assert.That(report.Unresolved.Count, Is.EqualTo(1));
            StringAssert.StartsWith("DemoContent.UnresolvedReference:", DemoContentClosure.Violations(report, EmptyBudget()).Single());
            report.Roots.Add("missing scene", "scene");
            StringAssert.StartsWith("DemoContent.MissingRoot:", Assert.Throws<InvalidDataException>(() => DemoContentClosure.Traverse(report)).Message);
        }

        [Test]
        public void ExternalPackageException_DoesNotPermitADifferentUnresolvedGuid()
        {
            var budget = EmptyBudget();
            budget.knownExceptions = new[] { new DemoContentClosure.KnownException
                { rule = "ExternalPackageGuid", target = GuidA, reason = "A package-owned default material." } };
            var report = new DemoContentClosure.Report();
            report.Unresolved.Add(new DemoContentClosure.Reference { Source = "scene", ObjectId = "7", Guid = GuidA });
            Assert.That(DemoContentClosure.Violations(report, budget), Is.Empty);
            report.Unresolved.Add(new DemoContentClosure.Reference { Source = "scene", ObjectId = "8", Guid = GuidB });
            StringAssert.Contains(GuidB, DemoContentClosure.Violations(report, budget).Single());
        }

        [Test]
        public void DiskReader_RootsUnreferencedResourcesAndStreaming_AndFollowsImporterInputs()
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(), "Evidence~", "demo-content-tests",
                "closure-" + Guid.NewGuid().ToString("N"));
            string[] files = { "Assets/island.unity", "Assets/Resources/unused.asset", "Assets/StreamingAssets/other.bin",
                "Assets/input.png", "Assets/recipe.hhterrain", "Assets/Editor/Resources/preview.asset", "Assets/other.unity" };
            try
            {
                foreach (string path in files)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, path)));
                    File.WriteAllText(Path.Combine(root, path), "source");
                }
                var budget = EmptyBudget();
                budget.sceneRoots = new[] { "Assets/island.unity", "Assets/recipe.hhterrain" };
                budget.importerInputs = new[] { new DemoContentClosure.ImporterInputs
                    { asset = "Assets/recipe.hhterrain", importer = "Assets/Editor/importer.cs", sources = new[] { "Assets/input.png" } } };
                var report = DemoContentClosure.Build(root, budget, new Dictionary<string, string>());
                Assert.That(Path.IsPathRooted(root), Is.True);
                Assert.That(report.Problems, Is.Empty);
                Assert.That(report.Unresolved, Is.Empty);
                CollectionAssert.AreEquivalent(new[] { "Assets/island.unity", "Assets/recipe.hhterrain", "Assets/input.png",
                    "Assets/Resources/unused.asset", "Assets/StreamingAssets/other.bin" }, report.Parent.Keys);
                Assert.That(report.Assets.ContainsKey("Assets/Editor/Resources/preview.asset"), Is.False);
                Assert.That(report.Parent.ContainsKey("Assets/other.unity"), Is.False);
                Assert.That(report.ReachableBytes, Is.EqualTo(30));
                Assert.That(report.UnreachableBytes, Is.EqualTo(6));
                Assert.That(report.Chain("Assets/input.png"), Is.EqualTo("Assets/recipe.hhterrain -> Assets/input.png"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void ManifestContract_IgnoresFormattingAndObjectOrder_ButPinsEveryValue()
        {
            const string original = "{\"dependencies\":{\"package.a\":\"1\",\"package.b\":\"2\"},\"testables\":[\"a\",\"b\"]}";
            const string reordered = "{\r\n  \"testables\": [\"a\", \"b\"], \"dependencies\": {\"package.b\": \"2\", \"package.a\": \"1\"}\r\n}";
            string hash = DemoContentClosure.ManifestHash(original);
            Assert.That(DemoContentClosure.ManifestHash(reordered), Is.EqualTo(hash));
            Assert.That(DemoContentClosure.ManifestHash(original.Replace("\"1\"", "\"3\"")), Is.Not.EqualTo(hash));
            Assert.That(DemoContentClosure.ManifestHash(original.Replace("package.a", "package.c")), Is.Not.EqualTo(hash));
            Assert.That(DemoContentClosure.ManifestHash(original.Replace("[\"a\",\"b\"]", "[\"b\",\"a\"]")), Is.Not.EqualTo(hash));
            Assert.That(DemoContentClosure.ManifestHash(original.Replace("\"testables\"", "\"scopedRegistries\"")), Is.Not.EqualTo(hash));
            Assert.Throws<FormatException>(() => DemoContentClosure.ManifestHash(original + " broken"));
        }

        [Test]
        public void ManifestContract_IgnoresOnlyTheHostToolchain_AndAnyOtherAddedPackageStillTrips()
        {
            const string original = "{\"dependencies\":{\"package.a\":\"1\",\"package.b\":\"2\"},\"testables\":[\"a\",\"b\"]}";
            string hash = DemoContentClosure.ManifestHash(original);
            string withToolchain = original.Replace("\"package.b\":\"2\"", "\"package.b\":\"2\"," +
                string.Join(",", DemoContentClosure.HostToolchainPackages.Select(p => "\"" + p + "\":\"1.1.1\"")));
            Assert.That(DemoContentClosure.HostToolchainPackages, Has.Length.EqualTo(2));
            Assert.That(DemoContentClosure.ManifestHash(withToolchain), Is.EqualTo(hash));
            Assert.That(DemoContentClosure.ManifestHash(original.Replace("\"package.b\":\"2\"",
                "\"package.b\":\"2\",\"com.unity.sdk.linux-x86_64-extra\":\"1.1.1\"")), Is.Not.EqualTo(hash));
            Assert.That(DemoContentClosure.ManifestHash(original.Replace("\"package.b\":\"2\"",
                "\"package.b\":\"2\",\"com.unity.toolchain.win-x86_64\":\"1.1.1\"")), Is.Not.EqualTo(hash));
            Assert.That(DemoContentClosure.ManifestHash(withToolchain.Replace("\"package.a\":\"1\"", "\"package.a\":\"3\"")),
                Is.Not.EqualTo(hash));
            // Only "dependencies" is filtered: the same name elsewhere is still pinned.
            Assert.That(DemoContentClosure.ManifestHash(original.Replace("[\"a\",\"b\"]",
                "[\"a\",\"b\",\"com.unity.sdk.linux-x86_64\"]")), Is.Not.EqualTo(hash));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PackageInclude_IsExplicitWithOrWithoutResolvedBytes_AndNeverWaivesAProjectReference(bool resolved)
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(), "Evidence~", "demo-content-tests",
                "includes-" + Guid.NewGuid().ToString("N"));
            const string package = "Packages/com.example.render/Library/Core.hlsl";
            const string missing = "Assets/missing.hlsl";
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Assets"));
                File.WriteAllText(Path.Combine(root, "Assets/test.shader"),
                    "#include \"" + package + "\"\n#include \"" + missing + "\"\n");
                if (resolved)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, package)));
                    File.WriteAllText(Path.Combine(root, package), "resolved external source");
                }
                var budget = EmptyBudget();
                budget.recordedBytes = new FileInfo(Path.Combine(root, "Assets/test.shader")).Length;
                budget.knownExceptions = new[] { new DemoContentClosure.KnownException
                    { rule = "ExternalPackageInclude", target = package, reason = "External include bytes are outside the project-source count." } };
                var report = DemoContentClosure.Build(root, budget, new Dictionary<string, string>());
                Assert.That(report.ReachableBytes, Is.EqualTo(budget.recordedBytes));
                CollectionAssert.AreEquivalent(new[] { package, missing }, report.Unresolved.Select(r => r.Target));
                string violation = DemoContentClosure.Violations(report, budget).Single();
                StringAssert.StartsWith("DemoContent.UnresolvedReference:", violation);
                Assert.That(violation, Does.Contain(missing));
                report.Unresolved.RemoveAll(r => r.Target == missing);
                Assert.That(DemoContentClosure.Violations(report, budget), Is.Empty);
                report.Unresolved[0].ObjectId = "serialized";
                Assert.That(DemoContentClosure.Violations(report, budget).Count, Is.EqualTo(2), "only an include may use this exception");
                report.Unresolved.Clear();
                StringAssert.StartsWith("DemoContent.StaleKnownException:", DemoContentClosure.Violations(report, budget).Single());
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        static Dictionary<string, string> Index() => new Dictionary<string, string>
        {
            [GuidA] = "Assets/Art/NineMileCreek/wall.png", [GuidB] = "Assets/Art/shared.png"
        };

        static DemoContentClosure.Budget EmptyBudget() => new DemoContentClosure.Budget
        {
            sceneRoots = Array.Empty<string>(), settingsRoots = Array.Empty<string>(), foreignFolderPrefixes = Array.Empty<string>(),
            knownExceptions = Array.Empty<DemoContentClosure.KnownException>()
        };

        static void Add(DemoContentClosure.Report report, string path, long bytes, params string[] dependencies)
        {
            var asset = new DemoContentClosure.Asset { Path = path, Bytes = bytes };
            asset.References.AddRange(dependencies.Select(d => new DemoContentClosure.Reference { Source = path, ObjectId = "0", Target = d }));
            report.Assets.Add(path, asset);
        }
    }
}
