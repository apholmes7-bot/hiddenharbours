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

        [Test]
        public void RodStates_AllSevenWire_WithAgreedFrameCounts_AndFiniteAnchors()
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
