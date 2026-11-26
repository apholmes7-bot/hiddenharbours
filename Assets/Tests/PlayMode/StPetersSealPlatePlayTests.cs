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
    // The seals on the Whelps, shot over the skerries at low, mid and high water and with the dory coming in at mid
    // water (once as they react, once as they slip in), 32 px a metre. Shares the GPU/title-only harness. Local
    // evidence only: it asserts what it can (no seal lying on a wet rock) and writes the plates for a person to judge.
    public partial class WharfBuildingPass2PlatePlayTests
    {
        const string SealPlatesVariable = "HH_SEAL_PLATES";

        readonly struct SealShot
        {
            public readonly string Id; public readonly float Tide; public readonly bool MidWater; public readonly bool Dory;
            public SealShot(string id, float tide, bool midWater, bool dory) { Id = id; Tide = tide; MidWater = midWater; Dory = dory; }
        }

        static readonly SealShot[] SealShots =
        {
            new SealShot("seals.low", -2.0f, false, false),
            new SealShot("seals.mid", -1.0f, true, false),
            new SealShot("seals.high", 0.6f, false, false),
            new SealShot("seals.mid.dory", -1.0f, true, true),
        };

        // The skerries board's frame (whelps_lookout.skerries.*).
        const float SealX0 = 172f, SealX1 = 200f, SealY0 = -57.75f, SealY1 = -42f;

        /// <summary>Where the dory is held for the approach: in the water east of the south ledge, about 5 m off
        /// its seals.</summary>
        static readonly Vector2 DoryAt = new Vector2(186f, -53.5f);

        const float MidSweepFrom = -1.6f, MidSweepTo = -0.3f, MidSweepStep = 0.1f;

        /// <summary>Seconds after the first head shows in the water that the "slipping in" plate is shot.</summary>
        const float SlipPlateSeconds = 0.4f;

        /// <summary>The approach: from ~25 m east of the south ledge's seals, rowed in easily (under the species'
        /// startle speed) to ~10 m or the first alert, then hard in to <see cref="DoryAt"/>.</summary>
        static readonly Vector2 DoryFrom = new Vector2(206f, -53.5f), DoryAlertStop = new Vector2(191f, -53.5f);
        const float DoryEasySpeed = 1.0f, DoryHardSpeed = 3.0f;

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
            float midWater = float.NaN;
            try
            {
                foreach (SealShot shot in SealShots)
                {
                    yield return PinTheHour(Noon, ClearRunSeconds);
                    float tide = shot.Tide;
                    if (shot.MidWater)
                    {
                        if (float.IsNaN(midWater)) midWater = SweepMidWater(host, held);
                        tide = midWater;
                    }
                    GameServices.Environment = new LandingBoardTide(held, tide);
                    Time.timeScale = 1f;
                    float until = Time.realtimeSinceStartup + 20f;
                    do { yield return null; }
                    while (Time.realtimeSinceStartup < until &&
                           Mathf.Abs(Shader.GetGlobalVector("_HHSeaLevelWorld").x - tide) > .01f);

                    // Every seal starts again from the schedule at this tide, as on a region load.
                    host.Haulout = host.Haulout;
                    if (shot.Dory)
                    {
                        hull = NearestHull(DoryAt);
                        Assert.IsNotNull(hull, "no hull in St Peters to bring close.");
                        hullWas = hull.position;
                    }
                    FrameTheSkerries(pixelCameras);

                    float run = Time.unscaledTime + 1.5f;
                    while (Time.unscaledTime < run) yield return null;
                    if (!shot.Dory)
                    {
                        yield return ShootSealPlate(host, dir, shot.Id, tide);
                        continue;
                    }

                    // The dory comes in from the east as a player would: from outside the alert ring at an easy row
                    // (under the startle speed) until a seal looks up, then hard in until they slip off and heads
                    // show in the water.
                    Vector2 at = DoryFrom;
                    run = Time.unscaledTime + 2f;
                    while (Time.unscaledTime < run) { hull.position = new Vector3(at.x, at.y, hull.position.z); yield return null; }
                    bool alert = false;
                    while (!alert && at.x > DoryAlertStop.x)
                    {
                        at = Vector2.MoveTowards(at, DoryAlertStop, DoryEasySpeed * Time.deltaTime);
                        hull.position = new Vector3(at.x, at.y, hull.position.z);
                        yield return null;
                        for (int i = 0; i < host.SpotCount; i++) alert |= host.PhaseOf(i) == SealPhase.Alert;
                    }
                    Debug.Log($"[SealPlates] dory '{hull.name}' at {hull.position}; alert={alert}");
                    yield return ShootSealPlate(host, dir, shot.Id + "_alert", tide);
                    bool heads = false;
                    run = Time.unscaledTime + 8f;
                    while (!heads && Time.unscaledTime < run)
                    {
                        at = Vector2.MoveTowards(at, DoryAt, DoryHardSpeed * Time.deltaTime);
                        hull.position = new Vector3(at.x, at.y, hull.position.z);
                        yield return null;
                        for (int i = 0; i < host.SpotCount; i++)
                            heads |= host.PhaseOf(i) == SealPhase.Swimming && host.SwimWet(i);
                    }
                    run = Time.unscaledTime + SlipPlateSeconds;
                    while (Time.unscaledTime < run) { hull.position = new Vector3(at.x, at.y, hull.position.z); yield return null; }
                    Debug.Log($"[SealPlates] dory at {hull.position}; heads={heads}");
                    yield return ShootSealPlate(host, dir, shot.Id + "_slip", tide);
                    hull.position = hullWas; hull = null;
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

        /// <summary>The middle water where the schedule has some seals out and some heads up: swept, not guessed.</summary>
        static float SweepMidWater(SealHaulout host, IEnvironmentService held)
        {
            int best = -1; float tide = MidSweepFrom;
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
            Debug.Log($"[SealPlates] mid water swept to {tide:0.00} (score {best})");
            return tide;
        }

        void FrameTheSkerries(Behaviour[] pixelCameras)
        {
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
        }

        IEnumerator ShootSealPlate(SealHaulout host, string dir, string id, float tide)
        {
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
                    $"{id}: {host.Haulout.Spots[i].Id} lies on a rock at {lie} under water at {tide}");
            }

            // One pixel size on screen: every drawn seal at its art's own size (no scale, no rotation) and the
            // frame at the sprites' pixels per metre, so one sheet pixel lands on one screen pixel.
            float screenPxPerMetre = _h / (2f * _cam.orthographicSize);
            // The seals' own renderers are the host's direct children; the world's shadow casters hang their own
            // quads under them, and those are the shadow system's to judge (logged, not asserted).
            SpriteRenderer[] seals = Enumerable.Range(0, host.transform.childCount)
                .Select(c => host.transform.GetChild(c).GetComponent<SpriteRenderer>())
                .Where(sr => sr != null && sr.gameObject.activeInHierarchy).ToArray();
            var others = host.GetComponentsInChildren<SpriteRenderer>(false).Where(sr => sr.enabled && !seals.Contains(sr))
                .Select(sr => $"{sr.name}(ppu {(sr.sprite != null ? sr.sprite.pixelsPerUnit : 0f)}, scale {sr.transform.lossyScale}, " +
                              $"filter {(sr.sprite != null ? sr.sprite.texture.filterMode.ToString() : "-")})").Distinct().ToArray();
            if (others.Length > 0) Debug.Log($"[SealPlates] {id} other quads under the seals: {string.Join("; ", others.Take(4))}");
            foreach (SpriteRenderer sr in seals)
            {
                Assert.IsTrue((sr.transform.lossyScale - Vector3.one).sqrMagnitude < 1e-8f, $"{id}: {sr.name} is scaled {sr.transform.lossyScale}");
                Assert.IsTrue(Quaternion.Angle(sr.transform.rotation, Quaternion.identity) < 1e-3f, $"{id}: {sr.name} is rotated");
                Assert.IsTrue(sr.drawMode == SpriteDrawMode.Simple, $"{id}: {sr.name} is not drawn simple");
                if (sr.sprite != null)
                    Assert.AreEqual(sr.sprite.pixelsPerUnit, screenPxPerMetre, 0.01f, $"{id}: {sr.name} sheet px a metre vs the frame's");
            }
            Debug.Log($"[SealPlates] {id} pixels: frame {screenPxPerMetre:0.###} px/m (board {BoardPixelsPerMetre}), " +
                      $"host scale {host.transform.lossyScale}, seals drawn {seals.Length}");

            var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(Capture());
            tex.Apply();
            string path = Path.Combine(dir, id + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            string phases = string.Join(" ", Enumerable.Range(0, host.SpotCount)
                .Select(i => host.Haulout.Spots[i].Id + ":" + host.PhaseOf(i)));
            Debug.Log($"[SealPlates] plate written: {path} (tide {tide:0.00}) {phases}");
            Time.timeScale = 1f;
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
