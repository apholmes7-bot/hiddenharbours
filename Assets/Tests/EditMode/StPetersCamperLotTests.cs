using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;                 // YSortSprite
using HiddenHarbours.Art.Editor;          // VillageBuildingCatalog, InteriorCatalog
using HiddenHarbours.Economy;             // HomeDef, HomeDoor
using HiddenHarbours.World;               // TidalTerrain

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE CAMPER LOT ON AUNT GINNY'S LAND</b> — that it stands somewhere a camper can stand, that it
    /// is the size art says it is, and that the owner's two open decisions really are one field each.
    ///
    /// <para>Everything here re-derives from the contracts rather than restating the numbers in
    /// <see cref="StPetersCamperLot"/>, so a moved shed, a re-baked cottage or a flipped variant fails
    /// with the figure to use instead of passing on a stale constant.</para>
    /// </summary>
    public class StPetersCamperLotTests
    {
        /// <summary>The plot's own tightest existing walking gap — the woodshed's 2.09 m against the
        /// cottage. Anything new on this land clears everything else by at least as much, which is the
        /// only non-arbitrary bar available: it is what the owner already accepted on the ground.</summary>
        const float WalkingGapMetres = 2.0f;

        const float SpringHighWater = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;   // 2.2

        private GameObject _go;
        private TidalTerrain _terrain;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("StPetersTerrain_CamperLotTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_fixtureSprite != null) Object.DestroyImmediate(_fixtureSprite);
            if (_fixtureTexture != null) Object.DestroyImmediate(_fixtureTexture);
            _fixtureSprite = null;
            _fixtureTexture = null;
        }

        // =================================================================================
        //  1. WHAT ART SAYS THE CAMPER IS
        // =================================================================================

        /// <summary>
        /// 🔴 <b>The sidecar actually parses, and it parses the RIGHT numbers.</b> The document holds
        /// polygons (arrays of arrays) that <see cref="JsonUtility"/> has no representation for, so
        /// <see cref="CamperSidecar"/> lifts three flat blocks out of it instead of parsing the whole
        /// file. If that lifting is wrong it fails SILENTLY — every field deserialises to 0 and the lot
        /// places a camper of no size — so the values are pinned against art's own committed table.
        /// </summary>
        [Test]
        public void BothVariants_ReadTheDimensionsArtPublished()
        {
            var clipper = CamperSidecar.Read(CamperSidecar.Clipper);
            Assert.IsTrue(clipper.IsValid, "the Clipper's sidecar did not parse");
            Assert.AreEqual(7.16f, clipper.BodyLengthMetres, 1e-3f);
            Assert.AreEqual(8.58f, clipper.OverallLengthMetres, 1e-3f);
            Assert.AreEqual(2.20f, clipper.WidthMetres, 1e-3f);
            Assert.AreEqual(2.00f, clipper.HeadroomMetres, 1e-3f, "the Clipper is the one you stand up in");

            var bantam = CamperSidecar.Read(CamperSidecar.Bantam);
            Assert.IsTrue(bantam.IsValid, "the Bantam's sidecar did not parse");
            Assert.AreEqual(4.34f, bantam.BodyLengthMetres, 1e-3f);
            Assert.AreEqual(5.58f, bantam.OverallLengthMetres, 1e-3f);
            Assert.AreEqual(1.98f, bantam.WidthMetres, 1e-3f);
            Assert.AreEqual(1.80f, bantam.HeadroomMetres, 1e-3f, "the Bantam is the one you stoop in");

            Assert.AreEqual(default(CamperSidecar.Dimensions).IsValid,
                            CamperSidecar.Read("schooner").IsValid,
                            "a variant the kit does not loft must read as invalid, not as zeroes");
        }

        /// <summary>
        /// 🔴 <b>The width the lot places by is NOT <c>hull.width_m</c>, and the length is NOT centred on
        /// the position.</b> The step folds out past the skin on the door side, the sewer outlet stands
        /// proud on the street side, and the rig origin is the BODY centre with the drawbar hanging past
        /// it. Both are the obvious wrong reading, so both are pinned.
        /// </summary>
        [Test]
        public void TheGroundACamperCovers_IsWiderThanItsBodyAndNotCentredOnIt()
        {
            var d = CamperSidecar.Read(CamperSidecar.Clipper);

            Assert.Greater(d.HalfSpanMetres * 2f, d.WidthMetres,
                "the step and the hookups reach past the skin — placing by hull.width_m under-reserves " +
                "the ground on both sides");
            Assert.AreEqual(1.452f, d.HalfSpanMetres, 1e-3f,
                "the widest thing on a Clipper is the sewer outlet at −1.452 m, not the 1.10 m body half");

            Assert.Greater(d.NoseMetres, d.TailMetres,
                "the drawbar hangs forward of the origin — the footprint is asymmetric about it");
            Assert.AreEqual(5.00f, d.NoseMetres, 1e-3f);
            Assert.AreEqual(3.58f, d.TailMetres, 1e-3f);

            // The trap this replaces: half the overall length, which is 4.29 m and short by 0.71 m.
            Assert.Greater(d.NoseMetres, d.OverallLengthMetres * 0.5f,
                "half the overall length under-reserves the nose by most of a tow hitch");

            Assert.AreEqual(Mathf.Sqrt(1.452f * 1.452f + 5.00f * 5.00f), d.FootprintRadiusMetres, 1e-3f,
                "the reservation is the furthest CORNER from the origin");
        }

        // =================================================================================
        //  2. WHERE IT STANDS
        // =================================================================================

        /// <summary>The site derives from her cottage and nothing else, so moving her plot moves the
        /// lot with it rather than stranding a camper in the trees.</summary>
        [Test]
        public void TheLot_IsDerivedFromGinnysCottage()
        {
            Assert.AreEqual(StPetersGinnyPlot.CottagePos + StPetersCamperLot.LotOffset,
                            StPetersCamperLot.LotPos);
            Assert.AreEqual(StPetersGinnyPlot.CottagePos, StPetersCamperLot.Approach,
                "you arrive past her house — the door turns toward the ground people walk up from");
        }

        /// <summary>
        /// 🔴 <b>You cannot live below the tide.</b> The same guard the cottage and every shed carry, and
        /// the sabotage arm for this PR: move the lot into the sea and this fails.
        /// </summary>
        [Test]
        public void TheLot_IsOnDryGroundAboveTheTreeLine_InsideThePlateau()
        {
            Vector2 p = StPetersCamperLot.LotPos;
            float e = _terrain.ElevationAt(p);

            Assert.Greater(e, SpringHighWater,
                $"the camper stands at {e:0.00} m, at or below spring high water ({SpringHighWater:0.00} m)");
            Assert.Greater(e, StPetersWoods.TreeLineElevation,
                $"the camper stands at {e:0.00} m, below the {StPetersWoods.TreeLineElevation:0.00} m " +
                "tree line — that is not the woods, it is the heath");

            // The whole footprint, not just the point: a camper is eight metres long.
            float reach = StPetersCamperLot.FootprintRadiusMetres;
            foreach (Vector2 dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                Vector2 corner = p + dir * reach;
                Assert.Greater(_terrain.ElevationAt(corner), SpringHighWater,
                    $"the camper's ground at {corner} floods");
                Assert.LessOrEqual(
                    TidalTerrain.IslandDistance(corner, StPetersBuilder.IslandCenter,
                                                StPetersBuilder.IslandRadius, StPetersBuilder.IslandRadiusY),
                    StPetersBuilder.IslandRadius,
                    $"the camper's ground at {corner} is past the plateau edge");
            }
        }

        /// <summary>
        /// 🔴 <b>The camper is on HER LAND, by the same test she is.</b> Her plot was sited 85 m out
        /// because <see cref="StPetersWoods.DooryardRadius"/> is where the village's disturbance stops
        /// reading on the ground; a camper parked inside that band would be on the village's weedy edge
        /// wearing a fence, which is exactly the thing #551 argued against. <b>This is why the lot is
        /// north and not on the approach side</b> — every approach-side site that clears her dooryard,
        /// her freezer and the lean-to lands 72–74 m from the hearth, inside the band.
        /// </summary>
        [Test]
        public void TheLot_StandsPastTheVillagesDooryardBand_EvenAtItsDrawbar()
        {
            float nearest = Vector2.Distance(StPetersCamperLot.LotPos, StPetersBuilder.VillageHearthPos)
                            - StPetersCamperLot.FootprintRadiusMetres;

            Assert.Greater(nearest, StPetersWoods.DooryardRadius,
                $"the camper's nearest ground is {nearest:0.0} m from the village hearth, inside the " +
                $"{StPetersWoods.DooryardRadius:0.0} m dooryard band. That is the village's disturbed " +
                "edge, not Ginny's land — the same line her cottage was sited past.");
        }

        /// <summary>
        /// Nothing on the plot stands in anything else, by the plot's own tightest existing walking gap.
        /// Derived from each thing's own footprint, so a re-baked cottage or a moved shed fails here
        /// with the overlap in metres.
        /// </summary>
        [Test]
        public void TheCamper_ClearsEverythingElseOnThePlot()
        {
            float camper = StPetersCamperLot.FootprintRadiusMetres;
            Assert.Greater(camper, 0f, "no footprint — this assert would be vacuous");

            var cottage = VillageBuildingCatalog.Find(StPetersGinnyPlot.CottageKey);
            if (cottage.IsValid)
            {
                float gap = Vector2.Distance(StPetersCamperLot.LotPos, StPetersGinnyPlot.CottagePos)
                            - StPetersVillage.FootprintRadiusMetres(cottage) - camper;
                Assert.Greater(gap, WalkingGapMetres,
                    $"only {gap:0.00} m between the camper and her cottage — you cannot get round it");
            }

            foreach (var shed in StPetersGinnyPlot.Sheds)
            {
                float gap = Vector2.Distance(StPetersCamperLot.LotPos, shed.Position)
                            - shed.FootprintRadiusMetres - camper;
                Assert.Greater(gap, WalkingGapMetres,
                    $"only {gap:0.00} m between the camper and the {shed.Key}");
            }

            // Her freezer and her garden are not buildings, but walking into either is the same problem.
            foreach (var (what, at) in new[] { ("freezer", StPetersGinnyPlot.FreezerPos),
                                               ("garden", StPetersGinnyPlot.GardenPos),
                                               ("dooryard", StPetersGinnyPlot.Dooryard) })
                Assert.Greater(Vector2.Distance(StPetersCamperLot.LotPos, at) - camper, WalkingGapMetres,
                    $"the camper stands on her {what}");
        }

        /// <summary>
        /// The clearing contains the lot — and it is the CAMPER that decides the number now, which is the
        /// finding that grew <see cref="StPetersGinnyPlot.ClearingRadius"/> from 18 m to 20 m. Derived,
        /// so a variant flip or a re-site fails with the radius to use.
        /// </summary>
        [Test]
        public void TheClearing_ContainsTheCamper_AndIsWhatTheCamperNeeds()
        {
            Assert.IsTrue(StPetersGinnyPlot.IsInsideClearing(StPetersCamperLot.LotPos),
                "the camper stands outside the only clearing this plot declares, so the woods plant " +
                "through it. Grow StPetersGinnyPlot.ClearingRadius — do NOT add a second clearing.");

            float need = StPetersCamperLot.ClearingReachMetres;
            Assert.LessOrEqual(need, StPetersGinnyPlot.ClearingRadius,
                $"the camper's furthest ground reaches {need:0.00} m but the clearing is " +
                $"{StPetersGinnyPlot.ClearingRadius:0.00} m. Widen ClearingRadius to at least that.");

            Assert.AreEqual(need, StPetersGinnyPlot.RequiredClearingRadius(), 1e-3f,
                "the camper is now the furthest thing on the plot, so it should be what " +
                "RequiredClearingRadius reports — if a shed has overtaken it, the class notes are stale");
        }

        /// <summary>Her clearing and the village's do not touch, so the camper has not quietly joined
        /// the two into one settlement.</summary>
        [Test]
        public void TheGrownClearing_StillDoesNotReachTheVillages()
        {
            float between = Vector2.Distance(StPetersGinnyPlot.CottagePos,
                                             StPetersBuilder.VillageHearthPos);
            Assert.Greater(between,
                StPetersGinnyPlot.ClearingRadius + StPetersWoods.VillageClearingRadius,
                "her clearing now touches the village's — there is no woods left between them, and " +
                "'she moved into the trees' stops being true");
        }

        // =================================================================================
        //  3. THE DOOR AND THE STEP
        // =================================================================================

        /// <summary>The doorstep is off the step and pointed at the cottage — derived from the camper's
        /// own tread, not from the 7.5 m house convention that would leave you standing in the trees.</summary>
        [Test]
        public void TheDoorstep_IsOneStrideOffTheTread_TowardTheCottage()
        {
            var d = StPetersCamperLot.Dimensions;
            float outMetres = Vector2.Distance(StPetersCamperLot.Doorstep, StPetersCamperLot.LotPos);

            Assert.AreEqual(d.DoorReachMetres + StPetersCamperLot.StrideMetres, outMetres, 1e-3f);
            Assert.Greater(outMetres, d.DoorReachMetres, "you are standing ON the bottom tread");
            Assert.Less(outMetres, StPetersInhabitants.DoorstepMetres,
                "the house doorstep is 7.5 m — on a trailer that puts you in the woods");

            Vector2 toCottage = (StPetersGinnyPlot.CottagePos - StPetersCamperLot.LotPos).normalized;
            Vector2 toStep = (StPetersCamperLot.Doorstep - StPetersCamperLot.LotPos).normalized;
            Assert.AreEqual(1f, Vector2.Dot(toCottage, toStep), 1e-3f,
                "the step is not on the side you walk up from");
        }

        /// <summary>The drawbar points along the lot, not at a shed — derived, so a shed that moves
        /// re-parks the camper instead of poking a tow hitch through a wall.</summary>
        [Test]
        public void TheDrawbar_PointsAcrossTheApproach_AndAwayFromTheNearestBuilding()
        {
            Vector2 nose = StPetersCamperLot.NoseDirection;
            Assert.AreEqual(1f, nose.magnitude, 1e-3f);

            Vector2 toApproach = (StPetersCamperLot.Approach - StPetersCamperLot.LotPos).normalized;
            Assert.AreEqual(0f, Vector2.Dot(nose, toApproach), 1e-3f,
                "the camper is parked end-on to the approach, so its door faces sideways");

            Vector2 tip = StPetersCamperLot.LotPos + nose * StPetersCamperLot.Dimensions.NoseMetres;
            Vector2 wrongTip = StPetersCamperLot.LotPos - nose * StPetersCamperLot.Dimensions.NoseMetres;
            Assert.GreaterOrEqual(NearestBuilding(tip), NearestBuilding(wrongTip),
                "the drawbar points at the tighter side of the lot");

            foreach (var shed in StPetersGinnyPlot.Sheds)
                Assert.Greater(Vector2.Distance(tip, shed.Position), shed.FootprintRadiusMetres,
                    $"the drawbar tip is inside the {shed.Key}");

            static float NearestBuilding(Vector2 at)
            {
                float worst = Vector2.Distance(at, StPetersGinnyPlot.CottagePos);
                foreach (var shed in StPetersGinnyPlot.Sheds)
                {
                    float gap = Vector2.Distance(at, shed.Position) - shed.FootprintRadiusMetres;
                    if (gap < worst) worst = gap;
                }
                return worst;
            }
        }

        // =================================================================================
        //  4. THE OWNER'S TWO DECISIONS
        // =================================================================================

        /// <summary>
        /// The Def ships with the PR, carrying both pending decisions on their documented defaults. If
        /// this fails the lot is running on code fallbacks and the owner has nothing to edit.
        /// </summary>
        [Test]
        public void TheHomeDef_ShipsWithTheDocumentedDefaults()
        {
            HomeDef home = StPetersCamperLot.Home;
            Assert.IsNotNull(home,
                $"no HomeDef at {StPetersCamperLot.HomeDefPath} — the owner's starter-home and " +
                "Bantam/Clipper decisions have nowhere to live");

            Assert.AreEqual(StPetersCamperLot.FallbackHomeId, home.Id,
                "the id is what the deed is saved under — it is append-only and must not drift");
            Assert.AreEqual(HomeDef.Mode.Purchasable, home.Acquisition,
                "DEFAULT = purchasable, per the handoff. If the owner has ruled, update this test with " +
                "his ruling rather than deleting it.");
            Assert.AreEqual(CamperSidecar.Clipper, home.Variant,
                "DEFAULT = the Clipper, the one you can stand up in.");
            Assert.Greater(home.Price, 0, "a purchasable home with no price is free");
            Assert.AreEqual(home.Id, StPetersCamperLot.HomeId);
        }

        /// <summary>
        /// 🔴 <b>The Bantam/Clipper decision really is one field.</b> Every invariant above is re-checked
        /// against the OTHER variant, so the owner can flip the asset without the clearing, the spacing
        /// or the doorstep still being measured against the camper he did not pick.
        /// </summary>
        [Test]
        public void FlippingTheVariantToTheBantam_KeepsEveryInvariant()
        {
            var bantam = CamperSidecar.Read(CamperSidecar.Bantam);
            Assert.IsTrue(bantam.IsValid);

            float r = bantam.FootprintRadiusMetres;
            Assert.Less(r, StPetersCamperLot.Dimensions.FootprintRadiusMetres,
                "the Bantam is the smaller of the two — if it reserves more ground, the derivation is wrong");

            // Everything the Clipper clears, the Bantam clears by more. Checked explicitly rather than
            // argued, because "smaller therefore fine" stops being true the moment the lot moves.
            float lotToCottage = Vector2.Distance(StPetersCamperLot.LotPos, StPetersGinnyPlot.CottagePos);
            var cottage = VillageBuildingCatalog.Find(StPetersGinnyPlot.CottageKey);
            if (cottage.IsValid)
                Assert.Greater(lotToCottage - StPetersVillage.FootprintRadiusMetres(cottage) - r,
                               WalkingGapMetres, "the Bantam does not clear the cottage");

            foreach (var shed in StPetersGinnyPlot.Sheds)
                Assert.Greater(Vector2.Distance(StPetersCamperLot.LotPos, shed.Position)
                               - shed.FootprintRadiusMetres - r,
                               WalkingGapMetres, $"the Bantam does not clear the {shed.Key}");

            Assert.LessOrEqual(lotToCottage + r, StPetersGinnyPlot.ClearingRadius,
                "the Bantam falls outside the clearing");
            Assert.Greater(Vector2.Distance(StPetersCamperLot.LotPos, StPetersBuilder.VillageHearthPos) - r,
                           StPetersWoods.DooryardRadius, "the Bantam sits inside the dooryard band");
        }

        // =================================================================================
        //  5. PLACEMENT, GREYBOX AND THE SHELL
        // =================================================================================

        /// <summary>
        /// ⚠️⚠️ <b>The camper is a SHELL and the door must say so.</b> The kit ships no interior and
        /// <c>camperInteriorRig.js</c> is not in the drop, so a placed door reports NOT enterable — and
        /// it agrees with the interior contract rather than declaring it, so the day a room IS baked this
        /// test is what notices.
        /// </summary>
        [Test]
        public void ThePlacedCamper_HasADoorThatRefusesEntryWhileThereIsNoRoomBaked()
        {
            bool roomBaked = InteriorCatalog.FindRoom(StPetersCamperLot.InteriorKey).IsValid;
            Assert.IsFalse(roomBaked,
                $"a room IS baked under '{StPetersCamperLot.InteriorKey}' — the camper interior rig has " +
                "landed. Wire it (its own PR) and update this test; do not just flip the door's flag.");

            var (root, door) = PlaceLot();
            try
            {
                Assert.IsNotNull(door, "the camper went up with no door on it");
                Assert.AreEqual(roomBaked, door.InteriorStood,
                    "the door's enterable state disagrees with what is actually baked — that is the " +
                    "sabotage this test exists for");
                Assert.IsNotNull(door.Home, "the door does not know which home it belongs to");
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// ⭐ <b>The sheets are BAKED, and the lot draws the parked frame at its own facing.</b> This
        /// test shipped as <c>WithNoBakedSheets_TheLotStillDrawsAMarkerOfTheRightSize</c> — a greybox
        /// assertion with a tripwire that fired the moment #553's sheets reached the merged tree, by
        /// design. This is its flipped form.
        ///
        /// <para><b>The parked frame is the door CUE's first frame, not the rest sheet</b> — the
        /// owner's 2026-08-18 no-awning ruling, and the reason the expected name carries a role and a
        /// swing index. It is built from <see cref="CamperKit.Build.Key"/> rather than spelled out, so
        /// a renamed role fails here with the name to use instead of passing on a stale string. A cue
        /// frame other than <see cref="StPetersCamperLot.ParkedFrame"/> would stand the camper in the
        /// world with its door half open, which looks finished and is wrong.</para>
        ///
        /// <para>The scale assert is the building kit's first guardrail: baked art is honest metric
        /// size (32 px = 1 m with the camera's foreshortening baked in); scaling it to the footprint
        /// would break the pixel grid.</para>
        ///
        /// <para>The greybox branch is now unreachable in a full checkout and keeps no direct coverage
        /// here — its contract (draw the RIGHT art or none, fall back to the sheds' tinted marker) is
        /// pinned by <see cref="TheSpriteLookup_RefusesAFacingItCannotFind"/>, and if the sheets ever
        /// leave the tree this test's first assert fires with the map back.</para>
        /// </summary>
        [Test]
        public void WithTheSheetsBaked_TheLotDrawsTheParkedFrameAtItsOwnFacing()
        {
            Sprite baked = StPetersCamperLot.TryLoadCamperSprite(StPetersCamperLot.Variant,
                                                                 StPetersCamperLot.DoorFacing);
            Assert.IsNotNull(baked,
                "no baked camper sheet found — if the sheets left the tree the lot falls back to the " +
                "tinted greybox marker (that branch still ships), and this test should flip back to " +
                "its pre-#553 greybox form.");

            var parked = new CamperKit.Build(StPetersCamperLot.Variant, StPetersCamperLot.ParkedRole);
            Assert.AreEqual(
                $"{parked.Key}_d{StPetersCamperLot.DoorFacing}_s{StPetersCamperLot.ParkedFrame}",
                baked.name,
                "the lookup resolved something other than the parked frame. It must be the door cue's " +
                "first frame — that is the one the kit bakes with no awning (the owner's ruling) and " +
                "with the door shut and the step stowed. The rest sheet is the awninged fit-out and a " +
                "later cue frame is mid-swing; either would be wrong in the world.");

            var (root, door) = PlaceLot();
            try
            {
                var sr = door.GetComponent<SpriteRenderer>();
                Assert.AreSame(baked, sr.sprite,
                    "the placed camper is not drawing the parked frame the lookup resolved");

                Assert.AreEqual(Vector3.one, door.transform.localScale,
                    "baked art must draw at honest metric size — scaling it to the footprint breaks " +
                    "the pixel grid (the building kit's first guardrail). Footprint-sized scaling is " +
                    "the GREYBOX branch's job only.");

                Assert.IsNotNull(door.GetComponent<YSortSprite>(),
                    "a thing you walk around must Y-sort (ADR 0032) or the meadow buries it");
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>The lookup draws the RIGHT art or none — never an arbitrary facing, which would look
        /// finished and be wrong.</summary>
        [Test]
        public void TheSpriteLookup_RefusesAFacingItCannotFind()
        {
            Assert.IsNull(StPetersCamperLot.TryLoadCamperSprite(StPetersCamperLot.Variant, 99));
            Assert.IsNull(StPetersCamperLot.TryLoadCamperSprite("schooner", 0));
            Assert.IsNull(StPetersCamperLot.TryLoadCamperSprite(null, 0));
        }

        /// <summary>
        /// 🔴 <b>THE PARKED CAMPER HAS NO AWNING</b> — the owner's ruling of 2026-08-18, pinned in the
        /// two independent ways it can go wrong.
        ///
        /// <para><b>1. The wrong sheet.</b> The build the lot parks from must be one the kit bakes
        /// unawninged. <c>CamperKit.Build.AwningOverride</c> is a hard <c>false</c> for
        /// <see cref="CamperKit.Role.Enter"/> and <c>null</c> for <see cref="CamperKit.Role.Rest"/> —
        /// "defer to the rig", i.e. fit whatever the variant's default is, which for the Clipper is an
        /// awning. Reading the flag rather than restating the role means a kit that ever starts baking
        /// the cue awninged fails here instead of quietly shipping.</para>
        ///
        /// <para><b>2. The right sheet, re-baked wrong.</b> A name assert cannot see an <c>enter</c>
        /// sheet that has grown an awning, so the pixels are checked too: the parked cell and the rest
        /// cell must differ by more than <see cref="AwningPixelsFloor"/>. Measured on the sheets in the
        /// tree, the Clipper's rest/cue-frame-0 difference runs <b>2858–5083 px per facing</b>
        /// (<c>CamperKit.AwningPop</c> records the same range) and is <b>5083 px at facing 2</b>, the one
        /// this lot parks at; 2000 sits below that with margin. Point the lookup back at the rest sheet
        /// and this collapses to 0 — which is the swap this test exists to catch.</para>
        ///
        /// <para>⚠ Compares DIFFERING pixels, not an ink count. The awning is drawn largely OVER the
        /// body rather than beyond its silhouette, so at facing 2 it adds only 634 opaque pixels while
        /// changing 5083 — an opaque-count guard would sit under any sane floor and go red on correct
        /// code.</para>
        ///
        /// <para>⚠ And which way that pixel test reads is asked of the CONTRACT, not assumed from the
        /// variant. The Bantam fits no awning in either role, so for it the two must be pixel-IDENTICAL
        /// and a difference is the bug; hard-coding the Clipper's direction would fail on correct art
        /// the moment the owner flips the Def to the Bantam.</para>
        /// </summary>
        [Test]
        public void TheParkedFrame_IsBakedWithoutTheAwning()
        {
            Assert.That(new CamperKit.Build(CamperSidecar.Clipper, StPetersCamperLot.ParkedRole)
                            .AwningOverride, Is.False,
                $"the lot parks from the '{StPetersCamperLot.ParkedRole}' sheet, which the kit does NOT " +
                "bake unawninged. The owner ruled the awning is never drawn; a sheet that defers its " +
                "awning to the rig cannot honour that.");

            // BOTH lengths, not just the one the Def happens to name. The variant is one of the owner's
            // two open decisions and he flips it on an asset, so a guard that only covered today's
            // choice would go quiet exactly when he exercised it.
            foreach (string variant in CamperKit.Variants)
            {
                var parked = new CamperKit.Build(variant, StPetersCamperLot.ParkedRole);
                var fitted = new CamperKit.Build(variant, CamperKit.Role.Rest);

                Sprite baked = StPetersCamperLot.TryLoadCamperSprite(variant,
                                                                     StPetersCamperLot.DoorFacing);
                Assert.IsNotNull(baked, $"no baked '{variant}' sheet found — see the parked-frame test.");
                Assert.IsTrue(baked.name.StartsWith(parked.Key, System.StringComparison.Ordinal),
                    $"'{baked.name}' did not come from the '{parked.Key}' sheet");

                Sprite restFrame = FindSprite($"{fitted.Key}_d{StPetersCamperLot.DoorFacing}");
                Assert.IsNotNull(restFrame,
                    $"'{fitted.Key}_d{StPetersCamperLot.DoorFacing}' is gone. The rest sheet is " +
                    "deliberately kept baked and named as it was — nothing in the no-awning ruling " +
                    "deletes art, and a later consumer may want the fitted-out look.");

                Color[] parkedPixels = CellPixels(baked), restPixels = CellPixels(restFrame);
                if (parkedPixels == null || restPixels == null)
                {
                    Assert.Ignore("the camper sheets did not decode — they are LFS-backed, so a " +
                                  "pointer-only checkout cannot run this. `git lfs pull`. Ignored " +
                                  "rather than passed, so an unfetched checkout cannot look green.");
                    return;
                }

                Assert.AreEqual(restPixels.Length, parkedPixels.Length,
                    $"the '{variant}' rest and enter sheets no longer share a cell size. That identity " +
                    "is what makes the parked frame the same camper on the same ground minus the " +
                    "awning — without it the camper shifts when its door opens, and #553's union-crop " +
                    "argument is broken.");

                int differing = 0;
                for (int i = 0; i < parkedPixels.Length; i++)
                    if (parkedPixels[i] != restPixels[i]) differing++;

                // ⚠ Which way this reads depends on the VARIANT, and the contract is asked rather than
                // assumed. The Clipper fits an awning by default so its two roles must differ; the
                // Bantam fits none either way, so for IT they must be identical (measured: 0 px at
                // every facing) — hard-coding the Clipper's direction would fail on correct art.
                if (RestSheetIsAwninged(fitted.Key))
                    Assert.That(differing, Is.GreaterThan(AwningPixelsFloor),
                        $"only {differing} px separate the '{variant}' parked frame from its awninged " +
                        $"rest frame at facing {StPetersCamperLot.DoorFacing} (expected " +
                        $">{AwningPixelsFloor}; measured 5083). Either the lot is parking from the rest " +
                        "sheet again, or the cue has been re-baked WITH an awning. Both put a wall of " +
                        "canvas over the door.");
                else
                    Assert.That(differing, Is.Zero,
                        $"{differing} px separate the '{variant}' parked frame from its rest frame, on " +
                        "a variant whose rest sheet fits NO awning — the two roles should be the same " +
                        "build. Something other than the awning now splits them; see CamperBakeTests." +
                        "OnlyTheAwningSeparatesARestSheetFromItsEnterSheetsFirstFrame.");
            }
        }

        /// <summary>Whether the kit baked this sheet with an awning fitted, <b>read off the contract the
        /// bake wrote</b> rather than inferred from the variant name — a drop that changes which length
        /// fits an awning then moves this test with it. See <c>CamperKit.Build.AwningOverride</c>: only
        /// the cue decides its own awning; a rest sheet defers to the rig.</summary>
        static bool RestSheetIsAwninged(string sheetKey)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(CamperKit.ContractPath);
            Assert.IsNotNull(json,
                $"no camper contract at {CamperKit.ContractPath}. It is written BY the bake, so a " +
                "missing one means these sheets were never baked by this code.");

            var contract = JsonUtility.FromJson<CamperKit.Contract>(json.text);
            foreach (var sheet in contract.sheets)
                if (sheet.key == sheetKey) return sheet.awning;

            Assert.Fail($"the contract has no entry for '{sheetKey}' — CamperKit.Builds and the sheets " +
                        "on disk have drifted apart; re-bake rather than editing the JSON.");
            return false;
        }

        /// <summary>The floor the awning's own footprint clears with margin — see
        /// <see cref="TheParkedFrame_IsBakedWithoutTheAwning"/> for the measurement it sits under.</summary>
        const int AwningPixelsFloor = 2000;

        /// <summary>A baked sub-sprite by exact name, or null. Sheets import spriteMode MULTIPLE, so they
        /// come out of <c>LoadAllAssetsAtPath</c> and are matched by name.</summary>
        static Sprite FindSprite(string exactName)
        {
            foreach (string guid in AssetDatabase.FindAssets("camper t:Texture2D",
                                                             new[] { "Assets/_Project/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is Sprite s && s.name == exactName) return s;
            }
            return null;
        }

        /// <summary>
        /// One sprite's cell, read from the PNG on disk rather than through the imported texture.
        ///
        /// <para>⚠ The sheets import <c>isReadable: 0</c>, so <c>sprite.texture.GetPixels</c> throws.
        /// Decoding the file gives the same bytes without turning on a CPU copy of a 14 MB sheet for
        /// every build the owner runs. Returns null when the file cannot be read or decoded — an
        /// LFS-pointer checkout, which the caller reports as Ignore rather than green.</para>
        /// </summary>
        static Color[] CellPixels(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite.texture);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            var decoded = new Texture2D(2, 2);
            try
            {
                if (!decoded.LoadImage(File.ReadAllBytes(path))) return null;

                Rect r = sprite.rect;
                if (r.xMax > decoded.width || r.yMax > decoded.height) return null;
                return decoded.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
            }
            finally { Object.DestroyImmediate(decoded); }
        }

        /// <summary>
        /// 🔴 <b>The meadow must not grow through the newest building on the island.</b> This is the
        /// exact shape of the post-office bug <see cref="StPetersGrass.BuildingSites"/>'s own note is
        /// about: a site left off that list is silent from outside and swallows the building.
        /// </summary>
        [Test]
        public void TheLot_HasItsOwnGrassClearing()
        {
            bool cleared = false;
            foreach (var site in StPetersGrass.BuildingSites)
                if (Vector2.Distance(site, StPetersCamperLot.LotPos) < 0.01f) { cleared = true; break; }

            Assert.IsTrue(cleared,
                $"the camper stands at {StPetersCamperLot.LotPos} and StPetersGrass.BuildingSites has no " +
                "clearing for it, so the meadow grows through its ground");
        }

        /// <summary>Places the lot on its own root and returns the camper's door. The fixture passes no
        /// terrain (the dry-ground guard has its own test above) and no occupant.</summary>
        private (GameObject root, HomeDoor door) PlaceLot()
        {
            var root = new GameObject("CamperLotFixture");
            _fixtureTexture = new Texture2D(2, 2);
            _fixtureSprite = Sprite.Create(_fixtureTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 32f);
            Assert.IsTrue(StPetersCamperLot.Place(root.transform, null, _fixtureSprite),
                          "the lot did not place");
            return (root, root.GetComponentInChildren<HomeDoor>());
        }

        private Sprite _fixtureSprite;
        private Texture2D _fixtureTexture;
    }
}
