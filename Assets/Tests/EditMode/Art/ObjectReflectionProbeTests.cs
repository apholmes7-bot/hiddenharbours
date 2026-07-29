using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// ADR 0027 #8's <b>FIDELITY PROBE</b>, run as a test rather than left as a claim — the ADR's
    /// open question was "is a flat mirrored silhouette enough at PPU 32, or must reflections honour
    /// the interior mask and hull self-occlusion?", with the instruction to <i>probe before
    /// building</i> and let the cheapest correct answer win.
    ///
    /// <para><b>What it renders — END TO END.</b> A sea quad running the real water shader, and one
    /// sprite carrying a <see cref="ReflectiveObject"/> standing on it. The real
    /// <c>IsoFacetHullFeature</c> pass draws that sprite ONCE into <c>_HHReflectTex</c> mirrored
    /// about its published ground-contact pivot — no interior mask, no self-occlusion, no
    /// per-vertex sway — and the real water shader composites it back. Through
    /// <c>Camera.Render()</c> on the project's own 2D renderer, at PPU 32. Nothing here bypasses
    /// URP.
    ///
    /// <para>⚠️ The first draft of this probe had NO water in it and measured 0 reflected pixels —
    /// correctly. <c>_HHReflectTex</c> is a private target, not the camera's colour buffer: without
    /// a sea to composite it, the pass draws into a texture nobody reads. That is worth knowing
    /// about the architecture, and it is why the probe carries a sea.</para></para>
    ///
    /// <para><b>What it asserts</b> (this is a test, not a screenshot with extra steps):
    /// <list type="bullet">
    /// <item>the reflection is THERE — the sea BELOW the waterline takes the reflector's colour;</item>
    /// <item>it is a MIRROR, not an offset copy — the reflected wedge tapers AWAY from the
    /// waterline while the source tapers upward, so a translated copy would fail;</item>
    /// <item>and it is BOUNDED — the sea well away from the reflector is untouched.</item>
    /// </list>
    /// It also writes the frame to <c>artifacts/</c> as a PNG so the probe can be LOOKED at, which
    /// is what "does it read?" ultimately means. <c>artifacts/</c> is git-ignored: the probe result
    /// goes in the PR body, not into the repo.</para>
    ///
    /// <para><b>Why CI cannot adjudicate this.</b> CI runs Unity with NO graphics device ("Null
    /// Device"), where a render does not fail — it CRASHES the editor (exit 1, no results XML). This
    /// fixture therefore gates on <see cref="RequireAGraphicsDevice"/> FIRST and skips loudly, the
    /// same discipline <c>IsoFacetUrpPassTests</c> keeps. A green CI run carries no evidence about
    /// this fixture; the headless twin (<see cref="WaterReflectionWarp"/>) and the shader compile
    /// guard are what CI does prove.</para>
    /// </summary>
    public class ObjectReflectionProbeTests
    {
        /// <summary>An otherwise-unused layer: EditMode fixtures share a scene and other tests'
        /// leftovers must not photobomb the readback (the lesson the sprite-matrix guard learned).</summary>
        const int ProbeLayer = 31;
        const int Size = 128;          // 128 px at PPU 32 = a 4 m window — a boat-sized view
        const float Ppu = 32f;

        GameObject _spriteGo, _camGo, _seaGo;
        Camera _cam;
        RenderTexture _rt;
        Texture2D _sheet;
        Sprite _sprite;
        Material _material, _seaMaterial;

        [TearDown]
        public void TearDown()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            if (_spriteGo != null) Object.DestroyImmediate(_spriteGo);
            if (_seaGo != null) Object.DestroyImmediate(_seaGo);
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            if (_sprite != null) Object.DestroyImmediate(_sprite);
            if (_sheet != null) Object.DestroyImmediate(_sheet);
            if (_material != null) Object.DestroyImmediate(_material);
            if (_seaMaterial != null) Object.DestroyImmediate(_seaMaterial);
            _spriteGo = _camGo = _seaGo = null; _cam = null; _sprite = null; _sheet = null;
            _material = _seaMaterial = null;
        }

        /// <summary>
        /// A sea quad running the REAL water material, tuned to the calmest, plainest sea the
        /// shader can make: object reflections at full strength, a FLAT mirror (no wave warp — this
        /// probe is about the silhouette, not the wobble), no depth sink, no palette grade and no
        /// deep-blue pull, so what lands on the water is the reflection and not the shader's own
        /// art direction. Everything set here is a material property (rule 6) on a THROWAWAY copy —
        /// the committed Water.mat is never touched.
        /// </summary>
        void MakeSea()
        {
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Materials/Water.mat");
            Assert.IsNotNull(source, "the hero water material must exist for this probe.");
            _seaMaterial = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            _seaMaterial.SetFloat("_ObjectReflectStrength", 1f);
            _seaMaterial.SetFloat("_ObjectReflectWarp", 0f);     // the flat mirror the probe judges
            _seaMaterial.SetFloat("_ObjectReflectSink", 0f);
            _seaMaterial.SetFloat("_ReflectionStrength", 1f);    // the sea-state master: glass
            _seaMaterial.SetFloat("_Chop", 0f);
            _seaMaterial.SetFloat("_Roughness", 0f);
            _seaMaterial.SetFloat("_WaterLevel", 0f);
            _seaMaterial.SetFloat("_PaletteGradeStrength", 0f);
            _seaMaterial.SetFloat("_DeepBlueStrength", 0f);
            _seaMaterial.SetFloat("_SkyReflectionStrength", 0f);
            _seaMaterial.SetFloat("_FoamThreshold", 2f);         // keep foam off the readback
            _seaMaterial.DisableKeyword("_USE_HEIGHTTEX");
            _seaMaterial.SetFloat("_UseHeightTex", 0f);          // no height map => uniformly deep

            _seaGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _seaGo.name = "ProbeSea";
            _seaGo.layer = ProbeLayer;
            Object.DestroyImmediate(_seaGo.GetComponent<Collider>());
            _seaGo.transform.position = new Vector3(0f, 0f, 0.5f);
            _seaGo.transform.localScale = new Vector3(Size / Ppu * 2f, Size / Ppu * 2f, 1f);
            var mr = _seaGo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _seaMaterial;
            mr.sortingOrder = -100;   // under the sprite (URP 2D sorts renderers, not just sprites)
        }

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — this run has no graphics device (Renderer: Null " +
                    "Device), so the HHReflect pass could not render and proved nothing. Expected " +
                    "on CI; the ADR 0027 #8 fidelity probe only runs on a machine with a GPU.");
            }
        }

        [Test]
        public void FidelityProbe_AFlatMirroredSilhouette_ReadsAtPpu32()
        {
            RequireAGraphicsDevice();

            var shader = Shader.Find("HiddenHarbours/TreeWind");
            Assert.IsNotNull(shader, "the reflective sprite shader must exist for this probe.");

            // ---- an unmistakable source: a solid block sitting ON the pivot, taller than wide ----
            // Deliberately NOT tree art. The probe answers "does a mirrored silhouette read", and a
            // synthetic block makes "is row d below the pivot the mirror of row d above it" an
            // exact question instead of an eyeballed one.
            const int cellW = 16, cellH = 32;
            _sheet = new Texture2D(cellW, cellH, TextureFormat.RGBA32, false, false)
            { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < cellH; y++)
            for (int x = 0; x < cellW; x++)
            {
                // A wedge: wide at the base, narrow at the top, so a VERTICAL flip is detectable
                // (a symmetric shape would pass a mirror test even unmirrored).
                int halfWidth = Mathf.Max(1, (cellW / 2) - (y * cellW) / (2 * cellH));
                bool inside = Mathf.Abs(x - cellW / 2) < halfWidth;
                _sheet.SetPixel(x, y, inside ? new Color(0.9f, 0.3f, 0.2f, 1f) : Color.clear);
            }
            _sheet.Apply(false, false);
            // Pivot at bottom-centre = the ground contact (the ADR 0026 convention).
            _sprite = Sprite.Create(_sheet, new Rect(0, 0, cellW, cellH), new Vector2(0.5f, 0f), Ppu,
                                    0, SpriteMeshType.FullRect);

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _material.SetFloat("_AlphaClip", 0.01f);
            _material.SetFloat("_LightResponse", 0f);      // the probe is about geometry, not light
            _material.SetFloat("_IdleSway", 0f);
            _material.SetFloat("_SwayAmount", 0f);

            MakeSea();

            _spriteGo = new GameObject("ProbeReflector") { layer = ProbeLayer };
            _spriteGo.transform.position = Vector3.zero;   // the pivot sits at the world origin's Y
            var sr = _spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.sharedMaterial = _material;
            var reflector = _spriteGo.AddComponent<ReflectiveObject>();
            reflector.Refresh();
            Assert.AreEqual(1f, ReflectiveObject.OriginOn(sr).w, 1e-5f,
                "the probe is meaningless unless the mirror axis actually reached the renderer.");

            // ---- the camera: the pivot at mid-height, so source and reflection both fit ----
            _camGo = new GameObject("ProbeCam") { layer = ProbeLayer };
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = Size / (2f * Ppu);
            _cam.transform.position = new Vector3(0f, 0f, -100f);
            _cam.nearClipPlane = 1f;
            _cam.farClipPlane = 400f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Color.clear;
            _cam.cullingMask = 1 << ProbeLayer;
            _cam.allowHDR = true;      // the reflect target is ARGBHalf; do not force an 8-bit path
            _cam.allowMSAA = false;
            _rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGBHalf)
            { filterMode = FilterMode.Point };
            _cam.targetTexture = _rt;

            // The 2D renderer's HHReflect list is only recorded when something is reflective. It is
            // — we just made one — so this render exercises the real pass.
            Assert.Greater(ReflectionRegistry.Count, 0,
                "the feature's only gate is the registry count; with 0 there is no pass to probe.");

            // Two renders: the first can pay a cold shader-cache compile, which has faked a
            // regression on this repo before. The SECOND is the one that is read.
            _cam.Render();
            _cam.Render();

            Color32[] px = ReadBack();

            // ---- what the probe measures ------------------------------------------------------
            // The sea is OPAQUE, so "is the reflection there" cannot be an alpha question — it is a
            // COLOUR one. The reflector is warm red (0.9, 0.3, 0.2) over a cool sea, so a pixel
            // whose red clearly beats its blue is reflected reflector. GetPixels32 is BOTTOM-left
            // origin and the camera is centred on the pivot, so rows [0, Size/2) are BELOW the
            // waterline (the reflection) and (Size/2, Size) ABOVE it (the source itself).
            int mid = Size / 2;
            int below = WarmIn(px, 0, mid);
            int above = WarmIn(px, mid, Size);

            string png = WriteProbePng(px);
            TestContext.WriteLine($"[ADR 0027 #8 fidelity probe] PPU {Ppu}, {Size}x{Size}, flat " +
                                  $"mirror (warp 0), glass sea. Warm px ABOVE the waterline " +
                                  $"(the source) = {above}; BELOW (the reflection) = {below}. " +
                                  $"Frame: {png}");

            Assert.Greater(above, 50, "the SOURCE must draw, or the probe is measuring nothing.");
            Assert.Greater(below, 50,
                "nothing reflected below the waterline: either the HHReflect pass drew nothing, " +
                "the water never sampled it, or the mirror collapsed the geometry (an unpublished " +
                "_HHReflectOrigin does exactly that, deliberately).");

            // It is a MIRROR, not an offset copy: the wedge is wide at its base and narrow at its
            // top, so the reflection must be WIDE next to the waterline and narrow far from it.
            // A translated copy would taper the SAME way as the source and fail this.
            int wideSource = WarmRowWidth(px, mid + 2);
            int narrowSource = WarmRowWidth(px, Size - 6);
            int wideReflection = WarmRowWidth(px, mid - 3);
            int narrowReflection = WarmRowWidth(px, 4);
            TestContext.WriteLine($"  taper — source {wideSource} px at the waterline -> " +
                                  $"{narrowSource} px at the crown; reflection {wideReflection} -> " +
                                  $"{narrowReflection}.");
            Assert.Greater(wideSource, narrowSource, "the fixture's own wedge must taper upward.");
            Assert.Greater(wideReflection, narrowReflection,
                "the reflection must taper AWAY from the waterline — i.e. it is mirrored, not " +
                "merely translated.");

            // …and BOUNDED: the sea in the far corners, nowhere near the reflector, is untouched.
            Assert.AreEqual(0, WarmRowWidth(px, 1, 0, 8),
                "the reflection must not bleed across open water.");
        }

        Color32[] ReadBack()
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Color32[] px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            return px;
        }

        /// <summary>A pixel that has taken the reflector's warmth: red clearly beating blue over a
        /// cool sea. The margin is generous on purpose — the probe asks "does it READ", not "is it
        /// byte-exact", and a tight threshold would turn a legitimate tint change into a red test.</summary>
        static bool IsWarm(Color32 c) => c.r > c.b + 40 && c.a > 8;

        static int WarmIn(Color32[] px, int yFrom, int yTo)
        {
            int n = 0;
            for (int y = yFrom; y < yTo; y++)
            for (int x = 0; x < Size; x++)
                if (IsWarm(px[y * Size + x])) n++;
            return n;
        }

        static int WarmRowWidth(Color32[] px, int y, int xFrom = 0, int xTo = Size)
        {
            int n = 0;
            for (int x = xFrom; x < xTo; x++)
                if (IsWarm(px[y * Size + x])) n++;
            return n;
        }

        /// <summary>Write the probe frame so it can be LOOKED at — "does it read?" is finally a
        /// question about pixels. Goes to <c>artifacts/</c>, which is git-ignored: the answer belongs
        /// in the PR body, not in the repo.</summary>
        static string WriteProbePng(Color32[] px)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "adr0027-8-reflection-probe.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }
    }
}
