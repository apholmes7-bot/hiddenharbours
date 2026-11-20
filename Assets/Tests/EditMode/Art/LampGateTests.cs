using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The SHARED lamp gate (owner ruling 2026-11-14: "Lamps should not glow at noon"). Every lamp, window and neon
    /// glow passes through <see cref="LightMath.NightGate(float, float, float)"/> (or its HLSL twins) with its own
    /// threshold; the day/night profile's lamp band now holds every one of them off through any daylight and full by
    /// dusk. Walked across the shipped day curve (<see cref="DayNightProfile.CreateDefault"/>, which
    /// DayNightProfileAssetTests pins equal to the shipped asset), clear and overcast, moonless and moonlit.
    /// </summary>
    public class LampGateTests
    {
        /// <summary>Real gate settings in the game: SceneLight's default, the boat lamp's, a window glow that opens early.</summary>
        static readonly (float Threshold, float Softness)[] Lamps = { (0.12f, 0.35f), (0.35f, 0.25f), (0.05f, 0.2f), (0f, 0.1f) };

        DayNightProfile _profile;

        [SetUp] public void SetUp() { _profile = DayNightProfile.CreateDefault(); LightMath.SetLampBand(0f, 0f); }

        [TearDown]
        public void TearDown()
        {
            LightMath.SetLampBand(0f, 0f);
            if (_profile != null) Object.DestroyImmediate(_profile);
        }

        float Lum(float hour, float visibility = 1f, float seaState = 0f, float moonIllumination = 0f, float moonElevation = 0f) =>
            LightMath.Luminance(DayNightMath.DayNightTint(hour, _profile, visibility, seaState, moonIllumination, moonElevation));

        float Gate(float lum, (float Threshold, float Softness) lamp) =>
            LightMath.NightGate(lum, lamp.Threshold, lamp.Softness, _profile.LampsOffDarkness, _profile.LampsOnDarkness);

        [Test]
        public void AtNoon_EveryLampIsDark_ClearOrOvercast()
        {
            foreach (var lamp in Lamps)
            {
                Assert.AreEqual(0f, Gate(Lum(13f), lamp), 1e-6f, $"clear noon, lamp {lamp}");
                Assert.AreEqual(0f, Gate(Lum(13f, visibility: 0f, seaState: 1f), lamp), 1e-6f, $"fog and storm at noon, lamp {lamp}");
            }
        }

        [Test]
        public void ThroughTheWholeDaylight_EveryLampIsDark()
        {
            // Clear: from two hours after sunrise to the warm key. Fog and a full storm darken the frame by up to
            // the profile's weather cap, so a storm's evening may light the lamps early (18:00 storm reads 0.40,
            // the band's edge); through the middle of the day (solar noon +/- 3 h) not even a storm does.
            // Clear daylight ends at the profile's warm key (sunset - WarmKeyBeforeSunsetHours, ~18:00): past it the
            // evening tint darkens through the band (18:45 reads 0.43) and the lamps begin to come up, as they should.
            float from = _profile.SunriseHour + 2f, to = _profile.SunsetHour - _profile.WarmKeyBeforeSunsetHours;
            float noon = (_profile.SunriseHour + _profile.SunsetHour) * 0.5f;
            for (float h = from; h <= to; h += 0.25f)
                foreach (var lamp in Lamps)
                {
                    Assert.AreEqual(0f, Gate(Lum(h), lamp), 1e-6f, $"{h:0.00}h clear, lamp {lamp}: darkness {1f - Lum(h):0.000}");
                    if (Mathf.Abs(h - noon) <= 3f)
                        Assert.AreEqual(0f, Gate(Lum(h, visibility: 0f, seaState: 1f), lamp), 1e-6f,
                            $"{h:0.00}h fog and storm, lamp {lamp}: darkness {1f - Lum(h, 0f, 1f):0.000}");
                }
        }

        [Test]
        public void AtNight_EveryLampIsFull_MoonlessOrUnderAFullMoon()
        {
            foreach (float h in new[] { 23f, 23.5f, 0f, 1f, 2f, 3f })
                foreach (var lamp in Lamps)
                {
                    Assert.AreEqual(1f, Gate(Lum(h), lamp), 1e-4f, $"moonless {h}h, lamp {lamp}");
                    Assert.AreEqual(1f, Gate(Lum(h, moonIllumination: 1f, moonElevation: 1f), lamp), 1e-4f,
                        $"full moon at its peak, {h}h, lamp {lamp}: darkness {1f - Lum(h, 1f, 0f, 1f, 1f):0.000}");
                }
        }

        [Test]
        public void AtDusk_TheLampsAreOn()
        {
            foreach (var lamp in Lamps)
                Assert.AreEqual(1f, Gate(Lum(20.5f), lamp), 1e-4f, $"20:30, lamp {lamp}: darkness {1f - Lum(20.5f):0.000}");
        }

        [Test]
        public void TheGate_NeverFallsAsTheEveningDarkens()
        {
            foreach (var lamp in Lamps)
            {
                float last = 0f;
                for (float h = 12f; h <= 24f; h += 0.1f)
                {
                    float g = Gate(Lum(h), lamp);
                    Assert.GreaterOrEqual(g, last - 1e-5f, $"{h:0.0}h, lamp {lamp}");
                    last = g;
                }
            }
        }

        [Test]
        public void NoBand_IsTheOldGateExactly()
        {
            for (float lum = 0f; lum <= 1f; lum += 0.05f)
                foreach (var lamp in Lamps)
                {
                    float darkness = Mathf.Clamp01(1f - lum);
                    float lo = Mathf.Clamp01(lamp.Threshold), hi = Mathf.Clamp01(lamp.Threshold + Mathf.Max(lamp.Softness, 1e-4f));
                    float t = Mathf.Clamp01((darkness - lo) / (hi - lo));
                    float old = t * t * (3f - 2f * t);
                    Assert.AreEqual(old, LightMath.NightGate(lum, lamp.Threshold, lamp.Softness, 0f, 0f), 1e-5f);
                    Assert.AreEqual(old, LightMath.NightGate(lum, lamp.Threshold, lamp.Softness), 1e-5f, "no band published");
                }
        }

        [Test]
        public void ThePublishedBand_IsWhatTheThreeArgumentGateApplies()
        {
            LightMath.SetLampBand(_profile.LampsOffDarkness, _profile.LampsOnDarkness);
            foreach (var lamp in Lamps)
            {
                Assert.AreEqual(0f, LightMath.NightGate(Lum(13f), lamp.Threshold, lamp.Softness), 1e-6f, $"noon, lamp {lamp}");
                Assert.AreEqual(0f, LightMath.NightGateWithFallback(Lum(13f), lamp.Threshold, lamp.Softness, true, 1f), 1e-6f);
                Assert.AreEqual(1f, LightMath.NightGate(Lum(2f), lamp.Threshold, lamp.Softness), 1e-4f, $"2am, lamp {lamp}");
            }
        }

        /// <summary>The water's cone gate is the plain ramp (its shader is hash-pinned by SurfSequenceTests), so the
        /// band reaches it folded into the published (threshold, softness): the shader's own smoothstep on that pair
        /// must be the banded C# gate, noon dark included, and no band must pass the pair through untouched.</summary>
        [Test]
        public void TheWatersPublishedRamp_CarriesTheBand()
        {
            foreach (var lamp in Lamps)
            {
                Vector2 plain = LightMath.BandedGateRamp(lamp.Threshold, lamp.Softness);
                Assert.AreEqual(new Vector2(lamp.Threshold, lamp.Softness), plain, $"no band, lamp {lamp}");
            }
            LightMath.SetLampBand(_profile.LampsOffDarkness, _profile.LampsOnDarkness);
            for (float lum = 0f; lum <= 1f; lum += 0.05f)
                foreach (var lamp in Lamps)
                {
                    Vector2 ramp = LightMath.BandedGateRamp(lamp.Threshold, lamp.Softness);
                    float lo = Mathf.Clamp01(ramp.x), hi = Mathf.Clamp01(ramp.x + Mathf.Max(ramp.y, 1e-4f));
                    float t = Mathf.Clamp01((Mathf.Clamp01(1f - lum) - lo) / (hi - lo));
                    float shader = t * t * (3f - 2f * t);
                    Assert.AreEqual(LightMath.NightGate(lum, lamp.Threshold, lamp.Softness), shader, 1e-4f, $"lum {lum}, lamp {lamp}");
                }
            foreach (var lamp in Lamps)
            {
                Vector2 ramp = LightMath.BandedGateRamp(lamp.Threshold, lamp.Softness);
                Vector2 twice = LightMath.BandedGateRamp(ramp.x, ramp.y);
                Assert.AreEqual(ramp.x, twice.x, 1e-6f, "idempotent threshold");
                Assert.AreEqual(ramp.y, twice.y, 1e-5f, "idempotent softness");
                Assert.GreaterOrEqual(ramp.x, _profile.LampsOffDarkness, "noon stays dark on the water");
            }
        }

        [Test]
        public void TheBand_IsTheProfilesData()
        {
            Assert.Greater(_profile.LampsOnDarkness, _profile.LampsOffDarkness, "a band that switches on before it switches off");
            Assert.AreEqual(DayNightProfile.DefaultLampsOffDarkness, _profile.LampsOffDarkness);
            Assert.AreEqual(DayNightProfile.DefaultLampsOnDarkness, _profile.LampsOnDarkness);
        }
    }
}
