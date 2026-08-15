using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ THE SHORE OF NINE MILE CREEK — the tidal planting, the foreshore's rock, and the four
    /// treatments this coast has that the island cannot express.
    ///
    /// <para><b>Every number here is DERIVED from the plan the builder pushes</b>, never from a literal
    /// copied out of it — the house's derived-bounds law, and the same discipline
    /// <see cref="NineMileCreekMainlandCoastTests"/> keeps. A classifier that silently died would empty
    /// the scatter, and a bound written down by hand would go on passing.</para>
    ///
    /// <para><b>The three things this file exists to hold:</b> that the layout is a HASH and not an
    /// accident (so two Build clicks agree and a seed axis genuinely moves it); that the shore pass and
    /// #538's field pass ABUT rather than overlap; and that nothing is planted on ground somebody works.</para>
    /// </summary>
    public class NineMileCreekShoreTests
    {
        /// <summary>The terrain exactly as the scene carries it — built through the builder's own
        /// <see cref="NineMileCreekMainland.ConfigureTerrain"/>, so a test can never assert a coast the
        /// scene does not have.</summary>
        private static MainlandTidalTerrain MakeTerrain(out GameObject host)
        {
            host = new GameObject("NMCShoreTestTerrain");
            var terrain = host.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(terrain);
            return terrain;
        }

        private static void Destroy(GameObject host)
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        // =============================================================================================
        //  1. the ladder — is sharing St Peters' legitimate, and does it mean anything here?
        // =============================================================================================

        [Test]
        public void TheLadderMayBeSharedOnlyBecauseTheTwoRegionsAuthorTheSameTide()
        {
            // ⚠ If this ever goes false the fix is NOT to relax it — it is to give this region its own
            // planting ladder, re-derived from its own tide, because every floor in it is a TIDE STATE.
            Assert.That(NineMileCreekShoreMap.SharesTheIslandsTide, Is.True,
                "NineMileCreekShore references St Peters' marsh floors. That reference is only honest " +
                "while the two regions author the same tide — they do not any more, so the ladder must fork.");
        }

        [Test]
        public void TheWetTwoFloorsAreThisCoastsOwnShelfLips_AndTheyAgreeWithTheIslands()
        {
            // The doc comment claims the mid-intertidal and subtidal floors are reached from THIS region's
            // authored foreshore apron rather than inherited — and that they land on the island's numbers
            // anyway. Both halves, measured.
            Assert.That(NineMileCreekShore.MidIntertidalFloorElevation,
                Is.EqualTo(NineMileCreekMainland.ShelfInnerElevation).Within(1e-4f),
                "the rockweed band's floor is the foreshore shelf's INNER lip");
            Assert.That(NineMileCreekShore.SubtidalFloorElevation,
                Is.EqualTo(NineMileCreekMainland.ShelfOuterElevation).Within(1e-4f),
                "planting stops at the shelf's OUTER lip, where the apron drops to the bay floor");

            Assert.That(NineMileCreekShore.MidIntertidalFloorElevation,
                Is.EqualTo(StPetersShorePlants.MidIntertidalFloorElevation).Within(1e-4f),
                "the two coasts shelve alike — a player crossing the bar must not watch the weed line jump");
            Assert.That(NineMileCreekShore.SubtidalFloorElevation,
                Is.EqualTo(StPetersShorePlants.SubtidalFloorElevation).Within(1e-4f),
                "…and stop planting at the same depth, for the same reason");
        }

        [Test]
        public void NothingPlantsBelowTheShelfOrAboveTheMeadow()
        {
            Assert.That(NineMileCreekShore.ZoneAt(NineMileCreekShore.SubtidalFloorElevation - 0.01f),
                Is.Null, "below the shelf's outer lip the water is deeper than the seabed reads through");
            Assert.That(NineMileCreekShore.ZoneAt(NineMileCreekShoreMap.GrassFloorElevation),
                Is.Null, "at the grass floor the FIELDS take over — that is the seam with #538");
            Assert.That(NineMileCreekShore.ZoneAt(NineMileCreekShoreMap.GrassFloorElevation - 0.01f),
                Is.EqualTo(NineMileCreekShore.ZoneUpland),
                "…and a hair below it the shore's own upland is still in charge, with no gap between them");
        }

        // =============================================================================================
        //  2. the scatter is a HASH, not an accident
        // =============================================================================================

        [Test]
        public void TheSameSaltScattersTheSameShoreTwice_AndADifferentSaltMovesIt()
        {
            // ⭐ THE DETERMINISM GUARD, and the acceptance criterion this pass was given — with one
            // premise corrected. The handoff asks for "(worldSeed, position) → identical layout twice; a
            // different seed moves it". `worldSeed` is a RUNTIME save field (SaveData.WorldSeed) with no
            // reader in any editor assembly, and this pass BAKES a scene the owner commits: a decor layout
            // that varied with a save's seed would make the committed scene disagree with itself between
            // saves. StPetersRoutines.PreviewSeed records the same finding. The property actually wanted —
            // that the arrangement is a stable hash with a seed AXIS — is what is measured here.
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                var a = NineMileCreekShore.Scatter(terrain);
                var b = NineMileCreekShore.Scatter(terrain);

                Assert.That(a.Count, Is.GreaterThan(0), "the shore must actually plant something");
                Assert.That(b.Count, Is.EqualTo(a.Count), "two scatters must place the same number");
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.That(b[i].Position, Is.EqualTo(a[i].Position),
                        $"site {i} moved between two identical scatters — something reads hidden state");
                    Assert.That(b[i].SpeciesKey, Is.EqualTo(a[i].SpeciesKey), $"site {i} changed species");
                    Assert.That(b[i].Zone, Is.EqualTo(a[i].Zone), $"site {i} changed zone");
                    Assert.That(b[i].Treatment, Is.EqualTo(a[i].Treatment), $"site {i} changed treatment");
                    Assert.That(b[i].Scale, Is.EqualTo(a[i].Scale).Within(1e-6f), $"site {i} changed size");
                }

                // …and the salt is a real axis: a different one must produce a genuinely different shore,
                // not a shore that merely differs somewhere. Compare position SETS.
                var moved = NineMileCreekShore.Scatter(terrain, salt: 101);
                var original = new HashSet<Vector2>();
                foreach (var s in a) original.Add(s.Position);
                int shared = 0;
                foreach (var s in moved) if (original.Contains(s.Position)) shared++;

                Assert.That(shared, Is.LessThan(a.Count / 10),
                    $"a different salt left {shared} of {a.Count} sites in place — the layout is not " +
                    "actually being driven by the hash");
            }
            finally { Destroy(host); }
        }

        [Test]
        public void TheRockScatterIsStableToo()
        {
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                var a = NineMileCreekShore.ScatterRocks(terrain);
                var b = NineMileCreekShore.ScatterRocks(terrain);

                Assert.That(a.Count, Is.GreaterThan(0), "a red-sandstone coast sheds boulders");
                Assert.That(b.Count, Is.EqualTo(a.Count));
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.That(b[i].Position, Is.EqualTo(a[i].Position), $"rock {i} moved");
                    Assert.That(b[i].Sprite, Is.EqualTo(a[i].Sprite), $"rock {i} changed slice");
                }
            }
            finally { Destroy(host); }
        }

        [Test]
        public void TheCliffWallsResolveIdenticallyTwice()
        {
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                var a = NineMileCreekCliffWalls.ResolveChunks(terrain);
                var b = NineMileCreekCliffWalls.ResolveChunks(terrain);

                Assert.That(a.Count, Is.GreaterThan(0), "the plan authors rock; the walls must stand");
                Assert.That(b.Count, Is.EqualTo(a.Count), "two resolves must cut the same chunks");
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.That(b[i].Samples.Count, Is.EqualTo(a[i].Samples.Count), $"chunk {i} restationed");
                    Assert.That(b[i].AspectIndex, Is.EqualTo(a[i].AspectIndex), $"chunk {i} changed aspect");
                    Assert.That(b[i].BatterIndex, Is.EqualTo(a[i].BatterIndex), $"chunk {i} changed batter");
                    Assert.That(b[i].AlongOffsetMetres,
                        Is.EqualTo(a[i].AlongOffsetMetres).Within(1e-4f), $"chunk {i} changed its u origin");
                }
            }
            finally { Destroy(host); }
        }

        // =============================================================================================
        //  3. the seam with #538 — the two passes ABUT, they do not overlap
        // =============================================================================================

        [Test]
        public void NoShorePlantStandsOnGroundTheFieldsPassClaims()
        {
            // ⭐ THE HANDOFF'S STANDING WORRY, measured from the SHORE's side. The two passes are gated on
            // one elevation rather than on a clearance radius, so this is exact rather than approximate:
            // NineMileCreekShore.ZoneAt stops the upland at the grass floor and
            // NineMileCreekFields.IsGrassGround starts the meadow there.
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                float grassFloor = NineMileCreekShoreMap.GrassFloorElevation;
                var sites = NineMileCreekShore.Scatter(terrain);
                Assert.That(sites.Count, Is.GreaterThan(0));

                foreach (var s in sites)
                {
                    float e = terrain.ElevationAt(s.Position);
                    Assert.That(e, Is.LessThan(grassFloor),
                        $"{s.SpeciesKey} at {s.Position} stands at {e:F2} m, on or above the grass floor " +
                        $"({grassFloor:F2} m) — that is the fields' band and #538 already plants it");
                }
            }
            finally { Destroy(host); }
        }

        [Test]
        public void NoHedgerowOrFieldTreeStandsOnGroundTheShorePassClaims()
        {
            // …and from the FIELDS' side, because a seam only holds if both sides respect it. The woody
            // layer's own floor is higher again (FieldFloorElevation), so this has margin — measured, so
            // that a future re-tune of either floor that closed the gap is caught here rather than in a
            // screenshot.
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                Assert.That(NineMileCreekFields.FieldFloorElevation,
                    Is.GreaterThanOrEqualTo(NineMileCreekShoreMap.GrassFloorElevation),
                    "the woody layer must not reach below the grass floor, or hedges grow into the shore");

                // The shore's own ceiling, from the other direction: the topmost thing it plants is the
                // upland, and the upland's ceiling IS the grass floor.
                Assert.That(NineMileCreekShore.ZoneAt(NineMileCreekFields.FieldFloorElevation), Is.Null,
                    "at the hedgerows' own floor the shore pass must already have stopped");
            }
            finally { Destroy(host); }
        }

        // =============================================================================================
        //  4. nothing is planted on ground somebody works
        // =============================================================================================

        [Test]
        public void NothingGrowsOnTheBuiltGround_ButTheBasinsBottomIsNotBuiltGround()
        {
            // ⭐ THE DELIBERATE DIFFERENCE FROM THE FIELDS' GATE. NineMileCreekFields refuses every fill;
            // this pass refuses the ones you STAND on and accepts the harbour shoal, which is the basin's
            // water. Both halves are asserted, because dropping either would be a silent regression: the
            // first would grow eelgrass on the quay, the second would leave a sheltered harbour bottom
            // completely bare.
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                // The yard, the two decks and the crib crest: all authored above the highest water.
                Assert.That(NineMileCreekShore.OnBuiltGround(
                    new Vector2(NineMileCreekMainland.SpitFill.Center.x,
                                NineMileCreekMainland.SpitFill.Center.y)), Is.True,
                    "the spit's red-earth yard is ground you walk on");
                Assert.That(NineMileCreekShore.OnBuiltGround(
                    new Vector2(NineMileCreekMainland.NorthWallFill.Center.x,
                                NineMileCreekMainland.NorthWallFill.Center.y)), Is.True,
                    "the north wall is a deck");

                // …and the shoal is not.
                Assert.That(NineMileCreekShore.OnBuiltGround(
                    new Vector2(NineMileCreekMainland.HarbourShoalFill.Center.x,
                                NineMileCreekMainland.HarbourShoalFill.Center.y)), Is.False,
                    "the harbour shoal is the BASIN'S BOTTOM — four metres of water over it at high tide");

                var sites = NineMileCreekShore.Scatter(terrain);
                foreach (var s in sites)
                    Assert.That(NineMileCreekShore.OnBuiltGround(s.Position), Is.False,
                        $"{s.SpeciesKey} at {s.Position} is growing on made ground somebody walks on");

                int inBasin = 0;
                foreach (var s in sites) if (NineMileCreekShore.InTheHarbourBasin(s.Position)) inBasin++;
                Assert.That(inBasin, Is.GreaterThan(0),
                    "the basin's bottom is sheltered water and must carry SOMETHING — a harbour bottom " +
                    "that grows nothing at all is a bug rather than a restraint");
            }
            finally { Destroy(host); }
        }

        [Test]
        public void NothingGrowsOnARoad_OrOnTheWalkingBar_OrInAWorkingBerth()
        {
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                var sites = NineMileCreekShore.Scatter(terrain);
                foreach (var s in sites)
                {
                    Assert.That(NineMileCreekFields.OnAnyCarriageway(s.Position, 0f), Is.False,
                        $"{s.SpeciesKey} at {s.Position} is growing down the middle of a road — #527's " +
                        "rule is that made ground WINS and shore treatment abuts it");

                    // The crossing is a WALKING line and weed over it reads as "do not cross". The GUT is
                    // exempt by design: it is the boat channel cut through the bar.
                    if (NineMileCreekShore.InTheGut(s.Position)) continue;
                    float toBar = MainlandTidalTerrain.DistanceToSegment(
                        s.Position, NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo);
                    Assert.That(toBar, Is.GreaterThanOrEqualTo(NineMileCreekMainland.BarHalfWidth),
                        $"{s.SpeciesKey} at {s.Position} is growing across the tidal crossing");
                }

                for (int i = 0; i < NineMileCreekMainland.BerthCount; i++)
                {
                    Vector2 berth = NineMileCreekMainland.BerthPos(i);
                    foreach (var s in sites)
                        Assert.That(Vector2.Distance(s.Position, berth),
                            Is.GreaterThanOrEqualTo(NineMileCreekShore.WorkingWaterClearance),
                            $"{s.SpeciesKey} at {s.Position} is growing in berth {i} — a propeller has " +
                            "already cut it");
                }
            }
            finally { Destroy(host); }
        }

        // =============================================================================================
        //  5. the four treatments — this coast's own, and botanically honest
        // =============================================================================================

        [Test]
        public void TheMarshPoolIsFreshAndTheBarachoisIsNot()
        {
            // ⚠ REFUTES THE GEOGRAPHY FILE'S OWN WORDING. NineMileCreekMainland §7 calls both carves
            // "freshwater-side features". Re-derived from the authored polyline: the barachois' rectangle
            // reaches seaward of the coast run (that IS the "drained through the coastline's notch" the
            // same paragraph describes), and the marsh pool's does not.
            Assert.That(NineMileCreekShore.BarachoisIsBrackish, Is.True,
                "the barachois opens to the sea through the notch — it is brackish, not fresh");
            Assert.That(NineMileCreekShore.MarshPoolIsFresh, Is.True,
                "the marsh pool sits behind an unbroken sill of plateau — no tide reaches it");
        }

        [Test]
        public void CattailGrowsInTheFreshPoolAndNowhereElse()
        {
            // The one species the kit bakes that has never been placed, and the reason St Peters holds it
            // back — "it wants standing FRESH water and this island has none authored" — stops applying
            // exactly here. It must not leak anywhere salt.
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                var sites = NineMileCreekShore.Scatter(terrain);
                int cattails = 0;
                foreach (var s in sites)
                {
                    if (s.SpeciesKey != "Cattail") continue;
                    cattails++;
                    Assert.That(s.Treatment, Is.EqualTo(NineMileCreekShore.ShoreTreatment.Fresh),
                        $"cattail at {s.Position} is standing in salt water");
                }
                Assert.That(cattails, Is.GreaterThan(0),
                    "the marsh pool is the first standing fresh water either region has — if this is 0 " +
                    "the Fresh treatment is not reaching the pool at all");
            }
            finally { Destroy(host); }
        }

        [Test]
        public void NoEelgrassOnAWaveBeatenFace()
        {
            // Eelgrass is a SOFT-BOTTOM species. A meadow of it at the foot of a crib breakwater or under
            // a red bluff is the kind of mistake the eye does not catch and a fisherman does.
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                foreach (var s in NineMileCreekShore.Scatter(terrain))
                    if (s.Treatment == NineMileCreekShore.ShoreTreatment.Exposed)
                        Assert.That(s.SpeciesKey, Is.Not.EqualTo("Eelgrass"),
                            $"eelgrass at {s.Position} is holding on to a wave-beaten face");
            }
            finally { Destroy(host); }
        }

        [Test]
        public void AllFourTreatmentsActuallyReachTheCoast()
        {
            // A treatment nothing ever resolves to is a treatment that does not exist. Measured rather
            // than assumed, because three of the four are gated on rectangles the plan can move.
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                var seen = new HashSet<NineMileCreekShore.ShoreTreatment>();
                foreach (var s in NineMileCreekShore.Scatter(terrain)) seen.Add(s.Treatment);

                foreach (NineMileCreekShore.ShoreTreatment t in
                         System.Enum.GetValues(typeof(NineMileCreekShore.ShoreTreatment)))
                    Assert.That(seen.Contains(t), Is.True,
                        $"nothing on this coast resolved to {t} — the treatment is dead code");
            }
            finally { Destroy(host); }
        }

        // =============================================================================================
        //  6. the walls stand where the PLAN says, and nowhere else
        // =============================================================================================

        [Test]
        public void EveryCliffChunkStandsOnASectorThePlanCallsRock()
        {
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                var chunks = NineMileCreekCliffWalls.ResolveChunks(terrain);
                Assert.That(chunks.Count, Is.GreaterThan(0));

                foreach (var c in chunks)
                    Assert.That(CoastPlan.IsCliff(c.Class), Is.True,
                        $"a wall was cut for class {c.Class}, which the plan does not call rock — an art " +
                        "pass may draw what the plan declares and must never invent geometry");
            }
            finally { Destroy(host); }
        }

        [Test]
        public void NoWallStandsPastEitherEndOfTheOpenRun()
        {
            // ⚠ THE OPEN-RUN HAZARD. MainlandCoast.ClassAt CLAMPS, so a sweep that ran off the end would
            // read the first/last sector's class from beyond the coastline — and both of this run's ends
            // are soft, so the bug would be invisible today and would appear the moment someone authored
            // rock at either end.
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                float run = terrain.CoastRunLength;
                foreach (var c in NineMileCreekCliffWalls.ResolveChunks(terrain))
                foreach (var s in c.Samples)
                {
                    MainlandCoast.Project(terrain.CoastPoints, s.BrowPlan, out float along, out _);
                    Assert.That(along, Is.GreaterThanOrEqualTo(-0.01f).And.LessThanOrEqualTo(run + 0.01f),
                        $"a wall station projects to {along:F2} m along a {run:F2} m run — it is standing " +
                        "off the end of the coastline");
                }
            }
            finally { Destroy(host); }
        }

        [Test]
        public void EveryChunkWearsAFaceTheAspectLawAllows()
        {
            // The batter SNAPS and the azimuth runs free, but the azimuth must still land inside the
            // kit's tolerance of one of the five authored facings — otherwise the wall is drawn wearing a
            // face chosen for a direction it does not point.
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                foreach (var c in NineMileCreekCliffWalls.ResolveChunks(terrain))
                {
                    float error = CoastPlan.AspectSnapError(c.WallAzimuth);
                    Assert.That(error, Is.LessThanOrEqualTo(CoastPlan.AspectSnapToleranceDegrees + 0.01f),
                        $"a chunk facing {c.WallAzimuth:F1}° is {error:F1}° from the nearest authored " +
                        $"facing — the aspect law's ceiling is {CoastPlan.AspectSnapToleranceDegrees}°");
                }
            }
            finally { Destroy(host); }
        }

        [Test]
        public void NoChunkSortsOverMoreWorldYThanOneCharacterIsTall()
        {
            // ADR 0032: a chunk has ONE sorting order and spans a stretch of world Y, so what makes an
            // order dishonest is the Y span, not the length. Measured off the BUILT chunks rather than
            // trusting the cut rule — StPetersCliffWallTests' own discipline.
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                float worst = 0f;
                foreach (var c in NineMileCreekCliffWalls.ResolveChunks(terrain))
                {
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (var s in c.Samples)
                    {
                        float y = HiddenHarbours.Art.CliffWallGeometry.ToeScreen(s).y;
                        lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
                    }
                    worst = Mathf.Max(worst, hi - lo);
                }
                Assert.That(worst,
                    Is.LessThanOrEqualTo(NineMileCreekCliffWalls.ChunkToeSpanMetres + 0.01f),
                    $"the worst chunk's foot wanders {worst:F2} m of world Y — a walker standing at its " +
                    "shallowest foot would be sorted by the wall at the other end");
            }
            finally { Destroy(host); }
        }

        [Test]
        public void TheSoilHorizonIsThisRegionsOwnMeadowBand()
        {
            Assert.That(NineMileCreekCliffWalls.OverburdenMetres,
                Is.EqualTo(NineMileCreekMainland.LandElevation
                           - NineMileCreekShoreMap.GrassFloorElevation).Within(1e-4f),
                "the soil on a face must be as deep as the soil the clifftop is already growing on");
            Assert.That(NineMileCreekCliffWalls.OverburdenMetres, Is.GreaterThan(0f),
                "a negative or zero soil horizon would draw every face in bare rock");
        }

        // =============================================================================================
        //  7. the footprint stays inside a scene the owner can open
        // =============================================================================================

        [Test]
        public void TheShoresFootprintStaysInsideItsBudget()
        {
            // Each plant is a GameObject + SpriteRenderer + ShorePlantTideView; St Peters carries 384 of
            // them. This coast is longer and has two ponds, so a bigger number is expected — but an
            // unbounded one is how a scene stops opening (rule 7).
            var terrain = MakeTerrain(out GameObject host);
            try
            {
                int plants = NineMileCreekShore.Scatter(terrain).Count;
                int rocks = NineMileCreekShore.ScatterRocks(terrain).Count;

                Assert.That(plants, Is.InRange(100, 4000),
                    $"the shore placed {plants} plants — outside the range a hand-openable scene carries");
                Assert.That(rocks, Is.InRange(1, 200),
                    $"the foreshore placed {rocks} rocks — a tell, not a rock field");
            }
            finally { Destroy(host); }
        }
    }
}
