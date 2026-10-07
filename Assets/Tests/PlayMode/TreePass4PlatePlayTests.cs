using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE TREE RIG PASS 4, PHOTOGRAPHED AT THE PLAY CAMERA</b>: the owner judges the new trees from these
    /// plates, one BEFORE (pass 3, as main draws it) and one AFTER (pass 4, re-bound in place) of each frame,
    /// shot by the same class in two runs (handoff 2026-09-23, tree rig pass 4 intake, §4).
    ///
    /// <para>The plates, all in St Peters' woods:</para>
    /// <list type="number">
    /// <item><description>The woods at 13:00, 17:45 (golden hour) and 02:00, and on foot at 13:00.</description></item>
    /// <item><description>Snow at 0, 50 and 100 %, through <see cref="FoliageSnowBridge.OverrideCoverage"/>,
    /// never through the save.</description></item>
    /// <item><description>Calm, a breeze (6 m/s) and a gale (18 m/s), each wind from the west and from the
    /// north, through the environment the wind bridge reads; each with a 16-frame burst, since the wind's
    /// look is motion.</description></item>
    /// <item><description>One close shot per species: the nine the woods plant, each at a tree the scene
    /// census scored least hidden, and the tamarack, which no scene plants, staged through the catalog's own
    /// <c>Configure</c> on the black spruce's root with the spruce hidden.</description></item>
    /// <item><description>The numbers: texture memory the woods bind, draw calls and batches with the trees
    /// on and off, and frame time with the trees on and off.</description></item>
    /// </list>
    ///
    /// <para><b>THE FRAME.</b> The persistent core's camera and its own <see cref="CameraFollow"/>, asked for a
    /// world height that the zoom ladder hits exactly at the 1080 px design screen: zoom 2 frames
    /// 1080 / (2 x 32) = 16.875 m, which holds the tallest pass-4 tree (the white pine, 13.4 m above its root),
    /// and the on-foot plate asks <see cref="CameraFraming.OnFoot"/>, 8.4375 m. Every centre lies on the half
    /// metre, on every grid the camera snaps to, and was scored with no Unity from the committed scene: the
    /// woods frame holds 11 whole trees of all nine species.</para>
    ///
    /// <para><b>THE DAY AND THE WEATHER.</b> High Summer, day 1, so the calendar's snow is 0 and the woods'
    /// summer sheets are the season the game draws; the sun has no season. Noon is the cliff plates' 13:00,
    /// but their 19:30 golden hour is past this profile's dusk key (t 0.80, 19:12): the first run measured
    /// that frame at a mean of (14, 14, 1) of 255, a black picture, so the golden hour here is the profile's
    /// own sunset colour key (t 0.74, 17:45). The weather is held clear with a calm sea on every plate
    /// (visibility and sea state feed the grade and the rain), and only the wind moves, so a plate differs
    /// from its neighbour by its one subject.</para>
    ///
    /// <para><b>THE SAVE.</b> Every worktree shares the owner's <c>savegame.json</c>, and the
    /// <see cref="SaveService"/> writes it on quit. Before anything loads, every live SaveService is pointed at
    /// a sandbox file under this plate folder, and every load re-checks it, so nothing here can write the
    /// owner's save.</para>
    ///
    /// <para>⚠ No GPU on CI: every case skips there before it loads anything. Plates land in
    /// <c>Application.temporaryCachePath/tree-pass-4/</c>, which every worktree shares, so a run reads its
    /// own plates by a floor file, never by name.</para>
    /// </summary>
    public class TreePass4PlatePlayTests
    {
        const string PlateDir = "tree-pass-4";
        const string CleanupSceneName = "TreePass4PlateCleanup";
        const string StPeters = "StPeters";

        const int PlateHeightPx = CameraFollow.DesignScreenHeightPx;   // 1080: the zoom depends on it
        const int PlateWidthPx = 1920;
        const int ArtPixelsPerMetre = 32;
        const int WoodsZoom = 2;                                        // 16.875 m tall

        const float LeadEaseOutSeconds = 6f;
        const float OnTheCentreMetres = 0.001f;

        const float NoonHours = 13f;         // solar noon (the cliff plates' hours: the sun has no season)
        const float GoldenHours = 17.75f;    // the day-night profile's orange sunset key, t 0.74
        const float NightHours = 26f;        // 02:00 the next night

        const float BreezeMs = 6f;           // Beaufort 4, a moderate breeze
        const float GaleMs = 18f;            // Beaufort 8, a gale
        const int BurstFrames = 16;
        const float BurstSpacingSeconds = 0.125f;   // 16 frames over 2 s, the shader's wind loop

        // Scored with no Unity against the committed StPeters.unity (259 trees): 11 whole trees, 9 species.
        static readonly Vector2 Woods = new Vector2(125f, -33f);

        /// <summary>One close shot per species: the root of the tree the census scored least hidden (fewest
        /// trees in front of it), and the frame centre 6 m above it on the half metre.</summary>
        static readonly (string species, Vector2 root, Vector2 centre)[] CloseShots =
        {
            ("RedSpruce", new Vector2(20.13f, -25.25f), new Vector2(20f, -19f)),
            ("BlackSpruce", new Vector2(58.49f, -5.31f), new Vector2(58.5f, 0.5f)),
            ("BalsamFir", new Vector2(118.07f, -45.2f), new Vector2(118f, -39f)),
            ("WhitePine", new Vector2(27.85f, -20.4f), new Vector2(28f, -14.5f)),
            ("WhiteCedar", new Vector2(66.85f, -43.3f), new Vector2(67f, -37.5f)),
            ("WhiteBirch", new Vector2(102.35f, 2.92f), new Vector2(102.5f, 9f)),
            ("RedMaple", new Vector2(48.65f, -5.04f), new Vector2(48.5f, 1f)),
            ("RedOak", new Vector2(66.43f, -8.2f), new Vector2(66.5f, -2f)),
            ("TremblingAspen", new Vector2(88.64f, -40.2f), new Vector2(88.5f, -34f)),
        };
        const string StagedSpecies = "Tamarack";
        const string StagedOn = "BlackSpruce";
        const float RootMatchMetres = 0.05f;

        const string SandboxSaveName = "savegame-sandbox.json";

        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        readonly List<Object> _spawned = new List<Object>();
        readonly List<Renderer> _hidden = new List<Renderer>();
        bool _loadedAny;
        bool _introOnEntry;
        string _healed = "none";
        string _saveGuard = "(not sandboxed)";
        bool _asyncShadersOnEntry = true;

        Camera _cam;
        CameraFollow _follow;
        bool _followCaptured;
        Transform _followTargetOnEntry;
        float _followSmoothOnEntry;
        float _askedHeight;
        string _eased = "(not framed)";
        RenderTexture _rt;
        int _w, _h;

        IEnvironmentService _envOnEntry;
        HeldWeather _weather;
        string _pinned = "(not pinned)";

        readonly List<SpriteRenderer> _trees = new List<SpriteRenderer>();
        string _arm = "(unknown)";
        string _armWhy = "";

        // =============================================================================================
        //  Set-up and teardown: leave nothing loaded, and never the owner's save
        // =============================================================================================

        [UnitySetUp]
        public IEnumerator SetUpRegion()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);
            SandboxTheSave("set-up");
