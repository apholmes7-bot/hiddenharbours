using System;
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
    /// <b>ADR 0022 phase 6: the fleet is mesh.</b> Phases 4 and 5 proved one hull each; this fixture
    /// polices all eleven at once, and it is deliberately CPU-ONLY so CI actually adjudicates it —
    /// the GPU acceptance fixtures (<c>IsoFacetLobsterEndToEndTests</c>,
    /// <c>IsoFacetSideDraggerAcceptanceTests</c>) skip loudly on the "Null Device" runner, and a
    /// check CI cannot run is a check that quietly stops running.
    ///
    /// <para><b>The four questions, in the order they cost this project money:</b></para>
    /// <list type="number">
    ///   <item><b>Is a committed bake stale?</b> Builder-generated assets going stale is the single
    ///   most repeated failure in this repo — the "broken boat" gets debugged in the code for an
    ///   hour before someone re-runs the builder. Every hull is re-extracted from its rig here and
    ///   compared against what is committed.</item>
    ///   <item><b>Did a new rig drop get missed?</b> Art arrives by PR. A hull rig that is neither
    ///   baked nor explicitly excluded fails, so the answer cannot be "nobody noticed".</item>
    ///   <item><b>Is the wiring honest?</b> A mesh-only hull must have no sprite compass and a
    ///   sheeted one must keep hers — that difference is what the owner's V key reads.</item>
    ///   <item><b>Is the dory's RECONSTRUCTED material table right?</b> The one place we transcribe
    ///   what a rig means rather than reading what it exports, and therefore the one place that has
    ///   to be proved in pixels. See <see cref="DoryReconstructedMaterials_ReproduceHerOwnRenderer"/>
    ///   and its sabotage.</item>
    /// </list>
    /// </summary>
    public class HullMeshFleetTests
    {
        static string RepoRoot => RigCatalog.RepoRoot;
        static string RigFolder => Path.Combine(RepoRoot, "docs", "art", "rigs");

        /// <summary>
        /// How a hull rig is RECOGNISED, rather than remembered.
        ///
        /// <para>A hull is a thing that rocks on the sea, and the rock block is the hull contract
        /// (<c>RigCatalog.Install</c> says so: "boats own their rock cycle"). <c>rollA</c> is the
        /// discriminator because it is the one token that appears in every boat rig and in nothing
        /// else — measured over all 50 rigs on 2026-07-23: exactly the eleven hulls. Searching for
        /// <c>ROCK</c> instead would also catch <c>characterIsoRig</c> (which mentions it in a
        /// comment explaining it has none) and <c>rockCrabRig</c> (whose name simply contains it),
        /// and a discriminator with false positives trains people to edit the exclusion list.</para>
        /// </summary>
        const string HullSignal = "rollA";

        static IEnumerable<string> HullRigFilesOnDisk() =>
            Directory.EnumerateFiles(RigFolder, "*.js")
                     .Where(f => File.ReadAllText(f).Contains(HullSignal))
                     .Select(Path.GetFileName)
                     .OrderBy(f => f, StringComparer.Ordinal);

        // ---- 1. coverage: no rig drop can be silently missed --------------------------------

        [Test]
        public void EveryHullRigOnDisk_IsEitherBakedOrExplicitlyExcluded()
        {
            var baked = new HashSet<string>(HullMeshFleet.BakedRigFileNames, StringComparer.Ordinal);
            var missed = HullRigFilesOnDisk()
                         .Where(f => !baked.Contains(f) && !HullMeshFleet.NotHulls.ContainsKey(f))
                         .ToList();

            CollectionAssert.IsEmpty(missed,
                "A rig that rocks on the sea is in docs/art/rigs/ but this fleet neither bakes it nor " +
                "says why not: " + string.Join(", ", missed) + ".\n" +
                "Art arrives by PR from the art director, so this is the expected way a new hull " +
                "shows up. Add it to HullMeshFleet.Hulls (and it gets a mesh), or to " +
                "HullMeshFleet.NotHulls with the reason (and it does not). Do not silence this by " +
                "editing the rig — docs/art/rigs/** is read-only to us.");
        }

        [Test]
        public void TheExclusionList_OnlyNamesRigsThatExist()
        {
            // An exclusion list that outlives its files rots into folklore: it goes on explaining why
            // something is skipped long after the something is gone.
            foreach (var kvp in HullMeshFleet.NotHulls)
                FileAssert.Exists(Path.Combine(RigFolder, kvp.Key),
                    $"HullMeshFleet.NotHulls excludes '{kvp.Key}' but no such rig exists any more. " +
                    "Delete the entry.");
        }

        [Test]
        public void EveryCatalogRig_ExistsOnDisk()
        {
            foreach (var hull in HullMeshFleet.Hulls)
                FileAssert.Exists(Path.Combine(RepoRoot, hull.ScriptPath),
                    $"{hull.Key} points at {hull.ScriptPath}, which is not there.");
        }

        // ---- 2. the catalog is well formed --------------------------------------------------

        [Test]
        public void Catalog_HasUniqueKeysIdsAndPaths()
        {
            var hulls = HullMeshFleet.Hulls;
            CollectionAssert.AllItemsAreUnique(hulls.Select(h => h.Key).ToList(), "duplicate hull key");
            CollectionAssert.AllItemsAreUnique(hulls.Select(h => h.MeshId).ToList(), "duplicate mesh id");
            CollectionAssert.AllItemsAreUnique(hulls.Select(h => h.MeshAssetPath).ToList(),
                                               "two hulls would write the same asset");

            // One visual must not be claimed by two hulls: the last bake would silently win.
            var visuals = hulls.SelectMany(h => h.VisualAssetPaths).ToList();
            CollectionAssert.AllItemsAreUnique(visuals, "two hulls claim the same BoatVisualDef");
        }

        [Test]
        public void Catalog_IdsFollowTheProjectConvention()
        {
            // CLAUDE.md §5: ids are type.snake_case, append-only and stable.
            const string idRx = @"^hullmesh\.[a-z0-9]+(_[a-z0-9]+)*$";
            const string visRx = @"^visual\.[a-z0-9]+(_[a-z0-9]+)*$";

            foreach (var hull in HullMeshFleet.Hulls)
            {
                Assert.That(hull.MeshId, Does.Match(idRx), $"{hull.Key}: bad mesh id");
                foreach (string vid in hull.VisualIds)
                    Assert.That(vid, Does.Match(visRx), $"{hull.Key}: bad visual id");
            }
        }

        [Test]
        public void Catalog_SuppliesAVisualIdForExactlyTheHullsThatNeedOne()
        {
            foreach (var hull in HullMeshFleet.Hulls)
            {
                if (hull.HasBakedSheet)
                    // Its visual already exists with an id of its own, and ids are append-only —
                    // carrying a second opinion here is how a def ends up renamed by a re-bake.
                    CollectionAssert.IsEmpty(hull.VisualIds,
                        $"{hull.Key} has a baked sheet, so its visual already exists and its id must " +
                        "not be restated here.");
                else
                    Assert.AreEqual(hull.VisualAssetPaths.Length, hull.VisualIds.Length,
                        $"{hull.Key} is mesh-only: every visual it creates needs an id.");
            }
        }

        // ---- 3. the committed bakes are not stale --------------------------------------------

        [Test]
        public void EveryCommittedHullMesh_MatchesAFreshExtractionFromItsRig()
        {
            var stale = new List<string>();

            foreach (var hull in HullMeshFleet.Hulls)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                Assert.IsNotNull(def, $"{hull.Key}: no committed def at {hull.MeshAssetPath}. " +
                                      "Run Hidden Harbours ▸ Art ▸ 3D Hulls ▸ Bake ALL fleet hull meshes.");
                Assert.IsTrue(def.IsUsable(), $"{hull.Key}: the committed def is not usable.");
                Assert.IsNotNull(def.Mesh, $"{hull.Key}: the mesh sub-asset is missing.");

                using IRigScriptHost host = RigScriptHostFactory.Create();
                RigMeshData fresh = RigMeshExtractor.ExtractFrom(host, hull.ScriptPath, hull.GlobalName);
                RigMeshBuild built = RigMeshBuilder.Build(fresh, $"{hull.GlobalName}Check");

                try
                {
                    void Same(string what, object committed, object rig)
                    {
                        if (!Equals(committed, rig))
                            stale.Add($"{hull.Key}.{what}: committed {committed}, rig says {rig}");
                    }

                    Same("verts", def.Mesh.vertexCount, built.Mesh.vertexCount);
                    Same("tris", def.Mesh.triangles.Length, built.Mesh.triangles.Length);
                    Same("CellW", def.CellW, fresh.W);
                    Same("CellH", def.CellH, fresh.H);
                    Same("PxPerMetre", def.PxPerMetre, fresh.PxPerMetre);
                    Same("ElevationDeg", def.ElevationDeg, (float)fresh.DefaultElev);
                    Same("Ramps", def.Ramps.Length, fresh.Materials.Count);
                    Same("SourceRigPath", def.SourceRigPath, hull.ScriptPath);
                    Same("Id", def.Id, hull.MeshId);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(built.Mesh);
                }
            }

            CollectionAssert.IsEmpty(stale,
                "Committed hull meshes no longer match their rigs:\n  " + string.Join("\n  ", stale) +
                "\n\nThis is the repo's most repeated failure — a builder-generated asset went stale " +
                "and the boat gets debugged in the code. Re-run Hidden Harbours ▸ Art ▸ 3D Hulls ▸ " +
                "Bake ALL fleet hull meshes and commit the result.");
        }

        [Test]
        public void EveryCommittedRockAmplitude_MatchesItsRigsRockBlock()
        {
            // Rock is TRANSCRIPTION, not tuning (RigMeshAssetBaker says so), so a drift here means
            // either a stale bake or somebody tuning the wrong end — the rig owns these numbers.
            foreach (var hull in HullMeshFleet.Hulls)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                Assert.IsNotNull(def, hull.Key);

                using IRigScriptHost host = RigScriptHostFactory.Create();
                host.Execute(File.ReadAllText(Path.Combine(RepoRoot, hull.ScriptPath)));
                string g = hull.GlobalName;

                Assert.IsTrue(host.EvaluateBool($"typeof {g}.ROCK === 'object' && {g}.ROCK !== null"),
                    $"{hull.Key} exports no ROCK block — a hull that cannot rock is not a hull.");

                Assert.AreEqual((float)host.EvaluateNumber($"{g}.ROCK.rollA || 0"),
                                def.RockRollDegrees, 1e-4f, $"{hull.Key} roll");
                Assert.AreEqual((float)host.EvaluateNumber($"{g}.ROCK.pitchA || 0"),
                                def.RockPitchDegrees, 1e-4f, $"{hull.Key} pitch");
                Assert.AreEqual((float)host.EvaluateNumber($"{g}.ROCK.heaveA || 0"),
                                def.RockHeavePixels, 1e-4f, $"{hull.Key} heave");
            }
        }

        /// <summary>
        /// The tanker works at 16 px = 1 m, half the fleet standard, because at 32 she would be
        /// ~3,500 px long. She is here as a REGRESSION TARGET, not as trivia: she is the only hull
        /// that can catch anything downstream quietly assuming 32.
        /// </summary>
        [Test]
        public void PxPerMetre_IsReadFromEachRigAndTheTankerIsNotTheFleetStandard()
        {
            var byKey = new Dictionary<string, int>();
            foreach (var hull in HullMeshFleet.Hulls)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                Assert.IsNotNull(def, hull.Key);

                using IRigScriptHost host = RigScriptHostFactory.Create();
                host.Execute(File.ReadAllText(Path.Combine(RepoRoot, hull.ScriptPath)));
                Assert.AreEqual((int)host.EvaluateNumber($"{hull.GlobalName}.PX"), def.PxPerMetre,
                    $"{hull.Key}: committed px/m disagrees with the rig's own PX.");
                byKey[hull.Key] = def.PxPerMetre;
            }

            Assert.AreEqual(16, byKey["tanker"],
                "The tanker is the fleet's only half-scale rig. If this became 32 either the rig " +
                "changed or somebody 'corrected' it — she would then be twice her real length.");
            foreach (var kvp in byKey.Where(k => k.Key != "tanker"))
                Assert.AreEqual(32, kvp.Value, $"{kvp.Key} is not at the fleet standard 32 px/m.");
        }

        // ---- 4. the wiring is honest ----------------------------------------------------------

        [Test]
        public void EveryFleetVisual_PointsAtItsOwnBakedMesh()
        {
            // Wiring is unconditional — every hull in the fleet carries her mesh, whether or not she is
            // presented as one. See OverlayBlockedReason.
            foreach (var hull in HullMeshFleet.Hulls)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                foreach (string path in hull.VisualAssetPaths)
                {
                    var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(path);
                    Assert.IsNotNull(visual, $"{hull.Key}: no visual at {path}.");
                    Assert.AreSame(def, visual.HullMesh,
                        $"{visual.Id} points at the wrong hull mesh.");
                    Assert.AreEqual(def.ElevationDeg, visual.ArtBakeElevationDegrees, 1e-3f,
                        $"{visual.Id}: anchors and wake would foreshorten against a different camera " +
                        "than the mesh is projected through.");
                }
            }
        }

        /// <summary>
        /// <b>The rollout is variant-gated, and the gate is the sprite overlays.</b> A hull whose oars
        /// or outboard are baked per facing cell must stay on the sprite compass, because
        /// <c>BoatHullSkinner.ApplyMesh</c> drops those overlays — a mesh rotates continuously and
        /// there is no cell to look up. This is not a caution: flipping them turned four
        /// <c>PilotableFleetPlayTests</c> red with "the dory has her oars: expected not null".
        /// </summary>
        [Test]
        public void OnlyOverlayFreeHulls_ArePresentedAsMesh()
        {
            foreach (var hull in HullMeshFleet.Hulls)
                foreach (string path in hull.VisualAssetPaths)
                {
                    var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(path);
                    Assert.IsNotNull(visual, path);

                    if (hull.FlipsToMesh)
                    {
                        Assert.AreEqual(BoatHullVariant.Mesh, visual.Variant,
                            $"{visual.Id} wears no sprite overlay and should be presented as a mesh — " +
                            "the owner ruled the fleet goes mesh wherever it can.");
                        Assert.IsFalse(visual.HasOarSheets() || visual.HasMotor(),
                            $"{visual.Id} is flipped to Mesh but binds oar/motor sheets, which the mesh " +
                            "path silently drops. Give her an OverlayBlockedReason in HullMeshFleet.");
                    }
                    else
                    {
                        Assert.AreEqual(BoatHullVariant.Sprite, visual.Variant,
                            $"{visual.Id} is blocked ({hull.OverlayBlockedReason}) and must stay a " +
                            "sprite hull until that overlay has a mesh of its own.");
                        Assert.IsTrue(visual.HasOarSheets() || visual.HasMotor(),
                            $"{visual.Id} carries an OverlayBlockedReason but binds no oar or motor " +
                            "sheets, so the block is stale — flip her.");
                        Assert.IsTrue(visual.HasHullMesh(),
                            $"{visual.Id} is blocked, but her mesh must still be baked and wired so the " +
                            "flip is one field later.");
                    }
                }
        }

        /// <summary>
        /// The sheet is what the owner's V key toggles against, so "has a sheet" must mean "still has
        /// a compass" — and "no sheet" must mean NO compass, because a half-populated one would offer
        /// him an A/B whose sprite half does not exist.
        /// </summary>
        [Test]
        public void MeshOnlyHulls_HaveNoSpriteCompass_AndSheetedHullsKeepTheirs()
        {
            foreach (var hull in HullMeshFleet.Hulls)
                foreach (string path in hull.VisualAssetPaths)
                {
                    var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(path);
                    Assert.IsNotNull(visual, path);

                    if (hull.HasBakedSheet)
                        Assert.IsTrue(visual.HasFullCompass(),
                            $"{visual.Id} lost her sprite compass. She has baked art, and keeping it " +
                            "wired is the ONLY check on the mesh path that works by eye (V at the helm).");
                    else
                        Assert.IsFalse(visual.HasFullCompass(),
                            $"{visual.Id} has no baked sheet, so a compass here is fiction — the V key " +
                            "would offer an A/B with nothing on the sprite side.");
                }
        }

        // ---- 5. the dory's reconstructed material table, proved in pixels ---------------------

        /// <summary>
        /// <b>The one transcription in the whole pipeline, and therefore the one thing here that could
        /// be confidently wrong.</b>
        ///
        /// <para>The dory predates the <c>MATS</c> convention and selects her ramps inline, so the
        /// shim REBUILDS her material table from her own <c>RAMP</c>/<c>IRON</c> consts
        /// (<see cref="RigMeshSymbols.Reconstructions"/>). Reading a rig and declaring what it means
        /// is precisely how this project has shipped mirrored boats five times, so the claim is
        /// settled the only way that counts: the truth side of this compare is HER OWN
        /// <c>render()</c>, which never touches the reconstruction, against a reference rasterisation
        /// that is nothing but the reconstruction. A wrong ramp or a wrong offset does not shift a
        /// boundary pixel — it recolours the entire boat.</para>
        /// </summary>
        [Test]
        public void DoryReconstructedMaterials_ReproduceHerOwnRenderer()
        {
            FleetHull dory = HullMeshFleet.Get("dory");
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = RigMeshExtractor.ExtractFrom(host, dory.ScriptPath, dory.GlobalName);

            CollectionAssert.Contains(data.ReconstructedSymbols, "MATS",
                "This test exists because the dory's MATS is reconstructed. If it is now EXPORTED by " +
                "the rig, that is good news: delete the RigMeshSymbols.Reconstructions entry and this " +
                "test with it.");

            double worst = WorstFaceDiffPercent(host, data, dory.GlobalName);

            // The bar is the same one RigMeshMenu applies to every hull. What matters is the ORDER OF
            // MAGNITUDE: a correct table lands in tenths of a percent (residual last-ULP shade steps
            // at facet boundaries), and the sabotage below shows what a wrong one looks like.
            Assert.Less(worst, 0.5,
                $"The dory's reconstructed material table does not reproduce her own renderer " +
                $"(worst {worst:F4}% of cell pixels differ across 8 headings).");
        }

        [Test]
        public void DoryReconstruction_Sabotage_AWrongRampOffsetIsCaught()
        {
            FleetHull dory = HullMeshFleet.Get("dory");
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = RigMeshExtractor.ExtractFrom(host, dory.ScriptPath, dory.GlobalName);

            double honest = WorstFaceDiffPercent(host, data, dory.GlobalName);

            // Shift the wood ramp one shade step — the smallest lie the reconstruction could tell.
            // (Getting `off` wrong is far likelier than getting the ramp wrong: the dory's clamp
            // bounds are what encode it, and they are easy to misread.)
            data.Materials[0].Off += 1;
            double sabotaged = WorstFaceDiffPercent(host, data, dory.GlobalName);

            Assert.Greater(sabotaged, honest * 20.0,
                $"A one-step error in the reconstructed ramp offset moved the image from {honest:F4}% " +
                $"to only {sabotaged:F4}%. That means the check above is not actually reading the " +
                "material table, so it would pass with a wrong one — fix the check, not the number.");
        }

        /// <summary>Worst whole-cell divergence between the rig's own render and a reference
        /// rasterisation of the extracted faces, over the 8 cardinal headings.</summary>
        static double WorstFaceDiffPercent(IRigScriptHost host, RigMeshData data, string global)
        {
            double worst = 0;
            for (int dir = 0; dir < 8; dir++)
            {
                var view = new RigViewOptions(dir, data.DefaultElev);
                byte[] truth = host.EvaluateBytes($"{global}.render({view.ToJsArgs()})");
                var diff = RigMeshReferenceRasterizer.Compare(
                    truth, RigMeshReferenceRasterizer.RenderFromFaces(data, view), data.W, data.H);
                worst = Math.Max(worst, diff.PercentDiffering);
            }
            return worst;
        }
    }
}
