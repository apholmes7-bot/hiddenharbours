using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>Takes the album's little picture</b> (save v28; owner 2026-11-26: "yes photoalbum should save small
    /// pictures as little poloraids"). On a kept shutter it renders the frame once, at the art grid
    /// (<see cref="FigurePixelGrid.ArtPixelsPerMetre"/> pixels a metre), from a hidden camera copied off the
    /// main one, then point-samples that down to the def's thumbnail size (<see cref="PhotoThumbnail"/>) and
    /// hands back the stored string. Never a blend: one source pixel per thumbnail pixel.
    ///
    /// <para><b>Once per shutter, never per frame.</b> The render target and read-back texture are made for
    /// the one picture and released straight after. With no main camera (a headless test) it gives null and
    /// the picture keeps an empty thumbnail; the record is kept either way.</para>
    /// </summary>
    public static class PhotoThumbnailCapture
    {
        private static Camera _capture;

        /// <summary>The stored thumbnail of <paramref name="frame"/> (world metres), or null when there is
        /// nothing to render with.</summary>
        public static string Take(PhotoCameraDef def, Rect frame)
        {
            if (def == null || frame.width <= 0f || frame.height <= 0f) return null;
            Camera main = Camera.main;
            if (main == null) return null;

            int sourceW = Mathf.Max(1, Mathf.RoundToInt(frame.width * FigurePixelGrid.ArtPixelsPerMetre));
            int sourceH = Mathf.Max(1, Mathf.RoundToInt(frame.height * FigurePixelGrid.ArtPixelsPerMetre));
            int w = def.ThumbnailWidthPx, h = def.ThumbnailHeightPx;

            Camera cam = CaptureCamera();
            if (cam == null) return null;
            cam.CopyFrom(main);
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = frame.height * 0.5f;
            cam.aspect = frame.width / frame.height;
            Vector3 at = main.transform.position;
            cam.transform.SetPositionAndRotation(new Vector3(frame.center.x, frame.center.y, at.z),
                                                 main.transform.rotation);

            RenderTexture target = RenderTexture.GetTemporary(sourceW, sourceH, 24, RenderTextureFormat.ARGB32);
            target.filterMode = FilterMode.Point;
            RenderTexture previous = RenderTexture.active;
            Texture2D read = null;
            try
            {
                cam.targetTexture = target;
                cam.Render();
                RenderTexture.active = target;
                read = new Texture2D(sourceW, sourceH, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0, 0, sourceW, sourceH), 0, 0, false);
                read.Apply(false);
                Color32[] source = read.GetPixels32();
                var small = new Color32[w * h];
                if (!PhotoThumbnail.PointSample(source, sourceW, sourceH, small, w, h)) return null;
                return PhotoThumbnail.Encode(small, w, h);
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (read != null) Object.Destroy(read);
            }
        }

        private static Camera CaptureCamera()
        {
            if (_capture != null) return _capture;
            var go = new GameObject("PhotoThumbnailCapture") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            _capture = go.AddComponent<Camera>();
            _capture.enabled = false;
            return _capture;
        }
    }
}
