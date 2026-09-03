using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The acceptance in pixels: does a lamp's shadow reach the screen, on the far side of the
    /// caster, and only there?</b> (ADR 0016, lights PR B.)
    ///
    /// <para>The pure tests prove the maths; the system tests prove the pairing. Neither can prove
    /// one darkened pixel lands where the maths says, above the glow, and nowhere it should not. So
    /// this stands a lit scene — a grey ground, a post, a real <see cref="SceneLight"/> glow quad, a
    /// real mesh hull — publishes the shipped 02:00 tint, drives the shipped system, and
    /// photographs it on the GPU.</para>
    ///
    /// <para><b>The metric is a DIFFERENCE, and it is exact here on purpose.</b> Nothing in this
    /// scene runs on <c>_Time</c> (no water, no wind, flicker 0), so two shots of the same
    /// configuration are byte-identical and "darkened" means darkened. That is also what makes the
    /// passthrough provable: strength 0 must equal the system absent, byte for byte.</para>
    ///
    /// <para><b>Self-skips without a graphics device</b> — the standing CI law. A skip is "NOT
    /// VERIFIED", never "passed". Contact sheets go to <c>artifacts/lamp-shadows/</c> (gitignored);
    /// the ones the PR ships are copied into <c>docs/art/spikes/lights-cast-shadows/</c> by hand.</para>
    /// </summary>
    public class LampShadowRenderTests
    {
        private const int ShotPx = 400;
        private const float FrameMetres = 10f;          // ortho size 5 -> 40 px per metre
        private const float PxPerMetre = ShotPx / FrameMetres;
        private static readonly Color Night = new Color(0.016f, 0.020f, 0.040f, 1f);
        private const string DoryMeshPath = "Assets/_Project/Data/Boats/HullMeshes/DoryIsoHullMesh.asset";
        private static readonly int IdDayNightTint = Shader.PropertyToID("_DayNightTint");

        private readonly List<Object> _spawned = new List<Object>();
        private Camera _cam;
        private RenderTexture _rt;
        private LampShadowSystem _system;
        private Material _unlit;
        private Color _tintBefore;

        [SetUp]
        public void SetUp()
        {
            LampShadowSystem.ClearRegistries();
            _tintBefore = Shader.GetGlobalColor(IdDayNightTint);
        }

        [TearDown]
        public void TearDown()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            _cam = null;
            _system = null;
            LampShadowSystem.ClearRegistries();
            Shader.SetGlobalColor(IdDayNightTint, _tintBefore);
        }

        private static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device (Null Device), so nothing rendered and " +
                              "nothing was proved. Expected on CI; a drawn shadow needs a GPU.");
        }

        // =============================================================================================
        //  The acceptance
        // =============================================================================================

        /// <summary>
        /// A post under a lamp: the frame darkens ONLY on the far side of the post, inside the sheared
        /// box the maths predicts, never on the post itself — and strength 0 is the system absent,
        /// byte for byte.
        /// </summary>
        [Test]
        public void APost_ThrowsAShadowAwayFromTheLamp_AndStrengthZeroIsTheShippedFrame()
        {
            RequireAGraphicsDevice();
            BuildTheStage();
            SceneLight lamp = LitLamp(new Vector2(-3f, 0f), range: 8f);
            SpriteShadow post = Post(new Vector2(0f, 0f));
            Assert.IsTrue(post.TryGetLampShadowCaster(out LampShadowCasterState state));

            Color32[] on = Shoot("post-01-strength-0.8", strength: 0.8f);
            Color32[] off = Shoot("post-02-strength-0", strength: 0f);
            Color32[] offAgain = Shoot("post-03-strength-0-repeat", strength: 0f);
            _system.DisableAll();
            _system.enabled = false;
            Color32[] absent = Shoot("post-04-system-absent", strength: 0f, publish: false);

            Assert.IsTrue(Identical(off, offAgain), "nothing in this scene runs on the clock: two identical shots are identical");
            Assert.IsTrue(Identical(off, absent), "STRENGTH 0 IS TODAY'S FRAME — byte for byte the same as the system absent");
            Assert.IsFalse(Identical(on, off), "and strength 0.8 changes something");

            // Where it changed. Predict the shadow's box from the same maths the system ran.
            LampShadowProfile p = _system.Profile;
            Vector2 foot = LampShadowMath.SnapToPixels(state.Foot, p.PixelsPerUnit);
            Vector2 dir = LampShadowMath.ShadowDirection(lamp.WorldOrigin, foot, Vector2.down);
            float len = LampShadowMath.ShadowLengthMultiple(
                LampShadowMath.LampElevation(lamp.LampHeightMeters, (foot - lamp.WorldOrigin).magnitude, p.MinLampHeightMeters),
                p.LengthAtNoon, p.LengthAtHorizon, p.MaxLength);
            LampShadowMath.ShearedBounds(state.RectMin, state.RectMax, foot, dir, len, out Vector2 bmin, out Vector2 bmax);

            int darkened = 0, outsideBox = 0, onThePost = 0;
            double cx = 0;
            for (int i = 0; i < on.Length; i++)
            {
                if (!Darker(on[i], off[i])) continue;
                darkened++;
                Vector2 w = PixelToWorld(i);
                cx += w.x;
                if (w.x < bmin.x - 2f / PxPerMetre || w.x > bmax.x + 2f / PxPerMetre ||
                    w.y < bmin.y - 2f / PxPerMetre || w.y > bmax.y + 2f / PxPerMetre) outsideBox++;
                if (w.x > state.RectMin.x + 1f / PxPerMetre && w.x < state.RectMax.x - 1f / PxPerMetre &&
                    w.y > state.RectMin.y + 1f / PxPerMetre && w.y < state.RectMax.y - 1f / PxPerMetre) onThePost++;
            }
            int expectedArea = Mathf.RoundToInt((state.RectMax.x - state.RectMin.x) * (state.RectMax.y - state.RectMin.y)
                                                * PxPerMetre * PxPerMetre);
            Debug.Log($"[lamp-shadow] post: {darkened} px darkened (the post's own image is {expectedArea} px); " +
                      $"{outsideBox} outside the predicted box; {onThePost} on the post; centroid x {cx / Mathf.Max(darkened, 1):F2} m; " +
                      $"box x [{bmin.x:F2}, {bmax.x:F2}] y [{bmin.y:F2}, {bmax.y:F2}]; rake {len:F2}x along {dir}");

            Assert.Greater(darkened, expectedArea / 2,
                "the shadow must be at least half the post's own area — a sheared copy of it, not a few edge pixels");
            Assert.AreEqual(0, outsideBox, "every darkened pixel lies inside the sheared silhouette's box");
            Assert.AreEqual(0, onThePost, "a shadow never darkens its own caster");
            Assert.Greater(cx / darkened, state.RectMax.x, "the shadow lies on the FAR side of the post from the lamp");

            SaveSheet("fixture-sprite-post", on, off);
            AssertThePublishedSheetIsTheRightWayUp("fixture-sprite-post", foot, dir, len);
        }

        /// <summary>The lamp on the other side: the shadow swings to the other side with it — it is cast BY THE LAMP.</summary>
        [Test]
        public void MovingTheLamp_SwingsTheShadowToTheOtherSide()
        {
            RequireAGraphicsDevice();
            BuildTheStage();
            SceneLight lamp = LitLamp(new Vector2(-3f, 0f), range: 8f);
            SpriteShadow post = Post(Vector2.zero);
            post.TryGetLampShadowCaster(out LampShadowCasterState state);

            Color32[] left = Shoot("post-05-lamp-west", strength: 0.8f);
            Color32[] leftOff = Shoot("post-06-lamp-west-off", strength: 0f);
            lamp.transform.position = new Vector3(3f, 0f, 0f);
            Color32[] right = Shoot("post-07-lamp-east", strength: 0.8f);
            Color32[] rightOff = Shoot("post-08-lamp-east-off", strength: 0f);

            double cxLeft = Centroid(left, leftOff, out int nLeft);
            double cxRight = Centroid(right, rightOff, out int nRight);
            Debug.Log($"[lamp-shadow] sweep: lamp west -> shadow centroid x {cxLeft:F2} ({nLeft} px); " +
                      $"lamp east -> {cxRight:F2} ({nRight} px)");
            Assert.Greater(nLeft, 0);
            Assert.Greater(nRight, 0);
            Assert.Greater(cxLeft, state.RectMax.x, "lamp west: the shadow lies east");
            Assert.Less(cxRight, state.RectMin.x, "lamp east: the shadow lies west — it moved with the lamp");
            SaveSheet("fixture-sprite-sweep", left, right);
        }

        /// <summary>
        /// A mesh hull has no sprite: her silhouette comes out of the feature's resolved screen texture
        /// through her id block. The dory, lit from the west, must darken the ground east of her.
        /// </summary>
        [Test]
        public void AMeshHull_ThrowsAShadowFromTheResolvedScreenTexture()
        {
            RequireAGraphicsDevice();
            BuildTheStage();
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(DoryMeshPath);
            Assert.IsNotNull(def, "the dory's hull mesh def must exist");

            var host = new GameObject("dory");
            _spawned.Add(host);
            var hull = host.AddComponent<IsoFacetHullRenderer>();
            hull.Configure(IsoFacetHullPresentationService.ToSetup(def));
            hull.HeadingDirUnits = 2f;   // broadside to the beam so her length faces the lamp
            hull.ApplyPose();
            HullLampShadowCaster caster = HullLampShadowCaster.Fit(host);
            LampShadowSystem.RegisterCaster(caster);
            Assert.IsTrue(caster.TryGetLampShadowCaster(out LampShadowCasterState state));

            SceneLight lamp = LitLamp(new Vector2(-6f, 0f), range: 14f);

            Color32[] on = Shoot("hull-01-strength-0.8", strength: 0.8f);
            Color32[] off = Shoot("hull-02-strength-0", strength: 0f);
            Assert.IsFalse(Identical(on, off), "the hull's shadow must reach the frame");

            LampShadowProfile p = _system.Profile;
            Vector2 foot = LampShadowMath.SnapToPixels(state.Foot, p.PixelsPerUnit);
            Vector2 dir = LampShadowMath.ShadowDirection(lamp.WorldOrigin, foot, Vector2.down);
            float len = LampShadowMath.ShadowLengthMultiple(
                LampShadowMath.LampElevation(lamp.LampHeightMeters, (foot - lamp.WorldOrigin).magnitude, p.MinLampHeightMeters),
                p.LengthAtNoon, p.LengthAtHorizon, p.MaxLength);
            len = LampShadowMath.ClampShearFold(dir, len, p.MinShearDenominator);
            LampShadowMath.ShearedBounds(state.RectMin, state.RectMax, foot, dir, len, out Vector2 bmin, out Vector2 bmax);

            int darkened = 0, outsideBox = 0, eastOfHer = 0;
            for (int i = 0; i < on.Length; i++)
            {
                if (!Darker(on[i], off[i])) continue;
                darkened++;
                Vector2 w = PixelToWorld(i);
                if (w.x < bmin.x - 2f / PxPerMetre || w.x > bmax.x + 2f / PxPerMetre ||
                    w.y < bmin.y - 2f / PxPerMetre || w.y > bmax.y + 2f / PxPerMetre) outsideBox++;
                if (w.x > state.RectMax.x) eastOfHer++;
            }
            Debug.Log($"[lamp-shadow] hull: {darkened} px darkened, {outsideBox} outside the predicted box, " +
                      $"{eastOfHer} east of her cell; rake {len:F2}x along {dir}; cell x [{state.RectMin.x:F2}, {state.RectMax.x:F2}]");

            Assert.Greater(darkened, 200, "a boat throws a boat-sized shadow, not a smudge");
            Assert.AreEqual(0, outsideBox, "every darkened pixel lies inside the sheared silhouette's box");
            Assert.Greater(eastOfHer, 100, "her upperworks throw east of her cell — the silhouette came through the id lookup");

            SaveSheet("fixture-hull-dory", on, off);
        }

        // =============================================================================================
        //  Measurement
        // =============================================================================================

        private static bool Darker(Color32 a, Color32 b)
        {
            int la = a.r + a.g + a.b, lb = b.r + b.g + b.b;
            return la < lb - 6;   // two 8-bit steps summed over three channels: below rounding, above nothing
        }

        private static bool Identical(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b || a[i].a != b[i].a) return false;
            return true;
        }

        private double Centroid(Color32[] on, Color32[] off, out int count)
        {
            double cx = 0;
            count = 0;
            for (int i = 0; i < on.Length; i++)
            {
                if (!Darker(on[i], off[i])) continue;
                cx += PixelToWorld(i).x;
                count++;
            }
            return count > 0 ? cx / count : 0;
        }

        /// <summary>Read-back pixel (bottom-left origin) to world.</summary>
        private Vector2 PixelToWorld(int index)
        {
            int px = index % ShotPx, py = index / ShotPx;
            Vector3 c = _cam.transform.position;
            return new Vector2(c.x + (px + 0.5f) / PxPerMetre - FrameMetres * 0.5f,
                               c.y + (py + 0.5f) / PxPerMetre - FrameMetres * 0.5f);
        }

        // =============================================================================================
        //  The stage
        // =============================================================================================

        private void BuildTheStage()
        {
            Shader.SetGlobalColor(IdDayNightTint, Night);

            var camGo = new GameObject("LampShadowShotCam");
            _spawned.Add(camGo);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = FrameMetres * 0.5f;
            _cam.transform.position = new Vector3(0f, 0f, -10f);
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 100f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Color.black;
            _cam.allowMSAA = false;
            _cam.allowHDR = false;
            _rt = new RenderTexture(ShotPx, ShotPx, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            _cam.targetTexture = _rt;

            var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            Assert.IsNotNull(unlitShader, "URP's Sprite-Unlit-Default shader is missing?");
            _unlit = new Material(unlitShader);
            _spawned.Add(_unlit);

            // A mid-grey ground so a multiply has something to take from, drawn under everything.
            var ground = Solid(4, 4, new Color32(128, 128, 128, 255));
            var groundGo = new GameObject("ground");
            _spawned.Add(groundGo);
            var gsr = groundGo.AddComponent<SpriteRenderer>();
            gsr.sprite = Sprite.Create(ground, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            _spawned.Add(gsr.sprite);
            gsr.sharedMaterial = _unlit;
            gsr.sortingOrder = -10;
            groundGo.transform.localScale = new Vector3(FrameMetres * 1.2f, FrameMetres * 1.2f, 1f);

            var host = new GameObject("LampShadowSystem (fixture)") { hideFlags = HideFlags.HideAndDontSave };
            _spawned.Add(host);
            _system = host.AddComponent<LampShadowSystem>();
            _system.Profile = LampShadowProfile.CreateDefault();
            _spawned.Add(_system.Profile);
        }

        private Texture2D Solid(int w, int h, Color32 c)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            tex.SetPixels32(px);
            tex.Apply();
            _spawned.Add(tex);
            return tex;
        }

        /// <summary>A quarter-metre-wide, metre-and-a-half-tall opaque post standing on its feet at <paramref name="at"/>.</summary>
        private SpriteShadow Post(Vector2 at)
        {
            var tex = Solid(8, 48, new Color32(220, 210, 190, 255));
            var go = new GameObject("post");
            _spawned.Add(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 48), new Vector2(0.5f, 0f), 32f);
            _spawned.Add(sr.sprite);
            sr.sharedMaterial = _unlit;
            sr.sortingOrder = 5;
            var caster = go.AddComponent<SpriteShadow>();
            LampShadowSystem.RegisterCaster(caster);   // OnEnable does this at runtime; edit mode runs none
            return caster;
        }

        /// <summary>
        /// A REAL <see cref="SceneLight"/>, quad and all. Edit mode never runs <c>Awake</c>/<c>OnEnable</c>,
        /// so the lifecycle the runtime would run is invoked directly — the same trick
        /// <c>SpriteShadowCastsPlayTests</c> uses for <c>Tick</c>. The glow it draws is what the shadow
        /// must land ON TOP of.
        /// </summary>
        private SceneLight LitLamp(Vector2 at, float range)
        {
            var go = new GameObject("lamp");
            _spawned.Add(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var light = go.AddComponent<SceneLight>();
            light.Shape = SceneLight.LightShape.Radial;
            light.Range = range;
            light.Intensity = 1.5f;
            light.FlickerAmount = 0f;
            Invoke(light, "Awake");
            Invoke(light, "OnEnable");   // Tick + PoseQuad + RegisterLight
            return light;
        }

        private static void Invoke(Object target, string method)
        {
            MethodInfo m = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, $"{target.GetType().Name}.{method}() not found (private API moved?)");
            m.Invoke(target, null);
        }

        private Color32[] Shoot(string name, float strength, bool publish = true)
        {
            Shader.SetGlobalColor(IdDayNightTint, Night);
            if (publish)
            {
                _system.Profile.Strength = strength;
                _system.PublishFrame(_cam);
            }
            // Every lamp re-poses against the camera the way LateUpdate would.
            foreach (var light in Object.FindObjectsByType<SceneLight>(FindObjectsSortMode.None))
            {
                Invoke(light, "Tick");
                Invoke(light, "PoseQuad");
            }

            WaitOutShaderCompilation();
            _cam.Render();
            _cam.Render();   // the second is read: a cold shader cache has faked a regression here before

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(ShotPx, ShotPx, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, ShotPx, ShotPx), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Color32[] px = tex.GetPixels32();
            SavePng(name, tex);
            Object.DestroyImmediate(tex);
            return px;
        }

        private void WaitOutShaderCompilation()
        {
            for (int i = 0; i < 10; i++)
            {
                _cam.Render();
                if (!ShaderUtil.anythingCompiling) return;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (ShaderUtil.anythingCompiling && clock.Elapsed.TotalSeconds < 120)
                    System.Threading.Thread.Sleep(25);
            }
            Assert.Fail("SHADERS NEVER FINISHED COMPILING — not a shadow regression. Re-run with a warm shader cache.");
        }

        // =============================================================================================
        //  Publishing
        // =============================================================================================

        private static string ArtifactDir()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "lamp-shadows");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void SavePng(string name, Texture2D tex)
            => File.WriteAllBytes(Path.Combine(ArtifactDir(), name + ".png"), tex.EncodeToPNG());

        /// <summary>An A|B contact sheet with a hairline divider — one frame that answers the question.</summary>
        private static void SaveSheet(string name, Color32[] left, Color32[] right)
        {
            const int gap = 6;
            int w = ShotPx * 2 + gap;
            var tex = new Texture2D(w, ShotPx, TextureFormat.RGBA32, false);
            var px = new Color32[w * ShotPx];
            for (int y = 0; y < ShotPx; y++)
            {
                for (int x = 0; x < ShotPx; x++)
                {
                    px[y * w + x] = left[y * ShotPx + x];
                    px[y * w + ShotPx + gap + x] = right[y * ShotPx + x];
                }
                for (int g = 0; g < gap; g++) px[y * w + ShotPx + g] = new Color32(153, 140, 102, 255);
            }
            // GetPixels32/ReadPixels are BOTTOM-left; SetPixels32 is bottom-left too — no flip, and the
            // assertion below reads the FILE back to say so.
            tex.SetPixels32(px);
            tex.Apply();
            SavePng(name, tex);
            Object.DestroyImmediate(tex);
        }

        /// <summary>
        /// The eye reads the FILE, not the buffer: the published sheet must be the right way up. The
        /// lamp saturates every channel around the post, so no single pixel's colour can say which way
        /// is up — but the SHADOW can: it darkens the left panel against the right one at the sheared
        /// image of a point 0.75 m ABOVE the feet, and nowhere at the mirror point 0.75 m BELOW them.
        /// An upside-down file puts the darkening at the mirror point instead.
        /// </summary>
        private void AssertThePublishedSheetIsTheRightWayUp(string name, Vector2 foot, Vector2 dir, float len)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(ArtifactDir(), name + ".png"));
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(tex.LoadImage(bytes), "the published sheet must decode");
            int w = tex.width;
            Color32[] px = tex.GetPixels32();   // bottom-left origin once decoded
            Object.DestroyImmediate(tex);

            Vector2 above = LampShadowMath.Shear(new Vector2(foot.x, foot.y + 0.75f), foot, dir, len);
            Vector2 below = new Vector2(above.x, foot.y - 0.75f);
            int Index(Vector2 world, int panel)
            {
                int col = panel * (ShotPx + 6) + Mathf.RoundToInt((world.x - _cam.transform.position.x + FrameMetres * 0.5f) * PxPerMetre);
                int row = Mathf.RoundToInt((world.y - _cam.transform.position.y + FrameMetres * 0.5f) * PxPerMetre);
                return row * w + col;
            }
            Assert.IsTrue(Darker(px[Index(above, 0)], px[Index(above, 1)]),
                $"the shadow of a point above the feet ({above}) must darken the left panel in the published file");
            Assert.IsFalse(Darker(px[Index(below, 0)], px[Index(below, 1)]),
                $"and the mirror point below the feet ({below}) must not — an upside-down sheet fails here");
        }
    }
}
