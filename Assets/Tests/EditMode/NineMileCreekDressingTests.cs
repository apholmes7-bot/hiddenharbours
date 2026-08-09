using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>PHASE B — THE DRESSING OF NINE MILE CREEK.</b> The gear, the services and the tideline that
    /// #462 left to this phase, plus the measurement of the one thing Phase B could NOT do.
    ///
    /// <para><b>These are builder-wiring tests, which is the point.</b> The A-2 handoff names the trap by
    /// name: <i>scene-wired is not builder-wired</i>. A prop dragged into <c>NineMileCreek.unity</c>
    /// survives exactly until the owner rebuilds the scene. So everything below is asserted against the
    /// <b>pure placement tables</b> that <see cref="NineMileCreekBuilder"/> consumes — which means a
    /// rebuild reproduces it, and a test can check it with no art imported at all.</para>
    ///
    /// <para><b>⚠️ Deliberately art-independent where it can be.</b> The sheets are Git-LFS binaries and
    /// the pack bakes to order, so "has this prop got pixels" is a different question from "is this prop
    /// correctly placed" — and only the second one is world-content's. The sorting sweep is done against
    /// the BAND ARITHMETIC (does the decor band resolve this world Y at all — the ADR 0032 saturation
    /// defect) rather than against a loaded sprite, so it holds whether or not the pack has imported. The
    /// one test that needs pixels says so and passes vacuously without them.</para>
    /// </summary>
    public class NineMileCreekDressingTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        private MainlandTidalTerrain MakeCreekTerrain()
        {
            var go = new GameObject("TidalTerrain");
            _spawned.Add(go);
            var terrain = go.AddComponent<MainlandTidalTerrain>();
            NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);
            return terrain;
        }

        private static float SpringHigh => NineMileCreekMainland.SpringHighWater;
        private static float SpringLow => NineMileCreekMainland.SpringLowWater;

        private static List<NineMileCreekDressing.Prop> Props() =>
            NineMileCreekDressing.AllProps().ToList();

        // =============================================================================================
        //  1. THE FACE ARITHMETIC — what this wharf needs, and what the pack now bakes
        // =============================================================================================

        [Test]
        public void TheFaceThisWharfNeeds_IsDerivedFromItsOwnTideAndDeck_NotTypedIn()
        {
            // The rig's frame puts z = 0 at the LOWEST water; the game's datum is mean water. Everything
            // else in this section is wrong by an amplitude if that conversion is.
            Assert.That(NineMileCreekQuayFace.ToRigZ(SpringLow), Is.EqualTo(0f).Within(1e-4f),
                "the rig's chart datum IS this region's lowest water — that is the whole conversion");

            Assert.That(NineMileCreekQuayFace.RequiredTideRangeMetres,
                Is.EqualTo(SpringHigh - SpringLow).Within(1e-4f),
                "the rig's tideRange is datum to highest water: 4.4 m here, twice the amplitude");

            Assert.That(NineMileCreekQuayFace.RequiredClearanceMetres,
                Is.EqualTo(NineMileCreekMainland.WharfDeckElevation - SpringHigh).Within(1e-4f),
                "…and its clearance is the freeboard NineMileCreekMainland authored (0.8 m)");

            // ⭐ THE AGREEMENT THAT MAKES THIS A ONE-PARAMETER PROBLEM. The rig's own auto rule is
            // deckZ = tideRange + clearance. This wharf's authored deck satisfies it exactly, which is
            // why no new geometry is needed — only a bake at this coast's tide.
            Assert.That(NineMileCreekQuayFace.RequiredDeckZMetres,
                Is.EqualTo(NineMileCreekQuayFace.RequiredTideRangeMetres +
                           NineMileCreekQuayFace.RequiredClearanceMetres).Within(1e-4f),
                "the authored deck is exactly the rig's own deckZ = tideRange + clearance. If this ever " +
                "fails, the wharf has been re-authored to something the rig cannot quote and the " +
                "re-bake order in NineMileCreekQuayFace is no longer a simple parameter change");
        }

        [Test]
        public void TheFaceIsMEASUREDOffTheTerrain_SoALoweredWallShowsUpHere()
        {
            var terrain = MakeCreekTerrain();

            Assert.That(NineMileCreekQuayFace.StructuralFaceFrom(terrain),
                Is.EqualTo(NineMileCreekQuayFace.StructuralFaceMetres).Within(0.01f),
                "the drawn face has to be the face the terrain actually stands up — measured, so that a " +
                "terrain edit which lowered the wall changes the bake order instead of leaving this " +
                "class quietly describing a wharf the region no longer has");

            Assert.That(NineMileCreekQuayFace.StructuralFaceMetres,
                Is.LessThan(NineMileCreekQuayFace.RequiredDeckZMetres),
                "the structural face is shorter than the drop to lowest water, because the harbour shoal " +
                "is filled above datum and the last stretch of the rig's frame is under the seabed");
        }

        [Test]
        public void TheShortfallTripwireReadsZERO_BecauseThePackIsBakedForTHISCoast()
        {
            // ⭐⭐ THE TRIPWIRE #471 LEFT, AND THE WHOLE GATE ON THE DRAWN QUAY. It wrote
            // TideShortfallMetres with the note "if it ever goes to zero the pack has been re-baked at
            // this tide". #477 re-parameterised the rig and #478 re-baked all 17 sheets, so it is zero —
            // and it is asserted EXACTLY, not as "small", because the two sides are recomputed from
            // different places: the left from the region's authored tide, the right from the rig's
            // committed defaults. They agree only if the bake really is this coast's.
            //
            // ⚠️ IF THIS FAILS, THE QUAY IS BEING DRAWN AT THE WRONG HEIGHT. That defect renders
            // perfectly and merely looks like a slightly wrong wharf, which is precisely why it is
            // pinned here rather than left to the eye.
            Assert.That(NineMileCreekQuayFace.TideShortfallMetres, Is.EqualTo(0f).Within(1e-4f),
                $"the pack is baked at {NineMileCreekQuayFace.BakedRigTideRange:0.##} m of tide against " +
                $"this region's {NineMileCreekQuayFace.RequiredTideRangeMetres:0.##} m. It was 1.8 for " +
                "the whole of #471 and the quay could not be drawn; re-bake at " +
                $"{NineMileCreekQuayFace.RequiredBakeOptions()} before drawing a wall off these sheets");

            Assert.That(NineMileCreekQuayFace.ClearanceShortfallMetres, Is.EqualTo(0f).Within(1e-4f),
                "…and the freeboard half. Both halves at zero is a stronger statement than the total " +
                "being zero: the bake landed on the right TIDE and the right FREEBOARD, not merely on " +
                "the right sum — and it is the tide that re-pins the growth bands");

            // The decomposition still has to hold, because it is what made the re-bake orderable. The
            // first draft of this test asserted the shortfall WAS the tide difference; CI caught it on
            // the first run (2.4 against 2.6).
            Assert.That(NineMileCreekQuayFace.ShortfallMetres,
                Is.EqualTo(NineMileCreekQuayFace.TideShortfallMetres +
                           NineMileCreekQuayFace.ClearanceShortfallMetres).Within(1e-4f),
                "the shortfall has to be the sum of its two halves, or one of the three is wrong");
            Assert.That(NineMileCreekQuayFace.ShortfallMetres, Is.EqualTo(0f).Within(1e-4f),
                "…and therefore zero. #471 measured it at 2.40 m");

            Assert.That(NineMileCreekQuayFace.BakedDeckZMetres,
                Is.EqualTo(NineMileCreekQuayFace.RequiredDeckZMetres).Within(1e-4f),
                "the baked deck and the authored deck are the same height, which is what makes the face " +
                "one course of a piece rather than a tiling problem");

            Assert.That(NineMileCreekQuayFace.BakedPackCanDrawTheFace(), Is.True,
                "the committed pack can build this wharf's face, in this wharf's material, at this " +
                "wharf's deck height. #471 asserted the opposite and said flipping it would be good news");
        }

        [Test]
        public void TheFaceIsDrawnInONECourse_SoTheFittingsLandAtTheDeck()
        {
            // ⭐ THE HALF A STACK COULD NEVER HAVE DELIVERED. The pack bakes a course's bollards, rings,
            // ladder and hung tyres at ITS deck — so a piece used as a LOWER course puts that furniture
            // halfway up the finished wall, which is what ruled vertical tiling out even before the
            // material argument. One course puts them where a rope goes round them.
            Assert.That(NineMileCreekQuayFace.CoursesNeeded, Is.EqualTo(1),
                "the face needs more than one course, so the wall has a seam in it and a set of fittings " +
                "hanging in the middle of it");
            Assert.That(NineMileCreekQuayFace.DrawnInOneCourse, Is.True);
            Assert.That(NineMileCreekQuayFace.FittingsLandAtTheDeck, Is.True,
                "one course AND the right height — either alone would still hang the pack's bollards " +
                "somewhere other than on the deck");

            // ⭐ …AND THE GROWTH BANDS ARE RE-PINNED, which is the part no stack could ever have fixed:
            // a stacked wall would have worn two sets of them, at the wrong heights, twice. #471 asked
            // for barnacle at 1.76–3.52 m and rockweed at 0.26–1.76 m; the rig bands growth as fractions
            // of the tidal frame, so the one parameter that fixed the height fixed these for free.
            Assert.That(NineMileCreekQuayFace.BarnacleBand.x, Is.EqualTo(1.76f).Within(0.01f));
            Assert.That(NineMileCreekQuayFace.BarnacleBand.y, Is.EqualTo(3.52f).Within(0.01f));
            Assert.That(NineMileCreekQuayFace.RockweedBand.x, Is.EqualTo(0.26f).Within(0.01f));
            Assert.That(NineMileCreekQuayFace.RockweedBand.y, Is.EqualTo(1.76f).Within(0.01f),
                "the growth bands are not where #471 asked for them. They are pinned to the tidal FRAME " +
                "as fractions of the range, so if these are wrong the range is wrong and so is the face");

            // The historical record, kept on purpose: a stack was never blocked by the arithmetic, and
            // saying otherwise would send a future art-director looking for the wrong fix.
            Assert.That(NineMileCreekQuayFace.AStackReachesTheHeight(), Is.True,
                "two sheetCell courses still come to 5.20 m exactly — the arithmetic was never the stop");
            var courseList = NineMileCreekQuayFace.StackableCourses();
            Assert.That(courseList.Count, Is.EqualTo(1),
                "the pack's stackable-course list has changed — re-derive before trusting the record");
            Assert.That(courseList[0].UsableHere, Is.False,
                $"'{courseList[0].Key}' is recorded as usable on this wharf. #462 ruled it sheet pile, " +
                "which this wharf is not built of, and a capped cell has no deck to land a catch on");
        }

        // =============================================================================================
        //  2. THE DRAWN QUAY — the wall #471 measured and could not place
        // =============================================================================================
        // ⚠️ ASSERTED ON THE PURE TABLE, not on loaded sprites, for the reason the class note gives: the
        // wharf sheets are Git-LFS binaries and "has this piece got pixels" is a different question from
        // "is this piece correctly placed". Only the second one is world-content's. The one test that
        // needs pixels says so and passes vacuously without them.

        private static List<NineMileCreekDressing.FacePiece> Face() =>
            NineMileCreekDressing.FacePieces().ToList();

        private static List<NineMileCreekDressing.FacePiece> FaceRun(string wall) =>
            Face().Where(p => p.Wall == wall).ToList();

        /// <summary>A run has to COVER its wall: the right number of pieces, evenly pitched, each half a
        /// pitch inside the ends, and never pitched further apart than a piece is long.</summary>
        private static void AssertRunCovers(string wall, Vector2 from, Vector2 to)
        {
            var run = FaceRun(wall);
            float length = Vector2.Distance(from, to);
            float piece = NineMileCreekQuayFace.FaceCourseRunMetres;

            Assert.That(run, Is.Not.Empty, $"the {wall} has no face on it at all");
            Assert.That(run.Count, Is.EqualTo(Mathf.CeilToInt(length / piece)),
                $"the {wall} is {length:0.#} m of wall drawn with {run.Count} piece(s) of {piece:0.#} m. " +
                "The count is the CEILING of that division on purpose — a floor would leave the far end " +
                "of the wall undrawn, and a hole in a quay reads far worse than a small overlap");

            float pitch = length / run.Count;
            Assert.That(pitch, Is.LessThanOrEqualTo(piece + 1e-3f),
                $"the {wall}'s pieces are pitched {pitch:0.00} m apart but are only {piece:0.00} m long — " +
                "that is a gap between every pair of them, straight through to the bay behind");

            Assert.That(Vector2.Distance(run[0].Lip, from), Is.EqualTo(pitch * 0.5f).Within(1e-3f),
                $"the first {wall} piece is not centred in its own slot — the same 'west end PLUS half a " +
                "block' rule #462's breakwater armour uses, and for the same reason: a piece placed at " +
                "the start of its slot puts half of itself past the end of the wall");
            Assert.That(Vector2.Distance(run[run.Count - 1].Lip, to), Is.EqualTo(pitch * 0.5f).Within(1e-3f),
                $"…and the last {wall} piece overhangs the far end");

            for (int i = 1; i < run.Count; i++)
                Assert.That(Vector2.Distance(run[i].Lip, run[i - 1].Lip), Is.EqualTo(pitch).Within(1e-3f),
                    $"{wall} pieces {i - 1} and {i} are not one pitch apart — the run has drifted");
        }

        [Test]
        public void EachFaceRunCoversItsWholeWall_AndNeverLeavesAGap()
        {
            AssertRunCovers(NineMileCreekDressing.NorthWallRun,
                            NineMileCreekDressing.NorthFaceWest, NineMileCreekDressing.NorthFaceEast);
            AssertRunCovers(NineMileCreekDressing.WestWallRun,
                            NineMileCreekDressing.ApronFaceSouth, NineMileCreekDressing.ApronFaceNorth);
            AssertRunCovers(NineMileCreekDressing.BreakwaterRun,
                            NineMileCreekDressing.BreakwaterCrestWest,
                            NineMileCreekDressing.BreakwaterCrestEast);
        }

        [Test]
        public void EveryFacePieceIsPlacedSoItsDRAWNLipLandsOnTheWallsREALLip()
        {
            // ⭐ THE ONE LINE THE PLACEMENT GUARANTEES, and the trap it exists to absorb: the wharf pack
            // pivots at CHART DATUM — "ground-centre of the footprint at z = 0 = lowest water" — not
            // where the piece touches the ground the way every other pack this region places does. A face
            // piece dropped at the lip the way a crate is dropped on the deck draws the whole quay
            // metres up-screen of the wall, and it looks fine, which is the problem.
            foreach (var p in Face())
            {
                Vector2 seaward = NineMileCreekDressing.PlanDirectionOf(p.Heading);
                Vector2 drawnLip = p.Position + NineMileCreekQuayFace.LipRiseFromPivot(seaward);

                Assert.That(Vector2.Distance(drawnLip, p.Lip), Is.LessThan(1e-3f),
                    $"a {p.Wall} piece pivoted at {p.Position} draws its lip at {drawnLip}, not at the " +
                    $"{p.Lip} it claims. {p.Reason}");

                Assert.That(Vector2.Distance(p.Position, p.Lip), Is.GreaterThan(1f),
                    $"a {p.Wall} piece's pivot has been collapsed onto its lip. The wharf pack's pivot " +
                    $"is {NineMileCreekQuayFace.BakedDeckZMetres:0.#} m of drawn height below the deck " +
                    "and half a piece-width behind it — placing by the lip is the defect this offset exists to stop");

                Assert.That(p.Reason, Is.Not.Null.And.Not.Empty,
                    $"a {p.Wall} piece has no reason recorded");
            }
        }

        [Test]
        public void EveryFacePiecesLipLiesOnTheWallItBelongsTo()
        {
            Rect quay = NineMileCreekWharf.DeckFootprint();
            Rect apron = NineMileCreekWharf.ApronFootprint();

            foreach (var p in FaceRun(NineMileCreekDressing.NorthWallRun))
            {
                Assert.That(p.Lip.y, Is.EqualTo(NineMileCreekWharf.MooringEdgeY).Within(1e-3f),
                    "the mooring face is drawn on the mooring edge — #462's own line, not a parallel one");
                Assert.That(p.Lip.x, Is.InRange(quay.xMin, quay.xMax));
            }

            foreach (var p in FaceRun(NineMileCreekDressing.WestWallRun))
            {
                Assert.That(p.Lip.x, Is.EqualTo(apron.xMax).Within(1e-3f),
                    "the apron's water side faces EAST, which NineMileCreekMainland states and the " +
                    "fill's own shape confirms");
                Assert.That(p.Lip.y, Is.InRange(apron.yMin, apron.yMax));
            }

            foreach (var p in FaceRun(NineMileCreekDressing.BreakwaterRun))
            {
                Assert.That(p.Lip.y, Is.EqualTo(NineMileCreekWharf.BreakwaterY).Within(1e-3f),
                    "the arm is drawn on the crest line #462 lays its COLLISION on, so what a boat hits " +
                    "and what a player sees are the same line");
                Assert.That(p.Lip.x, Is.InRange(NineMileCreekWharf.BreakwaterWestX,
                                                NineMileCreekWharf.BreakwaterEastX));
            }
        }

        [Test]
        public void TheInsideOfTheLWhereTheTwoWallsMeetIsNotDrawnAsAFace()
        {
            // ⚠️ The apron runs north to y = 92 and the quay's deck starts at y = 87, so each wall's
            // edge disappears into the other's ground for the last few metres. Drawing either would put
            // a 4.6 m wall of log crib in the middle of the wharf, facing a deck. Both bounds are derived
            // from the OTHER wall's footprint, so re-siting either re-cuts the corner.
            Rect quay = NineMileCreekWharf.DeckFootprint();
            Rect apron = NineMileCreekWharf.ApronFootprint();

            foreach (var p in FaceRun(NineMileCreekDressing.NorthWallRun))
                Assert.That(p.Lip.x, Is.GreaterThan(apron.xMax),
                    $"a mooring-face piece is drawn at x = {p.Lip.x:0.#}, west of the apron's east side " +
                    $"({apron.xMax:0.#}) — that stretch of the quay's south side stands against the " +
                    "apron's own deck, not against the basin");

            foreach (var p in FaceRun(NineMileCreekDressing.WestWallRun))
                Assert.That(p.Lip.y, Is.LessThan(quay.yMin),
                    $"an apron-face piece is drawn at y = {p.Lip.y:0.#}, north of the quay's mooring " +
                    $"edge ({quay.yMin:0.#}) — it would be a wall standing inside the wharf");
        }

        [Test]
        public void EveryFacePieceLooksAtTheWaterItsOwnWallHolds()
        {
            foreach (var p in FaceRun(NineMileCreekDressing.NorthWallRun))
                Assert.That(p.Heading,
                    Is.EqualTo(NineMileCreekWharf.MooringFaceHeadingDegrees).Within(1e-3f),
                    "the mooring face has to look the way #462's mooring face looks, or the wall is " +
                    "drawn showing the player its back");

            foreach (var p in FaceRun(NineMileCreekDressing.WestWallRun))
                Assert.That(p.Heading, Is.EqualTo(NineMileCreekDressing.ApronSeawardHeading).Within(1e-3f),
                    "the apron's face looks east, at the water a boat lies against it to take fuel from");

            foreach (var p in FaceRun(NineMileCreekDressing.BreakwaterRun))
                Assert.That(p.Heading,
                    Is.EqualTo(NineMileCreekWharf.MooringFaceHeadingDegrees).Within(1e-3f),
                    "the arm's exposed side is the one AWAY from the basin it shelters, which at this " +
                    "wharf is the same way the quay looks. It is derived from where the two are, not " +
                    "typed, so re-siting either turns the arm rather than leaving it facing its lee");

            // ⚠️ THE MISLABEL THAT HAS SHIPPED IN FIVE KITS: the sheet's order array reads N NE E SE…
            // (clockwise) while the wharf pack is registered COUNTER-clockwise, so cell i depicts
            // heading −45°·i. Read as a compass, every wall would face the wrong way and nothing at all
            // would fail.
            foreach (var p in Face())
                Assert.That(NineMileCreekDressing.FacingFor(p),
                    Is.InRange(0, NineMileCreekDressing.Facings - 1),
                    $"a {p.Wall} piece at heading {p.Heading:0.#}° resolves outside the sheet's " +
                    $"{NineMileCreekDressing.Facings} facings");
        }

        [Test]
        public void TheQuayFaceSortsByItsLip_SoABoatAtTheBerthIsNotDrawnBehindTheWall()
        {
            foreach (var p in Face())
                Assert.That(p.Position.y + p.SortYOffset, Is.EqualTo(p.Lip.y).Within(1e-3f),
                    $"a {p.Wall} piece's sort offset does not land on its lip");

            // ⭐ WHY THE OFFSET IS LOAD-BEARING. The pivot sits down-screen of the wall, out where the
            // fleet lies. Sorted from there the quay draws IN FRONT of every boat moored against it —
            // invisible until there is a boat at the berth to be hidden, and then obviously wrong.
            float berthY = NineMileCreekWharf.BerthPos(0).y;
            int boat = Order(berthY);

            var north = FaceRun(NineMileCreekDressing.NorthWallRun);
            Assert.That(north, Is.Not.Empty);
            foreach (var p in north)
            {
                Assert.That(Order(p.Position.y + p.SortYOffset), Is.LessThan(boat),
                    $"sorted at its lip ({p.Lip.y:0.##}) the wall still draws over a hull at the berth " +
                    $"line ({berthY:0.##})");
                Assert.That(Order(p.Position.y), Is.GreaterThan(boat),
                    "sorted at its PIVOT the wall would draw in front of the boat — which is exactly " +
                    "the defect YSortSprite.SortPivotYOffset is set to avoid here. If this ever stops " +
                    "being true the offset has become decoration and can go");
            }
        }

        private static int Order(float worldY) =>
            YSortSprite.OrderFor(worldY, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                 SortingBands.DecorFloor, SortingBands.DecorCeiling);

        [Test]
        public void TheWallIsDrawnInTheMaterialTheRegionRuledItToBe()
        {
            // #462 read the owner's photographs as log crib — "log boxes filled with stone, what a small
            // community wharf actually builds" — and reserved sheet pile for "the commercial quay money
            // and machinery would build". Held against the region's OWN constant so the ruling and the
            // piece drawn for it cannot drift apart.
            Assert.That(NineMileCreekQuayFace.FaceCourseFamily,
                Is.EqualTo(NineMileCreekWharf.BreakwaterArmour),
                $"the quay is drawn with the '{NineMileCreekQuayFace.FaceCourseFamily}' family while the " +
                $"region rules its structure '{NineMileCreekWharf.BreakwaterArmour}'");

            var course = NineMileCreekQuayFace.FaceCourse();
            Assert.That(course.Key, Is.EqualTo(NineMileCreekQuayFace.FaceCourseKey));
            Assert.That(course.IsRuledMaterial, Is.True,
                "the face is drawn with a material this wharf is ruled not to be built of");
            Assert.That(course.HasWorkingDeck, Is.True,
                "a face with no working top has nothing to land a catch on and nowhere to stand a bollard");
            Assert.That(course.UsableHere, Is.True);
        }

        [Test]
        public void TheFaceIsItsOwnList_BecauseItFailsEverySweepTheGearTakes()
        {
            var face = Face();
            var terrain = MakeCreekTerrain();

            Assert.That(face.Count, Is.GreaterThan(0), "the quay is undrawn again");
            // Rule 7: the retired tile kit would have needed 1 320 cells for these two walls. This is
            // two dozen objects for the whole quay, and the ceiling is here so a future 'more detail'
            // pass has to argue for it.
            Assert.That(face.Count, Is.LessThan(40),
                $"{face.Count} face objects for a static quay is a perf budget spent on nothing");

            Assert.That(Props().Select(p => p.Key).ToList(),
                Does.Not.Contain(NineMileCreekQuayFace.FaceCourseKey),
                "the face has been folded into AllProps(). Every sweep over the props asks questions " +
                "that are right for gear and wrong for structure");

            // …and here is why, concretely: the pieces on the mooring face stand in the BASIN. A prop
            // there is an authoring bug; a quay face there is the quay.
            var onTheLip = face.Where(p => p.Wall == NineMileCreekDressing.NorthWallRun).ToList();
            Assert.That(onTheLip, Is.Not.Empty);
            foreach (var p in onTheLip)
                Assert.That(terrain.ElevationAt(p.Position), Is.LessThan(SpringHigh),
                    "a mooring-face piece's pivot is on dry land, which means it is not standing in the " +
                    "water the wall is built out into");
        }

        // =============================================================================================
        //  3. THE DRESSING IS BUILDER-WIRED — the named trap
        // =============================================================================================

        [Test]
        public void TheBuilderPlacesTheDressing_SoARebuildKeepsIt()
        {
            var terrain = MakeCreekTerrain();
            NineMileCreekDressing.Place(terrain);

            var root = GameObject.Find(NineMileCreekDressing.RootName);
            _spawned.Add(root);
            Assert.That(root, Is.Not.Null,
                "the dressing has to come from the BUILDER. A prop wired into the scene survives exactly " +
                "until the owner rebuilds it, which is the whole reason this is a table and not a scene");

            foreach (string group in new[]
                     {
                         NineMileCreekDressing.FaceRootName,
                         NineMileCreekDressing.QuayRootName, NineMileCreekDressing.ApronRootName,
                         NineMileCreekDressing.YardRootName, NineMileCreekDressing.UtilityRootName,
                     })
                Assert.That(root.transform.Find(group), Is.Not.Null,
                    $"'{group}' is missing — the jobs this pass does should each be a group the " +
                    "owner can find and hide");
        }

        [Test]
        public void TheBuilderDrawsTheQuay_AndTheWallJoinsTheYSortBandLikeEverythingElse()
        {
            var terrain = MakeCreekTerrain();
            NineMileCreekDressing.Place(terrain);

            var root = GameObject.Find(NineMileCreekDressing.RootName);
            _spawned.Add(root);
            Assert.That(root, Is.Not.Null);

            var group = root.transform.Find(NineMileCreekDressing.FaceRootName);
            Assert.That(group, Is.Not.Null,
                "the quay face has to come from the BUILDER like the rest of Phase B — a wall dragged " +
                "into the scene survives exactly until the owner rebuilds it");

            // Vacuous when the wharf pack has not imported (the sheets are Git-LFS binaries), and
            // deliberately so: the PLACEMENT is asserted without art by §2.
            var renderers = group.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            if (renderers.Length == 0) Assert.Pass("the wharf ISO pack has not imported — placement is " +
                                                  "asserted without it in section 2");

            Assert.That(renderers.Length, Is.EqualTo(Face().Count),
                "the drawn wall and the placement table disagree about how many courses there are");

            foreach (var sr in renderers)
            {
                var ysort = sr.GetComponent<YSortSprite>();
                Assert.That(ysort, Is.Not.Null,
                    $"'{sr.name}' has no YSortSprite. The wharf-deck band #462 kept open for this is six " +
                    "orders wide and the north wall is 84 m long — that is #462's own argument against " +
                    "the retired tile kit's per-row scheme, and it applies to the piece it was about");
                Assert.That(ysort.SortPivotYOffset, Is.GreaterThan(0f),
                    $"'{sr.name}' sorts by its own transform, which for this pack is a pivot at chart " +
                    "datum — out in the basin, in front of the boats");
                Assert.That(sr.sortingOrder,
                    Is.InRange(SortingBands.DecorFloor, SortingBands.DecorCeiling),
                    $"'{sr.name}' sorts at {sr.sortingOrder}, outside the decor band");
            }
        }

        [Test]
        public void EveryPlacedSprite_JoinsTheYSortBand_NeverAHandPickedOrder()
        {
            var terrain = MakeCreekTerrain();
            NineMileCreekDressing.Place(terrain);

            var root = GameObject.Find(NineMileCreekDressing.RootName);
            _spawned.Add(root);
            Assert.That(root, Is.Not.Null);

            var renderers = root.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            // Vacuous when the pack has not imported, and deliberately so — see the class note. The
            // PLACEMENT is asserted without art by the band-arithmetic sweep below.
            foreach (var sr in renderers)
            {
                Assert.That(sr.GetComponent<YSortSprite>(), Is.Not.Null,
                    $"'{sr.name}' has no YSortSprite — dressing joins the decor band (ADR 0032), it does " +
                    "not pick an order. A prop with a fixed order cannot be walked in front of");
                Assert.That(sr.sortingOrder,
                    Is.InRange(SortingBands.DecorFloor, SortingBands.DecorCeiling),
                    $"'{sr.name}' sorts at {sr.sortingOrder}, outside the decor band " +
                    $"[{SortingBands.DecorFloor}, {SortingBands.DecorCeiling}]");
            }
        }

        [Test]
        public void EveryPieceOfDressing_SitsWhereTheDecorBandStillRESOLVES()
        {
            // ⚠️ THE ADR 0032 DEFECT, and the reason this is checked on POSITIONS rather than on loaded
            // sprites: where the band's clamp bites, sorting silently STOPS — every sprite past the end
            // saturates on one order and interleaves by draw order instead of by position. Nothing fails;
            // the wharf just stops layering.
            var terrain = MakeCreekTerrain();

            foreach (var prop in Props())
                Assert.That(SortingBands.ResolvesWorldY(prop.Position.y), Is.True,
                    $"'{prop.Key}' at Y = {prop.Position.y:0.#} is outside the decor band's " +
                    $"±{SortingBands.DecorHalfExtentMetres} m resolving range — it would saturate and " +
                    "stop layering, silently");

            foreach (var find in NineMileCreekDressing.Finds(terrain))
                Assert.That(SortingBands.ResolvesWorldY(find.Position.y), Is.True,
                    $"a {find.Key} at Y = {find.Position.y:0.#} is outside the decor band's resolving range");
        }

        [Test]
        public void TheDressingIsSparse_AndEveryPieceSaysWhatItIsFor()
        {
            var props = Props();

            Assert.That(props.Count, Is.GreaterThan(20),
                "a wharf with a handful of props on it reads as unbuilt rather than as quiet");
            Assert.That(props.Count, Is.LessThan(80),
                "…and the owner's photographs are of a working wharf on a coast of FIELDS. The 61-piece " +
                "decor kit is a temptation, not a budget: a carpeted wharf reads as a diorama");

            foreach (var prop in props)
            {
                Assert.That(prop.Key, Is.Not.Null.And.Not.Empty);
                Assert.That(prop.Reason, Is.Not.Null.And.Not.Empty,
                    $"'{prop.Key}' has no reason recorded. A prop that cannot say what it is for is the " +
                    "one to cut — that is how this stays sparse under the next pass");
            }
        }

        // =============================================================================================
        //  4. WHERE THINGS STAND — derived, and clear of everything already there
        // =============================================================================================

        [Test]
        public void NothingStandsInTheWorkingStripAlongTheMooringEdge()
        {
            Rect quay = NineMileCreekWharf.DeckFootprint();

            // Only the safety and tide gear stand forward of the gear band, and even they keep back by
            // the fitting clearance. Fish are landed across this ground and lines are handled on it.
            foreach (var prop in Props())
            {
                if (!quay.Contains(prop.Position)) continue;
                Assert.That(prop.Position.y,
                    Is.GreaterThanOrEqualTo(quay.yMin + NineMileCreekDressing.FittingClearanceMetres - 1e-3f),
                    $"'{prop.Key}' stands within {NineMileCreekDressing.FittingClearanceMetres} m of the " +
                    "mooring lip — that is where a line is handled and a hull comes alongside");
                Assert.That(prop.Position.y,
                    Is.LessThanOrEqualTo(quay.yMax - NineMileCreekDressing.BackSetbackMetres + 1e-3f),
                    $"'{prop.Key}' has spilled off the back of the deck into the yard");
            }

            // …and the gear band itself leaves the working strip empty.
            var inStrip = Props()
                .Where(p => quay.Contains(p.Position) &&
                            p.Position.y < quay.yMin + NineMileCreekDressing.WorkingStripMetres)
                .Select(p => p.Key)
                .ToList();
            Assert.That(inStrip, Is.EquivalentTo(new[] { "tideStaff", "ringStation", "ringStation" }),
                "only the tide board and the life rings belong forward of the working strip. Anything " +
                $"else inside {NineMileCreekDressing.WorkingStripMetres} m of the lip is in the way of " +
                $"landing a catch — found: {string.Join(", ", inStrip)}");
        }

        [Test]
        public void NoPropCrowdsOneOf462sMooringFittings()
        {
            // ⭐ The fittings are the wharf's one hard promise — the bollard you see IS the bollard you
            // tie to. A crate at arm's length of one is in the way of the only thing the wharf is for.
            var fittings = NineMileCreekWharf.Fittings();

            foreach (var prop in Props())
                foreach (var fitting in fittings)
                {
                    float d = Vector2.Distance(prop.Position, fitting.Position);
                    Assert.That(d, Is.GreaterThanOrEqualTo(
                            NineMileCreekDressing.FittingClearanceMetres - 1e-3f),
                        $"'{prop.Key}' at {prop.Position} is {d:0.00} m from the '{fitting.Name}' at " +
                        $"{fitting.Position} — inside the " +
                        $"{NineMileCreekDressing.FittingClearanceMetres} m a fitting needs to be used");
                }
        }

        [Test]
        public void NothingBlocksTheViewOfTheDerelictDoryFromWhereThePlayerLands()
        {
            // ⚠️ THE REGION'S OPENING BEAT. NineMileCreekDory measures this line and the whole §7.2 exit
            // condition hangs off it: you step ashore and you SEE the boat you are going to buy. A first
            // draft of this pass put the fuel pump 0.28 m off it.
            foreach (var prop in Props())
                Assert.That(
                    NineMileCreekDory.SightlineIsClear(NineMileCreekDory.HaulOutPos, prop.Position,
                                                       NineMileCreekDressing.DoryYardClearanceMetres),
                    Is.True,
                    $"'{prop.Key}' at {prop.Position} stands on the line from where the player lands to " +
                    "the derelict. Move the prop; the beat is not negotiable");
        }

        [Test]
        public void NothingSitsInTheRoadsClearedCorridor()
        {
            foreach (var prop in Props())
            {
                float d = NineMileCreekMainland.DistanceToRoute(
                    NineMileCreekMainland.WharfRoad, prop.Position);
                Assert.That(d, Is.GreaterThanOrEqualTo(NineMileCreekMainland.RoadHalfWidth - 1e-3f),
                    $"'{prop.Key}' at {prop.Position} is {d:0.00} m from Wharf Road's centre-line, " +
                    $"inside the {NineMileCreekMainland.RoadHalfWidth} m corridor the plan clears. A " +
                    "hydrant in the road is a hydrant a truck hits");
            }
        }

        [Test]
        public void EveryPieceOfDressingStandsOnDryGround()
        {
            var terrain = MakeCreekTerrain();

            // The same bar the road test uses: above the HIGHEST water, not above mean. The poles are
            // what this really asks about — the plan offsets them 5 m north of a road that is itself
            // only just clear of the barachois — but the gear is swept too, because the deck carries
            // only 0.8 m of freeboard at spring high and a terrain edit is all it would take.
            foreach (var prop in Props())
            {
                float e = terrain.ElevationAt(prop.Position);
                Assert.That(e, Is.GreaterThan(SpringHigh),
                    $"'{prop.Key}' stands at {prop.Position} where the ground is {e:0.00} m — at or " +
                    $"below spring high water ({SpringHigh:0.0} m). {prop.Reason}");
            }
        }

        [Test]
        public void ThePoleLineWalksWharfRoadsOwnPublishedRoute()
        {
            var poles = NineMileCreekDressing.Poles();
            float spacing = NineMileCreekMainland.UtilityPoleSpacingMetres;
            float offset = NineMileCreekMainland.UtilityPoleOffsetMetres;
            float length = NineMileCreekMainland.RouteLength(NineMileCreekMainland.WharfRoad);

            Assert.That(poles.Count, Is.EqualTo(Mathf.FloorToInt(length / spacing) + 1),
                "the pole count is the route's length over the plan's own spacing — never a typed number");

            foreach (var pole in poles)
            {
                float d = NineMileCreekMainland.DistanceToRoute(
                    NineMileCreekMainland.WharfRoad, pole.Position);
                Assert.That(d, Is.EqualTo(offset).Within(0.05f),
                    $"a pole stands {d:0.00} m from the road instead of the plan's {offset} m");
            }

            // NORTH of the centre-line, which §12 states in as many words.
            for (int i = 0; i < poles.Count; i++)
            {
                float along = i * spacing;
                Vector2 on = MainlandCoast.PositionAt(NineMileCreekMainland.WharfRoad, along);
                Assert.That(poles[i].Position.y, Is.GreaterThan(on.y),
                    $"pole {i} is on the SOUTH side of Wharf Road; the plan puts the line north of it");
            }

            // …and the line actually gets to the wharf, which is the only reason it exists.
            float toWharf = poles.Min(p => Vector2.Distance(p.Position, NineMileCreekDressing.WharfEntrance()));
            Assert.That(toWharf, Is.LessThan(spacing),
                "the pole line has to REACH the wharf — the yard light is the end of it");
        }

        [Test]
        public void TheWinchIsTheTallLegibleObjectThePlanAskedFor()
        {
            // NineMileCreekMainland: the west wall's water side is a curb-only edge at this camera,
            // "which is why the plan wants the winch to be a tall legible object rather than a detail on
            // a wall". This is that promise, kept.
            var at = new Vector2(NineMileCreekMainland.WinchPos.x, NineMileCreekMainland.WinchPos.y);
            var winch = NineMileCreekDressing.ApronGear()
                .FirstOrDefault(p => Vector2.Distance(p.Position, at) < 1e-3f);

            Assert.That(winch.Key, Is.Not.Null.And.Not.Empty,
                "nothing stands at the authored WinchPos — the apron has no machine on it");
            Assert.That(winch.Heading,
                Is.EqualTo(NineMileCreekDressing.ApronSeawardHeading).Within(1e-3f),
                "the winch has to look at the water it lifts out of");
        }

        [Test]
        public void TheDressingDoesNotDuplicateAFittingThatIsALREADYReal()
        {
            // ⭐ #462's two guarantees: the ladder you can SEE is the ladder you can CLIMB, and the
            // bollard you can see is the bollard you can tie to. A decorative twin of either is exactly
            // the promise those components exist to keep.
            var keys = Props().Select(p => p.Key).ToList();

            foreach (string forbidden in new[] { "rescueLadder", "ringPost" })
                Assert.That(keys, Does.Not.Contain(forbidden),
                    $"'{forbidden}' is a decorative copy of something #462 made REAL. A ladder you " +
                    "cannot climb standing beside one you can is worse than no ladder");

            // ringStation is fine and is the reason this test names the two rather than banning a theme:
            // a life ring is rescue gear, not a tie-off, and nothing in the sim claims it.
            Assert.That(keys, Does.Contain("ringStation"),
                "the wharf should still carry life rings — those are not mooring fittings");
        }

        // =============================================================================================
        //  5. THE FORESHORE
        // =============================================================================================

        [Test]
        public void EveryFindsStateIsTheOneItsOwnTideBandCallsFor()
        {
            var terrain = MakeCreekTerrain();
            var finds = NineMileCreekDressing.Finds(terrain);
            var bands = NineMileCreekDressing.Bands().ToDictionary(b => b.Zone, b => b);

            Assert.That(finds.Count, Is.GreaterThan(0),
                "the foreshore should have something on it — the finds contract is committed text and " +
                "does not need the sheets to have baked");

            foreach (var find in finds)
            {
                Assert.That(bands.ContainsKey(find.Zone), Is.True,
                    $"'{find.Key}' is in zone '{find.Zone}', which no band covers");
                Assert.That(find.State, Is.EqualTo(bands[find.Zone].State),
                    $"a {find.Key} in the '{find.Zone}' band is drawn '{find.State}'. The kit bakes " +
                    "three states per find for exactly this axis, and the band decides it");
            }
        }

        [Test]
        public void EveryFindLiesAtAnElevationItsBandActuallyCovers()
        {
            var terrain = MakeCreekTerrain();
            var bands = NineMileCreekDressing.Bands().ToDictionary(b => b.Zone, b => b);

            foreach (var find in NineMileCreekDressing.Finds(terrain))
            {
                var band = bands[find.Zone];
                // The search targets the band's MIDDLE, so a hit should be inside the band with room —
                // a tolerance only because the bisection stops at a finite depth.
                Assert.That(find.Elevation,
                    Is.InRange(band.MinElevation - 0.05f, band.MaxElevation + 0.05f),
                    $"a {find.Key} ({find.Zone}/{find.State}) is lying at {find.Elevation:0.00} m, " +
                    $"outside its band [{band.MinElevation:0.00}, {band.MaxElevation:0.00}]. A 'wet' " +
                    "find above the tide is the whole banding scheme not working");
            }
        }

        [Test]
        public void NoFindLiesOnTheWharfsMadeGround()
        {
            var terrain = MakeCreekTerrain();

            foreach (var find in NineMileCreekDressing.Finds(terrain))
                Assert.That(NineMileCreekDressing.IsOnMadeGround(find.Position), Is.False,
                    $"a {find.Key} is lying at {find.Position}, on the spit or the harbour shoal. Finds " +
                    "are what the tide left on the NATURAL foreshore; a shell on a concrete deck is a prop");
        }

        [Test]
        public void TheScatterIsAPureFunctionOfItsIndex_SoARebuildReproducesIt()
        {
            // Rule 5: no hidden randomness. Two builds of the same region must lay the same beach, or
            // every screenshot, playtest note and bug report is about a different shore.
            var terrain = MakeCreekTerrain();
            var first = NineMileCreekDressing.Finds(terrain);
            var second = NineMileCreekDressing.Finds(terrain);

            Assert.That(second.Count, Is.EqualTo(first.Count), "the scatter changed size between builds");
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].Key, Is.EqualTo(first[i].Key));
                Assert.That(second[i].State, Is.EqualTo(first[i].State));
                Assert.That(second[i].LieAngle, Is.EqualTo(first[i].LieAngle));
                Assert.That(second[i].Variant, Is.EqualTo(first[i].Variant));
                Assert.That(Vector2.Distance(second[i].Position, first[i].Position), Is.LessThan(1e-4f),
                    $"find {i} ({first[i].Key}) moved between two builds of the same region");
            }
        }

        [Test]
        public void EveryZoneTheFindsContractDeclaresHasABandToLandIn()
        {
            var zones = NineMileCreekDressing.DeclaredFindZones();
            Assert.That(zones.Count, Is.GreaterThan(0),
                "the shore-finds contract is committed text — if it read as empty, it failed to load");

            var covered = NineMileCreekDressing.Bands().Select(b => b.Zone).ToList();
            foreach (string zone in zones)
                Assert.That(covered, Does.Contain(zone),
                    $"the kit bakes finds for the '{zone}' zone and no band places them, so they are " +
                    "baked and never seen. Add the band rather than dropping the finds");
        }

        [Test]
        public void EveryFindAsksForASliceItsSheetActuallyHas()
        {
            var terrain = MakeCreekTerrain();

            foreach (var find in NineMileCreekDressing.Finds(terrain))
            {
                Assert.That(find.LieAngle,
                    Is.InRange(0, NineMileCreekDressing.FindLieAngles - 1),
                    $"a {find.Key} asks for lie angle {find.LieAngle}; the sheet bakes " +
                    $"{NineMileCreekDressing.FindLieAngles}");
                Assert.That(find.Variant,
                    Is.InRange(0, NineMileCreekDressing.FindVariants - 1),
                    $"a {find.Key} asks for variant {find.Variant}; the sheet bakes " +
                    $"{NineMileCreekDressing.FindVariants}");
            }
        }

        [Test]
        public void TheBandsCoverTheTideWithoutOverlappingIntoNonsense()
        {
            var bands = NineMileCreekDressing.Bands();

            var tide = bands.First(b => b.Zone == "tide");
            Assert.That(tide.MinElevation, Is.EqualTo(SpringLow).Within(1e-4f),
                "the intertidal band starts at the lowest water — that is what intertidal means");
            Assert.That(tide.MaxElevation, Is.EqualTo(SpringHigh).Within(1e-4f));

            var wrack = bands.First(b => b.Zone == "wrack");
            Assert.That(wrack.Middle, Is.EqualTo(SpringHigh).Within(1e-4f),
                "the wrack line is where the biggest tide stopped");

            var upper = bands.First(b => b.Zone == "upper");
            Assert.That(upper.MinElevation, Is.GreaterThanOrEqualTo(SpringHigh - 1e-4f),
                "the upper beach is above the strandline, or it is not upper beach");
        }

        // =============================================================================================
        //  6. THE FACING — the mislabel that has shipped five times
        // =============================================================================================

        [Test]
        public void EveryPropResolvesToAFacingItsSheetActuallyBaked()
        {
            foreach (var prop in Props())
                Assert.That(NineMileCreekDressing.FacingFor(prop),
                    Is.InRange(0, NineMileCreekDressing.Facings - 1),
                    $"'{prop.Key}' at heading {prop.Heading:0.#}° resolves outside the sheet's " +
                    $"{NineMileCreekDressing.Facings} facings");
        }

        [Test]
        public void TheQuayGearLooksAtTheWaterTheWharfWorks()
        {
            // Derived from #462's mooring-face heading rather than typed as "south", so re-pointing the
            // wall turns the gear with it.
            var quay = NineMileCreekDressing.QuayGear();
            var working = quay.Where(p => p.Key != "harbourSign" && p.Key != "noticeBoard" &&
                                          p.Key != "bunting").ToList();

            Assert.That(working, Is.Not.Empty);
            foreach (var prop in working)
                Assert.That(prop.Heading,
                    Is.EqualTo(NineMileCreekWharf.MooringFaceHeadingDegrees).Within(1e-3f),
                    $"'{prop.Key}' does not face the water. Working gear on a quay is used from the " +
                    "water side");

            // …and the sign group faces the other way, at whoever is coming down the road.
            float arrival = NineMileCreekDressing.RoadArrivalHeading();
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(arrival, NineMileCreekWharf.MooringFaceHeadingDegrees)),
                Is.GreaterThan(45f),
                "the sign at the entrance should not be pointing the same way as the working gear — it " +
                "is read by somebody arriving, not by somebody landing fish");
        }
    }
}
