using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Tests.EditMode
{
    public class DaylightSwellReadConformanceTests
    {
        [Test]
        public void DaylightSwellRead_ShaderAndTwinAgree()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("NOT VERIFIED: W1 production HLSL conformance requires the owner's GPU slot; CI has a Null device.");
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
                Assert.Ignore("NOT VERIFIED: this GPU cannot supply the float target required for signed/HDR conformance.");

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(DaylightSwellReadTests.Probe);
            Assert.IsNotNull(shader);
            Assert.IsTrue(shader.isSupported, "W1 conformance shader must compile on the granted graphics backend.");
            var material = new Material(shader);
            var target = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            RenderTexture previousTarget = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                Assert.IsTrue(target.Create());
                GL.sRGBWrite = false;
                int comparisons = 0;
                bool relativeMoved = false;
                foreach (Vector3 colour in new[] { new Vector3(0.04f, 0.1f, 2f), new Vector3(-0.1f, 0f, 0.02f) })
                foreach (float bands in new[] { 0f, 1f, 4f, 1.6f })
                foreach (float strength in new[] { 0f, 0.35f })
                foreach (float gate in new[] { 0f, 0.393586f, 1f })
                foreach (float band in new[] { -1f, 0f, 1f })
                foreach (float mode in new[] { 0f, 1f })
                {
                    material.SetVector("_InputColour", new Vector4(colour.x, colour.y, colour.z, 1f));
                    material.SetFloat("_SignedBand", band);
                    material.SetFloat("_SwellReadStrength", strength);
                    material.SetFloat("_Gate", gate);
                    material.SetFloat("_SwellReadBands", bands);
                    material.SetFloat("_SwellReadRelative", mode);
                    material.SetFloat("_LegacyOracle", 0f);
                    Vector3 drawn = Draw(material, target, readback);
                    Vector3 expected = DaylightSwellRead.Apply(colour, band, strength, gate, bands, mode);
                    for (int c = 0; c < 3; c++)
                        Assert.That(drawn[c], Is.EqualTo(expected[c]).Within(2e-6f * Mathf.Max(1f, Mathf.Abs(expected[c]))),
                            $"channel {c}, C={colour}, band={band}, S={strength}, g={gate}, N={bands}, switch={mode}");

                    material.SetFloat("_LegacyOracle", 1f);
                    Vector3 legacy = Draw(material, target, readback);
                    if (mode == 0f) DaylightSwellReadTests.AssertBits(legacy, drawn);
                    if (strength == 0f || gate == 0f) DaylightSwellReadTests.AssertBits(colour, drawn);
                    if (mode == 1f && Mathf.Abs(drawn.x - legacy.x) > 0.001f) relativeMoved = true;
                    comparisons++;
                }
                Assert.IsTrue(relativeMoved, "A disconnected/ignored adoption switch must not pass conformance.");
                TestContext.WriteLine($"W1 production helper: {comparisons} signed/HDR comparisons; default-switch legacy readbacks bit-equal. Full-water plates and GPU cost still require Phase C.");
            }
            finally
            {
                GL.sRGBWrite = previousSrgb;
                RenderTexture.active = previousTarget;
                target.Release();
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(material);
            }
        }

        static Vector3 Draw(Material material, RenderTexture target, Texture2D readback)
        {
            Graphics.Blit(Texture2D.blackTexture, target, material, 0);
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
            readback.Apply(false, false);
            Color colour = readback.GetPixel(0, 0);
            return new Vector3(colour.r, colour.g, colour.b);
        }
    }
}
