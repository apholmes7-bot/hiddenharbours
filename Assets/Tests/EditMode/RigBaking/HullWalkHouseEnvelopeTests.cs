using System;
using System.Collections.Generic;
using HiddenHarbours.Boats;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    public sealed class HullWalkHouseEnvelopeTests
    {
        static Vector2[] Square(float x0, float x1) => new[]
        {
            new Vector2(x0, 0), new Vector2(x1, 0), new Vector2(x1, 1), new Vector2(x0, 1)
        };

        [Test]
        public void IndoorClimb_JoinsDisjointSolesIntoOneEnvelope()
        {
            var strip = new[] { new Vector2(.75f, .25f), new Vector2(2.25f, .25f),
                new Vector2(2.25f, .75f), new Vector2(.75f, .75f) };
            var ring = HullWalkSidecarImporter.HouseEnvelope("joined_hull",
                new[] { Square(0, 1), Square(2, 3) }, new[] { strip });
            Assert.That(DeckAreaMath.Contains(ring, new Vector2(.5f, .5f)), Is.True);
            Assert.That(DeckAreaMath.Contains(ring, new Vector2(2.5f, .5f)), Is.True);
            Assert.That(DeckAreaMath.Contains(ring, new Vector2(1.5f, .5f)), Is.True);
        }

        [Test]
        public void DisjointSolesWithoutClimb_RefuseByHullAndFloorCount()
        {
            var error = Assert.Throws<FormatException>(() => HullWalkSidecarImporter.HouseEnvelope("unjoined_hull",
                new[] { Square(0, 1), Square(2, 3) }, Array.Empty<Vector2[]>()));
            Assert.That(error.Message, Is.EqualTo("unjoined_hull: the soles and the indoor climbs do not join into one house envelope (2 floors)"));
        }

        [Test]
        public void OverlappingSoles_KeepTheirExactUnionWithoutAddingTheClimb()
        {
            var soles = new[] { Square(0, 1), Square(.5f, 1.5f) };
            var original = HullWalkPolygonUnion.Union(soles)[0];
            var ring = HullWalkSidecarImporter.HouseEnvelope("overlapping_hull", soles, new[] { Square(3, 4) });
            CollectionAssert.AreEqual(original, ring);
            Assert.That(DeckAreaMath.Contains(ring, new Vector2(3.5f, .5f)), Is.False);
        }

        [Test]
        public void OneLevelsDisconnectedFloor_StillRefusesWithTheOriginalMessage()
        {
            var error = Assert.Throws<FormatException>(() => HullWalkPolygonUnion.Union(new[] { Square(0, 1), Square(2, 3) }));
            Assert.That(error.Message, Is.EqualTo("walk polygons: disconnected floor requires separate levels"));
        }

        [Test]
        public void SeparateCompartmentWithDeckLadder_KeepsTheLargestRingAndNamesTheIsland()
        {
            var house = Square(0, 3);
            var island = Square(5, 6);
            var messages = new List<string>();
            var ring = HullWalkSidecarImporter.HouseEnvelope("two_compartments", new[] { house, island },
                Array.Empty<Vector2[]>(), new[] { ("crew_ladder", 0, 1, island) },
                new[] { ("salon", house), ("crew_mess", island) }, messages.Add);
            CollectionAssert.AreEqual(house, ring);
            Assert.That(messages.Count, Is.EqualTo(1));
            Assert.That(messages[0], Does.Contain("crew_mess").And.Contain("crew_ladder").And.Contain("1 m^2"));
        }

        [Test]
        public void SeparateCompartmentWithoutDeckEntry_KeepsTheExactRefusal()
        {
            var house = Square(0, 3);
            var island = Square(5, 6);
            foreach (var entries in new[] {
                Array.Empty<(string, int, int, Vector2[])>(),
                new[] { ("indoor_stair", 1, 2, island) },
                new[] { ("deck_to_deck", 0, 0, island) },
                new[] { ("missed_ladder", 0, 1, Square(8, 9)) } })
            {
                var error = Assert.Throws<FormatException>(() => HullWalkSidecarImporter.HouseEnvelope(
                    "two_compartments", new[] { house, island }, Array.Empty<Vector2[]>(), entries));
                Assert.That(error.Message, Is.EqualTo("two_compartments: the soles and the indoor climbs do not join into one house envelope (2 floors)"));
            }
        }
    }
}
