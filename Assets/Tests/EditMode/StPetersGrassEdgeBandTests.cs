using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The meadow's EDGE</b> — the 2026-08-26 pass, measured.
    ///
    /// <para><b>The defect.</b> The owner walked the retuned island and ruled the coverage good and
    /// the transitions bad: tall-tuft fields ending in a hard line against the flat compacted-grass
    /// splat. A field gated on a hard predicate has no choice about that — a cell either clears
    /// <see cref="StPetersGrass.ChanceOpen"/> (0.97) or plants nothing, so the meadow met bare ground
    /// at a step from near-full cover to nothing in one 0.85 m cell.</para>
    ///
    /// <para><b>⚠ DENSITY IS MEASURED, NOT REASONED — the law this whole lane runs on.</b> Every
    /// assertion here walks the planter's OWN pure deciders over the planter's OWN grid and compares
    /// what they predict against what <see cref="StPetersGrass.Scatter"/> actually planted (the
    /// derived-bounds pin, third confirmed use). Nothing is a literal that a re-tune would falsify:
    /// turn <see cref="StPetersGrass.EdgeBandMetres"/> and prediction and island move together, break
    /// the falloff and only one of them moves.</para>
    ///
    /// <para><b>⚠ AND THE INTERIOR IS THE OTHER HALF OF THE CLAIM.</b> "Do not retune the
    /// field-interior density" was an explicit instruction, so this does not merely check that the
    /// interior is <i>about</i> right — it reproduces the pre-band decision cell for cell and requires
    /// it to be EXACTLY reproduced wherever the band does not reach.</para>
    /// </summary>
    public class StPetersGrassEdgeBandTests
    {
        GameObject _go;
        TidalTerrain _terrain;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TidalTerrain_EdgeBandTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        ITidalTerrain Terrain() => _terrain;

        // =====================================================================================
        //  walking the planter's own grid
        // =====================================================================================

        /// <summary>One candidate cell of the meadow, with everything the deciders say about it. Built
        /// by <see cref="Candidates"/> from the RUNTIME scatter's own grid — the same
        /// <see cref="GrassFieldScatter.CellCentre"/> the bake and the renderer use, so this walk and
        /// the island cannot disagree about where a cell is.</summary>
        struct Cell
        {
            public int Ix, Iy;
            public Vector2 P;
            public float EdgeDistance;      // metres from the nearest edge of the field
            public float Clearance;         // metres from the nearest CUT edge (fence, tread, wall)
            public float Contour;           // metres from the nearest FIELD contour (swathe, floor)
            public float BandWidth;         // the meandering band width at this point
            public float Falloff;           // 0..1 — the multiplier the edge band applies
            public GrassTier Tier;
            public float LegacyChance;      // what the pre-band gate would have used
            public float Chance;            // what the gate uses now
            public float Roll;              // the cell's own accept hash
        }

        /// <summary>Every cell the meadow is allowed to plant on, with its deciders evaluated once.
        /// Walked exactly as <see cref="StPetersGrass.Scatter"/> walks it, and cached across the tests
        /// in this fixture because the walk is the expensive part.</summary>
        List<Cell> Candidates()
        {
            if (_candidates != null) return _candidates;

            var terrain = Terrain();
            var layout = StPetersGrass.FieldLayout();
            var cells = new List<Cell>(60000);

            for (int ix = 0; ix < layout.CellsX; ix++)
            for (int iy = 0; iy < layout.CellsY; iy++)
            {
                var p = GrassFieldScatter.CellCentre(layout, ix, iy);
                if (!StPetersGrass.IsPlantableMeadow(terrain, p)) continue;
                if (!StPetersGrass.InSwathe(p)) continue;

                float e = terrain.ElevationAt(p);
                float falloff = StPetersGrass.EdgeFalloff(terrain, p, out GrassTier tier);

                cells.Add(new Cell
                {
                    Ix = ix,
                    Iy = iy,
                    P = p,
                    EdgeDistance = StPetersGrass.EdgeDistanceMetres(terrain, p),
                    Clearance = StPetersGrass.ClearanceDistanceMetres(p),
                    Contour = StPetersGrass.FieldContourDistanceMetres(terrain, p),
                    BandWidth = StPetersGrass.BandWidthAt(p),
                    Falloff = falloff,
                    Tier = tier,
                    LegacyChance = StPetersWoods.InStand(p, e)
                        ? StPetersGrass.ChanceWoods
                        : StPetersGrass.ChanceOpen,
                    Chance = StPetersGrass.ChanceAt(terrain, p, e) * falloff,
                    Roll = StPetersShoreMap.Hash01(ix, iy, 173),
                });
            }

            _candidates = cells;
            return cells;
        }

        List<Cell> _candidates;

        // =====================================================================================
        //  1. the band thins the field, and it thins it MONOTONICALLY
        // =====================================================================================

        /// <summary>
        /// <b>The owner's ask, as a number.</b> Walk out from the field's boundary and the density must
        /// RISE, without a step, until it reaches the interior's — that is the whole difference between
        /// a field that lies down into the ground it meets and one that stops against it.
        ///
        /// <para>Measured in bands of the LOCAL band width rather than in metres, because the band's
        /// width meanders on purpose (<see cref="StPetersGrass.BandWidthAt"/>): a fixed-metre bucket
        /// would mix hem cells from a narrow stretch with interior cells from a wide one and smear the
        /// very gradient this is measuring.</para>
        ///
        /// <para>Both halves are asserted. <b>Predicted</b> density per bucket is the mean of the
        /// planter's own accept chance there; <b>measured</b> is the share of those cells the planter
        /// actually planted on. Monotone is required of the prediction (it is a property of the
        /// deciders) and agreement is required of the pair (it is the proof the deciders are the ones
        /// that ran).</para>
        /// </summary>
        [Test]
        public void TheEdgeBand_ThinsTheField_MonotonicallyTowardTheBoundary()
        {
            var cells = Candidates();
            Assert.Greater(cells.Count, 0, "sanity: the grid found no plantable ground at all");

            const int buckets = 6;             // five inside the band, one for the interior
            var predicted = new float[buckets];
            var accepted = new int[buckets];
            var total = new int[buckets];

            foreach (var c in cells)
            {
                // t = 0 at the boundary, 1 at the inner edge of the band, clamped for the interior.
                float t = Mathf.Clamp01(c.EdgeDistance / Mathf.Max(0.01f, c.BandWidth));
                int b = Mathf.Min(buckets - 1, Mathf.FloorToInt(t * (buckets - 1)));
                predicted[b] += c.Chance;
                total[b]++;
                if (c.Roll <= c.Chance) accepted[b]++;
            }

            var report = new List<string>();
            for (int b = 0; b < buckets; b++)
                report.Add(total[b] == 0
                    ? $"[{b}] empty"
                    : $"[{b}] n={total[b]} predicted {predicted[b] / total[b]:P1} " +
                      $"measured {accepted[b] / (float)total[b]:P1}");
            string summary = string.Join(" · ", report);
            Debug.Log($"[EdgeBand] density by distance-into-the-band (0 = boundary): {summary}");

            // Every bucket has to be POPULATED, or the "gradient" is two buckets and a cliff.
            for (int b = 0; b < buckets; b++)
                Assert.Greater(total[b], 200,
                    $"bucket {b} holds only {total[b]} cells, so the band is not resolved across its " +
                    $"own width and this test cannot see a gradient in it. {summary}");

            // MONOTONE, on the deciders themselves.
            for (int b = 1; b < buckets; b++)
            {
                float lo = predicted[b - 1] / total[b - 1];
                float hi = predicted[b] / total[b];
                Assert.GreaterOrEqual(hi, lo - 1e-4f,
                    $"the meadow gets THINNER as it moves inland between buckets {b - 1} and {b} " +
                    $"({lo:P1} → {hi:P1}). The edge band's falloff has inverted — check the sign of " +
                    $"StPetersGrass.EdgeDistanceMetres. {summary}");
            }

            // And it has to be a real ramp, not a rounding error.
            float hem = predicted[0] / total[0];
            float interior = predicted[buckets - 1] / total[buckets - 1];
            Assert.Less(hem, interior * 0.9f,
                $"the ground nearest the boundary is {hem:P1} dense against the interior's " +
                $"{interior:P1} — the falloff is barely firing, so the field still ends in very nearly " +
                $"the hard line it did before. {summary}");

            // ⭐⭐ AND THE TWO FLOORS HAVE TO BE THE TWO FLOORS. This is the assertion that pins the
            // 2026-08-26 correction rather than merely "something thins somewhere": a CUT edge (mow
            // line, tread, wall) must still be nearly full density at the line, and a FIELD CONTOUR
            // must be markedly thinner. Ramping both to zero is what deleted the verge and cost the
            // meadow 5.7 points of coverage — see StPetersGrass's two-floors note.
            AssertFloorHolds(GroundAtTheBoundary(c => c.Clearance <= c.Contour),
                             StPetersGrass.ClearanceFalloffFloor, "a CUT edge (fence, tread, wall)");
            AssertFloorHolds(GroundAtTheBoundary(c => c.Contour < c.Clearance),
                             StPetersGrass.FieldFalloffFloor, "a FIELD contour (swathe, grass floor)");

            // The prediction and the island must be the same island. Bucket-wise, with a tolerance for
            // the discreteness of a per-cell hash.
            for (int b = 0; b < buckets; b++)
            {
                float p = predicted[b] / total[b];
                float m = accepted[b] / (float)total[b];
                Assert.That(m, Is.EqualTo(p).Within(0.05f),
                    $"bucket {b}: the planter's own deciders predict {p:P1} of cells carry grass and " +
                    $"the walk measures {m:P1}. Prediction and reality move together when a knob is " +
                    $"tuned; a divergence means the scatter is NOT running the deciders this test " +
                    $"reads. {summary}");
            }
        }

        /// <summary>The cells sitting in the OUTERMOST fifth of the band — the ground actually at the
        /// boundary — whose nearest edge is of the kind <paramref name="ofKind"/> picks out.</summary>
        List<Cell> GroundAtTheBoundary(System.Func<Cell, bool> ofKind) =>
            Candidates()
                .Where(c => c.EdgeDistance < Mathf.Max(0.01f, c.BandWidth) * 0.2f)
                .Where(ofKind)
                .ToList();

        /// <summary>
        /// The ground at one kind of boundary must be thinned to about that kind's FLOOR — not to
        /// zero, and not left alone.
        ///
        /// <para>The bar is the floor times the density the same ground would carry with no band at
        /// all, so it moves when <c>ChanceOpen</c> or the swathe gate moves and needs no re-pinning.
        /// The window is generous (±0.18) because these cells are within a fifth of the band's inner
        /// edge, not exactly ON the boundary, so their falloff sits a little above the floor by
        /// construction.</para>
        /// </summary>
        static void AssertFloorHolds(List<Cell> at, float floor, string what)
        {
            Assert.Greater(at.Count, 100,
                $"only {at.Count} cells sit at {what}, which is too few to measure a floor against.");

            float measured = at.Average(c => c.Chance);
            float unbanded = at.Average(c => c.LegacyChance);
            float ratio = measured / Mathf.Max(1e-4f, unbanded);

            Debug.Log($"[EdgeBand] at {what}: {at.Count} cells, density {measured:P1} against an " +
                      $"unbanded {unbanded:P1} — ×{ratio:F2}, floor {floor:F2}");

            Assert.That(ratio, Is.EqualTo(floor).Within(0.18f),
                $"ground at {what} carries ×{ratio:F2} of its unbanded density, where the floor for " +
                $"that kind of edge is {floor:F2}. " +
                (ratio < floor
                    ? "Thinning a CUT edge toward nothing digs a bald moat round every fence and " +
                      "doorstep, and it is what deleted the trodden VERGE — a habitat that only " +
                      "exists in the 1.2 m ribbon beside a path."
                    : "This edge is barely being softened at all, which is the hard line the pass " +
                      "exists to remove."));
        }

        // =====================================================================================
        //  2. the interior is untouched — not "about right", EXACTLY reproduced
        // =====================================================================================

        /// <summary>
        /// 🔴 <b>"Coverage itself is ruled good — do not retune the field-interior density."</b>
        ///
        /// <para>So this reproduces the PRE-BAND accept decision — <c>InStand ? ChanceWoods :
        /// ChanceOpen</c>, no falloff, no ring — and requires it to be reproduced exactly on every cell
        /// the new deciders leave alone. A cell is left alone when two things are true at once: the
        /// edge falloff is exactly 1 (the site is deeper into the field than the band reaches) and the
        /// stand ring is unanimous (the ground around it is all wood or all open, so the blend has
        /// nothing to blend). That is the honest statement of the guarantee — the pass changes
        /// TRANSITIONS and nothing else — and it is stronger than any tolerance on a total.</para>
        ///
        /// <para>The second assertion is the one that stops the first being vacuous: the untouched set
        /// has to be most of the island. A pass that softened every cell a little would satisfy "the
        /// cells I did not touch are unchanged" trivially.</para>
        /// </summary>
        [Test]
        public void TheInterior_IsUntouched_CellForCell()
        {
            var terrain = Terrain();
            var cells = Candidates();

            int untouched = 0, changed = 0;
            foreach (var c in cells)
            {
                float wood = StPetersGrass.StandFraction(terrain, c.P, terrain.ElevationAt(c.P));
                bool unanimous = wood <= 0f || wood >= 1f;
                if (c.Falloff < 1f || !unanimous) { changed++; continue; }

                untouched++;
                Assert.AreEqual(c.LegacyChance, c.Chance, 1e-5f,
                    $"the cell at {c.P} is {c.EdgeDistance:F2} m inside the field (band " +
                    $"{c.BandWidth:F2} m) with a unanimous stand ring, so the edge pass must not have " +
                    $"touched it — but its accept chance moved from {c.LegacyChance:F4} to " +
                    $"{c.Chance:F4}. The interior density was ruled GOOD; this pass is only allowed " +
                    "to change transitions.");
            }

            float share = untouched / (float)(untouched + changed);
            Debug.Log($"[EdgeBand] {untouched:N0} of {cells.Count:N0} candidate cells are untouched " +
                      $"({share:P1}); {changed:N0} sit in a transition.");

            Assert.Greater(share, 0.5f,
                $"only {share:P0} of the meadow's cells are outside every transition. The band or the " +
                "stand ring is reaching too far into the field — the interior the owner ratified is " +
                "supposed to be most of the island, and 'the cells I did not touch are unchanged' is " +
                "not a claim worth making about a minority of them.");
        }

        // =====================================================================================
        //  3. the band MEANDERS — it is not a stripe
        // =====================================================================================

        /// <summary>
        /// <b>⚠ A falloff of constant width is its own artefact.</b> Ramping over a fixed distance draws
        /// a perfectly parallel border inside every boundary on the island, and the eye reads a
        /// constant-width gradient as painted trim rather than as an absence of trim. The brief called
        /// it out by name: <i>"a straight falloff stripe is as artificial as the hard line"</i>.
        ///
        /// <para>Pinned two ways: the width genuinely varies across the island, and it varies at a
        /// FINE ENOUGH grain to wander along one building's clearing rather than giving each building
        /// its own constant width — which would be the same stripe one scale up.</para>
        /// </summary>
        [Test]
        public void TheBandWidth_Meanders_RatherThanDrawingAParallelStripe()
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var c in Candidates())
            {
                if (c.BandWidth < min) min = c.BandWidth;
                if (c.BandWidth > max) max = c.BandWidth;
            }

            Debug.Log($"[EdgeBand] band width {min:F2}–{max:F2} m " +
                      $"(nominal {StPetersGrass.EdgeBandMetres} m ±{StPetersGrass.EdgeBandJitter:P0}, " +
                      $"noise scale {StPetersGrass.EdgeBandNoiseScale} m)");

            // It has to swing most of the way through the jitter it declares, or the noise is being
            // sampled somewhere it barely varies.
            float declared = StPetersGrass.EdgeBandMetres * StPetersGrass.EdgeBandJitter * 2f;
            Assert.Greater(max - min, declared * 0.7f,
                $"the band width only spans {max - min:F2} m of the {declared:F2} m its jitter " +
                "declares. The meander is nearly flat, so the falloff is drawing a parallel stripe " +
                "inside every boundary — which is the artefact it was supposed to replace.");

            // And the grain has to be finer than a building's clearing, or each building gets ONE
            // width and the stripe is back at the scale of a house.
            Assert.Less(StPetersGrass.EdgeBandNoiseScale, StPetersGrass.BuildingClearanceMetres * 2f,
                $"the band-width meander has a {StPetersGrass.EdgeBandNoiseScale} m feature size " +
                $"against a {StPetersGrass.BuildingClearanceMetres} m building clearing — it cannot " +
                "wander along one building's edge, so that building's band is a constant width.");

            // The whole range must stay inside the ruled 2.5–4 m the owner's brief allowed, with the
            // usual quarter-metre of slack for the endpoints.
            Assert.GreaterOrEqual(min, 2.0f,
                $"the band narrows to {min:F2} m — under two grid steps, which is too few cells to " +
                "read as a gradient rather than as a sparse row or two.");
            Assert.LessOrEqual(max, 4.5f,
                $"the band widens to {max:F2} m, past the ~4 m the brief ruled. A band this deep " +
                "stops being a transition and starts eating the field the owner ratified.");
        }

        // =====================================================================================
        //  4. the height classes step DOWN across the band
        // =====================================================================================

        /// <summary>
        /// <b>A field that thins toward its edge but stays knee-high to the last blade still ends in a
        /// line — just a dotted one.</b> So the same distance steps the ART down a height class: the
        /// interior wears whatever its habitat has, the outer band drops the tall blades, the last
        /// metre is short.
        ///
        /// <para><b>⚠ Measured against the COMMITTED manifest, not asserted.</b> Which classes a
        /// habitat has baked is a property of the grass library, and the whole point of the ally
        /// machinery is that a thin pool borrows rather than going bald. So this checks the property
        /// that actually matters — <i>the hem is never taller than the interior</i> — habitat by
        /// habitat, and reports what it found.</para>
        /// </summary>
        [Test]
        public void TheArt_StepsDownAHeightClass_TowardTheHem()
        {
            var library = GrassLibraryCatalog.Load();
            var imported = GrassLibraryCatalog.Imported(library);
            if (imported.Count == 0)
                Assert.Ignore("No grass sprite in the library is imported in this checkout (LFS).");

            var chooser = new StPetersWoodsPlanter.GrassArtChooser(imported);
            var report = new List<string>();

            foreach (string habitat in StPetersGrassField.HabitatIds)
            {
                var interior = chooser.ForTier(habitat, GrassTier.Interior);
                var band = chooser.ForTier(habitat, GrassTier.Band);
                var hem = chooser.ForTier(habitat, GrassTier.Hem);

                report.Add($"{habitat}: interior {interior.Count} (max {Tallest(interior)}) · " +
                           $"band {band.Count} (max {Tallest(band)}) · hem {hem.Count} (max {Tallest(hem)})");

                // Nothing may be EMPTY. An empty pool draws no tuft at all, which is a bald hem — a
                // far worse artefact than a slightly tall one, and one the owner would have to
                // diagnose from a screenshot.
                Assert.Greater(hem.Count, 0, $"'{habitat}' resolves NO art in the hem — the last metre " +
                                             "of the field round that ground would be bare.");
                Assert.Greater(band.Count, 0, $"'{habitat}' resolves NO art in the edge band.");

                // The step-down itself: never TALLER as you approach the boundary.
                Assert.LessOrEqual(Tallest(hem), Tallest(band),
                    $"'{habitat}' wears taller art in the hem than in the band — the step-down is " +
                    $"inverted. {string.Join(" | ", report)}");
                Assert.LessOrEqual(Tallest(band), Tallest(interior),
                    $"'{habitat}' wears taller art in the band than in the interior. " +
                    $"{string.Join(" | ", report)}");

                // ⚠ And the variety floor still binds at every tier. If this fails the answer is a
                // BAKE, not a smaller floor — the same ruling the 2026-08-06 variety pass made.
                foreach (var (tier, pool) in new[]
                         {
                             (GrassTier.Band, band), (GrassTier.Hem, hem),
                         })
                    Assert.GreaterOrEqual(pool.Count, StPetersGrass.MinHabitatVariety,
                        $"'{habitat}' resolves only {pool.Count} variants at the {tier} tier, under " +
                        $"the {StPetersGrass.MinHabitatVariety} a pool needs before it reads as a " +
                        "field rather than a repeating pattern — even after borrowing from its " +
                        "allies. BAKE MORE ART, DO NOT LOWER THE FLOOR: retag a short variant for " +
                        "this habitat in docs/art/rigs/grassSpeciesRig.js (a manifest line, no new " +
                        "pixels) or draw one. Lowering MinHabitatVariety would hide this everywhere " +
                        "at once.");
            }

            Debug.Log($"[EdgeBand] art pools by tier — {string.Join(" | ", report)}");

            // At least ONE habitat has to actually step down, or the whole mechanism is inert on this
            // library and the hard line is unchanged where the tall blades are.
            Assert.IsTrue(
                StPetersGrassField.HabitatIds.Any(
                    h => Tallest(chooser.ForTier(h, GrassTier.Hem))
                         < Tallest(chooser.ForTier(h, GrassTier.Interior))),
                "no habitat on the island wears shorter art at its hem than in its interior, so the " +
                "height step-down is doing nothing at all. Check GrassArtChooser.ClassesFor against " +
                "the height classes the manifest actually declares.");
        }

        /// <summary>The tallest height class in a pool, as an index into
        /// <see cref="GrassLibraryCatalog.HeightClasses"/> (which is ordered shortest-first). −1 for an
        /// empty pool.</summary>
        static int Tallest(List<GrassLibraryCatalog.Entry> pool) =>
            pool.Count == 0
                ? -1
                : pool.Max(e => System.Array.FindIndex(
                      GrassLibraryCatalog.HeightClasses,
                      c => string.Equals(c, e.HeightClass, System.StringComparison.OrdinalIgnoreCase)));

        // =====================================================================================
        //  5. the tier survives the round trip into the field's slot byte
        // =====================================================================================

        /// <summary>
        /// The tier is stored in two spare bits of the slot byte, so it has to come back out as itself
        /// alongside the habitat id and the broad flag — and, critically, a field baked BEFORE those
        /// bits meant anything must still decode as <see cref="GrassTier.Interior"/> and draw the
        /// meadow it always drew.
        /// </summary>
        [Test]
        public void TheSlotByte_CarriesTheTier_WithoutDisturbingWhatWasThere()
        {
            foreach (GrassTier tier in new[] { GrassTier.Interior, GrassTier.Band, GrassTier.Hem })
            foreach (bool broad in new[] { false, true })
            for (int id = 1; id <= GrassFieldScatter.MaxHabitats; id++)
            {
                byte slot = GrassFieldScatter.PackSlot(id, broad, tier);
                Assert.AreEqual(id, GrassFieldScatter.HabitatOf(slot), $"habitat id lost ({tier}, {broad})");
                Assert.AreEqual(broad, GrassFieldScatter.BroadOf(slot), $"broad flag lost ({tier}, id {id})");
                Assert.AreEqual(tier, GrassFieldScatter.TierOf(slot), $"tier lost (id {id}, broad {broad})");
                Assert.IsFalse(GrassFieldScatter.IsEmpty(slot));
            }

            // Empty still wins over every flag: an empty site has no habitat, so it has no tier either.
            Assert.AreEqual(GrassFieldScatter.EmptySlot,
                            GrassFieldScatter.PackSlot(0, true, GrassTier.Hem));

            // 🔴 BACKWARD COMPATIBILITY. Every field on disk before 2026-08-26 was packed by the
            // two-argument call with zeros in bits 5..6. It must decode as Interior — the full pool —
            // or a scene nobody re-baked quietly repaints itself with hem art.
            byte legacy = GrassFieldScatter.PackSlot(3, true);
            Assert.AreEqual(GrassTier.Interior, GrassFieldScatter.TierOf(legacy),
                "a slot byte packed the way every shipped field was packed must decode as Interior.");
            Assert.AreEqual(3, GrassFieldScatter.HabitatOf(legacy));
            Assert.IsTrue(GrassFieldScatter.BroadOf(legacy));
        }

        // =====================================================================================
        //  6. the scatter actually carries the tier out to the bake
        // =====================================================================================

        /// <summary>
        /// The mechanism has to be LIVE on the island, not merely correct in isolation: the scatter must
        /// stamp a tier on every site, all three tiers must appear, and the hem must be a minority —
        /// a hem that is most of the island means the band is swallowing the field.
        /// </summary>
        [Test]
        public void TheScatter_StampsEveryTuftWithItsTier_AndAllThreeAppear()
        {
            var sites = StPetersGrass.Scatter(Terrain());
            Assert.Greater(sites.Count, 0, "sanity: the meadow scattered nothing");

            var counts = new Dictionary<GrassTier, int>();
            foreach (var s in sites)
                counts[s.Tier] = counts.TryGetValue(s.Tier, out int n) ? n + 1 : 1;

            string summary = string.Join(", ",
                new[] { GrassTier.Interior, GrassTier.Band, GrassTier.Hem }
                    .Select(t => $"{t} {(counts.TryGetValue(t, out int n) ? n : 0)}"));
            Debug.Log($"[EdgeBand] {sites.Count} tufts by tier — {summary}");

            foreach (GrassTier tier in new[] { GrassTier.Interior, GrassTier.Band, GrassTier.Hem })
                Assert.IsTrue(counts.ContainsKey(tier) && counts[tier] > 0,
                    $"no tuft on the island is in the {tier} tier. The band is inert there and the " +
                    $"field still ends the way it did. Got: {summary}.");

            Assert.Greater(counts[GrassTier.Interior], sites.Count / 2,
                $"under half the meadow is interior — the edge band has grown into the field rather " +
                $"than hemming it. Got: {summary}.");
        }
    }
}
