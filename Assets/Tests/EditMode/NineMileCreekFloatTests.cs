using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE FLOATING DOCK AT NINE MILE CREEK</b> — S2 of the wet-wharf arc, and the thing S1's channel
    /// was cut to carry.
    ///
    /// <para>S1 dredged a meandering lane that keeps water in the bullpen at spring low, and routed it so
    /// that <i>"every metre of the float (y = 70, x = 104 → 152) lies within 3 m of the centre-line"</i>.
    /// This file is the other end of that sentence: the float is now a real deck, and these tests measure
    /// whether the lane actually floats it — <b>including under her edges</b>, which the channel pass could
    /// not check because there was no float to have edges.</para>
    ///
    /// <para><b>Every number is traced, and none is typed.</b> The freeboard and the draught are the wharf
    /// rig's own committed defaults, scraped from <c>wharfIsoRig.js</c> here so that a re-bake which
    /// re-shapes the float cannot leave the SIM standing on planks the PIXELS no longer draw — the same
    /// tripwire discipline <c>NineMileCreekQuayFace</c> runs on the quay face, and the same scrape
    /// <c>LadderBoardingConfigTests</c> uses to keep a choice from drifting. The bed is measured off the
    /// authored terrain. The brow's run is the gap the geography leaves.</para>
    ///
    /// <para>The mechanism itself — deck = tide, grounding, the brow's lerp, the constant drop at a float
    /// cleat — is <c>FloatingPlatformTests</c>' subject. This file is about THIS harbour.</para>
    /// </summary>
    public class NineMileCreekFloatTests
    {
        // =============================================================================================
        //  ⭐⭐ THE PICTURE OF HER — owner, 2026-09-04: "there should be a floating dock with a gangway
        //  too with boats moored". She was right, and she was looking at the reason: the dock and the
        //  brow were built as SURFACES with no sprite at all, so the two small craft on the fingers lay
        //  in open water.
        // =============================================================================================

        [Test]
        public void TheFloatRunIsDrawnAsAWholeNumberOfTheRigsOwnBays()
        {
            var courses = NineMileCreekWharf.FloatCourses();
            Assert.That(courses, Is.Not.Empty, "the float run is undrawn again");

            float bay = NineMileCreekQuayFace.BakedRigFloatRunMetres;
            Assert.That(NineMileCreekWharf.FloatRunRemainderMetres, Is.EqualTo(0f).Within(1e-3f),
                $"the {NineMileCreekMainland.FloatRunLengthMetres:0.#} m run is not a whole number of " +
                $"{bay:0.#} m bays — {NineMileCreekWharf.FloatRunRemainderMetres:0.##} m of dock you can " +
                "walk on has no picture. Re-cut the run to a multiple of the bay, or the pack needs a " +
                "shorter float preset; a stretched cell is not an option");

            // Butted end to end, west to east, each centred in its own bay.
            for (int i = 0; i < courses.Count; i++)
            {
                Assert.That(courses[i].Position.y,
                    Is.EqualTo(NineMileCreekMainland.FloatRunY).Within(1e-3f),
                    "a float bay has left the run's own line");
                Assert.That(courses[i].Position.x,
                    Is.EqualTo(NineMileCreekMainland.FloatRunWestX + bay * (i + 0.5f)).Within(1e-3f),
                    $"float bay {i} is not centred in its own bay");
                Assert.That(courses[i].Key, Is.EqualTo(NineMileCreekQuayFace.FloatCourseKey));
            }

            // …and the run they cover is the run the SURFACE claims, so the planks drawn and the planks
            // stood on are the same planks.
            Rect box = NineMileCreekWharf.FloatFootprint();
            Assert.That(courses[0].Position.x - bay * 0.5f, Is.EqualTo(box.xMin).Within(1e-3f));
            Assert.That(courses[courses.Count - 1].Position.x + bay * 0.5f,
                        Is.EqualTo(box.xMax).Within(1e-3f));
        }

        [Test]
        public void TheDrawnFloatRidesTheSameDeckTheSurfaceStandsYouOn()
        {
            // ⭐ THE ONE NUMBER A DRAWN FLOAT CARRIES. The sprite was baked with her lying at ONE water
            // level; the sim drives her from the live tide. If the two ever disagree the player stands on
            // planks the picture has moved out from under — which is the defect FloatingPlatform's own
            // doc comment warns about, from the other side.
            Assert.That(NineMileCreekQuayFace.BakedRigWaterLevelMetres, Is.EqualTo(2.42f).Within(1e-3f),
                "the pack's contract records tide.baked = 2.42; the rig's own rule is " +
                "s.tide = s.tideRange * 0.55. If this has moved the pack was re-baked at another coast " +
                "and every drawn float in the game is riding from the wrong datum");
            Assert.That(NineMileCreekQuayFace.BakedRigFloatDeckZMetres,
                Is.EqualTo(NineMileCreekQuayFace.BakedRigWaterLevelMetres +
                           NineMileCreekQuayFace.BakedRigFloatFreeboard).Within(1e-4f),
                "the drawn deck is the rig's own s.floatDeckZ = s.tide + s.freeboard, not a second rule");

            // At the tide she was drawn at, the picture belongs exactly where the builder put it.
            Assert.That(FloatingPlatformVisual.ScreenRise(
                            NineMileCreekQuayFace.BakedRigFloatDeckZMetres,
                            NineMileCreekQuayFace.BakedRigFloatDeckZMetres),
                        Is.EqualTo(0f).Within(1e-5f));

            // And a metre of deck is a metre of HEIGHT — 0.766 up the screen, not the 0.643 a metre of
            // northward GROUND draws. Nineteen per cent, and indistinguishable from "the art is a bit off".
            Assert.That(FloatingPlatformVisual.ScreenRise(
                            NineMileCreekQuayFace.BakedRigFloatDeckZMetres + 1f,
                            NineMileCreekQuayFace.BakedRigFloatDeckZMetres),
                        Is.EqualTo(IsoGround.HeightScale).Within(1e-5f));
            Assert.That(IsoGround.HeightScale, Is.Not.EqualTo(IsoGround.GroundDepthScale).Within(0.01f));
        }

        [Test]
        public void TheDrawnFloatSitsUnderEveryHullThatTiesToIt_AndOverTheApronFaceTheBrowLandsOn()
        {
            int order = NineMileCreekWharf.FloatSortingOrder;

            Assert.That(order, Is.LessThan(SortingBands.DecorFloor),
                "the dock draws inside the Y-sort decor band, so anything standing on it — the player, a " +
                "villager — is behind the planks at half the positions on it");
            Assert.That(order, Is.GreaterThan(SortingBands.Sea),
                "the dock draws at or under the sea plane and the water is over it");
            Assert.That(order, Is.InRange(SortingBands.WharfDeckMin, SortingBands.WharfDeckMax));

            // The two customers, in the same argument the quay face's rungs are picked by.
            Assert.That(order, Is.GreaterThan(
                NineMileCreekDressing.FaceSortingOrder(NineMileCreekDressing.WestWallRun)),
                "the brow lands ON the apron's east face, so the dock has to draw over it");

            foreach (var owner in NineMileCreekMooredFleet.LoadOwners())
            {
                var visual = owner != null && owner.Boat != null ? owner.Boat.Visual : null;
                if (visual == null) continue;
                Assert.That(visual.SortingOrder, Is.GreaterThan(order),
                    $"a hull composites at {visual.SortingOrder} and the dock draws at {order} — a boat " +
                    "tied to the float would be drawn inside the planks she is tied to");
            }
        }

        private GameObject _go;
        private MainlandTidalTerrain _terrain;

        private static float SpringLow => NineMileCreekMainland.SpringLowWater;     // −2.2
        private static float SpringHigh => NineMileCreekMainland.SpringHighWater;   //  2.2
        private static float Shoal => NineMileCreekMainland.BasinBedElevation;      // −1.6
        private static float Freeboard => NineMileCreekQuayFace.BakedRigFloatFreeboard;
        private static float Draught => NineMileCreekQuayFace.BakedRigFloatDraughtMetres;

        /// <summary>A terrain whose every point sits at one height — for the WIRING tests, which are about
        /// what the builder built and not about the coast plan.</summary>
        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation = 3.0f;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        [SetUp]
        public void SetUp()
        {
            DestroyAnyWharf();
            StandableSurfaces.Clear();
            MooringCleats.Clear();
            _go = new GameObject("NineMileCreekFloat_Test");
            _terrain = _go.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            DestroyAnyWharf();
            StandableSurfaces.Clear();
            MooringCleats.Clear();
            GameServices.Reset();
        }

        /// <summary>Tear the builder's wharf out BY NAME — a fixture's cleanup must not depend on the thing
        /// it is testing having worked (<c>NineMileCreekLadderTests</c>' rule, and its reason).</summary>
        private static void DestroyAnyWharf()
        {
            for (int guard = 0; guard < 8; guard++)
            {
                GameObject root = GameObject.Find(NineMileCreekWharf.RootName);
                if (root == null) return;
                Object.DestroyImmediate(root);
            }
        }

        private float Elev(Vector2 p) => _terrain.ElevationAt(p);

        private static string RigSource() => File.ReadAllText(Path.Combine(
            Application.dataPath, "..", "docs/art/rigs/iso-rig-pack/wharf-kit-iso/wharfIsoRig.js"));

        // =============================================================================================
        //  1. THE NUMBERS ARE THE ART'S — scraped, so a re-bake cannot drift the sim off the pixels
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>THE TRIPWIRE.</b> Every float dimension the sim uses is quoted from the rig, and this is
        /// the test that fails the day the rig stops saying it. A float re-baked with more foam under it
        /// draws deeper and would ground earlier; if the sim kept the old draught the player would stand
        /// on a dock the picture has already put on the mud, and nothing else in the project would notice.
        /// </summary>
        [Test]
        public void EveryFloatDimensionIsStillTheRigsOwn_ScrapedFromItsSource()
        {
            string rig = RigSource();

            StringAssert.Contains("freeboard: 0.40", rig,
                "wharfIsoRig.js no longer defaults a float's freeboard to 0.40 m — that is the height the " +
                "committed sheets draw her deck at, and NineMileCreekQuayFace.BakedRigFloatFreeboard is " +
                "quoting it. Re-quote it, and re-measure whether she still floats.");
            StringAssert.Contains("s.floatDeckZ = s.tide + s.freeboard", rig,
                "the rig no longer states deck = tide + freeboard, which is the whole rule " +
                "World.FloatingPlatform implements");
            StringAssert.Contains("t = 0.05, frameD = 0.26", rig,
                "the timber float's deck plank + frame depth moved — her draught is derived from them");
            StringAssert.Contains("bh = 0.42", rig,
                "the flotation billets' depth moved — that is the bottom of her, and what she grounds on");
            StringAssert.Contains("const z1 = fz0 + 0.02", rig,
                "the billets are no longer slung 0.02 m up into the frame — the draught chain has changed");
            StringAssert.Contains("width:[1.8,3.6,2.4]", rig,
                "the float family's width default moved — the walkable footprint is cut to it, and a " +
                "footprint wider than the drawing is a player standing on water");
            StringAssert.Contains("gangWidth: 0.95", rig,
                "the rig's gangway width moved — the brow's walkable half-width is half of it");
            StringAssert.Contains("(T.R + s.freeboard + 0.9) * 2.4", rig,
                "the rig's own auto gangway run changed — this region reports how far its authored brow " +
                "falls short of it, and that comparison must stay against the live rule");
        }

        /// <summary>Her draught is the billets the freeboard is drawn above — 0.71 m of float less 0.40 m
        /// of it standing proud = 0.31 m in the water. Arithmetic, so a deeper billet grounds her earlier
        /// on its own.</summary>
        [Test]
        public void TheDraughtIsDerivedFromTheBillets_NotTyped()
        {
            Assert.That(NineMileCreekQuayFace.BakedRigFloatHullDepthMetres,
                        Is.EqualTo(0.71f).Within(1e-4f), "0.05 plank + 0.26 frame − 0.02 rise + 0.42 billet");
            Assert.That(Draught, Is.EqualTo(0.31f).Within(1e-4f), "…less the 0.40 m she carries above water");
            Assert.That(NineMileCreekQuayFace.BakedRigFloatHullDepthMetres - Freeboard,
                        Is.EqualTo(Draught).Within(1e-6f), "the draught must BE the derivation");
        }

        // =============================================================================================
        //  2. THE WATER UNDER HER — S1's channel, measured from the float's side
        // =============================================================================================

        /// <summary>
        /// The bed she grounds on is the SHALLOWEST ground under her, not the middle. Asserted against an
        /// independent finer walk of the same footprint, so the builder's sampling cannot quietly miss a
        /// high spot under one edge and float her through it.
        /// </summary>
        [Test]
        public void TheBedIsMeasuredAsTheShallowestPointUnderHer_NotTheCentreLine()
        {
            float measured = NineMileCreekWharf.FloatBedElevationFrom(_terrain);
            Rect box = NineMileCreekWharf.FloatFootprint();

            float finest = float.NegativeInfinity;
            for (float x = box.xMin; x <= box.xMax + 1e-4f; x += 0.25f)
                for (float y = box.yMin; y <= box.yMax + 1e-4f; y += 0.2f)
                    finest = Mathf.Max(finest, Elev(new Vector2(x, y)));

            // The coarse grid's samples are a SUBSET of the fine one's (1.0 is a whole number of 0.25s
            // and of 0.2s, and both walks pin their last row and column onto the edges), so the builder
            // can only ever under-report. What matters is by how much: the shallowest point under this
            // float is a CORNER, which both walks land on exactly, so the honest expectation is nothing
            // at all — and the tolerance is there for the day the lane moves and the maximum slides into
            // the interior, where a metre of step could genuinely miss something.
            Assert.That(measured, Is.LessThanOrEqualTo(finest + 1e-3f),
                "a subset of samples cannot find higher ground than the superset — check the walk");
            Assert.That(measured, Is.GreaterThanOrEqualTo(finest - 0.05f),
                $"the builder measured {measured:0.000} m and a four-times-finer walk of the same " +
                $"footprint finds {finest:0.000} m — the sampling step is missing ground that matters");

            float onTheLine = Elev(new Vector2(box.center.x, NineMileCreekMainland.FloatRunY));
            Assert.That(measured, Is.GreaterThanOrEqualTo(onTheLine - 1e-4f),
                "the controlling bed can never be DEEPER than a point inside the footprint — if it is, " +
                "the measurement is not the shallowest point and a rigid raft would sink into a shoal");
        }

        /// <summary>
        /// ⭐ <b>THE HEADLINE: she floats at dead low spring, and this is by how much.</b> S1 cut the lane
        /// for the fleet's draughts; the float is the one thing on it that cannot move out of the way, and
        /// a dock aground for part of every big tide would tilt, part its gangway from its abutment, and
        /// be the least cozy thing on this coast.
        /// </summary>
        [Test]
        public void SheFloatsAtDeadLowSpring_WithTheMarginStated()
        {
            float bed = NineMileCreekWharf.FloatBedElevationFrom(_terrain);
            float depth = SpringLow - bed;

            Assert.That(FloatingPlatform.IsAground(SpringLow, bed, Draught), Is.False,
                $"at spring low ({SpringLow:0.00} m) the shallowest bed under her is {bed:0.00} m — " +
                $"{depth:0.00} m of water against a {Draught:0.00} m draught. She is on the bottom.");

            Assert.That(depth - Draught, Is.GreaterThan(0.25f),
                $"she floats by only {depth - Draught:0.00} m at spring low. That is inside the noise of " +
                "a terrain edit, so treat it as a warning rather than a pass: widen or deepen the lane " +
                "under the float run before the next pass shoals it by accident.");
        }

        /// <summary>…and the lane really is what does it. Under the float the bed is BELOW the −1.6 m
        /// shoal everywhere, which is only true because the channel was routed through her.</summary>
        [Test]
        public void TheChannelIsWhatFloatsHer_TheShoalAloneWouldNot()
        {
            Rect box = NineMileCreekWharf.FloatFootprint();
            for (float x = box.xMin; x <= box.xMax + 1e-4f; x += 2f)
            {
                var at = new Vector2(x, NineMileCreekMainland.FloatRunY);
                Assert.That(Elev(at), Is.LessThan(Shoal - 0.5f),
                    $"at {at} the bed is {Elev(at):0.00} m — barely under the {Shoal:0.00} m shoal, so " +
                    "the channel has stopped reaching the float run and she is riding on the gate");
            }

            // The counterfactual, stated: on the bare shoal she would ground for part of every big tide.
            Assert.That(FloatingPlatform.IsAground(SpringLow, Shoal, Draught), Is.True,
                "if a float on the −1.6 m shoal did NOT ground at spring low, this whole pass is " +
                "measuring nothing and the channel was not needed to float her");
        }

        /// <summary>Her deck swings the region's whole range, and never touches the apron at the top of
        /// it: at spring high she is still 0.40 m BELOW the quay, so you always step DOWN onto the brow.
        /// That is the rig's clearance-versus-freeboard relation, and a float that rose past its own
        /// abutment would push the brow up at the wrong end.</summary>
        [Test]
        public void HerDeckSwingsTheWholeRange_AndNeverRisesPastTheQuayItHangsOff()
        {
            float bed = NineMileCreekWharf.FloatBedElevationFrom(_terrain);
            float low = FloatingPlatform.DeckElevation(SpringLow, bed, Draught, Freeboard);
            float high = FloatingPlatform.DeckElevation(SpringHigh, bed, Draught, Freeboard);

            Assert.That(high - low, Is.EqualTo(SpringHigh - SpringLow).Within(1e-3f),
                "a float that is never aground swings exactly the tide's range — 4.4 m here");
            Assert.That(low, Is.EqualTo(SpringLow + Freeboard).Within(1e-3f));

            float apron = NineMileCreekWharf.ApronElevationFrom(_terrain);
            Assert.That(high, Is.LessThan(apron),
                $"at spring high her deck is {high:0.00} m against the apron's {apron:0.00} m — a float " +
                "that rode above its own abutment would hinge the brow upward");
            Assert.That(apron - high, Is.EqualTo(NineMileCreekQuayFace.BakedRigClearance - Freeboard)
                                        .Within(1e-3f),
                "the smallest step down onto the brow is the quay's clearance less the float's freeboard " +
                "— both the rig's own numbers, so the two structures were baked to meet");
        }

        // =============================================================================================
        //  3. THE BROW, AND WHERE EVERYTHING SITS
        // =============================================================================================

        /// <summary>The brow spans the actual gap: its hinge is ON the apron and its landing is ON the
        /// float, so there is no stretch of water at either end that is neither.</summary>
        [Test]
        public void TheBrowHingesOnTheApronAndLandsOnTheFloat()
        {
            Assert.That(StandablePlatform.Contains(NineMileCreekWharf.ApronFootprint(),
                                                   NineMileCreekWharf.GangwayShoreEnd), Is.True,
                "the hinge must be on the apron, or it is bolted to the basin");
            Assert.That(StandablePlatform.Contains(NineMileCreekWharf.FloatFootprint(),
                                                   NineMileCreekWharf.GangwayFloatEnd), Is.True,
                "the landing must be on the float, or the last step is into the water");
            Assert.That(NineMileCreekWharf.GangwayRunMetres, Is.EqualTo(12f).Within(1e-3f),
                "the gap between the apron's east face (x = 92) and the float's west end (x = 104)");
        }

        /// <summary>
        /// ⚠ <b>AND IT IS SHORTER THAN THE ART WOULD HAVE CHOSEN.</b> The rig solves its own brow at
        /// <c>(tideRange + freeboard + 0.9) × 2.4</c> = 13.68 m for this coast; the geography leaves 12.
        /// So the walk down is steeper than the kit's own rule — reported here rather than fixed, because
        /// lengthening it means moving the float east and S1's meander is pinned to the run's present ends.
        /// </summary>
        [Test]
        public void TheBrowIsShorterThanTheRigsOwnRule_AndTheCostIsStated()
        {
            float authored = NineMileCreekWharf.GangwayRunMetres;
            float wanted = NineMileCreekQuayFace.RigAutoGangwayRunMetres;

            Assert.That(wanted, Is.EqualTo(13.68f).Within(0.01f),
                "the rig's auto run for a 4.4 m range and a 0.40 m freeboard");
            Assert.That(authored, Is.LessThan(wanted),
                "if the authored brow is no longer shorter than the rig's rule this finding is stale and " +
                "the note on NineMileCreekWharf.GangwayRunMetres should come out");
            Assert.That(authored, Is.GreaterThanOrEqualTo(3f),
                "…but it must stay inside the rig's hard 3–14 m clamp, or the kit cannot draw it at all");

            // The honest consequence, in the units a person feels: 1 : 2.5 at the bottom of the tide.
            float bed = NineMileCreekWharf.FloatBedElevationFrom(_terrain);
            float drop = NineMileCreekWharf.ApronElevationFrom(_terrain)
                       - FloatingPlatform.DeckElevation(SpringLow, bed, Draught, Freeboard);
            Assert.That(drop / authored, Is.EqualTo(0.4f).Within(0.01f),
                $"the brow falls {drop:0.00} m over {authored:0.0} m at dead low spring");
        }

        /// <summary>Every float cleat stands on the float. A tie-off hanging off the end is a rope made
        /// fast to the sea.</summary>
        [Test]
        public void EveryFloatCleatStandsOnTheFloat()
        {
            Rect box = NineMileCreekWharf.FloatFootprint();
            var cleats = NineMileCreekWharf.FloatCleatPositions();
            Assert.That(cleats.Count, Is.GreaterThanOrEqualTo(4), "a 48 m float run with fewer than four " +
                "places to tie is not a berth");
            foreach (Vector2 at in cleats)
                Assert.That(StandablePlatform.Contains(box, at), Is.True, $"the cleat at {at} is off the float");
        }

        /// <summary>⭐ <b>THE RULED GATE IS STILL UNTOUCHED.</b> S1's whole discipline was that the lane is
        /// cut PAST the berths, not through them; the float must not quietly re-open that question by
        /// overlapping the berth line, the dock zone or the slipway.</summary>
        [Test]
        public void TheFloatDoesNotReachTheBerthsTheDockZoneOrTheSlipway()
        {
            Rect box = NineMileCreekWharf.FloatFootprint();

            for (int i = 0; i < NineMileCreekMainland.BerthCount; i++)
                Assert.That(StandablePlatform.Contains(box, NineMileCreekMainland.BerthPos(i)), Is.False,
                    $"berth {i} is inside the float's footprint — the fleet cannot moor on the dock");

            var dockZone = new Vector2(NineMileCreekMainland.DockZonePos.x, NineMileCreekMainland.DockZonePos.y);
            Assert.That(box.yMax + NineMileCreekMainland.DockZoneRadius, Is.LessThan(dockZone.y),
                "the float reaches into the unloading zone");

            // All four corners, not the middle of one edge: a rectangle can clear a ramp at its centre
            // and still have a corner sitting on it.
            foreach (Vector2 corner in new[]
                     {
                         new Vector2(box.xMin, box.yMin), new Vector2(box.xMax, box.yMin),
                         new Vector2(box.xMin, box.yMax), new Vector2(box.xMax, box.yMax),
                     })
                Assert.That(MainlandTidalTerrain.DistanceToSegment(
                                corner,
                                NineMileCreekMainland.SlipwayHeadPos, NineMileCreekMainland.SlipwayToePos),
                            Is.GreaterThan(NineMileCreekMainland.SlipwayHalfWidthMetres),
                    $"the float's corner at {corner} lies on the boatyard's slipway");
        }

        // =============================================================================================
        //  4. THE WIRING — the builder actually builds it
        // =============================================================================================

        /// <summary>
        /// The float, the brow and the cleats are BUILDER-wired, not scene-wired — the region's scene
        /// rebuild is the owner's and is pending, so anything that only existed because someone dropped it
        /// into the open scene would vanish the moment the builder ran (<c>NineMileCreekLadderTests</c>'
        /// argument, and this pass inherits it).
        /// </summary>
        [Test]
        public void TheBuilderBuildsTheFloatTheBrowAndEveryCleat()
        {
            NineMileCreekWharf.Place(new FlatTerrain { Elevation = -5f });

            var floats = Object.FindObjectsByType<FloatingPlatform>(FindObjectsSortMode.None);
            Assert.That(floats.Length, Is.EqualTo(1), "one float run — the photograph shows two and the " +
                "basin has room for one (the basin-depth finding)");
            FloatingPlatform dock = floats[0];
            Assert.That(dock.Id, Is.EqualTo(NineMileCreekWharf.FloatSurfaceId));
            Assert.That(dock.FreeboardMetres, Is.EqualTo(Freeboard).Within(1e-4f));
            Assert.That(dock.DraughtMetres, Is.EqualTo(Draught).Within(1e-4f));
            Assert.That(dock.BedElevation, Is.EqualTo(-5f).Within(1e-4f),
                "the bed must be MEASURED off the terrain handed in, not defaulted");
            Assert.That(dock.Footprint.width, Is.EqualTo(NineMileCreekMainland.FloatRunLengthMetres).Within(1e-3f));

            var brows = Object.FindObjectsByType<GangwayPlatform>(FindObjectsSortMode.None);
            Assert.That(brows.Length, Is.EqualTo(1));
            Assert.That(brows[0].Landing, Is.SameAs(dock),
                "the brow must land on the float the builder made, or its landing end is a stale height");
            Assert.That(brows[0].AbutmentElevation, Is.EqualTo(-5f).Within(1e-4f),
                "the hinge is the apron's MEASURED deck");
            Assert.That(brows[0].HalfWidthMetres * 2f,
                        Is.EqualTo(NineMileCreekQuayFace.BakedRigGangwayWidthMetres).Within(1e-4f));

            var cleats = Object.FindObjectsByType<FloatCleat>(FindObjectsSortMode.None);
            Assert.That(cleats.Length, Is.EqualTo(NineMileCreekWharf.FloatCleatPositions().Count),
                "every position in the table must become a real cleat — the float you see is the float " +
                "you tie to, which is #451's guarantee applied to the one deck that moves");
            foreach (FloatCleat c in cleats)
                Assert.That(c.Float, Is.SameAs(dock), "a float cleat carrying no float reads datum");
        }

        /// <summary>And the ids are distinct and stable: a surface that claims another's id is a surface a
        /// saved mooring line can be restored onto by mistake.</summary>
        [Test]
        public void TheFloatAndTheBrowCarryTheirOwnIds()
        {
            Assert.That(NineMileCreekWharf.FloatSurfaceId, Is.EqualTo("wharf.nine_mile_creek.float"));
            Assert.That(NineMileCreekWharf.GangwaySurfaceId, Is.EqualTo("wharf.nine_mile_creek.gangway"));
            Assert.That(NineMileCreekWharf.FloatSurfaceId, Is.Not.EqualTo(NineMileCreekWharf.SurfaceId));
            Assert.That(NineMileCreekWharf.FloatSurfaceId, Is.Not.EqualTo(NineMileCreekWharf.ApronSurfaceId));
        }
    }
}
