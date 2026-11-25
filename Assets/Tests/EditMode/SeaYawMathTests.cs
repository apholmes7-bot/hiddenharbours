using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The sea yaw (<see cref="SeaYawMath"/>): the wind and the sea turn a hull's head. Guards the owner's
    /// promises: nothing turns in calm or sheltered water; a light hull is turned more than a heavy one;
    /// the rudder beats the sea at cruise on every shipped hull and the autopilot still holds; a
    /// following sea makes the stern wander; and the whole thing is a pure function of the sim's
    /// (worldSeed, gameTime) wind and waves.
    /// </summary>
    public class SeaYawMathTests
    {
        private const string BoatsFolder = "Assets/_Project/Data/Boats";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        private const float Gravity = 9.81f;

        private static SeaYawSettings S => SeaYawSettings.Default;

        private static List<BoatHullDef> ShippedHulls()
            => AssetDatabase.FindAssets("t:BoatHullDef", new[] { BoatsFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<BoatHullDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(h => h != null && SeaYawMath.FullHelmTorque(h) > 0f && h.MassKg > 0f)
                .ToList();

        private static BoatHullDef Shipped(string id)
        {
            BoatHullDef hull = ShippedHulls().FirstOrDefault(h => h.Id == id);
            Assert.IsNotNull(hull, $"premise: the shipped hull {id} is in {BoatsFolder}");
            return hull;
        }

        private static GameConfig ShippedConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(config, $"premise: the shipped config is at {ConfigPath}");
            return config;
        }

        private static Vector2 Bow(float mathAngleDegrees)
            => new Vector2(Mathf.Cos(mathAngleDegrees * Mathf.Deg2Rad), Mathf.Sin(mathAngleDegrees * Mathf.Deg2Rad));

        private static float WindShareFor(BoatHullDef hull, Vector2 wind, Vector2 bow, in SeaYawSettings s)
        {
            float bias = SeaYawMath.WindYawBias(false, hull.HasSailPlan, false, false, hull.SailWeatherHelm, in s);
            return SeaYawMath.WindShare(wind, bow, bias, SeaYawMath.WindSusceptibility(hull.WindExposure, hull.MassKg, in s), in s);
        }

        private static WaveTrains OneTrain(Vector2 travel, float wavelength, float amplitude)
        {
            var train = new WaveTrain(travel, wavelength, amplitude, 0f, Gravity);
            return new WaveTrains(train, default, default, default, 1, 1f);
        }

        // =============================================================================================
        //  the shipped tuning
        // =============================================================================================

        [Test]
        public void TheShippedConfigCarriesTheCodeDefaults()
        {
            SeaYawSettings shipped = ShippedConfig().SeaYaw;
            SeaYawSettings code = SeaYawSettings.Default;
            Assert.AreEqual(code.Enabled, shipped.Enabled, "Enabled");
            Assert.AreEqual(code.CalmWindMetresPerSecond, shipped.CalmWindMetresPerSecond, "CalmWindMetresPerSecond");
            Assert.AreEqual(code.ReferenceWindMetresPerSecond, shipped.ReferenceWindMetresPerSecond, "ReferenceWindMetresPerSecond");
            Assert.AreEqual(code.WindHelmShare, shipped.WindHelmShare, "WindHelmShare");
            Assert.AreEqual(code.HullWindYawBias, shipped.HullWindYawBias, "HullWindYawBias");
            Assert.AreEqual(code.AnchoredWindYawBias, shipped.AnchoredWindYawBias, "AnchoredWindYawBias");
            Assert.AreEqual(code.WaveHelmShare, shipped.WaveHelmShare, "WaveHelmShare");
            Assert.AreEqual(code.ReferenceWaveSlope, shipped.ReferenceWaveSlope, "ReferenceWaveSlope");
            Assert.AreEqual(code.WaveSpanWavelengthFraction, shipped.WaveSpanWavelengthFraction, "WaveSpanWavelengthFraction");
            Assert.AreEqual(code.ReferenceMassKg, shipped.ReferenceMassKg, "ReferenceMassKg");
            Assert.AreEqual(code.MassExponent, shipped.MassExponent, "MassExponent");
            Assert.AreEqual(code.ReferenceWindExposure, shipped.ReferenceWindExposure, "ReferenceWindExposure");
            Assert.AreEqual(code.MaxSusceptibility, shipped.MaxSusceptibility, "MaxSusceptibility");
            Assert.AreEqual(code.MaxHelmShare, shipped.MaxHelmShare, "MaxHelmShare");
        }

        [Test]
        public void TheCapSitsUnderTheAutopilotsHelm()
        {
            GameConfig config = ShippedConfig();
            Assert.Less(config.SeaYaw.MaxHelmShare, config.HelmAutopilot.MaxHelm,
                "the sea may never ask for more helm than the autopilot is allowed to give.");
            Assert.Less(config.SeaYaw.MaxHelmShare, 1f, "the sea may never ask for her whole helm.");
        }

        [Test]
        public void OnlyTheSloopsCarryWeatherHelm()
        {
            foreach (BoatHullDef hull in ShippedHulls())
            {
                if (hull.HasSailPlan)
                    Assert.Greater(hull.SailWeatherHelm, 0f, $"{hull.Id}: a sailing hull carries weather helm.");
                else
                    Assert.AreEqual(0f, hull.SailWeatherHelm, $"{hull.Id}: weather helm is a sail term only.");
            }
        }

        // =============================================================================================
        //  calm, shelter, off: nothing turns
        // =============================================================================================

        [Test]
        public void CalmAirsAndAGlassSeaTurnNothing_AtEveryHeading()
        {
            BoatHullDef dory = Shipped("boat.dory");
            Vector2 lightAir = new Vector2(S.CalmWindMetresPerSecond * 0.99f, 0f);
            Vector2 glassSlope = Vector2.zero;
            for (int deg = 0; deg < 360; deg += 15)
            {
                Vector2 bow = Bow(deg);
                float wind = WindShareFor(dory, lightAir, bow, S);
                float wave = SeaYawMath.WaveShare(glassSlope, glassSlope, bow, 1f, S);
                Assert.AreEqual(0f, wind, $"heading {deg}: light airs under the calm threshold turned her.");
                Assert.AreEqual(0f, wave, $"heading {deg}: a glass sea turned her.");
                Assert.AreEqual(0f, SeaYawMath.Combine(wind, wave, 1f, S));
            }
        }

        [Test]
        public void AGlassSeaFromTheRealWaveLawHasNoSlopeToTurnWith()
        {
            WaveTrains glass = WaveMath.TrainsFrom(Vector2.zero, 0f, WaveFieldSettings.Default);
            Vector2 pos = new Vector2(12f, -3f);
            Vector2 bow = Bow(30f);
            float half = SeaYawMath.WaveHalfSpan(4.5f, Mathf.Max(1f, glass.Dominant.Wavelength), S);
            Vector2 bowSlope = WaveMath.Sample(pos + bow * half, 100.0, in glass).Slope;
            Vector2 sternSlope = WaveMath.Sample(pos - bow * half, 100.0, in glass).Slope;
            Assert.AreEqual(0f, SeaYawMath.WaveShare(bowSlope, sternSlope, bow, 1f, S),
                "a dead-calm sea built by the wave law must turn nothing.");
        }

        [Test]
        public void ShelterAndTheMasterSwitchTurnNothing_EvenInAGale()
        {
            BoatHullDef dory = Shipped("boat.dory");
            float gale = WindShareFor(dory, new Vector2(0f, 25f), Bow(0f), S);
            Assert.AreNotEqual(0f, gale, "premise: a beam gale turns an exposed dory.");
            Assert.AreEqual(0f, SeaYawMath.Combine(gale, 0.3f, 0f, S), "sheltered water (exposure 0) turned her.");

            SeaYawSettings off = S;
            off.Enabled = false;
            Assert.AreEqual(0f, SeaYawMath.Combine(gale, 0.3f, 1f, off), "the master switch off still turned her.");
        }

        [Test]
        public void AWindDeadAheadOrAsternTurnsNothing()
        {
            BoatHullDef dory = Shipped("boat.dory");
            Vector2 bow = Bow(90f);
            Assert.AreEqual(0f, WindShareFor(dory, bow * 15f, bow, S), 1e-6f, "wind dead astern");
            Assert.AreEqual(0f, WindShareFor(dory, -bow * 15f, bow, S), 1e-6f, "wind dead ahead");
        }

        // =============================================================================================
        //  which way she turns
        // =============================================================================================

        [Test]
        public void ABeamWindBlowsAnUnsailedBowOffDownwind()
        {
            BoatHullDef punt = Shipped("boat.punt");
            Vector2 bowNorth = Bow(90f);
            // Wind blowing towards the west (downwind lies to port of a north-pointing bow): the bow
            // swings to port, counter-clockwise, positive.
            float toWest = WindShareFor(punt, new Vector2(-12f, 0f), bowNorth, S);
            float toEast = WindShareFor(punt, new Vector2(12f, 0f), bowNorth, S);
            Assert.Greater(toWest, 0f, "a wind blowing west should swing a north-pointing bow to port (downwind).");
            Assert.Less(toEast, 0f, "a wind blowing east should swing a north-pointing bow to starboard (downwind).");
            Assert.AreEqual(toWest, -toEast, 1e-6f, "the turn is symmetric port and starboard.");
        }

        [Test]
        public void DrawingSailsRoundHerUpAndAnAnchorHoldsHerHeadToWind()
        {
            BoatHullDef sloop = Shipped("boat.sloop_30");
            Vector2 bowNorth = Bow(90f);
            Vector2 toWest = new Vector2(-12f, 0f);
            float susc = SeaYawMath.WindSusceptibility(sloop.WindExposure, sloop.MassKg, S);

            float drawing = SeaYawMath.WindShare(toWest, bowNorth,
                SeaYawMath.WindYawBias(false, true, false, true, sloop.SailWeatherHelm, S), susc, S);
            Assert.Less(drawing, 0f, "with her sails drawing, weather helm brings her bow UP to windward (east).");

            float luffing = SeaYawMath.WindShare(toWest, bowNorth,
                SeaYawMath.WindYawBias(false, true, false, false, sloop.SailWeatherHelm, S), susc, S);
            Assert.Greater(luffing, 0f, "with her sails not drawing she blows off like any hull.");

            float anchored = SeaYawMath.WindShare(toWest, bowNorth,
                SeaYawMath.WindYawBias(true, true, false, true, sloop.SailWeatherHelm, S), susc, S);
            Assert.Less(anchored, 0f, "at anchor she lies head to wind.");

            Assert.AreEqual(0f, SeaYawMath.WindYawBias(false, true, true, true, sloop.SailWeatherHelm, S),
                "an unmanned sailing hull is left to the sail drive's own round-up.");
        }

        [Test]
        public void ASlopeUnderTheBowOnlyTurnsTheBowAwayFromIt()
        {
            Vector2 bowNorth = Bow(90f);
            Vector2 risingToStarboard = new Vector2(0.1f, 0f);   // uphill towards the east
            float share = SeaYawMath.WaveShare(risingToStarboard, Vector2.zero, bowNorth, 1f, S);
            Assert.Greater(share, 0f, "the water runs downhill: a bow on a slope rising to starboard goes to port.");
            Assert.AreEqual(S.WaveHelmShare, share, 1e-5f, "the reference slope asks for exactly WaveHelmShare.");
            Assert.AreEqual(0f, SeaYawMath.WaveShare(risingToStarboard, risingToStarboard, bowNorth, 1f, S),
                "the same slope under bow and stern shoves her bodily and does not turn her.");
        }

        // =============================================================================================
        //  light hulls are turned more than heavy ones
        // =============================================================================================

        [Test]
        public void ALightHullIsTurnedMoreThanAHeavyOne_WindAndSea()
        {
            string[] ladder = { "boat.dory", "boat.punt", "boat.cape_islander", "boat.tanker" };
            BoatHullDef[] hulls = ladder.Select(Shipped).ToArray();
            Vector2 bow = Bow(90f);
            Vector2 beamWind = new Vector2(-12f, 0f);

            for (int i = 1; i < hulls.Length; i++)
            {
                BoatHullDef lighter = hulls[i - 1], heavier = hulls[i];
                Assert.Greater(SeaYawMath.WindSusceptibility(lighter.WindExposure, lighter.MassKg, S),
                               SeaYawMath.WindSusceptibility(heavier.WindExposure, heavier.MassKg, S),
                               $"{lighter.Id} should feel the wind more than {heavier.Id}.");
                Assert.Greater(WindShareFor(lighter, beamWind, bow, S), WindShareFor(heavier, beamWind, bow, S),
                               $"{lighter.Id} should be turned harder by a beam wind than {heavier.Id}.");
                Assert.Greater(SeaYawMath.WaveSusceptibility(BoatController.ResponseFor(lighter), S),
                               SeaYawMath.WaveSusceptibility(BoatController.ResponseFor(heavier), S),
                               $"{lighter.Id} should feel the sea more than {heavier.Id}.");
            }
        }

        [Test]
        public void TheDoryAtTheReferenceWindIsAskedForTheReferenceShare()
        {
            BoatHullDef dory = Shipped("boat.dory");
            Assert.AreEqual(1f, SeaYawMath.WindSusceptibility(dory.WindExposure, dory.MassKg, S), 1e-5f,
                "premise: the dory is the reference hull the shares are quoted for.");
            float share = WindShareFor(dory, new Vector2(-S.ReferenceWindMetresPerSecond, 0f), Bow(90f), S);
            Assert.AreEqual(S.WindHelmShare, share, 1e-5f);
        }

        // =============================================================================================
        //  the player stays in control
        // =============================================================================================

        [Test]
        public void TheRudderBeatsTheSeaAtCruise_OnEveryShippedHull()
        {
            SeaYawSettings s = ShippedConfig().SeaYaw;
            List<BoatHullDef> hulls = ShippedHulls();
            Assert.Greater(hulls.Count, 5, "premise: the shipped fleet loads");
            foreach (BoatHullDef hull in hulls)
            {
                // A storm on the beam and a steep quartering sea: everything the sea has.
                Vector2 bow = Bow(90f);
                float wind = WindShareFor(hull, new Vector2(-30f, 0f), bow, s);
                float wave = SeaYawMath.WaveShare(new Vector2(0.5f, 0f), new Vector2(-0.5f, 0f), bow,
                    SeaYawMath.WaveSusceptibility(BoatController.ResponseFor(hull), s), s);
                float share = SeaYawMath.Combine(wind, wave, 1f, s);
                float fullHelm = SeaYawMath.FullHelmTorque(hull);
                float seaTorque = Mathf.Abs(SeaYawMath.Torque(share, fullHelm, 1f));

                float helmAtCruise;
                if (hull.HasSailPlan)
                    helmAtCruise = Mathf.Abs(BoatController.RudderTorque(1f, hull.RudderAuthority,
                        hull.RudderFullAuthorityWayMs, hull.RudderFullAuthorityWayMs));
                else if (BoatController.UsesEngineHelm(hull.Propulsion))
                    helmAtCruise = Mathf.Abs(BoatController.RudderTorque(1f, hull.RudderAuthority, 3f));
                else
                    helmAtCruise = Mathf.Abs(BoatController.OarYawTorque(1f, 0f, hull.OarPower, hull.OarLateralOffset));

                Assert.LessOrEqual(seaTorque, s.MaxHelmShare * helmAtCruise * 1.0001f,
                    $"{hull.Id}: the sea asked for more than MaxHelmShare of her helm.");
                Assert.Less(seaTorque, helmAtCruise, $"{hull.Id}: the sea out-turned her helm at cruise.");
            }
        }

        [TestCase(5f)]
        [TestCase(40f)]
        [TestCase(200f)]
        public void TheAutopilotHoldsInAStrongBeamWindAndASea_OnTheMostSusceptibleHull(float inertia)
        {
            GameConfig config = ShippedConfig();
            SeaYawSettings s = config.SeaYaw;
            HelmAutopilotSettings cfg = config.HelmAutopilot;
            BoatHullDef hull = ShippedHulls().Where(h => h.AutopilotOffered)
                .OrderByDescending(h => SeaYawMath.WindSusceptibility(h.WindExposure, h.MassKg, s)
                                      + SeaYawMath.WaveSusceptibility(BoatController.ResponseFor(h), s))
                .First();

            // The one-axis yaw loop of HeadingHoldAutopilotTests: the game's rudder law, the rudder feel
            // scale, and the body's angular damping as BoatController sets it. Heading here is a math
            // angle (counter-clockwise positive), so a positive torque raises it.
            const float rudderFeel = 0.01f, angularDamping = 2.5f, dt = 0.02f, way = 3f, authority = 600f;
            const float target = 90f, seconds = 120f, tail = 30f, tolerance = 3f;
            Vector2 strongBeamWind = new Vector2(-15f, 0f);
            float waveAmplitude = SeaYawMath.WaveShare(new Vector2(s.ReferenceWaveSlope, 0f), Vector2.zero, Bow(90f),
                SeaYawMath.WaveSusceptibility(BoatController.ResponseFor(hull), s), s);

            var ap = new HeadingHoldAutopilot();
            Assert.IsTrue(ap.TryEngage(new AutopilotSensors(true, true, false, false, target, 0f, way), 0f, cfg),
                "premise: it engages under way");
            float heading = target + 10f, rate = 0f, worst = 0f;
            int steps = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < steps; i++)
            {
                float t = i * dt;
                Assert.IsTrue(ap.Tick(new AutopilotSensors(true, true, false, false, heading, rate, way), dt, cfg,
                    out float helm), $"the hold let go at t={t:F2}s");
                float wind = WindShareFor(hull, strongBeamWind, Bow(heading), s);
                float wave = waveAmplitude * Mathf.Sin(2f * Mathf.PI * t / 6f);
                float sea = SeaYawMath.Combine(wind, wave, 1f, s);
                float torque = (BoatController.RudderTorque(helm, authority, way) + sea * authority) * rudderFeel;
                rate = (rate + torque / inertia * Mathf.Rad2Deg * dt) / (1f + dt * angularDamping);
                heading += rate * dt;
                if (t >= seconds - tail) worst = Mathf.Max(worst, Mathf.Abs(Mathf.DeltaAngle(target, heading)));
            }
            Assert.Less(worst, tolerance,
                $"{hull.Id} (inertia {inertia}): a 15 m/s beam wind and a sea left the hold {worst:F2}° off.");
        }

        // =============================================================================================
        //  a following sea makes the stern wander
        // =============================================================================================

        private static int SignChanges(Vector2 travel, Vector2 bow, out float peak)
        {
            const float wavelength = 20f, amplitude = 0.5f, speed = 3f, dt = 0.05f, seconds = 60f;
            WaveTrains trains = OneTrain(travel, wavelength, amplitude);
            float half = SeaYawMath.WaveHalfSpan(4.5f, wavelength, S);
            float last = 0f;
            int changes = 0;
            peak = 0f;
            for (int i = 0; i * dt < seconds; i++)
            {
                double t = i * dt;
                Vector2 pos = bow * (speed * (float)t);
                float share = SeaYawMath.WaveShare(WaveMath.Sample(pos + bow * half, t, in trains).Slope,
                                                   WaveMath.Sample(pos - bow * half, t, in trains).Slope, bow, 1f, S);
                if (share != 0f && last != 0f && Mathf.Sign(share) != Mathf.Sign(last)) changes++;
                if (share != 0f) last = share;
                peak = Mathf.Max(peak, Mathf.Abs(share));
            }
            return changes;
        }

        [Test]
        public void AFollowingQuarteringSeaSwingsHerMoreSlowlyThanAHeadSea()
        {
            Vector2 travel = Bow(90f);                              // the waves run north
            int following = SignChanges(travel, Bow(45f), out float followingPeak);   // running before them
            int head = SignChanges(travel, Bow(225f), out float headPeak);            // butting into them
            Assert.Greater(followingPeak, 0.05f, "premise: a quartering sea turns the dory noticeably.");
            Assert.Greater(headPeak, 0.05f, "premise: the head quartering sea turns her too.");
            Assert.Less(following, head * 0.7f,
                $"running before the sea she should be swung back and forth far less often (following {following}, " +
                $"head {head}): each swing lasts long enough for the stern to wander.");
        }

        [Test]
        public void AHullLongerThanTheWavesBridgesThem()
        {
            Assert.AreEqual(2.25f, SeaYawMath.WaveHalfSpan(4.5f, 40f, S), 1e-5f, "a short hull reads bow and stern.");
            Assert.AreEqual(S.WaveSpanWavelengthFraction * 20f, SeaYawMath.WaveHalfSpan(110f, 20f, S), 1e-5f,
                "a long hull reads a fraction of the wave, not her length.");
            Assert.AreEqual(0f, SeaYawMath.WaveHalfSpan(4.5f, 0f, S), "no waves, no span.");
        }

        // =============================================================================================
        //  determinism: a pure function of (worldSeed, gameTime)
        // =============================================================================================

        private static float ShareAt(double time, int seed, BoatHullDef hull)
        {
            const double secondsPerHour = 75.0;
            Vector2 wind = WeatherModel.SampleWind(time, seed, secondsPerHour, WindProfile.CoddleCove);
            float sea01 = WeatherModel.SeaState01(wind.magnitude);
            WaveTrains trains = WaveMath.TrainsFrom(wind, sea01, WaveFieldSettings.Default);
            Vector2 pos = new Vector2(40f, -25f);
            Vector2 bow = Bow(70f);
            float half = SeaYawMath.WaveHalfSpan(hull.LengthMeters, trains.Dominant.Wavelength, S);
            float wave = half > 0f
                ? SeaYawMath.WaveShare(WaveMath.Sample(pos + bow * half, time, in trains).Slope,
                                       WaveMath.Sample(pos - bow * half, time, in trains).Slope, bow,
                                       SeaYawMath.WaveSusceptibility(BoatController.ResponseFor(hull), S), S)
                : 0f;
            return SeaYawMath.Combine(WindShareFor(hull, wind, bow, S), wave, 1f, S);
        }

        [Test]
        public void TheSameSeedAndTimeGiveTheSameBits_AndTimeAndSeedMoveIt()
        {
            BoatHullDef dory = Shipped("boat.dory");
            var a = new float[200];
            var b = new float[200];
            var otherSeed = new float[200];
            for (int i = 0; i < a.Length; i++)
            {
                double t = 1000.0 + i * 217.3;   // spread across many game days
                a[i] = ShareAt(t, 12345, dory);
                b[i] = ShareAt(t, 12345, dory);
                otherSeed[i] = ShareAt(t, 777, dory);
            }
            for (int i = 0; i < a.Length; i++)
                Assert.AreEqual(BitConverter.SingleToInt32Bits(a[i]), BitConverter.SingleToInt32Bits(b[i]),
                    $"sample {i}: the sea yaw is not a pure function of (seed, time).");
            Assert.Greater(a.Count(v => v != 0f), 0, "premise: the sea turned her at least once in many game days.");
            Assert.Greater(a.Distinct().Count(), 10, "the turn should move with game time.");
            Assert.IsTrue(a.Where((v, i) => v != otherSeed[i]).Any(), "another world seed should be another sea.");
        }

        // =============================================================================================
        //  the controller applies it (and leaves calm water alone)
        // =============================================================================================

        private sealed class FixedSea : IEnvironmentService
        {
            private readonly Vector2 _wind;
            public FixedSea(Vector2 wind) { _wind = wind; }
            public int WorldSeed => 12345;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(_wind, Vector2.zero, 0f, SeaState.Glass, 1f, 0f);
            public float TideHeightAt(double t) => 0f;
            public float WaterLevelAt(double t) => 0f;
        }

        private static float ControllerShare(Vector2 wind)
        {
            var oldSea = GameServices.Environment;
            var oldTerrain = GameServices.TidalTerrain;
            var go = new GameObject("sea yaw probe") { hideFlags = HideFlags.DontSave };
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            try
            {
                hull.Propulsion = PropulsionType.Engine;
                hull.MassKg = 400f;
                hull.WindExposure = 0.6f;
                hull.RudderAuthority = 600f;
                GameServices.Environment = new FixedSea(wind);
                GameServices.TidalTerrain = null;   // open water: fully exposed
                go.transform.rotation = Quaternion.Euler(0f, 0f, -90f);   // bow east
                var boat = go.AddComponent<BoatController>();
                boat.SetHull(hull);
                go.GetComponent<Rigidbody2D>().rotation = -90f;
                var tick = typeof(BoatController).GetMethod("StepPhysics", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(tick, "premise: BoatController.StepPhysics exists");
                tick.Invoke(boat, null);
                return boat.LastSeaYawHelmShare;
            }
            finally
            {
                GameServices.Environment = oldSea;
                GameServices.TidalTerrain = oldTerrain;
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(hull);
            }
        }

        [Test]
        public void TheControllerLeavesCalmWaterAlone()
        {
            Assert.AreEqual(0f, ControllerShare(new Vector2(0f, 2f)), "light airs on a glass sea turned the hull.");
        }

        [Test]
        public void TheControllerTurnsAnExposedHullDownwind()
        {
            // Bow east, wind blowing north: downwind lies to port, so she turns counter-clockwise (+).
            float share = ControllerShare(new Vector2(0f, 12f));
            Assert.Greater(share, 0f, "a beam wind in open water should turn her bow downwind.");
            Assert.LessOrEqual(share, SeaYawSettings.Default.MaxHelmShare + 1e-6f);
        }
    }
}
