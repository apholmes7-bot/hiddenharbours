using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.App.Editor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The owner's day/night lever exists, and the fallback still agrees with it</b> (water fidelity
    /// PR 4, the 2026-09-02 night ruling).
    ///
    /// <para><c>DayNightController</c> has always read <c>Resources/DayNightProfile</c> and fallen back to
    /// <see cref="DayNightProfile.CreateDefault"/> when it found none — and it always found none, because
    /// the asset was never authored. The night was a constant in code and rule 6's "the owner tunes it in
    /// an inspector" was not true of the single most look-defining number in the game. The asset now ships.</para>
    ///
    /// <para><b>Two properties, and the second is the one that rots.</b> That the asset EXISTS and is
    /// loadable by the name the controller uses; and that it still says what
    /// <see cref="DayNightProfile.ApplyDefaults"/> says. The moment those two part, the look depends on
    /// which path ran — the asset in a built player, the code default in a fixture that loads without
    /// Resources — and nothing tells you. This is the <c>GameConfig</c>-is-behind-the-code family one layer
    /// over, and it is caught here by comparing them rather than by hoping.</para>
    ///
    /// <para>⚠️ The comparison is DELIBERATELY not a whole-object equality: the owner is expected to tune
    /// this asset, and a test that demanded byte-equality with the code would go red the first evening he
    /// did. It pins the values a FEATURE depends on — the moonlight the ruling moved, and the shape of the
    /// night — and leaves his art direction alone.</para>
    /// </summary>
    public class DayNightProfileAssetTests
    {
        [Test]
        public void TheProfileAsset_Ships_AndLoadsByTheNameTheControllerUses()
        {
            var byPath = AssetDatabase.LoadAssetAtPath<DayNightProfile>(DayNightProfileBuilder.ProfilePath);
            Assert.IsNotNull(byPath,
                $"{DayNightProfileBuilder.ProfilePath} is missing. It is the owner's only lever on the " +
                "night (rule 6); run Hidden Harbours ▸ Lighting ▸ Create the Day-Night Profile.");

            var byName = Resources.Load<DayNightProfile>("DayNightProfile");
            Assert.IsNotNull(byName,
                "the profile must load by the RESOURCE NAME DayNightController asks for, not merely exist " +
                "at a path — it has to be under a Resources folder and named DayNightProfile");
            Assert.AreSame(byPath, byName, "the shipped asset and the loaded resource must be one object");
        }

        [Test]
        public void TheShippedAsset_AndTheCodeFallback_StillAgree()
        {
            var shipped = Resources.Load<DayNightProfile>("DayNightProfile");
            Assert.IsNotNull(shipped, "the profile must ship — see the test above");

            DayNightProfile fallback = DayNightProfile.CreateDefault();
            try
            {
                Assert.AreEqual(fallback.MoonlightLiftMax, shipped.MoonlightLiftMax, 1e-5f,
                    "the moonlight lift must be the same in the asset and in the fallback — this is the " +
                    "number the 2026-09-02 ruling moved, and a scene that loads without Resources must " +
                    "still get the ruled night");
                Assert.AreEqual(fallback.MoonlightTint, shipped.MoonlightTint, "…and the colour it lifts toward");
                Assert.AreEqual(fallback.SunriseHour, shipped.SunriseHour, 1e-5f, "…the day's length (sunrise)");
                Assert.AreEqual(fallback.SunsetHour, shipped.SunsetHour, 1e-5f, "…the day's length (sunset)");
                Assert.AreEqual(fallback.WeatherDimMax, shipped.WeatherDimMax, 1e-5f,
                    "…and how far cloud may dim it, which is what hides the moon");

                // The night's own darkness, read through the maths rather than off a curve key: this is the
                // floor the ruling says must stay dark, so it is compared where it is actually used.
                Color assetNight = DayNightMath.DayNightTint(2f, shipped, 1f, 0f);
                Color codeNight = DayNightMath.DayNightTint(2f, fallback, 1f, 0f);
                Assert.AreEqual(codeNight.r, assetNight.r, 1e-4f, "the 02:00 tint must agree (r)");
                Assert.AreEqual(codeNight.g, assetNight.g, 1e-4f, "the 02:00 tint must agree (g)");
                Assert.AreEqual(codeNight.b, assetNight.b, 1e-4f, "the 02:00 tint must agree (b)");
            }
            finally { Object.DestroyImmediate(fallback); }
        }

        [Test]
        public void TheMoonlessNight_IsBitwiseUnMOVEDByTheRuling()
        {
            // ⭐ The half of the ruling that is an ABSENCE. Raising the lift must not have lightened the
            // nights the radar and the lamp exist for: every factor multiplies, so a new moon, a set moon
            // and full overcast each drive the lift to exactly 0 and the tint is the moonless computation,
            // bit for bit — not "close to". Asserted against the four-argument overload, which is the
            // moonless computation by definition.
            var profile = Resources.Load<DayNightProfile>("DayNightProfile");
            Assert.IsNotNull(profile, "the profile must ship — see the test above");

            Color moonless = DayNightMath.DayNightTint(2f, profile, 1f, 0f);
            Assert.AreEqual(moonless, DayNightMath.DayNightTint(2f, profile, 1f, 0f, 0f, 1f),
                "a NEW moon high in a clear sky must leave the night bitwise unchanged");
            Assert.AreEqual(moonless, DayNightMath.DayNightTint(2f, profile, 1f, 0f, 1f, 0f),
                "a FULL moon below the horizon must leave the night bitwise unchanged");

            Color overcast = DayNightMath.DayNightTint(2f, profile, 0.1f, 0f);
            Assert.AreEqual(overcast, DayNightMath.DayNightTint(2f, profile, 0.1f, 0f, 1f, 1f),
                "cloud must hide the moon bitwise — the owner's \"brighter if not cloudy\", from the " +
                "other side");
        }

        [Test]
        public void AClearFullMoon_ActuallyLiftsTheNight_ByTheRuledAmount()
        {
            var profile = Resources.Load<DayNightProfile>("DayNightProfile");
            Assert.IsNotNull(profile, "the profile must ship — see the test above");

            Color moonless = DayNightMath.DayNightTint(2f, profile, 1f, 0f);
            Color moonlit = DayNightMath.DayNightTint(2f, profile, 1f, 0f, 1f, 1f);
            float dark = 0.299f * moonless.r + 0.587f * moonless.g + 0.114f * moonless.b;
            float lit = 0.299f * moonlit.r + 0.587f * moonlit.g + 0.114f * moonlit.b;

            Debug.Log($"[night] 02:00 clear: moonless tint ({moonless.r:F3}, {moonless.g:F3}, {moonless.b:F3}) " +
                      $"luma {dark:F4}; full moon at peak ({moonlit.r:F3}, {moonlit.g:F3}, {moonlit.b:F3}) " +
                      $"luma {lit:F4} — x{lit / Mathf.Max(dark, 1e-6f):F1}.");

            Assert.Greater(lit, dark * 5f,
                $"a clear full moon at peak must be a night you can steer by — it lifted the 02:00 luma " +
                $"from {dark:F4} to {lit:F4}, and at the pre-ruling 0.05 that ratio was under 2");
            Assert.Greater(profile.MoonlightLiftMax, 0.2f,
                "the ruled lift must be on the asset the game loads, not only in the code");
        }
    }
}
