using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;                 // SpriteLightMath — the bake camera's two scales
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐⭐ <b>THE BROW AT NINE MILE CREEK — AND THE DATUM LINE IT MEASURED.</b>
    ///
    /// <para>#735 built the gangway, plated it at three states of tide and TOOK IT OUT: the only drawn
    /// ramp the pack owned was baked into the raft's own cell, so it rode with the raft, and its hinge —
    /// the one part of a gangway that must not move — stood 2.0 m in the air at spring high and 2.3 m
    /// buried at spring low. This file is the other end of that refusal.</para>
    ///
    /// <para><b>A brow is the first thing in this region that has to meet TWO drawn objects at once</b>,
    /// and that is why it found what a single piece never could. The apron's face is placed by
    /// <c>NineMileCreekQuayFace.PivotForLip</c>, which lands its DRAWN DECK LIP on the wall's plan lip;
    /// the float was placed by its PIVOT on the run's plan line, against a baked deck quoted in the RIG's
    /// frame while <c>FloatingPlatform.DeckElevationNow</c> answers in the GAME's. Each piece looked
    /// right on its own. Between them was a CONSTANT 2.30 units of screen height, in the wrong
    /// direction, at every state of the tide — so a correctly-sloped ramp climbed from the wharf UP to
    /// the float.</para>
    ///
    /// <para>The fix is one rule, and these tests are it: <b>every wharf-pack piece's chart datum sits on
    /// one drawn line</b>, <c>planY − PackDatumRise</c>. <c>PivotForLip</c> has always put the apron's
    /// there. Put the raft's, its piles' and the brow's there too and every height any of them draws is
    /// measured from one zero.</para>
    ///
    /// <para>The float's own mechanics — deck = tide, grounding, the brow's lerp — are
    /// <c>FloatingPlatformTests</c>'; this harbour's water under her is
    /// <c>NineMileCreekFloatTests</c>'. This file is about the PICTURE agreeing with itself.</para>
    /// </summary>
    public class NineMileCreekGangwayTests
    {
        private static float H => SpriteLightMath.HeightScale;                       // 0.766
        private static float ApronZ => NineMileCreekMainland.WharfDeckElevation;      // 3.00 m
        private static float SpringLow => NineMileCreekMainland.SpringLowWater;       // −2.2
        private static float SpringHigh => NineMileCreekMainland.SpringHighWater;     //  2.2
        private static float PlanY => NineMileCreekMainland.FloatRunY;                // 70

        private GameObject _go;
        private MainlandTidalTerrain _terrain;

        [SetUp]
        public void SetUp()
        {
            StandableSurfaces.Clear();
            _go = new GameObject("NineMileCreekGangway_Test");
            _terrain = _go.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            StandableSurfaces.Clear();
            GameServices.Reset();
        }

        private static string RigSource() => File.ReadAllText(Path.Combine(
            Application.dataPath, "..", "docs/art/rigs/iso-rig-pack/wharf-kit-iso/wharfIsoRig.js"));

        /// <summary>The float's deck height (m above the GAME's datum) at a water level, off her own
        /// authored numbers and the bed the builder measures — the same expression
        /// <c>FloatingPlatform.DeckElevation</c> is.</summary>
        private float DeckAt(float waterLevel) => FloatingPlatform.DeckElevation(
            waterLevel,
            NineMileCreekWharf.FloatBedElevationFrom(_terrain),
            NineMileCreekQuayFace.BakedRigFloatDraughtMetres,
            NineMileCreekQuayFace.BakedRigFloatFreeboard);

        // -- where each piece's picture actually lands, built ONLY from what the region publishes ------

        /// <summary>The world Y the apron's DRAWN deck lip sits on at the brow's plan line — the wall's
        /// own plan lip, which is what <c>PivotForLip</c> exists to guarantee.</summary>
        private static float DrawnApronDeckY =>
            NineMileCreekQuayFace.PivotForLip(new Vector2(0f, PlanY), Vector2.right).y
            + NineMileCreekQuayFace.LipRiseFromPivot(Vector2.right).y;

        /// <summary>The world Y the RAFT's drawn deck sits on: her pivot (the datum line, plus the ride)
        /// plus the deck height the sprite was baked carrying.</summary>
        private static float DrawnFloatDeckY(float deckElevationGame) =>
            NineMileCreekQuayFace.PivotForPlan(new Vector2(0f, PlanY)).y
            + FloatingPlatformVisual.ScreenRise(deckElevationGame,
                                                NineMileCreekQuayFace.BakedFloatDeckGameMetres)
            + NineMileCreekQuayFace.BakedRigFloatDeckZMetres * H;

        /// <summary>The world Y the BROW's drawn hinge sits on — its pivot on the datum line plus the
        /// hinge height the ramp is baked at. Independent of the tide and of the rung, by construction.</summary>
        private static float DrawnBrowHingeY =>
            NineMileCreekQuayFace.PivotForPlan(new Vector2(0f, PlanY)).y
            + NineMileCreekQuayFace.BakedDeckZMetres * H;

        /// <summary>…and its drawn FOOT, for a given rung: the same pivot, plus the landing height that
        /// rung was baked at (hinge less the rung's drop, plus the rig's own roller clearance).</summary>
        private static float DrawnBrowFootY(int rung) =>
            NineMileCreekQuayFace.PivotForPlan(new Vector2(0f, PlanY)).y
            + (NineMileCreekQuayFace.BakedDeckZMetres
               - NineMileCreekQuayFace.GangwayRungDrops[rung]
               + NineMileCreekQuayFace.BakedRigGangwayFootClearanceMetres) * H;

        // =============================================================================================
        //  1. THE DATUM LINE — the defect the brow measured, and the rule that closes it
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>THE HINGE IS ON THE APRON, EXACTLY, AT EVERY STATE OF THE TIDE</b> — and it is exact
        /// because it is the SAME NUMBER, not because it lands close. The brow's cell never moves: all
        /// nine rungs share one cell and one pivot (measured, in the pack contract), so the sprite is
        /// placed once and only the picture between its ends changes.
        ///
        /// <para>This is the assertion #735's plate <c>04-REFUSED</c> failed, restated as arithmetic.</para>
        /// </summary>
        [Test]
        public void TheHingeIsOnTheApronsDrawnDeck_AndNothingCanMoveIt()
        {
            Assert.That(DrawnBrowHingeY, Is.EqualTo(DrawnApronDeckY).Within(1e-4f),
                "the brow's drawn hinge does not land on the apron's drawn deck lip. Both are placed " +
                "from NineMileCreekQuayFace.PackDatumRise — if they have parted, one of the two has " +
                "stopped measuring its height from the pack's datum line, which is exactly the defect " +
                "this rule exists to make impossible");

            // …and it is the plan lip itself, which is where a walker on the apron draws. A hinge that
            // is right relative to the drawn wall but wrong relative to the person crossing it is the
            // same bug one layer along.
            Assert.That(DrawnBrowHingeY, Is.EqualTo(PlanY).Within(1e-4f),
                "the apron's drawn deck lip is its plan lip (PivotForLip's whole purpose), so the hinge " +
                "must be there too");

            // ⚠️ The DRAWN hinge stands at the height the PACK was baked for; the WALKABLE one is
            // measured off this region's terrain. #471's tripwire says those are the same number, and
            // this is the first thing that would be silently wrong if it ever stopped being true — the
            // brow would hinge where the art thinks the deck is rather than where the player stands.
            Assert.That(NineMileCreekQuayFace.ShortfallMetres, Is.EqualTo(0f).Within(1e-3f),
                "the pack is no longer baked at this wharf's deck height — the drawn hinge and the " +
                "walkable abutment have parted, and NineMileCreekQuayFace's own decomposition says " +
                "whether it is the tide or the freeboard that moved");
            Assert.That(NineMileCreekWharf.ApronElevationFrom(_terrain), Is.EqualTo(ApronZ).Within(1e-3f),
                "the builder hands GangwayPlatform an abutment MEASURED off the terrain while this file " +
                "reasons from the authored constant — a terrain edit that lowered the apron would move " +
                "the brow the sim walks and not the brow the pack draws");
        }

        /// <summary>
        /// ⭐⭐ <b>THE FOOT LANDS ON THE FLOAT AT EVERY STATE OF THE TIDE</b>, to within the ladder's own
        /// step — and it is never ABOVE the planks by more than the rig's own roller clearance, which is
        /// the whole reason the rung rounds up.
        ///
        /// <para><b>This is the test that fails on #735's placement.</b> With the raft anchored by its
        /// pivot on the plan line, against a baked deck quoted in the rig's frame, this residual is a
        /// constant +2.30 units — the apron's own drawn height — at every one of these water levels.</para>
        /// </summary>
        [Test]
        public void TheFootLandsOnTheFloatAtEveryStateOfTheTide()
        {
            float step = NineMileCreekQuayFace.GangwayRungDrops[1] -
                         NineMileCreekQuayFace.GangwayRungDrops[0];

            // The raft's own drawn hull — deck down to the bottom of her billets — is what has to cover a
            // foot that settles in. Derived from the art, so a deeper billet buys the ladder more room.
            float raftHullUnits = NineMileCreekQuayFace.BakedRigFloatHullDepthMetres * H;

            for (int i = 0; i <= 40; i++)
            {
                float water = Mathf.Lerp(SpringLow, SpringHigh, i / 40f);
                float deck = DeckAt(water);
                float drop = ApronZ - deck;
                int rung = NineMileCreekQuayFace.GangwayRungFor(drop);

                float foot = DrawnBrowFootY(rung);
                float planks = DrawnFloatDeckY(deck);
                float above = foot - planks;                       // + = clear of the deck, − = settled in

                // The rig's own roller clearance, plus the 1 mm a rung is allowed to round DOWN by so an
                // exact tie lands on its own rung rather than a step past it (GangwayVisual's note).
                Assert.That(above, Is.LessThanOrEqualTo(
                        (NineMileCreekQuayFace.BakedRigGangwayFootClearanceMetres
                         + NineMileCreekQuayFace.GangwayRungToleranceMetres) * H + 1e-4f),
                    $"at water {water:0.00} m the brow's foot floats {above:0.000} u above the planks. " +
                    "Daylight under the rollers is the one artefact that reads as broken — the rung must " +
                    "round UP so the drawn ramp is never flatter than the real one");

                Assert.That(above, Is.GreaterThan(-(step * H) - 1e-4f),
                    $"at water {water:0.00} m the foot has settled {-above:0.000} u into the deck, more " +
                    $"than one {step:0.00} m rung of the ladder. Either the ladder does not cover this " +
                    "tide or the rung choice has stopped rounding up");

                Assert.That(-above, Is.LessThan(raftHullUnits),
                    $"at water {water:0.00} m the foot settles {-above:0.000} u into a raft that draws " +
                    $"only {raftHullUnits:0.000} u of hull below her deck — the residual has stopped " +
                    "being covered by the thing the brow lands on, and the ladder needs more rungs");
            }
        }

        /// <summary>
        /// ⭐ The correction, stated as its own number so a regression names itself: the float's picture
        /// moves by exactly the apron's own drawn height, and that is a CONSTANT — the same at spring
        /// low as at spring high, which is what proves it is an anchor and not a ride.
        /// </summary>
        [Test]
        public void TheFloatsPlacementCorrectionIsAConstant_AndItIsTheAproneOwnDrawnHeight()
        {
            // What #735 drew: the pivot on the plan line, baked deck quoted in the RIG's frame.
            float Shipped(float deck) => PlanY
                + FloatingPlatformVisual.ScreenRise(deck, NineMileCreekQuayFace.BakedRigFloatDeckZMetres)
                + NineMileCreekQuayFace.BakedRigFloatDeckZMetres * H;

            float first = Shipped(DeckAt(SpringLow)) - DrawnFloatDeckY(DeckAt(SpringLow));
            foreach (float water in new[] { SpringLow, -1f, 0f, 1f, SpringHigh })
            {
                float deck = DeckAt(water);
                Assert.That(Shipped(deck) - DrawnFloatDeckY(deck), Is.EqualTo(first).Within(1e-4f),
                    "the correction must be the same at every tide — a residual that varies with the " +
                    "water would be a ride, not an anchor, and #735's ride is correct and untouched");
            }
            Assert.That(first, Is.EqualTo(ApronZ * H).Within(1e-3f),
                "the correction is the apron's own 3.00 m of drawn height, which is what it means for " +
                "the two pieces to have been measuring from different zeros");
        }

        /// <summary>The frame half of it, pinned on its own: the rig's datum is LOWEST water and the
        /// game's is MEAN, and the number the visual compares against must be in the frame
        /// <c>DeckElevationNow</c> answers in.</summary>
        [Test]
        public void TheBakedFloatDeckIsQuotedInTheFrameTheSimAnswersIn()
        {
            Assert.That(NineMileCreekQuayFace.BakedFloatDeckGameMetres,
                Is.EqualTo(NineMileCreekQuayFace.BakedRigFloatDeckZMetres + SpringLow).Within(1e-4f),
                "the conversion is ToRigZ backwards, and nothing else");
            Assert.That(NineMileCreekQuayFace.ToRigZ(NineMileCreekQuayFace.BakedFloatDeckGameMetres),
                Is.EqualTo(NineMileCreekQuayFace.BakedRigFloatDeckZMetres).Within(1e-4f),
                "…and it must round-trip through the region's own one conversion");
            Assert.That(NineMileCreekQuayFace.BakedFloatDeckGameMetres,
                Is.Not.EqualTo(NineMileCreekQuayFace.BakedRigFloatDeckZMetres).Within(0.1f),
                "the two frames differ by an amplitude — if they ever coincide this test has stopped " +
                "being able to catch the mix-up it was written for");
        }

        // =============================================================================================
        //  2. THE LADDER — the rig's, covering this coast, and rounded the one way that hides
        // =============================================================================================

        [Test]
        public void TheLadderIsDerivedFromTheSameThreeNumbersTheRigUses()
        {
            var drops = NineMileCreekQuayFace.GangwayRungDrops;
            Assert.That(drops.Count, Is.EqualTo(NineMileCreekQuayFace.GangwayRungCount));
            Assert.That(drops.Count, Is.EqualTo(9), "nine rungs — see the rig's GANGWAY_RUNGS");

            Assert.That(drops[0], Is.EqualTo(NineMileCreekQuayFace.BakedRigClearance
                                             - NineMileCreekQuayFace.BakedRigFloatFreeboard).Within(1e-4f),
                "the shallowest rung is the drop at HIGHEST water: clearance − freeboard");
            Assert.That(drops[drops.Count - 1],
                Is.EqualTo(NineMileCreekQuayFace.BakedRigTideRange
                           + NineMileCreekQuayFace.BakedRigClearance
                           - NineMileCreekQuayFace.BakedRigFloatFreeboard).Within(1e-4f),
                "the deepest is the drop at LOWEST water: tideRange + clearance − freeboard");

            for (int i = 1; i < drops.Count; i++)
                Assert.That(drops[i], Is.GreaterThan(drops[i - 1]),
                    "the ladder must ascend — GangwayRungFor walks it in order and would answer nonsense");

            // ⭐ An EXACT tie must land on its own rung, not a step past it. This is the case the 09-06
            // plate run caught in the real region: the ladder and the drop are derived from the same
            // three numbers by two routes, so rung 4 came out 4.4e-16 m under a 2.600 m drop and a
            // strict comparison bought a full 0.55 m step of foot-in-the-planks for nothing.
            for (int i = 0; i < drops.Count; i++)
                Assert.That(NineMileCreekQuayFace.GangwayRungFor(drops[i]), Is.EqualTo(i),
                    $"a drop of exactly rung {i}'s own {drops[i]:0.000} m did not choose rung {i}");

            float step = drops[1] - drops[0];
            for (int i = 1; i < drops.Count; i++)
                Assert.That(drops[i] - drops[i - 1], Is.EqualTo(step).Within(1e-4f),
                    "the ladder is evenly spaced; an uneven one would need its worst step stated");
        }

        /// <summary>
        /// The ladder has to reach every drop THIS coast can produce, at both ends, or the clamp in
        /// <c>GangwayRungFor</c> silently draws the wrong slope at the extremes — which is a defect that
        /// only appears at spring tides, i.e. exactly when someone is not looking.
        /// </summary>
        [Test]
        public void TheLadderCoversEveryDropThisCoastCanProduce()
        {
            var drops = NineMileCreekQuayFace.GangwayRungDrops;
            float shallowest = ApronZ - DeckAt(SpringHigh);
            float deepest = ApronZ - DeckAt(SpringLow);

            Assert.That(shallowest, Is.GreaterThanOrEqualTo(drops[0] - 1e-4f),
                $"at spring high the real drop is {shallowest:0.00} m, shallower than the ladder's " +
                $"shallowest rung ({drops[0]:0.00} m) — the brow would be drawn steeper than it is");
            Assert.That(deepest, Is.LessThanOrEqualTo(drops[drops.Count - 1] + 1e-4f),
                $"at spring low the real drop is {deepest:0.00} m, deeper than the ladder reaches " +
                $"({drops[drops.Count - 1]:0.00} m) — the brow would be drawn flatter than it is, and " +
                "its foot would hang over the planks");
        }

        /// <summary>The rung choice is pure and lives in TWO places that must agree — the region's, which
        /// the builder uses, and the runtime component's, which the game uses. One rule, asserted across
        /// the whole ladder plus its two open ends.</summary>
        [Test]
        public void TheRegionAndTheRuntimePickTheSameRung()
        {
            var drops = NineMileCreekQuayFace.GangwayRungDrops;
            var asArray = new float[drops.Count];
            for (int i = 0; i < drops.Count; i++) asArray[i] = drops[i];

            for (int i = 0; i <= 60; i++)
            {
                float drop = Mathf.Lerp(-1f, 6f, i / 60f);   // deliberately past BOTH ends of the ladder
                Assert.That(GangwayVisual.RungFor(asArray, drop),
                            Is.EqualTo(NineMileCreekQuayFace.GangwayRungFor(drop)),
                            $"the two rung rules disagree at a drop of {drop:0.00} m");
            }

            Assert.That(GangwayVisual.RungFor(new float[0], 1f), Is.EqualTo(-1),
                "no rungs is not rung zero — a component with no ladder must draw nothing rather than " +
                "the ramp from one end of the tide");
        }

        [Test]
        public void TheDrawnBrowIsTheWalkableBrow()
        {
            Assert.That(NineMileCreekQuayFace.BakedRigGangwayRunMetres,
                Is.EqualTo(NineMileCreekWharf.GangwayRunMetres).Within(1e-3f),
                "the cell is baked at 12 m of plan run and the walkable segment is a different length — " +
                "ADR 0010's render==sim, and a ramp the player walks off the end of");
        }

        // =============================================================================================
        //  3. THE PILES — the half that does not ride
        // =============================================================================================

        [Test]
        public void ThePilesTakeTheRaftsOwnPlanPositions_AndNothingMovesThem()
        {
            var raft = NineMileCreekWharf.FloatCourses();
            var piles = NineMileCreekWharf.FloatPilesCourses();

            Assert.That(piles.Count, Is.EqualTo(raft.Count),
                "one set of piles per bay, or a lengthened run grows dock without growing what holds it");
            for (int i = 0; i < piles.Count; i++)
            {
                Assert.That(piles[i].Position, Is.EqualTo(raft[i].Position),
                    $"pile set {i} has drifted off the bay it guides");
                Assert.That(piles[i].Key, Is.EqualTo(NineMileCreekQuayFace.FloatPilesCourseKey));
            }
        }

        [Test]
        public void ThePilesDrawUnderTheRaft_AndTheCostIsStated()
        {
            Assert.That(NineMileCreekWharf.FloatPilesSortingOrder,
                Is.LessThan(NineMileCreekWharf.FloatSortingOrder),
                "a guide pile stands on the raft's NORTH side, away from this camera, so the raft draws " +
                "over it");
            Assert.That(NineMileCreekWharf.FloatPilesSortingOrder,
                Is.GreaterThan(SortingBands.Sea),
                "the piles are structure, not seabed — the water does not draw over them");
            Assert.That(NineMileCreekWharf.FloatPilesSortingOrder,
                Is.InRange(SortingBands.WharfDeckMin, SortingBands.WharfDeckMax));
        }

        // =============================================================================================
        //  4. SORTING — the brow has to be over BOTH things it touches
        // =============================================================================================

        [Test]
        public void TheBrowDrawsOverTheApronsFaceAndOverTheRaftItLandsOn()
        {
            int brow = NineMileCreekWharf.GangwaySortingOrder;

            Assert.That(brow, Is.GreaterThan(
                    NineMileCreekDressing.FaceSortingOrder(NineMileCreekDressing.WestWallRun)),
                "the hinge is bolted to the apron's east face, so the brow draws on it");
            Assert.That(brow, Is.GreaterThan(NineMileCreekWharf.FloatSortingOrder),
                "the foot RESTS ON the raft's deck — a thing that lands on a deck is drawn on it");
            Assert.That(brow, Is.LessThan(SortingBands.DecorFloor),
                "the brow is structure at the water's edge, not decor: anything Y-sorted standing on it " +
                "must be able to draw over it");
            Assert.That(brow, Is.InRange(SortingBands.WharfDeckMin, SortingBands.WharfDeckMax));
        }

        // =============================================================================================
        //  5. THE RIG STILL SAYS IT — the tripwire, scraped from the source the pixels came from
        // =============================================================================================

        [Test]
        public void TheRigStillSplitsTheRaftFromItsSeabedFurniture()
        {
            string rig = RigSource();

            StringAssert.Contains("timberFloat:  { family:'float',  hull:'timber', guidePiles:false, chain:false, pileHoops:true }", rig,
                "timberFloat has taken its guide piles and its mooring chain back into the raft's own " +
                "cell — which is #735's named lie, and at this wharf it puts 4.4 m of tidal travel into " +
                "piles that are driven into the bottom — or it has stopped drawing the sliding COLLARS, " +
                "which belong with the raft they are bolted to");

            // ⭐ THE HOOPS RIDE. A guide hoop is a sliding collar on the raft, not part of the pile it
            // runs on. Split on the rig's `fixed` tag alone it went into the standing half, and the
            // 2026-09-06 low-water plate drew sixteen collars hanging three units over a dock that had
            // gone down the piles without them. The gate has to be the RAFT's.
            StringAssert.Contains("if(s.raft && s.pileHoops) for", rig,
                "the guide hoops are no longer drawn with the raft — either they are back on the pile " +
                "(so they stand still while the dock rides down past them) or they are drawn with no " +
                "raft at all");
            StringAssert.Contains("pileHoops:false, fittings:", rig,
                "floatPiles has taken the sliding collars back — it is the STANDING half, and a collar " +
                "that stands is the defect this preset exists to end");
            StringAssert.Contains("floatPiles:   { family:'float',  hull:'timber', raft:false", rig,
                "the fixed half of the float has gone from the rig's presets — nothing then draws the " +
                "guide piles, the chain or its anchor block at all");
            StringAssert.Contains("gangway:      { family:'gangway', run:12 }", rig,
                "the brow's preset moved. The run is this wharf's own 12 m gap and the drawn ramp must " +
                "be the walkable one (ADR 0010)");
            StringAssert.Contains("const GANGWAY_RUNGS = 9;", rig,
                "the rig's rung count moved — the sheet, the contract and this region all bake against " +
                "it, and a mismatch slices the ladder wrongly rather than failing");
            StringAssert.Contains("const lo = s.clearance - s.freeboard, hi = s.tideRange + s.clearance - s.freeboard", rig,
                "the rig no longer derives the ladder's ends from clearance, freeboard and tideRange — " +
                "NineMileCreekQuayFace.GangwayRungDrops recomputes exactly that expression, and a " +
                "hard-coded ladder in either place is a coast this pack was not baked for");
            StringAssert.Contains("s.deckZ - gangwayDrops(s)[s.rung]", rig,
                "the rung no longer reads the ladder against the hinge the auto already solved — a " +
                "second literal there is how the two ends of a ramp stop agreeing");
        }
    }
}
