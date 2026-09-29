using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>The villagers ashore as meshes: the plates, and the numbers for the report</b> (item 8; ADR 0044 §8,
    /// amendment 2026-09-27). Every case needs a graphics device and SKIPS on CI's Null device.
    ///
    /// <para><b>The plates</b> are shot with both cast switches ON, on a runtime COPY of the loaded config (the
    /// asset is never written):</para>
    /// <list type="bullet">
    /// <item>St Peters at dawn and at noon, the wharf with its villagers.</item>
    /// <item>Nine Mile Creek at noon, its two villagers refused honestly while the facet ids are exhausted.</item>
    /// <item>A villager inside a house with the player.</item>
    /// <item>A villager in conversation with the player.</item>
    /// <item>Two villagers of one skin idling side by side, not in step.</item>
    /// <item>The switch OFF against ON on the same frame.</item>
    /// </list>
    ///
    /// <para><b>Each plate proves its subjects are in it.</b> Each subject's point is in the frame and a renderer
    /// of hers rasterised. Each subject removed ALONE changes her own box by more than the back-to-back noise
    /// there. A control with every subject removed, saved beside the plate, hashes differently. The mesh claims
    /// are <see cref="MeshProblemsOf"/>'s, and every mesh villager's frame is the one (worldSeed, gameTime) asks
    /// for. A villager in frame but hidden by the scene (a roof, a wall) is named in the caption rather than
    /// failed: that is what the owner reads the plate for.</para>
    ///
    /// <para><b>The numbers</b> are the facet census at St Peters, at a cold Nine Mile Creek and at the creek
    /// arrived from St Peters, each ON and OFF, plus frame time and draw calls at St Peters noon, ON against
    /// OFF. They are logged as <c>NUMBERS</c> lines and written beside the plates as <c>numbers-*.txt</c>.</para>
    ///
    /// <para>The hour is pinned on the clock's own time scale (<see cref="PinTheHour"/>), never with
    /// <see cref="WharfNightStage.SetNight"/>, whose lamp gate is a night plate's wait. The frame is the game's own
    /// (<see cref="WharfNightStage.FrameOn"/>) and is never widened. St Peters is the start scene, so the shell is
    /// cleared with <see cref="ShellFlow.Reset"/>, never <c>StartNewGame()</c>, which would write the save every
    /// worktree shares.</para>
    /// </summary>
    public class VillagersAshorePlatePlayTests
    {
        private const string PlateDir = "villagers-ashore-plates";
        private const string StPetersScene = "StPeters";
        private const string NineMileCreekScene = "NineMileCreek";
        private const string NineMileCreekRegionId = "region.nine_mile_creek";

        /// <summary><c>SpriteShadowCastsPlayTests</c>' dawn. The island has no dawn constant of its own.</summary>
        private const float DawnHour = 6.5f;
        private const float MorningHour = 10f;
        private const float NoonHour = 12f;
        private const float AfternoonHour = 13f;

        // StPetersRoutines' station ids (editor-only code, so spelled out here).
        private const string SlipHeadStation = "station.st_peters.slip_head";
        private const string WharfHeadStation = "station.st_peters.wharf_head";
        private const string PostOfficeCounterStation = "station.st_peters.post_office_counter";
        private const string GreenStation = "station.st_peters.green_a";

        private const string BasilName = "BasilSamson";
        private const string JuniorName = "JuniorPoirier";
        private const string RoseName = "RoseMacIsaac";

        /// <summary>St Peters stands six routines today. Fewer means the scene lost villagers.</summary>
        private const int VillagersOnTheIsland = 6;

        /// <summary>The saved Nine Mile Creek holds Wendell and Hector. Claudette is in NineMileCreekPeople's list
        /// and not in the saved scene.</summary>
        private const int VillagersAtTheCreek = 2;

        /// <summary>The facet id hulls share at exhaustion. A figure never takes it.</summary>
        private const int OverflowId = 255;

        private const string RefusalWarningMark = "gets NO facet id and keeps her sprite";
        private const string PresenterFrameMark = "CharacterFigurePresenter";

        private const float SouthDegrees = 180f;
        private const float WestDegrees = 270f;
        private const float SideBySideMetres = 1.4f;
        private const float TalkStandOffMetres = 1.1f;
        private const float YawToleranceDegrees = 0.01f;

        /// <summary>Her box on the plate: this far each side of her feet, below them and above them.</summary>
        private const float BoxHalfWidthMetres = 0.9f;
        private const float BoxBelowMetres = 0.5f;
        private const float BoxAboveMetres = 2.6f;

        /// <summary>A pixel has changed when its three channels move by more than this in sum (0..255 each).</summary>
        private const int PixelThreshold = 30;

        /// <summary>Removing a drawn villager changes at least this many pixels of her box.</summary>
        private const int MinSubjectPixels = 40;

        /// <summary>The share of the frame the framing may fill, so every subject's feet stay clear of the edge.</summary>
        private const float FrameFill = 0.9f;

        /// <summary>Only used when no camera can be read: St Peters' plate camera as the first plate run measured
        /// it (900 px high at the game's pixel-perfect zoom). The frame is otherwise always read from the plate
        /// camera itself, once <see cref="WharfNightStage.FrameOn"/> has given it the plate's height.</summary>
        private const float FallbackOrthoSize = 7.03f;
        private const float FallbackAspect = 1.333f;

        /// <summary>A villager the day has on her way somewhere is held this far through her def's walking band.</summary>
        private const float WalkBandMiddle = 0.5f;
        private const int ReframeMaxFrames = 400;

        /// <summary>ON, OFF, ON, OFF: two rounds, so the report shows the noise beside the difference.</summary>
        private const int RenderRounds = 2;

        /// <summary>At least one second for the light, and several of the routine's 0.25 s shelter checks.</summary>
        private const float SettleSeconds = 1.5f;
        private const int MaxSettleFrames = 900;
        private const int MaxTravelFrames = 1200;
        private const int SwitchSettleFrames = 5;
        private const int NoRetryFrames = 90;
        private const int TalkSettleFrames = 30;
        private const float ShelterWaitSeconds = 0.6f;
        private const float StandSettleSeconds = 1f;
        private static readonly float[] InsideInsetsMetres = { 1.2f, 0.9f, 0.6f, 0.4f };
        private const int RecorderFrames = 60;
        private const int RenderSamples = 60;

        private static string s_floor;

        private readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        private readonly List<Object> _spawned = new List<Object>();
        private readonly List<IsoCharacterSprite> _heldHeadings = new List<IsoCharacterSprite>();
        private readonly List<IsoCharacterSprite> _heldSpeeds = new List<IsoCharacterSprite>();
        private readonly List<IsoCharacterSprite> _walkers = new List<IsoCharacterSprite>();
        private readonly List<string> _refusalStacks = new List<string>();

        private WharfNightStage _stage;
        private bool _loadedAny;
        private bool _introOnEntry;
        private GameConfig _loadedConfig;
        private float _hour;
        private string _healed = "none";

        private PlayerWalkController _player;
        private IsoCharacterSprite _playerIso;
        private Rigidbody2D _playerBody;
        private bool _playerBodyTaken;
        private bool _playerBodyWasSimulated;

        private bool _listening;
        private StackTraceLogType _warningTraceBefore;

        // =============================================================================================
        //  The floor, the snapshot and the teardown
        // =============================================================================================

        /// <summary>The plate directory is one directory for the whole machine; only plates newer than this file
        /// were shot by this run.</summary>
        [OneTimeSetUp]
        public void WriteTheFloor()
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            s_floor = Path.Combine(dir, "_floor.txt");
            File.WriteAllText(
                s_floor,
                "Floor for " + nameof(VillagersAshorePlatePlayTests) + " at " +
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "Only plates in this directory NEWER than this file were shot by this run.\n",
                new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] FLOOR written: {s_floor}");
        }

        [UnitySetUp]
        public IEnumerator SetUpRegion()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);
            _refusalStacks.Clear();
            _walkers.Clear();
            _healed = "none";
            yield return null;
        }

        /// <summary>
        /// The statics first (the log guard, engine time, the log listener and its stack trace type), then what
        /// the case held (headings, speeds, the player's body), then the loaded config back BEFORE the stage goes
        /// (GameRoot.OnDestroy nulls GameServices.Config only while it holds its own asset), then the regions. The
        /// creek is unloaded too when a case travelled there, because the stage unloads only its own scene. Every
        /// DontDestroyOnLoad root the regions installed is destroyed by IDENTITY against the snapshot.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;
            StopListening();

            foreach (IsoCharacterSprite iso in _heldHeadings) if (iso != null) iso.ReleaseHeading();
            _heldHeadings.Clear();
            foreach (IsoCharacterSprite iso in _heldSpeeds) if (iso != null) iso.ReleaseSpeed();
            _heldSpeeds.Clear();
            if (_playerBody != null && _playerBodyTaken) _playerBody.simulated = _playerBodyWasSimulated;
            _player = null;
            _playerIso = null;
            _playerBody = null;
            _playerBodyTaken = false;

            if (_loadedConfig != null) GameServices.Config = _loadedConfig;
            _loadedConfig = null;
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();

            if (_stage != null)
            {
                yield return _stage.TearDown();
                _stage = null;
            }
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s.isLoaded && s.name == NineMileCreekScene && SceneManager.sceneCount > 1)
                    yield return SceneManager.UnloadSceneAsync(s);
            }

            if (!_loadedAny) yield break;

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
        //  The plates
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>PLATE 1: St Peters at dawn, the wharf with its villagers.</b> The frame holds the slip head (the
        /// wharf's landward end) and the villagers out of doors nearest it that fit. At dawn Basil is still on his
        /// 3.3-hour walk out (he reaches the wharf head at about 8:18). If nobody out of doors is within one frame
        /// of the wharf, the frame holds the villager out of doors nearest it instead, and the caption says so.
        /// The clock is stopped for the plate, so a villager on her way is held at her walking pace
        /// (<see cref="HoldTheWalkers"/>).
        /// </summary>
        [UnityTest]
        public IEnumerator Plate_TheWharfWithItsVillagers_StPetersDawn()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return ShootTheWharf("1-wharf-stpeters-dawn", DawnHour);
        }

        /// <summary>⭐ <b>PLATE 2: St Peters at noon, the wharf with its villagers</b>, framed as plate 1.</summary>
        [UnityTest]
        public IEnumerator Plate_TheWharfWithItsVillagers_StPetersNoon()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return ShootTheWharf("2-wharf-stpeters-noon", NoonHour);
        }

        private IEnumerator ShootTheWharf(string key, float hour)
        {
            yield return Arrive(StPetersScene, hour);
            List<IsoCharacterSprite> villagers = Villagers();
            HoldTheWalkers(villagers);
            yield return _stage.FrameOn(Station(SlipHeadStation).Position);

            Vector2 centre = FrameTheWharf(villagers, out List<IsoCharacterSprite> held, out string framing);
            Assert.Greater(held.Count, 0,
                $"harness: nobody is out of doors on St Peters at {Hhmm(hour)}:\n  " +
                string.Join("\n  ", villagers.Select(Describe)));
            yield return ReframeOn(centre);

            AssertEveryVillagerIsHerMesh(villagers, key);
            List<IsoCharacterSprite> inFrame = villagers.Where(v => Shows(v) && InFrame(v.transform)).ToList();
            Shoot(key, $"St Peters {Hhmm(hour)}, the game's camera parked on {Fmt(centre)}: {framing}",
                  strict: new List<IsoCharacterSprite>(), others: inFrame, everyone: villagers,
                  alsoInFrame: new List<Transform>(), extra: null);
        }

        /// <summary>
        /// ⭐ <b>PLATE 3: Nine Mile Creek at noon, its two villagers refused honestly.</b> The creek is loaded cold,
        /// as the existing creek plates load it, and today its hulls exhaust the facet ids at load. A refused
        /// villager is her whole sprite, with one warning for her one ask and no ask again. The listener is up
        /// before the load, because the villagers ask in the region's first frames. The warnings carry no context
        /// object, so they are attributed by their stack (the presenter's, not the player's DeckRiderMeshPresenter)
        /// and counted against the refused presenters: a presenter is refused only after an ask, so as many
        /// warnings as refusals is one each. When the pool has room (after #877 Phase B) they are asserted as
        /// meshes instead, and the caption says which.
        /// </summary>
        [UnityTest]
        public IEnumerator Plate_TheCreeksTwoVillagers_RefusedHonestly_NineMileCreekNoon()
        {
            WharfNightStage.RequireAGraphicsDevice();
            const string key = "3-creek-refused-honestly-noon";
            StartListening();
            yield return Arrive(NineMileCreekScene, NoonHour);

            List<IsoCharacterSprite> creek = CreekVillagers();
            int atArrival = PresenterRefusals();
            for (int i = 0; i < NoRetryFrames; i++) yield return null;
            int afterWaiting = PresenterRefusals();

            List<IsoCharacterSprite> refused = creek.Where(v => Presenter(v) != null && Presenter(v).AshoreRefused).ToList();
            var problems = new List<string>();
            foreach (IsoCharacterSprite v in creek)
                problems.AddRange(refused.Contains(v) ? RefusalProblemsOf(v) : MeshProblemsOf(v).Concat(FrameProblemsOf(v)));
            Assert.IsEmpty(problems, $"{key}: a villager at the creek is neither refused honestly nor her mesh:\n  " +
                                     string.Join("\n  ", problems));
            Assert.AreEqual(0, UnattributedRefusals(),
                $"{key}: {UnattributedRefusals()} refusal warnings carried no stack trace, so they cannot be attributed " +
                $"(the warning stack trace type was forced to {StackTraceLogType.ScriptOnly})");
            Assert.AreEqual(refused.Count, atArrival,
                $"{key}: {refused.Count} villagers are refused and their presenters logged {atArrival} refusal " +
                "warnings, where one ask each gives one warning each");
            Assert.AreEqual(atArrival, afterWaiting,
                $"{key}: a refused villager asked again: {afterWaiting - atArrival} more refusal warnings over " +
                $"{NoRetryFrames} frames");

            Census census = TakeCensus("cold Nine Mile Creek at noon, ON");
            yield return _stage.FrameOn(Centre(creek));
            Assert.AreEqual(atArrival, PresenterRefusals(),
                $"{key}: a refused villager asked again while the plate was framed");

            string verdict = refused.Count == creek.Count
                ? $"all {creek.Count} REFUSED: their whole sprites, {atArrival} refusal warnings from their presenters " +
                  $"(one each), none more over {NoRetryFrames} frames or the framing"
                : $"{refused.Count} of {creek.Count} refused ({Names(refused)}); the rest are their meshes (the pool had room)";
            Shoot(key, $"Nine Mile Creek {Hhmm(NoonHour)}, loaded cold, the game's camera parked between its villagers " +
                       $"at {Fmt(Centre(creek))}",
                  strict: creek, others: new List<IsoCharacterSprite>(), everyone: creek,
                  alsoInFrame: new List<Transform>(),
                  extra: $"Verdict: {verdict}\nWarnings from other callers (the player's DeckRiderMeshPresenter and any " +
                         $"other): {_refusalStacks.Count - atArrival}\n{census}");
        }

        /// <summary>
        /// ⭐ <b>PLATE 4: a villager inside a house with the player.</b> Rose keeps the post office counter from
        /// 8:30 to 12:30, inside the post office (the store counter, by contrast, is an out-of-doors station). At
        /// 10:00, with the player outside, she is sheltered: neither picture. The player is stood a step inside
        /// beside her. Once the routine's shelter check (unscaled, every 0.25 s) sees the player in there too,
        /// she is revealed as her mesh, and the building's shell gives way to its room.
        /// </summary>
        [UnityTest]
        public IEnumerator Plate_AVillagerInsideAHouse_WithThePlayer_StPetersMorning()
        {
            WharfNightStage.RequireAGraphicsDevice();
            const string key = "4-inside-the-post-office-stpeters-morning";
            yield return Arrive(StPetersScene, MorningHour);

            List<IsoCharacterSprite> villagers = Villagers();
            IsoCharacterSprite rose = Named(villagers, RoseName);
            RoutineStationEntry counter = Station(PostOfficeCounterStation);
            Assert.IsTrue(counter.Indoors, $"harness: '{PostOfficeCounterStation}' is not inside a building");
            BuildingInterior postOffice = counter.Interior;
            CharacterFigurePresenter presenter = Presenter(rose);
            Assert.IsNotNull(presenter, $"harness: {Who(rose)} was given no figure presenter");
            Assert.IsFalse(Shows(rose),
                $"harness: Rose is drawn at {Hhmm(MorningHour)} with the player outside the post office: {Describe(rose)}");
            Assert.AreEqual(CharacterFigurePresenter.Refusal.SpriteDisabled, presenter.WhyNot,
                $"sheltered, she draws neither picture: {Describe(rose)}");
            Assert.IsFalse(presenter.AshoreFigure != null && presenter.AshoreFigure.Visible,
                $"sheltered, and her figure is visible: {Describe(rose)}");

            yield return FindThePlayer();
            Vector2 her = rose.transform.position;
            Vector2 inside = AStepInsideBeside(postOffice, her);
            PutThePlayerAt(inside, BearingDegrees(inside, her));
            float since = Time.unscaledTime;
            while (Time.unscaledTime - since < ShelterWaitSeconds) yield return null;

            Assert.IsTrue(postOffice.IsInside,
                $"the player stands at {Fmt(inside)}, inside the post office's footprint, and it does not count her in");
            Assert.IsTrue(Shows(rose), $"the player is inside with her and she is still hidden: {Describe(rose)}");
            for (int i = 0; i < SwitchSettleFrames; i++) yield return null;
            AssertEveryVillagerIsHerMesh(villagers, key);
            Assert.IsTrue(IsMesh(rose), $"revealed, and not her mesh: {Describe(rose)}");

            Vector2 centre = Vector2.Lerp(inside, her, 0.5f);
            yield return _stage.FrameOn(centre);
            Assert.IsTrue(postOffice.IsInside, "the player left the post office while the plate was framed");
            Assert.IsTrue(IsMesh(rose), $"she stopped drawing as her mesh while the plate was framed: {Describe(rose)}");

            List<IsoCharacterSprite> others = villagers.Where(v => v != rose && Shows(v) && InFrame(v.transform)).ToList();
            Shoot(key, $"St Peters {Hhmm(MorningHour)}, inside the post office: Rose at the counter {Fmt(her)}, the player " +
                       $"a step inside beside her at {Fmt(inside)}; the game's camera parked on {Fmt(centre)}",
                  strict: new List<IsoCharacterSprite> { rose }, others: others, everyone: villagers,
                  alsoInFrame: new List<Transform> { _player.transform },
                  extra: $"The player: {DescribeThePlayer()}\nThe post office counts the player in: {postOffice.IsInside}");
        }

        /// <summary>
        /// ⭐ <b>PLATE 5: a villager in conversation with the player.</b> Basil at 13:00, wherever his day has him,
        /// with the player stood a step east of him and facing him. The press goes through the scene's own
        /// WorldInteractor, the one the interact key drives. He stops to talk (his blocks are interruptible) and
        /// turns to her, and the dialogue shows.
        /// </summary>
        [UnityTest]
        public IEnumerator Plate_AVillagerInConversation_WithThePlayer_StPetersAfternoon()
        {
            WharfNightStage.RequireAGraphicsDevice();
            const string key = "5-in-conversation-stpeters-afternoon";
            yield return Arrive(StPetersScene, AfternoonHour);

            List<IsoCharacterSprite> villagers = Villagers();
            IsoCharacterSprite basil = Named(villagers, BasilName);
            var routine = basil.GetComponent<VillagerRoutine>();
            Assert.IsNotNull(routine, $"harness: {Who(basil)} has no VillagerRoutine");
            Assert.IsTrue(Shows(basil), $"harness: Basil is not out of doors at {Hhmm(AfternoonHour)}: {Describe(basil)}");
            var interactor = Object.FindFirstObjectByType<WorldInteractor>();
            Assert.IsNotNull(interactor, "harness: St Peters has no WorldInteractor, so nobody can be talked to");
            var dialogue = Object.FindFirstObjectByType<DialoguePresenter>();
            Assert.IsNotNull(dialogue, "harness: St Peters has no DialoguePresenter, so no conversation can show");

            yield return FindThePlayer();
            Vector2 him = basil.transform.position;
            Vector2 at = him + new Vector2(TalkStandOffMetres, 0f);
            PutThePlayerAt(at, WestDegrees);
            _player.Face(Facing.Left);
            for (int i = 0; i < 3; i++) yield return null;

            Assert.IsTrue(interactor.BeginInteract(),
                $"Basil is {TalkStandOffMetres:0.0} m west of the player, who faces him, and the interact press found " +
                $"nobody: {Describe(basil)}");
            for (int i = 0; i < TalkSettleFrames; i++) yield return null;
            Assert.IsTrue(dialogue.IsShowing, "the conversation began and no dialogue is showing");
            Assert.IsTrue(routine.IsTalking, $"Basil did not stop to talk: {Describe(basil)}");
            AssertEveryVillagerIsHerMesh(villagers, key);
            Assert.IsTrue(IsMesh(basil), $"in conversation, and not his mesh: {Describe(basil)}");

            Vector2 centre = Vector2.Lerp(at, basil.transform.position, 0.5f);
            yield return _stage.FrameOn(centre);
            Assert.IsTrue(dialogue.IsShowing, "the conversation ended while the plate was framed");
            Assert.IsTrue(routine.IsTalking, "Basil walked off while the plate was framed");

            List<IsoCharacterSprite> others = villagers.Where(v => v != basil && Shows(v) && InFrame(v.transform)).ToList();
            Shoot(key, $"St Peters {Hhmm(AfternoonHour)}: Basil at {Fmt(basil.transform.position)} in conversation, the player " +
                       $"{TalkStandOffMetres:0.0} m east of where he stood, facing him; the game's camera parked on {Fmt(centre)}",
                  strict: new List<IsoCharacterSprite> { basil }, others: others, everyone: villagers,
                  alsoInFrame: new List<Transform> { _player.transform },
                  extra: $"The player: {DescribeThePlayer()}\nDialogue showing: {dialogue.IsShowing}; Basil talking: {routine.IsTalking}");
        }

        /// <summary>
        /// ⭐ <b>PLATE 6: two villagers of one skin idling side by side, not in step.</b> No two St Peters villagers
        /// share a skin, so the fixture places the pair. Basil, and the first villager whose key puts the idle off
        /// Basil's phase at this world seed (Junior first), re-dressed in Basil's own art def. Both routines are
        /// switched off, both stand 1.4 m apart on the green facing south, and both speeds are held at zero, so
        /// both idle. Each asks for its frame from (worldSeed, gameTime) and its own key, and the two differ.
        /// </summary>
        [UnityTest]
        public IEnumerator Plate_TwoOfOneSkinIdlingSideBySide_NotInStep_StPetersNoon()
        {
            WharfNightStage.RequireAGraphicsDevice();
            const string key = "6-one-skin-side-by-side-stpeters-noon";
            yield return Arrive(StPetersScene, NoonHour);

            List<IsoCharacterSprite> villagers = Villagers();
            IsoCharacterSprite basil = Named(villagers, BasilName);
            CharacterSkinDef skin = basil.Visual != null ? basil.Visual.Skin : null;
            Assert.IsNotNull(skin, $"harness: {Who(basil)} links no skin");
            Assert.IsTrue(skin.TryGetClip(CharacterSkinStateMap.Idle, out CharacterSkinDef.SkinClip idle),
                $"harness: '{skin.Id}' has no '{CharacterSkinStateMap.Idle}' clip");
            Assert.IsTrue(idle.Loop && idle.FrameCount > 1,
                $"harness: '{skin.Id}' idles on {idle.FrameCount} frame(s), looping {idle.Loop}: there is no phase to be out of");
            Assert.IsNotNull(GameServices.Environment, "harness: the island published no environment, so there is no world seed");
            int seed = GameServices.Environment.WorldSeed;
            int basilPhase = IdlePhase(basil, idle, seed);
            IsoCharacterSprite partner = villagers
                .Where(v => v != basil)
                .OrderBy(v => v.name == JuniorName ? 0 : 1).ThenBy(v => v.name, StringComparer.Ordinal)
                .FirstOrDefault(v => IdlePhase(v, idle, seed) != basilPhase);
            Assert.IsNotNull(partner,
                $"no villager's key puts the idle off Basil's phase {basilPhase} at world seed {seed}: " +
                string.Join(", ", villagers.Select(v => $"{v.name} {IdlePhase(v, idle, seed)}")));
            string partnersOwnDef = partner.Visual != null ? partner.Visual.name : "none";

            Assert.IsTrue(TheStations().TryFind(GreenStation, out RoutineStationEntry green),
                $"harness: St Peters has no station '{GreenStation}'");
            Assert.IsFalse(green.Indoors, $"harness: '{GreenStation}' is indoors");

            BringHerOut(basil);
            BringHerOut(partner);
            partner.Configure(basil.Visual);
            StandStill(basil, green.Position + new Vector2(-SideBySideMetres * 0.5f, 0f), SouthDegrees);
            StandStill(partner, green.Position + new Vector2(SideBySideMetres * 0.5f, 0f), SouthDegrees);
            float since = Time.unscaledTime;
            while (Time.unscaledTime - since < StandSettleSeconds) yield return null;

            yield return _stage.FrameOn(green.Position);

            var pair = new List<IsoCharacterSprite> { basil, partner };
            var problems = pair.SelectMany(MeshProblemsOf).Concat(pair.SelectMany(FrameProblemsOf)).ToList();
            Assert.IsEmpty(problems, $"{key}: the pair is not two meshes:\n  " + string.Join("\n  ", problems));
            Assert.AreSame(skin, partner.Visual.Skin, $"{Who(partner)} does not wear Basil's skin '{skin.Id}'");
            IsoCharacterFigureRenderer basilFigure = Presenter(basil).AshoreFigure;
            IsoCharacterFigureRenderer partnerFigure = Presenter(partner).AshoreFigure;
            Assert.AreEqual(CharacterSkinStateMap.Idle, basilFigure.DrawnStateKey, $"Basil is not idling: {Describe(basil)}");
            Assert.AreEqual(CharacterSkinStateMap.Idle, partnerFigure.DrawnStateKey, $"{partner.name} is not idling: {Describe(partner)}");
            Assert.AreNotEqual(basilFigure.DrawnFrame, partnerFigure.DrawnFrame,
                $"two of one skin idle in step: Basil and {partner.name} both show idle frame {basilFigure.DrawnFrame} " +
                $"(asked {basilFigure.RequestedFrame} and {partnerFigure.RequestedFrame})");

            Shoot(key, $"St Peters {Hhmm(NoonHour)}, the green ({GreenStation} at {Fmt(green.Position)}): Basil and " +
                       $"{partner.name} placed {SideBySideMetres:0.0} m apart facing south, routines off, speeds held at zero; " +
                       "the game's camera parked on the station",
                  strict: pair, others: new List<IsoCharacterSprite>(), everyone: pair,
                  alsoInFrame: new List<Transform>(),
                  extra: $"Placed by the fixture: no two St Peters villagers share a skin. {partner.name} is re-dressed in " +
                         $"Basil's own art def '{basil.Visual.name}' (his own is '{partnersOwnDef}'), so both wear '{skin.Id}'.\n" +
                         $"World seed {seed}; idle phases Basil {basilPhase}, {partner.name} {IdlePhase(partner, idle, seed)} " +
                         $"of {idle.FrameCount}; drawn frames {basilFigure.DrawnFrame} and {partnerFigure.DrawnFrame}.");
        }

        /// <summary>
        /// ⭐ <b>PLATE 7: the switch OFF against ON on the same frame.</b> The frame holds the most villagers out of
        /// doors, measured on the plate camera. ON is shot twice back to back, then the switch goes OFF on a second
        /// runtime copy, and the same frame is shot again: every villager is her sprite, as on 8ae9c5af. Each side
        /// has its own subject-removed control.
        /// </summary>
        [UnityTest]
        public IEnumerator Plate_TheSwitchOffAgainstOn_OnTheSameFrame_StPetersNoon()
        {
            WharfNightStage.RequireAGraphicsDevice();
            const string key = "7-switch-off-against-on-stpeters-noon";
            yield return Arrive(StPetersScene, NoonHour);

            List<IsoCharacterSprite> villagers = Villagers();
            HoldTheWalkers(villagers);
            yield return _stage.FrameOn(Station(SlipHeadStation).Position);
            Vector2 centre = FrameTheMost(villagers, out List<IsoCharacterSprite> held);
            Assert.Greater(held.Count, 0, $"harness: nobody is out of doors on St Peters at {Hhmm(NoonHour)}");
            yield return ReframeOn(centre);

            AssertEveryVillagerIsHerMesh(villagers, key + " ON");
            List<IsoCharacterSprite> inFrame = villagers.Where(v => Shows(v) && InFrame(v.transform)).ToList();
            Assert.Greater(inFrame.Count, 0, $"{key}: no villager out of doors is in the frame");
            byte[] onA = _stage.Capture();
            byte[] onB = _stage.Capture();
            foreach (IsoCharacterSprite v in inFrame) AssertTheSubjectIsInFrame(v.transform, $"{key} ON: {v.name}");
            byte[] onControl = CaptureWithout(inFrame);
            int onFrame = Time.frameCount;
            List<string> onLines = villagers.Select(Describe).ToList();
            string onConfig = ConfigLine();

            UseSwitches(true, false);
            for (int i = 0; i < SwitchSettleFrames; i++) yield return null;
            var spriteProblems = villagers.SelectMany(SpriteProblemsOf).ToList();
            Assert.IsEmpty(spriteProblems, $"{key} OFF: not every villager is her sprite exactly as before:\n  " +
                                           string.Join("\n  ", spriteProblems));
            byte[] off = _stage.Capture();
            foreach (IsoCharacterSprite v in inFrame) AssertTheSubjectIsInFrame(v.transform, $"{key} OFF: {v.name}");
            byte[] offControl = CaptureWithout(inFrame);
            int offFrame = Time.frameCount;
            List<string> offLines = villagers.Select(Describe).ToList();

            int w = _stage.Width, h = _stage.Height;
            var pixels = new List<string>();
            int differing = 0;
            foreach (IsoCharacterSprite v in inFrame)
            {
                RectInt box = BoxOf(v.transform.position);
                int noise = ChangedIn(onA, onB, box, w, h);
                int switched = ChangedIn(onA, off, box, w, h);
                int meshGone = ChangedIn(onA, onControl, box, w, h);
                int spriteGone = ChangedIn(off, offControl, box, w, h);
                bool differs = switched >= MinSubjectPixels && switched > noise;
                if (differs) differing++;
                pixels.Add($"{v.name}: ON against OFF {switched} px of her box {box}; her mesh removed {meshGone} px; her " +
                           $"sprite removed {spriteGone} px; back-to-back noise {noise} px{(differs ? "" : " (NO visible difference)")}");
            }

            string hashOnA = Hash(onA), hashOnB = Hash(onB), hashOff = Hash(off);
            string hashOnControl = Hash(onControl), hashOffControl = Hash(offControl);
            _stage.SavePlate(key + "-on.png", onA);
            _stage.SavePlate(key + "-on-control.png", onControl);
            _stage.SavePlate(key + "-off.png", off);
            _stage.SavePlate(key + "-off-control.png", offControl);
            WriteText(key + ".txt",
                $"{key}\nFrame: St Peters {Hhmm(NoonHour)}, the game's camera parked on {Fmt(centre)}, holding the most " +
                $"villagers out of doors ({Names(held)}){HeldWalkersNote(held)}\n{Stamp()}\n" +
                $"ON (shot at engine frame {onFrame}, the plate and its control in that one frame): {onConfig}\n" +
                $"  - {string.Join("\n  - ", onLines)}\n" +
                $"OFF (shot at engine frame {offFrame}, {offFrame - onFrame} frames later, the world frozen between): " +
                $"{ConfigLine()}\n  - {string.Join("\n  - ", offLines)}\n" +
                $"Pixels (threshold {PixelThreshold} over three channels; her box {BoxHalfWidthMetres} m each side of her " +
                $"feet, {BoxBelowMetres} m below to {BoxAboveMetres} m above):\n  - {string.Join("\n  - ", pixels)}\n" +
                $"Hashes: ON-a {hashOnA}, ON-b {hashOnB} ({(hashOnA == hashOnB ? "identical" : $"differ in {ChangedIn(onA, onB, new RectInt(0, 0, w, h), w, h)} px")}); " +
                $"OFF {hashOff}; ON control {hashOnControl}; OFF control {hashOffControl}\n" +
                $"Files: {key}-on.png, {key}-on-control.png, {key}-off.png, {key}-off-control.png\n");

            Assert.AreNotEqual(hashOnA, hashOff, $"{key}: ON and OFF hash the same, so the switch changed nothing in the picture");
            Assert.AreNotEqual(hashOnA, hashOnControl, $"{key}: removing the meshes changed nothing, so no mesh was drawn");
            Assert.AreNotEqual(hashOff, hashOffControl, $"{key}: removing the sprites changed nothing, so no sprite was drawn");
            Assert.Greater(differing, 0, $"{key}: no villager in frame looks any different ON and OFF:\n  " +
                                         string.Join("\n  ", pixels));
        }

        // =============================================================================================
        //  The numbers (for the report)
        // =============================================================================================

        /// <summary><b>NUMBERS: the facet census at St Peters noon, ON then OFF.</b> ON, nobody is refused and
        /// everyone out of doors holds her id. OFF, the figure ids given back are exactly the villagers'.</summary>
        [UnityTest]
        public IEnumerator Numbers_TheFacetCensus_StPetersNoon_OnThenOff()
        {
            WharfNightStage.RequireAGraphicsDevice();
            const string key = "numbers-census-stpeters-noon";
            yield return Arrive(StPetersScene, NoonHour);
            List<IsoCharacterSprite> villagers = Villagers();
            Census on = TakeCensus("St Peters noon, ON");
            List<string> ungranted = villagers.Where(v => Shows(v) && !HoldsAnId(v)).Select(Describe).ToList();

            UseSwitches(true, false);
            for (int i = 0; i < SwitchSettleFrames; i++) yield return null;
            Census off = TakeCensus("St Peters noon, OFF");
            WriteNumbers(key, on, off);

            Assert.AreEqual(0, on.Refused, $"ON, a villager was refused at St Peters:\n{on}");
            Assert.IsEmpty(ungranted, "ON, a villager out of doors holds no facet id:\n  " + string.Join("\n  ", ungranted));
            Assert.AreEqual(0, off.VillagerIds, $"OFF, a villager still holds a facet id:\n{off}");
            Assert.AreEqual(on.VillagerIds, on.RegistryFigures - off.RegistryFigures,
                $"the switch gave back {on.RegistryFigures - off.RegistryFigures} figure ids, and the villagers held {on.VillagerIds}");
        }

        /// <summary><b>NUMBERS: the facet census at a cold Nine Mile Creek, ON then OFF.</b></summary>
        [UnityTest]
        public IEnumerator Numbers_TheFacetCensus_ColdNineMileCreekNoon_OnThenOff()
        {
            WharfNightStage.RequireAGraphicsDevice();
            const string key = "numbers-census-creek-cold-noon";
            yield return Arrive(NineMileCreekScene, NoonHour);
            List<IsoCharacterSprite> creek = CreekVillagers();
            Census on = TakeCensus("cold Nine Mile Creek noon, ON");
            List<string> undecided = creek.Where(v => Presenter(v) != null && !HoldsAnId(v) && !Presenter(v).AshoreRefused)
                                          .Select(Describe).ToList();

            UseSwitches(true, false);
            for (int i = 0; i < SwitchSettleFrames; i++) yield return null;
            Census off = TakeCensus("cold Nine Mile Creek noon, OFF");
            WriteNumbers(key, on, off);

            Assert.IsEmpty(undecided, "ON, a creek villager was neither granted nor refused:\n  " + string.Join("\n  ", undecided));
            Assert.AreEqual(0, off.VillagerIds, $"OFF, a villager still holds a facet id:\n{off}");
            Assert.AreEqual(0, off.Refused, $"OFF, a villager is still held as refused:\n{off}");
        }

        /// <summary><b>NUMBERS: the creek arrived from St Peters, ON, then OFF.</b> The crossing is the game's own
        /// RegionSceneLoader. Regions are never unloaded, so St Peters' hulls and villagers are counted too, unless
        /// the travel coordinator switched them off. The creek's villagers are held to plate 3's claims by name:
        /// each one either refused honestly (her whole sprite, one warning, no ask again) or her mesh drawn
        /// right. Not photographed: after the crossing the creek's own camera tagged MainCamera stands beside the
        /// game's, so the stage's framing could shoot through the wrong one.</summary>
        [UnityTest]
        public IEnumerator Numbers_TheFacetCensus_NineMileCreekArrivedFromStPeters_OnThenOff()
        {
            WharfNightStage.RequireAGraphicsDevice();
            const string key = "numbers-census-creek-arrived-on";
            StartListening();
            yield return Arrive(StPetersScene, NoonHour);
            Census before = TakeCensus("St Peters noon before the crossing, ON");
            int warnedBefore = PresenterRefusals();
            yield return TravelToTheCreek();
            yield return PinTheHour(NoonHour);

            List<IsoCharacterSprite> creek = CreekVillagers();
            int atArrival = PresenterRefusals() - warnedBefore;
            for (int i = 0; i < NoRetryFrames; i++) yield return null;
            int afterWaiting = PresenterRefusals() - warnedBefore;
            Census on = TakeCensus("Nine Mile Creek arrived from St Peters, ON");
            List<IsoCharacterSprite> refused = creek.Where(v => Presenter(v) != null && Presenter(v).AshoreRefused).ToList();
            List<IsoCharacterSprite> meshes = creek.Where(IsMesh).ToList();
            var problems = new List<string>();
            foreach (IsoCharacterSprite v in creek)
                problems.AddRange(refused.Contains(v) ? RefusalProblemsOf(v) : MeshProblemsOf(v).Concat(FrameProblemsOf(v)));
            List<string> creekLines = creek.Select(Describe).ToList();

            UseSwitches(true, false);
            for (int i = 0; i < SwitchSettleFrames; i++) yield return null;
            Census off = TakeCensus("Nine Mile Creek arrived from St Peters, then OFF");
            WriteNumbers(key,
                $"The creek's villagers, arrived from St Peters with both switches on: {meshes.Count} drawing as their " +
                $"meshes ({Names(meshes)}), {refused.Count} refused honestly ({Names(refused)}); refusal warnings from " +
                $"their presenters after the crossing: {atArrival} at arrival, {afterWaiting} after {NoRetryFrames} " +
                $"frames\n  - {string.Join("\n  - ", creekLines)}",
                before, on, off);

            Assert.IsEmpty(problems, $"{key}: a villager at the creek is neither refused honestly nor her mesh:\n  " +
                                     string.Join("\n  ", problems));
            Assert.AreEqual(refused.Count, atArrival,
                $"{key}: {refused.Count} villagers are refused and their presenters logged {atArrival} refusal " +
                "warnings after the crossing, where one ask each gives one warning each");
            Assert.AreEqual(atArrival, afterWaiting,
                $"{key}: a refused villager asked again: {afterWaiting - atArrival} more refusal warnings over " +
                $"{NoRetryFrames} frames");
            Assert.AreEqual(0, off.VillagerIds, $"OFF, a villager still holds a facet id:\n{off}");
        }

        /// <summary><b>NUMBERS: the creek arrived from St Peters with the switch OFF throughout.</b></summary>
        [UnityTest]
        public IEnumerator Numbers_TheFacetCensus_NineMileCreekArrivedFromStPeters_OffThroughout()
        {
            WharfNightStage.RequireAGraphicsDevice();
            const string key = "numbers-census-creek-arrived-off";
            yield return Arrive(StPetersScene, NoonHour);
            UseSwitches(true, false);
            for (int i = 0; i < SwitchSettleFrames; i++) yield return null;
            Census before = TakeCensus("St Peters noon before the crossing, OFF");
            yield return TravelToTheCreek();
            yield return PinTheHour(NoonHour);
            Census off = TakeCensus("Nine Mile Creek arrived from St Peters, OFF throughout");
            WriteNumbers(key, before, off);

            Assert.AreEqual(0, off.VillagerIds, $"OFF, a villager holds a facet id:\n{off}");
            Assert.AreEqual(0, off.Refused, $"OFF, a villager is held as refused:\n{off}");
        }

        /// <summary>
        /// <b>NUMBERS: frame time and draw calls at St Peters noon, ON against OFF.</b> Plate 7's frame, frozen, in
        /// <see cref="RenderRounds"/> interleaved rounds (ON, OFF, ON, OFF), so the report shows the noise beside
        /// the difference. Each side is read through the engine's own render counters (every one this editor offers
        /// in the Render category is listed; those for draw calls, batches, SetPass calls, triangles and vertices
        /// are recorded, each started in its own category), the editor's own frame stats and the main-thread time,
        /// plus the median of <see cref="RenderSamples"/> timed <c>Camera.Render()</c> calls. Each mesh villager's
        /// figure is listed with its renderers and materials (one overlay material and one quad each, by design).
        /// </summary>
        [UnityTest]
        public IEnumerator Numbers_FrameTimeAndDrawCalls_StPetersNoon_OnAgainstOff()
        {
            WharfNightStage.RequireAGraphicsDevice();
            const string key = "numbers-render-stpeters-noon";
            yield return Arrive(StPetersScene, NoonHour);
            List<IsoCharacterSprite> villagers = Villagers();
            HoldTheWalkers(villagers);
            yield return _stage.FrameOn(Station(SlipHeadStation).Position);
            Vector2 centre = FrameTheMost(villagers, out List<IsoCharacterSprite> held);
            yield return ReframeOn(centre);

            List<ProfilerRecorderDescription> counters = RenderCounters(out List<string> offered, out List<string> missing);
            List<string> figures = villagers.Where(IsMesh).Select(FigureCost).ToList();
            var rounds = new List<RenderNumbers>();
            for (int round = 1; round <= RenderRounds; round++)
            {
                foreach (bool ashore in new[] { true, false })
                {
                    UseSwitches(true, ashore);
                    for (int i = 0; i < SwitchSettleFrames; i++) yield return null;
                    var n = new RenderNumbers
                    {
                        Label = $"{(ashore ? "ON" : "OFF")} #{round}",
                        Ashore = ashore,
                        MeshVillagers = villagers.Count(IsMesh),
                        MeshVillagersInFrame = villagers.Count(v => IsMesh(v) && InFrame(v.transform)),
                    };
                    yield return MeasureRender(n, counters);
                    rounds.Add(n);
                }
            }
            Census offCensus = TakeCensus("St Peters noon, OFF (render case, last round)");

            var body = new StringBuilder();
            body.Append($"{key}\nFrame: St Peters {Hhmm(NoonHour)}, the game's camera parked on {Fmt(centre)}, holding " +
                        $"{Names(held)}{HeldWalkersNote(held)}\n{Stamp()}\n");
            body.Append($"Render counters this editor offers ({offered.Count}): {string.Join(", ", offered)}\n");
            body.Append($"Recorded: {string.Join(", ", counters.Select(d => $"'{d.Name}' ({d.Category.Name})"))}; " +
                        $"not offered: {(missing.Count > 0 ? string.Join(", ", missing) : "none")}\n");
            foreach (RenderNumbers r in rounds) body.Append(r).Append('\n');
            for (int i = 0; i + 1 < rounds.Count; i += 2)
                body.Append($"ON - OFF, round {i / 2 + 1}: {DifferenceOf(rounds[i], rounds[i + 1])}\n");
            for (int i = 2; i < rounds.Count; i++)
                body.Append($"Noise, {rounds[i].Label} - {rounds[i - 2].Label}: {DifferenceOf(rounds[i], rounds[i - 2])}\n");
            body.Append($"Figures ON ({figures.Count}):\n  - {string.Join("\n  - ", figures)}\n");
            WriteText(key + ".txt", body.ToString());
            Debug.Log($"[{PlateDir}] NUMBERS {body}");

            List<RenderNumbers> on = rounds.Where(r => r.Ashore).ToList();
            List<RenderNumbers> off = rounds.Where(r => !r.Ashore).ToList();
            Assert.IsTrue(on.All(r => r.MeshVillagers > 0 && r.MeshVillagers == on[0].MeshVillagers),
                "ON, the villagers drawing as meshes differ between rounds: " + string.Join(", ", on.Select(r => $"{r.Label} {r.MeshVillagers}")));
            Assert.IsTrue(off.All(r => r.MeshVillagers == 0),
                "OFF, a villager still draws as her mesh: " + string.Join(", ", off.Select(r => $"{r.Label} {r.MeshVillagers}")));
            Assert.AreEqual(0, offCensus.VillagerIds, $"OFF, a villager still holds a facet id:\n{offCensus}");
        }

        // =============================================================================================
        //  Arriving, the hour and the switches
        // =============================================================================================

        private IEnumerator Arrive(string scene, float hour)
        {
            if (!_loadedAny) _introOnEntry = GameServices.OpeningCinematicRunning;
            _loadedAny = true;
            _hour = hour;
            _stage = new WharfNightStage(scene, PlateDir);
            yield return _stage.Load();

            if (ShellFlow.WorldInputBlocked)
            {
                Debug.Log($"[{PlateDir}] arrived at the shell's title page (phase {ShellFlow.Phase}); ShellFlow.Reset(), " +
                          "not StartNewGame(), which would write the shared save.");
                ShellFlow.Reset();
            }
            InteractionGate.Reset();

            _loadedConfig = GameServices.Config;
            Assert.IsNotNull(_loadedConfig, $"harness: {scene} published no GameConfig, so there is no switch to set");
            UseSwitches(true, true);
            yield return PinTheHour(hour);
        }

        /// <summary>The switches, on a runtime copy of the loaded config. The asset is never written.</summary>
        private void UseSwitches(bool meshCast, bool ashore)
        {
            GameConfig source = _loadedConfig != null ? _loadedConfig : GameServices.Config;
            Assert.IsNotNull(source, "harness: no GameConfig is published, so there is no switch to set");
            GameConfig copy = Object.Instantiate(source);
            copy.name = $"(plate: MeshCast {meshCast}, MeshCastAshore {ashore})";
            copy.MeshCast = meshCast;
            copy.MeshCastAshore = ashore;
            _spawned.Add(copy);
            GameServices.Config = copy;
        }

        /// <summary>
        /// Stop the clock at <paramref name="hours"/> on day 1, then let the light and the village catch up. The
        /// clock stops on its own time scale while engine time runs. The day/night controller then catches the
        /// light, each routine samples its pose from the stopped clock, and a villager's measured speed (smoothed
        /// on scaled time) settles after the jump onto her pose. The camera is framed and the world frozen later,
        /// by <see cref="WharfNightStage.FrameOn"/>.
        /// </summary>
        private IEnumerator PinTheHour(float hours)
        {
            RepublishTheRegionsClock();
            IGameClock clock = GameServices.Clock;
            GameConfig config = GameServices.Config;
            Assert.IsNotNull(clock, "harness: the region registered no clock, so the hour cannot be pinned");
            Assert.IsNotNull(config, "harness: the region registered no GameConfig, so a day has no length");

            double t = (1.0 + hours / 24.0) * config.SecondsPerDay;
            Time.timeScale = 1f;
            clock.SeekTo(t);
            clock.TimeScale = 0f;
            Assert.LessOrEqual(Math.Abs(clock.TotalSeconds - t), 1.0,
                $"the clock did not land on {t:0.0} s (it reads {clock.TotalSeconds:0.0} s)");

            Color tint = Shader.GetGlobalColor("_DayNightTint");
            float waited = 0f;
            int still = 0, frames = 0;
            while (frames < MaxSettleFrames)
            {
                yield return null;
                frames++;
                waited += Time.unscaledDeltaTime;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f ? still + 1 : 0;
                tint = now;
                if (waited >= SettleSeconds && still >= 4) break;
            }
            Assert.Less(frames, MaxSettleFrames, $"the day/night tint never settled at {Hhmm(hours)}");
            Debug.Log($"[{PlateDir}] asked for {Hhmm(hours)}; clock reads {clock.HourOfDay:0.00} h on day {clock.DayIndex}; " +
                      $"settled after {frames} frames ({waited:0.00} s)");
        }

        /// <summary>A second region can arrive with the first one's destroyed clock published (see
        /// <see cref="WharfNightStage.SetNight"/>). Publish the region's own instead.</summary>
        private void RepublishTheRegionsClock()
        {
            if (!IsGone(GameServices.Clock)) return;
            var live = Object.FindFirstObjectByType<HiddenHarbours.Environment.GameClock>();
            if (live == null) return;
            GameServices.Clock = live;
            Healed("the region's own GameClock republished over a destroyed one");
        }

        /// <summary>
        /// Cross to the creek the way play does: St Peters' RegionSceneLoader loads it additively and makes it the
        /// active scene. Arriving activates the creek's dev core, which republishes the config ASSET over the
        /// switch copy and, when destroyed, nulls Config, Clock, Environment and Wallet
        /// (<c>MoodGradeTriptychPlatePlayTests</c>). The live services and the switch copy are put back, and the
        /// numbers say so.
        /// </summary>
        private IEnumerator TravelToTheCreek()
        {
            var loader = Object.FindFirstObjectByType<RegionSceneLoader>();
            Assert.IsNotNull(loader, "harness: St Peters has no RegionSceneLoader, so the creek cannot be reached as play reaches it");
            GameConfig switches = GameServices.Config;
            IGameClock clockBefore = GameServices.Clock;
            IEnvironmentService envBefore = GameServices.Environment;
            IWallet walletBefore = GameServices.Wallet;

            loader.TravelTo(NineMileCreekRegionId);
            int frames = 0;
            while (frames < MaxTravelFrames && SceneManager.GetActiveScene().name != NineMileCreekScene)
            {
                yield return null;
                frames++;
            }
            Assert.AreEqual(NineMileCreekScene, SceneManager.GetActiveScene().name,
                $"harness: {frames} frames after the crossing began, the creek is not the active scene");
            for (int i = 0; i < 8; i++) yield return null;

            if (!ReferenceEquals(GameServices.Config, switches))
            {
                GameServices.Config = switches;
                Healed("Config (the switch copy) republished after the creek's dev core replaced it");
            }
            if (IsGone(GameServices.Clock) && !IsGone(clockBefore)) { GameServices.Clock = clockBefore; Healed("Clock"); }
            if (IsGone(GameServices.Environment) && !IsGone(envBefore)) { GameServices.Environment = envBefore; Healed("Environment"); }
            if (IsGone(GameServices.Wallet) && !IsGone(walletBefore)) { GameServices.Wallet = walletBefore; Healed("Wallet"); }
            Debug.Log($"[{PlateDir}] crossed to the creek in {frames} frames; region '{GameServices.CurrentRegionId}'; healed: {_healed}");
        }

        private void Healed(string what)
        {
            _healed = _healed == "none" ? what : _healed + "; " + what;
            Debug.Log($"[{PlateDir}] healed: {what}");
        }

        /// <summary>A service reference that is null, or a destroyed Unity object behind an interface.</summary>
        private static bool IsGone(object service) => service == null || (service is Object o && o == null);

        // =============================================================================================
        //  The villagers
        // =============================================================================================

        private static List<IsoCharacterSprite> Villagers()
        {
            var routines = Object.FindObjectsByType<VillagerRoutine>(FindObjectsInactive.Include);
            var villagers = new List<IsoCharacterSprite>();
            foreach (VillagerRoutine routine in routines.OrderBy(r => r.name, StringComparer.Ordinal))
            {
                var iso = routine.GetComponent<IsoCharacterSprite>();
                Assert.IsNotNull(iso, $"harness: villager '{routine.name}' has no IsoCharacterSprite beside their routine");
                villagers.Add(iso);
            }
            Assert.GreaterOrEqual(villagers.Count, VillagersOnTheIsland,
                $"harness: St Peters stands {villagers.Count} villagers ([{Names(villagers)}])");
            return villagers;
        }

        private static List<IsoCharacterSprite> CreekVillagers()
        {
            List<IsoCharacterSprite> creek = Object.FindObjectsByType<NpcFigureStand>(FindObjectsInactive.Include)
                .Where(s => s.gameObject.scene.name == NineMileCreekScene)
                .OrderBy(s => s.name, StringComparer.Ordinal)
                .Select(s => s.FigureCharacter)
                .Where(c => c != null)
                .ToList();
            Assert.GreaterOrEqual(creek.Count, VillagersAtTheCreek,
                $"harness: Nine Mile Creek stands {creek.Count} villagers with a body ([{Names(creek)}]); the saved scene " +
                "holds Wendell and Hector");
            return creek;
        }

        private static IsoCharacterSprite Named(List<IsoCharacterSprite> villagers, string name)
        {
            IsoCharacterSprite v = villagers.SingleOrDefault(x => x.name == name);
            Assert.IsNotNull(v, $"harness: St Peters has no villager named '{name}' ([{Names(villagers)}])");
            return v;
        }

        private static RoutineStations TheStations()
        {
            var stations = Object.FindFirstObjectByType<RoutineStations>();
            Assert.IsNotNull(stations, "harness: St Peters has no RoutineStations");
            return stations;
        }

        private static CharacterFigurePresenter Presenter(IsoCharacterSprite v) => v.GetComponent<CharacterFigurePresenter>();

        private static bool Shows(IsoCharacterSprite v)
        {
            var sprite = v.GetComponent<SpriteRenderer>();
            return sprite != null && sprite.enabled && v.gameObject.activeInHierarchy;
        }

        private static bool IsMesh(IsoCharacterSprite v)
        {
            CharacterFigurePresenter p = Presenter(v);
            return p != null && p.WhyNot == CharacterFigurePresenter.Refusal.None && p.AshoreFigure != null &&
                   p.AshoreFigure.IsAshore && p.AshoreFigure.Visible;
        }

        private static bool HoldsAnId(IsoCharacterSprite v)
        {
            CharacterFigurePresenter p = Presenter(v);
            return p != null && p.AshoreFigure != null && p.AshoreFigure.IsAshore;
        }

        private static string Who(IsoCharacterSprite v) =>
            $"'{v.name}' ({(v.Visual != null ? v.Visual.name : "no def")})";

        /// <summary>Out on her own feet whatever the hour: her routine off, whose OnDisable hands her renderer back
        /// visible.</summary>
        private static void BringHerOut(IsoCharacterSprite v)
        {
            var routine = v.GetComponent<VillagerRoutine>();
            Assert.IsNotNull(routine, $"harness: '{v.name}' has no VillagerRoutine");
            routine.enabled = false;
            Assert.IsTrue(Shows(v), $"harness: '{v.name}' is still hidden with her routine switched off");
        }

        /// <summary>Stood at a spot facing a way, with her speed held at zero so she idles whatever the move read as.</summary>
        private void StandStill(IsoCharacterSprite v, Vector2 at, float headingDegrees)
        {
            if (v.TryGetComponent(out Rigidbody2D body)) body.position = at;
            Vector3 was = v.transform.position;
            v.transform.position = new Vector3(at.x, at.y, was.z);
            v.HoldHeading(headingDegrees);
            v.HoldSpeed(0f);
            _heldHeadings.Add(v);
            _heldSpeeds.Add(v);
        }

        /// <summary>The phase her idle loop is drawn at: her key's seed through the clip's own offset.</summary>
        private static int IdlePhase(IsoCharacterSprite v, CharacterSkinDef.SkinClip idle, int seed)
        {
            string key = v.TryGetComponent(out NpcFigureStand stand) ? stand.FigureKey : null;
            int phaseSeed = CharacterFigurePresenter.AshorePhaseSeed(seed, CharacterFigurePresenter.KeyHash(key));
            return CharacterSkinPose.PhaseFrame(phaseSeed, idle.StateKey, idle.FrameCount);
        }

        /// <summary>The frame (worldSeed, gameTime) and her key ask for, or -1 when she draws no looping clip.</summary>
        private static int ExpectedFrame(IsoCharacterSprite v)
        {
            CharacterFigurePresenter p = Presenter(v);
            IsoCharacterFigureRenderer f = p != null ? p.AshoreFigure : null;
            CharacterSkinDef skin = v.Visual != null ? v.Visual.Skin : null;
            if (f == null || skin == null || string.IsNullOrEmpty(f.DrawnStateKey)) return -1;
            if (!skin.TryGetClip(f.DrawnStateKey, out CharacterSkinDef.SkinClip clip) || !clip.Loop) return -1;
            string key = v.TryGetComponent(out NpcFigureStand stand) ? stand.FigureKey : null;
            int seed = GameServices.Environment != null ? GameServices.Environment.WorldSeed : 0;
            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0d;
            return CharacterSkinPose.FrameFor(clip, CharacterFigurePresenter.AshorePhaseSeed(seed, CharacterFigurePresenter.KeyHash(key)),
                                              now, 0d);
        }

        // =============================================================================================
        //  The player
        // =============================================================================================

        /// <summary>The region's own on-foot player: the published one, else the first with a body. Found, never
        /// built.</summary>
        private IEnumerator FindThePlayer()
        {
            for (int f = 0; f < 240 && _player == null; f++)
            {
                Transform published = GameServices.PlayerTransform;
                PlayerWalkController walker = published != null ? published.GetComponent<PlayerWalkController>() : null;
                if (walker == null)
                    walker = Object.FindObjectsByType<PlayerWalkController>()
                                   .FirstOrDefault(w => w.GetComponent<IsoCharacterSprite>() != null);
                if (walker != null)
                {
                    _player = walker;
                    break;
                }
                yield return null;
            }
            Assert.IsNotNull(_player, "harness: the region has no on-foot player after 240 frames");
            Assert.IsNull(_player.GetComponentInParent<IsoFacetHullRenderer>(), "harness: the player is aboard a hull");
            _playerIso = _player.GetComponent<IsoCharacterSprite>();
            _playerBody = _player.GetComponent<Rigidbody2D>();
        }

        private void PutThePlayerAt(Vector2 at, float headingDegrees)
        {
            if (_playerBody != null)
            {
                if (!_playerBodyTaken)
                {
                    _playerBodyWasSimulated = _playerBody.simulated;
                    _playerBodyTaken = true;
                }
                _playerBody.linearVelocity = Vector2.zero;
                _playerBody.angularVelocity = 0f;
                _playerBody.position = at;
                _playerBody.simulated = false;
            }
            Vector3 was = _player.transform.position;
            _player.transform.position = new Vector3(at.x, at.y, was.z);
            if (_playerIso != null)
            {
                _playerIso.HoldHeading(headingDegrees);
                _heldHeadings.Add(_playerIso);
            }
        }

        private string DescribeThePlayer()
        {
            if (_player == null) return "not found";
            bool published = GameServices.PlayerTransform == _player.transform;
            return $"'{_player.name}' at {Fmt(_player.transform.position)}, facing {_player.CurrentFacing}, heading " +
                   $"{(_playerIso != null ? _playerIso.HeadingDegrees.ToString("0.0", CultureInfo.InvariantCulture) : "?")}°, " +
                   $"{(published ? "the published player" : "NOT the published player")}";
        }

        /// <summary>A spot a step inside the footprint beside her, as deep inside the walls as a step allows (a
        /// building counts the player in at its own wall thickness, 0.3 m unless it says otherwise), else the
        /// footprint's own inside point.</summary>
        private static Vector2 AStepInsideBeside(BuildingInterior interior, Vector2 her)
        {
            InteriorFootprint f = interior.Footprint;
            Vector2[] steps =
            {
                new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f),
                new Vector2(0.8f, -0.8f), new Vector2(-0.8f, -0.8f), new Vector2(0.8f, 0.8f), new Vector2(-0.8f, 0.8f),
            };
            foreach (float inset in InsideInsetsMetres)
                foreach (Vector2 step in steps)
                    if (f.Contains(her + step, inset)) return her + step;
            Vector2 fallback = f.ModelToWorld(new Vector2(f.DoorAcrossMetres, f.DoorSign * (f.LengthMetres * 0.5f - 1.2f)));
            Assert.IsTrue(f.Contains(fallback, 0.1f),
                $"harness: found no spot inside '{interior.name}' beside {Fmt(her)}, and its own inside point {Fmt(fallback)} " +
                "is outside its footprint");
            return fallback;
        }

        /// <summary>The compass bearing from one spot to another (0 north, 90 east).</summary>
        private static float BearingDegrees(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float b = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            return b < 0f ? b + 360f : b;
        }

        // =============================================================================================
        //  The claims
        // =============================================================================================

        /// <summary>Every villager as her mesh, or sheltered as nothing, with no id shared and every frame the one
        /// (worldSeed, gameTime) asks for.</summary>
        private static void AssertEveryVillagerIsHerMesh(List<IsoCharacterSprite> villagers, string what)
        {
            var problems = villagers.SelectMany(MeshProblemsOf)
                                    .Concat(DuplicateFigureIds(villagers))
                                    .Concat(villagers.SelectMany(FrameProblemsOf))
                                    .ToList();
            Assert.IsEmpty(problems, $"{what}: not every villager draws as her mesh with both switches on:\n  " +
                                     string.Join("\n  ", problems));
        }

        /// <summary>
        /// Everything that would make a villager draw other than as her mesh with both switches on, by name. Out
        /// of doors: the mesh, with her sprite forced off (never disabled), her overlay sorted exactly as her
        /// sprite, and her figure turned as her sprite faces. Sheltered: neither picture, her figure hidden.
        /// (<c>CharacterMeshCastAshorePlayTests</c>' claims, repeated so each fixture stands alone.)
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
            CharacterFigurePresenter presenter = Presenter(villager);
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
            else if (figure.FigureId <= 0 || figure.FigureId >= OverflowId)
                yield return $"{who}: drawing under facet id {figure.FigureId}, outside 1..{OverflowId - 1}";
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

        /// <summary>Rule 5: a mesh villager's looping clip asks for the frame (worldSeed, gameTime) and her key give.</summary>
        private static IEnumerable<string> FrameProblemsOf(IsoCharacterSprite villager)
        {
            if (!IsMesh(villager)) yield break;
            int expected = ExpectedFrame(villager);
            IsoCharacterFigureRenderer figure = Presenter(villager).AshoreFigure;
            if (expected >= 0 && figure.RequestedFrame != expected)
                yield return $"{Who(villager)}: asks for '{figure.DrawnStateKey}' frame {figure.RequestedFrame}, and " +
                             $"(worldSeed, gameTime) and her key give {expected}";
        }

        /// <summary>Everything that would make a villager draw other than as her sprite, exactly as before, by name.</summary>
        private static IEnumerable<string> SpriteProblemsOf(IsoCharacterSprite villager)
        {
            string who = Who(villager);
            if (villager.GetComponentsInChildren<IsoCharacterFigureRenderer>(true).Length > 0)
                yield return $"{who}: carries a figure renderer, so a figure is built or a facet id is held";
            CharacterFigurePresenter presenter = Presenter(villager);
            if (presenter != null && presenter.WhyNot != CharacterFigurePresenter.Refusal.SwitchOff)
                yield return $"{who}: her presenter says '{presenter.NotDrawingReason ?? "drawing"}', not that a switch is off";
            var sprite = villager.GetComponent<SpriteRenderer>();
            if (sprite == null)
            {
                yield return $"{who}: has no SpriteRenderer";
                yield break;
            }
            if (sprite.forceRenderingOff)
                yield return $"{who}: the sprite is forced off, so she draws as nothing";
            if (Shows(villager) && sprite.sprite == null)
                yield return $"{who}: out of doors with an empty sprite";
        }

        /// <summary>Refused honestly: her whole sprite, nothing built, and the presenter says why.</summary>
        private static IEnumerable<string> RefusalProblemsOf(IsoCharacterSprite villager)
        {
            string who = Who(villager);
            CharacterFigurePresenter presenter = Presenter(villager);
            if (presenter.WhyNot != CharacterFigurePresenter.Refusal.FacetIdRefused)
                yield return $"{who}: refused, and her presenter says '{presenter.NotDrawingReason ?? "drawing"}'";
            if (presenter.AshoreFigure != null)
                yield return $"{who}: refused, and still carries an ashore figure";
            if (villager.GetComponentsInChildren<IsoCharacterFigureRenderer>(true).Length > 0)
                yield return $"{who}: refused, and a figure renderer is still built under her";
            var sprite = villager.GetComponent<SpriteRenderer>();
            if (sprite == null)
            {
                yield return $"{who}: has no SpriteRenderer";
                yield break;
            }
            if (!sprite.enabled)
                yield return $"{who}: refused, and her sprite is switched off";
            if (sprite.forceRenderingOff)
                yield return $"{who}: refused, and her sprite is forced off, so she draws as nothing";
            if (sprite.sprite == null)
                yield return $"{who}: refused, with an empty sprite";
        }

        /// <summary>Two villagers drawn under one facet id would be read as one figure by the pass.</summary>
        private static IEnumerable<string> DuplicateFigureIds(List<IsoCharacterSprite> villagers) =>
            villagers.Where(HoldsAnId)
                     .GroupBy(v => Presenter(v).AshoreFigure.FigureId)
                     .Where(g => g.Count() > 1)
                     .Select(g => $"facet id {g.Key} is held by {string.Join(" and ", g.Select(Who))}");

        // =============================================================================================
        //  The refusal warnings
        // =============================================================================================

        private void StartListening()
        {
            if (_listening) return;
            _warningTraceBefore = Application.GetStackTraceLogType(LogType.Warning);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.ScriptOnly);
            Application.logMessageReceived += OnLog;
            _listening = true;
        }

        private void StopListening()
        {
            if (!_listening) return;
            Application.logMessageReceived -= OnLog;
            Application.SetStackTraceLogType(LogType.Warning, _warningTraceBefore);
            _listening = false;
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Warning && message != null && message.Contains(RefusalWarningMark))
                _refusalStacks.Add(stack ?? string.Empty);
        }

        private int PresenterRefusals() => _refusalStacks.Count(s => s.Contains(PresenterFrameMark));

        private int UnattributedRefusals() => _refusalStacks.Count(string.IsNullOrWhiteSpace);

        // =============================================================================================
        //  The framing
        // =============================================================================================

        /// <summary>
        /// The frame for "the wharf with its villagers". It holds the wharf (its slip head, the landward end, or its
        /// head, whichever holds more), then every villager out of doors, nearest first, while everything chosen
        /// still fits the frame's inner 90%. If nobody out of doors fits beside the wharf, it holds the villager out
        /// of doors nearest the slip head and those that fit beside her, and says how far she is from the wharf and
        /// whether she is on her way. Reads the plate camera, so call it after the first
        /// <see cref="WharfNightStage.FrameOn"/>.
        /// </summary>
        private Vector2 FrameTheWharf(List<IsoCharacterSprite> villagers, out List<IsoCharacterSprite> held,
                                      out string framing)
        {
            RoutineStationEntry slip = Station(SlipHeadStation);
            bool hasHead = TheStations().TryFind(WharfHeadStation, out RoutineStationEntry head);
            HalfExtents(out float halfW, out float halfH);

            List<IsoCharacterSprite> outdoors = NearestFirst(villagers.Where(Shows), slip.Position);
            held = Fit(slip.Position, outdoors, halfW, halfH, out Vector2 centre);
            string anchoredOn = "the slip head";
            if (hasHead)
            {
                List<IsoCharacterSprite> atHead = Fit(head.Position, NearestFirst(outdoors, head.Position), halfW, halfH,
                                                      out Vector2 headCentre);
                if (atHead.Count > held.Count)
                {
                    held = atHead;
                    centre = headCentre;
                    anchoredOn = "the wharf head";
                }
            }
            string wharf = $"the slip head at {Fmt(slip.Position)}, the wharf's landward end" +
                           (hasHead ? $" (its head at {Fmt(head.Position)})" : "");
            if (held.Count > 0)
            {
                framing = $"{wharf}; the frame holds {anchoredOn} and the villagers out of doors nearest it that fit: " +
                          $"{Names(held)}{HeldWalkersNote(held)}";
                return centre;
            }
            if (outdoors.Count == 0)
            {
                framing = $"NOBODY is out of doors on the island at this hour; the frame holds {wharf}";
                return slip.Position;
            }

            IsoCharacterSprite nearest = outdoors[0];
            Vector2 her = nearest.transform.position;
            held = Fit(her, NearestFirst(outdoors, her), halfW, halfH, out centre);
            var routine = nearest.GetComponent<VillagerRoutine>();
            string going = routine != null && routine.Pose.Walking
                ? $"on her way (her routine has her walking, heading {routine.Pose.HeadingDegrees:0}°)"
                : "standing at her station";
            framing = $"NOBODY out of doors is within one frame of {wharf} at this hour. The frame holds the villager out " +
                      $"of doors nearest it, {nearest.name}, {Vector2.Distance(slip.Position, her):0} m from the slip head " +
                      $"and {going}, with those that fit beside her: {Names(held)}{HeldWalkersNote(held)}";
            return centre;
        }

        private static List<IsoCharacterSprite> NearestFirst(IEnumerable<IsoCharacterSprite> villagers, Vector2 to) =>
            villagers.OrderBy(v => Vector2.Distance(to, v.transform.position))
                     .ThenBy(v => v.name, StringComparer.Ordinal)
                     .ToList();

        /// <summary>The frame holding the most villagers out of doors: each one tried as the anchor. Reads the
        /// plate camera, so call it after the first <see cref="WharfNightStage.FrameOn"/>.</summary>
        private Vector2 FrameTheMost(List<IsoCharacterSprite> villagers, out List<IsoCharacterSprite> held)
        {
            HalfExtents(out float halfW, out float halfH);
            List<IsoCharacterSprite> outdoors = villagers.Where(Shows).ToList();
            held = new List<IsoCharacterSprite>();
            Vector2 best = Vector2.zero;
            foreach (IsoCharacterSprite anchor in outdoors)
            {
                Vector2 a = anchor.transform.position;
                List<IsoCharacterSprite> byDistance = outdoors
                    .OrderBy(v => Vector2.Distance(a, v.transform.position))
                    .ThenBy(v => v.name, StringComparer.Ordinal)
                    .ToList();
                List<IsoCharacterSprite> fit = Fit(a, byDistance, halfW, halfH, out Vector2 centre);
                if (fit.Count > held.Count)
                {
                    held = fit;
                    best = centre;
                }
            }
            return best;
        }

        private static List<IsoCharacterSprite> Fit(Vector2 anchor, List<IsoCharacterSprite> nearestFirst, float halfW,
                                                    float halfH, out Vector2 centre)
        {
            float minX = anchor.x, maxX = anchor.x, minY = anchor.y, maxY = anchor.y;
            var held = new List<IsoCharacterSprite>();
            foreach (IsoCharacterSprite v in nearestFirst)
            {
                Vector2 p = v.transform.position;
                float x0 = Mathf.Min(minX, p.x - BoxHalfWidthMetres), x1 = Mathf.Max(maxX, p.x + BoxHalfWidthMetres);
                float y0 = Mathf.Min(minY, p.y - BoxBelowMetres), y1 = Mathf.Max(maxY, p.y + BoxAboveMetres);
                if (x1 - x0 > 2f * halfW * FrameFill || y1 - y0 > 2f * halfH * FrameFill) continue;
                minX = x0; maxX = x1; minY = y0; maxY = y1;
                held.Add(v);
            }
            centre = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            return held;
        }

        /// <summary>The plate camera's half frame. Its pixel-perfect ortho follows its pixel height, so it is only the
        /// plate's once <see cref="WharfNightStage.FrameOn"/> has given it the plate's render texture.</summary>
        private void HalfExtents(out float halfW, out float halfH)
        {
            Camera cam = _stage != null && _stage.Camera != null ? _stage.Camera : Camera.main;
            float ortho = cam != null && cam.orthographic ? cam.orthographicSize : FallbackOrthoSize;
            float aspect = cam != null && cam.aspect > 0f ? cam.aspect : FallbackAspect;
            halfH = ortho;
            halfW = ortho * aspect;
        }

        /// <summary>
        /// Move the game's camera to a second spot once the first <see cref="WharfNightStage.FrameOn"/> has given it
        /// the plate's height, and so its true frame, exactly as FrameOn moved it: its own anchor moves, the world
        /// runs until the camera is still, and only then stops again. The render texture stays. The clock stays
        /// stopped on its own time scale throughout, so the hour does not move.
        /// </summary>
        private IEnumerator ReframeOn(Vector2 at)
        {
            var follow = Object.FindFirstObjectByType<CameraFollow>();
            Assert.IsNotNull(follow, "harness: the region lost its CameraFollow after the first framing");
            Assert.IsNotNull(follow.Target, "harness: the game's camera follows nothing after the first framing");
            Assert.AreEqual("PlateAnchor", follow.Target.name,
                $"harness: the game's camera follows '{follow.Target.name}', not the plate's anchor");
            Transform anchor = follow.Target;
            anchor.position = new Vector3(at.x, at.y, anchor.position.z);

            Camera cam = _stage.Camera;
            Time.timeScale = 1f;
            Vector3 lastPos = cam.transform.position;
            float lastSize = cam.orthographicSize;
            int still = 0, frames = 0;
            while (still < 5 && frames < ReframeMaxFrames)
            {
                yield return null;
                frames++;
                bool moved = (cam.transform.position - lastPos).sqrMagnitude > 1e-8f ||
                             Mathf.Abs(cam.orthographicSize - lastSize) > 1e-5f;
                still = moved ? 0 : still + 1;
                lastPos = cam.transform.position;
                lastSize = cam.orthographicSize;
            }
            Assert.Less(frames, ReframeMaxFrames, "the camera never stopped moving after the plate was reframed");
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
        }

        /// <summary>
        /// The clock is stopped for a plate, so a villager her routine has on her way somewhere reads as standing
        /// still, and would idle. Each one is held at the middle of her def's walking band, so she is drawn walking
        /// as the day has her. The held are named in the caption.
        /// </summary>
        private void HoldTheWalkers(List<IsoCharacterSprite> villagers)
        {
            foreach (IsoCharacterSprite v in villagers)
            {
                var routine = v.GetComponent<VillagerRoutine>();
                if (routine == null || !routine.enabled || !routine.Pose.Walking || !Shows(v) || v.Visual == null) continue;
                float walk = Mathf.Max(0f, v.Visual.WalkSpeedThreshold);
                float run = Mathf.Max(walk, v.Visual.RunSpeedThreshold);
                v.HoldSpeed(run > walk ? walk + (run - walk) * WalkBandMiddle : walk);
                _heldSpeeds.Add(v);
                _walkers.Add(v);
            }
        }

        private string HeldWalkersNote(IEnumerable<IsoCharacterSprite> held)
        {
            List<IsoCharacterSprite> walking = held.Where(_walkers.Contains).ToList();
            return walking.Count == 0
                ? ""
                : $". On their way, held at their walking pace because the clock is stopped: {Names(walking)}";
        }

        private static RoutineStationEntry Station(string id)
        {
            Assert.IsTrue(TheStations().TryFind(id, out RoutineStationEntry entry), $"harness: St Peters has no station '{id}'");
            return entry;
        }

        private static Vector2 Centre(List<IsoCharacterSprite> villagers)
        {
            if (villagers.Count == 0) return Vector2.zero;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (IsoCharacterSprite v in villagers)
            {
                Vector2 p = v.transform.position;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y + BoxAboveMetres);
            }
            return new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        }

        private bool InFrame(Transform t)
        {
            Vector3 vp = _stage.Camera.WorldToViewportPoint(t.position);
            return vp.z > 0f && vp.x >= 0.02f && vp.x <= 0.98f && vp.y >= 0.02f && vp.y <= 0.98f;
        }

        /// <summary>⭐⭐ THE PLATE MUST CONTAIN ITS SUBJECT: its point inside the frame AND something of it
        /// rasterised (<c>AshoreFigurePlatePlayTests</c>).</summary>
        private void AssertTheSubjectIsInFrame(Transform subject, string what)
        {
            Camera cam = _stage.Camera;
            Vector3 vp = cam.WorldToViewportPoint(subject.position);
            Renderer[] renderers = subject.GetComponentsInChildren<Renderer>(true);
            int visible = renderers.Count(r => r != null && r.enabled && r.isVisible);
            string diagnosis =
                $"[{PlateDir}] {what}: subject at world {subject.position}, viewport ({vp.x:0.000}, {vp.y:0.000}, " +
                $"z {vp.z:0.00}); camera at {cam.transform.position} ortho {cam.orthographicSize:0.00} aspect " +
                $"{cam.aspect:0.000}; {visible} of {renderers.Length} renderers visible";
            Debug.Log(diagnosis);
            Assert.Greater(vp.z, 0f, $"the subject is BEHIND the camera. {diagnosis}");
            Assert.That(vp.x, Is.InRange(0.02f, 0.98f), $"the subject is off the side of the frame. {diagnosis}");
            Assert.That(vp.y, Is.InRange(0.02f, 0.98f), $"the subject is off the top or bottom of the frame. {diagnosis}");
            Assert.Greater(renderers.Length, 0, $"the subject carries no renderer at all. {diagnosis}");
            Assert.Greater(visible, 0, $"nothing of the subject rasterised into the frame. {diagnosis}");
        }

        // =============================================================================================
        //  The shutter
        // =============================================================================================

        /// <summary>
        /// Shoot the frozen frame twice back to back, then once with each subject removed alone (her figure hidden,
        /// or her sprite forced off, and put back before the next frame), then once with every subject removed.
        /// A <paramref name="strict"/> subject must change her own box by at least <see cref="MinSubjectPixels"/>
        /// and by more than the back-to-back noise there. With no strict subject, at least one of the
        /// <paramref name="others"/> must. The plate, its control and a caption naming every villager are written
        /// before the verdict, so a red plate can still be opened.
        /// </summary>
        private void Shoot(string key, string frame, List<IsoCharacterSprite> strict, List<IsoCharacterSprite> others,
                           List<IsoCharacterSprite> everyone, List<Transform> alsoInFrame, string extra)
        {
            int shotAt = Time.frameCount;
            byte[] a = _stage.Capture();
            byte[] b = _stage.Capture();
            foreach (IsoCharacterSprite v in strict) AssertTheSubjectIsInFrame(v.transform, $"{key}: {v.name}");
            foreach (IsoCharacterSprite v in others) AssertTheSubjectIsInFrame(v.transform, $"{key}: {v.name}");
            foreach (Transform t in alsoInFrame) AssertTheSubjectIsInFrame(t, $"{key}: {t.name}");

            int w = _stage.Width, h = _stage.Height;
            var pixels = new List<string>();
            var failures = new List<string>();
            int visibleOthers = 0;
            List<IsoCharacterSprite> subjects = strict.Concat(others).ToList();
            foreach (IsoCharacterSprite v in subjects)
            {
                RectInt box = BoxOf(v.transform.position);
                int noise = ChangedIn(a, b, box, w, h);
                int changed = ChangedIn(a, CaptureWithout(new List<IsoCharacterSprite> { v }), box, w, h);
                bool seen = changed >= MinSubjectPixels && changed > noise;
                string drawnAs = IsMesh(v) ? "her mesh" : "her sprite";
                pixels.Add($"{v.name} ({drawnAs}): removing her changes {changed} px of her box {box}; back-to-back noise " +
                           $"there {noise} px{(seen ? "" : " (HIDDEN by the scene in this frame, or not drawn)")}");
                if (strict.Contains(v))
                {
                    if (!seen)
                        failures.Add($"{v.name}: removing her changes {changed} px of her box (noise {noise} px, floor " +
                                     $"{MinSubjectPixels} px), so she is not in the picture");
                }
                else if (seen)
                {
                    visibleOthers++;
                }
            }
            byte[] none = CaptureWithout(subjects);

            string hashA = Hash(a), hashB = Hash(b), hashNone = Hash(none);
            _stage.SavePlate(key + ".png", a);
            _stage.SavePlate(key + "-control.png", none);
            WriteText(key + ".txt",
                $"{key}\nFrame: {frame}\n" +
                $"Shot at engine frame {shotAt}: A, B, each subject removed alone and the control, all in that one frame\n" +
                $"{Stamp()}\n" +
                $"Villagers:\n  - {string.Join("\n  - ", everyone.Select(Describe))}\n" +
                $"Pixels (threshold {PixelThreshold} over three channels; her box {BoxHalfWidthMetres} m each side of her " +
                $"feet, {BoxBelowMetres} m below to {BoxAboveMetres} m above):\n  - " +
                (pixels.Count > 0 ? string.Join("\n  - ", pixels) : "no villager in frame") + "\n" +
                $"Hashes: A {hashA}, B {hashB} ({(hashA == hashB ? "identical" : $"differ in {ChangedIn(a, b, new RectInt(0, 0, w, h), w, h)} px")}); " +
                $"control (every subject removed) {hashNone}\n" +
                $"Files: {key}.png, {key}-control.png\n" +
                (string.IsNullOrEmpty(extra) ? "" : extra + "\n"));

            Assert.Greater(subjects.Count, 0, $"{key}: the plate has no subject in it");
            Assert.AreNotEqual(hashA, hashNone,
                $"{key}: the frame with every subject removed hashes the same as the frame with them, so nothing of them was drawn");
            Assert.IsEmpty(failures, $"{key}: a subject is not in the picture:\n  " + string.Join("\n  ", failures));
            if (strict.Count == 0)
                Assert.Greater(visibleOthers, 0, $"{key}: no villager in the frame is visible in the picture:\n  " +
                                                 string.Join("\n  ", pixels));
        }

        /// <summary>One shot with these villagers removed: each figure hidden, each sprite forced off, and all put
        /// back in the same frame, before any Update can see them.</summary>
        private byte[] CaptureWithout(List<IsoCharacterSprite> hidden)
        {
            var figures = new List<IsoCharacterFigureRenderer>();
            var sprites = new List<SpriteRenderer>();
            foreach (IsoCharacterSprite v in hidden)
            {
                CharacterFigurePresenter p = Presenter(v);
                if (p != null && p.AshoreFigure != null && p.AshoreFigure.Visible)
                {
                    p.AshoreFigure.Visible = false;
                    figures.Add(p.AshoreFigure);
                }
                var sprite = v.GetComponent<SpriteRenderer>();
                if (sprite != null && sprite.enabled && !sprite.forceRenderingOff)
                {
                    sprite.forceRenderingOff = true;
                    sprites.Add(sprite);
                }
            }
            try
            {
                return _stage.Capture();
            }
            finally
            {
                foreach (IsoCharacterFigureRenderer f in figures) if (f != null) f.Visible = true;
                foreach (SpriteRenderer s in sprites) if (s != null) s.forceRenderingOff = false;
            }
        }

        private RectInt BoxOf(Vector2 feet)
        {
            Camera cam = _stage.Camera;
            Vector3 lo = cam.WorldToScreenPoint(new Vector3(feet.x - BoxHalfWidthMetres, feet.y - BoxBelowMetres, 0f));
            Vector3 hi = cam.WorldToScreenPoint(new Vector3(feet.x + BoxHalfWidthMetres, feet.y + BoxAboveMetres, 0f));
            int x0 = Mathf.FloorToInt(Mathf.Min(lo.x, hi.x)), y0 = Mathf.FloorToInt(Mathf.Min(lo.y, hi.y));
            int x1 = Mathf.CeilToInt(Mathf.Max(lo.x, hi.x)), y1 = Mathf.CeilToInt(Mathf.Max(lo.y, hi.y));
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        private static int ChangedIn(byte[] a, byte[] b, RectInt r, int w, int h)
        {
            int n = 0;
            for (int y = Mathf.Max(0, r.yMin); y < Mathf.Min(h, r.yMax); y++)
            {
                for (int x = Mathf.Max(0, r.xMin); x < Mathf.Min(w, r.xMax); x++)
                {
                    int p = (y * w + x) * 4;
                    int d = Math.Abs(a[p] - b[p]) + Math.Abs(a[p + 1] - b[p + 1]) + Math.Abs(a[p + 2] - b[p + 2]);
                    if (d > PixelThreshold) n++;
                }
            }
            return n;
        }

        private static string Hash(byte[] frame)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(frame)).Replace("-", "").Substring(0, 16).ToLowerInvariant();
        }

        // =============================================================================================
        //  The census and the render numbers
        // =============================================================================================

        private sealed class Census
        {
            public string Where;
            public int RegistryHulls, RegistryFigures, LiveHulls, UniqueHullIds, OnOverflow, WithBlock, BlockIds, WithoutBlock;
            public int Granted, Refused, Sheltered, SwitchedOff, NoPresenter, Other, VillagerIds;
            public int CreekGranted, CreekRefused;
            public readonly List<string> Villagers = new List<string>();

            /// <summary>Unique hull ids, the shared overflow id once if any hull is on it, the fore blocks and the
            /// figure ids.</summary>
            public int IdsInUse => UniqueHullIds + (OnOverflow > 0 ? 1 : 0) + BlockIds + RegistryFigures;

            public override string ToString() =>
                $"NUMBERS {Where}: facet ids in use {IdsInUse} of {OverflowId} = {UniqueHullIds} unique hull ids + " +
                $"{(OnOverflow > 0 ? "1 (the overflow id)" : "0")} + {BlockIds} fore-block ids on {WithBlock} hulls + " +
                $"{RegistryFigures} figure ids. Hulls: {LiveHulls} live (registry {RegistryHulls}), {OnOverflow} on the shared " +
                $"overflow id {OverflowId}, {WithoutBlock} without a fore block. Figure ids: {RegistryFigures} (registry), " +
                $"{VillagerIds} held by villagers, {RegistryFigures - VillagerIds} by others (the player). Villagers: " +
                $"{Granted} granted, {Refused} refused, {Sheltered} sheltered and never asked, {SwitchedOff} switch off, " +
                $"{NoPresenter} with no skin, {Other} other; at the creek {CreekGranted} granted, {CreekRefused} refused.\n" +
                $"    - {string.Join("\n    - ", Villagers)}";
        }

        private static Census TakeCensus(string where)
        {
            var c = new Census
            {
                Where = where,
                RegistryHulls = IsoFacetHullRegistry.Count,
                RegistryFigures = IsoFacetHullRegistry.FigureCount,
            };
            var ids = new HashSet<int>();
            foreach (IsoFacetHullRenderer hull in Object.FindObjectsByType<IsoFacetHullRenderer>(FindObjectsInactive.Include))
            {
                if (hull.HullId == 0) continue;
                c.LiveHulls++;
                if (hull.HullId == OverflowId) c.OnOverflow++;
                else ids.Add(hull.HullId);
                if (hull.ForeHullId != 0)
                {
                    c.WithBlock++;
                    c.BlockIds += hull.ForeHullIdTop - hull.ForeHullId + 1;
                }
                else
                {
                    c.WithoutBlock++;
                }
            }
            c.UniqueHullIds = ids.Count;

            foreach (NpcFigureStand stand in Object.FindObjectsByType<NpcFigureStand>(FindObjectsInactive.Include)
                                                   .OrderBy(s => s.gameObject.scene.name, StringComparer.Ordinal)
                                                   .ThenBy(s => s.name, StringComparer.Ordinal))
            {
                bool atTheCreek = stand.gameObject.scene.name == NineMileCreekScene;
                CharacterFigurePresenter p = stand.GetComponent<CharacterFigurePresenter>();
                string state;
                if (p == null)
                {
                    c.NoPresenter++;
                    state = "no presenter (no skin)";
                }
                else if (p.AshoreFigure != null && p.AshoreFigure.IsAshore)
                {
                    c.Granted++;
                    c.VillagerIds++;
                    if (atTheCreek) c.CreekGranted++;
                    state = $"GRANTED facet id {p.AshoreFigure.FigureId} ({p.NotDrawingReason ?? "drawing"})";
                }
                else if (p.AshoreRefused)
                {
                    c.Refused++;
                    if (atTheCreek) c.CreekRefused++;
                    state = $"REFUSED ({p.NotDrawingReason})";
                }
                else if (p.WhyNot == CharacterFigurePresenter.Refusal.SpriteDisabled)
                {
                    c.Sheltered++;
                    state = "sheltered, never asked";
                }
                else if (p.WhyNot == CharacterFigurePresenter.Refusal.SwitchOff)
                {
                    c.SwitchedOff++;
                    state = "switch off, no id";
                }
                else
                {
                    c.Other++;
                    state = $"no id: {p.NotDrawingReason ?? "drawing"}";
                }
                c.Villagers.Add($"{stand.name} [{stand.gameObject.scene.name}]: {state}");
            }
            return c;
        }

        private void WriteNumbers(string key, params Census[] censuses) => WriteNumbers(key, (string)null, censuses);

        private void WriteNumbers(string key, string extra, params Census[] censuses)
        {
            string body = $"{key}\n{Stamp()}\n" + string.Join("\n", censuses.Select(c => c.ToString())) + "\n" +
                          (string.IsNullOrEmpty(extra) ? "" : extra + "\n");
            WriteText(key + ".txt", body);
            foreach (Census c in censuses) Debug.Log($"[{PlateDir}] {c}");
            if (!string.IsNullOrEmpty(extra)) Debug.Log($"[{PlateDir}] NUMBERS {key}: {extra}");
        }

        /// <summary>The render counters recorded, when this editor offers them, each in the category it offers it in.</summary>
        private static readonly string[] RenderCounterNames =
        {
            "Draw Calls Count", "Batches Count", "Total Batches Count", "SetPass Calls Count", "Triangles Count",
            "Vertices Count",
        };

        /// <summary>The editor's own frame stats (its Game view stats window) read by name, since the set moves
        /// between editor versions (6000.5 has no <c>batches</c>).</summary>
        private static readonly string[] EditorStatNames =
        {
            "drawCalls", "setPassCalls", "srpBatcherDrawCalls", "dynamicBatches", "staticBatches", "instancedBatches",
            "triangles", "vertices",
        };

        private sealed class RenderNumbers
        {
            public string Label;
            public bool Ashore;
            public int MeshVillagers, MeshVillagersInFrame;
            public readonly List<KeyValuePair<string, long>> Counters = new List<KeyValuePair<string, long>>();
            public readonly List<KeyValuePair<string, long>> EditorStats = new List<KeyValuePair<string, long>>();
            public double MainThreadMs = -1, RenderMedianMs = -1;

            public static long ValueOf(List<KeyValuePair<string, long>> values, string name)
            {
                foreach (KeyValuePair<string, long> kv in values) if (kv.Key == name) return kv.Value;
                return -1;
            }

            public override string ToString() =>
                $"NUMBERS render {Label}: {MeshVillagers} villagers as meshes ({MeshVillagersInFrame} in frame); profiler " +
                $"counters {List(Counters)}; editor frame stats (UnityStats) {List(EditorStats)}; main thread " +
                $"{Ms(MainThreadMs)} (mean of {RecorderFrames} frames); Camera.Render median {Ms(RenderMedianMs)} of " +
                $"{RenderSamples} (CPU side, the GPU runs on)";

            private static string List(List<KeyValuePair<string, long>> values) =>
                values.Count > 0 ? string.Join(", ", values.Select(kv => $"{kv.Key} {Show(kv.Value)}")) : "none offered";

            private static string Show(long v) => v >= 0 ? v.ToString(CultureInfo.InvariantCulture) : "unavailable";

            private static string Ms(double v) =>
                v >= 0 ? v.ToString("0.000", CultureInfo.InvariantCulture) + " ms" : "unavailable";
        }

        /// <summary>Every counter this editor offers in the Render category (named for the report), and those of
        /// <see cref="RenderCounterNames"/> it offers in any category, each with the category it offers it in.</summary>
        private static List<ProfilerRecorderDescription> RenderCounters(out List<string> offered, out List<string> missing)
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            List<ProfilerRecorderDescription> all = handles.Select(ProfilerRecorderHandle.GetDescription).ToList();
            string render = ProfilerCategory.Render.Name;
            offered = all.Where(d => d.Category.Name == render).Select(d => d.Name).Distinct()
                         .OrderBy(n => n, StringComparer.Ordinal).ToList();
            var chosen = new List<ProfilerRecorderDescription>();
            missing = new List<string>();
            foreach (string name in RenderCounterNames)
            {
                List<ProfilerRecorderDescription> matches = all.Where(d => d.Name == name)
                                                               .OrderBy(d => d.Category.Name == render ? 0 : 1).ToList();
                if (matches.Count > 0) chosen.Add(matches[0]);
                else missing.Add(name);
            }
            return chosen;
        }

        /// <summary>One side's numbers less the other's, counter by counter.</summary>
        private static string DifferenceOf(RenderNumbers a, RenderNumbers b) =>
            string.Join(", ", a.Counters.Select(kv => $"{kv.Key} {Delta(kv.Value, RenderNumbers.ValueOf(b.Counters, kv.Key))}")) +
            "; UnityStats " +
            string.Join(", ", a.EditorStats.Select(kv => $"{kv.Key} {Delta(kv.Value, RenderNumbers.ValueOf(b.EditorStats, kv.Key))}")) +
            $"; main thread {MsDelta(a.MainThreadMs, b.MainThreadMs)}; Camera.Render median " +
            $"{MsDelta(a.RenderMedianMs, b.RenderMedianMs)}";

        private static string MsDelta(double a, double b) =>
            a >= 0 && b >= 0 ? (a - b).ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture) + " ms" : "unavailable";

        /// <summary>The engine's own counters over the frozen frame, then the render itself, timed.</summary>
        private IEnumerator MeasureRender(RenderNumbers into, List<ProfilerRecorderDescription> counters)
        {
            var recorders = new ProfilerRecorder[counters.Count];
            for (int i = 0; i < counters.Count; i++) recorders[i] = ProfilerRecorder.StartNew(counters[i].Category, counters[i].Name);
            ProfilerRecorder main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", RecorderFrames);
            try
            {
                for (int i = 0; i < RecorderFrames + 2; i++) yield return null;
                for (int i = 0; i < counters.Count; i++)
                    into.Counters.Add(new KeyValuePair<string, long>(counters[i].Name,
                                                                     recorders[i].Valid ? recorders[i].LastValue : -1));
#if UNITY_EDITOR
                foreach (string name in EditorStatNames)
                {
                    PropertyInfo stat = typeof(UnityEditor.UnityStats).GetProperty(name, BindingFlags.Public | BindingFlags.Static);
                    object value = stat != null ? stat.GetValue(null) : null;
                    into.EditorStats.Add(new KeyValuePair<string, long>(
                        name, value != null ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : -1));
                }
#endif
                if (main.Valid && main.Count > 0)
                {
                    double sum = 0d;
                    for (int i = 0; i < main.Count; i++) sum += main.GetSample(i).Value;
                    into.MainThreadMs = sum / main.Count * 1e-6;
                }
            }
            finally
            {
                for (int i = 0; i < recorders.Length; i++) recorders[i].Dispose();
                main.Dispose();
            }

            var samples = new double[RenderSamples];
            var watch = new System.Diagnostics.Stopwatch();
            for (int i = 0; i < RenderSamples; i++)
            {
                watch.Restart();
                _stage.Camera.Render();
                watch.Stop();
                samples[i] = watch.Elapsed.TotalMilliseconds;
            }
            Array.Sort(samples);
            into.RenderMedianMs = samples[RenderSamples / 2];
        }

        /// <summary>One mesh villager's figure: its renderers, their materials, and its overlay quad.</summary>
        private static string FigureCost(IsoCharacterSprite v)
        {
            IsoCharacterFigureRenderer f = Presenter(v).AshoreFigure;
            List<Renderer> renderers = f.GetComponentsInChildren<Renderer>(true).ToList();
            MeshRenderer overlay = f.AshoreOverlay;
            if (overlay != null && !renderers.Contains(overlay)) renderers.Add(overlay);
            int materials = renderers.Sum(r => r.sharedMaterials.Length);
            int quad = overlay != null && overlay.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null
                ? filter.sharedMesh.vertexCount
                : -1;
            string overlayMaterial = overlay != null && overlay.sharedMaterial != null ? overlay.sharedMaterial.name : "none";
            return $"{v.name}: {renderers.Count} renderers, {materials} materials " +
                   $"[{string.Join(", ", renderers.Select(r => $"{r.GetType().Name} '{r.name}' x{r.sharedMaterials.Length}"))}]; " +
                   $"overlay material '{overlayMaterial}', overlay mesh {quad} vertices";
        }

        private static string Delta(long on, long off) =>
            on >= 0 && off >= 0 ? (on - off).ToString("+0;-0;0", CultureInfo.InvariantCulture) : "unavailable";

        // =============================================================================================
        //  Captions and the small print
        // =============================================================================================

        private string Describe(IsoCharacterSprite v)
        {
            CharacterFigurePresenter p = Presenter(v);
            var sprite = v.GetComponent<SpriteRenderer>();
            string key = v.TryGetComponent(out NpcFigureStand stand) ? stand.FigureKey ?? "no key" : "no stand";
            string skin = v.Visual != null && v.Visual.Skin != null ? v.Visual.Skin.Id : "none";
            string frame = "not framed";
            if (_stage != null && _stage.Camera != null)
            {
                Vector3 vp = _stage.Camera.WorldToViewportPoint(v.transform.position);
                frame = InFrame(v.transform)
                    ? $"IN FRAME at viewport ({vp.x:0.000}, {vp.y:0.000})"
                    : $"out of frame (viewport {vp.x:0.00}, {vp.y:0.00})";
            }
            string state;
            if (p == null)
            {
                state = "her SPRITE: no presenter (her def links no skin)";
            }
            else if (IsMesh(v))
            {
                IsoCharacterFigureRenderer f = p.AshoreFigure;
                string sort = f.AshoreOverlay != null
                    ? $"overlay layer {f.AshoreOverlay.sortingLayerID} order {f.AshoreOverlay.sortingOrder}"
                    : "no overlay";
                state = $"her MESH, facet id {f.FigureId}, '{f.DrawnStateKey}' frame {f.DrawnFrame} (asked " +
                        $"{f.RequestedFrame}; (worldSeed, gameTime) and her key give {ExpectedFrame(v)}), yaw " +
                        $"{p.AshoreYawDegrees:0.0}°, {sort}";
            }
            else if (p.AshoreRefused)
            {
                state = $"her SPRITE: refused a facet id ({p.NotDrawingReason})";
            }
            else
            {
                state = $"not her mesh: {p.NotDrawingReason ?? "drawing"}" +
                        (HoldsAnId(v) ? $" (holds facet id {p.AshoreFigure.FigureId})" : "");
            }
            string spriteState = sprite == null
                ? "no SpriteRenderer"
                : $"sprite {(sprite.enabled ? "enabled" : "DISABLED (sheltered)")}, forceRenderingOff " +
                  $"{sprite.forceRenderingOff}, layer {sprite.sortingLayerID} order {sprite.sortingOrder}";
            return $"{v.name} ({key}, skin '{skin}', heading {v.HeadingDegrees:0.0}°) at {Fmt(v.transform.position)}, " +
                   $"{frame}: {state}; {spriteState}";
        }

        private string Stamp()
        {
            IGameClock clock = GameServices.Clock;
            string hour = clock != null
                ? $"asked {Hhmm(_hour)}, the clock reads {clock.HourOfDay:0.00} h on day {clock.DayIndex}"
                : $"asked {Hhmm(_hour)}, no clock";
            string seed = GameServices.Environment != null
                ? GameServices.Environment.WorldSeed.ToString(CultureInfo.InvariantCulture)
                : "no environment";
            string camera = _stage != null && _stage.Camera != null
                ? $"camera at {_stage.Camera.transform.position}, ortho {_stage.Camera.orthographicSize:0.00}, aspect " +
                  $"{_stage.Camera.aspect:0.000}, plate {_stage.Width}x{_stage.Height}"
                : "not framed";
            return $"Hour: {hour}; world seed {seed}\nConfig: {ConfigLine()}\nCamera: {camera}\nHealed: {_healed}";
        }

        private static string ConfigLine()
        {
            GameConfig c = GameServices.Config;
            return c == null ? "none published" : $"'{c.name}', MeshCast {c.MeshCast}, MeshCastAshore {c.MeshCastAshore}";
        }

        private static void WriteText(string name, string body)
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.WriteAllText(path, body, new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] caption written: {path}");
        }

        private static string Names(IEnumerable<IsoCharacterSprite> villagers)
        {
            string names = string.Join(", ", villagers.Select(v => v.name));
            return names.Length > 0 ? names : "nobody";
        }

        private static string Hhmm(float hours)
        {
            int minutes = Mathf.RoundToInt(hours * 60f);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        private static string Fmt(Vector3 v) => $"({v.x:0.00}, {v.y:0.00})";

        // =============================================================================================
        //  DontDestroyOnLoad bookkeeping (#764)
        // =============================================================================================

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
    }
}
