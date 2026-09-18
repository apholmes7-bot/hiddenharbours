using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The cabin door draws its leaf where the player can see it</b> (owner playtest 2026-09-17:
    /// "after i board it and press e at an interactable door, i dont see the doors open").
    ///
    /// <para>The 2026-08-28 ruling stands: the press moves the LEAF, and the player walks through the
    /// doorway. Until this pass the press flipped <see cref="BoatCabinDoor.IsOpen"/> and nothing drew
    /// it, because every hull mesh was baked at doorOpen 0. The door now pushes its state through the
    /// Core <see cref="IHullDoorLeaf"/> seam. These pin the door's half against a recording fake,
    /// the wiring against the real hull renderer, and the fleet's assets.</para>
    /// </summary>
    public class BoatCabinDoorLeafTests
    {
        private const float DeckRoll = 5f;
        private const float HeavePixels = 1.6f;
        private const float PitchLift = 0.02f;

        /// <summary>The fleet's own measurement: a 0.72 m opening on the aft face.</summary>
        private const float ClearWidth = 0.72f;
        private static readonly Vector2 Doorway = new(0f, -2.1f);

        private const string DoorId = "fixture.boat.door_leaf_test.cabin_door";

        private static readonly MethodInfo DoorFrame =
            typeof(BoatCabinDoor).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo DoorLeafStep =
            typeof(BoatCabinDoor).GetMethod("KeepLeafInStep", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            GameServices.Config = null;
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        // =====================================================================================
        //  THE DOOR'S HALF, AGAINST A RECORDING LEAF
        // =====================================================================================

        [Test]
        public void SetOpen_DrawsTheLeafOpen_AndShut_OncePerChange()
        {
            Rig rig = NewRig(withLeaf: true);
            CollectionAssert.IsEmpty(rig.Leaf.Shows,
                "wired shut to a leaf already drawn shut, there is nothing to push");

            rig.Door.SetOpen(true);
            Assert.IsTrue(rig.Leaf.ShownOpen,
                "SetOpen(true) is the arrival's opening: it must draw the leaf open at once, not on a later frame");
            rig.Door.SetOpen(true);
            rig.Door.SetOpen(false);
            Assert.IsFalse(rig.Leaf.ShownOpen, "SetOpen(false) draws it shut");
            CollectionAssert.AreEqual(new[] { true, false }, rig.Leaf.Shows,
                "one push per change; an unchanged door pushes nothing");
        }

        [Test]
        public void Wiring_ShutsALeafTheHullStillDrawsOpen()
        {
            // The renderer keeps its answer across a repaint, and the installer re-wires the door on
            // every hull change, so a fresh wiring can meet a leaf still drawn open.
            Rig rig = NewRig(withLeaf: true, leafDrawnOpen: true);

            Assert.IsFalse(rig.Door.IsOpen, "a freshly wired door is shut (2026-08-28)");
            Assert.IsFalse(rig.Leaf.ShownOpen, "and its leaf is drawn to match the moment it is wired");
            CollectionAssert.AreEqual(new[] { false }, rig.Leaf.Shows);
        }

        [Test]
        public void APress_LandsTheLeafAtTheEndOfTheCue_NotAtThePress()
        {
            Rig rig = NewRig(withLeaf: true);
            float cue = rig.Door.CueSeconds;
            Assert.Greater(cue, 0f, "precondition: the door measures a cue (8 frames at 70 ms)");
            Assert.IsTrue(rig.Door.ThresholdIsWalkable,
                "precondition: a measured doorway, so the press moves the leaf and never carries the player");

            Assert.IsTrue(rig.Door.TryUse(), "the press is taken");
            rig.Door.Tick(cue * 0.5f);
            Assert.IsFalse(rig.Door.IsOpen, "mid-cue the door is not open yet");
            CollectionAssert.IsEmpty(rig.Leaf.Shows,
                "mid-cue the leaf is still drawn shut: the bake carries the end poses only, so the leaf " +
                "lands when the cue does");

            rig.Door.Tick(cue);
            Assert.IsTrue(rig.Door.IsOpen);
            CollectionAssert.AreEqual(new[] { true }, rig.Leaf.Shows, "the cue's end draws the leaf open, once");

            Assert.IsTrue(rig.Door.TryUse(), "the second press is taken");
            rig.Door.Tick(cue * 0.5f);
            Assert.IsTrue(rig.Leaf.ShownOpen, "mid-cue the leaf is still drawn open");
            rig.Door.Tick(cue);
            Assert.IsFalse(rig.Door.IsOpen);
            CollectionAssert.AreEqual(new[] { true, false }, rig.Leaf.Shows, "the cue's end draws it shut, once");
        }

        [Test]
        public void AnAdditionalDoor_NeverMovesTheLeaf()
        {
            Rig rig = NewRig(withLeaf: true, asAdditionalDoor: true);
            Assert.AreEqual("saloon", rig.Door.Door.Id, "precondition: the door reads the def's additional door");

            rig.Door.SetOpen(true);
            Frame(rig.Door);
            Assert.IsTrue(rig.Door.TryUse(), "precondition: the press is taken");
            rig.Door.Tick(rig.Door.CueSeconds + 0.01f);
            Assert.IsFalse(rig.Door.IsOpen, "precondition: the press shut it");
            Frame(rig.Door);

            CollectionAssert.IsEmpty(rig.Leaf.Shows,
                "the bake splits out the MAIN door's leaf only; an additional door that pushed would swing " +
                "a leaf that is not its own");
        }

        [Test]
        public void ALeafPresentedAfterTheWiring_IsFoundOnTheNextFrame()
        {
            // The hull-swap order: SetHull re-wires the door, THEN the skinner presents her mesh.
            Rig rig = NewRig(withLeaf: false);
            rig.Door.SetOpen(true);

            RecordingLeaf leaf = AddLeaf(rig.Root);
            Frame(rig.Door);

            Assert.IsTrue(leaf.ShownOpen, "the door finds a leaf that arrived after it was wired");
            CollectionAssert.AreEqual(new[] { true }, leaf.Shows, "and draws it in the door's state, once");
        }

        [Test]
        public void ALeafReplacedUnderTheDoor_IsFoundAgain()
        {
            Rig rig = NewRig(withLeaf: true);
            rig.Door.SetOpen(true);
            Assert.IsTrue(rig.Leaf.ShownOpen, "precondition: the first leaf is drawn open");

            Object.DestroyImmediate(rig.Leaf.gameObject);
            RecordingLeaf second = AddLeaf(rig.Root);
            Frame(rig.Door);

            Assert.IsTrue(second.ShownOpen,
                "a destroyed leaf is a fake null; the door must look again, not keep pushing into it");
            CollectionAssert.AreEqual(new[] { true }, second.Shows);
        }

        [Test]
        public void ADoorWithNoLeaf_LooksForOneTwiceASecond_NotEveryFrame()
        {
            Rig rig = NewRig(withLeaf: false);
            rig.Door.SetOpen(true);
            LeafStep(rig.Door, 0.016f);            // looks, finds nothing, waits

            RecordingLeaf leaf = AddLeaf(rig.Root);
            LeafStep(rig.Door, 0.2f);
            LeafStep(rig.Door, 0.2f);
            CollectionAssert.IsEmpty(leaf.Shows,
                "a hull drawn with no split leaf (a mesh baked before the split, or her sheet) has none for " +
                "as long as she sails: searching her tree every frame is a cost for nothing (Rule 7), so the " +
                "door waits half a second between looks");

            LeafStep(rig.Door, 0.2f);
            CollectionAssert.AreEqual(new[] { true }, leaf.Shows, "past half a second it looks again, and finds her");
        }

        // =====================================================================================
        //  THE WIRING, AGAINST THE REAL HULL RENDERER
        // =====================================================================================

        [Test]
        public void TheRealHullRenderer_DrawsTheLeafTheDoorIsIn()
        {
            Rig rig = NewRig(withLeaf: false);
            Mesh shut = Quad("HHTestLeafShut", -2f);
            Mesh open = Quad("HHTestLeafOpen", -1.28f);

            var hullGo = new GameObject("FacetHull");
            hullGo.transform.SetParent(rig.Root.transform, false);
            var hull = hullGo.AddComponent<IsoFacetHullRenderer>();
            hull.Configure(SyntheticSetup(shut, open));
            Frame(rig.Door);

            Assert.IsTrue(hull.CarriesDoorLeaf, "precondition: the synthetic hull carries a split leaf");
            Assert.AreSame(shut, LeafMesh(hull), "a shut door, a shut leaf");

            rig.Door.SetOpen(true);
            Assert.AreSame(open, LeafMesh(hull), "SetOpen(true) reaches the renderer's leaf choice");
            rig.Door.SetOpen(false);
            Assert.AreSame(shut, LeafMesh(hull), "SetOpen(false) reaches it too");

            Assert.IsTrue(rig.Door.TryUse(), "the press is taken");
            rig.Door.Tick(rig.Door.CueSeconds + 0.01f);
            Assert.IsTrue(rig.Door.IsOpen);
            Assert.AreSame(open, LeafMesh(hull), "a press draws the real leaf open at the cue's end");
        }

        // =====================================================================================
        //  THE FLEET
        // =====================================================================================

        [Test]
        public void EveryMeshHullWithACabinDoor_CarriesItsDoorLeaf()
        {
            var measured = new List<string>();
            var missing = new StringBuilder();
            int missingCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:BoatVisualDef"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(path);
                if (visual == null || !visual.HasInterior() || visual.Interior.Door == null || !visual.HasHullMesh())
                    continue;

                measured.Add(visual.name);
                if (visual.HullMesh.HasDoorLeaf()) continue;
                missingCount++;
                missing.AppendLine($"  {path} -> {AssetDatabase.GetAssetPath(visual.HullMesh)}");
            }

            CollectionAssert.Contains(measured, "CapeIslanderIso", "the cape islander is a playtest hull with a cabin door");
            CollectionAssert.Contains(measured, "LobsterBoatIso", "the lobster boat is a playtest hull with a cabin door");
            Assert.GreaterOrEqual(measured.Count, 27,
                "27 mesh hulls carried a cabin door on 2026-09-17; fewer means this guard stopped looking. " +
                "If a hull really lost her door, name it in the PR and lower this bar.");
            Assert.AreEqual(0, missingCount,
                $"{missingCount} of {measured.Count} mesh hulls with a cabin door have no DoorLeafClosed/" +
                "DoorLeafOpen pair, so their door never visibly opens. Re-bake them with RigMeshAssetBaker:\n" +
                missing);
        }

        // =====================================================================================
        //  THE RIG
        // =====================================================================================

        /// <summary>The seam, faked: what the door told the hull, in order.</summary>
        private sealed class RecordingLeaf : MonoBehaviour, IHullDoorLeaf
        {
            public bool ShownOpen;
            public readonly List<bool> Shows = new();

            public bool CarriesDoorLeaf => true;
            public bool DoorLeafShownOpen => ShownOpen;

            public void ShowDoorLeaf(bool open)
            {
                Shows.Add(open);
                ShownOpen = open;
            }
        }

        private struct Rig
        {
            public GameObject Root;
            public BoatInterior Interior;
            public BoatCabinDoor Door;
            public RecordingLeaf Leaf;
        }

        /// <summary>
        /// A cabin and her door, wired the way the placement pass wires them (the walkthrough tests'
        /// rig), with a recording leaf on a child of the hull root when asked for.
        /// </summary>
        private Rig NewRig(bool withLeaf, bool leafDrawnOpen = false, bool asAdditionalDoor = false)
        {
            var made = ScriptableObject.CreateInstance<BoatInteriorDef>();
            made.Id = "interior.door_leaf_test";
            made.PixelsPerMetre = 32;
            made.Levels = new[]
            {
                new BoatInteriorLevel
                {
                    Id = "house_sole",
                    SoleZMeters = 1.78f,
                    Outline = new[]
                    {
                        new Vector2(-1f, -2f), new Vector2(1f, -2f),
                        new Vector2(1f, 2f), new Vector2(-1f, 2f),
                    },
                },
            };
            made.Door = MeasuredDoor("entry");
            if (asAdditionalDoor) made.AdditionalDoors = new[] { MeasuredDoor("saloon") };
            _spawned.Add(made);

            var root = new GameObject("DoorLeafTestHull");
            _spawned.Add(root);

            var exterior = new GameObject("House").AddComponent<SpriteRenderer>();
            exterior.transform.SetParent(root.transform, false);

            var room = new GameObject("Room").AddComponent<SpriteRenderer>();
            room.transform.SetParent(root.transform, false);

            var interior = root.AddComponent<BoatInterior>();
            interior.Configure(made, exterior, room, null, room.transform, root.transform,
                               DummyCells(8), 8, true, 0f, DeckRoll, HeavePixels, PitchLift);

            RecordingLeaf leaf = null;
            if (withLeaf)
            {
                leaf = AddLeaf(root);
                leaf.ShownOpen = leafDrawnOpen;
            }

            var door = new GameObject("CabinDoor").AddComponent<BoatCabinDoor>();
            door.transform.SetParent(root.transform, false);
            door.Configure(interior, DoorId, asAdditionalDoor ? 0 : -1, 1.2f, "Open the door", "Close the door");

            return new Rig { Root = root, Interior = interior, Door = door, Leaf = leaf };
        }

        private static BoatInteriorDoor MeasuredDoor(string id) => new BoatInteriorDoor
        {
            Id = id,
            Side = "aft",
            ClearWidthMeters = ClearWidth,
            ClearHeightMeters = 1.85f,
            ThresholdPoint = new Vector3(Doorway.x, Doorway.y, 1.78f),
            Mechanism = BoatInteriorDoorMechanism.Sliding,
            CueFrames = 8,
            CueMillisecondsPerFrame = 70f,
            CueReversedOnExit = true,
        };

        private static RecordingLeaf AddLeaf(GameObject root)
        {
            var hull = new GameObject("Hull");
            hull.transform.SetParent(root.transform, false);
            return hull.AddComponent<RecordingLeaf>();
        }

        /// <summary>One of the door's own frames; EditMode runs no Update for her.</summary>
        private static void Frame(BoatCabinDoor door)
        {
            Assert.IsNotNull(DoorFrame, "BoatCabinDoor.Update is the per-frame wire these tests drive");
            DoorFrame.Invoke(door, null);
        }

        private static void LeafStep(BoatCabinDoor door, float unscaledSeconds)
        {
            Assert.IsNotNull(DoorLeafStep, "BoatCabinDoor.KeepLeafInStep is the per-frame leaf wire");
            DoorLeafStep.Invoke(door, new object[] { unscaledSeconds });
        }

        private Sprite[] DummyCells(int n)
        {
            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            var cells = new Sprite[n];
            for (int i = 0; i < n; i++)
            {
                cells[i] = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
                cells[i].name = $"cell_{i}";
                _spawned.Add(cells[i]);
            }
            return cells;
        }

        /// <summary>A 0.72 m by 1.85 m quad across the doorway, its free edge at <paramref name="freeEdgeY"/>.</summary>
        private Mesh Quad(string name, float freeEdgeY)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.36f, -2f, 0f), new Vector3(0.36f, freeEdgeY, 0f),
                new Vector3(0.36f, freeEdgeY, 1.85f), new Vector3(-0.36f, -2f, 1.85f),
            });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals();
            _spawned.Add(mesh);
            return mesh;
        }

        private IsoFacetHullSetup SyntheticSetup(Mesh shut, Mesh open)
        {
            Mesh hullMesh = Quad("HHTestHull", 2f);   // any non-empty mesh serves as her hull here
            var bayer = new float[16];
            for (int i = 0; i < 16; i++) bayer[i] = (i + 0.5f) / 16f;
            return new IsoFacetHullSetup
            {
                Mesh = hullMesh,
                DoorLeafClosed = shut,
                DoorLeafOpen = open,
                Ramps = new[]
                {
                    new[] { new Color32(10, 20, 30, 255), new Color32(40, 50, 60, 255), new Color32(70, 80, 90, 255) },
                },
                RampOffsets = new[] { 0 },
                LightN = new Vector3(0.3f, -0.5f, 0.81f),
                Gain = 1f,
                Bias = 0f,
                Bayer16 = bayer,
                Keyline = new Color32(12, 14, 18, 255),
                PivotPx = new Vector2(32f, 48f),
                PxPerMetre = 32,
                CellW = 64,
                CellH = 64,
                ElevationDeg = 40f,
            };
        }

        private static Mesh LeafMesh(IsoFacetHullRenderer hull)
        {
            Transform leaf = hull.PosedMesh != null ? hull.PosedMesh.Find("DoorLeaf") : null;
            Assert.IsNotNull(leaf, "the renderer hangs her leaf on a 'DoorLeaf' child of her posed mesh");
            return leaf.GetComponent<MeshFilter>().sharedMesh;
        }
    }
}
