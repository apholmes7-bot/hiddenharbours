using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

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
        const string ImageDir = "docs/art/spikes/full-mesh-interiors";
        const int ProbeLayer = 31;
        const string LevelGateKeyword = "HH_LEVEL_GATE";

        /// <summary>
        /// One converted hull, as a test case. <see cref="ToString"/> is what NUnit shows, so a red
        /// names the boat rather than a struct dump.
        /// </summary>
        public readonly struct ConvertedHull
        {
            /// <summary>Short, stable, and it is also the eyeball pack's filename prefix —
            /// the interior rig's own vocabulary for this boat ("lobster", "cape",
            /// "lobvar-inshore-hardtop-fundy").</summary>
            public readonly string Name;
            public readonly string MeshPath;
            /// <summary>Whether her pack and reports are COMMITTED (the owner's sample) or written
            /// to the temporary cache (the fixture carries the rest). See <see cref="CommittedPacks"/>.</summary>
            public readonly bool Committed;

            public ConvertedHull(string name, string meshPath, bool committed)
            {
                Name = name; MeshPath = meshPath; Committed = committed;
            }
            public override string ToString() => Name;
        }

        /// <summary>
        /// The pack/report prefix per converted hull, keyed by the FLEET KEY (a rig global is shared by
        /// a whole generator family — the eighteen lobster variants are one global). ⚠️ This map does
        /// NOT decide WHICH hulls are tested — <see cref="ConvertedHulls"/> derives that from
        /// <see cref="RigMeshAssetBaker.MeshInteriorHulls"/>, the bake's own switch, so the fixture
        /// cannot drift from what was actually baked. All this supplies is the human-facing name,
        /// and a hull converted without one FAILS LOUDLY there rather than quietly writing a pack
        /// called "-090-closed.png". The variants' names are DERIVED from the fleet's own list, so a
        /// nineteenth variant would arrive named rather than unnamed.
        /// </summary>
        static readonly IReadOnlyDictionary<string, string> PackNames = BuildPackNames();

        static IReadOnlyDictionary<string, string> BuildPackNames()
        {
            var names = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["lobsterBoat"] = "lobster",
                ["capeIslander"] = "cape",
                // The four working ships (rollout PR 2) — the interior rig's own keys for them.
                ["sideDragger"] = "dragger",
                ["sternTrawler"] = "trawler",
                ["sternTrawlerMk2"] = "trawler2",
                ["coastalPacket"] = "packet",
            };
            foreach (LobsterVariant v in LobsterVariantFleet.All)
                names[v.Key] = $"lobvar-{v.Size}-{v.Style}-{v.Region}";
            return names;
        }

        /// <summary>
        /// The hulls whose eyeball packs and reports are COMMITTED under <see cref="ImageDir"/> —
        /// the owner's sample: the two first conversions, and for the eighteen variants one hardtop,
        /// one open, one per rig region (the handoff's rule). Every other converted hull is measured
        /// by exactly the same guards; her pack goes to the temporary cache so a full sweep does not
        /// rewrite two hundred committed pictures, and anyone can look at it from the log's path.
        /// </summary>
        static readonly HashSet<string> CommittedPacks = new(StringComparer.Ordinal)
        {
            "lobsterBoat",
            "capeIslander",
            "lobsterInshoreHardtopFundy",            // hardtop, Fundy
            "lobsterOffshoreOpenNewfoundland",       // open, Newfoundland
            "lobsterStandardHardtopNorthumberland",  // hardtop, Northumberland
            // The four working ships: all four, because unlike the eighteen they are four SEPARATE
            // rig files making four different boats, so there is no family for a sample to stand in
            // for — and each is the first hull of her size the room has ever been cut out of.
            "sideDragger",
            "sternTrawler",
            "sternTrawlerMk2",
            "coastalPacket",
        };

        /// <summary>
        /// <b>Every hull the baker actually converted — read from the baker, not transcribed.</b>
        /// Adding a hull to <see cref="RigMeshAssetBaker.MeshInteriorHulls"/> and re-baking is what
        /// enrols her here; a rollout batch adds a NAME to <see cref="PackNames"/>, never a file.
        /// The two-copies-of-the-rule drift this avoids is the same one
        /// <c>AppendMeshInteriorIfConverted</c> is factored to avoid on the bake side.
        /// </summary>
        public static IEnumerable<ConvertedHull> ConvertedHulls()
        {
            var found = new List<ConvertedHull>();
            var matched = new HashSet<string>(StringComparer.Ordinal);

            foreach (FleetHull hull in HullMeshFleet.Hulls)
            {
                if (!RigMeshAssetBaker.IsMeshInteriorHull(hull.GlobalName)) continue;
                if (!PackNames.TryGetValue(hull.Key, out string name))
                    throw new InvalidOperationException(
                        $"'{hull.Key}' ({hull.GlobalName}) is converted by RigMeshAssetBaker." +
                        "MeshInteriorHulls but FullMeshInteriorRenderTests.PackNames has no short " +
                        "name for her, so her eyeball pack and her reports would be written without a " +
                        "prefix — and would overwrite another hull's. Add her name.");
                matched.Add(hull.GlobalName);
                found.Add(new ConvertedHull(name, hull.MeshAssetPath, CommittedPacks.Contains(hull.Key)));
            }

            // ⚠️ COMPLETENESS, AND EAGERLY — the reason this method is a list and not an iterator.
            // A name on MeshInteriorHulls that matches no fleet hull (a typo, or a hull retired from
            // the catalog) is INERT everywhere else: the bake only ever walks the fleet, so it never
            // looks the name up and never complains, and an iterator would simply yield one case
            // fewer. The suite would go green having quietly stopped testing a converted hull —
            // the same shape as the guard above, one level up. Built eagerly so this throws at
            // DISCOVERY, where it is visible, rather than mid-enumeration.
            string[] unmatched = RigMeshAssetBaker.MeshInteriorHulls
                                                  .Where(g => !matched.Contains(g)).ToArray();
            if (unmatched.Length > 0)
                throw new InvalidOperationException(
                    $"RigMeshAssetBaker.MeshInteriorHulls names {string.Join(", ", unmatched)}, " +
                    "which no hull in HullMeshFleet.Hulls carries as a GlobalName. Either the name " +
                    "is misspelled or she is no longer in the catalog — and until it is fixed she " +
                    "is converted in the bake's eyes and untested in this fixture's.");

            Assert.IsNotEmpty(found, "no converted hulls at all — this whole fixture would vacuously pass.");
            return found;
        }

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

        static HullMeshDef LoadHullOrIgnore(ConvertedHull hull)
        {
            var hm = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshPath);
            if (hm == null) Assert.Ignore($"{hull.MeshPath} is not present — bake her first.");
            if (hm.InteriorRamps == null || hm.InteriorRamps.Length == 0)
                Assert.Ignore($"{hull.MeshPath} carries no interior palette, so she has not been " +
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
        [TestCaseSource(nameof(ConvertedHulls))]
        public void ClosedUp_TheRoomChangesNothing_AgainstAHullWithNoRoomAtAll(ConvertedHull hull)
        {
            RequireAGraphicsDevice();
            HullMeshDef hm = LoadHullOrIgnore(hull);

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

            WriteReport($"{hull.Name}-closed-up-costs-nothing.txt", log.ToString(), hull.Committed);
        }

        /// <summary>
        /// <b>CUT OPEN, THE ROOM ARRIVES.</b> The floor is deliberately not "more than zero": the
        /// spike measured a room that WAS revealed and still only survived at 20.3%, because the
        /// hull's near topsides sit in front of a cabin sole in a ¾ view. A cut that draws a few
        /// hundred pixels of room has the same failure and would pass a nonzero test.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(ConvertedHulls))]
        public void CutOpen_TheRoomActuallyArrives_AtEveryHeading(ConvertedHull hull)
        {
            RequireAGraphicsDevice();
            HullMeshDef hm = LoadHullOrIgnore(hull);

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
            WriteReport($"{hull.Name}-cut-open-the-room-arrives.txt", log.ToString(), hull.Committed);
        }

        /// <summary>
        /// <b>THE CONTROL FOR EVERY OTHER TEST IN THIS FILE: do the headings actually reach the
        /// renderer?</b> Without this, a fixture that silently renders one heading four times passes
        /// its per-heading assertions four times and reports numbers that repeat to the digit — and
        /// repeating numbers read like a stable measurement, not like an absent one.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(ConvertedHulls))]
        public void HeadingsAreActuallyApplied_OrEveryPerHeadingNumberHereIsOneNumber(ConvertedHull hull)
        {
            RequireAGraphicsDevice();
            HullMeshDef hm = LoadHullOrIgnore(hull);

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
        [TestCaseSource(nameof(ConvertedHulls))]
        public void Diagnostic_WhereTheRiggingDraws(ConvertedHull hull)
        {
            RequireAGraphicsDevice();
            HullMeshDef hm = LoadHullOrIgnore(hull);

            int RiggingTag = TopStructureTag(hm, out string topTable);
            var log = new StringBuilder();
            log.AppendLine($"WHERE THE TOP STRUCTURE DRAWS — level {RiggingTag} culled vs not");
            log.AppendLine("The tag is DERIVED from the mesh (highest exterior geometry), not named.");
            log.AppendLine("Rows are screen rows, 0 = TOP of the cell. Lower row number = higher up.");
            log.AppendLine();
            log.AppendLine(topTable);
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

                (int top, int bottom) hullRows = InkedRows(noRig, w, h);
                (int top, int bottom) all = InkedRows(whole, w, h);
                log.AppendLine($"  heading {heading,5:0}°  whole rows {all.top}..{all.bottom}"
                             + $"  |  without rigging {hullRows.top}..{hullRows.bottom}"
                             + $"  |  rigging {n,5} px, rows {minRow}..{maxRow}");
                // SELF-CHECK. Every changed pixel must lie inside the whole render's own inked band;
                // a diff outside it means the two arms are not the same boat and the rest of this
                // line is meaningless. The first cut of this diagnostic reported rigging at row 91
                // against a whole-image top of 128 and I nearly published the conclusion anyway.
                bool sane = n == 0 || (minRow >= all.top && maxRow <= all.bottom);
                log.AppendLine($"  {"",13} self-check: changed rows inside the whole render's band? "
                             + (sane ? "yes" : "NO — THIS LINE IS NOT TRUSTWORTHY"));
                if (sane)
                    log.AppendLine($"  {"",13} rigging sits {(minRow < hullRows.top ? "ABOVE" : "not above")} "
                                 + $"the hull's top, and {(maxRow > hullRows.bottom ? "BELOW" : "not below")} "
                                 + "its bottom.");
            }
            WriteReport($"{hull.Name}-where-the-rigging-draws.txt", log.ToString(), hull.Committed);
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
        /// <b>THE PUBLISHED FILE IS THE BUFFER.</b> Every number in this file is computed on the
        /// render buffer, so a file saved the wrong way up is invisible to all of them — which is
        /// exactly how the first pack shipped inverted with every test green. This guard closes that
        /// hole at its source: the PNG <see cref="SavePng"/> writes is read back and must equal the
        /// buffer BYTE FOR BYTE (PNG is lossless; a flip, a channel swap or a stride error all differ
        /// on tens of thousands of pixels). No geometry is assumed.
        ///
        /// <para>The first form of this guard asserted "the mast's ink sits above the hull's mean row"
        /// and it was heading-fragile by construction: in a ¾ view the far end of a hull projects
        /// HIGHER on screen than a short mast amidships, and on two open/newfoundland lobster variants
        /// (whose masthead sits on the dry stack, aft and to port) it read a correctly oriented pack
        /// as upside down at 135°. A guard that models the projection is one more thing to get wrong;
        /// this one reads the file.</para>
        /// </summary>
        [Test]
        [TestCaseSource(nameof(ConvertedHulls))]
        public void ThePublishedPack_IsNotUpsideDown(ConvertedHull hull)
        {
            RequireAGraphicsDevice();
            HullMeshDef hm = LoadHullOrIgnore(hull);

            foreach (float heading in Headings)
            {
                byte[] whole = Render(hm, hm.Mesh, 0, heading);
                Assert.Greater(CountInked(whole), 0, $"nothing rendered at {heading}° — this guard would test nothing");

                string path = SavePng($"{hull.Name}-{heading:000}-roundtrip.png", whole, hm, committed: false);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(path), false),
                                  $"the PNG at {path} did not decode");
                    Assert.AreEqual(hm.CellW, tex.width); Assert.AreEqual(hm.CellH, tex.height);
                    Color32[] px = tex.GetPixels32();   // bottom-left origin, like every Texture2D
                    var fromFile = new byte[whole.Length];
                    int w = hm.CellW, h = hm.CellH;
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            Color32 c = px[(h - 1 - y) * w + x];   // to top-left, the buffer's frame
                            int o = (y * w + x) * 4;
                            fromFile[o] = c.r; fromFile[o + 1] = c.g; fromFile[o + 2] = c.b; fromFile[o + 3] = c.a;
                        }
                    int differ = CountDiffering(whole, fromFile);
                    Assert.AreEqual(0, differ,
                        $"at {heading}° the PNG on disk differs from the render buffer on {differ} px. " +
                        "SavePng is the ONE place the top-left buffer is flipped into Texture2D's " +
                        "bottom-left frame; a pack that fails here is published inverted (or worse) " +
                        "while every buffer-side number stays green.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                    File.Delete(path);
                }
            }
        }

        /// <summary>The owner's pack: every enclosed level, closed and open, at every heading, plus
        /// the closed-up control. Not an assertion — this one exists to be looked at.</summary>
        [Test]
        [TestCaseSource(nameof(ConvertedHulls))]
        public void EyeballPack_IsWritten(ConvertedHull hull)
        {
            RequireAGraphicsDevice();
            HullMeshDef hm = LoadHullOrIgnore(hull);

            foreach (float heading in Headings)
            {
                SavePng($"{hull.Name}-{heading:000}-closed.png", Render(hm, hm.Mesh, 0, heading), hm, hull.Committed);
                foreach (HullMeshDef.LevelTag lvl in hm.LevelTags)
                {
                    if (!lvl.Enclosed) continue;
                    SavePng($"{hull.Name}-{heading:000}-open-{lvl.LevelId}.png",
                            Render(hm, hm.Mesh, lvl.Tag, heading, lvl.LidTag), hm, hull.Committed);
                }
            }
            UnityEngine.Debug.Log($"[full-mesh-interiors] {hull.Name}: eyeball pack written to " +
                                  (hull.Committed ? ImageDir : CacheDir("full-mesh-interiors")));
        }

        // ============================================================================ machinery

        /// <summary>
        /// <b>The level tag whose EXTERIOR geometry stands highest on this boat — measured off the
        /// mesh, never named.</b>
        ///
        /// <para>This replaces a hard-coded <c>RiggingTag = 5</c>. Note first what 5 IS: a raw tag the
        /// bake writes into TexCoord1.x, appearing in NO hull's declared <c>LevelTags</c> — both
        /// converted hulls declare exactly house/cuddy/cockpit/foredeck (3/4/1/2), so nothing in the
        /// committed data says 5 means rigging, or that a given hull has one.</para>
        ///
        /// <para><b>MEASURED, both hulls:</b> the derivation returns 5 for each, so this is the same
        /// subject the transcribed constant had, not a behaviour change — but their rigging stands at
        /// very different heights (lobster top z 5.958 over a house at 3.055; cape 4.53 over 3.125),
        /// and the cape's is 72 verts against the lobster's 320. A hull whose top structure is tagged
        /// otherwise — or absent — would have made the transcribed 5 cull nothing and the guard pass
        /// by testing nothing, which is the failure its own <c>rigN &gt; 0</c> assertion catches one
        /// hull too late. Deriving it means the guard finds its own subject on every future hull.</para>
        ///
        /// <para>Height is the mesh's local <c>z</c>: <c>Vector3d.ToVector3()</c> maps
        /// (X, Y, Z) straight through, and the bake reports the same axis as "above the keel". Room
        /// vertices are excluded — the question is which structure stands ON the boat, and a cabin
        /// ceiling is inside her, not on top of her.</para>
        /// </summary>
        static int TopStructureTag(HullMeshDef hm, out string table)
        {
            var tags = new List<Vector2>();
            hm.Mesh.GetUVs(1, tags);
            Vector3[] v = hm.Mesh.vertices;

            var topZ = new Dictionary<int, float>();
            var count = new Dictionary<int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                if (tags[i].y > 0.5f) continue;                    // room faces are inside, not on top
                int tag = Mathf.RoundToInt(tags[i].x);
                if (tag <= 0) continue;                            // 0 is untagged hull structure
                if (!topZ.TryGetValue(tag, out float z) || v[i].z > z) topZ[tag] = v[i].z;
                count[tag] = count.TryGetValue(tag, out int c) ? c + 1 : 1;
            }

            var sb = new StringBuilder("  per-tag exterior height (mesh local z, m above the keel):");
            foreach (var kv in topZ.OrderByDescending(k => k.Value))
            {
                sb.AppendLine();
                sb.Append($"    tag {kv.Key,2}  top z {kv.Value,7:0.###}  "
                        + $"({count[kv.Key]} verts)");
            }
            table = sb.ToString();

            Assert.IsNotEmpty(topZ,
                "this hull's mesh carries no exterior vertex with a level tag above 0, so there is " +
                "no top structure to cull and the orientation guard would test nothing.");
            return topZ.OrderByDescending(k => k.Value).First().Key;
        }

        /// <summary>
        /// The same mesh with every room-flagged face removed — the control arm. One implementation,
        /// shared with the acceptance fixtures that photograph a converted hull's EXTERIOR (see
        /// <see cref="ConvertedInteriors.MeshWithoutTheRoom"/>): two copies of "which faces are the
        /// room" is the drift this arc factors out everywhere else.
        /// </summary>
        static Mesh MeshWithoutTheRoom(Mesh src, out int roomVerts, out int hullVerts)
            => ConvertedInteriors.MeshWithoutTheRoom(src, out roomVerts, out hullVerts);

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
        /// <summary>Where a NON-committed hull's pack and reports go: the temporary cache, logged.</summary>
        static string CacheDir(string leaf) => Path.Combine(Application.temporaryCachePath, leaf);

        static string SavePng(string name, byte[] rgba, HullMeshDef hm, bool committed)
        {
            string dir = committed ? Path.Combine(RepoRoot, ImageDir) : CacheDir("full-mesh-interiors");
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
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            return path;
        }

        static void WriteReport(string name, string text, bool committed)
        {
            string dir = committed ? Path.Combine(RepoRoot, "docs", "design", "spikes")
                                   : CacheDir("full-mesh-interiors-reports");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), text);
            UnityEngine.Debug.Log("[full-mesh-interiors] " + name + "\n" + text);
        }
    }
}
