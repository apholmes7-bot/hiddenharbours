using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The hull-presenter seam (ADR 0022 phase 1).</b> These guard the two things phase 1 can actually
    /// get wrong, because it adds no behaviour:
    ///
    /// <list type="number">
    ///   <item><b>The enum-default trap.</b> A committed <c>.asset</c> deserialises a field it has never
    ///   heard of to <c>default</c>. For an enum that is the member numbered 0 — NOT the C# initialiser.
    ///   If <see cref="BoatHullVariant.Mesh"/> were 0, every boat already in the game would silently
    ///   become a mesh hull. The tests below assert the numbering AND then go and read the real committed
    ///   assets off disk, because the numbering is only a proxy for the thing that matters.</item>
    ///   <item><b>Forwarding drift.</b> <see cref="SpriteHullPresenter"/> must be a pane of glass. Every
    ///   member is asserted to agree with the <see cref="DirectionalBoatSprite"/> behind it — so the day
    ///   someone "helpfully" adds a rule to a getter, this goes red.</item>
    /// </list>
    /// </summary>
    public class BoatHullPresenterSeamTests
    {
        private const string DataBoats = "Assets/_Project/Data/Boats";

        // ---- (1) the enum-default trap ---------------------------------------------------------

        [Test]
        public void Variant_DefaultIsSprite_SoEveryCommittedAssetKeepsItsSkin()
        {
            // THE TRAP. Unity gives a missing serialized field default(T); for an enum that is member 0.
            Assert.AreEqual(BoatHullVariant.Sprite, default(BoatHullVariant),
                "default(BoatHullVariant) MUST be Sprite — every BoatVisualDef asset committed before this " +
                "field existed deserialises to it. If this is Mesh, every boat in the game loses its skin.");
        }

        [Test]
        public void Variant_SpriteIsNumberedZero_AndMeshIsNot()
        {
            Assert.AreEqual(0, (int)BoatHullVariant.Sprite, "Sprite must be the ZERO value");
            Assert.AreNotEqual(0, (int)BoatHullVariant.Mesh, "Mesh must never be the zero value");
        }

        [Test]
        public void Variant_NumberingIsAppendOnly_SoNoAssetSilentlyChangesMeaning()
        {
            // The variant is not persisted as a string — it is persisted as an INT. Renumbering an existing
            // member re-points every asset that stored the old number. Pin the wire values explicitly.
            Assert.AreEqual(0, (int)BoatHullVariant.Sprite, "Sprite is wire value 0 — append-only");
            Assert.AreEqual(1, (int)BoatHullVariant.Mesh, "Mesh is wire value 1 — append-only");
        }

        /// <summary>The one hull ALLOWED (and required) to be the mesh variant: the lobster boat,
        /// ADR 0022 phase 4's end-to-end hull. Everything else must still deserialise to Sprite.</summary>
        private const string MeshVariantException = "visual.lobster_boat_iso";

        [Test]
        public void Variant_CommittedAssets_AreSprite_ExceptTheLobster()
        {
            // The test with the teeth: not the enum's algebra, but the actual .asset files on disk.
            // Every pre-phase-4 asset has no Variant key and must come back Sprite; the lobster is
            // phase 4's deliberate flip — and if SHE reads Mesh, she must also be drawable as one.
            var all = AssetDatabase.FindAssets($"t:{nameof(BoatVisualDef)}", new[] { DataBoats })
                                   .Select(AssetDatabase.GUIDToAssetPath)
                                   .Select(AssetDatabase.LoadAssetAtPath<BoatVisualDef>)
                                   .Where(v => v != null)
                                   .ToList();

            Assert.IsNotEmpty(all, "no BoatVisualDefs found — the search path is wrong, so this test is vacuous");
            bool sawTheLobster = false;
            foreach (var v in all)
            {
                if (v.Id == MeshVariantException)
                {
                    sawTheLobster = true;
                    Assert.AreEqual(BoatHullVariant.Mesh, v.Variant,
                        "the lobster boat is phase 4's mesh hull — if this reverted, the owner's " +
                        "'i want them in game' mandate quietly un-shipped");
                    Assert.IsTrue(v.HasHullMesh(),
                        "a committed Mesh variant MUST carry a usable HullMesh def, or the skinner " +
                        "falls back to sprite forever and the variant is a lie");
                    continue;
                }
                Assert.AreEqual(BoatHullVariant.Sprite, v.Variant,
                    $"'{v.Id}' ({AssetDatabase.GetAssetPath(v)}) must still be a Sprite hull — only the " +
                    "lobster has a baked mesh def; a Mesh here would fall back with a warning every apply");
            }
            Assert.IsTrue(sawTheLobster, "the lobster visual is missing from Data/Boats — vacuous exception");
        }

        [Test]
        public void Variant_FieldInitialiserIsSprite_TheGuardThatActuallyProtectsShippedAssets()
        {
            // MEASURED, not assumed. Inverting the enum numbering (Sprite=1, Mesh=0) does NOT flip the
            // committed assets: Unity constructs the managed object — running field initialisers — and
            // only then overlays the YAML, so a key absent from the file leaves the initialiser standing.
            // The initialiser is therefore the first line of defence and deserves its own test; the
            // numbering above is the second, for every path where no initialiser runs.
            var fresh = ScriptableObject.CreateInstance<BoatVisualDef>();
            try
            {
                Assert.AreEqual(BoatHullVariant.Sprite, fresh.Variant,
                    "BoatVisualDef.Variant must be INITIALISED to Sprite, not merely numbered 0 — " +
                    "deleting the initialiser silently removes the protection the shipped assets rely on");
            }
            finally { Object.DestroyImmediate(fresh); }
        }

        [Test]
        public void Variant_RuntimeVisual_IsAlsoSprite()
        {
            // CreateRuntime builds a def in memory (the ambient fleet, the rotation harness). It goes
            // through CreateInstance, not deserialisation, so it takes the C# initialiser — assert the two
            // paths agree rather than assuming they do.
            var def = BoatVisualDef.CreateRuntime(new Sprite[] { null });
            Assert.AreEqual(BoatHullVariant.Sprite, def.Variant, "an in-memory runtime visual is a sprite hull");
            Object.DestroyImmediate(def);
        }

        // ---- (2) the presenter is a pane of glass ----------------------------------------------

        private static GameObject MakeBoat(int facingCount, bool ccw, float bakeElevation,
                                           out DirectionalBoatSprite directional)
        {
            var root = new GameObject("Boat");
            var childGo = new GameObject("Visual");
            childGo.transform.SetParent(root.transform, false);
            var sr = childGo.AddComponent<SpriteRenderer>();

            // Null sprites are fine: every member under test is derived from the CONFIGURED array's length
            // and the transform, never from the sprite pixels. Keeps this a pure EditMode test with no art
            // dependency and no rendering (CI has no graphics device).
            directional = root.AddComponent<DirectionalBoatSprite>();
            directional.Configure(new Sprite[facingCount], sr,
                                  zeroHeadingDegrees: 0f,
                                  facingsAreCounterClockwise: ccw,
                                  bakeElevationDegrees: bakeElevation);
            return root;
        }

        [Test]
        public void Presenter_ReportsSpriteVariant()
        {
            var root = MakeBoat(8, false, 40f, out var directional);
            try
            {
                Assert.AreEqual(BoatHullVariant.Sprite, new SpriteHullPresenter(directional).Variant);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Presenter_ForwardsEveryArtFact_Unchanged()
        {
            var root = MakeBoat(32, true, 40f, out var directional);
            try
            {
                var p = new SpriteHullPresenter(directional);

                Assert.AreEqual(32, p.FacingCount, "facing count is forwarded");
                Assert.IsTrue(p.FacingsAreCounterClockwise,
                    "FacingsAreCounterClockwise is per-ARTWORK and load-bearing — it must survive the seam");
                Assert.AreEqual(40f, p.BakeElevationDegrees, 1e-4f, "bake elevation is forwarded");
                Assert.AreEqual(directional.HasRockGrid, p.HasRockGrid, "rock-grid gate is forwarded");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Presenter_DrawnHeading_MatchesTheComponent_AcrossTheFullCircle()
        {
            var root = MakeBoat(8, false, 40f, out var directional);
            try
            {
                var p = new SpriteHullPresenter(directional);
                for (float deg = 0f; deg < 360f; deg += 7f)
                {
                    root.transform.rotation = Quaternion.Euler(0f, 0f, -deg);   // z-CCW, bow = up
                    Assert.AreEqual(directional.DrawnHeadingDegrees(), p.DrawnHeadingDegrees(), 1e-4f,
                        $"the presenter must not re-derive the drawn heading (at {deg})");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Presenter_FacingCell_AgreesWithHowTheOverlayLayersPickTheirRow()
        {
            // The overlays (DoryOarLayer, OutboardMotorLayer) go DrawnHeadingDegrees() ->
            // HeadingToFacingIndex. The presenter goes straight from the raw heading. Those are only the
            // same because the snap is idempotent under the index — assert it, for CCW art especially,
            // because a disagreement here puts the engine on the bow.
            foreach (bool ccw in new[] { false, true })
            {
                var root = MakeBoat(8, ccw, 40f, out var directional);
                try
                {
                    var p = new SpriteHullPresenter(directional);
                    for (float deg = 0f; deg < 360f; deg += 3f)
                    {
                        root.transform.rotation = Quaternion.Euler(0f, 0f, -deg);
                        int overlayRow = DirectionalBoatSprite.HeadingToFacingIndex(
                            directional.DrawnHeadingDegrees(), 8, 0f, ccw);
                        Assert.AreEqual(overlayRow, p.FacingCellIndex,
                            $"presenter cell must equal the overlay's row (ccw={ccw}, heading {deg})");
                        Assert.That(p.FacingCellIndex, Is.InRange(0, 7), "cell in range");
                    }
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        [Test]
        public void Presenter_RockFrameAndTilt_WriteThrough_NotIntoTheAdapter()
        {
            var root = MakeBoat(8, false, 40f, out var directional);
            try
            {
                var p = new SpriteHullPresenter(directional);

                Assert.AreEqual(-1, p.RockFrame, "-1 (level) is the default, as on the component");

                p.RockFrame = 5;
                Assert.AreEqual(5, directional.RockFrame,
                    "the presenter must WRITE THROUGH to the component — BoatWaveMotion's rock would " +
                    "otherwise vanish into the adapter and the hull would stop rocking");
                Assert.AreEqual(5, p.RockFrame, "and read back");

                p.VisualTiltDegrees = 3.5f;
                Assert.AreEqual(3.5f, directional.VisualTiltDegrees, 1e-4f,
                    "the tilt hook must write through — it is the ONLY supported additive rotation, " +
                    "because the component stomps the visual's rotation every LateUpdate");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Presenter_SurvivesADestroyedComponent_ReportingUnskinnedDefaults()
        {
            // BoatHullSkinner.RemoveSkin destroys the DirectionalBoatSprite on a hull swap. A caller
            // holding a stale presenter must degrade, not throw mid-frame.
            var root = MakeBoat(8, false, 40f, out var directional);
            try
            {
                var p = new SpriteHullPresenter(directional);
                Object.DestroyImmediate(directional);

                Assert.DoesNotThrow(() =>
                {
                    Assert.AreEqual(0f, p.DrawnHeadingDegrees(), 1e-4f);
                    Assert.AreEqual(0, p.FacingCount);
                    Assert.AreEqual(0, p.FacingCellIndex);
                    Assert.IsFalse(p.FacingsAreCounterClockwise);
                    Assert.AreEqual(90f, p.BakeElevationDegrees, 1e-4f, "90 = plan view, the unskinned default");
                    Assert.AreEqual(-1, p.RockFrame);
                    p.RockFrame = 4;                 // a write must be swallowed, not thrown
                    p.VisualTiltDegrees = 1f;
                });
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Presenter_AnchorsAreNeverNull()
        {
            var root = MakeBoat(8, false, 40f, out var directional);
            try
            {
                Assert.IsNotNull(new SpriteHullPresenter(directional).Anchors,
                    "a caller must be able to ask for an anchor without a null check");
                Assert.IsNotNull(new SpriteHullPresenter(null).Anchors,
                    "even with no component behind it");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ---- (3) the anchor contract -----------------------------------------------------------

        [Test]
        public void Anchors_UndefinedAnchor_MissesAndLeavesTheListUntouched()
        {
            var root = MakeBoat(8, false, 40f, out var directional);
            try
            {
                var anchors = new SpriteHullAnchors(directional);
                var into = new List<Vector2> { new Vector2(9f, 9f) };

                Assert.IsFalse(anchors.Has(BoatAnchorId.HaulerMount), "nothing defined it");
                Assert.IsFalse(anchors.TryGetPoints(BoatAnchorId.HaulerMount, into), "a miss returns false");
                Assert.AreEqual(1, into.Count, "a miss must not touch the caller's list");
                Assert.AreEqual(new Vector2(9f, 9f), into[0], "…nor rewrite what was already in it");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Anchors_NullList_IsAMiss_NotAThrow()
        {
            var root = MakeBoat(8, false, 40f, out var directional);
            try
            {
                var anchors = new SpriteHullAnchors(directional);
                anchors.Define(BoatAnchorId.HelmSeat, new Vector3(0f, 1f, 0.5f));
                Assert.IsFalse(anchors.TryGetPoints(BoatAnchorId.HelmSeat, null));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Anchors_SinglePoint_IsTheRigProjection_ForTheDRAWNCELL()
        {
            // The load-bearing assertion: the anchor is projected in CELL space (θ = cell·2π/count), NOT
            // in compass-heading space. For counter-clockwise art those differ, and getting it wrong puts
            // every anchored thing on the wrong side of the boat — the mirrored-art class of bug.
            const int Count = 8;
            var mount = new Vector3(0f, -3.53f, 0.72f);

            foreach (bool ccw in new[] { false, true })
            {
                var root = MakeBoat(Count, ccw, 40f, out var directional);
                try
                {
                    var anchors = new SpriteHullAnchors(directional);
                    anchors.Define(BoatAnchorId.MotorMount, mount);
                    Assert.IsTrue(anchors.Has(BoatAnchorId.MotorMount));

                    var into = new List<Vector2>();
                    for (float deg = 0f; deg < 360f; deg += 15f)
                    {
                        root.transform.rotation = Quaternion.Euler(0f, 0f, -deg);
                        into.Clear();

                        Assert.IsTrue(anchors.TryGetPoints(BoatAnchorId.MotorMount, into));
                        Assert.AreEqual(1, into.Count, "a single-point anchor appends exactly one point");

                        int cell = directional.CurrentFacingIndex;
                        Vector2 expected = MountedRockPoseMath.Project(
                            mount,
                            headingRadians: cell * 2f * Mathf.PI / Count,
                            rollRadians: 0f, pitchRadians: 0f,
                            elevationRadians: 40f * Mathf.Deg2Rad);

                        Assert.AreEqual(expected.x, into[0].x, 1e-4f, $"anchor x (ccw={ccw}, {deg}°)");
                        Assert.AreEqual(expected.y, into[0].y, 1e-4f, $"anchor y (ccw={ccw}, {deg}°)");
                    }
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        [Test]
        public void Anchors_MultiPoint_AppendsEveryPointInRigOrder()
        {
            var root = MakeBoat(8, false, 40f, out var directional);
            try
            {
                var anchors = new SpriteHullAnchors(directional);
                var tubs = new[]
                {
                    new Vector3(-0.8f, 0.4f, 0.3f),
                    new Vector3( 0.8f, 0.4f, 0.3f),
                    new Vector3( 0.0f, 1.2f, 0.3f),
                };
                anchors.Define(BoatAnchorId.TubMounts, tubs);

                var into = new List<Vector2>();
                Assert.IsTrue(anchors.TryGetPoints(BoatAnchorId.TubMounts, into));
                Assert.AreEqual(tubs.Length, into.Count, "every tub is appended");

                // Order is the rig's own, and it is observable: at heading North the three tubs have
                // distinct x, in the order they were defined.
                Assert.Less(into[0].x, into[1].x, "port tub is left of starboard — rig order preserved");
                Assert.AreEqual(0f, into[2].x, 1e-4f, "the centreline tub is on the centreline");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Anchors_AppendToANonEmptyList_DoNotClearIt()
        {
            var root = MakeBoat(8, false, 40f, out var directional);
            try
            {
                var anchors = new SpriteHullAnchors(directional);
                anchors.Define(BoatAnchorId.HelmSeat, new Vector3(0f, 1f, 0.5f));

                var into = new List<Vector2> { Vector2.one };
                Assert.IsTrue(anchors.TryGetPoints(BoatAnchorId.HelmSeat, into));
                Assert.AreEqual(2, into.Count, "the contract is APPEND — the caller owns the list");
                Assert.AreEqual(Vector2.one, into[0], "the caller's existing entry is untouched");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Anchors_DefineNull_ClearsTheAnchor()
        {
            var root = MakeBoat(8, false, 40f, out var directional);
            try
            {
                var anchors = new SpriteHullAnchors(directional);
                anchors.Define(BoatAnchorId.PilotStand, new Vector3(0f, 0.5f, 0.4f));
                Assert.IsTrue(anchors.Has(BoatAnchorId.PilotStand));

                anchors.Define(BoatAnchorId.PilotStand, null);
                Assert.IsFalse(anchors.Has(BoatAnchorId.PilotStand),
                    "clearing must MISS, not return a stale point — a hull swap re-defines the table");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ---- (4) the skinner actually builds one ------------------------------------------------

        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private Sprite MakeSprite()
        {
            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            var spr = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            _spawned.Add(spr);
            return spr;
        }

        private BoatVisualDef MakeVisual()
        {
            var v = ScriptableObject.CreateInstance<BoatVisualDef>();
            _spawned.Add(v);
            v.Id = "visual.seam_test";
            var facings = new Sprite[8];
            for (int i = 0; i < facings.Length; i++) facings[i] = MakeSprite();
            v.Facings = facings;
            v.FacingsAreCounterClockwise = true;      // the iso-boat convention
            v.ArtBakeElevationDegrees = 40f;
            return v;
        }

        [Test]
        public void Skinner_HandsBackAPresenter_ThatDescribesTheRigItJustBuilt()
        {
            var visual = MakeVisual();
            var root = new GameObject("Boat");
            _spawned.Add(root);

            var rig = BoatHullSkinner.Apply(root, visual, boat: null);

            Assert.IsTrue(rig.Skinned, "the fixture must actually skin, or this test is vacuous");
            Assert.IsNotNull(rig.Presenter, "a skinned hull hands back a presenter");
            Assert.AreEqual(BoatHullVariant.Sprite, rig.Presenter.Variant);
            Assert.AreSame(rig.Visual, rig.Presenter.Visual, "the presenter points at the rig's own visual child");
            Assert.AreEqual(8, rig.Presenter.FacingCount);
            Assert.IsTrue(rig.Presenter.FacingsAreCounterClockwise,
                "the per-artwork CCW fact must reach the presenter — it is how overlays pick their row");
            Assert.AreEqual(40f, rig.Presenter.BakeElevationDegrees, 1e-4f);
            Assert.AreEqual(rig.Directional.DrawnHeadingDegrees(), rig.Presenter.DrawnHeadingDegrees(), 1e-4f);
        }

        [Test]
        public void Skinner_UnskinnedHull_HasNoPresenter()
        {
            var visual = ScriptableObject.CreateInstance<BoatVisualDef>();
            _spawned.Add(visual);
            visual.Facings = System.Array.Empty<Sprite>();     // no compass ⇒ no skin

            var root = new GameObject("Boat");
            _spawned.Add(root);

            var rig = BoatHullSkinner.Apply(root, visual, boat: null);
            Assert.IsFalse(rig.Skinned);
            Assert.IsNull(rig.Presenter, "no skin, no presenter — matching Directional's own contract");
        }

        [Test]
        public void Skinner_AHullWithNoMotor_DefinesNoMotorAnchor()
        {
            // An anchor with no authored point must MISS. Returning the origin would silently pin
            // everything to the boat's centre and look almost right.
            var visual = MakeVisual();
            var root = new GameObject("Boat");
            _spawned.Add(root);

            var rig = BoatHullSkinner.Apply(root, visual, boat: null);
            Assert.IsFalse(rig.Presenter.Anchors.Has(BoatAnchorId.MotorMount),
                "this visual binds no motor sheets, so it has no motor mount");
        }

        [Test]
        public void Anchors_EveryBakedAnchorFunctionHasAnId()
        {
            // RigBaker.AnchorFunctions is the art director's set. If the baker grows one and the enum does
            // not, a mesh hull will silently be unable to express an anchor a sprite hull already bakes.
            var names = System.Enum.GetNames(typeof(BoatAnchorId));
            foreach (var baked in new[] { "motorMount", "tubMounts", "helmSeat", "tillerGrip",
                                          "haulerMount", "navMounts", "pilotStand" })
            {
                string pascal = char.ToUpperInvariant(baked[0]) + baked.Substring(1);
                Assert.Contains(pascal, names,
                    $"the baker writes anchor '{baked}' but BoatAnchorId has no member for it");
            }
        }
    }
}