#if UNITY_EDITOR
            // A plate must never shoot a placeholder: compile each shader in place at its first render.
            _asyncShadersOnEntry = UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;   // ⚠ a STATIC: left at 0 it stops every test that follows
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = _asyncShadersOnEntry;
#endif
            FoliageSnowBridge.ClearOverride();
            if (_weather != null && ReferenceEquals(GameServices.Environment, _weather))
                GameServices.Environment = _envOnEntry;

            foreach (Renderer r in _hidden) if (r != null) r.enabled = true;
            _hidden.Clear();
            if (_follow != null && _followCaptured)
            {
                _follow.Target = _followTargetOnEntry;
                _follow.Smooth = _followSmoothOnEntry;
            }
            if (_cam != null) _cam.targetTexture = null;
            ReleaseTarget();
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();

            _follow = null;
            _cam = null;
            _followCaptured = false;
            _followTargetOnEntry = null;
            _weather = null;
            _envOnEntry = null;
            _trees.Clear();
            _arm = "(unknown)";
            _armWhy = "";
            _pinned = "(not pinned)";
            _eased = "(not framed)";
            _healed = "none";

            if (!_loadedAny) yield break;

            if (GameServices.Clock != null) GameServices.Clock.TimeScale = 1f;
            GameServices.OpeningCinematicRunning = _introOnEntry;
            GameServices.PendingArrivalKey = null;

            Scene clean = SceneManager.GetSceneByName(CleanupSceneName);
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene(CleanupSceneName);
            if (clean.IsValid()) SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == StPeters)
                    yield return SceneManager.UnloadSceneAsync(s);
            }

            // ⚠ #764: a Single load promotes the persistent core's roots into DontDestroyOnLoad; destroy
            // exactly the roots this case added, by identity, then clear the service slots. The SaveService
            // was resident before the case, so it stays, and stays sandboxed: its quit-time write lands in
            // the sandbox.
            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go)) Object.DestroyImmediate(go);
            GameServices.Reset();
            SandboxTheSave("teardown");
            _loadedAny = false;
            yield return null;
        }

        // =============================================================================================
        //  The plates
        // =============================================================================================

        /// <summary><b>Plate 1.</b> The woods at noon, in the golden hour and at night, and on foot at noon;
        /// and the numbers, at noon.</summary>
        [UnityTest]
        public IEnumerator StPeters_TheWoods_AtNoonGoldenHourAndNight()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            yield return PinTheHour(NoonHours);

            yield return FrameOn(Woods, onFoot: true);
            ShootTheWoods("woods-onfoot-1300", "St Peters' woods on foot at 13:00 (the walker's view)", Woods);

            yield return FrameOn(Woods, onFoot: false);
            ShootTheWoods("woods-1300", "St Peters' woods at 13:00", Woods);
            yield return TakeTheNumbers();

            // Same camera, other hours: the frame was asserted on the woods at noon, and a dark hour's
            // share only says how dark it is, so it is recorded there and not asserted.
            yield return PinTheHour(GoldenHours);
            ShootTheWoods("woods-1745", "St Peters' woods at 17:45, the golden hour", Woods, framedAtNoon: true);
            yield return PinTheHour(NightHours);
            ShootTheWoods("woods-0200", "St Peters' woods at 02:00", Woods, framedAtNoon: true);
        }

        /// <summary><b>Plate 2.</b> The woods at noon under 0, 50 and 100 % snow, through the bridge's own
        /// override. Pass 3 has no snow, so its three plates are the same trees.</summary>
        [UnityTest]
        public IEnumerator StPeters_TheWoods_UnderSnow_At0_50And100Percent()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            yield return PinTheHour(NoonHours);
            yield return FrameOn(Woods, onFoot: false);
            foreach (float cover in new[] { 0f, 0.5f, 1f })
            {
                FoliageSnowBridge.OverrideCoverage(cover);
                for (int i = 0; i < 2; i++) yield return null;
                Assert.AreEqual(cover, FoliageSnowBridge.Published, 1e-6f,
                    $"the snow global reads {FoliageSnowBridge.Published}, not the {cover} the plate asked for.");
                int pct = Mathf.RoundToInt(cover * 100f);
                ShootTheWoods($"woods-snow-{pct:000}", $"St Peters' woods at 13:00 under {pct} % snow", Woods);
            }
        }

        /// <summary><b>Plate 3.</b> The woods at noon in a calm, a breeze and a gale, each wind from the west and
        /// from the north: a still, and a burst of frames that shows the motion.</summary>
        [UnityTest]
        public IEnumerator StPeters_TheWoods_InCalmBreezeAndGale_FromTwoDirections()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            yield return PinTheHour(NoonHours);
            yield return FrameOn(Woods, onFoot: false);

            var winds = new (string key, string words, Vector2 ms)[]
            {
                ("calm", "a calm", Vector2.zero),
                ("breeze-from-west", $"a {BreezeMs:0} m/s breeze from the west", new Vector2(BreezeMs, 0f)),
                ("breeze-from-north", $"a {BreezeMs:0} m/s breeze from the north", new Vector2(0f, -BreezeMs)),
                ("gale-from-west", $"an {GaleMs:0} m/s gale from the west", new Vector2(GaleMs, 0f)),
                ("gale-from-north", $"an {GaleMs:0} m/s gale from the north", new Vector2(0f, -GaleMs)),
            };
            foreach (var wind in winds)
            {
                yield return HoldTheWind(wind.ms);
                ShootTheWoods($"wind-{wind.key}", $"St Peters' woods at 13:00 in {wind.words}", Woods);
                yield return ShootABurst($"wind-{wind.key}", wind.words);
            }
        }

        /// <summary><b>Plate 4.</b> One close shot per species at noon, calm and bare: the nine the woods plant,
        /// and the tamarack staged on the black spruce's root.</summary>
        [UnityTest]
        public IEnumerator StPeters_EverySpecies_CloseUp()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            yield return PinTheHour(NoonHours);

            foreach (var shot in CloseShots)
            {
                SpriteRenderer subject = TreeAt(shot.root, shot.species);
                yield return FrameOn(shot.centre, onFoot: false);
                ShootTheSubject($"species-{shot.species}", $"{shot.species} at ({shot.root.x:0.00}, {shot.root.y:0.00}), 13:00",
                                shot.centre, subject, null);
            }

            var host = CloseShots.First(s => s.species == StagedOn);
            SpriteRenderer spruce = TreeAt(host.root, StagedOn);
            spruce.enabled = false;
            _hidden.Add(spruce);
            SpriteRenderer staged = StageThroughTheCatalog(StagedSpecies, host.root, out string how);
            yield return FrameOn(host.centre, onFoot: false);
            ShootTheSubject($"species-{StagedSpecies}", $"{StagedSpecies}, staged on {StagedOn}'s root " +
                            $"({host.root.x:0.00}, {host.root.y:0.00}) with the spruce hidden, 13:00",
                            host.centre, staged, how);
        }

        // =============================================================================================
        //  The save: sandboxed before anything loads
        // =============================================================================================

        /// <summary>Point every live <see cref="SaveService"/> at a sandbox file, and fail loudly when one cannot
        /// be, so no case runs with the owner's save writable.</summary>
        void SandboxTheSave(string when)
        {
            FieldInfo pathField = typeof(SaveService).GetField("_path", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(pathField, "SaveService has no private _path field, so its writes cannot be sandboxed; " +
                                        "the plates will not run against the owner's save.");
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            string sandbox = Path.GetFullPath(Path.Combine(dir, SandboxSaveName));
            string owners = Path.GetFullPath(SaveStore.DefaultPath);
            Assert.AreNotEqual(owners, sandbox, "the sandbox is the owner's save path.");

            int live = 0, moved = 0;
            foreach (SaveService s in Resources.FindObjectsOfTypeAll<SaveService>())
            {
                if (s == null || !s.gameObject.scene.IsValid()) continue;
                live++;
                string was = pathField.GetValue(s) as string;
                if (!string.Equals(was, sandbox, StringComparison.OrdinalIgnoreCase))
                {
                    pathField.SetValue(s, sandbox);
                    moved++;
                }
                Assert.AreEqual(sandbox, pathField.GetValue(s) as string, "a SaveService still points at another file.");
            }
            _saveGuard = $"{live} SaveService(s) write to {sandbox} ({moved} re-pointed at {when}); the owner's is {owners}";
            Debug.Log($"[{PlateDir}] save: {_saveGuard}");
        }

        // =============================================================================================
        //  The region, the hour, the weather and the frame
        // =============================================================================================

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: no graphics device (Null Device), so nothing rendered " +
                              "and nothing was proved. Expected on CI; a plate of the trees needs a GPU.");
        }

        IEnumerator LoadStPeters()
        {
            if (!_loadedAny) _introOnEntry = GameServices.OpeningCinematicRunning;

            GameConfig configBefore = GameServices.Config;
            IGameClock clockBefore = GameServices.Clock;
            IEnvironmentService envBefore = GameServices.Environment;
            IWallet walletBefore = GameServices.Wallet;

            LogAssert.ignoreFailingMessages = true;   // the region logs decor complaints of its own
            _loadedAny = true;
            yield return SceneManager.LoadSceneAsync(StPeters, LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;
            SandboxTheSave("load");

            var healed = new List<string>();
            if (GameServices.Config == null && configBefore != null) { GameServices.Config = configBefore; healed.Add("Config"); }
            if (GameServices.Clock == null && clockBefore != null) { GameServices.Clock = clockBefore; healed.Add("Clock"); }
            if (GameServices.Environment == null && envBefore != null) { GameServices.Environment = envBefore; healed.Add("Environment"); }
            if (GameServices.Wallet == null && walletBefore != null) { GameServices.Wallet = walletBefore; healed.Add("Wallet"); }
            if (healed.Count > 0) _healed = $"re-published {string.Join(", ", healed)} after the region's dev core took them down";

            int frames = 0;
            while (frames < 240 && FindPersistentFollow() == null)
            {
                yield return null;
                frames++;
            }
            Assert.IsNotNull(FindPersistentFollow(), "after 240 frames there is no CameraFollow on the persistent core's camera.");

            Assert.IsNotNull(GameServices.Environment, "the region registered no environment, so the weather cannot be held.");
            _envOnEntry = GameServices.Environment;
            _weather = new HeldWeather(_envOnEntry);
            GameServices.Environment = _weather;
            FoliageSnowBridge.OverrideCoverage(0f);

            FindTheTrees();
            Debug.Log($"[{PlateDir}] St Peters loaded: region '{GameServices.CurrentRegionId}', camera up after {frames} " +
                      $"frames; {_trees.Count} trees, arm {_arm} ({_armWhy}).");
        }

        /// <summary>Every tree in the region, and which pass it draws: pass 4 publishes <c>_TreeMaps</c> 1 on
        /// every tree it re-bound, pass 3 on none, and a mix means the re-bind missed trees.</summary>
        void FindTheTrees()
        {
            _trees.Clear();
            foreach (TreeTrunkAnchor a in Object.FindObjectsByType<TreeTrunkAnchor>())
            {
                if (a == null || a.gameObject.scene.name != StPeters) continue;
                var r = a.GetComponent<SpriteRenderer>();
                if (r != null) _trees.Add(r);
            }
            Assert.Greater(_trees.Count, 0, "St Peters has no trees.");
            int withMaps = _trees.Count(r => TreeTrunkAnchor.TreeMapsOn(r, 0f) > 0.5f);
            _armWhy = $"{withMaps} of {_trees.Count} trees publish _TreeMaps 1";
            Assert.That(withMaps == 0 || withMaps == _trees.Count,
                $"{_armWhy}: some trees draw pass 4 and some pass 3, so the re-bind missed trees.");
            _arm = withMaps == 0 ? "before" : "after";
        }

        SpriteRenderer TreeAt(Vector2 root, string species)
        {
            SpriteRenderer best = null;
            float bestD = float.MaxValue;
            foreach (SpriteRenderer r in _trees)
            {
                float d = Vector2.Distance(r.transform.position, root);
                if (d < bestD) { bestD = d; best = r; }
            }
            Assert.IsNotNull(best, $"no tree near ({root.x}, {root.y}).");
            Assert.LessOrEqual(bestD, RootMatchMetres,
                $"the nearest tree to the {species} root ({root.x}, {root.y}) is {bestD:0.###} m away, so the woods moved.");
            Assert.IsNotNull(best.sprite, $"the {species} at ({root.x}, {root.y}) draws no sprite.");
            Assert.That(best.sprite.name, Does.StartWith(species + "_"),
                $"the tree at ({root.x}, {root.y}) draws '{best.sprite.name}', not a {species}.");
            return best;
        }

        /// <summary>
        /// Plant one tree the way the planters do, through <c>AcadianTreeCatalog.Configure</c>, reached by
        /// reflection because the catalog lives in the Art editor assembly this test assembly does not
        /// reference. The same sprite, material, anchor, light sheets and wind maps as a planted tree.
        /// </summary>
        SpriteRenderer StageThroughTheCatalog(string species, Vector2 root, out string how)
        {
            Type catalog = Type.GetType("HiddenHarbours.Art.Editor.AcadianTreeCatalog, HiddenHarbours.Art.Editor");
            Assert.IsNotNull(catalog, "the Art editor assembly has no AcadianTreeCatalog, so a tree cannot be staged.");
            const BindingFlags S = BindingFlags.Public | BindingFlags.Static;
            MethodInfo scan = catalog.GetMethod("Scan", S, null, Type.EmptyTypes, null);
            MethodInfo loadMaterial = catalog.GetMethod("LoadMaterial", S, null, Type.EmptyTypes, null);
            MethodInfo loadVariant = catalog.GetMethods(S).FirstOrDefault(m => m.Name == "LoadVariant" && m.GetParameters().Length == 2);
            MethodInfo configure = catalog.GetMethods(S).FirstOrDefault(m => m.Name == "Configure" && m.GetParameters().Length == 5);
            Assert.IsNotNull(scan, "no AcadianTreeCatalog.Scan()");
            Assert.IsNotNull(loadMaterial, "no AcadianTreeCatalog.LoadMaterial()");
            Assert.IsNotNull(loadVariant, "no AcadianTreeCatalog.LoadVariant(placement, variant)");
            Assert.IsNotNull(configure, "no AcadianTreeCatalog.Configure(go, placement, sprite, material, sortingOrder)");

            object placement = null;
            foreach (object p in (IEnumerable)scan.Invoke(null, null))
            {
                object entry = p.GetType().GetField("Entry")?.GetValue(p);
                string sp = entry?.GetType().GetField("species")?.GetValue(entry) as string;
                string season = p.GetType().GetField("Season")?.GetValue(p) as string;
                if (sp == species && season == "summer") { placement = p; break; }
            }
            Assert.IsNotNull(placement, $"the kit's contract has no summer {species}, so it cannot be staged.");

            var sprite = (Sprite)loadVariant.Invoke(null, new[] { placement, (object)0 });
            var material = (Material)loadMaterial.Invoke(null, null);
            Assert.IsNotNull(sprite, $"the {species} has no variant-0 sprite.");
            Assert.IsNotNull(material, "the tree material did not load.");

            var go = new GameObject($"{species}_0 (staged for the plate)");
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetSceneByName(StPeters));
            _spawned.Add(go);
            go.transform.position = new Vector3(root.x, root.y, 0f);
            object sortingOrder = configure.GetParameters()[4].DefaultValue;
            var r = (SpriteRenderer)configure.Invoke(null, new[] { go, placement, sprite, material, sortingOrder });
            Assert.IsNotNull(r, "Configure returned no renderer.");
            how = $"staged through AcadianTreeCatalog.Configure (the planters' path): sprite '{sprite.name}', " +
                  $"material '{material.name}', sortingOrder {sortingOrder}, _TreeMaps {TreeTrunkAnchor.TreeMapsOn(r, 0f)}";
            return r;
        }

        /// <summary>Stop the clock at <paramref name="hours"/> after High Summer day 1's midnight, then let the
        /// light catch up (the day/night controller ticks on scaled time and reads the clock's hour).</summary>
        IEnumerator PinTheHour(float hours)
        {
            IGameClock clock = GameServices.Clock;
            GameConfig config = GameServices.Config;
            Assert.IsNotNull(clock, "the region registered no clock, so the hour cannot be pinned.");
            Assert.IsNotNull(config, "the region registered no GameConfig, so a day has no length.");

            double spd = config.SecondsPerDay;
            double t = (config.DaysPerSeason * (int)Season.HighSummer + hours / 24.0) * spd;
            Time.timeScale = 1f;
            clock.SeekTo(t);
            clock.TimeScale = 0f;
            Assert.LessOrEqual(Math.Abs(clock.TotalSeconds - t), 1.0, $"the clock did not land on {t:0.0} s.");
            Assert.AreEqual(Season.HighSummer, clock.Season, "the pinned day is not in High Summer.");

            Color tint = Shader.GetGlobalColor("_DayNightTint");
            float waited = 0f;
            int still = 0, frames = 0;
            while (frames < 900)
            {
                yield return null;
                frames++;
                waited += Time.deltaTime;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f ? still + 1 : 0;
                tint = now;
                if (waited >= 1f && still >= 4) break;
            }
            Assert.Less(frames, 900, $"the day/night tint never settled at {hours} h.");
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;

            float hour = clock.HourOfDay;
            int hh = Mathf.FloorToInt(hour), mm = Mathf.FloorToInt((hour - hh) * 60f);
            Vector4 sun = Shader.GetGlobalVector("_SunDir");
            _pinned = $"{clock.Season} day {clock.DayOfSeason} {hh:00}:{mm:00} ({hour:0.000} h, TotalSeconds " +
                      $"{clock.TotalSeconds:0.0}); _DayNightTint ({tint.r:0.000}, {tint.g:0.000}, {tint.b:0.000}), " +
                      $"_SunDir ({sun.x:0.000}, {sun.y:0.000}, {sun.z:0.000}), _SunElevation " +
                      $"{Shader.GetGlobalFloat("_SunElevation"):0.000}; settled after {frames} frames / {waited:0.00} s";
            Debug.Log($"[{PlateDir}] pinned: {_pinned}");
        }

        /// <summary>Hold the wind the bridge reads, then wait for <c>_WindWorld</c> to carry it (the bridge
        /// publishes on a throttled tick of scaled time).</summary>
        IEnumerator HoldTheWind(Vector2 windMs)
        {
            _weather.Wind = windMs;
            Time.timeScale = 1f;
            float start = Time.realtimeSinceStartup;
            bool there = false;
            while (Time.realtimeSinceStartup - start < 3f)
            {
                yield return null;
                Vector4 w = Shader.GetGlobalVector("_WindWorld");
                var now = new Vector2(w.x, w.y);
                there = windMs == Vector2.zero
                    ? now.sqrMagnitude < 1e-10f
                    : now.sqrMagnitude > 1e-10f && Vector2.Dot(now.normalized, windMs.normalized) > 0.9999f;
                if (there && Time.realtimeSinceStartup - start > 0.5f) break;
            }
            Assert.IsTrue(there, $"_WindWorld never carried the held wind ({windMs.x}, {windMs.y}) m/s.");
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
        }

        /// <summary>
        /// Hand the play camera's own follow a still anchor on the frame centre with the 1080 px target
        /// attached, framed at the woods' zoom (or on foot), let the world run until the camera sits on the
        /// centre, then stop the world (TerrainPxFlipPlatePlayTests.FrameOn, whose notes hold here).
        /// </summary>
        IEnumerator FrameOn(Vector2 at, bool onFoot)
        {
            _follow = FindPersistentFollow();
            Assert.IsNotNull(_follow, "no CameraFollow on the persistent core's camera.");
            Assert.IsTrue(_follow.isActiveAndEnabled, "the play camera's CameraFollow is disabled.");
            _cam = _follow.GetComponent<Camera>();
            Assert.IsNotNull(_cam, "the play camera's CameraFollow has no Camera.");
            if (!_followCaptured)
            {
                _followTargetOnEntry = _follow.Target;
                _followSmoothOnEntry = _follow.Smooth;
                _followCaptured = true;
            }

            Time.timeScale = 0f;
            var anchor = new GameObject("TreePass4PlateAnchor");
            _spawned.Add(anchor);
            anchor.transform.position = new Vector3(at.x, at.y, 0f);
            _follow.Target = anchor.transform;
            _follow.Smooth = 1000f;
            _cam.transform.position = new Vector3(at.x, at.y, _cam.transform.position.z);
            _askedHeight = onFoot
                ? _follow.WorldHeightFor(CameraFraming.OnFoot)
                : PlateHeightPx / (float)(WoodsZoom * ArtPixelsPerMetre);
            _follow.SetFraming(_askedHeight, 0f);

            if (_cam.targetTexture == _rt) _cam.targetTexture = null;
            ReleaseTarget();
            _w = PlateWidthPx;
            _h = PlateHeightPx;
            _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
            _rt.Create();
            _cam.targetTexture = _rt;
            for (int i = 0; i < 2; i++) yield return null;

            Time.timeScale = 1f;
            for (int i = 0; i < 8; i++) yield return null;
            yield return SettleCamera(_cam);
            yield return EaseOntoTheCentre(at);
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;

            Vector3 p = _cam.transform.position;
            float worldHeight = _cam.orthographicSize * 2f;
            Assert.LessOrEqual(Vector2.Distance(new Vector2(p.x, p.y), at), OnTheCentreMetres,
                $"the camera sits at ({p.x:0.####}, {p.y:0.####}), not on the frame centre ({at.x:0.###}, {at.y:0.###}), " +
                $"after {_eased}: something else drives it, so the two arms would not line up.");
            float want = onFoot ? 8.4375f : _askedHeight;
            Assert.AreEqual(want, worldHeight, 0.01f, $"the camera frames {worldHeight:0.###} m tall, not {want:0.####} m.");
        }

        IEnumerator EaseOntoTheCentre(Vector2 at)
        {
            Vector2 stillAt = _cam.transform.position;
            float start = Time.time;
            int frames = 0;
            while (Vector2.Distance(_cam.transform.position, at) > OnTheCentreMetres && Time.time - start < LeadEaseOutSeconds)
            {
                yield return null;
                frames++;
            }
            _eased = $"still at ({stillAt.x:0.####}, {stillAt.y:0.####}), then {frames} frames / {Time.time - start:0.00} s " +
                     "at full time for the look-ahead to ease out";
        }

        static IEnumerator SettleCamera(Camera cam)
        {
            Vector3 lastPos = cam.transform.position;
            float lastSize = cam.orthographicSize;
            int still = 0, frames = 0;
            while (still < 5 && frames < 400)
            {
                yield return null;
                frames++;
                bool moved = (cam.transform.position - lastPos).sqrMagnitude > 1e-8f ||
                             Mathf.Abs(cam.orthographicSize - lastSize) > 1e-5f;
                still = moved ? 0 : still + 1;
                lastPos = cam.transform.position;
                lastSize = cam.orthographicSize;
            }
            Assert.Less(frames, 400, "the camera never came to rest.");
        }

        static CameraFollow FindPersistentFollow()
        {
            foreach (CameraFollow f in Object.FindObjectsByType<CameraFollow>())
                if (f != null && MoodGradeDirector.IsPersistentCamera(f.GetComponent<Camera>()))
                    return f;
            return null;
        }

        void ReleaseTarget()
        {
            if (_rt == null) return;
            _rt.Release();
            Object.DestroyImmediate(_rt);
            _rt = null;
        }

        // =============================================================================================
        //  Shooting
        // =============================================================================================

        /// <summary>A woods plate, and its control with every tree switched off: the trees must be the
        /// subject, most of the frame. <paramref name="framedAtNoon"/> is a later hour from the camera that
        /// noon's plate asserted; its share is recorded, not asserted, because the dark hours hold the trees
        /// a least step or two above the ground (the first run: 2.7 % of 19:30's pixels moved by more than 2).</summary>
        void ShootTheWoods(string plate, string subject, Vector2 at, bool framedAtNoon = false)
        {
            byte[] picture = Capture();
            var off = new List<SpriteRenderer>();
            foreach (SpriteRenderer r in _trees) if (r != null && r.enabled) { r.enabled = false; off.Add(r); }
            byte[] control;
            try { control = Capture(); }
            finally { foreach (SpriteRenderer r in off) r.enabled = true; }
            double share = ChangedShare(picture, control);

            SavePlate($"{plate}-{_arm}.png", picture, _w, _h);
            SavePlate($"{plate}-{_arm}-control-no-trees.png", control, _w, _h);
            SaveText($"{plate}-{_arm}.txt", Caption(plate, subject, at, Fnv1a(picture), Fnv1a(control), share,
                                                   $"trees in frame: {TreesInFrame()} of {_trees.Count}\n" +
                                                   (framedAtNoon ? "framing: asserted at noon from this camera; this share is recorded only\n" : "")));
            if (framedAtNoon) return;
            Assert.Greater(share, 0.2, $"{plate}: switching the trees off changed only {share:P1} of the frame, " +
                                       "so the woods are not the subject.");
        }

        /// <summary>A close shot, and its control with the subject tree alone switched off.</summary>
        void ShootTheSubject(string plate, string subject, Vector2 at, SpriteRenderer tree, string how)
        {
            byte[] picture = Capture();
            tree.enabled = false;
            byte[] control;
            try { control = Capture(); }
            finally { tree.enabled = true; }
            double share = ChangedShare(picture, control);

            var sb = new StringBuilder();
            sb.AppendLine($"subject: '{tree.name}' sprite '{tree.sprite.name}' ({tree.sprite.rect.width:0}x{tree.sprite.rect.height:0} px, " +
                          $"pivot {tree.sprite.pivot.x:0},{tree.sprite.pivot.y:0}) on sheet '{tree.sprite.texture.name}' " +
                          $"{tree.sprite.texture.width}x{tree.sprite.texture.height}; bounds {tree.bounds.size.x:0.00} x {tree.bounds.size.y:0.00} m; " +
                          $"_TrunkAnchor {TreeTrunkAnchor.AnchorOn(tree):0.0000}; _TreeMaps {TreeTrunkAnchor.TreeMapsOn(tree, 0f):0}");
            if (how != null) sb.AppendLine(how);
            SavePlate($"{plate}-{_arm}.png", picture, _w, _h);
            SavePlate($"{plate}-{_arm}-control-no-subject.png", control, _w, _h);
            SaveText($"{plate}-{_arm}.txt", Caption(plate, subject, at, Fnv1a(picture), Fnv1a(control), share, sb.ToString()));
            // The floor is "in the picture", not "fills it": pass 3's black spruce is 73 px wide and moved
            // 0.52 % of the frame in the first run; a tree off the frame moves none.
            Assert.Greater(share, 0.002, $"{plate}: switching the subject off changed only {share:P2} of the frame, " +
                                         "so the tree is not in the picture.");
        }

        /// <summary>
        /// Run the world and shoot <see cref="BurstFrames"/> frames about <see cref="BurstSpacingSeconds"/> apart
        /// in shader time, each halved to one pixel per art pixel (zoom 2 draws every art pixel as 2 x 2), with
        /// the shader time of each frame written beside them, so the motion can be played back at its pace.
        /// </summary>
        IEnumerator ShootABurst(string plate, string words)
        {
            Time.timeScale = 1f;
            var times = new StringBuilder();
            times.AppendLine($"{plate}-{_arm}: {words}; {BurstFrames} frames at 1 px per art pixel ({_w / 2}x{_h / 2}); " +
                             "t = Time.timeSinceLevelLoad at the render (the shader's _Time.y)");
            float next = Time.timeSinceLevelLoad;
            for (int f = 0; f < BurstFrames; f++)
            {
                while (Time.timeSinceLevelLoad < next) yield return null;
                float t = Time.timeSinceLevelLoad;
                byte[] full = Capture();
                SavePlate($"{plate}-{_arm}-burst-{f:00}.png", HalfOf(full), _w / 2, _h / 2);
                times.AppendLine($"{f:00} t={t.ToString("0.0000", CultureInfo.InvariantCulture)}");
                next = t + BurstSpacingSeconds;
                yield return null;
            }
            SaveText($"{plate}-{_arm}-burst.txt", times.ToString());
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
        }

        int TreesInFrame()
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_cam);
            return _trees.Count(r => r != null && r.enabled && GeometryUtility.TestPlanesAABB(planes, r.bounds));
        }

        byte[] Capture()
        {
            _cam.Render();
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_w, _h, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prevActive;

            Color[] px = tex.GetPixels();
            var outBytes = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++)
            {
                Color c = new Color(Mathf.Clamp01(px[i].r), Mathf.Clamp01(px[i].g), Mathf.Clamp01(px[i].b), 1f).gamma;
                outBytes[i * 4] = (byte)Mathf.RoundToInt(c.r * 255f);
                outBytes[i * 4 + 1] = (byte)Mathf.RoundToInt(c.g * 255f);
                outBytes[i * 4 + 2] = (byte)Mathf.RoundToInt(c.b * 255f);
                outBytes[i * 4 + 3] = 255;
            }
            Object.DestroyImmediate(tex);
            return outBytes;
        }

        byte[] HalfOf(byte[] full)
        {
            int w = _w / 2, h = _h / 2;
            var half = new byte[w * h * 4];
            for (int row = 0; row < h; row++)
                for (int col = 0; col < w; col++)
                    Buffer.BlockCopy(full, ((row * 2) * _w + col * 2) * 4, half, (row * w + col) * 4, 4);
            return half;
        }

        static ulong Fnv1a(byte[] data)
        {
            ulong h = 14695981039346656037UL;
            foreach (byte b in data)
            {
                h ^= b;
                h *= 1099511628211UL;
            }
            return h;
        }

        double ChangedShare(byte[] a, byte[] b)
        {
            long changed = 0;
            for (int i = 0; i < a.Length; i += 4)
                if (Mathf.Abs(a[i] - b[i]) > 2 || Mathf.Abs(a[i + 1] - b[i + 1]) > 2 || Mathf.Abs(a[i + 2] - b[i + 2]) > 2)
                    changed++;
            return (double)changed / (a.Length / 4);
        }

        // =============================================================================================
        //  The numbers: texture memory, draw calls, frame time
        // =============================================================================================

        IEnumerator TakeTheNumbers()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"numbers-{_arm}: St Peters' woods at 13:00, zoom {WoodsZoom}, calm, bare; {_trees.Count} trees in the " +
                          $"region, {TreesInFrame()} in frame; run {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z, Unity " +
                          $"{Application.unityVersion}, {SystemInfo.graphicsDeviceType} on {SystemInfo.graphicsDeviceName}, " +
                          $"batch mode {Application.isBatchMode}");

            // Texture memory: every texture the region's trees bind, once.
            var kinds = new Dictionary<Texture, string>();
            foreach (SpriteRenderer r in _trees)
            {
                if (r == null) continue;
                void Add(Texture t, string kind) { if (t != null && !kinds.ContainsKey(t)) kinds[t] = kind; }
                if (r.sprite != null) Add(r.sprite.texture, "albedo");
                Add(TreeTrunkAnchor.MaskOn(r), "mask");
                Add(TreeTrunkAnchor.NormalOn(r), "normal");
                Add(TreeTrunkAnchor.WindTexOn(r), "wind");
                Add(TreeTrunkAnchor.PhaseTexOn(r), "phase");
                Add(TreeTrunkAnchor.SnowTexOn(r), "snow");
                Add(TreeTrunkAnchor.PaletteOn(r), "palette");
            }
            long runtimeAll = 0, gpuAll = 0;
            foreach (var group in kinds.GroupBy(kv => kv.Value).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                long runtime = 0, gpu = 0;
                foreach (var kv in group)
                {
                    runtime += Profiler.GetRuntimeMemorySizeLong(kv.Key);
                    gpu += GpuBytes(kv.Key);
                }
                runtimeAll += runtime;
                gpuAll += gpu;
                string formats = string.Join(",", group.Select(kv => kv.Key.graphicsFormat.ToString()).Distinct());
                sb.AppendLine($"  textures {group.Key}: {group.Count()} bound, {Mb(gpu)} MB on the GPU by size x format " +
                              $"({formats}), {Mb(runtime)} MB by Profiler.GetRuntimeMemorySizeLong");
            }
            sb.AppendLine($"  textures all: {kinds.Count} bound, {Mb(gpuAll)} MB on the GPU, {Mb(runtimeAll)} MB by the profiler");

            // Draw calls with the trees on and off, the world stopped (the camera still renders each frame).
            long[] on = new long[3], off = new long[3];
            yield return CountDrawCalls(true, on);
            yield return CountDrawCalls(false, off);
            sb.AppendLine($"  draw calls {on[0]} with the trees, {off[0]} without ({on[0] - off[0]:+0;-0} for the trees); " +
                          $"batches {on[1]} / {off[1]} ({on[1] - off[1]:+0;-0}); SetPass calls {on[2]} / {off[2]} ({on[2] - off[2]:+0;-0})");

            // Frame time with the trees on and off, the world running at the stopped hour.
            float[] withTrees = null, without = null;
            yield return FrameTimes(true, v => withTrees = v);
            yield return FrameTimes(false, v => without = v);
            sb.AppendLine($"  frame time with the trees: median {Ms(Median(withTrees))} ms, p90 {Ms(P90(withTrees))} ms; " +
                          $"without: median {Ms(Median(without))} ms, p90 {Ms(P90(without))} ms (unscaled delta, " +
                          $"{withTrees.Length} frames each after a 30-frame warm-up; the editor in batch mode, not a player)");
            SaveText($"numbers-{_arm}.txt", sb.ToString());
            Debug.Log($"[{PlateDir}] {sb}");
        }

        IEnumerator CountDrawCalls(bool treesOn, long[] into)
        {
            var off = new List<Renderer>();
            if (!treesOn)
                foreach (SpriteRenderer r in _trees)
                    if (r != null && r.enabled) { r.enabled = false; off.Add(r); }
            var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            try
            {
                for (int i = 0; i < 3; i++) yield return null;
                into[0] = drawCalls.Valid ? drawCalls.LastValue : -1;
                into[1] = batches.Valid ? batches.LastValue : -1;
                into[2] = setPass.Valid ? setPass.LastValue : -1;
            }
            finally
            {
                drawCalls.Dispose();
                batches.Dispose();
                setPass.Dispose();
                foreach (Renderer r in off) if (r != null) r.enabled = true;
            }
        }

        IEnumerator FrameTimes(bool treesOn, Action<float[]> done)
        {
            var off = new List<Renderer>();
            if (!treesOn)
                foreach (SpriteRenderer r in _trees)
                    if (r != null && r.enabled) { r.enabled = false; off.Add(r); }
            Time.timeScale = 1f;
            var samples = new List<float>();
            try
            {
                for (int i = 0; i < 30; i++) yield return null;
                for (int i = 0; i < 150; i++)
                {
                    yield return null;
                    samples.Add(Time.unscaledDeltaTime);
                }
            }
            finally
            {
                Time.timeScale = 0f;
                foreach (Renderer r in off) if (r != null) r.enabled = true;
            }
            done(samples.ToArray());
        }

        /// <summary>A texture's size on the GPU from its dimensions and format, every mip counted (the
        /// profiler's figure can include a CPU copy in the editor, so both are reported).</summary>
        static long GpuBytes(Texture t)
        {
            long bytes = 0;
            for (int m = 0; m < Math.Max(1, t.mipmapCount); m++)
                bytes += GraphicsFormatUtility.ComputeMipmapSize(Math.Max(1, t.width >> m), Math.Max(1, t.height >> m), t.graphicsFormat);
            return bytes;
        }

        static string Mb(long bytes) => (bytes / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture);
        static string Ms(float seconds) => (seconds * 1000f).ToString("0.00", CultureInfo.InvariantCulture);
        static float Median(float[] v) { var s = v.OrderBy(x => x).ToArray(); return s[s.Length / 2]; }
        static float P90(float[] v) { var s = v.OrderBy(x => x).ToArray(); return s[(int)(s.Length * 0.9f)]; }

        // =============================================================================================
        //  The caption: what each plate was shot through
        // =============================================================================================

        string Caption(string plate, string subject, Vector2 at, ulong hPicture, ulong hControl, double share, string extra)
        {
            Vector3 p = _cam.transform.position;
            float worldHeight = _cam.orthographicSize * 2f;
            Vector4 wind = Shader.GetGlobalVector("_WindWorld");
            var sb = new StringBuilder();
            sb.AppendLine($"{plate}-{_arm}");
            sb.AppendLine(subject);
            sb.AppendLine($"arm: {_arm} ({_armWhy}; pass 4 publishes _TreeMaps 1 on every tree it re-bound, pass 3 on none)");
            sb.AppendLine($"run: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z, Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceType} on " +
                          $"{SystemInfo.graphicsDeviceName}. Hashes are an identity only within this run.");
            sb.AppendLine($"scene: {SceneManager.GetActiveScene().name}; region '{GameServices.CurrentRegionId}'; services healed: {_healed}");
            sb.AppendLine($"save: {_saveGuard}");
            sb.AppendLine($"camera: '{_cam.name}', persistent {MoodGradeDirector.IsPersistentCamera(_cam)}; asked centre ({at.x:0.###}, " +
                          $"{at.y:0.###}); camera at ({p.x:0.####}, {p.y:0.####}, {p.z:0.###}); {_eased}");
            sb.AppendLine($"frame: ortho {_cam.orthographicSize:0.#####} -> {worldHeight * _cam.aspect:0.###} x {worldHeight:0.####} m " +
                          $"(asked {_askedHeight:0.####} m), render texture {_w}x{_h} px, {_follow.WorldUnitsPerRenderedPixel:0.#####} m per rendered pixel");
            sb.AppendLine($"hour: {_pinned}");
            sb.AppendLine($"weather held: {_weather?.Describe() ?? "(not held)"}; _WindWorld ({wind.x:0.0000}, {wind.y:0.0000})");
            sb.AppendLine($"snow: _FoliageSnow {FoliageSnowBridge.Published:0.000} (overridden {FoliageSnowBridge.IsOverridden}); the calendar " +
                          $"would give {FoliageSnowBridge.CoverageFor(Season.HighSummer, 1):0.000} on High Summer day 1 and " +
                          $"{FoliageSnowBridge.CoverageFor(Season.EarlySpring, 1):0.000} on Early Spring day 1");
            sb.AppendLine($"picture {hPicture:x16}; control {hControl:x16}; {share:P2} of the frame changed");
            if (!string.IsNullOrEmpty(extra)) sb.Append(extra);
            return sb.ToString();
        }

        static void SavePlate(string name, byte[] rgbaBottomLeft, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgbaBottomLeft);
            tex.Apply();
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void SaveText(string name, string text)
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), text, new UTF8Encoding(false));
        }

        // =============================================================================================
        //  The weather: the region's own environment, with the wind, the visibility and the sea held
        // =============================================================================================

        /// <summary>
        /// The region's environment with three things held: the wind (the one thing a plate varies), the
        /// visibility (clear) and the sea state (calm), which feed the grade, the day/night light and the rain.
        /// The tide and everything else are the region's own.
        /// </summary>
        sealed class HeldWeather : IEnvironmentService
        {
            readonly IEnvironmentService _inner;
            public Vector2 Wind;

            public HeldWeather(IEnvironmentService inner) { _inner = inner; }

            public int WorldSeed => _inner.WorldSeed;
            public TideProfile ActiveTideProfile { get => _inner.ActiveTideProfile; set => _inner.ActiveTideProfile = value; }
            public EnvironmentSample Sample()
            {
                EnvironmentSample s = _inner.Sample();
                return new EnvironmentSample(Wind, s.CurrentVector, s.TideHeight, SeaState.Calm, 1f, 0f);
            }
            public float TideHeightAt(double totalSeconds) => _inner.TideHeightAt(totalSeconds);
            public float WaterLevelAt(double totalSeconds) => _inner.WaterLevelAt(totalSeconds);
            public float SeaState01At(double totalSeconds) => 0f;

            public string Describe() =>
                $"wind ({Wind.x:0.#}, {Wind.y:0.#}) m/s ({Wind.magnitude:0.#} m/s), visibility 1, sea Calm; tide the region's own";
        }

        // =============================================================================================
        //  DontDestroyOnLoad bookkeeping (#764)
        // =============================================================================================

        static Scene PersistentScene()
        {
            var probe = new GameObject("__ddolProbe");
            Object.DontDestroyOnLoad(probe);
            Scene s = probe.scene;
            Object.DestroyImmediate(probe);
            return s;
        }

        static List<GameObject> PersistentRoots()
        {
            Scene s = PersistentScene();
            return s.IsValid() ? s.GetRootGameObjects().ToList() : new List<GameObject>();
        }
    }
}
