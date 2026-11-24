using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
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

        // ---- the line: once per approach, then a long cool-down --------------------------------------

        [Test]
        public void TheLine_IsThoughtOnceAsHeComesIn_NotWhileHeStays_AndNotAgainInsideItsCoolDown()
        {
            StPeters();
            var east = Walls().Single(w => w.Id == "campaign_barrier.st_peters_storm_east");
            var north = Walls().Single(w => w.Id == "campaign_barrier.st_peters_storm_north");
            foreach (var w in Walls())
                Assert.GreaterOrEqual(w.LineCooldownSeconds, 300f, w.Id + ": minutes, not a nag every minute");

            float now = 100f;
            var d = Director(Walls());
            d.Seconds = () => now;
            Vector2 inIt = east.EdgePoint + east.PushDirection * (east.FullDistanceMetres - 5f) + Vector2.up * 100f;
            Vector2 clear = east.EdgePoint + east.PushDirection * (east.StartDistanceMetres + 20f) + Vector2.up * 100f;
            var body = Hull(inIt, Vector2.zero);

            void Sit(Vector2 at, float seconds)
            {
                body.transform.position = at;
                for (int i = 0; i < 50; i++) { now += seconds / 50f; d.Step(); }
            }

            Sit(inIt, 1f);
            Assert.AreEqual(1, d.LinesSpoken, "he thinks it as he comes in");
            Sit(inIt, east.LineCooldownSeconds * 5f);
            Assert.AreEqual(1, d.LinesSpoken, "and never again while he sits in it, however long");

            // Out and straight back: a new approach, but its cool-down has only just started again.
            now = 100f;
            d.LoadWalls(Walls());
            Sit(inIt, 1f);
            Assert.AreEqual(2, d.LinesSpoken);
            Sit(clear, 1f);
            Sit(inIt, 1f);
            Assert.AreEqual(2, d.LinesSpoken, "back in inside the cool-down: nothing");
            Sit(clear, east.LineCooldownSeconds);
            Sit(inIt, 1f);
            Assert.AreEqual(3, d.LinesSpoken, "a fresh approach after the cool-down: once more");

            // Another edge is another thought: the east line's cool-down does not swallow the north's.
            Sit(clear, 1f);
            Sit(north.EdgePoint + north.PushDirection * (north.FullDistanceMetres - 5f) + Vector2.left * 120f, 1f);
            Assert.AreEqual(4, d.LinesSpoken);
        }

        // ---- the look: full where he is stopped, seen coming, gone with the flag ----------------------

        [Test]
        public void TheLook_IsFullWhereARowedBoatIsStopped_WithRainAndADarkFront_AndIsOffTheStepTheFlagIsSet()
        {
            var save = StPeters();
            foreach (var w in Walls())
            {
                Assert.Greater(w.LookFullDistanceMetres, w.FullDistanceMetres, w.Id + ": full before the push is");
                Assert.Less(w.LookFullDistanceMetres, w.StartDistanceMetres, w.Id);
                Assert.Greater(w.StormRain01, 0.5f, w.Id + ": it rains");
                Assert.Greater(w.SquallDarkness01, 0.3f, w.Id + ": the front is dark");

                var d = Director(Walls());
                Vector2 at = w.EdgePoint + w.PushDirection * (w.LookFullDistanceMetres - 1f);
                Hull(at, Vector2.zero);
                d.Step();
                Assert.AreEqual(1f, d.Look01, 1e-5f, w.Id);
                Assert.Less(d.Storm01, 1f, w.Id + ": the push is still building where the look is whole");
                Assert.AreEqual(1f, LocalWeather.Current.Strength01, 1e-5f, w.Id);
                LocalSquall squall = LocalWeather.Squall;
                Assert.IsTrue(LocalWeather.SquallActive, w.Id);
                Assert.AreEqual(1f, squall.Loom01, 1e-5f, w.Id + ": the whole view is under the front");
                Assert.AreEqual(-w.PushDirection, squall.Toward, w.Id + ": the dark lies toward the edge");
                Assert.AreEqual(w.SquallDarkness01, squall.Darkness01, 1e-5f, w.Id);
                Assert.AreEqual(w.StormRain01, squall.Rain01, 1e-5f, w.Id);
                Assert.AreEqual(w.StormRain01, LocalWeather.Rain(0f), 1e-5f, w.Id + ": rain under a dry sky");
                Assert.AreEqual(1f, LocalWeather.Rain(1f), w.Id + ": never less than the sky's own");

                save.SetFlag(GateFlag, true);
                d.Step();
                Assert.AreEqual(0f, d.Look01, w.Id);
                Assert.AreEqual(0f, d.Loom01, w.Id);
                Assert.IsFalse(LocalWeather.Active, w.Id + ": no storm is drawn or heard");
                Assert.IsFalse(LocalWeather.SquallActive, w.Id + ": no front, no rain");
                Assert.AreEqual(0f, LocalWeather.Squall.Loom01, w.Id);
                Assert.AreEqual(0f, LocalWeather.Rain(0f), w.Id);
                Assert.AreEqual(Vector2.one, LocalWeather.RainStreakScale, w.Id);
                var sim = new EnvironmentSample(new Vector2(2f, 1f), Vector2.zero, 0.4f, SeaState.Calm, 0.9f, 0.1f);
                Assert.AreEqual(sim.WindVector, LocalWeather.Shade(sim).WindVector, w.Id + ": the sim's own weather, untouched");
                Assert.AreEqual(sim.SeaState01, LocalWeather.Shade(sim).SeaState01, w.Id);
                save.SetFlag(GateFlag, false);
                Object.DestroyImmediate(d.gameObject);
            }
        }

        [Test]
        public void TheDarkFront_IsSeenFromABoatWellBeforeTheWeather_GrowsAllTheWayIn_AndNeverStandsOverTheIsland()
        {
            StPeters();
            foreach (var w in Walls())
            {
                Assert.GreaterOrEqual(w.SightDistanceMetres, w.StartDistanceMetres + 30f, w.Id + ": seen before it is felt");
                Vector2 P(float inside) => w.EdgePoint + w.PushDirection * inside;
                Assert.AreEqual(0f, w.Loom01At(P(w.SightDistanceMetres)), 1e-5f, w.Id);
                Assert.AreEqual(CampaignBarrierDef.LoomShareAtTheStartLine, w.Loom01At(P(w.StartDistanceMetres)), 1e-4f, w.Id);
                Assert.AreEqual(1f, w.Loom01At(P(w.LookFullDistanceMetres)), 1e-5f, w.Id);
                float last = 0f;
                for (float inside = w.SightDistanceMetres + 10f; inside >= 0f; inside -= 1f)
                {
                    float loom = w.Loom01At(P(inside));
                    Assert.GreaterOrEqual(loom, last - 1e-5f, $"{w.Id}: the front only grows as he closes ({inside} m)");
                    Assert.GreaterOrEqual(loom, w.Look01At(P(inside)) - 1e-5f, $"{w.Id}: never weather without its dark ({inside} m)");
                    Assert.GreaterOrEqual(w.Look01At(P(inside)), w.Storm01At(P(inside)) - 1e-5f, $"{w.Id}: never a push with no weather ({inside} m)");
                    last = loom;
                }

                // The same water in a boat and on foot: afloat he sees the front, ashore he does not.
                Vector2 seen = P((w.SightDistanceMetres + w.StartDistanceMetres) * 0.5f);
                var afloat = Director(Walls());
                Hull(seen, Vector2.zero);
                afloat.Step();
                Assert.Greater(afloat.Loom01, 0f, w.Id);
                Assert.AreEqual(0f, afloat.Look01, w.Id + ": no weather yet, only its front");
                Assert.IsFalse(LocalWeather.Active, w.Id);
                Assert.IsTrue(LocalWeather.SquallActive, w.Id);
                Assert.AreEqual(0f, LocalWeather.Squall.Rain01, w.Id + ": dry until the weather itself");
                Object.DestroyImmediate(afloat.gameObject);
                LocalWeather.Clear();

                GameServices.Helm.SetPilotedHull(null);
                GameServices.Helm.SetPlayersBoat(null);
                var walker = new GameObject("WallsWalker");
                _made.Add(walker);
                walker.transform.position = seen;
                GameServices.PlayerTransform = walker.transform;
                var ashore = Director(Walls());
                ashore.Step();
                Assert.AreEqual(0f, ashore.Loom01, w.Id + ": on foot the front is not drawn");
                Assert.IsFalse(LocalWeather.SquallActive, w.Id);
                Object.DestroyImmediate(ashore.gameObject);
                GameServices.PlayerTransform = null;
            }

            // The island's own places lie outside every front even for a boat: the berth is never dark.
            foreach (var w in Walls())
                foreach (var p in new Vector2[] { StPetersBuilder.DockZonePos, StPetersBuilder.ArrivalPos,
                                                  StPetersBuilder.DisembarkPos, new Vector2(StPetersWharf.HeadCellX, 0f),
                                                  StPetersArrivalOpening.Berth() })
                    Assert.AreEqual(0f, w.Loom01At(p), $"{w.Id} at {p}");
        }

        [Test]
        public void TheVeil_IsOffTheViewAtNoLoom_AndOverAllOfItAtFull()
        {
            const float HalfWidth = 16f, HalfHeight = 9f;
            foreach (var w in Walls())
            {
                Vector2 toward = -w.PushDirection;
                float along = SquallVeil.HalfExtentAlong(toward, HalfWidth, HalfHeight);
                Assert.AreEqual(Mathf.Abs(toward.x) > 0.5f ? HalfWidth : HalfHeight, along, 1e-4f, w.Id);
                float feather = SquallVeil.FeatherMetres(w.SquallFeather01, along);
                Assert.Greater(feather, 1f, w.Id + ": a soft front, never a drawn line");
                Assert.AreEqual(along, SquallVeil.FrontOffsetMetres(0f, along, feather), 1e-4f,
                    w.Id + ": at no loom the front's clear end is the view's far side");
                Assert.LessOrEqual(SquallVeil.FrontOffsetMetres(1f, along, feather) + feather, -along + 1e-4f,
                    w.Id + ": at full loom the whole view is under the dark");
                float half = SquallVeil.FrontOffsetMetres(CampaignBarrierDef.LoomShareAtTheStartLine, along, feather);
                Assert.Less(half, 0f, w.Id + ": at the start line the soft edge is over the boat");
                Assert.Greater(half + feather, 0f, w.Id);
            }
            Assert.Greater(SquallVeil.FeatherMetres(0f, 16f), 0f, "never a zero-length edge");
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
