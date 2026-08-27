using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b><see cref="HullMeshDef.CutawayTagForDeck"/>, on its own</b> — the one lookup the whole
    /// cutaway turns on, tested without a rig, an asset or a scene.
    ///
    /// <para>Every branch of it answers <b>0</b>, and 0 is also the gate's "draw her whole
    /// exterior". That is the design rather than a coincidence, and it is what these cases pin: a
    /// cutaway that does not happen is a missing feature, one that happens to the wrong room is a
    /// broken boat, so every uncertain answer has to fall the same way.</para>
    /// </summary>
    public sealed class HullMeshCutawayTableTests
    {
        private HullMeshDef _def;

        [SetUp]
        public void SetUp()
        {
            _def = ScriptableObject.CreateInstance<HullMeshDef>();
            _def.LevelTags = new[]
            {
                new HullMeshDef.LevelTag { LevelId = "house", DeckId = "house_sole", Tag = 2, Enclosed = true },
                new HullMeshDef.LevelTag { LevelId = "bridge", DeckId = "bridge_sole", Tag = 3, Enclosed = true },
                new HullMeshDef.LevelTag { LevelId = "below", DeckId = "below_sole", Tag = 4, Enclosed = true },
                new HullMeshDef.LevelTag { LevelId = "main_deck", DeckId = "main_deck", Tag = 1, Enclosed = false },
            };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_def);

        [Test]
        public void AnEnclosedLevel_AnswersItsOwnTag()
        {
            Assert.AreEqual(2, _def.CutawayTagForDeck("house_sole"));
            Assert.AreEqual(3, _def.CutawayTagForDeck("bridge_sole"));
            Assert.AreEqual(4, _def.CutawayTagForDeck("below_sole"));
        }

        [Test]
        public void AnOpenLevel_AnswersZero_BecauseCuttingItWouldBeCuttingTheSky()
        {
            Assert.AreEqual(0, _def.CutawayTagForDeck("main_deck"),
                "main_deck is a working deck with a declared open sky. It is a real level of a real " +
                "def — the trawler and the packet both publish one at the same z as their house — " +
                "and it must never be cut.");
        }

        [Test]
        public void AnUnknownOrEmptyDeckId_AnswersZeroRatherThanGuessing()
        {
            Assert.AreEqual(0, _def.CutawayTagForDeck("wheelhouse"));   // the rig's name, not the def's
            Assert.AreEqual(0, _def.CutawayTagForDeck(""));
            Assert.AreEqual(0, _def.CutawayTagForDeck(null));
        }

        [Test]
        public void TheMatchIsOrdinalAndExact()
        {
            Assert.AreEqual(0, _def.CutawayTagForDeck("House_Sole"),
                "Level ids are data keys, not prose. A case-insensitive match here would be one " +
                "step from a fuzzy one, and 'house' vs 'house_sole' is exactly the mismatch this " +
                "lookup exists to catch loudly.");
            Assert.AreEqual(0, _def.CutawayTagForDeck("house_sole "));
        }

        [Test]
        public void AHullWithNoTableAtAll_AnswersZeroForEverything()
        {
            var bare = ScriptableObject.CreateInstance<HullMeshDef>();
            try
            {
                Assert.AreEqual(0, bare.CutawayTagForDeck("house_sole"),
                    "Most of the fleet was baked before the cutaway kit and has no table. That is " +
                    "the ordinary case, not a fault.");
                Assert.IsFalse(bare.CarriesLevelTags);
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        [Test]
        public void ATableWithNoMesh_DoesNotClaimToCarryTags()
        {
            Assert.IsFalse(_def.CarriesLevelTags,
                "The table and the tagged MESH are two separate facts and both have to hold. A def " +
                "re-serialised from a newer rig with an older mesh sub-asset is precisely the " +
                "stale-bake state this repo keeps meeting, and it must not read as ready.");
        }
    }
}
