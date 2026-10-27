using System;
using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests
{
    public sealed class BoatCabinEntryLandingTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _made.Count - 1; i >= 0; i--) if (_made[i] != null) Object.DestroyImmediate(_made[i]);
            _made.Clear();
        }

        [Test]
        public void AuthoredStairEntryMeasuresItsDoorPlane_NotItsNearestSideEdge()
        {
            var rig = Make();
            Assert.IsFalse(BoatCabinThreshold.TryWallLine(rig.Def, rig.Def.Door, out _, out _, out _));
            Assert.IsTrue(BoatCabinThreshold.TryEntryLine(rig.Def, rig.Def.Door,
                out BoatCabinEntryKind kind, out int? room, out Vector2 line, out Vector2 normal));
            Assert.AreEqual(BoatCabinEntryKind.Landing, kind);
            Assert.IsNull(room);
            Assert.AreEqual(new Vector2(-0.30f, -0.51f), line);
            Assert.AreEqual(Vector2.down, normal, "the 0.07 m side edge must never become her axis");
            rig.Def.Door.Side = "fwd";
            Assert.IsTrue(BoatCabinThreshold.TryEntryLine(rig.Def, rig.Def.Door, out _, out _, out _, out normal));
            Assert.AreEqual(Vector2.down, normal, "a label cannot turn the measured plane around");
            rig.Def.Door.EntryLanding.HeightMetres = 1.37f;
            Assert.IsFalse(BoatCabinThreshold.TryEntryLine(rig.Def, rig.Def.Door, out _, out _, out _, out _));
            rig.Def.Door.EntryLanding.HeightMetres = 1.17f;
            rig.Def.Door.EntryLanding.RouteId = "missing";
            Assert.IsFalse(BoatCabinThreshold.TryEntryLine(rig.Def, rig.Def.Door, out _, out _, out _, out _));
        }

        [Test]
        public void DoorCrossingKeepsTheUpperLandingHeight_UntilTheNamedStair()
        {
            var rig = Make();
            Vector3 sill = rig.Def.Door.ThresholdPoint;
            Assert.IsFalse(rig.Door.TryWalkThroughAt(sill, Vector2.up, 0.02f), "closed door");
            Open(rig.Door);
            Assert.IsFalse(rig.Door.CarriesHeld(new Vector2(20f, 20f), Vector2.up),
                "a landing has no room furniture to index outside the pull");
            Assert.IsFalse(rig.Door.TryWalkThroughAt(new Vector3(sill.x, sill.y, 0.22f), Vector2.up, 0.02f));
            Assert.IsFalse(rig.Door.TryWalkThroughAt(sill, Vector2.zero, 0.02f));
            Assert.IsTrue(rig.Door.TryWalkThroughAt(sill, Vector2.up, 0.02f));
            Assert.IsTrue(rig.Cabin.IsInside);
            Assert.AreSame(rig.Def.Door, rig.Cabin.EntryLandingDoor);
            Assert.AreEqual(-1, rig.Cabin.Level, "a landing is not cabin level zero");
            Assert.IsFalse(rig.Cabin.TryGoToLevel(0), "only the named stair may leave this surface for a room");
            Assert.IsFalse(rig.Cabin.TryTakeEntryStair("another_route"));
            Assert.AreEqual(1.17f, rig.Cabin.EntryLandingArrivalPoint.z);
            Assert.AreEqual(2, rig.Cut.RequestedCut.Level, "the existing helm_deck cut opens the house");
            Assert.IsFalse(rig.Door.TryWalkThroughAt(sill, Vector2.down, 0.02f), "same settle rule");
            Assert.IsTrue(rig.Door.TryWalkThroughAt(sill, Vector2.down, 0.20f));
            Assert.IsFalse(rig.Cabin.IsInside);
            Assert.IsFalse(rig.Cut.RequestedCut.Opens);
        }

        [Test]
        public void ClosedStairEntryCannotBeBypassedByTheRoute()
        {
            var rig = Make();
            SetHere(rig.Walk, new Vector3(0f, -3f, 1.17f));
            Assert.IsFalse(OrdinaryRoute(rig)); // arm the ordinary route from a distant deck point
            SetHere(rig.Walk, rig.Def.Routes[0].FromPoint);
            Assert.IsFalse(OrdinaryRoute(rig), "an armed deck route cannot step round its closed door");
            Assert.IsFalse(Stair(rig, Vector2.up));
            Assert.IsFalse(rig.Cabin.IsInside);
            Assert.IsFalse(rig.Door.IsOpen);
        }

        [Test]
        public void LandingAndStairCanBeTakenAndRearmedBothWays()
        {
            var rig = Make();
            Open(rig.Door);
            Assert.IsTrue(rig.Door.TryWalkThroughAt(rig.Def.Door.ThresholdPoint, Vector2.up, 0.02f));
            SetHere(rig.Walk, rig.Def.Door.ThresholdPoint);
            Assert.IsTrue(Stair(rig, Vector2.up));
            Assert.AreEqual(0, rig.Cabin.Level);
            Assert.IsNull(rig.Cabin.EntryLandingDoor);
            Assert.AreEqual(0.22f, rig.Walk.DeckHeightMeters);
            Assert.AreEqual(4, rig.Cut.RequestedCut.Level);
            Assert.IsFalse(Stair(rig, Vector2.up), "holding the arrival key cannot bounce her back");
            Assert.IsFalse(Stair(rig, Vector2.zero));
            Assert.IsTrue(Stair(rig, Vector2.down));
            Assert.AreEqual(-1, rig.Cabin.Level);
            Assert.AreEqual(1.17f, rig.Walk.DeckHeightMeters);
            Assert.AreEqual(new Vector2(-0.30f, -0.36f), rig.Walk.DeckLocalPosition);
            Assert.AreEqual(2, rig.Cut.RequestedCut.Level);
            Assert.IsTrue(rig.Cabin.IsInside, "up the stair is not out through the door");
            Assert.IsFalse(Stair(rig, Vector2.down));
            Assert.IsTrue(Stair(rig, Vector2.up), "deliberate reverse rearms on the short landing");
            Assert.AreEqual(0.22f, rig.Walk.DeckHeightMeters);
            Assert.AreEqual(0.30f, rig.Def.RouteEndReach);
            Assert.AreEqual(0.10f, rig.Def.FloorTolerance);
        }

        [Test]
        public void LegacyFleetKeepsEveryMeasuredLineAndAxis()
        {
            foreach (var row in Legacy)
            {
                var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                    "Assets/_Project/Data/Boats/Visuals/" + row.Key + ".asset");
                Assert.IsNotNull(visual, row.Key);
                Assert.IsTrue(BoatCabinThreshold.TryWallLine(visual.Interior, visual.Interior.Door,
                    out int index, out Vector2 point, out Vector2 normal), row.Key);
                AssertLegacyLine(row.Key, index, point, normal);
                Assert.IsTrue(BoatCabinThreshold.TryEntryLine(visual.Interior, visual.Interior.Door,
                    out BoatCabinEntryKind kind, out int? room, out Vector2 entryPoint, out Vector2 entryNormal));
                Assert.AreEqual(BoatCabinEntryKind.RoomEdge, kind);
                Assert.AreEqual(index, room);
                AssertLegacyLine(row.Key, room.Value, entryPoint, entryNormal);
            }
            Assert.AreEqual(27, Legacy.Count);
        }

        internal static void AssertLegacyLine(string name, int index, Vector2 point, Vector2 normal)
        {
            Assert.IsTrue(Legacy.TryGetValue(name, out int[] expected), name);
            CollectionAssert.AreEqual(expected, new[] { index, Bits(point.x), Bits(point.y), Bits(normal.x), Bits(normal.y) }, name);
        }

        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);

        // Fixed float bits from the pre-change Phase A census, not the method under test.
        private static readonly Dictionary<string, int[]> Legacy = new Dictionary<string, int[]>
        {
            { "CapeIslanderIso", new[] { 0, 1022739072, 1058139013, -2147483648, -1082130432 } },
            { "CoastalPacketIso", new[] { 1, -1080033280, -1047595581, 0, 1065353216 } },
            { "LobsterBoatIso", new[] { 0, -1124744576, 1058977874, -2147483648, -1082130432 } },
            { "LobsterInshoreHardtopFundyIso", new[] { 0, -1130113248, 1066930274, -2147483648, -1082130432 } },
            { "LobsterInshoreHardtopNewfoundlandIso", new[] { 0, -1127965824, 1059128869, -2147483648, -1082130432 } },
            { "LobsterInshoreHardtopNorthumberlandIso", new[] { 0, -1129576416, 1048039129, -2147483648, -1082130432 } },
            { "LobsterInshoreOpenFundyIso", new[] { 0, -1130113248, 1066930274, -2147483648, -1082130432 } },
            { "LobsterInshoreOpenNewfoundlandIso", new[] { 0, -1127965824, 1059128869, -2147483648, -1082130432 } },
            { "LobsterInshoreOpenNorthumberlandIso", new[] { 0, -1129576416, 1048039129, -2147483648, -1082130432 } },
            { "LobsterOffshoreHardtopFundyIso", new[] { 0, -1125818368, 1073473389, -2147483648, -1082130432 } },
            { "LobsterOffshoreHardtopNewfoundlandIso", new[] { 0, -1123066848, 1065512600, -2147483648, -1082130432 } },
            { "LobsterOffshoreHardtopNorthumberlandIso", new[] { 0, -1124207680, 1052334096, -2147483648, -1082130432 } },
            { "LobsterOffshoreOpenFundyIso", new[] { 0, -1125818368, 1073473389, -2147483648, -1082130432 } },
            { "LobsterOffshoreOpenNewfoundlandIso", new[] { 0, -1123066848, 1065512600, -2147483648, -1082130432 } },
            { "LobsterOffshoreOpenNorthumberlandIso", new[] { 0, -1124207680, 1052334096, -2147483648, -1082130432 } },
            { "LobsterStandardHardtopFundyIso", new[] { 0, -1127965760, 1070638039, -2147483648, -1082130432 } },
            { "LobsterStandardHardtopNewfoundlandIso", new[] { 0, -1124744576, 1062836634, -2147483648, -1082130432 } },
            { "LobsterStandardHardtopNorthumberlandIso", new[] { 0, -1126355200, 1050589266, -2147483648, -1082130432 } },
            { "LobsterStandardOpenFundyIso", new[] { 0, -1127965760, 1070638039, -2147483648, -1082130432 } },
            { "LobsterStandardOpenNewfoundlandIso", new[] { 0, -1124744576, 1062836634, -2147483648, -1082130432 } },
            { "LobsterStandardOpenNorthumberlandIso", new[] { 0, -1126355200, 1050589266, -2147483648, -1082130432 } },
            { "SideDraggerIso", new[] { 1, -1119040384, -1063948124, 0, 1065353216 } },
            { "SportFisherConvertibleIso", new[] { 0, -1092196760, -1063025377, 0, -1082130432 } },
            { "SportFisherSkybridgeIso", new[] { 0, -1088841316, -1054888428, 0, -1082130432 } },
            { "SternTrawlerIso", new[] { 1, -1119040320, 1070134723, 0, -1082130432 } },
            { "SternTrawlerMk2Iso", new[] { 1, -1119040320, 1070134723, 0, -1082130432 } },
            { "TankerIso", new[] { 2, 0, -1035854152, 0, -1082130432 } },
        };

        private sealed class Rig
        {
            internal BoatInteriorDef Def; internal BoatInterior Cabin; internal BoatCabinDoor Door;
            internal DeckWalkController Walk; internal BoatCutaway Cut; internal BoatDeckDef Deck;
        }

        private Rig Make()
        {
            var root = new GameObject("EntryLandingFixture"); root.SetActive(false); _made.Add(root);
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>(); _made.Add(def);
            def.Levels = new[] { new BoatInteriorLevel { Id = "cabin", SoleZMeters = 0.22f,
                Outline = new[] { new Vector2(-0.19f, 0.59f), new Vector2(0.19f, 0.59f),
                    new Vector2(0.19f, 0.98f), new Vector2(-0.19f, 0.98f) } } };
            def.Door = new BoatInteriorDoor { Id = "companionway", Side = "aft", ClearWidthMeters = 0.64f,
                ClearHeightMeters = 0.76f, ThresholdPoint = new Vector3(-0.30f, -0.51f, 1.17f),
                EntryLanding = new BoatInteriorEntryLanding { OpeningStart = new Vector2(-0.62f, -0.51f),
                    OpeningEnd = new Vector2(0.02f, -0.51f), HeightMetres = 1.17f, RouteId = "companionway",
                    UpperPlateau = new[] { new Vector2(-0.23f, -0.76f), new Vector2(-0.23f, -0.36f),
                        new Vector2(-0.37f, -0.36f), new Vector2(-0.37f, -0.76f) } } };
            def.Routes = new[] { new BoatInteriorRoute { Id = "companionway", FromLevel = "helm_fwd",
                ToLevel = "cabin", FromPoint = new Vector3(-0.30f, -0.76f, 1.17f),
                ToPoint = new Vector3(-0.06f, 0.70f, 0.22f), Mechanism = "InteriorStair", Placed = true } };
            var mesh = ScriptableObject.CreateInstance<HullMeshDef>(); _made.Add(mesh);
            mesh.LevelTags = new[] { new HullMeshDef.LevelTag { DeckId = "helm_deck", Tag = 2, Enclosed = true, SoleZMeters = 1.17f },
                new HullMeshDef.LevelTag { DeckId = "cabin", Tag = 4, Enclosed = true, SoleZMeters = 0.22f } };
            var deck = ScriptableObject.CreateInstance<BoatDeckDef>(); _made.Add(deck);
            var exterior = new GameObject("Exterior"); exterior.transform.SetParent(root.transform); exterior.AddComponent<SpriteRenderer>();
            var inside = new GameObject("Interior"); inside.transform.SetParent(root.transform); inside.AddComponent<SpriteRenderer>();
            var cabin = root.AddComponent<BoatInterior>();
            cabin.Configure(def, exterior.GetComponent<SpriteRenderer>(), inside.GetComponent<SpriteRenderer>(),
                null, inside.transform, root.transform, new Sprite[8], 8, true, 0f, 0f, 0f, 0f, meshRoom: mesh);
            var door = root.AddComponent<BoatCabinDoor>(); door.Configure(cabin, "entry_fixture", -1, 1f, "Open the door", "Close the door");
            var cut = root.AddComponent<BoatCutaway>(); cut.Configure(cabin, mesh, root.transform, new object());
            var walk = root.AddComponent<DeckWalkController>();
            walk.enabled = false; // Direct pure-step calls; no live scene input is used by this fixture.
            root.SetActive(true); // Its listeners receive normal OnDisable/OnDestroy cleanup.
            return new Rig { Def = def, Cabin = cabin, Door = door, Cut = cut, Deck = deck, Walk = walk };
        }

        private static void Open(BoatCabinDoor door)
        { Assert.IsTrue(door.TryUse()); door.Tick(door.CueSeconds + 0.01f); Assert.IsTrue(door.IsOpen); }
        private static void SetHere(DeckWalkController walk, Vector3 point)
        { Field("_deckLocal").SetValue(walk, (Vector2)point); Field("_deckHeight").SetValue(walk, point.z); }
        private static FieldInfo Field(string name) => typeof(DeckWalkController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool Stair(Rig rig, Vector2 held) => (bool)typeof(DeckWalkController).GetMethod(
            "TakeEntryLandingRoute", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(rig.Walk, new object[] { rig.Cabin, rig.Deck, held });
        private static bool OrdinaryRoute(Rig rig) => (bool)typeof(DeckWalkController).GetMethod(
            "TakeACabinRoute", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(rig.Walk, new object[] { rig.Cabin, rig.Deck, false });
    }
}
