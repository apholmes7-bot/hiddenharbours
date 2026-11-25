using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE WHOLE WALL IS A DOOR, AND DOORS DO NOT TELEPORT</b> (ADR 0057; owner ruling 2026-11-15).
    /// The pure world frame: a crossing maps a mover to the SAME world point on the other side, both ways,
    /// with no shortening and no bounce; touching edges are seams, area overlaps are faults.
    /// </summary>
    public sealed class WorldFrameTests
    {
        const float H = 1f;

        static RegionFrame Frame(string id, Vector2 origin, Vector2 size) =>
            new RegionFrame(id, origin, new Rect(-size * 0.5f, size));

        static WorldFrame TwoSideBySide(out RegionFrame west, out RegionFrame east)
        {
            west = Frame("region.west", Vector2.zero, new Vector2(100f, 60f));
            east = Frame("region.east", new Vector2(100f, -10f), new Vector2(100f, 80f));
            var frame = new WorldFrame();
            frame.Register(west);
            frame.Register(east);
            return frame;
        }

        [Test]
        public void MapAcross_IsTheSameWorldPoint_BothWays_AndRoundTripsExactly()
        {
            TwoSideBySide(out RegionFrame w, out RegionFrame e);
            var local = new Vector2(51f, 12.5f);
            Vector2 there = WorldFrame.MapAcross(w, e, local);
            Assert.That(e.ToWorld(there), Is.EqualTo(w.ToWorld(local)), "same world point");
            Assert.That(WorldFrame.MapAcross(e, w, there), Is.EqualTo(local), "round trip");
        }

        [Test]
        public void Resolve_InsideOrWithinTheHysteresisBand_Stays()
        {
            WorldFrame f = TwoSideBySide(out _, out _);
            Assert.That(f.Resolve("region.west", new Vector2(10f, 0f), H).Kind, Is.EqualTo(CrossingKind.Stay));
            Assert.That(f.Resolve("region.west", new Vector2(50.9f, 0f), H).Kind, Is.EqualTo(CrossingKind.Stay),
                "less than the hysteresis past the edge is still home");
        }

        [Test]
        public void Resolve_PastTheEdge_CrossesAtTheSamePositionAlongTheEdge()
        {
            WorldFrame f = TwoSideBySide(out RegionFrame w, out RegionFrame e);
            foreach (float y in new[] { -29f, -10f, 0f, 17.25f, 29f })
            {
                var local = new Vector2(51.2f, y);
                CrossingDecision d = f.Resolve(w.RegionId, local, H);
                Assert.That(d.Kind, Is.EqualTo(CrossingKind.Cross), $"y {y}");
                Assert.That(d.To.RegionId, Is.EqualTo(e.RegionId));
                Assert.That(d.Edge, Is.EqualTo(RegionEdge.East));
                Vector2 landed = local + d.LocalShift;
                Assert.That(Vector2.Distance(e.ToWorld(landed), w.ToWorld(local)), Is.LessThan(1e-4f),
                    $"y {y}: lands at the same world point, no jump along or across the edge");
            }
        }

        [Test]
        public void ACrossing_NeverShortensTheTrip_AndArrivesInsideTheHysteresisBand_SoItCannotBounce()
        {
            WorldFrame f = TwoSideBySide(out RegionFrame w, out RegionFrame e);
            var a = new Vector2(30f, 5f);          // a start in the west region
            var b = new Vector2(51.5f, 5f);        // just past the east edge
            CrossingDecision d = f.Resolve(w.RegionId, b, H);
            Assert.That(d.Kind, Is.EqualTo(CrossingKind.Cross));
            Vector2 landed = b + d.LocalShift;
            float walked = Vector2.Distance(a, b);
            float worldTrip = Vector2.Distance(w.ToWorld(a), e.ToWorld(landed));
            Assert.That(worldTrip, Is.EqualTo(walked).Within(1e-4f), "the world distance equals the distance moved");
            Assert.That(f.Resolve(e.RegionId, landed, H).Kind, Is.EqualTo(CrossingKind.Stay),
                "the arrival does not immediately cross back");
        }

        [Test]
        public void Resolve_PastAnEdgeFacingNothing_IsABoundary_ClampedInside()
        {
            WorldFrame f = TwoSideBySide(out RegionFrame w, out _);
            CrossingDecision d = f.Resolve(w.RegionId, new Vector2(-55f, 3f), H);
            Assert.That(d.Kind, Is.EqualTo(CrossingKind.Boundary));
            Assert.That(d.Edge, Is.EqualTo(RegionEdge.West));
            Assert.That(d.ClampedLocal, Is.EqualTo(new Vector2(-50f, 3f)));
        }

        [Test]
        public void TouchingIsASeam_SharingAreaIsAnOverlap()
        {
            WorldFrame f = TwoSideBySide(out RegionFrame w, out RegionFrame e);
            Assert.That(f.Validate(), Is.Empty);
            Assert.That(WorldFrame.TryGetSeam(w, e, out RegionSeam s), Is.True);
            Assert.That(s.EdgeOfA, Is.EqualTo(RegionEdge.East));
            Assert.That(s.Length, Is.EqualTo(60f).Within(1e-3f), "the shared stretch is the overlap of the two edges");
            Assert.That(WorldFrame.TryGetSeam(e, w, out RegionSeam back), Is.True);
            Assert.That(back.EdgeOfA, Is.EqualTo(WorldFrame.Opposite(s.EdgeOfA)), "symmetric pairing");

            f.Register(Frame("region.overlap", new Vector2(40f, 0f), new Vector2(30f, 30f)));
            Assert.That(f.Validate(), Has.Count.GreaterThan(0));
        }

        [Test]
        public void AnUnknownRegion_Stays_SoNoFrameMeansLegacyBehaviour()
        {
            WorldFrame f = TwoSideBySide(out _, out _);
            Assert.That(f.Resolve("region.nowhere", new Vector2(9999f, 0f), H).Kind, Is.EqualTo(CrossingKind.Stay));
            Assert.That(RegionPassage.StandsDownForWorldFrame(null, "region.west", false), Is.False, "no frame: bands live");
            Assert.That(RegionPassage.StandsDownForWorldFrame(f, "region.nowhere", false), Is.False, "off the map: bands live");
            Assert.That(RegionPassage.StandsDownForWorldFrame(f, "region.west", true), Is.False, "the opening keeps its door");
            Assert.That(RegionPassage.StandsDownForWorldFrame(f, "region.west", false), Is.True, "framed: the wall is the door");
        }

        // ---- the crossing applied to the bodies (no boat teleport, D1) -----------------------------------

        [Test]
        public void AFootCrossing_ShiftsTheWalker_AndLeavesTheBoatExactlyWhereSheWas()
        {
            var player = new GameObject("player").transform;
            var boat = new GameObject("boat").transform;
            try
            {
                player.position = new Vector3(381f, -40f, 0f);
                boat.SetPositionAndRotation(new Vector3(120f, 7f, 0f), Quaternion.Euler(0f, 0f, 33f));
                RegionTravelCoordinator.ApplyCrossing(player, boat, new Vector2(-760f, 100f), boatCame: false);
                Assert.That(player.position, Is.EqualTo(new Vector3(-379f, 60f, 0f)));
                Assert.That(boat.position, Is.EqualTo(new Vector3(120f, 7f, 0f)), "the boat stays where she was left");
                Assert.That(boat.rotation.eulerAngles.z, Is.EqualTo(33f).Within(1e-3f));
            }
            finally { Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(boat.gameObject); }
        }

        [Test]
        public void TheBoatsRegion_FollowsHerOnlyWhenSheCame_AndAnUnknownOneIsTheRegionLeft()
        {
            const string west = "region.west", east = "region.east";
            Assert.That(RegionTravelCoordinator.BoatRegionAfter("", west, east, boatCame: true), Is.EqualTo(east),
                "she came: she is in the region arrived in");
            Assert.That(RegionTravelCoordinator.BoatRegionAfter(west, east, west, boatCame: true), Is.EqualTo(west));
            Assert.That(RegionTravelCoordinator.BoatRegionAfter(west, west, east, boatCame: false), Is.EqualTo(west),
                "a walk out leaves her where she was");
            Assert.That(RegionTravelCoordinator.BoatRegionAfter(west, east, west, boatCame: false), Is.EqualTo(west),
                "and a walk home finds her there");
            Assert.That(RegionTravelCoordinator.BoatRegionAfter("", west, east, boatCame: false), Is.EqualTo(west),
                "an unknown region was shown as 'here': walked away from, she is in the region left (played direct)");
            Assert.That(RegionTravelCoordinator.BoatRegionAfter("", null, east, boatCame: false), Is.EqualTo(""),
                "from the shell no region is left behind: still unknown");
        }

        /// <summary>The land template's closing solid for one side of a centred rectangle: flush outside the
        /// edge, <paramref name="thickness"/> deep, the full length of the side (LandSceneTemplate.AddClosedEdges).</summary>
        static Rect ClosingSolid(Rect r, RegionEdge edge, float thickness) => edge switch
        {
            RegionEdge.East => new Rect(r.xMax, r.yMin, thickness, r.height),
            RegionEdge.West => new Rect(r.xMin - thickness, r.yMin, thickness, r.height),
            RegionEdge.North => new Rect(r.xMin, r.yMax, r.width, thickness),
            _ => new Rect(r.xMin, r.yMin - thickness, r.width, thickness),
        };

        [Test]
        public void ASolidFlushBeyondASharedEdge_StandsDown_AndOneBeyondABoundaryHolds()
        {
            WorldFrame f = TwoSideBySide(out RegionFrame west, out RegionFrame east);
            const float thick = 2f;
            Assert.That(f.HasNeighbourAlong(west.RegionId, RegionEdge.East), Is.True);
            Assert.That(f.HasNeighbourAlong(east.RegionId, RegionEdge.West), Is.True);
            foreach (RegionEdge e in new[] { RegionEdge.West, RegionEdge.North, RegionEdge.South })
                Assert.That(f.HasNeighbourAlong(west.RegionId, e), Is.False, $"west's {e} faces nothing");

            // The wall that held the walker 0.35 m and the dory 2 m short of the seam: it must go.
            Assert.That(f.StandsDownOnSharedEdge(west.RegionId, ClosingSolid(west.LocalRect, RegionEdge.East, thick)), Is.True);
            Assert.That(f.StandsDownOnSharedEdge(east.RegionId, ClosingSolid(east.LocalRect, RegionEdge.West, thick)), Is.True,
                "and the neighbour's wall on the same seam, or the way back is shut");
            // A world boundary keeps its wall.
            foreach (RegionEdge e in new[] { RegionEdge.West, RegionEdge.North, RegionEdge.South })
                Assert.That(f.StandsDownOnSharedEdge(west.RegionId, ClosingSolid(west.LocalRect, e, thick)), Is.False,
                    $"west's {e} wall holds");
        }

        [Test]
        public void ASolidThatReachesInsideTheRegion_NeverStandsDown()
        {
            WorldFrame f = TwoSideBySide(out RegionFrame west, out _);
            Rect r = west.LocalRect;
            // A quay that straddles the shared edge, and a rock just inside it: scenery, not a closing wall.
            Assert.That(f.StandsDownOnSharedEdge(west.RegionId, new Rect(r.xMax - 3f, 0f, 6f, 2f)), Is.False);
            Assert.That(f.StandsDownOnSharedEdge(west.RegionId, new Rect(r.xMax - 1f, 0f, 0.9f, 0.9f)), Is.False);
            Assert.That(f.StandsDownOnSharedEdge("region.unknown", ClosingSolid(r, RegionEdge.East, 2f)), Is.False,
                "a region off the map keeps every wall");
        }

        [Test]
        public void ABoatCrossing_ShiftsHerAndHerCrewOnce_KeepingHeading()
        {
            var boat = new GameObject("boat").transform;
            var player = new GameObject("player").transform;
            try
            {
                boat.SetPositionAndRotation(new Vector3(381f, 10f, 0f), Quaternion.Euler(0f, 0f, -90f));
                player.SetParent(boat, worldPositionStays: false);
                player.localPosition = new Vector3(0.5f, 1f, 0f);
                RegionTravelCoordinator.ApplyCrossing(player, boat, new Vector2(-760f, 0f), boatCame: true);
                Assert.That(boat.position, Is.EqualTo(new Vector3(-379f, 10f, 0f)));
                Assert.That(boat.rotation.eulerAngles.z, Is.EqualTo(270f).Within(1e-3f), "heading kept");
                Assert.That(player.localPosition, Is.EqualTo(new Vector3(0.5f, 1f, 0f)),
                    "the crew aboard rides along and is not shifted a second time");
            }
            finally { Object.DestroyImmediate(boat.gameObject); }
        }
    }

    /// <summary>
    /// ⭐ <b>THE SHIPPED WORLD LAYOUT</b> (ADR 0057). Loads the real <see cref="WorldMap"/> and holds the
    /// layout to the table in <c>docs/architecture/world-frame.md</c>: no overlaps, exactly the seams the
    /// table lists, every seam crossed both ways at the same world point, and the tidal bar lined up.
    /// </summary>
    public sealed class WorldLayoutTests
    {
        const string Nmc = "region.nine_mile_creek", StP = "region.st_peters", Rp = "region.rice_point",
                     Ew = "region.east_water", Cmb = "region.cumberland", Sw = "region.south_water";

        static WorldMap Map()
        {
            WorldMap map = WorldMap.LoadShipped();
            Assert.That(map, Is.Not.Null, "the shipped WorldMap is in Resources");
            return map;
        }

        static WorldFrame Built() => Map().BuildFrame();

        [Test]
        public void TheMap_ListsTheSixPlayableRegions_EachWithAUsableExtent()
        {
            WorldMap map = Map();
            Assert.That(map.Regions.All(r => r != null && r.HasUsableExtent), Is.True);
            Assert.That(map.Regions.Select(r => r.Id), Is.EquivalentTo(new[] { Nmc, StP, Rp, Ew, Cmb, Sw }));
        }

        [Test]
        public void NoTwoRegionsOverlap()
        {
            Assert.That(Built().Validate(), Is.Empty);
        }

        [Test]
        public void TheSeams_AreExactlyTheTable()
        {
            var expected = new Dictionary<string, float>
            {
                { Key(Nmc, StP), 340f }, { Key(Nmc, Rp), 360f }, { Key(Nmc, Cmb), 760f }, { Key(Nmc, Sw), 760f },
                { Key(Rp, Sw), 760f }, { Key(StP, Ew), 480f }, { Key(StP, Sw), 180f },
            };
            var actual = Built().Seams().ToDictionary(s => Key(s.RegionA, s.RegionB), s => s.Length);
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys));
            foreach (var kv in expected)
                Assert.That(actual[kv.Key], Is.EqualTo(kv.Value).Within(0.01f), kv.Key);
        }

        static string Key(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;

        [Test]
        public void EverySeam_CrossedBothWays_LandsAtTheSameWorldPoint_AndRoundTrips()
        {
            WorldFrame f = Built();
            const float h = 1f, past = 1.5f;
            foreach (RegionSeam s in f.Seams())
            {
                f.TryGet(s.RegionA, out RegionFrame a);
                f.TryGet(s.RegionB, out RegionFrame b);
                Vector2 outward = Outward(s.EdgeOfA);
                for (int i = 1; i < 10; i++)
                {
                    Vector2 onSeam = Vector2.Lerp(s.From, s.To, i / 10f);
                    // A -> B
                    Vector2 inA = a.ToLocal(onSeam + outward * past);
                    CrossingDecision ab = f.Resolve(a.RegionId, inA, h);
                    Assert.That(ab.Kind, Is.EqualTo(CrossingKind.Cross), $"{s.RegionA}->{s.RegionB} at {onSeam}");
                    Assert.That(ab.To.RegionId, Is.EqualTo(b.RegionId), $"{s.RegionA}->{s.RegionB} at {onSeam}");
                    Vector2 inB = inA + ab.LocalShift;
                    Assert.That(Vector2.Distance(b.ToWorld(inB), a.ToWorld(inA)), Is.LessThan(1e-3f));
                    // B -> A, from just the other side
                    Vector2 backInB = b.ToLocal(onSeam - outward * past);
                    CrossingDecision ba = f.Resolve(b.RegionId, backInB, h);
                    Assert.That(ba.Kind, Is.EqualTo(CrossingKind.Cross), $"{s.RegionB}->{s.RegionA} at {onSeam}");
                    Assert.That(ba.To.RegionId, Is.EqualTo(a.RegionId));
                    Vector2 backInA = backInB + ba.LocalShift;
                    Assert.That(Vector2.Distance(a.ToWorld(backInA), b.ToWorld(backInB)), Is.LessThan(1e-3f));
                    Assert.That(ab.LocalShift + ba.LocalShift, Is.EqualTo(Vector2.zero), "the shifts are exact inverses");
                }
            }
        }

        static Vector2 Outward(RegionEdge edge) => edge switch
        {
            RegionEdge.West => Vector2.left,
            RegionEdge.East => Vector2.right,
            RegionEdge.South => Vector2.down,
            _ => Vector2.up,
        };

        [Test]
        public void TheTidalBar_IsOneLine_AcrossTheCreekAndStPeters()
        {
            WorldMap map = Map();
            Assert.That(map.TryGet(Nmc, out RegionDef nmc), Is.True);
            Assert.That(map.TryGet(StP, out RegionDef stp), Is.True);
            // The bar's two ends as the scenes author them: the creek's (352, -200) and St Peters' (-356, 0).
            float creekBarWorldY = -200f + nmc.WorldOrigin.y;
            float stpBarWorldY = 0f + stp.WorldOrigin.y;
            Assert.That(creekBarWorldY, Is.EqualTo(stpBarWorldY).Within(0.01f), "no sideways jump crossing the bar");
        }

        /// <summary>
        /// The four PlayMode reds on the first push (walker held at x 379.65, dory at 378.00, 0.35 m and 2 m
        /// inside Rice Point's east edge; dory 1.9 m inside its south edge) were Rice Point's closing solids.
        /// On the shipped map the east and south walls stand on seams and must stand down; west and north face
        /// nothing and must hold. Cumberland's south wall sits on the creek's north beach seam.
        /// </summary>
        [Test]
        public void ThePlayableRegionsClosingWalls_OpenOnTheirSeams_AndHoldOnTheirBoundaries()
        {
            WorldFrame f = Built();
            void Expect(string id, RegionEdge edge, bool opens)
            {
                Assert.That(f.TryGet(id, out RegionFrame r), Is.True, id);
                Rect rect = r.LocalRect;
                const float thick = 2f;
                Rect wall = edge switch
                {
                    RegionEdge.East => new Rect(rect.xMax, rect.yMin, thick, rect.height),
                    RegionEdge.West => new Rect(rect.xMin - thick, rect.yMin, thick, rect.height),
                    RegionEdge.North => new Rect(rect.xMin, rect.yMax, rect.width, thick),
                    _ => new Rect(rect.xMin, rect.yMin - thick, rect.width, thick),
                };
                Assert.That(f.StandsDownOnSharedEdge(id, wall), Is.EqualTo(opens), $"{id} {edge} wall opens: {opens}");
            }
            Expect(Rp, RegionEdge.East, true);    // the creek: strand and sea door
            Expect(Rp, RegionEdge.South, true);   // the South Water: the channel mouth
            Expect(Rp, RegionEdge.West, false);
            Expect(Rp, RegionEdge.North, false);
            Expect(Cmb, RegionEdge.South, true);  // the creek's north beach
            Expect(Cmb, RegionEdge.West, false);
            Expect(Cmb, RegionEdge.East, false);
            Expect(Cmb, RegionEdge.North, false);
        }

        [Test]
        public void TheLegacyGreyboxAndUnbuiltWater_AreOffTheMap()
        {
            WorldFrame f = Built();
            Assert.That(f.Contains("region.coddle_cove"), Is.False);
            Assert.That(f.Contains("region.west_water"), Is.False);
        }
    }
}
