using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The mesh hull presenter + the skinner's mesh branch (ADR 0022 phase 4).</b> What phase 1's
    /// seam tests did for the sprite adapter, these do for the second implementation: the contract
    /// answers, the continuous-rock channel, the driver's heading mapping — and the skinner-level
    /// behaviour that actually protects the fleet: mesh only when the data AND the service both say
    /// so, sprite fallback everywhere else, and a clean swap in BOTH directions (the A/B toggle's
    /// whole mechanism). Headless: the renderer behind the seam is a test double, so nothing here
    /// needs a GPU — the real facet pipeline's pixels are IsoFacetUrpPassTests' business.
    /// </summary>
    public class MeshHullPresenterTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private IHullMeshPresentationService _previousService;

        [SetUp]
        public void SetUp() => _previousService = HullMeshPresentation.Service;

        [TearDown]
        public void TearDown()
        {
            HullMeshPresentation.Service = _previousService;
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- doubles --------------------------------------------------------------------------

        private sealed class FakeRenderer : IHullMeshRenderer
        {
            public float HeadingDirUnits { get; set; }
            public float RollDegrees { get; set; }
            public float PitchDegrees { get; set; }
            public float HeavePixels { get; set; }
            public bool IsConfigured => true;
            public int SortingLayerId; public int SortingOrder;
            public void SetSorting(int layerId, int order) { SortingLayerId = layerId; SortingOrder = order; }
        }

        private sealed class FakeService : IHullMeshPresentationService
        {
            public readonly FakeRenderer Renderer = new FakeRenderer();
            public GameObject InstalledOn; public int Installs; public int Removes;
            public bool RefuseInstall;

            public IHullMeshRenderer Install(GameObject host, HullMeshDef def)
            {
                if (RefuseInstall) return null;
                Installs++; InstalledOn = host; return Renderer;
            }

            public void Remove(GameObject host) { Removes++; }
        }

        private HullMeshDef MakeUsableDef(bool ccw = true)
        {
            var def = ScriptableObject.CreateInstance<HullMeshDef>();
            _spawned.Add(def);
            var mesh = new Mesh();
            _spawned.Add(mesh);
            def.Id = "hullmesh.test";
            def.Mesh = mesh;
            def.Ramps = new[] { new HullMeshDef.Ramp { Colors = new[] { new Color32(1, 2, 3, 255) }, Offset = 0 } };
            def.Bayer16 = new float[16];
            def.PxPerMetre = 32;
            def.CellW = 456; def.CellH = 420;
            def.ElevationDeg = 40f;
            def.AzimuthCounterClockwise = ccw;
            def.RockRollDegrees = 2.8f; def.RockPitchDegrees = 1.6f; def.RockHeavePixels = 1.2f;
            return def;
        }

        private BoatVisualDef MakeMeshVisual(HullMeshDef hullMesh, bool withCompass = true)
        {
            var v = ScriptableObject.CreateInstance<BoatVisualDef>();
            _spawned.Add(v);
            v.Id = "visual.mesh_test";
            v.Variant = BoatHullVariant.Mesh;
            v.HullMesh = hullMesh;
            if (withCompass)
            {
                var facings = new Sprite[8];
                for (int i = 0; i < facings.Length; i++)
                {
                    var tex = new Texture2D(4, 4); _spawned.Add(tex);
                    var spr = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f)); _spawned.Add(spr);
                    facings[i] = spr;
                }
                v.Facings = facings;
                v.FacingsAreCounterClockwise = false;   // the lobster's sheet fact: true clockwise
                v.ArtBakeElevationDegrees = 40f;
            }
            return v;
        }

        private GameObject MakeRoot()
        {
            var root = new GameObject("Boat");
            _spawned.Add(root);
            return root;
        }

        // ---- the presenter contract -----------------------------------------------------------

        [Test]
        public void Presenter_AnswersTheSpriteShapedQuestions_TheMeshWay()
        {
            var root = MakeRoot();
            var driver = root.AddComponent<MeshHullDriver>();
            driver.Configure(root.transform, new FakeRenderer(), MakeUsableDef(), 0f);
            var p = new MeshHullPresenter(driver);

            Assert.AreEqual(BoatHullVariant.Mesh, p.Variant);
            Assert.AreEqual(0, p.FacingCount, "0 facings = the documented 'unquantised' signal");
            Assert.AreEqual(0, p.FacingCellIndex);
            Assert.IsFalse(p.FacingsAreCounterClockwise,
                "the SHEET mirror flag is meaningless for a mesh — the live rig's convention is the " +
                "driver's business, not surfaced as this sheet fact");
            Assert.AreEqual(40f, p.BakeElevationDegrees, 1e-4f,
                "the mesh bakes the same elevation into its object transform — anchors read one number");
            Assert.IsTrue(p.HasRockGrid, "rock is a transform, free — always available");
            Assert.IsTrue(p.SupportsContinuousRock);
            Assert.IsNotNull(p.Anchors, "anchors are never null");
        }

        [Test]
        public void Presenter_DrawnHeading_IsContinuous_AndTracksTheRoot()
        {
            var root = MakeRoot();
            var driver = root.AddComponent<MeshHullDriver>();
            driver.Configure(root.transform, new FakeRenderer(), MakeUsableDef(), 0f);
            var p = new MeshHullPresenter(driver);

            // Including headings a 32-facing sheet cannot draw: no snap, ever.
            foreach (float deg in new[] { 0f, 3.7f, 45f, 101.3f, 222.2f, 359.9f })
            {
                root.transform.rotation = Quaternion.Euler(0f, 0f, -deg);   // z-CCW, bow = up
                Assert.AreEqual(deg, p.DrawnHeadingDegrees(), 1e-3f,
                    $"the drawn heading of a mesh hull IS the true heading (at {deg}°)");
            }
        }

        [Test]
        public void Presenter_SurvivesADestroyedDriver_ReportingUnskinnedDefaults()
        {
            var root = MakeRoot();
            var driver = root.AddComponent<MeshHullDriver>();
            var p = new MeshHullPresenter(driver);
            Object.DestroyImmediate(driver);

            Assert.DoesNotThrow(() =>
            {
                Assert.AreEqual(0f, p.DrawnHeadingDegrees(), 1e-4f);
                Assert.AreEqual(90f, p.BakeElevationDegrees, 1e-4f, "90 = plan view, the unskinned default");
                Assert.AreEqual(MountedRockPoseMath.LevelRockFrame, p.RockFrame);
                p.RockFrame = 3;
                p.SetRockPhaseDegrees(90f);
                p.VisualTiltDegrees = 1f;
            });
        }

        // ---- the driver: heading mapping + rock channel ---------------------------------------

        [Test]
        public void Driver_MapsHeadingThroughTheMeasuredConvention_EveryLateUpdate()
        {
            var root = MakeRoot();
            var fake = new FakeRenderer();
            var visualChild = new GameObject("Visual").transform;
            visualChild.SetParent(root.transform, false);
            var driver = root.AddComponent<MeshHullDriver>();
            driver.Configure(visualChild, fake, MakeUsableDef(ccw: true), 0f);

            root.transform.rotation = Quaternion.Euler(0f, 0f, -90f);   // heading East
            driver.Drive();

            Assert.AreEqual(-2f, fake.HeadingDirUnits, 1e-3f,
                "East through a measured-CCW rig is dir −2 — the sign IS the mirror saga");
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, visualChild.rotation), 1e-3f,
                "the visual child is stomped to screen identity — the mesh's own rotation is the only turn");
        }

        [Test]
        public void Driver_PosesContinuousRock_FromThePhase_AndLevelsOnMinusOne()
        {
            var root = MakeRoot();
            var fake = new FakeRenderer();
            var visualChild = new GameObject("Visual").transform;
            visualChild.SetParent(root.transform, false);
            var driver = root.AddComponent<MeshHullDriver>();
            driver.Configure(visualChild, fake, MakeUsableDef(), 0f);

            driver.SetRockPhaseDegrees(90f);   // the crest
            driver.Drive();
            Assert.AreEqual(2.8f, fake.RollDegrees, 1e-3f, "crest: full roll (the rig's rollA)");
            Assert.AreEqual(0f, fake.PitchDegrees, 1e-3f, "crest: pitch through zero");
            Assert.AreEqual(1.2f, fake.HeavePixels, 1e-3f, "crest: full heave (rig pixels)");

            driver.RockFrame = -1;             // calm — the same level signal the sprite path uses
            driver.Drive();
            Assert.AreEqual(0f, fake.RollDegrees, 1e-4f);
            Assert.AreEqual(0f, fake.PitchDegrees, 1e-4f);
            Assert.AreEqual(0f, fake.HeavePixels, 1e-4f);
        }

        // ---- the skinner: variant selection + both swap directions ----------------------------

        [Test]
        public void Skinner_MeshVariant_BuildsTheMeshRig_WhenServiceRegistered()
        {
            var service = new FakeService();
            HullMeshPresentation.Service = service;

            var root = MakeRoot();
            var rig = BoatHullSkinner.Apply(root, MakeMeshVisual(MakeUsableDef()), boat: null);

            Assert.IsTrue(rig.Skinned);
            Assert.IsNotNull(rig.Presenter);
            Assert.AreEqual(BoatHullVariant.Mesh, rig.Presenter.Variant);
            Assert.AreEqual(1, service.Installs, "the renderer was installed through the Core seam");
            Assert.IsNull(rig.Directional, "no compass component on the mesh path");
            Assert.IsNull(rig.Renderer, "no SpriteRenderer on the mesh path");
            Assert.IsNotNull(root.GetComponent<MeshHullDriver>(), "the driver rides the physics root");
            Assert.IsNotNull(rig.Visual, "the shared visual child exists");
            Assert.AreEqual(BoatHullSkinner.VisualChildName, rig.Visual.name,
                "the load-bearing child name survives the mesh path (BoatSpotlight finds it BY NAME)");
            Assert.IsNull(rig.Visual.GetComponent<SpriteRenderer>(),
                "the sprite picture is gone — two hulls must not draw at once");
            var host = root.GetComponent<BoatHullPresenterHost>();
            Assert.IsNotNull(host, "the presenter is published for GameObject-bound consumers");
            Assert.AreSame(rig.Presenter, host.Presenter);
        }

        [Test]
        public void Skinner_MeshVariant_FallsBackToSprite_WithNoService()
        {
            HullMeshPresentation.Service = null;

            var root = MakeRoot();
            var rig = BoatHullSkinner.Apply(root, MakeMeshVisual(MakeUsableDef()), boat: null);

            Assert.IsTrue(rig.Skinned, "the hull is still skinned — by the sprite compass");
            Assert.AreEqual(BoatHullVariant.Sprite, rig.Presenter.Variant,
                "no service (edit-time builders, headless contexts) = the sprite path stands");
            Assert.IsNotNull(rig.Directional);
            Assert.IsNull(root.GetComponent<MeshHullDriver>());
        }

        [Test]
        public void Skinner_MeshVariant_FallsBackToSprite_WhenTheDefIsUnusable()
        {
            var service = new FakeService();
            HullMeshPresentation.Service = service;

            var brokenDef = ScriptableObject.CreateInstance<HullMeshDef>();   // no mesh, no ramps
            _spawned.Add(brokenDef);
            var root = MakeRoot();

            var rig = BoatHullSkinner.Apply(root, MakeMeshVisual(brokenDef), boat: null);

            Assert.IsTrue(rig.Skinned);
            Assert.AreEqual(BoatHullVariant.Sprite, rig.Presenter.Variant,
                "an unusable def must degrade to the shipped look, never to an invisible boat");
            Assert.AreEqual(0, service.Installs, "the service was never asked to install garbage");
        }

        [Test]
        public void Skinner_VariantOverride_FlipsBothWays_InPlace()
        {
            // THE A/B MECHANISM: same root, same visual, opposite override — and back. What V at the
            // helm does, minus the keyboard.
            var service = new FakeService();
            HullMeshPresentation.Service = service;
            var visual = MakeMeshVisual(MakeUsableDef());
            var root = MakeRoot();

            var mesh1 = BoatHullSkinner.Apply(root, visual, null);
            Assert.AreEqual(BoatHullVariant.Mesh, mesh1.Presenter.Variant, "asset says Mesh");

            var sprite = BoatHullSkinner.Apply(root, visual, null,
                new BoatHullSkinner.Options { VariantOverride = BoatHullVariant.Sprite });
            Assert.AreEqual(BoatHullVariant.Sprite, sprite.Presenter.Variant, "forced to Sprite");
            Assert.IsNotNull(sprite.Renderer, "the sprite picture is back");
            Assert.IsNull(root.GetComponent<MeshHullDriver>(), "the mesh driver is gone");
            Assert.Greater(service.Removes, 0, "the mesh renderer was removed through the seam");

            var mesh2 = BoatHullSkinner.Apply(root, visual, null,
                new BoatHullSkinner.Options { VariantOverride = BoatHullVariant.Mesh });
            Assert.AreEqual(BoatHullVariant.Mesh, mesh2.Presenter.Variant, "and forced back to Mesh");
            Assert.IsNull(mesh2.Visual.GetComponent<SpriteRenderer>(), "the sprite picture is gone again");
            Assert.IsNotNull(root.GetComponent<MeshHullDriver>());

            var host = root.GetComponent<BoatHullPresenterHost>();
            Assert.AreSame(mesh2.Presenter, host.Presenter, "the host always publishes the CURRENT presenter");
        }

        [Test]
        public void Skinner_SpriteHull_NeverTouchesTheMeshService()
        {
            var service = new FakeService();
            HullMeshPresentation.Service = service;

            var visual = MakeMeshVisual(MakeUsableDef());
            visual.Variant = BoatHullVariant.Sprite;   // a normal hull

            var rig = BoatHullSkinner.Apply(MakeRoot(), visual, null);
            Assert.AreEqual(BoatHullVariant.Sprite, rig.Presenter.Variant);
            Assert.AreEqual(0, service.Installs,
                "every other hull stays a sprite and pays nothing for the mesh path existing");
        }

        // ---- the wave-motion channel ----------------------------------------------------------

        [Test]
        public void WaveMotion_ContinuousRock_ReachesTheDriver_Unquantised()
        {
            // Not a full wave-field integration (PlayMode's business) — the seam question only:
            // a presenter that supports continuous rock receives the phase, not a frame.
            var root = MakeRoot();
            var fake = new FakeRenderer();
            var visualChild = new GameObject("Visual").transform;
            visualChild.SetParent(root.transform, false);
            var driver = root.AddComponent<MeshHullDriver>();
            driver.Configure(visualChild, fake, MakeUsableDef(), 0f);
            var p = new MeshHullPresenter(driver);

            p.SetRockPhaseDegrees(37.5f);      // no 8-frame grid can represent this
            driver.Drive();
            float expectedRoll = 2.8f * Mathf.Sin(37.5f * Mathf.Deg2Rad);
            Assert.AreEqual(expectedRoll, fake.RollDegrees, 1e-3f,
                "the phase reached the renderer continuously — no frame rounding on the mesh path");
        }
    }
}
