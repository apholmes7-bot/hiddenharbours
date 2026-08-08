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
            public float RidePixels { get; set; }
            public bool IsConfigured => true;
            public int SortingLayerId; public int SortingOrder;
            public void SetSorting(int layerId, int order) { SortingLayerId = layerId; SortingOrder = order; }
            // The deck-occupant split: recorded, so a test can pin what the presenter forwarded.
            public Vector3 OccupantRigMeters { get; private set; }
            public bool OccupantActive { get; private set; }
            public float DeckOccluderIdValue { get; set; }
            public void SetDeckOccupant(Vector3 rigLocalMeters, bool active)
            { OccupantRigMeters = rigLocalMeters; OccupantActive = active; }
            public float DeckOccluderId => DeckOccluderIdValue;
        }

        /// <summary>A fitting as the seam sees it: a local rotation and a lateral mount, nothing more
        /// (ADR 0022 phase 7). Records what was asked of it so a test can assert the POSE rather
        /// than the pixels.</summary>
        private sealed class FakePropRenderer : IHullPropRenderer
        {
            public Quaternion LocalRotation { get; set; } = Quaternion.identity;
            public float LateralOffsetMeters { get; set; }
            public Vector3 FitmentOffsetMeters { get; set; }
            public bool Visible { get; set; } = true;
            public bool IsConfigured => true;
        }

        private sealed class FakeService : IHullMeshPresentationService
        {
            public readonly FakeRenderer Renderer = new FakeRenderer();
            public GameObject InstalledOn; public int Installs; public int Removes;
            public bool RefuseInstall;

            public readonly System.Collections.Generic.Dictionary<string, FakePropRenderer> Props =
                new System.Collections.Generic.Dictionary<string, FakePropRenderer>();
            public int PropAttaches, PropDetaches;
            public bool RefuseProps;

            public IHullMeshRenderer Install(GameObject host, HullMeshDef def)
            {
                if (RefuseInstall) return null;
                Installs++; InstalledOn = host; return Renderer;
            }

            public IHullPropRenderer AttachProp(GameObject host, HullPropMeshDef def, string slot)
            {
                if (RefuseProps) return null;
                PropAttaches++;
                if (!Props.TryGetValue(slot, out var p)) Props[slot] = p = new FakePropRenderer();
                return p;
            }

            public void DetachProps(GameObject host) { PropDetaches++; Props.Clear(); }

            public void DetachProp(GameObject host, string slot)
            {
                if (Props.Remove(slot)) PropDetaches++;
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
                p.SetDeckOccupant(new Vector3(0.3f, -1.2f, 0.9f), true);
                Assert.AreEqual(0f, p.DeckOccluderId, 1e-6f,
                    "a hull that is gone hides nobody — the figure must not keep discarding against " +
                    "an id that is now free for another boat to be issued");
            });
        }

        // ---- the deck occupant: who stands on her, and what hides them -------------------------

        [Test]
        public void Presenter_HandsTheDeckOccupantStraightToTheDrawer_InRigMetres()
        {
            // Only the facet pass holds a hull's DEPTH, so "is the wheelhouse in front of the
            // fisher?" can only be answered there. The presenter's whole job is to carry the
            // question across without reinterpreting it: the point stays in the hull's own rig
            // frame, which is the frame the deck polygons and every fitting pivot already speak.
            var root = MakeRoot();
            var fake = new FakeRenderer();
            var driver = root.AddComponent<MeshHullDriver>();
            driver.Configure(root.transform, fake, MakeUsableDef(), 0f);
            var p = new MeshHullPresenter(driver);

            var stand = new Vector3(0.42f, -1.35f, 0.85f);
            p.SetDeckOccupant(stand, true);

            Assert.AreEqual(stand, fake.OccupantRigMeters, "the rig point travels unchanged");
            Assert.IsTrue(fake.OccupantActive);

            fake.DeckOccluderIdValue = 7f / 255f;
            Assert.AreEqual(7f / 255f, p.DeckOccluderId, 1e-6f,
                "and the id that hides the figure comes back the same way");

            p.SetDeckOccupant(Vector3.zero, false);
            Assert.IsFalse(fake.OccupantActive, "stepping ashore stops the hull splitting her image");
        }

        [Test]
        public void SpriteHull_IgnoresTheDeckOccupant_BecauseAFlatSheetHasNoDepth()
        {
            // Deliberately inert, in the same family as SetRockPhaseDegrees and SetStormRock: a
            // sprite hull's image is one baked sheet with no depth in it, so there is no honest
            // answer to give. The pilotable fleet is all mesh (ADR 0022) — this is the greybox and
            // the ambient fleet, and they must neither crash nor half-answer.
            var root = MakeRoot();
            var visualChild = new GameObject("Visual");
            visualChild.transform.SetParent(root.transform, false);
            _spawned.Add(visualChild);
            var sr = visualChild.AddComponent<SpriteRenderer>();
            var directional = root.AddComponent<DirectionalBoatSprite>();
            directional.Configure(new Sprite[8], sr);
            var p = new SpriteHullPresenter(directional);

            Assert.DoesNotThrow(() => p.SetDeckOccupant(new Vector3(0.3f, -1f, 0.8f), true));
            Assert.AreEqual(0f, p.DeckOccluderId, 0f,
                "0 means 'nothing hides you here' — the figure's shader stays inert and she draws " +
                "exactly as she always has on a sprite hull");
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

        // ---- the fittings (ADR 0022 phase 7) ---------------------------------------------------

        private HullPropMeshDef MakeUsableProp(float[] mounts = null)
        {
            var def = ScriptableObject.CreateInstance<HullPropMeshDef>();
            _spawned.Add(def);
            // ⚠️ A real vertex, because HullPropMeshDef.IsUsable() insists on one — a fitting that
            // cannot be drawn must be REFUSED rather than attached as an invisible engine, and a
            // fixture with an empty mesh would be testing the refusal path by accident.
            var mesh = new Mesh(); _spawned.Add(mesh);
            mesh.SetVertices(new[] { Vector3.zero, Vector3.right, Vector3.up });
            mesh.SetNormals(new[] { Vector3.forward, Vector3.forward, Vector3.forward });
            mesh.SetUVs(0, new List<Vector4> { Vector4.zero, Vector4.zero, Vector4.zero });
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            def.Id = "hullprop.test_motor";
            def.Mesh = mesh;
            def.Ramps = new[] { new HullMeshDef.Ramp { Colors = new[] { new Color32(9, 9, 9, 255) }, Offset = 0 } };
            def.Bayer16 = new float[16];
            def.PxPerMetre = 32;
            def.CellW = 272; def.CellH = 216;
            def.ElevationDeg = 40f;
            def.PivotLocalMeters = new Vector3(0f, -3.57f, 0.72f);
            def.MaxSteerDegrees = 30f;
            def.MaxTiltDegrees = 40f;
            def.LateralMountsMeters = mounts ?? System.Array.Empty<float>();
            return def;
        }

        /// <summary>
        /// <b>The flip that finished phase 7.</b> A mesh hull carrying an outboard FITTING draws it,
        /// where a mesh hull carrying only motor SHEETS had them dropped — which is the regression that
        /// held the punt and both skiffs on the sprite compass through the whole of phase 6.
        /// </summary>
        [Test]
        public void Skinner_MeshHullWithAMotorFitting_BoltsItOn()
        {
            var service = new FakeService();
            HullMeshPresentation.Service = service;

            var visual = MakeMeshVisual(MakeUsableDef());
            visual.MotorMesh = MakeUsableProp(new[] { -0.34f, 0.34f });
            visual.MotorFit = OutboardMotorLayer.MotorFit.Single;
            visual.MotorMaxSteerDegrees = 30f;

            var root = MakeRoot();
            BoatHullSkinner.Apply(root, visual, boat: null);

            var layer = root.GetComponent<OutboardMotorMeshLayer>();
            Assert.IsNotNull(layer, "a mesh hull whose visual carries a motor FITTING must wear it.");
            Assert.IsTrue(layer.IsWired);
            Assert.AreEqual(1, layer.EngineCount,
                "a Single fit takes ONE engine, even though the fitting knows a twin spacing.");
            Assert.AreEqual(0f, layer.MountMeters(0), 1e-4f, "…on the centreline.");
        }

        /// <summary>The twin is the SAME fitting instantiated twice — no second asset, no second
        /// bake, and no ordering rule between them (a shared depth buffer settles it).</summary>
        [Test]
        public void Skinner_TwinFit_InstantiatesOneFittingAtBothClampPositions()
        {
            var service = new FakeService();
            HullMeshPresentation.Service = service;

            var visual = MakeMeshVisual(MakeUsableDef());
            visual.MotorMesh = MakeUsableProp(new[] { -0.34f, 0.34f });
            visual.MotorFit = OutboardMotorLayer.MotorFit.Twin;

            var root = MakeRoot();
            BoatHullSkinner.Apply(root, visual, boat: null);

            var layer = root.GetComponent<OutboardMotorMeshLayer>();
            Assert.IsNotNull(layer);
            Assert.AreEqual(2, layer.EngineCount);
            Assert.AreEqual(-0.34f, layer.MountMeters(0), 1e-4f);
            Assert.AreEqual(+0.34f, layer.MountMeters(1), 1e-4f);
            Assert.AreEqual(2, service.Props.Count,
                "two INSTANCES of one fitting, in two named slots — not one engine drawn twice.");
        }

        /// <summary>
        /// ⚠️ A hull's fittings are decided independently, so "this hull has no outboard" must not
        /// unbolt her OARS on the way past. That is why the seam grew a per-slot detach.
        /// </summary>
        [Test]
        public void Skinner_AHullWithOarsButNoMotor_KeepsHerOars()
        {
            var service = new FakeService();
            HullMeshPresentation.Service = service;

            var visual = MakeMeshVisual(MakeUsableDef());
            visual.OarPortMesh = MakeUsableProp();
            visual.OarStarMesh = MakeUsableProp();
            visual.MotorMesh = null;

            var root = MakeRoot();
            var boat = root.AddComponent<BoatController>();
            BoatHullSkinner.Apply(root, visual, boat);

            Assert.IsNotNull(root.GetComponent<DoryOarMeshLayer>(), "her oars are wired");
            Assert.IsTrue(root.GetComponent<DoryOarMeshLayer>().IsWired,
                "…and still attached after the motor branch decided she has no engine");
            Assert.IsNull(root.GetComponent<OutboardMotorMeshLayer>());
        }

        /// <summary>
        /// <b>The dory's engine is a PURCHASE, and the skin has to know it.</b> Both her hulls wear
        /// the same visual (D8's variant-asset answer for M1), so the fitting is on the visual and the
        /// permission is on the hull: <c>boat.dory</c> rows, <c>boat.dory_outboard</c> motors. Without
        /// this gate the rowed dory tows an outboard she has not bought, and buying it changes nothing
        /// you can see — which would be the whole §7.7 rung, silently missing.
        /// </summary>
        [Test]
        public void Skinner_ARowedHull_DoesNotWearTheEngineHerVisualCarries()
        {
            var service = new FakeService();
            HullMeshPresentation.Service = service;

            var visual = MakeMeshVisual(MakeUsableDef());
            visual.MotorMesh = MakeUsableProp();
            visual.OarPortMesh = MakeUsableProp();
            visual.OarStarMesh = MakeUsableProp();

            var rowed = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(rowed);
            rowed.Id = "boat.test_dory";
            rowed.Visual = visual;
            rowed.Propulsion = PropulsionType.Oars;

            var root = MakeRoot();
            var boat = root.AddComponent<BoatController>();
            BoatHullSkinner.ApplyHull(root, baseRenderer: null, hull: rowed, boat: boat);

            Assert.IsNull(root.GetComponent<OutboardMotorMeshLayer>(),
                "a hull whose Propulsion is Oars must not wear the engine her visual can draw — the " +
                "outboard is the upgrade, and this is the only thing that makes buying it mean anything.");
            Assert.IsTrue(root.GetComponent<DoryOarMeshLayer>().IsWired,
                "…and she still rows, obviously.");
        }

        /// <summary>The other half of the same gate: flip Propulsion and the engine appears — same
        /// visual, same fitting, same boat. That IS the D8 upgrade.</summary>
        [Test]
        public void Skinner_TheSameVisualUnderAnEngineHull_WearsTheOutboard_AndShipsTheOars()
        {
            var service = new FakeService();
            HullMeshPresentation.Service = service;

            var visual = MakeMeshVisual(MakeUsableDef());
            visual.MotorMesh = MakeUsableProp();
            visual.OarPortMesh = MakeUsableProp();
            visual.OarStarMesh = MakeUsableProp();
            visual.MotorMeshFitmentOffsetMeters = new Vector3(0f, 0.28f, 0.055f);

            var powered = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(powered);
            powered.Id = "boat.test_dory_outboard";
            powered.Visual = visual;
            powered.Propulsion = PropulsionType.Engine;

            var root = MakeRoot();
            var boat = root.AddComponent<BoatController>();
            BoatHullSkinner.ApplyHull(root, baseRenderer: null, hull: powered, boat: boat);

            var motor = root.GetComponent<OutboardMotorMeshLayer>();
            Assert.IsNotNull(motor, "the hull that bought the engine wears it.");
            Assert.IsTrue(motor.IsWired);

            var oars = root.GetComponent<DoryOarMeshLayer>();
            Assert.IsNotNull(oars, "her oars stay in the boat — a dory always carries them.");
            Assert.IsTrue(oars.MotorIsRunning,
                "…but they are SHIPPED while the engine runs: the oar layer must have been handed the " +
                "motor layer, which is why the skinner wires the engine BEFORE the oars.");

            // The borrowed-fitting shift reaches the renderer, once, at install.
            Assert.AreEqual(new Vector3(0f, 0.28f, 0.055f),
                            service.Props[BoatHullSkinner.MotorMeshSlotNames[0]].FitmentOffsetMeters,
                "a borrowed engine is hung by the visual's fitment offset — without it the dory wears " +
                "the punt's outboard where the PUNT's rig put it, over a metre astern of her transom.");
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
