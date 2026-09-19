using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A villager ashore on St Peters still draws as their sprite, exactly as before the cast</b> (ADR 0044,
    /// amendment 2026-09-17, option (b): "Villagers stay sprites ashore").
    ///
    /// <para>The island loads as it ships, with the committed <see cref="GameConfig"/> (MeshCast ON) and Art's
    /// figure service registered. Only a stand asks for a figure, and the only stand is a moored boat, so no
    /// villager may carry a presenter or a figure, and no villager's sprite may be hidden. The presenter hides
    /// a sprite through <c>forceRenderingOff</c> and never touches <c>enabled</c>. The routine's shelter owns
    /// <c>enabled</c>, so a villager who is indoors is today's behaviour and is not asserted either way.</para>
    ///
    /// <para>Once Phase C links a skin to every art def, Basil Samson's committed def carries one too, and
    /// the first test proves option (b) on the real data. The second proves it already: it dresses Basil
    /// in a skinned clone of the shared def, moors a boat whose skipper wears the SAME clone, and requires a mesh
    /// aboard and a sprite ashore in one world.</para>
    /// </summary>
    public class CharacterMeshCastAshorePlayTests
    {
        private const string SceneName = "StPeters";
        private const string CleanupSceneName = "CastAshoreCleanup";
        private const string SkipperOwnerPath = "Assets/_Project/Data/Boats/Owners/ArsenaultLeo.asset";
        private const string StandInSkinPath = "Assets/_Project/Data/Characters/Skin/fisher.asset";

        /// <summary>St Peters' villager who wears the same art def as Leo Arsenault's skipper.</summary>
        private const string SharedDefVillagerName = "BasilSamson";

        /// <summary>St Peters stands six routines today. Fewer means the scene lost villagers, and a check
        /// over the rest is no longer the island the owner asked about.</summary>
        private const int VillagersOnTheIsland = 6;

        /// <summary>Far from the island, its colliders, its triggers and its people.</summary>
        private static readonly Vector2 OffshoreBerth = new Vector2(-2000f, -2000f);

        private const float SkipperHeadingDegrees = 135f;
        private const int SettleFrames = 3;

        private readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        private readonly List<Object> _spawned = new();

        private static Scene PersistentScene()
        {
            var probe = new GameObject("__ddolProbe");
            Object.DontDestroyOnLoad(probe);
            Scene s = probe.scene;
            Object.DestroyImmediate(probe);
            return s;
        }

        private static List<GameObject> PersistentRoots()
        {
            Scene s = PersistentScene();
            return s.IsValid() ? s.GetRootGameObjects().ToList() : new List<GameObject>();
        }

        [UnitySetUp]
        public IEnumerator SetUpRegion()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;
            GameServices.PendingArrivalKey = null;

            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();

            var clean = SceneManager.CreateScene(CleanupSceneName);
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == SceneName)
                    yield return SceneManager.UnloadSceneAsync(s);
            }

            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go)) Object.DestroyImmediate(go);

            GameServices.Reset();
            yield return null;
        }

        // ------------------------------------------------------------------ the tests

        [UnityTest]
        public IEnumerator EveryVillagerAshore_DrawsTheirSpriteExactlyAsToday_WithTheCastSwitchOn()
        {
            yield return LoadTheIsland();
            AssertTheCastIsLive();
            yield return Settle();

            List<IsoCharacterSprite> villagers = Villagers();
            List<string> problems = villagers.SelectMany(SpriteProblemsOf).Concat(PresentersNotAboard()).ToList();

            Assert.IsEmpty(problems,
                $"{problems.Count} problem(s) across {villagers.Count} villagers on St Peters with MeshCast ON. " +
                "Villagers stay sprites ashore (ADR 0044 §7, option (b)):\n  " + string.Join("\n  ", problems));

            TestContext.WriteLine(
                $"{villagers.Count} villagers drew as their sprites; {villagers.Count(Shows)} were out of doors, " +
                $"and {villagers.Count(v => v.Visual != null && v.Visual.Skin != null)} wear an art def that links a skin.");
        }

        [UnityTest]
        public IEnumerator TheSameSkinnedDef_IsAMeshAboard_AndTheSpriteAshore_InOneWorld()
        {
            yield return LoadTheIsland();
            AssertTheCastIsLive();

            List<IsoCharacterSprite> villagers = Villagers();
            IsoCharacterSprite basil = villagers.SingleOrDefault(v => v.name == SharedDefVillagerName);
            Assert.IsNotNull(basil, $"harness: St Peters has no villager named '{SharedDefVillagerName}'");

            BoatOwnerDef committedOwner = Load<BoatOwnerDef>(SkipperOwnerPath);
            CharacterVisualDef committedDef = committedOwner.Skipper;
            Assert.IsNotNull(committedDef, $"harness: {SkipperOwnerPath} names no skipper");
            Assert.AreSame(committedDef, basil.Visual,
                $"harness: {SharedDefVillagerName} wears '{basil.Visual?.name}', not '{committedDef.name}' like " +
                $"'{committedOwner.Id}''s skipper, so this is no longer one def in two places");
            Assert.Greater(committedOwner.AboardCount(), 0, $"harness: '{committedOwner.Id}' has no room aboard");

            CharacterSkinDef skin = committedDef.Skin != null ? committedDef.Skin : Load<CharacterSkinDef>(StandInSkinPath);
            Assert.IsTrue(skin.IsUsable(), $"harness: '{skin.Id}' is not usable, so no figure could be built from it");
            Assert.IsTrue(skin.DrawsAsMesh(CharacterSkinStateMap.Idle),
                $"harness: '{skin.Id}' does not switch on '{CharacterSkinStateMap.Idle}'");

            // One skinned def, worn in both places. Clones only: the committed assets are never written.
            var dressed = Object.Instantiate(committedDef);
            dressed.name = committedDef.name;
            dressed.Skin = skin;
            _spawned.Add(dressed);
            basil.Configure(dressed);

            var owner = Object.Instantiate(committedOwner);
            owner.name = committedOwner.name;
            owner.Skipper = dressed;
            _spawned.Add(owner);
            MooredBoat moored = Moor(owner, OffshoreBerth);

            yield return Settle();

            // The positive first: in this world, under the shipped switch, the def draws as a mesh aboard.
            Assert.IsTrue(moored.IsPresented, $"harness: '{owner.Id}' drew no hull at all");
            IsoCharacterSprite[] aboard = moored.GetComponentsInChildren<IsoCharacterSprite>(true);
            Assert.AreEqual(1, aboard.Length, $"harness: '{owner.Id}' stands {aboard.Length} characters, not one skipper");
            IsoCharacterSprite skipper = aboard[0];
            Assert.AreSame(dressed, skipper.Visual, "harness: the skipper aboard does not wear the dressed def");
            var presenter = skipper.GetComponent<CharacterFigurePresenter>();
            Assert.IsNotNull(presenter, $"'{owner.Id}''s skipper wears '{skin.Id}' and was given no figure presenter");
            Assert.AreEqual(CharacterFigurePresenter.Refusal.None, presenter.WhyNot,
                $"the skipper aboard is not drawing as their mesh on St Peters: {presenter.NotDrawingReason}");
            Assert.IsTrue(skipper.GetComponent<SpriteRenderer>().forceRenderingOff,
                "the skipper aboard draws the mesh and the sprite both");

            // The same def, ashore: the sprite, exactly as today.
            Assert.AreSame(dressed, basil.Visual, $"harness: {SharedDefVillagerName} lost the dressed def");
            List<string> problems = villagers.SelectMany(SpriteProblemsOf).Concat(PresentersNotAboard()).ToList();
            Assert.IsEmpty(problems,
                $"'{skin.Id}' draws as a mesh aboard, as it should, and {problems.Count} problem(s) ashore say the " +
                "villagers did not stay sprites (ADR 0044 §7, option (b)):\n  " + string.Join("\n  ", problems));
        }

        // ------------------------------------------------------------------ the harness

        private IEnumerator LoadTheIsland()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            for (int i = 0; i < 4; i++) yield return null;
            LogAssert.ignoreFailingMessages = false;
        }

        private static IEnumerator Settle()
        {
            for (int i = 0; i < SettleFrames; i++) yield return null;
        }

        /// <summary>The island must be running the cast as it ships, or every "still a sprite" below would
        /// pass on a switch that was never on.</summary>
        private static void AssertTheCastIsLive()
        {
            Assert.IsNotNull(GameServices.Config, "harness: St Peters loaded and published no GameConfig");
            Assert.IsTrue(GameServices.Config.MeshCast,
                "harness: the island's GameConfig has MeshCast OFF, so a sprite ashore proves nothing about the cast");
            Assert.IsInstanceOf<CharacterFigurePresentationService>(CharacterFigurePresentation.Service,
                "harness: Art's figure service is not registered on the loaded island, so no stand could ask for a figure");
        }

        private static List<IsoCharacterSprite> Villagers()
        {
            var routines = Object.FindObjectsByType<VillagerRoutine>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var villagers = new List<IsoCharacterSprite>();
            foreach (VillagerRoutine routine in routines.OrderBy(r => r.name, System.StringComparer.Ordinal))
            {
                var iso = routine.GetComponent<IsoCharacterSprite>();
                Assert.IsNotNull(iso, $"harness: villager '{routine.name}' has no IsoCharacterSprite beside their routine");
                villagers.Add(iso);
            }
            Assert.GreaterOrEqual(villagers.Count, VillagersOnTheIsland,
                $"harness: St Peters stands {villagers.Count} villagers ([{string.Join(", ", villagers.Select(v => v.name))}])");
            return villagers;
        }

        private static bool Shows(IsoCharacterSprite villager)
        {
            var sprite = villager.GetComponent<SpriteRenderer>();
            return sprite != null && sprite.enabled && villager.gameObject.activeInHierarchy;
        }

        /// <summary>Everything that would make a villager draw other than as today's sprite, by name.</summary>
        private static IEnumerable<string> SpriteProblemsOf(IsoCharacterSprite villager)
        {
            string who = $"'{villager.name}' ({(villager.Visual != null ? villager.Visual.name : "no def")})";

            if (villager.GetComponentsInChildren<CharacterFigurePresenter>(true).Length > 0)
                yield return $"{who}: carries a figure presenter";
            if (villager.GetComponentsInChildren<IsoCharacterFigureRenderer>(true).Length > 0)
                yield return $"{who}: carries a figure renderer";
            if (villager.GetComponentsInChildren<Transform>(true).Any(t => t.name == CharacterFigurePresenter.FigureObjectName))
                yield return $"{who}: has a '{CharacterFigurePresenter.FigureObjectName}' object";

            var sprite = villager.GetComponent<SpriteRenderer>();
            if (sprite == null)
            {
                yield return $"{who}: has no SpriteRenderer";
                yield break;
            }
            if (sprite.forceRenderingOff)
                yield return $"{who}: the sprite is forced off, so they draw as nothing";
            if (Shows(villager) && sprite.sprite == null)
                yield return $"{who}: out of doors with an empty sprite";
        }

        /// <summary>A moored boat's skipper is the only stand option (b) wires. A presenter anywhere else on
        /// the island is a figure that nobody should have asked for.</summary>
        private static IEnumerable<string> PresentersNotAboard() =>
            Object.FindObjectsByType<CharacterFigurePresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(p => p.GetComponentInParent<MooredBoat>(true) == null)
                .Select(p => $"a figure presenter on '{PathOf(p.transform)}', which is not aboard a moored boat");

        private static string PathOf(Transform t) =>
            t.parent == null ? t.name : $"{PathOf(t.parent)}/{t.name}";

        private static T Load<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"harness: no {typeof(T).Name} at {path}");
            return asset;
#else
            Assert.Ignore("Needs the AssetDatabase: this loads the REAL committed defs, not a mirror.");
            return null;
#endif
        }

        private MooredBoat Moor(BoatOwnerDef owner, Vector2 at)
        {
            var go = new GameObject($"Moored_{owner.Id}");
            go.SetActive(false);
            _spawned.Add(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var moored = go.AddComponent<MooredBoat>();
            moored.Configure(owner, SkipperHeadingDegrees);
            go.SetActive(true);
            return moored;
        }
    }
}
