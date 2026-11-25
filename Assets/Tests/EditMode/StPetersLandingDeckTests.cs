using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    public class StPetersLandingDeckTests
    {
        public static string ProjectRoot = Directory.GetCurrentDirectory();

        [Serializable]
        sealed class WeightGuardBaseProof
        {
            public string baseHead, movedCase, ceilingFailure, retiredNamesFailure, bakedMarkerFailure;
            public long baseBytes, branchBytes;
            public int baseAtlasNames;
            public bool baseDeckZeroZeroPresent, branchRestored;
        }

        // Explicit local three-way probe; it adds no CI case and stores no copied base fixture.
        public static void RunBaseNegativeProofFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int argument = Array.IndexOf(args, "-stpK0Proof");
            if (argument < 0 || argument + 1 >= args.Length) throw new InvalidOperationException("The weight proof needs an evidence output path.");
            string output = Path.GetFullPath(args[argument + 1]);
            string evidence = Path.GetFullPath(Path.Combine(ProjectRoot, "Evidence~/stp-kits/k0")) + Path.DirectorySeparatorChar;
            if (!output.StartsWith(evidence, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The weight proof output must stay in evidence.");
            string scenePath = Path.Combine(ProjectRoot, ScenePath);
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path == ScenePath)
                    throw new InvalidOperationException("Close the St Peters editor scene before the base-negative proof.");
            byte[] branch = File.ReadAllBytes(scenePath);
            const string baseHead = "43d0b305f8b3732236a5900151fce7a106d6dd75";
            var start = new System.Diagnostics.ProcessStartInfo("git", "show " + baseHead + ":" + ScenePath)
            {
                WorkingDirectory = ProjectRoot, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            };
            start.EnvironmentVariables["GIT_OPTIONAL_LOCKS"] = "0";
            start.EnvironmentVariables["GIT_NO_LAZY_FETCH"] = "1";
            byte[] baseline;
            using (var process = System.Diagnostics.Process.Start(start))
            using (var buffer = new MemoryStream())
            {
                process.StandardOutput.BaseStream.CopyTo(buffer);
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException("The pinned base read failed: " + error);
                baseline = buffer.ToArray();
            }
            string text = Encoding.UTF8.GetString(baseline).Replace("\r\n", "\n");
            var proof = new WeightGuardBaseProof
            {
                baseHead = baseHead, baseBytes = baseline.LongLength, branchBytes = branch.LongLength,
                movedCase = "SceneWeightGuardTests.StPetersPlantField_RetiresTheMeasuredRootsWithoutSavingItsChunks",
                baseAtlasNames = System.Text.RegularExpressions.Regex.Matches(text,
                    @"^  m_Name: Deck_\d+_\d+$", System.Text.RegularExpressions.RegexOptions.Multiline).Count,
                baseDeckZeroZeroPresent = text.Contains("  m_Name: Deck_0_0\n"),
            };
            string ExpectedFailure(Action action)
            {
                var previous = NUnit.Framework.Internal.TestExecutionContext.CurrentContext;
                var isolated = new NUnit.Framework.Internal.TestExecutionContext(previous);
                var test = new NUnit.Framework.Internal.TestSuite("Landing base-negative proof");
                isolated.CurrentTest = test;
                isolated.CurrentResult = test.MakeTestResult();
                var setter = typeof(NUnit.Framework.Internal.TestExecutionContext).GetProperty("CurrentContext")
                    .GetSetMethod(true);
                setter.Invoke(null, new object[] { isolated });
                try
                {
                    try { action(); }
                    catch (AssertionException error)
                    {
                        return error.Message.Length <= 2048 ? error.Message : error.Message.Substring(0, 2048) + " [diagnostic truncated]";
                    }
                }
                finally { setter.Invoke(null, new object[] { previous }); }
                throw new InvalidOperationException("The moved guard unexpectedly accepted the pinned base.");
            }
            UnityEditor.AssetDatabase.DisallowAutoRefresh();
            try
            {
                File.WriteAllBytes(scenePath, baseline);
                proof.ceilingFailure = ExpectedFailure(() => new SceneWeightGuardTests()
                    .StPetersPlantField_RetiresTheMeasuredRootsWithoutSavingItsChunks());
                proof.retiredNamesFailure = ExpectedFailure(() => Assert.AreEqual(0, proof.baseAtlasNames,
                    "the atlas deck tiles are retired"));
                proof.bakedMarkerFailure = ExpectedFailure(() => StringAssert.Contains("  m_Name: WharfPass2Deck\n", text,
                    "the baked landing deck is placed"));
            }
            finally
            {
                File.WriteAllBytes(scenePath, branch);
                UnityEditor.AssetDatabase.AllowAutoRefresh();
            }
            proof.branchRestored = File.ReadAllBytes(scenePath).SequenceEqual(branch);
            if (!proof.branchRestored) throw new InvalidOperationException("The branch scene was not restored after the base-negative proof.");
            File.WriteAllText(output, JsonUtility.ToJson(proof, true) + "\n", new UTF8Encoding(false));
            UnityEngine.Debug.Log("[StPetersLandingDeck] BASE_NEGATIVE_PROOF_COMPLETE; branch scene restored.");
        }

        sealed class RootTerrain : ITidalTerrain
        {
            public Vector2 ReadAt;
            public float ElevationAt(Vector2 at) { ReadAt = at; return 5.35f; }
        }

        [Test]
        public void DeckPictureLandsOnTheFloorTheGameWalks()
        {
            Rect picture = FrozenPictureFloor();
            Rect floor = StPetersWharf.DeckFootprint();
            const float OneNativePixel = 1f / 32f;
            Assert.That(floor.yMax, Is.EqualTo(picture.yMax).Within(OneNativePixel), "north lip");
            Assert.That(floor.yMin, Is.EqualTo(picture.yMin).Within(OneNativePixel), "south lip");
            Assert.That(floor.xMin, Is.EqualTo(picture.xMin).Within(OneNativePixel), "west end");
            Assert.That(floor.xMax, Is.EqualTo(picture.xMax).Within(OneNativePixel), "east end");
        }

        [Test]
        public void EveryDeckEdgeFittingAndLandingDeckRowStandsOnThePictureFloor()
        {
            Rect picture = FrozenPictureFloor();
            Rect floor = StPetersWharf.DeckFootprint();
            foreach (var fitting in StPetersWharf.Fittings())
            {
                Assert.That(HiddenHarbours.World.StandablePlatform.Contains(floor, fitting.Position),
                    Is.True, fitting.Name + " belongs to the registered floor");
                Assert.That(HiddenHarbours.World.StandablePlatform.Contains(picture, fitting.Position),
                    Is.True, fitting.Name + " stands on the picture");
            }
            string asset = File.ReadAllText(Path.Combine(ProjectRoot,
                "Assets/_Project/Data/KeyScenes/KeyScene_stp_landing.asset")).Replace("\r\n", "\n");
            foreach (string row in Regex.Split(asset, @"(?m)^  - Id: ").Skip(1))
            {
                if (!Regex.IsMatch(row, @"(?m)^    StandsOn: deck$")) continue;
                string id = row.Substring(0, row.IndexOf('\n'));
                bool hasFoot = Regex.IsMatch(row, @"(?m)^    HasFoot: 1$");
                Match point = Regex.Match(row, @"(?m)^    " + (hasFoot ? "Foot" : "At") +
                    @": \{x: ([-+0-9.Ee]+), y: ([-+0-9.Ee]+)\}");
                Assert.That(point.Success, Is.True, id + " has its recorded foot");
                var at = new Vector2(float.Parse(point.Groups[1].Value, CultureInfo.InvariantCulture),
                    float.Parse(point.Groups[2].Value, CultureInfo.InvariantCulture));
                Assert.That(HiddenHarbours.World.StandablePlatform.Contains(floor, at), Is.True, id + " registered floor");
                Assert.That(HiddenHarbours.World.StandablePlatform.Contains(picture, at), Is.True, id + " picture floor");
            }
        }

        // Independent measurements of the frozen image: its lip rows and end planes, not values
        // read from the layout implementation. The existing pixel oracle pins the image itself.
        static Rect FrozenPictureFloor()
        {
            string text = File.ReadAllText(Path.Combine(ProjectRoot, StPetersLandingDeckBaker.ContractPath));
            float Number(string key)
            {
                Match match = Regex.Match(text, "\"" + key + "\"\\s*:\\s*([-+0-9.Ee]+)");
                Assert.That(match.Success, Is.True, "frame metadata: " + key);
                return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            float ppu = Number("pixelsPerUnit");
            float west = Number("originX") + (11 - Number("pivotX")) / ppu;
            float south = Number("originY") + (Number("pivotY") - 125) / ppu;
            return new Rect(west, south, (987 - 11) / ppu, (125 - 6) / ppu);
        }

        [Test]
        public void SurfaceFootprintAndBothBerthEdgesKeepTheirBaseValues()
        {
            Assert.That(StPetersWharf.SurfaceId, Is.EqualTo("wharf.st_peters"));
            Assert.That(StPetersWharf.DeckFootprint(), Is.EqualTo(FrozenPictureFloor()));
            Assert.That(StPetersWharf.MooringFaceY, Is.EqualTo(2.53345215f).Within(1e-6f));
            Assert.That(StPetersWharf.NorthFaceY, Is.EqualTo(6.25220215f).Within(1e-6f));
            Assert.That(StPetersWharf.DeckCellsBackToFront().Count(), Is.EqualTo(186));
        }

        [Test]
        public void DeckElevationStillSamplesTheSameRootAndStaysAboveSpringHigh()
        {
            var terrain = new RootTerrain();
            Assert.That(StPetersWharf.DeckElevationFrom(terrain), Is.EqualTo(5.35f));
            Assert.That(terrain.ReadAt, Is.EqualTo(new Vector2(183.5f, 0)));
            Assert.That(StPetersWharf.DeckElevationFrom(terrain), Is.GreaterThan(2.2f));
        }

        [Test]
        public void BothLampOwnersAndTheirDerivedRowRemainUnchanged()
        {
            Assert.That(StPetersWharf.LampRowY, Is.EqualTo(5.75220215f).Within(1e-6f));
            var lamps = StPetersWharf.LampPostSites();
            Assert.That(lamps.Count, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { new Vector2(211f, 5.75220215f), new Vector2(184.5f, 5.75220215f) },
                lamps.Select(l => l.Position).ToArray());
            foreach (var lamp in lamps)
            {
                Assert.That(lamp.Family, Is.EqualTo("wharfDecor"));
                Assert.That(lamp.Key, Is.EqualTo("lanternPost"));
                Assert.That(lamp.Heading, Is.EqualTo(180f));
                Assert.That(lamp.Deck, Is.EqualTo(FrozenPictureFloor()));
            }
        }

        static string Sha(string text)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        static string Body(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            int brace = source.IndexOf('{', start), depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                if (source[i] == '}' && --depth == 0) return source.Substring(start, i - start + 1);
            }
            Assert.Fail("Unclosed source body: " + signature);
            return null;
        }

        [Test]
        public void FloorFittingsAndLampSourceAndLandingDefStayByteIdenticalToBase()
        {
            string source = File.ReadAllText(Path.Combine(ProjectRoot, "Assets/_Project/Code/App/Editor/StPetersWharf.cs")).Replace("\r\n", "\n");
            StringAssert.Contains("floor.Configure(SurfaceId, DeckFootprint(), deckElevation);", source);
            // Ruling 5 retires the old positional hashes. Keep all non-placement content pinned.
            var evidence = LandingDockEvidence.Load();
            int fittings = source.IndexOf("// Fittings, on their own root", StringComparison.Ordinal);
            Assert.That(Sha(source.Substring(fittings, source.IndexOf("            Debug.Log($\"[StPetersWharf] Built", fittings, StringComparison.Ordinal) - fittings).TrimEnd()),
                Is.EqualTo("ed356dc1434f6d2ab24615975bacccd1f530e038c7d093bddeb2888d46a75aa3"));
            string rows = File.ReadAllText(Path.Combine(ProjectRoot, "Assets/_Project/Data/KeyScenes/KeyScene_stp_landing.asset")).Replace("\r\n", "\n");
            string nonPlacement = Regex.Replace(rows, @"(?ms)^  - Id: (\S+)\n(.*?)(?=^  - Id:|\z)", m =>
                evidence.placementIds.Contains(m.Groups[1].Value) ? Regex.Replace(m.Value, @"(?m)^    (At|SortY): .*\n", "") : m.Value);
            Assert.That(Sha(nonPlacement), Is.EqualTo(evidence.landingNonPlacementSha256));
        }

        [Test]
        public void RecipePinsTheBoardContextWithoutChangingTheGameplayFootprint()
        {
            var r = StPetersLandingDeckBaker.ReadRecipe();
            Assert.That(r.family, Is.EqualTo("pier"));
            Assert.That(r.originX, Is.EqualTo(198)); Assert.That(r.originY, Is.EqualTo(0));
            Assert.That(r.x0, Is.EqualTo(183)); Assert.That(r.length, Is.EqualTo(30.5f));
            Assert.That(r.bays, Is.EqualTo(10)); Assert.That(r.bayLen, Is.EqualTo(3.05f));
            Assert.That(r.width, Is.EqualTo(6)); Assert.That(r.deckElevation, Is.EqualTo(5.35f));
            Assert.That(r.datumShift, Is.EqualTo(2.2f)); Assert.That(r.tideRange, Is.EqualTo(4.4f));
            Assert.That(r.stage, Is.EqualTo("weathered")); Assert.That(r.season, Is.EqualTo("summer"));
            Assert.That(r.dir, Is.Zero); Assert.That(r.frame, Is.Zero);
            Assert.That(r.wind, Is.EqualTo(0.3f)); Assert.That(r.gust, Is.EqualTo(0.3f)); Assert.That(r.windDir, Is.EqualTo(1));
            Assert.That(r.fittings, Is.False); Assert.That(r.fixtures, Is.False);
            Assert.That(r.importSizeCap, Is.EqualTo(2048));
            Assert.That(r.sourceSha256, Is.EqualTo("2d8bde76678511edfa188d3d5df58410dae14da69c862e1f7121a19c6778bb09"));
            StPetersLandingDeckBaker.CheckRecipe(r);
            Assert.That(StPetersWharf.DeckFootprint(), Is.EqualTo(FrozenPictureFloor()),
                "Ruling 5 makes the standable surface follow the measured picture; the recipe is unchanged.");
        }

        [Test]
        public void CommittedWharfOutsideTheVisualDeckKeepsItsBaseBytes()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(Path.Combine(ProjectRoot, ScenePath)));
            var wharf = scene.RootNamed("StPetersWharf");
            var proof = LandingDockEvidence.Load();
            Assert.That(scene.SubtreeOf(wharf.FileId).Count, Is.EqualTo(proof.expectedWharfDocuments));
            string text = string.Join("\n", proof.protectedWharfIds.OrderBy(id => id).Select(id => scene.Require(id, "protected wharf document").Text));
            Assert.That(proof.protectedWharfIds.Length, Is.EqualTo(39));
            Assert.That(Sha(text), Is.EqualTo(proof.protectedWharfSha256));
        }

        static string Fixture()
        {
            var sb = new StringBuilder("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            void Go(long id, string name, params long[] components)
            {
                sb.Append($"--- !u!1 &{id}\nGameObject:\n  m_Component:\n");
                foreach (long c in components) sb.Append($"  - component: {{fileID: {c}}}\n");
                sb.Append($"  m_Name: {name}\n");
            }
            void Tr(long id, long go, long father, long[] children)
            {
                sb.Append($"--- !u!4 &{id}\nTransform:\n  m_GameObject: {{fileID: {go}}}\n" +
                    "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n");
                sb.Append(children.Length == 0 ? "  m_Children: []\n" : "  m_Children:\n");
                foreach (long c in children) sb.Append($"  - {{fileID: {c}}}\n");
                sb.Append($"  m_Father: {{fileID: {father}}}\n");
            }
            var cells = StPetersWharf.DeckCellsBackToFront().ToArray();
            Go(1, "StPetersWharf", 2); Tr(2, 1, 0, new long[] { 4, 6 });
            Go(3, "Deck", 4); Tr(4, 3, 2, cells.Select((_, i) => 1001L + i * 3).ToArray());
            Go(5, "Fittings", 6); Tr(6, 5, 2, Array.Empty<long>());
            for (int i = 0; i < cells.Length; i++)
            {
                long id = 1000 + i * 3;
                Go(id, $"Deck_{cells[i].x}_{cells[i].y}", id + 1, id + 2);
                Tr(id + 1, id, 4, Array.Empty<long>());
                sb.Append($"--- !u!212 &{id + 2}\nSpriteRenderer:\n  m_GameObject: {{fileID: {id}}}\n" +
                    "  m_Materials:\n  - {fileID: 2100000, guid: a97c105638bdf8b4a8650670310a4cd3, type: 2}\n");
            }
            return sb.ToString();
        }

        [Test]
        public void BankedDockMatchesItsBuilders_AndTheNamedPatchHasNothingLeftToChange()
        {
            var go = new GameObject("LandingDockBuilderAgreement");
            try
            {
                var terrain = go.AddComponent<HiddenHarbours.World.TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(terrain);
                var scene = SceneYaml.Parse(File.ReadAllText(Path.Combine(ProjectRoot, ScenePath)));
                var operations = new System.Collections.Generic.List<DockOperation>();
                PlanLandingDock(scene, terrain, new EditorNavMarkAssets(), operations);
                Assert.That(operations, Is.Empty, string.Join("\n", operations.Select(o => o.kind + " " + o.name)));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static readonly SpriteRef FixtureSprite = new SpriteRef(
            new ObjRef(21300000, "00000000000000000000000000000001", 3), new Vector2(31, 10));

        [Test]
        public void VisualMigrationNamesAllAtlasDeletionsAndLeavesTheOtherHierarchyAlone()
        {
            var scene = SceneYaml.Parse(Fixture());
            var patch = LandingDeck(scene, FixtureSprite, new Vector2(198, -1.685f));
            Assert.That(patch.Deletions.Count(), Is.EqualTo(558));
            Assert.That(patch.Deletions.All(d => d.Name.StartsWith("StPetersWharf/Deck/Deck_", StringComparison.Ordinal)), Is.True);
            var after = patch.Apply(scene);
            foreach (long id in new long[] { 1, 2, 5, 6 }) Assert.That(after.Get(id).Text, Is.EqualTo(scene.Get(id).Text));
            string once = after.Render();
            Assert.That(patch.ApplyTo(once), Is.EqualTo(once));
            Assert.That(LandingDeck(after, FixtureSprite, new Vector2(198, -1.685f)).IsEmpty, Is.True);
        }

        [Test]
        public void VisualMigrationRefusesAnUnrecordedDeckChild()
        {
            string text = Fixture().Replace("m_Name: Deck_183_2", "m_Name: unrecorded");
            Assert.Throws<Refusal>(() => LandingDeck(SceneYaml.Parse(text), FixtureSprite, Vector2.zero));
        }
    }
}
