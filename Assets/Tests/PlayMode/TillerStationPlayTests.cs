using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The dory's tiller seat, wired for real.</b> The committed dory hulls skinned through
    /// <see cref="BoatHullSkinner.ApplyHull"/> with the self-registered presentation service, over real
    /// frames: the outboard dory gains the stern bench (the motor fit) and the tiller station beside
    /// her engine, and its grip is where the engine's mesh puts it; the rowed dory gains neither, and
    /// swapping her back down takes both away. Headless-safe: no camera is created, nothing renders.
    /// </summary>
    public class TillerStationPlayTests
    {
        const string BoatsFolder = "Assets/_Project/Data/Boats";

        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform hit = FindDeep(t.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        static int CountDeep(Transform t, string name)
        {
            int n = t.name == name ? 1 : 0;
            for (int i = 0; i < t.childCount; i++) n += CountDeep(t.GetChild(i), name);
            return n;
        }

        [UnityTest]
        public IEnumerator TheOutboardDory_GainsHerBenchAndTillerSeat_TheRowedDoryNeither()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed dories.");
            yield break;
#else
            var outboard = AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{BoatsFolder}/DoryOutboard.asset");
            var rowed = AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{BoatsFolder}/Dory.asset");
            Assert.IsNotNull(outboard, "missing the committed DoryOutboard hull");
            Assert.IsNotNull(rowed, "missing the committed Dory hull");
            Assert.AreSame(outboard.Visual, rowed.Visual, "both dories wear the one dory visual");
            Assert.IsNotNull(HullMeshPresentation.Service, "the Art presentation service self-registers");

            _root = new GameObject("Dory");
            var rig = BoatHullSkinner.ApplyHull(_root, null, outboard, boat: null);
            Assert.IsTrue(rig.Skinned, "the outboard dory skins");
            yield return null;   // one real LateUpdate: the engine poses

            Transform fit = FindDeep(_root.transform, BoatHullSkinner.MotorFitChildName);
            Assert.IsNotNull(fit, "the outboard dory wears her stern bench and motor board");
            Assert.IsNotNull(fit.GetComponent<IsoFacetPropRenderer>(), "the motor fit is a mesh fitting");
            Assert.AreEqual(1, CountDeep(_root.transform, BoatHullSkinner.MotorFitChildName), "exactly one motor fit");

            var station = _root.GetComponent<OutboardTillerStation>();
            Assert.IsNotNull(station, "the tiller station rides beside her engine");
            var motor = _root.GetComponent<OutboardMotorMeshLayer>();
            Assert.IsNotNull(motor, "her engine is drawn");
            ITillerStation seat = station;
            Assert.IsTrue(seat.TryGetTillerTargets(out RowingTargets t), "the station answers once her engine is wired");

            BoatVisualDef visual = outboard.Visual;
            Vector3 expected = TillerStationMath.Grip(visual.Tiller.TillerGripAtRestLocalMeters,
                visual.MotorMesh.PivotLocalMeters,
                OutboardMotorMeshPose.Rotation(motor.SteerDegrees, motor.TiltDegrees),
                HullPropFitment.Mount(motor.MountMeters(0), visual.MotorMeshFitmentOffsetMeters));
            Vector3 held = t.StarboardGripValid ? t.StarboardGrip : t.PortGrip;
            Assert.AreEqual(0f, (held - expected).magnitude, 1e-5f, "the held grip is the tiller where the engine's mesh puts it");
            Assert.AreEqual(visual.Tiller.SeatLocalMeters, t.Seat, "he sits on the station's seat");

            // Sold back down to oars: the bench and the seat go with the engine.
            rig = BoatHullSkinner.ApplyHull(_root, null, rowed, boat: null);
            Assert.IsTrue(rig.Skinned, "the rowed dory skins");
            yield return null;
            Assert.IsNull(_root.GetComponent<OutboardTillerStation>(), "no tiller seat on the rowed dory");
            Assert.AreEqual(0, CountDeep(_root.transform, BoatHullSkinner.MotorFitChildName), "the rowed dory stays plain");

            // And up again: one of each, not two.
            BoatHullSkinner.ApplyHull(_root, null, outboard, boat: null);
            yield return null;
            Assert.AreEqual(1, _root.GetComponents<OutboardTillerStation>().Length, "one tiller seat after a re-skin");
            Assert.AreEqual(1, CountDeep(_root.transform, BoatHullSkinner.MotorFitChildName), "one motor fit after a re-skin");
#endif
        }

        [UnityTest]
        public IEnumerator ThePunt_SeatsHerSkipperAtHerTiller_OnHerOwnSternBench()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed punt.");
            yield break;
#else
            var punt = AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{BoatsFolder}/Punt.asset");
            Assert.IsNotNull(punt, "missing the committed Punt hull");
            Assert.IsNotNull(HullMeshPresentation.Service, "the Art presentation service self-registers");

            _root = new GameObject("Punt");
            var rig = BoatHullSkinner.ApplyHull(_root, null, punt, boat: null);
            Assert.IsTrue(rig.Skinned, "the punt skins");
            yield return null;   // one real LateUpdate: the engine poses

            var station = _root.GetComponent<OutboardTillerStation>();
            Assert.IsNotNull(station, "the punt's tiller station rides beside her engine");
            Assert.AreEqual(0, CountDeep(_root.transform, BoatHullSkinner.MotorFitChildName),
                "the punt's stern bench is part of her hull: no motor fit is hung");
            var motor = _root.GetComponent<OutboardMotorMeshLayer>();
            Assert.IsNotNull(motor, "her engine is drawn");
            ITillerStation seat = station;
            Assert.IsTrue(seat.TryGetTillerTargets(out RowingTargets t), "the station answers once her engine is wired");

            BoatVisualDef visual = punt.Visual;
            Vector3 expected = TillerStationMath.Grip(visual.Tiller.TillerGripAtRestLocalMeters,
                visual.MotorMesh.PivotLocalMeters,
                OutboardMotorMeshPose.Rotation(motor.SteerDegrees, motor.TiltDegrees),
                HullPropFitment.Mount(motor.MountMeters(0), visual.MotorMeshFitmentOffsetMeters));
            Vector3 held = t.StarboardGripValid ? t.StarboardGrip : t.PortGrip;
            Assert.AreEqual(0f, (held - expected).magnitude, 1e-5f, "the held grip is the tiller where the engine's mesh puts it");
            Assert.AreEqual(visual.Tiller.SeatLocalMeters, t.Seat, "he sits on the station's seat");
            Assert.AreEqual(0f, t.TurnStarboardDegrees, 1e-5f, "with nobody at the helm the engine is dead ahead and he sits square");
#endif
        }
    }
}
