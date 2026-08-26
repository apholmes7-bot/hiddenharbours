using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The 2026-08-01 decoration pass — the grass layer and the interior erratics (owner: "decorate the
    /// island with our grass rigs, add more trees, add rocks"). Everything here asserts the PURE deciders
    /// (<see cref="StPetersGrass"/>, <see cref="StPetersShoreMap.ScatterFieldRocks"/>), the same
    /// instrument <c>StPetersWoodsTests</c> uses: the scene is a build artifact, the decider is the truth.
    ///
    /// <para>The tree densification rides the existing <c>StPetersWoodsTests</c> pins — count budget
    /// (40–500), wooded fraction (&lt; 0.7), mosaic sabotage — which is exactly what those pins are FOR:
    /// "more trees" had to fit inside the reverting-island thesis or fail loudly here.</para>
    /// </summary>
    public class StPetersDecorTests
    {
        private GameObject _go;
        private TidalTerrain _terrain;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("StPetersTerrain_DecorTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // =====================================================================================
        //  GRASS
        // =====================================================================================

        [Test]
        public void TheGrassIsDeterministic_NoRng()
        {
            var a = StPetersGrass.Scatter(_terrain);
            var b = StPetersGrass.Scatter(_terrain);

            Assert.AreEqual(a.Count, b.Count, "two scatters must plant the same meadow");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Less((a[i].Position - b[i].Position).magnitude, 1e-6f, $"tuft {i} moved");
                Assert.AreEqual(a[i].Habitat, b[i].Habitat, $"tuft {i} changed habitat");
                Assert.AreEqual(a[i].Roll, b[i].Roll, $"tuft {i} changed its variant roll");
                Assert.AreEqual(a[i].Scale, b[i].Scale, 1e-6f, $"tuft {i} changed size");
                Assert.AreEqual(a[i].Tint, b[i].Tint, $"tuft {i} changed colour");
            }
        }

        /// <summary>
        /// The tuft count, <b>DERIVED from the planter's own knobs rather than re-pinned as a
        /// literal</b> (house law: bounds are derived, never widened).
        ///
        /// <para><b>Why the old literal band had to go, and why a wider one would be no better.</b>
        /// It read <c>400 &lt; n &lt; 1800</c>, measured at 593 on the pre-green-over island. The
        /// green-over took <see cref="StPetersGrass.GrassStep"/> 4.0 → 2.2 m and
        /// <see cref="StPetersGrass.SwatheThreshold"/> −0.15 → −0.62, and the count went to ~3,780 —
        /// so the pin failed for the one reason a pin must not: the island changed on purpose. Simply
        /// writing 3,780 in would buy exactly one re-tune before the same thing happened again.</para>
        ///
        /// <para><b>So it predicts instead.</b> Walk the same grid the planter walks, with the same
        /// pure deciders, and sum the EXPECTED tufts per cell:
        /// <c>Σ P(accept) × TuftsAt(cell)</c>. The count goes as the inverse square of
        /// <c>GrassStep</c> and linearly in the gates, and all of that falls out of the walk rather
        /// than being restated. Re-tune any knob and prediction and reality move together; break a
        /// GATE and only one of them moves, which is the failure this exists to catch.</para>
        ///
        /// <para>Verified against the offline port of the decider that reproduces
        /// <see cref="StPetersBuilder"/>'s own quoted beach elevations (radius+6 → 4.49 m, +10 → 2.50,
        /// +14 → 0.51, +16 → −0.27): predicted 3,800 against an actual 3,778, a <b>0.6%</b> error, so
        /// the tolerance below carries ~25× headroom. The residual is the sub-tuft re-gate — offsets
        /// of ±0.7 m that spill across a clearing edge and are dropped per BLADE.</para>
        /// </summary>
        [Test]
        public void TheSward_IsAMeadowsWorthOfTufts_NotACarpetAndNotAMange()
        {
            // Tolerance on the prediction. Covers the sub-tuft re-gate (measured 0.6%) with room for
            // the hash's discreteness after a re-tune; tight enough that a dead gate — which moves
            // the count by a factor, not a percent — still fails.
            const float tolerance = 0.15f;

            float expected = 0f;
            int cells = 0;

            float minX = StPetersBuilder.IslandCenter.x - StPetersBuilder.IslandRadius;
            float maxX = StPetersBuilder.IslandCenter.x + StPetersBuilder.IslandRadius;
            float minY = StPetersBuilder.IslandCenter.y - StPetersBuilder.IslandRadiusY;
            float maxY = StPetersBuilder.IslandCenter.y + StPetersBuilder.IslandRadiusY;
            int nx = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / StPetersGrass.GrassStep));
            int ny = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / StPetersGrass.GrassStep));

            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                var p = new Vector2(
                    minX + (ix + 0.5f) * StPetersGrass.GrassStep
                         + (StPetersShoreMap.Hash01(ix, iy, 163) * 2f - 1f) * StPetersGrass.GrassJitter,
                    minY + (iy + 0.5f) * StPetersGrass.GrassStep
                         + (StPetersShoreMap.Hash01(ix, iy, 167) * 2f - 1f) * StPetersGrass.GrassJitter);

                // ⚠ The MEADOW's gate, not the trees'. The 2026-08-05 retune gave grass its own
                // clearings (StPetersGrass.IsPlantableMeadow) — the prediction has to walk the gate the
                // scatter actually walks or it predicts a different island.
                if (!StPetersGrass.IsPlantableMeadow(_terrain, p)) continue;
                cells++;
                if (!StPetersGrass.InSwathe(p)) continue;

                // ⭐ AND THE EDGE BAND, since 2026-08-26. The accept chance is no longer a two-valued
                // step (open vs under-canopy): it is the ground's own chance, blended across the woods'
                // edge, RAMPED DOWN as the field runs out. The prediction reads the same two deciders
                // the scatter reads — restating either of them here is how a pin starts describing a
                // different island from the one that ships.
                float chance = StPetersGrass.ChanceAt(_terrain, p, _terrain.ElevationAt(p))
                             * StPetersGrass.EdgeFalloff(_terrain, p);
                expected += chance * StPetersGrass.TuftsAt(p, StPetersShoreMap.Hash01(ix, iy, 179));
            }

            Assert.Greater(cells, 0, "sanity: the grid found no plantable ground at all");
            Assert.Greater(expected, 0f, "sanity: every cell was gated out before the count was predicted");

            int actual = StPetersGrass.Scatter(_terrain).Count;
            float ratio = actual / expected;

            Assert.That(ratio, Is.EqualTo(1f).Within(tolerance),
                $"the meadow planted {actual} tufts where its own knobs predict {expected:F0} " +
                $"({ratio:P0} of prediction). Prediction and reality move together when a knob is " +
                "TUNED, so a divergence this size means a GATE changed behaviour — check the swathe " +
                "gate, the stand/open chance split, and the per-blade re-gate on the sub-tuft offsets.");

            // The structural ceiling, also derived: TuftsAt is capped at 2 (it was 3 before the grid
            // halved), so nothing can plant more than two per candidate cell however the knobs move.
            // A count above this is not a tuning result, it is the grid walk itself being wrong.
            Assert.LessOrEqual(actual, cells * StPetersGrass.MaxTuftsPerCell,
                $"{actual} tufts from {cells} candidate cells is more than the " +
                $"{StPetersGrass.MaxTuftsPerCell}-per-cell cap allows.");
        }

        /// <summary>
        /// What the meadow actually keeps clear, per BLADE — the sub-tuft offsets re-pass the gate, and
        /// this is the pin for that.
        ///
        /// <para><b>⚠ RE-DERIVED at the 2026-08-05 retune, and the list got SHORTER on purpose.</b> It
        /// used to assert the TREES' clearings, because grass borrowed them: a 44 m village disc, a
        /// 40 m crossing sightline, a spawn disc. Every one of those is a reason about things that
        /// stand two storeys high — you must be able to see the bar over the treetops, the first thing
        /// you look at must not be a trunk. Grass is ankle-deep, and at the density the owner asked for
        /// those discs would have left roughly a third of the island bald with a hard edge round each
        /// one. So the claims below are the meadow's own (<see cref="StPetersGrass.IsPlantableMeadow"/>)
        /// and they are the ones that survive the argument: not through a building, not on the wharf,
        /// not down the middle of a walked path.</para>
        /// </summary>
        [Test]
        public void EveryTuft_StandsOnTheGrassBand_AndOutOfTheMeadowsOwnClearings()
        {
            foreach (var s in StPetersGrass.Scatter(_terrain))
            {
                Assert.GreaterOrEqual(_terrain.ElevationAt(s.Position),
                    StPetersShoreMap.GrassFloorElevation - 1e-3f,
                    $"a tuft at {s.Position} sits below the grass band");

                // Each site's OWN radius since 2026-08-19 — the cannery needs 9.06 m where a cottage
                // needs the 7 m floor. Asserting the floor for all of them would pass a tuft standing
                // two metres inside the cannery.
                foreach (var k in StPetersGrass.BuildingKeepouts)
                    Assert.GreaterOrEqual(Vector2.Distance(s.Position, k.Position), k.RadiusMetres,
                        $"a tuft at {s.Position} is inside {k.What} at {k.Position} " +
                        $"({k.RadiusMetres:F2} m of clearing)");

                Assert.GreaterOrEqual(Vector2.Distance(s.Position, StPetersBuilder.DockZonePos),
                    StPetersWoods.DockClearance, "a tuft is on the dock");
                Assert.GreaterOrEqual(StPetersShoreMap.DistanceToSegment(s.Position,
                        StPetersBuilder.BerthFrom, StPetersBuilder.BerthTo),
                    StPetersWoods.DockClearance, "a tuft is in the berth");

                Assert.GreaterOrEqual(StPetersGrass.DistanceToWalkedPath(s.Position),
                    StPetersGrass.PathBareHalfWidthMetres,
                    $"a tuft at {s.Position} is growing down the middle of a walked path — the tread " +
                    "is what makes a path read as one, and at this coverage the meadow would swallow " +
                    "it whole");
            }
        }

        /// <summary>
        /// 🔴 <b>The building clearance, derived from the buildings.</b> The meadow keeps grass off a
        /// building's site by a flat radius, because <see cref="StPetersGrass.IsPlantableMeadow"/> is
        /// called tens of thousands of times a build and must not read a JSON contract to answer. That
        /// makes this the place the radius is checked against the contract instead — the same shape
        /// <c>StPetersVillageTests</c> uses for the tree clearing.
        ///
        /// <para>The buildings pivot at their footprint CENTRE and the owner may re-face one, so the
        /// number that has to be covered is the largest HALF-DIAGONAL, not the largest width.</para>
        /// </summary>
        /// <summary>
        /// 🔴 <b>EVERY PLACED BUILDING HAS A CLEARING, and this is the test that did not exist when the
        /// post office was placed without one.</b>
        ///
        /// <para><c>StPetersGrass.BuildingSites</c> is a hand-maintained list, and a site missing from it
        /// fails in total silence: the meadow grows through the building's ground, and because a room's
        /// floor sorts at <c>ShopCatalog.RoomSortingOrder</c> (1) — BELOW the Y-sort band the tufts live
        /// in — the grass draws OVER the floor. From outside the building is perfect. It took rendering
        /// the interior reveal and finding the post office had vanished into the meadow to see it.</para>
        ///
        /// <para>So the list is checked against the two things that actually place buildings on this
        /// island, rather than against itself.</para>
        /// </summary>
        [Test]
        public void EveryPlacedBuilding_HasAGrassClearing()
        {
            foreach (var house in StPetersVillage.Sites)
                AssertCleared(house.Key, house.Position);

            foreach (var shop in StPetersShops.Sites)
                AssertCleared(shop.Key, shop.Position);

            // Aunt Ginny's plot, out in the eastern woods (2026-08-16). Her cottage IS a kit entry now,
            // but it is not in StPetersVillage.Sites — it is placed by its own file — so it would be
            // missed by both loops above. Her sheds are greybox markers with no contract at all, which
            // makes them the single most forgettable buildings on the island: exactly the shape of the
            // post-office bug this test exists for.
            AssertCleared(StPetersGinnyPlot.CottageKey, StPetersGinnyPlot.CottagePos);
            foreach (var shed in StPetersGinnyPlot.Sheds)
                AssertCleared(shed.Key, shed.Position);

            // The derelict cannery out by the pier (2026-08-19) — placed by its own file, like Ginny's
            // cottage, so both loops above miss it for the same reason.
            AssertCleared(StPetersCannery.BuildKey, StPetersCannery.Site);

            void AssertCleared(string key, Vector2 at)
            {
                bool cleared = false;
                foreach (var site in StPetersGrass.BuildingSites)
                    if (Vector2.Distance(site, at) < 0.01f) { cleared = true; break; }

                Assert.IsTrue(cleared,
                    $"'{key}' stands at {at} and StPetersGrass.BuildingSites has no clearing for it, so " +
                    "the meadow grows through its ground — and OVER its floor once you are inside. Add " +
                    "its site constant to that list.");
            }
        }

        /// <summary>
        /// <b>Every building's clearing covers ITS OWN footprint</b>, so a quarter-turned building never
        /// has grass growing through its corner.
        ///
        /// <para>⚠️ This used to assert one global number against the BIGGEST footprint the island
        /// places, which was right while every building was a house-sized box. The derelict cannery
        /// (9.06 m of half-diagonal against a 7 m constant) broke that: satisfying it globally would have
        /// given every cottage on the green two more metres of bald dirt for the sake of one building
        /// 170 m away. <c>StPetersGrass.MeadowKeepout</c> is per site now, and this is the per-site
        /// form of the same guarantee — strictly stronger, because the old one could not have caught a
        /// big building paired with a small clearing.</para>
        ///
        /// <para>The footprints are read from the two CONTRACTS, not from the grass file, so this still
        /// checks the list against something other than itself.</para>
        /// </summary>
        [Test]
        public void EveryBuildingsClearing_CoversItsOwnFootprint()
        {
            var placements = HiddenHarbours.Art.Editor.VillageBuildingCatalog.Scan();
            if (placements == null || placements.Count == 0)
                Assert.Ignore("The village building kit is not on disk in this checkout.");

            int checked_ = 0;

            // The village kit's own sites, plus the three placed by their own files.
            foreach (var house in StPetersVillage.Sites)
                checked_ += CheckKit(house.Key, house.Position);

            checked_ += CheckKit(StPetersGinnyPlot.CottageKey, StPetersGinnyPlot.CottagePos);
            foreach (var shed in StPetersGinnyPlot.Sheds)
                checked_ += CheckKit(shed.BuildKey, shed.Position);
            checked_ += CheckKit(StPetersCannery.BuildKey, StPetersCannery.Site);

            // …and the shops, which are a DIFFERENT KIT and were the bigger buildings until the cannery.
            foreach (var shop in StPetersShops.Sites)
            {
                var shell = HiddenHarbours.Art.Editor.ShopCatalog.FindShell(shop.Key);
                if (!shell.IsValid) continue;
                checked_ += Check(shop.Key, shop.Position,
                                  HiddenHarbours.Art.Editor.ShopCatalog.FootprintRadiusMetres(shell));
            }

            Assert.Greater(checked_, 4,
                "fewer than five buildings were actually measured — this test would be passing " +
                "vacuously on a tree with no baked art");

            // Nothing may drop BELOW the floor either: shrinking a small building's clearing to its own
            // footprint would be a visible change to the village that nobody asked for.
            foreach (var k in StPetersGrass.BuildingKeepouts)
                Assert.GreaterOrEqual(k.RadiusMetres, StPetersGrass.BuildingClearanceMetres,
                    $"{k.What} clears only {k.RadiusMetres:F2} m, under the " +
                    $"{StPetersGrass.BuildingClearanceMetres} m floor");

            int CheckKit(string buildKey, Vector2 at)
            {
                var placement = HiddenHarbours.Art.Editor.VillageBuildingCatalog.Find(buildKey);
                if (!placement.IsValid) return 0;
                return Check(buildKey, at,
                             StPetersVillage.FootprintRadiusMetres(placement));
            }

            int Check(string what, Vector2 at, float halfDiagonal)
            {
                foreach (var k in StPetersGrass.BuildingKeepouts)
                {
                    if (Vector2.Distance(k.Position, at) >= 0.01f) continue;

                    Assert.GreaterOrEqual(k.RadiusMetres, halfDiagonal,
                        $"'{what}' at {at} needs {halfDiagonal:F2} m of half-diagonal but its meadow " +
                        $"clearing is only {k.RadiusMetres:F2} m — a quarter-turned building would have " +
                        "grass growing through its corner. Widen the clearing; do not shrink the " +
                        "building.");
                    return 1;
                }

                Assert.Fail($"'{what}' stands at {at} with no meadow keepout at all — see " +
                            "EveryPlacedBuilding_HasAGrassClearing.");
                return 0;
            }
        }

        /// <summary>
        /// The worn ground, <b>tied to <see cref="StPetersGrass.SwatheThreshold"/> rather than to a
        /// literal coverage band</b>.
        ///
        /// <para><b>Why the literal failed.</b> It pinned <c>0.30 &lt; f &lt; 0.85</c>. The
        /// green-over moved the threshold −0.15 → −0.62 and coverage went to 0.887 — again, the
        /// island changing on purpose rather than a regression. And the band was never really about
        /// the number: it was two claims, "the gate is consulted on the meadow" and "the gate rejects
        /// something". Both can be stated so they survive a re-tune.</para>
        ///
        /// <para><b>The derivation.</b> Sample the gate over the meadow, and over an UNCONSTRAINED
        /// grid across the same region. The meadow's pass rate must match the field's own — that is
        /// what "the meadow sees the same gate as everywhere else" means, and it holds at any
        /// threshold. Then require the field to reject something at all, which is what stops the gate
        /// being switched off by sliding the threshold to −1.</para>
        ///
        /// <para>Measured with the offline port at BOTH thresholds, which is the point: at −0.62 the
        /// meadow passes 88.7% against the field's 87.9% (Δ 0.007); at the old −0.15, 52.4% against
        /// 51.2% (Δ 0.012). The same assertion holds either side of the re-tune.</para>
        /// </summary>
        [Test]
        public void TheSwardHasWornGround_TheSwatheFieldActuallyGates()
        {
            // How far the meadow's pass rate may sit from the field's. The two measured 0.007 apart
            // at the shipped threshold and 0.012 at the previous one; 0.10 absorbs the meadow being
            // a biased sub-sample of the region without absorbing a bypassed gate.
            const float agreement = 0.10f;

            // The field must reject at least this much for the gate to be doing anything. A
            // threshold at the bottom of the field's own [−1, 1] range rejects nothing, which is a
            // gate in name only.
            const float minRejected = 0.02f;

            int meadow = 0, meadowPass = 0, field = 0, fieldPass = 0;
            for (float x = -50f; x <= 190f; x += 3f)
            for (float y = -68f; y <= 68f; y += 3f)
            {
                var p = new Vector2(x, y);

                field++;
                if (StPetersGrass.InSwathe(p)) fieldPass++;

                if (!StPetersGrass.IsPlantableMeadow(_terrain, p)) continue;
                if (StPetersWoods.InStand(p, _terrain.ElevationAt(p))) continue;
                meadow++;
                if (StPetersGrass.InSwathe(p)) meadowPass++;
            }

            Assert.Greater(meadow, 200, "sanity: the sweep found a meadow to measure");

            float fMeadow = (float)meadowPass / meadow;
            float fField = (float)fieldPass / field;

            Assert.That(fMeadow, Is.EqualTo(fField).Within(agreement),
                $"the meadow carries grass on {fMeadow:P1} of its ground but the swathe field passes " +
                $"{fField:P1} of the region — the meadow is not seeing the same gate everywhere else " +
                "sees, so something is bypassing or double-applying it.");

            Assert.Greater(1f - fField, minRejected,
                $"the swathe gate rejects only {1f - fField:P1} of the region at " +
                $"SwatheThreshold {StPetersGrass.SwatheThreshold} — there is no worn ground left, and " +
                "a gate that refuses nothing is not a gate.");
        }

        [Test]
        public void UnderTheWoods_TheGrassThins()
        {
            var sites = StPetersGrass.Scatter(_terrain);
            int inWoods = sites.Count(s => StPetersWoods.InStand(s.Position,
                                                                _terrain.ElevationAt(s.Position)));
            float f = (float)inWoods / Mathf.Max(1, sites.Count);
            Assert.Less(f, 0.25f,
                $"{f:P0} of the sward stands under a canopy — the shade gate (ChanceWoods) is dead");
        }

        [Test]
        public void EveryTuft_IsAValidVariantAtAValidScale()
        {
            foreach (var s in StPetersGrass.Scatter(_terrain))
            {
                // Site identity is now habitat-tag + roll (the planter resolves art from the grass
                // library); tag↔library validity is pinned by StPetersGreenOverTests.
                //
                // ⚠ Roll is NOT the old 0..2 sprite index. That index existed because there were
                // exactly three tuft sprites and the site named one of them; the library now holds 29
                // and the site does not know how many. Roll is a stable hash pick the planter reduces
                // with `Roll % choices.Count`, so its contract is only that it is NON-NEGATIVE — a
                // negative roll would make that modulo negative and index out of range. Pinning an
                // upper bound here would re-couple the site to a variant count it deliberately no
                // longer knows.
                Assert.GreaterOrEqual(s.Roll, 0,
                    "the variant roll is reduced with % against the library's size — a negative roll " +
                    "indexes out of range");
                Assert.That(string.IsNullOrEmpty(s.Habitat), Is.False, "a tuft with no habitat tag cannot resolve art");
                Assert.That(s.Scale, Is.InRange(StPetersGrass.ScaleMin, StPetersGrass.ScaleMax));
                Assert.That(s.Tint.a, Is.EqualTo(1f), "a translucent tuft is a bug, not a look");
            }
        }

        [Test]
        public void TheWeatherCoastBleaches_TheShelterStaysGreen()
        {
            // The tint gradient, at a fixed jitter so only EXPOSURE varies: the blasted south-east coast
            // must sit closer to straw (blue drops hardest) than the sheltered core.
            var exposed = new Vector2(StPetersBuilder.IslandCenter.x + 15f,
                                      StPetersBuilder.IslandCenter.y - StPetersBuilder.IslandRadiusY + 12f);
            var core = StPetersBuilder.IslandCenter;

            Assert.Greater(StPetersWoods.ExposureAt(exposed), StPetersWoods.ExposureAt(core),
                "sanity: the probe points must actually differ in exposure");
            Assert.Less(StPetersGrass.TintAt(exposed, 0.5f).b, StPetersGrass.TintAt(core, 0.5f).b,
                "the exposed coast's grass must bleach toward straw (blue falls) relative to the core");
        }

        // =====================================================================================
        //  FIELD ROCKS
        // =====================================================================================

        [Test]
        public void TheErraticsAreDeterministic_NoRng()
        {
            var a = StPetersShoreMap.ScatterFieldRocks(_terrain);
            var b = StPetersShoreMap.ScatterFieldRocks(_terrain);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Less((a[i].Position - b[i].Position).magnitude, 1e-6f, $"rock {i} moved");
                Assert.AreEqual(a[i].Sprite, b[i].Sprite, $"rock {i} changed sprite");
            }
        }

        [Test]
        public void ADozenOddErratics_NotAQuarryAndNotNone()
        {
            var rocks = StPetersShoreMap.ScatterFieldRocks(_terrain);
            Assert.Greater(rocks.Count, 4, "the field lost its stones — a gate is rejecting everything");
            Assert.LessOrEqual(rocks.Count, StPetersShoreMap.FieldRockAttempts,
                "more survivors than attempts — the scatter is double-placing");
            Assert.Less(rocks.Count, 30, "the meadow reads as a quarry — the gates are dead");
        }

        [Test]
        public void EveryErratic_KeepsTheClearings_AndTheOpenGround()
        {
            foreach (var r in StPetersShoreMap.ScatterFieldRocks(_terrain))
            {
                Assert.GreaterOrEqual(_terrain.ElevationAt(r.Position),
                    StPetersShoreMap.GrassFloorElevation - 1e-3f, "an erratic sits below the field");
                Assert.IsFalse(StPetersWoods.InStand(r.Position, _terrain.ElevationAt(r.Position)),
                    "an erratic hides under a closed canopy — invisible ground cost");

                Assert.Greater(Vector2.Distance(r.Position, StPetersBuilder.VillageHearthPos),
                    StPetersWoods.VillageClearingRadius, "an erratic is inside the village clearing");
                Assert.Greater(Vector2.Distance(r.Position, StPetersGinnyPlot.CottagePos),
                    StPetersGinnyPlot.ClearingRadius,
                    "an erratic sits inside Ginny's plot — a boulder through her shed is exactly as " +
                    "wrong as a spruce through the schoolhouse");
                Assert.Greater(Vector2.Distance(r.Position, StPetersBuilder.StartSpawnPos),
                    StPetersWoods.SpawnClearingRadius, "an erratic is on the spawn");
                Assert.Greater(StPetersShoreMap.DistanceToSegment(r.Position,
                        StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo),
                    StPetersWoods.CrossingClearance, "an erratic is on the crossing's approach");
                Assert.Greater(Vector2.Distance(r.Position, StPetersBuilder.DockZonePos),
                    StPetersWoods.DockClearance, "an erratic is on the dock");
            }
        }

        [Test]
        public void TheBarrensAreStonier_TheExposureBiasHolds()
        {
            var rocks = StPetersShoreMap.ScatterFieldRocks(_terrain);
            Assert.Greater(rocks.Count, 0, "sanity");
            int exposedRocks = rocks.Count(r =>
                StPetersWoods.ExposureAt(r.Position) >= StPetersShoreMap.FieldRockExposure);
            Assert.GreaterOrEqual((float)exposedRocks / rocks.Count, 0.5f,
                "most erratics must stand on exposed ground — the bias gate is dead");
        }

        [Test]
        public void EveryErratic_IsAKitBoulder()
        {
            var boulders = new[] { "bs", "bm", "bl" };
            foreach (var r in StPetersShoreMap.ScatterFieldRocks(_terrain))
                Assert.Contains(r.Sprite, boulders,
                    "field rocks are the kit's boulders — sea stacks and reef belong to the shore rings");
        }
    }
}
