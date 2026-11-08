using HiddenHarbours.Core;
using HiddenHarbours.Art;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public sealed class ShoreLineCaptureHarnessTests
    {
        [Test] public void MedianRejectsAnOutlierIndependentlyInEveryChannel()
        {
            var a = new[] { new Color32(1, 240, 10, 255), new Color32(90, 1, 8, 255) };
            var b = new[] { new Color32(2, 100, 20, 255), new Color32(2, 3, 7, 255) };
            var c = new[] { new Color32(255, 90, 15, 255), new Color32(1, 2, 6, 255) };
            CollectionAssert.AreEqual(new[] { new Color32(2, 100, 15, 255), new Color32(2, 2, 7, 255) }, ShoreLineCaptureHarness.Median(a, b, c));
        }
        [Test] public void NoiseMaskNamesOnlyChangedPixelsAndConvertsTopOriginCoordinates()
        {
            var a = new[] { new Color32(1, 1, 1, 255), new Color32(2, 2, 2, 255), new Color32(3, 3, 3, 255), new Color32(4, 4, 4, 255) };
            var b = (Color32[])a.Clone(); b[0].r = 3; b[3].b = 7;
            var noise = ShoreLineCaptureHarness.Noise(a, b, 2);
            Assert.AreEqual(2, noise.Length); Assert.AreEqual(0, noise[0].x); Assert.AreEqual(1, noise[0].y); Assert.AreEqual(2, noise[0].dr);
            CollectionAssert.AreEqual(new[] { true, false, false, true }, ShoreLineCaptureHarness.NoiseMask(noise, 2, 2));
        }
        [Test] public void RenderedClockIsFrozenAfterUrpCameraSetupAndRestoresCoreClock()
        {
            const string source = @"Shader ""Hidden/ShoreCaptureClockPin"" { SubShader { Tags { ""RenderPipeline""=""UniversalPipeline"" } Pass { Tags { ""LightMode""=""Universal2D"" } ZWrite Off Cull Off
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
float4 vert(float4 p:POSITION):SV_POSITION{return TransformObjectToHClip(p.xyz);}
float4 frag():SV_Target{return float4(_Time.y/43200,0,0,1);}
ENDHLSL
} } }";
            var shader = ShaderUtil.CreateShaderAsset(source); var material = new Material(shader);
            var quad = new GameObject("Capture clock test quad") { hideFlags = HideFlags.HideAndDontSave };
            var cameraObject = new GameObject("Capture clock test camera") { hideFlags = HideFlags.HideAndDontSave };
            var mesh = new Mesh(); var target = new RenderTexture(4, 4, 24, RenderTextureFormat.ARGB32);
            var priorClock = GameServices.Clock;
            try
            {
                quad.transform.position = new Vector3(10000, 10000);
                mesh.vertices = new[] { new Vector3(-1, -1), new Vector3(1, -1), new Vector3(1, 1), new Vector3(-1, 1) };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateBounds();
                quad.AddComponent<MeshFilter>().sharedMesh = mesh; quad.AddComponent<MeshRenderer>().sharedMaterial = material;
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = 1; camera.transform.position = new Vector3(10000, 10000, -10);
                using (var clock = new ShoreLineCaptureHarness.PoseClock(camera, 12))
                {
                    var pixels = ShoreLineCaptureHarness.ReadMedian(camera, target);
                    Assert.AreEqual(255, pixels[5].r, "The shader must see the frozen hour rather than editor realtime.");
                    Assert.GreaterOrEqual(clock.AppliedFrames, 7); Assert.AreEqual(43200, GameServices.Clock.FlowSeconds);
                }
                Assert.AreSame(priorClock, GameServices.Clock);
            }
            finally
            {
                target.Release();
                foreach (var obj in new Object[] { quad, cameraObject, mesh, target, material, shader }) Object.DestroyImmediate(obj);
            }
        }

        [Test] public void DistanceSnapshotUsesOriginalEnabledStateWhileWallsAreHidden()
        {
            var host = new GameObject("Wall snapshot fixture") { hideFlags = HideFlags.HideAndDontSave };
            host.SetActive(false); host.AddComponent<CliffWallSurface>();
            var child = new GameObject("Face") { hideFlags = HideFlags.HideAndDontSave }; child.transform.SetParent(host.transform);
            var mesh = new Mesh();
            try
            {
                mesh.vertices = new[] { new Vector3(-1, -1), new Vector3(1, -1), new Vector3(1, 1), new Vector3(-1, 1) };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.AddComponent<MeshRenderer>(); renderer.enabled = false;
                var original = new Dictionary<Renderer, bool> { [renderer] = true };
                var snapshot = ShoreLineCaptureHarness.SnapshotFaces(new[] { renderer }, original,
                    new Vector2(-2, -2), new Vector2(4, 4), .25f);
                Assert.IsTrue(snapshot.At(Vector2.zero)); Assert.IsFalse(renderer.enabled);
                original[renderer] = false;
                Assert.IsFalse(ShoreLineCaptureHarness.SnapshotFaces(new[] { renderer }, original,
                    new Vector2(-2, -2), new Vector2(4, 4), .25f).At(Vector2.zero));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(mesh); }
        }
    }
}
