using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b><see cref="HullMeshDef.CutawayForDeck"/>, on its own</b> — the one lookup the whole cutaway
    /// turns on, tested without a rig, an asset or a scene.
    ///
    /// <para>Every uncertain branch of it answers <see cref="HullMeshDef.Cut.None"/>, whose level is
    /// <b>0</b> — which is also the gate's "draw her whole exterior". That is the design rather than
    /// a coincidence, and it is what these cases pin: a cutaway that does not happen is a missing
    /// feature, one that happens to the wrong room is a broken boat, so every uncertain answer has to
    /// fall the same way.</para>
    ///
    /// <para>The LID half is the coordinator's ruling of 2026-08-27 — a cut takes its declared
    /// ceiling, one hop. The table below deliberately mixes the three real shapes: a level with a lid
    /// (<c>below</c> → <c>main_deck</c>), a level without one (<c>house</c>, which folds its own boat
    /// deck into its own tag), and an OPEN level that is itself a lid and may never be cut into.</para>
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
                new HullMeshDef.LevelTag { LevelId = "below", DeckId = "below_sole", Tag = 4, Enclosed = true,
                                           LidLevelId = "main_deck", LidTag = 1 },
                new HullMeshDef.LevelTag { LevelId = "main_deck", DeckId = "main_deck", Tag = 1, Enclosed = false },
            };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_def);

        [Test]
        public void AnEnclosedLevel_AnswersItsOwnTag()
        {
            Assert.AreEqual(2, _def.CutawayForDeck("house_sole").Level);
            Assert.AreEqual(3, _def.CutawayForDeck("bridge_sole").Level);
            Assert.AreEqual(4, _def.CutawayForDeck("below_sole").Level);
        }

        /// <summary>
        /// <b>The ruling: a cut takes its declared ceiling.</b> Standing in the engine space, the main
        /// deck over your head comes off with it — otherwise the gate engages and you look at a whole
        /// boat, which is the defect this round exists to close.
        /// </summary>
        [Test]
        public void ALevelWithADeclaredLid_TakesItInTheSameAnswer()
        {
            HullMeshDef.Cut cut = _def.CutawayForDeck("below_sole");
            Assert.AreEqual(4, cut.Level);
            Assert.AreEqual(1, cut.Lid, "below's ceiling record names main_deck as its lid.");
            Assert.IsTrue(cut.Opens);
        }

        /// <summary>
        /// <b>No lid is the ORDINARY answer, not a gap.</b> Both ships' house and bridge already fold
        /// their own lid into their own tag — the boat deck is tagged <c>house</c>, the wheelhouse
        /// deckhead is tagged <c>bridge</c> — so one tag does the work and a hop would take a second
        /// room off for nothing.
        /// </summary>
        [Test]
        public void ALevelWithNoLid_TakesNothingExtra()
        {
            HullMeshDef.Cut cut = _def.CutawayForDeck("house_sole");
            Assert.AreEqual(2, cut.Level);
            Assert.AreEqual(0, cut.Lid);
        }

        [Test]
        public void AnOpenLevel_AnswersNone_BecauseCuttingItWouldBeCuttingTheSky()
        {
            HullMeshDef.Cut cut = _def.CutawayForDeck("main_deck");
            Assert.IsFalse(cut.Opens);
            Assert.AreEqual(0, cut.Level);
            Assert.AreEqual(0, cut.Lid,
                "main_deck is a working deck with a declared open sky. It is a real level of a real " +
                "def — the trawler and the packet both publish one at the same z as their house — " +
                "and it must never be cut INTO. Being somebody else's lid is a different job.");
        }

        /// <summary>
        /// <b>The asymmetry, stated as a test because it looks like a bug otherwise.</b> An open level
        /// may not be entered into a cut, and may perfectly well BE a lid. All three lids in batch 1
        /// are open decks.
        /// </summary>
        [Test]
        public void AnOpenLevelIsRefusedAsATarget_AndStillServesAsSomebodyElsesLid()
        {
            Assert.IsFalse(_def.CutawayForDeck("main_deck").Opens);
            Assert.AreEqual(1, _def.CutawayForDeck("below_sole").Lid);
        }

        [Test]
        public void AnUnknownOrEmptyDeckId_AnswersNoneRatherThanGuessing()
        {
            Assert.IsFalse(_def.CutawayForDeck("wheelhouse").Opens);   // the rig's name, not the def's
            Assert.IsFalse(_def.CutawayForDeck("").Opens);
            Assert.IsFalse(_def.CutawayForDeck(null).Opens);
        }

        [Test]
        public void TheMatchIsOrdinalAndExact()
        {
            Assert.IsFalse(_def.CutawayForDeck("House_Sole").Opens,
                "Level ids are data keys, not prose. A case-insensitive match here would be one " +
                "step from a fuzzy one, and 'house' vs 'house_sole' is exactly the mismatch this " +
                "lookup exists to catch loudly.");
            Assert.IsFalse(_def.CutawayForDeck("house_sole ").Opens);
        }

        [Test]
        public void AHullWithNoTableAtAll_AnswersNoneForEverything()
        {
            var bare = ScriptableObject.CreateInstance<HullMeshDef>();
            try
            {
                Assert.IsFalse(bare.CutawayForDeck("house_sole").Opens,
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

        [Test]
        public void CutNone_IsTheSameValueAsTheGateBeingOff()
        {
            Assert.AreEqual(0, HullMeshDef.Cut.None.Level);
            Assert.AreEqual(0, HullMeshDef.Cut.None.Lid);
            Assert.IsFalse(HullMeshDef.Cut.None.Opens,
                "0 is 'show the exterior' in the shader too. One value, one meaning, both sides of " +
                "the seam — which is what makes every refusal above land on the shipped picture.");
        }
    }
}
