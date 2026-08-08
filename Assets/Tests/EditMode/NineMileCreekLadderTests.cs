using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The wharf ladder is BUILDER-wired, not scene-wired.</b>
    ///
    /// <para>The region's scene rebuild is the owner's and is pending, so a ladder that only existed
    /// because someone dropped it into the open scene would pass every eyeball test and vanish the moment
    /// the builder ran. These tests therefore run <see cref="NineMileCreekWharf.Place"/> itself and assert
    /// against what it BUILT — that the ladder in the fittings table became a real, registered
    /// <see cref="WharfLadder"/> at the wall's MEASURED deck height, facing the water.</para>
    ///
    /// <para>Same principle as A-2's mooring cleats: the ladder you can see is the ladder you can climb,
    /// because both come out of one table.</para>
    /// </summary>
    public class NineMileCreekLadderTests
    {
        /// <summary>A terrain whose every point sits at one height — enough to measure a level deck off,
        /// and it keeps the assertion about the WIRING rather than about the coast plan.</summary>
        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation = 3.0f;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        [SetUp]
        public void SetUp()
        {
            DestroyAnyWharf();
            BoardingLadders.Clear();
            StandableSurfaces.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyAnyWharf();
            BoardingLadders.Clear();
            StandableSurfaces.Clear();
        }

        /// <summary>
        /// Tear down whatever the builder put in the scene, BY NAME.
        ///
        /// <para>By name rather than by what was collected, deliberately: if the builder stopped making
        /// ladders the collected list would be empty and an entire wharf — decks registered as standable
        /// floor, cleats, a breakwater collider — would leak into every test that ran after this one. A
        /// fixture's cleanup must not depend on the thing it is testing having worked.</para>
        /// </summary>
        private static void DestroyAnyWharf()
        {
            for (int guard = 0; guard < 8; guard++)
            {
                GameObject root = GameObject.Find(NineMileCreekWharf.RootName);
                if (root == null) return;
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Run the real builder and hand back every ladder it made.</summary>
        private static WharfLadder[] BuildAndCollect(float deckElevation)
        {
            NineMileCreekWharf.Place(new FlatTerrain { Elevation = deckElevation });
            return Object.FindObjectsByType<WharfLadder>(FindObjectsSortMode.None);
        }

        /// <summary>How many ladders the DATA says this wharf has — the one source both the sprite and the
        /// climb come from.</summary>
        private static List<NineMileCreekWharf.Piece> TableLadders()
            => NineMileCreekWharf.Fittings().Where(f => f.Name == "ladder").ToList();

        [Test]
        public void TheTable_StillCarriesALadder()
        {
            Assert.That(TableLadders().Count, Is.GreaterThanOrEqualTo(1),
                "the quay's fittings table has no ladder at all — against a wall that bares metres of " +
                "face at spring low, that is not a nicety");
        }

        [Test]
        public void TheBuilder_MakesEveryTableLadderAClimbableOne()
        {
            var table = TableLadders();
            WharfLadder[] built = BuildAndCollect(3.0f);

            Assert.AreEqual(table.Count, built.Length,
                "every 'ladder' in the fittings table must become a real WharfLadder. A ladder drawn but " +
                "not built is a wall the fisher cannot climb; one built but not drawn is a climb up thin air.");
        }

        [Test]
        public void TheBuiltLadder_StandsWhereTheTablePutsIt()
        {
            var table = TableLadders();
            WharfLadder[] built = BuildAndCollect(3.0f);

            foreach (var piece in table)
            {
                bool matched = built.Any(l =>
                    Mathf.Abs(l.WorldPosition.x - piece.Position.x) < 1e-3f &&
                    Mathf.Abs(l.WorldPosition.y - piece.Position.y) < 1e-3f);
                Assert.IsTrue(matched,
                    $"no built ladder stands at the table's {piece.Position} — placement has drifted from " +
                    "the table, so the sprite and the climb are now two different ladders");
            }
        }

        /// <summary>The ladder's top is the wall's MEASURED deck height, not a literal — the fixed end of
        /// the tide gap. Measured means a terrain edit that lowers the fill takes the ladder down with it
        /// instead of leaving it lying about where your hands are.</summary>
        [Test]
        public void TheBuiltLadder_TopsOutAtTheMeasuredDeck()
        {
            const float deck = 2.6f;
            WharfLadder[] built = BuildAndCollect(deck);
            Assert.That(built.Length, Is.GreaterThanOrEqualTo(1));

            foreach (var l in built)
                Assert.AreEqual(deck, l.TopElevationMeters, 1e-4f,
                    "the ladder's top must be the deck the builder measured off the terrain");
        }

        /// <summary>It hangs on the MOORING face, looking out over the water — which is the side the boats
        /// lie on. A ladder facing inland is a ladder down the back of the wharf.</summary>
        [Test]
        public void TheBuiltLadder_FacesTheWater()
        {
            WharfLadder[] built = BuildAndCollect(3.0f);
            Assert.That(built.Length, Is.GreaterThanOrEqualTo(1));

            foreach (var l in built)
            {
                Assert.AreEqual(NineMileCreekWharf.MooringFaceHeadingDegrees, l.FaceHeadingDegrees, 1e-3f);
                Assert.AreEqual(NineMileCreekWharf.MooringEdgeY, l.WorldPosition.y, 1e-3f,
                    "a hung fitting pivots at the TOP and falls away from the lip, so it goes AT the lip");

                // The climber has their back to the sea, and their seat is out over the water — i.e. on
                // the far side of the lip from the concrete.
                Vector2 seat = l.WorldPosition
                             + LadderBoardingMath.SeatOffset(l.FaceHeadingDegrees, l.StandoffMetres);
                Assert.Less(seat.y, l.WorldPosition.y,
                    "the standoff must seat the climber OFF the wall over the water, not back on the deck");
            }
        }

        /// <summary>
        /// The built ladder answers the Core lookup the boarding move makes, so the Player module finds it
        /// without ever naming the World component (rule 4).
        ///
        /// <para><b>Driven through the PURE overload, not the live registry, and that is not a shortcut.</b>
        /// Unity does not call <c>OnEnable</c> on a component added in EDIT mode unless it is
        /// <c>[ExecuteAlways]</c> — which <see cref="WharfLadder"/> is not, and neither are its siblings
        /// <see cref="ShoreCleat"/> / <see cref="StandablePlatform"/>. So the registry is legitimately
        /// empty here and "it registers itself on enable" is a claim only PlayMode can make; it is made
        /// there, by <c>LadderBoardingPlayTests.AWharfLadder_RegistersItselfOnEnable</c>. What EditMode
        /// can prove is the half that is actually this fixture's business: the BUILDER produced ladders
        /// the lookup rule accepts.</para>
        ///
        /// <para>⚠ The first draft of this pair used the <c>Now</c> twin against that empty registry. The
        /// positive assertion failed honestly and is what caught it — but the negative one below
        /// <b>passed for the wrong reason</b>, because "no ladder within reach" is exactly what an empty
        /// registry says. A negative assertion over a live global is worth very little; this is why both
        /// now supply their own list.</para>
        /// </summary>
        [Test]
        public void TheBuiltLadder_AnswersTheCoreLookup()
        {
            List<IBoardingLadder> built = BuildAndCollect(3.0f).Cast<IBoardingLadder>().ToList();
            Assert.That(built.Count, Is.GreaterThanOrEqualTo(1), "the builder made no ladder to look up");

            var table = TableLadders();
            Assert.That(table.Count, Is.GreaterThanOrEqualTo(1));

            Assert.IsTrue(
                BoardingLadders.TryFindNearest(built, table[0].Position, 1f, out IBoardingLadder found),
                "a fisher standing at the ladder must find it through the Core lookup");
            Assert.IsNotNull(found);
            Assert.AreEqual(3.0f, found.TopElevationMeters, 1e-4f);
        }

        /// <summary>The reach lookup is a REACH: a fisher at the far end of an 84 m wall is not at the
        /// ladder, and must not be handed one. Supplied its own list — see the note above for why a
        /// negative assertion over the live registry proves nothing.</summary>
        [Test]
        public void ALadderAcrossTheWharf_IsNotWithinReach()
        {
            List<IBoardingLadder> built = BuildAndCollect(3.0f).Cast<IBoardingLadder>().ToList();
            Assert.That(built.Count, Is.GreaterThanOrEqualTo(1),
                "with no ladders in the list this assertion would be vacuous");

            var table = TableLadders();
            Vector2 farOff = table[0].Position + new Vector2(30f, 0f);

            Assert.IsFalse(
                BoardingLadders.TryFindNearest(built, farOff,
                                               LadderBoardingSettings.Default.LadderReachMetres, out _),
                "a ladder 30 m along the quay is not the way aboard from here — that berth steps across " +
                "or does not board at all");
        }

        /// <summary>The kit's own geometry reaches the built ladder, because the climb art was authored
        /// against it.</summary>
        [Test]
        public void TheBuiltLadder_CarriesTheKitsMeasuredGeometry()
        {
            WharfLadder[] built = BuildAndCollect(3.0f);
            foreach (var l in built)
            {
                Assert.AreEqual(LadderBoardingMath.RigRungMetres, l.RungMetres, 1e-4f,
                    "the rung spacing is WharfIso.FIT.ladder.rung — what the clip plants soles against");
                Assert.AreEqual(LadderBoardingMath.RigStandoffMetres, l.StandoffMetres, 1e-4f,
                    "the standoff is the rig's measured ladderMount().standoff, never eyeballed");
            }
        }
    }
}
