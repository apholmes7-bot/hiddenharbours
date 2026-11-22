using System.Collections.Generic;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// ADR 0060 PR 3: the dwelling presentation service. A parked home is installed on the shared facet
    /// renderer in a still pose, with the sun and lamp casters fitted once, and none of a boat's water
    /// components. An empty home holds one facet id; the deck-occupant block is taken only while
    /// somebody is inside. Run against the committed Clipper bake, so the real asset is what is proved.
    /// </summary>
    public class DwellingPresentationServiceTests
    {
        const string Clipper = "Assets/_Project/Data/Dwellings/Meshes/CamperClipperDwellingMesh.asset";
        const string DoorPart = "Assets/_Project/Data/Boats/HullProps/DoryOarPortPropMesh.asset";

        readonly List<Object> _owned = new();
        IDwellingMeshPresentationService _savedService;

        [SetUp] public void SetUp() => _savedService = DwellingMeshPresentation.Service;

        [TearDown] public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            DwellingMeshPresentation.Service = _savedService;
        }

        static DwellingMeshDef LoadClipper()
        {
            var def = AssetDatabase.LoadAssetAtPath<DwellingMeshDef>(Clipper);
            Assert.That(def, Is.Not.Null, Clipper);
            Assert.That(def.FirstProblem(), Is.Null, "the committed Clipper bake must be usable");
            return def;
        }

        GameObject Host(string name = "DwellingFixture")
        {
            var host = new GameObject(name); _owned.Add(host);
            host.transform.position = new Vector3(14f, -6f, 0f);
            return host;
        }

        IsoFacetHullRenderer Install(GameObject host, DwellingMeshDef def)
        {
            var renderer = (IsoFacetHullRenderer)new IsoFacetDwellingPresentationService().Install(host, def);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.IsConfigured, Is.True);
            return renderer;
        }

        [Test] public void Install_ConfiguresTheSharedRenderer_FromTheClipperBake()
        {
            var def = LoadClipper();
            var host = Host();
            var renderer = Install(host, def);
            Assert.That(host.GetComponent<IsoFacetHullRenderer>(), Is.SameAs(renderer));
            Assert.That(renderer.CarriesLevelTags, Is.True, "the room's faces carry their level tags");
            Assert.That(renderer.HullId, Is.GreaterThan(0), "an enabled dwelling holds a facet id");
        }

        [Test] public void Install_HoldsTheStillPose_EvenOnAHostLeftLeaning()
        {
            var def = LoadClipper();
            var host = Host();
            var renderer = host.AddComponent<IsoFacetHullRenderer>();
            IHullMeshRenderer pose = renderer;
            pose.RollDegrees = 7f; pose.PitchDegrees = -3f; pose.HeavePixels = 2f; pose.RidePixels = 4f;
            pose.HeadingDirUnits = 2.5f;

            Install(host, def);
            Assert.That(pose.RollDegrees, Is.Zero);
            Assert.That(pose.PitchDegrees, Is.Zero);
            Assert.That(pose.HeavePixels, Is.Zero);
            Assert.That(pose.RidePixels, Is.Zero);
            Assert.That(pose.HeadingDirUnits, Is.EqualTo(2.5f), "the heading is the caller's, from the site");
        }

        [Test] public void Install_FitsSunAndLampCasters_ExactlyOnce_AcrossReinstalls()
        {
            var def = LoadClipper();
            var host = Host();
            var renderer = Install(host, def);
            var sun = host.GetComponent<HullSunShadowCaster>();
            var lamp = host.GetComponent<HullLampShadowCaster>();
            Assert.That(sun, Is.Not.Null); Assert.That(lamp, Is.Not.Null);

            Assert.That(Install(host, def), Is.SameAs(renderer), "a re-install reconfigures in place");
            Assert.That(host.GetComponents<IsoFacetHullRenderer>(), Has.Length.EqualTo(1));
            Assert.That(host.GetComponents<HullSunShadowCaster>(), Has.Length.EqualTo(1));
            Assert.That(host.GetComponents<HullLampShadowCaster>(), Has.Length.EqualTo(1));
            Assert.That(host.GetComponent<HullSunShadowCaster>(), Is.SameAs(sun));
            Assert.That(host.GetComponent<HullLampShadowCaster>(), Is.SameAs(lamp));
        }

        [Test] public void Install_AddsNoWaterDeckOrLampComponents()
        {
            var host = Host();
            Install(host, LoadClipper());
            Assert.That(host.GetComponentsInChildren<FoamInjector>(true), Is.Empty, "no wake on gravel");
            Assert.That(host.GetComponentsInChildren<ReflectiveObject>(true), Is.Empty, "no reflection ashore");
            Assert.That(host.GetComponentsInChildren<HullDeckShadowCaster>(true), Is.Empty);
            Assert.That(host.GetComponentsInChildren<BoatLamps>(true), Is.Empty);
            Assert.That(host.GetComponentsInChildren<BoatWindowGlow>(true), Is.Empty);
            Assert.That(host.GetComponentsInChildren<BoatSpotlight>(true), Is.Empty);
        }

        [Test] public void ToSetup_CopiesBothPalettes_AndLeavesTheClampOff()
        {
            var def = LoadClipper();
            var setup = IsoFacetDwellingPresentationService.ToSetup(def);
            Assert.That(setup.Mesh, Is.SameAs(def.Mesh));
            Assert.That(setup.Ramps, Has.Length.EqualTo(def.Ramps.Length));
            Assert.That(setup.InteriorRamps, Has.Length.EqualTo(def.InteriorRamps.Length));
            Assert.That(setup.InteriorRamps.Length, Is.GreaterThan(0), "the Clipper bake carries its room");
            for (int m = 0; m < def.Ramps.Length; m++)
            {
                Assert.That(setup.Ramps[m], Is.SameAs(def.Ramps[m].Colors));
                Assert.That(setup.RampOffsets[m], Is.EqualTo(def.Ramps[m].Offset));
                Assert.That(setup.RampDitherWeights[m], Is.EqualTo(def.Ramps[m].DitherWeight));
            }
            for (int m = 0; m < def.InteriorRamps.Length; m++)
            {
                Assert.That(setup.InteriorRamps[m], Is.SameAs(def.InteriorRamps[m].Colors));
                Assert.That(setup.InteriorRampOffsets[m], Is.EqualTo(def.InteriorRamps[m].Offset));
                Assert.That(setup.InteriorRampDitherWeights[m], Is.EqualTo(def.InteriorRamps[m].DitherWeight));
            }
            Assert.That(setup.WatertightDeckHeightMeters, Is.Zero);
            Assert.That(setup.WatertightHalfBeamMeters, Is.Zero);
            Assert.That(setup.ReceivesWaterCaustics, Is.False);
            Assert.That(setup.Lamps, Is.Null.Or.Empty);
            Assert.That(setup.Panes, Is.Null.Or.Empty);
            Assert.That(setup.DoorLeafClosed, Is.Null);
            Assert.That(setup.DoorLeafOpen, Is.Null);
            Assert.That(setup.PivotPx, Is.EqualTo(def.PivotPx));
            Assert.That(setup.PxPerMetre, Is.EqualTo(def.PxPerMetre));
            Assert.That(setup.CellW, Is.EqualTo(def.CellW));
            Assert.That(setup.CellH, Is.EqualTo(def.CellH));
            Assert.That(setup.ElevationDeg, Is.EqualTo(def.ElevationDeg));
        }

        [Test] public void AnEmptyDwelling_HoldsOneId_AndTakesItsBlockOnlyWhileOccupied()
        {
            var renderer = Install(Host(), LoadClipper());
            Assert.That(renderer.HullId, Is.GreaterThan(0));
            Assert.That(renderer.ForeHullId, Is.Zero, "nobody inside: no deck-occupant block");

            var occupant = new object();
            int slot = renderer.DeckOccupants.Claim(occupant);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            Assert.That(renderer.ForeHullId, Is.GreaterThan(0), "he steps in: the block is taken");

            renderer.DeckOccupants.Release(slot, occupant);
            Assert.That(renderer.ForeHullId, Is.Zero, "he steps out: the block is given back");
        }

        [Test] public void TheLevelCut_ReachesTheRenderer_AsTheDefStatesIt()
        {
            var def = LoadClipper();
            var renderer = Install(Host(), def);
            Assert.That(renderer.CutawayShown.Opens, Is.False, "outside, the dwelling is whole");

            IHullCutaway cutaway = renderer;
            var saloon = def.CutForLevel(def.Levels[0].LevelId);
            Assert.That(saloon.Opens, Is.True);
            cutaway.ShowCutaway(saloon);
            Assert.That(renderer.CutawayShown.Level, Is.EqualTo(saloon.Level));
            Assert.That(renderer.CutawayShown.LiftLevels, Is.EqualTo(saloon.LiftLevels));

            cutaway.ShowCutaway(HullMeshDef.Cut.None);
            Assert.That(renderer.CutawayShown.Opens, Is.False);
        }

        [Test] public void Install_RefusesAnUnusableDef_AndAddsNothing()
        {
            var bad = ScriptableObject.CreateInstance<DwellingMeshDef>(); _owned.Add(bad);
            bad.Id = "dwelling.broken_fixture";
            var host = Host();
            LogAssert.Expect(LogType.Error, new Regex("dwelling\\.broken_fixture.*unusable"));
            Assert.That(new IsoFacetDwellingPresentationService().Install(host, bad), Is.Null);
            Assert.That(host.GetComponent<IsoFacetHullRenderer>(), Is.Null);
            Assert.That(host.GetComponent<HullSunShadowCaster>(), Is.Null);

            LogAssert.Expect(LogType.Error, new Regex("unusable"));
            Assert.That(new IsoFacetDwellingPresentationService().Install(host, null), Is.Null);
            Assert.That(new IsoFacetDwellingPresentationService().Install(null, LoadClipper()), Is.Null);
        }

        [Test] public void AttachPart_HangsFromThePosedMesh_ByItsSlot_AndDetachesByItsSlot()
        {
            var part = AssetDatabase.LoadAssetAtPath<HullPropMeshDef>(DoorPart);
            Assert.That(part, Is.Not.Null, DoorPart);
            var host = Host();
            var renderer = Install(host, LoadClipper());
            var service = new IsoFacetDwellingPresentationService();

            var first = (IsoFacetPropRenderer)service.AttachPart(host, part, "door_leaf");
            Assert.That(first, Is.Not.Null);
            Assert.That(first.transform.parent, Is.SameAs(renderer.PosedMesh), "parts turn with the home");
            var again = (IsoFacetPropRenderer)service.AttachPart(host, part, "door_leaf");
            Assert.That(again, Is.SameAs(first), "a re-attach reconfigures in place");
            Assert.That(renderer.PosedMesh.GetComponentsInChildren<IsoFacetPropRenderer>(true), Has.Length.EqualTo(1));

            service.DetachPart(host, "door_leaf");
            Assert.That(renderer.PosedMesh.GetComponentsInChildren<IsoFacetPropRenderer>(true), Is.Empty);
            service.DetachPart(host, "door_leaf");   // safe when none is there
        }

        [Test] public void AttachPart_RefusesWithNothingInstalled()
        {
            var part = AssetDatabase.LoadAssetAtPath<HullPropMeshDef>(DoorPart);
            var host = Host();
            LogAssert.Expect(LogType.Error, new Regex("no dwelling mesh is installed"));
            Assert.That(new IsoFacetDwellingPresentationService().AttachPart(host, part, "door_leaf"), Is.Null);
            Assert.That(host.transform.childCount, Is.Zero);
        }

        [Test] public void Remove_TakesTheRendererPartsCastersAndGroup()
        {
            var part = AssetDatabase.LoadAssetAtPath<HullPropMeshDef>(DoorPart);
            var host = Host();
            var service = new IsoFacetDwellingPresentationService();
            Install(host, LoadClipper());
            service.AttachPart(host, part, "freezer_lid");

            service.Remove(host);
            Assert.That(host.GetComponent<IsoFacetHullRenderer>(), Is.Null);
            Assert.That(host.GetComponent<HullSunShadowCaster>(), Is.Null);
            Assert.That(host.GetComponent<HullLampShadowCaster>(), Is.Null);
            Assert.That(host.GetComponent<SortingGroup>(), Is.Null);
            Assert.That(host.GetComponentsInChildren<IsoFacetPropRenderer>(true), Is.Empty);
            service.Remove(host);   // safe when none is present
            service.Remove(null);
        }

        [Test] public void EnsureRegistered_FillsAnEmptyLocator_AndNeverReplacesALiveService()
        {
            DwellingMeshPresentation.Service = null;
            IsoFacetDwellingPresentationService.EnsureRegistered();
            Assert.That(DwellingMeshPresentation.Service, Is.TypeOf<IsoFacetDwellingPresentationService>());

            var live = new IsoFacetDwellingPresentationService();
            DwellingMeshPresentation.Service = live;
            IsoFacetDwellingPresentationService.EnsureRegistered();
            Assert.That(DwellingMeshPresentation.Service, Is.SameAs(live));
        }
    }
}
