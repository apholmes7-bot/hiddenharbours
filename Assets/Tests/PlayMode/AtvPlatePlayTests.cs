using System.Collections;
using System.Collections.Generic;
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
    /// ⭐⭐ <b>THE TWO PLATES THE OWNER IS OWED</b> — the three machines standing outside the shop, and
    /// the player astride one of them mid-ride. Shot in the REAL committed St Peters, through the game's
    /// own camera, at the shipped exposure.
    ///
    /// <para><b>Why a PlayMode fixture and not a GUI editor session.</b> The plate is evidence, and this
    /// route is strictly better evidence: it loads the region the player loads, finds the machines the
    /// BUILDER placed rather than ones the fixture stood up, renders <c>Camera.main</c> — so the day/night
    /// overlay and every light quad are in the frame, because they are pinned to that camera's own frustum
    /// — and it <b>asserts the thing each plate is supposed to show</b> before saving it. A plate pair that
    /// can silently stop matching its own caption is a picture, not a measurement
    /// (<c>live-editor-plate-recipe</c>).</para>
    ///
    /// <para><b>ONE region load, two plates, and that is deliberate.</b> A mesh machine composites through
    /// the facet buffer and holds a HULL ID out of 255 handed out 1 + 12 at a time, and
    /// <c>IsoFacetHullRegistry.s_NextId</c> never rewinds — so a fixture that loads a region per plate
    /// exhausts the pool and photographs a shop with no machines outside it while every renderer reports
    /// <c>isVisible</c> true (<c>in-frame-is-not-in-the-picture-for-a-mesh-hull</c>, #753). Both plates come
    /// off one load, and each one is refused rather than written if its subject holds facet id 0.</para>
    ///
    /// <para><b>⚠️ The hands do not reach the bars, and that is expected.</b> The <c>drive</c> clip was
    /// baked to a helm 0.315 m forward of the seat and a quad's bars are 0.66 m ahead, so the rider is
    /// <b>11.6–13.9 px</b> short at the hands against 0.68 px on the Otter the clip was fitted to. The
    /// astride stance is an upstream art ask; <c>AtvAstrideRiderTests</c> pins each residual exactly. This
    /// fixture asserts she is ON THE SADDLE — which is the claim the plate makes — and says nothing about
    /// her hands, because that gap is known, measured and somebody else's to close.</para>
    /// </summary>
    public class AtvPlatePlayTests
    {
        const string SceneName = "StPeters";
        const string PlateDir = "AtvPlates";

        /// <summary>Late morning: the shipped daylight exposure, lamps off, so the plate shows the machines
        /// and not a lighting study. <c>WharfNightStage</c> waits for the tint to actually WEAR the hour
        /// before anything is photographed.</summary>
        const float PlateHour = 11f;

        /// <summary>How far she rides before the second plate — far enough to be clear of the row she was
        /// parked in, so the picture reads as a ride rather than as a mount.</summary>
        const float RideMetres = 12f;

        const int MaxSteps = 3000;

        WharfNightStage _stage;
        HeldDriveInput _held;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_stage != null) yield return _stage.TearDown();
            _stage = null;
            _held = null;
        }

        // ---- finding what the builder placed -----------------------------------------------------

        static GameObject Machine(string name) => GameObject.Find(name);

        /// <summary>
        /// ⭐⭐ <b>Does this machine hold a compositing slot?</b> Not "is she in the frustum" and not "is her
        /// renderer enabled" — both are true of a machine the facet pool has run out of room for, and she is
        /// simply not in the picture. Null renderer means she never took the mesh path at all, which for a
        /// vehicle is the same answer: nothing to photograph.
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

        /// <summary>The subject-in-frame law: a plate that saves without asking whether the thing it is
        /// named after is on screen is a picture of somewhere else. Reported with the numbers so a failure
        /// says where the camera actually was.</summary>
        void AssertInFrame(Transform subject, string what)
        {
            Camera cam = _stage.Camera;
            Vector3 v = cam.WorldToViewportPoint(subject.position);
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
        public IEnumerator ThreeMachinesAtTheShop_AndThePlayerAstrideOne()
        {
            // ⚠️ FIRST statement, before any yield — an Assert.Ignore raised after a yield unwinds through
            // the runner and records the case as FAILED with the skip text attached.
            WharfNightStage.RequireAGraphicsDevice();

            _stage = new WharfNightStage(SceneName, PlateDir);
            yield return _stage.Load();
            yield return _stage.SetNight(PlateHour);

            // ---- the row, as the builder placed it ------------------------------------------------
            var placed = new List<GameObject>();
            foreach ((string name, string _) in StPetersMachines.Row)
            {
                GameObject go = Machine(name);
                Assert.IsNotNull(go,
                    $"{name} is not in the committed St Peters scene. The row is placed by " +
                    "StPetersMachines and pinned by StPetersMachinesTests; if that is green and this is " +
                    "not, the scene loaded is not the committed one.");
                placed.Add(go);
            }

            // ⭐ Each one holds a compositing slot, or NO PLATE IS WRITTEN and the reason is named. A plate
            // fixture inside a full suite cannot help meeting an exhausted pool, and writing the file
            // anyway is a lie about what is in it (#753).
            var uncomposited = new List<string>();
            foreach (GameObject go in placed)
                if (!IsComposited(go, out string why)) uncomposited.Add(why);

            // ---- PLATE 1: the three, parked ---------------------------------------------------------
            Vector2 rowCentre = (placed[0].transform.position + placed[2].transform.position) * 0.5f;
            yield return _stage.FrameOn(rowCentre);

            foreach (GameObject go in placed) AssertInFrame(go.transform, go.name);

            if (uncomposited.Count > 0)
            {
                Debug.LogWarning($"[{PlateDir}] NO PLATE WRITTEN for the parked row:\n  " +
                                 string.Join("\n  ", uncomposited));
            }
            else
            {
                _stage.SavePlate("three-machines-at-the-store.png", _stage.Capture());
                Debug.Log($"[{PlateDir}] parked row: " +
                          $"{placed[0].name} {(Vector2)placed[0].transform.position}, " +
                          $"{placed[1].name} {(Vector2)placed[1].transform.position}, " +
                          $"{placed[2].name} {(Vector2)placed[2].transform.position}; " +
                          $"camera {_stage.Camera.transform.position} ortho " +
                          $"{_stage.Camera.orthographicSize:0.00}.");
            }

            // ---- PLATE 2: the player astride, mid-ride ----------------------------------------------
            GameObject quadGo = placed[0];
            var door = quadGo.GetComponent<VehicleDoor>();
            var controller = quadGo.GetComponent<VehicleController>();
            Assert.IsNotNull(door, $"{quadGo.name} has no VehicleDoor — she is scenery, not a machine you " +
                                   "can get on, and the second plate has no subject.");
            Assert.IsNotNull(controller, $"{quadGo.name} has no VehicleController.");

            var switcher = Object.FindFirstObjectByType<ControlSwitcher>();
            var presenter = Object.FindFirstObjectByType<PlayerDrivePresenter>();
            Assert.IsNotNull(switcher, "the region has no ControlSwitcher, so nobody can get on anything.");
            Assert.IsNotNull(presenter, "the player carries no PlayerDrivePresenter, so no rider would be " +
                                        "drawn on her and the plate would be of an empty machine.");

            // The world has to run again to ride: FrameOn stopped engine time for plate 1 (law 1).
            Time.timeScale = 1f;
            yield return null;

            // ⭐⭐ THE SHELL IS HOLDING THE WORLD, AND IT COST A WHOLE RUN TO FIND.
            //
            // St Peters IS the start scene, so a fixture that loads it gets the TITLE PAGE — and
            // `ControlSwitcher.Update` checks `ShellFlow.WorldInputBlocked` FIRST of everything, parks the
            // controls and calls `ReleaseDriveInput()` before it ever reaches the wheel. The machine then
            // sits at full throttle doing nothing, with the land gate reporting dry ground and a cap ABOVE
            // her top speed (measured: dry=True, cap=9.22 m/s against a 9 m/s machine) — which reads
            // exactly like a broken drivetrain and is nothing of the kind.
            //
            // ⚠️ The number that named it was `HeldDriveInput.Reads == 0` over 3000 physics steps: the
            // switcher never ASKED. A fixture that only watched the odometer would have blamed the
            // vehicle. `ShellFlow.Reset()` is the slate wipe, not `StartNewGame()` — that one begins the
            // arrival and overwrites the SHARED savegame.
            Debug.Log($"[{PlateDir}] gates before the ride: shell blocked={ShellFlow.WorldInputBlocked} " +
                      $"(phase {ShellFlow.Phase}, paused {ShellPause.IsPaused}), " +
                      $"interaction blocked={InteractionGate.IsBlocked}, " +
                      $"boarding move={switcher.IsBoardingMove}, mode={switcher.Mode}");
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            InteractionGate.Reset();
            yield return null;

            _held = new HeldDriveInput();
            switcher.ConfigureDriveInput(_held);
            Assert.IsTrue(switcher.TryEnterDriving(door),
                "she could not get on the quad the builder parked. Every gate is re-read in " +
                "TryEnterDriving — a refusal here is the plate's subject refusing to exist.");
            yield return null;

            Assert.IsTrue(presenter.IsShowing,
                "nobody is drawn on her. A quad has no inside to be in, so a hidden rider is a machine " +
                "riding itself — and this plate is supposed to show a person on a saddle.");

            // ⭐ WHAT THE LAND GATE SAYS BEFORE SHE IS ASKED TO MOVE. A road vehicle's speed is capped at
            // what she could still stop from before her leading wheel leaves dry ground, so a machine that
            // will not move on apparently good grass is asking a terrain question, not a drivetrain one —
            // and the answer belongs in the log whether she moves or not.
            VehicleMeshDef qm = controller.Vehicle.Mesh;
            Vector2 at = quadGo.transform.position;
            float cap = VehicleGrounding.SpeedCapNow(
                at, quadGo.transform.up, controller.Vehicle.MaxSpeedMetersPerSecond,
                qm.FrontAxleY, qm.RearAxleY, controller.Vehicle.BrakingMetersPerSecondSquared,
                qm.WheelRadiusMeters);
            Debug.Log($"[{PlateDir}] land gate at {at}: dry={VehicleGrounding.IsDryLandNow(at)}, " +
                      $"cap={cap:0.##} m/s (top {controller.Vehicle.MaxSpeedMetersPerSecond:0.#}); " +
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
            _held.Set(0f, 0f, false);
            float rode = controller.OdometerMeters - odometerAt;

            // ⭐ SHE IS ON THE SADDLE — the claim the plate makes, asserted against the machine's OWN live
            // seat rather than against her root. Nothing here asks about her HANDS: the drive clip's reach
            // is 11.6–13.9 px short of these bars, that is measured, pinned and upstream.
            Vector2 seat = door.DriverSeatWorldPosition;
            GameObject playerGo = presenter.gameObject;
            float offSaddle = Vector2.Distance(playerGo.transform.position, seat);
            Assert.That(offSaddle, Is.LessThan(0.6f),
                $"the rider is {offSaddle:0.##} m off the saddle, so the plate would show her beside the " +
                "machine rather than on it.");

            yield return _stage.FrameOn(quadGo.transform.position);

            AssertInFrame(quadGo.transform, quadGo.name);
            AssertInFrame(playerGo.transform, "the rider");

            if (!IsComposited(quadGo, out string quadWhy))
            {
                Debug.LogWarning($"[{PlateDir}] NO PLATE WRITTEN for the ride: {quadWhy}");
            }
            else
            {
                _stage.SavePlate("player-astride-mid-ride.png", _stage.Capture());
                Debug.Log($"[{PlateDir}] ride: covered {rode:0.0} m at " +
                          $"{controller.SpeedMetersPerSecond:0.0} m/s; rider {offSaddle:0.000} m off the " +
                          $"saddle at {(Vector2)playerGo.transform.position}; machine at " +
                          $"{(Vector2)quadGo.transform.position}.");
            }

            // Give the wheel back so the region is not left with a player welded to a quad.
            switcher.LeaveDriving();
            yield return null;

            // ⚠️ ASSERTED LAST, AFTER THE PLATE IS SAFE. The picture of a rider on a saddle is worth
            // having either way — it is what the second plate is FOR — and losing it to this assertion
            // would trade the evidence for the complaint. The log line above names which of the three
            // possible causes it is: no demand reaching the seat, a land gate refusing the ground, or a
            // drivetrain that turns a demand into nothing.
            Assert.That(rode, Is.GreaterThanOrEqualTo(RideMetres - 0.5f),
                $"⚠️ SHE DID NOT RIDE. {steps} physics steps at full throttle covered {rode:0.##} m in " +
                "the COMMITTED St Peters — while the same machine rides 30 m in a fixture-built scene " +
                "(AtvPlayerRidePlayTests). That difference is the finding, not this fixture: #774 says " +
                "the player can ride one, and in the region she is actually in she cannot.");
        }
    }
}
