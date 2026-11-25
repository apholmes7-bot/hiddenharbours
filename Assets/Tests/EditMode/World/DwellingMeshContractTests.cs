using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// The Core contracts for parked dwellings (ADR 0060, PR 1): the pure rules a
    /// <see cref="DwellingMeshDef"/> is held to, the cut it hands the renderer, and the presentation
    /// locator. No bake, no renderer, no scene.
    /// </summary>
    public class DwellingMeshContractTests
    {
        private static readonly Vector2[] Square =
            { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) };

        private static DwellingLevel Level(string id, int tag, int lid = 0, bool enclosed = true, int storey = 0) =>
            new DwellingLevel { LevelId = id, Tag = tag, LidTag = lid, Enclosed = enclosed, Storey = storey, Floor = Square };

        private static DwellingThreshold Door(string level = "saloon") =>
            new DwellingThreshold { Id = "entry", LevelId = level, ClearWidthMeters = 0.72f, LandingRadiusMeters = 0.35f };

        private static DwellingFixtureAnchor Anchor(string id, string verb, string level = "saloon", string defId = "") =>
            new DwellingFixtureAnchor { AnchorId = id, Verb = verb, LevelId = level, DefId = defId };

        private static DwellingLevel[] TwoLevels() => new[] { Level("saloon", 1, lid: 3), Level("head", 2, lid: 3) };

        // ---- floor outlines ---------------------------------------------------------------

        [Test]
        public void SignedArea_IsPositiveCounterClockwiseAndNegativeClockwise()
        {
            Assert.That(DwellingMeshRules.SignedArea(Square), Is.EqualTo(4f).Within(1e-6f));
            var clockwise = (Vector2[])Square.Clone();
            System.Array.Reverse(clockwise);
            Assert.That(DwellingMeshRules.SignedArea(clockwise), Is.EqualTo(-4f).Within(1e-6f));
        }

        [Test]
        public void FloorCloses_AcceptsARealOutlineEitherWayRoundAndARepeatedFirstPoint()
        {
            Assert.That(DwellingMeshRules.FloorCloses(Square), Is.True);
            var clockwise = (Vector2[])Square.Clone();
            System.Array.Reverse(clockwise);
            Assert.That(DwellingMeshRules.FloorCloses(clockwise), Is.True);
            var repeated = new[] { Square[0], Square[1], Square[2], Square[3], Square[0] };
            Assert.That(DwellingMeshRules.FloorCloses(repeated), Is.True);
        }

        [Test]
        public void FloorCloses_RefusesNullTooFewPointsALineAndNonFiniteValues()
        {
            Assert.That(DwellingMeshRules.FloorCloses(null), Is.False);
            Assert.That(DwellingMeshRules.FloorCloses(new[] { Vector2.zero, Vector2.one }), Is.False);
            Assert.That(DwellingMeshRules.FloorCloses(new[] { Vector2.zero, Vector2.one, new Vector2(2f, 2f) }),
                Is.False, "three collinear points enclose nothing");
            var nan = (Vector2[])Square.Clone();
            nan[2] = new Vector2(float.NaN, 1f);
            Assert.That(DwellingMeshRules.FloorCloses(nan), Is.False);
        }

        // ---- the cut ----------------------------------------------------------------------

        [Test]
        public void CutFor_AnEnclosedLevel_IsItsTagAndItsLid()
        {
            var cut = DwellingMeshRules.CutFor(TwoLevels(), "head");
            Assert.That(cut.Opens, Is.True);
            Assert.That(cut.Level, Is.EqualTo(2));
            Assert.That(cut.Lid, Is.EqualTo(3));
            Assert.That(cut.LiftLevels, Is.EqualTo(1 << 3), "the renderer's own Cut lifts the lid");
        }

        [Test]
        public void CutFor_RefusalsDrawTheWholeExterior()
        {
            var levels = new[] { Level("saloon", 1), Level("porch", 2, enclosed: false), Level("bad", 0) };
            Assert.That(DwellingMeshRules.CutFor(levels, "porch").Opens, Is.False, "an open level cannot be cut");
            Assert.That(DwellingMeshRules.CutFor(levels, "bad").Opens, Is.False, "tag 0 is the exterior");
            Assert.That(DwellingMeshRules.CutFor(levels, "attic").Opens, Is.False, "unknown level");
            Assert.That(DwellingMeshRules.CutFor(levels, null).Opens, Is.False);
            Assert.That(DwellingMeshRules.CutFor(null, "saloon").Opens, Is.False);
        }

        [Test]
        public void CutFor_ALevelWithNoLid_TakesNothingWithIt()
        {
            var cut = DwellingMeshRules.CutFor(new[] { Level("saloon", 1) }, "saloon");
            Assert.That(cut.Level, Is.EqualTo(1));
            Assert.That(cut.Lid, Is.EqualTo(0));
            Assert.That(cut.LiftLevels, Is.EqualTo(0));
        }

        // ---- the room's first problem -----------------------------------------------------

        [Test]
        public void FirstRoomProblem_IsNullForACompleteRoom()
        {
            var anchors = new[]
            {
                Anchor("freezer", "freeze_catch", defId: "freezer.camper_freezer"),
                Anchor("head_basin", "wash", level: "head"),
            };
            Assert.That(DwellingMeshRules.FirstRoomProblem(TwoLevels(), Door(), anchors), Is.Null);
            Assert.That(DwellingMeshRules.FirstRoomProblem(TwoLevels(), Door(), null), Is.Null,
                "a room with no fixtures is still a room");
        }

        [TestCase("no levels", "Levels")]
        [TestCase("empty id", "LevelId")]
        [TestCase("tag zero", "Tag")]
        [TestCase("lid is self", "LidTag")]
        [TestCase("open floor", "Floor")]
        [TestCase("repeated id", "repeats")]
        [TestCase("repeated tag", "repeats")]
        public void FirstRoomProblem_NamesTheBrokenLevelField(string sabotage, string expected)
        {
            var levels = TwoLevels();
            switch (sabotage)
            {
                case "no levels": levels = new DwellingLevel[0]; break;
                case "empty id": levels[1].LevelId = ""; break;
                case "tag zero": levels[1].Tag = 0; break;
                case "lid is self": levels[1].LidTag = levels[1].Tag; break;
                case "open floor": levels[1].Floor = new[] { Vector2.zero, Vector2.one }; break;
                case "repeated id": levels[1].LevelId = levels[0].LevelId; break;
                case "repeated tag": levels[1].Tag = levels[0].Tag; break;
            }
            Assert.That(DwellingMeshRules.FirstRoomProblem(levels, Door(), null), Does.Contain(expected));
        }

        [Test]
        public void FirstRoomProblem_RefusesAMissingDoorOrADoorOntoNoLevel()
        {
            var noDoor = Door();
            noDoor.ClearWidthMeters = 0f;
            Assert.That(noDoor.IsPublished, Is.False);
            Assert.That(DwellingMeshRules.FirstRoomProblem(TwoLevels(), noDoor, null), Does.Contain("Threshold"));
            Assert.That(DwellingMeshRules.FirstRoomProblem(TwoLevels(), Door("loft"), null), Does.Contain("Threshold.LevelId"));
        }

        [Test]
        public void FirstRoomProblem_RefusesAnAnchorWithNoVerbNoLevelOrARepeatedId()
        {
            Assert.That(DwellingMeshRules.FirstRoomProblem(TwoLevels(), Door(), new[] { Anchor("", "sit") }),
                Does.Contain("AnchorId"));
            Assert.That(DwellingMeshRules.FirstRoomProblem(TwoLevels(), Door(), new[] { Anchor("settee", "") }),
                Does.Contain("Verb"));
            Assert.That(DwellingMeshRules.FirstRoomProblem(TwoLevels(), Door(), new[] { Anchor("settee", "sit", "loft") }),
                Does.Contain("names no level"));
            Assert.That(DwellingMeshRules.FirstRoomProblem(TwoLevels(), Door(),
                new[] { Anchor("settee", "sit"), Anchor("settee", "sit") }), Does.Contain("repeats"));
        }

        [Test]
        public void Anchor_DefIdAndFootprintAreOptionalAndAskedOnce()
        {
            var bunk = Anchor("bunk", "sleep_save");
            Assert.That(bunk.HasDefId, Is.False);
            Assert.That(bunk.HasFootprint, Is.False);
            var freezer = Anchor("freezer", "freeze_catch", defId: "freezer.camper_freezer");
            freezer.Footprint = Square;
            Assert.That(freezer.HasDefId, Is.True);
            Assert.That(freezer.HasFootprint, Is.True);
        }

        // ---- the def ----------------------------------------------------------------------

        [Test]
        public void Def_LookupsAndCutReadTheTables()
        {
            var def = ScriptableObject.CreateInstance<DwellingMeshDef>();
            try
            {
                def.Levels = TwoLevels();
                def.Anchors = new[] { Anchor("bunk", "sleep_save") };
                Assert.That(def.TryGetLevel("head", out var head), Is.True);
                Assert.That(head.Tag, Is.EqualTo(2));
                Assert.That(def.TryGetLevel("loft", out _), Is.False);
                Assert.That(def.TryGetAnchor("bunk", out var bunk), Is.True);
                Assert.That(bunk.Verb, Is.EqualTo("sleep_save"));
                Assert.That(def.TryGetAnchor("stove", out _), Is.False);
                Assert.That(def.CutForLevel("saloon").Level, Is.EqualTo(1));
                Assert.That(def.HasRoom(), Is.False, "no interior ramps, no room geometry");
                Assert.That(def.Id, Does.StartWith("dwelling."));
            }
            finally { Object.DestroyImmediate(def); }
        }

        [Test]
        public void Def_IsUsable_OnlyWithRenderPayloadAndRoom()
        {
            var def = ScriptableObject.CreateInstance<DwellingMeshDef>();
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            try
            {
                Assert.That(def.IsUsable(), Is.False);
                Assert.That(def.FirstProblem(), Does.Contain("Mesh"));

                def.Mesh = mesh;
                Assert.That(def.FirstProblem(), Does.Contain("Ramps"));
                def.Ramps = new[] { new HullMeshDef.Ramp { Colors = new[] { new Color32(1, 2, 3, 255) } } };
                Assert.That(def.FirstProblem(), Does.Contain("Bayer16"));
                def.Bayer16 = new float[DwellingMeshDef.DitherMatrixLength];
                Assert.That(def.FirstProblem(), Does.Contain("Cell"));
                def.CellW = 64; def.CellH = 64;
                Assert.That(def.FirstProblem(), Does.Contain("Levels"));
                def.Levels = TwoLevels();
                def.Threshold = Door();
                Assert.That(def.FirstProblem(), Is.Null);
                Assert.That(def.IsUsable(), Is.True);

                def.InteriorRamps = new HullMeshDef.Ramp[HullMeshDef.InteriorRampSlots + 1];
                Assert.That(def.FirstProblem(), Does.Contain("InteriorRamps"), "the interior table's cap holds");
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(mesh);
            }
        }

        // ---- the presentation locator -----------------------------------------------------

        private sealed class FakeService : IDwellingMeshPresentationService
        {
            public IHullMeshRenderer Install(GameObject host, DwellingMeshDef def) => null;
            public IHullPropRenderer AttachPart(GameObject host, HullPropMeshDef def, string slot) => null;
            public void DetachPart(GameObject host, string slot) { }
            public void Remove(GameObject host) { }
        }

        [Test]
        public void Locator_HoldsWhatArtRegistersAndNullMeansNoMeshPath()
        {
            var previous = DwellingMeshPresentation.Service;
            try
            {
                DwellingMeshPresentation.Service = null;
                Assert.That(DwellingMeshPresentation.Service, Is.Null);
                var fake = new FakeService();
                DwellingMeshPresentation.Service = fake;
                Assert.That(DwellingMeshPresentation.Service, Is.SameAs(fake));
            }
            finally { DwellingMeshPresentation.Service = previous; }
        }
    }
}
