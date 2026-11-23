using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A bird in the air is flat to the sun.</b> The ground-shadow rule treats every caster as an upright
    /// standee and stretches it along the sun by up to <see cref="SpriteShadowProfile.MaxLength"/> x its
    /// height near sunset. A flying gull's outline is a few pixels tall, so at 20:30 its shadow became a
    /// 2.5 m sliver drawn at its ground point while the bird itself was lifted up the screen: a faint dark
    /// line across the water nobody could read as a bird. These pin the fix: a caster with a height above
    /// the ground keeps (about) its noon shape at every hour and fades with height, and a caster standing on
    /// the ground or a post is exactly as it was.
    /// </summary>
    public class FlyingBirdShadowTests
    {
        static readonly int IdSunDir = Shader.PropertyToID("_SunDir");
        static readonly int IdSunElevation = Shader.PropertyToID("_SunElevation");
        static readonly int IdShadowStrength = Shader.PropertyToID("_ShadowStrength");
        static readonly int IdShadowLen = Shader.PropertyToID("_ShadowLen");
        static readonly int IdShadowColor = Shader.PropertyToID("_ShadowColor");

        const float DuskHour = 20.5f;
        const float GullCruiseMetres = 8f;

        readonly List<Object> _owned = new();
        Vector4 _sunDir;
        float _sunElevation, _shadowStrength;

        [SetUp]
        public void SetUp()
        {
            _sunDir = Shader.GetGlobalVector(IdSunDir);
            _sunElevation = Shader.GetGlobalFloat(IdSunElevation);
            _shadowStrength = Shader.GetGlobalFloat(IdShadowStrength);
        }

        [TearDown]
        public void TearDown()
        {
            Shader.SetGlobalVector(IdSunDir, _sunDir);
            Shader.SetGlobalFloat(IdSunElevation, _sunElevation);
            Shader.SetGlobalFloat(IdShadowStrength, _shadowStrength);
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            SpriteShadow.SharedProfile = null;   // static: never leak a test's profile
        }

        static SpriteShadowProfile Shipped()
        {
            var asset = Resources.Load<SpriteShadowProfile>(SpriteShadow.ProfileResourcePath);
            Assert.IsNotNull(asset, "Resources/SpriteShadowProfile.asset must ship");
            return asset;
        }

        static float SunElevationAt(float hour)
        {
            var day = Resources.Load<DayNightProfile>("DayNightProfile");
            float sunrise = day != null ? day.SunriseHour : DaylightHours.DefaultSunriseHour;
            float sunset = day != null ? day.SunsetHour : DaylightHours.DefaultSunsetHour;
            return DayNightMath.SunElevation(hour, sunrise, sunset);
        }

        static float StandingMultiplier(SpriteShadowProfile p, float elevation)
            => DayNightMath.ShadowLength(elevation, p.LengthAtNoon, p.LengthAtHorizon, p.MaxLength);

        // ── the pure rule ────────────────────────────────────────────────────────────────────────────

        [Test]
        public void AtDusk_AFlyingCaster_RakesNoLongerThanTheNoonStub()
        {
            var p = Shipped();
            float standing = StandingMultiplier(p, SunElevationAt(DuskHour));
            Assert.Greater(standing, 2f * p.LengthAtNoon,
                "sanity: at 20:30 the standing rake is the long dusk stretch (the bug's precondition)");

            float aloft = SpriteShadow.AloftLengthMultiplier(
                standing, SpriteShadow.AloftWeight(GullCruiseMetres, p.AloftBlendMetres), p.AloftMaxLength);
            Assert.AreEqual(Mathf.Min(standing, p.AloftMaxLength), aloft, 1e-5f, "a bird in the air takes no dusk stretch");
            Assert.LessOrEqual(aloft, p.LengthAtNoon + 1e-5f, "the shipped cap is the noon stub: the same shape all day");
        }

        [Test]
        public void AStandingCaster_IsUntouched_AtEveryHour()
        {
            var p = Shipped();
            for (float hour = 5f; hour <= 22f; hour += 0.25f)
            {
                float standing = StandingMultiplier(p, SunElevationAt(hour));
                float w = SpriteShadow.AloftWeight(0f, p.AloftBlendMetres);
                Assert.AreEqual(0f, w, "height 0 is standing");
                Assert.AreEqual(standing, SpriteShadow.AloftLengthMultiplier(standing, w, p.AloftMaxLength), 0f,
                    $"hour {hour}: a gull on the ground or a post keeps the normal standing shadow");
                Assert.AreEqual(1f, SpriteShadow.AloftAlphaScale(0f, p.AloftFadeMetres, p.AloftFarAlpha), 0f,
                    "a standing caster keeps all its darkness");
            }
        }

        [Test]
        public void AtNoon_AFlyingCaster_IsTheSameAsStanding()
        {
            var p = Shipped();
            float noon = StandingMultiplier(p, 1f);
            float aloft = SpriteShadow.AloftLengthMultiplier(noon, 1f, p.AloftMaxLength);
            Assert.AreEqual(noon, aloft, 1e-5f, "the cap never lengthens a shadow and does not bind at noon");
        }

        [Test]
        public void TakeOffAndLanding_EaseBetweenStandingAndFlat_WithNoJump()
        {
            const float blend = 0.5f;
            float prev = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float h = blend * 1.2f * i / 100f;
                float w = SpriteShadow.AloftWeight(h, blend);
                Assert.GreaterOrEqual(w, prev - 1e-6f, "weight never falls as the bird climbs");
                Assert.Less(w - prev, 0.05f, $"no jump at {h:0.000} m");
                prev = w;
            }
            Assert.AreEqual(1f, SpriteShadow.AloftWeight(blend, blend), 1e-6f, "fully flat at the blend height");
            Assert.AreEqual(1f, SpriteShadow.AloftWeight(0.01f, 0f), 0f, "blend 0 = flat once off the ground");
            Assert.AreEqual(0f, SpriteShadow.AloftWeight(-1f, blend), 0f, "a negative height is standing");
        }

        [Test]
        public void AFlyingShadow_FadesWithHeight_ToTheFarAlpha()
        {
            Assert.AreEqual(1f, SpriteShadow.AloftAlphaScale(0f, 14f, 0.5f), 0f);
            Assert.AreEqual(0.75f, SpriteShadow.AloftAlphaScale(7f, 14f, 0.5f), 1e-5f, "linear between");
            Assert.AreEqual(0.5f, SpriteShadow.AloftAlphaScale(14f, 14f, 0.5f), 1e-5f);
            Assert.AreEqual(0.5f, SpriteShadow.AloftAlphaScale(40f, 14f, 0.5f), 1e-5f, "held above the fade height");
            Assert.AreEqual(0.5f, SpriteShadow.AloftAlphaScale(1f, 0f, 0.5f), 1e-5f, "fade 0 = far once airborne");
        }

        [Test]
        public void TheShippedAsset_CarriesTheAloftDials_AtTheirCodeDefaults()
        {
            var p = Shipped();
            var fresh = ScriptableObject.CreateInstance<SpriteShadowProfile>();
            _owned.Add(fresh);
            Assert.AreEqual(fresh.AloftMaxLength, p.AloftMaxLength, 1e-6f, "AloftMaxLength");
            Assert.AreEqual(fresh.AloftBlendMetres, p.AloftBlendMetres, 1e-6f, "AloftBlendMetres");
            Assert.AreEqual(fresh.AloftFadeMetres, p.AloftFadeMetres, 1e-6f, "AloftFadeMetres");
            Assert.AreEqual(fresh.AloftFarAlpha, p.AloftFarAlpha, 1e-6f, "AloftFarAlpha");
            Assert.Greater(p.AloftFarAlpha, 0f, "a flying bird still casts a shadow");
            Assert.Less(p.AloftFarAlpha, 1f, "and a high one is fainter than a low one");
        }

        // ── the component, end to end (headless: the published property block) ──────────────────────

        SpriteShadow Caster(float heightAboveGround)
        {
            var texture = new Texture2D(16, 8);
            _owned.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 8), new Vector2(0.5f, 0f), 32f);
            sprite.name = "gull_fly_0_d5";
            _owned.Add(sprite);
            var go = new GameObject("gull");
            _owned.Add(go);
            go.AddComponent<SpriteRenderer>().sprite = sprite;
            var shadow = go.AddComponent<SpriteShadow>();
            // Not [ExecuteAlways]: EditMode wakes it by hand, as the lamp-shadow tests do.
            Invoke(shadow, "Awake");
            shadow.HeightAboveGround = heightAboveGround;
            Invoke(shadow, "Tick");
            return shadow;
        }

        static void Invoke(SpriteShadow s, string method)
        {
            MethodInfo m = typeof(SpriteShadow).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(m, Is.Not.Null, $"SpriteShadow.{method}() not found (private API moved?)");
            m.Invoke(s, null);
        }

        static MaterialPropertyBlock Block(SpriteShadow s)
        {
            var child = s.transform.Find("SpriteShadow").GetComponent<SpriteRenderer>();
            var block = new MaterialPropertyBlock();
            child.GetPropertyBlock(block);
            return block;
        }

        [Test]
        public void TheComponent_AtDusk_DrawsAFlyingGullAtTheNoonLength_AndAStandingOneAtTheDuskLength()
        {
            var p = Shipped();
            SpriteShadow.SharedProfile = p;
            float elevation = SunElevationAt(DuskHour);
            Shader.SetGlobalVector(IdSunDir, new Vector4(-1f, 0f, 0f, 0f));   // sun in the west, shadow east
            Shader.SetGlobalFloat(IdSunElevation, elevation);
            Shader.SetGlobalFloat(IdShadowStrength, 1f);

            var standing = Caster(0f);
            var flying = Caster(GullCruiseMetres);
            float height = standing.GetComponent<SpriteRenderer>().sprite.bounds.size.y;

            float standLen = Block(standing).GetFloat(IdShadowLen);
            float flyLen = Block(flying).GetFloat(IdShadowLen);
            Assert.AreEqual(StandingMultiplier(p, elevation) * height, standLen, 1e-4f, "standing: the dusk rake as before");
            Assert.AreEqual(p.AloftMaxLength * height, flyLen, 1e-4f, "flying: the noon stub, the bird's own shape");

            float standA = Block(standing).GetColor(IdShadowColor).a;
            float flyA = Block(flying).GetColor(IdShadowColor).a;
            Assert.AreEqual(standA * SpriteShadow.AloftAlphaScale(GullCruiseMetres, p.AloftFadeMetres, p.AloftFarAlpha),
                flyA, 1e-4f, "a bird in the air casts a fainter shadow the higher it is");
        }
    }
}
