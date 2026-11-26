using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The first week's boat, end to end in the loaded St Peters scene: he buys the slip dory through the
    /// sale, carries the four mending supplies in his shopping bag, holds the interact key at her through
    /// the three jobs, launches her, and she is his dory afloat at her mooring, boardable and rowable;
    /// the save then holds what the week asks for. A fresh launch of the same save shows her at the
    /// mooring with the slip empty, and nothing is replayed.
    ///
    /// <para><b>The keys are a test keyboard</b> (the E and W keys, pressed and held), so the press goes
    /// through the real control switcher and the held work through the station's own key read. Where an
    /// editor drops the test keyboard's state, the test says so in its log ("[dory-launch] keys: ...") and
    /// makes the same calls the key reads make: the station's Press and TickHeld, and the oar seam.</para>
    ///
    /// <para><b>The save is in memory.</b> It is swapped in after each load, so nothing is written to disk.
    /// With a graphics device it also writes pictures for a person to look at (to <c>DORY_OUT</c>, or the
    /// temporary cache); without one the journey still runs and only the pictures are skipped.</para>
    /// </summary>
    public class SlipDoryMendAndLaunchPlayTests
    {
        private const string SceneName = "StPeters";
        private const string BagPath = "Assets/_Project/Data/Resources/ShoppingBags/PaperShoppingBag.asset";
        private const float PersonRadius = 0.3f;
        private const float ReachInset = 0.4f;
        private const int StandingPoints = 24;
        private const int ShotW = 1280, ShotH = 720;
        private const float RowSeconds = 2.5f;
        private const float MinRowMetres = 0.5f;
        private const float AtMooringMetres = 0.5f;
        private const int TideSampleHours = 25;          // a full day of tides and a little over
        private const int WaitHours = 3;
        private const float ThoughtReadSeconds = 2.5f;   // real time for a thought bubble to type out

        private readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        private readonly List<string> _shots = new List<string>();
        private readonly StringBuilder _notes = new StringBuilder();
        private Keyboard _keyboard;
        private InputSettings _savedSettings, _testSettings;
        private bool _keysLand;
        private string _out;
        private int _launches;

        private sealed class MemorySave : ISaveService
        {
            public MemorySave(SaveData data) { Current = data; }
            public SaveData Current { get; private set; }
            public int Writes;
            public bool GetFlag(string key)
            {
                if (Current?.OnboardingFlags == null) return false;
                foreach (var f in Current.OnboardingFlags) if (f.Key == key) return f.Value;
                return false;
            }
            public void SetFlag(string key, bool value)
            {
                Current.OnboardingFlags ??= new List<SaveFlag>();
                for (int i = 0; i < Current.OnboardingFlags.Count; i++)
                    if (Current.OnboardingFlags[i].Key == key) { Current.OnboardingFlags[i] = new SaveFlag(key, value); return; }
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }
            public void Save() => Writes++;
            public void BeginNewGame() => Current = SaveMigration.NewGame();
        }

        private sealed class Purse : IWallet
        {
            public Purse(int money) { Money = money; }
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        private void OnLaunched(SlipDoryLaunched e) => _launches++;

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
        public IEnumerator SetUp()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);
            // An unfocused batchmode editor disables non-background devices, so key state written to the
            // test keyboard is dropped. A runtime copy of the settings that ignores focus is put in for
            // this test only (never the project's asset) and the original is put back at teardown.
            _savedSettings = InputSystem.settings;
            _testSettings = Object.Instantiate(_savedSettings);
            _testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = _testSettings;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.EnableDevice(_keyboard);
            _keyboard.MakeCurrent();
            yield return null;
            Hold(UnityEngine.InputSystem.Key.F12);
            yield return null;
            _keysLand = _keyboard.f12Key.isPressed;
            Hold();
            yield return null;
            _launches = 0;
            EventBus.Subscribe<SlipDoryLaunched>(OnLaunched);
            _out = System.Environment.GetEnvironmentVariable("DORY_OUT");
            if (string.IsNullOrEmpty(_out)) _out = Path.Combine(Application.temporaryCachePath, "DoryLaunch");
            Directory.CreateDirectory(_out);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EventBus.Unsubscribe<SlipDoryLaunched>(OnLaunched);
            if (_keyboard != null) { InputSystem.RemoveDevice(_keyboard); _keyboard = null; }
            if (_savedSettings != null) InputSystem.settings = _savedSettings;
            if (_testSettings != null) Object.Destroy(_testSettings);
            _savedSettings = _testSettings = null;
            if (_notes.Length > 0)
                File.WriteAllText(Path.Combine(_out, "notes.txt"), _notes.ToString(), new UTF8Encoding(false));
            yield return Unload();
            GameServices.Reset();
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        private IEnumerator Unload()
        {
            GameServices.PendingArrivalKey = null;
            var clean = SceneManager.CreateScene("SlipDoryMendCleanup" + Time.frameCount);
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == SceneName)
                    yield return HiddenHarbours.Tests.Support.SceneSettle.Unload(s);
            }
            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go))
                    Object.DestroyImmediate(go);
            ShoppingBagContents.Clear();
            InteractionGate.Reset();
        }

        /// <summary>Loads the island, puts the memory game in place of the loaded save and tells the scene
        /// a game has loaded, as the title page does; the opening is stood down.</summary>
        private IEnumerator LoadTheIslandWith(MemorySave save, IWallet purse)
        {
            LogAssert.ignoreFailingMessages = true;
            yield return HiddenHarbours.Tests.Support.SceneSettle.Load(SceneName, LoadSceneMode.Single);
            for (int i = 0; i < 4; i++) yield return null;
            foreach (var o in Object.FindObjectsByType<ArrivalOpening>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                o.enabled = false;
            GameServices.OpeningCinematicRunning = false;
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            InteractionGate.Reset();
            GameServices.Save = save;
            GameServices.Wallet = purse;
            EventBus.Publish(new GameLoaded());
            for (int i = 0; i < 10; i++) yield return null;
            HiddenHarbours.UI.TitleScreen.CloseIfOpen();   // the page the scene opens on; the memory game stands in for its choice
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            // The title parks the clock; entering the world starts it (ShellFlow.EnterWorld), so do that last step here.
            if (GameServices.Clock != null) GameServices.Clock.IsPaused = false;
            yield return null;
        }

        private static MemorySave NewGameOnTheSlip()
        {
            var save = new MemorySave(SaveMigration.NewGame());
            SlipDoryStory.BeginForNewGame(save);
            return save;
        }

        private static SlipDory TheSlipDory()
        {
            SlipDory[] all = Object.FindObjectsByType<SlipDory>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(d => d.gameObject.scene.name == SceneName).ToArray();
            Assert.AreEqual(1, all.Length, "the St Peters scene must hold exactly one slip dory");
            return all[0];
        }

        private static GameObject BodyOf(SlipDory dory) => dory.GetComponentInChildren<SpriteRenderer>(true).gameObject;

        private static Transform PersistentBoat()
        {
            var coordinator = Object.FindFirstObjectByType<RegionTravelCoordinator>(FindObjectsInactive.Include);
            Assert.IsNotNull(coordinator, "the St Peters scene has no travel coordinator");
            FieldInfo f = typeof(RegionTravelCoordinator).GetField("_boat", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, "the coordinator's boat field moved; update this test");
            var boat = f.GetValue(coordinator) as Transform;
            Assert.IsNotNull(boat, "the coordinator has no boat wired in the St Peters scene");
            return boat;
        }

        private static void Teleport(Transform t, Vector2 to)
        {
            t.position = new Vector3(to.x, to.y, t.position.z);
            var body = t.GetComponent<Rigidbody2D>();
            if (body != null) { body.position = to; body.linearVelocity = Vector2.zero; }
            Physics2D.SyncTransforms();
        }

        /// <summary>A spot within her reach where he can stand at this water, clear of colliders.</summary>
        private static Vector2 StandingSpotBy(Vector2 centre, float reach)
        {
            ITidalTerrain terrain = GameServices.TidalTerrain;
            float water = GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds);
            float wade = GameServices.Config.WadeDepth;
            Physics2D.SyncTransforms();
            float r = reach - ReachInset;
            for (int k = 0; k < StandingPoints; k++)
            {
                float a = -Mathf.PI * 0.5f + k * Mathf.PI * 2f / StandingPoints;   // from the south side round
                Vector2 p = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (terrain != null && !TidalExposure.IsWalkable(water, terrain.ElevationAt(p), wade)) continue;
                bool blocked = Physics2D.OverlapCircleAll(p, PersonRadius)
                    .Any(c => c != null && !c.isTrigger && c.enabled && c.attachedRigidbody != GameServices.PlayerTransform?.GetComponent<Rigidbody2D>());
                if (!blocked) return p;
            }
            Assert.Fail($"no standing spot within {reach} m of {centre} at {water:F2} m water");
            return centre;
        }

        // Queued, so the change lands inside the next input update and "pressed this frame" reads true.
        private void Hold(params Key[] held) =>
            InputSystem.QueueStateEvent(_keyboard, held.Length == 0 ? new KeyboardState() : new KeyboardState(held));

        private IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// <summary>Real time for the last thought to type out before a picture of it.</summary>
        private IEnumerator LetHimFinishThinking()
        {
            float until = Time.realtimeSinceStartup + ThoughtReadSeconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        /// <summary>A tap of E through the control switcher: down for a frame, then up.</summary>
        private IEnumerator TapE(DoryMendingStation station)
        {
            if (!_keysLand)
            {
                // No key reaches this editor: the press the verb would make, for the published candidate.
                Assert.AreEqual(station.Id, InteractVerb.CurrentCandidateId, "the interact key would reach the station");
                station.Press(GameServices.PlayerTransform, GameServices.BagHands);
                _lastTap = "E: pressed through the station (no key delivery here)";
                yield return null;
                yield break;
            }
            _keyboard.MakeCurrent();
            Hold(UnityEngine.InputSystem.Key.E);
            yield return null;
            _lastTap = KeyState("E");
            yield return null;
            Hold();
            yield return null;
        }

        private string _lastTap = "";

        /// <summary>What the control switcher saw of the key, for a failure message.</summary>
        private string KeyState(string key)
        {
            var sw = Object.FindFirstObjectByType<ControlSwitcher>();
            return $"{key}: current is the test keyboard {ReferenceEquals(Keyboard.current, _keyboard)}, " +
                   $"pressed {_keyboard.eKey.isPressed}, keyboards {InputSystem.devices.Count(d => d is Keyboard)}, " +
                   $"gate {InteractionGate.IsBlocked}, shell blocked {ShellFlow.WorldInputBlocked}, " +
                   $"mode {(sw != null ? sw.Mode.ToString() : "none")}, candidate '{InteractVerb.CurrentCandidateId}'";
        }

        private void Note(string line)
        {
            _notes.AppendLine(line);
            Debug.Log("[dory-launch] " + line);
        }

        // ---- pictures -------------------------------------------------------------------------------

        private sealed class CanvasRecord
        {
            public Canvas Canvas; public RenderMode Mode; public Camera Cam; public float Plane;
            public int Layer; public int Order; public CanvasScaler Scaler; public CanvasScaler.ScaleMode ScaleMode; public float Factor;
        }

        /// <summary>
        /// One picture through the game's own camera at a fixed size, with the screen canvases (the
        /// bubbles) drawn through that camera on the top sorting layer, so what a person would see is in
        /// it. The camera's follow and pixel snap are stood down for the shot and put back after.
        /// </summary>
        private IEnumerator Shoot(string name, Vector2 centre, float ortho, string caption)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Note($"picture {name} skipped: no graphics device"); yield break; }
            var follow = Object.FindAnyObjectByType<CameraFollow>();
            Camera cam = follow != null ? follow.GetComponent<Camera>() : Camera.main;
            if (cam == null) { Note($"picture {name} skipped: no camera"); yield break; }
            bool followOn = follow != null && follow.enabled;
            var ppc = cam.GetComponent<PixelPerfectCamera>();
            bool ppcOn = ppc != null && ppc.enabled;
            Vector3 camPos = cam.transform.position;
            float camOrtho = cam.orthographicSize;
            var rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var records = new List<CanvasRecord>();
            int top = SortingLayer.layers.Length > 0 ? SortingLayer.layers[SortingLayer.layers.Length - 1].id : 0;
            try
            {
                if (follow != null) follow.enabled = false;
                if (ppc != null) ppc.enabled = false;
                cam.transform.position = new Vector3(centre.x, centre.y, camPos.z);
                cam.orthographicSize = ortho;
                cam.targetTexture = rt;
                foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (c == null || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    var rec = new CanvasRecord
                    {
                        Canvas = c, Mode = c.renderMode, Cam = c.worldCamera, Plane = c.planeDistance,
                        Layer = c.sortingLayerID, Order = c.sortingOrder, Scaler = c.GetComponent<CanvasScaler>(),
                    };
                    if (rec.Scaler != null) { rec.ScaleMode = rec.Scaler.uiScaleMode; rec.Factor = rec.Scaler.scaleFactor; }
                    records.Add(rec);
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = Mathf.Max(cam.nearClipPlane + 0.1f, 1f);
                    c.sortingLayerID = top;
                    c.sortingOrder = Mathf.Min(32000, 30000 + rec.Order);
                }
                // Two frames so the canvases lay out against the picture's size and the bubbles re-aim.
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                string path = Path.Combine(_out, name + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.Destroy(tex);
                _shots.Add(path);
                File.WriteAllText(Path.ChangeExtension(path, ".txt"), caption, new UTF8Encoding(false));
                Note($"picture {name}: centre {centre} ortho {ortho}: {caption}");
            }
            finally
            {
                foreach (CanvasRecord r in records)
                {
                    if (r.Canvas == null) continue;
                    r.Canvas.renderMode = r.Mode; r.Canvas.worldCamera = r.Cam; r.Canvas.planeDistance = r.Plane;
                    r.Canvas.sortingLayerID = r.Layer; r.Canvas.sortingOrder = r.Order;
                    if (r.Scaler != null) { r.Scaler.uiScaleMode = r.ScaleMode; r.Scaler.scaleFactor = r.Factor; }
                }
                cam.targetTexture = null;
                cam.transform.position = camPos;
                cam.orthographicSize = camOrtho;
                if (ppc != null) ppc.enabled = ppcOn;
                if (follow != null) follow.enabled = followOn;
                rt.Release();
                Object.Destroy(rt);
            }
        }

        /// <summary>Every picture taken, on one sheet, in order, three across.</summary>
        private void ContactSheet()
        {
            if (_shots.Count == 0) return;
            const int cols = 3, tw = 640, th = 360;
            int rows = (_shots.Count + cols - 1) / cols;
            var sheet = new Texture2D(cols * tw, rows * th, TextureFormat.RGBA32, false);
            var fill = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(24, 24, 24, 255);
            sheet.SetPixels32(fill);
            var small = new RenderTexture(tw, th, 0, RenderTextureFormat.ARGB32);
            for (int i = 0; i < _shots.Count; i++)
            {
                var src = new Texture2D(2, 2);
                src.LoadImage(File.ReadAllBytes(_shots[i]));
                Graphics.Blit(src, small);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = small;
                var cell = new Texture2D(tw, th, TextureFormat.RGBA32, false);
                cell.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
                cell.Apply();
                RenderTexture.active = prev;
                int col = i % cols, row = rows - 1 - i / cols;
                sheet.SetPixels(col * tw, row * th, tw - 4, th - 4, cell.GetPixels(0, 0, tw - 4, th - 4));
                Object.Destroy(cell);
                Object.Destroy(src);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(_out, "contact-sheet.png"), sheet.EncodeToPNG());
            Object.Destroy(sheet);
            small.Release();
            Object.Destroy(small);
            var order = new StringBuilder("Contact sheet, left to right, top to bottom:\n");
            for (int i = 0; i < _shots.Count; i++)
                order.AppendLine($"{i + 1}. {Path.GetFileNameWithoutExtension(_shots[i])}: {File.ReadAllText(Path.ChangeExtension(_shots[i], ".txt"))}");
            File.WriteAllText(Path.Combine(_out, "contact-sheet.txt"), order.ToString(), new UTF8Encoding(false));
        }

        // ---- the journey ----------------------------------------------------------------------------

        [UnityTest, Timeout(900000)]
        public IEnumerator BuyCarryMendLaunch_SheIsHisDoryAfloat_AndAReloadKeepsIt()
        {
            MemorySave save = NewGameOnTheSlip();
            var purse = new Purse(0);
            yield return LoadTheIslandWith(save, purse);

            SlipDory dory = TheSlipDory();
            var station = dory.GetComponent<DoryMendingStation>();
            Assert.IsNotNull(station, "the scene's slip dory carries no mending station");
            DoryMendingStationDef def = station.Station;
            Assert.IsNotNull(def, "the slip dory's mending station has no data");
            string boatId = def.Mending.BoatId;
            Transform boat = PersistentBoat();
            Assert.IsFalse(boat.gameObject.activeSelf, "a new game keeps his boat out of the world");
            Vector2 mooring = def.LaunchBerth;
            Note($"clock {GameServices.Clock.TotalSeconds:F0} s, hour {GameServices.Clock.HourOfDay:F2}; slip dory at {(Vector2)dory.transform.position}; mooring {mooring}; gap {(mooring - (Vector2)dory.transform.position).magnitude:F1} m");

            // ---- bought through the sale ----
            Assert.IsFalse(station.IsAvailable, "not his yet: nothing to mend");
            purse.Add(dory.Sale.Price);
            Assert.AreEqual(SlipBoatSaleResult.Bought, dory.Press(), "the sale buys her");
            yield return Frames(2);
            CollectionAssert.Contains(save.Current.OwnedBoats, boatId);
            Assert.IsTrue(station.IsAvailable, "bought: the slip offers the mending");
            Assert.IsFalse(dory.IsAvailable, "and the sale stands aside");

            // ---- he walks up to her ----
            Transform player = GameServices.PlayerTransform;
            Assert.IsNotNull(player, "the island published no player");
            var switcher = Object.FindFirstObjectByType<ControlSwitcher>();
            Assert.IsNotNull(switcher, "the island has no control switcher");
            Vector2 slip = dory.transform.position;
            Teleport(player, StandingSpotBy(slip, def.ReachMeters));
            yield return Frames(6);
            Assert.AreEqual(station.Id, InteractVerb.CurrentCandidateId, "at her side the interact key offers the mending");
            var jobs = def.Mending.Jobs.OrderBy(j => j.Order).ToList();
            Assert.AreEqual(jobs[0].DisplayName, station.VerbLabel);
            Vector2 frame = slip + new Vector2(0f, 1.5f);
            yield return Shoot("01-verb-patch-the-plank", frame, 5f, $"At the slip, bought: the verb is '{station.VerbLabel}'.");

            // ---- no supplies: refused in words ----
            Note(_keysLand ? "keys: the test keyboard reaches the game; E and W are real key presses"
                           : "keys: this headless editor drops the test keyboard's state, so the presses go through the station's Press/TickHeld and the oars through DevBoatInput.DriveOars (the calls the key reads make)");
            yield return TapE(station);
            yield return Frames(6);
            Assert.AreEqual(DoryMendReason.MissingMaterials, station.LastReason, "with nothing in his bag the job is refused; " + _lastTap);
            Assert.IsFalse(station.IsWorking);
            string missing = station.LastLine;
            Assert.IsFalse(string.IsNullOrEmpty(missing));
            yield return LetHimFinishThinking();
            yield return Shoot("02-missing-supplies", frame, 5f, $"Pressed with nothing in his bag: he thinks '{missing}'.");

            // ---- the four supplies into the bag in his hand, as the shelf does ----
            IShoppingBagHands hands = GameServices.BagHands;
            Assert.IsNotNull(hands, "the player published no hands for the shopping bag");
            var bag = AssetDatabase.LoadAssetAtPath<ShoppingBagDef>(BagPath);
            Assert.IsNotNull(bag, BagPath + " is missing");
            foreach (DoryMendingJobDef job in jobs)
                foreach (MendingMaterial m in job.Materials)
                    for (int n = 0; n < m.Count; n++)
                    {
                        SupplyLocker.Add(save.Current, m.ItemId, 1);
                        Assert.AreEqual(ShoppingBagRefusal.None, hands.TryBag(bag, m.ItemId), m.ItemId + " into the bag");
                    }
            yield return Frames(4);
            Assert.IsTrue(hands.HoldsBag, "the bag is in his hand");

            // ---- the three jobs, each a press and a held key ----
            double clockStart = GameServices.Clock.TotalSeconds;
            for (int j = 0; j < jobs.Count; j++)
            {
                DoryMendingJobDef job = jobs[j];
                Assert.AreEqual(job.DisplayName, station.VerbLabel, "the verb names the job in hand");
                if (j > 0) yield return LetHimFinishThinking();
                if (j > 0)
                    yield return Shoot($"0{3 + j}-verb-{job.Id.Substring(job.Id.LastIndexOf('.') + 1)}", frame, 5f,
                        $"After the last job: the verb is '{station.VerbLabel}'.");
                float heldFor = job.WorkGameMinutes / def.GameMinutesPerHeldSecond;
                if (!_keysLand)
                {
                    // The held key, frame by frame in the station's own tick steps, all within this frame
                    // so the key read (which sees no key here) cannot pause it.
                    yield return TapE(station);
                    Assert.IsTrue(station.IsWorking, $"a press starts '{job.DisplayName}' ({station.LastReason})");
                    float step = Mathf.Max(def.WorkTickSeconds, Time.fixedDeltaTime);
                    int ticks = 0, limit = Mathf.CeilToInt(heldFor * 3f / step) + 10;
                    while (station.IsWorking && ticks < limit)
                    {
                        Transform pl = GameServices.PlayerTransform;
                        bool inReach = ((Vector2)pl.position - (Vector2)station.transform.position).sqrMagnitude <= def.ReachMeters * def.ReachMeters;
                        station.TickHeld(true, inReach, step);
                        ticks++;
                        if (j == 0 && ticks == Mathf.Max(1, Mathf.RoundToInt(heldFor * 0.4f / step)) && station.IsWorking)
                        {
                            // Stand the key read down for the two frames of the picture, then carry on.
                            FieldInfo working = typeof(DoryMendingStation).GetField("_working", BindingFlags.Instance | BindingFlags.NonPublic);
                            working.SetValue(station, false);
                            yield return Shoot("03-mid-mend", frame, 5f, $"Holding E on '{job.DisplayName}': he thinks '{station.LastLine}'.");
                            working.SetValue(station, true);
                        }
                    }
                    Note($"'{job.DisplayName}': {ticks} held ticks of {step} s ({ticks * step:F1} s held; data says {heldFor:F1} s)");
                    yield return Frames(2);
                    Assert.IsFalse(station.IsWorking, $"'{job.DisplayName}' never finished in {ticks} held ticks");
                    Assert.IsTrue(save.GetFlag(job.CompletedFlag), $"'{job.DisplayName}' wrote its flag");
                    continue;
                }
                _keyboard.MakeCurrent();
                Hold(UnityEngine.InputSystem.Key.E);
                yield return Frames(3);
                Assert.IsTrue(station.IsWorking, $"a press of E starts '{job.DisplayName}' ({station.LastReason}; {KeyState("E")})");
                float until = Time.time + heldFor * 3f + 5f;
                bool shot = false;
                while (station.IsWorking && Time.time < until)
                {
                    if (j == 0 && !shot && Time.time > until - heldFor * 3f - 5f + heldFor * 0.4f)
                    {
                        shot = true;
                        yield return Shoot("03-mid-mend", frame, 5f, $"Holding E on '{job.DisplayName}': he thinks '{station.LastLine}'.");
                    }
                    yield return null;
                }
                Hold();
                yield return Frames(2);
                Assert.IsFalse(station.IsWorking, $"'{job.DisplayName}' never finished in {heldFor * 3f + 5f:F0} s of holding");
                Assert.IsTrue(save.GetFlag(job.CompletedFlag), $"'{job.DisplayName}' wrote its flag");
            }
            double minutes = jobs.Sum(jb => (double)jb.WorkGameMinutes);
            Note($"mended: clock moved {(GameServices.Clock.TotalSeconds - clockStart) / (GameServices.Config.SecondsPerDay / 1440.0):F1} game minutes for {minutes} minutes of work");
            CollectionAssert.Contains(save.Current.RepairedBoats, boatId, "mended: she is repaired");
            foreach (DoryMendingJobDef job in jobs)
                foreach (MendingMaterial m in job.Materials)
                    Assert.AreEqual(0, SupplyLocker.Owned(save.Current, m.ItemId), m.ItemId + " was used");
            Assert.AreEqual(def.LaunchLabel, station.VerbLabel);
            Assert.IsTrue(SlipDoryStory.IsOnTheSlip(save), "mended, she still lies on the slip");
            yield return LetHimFinishThinking();
            yield return Shoot("06-verb-launch-her", frame, 5f, $"Mended: '{station.LastLine}'. The verb is '{station.VerbLabel}'.");

            // ---- the launch ----
            yield return TapE(station);
            yield return Frames(6);
            Assert.AreEqual(1, _launches, "one press, one launch");
            Assert.IsFalse(SlipDoryStory.IsOnTheSlip(save), "launched: the slip flag is cleared");
            Assert.IsFalse(BodyOf(dory).activeSelf, "the slip is empty");
            Assert.IsFalse(station.IsAvailable);
            Assert.IsTrue(boat.gameObject.activeInHierarchy, "his boat is in the world");
            Assert.Less(((Vector2)boat.position - mooring).magnitude, AtMooringMetres, $"she lies at {(Vector2)boat.position}, not her mooring {mooring}");
            var controller = boat.GetComponent<BoatController>();
            Assert.IsNotNull(controller);
            Assert.AreEqual(boatId, controller.Hull != null ? controller.Hull.Id : null, "the boat in the world is his dory");
            Assert.AreEqual(def.LaunchedLine, station.LastLine, "the launch line is his thought (it names where she lies)");
            float water = GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds);
            float bed = GameServices.TidalTerrain.ElevationAt(boat.position);
            Assert.Greater(water - bed, controller.Hull.DraughtMeters, $"afloat: {water - bed:F2} m of water under a {controller.Hull.DraughtMeters} m draught");
            Note($"launched: boat at {(Vector2)boat.position}, water {water:F2} m, bed {bed:F2} m, depth {water - bed:F2} m; line '{station.LastLine}'");

            // She rides the tide at her mooring: over the next day of tides (the water is a pure function of
            // time) the depth there never falls under her draught.
            double secondsPerHour = GameServices.Config.SecondsPerDay / 24.0;
            double now = GameServices.Clock.TotalSeconds;
            float lowest = float.MaxValue, highest = float.MinValue;
            for (int q = 0; q <= TideSampleHours * 4; q++)
            {
                float w = GameServices.Environment.WaterLevelAt(now + q * secondsPerHour * 0.25);
                lowest = Mathf.Min(lowest, w); highest = Mathf.Max(highest, w);
            }
            Note($"tide at her mooring over {TideSampleHours} h: water {lowest:F2} to {highest:F2} m over a {bed:F2} m bed; least depth {lowest - bed:F2} m, draught {controller.Hull.DraughtMeters} m");
            Assert.Greater(lowest - bed, controller.Hull.DraughtMeters, "she grounds at her mooring at low water");

            yield return LetHimFinishThinking();
            Vector2 both = (slip + mooring) * 0.5f + new Vector2(0f, 2f);
            yield return Shoot("07-after-launch-wide", both, 11f, $"After the launch: the slip at {slip} is empty, his dory lies at her mooring {mooring}. He thinks '{station.LastLine}'.");
            var gameCam = Object.FindAnyObjectByType<CameraFollow>()?.GetComponent<Camera>();
            Vector2 view = gameCam != null ? (Vector2)gameCam.transform.position : (Vector2)player.position;
            float viewOrtho = gameCam != null ? gameCam.orthographicSize : 7f;
            Note($"the game camera after the launch: centre {view}, ortho {viewOrtho:F2} (sees {viewOrtho * 2f * ShotW / ShotH:F1} x {viewOrtho * 2f:F1} m); she is {((Vector2)boat.position - view).magnitude:F1} m from its centre");
            yield return Shoot("08-after-launch-player-view", view, viewOrtho, "After the launch, through the game camera as it stands: can he see where she went?");

            // Three hours on, by the clock, she is still on her mooring.
            Assert.IsTrue(GameServices.Clock.FastForwardTo(GameServices.Clock.TotalSeconds + WaitHours * secondsPerHour), "the clock moves on");
            yield return Frames(10);
            Assert.Less(((Vector2)boat.position - mooring).magnitude, AtMooringMetres, $"{WaitHours} h on she lies at {(Vector2)boat.position}, off her mooring {mooring}");
            Note($"{WaitHours} h on (hour {GameServices.Clock.HourOfDay:F2}): boat at {(Vector2)boat.position}, water {GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds):F2} m");

            // ---- board and row her ----
            Teleport(player, (Vector2)boat.position + new Vector2(0f, -1.6f));
            yield return Frames(3);
            Assert.IsTrue(switcher.WithinBoardReach(), "from the wharf by her mooring he can board her");
            Assert.IsTrue(HiddenHarbours.Tests.Support.TimedBoarding.Interact(switcher), "he boards her");
            Assert.AreEqual(ControlMode.OnDeck, switcher.Mode);
            // On deck he is placed in her deck frame (the deck walk re-projects a world teleport): onto
            // the sole under her thwart, as the dory's own aboard tests do.
            BoatDeckDef deck = BoatDeckAreas.Resolve(boat.gameObject);
            Assert.IsNotNull(deck, "his dory has no deck");
            var deckWalk = player.GetComponent<DeckWalkController>();
            Assert.IsNotNull(deckWalk, "the player has no deck walk");
            deckWalk.SnapToDeckLocal(new Vector2(deck.HelmStationLocalMeters.x, deck.HelmStationLocalMeters.y));
            yield return Frames(2);
            Assert.IsTrue(switcher.WithinHelmReach(), $"at her thwart (he is at {(Vector2)player.position}, her helm at {switcher.HelmWorldPosition})");
            Assert.IsTrue(HiddenHarbours.Tests.Support.TimedBoarding.Interact(switcher), "he takes the oars");
            Assert.AreEqual(ControlMode.Aboard, switcher.Mode, "at the oars");
            Vector2 start = boat.position;
            var oars = boat.GetComponentInChildren<DevBoatInput>(true) ?? Object.FindFirstObjectByType<DevBoatInput>();
            Assert.IsNotNull(oars, "his dory has no oar input");
            _keyboard.MakeCurrent();
            Hold(UnityEngine.InputSystem.Key.W);
            float rowUntil = Time.time + RowSeconds;
            bool rowShot = false;
            while (Time.time < rowUntil)
            {
                if (!rowShot && Time.time > rowUntil - RowSeconds * 0.4f)
                {
                    rowShot = true;
                    yield return Shoot("09-rowing", (Vector2)boat.position, 6f, "He rows her off the mooring (W held).");
                }
                yield return null;
                // After the key read's Update: both oars ahead through the seat-gated seam, as W gives.
                if (!_keysLand) oars.DriveOars(1f, 1f, false);
            }
            if (!_keysLand) oars.DriveOars(0f, 0f, false);
            Hold();
            float rowed = ((Vector2)boat.position - start).magnitude;
            Note($"rowed {rowed:F2} m in {RowSeconds} s");
            Assert.Greater(rowed, MinRowMetres, "holding W at the oars moves her");

            // ---- what the week asks for ----
            CollectionAssert.Contains(save.Current.OwnedBoats, boatId, "the week sees her his");
            CollectionAssert.Contains(save.Current.RepairedBoats, boatId, "and repaired");
            Assert.AreEqual(boatId, save.Current.ActiveHullId, "she is his active boat");

            // ---- a fresh launch of the same game ----
            string json = JsonUtility.ToJson(save.Current);
            yield return Unload();
            GameServices.Reset();
            yield return Frames(2);
            var loaded = new MemorySave(JsonUtility.FromJson<SaveData>(json));
            yield return LoadTheIslandWith(loaded, new Purse(0));
            SlipDory again = TheSlipDory();
            var againStation = again.GetComponent<DoryMendingStation>();
            Transform boatAgain = PersistentBoat();
            Assert.AreEqual(1, _launches, "the load does not launch her again");
            Assert.IsFalse(SlipDoryStory.IsOnTheSlip(loaded));
            Assert.IsFalse(BodyOf(again).activeSelf, "the slip is still empty");
            Assert.IsFalse(again.IsAvailable || againStation.IsAvailable, "and offers nothing");
            Assert.IsTrue(boatAgain.gameObject.activeInHierarchy, "his dory is in the world");
            Assert.Less(((Vector2)boatAgain.position - mooring).magnitude, AtMooringMetres,
                $"after the load she lies at {(Vector2)boatAgain.position}, not her mooring {mooring}");
            Note($"reloaded: boat at {(Vector2)boatAgain.position}, active {boatAgain.gameObject.activeInHierarchy}; launches {_launches}");
            yield return Shoot("10-after-reload", both, 11f, "A fresh launch of the saved game: the slip is empty, his dory is at her mooring.");

            ContactSheet();
        }
    }
}
