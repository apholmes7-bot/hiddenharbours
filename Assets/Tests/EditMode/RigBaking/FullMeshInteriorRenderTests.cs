using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>ADR 0041 — the room, drawn.</b> Renders a converted hull from her COMMITTED def (no spike
    /// extrusion, no rig call at render time) closed up and cut open, asserts the two are what they
    /// claim to be, and writes the owner's eyeball pack.
    ///
    /// <para><b>The two assertions that make this a test rather than a picture generator.</b>
    /// Closed up, her room must contribute <i>nothing</i> — the shipped picture is the shipped
    /// picture, and that is measured against the same hull rendered from a mesh with the room's
    /// faces stripped out, not against a remembered number. Cut open, the room must actually
    /// ARRIVE: a cut that reveals a handful of pixels is a cut that is not working, and the spike
    /// already measured what "not working" looks like (a room surviving at 20.3% because the hull's
    /// near topsides stand in front of it).</para>
    ///
    /// <para>⚠️ GPU-only, by nature. CI has no graphics device and skips loudly rather than
    /// pretending; the pack is produced on the local card.</para>
    /// </summary>
    public class FullMeshInteriorRenderTests
    {
        const string HullMeshPath = "Assets/_Project/Data/Boats/HullMeshes/LobsterBoatIsoHullMesh.asset";
        const string ImageDir = "docs/art/spikes/full-mesh-interiors";
        const int ProbeLayer = 31;
        const string LevelGateKeyword = "HH_LEVEL_GATE";

        /// <summary>Headings for the pack. Beam and quarter, because a ¾ view is where the hull's
        /// own near topsides stand between the camera and a cabin sole — the case the depth shift
        /// exists for.</summary>
        static readonly float[] Headings = { 90f, 135f, 180f, 45f };

        static string RepoRoot => Directory.GetParent(Application.dataPath).FullName;

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device (Renderer: Null Device). " +
                              "These pictures need the local GPU; CI cannot produce them.");
        }

        static HullMeshDef LoadHullOrIgnore()
        {
            var hm = AssetDatabase.LoadAssetAtPath<HullMeshDef>(HullMeshPath);
            if (hm == null) Assert.Ignore($"{HullMeshPath} is not present — bake her first.");
            if (hm.InteriorRamps == null || hm.InteriorRamps.Length == 0)
                Assert.Ignore($"{HullMeshPath} carries no interior palette, so she has not been " +
                              "converted to a mesh room yet. Add her to " +
                              "RigMeshAssetBaker.MeshInteriorHulls and re-bake.");
            return hm;
        }

        // ============================================================ the two claims, measured

        /// <summary>
        /// <b>CLOSED UP, THE ROOM COSTS NOTHING — measured against a mesh with the room removed,
        /// not against a remembered number.</b>
        ///
        /// <para>This is the claim the whole design rests on and the one that was WRONG the first
        /// time it was built: the room's faces live in the hull mesh, and the only thing that hides
        /// them is a discard inside <c>HH_LEVEL_GATE</c>. With the keyword off she drew her cabin
        /// through her own topsides at 31–42% of her inked pixels. The control arm here is the
        /// hull's own faces alone, so the assertion cannot pass by both arms being broken the same
        /// way.</para>
        /// </summary>
        [Test]
        public void ClosedUp_TheRoomChangesNothing_AgainstAHullWithNoRoomAtAll()
        {
            RequireAGraphicsDevice();
            HullMeshDef hm = LoadHullOrIgnore();

            Mesh stripped = MeshWithoutTheRoom(hm.Mesh, out int roomVerts, out int hullVerts);
            Assert.Greater(roomVerts, 0,
                "this hull's mesh carries no room-flagged vertices, so the control arm is the same " +
                "mesh twice and would pass on any defect whatsoever.");

            var log = new StringBuilder();
            log.AppendLine("CLOSED UP — the full mesh against a hull with the room stripped out");
            log.AppendLine($"hull verts {hullVerts}, room verts {roomVerts}");
            try
            {
                foreach (float heading in Headings)
                {
                    byte[] full = Render(hm, hm.Mesh, cut: 0, heading: heading);
                    byte[] hullOnly = Render(hm, stripped, cut: 0, heading: heading);
                    int differ = CountDiffering(full, hullOnly);
                    log.AppendLine($"  heading {heading,5:0}°  differing px {differ}");
                    Assert.AreEqual(0, differ,
                        $"closed up at {heading}°, {differ} pixels differ between the full mesh and " +
                        "the same hull with her room stripped out. The room is drawing when nobody " +
                        "is aboard — check that ApplyCutawayKeyword still keeps HH_LEVEL_GATE on " +
                        "for a hull that carries room geometry, because the discard that hides her " +
                        "cabin lives only inside that keyword.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(stripped); }

            WriteReport("closed-up-costs-nothing.txt", log.ToString());
        }

        /// <summary>
        /// <b>CUT OPEN, THE ROOM ARRIVES.</b> The floor is deliberately not "more than zero": the
        /// spike measured a room that WAS revealed and still only survived at 20.3%, because the
        /// hull's near topsides sit in front of a cabin sole in a ¾ view. A cut that draws a few
        /// hundred pixels of room has the same failure and would pass a nonzero test.
        /// </summary>
        [Test]
        public void CutOpen_TheRoomActuallyArrives_AtEveryHeading()
        {
            RequireAGraphicsDevice();
            HullMeshDef hm = LoadHullOrIgnore();

            var log = new StringBuilder();
            log.AppendLine("CUT OPEN — how much of the picture the room accounts for");
            log.AppendLine("Floor is 4% of the hull's own inked area: the spike's failing case sat at");
            log.AppendLine("20.3% of the ROOM revealed, which still reads as a room that is not there.");

            bool any = false;
            foreach (HullMeshDef.LevelTag lvl in hm.LevelTags)
            {
                if (!lvl.Enclosed) continue;          // an open working deck has no room to reveal
                any = true;
                foreach (float heading in Headings)
                {
                    byte[] closed = Render(hm, hm.Mesh, 0, heading);
                    byte[] open = Render(hm, hm.Mesh, lvl.Tag, heading, lvl.LidTag);
                    int inkedClosed = CountInked(closed);
                    int differ = CountDiffering(closed, open);
                    double pct = 100.0 * differ / Math.Max(1, inkedClosed);
                    log.AppendLine($"  {lvl.LevelId,-10} tag {lvl.Tag} lid {lvl.LidTag}  " +
                                   $"heading {heading,5:0}°  changed {differ} px of {inkedClosed} " +
                                   $"inked ({pct:0.0}%)");
                    Assert.Greater(pct, 4.0,
                        $"cutting '{lvl.LevelId}' open at {heading}° changed only {pct:0.0}% of her " +
                        "inked pixels. Either the level tag does not match the geometry, or the " +
                        "room is drawing BEHIND the hull — UV0.z (the depth shift) is what puts it " +
                        "in front, and the spike measured 20.3% survival when that was missing.");
                }
            }
            Assert.IsTrue(any, "this hull declares no enclosed level, so nothing was tested.");
            WriteReport("cut-open-the-room-arrives.txt", log.ToString());
        }

        /// <summary>
        /// <b>THE CONTROL FOR EVERY OTHER TEST IN THIS FILE: do the headings actually reach the
        /// renderer?</b> Without this, a fixture that silently renders one heading four times passes
        /// its per-heading assertions four times and reports numbers that repeat to the digit — and
        /// repeating numbers read like a stable measurement, not like an absent one.
        /// </summary>
        [Test]
        public void HeadingsAreActuallyApplied_OrEveryPerHeadingNumberHereIsOneNumber()
        {
            RequireAGraphicsDevice();
            HullMeshDef hm = LoadHullOrIgnore();

            byte[] first = Render(hm, hm.Mesh, 0, Headings[0]);
            for (int i = 1; i < Headings.Length; i++)
            {
                int differ = CountDiffering(first, Render(hm, hm.Mesh, 0, Headings[i]));
                Assert.Greater(differ, 500,
                    $"heading {Headings[i]}° renders all but identically to {Headings[0]}° " +
                    $"({differ} px differ). The pose is not reaching the renderer — ApplyPose() is " +
                    "driven by LateUpdate, which EditMode does not run, so it must be called by hand " +
                    "after setting HeadingDirUnits.");
            }
        }

        /// <summary>
        /// <b>DIAGNOSTIC: where does the rigging level actually draw?</b> Raised by the coordinator
        /// off the eyeball pack — a pole appears to hang below her keel, while her baked sheet draws
        /// the tackle standing up from the house roof. Her rig puts those faces at z 2.953..5.958,
        /// i.e. above the house (top 3.055), and they are NOT new: the July rig that baked her sheet
        /// has the same 80 faces above z 3.10, topping at the same 5.958. So staleness cannot explain
        /// it and the question is what the MESH path does with them.
        ///
        /// <para>This isolates them: cutting level 5 removes exactly the rigging faces, so the pixels
        /// that change ARE the rigging, and their screen rows say which way it points. Reported, not
        /// asserted — the fix (if there is one) is not this PR's, and a bare assertion here would
        /// encode a conclusion nobody has reached yet.</para>
        /// </summary>
        [Test]
        public void Diagnostic_WhereTheRiggingDraws()
        {
            RequireAGraphicsDevice();
            HullMeshDef hm = LoadHullOrIgnore();

            const int RiggingTag = 5;
            var log = new StringBuilder();
            log.AppendLine("WHERE THE RIGGING DRAWS — level 5 culled vs not");
            log.AppendLine("Her rig places these faces at z 2.953..5.958; the house tops at 3.055.");
            log.AppendLine("Rows are screen rows, 0 = TOP of the cell. Lower row number = higher up.");
            log.AppendLine();

            foreach (float heading in Headings)
            {
                byte[] whole = Render(hm, hm.Mesh, 0, heading);
                byte[] noRig = Render(hm, hm.Mesh, RiggingTag, heading);

                int w = hm.CellW, h = hm.CellH;
                int minRow = int.MaxValue, maxRow = -1, n = 0;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = (y * w + x) * 4;
                        bool diff = whole[i] != noRig[i] || whole[i + 1] != noRig[i + 1]
                                 || whole[i + 2] != noRig[i + 2] || whole[i + 3] != noRig[i + 3];
                        if (!diff) continue;
                        n++;
                        if (y < minRow) minRow = y;
                        if (y > maxRow) maxRow = y;
                    }

                (int top, int bottom) hull = InkedRows(noRig, w, h);
                (int top, int bottom) all = InkedRows(whole, w, h);
                log.AppendLine($"  heading {heading,5:0}°  whole rows {all.top}..{all.bottom}"
                             + $"  |  without rigging {hull.top}..{hull.bottom}"
                             + $"  |  rigging {n,5} px, rows {minRow}..{maxRow}");
                // SELF-CHECK. Every changed pixel must lie inside the whole render's own inked band;
                // a diff outside it means the two arms are not the same boat and the rest of this
                // line is meaningless. The first cut of this diagnostic reported rigging at row 91
                // against a whole-image top of 128 and I nearly published the conclusion anyway.
                bool sane = n == 0 || (minRow >= all.top && maxRow <= all.bottom);
                log.AppendLine($"  {"",13} self-check: changed rows inside the whole render's band? "
                             + (sane ? "yes" : "NO — THIS LINE IS NOT TRUSTWORTHY"));
                if (sane)
                    log.AppendLine($"  {"",13} rigging sits {(minRow < hull.top ? "ABOVE" : "not above")} "
                                 + $"the hull's top, and {(maxRow > hull.bottom ? "BELOW" : "not below")} "
                                 + "its bottom.");
            }
            WriteReport("where-the-rigging-draws.txt", log.ToString());
        }

        static (int top, int bottom) InkedRows(byte[] rgba, int w, int h)
        {
            int top = int.MaxValue, bottom = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (rgba[(y * w + x) * 4 + 3] > 8) { if (y < top) top = y; if (y > bottom) bottom = y; }
            return (top, bottom);
        }

        /// <summary>
        /// <b>THE PACK IS THE RIGHT WAY UP.</b> Her rigging sits at z 2.953..5.958, above a house
        /// that tops at 3.055, so in a published frame the mast's ink must appear ABOVE the hull's
        /// centre of mass, not below it. Cheap, and it is the check the first pack did not have:
        /// every number in this file is computed on the render buffer, so an inverted FILE is
        /// invisible to all of them.
        /// </summary>
        [Test]
        public void ThePublishedPack_IsNotUpsideDown()
        {
            RequireAGraphicsDevice();
            HullMeshDef hm = LoadHullOrIgnore();
            const int RiggingTag = 5;

            foreach (float heading in Headings)
            {
                byte[] whole = Render(hm, hm.Mesh, 0, heading);
                byte[] noRig = Render(hm, hm.Mesh, RiggingTag, heading);
                int w = hm.CellW, h = hm.CellH;

                // the rigging's centre row, and the hull's, in PUBLISHED (top-left) orientation
                long rigSum = 0, rigN = 0, hullSum = 0, hullN = 0;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = (y * w + x) * 4;
                        bool changed = whole[i] != noRig[i] || whole[i + 1] != noRig[i + 1]
                                    || whole[i + 2] != noRig[i + 2] || whole[i + 3] != noRig[i + 3];
                        if (changed) { rigSum += y; rigN++; }
                        else if (noRig[i + 3] > 8) { hullSum += y; hullN++; }
                    }
                Assert.Greater(rigN, 0, $"no rigging pixels at {heading}° — this guard tested nothing.");

                double rigRow = (double)rigSum / rigN, hullRow = (double)hullSum / hullN;
                // ⚠️ NO ARITHMETIC HERE, ON PURPOSE. The published row EQUALS the buffer row: the
                // pipeline flips twice — ReadBackTopLeft turns the bottom-left RenderTexture into a
                // top-left buffer, SavePng flips that back into Texture2D's bottom-left space, and
                // EncodeToPNG writes top-down. Two flips compose to identity, and it is VERIFIED
                // that way rather than reasoned: the published files' inked row spans equal these
                // buffers' to the row at all four headings. The first cut of this guard "corrected"
                // for a flip that cancels and failed on a pack that was by then correct — a guard
                // that models the pipeline instead of reading it is one more thing to get wrong.
                Assert.Less(rigRow, hullRow,
                    $"at {heading}° the mast's ink sits BELOW the hull's (rows {rigRow:0} vs " +
                    $"{hullRow:0}). Her rigging is at z 2.953..5.958 over a house topping at 3.055, " +
                    "so this is upside down. Check SavePng: ReadBackTopLeft is top-left origin and " +
                    "Texture2D is bottom-left, and every OTHER measurement in this file reads the " +
                    "buffer rather than the file, so all of them stay green through the defect.");
            }
        }

        /// <summary>The owner's pack: every enclosed level, closed and open, at every heading, plus
        /// the closed-up control. Not an assertion — this one exists to be looked at.</summary>
        [Test]
        public void EyeballPack_IsWritten()
        {
            RequireAGraphicsDevice();
            HullMeshDef hm = LoadHullOrIgnore();

            foreach (float heading in Headings)
            {
                SavePng($"lobster-{heading:000}-closed.png", Render(hm, hm.Mesh, 0, heading), hm);
                foreach (HullMeshDef.LevelTag lvl in hm.LevelTags)
                {
                    if (!lvl.Enclosed) continue;
                    SavePng($"lobster-{heading:000}-open-{lvl.LevelId}.png",
                            Render(hm, hm.Mesh, lvl.Tag, heading, lvl.LidTag), hm);
                }
            }
            UnityEngine.Debug.Log($"[full-mesh-interiors] eyeball pack written to {ImageDir}");
        }

        // ============================================================================ machinery

        /// <summary>
        /// The same mesh with every room-flagged face removed — the control arm. Built by filtering
        /// TexCoord1.y, the flag the bake writes, so it removes exactly what the shader's discard
        /// would have hidden and nothing else.
        /// </summary>
        static Mesh MeshWithoutTheRoom(Mesh src, out int roomVerts, out int hullVerts)
        {
            var tags = new System.Collections.Generic.List<Vector2>();
            src.GetUVs(1, tags);
            var uv0 = new System.Collections.Generic.List<Vector4>();
            src.GetUVs(0, uv0);
            Vector3[] v = src.vertices, n = src.normals;
            int[] tri = src.triangles;

            var keepV = new System.Collections.Generic.List<Vector3>();
            var keepN = new System.Collections.Generic.List<Vector3>();
            var keepA = new System.Collections.Generic.List<Vector4>();
            var keepL = new System.Collections.Generic.List<Vector2>();
            var keepT = new System.Collections.Generic.List<int>();
            var remap = new int[v.Length];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;

            roomVerts = 0;
            for (int i = 0; i < v.Length; i++) if (tags[i].y > 0.5f) roomVerts++;
            hullVerts = v.Length - roomVerts;

            for (int t = 0; t + 2 < tri.Length; t += 3)
            {
                if (tags[tri[t]].y > 0.5f) continue;          // flat per face, so one vertex decides
                for (int k = 0; k < 3; k++)
                {
                    int src_i = tri[t + k];
                    if (remap[src_i] < 0)
                    {
                        remap[src_i] = keepV.Count;
                        keepV.Add(v[src_i]); keepN.Add(n[src_i]);
                        keepA.Add(uv0[src_i]); keepL.Add(tags[src_i]);
                    }
                    keepT.Add(remap[src_i]);
                }
            }

            var m = new Mesh { name = src.name + "_HullOnly", indexFormat = src.indexFormat };
            m.SetVertices(keepV); m.SetNormals(keepN);
            m.SetUVs(0, keepA); m.SetUVs(1, keepL);
            m.SetTriangles(keepT, 0, true);
            return m;
        }

        static byte[] Render(HullMeshDef def, Mesh mesh, int cut, float heading, int lid = 0)
        {
            var go = new GameObject("PackHull") { layer = ProbeLayer };
            try
            {
                var r = go.AddComponent<IsoFacetHullRenderer>();
                IsoFacetHullSetup setup = IsoFacetHullPresentationService.ToSetup(def);
                setup.Mesh = mesh;
                r.Configure(setup);
                r.ShowCutaway(new HullMeshDef.Cut(cut, lid));
                r.HeadingDirUnits = HullMeshMath.HeadingToDirUnits(heading, 0f,
                                                                   def.AzimuthCounterClockwise);
                // ⚠️ EXPLICITLY, and this is not a nicety. The pose reaches the property block from
                // LateUpdate, which EditMode never runs — so a heading set and not applied renders
                // the PREVIOUS heading, silently. It cost a whole pass here: every heading produced
                // a byte-identical picture and the cut-open numbers repeated to the digit, which
                // read as a suspiciously stable measurement rather than as no measurement at all.
                // ShowCutaway calls ApplyPose itself, which is exactly why the CUT appeared to work
                // while the heading did not. HeadingsAreActuallyApplied below is the guard.
                r.ApplyPose();
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = ProbeLayer;
                return RenderCell(def, def.CellW, def.CellH);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static byte[] RenderCell(HullMeshDef def, int w, int h)
        {
            float ppu = def.PxPerMetre;
            float ox = (def.PivotPx.x - def.CellW / 2f) / ppu;
            float oy = (def.CellH / 2f - def.PivotPx.y) / ppu;

            var camGo = new GameObject("PackCam");
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = def.CellH / (2f * ppu);
                cam.transform.position = new Vector3(-ox, -oy, -100f);
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 400f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.clear;
                cam.cullingMask = 1 << ProbeLayer;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.targetTexture = rt;

                WaitOutShaderCompilation(cam);
                cam.Render();
                return ReadBackTopLeft(rt, w, h);
            }
            finally
            {
                RenderTexture.active = null;
                camGo.GetComponent<Camera>().targetTexture = null;
                UnityEngine.Object.DestroyImmediate(camGo);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        static void WaitOutShaderCompilation(Camera cam)
        {
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                cam.Render();
                if (!ShaderUtil.anythingCompiling) return;
                while (ShaderUtil.anythingCompiling && clock.Elapsed.TotalSeconds < 180.0)
                    Thread.Sleep(25);
            }
            Assert.Fail("SHADERS NEVER FINISHED COMPILING — re-run with a warm cache.");
        }

        static byte[] ReadBackTopLeft(RenderTexture rt, int w, int h)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Color32[] px = tex.GetPixels32();
            UnityEngine.Object.DestroyImmediate(tex);

            var bytes = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                int src = (h - 1 - y) * w, dst = y * w;
                for (int x = 0; x < w; x++)
                {
                    Color32 c = px[src + x];
                    int o = (dst + x) * 4;
                    bytes[o] = c.r; bytes[o + 1] = c.g; bytes[o + 2] = c.b; bytes[o + 3] = c.a;
                }
            }
            return bytes;
        }

        static int CountInked(byte[] rgba)
        {
            int n = 0;
            for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] > 8) n++;
            return n;
        }

        static int CountDiffering(byte[] a, byte[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i += 4)
                if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3])
                    n++;
            return n;
        }

        /// <summary>
        /// ⚠️ <b>THE FLIP IS LOAD-BEARING.</b> <see cref="ReadBackTopLeft"/> hands back TOP-left-origin
        /// bytes (that is its whole job), and <c>Texture2D</c> is BOTTOM-left origin — so writing the
        /// buffer straight in through <c>LoadRawTextureData</c> saves the picture upside down while
        /// every measurement in this file, which works on the buffer and never on the file, stays
        /// perfectly correct.
        ///
        /// <para>That is exactly what happened: the first pack shipped inverted, and because the
        /// numbers were right and only the pictures were wrong, it survived my own eye, a commit, and
        /// a reviewer's — who reasonably read a mast standing correctly above the house as a pole
        /// hanging through the keel, and traced it toward a mesh-path sign flip that does not exist.
        /// A rendering fixture that measures one buffer and publishes another must flip in exactly
        /// one place, and this is it.</para>
        /// </summary>
        static void SavePng(string name, byte[] rgba, HullMeshDef hm)
        {
            string dir = Path.Combine(RepoRoot, ImageDir);
            Directory.CreateDirectory(dir);
            int w = hm.CellW, h = hm.CellH;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int s = (y * w + x) * 4;
                    px[(h - 1 - y) * w + x] = new Color32(rgba[s], rgba[s + 1], rgba[s + 2], rgba[s + 3]);
                }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static void WriteReport(string name, string text)
        {
            string dir = Path.Combine(RepoRoot, "docs", "design", "spikes");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), text);
            UnityEngine.Debug.Log("[full-mesh-interiors] " + name + "\n" + text);
        }
    }
}
