using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// The interior seam (ADR 0060 section 3 and 5, PR 4): <see cref="BuildingInterior"/>'s reveal interface
    /// with the sprite swap unchanged, the polygon floor read off a <see cref="DwellingMeshDef"/>,
    /// <see cref="DwellingCutaway"/> writing the cut on real transitions, and the occupant sort rule. No bake,
    /// no renderer, no scene: the renderer is a recording double behind Core's <see cref="IHullCutaway"/>.
    /// </summary>
    public class DwellingInteriorSeamTests
    {
        const float Depth = 0.6427876f;
        const float Wall = 0.3f;

        static readonly Vector2[] Ell =
        {
            new Vector2(-1f, -3f), new Vector2(1f, -3f), new Vector2(1f, 1f),
            new Vector2(0f, 1f), new Vector2(0f, 3f), new Vector2(-1f, 3f),
        };

        static readonly Vector2[] HeadFloor =
            { new Vector2(-1f, 2f), new Vector2(0f, 2f), new Vector2(0f, 3f), new Vector2(-1f, 3f) };

        readonly List<Object> _spawned = new List<Object>();

        [SetUp] public void SetUp() => OccupantSorting.Reset();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            OccupantSorting.Reset();
        }

        T Spawn<T>(T o) where T : Object { _spawned.Add(o); return o; }

        DwellingMeshDef Camper(string doorLevel = "saloon")
        {
            var def = Spawn(ScriptableObject.CreateInstance<DwellingMeshDef>());
            def.Id = "dwelling.test_camper";
            def.Levels = new[]
            {
                new DwellingLevel { LevelId = "saloon", Tag = 1, LidTag = 3, Enclosed = true, Storey = 0, Floor = Ell },
                new DwellingLevel { LevelId = "head", Tag = 2, LidTag = 3, Enclosed = true, Storey = 0, Floor = HeadFloor },
            };
            def.Threshold = new DwellingThreshold
            {
                Id = "entry", LevelId = doorLevel, PointMeters = new Vector3(1f, -1f, 0.5f),
                ClearWidthMeters = 0.72f, InsideLandingMeters = new Vector2(0.5f, -1f), LandingRadiusMeters = 0.35f,
            };
            def.Anchors = new DwellingFixtureAnchor[0];
            return def;
        }

        sealed class RecordingCutaway : MonoBehaviour, IHullCutaway
        {
            public int Calls;
            public bool CarriesLevelTags => true;
            public HullMeshDef.Cut CutawayShown { get; private set; }
            public void ShowCutaway(HullMeshDef.Cut cut) { Calls++; CutawayShown = cut; }
        }

        BuildingInterior Dwelling(Vector2 at, out DwellingCutaway cutaway, out RecordingCutaway renderer,
                                  out Transform occupant, bool withRenderer = true)
        {
            var go = Spawn(new GameObject("camper"));
            go.transform.position = at;
            var interior = go.AddComponent<BuildingInterior>();
            interior.Configure(null, null, null, 2f, 6f, 0, 8, Depth, Wall, 1.4f);
            cutaway = go.AddComponent<DwellingCutaway>();
            renderer = null;
            if (withRenderer)
            {
                var skin = new GameObject("skin");
                skin.transform.SetParent(go.transform, false);
                renderer = skin.AddComponent<RecordingCutaway>();
            }
            Assert.That(interior.ConfigureDwelling(Camper()), Is.True);
            occupant = Spawn(new GameObject("occupant")).transform;
            occupant.position = at + new Vector2(5f, -10f);   // well outside
            interior.SetOccupant(occupant);
            return interior;
        }

        static void Tick(BuildingInterior interior) =>
            typeof(BuildingInterior)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Invoke(interior, null);

        static Vector2 World(BuildingInterior interior, Vector2 model) => interior.Footprint.ModelToWorld(model);

        // ---- the rules -------------------------------------------------------------------------------

        [Test]
        public void PrimaryLevel_IsTheDoorsLevelOnItsStorey_ElseTheFirstOnTheStorey()
        {
            Assert.That(DwellingInteriorRules.PrimaryLevelIndex(Camper("saloon"), 0), Is.EqualTo(0));
            Assert.That(DwellingInteriorRules.PrimaryLevelIndex(Camper("head"), 0), Is.EqualTo(1),
                "the level the door opens onto speaks for its storey");
            Assert.That(DwellingInteriorRules.PrimaryLevelIndex(Camper(), 1), Is.EqualTo(-1), "no storey above");
            Assert.That(DwellingInteriorRules.PrimaryLevelIndex(null, 0), Is.EqualTo(-1));
        }

        [Test]
        public void CutFor_IsNoneOutside_AndThePrimaryLevelsCutInside()
        {
            var def = Camper();
            Assert.That(DwellingInteriorRules.CutFor(def, false, 0).Opens, Is.False, "outside, the exterior is whole");
            var cut = DwellingInteriorRules.CutFor(def, true, 0);
            Assert.That(cut.Level, Is.EqualTo(1));
            Assert.That(cut.Lid, Is.EqualTo(3));
            Assert.That(DwellingInteriorRules.CutFor(def, true, 1).Opens, Is.False, "a storey with no level");
            Assert.That(DwellingInteriorRules.CutFor(null, true, 0).Opens, Is.False);
        }

        [Test]
        public void TryFloor_ReadsThePrimaryLevelsOutline_ByReference()
        {
            Assert.That(DwellingInteriorRules.TryFloor(Camper(), 0, out Vector2[] floor), Is.True);
            Assert.That(ReferenceEquals(floor, Ell), Is.True, "the floor comes from the def, never retyped");
            Assert.That(DwellingInteriorRules.TryFloor(Camper(), 1, out floor), Is.False);
            Assert.That(floor, Is.Null);
        }

        // ---- the polygon floor on the building --------------------------------------------------------

        [Test]
        public void AMeshDwelling_TestsItsOutline_NotItsRectangle()
        {
            var interior = Dwelling(new Vector2(20f, 30f), out _, out _, out _);
            InteriorFootprint f = interior.Footprint;
            Assert.That(f.HasFloorOutline, Is.True);

            Vector2 notch = World(interior, new Vector2(0.5f, 2f));
            var rectangle = new InteriorFootprint(f.Centre, 2f, 6f, 0, 8, Depth);
            Assert.That(rectangle.Contains(notch, Wall), Is.True, "the configured rectangle covers the notch");
            Assert.That(f.Contains(notch, Wall), Is.False, "the def's outline does not");
            Assert.That(interior.DoorWorld, Is.EqualTo(f.ModelToWorld(new Vector2(1f, -1f))),
                "the threshold is the def's door point");
        }

        [Test]
        public void ADwellingWithNoWayIn_IsRefused_AndKeepsTheRectangle()
        {
            var go = Spawn(new GameObject("bad"));
            var interior = go.AddComponent<BuildingInterior>();
            interior.Configure(null, null, null, 2f, 6f, 0, 8, Depth, Wall, 1.4f);
            var def = Camper();
            def.Threshold = default;

            LogAssert.Expect(LogType.Error, new Regex("refused dwelling.*Threshold"));
            Assert.That(interior.ConfigureDwelling(def), Is.False);
            Assert.That(interior.DwellingMesh, Is.Null);
            Assert.That(interior.Footprint.HasFloorOutline, Is.False);
        }

        // ---- the mesh reveal ------------------------------------------------------------------------

        [Test]
        public void WalkingIn_CutsThePrimaryLevel_AndWalkingOut_ClosesItUp()
        {
            var interior = Dwelling(new Vector2(20f, 30f), out var cutaway, out var renderer, out var occupant);
            Assert.That(renderer.CutawayShown.Opens, Is.False, "an empty dwelling is whole");

            occupant.position = World(interior, new Vector2(-0.3f, -2f));
            Tick(interior);
            Assert.That(interior.IsInside, Is.True);
            Assert.That(cutaway.RequestedCut.Level, Is.EqualTo(1));
            Assert.That(renderer.CutawayShown.Level, Is.EqualTo(1));
            Assert.That(renderer.CutawayShown.Lid, Is.EqualTo(3), "the roof comes off with the room");

            occupant.position = World(interior, new Vector2(0f, -8f));
            Tick(interior);
            Assert.That(interior.IsInside, Is.False);
            Assert.That(renderer.CutawayShown.Opens, Is.False);
        }

        [Test]
        public void StepsInsideTheNotchOfTheRectangle_AreOutside_SoNothingOpens()
        {
            var interior = Dwelling(Vector2.zero, out _, out var renderer, out var occupant);
            occupant.position = World(interior, new Vector2(0.5f, 2f));
            Tick(interior);
            Assert.That(interior.IsInside, Is.False);
            Assert.That(renderer.CutawayShown.Opens, Is.False);
        }

        [Test]
        public void TheCutIsWrittenOnTransitionsOnly_NotPerFrame()
        {
            var interior = Dwelling(Vector2.zero, out _, out var renderer, out var occupant);
            occupant.position = World(interior, new Vector2(-0.3f, -2f));
            Tick(interior);
            int afterEntry = renderer.Calls;
            for (int i = 0; i < 10; i++) Tick(interior);
            Assert.That(renderer.Calls, Is.EqualTo(afterEntry), "standing still inside writes nothing");
        }

        [Test]
        public void ARendererInstalledLate_GetsTheStandingCut_OnReassert()
        {
            var interior = Dwelling(Vector2.zero, out var cutaway, out _, out var occupant, withRenderer: false);
            occupant.position = World(interior, new Vector2(-0.3f, -2f));
            Tick(interior);
            Assert.That(cutaway.RequestedCut.Level, Is.EqualTo(1), "the cut is remembered with no renderer");

            var skin = new GameObject("late skin");
            skin.transform.SetParent(interior.transform, false);
            var late = skin.AddComponent<RecordingCutaway>();
            cutaway.Reassert();
            Assert.That(late.CutawayShown.Level, Is.EqualTo(1));
        }

        // ---- the occupant sort rule ----------------------------------------------------------------

        [Test]
        public void Inside_HeSortsOneRungInFrontOfTheDwelling_AndOutside_ThatInputIsGone()
        {
            var interior = Dwelling(new Vector2(4f, 12f), out var cutaway, out _, out var occupant);
            Assert.That(OccupantSorting.TryOrderFor(occupant, out _), Is.False);

            occupant.position = World(interior, new Vector2(-0.3f, -2f));
            Tick(interior);
            Assert.That(OccupantSorting.TryOrderFor(occupant, out int order), Is.True);
            Assert.That(cutaway.DwellingOrder, Is.EqualTo(HullSorting.OrderAt(12f)),
                "the dwelling sorts by the hull rule at its ground pivot");
            Assert.That(order, Is.EqualTo(cutaway.DwellingOrder + DwellingSorting.OccupantRungsInFront));

            occupant.position = World(interior, new Vector2(0f, -8f));
            Tick(interior);
            Assert.That(OccupantSorting.TryOrderFor(occupant, out _), Is.False);
        }

        [Test]
        public void DisablingTheDwelling_DropsTheOccupantInput()
        {
            var interior = Dwelling(Vector2.zero, out var cutaway, out _, out var occupant);
            occupant.position = World(interior, new Vector2(-0.3f, -2f));
            Tick(interior);
            Assert.That(OccupantSorting.IsOccupied, Is.True);
            cutaway.enabled = false;
            // EditMode dispatches OnDisable on a plain MonoBehaviour only for a live object; call the rule.
            typeof(DwellingCutaway).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cutaway, null);
            Assert.That(OccupantSorting.IsOccupied, Is.False);
        }
    }
}
