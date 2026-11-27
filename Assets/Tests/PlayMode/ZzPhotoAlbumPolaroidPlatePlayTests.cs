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
using UnityEngine.Rendering.Universal;
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
            new Vector2(-3f, 5f), new Vector2(10f, -2f), new Vector2(-10f, 2f), new Vector2(2f, -10f),
            new Vector2(6f, 8f), new Vector2(-5f, -8f),
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
                book.ScreenPixelsOverride = null;
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

            // The shipped size: ten pictures, enough for full rows of cards and a page turn.
            foreach (int factor in new[] { 1 })
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
            book.ScreenPixelsOverride = new Vector2Int(ScreenW, ScreenH);   // lay the book out for 1080p
            book.WirePhotos(() => entries);
            book.Open();
            yield return null;
            int tab = book.TabIndexOf(NotebookContent.TabPhotos);
            Assert.GreaterOrEqual(tab, 0, "the PHOTO stub is on the fore-edge once there is a picture");
            book.SelectTab(tab);
            yield return null;

            int spread = 1;
            int firstSpreadCards = 0;
            while (true)
            {
                int shown = 0;
                yield return ShootTheSpread(main, book, def, evidence, spread, n => shown = n);
                if (spread == 1) firstSpreadCards = shown;
                if (!book.HasNextSpread || spread >= 3) break;
                book.TurnPage(1);
                yield return null;
                spread++;
            }
            int across = NotebookPhotoGrid.For(NotebookPhotoPage.Style(def), book.Fit.Cols).Across;
            Debug.Log($"[album plate] screen {Screen.width}x{Screen.height} fit cols {book.Fit.Cols} lines {book.Fit.Lines} " +
                      $"scale {book.Fit.Scale} across {across} first spread {firstSpreadCards} spreads shot {spread}");
            Assert.GreaterOrEqual(firstSpreadCards, 2 * across, "two full rows on the first spread");
            Assert.GreaterOrEqual(firstSpreadCards, 4, "two rows to a leaf, four to a spread");
            Assert.Greater(spread, 1, "ten pictures turn the page");
            book.Close();
            yield return null;
        }

        static IEnumerator ShootTheSpread(Camera main, NotebookPresenter book, PhotoCameraDef def, string evidence,
                                          int spread, Action<int> cardsShown)
        {
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

            // An album: straight cards (a tilt would be mixels), side by side with air, a caption under each.
            foreach (Image c in cards)
                Assert.AreEqual(Quaternion.identity, c.rectTransform.localRotation, "a straight card");
            for (int i = 0; i < cards.Length; i++)
                for (int j = i + 1; j < cards.Length; j++)
                {
                    RectTransform a = cards[i].rectTransform, b = cards[j].rectTransform;
                    if (a.parent != b.parent ||
                        !Mathf.Approximately(a.anchoredPosition.y, b.anchoredPosition.y)) continue;
                    float gap = Mathf.Abs(a.anchoredPosition.x - b.anchoredPosition.x) - def.PolaroidWidthPx;
                    Assert.GreaterOrEqual(gap, def.PolaroidMinGapPx, "air between two cards in a row");
                }
            // What and how many is written on each card's strip, inside the card and clear of its edges; the
            // day and place sit under it.
            Text[] onCard = book.GetComponentsInChildren<Text>(false)
                                .Where(t => t.name == NotebookPresenter.PolaroidCardCaptionPieceName).ToArray();
            Assert.AreEqual(cards.Length, onCard.Length, "every card has its line written on the strip");
            foreach (Text t in onCard)
            {
                var r = t.rectTransform;
                Assert.AreEqual(NotebookPresenter.PolaroidPieceName, r.parent.name, "written on the card itself");
                Assert.IsFalse(t.text.Contains("\n"), "one line, never wrapped");
                Assert.LessOrEqual(t.text.Length, NotebookPhotoPage.Style(def).OnCardCols, "clipped to the card");
            }
            Text[] under = book.GetComponentsInChildren<Text>(false)
                               .Where(t => t.name == NotebookPresenter.PolaroidCaptionPieceName).ToArray();
            Assert.GreaterOrEqual(under.Length, cards.Length, "the day and place under each card");

            cardsShown(cards.Length);
            RectTransform leaf = book.GetComponentsInChildren<RectTransform>(false).First(r => r.name == "Leaf");
            if (!string.IsNullOrEmpty(evidence))
                yield return Plate(main, leaf,
                                   Path.Combine(evidence, $"album-{def.ThumbnailWidthPx}x{def.ThumbnailHeightPx}-spread{spread}"));
        }

        /// <summary>
        /// The page as the player sees it. In the game every notebook and HUD canvas is ScreenSpaceOverlay:
        /// drawn after every camera, over the whole world. An overlay canvas does not draw into a camera's
        /// RenderTexture, so the plate takes the two apart and lays them together the way the screen does:
        /// the world camera renders the world alone, then a UI-only camera renders the root overlay canvases
        /// (moved to the UI layer for the shot, sorted among themselves by their own orders) twice, over
        /// black and over white. Whatever blending the canvases do, a pixel is F + T x background, so the
        /// two shots give F (over black) and T (white minus black), and the page over the world is
        /// F + T x world, worked in linear light. (The first plate bound the canvases to the world camera,
        /// where the 2D renderer sorted the book's order 400 against world sprites up to 32763 on the one
        /// sorting layer, and the world drew over the paper.)
        /// </summary>
        static IEnumerator Plate(Camera camera, RectTransform cropTo, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var previousTarget = camera.targetTexture;
            var target = RenderTexture.GetTemporary(ScreenW, ScreenH, 24, RenderTextureFormat.ARGB32);
            var uiTarget = RenderTexture.GetTemporary(ScreenW, ScreenH, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;

            int uiLayer = LayerMask.NameToLayer("UI");
            Assert.GreaterOrEqual(uiLayer, 0, "the built-in UI layer");
            var uiGo = new GameObject("AlbumPlateUiCamera");
            uiGo.transform.position = new Vector3(0f, 0f, -5000f);
            var uiCamera = uiGo.AddComponent<Camera>();
            uiCamera.enabled = false;
            uiCamera.orthographic = true;
            uiCamera.cullingMask = 1 << uiLayer;
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.targetTexture = uiTarget;
            uiCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            var overlays = new List<(Canvas canvas, Camera camera, float distance)>();
            var layers = new List<(GameObject go, int layer)>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isActiveAndEnabled || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                overlays.Add((canvas, canvas.worldCamera, canvas.planeDistance));
                foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
                {
                    layers.Add((t.gameObject, t.gameObject.layer));
                    t.gameObject.layer = uiLayer;
                }
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 1f;
            }
            yield return null;
            Canvas.ForceUpdateCanvases();
            bool captured = false;
            Color32[] world = null;
            Exception failure = null;
            void Rendered(UnityEngine.Rendering.ScriptableRenderContext context, Camera rendered)
            {
                if (rendered != camera || captured) return;
                captured = true;
                try { world = Read(target); }
                catch (Exception ex) { failure = ex; }
            }
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += Rendered;
            try
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 10d;
                while (!captured && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.IsTrue(captured, "the world camera did not render");
                Assert.IsNull(failure, failure?.ToString());

                Canvas.ForceUpdateCanvases();
                uiCamera.backgroundColor = Color.black;
                uiCamera.Render();
                Color32[] overBlack = Read(uiTarget);
                uiCamera.backgroundColor = Color.white;
                uiCamera.Render();
                Color32[] overWhite = Read(uiTarget);
                bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                var page = new Color32[world.Length];
                int drawn = 0;
                for (int i = 0; i < page.Length; i++)
                {
                    bool opaque = overBlack[i].r == overWhite[i].r && overBlack[i].g == overWhite[i].g &&
                                  overBlack[i].b == overWhite[i].b;
                    if (opaque) drawn++;
                    page[i] = new Color32(Lay(overBlack[i].r, overWhite[i].r, world[i].r, linear),
                                          Lay(overBlack[i].g, overWhite[i].g, world[i].g, linear),
                                          Lay(overBlack[i].b, overWhite[i].b, world[i].b, linear), 255);
                }
                Assert.Greater(drawn, page.Length / 20, "the canvases drew an opaque page into the plate");

                var image = new Texture2D(ScreenW, ScreenH, TextureFormat.RGB24, false);
                try
                {
                    image.SetPixels32(page);
                    image.Apply();
                    File.WriteAllBytes(path + "-page.png", image.EncodeToPNG());

                    var corners = new Vector3[4];
                    cropTo.GetWorldCorners(corners);
                    Vector2 a = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
                    Vector2 b = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x)) - CropPad, 0, ScreenW - 1);
                    int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y)) - CropPad, 0, ScreenH - 1);
                    int x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x)) + CropPad, x0 + 1, ScreenW);
                    int y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y)) + CropPad, y0 + 1, ScreenH);
                    File.WriteAllBytes(path + "-crop3x.png", Scaled(image, x0, y0, x1 - x0, y1 - y0, CropScale));
                }
                finally { Object.Destroy(image); }
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
                foreach (var held in layers)
                    if (held.go != null) held.go.layer = held.layer;
                uiCamera.targetTexture = null;
                Object.Destroy(uiGo);
                camera.targetTexture = previousTarget;
                RenderTexture.ReleaseTemporary(target);
                RenderTexture.ReleaseTemporary(uiTarget);
            }
        }

        static Color32[] Read(RenderTexture source)
        {
            var previous = RenderTexture.active;
            var read = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = source;
                read.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                read.Apply();
                return read.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(read);
            }
        }

        /// <summary>One channel of the page over the world: F + T x world, F the shot over black, T the
        /// white shot minus it, in linear light when the project renders linear.</summary>
        static byte Lay(byte overBlack, byte overWhite, byte world, bool linear)
        {
            float f = overBlack / 255f, w = overWhite / 255f, s = world / 255f;
            if (linear)
            {
                f = Mathf.GammaToLinearSpace(f);
                w = Mathf.GammaToLinearSpace(w);
                s = Mathf.GammaToLinearSpace(s);
            }
            float c = Mathf.Clamp01(f + (w - f) * s);
            if (linear) c = Mathf.LinearToGammaSpace(c);
            return (byte)Mathf.RoundToInt(c * 255f);
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
