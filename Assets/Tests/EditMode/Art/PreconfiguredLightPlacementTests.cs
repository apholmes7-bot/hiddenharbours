using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public sealed class PreconfiguredLightPlacementTests
    {
        [Test]
        public void PlacementReach_KeepsThePostAndDoorPoolsDistinct()
        {
            Assert.That(PreconfiguredLight.ResolvedReach(LightPresets.Kind.HungLantern, 3.6f), Is.EqualTo(3.6f));
            Assert.That(PreconfiguredLight.ResolvedReach(LightPresets.Kind.HungLantern, 3.4f), Is.EqualTo(3.4f));
            Assert.That(PreconfiguredLight.ResolvedReach(LightPresets.Kind.Lightpost, 0f), Is.EqualTo(3.6f));
            Assert.That(PreconfiguredLight.ResolvedReach(LightPresets.Kind.Lantern, 0f), Is.EqualTo(3f));
        }

        [Test]
        public void PlacementReach_SurvivesPresetWidthAndEnableChanges()
        {
            var go = new GameObject("village lamp");
            try
            {
                var light = go.AddComponent<SceneLight>();
                var preset = go.AddComponent<PreconfiguredLight>();
                preset.ReachOverrideMetres = 3.6f;
                preset.Preset = LightPresets.Kind.HungLantern;
                preset.FittingWidthMetres = .14f;
                go.SetActive(false);
                go.SetActive(true);
                Assert.That(light.ReachMetres, Is.EqualTo(3.6f));
                Assert.That(light.Range, Is.EqualTo(LightPresets.BloomForFitting(.14f)));
                preset.ReachOverrideMetres = 0f;
                Assert.That(light.ReachMetres, Is.EqualTo(3.6f),
                    "the 10-27 ruling A: a cleared override falls back to the hung lantern's 3.6 m default");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
