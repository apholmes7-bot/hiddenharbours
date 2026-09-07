using System.Collections.Generic;
using System.IO;
using System.Text;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>ROW 29, DRAWN — where the three families spring from, through a turn.</b> The owner,
    /// 2026-09-06: <i>"the foam seemed off-centred with three different sections leaving the boat."</i>
    ///
    /// <para><b>⚠️ WHAT THIS PLATE IS, exactly.</b> It is a diagram of the shipped ARITHMETIC driven by the
    /// shipped DATA: the cape's own <c>HullMeshDef</c> is loaded from the asset, and the roots are computed
    /// by the production functions — <c>FoamBuffer.SternWorld</c> for the advected sheet and
    /// <c>WakeGrading.SternAnchorFromRoot</c> for the sprite families. <b>It is NOT a photograph of
    /// rendered foam.</b> A rendered plate needs a live boat with her emitter, her pools and the foam
    /// buffer's render feature in a PlayMode scene; that fixture is not built, and saying so is cheaper
    /// than a picture that overclaims.</para>
    ///
    /// <para>The gap that leaves — "do the emitter's call sites actually go through the one root?" — is
    /// closed by a source tripwire beside this, not by the picture.</para>
    /// </summary>
    public class OneWakeRootPlateTests
    {
        const string OutDir = "artifacts/row29-one-wake";
        const int Cell = 420;                    // px per heading panel
        const float MetresAcross = 34f;          // world metres the panel spans

        /// <summary>The three headings the plate is shot at — entering, mid and leaving a turn to
        /// starboard, which is where the families used to separate.</summary>
        static readonly float[] Headings = { 0f, 45f, 90f };

        static Vector2 Bow(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        }

        [Test]
        public void ThePlate_TheThreeRootsThroughATurn_BeforeAndAfter()
        {
            // The cape, from her SHIPPED def — not from constants transcribed into this file.
            HullMeshDef cape = LoadCape();
            float lofted = cape.WakeSternOffsetMeters;
            float halfBeam = cape.WatertightHalfBeamMeters;
            float elevation = cape.ElevationDeg;
            float nominalLength = lofted * 2f;    // what BoatHullDef.LengthMeters stands in for here

            Assert.Greater(lofted, 0f, "the cape must carry a lofted transom offset");
            Directory.CreateDirectory(OutDir);

            var report = new StringBuilder();
            report.AppendLine("row 29 — where the three families spring from, through a turn");
            report.AppendLine($"  {cape.name}: lofted transom {lofted:0.00} m, half-beam {halfBeam:0.00} m, " +
                              $"bake elevation {elevation:0.#} deg");
            report.AppendLine();
            report.AppendLine("heading | arm    | sheet root         | sprite root        | separation");

            var rows = new List<Color[][]>();
            float worstBefore = 0f, worstAfter = 0f;

            foreach (float heading in Headings)
            {
                Vector2 bow = Bow(heading);
                var origin = Vector2.zero;
                var pair = new Color[2][];

                for (int arm = 0; arm < 2; arm++)
                {
                    bool after = arm == 1;
                    // The SHEET always used the lofted transom (PR 11a). The SPRITE families used half a
                    // NOMINAL length plus a nudge before row 29, and the same lofted root after it.
                    Vector2 sheet = HiddenHarbours.Art.FoamBuffer.SternWorld(origin, bow, lofted, elevation);
                    Vector2 sprite = after
                        ? WakeGrading.SternAnchorFromRoot(origin, bow, lofted, 0.15f, elevation)
                        : WakeGrading.SternAnchor(origin, bow, nominalLength + 0.1f, 0.15f, elevation);

                    float sep = Vector2.Distance(sheet, sprite);
                    if (after) worstAfter = Mathf.Max(worstAfter, sep);
                    else worstBefore = Mathf.Max(worstBefore, sep);

                    report.AppendLine(
                        $"{heading,6:0}° | {(after ? "after " : "before")} | " +
                        $"({sheet.x,6:0.00},{sheet.y,6:0.00}) | ({sprite.x,6:0.00},{sprite.y,6:0.00}) | " +
                        $"{sep:0.000} m ({sep / (2f * halfBeam):0.00} of her beam)");

                    pair[arm] = Draw(origin, bow, lofted, halfBeam, elevation, sheet, sprite);
                }
                rows.Add(pair);
            }

            report.AppendLine();
            report.AppendLine($"worst separation BEFORE {worstBefore:0.000} m, AFTER {worstAfter:0.000} m");
            File.WriteAllText(Path.Combine(OutDir, "ROW29.txt"), report.ToString());
            Debug.Log("[row29] the three roots\n" + report);

            string sheetPath = Path.Combine(Directory.GetCurrentDirectory(), OutDir, "SHEET-row29-one-wake.png");
            WriteSheet(sheetPath, rows);
            Assert.IsTrue(File.Exists(sheetPath), "the sheet must be written — the FILE is the evidence");

            Assert.Greater(worstBefore, 0.05f,
                "DEAD CONTROL: the BEFORE arm must actually separate, or the plate is drawing one root " +
                "twice and shows nothing.");
            Assert.LessOrEqual(worstAfter, 0.16f,
                "AFTER, the two roots may differ only by the deposits' own named nudge (0.15 m from the " +
                "shared root), never by a second opinion about where the boat ends.");
        }

        static HullMeshDef LoadCape()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:HullMeshDef"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("CapeIslander")) continue;
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(path);
                if (def != null) return def;
            }
            Assert.Ignore("SKIPPED, NOT VERIFIED — the cape's HullMeshDef was not found.");
            return null;
        }

        /// <summary>One panel: the hull drawn as her own footprint at this heading, her transom, and the two
        /// roots marked. World metres map to pixels through <see cref="MetresAcross"/>.</summary>
        static Color[] Draw(Vector2 origin, Vector2 bow, float lofted, float halfBeam, float elevation,
                            Vector2 sheet, Vector2 sprite)
        {
            var px = new Color[Cell * Cell];
            var sea = new Color(0.07f, 0.11f, 0.15f, 1f);
            for (int i = 0; i < px.Length; i++) px[i] = sea;

            float scale = Cell / MetresAcross;
            Vector2 centre = new Vector2(Cell, Cell) * 0.5f;
            Vector2 ToPx(Vector2 w) => centre + new Vector2(w.x, w.y) * scale;

            // The hull's own footprint, projected the way her art is: a capsule from stem to transom.
            Vector2 stem = WakeRootMath.ProjectAlongHeading(origin, bow, lofted, elevation);
            Vector2 transom = WakeRootMath.SternWorld(origin, bow, lofted, elevation);
            DrawCapsule(px, ToPx(stem), ToPx(transom), halfBeam * scale, new Color(0.34f, 0.28f, 0.22f, 1f));
            DrawLine(px, ToPx(stem), ToPx(transom), new Color(0.55f, 0.46f, 0.36f, 1f));

            // The transom itself — the point every family should spring from.
            DrawRing(px, ToPx(transom), 9f, 2.0f, new Color(0.85f, 0.85f, 0.88f, 1f));
            // The advected sheet's root, and the sprite families'.
            DrawDisc(px, ToPx(sheet), 5.5f, new Color(0.35f, 0.75f, 0.95f, 1f));
            DrawDisc(px, ToPx(sprite), 4.0f, new Color(0.98f, 0.62f, 0.24f, 1f));
            return px;
        }

        static void Plot(Color[] px, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= Cell || y >= Cell) return;
            px[y * Cell + x] = c;
        }

        static void DrawDisc(Color[] px, Vector2 at, float r, Color c)
        {
            int ri = Mathf.CeilToInt(r);
            for (int dy = -ri; dy <= ri; dy++)
            for (int dx = -ri; dx <= ri; dx++)
                if (dx * dx + dy * dy <= r * r)
                    Plot(px, Mathf.RoundToInt(at.x) + dx, Mathf.RoundToInt(at.y) + dy, c);
        }

        static void DrawRing(Color[] px, Vector2 at, float r, float thickness, Color c)
        {
            int ri = Mathf.CeilToInt(r + thickness);
            for (int dy = -ri; dy <= ri; dy++)
            for (int dx = -ri; dx <= ri; dx++)
            {
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= r + thickness && d >= r - thickness)
                    Plot(px, Mathf.RoundToInt(at.x) + dx, Mathf.RoundToInt(at.y) + dy, c);
            }
        }

        static void DrawLine(Color[] px, Vector2 a, Vector2 b, Color c)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b)) + 1;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                Plot(px, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), c);
            }
        }

        static void DrawCapsule(Color[] px, Vector2 a, Vector2 b, float r, Color c)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b)) + 1;
            for (int i = 0; i <= steps; i++)
                DrawDisc(px, Vector2.Lerp(a, b, i / (float)steps), r, c);
        }

        /// <summary>Three rows (the headings) × two columns (before, after).</summary>
        static void WriteSheet(string path, List<Color[][]> rows)
        {
            const int gap = 8;
            int w = Cell * 2 + gap;
            int h = Cell * rows.Count + gap * (rows.Count - 1);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.03f, 0.04f, 0.05f, 1f);
            for (int r = 0; r < rows.Count; r++)
            for (int col = 0; col < 2; col++)
            {
                Color[] img = rows[r][col];
                int ox = col * (Cell + gap);
                int oy = (rows.Count - 1 - r) * (Cell + gap);
                for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                    px[(oy + y) * w + ox + x] = img[y * Cell + x];
            }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>
        /// 🔴 <b>THE TRIPWIRE THE PICTURE CANNOT BE.</b> The plate shows that the two ROOT FUNCTIONS agree;
        /// it cannot show that <c>BoatWakeEmitter</c> actually calls them. This does: every stern anchor in
        /// the emitter must go through the one root, and the length-derived <c>SternAnchor</c> must not
        /// appear there at all.
        /// </summary>
        [Test]
        public void TheEmittersSternAnchors_AllGoThroughTheOneRoot()
        {
            const string rel = "Assets/_Project/Code/Boats/BoatWakeEmitter.cs";
            string path = Path.Combine(Application.dataPath, "..", rel);
            Assert.IsTrue(File.Exists(path), "BoatWakeEmitter not found at " + rel);
            string code = File.ReadAllText(path);

            int fromRoot = CountOf(code, "WakeGrading.SternAnchorFromRoot(");
            int legacy = CountOf(code, "WakeGrading.SternAnchor(");
            TestContext.WriteLine($"BoatWakeEmitter: {fromRoot} anchors from the one root, {legacy} legacy");

            Assert.AreEqual(3, fromRoot,
                "All THREE of the emitter's stern anchors — the plume/roll, the deposits and the plume " +
                "apex — must take the hull's own lofted transom.");
            Assert.AreEqual(0, legacy,
                "The length-derived SternAnchor must not survive in the emitter: it is the second opinion " +
                "about where the boat ends that row 29 exists to remove. (It stays on WakeGrading for the " +
                "sprite fleet and its own tests, which is why this is a tripwire and not a deletion.)");
            StringAssert.Contains("SternOffsetMeters()", code,
                "...and the offset must come from the presenter seam, not a constant.");
        }

        static int CountOf(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }
    }
}
