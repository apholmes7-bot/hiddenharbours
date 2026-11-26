using System;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>The north-east light: where the lens points is the clock's alone, the station is data, and the
    /// scene holds exactly what the station's step would write.</summary>
    public class StPetersLightStationTests
    {
        const string DefPath = LightStationFolder + "/LightStation_stp_ne_light.asset";

        static LightStationDef Def()
        {
            var def = AssetDatabase.LoadAssetAtPath<LightStationDef>(DefPath);
            Assert.That(def, Is.Not.Null, "no light station at " + DefPath);
            return def;
        }

        [Test]
        public void TheBearing_IsAFunctionOfTheClockAlone()
        {
            Assert.That(LightSweep.BearingDegrees(0d, 12f, 0f), Is.EqualTo(0f));
            Assert.That(LightSweep.BearingDegrees(3d, 12f, 0f), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(LightSweep.BearingDegrees(9d, 12f, 0f), Is.EqualTo(270f).Within(1e-4f));
            Assert.That(LightSweep.BearingDegrees(12d, 12f, 0f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(LightSweep.BearingDegrees(1d, 12f, 2f), Is.EqualTo(90f).Within(1e-4f), "the phase is seconds into the turn");
            Assert.That(LightSweep.BearingDegrees(-3d, 12f, 0f), Is.EqualTo(270f).Within(1e-4f), "a clock before zero still turns the same way");
            Assert.That(LightSweep.BearingDegrees(5d, 0f, 0f), Is.EqualTo(0f), "a lens that cannot turn points one way");

            // A year of game seconds in: the same second gives the same bearing, to the bit, and a turn later it is back.
            double late = 365d * 86400d + 4.25d;
            float a = LightSweep.BearingDegrees(late, 12f, 0f);
            Assert.That(BitConverter.GetBytes(LightSweep.BearingDegrees(late, 12f, 0f)), Is.EqualTo(BitConverter.GetBytes(a)));
            Assert.That(LightSweep.BearingDegrees(late + 12d, 12f, 0f), Is.EqualTo(a).Within(1e-3f));
            Assert.That(a, Is.EqualTo(4.25f / 12f * 360f).Within(1e-3f));
            for (double t = 0d; t < 40d; t += 0.37d)
                Assert.That(LightSweep.BearingDegrees(t, 12f, 0f), Is.InRange(0f, 360f).And.LessThan(360f));
        }

        [Test]
        public void TwoBeamsOnATwelveSecondTurn_FlashEverySixSeconds()
        {
            Assert.That(LightSweep.FlashEverySeconds(12f, 2), Is.EqualTo(6f));
            Assert.That(LightSweep.IsFlashing(LightSweep.TowardViewerDegrees, 2, 14f), Is.True);
            Assert.That(LightSweep.IsFlashing(LightSweep.TowardViewerDegrees - 180f, 2, 14f), Is.True, "the second beam flashes too");
            Assert.That(LightSweep.IsFlashing(0f, 2, 14f), Is.False);
            Assert.That(LightSweep.OffViewerDegrees(0f, 2), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(LightSweep.OffViewerDegrees(0f, 1), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(LightSweep.OffViewerDegrees(90f, 1), Is.EqualTo(180f).Within(1e-4f), "one beam pointing away is as far off as it gets");

            int flashes = 0; bool was = false; double first = -1d, last = -1d;
            const double step = 0.01d;
            for (double t = 0.005d; t < 120d; t += step)
            {
                bool on = LightSweep.IsFlashing(LightSweep.BearingDegrees(t, 12f, 0f), 2, 14f);
                if (on && !was) { flashes++; if (first < 0d) first = t; last = t; }
                was = on;
            }
            Assert.That(flashes, Is.EqualTo(20), "twenty flashes in two minutes");
            Assert.That((last - first) / (flashes - 1), Is.EqualTo(6d).Within(step * 2d));
        }

        [Test]
        public void ABeamRunningAway_IsDrawnSqueezedLikeTheGround()
        {
            float k = IsoGround.GroundDepthScale;
            Assert.That(LightSweep.DrawnDegrees(0f, k), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(LightSweep.DrawnDegrees(90f, k), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(LightSweep.DrawnDegrees(180f, k), Is.EqualTo(180f).Within(1e-4f));
            Assert.That(LightSweep.DrawnDegrees(270f, k), Is.EqualTo(270f).Within(1e-4f));
            Assert.That(LightSweep.DrawnDegrees(45f, k), Is.EqualTo(Mathf.Atan(k) * Mathf.Rad2Deg).Within(1e-3f));
            Assert.That(LightSweep.DrawnDegrees(45f, k), Is.LessThan(45f));
            // Two beams opposite on the ground stay opposite on the screen.
            for (float b = 0f; b < 180f; b += 7f)
                Assert.That(Mathf.DeltaAngle(LightSweep.DrawnDegrees(b, k), LightSweep.DrawnDegrees(b + 180f, k)),
                    Is.EqualTo(180f).Within(1e-2f).Or.EqualTo(-180f).Within(1e-2f));
            Assert.That(LightSweep.DrawnReach(0f, 10f, k), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(LightSweep.DrawnReach(90f, 10f, k), Is.EqualTo(10f * k).Within(1e-4f));
        }

        [Test]
        public void TheNorthEastLight_IsData_AndAgreesWithTheRigThatDrewIt()
        {
            LightStationDef def = Def();
            Assert.That(def.Id, Is.EqualTo("lightstation.stp_ne_light"));
            Assert.That(def.KeySceneId, Is.EqualTo("keyscene.stp_ne_light"));

            var placements = LoadLightStations();
            Assert.That(placements.Count, Is.EqualTo(1));
            Assert.That(placements[0].Def, Is.SameAs(def));
            Assert.That(placements[0].TowerAt, Is.EqualTo(new Vector2(206.5f, 58.8f)), "the tower stands where the key scene's row says");

            LightStationContract contract = LoadLightStationContract();
            Assert.That(contract.parts.Select(p => p.name), Is.EqualTo(new[] { "tower", "keepersHouse", "shed" }));
            Assert.That(contract.character.name, Is.EqualTo(def.Character), "the rig's character is the light list's");
            Assert.That(LightSweep.FlashEverySeconds(def.TurnSeconds, def.Beams), Is.EqualTo(contract.character.flashEverySeconds));
            Assert.That(def.Beams, Is.EqualTo(contract.character.beams));
            Assert.That(contract.lantern.riseMetres, Is.EqualTo(contract.lantern.focalMetres * IsoGround.HeightScale).Within(0.01f));
            foreach (var part in contract.parts)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LightStationSprites + "/" + part.sprite);
                Assert.That(sprite, Is.Not.Null, part.sprite);
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(contract.pixelsPerMetre));
                Assert.That(sprite.pivot.x / sprite.rect.width, Is.EqualTo(part.pivot.x).Within(1e-3f), part.sprite + " pivot x");
                Assert.That(sprite.pivot.y / sprite.rect.height, Is.EqualTo(part.pivot.y).Within(1e-3f), part.sprite + " pivot y");
            }
        }

        [Test]
        public void TheScene_HoldsExactlyTheStationItsStepWouldWrite()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(ScenePath));
            var placements = LoadLightStations();
            LayerPatch patch = LightStation(scene, placements, LoadLightStationContract(), new EditorLightStationAssets());
            Assert.That(patch.IsEmpty, Is.True, "StPeters.unity is not what the light station step writes:\n" + patch.Summary());

            var root = scene.RootNamed(LightStationRootName);
            var owned = scene.SubtreeOf(root.FileId);
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.ClassId == 212), Is.EqualTo(3), "tower, house, shed");
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.ClassId == 60), Is.EqualTo(3), "each stands on a footprint");
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.IsScript(typeof(SceneLight).FullName)),
                Is.EqualTo(3 + placements[0].Def.Beams), "the lantern, the flash, the keeper's lamp and the beams");
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.IsScript(typeof(PreconfiguredLight).FullName)), Is.EqualTo(1));
            var lamp = scene.Docs.Single(d => owned.Contains(d.FileId) && d.IsScript(typeof(LightStationLamp).FullName));
            Assert.That(lamp.FieldRefs("_beams").Count(), Is.EqualTo(placements[0].Def.Beams));
            foreach (long beam in lamp.FieldRefs("_beams")) Assert.That(owned.Contains(beam), Is.True);
        }

        [Test]
        public void TheLantern_PointsItsBeamsWhereTheSweepSays_AndShowsTheFlashOnlyThen()
        {
            LightStationDef def = Def();
            var go = new GameObject("lamp under test");
            var flashGo = new GameObject("flash");
            try
            {
                go.SetActive(false);
                var lamp = go.AddComponent<LightStationLamp>();
                var beams = new Transform[def.Beams];
                for (int i = 0; i < beams.Length; i++)
                {
                    beams[i] = new GameObject("beam" + i).transform;
                    beams[i].SetParent(go.transform, false);
                }
                flashGo.transform.SetParent(go.transform, false);
                var flash = flashGo.AddComponent<BoxCollider2D>();   // any Behaviour stands in for the flash lamp
                var so = new SerializedObject(lamp);
                so.FindProperty("_def").objectReferenceValue = def;
                var list = so.FindProperty("_beams");
                list.arraySize = beams.Length;
                for (int i = 0; i < beams.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = beams[i];
                so.FindProperty("_flash").objectReferenceValue = flash;
                so.ApplyModifiedPropertiesWithoutUndo();

                lamp.Turn();
                float drawn = LightSweep.DrawnDegrees(lamp.BearingDegrees, IsoGround.GroundDepthScale);
                Vector2 up = beams[0].up;
                Assert.That(Mathf.DeltaAngle(Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg, drawn), Is.EqualTo(0f).Within(0.01f),
                    "a lamp throws along its up, and the first beam's up is the sweep's drawn bearing");
                if (beams.Length == 2)
                    Assert.That(Vector2.Dot(beams[0].up, beams[1].up), Is.EqualTo(-1f).Within(1e-3f), "two beams, back to back");
                Assert.That(flash.enabled, Is.EqualTo(LightSweep.IsFlashing(lamp.BearingDegrees, def.Beams, def.FlashHalfAngleDegrees)));
                Assert.That(lamp.IsFlashing, Is.EqualTo(flash.enabled));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
