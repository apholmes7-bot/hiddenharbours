using System.Collections;
using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // The seals on the Whelps, shot over the skerries at low, mid and high water and with a hull close, 32 px a
    // metre. Shares the GPU/title-only harness. Local evidence only: it asserts what it can (no seal drawn on a
    // wet rock) and writes the plates for a person to look at.
    public partial class WharfBuildingPass2PlatePlayTests
    {
        const string SealPlatesVariable = "HH_SEAL_PLATES";

        readonly struct SealShot
        {
            public readonly string Id; public readonly float Tide; public readonly bool HullClose;
            public SealShot(string id, float tide, bool hullClose) { Id = id; Tide = tide; HullClose = hullClose; }
        }

        static readonly SealShot[] SealShots =
        {
            new SealShot("seals.low", -2.0f, false),
            new SealShot("seals.mid", -1.2f, false),
            new SealShot("seals.high", 0.6f, false),
            new SealShot("seals.low.hull_close", -2.0f, true),
        };

        // The skerries board's frame (whelps_lookout.skerries.*).
        const float SealX0 = 172f, SealX1 = 200f, SealY0 = -57.75f, SealY1 = -42f;

        /// <summary>Where the hull is put for the close shot: on the flat between the ledges, about 5 m off the
        /// first ledge's southern seals and the third's eastern ones, inside the frame.</summary>
        static readonly Vector2 HullCloseAt = new Vector2(186f, -52.5f);

        const float MidSweepFrom = -1.6f, MidSweepTo = -0.3f, MidSweepStep = 0.1f;

        [UnityTest]
        public IEnumerator StPeters_TheSealsOnTheWhelps_AtLowMidAndHighWater_AndWithAHullClose()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            EnsureTheFollow();
            HoldTheMood();
            _look = "seals";
            _lookEvidence = "the skerries board's frame, 32 px a metre, the follow and the pixel camera off";

            var host = Object.FindAnyObjectByType<SealHaulout>();
            Assert.IsNotNull(host, "St Peters holds no seal haul-out.");
            Assert.IsNotNull(host.Haulout, "the haul-out host has no haul-out.");

            string dir = System.Environment.GetEnvironmentVariable(SealPlatesVariable);
            if (string.IsNullOrEmpty(dir)) dir = PlatePath();
            Directory.CreateDirectory(dir);

            IEnvironmentService held = GameServices.Environment;
            bool followEnabled = _follow.enabled;
            float aspect = _cam.aspect, ortho = _cam.orthographicSize;
            Vector3 camAt = _cam.transform.position;
            var pixelCameras = _cam.GetComponents<Behaviour>()
                .Where(b => b.GetType().Name == "PixelPerfectCamera" && b.enabled).ToArray();
            Transform hull = null; Vector3 hullWas = Vector3.zero;
            try
            {
                foreach (SealShot shot in SealShots)
                {
                    yield return PinTheHour(Noon, ClearRunSeconds);
                    GameServices.Environment = new LandingBoardTide(held, shot.Tide);
                    Time.timeScale = 1f;
                    float until = Time.realtimeSinceStartup + 20f;
                    do { yield return null; }
                    while (Time.realtimeSinceStartup < until &&
                           Mathf.Abs(Shader.GetGlobalVector("_HHSeaLevelWorld").x - shot.Tide) > .01f);

                    float tide = shot.Tide;
                    if (shot.Id == "seals.mid")
                    {
                        // The middle water where the schedule has some out and some heads up: swept, not guessed.
                        int best = -1;
                        for (float level = MidSweepFrom; level <= MidSweepTo + 1e-3f; level += MidSweepStep)
                        {
                            GameServices.Environment = new LandingBoardTide(held, level);
                            host.Haulout = host.Haulout;
                            host.TickNow(0.01f);
                            int hauled = 0, wet = 0;
                            for (int i = 0; i < host.SpotCount; i++)
                            {
                                if (host.PlaceOf(i) == SealPlace.Hauled) hauled++;
                                else if (host.PlaceOf(i) == SealPlace.InWater && host.SwimWet(i)) wet++;
                            }
                            int score = hauled > 0 && wet > 0 ? 100 + Mathf.Min(hauled, wet) : wet;
                            if (score > best) { best = score; tide = level; }
                        }
                        GameServices.Environment = new LandingBoardTide(held, tide);
                        Debug.Log($"[SealPlates] mid water swept to {tide:0.00} (score {best})");
                    }

                    // Every seal starts again from the schedule at this tide, as on a region load.
                    host.Haulout = host.Haulout;
                    if (shot.HullClose)
                    {
                        hull = NearestHull(new Vector2(186f, -50f));
                        Assert.IsNotNull(hull, "no hull in St Peters to bring close.");
                        hullWas = hull.position;
                    }

                    _follow.enabled = false;
                    foreach (Behaviour ppc in pixelCameras) ppc.enabled = false;
                    int w = Mathf.RoundToInt((SealX1 - SealX0) * BoardPixelsPerMetre);
                    int h = Mathf.RoundToInt((SealY1 - SealY0) * BoardPixelsPerMetre);
                    if (_rt == null || _rt.width != w || _rt.height != h)
                    {
                        if (_rt != null) { _cam.targetTexture = null; _rt.Release(); Object.DestroyImmediate(_rt); }
                        _w = w; _h = h;
                        _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
                        _rt.Create();
                    }
                    _cam.targetTexture = _rt;
                    _cam.transform.position = new Vector3((SealX0 + SealX1) * 0.5f, (SealY0 + SealY1) * 0.5f, _cam.transform.position.z);
                    _cam.orthographicSize = (SealY1 - SealY0) * 0.5f;
                    _cam.aspect = (SealX1 - SealX0) / (SealY1 - SealY0);

                    float run = Time.unscaledTime + 1.5f;
                    while (Time.unscaledTime < run) yield return null;
                    if (shot.HullClose)
                    {
                        // Shot while they react: the first frame any seal is alert or slipping (or 4 s).
                        run = Time.unscaledTime + 4f;
                        bool reacting = false;
                        while (Time.unscaledTime < run && !reacting)
                        {
                            hull.position = new Vector3(HullCloseAt.x, HullCloseAt.y, hull.position.z);
                            yield return null;
                            for (int i = 0; i < host.SpotCount; i++)
                                reacting |= host.PhaseOf(i) == SealPhase.Alert || host.PhaseOf(i) == SealPhase.SlidingIn;
                        }
                        run = Time.unscaledTime + 0.35f;
                        while (Time.unscaledTime < run) { hull.position = new Vector3(HullCloseAt.x, HullCloseAt.y, hull.position.z); yield return null; }
                        Debug.Log($"[SealPlates] hull '{hull.name}' held at {hull.position}; reacting={reacting}");
                    }
                    yield return ShadersReady();
                    Time.timeScale = 0f;
                    for (int i = 0; i < 2; i++) yield return null;

                    // The rule a picture can break: no seal lying on a rock the water covers.
                    for (int i = 0; i < host.SpotCount; i++)
                    {
                        SealPhase p = host.PhaseOf(i);
                        if (p != SealPhase.Resting && p != SealPhase.Alert) continue;
                        float lie = host.Haulout.Spots[i].LieMetres;
                        Assert.IsTrue(TidalExposure.IsExposed(tide, lie),
                            $"{shot.Id}: {host.Haulout.Spots[i].Id} lies on a rock at {lie} under water at {tide}");
                    }

                    var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
                    tex.LoadRawTextureData(Capture());
                    tex.Apply();
                    string path = Path.Combine(dir, shot.Id + ".png");
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    string phases = string.Join(" ", Enumerable.Range(0, host.SpotCount)
                        .Select(i => host.Haulout.Spots[i].Id + ":" + host.PhaseOf(i)));
                    Debug.Log($"[SealPlates] plate written: {path} (tide {tide:0.00}) {phases}");
                    if (hull != null) { hull.position = hullWas; hull = null; }
                    Time.timeScale = 1f;
                }
                Debug.Log("[SealPlates] PLATES_COMPLETE " + dir);
            }
            finally
            {
                if (hull != null) hull.position = hullWas;
                GameServices.Environment = held;
                _follow.enabled = followEnabled;
                foreach (Behaviour ppc in pixelCameras) if (ppc != null) ppc.enabled = true;
                _cam.aspect = aspect;
                _cam.orthographicSize = ortho;
                _cam.transform.position = camAt;
                Time.timeScale = 1f;
            }
        }

        static Transform NearestHull(Vector2 near)
        {
            Transform best = null; float bestD = float.PositiveInfinity;
            var hulls = HullPresences.Active;
            for (int i = 0; i < hulls.Count; i++)
            {
                if (!(hulls[i] is Component c) || c == null) continue;
                float d = Vector2.Distance(hulls[i].Footprint.Center, near);
                if (d < bestD) { bestD = d; best = c.transform; }
            }
            return best;
        }
    }
}
