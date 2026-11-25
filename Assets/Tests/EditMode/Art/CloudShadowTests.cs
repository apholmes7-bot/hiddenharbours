using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public sealed class CloudShadowTests
    {
        const double Day = 1800;
        CloudShadowDef _def;
        GameObject _host, _cameraHost;
        CloudShadowSystem _system;
        Camera _camera;

        /// <summary>A wind law for the fixture: a fixed vector, or one that turns and strengthens with time.</summary>
        sealed class Wind : IWaveWindSource
        {
            public Vector2 Steady; public bool Turning; public int Calls;
            public WaveWindSample WaveWindAt(double t)
            {
                Calls++;
                if (!Turning) return new WaveWindSample(Steady, 0);
                float a = (float)(t / 400.0), s = 3 + 2 * Mathf.Sin((float)(t / 170.0));
                return new WaveWindSample(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s, 0);
            }
        }

        [SetUp] public void SetUp()
        {
            _def = ScriptableObject.CreateInstance<CloudShadowDef>();
            _def.Shader = Shader.Find("HiddenHarbours/CloudShadow");
            _host = new GameObject("CloudGuard"); _system = _host.AddComponent<CloudShadowSystem>(); _system.Configure(_def);
            _cameraHost = new GameObject("CloudGuardCamera"); _camera = _cameraHost.AddComponent<Camera>(); _camera.orthographic = true;
            _camera.transform.position = new Vector3(0, 0, -10);
        }

        [TearDown] public void TearDown()
        {
            PlayerStorey.Clear();
            Object.DestroyImmediate(_host); Object.DestroyImmediate(_cameraHost); Object.DestroyImmediate(_def);
        }

        /// <summary>b - a on the pattern's repeat (offsets are wrapped to it).</summary>
        float Delta(float a, float b) => (float)(CloudShadowMath.Wrap(b - a + _def.ScaleMetres * .5, _def.ScaleMetres) - _def.ScaleMetres * .5);

        static CloudShadowFrame Frame(int seed, double t, IWaveWindSource wind, float cover = .5f, float elevation = 1)
            => new CloudShadowFrame(seed, t, wind, cover, elevation);

        // ---- amount: cover, sun, indoors -------------------------------------------------------------

        [TestCase(0f, 0f)] [TestCase(.2f, .5f)] [TestCase(.5f, .65f)] [TestCase(.8f, .35f)] [TestCase(1f, 0f)]
        public void Strength_FollowsTheCoverCurve_NoneInAClearSkyOrFlatOvercast(float cover, float expected)
            => Assert.That(CloudShadowMath.Strength(_def, cover, 1), Is.EqualTo(expected).Within(.000001));

        [Test] public void BrokenCloud_IsStrongerThanThinOrMergedCloud()
        {
            float broken = CloudShadowMath.Strength(_def, .5f, 1);
            Assert.That(broken, Is.GreaterThan(CloudShadowMath.Strength(_def, .05f, 1)));
            Assert.That(broken, Is.GreaterThan(CloudShadowMath.Strength(_def, .95f, 1)));
            Assert.That(_def.Coverage.Evaluate(.95f), Is.GreaterThan(_def.Coverage.Evaluate(.5f)), "patches merge as the cover closes");
        }

        [TestCase(0f)] [TestCase(-1f)] [TestCase(-.2f)]
        public void Night_CastsNothing(float elevation)
        {
            Assert.That(CloudShadowMath.Strength(_def, .5f, elevation), Is.Zero);
            _system.Prepare(_camera, Frame(1, 100, new Wind { Steady = Vector2.right }, .5f, elevation));
            Assert.That(_system.DrawCount, Is.Zero);
        }

        [Test] public void LowSun_FadesTheShadeTowardTheHorizon()
        {
            float noon = CloudShadowMath.Strength(_def, .5f, 1), low = CloudShadowMath.Strength(_def, .5f, _def.FullSunElevation * .5f);
            Assert.That(low, Is.EqualTo(noon * .5f).Within(.000001));
        }

        [Test] public void ClearSkyAndFullOvercast_DisableTheCachedDraw()
        {
            var wind = new Wind { Steady = Vector2.right };
            _system.Prepare(_camera, Frame(1, 100, wind, .5f)); Assert.That(_system.DrawCount, Is.EqualTo(1));
            _system.Prepare(_camera, Frame(1, 100, wind, 0)); Assert.That(_system.DrawCount, Is.Zero);
            _system.Prepare(_camera, Frame(1, 100, wind, 1)); Assert.That(_system.DrawCount, Is.Zero);
        }

        [Test] public void Indoors_TheShadeIsNothing()
        {
            var wind = new Wind { Steady = Vector2.right };
            Assert.That(CloudShadowMath.Strength(_def, .5f, 1, 0), Is.Zero);
            _system.SetOutdoors(0);
            _system.Prepare(_camera, Frame(1, 100, wind)); Assert.That(_system.DrawCount, Is.Zero);
            _system.SetOutdoors(1);
            _system.Prepare(_camera, Frame(1, 100, wind)); Assert.That(_system.DrawCount, Is.EqualTo(1));
        }

        [Test] public void PlayerStorey_SaysIndoorsWhileABuildingHoldsHim()
        {
            var house = new object();
            Assert.That(PlayerStorey.Indoors, Is.False);
            PlayerStorey.Report(house, true, 0); Assert.That(PlayerStorey.Indoors, Is.True); Assert.That(_system.Indoors, Is.True);
            PlayerStorey.Report(new object(), false, 0); Assert.That(PlayerStorey.Indoors, Is.True, "another house's 'outside' changes nothing");
            PlayerStorey.Report(house, false, 0); Assert.That(PlayerStorey.Indoors, Is.False); Assert.That(_system.Indoors, Is.False);
        }

        [Test] public void Overcast_NormalizesTheLightingProfilesWeatherCap()
        {
            var profile = Resources.Load<DayNightProfile>("DayNightProfile"); Assert.That(profile, Is.Not.Null);
            Assert.That(CloudShadowMath.Overcast(1, 0, profile), Is.Zero);
            Assert.That(CloudShadowMath.Overcast(1, 1, profile), Is.EqualTo(1));
            Assert.That(CloudShadowMath.Overcast(0, 0, profile), Is.EqualTo(1));
        }

        [Test] public void Cover_IsTheLargerOfFairWeatherCloudAndTheWeathersOvercast()
        {
            for (double t = 0; t < 20000; t += 37)
            {
                float fair = CloudShadowMath.FairCover(_def, 7, t);
                Assert.That(CloudShadowMath.Cover(_def, 7, t, 0), Is.EqualTo(fair));
                Assert.That(CloudShadowMath.Cover(_def, 7, t, .9f), Is.EqualTo(Mathf.Max(fair, .9f)));
            }
        }

        [Test] public void FairWeather_HasClearSpellsAndBrokenSpells_FromSeedAndTimeAlone()
        {
            float min = 1, max = 0; int clear = 0, n = 0;
            for (double t = 0; t < 200 * _def.FairCoverPeriodSeconds; t += 50, n++)
            {
                float c = CloudShadowMath.FairCover(_def, 12345, t);
                Assert.That(CloudShadowMath.FairCover(_def, 12345, t), Is.EqualTo(c), "same seed and time, same sky");
                min = Mathf.Min(min, c); max = Mathf.Max(max, c); if (c == 0) clear++;
            }
            Assert.That(min, Is.Zero, "some spells are clear sky");
            Assert.That(max, Is.GreaterThan(_def.FairCoverMax * .8f), "some spells build to broken cloud");
            Assert.That(max, Is.LessThanOrEqualTo(_def.FairCoverMax));
            Assert.That(clear / (float)n, Is.InRange(.15f, .85f));
            Assert.That(CloudShadowMath.FairCover(_def, 1, 12345), Is.Not.EqualTo(CloudShadowMath.FairCover(_def, 2, 12345)));
        }

        // ---- drift: the wind carries it, deterministically -------------------------------------------

        [Test] public void Drift_IsDownwind_AndScalesWithWind()
        {
            var one = new CloudDrift().WindIntegral(new Wind { Steady = new Vector2(3, 4) }, 1, 300, Day, 15, out _);
            var two = new CloudDrift().WindIntegral(new Wind { Steady = new Vector2(6, 8) }, 1, 300, Day, 15, out _);
            Assert.That(one.x, Is.EqualTo(900).Within(.001)); Assert.That(one.y, Is.EqualTo(1200).Within(.001));
            Assert.That(two.x, Is.EqualTo(one.x * 2).Within(.001)); Assert.That(two.y, Is.EqualTo(one.y * 2).Within(.001));

            // The shader samples (world - offset): a growing offset moves the pattern the same way.
            var drift = new CloudDrift(); var wind = new Wind { Steady = new Vector2(0, 5) };
            Vector4 a = drift.Offsets(wind, 1, 100, Day, _def), b = drift.Offsets(wind, 1, 120, Day, _def);
            Assert.That(Delta(a.x, b.x), Is.EqualTo(0).Within(.0001));
            Assert.That(Delta(a.y, b.y), Is.EqualTo(5 * 20 * _def.DriftMetresPerGameSecondPerMps).Within(.0001));
            Assert.That(Delta(a.w, b.w), Is.EqualTo(5 * 20 * _def.DriftMetresPerGameSecondPerMps * _def.SecondaryRate).Within(.0001));
        }

        [Test] public void Drift_FollowsAChangingWind_AsItsIntegral()
        {
            var wind = new Wind { Turning = true };
            Vector2 got = new CloudDrift().WindIntegral(wind, 1, 1234.5, Day, 15, out _);
            double x = 0, y = 0, h = .05;
            for (double t = 0; t < 1234.5; t += h) { var w = wind.WaveWindAt(t + h * .5).Wind; x += w.x * h; y += w.y * h; }
            Assert.That(got.x, Is.EqualTo(x).Within(Mathf.Abs((float)x) * .01 + 1));
            Assert.That(got.y, Is.EqualTo(y).Within(Mathf.Abs((float)y) * .01 + 1));
        }

        [Test] public void SeedAndGameTime_ReproduceTheSameOffset_HoweverYouArrived()
        {
            var wind = new Wind { Turning = true };
            var stepped = new CloudDrift();
            for (double t = 80000; t < 80150; t += 1.37) stepped.Offsets(wind, 123, t, Day, _def);
            Vector4 walked = stepped.Offsets(wind, 123, 80150, Day, _def);
            Vector4 cold = new CloudDrift().Offsets(wind, 123, 80150, Day, _def);
            Assert.That(walked, Is.EqualTo(cold), "a running sum is only a memo of the same additions");
            Assert.That(new CloudDrift().Offsets(wind, 124, 80150, Day, _def), Is.Not.EqualTo(cold));

            // Time going back (a load) recomputes rather than carrying a stale sum.
            Vector4 earlier = stepped.Offsets(wind, 123, 80010, Day, _def);
            Assert.That(earlier, Is.EqualTo(new CloudDrift().Offsets(wind, 123, 80010, Day, _def)));
        }

        [Test] public void ANewWindLaw_IsSummedAgain_NotAddedToTheOldOnesSum()
        {
            var calm = new Wind { Steady = new Vector2(1, 0) };
            var drift = new CloudDrift();
            drift.Offsets(calm, 123, 80100, Day, _def);
            // the same source now answers with another law (a region's wind profile arrives)
            calm.Steady = new Vector2(0, 4);
            Assert.That(drift.Offsets(calm, 123, 80200, Day, _def), Is.EqualTo(new CloudDrift().Offsets(calm, 123, 80200, Day, _def)));
            // a different source (a new environment) gives the fresh answer too
            var other = new Wind { Steady = new Vector2(0, 4) };
            drift.Offsets(calm, 123, 80300, Day, _def);
            Assert.That(drift.Offsets(other, 123, 80400, Day, _def), Is.EqualTo(new CloudDrift().Offsets(other, 123, 80400, Day, _def)));
        }

        [Test] public void EachDay_StartsAFreshSky_AtMidnight()
        {
            var wind = new Wind { Steady = new Vector2(3, 1) };
            new CloudDrift().WindIntegral(wind, 5, Day * 3 + 10, Day, 15, out long day);
            Assert.That(day, Is.EqualTo(3));
            Assert.That(CloudShadowMath.DayOrigin(5, 3, 1024), Is.Not.EqualTo(CloudShadowMath.DayOrigin(5, 4, 1024)));
            Vector2 since = new CloudDrift().WindIntegral(wind, 5, Day * 3 + 10, Day, 15, out _);
            Assert.That(since.x, Is.EqualTo(30).Within(.001), "the drift counts from midnight, so a cold start is a day's work at most");
        }

        [Test] public void TwoCameras_DoNotAdvanceItTwice()
        {
            var wind = new Wind { Steady = new Vector2(0, 5) };
            _system.Prepare(_camera, Frame(123, 500, wind)); Vector4 first = _system.Offsets; int calls = wind.Calls;
            _system.Prepare(_camera, Frame(123, 500, wind));
            Assert.That(_system.Offsets, Is.EqualTo(first)); Assert.That(wind.Calls, Is.EqualTo(calls));
            _system.Prepare(_camera, Frame(123, 520, wind));
            Assert.That(Delta(first.y, _system.Offsets.y), Is.EqualTo(5 * 20 * _def.DriftMetresPerGameSecondPerMps).Within(.0001));
        }

        // ---- shape -------------------------------------------------------------------------------

        [Test] public void Noise_IsSeeded_Tileable_AndEqualisedSoCoverageMeansArea()
        {
            byte[] a = CloudShadowMath.BuildNoise(12345, 64, 8, 4);
            Assert.That(CloudShadowMath.BuildNoise(12345, 64, 8, 4), Is.EqualTo(a));
            Assert.That(CloudShadowMath.BuildNoise(12346, 64, 8, 4), Is.Not.EqualTo(a));
            int above = 0; foreach (byte b in a) if (b >= 128) above++;
            Assert.That(above / (float)a.Length, Is.EqualTo(.5f).Within(.01f));
            double inner = 0, seam = 0;
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 63; x++) inner += Mathf.Abs(a[y * 64 + x + 1] - a[y * 64 + x]);
                seam += Mathf.Abs(a[y * 64] - a[y * 64 + 63]);
            }
            Assert.That(seam / 64, Is.LessThan(inner / (63 * 64) * 3), "the repeat has no visible seam");
        }

        [Test] public void Off_ReleasesTheRenderer_AndCostsNoDraw()
        {
            var wind = new Wind { Steady = Vector2.right };
            _system.Prepare(_camera, Frame(1, 0, wind));
            Assert.That(_system.DrawCount, Is.EqualTo(1));
            Assert.That(_host.GetComponentsInChildren<MeshRenderer>(), Has.Length.EqualTo(1));
            var renderer = _system.ShadowRenderer;
            _system.Prepare(_camera, Frame(1, 1, wind));
            Assert.That(_system.ShadowRenderer, Is.SameAs(renderer));
            _system.SetCasting(false);
            Assert.That(_system.DrawCount, Is.Zero); Assert.That(_system.ShadowRenderer, Is.Null);
            Assert.That(_host.GetComponentsInChildren<MeshRenderer>(), Is.Empty);
        }

        [Test] public void DisabledDef_DoesNotCreateARenderer()
        {
            _def.Enabled = false; _system.Configure(_def);
            _system.Prepare(_camera, Frame(1, 0, new Wind { Steady = Vector2.right }));
            Assert.That(_system.ShadowRenderer, Is.Null); Assert.That(_system.DrawCount, Is.Zero);
        }

        [Test] public void MovingTime_AllocatesNoManagedBytes()
        {
            var wind = new Wind { Steady = new Vector2(3, 4) };
            for (int i = 0; i < 100; i++) _system.Prepare(_camera, Frame(1, 1000 + i * .02, wind));
            System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) _system.Prepare(_camera, Frame(1, 1002 + i * .02, wind));
            long bytes = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.Zero, "the warmed camera path reuses its noise, material, quad and property block");
        }

        [Test] public void OverlayAndNonWorldCameras_AreExcluded()
        {
            Assert.That(CloudShadowSystem.IsWorldCamera(_camera), Is.True);
            _camera.cullingMask = 1 << 5;
            Assert.That(CloudShadowSystem.IsWorldCamera(_camera), Is.False, "a camera that does not draw the world layer");
            _camera.cullingMask = ~0;
            _camera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            Assert.That(CloudShadowSystem.IsWorldCamera(_camera), Is.False);
            _system.Prepare(_camera, Frame(1, 0, new Wind())); Assert.That(_system.DrawCount, Is.Zero);
        }

        [Test] public void DefaultAsset_Validates_AndHasAStableTypedId()
        {
            var def = Resources.Load<CloudShadowDef>(nameof(CloudShadowDef));
            Assert.That(def, Is.Not.Null); Assert.That(def.IsValid(), Is.True); Assert.That(def.Id, Is.EqualTo("cloud_shadows.default"));
            Assert.That(def.Enabled, Is.True);
            Assert.That(def.Strength.Evaluate(0), Is.Zero, "a clear sky casts no cloud shade");
            Assert.That(def.ScaleMetres / def.NoiseCells, Is.GreaterThan(40), "a cloud is far bigger than a boat");
        }

        [Test] public void InvalidData_IsRejected()
        {
            Assert.That(_def.IsValid(), Is.True);
            _def.ScaleMetres = float.NaN; Assert.That(_def.IsValid(), Is.False);
            _def.ScaleMetres = 1024; _def.NoiseSize = 96; Assert.That(_def.IsValid(), Is.False);
            _def.NoiseSize = 128; _def.NoiseCells = 6; Assert.That(_def.IsValid(), Is.False);
            _def.NoiseCells = 8; _def.ShadeColour = Color.black; Assert.That(_def.IsValid(), Is.False, "the shade never goes black");
            _def.ShadeColour = new Color(.62f, .67f, .8f); _def.DriftStepSeconds = 0; Assert.That(_def.IsValid(), Is.False);
            _def.DriftStepSeconds = 15; _def.Id = ""; Assert.That(_def.IsValid(), Is.False);
        }

        [Test] public void TheCloudShader_NeitherUsesTheStencil_NorMultipliesTheDayNightTintAgain()
        {
            string shader = File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursCloudShadow.shader");
            Assert.That(Regex.IsMatch(shader, @"(?m)^\s*Stencil\s*[\r\n{]"), Is.False);
            Assert.That(shader, Does.Not.Contain("_DayNightTint"));
            Assert.That(shader, Does.Contain("Blend DstColor Zero"));
            Assert.That(shader, Does.Contain("UNITY_MATRIX_I_VP"));
        }
    }
}
