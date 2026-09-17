using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>The cast's figure seam belongs to Core, and only to Core</b> (ADR 0044, amendment 2026-09-17;
    /// rule 4).
    ///
    /// <para>The skipper's mesh is drawn by a presenter in Art. The boat that stands the skipper lives in
    /// Boats, and the villagers live in World. Neither module may reach Art. They ask Core's
    /// <see cref="CharacterFigurePresentation"/> for a figure, and Art registers the one that answers.</para>
    ///
    /// <para>This assembly references Core and World and nothing of Art, so the fakes below compiling at
    /// all is the first half of the proof. The second half reads both kinds of reference: the DECLARED
    /// ones (the asmdefs) and the COMPILED ones (what the C# compiler kept). A declared reference nobody
    /// uses yet is still an open door, and an asmdef reference is not a seam.</para>
    /// </summary>
    public class CharacterFigureSeamTests
    {
        const string CoreAssembly = "HiddenHarbours.Core";
        const string ArtAssembly = "HiddenHarbours.Art";
        const string ArtAsmdef = "Assets/_Project/Code/Art/HiddenHarbours.Art.asmdef";
        const string PlayerAsmdef = "Assets/_Project/Code/Player/HiddenHarbours.Player.asmdef";

        static readonly string[] ArtFreeAsmdefs =
        {
            "Assets/_Project/Code/Boats/HiddenHarbours.Boats.asmdef",
            "Assets/_Project/Code/World/HiddenHarbours.World.asmdef",
            "Assets/Tests/EditMode/World/HiddenHarbours.Tests.World.EditMode.asmdef",
        };

        sealed class FakeStand : ICharacterFigureStand
        {
            public IsoCharacterSprite FigureCharacter => null;
            public Transform FigureHull => null;
            public Vector3 FigureStandRigMetres { get; set; }
            public float FigureDeckBearingDegrees { get; set; }
        }

        sealed class FakeFigure : ICharacterFigure
        {
            public bool DrawsInsteadOfSprite { get; set; }
            public ICharacterFigureStand PosedStand;
            public bool PosedAboard;
            public int Poses;

            public void PoseFigure(ICharacterFigureStand stand, bool aboard)
            {
                PosedStand = stand;
                PosedAboard = aboard;
                Poses++;
            }
        }

        sealed class FakeService : ICharacterFigurePresentationService
        {
            public readonly FakeFigure Figure = new FakeFigure();
            public GameObject AttachedHost;
            public ICharacterFigureStand AttachedStand;

            public ICharacterFigure Attach(GameObject host, ICharacterFigureStand stand)
            {
                AttachedHost = host;
                AttachedStand = stand;
                return Figure;
            }
        }

        [Serializable]
        sealed class AsmdefReferences
        {
            public string[] references;
        }

        ICharacterFigurePresentationService _savedService;
        GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _savedService = CharacterFigurePresentation.Service;
            _host = new GameObject("CharacterFigureSeamTests.Host");
        }

        [TearDown]
        public void TearDown()
        {
            CharacterFigurePresentation.Service = _savedService;
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [Test]
        public void AFigureIsAskedForAndPosedThroughCoreAlone()
        {
            var service = new FakeService();
            CharacterFigurePresentation.Service = service;
            var stand = new FakeStand
            {
                FigureStandRigMetres = new Vector3(0.1f, -0.4f, 0.6f),
                FigureDeckBearingDegrees = 135f,
            };

            // The stand's own call, exactly as MooredBoat makes it.
            ICharacterFigure figure = CharacterFigurePresentation.Service?.Attach(_host, stand);

            Assert.AreSame(service.Figure, figure, "the locator did not hand back the registered service's figure");
            Assert.AreSame(_host, service.AttachedHost);
            Assert.AreSame(stand, service.AttachedStand);

            service.Figure.DrawsInsteadOfSprite = true;
            figure.PoseFigure(stand, aboard: true);
            Assert.IsTrue(figure.DrawsInsteadOfSprite);
            Assert.AreSame(stand, service.Figure.PosedStand);
            Assert.IsTrue(service.Figure.PosedAboard);
            Assert.AreEqual(1, service.Figure.Poses);
        }

        [Test]
        public void TheSeamIsCompiledIntoCore()
        {
            // This assembly can see World as well as Core, so compiling is not enough on its own: the seam
            // moving into World would still compile here, and Boats could no longer reach it.
            foreach (Type seam in new[]
            {
                typeof(ICharacterFigureStand), typeof(ICharacterFigure),
                typeof(ICharacterFigurePresentationService), typeof(CharacterFigurePresentation),
            })
                Assert.AreEqual(CoreAssembly, seam.Assembly.GetName().Name, $"{seam.Name} left Core");
        }

        [Test]
        public void NeitherBoatsNorWorldDeclaresArt()
        {
            string artGuidRef = "GUID:" + GuidOf(ArtAsmdef + ".meta");

            // The reader must be able to say yes first: the player draws its own mesh, so Player declares Art.
            CollectionAssert.Contains(ReferencesOf(PlayerAsmdef), ArtAssembly,
                "harness: the asmdef reader did not find Art in Player's references, so it cannot find it anywhere");

            foreach (string asmdef in ArtFreeAsmdefs)
            {
                string[] refs = ReferencesOf(asmdef);
                CollectionAssert.Contains(refs, CoreAssembly, $"harness: read no Core reference from {asmdef}");
                CollectionAssert.DoesNotContain(refs, ArtAssembly,
                    $"{asmdef} declares Art. The cast's figure is reached through Core's CharacterFigurePresentation " +
                    "(ADR 0044 §7, rule 4).");
                CollectionAssert.DoesNotContain(refs, artGuidRef, $"{asmdef} declares Art by GUID.");
            }
        }

        [Test]
        public void NeitherCompiledBoatsNorCompiledWorldReferencesArt()
        {
            Assert.IsTrue(CompiledReferencesOf(Loaded("HiddenHarbours.Player")).Contains(ArtAssembly),
                "harness: compiled Player does not reference Art, so this check cannot see a reference");

            foreach (Assembly assembly in new[]
            {
                Loaded("HiddenHarbours.Boats"), Loaded("HiddenHarbours.World"), typeof(CharacterFigureSeamTests).Assembly,
            })
            {
                string[] compiled = CompiledReferencesOf(assembly);
                CollectionAssert.Contains(compiled, CoreAssembly, $"harness: compiled {assembly.GetName().Name} names no Core");
                CollectionAssert.DoesNotContain(compiled, ArtAssembly,
                    $"compiled {assembly.GetName().Name} references Art.");
            }
        }

        static string[] ReferencesOf(string asmdefPath)
        {
            Assert.IsTrue(File.Exists(asmdefPath), $"harness: {asmdefPath} is missing");
            AsmdefReferences parsed = JsonUtility.FromJson<AsmdefReferences>(File.ReadAllText(asmdefPath));
            Assert.IsNotNull(parsed?.references, $"harness: {asmdefPath} has no references array");
            return parsed.references;
        }

        static string GuidOf(string metaPath)
        {
            Assert.IsTrue(File.Exists(metaPath), $"harness: {metaPath} is missing");
            string line = File.ReadAllLines(metaPath).FirstOrDefault(l => l.StartsWith("guid: ", StringComparison.Ordinal));
            Assert.IsNotNull(line, $"harness: {metaPath} has no guid line");
            string guid = line.Substring("guid: ".Length).Trim();
            Assert.AreEqual(32, guid.Length, $"harness: {metaPath} guid '{guid}'");
            return guid;
        }

        static Assembly Loaded(string name)
        {
            Assembly found = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
            Assert.IsNotNull(found, $"harness: {name} is not loaded in the editor");
            return found;
        }

        static string[] CompiledReferencesOf(Assembly assembly) =>
            assembly.GetReferencedAssemblies().Select(r => r.Name).ToArray();
    }
}
