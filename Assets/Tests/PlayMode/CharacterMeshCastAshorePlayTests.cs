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
    /// <b>A villager ashore on St Peters draws as her mesh while both cast switches are on, and as her sprite,
    /// exactly as before, while <see cref="GameConfig.MeshCastAshore"/> is off</b> (ADR 0044, amendment
    /// 2026-09-27, which supersedes option (b)'s "Villagers stay sprites ashore" for villagers).
    ///
    /// <para>The island loads as it ships, with Art's figure service registered. Each villager's
    /// <see cref="Interactable"/> gives her an <see cref="NpcFigureStand"/> at Awake, and the stand asks for her
    /// figure. The switches are then set on a runtime COPY of the loaded config, never on the asset, so these
    /// cases hold whatever the asset ships. The presenter hides a sprite through <c>forceRenderingOff</c> and
    /// never touches <c>enabled</c>. The routine's shelter owns <c>enabled</c>, so a villager indoors is
    /// asserted as sheltered, not skipped.</para>
    ///
    /// <para><b>Retired by name, with the production behaviour that died with them:</b>
    /// <c>EveryVillagerAshore_DrawsTheirSpriteExactlyAsToday_WithTheCastSwitchOn</c> held option (b)'s
    /// "villagers are not wired" (no villager carried a presenter or a figure), and
    /// <c>TheSameSkinnedDef_IsAMeshAboard_AndTheSpriteAshore_InOneWorld</c> held a villager's
    /// <c>Refusal.Ashore</c>. Each is replaced by an ON case and an OFF case.</para>
    ///
    /// <para>The adder's cases build their hosts in code, with a recording service in the locator.</para>
    /// </summary>
    public class CharacterMeshCastAshorePlayTests
    {
        private const string SceneName = "StPeters";
        private const string CleanupSceneName = "CastAshoreCleanup";
        private const string SkipperOwnerPath = "Assets/_Project/Data/Boats/Owners/ArsenaultLeo.asset";

        /// <summary>St Peters' villager who wears the same art def as Leo Arsenault's skipper.</summary>
        private const string SharedDefVillagerName = "BasilSamson";

        /// <summary>St Peters stands six routines today. Fewer means the scene lost villagers, and a check
        /// over the rest is no longer the island the owner asked about.</summary>
        private const int VillagersOnTheIsland = 6;

        /// <summary>The plates' hour, on the second day: most of the island is out of doors.</summary>
        private const double NoonHour = 12d;

        /// <summary>Far from the island, its colliders, its triggers and its people.</summary>
        private static readonly Vector2 OffshoreBerth = new Vector2(-2000f, -2000f);

        private const float SkipperHeadingDegrees = 135f;
        private const int SettleFrames = 3;
        private const float YawToleranceDegrees = 0.01f;
        private const float WalkBandMiddle = 0.5f;
        private const float RunPastThreshold = 1.5f;

        private readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        private readonly List<Object> _spawned = new();
        private bool _serviceSwapped;
        private ICharacterFigurePresentationService _serviceBefore;

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
            if (_serviceSwapped) CharacterFigurePresentation.Service = _serviceBefore;
            _serviceSwapped = false;
            _serviceBefore = null;

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

        // ------------------------------------------------------------------ every villager

        [UnityTest]
        public IEnumerator EveryVillagerAshore_IsTheirMesh_WithMeshCastAndMeshCastAshoreOn()
        {
            yield return LoadTheIsland();
            AssertTheCastIsInstalled();
            UseSwitches(meshCast: true, ashore: true);
            yield return AtNoon();

            List<IsoCharacterSprite> villagers = Villagers();
            int outOfDoors = villagers.Count(Shows);
            Assert.Greater(outOfDoors, 0,
                "harness: every villager on St Peters is indoors at noon, so nothing here would be a mesh ashore");

            List<string> problems = villagers.SelectMany(MeshProblemsOf)
                                             .Concat(DuplicateFigureIds(villagers))
                                             .Concat(PresentersNobodyAskedFor()).ToList();
            Assert.IsEmpty(problems,
                $"{problems.Count} problem(s) across {villagers.Count} villagers on St Peters with MeshCast and " +
                "MeshCastAshore ON. A villager out of doors draws as her mesh with her sprite forced off, sorted " +
                "and turned as her sprite (ADR 0044, amendment 2026-09-27):\n  " + string.Join("\n  ", problems));

            TestContext.WriteLine(
                $"{villagers.Count} villagers; {outOfDoors} out of doors drew as their meshes, " +
                $"{villagers.Count - outOfDoors} sheltered drew neither picture; facet registry: " +
                $"{IsoFacetHullRegistry.Count} hull(s), {IsoFacetHullRegistry.FigureCount} figure id(s).");
        }

        /// <summary>
        /// ⭐ <b>In every state her day uses.</b> Each of the six, brought out of doors, is held at an idling, a
        /// walking and a running speed inside her own art def's bands. Her sprite draws the gait its own sheets
        /// have at that speed (run, else walk, else idle), and her skin's state map turns that gait into her
        /// mesh's state. At each speed she draws as her mesh, sprite forced off, in exactly the state the map
        /// names. A gait her skin has no clip for draws the map's fallback, and this case names it; a state her
        /// skin does not mesh would refuse her to her sprite, and fails.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryVillagerAshore_IsTheirMesh_IdlingWalkingAndRunning()
        {
            yield return LoadTheIsland();
            AssertTheCastIsInstalled();
            UseSwitches(meshCast: true, ashore: true);
            yield return AtNoon();

            List<IsoCharacterSprite> villagers = Villagers();
            foreach (IsoCharacterSprite villager in villagers) BringHerOut(villager);
            var problems = new List<string>();
            var drawn = new List<string>();
            try
            {
                foreach (CharacterGait gait in new[] { CharacterGait.Idle, CharacterGait.Walk, CharacterGait.Run })
                {
                    foreach (IsoCharacterSprite villager in villagers) villager.HoldSpeed(SpeedFor(villager.Visual, gait));
                    yield return Settle();

                    foreach (IsoCharacterSprite villager in villagers)
                    {
                        List<string> mine = MeshProblemsOf(villager).ToList();
                        problems.AddRange(mine.Select(p => $"held at a {gait} speed: {p}"));
                        if (mine.Count > 0) continue;

                        CharacterSkinDef skin = villager.Visual.Skin;
                        IsoCharacterFigureRenderer figure = villager.GetComponent<CharacterFigurePresenter>().AshoreFigure;
                        bool resolves = CharacterSkinStateMap.Resolve(skin, villager.Stance, villager.Gait,
                                                                      out string wanted, out bool fellBack);
                        if (!resolves)
                            problems.Add($"{Who(villager)} held at a {gait} speed: her sprite draws {villager.Gait}, " +
                                         $"'{skin.Id}' resolves no state for it, and she still drew as her mesh");
                        else if (figure.DrawnStateKey != wanted)
                            problems.Add($"{Who(villager)} held at a {gait} speed: her sprite draws {villager.Gait}, " +
                                         $"'{skin.Id}' maps it to '{wanted}', and her mesh shows '{figure.DrawnStateKey}'");
                        drawn.Add($"{villager.name} at a {gait} speed: sprite {villager.Gait}, mesh " +
                                  $"'{figure.DrawnStateKey}'{(fellBack ? " (the map's fallback: the skin has no clip for it)" : "")}");
                    }
                }
            }
            finally
            {
                foreach (IsoCharacterSprite villager in villagers)
                    if (villager != null) villager.ReleaseSpeed();
            }

            Assert.IsEmpty(problems,
                $"{problems.Count} problem(s) across {villagers.Count} villagers held idling, walking and running on " +
                "St Peters with both switches ON:\n  " + string.Join("\n  ", problems));
            TestContext.WriteLine(string.Join("\n", drawn));
        }

        [UnityTest]
        public IEnumerator EveryVillagerAshore_DrawsTheirSpriteExactlyAsToday_WithMeshCastAshoreOff()
        {
            yield return LoadTheIsland();
            AssertTheCastIsInstalled();
            UseSwitches(meshCast: true, ashore: true);
            yield return AtNoon();
            int figuresOn = IsoFacetHullRegistry.FigureCount;

            // OFF with the figures already built: each gives her id back and goes, and her sprite is hers.
            UseSwitches(meshCast: true, ashore: false);
            yield return Settle();

            List<IsoCharacterSprite> villagers = Villagers();
            List<string> problems = villagers.SelectMany(SpriteProblemsOf).Concat(PresentersNobodyAskedFor()).ToList();
            Assert.IsEmpty(problems,
                $"{problems.Count} problem(s) across {villagers.Count} villagers on St Peters with MeshCastAshore " +
                "OFF. Every villager draws her sprite exactly as before the amendment, with no figure and no " +
                "facet id:\n  " + string.Join("\n  ", problems));

            TestContext.WriteLine(
                $"{villagers.Count} villagers drew as their sprites; {villagers.Count(Shows)} were out of doors; " +
                $"figure ids {figuresOn} with the switch ON -> {IsoFacetHullRegistry.FigureCount} OFF.");
        }

        // ------------------------------------------------------------------ one def, two places

        [UnityTest]
        public IEnumerator TheSameSkinnedDef_IsAMeshAboard_AndAMeshAshore_InOneWorld()
        {
            yield return LoadTheIsland();
            AssertTheCastIsInstalled();
            UseSwitches(meshCast: true, ashore: true);

            IsoCharacterSprite basil = TheSharedDefVillager(out BoatOwnerDef owner, out CharacterSkinDef skin);
            BringHerOut(basil);
            MooredBoat moored = Moor(owner, OffshoreBerth);
            yield return Settle();

            AssertTheSkipperAboardIsTheirMesh(moored, owner, skin);

            List<string> problems = MeshProblemsOf(basil).ToList();
            Assert.IsEmpty(problems,
                $"'{skin.Id}' draws as a mesh aboard, and {problems.Count} problem(s) say the same def did not draw " +
                $"as a mesh on {SharedDefVillagerName} ashore, in the same world:\n  " + string.Join("\n  ", problems));
        }

        [UnityTest]
        public IEnumerator TheSameSkinnedDef_IsAMeshAboard_AndTheSpriteAshore_WithMeshCastAshoreOff()
        {
            yield return LoadTheIsland();
            AssertTheCastIsInstalled();
            UseSwitches(meshCast: true, ashore: false);

            IsoCharacterSprite basil = TheSharedDefVillager(out BoatOwnerDef owner, out CharacterSkinDef skin);
            BringHerOut(basil);
            MooredBoat moored = Moor(owner, OffshoreBerth);
            yield return Settle();

            AssertTheSkipperAboardIsTheirMesh(moored, owner, skin);

            List<string> problems = SpriteProblemsOf(basil).ToList();
            Assert.IsEmpty(problems,
                $"'{skin.Id}' draws as a mesh aboard, as MeshCast says, and {problems.Count} problem(s) say " +
                $"{SharedDefVillagerName} did not keep her sprite ashore with MeshCastAshore OFF:\n  " +
                string.Join("\n  ", problems));
        }

        // ------------------------------------------------------------------ the adder

        [UnityTest]
        public IEnumerator TheAdder_StandsAVillagerOnAwake_WithHerInteractableSwitchedOff()
        {
            RecordingService service = SwapInARecordingService();
            GameObject host = MakeHost("TestVillager", withBody: true, MakeNpc("npc.test_villager"), out Interactable talk);
            // She begins the day sheltered: her routine has switched her Interactable off, and Start would
            // never run. Awake runs on every component of an active GameObject, enabled or not.
            talk.enabled = false;
            host.SetActive(true);
            yield return null;

            Assert.IsTrue(host.TryGetComponent(out NpcFigureStand stand),
                "a villager (an NpcDef and an IsoCharacterSprite) whose Interactable is switched off was given no stand");
            Assert.AreEqual(1, host.GetComponents<NpcFigureStand>().Length, "the adder stood her twice");
            Assert.AreEqual(1, service.Asked.Count, "the stand did not ask Core for her figure exactly once");
            Assert.AreSame(host, service.Asked[0].Host, "the stand asked for a figure on another host");
            Assert.AreSame(stand, service.Asked[0].Stand, "the stand asked for a figure posed from another stand");
            Assert.AreSame(host.GetComponent<IsoCharacterSprite>(), stand.FigureCharacter,
                "the stand does not publish her own IsoCharacterSprite");
            Assert.AreEqual("npc.test_villager", stand.FigureKey, "the stand's key is not her NpcDef's id");
            Assert.IsNull(stand.FigureHull, "a villager on her own feet publishes a hull");
            Assert.IsFalse(talk.enabled,
                "the adder switched her Interactable on: whether she can be talked to is her routine's");
        }

        [UnityTest]
        public IEnumerator TheAdder_GivesAThingWithNoBody_Nothing()
        {
            RecordingService service = SwapInARecordingService();
            NpcDef letter = MakeNpc("npc.test_letter");
            letter.Kind = InteractKind.Read;
            // NedsLetter's shape: an NpcDef and no IsoCharacterSprite.
            GameObject host = MakeHost("TestLetter", withBody: false, letter, out _);
            host.SetActive(true);
            yield return null;

            Assert.IsFalse(host.TryGetComponent(out NpcFigureStand _), "a thing with no body was given a stand");
            Assert.IsEmpty(service.Asked, "a thing with no body asked Core for a figure");
        }

        [UnityTest]
        public IEnumerator TheAdder_LeavesAHostThatAlreadyCarriesAFigure_Alone()
        {
            RecordingService service = SwapInARecordingService();
            GameObject host = MakeHost("TestFigured", withBody: true, MakeNpc("npc.test_figured"), out _);
            host.AddComponent<HeldFigure>();
            host.SetActive(true);
            yield return null;

            Assert.IsFalse(host.TryGetComponent(out NpcFigureStand _), "a host that already carries a figure was given a stand");
            Assert.IsEmpty(service.Asked, "a host that already carries a figure asked Core for another");
            Assert.AreEqual(1, host.GetComponents<ICharacterFigure>().Length, "the host carries a second figure");
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

        /// <summary>Noon on the second day, the plates' hour. A villager still indoors is asserted as
        /// sheltered.</summary>
        private static IEnumerator AtNoon()
        {
            Assert.IsNotNull(GameServices.Clock, "harness: St Peters loaded and published no clock");
            double spd = GameServices.Config.SecondsPerDay;
            GameServices.Clock.SeekTo((1d + NoonHour / 24d) * spd);
            yield return Settle();
        }

        /// <summary>The island must be running Art's figure service and publish a config, or every claim
        /// below would be about a figure nobody could have asked for.</summary>
        private static void AssertTheCastIsInstalled()
        {
            Assert.IsNotNull(GameServices.Config, "harness: St Peters loaded and published no GameConfig");
            Assert.IsInstanceOf<CharacterFigurePresentationService>(CharacterFigurePresentation.Service,
                "harness: Art's figure service is not registered on the loaded island, so no stand could ask for a figure");
        }

        /// <summary>The switches, on a runtime copy of the loaded config. The asset is never written.</summary>
        private void UseSwitches(bool meshCast, bool ashore)
        {
            GameConfig copy = Object.Instantiate(GameServices.Config);
            copy.name = $"(test: MeshCast {meshCast}, MeshCastAshore {ashore})";
            copy.MeshCast = meshCast;
            copy.MeshCastAshore = ashore;
            _spawned.Add(copy);
            GameServices.Config = copy;
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

        private static string Who(IsoCharacterSprite villager) =>
            $"'{villager.name}' ({(villager.Visual != null ? villager.Visual.name : "no def")})";

        /// <summary>
        /// Everything that would make a villager draw other than as her mesh with both switches on, by name:
        /// out of doors, the mesh with her sprite forced off (never disabled), her overlay sorted exactly as
        /// her sprite and her figure turned as her sprite faces; sheltered, neither picture, and her figure
        /// hidden.
        /// </summary>
        private static IEnumerable<string> MeshProblemsOf(IsoCharacterSprite villager)
        {
            string who = Who(villager);
            var sprite = villager.GetComponent<SpriteRenderer>();
            if (sprite == null)
            {
                yield return $"{who}: has no SpriteRenderer";
                yield break;
            }
            CharacterSkinDef skin = villager.Visual != null ? villager.Visual.Skin : null;
            if (skin == null)
            {
                yield return $"{who}: her art def links no CharacterSkinDef, so she can only ever be a sprite";
                yield break;
            }
            if (!villager.TryGetComponent(out NpcFigureStand stand))
            {
                yield return $"{who}: was given no NpcFigureStand";
                yield break;
            }
            var presenter = villager.GetComponent<CharacterFigurePresenter>();
            if (presenter == null)
            {
                yield return $"{who}: wears '{skin.Id}' and was given no figure presenter";
                yield break;
            }
            if (!ReferenceEquals(presenter.Stand, stand))
                yield return $"{who}: her presenter is not posed from her own stand";

            IsoCharacterFigureRenderer figure = presenter.AshoreFigure;
            if (!Shows(villager))
            {
                if (presenter.WhyNot != CharacterFigurePresenter.Refusal.SpriteDisabled)
                    yield return $"{who}: sheltered, and her presenter says '{presenter.NotDrawingReason}'";
                if (sprite.forceRenderingOff)
                    yield return $"{who}: sheltered, with her sprite forced off as well";
                if (figure != null && figure.Visible)
                    yield return $"{who}: sheltered, and her figure is still visible";
                yield break;
            }

            if (presenter.WhyNot != CharacterFigurePresenter.Refusal.None)
            {
                yield return $"{who}: out of doors and not drawing as her mesh: {presenter.NotDrawingReason}";
                yield break;
            }
            if (figure == null || !figure.IsAshore || !figure.Visible)
                yield return $"{who}: drawing, and her ashore figure is " +
                             (figure == null ? "missing" : !figure.IsAshore ? "holding no facet id" : "hidden");
            if (!sprite.forceRenderingOff)
                yield return $"{who}: draws her mesh and her sprite both";
            if (figure != null && figure.AshoreOverlay != null &&
                (figure.AshoreOverlay.sortingLayerID != sprite.sortingLayerID ||
                 figure.AshoreOverlay.sortingOrder != sprite.sortingOrder))
                yield return $"{who}: her overlay sorts at layer {figure.AshoreOverlay.sortingLayerID}, order " +
                             $"{figure.AshoreOverlay.sortingOrder}; her sprite at layer {sprite.sortingLayerID}, order " +
                             $"{sprite.sortingOrder}";
            float heading = villager.HeadingDegrees;
            float yaw = skin.AzimuthCounterClockwise ? -heading : heading;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, presenter.AshoreYawDegrees)) > YawToleranceDegrees)
                yield return $"{who}: her figure is turned {presenter.AshoreYawDegrees:0.00}°, and her sprite's " +
                             $"heading {heading:0.00}° asks for {yaw:0.00}°";
        }

        /// <summary>Everything that would make a villager draw other than as today's sprite, by name.</summary>
        private static IEnumerable<string> SpriteProblemsOf(IsoCharacterSprite villager)
        {
            string who = Who(villager);

            if (villager.GetComponentsInChildren<IsoCharacterFigureRenderer>(true).Length > 0)
                yield return $"{who}: carries a figure renderer, so a figure is built or a facet id is held";
            if (villager.GetComponentsInChildren<Transform>(true).Any(t =>
                    t.name == CharacterFigurePresenter.AshoreFigureObjectName ||
                    t.name == CharacterFigurePresenter.FigureObjectName))
                yield return $"{who}: has a figure object";
            var presenter = villager.GetComponent<CharacterFigurePresenter>();
            if (presenter != null && presenter.WhyNot != CharacterFigurePresenter.Refusal.SwitchOff)
                yield return $"{who}: her presenter says '{presenter.NotDrawingReason ?? "drawing"}', not that " +
                             "a switch is off";

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

        /// <summary>Two villagers drawn under one facet id would be read as one figure by the pass.</summary>
        private static IEnumerable<string> DuplicateFigureIds(List<IsoCharacterSprite> villagers) =>
            villagers.Select(v => (v, p: v.GetComponent<CharacterFigurePresenter>()))
                     .Where(x => x.p != null && x.p.AshoreFigure != null && x.p.AshoreFigure.IsAshore)
                     .GroupBy(x => x.p.AshoreFigure.FigureId)
                     .Where(g => g.Count() > 1)
                     .Select(g => $"facet id {g.Key} is held by {string.Join(" and ", g.Select(x => Who(x.v)))}");

        /// <summary>A moored boat's skipper and a villager's stand are the only two that ask for a figure. A
        /// presenter anywhere else on the island is a figure nobody should have asked for.</summary>
        private static IEnumerable<string> PresentersNobodyAskedFor() =>
            Object.FindObjectsByType<CharacterFigurePresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(p => p.GetComponentInParent<MooredBoat>(true) == null && !p.TryGetComponent(out NpcFigureStand _))
                .Select(p => $"a figure presenter on '{PathOf(p.transform)}', which is neither aboard a moored boat " +
                             "nor a villager's");

        private static string PathOf(Transform t) =>
            t.parent == null ? t.name : $"{PathOf(t.parent)}/{t.name}";

        /// <summary>Basil, who wears Leo Arsenault's skipper's own art def, and that def's skin: one skinned
        /// def in two places, from the committed data.</summary>
        private IsoCharacterSprite TheSharedDefVillager(out BoatOwnerDef owner, out CharacterSkinDef skin)
        {
            IsoCharacterSprite basil = Villagers().SingleOrDefault(v => v.name == SharedDefVillagerName);
            Assert.IsNotNull(basil, $"harness: St Peters has no villager named '{SharedDefVillagerName}'");

            BoatOwnerDef committedOwner = Load<BoatOwnerDef>(SkipperOwnerPath);
            CharacterVisualDef committedDef = committedOwner.Skipper;
            Assert.IsNotNull(committedDef, $"harness: {SkipperOwnerPath} names no skipper");
            Assert.AreSame(committedDef, basil.Visual,
                $"harness: {SharedDefVillagerName} wears '{basil.Visual?.name}', not '{committedDef.name}' like " +
                $"'{committedOwner.Id}''s skipper, so this is no longer one def in two places");
            Assert.Greater(committedOwner.AboardCount(), 0, $"harness: '{committedOwner.Id}' has no room aboard");

            skin = committedDef.Skin;
            Assert.IsNotNull(skin, $"harness: '{committedDef.name}' links no CharacterSkinDef");
            Assert.IsTrue(skin.IsUsable(), $"harness: '{skin.Id}' is not usable, so no figure could be built from it");
            Assert.IsTrue(skin.DrawsAsMesh(CharacterSkinStateMap.Idle),
                $"harness: '{skin.Id}' does not switch on '{CharacterSkinStateMap.Idle}'");

            // A clone of the owner, so the committed asset is never written; it names the same skipper def.
            owner = Object.Instantiate(committedOwner);
            owner.name = committedOwner.name;
            _spawned.Add(owner);
            return basil;
        }

        /// <summary>Out on her own feet whatever the hour: her routine is switched off, and its OnDisable
        /// hands her renderer back visible (<c>VillagerRoutinePlayTests.DisablingHerHandsTheRendererBackVISIBLE</c>).</summary>
        private static void BringHerOut(IsoCharacterSprite villager)
        {
            var routine = villager.GetComponent<VillagerRoutine>();
            Assert.IsNotNull(routine, $"harness: '{villager.name}' has no VillagerRoutine");
            routine.enabled = false;
            Assert.IsTrue(Shows(villager), $"harness: '{villager.name}' is still hidden with her routine switched off");
        }

        /// <summary>A speed inside the band her art def draws <paramref name="gait"/> for
        /// (<see cref="IsoCharacterMath.GaitFor"/>): standing, halfway through the walking band, and half as fast
        /// again as the running threshold.</summary>
        private static float SpeedFor(CharacterVisualDef visual, CharacterGait gait)
        {
            float walk = Mathf.Max(0f, visual.WalkSpeedThreshold);
            float run = Mathf.Max(walk, visual.RunSpeedThreshold);
            Assert.Greater(walk, 0f, $"harness: '{visual.name}' walks from a standstill, so she has no idle speed");
            switch (gait)
            {
                case CharacterGait.Walk: return run > walk ? (walk + run) * WalkBandMiddle : walk;
                case CharacterGait.Run: return Mathf.Max(run, walk) * RunPastThreshold;
                default: return 0f;
            }
        }

        private static void AssertTheSkipperAboardIsTheirMesh(MooredBoat moored, BoatOwnerDef owner, CharacterSkinDef skin)
        {
            Assert.IsTrue(moored.IsPresented, $"harness: '{owner.Id}' drew no hull at all");
            IsoCharacterSprite[] aboard = moored.GetComponentsInChildren<IsoCharacterSprite>(true);
            Assert.AreEqual(1, aboard.Length, $"harness: '{owner.Id}' stands {aboard.Length} characters, not one skipper");
            IsoCharacterSprite skipper = aboard[0];
            Assert.AreSame(owner.Skipper, skipper.Visual, "harness: the skipper aboard does not wear the owner's def");
            var presenter = skipper.GetComponent<CharacterFigurePresenter>();
            Assert.IsNotNull(presenter, $"'{owner.Id}''s skipper wears '{skin.Id}' and was given no figure presenter");
            Assert.AreEqual(CharacterFigurePresenter.Refusal.None, presenter.WhyNot,
                $"the skipper aboard is not drawing as their mesh on St Peters: {presenter.NotDrawingReason}");
            Assert.IsNotNull(presenter.Figure, "the skipper aboard draws, and has no aboard figure");
            Assert.IsNull(presenter.AshoreFigure, "the skipper aboard carries an ashore figure");
            Assert.IsTrue(skipper.GetComponent<SpriteRenderer>().forceRenderingOff,
                "the skipper aboard draws the mesh and the sprite both");
        }

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

        // ------------------------------------------------------------------ the adder's harness

        private RecordingService SwapInARecordingService()
        {
            var service = new RecordingService();
            if (!_serviceSwapped) _serviceBefore = CharacterFigurePresentation.Service;
            _serviceSwapped = true;
            CharacterFigurePresentation.Service = service;
            return service;
        }

        private NpcDef MakeNpc(string id)
        {
            var npc = ScriptableObject.CreateInstance<NpcDef>();
            npc.name = id;
            npc.Id = id;
            _spawned.Add(npc);
            return npc;
        }

        /// <summary>A host built INACTIVE, so nothing wakes until the test activates it.</summary>
        private GameObject MakeHost(string name, bool withBody, NpcDef npc, out Interactable talk)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            _spawned.Add(go);
            if (withBody) go.AddComponent<IsoCharacterSprite>();   // its [RequireComponent] brings the SpriteRenderer
            talk = go.AddComponent<Interactable>();
            talk.Configure(npc);
            return go;
        }

        private sealed class RecordingService : ICharacterFigurePresentationService
        {
            public readonly List<(GameObject Host, ICharacterFigureStand Stand)> Asked = new();

            public ICharacterFigure Attach(GameObject host, ICharacterFigureStand stand)
            {
                Asked.Add((host, stand));
                return null;
            }
        }

        /// <summary>A figure somebody else already put on the host.</summary>
        private sealed class HeldFigure : MonoBehaviour, ICharacterFigure
        {
            public bool DrawsInsteadOfSprite => false;
            public void PoseFigure(ICharacterFigureStand stand, bool aboard) { }
        }
    }
}
