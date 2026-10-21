#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class NineMileCreekBerthFoamPlayTests : NineMileCreekBerthProof
    {
        [UnityTest]
        public IEnumerator NoPaleWakeDiscUnderAnyResidentHullAtThePlateTides()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: pale-disc pixel proof requires a graphics device.");
            Camera cam = Camera.main;
            Assert.IsNotNull(cam);
            var follow = cam.GetComponent<CameraFollow>();
            var ppc = cam.GetComponent("PixelPerfectCamera") as Behaviour;
            bool oldFollow = follow != null && follow.enabled, oldPpc = ppc != null && ppc.enabled;
            Vector3 oldPosition = cam.transform.position;
            float oldSize = cam.orthographicSize, oldAspect = cam.aspect;
            RenderTexture oldTarget = cam.targetTexture;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGBHalf);
            rt.Create();
            try
            {
                if (follow != null) follow.enabled = false;
                if (ppc != null) ppc.enabled = false;
                cam.targetTexture = rt;
                cam.aspect = 1280f / 720f;
                cam.orthographicSize = 25;
                cam.transform.position = new Vector3(132, 85, oldPosition.z);
                foreach (double time in Tides)
                {
                    yield return Seek(time, 120);
                    Time.timeScale = 0;
                    // Same camera, same frame. Removing only wake foam identifies the offending layer;
                    // white hull paint, reflections and sky highlights cancel rather than counting as foam.
                    Color[] on = Capture(cam, rt);
                    var renderers = Resources.FindObjectsOfTypeAll<Renderer>().Where(r =>
                        r.gameObject.scene == Loaded && r.sharedMaterial != null &&
                        r.sharedMaterial.HasProperty("_WakeFoamStrength")).ToArray();
                    Assert.Greater(renderers.Length, 1, "Include the hidden displaced-water chunks, not just disabled Sea.");
                    var oldBlocks = renderers.Select(r => { var b = new MaterialPropertyBlock(); r.GetPropertyBlock(b); return b; }).ToArray();
                    Color[] off;
                    try
                    {
                        foreach (Renderer r in renderers)
                        {
                            var b = new MaterialPropertyBlock(); r.GetPropertyBlock(b);
                            b.SetFloat("_WakeFoamStrength", 0); r.SetPropertyBlock(b);
                        }
                        off = Capture(cam, rt);
                    }
                    finally { for (int i = 0; i < renderers.Length; i++) renderers[i].SetPropertyBlock(oldBlocks[i]); }
                    var failures = new List<string>();
                    foreach (var boat in Fleet)
                    {
                        var injector = boat.GetComponentInChildren<FoamInjector>();
                        var source = boat.GetComponentInChildren<IHullWakePoseSource>();
                        Assert.IsNotNull(injector, boat.Owner.Id + " has no foam source to inspect");
                        Assert.IsTrue(source != null && source.TryGetWakePose(out _), boat.Owner.Id + " has no drawn stern");
                        source.TryGetWakePose(out HullWakePose pose);
                        Vector3 centre = cam.WorldToViewportPoint(pose.DrawnStern);
                        Assert.That(centre.x, Is.InRange(0f, 1f), boat.Owner.Id + " is out of frame");
                        Assert.That(centre.y, Is.InRange(0f, 1f), boat.Owner.Id + " is out of frame");
                        float cx = centre.x * rt.width, cy = centre.y * rt.height;
                        float radius = injector.RadiusMeters * rt.height / (2 * cam.orthographicSize);
                        int pale = 0;
                        for (int y = Mathf.Max(0, Mathf.FloorToInt(cy-radius)); y <= Mathf.Min(rt.height-1, Mathf.CeilToInt(cy+radius)); y++)
                        for (int x = Mathf.Max(0, Mathf.FloorToInt(cx-radius)); x <= Mathf.Min(rt.width-1, Mathf.CeilToInt(cx+radius)); x++)
                        {
                            if ((x-cx)*(x-cx)+(y-cy)*(y-cy) > radius*radius) continue;
                            Color a = on[y*rt.width+x], b = off[y*rt.width+x];
                            // Linear RGB > .5 is a visibly pale patch; > .15 contribution separates
                            // an actual foam disc from readback rounding at a shared edge.
                            if (a.r > .5f && a.g > .5f && a.b > .5f &&
                                a.r-b.r > .15f && a.g-b.g > .15f && a.b-b.b > .15f) pale++;
                        }
                        if (pale > 0) failures.Add(boat.Owner.Id + ": " + pale + " pale wake pixels");
                    }
                    Assert.IsEmpty(failures, "Pale discs at t=" + time + ": " + string.Join("; ", failures));
                    Time.timeScale = 1;
                }
            }
            finally
            {
                Time.timeScale = 1;
                cam.targetTexture = oldTarget; cam.transform.position = oldPosition;
                cam.orthographicSize = oldSize; cam.aspect = oldAspect;
                if (follow != null) follow.enabled = oldFollow;
                if (ppc != null) ppc.enabled = oldPpc;
                rt.Release(); Object.Destroy(rt);
            }
        }

        static Color[] Capture(Camera cam, RenderTexture rt)
        {
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                cam.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
                return texture.GetPixels();
            }
            finally { RenderTexture.active = previous; Object.Destroy(texture); }
        }
    }
}

#endif
