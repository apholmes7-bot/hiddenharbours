using System.IO;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>How many things are drawing a boat's cabin while somebody is below</b> — the measurement
    /// ADR 0041's retirement (fleet rollout PR 0) turns on.
    ///
    /// <para>Two sources can each put a room on screen. The SPRITE room is
    /// <see cref="BoatInteriorInstaller"/>'s <c>BoatInteriorRoom</c> child: a <see cref="SpriteRenderer"/>
    /// the ADR 0038 runtime switches on and hands a baked cell. The MESH room is the hull's own geometry,
    /// revealed when <see cref="IsoFacetHullRenderer"/> holds an open cut (ADR 0041; the room's faces live
    /// in the hull mesh and the cut is the only thing that shows them). A converted hull must draw from
    /// exactly one of the two, and the honest count is the SOURCES — a renderer enabled with a cabin
    /// picture, a hull with her house cut open — not a pixel guess about which one is on top.</para>
    /// </summary>
    public static class BelowDecksDrawSources
    {
        public readonly struct Count
        {
            public readonly bool SpriteRoom;
            public readonly bool MeshRoom;
            public Count(bool sprite, bool mesh) { SpriteRoom = sprite; MeshRoom = mesh; }
            public int Total => (SpriteRoom ? 1 : 0) + (MeshRoom ? 1 : 0);
            public override string ToString() =>
                $"{Total} source(s) drawing below decks — sprite room: {(SpriteRoom ? "DRAWING" : "off")}, " +
                $"mesh room (cut open): {(MeshRoom ? "DRAWING" : "closed")}";
        }

        /// <summary>Read both sources off the live objects under a boat root.</summary>
        public static Count Measure(Transform boatRoot)
        {
            bool sprite = false, mesh = false;
            if (boatRoot != null)
            {
                Transform room = boatRoot.Find(BoatInteriorInstaller.RoomChildName);
                var sr = room != null ? room.GetComponent<SpriteRenderer>() : null;
                sprite = sr != null && sr.enabled && sr.sprite != null;

                var hull = boatRoot.GetComponentInChildren<IsoFacetHullRenderer>(true);
                mesh = hull != null && hull.CutawayShown.Opens;
            }
            return new Count(sprite, mesh);
        }

        /// <summary>
        /// A plate of the boat as the game draws her right now — an ortho camera centred on the hull's
        /// root, rendered to a texture and written OUTSIDE the repo (the temporary cache), so a test run
        /// never rewrites a committed picture. The path is logged; whoever wants it committed copies it.
        /// </summary>
        public static string WritePlate(Transform boatRoot, string fileName, float metresTall, int pxPerMetre)
        {
            int h = Mathf.RoundToInt(metresTall * pxPerMetre);
            int w = Mathf.RoundToInt(h * 4f / 3f);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            var camGo = new GameObject("RetirementPlateCam");
            string dir = Path.Combine(Application.temporaryCachePath, "mesh-interiors-retirement");
            string path = Path.Combine(dir, fileName);
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = metresTall * 0.5f;
                Vector3 p = boatRoot.position;
                cam.transform.position = new Vector3(p.x, p.y, -100f);
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 400f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.16f, 0.22f, 0.30f, 1f);
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.Destroy(tex);
                Debug.Log($"[mesh-interiors-retirement] plate written: {path}");
                return path;
            }
            finally
            {
                camGo.GetComponent<Camera>().targetTexture = null;
                Object.Destroy(camGo);
                rt.Release();
                Object.Destroy(rt);
            }
        }
    }
}
