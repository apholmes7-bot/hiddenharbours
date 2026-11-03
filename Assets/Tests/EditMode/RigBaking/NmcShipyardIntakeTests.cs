using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    public class NmcShipyardIntakeTests
    {
        static readonly string[] Keys = { "yardFront", "pierW", "pierE", "lift", "slab", "hardA", "hardB", "hardC", "shed", "floodW", "floodE", "fence", "props" };
        static object Member(object o, string key) => DeckSidecarJson.Member(o, key);
        static object Today() => DeckSidecarJson.Parse(File.ReadAllText(NmcShipyardKit.TodayPath));

        [Test]
        public void TheDeliveredRig_IsUnchangedByItsPinnedBytesAndSum()
        {
            byte[] bytes = File.ReadAllBytes(NmcShipyardKit.Folder + "/shipyardRig2.js");
            Assert.That(bytes.Length, Is.EqualTo(61024));
            using var hash = SHA256.Create();
            Assert.That(string.Concat(hash.ComputeHash(bytes).Select(b => b.ToString("x2"))),
                Is.EqualTo("f228320e85e8045d73a56058aa1ff82723320ff33d62a04c99e7986228eb3fb3"));
        }

        [Test]
        public void TheWholeYard_HasExactlyTheThirteenDeliveredParts_WithExistingDefs()
        {
            CollectionAssert.AreEqual(Keys, NmcShipyardKit.Keys);
            var pieces = NineMileCreekWharfYard.Read().pieces;
            CollectionAssert.AreEquivalent(Keys, pieces.Select(p => p.key));
            CollectionAssert.AreEquivalent(new[] { "structure.nmc_yard_yard_front", "structure.nmc_yard_pier_w", "structure.nmc_yard_pier_e",
                "structure.nmc_yard_lift", "structure.nmc_yard_slab", "structure.nmc_yard_hard_a", "structure.nmc_yard_hard_b", "structure.nmc_yard_hard_c",
                "structure.nmc_yard_shed", "structure.nmc_yard_flood_w", "structure.nmc_yard_flood_e", "structure.nmc_yard_fence", "structure.nmc_yard_props" }, pieces.Select(p => p.id));
            foreach (object p in DeckSidecarJson.AsArray(Member(Today(), "pieces")))
                Assert.That(DeckSidecarJson.String(Member(Member(p, "gameplay"), "defType")), Is.EqualTo("SetPieceDef"));
        }

        [Test]
        public void EveryPartPlansUnderTheKitsOneCap_AndTheTodayRecordsThatCap()
        {
            Assert.That(NmcShipyardKit.ImportSizeCap, Is.EqualTo(4096));
            Assert.That(NmcShipyardKit.Definition.ImportSizeCap, Is.EqualTo(4096));
            Assert.That(DeckSidecarJson.Float(Member(Member(Today(), "bake"), "importSizeCap")), Is.EqualTo(4096));
            foreach (object p in DeckSidecarJson.AsArray(Member(Today(), "pieces")))
            {
                var cells = DeckSidecarJson.AsArray(Member(Member(p, "gameplay"), "cells"));
                int w = (int)cells.Max(c => DeckSidecarJson.Float(Member(c, "w")));
                int h = (int)cells.Max(c => DeckSidecarJson.Float(Member(c, "h")));
                Assert.That(NavBuoyKit.PlanSheet(w, h, 8, 4096, out _, out _, out _, out _), Is.True,
                    DeckSidecarJson.String(Member(p, "key")));
            }
        }

        [Test]
        public void TheDoorIsThePublishedShedDoor_AndTheOutboardStandsOnTheYardQuay()
        {
            Assert.That(NineMileCreekWharfYard.Door.x, Is.EqualTo(197.8f).Within(0.001));
            Assert.That(NineMileCreekWharfYard.Door.y, Is.EqualTo(123.9f).Within(0.001));
            Assert.That(NineMileCreekWharfYard.Quay, Is.EqualTo(Rect.MinMaxRect(184, 86, 212, 96)));
            Assert.That(NineMileCreekWharfYard.Quay.Contains(NineMileCreekBuilder.HectorsBarrelPos), Is.True);
            Assert.That((Vector2)NineMileCreekBuilder.HectorsBarrelPos, Is.EqualTo(NineMileCreekTrips.DoryYardPost()));
            Assert.That(NineMileCreekGroundPlan.YardRoadEnd, Is.EqualTo(new Vector2(190,120)));
        }

        [Test]
        public void TheWalksAndSolidsAreSeparate_AndTheShedDoorIsAnOpening()
        {
            var pieces = DeckSidecarJson.AsArray(Member(Today(), "pieces"));
            object Part(string key) => Member(pieces.Single(p => DeckSidecarJson.String(Member(p, "key")) == key), "gameplay");
            foreach (string key in new[] { "yardFront", "pierW", "pierE", "slab", "shed" })
                Assert.That(DeckSidecarJson.AsArray(Member(Part(key), "walkSurfaces")).Count, Is.GreaterThan(0), key);
            foreach (string key in new[] { "lift", "hardA", "hardB", "hardC", "shed", "fence", "props" })
                Assert.That(DeckSidecarJson.AsArray(Member(Part(key), "colliders")).Count, Is.GreaterThan(0), key);
            object shed = Part("shed");
            var door = DeckSidecarJson.AsArray(Member(Member(shed, "anchors"), "door"));
            var at = new Vector2(DeckSidecarJson.Float(door[0]), DeckSidecarJson.Float(door[1]));
            foreach (object c in DeckSidecarJson.AsArray(Member(shed, "colliders")))
            {
                var points = DeckSidecarJson.AsArray(Member(c, "pts")).Select(p => DeckSidecarJson.AsArray(p))
                    .Select(p => new Vector2(DeckSidecarJson.Float(p[0]), DeckSidecarJson.Float(p[1]))).ToArray();
                Assert.That(new HiddenHarbours.World.GroundPolygon(points).Contains(at), Is.False, "the door is not a solid");
            }
        }

        [Test]
        public void EveryBakedDefAndImportedSheet_PreservesTheRigAndItsSize()
        {
            var contract = JsonUtility.FromJson<HiddenHarbours.Art.Editor.SetPieceSheetSlicer.Contract>(
                File.ReadAllText(NmcShipyardKit.SheetFolder + "/nmcShipyard.contract.json"));
            Assert.That(contract.importSizeCap, Is.EqualTo(4096));
            Assert.That(contract.pieces.Count, Is.EqualTo(13));
            foreach (var sheet in contract.pieces)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(NmcShipyardKit.SheetFolder + "/SetPiece_" + sheet.key + ".png");
                Assert.That(texture, Is.Not.Null, sheet.key);
                Assert.That((texture.width, texture.height), Is.EqualTo((sheet.sheetW, sheet.sheetH)), sheet.key);
                SetPieceDef def = NineMileCreekWharfYard.Definition(sheet.key);
                Assert.That(def.HasArt, Is.True, sheet.key);
                Assert.That(def.Id, Is.EqualTo(NineMileCreekWharfYard.Read().pieces.Single(p => p.key == sheet.key).id));
                Assert.That(def.Frames.Length, Is.EqualTo(8));
            }
        }

        [Test]
        public void TheCommittedYard_HasItsTradesAndSupports_AndNoOldStation()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath));
            foreach (string name in new[] { "ShipwrightDoryYard", "ShipwrightShed", "UsedOutboardSeller", "BoatShed" })
                Assert.That(scene.Docs.Any(d => d.ClassId == 1 && d.Field("m_Name") == name), Is.False, name);
            var yard = scene.RootNamed(NineMileCreekWharfYard.RootName);
            var inside = scene.SubtreeOf(yard.FileId);
            Assert.That(scene.Docs.Count(d => inside.Contains(d.FileId) && d.IsScript("HiddenHarbours.Art.SetPiecePlacement")), Is.EqualTo(13));
            Assert.That(scene.Docs.Count(d => inside.Contains(d.FileId) && d.IsScript("HiddenHarbours.World.StandablePlatform")), Is.EqualTo(5));
            foreach (string name in new[] { NineMileCreekWharfYard.CatalogueName, NineMileCreekWharfYard.DoryTradeName, NineMileCreekWharfYard.OutboardTradeName })
                Assert.That(scene.TransformOf(scene.RootNamed(name)).Field("m_LocalPosition"), Is.Not.Empty);
        }
    }
}
