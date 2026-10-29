using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE ACTIVATION ON G3, AS NUMBERS.</b> What N1's steps 04 to 09 write into the scene, what the builder and
    /// the road painter read, and what the tests hold the scene to, from one place. The lines are the third issue's
    /// plan (<c>road.nmc.route_91</c>, <c>road.nmc.wharf_road</c>, <c>bridge.nmc_route91_river</c>); the wharf
    /// support is the measured minimum that keeps the loop whole on G3: caps at the two faces, never ground fill.
    ///
    /// <para>The analytic plan (<see cref="NineMileCreekMainland"/>) is not edited. It stays the authoring premise
    /// of the later waves that still read it, and of the builder's wharf.</para>
    /// </summary>
    public static class NineMileCreekGroundPlan
    {
        // =====================================================================================
        //  the fixed wharf (H5)
        // =====================================================================================

        /// <summary>The caps' deck. G3 buries the old 3.0 m deck by 0.40 to 0.57 m at the north face, so the face
        /// tops sit 0.4 m higher; the dry apron behind them is the ground itself.</summary>
        public const float DeckElevation = 3.4f;

        /// <summary>The old faces: the analytic wharf's north wall and deck, before G3.</summary>
        public const float OldNorthFaceY = 87f;

        /// <summary>The visible north face. At exactly this line the bed runs the whole half-metre transition, so
        /// the cap's brow is read a quarter metre landward and its toe a quarter metre seaward.</summary>
        public const float NorthFaceY = 86f;

        /// <summary>The crane apron's face, west of the step.</summary>
        public const float CraneFaceY = 76f;

        /// <summary>Where the two faces meet, and their outer ends.</summary>
        public const float FaceStepX = 78f, NorthFaceEastX = 212f, CraneFaceWestX = 50f;

        /// <summary>A cap spans the face's half-metre raster transition from its face line to the landward texel
        /// line: y 86 to 86.25 at the north face, 76 to 76.25 at the crane face.</summary>
        public const float CapDepthMetres = 0.25f;

        /// <summary>Half a quay blocker's thickness, as the builder lays them.</summary>
        public const float BlockerHalfThickness = 0.175f;

        /// <summary>The north cap: the whole north face, step to breakwater root.</summary>
        public static Rect NorthCap => new Rect(FaceStepX, NorthFaceY, NorthFaceEastX - FaceStepX, CapDepthMetres);

        /// <summary>The crane apron's cap.</summary>
        public static Rect CraneCap => new Rect(CraneFaceWestX, CraneFaceY, FaceStepX - CraneFaceWestX, CapDepthMetres);

        /// <summary>The brow down to the float: shore end on the dry apron, landing on the float's north edge.</summary>
        public static readonly Vector2 GangwayShoreEnd = new Vector2(105f, 86.5f);
        public static readonly Vector2 GangwayFloatEnd = new Vector2(105f, 71.2f);

        /// <summary>The opening the north blocker leaves for the brow: 1.2 m, x 104.4 to 105.6.</summary>
        public const float GangwayOpeningHalfWidth = 0.6f;

        /// <summary>The shore cleats' row: on the dry apron, a metre behind the face line.</summary>
        public const float CleatRowY = 86.5f;

        /// <summary>The fifteenth shore cleat, off the retired west apron: one bay west of bollard 0.</summary>
        public static readonly Vector2 WestCleat = new Vector2(92.5f, CleatRowY);

        /// <summary>The wall berths stand a metre further south, clear of the new face.</summary>
        public const float WallBerthShiftMetres = NorthFaceY - OldNorthFaceY;

        /// <summary>The wharf's ladder, re-lined with its face.</summary>
        public static readonly Vector2 WharfLadderPos = new Vector2(139.25f, NorthFaceY);

        /// <summary>The breakwater's two source arms (the wharf source's south arm and its east arm). The old
        /// edge crossed the basin at y 38 and the track with it.</summary>
        public static readonly Vector2[] SouthArm = { new Vector2(38f, 2f), new Vector2(116f, 2f) };
        public static readonly Vector2[] EastArm = { new Vector2(212f, 86f), new Vector2(212f, 20f), new Vector2(158f, -6f) };

        // =====================================================================================
        //  the berth and the arrival (H5/H6)
        // =====================================================================================

        /// <summary>Berth slot 0 on the north face: x 78.6 to 93.8, port side to the fender line.</summary>
        public const float BerthX = 86.2f, BerthSlotWestX = 78.6f, BerthSlotEastX = 93.8f;

        /// <summary>She lies heading east, her port side to the face.</summary>
        public const float BerthHeadingDegrees = 90f;

        /// <summary>The fender line her port side lies against: half a metre off the face.</summary>
        public const float FenderLineY = 85.5f;

        /// <summary>The design boat's half beam: the live CapeIslander, 12.9 m, draught 1.4.</summary>
        public const float DesignHalfBeamMetres = 2.4f;

        /// <summary>Where the design boat's centre lies alongside (86.2, 83.1); a narrower hull lies further in.</summary>
        public static Vector2 ArrivalPos => new Vector2(BerthX, FenderLineY - DesignHalfBeamMetres);

        /// <summary>The arrival's heading as Unity writes <c>Quaternion.Euler(0, 0, -90)</c>.</summary>
        public static readonly Quaternion ArrivalRotation = new Quaternion(0f, 0f, -0.7071068f, 0.7071068f);

        /// <summary>The dock zone sits on the fender line, so every hull alongside is inside its reach.</summary>
        public static Vector2 DockZonePos => new Vector2(BerthX, FenderLineY);

        /// <summary>Where she steps ashore: on the dry apron, 1.35 m behind the berth ladder.</summary>
        public static Vector2 DisembarkPos => new Vector2(BerthX, 87.35f);

        /// <summary>The player's berth ladder, on the face at the berth.</summary>
        public static Vector2 BerthLadderPos => new Vector2(BerthX, NorthFaceY);

        public const string BerthLadderName = "Ladder_Berth0";
        public const string BerthLadderId = "wharf.nine_mile_creek.ladder_berth_0";

        // =====================================================================================
        //  the Otter, parked safe for now (owner ruling)
        // =====================================================================================

        /// <summary>Where the Otter stands on G3. The owner's ruling (2026-10-15): "Just place the otter somewhere
        /// safe for now. Finishing the scene is more important". G3 has no boat ramp, so the landing the plan derives
        /// beside the old ramp's head (<see cref="NineMileCreekMainland.OtterLandingPos"/>) launches nothing. She
        /// keeps that spot, which on G3 is dry yard well above spring high, clear of the roads, the working sites
        /// and the loop's walks. Her launch is a later wave's.</summary>
        public static readonly Vector2 OtterParkPos = new Vector2(62.158894f, 95.39067f);

        /// <summary>She faces due north, inland: the dry yard ahead of her, the quay faces behind. Unity's
        /// counter-clockwise z, so 0 is north.</summary>
        public const float OtterParkHeadingDegrees = 0f;

        /// <summary>Her rotation, the same bits for the builder and for step 08.</summary>
        public static Quaternion OtterParkRotation =>
            new Quaternion(0f, 0f, Mathf.Sin(OtterParkHeadingDegrees * Mathf.Deg2Rad * 0.5f),
                Mathf.Cos(OtterParkHeadingDegrees * Mathf.Deg2Rad * 0.5f));

        // =====================================================================================
        //  the roads (H4)
        // =====================================================================================

        /// <summary>Route 91 as the third issue draws it: from the north edge, over the river on the bridge, through
        /// the town and down to the south shore at (-380, -142). No Rice Point door.</summary>
        public static readonly Vector2[] Route91 =
        {
            new Vector2(-186f, 280f), new Vector2(-181f, 242f), new Vector2(-177.6f, 222f), new Vector2(-176.8f, 205f),
            new Vector2(-176.6f, 197f), new Vector2(-177.1f, 147f), new Vector2(-177.45f, 92f), new Vector2(-177.6f, 57f),
            new Vector2(-181f, 10f), new Vector2(-190f, -40f), new Vector2(-206f, -82f), new Vector2(-229.4f, -109f),
            new Vector2(-240f, -114.6f), new Vector2(-252f, -117.6f), new Vector2(-266f, -119f), new Vector2(-284f, -119.6f),
            new Vector2(-302f, -119.8f), new Vector2(-316f, -120.4f), new Vector2(-328f, -122f), new Vector2(-339f, -124.8f),
            new Vector2(-349f, -128.6f), new Vector2(-358f, -133f), new Vector2(-366f, -137.4f), new Vector2(-373f, -140.6f),
            new Vector2(-380f, -142f),
        };

        /// <summary>Wharf Road as the third issue draws it: the town to the neck, then the final ramp up to the
        /// turning pad at (50, 118). The yard's own road beyond is a later wave.</summary>
        public static readonly Vector2[] WharfRoad =
        {
            new Vector2(-177.45f, 92f), new Vector2(-100f, 92f), new Vector2(-40f, 92f), new Vector2(14f, 92f),
            new Vector2(22f, 95f), new Vector2(27f, 101f), new Vector2(29f, 108f), new Vector2(30.5f, 114f),
            new Vector2(34f, 118f), new Vector2(50f, 118f),
        };

        /// <summary>The turning circle at the head of the ramp.</summary>
        public static readonly Vector2 TurningPadCentre = new Vector2(62f, 125f);
        public const float TurningPadRadius = 15f;
        public const string TurningPadName = "WharfTurningPad";

        /// <summary>The yard's own road (the wharf key scene's yard.main_road) runs straight along this y from the
        /// turning pad's east side to the boat yard's gate. It is not Wharf Road, and nothing paves it yet: a later
        /// wave's.</summary>
        public const float YardRoadY = 120f;

        /// <summary>Where the yard road's line leaves the turning circle on its east side, (76.14, 120): where a
        /// truck leaves the pad for the buyers' gravel.</summary>
        public static Vector2 TurningPadExit
        {
            get
            {
                float dy = YardRoadY - TurningPadCentre.y;
                return new Vector2(TurningPadCentre.x + Mathf.Sqrt(TurningPadRadius * TurningPadRadius - dy * dy),
                    YardRoadY);
            }
        }

        /// <summary>Route 91's river bridge: the plan's 36 m span at (-176.8, 205), 9.5 m wide, carried a metre
        /// onto the stable bank at each end (y 186 and 224, both +6.0 on G3). The south bench under the plan's own
        /// span end reads +2.7, so the span alone would leave a 3.3 m step.</summary>
        public static readonly Rect BridgeDeck = new Rect(-181.55f, 186f, 9.5f, 38f);
        public const float BridgeDeckElevation = 6f;
        public const string BridgeId = "bridge.nmc_route91_river";
        public const string BridgeName = "Route91RiverBridge";

        /// <summary>The paved network on G3: today's ways, with Route 91 and Wharf Road on their final lines, the
        /// turning pad added and the town walks joining the two roads where they now run. Surfaces, widths and
        /// ranks are unchanged: no road is widened.</summary>
        public static IReadOnlyList<NineMileCreekRoads.Way> Ways()
        {
            var list = new List<NineMileCreekRoads.Way>();
            foreach (var way in NineMileCreekRoads.Ways())
            {
                if (way.Name == NineMileCreekRoads.ThroughRoadName)
                    list.Add(Rerouted(way, Route91));
                else if (way.Name == NineMileCreekRoads.WharfRoadName)
                {
                    list.Add(Rerouted(way, WharfRoad));
                    list.Add(new NineMileCreekRoads.Way(TurningPadName, NineMileCreekRoads.GravelSurface,
                        new[] { TurningPadCentre, TurningPadCentre }, TurningPadRadius, NineMileCreekRoads.RankWharfRoad,
                        false, "the turning circle at the head of Wharf Road's ramp: a truck that came up it turns here"));
                }
                else if (way.Name.StartsWith(NineMileCreekRoads.TownWalkNamePrefix, System.StringComparison.Ordinal))
                    continue;   // re-derived below, joining the final lines
                else list.Add(way);
            }
            list.AddRange(TownWalks());
            return list;
        }

        /// <summary>The five town walks, each joining Route 91 or Wharf Road on its final line. Only where a walk
        /// meets the road moves with the roads; the lots and the doorstep rule are the town wave's.</summary>
        public static IReadOnlyList<NineMileCreekRoads.Way> TownWalks() =>
            NineMileCreekRoads.TownWalks(Route91, WharfRoad);

        static NineMileCreekRoads.Way Rerouted(NineMileCreekRoads.Way way, Vector2[] route) =>
            new NineMileCreekRoads.Way(way.Name, way.Surface, route, way.HalfWidth, way.Rank, way.CentreLineCritical,
                way.Reason + " (third issue's line on G3)");

        /// <summary>Pave G3: the dry-ground rule everywhere but on the bridge deck, which carries the road over the
        /// river.</summary>
        public static NineMileCreekRoads.Paving Pave(ITidalTerrain terrain) =>
            NineMileCreekRoads.Pave(terrain, Ways(), NineMileCreekRoads.Pads(), new[] { BridgeDeck });

        // =====================================================================================
        //  what kit v3 still paints, once kit v4 lays the network (NMC H4)
        // =====================================================================================

        /// <summary>Where the network's Wharf Road ends and the wharf's own stub of it takes over: the network
        /// (<see cref="NineMileCreekRoadNetwork"/>) clips road.nmc_wharf_road here, and the stub runs on to the turning
        /// pad, both the wharf's (H5/H6).</summary>
        public static readonly Vector2 WharfStubStart = new Vector2(40f, 118f);
        public const string WharfStubName = "WharfRoadWharfStub";

        /// <summary>The ways kit v3 still paints: the wharf's stub of Wharf Road, from where the network clips it to
        /// the end of G3's line, and the turning pad at its head. Every other road is kit v4's, drawn by the road floor
        /// (<c>RoadFloorStreamer</c>), so step 06 and the road painter lay none of it and no rebuild brings it back.</summary>
        public static IReadOnlyList<NineMileCreekRoads.Way> KeptV3Ways()
        {
            NineMileCreekRoads.Way wharf = Ways().Single(w => w.Name == NineMileCreekRoads.WharfRoadName);
            NineMileCreekRoads.Way pad = Ways().Single(w => w.Name == TurningPadName);
            return new[]
            {
                new NineMileCreekRoads.Way(WharfStubName, wharf.Surface, new[] { WharfStubStart, WharfRoad[WharfRoad.Length - 1] },
                    wharf.HalfWidth, wharf.Rank, wharf.CentreLineCritical,
                    "the wharf's stub of Wharf Road, from where kit v4's network clips it to the turning pad (H5/H6's)"),
                pad,
            };
        }

        /// <summary>The pads kit v3 still paints: the winch apron and the buyers' parking (the wharf's) and the
        /// station's forecourt (the gas bar's). The truck park and the laydown are lots on kit v4's network.</summary>
        public static IReadOnlyList<NineMileCreekRoads.Pad> KeptV3Pads() =>
            NineMileCreekRoads.Pads().Where(p => p.Name != NineMileCreekRoads.TruckParkName &&
                                                 p.Name != NineMileCreekRoads.LaydownName).ToList();

        /// <summary>What step 06 and the road painter paint in kit v3: <see cref="KeptV3Ways"/> and
        /// <see cref="KeptV3Pads"/> under the dry-ground rule. None of it crosses water, so no deck is exempted.</summary>
        public static NineMileCreekRoads.Paving PaveKeptV3(ITidalTerrain terrain) =>
            NineMileCreekRoads.Pave(terrain, KeptV3Ways(), KeptV3Pads(), System.Array.Empty<Rect>());

        /// <summary>The bridges' root: the three plain standable decks of the network (Q7), each an instance of its own
        /// prefab under <see cref="BridgePrefabFolder"/>. H7 and H12 dress them.</summary>
        public const string BridgesRootName = "NineMileCreekBridges";
        public const string BridgePrefabFolder = "Assets/_Project/Prefabs/Bridges";

        public static string BridgePrefabPath(string prefab) => BridgePrefabFolder + "/" + prefab + ".prefab";
    }
}
