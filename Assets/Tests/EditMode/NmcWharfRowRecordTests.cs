using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class NmcWharfRowRecordTests
    {
        const string ScenePath = "Assets/_Project/Data/KeyScenes/KeyScene_nmc_wharf.asset";
        const string RecordPath = "Assets/_Project/Data/KeyScenes/KeyScene_nmc_wharf.owners.json";
        static readonly string[] Owners = { "H5", "H6", "N8", "N9" };

        [Serializable] public sealed class Rectangle
        {
            public float minX, maxX, minY, maxY;
            public bool Contains(Vector2 at) => at.x >= minX && at.x <= maxX && at.y >= minY && at.y <= maxY;
        }
        [Serializable] public sealed class KindRule { public string kind, standsOn; }
        [Serializable] public sealed class KitRule { public string kit; public string[] pieces; }
        [Serializable] public sealed class Rule
        {
            public string owner, description;
            public string[] ids, excludeOwners;
            public Rectangle[] rectangles, excludedRectangles;
            public KindRule[] kinds;
            public KitRule[] kitPieces;
        }
        [Serializable] public sealed class Row { public string id, owner, place, waits; }
        [Serializable] public sealed class Record
        {
            public string schema, scene, ownershipRule;
            public Rule[] rules;
            public Row[] rows;
        }

        static T[] Items<T>(T[] values) => values ?? Array.Empty<T>();
        static Record Read() => JsonUtility.FromJson<Record>(File.ReadAllText(RecordPath));
        static KeySceneDef Scene()
        {
            var scene = AssetDatabase.LoadAssetAtPath<KeySceneDef>(ScenePath);
            Assert.NotNull(scene, ScenePath);
            return scene;
        }
        static bool Matches(KeyScenePiece piece, Rule rule) =>
            (Items(rule.ids).Contains(piece.Id) || Items(rule.rectangles).Any(r => r.Contains(piece.At)) ||
             Items(rule.kinds).Any(k => k.kind == piece.Kind && k.standsOn == piece.StandsOn) ||
             Items(rule.kitPieces).Any(k => k.kit == piece.Kit && Items(k.pieces).Contains(piece.Piece))) &&
            !Items(rule.excludedRectangles).Any(r => r.Contains(piece.At));

        static string[] OwnersFor(KeyScenePiece piece, Record record)
        {
            var raw = record.rules.ToDictionary(r => r.owner, r => Matches(piece, r));
            return record.rules.Where(r => raw[r.owner] && !Items(r.excludeOwners).Any(owner => raw[owner]))
                         .Select(r => r.owner).ToArray();
        }

        [Test]
        public void EveryRowIsOwnedOnce_ByTheRulesInTheRecord_IncludingTheNorthCourt()
        {
            Record record = Read();
            KeySceneDef scene = Scene();
            Assert.AreEqual("hidden-harbours/key-scene-owners@1", record.schema);
            Assert.AreEqual(scene.Id, record.scene);
            Assert.IsNotEmpty(record.ownershipRule);
            CollectionAssert.AreEquivalent(Owners, record.rules.Select(r => r.owner).ToArray());
            Assert.AreEqual(record.rows.Length, record.rows.Select(r => r.id).Distinct().Count(), "duplicate record id");
            CollectionAssert.AreEquivalent(scene.Pieces.Select(p => p.Id).ToArray(), record.rows.Select(r => r.id).ToArray(),
                "Every asset row is recorded once; no record names an absent row.");
            var byId = record.rows.ToDictionary(r => r.id);
            foreach (KeyScenePiece piece in scene.Pieces)
            {
                string[] owners = OwnersFor(piece, record);
                Assert.AreEqual(1, owners.Length, $"{piece.Id} at {piece.At}: {string.Join(", ", owners)}; STOP for an unowned or multiply owned row");
                Assert.AreEqual(owners[0], byId[piece.Id].owner, piece.Id);
            }
            var court = Enumerable.Range(0, 2).SelectMany(row => Enumerable.Range(0, 9)
                .Select(column => $"prop.nmc_traps_court_{row}_{column}")).Concat(new[] { "gull.nmc_flats" }).ToArray();
            Assert.AreEqual(19, court.Length);
            foreach (string id in court)
            {
                Assert.AreEqual("H6", byId[id].owner, id);
                Assert.AreEqual("yard, the north court", byId[id].place, id);
            }
            TestContext.WriteLine(string.Join("; ", Owners.Select(owner => owner + "=" + record.rows.Count(r => r.owner == owner))) +
                                  "; total=" + record.rows.Length);
        }

        [Test]
        public void TheRowsPlacedSoFar_AreOwnedByH6OrH5_NeverN8OrN9_AndNeverAWaitingRow()
        {
            Record record = Read();
            KeySceneDef scene = Scene();
            // seat ruling s9, 11-07: this guard admits eligible H5 rows; completeness belongs to Phase B.
            var byId = record.rows.ToDictionary(r => r.id);
            foreach (string id in scene.PlacementIds)
            {
                Assert.That(byId.ContainsKey(id), Is.True, id);
                Assert.That(byId[id].owner, Is.EqualTo("H6").Or.EqualTo("H5"), id);
                Assert.That(string.IsNullOrEmpty(byId[id].waits), Is.True, id);
            }
            CollectionAssert.DoesNotContain(scene.PlacementIds, "prop.nmc_light_ramp");
            foreach (string id in new[] { "structure.nmc_harbourmaster", "structure.nmc_fish_market", "structure.nmc_restaurant" })
                CollectionAssert.Contains(scene.PlacementIds, id);
            Assert.IsTrue(scene.Placed, "This phase must activate its explicit placement subset.");
        }

        static StPetersLayerRefresh.SceneYaml waterCandidate;
        static bool waterDryRunStarted;
        static string waterDryRunFailure;
        static StPetersLayerRefresh.SceneYaml WaterSideCandidate()
        {
            const string evidence = "Evidence~/nmc-h5/phase-b";
            if (!waterDryRunStarted)
            {
                waterDryRunStarted = true;
                Directory.CreateDirectory(evidence);
                byte[] inherited = File.ReadAllBytes(NineMileCreekLayerRefresh.ScenePath);
                try
                {
                    NineMileCreekWharfLandSide.ApplyWaterSide(evidence + "/n1-headless", false);
                    waterCandidate = StPetersLayerRefresh.SceneYaml.Parse(File.ReadAllText(evidence + "/n1-headless/candidate.unity"));
                    if (!inherited.SequenceEqual(File.ReadAllBytes(NineMileCreekLayerRefresh.ScenePath)))
                        throw new InvalidOperationException("The dry run changed the inherited scene.");
                    File.WriteAllText(evidence + "/n1-dry-run-stack.txt", "PASS: current-code dry run completed without an exception.\n");
                }
                catch (Exception ex)
                {
                    waterDryRunFailure = ex.ToString();
                    File.WriteAllText(evidence + "/n1-dry-run-stack.txt", waterDryRunFailure);
                    TestContext.WriteLine(waterDryRunFailure);
                }
            }
            Assert.That(waterDryRunFailure, Is.Null, waterDryRunFailure);
            Assert.That(waterCandidate, Is.Not.Null);
            return waterCandidate;
        }

        [Test]
        public void TheN1DryRunPlacesExactlyTheEligibleTwentyTwo_WithItsStack()
        {
            Record record = Read();
            var expected = record.rows.Where(r => r.owner == "H5" && string.IsNullOrEmpty(r.waits)).Select(r => r.id).ToArray();
            Assert.That(expected, Has.Length.EqualTo(22));
            var owned = new HashSet<string>(record.rows.Where(r => r.owner == "H5").Select(r => r.id));
            var candidate = WaterSideCandidate();
            var actual = candidate.Docs.Where(d => d.ClassId == 1 && owned.Contains(d.Field("m_Name"))).Select(d => d.Field("m_Name")).ToArray();
            CollectionAssert.AreEquivalent(expected, actual);
            Assert.That(actual.Distinct().Count(), Is.EqualTo(22));
            CollectionAssert.DoesNotContain(actual, "prop.nmc_light_ramp");
        }

        [Test]
        public void TheRampPickupIsTheModern3500_InTheCandidate()
        {
            var yaml = WaterSideCandidate();
            var go = yaml.Docs.Single(d => d.ClassId == 1 && d.Field("m_Name") == "vehicle.nmc_ramp_pickup");
            var machine = yaml.ComponentsOf(go).Single(d => d.IsScript("HiddenHarbours.Vehicles.ParkedVehicle"));
            string guid = AssetDatabase.AssetPathToGUID("Assets/_Project/Data/Vehicles/Modern3500.asset");
            Assert.That(guid, Is.Not.Empty);
            StringAssert.Contains("guid: " + guid, machine.Field("_vehicle"));
            Assert.That(machine.Field("_drivable"), Is.EqualTo("1"));
        }

        [TestCase("west", "float")]
        [TestCase("fleet", "fingerA")]
        [TestCase("fleet", "fingerB")]
        [TestCase("east", "recFloat")]
        public void ASplitModuleReferencesOnlyItsThreeLayers_AndTheWholeImportIsRetired(string harbour, string module)
        {
            var yaml = WaterSideCandidate();
            var row = yaml.Docs.Single(d => d.ClassId == 1 && d.Field("m_Name") == "harbour." + harbour);
            var mine = yaml.SubtreeOf(row.FileId);
            var guids = new HashSet<string>();
            const string folder = "Assets/_Project/Art/Sprites/NineMileCreek/WaterSide/";
            foreach (string layer in new[] { "back", "body", "front" })
            {
                string name = layer == "body" ? module : module + "_" + layer;
                var node = yaml.Docs.Single(d => mine.Contains(d.FileId) && d.ClassId == 1 && d.Field("m_Name") == name);
                var renderer = yaml.ComponentsOf(node).Single(d => d.ClassId == 212);
                string guid = Regex.Match(renderer.Field("m_Sprite"), @"guid: ([a-f0-9]{32})").Groups[1].Value;
                Assert.That(guids.Add(guid), Is.True, name + " has its own layer texture");
                string expected = folder + "WaterSide_" + harbour + "_" + module + "_" + layer + "_0.png";
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(expected));
            }
            string retired = folder + "WaterSide_" + harbour + "_" + module + "_0.png";
            Assert.That(File.Exists(retired), Is.False, "the whole texture is retired from import");
            Assert.That(File.Exists(retired + ".meta"), Is.False);
            Assert.That(guids.Count, Is.EqualTo(3));
        }

        [Test]
        public void TheH6HundredKeepTheirTransforms_ToAMillimetre_InTheCandidate()
        {
            var frozen = new (long id, float x, float y, float z)[]
            {
                (1338658753L, 179.600006f, 126.400002f, 0f), // boat.nmc_damaged_dory
                (1538853976L, 96f, 141.553558f, 0f), // gull.nmc_flats
                (1693235975L, 91.5999985f, 130.779053f, 0f), // gull.nmc_market
                (1579098230L, 166f, 141.117905f, 0f), // gull.nmc_restaurant
                (165648399L, 116.199997f, 108.460098f, 0f), // gull.nmc_shed
                (850060642L, 0f, 0f, 0f), // harbour.yard
                (965868784L, 146.399994f, 98.5999985f, 0f), // prop.nmc_barrow
                (1833422571L, 134.399994f, 98.1999969f, 0f), // prop.nmc_baskets
                (675617427L, 104f, 97.5999985f, 0f), // prop.nmc_buoys_leo
                (319329213L, 119.400002f, 97.5999985f, 0f), // prop.nmc_buoys_marie
                (285807629L, 113f, 98.1999969f, 0f), // prop.nmc_nets_a
                (1648887692L, 81.1999969f, 110.199997f, 0f), // prop.nmc_notice
                (135959219L, 84f, 94.1999969f, 0f), // prop.nmc_quay_traps_0
                (401828063L, 127.5f, 94.5999985f, 0f), // prop.nmc_quay_traps_1
                (1032932238L, 150.5f, 94.4000015f, 0f), // prop.nmc_quay_traps_2
                (1451962453L, 158.800003f, 87f, 0f), // prop.nmc_ring_e
                (102011642L, 80.5999985f, 87f, 0f), // prop.nmc_ring_w
                (1177207442L, 94.5999985f, 99f, 0f), // prop.nmc_traps_0
                (263502863L, 95.3000031f, 100.400002f, 0f), // prop.nmc_traps_1
                (1766699788L, 110.199997f, 99.4000015f, 0f), // prop.nmc_traps_10
                (1731684554L, 110.900002f, 100.800003f, 0f), // prop.nmc_traps_11
                (1842003101L, 108f, 99.1999969f, 0f), // prop.nmc_traps_2
                (1103159823L, 108.699997f, 100.599998f, 0f), // prop.nmc_traps_3
                (1490207739L, 124.199997f, 99f, 0f), // prop.nmc_traps_4
                (989615864L, 139.600006f, 99.4000015f, 0f), // prop.nmc_traps_5
                (1435590276L, 140.300003f, 100.800003f, 0f), // prop.nmc_traps_6
                (1713710121L, 154.399994f, 99.1999969f, 0f), // prop.nmc_traps_7
                (764044812L, 165.199997f, 99.5999985f, 0f), // prop.nmc_traps_8
                (192806135L, 165.899994f, 101f, 0f), // prop.nmc_traps_9
                (1148982813L, 82f, 144f, 0f), // prop.nmc_traps_court_0_0
                (1734488556L, 83.3199997f, 144f, 0f), // prop.nmc_traps_court_0_1
                (1873077070L, 84.6399994f, 144f, 0f), // prop.nmc_traps_court_0_2
                (1594957225L, 85.9599991f, 144f, 0f), // prop.nmc_traps_court_0_3
                (422869812L, 87.2799988f, 144f, 0f), // prop.nmc_traps_court_0_4
                (1336574391L, 88.5999985f, 144f, 0f), // prop.nmc_traps_court_0_5
                (1262526772L, 89.9199982f, 144f, 0f), // prop.nmc_traps_court_0_6
                (2001370050L, 91.2399979f, 144f, 0f), // prop.nmc_traps_court_0_7
                (276343498L, 92.5599976f, 144f, 0f), // prop.nmc_traps_court_0_8
                (1499145910L, 82f, 142.880005f, 0f), // prop.nmc_traps_court_1_0
                (1362176250L, 83.3199997f, 142.880005f, 0f), // prop.nmc_traps_court_1_1
                (392128594L, 84.6399994f, 142.880005f, 0f), // prop.nmc_traps_court_1_2
                (620771301L, 85.9599991f, 142.880005f, 0f), // prop.nmc_traps_court_1_3
                (1219178851L, 87.2799988f, 142.880005f, 0f), // prop.nmc_traps_court_1_4
                (1311510144L, 88.5999985f, 142.880005f, 0f), // prop.nmc_traps_court_1_5
                (1298002133L, 89.9199982f, 142.880005f, 0f), // prop.nmc_traps_court_1_6
                (614071422L, 91.2399979f, 142.880005f, 0f), // prop.nmc_traps_court_1_7
                (561428101L, 92.5599976f, 142.880005f, 0f), // prop.nmc_traps_court_1_8
                (373961753L, 106.400002f, 109.599998f, 0f), // prop.nmc_traps_lot_0_0
                (832971804L, 107.720001f, 109.599998f, 0f), // prop.nmc_traps_lot_0_1
                (414411019L, 106.400002f, 108.480003f, 0f), // prop.nmc_traps_lot_1_0
                (1534859344L, 107.720001f, 108.480003f, 0f), // prop.nmc_traps_lot_1_1
                (1244577787L, 43.5999985f, 100.800003f, 0f), // prop.nmc_traps_ramp_0_0
                (2107072214L, 44.9199982f, 100.800003f, 0f), // prop.nmc_traps_ramp_0_1
                (976326378L, 43.5999985f, 99.6800003f, 0f), // prop.nmc_traps_ramp_1_0
                (909616093L, 44.9199982f, 99.6800003f, 0f), // prop.nmc_traps_ramp_1_1
                (745192767L, 174.800003f, 110.5f, 0f), // prop.nmc_traps_yard_w_0_0
                (316184017L, 176.119995f, 110.5f, 0f), // prop.nmc_traps_yard_w_0_1
                (1035425609L, 177.440002f, 110.5f, 0f), // prop.nmc_traps_yard_w_0_2
                (167683147L, 178.759995f, 110.5f, 0f), // prop.nmc_traps_yard_w_0_3
                (1899806786L, 174.800003f, 109.379997f, 0f), // prop.nmc_traps_yard_w_1_0
                (823343777L, 176.119995f, 109.379997f, 0f), // prop.nmc_traps_yard_w_1_1
                (1298234010L, 177.440002f, 109.379997f, 0f), // prop.nmc_traps_yard_w_1_2
                (1430823377L, 178.759995f, 109.379997f, 0f), // prop.nmc_traps_yard_w_1_3
                (2146105513L, 174.800003f, 108.260002f, 0f), // prop.nmc_traps_yard_w_2_0
                (876202425L, 176.119995f, 108.260002f, 0f), // prop.nmc_traps_yard_w_2_1
                (1205639182L, 177.440002f, 108.260002f, 0f), // prop.nmc_traps_yard_w_2_2
                (363517728L, 178.759995f, 108.260002f, 0f), // prop.nmc_traps_yard_w_2_3
                (175185111L, 174.800003f, 107.139999f, 0f), // prop.nmc_traps_yard_w_3_0
                (1944548913L, 176.119995f, 107.139999f, 0f), // prop.nmc_traps_yard_w_3_1
                (1498574501L, 177.440002f, 107.139999f, 0f), // prop.nmc_traps_yard_w_3_2
                (1999166376L, 178.759995f, 107.139999f, 0f), // prop.nmc_traps_yard_w_3_3
                (145290156L, 174.800003f, 106.019997f, 0f), // prop.nmc_traps_yard_w_4_0
                (1293336290L, 176.119995f, 106.019997f, 0f), // prop.nmc_traps_yard_w_4_1
                (1028440428L, 177.440002f, 106.019997f, 0f), // prop.nmc_traps_yard_w_4_2
                (1257083135L, 178.759995f, 106.019997f, 0f), // prop.nmc_traps_yard_w_4_3
                (1595963769L, 136.5f, 131.199997f, 0f), // structure.nmc_bait_shed
                (1658062942L, 64f, 99.1999969f, 0f), // structure.nmc_cold_store
                (171148209L, 91.1999969f, 127.599998f, 0f), // structure.nmc_fish_market
                (1391182557L, 86.4000015f, 112.599998f, 0f), // structure.nmc_harbourmaster
                (1694645947L, 168f, 136f, 0f), // structure.nmc_restaurant
                (475959469L, 118.5f, 131f, 0f), // structure.nmc_shed_alma
                (1753885130L, 110.5f, 131f, 0f), // structure.nmc_shed_celeste
                (1588456734L, 125.599998f, 131f, 0f), // structure.nmc_shed_dan
                (1673998848L, 160.300003f, 101.800003f, 0f), // structure.nmc_shed_hughie
                (1376126406L, 101f, 101.800003f, 0f), // structure.nmc_shed_leo
                (1606668256L, 118.800003f, 101.800003f, 0f), // structure.nmc_shed_marie
                (569417620L, 149.300003f, 101.800003f, 0f), // structure.nmc_shed_ross
                (2145785558L, 133.600006f, 101.800003f, 0f), // structure.nmc_shed_yvette
                (118560183L, 152f, 131.399994f, 0f), // structure.nmc_trap_store
                (1809494775L, 98.1999969f, 128.399994f, 0f), // vehicle.nmc_buyer_truck
                (857145863L, 171.199997f, 127.800003f, 0f), // vehicle.nmc_delivery
                (538537957L, 103.800003f, 128.399994f, 0f), // vehicle.nmc_lot_a
                (1474228625L, 142.600006f, 128f, 0f), // vehicle.nmc_lot_b
                (489877244L, 145.399994f, 128f, 0f), // vehicle.nmc_lot_c
                (589154137L, 101f, 128.399994f, 0f), // vehicle.nmc_lot_e
                (759933381L, 99.5f, 91.1999969f, 0f), // vehicle.nmc_pickup_leo
                (1420472359L, 121f, 91f, 0f), // vehicle.nmc_pickup_marie
                (2125258142L, 141.5f, 91.5999985f, 0f), // vehicle.nmc_pickup_ross
                (1556425803L, 65.5749969f, 133f, 0f), // vehicle.nmc_reefer_tractor
                (2077148380L, 56f, 133f, 0f), // vehicle.nmc_reefer_trailer
            };
            var yaml = WaterSideCandidate();
            foreach (var at in frozen)
            {
                string position = yaml.Require(at.id, "H6 frozen transform").Field("m_LocalPosition");
                var numbers = Regex.Matches(position, @"[xyz]: ([^,}]+)").Cast<Match>()
                    .Select(m => float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                Assert.That(numbers, Has.Length.EqualTo(3), at.id.ToString());
                Assert.That(numbers[0], Is.EqualTo(at.x).Within(0.001f), at.id + " x");
                Assert.That(numbers[1], Is.EqualTo(at.y).Within(0.001f), at.id + " y");
                Assert.That(numbers[2], Is.EqualTo(at.z).Within(0.001f), at.id + " z");
            }

        }

        [Test]
        public void TheElevenFuelStopRows_WaitForH9_AndRemainOutsideThePlacementSubset()
        {
            string[] ids = { "prop.nmc_dumpster", "prop.nmc_picnic", "prop.nmc_ice", "prop.nmc_bin",
                "prop.nmc_picnic_r", "prop.nmc_light_court", "prop.nmc_light_lot", "prop.nmc_light_quay_w",
                "prop.nmc_light_quay_e", "prop.nmc_light_ramp", "prop.nmc_light_lot_e" };
            Record record = Read();
            KeySceneDef scene = Scene();
            CollectionAssert.AreEquivalent(ids, record.rows.Where(r => r.waits == "fuelStopRig intake (H9)").Select(r => r.id).ToArray());
            foreach (string id in ids)
            {
                Assert.AreEqual("fuelStopRig (pass 6)", scene.Pieces.Single(p => p.Id == id).Kit, id);
                CollectionAssert.DoesNotContain(scene.PlacementIds, id);
                // seat ruling s8, 11-07, Decision 4 lean a: the ramp light remains H5's waiting row.
                Assert.AreEqual(id == "prop.nmc_light_ramp" ? "H5" : "H6", record.rows.Single(r => r.id == id).owner, id);
            }
        }

        [Test]
        public void TheWharfsParkedPickups_AreTheNewModels_RowByRow()
        {
            var yaml = StPetersLayerRefresh.SceneYaml.Parse(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath));
            var expected = new Dictionary<string, string>
            {
                ["vehicle.nmc_lot_e"] = "Modern2500",
                ["vehicle.nmc_lot_a"] = "Modern350",
                ["vehicle.nmc_lot_c"] = "Modern3500",
                ["vehicle.nmc_pickup_leo"] = "Modern350",
                ["vehicle.nmc_pickup_ross"] = "Modern2500",
                ["vehicle.nmc_pickup_marie"] = "Modern350",
            };
            foreach (var pair in expected)
            {
                var matches = yaml.Docs.Where(d => d.ClassId == 1 && d.Field("m_Name") == pair.Key).ToArray();
                Assert.That(matches.Length, Is.EqualTo(1), pair.Key);
                var machine = yaml.ComponentsOf(matches[0]).Single(d => d.IsScript("HiddenHarbours.Vehicles.ParkedVehicle"));
                string guid = AssetDatabase.AssetPathToGUID("Assets/_Project/Data/Vehicles/" + pair.Value + ".asset");
                Assert.That(guid, Is.Not.Empty, pair.Key);
                StringAssert.Contains("guid: " + guid, machine.Field("_vehicle"), pair.Key);
                Assert.That(machine.Field("_drivable"), Is.EqualTo("1"), pair.Key);
            }
            Assert.That(NineMileCreekWharfLandSide.ParkedPickupDefinition("vehicle.nmc_ramp_pickup"),
                Is.EqualTo("Assets/_Project/Data/Vehicles/Modern3500.asset"));
            // seat ruling s9, 11-07: the ramp-pickup expectation returns with Phase B's placement.
            Assert.That(yaml.Docs.Any(d => d.ClassId == 1 && d.Field("m_Name") == "vehicle.nmc_ramp_pickup"), Is.False,
                "The ramp pickup waits for H5; this phase places no H5 row.");
            Assert.Throws<StPetersLayerRefresh.Refusal>(() => NineMileCreekWharfLandSide.ParkedPickupDefinition("vehicle.fixture_unassigned"));
        }

        [Test]
        public void AnUnknownPlaceIsUnowned_AndTwoOwnersAreVisibleToTheGate()
        {
            Record record = Read();
            var outside = new KeyScenePiece { Id = "prop.fixture_outside", At = new Vector2(0, 1000), Kit = "wharfDecor", Piece = "trapStack" };
            Assert.IsEmpty(OwnersFor(outside, record));
            var overlap = new KeyScenePiece { Id = "boat.fixture_overlap", Kind = "boat", StandsOn = "water", At = new Vector2(60, 80) };
            // seat ruling s8, 11-07, Decision 4 lean a: N8 yields to the asset's H5 apron rule.
            CollectionAssert.AreEquivalent(new[] { "H5" }, OwnersFor(overlap, record));
            var twoOwners = new Record
            {
                rules = new[]
                {
                    new Rule { owner = "H5", ids = new[] { overlap.Id } },
                    new Rule { owner = "N8", kinds = new[] { new KindRule { kind = "boat", standsOn = "water" } } },
                },
            };
            CollectionAssert.AreEquivalent(new[] { "H5", "N8" }, OwnersFor(overlap, twoOwners));
        }
    }
}
