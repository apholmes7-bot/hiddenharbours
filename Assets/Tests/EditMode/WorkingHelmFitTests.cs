using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class WorkingHelmFitTests
    {
        [TestCase("Dory", "boat.dory", WorkingInstrument.None)]
        [TestCase("LobsterBoat", "boat.lobster_boat", WorkingInstrument.Depth | WorkingInstrument.Motion | WorkingInstrument.Wind | WorkingInstrument.Engine)]
        [TestCase("SternTrawler", "boat.stern_trawler", WorkingInstrument.All)]
        public void AuthoredFit_UsesStableHullIdentityAndExistingEquipmentResolver(string name, string hullId, WorkingInstrument expected)
        {
            var profile = AssetDatabase.LoadAssetAtPath<WorkingHelmFitDef>("Assets/_Project/Data/Boats/InstrumentFits/" + name + ".asset");
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.Problem(), Is.Null);
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/" + name + ".asset");
            Assert.That(hull, Is.Not.Null);
            Assert.That(profile.HullId, Is.EqualTo(hull.Id));
            Assert.That(profile.HullId, Is.EqualTo(hullId));
            Assert.That(profile.Resolve(profile.DefaultEquipment()).Instruments, Is.EqualTo(expected));
        }

        [Test]
        public void LiveEquipmentIsAuthoritative_ProfileDoesNotGrantAnUnownedGps()
        {
            var profile = AssetDatabase.LoadAssetAtPath<WorkingHelmFitDef>("Assets/_Project/Data/Boats/InstrumentFits/SternTrawler.asset");
            var live = new HelmFit(ConsoleRigKind.Novi, SounderKind.Depth, CompassMount.None, false, false);
            Assert.That(profile.Resolve(live).Instruments & WorkingInstrument.Plotter, Is.EqualTo(WorkingInstrument.None));
            Assert.That(profile.Resolve(live).Instruments & WorkingInstrument.Depth, Is.EqualTo(WorkingInstrument.Depth));
        }

        [Test]
        public void ProfileValidatorRejectsDuplicateIds_NegativeCalibration_AndParallelSlotFlags()
        {
            var profile = ScriptableObject.CreateInstance<WorkingHelmFitDef>();
            try
            {
                profile.Id = "helm_fit.test"; profile.HullId = "boat.test";
                profile.FactoryInstruments = new[] { BoatEquipment.GpsId, BoatEquipment.GpsId };
                Assert.That(profile.Problem(), Is.EqualTo("duplicate instrument id"));
                profile.FactoryInstruments = System.Array.Empty<string>();
                profile.MinimumKeelClearanceMetres = -1f;
                Assert.That(profile.Problem(), Is.EqualTo("invalid calibration"));
                profile.MinimumKeelClearanceMetres = 0f; profile.AdditionalReadings = WorkingInstrument.Plotter;
                Assert.That(profile.Problem(), Is.EqualTo("depth and plotter belong to existing equipment"));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void BerthProjectionUsesExistingMidpointAndSourceRegion()
        {
            var berth = ScriptableObject.CreateInstance<BerthDef>();
            try
            {
                berth.Id = "berth.test"; berth.RegionId = "region.test";
                berth.Stern = new Vector2(10f, 20f); berth.Bow = new Vector2(10f, 30f);
                var point = HelmRegionWaypoints.Berth(berth);
                Assert.That(point.Id, Is.EqualTo(berth.Id));
                Assert.That(point.RegionId, Is.EqualTo(berth.RegionId));
                Assert.That(point.Position, Is.EqualTo(new Vector2(10f, 25f)));
                Assert.That(point.Kind, Is.EqualTo(HelmWaypointKind.Berth));
            }
            finally { Object.DestroyImmediate(berth); }
        }

        [Test]
        public void SeamProjectionUsesExistingSourceTrigger_NeverTargetArrivalCoordinates()
        {
            var target = ScriptableObject.CreateInstance<RegionDef>();
            var go = new GameObject("Seam fixture");
            try
            {
                target.DisplayName = "Next water"; go.transform.position = new Vector3(12f, 15f, 0f);
                var passage = go.AddComponent<RegionPassage>(); passage.Configure(target, null, "arrival.north");
                var point = HelmRegionWaypoints.SeamExit("region.source", "passage.source_north", passage);
                Assert.That(point.RegionId, Is.EqualTo("region.source"));
                Assert.That(point.Position, Is.EqualTo(new Vector2(12f, 15f)));
                Assert.That(point.Name, Is.EqualTo(target.DisplayName));
                Assert.That(point.Kind, Is.EqualTo(HelmWaypointKind.SeamExit));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(target); }
        }

        [Test]
        public void WaypointCatalogueHasStableOrderingAndRejectsDuplicateIdentity()
        {
            var a = new HelmWaypoint("a", "region.test", "A", Vector2.zero, HelmWaypointKind.Berth);
            var b = new HelmWaypoint("b", "region.test", "B", Vector2.up, HelmWaypointKind.SeamExit);
            var catalogue = new HelmRegionWaypoints(new[] { b, a });
            Assert.That(catalogue.Count, Is.EqualTo(2));
            Assert.That(catalogue.At(0).Id, Is.EqualTo("a"));
            Assert.That(catalogue.At(1).Id, Is.EqualTo("b"));
            Assert.Throws<System.ArgumentException>(() => new HelmRegionWaypoints(new[] { a, a }));
        }
    }
}
