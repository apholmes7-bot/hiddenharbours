using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE FACET-ID BUDGET, COUNTED ON THE OWNER'S OWN ROUTE — AND HER FIGURE AT ST PETERS AFTER NINE
    /// MILE CREEK</b> (ADR 0056; the owner: "why do we see old character sprites sometimes? when
    /// loading st peters ...").
    ///
    /// <para>The facet buffer's alpha is one 8-bit id space shared by hull ids, their twelve-wide fore
    /// blocks and ashore figures. When it runs out the player's ashore figure is refused and she is drawn
    /// with her old sprite until her next arrival. This fixture walks the route the game takes — St
    /// Peters (the start scene), the game's own additive travel to Nine Mile Creek, back to St Peters,
    /// then Rice Point and South Water — and at every stage writes a census of who holds ids: every
    /// registered hull by scene and kind, which hulls have somebody on deck, which hold a block, every
    /// figure id, and the pool. From it, per stage:</para>
    /// <list type="bullet">
    /// <item><b>eager</b> = 13 × hulls + figures: what main's rule (a block per live hull) wants;</item>
    /// <item><b>lazy</b> = hulls + 12 × hulls-with-somebody-aboard + figures: what ADR 0056 wants;</item>
    /// <item><b>worst in play</b> = the same, plus 12 for the player boarding a boat nobody stands on,
    /// plus 1 for a co-op friend's figure.</item>
    /// </list>
    /// <para>Every frame of the route her presenter is sampled, and every change in whether she is drawn
    /// as a mesh or refused is logged with the pool beside it. At St Peters after Nine Mile Creek she is
    /// photographed close, at noon, with the ashore switch as shipped (ON): the sprite on main, the
    /// mesh figure with the fix.</para>
    ///
    /// <para><b>It compiles on main</b>: every member ADR 0056 added is read by reflection, so the same
    /// file counts both builds and names its plates <c>-main</c> or <c>-fix</c> by what it finds.
    /// Needs a graphics device (the plate); ignored, loudly, on CI. ⚠️ The plate directory is shared by
    /// every worktree: a floor file is written first, read only plates newer than it. ⚠️ St Peters is
    /// the start scene and is left through <see cref="ShellFlow.Reset"/>, never StartNewGame, and the
    /// save path must be redirected (<c>HH_TEST_RUN_SAVE</c>) before this runs.</para>
    /// </summary>
    public sealed class FacetIdBudgetPlayTests
    {
        const string PlateDir = "facet-id-budget";
        const string EvidenceDir = "Evidence~/nmc-h6/facet-budget";
        const string StPetersScene = "StPeters";
        const string NineMileCreekScene = "NineMileCreek";
        const string RicePointScene = "RicePoint";
        const string SouthWaterScene = "SouthWater";
        const string StPetersRegion = "region.st_peters";
        const string NineMileCreekRegion = "region.nine_mile_creek";
        const string RicePointRegion = "region.rice_point";
        const string SouthWaterRegion = "region.south_water";
        const float Noon = 12f;
        /// <summary>Three of the ambient fleet's 0.5 s gate ticks: long enough for a region's late
        /// frees (and late registrations) to land before the census.</summary>
        const float SettleSeconds = 1.5f;
        const float TravelDeadlineSeconds = 90f;
        const int DecideFrames = 300;
        /// <summary>The game's closest on-foot view, 5.625 m tall (33.75 m ÷ 6, a crisp step).</summary>
        const float CloseOrthoSize = 2.8125f;
        const int ForeBlock = IsoFacetHullRenderer.DeckOccupantSlots;
        const int EagerCostPerHull = 1 + ForeBlock;
        const int OverflowId = 255;

        static readonly string[] KnownKinds =
        {
            "MooredBoat", "BoatController", "ParkedVehicle", "ParkedTrailer", "BeachedWreck",
            "AmbientFleetPresenter", "NpcTrawlBoats", "NpcPotBoats", "DevBoatPicker",
        };

        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
        const BindingFlags HiddenInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly FieldInfo HullsField = typeof(IsoFacetHullRegistry).GetField("s_Hulls", Hidden);
        static readonly FieldInfo FigureIdsField = typeof(IsoFacetHullRegistry).GetField("s_FigureIds", Hidden);
        static readonly FieldInfo PoolField = typeof(IsoFacetHullRegistry).GetField("s_Pool", Hidden);
        static readonly FieldInfo SlotsField = typeof(IsoFacetHullRenderer).GetField("_deckSlots", HiddenInstance);

        static string s_floor;

        WharfNightStage _stage;
        bool _loadedAny;
        bool _introOnEntry;
        string _label;
        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        readonly List<string> _notes = new List<string>();
        readonly List<string> _transitions = new List<string>();
        readonly List<string> _refusals = new List<string>();
        readonly List<string> _summary = new List<string>();
        string _lastState;
        DeckRiderMeshPresenter _presenter;
        DeckRiderVisual _rider;

        // =============================================================================================
        //  The floor, the census rows
        // =============================================================================================

        [System.Serializable]
        sealed class HullRow
        {
            public string name, scene, kind;
            public int hull, fore, claims;
        }

        [System.Serializable]
        sealed class SceneRow
        {
            public string scene;
            public int hulls, aboard, blocks;
        }

        [System.Serializable]
        sealed class Census
        {
            public string build, stage, activeScene;
            public int hulls, hullsWithSomebodyAboard, hullsHoldingABlock, hullsOn255, figures;
            public int eagerWanted, lazyWanted, worstInPlayEager, worstInPlayLazy, heldNow;
            public int poolNextId, poolGeneralTop, poolFreeSingles, poolFreeBlocks, poolPlayerReserve, poolFreePlayerIds;
            public bool herAboard, herDrawsAshore, herRefused;
            public int herFigureId;
            public string herReason;
            public SceneRow[] scenes;
            public HullRow[] hullRows;
            public int[] figureIds;
        }

        [OneTimeSetUp]
        public void WriteTheFloor()
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            s_floor = Path.Combine(dir, "_floor.txt");
            File.WriteAllText(s_floor,
                "Floor for " + nameof(FacetIdBudgetPlayTests) + " at " +
                System.DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "Only plates in this directory NEWER than this file were shot by this run.\n",
                new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] FLOOR written: {s_floor}");
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);
            _notes.Clear(); _transitions.Clear(); _refusals.Clear(); _summary.Clear();
            _lastState = null; _presenter = null; _rider = null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;
            if (_stage != null)
            {
                yield return _stage.TearDown();
                _stage = null;
            }
            if (!_loadedAny) yield break;

            foreach (string name in new[] { NineMileCreekScene, RicePointScene, SouthWaterScene, StPetersScene })
            {
                Scene s = SceneManager.GetSceneByName(name);
                if (s.IsValid() && s.isLoaded) yield return SceneManager.UnloadSceneAsync(s);
            }
            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go)) Object.DestroyImmediate(go);

            GameServices.Reset();
            GameServices.OpeningCinematicRunning = _introOnEntry;
            ShellFlow.Reset();
            InteractionGate.Reset();
            _loadedAny = false;
            yield return null;
        }

        // =============================================================================================
        //  The route
        // =============================================================================================

        /// <summary>
        /// St Peters cold → Nine Mile Creek (the game's additive travel) → St Peters (her plate) → Rice
        /// Point → South Water → St Peters. A census at each arrival and again after it settles; her
        /// presenter sampled every frame throughout. Asserts, AFTER every census and the plate are
        /// written: she draws ashore at St Peters after Nine Mile Creek, and the pool never refused her
        /// anywhere on the route.
        /// </summary>
        [UnityTest]
        public IEnumerator Budget_OnTheOwnersRoute_HerFigureIsNeverRefused_AndAtStPetersAfterNineMileCreekSheIsTheMesh()
        {
            WharfNightStage.RequireAGraphicsDevice();
            AssertTheSaveIsRedirected();
            _label = IsTheFix() ? "fix" : "main";
            FreshPoolIfNothingHoldsAnId();

            _introOnEntry = GameServices.OpeningCinematicRunning;
            _loadedAny = true;
            _stage = new WharfNightStage(StPetersScene, PlateDir);
            yield return _stage.Load();
            if (ShellFlow.WorldInputBlocked)
            {
                Debug.Log($"[{PlateDir}] St Peters opened on the shell (phase {ShellFlow.Phase}); ShellFlow.Reset() — " +
                          "never StartNewGame(), which would write the shared savegame.");
                ShellFlow.Reset();
            }
            InteractionGate.Reset();
            yield return _stage.SetNight(Noon);
            _notes.Add(SwitchNote());

            yield return Watch("stp-cold", SettleSeconds);
            Census stpCold = Count("stp-cold");

            yield return TravelTo(NineMileCreekRegion, NineMileCreekScene, "to-nmc");
            Count("nmc-arrival");
            yield return Watch("nmc", SettleSeconds);
            Census nmc = Count("nmc-settled");

            yield return TravelTo(StPetersRegion, StPetersScene, "to-stp");
            Count("stp-after-nmc-arrival");
            yield return WaitForHerDecision("stp-after-nmc");
            yield return Watch("stp-after-nmc", SettleSeconds);
            Census stpAfter = Count("stp-after-nmc");
            yield return PlateHer("stp-after-nmc");
            Time.timeScale = 1f;   // the plate froze the world; the travels below need it running

            yield return TravelTo(RicePointRegion, RicePointScene, "to-rice-point");
            Count("rice-point-arrival");
            yield return Watch("rice-point", SettleSeconds);
            Count("rice-point-settled");

            yield return TravelTo(SouthWaterRegion, SouthWaterScene, "to-south-water");
            Count("south-water-arrival");
            yield return Watch("south-water", SettleSeconds);
            Count("south-water-settled");

            yield return TravelTo(StPetersRegion, StPetersScene, "to-stp-final");
            yield return WaitForHerDecision("stp-final");
            yield return Watch("stp-final", SettleSeconds);
            Count("stp-final");

            WriteTheSummary();

            Assert.IsNotNull(stpCold, "harness: no census at St Peters");
            Assert.IsNotNull(nmc, "harness: no census at Nine Mile Creek");
            Assert.IsTrue(stpAfter.herDrawsAshore,
                $"[{PlateDir}] {_label}: at St Peters after Nine Mile Creek she is NOT drawn as her mesh figure — " +
                $"'{stpAfter.herReason}' (refused {stpAfter.herRefused}). This is the owner's report: the old " +
                "sprite. Pool: next " + stpAfter.poolNextId + ", free singles " + stpAfter.poolFreeSingles +
                ", free player ids " + stpAfter.poolFreePlayerIds + ".");
            Assert.IsEmpty(_refusals,
                $"[{PlateDir}] {_label}: the pool refused her figure {_refusals.Count} time(s) on the route:\n" +
                string.Join("\n", _refusals));
        }

        // =============================================================================================
        //  Travel, watch, decide
        // =============================================================================================

        IEnumerator TravelTo(string regionId, string sceneName, string stage)
        {
            var loader = Object.FindFirstObjectByType<HiddenHarbours.World.RegionSceneLoader>();
            Assert.IsNotNull(loader, $"[{PlateDir}] no RegionSceneLoader: the route must take the game's own travel");
            Assert.IsTrue(loader.Registry.TryGet(regionId, out var destination), $"no region '{regionId}'");
            Assert.IsTrue(loader.Travel(destination), $"[{PlateDir}] the game declined to travel to {regionId}");
            float deadline = Time.realtimeSinceStartup + TravelDeadlineSeconds;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
            {
                Sample(stage);
                yield return null;
            }
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name, $"[{PlateDir}] {sceneName} never became active");
            for (int i = 0; i < 3; i++)
            {
                Sample(stage);
                yield return null;
            }
        }

        IEnumerator Watch(string stage, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                Sample(stage);
                yield return null;
            }
        }

        /// <summary>Frames until her presenter has DECIDED on this arrival: drawn, or refused.</summary>
        IEnumerator WaitForHerDecision(string stage)
        {
            for (int f = 0; f < DecideFrames; f++)
            {
                Sample(stage);
                if (_presenter != null && (_presenter.DrawsAshore || _presenter.AshoreRefused)) yield break;
                yield return null;
            }
            _notes.Add($"{stage}: her presenter had not decided after {DecideFrames} frames " +
                       $"('{(_presenter != null ? _presenter.NotDrawingReason : "no presenter")}')");
        }

        bool FindHerPresenter()
        {
            if (_presenter != null) return true;
            Transform her = GameServices.PlayerTransform;
            if (her == null) return false;
            _rider = her.GetComponentInChildren<DeckRiderVisual>(true);
            _presenter = _rider != null ? _rider.GetComponent<DeckRiderMeshPresenter>() : null;
            return _presenter != null;
        }

        /// <summary>One frame of her: logged only when drawn/refused changes, with the pool beside it.</summary>
        void Sample(string stage)
        {
            if (!FindHerPresenter()) return;
            string state = $"draws {_presenter.DrawsAshore}, refused {_presenter.AshoreRefused}";
            if (state == _lastState) return;
            _lastState = state;
            ReadPool(out int next, out int top, out int singles, out int blocks, out int reserve, out int freePlayer);
            string line = $"{stage} f{Time.frameCount} [{SceneManager.GetActiveScene().name}] {state} " +
                          $"('{_presenter.NotDrawingReason}'); hulls {IsoFacetHullRegistry.Count}, figures " +
                          $"{IsoFacetHullRegistry.FigureCount}, pool next {next}, free singles {singles}, " +
                          $"free blocks {blocks}, free player ids {freePlayer}/{reserve}";
            _transitions.Add(line);
            if (_presenter.AshoreRefused) _refusals.Add(line);
            Debug.Log($"[{PlateDir}] {_label} HER: {line}");
        }

        // =============================================================================================
        //  The census
        // =============================================================================================

        Census Count(string stage)
        {
            FindHerPresenter();
            var hulls = HullsField != null
                ? ((IEnumerable<IsoFacetHullRenderer>)HullsField.GetValue(null)).Where(h => h != null).ToList()
                : new List<IsoFacetHullRenderer>();
            var rows = hulls.Select(h => new HullRow
            {
                name = h.transform.root.name == h.name ? h.name : h.transform.root.name + "/" + h.name,
                scene = h.gameObject.scene.name,
                kind = KindOf(h),
                hull = h.HullId,
                fore = h.ForeHullId,
                claims = ClaimsOn(h),
            }).OrderBy(r => r.scene, System.StringComparer.Ordinal).ThenBy(r => r.hull).ToArray();

            var figureIds = FigureIdsField != null
                ? ((IEnumerable<int>)FigureIdsField.GetValue(null)).OrderBy(i => i).ToArray()
                : new int[0];
            ReadPool(out int next, out int top, out int singles, out int blocks, out int reserve, out int freePlayer);

            int nHulls = rows.Length, aboard = rows.Count(r => r.claims > 0), held = rows.Count(r => r.fore != 0);
            int nFigures = figureIds.Length;
            bool herAboard = _rider != null && _rider.LiveHullPresenter != null;
            int distinctHullIds = rows.Select(r => r.hull).Where(id => id != 0).Distinct().Count();
            var c = new Census
            {
                build = _label, stage = stage, activeScene = SceneManager.GetActiveScene().name,
                hulls = nHulls, hullsWithSomebodyAboard = aboard, hullsHoldingABlock = held,
                hullsOn255 = rows.Count(r => r.hull == OverflowId), figures = nFigures,
                eagerWanted = EagerCostPerHull * nHulls + nFigures,
                lazyWanted = nHulls + ForeBlock * aboard + nFigures,
                worstInPlayEager = EagerCostPerHull * nHulls + nFigures + 1,
                worstInPlayLazy = nHulls + ForeBlock * aboard + nFigures + (herAboard ? 0 : ForeBlock) + 1,
                heldNow = distinctHullIds + ForeBlock * held + nFigures,
                poolNextId = next, poolGeneralTop = top, poolFreeSingles = singles, poolFreeBlocks = blocks,
                poolPlayerReserve = reserve, poolFreePlayerIds = freePlayer,
                herAboard = herAboard,
                herDrawsAshore = _presenter != null && _presenter.DrawsAshore,
                herRefused = _presenter != null && _presenter.AshoreRefused,
                herReason = _presenter != null ? _presenter.NotDrawingReason : "no presenter",
                herFigureId = _presenter != null && _presenter.AshoreFigure != null ? _presenter.AshoreFigure.FigureId : 0,
                scenes = rows.GroupBy(r => r.scene).OrderBy(g => g.Key, System.StringComparer.Ordinal)
                    .Select(g => new SceneRow
                    {
                        scene = g.Key, hulls = g.Count(), aboard = g.Count(r => r.claims > 0),
                        blocks = g.Count(r => r.fore != 0),
                    }).ToArray(),
                hullRows = rows,
                figureIds = figureIds,
            };

            string perScene = string.Join("; ", c.scenes.Select(s => $"{s.scene} {s.hulls}h/{s.aboard}a/{s.blocks}b"));
            string kinds = string.Join(", ", rows.GroupBy(r => r.scene + ":" + r.kind)
                .OrderBy(g => g.Key, System.StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}"));
            string line = $"{_label} {stage} [{c.activeScene}]: hulls {nHulls} (aboard {aboard}, holding a block {held}, " +
                          $"on 255 {c.hullsOn255}), figures {nFigures} [{string.Join(",", figureIds)}]; WANTED eager {c.eagerWanted}, " +
                          $"lazy {c.lazyWanted}; worst in play eager {c.worstInPlayEager}, lazy {c.worstInPlayLazy}; " +
                          $"held now {c.heldNow}; pool next {next}, top {top}, free singles {singles}, free blocks " +
                          $"{blocks}, player ids free {freePlayer}/{reserve}; HER aboard {herAboard}, draws " +
                          $"{c.herDrawsAshore}, refused {c.herRefused}, id {c.herFigureId} ('{c.herReason}'); " +
                          $"by scene: {perScene}; by kind: {kinds}";
            _summary.Add(line);
            Debug.Log($"[{PlateDir}] CENSUS {line}");

            Directory.CreateDirectory(EvidenceDir);
            File.WriteAllText(Path.Combine(EvidenceDir, $"{_label}-{stage}.json"), JsonUtility.ToJson(c, true),
                              new UTF8Encoding(false));
            return c;
        }

        static int ClaimsOn(IsoFacetHullRenderer hull)
        {
            if (SlotsField == null) return -1;
            var slots = (System.Array)SlotsField.GetValue(hull);
            FieldInfo owner = SlotsField.FieldType.GetElementType().GetField("Owner");
            int n = 0;
            foreach (object slot in slots) if (owner.GetValue(slot) != null) n++;
            return n;
        }

        static string KindOf(IsoFacetHullRenderer hull)
        {
            for (Transform t = hull.transform; t != null; t = t.parent)
                foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
                    if (mb != null && KnownKinds.Contains(mb.GetType().Name)) return mb.GetType().Name;
            return "root:" + hull.transform.root.name;
        }

        static void ReadPool(out int next, out int top, out int freeSingles, out int freeBlocks,
                             out int reserve, out int freePlayer)
        {
            next = top = freeSingles = freeBlocks = reserve = freePlayer = -1;
            object pool = PoolField != null ? PoolField.GetValue(null) : null;
            if (pool == null) return;
            System.Type t = pool.GetType();
            next = (int)t.GetField("_nextId", HiddenInstance).GetValue(pool);
            FieldInfo topField = t.GetField("_generalTop", HiddenInstance);
            top = topField != null ? (int)topField.GetValue(pool) : OverflowId - 1;
            freeSingles = ((Stack<int>)t.GetField("_freeIds", HiddenInstance).GetValue(pool)).Count;
            freeBlocks = ((Stack<int>)t.GetField("_freeForeBlocks", HiddenInstance).GetValue(pool)).Count;
            FieldInfo reserveField = t.GetField("_playerReserve", HiddenInstance);
            reserve = reserveField != null ? (int)reserveField.GetValue(pool) : 0;
            FieldInfo playerField = t.GetField("_freePlayerIds", HiddenInstance);
            freePlayer = playerField != null ? ((Stack<int>)playerField.GetValue(pool)).Count : 0;
        }

        // =============================================================================================
        //  Her plate
        // =============================================================================================

        IEnumerator PlateHer(string stage)
        {
            Transform her = GameServices.PlayerTransform;
            Assert.IsNotNull(her, $"[{PlateDir}] no player to photograph at {stage}");
            yield return _stage.FrameOn(her.position);
            Camera cam = _stage.Camera;
            cam.orthographicSize = CloseOrthoSize;   // set straight before the shutter: the follow resets zoom
            byte[] shot = _stage.Capture();
            string name = $"{stage}-{_label}.png";
            _stage.SavePlate(name, shot);
            string state = _presenter == null ? "no presenter"
                : $"draws ashore {_presenter.DrawsAshore}, refused {_presenter.AshoreRefused}, figure id " +
                  $"{(_presenter.AshoreFigure != null ? _presenter.AshoreFigure.FigureId : 0)} ('{_presenter.NotDrawingReason}')";
            string line = $"PLATE {name}: her at {(Vector2)her.position} in {SceneManager.GetActiveScene().name}, " +
                          $"{(GameServices.Clock != null ? GameServices.Clock.HourOfDay.ToString("00.00", CultureInfo.InvariantCulture) : "no clock")} h, ortho {CloseOrthoSize} ({2f * CloseOrthoSize:0.00} m tall); {state}";
            _summary.Add(line);
            Debug.Log($"[{PlateDir}] {line}");
        }

        // =============================================================================================
        //  Harness
        // =============================================================================================

        static bool IsTheFix() => typeof(IsoFacetIdPool).GetField("_generalTop", HiddenInstance) != null;

        /// <summary>A pool as fresh as a new game's — but only when nothing holds an id, so nothing can
        /// free an id into a pool that never issued it. Otherwise the inherited pool is used and said.</summary>
        void FreshPoolIfNothingHoldsAnId()
        {
            if (IsoFacetHullRegistry.Count != 0 || IsoFacetHullRegistry.FigureCount != 0)
            {
                _notes.Add($"pool INHERITED from earlier tests ({IsoFacetHullRegistry.Count} hulls, " +
                           $"{IsoFacetHullRegistry.FigureCount} figures registered at the start)");
                return;
            }
            ConstructorInfo withReserve = typeof(IsoFacetIdPool).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
            IsoFacetIdPool pool;
            if (withReserve != null)
            {
                PropertyInfo reserve = typeof(GameServices).GetProperty("PlayerFigureIdReserve", BindingFlags.Public | BindingFlags.Static);
                pool = (IsoFacetIdPool)withReserve.Invoke(new object[] { reserve != null ? (int)reserve.GetValue(null) : 0 });
            }
            else pool = new IsoFacetIdPool();
            IsoFacetHullRegistry.SwapIdPoolForTests(pool);
            _notes.Add("pool FRESH at the start, as a new game's");
        }

        static void AssertTheSaveIsRedirected()
        {
            Assert.AreNotEqual(Path.GetFullPath(SaveStore.DefaultPath), Path.GetFullPath(SaveStore.ActivePath),
                $"[{PlateDir}] the savegame is NOT redirected ({SaveStore.TestRunSaveVariable} unset): St Peters' " +
                "persistent core may write the save every worktree shares. Set it in the launch shell.");
        }

        static string SwitchNote()
        {
            GameConfig c = GameServices.Config;
            return c == null ? "no GameConfig published"
                : $"switches as loaded: MeshCharacter {c.MeshCharacter}, MeshCharacterAshore {c.MeshCharacterAshore}, " +
                  $"MeshCastAshore {c.MeshCastAshore}";
        }

        void WriteTheSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"facet-id budget, build '{_label}', {System.DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}");
            foreach (string n in _notes) sb.AppendLine("NOTE " + n);
            foreach (string s in _summary) sb.AppendLine(s);
            sb.AppendLine("HER TRANSITIONS:");
            foreach (string t in _transitions) sb.AppendLine("  " + t);
            Directory.CreateDirectory(EvidenceDir);
            File.WriteAllText(Path.Combine(EvidenceDir, $"summary-{_label}.txt"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] SUMMARY written to {EvidenceDir}/summary-{_label}.txt\n{sb}");
        }

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
