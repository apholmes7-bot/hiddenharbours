using System;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersVillageStations;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class StPetersVillageStationTests
    {
        static Ground FlatGround()
        {
            return new Ground { X0 = -10, Y1 = 10, Mpp = 1, Min = 0, Max = 6,
                Height = new TerrainPlanPng.Image { Width = 20, Height = 20, Channels = 1, BitDepth = 16,
                    Samples = Enumerable.Repeat((ushort)65535, 400).ToArray() },
                Path = new TerrainPlanPng.Image { Width = 20, Height = 20, Channels = 4, BitDepth = 8,
                    Samples = Enumerable.Repeat((ushort)255, 1600).ToArray() } };
        }
        static GroundPolygon Box(float x0, float y0, float x1, float y1) => new GroundPolygon(new[] {
            new Vector2(x0,y0), new Vector2(x1,y0), new Vector2(x1,y1), new Vector2(x0,y1) });

        [Test]
        public void BuilderBindings_UseCheckedStands_AndKeepHeldSites()
        {
            Invalidate();
            Assert.That(StPetersInhabitants.Dooryard(StPetersBuilder.SchoolPos), Is.EqualTo(new Vector2(-11.4f,29.985f)));
            Assert.That(StPetersInhabitants.Dooryard(StPetersBuilder.SageCottagePos), Is.EqualTo(new Vector2(-21.5f,35.38f)));
            Assert.That(StPetersInhabitants.Dooryard(StPetersBuilder.GeneralStorePos), Is.EqualTo(Current[StPetersRoutines.StationStoreCounter].Position));
            Assert.That((Vector2)StPetersBuilder.GeneralStoreCounterPos, Is.EqualTo(Current["GeneralStoreCounter"].Position));
            for (int slot = -1; slot <= 1; slot++)
                Assert.That(StPetersRoutines.CustomerSlot(slot), Is.EqualTo(Current[StPetersRoutines.CustomerSlotIds[slot+1]].Position));
            Assert.Throws<ArgumentOutOfRangeException>(() => StPetersRoutines.CustomerSlot(2));
            foreach (Vector3 held in new[] { StPetersBuilder.RedSaltboxPos, StPetersBuilder.WhiteFarmhousePos, StPetersShops.PostOfficePos })
            {
                Assert.That(TryDooryard(held,out _), Is.False);
                Vector2 site = held;
                Assert.That(StPetersInhabitants.Dooryard(held), Is.EqualTo(site + (StPetersBuilder.VillageGreen-site).normalized * StPetersInhabitants.DoorstepMetres));
            }
        }

        [Test]
        public void CommittedFrames_KeepStreetRowAndResolveBothOwnWalks()
        {
            var rows = Read();
            Assert.That(rows.Count, Is.EqualTo(7));
            string[] ids = { StPetersRoutines.StationStoreCounter, StPetersRoutines.StationStoreCustomerA,
                StPetersRoutines.StationStoreCustomerB, StPetersRoutines.StationStoreCustomerC };
            Vector2[] positions = { new Vector2(2.032163f,27.905405f), new Vector2(.9467809f,24.967386f),
                new Vector2(1.8450282f,24.911245f), new Vector2(2.7432756f,24.855104f) };
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.That(rows[ids[i]].Street, Is.True);
                Assert.That(rows[ids[i]].Fallback, Is.False);
                Assert.That(rows[ids[i]].Position, Is.EqualTo(positions[i]));
                Assert.That(rows[ids[i]].Candidate, Is.EqualTo(positions[i]));
            }
            Assert.That(rows[StPetersRoutines.LaneSchool].Position, Is.EqualTo(new Vector2(-11.4f,29.985f)));
            Assert.That(rows[StPetersRoutines.StationSageDooryard].Position, Is.EqualTo(new Vector2(-21.5f,35.38f)));
            Assert.That(rows[StPetersRoutines.LaneSchool].Fallback, Is.True);
            Assert.That(rows[StPetersRoutines.StationSageDooryard].Fallback, Is.True);
            Assert.That(rows["GeneralStoreCounter"].Position, Is.EqualTo(new Vector2(1.9385955f,26.408325f)));
        }

        [Test]
        public void StreetRule_RejectsWetRigAndEveryLot_WithoutWalkFallback()
        {
            var ground = FlatGround();
            var walk = new[] { new Vector2(0,-3), new Vector2(0,3) };
            var areas = new[] { new Area { Lot = "general_store", Polygon = Box(-1,-1,1,1) } };
            var rigs = new[] { Box(-1,-1,1,1) };
            foreach (string id in StPetersRoutines.CustomerSlotIds.Concat(new[] { StPetersRoutines.StationStoreCounter }))
            {
                Assert.Throws<StPetersLayerRefresh.Refusal>(() => Resolve(id,"general_store",Vector2.zero,walk,1,areas,Array.Empty<GroundPolygon>(),ground));
                Assert.Throws<StPetersLayerRefresh.Refusal>(() => Resolve(id,"general_store",Vector2.zero,walk,1,Array.Empty<Area>(),rigs,ground));
                ground.Max = 2.2f;
                Assert.Throws<StPetersLayerRefresh.Refusal>(() => Resolve(id,"general_store",Vector2.zero,walk,1,Array.Empty<Area>(),Array.Empty<GroundPolygon>(),ground));
                ground.Max = 6;
                var clear = Resolve(id,"general_store",Vector2.zero,null,0,Array.Empty<Area>(),Array.Empty<GroundPolygon>(),ground);
                Assert.That(clear.Position, Is.EqualTo(Vector2.zero));
                Assert.That(clear.Fallback, Is.False);
            }
        }

        [Test]
        public void OwnWalkRule_RequiresPaintAndOwnLot_AndChoosesNearestLegalPoint()
        {
            var ground = FlatGround();
            var walk = new[] { new Vector2(0,-4), new Vector2(0,4) };
            var areas = new[] { new Area { Lot = "school", Polygon = Box(-2,-5,2,5) } };
            var row = Resolve("lane_school","school",new Vector2(3,1),walk,2,areas,Array.Empty<GroundPolygon>(),ground);
            Assert.That(row.Position, Is.EqualTo(new Vector2(0,1)));
            Assert.That(row.Fallback, Is.True);
            Array.Clear(ground.Path.Samples,0,ground.Path.Samples.Length);
            Assert.Throws<StPetersLayerRefresh.Refusal>(() => Resolve("lane_school","school",Vector2.zero,walk,2,areas,Array.Empty<GroundPolygon>(),ground));
            ground = FlatGround();
            Assert.Throws<StPetersLayerRefresh.Refusal>(() => Resolve("lane_school","school",Vector2.zero,walk,2,Array.Empty<Area>(),Array.Empty<GroundPolygon>(),ground));
        }
    }
}
