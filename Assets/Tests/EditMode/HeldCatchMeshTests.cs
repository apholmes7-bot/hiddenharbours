using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A fish in his hand is the rig's facet mesh at its size rung</b> (fish art pass 2, section 3):
    /// the seven meshed species are drawn as their baked mesh on the hand's tool socket, scaled to the
    /// rung the sprite of the same catch was drawn at; the species that only borrow a kind's art keep
    /// their sprite.
    ///
    /// <list type="bullet">
    /// <item>The library answers a mesh only for a species that IS its kind, at the rung's scale.</item>
    /// <item>Every committed rung's scale is the rig's published scale for that rung.</item>
    /// <item>The turn to the sprite's facing is zero when the facing is the heading, applies the face-on
    /// map, and follows the mesh's azimuth sense.</item>
    /// <item>The build scales about the grip, adds a thin line along the fins' open edges and a block
    /// per eye, and leaves the rod's build as it was.</item>
    /// <item>On a posed fisher the fish hangs on the socket at the identity, pads the overlay by its own
    /// claim beside the rod's, and each missing piece is a refusal.</item>
    /// </list>
    /// </summary>
    public sealed class HeldCatchMeshTests
    {
        private const string LibraryPath = "Assets/_Project/Data/Art/CatchItemLibrary.asset";
        private const string FisherPath = "Assets/_Project/Data/Characters/Skin/fisher.asset";
        private const string CodPath = "Assets/_Project/Data/Characters/HeldProps/FishCodHeldMesh.asset";
        private const string CanePath = "Assets/_Project/Data/Characters/HeldProps/RodCaneHeldMesh.asset";
        private const string CarryHandsPath = "Assets/_Project/Code/Player/CarryHands.cs";
        private const string PresenterPath = "Assets/_Project/Code/Player/RodFightPresenter.cs";

        /// <summary>The species the pass meshes, by their own kind.</summary>
        private static readonly (string species, string kind)[] Meshed =
        {
            ("fish.atlantic_cod", "cod"), ("fish.haddock", "haddock"), ("fish.pollock", "pollock"),
            ("fish.mackerel", "mackerel"), ("fish.striped_bass", "bass"), ("fish.winter_flounder", "flounder"),
            ("fish.atlantic_herring", "herring"),
            // The coast species kit's three, baked by the same fish rig's held reconstruction.
            ("fish.rainbow_smelt", "smelt"), ("fish.atlantic_tomcod", "tomcod"), ("fish.cunner", "cunner"),
        };

        /// <summary>
        /// The catchable species that have NO held mesh yet, by id. Empty: the crustaceans and the
        /// shellfish landed as the shellfish family (crustaceans by the back from their own rig,
        /// shellfish as the handful the game hands over). Owed, not excused:
        /// <see cref="EveryCatchableSpecies_IsHeldAsAMesh_OrIsNamedOwed"/> fails when a species here
        /// gains a mesh (take it off) and when a new species has neither.
        /// </summary>
        private static readonly string[] OwedNoHeldMeshPattern = { };

        private const string FishDataFolder = "Assets/_Project/Data/Fish";

        private static readonly string[] Rungs = { "_sm", "", "_lg" };

        private static readonly MethodInfo s_figureOnDestroy =
            typeof(IsoCharacterFigureRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo s_propOnDestroy =
            typeof(IsoFacetHeldPropRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<Object> _made = new List<Object>();
        private readonly List<IsoCharacterFigureRenderer> _figures = new List<IsoCharacterFigureRenderer>();
        private readonly List<HeldCatchMesh> _drivers = new List<HeldCatchMesh>();
        private GameConfig _config;
        private GameObject _home;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _made.Add(_config);
            _home = new GameObject("HeldCatchMeshTestsHome");
            _made.Add(_home);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (HeldCatchMesh d in _drivers)
            {
                if (d.Renderer != null && s_propOnDestroy != null) s_propOnDestroy.Invoke(d.Renderer, null);
                d.Dispose();
            }
            _drivers.Clear();
            foreach (IsoCharacterFigureRenderer f in _figures)
            {
                if (f == null) continue;
                if (s_figureOnDestroy != null) s_figureOnDestroy.Invoke(f, null);
                Object.DestroyImmediate(f.gameObject);
            }
            _figures.Clear();
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // ================================================================ the library

        [Test]
        public void EachMeshedSpecies_HasItsOwnMesh_AtEveryRung_AtTheRigsScale()
        {
            CatchItemLibrary art = Load<CatchItemLibrary>(LibraryPath);
            int checkedRungs = 0;
            foreach ((string species, string kind) in Meshed)
            {
                Dictionary<string, float> scales = RodKitImporter.RungScales(kind);
                Assert.AreEqual(3, scales.Count, $"'{kind}' publishes no three-rung ladder");
                HeldPropMeshDef baked = art.HeldMeshFor(kind);
                Assert.IsNotNull(baked, $"'{kind}' carries no held mesh");
                Assert.AreEqual(HeldPropMeshDef.FishFamily, baked.Family);
                Assert.AreEqual(kind, baked.Variant);
                foreach (string suffix in Rungs)
                {
                    HeldPropMeshDef got = art.HeldMeshFor(species, kind + suffix, out float scale);
                    Assert.AreSame(baked, got, $"{species} as '{kind + suffix}' is not drawn with its own mesh");
                    Assert.IsTrue(scales.TryGetValue(suffix, out float want), $"'{kind}' has no rung '{suffix}'");
                    Assert.AreEqual(want, scale, 1e-5f,
                                    $"'{kind + suffix}' draws its mesh at {scale}, the rig drew it at {want}");
                    checkedRungs++;
                }
            }
            Assert.AreEqual(Meshed.Length * Rungs.Length, checkedRungs);
        }

        /// <summary>
        /// <b>Coverage.</b> Every shipped species def (everything the player can catch) is held as a
        /// mesh: its own kind resolves to a held mesh at the middle rung and at every rung the rig
        /// publishes for it. The only exceptions are named in <see cref="OwedNoHeldMeshPattern"/>, all
        /// shellfish by category, and each of those must still fall back (so the list shrinks the day a
        /// mesh lands). A new species added without a held mesh fails here by name.
        /// </summary>
        [Test]
        public void EveryCatchableSpecies_IsHeldAsAMesh_OrIsNamedOwed()
        {
            CatchItemLibrary art = Load<CatchItemLibrary>(LibraryPath);
            var owed = new HashSet<string>(OwedNoHeldMeshPattern);
            var seen = new HashSet<string>();
            var report = new System.Text.StringBuilder();
            bool bad = false;

            foreach (string guid in AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { FishDataFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Fishing.FishSpeciesDef>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                seen.Add(def.Id);
                string kind = art.KindFor(def.Id);

                var rungs = new List<string> { kind };
                foreach (string suffix in RodKitImporter.RungScales(kind).Keys)
                    if (suffix.Length > 0) rungs.Add(kind + suffix);

                var missing = new List<string>();
                foreach (string rung in rungs)
                    if (art.HeldMeshFor(def.Id, rung, out _) == null) missing.Add(rung);

                if (owed.Contains(def.Id))
                {
                    if (def.Category != FishCategory.Shellfish)
                    {
                        bad = true;
                        report.AppendLine($"  x {def.Id}: named owed, but it is {def.Category}, not shellfish.");
                    }
                    else if (missing.Count == 0)
                    {
                        bad = true;
                        report.AppendLine($"  x {def.Id}: now held as a mesh; take it off the owed list.");
                    }
                    else report.AppendLine($"  - {def.Id} as '{kind}': owed (no held-mesh pattern for its body).");
                    continue;
                }

                if (missing.Count > 0)
                {
                    bad = true;
                    report.AppendLine($"  x {def.Id} as '{kind}': no held mesh at {string.Join(", ", missing)}.");
                }
                else report.AppendLine($"  ok {def.Id} as '{kind}': {rungs.Count} rung(s) meshed.");
            }

            foreach (string id in OwedNoHeldMeshPattern)
                if (!seen.Contains(id))
                {
                    bad = true;
                    report.AppendLine($"  x {id}: named owed, but no species def carries it.");
                }

            Assert.Greater(seen.Count, OwedNoHeldMeshPattern.Length, "harness: no species defs were found");
            Assert.IsFalse(bad, "A catchable species falls back to a sprite in his hand. Bake its held mesh " +
                                "(Bake ALL held props, then Build Catch Item Library) or, for a body type " +
                                "with no held-mesh pattern, name it owed.\n" + report);
        }

        [Test]
        public void ASpeciesThatBorrowsItsKind_IsHeldAsItsOwnMesh_AndAnUnmappedOneIsNot()
        {
            CatchItemLibrary art = Load<CatchItemLibrary>(LibraryPath);
            Assert.AreEqual("tomcod", art.KindFor("fish.atlantic_tomcod"),
                            "harness: the tomcod draws as its own kind, not as the cod");
            HeldPropMeshDef tomcod = art.HeldMeshFor("fish.atlantic_tomcod", "tomcod", out _);
            Assert.IsNotNull(tomcod, "the tomcod's own kind is held as a mesh");
            Assert.AreEqual("tomcod", tomcod.Variant, "the tomcod is held as its own mesh, never the cod's");
            Assert.AreNotSame(art.HeldMeshFor("cod"), tomcod);
            HeldPropMeshDef lobster = art.HeldMeshFor("fish.lobster", "lobster", out float s);
            Assert.IsNotNull(lobster, "the lobster is held as a mesh from its own rig");
            Assert.AreEqual("heldprop.shellfish_lobster", lobster.Id);
            Assert.AreEqual(1f, s);
            HeldPropMeshDef clam = art.HeldMeshFor("clam");
            HeldPropMeshDef quahog = art.HeldMeshFor("fish.northern_quahog", "clam", out float qs);
            Assert.IsNotNull(quahog, "the quahog borrows the clam's sprite but is held as its own mesh");
            Assert.AreEqual("heldprop.shellfish_quahog", quahog.Id);
            Assert.AreNotSame(clam, quahog, "the quahog is never held as the clam's mesh");
            Assert.AreEqual(1f, qs);
            HeldPropMeshDef crab = art.HeldMeshFor("fish.rock_crab", "crab", out _);
            Assert.IsNotNull(crab, "the rock crab is held as the crab kind's mesh");
            Assert.AreEqual("heldprop.shellfish_crab", crab.Id);
            HeldPropMeshDef green = art.HeldMeshFor("fish.green_crab", "crab", out _);
            Assert.IsNotNull(green, "the green crab is held as its own recoloured mesh");
            Assert.AreEqual("heldprop.shellfish_green_crab", green.Id);
            Assert.AreNotSame(crab, green, "the green crab is never held in the rock crab's colours");
            Assert.IsNull(art.HeldMeshFor(null, "cod", out _));
            Assert.IsNull(art.HeldMeshFor("fish.atlantic_cod", null, out _));
            Assert.IsNull(art.HeldMeshFor("fish.atlantic_cod", "no_such_kind", out _));
        }

        [Test]
        public void AHandMadeTable_ReadsOwnBorrowedAndScale_AndANonPositiveScaleIsOne()
        {
            HeldPropMeshDef cod = Load<HeldPropMeshDef>(CodPath);
            var art = ScriptableObject.CreateInstance<CatchItemLibrary>();
            _made.Add(art);
            art.Configure(
                new[]
                {
                    new CatchItemLibrary.KindEntry { Kind = "cod", HeldMesh = cod, HeldMeshScale = 1.25f },
                    new CatchItemLibrary.KindEntry { Kind = "cod_sm", HeldMesh = cod, HeldMeshScale = 0f },
                },
                new[]
                {
                    new CatchItemLibrary.SpeciesEntry { SpeciesId = "fish.a_cod", Kind = "cod" },
                    new CatchItemLibrary.SpeciesEntry { SpeciesId = "fish.b_tomcod", Kind = "cod", BorrowsKind = true },
                });
            Assert.AreSame(cod, art.HeldMeshFor("fish.a_cod", "cod", out float scale));
            Assert.AreEqual(1.25f, scale, 1e-6f);
            Assert.AreSame(cod, art.HeldMeshFor("fish.a_cod", "cod_sm", out scale));
            Assert.AreEqual(1f, scale, "a rung with no scale draws as baked");
            Assert.IsNull(art.HeldMeshFor("fish.b_tomcod", "cod", out _));
            Assert.IsNull(art.HeldMeshFor("fish.not_in_table", "cod", out _));
        }

        // ================================================================ the turn

        [Test]
        public void TheTurn_IsZeroWhenTheFacingIsTheHeading_AndFollowsTheMeshesAzimuthSense()
        {
            HeldPropMeshDef cod = Load<HeldPropMeshDef>(CodPath);
            // The four quarter headings, which every heading dial draws exactly.
            for (int facing = 0; facing < 8; facing += 2)
                Assert.AreEqual(0f, HeldCatchMesh.TurnToFacingDegrees(cod, null, facing, facing * 45f), 1e-3f,
                                $"facing {facing} at its own heading must hang as baked");

            float sign = cod.AzimuthCounterClockwise ? -1f : 1f;
            Assert.AreEqual(sign * 90f, HeldCatchMesh.TurnToFacingDegrees(cod, null, 2, 0f), 1e-3f);
            Assert.AreEqual(sign * -45f, HeldCatchMesh.TurnToFacingDegrees(cod, null, 7, 0f), 1e-3f,
                            "the turn is the short way round");

            HeldPropMeshDef flipped = Object.Instantiate(cod);
            _made.Add(flipped);
            flipped.AzimuthCounterClockwise = !cod.AzimuthCounterClockwise;
            Assert.AreEqual(-HeldCatchMesh.TurnToFacingDegrees(cod, null, 2, 0f),
                            HeldCatchMesh.TurnToFacingDegrees(flipped, null, 2, 0f), 1e-3f);

            Assert.AreEqual(0f, HeldCatchMesh.TurnToFacingDegrees(null, null, 3, 0f));
        }

        [Test]
        public void TheTurn_AppliesTheFaceOnMap_AndIgnoresABadEntry()
        {
            HeldPropMeshDef cod = Load<HeldPropMeshDef>(CodPath);
            int[] map = { 2, 2, 2, 2, 6, 6, 6, 6 };
            Assert.AreEqual(HeldCatchMesh.TurnToFacingDegrees(cod, null, 2, 0f),
                            HeldCatchMesh.TurnToFacingDegrees(cod, map, 0, 0f), 1e-3f, "facing 0 shows as 2");
            Assert.AreEqual(HeldCatchMesh.TurnToFacingDegrees(cod, null, 6, 0f),
                            HeldCatchMesh.TurnToFacingDegrees(cod, map, 5, 0f), 1e-3f, "facing 5 shows as 6");
            int[] bad = { 99, -1, 2, 3, 4, 5, 6, 7 };
            Assert.AreEqual(HeldCatchMesh.TurnToFacingDegrees(cod, null, 0, 0f),
                            HeldCatchMesh.TurnToFacingDegrees(cod, bad, 0, 0f), 1e-3f, "an entry off the sheet is ignored");
        }

        // ================================================================ the build

        [Test]
        public void TheBuild_ScalesAboutTheGrip_AndKeepsThePixelWidths()
        {
            HeldPropMeshDef cod = Load<HeldPropMeshDef>(CodPath);
            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cod, new IsoFacetHeldPropMesh.Options { Scale = 1f },
                                                        out var one, out string why), why);
            _made.Add(one.Mesh);
            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cod, new IsoFacetHeldPropMesh.Options { Scale = 0.5f },
                                                        out var half, out why), why);
            _made.Add(half.Mesh);
            Vector3 grip = cod.GripLocalMeters;
            Vector3[] a = one.Mesh.vertices, b = half.Mesh.vertices;
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
                Assert.Less(((b[i] - grip) - (a[i] - grip) * 0.5f).magnitude, 1e-5f, $"vertex {i} is not scaled about the grip");

            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cod, new IsoFacetHeldPropMesh.Options { Scale = 0f },
                                                        out var zero, out why), why);
            _made.Add(zero.Mesh);
            Assert.AreEqual(one.Mesh.bounds.size, zero.Mesh.bounds.size, "a scale of 0 is 1");
        }

        [Test]
        public void TheFish_GetsAThinLineAlongItsFinsOpenEdges_AndABlockPerEye()
        {
            HeldPropMeshDef cod = Load<HeldPropMeshDef>(CodPath);
            int fin = IsoFacetHeldPropMesh.RampIndexOf(cod, HeldCatchMesh.FinRampName);
            Assert.GreaterOrEqual(fin, 0, "the cod paints no 'fin' ramp");
            float px = 1f / cod.PxPerMetre;

            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cod, new IsoFacetHeldPropMesh.Options(), out var bare, out string why), why);
            _made.Add(bare.Mesh);
            Assert.AreEqual(0, bare.TubeSegments, "the bare build adds nothing");

            var finOnly = new IsoFacetHeldPropMesh.Options { EdgeLineWidthMeters = px, EdgeRampName = HeldCatchMesh.FinRampName };
            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cod, finOnly, out var fins, out why), why);
            _made.Add(fins.Mesh);
            Assert.Greater(fins.TubeSegments, 0, "no fin edge took a line");
            Assert.Greater(fins.Mesh.vertexCount, bare.Mesh.vertexCount);
            Assert.AreEqual(bare.Ramps.Length, fins.Ramps.Length, "a fin line is a facet of the fin ramp, no new slot");

            int dots = 0;
            foreach (HeldPropMeshDef.Line line in cod.Lines)
                if (line.Dots && line.Points != null) dots += line.Points.Length;
            Assert.Greater(dots, 0, "the cod plots no eyes");
            var eyes = new IsoFacetHeldPropMesh.Options { DotSizeMeters = px };
            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cod, eyes, out var eyed, out why), why);
            _made.Add(eyed.Mesh);
            Assert.AreEqual(dots, eyed.TubeSegments, "one block per eye");
            Assert.LessOrEqual(eyed.Ramps.Length, HeldPropMeshDef.RampSlots);

            var wrongRamp = new IsoFacetHeldPropMesh.Options { EdgeLineWidthMeters = px, EdgeRampName = "no_such_ramp" };
            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cod, wrongRamp, out var none, out why), why);
            _made.Add(none.Mesh);
            Assert.AreEqual(0, none.TubeSegments, "a ramp the fish does not paint takes no line");
        }

        [Test]
        public void TheRodsBuild_IsWhatItWas()
        {
            HeldPropMeshDef cane = Load<HeldPropMeshDef>(CanePath);
            float width = 1.5f / cane.PxPerMetre;
            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cane, width, out var old, out string why), why);
            _made.Add(old.Mesh);
            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cane, new IsoFacetHeldPropMesh.Options { LineWidthMeters = width },
                                                        out var stated, out why), why);
            _made.Add(stated.Mesh);
            Assert.AreEqual(old.TubeSegments, stated.TubeSegments);
            Assert.AreEqual(old.Mesh.vertexCount, stated.Mesh.vertexCount);
            Assert.AreEqual(old.Ramps.Length, stated.Ramps.Length);
        }

        [Test]
        public void ScalingAboutTheGrip_IsLinear_ForEveryMeshedSpecies()
        {
            CatchItemLibrary art = Load<CatchItemLibrary>(LibraryPath);
            float[] scales = { 0.25f, 0.5f, 0.8f, 1.5f, 2f, 3.2f };
            foreach ((string _, string kind) in Meshed)
            {
                HeldPropMeshDef def = art.HeldMeshFor(kind);
                Assert.IsNotNull(def, $"'{kind}' carries no held mesh");
                Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(def, new IsoFacetHeldPropMesh.Options { Scale = 1f },
                                                            out var one, out string why), why);
                _made.Add(one.Mesh);
                Vector3 grip = def.GripLocalMeters;
                Vector3[] a = one.Mesh.vertices;
                Vector3 size1 = one.Mesh.bounds.size;
                foreach (float s in scales)
                {
                    Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(def, new IsoFacetHeldPropMesh.Options { Scale = s },
                                                                out var got, out why), why);
                    _made.Add(got.Mesh);
                    Vector3[] b = got.Mesh.vertices;
                    Assert.AreEqual(a.Length, b.Length, $"'{kind}' x{s} changed its vertex count");
                    for (int i = 0; i < a.Length; i++)
                        Assert.Less(((b[i] - grip) - (a[i] - grip) * s).magnitude, 1e-4f * Mathf.Max(1f, s),
                                    $"'{kind}' x{s}: vertex {i} is not on the line through the grip");
                    Vector3 want = size1 * s;
                    Assert.Less((got.Mesh.bounds.size - want).magnitude, 1e-4f * Mathf.Max(1f, s),
                                $"'{kind}' x{s}: the bounds did not scale with it");
                }
            }
        }

        [Test]
        public void TheFrontBias_PullsEveryVertexTowardTheCamera_AndTheRodHasNone()
        {
            HeldPropMeshDef cod = Load<HeldPropMeshDef>(CodPath);
            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cod, new IsoFacetHeldPropMesh.Options(), out var flat, out string why), why);
            _made.Add(flat.Mesh);
            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cod, new IsoFacetHeldPropMesh.Options { DepthBiasMeters = 0.4f },
                                                        out var front, out why), why);
            _made.Add(front.Mesh);
            var a = new List<Vector4>(); var b = new List<Vector4>();
            flat.Mesh.GetUVs(0, a); front.Mesh.GetUVs(0, b);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].z + 0.4f, b[i].z, 1e-5f, $"vertex {i}'s depth bias");
                Assert.AreEqual(a[i].x, b[i].x, 0f, "the bias changed a material id");
            }
            Assert.Greater(_config.HeldFishFrontBiasMeters, 0f, "a held fish is drawn in front of his body by default");
        }

        [Test]
        public void TheReadableScale_RaisesATinyFishToTheFloor_AndLeavesABigOneAlone()
        {
            CatchItemLibrary art = Load<CatchItemLibrary>(LibraryPath);
            HeldPropMeshDef herring = art.HeldMeshFor("herring");
            Assert.IsNotNull(herring);
            Vector3 size = herring.Mesh.bounds.size;
            float lengthPx = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * herring.PxPerMetre;

            float tiny = 4f / lengthPx;
            float raised = HeldCatchMesh.ReadableScale(herring, tiny, 16f);
            Assert.AreEqual(16f, lengthPx * raised, 1e-3f, "a tiny fish is not raised to the floor exactly");
            float big = 40f / lengthPx;
            Assert.AreEqual(big, HeldCatchMesh.ReadableScale(herring, big, 16f), 1e-6f, "a fish over the floor was changed");
            Assert.AreEqual(tiny, HeldCatchMesh.ReadableScale(herring, tiny, 0f), 1e-6f, "a floor of 0 changed the scale");
            Assert.AreEqual(1f, HeldCatchMesh.ReadableScale(null, 0f, 16f), "no def and no scale is 1");
            Assert.AreEqual(1f, HeldCatchMesh.ReadableScale(herring, 0f, 0f), "a scale of 0 is 1");
            foreach (string kind in new[] { "crab", "mussel" })
            {
                HeldPropMeshDef shell = art.HeldMeshFor(kind);
                Assert.IsNotNull(shell, $"harness: '{kind}' carries no held mesh");
                Assert.AreEqual(HeldPropMeshDef.ShellfishFamily, shell.Family);
                Assert.AreEqual(1f, HeldCatchMesh.ReadableScale(shell, 1f, 16f), 1e-6f,
                                $"the fish floor raised the {kind}: a crustacean or a handful is drawn at its sheet's size");
            }
            Assert.GreaterOrEqual(_config.HeldFishMinLengthPx, _config.HeldFishKeylineMaxLengthPx, "the floor is under the length the sheet already outlines to read");
        }

        [Test]
        public void TheNearerHand_TakesTheFish_OnlyPastTheSwapDepth()
        {
            const string R = HeldPropSockets.RightHandTool, L = HeldPropSockets.LeftHandTool;
            Assert.AreEqual(L, HeldCatchMesh.NearerSocket(R, L, 0f, -0.2f, 0.05f), "the clearly nearer hand did not take it");
            Assert.AreEqual(R, HeldCatchMesh.NearerSocket(R, L, 0f, -0.03f, 0.05f), "a hair's difference swapped hands");
            Assert.AreEqual(R, HeldCatchMesh.NearerSocket(R, L, 0f, 0.2f, 0.05f), "the farther hand took it");
            Assert.AreEqual(L, HeldCatchMesh.NearerSocket(R, L, 0f, -0.01f, -1f), "a negative swap depth is not 0");
        }

        [Test]
        public void TheFishTone_SetsGainLiftAndDither_AndTheRodIsUntouched()
        {
            HeldPropMeshDef cod = Load<HeldPropMeshDef>(CodPath);
            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cod, new IsoFacetHeldPropMesh.Options(), out var plain, out string why), why);
            _made.Add(plain.Mesh);
            var half = new IsoFacetHeldPropMesh.Options { Tone = true, ToneGain = 1f, ToneLift = 0f, DitherScale = 0.5f };
            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cod, half, out var toned, out why), why);
            _made.Add(toned.Mesh);
            Assert.AreEqual(plain.RampDitherWeights.Length, toned.RampDitherWeights.Length);
            for (int i = 0; i < plain.RampDitherWeights.Length; i++)
                Assert.AreEqual(plain.RampDitherWeights[i] * 0.5f, toned.RampDitherWeights[i], 1e-6f, $"ramp {i}'s dither");
            var off = new IsoFacetHeldPropMesh.Options { Tone = false, DitherScale = 0f };
            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cod, off, out var untoned, out why), why);
            _made.Add(untoned.Mesh);
            CollectionAssert.AreEqual(plain.RampDitherWeights, untoned.RampDitherWeights, "tone off still scaled the dither");

            IsoCharacterFigureRenderer figure = Fisher();
            Assert.IsTrue(figure.SetPose("idle", 0));
            _config.HeldFishToneGain = 1.5f;
            _config.HeldFishToneLift = 0.4f;
            HeldCatchMesh driver = Driver();
            Assert.IsTrue(driver.TryDraw(_config, cod, 1f, HeldPropSockets.RightHandTool, 0f, figure), $"{driver.LastRefusal}");
            Material m = driver.Renderer.FacetMaterial;
            Assert.IsNotNull(m);
            Assert.AreEqual(cod.Gain * 1.5f, m.GetFloat(IsoFacetShaderIds.Gain), 1e-5f, "the fish's gain");
            Assert.AreEqual(cod.Bias + 0.4f, m.GetFloat(IsoFacetShaderIds.Bias), 1e-5f, "the fish's lift");

            _config.HeldFishToneLift = 0.1f;
            Assert.IsTrue(driver.TryDraw(_config, cod, 1f, HeldPropSockets.RightHandTool, 0f, figure));
            Assert.AreEqual(cod.Bias + 0.1f, driver.Renderer.FacetMaterial.GetFloat(IsoFacetShaderIds.Bias), 1e-5f,
                            "a changed tone was not rebuilt");
        }

        // ================================================================ on a posed fisher

        [Test]
        public void OnAPosedFisher_TheFishHangsOnTheSocket_AtItsRungScale_AndPadsTheOverlay()
        {
            HeldPropMeshDef cod = Load<HeldPropMeshDef>(CodPath);
            IsoCharacterFigureRenderer figure = Fisher();
            HeldCatchMesh driver = Driver();
            int socket = figure.SkinDef.IndexOfBone(HeldPropSockets.RightHandTool);
            Assert.AreNotEqual(CharacterSkinDef.NoBone, socket, "the fisher has no right tool socket");

            foreach (string state in new[] { "idle", "walk" })
            {
                Assert.IsTrue(figure.SkinDef.TryGetClip(state, out CharacterSkinDef.SkinClip clip), $"harness: no '{state}'");
                foreach (int frame in new[] { 0, clip.FrameCount - 1 })
                {
                    Assert.IsTrue(figure.SetPose(state, frame), $"harness: the fisher did not pose '{state}' {frame}");
                    Assert.IsTrue(driver.TryDraw(_config, cod, 0.8f, HeldPropSockets.RightHandTool, 0f, figure),
                                  $"'{state}' {frame}: {driver.LastRefusal}");
                    IsoFacetHeldPropRenderer r = driver.Renderer;
                    Assert.AreSame(figure.FacetTransform, r.transform.parent);
                    Assert.IsTrue(r.Visible);
                    Assert.IsTrue(figure.TryBonePoint(socket, out Vector3 grip));
                    Assert.Less((r.transform.localPosition - grip).magnitude, 1e-5f, "the grip is not on the socket");
                    Assert.Less(Quaternion.Angle(r.transform.localRotation, Quaternion.identity), 1e-3f,
                                "with no turn the fish keeps the rig's held pose (D1)");
                    Assert.AreEqual(0.8f, driver.Scale, 1e-6f);
                    Assert.AreEqual(r.TipLocal.magnitude, figure.HeldReachMetres, 1e-5f, "the overlay is not padded");
                }
            }

            Assert.IsTrue(driver.TryDraw(_config, cod, 0.8f, HeldPropSockets.RightHandTool, 90f, figure));
            Assert.AreEqual(90f, Quaternion.Angle(driver.Renderer.transform.localRotation, Quaternion.identity), 1e-2f);

            driver.Hide();
            Assert.IsFalse(driver.Renderer.Visible);
            Assert.AreEqual(0f, figure.HeldReachMetres, "the pad was not given back");
        }

        [Test]
        public void TheFishAndTheRod_EachClaimTheirReach_AndTheOverlayTakesTheLarger()
        {
            IsoCharacterFigureRenderer figure = Fisher();
            figure.SetHeldReachMetres(IsoCharacterFigureRenderer.HeldReachTool, 1.2f);
            figure.SetHeldReachMetres(IsoCharacterFigureRenderer.HeldReachCatch, 0.4f);
            Assert.AreEqual(1.2f, figure.HeldReachMetres, 1e-6f);
            figure.SetHeldReachMetres(0f);
            Assert.AreEqual(0.4f, figure.HeldReachMetres, 1e-6f, "putting the rod down kept the fish's pad");
            figure.SetHeldReachMetres(IsoCharacterFigureRenderer.HeldReachCatch, 0f);
            Assert.AreEqual(0f, figure.HeldReachMetres);
            figure.SetHeldReachMetres(99, 5f);
            Assert.AreEqual(0f, figure.HeldReachMetres, "an unknown claimant is ignored");
        }

        [Test]
        public void EachMissingPiece_IsARefusal_AndAFishThatWasUpGoesDown()
        {
            HeldPropMeshDef cod = Load<HeldPropMeshDef>(CodPath);
            IsoCharacterFigureRenderer figure = Fisher();
            Assert.IsTrue(figure.SetPose("idle", 0));

            GameConfig off = ScriptableObject.CreateInstance<GameConfig>();
            _made.Add(off);
            off.MeshCatchInHand = false;

            Refuses(figure, d => d.TryDraw(off, cod, 1f, HeldPropSockets.RightHandTool, 0f, figure), HeldCatchMesh.Refusal.SwitchOff);
            Refuses(figure, d => d.TryDraw(null, cod, 1f, HeldPropSockets.RightHandTool, 0f, figure), HeldCatchMesh.Refusal.SwitchOff);
            Refuses(figure, d => d.TryDraw(_config, null, 1f, HeldPropSockets.RightHandTool, 0f, figure), HeldCatchMesh.Refusal.NoMesh);
            Refuses(figure, d => d.TryDraw(_config, cod, 1f, "no_such_bone", 0f, figure), HeldCatchMesh.Refusal.NoMountBone);
            Refuses(figure, d => d.TryDraw(_config, cod, 1f, HeldPropSockets.RightHandTool, 0f, null), HeldCatchMesh.Refusal.NoMeshFigure);
            Refuses(figure, d =>
            {
                figure.Visible = false;
                bool drew = d.TryDraw(_config, cod, 1f, HeldPropSockets.RightHandTool, 0f, figure);
                figure.Visible = true;
                return drew;
            }, HeldCatchMesh.Refusal.NoMeshFigure);
        }

        [Test]
        public void TheCarrierAndTheLanding_TurnTheSpriteOff_ExactlyWhenTheMeshDraws()
        {
            // Both callers need a live scene to run a frame, so their wiring is pinned in source: the
            // carried sprite shows only when the mesh did not draw, and the landing sprite goes off on a
            // frame the landing mesh draws.
            string carry = File.ReadAllText(CarryHandsPath).Replace("\r\n", "\n");
            StringAssert.Contains("fish.SetSpriteVisible(!drawn);", carry);
            StringAssert.Contains("HeldCatchMesh.TurnToFacingDegrees(fish.HeldMesh, fish.FacingMap, fish.Facing,", carry);
            string presenter = File.ReadAllText(PresenterPath).Replace("\r\n", "\n");
            StringAssert.Contains("if (landPosed && TryDrawLandMesh())", presenter);
            StringAssert.Contains("_heldSr.enabled = false;", presenter);
        }

        // ================================================================ harness

        private void Refuses(IsoCharacterFigureRenderer figure, System.Func<HeldCatchMesh, bool> draw, HeldCatchMesh.Refusal want)
        {
            HeldPropMeshDef cod = Load<HeldPropMeshDef>(CodPath);
            HeldCatchMesh driver = Driver();
            Assert.IsTrue(figure.Visible, "harness");
            Assert.IsTrue(driver.TryDraw(_config, cod, 1f, HeldPropSockets.RightHandTool, 0f, figure),
                          $"harness: the fish did not draw first ({driver.LastRefusal})");
            bool wasVisible = figure.Visible;
            Assert.IsFalse(draw(driver), $"expected {want}, but the mesh drew");
            Assert.AreEqual(want, driver.LastRefusal);
            Assert.IsFalse(driver.Drawing);
            Assert.IsFalse(driver.Renderer.Visible, $"{want}: the fish that was up is still drawn");
            Assert.AreEqual(0f, figure.HeldReachMetres, $"{want}: the pad was not given back");
            Assert.AreEqual(wasVisible, figure.Visible);
        }

        private IsoCharacterFigureRenderer Fisher()
        {
            var figure = new GameObject("HeldCatchMeshTestsFisher").AddComponent<IsoCharacterFigureRenderer>();
            _figures.Add(figure);
            figure.Configure(Load<CharacterSkinDef>(FisherPath));
            figure.Visible = true;
            Assert.IsTrue(figure.IsConfigured, "harness: the fisher figure did not configure");
            return figure;
        }

        private HeldCatchMesh Driver()
        {
            var d = new HeldCatchMesh(_home.transform);
            _drivers.Add(d);
            return d;
        }

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"harness: no {typeof(T).Name} at {path}");
            return asset;
        }
    }
}
