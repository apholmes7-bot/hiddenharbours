using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The five hulls the 2026-08-12 fleet pack's last three rigs make</b> — the zodiac's two
    /// builds, the reshaped sport skiff, and the two battlewagons.
    ///
    /// <para>The lobster generator has <see cref="LobsterVariantFleetTests"/> and this is the same
    /// argument for the families that followed her: three tables name these hulls
    /// (<see cref="ZodiacFleet"/>, <see cref="SportFisherFleet"/> and the <c>sportSkiffMk2</c> row in
    /// <see cref="HullMeshFleet.OneHullPerRig"/>), four lanes read them, and a table that quietly
    /// stopped being the whole list would fail in the least visible way available — a plausible boat
    /// that is the wrong one.</para>
    ///
    /// <para><b>What is different here, and why it needs its own fixture rather than a row in
    /// hers.</b> The sport fisher is a REGISTRY: her global publishes no cell, no elevation and no
    /// renderer, so her extraction carries a <see cref="RigHullExtraction.HullScope"/> that no
    /// previously-baked hull has. Half of what follows exists to prove that scope actually reached
    /// the hull it names — because the failure mode if it did not is silent. Her own
    /// <c>byId</c> resolves an unknown id to the FIRST hull, so a typo bakes the 16.2 m boat under
    /// the 27.4 m boat's id and every count still looks plausible.</para>
    /// </summary>
    public class FleetPackHullTests
    {
        static string RepoRoot => RigCatalog.RepoRoot;

        static IRigScriptHost Host(string scriptPath)
        {
            var host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Path.Combine(RepoRoot, scriptPath)));
            return host;
        }

        /// <summary>Every mesh id this PR added, with the asset stem the bake wrote it to.</summary>
        static IEnumerable<(string Key, string MeshId, string AssetName, string SidecarStem)> Five()
        {
            foreach (ZodiacBuild b in ZodiacFleet.All)
                yield return (b.Key, b.MeshId, b.AssetName, b.SidecarStem);
            foreach (SportFisherHull h in SportFisherFleet.All)
                yield return (h.Key, h.MeshId, h.AssetName, h.SidecarStem);
            yield return ("sportSkiffMk2", "hullmesh.sport_skiff_mk2_iso", "SportSkiffMk2Iso",
                          "sportSkiffMk2Iso");
        }

        // ---- 1. each list is still the whole list ------------------------------------------------

        /// <summary>
        /// <b>The zodiac's builds are the rig's own.</b> A drop that added a third build would leave
        /// this repo baking two, passing everything, and saying nothing about the third.
        /// </summary>
        [Test]
        public void TheZodiacsBuilds_AreTheRigsOwn()
        {
            using IRigScriptHost host = Host(ZodiacFleet.ScriptPath);
            Assert.AreEqual(ZodiacFleet.ExpectedBuildSignature, ZodiacFleet.BuildSignature(host),
                "The rig's BUILDS are no longer the ones ZodiacFleet enumerates. Update the fleet, " +
                "FleetFlotationTableTests.Authored and FleetDeckOccupancyTableTests.Authored " +
                "together — a build with no flotation row is a hull the sea draws through.");
        }

        [Test]
        public void TheSportFishersHulls_AreTheRigsOwn()
        {
            using IRigScriptHost host = Host(SportFisherFleet.ScriptPath);
            Assert.AreEqual(SportFisherFleet.ExpectedHullSignature,
                            SportFisherFleet.HullSignature(host),
                "The rig's HULLS are no longer the ones SportFisherFleet enumerates.");
        }

        // ---- 2. the derived names round-trip through the tools that consume them ------------------

        /// <summary>
        /// <b>The deck id the importer will mint must be the id these fleets say it is.</b> The
        /// importer does not take an id, it DERIVES one from the sidecar's file stem — so the stem is
        /// the input to somebody else's naming function, and one that did not round-trip would mint a
        /// deck id no authored table names.
        /// </summary>
        [Test]
        public void EveryDerivedName_RoundTripsThroughTheImportersOwnConventions()
        {
            foreach (var h in Five())
            {
                string stem = DeckSidecarImporter.StripRigSuffix(h.SidecarStem);
                Assert.AreEqual(h.SidecarStem, stem,
                    $"{h.Key}: the importer strips a 'Rig' suffix; this stem does not survive it.");

                string assetName = char.ToUpperInvariant(stem[0]) + stem.Substring(1);
                Assert.AreEqual(h.AssetName, assetName,
                    $"{h.Key}: the importer capitalises the stem to get the Def file name, and that " +
                    "must be the file the mesh bake writes her visual beside.");

                Assert.AreEqual(h.MeshId, "hullmesh." + DeckSidecarImporter.SnakeCase(stem),
                    $"{h.Key}: her mesh id and her deck id no longer share a body, so the two " +
                    "authored tables' keys stop lining up with the assets on disk.");
            }
        }

        [Test]
        public void EveryId_FollowsTheProjectConvention()
        {
            foreach (var h in Five())
                Assert.That(h.MeshId, Does.Match(@"^hullmesh\.[a-z0-9]+(_[a-z0-9]+)*$"), h.Key);
        }

        /// <summary>The ids this bake chose are the ones both authored tables are keyed by — so
        /// nobody has to check by eye that five strings match five strings, twice.</summary>
        [Test]
        public void TheChosenIds_AreTheOnesTheAuthoredTablesAreKeyedBy()
        {
            foreach (var h in Five())
            {
                Assert.IsTrue(FleetFlotationTableTests.Authored.ContainsKey(h.MeshId),
                    $"{h.Key}: no flotation row for {h.MeshId} — she would float with her deck under " +
                    "water, or not float at all.");
                Assert.IsTrue(FleetDeckOccupancyTableTests.Authored.ContainsKey(h.MeshId),
                    $"{h.Key}: no deck-occupancy row for {h.MeshId}.");
            }
        }

        // ---- 3. the fleet rows are built from these tables and nothing else -----------------------

        [Test]
        public void TheFleetCarriesExactlyTheseFive_WithTheirOwnExtractions()
        {
            foreach (var h in Five())
            {
                FleetHull row = HullMeshFleet.Get(h.Key);
                Assert.AreEqual(h.MeshId, row.MeshId, h.Key);
                Assert.IsFalse(row.HasBakedSheet,
                    $"{h.Key}: none of the five has a 32-facing sheet, and claiming one would make " +
                    "the bake WIRE a compass that does not exist instead of creating a mesh-only " +
                    "visual.");
                Assert.IsTrue(row.FlipsToMesh,
                    $"{h.Key}: nothing blocks these — they wear no sprite overlay.");
            }

            // The zodiac and the sport fisher are cells of a generator; the Mk2 is one hull per rig.
            foreach (ZodiacBuild b in ZodiacFleet.All)
                Assert.IsTrue(HullMeshFleet.Get(b.Key).IsVariant, $"{b.Key} must name a variant");
            foreach (SportFisherHull h in SportFisherFleet.All)
                Assert.IsTrue(HullMeshFleet.Get(h.Key).IsVariant, $"{h.Key} must name a variant");
            Assert.IsFalse(HullMeshFleet.Get("sportSkiffMk2").IsVariant,
                "The Mk2's rig makes exactly one boat, so she takes the static-F path every hull " +
                "baked before 2026-08-13 takes.");
        }

        /// <summary>
        /// <b>Only the sport fisher carries a hull SCOPE, and both her hulls carry a different one.</b>
        ///
        /// <para>The scope is what makes a registry rig readable at all, and it is also the one field
        /// whose mistyping is silent — <c>byId</c> falls back to the first hull. So this pins that
        /// the two rows do not share a scope, and that the families which need no scope do not carry
        /// one (a stray scope on the zodiac would read a cell off an object that has none).</para>
        /// </summary>
        [Test]
        public void OnlyTheRegistryRig_CarriesAHullScope_AndHerTwoDiffer()
        {
            var scopes = SportFisherFleet.All.Select(h => h.Extraction.HullScope).ToList();
            CollectionAssert.AllItemsAreUnique(scopes);
            foreach (string s in scopes)
                Assert.IsNotEmpty(s, "a sport fisher row with no scope reads the GLOBAL's cell, and " +
                                     "her global has none — the bake would pose at NaN, not fail.");

            foreach (ZodiacBuild b in ZodiacFleet.All)
                Assert.IsEmpty(b.Extraction.HullScope ?? "",
                    $"{b.Key}: both zodiac builds share ONE cell (272×248) and one camera, so a " +
                    "scope here would point at an object the rig does not publish.");
        }

        // ---- 4. the three rigs are baked and no longer excluded -----------------------------------

        /// <summary>
        /// Each of the three <see cref="HullMeshFleet.NotHulls"/> entries said "delete this when the
        /// bake lands". This is the other half of that promise: a rig cannot be both baked and
        /// excluded, and the coverage test would otherwise never notice a stale exclusion.
        /// </summary>
        [Test]
        public void TheThreeRigs_AreBakedAndNoLongerExcluded()
        {
            foreach (string rig in new[] { "zodiacIsoRig.js", "sportSkiffMk2IsoRig.js",
                                           "sportFisherIsoRig2.js" })
            {
                Assert.IsFalse(HullMeshFleet.NotHulls.ContainsKey(rig),
                    $"{rig} is baked now — its NotHulls entry asked to be deleted when it was.");
                CollectionAssert.Contains(HullMeshFleet.BakedRigFileNames.ToList(), rig);
            }
        }

        // ---- 5. every hull's whole chain is on disk ----------------------------------------------

        /// <summary>
        /// <b>Sidecar → deck Def → visual → mesh, per hull.</b> The chain only means anything end to
        /// end; a mesh with no deck is a boat you cannot stand on, and a deck with no visual is a
        /// boat nobody sees.
        /// </summary>
        [Test]
        public void EveryHull_HasHerSidecarDeckVisualAndMesh()
        {
            foreach (var h in Five())
            {
                string sidecar = Path.Combine(RepoRoot, "docs/art/rigs/gameplay",
                                              h.SidecarStem + ".gameplay.json");
                Assert.IsTrue(File.Exists(sidecar), $"{h.Key}: no sidecar at {sidecar}");

                var mesh = AssetDatabase.LoadAssetAtPath<HullMeshDef>(
                    $"Assets/_Project/Data/Boats/HullMeshes/{h.AssetName}HullMesh.asset");
                Assert.IsNotNull(mesh, $"{h.Key}: no HullMeshDef");
                Assert.AreEqual(h.MeshId, mesh.Id, $"{h.Key}: mesh id");
                Assert.IsTrue(mesh.IsUsable(),
                    $"{h.Key}: the def baked but is not usable — the commonest cause is a MATS table " +
                    "wider than the facet shader's 16-entry _RampMeta.");

                var deck = AssetDatabase.LoadAssetAtPath<BoatDeckDef>(
                    $"Assets/_Project/Data/Boats/Decks/{h.AssetName}.asset");
                Assert.IsNotNull(deck, $"{h.Key}: no BoatDeckDef");

                var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                    $"Assets/_Project/Data/Boats/Visuals/{h.AssetName}.asset");
                Assert.IsNotNull(visual, $"{h.Key}: no BoatVisualDef");
                Assert.AreSame(mesh, visual.HullMesh, $"{h.Key}: visual is not wearing her own mesh");
                Assert.AreSame(deck, visual.Deck, $"{h.Key}: visual is not wearing her own deck");
                Assert.IsFalse(visual.HasFullCompass(),
                    $"{h.Key}: mesh-only hulls must report no compass, so the V-key A/B does not " +
                    "offer half a comparison that does not exist.");
            }
        }

        /// <summary>
        /// <b>Five committed meshes, five different boats.</b>
        ///
        /// <para>This is the test that catches the defect nothing else can see. Both generator rigs
        /// resolve an id they do not recognise to their DEFAULT hull, silently — measured, on both.
        /// A mistyped descriptor therefore bakes the right ID over the wrong GEOMETRY, and every
        /// other check still passes: the asset exists, the id is right, the cell is plausible.
        /// ⚠️ Face COUNT is not the oracle either — #541 measured two lobster variants at 637 faces
        /// that are different boats. A fingerprint over the committed VERTICES is.</para>
        /// </summary>
        [Test]
        public void TheFiveCommittedMeshes_AreFiveDifferentBoats()
        {
            var prints = new Dictionary<string, string>();
            foreach (var h in Five())
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(
                    $"Assets/_Project/Data/Boats/HullMeshes/{h.AssetName}HullMesh.asset");
                Assert.IsNotNull(def?.Mesh, $"{h.Key}: no mesh");
                prints[h.Key] = Fingerprint(def.Mesh);
            }

            foreach (var a in prints)
            foreach (var b in prints)
            {
                if (a.Key == b.Key) continue;
                Assert.AreNotEqual(a.Value, b.Value,
                    $"{a.Key} and {b.Key} are the SAME geometry. Both generator rigs fall back to " +
                    "their default hull on an id they do not know, so this is what a typo in a " +
                    "FaceExpression or a HullScope looks like — the right id over the wrong boat.");
            }
        }

        static string Fingerprint(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < v.Length; i++)
                {
                    h = Hash(h, v[i].x); h = Hash(h, v[i].y); h = Hash(h, v[i].z);
                }
                return $"{v.Length}:{mesh.triangles.Length}:{h:x8}";
            }
        }

        static uint Hash(uint h, float f)
        {
            unchecked
            {
                // Quantise before hashing: the point is which BOAT this is, not the last bit.
                int q = Mathf.RoundToInt(f * 100000f);
                for (int b = 0; b < 4; b++)
                {
                    h ^= (uint)((q >> (b * 8)) & 0xFF);
                    h *= 16777619u;
                }
                return h;
            }
        }

        // ---- 6. the two shims this PR added are still aimed at something --------------------------

        /// <summary>
        /// <b>The sport fisher's INNER widening must match its anchor exactly once.</b>
        ///
        /// <para>Unlike the exported-literal shim, this one fails QUIETLY when it goes stale: a
        /// mis-aimed insert leaves a rig that still runs and still draws, it simply has no faces to
        /// extract, and the error surfaces much later from an expression that looks unrelated. So the
        /// anchor is asserted directly against the committed rig text.</para>
        /// </summary>
        [Test]
        public void TheSportFishersInnerWidening_IsStillAimedAtHerReturnLiteral()
        {
            string path = Path.Combine(RepoRoot, SportFisherFleet.ScriptPath);
            string source = File.ReadAllText(path);

            var widenings = RigMeshSymbols.InnerWideningsFor(SportFisherFleet.ScriptPath);
            Assert.AreEqual(1, widenings.Count, "she is the only rig that needs one");

            // ApplyInnerWidenings throws unless the anchor matches exactly once.
            string widened = RigMeshExtractor.ApplyInnerWidenings(source, SportFisherFleet.ScriptPath);
            Assert.AreNotEqual(source, widened, "the widening inserted nothing");
            StringAssert.Contains("faceList", widened);
        }

        /// <summary>
        /// The shim names must be ABSENT from the unmodified rigs — the same guard
        /// <c>RigMeshExtractionTests</c> keeps for the lobster generator. A rig that started
        /// exporting one of these would make the shim a silent no-op over the rig's own version.
        /// </summary>
        [Test]
        public void TheShimNames_AreAbsentFromTheUnmodifiedRigs()
        {
            foreach ((string script, string global) in new[]
                     {
                         (ZodiacFleet.ScriptPath, ZodiacFleet.GlobalName),
                         (SportFisherFleet.ScriptPath, SportFisherFleet.GlobalName),
                     })
            {
                using IRigScriptHost host = Host(script);
                Assert.IsFalse(host.EvaluateBool($"typeof {global}.variantFaces === 'function'"),
                    $"{script} now exports variantFaces itself — retire the reconstruction rather " +
                    "than shadowing the rig's own.");
            }

            using IRigScriptHost fisher = Host(SportFisherFleet.ScriptPath);
            Assert.IsFalse(
                fisher.EvaluateBool(
                    $"typeof {SportFisherFleet.GlobalName}.byId('convertible').faceList === 'function'"),
                "the rig now returns faceList from makeRig — delete the InnerWidenings entry, it is " +
                "no longer buying anything.");
        }

        /// <summary>
        /// <b>The zodiac's ramp table fits the shader, and it fits because it is HERS.</b>
        ///
        /// <para>She declares eighteen materials and <c>_RampMeta</c> is a <c>float4[16]</c>, so her
        /// first bake produced a def and then failed <see cref="HullMeshDef.IsUsable"/>. The bake
        /// hands the extractor the materials her hull faces actually reference — fourteen, the same
        /// fourteen on both builds. This pins both halves: that the table fits, and that it still
        /// starts with <c>hull</c>, which is what the face packer resolves an unknown material to.
        /// </para>
        /// </summary>
        [Test]
        public void TheZodiacsRampTable_FitsTheShader_AndStillLeadsWithHerDefault()
        {
            foreach (ZodiacBuild b in ZodiacFleet.All)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(
                    $"Assets/_Project/Data/Boats/HullMeshes/{b.AssetName}HullMesh.asset");
                Assert.IsNotNull(def, b.Key);
                Assert.LessOrEqual(def.Ramps.Length, 16,
                    $"{b.Key}: the facet shader's _RampMeta holds 16, and a def over that baked fine " +
                    "and then failed IsUsable() — which is exactly how she failed on 2026-08-16.");

                // The committed def stores no material NAMES — the ramp index is the identity — so
                // the names are checked against a fresh extraction, which is the thing that decides
                // them.
                RigMeshData data;
                using (IRigScriptHost host = RigScriptHostFactory.Create())
                    data = RigMeshExtractor.ExtractFrom(host, ZodiacFleet.ScriptPath,
                                                        ZodiacFleet.GlobalName,
                                                        hull: b.Extraction);

                Assert.AreEqual(data.Materials.Count, def.Ramps.Length,
                    $"{b.Key}: the committed ramp count disagrees with a fresh extraction.");
                Assert.AreEqual(14, data.Materials.Count,
                    $"{b.Key}: her hull faces reference fourteen of her eighteen DECLARED materials " +
                    "(the four nobody names are tubed, blulit, amb and amblit — the last two are the " +
                    "lit nav-light variants her beacon renderer uses, not her hull). A different " +
                    "number means her palette moved: re-measure which materials her faces name " +
                    "before touching this.");
                Assert.AreEqual("hull", data.Materials[0].Name,
                    $"{b.Key}: the default ramp must stay FIRST — the face packer resolves an " +
                    "unknown material name to index 0, so reordering silently repaints the boat.");

                var names = new HashSet<string>();
                foreach (RigFace f in data.Faces) names.Add(data.Materials[f.Mat].Name);
                Assert.LessOrEqual(names.Count, data.Materials.Count,
                    $"{b.Key}: a face resolved to a material outside the table.");
            }
        }
    }
}
