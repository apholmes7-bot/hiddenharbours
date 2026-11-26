using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A boat's cabin lights have a switch</b> (owner 2026-11-25: "boat cabin lights need a switch at
    /// the helm", and "larger yachts also get a physical light switch if the helm is a far distance
    /// from the player's entry door"). Pins the layout rule (<see cref="CabinLightSwitchRule"/>), the one
    /// state both switches throw (<see cref="CabinLightSwitchboard"/>), the player's-boat-only offer
    /// (<see cref="CabinLightSwitch"/>), the installer building them off the committed data, and that
    /// every shipped boat with a cabin resolves a helm switch.
    /// </summary>
    public class CabinLightSwitchTests
    {
        const float Far = 2.5f;
        const string GameConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const string CapeInteriorPath = "Assets/_Project/Data/Boats/Interiors/CapeIslanderIso.asset";
        const string MotorYachtInteriorPath = "Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset";

        static readonly Vector3 Helm = new Vector3(0f, 1.35f, 0.74f);

        readonly List<Object> _spawned = new List<Object>();
        readonly List<CabinLightSwitchThrown> _thrown = new List<CabinLightSwitchThrown>();
        System.Action<CabinLightSwitchThrown> _onThrown;
        GameConfig _previousConfig;
        CabinLightSwitchSpot[] _spots;

        sealed class FakeOutlet : ICabinLightsOutlet
        {
            public bool Lit;
            public bool Known = true;
            public readonly List<CabinLightSwitchSetting> Applied = new List<CabinLightSwitchSetting>();

            public bool TryReadLit(out bool lit) { lit = Lit; return Known; }
            public void Apply(CabinLightSwitchSetting setting) => Applied.Add(setting);
        }

        [SetUp]
        public void SetUp()
        {
            _previousConfig = GameServices.Config;
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(config);
            GameServices.Config = config;
            GameServices.Helm.SetPlayersBoat(null);

            _spots = new CabinLightSwitchSpot[CabinLightSwitchRule.MaxSwitchesPerBoat];
            _thrown.Clear();
            _onThrown = _thrown.Add;
            EventBus.Subscribe(_onThrown);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_onThrown);
            GameServices.Helm.SetPlayersBoat(null);
            GameServices.Config = _previousConfig;

            // The installer test's cutaway subscribes in Configure and its OnDestroy never runs in
            // EditMode: clear what it joined, as BoatBunkTests does.
            EventBus.Clear<CabinEntered>();
            EventBus.Clear<CabinEntryLandingEntered>();
            EventBus.Clear<CabinLeft>();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<CabinLightSwitchThrown>();
            Interactables.Clear();

            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
            BoatInteriorCells.Reset();
        }

        // =====================================================================================
        //  the layout rule
        // =====================================================================================

        [Test]
        public void AHelmNearTheDoor_GetsOneSwitch_AtTheHelm()
        {
            Vector3 door = Helm + new Vector3(0f, -1f, 0f);
            int n = CabinLightSwitchRule.Plan(null, true, Helm, true, door, Far, _spots);

            Assert.AreEqual(1, n);
            Assert.AreEqual(CabinLightSwitchRole.Helm, _spots[0].Role);
            Assert.AreEqual(CabinLightSwitchRule.HelmSpotId, _spots[0].Id);
            Assert.AreEqual(Helm, _spots[0].LocalMetres);
        }

        [Test]
        public void AHelmFarFromTheDoor_GetsTwoSwitches_HelmAndDoor()
        {
            Vector3 door = Helm + new Vector3(0f, -3f, -1f);   // 3.16 m: over 2.5
            int n = CabinLightSwitchRule.Plan(null, true, Helm, true, door, Far, _spots);

            Assert.AreEqual(2, n);
            Assert.AreEqual(CabinLightSwitchRole.Helm, _spots[0].Role);
            Assert.AreEqual(CabinLightSwitchRole.Door, _spots[1].Role);
            Assert.AreEqual(door, _spots[1].LocalMetres);
        }

        [Test]
        public void TheThreshold_IsStraightLine_AndStrictlyMore()
        {
            Assert.IsFalse(CabinLightSwitchRule.IsFar(Vector3.zero, new Vector3(0f, 0f, 2.5f), Far), "exactly at it is near");
            Assert.IsTrue(CabinLightSwitchRule.IsFar(Vector3.zero, new Vector3(0f, 0f, 2.51f), Far), "height counts: a flybridge is far");
        }

        [Test]
        public void AnAuthoredList_WinsOverTheRule()
        {
            var authored = new[]
            {
                new CabinLightSwitchPlacement { Id = "saloon", Role = CabinLightSwitchRole.Door, LocalMetres = new Vector3(1f, 2f, 3f) },
            };
            Vector3 door = Helm + new Vector3(0f, -10f, 0f);   // far: the rule would place two
            int n = CabinLightSwitchRule.Plan(authored, true, Helm, true, door, Far, _spots);

            Assert.AreEqual(1, n, "her own list replaces the rule outright");
            Assert.AreEqual("saloon", _spots[0].Id);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), _spots[0].LocalMetres);
        }

        [Test]
        public void AnAuthoredListWithNoUsableEntry_FallsBackToTheRule()
        {
            var authored = new[] { new CabinLightSwitchPlacement { Id = "" }, null };
            int n = CabinLightSwitchRule.Plan(authored, true, Helm, false, Vector3.zero, Far, _spots);
            Assert.AreEqual(1, n);
            Assert.AreEqual(CabinLightSwitchRule.HelmSpotId, _spots[0].Id);
        }

        [Test]
        public void NoHelm_NoSwitches()
        {
            Assert.AreEqual(0, CabinLightSwitchRule.Plan(null, false, Vector3.zero, true, Helm, Far, _spots));
        }

        [Test]
        public void APress_SwapsOnAndOff_AndFromAutoFlipsWhatIsShowing()
        {
            Assert.AreEqual(CabinLightSwitchSetting.Off, CabinLightSwitchRule.Toggled(CabinLightSwitchSetting.On, true, true));
            Assert.AreEqual(CabinLightSwitchSetting.On, CabinLightSwitchRule.Toggled(CabinLightSwitchSetting.Off, true, false));
            Assert.AreEqual(CabinLightSwitchSetting.Off, CabinLightSwitchRule.Toggled(CabinLightSwitchSetting.Auto, true, true), "lit now: off");
            Assert.AreEqual(CabinLightSwitchSetting.On, CabinLightSwitchRule.Toggled(CabinLightSwitchSetting.Auto, true, false), "dark now: on");
            Assert.AreEqual(CabinLightSwitchSetting.On, CabinLightSwitchRule.Toggled(CabinLightSwitchSetting.Auto, false, false), "unknown reads dark: on");
        }

        // =====================================================================================
        //  the one state
        // =====================================================================================

        [Test]
        public void PressingEitherSwitch_TogglesTheOneState_AndRaisesOneEventEach()
        {
            CabinLightSwitchboard board = Boat("Mine", out CabinLightSwitch helm, out CabinLightSwitch door);
            GameServices.Helm.SetPlayersBoat(board.gameObject);

            Assert.IsTrue(helm.IsAvailable && door.IsAvailable, "both are offered aboard his boat");
            Assert.AreEqual(CabinLightSwitch.VerbOn, helm.VerbLabel);

            Assert.IsTrue(helm.Press(), "the helm switch turns them on");
            Assert.AreEqual(CabinLightSwitchSetting.On, board.Setting);
            Assert.AreEqual(1, _thrown.Count, "one press, one event");
            Assert.IsTrue(_thrown[0].On);
            Assert.AreEqual(helm.Id, _thrown[0].SwitchId);
            Assert.AreEqual(board.gameObject.GetEntityId(), _thrown[0].HullId);
            Assert.AreEqual(CabinLightSwitch.VerbOff, door.VerbLabel, "the door switch reads the same state");

            Assert.IsFalse(door.Press(), "the door switch turns the same lights off");
            Assert.AreEqual(CabinLightSwitchSetting.Off, board.Setting);
            Assert.AreEqual(2, _thrown.Count);
            Assert.IsFalse(_thrown[1].On);
            Assert.AreEqual(door.Id, _thrown[1].SwitchId);
        }

        [Test]
        public void APress_IsHandedToTheLights_AndTheFirstFlipsWhatTheyShow()
        {
            CabinLightSwitchboard board = Boat("Lit", out CabinLightSwitch helm, out _);
            GameServices.Helm.SetPlayersBoat(board.gameObject);
            var outlet = new FakeOutlet { Lit = true };
            board.SetOutlet(outlet);
            outlet.Applied.Clear();

            Assert.IsFalse(helm.Press(), "lit under the automatic rule: the first press turns them off");
            CollectionAssert.AreEqual(new[] { CabinLightSwitchSetting.Off }, outlet.Applied);
        }

        [Test]
        public void AnNpcBoat_IsNeverOfferedTheSwitch_AndKeepsItsAutomaticRule()
        {
            CabinLightSwitchboard npc = Boat("Npc", out CabinLightSwitch helm, out CabinLightSwitch door);
            CabinLightSwitchboard mine = Boat("Mine", out _, out _);
            GameServices.Helm.SetPlayersBoat(mine.gameObject);

            Assert.IsFalse(helm.IsAvailable || door.IsAvailable, "not his boat: not offered");
            Assert.IsFalse(helm.Press());
            Assert.AreEqual(CabinLightSwitchSetting.Auto, npc.Setting, "an NPC boat stays on the automatic rule");
            Assert.IsEmpty(_thrown, "nothing thrown, nothing published");
        }

        [Test]
        public void TwoBoats_AreIndependent()
        {
            CabinLightSwitchboard a = Boat("A", out CabinLightSwitch aHelm, out _);
            CabinLightSwitchboard b = Boat("B", out CabinLightSwitch bHelm, out _);

            GameServices.Helm.SetPlayersBoat(a.gameObject);
            aHelm.Press();
            Assert.AreEqual(CabinLightSwitchSetting.On, a.Setting);
            Assert.AreEqual(CabinLightSwitchSetting.Auto, b.Setting, "her neighbour is untouched");

            GameServices.Helm.SetPlayersBoat(b.gameObject);
            bHelm.Press();
            bHelm.Press();
            Assert.AreEqual(CabinLightSwitchSetting.Off, b.Setting);
            Assert.AreEqual(CabinLightSwitchSetting.On, a.Setting);
        }

        [Test]
        public void AHullSwap_KeepsEachBoatsOwnSetting()
        {
            CabinLightSwitchboard board = Boat("Swapper", out CabinLightSwitch helm, out _);
            GameServices.Helm.SetPlayersBoat(board.gameObject);
            board.BeginBoat("boat.first");
            helm.Press();
            Assert.AreEqual(CabinLightSwitchSetting.On, board.Setting);

            board.BeginBoat("boat.second");
            Assert.AreEqual(CabinLightSwitchSetting.Auto, board.Setting, "a boat never pressed starts on Auto");
            board.BeginBoat("boat.first");
            Assert.AreEqual(CabinLightSwitchSetting.On, board.Setting, "back aboard the first, as she was left");
        }

        [Test]
        public void TheQueryPath_DoesNotAllocate()
        {
            CabinLightSwitchboard board = Boat("Quiet", out CabinLightSwitch helm, out _);
            GameServices.Helm.SetPlayersBoat(board.gameObject);
            Query(helm, 8);   // warm

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            Query(helm, 1000);
            long after = System.GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0, after - before, "the chooser asks every frame; it must cost nothing");
        }

        static int Query(CabinLightSwitch s, int times)
        {
            int sink = 0;
            for (int i = 0; i < times; i++)
            {
                if (s.IsAvailable) sink++;
                sink += s.VerbLabel.Length + s.Id.Length + s.Priority + (int)s.Contexts;
                sink += (int)s.WorldPosition.x + (int)s.ReachMeters;
                if (s.RequiresFacing) sink++;
            }
            return sink;
        }

        // =====================================================================================
        //  the shipped data
        // =====================================================================================

        [Test]
        public void EveryShippedCabinBoat_ResolvesAHelmSwitch()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            Assert.IsNotNull(config, "premise: the shipped config");
            float far = config.CabinLightSwitchDoorDistanceMetres;

            BoatVisualDef[] cabins = AssetDatabase.FindAssets("t:BoatVisualDef", new[] { "Assets/_Project/Data/Boats" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<BoatVisualDef>)
                .Where(v => v != null && v.HasInterior())
                .OrderBy(v => v.name, System.StringComparer.Ordinal)
                .ToArray();
            Assert.GreaterOrEqual(cabins.Length, 22, "premise: the kit hulls and the big boats all have cabins");

            var faults = new List<string>();
            var two = new List<string>();
            int one = 0;
            foreach (BoatVisualDef v in cabins)
            {
                int n = CabinLightSwitchRule.PlanFor(v, far, _spots);
                if (n == 0) { faults.Add($"{v.name}: no helm switch (no helm station, no enter_helm anchor, no authored list)"); continue; }
                bool ruled = v.CabinLightSwitches == null || v.CabinLightSwitches.Length == 0;
                if (ruled && _spots[0].Role != CabinLightSwitchRole.Helm)
                    faults.Add($"{v.name}: the rule's first switch is not at the helm");
                var ids = new HashSet<string>(System.StringComparer.Ordinal);
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = _spots[i].LocalMetres;
                    if (float.IsNaN(p.x + p.y + p.z) || float.IsInfinity(p.x + p.y + p.z))
                        faults.Add($"{v.name}: switch '{_spots[i].Id}' is not at a finite point");
                    if (!ids.Add(_spots[i].Id)) faults.Add($"{v.name}: two switches share id '{_spots[i].Id}'");
                }
                if (n >= 2) two.Add(v.name); else one++;
            }

            TestContext.WriteLine($"one switch: {one} boats; two switches: {string.Join(", ", two)}");
            Assert.IsEmpty(faults, string.Join("\n", faults));
            Assert.Greater(one, 0, "premise: the small cabin boats need only the helm switch");
            Assert.Greater(two.Count, 0, "premise: some shipped boat's helm is far from her door");
        }

        [Test]
        public void TheInstaller_BuildsOneSwitchOnTheCapeIslander_AndTwoOnTheMotorYacht()
        {
            BoatInteriorInstaller cape = BuildInstallerFor(AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(CapeInteriorPath), "CapeSwitches");
            BoatInteriorInstaller yacht = BuildInstallerFor(AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(MotorYachtInteriorPath), "YachtSwitches");
            Assert.IsNotNull(cape, "premise: a committed hull wears the cape islander's interior");
            Assert.IsNotNull(yacht, "premise: a committed hull wears the motor yacht's interior");

            Assert.AreEqual(1, cape.LightSwitches.Count, "helm 0.9 m from the door: one switch");
            Assert.AreEqual(CabinLightSwitchRole.Helm, cape.LightSwitches[0].Role);
            Assert.AreEqual(2, yacht.LightSwitches.Count, "helm 11 m from the door: a second by the door");
            Assert.AreEqual(CabinLightSwitchRole.Door, yacht.LightSwitches[1].Role);

            CabinLightSwitchboard board = yacht.GetComponent<CabinLightSwitchboard>();
            Assert.IsNotNull(board, "one board on her root");
            Assert.AreSame(board, yacht.LightSwitches[0].Board);
            Assert.AreSame(board, yacht.LightSwitches[1].Board, "both switches throw the same state");
            Assert.AreNotEqual(yacht.LightSwitches[0].Id, yacht.LightSwitches[1].Id);
        }

        // =====================================================================================
        //  rig
        // =====================================================================================

        /// <summary>A bare root with a board and two switches on it (no cabin: reach alone decides).</summary>
        CabinLightSwitchboard Boat(string name, out CabinLightSwitch helm, out CabinLightSwitch door)
        {
            var root = new GameObject(name);
            _spawned.Add(root);
            var board = root.AddComponent<CabinLightSwitchboard>();
            helm = Switch(root, board, name, CabinLightSwitchRule.HelmSpotId, CabinLightSwitchRole.Helm);
            door = Switch(root, board, name, CabinLightSwitchRule.DoorSpotId, CabinLightSwitchRole.Door);
            return board;
        }

        static CabinLightSwitch Switch(GameObject root, CabinLightSwitchboard board, string boat, string spot,
                                       CabinLightSwitchRole role)
        {
            var go = new GameObject(spot);
            go.transform.SetParent(root.transform, false);
            var s = go.AddComponent<CabinLightSwitch>();
            s.Configure(board, null, CabinLightSwitchRule.FixtureId("interior." + boat, spot), role, Vector3.zero);
            return s;
        }

        /// <summary>The committed hull that wears <paramref name="interior"/>, cabin built by the real
        /// installer (the same stand-up as BoatBunkTests).</summary>
        BoatInteriorInstaller BuildInstallerFor(BoatInteriorDef interior, string name)
        {
            if (interior == null) return null;
            BoatHullDef hull = AssetDatabase.FindAssets("t:BoatHullDef", new[] { "Assets/_Project/Data/Boats" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<BoatHullDef>)
                .FirstOrDefault(h => h != null && h.Visual != null && h.Visual.Interior == interior);
            if (hull == null) return null;

            var root = new GameObject(name);
            _spawned.Add(root);
            var boat = root.AddComponent<BoatController>();
            boat.SetHull(hull);
            boat.enabled = false;
            root.AddComponent<DirectionalBoatSprite>();
            var installer = root.AddComponent<BoatInteriorInstaller>();
            installer.Build();
            Assert.IsTrue(installer.Built, $"{interior.Id}: the installer built her cabin");
            return installer;
        }
    }
}
