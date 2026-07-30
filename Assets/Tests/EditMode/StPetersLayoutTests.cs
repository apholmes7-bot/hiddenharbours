using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ ST PETERS AT ITS RULED SIZE — 760 × 520 m of scene around a ~450 × 260 m island, with the
    /// sandbar leaving the WEST end and the dock on the EAST (docs/design/scene-sizing-and-world-scale.md
    /// §5.1, §5.1a, §7 items 1 and 3; owner-ruled 2026-07-23).
    ///
    /// <para>These are the assertions that make the numbers a LAYOUT rather than a pile of constants:
    /// the island is the size it was ruled, it sits on the correct side of the scene, the bar runs the
    /// other way, and — the one that would have caught a live bug years before anyone saw it — the boat
    /// actually floats where the builder moors her.</para>
    ///
    /// <para><see cref="StPetersTerrainTests"/> is the sibling: it asserts the tide BEHAVIOUR (bar and
    /// channel inverse over the swing, the neap gap). This asserts the GEOMETRY those behaviours happen
    /// on.</para>
    /// </summary>
    public class StPetersLayoutTests
    {
        private TidalTerrain _terrain;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TidalTerrain_LayoutTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            GameServices.Reset();
        }

        /// <summary>Water level at a given point in the swing, in metres above datum.</summary>
        private static float Water(float t) =>
            StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude * t;

        // =========================================================================================
        // 1. the ruled dimensions
        // =========================================================================================

        [Test]
        public void TheSceneIsTheRuled760x520_AndTheIslandTheRuled450x260()
        {
            Assert.AreEqual(760f, StPetersBuilder.RegionWorldSize.x, 0.01f, "scene width (§5.1)");
            Assert.AreEqual(520f, StPetersBuilder.RegionWorldSize.y, 0.01f, "scene height (§5.1)");

            Assert.AreEqual(450f, StPetersBuilder.IslandRadius * 2f, 0.01f,
                "island landmass along X — the ruled ~450 m, a ~1:5 compression of the real island");
            Assert.AreEqual(260f, StPetersBuilder.IslandRadiusY * 2f, 0.01f,
                "island landmass across Y — the ruled ~260 m");

            // The def the whole engine reads must carry it too (the #320 contract).
            var region = AssetDatabase().WorldSizeMeters;
            Assert.AreEqual(StPetersBuilder.RegionWorldSize.x, region.x, 0.01f,
                "the RegionDef must publish the builder's extent — the sea plane is scaled to it");
            Assert.AreEqual(StPetersBuilder.RegionWorldSize.y, region.y, 0.01f);

            Debug.Log($"[st-peters] {StPetersBuilder.RegionWorldSize.x} × " +
                      $"{StPetersBuilder.RegionWorldSize.y} m scene, " +
                      $"{StPetersBuilder.IslandRadius * 2f} × {StPetersBuilder.IslandRadiusY * 2f} m island, " +
                      $"seabed {AssetDatabase().SeabedTexels.x} × {AssetDatabase().SeabedTexels.y} texels " +
                      $"at {AssetDatabase().SeabedPixelsPerMetre} px/m, water bake " +
                      $"{StPetersBuilder.WaterHeightBakeResolution}² " +
                      $"({StPetersBuilder.RegionWorldSize.x / StPetersBuilder.WaterHeightBakeResolution:F2} m/texel).");
        }

        private static RegionDef AssetDatabase() =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<RegionDef>(
                "Assets/_Project/Data/Regions/StPeters.asset");

        /// <summary>
        /// Why <c>TidalTerrain</c> gained a Y radius at all.
        ///
        /// <para>The first reason is the plain one: the ruled landmass is <b>450 × 260 m</b>, and a disc
        /// is as wide as it is long, so a disc cannot be that shape at any radius. The second is what
        /// makes it more than a nicety — a 450 m disc in a 520 m scene leaves only 35 m of water off
        /// each long side, which the island's own 30 m beach eats almost entirely. There would be
        /// nowhere to put the reef shelf the same section rules in, let alone open water.</para>
        /// </summary>
        [Test]
        public void TheIslandIsAnEllipse_BecauseADiscCannotBeTheRuledShape()
        {
            Assert.Less(StPetersBuilder.IslandRadiusY, StPetersBuilder.IslandRadius,
                "an island is longer than it is wide — and 450 × 260 is not a disc at any radius");

            float discWidth = StPetersBuilder.IslandRadius * 2f;
            float discWaterPerSide = (StPetersBuilder.RegionWorldSize.y - discWidth) * 0.5f;
            Assert.Less(discWaterPerSide, StPetersBuilder.IslandFalloff + 10f,
                "SABOTAGE CHECK — if a disc left comfortably more water than its own beach needs, the " +
                "shape argument would be the only one and this measurement would be worth deleting.");

            float actualWaterPerSide =
                (StPetersBuilder.RegionWorldSize.y - StPetersBuilder.IslandRadiusY * 2f) * 0.5f;
            Assert.Greater(actualWaterPerSide, 3f * StPetersBuilder.IslandFalloff,
                "the ellipse must leave real water north and south — beach, then reef shelf, then sea");

            Debug.Log($"[st-peters] a {discWidth} m disc would leave {discWaterPerSide} m of water per " +
                      $"long side in a {StPetersBuilder.RegionWorldSize.y} m scene, against its own " +
                      $"{StPetersBuilder.IslandFalloff} m beach — no room for the reef shelf. The " +
                      $"{StPetersBuilder.IslandRadiusY * 2f} m ellipse leaves {actualWaterPerSide} m.");
        }

        // =========================================================================================
        // 2. the flip: island EAST, bar WEST
        // =========================================================================================

        /// <summary>
        /// §5.1: *"The sandbar leaves the WEST end. This flips today's greybox, where the island sits at
        /// x = −40 and the bar runs east to x = +34."* Both halves of that sentence are asserted, because
        /// getting only one of them right would put the crossing on the wrong side of the island.
        /// </summary>
        [Test]
        public void TheIslandSitsEastOfCentre_AndTheSandbarLeavesTheWestEnd()
        {
            Assert.Greater(StPetersBuilder.IslandCenter.x, 0f,
                "the island must sit EAST of the scene centre (§5.1)");

            Assert.Less(StPetersBuilder.SandbarTo.x, StPetersBuilder.SandbarFrom.x,
                "the bar must run WEST — From is the island end, To is the crossing end");
            Assert.Less(StPetersBuilder.SandbarTo.x, StPetersBuilder.IslandCenter.x,
                "…away from the island, not across it");

            // The bar's island end must actually be ON the island, or the crossing starts in the sea.
            float dHead = TidalTerrain.IslandDistance(StPetersBuilder.SandbarFrom,
                                                      StPetersBuilder.IslandCenter,
                                                      StPetersBuilder.IslandRadius,
                                                      StPetersBuilder.IslandRadiusY);
            Assert.LessOrEqual(dHead, StPetersBuilder.IslandRadius,
                "the bar's head must sit on the island's land, so the two join");

            // …and the dock must be on the OPPOSITE end from the bar (§5.1a, ruled).
            Assert.Greater(StPetersBuilder.DockZonePos.x, StPetersBuilder.IslandCenter.x,
                "the dock is on the EAST end — the far side from the crossing (§5.1a)");

            Debug.Log($"[st-peters] island at x={StPetersBuilder.IslandCenter.x}, bar " +
                      $"{StPetersBuilder.SandbarFrom.x} → {StPetersBuilder.SandbarTo.x} (west), " +
                      $"dock at x={StPetersBuilder.DockZonePos.x} (east). Walk out the west, come home " +
                      "under power to the east.");
        }

        // =========================================================================================
        // 3. ⭐ the boat must actually float where she is moored
        // =========================================================================================

        /// <summary>
        /// ⭐ THE BERTH'S OWN TIDE GATE, which is a RULING and not a bug.
        ///
        /// <para>§5.1a: *"Dock approach / berth bed ≈ −1.0 m. Clears 0.6 m draught whenever the water is
        /// above −0.4 m — most of the cycle — and dries near spring low, so the dock has its own gentle
        /// tide gate rather than being a permanent open door. Deliberate: even coming home under power
        /// should mean reading the tide."*</para>
        ///
        /// <para>⚠ This test previously demanded the dory float at EVERY tide, which was right while the
        /// mooring sat on the bare −4 m floor and is wrong now: it would forbid the very gate the owner
        /// ruled in. What it holds instead is both halves of the ruling — the skiff tier clears the slip
        /// for MOST of the cycle, AND the slip dries near spring low. The old assertion would have
        /// passed a berth dredged to −4 m, which is exactly the "permanent open door" §5.1a rejects.</para>
        /// </summary>
        [Test]
        public void TheBerthClearsTheSkiffTierForMostOfTheCycle_AndStillDriesNearSpringLow()
        {
            float bed = _terrain.ElevationAt(
                new Vector2(StPetersBuilder.DoryMooredPos.x, StPetersBuilder.DoryMooredPos.y));

            // ⚠ At or a little BELOW the berth bed, not exactly on it. The carve only ever cuts DOWN
            // (it must never raise the seabed), so where the reef already lies deeper than −1.0 m the
            // slip simply keeps the reef's own depth — measured −1.05 m at the mooring. What must not
            // happen is the mooring sitting on the shallow beach above the slip, or out on the −4 m
            // floor beyond it; both would mean the dock geometry and the terrain had come apart.
            Assert.LessOrEqual(bed, StPetersBuilder.BerthBedElevation + 0.05f,
                "the mooring sits ABOVE the berth bed — the carve has not reached it, so the boat is " +
                "parked on the reef instead of in the slip");
            Assert.Greater(bed, StPetersBuilder.ReefShelfOuterElevation - 0.5f,
                "the mooring has fallen past the reef into open water — the slip is supposed to hold " +
                "her against the shore, and a dock zone out on the harbour floor cannot be stepped off");

            float draught = DoryDraughtMetres();

            // Half the ruling: usable for most of the cycle.
            float fraction = FractionOfCycleAfloat(bed, draught);
            Assert.Greater(fraction, 0.5f,
                $"the dory ({draught:F2} m) floats at the slip for only {fraction:P0} of the cycle — " +
                "§5.1a wants the skiff tier in and out for MOST of it, with the gate as seasoning.");

            // The other half: it is a GATE, not an open door.
            Assert.Less(Water(-1f) - bed, draught,
                $"the slip does NOT dry at spring low (bed {bed:F2} m against {Water(-1f):F2} m of " +
                "water) — that is a permanent open door, and §5.1a rules the dock keeps its own gentle " +
                "tide gate so that coming home under power still means reading the tide.");

            // …and she must not be so far out that the arrival point misses the dock zone.
            float arrivalToDock = Vector2.Distance(
                new Vector2(StPetersBuilder.ArrivalPos.x, StPetersBuilder.ArrivalPos.y),
                new Vector2(StPetersBuilder.DockZonePos.x, StPetersBuilder.DockZonePos.y));
            Assert.LessOrEqual(arrivalToDock, StPetersBuilder.DockZoneRadius,
                "the sail-home arrival must land INSIDE the dock zone — ControlSwitcher.InDockZone() " +
                "is a pure distance test, so a metre too far and you can never step ashore (#52).");

            Debug.Log($"[st-peters] berth bed {bed:F2} m; dory draught {draught:F2} m → afloat " +
                      $"{fraction:P1} of the cycle, needs the water above {bed + draught:F2} m; " +
                      $"{Water(-1f) - bed:F2} m of water at spring low (dries), " +
                      $"{Water(1f) - bed:F2} m at spring high. Arrival is {arrivalToDock:F2} m from the " +
                      $"dock zone (radius {StPetersBuilder.DockZoneRadius}).");
        }

        /// <summary>
        /// Fraction of one semidiurnal cycle a hull of <paramref name="draught"/> floats over a bed at
        /// <paramref name="bed"/>. Closed form on the sine carrier, so it is the same number the doc's
        /// own "most of the cycle" arithmetic produces rather than a sampled approximation.
        /// </summary>
        private static float FractionOfCycleAfloat(float bed, float draught)
        {
            float need = bed + draught;                  // the water level at which she lifts
            float amp = StPetersBuilder.TideAmplitude;
            float rel = (need - StPetersBuilder.TideMean) / amp;
            if (rel <= -1f) return 1f;
            if (rel >= 1f) return 0f;
            return (Mathf.PI - 2f * Mathf.Asin(rel)) / (2f * Mathf.PI);
        }

        /// <summary>
        /// SABOTAGE, MEASURED. The old mooring is fed to the SAME check and must fail it — otherwise the
        /// test above is passing for a reason unrelated to depth, and the next person to move a mooring
        /// gets no warning either.
        /// </summary>
        [Test]
        public void Sabotage_TheOldGreyboxMooring_WasAgroundForMostOfTheTide()
        {
            // The greybox island: centre (−40, 0), radius 22, falloff 10, plateau +6, floor −4.
            const float oldRadius = 22f, oldFalloff = 10f;
            var oldCentre = new Vector2(-40f, 0f);
            var oldMooring = new Vector2(-40f, -26f);

            float d = TidalTerrain.IslandDistance(oldMooring, oldCentre, oldRadius, 0f);
            float u = Mathf.Clamp01((d - oldRadius) / oldFalloff);
            float bed = Mathf.Lerp(StPetersBuilder.IslandElevation, StPetersBuilder.DeepHarbourElevation,
                                   Mathf.SmoothStep(0f, 1f, u));

            float draught = DoryDraughtMetres();
            Assert.Greater(bed, Water(-1f) - draught,
                "SABOTAGE NOT DETECTED — the old mooring floats at spring low, so there was nothing " +
                "wrong with it and this guard is measuring the wrong thing.");

            float floatsAbove = bed + draught;
            float fractionOfSwing = Mathf.InverseLerp(Water(-1f), Water(1f), floatsAbove);
            Assert.Greater(fractionOfSwing, 0.5f,
                "the old mooring should have been unusable for MOST of the swing, not a sliver of it");

            Debug.Log($"[st-peters] SABOTAGE — old mooring: bed {bed:F2} m, so a {draught:F2} m dory " +
                      $"needed the tide above {floatsAbove:F2} m — the top {(1f - fractionOfSwing) * 100f:F0}% " +
                      $"of the swing. The berth it was replaced by floats her for 57% of the cycle.");
        }

        private static float DoryDraughtMetres()
        {
            var dory = UnityEditor.AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatHullDef>(
                "Assets/_Project/Data/Boats/Dory.asset");
            Assert.IsNotNull(dory, "the Dory hull def must exist — she is the boat being moored");
            Assert.Greater(dory.DraughtMeters, 0f, "a hull with no draught cannot be checked");
            return dory.DraughtMeters;
        }

        // =========================================================================================
        // 4. everything authored is inside the region
        // =========================================================================================

        /// <summary>
        /// At 160 × 120 m every authored point was comfortably inside the scene by accident. At
        /// 760 × 520 the numbers are big enough to get wrong, and a spawn or a passage outside the
        /// painted rectangle reads as "the world just ends" rather than as a bug.
        /// </summary>
        [Test]
        public void EveryAuthoredPoint_LiesInsideTheRegionRectangle()
        {
            Vector2 half = StPetersBuilder.RegionWorldSize * 0.5f;
            Vector2 c = StPetersBuilder.RegionWorldCenter;

            var points = new (string name, Vector2 p)[]
            {
                ("island centre", StPetersBuilder.IslandCenter),
                ("island west tip", StPetersBuilder.IslandCenter + Vector2.left * StPetersBuilder.IslandRadius),
                ("island east tip", StPetersBuilder.IslandCenter + Vector2.right * StPetersBuilder.IslandRadius),
                ("island north tip", StPetersBuilder.IslandCenter + Vector2.up * StPetersBuilder.IslandRadiusY),
                ("island south tip", StPetersBuilder.IslandCenter + Vector2.down * StPetersBuilder.IslandRadiusY),
                ("sandbar head", StPetersBuilder.SandbarFrom),
                ("sandbar tip", StPetersBuilder.SandbarTo),
                ("player spawn", StPetersBuilder.StartSpawnPos),
                ("crossing passage", StPetersBuilder.ToNineMileCreekPassagePos),
                ("dock zone", StPetersBuilder.DockZonePos),
                ("disembark", StPetersBuilder.DisembarkPos),
                ("arrival", StPetersBuilder.ArrivalPos),
            };

            foreach (var (name, p) in points)
            {
                Assert.That(p.x, Is.InRange(c.x - half.x, c.x + half.x), $"{name} x is outside the region");
                Assert.That(p.y, Is.InRange(c.y - half.y, c.y + half.y), $"{name} y is outside the region");
            }

            // The bar's far tip must clear the edge by enough that the passage band fits beyond it.
            float tipToEdge = StPetersBuilder.SandbarTo.x - (c.x - half.x);
            Assert.Greater(tipToEdge, 10f,
                "the bar must stop short of the scene edge — the crossing passage sits beyond its tip");

            Debug.Log($"[st-peters] all {points.Length} authored points inside " +
                      $"{StPetersBuilder.RegionWorldSize.x} × {StPetersBuilder.RegionWorldSize.y} m; " +
                      $"bar tip is {tipToEdge:F0} m from the west edge, passage at " +
                      $"{StPetersBuilder.ToNineMileCreekPassagePos.x}.");
        }

        // =========================================================================================
        // 5. the opening still works at the new scale
        // =========================================================================================

        [Test]
        public void TheOpeningStillReads_SpawnOnLand_WalkTheBaredBar_ReachThePassage()
        {
            float spawn = _terrain.ElevationAt(
                new Vector2(StPetersBuilder.StartSpawnPos.x, StPetersBuilder.StartSpawnPos.y));
            Assert.IsTrue(TidalExposure.IsExposed(Water(1f), spawn),
                "the player must spawn on ground that is dry even at spring high — you start at home");

            Vector2 barMid = Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo, 0.5f);
            float bar = _terrain.ElevationAt(barMid);
            Assert.IsTrue(TidalExposure.IsExposed(Water(-1f), bar), "the bar bares at low water — you walk it");
            Assert.IsFalse(TidalExposure.IsExposed(Water(1f), bar), "…and floods at high water — the gate");

            float passage = _terrain.ElevationAt(
                new Vector2(StPetersBuilder.ToNineMileCreekPassagePos.x,
                            StPetersBuilder.ToNineMileCreekPassagePos.y));
            Assert.IsTrue(TidalExposure.IsExposed(Water(-1f), passage),
                "the crossing passage must be reachable ON FOOT at low water — that is the whole arc");

            Debug.Log($"[st-peters] opening: spawn {spawn:F2} m (dry always), bar mid {bar:F2} m " +
                      $"(bares low, floods high), passage {passage:F2} m (walkable at low water). " +
                      $"Bar is {Vector2.Distance(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo):F0} m " +
                      "long in this scene.");
        }

        [Test]
        public void TheClamFieldStaysAReasonableSize_AtTheNewScale()
        {
            var holes = StPetersBuilder.ScatterClamHoles(_terrain);

            Assert.Greater(holes.Count, 20,
                "the flats must carry a real clam field — the opening's whole economy is digging them");
            Assert.Less(holes.Count, 250,
                "…but every hole is a GameObject with a sprite, a collider and two components (rule 7). " +
                "If the bar grew, ClamScatterStep has to grow with it.");

            // Every hole must be on ground that actually bares and floods, or it is either permanently
            // under water or permanently dry — both of which read as a broken clam.
            float low = Water(-1f), high = Water(1f);
            foreach (Vector2 h in holes)
            {
                float e = _terrain.ElevationAt(h);
                Assert.IsTrue(TidalExposure.IsExposed(low, e), $"clam hole at {h} never bares");
                Assert.IsFalse(TidalExposure.IsExposed(high, e), $"clam hole at {h} never floods");
            }

            Debug.Log($"[st-peters] {holes.Count} clam holes over the bar's " +
                      $"{Vector2.Distance(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo):F0} m × " +
                      $"{StPetersBuilder.SandbarHalfWidth * 2f:F0} m footprint at a " +
                      $"{StPetersBuilder.ClamScatterStep} m grid — all intertidal.");
        }

        // =========================================================================================
        // 6. ⭐ the COMMITTED painted seabed must describe THIS island
        // =========================================================================================

        // =========================================================================================
        // 5b. ⭐ THE REEF RING AND THE ONE DOOR (§5.1a, ruled)
        // =========================================================================================

        /// <summary>
        /// The ring must be a ring — shallow all the way round, not just where somebody sampled it. Any
        /// gap in it and "reefs make landing hard for all but shallow draft" stops being true from one
        /// bearing, which is the kind of hole a player finds in a minute and a test never does unless it
        /// walks the whole circle.
        ///
        /// <para>⭐ <b>The ring has exactly TWO crossings, and that IS the region.</b> The berth cuts the
        /// east door for boats; the SANDBAR rides over the shelf on the west, because the walking path
        /// has to reach the island somehow. You leave home on foot to the west and come back under
        /// power to the east — the two ways through the reef are the two halves of the opening arc.
        /// (The bar was not in the first version of this test and it failed on it, which is the right
        /// way round: a ring that had no foot crossing would have been the real bug.)</para>
        /// </summary>
        [Test]
        public void TheReefShelfRingsTheWholeIsland_ExceptItsTwoCrossings()
        {
            Assert.Greater(StPetersBuilder.ReefShelfWidth, 0f, "the ring is ruled in (§5.1a)");

            // Sample the shelf band all the way round, on the ellipse's own metric.
            float shelfMid = StPetersBuilder.IslandRadius + StPetersBuilder.IslandFalloff
                           + StPetersBuilder.ReefShelfWidth * 0.5f;
            float aspect = StPetersBuilder.IslandRadiusY / StPetersBuilder.IslandRadius;

            int onShelf = 0, throughBerth = 0, overTheBar = 0;
            float shallowest = float.MinValue, deepest = float.MaxValue;
            for (int i = 0; i < 180; i++)
            {
                float a = i * Mathf.PI * 2f / 180f;
                var p = StPetersBuilder.IslandCenter +
                        new Vector2(Mathf.Cos(a) * shelfMid, Mathf.Sin(a) * shelfMid * aspect);
                float e = _terrain.ElevationAt(p);

                // Inside the berth's carve the ground is the slip, not the reef — the boat door.
                if (DistanceToBerth(p) < StPetersBuilder.BerthHalfWidth) { throughBerth++; continue; }

                // …and where the sandbar rides over the shelf, the ground is the walking path — the
                // foot door. Both are crossings BY DESIGN, not holes in the ring.
                if (DistanceToSandbar(p) < StPetersBuilder.SandbarHalfWidth) { overTheBar++; continue; }

                onShelf++;
                Assert.Less(e, 0f, $"the shelf at {p} is above datum — that is beach, not reef");
                Assert.Greater(e, StPetersBuilder.DeepHarbourElevation + 0.5f,
                    $"the shelf at {p} has fallen to the deep floor — the ring has a HOLE there, and a " +
                    "hull that cannot cross the reef anywhere else can simply come in on this bearing.");
                shallowest = Mathf.Max(shallowest, e);
                deepest = Mathf.Min(deepest, e);
            }

            Assert.Greater(onShelf, 140, "most of the circle must be reef, not crossing");
            Assert.Greater(throughBerth, 0, "the berth must actually cut its door through the ring");
            Assert.Greater(overTheBar, 0, "…and the sandbar must actually reach the island over it");

            Debug.Log($"[st-peters] reef ring: {onShelf}/180 bearings on the shelf between " +
                      $"{deepest:F2} and {shallowest:F2} m; {throughBerth} through the berth (the boat " +
                      $"door, east), {overTheBar} over the sandbar (the foot door, west).");
        }

        private static float DistanceToSandbar(Vector2 p)
        {
            Vector2 a = StPetersBuilder.SandbarFrom, b = StPetersBuilder.SandbarTo;
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + t * ab);
        }

        private static float DistanceToBerth(Vector2 p)
        {
            Vector2 a = StPetersBuilder.BerthFrom, b = StPetersBuilder.BerthTo;
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + t * ab);
        }

        /// <summary>
        /// ⭐ The gate the ring exists to be, measured against the real hull ladder rather than asserted
        /// in prose. §5.1a: every skiff/punt-tier hull is ≤ 0.6 m, the first two WORKING hulls are
        /// 1.3–1.4 m, the dragger is 2.9 m — *"the island you start on becomes the island your big boat
        /// can never come home to."*
        ///
        /// <para>⚠ This test reports the band each hull lands in and asserts only the ORDERING plus the
        /// two ends, because §5.1a itself flags the middle as a thing to look at once authored: *"the
        /// lobster boat and Cape Islander land in a sometimes band, not a never band… If it should land
        /// harder, raise the shelf rather than lowering the boats."* Pinning a percentage here would
        /// freeze a number the owner has explicitly reserved.</para>
        /// </summary>
        [Test]
        public void TheBerthGatesTheHullLadder_SkiffTierHomeWorkingHullsTideGatedDraggerNever()
        {
            float bed = _terrain.ElevationAt(
                new Vector2(StPetersBuilder.DockZonePos.x, StPetersBuilder.DockZonePos.y));

            var hulls = new[] { "Dory", "FishingSkiff", "Punt", "PuntUpgraded", "SportSkiff",
                                "SportSkiffTwin", "ConsoleSkiff", "LobsterBoat", "CapeIslander" };

            var report = new System.Text.StringBuilder(
                $"[st-peters] the berth at {bed:F2} m against the fleet:\n");
            float skiffTierWorst = 1f, workingHullBest = 0f;

            foreach (string name in hulls)
            {
                var hull = UnityEditor.AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatHullDef>(
                    $"Assets/_Project/Data/Boats/{name}.asset");
                if (hull == null) continue;

                float f = FractionOfCycleAfloat(bed, hull.DraughtMeters);
                bool skiffTier = hull.DraughtMeters <= 0.6f;
                if (skiffTier) skiffTierWorst = Mathf.Min(skiffTierWorst, f);
                else workingHullBest = Mathf.Max(workingHullBest, f);

                report.AppendLine($"  {name,-15} draught {hull.DraughtMeters:F2} m → afloat {f,6:P1} " +
                                  $"of the cycle (needs the water above {bed + hull.DraughtMeters:F2} m)" +
                                  (skiffTier ? "   [skiff tier]" : "   [working hull]"));
            }

            Assert.Greater(skiffTierWorst, 0.5f,
                "every hull in the ≤ 0.6 m tier must get in and out for most of the cycle — that tier " +
                "is the one you learn on, and home has to be usable");
            Assert.Less(workingHullBest, skiffTierWorst,
                "a working hull must be gated HARDER than every skiff — otherwise the geography does " +
                "not separate the tier you learn on from the tier you graduate to, and the whole point " +
                "of §5.1a's 0.6 m cut is lost");

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// SABOTAGE, MEASURED — and this one is a bug I actually shipped into the working tree before the
        /// tests caught it. <see cref="TidalTerrain.IslandProfile"/> composes four disjoint bands, and the
        /// tempting way to write it is a set of <c>Lerped</c> calls combined with <c>Mathf.Max</c>. That
        /// is wrong, because <c>Lerped</c> HOLDS its outer value for every distance past its own band: the
        /// beach term reads −1.0 m from its toe to the horizon, so <c>max</c> pins the ENTIRE seabed at
        /// the reef's inner depth and every hull in the game can cross anywhere.
        /// </summary>
        [Test]
        public void Sabotage_ComposingTheIslandBandsWithMax_FloodsTheWholeSeabedToShelfDepth()
        {
            float rx = StPetersBuilder.IslandRadius, fall = StPetersBuilder.IslandFalloff;
            float shelfEnd = rx + fall + StPetersBuilder.ReefShelfWidth;
            float wayOut = shelfEnd + 400f;      // open sea, far past the drop-off

            // What the shipped chain says out there…
            float correct = _terrain.IslandProfile(wayOut);
            Assert.AreEqual(StPetersBuilder.DeepHarbourElevation, correct, 0.01f,
                "far from the island the ground must be the deep floor");

            // …versus what a max() composition would have said. Each band's Lerped, evaluated out here:
            float beachTerm = StPetersBuilder.ReefShelfInnerElevation;   // Lerped holds its outer value
            float shelfTerm = StPetersBuilder.ReefShelfOuterElevation;   // …and so does the shelf's
            float dropTerm = StPetersBuilder.DeepHarbourElevation;
            float naive = Mathf.Max(beachTerm, Mathf.Max(shelfTerm, dropTerm));

            Assert.AreNotEqual(correct, naive,
                "SABOTAGE NOT DETECTED — if max() gave the same answer as the chain there would be no " +
                "reason to write the chain, and this test should go.");
            Assert.AreEqual(StPetersBuilder.ReefShelfInnerElevation, naive, 0.01f,
                "the naive composition should pin the open sea at the reef's INNER depth — that is the " +
                "failure mode, and it is worth stating exactly");

            float draught = DoryDraughtMetres();
            Assert.Greater(FractionOfCycleAfloat(naive, 2.9f), 0f,
                "…and the consequence: even a 2.9 m dragger would float over open sea it should never " +
                "reach, because the whole seabed had risen to the shelf");

            Debug.Log($"[st-peters] SABOTAGE — band composition: at {wayOut:F0} m from the centre the " +
                      $"chain gives {correct:F2} m (the floor); a max() of the three band terms gives " +
                      $"{naive:F2} m — the reef's inner depth, spread to the horizon. Dory draught " +
                      $"{draught:F2} m would then float everywhere, and so would the dragger.");
        }

        private static PaintedHeightMap Seabed() =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(
                "Assets/_Project/Data/Terrain/StPetersSeabed.asset");

        /// <summary>
        /// ⭐ THE GUARD AGAINST THE DRIFT THAT ALREADY HAPPENED ONCE. The painted seabed is the shipped
        /// coast — <b>paint = sail</b> (ADR 0014), so the same map decides what the water draws AND where
        /// the player can wade. When the region grew to 760 × 520 m the committed map went on describing
        /// a 160 × 120 m world, and nothing said so: it decodes fine, it just describes somewhere else.
        ///
        /// <para>Checking the map's own rect against the <see cref="RegionDef"/> is the cheap half.
        /// Checking the TEXTURE's dimensions against the region's derived texel grid is the half that
        /// also catches Unity <b>silently downscaling</b> an oversized import — the failure mode where
        /// every count still matches and only a dimension assert notices.</para>
        /// </summary>
        [Test]
        public void TheCommittedSeabed_CoversTheRegion_AtTheRegionsOwnResolution()
        {
            var map = Seabed();
            Assert.IsNotNull(map, "the committed StPetersSeabed seed must exist (ADR 0014)");
            var region = AssetDatabase();

            Assert.AreEqual(region.WorldCenter.x, map.WorldCenter.x, 0.01f, "seabed centre x vs the region");
            Assert.AreEqual(region.WorldCenter.y, map.WorldCenter.y, 0.01f, "seabed centre y vs the region");
            Assert.AreEqual(region.WorldSizeMeters.x, map.WorldSize.x, 0.01f,
                "the seabed must cover the WHOLE region — a smaller rect means the coast the player " +
                "sails is not the coast the region is");
            Assert.AreEqual(region.WorldSizeMeters.y, map.WorldSize.y, 0.01f);

            var tex = map.HeightTexture;
            Assert.IsNotNull(tex, "the seed must reference its external height PNG");
            Assert.IsTrue(tex.isReadable,
                "the height texture must be CPU-readable or the sim cannot decode it at all (ADR 0014)");

            Assert.AreEqual(region.SeabedTexels.x, tex.width,
                $"the committed PNG is {tex.width} px wide but the region derives " +
                $"{region.SeabedTexels.x} — either the bake is stale, or Unity DOWNSCALED an oversized " +
                "import and the only thing that would ever notice is this assert.");
            Assert.AreEqual(region.SeabedTexels.y, tex.height, "…and its height");

            Assert.LessOrEqual(Mathf.Max(tex.width, tex.height), RegionDef.MaxSeabedTexels,
                "over the cap the import is downscaled behind your back");

            // The encoding range must BRACKET the terrain, or the deepest water and the highest land are
            // both clipped to the same value and the coast flattens at its extremes.
            Assert.LessOrEqual(map.MinElevation, StPetersBuilder.DeepHarbourElevation,
                "R=0 must reach at least the deep-harbour floor");
            Assert.GreaterOrEqual(map.MaxElevation, StPetersBuilder.IslandElevation,
                "R=1 must reach at least the island plateau");

            float texelX = map.WorldSize.x / tex.width, texelY = map.WorldSize.y / tex.height;
            Assert.AreEqual(texelX, texelY, 0.01f,
                "the texels must be SQUARE — a stretched grid samples the two axes at different " +
                "densities, which is the bug #320 removed");

            Debug.Log($"[st-peters] committed seabed: {tex.width} × {tex.height} texels over " +
                      $"{map.WorldSize.x} × {map.WorldSize.y} m = {texelX:F2} m/texel, elevation " +
                      $"{map.MinElevation}..{map.MaxElevation} m, readable={tex.isReadable}, " +
                      $"{tex.width * tex.height / 1024} KiB R8.");
        }

        /// <summary>
        /// …and it must be a bake of THIS coast, not merely a correctly-sized one. Decoded elevations are
        /// compared against the analytic terrain at points chosen to sit in FLAT interiors — island,
        /// bar crest, channel bed, open floor — where bilinear sampling is exact and only the R8
        /// quantisation contributes error.
        /// </summary>
        [Test]
        public void TheCommittedSeabed_DecodesToTheAnalyticCoastItWasBakedFrom()
        {
            var map = Seabed();
            Assert.IsNotNull(map);
            PaintedHeightField field = map.Field;
            Assert.IsNotNull(field, "the height texture must decode (readable + linear)");

            // One R8 step over the encoded range, plus a hair for the round-trip.
            float tolerance = (map.MaxElevation - map.MinElevation) / 255f + 0.02f;

            var probes = new (string what, Vector2 p)[]
            {
                ("island interior", StPetersBuilder.IslandCenter),
                ("bar crest", Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo, 0.3f)),
                ("channel bed", Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo,
                                             StPetersBuilder.ChannelAlong)),
                ("open floor N", new Vector2(0f, 230f)),
                ("open floor E", new Vector2(360f, 0f)),
                ("the mooring", new Vector2(StPetersBuilder.DockZonePos.x, StPetersBuilder.DockZonePos.y)),
            };

            var report = new System.Text.StringBuilder(
                "[st-peters] committed seabed vs the analytic coast it was baked from:\n");
            foreach (var (what, p) in probes)
            {
                float analytic = _terrain.ElevationAtZones(p);
                float painted = field.ElevationAt(p);
                report.AppendLine($"  {what,-16} {p}  analytic {analytic,6:F2} m  painted {painted,6:F2} m " +
                                  $" Δ {Mathf.Abs(painted - analytic):F3} m");
                Assert.AreEqual(analytic, painted, tolerance,
                    $"{what}: the committed seabed disagrees with the analytic terrain by more than one " +
                    "quantisation step — the bake is of a different coast, so what the shader draws is " +
                    "not what the tide bares (paint = sail, ADR 0014).");
            }

            Debug.Log(report + $"  tolerance {tolerance:F3} m (one R8 step over " +
                      $"{map.MaxElevation - map.MinElevation} m + round-trip).");
        }
    }
}
