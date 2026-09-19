using System.Collections;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>HER TWO PLATES</b> — the Modern 3500 parked in the Nine Mile Creek truck park, and the
    /// Modern 3500 driven off it. Shot in the REAL committed Nine Mile Creek, through the game's own camera,
    /// at the shipped exposure, off ONE region load: <see cref="AtvPlatePlayTests"/>' pattern and its reason.
    /// She composites through the facet buffer, the pool of 255 hull ids never rewinds, and each plate is
    /// refused rather than written if she holds facet id 0.
    ///
    /// <para><b>⚠️ The Dually is not in the parked plate, and that is his timetable, not a fault.</b> She
    /// stands in the park's WEST bay beside his (D2), but his <c>ScheduledTrip</c> (<c>FishBuyerRun.asset</c>)
    /// takes him out before sunrise and brings him home after sunset, so at every daylight hour his bay is
    /// empty and he is on the buyers' gravel. The plate is shot at noon for HER, because the badge cut (D1)
    /// only reads by day. It is framed on the pair of bays, and where he is is logged, never asserted.</para>
    ///
    /// <para><b>⚠️ Nobody is drawn at her wheel, and that IS asserted.</b> Her sidecar's drive interaction is
    /// at <c>door_fl</c>, which is not a seat it lists in the open. The reader gives her no driver seat,
    /// exactly as it gives the Dually none, so the bake leaves <c>DriverSeatLocal</c> at zero and
    /// <see cref="PlayerDrivePresenter"/> draws nobody at a cab's wheel. The driven plate is proved by her
    /// odometer and by the switcher holding HER seat, not by a figure.</para>
    ///
    /// <para><b>Why north, and why she brakes before the second plate.</b> Her nose is <c>transform.up</c>,
    /// north. The ground from her tail to 40 m past her nose was sampled headless through the region's own
    /// terrain composition: flat 6 m land, 3.8 m above spring high water, with the nearest wet ground 59.5 m
    /// east. The region's channels cut only seabed below their −1.6 m ceiling and cannot reach it, so the ride
    /// cannot meet the land gate. And <c>FrameOn</c> settles the camera with engine time RUNNING (up to 400
    /// frames), so a truck still coasting from ~10 m/s would leave the anchor behind. She is braked to a stop
    /// first, the way <c>RoadFleetJourneyPlayTests</c> parks every machine.</para>
    /// </summary>
    public class Modern3500PlatePlayTests
    {
        const string SceneName = "NineMileCreek";
        const string PlateDir = "Modern3500Plates";

        /// <summary>Noon: the hour this region's daylight plates are shot at, lamps off, and the hour her grille
        /// reads — which is what a plate after a badge cut is for.</summary>
        const float PlateHour = 12f;

        /// <summary>How far she is driven before the second plate: the ATV's ride, which is more than her own
        /// 7 m length, so the picture reads as a truck that has gone somewhere rather than one nudged.</summary>
        const float RideMetres = 12f;

        /// <summary>How far she may stand from the builder's bay once the region is up. The parked plate is a
        /// claim about WHERE she is parked, so it is asserted before it is shot.</summary>
        const float ParkedDriftMetres = 0.05f;

        /// <summary>Stopped, as <c>RoadFleetJourneyPlayTests.Park</c> reads it.</summary>
        const float StoppedMetresPerSecond = 0.05f;

        const int MaxSteps = 3000;
        const int MaxStepsToStop = 400;

        WharfNightStage _stage;
        HeldDriveInput _held;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_stage != null) yield return _stage.TearDown();
            _stage = null;
            _held = null;
        }

        /// <summary>
        /// Does she hold a compositing slot? Not "is she in the frustum" and not "is her renderer enabled":
        /// both are true of a truck the facet pool has run out of room for, and she is simply not in the
        /// picture (<see cref="AtvPlatePlayTests"/>, #753).
        /// </summary>
        static bool IsComposited(GameObject go, out string why)
        {
            var facet = go != null ? go.GetComponentInChildren<IsoFacetHullRenderer>(true) : null;
            if (facet == null)
            {
                why = $"{(go == null ? "(missing)" : go.name)} has no IsoFacetHullRenderer — she never took " +
                      "the mesh path, so there is no picture of her to photograph.";
                return false;
            }

            if (facet.HullId <= 0)
            {
                why = $"{go.name} holds facet id 0 — the compositing pool is exhausted, so she is in the " +
                      "frustum and NOT in the picture.";
                return false;
            }

            why = null;
            return true;
        }

        /// <summary>The subject-in-frame law, for a point: a plate that saves without asking whether the thing
        /// it is named after is on screen is a picture of somewhere else.</summary>
        void AssertInFrame(Vector3 world, string what)
        {
            Camera cam = _stage.Camera;
            Vector3 v = cam.WorldToViewportPoint(world);
            Assert.That(v.x, Is.InRange(0.03f, 0.97f),
                $"{what} is off the plate horizontally at viewport ({v.x:0.000}, {v.y:0.000}); camera at " +
                $"{cam.transform.position}, ortho {cam.orthographicSize:0.00}, aspect {cam.aspect:0.000}.");
            Assert.That(v.y, Is.InRange(0.03f, 0.97f),
                $"{what} is off the plate vertically at viewport ({v.x:0.000}, {v.y:0.000}); camera at " +
                $"{cam.transform.position}, ortho {cam.orthographicSize:0.00}, aspect {cam.aspect:0.000}.");
        }

        // =============================================================================================
        //  THE PLATES
        // =============================================================================================

        [UnityTest]
        public IEnumerator SheStandsInHerBayBesideHis_AndSheIsDrivenOffThePark()
        {
            // ⚠️ FIRST statement, before any yield — an Assert.Ignore raised after a yield unwinds through
            // the runner and records the case as FAILED with the skip text attached.
            WharfNightStage.RequireAGraphicsDevice();

            _stage = new WharfNightStage(SceneName, PlateDir);
            yield return _stage.Load();
            yield return _stage.SetNight(PlateHour);

            // ---- her, as the builder placed her ------------------------------------------------------
            GameObject her = GameObject.Find(NineMileCreekTruckPark.Modern3500Name);
            Assert.IsNotNull(her,
                $"{NineMileCreekTruckPark.Modern3500Name} is not in the committed Nine Mile Creek. " +
                "NineMileCreekTruckPark.PlaceModern3500 parks her and NineMileCreekTruckParkTests pins it; if " +
                "those are green and this is not, the scene was not rebuilt through its builder after her bake.");

            Vector2 bay = NineMileCreekMainland.Modern3500ParkPos;
            Vector2 parkedAt = her.transform.position;
            Assert.That(Vector2.Distance(parkedAt, bay), Is.LessThan(ParkedDriftMetres),
                $"she stands at {parkedAt}, {Vector2.Distance(parkedAt, bay):0.###} m from her bay {bay}, so the " +
                "parked plate would be of somewhere the builder did not put her.");

            GameObject him = GameObject.Find(NineMileCreekTruckPark.TruckName);
            Rect park = NineMileCreekRoads.TruckParkArea();
            Debug.Log($"[{PlateDir}] the Dually at {PlateHour:0.##}h: " +
                      (him == null
                          ? "NOT in the scene"
                          : $"at {(Vector2)him.transform.position}, " +
                            (park.Contains(him.transform.position)
                                ? "home in the park"
                                : "away from the park on the buyer's run")));

            // ---- PLATE 1: her, parked in the bay beside his -------------------------------------------
            Vector2 hisBay = NineMileCreekMainland.TruckParkPos;
            yield return _stage.FrameOn((parkedAt + hisBay) * 0.5f);

            AssertInFrame(her.transform.position, her.name);
            AssertInFrame(hisBay, "the Dually's bay");

            if (!IsComposited(her, out string parkedWhy))
            {
                Debug.LogWarning($"[{PlateDir}] NO PLATE WRITTEN for her parked: {parkedWhy}");
            }
            else
            {
                _stage.SavePlate("modern3500-parked-beside-the-duallys-bay.png", _stage.Capture());
                Debug.Log($"[{PlateDir}] parked: {her.name} at {parkedAt}, the Dually's bay at {hisBay}; " +
                          $"camera {_stage.Camera.transform.position} ortho {_stage.Camera.orthographicSize:0.00}.");
            }

            // ---- PLATE 2: her, driven off the park ----------------------------------------------------
            var door = her.GetComponent<VehicleDoor>();
            var controller = her.GetComponent<VehicleController>();
            Assert.IsNotNull(door, $"{her.name} has no VehicleDoor — she is scenery, not a truck anybody can " +
                                   "drive, and the second plate has no subject.");
            Assert.IsNotNull(controller, $"{her.name} has no VehicleController.");
            Assert.IsNotNull(controller.Vehicle, $"{her.name}'s controller carries no VehicleDef.");
            Assert.IsNotNull(controller.Vehicle.Mesh,
                $"{her.name}'s def carries no baked mesh — she was placed before her bake, so nothing below " +
                "would be about the truck the player drives.");

            var switcher = Object.FindFirstObjectByType<ControlSwitcher>();
            Assert.IsNotNull(switcher, "the region has no ControlSwitcher, so nobody can drive anything.");
            var presenter = Object.FindFirstObjectByType<PlayerDrivePresenter>();

            // The world has to run again to drive: FrameOn stopped engine time for plate 1.
            Time.timeScale = 1f;
            yield return null;

            // The shell gate, exactly as AtvPlatePlayTests found it: a region loaded as a run's first scene can
            // come up behind the title page, and ControlSwitcher.Update releases the wheel before it reads it.
            // `ShellFlow.Reset()` is the slate wipe; `StartNewGame()` would overwrite the SHARED savegame.
            Debug.Log($"[{PlateDir}] gates before the drive: shell blocked={ShellFlow.WorldInputBlocked} " +
                      $"(phase {ShellFlow.Phase}, paused {ShellPause.IsPaused}), " +
                      $"interaction blocked={InteractionGate.IsBlocked}, " +
                      $"boarding move={switcher.IsBoardingMove}, mode={switcher.Mode}");
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            InteractionGate.Reset();
            yield return null;

            _held = new HeldDriveInput();
            switcher.ConfigureDriveInput(_held);
            Assert.IsTrue(switcher.TryEnterDriving(door),
                "she could not be driven from the bay the builder parked her in. Every gate is re-read in " +
                "TryEnterDriving — a refusal here is the second plate's subject refusing to exist.");
            yield return null;

            Assert.That(switcher.Mode, Is.EqualTo(ControlMode.Driving),
                "the wheel was taken and the mode is not Driving.");
            Assert.That(switcher.DrivenSeat, Is.SameAs(door), "the wheel taken is not hers.");

            // Read NOW, asserted LAST: a plate of a driver drawn on a hard cab is evidence worth keeping.
            bool showsDriver = door.ShowsDriver;
            bool driverDrawn = presenter != null && presenter.IsShowing;
            Debug.Log($"[{PlateDir}] at her wheel: her seat shows a driver={showsDriver}; the drive presenter " +
                      $"{(presenter == null ? "is ABSENT" : presenter.IsShowing ? "is drawing one" : "draws nobody")}.");

            // ⭐ WHAT THE LAND GATE SAYS BEFORE SHE IS ASKED TO MOVE (AtvPlatePlayTests' log, for the same
            // reason): a truck that will not move on good ground is asking a terrain question, not a drivetrain
            // one. Read through the overload VehicleController itself calls, dispatched on her kind.
            VehicleDef def = controller.Vehicle;
            VehicleMeshDef mesh = def.Mesh;
            float cap = VehicleGrounding.SpeedCapNow(
                def.Kind, parkedAt, her.transform.up, def.MaxSpeedMetersPerSecond,
                mesh.FrontAxleY, mesh.RearAxleY, def.BrakingMetersPerSecondSquared,
                mesh.WheelRadiusMeters);
            Debug.Log($"[{PlateDir}] land gate at {parkedAt}: kind={def.Kind}, " +
                      $"dry={VehicleGrounding.IsDryLandNow(parkedAt)}, " +
                      $"cap={cap:0.##} m/s (top {def.MaxSpeedMetersPerSecond:0.#}, accel " +
                      $"{def.AccelerationMetersPerSecondSquared:0.#}, braking {def.BrakingMetersPerSecondSquared:0.#}); " +
                      $"terrain={(GameServices.TidalTerrain == null ? "NONE" : GameServices.TidalTerrain.GetType().Name)}, " +
                      $"environment={(GameServices.Environment == null ? "NONE" : GameServices.Environment.GetType().Name)}, " +
                      $"clock={(GameServices.Clock == null ? "NONE" : GameServices.Clock.TotalSeconds.ToString("0.0"))}");

            float odometerAt = controller.OdometerMeters;
            _held.Set(1f, 0f, false);
            int steps = 0;
            while (controller.OdometerMeters - odometerAt < RideMetres && steps < MaxSteps)
            {
                steps++;
                yield return new WaitForFixedUpdate();
                if (steps == 60 || steps == MaxSteps)
                    Debug.Log($"[{PlateDir}] step {steps}: reads={_held.Reads}, " +
                              $"throttle={controller.Throttle:0.00}, " +
                              $"speed={controller.SpeedMetersPerSecond:0.000} m/s, " +
                              $"odometer +{controller.OdometerMeters - odometerAt:0.###} m, " +
                              $"afloat={controller.IsAfloat}, timeScale={Time.timeScale:0.0}");
            }
            float rode = controller.OdometerMeters - odometerAt;

            // Braked to a stop BEFORE the plate (class note): FrameOn settles with engine time running.
            _held.Set(0f, 0f, true);
            int braking = 0;
            while (Mathf.Abs(controller.SpeedMetersPerSecond) > StoppedMetresPerSecond && braking < MaxStepsToStop)
            {
                braking++;
                yield return new WaitForFixedUpdate();
            }
            float speedAtRest = Mathf.Abs(controller.SpeedMetersPerSecond);
            _held.Release();
            yield return null;

            Vector2 droveTo = her.transform.position;
            Debug.Log($"[{PlateDir}] driven: {rode:0.##} m on the throttle in {steps} steps, then " +
                      $"{controller.OdometerMeters - odometerAt - rode:0.##} m on the brake in {braking} steps; " +
                      $"at rest {speedAtRest:0.###} m/s at {droveTo}, {Vector2.Distance(parkedAt, droveTo):0.##} m " +
                      $"from her bay; dry={VehicleGrounding.IsDryLandNow(droveTo)}, afloat={controller.IsAfloat}.");

            yield return _stage.FrameOn(droveTo);

            AssertInFrame(her.transform.position, her.name);

            if (!IsComposited(her, out string drivenWhy))
            {
                Debug.LogWarning($"[{PlateDir}] NO PLATE WRITTEN for her driven: {drivenWhy}");
            }
            else
            {
                _stage.SavePlate("modern3500-driven-off-the-park.png", _stage.Capture());
                Debug.Log($"[{PlateDir}] driven plate: {her.name} at {(Vector2)her.transform.position}; " +
                          $"camera {_stage.Camera.transform.position} ortho {_stage.Camera.orthographicSize:0.00}.");
            }

            // Give the wheel back so the region is not left with a player shut in her cab.
            switcher.LeaveDriving();
            yield return null;

            // ⚠️ ASSERTED LAST, AFTER THE PLATES ARE SAFE — AtvPlatePlayTests' order and its reason: the
            // pictures are worth having either way, and losing them to an assertion trades the evidence for
            // the complaint.
            Assert.That(rode, Is.GreaterThanOrEqualTo(RideMetres - 0.5f),
                $"⚠️ SHE WAS NOT DRIVEN. {steps} physics steps at full throttle covered {rode:0.##} m in the " +
                "COMMITTED Nine Mile Creek, over ground sampled 3.8 m above spring high water. The logs above " +
                "name which it is: no demand reaching her seat (reads), a land gate refusing the ground " +
                "(dry/cap), or a drivetrain turning a demand into nothing (throttle/speed).");
            Assert.That(speedAtRest, Is.LessThan(StoppedMetresPerSecond),
                $"on the brake for {MaxStepsToStop} steps and still doing {speedAtRest:0.##} m/s, so the second " +
                "plate was of a truck the camera was chasing.");
            Assert.IsFalse(showsDriver,
                "her seat shows a driver, so her baked def carries a DriverSeatLocal — but her sidecar publishes " +
                "no seat in the open (its drive interaction is door_fl, a cab door, as on the Dually), so " +
                "something between her sidecar and her bake has changed.");
            Assert.IsFalse(driverDrawn,
                "a hard cab is drawing a driver — a machine whose seats are inside a cab publishes none and " +
                "keeps the driver hidden (PlayerDrivePresenter).");
        }
    }
}
