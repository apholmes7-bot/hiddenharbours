using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The mainland's ground classifier. Three things here are load-bearing and none of them are visible
    /// in a screenshot until they are wrong:
    ///
    /// <list type="number">
    /// <item><description>the band ladder is St Peters', BY REFERENCE, and that is only legitimate while
    /// the two regions share a tide — so the sharing is asserted, not assumed;</description></item>
    /// <item><description>the shader's island-shaped weather sector is NEUTRALISED across the whole
    /// region, which is measured with the shader's own arithmetic rather than trusted;</description></item>
    /// <item><description>rock comes from the COAST PLAN, so the painted rock and the terrain's own cliff
    /// walls cannot disagree.</description></item>
    /// </list>
    /// </summary>
    public class NineMileCreekShoreMapTests
    {
        private static MainlandTidalTerrain MakeTerrain(out GameObject go)
        {
            go = new GameObject("NMCShoreMapTestTerrain");
            var t = go.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(t);
            return t;
        }

        // ============================ 1. THE SHARED LADDER ============================

        [Test]
        public void TheLadderIsSharedOnlyBecauseTheTideIs()
        {
            // ⭐ THE CONDITION, NOT THE CONSEQUENCE. Every floor in the ladder is a TIDE STATE, so
            // referencing the island's is legitimate exactly while the two tides are equal — which
            // NineMileCreekMainland §2 goes to some length to arrange, because the tidal bar is ONE bar
            // spanning the seam. If this ever goes red the fix is NOT to relax it: it is to fork the
            // ladder and re-derive it from this region's own tide.
            Assert.IsTrue(NineMileCreekShoreMap.SharesTheIslandsTide,
                "Nine Mile Creek no longer authors St Peters' tide, so it may no longer borrow its band " +
                "ladder — the floors are tide states. Fork NineMileCreekShoreMap's floors and re-check " +
                "the crossing's seam.");

            Assert.AreEqual(StPetersShoreMap.PaintFloorElevation,
                NineMileCreekShoreMap.PaintFloorElevation, 1e-6f);
            Assert.AreEqual(StPetersShoreMap.RippleFloorElevation,
                NineMileCreekShoreMap.RippleFloorElevation, 1e-6f);
            Assert.AreEqual(StPetersShoreMap.SandFloorElevation,
                NineMileCreekShoreMap.SandFloorElevation, 1e-6f);
            Assert.AreEqual(StPetersShoreMap.MarramFloorElevation,
                NineMileCreekShoreMap.MarramFloorElevation, 1e-6f);
            Assert.AreEqual(StPetersShoreMap.GrassFloorElevation,
                NineMileCreekShoreMap.GrassFloorElevation, 1e-6f);
        }

        [Test]
        public void TheBandsAreOrderedAndEachOneIsATideState()
        {
            float low = NineMileCreekShoreMap.SpringLowWater;
            float high = NineMileCreekShoreMap.SpringHighWater;

            // ⭐ Paint spent below the lowest water is paint on seabed that is never once uncovered —
            // the exact defect the 2026-08-01 amplitude ruling put into St Peters' floor.
            Assert.Greater(NineMileCreekShoreMap.PaintFloorElevation, low,
                "the paint floor sits below the lowest spring water — that ground never bares");

            Assert.Less(NineMileCreekShoreMap.PaintFloorElevation,
                        NineMileCreekShoreMap.RippleFloorElevation);
            Assert.Less(NineMileCreekShoreMap.RippleFloorElevation,
                        NineMileCreekShoreMap.SandFloorElevation);
            Assert.Less(NineMileCreekShoreMap.SandFloorElevation,
                        NineMileCreekShoreMap.MarramFloorElevation);
            Assert.Less(NineMileCreekShoreMap.MarramFloorElevation,
                        NineMileCreekShoreMap.GrassFloorElevation);

            // The marram band is the one defined by straddling the neap/spring gap, and the meadow cap
            // must sit above the highest water or the sea would wash the fields.
            Assert.Less(NineMileCreekShoreMap.NeapHighWater, NineMileCreekShoreMap.MarramFloorElevation);
            Assert.Less(NineMileCreekShoreMap.MarramFloorElevation, high);
            Assert.Greater(NineMileCreekShoreMap.GrassFloorElevation, high);

            // …and the fields themselves must actually reach the meadow cap, or this region has no grass.
            Assert.GreaterOrEqual(NineMileCreekMainland.LandElevation,
                NineMileCreekShoreMap.GrassFloorElevation,
                "the fields stand below the grass floor — the whole inland would draw as dune");
        }

        // ============================ 2. THE NEUTRALISED SECTOR ============================

        /// <summary>The shader's own blend width (<c>_SectorBlend</c>'s default in
        /// HiddenHarboursTerrainSplat.shader). Quoted so the margin below is measured against the number
        /// the GPU actually uses.</summary>
        private const float ShaderSectorBlend = 0.08f;

        [Test]
        public void TheWholeRegionResolvesToOneVocabulary_MeasuredNotTrusted()
        {
            // ⭐ MEASURED WITH THE SHADER'S OWN ARITHMETIC. The splat shader carries one island-shaped
            // weather/sheltered split and cannot read a coast run, so the region is pushed entirely onto
            // the sheltered ladder by putting the sector anchor far out to seaward. A factor that stopped
            // being big enough would show up as a wedge of cobble in one corner of the fields — which
            // reads as a painting bug, not a wiring one, and would be hunted in the wrong file.
            Vector2 half = NineMileCreekMainland.RegionWorldSize * 0.5f;
            Vector2 c = NineMileCreekMainland.RegionWorldCenter;

            // The worst case is the corner nearest the anchor and furthest off its axis; walk the whole
            // boundary and the interior rather than reasoning about which corner that is.
            float worst = float.MinValue;
            Vector2 worstAt = Vector2.zero;
            for (int i = 0; i <= 40; i++)
            for (int j = 0; j <= 40; j++)
            {
                var p = new Vector2(c.x - half.x + i / 40f * NineMileCreekMainland.RegionWorldSize.x,
                                    c.y - half.y + j / 40f * NineMileCreekMainland.RegionWorldSize.y);
                float bearing = NineMileCreekShoreMap.SectorBearingAt(p);
                if (bearing > worst) { worst = bearing; worstAt = p; }
            }

            // The shader gates on smoothstep(thresh - blend, thresh + blend, bearing) with
            // thresh = wiggle × feather and |wiggle| ≤ 1, so the highest threshold the weather half can
            // ever be reached at is feather + blend. Stay strictly below it everywhere.
            float ceiling = -(NineMileCreekShoreMap.SectorFeather + ShaderSectorBlend);
            Assert.Less(worst, ceiling,
                $"the sector anchor is no longer far enough offshore: the worst bearing in the region is " +
                $"{worst:0.####} at {worstAt}, which is not below {ceiling:0.####} — part of Nine Mile " +
                $"Creek would draw with the WEATHER coast's cobble vocabulary. Raise " +
                $"NineMileCreekShoreMap.SectorAnchorRegionWidths.");
        }

        [Test]
        public void TheSectorAnchorIsSeawardOfTheRegion()
        {
            // Water is EAST at Nine Mile Creek, so "seaward" is +x and the anchor must be clear of the
            // region's own east edge — an anchor inside the rectangle puts a sector boundary through it.
            float eastEdge = NineMileCreekMainland.RegionWorldCenter.x +
                             NineMileCreekMainland.RegionWorldSize.x * 0.5f;
            Assert.Greater(NineMileCreekShoreMap.SectorAnchor.x, eastEdge);
            Assert.Greater(NineMileCreekShoreMap.SectorFacing.x, 0f,
                "the sector faces seaward (east) — with the anchor out that way the whole region bears " +
                "back landward from it, which is what pins the weather weight at zero");
        }

        // ============================ 3. ROCK IS THE PLAN ============================

        [Test]
        public void RockCoastFollowsTheCoastPlan_NotABearing()
        {
            var terrain = MakeTerrain(out GameObject go);
            try
            {
                // ⭐ The classes the plan declares, asked through the SAME decider the cliff walls read.
                Assert.IsTrue(NineMileCreekShoreMap.IsRockCoast(CoastClass.Cliff));
                Assert.IsTrue(NineMileCreekShoreMap.IsRockCoast(CoastClass.DeepShoreCliff));
                Assert.IsTrue(NineMileCreekShoreMap.IsRockCoast(CoastClass.LedgeCliff));
                Assert.IsFalse(NineMileCreekShoreMap.IsRockCoast(CoastClass.Beach));
                Assert.IsFalse(NineMileCreekShoreMap.IsRockCoast(CoastClass.Dune));
                Assert.IsFalse(NineMileCreekShoreMap.IsRockCoast(CoastClass.Access),
                    "the gully is the ONE way down to the foreshore — dressing it in ledge and talus is " +
                    "the same lie as weathering the bar's walking line");

                // …and on the ground. The plan puts the crossing's landing on a Beach sector and the
                // spit's root on DeepShoreCliff; a point just seaward of each must answer accordingly.
                Assert.IsFalse(NineMileCreekShoreMap.IsRockCoastAt(terrain, SeawardOf(2, 6f)),
                    "the bar's landing is authored Beach — it must be walkable soft shore");
                Assert.IsTrue(NineMileCreekShoreMap.IsRockCoastAt(terrain, SeawardOf(6, 6f)),
                    "the weather face north of the crossing is authored Cliff — the tall red bluff you " +
                    "look back across the bar from");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>A point <paramref name="metres"/> seaward (east) of coast point
        /// <paramref name="index"/> — the sea is on the RIGHT of this run's south→north walk.</summary>
        private static Vector2 SeawardOf(int index, float metres) =>
            NineMileCreekMainland.CoastPoints[index] + new Vector2(metres, 0f);

        // ============================ 4. MADE GROUND AND PONDS ============================

        [Test]
        public void MadeGroundIsTheFills_AndTheFieldsAreNot()
        {
            // The yard, the two decks, the breakwater and the shoal — the ground that was BUILT.
            Assert.IsTrue(NineMileCreekShoreMap.IsMadeGround(NineMileCreekMainland.SpitFill.Center));
            Assert.IsTrue(NineMileCreekShoreMap.IsMadeGround(NineMileCreekMainland.NorthWallFill.Center));
            Assert.IsTrue(NineMileCreekShoreMap.IsMadeGround(NineMileCreekMainland.WestWallFill.Center));
            Assert.IsTrue(NineMileCreekShoreMap.IsMadeGround(NineMileCreekMainland.BreakwaterFill.Center));

            // The town, the fields and the open bay are not.
            Assert.IsFalse(NineMileCreekShoreMap.IsMadeGround(
                new Vector2(NineMileCreekMainland.HarbourmasterPos.x,
                            NineMileCreekMainland.HarbourmasterPos.y)));
            Assert.IsFalse(NineMileCreekShoreMap.IsMadeGround(new Vector2(-300f, -200f)));
        }

        [Test]
        public void ZoneMembershipFeathersOverTheZonesOwnFalloff_NeverAsARectangle()
        {
            // ⭐ THE DEFECT THIS PASS SHIPPED WITH AND THE SCREENSHOT CAUGHT. These gates were rectangle
            // tests, so the barachois came out with STRAIGHT EDGES and the wharf yard was cut with a
            // ruler — while the terrain underneath eases each zone in over 6–16 m. A painted edge that
            // does not follow the ground's edge reads as a bug in the art, not in the gate.
            MainlandZone pond = NineMileCreekMainland.BarachoisCarve;

            // Dead centre: fully the pond.
            Assert.AreEqual(1f, NineMileCreekShoreMap.PondWeight(pond.Center), 1e-4f);

            // Just outside the flat rectangle, inside the falloff: PARTIAL, which is the whole point.
            var justOut = new Vector2(pond.Center.x + pond.HalfSize.x + pond.Falloff * 0.5f, pond.Center.y);
            float mid = NineMileCreekShoreMap.PondWeight(justOut);
            Assert.That(mid, Is.GreaterThan(0.05f).And.LessThan(0.95f),
                $"the pond's margin is a step, not a grade ({mid:0.###}) — that is the rectangle bug");

            // Beyond the falloff: gone.
            var wellOut = new Vector2(pond.Center.x + pond.HalfSize.x + pond.Falloff * 2f, pond.Center.y);
            Assert.AreEqual(0f, NineMileCreekShoreMap.PondWeight(wellOut), 1e-4f);

            // Monotone across the margin — a grade, not a ripple.
            float prev = 1f;
            for (int i = 0; i <= 10; i++)
            {
                var p = new Vector2(pond.Center.x + pond.HalfSize.x + i / 10f * pond.Falloff, pond.Center.y);
                float w = NineMileCreekShoreMap.PondWeight(p);
                Assert.LessOrEqual(w, prev + 1e-4f, "the pond's margin is not monotone");
                prev = w;
            }

            // And the same for the made ground the wharf stands on.
            Assert.AreEqual(1f, NineMileCreekShoreMap.MadeGroundWeight(
                NineMileCreekMainland.SpitFill.Center), 1e-4f);
            var spitEdge = new Vector2(
                NineMileCreekMainland.SpitFill.Center.x + NineMileCreekMainland.SpitFill.HalfSize.x +
                NineMileCreekMainland.SpitFill.Falloff * 0.5f,
                NineMileCreekMainland.SpitFill.Center.y);
            Assert.That(NineMileCreekShoreMap.MadeGroundWeight(spitEdge),
                Is.GreaterThan(0.05f).And.LessThan(0.95f),
                "the yard's edge is a step — made ground must grade back into the natural shore");
        }

        [Test]
        public void PondGroundIsTheTwoCarves()
        {
            Assert.IsTrue(NineMileCreekShoreMap.IsPondGround(
                NineMileCreekMainland.BarachoisCarve.Center), "the barachois behind the spit");
            Assert.IsTrue(NineMileCreekShoreMap.IsPondGround(
                NineMileCreekMainland.MarshPoolCarve.Center), "the marsh pool south of Wharf Road");

            // ⭐ The NECK between them is dry ground on purpose — Wharf Road runs the 12 m between the two
            // ponds, which is why the reference photo has no causeway. Neither pond may claim it.
            var neck = new Vector2(14f, 92f);   // NineMileCreekMainland.WharfRoad's neck vertex
            Assert.IsFalse(NineMileCreekShoreMap.IsPondGround(neck),
                "the neck Wharf Road runs along has been swallowed by a pond");
        }

        // ============================ 5. THE BAND TABLE ============================

        [Test]
        public void TheFieldsReadAsGrass_AndTheDeepBayIsUnpainted()
        {
            var terrain = MakeTerrain(out GameObject go);
            try
            {
                // Inland, well behind the shore: the reverting meadow.
                Assert.AreEqual(ShoreMaterial.Grass,
                    NineMileCreekShoreMap.MaterialAt(terrain, new Vector2(-250f, 0f)));

                // Out in the open bay at −6 m: below the paint floor, so the quad clips and the Sea plane
                // is the whole picture. Painting there would be budget spent on ground nobody ever sees.
                Assert.AreEqual(ShoreMaterial.None,
                    NineMileCreekShoreMap.MaterialAt(terrain, new Vector2(340f, 200f)));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
