using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    public partial class WakeCentrePhotographPlayTests
    {
        const float AttachmentToleranceMetres = 2f / 32f;

        /// <summary>"The foam starts at her stern": how far along her track, ahead or astern, the water
        /// may draw new foam from the stern she draws. The image gate's nearest-pixel bar.</summary>
        const float StartsAtSternMetres = 0.5f;

        /// <summary>Her tide's rise at the shot hour, 11:00, as Gate B measured it on L02 and L03 (the
        /// drawn hull stood 1.16 m BELOW her plan-frame root).</summary>
        const float GateBTideRiseMetres = -1.16f;

        float _v24PreviousCaptureDelta;
        bool _v24LoadedIntro;
        readonly HashSet<string> _v24RendererPaths = new HashSet<string>();

        [UnitySetUp]
        public IEnumerator V24FixedFrameSetup()
        {
            _v24PreviousCaptureDelta = Time.captureDeltaTime;
            if (TestContext.CurrentContext.Test.Name.StartsWith("V24_")) Time.captureDeltaTime = 1f/30f;
            yield return null;
        }

        byte[] ShootNamed(Transform wakeRoot, FoamInjector injector, string family)
        {
            if (_emitter == null) _emitter = Object.FindFirstObjectByType<BoatWakeEmitter>();
            if (_emitter != null) _emitter.enabled = false;
            foreach (Transform child in wakeRoot)
            {
                var r = child.GetComponent<Renderer>();
                if (r != null && r.sharedMaterial != null)
                {
                    string path = $"RENDERER family={child.name} shader={r.sharedMaterial.shader.name}";
                    if (_v24RendererPaths.Add(path)) _rows.Add(path);
                }
                if (r != null) r.enabled = child.gameObject.activeSelf && (family == "ALL" || child.name == family);
            }
            if (family != "ALL")
            {
                if (injector != null) injector.enabled = false;
                FoamInjectionRegistry.PublishLookStrength(0f);
            }
            return Capture();
        }

        [UnityTest]
        public IEnumerator V24_MeshPoseOracle_FleetHeadingsPitchTideAndSwaps()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion();
            yield return SetHour(ShotHour);
            Assert.IsTrue(TryLegAnchors(1, out var anchors, out _), "No verified deep-water anchor");
            var root = new GameObject("v24-oracle-fleet");
            _spawned.Add(root);
            root.transform.position = anchors[0];
            var boat = root.AddComponent<BoatController>();
            boat.SetLocalSeabedDepth(40f);
            var rb = root.GetComponent<Rigidbody2D>();
            rb.simulated = false;
            boat.enabled = false;
            var picker = root.AddComponent<DevBoatPicker>();
            picker.Configure(System.Array.Empty<BoatHullDef>(), boat, null, null);
            yield return FrameOn(root.transform, new Bounds(root.transform.position, new Vector3(40f,40f,0f)));
            var failures = new StringBuilder();
            int checkedHulls = 0, samples = 0, rejectedOldHeading = 0, rejectedTideFrame = 0;
            float maxPose = 0f, maxAcross = 0f, maxAlong = 0f, maxUnfixedAcross = 0f;
            float pixelsPerMetre = _h / (2f * _cam.orthographicSize);
#if UNITY_EDITOR
            foreach (string guid in AssetDatabase.FindAssets("t:BoatHullDef", new[]{"Assets/_Project/Data/Boats"}))
            {
                var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (hull.Visual == null || !hull.Visual.HasHullMesh()) continue;
                picker.Show(hull);
                yield return null;
                var driver = root.GetComponent<MeshHullDriver>();
                var facet = root.GetComponentInChildren<IsoFacetHullRenderer>();
                var injector = root.GetComponentInChildren<FoamInjector>();
                Assert.IsNotNull(driver, hull.Id); Assert.IsNotNull(facet, hull.Id); Assert.IsNotNull(injector, hull.Id);
                var motion = root.GetComponent<BoatWaveMotion>();
                if (motion != null) motion.enabled = false;
                var def = hull.Visual.HullMesh;
                // The stern station is verified against the original rig's loft by WakeSternOffsetRigTests.
                // The oracle transforms the authored point through the ACTUAL GPU mesh transform,
                // never through the wake/presenter projection helper being tested.
                var local = new Vector3(0f, -def.WakeSternOffsetMeters, def.RestingDraftMeters);
                float hullAcross = 0f, hullAlong = 0f, hullUnfixed = 0f;
                int hullPoses = 0;
                foreach (float heading in new[]{0f,45f,90f,135f,180f,225f,270f,315f,259.8f,195f})
                foreach (float pitch in new[]{-6f,0f,6f})
                foreach (float tide in new[]{GateBTideRiseMetres,-0.7f,0.9f})
                {
                    root.transform.rotation = Quaternion.Euler(0f,0f,-heading);
                    driver.Visual.position = root.transform.position + Vector3.up * tide;
                    driver.RockFrame = -1;
                    driver.SetStormRock(1f, 2f, pitch);
                    driver.SetDisplacedHeaveMeters(0.25f);
                    driver.Drive(); facet.ApplyPose();
                    Vector2 oracle = facet.PosedMesh.TransformPoint(local);
                    Assert.IsTrue(driver.TryGetWakePose(out HullWakePose pose));
                    float poseError = Vector2.Distance(oracle, pose.DrawnStern);
                    // ⭐ WHERE THE WATER DRAWS the foam laid now (#875, way A): by the water's own rule, never
                    // the injector's inversion, against the stern the hull DRAWS, across her track and along
                    // it. The mirror this replaces added the tide's rise back to the injector's own point and
                    // so passed the unfixed code (0.00028 m on Gate B's L02, whose foam drew 1.16 m off her
                    // track). SWELL ONLY: this sweep is synchronous, so the stern-wave slots still hold the
                    // last LateUpdate's roots; the ramp and the intro measure that term frame by frame.
                    Vector2 born = injector.EmissionStern;
                    V24TrackOffsets(V24WaterDrawsSwell(born), pose.DrawnStern, pose.Heading,
                                    out float across, out float along);
                    // The unfixed birth (f5583fb3) at the same pose. Every tide swept has risen or fallen,
                    // so the same bars must reject it at every pose.
                    Vector2 unfixed = V24UnfixedTideFrameBirth(in pose);
                    V24TrackOffsets(V24WaterDrawsSwell(unfixed), pose.DrawnStern, pose.Heading,
                                    out float unfixedAcross, out float unfixedAlong);
                    if (unfixedAcross > AttachmentToleranceMetres || unfixedAlong > StartsAtSternMetres)
                        rejectedTideFrame++;
                    Vector2 sabotaged = WakeRootMath.SternWorld(driver.Visual.position, driver.Visual.up,
                        def.WakeSternOffsetMeters, def.ElevationDeg);
                    if (heading == 270f && Vector2.Distance(sabotaged, oracle) > AttachmentToleranceMetres)
                        rejectedOldHeading++;
                    hullAcross = Mathf.Max(hullAcross, across); hullAlong = Mathf.Max(hullAlong, along);
                    hullUnfixed = Mathf.Max(hullUnfixed, unfixedAcross); maxPose = Mathf.Max(maxPose,poseError);
                    maxAcross = Mathf.Max(maxAcross, across); maxAlong = Mathf.Max(maxAlong, along);
                    maxUnfixedAcross = Mathf.Max(maxUnfixedAcross, unfixedAcross); samples++; hullPoses++;
                    if (poseError > AttachmentToleranceMetres || across > AttachmentToleranceMetres || along > StartsAtSternMetres)
                        failures.AppendLine($"{hull.Id} heading={heading} pitch={pitch} tide={tide} pose={poseError:F5}m across={across:F5}m along={along:F5}m");
                }
                checkedHulls++;
                _rows.Add($"ORACLE {hull.Id} {hullPoses} poses; max across={hullAcross:F5}m ({hullAcross*pixelsPerMetre:F4} px) max along={hullAlong:F5}m; unfixed (f5583fb3) max across={hullUnfixed:F4}m; mesh={def.name}");
            }
#endif
            _rows.Add($"ORACLE SUMMARY hulls={checkedHulls} samples={samples} maxPose={maxPose:F6}m maxAcross={maxAcross:F6}m maxAlong={maxAlong:F6}m acrossBar={AttachmentToleranceMetres}m alongBar={StartsAtSternMetres}m unfixedRejected={rejectedTideFrame}/{samples} unfixedMaxAcross={maxUnfixedAcross:F4}m pixelsPerMetre={pixelsPerMetre:F5} oldHeadingRejected={rejectedOldHeading}");
            WriteMeasurements("v24-pose-oracle");
            Assert.GreaterOrEqual(checkedHulls,4);
            Assert.GreaterOrEqual(rejectedOldHeading,checkedHulls);
            Assert.AreEqual(samples, rejectedTideFrame,
                "The bars must reject the unfixed tide-frame birth (f5583fb3) at every pose: every tide swept has risen or fallen");
            Assert.GreaterOrEqual(maxUnfixedAcross, 1f,
                "At 11:00's rise the unfixed birth draws about a metre off her track on an east or west leg (Gate B L02/L03: 1.16 m)");
            Assert.IsEmpty(failures.ToString(), failures.ToString());
        }

        [UnityTest]
        public IEnumerator V24_Cape_ContinuousAccelerationDecelerationAndRest()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion();
            yield return SetHour(ShotHour);
            Assert.IsTrue(TryLegAnchors(1,out var anchors,out _));
            var hull=LoadAsset<BoatHullDef>(CapeHullPath);
            var visual=LoadAsset<BoatVisualDef>(CapeVisualPath);
            var start=anchors[0]-Vector2.right*36f;
            var she=Spawn("v24-ramp",hull,visual,false,new Leg("ramp",90f,0f),start);
            she.Boat.enabled=false; she.Rb.simulated=false;
            var probe=she.Go.AddComponent<V24RenderedSternProbe>(); probe.Configure(she.Boat);
            yield return FrameOn(she.Go.transform,new Bounds(anchors[0],new Vector3(115f,80f,0f)));
            Time.timeScale=1f;
            for(int frame=1;frame<=720;frame++)
            {
                float speed=frame<=180 ? frame/15f : Mathf.Max(0f,24f-frame/15f);
                Vector2 current=GameServices.Environment.Sample().CurrentVector;
                she.Rb.linearVelocity=Vector2.right*speed+current;
                she.Go.transform.position+=(Vector3)(she.Rb.linearVelocity/30f);
                yield return null;
                if(frame==90 || frame==180 || frame==270 || frame==360 || frame==720)
                {
                    Time.timeScale=0f;
                    Transform rig=FindWakeRoot(she.Go.name); Assert.IsNotNull(rig);
                    byte[] complete=ShootNamed(rig,she.Injector,"ALL");
                    SavePlate($"v24-ramp-frame-{frame}-speed-{speed}-complete.png",complete);
                    int live=0;
                    foreach(Transform child in rig)
                        if(child.gameObject.activeSelf && (child.name=="crest" || child.name=="sternRoll")) live++;
                    _rows.Add($"RAMP frame={frame} speed={speed} liveCrests={live} {probe.Summary()} metresPerPixel={2f*_cam.orthographicSize/_h:F6}");
                    if(frame==720) Assert.AreEqual(0,live,"Twelve seconds at rest must exhaust the wake");
                    RestoreRig(rig,she.Injector); Time.timeScale=1f;
                }
            }
            WriteMeasurements("v24-ramp");
            V24AssertTheWaterDrawsHerFoamOnHerTrack(probe, "ramp");
        }

        // ---- ⭐ THE WATER'S RULE, over the water's own inputs (#875, way A) -------------------------------
        // HiddenHarboursWater.shader, vertDisplaced: a foam deposit at ground point g draws at
        //     g + up * ( swell(g) * exaggeration * fade(g) + sternWaves(g) * fade(g) )
        // with fade(g) = ShoreFade01(level - bed(g), band) and NO tide term. Written out here from the
        // published wave field, the published displaced sea, the stern-wave slots as uploaded to the shader
        // and the drawing water's dials: never from FoamInjector's inversion, which is what is under test.

        /// <summary>The water's shore fade at a ground point: 1 in open water, 0 where the bed is bare.
        /// </summary>
        internal static float V24WaterFade(Vector2 ground, in DisplacedSeaState sea)
        {
            float level = GameServices.Environment != null && GameServices.Clock != null
                ? GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds) : 0f;
            float bed = GameServices.TidalTerrain != null
                ? GameServices.TidalTerrain.ElevationAt(ground) : float.NegativeInfinity;
            return ShoreFadeMath.Fade01(level - bed, sea.ShoreFadeBandMeters);
        }

        /// <summary>The swell's drawing lift at a ground point: the published field sampled by the
        /// shader's twin at the drawn sea's frequency scale and the fetch envelope, times the exaggeration,
        /// times the shore fade. This is the term way A inverts.</summary>
        internal static float V24WaterSwellLift(Vector2 ground)
        {
            if (!DisplacedSea.TryGet(out DisplacedSeaState sea)) return 0f;
            PackedWaveField field = WaveFieldBridge.ReadPublishedField();
            float swell = WaveFieldBridge.ShaderTwinSample(ground, in field, sea.FreqScale,
                GameServices.FetchEnvelopeAt(ground)).Height;
            return swell * sea.Exaggeration * V24WaterFade(ground, in sea);
        }

        /// <summary>Where the water draws a deposit laid at <paramref name="ground"/>, the stern-wave term
        /// left out.</summary>
        internal static Vector2 V24WaterDrawsSwell(Vector2 ground) => ground + Vector2.up * V24WaterSwellLift(ground);

        /// <summary>The stern waves' drawing lift at a ground point (<c>WakeLiftHeight</c> times the
        /// fade), split into the train rooted at <paramref name="ownRoot"/> and every other hull's. Read
        /// from the slots as uploaded to the shader, with the drawing water's dials. Way A does not invert
        /// this term: her own train stands a crest of up to the dial times her gate at her transom.</summary>
        internal static void V24WaterWakeLift(Vector2 ground, Vector2 ownRoot, float amplitudeMetres,
                                              float decayMetres, out float own, out float others)
        {
            own = 0f; others = 0f;
            if (amplitudeMetres <= 0f || decayMetres <= 0f) return;
            if (!DisplacedSea.TryGet(out DisplacedSeaState sea)) return;
            Vector4[] roots = Shader.GetGlobalVectorArray(FoamShaderIds.WakeLiftRoot);
            Vector4[] shapes = Shader.GetGlobalVectorArray(FoamShaderIds.WakeLiftShape);
            if (roots == null || shapes == null) return;
            float fade = V24WaterFade(ground, in sea);
            int n = Mathf.Min(roots.Length, shapes.Length);
            for (int i = 0; i < n; i++)
            {
                Vector4 shape = shapes[i];
                if (shape.x <= 0f || shape.y <= 0f) continue;
                var root = new Vector2(roots[i].x, roots[i].y);
                float height = fade * WakeLiftMath.HeightAt(ground, root, new Vector2(roots[i].z, roots[i].w),
                                                            amplitudeMetres * shape.x, shape.y, shape.z, decayMetres);
                if ((root - ownRoot).sqrMagnitude <= 1e-6f) own += height; else others += height;
            }
        }

        /// <summary>The stern-wave dials of the water that draws: every material on this scene's renderers
        /// that carries them (they are material properties; no code sets them). With several, the largest
        /// amplitude, so a bar built on it is never tighter than the sea drawn. Returns how many materials
        /// were read; with none, the shipped dials.</summary>
        internal static int V24WaterWakeDials(out float amplitudeMetres, out float decayMetres)
        {
            amplitudeMetres = LiftShippedMetres; decayMetres = LiftDecayMetres;
            int found = 0;
            var seen = new HashSet<Material>();
            foreach (Renderer r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (r == null || !r.gameObject.scene.IsValid()) continue;
                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;
                foreach (Material m in mats)
                {
                    if (m == null || !m.HasProperty(LiftAmplitudeProp) || !seen.Add(m)) continue;
                    float amplitude = m.GetFloat(LiftAmplitudeProp);
                    if (found == 0 || amplitude > amplitudeMetres)
                    {
                        amplitudeMetres = amplitude;
                        decayMetres = m.GetFloat(LiftDecayProp);
                    }
                    found++;
                }
            }
            return found;
        }

        /// <summary>The UNFIXED birth, f5583fb3 <c>FoamInjector.SternWorld</c>: the drawn transom LESS the
        /// tide's rise, then the swell lift inverted in three steps. Kept so the bars can be seen to
        /// reject it.</summary>
        internal static Vector2 V24UnfixedTideFrameBirth(in HullWakePose pose)
        {
            Vector2 datum = pose.DrawnStern - Vector2.up * pose.TideRise;
            Vector2 at = datum;
            for (int i = 0; i < 3; i++) at = datum - Vector2.up * V24WaterSwellLift(at);
            return at;
        }

        /// <summary>A drawn point's offset from the drawn stern ACROSS her track (the centring) and ALONG
        /// it (where the foam starts).</summary>
        internal static void V24TrackOffsets(Vector2 drawnAt, Vector2 drawnStern, Vector2 heading,
                                             out float across, out float along)
        {
            Vector2 h = heading.sqrMagnitude > 0f ? heading.normalized : Vector2.up;
            Vector2 error = drawnAt - drawnStern;
            across = Mathf.Abs(h.x * error.y - h.y * error.x);
            along = Mathf.Abs(Vector2.Dot(h, error));
        }

        /// <summary>The frame-by-frame bars, shared by the ramp and the intro. The swell rule is what way A
        /// fixes, so it takes the two-pixel bar; her own stern wave, which way A leaves in, is allowed its
        /// crest height at the dial on top of it.</summary>
        static void V24AssertTheWaterDrawsHerFoamOnHerTrack(V24RenderedSternProbe probe, string what)
        {
            string data = $"{what}: {probe.Summary()}";
            Assert.Greater(probe.Samples, 0, "The probe measured no frame. " + data);
            Assert.LessOrEqual(probe.MaxPoseError, AttachmentToleranceMetres,
                "The wake pose's stern must be the stern the mesh draws. " + data);
            Assert.LessOrEqual(probe.MaxAcrossSwell, AttachmentToleranceMetres,
                "The water must draw new foam on her track. " + data);
            Assert.LessOrEqual(probe.MaxAlongSwell, StartsAtSternMetres,
                "The water must draw new foam at her stern. " + data);
            Assert.LessOrEqual(probe.MaxAcrossFull, AttachmentToleranceMetres + probe.LiftAmplitudeMetres,
                "With her own stern wave, new foam may stand at most the wave's crest off her track. " + data);
            Assert.LessOrEqual(probe.MaxAlongFull, StartsAtSternMetres,
                "With her own stern wave, the water must still draw new foam at her stern. " + data);
        }

        [UnityTest]
        public IEnumerator V24_Cape_CardinalsAndBothTurns()
        {
            RequireAGraphicsDevice();
            yield return Photograph("v24-cape", CapeHullPath, CapeVisualPath, false);
        }

        [UnityTest]
        public IEnumerator V24_Dory_CardinalsAndBothTurns()
        { RequireAGraphicsDevice(); yield return Photograph("v24-dory",DoryHullPath,DoryVisualPath,false); }

        [UnityTest]
        public IEnumerator V24_Punt_CardinalsAndBothTurns()
        { RequireAGraphicsDevice(); yield return Photograph("v24-punt","Assets/_Project/Data/Boats/Punt.asset","Assets/_Project/Data/Boats/Visuals/PuntIsoBasic.asset",false); }

        [UnityTest]
        public IEnumerator V24_AuthoredIntroRoute_RenderedSternThroughScriptedTurns()
        {
            RequireAGraphicsDevice();
            LogAssert.ignoreFailingMessages=true;
            _v24LoadedIntro=true;
            yield return SceneManager.LoadSceneAsync("StPeters",LoadSceneMode.Single);
            for(int i=0;i<8;i++) yield return null;
            yield return SetHour(6f);
            Time.timeScale=1f;
            var opening=Object.FindAnyObjectByType<ArrivalOpening>();
            Assert.IsNotNull(opening,"Authored StPeters opening missing");
            if(opening.Boat==null)
            {
                typeof(ArrivalOpening).GetField("_alwaysRunInEditor",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(opening,true);
                Assert.IsTrue(opening.TryBegin(),"Authored intro could not start");
            }
            var boat=opening.Boat;
            var probe=boat.gameObject.AddComponent<V24RenderedSternProbe>();
            probe.Configure(boat);
            float start=Time.realtimeSinceStartup;
            int lastLeg=-1, plates=0;
            var route=opening.Route;
            while(Time.realtimeSinceStartup-start<180f && opening.Current!=ArrivalOpening.Phase.Moored)
            {
                yield return null;
                int nearest=0;float distance=float.MaxValue;
                for(int i=0;i<route.Length;i++)
                { float d=Vector2.Distance(boat.transform.position,route[i]); if(d<distance){distance=d;nearest=i;} }
                if(nearest!=lastLeg)
                {
                    lastLeg=nearest;
                    Time.timeScale=0f;
                    yield return FrameOn(boat.transform,new Bounds(boat.transform.position,new Vector3(55f,42f,0f)));
                    SavePlate($"v24-intro-mark-{nearest}.png",Capture());
                    _rows.Add($"INTRO mark={nearest} heading={ArrivalPilot.CompassOf(boat.transform.up):F3} speed={boat.Velocity.magnitude:F3} phase={opening.Current} clock={GameServices.Clock.TotalSeconds} frame={Time.frameCount} ortho={_cam.orthographicSize}");
                    plates++;Time.timeScale=1f;
                }
                if(plates>=route.Length && probe.HeadingSweep>30f) break;
            }
            _rows.Add($"INTRO headingSweep={probe.HeadingSweep:F3} {probe.Summary()} plates={plates} phase={opening.Current}");
            WriteMeasurements("v24-authored-intro");
            Assert.Greater(probe.Samples,30);
            Assert.Greater(probe.HeadingSweep,30f,"Intro did not execute its turns");
            Assert.GreaterOrEqual(plates,3,"Intro did not reach the approach marks");
            Assert.Less(probe.MaxPoseError,AttachmentToleranceMetres);
            V24AssertTheWaterDrawsHerFoamOnHerTrack(probe, "intro");
        }

        [UnityTest]
        public IEnumerator V24_Lobster_CardinalsAndBothTurns()
        { RequireAGraphicsDevice(); yield return Photograph("v24-lobster","Assets/_Project/Data/Boats/LobsterBoat.asset","Assets/_Project/Data/Boats/Visuals/LobsterBoatIso.asset",false); }

        [UnityTest]
        public IEnumerator V24_Cape_SpeedAndAgeResponse()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(); yield return SetHour(ShotHour);
            Assert.IsTrue(TryLegAnchors(4,out var starts,out _));
            var hull=LoadAsset<BoatHullDef>(CapeHullPath);
            var visual=LoadAsset<BoatVisualDef>(CapeVisualPath);
            float previousAlpha=-1f, previousScale=-1f;
            var errors=new StringBuilder();
            int index=0;
            foreach(float speed in new[]{0.5f,2f,6f,12f})
            {
                var leg=new Leg("speed-"+speed,90f,0f);
                var she=Spawn("v24-response",hull,visual,false,leg,starts[index++]);
                she.Boat.enabled=false; she.Rb.simulated=false;
                yield return FrameOn(she.Go.transform,new Bounds(starts[index-1]+Vector2.right*12f,new Vector3(95f,65f,0f)));
                Time.timeScale=1f;
                var current=GameServices.Environment.Sample().CurrentVector;
                for(int frame=0;frame<90;frame++)
                {
                    she.Rb.linearVelocity=Vector2.right*speed+current;
                    she.Go.transform.position+=(Vector3)(she.Rb.linearVelocity/30f);
                    yield return null;
                }
                Time.timeScale=0f;
                Transform rig=FindWakeRoot(she.Go.name);
                Assert.IsNotNull(rig);
                float alpha=0f, scale=0f;
                foreach(Transform child in rig)
                    if(child.gameObject.activeSelf && (child.name=="crest" || child.name=="sternRoll"))
                    {
                        var sr=child.GetComponent<SpriteRenderer>();
                        alpha=Mathf.Max(alpha,sr.color.a);scale=Mathf.Max(scale,child.localScale.x);
                    }
                byte[] complete=ShootNamed(rig,she.Injector,"ALL");
                byte[] bare=ShootNamed(rig,she.Injector,"NONE");
                SavePlate($"v24-response-speed-{speed}-complete.png",complete);
                SavePlate($"v24-response-speed-{speed}-bare.png",bare);
                WorstChannelDelta(complete,bare,out int pixels);
                _rows.Add($"RESPONSE speed={speed} frame=90 time={GameServices.Clock.TotalSeconds} crestPeakAlpha={alpha:F6} crestLength={scale:F6} visiblePixels={pixels} metresPerPixel={2f*_cam.orthographicSize/_h:F6}");
                if(alpha<=previousAlpha || scale<=previousScale) errors.AppendLine($"speed {speed}: alpha {alpha} <= {previousAlpha} or scale {scale} <= {previousScale}");
                previousAlpha=alpha;previousScale=scale;
                if(speed==12f)
                {
                    RestoreRig(rig,she.Injector); Time.timeScale=1f;
                    she.Rb.linearVelocity=current;
                    for(int frame=0;frame<360;frame++)
                    {
                        she.Go.transform.position+=(Vector3)(current/30f);
                        yield return null;
                        if(frame==89 || frame==359)
                        {
                            Time.timeScale=0f;
                            byte[] age=ShootNamed(rig,she.Injector,"ALL");
                            SavePlate($"v24-response-release-age-{(frame+1)/30}-complete.png",age);
                            int live=0;
                            foreach(Transform child in rig)
                                if(child.gameObject.activeSelf && (child.name=="crest" || child.name=="sternRoll")) live++;
                            _rows.Add($"RELEASE age={(frame+1)/30} liveCrests={live}");
                            if(frame==359 && live!=0) errors.AppendLine($"Crests remained after 12 s at zero through-water speed: {live}");
                            RestoreRig(rig,she.Injector);Time.timeScale=1f;
                        }
                    }
                }
                RestoreRig(rig,she.Injector); Time.timeScale=1f;
                _spawned.Remove(she.Go);Object.Destroy(she.Go);yield return null;
            }
            WriteMeasurements("v24-speed-age");
            Assert.IsEmpty(errors.ToString(),errors.ToString());
        }
    }

    /// <summary>
    /// WHERE THE WATER DRAWS NEW FOAM, frame by frame (#875, way A). It runs after the injector (30), so it
    /// reads this frame's deposit (<see cref="FoamInjector.TryTakeInjection"/> does not consume it) and the
    /// stern-wave slots the injector has just uploaded. It puts that deposit where the WATER draws it, by the
    /// water's own rule, and measures it against the stern the hull draws, across her track and along it:
    /// with the swell alone (the term way A inverts), and with her own stern wave as well (which it does
    /// not). The mirror this replaces added the tide's rise back to the injector's own point, so it passed
    /// the unfixed code.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class V24RenderedSternProbe : MonoBehaviour
    {
        MeshHullDriver _driver;
        IsoFacetHullRenderer _facet;
        FoamInjector _injector;
        Vector3 _local;
        float _firstHeading;
        float _liftAmplitude;
        float _liftDecay;
        public int Samples { get; private set; }
        public int Deposits { get; private set; }
        public int WaterMaterials { get; private set; }
        public float LiftAmplitudeMetres => _liftAmplitude;
        public float MaxPoseError { get; private set; }
        /// <summary>The swell rule: where the water draws new foam, her stern wave left out.</summary>
        public float MaxAcrossSwell { get; private set; }
        public float MaxAlongSwell { get; private set; }
        /// <summary>The swell and her own stern wave: the water's rule for a hull alone on the sea.</summary>
        public float MaxAcrossFull { get; private set; }
        public float MaxAlongFull { get; private set; }
        /// <summary>Other hulls' stern waves where her foam is born. Reported, not asserted.</summary>
        public float MaxOthersLift { get; private set; }
        /// <summary>Where the water draws the UNFIXED birth (f5583fb3). Reported, never asserted: it misses
        /// only where her tide has risen or fallen, and a hull without HullTideRide rides no tide.</summary>
        public float MaxUnfixedAcross { get; private set; }
        public float MaxTideRise { get; private set; }
        public float HeadingSweep { get; private set; }
        public void Configure(BoatController boat)
        {
            _driver=boat.GetComponent<MeshHullDriver>();
            _facet=boat.GetComponentInChildren<IsoFacetHullRenderer>();
            _injector=boat.GetComponentInChildren<FoamInjector>();
            var def=boat.Hull.Visual.HullMesh;
            _local=new Vector3(0f,-def.WakeSternOffsetMeters,def.RestingDraftMeters);
            _firstHeading=ArrivalPilot.CompassOf(boat.transform.up);
            WaterMaterials=WakeCentrePhotographPlayTests.V24WaterWakeDials(out _liftAmplitude,out _liftDecay);
        }
        public string Summary() =>
            $"samples={Samples} deposits={Deposits} maxPose={MaxPoseError:F6}m " +
            $"swell: across={MaxAcrossSwell:F6}m along={MaxAlongSwell:F6}m; " +
            $"with her stern wave: across={MaxAcrossFull:F6}m along={MaxAlongFull:F6}m " +
            $"(dial {_liftAmplitude:F3}m/{_liftDecay:F1}m from {WaterMaterials} water material(s)); " +
            $"other hulls' waves at birth={MaxOthersLift:F4}m; " +
            $"unfixed (f5583fb3) across={MaxUnfixedAcross:F4}m at |tide rise| up to {MaxTideRise:F4}m";
        void LateUpdate()
        {
            if(_driver==null || _facet==null || _injector==null || !_driver.TryGetWakePose(out var pose)) return;
            Vector2 oracle=_facet.PosedMesh.TransformPoint(_local);
            MaxPoseError=Mathf.Max(MaxPoseError,Vector2.Distance(oracle,pose.DrawnStern));
            // The deposit laid THIS frame when there is one; otherwise the point it would be laid at.
            Vector2 born;
            if(_injector.TryTakeInjection(out FoamInjection injection)) { born=injection.To; Deposits++; }
            else born=_injector.EmissionStern;
            float swell=WakeCentrePhotographPlayTests.V24WaterSwellLift(born);
            WakeCentrePhotographPlayTests.V24WaterWakeLift(born,born,_liftAmplitude,_liftDecay,out float own,out float others);
            WakeCentrePhotographPlayTests.V24TrackOffsets(born+Vector2.up*swell,pose.DrawnStern,pose.Heading,out float across,out float along);
            MaxAcrossSwell=Mathf.Max(MaxAcrossSwell,across); MaxAlongSwell=Mathf.Max(MaxAlongSwell,along);
            WakeCentrePhotographPlayTests.V24TrackOffsets(born+Vector2.up*(swell+own),pose.DrawnStern,pose.Heading,out across,out along);
            MaxAcrossFull=Mathf.Max(MaxAcrossFull,across); MaxAlongFull=Mathf.Max(MaxAlongFull,along);
            MaxOthersLift=Mathf.Max(MaxOthersLift,Mathf.Abs(others));
            Vector2 unfixed=WakeCentrePhotographPlayTests.V24UnfixedTideFrameBirth(in pose);
            WakeCentrePhotographPlayTests.V24TrackOffsets(WakeCentrePhotographPlayTests.V24WaterDrawsSwell(unfixed),pose.DrawnStern,pose.Heading,out across,out _);
            MaxUnfixedAcross=Mathf.Max(MaxUnfixedAcross,across);
            MaxTideRise=Mathf.Max(MaxTideRise,Mathf.Abs(pose.TideRise));
            HeadingSweep=Mathf.Max(HeadingSweep,Mathf.Abs(Mathf.DeltaAngle(_firstHeading,ArrivalPilot.CompassOf(transform.up))));
            Samples++;
        }
    }
}
