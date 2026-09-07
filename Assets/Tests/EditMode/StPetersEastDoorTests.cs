using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>ST PETERS' EAST DOOR</b> — the seam the game will OPEN through. The owner ruled the intro
    /// out in the water east of the island (2026-09-06) and ruled that water a real place rather than a
    /// set: <i>"a fully functional and returnable scene to the east that lines up with St Peters' east
    /// wall"</i>. This file holds the arithmetic of that door and of the region on the other side of it.
    ///
    /// <para><b>What is deliberately NOT asserted here.</b> That the door is actually IN the committed
    /// <c>StPeters.unity</c> — that is a claim about what Unity LOADS out of a scene, and it lives in
    /// <c>StPetersEastDoorPlayTests</c>, which loads the region and reads the components. A builder
    /// constant and a committed scene are two different statements (the <c>scene-wired is not
    /// builder-wired</c> lesson, #323), and this PR has to make both.</para>
    ///
    /// <para><b>The load-bearing test is the SEAM.</b> Everything else here would be caught eventually
    /// by somebody sailing into it; a seabed that steps across the load would not be, because it is a
    /// metre of bottom appearing under the hull at the exact instant the player is reading the sounder.
    /// The west water learned that at two seams. This door has one.</para>
    /// </summary>
    public class StPetersEastDoorTests
    {
        private GameObject _islandGo;
        private GameObject _eastWaterGo;
        private TidalTerrain _island;
        private RectTidalTerrain _eastWater;

        [SetUp]
        public void SetUp()
        {
            // Both sides of the seam stood up through their OWN configure calls — "the two sides agree"
            // is a claim about two terrains and cannot be made by reading one of them.
            _islandGo = new GameObject("StPeters_EastDoorTest");
            _island = _islandGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_island);

            _eastWaterGo = new GameObject("EastWater_SeamReference");
            _eastWater = _eastWaterGo.AddComponent<RectTidalTerrain>();
            EastWaterPlan.Plan.ConfigureTerrain(_eastWater);
        }

        [TearDown]
        public void TearDown()
        {
            if (_islandGo != null) Object.DestroyImmediate(_islandGo);
            if (_eastWaterGo != null) Object.DestroyImmediate(_eastWaterGo);
            GameServices.Reset();
        }

        private static Vector2 XY(Vector3 v) => new Vector2(v.x, v.y);

        // =============================================================================================
        //  ⭐ THE SEAM — the load-bearing one
        // =============================================================================================

        [Test]
        public void TheSeamDoesNotStep_BothSidesReportOneFloor()
        {
            // Sail across a seam whose two sides disagree and the depth sounder steps. The east water
            // answers this by being flat at the island's own floor rather than shoaling to meet it, so
            // the two numbers are not merely close — they are the same number, reached through
            // EastWaterPlan.BayFloorElevation => StPetersBuilder.DeepHarbourElevation.
            float islandSide = _island.ElevationAt(XY(StPetersBuilder.ToEastWaterPassagePos));
            float waterSide = _eastWater.ElevationAt(XY(EastWaterPlan.ToStPetersPassagePos));

            Assert.AreEqual(StPetersBuilder.DeepHarbourElevation, islandSide, 1e-4f,
                "St Peters' east door stands on her own deep-harbour floor");
            Assert.AreEqual(islandSide, waterSide, 1e-4f,
                "…and the east water reports the SAME bottom at its own door, so nothing steps under " +
                "the hull as the region loads");

            // The arrival either side of the seam is on the same floor too: a boat is STOPPED on
            // arrival and the first thing the sounder does is read where she is lying.
            Assert.AreEqual(islandSide, _island.ElevationAt(XY(StPetersBuilder.FromEastWaterArrivalPos)),
                            1e-4f, "the island's east arrival lies on the same floor as its door");
            Assert.AreEqual(waterSide, _eastWater.ElevationAt(XY(EastWaterPlan.FromStPetersArrivalPos)),
                            1e-4f, "and so does the east water's");
        }

        // =============================================================================================
        //  the door stands where the harbour's traffic actually crosses the wall
        // =============================================================================================

        [Test]
        public void TheEntranceChannelPublishesSeawardFirst_WhichIsWhatTheDoorReadsOffIt()
        {
            // ⚠ NOT a mirror of the derivation — it is the INVARIANT the derivation leans on. The door
            // takes its latitude from Waypoints[0] because the channel documents itself as SEAWARD →
            // HARBOUR ("reverse this list and every buoy on it changes hands"). Reverse it and the door
            // would quietly relocate to the dock's latitude, inside the harbour, with nothing failing.
            NavChannel entrance = StPetersNavMarks.Entrance;
            Assert.That(entrance.Waypoints.Length, Is.GreaterThanOrEqualTo(2),
                "a channel with fewer than two marks has no direction to publish");

            Vector2 first = entrance.Waypoints[0];
            Vector2 last = entrance.Waypoints[entrance.Waypoints.Length - 1];

            for (int i = 1; i < entrance.Waypoints.Length; i++)
                Assert.That(first.x, Is.GreaterThan(entrance.Waypoints[i].x),
                    $"waypoint 0 must be the seaward-most mark; waypoint {i} lies further east");

            Assert.AreEqual(StPetersBuilder.DockZonePos.x, last.x, 1e-3f,
                "…and the LAST mark is the dock, which is the other end of the same statement");
            Assert.AreEqual(first, StPetersBuilder.EntranceLandfall,
                "the landfall the door reads is that seaward mark");
        }

        [Test]
        public void BothOfTheIslandsSeaDoorsStandAtOneInset()
        {
            // The east door DERIVES its distance from the wall; the west door is a hand-typed −356f from
            // before the rule was written down. This is the test that stops those two drifting apart —
            // and it fails with the number to use if the region is ever resized.
            float half = StPetersBuilder.RegionWorldSize.x * 0.5f;
            float westInset = half - Mathf.Abs(StPetersBuilder.ToWestWaterPassagePos.x -
                                               StPetersBuilder.RegionWorldCenter.x);
            float eastInset = half - Mathf.Abs(StPetersBuilder.ToEastWaterPassagePos.x -
                                               StPetersBuilder.RegionWorldCenter.x);

            Assert.AreEqual(StPetersBuilder.SeaDoorInsetMetres, eastInset, 1e-4f,
                "the east door stands at the ruled inset inside the east wall");
            Assert.AreEqual(eastInset, westInset, 1e-4f,
                "…and the west door, typed by hand as -356 before the rule existed, stands at the same " +
                "one. If this fails the region has been resized and the literal was left behind");
            Assert.AreEqual(WestWaterPlan.DoorInsetMetres, StPetersBuilder.SeaDoorInsetMetres, 1e-4f,
                "and the inset is the bay's one figure, not this region's opinion");
        }

        [Test]
        public void SailOutOnTheChannelsOwnHeadingAndYouCrossTheDoor()
        {
            // ⭐ THE REAL CLAIM the fairway siting makes, and it is not "the numbers match". The marked
            // route STOPS at the landfall; a boat leaving carries on seaward on the heading the last leg
            // put her on, and the question is whether that heading takes her through a band 16 m further
            // out or past the end of it. A band you can miss on the harbour's own outbound track is a
            // crossing that silently does not fire — the worst failure a seam has.
            NavChannel entrance = StPetersNavMarks.Entrance;
            Vector2 landfall = entrance.Waypoints[0];
            Vector2 turn = entrance.Waypoints[1];
            Vector2 outbound = (landfall - turn).normalized;

            Assert.That(outbound.x, Is.GreaterThan(0.1f),
                "the outbound leg must actually head seaward for this question to mean anything");

            float run = StPetersBuilder.ToEastWaterPassagePos.x - landfall.x;
            Assert.That(run, Is.GreaterThan(0f), "the door lies seaward of the landfall");

            Vector2 crossing = landfall + outbound * (run / outbound.x);
            float offCentre = Mathf.Abs(crossing.y - StPetersBuilder.ToEastWaterPassagePos.y);

            Assert.That(offCentre, Is.LessThan(StPetersBuilder.EastWaterPassageBandSize.y * 0.5f),
                $"holding the channel's outbound heading crosses the wall {offCentre:0.0} m off the " +
                "door's centre — outside the band, so the crossing would not fire");
        }

        // =============================================================================================
        //  the band and the arrival are in water, in the region, and out of everyone's way
        // =============================================================================================

        [Test]
        public void TheDoorAndItsArrivalStandInOpenWater_NotOnTheReefAndNotOnGroundThatBares()
        {
            // world-map-plan §4.4: every seam in open water clear of hazards. Sampled at the band's four
            // corners rather than at its centre — a band is a rectangle, and a hazard that only clipped
            // one end of it would pass a centre-point check.
            Vector3 c = StPetersBuilder.ToEastWaterPassagePos;
            Vector2 h = StPetersBuilder.EastWaterPassageBandSize * 0.5f;

            foreach (Vector2 corner in new[]
                     {
                         new Vector2(c.x - h.x, c.y - h.y), new Vector2(c.x + h.x, c.y - h.y),
                         new Vector2(c.x - h.x, c.y + h.y), new Vector2(c.x + h.x, c.y + h.y),
                     })
                Assert.AreEqual(StPetersBuilder.DeepHarbourElevation, _island.ElevationAt(corner), 1e-4f,
                    $"the band's corner at ({corner.x:0}, {corner.y:0}) is off the deep floor");

            // …and nothing under the band bares at the lowest water the region ever has.
            float springLow = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;
            Assert.That(_island.ElevationAt(XY(c)), Is.LessThan(springLow),
                "the door must be afloat at spring low — a seam on ground that bares is a seam that " +
                "stops existing twice a day");
        }

        [Test]
        public void TheArrivalFloatsTheHullThatComesHomeOnIt_AtTheLowestWaterTheRegionHas()
        {
            // She is a 1.40 m cape islander and she arrives STOPPED, so the water under her at the
            // arrival is not a detail — it is whether the opening ends with the boat aground. The
            // required margin is the dredge's own: draught plus the ruled under-keel clearance.
            float springLow = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;
            float bed = _island.ElevationAt(XY(StPetersBuilder.FromEastWaterArrivalPos));
            float depth = springLow - bed;
            float required = StPetersBuilder.ArrivalHullDraughtMetres +
                             StPetersBuilder.BerthUnderKeelClearance;

            Assert.That(depth, Is.GreaterThanOrEqualTo(required - 1e-4f),
                $"the east arrival holds {depth:0.00} m at spring low and the hull that lands there " +
                $"needs {required:0.00} m");

            // ⚠ It lands EXACTLY on the margin, and that is not luck: the open bay's floor IS
            // TideMean − TideAmplitude − (draught + clearance) by construction (ApproachBedElevation).
            // The assertion above is the one that matters; this one says out loud that there is no
            // slack here, so a deeper hull cannot be sailed in on this door without dredging first.
            Assert.AreEqual(required, depth, 1e-3f,
                "the open bay gives this hull her margin and not one centimetre more");
        }

        [Test]
        public void TheArrivalStandsClearOfTheBandItCameThrough_OnTheHarbourSideOfIt()
        {
            float standoff = Mathf.Abs(StPetersBuilder.FromEastWaterArrivalPos.x -
                                       StPetersBuilder.ToEastWaterPassagePos.x);

            Assert.That(standoff, Is.GreaterThan(StPetersBuilder.EastWaterPassageBandSize.x * 0.5f),
                "a boat that has just arrived must not be sitting inside the band that put her there");
            Assert.That(StPetersBuilder.FromEastWaterArrivalPos.x,
                        Is.LessThan(StPetersBuilder.ToEastWaterPassagePos.x),
                "arriving from the east puts you WEST of the east door, with the harbour ahead of you");
            Assert.AreEqual(EastWaterPlan.ArrivalClearanceMetres, standoff, 1e-4f,
                "and it stands off by the one figure both sides of this door use");
        }

        [Test]
        public void TheArrivalLiesInsideTheEntranceChannelsMarkedFairway()
        {
            // ⭐ The point of siting the door on the fairway: you do not land NEAR the route home, you
            // land ON it. The arrival is inboard of the landfall — you have already made your landfall
            // by the time the region finishes loading — so the run in starts at the first mark you have
            // not yet passed, and it starts inside the marked water.
            NavChannel entrance = StPetersNavMarks.Entrance;
            Vector2 landfall = entrance.Waypoints[0];
            Vector2 turn = entrance.Waypoints[1];
            Vector2 arrival = XY(StPetersBuilder.FromEastWaterArrivalPos);

            Assert.That(arrival.x, Is.LessThan(landfall.x),
                "the arrival is INBOARD of the landfall — the outermost mark is already astern");

            Vector2 leg = turn - landfall;
            float off = Mathf.Abs(leg.x * (landfall.y - arrival.y) - leg.y * (landfall.x - arrival.x))
                        / leg.magnitude;

            Assert.That(off, Is.LessThanOrEqualTo(entrance.HalfWidthMetres),
                $"the arrival lies {off:0.0} m off the fairway's centreline, outside the marked " +
                $"{entrance.HalfWidthMetres:0} m — a boat put down there starts her run in from " +
                "outside her own channel");
        }

        [Test]
        public void TheBandIsWhollyInsideTheRegion()
        {
            // A band half-hanging off the edge is a door the camera's bounds clamp cannot follow a boat
            // into: she reaches it only by leaving the view.
            Vector3 c = StPetersBuilder.ToEastWaterPassagePos;
            Vector2 b = StPetersBuilder.EastWaterPassageBandSize;

            Assert.That(Mathf.Abs(c.x - StPetersBuilder.RegionWorldCenter.x) + b.x * 0.5f,
                        Is.LessThanOrEqualTo(StPetersBuilder.RegionWorldSize.x * 0.5f),
                        "the east door overhangs the region in X");
            Assert.That(Mathf.Abs(c.y - StPetersBuilder.RegionWorldCenter.y) + b.y * 0.5f,
                        Is.LessThanOrEqualTo(StPetersBuilder.RegionWorldSize.y * 0.5f),
                        "the east door overhangs the region in Y");
        }

        [Test]
        public void TheIslandsThreePassagesDoNotOverlap()
        {
            // Three ways off one island now: the bar on foot, the west sea door, the east sea door. A
            // boat standing in two bands takes whichever passage the physics step happened to raise
            // first, which is a crossing to the wrong region with nothing failing.
            var bar = (pos: StPetersBuilder.ToNineMileCreekPassagePos,
                       size: new Vector2(3f, StPetersBuilder.SandbarHalfWidth * 2f), name: "the bar walk");
            var west = (pos: StPetersBuilder.ToWestWaterPassagePos,
                        size: StPetersBuilder.WestWaterPassageBandSize, name: "the west sea door");
            var east = (pos: StPetersBuilder.ToEastWaterPassagePos,
                        size: StPetersBuilder.EastWaterPassageBandSize, name: "the east sea door");

            AssertBandsClear(bar.pos, bar.size, west.pos, west.size, bar.name, west.name);
            AssertBandsClear(bar.pos, bar.size, east.pos, east.size, bar.name, east.name);
            AssertBandsClear(west.pos, west.size, east.pos, east.size, west.name, east.name);
        }

        [Test]
        public void TheEastDoorIsClearOfTheWharfAndTheBerthAlongsideIt()
        {
            // The wharf, the dredged pocket and #751's berth line all live at the island's east end, and
            // the door is out beyond them. A band that reached the berth would fire the crossing on a
            // boat coming alongside.
            float bandNearFace = StPetersBuilder.ToEastWaterPassagePos.x -
                                 StPetersBuilder.EastWaterPassageBandSize.x * 0.5f;

            Assert.That(bandNearFace, Is.GreaterThan(StPetersBuilder.DockZonePos.x + 50f),
                "the east door must stand well seaward of the berth alongside the wharf");
            Assert.That(StPetersBuilder.FromEastWaterArrivalPos.x,
                        Is.GreaterThan(StPetersBuilder.DockZonePos.x + 50f),
                "…and so must the arrival, or a boat would land on top of the fleet");
        }

        // =============================================================================================
        //  the destination — the committed def, and the door on ITS side
        // =============================================================================================

        [Test]
        public void TheCommittedEastWaterDefSaysWhatThePlanSays()
        {
            // ⭐ THE #462 LESSON, and the one that actually matters here: the door's target is resolved
            // by PATH out of the committed asset, so if the asset and the plan ever disagree the door
            // sends boats to a region nobody authored. Read through AssetDatabase — what Unity LOADS,
            // not what the YAML reads (the blank-line lesson: a file-vs-file check is checking typing).
            var def = AssetDatabase.LoadAssetAtPath<RegionDef>(
                WaterSceneTemplate.RegionAssetPathFor(EastWaterPlan.RegionAssetName));
            Assert.IsNotNull(def,
                "the east water's RegionDef is COMMITTED, not minted on first build — St Peters' " +
                "builder looks it up by path to hang her east door on, and a door with no target is " +
                "an island the opening cannot sail into");

            WaterScenePlan plan = EastWaterPlan.Plan;

            Assert.AreEqual(EastWaterPlan.RegionId, def.Id, "ids are append-only and stable (ADR 0009)");
            Assert.AreEqual(EastWaterPlan.SceneName, def.SceneName);
            Assert.AreEqual(plan.DisplayName, def.DisplayName);
            Assert.AreEqual(plan.Description, def.Description);
            Assert.AreEqual(plan.WorldCenter, def.WorldCenter);
            Assert.AreEqual(plan.WorldSize, def.WorldSizeMeters);
            Assert.AreEqual(plan.SeabedPixelsPerMetre, def.SeabedPixelsPerMetre, 1e-6f);
            Assert.AreEqual(plan.NominalDepthMetres, def.HarbourDepthMeters, 1e-6f);
            Assert.AreEqual(plan.TideMean, def.TideMeanLevel, 1e-6f);
            Assert.AreEqual(plan.TideAmplitude, def.TideAmplitude, 1e-6f);
            Assert.AreEqual(plan.TidePhaseHours, def.TidePhaseHours, 1e-6f);
            Assert.IsFalse(def.IsDeepHarbour, "open water is not a harbour");
            Assert.IsEmpty(def.SpawnFishIds,
                "RegionDef's spawn list is dead code — the school model is the live source, and the " +
                "intro's fish are authored THERE");

            // And the push the builder would make must produce the same thing, applied to a THROWAWAY
            // instance: a test that dirties a committed asset to prove a point has changed the project
            // in order to measure it.
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
                Assert.AreEqual(def.HarbourDepthMeters, fresh.HarbourDepthMeters, 1e-6f);
                Assert.AreEqual(def.TideAmplitude, fresh.TideAmplitude, 1e-6f);
            }
            finally { Object.DestroyImmediate(fresh); }
        }

        [Test]
        public void TheEastWatersNameIsMarkedAsOwed()
        {
            // The owner names the water scenes (world-map-plan §7 Q4) and has not named this one. The
            // point is that the placeholder ADMITS it: a display name that reads as final is how a
            // working name ships to a player.
            var def = AssetDatabase.LoadAssetAtPath<RegionDef>(
                WaterSceneTemplate.RegionAssetPathFor(EastWaterPlan.RegionAssetName));
            Assert.AreEqual(EastWaterPlan.PlaceholderDisplayName, def.DisplayName);
            StringAssert.Contains("working name", def.DisplayName,
                "the display name must say out loud that it is a placeholder");
            Assert.AreEqual("region.east_water", def.Id,
                "…while the id underneath is the thing that must never change (ADR 0009)");
        }

        [Test]
        public void TheEastWatersTideIsTheIslandsTide_BecauseTheyShareTerrain()
        {
            // world-map-plan §4.3: adjacent regions sharing terrain share a tide profile. Borrowed by
            // reference, so a retune of the island moves the water it touches with it — and this holds
            // the three numbers equal so the day somebody re-tunes one, the other is not left behind.
            Assert.AreEqual(StPetersBuilder.TideMean, EastWaterPlan.TideMean, 1e-6f);
            Assert.AreEqual(StPetersBuilder.TideAmplitude, EastWaterPlan.TideAmplitude, 1e-6f);
            Assert.AreEqual(StPetersBuilder.TidePhaseHours, EastWaterPlan.TidePhaseHours, 1e-6f);
        }

        [Test]
        public void TheEastWatersOwnDoorObeysTheSameTwoRules()
        {
            // The far side of the seam is a WATER region, so its door spans the full height of the
            // region rather than the land region's 120 m band — but the two rules that make a door
            // usable are the same either side: the band fits inside its own region, and the arrival
            // stands clear of it on the region side.
            Vector3 door = EastWaterPlan.ToStPetersPassagePos;
            Vector2 band = EastWaterPlan.Plan.EdgeBandSize;

            Assert.AreEqual(EastWaterPlan.RegionWorldSize.y, band.y, 1e-4f,
                "in open water the region's EDGE is the door — nothing out there steers a hull toward " +
                "a band, so it spans the whole wall");
            Assert.That(Mathf.Abs(door.x - EastWaterPlan.RegionWorldCenter.x) + band.x * 0.5f,
                        Is.LessThanOrEqualTo(EastWaterPlan.RegionWorldSize.x * 0.5f),
                        "the east water's door overhangs its own region in X");

            float standoff = Mathf.Abs(EastWaterPlan.FromStPetersArrivalPos.x - door.x);
            Assert.That(standoff, Is.GreaterThan(band.x * 0.5f),
                "a boat arriving from St Peters must not be standing in the band she arrived by");
            Assert.That(EastWaterPlan.FromStPetersArrivalPos.x, Is.GreaterThan(door.x),
                "arriving from the west puts you EAST of the west door, with the open water ahead");
        }

        [Test]
        public void TheOneDoorRegionHasChosenItsFallback()
        {
            // The east water has a single way in today and the mid-bay adds a second later. A fallback
            // chosen now is a fallback chosen while there is only one right answer; chosen later, it is
            // chosen by whoever first needs it.
            Assert.AreEqual(EastWaterPlan.FromStPetersArrivalPos, EastWaterPlan.DefaultArrivalPos,
                "the region's one door is also where a keyless passage lands");
            Assert.IsNotEmpty(EastWaterPlan.FromStPetersArrivalKey,
                "…and it is NAMED anyway, so the day a second door arrives it adds a name rather than " +
                "re-pointing this one");
            Assert.AreEqual("east_water", StPetersBuilder.FromEastWaterArrivalKey,
                "the key the island answers for a boat coming home the other way");
        }

        // =============================================================================================
        //  helpers
        // =============================================================================================

        private static void AssertBandsClear(Vector3 aPos, Vector2 aSize, Vector3 bPos, Vector2 bSize,
                                             string a, string b)
        {
            float gapX = Mathf.Abs(aPos.x - bPos.x) - (aSize.x + bSize.x) * 0.5f;
            float gapY = Mathf.Abs(aPos.y - bPos.y) - (aSize.y + bSize.y) * 0.5f;
            Assert.That(gapX > 0f || gapY > 0f,
                $"{a} and {b} overlap — a boat in the overlap takes whichever passage the physics step " +
                "happened to raise first, which is a crossing into the wrong region with nothing failing");
        }
    }
}
