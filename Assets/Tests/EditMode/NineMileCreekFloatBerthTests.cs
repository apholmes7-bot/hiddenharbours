using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE BOATS ON THE FLOAT AT NINE MILE CREEK</b> — S3 of the wet-wharf arc, and the thing S1's
    /// channel and S2's dock were built to carry.
    ///
    /// <para>The owner's wharf photograph is two moorings, not one: <i>small craft on the float fingers in
    /// the middle of the bullpen, working boats against the tall quay walls</i>. S1 cut the lane, S2 made
    /// the float a real tide-riding deck, and this file is about the boats that lie alongside her — where
    /// they are, which side of the finger, and the two gates that decide who may be there at all.</para>
    ///
    /// <para><b>⭐ THESE TEST THE BUILDER'S OUTPUT, NOT THE BANKED SCENE.</b> Scene-wired is not
    /// builder-wired: a boat sitting prettily in <c>NineMileCreek.unity</c> proves nothing about the
    /// owner's next rebuild, which is the only thing he actually looks at. So the layout half is asserted
    /// on the pure tables, and the placement half runs <see cref="NineMileCreekMooredFleet.Place"/> into a
    /// scratch scene and reads back what it made.</para>
    ///
    /// <para><b>And the builder still PLACES rather than DRAWS.</b> The hull mesh path is chosen live, per
    /// run, by the skinner; a builder that skinned hulls here would bake the sprite fallback into the
    /// committed scene and the fleet would silently never be mesh. <see cref="TheBuilderPlacesAndDoesNotDraw"/>
    /// is that law, carried onto the float's boats.</para>
    /// </summary>
    public class NineMileCreekFloatBerthTests
    {
        private GameObject _go;
        private MainlandTidalTerrain _terrain;

        private static float SpringLow => NineMileCreekMainland.SpringLowWater;
        private static float Clearance => NineMileCreekMainland.ChannelKeelClearanceMetres;

        [SetUp]
        public void SetUp()
        {
            DestroyAnyFleet();
            _go = new GameObject("NineMileCreekFloatBerth_Test");
            _terrain = _go.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            DestroyAnyFleet();
        }

        /// <summary>Tear the placer's root out BY NAME — a fixture's cleanup must not depend on the thing
        /// it is testing having worked (<c>NineMileCreekLadderTests</c>' rule, and its reason).</summary>
        private static void DestroyAnyFleet()
        {
            for (int guard = 0; guard < 8; guard++)
            {
                GameObject root = GameObject.Find(NineMileCreekMooredFleet.RootName);
                if (root == null) return;
                Object.DestroyImmediate(root);
            }
        }

        private static List<BoatOwnerDef> Owners() => NineMileCreekMooredFleet.LoadOwners();

        private static IEnumerable<BoatOwnerDef> AtTheFloat() =>
            Owners().Where(o => o.Moorage == BoatMoorage.Float);

        private static BoatHullDef Hull(string path) =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<BoatHullDef>(path);

        // =============================================================================================
        //  1. THE TABLE — where the berths are, before anything is placed in them
        // =============================================================================================

        /// <summary>A berth at the float IS a cleat: the place a rope goes round and the place a boat
        /// lies are one object, which is #451's guarantee carried onto the deck that moves.</summary>
        [Test]
        public void TheFloatBerthsAreOneToACleat()
        {
            Assert.That(NineMileCreekWharf.FloatBerthCount, Is.GreaterThan(0),
                "the float berths nobody — either the run has no length or the cleat pitch has grown " +
                "past it, and the small craft in the photograph have nowhere to lie");
            Assert.That(NineMileCreekWharf.FloatBerthCount,
                Is.EqualTo(NineMileCreekWharf.FloatCleatPositions().Count),
                "the berth count and the cleat run must be the same table — two counts that could " +
                "disagree is a boat tied to nothing");
        }

        /// <summary>
        /// She lies <b>ALONGSIDE</b> the finger, never on it. A berth inside the float's own footprint
        /// would draw a boat sitting on the planking her skipper walks along, and (worse) would put her
        /// hull on the one rectangle of this harbour that is walkable floor.
        /// </summary>
        [Test]
        public void EveryFloatBerthLiesAlongsideTheFinger_NeverOnHerDeck()
        {
            Rect dock = NineMileCreekWharf.FloatFootprint();
            float off = NineMileCreekWharf.FloatBerthOffsetMetres;

            for (int i = 0; i < NineMileCreekWharf.FloatBerthCount; i++)
            {
                (Vector2 north, Vector2 south) = NineMileCreekWharf.FloatBerthSides(i);

                foreach (Vector2 side in new[] { north, south })
                {
                    Assert.IsFalse(dock.Contains(side),
                        $"float berth {i} at {side} is INSIDE the dock's own footprint {dock} — a boat " +
                        "berthed there is drawn lying on the planking, not beside it");
                    Assert.That(side.x, Is.InRange(dock.xMin, dock.xMax),
                        $"float berth {i} at {side} is off the END of a {dock.width:0.#} m finger — she " +
                        "would be tied to open water past the last cleat");
                    Assert.That(Mathf.Abs(side.y - NineMileCreekMainland.FloatRunY),
                        Is.EqualTo(off).Within(1e-4f),
                        $"float berth {i} stands {Mathf.Abs(side.y - NineMileCreekMainland.FloatRunY):0.00} m " +
                        $"off the run rather than the derived {off:0.00} m");
                }

                Assert.That(north.y, Is.GreaterThan(south.y),
                    $"float berth {i}'s two sides are not on two sides");
            }
        }

        /// <summary>
        /// ⭐ The standoff is the WALL's own, read off the region's berth line rather than picked again
        /// for the float. One convention for "alongside", so the float's boats stand off their dock
        /// exactly as far as the fleet stands off the quay — and moving the berth line moves both.
        /// </summary>
        [Test]
        public void TheStandoffIsTheWallsOwn_NotASecondNumber()
        {
            float wallGap = NineMileCreekWharf.MooringEdgeY - NineMileCreekMainland.FirstBerthPos.y;

            Assert.That(NineMileCreekWharf.MooredStandoffMetres, Is.EqualTo(wallGap).Within(1e-4f),
                "the float's standoff has stopped being the wall's — one of the two is now a typed " +
                "number and the moorings will drift apart on the next plan edit");
            Assert.That(NineMileCreekWharf.MooredStandoffMetres, Is.GreaterThan(0f),
                "a standoff of zero berths every boat inside the structure she is tied to");
            Assert.That(NineMileCreekWharf.FloatBerthOffsetMetres,
                Is.EqualTo(NineMileCreekWharf.FloatFootprint().height * 0.5f
                           + NineMileCreekWharf.MooredStandoffMetres).Within(1e-4f),
                "the offset off the finger's centre-line must be half her drawn width plus the standoff, " +
                "or a re-baked float of another width leaves her boats overlapping or adrift");
        }

        /// <summary>A boat at the float lies PARALLEL to the finger and points the way she leaves — the
        /// same derivation the wall's heading gets, off the float's own geometry.</summary>
        [Test]
        public void ABoatAtTheFloatLiesAlongTheFinger_NotAcrossIt()
        {
            float heading = NineMileCreekWharf.FloatMooredHeadingDegrees();

            Assert.That(Mathf.Approximately(heading, 90f) || Mathf.Approximately(heading, 270f),
                $"the float runs east–west, so a boat alongside her lies on 90° or 270°, not {heading}°");

            // …and of the two ways along it, AWAY from the brow: the landward end is where the gangway
            // lands, so the seaward end is the other one and that is where her bow points.
            Rect dock = NineMileCreekWharf.FloatFootprint();
            float browX = NineMileCreekWharf.GangwayFloatEnd.x;
            bool seawardIsEast = Mathf.Abs(dock.xMax - browX) > Mathf.Abs(browX - dock.xMin);
            Assert.That(heading, Is.EqualTo(seawardIsEast ? 90f : 270f),
                "her bow points at the end of the finger the brow does NOT land on — re-hinge the " +
                "gangway and the fleet turns round with it");
        }

        // =============================================================================================
        //  2. WHICH SIDE — measured, because the lane meanders across her
        // =============================================================================================

        /// <summary>Every berth takes the side of the finger with more water under it. The rule, on the
        /// terrain the region actually ships.</summary>
        [Test]
        public void EachBerthTakesTheDeeperSideOfTheFinger()
        {
            for (int i = 0; i < NineMileCreekWharf.FloatBerthCount; i++)
            {
                (Vector2 north, Vector2 south) = NineMileCreekWharf.FloatBerthSides(i);
                Vector2 chosen = NineMileCreekWharf.FloatBerthPos(i, _terrain);
                Vector2 other = chosen == north ? south : north;

                Assert.That(_terrain.ElevationAt(chosen),
                    Is.LessThanOrEqualTo(_terrain.ElevationAt(other) + 1e-4f),
                    $"float berth {i} was put on the SHALLOWER side: {chosen} reads " +
                    $"{_terrain.ElevationAt(chosen):0.00} m against {other}'s " +
                    $"{_terrain.ElevationAt(other):0.00} m");
            }
        }

        /// <summary>
        /// ⭐⭐ <b>AND THE DEEPER SIDE IS NOT ONE SIDE FOR THE WHOLE RUN — which is the entire reason this
        /// is measured rather than typed.</b> S1's lane does not run down the float's latitude; it wanders
        /// across it (y = 70 at the head, 67 at the southern swing, 71 at the northern one), so the water
        /// is on the north side of some berths and the south side of others. A constant would have berthed
        /// part of the fleet on the bank of the cut, and those are exactly the boats that would take the
        /// ground.
        ///
        /// <para>It also happens to be the picture: boats on both sides of the finger is what the
        /// photograph shows.</para>
        /// </summary>
        [Test]
        public void TheDeeperSideIsNotOneSideForTheWholeRun()
        {
            int north = 0, south = 0;
            for (int i = 0; i < NineMileCreekWharf.FloatBerthCount; i++)
            {
                (Vector2 n, _) = NineMileCreekWharf.FloatBerthSides(i);
                if (NineMileCreekWharf.FloatBerthPos(i, _terrain) == n) north++; else south++;
            }

            Assert.That(north, Is.GreaterThan(0),
                $"all {south} float berths came out on the SOUTH side. If that is real the meander no " +
                "longer crosses the float run and the per-berth measurement is buying nothing — but it " +
                "is far more likely the lane moved and half the fleet is now on the bank of the cut.");
            Assert.That(south, Is.GreaterThan(0),
                $"all {north} float berths came out on the NORTH side — see the note on the other half " +
                "of this assertion; the same warning applies mirrored.");
        }

        /// <summary>With no terrain there is nothing to measure and nothing is claimed: the berth falls
        /// back to one authored side rather than throwing. The established gate-off shape.</summary>
        [Test]
        public void WithNoTerrainTheBerthFallsBackRatherThanThrowing()
        {
            (_, Vector2 south) = NineMileCreekWharf.FloatBerthSides(0);
            Assert.That(NineMileCreekWharf.FloatBerthPos(0, null), Is.EqualTo(south));
            Assert.That(NineMileCreekWharf.FloatBerthDepthAtSpringLow(0, null),
                Is.EqualTo(float.PositiveInfinity),
                "no terrain must mean NO CLAIM about grounding, never a claim of zero water");
        }

        // =============================================================================================
        //  3. THE TWO GATES — who may lie at the float at all
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>THE HEADLINE: every berth on the run floats the deepest small craft the register keeps,
        /// at dead low spring.</b> The float is fit for its purpose along its whole length, not just where
        /// the lane happens to be deepest — otherwise half the fingers are decoration.
        /// </summary>
        [Test]
        public void EveryFloatBerthFloatsTheDeepestSmallCraftOnTheRegister()
        {
            var small = AtTheFloat().Where(o => o.Boat != null).ToList();
            Assert.IsNotEmpty(small, "nobody is authored at the float — there is nothing to float");
            float deepest = small.Max(o => o.Boat.DraughtMeters);

            for (int i = 0; i < NineMileCreekWharf.FloatBerthCount; i++)
            {
                float carries = NineMileCreekWharf.DeepestDraughtAFloatBerthCarries(i, _terrain);
                Assert.That(carries, Is.GreaterThanOrEqualTo(deepest),
                    $"float berth {i} carries {carries:0.00} m of draught (measured " +
                    $"{NineMileCreekWharf.FloatBerthDepthAtSpringLow(i, _terrain):0.00} m at spring low, " +
                    $"less the region's {Clearance:0.00} m keel clearance) and the deepest small craft on " +
                    $"the register draws {deepest:0.00} m. That berth is on the mud for part of every " +
                    "big tide.");
            }
        }

        /// <summary>…and the boats actually authored there float where they were actually put. The gate
        /// the builder enforces, asserted on the shipped register.</summary>
        [Test]
        public void EveryBoatAuthoredAtTheFloatFloatsInTheBerthSheClaims()
        {
            foreach (var o in AtTheFloat())
            {
                if (o.Boat == null) continue;
                float carries = NineMileCreekWharf.DeepestDraughtAFloatBerthCarries(o.BerthIndex, _terrain);
                Assert.That(o.Boat.DraughtMeters, Is.LessThanOrEqualTo(carries),
                    $"'{o.Id}' keeps '{o.Boat.Id}' ({o.Boat.DraughtMeters:0.00} m) in float berth " +
                    $"{o.BerthIndex}, which carries {carries:0.00} m. The builder REFUSES her, so she " +
                    "would simply be missing from the wharf rather than aground on it.");
            }
        }

        /// <summary>
        /// ⭐ <b>THE WORKING FLEET IS REFUSED THE FLOAT — on BEAM, which is the honest reason.</b> The
        /// handoff's rule is that a Cape Islander at the float is an authoring error, and it is; but the
        /// number that says so is not the water. At the meander's deepest reach the lane genuinely carries
        /// her draught, so a depth gate alone would let her lie there. What refuses her is size: a hull
        /// wider in the half-beam than the standoff is drawn lying ON a 2.4 m pontoon rather than
        /// alongside it.
        /// </summary>
        [Test]
        public void TheWorkingFleetIsTooWideForTheFloat()
        {
            float widest = NineMileCreekWharf.WidestHalfBeamAFloatBerthCarries;

            foreach (string path in new[]
                     {
                         "Assets/_Project/Data/Boats/CapeIslander.asset",
                         "Assets/_Project/Data/Boats/LobsterBoat.asset",
                     })
            {
                BoatHullDef hull = Hull(path);
                Assert.IsNotNull(hull, $"{path} is missing — this test names the working fleet by asset");
                HullMeshDef mesh = hull.Visual != null ? hull.Visual.HullMesh : null;
                Assert.IsNotNull(mesh, $"'{hull.Id}' has no hull mesh, so she has no measured beam");

                Assert.That(mesh.WatertightHalfBeamMeters, Is.GreaterThan(widest),
                    $"'{hull.Id}' is {mesh.WatertightHalfBeamMeters * 2f:0.00} m in the beam against a " +
                    $"float that admits {widest * 2f:0.00} m. If she now FITS, the working fleet may be " +
                    "authored onto the float and the photograph's two moorings have become one.");
            }
        }

        /// <summary>…and the small craft on the register are inside it, which is the same rule from the
        /// other side: the gate sorts the fleet, and nobody typed the list.</summary>
        [Test]
        public void TheSmallCraftAreInsideTheFloatsBeamLimit()
        {
            float widest = NineMileCreekWharf.WidestHalfBeamAFloatBerthCarries;
            foreach (var o in AtTheFloat())
            {
                float half = NineMileCreekMooredFleet.HalfBeamOf(o);
                if (half <= 0f) continue;   // sprite-only: no mesh, no measured beam — reported, not failed
                Assert.That(half, Is.LessThanOrEqualTo(widest),
                    $"'{o.Id}' is {half * 2f:0.00} m in the beam and the float admits " +
                    $"{widest * 2f:0.00} m");
            }
        }

        /// <summary>
        /// ⭐⭐ <b>CHOOSING THE DEEPER SIDE BUYS REAL DEPTH — the counterfactual that justifies measuring
        /// at all.</b> If both sides of every berth carried much the same water, the per-berth measurement
        /// would be ceremony and a constant would do. They do not: on at least one berth the shallow side
        /// would not float the small craft this register keeps, so a wrong-side constant is a boat on the
        /// bottom rather than a boat slightly off centre.
        /// </summary>
        [Test]
        public void ChoosingTheDeeperSideBuysRealDepth()
        {
            var small = AtTheFloat().Where(o => o.Boat != null).ToList();
            Assert.IsNotEmpty(small, "nobody is authored at the float");
            float needs = small.Max(o => o.Boat.DraughtMeters) + Clearance;

            bool aSideWouldGroundHer = false;
            float widestSpread = 0f;

            for (int i = 0; i < NineMileCreekWharf.FloatBerthCount; i++)
            {
                (Vector2 north, Vector2 south) = NineMileCreekWharf.FloatBerthSides(i);
                float deep = SpringLow - Mathf.Min(_terrain.ElevationAt(north),
                                                   _terrain.ElevationAt(south));
                float shallow = SpringLow - Mathf.Max(_terrain.ElevationAt(north),
                                                      _terrain.ElevationAt(south));

                widestSpread = Mathf.Max(widestSpread, deep - shallow);
                if (shallow < needs) aSideWouldGroundHer = true;

                Assert.That(NineMileCreekWharf.FloatBerthDepthAtSpringLow(i, _terrain),
                    Is.EqualTo(deep).Within(1e-4f),
                    $"float berth {i} reports a depth that is not its deeper side's");
            }

            Assert.IsTrue(aSideWouldGroundHer,
                $"both sides of every float berth carry the {needs:0.00} m this register's small craft " +
                "need. That would make the per-berth side measurement free to replace with a constant — " +
                "which is worth knowing, but check the lane has not simply been widened under the whole " +
                $"run before you simplify it. Widest side-to-side spread measured: {widestSpread:0.00} m.");
        }

        /// <summary>
        /// ⚠ <b>A BERTH IS NOT THE DOCK, and the two numbers must not be quoted at each other.</b>
        /// <c>FloatBedElevationFrom</c> is the SHALLOWEST ground anywhere under the float — a
        /// worst-point-over-a-rectangle, because a rigid raft rests on the highest thing beneath her. A
        /// berth is one point beside her on the better side. Measured here they genuinely differ, and on
        /// today's terrain the berths are the DEEPER of the two: the dock is gated by one corner of her
        /// own footprint. Neither bounds the other, which is the whole reason a hull is gated where the
        /// hull is.
        /// </summary>
        [Test]
        public void ABerthAndTheDockAreDifferentMeasurements()
        {
            float underTheDock = SpringLow - NineMileCreekWharf.FloatBedElevationFrom(_terrain);
            var berths = Enumerable.Range(0, NineMileCreekWharf.FloatBerthCount)
                .Select(i => NineMileCreekWharf.FloatBerthDepthAtSpringLow(i, _terrain))
                .ToList();

            Assert.That(berths.Min(), Is.Not.EqualTo(underTheDock).Within(1e-3f),
                $"every float berth measures exactly the {underTheDock:0.00} m under the dock herself. " +
                "That would mean the berth measurement is reading the dock's number rather than the " +
                "ground beside her — check FloatBerthDepthAtSpringLow is sampling at the BERTH.");
        }

        // =============================================================================================
        //  4. THE BUILDER'S OUTPUT — scene-wired is not builder-wired
        // =============================================================================================

        private static List<MooredBoat> PlaceAndRead(ITidalTerrain terrain)
        {
            NineMileCreekMooredFleet.Place(terrain);
            GameObject root = GameObject.Find(NineMileCreekMooredFleet.RootName);
            Assert.IsNotNull(root, "the placer built no fleet root at all");
            return root.GetComponentsInChildren<MooredBoat>(includeInactive: true).ToList();
        }

        /// <summary>Everybody on the register gets moored — at whichever of the two moorings is theirs.
        /// A refusal is a LogError and would fail this test by itself; this is the count.</summary>
        [Test]
        public void TheBuilderMoorsEveryOwnerOnTheRegister()
        {
            var owners = Owners();
            var boats = PlaceAndRead(_terrain);

            Assert.That(boats.Count, Is.EqualTo(owners.Count),
                $"{owners.Count} owners are authored and the placer moored {boats.Count}. Somebody was " +
                "refused — the log names who and why.");
            CollectionAssert.AreEquivalent(owners.Select(o => o.Id), boats.Select(b => b.Owner.Id),
                "the boats placed are not the owners authored");
        }

        /// <summary>
        /// ⭐ <b>THE PHOTOGRAPH, AS AN ASSERTION.</b> Each boat stands where her mooring says: the float's
        /// small craft alongside the finger in the middle of the bullpen, the working fleet on the wall's
        /// berth line. Read off the placed transforms, so a table that stopped being consulted fails here
        /// rather than in the owner's editor.
        /// </summary>
        [Test]
        public void TheSmallCraftLieAtTheFloatAndTheWorkingBoatsAtTheWall()
        {
            var boats = PlaceAndRead(_terrain);
            int atFloat = 0, atWall = 0;

            foreach (MooredBoat boat in boats)
            {
                BoatOwnerDef o = boat.Owner;
                var at = new Vector2(boat.transform.position.x, boat.transform.position.y);

                if (o.Moorage == BoatMoorage.Float)
                {
                    (Vector2 north, Vector2 south) = NineMileCreekWharf.FloatBerthSides(o.BerthIndex);
                    Assert.That(at == north || at == south,
                        $"'{o.Id}' lies at the float on paper and was placed at {at}, which is neither " +
                        $"side of float berth {o.BerthIndex} ({north} / {south})");
                    Assert.That(boat.HeadingDegrees,
                        Is.EqualTo(NineMileCreekWharf.FloatMooredHeadingDegrees()),
                        $"'{o.Id}' lies across the finger rather than along it");
                    atFloat++;
                }
                else
                {
                    Assert.That(at, Is.EqualTo(NineMileCreekMainland.BerthPos(o.BerthIndex)),
                        $"'{o.Id}' lies at the wall on paper but was not placed on the berth line");
                    Assert.That(boat.HeadingDegrees,
                        Is.EqualTo(NineMileCreekMooredFleet.MooredHeadingDegrees()),
                        $"'{o.Id}' lies across the wall rather than along it");
                    atWall++;
                }
            }

            Assert.That(atFloat, Is.GreaterThan(0), "the finger in the middle of the bullpen lies empty");
            Assert.That(atWall, Is.GreaterThan(0), "the tall quay faces stand over open water");
        }

        /// <summary>Nobody is put in the berth the player docks in — the one exclusion the wall has, and
        /// the float never did (the dock zone is a distance test against the WALL's berth line).</summary>
        [Test]
        public void NoBoatIsPlacedInThePlayersBerth()
        {
            Vector2 player = NineMileCreekMainland.BerthPos(NineMileCreekMooredFleet.PlayerBerthIndex());

            foreach (MooredBoat boat in PlaceAndRead(_terrain))
            {
                var at = new Vector2(boat.transform.position.x, boat.transform.position.y);
                Assert.That(Vector2.Distance(at, player), Is.GreaterThan(0.01f),
                    $"'{boat.Owner.Id}' was placed in the player's own berth at {player} — she docks on " +
                    "top of him");
            }
        }

        /// <summary>Every boat the builder actually put at the float floats there at dead low spring,
        /// measured at the position she was placed at rather than at the berth she claims.</summary>
        [Test]
        public void EveryBoatPlacedAtTheFloatFloatsWhereSheWasPut()
        {
            foreach (MooredBoat boat in PlaceAndRead(_terrain))
            {
                BoatOwnerDef o = boat.Owner;
                if (o.Moorage != BoatMoorage.Float || o.Boat == null) continue;

                var at = new Vector2(boat.transform.position.x, boat.transform.position.y);
                float depth = SpringLow - _terrain.ElevationAt(at);

                Assert.IsFalse(BoatCrossingWouldGround(depth, o.Boat.DraughtMeters),
                    $"'{o.Id}' draws {o.Boat.DraughtMeters:0.00} m and was placed at {at}, where there " +
                    $"is {depth:0.00} m at spring low. She is drawn floating on ground she is sitting on.");
                Assert.That(depth - o.Boat.DraughtMeters, Is.GreaterThanOrEqualTo(Clearance),
                    $"'{o.Id}' floats by only {depth - o.Boat.DraughtMeters:0.00} m at spring low, inside " +
                    $"the region's own {Clearance:0.00} m keel clearance");
            }
        }

        /// <summary>The same rule <c>BoatCrossing.CanFloat</c> holds every hull to, spelled here so a
        /// float berth and a passage cannot disagree about what "aground" means.</summary>
        private static bool BoatCrossingWouldGround(float depth, float draught) => depth < draught;

        /// <summary>
        /// ⚠ <b>THE BUILDER PLACES AND DOES NOT DRAW</b>, and the float's boats are not an exception.
        /// <c>IsoFacetHullPresentationService</c> registers the mesh path at runtime and edit-time
        /// builders deliberately do not, so a builder that skinned a hull here would serialise the SPRITE
        /// fallback into the committed scene and the fleet would silently never be mesh — with the right
        /// number of boats in exactly the right places.
        /// </summary>
        [Test]
        public void TheBuilderPlacesAndDoesNotDraw()
        {
            var boats = PlaceAndRead(_terrain);
            Assert.IsNotEmpty(boats);

            GameObject root = GameObject.Find(NineMileCreekMooredFleet.RootName);
            Assert.That(root.GetComponentsInChildren<Renderer>(includeInactive: true), Is.Empty,
                "the placer baked a renderer into the fleet. The hull mesh path is chosen LIVE by the " +
                "skinner on wake; anything drawn here is the sprite fallback, serialised into " +
                "NineMileCreek.unity, and the fleet would never be mesh again.");

            foreach (MooredBoat boat in boats)
                Assert.IsFalse(boat.IsPresented,
                    $"'{boat.Owner.Id}' has been skinned at BUILD time — see the note on this test");
        }
    }
}
