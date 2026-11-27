using System;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class RoomReachTests
    {
        static readonly Vector2[] Wall =
        {
            new Vector2(0f, -1f), new Vector2(0.1f, -1f),
            new Vector2(0.1f, 1f), new Vector2(0f, 1f)
        };

        [TestCase(-1f, 0f, 1f, 0f, false)]
        [TestCase(-1f, 2f, 1f, 2f, true)]
        [TestCase(-1f, 1f, 0f, 1f, false)]
        [TestCase(0f, -2f, 0f, 2f, false)]
        [TestCase(0.05f, 0f, 0.05f, 0f, false)]
        [TestCase(-1f, 0f, -1f, 0f, true)]
        [TestCase(-1f, 0f, 0.05f, 0f, false)]
        [TestCase(0.05f, 0f, -1f, 0f, false)]
        public void ClosedPolygonBoundaryIsConservative(float ax, float ay, float bx, float by, bool clear)
        {
            var a = new Vector2(ax, ay);
            var b = new Vector2(bx, by);
            Assert.AreEqual(clear, RoomReach.Clear(a, b, Wall));
            Assert.AreEqual(clear, RoomReach.Clear(b, a, Wall));
            var reversed = (Vector2[])Wall.Clone();
            Array.Reverse(reversed);
            Assert.AreEqual(clear, RoomReach.Clear(a, b, reversed));
        }

        [Test]
        public void InvalidFactsNeverAllowAPress()
        {
            Assert.IsFalse(RoomReach.Clear(new Vector2(float.NaN, 0f), Vector2.zero, Wall));
            Assert.IsFalse(RoomReach.Clear(Vector2.zero, new Vector2(float.PositiveInfinity, 0f), Wall));
            Assert.IsFalse(RoomReach.Clear(Vector2.zero, Vector2.one, null));
            Assert.IsFalse(RoomReach.Clear(Vector2.zero, Vector2.one, new Vector2[2]));
            Assert.IsFalse(RoomReach.SameFloor(-1, -1));
            Assert.IsFalse(RoomReach.SameFloor(1, 0));
        }

        [Test]
        public void RotatedThinWallCannotBeReachedThrough()
        {
            var turn = Quaternion.Euler(0f, 0f, 37f);
            var thin = new[] { new Vector2(0f, -1f), new Vector2(0.00001f, -1f),
                               new Vector2(0.00001f, 1f), new Vector2(0f, 1f) };
            for (int i = 0; i < thin.Length; i++) thin[i] = turn * thin[i];
            Assert.IsFalse(RoomReach.Clear(turn * Vector2.left, turn * Vector2.right, thin));
        }

        [Test]
        public void ExistingWallGroupsGateARealWardrobeAndFollowWallChanges()
        {
            var house = new GameObject("House");
            try
            {
                Transform Child(string name, Transform parent)
                {
                    var child = new GameObject(name).transform;
                    child.SetParent(parent, false);
                    return child;
                }
                var props = Child("Furniture", house.transform);
                var outer = Child("Walls", house.transform);
                var partition = Child("WallsGround", props).gameObject.AddComponent<PolygonCollider2D>();
                partition.SetPath(0, Wall);
                var interior = house.AddComponent<BuildingInterior>();
                interior.Configure(null, null, props, 6f, 6f, 0, 8, 1f, 0.2f, 1f);
                foreach (var path in interior.Footprint.WallQuads(0.2f, 1f))
                    outer.gameObject.AddComponent<PolygonCollider2D>().SetPath(0, path);
                props.gameObject.SetActive(true);
                var wardrobe = Child("Wardrobe", props).gameObject.AddComponent<InteriorWardrobe>();
                wardrobe.transform.position = new Vector2(0.5f, 0f);
                wardrobe.Configure("fixture.test.wardrobe", 1.2f);
                var legal = new InteractActor(new Vector2(1f, 0f), Vector2.zero, InteractContext.OnFoot);
                var blocked = new InteractActor(new Vector2(-0.5f, 0f), Vector2.zero, InteractContext.OnFoot);
                Assert.IsTrue(wardrobe.CanReach(legal));
                Assert.IsFalse(wardrobe.CanReach(blocked));
                Assert.IsFalse(InteractResolver.Qualifies(wardrobe, blocked, Vector2.zero, -1f, out _));
                Assert.IsTrue(InteractResolver.Qualifies(wardrobe, legal, Vector2.zero, -1f, out _));
                var exact = new InteractActor(wardrobe.WorldPosition + Vector2.right * wardrobe.ReachMeters,
                                              Vector2.zero, InteractContext.OnFoot);
                Assert.IsTrue(InteractResolver.Qualifies(wardrobe, exact, Vector2.zero, -1f, out _));
                var beyond = new InteractActor(exact.Position + Vector2.right * 0.01f, Vector2.zero, InteractContext.OnFoot);
                Assert.IsFalse(InteractResolver.Qualifies(wardrobe, beyond, Vector2.zero, -1f, out _));

                int opened = 0;
                void Opened(WardrobeRequested _) => opened++;
                EventBus.Subscribe<WardrobeRequested>(Opened);
                try
                {
                    wardrobe.Interact(blocked);
                    Assert.AreEqual(0, opened);
                    wardrobe.Interact(legal);
                    Assert.AreEqual(1, opened);
                }
                finally { EventBus.Unsubscribe<WardrobeRequested>(Opened); }

                partition.enabled = false;
                Assert.IsTrue(wardrobe.CanReach(blocked));
                partition.enabled = true;
                Assert.IsFalse(wardrobe.CanReach(blocked));
                partition.transform.localPosition = Vector2.up * 2f;
                Assert.IsTrue(wardrobe.CanReach(blocked));
                partition.transform.localPosition = Vector2.zero;
                Assert.IsFalse(wardrobe.CanReach(blocked));

                Assert.IsFalse(interior.CanReachExterior(new Vector2(2f, 0f), new Vector2(4f, 0f)),
                               "an outdoor fixture cannot be used through the house wall");
                Assert.IsTrue(interior.CanReachExterior(new Vector2(0f, -2.5f), new Vector2(0f, -3.5f)),
                              "an outdoor fixture can be reached through the open doorway");
                Assert.IsTrue(interior.CanReachExterior(new Vector2(4f, 0f), new Vector2(5f, 0f)),
                              "a house elsewhere does not block an outdoor press");

                var upperProps = Child("FurnitureUpper", house.transform);
                var upperWalls = Child("WallsUpper", house.transform);
                interior.ConfigureUpperLevel(null, upperProps, upperWalls, 2.5f, outer);
                wardrobe.transform.SetParent(upperProps, true);
                Assert.IsFalse(wardrobe.CanReach(legal), "overlapping XY cannot replace floor ownership");
                wardrobe.transform.SetParent(house.transform, true);
                Assert.IsFalse(wardrobe.CanReach(legal), "unknown floor refuses");
            }
            finally { UnityEngine.Object.DestroyImmediate(house); }
        }

        [TestCase(typeof(InteriorBed))]
        [TestCase(typeof(InteriorWardrobe))]
        [TestCase(typeof(InteriorStair))]
        [TestCase(typeof(WashStationPoint))]
        [TestCase(typeof(SitPoint))]
        [TestCase(typeof(HiddenHarbours.Player.CookingStationFixture))]
        [TestCase(typeof(HiddenHarbours.Player.StowageRack))]
        [TestCase(typeof(HiddenHarbours.Player.CamperFreezer))]
        public void EachOfferedHouseFixtureSuppliesTheSpatialGate(Type fixture) =>
            Assert.IsTrue(typeof(IRoomFixtureReach).IsAssignableFrom(fixture));
    }
}
