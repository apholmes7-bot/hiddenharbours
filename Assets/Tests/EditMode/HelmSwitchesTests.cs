using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public class HelmSwitchesContractTests
    {
        [Test]
        public void TheAnchorAnswersTheLampContractOnTheHullRoot()
            => Assert.IsTrue(typeof(IVesselWay).IsAssignableFrom(typeof(BoatAnchor)));

        [Test]
        public void TheIceBoxAnswersTheSharedInteractVerb()
            => Assert.IsTrue(typeof(IInteractable).IsAssignableFrom(typeof(DeckIceBox)));

        [Test]
        public void LHasNoLidBindingOrLidMutationInTheIceBoxUpdate()
        {
            // EditMode cannot reliably synthesize keyboard edges. Pin the removed binding and
            // the whole Update body instead; the actual key journey belongs to the night slot.
            string source = File.ReadAllText("Assets/_Project/Code/Boats/DeckIceBox.cs");
            Assert.IsFalse(source.Contains("_devLidKey"), "the old lid binding must be removed");
            Assert.IsFalse(source.Contains("Key.L"), "L belongs to the lights");
            int start = source.IndexOf("private void Update()", StringComparison.Ordinal);
            int end = source.IndexOf("public void TickProtection()", start, StringComparison.Ordinal);
            string update = source.Substring(start, end - start);
            StringAssert.DoesNotContain("SetLid(", update);
            StringAssert.Contains("_devAddIceKey", update, "I still adds dev ice");
        }
    }

    public class HelmSwitchesTests
    {
        private readonly List<Object> _objects = new();
        private sealed class Sea : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 1;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double t) => Level;
            public float WaterLevelAt(double t) => Level;
        }
        private sealed class Bottom : ITidalTerrain
        {
            public float ElevationAt(Vector2 p) => -2f;
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            GameServices.Environment = new Sea();
            GameServices.TidalTerrain = new Bottom();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            Interactables.Clear();
            InteractVerb.Reset();
            GameServices.Reset();
        }

        private GameObject Go(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }
        private T Def<T>() where T : ScriptableObject
        {
            var def = ScriptableObject.CreateInstance<T>();
            _objects.Add(def);
            return def;
        }
        private static void Field(object obj, string name, object value)
            => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);

        private (BoatController boat, BoatAnchor anchor, BoatLamps lamps, SceneLight[] lights) Rig()
        {
            var root = Go("Hull");
            var boat = root.AddComponent<BoatController>();
            var hull = Def<BoatHullDef>();
            hull.Id = "boat.test";
            hull.HasAnchor = true;
            hull.RodeMeters = 6f;
            hull.DraughtMeters = 0.3f;
            boat.SetHull(hull);
            var anchor = root.AddComponent<BoatAnchor>();
            anchor.Configure(boat);
            var (lamps, lights) = Lamps(root.transform);
            return (boat, anchor, lamps, lights);
        }

        private (BoatLamps lamps, SceneLight[] lights) Lamps(Transform root)
        {
            var visual = Go("RebuiltVisual");
            visual.transform.SetParent(root, false);
            var lamps = visual.AddComponent<BoatLamps>();
            var kinds = new[] { HullLampKind.AnchorLight, HullLampKind.PortSidelight,
                HullLampKind.StarboardSidelight, HullLampKind.SternLight, HullLampKind.Masthead };
            var mounts = new HullLamp[kinds.Length];
            var lights = new SceneLight[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                mounts[i] = new HullLamp(kinds[i], Vector3.zero);
                var child = Go(kinds[i].ToString());
                child.transform.SetParent(visual.transform, false);
                lights[i] = child.AddComponent<SceneLight>();
            }
            Field(lamps, "_lamps", mounts);
            Field(lamps, "_lights", lights);
            lamps.RefreshWay();
            return (lamps, lights);
        }

        private static void AssertLights(BoatLamps lamps, SceneLight[] lights, bool anchored)
        {
            Assert.AreEqual(anchored ? VesselWay.Moored : VesselWay.UnderWay, lamps.Way);
            Assert.AreEqual(anchored, lights[0].enabled, "anchor light");
            for (int i = 1; i < lights.Length; i++)
                Assert.AreEqual(!anchored, lights[i].enabled, "under-way lamp " + i);
        }

        [Test]
        public void DroppingAHoldingAnchorPushesAnchorLightAndExtinguishesSidelights()
        {
            var (_, anchor, lamps, lights) = Rig();
            AssertLights(lamps, lights, false);
            Assert.AreEqual(AnchorDrop.Set, anchor.TryDrop());
            Assert.IsTrue(anchor.IsHolding);
            AssertLights(lamps, lights, true); // no RefreshWay: the transition must push this frame
        }

        [Test]
        public void WeighingPushesSidelightsBackAfterTheAnchorLight()
        {
            var (_, anchor, lamps, lights) = Rig();
            anchor.TryDrop();
            AssertLights(lamps, lights, true); // baseline must fail here, not pass on default UnderWay
            anchor.Weigh();
            AssertLights(lamps, lights, false);
        }

        [Test]
        public void AVisualRebuiltWhileAtAnchorReadsTheAnchorLight()
        {
            var (boat, anchor, _, _) = Rig();
            anchor.TryDrop();
            // The re-skin service mounts the lamps on a new visual under the same physics root.
            // Drive OnEnable explicitly in EditMode, where the play lifecycle is not guaranteed.
            foreach (string mesh in new[] { "CapeIslanderIsoHullMesh", "LobsterBoatIsoHullMesh" })
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(
                    "Assets/_Project/Data/Boats/HullMeshes/" + mesh + ".asset");
                Assert.NotNull(def);
                var host = Go(mesh);
                host.transform.SetParent(boat.transform, false);
                Assert.NotNull(new IsoFacetHullPresentationService().Install(host, def));
                var lamps = host.GetComponent<BoatLamps>();
                Assert.NotNull(lamps);
                typeof(BoatLamps).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(lamps, null);
                Assert.AreEqual(VesselWay.Moored, lamps.Way);
                bool hasAnchor = false;
                for (int i = 0; i < lamps.Lamps.Length; i++)
                {
                    HullLampKind kind = lamps.Lamps[i].Kind;
                    if (kind == HullLampKind.AnchorLight)
                    {
                        hasAnchor = true;
                        Assert.IsTrue(lamps.Lights[i].enabled);
                    }
                    if (kind == HullLampKind.PortSidelight || kind == HullLampKind.StarboardSidelight)
                        Assert.IsFalse(lamps.Lights[i].enabled);
                }
                Assert.IsTrue(hasAnchor, "the committed hull must actually carry an anchor lamp");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BothRootProvidersAgreeAtABerthAndWhileTheArrivalAnchors(bool anchorFirst)
        {
            var root = Go("Both providers");
            root.SetActive(false); // no berth art or warnings required to test the declaration
            var boat = root.AddComponent<BoatController>();
            var hull = Def<BoatHullDef>();
            hull.HasAnchor = true; hull.RodeMeters = 6f; hull.DraughtMeters = 0.3f;
            boat.SetHull(hull);
            BoatAnchor anchor;
            MooredBoat berth;
            if (anchorFirst) { anchor = root.AddComponent<BoatAnchor>(); berth = root.AddComponent<MooredBoat>(); }
            else { berth = root.AddComponent<MooredBoat>(); anchor = root.AddComponent<BoatAnchor>(); }
            anchor.Configure(boat);
            var answer = (object)anchor as IVesselWay;
            Assert.NotNull(answer, "the player's anchor must itself answer, even on a root with a berth");
            Assert.AreEqual(VesselWay.Moored, answer.Way);
            anchor.TryDrop(); anchor.Weigh();
            Assert.AreEqual(VesselWay.Moored, root.GetComponent<IVesselWay>().Way);
            berth.SetWay(VesselWay.UnderWay);
            Assert.AreEqual(VesselWay.UnderWay, answer.Way);
            anchor.TryDrop();
            berth.SetWay(VesselWay.UnderWay);
            Assert.AreEqual(VesselWay.Moored, berth.Way);
            Assert.AreEqual(berth.Way, answer.Way);
            anchor.Weigh();
            Assert.AreEqual(VesselWay.UnderWay, answer.Way);
        }

        [Test]
        public void LosingAndRegainingTheBottomPushesTheLampRegime()
        {
            var (_, anchor, lamps, lights) = Rig();
            anchor.TryDrop();
            AssertLights(lamps, lights, true);
            var sea = (Sea)GameServices.Environment;
            var tick = typeof(BoatAnchor).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
            sea.Level = 8f;
            tick.Invoke(anchor, null);
            Assert.AreEqual(AnchorState.Dragging, anchor.State);
            AssertLights(lamps, lights, false);
            sea.Level = 0f;
            tick.Invoke(anchor, null);
            AssertLights(lamps, lights, true);
        }

        private (DeckIceBox box, BoatController boat, GameObject player) Box()
        {
            var root = Go("Ice boat");
            var boat = root.AddComponent<BoatController>();
            var hull = Def<BoatHullDef>();
            hull.DeckContainer = Def<DeckContainerDef>();
            boat.SetHull(hull);
            var hold = root.AddComponent<ShipHold>();
            hold.SetHull(hull);
            var box = root.AddComponent<DeckIceBox>();
            box.Configure(hold);
            var player = Go("Player");
            player.transform.SetParent(root.transform, false);
            GameServices.PlayerTransform = player.transform;
            return (box, boat, player);
        }
        private static IInteractable Candidate(DeckIceBox box)
        {
            var candidate = (object)box as IInteractable;
            Assert.NotNull(candidate, "the box must offer the shared deck verb");
            return candidate;
        }

        private sealed class OtherFixture : IInteractable
        {
            public string Id => "fixture.other";
            public string VerbLabel => "Other work";
            public Vector2 WorldPosition => new Vector2(0.5f, 0f);
            public float ReachMeters => 1f;
            public int Priority { get; set; }
            public InteractContext Contexts => InteractContext.OnDeck;
            public bool RequiresFacing => false;
            public bool IsAvailable => true;
            public void Interact(in InteractActor actor) { }
        }

        [Test]
        public void TheFuelTankRegistersForTheSharedVerbAndRelinquishesItWhenDisabled()
        {
            var tank = Go("Tank fixture").AddComponent<BoatFuelTank>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            // EditMode does not run the player's component lifecycle. Drive those exact callbacks,
            // without inserting the tank into the registry on the test's behalf.
            typeof(BoatFuelTank).GetMethod("OnEnable", flags).Invoke(tank, null);
            CollectionAssert.Contains(Interactables.Active, tank);
            typeof(BoatFuelTank).GetMethod("OnDisable", flags).Invoke(tank, null);
            CollectionAssert.DoesNotContain(Interactables.Active, tank);
            typeof(BoatFuelTank).GetMethod("OnEnable", flags).Invoke(tank, null);
            CollectionAssert.Contains(Interactables.Active, tank);
        }

        [Test]
        public void InteractTogglesTheLidWithAPromptAndOnlyOnHerOwnDeck()
        {
            var (box, _, player) = Box();
            var candidate = Candidate(box);
            Interactables.Register(candidate);
            var actor = new InteractActor(Vector2.zero, Vector2.zero, InteractContext.OnDeck);
            Assert.AreEqual("Close the ice box", candidate.VerbLabel);
            Assert.IsTrue(InteractVerb.TryPerform(actor, 180f));
            Assert.IsTrue(box.LidOn);
            Assert.AreEqual("Open the ice box", candidate.VerbLabel);
            Assert.IsTrue(InteractVerb.TryPerform(actor, 180f));
            Assert.IsFalse(box.LidOn);
            player.transform.SetParent(null);
            Assert.IsFalse(candidate.IsAvailable);
            Assert.IsFalse(InteractVerb.TryPerform(actor, 180f));
        }

        [Test]
        public void TheLidYieldsToDoorAndFuelPrioritiesEvenWhenTheBoxIsCloser()
        {
            var (box, _, _) = Box();
            var candidate = Candidate(box);
            Assert.Less(candidate.Priority, InteractPriority.Fixture, "cabin door");
            Assert.Less(candidate.Priority, InteractPriority.ToolTarget, "fuel tank with can held");
            var deck = new InteractActor(Vector2.zero, Vector2.zero, InteractContext.OnDeck);
            foreach (int priority in new[] { InteractPriority.Fixture, InteractPriority.ToolTarget })
            {
                var other = new OtherFixture { Priority = priority };
                Assert.IsTrue(InteractResolver.TryResolve(new[] { candidate, other }, deck, 180f, out var winner));
                Assert.AreSame(other, winner, "the nearer lid must yield to the door or fuel work");
            }
            var actor = new InteractActor(Vector2.zero, Vector2.zero, InteractContext.AtHelm);
            Assert.IsFalse(InteractResolver.TryResolve(new[] { candidate }, actor, 180f, out _));
            Assert.IsFalse(box.LidOn);
        }

        [Test]
        public void TheBoxWorksAwayFromTheHelmButNeverTakesItsPress()
        {
            var (box, boat, player) = Box();
            var candidate = Candidate(box);
            var walk = player.AddComponent<PlayerWalkController>();
            player.AddComponent<DeckWalkController>();
            var input = boat.gameObject.AddComponent<DevBoatInput>();
            var dock = Go("Dock");
            var sw = Go("Switcher").AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dock.transform, 3f, dock.transform);
            sw.ConfigureHelm(new Vector2(0f, 0.5f), 0.2f);
            sw.ConfigureInteractVerb(true, 180f);
            Assert.IsTrue(sw.TryInteract());
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode);
            Interactables.Register(candidate);
            player.transform.position = candidate.WorldPosition;
            Assert.IsFalse(sw.TakesTheHelmOnThisPress());
            Assert.IsTrue(sw.BeginInteract());
            Assert.IsTrue(box.LidOn);
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode);
            player.transform.position = sw.HelmWorldPosition;
            Assert.IsTrue(sw.TakesTheHelmOnThisPress());
            Assert.IsTrue(sw.BeginInteract());
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            Assert.IsTrue(box.LidOn, "taking the helm must not toggle the lid again");
        }
    }
}
