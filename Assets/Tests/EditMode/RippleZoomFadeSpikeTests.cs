using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.App;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// SPIKE (ADR 0027 #10, report only) — <b>can a ~0.10 m capillary ripple band survive the discrete
    /// pixel-perfect zoom tiers?</b> The ADR makes this the item's kill condition: *"prototype the
    /// per-tier amplitude fade first — if it cannot be made stable, DROP the item."*
    ///
    /// <para><b>This fixture measures; it ships no shader change.</b> The ripple band is prototyped by
    /// pushing the water shader's EXISTING finest octave (<c>_WindChopScale</c>) to ripple wavelength on
    /// a throwaway material copy — no new HLSL, nothing committed to <c>Water.mat</c>. The findings note
    /// is the deliverable (<c>dev/NOTE-ripples-zoom-fade-spike.md</c>).</para>
    ///
    /// <para><b>The headline result is arithmetic, and it retires the kill condition.</b>
    /// <c>PixelPerfectCamera.assetsPPU</c> is LOCKED at 32 and the camera frames a tier by changing its
    /// REFERENCE RESOLUTION, not by making each pixel cover more world. So world-metres-per-pixel is
    /// <c>1/32 = 3.125 cm</c> at every tier, and a 0.10 m ripple is <b>3.2 px at every tier</b> — from
    /// the 8.4 m on-foot framing to the 33.75 m Coastal Packet framing. The ADR's premise ("at a wider
    /// zoom tier a ripple falls below one pixel") describes a camera that changes orthographic size at
    /// fixed screen resolution. That is not this camera.</para>
    ///
    /// <para>CI has no graphics device, so the RENDER half gates and skips loudly; the arithmetic half is
    /// pure and runs anywhere.</para>
    /// </summary>
    public class RippleZoomFadeSpikeTests
    {
        const int ProbeLayer = 31;
        const float RippleWavelengthM = 0.10f;      // the ADR's band: 0.08–0.15 m
        const int Ppu = CameraFollow.AssetsPPU;     // 32, locked

        GameObject _seaGo, _camGo;
        Camera _cam;
        RenderTexture _rt;
        Material _sea;

        [TearDown]
        public void TearDown()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            if (_seaGo != null) Object.DestroyImmediate(_seaGo);
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            if (_sea != null) Object.DestroyImmediate(_sea);
            _seaGo = _camGo = null; _cam = null; _sea = null;
        }

        /// <summary>Every framing the game actually asks for: the on-foot/deck steps and each hull's
        /// authored <c>CameraWorldHeightMeters</c>, from the Dory to the Coastal Packet.</summary>
        static readonly (string Name, float WorldHeight)[] Framings =
        {
            ("deck (tightest)", 6f), ("on foot", 8f), ("Dory", 14f), ("FishingSkiff", 13.5f),
            ("PuntUpgraded", 17f), ("ConsoleSkiff", 18.5f), ("SportSkiff", 19f),
            ("LobsterBoat", 23f), ("CapeIslander", 24f), ("SideDragger", 40f),
            ("SternTrawler", 60f), ("CoastalPacket", 90f),
        };

        [Test]
        public void ArithmeticHalf_TheRipplesPixelFootprintIsTIERINVARIANT()
        {
            var report = new StringBuilder();
            report.AppendLine("framing              want m  zoom  refH px  live m   m/px     ripple px");
            float minPx = float.MaxValue, maxPx = float.MinValue;

            foreach (var (name, want) in Framings)
            {
                CameraFollow.ReferenceResolutionForWorldHeight(want, out _, out int refH);
                int zoom = Mathf.Max(1, CameraFollow.DesignScreenHeightPx / Mathf.Max(1, refH));
                float live = CameraFollow.WorldHeightAtZoom(CameraFollow.DesignScreenHeightPx, zoom, Ppu);
                float metresPerPixel = live / refH;          // world height over the internal buffer
                float ripplePx = RippleWavelengthM / metresPerPixel;
                minPx = Mathf.Min(minPx, ripplePx);
                maxPx = Mathf.Max(maxPx, ripplePx);
                report.AppendLine($"{name,-20}{want,7:0.#}{zoom,6}{refH,9}{live,8:0.00}" +
                                  $"{metresPerPixel * 100,7:0.000}cm{ripplePx,10:0.00}");
            }
            TestContext.WriteLine("[ripple zoom spike] " + report);

            // THE FINDING: the footprint does not vary. assetsPPU is locked, so a tier changes the
            // reference resolution, never the world-per-pixel ratio.
            Assert.AreEqual(minPx, maxPx, 0.01f,
                $"the ripple's pixel footprint must be tier-INVARIANT; measured {minPx:0.00}..{maxPx:0.00} px.");
            Assert.AreEqual(1f / Ppu, RippleWavelengthM / minPx, 1e-4f,
                "and it must equal 1/assetsPPU — the locked pixel grid.");

            // …and it never goes sub-pixel, which is the condition the ADR feared.
            Assert.Greater(minPx, 2f,
                $"a {RippleWavelengthM * 100:0}cm ripple resolves at {minPx:0.00} px — the ADR's " +
                "sub-pixel kill condition does not arise on this camera.");
        }

        [Test]
        public void ArithmeticHalf_WhatDoesVaryIsHOWMUCHSeaIsOnScreen()
        {
            // The real remaining risk is DENSITY, not aliasing: the widest framing shows ~5x more world
            // than the tightest, so ~5x more ripple cycles at the same pixel size. That is a busyness /
            // moire question the ADR's threshold-dither addresses, NOT a sub-pixel one.
            CameraFollow.ReferenceResolutionForWorldHeight(6f, out _, out int tightRefH);
            CameraFollow.ReferenceResolutionForWorldHeight(90f, out _, out int wideRefH);
            float tight = tightRefH / (float)Ppu;
            float wide = wideRefH / (float)Ppu;
            float cyclesTight = tight / RippleWavelengthM;
            float cyclesWide = wide / RippleWavelengthM;
            TestContext.WriteLine(
                $"[ripple zoom spike] world height {tight:0.0} m -> {wide:0.0} m across the tiers; " +
                $"ripple cycles down-screen {cyclesTight:0} -> {cyclesWide:0} " +
                $"({cyclesWide / cyclesTight:0.0}x more). The pixel SIZE is unchanged; only the COUNT grows.");
            Assert.Greater(cyclesWide, cyclesTight * 2f,
                "the density delta is the finding worth reporting — pin it so it cannot silently shrink.");
        }

        [Test]
        public void RenderHalf_TheBandReadsAtBothTierExtremes()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device; the spike's render half needs a GPU.");

            // Prototype the band with the shader's EXISTING finest octave pushed to ripple wavelength.
            // No new HLSL: the spike must not ship a shader change, and this is the same noise machinery
            // a real #10 band would use.
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Materials/Water.mat");
            Assert.IsNotNull(source);
            _sea = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            _sea.SetFloat("_WindChopScale", 1f / RippleWavelengthM);   // 10 lanes/unit = a 0.10 m band
            _sea.SetFloat("_WindChop", 0.45f);                         // well above the shipped 0.07
            _sea.SetFloat("_Roughness", 0.5f);
            _sea.SetFloat("_Chop", 0.3f);
            _sea.SetFloat("_WaterLevel", 0f);
            _sea.SetFloat("_UseHeightTex", 0f);
            _sea.DisableKeyword("_USE_HEIGHTTEX");

            foreach (var (name, want) in new[] { ("tight-6m", 6f), ("wide-90m", 90f) })
            {
                CameraFollow.ReferenceResolutionForWorldHeight(want, out int refW, out int refH);
                Shoot(refW, refH, want, name);
            }
            TestContext.WriteLine("[ripple zoom spike] frames in artifacts/ripple-spike-*.png");
        }

        void Shoot(int refW, int refH, float worldHeight, string name)
        {
            if (_seaGo == null)
            {
                _seaGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _seaGo.name = "SpikeSea";
                _seaGo.layer = ProbeLayer;
                Object.DestroyImmediate(_seaGo.GetComponent<Collider>());
                _seaGo.GetComponent<MeshRenderer>().sharedMaterial = _sea;
                _camGo = new GameObject("SpikeCam") { layer = ProbeLayer };
                _cam = _camGo.AddComponent<Camera>();
                _cam.orthographic = true;
                _cam.transform.position = new Vector3(0f, 0f, -100f);
                _cam.nearClipPlane = 1f;
                _cam.farClipPlane = 400f;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = Color.clear;
                _cam.cullingMask = 1 << ProbeLayer;
                _cam.allowMSAA = false;
            }
            // The camera frames `worldHeight` of sea into an refH-tall internal buffer — exactly what the
            // PixelPerfectCamera does at this tier, at 1/32 m per pixel.
            float live = refH / (float)Ppu;
            _cam.orthographicSize = live * 0.5f;
            _seaGo.transform.position = new Vector3(0f, 0f, 0.5f);
            _seaGo.transform.localScale = new Vector3(live * 3f, live * 3f, 1f);

            // Detach BEFORE releasing: Unity logs an error for releasing a live targetTexture, and an
            // unhandled error log fails the run for the wrong reason.
            if (_rt != null) { _cam.targetTexture = null; _rt.Release(); Object.DestroyImmediate(_rt); }
            _rt = new RenderTexture(refW, refH, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            _cam.targetTexture = _rt;
            _cam.Render();
            _cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(refW, refH, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, refW, refH), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            string dir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, $"ripple-spike-{name}.png"), tex.EncodeToPNG());
            TestContext.WriteLine(
                $"  {name}: refRes {refW}x{refH}, world height {live:0.00} m, " +
                $"{1000f / Ppu:0.#} mm/px, ripple {RippleWavelengthM / (1f / Ppu):0.0} px, " +
                $"{live / RippleWavelengthM:0} cycles down-screen");
            Object.DestroyImmediate(tex);
        }
    }
}
