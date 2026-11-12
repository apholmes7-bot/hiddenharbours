using HiddenHarbours.Tests.Support;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.App.Editor;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class WalkerFittingPlayTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly string PlateDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Evidence~/lights/pr1/plates",
            System.Environment.GetEnvironmentVariable("HH_LIGHTS_PLATE_RUN") ?? "run"));
        WharfNightStage _stage;
        GameConfig _oldConfig, _config;
        Transform _oldPlayer;
        GameObject _root, _host;
        SpriteRenderer _body;
        IsoCharacterFigureRenderer _figure;
        CharacterSkinDef _skin;
        CharacterVisualDef _visual;
        WalkerLights _walker;
        Transform _facet;
        bool _oldPause;
        readonly List<(WalkerLights Host, bool Enabled)> _hostsBefore = new();
        readonly List<(Renderer Renderer, bool ForceOff)> _actorRenderers = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_stage == null) yield break;
            GameServices.PlayerTransform = _oldPlayer;
            if (GameServices.Clock != null) GameServices.Clock.IsPaused = _oldPause;
            if (_stage != null) yield return _stage.TearDown();
            foreach (var item in _actorRenderers) if (item.Renderer != null) item.Renderer.forceRenderingOff = item.ForceOff;
            _actorRenderers.Clear();
            foreach (var item in _hostsBefore) if (item.Host != null) item.Host.enabled = item.Enabled;
            _hostsBefore.Clear();
            GameServices.Config = _oldConfig;
            if (_config != null) Object.Destroy(_config);
            InteractActorProbe.Clear();
            WalkerLights.SetOwnsDecorLightForTests(false);
        }

        IEnumerator SetUpStage()
        {
            Directory.CreateDirectory(PlateDir);
            _stage = new WharfNightStage("StPeters", "walker-fittings");
            yield return _stage.Load();
            HideSceneActorRenderers();
            yield return _stage.SetNight(2f);
            _oldPause = GameServices.Clock.IsPaused;
            GameServices.Clock.IsPaused = true;
            _oldConfig = GameServices.Config;
            _config = Object.Instantiate(_oldConfig);
            GameServices.Config = _config;
            _oldPlayer = GameServices.PlayerTransform;
            foreach (var host in Object.FindObjectsByType<WalkerLights>(FindObjectsSortMode.None))
            { _hostsBefore.Add((host, host.enabled)); host.enabled = false; }
#if UNITY_EDITOR
            _skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>("Assets/_Project/Data/Characters/Skin/fisher.asset");
            _visual = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>("Assets/_Project/Data/Characters/FisherIso.asset");
#endif
            Assert.IsNotNull(_skin); Assert.IsNotNull(_visual);
            Vector2 at = StPetersWharf.LadderPosition();
            _root = _stage.Track(new GameObject("WalkerFittingFixture"));
            _root.transform.position = at;
            _body = _root.AddComponent<SpriteRenderer>();
            _body.sprite = _visual.IdleSheet[0];
            _body.sharedMaterial = Resources.Load<Material>("PlayerSubmerge");
            _body.sortingOrder = 1200;
            var go = new GameObject("Figure");
            go.transform.SetParent(_root.transform, false);
            _figure = go.AddComponent<IsoCharacterFigureRenderer>();
            _figure.Configure(_skin);
            Assert.IsTrue(_figure.EnterAshore(_body));
            _facet = go.transform.Find("AshoreFrame/FacetFigure");
            Assert.IsNotNull(_facet);
            _figure.SetPose("walk", 0);
            _figure.Visible = true;
            _body.forceRenderingOff = true;
            GameServices.PlayerTransform = _root.transform;
            InteractActorProbe.Set(InteractActor.For(at, Vector2.up, ControlMode.OnFoot));
            _host = _stage.Track(new GameObject("WalkerFittingHost"));
            _walker = _host.AddComponent<WalkerLights>();
            Step(_walker, "Update");
            _walker.enabled = false; // inputs are held by this fixture, never a live keyboard
            _walker.Beam.SetOn(true);
            _walker.Beam.SetLive(true);
            WalkerLights.SetOwnsDecorLightForTests(true);
            yield return _stage.FrameOn(at + Vector2.up * .9f);
            _stage.Camera.orthographicSize = _stage.Height / 128f; // two screen pixels per art pixel
            yield return null;
            Step(_walker.Beam, "LateUpdate");
        }

        void HideSceneActorRenderers()
        {
            // Re-find loaded actors after travel; teardown restores each renderer's original state.
            foreach (var actor in Object.FindObjectsByType<PlayerWalkController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
            {
                if (_root != null && renderer.transform.IsChildOf(_root.transform)) continue;
                if (!_actorRenderers.Exists(item => item.Renderer == renderer))
                    _actorRenderers.Add((renderer, renderer.forceRenderingOff));
                renderer.forceRenderingOff = true;
            }
        }

        byte[] CapturePlate() { HideSceneActorRenderers(); return _stage.Capture(); }

        [UnityTest]
        [Explicit("Historical raster measurement before the housing correction.")]
        public IEnumerator HousingRaster_SignedReadBeforeTheOneCorrection()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return SetUpStage();
            var csv = new StringBuilder("clip,frame,facing,dx_px,dy_px,magnification,ortho_size\n");
            var fitting = Fitting();
            Assert.IsNotNull(fitting);
            Vector2? first = null;
            bool constant = true;
            foreach (string state in _skin.MeshStates)
            {
                Assert.IsTrue(_skin.TryGetClip(state, out var clip));
                for (int frame = 0; frame < clip.FrameCount; frame++)
                for (int dir = 0; dir < 8; dir++)
                {
                    Pose(state, frame, dir); Step(_walker.Beam, "LateUpdate");
                    RenderedFittingError(fitting, DrawnHeadScreen(), out _, out var signed);
                    if (!first.HasValue) first = signed;
                    else constant &= signed.Equals(first.Value);
                    csv.AppendLine($"{state},{frame},{dir},{signed.x:R},{signed.y:R},{PlanMagnification():R},{_stage.Camera.orthographicSize:R}");
                }
            }
            File.WriteAllText(Path.Combine(PlateDir, "housing-signed-before.csv"), csv.ToString());
            Assert.IsTrue(_skin.TryGetClip("walk", out var walk));
            Pose("walk", walk.FrameCount / 2, 0); Step(_walker.Beam, "LateUpdate"); ContactArms("walk-0");
            Assert.IsTrue(constant, "signed housing errors must be constant before the one correction");
            MeasureCost();
        }

        [UnityTest]
        public IEnumerator TheWalkersLamp_RidesHerHead_EveryMeshClipEveryFacing()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return SetUpStage();
            bool pixelTarget = _config.FigurePixelTarget;
            File.WriteAllText(Path.Combine(PlateDir, "lens-config.txt"), $"FigurePixelTarget={pixelTarget}\n");
            TestContext.Progress.WriteLine($"FigurePixelTarget={pixelTarget}");
            Assert.IsTrue(pixelTarget, "FigurePixelTarget is false: the expected figure point is invalid; report this first");
            float worst = 0f, fittingWorst = 0f, fittingAxisWorst = 0f;
            string worstName = "";
            int samples = 0, rendered = 0;
            var csv = new StringBuilder("clip,frame,facing,origin_error_px,fitting_error_px,brightness_centroid_error_px,dx_px,dy_px,magnification,ortho_size\n");
            var fitting = Fitting();
            foreach (string state in _skin.MeshStates)
            {
                Assert.IsTrue(_skin.TryGetClip(state, out var clip), state);
                for (int frame = 0; frame < clip.FrameCount; frame++)
                for (int dir = 0; dir < 8; dir++)
                {
                    Pose(state, frame, dir);
                    Step(_walker.Beam, "LateUpdate");
                    Vector2 expected = DrawnHeadScreen();
                    Vector2 origin = _stage.Camera.WorldToScreenPoint(BloomOrigin());
                    float error = Vector2.Distance(expected, origin);
                    if (error > worst) { worst = error; worstName = state + "/" + frame + "/" + dir; }
                    float fitError = -1f, brightnessError = -1f;
                    Vector2 signed = Vector2.zero;
                    if (fitting != null)
                    {
                        fitError = RenderedFittingError(fitting, expected, out brightnessError, out signed);
                        fittingWorst = Mathf.Max(fittingWorst, fitError);
                        fittingAxisWorst = Mathf.Max(fittingAxisWorst, Mathf.Abs(signed.x), Mathf.Abs(signed.y));
                        rendered++;
                    }
                    csv.AppendLine($"{state},{frame},{dir},{error:R},{fitError:R},{brightnessError:R},{signed.x:R},{signed.y:R},{PlanMagnification():R},{_stage.Camera.orthographicSize:R}");
                    samples++;
                    if (frame == clip.FrameCount / 2 && (state == "walk" || state == "ladderDown"))
                        ContactArms(state + "-" + dir);
                }
            }
            File.WriteAllText(Path.Combine(PlateDir, "mesh-anchor-errors.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(PlateDir, "mesh-worst.txt"),
                $"samples={samples}\nrendered_fittings={rendered}\nworst_origin_px={worst:R}\nworst={worstName}\nworst_rendered_fitting_px={fittingWorst:R}\nworst_rendered_fitting_axis_px={fittingAxisWorst:R}\n");
            Assert.IsNotNull(fitting, "the light must emerge from a drawn fitting");
            Assert.AreEqual(samples, rendered);
            Assert.LessOrEqual(worst, 1f, worstName);
            Assert.LessOrEqual(fittingAxisWorst, 1f, "worst rendered fitting, on either axis");
            yield return CheckFarthestWalkerLens(fitting);
        }

        IEnumerator CheckFarthestWalkerLens(SpriteRenderer fitting)
        {
            var camera = _stage.Camera;
            var pixelCamera = camera.GetComponents<Behaviour>().SingleOrDefault(component => component.GetType().Name == "PixelPerfectCamera");
            Assert.IsNotNull(pixelCamera);
            // The test assembly has no URP reference; use the component's public properties.
            var referenceX = pixelCamera.GetType().GetProperty("refResolutionX");
            var referenceY = pixelCamera.GetType().GetProperty("refResolutionY");
            int oldReferenceX = (int)referenceX.GetValue(pixelCamera), oldReferenceY = (int)referenceY.GetValue(pixelCamera);
            bool oldEnabled = pixelCamera.enabled;
            Sprite native = fitting.sprite;
            var built = native.pivot.x / native.rect.width == .6f ? native :
                Sprite.Create(native.texture, native.rect,
                    new Vector2(Mathf.Ceil(native.rect.width * .5f) / native.rect.width, .5f), native.pixelsPerUnit);
            var centred = native.pivot.x / native.rect.width == .5f ? native :
                Sprite.Create(native.texture, native.rect, new Vector2(.5f, .5f), native.pixelsPerUnit);
            if (built != native) built.hideFlags = HideFlags.HideAndDontSave;
            centred.hideFlags = HideFlags.HideAndDontSave;
            var csv = new StringBuilder("M,facing,pivot,dx,dy,magnitude,actual_magnification,ortho_size,origin_error_px\n");
            bool builtPass = true, centredPass = true, askedLens = true, centredRowsMatch = true;
            float worstOrigin = 0f, builtWorst = 0f, centredWorst = 0f;
            try
            {
                foreach (int m in new[] { 2, 3, 4, 5, 6 })
                {
                    referenceX.SetValue(pixelCamera, _stage.Width / m);
                    referenceY.SetValue(pixelCamera, _stage.Height / m);
                    pixelCamera.enabled = true;
                    yield return _stage.FrameOn((Vector2)_root.transform.position + Vector2.up * .9f);
                    for (int dir = 0; dir < 8; dir++)
                    {
                        foreach (Sprite sprite in new[] { built, centred })
                        {
                            fitting.sprite = sprite;
                            yield return null;
                            Pose("idle", 0, dir); Step(_walker.Beam, "LateUpdate");
                            Vector2 expected = DrawnHeadScreen();
                            float origin = Vector2.Distance(expected, _stage.Camera.WorldToScreenPoint(BloomOrigin()));
                            worstOrigin = Mathf.Max(worstOrigin, origin);
                            float housing = RenderedFittingError(fitting, expected, out _, out var signed);
                            double actual = PlanMagnification();
                            askedLens &= actual == m && Mathf.Abs(camera.orthographicSize - _stage.Height / (64f * m)) <= 1e-5f;
                            // One art pixel is M screen pixels. Record centre-pivot differences for review; suite rounding may differ.
                            if (sprite == centred)
                            {
                                bool negativeFacing = dir == 0 || dir == 1 || dir == 3 || dir == 6;
                                Vector2 recorded = m switch
                                {
                                    2 => new Vector2(1f, 0f),
                                    3 => new Vector2(negativeFacing ? -2f : 1f, -1f),
                                    4 => new Vector2(2f, 0f),
                                    5 => new Vector2(-3f, -1f),
                                    _ => new Vector2(negativeFacing ? -3f : 3f, 0f),
                                };
                                centredRowsMatch &= signed.Equals(recorded);
                                centredPass &= housing <= m;
                            }
                            else builtPass &= housing <= m;
                            if (m >= 3)
                            {
                                if (sprite == built) builtWorst = Mathf.Max(builtWorst, housing);
                                else centredWorst = Mathf.Max(centredWorst, housing);
                            }
                            float pivot = sprite.pivot.x / sprite.rect.width;
                            csv.AppendLine($"{m},{dir},{pivot:R},{signed.x:R},{signed.y:R},{housing:R},{actual:R},{camera.orthographicSize:R},{origin:R}");
                        }
                        fitting.sprite = built;
                    }
                }
                string tablePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Evidence~/lights/pr1/lens-table.csv"));
                File.WriteAllText(tablePath, csv.ToString());
                File.WriteAllText(Path.Combine(PlateDir, "lens-table.csv"), csv.ToString());
                Assert.IsTrue(askedLens, "a pinned lens did not render at its asked magnification and orthographic size");
                Assert.LessOrEqual(worstOrigin, 1f, "pinned-lens origin");
                Assert.IsTrue(centredPass, "the shipping centre pivot must meet one art pixel on every facing at M=2,3,4,5,6");
                fitting.sprite = centredPass && (!builtPass || centredWorst < builtWorst) ? centred : built;
                Assert.AreEqual(.5f, fitting.sprite.pivot.x / fitting.sprite.rect.width, "the measured centre pivot ships");
                Assert.AreEqual(.5f, native.pivot.x / native.rect.width, "the product must build the chosen pivot");
                File.WriteAllText(Path.Combine(PlateDir, "selected-pivot.txt"),
                    $"shipping_horizontal_pivot={native.pivot.x / native.rect.width:R}\nselected_horizontal_pivot={fitting.sprite.pivot.x / fitting.sprite.rect.width:R}\nshifted_pass={builtPass}\ncentred_pass={centredPass}\nshifted_worst_px={builtWorst:R}\ncentred_worst_px={centredWorst:R}\ncentred_rows_match_retained={centredRowsMatch}\n");
                MeasureCost();
            }
            finally
            {
                referenceX.SetValue(pixelCamera, oldReferenceX);
                referenceY.SetValue(pixelCamera, oldReferenceY);
                pixelCamera.enabled = oldEnabled;
                fitting.sprite = native;
                if (built != native) Object.DestroyImmediate(built);
                if (centred != native) Object.DestroyImmediate(centred);
            }
        }

        [UnityTest]
        public IEnumerator TheLamp_HoldsThroughASpritePose_WithoutAJump()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return SetUpStage();
            Pose("walk", 2, 4);
            Step(_walker.Beam, "LateUpdate");
            var fitting = Fitting();
            Vector2 before = (Vector2)BloomOrigin() - (Vector2)_root.transform.position;
            Assert.IsNotNull(fitting);
            foreach (string family in new[] { "ReachGround", "FishingHold" })
            {
                if (family == "ReachGround") _body.sprite = _visual.ReachGroundClip.Sheet[_visual.ClipFacingRowFor(CharacterClip.ReachGround, 180f) * 6 + 5];
                else
                {
                    var animator = Object.FindFirstObjectByType<PlayerFishingAnimator>(FindObjectsInactive.Include);
                    Assert.IsNotNull(animator);
                    var hold = (Sprite[])typeof(PlayerFishingAnimator).GetField("_holdFrames", Private).GetValue(animator);
                    _body.sprite = hold[FishingRow(animator, 4) * (hold.Length / 8)];
                }
                _figure.Visible = false;
                _body.forceRenderingOff = false;
                _root.transform.position += new Vector3(.25f, .125f, 0f);
                Step(_walker.Beam, "LateUpdate");
                Assert.LessOrEqual(Vector2.Distance(before, (Vector2)BloomOrigin() - (Vector2)_root.transform.position), 1e-5f, family + " entry");
                Assert.IsFalse(fitting.enabled, family + " fitting must hide");
                _figure.Visible = true;
                _body.forceRenderingOff = true;
                Step(_walker.Beam, "LateUpdate");
                Assert.LessOrEqual(Vector2.Distance(before, (Vector2)BloomOrigin() - (Vector2)_root.transform.position), 1f / 32f, family + " exit has no reset to the legacy lift");
                Assert.IsTrue(fitting.enabled);
                before = (Vector2)BloomOrigin() - (Vector2)_root.transform.position;
            }
            // Named initial path: a sprite-only def has never published a head, so it keeps today's lift.
            var bare = _stage.Track(new GameObject("NeverHadAHeadPoint"));
            var lamp = bare.AddComponent<Headlamp>();
            lamp.Configure(1.55f);
            Step(lamp, "LateUpdate");
            Assert.AreEqual(1.55f, lamp.Light.BloomLiftMetres, 1e-6f, "ConstantLiftUntilFirstHeadPoint");
            yield return SpriteExceptionPlates();
        }

        IEnumerator SpriteExceptionPlates()
        {
            var report = new StringBuilder("family,pose,frame,facing,held_to_visible_crown_px,held_x,held_y,crown_x,crown_y\n");
            float reachWorst = -1f, fishWorst = -1f, groundWorst = -1f, holdWorst = -1f;
            string fishWorstName = "", groundWorstName = "", holdWorstName = "";
            var fishing = Object.FindFirstObjectByType<PlayerFishingAnimator>(FindObjectsInactive.Include);
            Assert.IsNotNull(fishing, "the committed fishing sprite bank must be measured even while its player is inactive");
            var families = new Dictionary<string, Sprite[]> {
                { "reach-ground", _visual.ReachGroundClip.Sheet },
                { "reach-stowV", _visual.ReachStowVClip.Sheet },
                { "reach-stowH", _visual.ReachStowHClip.Sheet }
            };
            if (fishing != null)
                foreach (string name in new[] { "hold", "bite", "strike", "reel", "land", "castBack", "castRelease" })
                    families.Add("fishing-" + name, (Sprite[])typeof(PlayerFishingAnimator).GetField("_" + name + "Frames", Private).GetValue(fishing));
            foreach (var family in families)
            {
                Sprite[] sheet = family.Value;
                if (sheet == null || sheet.Length == 0) continue;
                int frames = sheet.Length / 8;
                for (int dir = 0; dir < 8; dir++)
                {
                    Pose("walk", 2, dir);
                    Step(_walker.Beam, "LateUpdate");
                    _figure.Visible = false; _body.forceRenderingOff = false;
                    for (int f = 0; f < frames; f++)
                    {
                        int row = family.Key.StartsWith("reach")
                            ? _visual.ClipFacingRowFor(family.Key == "reach-ground" ? CharacterClip.ReachGround
                                : family.Key == "reach-stowV" ? CharacterClip.ReachStowV : CharacterClip.ReachStowH, dir * 45f)
                            : FishingRow(fishing, dir);
                        _body.sprite = sheet[row * frames + f];
                        Step(_walker.Beam, "LateUpdate");
                        var bytes = CapturePlate();
                        Vector2 crown = SpriteCrownOnScreen(_body.sprite);
                        Vector2 held = _stage.Camera.WorldToScreenPoint(BloomOrigin());
                        float px = Vector2.Distance(held, crown);
                        report.AppendLine($"{(family.Key.StartsWith("reach") ? "reach" : "fishing")},{family.Key},{f},{dir},{px:R},{held.x:R},{held.y:R},{crown.x:R},{crown.y:R}");
                        if (family.Key.StartsWith("reach") && px > reachWorst)
                        { reachWorst = px; Save("exception-reach-worst.png", bytes); }
                        if (family.Key.StartsWith("fishing") && px > fishWorst)
                        { fishWorst = px; fishWorstName = family.Key + "/" + f + "/" + dir; Save("exception-fishing-worst.png", bytes); }
                        if (family.Key == "reach-ground" && px > groundWorst)
                        { groundWorst = px; groundWorstName = f + "/" + dir; Save("exception-reach-ground-worst.png", bytes); }
                        if (family.Key == "fishing-hold" && px > holdWorst)
                        { holdWorst = px; holdWorstName = f + "/" + dir; Save("exception-hold-worst.png", bytes); }
                        if (family.Key == "reach-ground" && f == frames - 1) ContactArms("crouch-" + dir);
                        if (family.Key == "fishing-hold") Save("hold-" + dir + "-" + f + ".png", bytes);
                    }
                }
            }
            File.WriteAllText(Path.Combine(PlateDir, "sprite-exceptions.csv"), report.ToString());
            File.WriteAllText(Path.Combine(PlateDir, "sprite-exceptions.txt"),
                $"Visible crown reference: centroid of nonblack body pixels in the top six art-row heights of a body-only render through the same camera. Lamp origin and crown pixel coordinates are retained in the CSV. No 1 px claim.\nreach_worst_px={reachWorst:R}\nreach_ground_worst_px={groundWorst:R}\nreach_ground_worst_frame_facing={groundWorstName}\nfishing_worst_px={fishWorst:R}\nfishing_worst={fishWorstName}\nhold_worst_px={holdWorst:R}\nhold_worst_frame_facing={holdWorstName}\n");
            yield return null;
        }

        [UnityTest]
        public IEnumerator FixtureTemperaturesAndFlames_AtTwoInTheMorning()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return SetUpStage();
            yield return BowBlobAttribution();
            yield return _stage.SetNight(2f);
            yield return _stage.FrameOn((Vector2)_root.transform.position + Vector2.up * .9f);
            _figure.Visible = false; _walker.Beam.SetOn(false); PublishControlledLamp();
            var lamps = new List<SceneLight>();
            Vector3 start = _root.transform.position + new Vector3(-8f, 0, 0);
            PreconfiguredLight source = null;
            SpriteRenderer glassArt = null;
            foreach (var candidate in Object.FindObjectsByType<PreconfiguredLight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var post = candidate.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(s => s.name == "Post");
                if (post == null || candidate.GetComponent<SceneLight>() == null) continue;
                source = candidate; glassArt = post; break;
            }
            Assert.IsNotNull(source, "the colour comparison uses a placed glass fitting");
            SceneLight sourceLamp = source.GetComponent<SceneLight>();
            foreach (LightPresets.Kind kind in Enum.GetValues(typeof(LightPresets.Kind)))
            {
                var go = _stage.Track(new GameObject("Fixture_" + kind));
                go.transform.position = start + Vector3.right * lamps.Count * 2.5f;
                var art = new GameObject("GlassFitting"); art.transform.SetParent(go.transform, false);
                art.transform.localPosition = glassArt.transform.localPosition;
                var sr = art.AddComponent<SpriteRenderer>();
                sr.sprite = glassArt.sprite;
                sr.sharedMaterial = Resources.Load<Material>("PlayerSubmerge");
                var lamp = go.AddComponent<SceneLight>();
                LightPresets.Apply(lamp, kind);
                lamp.Range = sourceLamp.Range;
                lamp.OriginOffset = sourceLamp.OriginOffset;
                lamp.BloomLiftMetres = sourceLamp.BloomLiftMetres;
                lamp.LampHeightMeters = sourceLamp.LampHeightMeters;
                lamp.ReachMetres = 0f; // equal glass fitting comparison, not the future window-spill path
                lamps.Add(lamp);
            }
            yield return null;
            foreach (var l in lamps) { Step(l, "Tick"); Step(l, "PoseQuad"); }
            Save("street-temperatures-0200.png", CapturePlate());
            File.WriteAllText(Path.Combine(PlateDir, "street-temperatures.txt"),
                "02:00 same-run comparison on a common placed glass fitting. Left to right: domestic 2700 K, sodium 2100 K, work 4300 K, flood 5600 K, oil flame 1800 K, LED 6500 K, hung flame 1800 K. Fitting geometry is held equal; preset intensity and flicker remain their own. This is a colour comparison, not a building-pane or fleet-anchor proof.\n");
            var baseline = CapturePlate();
            CollectionAssert.AreEqual(baseline, CapturePlate(), "plate repeat inside this run");
            double startTime = GameServices.Clock.TotalSeconds;
            for (int frame = 0; frame < 6; frame++)
            {
                GameServices.Clock.SeekTo(startTime + frame * .13d);
                foreach (var l in lamps) Step(l, "Tick");
                Save("flames-" + frame + ".png", CapturePlate());
            }
            GameServices.Clock.SeekTo(startTime);
        }

        IEnumerator BowBlobAttribution()
        {
            _figure.Visible = false; _walker.Beam.SetOn(false);
            WalkerLights.SetOwnsDecorLightForTests(false);
            PublishControlledLamp();
            var root = _stage.Track(new GameObject("MainCapeBowAttribution"));
            root.transform.position = _root.transform.position + Vector3.down * 8f;
            var body = root.AddComponent<Rigidbody2D>();
            body.simulated = false; body.rotation = -90f; body.linearVelocity = Vector2.right * 3f;
            var host = new GameObject("FacetMesh"); host.transform.SetParent(root.transform, false);
#if UNITY_EDITOR
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>("Assets/_Project/Data/Boats/HullMeshes/CapeIslanderIsoHullMesh.asset");
            Assert.IsNotNull(new IsoFacetHullPresentationService().Install(host, def));
            var hull = host.GetComponent<IsoFacetHullRenderer>();
            hull.HeadingDirUnits = HullMeshMath.HeadingToDirUnits(90f, 0f, def.AzimuthCounterClockwise);
#endif
            var beam = root.GetComponent<BoatSpotlight>();
            if (beam == null) beam = root.AddComponent<BoatSpotlight>();
            beam.SetBeam(true);
            // This is dawn, not the helper's night/day gate contract: at 06:00 the real tint
            // still gates lamps on. Seek the actual hour and publish its unchanged grade directly.
            Time.timeScale = 1f;
            GameServices.Clock.SeekTo(1.25d * GameServices.Config.SecondsPerDay);
            var cycle = Resources.FindObjectsOfTypeAll<DayNightController>().FirstOrDefault(c => c.isActiveAndEnabled);
            Assert.IsNotNull(cycle);
            Step(cycle, "Tick");
            Assert.AreEqual(6f, GameServices.Clock.HourOfDay, .001f, "the bow shutter must be at dawn");
            yield return _stage.FrameOn((Vector2)root.transform.position + Vector2.right * 4f);
            typeof(BoatSpotlight).GetField("_smoothedSpeed", Private).SetValue(beam, 3f);
            Step(beam, "Update");
            var bridge = Resources.FindObjectsOfTypeAll<WaterLightBridge>().FirstOrDefault(b => b.isActiveAndEnabled);
            if (bridge != null) Step(bridge, "LateUpdate");
            var light = beam.GetComponent<SceneLight>();
            Assert.IsNotNull(light); Step(light, "PoseQuad");
            Save("bow-0600-full-light.png", CapturePlate());
            light.enabled = false;
            Save("bow-0600-without-searchlight-quad.png", CapturePlate());
            beam.SetBeam(false); Step(beam, "Update");
            if (bridge != null) Step(bridge, "LateUpdate");
            Save("bow-0600-without-searchlight.png", CapturePlate());
            File.WriteAllText(Path.Combine(PlateDir, "bow-shutters.txt"),
                "Main-box Cape at 06:00, heading east, searchlight speed input held at 3 m/s; same-run shutters: full searchlight, its land quad disabled with its cached water state retained, then beam switched off and bridge republished. No foam setting changed. This is a main-box counterpart, not the intro branch's exact opening state.\n");
            root.SetActive(false);
        }

        void Pose(string state, int frame, int dir)
        {
            Assert.IsTrue(_figure.SetPose(state, frame));
            _figure.SetAshoreYaw(_skin.AzimuthCounterClockwise ? -45f * dir : 45f * dir);
            _figure.Visible = true;
            _body.forceRenderingOff = true;
            float angle = dir * 45f * Mathf.Deg2Rad;
            _walker.Beam.transform.up = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle) * IsoGround.GroundDepthScale, 0);
        }

        static int FishingRow(PlayerFishingAnimator animator, int direction)
        {
            bool ccw = (bool)typeof(PlayerFishingAnimator).GetField("_rowsAreCounterClockwise", Private).GetValue(animator);
            float angle = direction * 45f * Mathf.Deg2Rad;
            return PlayerFishingAnimMath.FacingRowFor(Mathf.Sin(angle), Mathf.Cos(angle) * IsoGround.GroundDepthScale,
                .001f, 8, ccw, 0);
        }

        SpriteRenderer Fitting() => _walker.Beam.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault();
        Vector3 BloomOrigin() => (Vector3)_walker.Beam.Light.WorldOrigin + Vector3.up * _walker.Beam.Light.BloomLiftMetres;
        Vector2 DrawnHeadScreen()
        {
            Vector4 h = _figure.HeadSnapPoint;
            Vector3 world = _facet.TransformPoint(new Vector3(h.x, h.y, h.z));
            var camera = _stage.Camera;
            double perPixel = camera.orthographicSize * 2d / camera.pixelHeight;
            double left = camera.transform.position.x - camera.orthographicSize * camera.aspect;
            double bottom = camera.transform.position.y - camera.orthographicSize;
            if (FigurePixelGrid.TryPlan(camera.pixelWidth, camera.pixelHeight, left, bottom, perPixel, out var plan))
                return new Vector2((float)(plan.OffsetX + (Math.Floor(world.x * 32d) - plan.OriginTexelX + .5d) * plan.Magnification),
                                   (float)(plan.OffsetY + (Math.Floor(world.y * 32d) - plan.OriginTexelY + .5d) * plan.Magnification));
            Vector3 p = camera.WorldToScreenPoint(world);
            return new Vector2(Mathf.Floor(p.x) + .5f, Mathf.Floor(p.y) + .5f);
        }

        double PlanMagnification()
        {
            var camera = _stage.Camera;
            return FigurePixelGrid.TryPlan(camera.pixelWidth, camera.pixelHeight,
                camera.transform.position.x - camera.orthographicSize * camera.aspect,
                camera.transform.position.y - camera.orthographicSize,
                camera.orthographicSize * 2d / camera.pixelHeight, out var plan) ? plan.Magnification : 1d;
        }

        float RenderedFittingError(SpriteRenderer fitting, Vector2 expected, out float brightnessError, out Vector2 signed)
        {
            var camera = _stage.Camera;
            int mask = camera.cullingMask, layer = fitting.gameObject.layer;
            var clear = camera.clearFlags; var bg = camera.backgroundColor;
            try
            {
                fitting.gameObject.layer = 31; camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.Render();
                double xsum = 0, ysum = 0, mass = 0, shapeX = 0, shapeY = 0;
                int shapePixels = 0;
                double magnification = PlanMagnification();
                int margin = (int)Math.Ceiling(magnification);
                int tileWidth = (int)Math.Ceiling(fitting.sprite.rect.width * magnification) + 2 * margin;
                int tileHeight = (int)Math.Ceiling(fitting.sprite.rect.height * magnification) + 2 * margin;
                int x0 = Mathf.Max(0, (int)Math.Floor(expected.x - tileWidth * .5)),
                    y0 = Mathf.Max(0, (int)Math.Floor(expected.y - tileHeight * .5));
                int width = Mathf.Min(tileWidth, _stage.Width - x0), height = Mathf.Min(tileHeight, _stage.Height - y0);
                var previous = RenderTexture.active;
                RenderTexture.active = camera.targetTexture;
                var tile = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
                tile.ReadPixels(new Rect(x0, y0, width, height), 0, 0); tile.Apply();
                Color[] pixels = tile.GetPixels();
                Object.DestroyImmediate(tile); RenderTexture.active = previous;
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Color c = pixels[y * width + x];
                    double w = c.r + c.g + c.b;
                    if (w <= .01) continue;
                    mass += w; xsum += (x0 + x + .5) * w; ysum += (y0 + y + .5) * w;
                    shapePixels++; shapeX += x0 + x + .5; shapeY += y0 + y + .5;
                }
                if (mass <= 0)
                {
                    brightnessError = float.PositiveInfinity;
                    signed = Vector2.positiveInfinity;
                    return float.PositiveInfinity; // no rendered pixels is a failed row, never a passing distance
                }
                brightnessError = Vector2.Distance(expected, new Vector2((float)(xsum / mass), (float)(ysum / mass)));
                // The fitting is a shape: colour/illumination can bias its brightness centroid.
                // Keep that read alongside the geometry; the strict one-pixel bar is unchanged.
                signed = new Vector2((float)(shapeX / shapePixels), (float)(shapeY / shapePixels)) - expected;
                return signed.magnitude;
            }
            finally { fitting.gameObject.layer = layer; camera.cullingMask = mask; camera.clearFlags = clear; camera.backgroundColor = bg; }
        }

        void ContactArms(string name)
        {
            var lamp = _walker.Beam;
            bool on = lamp.IsOn;
            Vector3 pos = lamp.transform.position;
            float lift = lamp.Light.BloomLiftMetres;
            bool sprite = !_figure.Visible;
            var fitting = Fitting();
            lamp.enabled = false;
            WaterLightBridge.Register(lamp); // disable only the follower in this fixed-mount control
            if (fitting != null) fitting.enabled = false;
            lamp.transform.position = _root.transform.position;
            lamp.Light.BloomLiftMetres = 1.55f;
            _walker.Lantern.enabled = true;
            lamp.SetOn(true); Step(lamp.Light, "PoseQuad"); PublishControlledLamp(); Save("before-" + name + "-on.png", CapturePlate());
            lamp.SetOn(false); PublishControlledLamp(); Save("before-" + name + "-off.png", CapturePlate());
            _walker.Lantern.enabled = false;
            lamp.transform.position = pos; lamp.Light.BloomLiftMetres = lift;
            lamp.enabled = true; lamp.SetOn(true); Step(lamp, "LateUpdate"); Step(lamp.Light, "PoseQuad");
            PublishControlledLamp();
            Save("after-" + name + "-on.png", CapturePlate());
            WriteNearbyRenderers("after-" + name + "-on-renderers.txt");
            lamp.SetOn(false); Step(lamp, "LateUpdate"); PublishControlledLamp(); Save("after-" + name + "-off.png", CapturePlate());
            lamp.SetOn(on); Step(lamp, "LateUpdate");
            Assert.AreEqual(sprite, !_figure.Visible);
        }

        void WriteNearbyRenderers(string filename)
        {
            var lines = new List<string>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var scene = renderer.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded || !renderer.enabled || renderer.forceRenderingOff ||
                    renderer.transform.IsChildOf(_root.transform) || renderer.bounds.SqrDistance(_root.transform.position) > 25f)
                    continue;
                string path = renderer.name;
                for (var parent = renderer.transform.parent; parent != null; parent = parent.parent)
                    path = parent.name + "/" + path;
                Vector3 centre = renderer.bounds.center;
                lines.Add($"{path}\t{renderer.GetType().FullName}\t{renderer.sortingLayerName}:{renderer.sortingOrder}\t({centre.x:R},{centre.y:R},{centre.z:R})");
            }
            lines.Sort(StringComparer.Ordinal);
            File.WriteAllLines(Path.Combine(PlateDir, filename), lines);
        }

        void PublishControlledLamp()
        {
            WalkerLights.SetOwnsDecorLightForTests(_walker.Beam.IsOn);
            if (!_walker.Beam.PublishDecorLampIfOwned())
            {
                WaterLightState dark = default;
                DecorLampGlobals.Publish(in dark);
            }
            var bridge = Resources.FindObjectsOfTypeAll<WaterLightBridge>().FirstOrDefault(b => b.isActiveAndEnabled);
            if (bridge != null) Step(bridge, "LateUpdate");
        }

        Vector2 SpriteCrownOnScreen(Sprite sprite)
        {
            Camera camera = _stage.Camera;
            int mask = camera.cullingMask, layer = _body.gameObject.layer;
            var clear = camera.clearFlags; var bg = camera.backgroundColor;
            try
            {
                _body.gameObject.layer = 31; camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.Render();
                Vector3 lo = camera.WorldToScreenPoint(_body.bounds.min);
                Vector3 hi = camera.WorldToScreenPoint(_body.bounds.max);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(lo.x) - 2), y0 = Mathf.Max(0, Mathf.FloorToInt(lo.y) - 2);
                int width = Mathf.Min(_stage.Width - x0, Mathf.CeilToInt(hi.x) - x0 + 2);
                int height = Mathf.Min(_stage.Height - y0, Mathf.CeilToInt(hi.y) - y0 + 2);
                var previous = RenderTexture.active; RenderTexture.active = camera.targetTexture;
                var tile = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
                tile.ReadPixels(new Rect(x0, y0, width, height), 0, 0); tile.Apply();
                Color[] pixels = tile.GetPixels(); Object.DestroyImmediate(tile); RenderTexture.active = previous;
                int rows = Mathf.Max(1, Mathf.RoundToInt(6f * camera.pixelHeight / (2f * camera.orthographicSize * sprite.pixelsPerUnit)));
                int top = -1; double xsum = 0, ysum = 0, n = 0;
                for (int y = height - 1; y >= 0; y--)
                {
                    if (top >= 0 && y <= top - rows) break;
                    for (int x = 0; x < width; x++)
                    {
                        Color p = pixels[y * width + x];
                        if (p.r + p.g + p.b <= .001f) continue;
                        if (top < 0) top = y;
                        xsum += x0 + x + .5; ysum += y0 + y + .5; n++;
                    }
                }
                Assert.Greater(n, 0, "body-only crown render has pixels");
                return new Vector2((float)(xsum / n), (float)(ysum / n));
            }
            finally { _body.gameObject.layer = layer; camera.cullingMask = mask; camera.clearFlags = clear; camera.backgroundColor = bg; }
        }

        void MeasureCost()
        {
            var action = (Action)Delegate.CreateDelegate(typeof(Action), _walker.Beam,
                typeof(Headlamp).GetMethod("LateUpdate", Private));
            for (int i = 0; i < 100; i++) action();
            using var allocation = new ManagedAllocationProbe();
            var timer = Stopwatch.StartNew();
            allocation.Begin();
            for (int i = 0; i < 10000; i++) action();
            long allocationEvents = allocation.End();
            timer.Stop();
            File.WriteAllText(Path.Combine(PlateDir, "follower-cost.txt"),
                $"calls=10000\nmean_cpu_ms={timer.Elapsed.TotalMilliseconds / 10000:R}\nmanaged_allocation_events={allocationEvents}\nextra_fitting_draws=1\n");
            Assert.LessOrEqual(allocationEvents, 0, "follower allocates nothing per frame after warmup");
            var fitting = Fitting();
            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            double on = 0, off = 0, worstDelta = double.NegativeInfinity;
            for (int i = 0; i < 14; i++)
            {
                fitting.enabled = false;
                double without = TimedRender(pixel);
                fitting.enabled = true;
                double withFitting = TimedRender(pixel);
                if (i < 2) continue;
                off += without; on += withFitting; worstDelta = Math.Max(worstDelta, withFitting - without);
            }
            Object.DestroyImmediate(pixel);
            File.AppendAllText(Path.Combine(PlateDir, "follower-cost.txt"),
                $"render_pairs=12\nmean_without_fitting_ms={off / 12:R}\nmean_with_fitting_ms={on / 12:R}\nmean_render_delta_ms={(on - off) / 12:R}\nworst_paired_render_delta_ms={worstDelta:R}\nTiming includes camera submission and a synchronous one-pixel readback; paired variation is retained, not claimed as a GPU-only timer.\n");
        }

        double TimedRender(Texture2D pixel)
        {
            var timer = Stopwatch.StartNew();
            _stage.Camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _stage.Camera.targetTexture;
            pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); pixel.Apply();
            RenderTexture.active = previous;
            timer.Stop();
            return timer.Elapsed.TotalMilliseconds;
        }

        void Save(string name, byte[] bytes)
        {
            var tex = new Texture2D(_stage.Width, _stage.Height, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(bytes); tex.Apply();
            File.WriteAllBytes(Path.Combine(PlateDir, name), tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        }
        static void Step(object value, string method) => value.GetType().GetMethod(method, Private).Invoke(value, null);
    }
}
