using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>Her door leaf is drawn in the pose she is told, and only in that pose</b>: the renderer's
    /// half of the Core <see cref="IHullDoorLeaf"/> seam (owner playtest 2026-09-17: "after i board it
    /// and press e at an interactable door, i dont see the doors open").
    ///
    /// <para>The bake lifts the door's faces out of the hull mesh twice, at doorOpen 0 and 1
    /// (<see cref="HullMeshDef.DoorLeafClosed"/>, <see cref="HullMeshDef.DoorLeafOpen"/>), and the
    /// renderer draws one of the two on a "DoorLeaf" child of her posed mesh. Every setup here is
    /// synthetic, so these hold on a checkout whose hull assets predate the split.</para>
    /// </summary>
    public sealed class IsoFacetHullDoorLeafTests
    {
        private const string LeafChildName = "DoorLeaf";

        private GameObject _hullGo;
        private Mesh _hullMesh;
        private Mesh _shut;
        private Mesh _open;
        private readonly List<UnityEngine.Object> _made = new();

        private static readonly Color32[] Ramp =
        {
            new Color32(10, 20, 30, 255), new Color32(40, 50, 60, 255), new Color32(70, 80, 90, 255),
        };

        [SetUp]
        public void SetUp()
        {
            _hullMesh = Quad("HHTestHull",
                new Vector3(-1f, -2f, 0f), new Vector3(1f, -2f, 0f),
                new Vector3(1f, 2f, 0f), new Vector3(-1f, 2f, 0f));
            // A 0.72 m leaf in her aft wall, and the same four corners swung 90 degrees about the hinge.
            _shut = Quad("HHTestLeafShut",
                new Vector3(-0.36f, -2f, 0f), new Vector3(0.36f, -2f, 0f),
                new Vector3(0.36f, -2f, 1.85f), new Vector3(-0.36f, -2f, 1.85f));
            _open = Quad("HHTestLeafOpen",
                new Vector3(-0.36f, -2f, 0f), new Vector3(-0.36f, -1.28f, 0f),
                new Vector3(-0.36f, -1.28f, 1.85f), new Vector3(-0.36f, -2f, 1.85f));
        }

        [TearDown]
        public void TearDown()
        {
            if (_hullGo != null) UnityEngine.Object.DestroyImmediate(_hullGo);
            for (int i = 0; i < _made.Count; i++)
                if (_made[i] != null) UnityEngine.Object.DestroyImmediate(_made[i]);
            _made.Clear();
        }

        // =================================================================== the leaf she draws

        [Test]
        public void ASplitLeaf_HangsOnHerPosedMesh_DrawnShut_InHerOwnInk()
        {
            IsoFacetHullRenderer hull = Configured(_shut, _open);

            Assert.IsTrue(hull.CarriesDoorLeaf,
                "a setup with both poses must carry the leaf; this is the answer the door reads");
            Assert.IsFalse(hull.DoorLeafShownOpen,
                "a hull nobody has told anything draws her door shut, the pose the bake shipped");

            MeshFilter leaf = LeafOf(hull);
            Assert.IsNotNull(leaf,
                "the leaf must be a child named 'DoorLeaf' under PosedMesh, so it takes her heading, " +
                "roll, pitch and heave with no code of its own");
            Assert.AreSame(_shut, leaf.sharedMesh, "shut = the doorOpen 0 pose");
            Assert.AreEqual(1, PosesDrawn(hull),
                "exactly one pose is drawn; two would ghost an open door over a shut one");

            Material hullInk = hull.PosedMesh.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.IsNotNull(hullInk, "precondition: her posed mesh draws with the facet material");
            Assert.AreSame(hullInk, leaf.GetComponent<MeshRenderer>().sharedMaterial,
                "the leaf must share her facet material, or it shades, dithers and cuts away apart " +
                "from the wall it hangs in");
        }

        [Test]
        public void ShowDoorLeaf_DrawsTheOpenPose_AndTheShutPoseAgain()
        {
            IsoFacetHullRenderer hull = Configured(_shut, _open);

            hull.ShowDoorLeaf(true);
            Assert.IsTrue(hull.DoorLeafShownOpen);
            Assert.AreSame(_open, LeafOf(hull).sharedMesh, "open = the doorOpen 1 pose");
            Assert.AreEqual(1, PosesDrawn(hull), "the open pose REPLACES the shut one");

            hull.ShowDoorLeaf(false);
            Assert.IsFalse(hull.DoorLeafShownOpen);
            Assert.AreSame(_shut, LeafOf(hull).sharedMesh, "shut again = the doorOpen 0 pose");
            Assert.AreEqual(1, PosesDrawn(hull));
        }

        [Test]
        public void ARepaint_KeepsTheDoorThePlayerOpened()
        {
            IsoFacetHullRenderer hull = Configured(_shut, _open);
            hull.ShowDoorLeaf(true);

            hull.Configure(Setup(_shut, _open));   // a paint scheme or a re-presentation rebuilds her children

            Assert.IsTrue(hull.CarriesDoorLeaf, "the rebuilt hull carries her leaf again");
            Assert.IsTrue(hull.DoorLeafShownOpen, "a repaint must not slam a door the player opened");
            Assert.AreSame(_open, LeafOf(hull).sharedMesh, "the rebuilt leaf is drawn in the answer she kept");
            Assert.AreEqual(1, PosesDrawn(hull),
                "the old leaf went with the old posed mesh; no second leaf is left behind");
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void WithoutBothPoses_SheCarriesNoLeaf_AndAPushIsHarmless(bool withShut, bool withOpen)
        {
            IsoFacetHullRenderer hull = Configured(withShut ? _shut : null, withOpen ? _open : null);

            Assert.IsFalse(hull.CarriesDoorLeaf,
                "a hull baked before the split carries no leaf (her door is still inside her mesh, drawn " +
                "shut), and a lone pose must never be drawn with no way to draw the other");
            Assert.IsNull(LeafOf(hull), "no DoorLeaf child");
            Assert.AreEqual(0, PosesDrawn(hull), "neither pose is drawn anywhere");

            Assert.DoesNotThrow(() => hull.ShowDoorLeaf(true), "a door on a hull with no leaf still pushes");
            Assert.IsTrue(hull.DoorLeafShownOpen, "the answer is recorded");
            Assert.AreEqual(0, PosesDrawn(hull), "and still nothing is drawn");

            hull.Configure(Setup(_shut, _open));
            Assert.AreSame(_open, LeafOf(hull).sharedMesh,
                "a hull that gains her leaf draws it in the answer she was already given");
        }

        // =================================================================== def -> setup

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void ThePresentationService_PassesTheLeafAsAPairOrNotAtAll(bool withShut, bool withOpen)
        {
            var def = ScriptableObject.CreateInstance<HullMeshDef>();
            _made.Add(def);
            def.Mesh = _hullMesh;
            def.DoorLeafClosed = withShut ? _shut : null;
            def.DoorLeafOpen = withOpen ? _open : null;

            IsoFacetHullSetup setup = IsoFacetHullPresentationService.ToSetup(def);

            bool pair = withShut && withOpen;
            Assert.AreEqual(pair, def.HasDoorLeaf(), "HasDoorLeaf is the pair, never either pose alone");
            Assert.AreSame(pair ? _shut : null, setup.DoorLeafClosed,
                "the shut pose reaches the renderer only with its open partner");
            Assert.AreSame(pair ? _open : null, setup.DoorLeafOpen,
                "the open pose reaches the renderer only with its shut partner");
        }

        // =================================================================== helpers

        private IsoFacetHullRenderer Configured(Mesh shut, Mesh open)
        {
            _hullGo = new GameObject("TestHull");
            var hull = _hullGo.AddComponent<IsoFacetHullRenderer>();
            hull.Configure(Setup(shut, open));
            return hull;
        }

        private IsoFacetHullSetup Setup(Mesh shut, Mesh open) => new IsoFacetHullSetup
        {
            Mesh = _hullMesh,
            DoorLeafClosed = shut,
            DoorLeafOpen = open,
            Ramps = new[] { Ramp },
            RampOffsets = new[] { 0 },
            LightN = new Vector3(0.3f, -0.5f, 0.81f),
            Gain = 1f,
            Bias = 0f,
            Bayer16 = Bayer(),
            Keyline = new Color32(12, 14, 18, 255),
            PivotPx = new Vector2(32f, 48f),
            PxPerMetre = 32,
            CellW = 64,
            CellH = 64,
            ElevationDeg = 40f,
        };

        private static float[] Bayer()
        {
            var b = new float[16];
            for (int i = 0; i < 16; i++) b[i] = (i + 0.5f) / 16f;
            return b;
        }

        private Mesh Quad(string name, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new[] { a, b, c, d });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals();
            _made.Add(mesh);
            return mesh;
        }

        private static MeshFilter LeafOf(IsoFacetHullRenderer hull)
        {
            Transform posed = hull.PosedMesh;
            Transform leaf = posed != null ? posed.Find(LeafChildName) : null;
            return leaf != null ? leaf.GetComponent<MeshFilter>() : null;
        }

        /// <summary>How many filters anywhere under her draw either pose.</summary>
        private int PosesDrawn(IsoFacetHullRenderer hull)
        {
            int drawn = 0;
            foreach (MeshFilter filter in hull.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh != null && (mesh == _shut || mesh == _open)) drawn++;
            }
            return drawn;
        }
    }
}
