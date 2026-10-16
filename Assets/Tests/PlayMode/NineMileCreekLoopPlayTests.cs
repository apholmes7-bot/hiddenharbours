#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;
using HiddenHarbours.Vehicles;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <para><b>NMC H2, PR 2 Phase B: the loop on the activated creek.</b> The scene's own fisher, in the scene
    /// as it ships, walks, drives and trades on G3's ground and the live tide. Nothing here stands in a rig: the
    /// walker, the switcher, the vehicles, the vendors, the pumps and the save service are the ones the scene
    /// wakes. A scripted held intent is the only player.</para>
    ///
    /// <para><b>No boarding.</b> The creek's dev core is walk-only (its builder wires the switcher with no boat):
    /// the boat that lies at berth 0 is the persistent core's, which arrives by sea. So the ladder and stepping
    /// aboard are not proven here; the return ends on the wharf's step-off at berth 0.</para>
    ///
    /// <para><b>The fixture.</b> A previous fixture's core can outlive its scene, and while it (or the destroyed
    /// husk it leaves in the locator) answers, the creek's DevRegionBootstrap destroys the scene's own dev core and
    /// there is no fisher; so the old cores go first and the clock and environment slots are emptied before the
    /// load. The save is the run's redirected service (TestRunSave), checked the way EventBusSaveHelmPlayTests
    /// checks it, and then pointed at a file of this case's own in the run's directory. The shared save is never
    /// opened. The licence wallet is the case's own too, woken on that file before New Game: the game's installs
    /// itself once per play session and publishes itself only as it wakes, so in the full run an earlier fixture's
    /// GameServices.Reset() leaves its slot empty for good (a launch of this class alone never shows it). The slot
    /// goes back as the case found it. The walk is tide-gated as St Peters' fisher is (the creek's dev core leaves
    /// the gate off; the fisher who crosses the bar from St Peters has it on, and the loop is about the tide).</para>
    ///
    /// <para><b>Where she is.</b> Every depth here is read where the walker reads it — at her body point, the
    /// rigidbody's position — and her foot circle (below it) is used only for clearance. The walking is an A*
    /// over the live physics (her own collision mask, non-trigger colliders, her own body excluded) and the live
    /// on-foot depth (decks included), then a pursuit along the path with a held intent; a stall plans again
    /// from where she stands. Every nested coroutine is driven inline so an assertion inside it reaches the
    /// runner.</para>
    /// </summary>
    public class NineMileCreekLoopPlayTests
    {
        const string SceneName = "NineMileCreek";

        // ---- the loop's stops (the Phase A2 walk's speaker spots: where the fisher stands to talk) ----------------
        static readonly Vector2 LicenceSpeaker = new Vector2(-150f, 114.5f);   // the harbourmaster's office
        static readonly Vector2 RodSpeaker = new Vector2(-206f, 112.5f);       // the general store's chandlery
        static readonly Vector2 ClamSpeaker = new Vector2(90.5f, 109.5f);      // the fish buyer's counter
        static readonly Vector2 DorySpeaker = new Vector2(68.182f, 111.25f);   // the dory yard and Hector's barrel
        const float SpeakerArriveMetres = 1.5f;
        const float StepOffArriveMetres = 0.4f;

        const string HarbourmasterSellerId = "seller.nmc_harbourmaster";
        const string ChandlerySellerId = "seller.nmc_chandlery";
        const string DoryYardSellerId = "seller.nmc_yard";
        const string DoryYardName = "ShipwrightDoryYard";        // the yard's id is shared with the shed; the yard is this one
        const string OutboardSellerId = "seller.hectors_barrel";
        const string FishBuyerStallName = "FishBuyerStall";
        const string WharfGasPumpId = "fixture.nmc.wharf_pump_gas";
        const string WharfDieselPumpId = "fixture.nmc.wharf_pump_diesel";
        const string RoadGasPumpId = "fixture.route_91.pump1a_gas";   // the creek's road station on Route 91
        const string ClamSpeciesPath = "Assets/_Project/Data/Fish/SoftShellClam.asset";

        // ---- the crossing: the tidal bar to St Peters, pushed along its centreline from 30 % of its length ---------
        const float PushFromFraction = 0.30f;
        const float PushToFraction = 0.60f;
        const float GutSearchFrom = 0.38f, GutSearchTo = 0.52f;   // where the bar's one gut must lie
        const float LowWaterPushSeconds = 120f;                   // game seconds: far more than the walk needs
        const float HighWaterPushSeconds = 20f;                   // game seconds of swimming: long enough to read the crawl

        // ---- the wharf ------------------------------------------------------------------------------------------
        // On the wharf deck in line with the gangway, and on the float 0.8 m past the gangway's foot (the float's
        // footprint is x 104..152, y 68.8..71.2).
        static readonly Vector2 GangwayFromBody = new Vector2(105f, 87.7f);
        static readonly Vector2 GangwayToBody = new Vector2(105f, 70.4f);
        const float GangwayArriveMetres = 0.3f;

        // ---- the drive: Wharf Road from x -40 down the final ramp to the turning pad, round inside it, and back up -
        static readonly Vector2 DriveStart = new Vector2(-40f, 92f);
        static readonly Vector2 DriveEnd = new Vector2(-20f, 92f);
        // A circle inside the pad (centre (62, 125), r 15) that keeps clear of the derelict dory's box at (70, 118).
        static readonly Vector2 PadLoopCentre = new Vector2(56f, 126f);
        const float PadLoopRadius = 8f;
        const int PadLoopPoints = 10;
        const float DriveReachMetres = 3f;
        const int MaxStepsPerWaypoint = 3000;   // 60 s of physics for one waypoint — fails loudly, never hangs
        const int MaxStepsToStop = 400;
        static RouteFollowMath.RouteFollowTuning Driver => new RouteFollowMath.RouteFollowTuning(3f, 12f, 0.6f, 0.15f, 12f, 20f);

        // ---- the walker -------------------------------------------------------------------------------------------
        const float WalkTimeScale = 2f;        // the walks run at twice speed; physics keeps its fixed step
        const float PlanCell = 0.5f, FineCell = 0.25f;
        const float PlanMargin = 40f, FineMargin = 25f;
        const int PlanMaxExpansions = 600000, FineMaxExpansions = 1500000;
        const float PlanRadiusSlack = 0.02f;   // the plan's circle is this much under her foot's radius
        const float LookaheadMetres = 1.5f;
        const float StallWindowSeconds = 2f, StallMetres = 0.25f;
        const int MaxReplans = 3;
        const int SettleFrames = 30;

        readonly List<Object> _spawned = new List<Object>();
        GameObject _core;
        ControlSwitcher _switcher;
        PlayerWalkController _walk;
        Rigidbody2D _rb;
        CircleCollider2D _foot;
        HeldWalkIntents _held;
        HeldDriveInput _drive;
        SaveService _save;
        ISaveService _previousSave;
        ILicenseService _previousLicences;
        string _runPath, _casePath;
        float _engineTimeScale = 1f;

        // =============================================================================================
        //  THE FIXTURE
        // =============================================================================================

        [UnitySetUp]
        public IEnumerator LoadTheCreekAndStartANewGame()
        {
            LogAssert.ignoreFailingMessages = true;   // the region's known decor-import errors, as every NMC fixture
            _engineTimeScale = Time.timeScale;
            Time.timeScale = 1f;

            foreach (GameRoot old in Object.FindObjectsByType<GameRoot>()) Object.Destroy(old.transform.root.gameObject);
            yield return null;
            GameServices.Clock = null;
            GameServices.Environment = null;
            ShellPause.Reset();
            MoveActionClaim.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            BoardingLadders.Clear();

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;

            GameRoot[] roots = Object.FindObjectsByType<GameRoot>();
            Assert.That(roots.Length, Is.EqualTo(1),
                $"{roots.Length} cores in play after the load; the creek's own dev core must be the only one.");
            _core = roots[0].transform.root.gameObject;   // DevCore: the GameRoot is one of its children
            _switcher = _core.GetComponentInChildren<ControlSwitcher>(true);
            _walk = _core.GetComponentInChildren<PlayerWalkController>(true);
            Assert.That(_switcher, Is.Not.Null, "the dev core carries no ControlSwitcher.");
            Assert.That(_walk, Is.Not.Null, "the dev core carries no PlayerWalkController.");
            _rb = _walk.GetComponent<Rigidbody2D>();
            _foot = _walk.GetComponent<CircleCollider2D>();
            Assert.That(_rb, Is.Not.Null, "the fisher has no Rigidbody2D.");
            Assert.That(_foot, Is.Not.Null, "the fisher has no foot circle.");
            Assert.That(GameServices.Clock, Is.Not.Null, "the creek's core wired no clock.");
            Assert.That(GameServices.Environment, Is.Not.Null, "the creek's core wired no environment.");
            Assert.That(GameServices.Config, Is.Not.Null, "the creek's core published no GameConfig.");

            // ---- the save: the run's redirected service, on a file of this case's own -----------------------------
            _runPath = System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable);
            Assert.That(_runPath, Is.Not.Null.And.Not.Empty, "no TestRunSave redirect: this fixture never opens the shared save.");
            string runDirectory = Path.GetDirectoryName(Path.GetFullPath(_runPath));
            Assert.That(Path.GetDirectoryName(runDirectory), Is.EqualTo(TestRunSave.RunsRoot));
            _save = Object.FindAnyObjectByType<SaveService>();
            Assert.That(_save, Is.Not.Null, "the real BeforeSceneLoad bootstrap must have run");
            Assert.That(_save.SavePath, Is.EqualTo(_runPath));
            Assert.That(SaveStore.ActivePath, Is.EqualTo(_runPath));
            _previousSave = GameServices.Save;
            GameServices.Save = _save;
            _casePath = Path.Combine(runDirectory, "nmc-loop-" + TestContext.CurrentContext.Test.MethodName + ".json");
            _save.Open(_casePath);

            // ---- the licence wallet: the case's own (see the class note), in its slot before New Game -------------
            _previousLicences = GameServices.Licenses;
            var licences = new GameObject("[LicenseService] the loop's own").AddComponent<LicenseService>();
            _spawned.Add(licences.gameObject);
            Assert.That(GameServices.Licenses, Is.SameAs(licences),
                "the case's licence wallet did not publish itself as it woke.");

            ShellFlow.StartNewGame();
            yield return null;
            Assert.That(ShellFlow.Phase, Is.EqualTo(ShellPhase.Playing), "New Game did not put the shell in play.");

            _held = new HeldWalkIntents();
            _walk.ConfigureWalkInput(_held);
            _drive = new HeldDriveInput();
            _switcher.ConfigureDriveInput(_drive);
            WalkerField("_tideGatedWalk").SetValue(_walk, true);
            for (int i = 0; i < 4; i++) yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownTheCreek()
        {
            _held?.Release();
            _drive?.Release();
            Time.timeScale = _engineTimeScale > 0f ? _engineTimeScale : 1f;
            if (_save != null && !string.IsNullOrEmpty(_runPath)) _save.Open(_runPath);   // back on the run's own file
            GameServices.Save = _previousSave ?? _save;
            GameServices.Licenses = _previousLicences;   // as the case found it; its own wallet goes with _spawned

            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            if (_core != null) Object.Destroy(_core);
            yield return null;
            GameServices.Clock = null;
            GameServices.Environment = null;

            const string cleanupName = "NineMileCreekLoopCleanup";
            Scene clean = SceneManager.GetSceneByName(cleanupName);
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene(cleanupName);
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == SceneName)
                    yield return SceneManager.UnloadSceneAsync(s);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        // =============================================================================================
        //  1. THE CROSSING — the bar to St Peters at low and at high water
        // =============================================================================================

        /// <summary>At the month's lowest water the crossing is bare: set on the crest at 30 % of the bar and
        /// pushed east along its centreline, she walks over the gut and on to 60 % without once being out of
        /// her depth.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator TheBarCrossing_AtLowWater_SheWalksOverTheGut()
        {
            Tide tide = ScanTheTide(daylightOnly: false);
            for (var e = PinTheTide(tide.LowAt); e.MoveNext();) yield return e.Current;
            Gut gut = FindTheGut();
            TestContext.WriteLine($"low water {Water():0.###} m at {tide.LowAt:0} s; the gut at {gut.Fraction:0.###} " +
                                  $"({gut.At.x:0.##}, {gut.At.y:0.##}), bed {gut.Bed:0.###} m, depth {Depth(gut.At):0.###} m");
            Assert.That(Depth(gut.At), Is.LessThanOrEqualTo(WadeDepth),
                "premise: at the month's lowest water the gut is not walkable, so the crossing never opens at all");

            var push = new Push();
            for (var e = PushAlongTheBar(push, LowWaterPushSeconds); e.MoveNext();) yield return e.Current;
            TestContext.WriteLine(push.ToString());
            Assert.That(push.FurthestFraction, Is.GreaterThanOrEqualTo(PushToFraction - 0.01f),
                $"at low water she stopped at {push.FurthestFraction:0.###} of the bar — the gut at " +
                $"{gut.Fraction:0.###} or something on the bar held her. {push}");
            Assert.That(push.DeepestAtBody, Is.LessThanOrEqualTo(WadeDepth),
                $"she crossed, but through {push.DeepestAtBody:0.###} m of water at {push.DeepestAt} — the bar is not bare at low water.");
        }

        /// <summary>At the month's highest water the sea is over the crossing: set on the same crest and pushed
        /// the same way, she is out of her depth at every step — past wading, in the swim band — and the walker
        /// holds her to the swim crawl, never a walk; and she is not trapped (P5): she still moves.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator TheBarCrossing_AtHighWater_SheIsOutOfHerDepth_AndCrawls()
        {
            Tide tide = ScanTheTide(daylightOnly: false);
            for (var e = PinTheTide(tide.HighAt); e.MoveNext();) yield return e.Current;
            float shallowest = float.MaxValue;
            float length = Vector2.Distance(NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo);
            for (float f = PushFromFraction; f <= PushToFraction; f += 0.25f / length)
                shallowest = Mathf.Min(shallowest, Depth(OnTheBar(f)));
            float walkCap = Mathf.Max(WalkerFloat("_moveSpeed"), WalkerFloat("_sprintSpeed"));
            float wadeSlow = WalkerFloat("_wadeSlowFactor");
            TestContext.WriteLine($"high water {Water():0.###} m at {tide.HighAt:0} s; the crest from {PushFromFraction} to " +
                                  $"{PushToFraction} is at least {shallowest:0.###} m deep; wade {WadeDepth}, swim {SwimLimit}; " +
                                  $"her top speed {walkCap} m/s, the wade's slow edge x{wadeSlow}");
            Assert.That(shallowest, Is.GreaterThan(WadeDepth),
                $"premise: at the month's highest water the crest is only {shallowest:0.###} m deep somewhere — she could wade it");

            var push = new Push();
            for (var e = PushAlongTheBar(push, HighWaterPushSeconds); e.MoveNext();) yield return e.Current;
            TestContext.WriteLine(push.ToString());
            Assert.That(push.ShallowestAtBody, Is.GreaterThan(WadeDepth),
                $"she found her feet in {push.ShallowestAtBody:0.###} m of water on the crossing at high water. {push}");
            Assert.That(push.FastestMetresPerSecond, Is.LessThanOrEqualTo(walkCap * wadeSlow + 0.05f),
                $"out of her depth she moved at {push.FastestMetresPerSecond:0.###} m/s, faster than the wade's slow edge " +
                $"({walkCap * wadeSlow:0.###} m/s). {push}");
            Assert.That(push.Metres, Is.GreaterThan(0.5f), $"she did not move: out of her depth she is trapped (P5). {push}");
        }

        // =============================================================================================
        //  2. THE DRIVE — down Wharf Road's final ramp to the pad, round inside it, and back up
        // =============================================================================================

        [UnityTest, Timeout(600000)]
        public IEnumerator TheDually_DrivesDownTheRamp_RoundThePad_AndBackUp()
        {
            VehicleController ctl = null;
            foreach (VehicleController v in Object.FindObjectsByType<VehicleController>())
                if (v.name == NineMileCreekTruckPark.TruckName) ctl = v;
            Assert.That(ctl, Is.Not.Null, $"the creek stood no {NineMileCreekTruckPark.TruckName}.");
            var door = ctl.GetComponent<VehicleDoor>();
            var body = ctl.GetComponent<Rigidbody2D>();
            Assert.That(door, Is.Not.Null, "the Dually has no driver's door.");
            Assert.That(body, Is.Not.Null, "the Dually has no body.");
            Assert.That(door.IsDrivable, Is.True, "the Dually's door reports not drivable.");
            Transform machine = ctl.transform;
            ITidalTerrain terrain = GameServices.TidalTerrain;
            Assert.That(terrain, Is.Not.Null, "no ground registered: every dry-land read below would be vacuous.");

            // She is set down on Wharf Road at x -40, facing east, and the fisher at her door.
            body.position = DriveStart;
            body.rotation = -90f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            machine.SetPositionAndRotation(new Vector3(DriveStart.x, DriveStart.y, machine.position.z), Quaternion.Euler(0f, 0f, -90f));
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(Vector2.Distance(machine.position, DriveStart), Is.LessThan(0.5f), "the Dually would not stand at the start.");

            StandHerAt(door.DoorWorldPosition);
            yield return null;
            Assert.That(InteractVerb.TryPerform(new InteractActor(door.DoorWorldPosition, Vector2.zero, InteractContext.OnFoot), 180f),
                Is.True, "E at the Dually's door resolved nothing.");
            yield return null;
            Assert.That(_switcher.Mode, Is.EqualTo(ControlMode.Driving), "E at her door and nobody took the wheel.");

            // Wharf Road falls from the town to the pad by the harbour: down the ramp first, then back up it.
            var down = new List<Vector2>();
            foreach (Vector2 p in NineMileCreekGroundPlan.WharfRoad) if (p.x > DriveStart.x) down.Add(p);
            var pad = new List<Vector2>();
            Vector2 last = down[down.Count - 1];
            float a0 = Mathf.Atan2(last.y - PadLoopCentre.y, last.x - PadLoopCentre.x);
            for (int k = 1; k <= PadLoopPoints; k++)
            {
                float a = a0 + k * 2f * Mathf.PI / PadLoopPoints;
                pad.Add(PadLoopCentre + PadLoopRadius * new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            }
            var back = new List<Vector2>();
            for (int i = down.Count - 2; i >= 0; i--) back.Add(down[i]);
            back.Add(DriveEnd);

            var drive = new Drive();
            for (var e = DriveThrough(machine, ctl, down, "down the ramp", drive); e.MoveNext();) yield return e.Current;
            float foot = terrain.ElevationAt(machine.position);
            for (var e = DriveThrough(machine, ctl, pad, "round the pad", drive); e.MoveNext();) yield return e.Current;
            float padRound = terrain.ElevationAt(machine.position);
            for (var e = DriveThrough(machine, ctl, back, "back up the ramp", drive); e.MoveNext();) yield return e.Current;
            float townEnd = terrain.ElevationAt(machine.position);

            _drive.Set(0f, 0f, true);
            for (int i = 0; i < MaxStepsToStop && Mathf.Abs(ctl.SpeedMetersPerSecond) > 0.05f; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(Mathf.Abs(ctl.SpeedMetersPerSecond), Is.LessThan(0.05f), "on the brake and she would not stop.");
            _drive.Release();
            yield return null;

            Vector2 landing = door.DoorWorldPosition;
            Assert.That(_switcher.BeginInteract(), Is.True, "E in the cab was not answered.");
            yield return null;
            TestContext.WriteLine($"{drive}; the ramp's foot at the pad {foot:0.###} m, round the pad {padRound:0.###} m, " +
                                  $"back at the town end {townEnd:0.###} m; odometer {ctl.OdometerMeters:0.#} m");
            Assert.That(_switcher.Mode, Is.EqualTo(ControlMode.OnFoot), "E in the cab did not put the fisher on her feet.");
            Assert.That(VehicleGrounding.IsDryLandNow(landing), Is.True, $"her door opened onto water at {landing}.");
            Assert.That(drive.WetSamples, Is.EqualTo(0), $"{drive.WetSamples} steps of the drive were off dry land, the first at {drive.FirstWet}.");
            Assert.That(drive.Afloat, Is.False, "the Dually floated somewhere on the drive.");
            Assert.That(townEnd - foot, Is.GreaterThan(1f),
                $"from the pad back to the town end she climbed {townEnd - foot:0.###} m: the road she drove is not the ramp down to the pad.");
        }

        // =============================================================================================
        //  3. THE GANGWAY — down to the float at low and at high water
        // =============================================================================================

        [UnityTest, Timeout(300000)]
        public IEnumerator TheGangway_AtLowWater_SheWalksDownToTheFloat() => DownTheGangway(low: true);

        [UnityTest, Timeout(300000)]
        public IEnumerator TheGangway_AtHighWater_SheWalksDownToTheFloat() => DownTheGangway(low: false);

        IEnumerator DownTheGangway(bool low)
        {
            Tide tide = ScanTheTide(daylightOnly: false);
            for (var e = PinTheTide(low ? tide.LowAt : tide.HighAt); e.MoveNext();) yield return e.Current;
            StandHerAt(GangwayFromBody);
            for (int i = 0; i < 4; i++) yield return null;

            var walk = new Walk();
            for (var e = WalkTo(GangwayToBody, GangwayArriveMetres, "down the gangway", walk); e.MoveNext();)
                yield return e.Current;
            bool onADeck = StandableSurfaces.TryGetDeckElevationNow(Body(), out float deck);
            TestContext.WriteLine($"{(low ? "low" : "high")} water {Water():0.###} m; {walk}; on a deck {onADeck} at {deck:0.###} m");
            Assert.That(walk.Reached, Is.True, $"she never reached the float. {walk}");
            Assert.That(walk.DeepestAtBody, Is.LessThanOrEqualTo(0f),
                $"the way down the gangway put her in {walk.DeepestAtBody:0.###} m of water at {walk.DeepestAt}. {walk}");
            Assert.That(onADeck, Is.True, $"she stands at {Body()}, which is no deck: the float is not under her.");
        }

        // =============================================================================================
        //  4. THE LOOP — licence, rod, clams, the earned dory, its repair, the outboard, fuel, the return, a reload
        // =============================================================================================

        [UnityTest, Timeout(1500000)]
        public IEnumerator TheLoop_LicenceRodClamsDoryRepairOutboardFuel_TheReturn_AndASaveReload()
        {
            IGameClock clock = GameServices.Clock;
            IWallet wallet = GameServices.Wallet;
            Assert.That(wallet, Is.Not.Null, "the creek's core wired no wallet.");
            Assert.That(GameServices.Licenses, Is.Not.Null, "no licence service in play.");
            SaveData save = GameServices.Save.Current;
            Assert.That(save, Is.Not.Null, "New Game left no save data.");
            clock.TimeScale = 0f;   // the working day holds while she walks it
            int topUps = 0;
            void Say(string line) => TestContext.WriteLine(line);
            void Afford(int price, string what)
            {
                if (wallet.Money >= price) return;
                int add = price - wallet.Money;
                topUps += add;
                wallet.Add(add);
                Say($"  fixture top-up ₲{add} before {what} (one bucket of clams is not a season's earnings)");
            }
            Say($"new game: {clock.HourOfDay:0.00} h, water {Water():0.###} m, ₲{wallet.Money}, at {Body()}");

            // ---- the licence -----------------------------------------------------------------------------------------
            var licence = Only<LicenseVendor>(v => v.SellerId == HarbourmasterSellerId, "the harbourmaster");
            for (var e = WalkLeg(LicenceSpeaker, SpeakerArriveMetres, "to the harbourmaster"); e.MoveNext();) yield return e.Current;
            Afford(licence.License.Price, "the licence");
            int before = wallet.Money;
            Assert.That(licence.TryBuy(), Is.True, "the harbourmaster would not sell the licence.");
            Assert.That(before - wallet.Money, Is.EqualTo(licence.License.Price), "the licence cost something other than its price.");
            Assert.That(GameServices.Licenses.IsLicensed(licence.License.Id), Is.True, "bought, and not licensed.");
            Say($"  licence {licence.License.Id} for ₲{licence.License.Price}");

            // ---- the rod ---------------------------------------------------------------------------------------------
            var chandlery = Only<GearShop>(v => v.SellerId == ChandlerySellerId, "the chandlery");
            for (var e = WalkLeg(RodSpeaker, SpeakerArriveMetres, "to the chandlery"); e.MoveNext();) yield return e.Current;
            Afford(chandlery.Offer.Price, "the rod");
            before = wallet.Money;
            Assert.That(chandlery.TryBuy(), Is.True, "the chandlery would not sell the rod.");
            Assert.That(before - wallet.Money, Is.EqualTo(chandlery.Offer.Price), "the rod cost something other than its price.");
            Assert.That(PlayerGear.Owns(save, chandlery.Offer.Id), Is.True, "bought, and the rod is not owned.");
            Say($"  {chandlery.Offer.Id} for ₲{chandlery.Offer.Price}");

            // ---- the clam sale ---------------------------------------------------------------------------------------
            for (var e = WalkLeg(ClamSpeaker, SpeakerArriveMetres, "to the fish buyer"); e.MoveNext();) yield return e.Current;
            var bucket = _core.GetComponentInChildren<ClamBucket>(true);
            Assert.That(bucket, Is.Not.Null, "the fisher carries no clam bucket.");
            if (!PlayerGear.HasBucket(save))
            {
                save.OwnedGear.Add(PlayerGear.BucketId);   // the creek's chandlery sells no bucket; the island's does
                Say($"  fixture grant: {PlayerGear.BucketId} (not on sale at the creek)");
            }
            var clam = UnityEditor.AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(ClamSpeciesPath);
            Assert.That(clam, Is.Not.Null, $"no clam species at {ClamSpeciesPath}.");
            int dug = bucket.CapacityUnits;
            Assert.That(dug, Is.GreaterThan(0), "the bucket holds nothing even with the bucket owned.");
            for (int i = 0; i < dug; i++)
                Assert.That(bucket.TryAdd(new CatchItem(clam.Id, clam.DisplayName, clam.Category, 0.05f, clam.BaseValue,
                                                        clam.SupplyElasticity, clam.SpoilPerDay, Freshness.Landed(clock.TotalSeconds))),
                    Is.True, $"the bucket refused clam {i + 1} of {dug}.");
            var stall = Only<WharfSellPoint>(p => p.gameObject.name == FishBuyerStallName, "the fish buyer's stall");
            Assert.That(Vector2.Distance(Body(), stall.transform.position), Is.LessThan(5f), "she is not at the fish buyer's counter.");
            before = wallet.Money;
            int paid = stall.Sell(bucket, wallet);
            Assert.That(paid, Is.GreaterThan(0), "the fish buyer paid nothing for a bucket of clams.");
            Assert.That(wallet.Money - before, Is.EqualTo(paid), "the payout and the wallet disagree.");
            Assert.That(stall.LastUnitsSold, Is.EqualTo(dug), "the buyer did not take the whole bucket.");
            Assert.That(bucket.UsedUnits, Is.EqualTo(0), "the bucket still holds clams after the sale.");
            Say($"  sold {dug} {clam.Id} for ₲{paid}");

            // ---- the earned dory, and its repair ---------------------------------------------------------------------
            var yard = Only<Shipwright>(s => s.SellerId == DoryYardSellerId && s.gameObject.name == DoryYardName, "the dory yard");
            for (var e = WalkLeg(DorySpeaker, SpeakerArriveMetres, "to the dory yard"); e.MoveNext();) yield return e.Current;
            ShipwrightOffer dory = yard.Offer;
            Afford(dory.Price, "the dory");
            before = wallet.Money;
            Assert.That(yard.TryBuy(), Is.True, "the yard would not sell the dory.");
            Assert.That(before - wallet.Money, Is.EqualTo(dory.Price), "the dory cost something other than its price.");
            Assert.That(save.OwnedBoats, Does.Contain(dory.BoatId), "bought, and the dory is not owned.");
            Say($"  {dory.BoatId} for ₲{dory.Price} (starts damaged: {dory.StartsDamaged})");
            Assert.That(dory.StartsDamaged, Is.True, "premise: the yard's dory is the one that needs work.");
            Assert.That(RepairLedger.IsRepaired(save, dory.BoatId), Is.False, "a damaged dory came out of the yard repaired.");
            Afford(dory.RepairCost, "the repair");
            before = wallet.Money;
            Assert.That(yard.TryRepair(), Is.True, "the yard would not repair the dory.");
            Assert.That(before - wallet.Money, Is.EqualTo(dory.RepairCost), "the repair cost something other than its price.");
            Assert.That(RepairLedger.IsRepaired(save, dory.BoatId), Is.True, "paid for, and the dory is not repaired.");
            Say($"  repaired {dory.BoatId} for ₲{dory.RepairCost}");

            // ---- a purchase: the used outboard ------------------------------------------------------------------------
            var hector = Only<Shipwright>(s => s.SellerId == OutboardSellerId, "Hector's barrel");
            Assert.That(Vector2.Distance(Body(), hector.transform.position), Is.LessThan(6f), "she is not at Hector's barrel.");
            ShipwrightOffer outboard = hector.Offer;
            Afford(outboard.Price, "the outboard");
            before = wallet.Money;
            Assert.That(hector.TryBuy(), Is.True, "Hector would not sell the outboard.");
            Assert.That(before - wallet.Money, Is.EqualTo(outboard.Price), "the outboard cost something other than its price.");
            Assert.That(save.OwnedBoats, Does.Contain(outboard.BoatId), "bought, and the outboard is not owned.");
            Say($"  {outboard.BoatId} for ₲{outboard.Price}");

            // ---- the fuel: a can at Route 91's forecourt (the wharf's pumps are their own case, below) -------------------
            var pump = Only<FuelPump>(p => p.Id == RoadGasPumpId, "Route 91's gas pump");
            Vector2 pumpAt = pump.WorldPosition;
            for (var e = WalkLeg(pumpAt, Mathf.Max(0.5f, pump.ReachMeters - 0.3f), "to Route 91's gas pump"); e.MoveNext();)
                yield return e.Current;
            var can = new GasCan(pump.Grade);
            FuelQuote quote = pump.Quote(can, wallet.Money + 1000);
            Assert.That(quote.CanBuy, Is.True, $"the pump would sell nothing into a {can.CapacityLitres} L can ({quote.Refusal}).");
            Afford(quote.Price, "the fuel");
            before = wallet.Money;
            Assert.That(pump.TryFill(can, wallet), Is.True, "the gas pump would not fill the can.");
            Assert.That(pump.LastLitresDelivered, Is.GreaterThan(0f), "the pump delivered nothing.");
            Assert.That(can.Litres, Is.EqualTo(pump.LastLitresDelivered).Within(1e-3f), "the can and the pump disagree.");
            Assert.That(before - wallet.Money, Is.GreaterThan(0), "the fuel was free.");
            Say($"  {pump.LastLitresDelivered:0.##} L of {pump.Grade} at {pumpAt} for ₲{before - wallet.Money}");

            // ---- the return: to the wharf's step-off at berth 0 (no boat on this core to board: see the class note) -----
            for (var e = WalkLeg(NineMileCreekBuilder.DisembarkPos, StepOffArriveMetres, "back to the step-off at berth 0"); e.MoveNext();)
                yield return e.Current;
            Assert.That(Depth(Body()), Is.LessThanOrEqualTo(0f), $"the step-off at {Body()} is under {Depth(Body()):0.###} m of water.");
            Say($"  back on the step-off at {Body()}, water {Water():0.###} m, depth {Depth(Body()):0.###} m");

            // ---- a save, and the reload --------------------------------------------------------------------------------------
            _save.Save();
            int money = wallet.Money;
            Assert.That(File.Exists(_casePath), Is.True, "the save wrote no file.");
            _save.Open(_casePath);
            SaveData loaded = _save.Current;
            Assert.That(_save.LoadedExistingSave, Is.True, "the reload found no game on disk.");
            Assert.That(loaded, Is.Not.SameAs(save), "the reload handed back the live object, not the disk's.");
            Assert.That(loaded.Money, Is.EqualTo(money), "the disk's money is not the wallet's.");
            Assert.That(loaded.OwnedLicenses, Does.Contain(licence.License.Id), "the licence did not survive the reload.");
            Assert.That(loaded.OwnedGear, Does.Contain(chandlery.Offer.Id), "the rod did not survive the reload.");
            Assert.That(loaded.OwnedBoats, Does.Contain(dory.BoatId), "the dory did not survive the reload.");
            Assert.That(loaded.OwnedBoats, Does.Contain(outboard.BoatId), "the outboard did not survive the reload.");
            Assert.That(RepairLedger.IsRepaired(loaded, dory.BoatId), Is.True, "the repair did not survive the reload.");
            wallet.Add(-1);   // a change the reload must undo
            ShellFlow.ContinueGame();
            yield return null;
            Assert.That(wallet.Money, Is.EqualTo(money), "Continue did not bring the wallet back to the saved money.");
            Assert.That(GameServices.Licenses.IsLicensed(licence.License.Id), Is.True, "Continue lost the licence.");
            Say($"  saved and reloaded: ₲{money}, {loaded.OwnedLicenses.Count} licence(s), {loaded.OwnedGear.Count} gear, " +
                $"{loaded.OwnedBoats.Count} boat(s); fixture top-ups ₲{topUps}");
        }

        // =============================================================================================
        //  5. THE WHARF'S PUMPS — stood down on their ids
        // =============================================================================================

        /// <summary>The wharf's two fuel pedestals stood on the old west apron, which G3 dredged: they stood in 2.2 m of
        /// water at the month's lowest. Both stand down with their ids kept (the owner's ruling) until H5's drawn wharf
        /// re-sites them, and boats fuel by can from Route 91. Each id must still name one pump in the creek, and
        /// neither that pump nor its pedestal may be live.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator TheWharfPumps_StandDown_OnTheirIds()
        {
            yield return null;
            FuelPump[] pumps = Object.FindObjectsByType<FuelPump>(FindObjectsInactive.Include);
            foreach (string id in new[] { WharfGasPumpId, WharfDieselPumpId })
            {
                var named = new List<FuelPump>();
                foreach (FuelPump p in pumps)
                    if (p.Id == id) named.Add(p);
                Assert.That(named.Count, Is.EqualTo(1), $"{id} names {named.Count} pumps in the creek; the stand-down keeps it on one.");
                GameObject hose = named[0].gameObject, pedestal = hose.transform.parent.gameObject;
                TestContext.WriteLine($"{id}: '{hose.name}' on '{pedestal.name}', live {hose.activeInHierarchy}, " +
                                      $"pedestal live {pedestal.activeInHierarchy}");
                Assert.That(hose.activeInHierarchy, Is.False, $"{id} is live; the wharf pumps stand down until H5 re-sites them.");
                Assert.That(pedestal.activeInHierarchy, Is.False, $"{id}'s pedestal '{pedestal.name}' still draws.");
            }
        }

        // =============================================================================================
        //  THE FISHER
        // =============================================================================================

        float WadeDepth => _walk.WadeDepth;
        float SwimLimit => _walk.SwimLimit;
        Vector2 Body() => _rb.position;
        static float Water() => GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds);
        static float Depth(Vector2 at) => TidalWalkability.DepthNow(at);

        static FieldInfo WalkerField(string name)
        {
            FieldInfo f = typeof(PlayerWalkController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null, $"PlayerWalkController.{name} was renamed or removed — re-point this fixture.");
            return f;
        }

        float WalkerFloat(string name) => (float)WalkerField(name).GetValue(_walk);

        void StandHerAt(Vector2 body)
        {
            _held.Release();
            _walk.transform.position = new Vector3(body.x, body.y, _walk.transform.position.z);
            _rb.position = body;
            _rb.linearVelocity = Vector2.zero;
        }

        // =============================================================================================
        //  THE TIDE
        // =============================================================================================

        struct Tide { public double LowAt, HighAt, MeanAt; public float Low, High; }

        /// <summary>A lunar month from the clock's own now, at a 240th of a day: the lowest and highest water,
        /// and the moment nearest the mean.</summary>
        static Tide ScanTheTide(bool daylightOnly)
        {
            IEnvironmentService env = GameServices.Environment;
            double day = GameServices.Config.SecondsPerDay;
            double start = GameServices.Clock.TotalSeconds;
            var tide = new Tide { Low = float.MaxValue, High = float.MinValue };
            float nearest = float.MaxValue;
            for (int i = 0; i <= 30 * 240; i++)
            {
                double t = start + i * day / 240.0;
                if (daylightOnly)
                {
                    double hour = (t / day % 1.0) * 24.0;
                    if (hour < 9.0 || hour > 16.0) continue;
                }
                float w = env.WaterLevelAt(t);
                if (w < tide.Low) { tide.Low = w; tide.LowAt = t; }
                if (w > tide.High) { tide.High = w; tide.HighAt = t; }
                if (Mathf.Abs(w) < nearest) { nearest = Mathf.Abs(w); tide.MeanAt = t; }
            }
            return tide;
        }

        static IEnumerator PinTheTide(double at)
        {
            IGameClock clock = GameServices.Clock;
            clock.TimeScale = 1f;
            clock.SeekTo(at);
            for (int i = 0; i < 4; i++) yield return null;
            clock.TimeScale = 0f;
            for (int i = 0; i < SettleFrames; i++) yield return null;   // the float, the gangway and the hulls on the new water
        }

        // =============================================================================================
        //  THE CROSSING'S PUSH
        // =============================================================================================

        struct Gut { public float Fraction, Bed; public Vector2 At; }

        static Vector2 OnTheBar(float fraction) => Vector2.Lerp(NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo, fraction);

        static Gut FindTheGut()
        {
            ITidalTerrain terrain = GameServices.TidalTerrain;
            Assert.That(terrain, Is.Not.Null, "no ground registered.");
            float length = Vector2.Distance(NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo);
            var gut = new Gut { Bed = float.MaxValue };
            for (float f = GutSearchFrom; f <= GutSearchTo; f += 0.25f / length)
            {
                float bed = terrain.ElevationAt(OnTheBar(f));
                if (bed < gut.Bed) { gut.Bed = bed; gut.Fraction = f; gut.At = OnTheBar(f); }
            }
            return gut;
        }

        sealed class Push
        {
            public float FurthestFraction, Seconds, Metres, FastestMetresPerSecond;
            public float DeepestAtBody = float.NegativeInfinity, ShallowestAtBody = float.PositiveInfinity;
            public Vector2 FurthestAt, DeepestAt;
            public override string ToString() =>
                $"push: furthest {FurthestFraction:0.###} at ({FurthestAt.x:0.##}, {FurthestAt.y:0.##}); {Metres:0.##} m in " +
                $"{Seconds:0.#} s, fastest {FastestMetresPerSecond:0.###} m/s; depth {ShallowestAtBody:0.###}..{DeepestAtBody:0.###} m, " +
                $"deepest at ({DeepestAt.x:0.##}, {DeepestAt.y:0.##})";
        }

        IEnumerator PushAlongTheBar(Push push, float seconds)
        {
            Vector2 from = OnTheBar(PushFromFraction), to = OnTheBar(PushToFraction);
            Vector2 axis = NineMileCreekMainland.BarTo - NineMileCreekMainland.BarFrom;
            float length = axis.magnitude;
            axis /= length;
            StandHerAt(from);
            for (int i = 0; i < 4; i++) yield return null;
            Time.timeScale = WalkTimeScale;

            Vector2 was = Body();
            push.FurthestFraction = PushFromFraction;
            push.FurthestAt = was;
            while (push.Seconds < seconds)
            {
                Vector2 at = Body();
                float fraction = Vector2.Dot(at - NineMileCreekMainland.BarFrom, axis) / length;
                float depth = Depth(at);
                if (fraction > push.FurthestFraction) { push.FurthestFraction = fraction; push.FurthestAt = at; }
                if (depth > push.DeepestAtBody) { push.DeepestAtBody = depth; push.DeepestAt = at; }
                push.ShallowestAtBody = Mathf.Min(push.ShallowestAtBody, depth);
                if (fraction >= PushToFraction) break;

                _held.Walk((to - at).normalized, true);
                yield return null;
                push.Seconds += Time.deltaTime;
                push.Metres += Vector2.Distance(was, Body());
                was = Body();
                push.FastestMetresPerSecond = Mathf.Max(push.FastestMetresPerSecond, _rb.linearVelocity.magnitude);
            }
            _held.Release();
            Time.timeScale = 1f;
            yield return null;
        }

        // =============================================================================================
        //  THE DRIVER
        // =============================================================================================

        sealed class Drive
        {
            public int Steps, WetSamples;
            public Vector2 FirstWet;
            public bool Afloat;
            public readonly List<string> Legs = new List<string>();
            public override string ToString() => $"drive: {Steps} steps, {string.Join("; ", Legs)}";
        }

        IEnumerator DriveThrough(Transform machine, VehicleController ctl, List<Vector2> points, string leg, Drive drive)
        {
            ITidalTerrain terrain = GameServices.TidalTerrain;
            float startedAt = ctl.OdometerMeters;
            for (int w = 0; w < points.Count; w++)
            {
                Vector2 target = points[w];
                Vector2 from = machine.position;
                int steps = 0;
                while (!RouteFollowMath.HasReached(from, target, machine.position, DriveReachMetres))
                {
                    Assert.That(steps++, Is.LessThan(MaxStepsPerWaypoint),
                        $"{leg}: {MaxStepsPerWaypoint} physics steps and she has not reached {target} — she is at " +
                        $"{(Vector2)machine.position} on {BoatKinematics.BearingDegrees(machine.up):0.#}° doing " +
                        $"{ctl.SpeedMetersPerSecond:0.##} m/s.");
                    _drive.Set(RouteFollowMath.Toward(BoatKinematics.BearingDegrees(machine.up), machine.position, target, false, Driver));
                    yield return new WaitForFixedUpdate();
                    drive.Steps++;
                    Vector2 at = machine.position;
                    if (!VehicleGrounding.IsDryLandNow(at) && drive.WetSamples++ == 0) drive.FirstWet = at;
                    drive.Afloat |= ctl.IsAfloat;
                }
            }
            drive.Legs.Add($"{leg} {ctl.OdometerMeters - startedAt:0.#} m to ({machine.position.x:0.#}, {machine.position.y:0.#}) " +
                           $"at {terrain.ElevationAt(machine.position):0.###} m");
        }

        // =============================================================================================
        //  THE WALKER — an A* over the live physics and the live tide, and a pursuit along it
        // =============================================================================================

        sealed class Walk
        {
            public string Leg;
            public bool Reached, Stalled, TimedOut;
            public float Metres, Seconds, DeepestAtBody = float.NegativeInfinity;
            public Vector2 DeepestAt;
            public int Replans, Nodes, Probes;
            public string Why;
            public override string ToString() =>
                $"{Leg}: reached {Reached}, {Metres:0.#} m in {Seconds:0.#} s, {Nodes} path nodes, {Probes} probes, " +
                $"{Replans} replan(s), deepest {DeepestAtBody:0.###} m at ({DeepestAt.x:0.##}, {DeepestAt.y:0.##})" +
                (Stalled ? ", stalled" : "") + (TimedOut ? ", timed out" : "") + (Why != null ? $", {Why}" : "");
        }

        IEnumerator WalkLeg(Vector2 body, float arrive, string leg)
        {
            var walk = new Walk();
            for (var e = WalkTo(body, arrive, leg, walk); e.MoveNext();) yield return e.Current;
            TestContext.WriteLine("  " + walk);
            Assert.That(walk.Reached, Is.True, $"the loop broke {leg}: {walk}");
        }

        IEnumerator WalkTo(Vector2 goal, float arrive, string leg, Walk walk)
        {
            walk.Leg = leg;
            var planner = new Planner(_rb, _foot.offset, _foot.radius - PlanRadiusSlack, WadeDepth);
            Time.timeScale = WalkTimeScale;
            try
            {
                for (int attempt = 0; attempt <= MaxReplans; attempt++)
                {
                    List<Vector2> path = planner.Plan(Body(), goal, arrive, PlanCell, PlanMargin, PlanMaxExpansions, out string why)
                                         ?? planner.Plan(Body(), goal, arrive, FineCell, FineMargin, FineMaxExpansions, out why);
                    walk.Probes = planner.Probes;
                    if (path == null) { walk.Why = $"no plan from {Body()}: {why}"; yield break; }
                    walk.Nodes += path.Count;
                    walk.Stalled = false;
                    for (var e = Follow(path, goal, arrive, walk); e.MoveNext();) yield return e.Current;
                    if (walk.Reached || walk.TimedOut) yield break;
                    walk.Replans++;
                }
            }
            finally
            {
                _held.Release();
                Time.timeScale = 1f;
            }
        }

        IEnumerator Follow(List<Vector2> path, Vector2 goal, float arrive, Walk walk)
        {
            float length = 0f;
            for (int i = 1; i < path.Count; i++) length += Vector2.Distance(path[i - 1], path[i]);
            float budget = length / 1.5f + 30f;
            float spent = 0f, sinceCheck = 0f;
            Vector2 checkpoint = Body(), was = Body();
            int k = 0;
            while (true)
            {
                Vector2 at = Body();
                if (Vector2.Distance(at, goal) <= arrive) { walk.Reached = true; break; }
                int best = k;
                float bestD = Vector2.Distance(at, path[k]);
                for (int j = k + 1; j < Mathf.Min(path.Count, k + 16); j++)
                {
                    float d = Vector2.Distance(at, path[j]);
                    if (d < bestD) { best = j; bestD = d; }
                }
                k = best;
                int t = k;
                while (t < path.Count - 1 && Vector2.Distance(at, path[t]) < LookaheadMetres) t++;
                Vector2 aim = t == path.Count - 1 ? goal : path[t];
                _held.Walk((aim - at).normalized, true);
                yield return null;

                float dt = Time.deltaTime;
                spent += dt;
                walk.Seconds += dt;
                Vector2 now = Body();
                walk.Metres += Vector2.Distance(was, now);
                was = now;
                float depth = Depth(now);
                if (depth > walk.DeepestAtBody) { walk.DeepestAtBody = depth; walk.DeepestAt = now; }
                sinceCheck += dt;
                if (sinceCheck >= StallWindowSeconds)
                {
                    if (Vector2.Distance(now, checkpoint) < StallMetres) { walk.Stalled = true; break; }
                    checkpoint = now;
                    sinceCheck = 0f;
                }
                if (spent > budget) { walk.TimedOut = true; break; }
            }
            _held.Release();
        }

        /// <summary>A grid A* in body positions: a node is open where the depth at the body is wadeable and her
        /// foot circle (offset below the body) touches no solid collider but her own.</summary>
        sealed class Planner
        {
            readonly Rigidbody2D _self;
            readonly Vector2 _footOffset;
            readonly float _radius, _maxDepth;
            readonly ContactFilter2D _filter;
            readonly List<Collider2D> _hits = new List<Collider2D>();
            public int Probes;

            public Planner(Rigidbody2D self, Vector2 footOffset, float radius, float maxDepth)
            {
                _self = self;
                _footOffset = footOffset;
                _radius = radius;
                _maxDepth = maxDepth;
                _filter = new ContactFilter2D { useTriggers = false };
                _filter.SetLayerMask(Physics2D.GetLayerCollisionMask(self.gameObject.layer));
            }

            bool Clear(Vector2 body)
            {
                Probes++;
                if (Depth(body) > _maxDepth) return false;
                _hits.Clear();
                Physics2D.OverlapCircle(body + _footOffset, _radius, _filter, _hits);
                for (int i = 0; i < _hits.Count; i++)
                    if (_hits[i].attachedRigidbody != _self) return false;
                return true;
            }

            public List<Vector2> Plan(Vector2 from, Vector2 goal, float goalRadius, float cell, float margin, int maxExpansions,
                                      out string why)
            {
                Vector2 min = Vector2.Min(from, goal) - Vector2.one * margin;
                Vector2 max = Vector2.Max(from, goal) + Vector2.one * margin;
                float ox = Mathf.Floor(min.x / cell) * cell, oy = Mathf.Floor(min.y / cell) * cell;
                int nx = Mathf.CeilToInt((max.x - ox) / cell) + 1, ny = Mathf.CeilToInt((max.y - oy) / cell) + 1;
                int n = nx * ny;
                var state = new sbyte[n];
                var g = new float[n];
                var parent = new int[n];
                var closed = new bool[n];
                for (int i = 0; i < n; i++) { g[i] = float.PositiveInfinity; parent[i] = -1; }
                Vector2 P(int i) => new Vector2(ox + (i % nx) * cell, oy + (i / nx) * cell);
                bool Ok(int i)
                {
                    if (state[i] == 0) state[i] = (sbyte)(Clear(P(i)) ? 1 : -1);
                    return state[i] > 0;
                }
                int ix0 = Mathf.Clamp(Mathf.RoundToInt((from.x - ox) / cell), 0, nx - 1);
                int iy0 = Mathf.Clamp(Mathf.RoundToInt((from.y - oy) / cell), 0, ny - 1);
                int start = ix0 + iy0 * nx;
                state[start] = 1;   // she stands there now
                g[start] = 0f;
                var open = new Heap();
                open.Push(start, Vector2.Distance(P(start), goal));
                int expansions = 0, found = -1;
                while (open.Count > 0)
                {
                    int cur = open.Pop();
                    if (closed[cur]) continue;
                    closed[cur] = true;
                    if (Vector2.Distance(P(cur), goal) <= goalRadius) { found = cur; break; }
                    if (++expansions > maxExpansions) break;
                    int cx = cur % nx, cy = cur / nx;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= nx || y >= ny) continue;
                        int nb = x + y * nx;
                        if (closed[nb] || !Ok(nb)) continue;
                        if (dx != 0 && dy != 0 && (!Ok(cx + dx + cy * nx) || !Ok(cx + (cy + dy) * nx))) continue;
                        float ng = g[cur] + (dx != 0 && dy != 0 ? cell * 1.41421356f : cell);
                        if (ng >= g[nb]) continue;
                        g[nb] = ng;
                        parent[nb] = cur;
                        open.Push(nb, ng + Vector2.Distance(P(nb), goal));
                    }
                }
                if (found < 0)
                {
                    why = $"{expansions} expansions on a {cell} m grid of {nx}x{ny}, {Probes} probes: no way within {goalRadius} m of {goal}";
                    return null;
                }
                var path = new List<Vector2>();
                for (int i = found; i >= 0; i = parent[i]) path.Add(P(i));
                path.Reverse();
                why = null;
                return path;
            }
        }

        sealed class Heap
        {
            readonly List<int> _node = new List<int>();
            readonly List<float> _key = new List<float>();
            public int Count => _node.Count;

            public void Push(int node, float key)
            {
                _node.Add(node);
                _key.Add(key);
                int i = _node.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (_key[p] <= _key[i]) break;
                    Swap(i, p);
                    i = p;
                }
            }

            public int Pop()
            {
                int top = _node[0];
                int last = _node.Count - 1;
                _node[0] = _node[last];
                _key[0] = _key[last];
                _node.RemoveAt(last);
                _key.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < _node.Count && _key[l] < _key[m]) m = l;
                    if (r < _node.Count && _key[r] < _key[m]) m = r;
                    if (m == i) break;
                    Swap(i, m);
                    i = m;
                }
                return top;
            }

            void Swap(int a, int b)
            {
                (_node[a], _node[b]) = (_node[b], _node[a]);
                (_key[a], _key[b]) = (_key[b], _key[a]);
            }
        }

        // =============================================================================================
        //  SMALL THINGS
        // =============================================================================================

        static T Only<T>(System.Predicate<T> match, string what) where T : Object
        {
            T found = null;
            int count = 0;
            foreach (T c in Object.FindObjectsByType<T>())
                if (match(c)) { found = c; count++; }
            Assert.That(count, Is.EqualTo(1), $"{count} of {what} in play; the loop needs exactly one.");
            return found;
        }

        /// <summary>A twenty-litre can of the pump's own grade: the fuel leg buys into it, not into a hull.</summary>
        sealed class GasCan : IFuelVessel
        {
            public GasCan(string grade) { Grade = grade; }
            public string Grade { get; }
            public float CapacityLitres => 20f;
            public float Litres { get; private set; }
            public void Deliver(float litres) => Litres = Mathf.Min(CapacityLitres, Litres + litres);
            public float Draw(float litres)
            {
                float d = Mathf.Min(Litres, litres);
                Litres -= d;
                return d;
            }
        }
    }
}
#endif
