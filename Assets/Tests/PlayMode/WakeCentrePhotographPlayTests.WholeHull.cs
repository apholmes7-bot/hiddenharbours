using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    // The whole hull makes foam (the owner, 2026-11-02: "i would like the whole hull body to be responsive
    // to making wake/foam, right now it just comes from stern from moving forward and looks off when going
    // in reverse"). The cape is driven through three manoeuvres, each one twice on its own patch of the same
    // water at the same hour: once with the hull-foam dials off (the trail alone, always from the transom,
    // as the game drew it before) and once with them as shipped. Each run is photographed with every foam
    // family drawn and with the advected sheet alone, through the game's own camera.
    //
    // Assertions are on the instrument only: which end the injector says it sheds her trail from. The look
    // is for the plates.
    public partial class WakeCentrePhotographPlayTests
    {
        readonly struct Manoeuvre
        {
            public readonly string Name;
            /// <summary>Metres a second in her own frame: x to starboard, y ahead.</summary>
            public readonly Vector2 Through;
            public readonly float TurnDegPerSec;
            public Manoeuvre(string name, float sway, float surge, float turnDegPerSec)
            { Name = name; Through = new Vector2(sway, surge); TurnDegPerSec = turnDegPerSec; }
        }

        const float WholeHullSeconds = 4f;
        const int RenderTimingFrames = 30;

        [UnityTest, Timeout(600000)]
        public IEnumerator TheCape_AheadAsternAndSliding_PhotographsTheWholeHullsFoam()
        {
            RequireAGraphicsDevice();   // FIRST, before any yield
            BoatHullDef hull = LoadAsset<BoatHullDef>(CapeHullPath);
            BoatVisualDef visual = LoadAsset<BoatVisualDef>(CapeVisualPath);
            if (hull == null || visual == null) yield break;

            yield return LoadRegion();
            yield return SetHour(ShotHour);

            var manoeuvres = new[]
            {
                new Manoeuvre("ahead", 0f, 4f, 0f),
                new Manoeuvre("astern", 0f, -3f, 0f),
                // Six runs is what this region is known to hold apart (the stern-response legs use six).
                new Manoeuvre("slide-to-starboard-turning-to-port", 1.5f, 1f, -25f),
            };
            if (!TryLegAnchors(manoeuvres.Length * 2, out List<Vector2> anchors, out float depth))
            {
                Assert.Ignore("SKIPPED, NOT VERIFIED: no open water inside " + SceneName +
                              "'s camera bounds deep enough for six runs.");
                yield break;
            }

            GameConfig config = GameServices.Config;
            Assert.IsNotNull(config, "the region published no GameConfig, so the dials cannot be set");
            HullFoamSettings shipped = config.HullFoam;
            HullFoamSettings before = shipped;
            before.LeadWeight = 0f;
            before.SideWeight = 0f;
            before.TrailFlipSpeed = 1e9f;   // never astern enough: the trail stays on the transom
            var failures = new List<string>();
            try
            {
                for (int i = 0; i < manoeuvres.Length; i++)
                    for (int arm = 0; arm < 2; arm++)
                    {
                        config.HullFoam = arm == 0 ? before : shipped;
                        yield return ShootWholeHull(hull, visual, manoeuvres[i], arm == 0 ? "before" : "after",
                                                    anchors[i * 2 + arm], failures);
                    }
            }
            finally { config.HullFoam = shipped; }

            Debug.Log($"[{PlateDir}] whole-hull: {depth.ToString("0.0", CultureInfo.InvariantCulture)} m under " +
                      "her at the shallowest anchor.");
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        IEnumerator ShootWholeHull(BoatHullDef hull, BoatVisualDef visual, Manoeuvre m, string arm,
                                   Vector2 start, List<string> failures)
        {
            string subject = "whole-hull";
            Spawned she = Spawn(subject, hull, visual, false, new Leg(m.Name, 0f, m.TurnDegPerSec), start);
            GameObject go = she.Go;
            FoamInjector injector = she.Injector;
            Assert.IsNotNull(injector, "the cape was fitted with no foam injector, so there is no sheet to shoot");

            // A close frame: the run is short, and the foam under test is against her hull.
            float reach = m.Through.magnitude * WholeHullSeconds + 3f * she.SternOffset + 6f;
            var run = new Bounds(new Vector3(start.x, start.y, 0f), Vector3.zero);
            run.Encapsulate(new Vector3(start.x - reach, start.y - reach, 0f));
            run.Encapsulate(new Vector3(start.x + reach, start.y + reach, 0f));
            yield return FrameOn(go.transform, run);

            float wall = Time.realtimeSinceStartup;
            float heading = 0f;
            bool everFromBow = false, everFromStern = false;
            for (float elapsed = 0f; elapsed < WholeHullSeconds; )
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                Assert.Less(Time.realtimeSinceStartup - wall, WholeHullSeconds * 8f + 30f,
                            m.Name + ": the clock this run advances on is not running");
                heading += m.TurnDegPerSec * dt;
                go.transform.rotation = Quaternion.Euler(0f, 0f, -heading);
                she.Rb.linearVelocity = (Vector2)go.transform.right * m.Through.x
                                        + (Vector2)go.transform.up * m.Through.y;
                // The first half second is her getting under way; which end trails is read after it.
                if (elapsed > 0.5f) { if (injector.TrailFromBow) everFromBow = true; else everFromStern = true; }
                yield return null;
            }

            bool expectBow = arm == "after" && m.Through.y < 0f;
            if (expectBow && everFromStern)
                failures.Add(m.Name + "/" + arm + ": making way astern, her trail was still shed from the transom");
            if (!expectBow && everFromBow)
                failures.Add(m.Name + "/" + arm + ": her trail was shed from the bow though she never went astern " +
                             "with the dials on");

            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
            Transform wakeRoot = FindWakeRoot(go.name);
            // What the frame costs to draw with her foam on the water, both arms the same way: the game's camera
            // rendered RenderTimingFrames times with the clock stopped, each one waited for by reading a pixel back.
            double renderMs = 0d;
            if (_rt != null)
            {
                var one = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                RenderTexture was = RenderTexture.active;
                for (int i = -4; i < RenderTimingFrames; i++)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    _cam.Render();
                    RenderTexture.active = _rt;
                    one.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                    if (i >= 0) renderMs += watch.Elapsed.TotalMilliseconds;
                }
                RenderTexture.active = was;
                Object.Destroy(one);
                renderMs /= RenderTimingFrames;
            }
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "[{0}] {1}/{2}/{3}: render {4:0.00} ms, mean of {5}.",
                                    PlateDir, subject, m.Name, arm, renderMs, RenderTimingFrames));
            if (wakeRoot != null)
            {
                SavePlate($"{subject}-{m.Name}-{arm}-photo.png",
                          ShootArm(wakeRoot, injector, sheet: true, foam: true, crest: true));
                SavePlate($"{subject}-{m.Name}-{arm}-sheet.png",
                          ShootArm(wakeRoot, injector, sheet: true, foam: false, crest: false));
                RestoreRig(wakeRoot, injector);
            }
            else failures.Add(m.Name + "/" + arm + ": no wake rig was built for her, so nothing was photographed");

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[{0}] {1}/{2}/{3}: {4:0.0} s at ({5:0.0} to starboard, {6:0.0} ahead) m/s, turning {7:0} deg/s; " +
                "from ({8:0.0}, {9:0.0}); hour {10:0.0}; trail from the bow: {11}; shot from ({12:0.00}, {13:0.00}), " +
                "{14:0.00} m tall, {15}x{16} px.",
                PlateDir, subject, m.Name, arm, WholeHullSeconds, m.Through.x, m.Through.y, m.TurnDegPerSec,
                start.x, start.y, ShotHour, everFromBow, _cam.transform.position.x, _cam.transform.position.y,
                _cam.orthographicSize * 2f, _w, _h));

            Time.timeScale = 1f;
            _spawned.Remove(go);
            Object.Destroy(go);
            yield return null;
        }
    }
}
