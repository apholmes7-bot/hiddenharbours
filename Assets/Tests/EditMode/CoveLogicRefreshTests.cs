#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards the ADR 0011 committed-scene contract for the cove pilot: <c>Refresh Cove Logic</c> must
    /// rebuild ONLY the tagged <c>--LOGIC--</c> subtree (so it's idempotent) and must NEVER touch the owner's
    /// hand-painted VISUAL layer (any object that is NOT under a <see cref="RegionLogicRoot"/>).
    ///
    /// <para>This drives <see cref="GreyboxBuilder.RebuildLogicSubtree"/> against the active in-memory scene
    /// (the proven repo pattern — no file I/O, no NewScene; we track + destroy only the roots we add) with a
    /// stub "painted" object standing in for the owner's Grid/Tilemaps + decor. It asserts: (a) exactly ONE
    /// logic root after a rebuild, (b) the painted object SURVIVES the rebuild as the SAME instance, and
    /// (c) rebuilding again is idempotent (still one root, painting still intact, same tree shape).</para>
    /// </summary>
    public class CoveLogicRefreshTests
    {
        Scene _scene;
        GreyboxBuilder.DataRefs _data;
        readonly HashSet<GameObject> _preExisting = new();

        const string PaintedName = "Grid (owner's painted layer)";

        [SetUp]
        public void SetUp()
        {
            // Operate on the active scene (the repo's builder-test convention) — no NewScene (which errors in
            // batchmode on an unsaved untitled scene). Remember what was already there so TearDown removes ONLY
            // the objects this test introduced.
            _scene = EditorSceneManager.GetActiveScene();
            _preExisting.Clear();
            foreach (var go in _scene.GetRootGameObjects())
                if (go != null) _preExisting.Add(go);

            // Prepare the cove's data assets once (idempotent on disk); the logic tree wires to these.
            _data = GreyboxBuilder.PrepareData();
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy every root we added (the --LOGIC-- root + any painted stub), leaving the scene as found.
            if (!_scene.IsValid()) return;
            foreach (var go in _scene.GetRootGameObjects().ToArray())
                if (go != null && !_preExisting.Contains(go))
                    Object.DestroyImmediate(go);
        }

        // Stand-in for the owner's painted VISUAL layer: a root object NOT marked with RegionLogicRoot.
        GameObject AddPaintedLayer()
        {
            var painted = new GameObject(PaintedName);
            if (painted.scene != _scene) SceneManager.MoveGameObjectToScene(painted, _scene);
            // a child "tile" to prove children of the painted layer survive too
            var tile = new GameObject("PaintedTile");
            tile.transform.SetParent(painted.transform, false);
            return painted;
        }

        int LogicRootCount() =>
            _scene.GetRootGameObjects().Count(go => go != null && go.GetComponent<RegionLogicRoot>() != null);

        GameObject FindPainted() =>
            _scene.GetRootGameObjects().FirstOrDefault(go => go != null && go.name == PaintedName);

        GameObject TheLogicRoot() =>
            _scene.GetRootGameObjects().First(go => go.GetComponent<RegionLogicRoot>() != null);

        [Test]
        public void Rebuild_CreatesExactlyOneTaggedLogicRoot()
        {
            GreyboxBuilder.RebuildLogicSubtree(_scene, _data);

            Assert.AreEqual(1, LogicRootCount(),
                "a refresh must leave exactly one --LOGIC-- root tagged with RegionLogicRoot");

            var root = TheLogicRoot();
            Assert.AreEqual(GreyboxBuilder.LogicRootName, root.name, "the logic root keeps its conventional name");
            Assert.AreEqual("region.coddle_cove", root.GetComponent<RegionLogicRoot>().RegionId,
                "the logic root is stamped with the cove region id");

            // The actual gameplay logic landed UNDER the root (e.g. the RegionAnchor), not at scene root.
            Assert.IsNotNull(root.GetComponentInChildren<RegionAnchor>(),
                "the cove's RegionAnchor must be generated under the --LOGIC-- root");
        }

        [Test]
        public void Rebuild_LeavesThePaintedLayerUntouched()
        {
            var painted = AddPaintedLayer();

            GreyboxBuilder.RebuildLogicSubtree(_scene, _data);

            var survivor = FindPainted();
            Assert.IsNotNull(survivor, "the owner's painted layer must survive a logic refresh");
            Assert.IsTrue(ReferenceEquals(painted, survivor),
                "the SAME painted GameObject must survive — refresh must not destroy + recreate it");
            Assert.IsNotNull(survivor.transform.Find("PaintedTile"),
                "children of the painted layer must survive too (the refresh never reaches outside --LOGIC--)");
            // and the painted layer is NOT a logic root (it must never be swept up by the reconciler)
            Assert.IsNull(survivor.GetComponent<RegionLogicRoot>(),
                "the painted layer must not be tagged as logic");
        }

        [Test]
        public void Rebuild_IsIdempotent_OneRootAndPaintingIntactAfterTwice()
        {
            var painted = AddPaintedLayer();

            GreyboxBuilder.RebuildLogicSubtree(_scene, _data);
            int childrenAfterFirst = TheLogicRoot().transform.childCount;

            // Refresh again — the destroy-and-recreate path must not accumulate roots or drift the tree shape.
            GreyboxBuilder.RebuildLogicSubtree(_scene, _data);

            Assert.AreEqual(1, LogicRootCount(),
                "a second refresh must NOT leave a second --LOGIC-- root (old root destroyed, one re-created)");

            int childrenAfterSecond = TheLogicRoot().transform.childCount;
            Assert.AreEqual(childrenAfterFirst, childrenAfterSecond,
                "the regenerated logic tree must have the same shape each refresh (idempotent)");

            var survivor = FindPainted();
            Assert.IsNotNull(survivor, "the painted layer must still be present after two refreshes");
            Assert.IsTrue(ReferenceEquals(painted, survivor),
                "and it must still be the very same object (never touched across refreshes)");
        }
    }
}
#endif
