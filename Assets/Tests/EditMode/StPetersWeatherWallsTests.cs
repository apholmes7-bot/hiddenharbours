using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The four storms that ring St Peters for the first week, as data and arithmetic: one wall on
    /// each edge, all lifted by the one gate flag; the island, its wharf and the arrival's berth are
    /// calm; the push is a pure function of where the boat is, how she moves and the flag; the
    /// arrival sails in untouched; and a rowed dory cannot out-pull the storm.
    /// </summary>
    public class StPetersWeatherWallsTests
    {
        const string GateFlag = "st_peters.weather_turned";
        const string Region = StPetersLayerRefresh.StPetersRegionId;

        static readonly Dictionary<string, Vector2> Inward = new Dictionary<string, Vector2>
        {
            { "campaign_barrier.st_peters_storm_east", Vector2.left },
            { "campaign_barrier.st_peters_storm_west", Vector2.right },
            { "campaign_barrier.st_peters_storm_north", Vector2.down },
            { "campaign_barrier.st_peters_storm_south", Vector2.up },
        };

        sealed class FlagSave : ISaveService
        {
            readonly HashSet<string> _flags = new HashSet<string>();
            public SaveData Current { get; } = new SaveData();
            public bool GetFlag(string key) => _flags.Contains(key);
            public void SetFlag(string key, bool value) { if (value) _flags.Add(key); else _flags.Remove(key); }
            public void Save() { }
        }

        readonly List<Object> _made = new List<Object>();

        static CampaignBarrierDef[] AllWalls() =>
            AssetDatabase.FindAssets("t:CampaignBarrierDef", new[] { "Assets/_Project/Data" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<CampaignBarrierDef>)
                .Where(w => w != null).ToArray();

        static CampaignBarrierDef[] Walls() => AllWalls().Where(w => w.RegionId == Region).ToArray();

        static Rect Bounds => new Rect(StPetersBuilder.RegionWorldCenter - StPetersBuilder.RegionWorldSize * 0.5f,
                                       StPetersBuilder.RegionWorldSize);

        [SetUp] public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
            GameServices.Reset();
        }

        // ---- the data -------------------------------------------------------------------------------

        [Test]
        public void FourWalls_OnePerEdge_AllLiftedByTheOneGateFlag()
        {
            var walls = Walls();
            CollectionAssert.AreEquivalent(Inward.Keys, walls.Select(w => w.Id));
            Rect r = Bounds;
            foreach (var w in walls)
            {
                Assert.AreEqual(GateFlag, w.LiftedByFlag, w.Id);
                Assert.AreEqual(Inward[w.Id], w.PushDirection, w.Id + " pushes back onto the island");
                Assert.AreEqual(0f, w.HalfLengthMetres, w.Id + ": the whole edge");
                // The edge point lies on the region's own rectangle, on the side it faces.
                float halfAcross = Mathf.Abs(Vector2.Dot(r.size * 0.5f, Inward[w.Id]));
                Assert.AreEqual(halfAcross, -Vector2.Dot(w.EdgePoint - r.center, Inward[w.Id]), 1e-3f,
                    w.Id + " stands on its edge");
                Assert.IsNotEmpty(w.Line, w.Id);
                Assert.Less(w.Line.Length, 120, w.Id + ": one bubble's worth");
            }
        }

        [Test]
        public void TheLinesRunInwardInOrder_AndTheLimitLiesWellInsideEverySeaDoor()
        {
            float doorInnerFace = StPetersBuilder.SeaDoorInsetMetres + StPetersBuilder.EastWaterPassageBandSize.x * 0.5f;
            foreach (var w in Walls())
            {
                Assert.Greater(w.StartDistanceMetres, w.FullDistanceMetres, w.Id);
                Assert.Greater(w.FullDistanceMetres, w.LimitDistanceMetres, w.Id);
                Assert.Greater(w.LimitDistanceMetres, doorInnerFace + 20f, w.Id + ": no boat reaches a door");
                Assert.Greater(w.MaxPushAcceleration, w.PushAcceleration, w.Id);
            }
        }

        [Test]
        public void TheIsland_ItsWharf_AndTheArrivalsBerth_AreCalm()
        {
            var places = new Dictionary<string, Vector2>
            {
                { "start spawn", StPetersBuilder.StartSpawnPos },
                { "the berth", StPetersBuilder.DockZonePos },
                { "sail-home park", StPetersBuilder.ArrivalPos },
                { "step ashore", StPetersBuilder.DisembarkPos },
                { "pier head", new Vector2(StPetersWharf.HeadCellX, 0f) },
                { "the island's centre", StPetersBuilder.RegionWorldCenter },
            };
            foreach (var w in Walls())
                foreach (var p in places)
                {
                    Assert.AreEqual(0f, w.Storm01At(p.Value), $"{w.Id} at {p.Key} {p.Value}");
                    Assert.Greater(w.DistanceInside(p.Value), w.StartDistanceMetres + 20f,
                        $"{p.Key}: {w.Id} begins well clear of it");
                }

            // A calm middle is left across both axes: the opposite walls never meet.
            Rect r = Bounds;
            var byId = Walls().ToDictionary(w => w.Id);
            Assert.Less(byId["campaign_barrier.st_peters_storm_east"].StartDistanceMetres
                        + byId["campaign_barrier.st_peters_storm_west"].StartDistanceMetres, r.width - 200f);
            Assert.Less(byId["campaign_barrier.st_peters_storm_north"].StartDistanceMetres
                        + byId["campaign_barrier.st_peters_storm_south"].StartDistanceMetres, r.height - 200f);
        }

        // ---- the director: deterministic, lifted by the flag, never during the arrival ----------------

        CampaignBarrierDirector Director(params CampaignBarrierDef[] walls)
        {
            var go = new GameObject("WallsDirector");
            _made.Add(go);
            var d = go.AddComponent<CampaignBarrierDirector>();
            d.LoadWalls(walls);
            return d;
        }

        Rigidbody2D Hull(Vector2 at, Vector2 velocity)
        {
            var go = new GameObject("WallsHull");
            _made.Add(go);
            go.transform.position = at;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.mass = 4f;
            body.linearVelocity = velocity;
            GameServices.Helm.SetPilotedHull(body);
            GameServices.Helm.SetPlayersBoat(body);
            return body;
        }

        FlagSave StPeters()
        {
            var save = new FlagSave();
            GameServices.Save = save;
            GameServices.CurrentRegionId = Region;
            return save;
        }

        [Test]
        public void ThePush_IsTheSameForTheSamePlaceSpeedAndFlags_WhateverOrderTheWallsAreListedIn()
        {
            StPeters();
            var walls = Walls();
            var reversed = walls.Reverse().ToArray();
            Rect r = Bounds;
            var probes = new[]
            {
                new Vector2(r.xMax - 70f, 10f), new Vector2(r.xMin + 100f, 150f), new Vector2(-30f, r.yMax - 58f),
                new Vector2(40f, r.yMin + 90f), new Vector2(r.xMax - 60f, r.yMax - 60f), new Vector2(0f, 0f),
            };
            foreach (var p in probes)
            {
                var v = new Vector2(0.7f, -1.3f);
                var a = Director(walls); Hull(p, v); a.Step();
                float storm = a.Storm01, push = a.PushAcceleration; string standing = a.Standing?.Id;
                for (int i = 0; i < 3; i++)
                {
                    var b = Director(i % 2 == 0 ? reversed : walls); Hull(p, v); b.Step();
                    Assert.AreEqual(storm, b.Storm01, $"storm at {p}");
                    Assert.AreEqual(push, b.PushAcceleration, $"push at {p}");
                    Assert.AreEqual(standing, b.Standing?.Id, $"which storm is drawn at {p}");
                }
            }
        }

        [Test]
        public void EachWall_PushesBackOntoTheIsland_AndLiftsWhenTheGateFlagIsSet()
        {
            var save = StPeters();
            foreach (var w in Walls())
            {
                var d = Director(Walls());
                Vector2 at = w.EdgePoint + w.PushDirection * (w.FullDistanceMetres - 5f);
                var body = Hull(at, Vector2.zero);
                d.Step();
                Assert.AreSame(w, d.Standing, w.Id);
                Assert.AreEqual(1f, d.Storm01, 1e-5f, w.Id);
                Assert.AreEqual(w.PushAcceleration, d.PushAcceleration, 1e-4f, w.Id + ": its own push, nothing more");
                Assert.IsTrue(LocalWeather.Active, w.Id + ": the storm is drawn and heard");
                Assert.AreEqual(at, (Vector2)body.transform.position, "nothing is ever written to her position");

                save.SetFlag(GateFlag, true);
                Assert.IsFalse(CampaignBarrierDirector.Stands(w), w.Id);
                d.Step();
                Assert.IsNull(d.Standing, w.Id);
                Assert.AreEqual(0f, d.Storm01, w.Id);
                Assert.AreEqual(0f, d.PushAcceleration, w.Id);
                Assert.IsFalse(LocalWeather.Active, w.Id + ": lifted, the same water is fair");
                save.SetFlag(GateFlag, false);
                Object.DestroyImmediate(d.gameObject);
            }
        }

        [Test]
        public void AtACorner_TheTwoPushesAdd_PointingIn_AndNeverPastTheCap()
        {
            StPeters();
            Rect r = Bounds;
            var d = Director(Walls());
            Hull(new Vector2(r.xMax - 20f, r.yMax - 20f), new Vector2(3f, 3f));
            d.Step();
            float cap = Walls().Max(w => w.MaxPushAcceleration);
            Assert.Greater(d.PushAcceleration, 0f);
            Assert.LessOrEqual(d.PushAcceleration, cap + 1e-4f, "a boat found far out is carried back, not thrown");
        }

        [Test]
        public void TheArrival_SailsInFromTheEast_WithNoPushAndNoStorm()
        {
            StPeters();
            var east = Walls().Single(w => w.Id == "campaign_barrier.st_peters_storm_east");
            Vector2[] route = StPetersArrivalOpening.Route();
            Assert.Greater(route.Length, 1);
            // The run in starts out on the edge, inside the east storm's reach: without the opening's
            // stand-down the arrival WOULD be pushed back, so this test means something.
            Assert.Greater(east.Storm01At(route[0]), 0f, "the landfall lies in the east storm's reach");
            Assert.Greater(east.Storm01At(StPetersBuilder.FromEastWaterArrivalPos), 0f);

            GameServices.OpeningCinematicRunning = true;
            var d = Director(Walls());
            var points = new List<Vector2> { StPetersBuilder.FromEastWaterArrivalPos };
            for (int i = 0; i + 1 < route.Length; i++)
                for (int k = 0; k <= 10; k++) points.Add(Vector2.Lerp(route[i], route[i + 1], k / 10f));
            foreach (var p in points)
            {
                Hull(p, new Vector2(-3f, 0f));
                d.Step();
                Assert.AreEqual(0f, d.PushAcceleration, $"the arrival at {p} is a passenger's crossing");
                Assert.AreEqual(0f, d.Storm01, $"{p}");
                Assert.IsFalse(LocalWeather.Active, $"{p}");
            }

            // Handed over: the berth and the planks he is put down on are calm, so nothing starts up.
            GameServices.OpeningCinematicRunning = false;
            foreach (var p in new[] { StPetersArrivalOpening.Berth(), StPetersArrivalOpening.StepAshore() })
            {
                Hull(p, Vector2.zero);
                d.Step();
                Assert.AreEqual(0f, d.Storm01, $"after the hand-over at {p}");
                Assert.AreEqual(0f, d.PushAcceleration);
            }
        }

        // ---- a rowed dory cannot out-pull the storm --------------------------------------------------

        [Test]
        public void TheStormsOwnPush_BeatsTheDorysHardestPull_BeforeTheLimitLine()
        {
            var dory = AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/Dory.asset");
            Assert.IsNotNull(dory);
            // Both oars pulling, averaged over a stroke at the strongest committed rowing style (an
            // unstyled stroke pulls 1x), on the body mass the controller gives her. Water drag only
            // takes away from this, so it is the most she can ask of the water for longer than a stroke.
            float styleScale = AssetDatabase.FindAssets("t:RowingStyleDef", new[] { "Assets/_Project/Data" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<RowingStyleDef>)
                .Where(s => s != null).Select(s => s.AverageThrustScale).DefaultIfEmpty(1f).Max();
            float pull = BoatController.OarThrust(1f, 1f, dory.OarPower) * BoatController.ForceFeelScale
                         * Mathf.Max(1f, styleScale) / BoatController.BodyMassFor(dory.MassKg);
            foreach (var w in Walls())
                Assert.Greater(w.PushAcceleration, pull,
                    $"{w.Id}: at full storm the weather alone sets a rowed dory back ({w.PushAcceleration} " +
                    $"against her {pull} m/s²), before the limit line is ever reached");
        }
    }
}
