using System.Collections;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The Dually genuinely takes the MESH path at runtime (ADR 0035).</b>
    ///
    /// <para>⚠️ <b>This has to be a PlayMode test, and that is the whole point.</b> The Art
    /// presentation service registers itself at <c>RuntimeInitializeOnLoadMethod</c> and edit-time
    /// scene builders deliberately do not register it, so in EditMode the service is absent BY
    /// DESIGN and an unskinned vehicle is the correct answer there. A fixture that "proved" the mesh
    /// path in EditMode would be proving nothing.</para>
    ///
    /// <para>The boat fleet has already shipped the failure this guards against: a builder that
    /// skins its own hulls serialises the fallback into the committed scene, where it draws a
    /// perfectly good boat in exactly the right berth and nothing warns
    /// (memory <c>mesh-hulls-must-skin-at-runtime</c>). On the vehicle path the fallback is an empty
    /// GameObject rather than a wrong picture, which is at least loud — but the rule is the same:
    /// a builder places a <see cref="ParkedVehicle"/>, and she skins herself when the scene runs.</para>
    /// </summary>
    public class VehicleSkinPlayTests
    {
        const string VehicleDefPath = "Assets/_Project/Data/Vehicles/Dually3500.asset";

        GameObject _go;

        static VehicleDef LoadDually()
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleDef>(VehicleDefPath);
            Assert.That(def, Is.Not.Null, $"{VehicleDefPath} is missing — re-run the vehicle bake.");
            return def;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.Destroy(_go);
            _go = null;
        }

        /// <summary>
        /// Build her the way a caller naturally would: a live GameObject, add the component,
        /// configure it. ⚠️ <c>AddComponent</c> on an ACTIVE object runs <c>OnEnable</c> immediately —
        /// before <c>Configure</c> has said which vehicle this is — so this order relies on
        /// <see cref="ParkedVehicle.Configure"/> skinning her itself. It is the order every test
        /// below uses, on purpose: it is the one that used to fail silently.
        /// </summary>
        ParkedVehicle SpawnLive(bool drivable)
        {
            _go = new GameObject("Dually", typeof(Rigidbody2D));
            var parked = _go.AddComponent<ParkedVehicle>();
            parked.Configure(LoadDually(), drivable);
            return parked;
        }

        /// <summary>
        /// ⭐ <b>Both authoring orders skin her, and exactly once.</b>
        ///
        /// <para>The INACTIVE-first order is the tidy one and runs through <c>OnEnable</c>. The
        /// live order runs through <c>Configure</c>. Measured 2026-08-17: the live order left her
        /// permanently unskinned with every call apparently succeeding, which took out all five
        /// fixtures in this file at once. Both are pinned here so neither can regress into the
        /// other's blind spot.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator BothAuthoringOrdersSkinHer_AndNeitherDoublesUp()
        {
            // 1. Inactive first — Configure, then activate. OnEnable does the work.
            _go = new GameObject("DuallyInactive", typeof(Rigidbody2D));
            _go.SetActive(false);
            var parked = _go.AddComponent<ParkedVehicle>();
            parked.Configure(LoadDually(), drivable: false);
            Assert.That(parked.IsSkinned, Is.False, "nothing should have skinned while she was inactive.");
            _go.SetActive(true);
            yield return null;
            Assert.That(parked.IsSkinned, Is.True, "the inactive-first order never skinned her.");
            Object.Destroy(_go);
            yield return null;

            // 2. Live — AddComponent has already run OnEnable, so Configure must do the work.
            parked = SpawnLive(drivable: false);
            yield return null;
            Assert.That(parked.IsSkinned, Is.True,
                "the live order never skinned her. AddComponent on an ACTIVE object runs OnEnable " +
                "before Configure names the vehicle, and the SetActive(true) that usually follows is " +
                "a no-op — so Configure has to skin her itself.");

            // Skinning twice must reconfigure in place, not accumulate a second set of wheels.
            parked.Skin();
            yield return null;
            Transform posed = _go.transform.Find(VehicleSkinner.VisualChildName)
                                 .GetComponent<HiddenHarbours.Art.IsoFacetHullRenderer>().PosedMesh;
            int wheels = 0;
            for (int i = 0; i < posed.childCount; i++)
                if (posed.GetChild(i).GetComponent<HiddenHarbours.Art.IsoFacetPropRenderer>() != null) wheels++;
            Assert.That(wheels, Is.EqualTo(6),
                "a re-skin accumulated wheels instead of reconfiguring the six in place.");
        }

        /// <summary>
        /// She skins herself on enable, through the live Art service, and the renderer she gets is
        /// CONFIGURED — not merely installed. An unconfigured renderer draws nothing while every
        /// "is it skinned?" flag reads true.
        /// </summary>
        [UnityTest]
        public IEnumerator SheSkinsHerselfOnEnable_AndTheRendererIsConfigured()
        {
            Assert.That(VehicleMeshPresentation.Service, Is.Not.Null,
                "no vehicle presentation service is registered in PlayMode. Art's " +
                "IsoFacetVehiclePresentationService self-registers at BeforeSceneLoad; if this is " +
                "null the mesh path is dead for every vehicle in the game.");

            ParkedVehicle parked = SpawnLive(drivable: true);
            yield return null;

            Assert.That(parked.IsSkinned, Is.True, "she never took the mesh path.");
            Assert.That(parked.Controller, Is.Not.Null, "a drivable vehicle must get a controller.");

            Transform visual = _go.transform.Find(VehicleSkinner.VisualChildName);
            Assert.That(visual, Is.Not.Null, "no visual child was created.");

            var driver = _go.GetComponent<VehicleMeshDriver>();
            Assert.That(driver, Is.Not.Null, "no VehicleMeshDriver was wired.");
            Assert.That(driver.Visual, Is.Not.Null, "the driver was parked rather than configured.");
        }

        /// <summary>
        /// ⭐ <b>All six wheels are actually attached as posable fittings.</b> The body mesh has no
        /// wheels in it at all — they were lifted out at bake time — so a vehicle whose fittings
        /// failed to attach is a truck sitting on nothing, and every flag above would still read
        /// true.
        /// </summary>
        [UnityTest]
        public IEnumerator EverySixWheelsAttachAsPosableFittings()
        {
            SpawnLive(drivable: true);
            yield return null;

            Transform visual = _go.transform.Find(VehicleSkinner.VisualChildName);
            var renderer = visual.GetComponent<HiddenHarbours.Art.IsoFacetHullRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.PosedMesh, Is.Not.Null,
                "no posed-mesh child, so the wheels have nothing to hang from.");

            foreach (string slot in new[]
                     { "WheelFL", "WheelFR", "WheelRL", "WheelRR", "KnuckleFL", "KnuckleFR" })
            {
                Transform t = renderer.PosedMesh.Find(slot);
                Assert.That(t, Is.Not.Null, $"fitting '{slot}' did not attach.");
                var prop = t.GetComponent<HiddenHarbours.Art.IsoFacetPropRenderer>();
                Assert.That(prop, Is.Not.Null, $"'{slot}' has no prop renderer.");
                Assert.That(prop.IsConfigured, Is.True, $"'{slot}' attached but was never configured.");
            }
        }

        /// <summary>
        /// ⚠️ <b>A road vehicle must NOT churn the wake-foam buffer or reflect in the water.</b>
        /// Installing a HULL attaches both; the vehicle service deliberately does not. A Dually
        /// laying a foam ribbon down Wharf Road and casting a reflection in a harbour she is nowhere
        /// near is exactly the defect a shared install method would have produced, and no existing
        /// test would have thought to ask.
        /// </summary>
        [UnityTest]
        public IEnumerator SheNeitherChurnsFoamNorReflectsInWater()
        {
            SpawnLive(drivable: false);
            yield return null;

            Transform visual = _go.transform.Find(VehicleSkinner.VisualChildName);
            Assert.That(visual.GetComponent<HiddenHarbours.Art.FoamInjector>(), Is.Null,
                "she is churning the wake-foam buffer. A truck is not a boat.");

            var renderer = visual.GetComponent<HiddenHarbours.Art.IsoFacetHullRenderer>();
            MeshRenderer overlay = renderer != null ? renderer.OverlayRenderer : null;
            if (overlay != null)
                Assert.That(overlay.GetComponent<HiddenHarbours.Art.ReflectiveObject>(), Is.Null,
                    "she is reflecting in the water.");
        }

        /// <summary>
        /// <b>Continuous heading, which is the headline of the mesh path.</b> The rig's own
        /// <c>yaw</c> axis moves zero geometry — it is a camera parameter — so a mesh vehicle reads
        /// at any heading between the eight facings for free. Turning her a THIRD of a facing must
        /// move the presented dir units by a third of a unit, not snap.
        ///
        /// <para>The sign is checked too: her measured convention is counter-clockwise, so the
        /// compass→dir mapping NEGATES. A flip here drives her stern-first at east and west.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator HerHeadingIsContinuous_AndMapsThroughTheMeasuredCcwConvention()
        {
            SpawnLive(drivable: false);
            yield return null;

            var driver = _go.GetComponent<VehicleMeshDriver>();

            // Heading 0 (nose north, transform.up = +y) is dir 0 by construction.
            _go.transform.rotation = Quaternion.identity;
            Assert.That(driver.CurrentDirUnits, Is.EqualTo(0f).Within(1e-3f));

            // A quarter turn to compass EAST. Unity 2D: +z rotation is counter-clockwise, so east
            // is −90°. CCW artwork means the mapping negates, so compass 90 ⇒ dir −2.
            _go.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
            Assert.That(driver.CurrentDirUnits, Is.EqualTo(-2f).Within(1e-3f),
                "her compass→dir mapping is not negating. She baked COUNTER-CLOCKWISE (both of the " +
                "rig's analytic oracles agree), so drawing compass heading h means asking the rig " +
                "for dir −h/45. Getting this backwards drives her stern-first at E/W.");

            // ⭐ A THIRD of a facing — the whole point of the mesh path. A facing grid would snap.
            _go.transform.rotation = Quaternion.Euler(0f, 0f, -15f);
            Assert.That(driver.CurrentDirUnits, Is.EqualTo(-1f / 3f).Within(1e-3f),
                "she snapped to a facing. The rig's yaw is a CAMERA parameter, so intermediate " +
                "headings cost nothing and must not be quantised.");
        }

        /// <summary>Driving her turns the machine, not just the wheels — end to end, through the
        /// real FixedUpdate. The coupling the rig's own sidecar says nothing but gameplay will
        /// do.</summary>
        [UnityTest]
        public IEnumerator DrivingWithTheWheelOverActuallyTurnsHer()
        {
            ParkedVehicle parked = SpawnLive(drivable: true);
            yield return null;

            VehicleController c = parked.Controller;
            float startHeading = BoatKinematics.BearingDegrees(_go.transform.up);

            // Wheel hard over at a standstill: she must NOT turn.
            c.SteerDemand = 1f;
            for (int i = 0; i < 40; i++) c.StepPhysics(0.02f);
            Assert.That(BoatKinematics.BearingDegrees(_go.transform.up),
                Is.EqualTo(startHeading).Within(0.01f),
                "she pivoted on the spot with the wheel over. A stopped truck does not turn.");

            // Now drive: she must come round, to the LEFT (+steer), and her wheels must be posed.
            c.Throttle = 1f;
            yield return null;
            for (int i = 0; i < 120; i++) yield return new WaitForFixedUpdate();

            Assert.That(c.SpeedMetersPerSecond, Is.GreaterThan(0.5f), "she never moved off.");
            Assert.That(c.OdometerMeters, Is.GreaterThan(0f));

            float turned = Mathf.DeltaAngle(startHeading, BoatKinematics.BearingDegrees(_go.transform.up));
            Assert.That(turned, Is.LessThan(-1f),
                "she drove with full LEFT lock and did not come round to port. +steer is a left " +
                "turn; a compass bearing DECREASES going left.");
        }
    }
}
