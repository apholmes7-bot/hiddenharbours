using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE EAST DOCK ALWAYS HAS WATER</b> — the owner's ruling of 2026-08-19, re-derived rather
    /// than trusted.
    ///
    /// <para>St Peters' east berth is the game's front door: a new game pilots the player in aboard a
    /// cape islander and puts them ashore on the wharf. Before this it was the §5.1a "gentle tide
    /// gate" — a −1.0 m slip that bared by 1.20 m at spring low, with its mouth at −1.89 m — so an
    /// arrival that happened to land at dead low water grounded on its own doorstep. The owner ruled
    /// the dock wet at every state of tide, and the answer is geometry rather than a special case:
    /// a dredged channel and berth pocket, cut on the SAME line as the slip so the reef still has
    /// exactly one door through it.</para>
    ///
    /// <para><b>What these tests are for.</b> Every number in the dredge is an arithmetic consequence
    /// of three things the owner may move — the region's tide, the harbour floor, and which hull the
    /// opening arrives on. So nothing here asserts a literal: each test recomputes the requirement
    /// from those sources (the hull's own def and mesh sidecar included) and fails <i>with the number
    /// to use</i>. The one thing that is checked as a literal is the mirror
    /// <see cref="StPetersBuilder.ArrivalHullDraughtMetres"/>, which exists only because a
    /// <c>const</c> cannot load an asset.</para>
    ///
    /// <para><b>⚠ Asserted along the AUTHORED line, never a found minimum.</b> A test that searches for
    /// the deepest water it can find and reports that the channel is deep passes on a channel that is
    /// in the wrong place. These walk the centre-line the builder actually authors, at a fixed step,
    /// and demand the claim at every station of it.</para>
    ///
    /// <para><see cref="StPetersLayoutTests"/> holds the rest of the berth's geometry;
    /// <see cref="NavMarkPlacementTests"/> holds that the buoyed fairway carries what it claims over
    /// this same terrain.</para>
    /// </summary>
    public class StPetersEastBerthTests
    {
        private const string CapeIslanderDefPath =
            "Assets/_Project/Data/Boats/CapeIslander.asset";
        private const string CapeIslanderMeshPath =
            "Assets/_Project/Data/Boats/HullMeshes/CapeIslanderIsoHullMesh.asset";

        /// <summary>Room either side of the arrival hull inside the channel's flat bottom, in metres —
        /// she is steered in, not threaded through. Half of it a side.</summary>
        private const float NavigableMarginMetres = 2f;

        /// <summary>How finely the centre-line and the cross-sections are walked. Fine enough that a
        /// sill or a pinch a metre wide cannot hide between two stations.</summary>
        private const float StationStepMetres = 0.25f;
        private const float ScanStepMetres = 0.05f;

        /// <summary>
        /// ⚠ A millimetre of slack on every depth comparison, and it is load-bearing. The dredged
        /// bed and the depth it is required to hold are the SAME arithmetic evaluated two ways —
        /// spring low minus (draught + clearance) against the bed, and water minus the bed against
        /// (draught + clearance) — so they land within an ULP of each other by construction, and an
        /// exact <c>&lt;</c> would make a correct channel fail on a rounding direction. A millimetre
        /// is far below anything the sea does and far above anything float32 does.
        /// </summary>
        private const float DepthEpsilon = 1e-3f;

        private TidalTerrain _terrain;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TidalTerrain_EastBerthTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            GameServices.Reset();
        }

        // =============================================================================================
        //  the sources of truth — the hull the channel was cut for
        // =============================================================================================

        private static float ArrivalDraughtMetres()
        {
            var hull = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatHullDef>(CapeIslanderDefPath);
            Assert.IsNotNull(hull, $"the arrival hull's def must exist at {CapeIslanderDefPath}");
            Assert.AreEqual(StPetersBuilder.ArrivalHullId, hull.Id,
                "the def at the arrival hull's path is not the hull the region names");
            Assert.Greater(hull.DraughtMeters, 0f, "a hull with no draught cannot be floated");
            return hull.DraughtMeters;
        }

        private static float ArrivalBeamMetres()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<HullMeshDef>(CapeIslanderMeshPath);
            Assert.IsNotNull(mesh, $"the arrival hull's mesh sidecar must exist at {CapeIslanderMeshPath}");
            Assert.Greater(mesh.WatertightHalfBeamMeters, 0f,
                "the sidecar reports no watertight half-beam — the channel's width has nothing to be " +
                "sized against, and a width picked without it is a guess");
            return mesh.WatertightHalfBeamMeters * 2f;
        }

        private static float SpringLowWater() =>
            StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;

        private static float Water(float tideFraction) =>
            StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude * tideFraction;

        // =============================================================================================
        // 1. the depth is DERIVED from the hull, not chosen
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>The whole ruling in one line of arithmetic.</b> The bed must clear the arrival hull's
        /// draught plus her stated under-keel clearance at the LOWEST water this region has. Recomputed
        /// here from the def and the tide, so a retune of either fails with the depth to dredge to.
        /// </summary>
        [Test]
        public void TheDredgedBedClearsTheArrivalHullAtSpringLow_DerivedFromHerOwnDraught()
        {
            float draught = ArrivalDraughtMetres();

            Assert.AreEqual(draught, StPetersBuilder.ArrivalHullDraughtMetres, 1e-4f,
                $"the region mirrors the arrival hull's draught as {StPetersBuilder.ArrivalHullDraughtMetres:F2} m " +
                $"but her def says {draught:F2} m. The mirror exists because a const cannot load an " +
                "asset; it is only safe while this holds. Update StPetersBuilder.ArrivalHullDraughtMetres.");

            float required = SpringLowWater() - (draught + StPetersBuilder.BerthUnderKeelClearance);

            Assert.LessOrEqual(StPetersBuilder.ApproachBedElevation, required + 1e-4f,
                $"the approach is dredged to {StPetersBuilder.ApproachBedElevation:F2} m but must reach " +
                $"{required:F2} m to hold {draught:F2} m of draught plus " +
                $"{StPetersBuilder.BerthUnderKeelClearance:F2} m of clearance at spring low " +
                $"({SpringLowWater():F2} m). The owner ruled this dock wet at every tide.");

            // ⭐ The clearance is not a taste: it is exactly what this region's own deep floor gives the
            // same hull at the same tide. If that stops being true the number has drifted into being an
            // opinion, and the comment that says otherwise has become a lie.
            float whatTheHarbourGives =
                SpringLowWater() - StPetersBuilder.DeepHarbourElevation - draught;
            Assert.AreEqual(whatTheHarbourGives, StPetersBuilder.BerthUnderKeelClearance, 1e-3f,
                $"the stated clearance {StPetersBuilder.BerthUnderKeelClearance:F2} m no longer equals " +
                $"the {whatTheHarbourGives:F2} m the −{-StPetersBuilder.DeepHarbourElevation:F1} m harbour " +
                "floor itself offers her. The dredge is supposed to cut the slip DOWN TO the floor and " +
                "stop — offering the approach's own water and no more.");

            Assert.AreEqual(StPetersBuilder.DeepHarbourElevation,
                StPetersBuilder.ApproachBedElevation, 1e-4f,
                "the derived bed no longer folds to the deep-harbour floor — see the note on " +
                "ApproachBedElevation, which claims it does BY CONSTRUCTION");

            Debug.Log($"[st-peters/berth] spring low {SpringLowWater():F2} m − ({draught:F2} draught + " +
                      $"{StPetersBuilder.BerthUnderKeelClearance:F2} clearance) = {required:F2} m required; " +
                      $"dredged to {StPetersBuilder.ApproachBedElevation:F2} m.");
        }

        // =============================================================================================
        // 2. every station of the AUTHORED line is wet at spring low
        // =============================================================================================

        /// <summary>
        /// ⭐ The claim the owner actually made, walked end to end: from the channel's mouth out on the
        /// drop-off to the head of the berth pocket, no station is dry — and none is merely wet either,
        /// each carries the arrival hull with her clearance.
        ///
        /// <para>⚠ Walked along <see cref="StPetersBuilder.ApproachFrom"/>→
        /// <see cref="StPetersBuilder.ApproachTo"/>, the line the builder authors. Searching for the
        /// deepest water instead would pass on a channel dredged in the wrong place.</para>
        /// </summary>
        [Test]
        public void EveryStationOfTheDredgedChannelCarriesHerAtSpringLow()
        {
            float draught = ArrivalDraughtMetres();
            float needed = draught + StPetersBuilder.BerthUnderKeelClearance;
            float water = SpringLowWater();

            float worst = float.MaxValue;
            Vector2 worstAt = Vector2.zero;

            foreach (Vector2 p in Stations())
            {
                float depth = water - _terrain.ElevationAt(p);
                if (depth < worst) { worst = depth; worstAt = p; }
            }

            Assert.GreaterOrEqual(worst, needed - DepthEpsilon,
                $"the channel carries only {worst:F2} m at ({worstAt.x:F1}, {worstAt.y:F1}) at spring " +
                $"low, against the {needed:F2} m the arrival hull needs. The east dock is ruled wet at " +
                "every tide — this station is where it stops being.");

            Debug.Log($"[st-peters/berth] the authored channel line carries {worst:F2} m at its worst " +
                      $"station at spring low (needs {needed:F2}).");
        }

        /// <summary>
        /// And the pocket holds her whole LENGTH, not just the point she is moored at — a hull is
        /// 12.9 m of boat, and a pocket sized to her mooring pin alone puts her bow on the sand.
        /// </summary>
        [Test]
        public void TheBerthPocketHoldsHerWholeLength()
        {
            var hull = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatHullDef>(CapeIslanderDefPath);
            Assert.IsNotNull(hull);
            Assert.Greater(hull.LengthMeters, 0f, "a hull with no length cannot be berthed");

            float needed = ArrivalDraughtMetres() + StPetersBuilder.BerthUnderKeelClearance;
            float water = SpringLowWater();
            var moored = new Vector2(StPetersBuilder.DoryMooredPos.x, StPetersBuilder.DoryMooredPos.y);

            // She lies along the channel — bow shoreward, stern seaward — so her extent is half her
            // length each way along the channel's own axis.
            Vector2 axis = (StPetersBuilder.ApproachTo - StPetersBuilder.ApproachFrom).normalized;
            float half = hull.LengthMeters * 0.5f;

            for (float t = -half; t <= half + 1e-4f; t += StationStepMetres)
            {
                Vector2 p = moored + axis * t;
                float depth = water - _terrain.ElevationAt(p);
                Assert.GreaterOrEqual(depth, needed - DepthEpsilon,
                    $"{Mathf.Abs(t):F1} m {(t < 0f ? "astern of" : "ahead of")} the mooring at " +
                    $"({p.x:F1}, {p.y:F1}) there is {depth:F2} m at spring low against {needed:F2} m " +
                    $"needed. The pocket is shorter than the {hull.LengthMeters:F1} m hull it berths — " +
                    "move StPetersBuilder.ApproachTo shoreward.");
            }
        }

        // =============================================================================================
        // 3. she FITS — and the channel narrows without drying
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>The width a pure falloff curve cannot give you.</b> The old single-slope trough put
        /// the design depth at one point, so carving to −4 m would have left 3.7 m of usable water
        /// across against a 4.80 m beam — a channel narrower than the boat it was cut for, and every
        /// depth assertion above would still have passed. The flat bottom is what states the width,
        /// and this is what holds it against the hull's own sidecar.
        /// </summary>
        [Test]
        public void TheChannelIsWiderThanHerBeamAtSpringLow_MeasuredAtEveryStation()
        {
            float beam = ArrivalBeamMetres();
            float requiredWidth = beam + NavigableMarginMetres;
            float needed = ArrivalDraughtMetres() + StPetersBuilder.BerthUnderKeelClearance;

            float worst = float.MaxValue;
            Vector2 worstAt = Vector2.zero;

            foreach (Vector2 p in Stations())
            {
                float w = NavigableWidthAt(p, SpringLowWater(), needed);
                if (w < worst) { worst = w; worstAt = p; }
            }

            Assert.GreaterOrEqual(worst, requiredWidth - DepthEpsilon,
                $"the channel pinches to {worst:F2} m of navigable water at ({worstAt.x:F1}, " +
                $"{worstAt.y:F1}) at spring low, against the {beam:F2} m beam plus " +
                $"{NavigableMarginMetres:F1} m of room = {requiredWidth:F2} m. Widen " +
                "StPetersBuilder.ApproachThalwegHalfWidth — the FLAT bottom is what sets this, not " +
                "the half-width.");

            Assert.GreaterOrEqual(StPetersBuilder.ApproachThalwegHalfWidth * 2f,
                                  requiredWidth - DepthEpsilon,
                "the flat bottom is narrower than the hull needs even before the shoulders are " +
                "counted — the shoulders are a bonus that shrinks with the tide, never the budget");

            Debug.Log($"[st-peters/berth] narrowest navigable width at spring low {worst:F2} m " +
                      $"(beam {beam:F2} + margin {NavigableMarginMetres:F1}).");
        }

        /// <summary>
        /// ⭐ The owner's picture, as an ordering: <i>"the channel SHRINKS IN WIDTH at low tide but
        /// stays navigable."</i> Both halves — strictly wider as the water rises, and never nil.
        /// </summary>
        [Test]
        public void TheChannelNarrowsAsTheTideFalls_AndNeverCloses()
        {
            float needed = ArrivalDraughtMetres() + StPetersBuilder.BerthUnderKeelClearance;
            float neap = GameConfig.DefaultNeapAmplitudeFraction;   // the constant, not a re-typed 0.45
            var mid = Vector2.Lerp(StPetersBuilder.ApproachFrom, StPetersBuilder.ApproachTo, 0.5f);

            float atSpringLow = NavigableWidthAt(mid, Water(-1f), needed);
            float atNeapLow = NavigableWidthAt(mid, Water(-neap), needed);
            float atMean = NavigableWidthAt(mid, Water(0f), needed);

            Assert.Greater(atSpringLow, 0f,
                "the channel closes at spring low — the one thing the ruling forbids");
            Assert.Greater(atNeapLow, atSpringLow,
                $"the channel is {atNeapLow:F2} m wide at neap low against {atSpringLow:F2} m at " +
                "spring low — it is supposed to NARROW as the water falls, which is what makes a " +
                "dredged cut read as a channel rather than as a trench");
            Assert.Greater(atMean, atNeapLow,
                $"the channel is {atMean:F2} m wide at mean water against {atNeapLow:F2} m at neap " +
                "low — the same ordering, one rung up");

            Debug.Log($"[st-peters/berth] navigable width mid-channel: spring low {atSpringLow:F2} m → " +
                      $"neap low {atNeapLow:F2} m → mean {atMean:F2} m.");
        }

        // =============================================================================================
        // 4. the dredge moves the channel and NOTHING else
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The additive guarantee, measured.</b> Everything this region already decided — the
        /// pier root's dry ground, the deck elevation measured off it, the beach slip's own tide gate,
        /// the reef ring, the bar, the island — must come out of the new terrain bit-for-bit as it came
        /// out of the old one. The comparison is against a terrain configured exactly as the region is
        /// and then had its approach switched OFF, so the only difference between the two is the
        /// feature under test.
        ///
        /// <para>This is the test that would have caught the version of this change that moved
        /// <see cref="StPetersWharf.RootCellX"/>'s +5.35 m. The pier root's margin over spring high
        /// water is thin by design, and a dredge that crept 15 m further west would have flooded the
        /// planks — silently, because nothing else looks there.</para>
        /// </summary>
        [Test]
        public void TheDredgeMovesTheChannelAndNothingElse()
        {
            var beforeGo = new GameObject("TidalTerrain_WithoutApproach");
            try
            {
                var before = beforeGo.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(before);
                var so = new SerializedObject(before);
                so.FindProperty("_approachHalfWidth").floatValue = 0f;   // the feature, switched off
                so.ApplyModifiedPropertiesWithoutUndo();

                var probes = new (string what, Vector2 at)[]
                {
                    ("the pier root",            new Vector2(StPetersWharf.RootCellX, 0f)),
                    ("the pier root's north edge", new Vector2(StPetersWharf.RootCellX, StPetersWharf.MaxCellY)),
                    ("the pier root's south edge", new Vector2(StPetersWharf.RootCellX, StPetersWharf.MinCellY)),
                    ("the beach slip's head",    StPetersBuilder.BerthTo),
                    // ⚠ The TIGHTEST of these by a long way. The deck's north and south lips sit
                    // 8.5 m from the dredged centre-line against a half-width of 8 — half a metre of
                    // margin — and StPetersWharfWalkabilityTests reads them as the +1.09 m shoal
                    // beside the berth. Move ApproachTo six metres shoreward and they become −4 m
                    // water, with nothing but that test to say so.
                    ("the deck's north lip",     new Vector2(StPetersWharf.DeckFootprint().center.x,
                                                             StPetersWharf.DeckFootprint().yMax + 1f)),
                    ("the deck's south lip",     new Vector2(StPetersWharf.DeckFootprint().center.x,
                                                             StPetersWharf.DeckFootprint().yMin - 1f)),
                    ("the beach above the slip", new Vector2(StPetersBuilder.BerthTo.x - 4f, 0f)),
                    ("the slip's own bed",       new Vector2(StPetersBuilder.BerthTo.x + 5f, 0f)),
                    ("the reef ring, north",     new Vector2(215f,  40f)),
                    ("the reef ring, south",     new Vector2(215f, -40f)),
                    ("the sandbar",              new Vector2(-200f, 0f)),
                    ("the island's village",     new Vector2(StPetersBuilder.StartSpawnPos.x,
                                                             StPetersBuilder.StartSpawnPos.y)),
                    ("the bar landing",          new Vector2(-45f, 0f)),
                };

                foreach ((string what, Vector2 at) in probes)
                    Assert.AreEqual(before.ElevationAt(at), _terrain.ElevationAt(at), 1e-5f,
                        $"{what} at ({at.x:F1}, {at.y:F1}) MOVED when the approach was dredged — it is " +
                        "outside the cut and must not have. Check ApproachTo and ApproachHalfWidth.");

                // …and the pier's planks are still clear of the highest water, which is the consequence
                // the probes above exist to protect.
                float root = _terrain.ElevationAt(new Vector2(StPetersWharf.RootCellX, 0f));
                Assert.Greater(root, Water(1f),
                    $"the pier root sits at {root:F2} m against spring high water {Water(1f):F2} m — " +
                    "the deck is measured off this ground, so a pier whose root floods is a pier that " +
                    "cannot be walked out to the boat");
            }
            finally
            {
                Object.DestroyImmediate(beforeGo);
            }
        }

        /// <summary>
        /// ⚠ The other half of "nothing else moved": the ruling wetted the DOCK, not the region. The
        /// reef apron and the beach slip are still supposed to bare near spring low — that is the
        /// §5.1a draught gate and the reason the island reads the way it does — and a dredge that
        /// quietly drowned them would have deleted the geography while passing every test above.
        /// </summary>
        [Test]
        public void TheReefAndTheBeachSlipStillBareAtSpringLow()
        {
            float water = SpringLowWater();

            float apron = _terrain.ElevationAt(new Vector2(215f, 40f));
            Assert.Greater(apron, water,
                $"the reef apron sits at {apron:F2} m and no longer bares at spring low ({water:F2} m) " +
                "— the ring is the §5.1a draught gate, and the ruling wetted the dock, not the reef");

            var slip = new Vector2(StPetersBuilder.BerthTo.x + 5f, 0f);
            float bed = _terrain.ElevationAt(slip);
            Assert.Greater(bed, water,
                $"the beach slip sits at {bed:F2} m and no longer bares at spring low ({water:F2} m) — " +
                "wading out to it is meant to mean reading the tide");

            Debug.Log($"[st-peters/berth] still baring at spring low: reef apron {apron:F2} m, beach " +
                      $"slip {bed:F2} m (water {water:F2} m).");
        }

        /// <summary>
        /// 🔴 <b>A dredge deeper than the height map can encode is a dredge that only half exists.</b>
        /// The painted seabed stores elevation in one R8 channel across
        /// <c>[SeabedMinElevation, SeabedMaxElevation]</c>, and anything below the floor CLIPS to it —
        /// silently. The sim would keep reading the analytic −4.2 m while the shader, the splat ground
        /// and any future adopted <c>PaintedTidalTerrain</c> read −4.0: paint ≠ sail, which is the one
        /// thing ADR 0014 exists to prevent, and no depth or width test above would notice.
        /// </summary>
        [Test]
        public void TheDredgedBedFitsInsideThePaintedHeightMapsEncodingRange()
        {
            Assert.GreaterOrEqual(StPetersBuilder.ApproachBedElevation,
                                  TerrainPaintTool.SeabedMinElevation - 1e-4f,
                $"the approach is dredged to {StPetersBuilder.ApproachBedElevation:F2} m, below the " +
                $"{TerrainPaintTool.SeabedMinElevation:F2} m floor the painted seabed encodes. The bake " +
                "would clip it and the picture would stop agreeing with the tide (ADR 0014: paint = " +
                "sail). Lower StPetersBuilder.DeepHarbourElevation — the encoding range is read from " +
                "it — before dredging past it.");
        }

        // =============================================================================================
        // 5. it is still ONE door
        // =============================================================================================

        /// <summary>
        /// §5.1a's reading — <i>"exactly one dredged slip cuts a door through the reef"</i> — survives
        /// the dredge because the cut runs the SAME line at the SAME width. Two 16 m cuts on one
        /// centre-line are one door; two cuts anywhere else would be two.
        /// </summary>
        [Test]
        public void TheDredgeIsTheSameDoor_NotASecondOne()
        {
            Assert.AreEqual(StPetersBuilder.BerthHalfWidth, StPetersBuilder.ApproachHalfWidth, 1e-4f,
                "the dredged cut is a different width from the slip it shares a line with — the reef " +
                "would read as having a door and a gate rather than one door");

            // Both centre-lines are the same ray out of the island: the approach's own line, extended,
            // must pass through the slip's head.
            float offLine = DistanceToSegment(StPetersBuilder.BerthTo,
                                              StPetersBuilder.ApproachFrom,
                                              StPetersBuilder.ApproachTo
                                              + (StPetersBuilder.ApproachTo - StPetersBuilder.ApproachFrom));
            Assert.Less(offLine, 1e-3f,
                $"the slip head is {offLine:F2} m off the dredged channel's line — the two cuts have " +
                "come apart, and the reef now has two doors");

            // The mouth genuinely reaches the drop-off, or the channel is a pocket nothing can enter —
            // which is exactly what the un-dredged slip was.
            float mouthBed = _terrain.ElevationAt(
                StPetersBuilder.ApproachFrom
                + (StPetersBuilder.ApproachFrom - StPetersBuilder.ApproachTo).normalized
                  * StPetersBuilder.ApproachHalfWidth);
            Assert.LessOrEqual(mouthBed, StPetersBuilder.ApproachBedElevation + 1e-3f,
                $"just outside the channel's mouth the seabed is {mouthBed:F2} m, shallower than the " +
                $"{StPetersBuilder.ApproachBedElevation:F2} m bed inside it — the cut ends in a sill, " +
                "so it is a dead-end pocket rather than a way in. Move ApproachFrom seaward until it " +
                "lands on the natural floor.");
        }

        // =============================================================================================
        // 6. ⚠ WHAT THE DREDGE COST — the door's own ladder, reported for the owner
        // =============================================================================================

        /// <summary>
        /// ⚠⚠ <b>THE RULING NECESSARILY ADMITS MORE THAN THE HULL IT WAS MADE FOR, and this is where
        /// that is written down and bounded.</b>
        ///
        /// <para>§5.1a gated the working hulls at the berth: against a −1.0 m bed the lobster boat was
        /// afloat there 45.6% of the cycle and the cape islander 44.2%, which was the *"you have
        /// outgrown home"* beat. A dock that is wet at spring low cannot keep that. The region's tide
        /// range is 4.4 m and the entire gap between the arrival hull and the lobster boat is 0.10 m of
        /// draught — floating one at the lowest water floats the other, and every hull under them, at
        /// every hour. No bed elevation separates them; this is arithmetic, not an implementation
        /// choice.</para>
        ///
        /// <para><b>What survives is a ladder of a different kind: the door admits by BEAM.</b> The cut
        /// is 8 m of flat bottom with shoulders that only open as the tide floods, so a hull too wide
        /// for the water deep enough to carry her simply cannot come up it. Measured along the whole
        /// authored channel:</para>
        ///
        /// <list type="bullet">
        /// <item>dory · punt · sport skiff · lobster boat · <b>cape islander</b> — in at EVERY tide (the ruling)</item>
        /// <item>side dragger (2.90 m, 7.0 m beam) — in at every tide except spring low</item>
        /// <item>stern trawler (4.20 m, 9.4 m beam) — only around high water</item>
        /// <item>coastal packet (5.00 m, 11.0 m beam) — only at spring high</item>
        /// <item>tanker (6.50 m, 18.0 m beam) — <b>NEVER</b></item>
        /// </list>
        ///
        /// <para><b>⚠ The owner's lever, if the dragger should be back outside.</b> Narrow
        /// <see cref="StPetersBuilder.ApproachThalwegHalfWidth"/>: her 7.0 m beam against the cape
        /// islander's 4.80 m is the only place in the fleet where a width can still cut. It is a
        /// one-constant change, and it is the door's version of §5.1a's own advice — *"raise the shelf
        /// rather than lowering the boats"*. It is NOT taken here: 8 m gives the arrival 1.6 m either
        /// side, and re-gating the fleet as a silent side effect of an arrival PR is exactly the kind
        /// of thing that should be a ruling.</para>
        ///
        /// <para>What this test PINS, rather than merely reporting: the ordering is monotone (a bigger
        /// hull is never admitted at more states of tide than a smaller one), the arrival hull is in at
        /// every state, and the top of the fleet is still barred at every state.</para>
        /// </summary>
        [Test]
        public void TheDoorAdmitsByBeamNow_MonotoneUpTheFleet_AndTheTankerNeverGetsIn()
        {
            var fleet = new[] { "Dory", "Punt", "SportSkiff", "LobsterBoat", "CapeIslander",
                                "SideDragger", "SternTrawler", "CoastalPacket", "Tanker" };
            float[] states = { -1f, -GameConfig.DefaultNeapAmplitudeFraction, 0f,
                                GameConfig.DefaultNeapAmplitudeFraction, 1f };
            string[] stateNames = { "spring low", "neap low", "mean", "neap high", "spring high" };

            var report = new System.Text.StringBuilder(
                "[st-peters/berth] who can come up the dredged door, and when:\n");
            int previousAdmitted = int.MaxValue;
            int arrivalAdmitted = -1, tankerAdmitted = -1;

            foreach (string name in fleet)
            {
                var hull = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatHullDef>(
                    $"Assets/_Project/Data/Boats/{name}.asset");
                var mesh = AssetDatabase.LoadAssetAtPath<HullMeshDef>(
                    $"Assets/_Project/Data/Boats/HullMeshes/{name}IsoHullMesh.asset");
                if (hull == null || mesh == null || mesh.WatertightHalfBeamMeters <= 0f) continue;

                float beam = mesh.WatertightHalfBeamMeters * 2f;
                int admitted = 0;
                var when = new System.Text.StringBuilder();
                for (int i = 0; i < states.Length; i++)
                {
                    float w = NarrowestWidthAlongTheChannel(Water(states[i]), hull.DraughtMeters);
                    bool ok = w >= beam;
                    if (ok) { admitted++; when.Append(when.Length > 0 ? ", " : "").Append(stateNames[i]); }
                }

                report.AppendLine($"  {name,-15} draught {hull.DraughtMeters:F2} m, beam {beam:F2} m → " +
                                  (admitted == 0 ? "NEVER" : when.ToString()));

                Assert.LessOrEqual(admitted, previousAdmitted,
                    $"{name} is admitted at more states of tide than a smaller hull above her in the " +
                    "list — the door has stopped being a ladder, which means a width or a depth is " +
                    "wrong rather than merely generous");
                previousAdmitted = admitted;

                if (hull.Id == StPetersBuilder.ArrivalHullId) arrivalAdmitted = admitted;
                if (name == "Tanker") tankerAdmitted = admitted;
            }

            Assert.AreEqual(states.Length, arrivalAdmitted,
                "the arrival hull is not admitted at every state of tide — that IS the ruling, and " +
                "every depth test above is meaningless if she cannot fit through the cut she needs");
            Assert.AreEqual(0, tankerAdmitted,
                "the tanker can reach the wharf. The dredge was supposed to open the door for the " +
                "arrival, not to turn a modest island wharf into a commercial quay — narrow " +
                "StPetersBuilder.ApproachThalwegHalfWidth or shallow the bed.");

            Debug.Log(report.ToString());
        }

        /// <summary>The narrowest water along the whole authored channel that carries
        /// <paramref name="draught"/> at <paramref name="water"/> — what actually gates a hull, since a
        /// pinch anywhere stops her everywhere past it. Stepped coarser than the depth walks: a hull is
        /// metres wide, so a metre of station spacing cannot hide a pinch she would fit through.</summary>
        private float NarrowestWidthAlongTheChannel(float water, float draught)
        {
            float worst = float.MaxValue;
            Vector2 a = StPetersBuilder.ApproachFrom, b = StPetersBuilder.ApproachTo;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
            for (int i = 0; i <= steps; i++)
                worst = Mathf.Min(worst, NavigableWidthAt(Vector2.Lerp(a, b, i / (float)steps),
                                                          water, draught));
            return worst;
        }

        // =============================================================================================
        //  helpers
        // =============================================================================================

        /// <summary>The AUTHORED centre-line, walked at a fixed step. Never a search.</summary>
        private static System.Collections.Generic.IEnumerable<Vector2> Stations()
        {
            Vector2 a = StPetersBuilder.ApproachFrom, b = StPetersBuilder.ApproachTo;
            float length = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / StationStepMetres));
            for (int i = 0; i <= steps; i++)
                yield return Vector2.Lerp(a, b, i / (float)steps);
        }

        /// <summary>
        /// How wide the water is at <paramref name="p"/>, across the channel, that carries
        /// <paramref name="needed"/> metres of draught at <paramref name="water"/>. Scanned outward
        /// from the centre-line to the first station that fails, both ways — so a channel with a
        /// shoal ISLAND in the middle of it reports the width of the usable side, not the sum.
        /// </summary>
        private float NavigableWidthAt(Vector2 p, float water, float needed)
        {
            Vector2 across = Vector2.Perpendicular(
                (StPetersBuilder.ApproachTo - StPetersBuilder.ApproachFrom).normalized);

            float Reach(float sign)
            {
                float d = 0f;
                while (d <= StPetersBuilder.ApproachHalfWidth * 4f)
                {
                    if (water - _terrain.ElevationAt(p + across * (sign * d)) < needed - DepthEpsilon)
                        break;
                    d += ScanStepMetres;
                }
                return Mathf.Max(0f, d - ScanStepMetres);
            }

            if (water - _terrain.ElevationAt(p) < needed - DepthEpsilon) return 0f;
            return Reach(1f) + Reach(-1f);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + t * ab);
        }
    }
}
