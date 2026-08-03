using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.UI.Editor
{
    /// <summary>
    /// Bakes the OWNER EYEBALL SHEET for the C# depth-sounder port (ADR 0025 S2 drift guard, part c —
    /// the full pixel golden-master still needs the editor Canvas2D shim we are not building yet). The
    /// rig's own "↓ STATE SHEET" export is <b>day/night × deep/at-set/shoal</b>
    /// (<c>docs/art/rigs/ui/depth-finder/README.md</c>); this renders the same six states through
    /// <see cref="DepthRigRender"/> and writes <c>docs/art/proofs/sounder-csharp-sheet-6.png</c> —
    /// outside Assets/ on purpose (a proof for the PR/owner, not game content). Compare against the
    /// sheet downloaded from <c>Depth Finder.dc.html</c>.
    /// </summary>
    public static class SounderSheetEyeball
    {
        private const string OutPath = "docs/art/proofs/sounder-csharp-sheet-6.png";

        // The three depth columns against the rig's own 3 m default set-point: comfortably deep, sitting
        // exactly ON the alarm (the inclusive-boundary case), and hard aground on the bar.
        private const float Alarm = 3f;
        private static readonly float[] Depths = { 26.2f, 3.0f, 0.4f };

        [MenuItem("Hidden Harbours/Dev/Bake Sounder C# Sheet (owner eyeball, day-night x 3 depths)")]
        public static void Bake()
        {
            int cw = DepthRigRender.W, ch = DepthRigRender.H;
            int w = cw * Depths.Length, h = ch * 2;
            var sheet = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            sheet.SetPixels32(new Color32[w * h]);

            var surface = new DrawSurface(cw, ch);
            Texture2D frame = null;
            for (int row = 0; row < 2; row++)
            {
                bool night = row == 1;
                for (int col = 0; col < Depths.Length; col++)
                {
                    float depth = Depths[col];
                    // The shoal columns show the alarm LIT (blink true) — that is the state worth judging.
                    bool triggered = depth <= Alarm;
                    var state = new DepthRigState(depth, feet: false, night: night, armed: true,
                                                  alarm: Alarm, tempC: 12f, blink: triggered);
                    DepthRigRender.Render(surface, in state);
                    surface.ToTexture(ref frame);
                    // day = TOP row of the sheet; texture rows count from the bottom.
                    sheet.SetPixels32(col * cw, (1 - row) * ch, cw, ch, frame.GetPixels32());
                }
            }
            sheet.Apply(false, false);

            string dir = Path.GetDirectoryName(OutPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(OutPath, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            if (frame != null) Object.DestroyImmediate(frame);
            Debug.Log($"[SounderSheetEyeball] Wrote {OutPath} ({w}x{h}).");
        }
    }
}
