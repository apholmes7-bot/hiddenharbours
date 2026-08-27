using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The mown lawns</b> — the owner's 2026-08-26 ask, measured.
    ///
    /// <para>The claim under test is not "it looks like a lawn" — that is his eye's job after a
    /// Build St Peters click. It is the two structural things a screenshot cannot show: that every
    /// property's care level is <b>authored on its own yard row</b> and reaches the ground the
    /// building stands on, and that the ladder's coupling between weight and position is being used
    /// deliberately rather than tripped over.</para>
    /// </summary>
    public class StPetersLawnTests
    {
        GameObject _go;
        TidalTerrain _terrain;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TidalTerrain_LawnTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
            StPetersYards.InvalidateCache();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // The splat grid this island actually paints on, so the coverage maps under test are the
        // resolution that ships rather than a convenient one.
        const int W = 152, H = 104;                       // a tenth of the real 1520x1040, same aspect
        static Vector2 WorldMin => StPetersBuilder.IslandCenter
                                 - StPetersBuilder.RegionWorldSize * 0.5f;
        static Vector2 WorldSize => StPetersBuilder.RegionWorldSize;

        // =====================================================================================

        /// <summary>
        /// <b>Every property says how it keeps its grass, and the island shows all three answers.</b>
        /// A table where everything came out Kept would satisfy every other assertion here and say
        /// nothing about anybody.
        /// </summary>
        [Test]
        public void EveryYard_DeclaresItsCare_AndTheIslandShowsAllThree()
        {
            var yards = StPetersYards.Yards;
            Assert.Greater(yards.Count, 0, "sanity: no yards at all");

            var byStyle = yards.GroupBy(y => y.Mown).ToDictionary(g => g.Key, g => g.Select(y => y.Name).ToArray());
            string summary = string.Join(" · ",
                byStyle.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}: {string.Join(", ", kv.Value)}"));
            Debug.Log($"[Lawns] {summary}");

            foreach (MownStyle style in new[] { MownStyle.Rough, MownStyle.Kept, MownStyle.Striped })
                Assert.IsTrue(byStyle.ContainsKey(style),
                    $"no property on the island is {style}. The owner ruled kept everywhere and " +
                    $"striped on the proud few, which needs all three to exist. Got: {summary}");

            // Striped is the exception, not the rule — "the households that would bother".
            Assert.Less(byStyle[MownStyle.Striped].Length, yards.Count,
                "every yard is striped, which makes the distinction say nothing.");
            CollectionAssert.AreEquivalent(StPetersLawns.StylesInUse().Distinct().ToArray(),
                                           byStyle.Keys.ToArray(),
                                           "StylesInUse disagrees with the yard table.");
        }

        /// <summary>
        /// 🔴 <b>The ladder's coupling, asserted as the deliberate design it is.</b>
        ///
        /// <para>A painted channel's value is BOTH the blend weight and the ladder position, so a
        /// rougher lawn is necessarily a THINNER one — the wild grass band grows up through it. That
        /// is the whole reason "kept but rough" needs no second material, and it is also exactly the
        /// property a well-meaning future edit would destroy by pushing every yard to 1.0 to "fix
        /// the weighting". This pins both halves.</para>
        /// </summary>
        [Test]
        public void TheCareLadder_RisesAndLetsTheMeadowThroughARoughLawn()
        {
            float rough = StPetersLawns.IntensityFor(MownStyle.Rough);
            float kept = StPetersLawns.IntensityFor(MownStyle.Kept);
            float striped = StPetersLawns.IntensityFor(MownStyle.Striped);

            Assert.Less(rough, kept, "a rough lawn must sit below a kept one on the ladder.");
            Assert.LessOrEqual(kept, striped, "a striped lawn is at least as well kept as a plain one.");
            Assert.LessOrEqual(striped, 1f, "the channel is 0..1.");

            // The shader keeps (1 - paintTotal) of the height-derived band. A rough lawn must leave a
            // real share of it, or it is just a slightly duller crisp lawn.
            float meadowThrough = 1f - rough;
            Assert.Greater(meadowThrough, 0.25f,
                $"a Rough lawn paints {rough:F2}, so only {meadowThrough:P0} of the wild grass band " +
                "shows through it. Rough is supposed to be a LET-GO dooryard — the meadow growing up " +
                "into it is the look, not a weighting bug to be corrected by raising this to 1.0.");

            // And a kept lawn must nearly displace it, or the mown ground never reads as mown.
            Assert.Less(1f - kept, 0.2f,
                $"a Kept lawn paints {kept:F2}, leaving {1f - kept:P0} of the wild band showing — too " +
                "much for ground somebody mows.");
        }

        /// <summary>
        /// <b>The lawn reaches the walls.</b> A yard exists to hold its building; a lawn that stopped
        /// short of the doorstep would leave a ring of meadow round every house, which is the defect
        /// the yard polygons were shaped to avoid in the first place.
        /// </summary>
        [Test]
        public void EveryLawn_ReachesTheGroundItsBuildingStandsOn()
        {
            foreach (var yard in StPetersYards.Yards)
            {
                float[] map = StPetersLawns.CoverageFor(yard.Mown, W, H, WorldMin, WorldSize);
                int x = Mathf.FloorToInt((yard.Owner.x - WorldMin.x) / WorldSize.x * W);
                int y = Mathf.FloorToInt((yard.Owner.y - WorldMin.y) / WorldSize.y * H);
                Assert.That(x, Is.InRange(0, W - 1), $"{yard.Name} is off the splat grid");
                Assert.That(y, Is.InRange(0, H - 1), $"{yard.Name} is off the splat grid");

                Assert.Greater(map[y * W + x], 0f,
                    $"the ground under {yard.Name}'s building carries no lawn at all — the coverage " +
                    "map and the yard polygon disagree about where the yard is.");
            }
        }

        /// <summary>
        /// <b>A style's coverage only ever lands inside that style's yards.</b> One map per style is
        /// what keeps the mow-line feather from moving the LADDER as well as the weight, and it is
        /// only sound if the maps are actually disjoint.
        /// </summary>
        [Test]
        public void EachStylesCoverage_StaysInsideItsOwnYards()
        {
            foreach (MownStyle style in StPetersLawns.StylesInUse())
            {
                float[] map = StPetersLawns.CoverageFor(style, W, H, WorldMin, WorldSize);
                int painted = 0;
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (map[y * W + x] <= 0f) continue;
                    painted++;
                    var p = new Vector2(WorldMin.x + (x + 0.5f) / W * WorldSize.x,
                                        WorldMin.y + (y + 0.5f) / H * WorldSize.y);
                    bool inOne = StPetersYards.Yards.Any(yd => yd.Mown == style && yd.Polygon.Contains(p));
                    Assert.IsTrue(inOne,
                        $"a texel at {p} carries {style} lawn but stands in no {style} yard.");
                }
                Assert.Greater(painted, 0, $"the {style} coverage map is empty.");
            }
        }

        /// <summary>Rule 5: the pass is a pure function of the authored table, so two runs agree and
        /// re-running the paint cannot drift the island.</summary>
        [Test]
        public void TheCoverage_IsDeterministic()
        {
            foreach (MownStyle style in StPetersLawns.StylesInUse())
            {
                float[] a = StPetersLawns.CoverageFor(style, W, H, WorldMin, WorldSize);
                float[] b = StPetersLawns.CoverageFor(style, W, H, WorldMin, WorldSize);
                CollectionAssert.AreEqual(a, b, $"the {style} coverage map changed between runs.");
            }
        }

        /// <summary>
        /// <b>The wild tufts stop at the mow line, and the lawn does not have to arrange that.</b>
        /// <see cref="StPetersGrass.IsPlantableMeadow"/> already refuses ground inside a yard, so
        /// wild grass and mown ground meet on ONE boundary rather than two that could drift apart.
        ///
        /// <para>⚠ This is the half of the seam that exists on main. The other half — the wild tufts
        /// THINNING and stepping down to short blades as they approach that same line, because a yard
        /// edge is a CUT edge to the grass layer's edge band — arrives with PR #662 and wants its own
        /// assertion here once that has merged.</para>
        /// </summary>
        [Test]
        public void TheWildMeadow_StopsAtEveryMowLine()
        {
            foreach (var yard in StPetersYards.Yards)
            {
                Assert.IsFalse(StPetersGrass.IsPlantableMeadow(_terrain, yard.Owner),
                    $"wild grass is allowed on {yard.Name}'s lawn — the meadow gate and the lawn " +
                    "paint would both claim that ground.");
            }

            // And the meadow must still exist just outside, or "stops at the mow line" is vacuous.
            int outside = StPetersYards.Yards.Count(
                y => StPetersGrass.IsPlantableMeadow(_terrain, y.Owner + Vector2.up * 30f)
                  || StPetersGrass.IsPlantableMeadow(_terrain, y.Owner + Vector2.down * 30f));
            Assert.Greater(outside, 0,
                "no yard has plantable meadow anywhere near it, so this test is asserting nothing.");
        }
    }
}
