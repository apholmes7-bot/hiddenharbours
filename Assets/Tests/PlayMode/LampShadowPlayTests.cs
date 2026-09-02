using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The HOOKS of the lamp-shadow system (ADR 0016, lights PR B) — what edit mode cannot see: a
    /// lamp registers itself on enable and leaves on disable, a sun caster registers as a lamp
    /// caster, the self-installed system draws at night and stands down at noon, and the sun
    /// shadow a caster already throws is byte-identical whether or not a lamp is in range of it.
    ///
    /// <para>Night and noon come through the REAL <see cref="DayNightController"/> off a fixed clock
    /// (the pattern <c>SpriteShadowCastsPlayTests</c> established), because the self-installed
    /// controller republishes the tint every tick and would overwrite a hand-set global.</para>
    /// </summary>
    public class LampShadowPlayTests
    {
        private static readonly int IdShadowColor = Shader.PropertyToID("_ShadowColor");
        private static readonly int IdShadowDir   = Shader.PropertyToID("_ShadowDir");
        private static readonly int IdShadowLen   = Shader.PropertyToID("_ShadowLen");
        private static readonly int IdShadowUV    = Shader.PropertyToID("_ShadowUV");

        private readonly List<Object> _spawned = new List<Object>();

        private sealed class FixedClock : IGameClock
        {
            private readonly float _hour;
            public FixedClock(float hour) { _hour = hour; }
            public double TotalSeconds => _hour * 3600.0;
            public GameTime Now => default;
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => default;
            public bool IsMarketDay => false;
            public float HourOfDay => _hour;
            public float DayFraction => Mathf.Repeat(_hour, 24f) / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private sealed class ClearEnvironment : IEnvironmentService
        {
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Vector2.zero, Vector2.zero, 0f, SeaState.Glass, 1f);
            public float TideHeightAt(double totalSeconds) => 0f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        private void SetTheHour(float hour)
        {
            GameServices.Clock = new FixedClock(hour);
            GameServices.Environment = new ClearEnvironment();
            // The self-installed controller ticks at 10 Hz; push the new hour through a fresh one now so
            // the next frame's globals are this hour's, not a stale tick's.
            var go = new GameObject($"DayNightController (h{hour})");
            _spawned.Add(go);
            var controller = go.AddComponent<DayNightController>();
            Invoke(controller, "Tick");
        }

        private static void Invoke(Object target, string method)
        {
            MethodInfo m = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, $"{target.GetType().Name}.{method}() not found (private API moved?)");
            m.Invoke(target, null);
        }

        private SceneLight Lamp(Vector2 at)
        {
            var go = new GameObject("lamp");
            _spawned.Add(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var light = go.AddComponent<SceneLight>();
            light.Shape = SceneLight.LightShape.Radial;
            light.Range = 9f;
            light.Intensity = 1.5f;
            light.FlickerAmount = 0f;
            return light;
        }

        private SpriteShadow Caster(Vector2 at)
        {
            var tex = new Texture2D(8, 48);
            _spawned.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 48), new Vector2(0.5f, 0f), 32f);
            _spawned.Add(sprite);
            var go = new GameObject("caster");
            _spawned.Add(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            go.AddComponent<SpriteRenderer>().sprite = sprite;
            return go.AddComponent<SpriteShadow>();
        }

        private static (Vector2 dir, float len, float alpha, Vector2 uv) SunProjection(SpriteShadow caster)
        {
            Transform child = caster.transform.Find("SpriteShadow");
            Assert.IsNotNull(child, "SpriteShadow created no child renderer");
            var sr = child.GetComponent<SpriteRenderer>();
            var mpb = new MaterialPropertyBlock();
            sr.GetPropertyBlock(mpb);
            Vector4 dir = mpb.GetVector(IdShadowDir);
            Vector4 uv = mpb.GetVector(IdShadowUV);
            return (new Vector2(dir.x, dir.y), mpb.GetFloat(IdShadowLen), mpb.GetColor(IdShadowColor).a, new Vector2(uv.x, uv.y));
        }

        // =====================================================================================

        [UnityTest]
        public IEnumerator ALamp_RegistersOnEnable_AndLeavesOnDisable()
        {
            int before = LampShadowSystem.LiveLightCount;
            SceneLight lamp = Lamp(Vector2.zero);
            yield return null;
            Assert.AreEqual(before + 1, LampShadowSystem.LiveLightCount, "a lit lamp registers itself");

            lamp.enabled = false;
            yield return null;
            Assert.AreEqual(before, LampShadowSystem.LiveLightCount, "and leaves when it goes dark");
        }

        [UnityTest]
        public IEnumerator ASunCaster_IsALampCasterToo_UntilItIsDisabled()
        {
            int before = LampShadowSystem.LiveCasterCount;
            SpriteShadow caster = Caster(Vector2.zero);
            yield return null;
            Assert.AreEqual(before + 1, LampShadowSystem.LiveCasterCount, "every SpriteShadow registers as a lamp caster");

            caster.gameObject.SetActive(false);
            yield return null;
            Assert.AreEqual(before, LampShadowSystem.LiveCasterCount);
        }

        [UnityTest]
        public IEnumerator AtNight_TheSystemDrawsAShadow_AndAtNoonItStandsDown()
        {
            Assert.IsNotNull(LampShadowSystem.Instance, "the system self-installs before the first scene");
            Lamp(new Vector2(-3f, 0f));
            Caster(Vector2.zero);

            SetTheHour(2f);
            yield return new WaitForSeconds(0.3f);   // past a 10 Hz pairing tick
            Assert.GreaterOrEqual(LampShadowSystem.Instance.ActiveShadowCount, 1,
                "a lit lamp beside a caster at 02:00 throws at least one shadow");

            SetTheHour(13f);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0, LampShadowSystem.Instance.ActiveShadowCount,
                "at noon the gate is shut: the noon control — no lamp shadows, and the sun's are untouched");
        }

        /// <summary>
        /// The sun shadow is <see cref="SpriteShadow"/>'s own quad and block; the lamp system draws
        /// its own pooled quads and never writes to the caster's. So every value the sun shadow
        /// pushes must be identical with a lamp in range and without one.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSunShadow_IsUntouchedByALampInRange()
        {
            SpriteShadow caster = Caster(Vector2.zero);
            SetTheHour(8f);   // a low morning sun: a real rake to compare
            yield return null;
            Invoke(caster, "Tick");
            var alone = SunProjection(caster);
            Assert.Greater(alone.alpha, 0f, "precondition: the sun shadow is live");

            Lamp(new Vector2(-3f, 0f));
            yield return new WaitForSeconds(0.3f);
            Invoke(caster, "Tick");
            var withLamp = SunProjection(caster);

            Assert.AreEqual(alone.dir, withLamp.dir, "the sun shadow's direction");
            Assert.AreEqual(alone.len, withLamp.len, "its length");
            Assert.AreEqual(alone.alpha, withLamp.alpha, "its alpha");
            Assert.AreEqual(alone.uv, withLamp.uv, "its pivot map — byte-identical with a lamp in range");
        }
    }
}
