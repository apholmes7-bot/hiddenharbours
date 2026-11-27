#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The album's little Polaroids, seen</b> (owner 2026-11-26: "yes photoalbum should save small pictures
    /// as little poloraids"). In St Peters, the shutter's capture takes real thumbnails of the shore at two
    /// sizes, the PHOTO tab is opened on them, and the page is shot at 1920 x 1080 with a 3x crop of the
    /// first Polaroid. Asserts the capture gives a whole thumbnail of the def's size and that the page draws
    /// one card per picture with a point-filtered picture in it. Writes plates only when
    /// <c>PHOTO_ALBUM_EVIDENCE</c> names a folder; ignored with no graphics device.
    /// </summary>
    public class ZzPhotoAlbumPolaroidPlatePlayTests
    {
        const int ScreenW = 1920, ScreenH = 1080;
        const int CropScale = 3;
        const int CropPad = 6;

        sealed class MemorySave : ISaveService
        {
            readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool LoadedExistingSave => false;
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool value) && value;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() { }
        }

        // Shore frames around the landing, metres from it: the wharf, the water off it, the beach.
        static readonly Vector2[] Offsets =
        {
            new Vector2(0f, 0f), new Vector2(4f, -6f), new Vector2(-7f, -3f), new Vector2(8f, 3f),
        };

        readonly List<Object> _made = new List<Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NotebookPresenter book = NotebookPresenter.Instance;
            if (book != null)
            {
                book.Close();
                book.WirePhotos(null);
            }
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            ShellRestart.DestroyPersistentCore();
            GameServices.Reset();
            ShellFlow.Reset();
            InteractionGate.Reset();
            yield return null;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TheShutterTakesRealThumbnails_AndThePhotoTabDrawsThemAsPolaroids()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("The capture and the page need a graphics device.");
            string evidence = System.Environment.GetEnvironmentVariable("PHOTO_ALBUM_EVIDENCE");

            ShellRestart.DestroyPersistentCore();
            GameServices.Reset();
            ShellFlow.Reset();
            var save = new MemorySave();
            save.SetFlag(ArrivalOpening.ArrivedFlagKey, true);
            GameServices.Save = save;
            yield return HiddenHarbours.Tests.Support.SceneSettle.Load("StPeters", LoadSceneMode.Single);
            yield return null;
            yield return null;
            ShellFlow.ContinueGame();
            yield return null;

            Camera main = Camera.main;
            Assert.IsNotNull(main);
            var follow = main.GetComponent<CameraFollow>();
            if (follow != null)
            {
                follow.ScreenPixelsOverride = new Vector2Int(ScreenW, ScreenH);
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
                follow.TickZoom(100d);
            }
            var anchor = Object.FindObjectsByType<RegionAnchor>(FindObjectsSortMode.None)
                .Single(a => a.gameObject.scene.name == "StPeters");
            Vector2 landing = anchor.DisembarkPoint != null ? (Vector2)anchor.DisembarkPoint.position : Vector2.zero;
            yield return new WaitForSecondsRealtime(0.5f);

            PhotoCameraDef shipped = PhotoCameraDef.Load();
            Assert.IsNotNull(shipped, "the camera ships");

            // The shipped size, and twice it (every border doubled too): both whole art-pixel steps.
            foreach (int factor in new[] { 1, 2 })
            {
                PhotoCameraDef def = Object.Instantiate(shipped);
                _made.Add(def);
                def.ThumbnailWidthPx = shipped.ThumbnailWidthPx * factor;
                def.ThumbnailHeightPx = shipped.ThumbnailHeightPx * factor;
                def.PolaroidBorderPx = shipped.PolaroidBorderPx * factor;
                def.PolaroidBottomBorderPx = shipped.PolaroidBottomBorderPx * factor;
                Assert.IsNull(def.ValidationError());

                SaveData album = SaveMigration.NewGame();
                for (int i = 0; i < Offsets.Length; i++)
                {
                    Rect frame = PhotoScoring.FrameAt(def, landing + Offsets[i]);
                    string thumbnail = PhotoThumbnailCapture.Take(def, frame);
                    Assert.IsTrue(PhotoThumbnail.TryReadSize(thumbnail, out int w, out int h), "picture " + i);
                    Assert.AreEqual(def.ThumbnailWidthPx, w);
                    Assert.AreEqual(def.ThumbnailHeightPx, h);
                    Assert.AreEqual(def.ThumbnailEncodedLength, thumbnail.Length);
                    var subjects = new[]
                    {
                        new PhotoSubjectResult { SubjectId = "wildlife.grey_seal", Count = 1 + i, Pose = WildlifePose.Resting,
                                                 Score = 40 + 15 * i },
                    };
                    Assert.IsNotNull(PhotoAlbumRows.Add(album, SavePlayerKeys.Local, 3600d * (8 + i), "region.st_peters",
                                                        subjects, 1, def.AlbumCapacity, thumbnail));
                    if (!string.IsNullOrEmpty(evidence))
                        WriteThumbnail(thumbnail, Path.Combine(evidence, $"thumb-{def.ThumbnailWidthPx}x{def.ThumbnailHeightPx}-{i + 1}.png"));
                }

                List<NotebookPhotoEntry> entries = NotebookPhotoPage.Entries(album, SavePlayerKeys.Local, def, 86400f);
                yield return ShootTheAlbum(main, entries, def, evidence);
            }
        }

        static IEnumerator ShootTheAlbum(Camera main, List<NotebookPhotoEntry> entries, PhotoCameraDef def, string evidence)
        {
            NotebookPresenter book = NotebookPresenter.Instance;
            Assert.IsNotNull(book, "the book self-installs");
            book.enabled = false;
            book.enabled = true;
            book.WirePhotos(() => entries);
            book.Open();
            yield return null;
            int tab = book.TabIndexOf(NotebookContent.TabPhotos);
            Assert.GreaterOrEqual(tab, 0, "the PHOTO stub is on the fore-edge once there is a picture");
            book.SelectTab(tab);
            yield return null;

            // Game zoom: the book's whole-number scale for a 1920 x 1080 screen, whatever the batch screen is.
            NotebookFit fit = NotebookLayout.Fit(Mathf.RoundToInt(ScreenW * NotebookPresenter.RoomFraction),
                                                 Mathf.RoundToInt(ScreenH * NotebookPresenter.RoomFraction));
            RectTransform bookRect = book.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "Book");
            bookRect.localScale = Vector3.one * fit.Scale;

            Image[] cards = book.GetComponentsInChildren<Image>(false)
                                .Where(i => i.name == NotebookPresenter.PolaroidPieceName).ToArray();
            Image[] pictures = book.GetComponentsInChildren<Image>(false)
                                   .Where(i => i.name == NotebookPresenter.PolaroidPicturePieceName).ToArray();
            Assert.Greater(cards.Length, 0, "a Polaroid card on the page");
            Assert.AreEqual(cards.Length, pictures.Length, "every card holds its picture");
            foreach (Image p in pictures)
            {
                Assert.AreEqual(FilterMode.Point, p.sprite.texture.filterMode, "point filtered, no mixels");
                Assert.AreEqual(def.ThumbnailWidthPx, Mathf.RoundToInt(p.rectTransform.rect.width));
                Assert.AreEqual(def.ThumbnailHeightPx, Mathf.RoundToInt(p.rectTransform.rect.height));
            }
            RectTransform card = cards[0].rectTransform;
            Assert.AreEqual(def.PolaroidWidthPx, Mathf.RoundToInt(card.rect.width));
            Assert.AreEqual(def.PolaroidHeightPx, Mathf.RoundToInt(card.rect.height));

            if (!string.IsNullOrEmpty(evidence))
                yield return Plate(main, card, Path.Combine(evidence, $"album-{def.ThumbnailWidthPx}x{def.ThumbnailHeightPx}"));
            book.Close();
            yield return null;
        }

        static IEnumerator Plate(Camera camera, RectTransform firstCard, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var previousTarget = camera.targetTexture;
            var target = RenderTexture.GetTemporary(ScreenW, ScreenH, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            var overlays = new List<(Canvas canvas, Camera camera, float distance)>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isActiveAndEnabled || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                overlays.Add((canvas, canvas.worldCamera, canvas.planeDistance));
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
            yield return null;
            Canvas.ForceUpdateCanvases();
            bool captured = false;
            Exception failure = null;
            void Rendered(UnityEngine.Rendering.ScriptableRenderContext context, Camera rendered)
            {
                if (rendered != camera || captured) return;
                captured = true;
                var previous = RenderTexture.active;
                var image = new Texture2D(ScreenW, ScreenH, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, ScreenW, ScreenH), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(path + "-page.png", image.EncodeToPNG());

                    var corners = new Vector3[4];
                    firstCard.GetWorldCorners(corners);
                    Vector2 a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                    Vector2 b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x)) - CropPad, 0, ScreenW - 1);
                    int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y)) - CropPad, 0, ScreenH - 1);
                    int x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x)) + CropPad * 30, x0 + 1, ScreenW);
                    int y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y)) + CropPad, y0 + 1, ScreenH);
                    File.WriteAllBytes(path + "-crop3x.png", Scaled(image, x0, y0, x1 - x0, y1 - y0, CropScale));
                }
                catch (Exception ex) { failure = ex; }
                finally { RenderTexture.active = previous; Object.Destroy(image); }
            }
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += Rendered;
            try
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 10d;
                while (!captured && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.IsTrue(captured, "the plate camera did not render");
                Assert.IsNull(failure, failure?.ToString());
            }
            finally
            {
                UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= Rendered;
                foreach (var overlay in overlays)
                {
                    if (overlay.canvas == null) continue;
                    overlay.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    overlay.canvas.worldCamera = overlay.camera;
                    overlay.canvas.planeDistance = overlay.distance;
                }
                camera.targetTexture = previousTarget;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        static byte[] Scaled(Texture2D source, int x, int y, int w, int h, int scale)
        {
            Color32[] all = source.GetPixels32();
            var big = new Texture2D(w * scale, h * scale, TextureFormat.RGB24, false);
            var px = new Color32[w * scale * h * scale];
            for (int j = 0; j < h * scale; j++)
                for (int i = 0; i < w * scale; i++)
                    px[j * w * scale + i] = all[(y + j / scale) * source.width + x + i / scale];
            big.SetPixels32(px);
            big.Apply();
            byte[] png = big.EncodeToPNG();
            Object.Destroy(big);
            return png;
        }

        static void WriteThumbnail(string stored, string file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            Color32[] px = PhotoThumbnail.Decode(stored, out int w, out int h);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
#endif
