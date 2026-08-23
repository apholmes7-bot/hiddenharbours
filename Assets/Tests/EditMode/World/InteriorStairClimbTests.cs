using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>The storey above stands a storey up, and the fisher walks there</b> — ADR 0036 as amended on
    /// 2026-08-23.
    ///
    /// <para>The defect these guard is the one that draws perfectly: before the amendment a second storey
    /// was placed at the building's own transform, so the bedroom landed pixel-for-pixel over the kitchen.
    /// Nothing threw, nothing sorted wrong, the colliders were all correct — going upstairs simply did not
    /// look like going anywhere. Every assertion below is therefore about a NUMBER nobody can see: how far
    /// up the storey is, that the walk between them ends exactly there and never doubles back, that a
    /// storey placed at the ground floor's own Y is caught, and that the lifted sheet sorts inside the
    /// band ADR 0032 defines.</para>
    ///
    /// <para>Pure arithmetic and two plain components — no baked room, no graphics device, no play loop —
    /// so the whole contract is provable headless in CI.</para>
    /// </summary>
    public class InteriorStairClimbTests
    {
        // =============================================================================
        //  the cottage's own numbers, restated so the assertions are anchored to
        //  something and not to themselves
        // =============================================================================

        /// <summary>sageCottage: 6.6 × 8.05 m of floor, and its storeys 3.1025 m apart. The last is the
        /// number this whole branch turns on — <c>interiorIsoRig</c> resolves
        /// <c>roomH = min(2.55 + size·0.85, wallH − 0.6) = 2.7625</c> at <c>size 0.25</c> and declares
        /// <c>storeyZ = roomH + 0.34</c> of joists, which the bake writes into the interiors contract as
        /// <c>storeyHeightMetres</c>. Restated here, NOT read from the contract: a test that asked the
        /// same file the code asks would agree with it however wrong both were.</summary>
        const float CottageWidth = 6.6f;
        const float CottageLength = 8.05f;
        const float CottageStoreyHeightMetres = 3.1025f;

        const float DepthScale = 0.6427876f;
        const float WallThickness = 0.3f;
        const float DoorwayWidth = 1.4f;

        static InteriorFootprint Cottage(Vector2 centre, int facing = 0) =>
            new InteriorFootprint(centre, CottageWidth, CottageLength, facing, 8, DepthScale);

        // =============================================================================
        //  how high a storey is
        // =============================================================================

        [Test]
        public void AStoreyDrawsAtItsDeclaredHeight_ProjectedAtTheSharedCamera_NotAtItsRawMetres()
        {
            float y = InteriorLevelLayout.UpperLevelY(CottageStoreyHeightMetres);

            // The oracle is the camera itself, written out longhand — not IsoGround.HeightScale, which is
            // the thing under test's own input. cos 40° ≈ 0.766.
            float expected = CottageStoreyHeightMetres *
                             Mathf.Cos(40f * Mathf.Deg2Rad);
            Assert.AreEqual(expected, y, 1e-5f,
                "a storey HEIGHT draws cos(elevation) up the screen — 3.1025 m of cottage is ~2.38 units");

            Assert.Greater(Mathf.Abs(y - CottageStoreyHeightMetres), 0.5f,
                "using the raw metres would stand the bedroom 31% too high");
            Assert.Greater(Mathf.Abs(y - CottageStoreyHeightMetres * DepthScale), 0.3f,
                "and running it through the GROUND squash — the wrong half of the same camera — 19% low");
        }

        [Test]
        public void AContractThatDeclaresNoStoreyHeightLiftsNothing_RatherThanGuessingOne()
        {
            Assert.AreEqual(0f, InteriorLevelLayout.UpperLevelY(0f), 1e-6f,
                "a bake older than the storeyHeightMetres field reports 0, and 0 must stay 0 here — the " +
                "complaint belongs to whoever places the storey, not to a fallback invented in the maths");
            Assert.AreEqual(0f, InteriorLevelLayout.UpperLevelY(-2f), 1e-6f, "a negative storey is not one");
            Assert.AreEqual(0f, InteriorLevelLayout.UpperLevelY(float.NaN), 1e-6f, "nor is a NaN");
        }

        [Test]
        public void TheLevelOffsetIsPurelyVertical_BecauseAStoreyStandsOnTheSameFootprint()
        {
            Vector2 offset = InteriorLevelLayout.LevelOffset(InteriorLevelLayout.UpperLevel,
                                                             CottageStoreyHeightMetres);

            Assert.AreEqual(0f, offset.x, 1e-6f,
                "a storey above is the same rectangle, so nothing moves across the ground");
            Assert.AreEqual(InteriorLevelLayout.UpperLevelY(CottageStoreyHeightMetres), offset.y, 1e-6f);
            Assert.AreEqual(Vector2.zero, InteriorLevelLayout.LevelOffset(
                                InteriorLevelLayout.GroundLevel, CottageStoreyHeightMetres),
                "and the ground floor is where the building says it is");
        }

        // =============================================================================
        //  the climb
        // =============================================================================

        [Test]
        public void TheClimbEndsExactlyOneStoreyUp_FromWhereverOnTheStairwellItStarted()
        {
            float rise = InteriorLevelLayout.UpperLevelY(CottageStoreyHeightMetres);
            var foot = new Vector2(12.5f, -20.25f);           // a stairwell somewhere in the world
            var climb = new StairTraversal(foot, foot + new Vector2(0f, rise), 0.5f);

            Vector2 arrived = climb.Advance(0.5f);

            Assert.IsTrue(climb.IsDone);
            Assert.AreEqual(foot.x, arrived.x, 1e-5f,
                "the head of the stairs is the same footprint coordinate as the foot — that is what " +
                "makes the way back a press where you land, and not a search");
            Assert.AreEqual(foot.y + rise, arrived.y, 1e-5f,
                "and exactly one storey up: the walk ends ON the floor above, never near it");
            Assert.AreEqual(rise, climb.RiseWorldUnits, 1e-5f);
        }

        [Test]
        public void TheClimbNeverGoesBackwards_AndNeverOvershootsTheFloorAbove()
        {
            float rise = InteriorLevelLayout.UpperLevelY(CottageStoreyHeightMetres);
            var foot = Vector2.zero;
            var head = new Vector2(0f, rise);

            // Sampled straight off the path rather than by ticking, so this is about the CURVE and not
            // about anyone's frame rate.
            float previous = float.NegativeInfinity;
            for (int i = 0; i <= 40; i++)
            {
                float t = i / 40f;
                Vector2 p = StairTraversal.PositionAt(foot, head, t);

                Assert.GreaterOrEqual(p.y, previous,
                    $"the fisher must never sink back down the stairs (t = {t:0.###}). An ease that " +
                    "overshoots — a back- or elastic-ease is the usual temptation — puts a body through " +
                    "the bedroom ceiling and drops it back onto the floor");
                Assert.LessOrEqual(p.y, head.y + 1e-5f, "and never above the floor they are climbing to");
                Assert.GreaterOrEqual(p.y, foot.y - 1e-5f, "nor below the one they left");
                previous = p.y;
            }

            Assert.AreEqual(foot.y, StairTraversal.PositionAt(foot, head, 0f).y, 1e-5f);
            Assert.AreEqual(head.y, StairTraversal.PositionAt(foot, head, 1f).y, 1e-5f);
            Assert.AreEqual(head.y, StairTraversal.PositionAt(foot, head, 2f).y, 1e-5f,
                "and a caller that oversteps its own playhead lands on the storey, not past it");
        }

        [Test]
        public void TheClimbAdvancesMonotonically_WhateverTheFramesLookLike()
        {
            var climb = new StairTraversal(Vector2.zero, new Vector2(0f, 2.4f), 0.5f);

            float previous = 0f;
            for (int frame = 0; frame < 12; frame++)
            {
                // A deliberately ragged frame: a spike, a couple of ordinary ticks, then a zero.
                float dt = frame == 3 ? 0.21f : frame == 7 ? 0f : 0.04f;
                float y = climb.Advance(dt).y;
                Assert.GreaterOrEqual(y, previous, "no frame may move the fisher down the stairs");
                previous = y;
            }

            Assert.IsTrue(climb.IsDone, "and a second of ragged frames gets them there");
        }

        [Test]
        public void ABadDeltaCannotRewindTheClimb()
        {
            var climb = new StairTraversal(Vector2.zero, new Vector2(0f, 2.4f), 0.5f);
            climb.Advance(0.25f);
            float half = climb.Position.y;

            climb.Advance(-1f);
            Assert.AreEqual(half, climb.Position.y, 1e-6f, "a negative delta is ignored, not applied");
            climb.Advance(float.NaN);
            Assert.AreEqual(half, climb.Position.y, 1e-6f, "and so is a NaN one");
        }

        [Test]
        public void AZeroDurationClimbIsOverBeforeItBegins_WhichIsTheOwnersInstantSwap()
        {
            var head = new Vector2(3f, 2.4f);
            var climb = new StairTraversal(Vector2.zero, head, 0f);

            Assert.IsTrue(climb.IsDone, "GameConfig.StairClimbSeconds = 0 is a legal setting");
            Assert.AreEqual(head, climb.Position, "and it arrives, rather than dividing by zero");
        }

        [Test]
        public void TheGaitHoldContractIsZero_BecauseAClimbCoversNoGround()
        {
            // Not a tautology check: this is the number whoever plays a climb must state to
            // IsoCharacterSprite every frame, in Update. Left unstated, the walker MEASURES ~2.4 units of
            // rise in half a second, reads ~5 m/s and draws a flat sprint while covering no ground at all.
            Assert.AreEqual(0f, StairTraversal.GaitSpeedMetresPerSecond,
                "a rise is height, not travel — there is no honest stride to draw");
        }

        // =============================================================================
        //  the sabotage: a storey placed at the ground floor's own Y
        // =============================================================================

        [Test]
        public void AStoreyPlacedAtTheGroundFloorsOwnYIsCaught()
        {
            // THE DEFECT, spelled out: this is exactly what ADR 0036 shipped, and it is what every other
            // test in this file would still pass with if the guard were deleted.
            Assert.IsTrue(InteriorLevelLayout.DrawsOverGroundFloor(0f),
                "a storey at the ground floor's own Y draws over it — that is the 2026-08-23 defect");
            Assert.IsTrue(InteriorLevelLayout.DrawsOverGroundFloor(0.02f),
                "and so does one under a pixel up (PPU 32), which lands on the same texel row");
            Assert.IsTrue(InteriorLevelLayout.DrawsOverGroundFloor(float.NaN), "NaN is not a storey");
            Assert.IsTrue(InteriorLevelLayout.DrawsOverGroundFloor(-2.4f), "and neither is a cellar");

            Assert.IsFalse(
                InteriorLevelLayout.DrawsOverGroundFloor(
                    InteriorLevelLayout.UpperLevelY(CottageStoreyHeightMetres)),
                "while the cottage's real storey clears it by two and a third world units");
        }

        [Test]
        public void ARoomLiftedByAStoreySitsAboveTheOneBelow_AndTheirRECTANGLESSTILLOVERLAP()
        {
            var ground = Cottage(new Vector2(4f, -20f));
            var upper = Cottage(new Vector2(4f, -20f) +
                                InteriorLevelLayout.LevelOffset(InteriorLevelLayout.UpperLevel,
                                                                CottageStoreyHeightMetres));

            Assert.AreEqual(InteriorLevelLayout.UpperLevelY(CottageStoreyHeightMetres),
                            upper.Centre.y - ground.Centre.y, 1e-5f);
            Assert.AreEqual(ground.Centre.x, upper.Centre.x, 1e-6f, "and directly above it");

            // The two floors are still the same rectangle, which is the half of ADR 0036 that did not
            // change: one footprint, one threshold, one definition of being in this building.
            Assert.AreEqual(ground.WidthMetres, upper.WidthMetres, 1e-6f);
            Assert.AreEqual(ground.LengthMetres, upper.LengthMetres, 1e-6f);
            Assert.IsTrue(upper.Contains(upper.Centre, WallThickness));

            // ⚠️ AND THEY OVERLAP IN WORLD XY, BY DESIGN AND BY A HAIR. A storey lifts 2.377 units; the
            // cottage's own floor is 2.587 units deep on screen. So the middle of the kitchen is (just)
            // inside the bedroom's rectangle — 28 thousandths of a unit inside it, about one pixel at
            // PPU 32. That is not a bug and cannot be tuned away: the ¾ view draws depth and height on
            // the same screen axis, so any storey shorter than its own room is deep will overlap it.
            // It is the reason the inside test asks "am I on the storey I am ON" rather than "which
            // storey am I in" — the second question has no answer here.
            Assert.IsTrue(upper.Contains(ground.Centre, WallThickness),
                "if this ever goes false the rooms have stopped overlapping, which is fine — but the " +
                "comment above and ADR 0036's amendment both claim they do, and one of the three is wrong");

            // What IS unambiguous: the far end of one floor is nowhere near the other.
            Vector2 upperFarEnd = upper.ModelToWorld(new Vector2(0f, CottageLength * 0.5f - 0.5f));
            Assert.IsFalse(ground.Contains(upperFarEnd, WallThickness),
                "the back of the bedroom is well off the floor below it");
            Vector2 groundNearEnd = ground.ModelToWorld(new Vector2(0f, -CottageLength * 0.5f + 0.5f));
            Assert.IsFalse(upper.Contains(groundNearEnd, WallThickness),
                "and the front of the kitchen is well off the floor above it");
        }

        // =============================================================================
        //  sorting — ADR 0032's band
        // =============================================================================

        [Test]
        public void TheUpperStoreysSheetSortsInsideTheAdr0032Band()
        {
            var upper = Cottage(new Vector2(4f, -20f) +
                                InteriorLevelLayout.LevelOffset(InteriorLevelLayout.UpperLevel,
                                                                CottageStoreyHeightMetres));

            int order = InteriorLevelLayout.UpperRoomSortingOrder(upper);

            Assert.GreaterOrEqual(order, SortingBands.DecorFloor,
                "the lifted sheet is drawn over ground that has grass and a dooryard on it, every tuft " +
                "of it a four-digit Y-sorted order — under the band's floor the meadow behind the house " +
                "would draw straight through a first-floor bedroom");
            Assert.LessOrEqual(order, SortingBands.DecorCeiling,
                "and it must not saturate at the top of the band either (2402)");
            Assert.AreEqual(2, SortingBands.DecorFloor, "the band ADR 0032 fixed is 2…2402");
            Assert.AreEqual(2402, SortingBands.DecorCeiling);
        }

        [Test]
        public void NoOccupantOfTheUpperStoreyCanSortBehindTheFloorTheyStandOn()
        {
            var upper = Cottage(new Vector2(4f, -20f) +
                                InteriorLevelLayout.LevelOffset(InteriorLevelLayout.UpperLevel,
                                                                CottageStoreyHeightMetres));
            int sheet = InteriorLevelLayout.UpperRoomSortingOrder(upper);

            // Every corner, the centre, and the two bed positions the plan uses: a Y-sorted body standing
            // anywhere on that floor must outrank the sheet. This is the guarantee the ground floor gets
            // from its fixed order, kept while the upper sheet joins the band.
            foreach (Vector2 corner in upper.Corners())
                Assert.GreaterOrEqual(InteriorLevelLayout.SortingOrderFor(corner.y), sheet,
                    "an occupant at a corner of the room draws in front of its floor, never behind it");

            Assert.Greater(InteriorLevelLayout.SortingOrderFor(upper.Centre.y), sheet,
                "and one in the middle of it certainly does — the far edge is the sheet's own rank, so " +
                "everything standing on the floor is south of it");
        }

        [Test]
        public void TheBandMapsYTheWayAdr0032SaysItDoes()
        {
            // The mapping restated in World (rule 4: World cannot reference Art to call YSortSprite), so
            // it is checked against SortingBands' own constants rather than against a second set of
            // literals — which is exactly how the band rotted the first time.
            Assert.AreEqual(SortingBands.DecorBase, InteriorLevelLayout.SortingOrderFor(0f),
                "world Y 0 sits at the middle of the band");
            Assert.AreEqual(SortingBands.DecorFloor,
                            InteriorLevelLayout.SortingOrderFor(SortingBands.DecorHalfExtentMetres),
                "and +300 m lands exactly on its floor");
            Assert.AreEqual(SortingBands.DecorCeiling,
                            InteriorLevelLayout.SortingOrderFor(-SortingBands.DecorHalfExtentMetres),
                "and −300 m on its ceiling");
            Assert.Greater(InteriorLevelLayout.SortingOrderFor(-1f), InteriorLevelLayout.SortingOrderFor(1f),
                "lower Y draws in front");
            Assert.AreEqual(SortingBands.DecorFloor, InteriorLevelLayout.SortingOrderFor(10_000f),
                "and anything past either end clamps into the band rather than out of it");
            Assert.AreEqual(SortingBands.DecorCeiling, InteriorLevelLayout.SortingOrderFor(-10_000f));
        }

        // =============================================================================
        //  the component: the storey swaps now, the body walks
        // =============================================================================

        readonly System.Collections.Generic.List<Object> _spawned =
            new System.Collections.Generic.List<Object>();

        [TearDown]
        public void TearDown()
        {
            MoveActionClaim.Reset();
            GameServices.PlayerTransform = null;
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        /// <summary>A cottage with a storey above, standing at the origin, lifted the way the builder
        /// lifts one.</summary>
        BuildingInterior StandTwoStorey(out SpriteRenderer upper)
        {
            var buildingGo = new GameObject("cottage");
            _spawned.Add(buildingGo);

            var shell = new GameObject("Shell").AddComponent<SpriteRenderer>();
            var ground = new GameObject("Interior").AddComponent<SpriteRenderer>();
            upper = new GameObject("InteriorUpper").AddComponent<SpriteRenderer>();
            var groundProps = new GameObject("Furniture");
            var upperProps = new GameObject("FurnitureUpper");
            var upperWalls = new GameObject("WallsUpper");

            foreach (Transform t in new[] { shell.transform, ground.transform, upper.transform,
                                            groundProps.transform, upperProps.transform,
                                            upperWalls.transform })
                t.SetParent(buildingGo.transform, worldPositionStays: false);

            var interior = buildingGo.AddComponent<BuildingInterior>();
            interior.Configure(shell, ground, groundProps.transform, CottageWidth, CottageLength,
                               facing: 0, facings: 8, groundDepthScale: DepthScale,
                               wallThicknessMetres: WallThickness, doorwayWidthMetres: DoorwayWidth);
            interior.ConfigureUpperLevel(upper, upperProps.transform, upperWalls.transform,
                                         InteriorLevelLayout.UpperLevelY(CottageStoreyHeightMetres));
            return interior;
        }

        /// <summary>Drive one frame of the component. EditMode runs no game loop, so <c>Update</c> is
        /// invoked directly — via reflection and NOT <c>SendMessage</c>, which trips the engine's
        /// edit-mode <c>ShouldRunBehaviour</c> assert (the idiom <c>CabinUpstairsTests</c> established).</summary>
        static void Tick(BuildingInterior interior) =>
            typeof(BuildingInterior)
                .GetMethod("Update", System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.NonPublic)
                .Invoke(interior, null);

        Transform WalkInside(BuildingInterior interior)
        {
            var occupant = new GameObject("occupant");
            _spawned.Add(occupant);
            interior.SetOccupant(occupant.transform);
            occupant.transform.position = interior.transform.position;
            Tick(interior);
            Assert.IsTrue(interior.IsInside, "precondition: the occupant is in the room");
            return occupant.transform;
        }

        [Test]
        public void TheUpperRoomSheetIsDrawnAtTheStoreysHeight_NotAtTheBuildingsOwnTransform()
        {
            BuildingInterior interior = StandTwoStorey(out SpriteRenderer upper);

            Assert.AreEqual(InteriorLevelLayout.UpperLevelY(CottageStoreyHeightMetres),
                            upper.transform.localPosition.y, 1e-5f,
                "the component moves the sheet itself, so the height it TESTS against and the height it " +
                "is DRAWN at are one number and cannot drift");
            Assert.AreEqual(InteriorLevelLayout.UpperLevelY(CottageStoreyHeightMetres),
                            interior.UpperLevelY, 1e-5f);
        }

        [Test]
        public void ConfiguringAStoreyTwiceDoesNotStackTwoLiftsOnIt()
        {
            BuildingInterior interior = StandTwoStorey(out SpriteRenderer upper);
            float lift = InteriorLevelLayout.UpperLevelY(CottageStoreyHeightMetres);

            // Re-running the builder over a house that already has an upstairs is normal — it is how the
            // owner re-stands a region — and it must be idempotent.
            interior.ConfigureUpperLevel(upper, upper.transform.parent.Find("FurnitureUpper"),
                                         upper.transform.parent.Find("WallsUpper"), lift);

            Assert.AreEqual(lift, upper.transform.localPosition.y, 1e-5f);
        }

        [Test]
        public void TheStoreySwapsAtOnceAndTheBodyFollows()
        {
            BuildingInterior interior = StandTwoStorey(out SpriteRenderer upper);
            Transform occupant = WalkInside(interior);
            Vector3 foot = occupant.position;

            Assert.IsTrue(interior.TryGoToLevel(InteriorLevelLayout.UpperLevel));

            Assert.AreEqual(1, interior.Level,
                "the storey is TRUE on the frame of the press — the interact registry, the colliders and " +
                "the drawn room all follow it now, and nothing waits on the walk");
            Assert.IsTrue(upper.enabled);
            Assert.IsTrue(interior.IsClimbing, "and the body is on the stairs");
            Assert.AreEqual(foot.x, occupant.position.x, 1e-5f, "which start where they were standing");
            Assert.AreEqual(foot.y, occupant.position.y, 1e-5f);

            Assert.AreEqual(foot.y + interior.UpperLevelY, interior.Climb.Head.y, 1e-5f,
                "and end one storey up");
        }

        [Test]
        public void SomethingElseMovingTheOccupantEndsTheClimbRatherThanDraggingThemBack()
        {
            BuildingInterior interior = StandTwoStorey(out _);
            Transform occupant = WalkInside(interior);
            interior.TryGoToLevel(InteriorLevelLayout.UpperLevel);
            Assert.IsTrue(interior.IsClimbing, "precondition: mid-climb");

            // A spawn, a region hop or a dev teleport — somebody else is placing this fisher now.
            occupant.position = interior.transform.position + new Vector3(0f, -40f, 0f);
            Tick(interior);

            Assert.IsFalse(interior.IsClimbing, "the walk hands the body back rather than fighting for it");
            Assert.IsFalse(interior.IsInside, "and the threshold test runs on that very frame");
            Assert.AreEqual(0, interior.Level, "so the house opens on its ground floor next time");
            Assert.IsFalse(MoveActionClaim.IsClaimed, "and the move keys are given back, never wedged");
        }

        [Test]
        public void AClimbNeverLeavesTheMoveAxisClaimedOrTheGaitHeld()
        {
            BuildingInterior interior = StandTwoStorey(out _);
            Transform occupant = WalkInside(interior);
            Vector3 foot = occupant.position;

            var skin = occupant.gameObject.AddComponent<IsoCharacterSprite>();
            interior.TryGoToLevel(InteriorLevelLayout.UpperLevel);

            Assert.IsTrue(interior.AdvanceClimb(0.1f), "a fifth of the way up");
            Assert.IsTrue(skin.IsSpeedHeld,
                "⚠ the gait is STATED while the climb writes position — and stated in Update, before " +
                "IsoCharacterSprite measures in LateUpdate. Left to measurement the walker reads ~5 m/s " +
                "of rise and draws a sprint on the spot");
            Assert.IsTrue(MoveActionClaim.IsClaimed, "and the move keys cannot fight the path");
            Assert.Greater(occupant.position.y, foot.y, "and the body is genuinely on its way");
            Assert.Less(occupant.position.y, foot.y + interior.UpperLevelY, "but not there yet");

            Assert.IsFalse(interior.AdvanceClimb(GameServices.StairClimbSeconds),
                           "the climb finishes");
            Assert.AreEqual(foot.y + interior.UpperLevelY, occupant.position.y, 1e-4f,
                "landing exactly on the floor above");
            Assert.IsFalse(skin.IsSpeedHeld, "the gait goes back to measurement");
            Assert.IsFalse(MoveActionClaim.IsClaimed, "and the player gets their legs back");
        }

        [Test]
        public void SABOTAGE_AStoreyStoodAtTheGroundFloorsOwnYIsRefusedOutLoud()
        {
            // The defect as a scene: a storey above configured with no lift. It is what ADR 0036 shipped,
            // it draws and collides perfectly, and the ONLY thing that can catch it is somebody saying so.
            var buildingGo = new GameObject("cottage");
            _spawned.Add(buildingGo);

            var ground = new GameObject("Interior").AddComponent<SpriteRenderer>();
            var upper = new GameObject("InteriorUpper").AddComponent<SpriteRenderer>();
            var props = new GameObject("FurnitureUpper");
            foreach (Transform t in new[] { ground.transform, upper.transform, props.transform })
                t.SetParent(buildingGo.transform, worldPositionStays: false);

            var interior = buildingGo.AddComponent<BuildingInterior>();
            interior.Configure(null, ground, null, CottageWidth, CottageLength, 0, 8, DepthScale,
                               WallThickness, DoorwayWidth);

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error, new System.Text.RegularExpressions.Regex("GROUND FLOOR's own Y"));
            interior.ConfigureUpperLevel(upper, props.transform, null, 0f);

            Assert.AreEqual(0f, interior.UpperLevelY, 1e-6f);
            Assert.AreEqual(ground.transform.localPosition.y, upper.transform.localPosition.y, 1e-6f,
                "and the two sheets are drawn at the same Y — which is the bug, stated as a number");
            Assert.AreEqual(interior.Footprint.Centre,
                            interior.FootprintFor(InteriorLevelLayout.UpperLevel).Centre,
                "the storeys are indistinguishable to the inside test too");
        }

        [Test]
        public void TheInsideTestFollowsTheStoreyYouAreOn()
        {
            BuildingInterior interior = StandTwoStorey(out _);
            InteriorFootprint ground = interior.Footprint;
            InteriorFootprint upper = interior.FootprintFor(InteriorLevelLayout.UpperLevel);

            Assert.AreEqual(interior.UpperLevelY, upper.Centre.y - ground.Centre.y, 1e-5f);

            // The back of the bedroom: on the storey above, and nowhere near the one below. Taken at the
            // far end rather than at the centre because the two rectangles genuinely overlap in the
            // middle (see ARoomLiftedByAStorey…) — the storeys are told apart by the LEVEL the test is
            // asked about, not by geometry alone.
            Vector2 backOfTheBedroom = upper.ModelToWorld(new Vector2(0f, CottageLength * 0.5f - 0.5f));
            Assert.IsTrue(upper.Contains(backOfTheBedroom, WallThickness));
            Assert.IsFalse(ground.Contains(backOfTheBedroom, WallThickness),
                "which is exactly why the threshold test has to know which storey it is asking about");
        }

        [Test]
        public void ABuildingWithNoStoreyAboveIsUntouchedByAnyOfThis()
        {
            var go = new GameObject("village cottage");
            _spawned.Add(go);
            var interior = go.AddComponent<BuildingInterior>();
            interior.Configure(null, null, null, CottageWidth, CottageLength, 0, 8, DepthScale,
                               WallThickness, DoorwayWidth);

            Assert.IsFalse(interior.HasUpperLevel);
            Assert.AreEqual(0f, interior.UpperLevelY, 1e-6f);
            Assert.AreEqual(interior.Footprint.Centre,
                            interior.FootprintFor(InteriorLevelLayout.UpperLevel).Centre,
                "a house with one storey has one footprint at one height, whatever it is asked");
            Assert.IsFalse(interior.IsClimbing);
        }
    }
}
