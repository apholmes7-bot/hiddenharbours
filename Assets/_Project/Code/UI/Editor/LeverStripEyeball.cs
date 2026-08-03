using System.IO;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI.Editor
{
    /// <summary>
    /// Bakes the OWNER EYEBALL SHEET for the C# lever port (ADR 0025 S1 drift guard, part c): the
    /// ported <see cref="LeverRigRender"/> at the same 9 sigs the rig's own "SPRITE STRIP · 9 FRAMES"
    /// export uses (sig = −1 … +1 in eighths, leverRig.js:302-307), graphite row over chrome row, to
    /// <c>docs/art/proofs/lever-csharp-strip-9.png</c> — outside Assets/ on purpose (a proof for the
    /// PR/owner, not game content). Compare against the strip downloaded from
    /// <c>docs/art/rigs/ui/lever-rig/Lever Rig.dc.html</c>.
    /// </summary>
    public static class LeverStripEyeball
    {
        private const int Frames = 9;
        private const string OutPath = "docs/art/proofs/lever-csharp-strip-9.png";

        [MenuItem("Hidden Harbours/Dev/Bake Lever C# Strip (owner eyeball, 9 sigs x 2 finishes)")]
        public static void Bake()
        {
            int w = LeverRigRender.W * Frames;
            int h = LeverRigRender.H * 2;
            var sheet = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var clear = new Color32[w * h];
            sheet.SetPixels32(clear);

            var surface = new DrawSurface(LeverRigRender.W, LeverRigRender.H);
            Texture2D frame = null;
            for (int row = 0; row < 2; row++)
            {
                HelmLeverFinish finish = row == 0 ? HelmLeverFinish.Graphite : HelmLeverFinish.Chrome;
                for (int i = 0; i < Frames; i++)
                {
                    float sig = -1f + 2f * i / (Frames - 1);   // the bakeStrip sig ladder (leverRig.js:305)
                    LeverRigRender.Render(surface, sig, finish);
                    surface.ToTexture(ref frame);
                    // graphite = TOP row of the sheet; texture rows count from the bottom.
                    sheet.SetPixels32(i * LeverRigRender.W, (1 - row) * LeverRigRender.H,
                                      LeverRigRender.W, LeverRigRender.H, frame.GetPixels32());
                }
            }
            sheet.Apply(false, false);

            string dir = Path.GetDirectoryName(OutPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(OutPath, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            if (frame != null) Object.DestroyImmediate(frame);
            Debug.Log($"[LeverStripEyeball] Wrote {OutPath} ({w}x{h}).");
        }
    }
}
