using System.Collections;
using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Player;
using HiddenHarbours.UI;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // The player's camera at the Whelps: the viewfinder's brackets over seals hauled out at low water, shot through
    // the play camera at the skerries board's 32 px a metre, and the picture the shutter would keep from that frame
    // judged by the real camera def against the live haul-out. Local evidence (GPU only); it asserts that the
    // frame holds seals and that the kept picture would answer Odile's request, and writes the plate.
    public partial class WharfBuildingPass2PlatePlayTests
    {
        const string CameraPlatesVariable = "HH_CAMERA_PLATES";

        [UnityTest]
        public IEnumerator StPeters_TheViewfinderOverTheSealsOnTheWhelps()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            EnsureTheFollow();
            HoldTheMood();
            _look = "camera";
            _lookEvidence = "the skerries board's frame, 32 px a metre, the viewfinder drawn into the play camera";

            var host = Object.FindAnyObjectByType<SealHaulout>();
            Assert.IsNotNull(host, "St Peters holds no seal haul-out.");
            PhotoCameraDef def = PhotoCameraDef.Load();
            Assert.IsNotNull(def, "no shipped camera def.");
            ViewfinderOverlay overlay = ViewfinderOverlay.Instance;
            Assert.IsNotNull(overlay, "the viewfinder did not install itself.");

            string dir = System.Environment.GetEnvironmentVariable(CameraPlatesVariable);
            if (string.IsNullOrEmpty(dir)) dir = PlatePath();
            Directory.CreateDirectory(dir);

            IEnvironmentService held = GameServices.Environment;
            bool followEnabled = _follow.enabled;
            float aspect = _cam.aspect, ortho = _cam.orthographicSize;
            Vector3 camAt = _cam.transform.position;
            var pixelCameras = _cam.GetComponents<Behaviour>()
                .Where(b => b.GetType().Name == "PixelPerfectCamera" && b.enabled).ToArray();
            try
            {
                yield return PinTheHour(Noon, ClearRunSeconds);
                const float lowWater = -2.0f;
                GameServices.Environment = new LandingBoardTide(held, lowWater);
                Time.timeScale = 1f;
                float until = Time.realtimeSinceStartup + 20f;
                do { yield return null; }
                while (Time.realtimeSinceStartup < until &&
                       Mathf.Abs(Shader.GetGlobalVector("_HHSeaLevelWorld").x - lowWater) > .01f);
                host.Haulout = host.Haulout;
                FrameTheSkerries(pixelCameras);
                float run = Time.unscaledTime + 1.5f;
                while (Time.unscaledTime < run) yield return null;

                // Where the seals lie: the centre of every one out on the rock, read through the Core seam.
                var scratch = new WildlifeSighting[def.MaxSightingsRead];
                var seen = new WildlifeSighting[def.MaxSightingsRead];
                int n = WildlifePresences.CopyInFrame(new Rect(SealX0, SealY0, SealX1 - SealX0, SealY1 - SealY0),
                                                      scratch, seen);
                var out_ = seen.Take(n).Where(s => s.OnLand && s.SubjectId == "wildlife.grey_seal").ToArray();
                Assert.That(out_.Length, Is.GreaterThanOrEqualTo(2), "fewer than two seals out at low water.");
                Vector2 centre = out_.Aggregate(Vector2.zero, (a, s) => a + s.WorldPosition) / out_.Length;

                // He stands on the skerries' west side, where the lookout path ends, and aims at them.
                Vector2 standing = new Vector2(centre.x - def.StartReachMetres, centre.y);
                var model = new PhotoCameraModel(def);
                model.NoteFacing(Vector2.right);
                model.Raise();
                Rect frame = model.FrameFor(standing);

                var album = new SaveData();
                PhotoShotOutcome outcome = model.Shoot(album, SavePlayerKeys.Local, standing,
                                                       GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0d,
                                                       GameServices.CurrentRegionId, 0f);
                string line = model.LineFor(outcome);
                Debug.Log($"[CameraPlates] frame {frame} from {standing}: {outcome}, '{line}', " +
                          $"{(model.LastKept != null ? JsonUtility.ToJson(model.LastKept) : "nothing kept")}");
                Assert.That(outcome, Is.EqualTo(PhotoShotOutcome.Kept), "the frame over the seals kept nothing.");

                PhotoRequestDef odile = Resources.LoadAll<PhotoRequestDef>(PhotoRequestDef.ResourcesFolder)
                    .Single(r => r.Id == "photo_request.odile_grey_seals");
                Assert.That(PhotoRequestTalk.AlbumHasOne(odile, album), Is.True,
                            "a picture of the seals at low water from the lookout side would not answer her request.");

                // The viewfinder, drawn as the player sees it, into the plate.
                ViewfinderOverlay.DrawWhileWorldStopped = true;
                overlay.DrawThrough(_cam);
                PhotoViewfinder.Show(frame, def.KeyboardHint);
                PhotoViewfinder.Say(line, Time.unscaledTime + 60f);
                yield return ShadersReady();
                Time.timeScale = 0f;
                for (int i = 0; i < 3; i++) yield return null;
                Assert.IsTrue(overlay.FrameDrawn, "the viewfinder's brackets are not drawn.");

                var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
                tex.LoadRawTextureData(Capture());
                tex.Apply();
                string path = Path.Combine(dir, "viewfinder.whelps.low.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Debug.Log($"[CameraPlates] plate written: {path}");

                // The album page's words for that picture, as the notebook would letter them.
                foreach (NotebookPhotoEntry e in NotebookPhotoPage.Entries(album, SavePlayerKeys.Local, def,
                                                                         GameServices.SecondsPerDay))
                    Debug.Log($"[CameraPlates] album: {e.Head} | {string.Join(" | ", e.Lines)}");
                File.WriteAllText(Path.Combine(dir, "album.txt"), string.Join("\n",
                    NotebookPhotoPage.Entries(album, SavePlayerKeys.Local, def, GameServices.SecondsPerDay)
                        .Select(e => e.Head + "\n  " + string.Join("\n  ", e.Lines))));
                Debug.Log("[CameraPlates] PLATES_COMPLETE " + dir);
            }
            finally
            {
                PhotoViewfinder.Reset();
                ViewfinderOverlay.DrawWhileWorldStopped = false;
                if (overlay != null) overlay.DrawThrough(null);
                GameServices.Environment = held;
                _follow.enabled = followEnabled;
                foreach (Behaviour ppc in pixelCameras) if (ppc != null) ppc.enabled = true;
                _cam.aspect = aspect;
                _cam.orthographicSize = ortho;
                _cam.transform.position = camAt;
                Time.timeScale = 1f;
            }
        }
    }
}
