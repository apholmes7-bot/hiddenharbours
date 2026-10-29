using System.Collections.Generic;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The cut an entry landing opens, and the id the hull mesh keys it by.</b> The enclosed level row at
    /// the landing's height asks for its DeckId's cut first. On a mesh with a layout schema whose lift sets name
    /// that deck by its level instead, it asks by the row's LevelId; the express cruiser's house row has DeckId
    /// helm_deck, and its lift set is house. The landing's event carries whichever id opened, so the cutaway
    /// asking the mesh with it opens the same cut.
    ///
    /// <para>Each hull mesh is built in code, the way <see cref="HullMeshCutawayTableTests"/> builds its own, and
    /// every expected value is written out by hand.</para>
    /// </summary>
    public sealed class BoatCabinEntryLandingCutTests
    {
        private const string HullWalk = "hidden-harbours/hull-walk@1";

        private readonly List<Object> _made = new List<Object>();
        private readonly List<string> _published = new List<string>();

        // Make()'s cutaway subscribes in Configure, and DestroyImmediate runs no OnDestroy here: without the clears
        // a destroyed cutaway stays on the bus for every later case, in any class.
        [SetUp]
        public void SetUp()
        {
            ClearCabinChannels();
            _published.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ClearCabinChannels();
            for (int i = _made.Count - 1; i >= 0; i--) if (_made[i] != null) Object.DestroyImmediate(_made[i]);
            _made.Clear();
        }

        [Test]
        public void AHullWalkRowWhoseDeckIdKeysNoCut_OpensByItsLevelId()
        {
            HullMeshDef mesh = Mesh(HullWalk, new[] { Lift("house", "house", 128), Lift("below", "cabin", 196, 4) },
                Row("house", "helm_deck", 2, 1.17f), Row("below", "cabin", 4, 0.22f));
            Assert.IsFalse(mesh.CutawayForDeck("helm_deck").Opens, "premise: no lift set is keyed by the row's deck id");
            var rig = Make(mesh);

            Assert.IsTrue(rig.Cabin.TryEnterLanding(rig.Def.Door), "the row's level keys a lift set that opens");
            CollectionAssert.AreEqual(new[] { "house" }, _published, "the event carries the id the cut opened by");
            Assert.AreEqual(128, rig.Cabin.EntryLandingCut.LiftLevels);
            Assert.AreEqual(128, rig.Cut.RequestedCut.LiftLevels, "the cutaway, asking by that id, opens the same cut");
        }

        [Test]
        public void AHullWalkRowWhoseDeckIdKeysACut_OpensByItsDeckId()
        {
            HullMeshDef mesh = Mesh(HullWalk,
                new[] { Lift("helm", "helm_deck", 32), Lift("house", "house", 128), Lift("below", "cabin", 196, 4) },
                Row("house", "helm_deck", 2, 1.17f), Row("below", "cabin", 4, 0.22f));
            Assert.IsTrue(mesh.CutawayForDeck("house").Opens, "premise: the row's level keys a cut as well");
            var rig = Make(mesh);

            Assert.IsTrue(rig.Cabin.TryEnterLanding(rig.Def.Door));
            CollectionAssert.AreEqual(new[] { "helm_deck" }, _published, "the deck id comes first");
            Assert.AreEqual(32, rig.Cabin.EntryLandingCut.LiftLevels);
            Assert.AreEqual(32, rig.Cut.RequestedCut.LiftLevels);
        }

        [Test]
        public void AMeshWithNoLayoutSchema_OpensByDeckIdThroughItsLevelTags()
        {
            HullMeshDef mesh = Mesh(null, null, Row("house", "helm_deck", 2, 1.17f), Row("below", "cabin", 4, 0.22f));
            var rig = Make(mesh);

            Assert.IsTrue(rig.Cabin.TryEnterLanding(rig.Def.Door));
            CollectionAssert.AreEqual(new[] { "helm_deck" }, _published);
            Assert.AreEqual(2, rig.Cabin.EntryLandingCut.Level, "the row's own tag");
            Assert.AreEqual(2, rig.Cut.RequestedCut.Level);
        }

        [Test]
        public void TwoRowsThatOpenAtTheLandingsHeight_ResolveToNoLanding()
        {
            HullMeshDef mesh = Mesh(HullWalk,
                new[] { Lift("house", "house", 128), Lift("loft", "loft", 64), Lift("below", "cabin", 196, 4) },
                Row("house", "helm_deck", 2, 1.17f), Row("loft", "loft", 3, 1.17f), Row("below", "cabin", 4, 0.22f));
            var rig = Make(mesh);

            Assert.IsFalse(rig.Cabin.TryEnterLanding(rig.Def.Door), "ambiguous geometry cannot choose a landing's cut");
            Assert.IsFalse(rig.Cabin.IsInside);
            CollectionAssert.IsEmpty(_published);
            Assert.IsFalse(rig.Cabin.EntryLandingCut.Opens);
            Assert.IsFalse(rig.Cut.RequestedCut.Opens);
        }

        [Test]
        public void ARowOffTheLandingsHeightByMoreThanTheTolerance_IsIgnored()
        {
            HullMeshDef mesh = Mesh(HullWalk,
                new[] { Lift("house", "house", 128), Lift("loft", "loft", 64), Lift("below", "cabin", 196, 4) },
                Row("house", "helm_deck", 2, 1.17f), Row("loft", "loft", 3, 1.32f), Row("below", "cabin", 4, 0.22f));
            var rig = Make(mesh);
            Assert.AreEqual(0.10f, rig.Def.FloorTolerance, "premise: the loft row is 0.15 m above a landing held to 0.10 m");

            Assert.IsTrue(rig.Cabin.TryEnterLanding(rig.Def.Door));
            CollectionAssert.AreEqual(new[] { "house" }, _published);
            Assert.AreEqual(128, rig.Cabin.EntryLandingCut.LiftLevels);
            Assert.AreEqual(128, rig.Cut.RequestedCut.LiftLevels);
        }

        private sealed class Rig
        {
            internal BoatInteriorDef Def; internal BoatInterior Cabin; internal BoatCutaway Cut;
        }

        private Rig Make(HullMeshDef mesh)
        {
            var root = new GameObject("EntryLandingCutFixture"); root.SetActive(false); _made.Add(root);
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>(); _made.Add(def);
            def.FloorToleranceMetres = 0.10f;
            def.Levels = new[] { new BoatInteriorLevel { Id = "cabin", SoleZMeters = 0.22f,
                Outline = new[] { new Vector2(-0.19f, 0.59f), new Vector2(0.19f, 0.59f),
                    new Vector2(0.19f, 0.98f), new Vector2(-0.19f, 0.98f) } } };
            def.Door = new BoatInteriorDoor { Id = "companionway", Side = "aft", ClearWidthMeters = 0.64f,
                ClearHeightMeters = 0.76f, ThresholdPoint = new Vector3(-0.30f, -0.51f, 1.17f),
                EntryLanding = new BoatInteriorEntryLanding { OpeningStart = new Vector2(-0.62f, -0.51f),
                    OpeningEnd = new Vector2(0.02f, -0.51f), HeightMetres = 1.17f, RouteId = "companionway",
                    UpperPlateau = new[] { new Vector2(-0.23f, -0.76f), new Vector2(-0.23f, -0.36f),
                        new Vector2(-0.37f, -0.36f), new Vector2(-0.37f, -0.76f) } } };
            def.Routes = new[] { new BoatInteriorRoute { Id = "companionway", FromLevel = "helm_fwd",
                ToLevel = "cabin", FromPoint = new Vector3(-0.30f, -0.76f, 1.17f),
                ToPoint = new Vector3(-0.06f, 0.70f, 0.22f), Mechanism = "InteriorStair", Placed = true } };
            var exterior = new GameObject("Exterior"); exterior.transform.SetParent(root.transform); exterior.AddComponent<SpriteRenderer>();
            var inside = new GameObject("Interior"); inside.transform.SetParent(root.transform); inside.AddComponent<SpriteRenderer>();
            var cabin = root.AddComponent<BoatInterior>();
            cabin.Configure(def, exterior.GetComponent<SpriteRenderer>(), inside.GetComponent<SpriteRenderer>(),
                null, inside.transform, root.transform, new Sprite[8], 8, true, 0f, 0f, 0f, 0f, meshRoom: mesh);
            var cut = root.AddComponent<BoatCutaway>(); cut.Configure(cabin, mesh, root.transform, new object());
            root.SetActive(true);
            EventBus.Subscribe<CabinEntryLandingEntered>(e => _published.Add(e.DeckId));
            return new Rig { Def = def, Cabin = cabin, Cut = cut };
        }

        private HullMeshDef Mesh(string layoutSchema, HullMeshDef.LiftSet[] liftSets, params HullMeshDef.LevelTag[] rows)
        {
            HullMeshDef mesh = ScriptableObject.CreateInstance<HullMeshDef>(); _made.Add(mesh);
            mesh.LayoutSchema = layoutSchema;
            mesh.LiftSets = liftSets;
            mesh.LevelTags = rows;
            return mesh;
        }

        private static HullMeshDef.LiftSet Lift(string key, string deckId, int liftLevels, int level = 0) =>
            new HullMeshDef.LiftSet { Key = key, DeckId = deckId, Level = level, LiftLevels = liftLevels };

        private static HullMeshDef.LevelTag Row(string levelId, string deckId, int tag, float soleZ) =>
            new HullMeshDef.LevelTag { LevelId = levelId, DeckId = deckId, Tag = tag, Enclosed = true, SoleZMeters = soleZ };

        private static void ClearCabinChannels()
        {
            EventBus.Clear<CabinEntered>();
            EventBus.Clear<CabinEntryLandingEntered>();
            EventBus.Clear<CabinLeft>();
            EventBus.Clear<ControlModeChanged>();
        }
    }
}
