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
        /// ⭐ THE ASSERT THAT WOULD HAVE CAUGHT THE OLD MOORING. Before the rescale the dory was moored
        /// at (−40, −26) under a comment reading *"off the island's south coast (deep water)"*. It was
        /// not deep water: the island's beach falloff puts that ground at <b>+2.48 m</b>, so a 0.30 m
        /// dory floated there only when the tide was above +2.78 m — near high water and nowhere else.
        /// Nothing said so, because nothing ever checked the comment against the terrain.
        /// </summary>
        [Test]
        public void TheDoryFloatsAtHerMooring_AtEveryPointInTheTide()
        {
            float bed = _terrain.ElevationAt(
                new Vector2(StPetersBuilder.DoryMooredPos.x, StPetersBuilder.DoryMooredPos.y));

            float draught = DoryDraughtMetres();
            float springLow = Water(-1f);
            float depthAtSpringLow = springLow - bed;

            Assert.Greater(depthAtSpringLow, draught,
                $"the dory ({draught:F2} m draught) is AGROUND at her own mooring at spring low: the " +
                $"bed there is {bed:F2} m and the water only reaches {springLow:F2} m. Move the " +
                "mooring into water that is deep at every tide, or author the dock's berth.");

            // And she must not be *so* far out that the arrival point misses the dock zone.
            float arrivalToDock = Vector2.Distance(
                new Vector2(StPetersBuilder.ArrivalPos.x, StPetersBuilder.ArrivalPos.y),
                new Vector2(StPetersBuilder.DockZonePos.x, StPetersBuilder.DockZonePos.y));
            Assert.LessOrEqual(arrivalToDock, StPetersBuilder.DockZoneRadius,
                "the sail-home arrival must land INSIDE the dock zone — ControlSwitcher.InDockZone() " +
                "is a pure distance test, so a metre too far and you can never step ashore (#52).");

            Debug.Log($"[st-peters] mooring bed {bed:F2} m; dory draught {draught:F2} m → " +
                      $"{depthAtSpringLow:F2} m of water at spring low, {Water(0f) - bed:F2} m at mean, " +
                      $"{Water(1f) - bed:F2} m at spring high. Arrival is {arrivalToDock:F2} m from the " +
                      $"dock zone (radius {StPetersBuilder.DockZoneRadius}).");
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
                      $"of the swing. The new mooring floats at every tide.");
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
    }
}
