using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Turns a rig into a committed <see cref="HullMeshDef"/> asset (ADR 0022 phase 4)</b> — the
    /// baked format phase 3 deliberately did not invent. Extraction (<see cref="RigMeshExtractor"/>)
    /// and mesh building (<see cref="RigMeshBuilder"/>) are unchanged; this adds the two per-artwork
    /// POSE facts the runtime needs and writes everything as one asset with the mesh as a sub-asset:
    ///
    /// <list type="number">
    ///   <item><b>The azimuth convention, MEASURED</b> (<see cref="RigAzimuthProbe"/> over the rig's
    ///   own quarter-turn render) — never read off a declaration, which has shipped mirrored boats
    ///   five times. This is the sign of the whole compass→dir mapping
    ///   (<see cref="HullMeshMath.HeadingToDirUnits"/>).</item>
    ///   <item><b>The rock amplitudes</b>, read off the rig's exported <c>ROCK</c> block (rollA /
    ///   pitchA / heaveA) — transcription, not tuning, exactly like the motor rock amplitudes in
    ///   BoatVisualLibraryBuilder.</item>
    /// </list>
    ///
    /// <para><b>Non-destructive re-bakes:</b> an existing asset is refreshed in place (same guid), so
    /// nothing pointing at the def breaks; only the mesh sub-asset is replaced. The output is
    /// committed — the owner re-runs this only when the art director's rig changes.</para>
    ///
    /// <para>⚠️ <c>docs/art/rigs/**</c> is read-only here as everywhere (art-director's lane); the
    /// extractor's tests assert the sources are byte-identical after a run.</para>
    /// </summary>
    public static class RigMeshAssetBaker
    {
        const string HullMeshFolder = "Assets/_Project/Data/Boats/HullMeshes";

        /// <summary>
        /// <b>The whole fleet, in one pass (ADR 0022 phase 6).</b> Every hull in
        /// <see cref="HullMeshFleet.Hulls"/>: extract, build, measure, write the def, then either
        /// WIRE the existing sheet-built visual or CREATE a mesh-only one.
        ///
        /// <para>Phases 4 and 5 each hand-wrote one of these. That was the right shape while the
        /// question was still "does this work at all"; phase 5 answered it (the dragger needed zero
        /// changes to the baker, the shader or the seam) and the owner ruled the whole fleet goes
        /// mesh. So the per-hull code became per-hull data and this became the entry point.</para>
        ///
        /// <para><b>One hull's failure does not abort the rest.</b> A bake is a long editor operation
        /// over eleven hulls; stopping at the first exception would mean the owner learns about one
        /// problem per run. Every hull is attempted, failures are collected, and the run ends with a
        /// single report — errors last, because that is what he needs to read.</para>
        /// </summary>
        [MenuItem(RigMeshGate.MenuRoot + "/Bake ALL fleet hull meshes", priority = 219)]
        public static void BakeFleet() => BakeFleetInternal(HullMeshFleet.Hulls);

        [MenuItem(RigMeshGate.MenuRoot + "/Bake ALL fleet hull meshes", validate = true)]
        static bool BakeFleetValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (-executeMethod) for the whole-fleet bake.</summary>
        public static void BakeFleetCli()
        {
            try
            {
                int failed = BakeFleetInternal(HullMeshFleet.Hulls);
                if (failed > 0)
                {
                    Debug.LogError($"[rig-mesh] CLI fleet bake FAILED: {failed} hull(s) did not bake.");
                    EditorApplication.Exit(1);
                    return;
                }
                Debug.Log("[rig-mesh] CLI fleet bake OK.");
                // Success must exit as loudly as failure: launched -quit-less (the -quit/RunTests
                // race), nothing else ever ends the editor, and the search indexer's idle CPU burn
                // reads as a bake still working — it cost a coordinator four phantom hours.
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[rig-mesh] CLI fleet bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Returns the number of hulls that failed. Reports every hull either way.</summary>
        static int BakeFleetInternal(IReadOnlyList<FleetHull> hulls)
        {
            var report = new StringBuilder($"[rig-mesh] fleet bake — {hulls.Count} hulls\n");
            var failures = new List<string>();

            for (int i = 0; i < hulls.Count; i++)
            {
                FleetHull hull = hulls[i];
                try
                {
                    EditorUtility.DisplayProgressBar(
                        "Baking fleet hull meshes", hull.Label, (i + 0.5f) / hulls.Count);

                    // ⚠️ hull.Extraction is NOT optional here, though it reads like it. BakeOne has
                    // passed it since ADR 0022 phase 8 and this loop did not, so the whole-fleet
                    // bake would have taken the static-F path for a GENERATOR hull: the extractor
                    // then demands `F`, the widening cannot supply one, and the run reports
                    // "✗ lobster …: FAILED" for all eighteen while the eleven pass. Loud rather than
                    // silent — but only because the extractor happens to fail closed, and a bake
                    // that means the wrong hull is exactly what this field exists to prevent.
                    HullMeshDef def = Bake(hull.ScriptPath, hull.GlobalName, hull.MeshAssetPath,
                                           hull.MeshId, hull.Extraction);
                    WireVisuals(hull, def);

                    long sheetBytes = (long)def.CellW * def.CellH * 4 * 32 * 4;  // 32 facings × 4 rock, RGBA32
                    long meshBytes = new FileInfo(hull.MeshAssetPath).Length;
                    report.Append(
                        $"  ✓ {hull.Label}\n" +
                        $"      {def.Mesh.vertexCount} verts / {def.Mesh.triangles.Length / 3} tris, " +
                        $"cell {def.CellW}×{def.CellH} @ {def.PxPerMetre} px/m, elev {def.ElevationDeg}°\n" +
                        $"      azimuth {(def.AzimuthCounterClockwise ? "CCW" : "CW")} (MEASURED), " +
                        $"rock roll {def.RockRollDegrees}° pitch {def.RockPitchDegrees}° " +
                        $"heave {def.RockHeavePixels}px\n" +
                        $"      asset {meshBytes / 1024.0:F1} KB vs a {sheetBytes / 1048576.0:F1} MiB " +
                        $"sheet set — {sheetBytes / (double)meshBytes:N0}× smaller\n");
                }
                catch (Exception e)
                {
                    failures.Add(hull.Key);
                    report.Append($"  ✗ {hull.Label}: FAILED — {e.Message}\n");
                }
            }

            EditorUtility.ClearProgressBar();
            report.Append(failures.Count == 0
                ? "\n  All hulls baked. The fleet is mesh."
                : $"\n  ⚠️ {failures.Count} FAILED: {string.Join(", ", failures)}");

            if (failures.Count == 0) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());
            return failures.Count;
        }

        /// <summary>The first mesh hull end-to-end: the lobster boat (she has both a mesh and a baked
        /// sheet to compare — ADR 0022's own phasing). Kept as its own item because she is the A/B
        /// hull and gets re-baked on her own more than any other; it is a catalog lookup now, so it
        /// cannot drift from what the fleet bake does to her.</summary>
        [MenuItem(RigMeshGate.MenuRoot + "/Bake Lobster Boat hull-mesh asset", priority = 220)]
        public static void BakeLobsterBoat() => BakeOne("lobsterBoat");

        [MenuItem(RigMeshGate.MenuRoot + "/Bake Lobster Boat hull-mesh asset", validate = true)]
        static bool BakeLobsterBoatValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (-executeMethod) for the same bake.</summary>
        public static void BakeLobsterBoatCli() => BakeOneCli("lobsterBoat");

        /// <summary>The intro flagship: the Cape Islander. Her own item for the same reason the
        /// lobster has one — she is the second converted hull (ADR 0041 PR 2) and gets re-baked on
        /// her own while her room is being settled, and a whole-fleet bake would rewrite twenty-nine
        /// defs' worth of non-deterministic YAML to move one.</summary>
        [MenuItem(RigMeshGate.MenuRoot + "/Bake Cape Islander hull-mesh asset", priority = 220)]
        public static void BakeCapeIslander() => BakeOne("capeIslander");

        [MenuItem(RigMeshGate.MenuRoot + "/Bake Cape Islander hull-mesh asset", validate = true)]
        static bool BakeCapeIslanderValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (-executeMethod) for the cape's bake.</summary>
        public static void BakeCapeIslanderCli() => BakeOneCli("capeIslander");

        /// <summary>
        /// <b>The hull that motivated the ADR (phase 5): the side dragger.</b> 25 m of riveted steel
        /// whose sheet set would have been <b>433.1 MiB</b> at 32 facings × 4 rock frames — against
        /// 143.9 KB of mesh, a ratio of ~3,082×. Mesh-only, so her bake CREATES her visual rather than
        /// wiring one; see <see cref="EnsureMeshOnlyVisual"/> for why that is a different job.
        /// </summary>
        [MenuItem(RigMeshGate.MenuRoot + "/Bake Side Dragger hull-mesh asset", priority = 221)]
        public static void BakeSideDragger() => BakeOne("sideDragger");

        [MenuItem(RigMeshGate.MenuRoot + "/Bake Side Dragger hull-mesh asset", validate = true)]
        static bool BakeSideDraggerValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (-executeMethod) for the dragger's bake.</summary>
        public static void BakeSideDraggerCli() => BakeOneCli("sideDragger");

        /// <summary>
        /// <b>The lobster generator's eighteen, and nothing else.</b>
        ///
        /// <para>Its own entry point rather than "just run the fleet bake", for a reason that is
        /// about the DIFF and not about the runtime. A fleet bake rewrites all twenty-nine defs, and
        /// Unity's serialisation is not byte-deterministic — local sub-asset fileIDs are regenerated
        /// and the YAML document order is reshuffled — so re-baking the eleven shipped hulls that
        /// this PR does not touch produces hundreds of lines of pure churn on assets nobody changed.
        /// Baking exactly the new hulls keeps the eleven's committed bytes untouched, which is what
        /// makes the change reviewable.</para>
        /// </summary>
        [MenuItem(RigMeshGate.MenuRoot + "/Bake the 18 lobster variant hull meshes", priority = 222)]
        public static void BakeLobsterVariants() =>
            BakeFleetInternal(HullMeshFleet.VariantHulls.ToList());

        [MenuItem(RigMeshGate.MenuRoot + "/Bake the 18 lobster variant hull meshes", validate = true)]
        static bool BakeLobsterVariantsValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (-executeMethod) for the eighteen.</summary>
        public static void BakeLobsterVariantsCli()
        {
            try
            {
                var hulls = HullMeshFleet.VariantHulls.ToList();
                if (hulls.Count == 0)
                    throw new InvalidOperationException(
                        "No variant hulls in the fleet — this bake would silently do nothing.");

                int failed = BakeFleetInternal(hulls);
                if (failed > 0)
                {
                    Debug.LogError($"[rig-mesh] CLI lobster-variant bake FAILED: {failed} of " +
                                   $"{hulls.Count} hull(s) did not bake.");
                    EditorApplication.Exit(1);
                    return;
                }
                Debug.Log($"[rig-mesh] CLI lobster-variant bake OK — {hulls.Count} hulls.");
                // EXIT ON SUCCESS — the same omission BakeFleetCli and BakeOneCli each carried once
                // (four phantom hours, then #690): launched -quit-less, nothing else ends the editor.
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[rig-mesh] CLI lobster-variant bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// <b>The five hulls the fleet pack's last three rigs make, and nothing else</b> — the
        /// zodiac's two builds, the reshaped sport skiff, and the two battlewagons.
        ///
        /// <para>Its own entry point for exactly the reason the eighteen have one, and the argument
        /// is now stronger: a fleet bake would rewrite all thirty-four defs, and Unity's
        /// serialisation is not byte-deterministic, so the twenty-nine this PR does not touch would
        /// come back with regenerated sub-asset fileIDs and reshuffled YAML for no change at all.
        /// Baking exactly the new hulls is what keeps the diff readable.</para>
        /// </summary>
        public static IReadOnlyList<FleetHull> FleetPackHulls
        {
            get
            {
                var keys = new List<string> { "sportSkiffMk2" };
                foreach (ZodiacBuild b in ZodiacFleet.All) keys.Add(b.Key);
                foreach (SportFisherHull h in SportFisherFleet.All) keys.Add(h.Key);

                var hulls = new List<FleetHull>(keys.Count);
                foreach (string k in keys) hulls.Add(HullMeshFleet.Get(k));
                return hulls;
            }
        }

        [MenuItem(RigMeshGate.MenuRoot + "/Bake the 5 fleet-pack hull meshes", priority = 223)]
        public static void BakeFleetPack() => BakeFleetInternal(FleetPackHulls);

        // The cutaway batch-1-only entry point (CutawayBatch1Hulls + BakeCutawayBatch1 +
        // BakeCutawayBatch1Cli) lived here from #666 until batch 2 landed. Its own docstring said
        // "when [batch 2] lands it goes through the existing whole-fleet and variant entry points,
        // and this one can retire" — batch 2 landed 2026-08-27 (#670) and it did.

        [MenuItem(RigMeshGate.MenuRoot + "/Bake the 5 fleet-pack hull meshes", validate = true)]
        static bool BakeFleetPackValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (-executeMethod) for the five.</summary>
        public static void BakeFleetPackCli()
        {
            try
            {
                var hulls = FleetPackHulls;
                if (hulls.Count != 5)
                    throw new InvalidOperationException(
                        $"Expected 5 fleet-pack hulls, found {hulls.Count}. This bake will not run " +
                        "on a list it does not recognise — a short list here is a silently partial " +
                        "bake, and the hull that went missing keeps whatever def it had.");

                int failed = BakeFleetInternal(hulls);
                if (failed > 0)
                {
                    Debug.LogError($"[rig-mesh] CLI fleet-pack bake FAILED: {failed} of " +
                                   $"{hulls.Count} hull(s) did not bake.");
                    EditorApplication.Exit(1);
                    return;
                }
                Debug.Log($"[rig-mesh] CLI fleet-pack bake OK — {hulls.Count} hulls.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[rig-mesh] CLI fleet-pack bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Bake one catalog hull by key, and wire whatever visuals it owns.</summary>
        public static HullMeshDef BakeOne(string key)
        {
            FleetHull hull = HullMeshFleet.Get(key);
            HullMeshDef def = Bake(hull.ScriptPath, hull.GlobalName, hull.MeshAssetPath, hull.MeshId,
                                   hull.Extraction);
            WireVisuals(hull, def);
            return def;
        }

        static void BakeOneCli(string key)
        {
            try
            {
                BakeOne(key);
                Debug.Log("[rig-mesh] CLI bake OK.");
                // ⚠️ EXIT ON SUCCESS, for the same reason BakeFleetCli spells out: these entries are
                // launched -quit-less (the -quit/RunTests race), so nothing else ever ends the
                // editor and the search indexer's idle CPU burn reads as a bake still working. The
                // fleet path learned that at the cost of four phantom coordinator hours; this one
                // was still missing it, so every single-hull CLI bake — lobster and dragger
                // included — hung forever ON SUCCESS and only failures terminated.
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[rig-mesh] CLI bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Point every <c>BoatVisualDef</c> this hull dresses at the freshly baked mesh. Which of the
        /// two paths runs is the catalog's <see cref="FleetHull.HasBakedSheet"/> — see that field for
        /// why the difference matters to the owner rather than only to the code.
        /// </summary>
        static void WireVisuals(in FleetHull hull, HullMeshDef def)
        {
            for (int v = 0; v < hull.VisualAssetPaths.Length; v++)
            {
                string path = hull.VisualAssetPaths[v];
                if (hull.HasBakedSheet) WireSheetedVisual(path, def, hull.OverlayBlockedReason);
                else EnsureMeshOnlyVisual(path, hull.VisualIds[v], def);
            }
        }

        /// <summary>
        /// Wire a visual that was BUILT FROM A SHEET: flip it to the mesh variant and point it at the
        /// def, leaving its sprite compass fully populated.
        ///
        /// <para><b>Leaving the compass is the point, not an oversight.</b> It is what keeps the
        /// owner's V-key A/B alive at the helm — the only check on the mesh path that works by eye
        /// rather than by test — and it is what lets the sprite-only overlays (oars, outboards) keep
        /// binding. Field-scoped for the same reason the mesh-only path is: a re-run of Build Boat
        /// Visual Defs does not know these two fields, so it cannot undo them, and nothing the owner
        /// changes in the Inspector is stomped by a re-bake.</para>
        /// </summary>
        static void WireSheetedVisual(string assetPath, HullMeshDef def, string overlayBlockedReason)
        {
            var visual = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatVisualDef>(assetPath);
            if (visual == null)
            {
                Debug.LogWarning($"[rig-mesh] {assetPath} not found — {def.Id} was baked but no visual " +
                                 "points at it. Run Build Boat Visual Defs first, then re-bake.");
                return;
            }

            // The mesh is wired either way. What the block decides is only whether she is PRESENTED as
            // one — and wiring without flipping is inert, because ShouldPresentMesh gates on the
            // variant alone. That leaves the eventual flip a one-field change.
            visual.HullMesh = def;
            if (overlayBlockedReason == null)
                visual.Variant = HiddenHarbours.Boats.BoatHullVariant.Mesh;

            EditorUtility.SetDirty(visual);
            AssetDatabase.SaveAssets();

            Debug.Log(overlayBlockedReason == null
                ? $"[rig-mesh] {visual.Id}: Variant → Mesh, HullMesh → {def.Id}. Her sprite compass " +
                  "stays wired — that is the A/B comparison (V at the helm)."
                : $"[rig-mesh] {visual.Id}: HullMesh → {def.Id}, but Variant STAYS Sprite — " +
                  $"{overlayBlockedReason}. The mesh is baked, proven and wired; flipping her is one " +
                  "field once that overlay has a mesh of its own.");
        }

        /// <summary>
        /// Create or refresh the <see cref="HiddenHarbours.Boats.BoatVisualDef"/> for a hull that has
        /// NO baked sheet — the mesh is the whole picture. Refreshed in place (same guid) so a
        /// <c>BoatHullDef</c> pointing at it never breaks, and <b>field-scoped</b>: it writes only the
        /// facts this bake actually knows, so the owner's <c>SortingOrder</c> (or anything else he
        /// touches in the Inspector) survives a re-bake.
        ///
        /// <para><c>Facings</c> is left EMPTY on purpose — that is what makes
        /// <see cref="HiddenHarbours.Boats.BoatVisualDef.HasFullCompass"/> false, which is the honest
        /// answer for a hull with no sheet. The consequences are all correct: the V key reports "this
        /// hull has only one look" instead of offering a sprite half of an A/B that does not exist,
        /// and sprite-only overlays (oars, outboard) refuse to bind rather than draw wrongly.</para>
        /// </summary>
        static void EnsureMeshOnlyVisual(string assetPath, string id, HullMeshDef mesh)
        {
            var visual = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatVisualDef>(assetPath);
            bool created = visual == null;
            if (created)
            {
                EnsureFolder(System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/'));
                visual = ScriptableObject.CreateInstance<HiddenHarbours.Boats.BoatVisualDef>();
                visual.Id = id;
                AssetDatabase.CreateAsset(visual, assetPath);
            }

            visual.HullMesh = mesh;
            visual.Variant = HiddenHarbours.Boats.BoatHullVariant.Mesh;
            // The bake's own art facts, so the anchors and the wake foreshorten against the same
            // camera the mesh is projected through. Zero heading is 0 for every boat rig (element 0
            // is the North-facing view) — stated as data rather than assumed at the call site.
            visual.ArtBakeElevationDegrees = mesh.ElevationDeg;
            visual.ZeroHeadingDegrees = 0f;

            EditorUtility.SetDirty(visual);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);
            Debug.Log($"[rig-mesh] {(created ? "Created" : "Refreshed")} {assetPath}: {visual.Id} is " +
                      $"MESH-ONLY (no sheet, no compass) → {mesh.Id}, elevation {mesh.ElevationDeg}°.");
        }

        /// <summary>
        /// Extract + build + measure + write one rig's hull-mesh asset. Returns the (created or
        /// refreshed) def.
        /// </summary>
        /// <summary>Report what the interior classifier decided, in the only terms a human can
        /// review: how many faces it called fully interior (plus the one-sided split), and how far
        /// above the keel the LOWEST fully-interior face sits. That second number is the
        /// cross-check — compare it against this hull's hand-measured
        /// <c>WatertightDeckHeightMeters</c>. It is measured over the FULLY-interior set (code 1)
        /// on purpose: that set is identical to what the side-blind classifier produced, so the
        /// pinned deck-line agreement carries across the per-side change untouched.</summary>
        private static void LogInteriorMask(string globalName, RigMeshData data, byte[] sides)
        {
            int full = 0, frontOnly = 0, backOnly = 0, total = 0;
            float lowest = float.MaxValue;
            foreach (var f in data.Faces)
            {
                byte code = total < sides.Length ? sides[total] : (byte)0;
                if (code == RigMeshInteriorClassifier.SideInterior)
                {
                    full++;
                    for (int k = 0; k < f.V.Length; k++)
                        lowest = Mathf.Min(lowest, (float)f.V[k].Z);
                }
                else if (code == RigMeshInteriorClassifier.SideFrontInterior) frontOnly++;
                else if (code == RigMeshInteriorClassifier.SideBackInterior) backOnly++;
                total++;
            }
            string low = full > 0 ? $"{lowest:0.###} m above the keel" : "n/a";
            float rail = RigMeshInteriorClassifier.DeriveRailHeight(data);
            string railText = rail < float.MaxValue ? $"{rail:0.###} m" : "none (degenerate)";
            Debug.Log($"[rig-mesh] {globalName} interior mask: {full}/{total} faces fully interior " +
                      $"({(total > 0 ? 100f * full / total : 0f):0.#}%), one-sided {frontOnly} " +
                      $"front-dry + {backOnly} back-dry, lowest fully-interior = {low}, " +
                      $"rail = {railText}. Two independent checks on this line: the lowest " +
                      "fully-interior should land on this hull's WatertightDeckHeightMeters, and " +
                      "the rail must sit ABOVE it — a rail at or below the deck line would leave " +
                      "the deck itself wettable.");
        }

        /// <summary>
        /// <b>Which way this rig's <c>dir</c> argument actually turns the artwork</b> — measured two
        /// independent ways, with the rig's own geometry as the authority when it offers one.
        ///
        /// <para><b>Why two.</b> <see cref="RigAzimuthProbe"/> finds the bow by TAPER: it bins the
        /// silhouette along its principal axis and calls the narrower end the bow. That works
        /// because a boat is pointed forward and blunt aft — and it stops working when the end bands
        /// are dominated by superstructure rather than by hull, because the "beam" it measures is a
        /// screen-space extent, and a tall raked stem projects wide even where the planking is
        /// narrow.</para>
        ///
        /// <para><b>MEASURED, on this fleet, 2026-08-15.</b> Across the eleven shipped hulls the
        /// taper signal (blunt/sharp beam ratio) runs 1.04–1.60 and the verdict is CCW everywhere;
        /// for the seven rigs that also publish a stern-and-masthead pair, the analytic bearing
        /// agrees with margins of 250–1563 px — no ambiguity anywhere. The lobster VARIANT generator
        /// is the one rig where the two disagree, and it disagrees on the weakest taper signal in
        /// the fleet: 1.088 on <c>standard/hardtop/northumberland</c>, down to <b>1.040</b> on
        /// <c>offshore/hardtop/newfoundland</c>. Her hardtop cantilevers FORWARD over the stem while
        /// the shipped lobster boat's roof extends AFT on two posts, which loads the two hulls'
        /// taper bands at opposite ends — same boat family, opposite answer from the same heuristic.
        /// The analytic oracle puts her bow 254 px WEST at a quarter turn, exactly where the shipped
        /// hull's is (249 px), and her un-squashed port→star ground bearing is byte-identical to the
        /// hero's at all eight headings. She is not mirrored; the taper test is fooled.</para>
        ///
        /// <para><b>So the analytic answer wins where it exists</b>, and the disagreement is logged
        /// as an ERROR rather than resolved quietly — a wrong convention mirrors the whole
        /// heading→dir mapping, which is the defect this project has shipped five times and the
        /// reason the probe exists at all. Rigs with no such pair (dory, punt, console, sport skiff
        /// — none publish <c>navMounts</c>) keep the pixel path they were baked from, unchanged.</para>
        ///
        /// <para><b>Nothing already committed moves.</b> Of the eleven shipped hulls, seven publish
        /// an abeam pair and on every one of them the analytic answer CONFIRMS the pixel answer
        /// (bearing exactly −90.00°, CCW); the other four never reach the new arm. So this can only
        /// change a hull whose two oracles disagree, and today that is exactly the eighteen.</para>
        /// </summary>
        static AzimuthConvention MeasureAzimuth(IRigScriptHost host, string globalName, RigMeshData data,
                                                RigHullExtraction extraction)
        {
            // ⚠️ THE VARIANT DESCRIPTOR BELONGS IN THIS RENDER. Without it a generator rig hands back
            // its DEFAULT hull — measured: all eighteen lobster cells probed an identical 45,211
            // opaque pixels, i.e. seventeen hulls' conventions were being decided by an eighteenth's
            // picture. It happened not to change the answer here; that it could is the point.
            string opts = extraction != null && extraction.IsVariant && extraction.ViewOptions != null
                ? extraction.ViewOptions
                : "{}";
            string view = $"Object.assign({{}},{opts},{{elev:{data.DefaultElev.ToString("R", Inv)}}})";

            // A registry rig draws from the HULL, not from the global — SportFisherIso2.render is
            // undefined and only SportFisherIso2.byId('skybridge').render exists. Every other rig's
            // scope IS the global, so this line changes nothing for them.
            string scope = extraction != null ? extraction.ScopeOr(globalName) : globalName;

            byte[] rgba = host.EvaluateBytes($"{scope}.render(2,{view})");
            RigAzimuthProbe.Result pixel = RigAzimuthProbe.MeasureFromQuarterTurn(rgba, data.W, data.H);
            Debug.Log($"[rig-mesh] {globalName} azimuth probe (pixels):\n{pixel.Report}");

            double taper = Math.Max(pixel.BowBeam, pixel.SternBeam) /
                           Math.Max(1e-6, Math.Min(pixel.BowBeam, pixel.SternBeam));

            AzimuthConvention analytic;
            string margin;

            if (HasAbeamPair(host, scope, view))
            {
                double bearing = GroundBearingOfAbeamPair(host, scope, view, data.DefaultElev);
                analytic = bearing > 0 ? AzimuthConvention.Clockwise
                                       : AzimuthConvention.CounterClockwise;
                margin = $"port→star ground bearing {bearing:F2}° at a quarter turn; pixel taper " +
                         $"ratio {taper:F3}";
            }
            else if (HasCentrelineForeAftPair(host, scope, view))
            {
                double dx = BowScreenXOfForeAftPair(host, scope, view);
                analytic = dx > 0 ? AzimuthConvention.Clockwise
                                  : AzimuthConvention.CounterClockwise;
                margin = $"stern-eye→painter screen dx {dx:F1} px at a quarter turn; pixel taper " +
                         $"ratio {taper:F3}";
            }
            else
            {
                Debug.Log($"[rig-mesh] {globalName}: no admissible analytic pair — neither abeam nav " +
                          "mounts nor a centreline fore-and-aft pair — so the pixel taper stands " +
                          "alone. That is the path every hull baked before 2026-08-15 took.");
                return pixel.Convention;
            }

            if (analytic == pixel.Convention)
            {
                Debug.Log($"[rig-mesh] {globalName} azimuth CONFIRMED {analytic} by the rig's own " +
                          $"anchors ({margin}).");
                return analytic;
            }

            Debug.LogError(
                $"[rig-mesh] {globalName}: THE TWO AZIMUTH ORACLES DISAGREE — pixels say " +
                $"{pixel.Convention}, the rig's own anchors say {analytic} ({margin}).\n" +
                "Taking the ANALYTIC answer, and the reason is that the two are not equally strong. " +
                "The abeam bearing is a pure ±x separation rotated by the rig's own camera and read " +
                "on the UN-SQUASHED ground plane, so its answer is a SIGN and it lands on ±90.00° " +
                "exactly; the pixel test infers the bow from which end of the silhouette is " +
                "narrower, which superstructure over the stem can invert. A taper ratio near 1 " +
                "above is the tell that it did.\n" +
                "⚠️ If this ever fires with a LARGE taper ratio, do not accept either answer on its " +
                "own — render the rig beside a registered reference in one host and compare their " +
                "bearings directly before baking anything.");
            return analytic;
        }

        /// <summary>
        /// True when this rig publishes port and starboard nav mounts that are a genuine ABEAM pair
        /// — the same y and z, opposite x — which is what makes their screen bearing a hull bearing.
        ///
        /// <para><b>Admissibility is CHECKED, not assumed, and the check has already earned its
        /// keep.</b> <see cref="RigAzimuthProbe"/>'s own note records an investigation misled by
        /// treating the console skiff's off-centre tub anchors as a centreline pair. Measured
        /// 2026-08-15 on this very fleet: the lobster generator's three <c>open/newfoundland</c>
        /// cells carry their masthead on the dry stack, <b>0.485–0.700 m to port</b> — so a
        /// stern-to-masthead formulation of this same cross-check was inadmissible on exactly those
        /// three and silently fell back to the heuristic it was meant to correct. The abeam pair has
        /// no such gap: every rig that publishes <c>navMounts</c> at all publishes it.</para>
        ///
        /// <para>The test is done in SCREEN space at heading 0, where the rig's own +x is screen +x
        /// and nothing has mixed the axes yet: an abeam pair at equal height projects to a
        /// horizontal segment, so a non-zero screen dy means the two mounts are not abeam.</para>
        /// </summary>
        /// <param name="scope">The object that publishes <c>navMounts</c> — the global for every
        /// one-hull rig, the per-hull object for a registry rig (see
        /// <see cref="RigHullExtraction.HullScope"/>).</param>
        static bool HasAbeamPair(IRigScriptHost host, string scope, string view)
        {
            if (!host.EvaluateBool($"typeof {scope}.navMounts === 'function'")) return false;
            if (!host.EvaluateBool(
                    $"(function(m){{return !!m && !!m.port && !!m.star;}})" +
                    $"({scope}.navMounts(0,{view}))"))
                return false;

            return host.EvaluateBool(
                $"(function(m){{return Math.abs(m.star.y-m.port.y) < 1e-6 && " +
                $"Math.abs(m.star.x-m.port.x) > 1e-6;}})({scope}.navMounts(0,{view}))");
        }

        /// <summary>
        /// The port→starboard bearing at a quarter turn, on the GROUND PLANE.
        ///
        /// <para>⚠️ <b>The un-squash is the whole method.</b> The ¾ projection scales screen depth by
        /// <c>sin(elevation)</c>, so any angle taken straight off screen coordinates is a squashed
        /// angle and is wrong by up to 12°. Dividing the screen dy by <c>sin(elev)</c> before the
        /// <c>atan2</c> recovers the real bearing — and it is self-checking: only the correct
        /// divisor lands the eight headings on exact 45° steps.</para>
        ///
        /// <para>Screen y is DOWN, so a hull turned a quarter CLOCKWISE (bow east) has her starboard
        /// side toward the viewer, giving a positive bearing; counter-clockwise gives −90°.
        /// Measured across the whole fleet 2026-08-15: every rig that publishes an abeam pair — the
        /// seven shipped hulls that do, plus all eighteen lobster variants — returns exactly
        /// <b>−90.00°</b>.</para>
        /// </summary>
        static double GroundBearingOfAbeamPair(IRigScriptHost host, string scope, string view,
                                               double elevationDeg)
        {
            string sin = Math.Sin(elevationDeg * Math.PI / 180.0).ToString("R", Inv);
            return host.EvaluateNumber(
                $"(function(m){{return Math.atan2((m.star.y-m.port.y)/{sin}," +
                $"m.star.x-m.port.x)*180/Math.PI;}})({scope}.navMounts(2,{view}))");
        }

        /// <summary>
        /// True when this rig publishes a bow eye and a stern eye that are a genuine CENTRELINE
        /// FORE-AND-AFT pair — the same x, different y — which is what makes the segment between
        /// them the hull's own fore-and-aft axis.
        ///
        /// <para><b>Why a second analytic arm at all.</b> The abeam pair is the stronger oracle and
        /// stays first, but only seven of the eleven shipped hulls publish <c>navMounts</c> and NONE
        /// of the three families this bake adds does except the sport fisher. Without this, the
        /// zodiac's two builds and the sport skiff Mk2 would rest on the pixel taper alone — the
        /// heuristic that had all eighteen lobster variants mirrored on a taper ratio of 1.040. A
        /// rig that says where its bow eye is has already answered the question; the answer just
        /// was not being asked for.</para>
        ///
        /// <para><b>Admissibility is CHECKED, and on this fleet it does real work.</b> Measured
        /// 2026-08-15 across every hull rig in the repo: the dory (9.60 px) and the punt (13.44 px)
        /// publish <c>painter</c>/<c>sternEye</c> whose screen x's DIFFER at heading 0 — their bow
        /// eyes are not on the centreline — and both are correctly rejected here and keep the pure
        /// pixel path they were baked from. This is the same trap <see cref="RigAzimuthProbe"/>'s own
        /// note records for the console skiff's off-centre tubs (−30.72 px), which one investigation
        /// has already been misled by.</para>
        ///
        /// <para><b>Nothing already committed moves.</b> Of the eleven shipped hulls exactly one —
        /// the console skiff — publishes an ADMISSIBLE pair and is not already covered by the abeam
        /// arm, and on her this says CounterClockwise, which is what
        /// <c>ConsoleIsoHullMesh.asset</c> already carries. The other ten either have no such pair or
        /// are rejected by the check above.</para>
        ///
        /// <para>Tested in SCREEN space at heading 0 for the same reason the abeam check is: the
        /// rig's own +x is screen +x there and nothing has mixed the axes yet, so a centreline pair
        /// projects to a VERTICAL segment — zero screen dx — and a non-zero dx means one of the two
        /// is off the centreline.</para>
        /// </summary>
        static bool HasCentrelineForeAftPair(IRigScriptHost host, string scope, string view)
        {
            if (!host.EvaluateBool($"typeof {scope}.painter === 'function' && " +
                                   $"typeof {scope}.sternEye === 'function'"))
                return false;

            return host.EvaluateBool(
                $"(function(p,s){{return !!p && !!s && Math.abs(p.x-s.x) < 1e-6 && " +
                $"Math.abs(p.y-s.y) > 1e-6;}})({scope}.painter(0,{view}),{scope}.sternEye(0,{view}))");
        }

        /// <summary>
        /// The bow's SIGNED SCREEN X at a quarter turn, taken from the rig's own two eyes rather than
        /// from the silhouette: <c>painter.x − sternEye.x</c>, i.e. stern-to-bow.
        ///
        /// <para><b>No un-squash, and that is not an omission.</b> The abeam arm has to un-squash
        /// because it reads an ANGLE, and the ¾ projection scales screen depth by
        /// <c>sin(elevation)</c>. This arm reads a SIGN on the horizontal axis only — the axis the
        /// projection leaves alone — and <see cref="RigAzimuthProbe"/>'s own step 4 is the same
        /// reading: "a quarter turn clockwise from north points the bow EAST (screen +x)". Dividing
        /// the y term by anything positive cannot change the sign of the x term, so the un-squash
        /// would be arithmetic with no effect on the answer.</para>
        ///
        /// <para>Measured 2026-08-15, and the margins are not marginal: zodiac hurricane −220.0 px,
        /// zodiac frc −201.1 px, sport skiff Mk2 −213.2 px, console skiff −219.1 px. All bow-WEST,
        /// all CounterClockwise, on cells 244–272 px wide.</para>
        /// </summary>
        static double BowScreenXOfForeAftPair(IRigScriptHost host, string scope, string view) =>
            host.EvaluateNumber(
                $"(function(p,s){{return p.x-s.x;}})" +
                $"({scope}.painter(2,{view}),{scope}.sternEye(2,{view}))");

        static readonly System.Globalization.CultureInfo Inv =
            System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>
        /// The cutaway table a bake writes, as a pure function of what the rig published.
        ///
        /// <para>Factored out of <c>BakeOne</c> on 2026-08-28 so the gate fixture can ask
        /// <c>CutawayForDeck</c> the question through the SAME mapping the bake uses, rather than
        /// transcribing these eight assignments into a test — where a field dropped here would go
        /// green there. It is the only caller-visible seam between "what the rig said" and "what
        /// the def carries", and it stays a transcription: no field is derived, defaulted or
        /// tuned.</para>
        ///
        /// <para>A rig that published no <c>geometry()</c> yields an empty array — the honest
        /// "this hull cannot be cut". Re-baking such a hull writes the same empty array she already
        /// had, so nothing moves.</para>
        /// </summary>
        public static HullMeshDef.LevelTag[] LevelTableFor(RigMeshData data)
        {
            if (data == null || data.Levels == null) return System.Array.Empty<HullMeshDef.LevelTag>();

            var table = new HullMeshDef.LevelTag[data.Levels.Count];
            for (int i = 0; i < data.Levels.Count; i++)
            {
                RigLevelRecord lvl = data.Levels[i];
                table[i] = new HullMeshDef.LevelTag
                {
                    LevelId = lvl.Id,
                    DeckId = lvl.DeckId,
                    Tag = lvl.Tag,
                    LidLevelId = lvl.LidLevelId,
                    LidTag = lvl.LidTag,
                    Enclosed = lvl.Enclosed,
                    SoleZMeters = (float)lvl.SoleZ,
                    CeilingZMeters = (float)lvl.CeilingZ,
                };
            }
            return table;
        }

        /// <param name="extraction">Non-null to bake ONE VARIANT of a rig that generates several
        /// hulls. Null — every hull baked before 2026-08-13 — takes the rig's static <c>F</c>.</param>
        const string InteriorKitFolder = "docs/art/rigs/boat-interiors-kit";
        const string InteriorRigFileName = "boatInteriorRig.js";

        /// <summary>
        /// <b>The hulls whose room is baked as MESH.</b> Opt-in, one global name per converted hull,
        /// because the sprite-sheet interior system keeps working for every hull NOT on this list and
        /// the two must never both draw for the same boat.
        ///
        /// <para>This is the fleet rollout's only switch: a batch adds its hulls here, re-bakes, and
        /// retires those hulls' sheets once the pictures agree. A hull added here whose sheets are
        /// still wired would draw her cabin twice.</para>
        /// </summary>
        public static readonly string[] MeshInteriorHulls =
        {
            "LobsterBoatIso",
            "CapeIslanderIso",
            // The eighteen lobster VARIANTS share one rig global (LobsterVariantFleet.GlobalName) and
            // convert as a family; each hull's own room comes from the variant triple her
            // RigHullExtraction carries, handed to the interior rig nested under `variant`.
            LobsterVariantFleet.GlobalName,
        };

        /// <summary>Is this hull converted — is her rig family on the switch? One predicate for
        /// the bake, the fleet adjudicator and the fixtures.</summary>
        public static bool IsMeshInteriorHull(string globalName)
            => MeshInteriorHulls.Contains(globalName, StringComparer.Ordinal);

        /// <summary>
        /// Which key in the interior rig's own <c>HULLS</c> table is this hull — derived by asking
        /// the rig, never transcribed. A table here would be a second place for the two rigs to
        /// disagree about what boats exist.
        /// </summary>
        static string InteriorHullKeyFor(IRigScriptHost host, string globalName, string interiorRigPath)
        {
            host.Execute(BoatInteriorGeometryExtractor.WidenInteriorRig(File.ReadAllText(interiorRigPath)));
            string matches = host.EvaluateString(
                "(function(){var H=globalThis.BoatInterior.HULLS,o=[];" +
                $"for(var k in H) if(H[k].sym==='{globalName}') o.push(k);" +
                "return o.join(' ');})()");
            string[] keys = matches.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (keys.Length == 1) return keys[0];
            throw new InvalidOperationException(
                keys.Length == 0
                    ? $"'{globalName}' is listed in MeshInteriorHulls but the interior rig's HULLS " +
                      "table names no hull with that sym, so there is no room to bake. Either the " +
                      "list is wrong or the rig has not met this boat."
                    : $"'{globalName}' matches {keys.Length} interior hulls ({matches}) — a multi-hull " +
                      "rig needs its `pick` disambiguated before her room can be baked, or the wrong " +
                      "boat's cabin lands in her hull.");
        }

        /// <summary>
        /// Append this hull's ROOM to <paramref name="data"/> if she is a converted hull, and return
        /// the bake-log report; null when she is not on <see cref="MeshInteriorHulls"/>.
        ///
        /// <para><b>Public, and shared with the fleet adjudicator on purpose.</b>
        /// <c>HullMeshFleetTests.EveryCommittedHullMesh_MatchesAFreshExtractionFromItsRig</c> re-derives
        /// every hull from her rig and compares against what is committed — so if the bake appended a
        /// room and a fresh extraction did not, that test would report every converted hull as stale
        /// forever, and the obvious "fix" is to teach the test a second copy of this logic. Two copies
        /// of the rule is precisely the drift that test exists to catch. One method, two callers.</para>
        /// </summary>
        public static string AppendMeshInteriorIfConverted(IRigScriptHost host, string globalName,
                                                          RigMeshData data,
                                                          RigHullExtraction extraction = null)
        {
            if (!IsMeshInteriorHull(globalName)) return null;

            string repo = Directory.GetParent(Application.dataPath).FullName;
            string interiorRig = Path.Combine(repo, InteriorKitFolder, InteriorRigFileName);
            string interiorKey = InteriorHullKeyFor(host, globalName, interiorRig);

            // A generator hull's variant triple rides on her extraction (ViewOptions is the same
            // {size,style,region} literal the interior rig wants under `variant`). Without it every
            // variant would bake the standard/hardtop/northumberland room — a correct-looking cabin
            // that is not hers, and nothing downstream would notice.
            string variantLiteral = extraction != null ? extraction.ViewOptions : null;

            var room = BoatInteriorGeometryExtractor.Extract(host, interiorKey, data, interiorRig,
                                                             variantLiteral);
            if (room.Materials.Count > HullMeshDef.InteriorRampSlots)
                throw new InvalidOperationException(
                    $"{globalName}'s room paints {room.Materials.Count} ramps and the facet shader's " +
                    $"_RampMetaInterior holds {HullMeshDef.InteriorRampSlots}. Do NOT spend the hull's " +
                    "own 16 to fix this — that cap is a fleet law. Merge ramps in the extraction, or " +
                    "take a widening upstream with a measured cost.");

            data.Faces.AddRange(room.Faces);
            data.InteriorMaterials = room.Materials;
            return room.Report;
        }

        public static HullMeshDef Bake(string scriptPath, string globalName, string assetPath, string id,
                                       RigHullExtraction extraction = null)
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = RigMeshExtractor.ExtractFrom(host, scriptPath, globalName,
                                                           hull: extraction);

            // The per-face INTERIOR MASK (ADR 0023). HULLS ONLY — never fittings, whose legs and
            // propellers must stay wettable (see RigMeshBuilder.Build's parameter doc). The log
            // line below IS the evidence trail: the committed mesh is an opaque binary blob, so the
            // per-hull interior count and the lowest interior height are the only reviewable
            // artefact a bake produces. The lowest interior height in particular should land on the
            // hand-measured HullMeshDef.WatertightDeckHeightMeters — it does for 9 of the 11 hulls,
            // which is the independent cross-check that says the classifier is right.
            byte[] interiorSides = RigMeshInteriorClassifier.ClassifySides(data);

            // ---- THE ROOM, AS GEOMETRY (ADR 0038, full mesh interiors) --------------------------
            //
            // Appended AFTER the ADR 0023 water mask is classified, and the side-code array is then
            // extended with zeroes rather than re-classified. Two reasons, and both are about not
            // changing something this PR has no business changing: the classifier's per-hull counts
            // and lowest-interior heights are a committed evidence trail, and a cabin sole is not a
            // surface the SEA should start reasoning about. Zero is "exterior both sides", which is
            // what every face carried before the mask existed.
            string roomReport = AppendMeshInteriorIfConverted(host, globalName, data, extraction);
            if (roomReport != null)
            {
                Array.Resize(ref interiorSides, data.Faces.Count);   // rooms are 0 = exterior both sides
                Debug.Log(roomReport.TrimEnd());
            }

            RigMeshBuild build = RigMeshBuilder.Build(data, $"{globalName}HullMesh", interiorSides);
            LogInteriorMask(globalName, data, interiorSides);

            // --- the measured azimuth convention (quarter turn: broadside, least ambiguous) --------
            AzimuthConvention convention = MeasureAzimuth(host, globalName, data, extraction);

            // --- the rock amplitudes, off the exported ROCK block (optional; 0 = no rock) ----------
            // Read from the HULL's scope for a registry rig: a 16.2 m boat and a 27.4 m boat do not
            // share a sea state, and SportFisherIso2.ROCK does not exist. Identical to the global for
            // every other rig, so no committed amplitude moves.
            string rockScope = extraction != null ? extraction.ScopeOr(globalName) : globalName;
            float rollA = 0f, pitchA = 0f, heaveA = 0f;
            bool hasRock = host.EvaluateBool(
                $"typeof {rockScope}.ROCK === 'object' && {rockScope}.ROCK !== null");
            if (hasRock)
            {
                rollA = (float)host.EvaluateNumber($"{rockScope}.ROCK.rollA || 0");
                pitchA = (float)host.EvaluateNumber($"{rockScope}.ROCK.pitchA || 0");
                heaveA = (float)host.EvaluateNumber($"{rockScope}.ROCK.heaveA || 0");
            }
            else
            {
                Debug.LogWarning($"[rig-mesh] {globalName} exports no ROCK block — the mesh hull will " +
                                 "not rock. If the rig has one, ask the art director to export it.");
            }

            // --- write the asset (create or refresh in place — the guid must survive) --------------
            EnsureFolder(HullMeshFolder);
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(assetPath);
            bool created = def == null;

            // ⚠️ A FILE THAT EXISTS BUT DOES NOT LOAD IS NOT A NEW ASSET — IT IS A BROKEN ONE.
            //
            // Treating it as new is silent data loss: the create path runs field initialisers, so
            // every field this baker does NOT write is quietly reset — today that is
            // RestingDraftMeters, which the hull-waterline work tunes per hull and no re-bake has any
            // business touching. MEASURED 2026-07-23, and it is not hypothetical: a run whose
            // Library/ carried a stale script→guid map wrote `m_Script: {fileID: 0}` into every def,
            // after which LoadAssetAtPath<HullMeshDef> returned null for assets that were sitting
            // right there, and the NEXT run "created" them over the top and reset the drafts to 0.
            // Failing loudly turns a silent stomp into a stop.
            if (created && File.Exists(assetPath))
                throw new InvalidOperationException(
                    $"{assetPath} exists on disk but did not load as a HullMeshDef, so this bake was " +
                    "about to recreate it and silently reset every field the baker does not write " +
                    "(RestingDraftMeters among them).\n" +
                    "Usual cause: a stale or borrowed Library/ — the script→guid map is wrong, the " +
                    "asset serialises with `m_Script: {fileID: 0}`, and it stops resolving to its " +
                    "type. Delete Library/ and let the project reimport, then bake again. If the " +
                    "asset really is meant to be replaced, delete it (and its .meta) deliberately.");

            if (created) def = ScriptableObject.CreateInstance<HullMeshDef>();

            def.Id = id;
            def.SourceRigPath = scriptPath;
            // Empty for a static-F hull, so the eleven defs baked before generators existed keep the
            // exact bytes they have. Non-empty is the ONLY thing that distinguishes eighteen lobster
            // boats sharing one rig path.
            def.SourceFaceBuilder = data.SourceFaceExpression;
            def.LightN = data.LightN.ToVector3();
            def.Gain = (float)data.Gain;
            def.Bias = (float)data.Bias;
            def.Keyline = data.Keyline;
            def.PivotPx = new Vector2((float)data.PivotX, (float)data.PivotY);
            def.PxPerMetre = data.PxPerMetre;
            def.CellW = data.W;
            def.CellH = data.H;
            def.ElevationDeg = (float)data.DefaultElev;
            def.AzimuthCounterClockwise = convention == AzimuthConvention.CounterClockwise;
            def.RockRollDegrees = rollA;
            def.RockPitchDegrees = pitchA;
            def.RockHeavePixels = heaveA;

            def.Ramps = new HullMeshDef.Ramp[data.Materials.Count];
            for (int m = 0; m < data.Materials.Count; m++)
                def.Ramps[m] = new HullMeshDef.Ramp
                {
                    Colors = data.Materials[m].Ramp,
                    Offset = data.Materials[m].Off,
                };

            // The room's own table. Empty on every hull not yet converted, which is what keeps the
            // sheet system working for them: absence of an interior palette is how a hull says she
            // still draws her cabin as a sprite.
            def.InteriorRamps = new HullMeshDef.Ramp[data.InteriorMaterials.Count];
            for (int m = 0; m < data.InteriorMaterials.Count; m++)
                def.InteriorRamps[m] = new HullMeshDef.Ramp
                {
                    Colors = data.InteriorMaterials[m].Ramp,
                    Offset = data.InteriorMaterials[m].Off,
                };

            def.Bayer16 = new float[16];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    def.Bayer16[x * 4 + y] = (float)data.Bayer[x, y];

            // THE CUTAWAY TABLE (owner ruling 2026-08-26). Transcription, not tuning: every field
            // comes off the rig's own geometry(), including the DeckId that joins her levels to the
            // interior def's. A rig that publishes none leaves an empty array, which is the honest
            // "this hull cannot be cut" — and re-baking such a hull writes the same empty array she
            // already had, so nothing moves.
            def.LevelTags = LevelTableFor(data);

            // The mesh sub-asset: replace, never accumulate. DestroyImmediate on the old one removes
            // it from the asset file; the new one is added under the same def.
            Mesh oldMesh = def.Mesh;
            def.Mesh = build.Mesh;
            if (created)
            {
                AssetDatabase.CreateAsset(def, assetPath);
            }
            else if (oldMesh != null)
            {
                AssetDatabase.RemoveObjectFromAsset(oldMesh);
                UnityEngine.Object.DestroyImmediate(oldMesh, allowDestroyingAssets: true);
            }
            AssetDatabase.AddObjectToAsset(build.Mesh, def);
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);

            Debug.Log($"[rig-mesh] {(created ? "Created" : "Refreshed")} {assetPath}: {build} — " +
                      $"azimuth {(def.AzimuthCounterClockwise ? "CCW (mapping negates)" : "CW")}, " +
                      $"rock ({rollA}, {pitchA}, {heaveA}), usable = {def.IsUsable()}" +
                      (def.LevelTags.Length > 0
                          ? $", cutaway levels [{string.Join(" · ", data.Levels)}]"
                          : ", no cutaway (her rig publishes no geometry())") + ".");
            if (!def.IsUsable())
                throw new InvalidOperationException($"Baked def at {assetPath} is not usable — see fields.");
            return def;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
