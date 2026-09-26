using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // Capture AFTER production LateUpdate. Reading in the next coroutine alone could let
    // ReflectiveObject.Update repair a dropped pivot before the assertion sees it.
    [DefaultExecutionOrder(10000)]
    public sealed class HullReflectionLateProbe : MonoBehaviour
    {
        public Renderer Target;
        public Vector4 Origin, HullOrigin;
        public float Lit;
        public int SampleFrame = -1;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        void LateUpdate()
        {
            Target.GetPropertyBlock(_block);
            Origin = _block.GetVector(ReflectionShaderIds.ReflectOrigin);
            Lit = _block.GetFloat(ReflectionShaderIds.ReflectLit);
            HullOrigin = _block.GetVector(IsoFacetShaderIds.HullOrigin);
            SampleFrame = Time.frameCount;
        }
    }

    public class HullReflectionPivotPlayTests
    {
        readonly List<GameObject> _roots = new List<GameObject>();

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            foreach (var root in _roots) Object.DestroyImmediate(root);
            _roots.Clear();
            GameServices.Reset();
        }

        static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        HullReflectionLateProbe MakeHull(string name, Vector3 position, bool lit,
            out MeshHullDriver driver, out ReflectiveObject reflector)
        {
            var root = new GameObject(name);
            _roots.Add(root);
            root.transform.position = position;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var hull = visual.AddComponent<IsoFacetHullRenderer>();
            var mesh = new GameObject("FacetMesh");
            mesh.transform.SetParent(visual.transform, false);
            var overlay = new GameObject("HullOverlay");
            overlay.transform.SetParent(visual.transform, false);
            // Only inject the CPU pose dependencies: no shader/material/asset or rendering needed.
            Set(hull, "_setup", new IsoFacetHullSetup { PxPerMetre = 32, ElevationDeg = 40f });
            Set(hull, "_meshChild", mesh.transform);
            Set(hull, "_meshRenderer", mesh.AddComponent<MeshRenderer>());
            Set(hull, "_overlayChild", overlay.transform);
            var renderer = overlay.AddComponent<MeshRenderer>();
            Set(hull, "_overlayRenderer", renderer);
            driver = root.AddComponent<MeshHullDriver>();
            driver.Configure(visual.transform, hull, null, 0f);
            reflector = overlay.AddComponent<ReflectiveObject>();
            Set(reflector, "_nightLitSource", lit);
            Set(reflector, "_gateInterval", 1000f);
            reflector.Refresh();
            var probe = root.AddComponent<HullReflectionLateProbe>();
            probe.Target = renderer;
            return probe;
        }

        [UnityTest]
        public IEnumerator HullReflection_StaticAndMovingPivotsSurviveLateUpdate()
        {
            var still = MakeHull("StillHull", new Vector3(4f, 6f, 0f), false, out _, out _);
            var moving = MakeHull("MovingHull", new Vector3(-3f, 9f, 0f), true,
                out var driver, out var reflector);
            Vector4 expectedMoving = new Vector4(-3f, 9f, 0f, 1f);
            for (int step = 0; step < 4; step++)
            {
                if (step == 1 || step == 3)
                {
                    driver.transform.position += new Vector3(2f, -1f, 0f);
                    driver.transform.rotation = Quaternion.Euler(0f, 0f, -37f * step);
                    driver.SetStormRock(1f, 2f, 3f * step);
                    // Ask the REAL Update to refresh this frame. The intervening frame proves
                    // persistence without a refresh; R1 does not change the reflector's cadence.
                    Set(reflector, "_timer", 0f);
                    Vector3 p = driver.transform.position;
                    expectedMoving = new Vector4(p.x, p.y, 0f, 1f);
                }
                int frame = Time.frameCount + (step == 1 || step == 3 ? 1 : 0);
                do { yield return null; } while (moving.SampleFrame < frame || still.SampleFrame < frame);
                Assert.AreEqual(new Vector4(4f, 6f, 0f, 1f), still.Origin,
                    "Still hull lost its published pivot after real LateUpdate.");
                Assert.AreEqual(expectedMoving, moving.Origin,
                    "Moving hull lost its published pivot after real LateUpdate.");
                Assert.AreEqual(0f, still.Lit);
                Assert.AreEqual(1f, moving.Lit,
                    "Moving hull lost its lit flag after real LateUpdate.");
                Assert.AreEqual(new Vector4(expectedMoving.x, expectedMoving.y, 0f, 0f), moving.HullOrigin,
                    "Production hull LateUpdate must publish the moved root too.");
                if (step > 0)
                {
                    var hull = driver.Visual.GetComponent<IsoFacetHullRenderer>();
                    Assert.AreNotEqual(0f, hull.PitchDegrees, "The real driver composed a non-level pose.");
                    Quaternion expected = IsoFacetMath.HullRotation(hull.HeadingDirUnits, 40f,
                        hull.RollDegrees, hull.PitchDegrees);
                    Assert.Less(Quaternion.Angle(expected, hull.PosedMesh.localRotation), 0.01f,
                        "Production ApplyPose ran after the driver's composed pose (including future trim).");
                }
            }
        }
    }
}
