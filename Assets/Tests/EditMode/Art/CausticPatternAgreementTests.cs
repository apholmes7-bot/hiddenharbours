using System.Runtime.InteropServices;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class CausticPatternAgreementTests
    {
        // Absolute brightness error, in the shared function's scalar optical value.
        private const float GpuCpuAbsoluteTolerance = 2e-5f;

        [StructLayout(LayoutKind.Sequential)]
        private struct Sample
        {
            public float Independent, Centre, PositiveX, NegativeX, PositiveY, NegativeY;
            public float Step, Gain, Blend, Envelope;

            public Sample(float independent, float centre, float px, float nx, float py, float ny,
                          float step, float gain, float blend, float envelope)
            {
                Independent = independent; Centre = centre; PositiveX = px; NegativeX = nx;
                PositiveY = py; NegativeY = ny; Step = step; Gain = gain; Blend = blend; Envelope = envelope;
            }
        }

        [Test]
        public void SharedHlsl_AgreesWithTheCSharpTwin_AtSampledHeights()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device (Null Device); the shared-pattern probe needs a GPU.");
            if (!SystemInfo.supportsComputeShaders) Assert.Ignore("A compute-capable graphics device is required.");
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/Tests/EditMode/Art/CausticPatternProbe.compute");
            Assert.That(shader, Is.Not.Null);
            var samples = new[]
            {
                new Sample(.3f, 0, 0, 0, 0, 0, .5f, 12, .5f, 1),
                new Sample(.7f, 1, 1, 1, .984375f, 1, .5f, 12, .5f, 1),
                new Sample(.2f, 1, 1, 1, 1.015625f, 1, .5f, 12, .5f, 1),
                new Sample(.4f, 1, 1, 1, .984375f, 1, 1, 12, .5f, 1),
                new Sample(.8f, 1, 1, 1, .984375f, 1, .5f, -2, .5f, 1),
                new Sample(.6f, 1, 1, 1, .984375f, 1, .5f, 12, 0, 1),
                new Sample(.6f, 1, 1, 1, .984375f, 1, .5f, 12, .001f, 1),
                new Sample(.6f, 1, 1, 1, .984375f, 1, .5f, 12, .0011f, 1),
                new Sample(.9f, 1, 1, 1, .984375f, 1, .5f, 12, 1, 0),
                new Sample(.9f, 1, 1, 1, .984375f, 1, .5f, 12, 1, .0125f),
                new Sample(.1f, 1, 1, 1, .984375f, 1, .5f, 12, 2, 1),
                new Sample(.1f, 1, 0, 0, 0, 0, 0, 12, 1, 1),
                new Sample(.1f, -2, -3, -1, -2, -2, .5f, 12, 1, 1),
                new Sample(1.3f, 1, 1, 1, .984375f, 1, .5f, 12, -.5f, 1),
            };
            using var input = new ComputeBuffer(samples.Length, Marshal.SizeOf<Sample>());
            using var output = new ComputeBuffer(samples.Length, sizeof(float));
            input.SetData(samples);
            int kernel = shader.FindKernel("ComparePattern");
            shader.SetInt("_Count", samples.Length);
            shader.SetBuffer(kernel, "_Inputs", input);
            shader.SetBuffer(kernel, "_Outputs", output);
            shader.Dispatch(kernel, (samples.Length + 7) / 8, 1, 1);
            var actual = new float[samples.Length];
            output.GetData(actual);
            for (int i = 0; i < samples.Length; i++)
            {
                Sample s = samples[i];
                float expected = CausticMath.Pattern(s.Independent, s.Centre, s.PositiveX, s.NegativeX,
                    s.PositiveY, s.NegativeY, s.Step, s.Gain, s.Blend, s.Envelope);
                Assert.That(actual[i], Is.EqualTo(expected).Within(GpuCpuAbsoluteTolerance), "sample " + i);
            }
        }
    }
}
