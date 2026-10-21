using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // OPT-IN, evidence only. One endpoint per fresh editor launch, named by HH_WAKE_HULL and HH_WAKE_LEG;
    // with neither set (CI, and any ordinary run) it skips before it touches anything. No prior readback
    // or manual render can repair the natural observation. No synthetic scene, diagnostic pass, or fleet
    // suppression.
    public partial class WakeCentrePhotographPlayTests
    {
        const string AcceptanceRoot = "Evidence~/wake-gameplay-acceptance-v24";

        /// <summary>The dory's hull id. Her one narrow band is her designed look (owner, 2026-09-23): she is
        /// held to the stern gate and a band centred on her track running astern, never to the powered
        /// workboats' broad-sheet bars.</summary>
        const string DoryHullId = "boat.dory";

        /// <summary>The stations astern, in metres along the track her drawn stern has run, where the image
        /// gate reads the foam's centre across her track (Gate B's stations).</summary>
        static readonly float[] CentreStationsMetres = { 0.25f, 0.5f, 1f, 1.5f, 2f, 3f, 4f, 6f };

        /// <summary>The stations the centre is GATED at: clear of her transom's own churn, young enough that
        /// the swell has barely moved the foam since its birth.</summary>
        static readonly float[] CentreGatedMetres = { 1f, 2f, 3f };

        /// <summary>How far to either side of her track the image is read for foam.</summary>
        const float CentreWindowMetres = 8f;

        /// <summary>
        /// THE CENTRE-LINE BAR (proposed; the seat judges it): the band's centre may stand at most this far
        /// off her track at 1, 2 and 3 m astern. It rejects the unfixed birth as Gate B measured it (cape L02
        /// +0.935/+0.875/+0.835 m, dory L03 +1.130/+1.050/+1.030 m at 1/2/3 m) by at least 0.38 m, and it
        /// admits what way A leaves in: her own stern-wave crest, which way A does not invert (about
        /// 0.22/0.18/0.11 m at 1/2/3 m at the shipped dial), the swell's movement since the foam's birth
        /// (about 0.13 m at most at 6 m/s) and a pixel or two of mask edge (0.04 m a pixel).
        /// </summary>
        const float CentreLineBarMetres = 0.45f;

        readonly List<RenderTexture> _acceptFrames = new List<RenderTexture>();
        readonly List<string> _acceptFrameNames = new List<string>();
        string _acceptDir;
        bool _acceptWantFrame;
        int _acceptNaturalFrame = -1;

        [UnityTest]
        public IEnumerator V24_BroadGameplay_Endpoint()
        {
            string hull = System.Environment.GetEnvironmentVariable("HH_WAKE_HULL");
            string leg = System.Environment.GetEnvironmentVariable("HH_WAKE_LEG");
            if (string.IsNullOrEmpty(hull) && string.IsNullOrEmpty(leg))
                Assert.Ignore("SKIPPED, NOT RUN: an opt-in gameplay endpoint. Set HH_WAKE_HULL and HH_WAKE_LEG " +
                              "(one endpoint per fresh editor launch); CI sets neither.");
            RequireAGraphicsDevice();
            string[] hulls = { "intro", "cape_islander", "dory", "punt", "lobster_boat" };
            string[] legs = { "north", "east", "south", "west", "cw", "ccw", "slow", "accel", "peak", "decel", "creep", "rest", "fade", "helm" };
            Assert.Contains(hull, hulls, "Specify a matrix hull; no silent default");
            Assert.Contains(leg, legs, "Specify a matrix endpoint; no silent default");
            Assert.IsTrue(hull != "intro" || leg == "helm", "Intro keeps its authored pilot");
            _acceptDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", AcceptanceRoot, "runs", hull + "-" + leg,
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(_acceptDir);
            LogAssert.ignoreFailingMessages = false;
            _v24LoadedIntro = true;
            Time.timeScale = 1f;
            var load = SceneManager.LoadSceneAsync("StPeters", LoadSceneMode.Single);
            float wall = Time.realtimeSinceStartup;
            while (!load.isDone) { Assert.Less(Time.realtimeSinceStartup-wall, 90f, "Authored boot timed out"); yield return null; }
            for (int i=0; i<8; i++) yield return null;
            ShellFlow.ContinueGame();
            for (int i=0; i<8; i++) yield return null;
            Assert.AreEqual("Direct3D12", SystemInfo.graphicsDeviceType.ToString());
            Assert.IsNotNull(GameServices.Clock); Assert.IsNotNull(GameServices.Config);
            GameServices.Clock.SeekTo((1.0+(hull=="intro"?6.0:11.0)/24.0)*GameServices.Config.SecondsPerDay);
            GameServices.Clock.TimeScale = 0f; // lighting fixed; simulation/foam age continue at 30 Hz
            Color tint = Shader.GetGlobalColor("_DayNightTint"); int stable=0;
            for (int i=0; i<300 && stable<4; i++)
            {
                yield return null; Color next=Shader.GetGlobalColor("_DayNightTint");
                stable=Mathf.Abs(next.r-tint.r)+Mathf.Abs(next.g-tint.g)+Mathf.Abs(next.b-tint.b)<1e-4f?stable+1:0;
                tint=next;
            }
            Assert.GreaterOrEqual(stable,4,"Authored lighting failed to settle");
            var opening=Object.FindAnyObjectByType<ArrivalOpening>(); Assert.IsNotNull(opening);
            BoatController boat;
            if (hull=="intro")
            {
                if (opening.Boat==null)
                {
                    typeof(ArrivalOpening).GetField("_alwaysRunInEditor",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(opening,true);
                    Assert.IsTrue(opening.TryBegin());
                }
                boat=opening.Boat;
            }
            else
            {
                var switcher=Object.FindAnyObjectByType<ControlSwitcher>(); Assert.IsNotNull(switcher);
                boat=(BoatController)AcceptanceField(switcher,"_boatController"); Assert.IsNotNull(boat);
                var picker=boat.GetComponent<DevBoatPicker>(); Assert.IsNotNull(picker);
                Assert.AreSame(boat,AcceptanceField(picker,"_boat"));
                var roster=(BoatHullDef[])AcceptanceField(picker,"_roster");
                var def=roster.SingleOrDefault(x=>x!=null && x.Id=="boat."+hull);
                Assert.IsNotNull(def,"Missing AUTHORED dev roster hull"); picker.Show(def);
                Assert.Greater(opening.Route.Length,0);
            }
            Assert.IsNotNull(boat);
            var rb=boat.GetComponent<Rigidbody2D>(); Assert.IsNotNull(rb);
            var injector=boat.GetComponentInChildren<FoamInjector>(); Assert.IsNotNull(injector);
            var facet=boat.GetComponentInChildren<IsoFacetHullRenderer>(); Assert.IsNotNull(facet);
            var driver=boat.GetComponent<MeshHullDriver>(); Assert.IsNotNull(driver);
            var probe=boat.gameObject.AddComponent<V24GameplaySternObserver>(); probe.Configure(boat);
            V24RenderedSternProbe stern=null;
            var controls=boat.GetComponentsInChildren<DevBoatInput>(true);
            var controlsWas=controls.Select(x=>x.enabled).ToArray();
            bool enabled=boat.enabled, simulated=rb.simulated;
            Vector3 originalPosition=boat.transform.position; Quaternion originalRotation=boat.transform.rotation;
            Vector2 velocity=rb.linearVelocity; float angular=rb.angularVelocity;
            var follows=Resources.FindObjectsOfTypeAll<CameraFollow>().Where(x=>x!=null && x.gameObject.scene.IsValid()).ToArray();
            var targets=follows.Select(x=>x.Target).ToArray(); var smoothing=follows.Select(x=>x.Smooth).ToArray();
            var fleet=Object.FindObjectsByType<BoatController>(FindObjectsInactive.Include);
            // Check active/enabled states of every other hull and every existing sheet injector.
            var fleetStates=fleet.Where(x=>x!=boat).Select(x=>Tuple.Create(x,x.enabled,x.gameObject.activeSelf)).ToArray();
            var effects=Object.FindObjectsByType<FoamInjector>(FindObjectsInactive.Include)
                .Select(x=>Tuple.Create(x,x.enabled,x.gameObject.activeSelf)).ToArray();
            foreach(var emitter in Object.FindObjectsByType<BoatWakeEmitter>(FindObjectsInactive.Include))
            {
                var trail=(WakeTrailConfig)AcceptanceField(emitter,"_trail");
                Assert.AreEqual(0.30f,trail.FoamWindDriftFraction,"Serialized wind drift changed");
            }
            bool naturalPilot=hull=="intro", realHelm=leg=="helm", turnLeg=leg=="cw"||leg=="ccw";
            float heading=leg=="east"?90f:leg=="south"?180f:leg=="west"?270f:0f;
            int frames=leg=="slow"?45:leg=="accel"?90:leg=="peak"?180:leg=="decel"?270:leg=="creep"?315:leg=="rest"?360:leg=="fade"?720:
                turnLeg?540:naturalPilot?2700:realHelm?900:180;
            _rows.Add($"ACCEPTANCE hull={hull} leg={leg} mode={(naturalPilot?"authored-pilot":realHelm?"real-controller-helm":"prescribed-motion-render-contract")} fleet={fleet.Length} effects={effects.Length} API={SystemInfo.graphicsDeviceType} GPU={SystemInfo.graphicsDeviceName} H=source fixedClock=true captureDt={Time.captureDeltaTime}");
            try
            {
                if (!naturalPilot)
                {
                    foreach(var input in controls) input.enabled=false;
                    boat.enabled=realHelm; rb.simulated=realHelm;
                    boat.transform.position=opening.Route[0]; boat.transform.rotation=Quaternion.Euler(0,0,-heading);
                    rb.position=boat.transform.position; rb.rotation=-heading; rb.linearVelocity=Vector2.zero; rb.angularVelocity=0;
                }
                yield return FrameOn(boat.transform,new Bounds(boat.transform.position,new Vector3(80f,64f,0f)));
                RenderPipelineManager.endCameraRendering+=AcceptanceCameraFinished;
                probe.ResetMeasurements();
                // THE CENTRE LINE IN DATA (#875, way A): where the WATER draws each new deposit, by its own
                // rule, against the stern she draws, every frame of the leg.
                stern=boat.gameObject.AddComponent<V24RenderedSternProbe>(); stern.Configure(boat);
                Vector2 start=boat.transform.position;
                float maxSpeed=0, lastSpeed=0;
                for(int frame=1;frame<=frames;frame++)
                {
                    Assert.Less(Time.realtimeSinceStartup-wall,180f,"Per-case wall budget exceeded; stop, preserve evidence");
                    if (!naturalPilot && !realHelm)
                    {
                        bool ramp=leg=="slow"||leg=="accel"||leg=="peak"||leg=="decel"||leg=="creep"||leg=="rest"||leg=="fade";
                        // Equal prescribed speed makes the rendering comparison independent of hull force tuning.
                        // Real force/helm behavior is covered separately, never inferred from these cases.
                        float speed=ramp?(frame<=180?frame/30f:Mathf.Max(0f,12f-frame/30f)):6f;
                        if(turnLeg) heading+=(leg=="cw"?1:-1)*22f/30f;
                        boat.transform.rotation=Quaternion.Euler(0,0,-heading);
                        rb.linearVelocity=(Vector2)boat.transform.up*speed+GameServices.Environment.Sample().CurrentVector;
                        boat.transform.position+=(Vector3)(rb.linearVelocity/30f);
                    }
                    else if(!naturalPilot)
                    {
                        // Public control API, unchanged production physics. The final 18 s are neutral.
                        float throttle=frame<=180?1f:frame<=360?1f:0f;
                        float steer=frame<=180?0f:frame<=270?0.5f:frame<=360?-0.5f:0f;
                        boat.SetControl(throttle,steer);
                        boat.SetOarInput(throttle*(steer>0?0.25f:1f),throttle*(steer<0?0.25f:1f),false);
                    }
                    // Follow only between renders, preserving camera rotation/pipeline/grade; other effects live.
                    _cam.transform.position=new Vector3(boat.transform.position.x,boat.transform.position.y,_cam.transform.position.z);
                    bool introFinished=naturalPilot && (opening.Current==ArrivalOpening.Phase.Moored || opening.Current==ArrivalOpening.Phase.HandedOver);
                    bool retain=frame==frames || frame==90 || frame==180 || frame==270 || frame==360 || frame==540 || introFinished || (naturalPilot && frame%270==0);
                    if(retain) { _acceptWantFrame=true; _acceptFrameNames.Add($"natural-f{frame:D4}"); }
                    yield return null;
                    lastSpeed=(rb.linearVelocity-GameServices.Environment.Sample().CurrentVector).magnitude;
                    maxSpeed=Mathf.Max(maxSpeed,lastSpeed);
                    if(frame%30==0 || frame==frames)
                    {
                        _rows.Add(probe.Row(frame,lastSpeed));
                        _rows.Add($"CENTRE_DATA f={frame} {stern.Summary()}");
                        if(naturalPilot)_rows.Add($"INTRO frame={frame} phase={opening.Current}");
                        foreach(var effect in effects)
                            if(effect.Item1!=null) _rows.Add($"FLEET_POSITION f={frame} injector={effect.Item1.GetEntityId()} subject={effect.Item1==injector} stern={effect.Item1.EmissionStern.ToString("F6")} active={effect.Item1.isActiveAndEnabled}");
                    }
                    if(introFinished)break;
                }
                // Await the final NATURAL camera callback without manually rendering or reading a texture.
                float until=Time.realtimeSinceStartup+5f;
                while(_acceptWantFrame) { Assert.Less(Time.realtimeSinceStartup,until,"No final natural camera submission");yield return null; }
                // All readbacks and material controls are TERMINAL, after the natural observation.
                for(int i=0;i<_acceptFrames.Count;i++) AcceptanceSave(_acceptFrameNames[i],AcceptanceRead(_acceptFrames[i]));
                Assert.Greater(probe.Accepted,30,"Subject not accepted in the real registry slot budget");
                _rows.Add($"CENTRE_DATA final {stern.Summary()}");
                foreach(var state in fleetStates)
                {
                    Assert.IsNotNull(state.Item1,"Authored fleet hull disappeared");
                    Assert.AreEqual(state.Item2,state.Item1.enabled,"Other hull controller state changed");
                    Assert.AreEqual(state.Item3,state.Item1.gameObject.activeSelf,"Other hull activation changed");
                }
                foreach(var state in effects)
                {
                    Assert.IsNotNull(state.Item1,"Authored injector disappeared");
                    Assert.AreEqual(state.Item2,state.Item1.enabled);Assert.AreEqual(state.Item3,state.Item1.gameObject.activeSelf);
                }
                _rows.Add($"MOTION maxThroughWater={maxSpeed:F6} lastThroughWater={lastSpeed:F6} displacement={Vector2.Distance(start,boat.transform.position):F6} headingSweep={probe.SignedSweep:F6}");
                if(naturalPilot)
                {
                    Assert.Greater(probe.SweepMax-probe.SweepMin,30f,"Authored intro turns not exercised in the bounded window");
                    Assert.IsTrue(opening.Current==ArrivalOpening.Phase.Moored||opening.Current==ArrivalOpening.Phase.HandedOver||opening.Current==ArrivalOpening.Phase.Docking,"Authored intro did not finish within its bounded window");
                }
                if(leg=="east"||leg=="west") Assert.Greater(probe.OldHeadingRejected,30,"Negative heading control was not discriminating");
                if(turnLeg) Assert.Greater(Mathf.Abs(probe.SignedSweep),360f,"Continuous full turn not exercised");
                if(realHelm && !naturalPilot) { Assert.Greater(maxSpeed,0.5f,"Real helm did not accelerate");Assert.Less(lastSpeed,maxSpeed*0.5f,"Neutral did not decelerate: rest not validated"); }
                AcceptancePair(probe,leg=="rest"||leg=="fade"||realHelm,turnLeg);
                // The data gate follows the image evidence, so a data failure still leaves the terminal images.
                V24AssertTheWaterDrawsHerFoamOnHerTrack(stern,$"FAIL: centre line (data) {hull}/{leg}");
            }
            finally
            {
                RenderPipelineManager.endCameraRendering-=AcceptanceCameraFinished;
                foreach(var rt in _acceptFrames) { rt.Release();Object.DestroyImmediate(rt); }
                _acceptFrames.Clear();_acceptFrameNames.Clear();_acceptWantFrame=false;
                if(boat!=null)
                {
                    if(!naturalPilot) { boat.SetControl(0,0);boat.SetOarInput(0,0,false);boat.transform.SetPositionAndRotation(originalPosition,originalRotation);rb.linearVelocity=velocity;rb.angularVelocity=angular;rb.simulated=simulated;boat.enabled=enabled; }
                    for(int i=0;i<controls.Length;i++) if(controls[i]!=null) controls[i].enabled=controlsWas[i];
                }
                for(int i=0;i<follows.Length;i++) if(follows[i]!=null) { follows[i].Target=targets[i];follows[i].Smooth=smoothing[i]; }
                if(stern!=null)Object.Destroy(stern);
                if(probe!=null)Object.Destroy(probe);
                File.WriteAllLines(Path.Combine(_acceptDir,"measurements.txt"),_rows);
            }
        }

        void AcceptanceCameraFinished(ScriptableRenderContext context,Camera camera)
        {
            if(camera!=_cam||!_acceptWantFrame)return;
            var copy=new RenderTexture(_rt.descriptor);copy.Create();Graphics.CopyTexture(_rt,copy);
            _acceptFrames.Add(copy);_acceptWantFrame=false;_acceptNaturalFrame=Time.frameCount;
        }
        static object AcceptanceField(object value,string name)
        {
            var field=value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            Assert.IsNotNull(field,"Fixture reflection invalid: "+name);return field.GetValue(value);
        }
        object AcceptanceState()
        {
            foreach(var feature in Resources.FindObjectsOfTypeAll<IsoFacetHullFeature>())
            {
                var pass=AcceptanceField(feature,"_pass");if(pass==null)continue;
                var states=(IDictionary)AcceptanceField(pass,"_foamStates");
                foreach(DictionaryEntry entry in states) if(entry.Key.ToString()==_cam.GetEntityId().ToString())return entry.Value;
            }
            Assert.Fail("No foam history for the actual game camera");return null;
        }
        static Texture2D AcceptanceRead(RenderTexture rt)
        {
            Assert.IsTrue(rt!=null&&rt.IsCreated());var old=RenderTexture.active;
            var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBAFloat,false,true);
            try { RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply(); }
            finally { RenderTexture.active=old; }
            return tex;
        }
        static string AcceptanceHash(Texture2D tex)
        { using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(tex.GetRawTextureData())).Replace("-",""); }
        void AcceptanceSave(string name,Texture2D texture)
        {
            var pixels=texture.GetPixels();for(int i=0;i<pixels.Length;i++)pixels[i]=pixels[i].gamma;
            var encoded=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            encoded.SetPixels(pixels);encoded.Apply();File.WriteAllBytes(Path.Combine(_acceptDir,name+".png"),encoded.EncodeToPNG());
            Object.DestroyImmediate(encoded);Object.DestroyImmediate(texture);
        }
        void AcceptancePair(V24GameplaySternObserver probe,bool mayHaveFaded,bool turnLeg)
        {
            object state=AcceptanceState();
            var initialization=AcceptanceField(state,"Initialization");
            var initializationState=AcceptanceField(initialization,"State");
            var stateFlags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            string status=initializationState.GetType().GetProperty("Status",stateFlags).GetValue(initializationState).ToString();
            _rows.Add($"H_STATE status={status} generation={initializationState.GetType().GetProperty("Generation",stateFlags).GetValue(initializationState)} attempts={initializationState.GetType().GetProperty("Attempt",stateFlags).GetValue(initializationState)}; counts are CPU state observations, not new GPU interruption coverage");
            Assert.AreEqual("Ready",status,"H never confirmed this camera's history initialization");
            var handle=(RTHandle)state.GetType().GetProperty("Read").GetValue(state);
            var raw=AcceptanceRead(handle.rt);string before=AcceptanceHash(raw);
            File.WriteAllBytes(Path.Combine(_acceptDir,"terminal-coverage-freshness.exr"),raw.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
            Vector2 origin=(Vector2)AcceptanceField(state,"Origin"), residual=(Vector2)AcceptanceField(state,"DriftResidual");
            Vector4 world=Shader.GetGlobalVector("_HHFoamBufferWorld");
            Assert.Less(Vector2.Distance(FoamBuffer.DrawOrigin(origin,residual),new Vector2(world.x,world.y)),0.001f,"Wrong camera mapping");
            Assert.AreEqual(0.30f,WakeTrailConfig.Default.FoamWindDriftFraction);
            _rows.Add($"RAW world={world.ToString("F8")} origin={origin.ToString("F8")} residual={residual.ToString("F8")} naturalFrame={_acceptNaturalFrame} terminalFrame={Time.frameCount} hash={before} spriteWindDriftContract=0.30 bufferDriftVelocity={FoamInjectionRegistry.DriftVelocity.ToString("F6")}");
            foreach(var parcel in probe.Parcels())
            {
                Vector2 uv=(parcel.Item2-new Vector2(world.x,world.y))*world.w;
                Vector2 wrongUv=(parcel.Item3-new Vector2(world.x,world.y))*world.w;
                bool inside=uv.x>=0&&uv.x<=1&&uv.y>=0&&uv.y<=1;
                Color value=inside?raw.GetPixelBilinear(uv.x,uv.y):Color.clear;
                bool wrongInside=wrongUv.x>=0&&wrongUv.x<=1&&wrongUv.y>=0&&wrongUv.y<=1;
                Color wrong=wrongInside?raw.GetPixelBilinear(wrongUv.x,wrongUv.y):Color.clear;
                _rows.Add($"OLD_PARCEL birthFrame={parcel.Item1} expectedWorld={parcel.Item2.ToString("F6")} incorrectlyHullAttached={parcel.Item3.ToString("F6")} inBuffer={inside} R={value.r:F8} G={value.g:F8} wrongInBuffer={wrongInside} wrongR={wrong.r:F8} wrongG={wrong.g:F8}; compare endpoint runs and inspect surrounding EXR, do not infer persistence from a computed coordinate");
            }
            float oldScale=Time.timeScale;var last=state.GetType().GetField("LastFrame");var oldLast=last.GetValue(state);
            var materials=Resources.FindObjectsOfTypeAll<Material>().Where(m=>m.shader!=null&&m.shader.name=="HiddenHarbours/Water"&&m.HasProperty("_WakeFoamStrength")).ToArray();
            var strengths=materials.Select(m=>m.GetFloat("_WakeFoamStrength")).ToArray();
            Assert.IsTrue(strengths.Any(x=>x>0f),"No active sheet composition");
            Texture2D full=null,control=null,repeat=null;
            try
            {
                Time.timeScale=0;last.SetValue(state,Time.frameCount);
                _cam.Render();full=AcceptanceRead(_rt);
                var afterFull=AcceptanceRead(((RTHandle)state.GetType().GetProperty("Read").GetValue(state)).rt);
                Assert.AreEqual(before,AcceptanceHash(afterFull),"INVALID PAIR: full rerender changed history");Object.DestroyImmediate(afterFull);
                for(int i=0;i<materials.Length;i++)materials[i].SetFloat("_WakeFoamStrength",0f);
                _cam.Render();control=AcceptanceRead(_rt);
                for(int i=0;i<materials.Length;i++)materials[i].SetFloat("_WakeFoamStrength",strengths[i]);
                _cam.Render();repeat=AcceptanceRead(_rt);
                var after=AcceptanceRead(((RTHandle)state.GetType().GetProperty("Read").GetValue(state)).rt);
                Assert.AreEqual(before,AcceptanceHash(after),"INVALID PAIR: control changed history");Object.DestroyImmediate(after);
                Assert.AreEqual(AcceptanceHash(full),AcceptanceHash(repeat),"INVALID PAIR: other rendering changed while frozen");
                var a=full.GetPixels();var b=control.GetPixels();var isolated=new Color[a.Length];var mask=new bool[a.Length];
                for(int i=0;i<a.Length;i++)
                {
                    Color x=a[i].gamma,y=b[i].gamma;
                    mask[i]=Mathf.Max(Mathf.Abs(x.r-y.r),Mathf.Max(Mathf.Abs(x.g-y.g),Mathf.Abs(x.b-y.b)))>12f/255f;
                    isolated[i]=mask[i]?a[i]:Color.black;
                }
                Vector2 screen=_cam.WorldToScreenPoint(probe.DrawnStern);
                float ppm=_h/(2f*_cam.orthographicSize), nearest=float.MaxValue;
                int roi=0,interior=0;int erosion=Mathf.Max(2,Mathf.CeilToInt(0.15f*ppm));
                // A 0.30 m thickness test, in a local 12 m ROI. Thin crest lines cannot satisfy it.
                for(int y=erosion;y<_h-erosion;y++)for(int x=erosion;x<_w-erosion;x++)
                {
                    float distance=Vector2.Distance(new Vector2(x,y),screen)/ppm;
                    if(distance>12f||!mask[y*_w+x])continue;
                    roi++;nearest=Mathf.Min(nearest,distance);
                    bool filled=true;
                    for(int dy=-erosion;dy<=erosion&&filled;dy++)for(int dx=-erosion;dx<=erosion;dx++)
                        if(!mask[(y+dy)*_w+x+dx]) { filled=false;break; }
                    if(filled)interior++;
                }
                var iso=new Texture2D(_w,_h,TextureFormat.RGBAFloat,false,true);iso.SetPixels(isolated);iso.Apply();
                AcceptanceSave("terminal-isolated-sheet",iso);
                bool dory=probe.HullId==DoryHullId;
                _rows.Add($"FILLED sheetAreaM2={roi/(ppm*ppm):F6} thickInteriorM2={interior/(ppm*ppm):F6} nearestVisibleToMeshSternM={nearest:F6} erosionDiameterM={2*erosion/ppm:F6} metresPerPixel={1/ppm:F6} mayHaveFaded={mayHaveFaded} hull={probe.HullId} hullGate={(dory?"dory-one-band":"powered-broad-sheet")}; {(dory?"ONE_NARROW_BAND_IS_HER_DESIGNED_LOOK_THREE_BANDS_DO_NOT_APPLY":"THREE_BANDS_AND_NEW_VS_OLD_REQUIRE_IMAGE_REVIEW")}");
                // THE CENTRE LINE IN THE IMAGE (#875, owner ruling 2): the mask's centre ACROSS her track at
                // stations astern replaces the nearest pixel as the centring measure. The nearest pixel stays
                // only as "the foam starts at her stern".
                string centreFailures=AcceptanceCentreLine(probe,mask,ppm,out int gatedReached);
                bool centreGated=!mayHaveFaded&&!turnLeg;
                _rows.Add($"CENTRE_GATE applied={centreGated} bar={CentreLineBarMetres:F2}m gatedStations={string.Join(",",CentreGatedMetres.Select(m=>m.ToString("F0")))}m reached={gatedReached} window=+-{CentreWindowMetres:F0}m sign=port+ " +
                          $"reason={(mayHaveFaded?"may-have-faded":turnLeg?"turn-leg-396deg-retraces-its-own-first-lap-foam-reported-only-the-data-gate-holds-the-turn":"gated")}");
                AcceptanceSave("terminal-full",full);full=null;AcceptanceSave("terminal-no-sheet-control",control);control=null;
                // Necessary rejection gates, not a sufficient artistic/three-band acceptance oracle.
                if(!mayHaveFaded)
                {
                    if(!dory)
                    {
                        Assert.Greater(roi/(ppm*ppm),1f,"FAIL: broad sheet absent/too small; crests do not substitute");
                        Assert.Greater(interior/(ppm*ppm),0.15f,"FAIL: no filled interior; thin lines are insufficient");
                    }
                    Assert.LessOrEqual(nearest,StartsAtSternMetres,"FAIL: visible NEW sheet does not reach independently drawn stern (0.5m image gate)");
                    if(centreGated)
                    {
                        if(gatedReached==0) Assert.Fail("FAIL: centre line (image): she ran less than 1 m, so no gated station was measured");
                        if(centreFailures.Length>0)
                            Assert.Fail($"FAIL: centre line (image) {probe.HullId}: the band must be centred on her track, running astern " +
                                        $"(|centre| <= {CentreLineBarMetres:F2} m at {string.Join(", ",CentreGatedMetres.Select(m=>m.ToString("F0")))} m astern):\n{centreFailures}");
                    }
                }
                _rows.Add("PAIR_VALID true; natural images precede all readbacks/material controls; no diagnostic recovery pass; manual owner-reference acceptance still required");
            }
            finally
            {
                for(int i=0;i<materials.Length;i++)if(materials[i]!=null)materials[i].SetFloat("_WakeFoamStrength",strengths[i]);
                last.SetValue(state,oldLast);Time.timeScale=oldScale;
                Object.DestroyImmediate(raw);if(full!=null)Object.DestroyImmediate(full);if(control!=null)Object.DestroyImmediate(control);if(repeat!=null)Object.DestroyImmediate(repeat);
            }
        }

        /// <summary>
        /// THE CENTRE LINE, in the image. At each station astern along the track her drawn stern has run
        /// (carried by the buffer's drift since she passed it), the foam mask is read on the line square to
        /// her track, <see cref="CentreWindowMetres"/> either side, a pixel at a time. The band's centre is
        /// the midpoint of its outermost foam, port positive: Gate B's measure. Writes a CENTRE row per
        /// station and returns the gated stations' failures, empty when there are none;
        /// <paramref name="gatedReached"/> counts the gated stations she has run past.
        /// </summary>
        string AcceptanceCentreLine(V24GameplaySternObserver probe,bool[] mask,float ppm,out int gatedReached)
        {
            var failures=new StringBuilder();gatedReached=0;
            foreach(float metres in CentreStationsMetres)
            {
                bool gated=Array.IndexOf(CentreGatedMetres,metres)>=0;
                if(!probe.TryTrackAstern(metres,out Vector2 station,out Vector2 along))
                {
                    _rows.Add($"CENTRE station={metres:F2} reached=false gated={gated}; she has not run this far");
                    continue;
                }
                if(gated)gatedReached++;
                var port=new Vector2(-along.y,along.x);
                var runs=AcceptanceRunsAcross(mask,station,port,ppm,out int clipped);
                string runText=string.Join(" ",runs.Select(r=>$"{r.x:F3}..{r.y:F3}"));
                if(runs.Count==0)
                {
                    _rows.Add($"CENTRE station={metres:F2} reached=true gated={gated} point={station.ToString("F4")} port={port.ToString("F4")} runs=0 clipped={clipped}");
                    if(gated)failures.AppendLine($"  {metres:F2} m astern: no foam across her track within {CentreWindowMetres:F0} m");
                    continue;
                }
                float lo=runs[0].x, hi=runs[runs.Count-1].y, centre=0.5f*(lo+hi);
                _rows.Add($"CENTRE station={metres:F2} reached=true gated={gated} point={station.ToString("F4")} port={port.ToString("F4")} runs={runs.Count} [{runText}] lo={lo:F3} hi={hi:F3} width={hi-lo:F3} centre={centre:F3} clipped={clipped}");
                if(!gated)continue;
                if(clipped>0)failures.AppendLine($"  {metres:F2} m astern: INVALID CENTRE, {clipped} sample(s) of the cross-section fell off the image");
                else if(Mathf.Abs(centre)>CentreLineBarMetres)failures.AppendLine($"  {metres:F2} m astern: centre {centre:F3} m off her track (port +), foam {runText}");
            }
            return failures.ToString();
        }

        /// <summary>The runs of foam on the line through <paramref name="station"/> along
        /// <paramref name="port"/>, in metres from the station (increasing), one pixel a step; samples that
        /// fall off the image are counted in <paramref name="clipped"/> and read as no foam.</summary>
        List<Vector2> AcceptanceRunsAcross(bool[] mask,Vector2 station,Vector2 port,float ppm,out int clipped)
        {
            var runs=new List<Vector2>();clipped=0;
            float step=1f/ppm, first=0f, last=0f;bool inRun=false;
            int count=Mathf.CeilToInt(2f*CentreWindowMetres*ppm);
            for(int k=0;k<=count;k++)
            {
                float t=-CentreWindowMetres+k*step;
                Vector3 at=_cam.WorldToScreenPoint(station+port*t);
                int x=Mathf.FloorToInt(at.x), y=Mathf.FloorToInt(at.y);
                bool on=false;
                if(x<0||y<0||x>=_w||y>=_h)clipped++;
                else on=mask[y*_w+x];
                if(on) { if(!inRun) { inRun=true;first=t; } last=t; }
                else if(inRun) { inRun=false;runs.Add(new Vector2(first,last)); }
            }
            if(inRun)runs.Add(new Vector2(first,last));
            return runs;
        }
    }

    [DefaultExecutionOrder(1000)]
    public sealed class V24GameplaySternObserver : MonoBehaviour
    {
        BoatController _boat;MeshHullDriver _driver;IsoFacetHullRenderer _facet;FoamInjector _injector;
        Vector3 _local;float _lastHeading;Vector2 _integratedDrift;
        public Vector2 DrawnStern { get; private set; }
        public string HullId => _boat!=null&&_boat.Hull!=null?_boat.Hull.Id:"";
        public float SignedSweep { get; private set; }
        public float SweepMin { get; private set; }
        public float SweepMax { get; private set; }
        public int Accepted { get; private set; }
        public int OldHeadingRejected { get; private set; }
        int _slot;float _amount,_vigour;Vector2 _born;
        readonly List<Vector2> _parcels=new List<Vector2>();readonly List<Vector2> _drifts=new List<Vector2>();
        readonly List<Vector3> _hullLocalParcels=new List<Vector3>();readonly List<int> _birthFrames=new List<int>();
        // The track her drawn stern has run, and the buffer's drift integral as she passed each point.
        readonly List<Vector2> _track=new List<Vector2>();readonly List<Vector2> _trackDrift=new List<Vector2>();
        public void Configure(BoatController boat)
        {
            _boat=boat;_driver=boat.GetComponent<MeshHullDriver>();_facet=boat.GetComponentInChildren<IsoFacetHullRenderer>();_injector=boat.GetComponentInChildren<FoamInjector>();
            var mesh=boat.Hull.Visual.HullMesh;_local=new Vector3(0,-mesh.WakeSternOffsetMeters,mesh.RestingDraftMeters);
        }
        public void ResetMeasurements() { Accepted=0;OldHeadingRejected=0;SignedSweep=0;SweepMin=0;SweepMax=0;_lastHeading=ArrivalPilot.CompassOf(_boat.transform.up);_integratedDrift=Vector2.zero;_parcels.Clear();_drifts.Clear();_hullLocalParcels.Clear();_birthFrames.Clear();_track.Clear();_trackDrift.Clear(); }
        void LateUpdate()
        {
            if(_driver==null||!_driver.TryGetWakePose(out _))return;
            DrawnStern=_facet.PosedMesh.TransformPoint(_local); // No production wake projection in this oracle.
            var mesh=_boat.Hull.Visual.HullMesh;
            Vector2 wrongHeading=WakeRootMath.SternWorld(_driver.Visual.position,_driver.Visual.up,mesh.WakeSternOffsetMeters,mesh.ElevationDeg);
            if(Vector2.Distance(wrongHeading,DrawnStern)>2f/32f)OldHeadingRejected++;
            _born=_injector.EmissionStern;
            float heading=ArrivalPilot.CompassOf(_boat.transform.up);SignedSweep+=Mathf.DeltaAngle(_lastHeading,heading);_lastHeading=heading;
            SweepMin=Mathf.Min(SweepMin,SignedSweep);SweepMax=Mathf.Max(SweepMax,SignedSweep);
            _integratedDrift+=FoamInjectionRegistry.DriftVelocity*Time.deltaTime;
            _track.Add(DrawnStern);_trackDrift.Add(_integratedDrift);
            var registry=typeof(FoamInjectionRegistry).GetField("s_Live",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null) as IList;
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            _slot=-1;int packed=0;
            if(registry!=null) foreach(FoamInjector live in registry)
            {
                if(live==null)continue;
                bool has=(bool)typeof(FoamInjector).GetField("_hasPending",flags).GetValue(live);
                int stamp=(int)typeof(FoamInjector).GetField("_pendingFrame",flags).GetValue(live);
                if(!has||stamp!=Time.frameCount)continue;
                if(live==_injector) { _slot=packed;break; }
                packed++;
            }
            bool pending=(bool)typeof(FoamInjector).GetField("_hasPending",flags).GetValue(_injector);
            int frame=(int)typeof(FoamInjector).GetField("_pendingFrame",flags).GetValue(_injector);
            var injection=(FoamInjection)typeof(FoamInjector).GetField("_pending",flags).GetValue(_injector);
            _amount=injection.Amount;_vigour=injection.Vigour;
            if(pending&&frame==Time.frameCount&&_slot>=0&&_slot<FoamBuffer.MaxInjectors)Accepted++;
        }
        /// <summary>The point <paramref name="metres"/> astern along the track her drawn stern has run,
        /// carried by the buffer's drift since she passed it (so it sits where foam born there sits now), and
        /// her direction of travel there. False while she has not yet run that far.</summary>
        public bool TryTrackAstern(float metres,out Vector2 point,out Vector2 along)
        {
            point=DrawnStern;along=Vector2.zero;
            float run=0f;
            for(int i=_track.Count-1;i>0;i--)
            {
                Vector2 a=_track[i], b=_track[i-1];
                float segment=Vector2.Distance(a,b);
                if(segment<1e-5f)continue;
                if(run+segment>=metres)
                {
                    float f=(metres-run)/segment;
                    Vector2 drift=Vector2.Lerp(_trackDrift[i],_trackDrift[i-1],f);
                    point=Vector2.Lerp(a,b,f)+(_integratedDrift-drift);
                    along=(a-b)/segment;
                    return true;
                }
                run+=segment;
            }
            return false;
        }
        public string Row(int frame,float speed)
        {
            if(frame==90||frame==180) { _parcels.Add(_born);_drifts.Add(_integratedDrift);_hullLocalParcels.Add(_boat.transform.InverseTransformPoint(_born));_birthFrames.Add(frame); }
            string old="";
            for(int i=0;i<_parcels.Count;i++) old+=$" parcel{i}World={(_parcels[i]+_integratedDrift-_drifts[i]).ToString("F6")}";
            return $"FRAME f={frame} speed={speed:F6} drawnMeshStern={DrawnStern.ToString("F6")} birth={_born.ToString("F6")} oldHeadingRejected={OldHeadingRejected} signedSweep={SignedSweep:F3} slot={_slot} accepted={Accepted} amount={_amount:F8} vigourGate={_vigour:F6} driftIntegral={_integratedDrift.ToString("F6")}{old}";
        }
        public IEnumerable<Tuple<int,Vector2,Vector2>> Parcels()
        {
            for(int i=0;i<_parcels.Count;i++)yield return Tuple.Create(_birthFrames[i],_parcels[i]+_integratedDrift-_drifts[i],(Vector2)_boat.transform.TransformPoint(_hullLocalParcels[i]));
        }
    }
}
