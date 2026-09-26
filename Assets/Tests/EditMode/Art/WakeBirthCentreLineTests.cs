using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// **THE WATER DRAWS NEW FOAM ON HER TRACK, AT THE STERN SHE DRAWS** (#875, way A: the owner's ruling
    /// of 2026-09-23).
    ///
    /// <para>The water draws a foam deposit at its ground point plus the surface's drawing lift, and it has
    /// NO tide term (<c>HiddenHarboursWater.shader</c>, <c>vertDisplaced</c>: the swell times the
    /// exaggeration times the shore fade, plus the stern-wave lift times the fade). The unfixed injector laid
    /// new foam at the drawn transom LESS the tide's rise, with the lift inverted, so the water drew it a
    /// whole rise off her track: 1.16 m north of the stern at 11:00 on an east leg, on the cape and on the
    /// dory alike (Gate B, plates L02 and L03). <see cref="FoamInjector.EmissionStern"/> is now the drawn
    /// transom with only the lift inverted.</para>
    ///
    /// <para>This takes the laid point from the injector and WHERE IT DRAWS from the water's rule, written
    /// out below over the water's own inputs (the published wave globals and the published displaced sea),
    /// never from the injector's inversion. It measures that point against
    /// <see cref="HullWakePose.DrawnStern"/> ACROSS her track, which is the centring, and ALONG it, which is
    /// the foam starting at the stern. It replaces the PlayMode mirror that added the tide back and so passed
    /// the unfixed code at 0.0003 m. No GPU and no scene, so CI runs it, where every PlayMode foam test
    /// skips.</para>
    /// </summary>
    public class WakeBirthCentreLineTests
    {
        /// <summary>Two pixels at 32 PPU: the PlayMode suite's attachment tolerance.</summary>
        private const float AcrossBarMetres = 2f / 32f;

        /// <summary>The image gate's "the foam starts at the stern" bar.</summary>
        private const float AlongBarMetres = 0.5f;

        /// <summary>The tide's rise at the Gate B shot hour, 11:00 (plates L02 and L03).</summary>
        private const float GateBTideRise = -1.16f;

        private static readonly float[] TideRises = { GateBTideRise, 0f, 0.9f };
        private static readonly float[] HeadingsDegrees = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f, 259.8f };
        private static readonly Vector2[] DrawnSterns =
        {
            new Vector2(371.303f, 38.825f),   // L02: the cape's drawn stern at f180
            new Vector2(375.458f, 39.010f),   // L03: the dory's drawn stern at f180
            Vector2.zero,
            new Vector2(-41.7f, 12.3f),
            new Vector2(120.5f, -88.25f),
            new Vector2(-203.4f, 150.6f),
        };

        private readonly object _seaOwner = new object();
        private GameObject _hull;
        private PoseStub _pose;
        private FoamInjector _injector;

        [SetUp]
        public void SetUp()
        {
            // No services: the water level reads 0, there is no bed (so the shore fade is 1) and the fetch
            // envelope is 1. The sea is the one thing published.
            GameServices.Reset();
            FoamInjectionRegistry.ClearWakeLift();
            var trains = new WaveTrain[2];
            trains[0] = new WaveTrain(new Vector2(0.8f, 0.6f), 60f, 0.2f, 0.4f, 9.81f);
            trains[1] = new WaveTrain(new Vector2(-0.28f, 0.96f), 42f, 0.08f, 1.9f, 9.81f);
            WaveTrains field = WaveTrains.From(trains, 2, 2.2f, 0);
            WaveFieldBridge.PublishGlobals(WaveFieldBridge.Pack(in field));
            // The drawn sea's swell scale (2.8) and a real exaggeration, so the lift is large enough for the
            // inversion to matter, and gentle enough (|d lift / d y| < 0.14) for its three steps to converge.
            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(1.5f, 0.5f, 2.8f));

            _hull = new GameObject("WakeBirthCentreLine hull");
            _pose = _hull.AddComponent<PoseStub>();
            var stern = new GameObject("stern injector");
            stern.transform.SetParent(_hull.transform, false);
            _injector = stern.AddComponent<FoamInjector>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hull != null) Object.DestroyImmediate(_hull);
            WaveFieldBridge.PublishGlobals(PackedWaveField.Empty);
            DisplacedSea.Clear(_seaOwner);
            FoamInjectionRegistry.ClearWakeLift();
            GameServices.Reset();
        }

        [Test]
        public void EmissionStern_TheWaterDrawsItOnHerTrack_AtTheDrawnStern_AtEveryHeadingAndTide()
        {
            // Premise: every stern-wave slot is empty, so the water's stern-wave term is zero here and the
            // rule in WhereTheWaterDraws is the whole rule.
            var root = new Vector4[FoamBuffer.MaxInjectors];
            var shape = new Vector4[FoamBuffer.MaxInjectors];
            FoamInjectionRegistry.ReadWakeLift(root, shape);
            for (int i = 0; i < shape.Length; i++)
                Assert.IsFalse(shape[i].x > 0f && shape[i].y > 0f, $"premise: stern-wave slot {i} is empty");

            int samples = 0, tideSamples = 0, unfixedRejected = 0, uninvertedRejected = 0;
            float maxAcross = 0f, maxAlong = 0f, maxLift = 0f;
            float maxUnfixedAcross = 0f, maxUnfixedAlong = 0f, maxUninverted = 0f;
            var failures = new StringBuilder();
            foreach (Vector2 drawnStern in DrawnSterns)
            {
                maxLift = Mathf.Max(maxLift, Mathf.Abs(SwellLift(drawnStern)));
                foreach (float degrees in HeadingsDegrees)
                foreach (float tideRise in TideRises)
                {
                    float radians = degrees * Mathf.Deg2Rad;
                    var heading = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
                    _pose.Pose = new HullWakePose(drawnStern, heading, tideRise);
                    samples++;

                    // THE FIX: where the water draws the deposit the injector lays now.
                    Vector2 laid = _injector.EmissionStern;
                    Assert.AreEqual(heading, _injector.EmissionHeading, "premise: the injector read the hull's pose");
                    TrackOffsets(WhereTheWaterDraws(laid), drawnStern, heading, out float across, out float along);
                    maxAcross = Mathf.Max(maxAcross, across);
                    maxAlong = Mathf.Max(maxAlong, along);
                    if (across > AcrossBarMetres || along > AlongBarMetres)
                        failures.AppendLine($"  stern={drawnStern} heading={degrees} tide={tideRise:F2}: " +
                                            $"across={across:F4} m along={along:F4} m");

                    // THE UNFIXED BIRTH (f5583fb3): the tide's rise taken out as well. The bars must reject it
                    // wherever the tide has risen or fallen.
                    TrackOffsets(WhereTheWaterDraws(UnfixedTideFrameBirth(drawnStern, tideRise)), drawnStern,
                                 heading, out float unfixedAcross, out float unfixedAlong);
                    if (tideRise != 0f)
                    {
                        tideSamples++;
                        if (unfixedAcross > AcrossBarMetres || unfixedAlong > AlongBarMetres) unfixedRejected++;
                        maxUnfixedAcross = Mathf.Max(maxUnfixedAcross, unfixedAcross);
                        maxUnfixedAlong = Mathf.Max(maxUnfixedAlong, unfixedAlong);
                    }

                    // A birth that never inverts the lift: the bars must see that too.
                    TrackOffsets(WhereTheWaterDraws(drawnStern), drawnStern, heading,
                                 out float rawAcross, out float rawAlong);
                    if (rawAcross > AcrossBarMetres || rawAlong > AlongBarMetres) uninvertedRejected++;
                    maxUninverted = Mathf.Max(maxUninverted, Mathf.Max(rawAcross, rawAlong));
                }
            }

            string summary =
                $"WAKE-BIRTH samples={samples}: fixed max across={maxAcross:F5} m (bar {AcrossBarMetres:F4}), " +
                $"max along={maxAlong:F5} m (bar {AlongBarMetres:F2}); unfixed birth rejected " +
                $"{unfixedRejected}/{tideSamples}, max across={maxUnfixedAcross:F4} m, max along={maxUnfixedAlong:F4} m; " +
                $"uninverted birth rejected {uninvertedRejected}/{samples}, max offset={maxUninverted:F4} m; " +
                $"max swell lift={maxLift:F4} m";
            Assert.GreaterOrEqual(maxLift, 0.15f, "premise: the sea lifts enough to exercise the inversion\n" + summary);
            Assert.AreEqual(tideSamples, unfixedRejected,
                "the bars must reject the unfixed tide-frame birth at every risen or fallen tide\n" + summary);
            Assert.GreaterOrEqual(maxUnfixedAcross, 1f,
                "the unfixed birth misses her track by about a metre at 11:00 on an east or west leg\n" + summary);
            Assert.Greater(uninvertedRejected, 0, "the bars must reject a birth that never inverts the lift\n" + summary);
            Assert.AreEqual(0, failures.Length,
                "the water must draw new foam on her track at the drawn stern:\n" + failures + summary);
            TestContext.WriteLine(summary);
        }

        /// <summary>WHERE THE WATER DRAWS a deposit laid at <paramref name="ground"/>, by its own rule
        /// (<c>vertDisplaced</c>: the ground point plus the drawing lift, no tide term), over the water's
        /// inputs. The stern-wave term is zero here by premise (every slot empty).</summary>
        private static Vector2 WhereTheWaterDraws(Vector2 ground) => ground + Vector2.up * SwellLift(ground);

        /// <summary>The water's swell lift at a ground point: the published field sampled by the shader's
        /// twin at the drawn sea's frequency scale, times the exaggeration, times the shore fade. With no
        /// services the level is 0 and there is no bed, so the fade is 1 and the fetch envelope is 1.</summary>
        private static float SwellLift(Vector2 ground)
        {
            Assert.IsTrue(DisplacedSea.TryGet(out DisplacedSeaState sea), "premise: the displaced sea is published");
            PackedWaveField field = WaveFieldBridge.ReadPublishedField();
            float swell = WaveFieldBridge.ShaderTwinSample(ground, in field, sea.FreqScale, 1f).Height;
            float fade = ShoreFadeMath.Fade01(float.PositiveInfinity, sea.ShoreFadeBandMeters);
            return swell * sea.Exaggeration * fade;
        }

        /// <summary>The unfixed birth, f5583fb3 <c>FoamInjector.SternWorld</c>: the drawn transom less the
        /// tide's rise, then the lift inverted in three steps.</summary>
        private static Vector2 UnfixedTideFrameBirth(Vector2 drawnStern, float tideRise)
        {
            Vector2 datum = drawnStern - Vector2.up * tideRise;
            Vector2 at = datum;
            for (int i = 0; i < 3; i++) at = datum - Vector2.up * SwellLift(at);
            return at;
        }

        /// <summary>The offset of a drawn point from the drawn stern, ACROSS her track and ALONG it.</summary>
        private static void TrackOffsets(Vector2 drawnAt, Vector2 drawnStern, Vector2 heading,
                                         out float across, out float along)
        {
            Vector2 h = heading.normalized;
            Vector2 error = drawnAt - drawnStern;
            across = Mathf.Abs(h.x * error.y - h.y * error.x);
            along = Mathf.Abs(Vector2.Dot(h, error));
        }

        /// <summary>A hull that reports a set wake pose; the injector finds it in its parent.</summary>
        private sealed class PoseStub : MonoBehaviour, IHullWakePoseSource
        {
            public HullWakePose Pose { get; set; }

            public bool TryGetWakePose(out HullWakePose pose)
            {
                pose = Pose;
                return true;
            }
        }
    }
}
