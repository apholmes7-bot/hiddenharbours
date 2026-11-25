using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;
using HiddenHarbours.Tests.Support;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class DockLadderContractTests
    {
        private sealed class DockBed : ITidalTerrain
        {
            public float Height;
            public float ElevationAt(Vector2 worldPos) => Height;
        }

        private sealed class DockSea : IEnvironmentService
        {
            public float Water;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double seconds) => Water;
            public float WaterLevelAt(double seconds) => Water;
        }

        // A flat, walkable deck reaching the foot isolates the handoff from PR 2's berth snugging.
        private static HopRig LandingRig(float water, bool withLadder = true)
        {
            var rig = new HopRig();
            rig.Player.position = new Vector3(86.2f, 87.35f, 0f);
            rig.Boat.position = new Vector3(86.2f, 84.65f, 0f);
            rig.Boat.rotation = Quaternion.Euler(0f, 0f, -90f);
            var boat = rig.Boat.GetComponent<BoatController>();
            boat.Hull.DraughtMeters = .3f;
            GameServices.Environment = new DockSea { Water = water };
            GameServices.TidalTerrain = new DockBed { Height = -6f };
            rig.Boat.gameObject.AddComponent<HullTideRide>().Configure(0f, .3f);
            var deck = ScriptableObject.CreateInstance<BoatDeckDef>();
            rig.Own(deck);
            deck.Id = "deck.ladder_landing_fixture";
            deck.WalkHalfExtents = new Vector2(2f, 3f);
            deck.Areas = new[] { DeckArea.From("floor", DeckAreaKind.Deck, new[] {
                new Vector3(-2f, -3f, 0f), new Vector3(2f, -3f, 0f),
                new Vector3(2f, 3f, 0f), new Vector3(-2f, 3f, 0f) }) };
            BoatDeckAreas.Write(rig.Boat.gameObject, deck);
            var landing = rig.Go("Landing", new Vector3(86.2f, 87.35f, 0f));
            var dock = rig.Go("Dock", new Vector3(86.2f, 85.5f, 0f));
            rig.Controls.Configure(rig.Walk, boat, null, dock.transform, 12f, landing.transform);
            typeof(ControlSwitcher).GetField("_boardLocalOffset", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(rig.Controls, Vector2.zero);
            rig.Go("Quay", Vector3.zero).AddComponent<StandablePlatform>()
                .Configure("wharf.landing_fixture", new Rect(78.6f, 86f, 15.2f, 2f), 3.4f);
            if (withLadder)
            {
                var ladder = rig.Go("Ladder", new Vector3(86.2f, 86f, 0f)).AddComponent<WharfLadder>();
                ladder.Configure("ladder.landing_fixture", 3.4f, 180f);
                ladder.ConfigureClimbTop(1f);
            }
            return rig;
        }

        private static Vector3 LandingRide(HopRig rig)
            => Vector3.up * rig.Boat.GetComponent<HullTideRide>().ScreenRiseNow();

        private sealed class HandoffHullPose : MonoBehaviour, IHullMeshRenderer
        {
            public float HeadingDirUnits { get; set; }
            public float RollDegrees { get; set; }
            public float PitchDegrees { get; set; }
            public float HeavePixels { get; set; }
            public float RidePixels { get; set; }
            public bool IsConfigured => true;
            public float DeckOccluderId => 0f;
            public IDeckOccupantSlots DeckOccupants => NoDeckOccupantSlots.Instance;
            public void SetSorting(int layer, int order) { }
            public void SetDeckOccupant(Vector3 at, bool active) { }
        }

        [TestCase(2.2f)]
        [TestCase(-2.2f)]
        public void ALadderHandoffIncludesTheHullsPublishedMeshHeave(float water)
        {
            using var rig = LandingRig(water);
            const float heaveMetres = -.38f;
            var mesh = ScriptableObject.CreateInstance<HullMeshDef>();
            rig.Own(mesh);
            mesh.PxPerMetre = 32;
            var visual = ScriptableObject.CreateInstance<BoatVisualDef>();
            rig.Own(visual);
            visual.Variant = BoatHullVariant.Mesh;
            visual.HullMesh = mesh;
            rig.Boat.GetComponent<BoatController>().Hull.Visual = visual;
            var pose = rig.Boat.gameObject.AddComponent<HandoffHullPose>();
            pose.HeavePixels = heaveMetres * mesh.PxPerMetre;

            Assert.IsTrue(rig.Controls.BeginInteract());
            Assert.IsTrue(rig.Controls.IsLadderBoarding);
            Vector3 end = (Vector3)typeof(ControlSwitcher).GetMethod("VaultEndWorld",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(rig.Controls, null);
            TimedBoarding.Complete(rig.Controls);
            Assert.AreEqual(ControlMode.OnDeck, rig.Controls.Mode);
            Vector3 drawn = rig.Player.position + LandingRide(rig) + Vector3.up * heaveMetres;
            Assert.LessOrEqual(Vector3.Distance(end, drawn), .25f,
                "the hull's published mesh heave must already be in the last off-leg destination");
        }

        [TestCase(2.2f)]
        [TestCase(-2.2f)]
        public void ALadderEndsUnderTheFootWhenTheDeckReachesIt(float water)
        {
            using var rig = LandingRig(water);
            // Independent authored geometry: cap y87, standoff .275, top +3.4, camera elevation 40.
            float footY = 87f - .275f * Mathf.Sin(40f * Mathf.Deg2Rad)
                - (3.4f - water) * Mathf.Cos(40f * Mathf.Deg2Rad);
            Assert.IsTrue(TimedBoarding.Interact(rig.Controls));
            Assert.AreEqual(ControlMode.OnDeck, rig.Controls.Mode);
            Vector3 drawn = rig.Player.position + LandingRide(rig);
            Assert.LessOrEqual(Vector3.Distance(new Vector3(86.2f, footY, 0f), drawn), .25f,
                "boarding keeps the foot's landing instead of sending her to the old seat");
        }

        [TestCase(2.2f)]
        [TestCase(-2.2f)]
        public void ALadderStepAshoreStartsAtTheRiddenDeckFigure(float water)
        {
            using var rig = LandingRig(water);
            Assert.IsTrue(TimedBoarding.Interact(rig.Controls));
            Vector3 drawn = rig.Player.position + LandingRide(rig);
            Assert.IsTrue(rig.Controls.BeginInteract());
            Assert.LessOrEqual(Vector3.Distance(drawn, rig.Player.position), .25f, "the mode handoff cannot unride her");
            rig.Controls.TickBoardingMove(0f);
            Assert.LessOrEqual(Vector3.Distance(drawn, rig.Player.position), .25f, "the approach starts at her picture");
        }

        [TestCase(2.2f)]
        [TestCase(-2.2f)]
        public void ALadderReturnKeepsTheRiddenDeckPointThroughItsHandoff(float water)
        {
            using var rig = LandingRig(water);
            Assert.IsTrue(TimedBoarding.Interact(rig.Controls));
            Vector3 drawn = rig.Player.position + LandingRide(rig);
            Assert.IsTrue(rig.Controls.BeginInteract());
            rig.Controls.TickBoardingMove(.1f);
            typeof(ControlSwitcher).GetMethod("CancelBoardingMove", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(rig.Controls, new object[] { true });
            TimedBoarding.Complete(rig.Controls);
            Assert.AreEqual(ControlMode.OnDeck, rig.Controls.Mode);
            Assert.LessOrEqual(Vector3.Distance(drawn, rig.Player.position + LandingRide(rig)), .25f);
        }

        [TestCase(2.2f)]
        [TestCase(-2.2f)]
        public void TheStepRouteKeepsItsAuthoredUnriddenSeat(float water)
        {
            using var rig = LandingRig(water, withLadder: false);
            Assert.IsTrue(rig.Controls.BeginInteract());
            Assert.IsFalse(rig.Controls.IsLadderBoarding);
            TimedBoarding.Complete(rig.Controls);
            Assert.AreEqual(ControlMode.OnDeck, rig.Controls.Mode);
            Assert.Less(Vector3.Distance(rig.Boat.position, rig.Player.position), .0001f,
                "the authored centre seat on this flat deck stays unchanged on the step route");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TheCapMovesEveryClimbLegWithoutMovingReachOrBerth(bool ascending)
        {
            using var rig = new HopRig();
            var go = new GameObject("Raised cap fixture");
            try
            {
                go.transform.position = rig.Player.position;
                var ladder = go.AddComponent<WharfLadder>();
                ladder.Configure("ladder.cap_fixture", 3.4f, 180f);
                IBoardingLadder contract = ladder;
                Assert.AreEqual(0f, contract.ClimbTopScreenOffset);
                Vector2 plan = contract.WorldPosition;
                ladder.ConfigureClimbTop(NineMileCreekGroundPlan.LadderClimbTopScreenOffset);
                Assert.AreEqual(plan, contract.WorldPosition);
                Assert.IsTrue(BoardingLadders.TryFindNearestNow(plan, .01f, out var nearest));
                Assert.AreSame(ladder, nearest);
                if (ascending) Assert.IsTrue(TimedBoarding.Interact(rig.Controls));
                Assert.IsTrue(rig.Controls.BeginInteract());
                Assert.IsTrue(rig.Controls.IsLadderBoarding);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var type = typeof(ControlSwitcher);
                Vector3 seat = (Vector3)type.GetField("_moveLadderSeat", flags).GetValue(rig.Controls);
                Vector2 expected = plan + Vector2.up * contract.ClimbTopScreenOffset
                    + LadderBoardingMath.ProjectedSeatOffset(contract.FaceHeadingDegrees,
                        contract.StandoffMetres, IsoGround.CameraElevationDegrees);
                Assert.Less(Vector2.Distance(expected, seat), .0001f);
                float gap = (float)type.GetField("_moveGapMetres", flags).GetValue(rig.Controls);
                Vector3 foot = seat - Vector3.up * LadderBoardingMath.ProjectHeight(gap, IsoGround.CameraElevationDegrees);
                Vector3 start = (Vector3)type.GetMethod("LegTwoStartWorld", flags).Invoke(rig.Controls, null);
                Vector3 end = (Vector3)type.GetMethod("LadderRungsEndWorld", flags).Invoke(rig.Controls, null);
                Assert.Less(Vector3.Distance(ascending ? foot : seat, start), .0001f);
                Assert.Less(Vector3.Distance(ascending ? seat : foot, end), .0001f);
                float transition = (float)type.GetField("_moveTransitionSeconds", flags).GetValue(rig.Controls);
                float climb = (float)type.GetField("_moveClimbSeconds", flags).GetValue(rig.Controls);
                type.GetMethod("TickLadderLeg", flags).Invoke(rig.Controls, new object[] { transition + climb, start });
                Assert.Less(Vector3.Distance(end, rig.Player.position), .0001f,
                    "the off-leg starts at the same raised waypoint where the rungs ended");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(2.2f, -6f, false)]
        [TestCase(-2.2f, -6f, false)]
        [TestCase(-2.2f, -1f, true)]
        public void BothBoardingAndArrivalReadThePicturesGroundedWaterline(float water, float bed, bool aground)
        {
            using var rig = new HopRig();
            GameServices.Environment = new DockSea { Water = water };
            GameServices.TidalTerrain = new DockBed { Height = bed };
            var ride = rig.Boat.gameObject.AddComponent<HullTideRide>();
            ride.Configure(0f, .3f);
            var go = new GameObject("Grounded berth ladder");
            var arrivalGo = new GameObject("Grounded arrival");
            try
            {
                go.transform.position = rig.Player.position;
                var ladder = go.AddComponent<WharfLadder>();
                ladder.Configure("ladder.ground_fixture", 3.4f, 180f);
                ladder.ConfigureClimbTop(NineMileCreekGroundPlan.LadderClimbTopScreenOffset);
                Assert.AreEqual(aground, ride.IsAgroundNow());
                float waterline = TidalRide.Waterline(water, bed, .3f);
                Assert.AreEqual(waterline, ((IHullTideRide)ride).WaterlineNow(), .0001f);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                float deckHeight = (float)typeof(ControlSwitcher).GetMethod("BoatDeckElevationNow", flags)
                    .Invoke(rig.Controls, null);
                Assert.AreEqual(waterline, deckHeight, .0001f, "this fixture has no authored freeboard");
                var arrival = arrivalGo.AddComponent<ArrivalOpening>();
                arrival.enabled = false;
                void Set(string field, object value) => typeof(ArrivalOpening).GetField(field, flags).SetValue(arrival, value);
                Set("_phase", ArrivalOpening.Phase.Moored);
                Set("_mooredTimer", 100f);
                Set("_player", rig.Player);
                Set("_boatRoot", rig.Boat);
                Set("_stepAshore", ladder.WorldPosition);
                Assert.IsTrue(arrival.StepAshore());
                float gap = (float)typeof(ArrivalOpening).GetField("_stepGap", flags).GetValue(arrival);
                float freeboard = (float)typeof(ArrivalOpening).GetField("DeckFreeboardMetres",
                    BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
                Assert.AreEqual(Mathf.Max(0f, ladder.TopElevationMeters - waterline - freeboard), gap, .0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(arrivalGo);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private sealed class HopSave : ISaveService
        {
            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private sealed class HopRig : IDisposable
        {
            private readonly List<UnityEngine.Object> _owned = new();
            public readonly ControlSwitcher Controls;
            public readonly PlayerWalkController Walk;
            public readonly Transform Player;
            public readonly Transform Boat;
            public readonly HopSave Save = new();

            public void Own(UnityEngine.Object item) => _owned.Add(item);

            public GameObject Go(string name, Vector3 at)
            {
                var go = new GameObject(name);
                go.transform.position = at;
                _owned.Add(go);
                return go;
            }

            public HopRig()
            {
                GameServices.Reset();
                InteractionGate.Reset();
                BoardingLadders.Clear();
                StandableSurfaces.Clear();
                Save.Current.OwnedBoats.Add("boat.dory");
                RepairLedger.MarkRepaired(Save.Current, "boat.dory");
                GameServices.Save = Save;
                var player = Go("Hop fisher", new Vector3(0f, -11.5f, 0f));
                Player = player.transform;
                Walk = player.AddComponent<PlayerWalkController>();
                player.AddComponent<DeckWalkController>().enabled = false;
                var boat = Go("Hop hull", new Vector3(0f, -13.8f, 0f));
                Boat = boat.transform;
                var controller = boat.AddComponent<BoatController>();
                var hull = ScriptableObject.CreateInstance<BoatHullDef>();
                hull.Id = "boat.dory";
                hull.CameraWorldHeightMeters = 14f;
                _owned.Add(hull);
                controller.SetHull(hull);
                controller.enabled = false;
                var zone = Go("Hop dock", new Vector3(0f, -12f, 0f));
                var landing = Go("Hop landing", new Vector3(0f, -10.5f, 0f));
                Controls = Go("Hop controls", Vector3.zero).AddComponent<ControlSwitcher>();
                Controls.Configure(Walk, controller, null, zone.transform, 3f, landing.transform);
                Controls.ConfigureHelm(new Vector2(10f, 10f), .05f);
                Controls.ConfigureBoardingMove(true, 3f, .8f, .5f, .35f);
            }

            public void Dispose()
            {
                for (int i = _owned.Count - 1; i >= 0; i--)
                    if (_owned[i] != null) UnityEngine.Object.DestroyImmediate(_owned[i]);
                InteractionGate.Reset();
                BoardingLadders.Clear();
                StandableSurfaces.Clear();
                GameServices.Reset();
            }
        }

        private static void AssertHopSettled(HopRig rig, Vector3 expected, ControlMode mode)
        {
            Assert.IsFalse(rig.Controls.IsBoardingMove);
            Assert.IsFalse(InteractionGate.IsBlocked);
            Assert.AreEqual(mode, rig.Controls.Mode);
            Assert.AreEqual(mode == ControlMode.OnFoot, rig.Walk.enabled);
            for (int i = 0; i < 8; i++)
            {
                rig.Controls.TickBoardingMove(.2f);
                Assert.Less(Vector3.Distance(expected, rig.Player.position), .0001f,
                    "a completed hop cannot resume a route in the region it left");
            }
            TimedBoarding.Complete(rig.Controls);
        }

        [Test]
        public void AHopMidBoardingKeepsTheCoordinatorsNewLanding()
        {
            using var rig = new HopRig();
            Assert.IsTrue(rig.Controls.BeginInteract());
            rig.Controls.TickBoardingMove(.2f);
            Assert.IsTrue(rig.Controls.IsBoardingMove);
            Vector3 landing = new Vector3(180f, 240f, 0f);
            rig.Player.position = landing;
            rig.Controls.ReassertControlMode();
            AssertHopSettled(rig, landing, ControlMode.OnFoot);
        }

        [Test]
        public void AHopDuringALiveReturnKeepsTheCoordinatorsNewLanding()
        {
            using var rig = new HopRig();
            // The repair gate is opt-in; this case exercises an enabled gate.
            var config = ScriptableObject.CreateInstance<GameConfig>();
            rig.Own(config);
            config.RequireBoatRepairsBeforeBoarding = true;
            GameServices.Config = config;
            Assert.IsTrue(rig.Controls.BeginInteract());
            rig.Controls.TickBoardingMove(.2f);
            rig.Save.Current.RepairedBoats.Remove("boat.dory");
            Assert.IsFalse(rig.Controls.BoardableNow());
            rig.Controls.TickBoardingMove(.01f);
            Assert.IsTrue((bool)typeof(ControlSwitcher).GetField("_moveReturning",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rig.Controls));
            Vector3 landing = new Vector3(-170f, 210f, 0f);
            rig.Player.position = landing;
            rig.Controls.ReassertControlMode();
            AssertHopSettled(rig, landing, ControlMode.OnFoot);
        }

        [Test]
        public void AHopMidDisembarkSettlesOnTheDecksBoardSpot()
        {
            using var rig = new HopRig();
            Assert.IsTrue(TimedBoarding.BeginInteract(rig.Controls));
            Assert.AreEqual(ControlMode.OnDeck, rig.Controls.Mode);
            Vector3 boardSpot = rig.Player.position;
            Assert.IsTrue(rig.Controls.BeginInteract());
            rig.Controls.TickBoardingMove(.2f);
            Assert.IsTrue(rig.Controls.IsBoardingMove);
            Vector3 regionShift = new Vector3(200f, 200f, 0f);
            rig.Boat.position += regionShift;
            rig.Player.position = new Vector3(150f, 270f, 0f);
            rig.Controls.ReassertControlMode();
            AssertHopSettled(rig, boardSpot + regionShift, ControlMode.OnDeck);
        }

        [Serializable] private sealed class Paper { public Clips clips; }
        [Serializable] private sealed class Clips { public Clip ladderDown; }
        [Serializable] private sealed class Clip { public Pin[] pins; }
        [Serializable] private sealed class Pin { public float u; public Ladder ladder; }
        [Serializable] private sealed class Ladder { public float descend; public float rung; }

        [Test]
        public void TheMeshRootMotionMatchesThePublishedContactPins()
        {
            var method = typeof(LadderBoardingMath).GetMethod("MeshDescendMetresAt",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(method, "the active mesh needs its own root-motion contract");
            string path = Path.Combine(Application.dataPath, "../docs/art/rigs/character/rig10/gameplay/characterIsoRig10.fisher.gameplay.json");
            var paper = JsonUtility.FromJson<Paper>(File.ReadAllText(path));
            Assert.AreEqual(10, paper.clips.ladderDown.pins.Length);
            foreach (var pin in paper.clips.ladderDown.pins)
                Assert.AreEqual(pin.ladder.descend,
                    (float)method.Invoke(null, new object[] { (double)pin.u, pin.ladder.rung }), .0001f,
                    $"mesh contact frame at phase {pin.u}");
        }

        [TestCase("south alongside", "StPeters", "wharf.st_peters.ladder_0", "required")]
        [TestCase("return berth", "StPeters", "wharf.st_peters.ladder_0", "required")]
        [TestCase("arrival passenger", "StPeters", "wharf.st_peters.ladder_0", "required")]
        [TestCase("north dory: deferred to K0 session", "StPeters", "", "deferred")]
        // NMC H5: the wharf's register gives every wall berth a ladder of its own, on its registered id. The old
        // Ladder_0 between berths is retired with its id; the player's berth ladder keeps its place under the
        // registered id.
        [TestCase("player berth 0", "NineMileCreek", "ladder.nmc_berth0", "required")]
        [TestCase("wall 1", "NineMileCreek", "ladder.nmc_berth1", "resident")]
        [TestCase("wall 2", "NineMileCreek", "ladder.nmc_berth2", "resident")]
        [TestCase("wall 3", "NineMileCreek", "ladder.nmc_berth3", "resident")]
        [TestCase("wall 4", "NineMileCreek", "ladder.nmc_berth4", "resident")]
        [TestCase("wall 5", "NineMileCreek", "ladder.nmc_berth5", "resident")]
        [TestCase("float 0", "NineMileCreek", "", "float")]
        [TestCase("float 1", "NineMileCreek", "", "float")]
        [TestCase("float 2", "NineMileCreek", "", "float")]
        [TestCase("float 3", "NineMileCreek", "", "float")]
        [TestCase("float 4", "NineMileCreek", "", "float")]
        [TestCase("float 5", "NineMileCreek", "", "float")]
        [TestCase("float 6", "NineMileCreek", "", "float")]
        [TestCase("float 7", "NineMileCreek", "", "float")]
        [TestCase("face", "RicePoint", "", "not built")]
        [TestCase("end", "RicePoint", "", "not built")]
        [TestCase("inner", "RicePoint", "", "not built")]
        [TestCase("stage 2 float", "RicePoint", "", "future growth")]
        [TestCase("stage 3 end", "RicePoint", "", "future growth")]
        [TestCase("stage 3 deep", "RicePoint", "", "future growth")]
        [TestCase("T-head outer", "Cumberland", "", "not built")]
        [TestCase("main west slip 1", "Cumberland", "", "not built")]
        [TestCase("main west slip 2", "Cumberland", "", "not built")]
        [TestCase("main west slip 3", "Cumberland", "", "not built")]
        [TestCase("main west slip 4", "Cumberland", "", "not built")]
        [TestCase("main east", "Cumberland", "", "not built")]
        [TestCase("fuel", "Cumberland", "", "not built")]
        [TestCase("east west slip 1", "Cumberland", "", "not built")]
        [TestCase("east west slip 2", "Cumberland", "", "not built")]
        [TestCase("east west slip 3", "Cumberland", "", "not built")]
        [TestCase("east west slip 4", "Cumberland", "", "not built")]
        [TestCase("bar float east", "Cumberland", "", "not built")]
        [TestCase("Coddle Cove dock: reachable legacy landing", "Greybox", "", "legacy landing")]
        [TestCase("public wharf: no shipped inbound passage", "Greywick", "", "unreachable")]
        public void ALadderServesEveryBerth(string berth, string scene, string ladder, string disposition)
        {
            string text = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scenes/" + scene + ".unity"));
            if (!string.IsNullOrEmpty(ladder))
            {
                StringAssert.Contains("_id: " + ladder, text, berth);
                var yaml = StPetersLayerRefresh.SceneYaml.Parse(text.Replace("\r", ""));
                var component = yaml.Docs.Single(d => d.ClassId == 114 && d.Field("_id") == ladder);
                var go = yaml.Require(component.FieldRef("m_GameObject"), berth);
                string position = yaml.TransformOf(go).Field("m_LocalPosition");
                float Number(string key) => float.Parse(Regex.Match(position,
                    key + @": ([\d.eE+-]+)").Groups[1].Value, CultureInfo.InvariantCulture);
                Vector2 at = new Vector2(Number("x"), Number("y"));
                if (disposition == "required")
                {
                    Vector2 shore = scene == "NineMileCreek"
                        ? NineMileCreekGroundPlan.DisembarkPos : (Vector2)StPetersBuilder.DisembarkPos;
                    Assert.LessOrEqual(Vector2.Distance(shore, at),
                        LadderBoardingSettings.Default.LadderReachMetres, berth + ": banked ladder is out of reach");
                }
            }
            else
                TestContext.WriteLine($"{scene}: {berth}: explicitly {disposition}; no ladder authored by this change");
            if (scene == "NineMileCreek" && disposition == "required")
                Assert.LessOrEqual(Vector2.Distance(NineMileCreekGroundPlan.DisembarkPos,
                    NineMileCreekGroundPlan.BerthLadderPos), LadderBoardingSettings.Default.LadderReachMetres);
        }

        [TestCase(-2.2f)]
        [TestCase(0f)]
        [TestCase(2.2f)]
        public void TheFloatAndTheCurrentDoryStayWithinTheStepClampAtEveryTide(float tide)
        {
            string text = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scenes/NineMileCreek.unity"));
            var yaml = StPetersLayerRefresh.SceneYaml.Parse(text.Replace("\r", ""));
            // NMC H5 retired the one old float (wharf.nine_mile_creek.float) for the wharf's four registered floats:
            // the same step, held on each of them.
            var floats = yaml.Docs.Where(d => d.ClassId == 114 && d.IsScript("HiddenHarbours.World.FloatingPlatform")).ToArray();
            CollectionAssert.AreEquivalent(
                new[] { "float.nmc_working", "float.nmc_finger_a", "float.nmc_finger_b", "float.nmc_recreation" },
                floats.Select(f => f.Field("_id")), "the wharf's four registered floats");
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/Dory.asset");
            var deck = AssetDatabase.LoadAssetAtPath<BoatDeckDef>("Assets/_Project/Data/Boats/Decks/DoryIso.asset");
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            foreach (var floating in floats)
            {
                float Read(string key) => float.Parse(floating.Field(key), CultureInfo.InvariantCulture);
                float floatTop = HiddenHarbours.World.FloatingPlatform.DeckElevation(tide,
                    Read("_bedElevation"), Read("_draughtMetres"), Read("_freeboardMetres"));
                foreach (var area in deck.Areas.Where(a => a.Kind == DeckAreaKind.Deck))
                    foreach (Vector2 point in area.Outline)
                    {
                        float height = DeckAreaMath.HeightAt(area.HeightPlane, point);
                        float boatDeck = tide + MooringLineMath.CleatHeightAboveWaterline(height, hull.DraughtMeters);
                        Assert.LessOrEqual(Mathf.Abs(floatTop - boatDeck), config.LadderBoarding.BoardClampMetres,
                            $"{floating.Field("_id")}: her deck at {floatTop:0.000} m and the dory's at {boatDeck:0.000} m, tide {tide:0.0}");
                    }
            }
        }
    }
}
