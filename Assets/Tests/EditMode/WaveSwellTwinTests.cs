using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WaveSwellTwinTests
    {
        [Test]
        public void SwellTrains_PackIntoTheSameEightSlots_WithTheSamePhases()
        {
            var sea = WaveSwellTests.Sea(new Vector2(8f, 1f), 0.6f);
            var packed = WaveFieldBridge.Pack(in sea);
            Assert.AreEqual(8, packed.Count); Assert.AreEqual(8, PackedWaveField.MaxTrains);
            Assert.AreEqual(sea.TotalAmplitude, packed.TotalAmplitude);
            for (int i = 6; i < 8; i++)
            {
                Assert.AreEqual(sea[i].Direction.x, packed.Train(i).x);
                Assert.AreEqual(sea[i].Direction.y, packed.Train(i).y);
                Assert.AreEqual(sea[i].Amplitude, packed.Train(i).w);
                Assert.AreEqual(sea[i].PhaseOffset, packed.Phase(i));
            }
        }

        [Test]
        public void SwellShelter_ThePackedTwinAgrees_AcrossBothFrequencyScales()
        {
            var sea = WaveSwellTests.Sea(new Vector2(8f, 1f), 0.6f);
            var packed = WaveFieldBridge.Pack(in sea);
            var settings = WaveFetchSettings.Default;
            settings.Strength = 0.8f;
            foreach (float windReach in new[] { 0f, 0.2f, 1f })
            foreach (float swell in new[] { 0f, 0.4f, 1f })
            foreach (float scale in new[] { 1f, 2.8f })
            for (int i = 0; i < 70; i++)
            {
                var at = new Vector2(i * 0.37f - 13f, i * -0.7f);
                var fetch = new WaveFetchSample(windReach, in settings).WithSwell(swell);
                var cpu = WaveMath.Sample(at * scale, 0.0, in sea, fetch);
                var twin = WaveFieldBridge.ShaderTwinSample(at, in packed, scale, fetch);
                Assert.AreEqual(cpu.Height, twin.Height, 0.00002f);
                Assert.AreEqual(cpu.Slope.x * scale, twin.Slope.x, 0.00003f);
                Assert.AreEqual(cpu.Slope.y * scale, twin.Slope.y, 0.00003f);
                Assert.AreEqual(cpu.CrestFactor, twin.CrestFactor, 0.00003f);
            }
        }

        [Test]
        public void GlassCalm_ShelteredSwell_ThePackedTwinReadsExactlyFlat()
        {
            var sea = WaveSwellTests.Sea(Vector2.zero, 0f);
            var packed = WaveFieldBridge.Pack(in sea);
            var fetch = WaveFetchSample.Open.WithSwell(0f);
            for (int i = 0; i < 100; i++)
            {
                var sample = WaveFieldBridge.ShaderTwinSample(new Vector2(i * 1.7f, -i * 0.9f), in packed, 2.8f, fetch);
                Assert.AreEqual(0f, sample.Height); Assert.AreEqual(Vector2.zero, sample.Slope);
                Assert.AreEqual(0f, sample.CrestFactor);
            }
        }
    }
}
