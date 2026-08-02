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
                                      "Run Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake ALL fleet hull meshes.");
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
                "and the boat gets debugged in the code. Re-run Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ " +
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
        /// <b>Every mesh hull must be WATERTIGHT and must float — the guard phase 6 needed and did
        /// not have.</b>
        ///
        /// <para>The displaced sea and the hulls share one private z-buffer, and that z-test has no
        /// concept of a SHELL: without the clamp a hull's low interior surfaces (cockpit sole, hold
        /// floor, working deck) are just geometry under a big enough lift, and the sea draws inside
        /// the boat. <c>WatertightDeckHeightMeters</c> = 0 turns the clamp entirely off — the
        /// renderer does not even call it — which is exactly the pre-fix state the owner rejected on
        /// 2026-07-23 and again on 2026-07-25 ("water shader is leaking onto deck of iso hulls
        /// making boats look semi submerged").</para>
        ///
        /// <para><b>Why this fixture and not the acceptance suite.</b> The watertight asserts in
        /// <c>HullWaterlineAcceptanceTests</c> are the right asserts, but they reach exactly two
        /// hulls by literal asset path and the storm tests among them need a GPU (they
        /// <c>Assert.Ignore</c> on CI's Null Device). So when phase 6 landed nine more hulls with
        /// all three flotation fields at 0, nothing went red. These fields are GAME-SIDE — the baker
        /// never writes them, which is what lets them survive re-bakes — so every rig-comparison
        /// test in this file is blind to them BY CONSTRUCTION: a rig oracle cannot police a field
        /// with no rig counterpart. This is the enumeration that can.</para>
        ///
        /// <para>Both clamp fields, never just the deck: at half-beam 0 the per-point law degrades
        /// to root-line-only, which this project has already measured and rejected — the far rail
        /// boards first while the root reads dry (docs/design/water-rendering.md §24).</para>
        /// </summary>
        [Test]
        public void EveryCommittedHullMesh_CarriesTheWatertightClampAndAWaterline()
        {
            var unclamped = new List<string>();
            var afloat = new List<string>();

            foreach (var hull in HullMeshFleet.Hulls)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                Assert.IsNotNull(def, hull.Key);

                if (def.WatertightDeckHeightMeters <= 0f || def.WatertightHalfBeamMeters <= 0f)
                    unclamped.Add($"{hull.Key} (deck {def.WatertightDeckHeightMeters}, " +
                                  $"halfBeam {def.WatertightHalfBeamMeters})");

                if (def.RestingDraftMeters <= 0f)
                    afloat.Add(hull.Key);

                // The clamp scans x over half the rig cell, and a hull is authored to fit her own
                // cell at EVERY heading — so she cannot reach farther abeam than that.
                float cellHalfMeters = 0.5f * def.CellW / def.PxPerMetre;
                Assert.Less(def.WatertightHalfBeamMeters, cellHalfMeters,
                    $"{hull.Key}: half-beam {def.WatertightHalfBeamMeters} m exceeds half her own " +
                    $"cell ({cellHalfMeters:0.##} m) — she cannot be that wide.");
            }

            Assert.IsEmpty(unclamped,
                "These committed hulls ship with the WATERTIGHT CLAMP OFF, so the displaced sea " +
                "draws inside them — the owner's flooding defect shipping again. Set both fields " +
                "from the rig source (deck height = the rig's own DECK constant, or its lowest " +
                "open interior surface above the keel when it has none): " +
                string.Join(", ", unclamped));

            Assert.IsEmpty(afloat,
                "These committed hulls have NO RESTING DRAFT, so their keels sit on the surface " +
                "and a trough opens air under the whole boat (HullMeshDef.RestingDraftMeters). " +
                "A boat that floats needs a design waterline: " + string.Join(", ", afloat));
        }

        /// <summary>
        /// <b>Every committed hull carries the per-face INTERIOR MASK, and it agrees with the
        /// hand-measured deck line.</b>
        ///
        /// <para>This is two guards in one, and the second is the interesting one. The mask
        /// (UV0.w) is what lets the displaced sea be kept out of a boat's interior per pixel
        /// instead of by shoving her whole hull toward the camera. A hull baked before the mask
        /// existed carries all-zero flags — which renders correctly but silently loses the
        /// protection — so the fleet has to be enumerated.</para>
        ///
        /// <para><b>The cross-check.</b> The LOWEST interior face the classifier finds, from
        /// geometry alone, should be the same height as
        /// <see cref="HullMeshDef.WatertightDeckHeightMeters"/> — which was measured
        /// independently, by hand, off the rig sources' own <c>DECK</c> constants. Two derivations
        /// that share no code and no input agreeing to a centimetre is the strongest evidence
        /// either of them is right, and it is what this pins. It is also the golden-master guard
        /// the classifier needs: 5–30% of sea-band faces move if the sampling constants change, and
        /// a drift big enough to matter moves this height.</para>
        ///
        /// <para>Measured at the shipped sampling (32 azimuths; elevations 0…−90): nine hulls land
        /// EXACTLY (dory/punt/trawler/Mk2) or within 18 mm; the coastal packet is the one outlier
        /// at −0.27 m and the least stable hull under perturbation — she is called out here rather
        /// than hidden by a loose global tolerance.</para>
        /// </summary>
        [Test]
        public void EveryCommittedHullMesh_CarriesTheInteriorMaskAndAgreesWithItsDeckLine()
        {
            const float Tight = 0.02f;      // the nine that agree to the centimetre
            const float Loose = 0.30f;      // the packet's known, documented disagreement
            var missing = new List<string>();
            var drifted = new List<string>();
            int agreeTightly = 0;

            foreach (var hull in HullMeshFleet.Hulls)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                Assert.IsNotNull(def, hull.Key);
                Assert.IsNotNull(def.Mesh, $"{hull.Key}: no mesh sub-asset.");

                var attrs = new List<Vector4>();
                def.Mesh.GetUVs(RigMeshBuilder.AttrUvChannel, attrs);
                Vector3[] verts = def.Mesh.vertices;

                // Mesh vertices are in the RIG's own frame, so z IS height above the keel bottom.
                // Counted over the FULLY-interior faces only (side code 1): that set is identical
                // to what the side-blind classifier produced, so the deck-line agreement pinned
                // below carries across the per-side change without re-measurement. One-sided faces
                // (codes 2/3) are policed by their own test.
                int interior = 0;
                float lowest = float.MaxValue;
                for (int i = 0; i < attrs.Count && i < verts.Length; i++)
                {
                    if (attrs[i].w <= 0.5f || attrs[i].w >= 1.5f) continue;
                    interior++;
                    if (verts[i].z < lowest) lowest = verts[i].z;
                }

                if (interior == 0)
                {
                    missing.Add($"{hull.Key} (0 of {attrs.Count} vertices flagged)");
                    continue;
                }

                float delta = lowest - def.WatertightDeckHeightMeters;
                if (Mathf.Abs(delta) <= Tight) agreeTightly++;
                if (Mathf.Abs(delta) > Loose)
                    drifted.Add($"{hull.Key}: lowest interior {lowest:0.###} m vs deck line " +
                                $"{def.WatertightDeckHeightMeters:0.##} m (off by {delta:+0.###;-0.###})");
            }

            Assert.IsEmpty(missing,
                "These committed hulls carry NO interior mask, so the displaced sea has nothing " +
                "keeping it out of their interiors. Re-bake: Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ " +
                "Bake ALL fleet hull meshes. " + string.Join(", ", missing));

            Assert.IsEmpty(drifted,
                "The classifier's lowest interior face no longer agrees with the hand-measured " +
                "deck line. Either the sampling constants changed (they are a GOLDEN MASTER — see " +
                "RigMeshInteriorClassifier) or a rig moved. " + string.Join("; ", drifted));

            Assert.GreaterOrEqual(agreeTightly, 9,
                $"Only {agreeTightly} of {HullMeshFleet.Hulls.Count} hulls agree with their deck " +
                $"line to within {Tight} m; nine did when the mask shipped. A drop here means the " +
                "classifier drifted even though no hull broke the loose bound.");
        }

        /// <summary>
        /// <b>Every committed hull carries the PER-SIDE interior codes — the inner strake's only
        /// guard.</b>
        ///
        /// <para>The rail retired the over-the-lip family, and the walls' faces the sea genuinely
        /// cannot see went interior — but the inner strake survived both: the rigs model a separate
        /// inner skin 0.035–0.05 m inside the planking, and from in under the covering board a
        /// below-horizon ray really does reach its BACK, while the surface the player looks at is
        /// its FRONT. No sampling density fixes that (16 → 96 px/m moved the console and cape by
        /// exactly 0 faces), because the two sides of one face have different relationships to the
        /// sea. The cure is the side codes (UV0.w = 2/3), and this test is what notices a re-bake
        /// or a refactor silently flattening them back to 0/1 — the strake defect returning with
        /// every other check green, exactly how it shipped the first time.</para>
        ///
        /// <para>Two claims, both measured at the shipped sampling: every hull has one-sided faces
        /// at all (planking whose outboard side is sea and whose inboard side is the cockpit wall),
        /// and the two hulls the owner reported the strake on (console, cape) have faces that are
        /// dry specifically on their FRONT (code 2) — the visible face of an inner skin whose back
        /// the sea reaches.</para>
        /// </summary>
        [Test]
        public void EveryCommittedHullMesh_CarriesPerSideInteriorCodes()
        {
            var flat = new List<string>();

            foreach (var hull in HullMeshFleet.Hulls)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                Assert.IsNotNull(def, hull.Key);
                Assert.IsNotNull(def.Mesh, $"{hull.Key}: no mesh sub-asset.");

                var attrs = new List<Vector4>();
                def.Mesh.GetUVs(RigMeshBuilder.AttrUvChannel, attrs);

                int frontDry = 0, backDry = 0;
                foreach (Vector4 a in attrs)
                {
                    if (a.w > 1.5f && a.w < 2.5f) frontDry++;
                    else if (a.w > 2.5f) backDry++;
                }

                if (frontDry + backDry == 0) flat.Add(hull.Key);

                if (hull.Key == "consoleSkiff" || hull.Key == "capeIslander")
                    Assert.Greater(frontDry, 0,
                        $"{hull.Key} has no front-dry (code 2) faces, but she is one of the two " +
                        "hulls the owner reported the inner-strake leak on (2026-07-26: \"its " +
                        "still on the console walls, in the cape islander its at the wall " +
                        "intersection with the floor\"). Without code-2 faces the strake's visible " +
                        "side is wettable again and the defect is back on screen.");
            }

            Assert.IsEmpty(flat,
                "These committed hulls carry NO one-sided interior codes (every UV0.w is 0 or 1), " +
                "so their meshes predate the per-side mask — re-bake: Hidden Harbours ▸ Dev ▸ 3D " +
                "Hulls ▸ Bake ALL fleet hull meshes. " + string.Join(", ", flat));
        }

        /// <summary>
        /// <b>The interior classifier must sample STRICTLY below the horizon.</b>
        ///
        /// <para>The whole rule is "a face is exterior iff the SEA can see it", and the sea lives
        /// below the waterline. The ring is fenced on both sides, for two different reasons:</para>
        ///
        /// <para><b>Above 0</b> — at +1° an upward-facing deck becomes visible and leaks into the
        /// EXTERIOR set, measured at 18 (dory) / 18 (lobster) / 44 (packet) faces flipping, i.e.
        /// decks becoming floodable across the fleet.</para>
        ///
        /// <para><b>At exactly 0</b> — the ray is LEVEL, and a level ray clears a low gunwale and
        /// lands on the INNER planking of the far side. Sheer curve makes that routine rather than a
        /// graze, since freeboard is lowest amidships; one frontmost pixel then condemns the whole
        /// face. That shipped, and the owner found it by eye (2026-07-26): <i>"it doesnt seem to
        /// affect the lower floor deck though, now its just the interior walls of the boat"</i>. It
        /// passed every existing check because the cross-check measures the LOWEST interior face,
        /// which the sole satisfies on every hull — structurally blind to the walls. Every elevation
        /// below 0 looks UPWARD at the hull and can only reach her outside, so dropping this one
        /// ring is surgical: −8° is what actually decides the topsides.</para>
        ///
        /// <para>Sabotage: put <c>0f</c> back into <c>ElevationsDeg</c> and this test goes red.
        /// It is the walls' only guard — no pixel metric sees them.</para>
        /// </summary>
        [Test]
        public void InteriorClassifier_SamplesStrictlyBelowTheHorizon()
        {
            foreach (float elevation in RigMeshInteriorClassifier.ElevationsDeg)
                Assert.Less(elevation, 0f,
                    $"Sampling elevation {elevation}° is not below the horizon. Above 0 the sea sees " +
                    "decks and flags them EXTERIOR (fleet-wide flooding); at exactly 0 a level ray " +
                    "clears a low gunwale and flags the INTERIOR WALLS exterior.");

            Assert.Contains(-8f, RigMeshInteriorClassifier.ElevationsDeg,
                "The shallowest below-horizon ring must be sampled — it is the direction that " +
                "decides the topsides, and with it gone the planking stops being wettable.");
            Assert.Contains(-90f, RigMeshInteriorClassifier.ElevationsDeg,
                "Straight up from underneath must be sampled, or the bottom of the hull is never " +
                "seen and the whole underside classifies as interior.");
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
                        // ⚠️ INVERTED BY PHASE 7, and the distinction is the whole phase. Binding a
                        // sprite overlay sheet is only a defect if that overlay has no MESH to cross
                        // over to: the sheets stay wired on purpose (they are the owner's V-key A/B,
                        // exactly like the sprite compass this hull also keeps). What must never
                        // happen is a flipped hull whose fitting exists ONLY as cells — that is the
                        // dory-with-no-oars regression the block was invented to prevent.
                        Assert.IsFalse(visual.HasOarSheets() && !visual.HasOarMeshes(),
                            $"{visual.Id} is flipped to Mesh and binds oar SHEETS but no oar MESHES, so " +
                            "the mesh path drops her oars and she rows with nothing. Bake the fitting " +
                            "(Bake ALL hull fittings) or give her an OverlayBlockedReason.");
                        // The outboards crossed over the same way the oars did, so the same shape of
                        // claim applies to them: sheets are fine, sheets with no fitting are not.
                        Assert.IsFalse(visual.HasMotor() && !visual.HasMotorMesh(),
                            $"{visual.Id} is flipped to Mesh and binds motor SHEETS but no motor MESH, " +
                            "so the mesh path drops her engine and she runs on nothing. Bake the " +
                            "fitting (Bake ALL hull fittings) or give her an OverlayBlockedReason.");
                    }
                    else
                    {
                        Assert.AreEqual(BoatHullVariant.Sprite, visual.Variant,
                            $"{visual.Id} is blocked ({hull.OverlayBlockedReason}) and must stay a " +
                            "sprite hull until that overlay has a mesh of its own.");
                        Assert.IsTrue((visual.HasOarSheets() && !visual.HasOarMeshes()) ||
                                      (visual.HasMotor() && !visual.HasMotorMesh()),
                            $"{visual.Id} carries an OverlayBlockedReason but every overlay she binds " +
                            "already has a fitting mesh, so the block is stale — flip her.");
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
