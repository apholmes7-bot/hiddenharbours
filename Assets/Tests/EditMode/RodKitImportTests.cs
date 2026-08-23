#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The rod-kit importer against the REAL committed art + sidecars (<see cref="RodKitImporter"/>):
    /// every rod state must wire sheets AND anchors whose frame counts agree, the bobber's four states
    /// must load with their line-attach tables, the fish species must key to the real FishSpeciesDef
    /// ids, and every converted anchor must be finite and sane. This is the drift alarm between the
    /// bake (art lane) and the presenter (this lane): a re-bake that changes a frame count or renames a
    /// state fails HERE, not silently in play.
    /// </summary>
    public class RodKitImportTests
    {
        private readonly System.Collections.Generic.List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private FishSpeciesDef MakeDef(string id)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id;
            _spawned.Add(f);
            return f;
        }

        /// <summary>
        /// The seven HELD states are the shipped path and must wire from the committed bake, whole.
        ///
        /// <para>The three REST states (<c>ground</c> / <c>stowV</c> / <c>stowH</c>) joined
        /// <see cref="RodPresenterMath.RodStates"/> with the rod-continuity fix, and their sheets come
        /// from a bake that runs on the owner's machine (Hidden Harbours ▸ Art ▸ Bake Fishing Kit —
        /// the PNGs are LFS). Until that bake lands they import as null, which is the importer's
        /// null-safe greybox behaviour and NOT a pass: <see cref="RestStates_AreWholeOrAbsent"/> holds
        /// them to all-or-nothing, so a half-baked rest cannot slip through, and this test tightens by
        /// itself the moment the sheets are committed.</para>
        /// </summary>
        [Test]
        public void RodStates_EveryHeldStateWires_WithAgreedFrameCounts_AndFiniteAnchors()
        {
            RodStateVisual[] states = RodKitImporter.BuildRodStates("cane", out int[] behindDirs);
            Assert.IsNotNull(states, "the cane tier's sidecar + sheets are committed — they must import");
            Assert.AreEqual(RodKitImporter.RodStateOrder.Length, states.Length);

            Assert.IsNotNull(behindDirs, "the kit's behindDirs layering rule must survive the import");
            Assert.IsNotEmpty(behindDirs);
            foreach (int d in behindDirs) Assert.That(d, Is.InRange(0, 7));

            for (int s = 0; s < states.Length; s++)
            {
                RodStateVisual v = states[s];
                string name = RodKitImporter.RodStateOrder[s];
                if (IsRest(name) && v == null) continue;   // see the doc above + RestStates_AreWholeOrAbsent
                Assert.IsNotNull(v, $"rod state '{name}' must wire (sheet + grips + tips all committed)");
                Assert.AreEqual(name, v.State);
                Assert.Greater(v.FramesPerDir, 0, name);
                Assert.AreEqual(8 * v.FramesPerDir, v.Frames.Length, $"{name}: 8 clean direction rows");
                Assert.AreEqual(v.Frames.Length, v.GripOffsets.Length, $"{name}: one grip per cell");
                Assert.AreEqual(v.Frames.Length, v.TipOffsets.Length, $"{name}: one tip per cell");
                for (int i = 0; i < v.Frames.Length; i++)
                {
                    Assert.IsNotNull(v.Frames[i], $"{name}[{i}]: no dead cells");
                    AssertFinite(v.GripOffsets[i], $"{name} grip[{i}]");
                    AssertFinite(v.TipOffsets[i], $"{name} tip[{i}]");
                    // Sanity bounds: a grip within a body-cell of the pivot, a tip within a rod-cell.
                    Assert.Less(v.GripOffsets[i].magnitude, 3f, $"{name} grip[{i}] left the body cell");
                    Assert.Less(v.TipOffsets[i].magnitude, 4f, $"{name} tip[{i}] left the rod cell");
                }
            }
        }

        static bool IsRest(string state)
            => state == "ground" || state == "stowV" || state == "stowH";

        /// <summary>A rest either imports completely or is not there at all. The failure this forbids
        /// is the quiet middle: a rest with a sheet but no grips (or the reverse) would draw a rod
        /// pinned to nothing, and the importer's per-element degradation would never say so.</summary>
        [Test]
        public void RestStates_AreWholeOrAbsent()
        {
            RodStateVisual[] states = RodKitImporter.BuildRodStates("cane", out _);
            Assert.IsNotNull(states);
            for (int s = 0; s < states.Length; s++)
            {
                string name = RodKitImporter.RodStateOrder[s];
                if (!IsRest(name) || states[s] == null) continue;
                RodStateVisual v = states[s];
                Assert.Greater(v.FramesPerDir, 1,
                    $"{name}: a rest is an ANIMATED hand-over, so it cannot be a single cell — its " +
                    "first frame is the hold stance the rod left the hand from.");
                Assert.AreEqual(8 * v.FramesPerDir, v.Frames.Length, $"{name}: 8 clean direction rows");
                Assert.AreEqual(v.Frames.Length, v.GripOffsets.Length, $"{name}: one grip per cell");
                Assert.AreEqual(v.Frames.Length, v.TipOffsets.Length, $"{name}: one tip per cell");
                Assert.That(v.HeldFramesPerDir, Is.InRange(1, v.FramesPerDir - 1),
                    $"{name}: the hand must let go DURING the animation — not at frame 0 (a cut at the " +
                    "seam) and not never (the rod is still in her hand when it is on the ground).");
                Assert.Greater(v.RestLiftM, 0f, $"{name}: a rest holds the grip above what it rests on");
            }
        }

        /// <summary>Every state that wires describes the SAME rod. This is the owner's law made
        /// checkable: no size change, no pivot change, across any transition.</summary>
        [Test]
        public void EveryWiredState_IsTheSameRod()
        {
            RodStateVisual[] states = RodKitImporter.BuildRodStates("cane", out _);
            Assert.IsNotNull(states);
            RodStateVisual reference = null;
            foreach (RodStateVisual v in states)
            {
                if (v == null) continue;
                if (reference == null) { reference = v; continue; }
                Assert.IsTrue(RodPresenterMath.SameRod(reference, v, out string why),
                    $"rod state '{v.State}' is not the same rod as '{reference.State}': {why}");
            }
            Assert.IsNotNull(reference, "at least one state must wire");
        }

        [Test]
        public void BobberStates_AllFourWire_WithAttachPointsPerFrame()
        {
            BobberStateVisual[] states = RodKitImporter.BuildBobberStates();
            Assert.IsNotNull(states, "the bobber sidecar + sheets are committed — they must import");
            Assert.AreEqual(RodKitImporter.BobberStateOrder.Length, states.Length);
            for (int s = 0; s < states.Length; s++)
            {
                BobberStateVisual v = states[s];
                string name = RodKitImporter.BobberStateOrder[s];
                Assert.IsNotNull(v, $"bobber state '{name}' must wire");
                Assert.Greater(v.Frames.Length, 0, name);
                Assert.Greater(v.SecondsPerFrame, 0f, name);
                Assert.AreEqual(v.Frames.Length, v.LineAttachOffsets.Length, $"{name}: attach per frame");
                foreach (Vector2 a in v.LineAttachOffsets)
                {
                    AssertFinite(a, name);
                    Assert.Less(a.magnitude, 1f, $"{name}: the stem top stays within the bobber cell");
                }
            }
        }

        [Test]
        public void FishSpecies_KeyToTheRealDefIds_WithMouthsAndHeldSheets()
        {
            // The REAL starter ids (Data/Fish) — the importer must key sheets to these, never invent ids.
            FishSpeciesDef[] roster =
            {
                MakeDef("fish.atlantic_cod"), MakeDef("fish.haddock"), MakeDef("fish.mackerel"),
            };
            FishSpeciesVisual[] species = RodKitImporter.BuildFishSpecies(roster);
            Assert.IsNotNull(species);
            Assert.AreEqual(3, species.Length, "cod, haddock and mackerel are baked AND in the roster " +
                "(pollock is baked but has no species def yet — it must be skipped, not invented)");

            foreach (FishSpeciesVisual sp in species)
            {
                Assert.IsTrue(sp.FishId == "fish.atlantic_cod" || sp.FishId == "fish.haddock"
                              || sp.FishId == "fish.mackerel", sp.FishId);
                Assert.Greater(sp.ShadowFramesPerDir, 0, $"{sp.FishId}: shadow");
                Assert.Greater(sp.DartFramesPerDir, 0, $"{sp.FishId}: dart");
                Assert.Greater(sp.ThrashFramesPerDir, 0, $"{sp.FishId}: thrash");
                Assert.Greater(sp.HeldFramesPerDir, 0, $"{sp.FishId}: held (gill/tail)");
                Assert.AreEqual(8 * sp.DartFramesPerDir, sp.DartMouthOffsets.Length,
                    $"{sp.FishId}: a mouth anchor per dart cell");
                Assert.AreEqual(8 * sp.ThrashFramesPerDir, sp.ThrashMouthOffsets.Length,
                    $"{sp.FishId}: a mouth anchor per thrash cell");
                foreach (Vector2 m in sp.DartMouthOffsets) AssertFinite(m, sp.FishId);
                if (sp.FishId == "fish.atlantic_cod")
                    Assert.IsTrue(sp.TwoHanded, "the rig holds the cod with both hands (mass 3)");
                else
                    Assert.IsFalse(sp.TwoHanded, $"{sp.FishId} is a one-hand carry in the rig");
            }
        }

        [Test]
        public void LandHands_Wire_ForEveryLandCell()
        {
            Assert.IsTrue(RodKitImporter.BuildLandHands(out Vector2[] mid, out Vector2[] right,
                                                        out int framesPerDir),
                "the fisher fight-anchor sidecar is committed — the land hands must import");
            Assert.Greater(framesPerDir, 0);
            Assert.AreEqual(8 * framesPerDir, mid.Length);
            Assert.AreEqual(8 * framesPerDir, right.Length);
            for (int i = 0; i < mid.Length; i++)
            {
                AssertFinite(mid[i], $"mid[{i}]");
                AssertFinite(right[i], $"right[{i}]");
                Assert.Less(mid[i].magnitude, 3f, $"mid[{i}] stays within the body cell");
            }
        }

        [Test]
        public void NoRoster_WiresNoSpecies_AndAMissingSidecarDegradesToNull()
        {
            Assert.IsEmpty(RodKitImporter.BuildFishSpecies(null),
                "no region roster = no species entries (never invented ids)");
            // A path that does not exist degrades to a warning + null, never a throw (greybox rule).
            Sprite[] missing = RodKitImporter.LoadSingleDirFrames("Assets/_Project/Art/Fishing/Iso/Nope.png");
            Assert.IsEmpty(missing);
        }

        private static void AssertFinite(Vector2 v, string label)
        {
            Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y)
                        || float.IsInfinity(v.x) || float.IsInfinity(v.y), $"{label} must be finite");
        }
    }
}
#endif
