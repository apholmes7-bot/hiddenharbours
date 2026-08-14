using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Tools are things you hold</b> — the owner's ruling of 2026-08-13, pinned.
    ///
    /// <para>"Currently the player always has a rod available to use — this should be a carried item like
    /// any item." · "They should need a shovel to dig clams."</para>
    ///
    /// <para>Four claims, and the third is the one that would actually have shipped broken:
    /// <list type="number">
    ///   <item><b>THE GATES</b> — digging needs the shovel IN HAND (owning it is no longer enough), and
    ///   casting needs the rod. Each is sabotage-checked from the other side: turn the gate off and the
    ///   refusal becomes a success, so the test is watching the gate rather than some other condition.</item>
    ///   <item><b>THE WRONG TOOL IS NOT A TOOL</b> — a rod in hand does not dig.</item>
    ///   <item><b>THE PRIORITY INVERSION</b> — with the shovel held, E at a bared hole must DIG, not "put
    ///   the shovel down". A held thing outranks a fixture (20 &gt; 0), so on the obvious wiring the one
    ///   act the shovel exists for is unreachable. <see cref="InteractPriority.ToolTarget"/> is the
    ///   answer and this is the test that proves it — in both directions, because the fix must not cost
    ///   you the ability to put the shovel down at all.</item>
    ///   <item><b>THE CONTENT</b> — the Defs the builder loads exist and carry the exact ids the gates
    ///   match on. A typo there is silent: no tool spawns, and the opening has no way to dig or fish.</item>
    /// </list></para>
    /// </summary>
    public class ToolsInHandTests
    {
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int Capacity = 20;
            public int CapacityUnits => Capacity;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { if (_items.Count >= Capacity) return false; _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation = 1.0f;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level = 0.5f;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private readonly List<Object> _spawned = new();
        private FlatEnv _env;
        private SaveData _save;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractActionClaim.Reset();
            EventBus.Clear<FishCaught>();

            _env = new FlatEnv();
            _save = SaveMigration.NewGame();
            _save.OwnedGear.Add("gear.shovel");     // she OWNS the shovel throughout — that is the point
            _save.OwnedGear.Add("gear.rod");

            GameServices.TidalTerrain = new FlatTerrain();
            GameServices.Environment = _env;
            GameServices.Save = new FakeSaveService(_save);
        }

        [TearDown]
        public void TearDown()
        {
            Interactables.Clear();
            InteractVerb.Reset();
            EventBus.Clear<FishCaught>();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- fixtures --------------------------------------------------------------------------------

        private FishSpeciesDef Clam()
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = "fish.soft_shell_clam"; f.DisplayName = "Soft-shell Clam";
            f.Category = FishCategory.Shellfish;
            f.MinWeightKg = 0.05f; f.MaxWeightKg = 0.2f; f.BaseValue = 2; f.SupplyElasticity = 0.45f;
            _spawned.Add(f);
            return f;
        }

        private ClamDig Hole(IHold bucket, Vector2 at = default, string toolId = ToolDef.ShovelId)
        {
            var go = new GameObject("ClamHole");
            _spawned.Add(go);
            go.transform.position = at;
            var d = go.AddComponent<ClamDig>();
            d.Configure(Clam(), bucket, go.transform, "gear.shovel", seed: 7, reachRadius: 1.25f);
            d.ConfigureInteract($"fixture.clam_hole.{at.x}_{at.y}", toolId);
            return d;
        }

        // ---- 1. THE DIG GATE -------------------------------------------------------------------------

        [Test]
        public void Digging_needs_the_shovel_IN_HAND_not_merely_owned()
        {
            // She owns gear.shovel (SetUp) and stands on a bared hole. Before the ruling that was enough.
            var hold = new FakeHold();
            ClamDig dig = Hole(hold);
            ToolInHand.Empty(_spawned);

            Assert.IsTrue(dig.IsExposedNow(), "precondition: the flat is bared");
            Assert.IsFalse(dig.ShovelInHand(), "her hands are empty");
            Assert.IsFalse(dig.TryDig(), "owning a shovel you left at home does not dig a clam");
            Assert.AreEqual(0, hold.UsedUnits);

            // …and the same hole, the same tide, the same save — with the shovel in her hands.
            ToolInHand.Holding(ToolDef.ShovelId, _spawned);

            Assert.IsTrue(dig.ShovelInHand());
            Assert.IsTrue(dig.TryDig(), "shovel in hand digs");
            // The clam lands IN HER HAND, not in the pail (the third ruling — CatchInHandTests owns that
            // behaviour in full). Asserting the hand is the faithful proof that the dig happened here.
            Assert.AreEqual("fish.soft_shell_clam", GameServices.Hands.Carried.DefId);
            Assert.AreEqual(0, hold.UsedUnits, "…and the pail is untouched until she puts it there");
        }

        [Test]
        public void The_in_hand_gate_is_what_refuses_it_and_nothing_else()
        {
            // SABOTAGE from the other side: the SAME empty-handed dig, with the in-hand gate configured
            // off (an empty tool id = the pre-ruling behaviour). If this did not succeed, the refusal
            // above was coming from some other condition and the gate above proves nothing.
            var hold = new FakeHold();
            ClamDig dig = Hole(hold, Vector2.zero, toolId: null);
            ToolInHand.Empty(_spawned);

            Assert.IsTrue(dig.ShovelInHand(), "no tool id configured = no in-hand gate");
            Assert.IsTrue(dig.TryDig(), "with the gate off, empty hands dig again — so the gate IS the refusal");
        }

        [Test]
        public void A_rod_in_your_hands_is_not_a_shovel()
        {
            var hold = new FakeHold();
            ClamDig dig = Hole(hold);
            ToolInHand.Holding(ToolDef.RodId, _spawned);

            Assert.IsFalse(dig.ShovelInHand(), "holding SOMETHING is not holding the right thing");
            Assert.IsFalse(dig.TryDig());
            Assert.AreEqual(0, hold.UsedUnits);
        }

        [Test]
        public void Owning_the_shovel_still_matters_even_with_one_in_your_hands()
        {
            // The two gates are different facts and both survive: holding is not a way around the shop.
            _save.OwnedGear.Remove("gear.shovel");
            var hold = new FakeHold();
            ClamDig dig = Hole(hold);
            ToolInHand.Holding(ToolDef.ShovelId, _spawned);

            Assert.IsFalse(dig.TryDig(), "a shovel you do not own does not dig");
        }

        // ---- 2. THE HOLE AS A CANDIDATE --------------------------------------------------------------

        [Test]
        public void A_hole_is_only_a_candidate_when_the_shovel_is_held_and_the_flat_is_bared()
        {
            ClamDig dig = Hole(new FakeHold());
            ToolInHand.Empty(_spawned);
            Assert.IsFalse(dig.IsAvailable, "barehanded, there is nothing here to act on");

            ToolInHand.Holding(ToolDef.ShovelId, _spawned);
            Assert.IsTrue(dig.IsAvailable, "shovel in hand on a bared flat — a work-site");

            _env.Level = 2.0f;                       // the tide floods the 1.0 m flat
            Assert.IsFalse(dig.IsAvailable, "under water it is not a work-site, shovel or no shovel");

            _env.Level = 0.5f;
            dig.MarkConsumed();
            Assert.IsFalse(dig.IsAvailable, "a spent hole is not a work-site either");
        }

        // ---- 3. THE PRIORITY INVERSION — the one that would have shipped broken -----------------------

        [Test]
        public void Shovel_in_hand_at_a_bared_hole_the_press_DIGS_rather_than_putting_the_shovel_down()
        {
            // ⚠️ THE TEST THE WHOLE PR TURNS ON. A held tool reports Held (20); a fixture reports 0. Make
            // the hole a plain fixture and this resolves to the shovel, E means "put it down", and digging
            // — the single thing a shovel is for — is unreachable while you are holding one.
            var hold = new FakeHold();
            ClamDig dig = Hole(hold, Vector2.zero);
            Interactables.Register(dig);

            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);
            var shovel = Object.FindAnyObjectByType<CarriableTool>();
            shovel.RefreshRegistration();            // EditMode fires no OnEnable — this line is load-bearing
            hands.transform.position = new Vector2(0.4f, 0f);   // standing on the hole, shovel in hand
            hands.ApplyCarriedPose();

            var actor = InteractActor.For(hands.transform.position, Vector2.up, ControlMode.OnFoot);
            Assert.IsTrue(InteractResolver.TryResolveNow(actor, InteractResolver.DefaultFacingArcDegrees,
                                                         out IInteractable best));
            Assert.AreSame(dig, best,
                           "the work-site outranks the thing in your hands — otherwise you can never dig");

            best.Interact(actor);
            Assert.IsTrue(dig.Consumed, "the press dug the hole out");
            Assert.AreEqual("fish.soft_shell_clam", hands.Carried.DefId, "…and the clam is in her hand");
            Assert.AreSame(shovel, hands.Slung, "the shovel went under her arm to make room for it");
        }

        [Test]
        public void Step_off_the_hole_and_the_same_press_puts_the_shovel_down()
        {
            // The other direction, and the reason ToolTarget is safe: the fix must not cost you the
            // ability to set the tool down. Put-down is what you get by stepping out of the work-site's
            // reach — a pace, and what a person does before laying a shovel on the sand.
            var hold = new FakeHold();
            ClamDig dig = Hole(hold, Vector2.zero);
            Interactables.Register(dig);

            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);
            var shovel = Object.FindAnyObjectByType<CarriableTool>();
            shovel.RefreshRegistration();
            hands.transform.position = new Vector2(5f, 0f);     // well outside the hole's 1.25 m reach
            hands.ApplyCarriedPose();

            var actor = InteractActor.For(hands.transform.position, Vector2.up, ControlMode.OnFoot);
            Assert.IsTrue(InteractResolver.TryResolveNow(actor, InteractResolver.DefaultFacingArcDegrees,
                                                         out IInteractable best));
            Assert.AreSame(shovel, best, "away from a work-site the held tool is the answer again");

            best.Interact(actor);
            Assert.IsFalse(hands.IsCarrying, "the press set the shovel down");
            Assert.AreEqual(0, hold.UsedUnits, "and dug nothing");
        }

        [Test]
        public void ToolTarget_outranks_Held_which_outranks_Portable_which_outranks_a_fixture()
        {
            // The ladder itself, stated once. The gaps exist so a later rung can land between two of these
            // without renumbering anything a scene serialized — so the ORDER is the contract, not the
            // numbers, and this is what a change to either must not break.
            Assert.Greater(InteractPriority.ToolTarget, InteractPriority.Held);
            Assert.Greater(InteractPriority.Held, InteractPriority.Portable);
            Assert.Greater(InteractPriority.Portable, InteractPriority.Fixture);
        }

        // ---- 4. THE ROD GATE -------------------------------------------------------------------------

        private DevFishingInput Rig()
        {
            var go = new GameObject("Dory");
            _spawned.Add(go);
            var fishing = go.AddComponent<FishingController>();
            var species = ScriptableObject.CreateInstance<FishSpeciesDef>();
            species.Id = "fish.cod"; species.DisplayName = "Cod";
            species.MinWeightKg = 1f; species.MaxWeightKg = 4f; species.BaseValue = 10;
            _spawned.Add(species);
            fishing.Configure(new FakeHold(), new[] { species }, "region.st_peters", Gear.Handline, seed: 7);
            var dev = go.AddComponent<DevFishingInput>();
            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
            return dev;
        }

        [Test]
        public void Fishing_is_dead_until_the_rod_is_in_her_hands()
        {
            DevFishingInput dev = Rig();
            ToolInHand.Empty(_spawned);

            Assert.IsFalse(dev.RodIsToHand, "no rod held");
            Assert.IsFalse(dev.FishingLive, "…so there is no cast to make, standing on the dock or not");

            ToolInHand.Holding(ToolDef.RodId, _spawned);

            Assert.IsTrue(dev.RodIsToHand);
            Assert.IsTrue(dev.FishingLive, "rod in hand on foot — the line is live");
        }

        [Test]
        public void A_shovel_in_her_hands_does_not_cast_a_line()
        {
            DevFishingInput dev = Rig();
            ToolInHand.Holding(ToolDef.ShovelId, _spawned);

            Assert.IsFalse(dev.FishingLive, "the wrong tool is not the tool");
        }

        [Test]
        public void The_rod_gate_is_what_kills_the_cast_and_nothing_else()
        {
            // SABOTAGE, same shape as the dig's: turn the gate off and the identical empty-handed rig
            // fishes again — which is the pre-ruling behaviour, and is also the QA escape hatch's own test.
            DevFishingInput dev = Rig();
            ToolInHand.Empty(_spawned);
            Assert.IsFalse(dev.FishingLive);

            dev.ConfigureRodGate(requireInHand: false, rodToolId: ToolDef.RodId);

            Assert.IsTrue(dev.FishingLive, "gate off = always-on fishing, the behaviour that shipped before");
        }

        [Test]
        public void The_ownership_gate_is_off_by_default_and_bites_when_switched_on()
        {
            // The owner's call: holding is enough, because ownership matters at the shop, not at the
            // shore. The switch exists for the day the opening stops standing a free rod on the wharf.
            DevFishingInput dev = Rig();
            _save.OwnedGear.Remove("gear.rod");
            ToolInHand.Holding(ToolDef.RodId, _spawned);

            Assert.IsTrue(dev.FishingLive, "by default, holding a rod you do not own still fishes");

            dev.ConfigureRodGate(requireInHand: true, rodToolId: ToolDef.RodId,
                                 requireOwnership: true, rodGearId: "gear.rod");

            Assert.IsFalse(dev.FishingLive, "with ownership required, an unowned rod does not fish");
        }

        // ---- 5. THE CONTENT THE BUILDER LOADS --------------------------------------------------------

        [Test]
        public void The_tool_defs_exist_and_carry_the_exact_ids_the_gates_match_on()
        {
            // ⚠️ Silent-failure guard. StPetersBuilder.MakeTool WARNS and places nothing when the Def is
            // missing, and the gates match on the id STRING — so a renamed asset or a mistyped id leaves
            // the opening with no shovel and no rod, a build that looks fine, and no way to dig or fish.
            AssertTool("Assets/_Project/Data/Tools/Shovel.asset", ToolDef.ShovelId, PlayerGear.ShovelId);
            AssertTool("Assets/_Project/Data/Tools/Rod.asset", ToolDef.RodId, PlayerGear.RodId);

            static void AssertTool(string path, string expectedId, string expectedGearId)
            {
                var def = AssetDatabase.LoadAssetAtPath<ToolDef>(path);
                Assert.IsNotNull(def, $"no ToolDef at '{path}' — the builder would place no tool there");
                Assert.AreEqual(expectedId, def.Id, "the id a gate matches on");
                Assert.AreEqual(expectedGearId, def.GearId, "the ownership record it links to");
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.DisplayName), "a refusal has to name it");
            }
        }

        [Test]
        public void The_tool_ids_are_a_different_id_space_from_the_gear_ids()
        {
            // Not pedantry: holding and owning are different facts, asked of different systems, and the
            // day they share a string is the day one gate silently answers the other's question.
            Assert.AreNotEqual(ToolDef.ShovelId, PlayerGear.ShovelId);
            Assert.AreNotEqual(ToolDef.RodId, PlayerGear.RodId);
        }

        [Test]
        public void The_greybox_tool_sprites_the_builder_wires_are_on_disk()
        {
            // The builder only WARNS on a missing sprite (the tool still works, it is just invisible),
            // which in play reads as "the shovel is not there" — so the paths are pinned here instead.
            foreach (string path in new[] { "Assets/_Project/Art/Sprites/Gear/Shovel.png",
                                            "Assets/_Project/Art/Sprites/Gear/Rod.png" })
            {
                // ⚠️ LoadAssetAtPath<Sprite> is null on a spriteMode:Multiple texture even when it IS
                // sliced — the sprites are sub-assets. LoadAllAssetsAtPath is what the builder uses.
                bool any = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Any();
                Assert.IsTrue(any, $"'{path}' has no sprite — the tool would be invisible in the world");
            }
        }

        // ---- 6. CARRYING ONE OUT OF THE REGION IT CAME FROM ------------------------------------------

        [Test]
        public void A_tool_carried_out_of_a_sleeping_region_is_set_down_where_she_stands()
        {
            // ⚠️ A defect this PR makes REACHABLE. A region hop does not unload the region you left — it
            // SetActive(false)s its roots. The tools are region content, so putting one down after a
            // crossing would re-parent it under a sleeping root: alive, inactive, invisible, out of the
            // interact registry, unrecoverable. Drop to the scene root instead.
            var region = new GameObject("StPetersRoot");
            _spawned.Add(region);

            var go = new GameObject("Shovel");
            _spawned.Add(go);
            go.transform.SetParent(region.transform, false);
            var def = ScriptableObject.CreateInstance<ToolDef>();
            def.Id = ToolDef.ShovelId; def.DisplayName = "Clam Shovel";
            _spawned.Add(def);
            var tool = go.AddComponent<CarriableTool>();
            tool.Configure(def, go.AddComponent<SpriteRenderer>(), "tool.st_peters.shovel");

            CarryHands hands = ToolInHand.Empty(_spawned);
            Assert.AreEqual(CarryRefusal.None, hands.TryPickUp(tool));

            region.SetActive(false);                            // …she crosses to Nine Mile Creek
            hands.transform.position = new Vector2(120f, -40f);

            Assert.AreEqual(CarryRefusal.None, hands.TryPlace());

            Assert.IsNull(go.transform.parent, "not re-parented into the sleeping region");
            Assert.IsTrue(go.activeInHierarchy, "and therefore still a thing in the world");
            Assert.AreEqual(new Vector2(120f, -40f), (Vector2)go.transform.position,
                            "set down where she actually stood");
        }
    }
}
