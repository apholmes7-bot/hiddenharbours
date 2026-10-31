using System.Collections;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class HullDeckShadowPlayTests
    {
        [UnityTest] public IEnumerator CapeIslanderAtBerth_0800()=>Check("CapeIslander",8,true);
        [UnityTest] public IEnumerator CapeIslanderAtBerth_1700()=>Check("CapeIslander",17,true);
        [UnityTest] public IEnumerator CapeIslanderAtBerth_2230()=>Check("CapeIslander",22.5f,false);
        [UnityTest] public IEnumerator DoryAtSea_CastsNoDeckShadow()=>Check("Dory",8,false);
        static IEnumerator Check(string name,float hour,bool casts)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)Assert.Ignore("The deck proof requires a graphics device.");
#if UNITY_EDITOR
            var def=AssetDatabase.LoadAssetAtPath<HullMeshDef>("Assets/_Project/Data/Boats/HullMeshes/"+name+"IsoHullMesh.asset");
            Assert.That(def,Is.Not.Null);
            var host=new GameObject("DeckPlay"+name){layer=31};
            host.transform.position=new Vector3(98,name=="Dory"?27.5f:82.5f,0);
            Vector4 oldSun=Shader.GetGlobalVector("_SunDir");float oldE=Shader.GetGlobalFloat("_SunElevation"),oldS=Shader.GetGlobalFloat("_ShadowStrength");
            var cameraObject=new GameObject("DeckPlayCamera");var camera=cameraObject.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=15;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;
            camera.transform.position=host.transform.position+Vector3.back*100;camera.farClipPlane=500;camera.allowHDR=false;camera.allowMSAA=false;
            var target=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);camera.targetTexture=target;
            var pixels=new Texture2D(256,256,TextureFormat.RGBA32,false);
            try
            {
                var hull=(IsoFacetHullRenderer)new IsoFacetHullPresentationService().Install(host,def);
                foreach(Transform child in host.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                host.GetComponent<HullSunShadowCaster>().enabled=false;
                hull.HeadingDirUnits=2;hull.ApplyPose();
                var profile=Resources.Load<DayNightProfile>("DayNightProfile");
                Vector2 sun=DayNightMath.SunDirection(hour,profile.SunriseHour,profile.SunsetHour,profile.ShadowSouthBias,profile.ShadowNoonLift);
                Shader.SetGlobalVector("_SunDir",new Vector4(sun.x,sun.y,0,0));Shader.SetGlobalFloat("_SunElevation",DayNightMath.SunElevation(hour,profile.SunriseHour,profile.SunsetHour));
                Shader.SetGlobalFloat("_ShadowStrength",DayNightMath.ShadowStrength(hour,profile.SunriseHour,profile.SunsetHour,0,profile.OvercastFadesShadow));
                yield return null;
                var deck=host.GetComponent<HullDeckShadowCaster>();
                Color32[] Read(bool on)
                {
                    Shader.SetGlobalVector("_SunDir",new Vector4(sun.x,sun.y,0,0));Shader.SetGlobalFloat("_SunElevation",DayNightMath.SunElevation(hour,profile.SunriseHour,profile.SunsetHour));
                    Shader.SetGlobalFloat("_ShadowStrength",DayNightMath.ShadowStrength(hour,profile.SunriseHour,profile.SunsetHour,0,profile.OvercastFadesShadow));
                    deck.enabled=on;deck.Tick();camera.Render();camera.Render();
                    RenderTexture previous=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();RenderTexture.active=previous;return pixels.GetPixels32();
                }
                var before=Read(false);var after=Read(true);int changed=0;
                for(int i=0;i<before.Length;i++)if(!before[i].Equals(after[i]))changed++;
                if(casts){Assert.That(deck.ShadowRenderer.enabled,Is.True);Assert.That(changed,Is.GreaterThan(0));}
                else{Assert.That(changed,Is.Zero);if(name=="Dory")Assert.That(deck.ShadowRenderer,Is.Null);else Assert.That(deck.ShadowRenderer.enabled,Is.False);}
            }
            finally
            {
                Shader.SetGlobalVector("_SunDir",oldSun);Shader.SetGlobalFloat("_SunElevation",oldE);Shader.SetGlobalFloat("_ShadowStrength",oldS);
                camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(host);
            }
#else
            Assert.Ignore("Committed hull definitions are loaded by the editor test runner.");yield break;
#endif
        }
    }
}
