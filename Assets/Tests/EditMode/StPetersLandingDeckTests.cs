using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        sealed class RootTerrain : ITidalTerrain
        {
            public Vector2 ReadAt;
            public float ElevationAt(Vector2 at) { ReadAt = at; return 5.35f; }
        }

        [Test]
        public void SurfaceFootprintAndBothBerthEdgesKeepTheirBaseValues()
        {
            Assert.That(StPetersWharf.SurfaceId, Is.EqualTo("wharf.st_peters"));
            Assert.That(StPetersWharf.DeckFootprint(), Is.EqualTo(new Rect(183, -3, 31, 6)));
            Assert.That(StPetersWharf.MooringFaceY, Is.EqualTo(-3f));
            Assert.That(StPetersWharf.NorthFaceY, Is.EqualTo(3f));
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
            Assert.That(StPetersWharf.LampRowY, Is.EqualTo(0.5f));
            var lamps = StPetersWharf.LampPostSites();
            Assert.That(lamps.Count, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { new Vector2(211.5f, 0.5f), new Vector2(184.5f, 0.5f) },
                lamps.Select(l => l.Position).ToArray());
            foreach (var lamp in lamps)
            {
                Assert.That(lamp.Family, Is.EqualTo("wharfDecor"));
                Assert.That(lamp.Key, Is.EqualTo("lanternPost"));
                Assert.That(lamp.Heading, Is.EqualTo(180f));
                Assert.That(lamp.Deck, Is.EqualTo(new Rect(183, -3, 31, 6)));
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
            Assert.That(Sha(Body(source, "public static float LampRowY")), Is.EqualTo("0092fd160dcbf483b8baafac06b2ff1877811ff6e26d2fa5ffd604e68c22c005"));
            Assert.That(Sha(Body(source, "public static IReadOnlyList<LampPosts.Site> LampPostSites()")), Is.EqualTo("22edea8efbe9768656e27a8db136907cf75606c4a195e197c7a5079a4c81bfcb"));
            int floor = source.IndexOf("public static Rect DeckFootprint()", StringComparison.Ordinal);
            Assert.That(Sha(source.Substring(floor, source.IndexOf("// --- FITTINGS", floor, StringComparison.Ordinal) - floor).TrimEnd()),
                Is.EqualTo("ee442c5703797afec15e6b4651cbae23c216ae539e85b09ad5eb77f93ec28ae9"));
            int fittings = source.IndexOf("// Fittings, on their own root", StringComparison.Ordinal);
            Assert.That(Sha(source.Substring(fittings, source.IndexOf("            Debug.Log($\"[StPetersWharf] Built", fittings, StringComparison.Ordinal) - fittings).TrimEnd()),
                Is.EqualTo("ed356dc1434f6d2ab24615975bacccd1f530e038c7d093bddeb2888d46a75aa3"));
            Assert.That(Sha(File.ReadAllText(Path.Combine(ProjectRoot, "Assets/_Project/Data/KeyScenes/KeyScene_stp_landing.asset")).Replace("\r\n", "\n")),
                Is.EqualTo("1496c74bbf96967cc07a6169666ae9533aec03fc126deefbd101c1cd39c691d6"));
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
            Assert.That(StPetersWharf.DeckFootprint(), Is.EqualTo(new Rect(183, -3, 31, 6)),
                "The board's art span does not replace the existing standable surface.");
        }

        [Test]
        public void CommittedWharfOutsideTheVisualDeckKeepsItsBaseBytes()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(Path.Combine(ProjectRoot, ScenePath)));
            var wharf = scene.RootNamed("StPetersWharf");
            var deck = scene.ChildNamed(scene.TransformOf(wharf), "Deck");
            var ids = scene.SubtreeOf(wharf.FileId); ids.ExceptWith(scene.SubtreeOf(deck.FileId));
            Assert.That(ids.Count, Is.EqualTo(57));
            string text = string.Join("\n", ids.OrderBy(id => id).Select(id => scene.Get(id).Text));
            Assert.That(Sha(text), Is.EqualTo("f5602131888ac8e2a7603323cccfc6bc052e6d9631883eefca6e453d77987811"));
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
