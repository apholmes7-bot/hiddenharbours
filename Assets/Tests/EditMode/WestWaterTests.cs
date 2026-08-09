using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE WEST WATER</b> — the bay's first open-water scene (world-map-plan §6 step 1), and the
    /// case the water-scene template is proved on before the mid-bay and the east water copy it.
    ///
    /// <para><b>The load-bearing tests in this file are the two SEAM tests.</b> A water scene's whole job
    /// is to sit between places, and the one thing it can get wrong that nothing else would catch is the
    /// depth it reports where it meets them. Sail across a seam whose two sides disagree and the sounder
    /// steps — a metre of seabed appearing or vanishing under a hull at the exact instant the player is
    /// watching an instrument. Nine Mile Creek's half of the tidal bar had to borrow St Peters' shoulder
    /// elevation for the same reason (the flats would otherwise have narrowed by a quarter across the
    /// load); a region touching BOTH of them has that exposure at twice the seams.</para>
    ///
    /// <para><b>Everything here is built through the same calls the builder uses</b>
    /// (<see cref="WestWaterPlan.Plan"/>, <see cref="WaterScenePlan.ConfigureTerrain"/>,
    /// <see cref="WaterSceneTemplate.ApplyRegionFacts"/>), so a test can never assert a region the scene
    /// does not have.</para>
    ///
    /// <para><b>No scene, no OnEnable.</b> EditMode never runs a region's activation, so nothing here
    /// asserts registration into <c>GameServices</c> — that is a rig fact and it lives in
    /// <c>WestWaterSailPlayTests</c>. What lives here is the arithmetic, which is where the seam bugs
    /// actually are.</para>
    /// </summary>
    public class WestWaterTests
    {
        private GameObject _westWaterGo;
        private GameObject _islandGo;
        private GameObject _mainlandGo;
        private RectTidalTerrain _westWater;
        private TidalTerrain _island;
        private MainlandTidalTerrain _mainland;

        [SetUp]
        public void SetUp()
        {
            // All three regions stood up through their OWN configure calls — the west water's neighbours
            // included, because "the seam matches" is a claim about two terrains and cannot be made by
            // reading one of them.
            _westWaterGo = new GameObject("WestWater_Test");
            _westWater = _westWaterGo.AddComponent<RectTidalTerrain>();
            WestWaterPlan.Plan.ConfigureTerrain(_westWater);

            _islandGo = new GameObject("StPeters_SeamReference");
            _island = _islandGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_island);

            _mainlandGo = new GameObject("NineMileCreek_SeamReference");
            _mainland = _mainlandGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_mainland);
        }

        [TearDown]
        public void TearDown()
        {
            if (_westWaterGo != null) Object.DestroyImmediate(_westWaterGo);
            if (_islandGo != null) Object.DestroyImmediate(_islandGo);
            if (_mainlandGo != null) Object.DestroyImmediate(_mainlandGo);
            GameServices.Reset();
        }

        private static Vector2 XY(Vector3 v) => new Vector2(v.x, v.y);

        // =============================================================================================
        //  ⭐ THE SEAMS — the load-bearing pair
        // =============================================================================================

        [Test]
        public void AtTheEastDoor_TheBedIsStPetersOwnFloor()
        {
            // Sail east out of the west water and you are in St Peters. The bed under the door must be
            // the bed St Peters has under ITS door, or the sounder steps across the load.
            float here = WestWaterPlan.ElevationAt(WestWaterPlan.ToStPetersPassagePos);
            float there = _island.ElevationAt(XY(StPetersBuilder.ToWestWaterPassagePos));

            Assert.AreEqual(there, here, 1e-4f,
                "the west water's east door and St Peters' sea door are the two sides of one seam. " +
                $"Here {here:0.000} m, there {there:0.000} m — a hull crossing would find the bottom " +
                "move under her.");
            Assert.AreEqual(StPetersBuilder.DeepHarbourElevation, here, 1e-4f,
                "and the number itself is the ISLAND'S — reached for through StPetersBuilder, never " +
                "re-typed, so a retune of the island's floor moves this seam with it");
        }

        [Test]
        public void AtTheWestDoor_TheBedIsNineMileCreeksOwnBayFloor()
        {
            float here = WestWaterPlan.ElevationAt(WestWaterPlan.ToNineMileCreekPassagePos);
            float there = _mainland.ElevationAt(XY(NineMileCreekMainland.ToWestWaterPassagePos));

            Assert.AreEqual(there, here, 1e-4f,
                "the west water's west door and the mainland's sea door are the two sides of one seam. " +
                $"Here {here:0.000} m, there {there:0.000} m.");
            Assert.AreEqual(NineMileCreekMainland.BayFloorElevation, here, 1e-4f,
                "and the number is the MAINLAND'S, reached for rather than copied");
        }

        [Test]
        public void TheSouthernBoundaryIsTheBarsOwnShoulder()
        {
            // ⭐ The third seam, and the one that is not a door: the west water's south edge IS the tidal
            // bar's northern flank. Nine Mile Creek borrowed St Peters' shoulder so the crossing would
            // not narrow across the load; this region borrows the same number so the water beside the
            // crossing does not shelve differently from the crossing itself.
            var southEdge = new Vector2(WestWaterPlan.RegionWorldCenter.x,
                                        WestWaterPlan.RegionWorldCenter.y - WestWaterPlan.RegionWorldSize.y * 0.5f);

            Assert.AreEqual(NineMileCreekMainland.BarFlankToeElevation,
                            WestWaterPlan.ElevationAt(southEdge), 1e-4f,
                "the south edge is the bar's shoulder, at the bar's own flank-toe elevation");
            Assert.AreEqual(StPetersBuilder.DeepHarbourElevation,
                            NineMileCreekMainland.BarFlankToeElevation, 1e-4f,
                "…and the mainland's shoulder is still St Peters' deep floor, which is the assumption " +
                "this region inherited when it borrowed the shoulder. If this ever fails, the crossing " +
                "changed and the west water has to be re-read, not re-tuned");
        }

        [Test]
        public void TheTideIsTheNeighboursTide_BecauseTheyShareTerrain()
        {
            // world-map-plan §4.3: adjacent regions sharing terrain must share a tide profile. This
            // region shares the bar's shoulder with both of them, so it has no freedom at all.
            Assert.AreEqual(StPetersBuilder.TideMean, WestWaterPlan.TideMean, 1e-6f);
            Assert.AreEqual(StPetersBuilder.TideAmplitude, WestWaterPlan.TideAmplitude, 1e-6f);
            Assert.AreEqual(StPetersBuilder.TidePhaseHours, WestWaterPlan.TidePhaseHours, 1e-6f);

            Assert.AreEqual(NineMileCreekMainland.TideMean, WestWaterPlan.TideMean, 1e-6f,
                "all three regions of the home arc run one tide — two tides across a shared seam is a " +
                "crossing that is dry on one side and flooded on the other at the same instant");
            Assert.AreEqual(NineMileCreekMainland.TideAmplitude, WestWaterPlan.TideAmplitude, 1e-6f);
            Assert.AreEqual(NineMileCreekMainland.TidePhaseHours, WestWaterPlan.TidePhaseHours, 1e-6f);
        }

        // =============================================================================================
        //  the region's facts, on the asset the game actually reads
        // =============================================================================================

        [Test]
        public void TheCommittedRegionDefSaysWhatThePlanSays()
        {
            // ⭐ THE #462 LESSON. A LoadOrCreate initialiser fires only on CREATE, so a shipped def can
            // sit there saying whatever it said the day it was written while the builder authors
            // something else entirely and reports success — which is exactly how St Peters came to
            // publish a 160 × 120 m extent against a builder authoring 760 × 520. The builder therefore
            // re-applies the facts unconditionally, and this holds the COMMITTED asset to the plan so a
            // re-run is never the only way to get them.
            var def = AssetDatabase.LoadAssetAtPath<RegionDef>(
                WaterSceneTemplate.RegionAssetPathFor(WestWaterPlan.RegionAssetName));
            Assert.IsNotNull(def, "the west water's RegionDef is committed, not minted on first build — " +
                                  "the neighbours' builders look it up by path to hang their sea doors on");

            var plan = WestWaterPlan.Plan;

            Assert.AreEqual(WestWaterPlan.RegionId, def.Id, "ids are append-only and stable (ADR 0009)");
            Assert.AreEqual(WestWaterPlan.SceneName, def.SceneName);
            Assert.AreEqual(plan.WorldCenter, def.WorldCenter);
            Assert.AreEqual(plan.WorldSize, def.WorldSizeMeters);
            Assert.AreEqual(plan.SeabedPixelsPerMetre, def.SeabedPixelsPerMetre, 1e-6f);
            Assert.AreEqual(plan.TideMean, def.TideMeanLevel, 1e-6f);
            Assert.AreEqual(plan.TideAmplitude, def.TideAmplitude, 1e-6f);
            Assert.AreEqual(plan.TidePhaseHours, def.TidePhaseHours, 1e-6f);
            Assert.IsFalse(def.IsDeepHarbour, "open water is not a harbour");
            Assert.IsEmpty(def.SpawnFishIds,
                "the home grounds are the MID-BAY's (world-map-plan §2.2) — a fishery here would be a " +
                "later phase smuggled into this one");

            // And the same push the builder makes must produce the same thing, so the committed copy and
            // a fresh build cannot disagree. Applied to a THROWAWAY instance rather than to the asset: a
            // test that dirties a committed asset to prove a point has changed the project to measure it.
            var fresh = ScriptableObject.CreateInstance<RegionDef>();
            try
            {
                WaterSceneTemplate.ApplyRegionFacts(fresh, plan);
                Assert.AreEqual(def.Id, fresh.Id);
                Assert.AreEqual(def.DisplayName, fresh.DisplayName);
                Assert.AreEqual(def.SceneName, fresh.SceneName);
                Assert.AreEqual(def.Description, fresh.Description);
                Assert.AreEqual(def.WorldCenter, fresh.WorldCenter);
                Assert.AreEqual(def.WorldSizeMeters, fresh.WorldSizeMeters);
                Assert.AreEqual(def.SeabedPixelsPerMetre, fresh.SeabedPixelsPerMetre, 1e-6f);
                Assert.AreEqual(def.HarbourDepthMeters, fresh.HarbourDepthMeters, 1e-6f);
                Assert.AreEqual(def.TideAmplitude, fresh.TideAmplitude, 1e-6f);
            }
            finally { Object.DestroyImmediate(fresh); }
        }

        [Test]
        public void TheNameIsMarkedAsOwed()
        {
            // The owner rules names (world-map-plan §7 Q4) and has not ruled this one. The point of the
            // assertion is that the placeholder ADMITS it is one: a display name that reads as final is
            // how a working name ships to a player.
            var def = AssetDatabase.LoadAssetAtPath<RegionDef>(
                WaterSceneTemplate.RegionAssetPathFor(WestWaterPlan.RegionAssetName));
            Assert.AreEqual(WestWaterPlan.PlaceholderDisplayName, def.DisplayName);
            StringAssert.Contains("working name", def.DisplayName,
                "the display name must say out loud that it is a placeholder");
            Assert.AreEqual("region.west_water", def.Id,
                "…while the id underneath is the thing that must never change (ADR 0009)");
        }

        // =============================================================================================
        //  the region is the size the sizing rule says, and holds what it is asked to hold
        // =============================================================================================

        [Test]
        public void TheExtentIsSizedByTimeToCrossInTheDory()
        {
            // scene-sizing §1.3: inshore water crosses in 3–5 minutes in its gating boat. The gating boat
            // here is the DORY (rowed, 3.00 m/s — §1.2), which is the whole reason this region exists.
            const float doryMetresPerSecond = 3.0f;
            float longAxis = WestWaterPlan.RegionWorldSize.x / doryMetresPerSecond;

            Assert.That(longAxis, Is.InRange(180f, 300f),
                $"the long axis crosses in {longAxis:0} s — outside scene-sizing §1.3's 3–5 min inshore band");

            // ⭐ And it must be no SHORTER than the walk it replaces, or the dory becomes strictly better
            // than reading the tide and the crossing stops being a decision.
            float sailSeconds = (WestWaterPlan.ToStPetersPassagePos.x - WestWaterPlan.FromNineMileCreekArrivalPos.x)
                                / doryMetresPerSecond;
            float walkSeconds = NineMileCreekMainland.CrossingTotalMetres / 3.0f;   // PlayerWalkController's walk
            Assert.That(sailSeconds, Is.GreaterThan(walkSeconds),
                $"the sail is {sailSeconds:0} s against a {walkSeconds:0} s walk over the bar. Going " +
                "round must not be quicker than waiting for the tide, or the bar is decoration");
        }

        [Test]
        public void EveryAuthoredPointIsInsideTheRegionsOwnRectangle()
        {
            Rect bounds = WestWaterPlan.Plan.WorldBounds;

            void Inside(Vector3 p, string what) =>
                Assert.IsTrue(bounds.Contains(new Vector2(p.x, p.y)),
                    $"{what} at {p} is outside the region rectangle {bounds}");

            Inside(WestWaterPlan.ToStPetersPassagePos, "the east door");
            Inside(WestWaterPlan.ToNineMileCreekPassagePos, "the west door");
            Inside(WestWaterPlan.FromStPetersArrivalPos, "the arrival from St Peters");
            Inside(WestWaterPlan.FromNineMileCreekArrivalPos, "the arrival from Nine Mile Creek");
            Inside(WestWaterPlan.DefaultArrivalPos, "the default arrival");
        }

        [Test]
        public void NothingInThisRegionEverBares_ItIsTheForgivingOne()
        {
            // "Sheltered, forgiving, the dory's water" (world-map-plan §2.2) is a claim about the bed,
            // and this is it stated as arithmetic: at the lowest water this region's tide can reach,
            // every square metre of it is still under water — and under enough of it that the deepest
            // hull the bay admits (the Cape Islander, 1.40 m) still floats.
            float springLow = WestWaterPlan.SpringLowWater;
            const float deepestBayDraught = 1.40f;

            float shallowest = float.NegativeInfinity;
            Vector2 shallowestAt = Vector2.zero;
            Rect bounds = WestWaterPlan.Plan.WorldBounds;
            for (int ix = 0; ix <= 76; ix++)
            {
                for (int iy = 0; iy <= 52; iy++)
                {
                    var p = new Vector2(bounds.xMin + bounds.width * ix / 76f,
                                        bounds.yMin + bounds.height * iy / 52f);
                    float e = WestWaterPlan.ElevationAt(p);
                    if (e > shallowest) { shallowest = e; shallowestAt = p; }
                }
            }

            Assert.That(springLow - shallowest, Is.GreaterThan(deepestBayDraught),
                $"the shallowest point is {shallowest:0.00} m at {shallowestAt}, leaving " +
                $"{springLow - shallowest:0.00} m at spring low — the first sail must not be able to " +
                "ground. Hazards are the mid-bay's (Governors Island), not this region's");
        }

        // =============================================================================================
        //  the doors
        // =============================================================================================

        [Test]
        public void EachDoorSpansTheFullHeightOfTheRegion()
        {
            // In open water the region's EDGE is the door: nothing steers a hull toward a band the way a
            // bar or a channel does inshore, so a band you can hold forty metres north of is a crossing
            // that silently does not fire.
            Assert.AreEqual(WestWaterPlan.RegionWorldSize.y, WestWaterPlan.Plan.EdgeBandSize.y, 1e-4f,
                "a water region's passage band is as tall as the region");
            Assert.That(WestWaterPlan.Plan.EdgeBandSize.x, Is.GreaterThanOrEqualTo(6f),
                "…and deep enough that a hull cannot pass through it between two physics steps");

            // A full-height band still has to FIT: inset far enough that its near face is inside the
            // rectangle the camera is clamped to, or a boat can reach the door only by leaving the view.
            AssertBandInside(WestWaterPlan.ToStPetersPassagePos, WestWaterPlan.Plan.EdgeBandSize,
                             WestWaterPlan.RegionWorldCenter, WestWaterPlan.RegionWorldSize, "the east door");
            AssertBandInside(WestWaterPlan.ToNineMileCreekPassagePos, WestWaterPlan.Plan.EdgeBandSize,
                             WestWaterPlan.RegionWorldCenter, WestWaterPlan.RegionWorldSize, "the west door");
        }

        [Test]
        public void AnArrivalStandsClearOfTheDoorItCameThrough()
        {
            float bandHalfDepth = WestWaterPlan.Plan.EdgeBandSize.x * 0.5f;

            float east = Mathf.Abs(WestWaterPlan.FromStPetersArrivalPos.x - WestWaterPlan.ToStPetersPassagePos.x);
            float west = Mathf.Abs(WestWaterPlan.FromNineMileCreekArrivalPos.x - WestWaterPlan.ToNineMileCreekPassagePos.x);

            Assert.That(east, Is.GreaterThan(bandHalfDepth),
                "a fisher who has just arrived must not be standing inside the band they arrived by");
            Assert.That(west, Is.GreaterThan(bandHalfDepth));

            // …and each lands on the side of its door that faces INTO the region, not out of it.
            Assert.That(WestWaterPlan.FromStPetersArrivalPos.x, Is.LessThan(WestWaterPlan.ToStPetersPassagePos.x),
                "arriving from the east puts you west of the east door, with the run ahead of you");
            Assert.That(WestWaterPlan.FromNineMileCreekArrivalPos.x, Is.GreaterThan(WestWaterPlan.ToNineMileCreekPassagePos.x),
                "arriving from the west puts you east of the west door");
        }

        [Test]
        public void TheNeighboursSeaDoorsSitInOpenWaterClearOfTheirHazards()
        {
            // world-map-plan §4.4: every seam in open water clear of hazards — never on a landfall or in
            // a channel the player is threading. Each neighbour's sea door has to answer for itself.
            Assert.AreEqual(StPetersBuilder.DeepHarbourElevation,
                            _island.ElevationAt(XY(StPetersBuilder.ToWestWaterPassagePos)), 1e-4f,
                "St Peters' sea door stands on the deep floor — not on the bar, not on the reef apron");
            Assert.AreEqual(NineMileCreekMainland.BayFloorElevation,
                            _mainland.ElevationAt(XY(NineMileCreekMainland.ToWestWaterPassagePos)), 1e-4f,
                "the mainland's sea door stands on the open bay floor — not on the harbour shoal, not " +
                "on the bar");

            // ⭐ AND THE TWO DOORS ON EACH SHARED EDGE MUST NOT OVERLAP. Both neighbours now carry two
            // passages on the same side of the region — the tidal crossing and the sea door — and a
            // trigger band that reached the other's would make the crossing fire the wrong region.
            AssertBandsClear(
                StPetersBuilder.ToNineMileCreekPassagePos.y, StPetersBuilder.SandbarHalfWidth * 2f,
                StPetersBuilder.ToWestWaterPassagePos.y, StPetersBuilder.WestWaterPassageBandSize.y,
                "St Peters' bar passage and its sea door");
            AssertBandsClear(
                NineMileCreekBuilder.ToStPetersPassagePos.y, NineMileCreekBuilder.PassageBandSize.y,
                NineMileCreekMainland.ToWestWaterPassagePos.y, NineMileCreekMainland.WestWaterPassageBandSize.y,
                "the mainland's bar passage and its sea door");

            // …and each door's whole BAND is inside its own region, not half-hanging off the edge where
            // the camera's bounds clamp cannot follow a boat into it.
            AssertBandInside(StPetersBuilder.ToWestWaterPassagePos, StPetersBuilder.WestWaterPassageBandSize,
                             StPetersBuilder.RegionWorldCenter, StPetersBuilder.RegionWorldSize,
                             "St Peters' sea door");
            AssertBandInside(NineMileCreekMainland.ToWestWaterPassagePos, NineMileCreekMainland.WestWaterPassageBandSize,
                             NineMileCreekMainland.RegionWorldCenter, NineMileCreekMainland.RegionWorldSize,
                             "the mainland's sea door");
        }

        private static void AssertBandInside(Vector3 centre, Vector2 band, Vector2 regionCentre,
                                             Vector2 regionSize, string what)
        {
            Assert.That(Mathf.Abs(centre.x - regionCentre.x) + band.x * 0.5f,
                        Is.LessThanOrEqualTo(regionSize.x * 0.5f), $"{what} overhangs the region in X");
            Assert.That(Mathf.Abs(centre.y - regionCentre.y) + band.y * 0.5f,
                        Is.LessThanOrEqualTo(regionSize.y * 0.5f), $"{what} overhangs the region in Y");
        }

        private static void AssertBandsClear(float aCentreY, float aHeight, float bCentreY, float bHeight,
                                             string what)
        {
            float gap = Mathf.Abs(aCentreY - bCentreY) - (aHeight + bHeight) * 0.5f;
            Assert.That(gap, Is.GreaterThan(0f),
                $"{what} overlap by {-gap:0.0} m — a boat in the overlap would take whichever passage " +
                "the physics step happened to raise first");
        }

        // =============================================================================================
        //  determinism — the same build twice is the same world twice
        // =============================================================================================

        [Test]
        public void TheBedIsDeterministic_TwoBuildsAgreeToTheBit()
        {
            // CLAUDE.md rule 5: the simulation is deterministic and carries no hidden global randomness.
            // A region's bed is the part of a scene most able to break that quietly — a jittered scatter
            // or a time-seeded falloff would still LOOK right and would put the seabed somewhere else on
            // the owner's machine than on the test machine. Two independently configured terrains,
            // sampled across the whole rectangle, must agree EXACTLY — not within a tolerance.
            var secondGo = new GameObject("WestWater_SecondBuild");
            try
            {
                var second = secondGo.AddComponent<RectTidalTerrain>();
                WestWaterPlan.Plan.ConfigureTerrain(second);

                Rect bounds = WestWaterPlan.Plan.WorldBounds;
                for (int ix = 0; ix <= 40; ix++)
                {
                    for (int iy = 0; iy <= 28; iy++)
                    {
                        var p = new Vector2(bounds.xMin + bounds.width * ix / 40f,
                                            bounds.yMin + bounds.height * iy / 28f);
                        Assert.AreEqual(_westWater.ElevationAt(p), second.ElevationAt(p),
                            $"two builds of the same plan disagree about the seabed at {p}");
                        Assert.AreEqual(_westWater.ElevationAt(p), WestWaterPlan.ElevationAt(p),
                            $"the plan and the component it configures disagree at {p} — a test that " +
                            "reads the plan would then be asserting a bed the scene does not have");
                    }
                }
            }
            finally { Object.DestroyImmediate(secondGo); }
        }

        [Test]
        public void ThePlanIsStableAcrossReads()
        {
            // The plan's derived members are properties, so each read re-evaluates. That is the right
            // shape (it dodges static-initialisation order, which has already bitten this codebase) but
            // it means "the same number every time" is a claim worth making rather than assuming.
            var a = WestWaterPlan.Plan;
            var b = WestWaterPlan.Plan;

            Assert.AreEqual(a.WorldSize, b.WorldSize);
            Assert.AreEqual(a.HeightMin, b.HeightMin, 0f);
            Assert.AreEqual(a.HeightMax, b.HeightMax, 0f);
            Assert.AreEqual(a.HeightBakeResolution, b.HeightBakeResolution);
            Assert.AreEqual(a.Zones.Length, b.Zones.Length);
            for (int i = 0; i < a.Zones.Length; i++)
            {
                Assert.AreEqual(a.Zones[i].Center, b.Zones[i].Center, $"zone {i} centre");
                Assert.AreEqual(a.Zones[i].HalfSize, b.Zones[i].HalfSize, $"zone {i} half-size");
                Assert.AreEqual(a.Zones[i].Elevation, b.Zones[i].Elevation, 0f, $"zone {i} elevation");
                Assert.AreEqual(a.Zones[i].Falloff, b.Zones[i].Falloff, 0f, $"zone {i} falloff");
            }

            // The doors and the arrivals are properties too, and a door that moved between two reads
            // would put the seam somewhere the seam tests never looked.
            Vector3 eastDoorFirst = WestWaterPlan.ToStPetersPassagePos;
            Vector3 westDoorFirst = WestWaterPlan.ToNineMileCreekPassagePos;
            Vector3 eastArrivalFirst = WestWaterPlan.FromStPetersArrivalPos;
            Vector3 westArrivalFirst = WestWaterPlan.FromNineMileCreekArrivalPos;

            Assert.AreEqual(eastDoorFirst, WestWaterPlan.ToStPetersPassagePos);
            Assert.AreEqual(westDoorFirst, WestWaterPlan.ToNineMileCreekPassagePos);
            Assert.AreEqual(eastArrivalFirst, WestWaterPlan.FromStPetersArrivalPos);
            Assert.AreEqual(westArrivalFirst, WestWaterPlan.FromNineMileCreekArrivalPos);
        }

        // =============================================================================================
        //  the height bake has to be able to SEE this bed
        // =============================================================================================

        [Test]
        public void TheBakeRangeBracketsTheWholeBed()
        {
            // The water shader bakes elevation into an R8 channel across [HeightMin, HeightMax]. The
            // component's own -4…6 default would flatten an offshore bed entirely — every sample below
            // -4 clamping to zero — so the range is derived from the field rather than left alone.
            var plan = WestWaterPlan.Plan;
            Assert.AreEqual(WestWaterPlan.BayFloorElevation, plan.HeightMin, 1e-4f,
                "the bake's floor is the region's deepest ground");
            Assert.AreEqual(WestWaterPlan.LeeFloorElevation, plan.HeightMax, 1e-4f,
                "…and its ceiling is the shallowest");
            Assert.That(plan.HeightMax, Is.GreaterThan(plan.HeightMin),
                "a zero-width bake range would quantise the whole bed to one value");

            Rect bounds = plan.WorldBounds;
            for (int ix = 0; ix <= 20; ix++)
            {
                for (int iy = 0; iy <= 14; iy++)
                {
                    var p = new Vector2(bounds.xMin + bounds.width * ix / 20f,
                                        bounds.yMin + bounds.height * iy / 14f);
                    float e = plan.ElevationAt(p);
                    Assert.That(e, Is.InRange(plan.HeightMin, plan.HeightMax),
                        $"the bed at {p} is {e:0.00} m, outside the range the bake maps — it would clip");
                }
            }
        }

        [Test]
        public void TheBakeGridIsDerivedFromTheExtent_NotTyped()
        {
            var plan = WestWaterPlan.Plan;
            int expected = Mathf.Clamp(
                Mathf.CeilToInt(Mathf.Max(plan.WorldSize.x, plan.WorldSize.y) * plan.SeabedPixelsPerMetre),
                64, RegionDef.MaxSeabedTexels);
            Assert.AreEqual(expected, plan.HeightBakeResolution);
            Assert.AreEqual(1f, WestWaterPlan.SeabedPixelsPerMetre, 1e-6f,
                "1 px/m is the ruled OFFSHORE figure (scene-sizing §6.1) — this region paints no seabed, " +
                "so this is its height-data resolution and there is nothing fine out here to resolve");
        }
    }
}
