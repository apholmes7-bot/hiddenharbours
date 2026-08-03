using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI.Editor
{
    /// <summary>
    /// Bakes the OWNER EYEBALL PROOFS for the C# fish-finder port (ADR 0025 S3b drift guard, part c — the
    /// full pixel golden-master still needs the editor Canvas2D shim we are not building). Two sheets,
    /// both written OUTSIDE <c>Assets/</c> on purpose (proofs for the PR and the owner, not game content):
    ///
    /// <list type="bullet">
    /// <item><c>docs/art/proofs/fish-finder-csharp-sheet-6.png</c> — the rig's own "↓ STATE SHEET" export,
    /// <b>day/night × shoal/mid/deep</b> (<c>docs/art/rigs/ui/fish-finder/README.md</c>), rendered through
    /// <see cref="FishRigRender"/>. Compare against the sheet downloaded from
    /// <c>Fish Finder.dc.html</c>.</item>
    /// <item><c>docs/art/proofs/fish-finder-console-brow.png</c> — the <b>brow-squash check</b>. The rig is
    /// PORTRAIT (480×660); "drawUnit fits any box" is a geometry claim, not an aesthetic one, and when S6
    /// flush-mounts an instrument in a WIDE console brow the sonar column becomes a letterbox. This puts
    /// the finder and the depth sounder in the same brow box, at the same size, for the owner to judge and
    /// to veto.</item>
    /// </list>
    /// </summary>
    public static class FishFinderSheetEyeball
    {
        private const string SheetPath = "docs/art/proofs/fish-finder-csharp-sheet-6.png";
        private const string BrowPath = "docs/art/proofs/fish-finder-console-brow.png";

        // shoal / mid / deep against the rig's own 20 m scale and 3 m default set-point. The shoal column
        // is INSIDE the alarm, so it shows the Ruling E flash — the state worth judging.
        private static readonly float[] Depths = { 2.4f, 9.4f, 18.6f };
        private const float Alarm = 3f;
        private const float Range = 20f;

        /// <summary>Both proofs in one call — the headless entry point
        /// (<c>-executeMethod HiddenHarbours.UI.Editor.FishFinderSheetEyeball.BakeAll</c>).</summary>
        [MenuItem("Hidden Harbours/Dev/Bake Fish Finder Proofs (both sheets)")]
        public static void BakeAll()
        {
            BakeSheet();
            BakeBrow();
        }

        [MenuItem("Hidden Harbours/Dev/Bake Fish Finder C# Sheet (owner eyeball, day-night x 3 depths)")]
        public static void BakeSheet()
        {
            int cw = FishRigRender.W, ch = FishRigRender.H;
            int w = cw * Depths.Length, h = ch * 2;
            var sheet = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            sheet.SetPixels32(new Color32[w * h]);

            var surface = new DrawSurface(cw, ch);
            Texture2D frame = null;
            List<SonarMark> marks = SampleMarks();

            for (int row = 0; row < 2; row++)
            {
                bool night = row == 1;
                for (int col = 0; col < Depths.Length; col++)
                {
                    float depth = Depths[col];
                    var prefs = new SounderPrefs(Alarm, true, false, night, Range);
                    bool triggered = DepthSounder.ShallowAlarm(depth, in prefs);   // ONE alarm rule
                    var state = new FishRigState(depth, 12f, Range, Alarm, feet: false, night: night,
                                                 fishId: true, armed: true, triggered: triggered,
                                                 blink: triggered, phase: 3.2 + col * 0.7,
                                                 adjust: FishRigAdjust.Range, sens: 0.75f, link: true,
                                                 batt: 0.8f, volts: 4f);
                    FishRigRender.Render(surface, in state, marks);
                    surface.ToTexture(ref frame);
                    // day = TOP row of the sheet; texture rows count from the bottom.
                    sheet.SetPixels32(col * cw, (1 - row) * ch, cw, ch, frame.GetPixels32());
                }
            }
            sheet.Apply(false, false);
            Write(SheetPath, sheet, w, h);
            Object.DestroyImmediate(sheet);
            if (frame != null) Object.DestroyImmediate(frame);
        }

        [MenuItem("Hidden Harbours/Dev/Bake Fish Finder Brow Proof (S6 squash check)")]
        public static void BakeBrow()
        {
            // Four panels the owner can judge in one look:
            //   1/2 — the S6 case: one wide flush-mount cutout, both candidate instruments in it.
            //   3/4 — why the small card is a SCALED BLIT of the native render and not a smaller drawing.
            const int browW = 300, browH = 120, halfW = 240, halfH = 330;
            const int pad = 16, labelH = 12, gap = 4;
            var ink = new Color32(230, 230, 230, 255);
            var warn = new Color32(224, 119, 107, 255);

            int w = pad * 3 + halfW * 2;
            int h = pad * 4 + (labelH + gap + browH) * 2 + labelH + gap + halfH;
            var s = new DrawSurface(w, h);
            s.Clear();
            s.FillRect(0, 0, w, h, new Color32(18, 22, 26, 255));

            var state = MakeState(9.4f, false, 3.2);

            int y = pad;
            // ⚠ Label text avoids J/Q/X/Z and punctuation beyond '-' and '.': RigDrawUtil's shared 3×5
            // font is consoleRig.js's SUBSET and renders anything else as a blank.
            RigDrawUtil.Text(s, "1 FINDER IN A 300 BY 120 BROW", pad, y, 2, ink);
            y += labelH + gap;
            FishRigRender.DrawUnit(s, pad, y, browW, browH, in state, SampleMarks());
            y += browH + pad;

            RigDrawUtil.Text(s, "2 SOUNDER IN THE SAME BROW", pad, y, 2, ink);
            y += labelH + gap;
            var dep = new DepthRigState(9.4f, false, false, true, 3f, 12f, false);
            DepthRigRender.DrawUnit(s, pad, y, browW, browH, in dep);
            y += browH + pad;

            RigDrawUtil.Text(s, "3 HALF RENDER - NOT SHIPPED", pad, y, 2, warn);
            RigDrawUtil.Text(s, "4 SHIPPED CARD - 2 TO 1 BLIT", pad * 2 + halfW, y, 2, ink);
            y += labelH + gap;

            // 3 — draw the instrument into a half-size mount. The rig picks its font scale from the mount
            // HEIGHT and its strip height from the glass WIDTH; below ~83% of native those two laws
            // disagree and the legends spill out of the pushers. This is the option we did not take.
            RigRect half = FishRigGeometry.StandaloneMount(halfW, halfH);
            FishRigRender.DrawUnit(s, pad + half.X, y + half.Y, half.W, half.H, in state, SampleMarks());

            // 4 — what the player actually sees: the NATIVE render decimated 2:1, typography intact.
            var native = new DrawSurface(FishRigRender.W, FishRigRender.H);
            FishRigRender.Render(native, in state, SampleMarks());
            Decimate2x(native, s, pad * 2 + halfW, y);

            Texture2D tex = null;
            s.ToTexture(ref tex);
            Write(BrowPath, tex, w, h);
            if (tex != null) Object.DestroyImmediate(tex);
        }

        /// <summary>Nearest-neighbour 2:1 decimation — exactly what a point-filtered RawImage does when
        /// the card blits the native surface at <c>CardScale</c> 0.5.</summary>
        private static void Decimate2x(DrawSurface src, DrawSurface dst, int dx, int dy)
        {
            for (int y = 0; y * 2 < src.Height; y++)
            {
                for (int x = 0; x * 2 < src.Width; x++)
                {
                    Color32 c = src.Pixels[(y * 2) * src.Width + x * 2];
                    if (c.a == 0) continue;
                    int px = dx + x, py = dy + y;
                    if (px < 0 || px >= dst.Width || py < 0 || py >= dst.Height) continue;
                    dst.Pixels[py * dst.Width + px] = c;
                }
            }
        }

        private static FishRigState MakeState(float depth, bool night, double phase)
        {
            var prefs = new SounderPrefs(Alarm, true, false, night, Range);
            return new FishRigState(depth, 12f, Range, Alarm, false, night, true, true,
                                    DepthSounder.ShallowAlarm(depth, in prefs), true, phase,
                                    FishRigAdjust.Range, 0.75f, true, 0.8f, 4f);
        }

        /// <summary>A representative return, built through the SHIPPED presenter so the proof shows what
        /// the game shows — three fish on a school you are sitting on, two on one you are clipping.</summary>
        internal static List<SonarMark> SampleMarks()
        {
            var seam = new List<FishMark>
            {
                new FishMark(6.2f, 3, 1f),
                new FishMark(12.8f, 2, 0.35f),
            };
            var into = new List<SonarMark>();
            FishFinderSettings cfg = FishFinderSettings.Default;
            FishMarkPresenter.Build(seam, in cfg, into);
            return into;
        }

        private static void Write(string path, Texture2D tex, int w, int h)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"[FishFinderSheetEyeball] Wrote {path} ({w}x{h}).");
        }
    }
}
