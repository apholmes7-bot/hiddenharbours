using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// Runs the REAL interior rigs through the REAL JS host and measures what the pilot rests on.
    ///
    /// <para>These are the tests that answer questions no amount of reading could settle, and every one
    /// of them exists because the same class of question has already been answered wrong in this repo:
    /// which way does the rig turn, which gable is the door on, does the room match its shell, did the
    /// options apply at all, and will the sheet fit under the import cap. They need no graphics device —
    /// only ClearScript — so CI runs the lot.</para>
    ///
    /// <para><b>The measurement this whole branch turns on</b> is
    /// <see cref="TheRoomStandsUnderItsShellAtAFacingOffsetOfFour"/>: the exterior rigs put their door
    /// on <c>+Y</c> and the room rig puts its doorway on <c>−Y</c>, so a room shown at the shell's own
    /// facing has its doorway against the BACK wall. Nothing throws when that is wrong; the player walks
    /// in the front door and appears at the back of the room, and it reads as an art bug.</para>
    /// </summary>
    public class InteriorRigBakeTests
    {
        const string Interior = "InteriorIso";
        const string Prop = "PropIso";
        const string House = "HouseIso";

        /// <summary>The pilot room's options, as the kit dials them.</summary>
        static string RoomOpts() => InteriorBakeMenu.OptionsLiteralFor(InteriorKit.RoomSet[0]);

        /// <summary>
        /// The pilot room rendered ONCE for the whole fixture. A room is a 1180×900 cell z-buffered and
        /// dithered in JS, eight times — several seconds a facing — so re-rendering it per test would
        /// put minutes on every CI run for no extra coverage.
        /// </summary>
        static InteriorCellSet PilotRoomCells()
        {
            if (_pilotRoom != null) return _pilotRoom;

            InteriorBakeRequest req = InteriorBakeMenu.RequestFor(InteriorKit.RoomSet[0]);
            using IRigScriptHost host = RigScriptHostFactory.Create();
            _pilotRoom = InteriorRigBaker.RenderCells(req, host);
            return _pilotRoom;
        }

        static InteriorCellSet _pilotRoom;

        static IRigScriptHost Host(params string[] rigKeys)
        {
            IRigScriptHost host = RigScriptHostFactory.Create();
            foreach (string key in rigKeys) RigCatalog.Install(host, RigCatalog.Get(key));
            return host;
        }

        // =============================================================================
        //  the catalog entries exist and report the cells the kit is solved for
        // =============================================================================

        [Test]
        public void BothInteriorRigsInstallAndReportTheirNativeCells()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();

            RigGeometry room = RigCatalog.Install(host, RigCatalog.Get("interior"));
            Assert.AreEqual(1180, room.Width, "the room rig's native cell width");
            Assert.AreEqual(900, room.Height, "the room rig's native cell height");
            Assert.AreEqual(8, room.NativeDirs);

            RigGeometry prop = RigCatalog.Install(host, RigCatalog.Get("interiorProp"));
            Assert.AreEqual(460, prop.Width);
            Assert.AreEqual(460, prop.Height);
        }

        [Test]
        public void TheRigsAgreeOnPixelsPerMetre_ThirtyTwo()
        {
            using IRigScriptHost host = Host("interior", "interiorProp", "house");
            Assert.AreEqual(32, (int)host.EvaluateNumber($"{Interior}.PX"));
            Assert.AreEqual(32, (int)host.EvaluateNumber($"{Prop}.PX"));
            Assert.AreEqual(32, (int)host.EvaluateNumber($"{House}.PX"),
                            "one scale ladder — a room, its furniture and its shell all at 32 px = 1 m");
        }

        // =============================================================================
        //  one camera, one turntable
        // =============================================================================

        [Test]
        public void TheRoomAndItsShellProjectIdenticallyAtEveryFacing()
        {
            using IRigScriptHost host = Host("house", "interior");

            bool agree = InteriorRigAzimuthProbe.ProjectionsAgree(host, House, Interior, out string report);
            Assert.IsTrue(agree, report);
        }

        [Test]
        public void TheFurnitureSharesTheRoomsTurntableToo()
        {
            using IRigScriptHost host = Host("interior", "interiorProp");

            bool agree = InteriorRigAzimuthProbe.ProjectionsAgree(host, Interior, Prop, out string report);
            Assert.IsTrue(agree,
                          "a chair that turned differently from the room it stands in would drift as " +
                          "the building turned:\n" + report);
        }

        // =============================================================================
        //  ⭐ the registration
        // =============================================================================

        // The door-gable pins (TheShellsDoorIsOnPlusY_AndTheRoomsIsOnMinusY,
        // TheBuildingProbeWouldMeasureTheRoomBackwards_WhichIsWhyItIsNotUsedOnIt,
        // TheRoomStandsUnderItsShellAtAFacingOffsetOfFour) retired with the village return (#898, R2):
        // the returned room opens its doorway on +Y and registers under its own shell at offset 0,
        // measured by VillageReturnIntakeTests.TheReturnedRoomsStandUnderTheirOwnShellsAtOffsetZero.

        [Test]
        public void TheRoomAndTheShellResolveTheSameFootprint_WhichIsTheOneToOneClaim()
        {
            using IRigScriptHost host = Host("house", "interior");

            var exterior = VillageBuildingKit.FindBuild("sageCottage").Value;
            string exteriorOpts = VillageBuildingBakeMenu.OptionsLiteralFor(exterior);

            double eWd = host.EvaluateNumber($"{House}.anchors(0,{exteriorOpts}).Wd");
            double eLn = host.EvaluateNumber($"{House}.anchors(0,{exteriorOpts}).Ln");
            double iWd = host.EvaluateNumber($"{Interior}.anchors(0,{RoomOpts()}).Wd");
            double iLn = host.EvaluateNumber($"{Interior}.anchors(0,{RoomOpts()}).Ln");

            Assert.AreEqual(eWd, iWd, 1e-6, "Wd — the interior header claims this and it is measured here");
            Assert.AreEqual(eLn, iLn, 1e-6, "Ln");
            Assert.AreEqual(6.6, iWd, 1e-6, "the sage cottage at size 0.25: 6 + 0.25*2.4");
            Assert.AreEqual(8.05, iLn, 1e-6, "7 + 0.25*4.2");
        }

        [Test]
        public void TheRoomDeclaresHowFarItIsToTheFloorAbove_AndTheCommittedContractCarriesThatNumber()
        {
            using IRigScriptHost host = Host("interior");

            // The rig is the authority, read the way the bake reads it. Since the village return
            // (drop 14) anchors().storeyZ is this floor's height ABOVE GRADE, not the rise, so the rise
            // is the upper storey's floor less this one's (InteriorRigBaker.StoreyRiseMetres).
            double declared = InteriorRigBaker.StoreyRiseMetres(host, Interior, RoomOpts());
            double aboveGrade = host.EvaluateNumber($"{Interior}.anchors(0,{RoomOpts()}).storeyZ");

            Assert.AreEqual(2.65, declared, 1e-6,
                "the sage cottage's floor-to-floor rise, measured at intake on Node and V8");
            Assert.Greater(declared - aboveGrade, 1.0,
                "the anchor is the floor above grade, far from the rise: reading it as the rise would " +
                "stand an upper storey half a metre off the ground floor");

            // ...and the bake wrote it down. This is the seam the whole storey-height fix rests on: the
            // engine reads a NUMBER FROM THE ART rather than one typed into a builder, exactly as it does
            // the facing offset. A contract whose value has drifted from the rig means a re-bake is due,
            // and the picture goes wrong silently in the meantime.
            InteriorKit.Contract contract = InteriorKit.Load();
            Assert.IsNotNull(contract, "the interiors contract is committed and parseable");

            InteriorKit.Entry cottage = System.Array.Find(contract.rooms, r => r.key == "sageCottage");
            Assert.IsNotNull(cottage, "the pilot room is in it");
            Assert.AreEqual(declared, cottage.storeyHeightMetres, 1e-4,
                "the committed contract's storeyHeightMetres must be the rise the rig's own storeys " +
                "measure — re-bake the interiors kit if this has drifted");

            foreach (InteriorKit.Entry room in contract.rooms)
                Assert.Greater(room.storeyHeightMetres, 2.0f,
                    $"'{room.key}' declares no plausible storey height, so anything standing a second " +
                    "level on it would draw that level over the ground floor (ADR 0036, amended)");
        }

        [Test]
        public void MeasureRegistration_RefusesWhenTheTwoBuildsAreDifferentSizes()
        {
            using IRigScriptHost host = Host("house", "interior");

            // A room dialled a size away from its shell: the exact authoring slip the pilot's whole
            // premise depends on catching, and it draws perfectly.
            Assert.Throws<InvalidOperationException>(() =>
                InteriorRigAzimuthProbe.MeasureRegistration(
                    host, House, "{size:0.25}", Interior, "{size:0.6}", 8));
        }

        // =============================================================================
        //  the silent-option trap
        // =============================================================================

        [Test]
        public void ThePilotRoomActuallyDiffersFromTheRigDefault()
        {
            using IRigScriptHost host = Host("interior");

            byte[] dialled = host.EvaluateBytes($"{Interior}.render(0,{RoomOpts()})");
            byte[] plain = host.EvaluateBytes($"{Interior}.render(0,{{}})");

            Assert.AreEqual(dialled.Length, plain.Length);
            CollectionAssert.AreNotEqual(dialled, plain,
                                         "if the two are byte-identical every dialled key was ignored — " +
                                         "these rigs read opts[k] != null ? opts[k] : fallback, so a " +
                                         "misspelled KEY changes nothing and says nothing");
        }

        [Test]
        public void AMisspelledKeyChangesNothing_WhichIsWhyEveryKeyIsGrepVerified()
        {
            // The worked example on record: winD is the rig's internal field name, winDensity is the
            // option it actually reads. Both strings appear in the rig source; only one does anything —
            // that HALF of the demonstration (the typo is silent) probes winD and stands unchanged.
            //
            // The CONTROL half ("a correctly-spelled option applies") deliberately does NOT use
            // winDensity: on CI both a nudge (0.1) and the extremes (1 vs 0) rendered byte-identical
            // at facing 0 — the density→count mapping (max(1, round((Ln/2.6)·(0.5+winD)))), the
            // default footprint and the facing's visible-wall set conspire so the drawn window count
            // never moved. A control must be UNMISSABLE, so it probes the floor MATERIAL instead:
            // 'stoneFlag' vs the default 'plank' swaps the whole floor field between two different
            // palettes at every facing. winDensity's facing-0 inertness is flagged to the
            // art-director as a rig finding, not silently absorbed here.
            using IRigScriptHost host = Host("interior");

            byte[] stone = host.EvaluateBytes($"{Interior}.render(0,{{floor:'stoneFlag'}})");
            byte[] typo = host.EvaluateBytes($"{Interior}.render(0,{{winD:1}})");
            byte[] plain = host.EvaluateBytes($"{Interior}.render(0,{{}})");

            CollectionAssert.AreNotEqual(stone, plain, "floor is a real option and it applies");
            CollectionAssert.AreEqual(typo, plain,
                                      "winD is ignored in total silence — this is the trap, demonstrated");
        }

        // =============================================================================
        //  the crop and the texture budget — MEASURED, headless, before anything is committed
        // =============================================================================

        [Test]
        public void EveryRoomCropsSmallEnoughToPackUnderTheImportCap()
        {
            foreach (InteriorKit.Build build in InteriorKit.RoomSet)
                AssertPacks(build, isRoom: true);
        }

        [Test]
        public void EveryPropCropsSmallEnoughToPackUnderTheImportCap()
        {
            foreach (InteriorKit.Build build in InteriorKit.PropSet)
                AssertPacks(build, isRoom: false);
        }

        static void AssertPacks(InteriorKit.Build build, bool isRoom)
        {
            InteriorBakeRequest req = InteriorBakeMenu.RequestFor(build);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            InteriorCellSet set = InteriorRigBaker.RenderCells(req, host);

            BuildingRigBaker.ChooseGrid(set.CellWidth, set.CellHeight, req.Facings,
                                        out int cols, out int rows, req.MaxSheetDimension);
            int w = cols * set.CellWidth, h = rows * set.CellHeight;

            // The point of the test is the NUMBER, not the boolean — a reader of the CI log should be
            // able to see the realized budget without baking anything.
            TestContext.WriteLine(
                $"{(isRoom ? "room" : "prop")} '{build.Key}': cropped cell {set.CellWidth}×{set.CellHeight} " +
                $"from {set.Geometry.Width}×{set.Geometry.Height} native " +
                $"({1.0 - (double)set.CellWidth * set.CellHeight / (set.Geometry.Width * (double)set.Geometry.Height):P0} " +
                $"saved) → {cols}×{rows} grid = {w}×{h} px, {w * (long)h * 4 / 1024 / 1024.0:F2} MiB RGBA32.");

            Assert.LessOrEqual(w, InteriorKit.ImportSizeCap,
                               $"'{build.Key}' packs {w} px wide, over the {InteriorKit.ImportSizeCap} px " +
                               "import cap. Over it Unity DOWNSCALES silently and the sprite count still " +
                               "comes out right. Raise InteriorKit.ImportSizeCap — one number, which the " +
                               "pack, the import lock and the verify assert all follow.");
            Assert.LessOrEqual(h, InteriorKit.ImportSizeCap, $"'{build.Key}' packs {h} px tall");
        }

        [Test]
        public void TheCropMovesThePivotWithIt_AndLeavesArtBelowTheFloorCentre()
        {
            InteriorCellSet set = PilotRoomCells();

            Assert.AreEqual(set.Geometry.PivotX - set.CropX, set.PivotX, 1e-6,
                            "the rig reports the pivot in NATIVE cell space; after cropping the same " +
                            "world point sits cropX closer to the origin. Getting this wrong throws " +
                            "nothing — every room simply stands a fixed distance from where it was put.");
            Assert.AreEqual(set.Geometry.PivotY - set.CropY, set.PivotY, 1e-6);

            Assert.Greater(set.PivotX, 0, "and the pivot stays inside the cropped cell");
            Assert.Greater(set.PivotY, 0);
            Assert.Less(set.PivotX, set.CellWidth);
            Assert.Less(set.PivotY, set.CellHeight);

            double belowPx = set.CellHeight - set.PivotY;
            TestContext.WriteLine($"{belowPx:F0} px ({belowPx / 32.0:F2} m) of the room draws BELOW its " +
                                  "floor centre — exactly what a bottom-centre pivot would sink it by.");

            Assert.Greater(belowPx, 8,
                           "the ¾ camera projects the whole near half of the floor below its centre, so " +
                           "there is always art under the pivot. A near-zero value here means the pivot " +
                           "has been quietly treated as bottom-centre.");
        }

        // =============================================================================
        //  the anchors the runtime reads
        // =============================================================================

        [Test]
        public void TheRoomExposesADoorwayAFloorCentreAndAHearth()
        {
            using IRigScriptHost host = Host("interior");
            string o = RoomOpts();

            Assert.IsTrue(host.EvaluateBool($"{Interior}.anchors(0,{o}).door !== undefined"));
            Assert.IsTrue(host.EvaluateBool($"{Interior}.anchors(0,{o}).floor !== undefined"));
            Assert.IsTrue(host.EvaluateBool($"{Interior}.anchors(0,{o}).hearth !== null"),
                          "the pilot room dials hearth:true, tied to the shell's one chimney");

            // The floor anchor IS the pivot: both are the ground centre, which is what makes the room's
            // transform position and its floor point the same thing.
            Assert.AreEqual(host.EvaluateNumber($"{Interior}.pivot.x"),
                            host.EvaluateNumber($"{Interior}.anchors(0,{o}).floor.x"), 1e-6);
            Assert.AreEqual(host.EvaluateNumber($"{Interior}.pivot.y"),
                            host.EvaluateNumber($"{Interior}.anchors(0,{o}).floor.y"), 1e-6);
        }

        [Test]
        public void EveryPropInTheKitIsARealPropName()
        {
            using IRigScriptHost host = Host("interiorProp");
            string known = host.EvaluateString($"{Prop}.list().join(',')");

            foreach (InteriorKit.Build build in InteriorKit.PropSet)
                Assert.IsTrue(host.EvaluateBool($"{Prop}.PROPS['{build.PropName}'] !== undefined"),
                              $"'{build.PropName}' is not a prop of the rig. Known: {known}. " +
                              "(The rig falls back to a CHAIR for an unknown name, so this would bake " +
                              "five chairs under five different names in silence.)");
        }

        [Test]
        public void EveryPropReportsAFootprintTheColliderCanUse()
        {
            using IRigScriptHost host = Host("interiorProp");

            foreach (InteriorKit.Build build in InteriorKit.PropSet)
            {
                string opts = InteriorBakeMenu.OptionsLiteralFor(build);
                double w = host.EvaluateNumber($"{Prop}.footprint('{build.PropName}',{opts}).w");
                double d = host.EvaluateNumber($"{Prop}.footprint('{build.PropName}',{opts}).d");

                Assert.Greater(w, 0.1, $"'{build.Key}' width");
                Assert.Greater(d, 0.1, $"'{build.Key}' depth");
                Assert.Less(w, 4.0, $"'{build.Key}' is wider than the room it goes in");
                Assert.Less(d, 4.0, $"'{build.Key}' is deeper than the room it goes in");
            }
        }
    }
}
