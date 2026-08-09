using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ADR 0022 phase 3 — the facet shader as a REAL URP pass, adjudicated in pixels.
    ///
    /// <para><b>What is being proved.</b> Phase 2 proved (headless, exactly) that the extracted
    /// mesh describes what the rig draws. This fixture proves the remaining, GPU-only claim: that
    /// the production render path — IsoFacetHullRenderer's transform + materials, the 2D
    /// renderer's render graph, IsoFacetHullFeature's facet MRT pass, the fullscreen keyline
    /// resolve and the in-scene overlay quad — reproduces the SAME cell the trusted CPU oracle
    /// (<see cref="RigMeshReferenceRasterizer"/>, keyline and dither included) produces from the
    /// same mesh. Every render goes through <c>Camera.Render()</c> with the project's own 2D
    /// renderer asset: nothing here bypasses URP.</para>
    ///
    /// <para><b>Why CI cannot adjudicate this, and what that means.</b> CI runs Unity with NO
    /// graphics device ("Null Device"); a render there does not fail, it CRASHES the editor
    /// (exit 1, no results XML). Every test therefore gates on
    /// <see cref="RequireAGraphicsDevice"/> FIRST and skips loudly — a green CI run carries no
    /// evidence about this fixture. The headless compile guard
    /// (IsoFacetShaderCompileGuardTests) still catches the magenta class on CI; the pixels need
    /// a machine with a GPU, which is where this fixture runs and bites.</para>
    ///
    /// <para><b>The acceptance metric is CONNECTED CLUSTER SIZE, not a percentage</b> — phase 2's
    /// lesson, inherited deliberately: a whole-cell percentage dilutes a localised defect on
    /// exactly the big hulls this ADR is for. The GPU comparison has a real noise floor the CPU
    /// one does not (hardware fill rules vs the rig's slack fill rule, float32 interpolation at
    /// facet boundaries — all of it lives ON edges, so it forms short connected RUNS, not
    /// singletons; see <see cref="MaxGpuNoiseCluster"/>). The sabotage cases are therefore the
    /// phase-3-shaped defect classes (a convention flipped end to end), each proven to land far
    /// above that floor — a single interior facet's winding is phase 2's catch, at exact
    /// arithmetic, not this fixture's.</para>
    ///
    /// <para><b>ADR 0031 — every oracle render here FORCES the keyline gate ON.</b> Production
    /// ships the 1 px keyline OFF (the outline is retired from the world style), but the oracle
    /// this fixture compares against — <see cref="RigMeshReferenceRasterizer"/> — draws it, and
    /// the claim being pinned is that the pass stays VERBATIM-CAPABLE of the rig look. So
    /// <see cref="ForceTheKeylineOn"/> flips the real dial (<c>GameServices.Config</c>, the same
    /// seam GameRoot wires) before every test and restores it after. The gate's own pixel
    /// acceptance is <see cref="KeylineGate_Off_RemovesTheFloodAndOnlyTheFlood"/>.</para>
    /// </summary>
    public class IsoFacetUrpPassTests
    {
        GameConfig _keylineConfig;
        GameConfig _prevConfig;

        /// <summary>ADR 0031: the oracle draws the keyline, so these renders must too — forced
        /// through the owner's real dial, never a parallel test-only path (the guard-rot lesson:
        /// a probe that bypasses the production mechanism proves nothing about it).</summary>
        [SetUp]
        public void ForceTheKeylineOn()
        {
            _prevConfig = GameServices.Config;
            _keylineConfig = ScriptableObject.CreateInstance<GameConfig>();
            _keylineConfig.HullKeylineFlood = true;
            GameServices.Config = _keylineConfig;
        }

        [TearDown]
        public void RestoreTheConfig()
        {
            GameServices.Config = _prevConfig;
            if (_keylineConfig != null) Object.DestroyImmediate(_keylineConfig);
            _keylineConfig = null;
        }
        /// <summary>Everything renders on this otherwise-unused layer — EditMode fixtures share a
        /// scene, and other tests' leftovers must not photobomb the readback (learned by the
        /// sprite-matrix guard).</summary>
        const int ProbeLayer = 31;

        /// <summary>
        /// Largest connected run of GPU-vs-oracle differing pixels accepted as noise.
        ///
        /// ⚠️ MEASURED (D3D11, 2026-07-21), then pinned. The GPU floor sits far above phase 2's
        /// cluster-1 because hardware top-left fill vs the rig's slack double-covering fill
        /// disagrees along facet EDGES, and an edge is connected by nature. The shape of the
        /// measurement says exactly that: CARDINAL headings, whose hull edges run axis-aligned
        /// for hundreds of pixels, produce the long runs — fractional and rocked views break the
        /// same disagreement into short ones:
        /// <code>
        ///   lobster dir 0      cluster 114 (silhouette  14)   2.58%
        ///   lobster dir 2      cluster 253 (silhouette 150)   3.04%   ← beam-on, longest straight edges
        ///   lobster dir 5.31   cluster  22 (silhouette  33)   2.62%
        ///   lobster rocked     cluster  17 (silhouette  44)   2.69%
        ///   dragger dir 3      cluster  51 (silhouette 110)   2.86%
        /// </code>
        /// All of it is the ADR's "facet- and dither-boundary single-step noise" class (the
        /// percentages recover the spike's 1.3–4.4% band) — one ramp step or one coverage pixel
        /// along a 1 px edge line, invisible at play scale. The sabotage floor is 5–200× higher:
        /// Bayer phase 1263 · unflipped light 34,978 · mirrored heading 57,356. If a legitimate
        /// change nudges a run past this, re-measure and re-verify the sabotage margins before
        /// relaxing — a threshold nobody has seen fail is a decoration.
        /// </summary>
        const int MaxGpuNoiseCluster = 300;

        /// <summary>Whole-cell backstop for comparability with ADR 0022's 1.3–4.4% shader figure.
        /// NOT the real criterion (see the fixture doc).</summary>
        const double MaxGpuPercent = 5.0;

        static RigMeshData s_Lobster;
        static Mesh s_LobsterMesh;

        [OneTimeTearDown]
        public void TearDown()
        {
            if (s_LobsterMesh != null) Object.DestroyImmediate(s_LobsterMesh);
            s_LobsterMesh = null;
            s_Lobster = null;
        }

        /// <summary>Must be the FIRST statement of every test: on a Null Device the crash happens
        /// in native rendering code that no assertion can intercept — never allocate first.</summary>
        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — this run has no graphics device (Renderer: Null " +
                    "Device), so the URP facet pass could not render and proved nothing. Expected " +
                    "on CI; the phase 3 rendering acceptance only runs on a machine with a GPU.");
            }
        }

        static void EnsureLobster()
        {
            if (s_Lobster != null) return;
            using var host = RigScriptHostFactory.Create();
            s_Lobster = RigMeshExtractor.ExtractFrom(
                host, "docs/art/rigs/lobsterBoatIsoRig.js", "LobsterBoatIso");
            s_LobsterMesh = RigMeshBuilder.Build(s_Lobster).Mesh;
        }

        // ------------------------------------------------------------------ the golden master

        [Test]
        public void UrpPass_ReproducesTheOracle_AcrossHeadingsAndRock()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var views = new[]
            {
                new RigViewOptions(0, s_Lobster.DefaultElev),
                new RigViewOptions(2, s_Lobster.DefaultElev),
                new RigViewOptions(5.31, s_Lobster.DefaultElev),      // continuous heading — the point
                new RigViewOptions(1, s_Lobster.DefaultElev,
                                   rollDegrees: 2.8, pitchDegrees: 1.6, heavePixels: 1.2),
            };

            // Measure EVERY view before asserting, so a red run still reports the full picture
            // (a first-view abort once hid three quarters of the measurement).
            using var scene = new HullScene(s_Lobster, s_LobsterMesh);
            int worstCluster = 0;
            double worstPercent = 0;
            var report = new System.Text.StringBuilder();
            foreach (var view in views)
            {
                scene.SetPose(view);
                byte[] gpu = scene.Render();
                byte[] oracle = RigMeshReferenceRasterizer.RenderFromMesh(
                    s_Lobster, s_LobsterMesh, view);
                var diff = RigMeshReferenceRasterizer.Compare(oracle, gpu, s_Lobster.W, s_Lobster.H);
                Debug.Log($"[iso-facet-urp] lobster {view.ToJsArgs()}: {diff}");
                report.AppendLine($"  {view.ToJsArgs()}: {diff}");
                worstCluster = Math.Max(worstCluster, diff.LargestDifferingCluster);
                worstPercent = Math.Max(worstPercent, diff.PercentDiffering);
            }

            Assert.LessOrEqual(worstCluster, MaxGpuNoiseCluster,
                "The URP pass diverged from the oracle by a connected patch beyond the measured " +
                $"GPU noise floor:\n{report}GPU noise is thin single-ramp-step runs along facet " +
                "and darkening edges; a patch beyond the floor is a real defect in the pass, the " +
                "resolve or the overlay.");
            Assert.Less(worstPercent, MaxGpuPercent,
                $"Whole-cell divergence beyond ADR 0022's measured shader class:\n{report}");
        }

        /// <summary>The hull that motivated the ADR — one heading, to prove the class scales.</summary>
        [Test]
        public void UrpPass_SideDragger_ReproducesTheOracle()
        {
            RequireAGraphicsDevice();

            RigMeshData data;
            using (var host = RigScriptHostFactory.Create())
                data = RigMeshExtractor.ExtractFrom(
                    host, "docs/art/rigs/sideDraggerIsoRig.js", "SideDraggerIso");
            var build = RigMeshBuilder.Build(data);
            try
            {
                using var scene = new HullScene(data, build.Mesh);
                var view = new RigViewOptions(3, data.DefaultElev);
                scene.SetPose(view);
                byte[] gpu = scene.Render();
                byte[] oracle = RigMeshReferenceRasterizer.RenderFromMesh(data, build.Mesh, view);
                var diff = RigMeshReferenceRasterizer.Compare(oracle, gpu, data.W, data.H);
                Debug.Log($"[iso-facet-urp] dragger {view.ToJsArgs()}: {diff}");

                Assert.LessOrEqual(diff.LargestDifferingCluster, MaxGpuNoiseCluster,
                    $"dragger {view.ToJsArgs()}: connected divergence ({diff}) on the 25 m hull.");
                Assert.Less(diff.PercentDiffering, MaxGpuPercent, $"dragger: {diff}");
            }
            finally
            {
                Object.DestroyImmediate(build.Mesh);
            }
        }

        // ------------------------------------------------------------------ dither lock

        /// <summary>
        /// The production analogue of ADR 0022's 0.00% dither crawl: translate hull AND camera
        /// together by a non-multiple-of-4 pixel offset and the image must be BYTE-IDENTICAL.
        /// Screen-pinned dither (13–16% crawl in the spike's measurement) fails this by exactly
        /// the pixels whose Bayer index changed.
        /// </summary>
        [Test]
        public void Dither_IsIndexedInTheHullFrame_NotTheScreen()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var view = new RigViewOptions(1, s_Lobster.DefaultElev);

            // Scenes are SEQUENTIAL, never simultaneous — a second live hull is culled into the
            // same renderer list and photobombs the frame (found the hard way: 23.8% "crawl"
            // that was really two overlapping hulls).
            byte[] a;
            using (var sceneA = new HullScene(s_Lobster, s_LobsterMesh))
            {
                sceneA.SetPose(view);
                a = sceneA.Render();
            }

            // (7,3) px: odd offsets in both axes, deliberately not a multiple of the 4x4 Bayer tile.
            var offset = new Vector3(7f / s_Lobster.PxPerMetre, 3f / s_Lobster.PxPerMetre, 0f);
            byte[] b;
            using (var sceneB = new HullScene(s_Lobster, s_LobsterMesh, worldOrigin: offset))
            {
                sceneB.SetPose(view);
                b = sceneB.Render();
            }

            var diff = RigMeshReferenceRasterizer.Compare(a, b, s_Lobster.W, s_Lobster.H);
            Debug.Log($"[iso-facet-urp] dither lock under (7,3)px translation: {diff}");
            Assert.AreEqual(0, diff.DifferingPixels,
                $"Translating hull+camera together changed {diff} — the dither (or something " +
                "else) is pinned to the SCREEN, not the hull frame. This is the 13–16% crawl " +
                "class ADR 0022 measured; in motion it shimmers on every moving boat.");
        }

        // ------------------------------------------------------------------ the ADR 0031 gate

        /// <summary>
        /// ADR 0031's pixel acceptance: turning the keyline gate OFF must remove EXACTLY rule 2 —
        /// the flooded outline pixels — and nothing else. Every pixel inked in both renders must be
        /// BYTE-IDENTICAL (the hull's colour and rule 1's depth-edge darkening are outside the
        /// gate), no pixel may appear that ON didn't draw, and the outline must genuinely vanish —
        /// a gate that stops gating renders the same cell twice and fails the removed-count here.
        /// The dial is flipped mid-scene through the owner's real config, proving the per-frame
        /// read end to end. CI has no GPU and skips this loudly; the headless halves of the gate
        /// (dial → material, shader branch, sabotage arms) live in IsoFacetKeylineGateTests.
        /// </summary>
        [Test]
        public void KeylineGate_Off_RemovesTheFloodAndOnlyTheFlood()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var view = new RigViewOptions(0, s_Lobster.DefaultElev);
            using var scene = new HullScene(s_Lobster, s_LobsterMesh);
            scene.SetPose(view);
            byte[] on = scene.Render();               // [SetUp] forced the gate ON

            _keylineConfig.HullKeylineFlood = false;  // the owner's dial, mid-play — re-read per frame
            byte[] off = scene.Render();
            _keylineConfig.HullKeylineFlood = true;

            int removed = 0, appeared = 0, changedSolid = 0;
            for (int i = 0; i < on.Length; i += 4)
            {
                bool inkedOn = on[i + 3] > 0, inkedOff = off[i + 3] > 0;
                if (inkedOff && !inkedOn) appeared++;
                else if (inkedOn && !inkedOff) removed++;
                else if (inkedOn &&
                         (on[i] != off[i] || on[i + 1] != off[i + 1] ||
                          on[i + 2] != off[i + 2] || on[i + 3] != off[i + 3]))
                    changedSolid++;
            }
            Debug.Log($"[iso-facet-urp] keyline gate off: removed {removed} px, " +
                      $"solid changed {changedSolid} px, appeared {appeared} px");

            Assert.AreEqual(0, changedSolid,
                $"{changedSolid} px inked in BOTH renders changed when the gate closed — the gate " +
                "must not touch one solid pixel: the hull's own colour and rule 1's depth-edge " +
                "darkening are outside it (ADR 0031's whole promise).");
            Assert.AreEqual(0, appeared,
                $"{appeared} px were drawn with the gate OFF that ON never drew — the gate can " +
                "only remove the flood, never add anything.");
            Assert.Greater(removed, 0,
                "The gate did NOTHING: ON and OFF rendered the same cell, so the owner's dial is " +
                "not reaching the shader. (This is the gate-stopped-gating direction; its headless " +
                "twin is IsoFacetKeylineGateTests.AGateThatStopsGating_IsCaught.)");
        }

        // ------------------------------------------------------------------ sorting

        /// <summary>
        /// The mesh hull must sort against SpriteRenderers exactly as well as a baked sprite —
        /// whole-object, via the SortingGroup workaround (ADR 0022 "Unchanged"). Above-sprite
        /// covers hull AND keyline (a fullscreen-composited keyline would paint over the sprite
        /// — the defect this quad architecture exists to prevent); below-sprite is covered by
        /// hull AND keyline.
        /// </summary>
        [Test]
        public void OverlayQuad_SortsAgainstSprites_WholeObject()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var view = new RigViewOptions(0, s_Lobster.DefaultElev);

            using var scene = new HullScene(s_Lobster, s_LobsterMesh);
            scene.SetPose(view);
            // Coverage truth for the sorting question is the GPU's OWN baseline (what the hull
            // actually drew) — the oracle's silhouette differs by a handful of fill-rule edge
            // pixels, and those are the golden master's business, not sorting's.
            byte[] baseline = scene.Render();

            var red = new Color32(255, 0, 0, 255);

            // ABOVE: the sprite covers everything it overlaps — hull, darkening, keyline.
            var above = scene.AddCoveringSprite(red, sortingOrder: 10);
            byte[] withAbove = scene.Render();
            Object.DestroyImmediate(above);
            int hullVisibleOverAbove = 0;
            ForEachPixel(withAbove, (i, px) => { if (!Equal(px, red)) hullVisibleOverAbove++; });
            Assert.AreEqual(0, hullVisibleOverAbove,
                $"{hullVisibleOverAbove} px of hull/keyline drew OVER a sprite with a higher " +
                "sorting order. The hull (keyline included) must sort as one object under the " +
                "SortingGroup — if the keyline leaks it is compositing after the scene instead " +
                "of through the overlay quad.");

            // BELOW: the hull covers the sprite wherever the oracle inks (keyline included);
            // the sprite shows only where the cell is empty.
            var below = scene.AddCoveringSprite(red, sortingOrder: -10);
            byte[] withBelow = scene.Render();
            Object.DestroyImmediate(below);
            int wrongOverHull = 0, wrongInEmpty = 0;
            for (int i = 0; i < baseline.Length; i += 4)
            {
                bool inked = baseline[i + 3] > 0;
                var got = new Color32(withBelow[i], withBelow[i + 1], withBelow[i + 2], withBelow[i + 3]);
                var expectHull = new Color32(baseline[i], baseline[i + 1], baseline[i + 2], baseline[i + 3]);
                if (inked && !Equal(got, expectHull)) wrongOverHull++;
                if (!inked && !Equal(got, red)) wrongInEmpty++;
            }
            Assert.AreEqual(0, wrongOverHull,
                $"{wrongOverHull} inked px changed when a sprite slid UNDER the hull — the hull " +
                "must cover it completely where it draws.");
            Assert.AreEqual(0, wrongInEmpty,
                $"{wrongInEmpty} empty-cell px did not show the sprite below — something is " +
                "drawing where the hull should draw nothing.");
        }

        // -------------------------------------------------------- the boat draws over her crew

        /// <summary>
        /// <b>A FIGURE ON DECK IS HIDDEN BY THE HULL IN FRONT OF THEM</b> — owner playtest
        /// 2026-08-07: <i>"rider/player sprites visible THROUGH closed cabins"</i> on hulls with a
        /// cockpit and doors.
        ///
        /// <para>The sorting proof above establishes the thing that made this unfixable by ordering:
        /// a sprite with a higher order covers the hull <b>everywhere</b>, whole-object, by design.
        /// So this asks the question the other way round. The hull is told somebody is standing on
        /// her deck; her facet pass marks the geometry nearer the camera than that point with her
        /// SECOND id; and a covering sprite drawn through <c>HiddenHarbours/DeckOccludedSprite</c> —
        /// sorted well ABOVE her, exactly as the on-deck fisher is — discards there and nowhere
        /// else.</para>
        ///
        /// <para>Measured on real pixels through the real URP pass, against the LOBSTER BOAT,
        /// because she is the hull with a wheelhouse to be hidden behind. Both arms are asserted:
        /// occupant ON must reveal a substantial part of her through the figure, and occupant OFF
        /// must reveal none of her at all — the second is the A/B contract, and it is what says this
        /// whole mechanism costs nothing on every hull with nobody aboard.</para>
        /// </summary>
        [Test]
        public void ADeckOccupant_IsHiddenByTheHullInFrontOfThem_PerPixel()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var view = new RigViewOptions(0, s_Lobster.DefaultElev);
            using var scene = new HullScene(s_Lobster, s_LobsterMesh);
            scene.SetPose(view);
            byte[] baseline = scene.Render();          // the hull alone: the GPU's own coverage truth

            int inked = 0;
            for (int i = 0; i < baseline.Length; i += 4) if (baseline[i + 3] > 0) inked++;
            Assert.Greater(inked, 200, "harness: the hull must actually have drawn something");

            var red = new Color32(255, 0, 0, 255);
            Assert.Greater(scene.Hull.ForeHullId, 0,
                "harness: a live hull must hold a FORE id, or there is nothing for the figure to " +
                "discard against");
            Assert.AreNotEqual(scene.Hull.HullId, scene.Hull.ForeHullId,
                "her two ids must differ, or the split cannot separate anything");
            float foreId = scene.Hull.ForeHullId / 255f;

            // ---- ARM 1: NOBODY ABOARD. The sprite is above the hull and covers her completely,
            // exactly as the sorting proof says it must. This is the contract that makes the feature
            // free when it is not in use — and the failure mode if the split ever fires by default.
            var idle = scene.AddOccludableSprite(red, sortingOrder: 10, occluderId: foreId);
            byte[] withIdle = scene.Render();
            Object.DestroyImmediate(idle);
            int hullThroughIdle = 0;
            ForEachPixel(withIdle, (i, px) => { if (!Equal(px, red)) hullThroughIdle++; });
            Assert.AreEqual(0, hullThroughIdle,
                $"{hullThroughIdle} px of hull drew over the figure with NO occupant set. With " +
                "nobody aboard the split must not happen at all — the facet alpha has to stay " +
                "byte-identical to before this existed.");

            // ---- ARM 2: SOMEBODY IS STANDING IN HER COCKPIT. Aft of amidships, on the sole, which
            // at heading 0 puts her wheelhouse between them and the camera.
            scene.Hull.SetDeckOccupant(new Vector3(0f, -1.6f, 1.35f), true);
            scene.Hull.ApplyPose();

            var aboard = scene.AddOccludableSprite(red, sortingOrder: 10, occluderId: foreId);
            byte[] withAboard = scene.Render();
            Object.DestroyImmediate(aboard);

            int hullThroughAboard = 0, sprayedOutsideTheHull = 0;
            for (int i = 0; i < baseline.Length; i += 4)
            {
                var got = new Color32(withAboard[i], withAboard[i + 1], withAboard[i + 2], withAboard[i + 3]);
                if (Equal(got, red)) continue;
                hullThroughAboard++;
                // Whatever shows through must be the HULL's own pixels: the discard may only happen
                // where she actually drew. A hole anywhere else would mean the figure is being
                // clipped by something that is not the boat.
                if (baseline[i + 3] == 0) sprayedOutsideTheHull++;
            }

            Assert.AreEqual(0, sprayedOutsideTheHull,
                $"{sprayedOutsideTheHull} px outside the hull's own silhouette stopped drawing the " +
                "figure. She may only be hidden where the BOAT is, never over open water.");
            Assert.Greater(hullThroughAboard, inked / 20,
                $"only {hullThroughAboard} of {inked} hull px covered a figure standing in her " +
                "cockpit. A lobster boat's wheelhouse is a large part of her image at heading 0 — " +
                "this close to zero means the depth split is not separating anything.");
            Assert.Less(hullThroughAboard, inked,
                $"ALL {inked} hull px covered the figure — the split has classified the whole hull " +
                "as 'in front', which hides them completely and is the pre-#445 behaviour the owner " +
                "asked to be rid of. The deck under their own boots must stay behind them.");
        }

        // ------------------------------------------------- the occupant SLOTS (the #474 blocker)

        /// <summary>The rig point of a figure standing on the sole, at a given distance forward of
        /// amidships. +Y is the bow; 1.35 m is the cockpit sole the shipped test stands on.</summary>
        static Vector3 Stand(float alongMetres) => new Vector3(0f, alongMetres, 1.35f);

        /// <summary>
        /// Render the lobster boat with ONE occupant, parked in slot <paramref name="slot"/>, and a
        /// full-frame sprite drawn through the occluded-sprite shader with that occupant's own ids.
        ///
        /// <para>The slot is reached by first claiming — and never activating — the slots below it.
        /// An unclaimed-but-parked slot contributes nothing to any band, which is itself part of what
        /// these tests prove: idle slots must be invisible to the picture.</para>
        /// </summary>
        static byte[] RenderOccupantInSlot(int slot, Vector3 stand, Color32 tint, out int hullInk)
        {
            EnsureLobster();
            using var scene = new HullScene(s_Lobster, s_LobsterMesh);
            scene.SetPose(new RigViewOptions(0, s_Lobster.DefaultElev));

            byte[] baseline = scene.Render();
            hullInk = 0;
            for (int i = 0; i < baseline.Length; i += 4) if (baseline[i + 3] > 0) hullInk++;

            var slots = scene.Hull.DeckOccupants;
            for (int i = 0; i < slot; i++)
                Assert.AreEqual(i, slots.Claim(new object()),
                    "harness: claims must fill slots in order, or this is not testing the slot asked for");

            var owner = new object();
            int mine = slots.Claim(owner);
            Assert.AreEqual(slot, mine, "harness: the occupant did not land in the slot under test");
            slots.Set(mine, owner, stand, true);
            scene.Hull.ApplyPose();

            var sprite = scene.AddOccludableSprite(tint, 10, slots.OccluderId(mine));
            byte[] px = scene.Render();
            Object.DestroyImmediate(sprite);
            return px;
        }

        /// <summary>
        /// <b>EVERY SLOT IS A REAL SLOT.</b> One occupant, standing in the same place, must produce
        /// the same picture whichever slot they hold — pixel for pixel, all twelve of them.
        ///
        /// <para><b>Why this is the per-slot sabotage test.</b> Put the wrong depth in slot <i>k</i>
        /// (or read the wrong index, or size the array short, or let the C# constant drift above the
        /// shader's) and slot <i>k</i> alone stops matching slot 0: either the hull stops hiding the
        /// figure at all (the loop never reached that slot) or it hides a different region (it read
        /// somebody else's depth). Each case fails on its own and names its own slot, which is the
        /// point — a single test over all twelve would go red without saying which one broke.</para>
        ///
        /// <para>Slot 0 is the control, so case 0 is a tautology on purpose: it is the one that
        /// proves the HARNESS renders deterministically, and if it ever fails the other eleven mean
        /// nothing.</para>
        /// </summary>
        [Test]
        public void EverySlot_HidesTheSameWayAsSlotZero_PerPixel(
            // Qualified: this file imports both NUnit.Framework and UnityEngine, and each declares a
            // RangeAttribute (the repo's FlowerPoseSelectTests hit the same ambiguity first).
            [NUnit.Framework.Range(0, IsoFacetHullRenderer.DeckOccupantSlots - 1)] int slot)
        {
            RequireAGraphicsDevice();
            var red = new Color32(255, 0, 0, 255);
            Vector3 stand = Stand(-1.6f);           // the cockpit — the wheelhouse is in front of it

            byte[] control = RenderOccupantInSlot(0, stand, red, out int inkA);
            byte[] under = RenderOccupantInSlot(slot, stand, red, out int inkB);

            Assert.Greater(inkA, 200, "harness: the hull must actually have drawn something");
            Assert.AreEqual(inkA, inkB, "harness: the two renders must be of the same boat");

            int hidden = 0, differ = 0;
            for (int i = 0; i < control.Length; i += 4)
            {
                var a = new Color32(control[i], control[i + 1], control[i + 2], control[i + 3]);
                var b = new Color32(under[i], under[i + 1], under[i + 2], under[i + 3]);
                if (!Equal(a, red)) hidden++;
                if (!Equal(a, b)) differ++;
            }

            Assert.Greater(hidden, inkA / 20,
                $"the control (slot 0) hid only {hidden} of {inkA} hull px — the harness is not " +
                "occluding anything, so slot " + slot + " cannot be compared against it");
            Assert.AreEqual(0, differ,
                $"slot {slot} drew {differ} px differently from slot 0 with the occupant standing in " +
                "exactly the same place. A slot that does not behave like every other slot is a slot " +
                "the deck cannot use — check the facet shader's occupant loop bound, its indexing, " +
                "and that HH_DECK_OCCUPANT_SLOTS still matches IsoFacetHullRenderer.DeckOccupantSlots.");
        }

        /// <summary>
        /// <b>THE THING #474 STOPPED ON.</b> Three occupants at three depths on one deck. Each is
        /// hidden by the hull in front of THEM, and — because the pixels in front of a near occupant
        /// are always also in front of a deeper one — their hidden regions must NEST: strictly
        /// growing as the occupant stands deeper, each one containing the last.
        ///
        /// <para>A single split plane cannot produce that. Nor can one id per occupant: a pixel
        /// carries exactly one id, so an id that means "in front of the trap" cannot also mean "in
        /// front of the fisher behind it", and the geometry between the two ends up marked for
        /// neither. Nesting is the observable that separates the band encoding from every cheaper
        /// thing that looks like it works with one occupant aboard.</para>
        ///
        /// <para>The depth ORDER is read from the ids the hull hands out rather than assumed from the
        /// stand points — the projection's sign is exactly the sort of thing that has been fixed and
        /// re-fixed on this lane, and this test has no business re-deriving it.</para>
        /// </summary>
        [Test]
        public void OccupantsAtDifferentDepths_AreHiddenInStrictlyNestedRegions_PerPixel()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            using var scene = new HullScene(s_Lobster, s_LobsterMesh);
            scene.SetPose(new RigViewOptions(0, s_Lobster.DefaultElev));
            byte[] baseline = scene.Render();
            int inked = 0;
            for (int i = 0; i < baseline.Length; i += 4) if (baseline[i + 3] > 0) inked++;
            Assert.Greater(inked, 200, "harness: the hull must actually have drawn something");

            var slots = scene.Hull.DeckOccupants;
            var owners = new object[3];
            var mySlot = new int[3];
            float[] along = { 1.6f, 0f, -1.6f };          // bow-ward, amidships, cockpit
            for (int k = 0; k < 3; k++)
            {
                owners[k] = new object();
                mySlot[k] = slots.Claim(owners[k]);
                Assert.GreaterOrEqual(mySlot[k], 0, "harness: three claims must fit in twelve slots");
                slots.Set(mySlot[k], owners[k], Stand(along[k]), true);
            }
            scene.Hull.ApplyPose();
            Assert.AreEqual(3, slots.ActiveCount, "all three must be standing before anything is read");

            // Sort by the id the hull handed back: rank 1 (the LOWEST id) is the deepest occupant,
            // the one the most hull is in front of.
            var order = new int[] { 0, 1, 2 };
            Array.Sort(order, (x, y) => slots.OccluderId(mySlot[x]).CompareTo(slots.OccluderId(mySlot[y])));
            Assert.AreNotEqual(slots.OccluderId(mySlot[order[0]]), slots.OccluderId(mySlot[order[2]]),
                "three occupants at three depths must not all share one id — if they do the ranks " +
                "collapsed and this is the single-plane split again, wearing twelve slots");

            var red = new Color32(255, 0, 0, 255);
            var hidden = new bool[3][];
            var counts = new int[3];
            for (int rank = 0; rank < 3; rank++)
            {
                int k = order[rank];
                var sprite = scene.AddOccludableSprite(red, 10, slots.OccluderId(mySlot[k]));
                byte[] px = scene.Render();
                Object.DestroyImmediate(sprite);

                var mask = new bool[px.Length / 4];
                int n = 0, outside = 0;
                for (int i = 0; i < px.Length; i += 4)
                {
                    var got = new Color32(px[i], px[i + 1], px[i + 2], px[i + 3]);
                    bool hid = !Equal(got, red);
                    mask[i / 4] = hid;
                    if (!hid) continue;
                    n++;
                    if (baseline[i + 3] == 0) outside++;
                }
                Assert.AreEqual(0, outside,
                    $"{outside} px OUTSIDE the hull's silhouette stopped drawing occupant {rank}. " +
                    "Nobody may be cut out over open water — that is another boat's id leaking into " +
                    "this hull's discard range.");
                hidden[rank] = mask;
                counts[rank] = n;
            }

            Assert.Greater(counts[2], 0,
                "even the NEAREST occupant must have some hull in front of them at this heading, or " +
                "the nesting below is being read off three empty sets");

            for (int rank = 0; rank < 2; rank++)
            {
                Assert.Greater(counts[rank], counts[rank + 1],
                    $"the deeper occupant (rank {rank + 1}) is hidden by {counts[rank]} px and the " +
                    $"nearer one (rank {rank + 2}) by {counts[rank + 1]} — the deeper one must have " +
                    "STRICTLY more of the boat in front of them. Equal counts mean the two are " +
                    "sharing one split plane, which is exactly the defect this seam replaced.");

                int notContained = 0;
                for (int p = 0; p < hidden[rank].Length; p++)
                    if (hidden[rank + 1][p] && !hidden[rank][p]) notContained++;
                Assert.AreEqual(0, notContained,
                    $"{notContained} px hide the NEARER occupant but not the deeper one. The regions " +
                    "must nest: anything standing between the camera and a near occupant is also " +
                    "between the camera and everyone behind them.");
            }
        }

        /// <summary>
        /// <b>THE BOAT'S OWN PICTURE IS UNCHANGED BY ANYBODY STANDING ON HER.</b> The split partitions
        /// her alpha into more ids; her overlay quad has to accept every one of them, or she loses
        /// the very pixels that were marked — a wheelhouse deleted the moment a second thing steps
        /// aboard.
        ///
        /// <para>This is the A/B that keeps the whole mechanism honest, and it is asserted on pixels
        /// with nobody, one occupant and three. Byte-identical, all three times.</para>
        /// </summary>
        [Test]
        public void TheHullsOwnPicture_IsByteIdenticalHoweverManyStandOnHer()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            using var scene = new HullScene(s_Lobster, s_LobsterMesh);
            scene.SetPose(new RigViewOptions(0, s_Lobster.DefaultElev));
            byte[] empty = scene.Render();

            var slots = scene.Hull.DeckOccupants;
            float[] along = { 1.6f, 0f, -1.6f };
            var owners = new object[3];
            var mySlot = new int[3];

            for (int k = 0; k < 3; k++)
            {
                owners[k] = new object();
                mySlot[k] = slots.Claim(owners[k]);
                slots.Set(mySlot[k], owners[k], Stand(along[k]), true);
                scene.Hull.ApplyPose();

                byte[] withCrew = scene.Render();
                int differ = 0;
                for (int i = 0; i < empty.Length; i++) if (empty[i] != withCrew[i]) differ++;
                Assert.AreEqual(0, differ,
                    $"{differ} bytes of the boat's own image changed with {k + 1} occupant(s) aboard. " +
                    "The split is a PARTITION of her alpha, not a change to her picture — her overlay " +
                    "must compose every band in her fore block (_HullIdForeSpan), or she is losing " +
                    "the geometry the split marked.");
            }
        }

        /// <summary>
        /// <b>A REFUSED OCCUPANT DRAWS WHOLE.</b> Fill every slot, then ask for one more. The claim is
        /// refused and logged; the refused claimant has no id, so it draws exactly as it did before
        /// any of this existed — over the boat, completely.
        ///
        /// <para>Whole or nothing is the contract that matters. Half-occluded is what a silently
        /// dropped claim would look like, and it is indistinguishable from the shader bug this seam
        /// was built to fix.</para>
        /// </summary>
        [Test]
        public void AnOccupantRefusedForWantOfASlot_DrawsWhole_NeverHalfOccluded()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            using var scene = new HullScene(s_Lobster, s_LobsterMesh);
            scene.SetPose(new RigViewOptions(0, s_Lobster.DefaultElev));

            var slots = scene.Hull.DeckOccupants;
            int capacity = IsoFacetHullRenderer.DeckOccupantSlots;
            for (int i = 0; i < capacity; i++)
            {
                var owner = new object();
                int s = slots.Claim(owner);
                Assert.AreEqual(i, s, "harness: the deck must fill in order");
                slots.Set(s, owner, Stand(-1.6f + i * 0.2f), true);
            }
            scene.Hull.ApplyPose();

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("deck-occupant slots"));
            int refused = slots.Claim(new object());
            Assert.AreEqual(-1, refused, "the surplus claim must be refused, loudly, never squeezed in");

            // A refused claimant has nothing to discard against: id 0, which every sprite in the game
            // bar those on a deck carries.
            var red = new Color32(255, 0, 0, 255);
            var sprite = scene.AddOccludableSprite(red, sortingOrder: 10, occluderId: 0f);
            byte[] px = scene.Render();
            Object.DestroyImmediate(sprite);

            int hullThrough = 0;
            ForEachPixel(px, (i, got) => { if (!Equal(got, red)) hullThrough++; });
            Assert.AreEqual(0, hullThrough,
                $"{hullThrough} px of hull drew over an occupant who was REFUSED a slot. With no id " +
                "the discard must not run at all: the refused thing draws whole, exactly as it did " +
                "before this seam existed. Anything else is the half-occluded picture that a silent " +
                "drop would produce.");
        }

        // ------------------------------------------------------------------ the deck contract

        /// <summary>
        /// The owner's deck-walking contract (2026-07-21): a renderer drawn through the
        /// HHHullDeck list is depth-tested PER-PIXEL against the hull's private z-buffer. Probed
        /// in all three regimes — decisively in front (wins everywhere), decisively behind
        /// (loses everywhere), and intersecting (wins and loses within one draw). The
        /// front/behind pair also proves the z-DIRECTION convention: plain ZTest LEqual under
        /// the render-graph camera path, no hand-flipped reversed-Z (the spike's GEqual/clear-0
        /// convention belonged to its hand-built command buffer and must NOT carry over).
        /// </summary>
        [Test]
        public void DeckRenderers_AreDepthTestedAgainstTheHull_PerPixel()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var view = new RigViewOptions(0, s_Lobster.DefaultElev);
            using var scene = new HullScene(s_Lobster, s_LobsterMesh);
            scene.SetPose(view);
            byte[] baseline = scene.Render();

            var magenta = new Color32(255, 0, 255, 255);
            // A 2x2 m probe centred on the hull origin — midship, solid hull all around, with
            // geometry both nearer and farther than the z=0 plane.
            var rect = new Rect(-1f, -1f, 2f, 2f);

            // BEHIND everything (z = +50, camera looks along +Z): the hull wins every pixel.
            byte[] behind = scene.RenderWithDeckProbe(rect, z: 50f, magenta);
            var diffBehind = RigMeshReferenceRasterizer.Compare(baseline, behind, s_Lobster.W, s_Lobster.H);
            Assert.AreEqual(0, diffBehind.DifferingPixels,
                $"A deck probe 50 m BEHIND the hull changed {diffBehind} — it must lose the " +
                "depth test everywhere. If it painted over the hull, the z-direction convention " +
                "is inverted (the spike's hand-built GEqual/clear-0 does NOT apply to the " +
                "render-graph camera path).");

            // IN FRONT of everything (z = -50): the probe wins every pixel of its footprint.
            byte[] front = scene.RenderWithDeckProbe(rect, z: -50f, magenta);
            int rectWrong = 0;
            ForEachPixelInWorldRect(scene, rect, shrinkPx: 1, (i) =>
            {
                if (!(front[i] == magenta.r && front[i + 1] == magenta.g && front[i + 2] == magenta.b))
                    rectWrong++;
            });
            Assert.AreEqual(0, rectWrong,
                $"{rectWrong} px inside a probe 50 m IN FRONT of the hull were not probe-coloured " +
                "— it must win the depth test everywhere it covers.");

            // INTERSECTING (z = 0): the hull is nearer in places and farther in others, so ONE
            // quad must both occlude and be occluded — the per-pixel claim itself.
            byte[] mixed = scene.RenderWithDeckProbe(rect, z: 0f, magenta);
            int probeWins = 0, hullWins = 0;
            ForEachPixelInWorldRect(scene, rect, shrinkPx: 1, (i) =>
            {
                bool isProbe = mixed[i] == magenta.r && mixed[i + 1] == magenta.g && mixed[i + 2] == magenta.b;
                bool sameAsBaseline = mixed[i] == baseline[i] && mixed[i + 1] == baseline[i + 1] &&
                                      mixed[i + 2] == baseline[i + 2];
                if (isProbe) probeWins++;
                else if (sameAsBaseline) hullWins++;
            });
            Debug.Log($"[iso-facet-urp] deck probe at z=0: probe wins {probeWins} px, hull wins {hullWins} px");
            Assert.Greater(probeWins, 0,
                "An intersecting deck probe never won the depth test — per-pixel deck occlusion " +
                "is not happening (whole-object sorting would look exactly like this).");
            Assert.Greater(hullWins, 0,
                "An intersecting deck probe won EVERYWHERE — the hull never occluded it, so the " +
                "z-buffer is not being tested per pixel.");
        }

        // ------------------------------------------------------------------ SABOTAGE

        /// <summary>
        /// ⚠️ A golden master nobody has seen fail is a decoration. These flip the three
        /// conventions phase 3 itself is responsible for — the reflected-frame light sign, the
        /// hull-frame dither phase, and the heading mirror — and assert the cluster metric
        /// catches each, with the measured margin on the record. (A single facet's winding is
        /// deliberately NOT a case here: that is extraction-level damage, caught by phase 2's
        /// EXACT arithmetic where it produces clusters of 2 against a floor of 1; under the GPU
        /// floor it would be dishonest theatre.)
        /// </summary>
        [Test]
        public void Sabotage_UnflippedLightZ_IsCaught()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var view = new RigViewOptions(0, s_Lobster.DefaultElev);
            byte[] oracle = RigMeshReferenceRasterizer.RenderFromMesh(s_Lobster, s_LobsterMesh, view);

            // Hand the component a pre-negated LN: its own reflection flip then restores the
            // rig-space vector — i.e. the shader dots with the UNreflected light, the exact
            // mistake "cleaning up the weird minus sign" would make.
            var setup = HullScene.SetupFrom(s_Lobster, s_LobsterMesh);
            setup.LightN = new Vector3(setup.LightN.x, setup.LightN.y, -setup.LightN.z);

            AssertSabotageCaught(setup, view, oracle, "light z-flip removed (reflection convention)");
        }

        [Test]
        public void Sabotage_ScreenPhaseDither_IsCaught()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var view = new RigViewOptions(0, s_Lobster.DefaultElev);
            byte[] oracle = RigMeshReferenceRasterizer.RenderFromMesh(s_Lobster, s_LobsterMesh, view);

            // The spike's (0,1) phase offset applied where it does not belong — ADR 0022's
            // dither-crawl defect class on a still image.
            var setup = HullScene.SetupFrom(s_Lobster, s_LobsterMesh);
            var shifted = new float[16];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    shifted[x * 4 + y] = setup.Bayer16[x * 4 + ((y + 1) & 3)];
            setup.Bayer16 = shifted;

            AssertSabotageCaught(setup, view, oracle, "Bayer grid phase-shifted +1 in y");
        }

        [Test]
        public void Sabotage_MirroredHeading_IsCaught()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            // The iso-art mirror saga's defect class: heading sign flipped end to end. dir 1 vs
            // dir -1 differ by 90° of turntable — the bow points the wrong way.
            var view = new RigViewOptions(1, s_Lobster.DefaultElev);
            byte[] oracle = RigMeshReferenceRasterizer.RenderFromMesh(s_Lobster, s_LobsterMesh, view);

            using var scene = new HullScene(s_Lobster, s_LobsterMesh);
            scene.Hull.HeadingDirUnits = -1f;
            scene.Hull.ApplyPose();
            byte[] gpu = scene.Render();
            var diff = RigMeshReferenceRasterizer.Compare(oracle, gpu, s_Lobster.W, s_Lobster.H);
            Debug.Log($"[iso-facet-urp][SABOTAGE] mirrored heading: {diff}");
            Assert.Greater(diff.LargestDifferingCluster, MaxGpuNoiseCluster,
                "SABOTAGE NOT DETECTED — a mirrored heading stayed under the noise floor. The " +
                "golden master cannot see the CCW defect class and every green run above is " +
                "worth less than it looks.");
        }

        void AssertSabotageCaught(IsoFacetHullSetup setup, RigViewOptions view, byte[] oracle, string what)
        {
            using var scene = new HullScene(s_Lobster, s_LobsterMesh, setup);
            scene.SetPose(view);
            byte[] gpu = scene.Render();
            var diff = RigMeshReferenceRasterizer.Compare(oracle, gpu, s_Lobster.W, s_Lobster.H);
            Debug.Log($"[iso-facet-urp][SABOTAGE] {what}: {diff}");
            Assert.Greater(diff.LargestDifferingCluster, MaxGpuNoiseCluster,
                $"SABOTAGE NOT DETECTED — {what} produced {diff}, within the noise this fixture " +
                "tolerates. The golden master cannot see this defect class.");
        }

        // ------------------------------------------------------------------ plumbing

        static bool Equal(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        static void ForEachPixel(byte[] rgba, Action<int, Color32> visit)
        {
            for (int i = 0; i < rgba.Length; i += 4)
                visit(i, new Color32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]));
        }

        /// <summary>Visit every byte-index whose pixel lies inside a WORLD-space rect (hull-origin
        /// relative), shrunk by <paramref name="shrinkPx"/> to keep assertions off the exact edge.</summary>
        static void ForEachPixelInWorldRect(HullScene scene, Rect rect, int shrinkPx, Action<int> visit)
        {
            var d = scene.Data;
            int x0 = Mathf.CeilToInt((float)d.PivotX + rect.xMin * d.PxPerMetre) + shrinkPx;
            int x1 = Mathf.FloorToInt((float)d.PivotX + rect.xMax * d.PxPerMetre) - shrinkPx;
            int y0 = Mathf.CeilToInt((float)d.PivotY - rect.yMax * d.PxPerMetre) + shrinkPx;
            int y1 = Mathf.FloorToInt((float)d.PivotY - rect.yMin * d.PxPerMetre) - shrinkPx;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    visit((y * d.W + x) * 4);
        }

        /// <summary>
        /// A self-cleaning render harness: one configured hull, one camera aimed so the rig
        /// pivot lands on its exact cell pixel (the spike's framing), readback flipped to the
        /// rig's top-left orientation. Waits out shader compilation before measuring — the
        /// cold-shader-cache trap fakes exactly the regressions this fixture hunts.
        /// </summary>
        sealed class HullScene : IDisposable
        {
            public readonly RigMeshData Data;
            public readonly IsoFacetHullRenderer Hull;
            readonly GameObject _hullGo;
            readonly GameObject _camGo;
            readonly Camera _cam;
            readonly RenderTexture _rt;
            readonly Vector3 _origin;
            bool _warm;

            public HullScene(RigMeshData data, Mesh mesh,
                             IsoFacetHullSetup setup = null, Vector3 worldOrigin = default)
            {
                Data = data;
                _origin = worldOrigin;

                _hullGo = new GameObject("TestHull");
                _hullGo.transform.position = worldOrigin;
                Hull = _hullGo.AddComponent<IsoFacetHullRenderer>();
                Hull.Configure(setup ?? SetupFrom(data, mesh));
                SetLayerRecursive(_hullGo.transform, ProbeLayer);

                float ppu = data.PxPerMetre;
                float ox = (float)((data.PivotX - data.W / 2.0) / ppu);
                float oy = (float)((data.H / 2.0 - data.PivotY) / ppu);
                _camGo = new GameObject("TestHullCam");
                _cam = _camGo.AddComponent<Camera>();
                _cam.orthographic = true;
                _cam.orthographicSize = data.H / (2f * ppu);
                _cam.transform.position = worldOrigin + new Vector3(-ox, -oy, -100f);
                _cam.nearClipPlane = 1f;
                _cam.farClipPlane = 400f;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = Color.clear;
                _cam.cullingMask = 1 << ProbeLayer;
                _cam.allowHDR = false;    // byte-exact palette needs the 8-bit sRGB path
                _cam.allowMSAA = false;

                _rt = new RenderTexture(data.W, data.H, 24, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Point,
                };
                _cam.targetTexture = _rt;
            }

            public static IsoFacetHullSetup SetupFrom(RigMeshData data, Mesh mesh)
            {
                var ramps = new Color32[data.Materials.Count][];
                var offs = new int[data.Materials.Count];
                for (int m = 0; m < data.Materials.Count; m++)
                {
                    ramps[m] = data.Materials[m].Ramp;
                    offs[m] = data.Materials[m].Off;
                }
                var bayer = new float[16];
                for (int x = 0; x < 4; x++)
                    for (int y = 0; y < 4; y++)
                        bayer[x * 4 + y] = (float)data.Bayer[x, y];
                return new IsoFacetHullSetup
                {
                    Mesh = mesh,
                    Ramps = ramps,
                    RampOffsets = offs,
                    LightN = new Vector3((float)data.LightN.X, (float)data.LightN.Y, (float)data.LightN.Z),
                    Gain = (float)data.Gain,
                    Bias = (float)data.Bias,
                    Bayer16 = bayer,
                    Keyline = data.Keyline,
                    PivotPx = new Vector2((float)data.PivotX, (float)data.PivotY),
                    PxPerMetre = data.PxPerMetre,
                    CellW = data.W,
                    CellH = data.H,
                    ElevationDeg = (float)data.DefaultElev,
                };
            }

            public void SetPose(RigViewOptions view)
            {
                Hull.HeadingDirUnits = (float)view.Dir;
                Hull.RollDegrees = (float)view.RollDegrees;
                Hull.PitchDegrees = (float)view.PitchDegrees;
                Hull.HeavePixels = (float)view.HeavePixels;
                Hull.ApplyPose();
            }

            public byte[] Render()
            {
                EnsureVariantsCompiled();
                _cam.Render();
                return ReadBackTopLeft();
            }

            /// <summary>A full-frame sprite (plain SpriteRenderer, default 2D material) for the
            /// sorting proof. Caller destroys it.</summary>
            public GameObject AddCoveringSprite(Color32 tint, int sortingOrder)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply(false, true);
                var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 0.1f);

                var go = new GameObject("CoveringSprite") { layer = ProbeLayer };
                go.transform.position = _origin;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;                    // 40x40 world units — covers any cell here
                sr.color = tint;
                sr.sortingOrder = sortingOrder;
                // The project's DEFAULT sprite material is the LIT one, and this scene has no
                // Light2D — a lit sprite would render black and fake a sorting failure. The
                // sorting question is identical either way; ask it with the unlit material.
                var unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                Assert.IsNotNull(unlit, "URP's Sprite-Unlit-Default shader is missing?");
                sr.sharedMaterial = new Material(unlit);
                _warm = false;                         // new material variant may need compiling
                return go;
            }

            /// <summary>
            /// The same full-frame sprite, drawn through <c>HiddenHarbours/DeckOccludedSprite</c> and
            /// told which hull id hides it — a stand-in for the on-deck figure, who is exactly this:
            /// an ordinary sprite sorted above the boat, with one extra number on its property
            /// block. Caller destroys it.
            ///
            /// <para>The occluder id goes on a PROPERTY BLOCK rather than the material, because that
            /// is how the shipping path writes it (per renderer, no material instancing) — a test
            /// that set it on the material would be proving a mechanism nothing uses.</para>
            ///
            /// <para><b>Two ids, because the discard is a RANGE.</b> An occupant is hidden by every
            /// band nearer than their own, so they carry their own LOW id and the hull's block TOP.
            /// <paramref name="occluderIdTop"/> defaults to this hull's real top, which is what the
            /// shipping path (<c>DeckRiderVisual</c>) writes — pass it explicitly only to prove what
            /// happens when it is wrong.</para>
            /// </summary>
            public GameObject AddOccludableSprite(Color32 tint, int sortingOrder, float occluderId,
                                                  float occluderIdTop = -1f)
            {
                var go = AddCoveringSprite(tint, sortingOrder);
                var sr = go.GetComponent<SpriteRenderer>();

                var shader = Shader.Find("HiddenHarbours/DeckOccludedSprite");
                Assert.IsNotNull(shader,
                    "HiddenHarbours/DeckOccludedSprite is missing — the figure has no way to be " +
                    "hidden behind anything, so treat it as a failure, not a pass.");
                sr.sharedMaterial = new Material(shader);

                var block = new MaterialPropertyBlock();
                sr.GetPropertyBlock(block);
                block.SetFloat("_HHDeckOccluderId", occluderId);
                block.SetFloat("_HHDeckOccluderIdTop",
                               occluderIdTop >= 0f ? occluderIdTop : Hull.ForeHullIdTop / 255f);
                sr.SetPropertyBlock(block);

                _warm = false;                         // a new shader variant needs compiling
                return go;
            }

            /// <summary>Render with a flat HHHullDeck probe quad at world z (hull-origin frame).</summary>
            public byte[] RenderWithDeckProbe(Rect rect, float z, Color32 color)
            {
                var shader = Shader.Find("HiddenHarbours/_HullDeckProbe");
                Assert.IsNotNull(shader, "HullDeckProbe.shader missing — the deck contract has no probe.");

                var mesh = new Mesh { name = "DeckProbeQuad" };
                mesh.SetVertices(new[]
                {
                    new Vector3(rect.xMin, rect.yMin, 0), new Vector3(rect.xMax, rect.yMin, 0),
                    new Vector3(rect.xMax, rect.yMax, 0), new Vector3(rect.xMin, rect.yMax, 0),
                });
                mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);

                var mat = new Material(shader);
                mat.SetColor("_ProbeColor", ((Color)color).linear);
                mat.SetColor("_KeyColor", ((Color)Data.Keyline).linear);

                var go = new GameObject("DeckProbe") { layer = ProbeLayer };
                go.transform.position = _origin + new Vector3(0, 0, z);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                var props = new MaterialPropertyBlock();
                props.SetFloat(IsoFacetShaderIds.HullId, Hull.HullId / 255f);
                mr.SetPropertyBlock(props);

                try
                {
                    _warm = false;
                    return Render();
                }
                finally
                {
                    Object.DestroyImmediate(go);
                    Object.DestroyImmediate(mat);
                    Object.DestroyImmediate(mesh);
                }
            }

            /// <summary>Block until a render stops triggering shader compilation (the cold-cache
            /// trap: URP's async-compile placeholder produces a wrong image that is
            /// indistinguishable from a real regression — see the sprite-matrix guard's history).</summary>
            void EnsureVariantsCompiled()
            {
                if (_warm) return;
                const double timeoutSeconds = 180.0;
                const int maxWarmUps = 10;
                var clock = Stopwatch.StartNew();
                int renders = 0;
                for (; renders < maxWarmUps; renders++)
                {
                    _cam.Render();
                    if (!ShaderUtil.anythingCompiling) break;
                    while (ShaderUtil.anythingCompiling && clock.Elapsed.TotalSeconds < timeoutSeconds)
                        Thread.Sleep(25);
                }
                if (ShaderUtil.anythingCompiling || renders >= maxWarmUps)
                    Assert.Fail(
                        "HULL SHADERS NEVER FINISHED COMPILING — this is NOT a facet-pass " +
                        $"regression. After {renders} warm-up render(s) and " +
                        $"{clock.Elapsed.TotalSeconds:F1}s, the compiler was still busy, so a " +
                        "measuring render would land on the async placeholder and produce a fake " +
                        "diff. Re-run with a warm shader cache; if it never settles, check the " +
                        "console for compile errors in the IsoFacet shaders.");
                _warm = true;
            }

            byte[] ReadBackTopLeft()
            {
                var prev = RenderTexture.active;
                RenderTexture.active = _rt;
                var tex = new Texture2D(Data.W, Data.H, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Data.W, Data.H), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                var px = tex.GetPixels32();
                Object.DestroyImmediate(tex);

                // GetPixels32 is BOTTOM-left origin; the rig's cell is TOP-left. Flip once, here.
                int w = Data.W, h = Data.H;
                var bytes = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                {
                    int srcRow = (h - 1 - y) * w;
                    int dstRow = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        var c = px[srcRow + x];
                        int d = (dstRow + x) * 4;
                        bytes[d] = c.r; bytes[d + 1] = c.g; bytes[d + 2] = c.b; bytes[d + 3] = c.a;
                    }
                }
                return bytes;
            }

            static void SetLayerRecursive(Transform t, int layer)
            {
                t.gameObject.layer = layer;
                for (int i = 0; i < t.childCount; i++)
                    SetLayerRecursive(t.GetChild(i), layer);
            }

            public void Dispose()
            {
                RenderTexture.active = null;
                if (_cam != null) _cam.targetTexture = null;
                if (_camGo != null) Object.DestroyImmediate(_camGo);
                if (_hullGo != null) Object.DestroyImmediate(_hullGo);
                if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
            }
        }
    }
}
