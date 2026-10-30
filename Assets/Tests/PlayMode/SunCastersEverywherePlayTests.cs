using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>Scene presence gates run without a GPU; the opt-in plate pass uses the same installer.</summary>
    public class SunCastersEverywherePlayTests
    {
        const string Output = "artifacts/shadows/a";
        readonly List<Object> _owned = new();
        readonly List<Camera> _disabledCameras = new();
        SunCasterInstaller _host;
        Camera _camera;
        RenderTexture _target;
        bool _logIgnore;
        float _timeScale;

        [SetUp] public void SetUp()
        {
            _logIgnore = LogAssert.ignoreFailingMessages;
            _timeScale = Time.timeScale;
            _host = SunCasterInstaller.Instance;
            Assert.That(_host, Is.Not.Null, "The runtime initializer must install the host itself.");
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (_host != null) { _host.SetCasting(true); _host.enabled = true; }
            if (_camera != null) _camera.targetTexture = null;
            if (_target != null) { _target.Release(); Object.Destroy(_target); }
            foreach (var camera in _disabledCameras) if (camera != null) camera.enabled = true;
            _disabledCameras.Clear();
            foreach (var obj in _owned) if (obj != null) Object.Destroy(obj);
            _owned.Clear();
            var clean = SceneManager.GetSceneByName("SunCasterAuditCleanup");
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene("SunCasterAuditCleanup");
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != clean && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            LogAssert.ignoreFailingMessages = _logIgnore;
            Time.timeScale = _timeScale;
        }

        [UnityTest] public IEnumerator Audit_StPeters_HousesVillagersAndPierCast()
            => Audit("StPeters", 500, 1, 1, 1);
        [UnityTest] public IEnumerator Audit_NineMileCreek_HousesVillagersAndQuayCast()
            => Audit("NineMileCreek", 700, 1, 1, 1);
        [UnityTest] public IEnumerator Audit_Cumberland_VillagersGainCasters()
            => Audit("Cumberland", 8, 1, 0, 0);
        [UnityTest] public IEnumerator Audit_RicePoint_StandingFiguresCast()
            => Audit("RicePoint", 1, 0, 0, 0);
        [UnityTest] public IEnumerator Audit_Greywick_LegacyTreesAndBuildingsCast()
            => Audit("Greywick", 20, 0, 1, 0);
        [UnityTest] public IEnumerator Audit_Greybox_WaterDoesNotCast()
            => Audit("Greybox", 0, 0, 0, 0);

        IEnumerator Load(string scene)
        {
            var config = GameServices.Config;
            var clock = GameServices.Clock;
            var environment = GameServices.Environment;
            var wallet = GameServices.Wallet;
            _host.enabled = false;
            _host.SetCasting(false);
            LogAssert.ignoreFailingMessages = true; // Existing region import/decor diagnostics are unrelated.
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            for (int i = 0; i < 12; i++) yield return null;
            if (GameServices.Config == null && config != null) GameServices.Config = config;
            if (GameServices.Clock == null && clock != null) GameServices.Clock = clock;
            if (GameServices.Environment == null && environment != null) GameServices.Environment = environment;
            if (GameServices.Wallet == null && wallet != null) GameServices.Wallet = wallet;
        }

        static SpriteRenderer[] Renderers()
            => Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        IEnumerator Audit(string scene, int totalFloor, int villagerFloor, int houseFloor, int pierFloor)
        {
            yield return Load(scene);
            if (GameServices.Clock != null && GameServices.Config != null) yield return PinHour(12f);
            int before = Renderers().Count(r => r.GetComponent<SpriteShadow>() != null);
            SpriteRenderer[] population = null;
            void Complete() => population = Renderers();
            _host.ScanCompleted += Complete;
            _host.enabled = true;
            _host.RequestScan();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (population == null && Time.realtimeSinceStartup < deadline) yield return null;
            _host.enabled = false;
            _host.ScanCompleted -= Complete;
            Assert.That(population, Is.Not.Null, "Wait for the installer's complete loaded-and-persistent scan.");
            _host.SetCasting(true);
            var rules = Resources.Load<SunCasterRules>(nameof(SunCasterRules));
            int total = 0, villagers = 0, houses = 0, piers = 0, excluded = 0;
            var rows = new List<string>();
            foreach (var renderer in population)
            {
                if (renderer == null) continue;
                var shadow = renderer.GetComponent<SpriteShadow>();
                if (shadow == null)
                {
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy && rules.Qualifies(renderer, out _))
                        Assert.Fail(scene + ": eligible sprite was missed: " + renderer.name);
                    continue;
                }
                total++;
                if (renderer.GetComponent<NpcFigureStand>() != null) villagers++;
                string kind = ClassOf(renderer, rules);
                if (kind == "building") houses++;
                if (kind == "pier") piers++;
                bool forbidden = renderer.sortingOrder >= SortingBands.SunShadePool ||
                    SunCasterRules.Matches(renderer.name, "*Seabed*") ||
                    SunCasterRules.Matches(renderer.name, "Sea") ||
                    SunCasterRules.Matches(renderer.name, "*Shadow*") || rules.IsExcludedArt(renderer);
                if (forbidden) excluded++;
                rows.Add($"{renderer.name}\t{kind}\t{renderer.sortingOrder}\t{renderer.transform.position}");
            }
            Assert.That(total, Is.GreaterThanOrEqualTo(totalFloor), scene + ": total standing-caster floor");
            Assert.That(villagers, Is.GreaterThanOrEqualTo(villagerFloor), scene + ": villager floor");
            Assert.That(houses, Is.GreaterThanOrEqualTo(houseFloor), scene + ": house floor");
            Assert.That(piers, Is.GreaterThanOrEqualTo(pierFloor), scene + ": pier face/deck floor");
            Assert.That(excluded, Is.Zero, scene + ": water, seabed and shade must have no caster");
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Path.Combine(Output, "census-" + scene + ".tsv"),
                new[] { $"before\t{before}\nafter\t{total}\nvillagers\t{villagers}\nhouses\t{houses}\npier\t{piers}", "name\tclass\torder\tposition" }.Concat(rows));
        }

        static string ClassOf(SpriteRenderer renderer, SunCasterRules rules)
        {
            if (renderer.GetComponent<NpcFigureStand>() != null) return "figure";
            foreach (var rule in rules.Classes)
                foreach (string pattern in rule.Names)
                    if (SunCasterRules.Matches(renderer.name, pattern) ||
                        (renderer.sprite != null && SunCasterRules.Matches(renderer.sprite.name, pattern))) return rule.Id;
            return "prop";
        }

        [UnityTest] public IEnumerator LateArrival_IsFoundByTheRunningHost()
        {
            var arrivalScene = SceneManager.CreateScene("SunCasterArrivalFixture");
            var texture = new Texture2D(16, 64); _owned.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 64), Vector2.zero, 32f); _owned.Add(sprite);
            var go = new GameObject("ArrivalCrate"); _owned.Add(go);
            SceneManager.MoveGameObjectToScene(go, arrivalScene);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sortingOrder = SortingBands.DecorBase;
            float until = Time.realtimeSinceStartup + Resources.Load<SunCasterRules>(nameof(SunCasterRules)).ArrivalScanSeconds * 4f;
            while (go.GetComponent<SpriteShadow>() == null && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(go.GetComponent<SpriteShadow>(), Is.Not.Null);
            Assert.That(go.GetComponents<SpriteShadow>(), Has.Length.EqualTo(1));
        }

        [UnityTest] public IEnumerator CapeIslander_WalkingDeckKeepsSpriteCaster_AtHelmItStops()
        {
            yield return Load("StPeters");
            yield return BuildDeckFixture();
            AssertDeckCaster();
        }

        SpriteRenderer _fisher;
        DeckWalkController _deckWalk;
        BoatController _boat;
        ControlSwitcher _switcher;

        IEnumerator BuildDeckFixture()
        {
#if UNITY_EDITOR
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/CapeIslander.asset");
            var character = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>("Assets/_Project/Data/Characters/FisherIso.asset");
            Assert.That(hull, Is.Not.Null);
            Assert.That(hull.Visual, Is.Not.Null);
            var boatGo = new GameObject("ShadowCapeIslander"); _owned.Add(boatGo);
            boatGo.transform.position = new Vector3(188f, -12f, 0f);
            _boat = boatGo.AddComponent<BoatController>();
            _boat.SetHull(hull);
            BoatHullSkinner.Apply(boatGo, hull.Visual, _boat,
                new BoatHullSkinner.Options { SkipWaveMotion = true, SkipOars = true });
            _boat.enabled = false;
            var rb = boatGo.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            var input = boatGo.AddComponent<DevBoatInput>(); input.enabled = false;
            var playerGo = new GameObject("Player"); _owned.Add(playerGo);
            _fisher = playerGo.AddComponent<SpriteRenderer>();
            _fisher.sortingOrder = SortingBands.DecorBase;
            var art = playerGo.AddComponent<IsoCharacterSprite>(); art.Configure(character);
            var walk = playerGo.AddComponent<PlayerWalkController>();
            _deckWalk = playerGo.AddComponent<DeckWalkController>(); _deckWalk.enabled = false;
            var riderGo = new GameObject("DeckRider"); riderGo.transform.SetParent(playerGo.transform, false);
            var riderRenderer = riderGo.AddComponent<SpriteRenderer>(); riderRenderer.enabled = false;
            playerGo.AddComponent<DeckRiderVisual>().Configure(riderRenderer, _fisher, art);
            var presenter = playerGo.GetComponent<DeckRiderMeshPresenter>();
            Assert.That(presenter, Is.Not.Null, "The rider's presentation service already installed its presenter.");
            presenter.Configure(character.Skin);
            var switchGo = new GameObject("ShadowDeckSwitcher"); _owned.Add(switchGo);
            _switcher = switchGo.AddComponent<ControlSwitcher>();
            _switcher.Configure(walk, _boat, input, null, 3f, null);
            _switcher.ConfigureHelm(new Vector2(-0.5f, -3f), 0.75f);
            _switcher.ConfigureBoardingMove(false, 1f, 1f, 1f, 1f);
            playerGo.transform.position = boatGo.transform.position;
            PlayerShadowInstaller.Attach(playerGo);
            _host.Consider(_fisher);
            yield return null;
            Assert.That(_switcher.TryInteract(), Is.True, "Board the real Cape Islander deck.");
            Assert.That(_switcher.Mode, Is.EqualTo(ControlMode.OnDeck));
            _deckWalk.SnapTo(new Vector2(-0.5f, -3f));
            for (int i = 0; i < 3; i++) yield return null;
#else
            Assert.Ignore("Committed boat assets are loaded through the editor test runner.");
            yield return null;
#endif
        }

        void AssertDeckCaster()
        {
            var shadow = _fisher.GetComponent<SpriteShadow>();
            Assert.That(_deckWalk.enabled, Is.True);
            Assert.That(DeckStance.IsActive, Is.True, "The real deck controller publishes the walking stance.");
            Assert.That(shadow, Is.Not.Null);
            Assert.That(shadow.TryGetLampShadowCaster(out _), Is.True, "The caster has a live sprite silhouette.");
            Assert.That(_switcher.TryInteract(), Is.True, "Take the helm from the configured deck station.");
            Assert.That(_switcher.Mode, Is.EqualTo(ControlMode.Aboard));
            Assert.That(_fisher.enabled, Is.False, "Taking the helm disables the body's sprite renderer.");
            Assert.That(shadow.TryGetLampShadowCaster(out _), Is.False);
            Assert.That(_switcher.TryInteract(), Is.True, "Release the helm back onto the deck.");
            Assert.That(_switcher.Mode, Is.EqualTo(ControlMode.OnDeck));
            Assert.That(_deckWalk.enabled, Is.True);
        }

        [UnityTest] public IEnumerator ProofFrames_OffOn()
        {
            if (System.Environment.GetEnvironmentVariable("HH_SHADOWS_A_PLATES") != "1" ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Opt-in GPU proof; scene counts and deck presence run separately in CI.");
            Directory.CreateDirectory(Path.Combine(Output, "plates"));
            foreach (string scene in new[] { "StPeters", "NineMileCreek", "Cumberland", "RicePoint", "Greywick", "Greybox" })
            {
                yield return Load(scene);
                _host.ScanLoadedScenes();
                SetUpCamera();
                Vector2 centre = scene == "StPeters" ? new Vector2(-8f, 44f) :
                    scene == "NineMileCreek" ? new Vector2(120f, 81f) :
                    scene == "Cumberland" ? new Vector2(240f, 53f) :
                    (Vector2)(GameObject.Find("Player")?.transform.position ?? Vector3.zero);
                yield return PinHour(17f);
                if (scene != "StPeters") yield return Pair(scene == "NineMileCreek" ? "nmc-quay-1700" : scene.ToLowerInvariant() + "-1700", centre, 16.875f);
                if (scene == "StPeters")
                {
                    foreach (float hour in new[] { 8f, 12f, 17f })
                    {
                        yield return PinHour(hour);
                        yield return Pair("stp-street-" + ((int)hour).ToString("00") + "00", new Vector2(-8f, 44f), 16.875f);
                    }
                    foreach (float hour in new[] { 8f, 17f })
                    {
                        yield return PinHour(hour);
                        yield return Pair("stp-pier-" + ((int)hour).ToString("00") + "00", new Vector2(191f, 0f), 16.875f);
                    }
                    yield return BuildDeckFixture();
                    yield return PinHour(17f);
                    AssertDeckCaster();
                    yield return Pair("cape-deck-1700", _boat.transform.position, 12f);
                }
                if (_camera != null) { _camera.targetTexture = null; Object.Destroy(_camera.gameObject); _camera = null; }
            }
        }

        void SetUpCamera()
        {
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera.enabled) { _disabledCameras.Add(camera); camera.enabled = false; }
            var go = new GameObject("SunShadowProofCamera"); _owned.Add(go);
            go.tag = "MainCamera";
            _camera = go.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.12f, 0.18f, 0.22f);
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            if (_target == null)
            {
                _target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
                _target.Create();
            }
            _camera.targetTexture = _target;
        }

        IEnumerator PinHour(float hour)
        {
            Time.timeScale = 1f;
            Assert.That(GameServices.Clock, Is.Not.Null, "A real region clock must publish the light.");
            Assert.That(GameServices.Config, Is.Not.Null);
            GameServices.Clock.SeekTo((1.0 + hour / 24.0) * GameServices.Config.SecondsPerDay);
            GameServices.Clock.TimeScale = 0f;
            GameServices.OpeningCinematicRunning = false;
            float until = Time.realtimeSinceStartup + 1.1f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.That(GameServices.Clock.HourOfDay, Is.EqualTo(hour).Within(0.01f));
            Time.timeScale = 0f;
        }

        IEnumerator Pair(string name, Vector2 centre, float height)
        {
            _camera.transform.position = new Vector3(centre.x, centre.y, -100f);
            _camera.orthographicSize = height / 2f;
            _host.SetCasting(false);
            for (int i = 0; i < 3; i++) yield return null;
            var before = Capture();
            _host.SetCasting(true);
            for (int i = 0; i < 3; i++) yield return null;
            var after = Capture();
            var pixels = new Color32[1920 * 2 * 1080];
            for (int y = 0; y < 1080; y++)
            {
                Array.Copy(before, y * 1920, pixels, y * 3840, 1920);
                Array.Copy(after, y * 1920, pixels, y * 3840 + 1920, 1920);
            }
            var sheet = new Texture2D(3840, 1080, TextureFormat.RGBA32, false);
            sheet.SetPixels32(pixels); sheet.Apply();
            File.WriteAllBytes(Path.Combine(Output, "plates", "compare-" + name + ".png"), sheet.EncodeToPNG());
            Object.Destroy(sheet);
            File.WriteAllText(Path.Combine(Output, "plates", name + ".txt"),
                $"Left: installer OFF. Right: ON. Same frozen world, camera, station and clock.\ncentre={centre}; height={height}; hour={GameServices.Clock.HourOfDay}\nStructural budget: one shared-material silhouette submission per caster.\n");
        }

        Color32[] Capture()
        {
            _camera.Render();
            var old = RenderTexture.active; RenderTexture.active = _target;
            var texture = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
            var result = texture.GetPixels32(); Object.Destroy(texture);
            RenderTexture.active = old;
            return result;
        }

    }
}
