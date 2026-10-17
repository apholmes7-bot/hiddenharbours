using System.Linq;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    public sealed class HullWalkImportParityTests
    {
        [Test]
        public void BoxWalk_EqualsHandBuiltDeckRoomHelmAndStair()
        {
            using var host = RigScriptHostFactory.Create();
            var rig = HullWalkFixture.Extract(host);
            var deck = ScriptableObject.CreateInstance<BoatDeckDef>();
            var room = ScriptableObject.CreateInstance<BoatInteriorDef>();
            try
            {
                var result = HullWalkSidecarImporter.Populate(rig.Walk, rig, "boxWalkRig", deck, room);
                Assert.That(result.Ok, Is.True);
                Assert.That(deck.Id, Is.EqualTo("deck.box_walk"));
                Assert.That(deck.Areas.Length, Is.EqualTo(1));
                var expected = DeckArea.From("deck", DeckAreaKind.Deck, new[] {
                    new Vector3(-2,-3,0),new Vector3(2,-3,0),new Vector3(2,3,0),new Vector3(-2,3,0) });
                CollectionAssert.AreEqual(expected.Outline, deck.Areas[0].Outline);
                Assert.That(DeckAreaMath.HeightAt(deck.Areas[0].HeightPlane, new Vector2(1,1)), Is.Zero);
                Assert.That(DeckAreaMath.Contains(deck.Areas[0], Vector2.zero), Is.False);
                Assert.That(deck.HelmStationLocalMeters, Is.EqualTo(new Vector3(.5f,1,2)));
                Assert.That(deck.HasHelmStation, Is.True);
                Assert.That(deck.Cleats.Single().PositionMeters, Is.EqualTo(new Vector3(0,-3,0)));
                Assert.That(room.Levels.Select(l => l.Id), Is.EqualTo(new[] { "room", "top" }));
                Assert.That(room.Levels.Select(l => l.SoleZMeters), Is.EqualTo(new[] { 1f, 2f }));
                foreach (var level in room.Levels)
                {
                    Assert.That(DeckAreaMath.Contains(level.Outline, new Vector2(-.75f,1)), Is.True);
                    Assert.That(DeckAreaMath.Contains(level.Outline, new Vector2(.75f,1)), Is.True);
                    Assert.That(DeckAreaMath.Contains(level.Outline, new Vector2(1.5f,1)), Is.False);
                }
                Assert.That(room.Door.ThresholdPoint, Is.EqualTo(new Vector3(0,-2,1)));
                Assert.That(room.Door.ClearWidthMeters, Is.EqualTo(1));
                Assert.That(room.Door.ClearHeightMeters, Is.EqualTo(1));
                Assert.That(room.Door.Mechanism, Is.EqualTo(BoatInteriorDoorMechanism.Sliding));
                Assert.That(room.Anchors.Single().ReachPoint, Is.EqualTo(new Vector3(.5f,1,2)));
                var route = room.Routes.Single();
                Assert.That(route.FromPoint, Is.EqualTo(new Vector3(0,-3,0)));
                Assert.That(route.ToPoint, Is.EqualTo(new Vector3(0,-2,1)));
                Assert.That(route.FromLevel, Is.EqualTo("deck"));
                Assert.That(route.ToLevel, Is.EqualTo("room"));
                Assert.That(route.TotalRiseMeters, Is.EqualTo(1));
                Assert.That(route.Exterior, Is.False);
                Assert.That(route.Placed, Is.True);
            }
            finally { Object.DestroyImmediate(deck); Object.DestroyImmediate(room); }
        }

        [Test]
        public void LeaflessDoorway_RefusesByHullAndDoorAndBuildsNoDoor()
        {
            using var host = RigScriptHostFactory.Create();
            var rig = HullWalkFixture.Extract(host);
            var walk = HullWalkFixture.Read(HullWalkFixture.WithLeafless);
            var deck = ScriptableObject.CreateInstance<BoatDeckDef>();
            var room = ScriptableObject.CreateInstance<BoatInteriorDef>();
            try
            {
                var result = HullWalkSidecarImporter.Populate(walk, rig, "boxWalkRig", deck, room);
                Assert.That(result.Refusals.Single(), Is.EqualTo("box_walk: house.doors.open_arch: leafless doorway: not imported until batch 1 (needs #916's door runtime)"));
                Assert.That(result.Ok, Is.False);
                Assert.That(room.AdditionalDoors, Is.Empty);
                Assert.That(room.Door.Id, Is.EqualTo("entry"));
            }
            finally { Object.DestroyImmediate(deck); Object.DestroyImmediate(room); }
        }

        [Test]
        public void PolygonUnion_PreservesConcavityAndEnclosedHole()
        {
            Vector2[] Rect(float x0,float y0,float x1,float y1) => new[] { new Vector2(x0,y0),new Vector2(x1,y0),new Vector2(x1,y1),new Vector2(x0,y1) };
            var concave = HullWalkPolygonUnion.Union(new[] { Rect(0,0,2,1),Rect(0,0,1,2) });
            Assert.That(DeckAreaMath.Contains(concave[0], new Vector2(1.5f,1.5f)), Is.False);
            var hole = HullWalkPolygonUnion.Union(new[] { Rect(0,0,3,1),Rect(0,2,3,3),Rect(0,1,1,2),Rect(2,1,3,2) });
            Assert.That(hole.Length, Is.EqualTo(2));
            Assert.That(DeckAreaMath.Contains(hole[1], new Vector2(1.5f,1.5f)), Is.True);
        }

        [Test]
        public void AnotherAreaCanFillAHoleInTheUnion()
        {
            Vector2[] Rect(float n) => new[] { new Vector2(-n,-n),new Vector2(n,-n),new Vector2(n,n),new Vector2(-n,n) };
            var rings = HullWalkPolygonUnion.Union(new[] {
                new HullWalkArea { Polygon = Rect(2), Holes = new[] { Rect(1) } },
                new HullWalkArea { Polygon = Rect(1), Holes = System.Array.Empty<Vector2[]>() } });
            Assert.That(rings.Length, Is.EqualTo(1));
            Assert.That(DeckAreaMath.Contains(rings[0], Vector2.zero), Is.True);
        }

        [Test]
        public void RampWithFlatEnds_PreservesClampedHeightAndPlayerCentreOutline()
        {
            var area = new HullWalkArea { Id = "ramp", Polygon = new[] {
                new Vector2(-1,-2),new Vector2(1,-2),new Vector2(1,2),new Vector2(-1,2) },
                Holes = System.Array.Empty<Vector2[]>(), Ramp = new HullWalkRamp { Axis = "y", A0 = -1, A1 = 1, Z0 = 0, Z1 = 2 } };
            var pieces = HullWalkSidecarImporter.DeckAreas(area).ToArray();
            Assert.That(pieces.Length, Is.EqualTo(3));
            foreach (float y in new[] { -1.5f, -.5f, .5f, 1.5f })
            {
                var p = new Vector2(.9f,y);
                var piece = pieces.Single(a => DeckAreaMath.Contains(a, p));
                Assert.That(DeckAreaMath.HeightAt(piece.HeightPlane, p), Is.EqualTo(area.HeightAt(p)).Within(.00001));
            }
        }
    }
}
