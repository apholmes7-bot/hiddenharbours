using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>The rough-edges jag: off by default, whole texels, deterministic, not the tile's period, and a collision
    /// line with no bite narrower than a walker can leave.</summary>
    public class CliffJagMathTests
    {
        const string StPetersMix = "Assets/_Project/Data/Resources/CliffFaceMixes/CliffFaceMix_StPeters.asset";
        static readonly CliffJag Rough = new CliffJag(0.35f, 0.45f, new Vector3(3f, 0.9f, 0.25f), new Vector3(0.6f, 0.3f, 0.15f), 1f, 6);

        [Test]
        public void Off_IsExactlyZero_SoOtherRegionsDoNotMove()
        {
            CliffJag off = default;
            Assert.IsFalse(off.On);
            for (float x = -20f; x < 40f; x += 0.37f)
                Assert.AreEqual(0f, CliffJagMath.At(in off, x, 0.5f));
            Assert.IsFalse(CliffJag.From(null).On);
        }

        /// <summary>Owner 11 26, no mixels: a wall that faces across the screen takes less of the roughness, or none.
        /// A south wall's push runs down its own drawn column and it keeps all of it; a steep east wall's push runs
        /// across a face that is a sliver on screen, and it takes none; with no bounds set nothing changes.</summary>
        [Test]
        public void ASteepWallAcrossTheScreen_TakesNoRoughness_AndASouthWallAllOfIt()
        {
            var bounds = new Vector2(0.35f, 0.8f);
            var faced = new CliffJag(0.35f, 0.45f, new Vector3(3f, 0.9f, 0.25f), new Vector3(0.6f, 0.3f, 0.15f), 1f, 6, bounds);
            float texel = 1f / CliffWaterlineMath.TexelsPerMetre;

            Vector2 south = Vector2.down, east = Vector2.right;
            Vector2 southColumn = Vector2.down;                               // brow to drawn toe, straight down the screen
            Vector2 steepEastColumn = new Vector2(2f, -6.1f).normalized;      // an 8 m bank at 76 degrees, facing east
            Vector2 easyEastColumn = new Vector2(7.2f, -6.1f).normalized;     // the same bank at 48 degrees

            Assert.AreEqual(1f, CliffJagMath.FacingGain(in faced, south, southColumn), 1e-6f);
            Assert.AreEqual(0f, CliffJagMath.FacingGain(in faced, east, steepEastColumn), 1e-6f);
            float between = CliffJagMath.FacingGain(in faced, east, easyEastColumn);
            Assert.That(between, Is.GreaterThan(0.5f).And.LessThan(1f), "a gentle east bank keeps most of it");
            Assert.AreEqual(1f, CliffJagMath.FacingGain(in Rough, east, steepEastColumn), "no bounds: every wall takes all of it");
            Assert.AreEqual(1f, CliffJagMath.FacingGain(default, east, steepEastColumn));

            for (float x = 0f; x < 40f; x += 0.23f)
            {
                const float profile = 0.31f, warp = 1.4f;
                // None: a flat face. No profile relief, no jag.
                Assert.AreEqual(0f, CliffJagMath.Outward(in faced, profile, warp, 0f, x, 0f), $"x {x}");
                // All: the profile at the warp's gain, and the jag's share still a whole number of texels.
                float all = CliffJagMath.Outward(in faced, profile, warp, 1f, x, 0f) - profile * warp;
                Assert.AreEqual(0f, Mathf.Abs(all / texel - Mathf.Round(all / texel)), 1e-3f, $"x {x}");
                // Part: that share of the profile, and never more jag than all of it, to the texel.
                float part = CliffJagMath.Outward(in faced, profile, warp, 0.5f, x, 0f)
                           - profile * Mathf.Lerp(1f, warp, 0.5f) * 0.5f;
                Assert.LessOrEqual(Mathf.Abs(part), Mathf.Abs(all) + texel, $"x {x}");
                // A wall with no bounds, or at full facing, is not multiplied: the floats it always had.
                Assert.AreEqual(profile * Mathf.Lerp(1f, warp, 1f) + CliffJagMath.Snap(CliffJagMath.At(in Rough, x, 0f) * Mathf.Lerp(1f, warp, 1f)),
                                CliffJagMath.Outward(in Rough, profile, warp, 1f, x, 0f), 1e-6f, $"x {x}");
            }
        }

        /// <summary>St Peters' real walls, sorted by the switch: some face across the screen and are laid on the screen
        /// grid, most do not, and the log lists every chunk with its facing so a plate can be read against it.</summary>
        [Test]
        public void StPeters_SomeWallsFaceAcrossTheScreen_AndMostDoNot()
        {
            CliffJag jag = CliffJag.From(UnityEditor.AssetDatabase.LoadAssetAtPath<CliffFaceMixDef>(StPetersMix));
            var walls = HiddenHarbours.App.Editor.StPetersCliffWalls.WallsInScene(
                System.IO.File.ReadAllText(StPetersLayerRefreshTests.ScenePath));
            int planar = 0;
            var log = new System.Text.StringBuilder();
            foreach (var w in walls)
            {
                CliffWallSample[] s = w.Samples.ToArray();
                if (s.Length == 0) continue;
                float sum = 0f;
                Vector2 mid = Vector2.zero;
                foreach (CliffWallSample p in s)
                {
                    sum += Mathf.Abs(Vector2.Dot(CliffWallGeometry.OutwardPlan(in p),
                        CliffWallGeometry.DrawnDown(p.BrowPlan, CliffWallGeometry.ToeScreen(in p))));
                    mid += p.BrowPlan;
                }
                bool across = CliffJagMath.FacesAcrossTheScreen(in jag, s);
                if (across) planar++;
                log.AppendLine($"{(across ? "PLANAR " : "       ")}{sum / s.Length:F2}  {mid / s.Length}  {w.Name}");
            }
            Debug.Log($"[cliff-facing] {planar} of {walls.Count} St Peters chunks are laid on the screen grid:\n{log}");
            Assert.Greater(planar, 0, "no St Peters wall is under the lower facing bound: the switch does nothing");
            Assert.Less(planar, walls.Count / 2, "most of St Peters' walls switched: the south walls have moved");
        }

        static CliffWallSample[] Wall(Vector2 from, Vector2 step, Vector2 run, float drop, int n = 9)
        {
            var wall = new CliffWallSample[n];
            for (int i = 0; i < n; i++) wall[i] = new CliffWallSample(from + step * i, from + step * i + run, drop);
            return wall;
        }

        /// <summary>The mapping switch (owner 11 26, no mixels): one answer per chunk, from the region's lower facing
        /// bound. A steep wall facing east or west is laid on the screen grid; a south wall, a south-east one and a
        /// gentle east bank keep the mapping they had; a region with no bounds switches nothing.</summary>
        [Test]
        public void OnlyAChunkFacingAcrossTheScreen_IsMappedOnTheScreenGrid()
        {
            var faced = new CliffJag(0.35f, 0.45f, new Vector3(3f, 0.9f, 0.25f), new Vector3(0.6f, 0.3f, 0.15f), 1f, 6,
                                     new Vector2(0.35f, 0.8f));
            CliffWallSample[] steepEast = Wall(new Vector2(180f, -30f), Vector2.up, new Vector2(2f, 0f), 8f);
            CliffWallSample[] steepWest = Wall(new Vector2(180f, -30f), Vector2.down, new Vector2(-2f, 0f), 8f);
            CliffWallSample[] south = Wall(new Vector2(60f, -70f), Vector2.right, new Vector2(0f, -2f), 8f);
            CliffWallSample[] southEast = Wall(new Vector2(170f, -35f), new Vector2(0.7f, 0.7f), new Vector2(1.4f, -1.4f), 8f);
            CliffWallSample[] gentleEast = Wall(new Vector2(180f, -30f), Vector2.up, new Vector2(7.2f, 0f), 8f);

            Assert.IsTrue(CliffJagMath.FacesAcrossTheScreen(in faced, steepEast), "steep east");
            Assert.IsTrue(CliffJagMath.FacesAcrossTheScreen(in faced, steepWest), "steep west");
            Assert.IsFalse(CliffJagMath.FacesAcrossTheScreen(in faced, south), "south");
            Assert.IsFalse(CliffJagMath.FacesAcrossTheScreen(in faced, southEast), "south-east");
            Assert.IsFalse(CliffJagMath.FacesAcrossTheScreen(in faced, gentleEast), "gentle east");
            Assert.IsFalse(CliffJagMath.FacesAcrossTheScreen(in Rough, steepEast), "no bounds: nothing switches");
            Assert.IsFalse(CliffJagMath.FacesAcrossTheScreen(default, steepEast));
            Assert.IsFalse(CliffJagMath.FacesAcrossTheScreen(in faced, new CliffWallSample[0]));
        }

        /// <summary>A planar face: every vertex's uv is its world position on the tile grid, so bedding lies level on
        /// screen (equal world y, equal v), nothing is rotated or sheared (u reads x alone, v reads y alone), and the
        /// vertices are the ones the other mapping lays.</summary>
        [Test]
        public void APlanarFace_IsMappedOnTheScreenGrid_AndItsVerticesDoNotMove()
        {
            var faced = new CliffJag(0.35f, 0.45f, new Vector3(3f, 0.9f, 0.25f), new Vector3(0.6f, 0.3f, 0.15f), 1f, 6,
                                     new Vector2(0.35f, 0.8f));
            const float metresS = 12f, metresT = 9f;
            var origin = new Vector3(181f, -26f, 0f);
            CliffWallColumns cols = CliffWallSurface.Unsliced(Wall(new Vector2(180f, -30f), Vector2.up, new Vector2(2f, 0f), 8f));
            System.Func<float, float, float> profile = (u, t) => 0.4f * Mathf.Sin(u * 40f) * t;

            CliffWallMeshData sheared = CliffWallSurface.FaceMesh(cols, 0f, 0f, 8, 1.5f, metresS, metresT, profile, 1f, origin,
                                                                  default, faced);
            CliffWallMeshData planar = CliffWallSurface.FaceMesh(cols, 0f, 0f, 8, 1.5f, metresS, metresT, profile, 1f, origin,
                                                                 default, faced, planar: true);

            CollectionAssert.AreEqual(sheared.Verts, planar.Verts);
            CollectionAssert.AreEqual(sheared.Tris, planar.Tris);
            CollectionAssert.AreEqual(sheared.ElevationUv, planar.ElevationUv);
            CollectionAssert.AreEqual(sheared.SeaPlanUv, planar.SeaPlanUv);
            for (int i = 0; i < planar.Verts.Length; i++)
            {
                Vector2 world = (Vector2)planar.Verts[i] + (Vector2)origin;
                Assert.AreEqual(world.x / metresS, planar.Uvs[i].x, 1e-4f, $"vertex {i} u");
                Assert.AreEqual(-world.y / metresT, planar.Uvs[i].y, 1e-4f, $"vertex {i} v");
            }
            // The other mapping is the sheared one the owner rejected: on this wall its v is not level on screen.
            CollectionAssert.AreNotEqual(sheared.Uvs, planar.Uvs);
        }

        /// <summary>South wall bytes unchanged: with St Peters' bounds set, a south wall (and its warp, its jag and its
        /// profile) is laid float for float as it is with no bounds, and it is not switched.</summary>
        [Test]
        public void ASouthWall_IsTheSameFloats_WithTheFacingBoundsSetOrNot()
        {
            var mix = UnityEditor.AssetDatabase.LoadAssetAtPath<CliffFaceMixDef>(StPetersMix);
            Assert.IsNotNull(mix, StPetersMix);
            CliffJag bounded = CliffJag.From(mix);
            var unbounded = new CliffJag(mix.BrowJagMetres, mix.FootJagMetres, mix.JagCellMetres, mix.JagWeights,
                                         mix.MinBiteMetres, mix.Seed);
            Assert.Greater(bounded.Facing.y, 0f);

            // A south wall that wanders: its brow rises and falls and its run and drop change, as the real ones do.
            var south = new CliffWallSample[25];
            for (int i = 0; i < south.Length; i++)
            {
                var brow = new Vector2(60f + i * 0.5f, -70f + 0.3f * Mathf.Sin(i * 0.7f));
                var run = new Vector2(0.4f * Mathf.Sin(i * 0.9f), -1.5f - 0.5f * Mathf.Cos(i * 0.4f));
                south[i] = new CliffWallSample(brow, brow + run, 6f + (i % 5) * 0.5f, 0.2f);
            }
            Assert.IsFalse(CliffJagMath.FacesAcrossTheScreen(in bounded, south));

            CliffWallColumns cols = CliffWallSurface.Unsliced(south);
            System.Func<float, float, float> profile = (u, t) => 0.4f * Mathf.Sin(u * 40f) * t;
            var origin = new Vector3(66f, -71f, 0f);
            foreach ((float start, float end) in new[] { (0f, 0f), (0f, 1.2f), (1.2f, 0f) })
            {
                CliffWallMeshData before = CliffWallSurface.FaceMesh(cols, start, end, 10, 3.25f, 12f, 9f, profile, 1f, origin,
                                                                     default, unbounded);
                CliffWallMeshData after = CliffWallSurface.FaceMesh(cols, start, end, 10, 3.25f, 12f, 9f, profile, 1f, origin,
                                                                    default, bounded,
                                                                    CliffJagMath.FacesAcrossTheScreen(in bounded, south));
                CollectionAssert.AreEqual(before.Verts, after.Verts, $"band {start}-{end} verts");
                CollectionAssert.AreEqual(before.Uvs, after.Uvs, $"band {start}-{end} uvs");
                CollectionAssert.AreEqual(before.ElevationUv, after.ElevationUv);
                CollectionAssert.AreEqual(before.SeaPlanUv, after.SeaPlanUv);
                CollectionAssert.AreEqual(before.Tris, after.Tris);
            }
        }

        /// <summary>St Peters sets the bounds, and in order: without them its steep east wall breaks into shards.</summary>
        [Test]
        public void StPeters_HoldsTheRoughnessBackOnWallsAcrossTheScreen()
        {
            var mix = UnityEditor.AssetDatabase.LoadAssetAtPath<CliffFaceMixDef>(StPetersMix);
            Assert.IsNotNull(mix, StPetersMix);
            Assert.Greater(mix.RoughnessByFacing.y, mix.RoughnessByFacing.x, "the bounds are not in order");
            Assert.Greater(mix.RoughnessByFacing.x, 0f, "St Peters' steep east wall takes the whole jag again");
            Assert.LessOrEqual(mix.RoughnessByFacing.y, 1f);
        }

        [Test]
        public void TheLine_MovesInWholeTexels_AndRepeatsItself()
        {
            float texel = 1f / CliffWaterlineMath.TexelsPerMetre;
            for (float x = 0f; x < 60f; x += 0.11f)
                foreach (float t in new[] { 0f, 0.3f, 1f })
                {
                    float a = CliffJagMath.At(in Rough, x, t);
                    Assert.AreEqual(0f, Mathf.Abs(a / texel - Mathf.Round(a / texel)), 1e-4f, $"x {x} t {t}");
                    Assert.AreEqual(a, CliffJagMath.At(in Rough, x, t), "the same place draws the same line");
                    Assert.LessOrEqual(Mathf.Abs(a), 0.45f * 1.06f + texel);
                }
        }

        [Test]
        public void TheLine_IsNotTheTilesPeriod_AndBreaksAtMoreThanOneScale()
        {
            int same = 0, n = 0, steps = 0, big = 0;
            float prev = CliffJagMath.At(in Rough, 0f, 1f);
            for (float x = 0.125f; x < 120f; x += 0.25f, n++)
            {
                float a = CliffJagMath.At(in Rough, x, 1f);
                if (Mathf.Approximately(a, CliffJagMath.At(in Rough, x + 12f, 1f))) same++;
                if (!Mathf.Approximately(a, prev)) steps++;
                if (Mathf.Abs(a - prev) >= 0.12f) big++;
                prev = a;
            }
            Assert.Less(same, n / 4, "the foot must not repeat with the kit's 12 m tile");
            Assert.Greater(steps, n / 2, "chips: the line steps at nearly every column");
            Assert.Greater(big, 10, "buttresses and bites: some steps are deep");
        }

        [Test]
        public void TheClosing_FillsEveryBiteNarrowerThanTheWalker_AndKeepsEveryBump()
        {
            const float step = 0.25f;
            int count = 480;
            var along = new float[count];
            var raw = new float[count];
            for (int i = 0; i < count; i++) { along[i] = i * step; raw[i] = CliffJagMath.At(in Rough, along[i], 1f); }
            float[] closed = CliffJagMath.Close(along, raw, Rough.MinBiteMetres);
            for (int i = 0; i < count; i++)
                Assert.GreaterOrEqual(closed[i], raw[i] - 1e-6f, "closing only ever fills");
            // Every run of the closed line lower than both its sides is at least the bite wide.
            for (int i = 1; i < count - 1; i++)
            {
                if (!(closed[i] < closed[i - 1] - 1e-6f)) continue;
                int j = i;
                while (j + 1 < count && Mathf.Abs(closed[j + 1] - closed[i]) < 1e-6f) j++;
                if (j + 1 < count && closed[j + 1] > closed[i] + 1e-6f)
                    Assert.GreaterOrEqual((j - i + 1) * step + 1e-4f, Rough.MinBiteMetres - step,
                        $"a notch at {along[i]} m is narrower than a walker can leave");
            }
            // The highest point of the line is never moved: closing fills bites, it does not grow buttresses.
            float rawMax = float.MinValue, closedMax = float.MinValue;
            for (int i = 0; i < count; i++) { rawMax = Mathf.Max(rawMax, raw[i]); closedMax = Mathf.Max(closedMax, closed[i]); }
            Assert.AreEqual(rawMax, closedMax, 1e-6f);
        }

        [Test]
        public void StPetersMix_TurnsTheRoughEdgesOn()
        {
#if UNITY_EDITOR
            var mix = UnityEditor.AssetDatabase.LoadAssetAtPath<CliffFaceMixDef>(StPetersMix);
            Assert.IsNotNull(mix);
            CliffJag jag = CliffJag.From(mix);
            Assert.IsTrue(jag.On);
            Assert.GreaterOrEqual(jag.MinBiteMetres, 2f * HiddenHarbours.World.QuayEdgePlan.WalkerFootRadiusMetres,
                "a bite the collision leaves open must be wider than the walker's foot");
#endif
        }
    }
}
