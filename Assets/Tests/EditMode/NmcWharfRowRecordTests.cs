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
        public void TheRowsThisPhasePlaces_AreASubsetOfH6()
        {
            Record record = Read();
            KeySceneDef scene = Scene();
            var h6 = new HashSet<string>(record.rows.Where(r => r.owner == "H6").Select(r => r.id));
            foreach (string id in scene.PlacementIds) Assert.IsTrue(h6.Contains(id), "non-H6 placement: " + id);
            foreach (string id in new[] { "structure.nmc_harbourmaster", "structure.nmc_fish_market", "structure.nmc_restaurant" })
                CollectionAssert.Contains(scene.PlacementIds, id);
            Assert.IsTrue(scene.Placed, "This phase must activate its explicit placement subset.");
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
                Assert.AreEqual(id == "prop.nmc_light_ramp" ? "H5" : "H6", record.rows.Single(r => r.id == id).owner, id);
            }
        }

        [Test]
        public void AnUnknownPlaceIsUnowned_AndTwoOwnersAreVisibleToTheGate()
        {
            Record record = Read();
            var outside = new KeyScenePiece { Id = "prop.fixture_outside", At = new Vector2(0, 1000), Kit = "wharfDecor", Piece = "trapStack" };
            Assert.IsEmpty(OwnersFor(outside, record));
            var overlap = new KeyScenePiece { Id = "boat.fixture_overlap", Kind = "boat", StandsOn = "water", At = new Vector2(60, 80) };
            CollectionAssert.AreEquivalent(new[] { "H5", "N8" }, OwnersFor(overlap, record));
        }
    }
}
