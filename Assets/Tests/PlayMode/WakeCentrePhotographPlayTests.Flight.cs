using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    // A hull leaves the water and lands (the owner, 2026-11-12: "Boats also need airborn capability with
    // impact and splashing if conditions/waves/speed would cause that. Water should react in realism").
    // The outboard dory is held at a speed she does not make on her own, straight into a forced strong sea,
    // and photographed twice through the game's own camera at its shipped zoom: once with air under her
    // keel, and once a moment after she lands.
    //
    // Assertions are on the instrument only: she was reported airborne, and a landing was reported. The
    // look is for the plates.
    public partial class WakeCentrePhotographPlayTests
    {
        const string FlightHullPath = "Assets/_Project/Data/Boats/DoryOutboard.asset";
        const float FlightSpeed = 16f;
        const float FlightSea01 = 0.8f;
        const float FlightSecondsPerTry = 7f;
        /// <summary>Air under her keel, metres, before the flight plate is taken.</summary>
        const float FlightPlateClearance = 0.35f;
        /// <summary>Seconds after the landing is reported before the splash plate is taken.</summary>
        const float SplashPlateDelay = 0.10f;
        /// <summary>Half the height of the frame both plates are taken at, metres.</summary>
        const float FlightPlateOrtho = 6f;

        int _flightLandings;
        float _flightLastImpact, _flightLandedAt;

        void OnFlightLanded(HullLanded e)
        {
            _flightLandings++;
            _flightLastImpact = e.Impact01;
            _flightLandedAt = Time.time;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator ASmallHull_HeldAtSpeedIntoAStrongSea_IsPhotographedInTheAirAndLanding()
        {
            RequireAGraphicsDevice();   // FIRST, before any yield
            BoatHullDef hull = LoadAsset<BoatHullDef>(FlightHullPath);
            BoatVisualDef visual = LoadAsset<BoatVisualDef>(DoryVisualPath);
            if (hull == null || visual == null) yield break;

            yield return LoadRegion();
            yield return SetHour(ShotHour);

            var weather = GameServices.Environment as HiddenHarbours.Environment.EnvironmentService;
            if (weather == null)
            {
                Assert.Ignore("SKIPPED, NOT VERIFIED: the region's weather is not the EnvironmentService, so " +
                              "no strong sea can be asked for.");
                yield break;
            }
            const int Tries = 3;
            if (!TryLegAnchors(Tries, out List<Vector2> anchors, out float depth))
            {
                Assert.Ignore("SKIPPED, NOT VERIFIED: no open water inside " + SceneName +
                              "'s camera bounds deep enough for the runs.");
                yield break;
            }

            bool wasForced = weather.DevForceSeaState;
            float wasSea = weather.DevSeaState01;
            weather.DevForceSeaState = true;
            weather.DevSeaState01 = FlightSea01;
            EventBus.Subscribe<HullLanded>(OnFlightLanded);
            bool gotFlight = false, gotSplash = false;
            try
            {
                for (int i = 0; i < anchors.Count && !(gotFlight && gotSplash); i++)
                {
                    Vector2 wind = GameServices.Environment.Sample().WindVector;
                    Vector2 into = wind.sqrMagnitude > 1e-6f ? -wind.normalized : Vector2.up;
                    float headingDeg = Mathf.Atan2(into.x, into.y) * Mathf.Rad2Deg;
                    Spawned she = Spawn("flight", hull, visual, false, new Leg("try" + i, headingDeg, 0f), anchors[i]);
                    GameObject go = she.Go;

                    var run = new Bounds(new Vector3(anchors[i].x, anchors[i].y, 0f), Vector3.one * 30f);
                    yield return FrameOn(go.transform, run);

                    _flightLandings = 0;
                    float peak = 0f;
                    bool airborneSeen = false;
                    for (float elapsed = 0f; elapsed < FlightSecondsPerTry && !(gotFlight && gotSplash); )
                    {
                        elapsed += Time.deltaTime;
                        go.transform.rotation = Quaternion.Euler(0f, 0f, -headingDeg);
                        she.Rb.linearVelocity = (Vector2)go.transform.up * FlightSpeed;
                        she.Rb.angularVelocity = 0f;
                        airborneSeen |= she.Boat.IsAirborne;
                        peak = Mathf.Max(peak, she.Boat.FlightClearanceMeters);

                        if (!gotFlight && she.Boat.IsAirborne && she.Boat.FlightClearanceMeters >= FlightPlateClearance)
                        {
                            yield return ShootFlightPlate(she, "flight-in-the-air.png",
                                $"in the air, {she.Boat.FlightClearanceMeters.ToString("0.00", CultureInfo.InvariantCulture)} m under her keel");
                            gotFlight = true;
                        }
                        else if (gotFlight && !gotSplash && _flightLandings > 0
                                 && Time.time - _flightLandedAt >= SplashPlateDelay)
                        {
                            yield return ShootFlightPlate(she, "flight-landing-splash.png",
                                $"{SplashPlateDelay.ToString("0.00", CultureInfo.InvariantCulture)} s after a landing of impact " +
                                _flightLastImpact.ToString("0.00", CultureInfo.InvariantCulture));
                            gotSplash = true;
                        }
                        if (!gotFlight) _flightLandings = 0;   // only a landing after the flight plate counts
                        yield return null;
                    }

                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[{0}] flight/try{1}: from ({2:0.0}, {3:0.0}) heading {4:0} deg at {5:0.0} m/s, sea {6:0.00}, wind " +
                        "({7:0.0}, {8:0.0}) m/s; airborne seen: {9}; most air {10:0.00} m; flight plate: {11}; splash plate: {12}.",
                        PlateDir, i, anchors[i].x, anchors[i].y, headingDeg, FlightSpeed, FlightSea01, wind.x, wind.y,
                        airborneSeen, peak, gotFlight, gotSplash));

                    Time.timeScale = 1f;
                    _spawned.Remove(go);
                    Object.Destroy(go);
                    yield return null;
                }
            }
            finally
            {
                EventBus.Unsubscribe<HullLanded>(OnFlightLanded);
                weather.DevForceSeaState = wasForced;
                weather.DevSeaState01 = wasSea;
                Time.timeScale = 1f;
            }

            Debug.Log($"[{PlateDir}] flight: {depth.ToString("0.0", CultureInfo.InvariantCulture)} m under her at the " +
                      "shallowest anchor. The dory is HELD at this speed by the test; she does not make it herself.");
            Assert.IsTrue(gotFlight, "She was never reported airborne with " + FlightPlateClearance +
                                     " m under her keel, so there is no flight to photograph.");
            Assert.IsTrue(gotSplash, "No landing was reported after the flight plate, so there is no splash to photograph.");
        }

        // Stop the clock, put the game's own camera over her at close range, and photograph every foam family.
        IEnumerator ShootFlightPlate(Spawned she, string file, string what)
        {
            Time.timeScale = 0f;
            Vector3 was = _cam.transform.position;
            float wasOrtho = _cam.orthographicSize;
            Vector3 at = she.Go.transform.position;
            _cam.transform.position = new Vector3(at.x, at.y, was.z);
            _cam.orthographicSize = FlightPlateOrtho;
            for (int i = 0; i < 2; i++) yield return null;

            Transform wakeRoot = FindWakeRoot(she.Go.name);
            if (wakeRoot != null && she.Injector != null)
            {
                SavePlate(file, ShootArm(wakeRoot, she.Injector, sheet: true, foam: true, crest: true));
                RestoreRig(wakeRoot, she.Injector);
            }
            else SavePlate(file, Capture());

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[{0}] flight plate {1}: {2}; shot from ({3:0.00}, {4:0.00}), {5:0.00} m tall, {6}x{7} px.",
                PlateDir, file, what, at.x, at.y, FlightPlateOrtho * 2f, _w, _h));

            _cam.transform.position = was;
            _cam.orthographicSize = wasOrtho;
            Time.timeScale = 1f;
        }
    }
}
