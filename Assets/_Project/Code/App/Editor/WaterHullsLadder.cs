using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    public static partial class WaterHullsPlates
    {
        private static readonly bool LadderStudy = System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_LADDER") == "1";
        private static readonly int[] LadderOrder = {6,0,7,1,2,3,4,5,8,9,10,11,12,13,14,15};
        private static Texture2D _geometryWhite;
        private static string _facetShaderName;
        private static Color32[] _geometricMask;
        private static float _physicalGain;
        private static RenderTexture _floatTarget;

        private static void InitializeLadder()
        {
            Directory.CreateDirectory(Path.Combine(_output,"ladder","raw"));
            string source=File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursIsoFacet.shader");
            Match name=Regex.Match(source,"Shader\\s+\"([^\"]+)\"");
            if(!name.Success) throw new InvalidOperationException("Facet shader name not found.");
            _facetShaderName=name.Groups[1].Value;
            _geometryWhite=new Texture2D(1,1,TextureFormat.RGBA32,false,true);
            _geometryWhite.hideFlags=HideFlags.HideAndDontSave;_geometryWhite.SetPixel(0,0,Color.white);_geometryWhite.Apply();
            _floatTarget=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            _floatTarget.Create();
            string pinFolder=System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_NATIVE_PIN_EVIDENCE");
            string pin=string.IsNullOrEmpty(pinFolder)?null:Path.Combine(pinFolder,"native-encoding-pin.json");
            if(pin==null||!File.Exists(pin))RunNativeEncodingPin(Path.Combine(_output,"native-pin"));
            else
            {
                var proof=JsonUtility.FromJson<EncodingPinProof>(File.ReadAllText(pin));
                if(!proof.succeeded||proof.sha!=System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SHA"))
                    throw new InvalidOperationException("Native encoding pin is not green for this source SHA.");
                Directory.CreateDirectory(Path.Combine(_output,"native-pin"));
                File.Copy(pin,Path.Combine(_output,"native-pin","native-encoding-pin.json"));
            }
        }

        public static void RunNativeEncodingPin(string output)
        {
            Directory.CreateDirectory(output);
            var proof=new EncodingPinProof {sha=System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SHA"),
                scope="ARGB32 sRGB target, production encode function, analytic wall fixture from the recorded noon parameters; live fleet physical gain is measured separately"};
            var samples=new List<EncodingPinSample>();Material material=null;Shader shader=null;RenderTexture target=null;
            RenderTexture active=RenderTexture.active;bool write=GL.sRGBWrite;
            try
            {
                string source=@"Shader ""Hidden/WaterHullsNativeEncodingPin"" { SubShader { Pass {
                    ZTest Always Cull Off ZWrite Off
                    HLSLPROGRAM
                    #pragma vertex vert_img
                    #pragma fragment frag
                    #include ""UnityCG.cginc""
                    #include ""Assets/_Project/Art/Shaders/Include/HullCausticWeight.hlsl""
                    float _PinGain, _PinPattern, _PinWeight, _PinRaw;
                    float4 frag(v2f_img i):SV_Target {
                        float a=_PinRaw>.5 ? 1.0-saturate(_PinGain*_PinPattern*_PinWeight)*.5
                            : HHHullCausticEncodeAlpha(_PinGain,_PinPattern,_PinWeight);
                        return float4(0,0,0,a);
                    }
                    ENDHLSL
                } } }";
                shader=ShaderUtil.CreateShaderAsset(source);shader.hideFlags=HideFlags.HideAndDontSave;
                if(ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Native encoding pin shader compile error.");
                material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                target=new RenderTexture(new RenderTextureDescriptor(4,4,RenderTextureFormat.ARGB32,0){sRGB=true,msaaSamples=1});target.Create();
                float weight=HullCausticLighting.Weight(.1f,.395f,true,0,.5f,true,1,3,1,.05f,.6f,.18f,
                    new Vector4(.02f,12,5,.5f),new Vector2(1,.05f),Vector2.right,Vector2.right,.15f,1);
                const float pattern=.6f;float physical=(4f/255)/(pattern*weight);proof.analyticPhysicalGain=physical;
                material.SetFloat("_PinPattern",pattern);material.SetFloat("_PinWeight",weight);GL.sRGBWrite=false;
                double previous=0;
                foreach(float multiplier in new[]{0f,1f,3f,6f,10f,1e20f})
                {
                    float gain=physical*multiplier;material.SetFloat("_PinRaw",0);material.SetFloat("_PinGain",gain);
                    Graphics.Blit(Texture2D.whiteTexture,target,material);Color32[] pixels=Read(target);
                    int min=pixels.Min(c=>(int)c.a),max=pixels.Max(c=>(int)c.a);
                    double mean=pixels.Average(c=>(double)CausticMath.DecodeAlpha(c.a/255f));
                    samples.Add(new EncodingPinSample {multiplier=multiplier,gain=gain,minAlphaByte=min,maxAlphaByte=max,decodedMean=mean});
                    if(multiplier==0 && (min!=255||mean!=0))throw new InvalidOperationException("Native zero-gain pin failed.");
                    if(multiplier>0 && (min<128||mean<=0||mean+1e-7<previous))throw new InvalidOperationException("Native encoding endpoint/monotonicity pin failed.");
                    if(multiplier==1 && Math.Abs(mean-4d/255)>2d/255+1e-7)throw new InvalidOperationException("Native physical test rung missed 4/255 by more than one step.");
                    previous=mean;
                }
                material.SetFloat("_PinRaw",1);material.SetFloat("_PinGain",1e30f);Graphics.Blit(Texture2D.whiteTexture,target,material);
                proof.unguardedSyncAlpha=Read(target)[0].a;
                var raw=UnityEngine.Rendering.AsyncGPUReadback.Request(target,0);raw.WaitForCompletion();
                if(raw.hasError)throw new InvalidOperationException("Native raw readback comparison failed.");
                proof.unguardedRawAlpha=raw.GetData<byte>()[3];
                proof.conversionObservation=proof.unguardedSyncAlpha!=proof.unguardedRawAlpha
                    ?"Synchronous versus raw readback differs; conversion occurs after the raw target byte."
                    :proof.unguardedRawAlpha==127?"Both paths return 127; the unguarded target already contains 127."
                    :"The isolated unguarded control did not reproduce the earlier 127; recorded once, not pursued further.";
                proof.succeeded=true;
            }
            catch(Exception error){proof.error=error.ToString();throw;}
            finally
            {
                proof.samples=samples.ToArray();File.WriteAllText(Path.Combine(output,"native-encoding-pin.json"),JsonUtility.ToJson(proof,true));
                GL.sRGBWrite=write;RenderTexture.active=active;
                if(target!=null){target.Release();Object.DestroyImmediate(target);}if(material!=null)Object.DestroyImmediate(material);if(shader!=null)Object.DestroyImmediate(shader);
            }
        }

        private static void Gain(float gain)
        {
            GameServices.Config.HullCausticVeinGain=gain;
            foreach(var hull in Object.FindObjectsByType<IsoFacetHullRenderer>(FindObjectsSortMode.None)) hull.ApplyPose();
        }

        private static Color32[] GeometricMask()
        {
            if(_geometricMask!=null) return _geometricMask;
            Source(false);
            var renderers=new HashSet<Renderer>();
            foreach(var hull in Object.FindObjectsByType<IsoFacetHullRenderer>(FindObjectsSortMode.None))
            {
                renderers.Add(hull.PosedMesh.GetComponent<Renderer>());
                foreach(var renderer in hull.GetComponentsInChildren<Renderer>())
                    foreach(Material material in renderer.sharedMaterials)
                        if(material!=null && material.shader.name==_facetShaderName) renderers.Add(renderer);
            }
            var originals=renderers.Select(r=>{var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);return(renderer:r,block:block);}).ToArray();
            var diagnostic=new List<string>();
            foreach(var hull in _boats[1].GetComponentsInChildren<IsoFacetHullRenderer>())
            {
                var renderer=hull.PosedMesh.GetComponent<Renderer>();var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                Vector4 plane=block.GetVector("_HHHullCausticPlane"),settings=block.GetVector("_HHHullCausticSettings");
                var mesh=hull.PosedMesh.GetComponent<MeshFilter>().sharedMesh;Vector3[] vertices=mesh.vertices;float min=float.PositiveInfinity,max=float.NegativeInfinity;
                foreach(Vector3 vertex in vertices){Vector3 point=hull.PosedMesh.TransformPoint(vertex);float height=Vector4.Dot(plane,new Vector4(point.x,point.y,point.z,1));min=Mathf.Min(min,height);max=Mathf.Max(max,height);}
                diagnostic.Add("hull="+hull.name+"; shader="+renderer.sharedMaterial.shader.name+"; settings="+settings.ToString("R")+"; plane="+plane.ToString("R")
                    +"; heights="+min.ToString("R")+".."+max.ToString("R")+"; root="+hull.transform.position.ToString("R")+"; bounds="+renderer.bounds);
                diagnostic.Add("terrain="+(GameServices.TidalTerrain!=null?GameServices.TidalTerrain.ElevationAt(hull.transform.position).ToString("R"):"none")
                    +"; seaLevel="+typeof(HullCausticLighting).GetField("_waterLevel",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null));
            }
            WritePng("geometric-probe-view.png",Read(_target));
            File.WriteAllLines(Path.Combine(_output,"geometric-packet-diagnostic.txt"),diagnostic);
            try
            {
                // Diagnostic only: preserve plane, width, wet/interior/upward gates and depth.
                // Force positive optical factors on the existing shader; no material/shader swap.
                foreach(var item in originals)
                {
                    var block=new MaterialPropertyBlock();item.renderer.GetPropertyBlock(block);
                    Vector4 settings=block.GetVector("_HHHullCausticSettings");settings.y=1;block.SetVector("_HHHullCausticSettings",settings);
                    Vector4 bearing=block.GetVector("_HHHullCausticBearing");bearing.z=1;block.SetVector("_HHHullCausticBearing",bearing);
                    block.SetVector("_HHHullCausticOptics",new Vector4(1,0,1,0));block.SetVector("_HHHullCausticIrradiance",new Vector4(1,1,0,0));
                    block.SetFloat("_SunElevation",1);block.SetFloat("_ShadowStrength",1);block.SetFloat("_Chop",1);block.SetFloat("_CausticAmount",1);
                    block.SetFloat("_HHHullCausticPreGain",1e30f);block.SetFloat("_HHHullCausticGain",0);
                    block.SetFloat("_HHHullCausticPainted",1);block.SetTexture("_CausticTex",_geometryWhite);
                    block.SetFloat("_CausticTexStrength",1);block.SetFloat("_CausticCurvatureBlend",0);item.renderer.SetPropertyBlock(block);
                }
                _camera.Render();
                Color32[] band=Read(BandTarget()); _geometricMask=new Color32[band.Length];
                for(int i=0;i<band.Length;i++) _geometricMask[i]=band[i].a>=128 && band[i].a<255
                    ? new Color32(255,255,255,255) : new Color32(0,0,0,255);
                WritePng("geometric-probe-current.png",_geometricMask);
                File.WriteAllText(Path.Combine(_output,"geometric-probe-current.txt"),"renderers="+originals.Length+"; pixels="+_geometricMask.Count(c=>c.r==255));
                File.WriteAllLines(Path.Combine(_output,"geometric-alpha-histogram.txt"),band.GroupBy(c=>c.a).OrderBy(g=>g.Key).Select(g=>g.Key+"="+g.Count()));
            }
            finally
            {
                foreach(var item in originals)item.renderer.SetPropertyBlock(item.block);
            }
            return _geometricMask;
        }

        private static (double mean,double max,int pixels) Decoded(Color32[] band,Color32[] mask)
        {
            double sum=0,max=0;int count=0;
            for(int i=0;i<band.Length;i++) if(mask[i].r==255)
            {double value=CausticMath.DecodeAlpha(band[i].a/255f);sum+=value;max=Math.Max(max,value);count++;}
            return(count>0?sum/count:0,max,count);
        }

        private static void MeasurePhysicalRung()
        {
            Color32[] mask=GeometricMask(); var samples=new List<GainProbe>();
            float lo=0,hi=256,best=0;double bestError=double.MaxValue;const double target=4d/255;
            double Sample(float gain)
            {
                Gain(gain);_camera.Render();var decoded=Decoded(Read(BandTarget()),mask);
                if(decoded.pixels==0) throw new InvalidOperationException("Heavy noon has no geometric receiving band.");
                samples.Add(new GainProbe {gain=gain,decodedMean=decoded.mean,decodedMax=decoded.max,pixels=decoded.pixels});
                double error=Math.Abs(decoded.mean-target);if(error<bestError){bestError=error;best=gain;}
                return decoded.mean;
            }
            double highMean=Sample(hi);
            for(int i=0;i<3;i++)
            {
                if(highMean<target){lo=hi;hi*=4;highMean=Sample(hi);continue;}
                float estimate=i==0 ? Mathf.Clamp((float)(target/highMean)*hi,hi*.025f,hi*.975f) : (lo+hi)*.5f;
                double mean=Sample(estimate);if(mean<target)lo=estimate;else{hi=estimate;highMean=mean;}
            }
            if(bestError>2d/255) throw new InvalidOperationException("Four physical-rung probes did not reach 4/255 within one decoded quantizer step.");
            _physicalGain=best;_veinGain=best*3;Gain(_veinGain);
            File.WriteAllText(Path.Combine(_output,"ladder","physical-rung.json"),JsonUtility.ToJson(new PhysicalProof {
                sha=System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SHA"),physicalGain=best,previewGain=_veinGain,
                targetMean=target,tolerance=2d/255,meanError=bestError,probes=samples.ToArray(),
                hour=GameServices.Clock.HourOfDay,sunBearing=Shader.GetGlobalVector("_SunDir"),
                shadowStrength=Shader.GetGlobalFloat("_ShadowStrength"),wind=_weather.Wind},true));
        }

        private static string PairName(int index)
        {
            if(index>=13)return new[]{"bearing-east-morning","bearing-west-morning","bearing-west-flat-calm"}[index-13];
            if(index==12)return "open-sea-berth";
            return(index<6?"dory":"heavy")+"-"+(((index/3)%2)==1?"underway":"berth")+"-"+new[]{"noon-calm","low-sun","blow"}[index%3];
        }

        private static Color[] FloatImage()
        {
            RenderTexture prior=RenderTexture.active;var texture=new Texture2D(1280,720,TextureFormat.RGBAFloat,false,true);
            try{RenderTexture.active=_floatTarget;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();return texture.GetPixels();}
            finally{RenderTexture.active=prior;Object.Destroy(texture);}
        }

        private static (int clipped,double max) Clipping(float gain,Color32[] mask)
        {
            bool hdr=_camera.allowHDR;
            try
            {
                _camera.allowHDR=true;_camera.targetTexture=_floatTarget;
                Gain(0);_camera.Render();Color[] before=FloatImage();
                Gain(gain);_camera.Render();Color[] after=FloatImage();int clipped=0;double max=0;
                for(int i=0;i<after.Length;i++)if(mask[i].r==255)
                {
                    max=Math.Max(max,Math.Max(after[i].r,Math.Max(after[i].g,after[i].b)));
                    if(after[i].r>1.00001f && after[i].r>before[i].r+.00001f
                        ||after[i].g>1.00001f && after[i].g>before[i].g+.00001f
                        ||after[i].b>1.00001f && after[i].b>before[i].b+.00001f)clipped++;
                }
                return(clipped,max);
            }
            finally{_camera.allowHDR=hdr;_camera.targetTexture=_target;Gain(gain);}
        }

        private static PairProof MeasurePair(int index,float gain,Color32[] before,Color32[] darkBefore,
            Color32[] after,Color32[] darkAfter,Color32[] mask)
        {
            string name=PairName(index);var decoded=Decoded(darkAfter,mask);int inside=0,outside=0,maxOutside=0,alphaOutside=0;
            double sum=0,max=0;
            for(int i=0;i<before.Length;i++)
            {
                if(darkBefore[i].r!=darkAfter[i].r||darkBefore[i].g!=darkAfter[i].g||darkBefore[i].b!=darkAfter[i].b)
                    throw new InvalidOperationException(name+": dark RGB changed with gain at pixel "+i);
                bool allowed=mask[i].r==255;
                if(!allowed && darkBefore[i].a!=darkAfter[i].a)alphaOutside++;
                int delta=Math.Max(Math.Abs(after[i].r-before[i].r),Math.Max(Math.Abs(after[i].g-before[i].g),Math.Abs(after[i].b-before[i].b)));
                if(allowed){if(delta>0)inside++;double shift=LumaShift(before[i],after[i]);sum+=shift;max=Math.Max(max,shift);}
                else if(delta>0){outside++;maxOutside=Math.Max(maxOutside,delta);}
            }
            var clip=Clipping(gain,mask);
            var proof=new PairProof {index=index,name=name,hull=index<6?"dory":"side-dragger",state=index<12&&((index/3)%2)==1?"underway at 3 m/s":"at berth",
                hour=GameServices.Clock.HourOfDay,wind=_weather.Wind,seaState=_weather.Sea,sha=System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SHA"),beforeSha=Base,
                sourceTree=System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SOURCE_TREE"),source="before base sea and band off; after shared sea, pre-quantizer gain; geometric mask diagnostic",
                bandPixels=decoded.pixels,changedInsideBand=inside,changedOutsideBand=outside,maxOutsideDelta=maxOutside,alphaChangedOutsideBand=alphaOutside,
                meanShift=decoded.pixels>0?sum/decoded.pixels:0,maxShift=max,decodedMean=decoded.mean,decodedMax=decoded.max,darkRgbUnchanged=true,
                clippedHullPixels=clip.clipped,maximumLinearHullChannel=clip.max,physicalGain=_physicalGain,rungMultiplier=gain/_physicalGain,
                tuningVeinGainBefore=0,tuningVeinGainAfter=gain,strength=_strength,chop=HullCausticLighting.LiveChop,
                width=HullCausticLighting.BandWidth(GameServices.Config,HullCausticLighting.LiveChop),widthBase=GameServices.Config.HullCausticBandBaseMeters,
                widthPerChop=GameServices.Config.HullCausticBandPerChopMeters,sunElevation=Shader.GetGlobalFloat("_SunElevation"),sunBearing=Shader.GetGlobalVector("_SunDir"),
                shadowStrength=Shader.GetGlobalFloat("_ShadowStrength"),awayFloor=GameServices.Config.HullCausticAwayFloor,f0=GameServices.Config.HullCausticFresnelF0,
                referenceElevationDegrees=GameServices.Config.HullCausticReferenceElevationDegrees,fresnelPower=GameServices.Config.HullCausticFresnelPower,
                chopDimming=GameServices.Config.HullCausticChopDimming,maxUpwardNormal=GameServices.Config.HullCausticMaxUpwardNormal,
                irradiancePower=GameServices.Config.HullCausticIrradiancePower,irradianceFloor=GameServices.Config.HullCausticIrradianceFloor,facetTiltDegrees=GameServices.Config.HullCausticFacetTiltDegrees};
            return proof;
        }

        private static void RequirePair(PairProof p)
        {
            if(p.changedOutsideBand!=0||p.alphaChangedOutsideBand!=0)throw new InvalidOperationException(p.name+": zero-outside gate failed; RGB "+p.changedOutsideBand+", alpha "+p.alphaChangedOutsideBand);
            if(p.clippedHullPixels!=0)throw new InvalidOperationException(p.name+": added light clipped "+p.clippedHullPixels+" hull pixels at rung "+p.rungMultiplier);
        }

        private static (Color32[] before,Color32[] dark,Color32[] mask) Baseline()
        {
            Source(true);_camera.Render();_camera.Render();_camera.Render();Color32[] before=Read(_target);
            Source(false);Gain(0);_camera.Render();Color32[] dark=Read(BandTarget());Color32[] mask=GeometricMask();
            return(before,dark,mask);
        }

        private static void CaptureGainLadder(int index)
        {
            var baseline=Baseline();string name=PairName(index);var rows=new List<PairProof>();
            WritePng("ladder/raw/"+name+"-before.png",baseline.before);WritePng("ladder/raw/"+name+"-mask.png",baseline.mask);
            foreach(float multiplier in new[]{1f,3f,6f,10f})
            {
                float gain=_physicalGain*multiplier;Gain(gain);_camera.Render();Color32[] after=Read(_target),dark=Read(BandTarget());
                PairProof proof=MeasurePair(index,gain,baseline.before,baseline.dark,after,dark,baseline.mask);rows.Add(proof);
                string file="ladder/raw/"+name+"-x"+multiplier.ToString("0");WritePng(file+".png",after);
                File.WriteAllText(Path.Combine(_output,file+".json"),JsonUtility.ToJson(proof,true));RequirePair(proof);
            }
            File.WriteAllText(Path.Combine(_output,"ladder",name+".json"),JsonUtility.ToJson(new PairList{pairs=rows.ToArray()},true));
            Gain(_veinGain);
        }

        private static void CaptureLadderPair(int index)
        {
            var baseline=Baseline();Gain(_veinGain);_camera.Render();Color32[] after=Read(_target),dark=Read(BandTarget());
            PairProof proof=MeasurePair(index,_veinGain,baseline.before,baseline.dark,after,dark,baseline.mask);
            if(index>=13)
            {
                var hull=_boats[1].GetComponentInChildren<IsoFacetHullRenderer>();var block=new MaterialPropertyBlock();hull.PosedMesh.GetComponent<Renderer>().GetPropertyBlock(block);
                Vector4 basis=block.GetVector("_HHHullCausticBearing");Vector3 n=hull.PosedMesh.localToWorldMatrix.MultiplyVector(_bearingRigNormal).normalized;
                proof.witnessFaceNormal=new Vector2(n.x,n.y*basis.x+n.z*basis.y).normalized;proof.witnessTriangle=_bearingTriangle;proof.headingDirUnits=hull.HeadingDirUnits;
                proof.witnessFacing=HullCausticLighting.Facing(proof.witnessFaceNormal,new Vector2(proof.sunBearing.x,proof.sunBearing.y),proof.awayFloor,proof.chop);
                proof.bearingFixture="same physical heavy face; east/west with south lean for camera visibility; 20-degree morning";
                if(index>=14&&Mathf.Abs(proof.witnessFacing-proof.awayFloor*proof.chop)>1e-6f)throw new InvalidOperationException("Away witness did not take the chop floor.");
            }
            _pairs.Add(proof);string json=JsonUtility.ToJson(proof,true);
            WritePng(proof.name+"-before.png",baseline.before);WritePng(proof.name+"-after.png",after);WritePng(proof.name+"-band-mask.png",baseline.mask);
            foreach(string suffix in new[]{"before","after","band-mask"})File.WriteAllText(Path.Combine(_output,proof.name+"-"+suffix+".json"),json);
            File.WriteAllText(Path.Combine(_output,"pixel-change-counts.json"),JsonUtility.ToJson(new PairList{pairs=_pairs.ToArray()},true));WriteBandShifts();
            if(index==0||index==6||index==7)WriteBandCrop(proof.name+"-band-crop",baseline.before,after,baseline.mask,json);
            RequirePair(proof);
        }

        private static void WriteLadderBandShifts()
        {
            var lines=new List<string> {
                "L13: fixed geometric band. Decoded mean/max are alpha-decoded weights times 255; RGB shift is absolute BT.601 code-value change.",
                "Physical gain "+_physicalGain+"; preview x3 gain "+_veinGain+". Main overlay multiplier is one.",
                "name | geometric px | decoded mean/255 | decoded max/255 | RGB mean | RGB max | RGB outside | alpha outside | max outside | sun bearing x,y | ShadowStrength | dark RGB unchanged | added-light clipped px"};
            foreach(PairProof p in _pairs.OrderBy(p=>p.index)) lines.Add(FormattableString.Invariant(
                $"{p.name} | {p.bandPixels} | {p.decodedMean*255:F4} | {p.decodedMax*255:F4} | {p.meanShift:F4} | {p.maxShift:F4} | {p.changedOutsideBand} | {p.alphaChangedOutsideBand} | {p.maxOutsideDelta} | {p.sunBearing.x:F6},{p.sunBearing.y:F6} | {p.shadowStrength:F6} | {p.darkRgbUnchanged} | {p.clippedHullPixels}"));
            File.WriteAllLines(Path.Combine(_output,"band-shift.txt"),lines);
        }

        [Serializable] private sealed class GainProbe {public float gain;public double decodedMean,decodedMax;public int pixels;}
        [Serializable] private sealed class EncodingPinSample
        {public float multiplier,gain;public int minAlphaByte,maxAlphaByte;public double decodedMean;}
        [Serializable] private sealed class EncodingPinProof
        {public string sha,scope,error,conversionObservation;public bool succeeded;public float analyticPhysicalGain;
         public int unguardedSyncAlpha,unguardedRawAlpha;public EncodingPinSample[] samples;}
        [Serializable] private sealed class PhysicalProof
        {public string sha;public float physicalGain,previewGain,hour,shadowStrength;public Vector4 sunBearing;public Vector2 wind;
         public double targetMean,tolerance,meanError;public GainProbe[] probes;}
    }
}
