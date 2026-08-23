using System;
using System.Collections.Generic;
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
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.Editor.Spikes;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>SPIKE FIXTURE (spike/interior-mesh) — questions B and C.</b> Not an acceptance test and
    /// not a guard: it MEASURES and it RENDERS, and the owner judges the pictures.
    ///
    /// <list type="bullet">
    /// <item><b>A</b> — writes the extrusion measurements
    /// (<see cref="BoatInteriorMeshExtrusionSpike"/>). Needs no GPU, so it runs on CI too.</item>
    /// <item><b>B</b> — proves ONE tag in TexCoord1.x serves both halves of ADR 0038's swap: the
    /// hull's own house faces carrying that level's id are culled, the room's faces carrying it are
    /// drawn, and no pixel shows both. ExactlyOneLayerOn, on a mesh hull, in pixels.</item>
    /// <item><b>C</b> — the look, three ways, at dock and at the crest of a Moderate sea, at the
    /// lobster's own per-tier zoom. Writes the PNGs the verdict points at.</item>
    /// </list>
    ///
    /// <para><b>Nothing here writes an asset.</b> The hull mesh is COPIED before it is tagged (a
    /// <c>Mesh.SetUVs</c> on the committed sub-asset would dirty the bake — the
    /// <c>sharedMaterial.SetFloat</c> trap in a different costume), the level gate is a global
    /// shader uniform reset in TearDown, and the only files written are the report and the PNGs
    /// under <c>docs/</c>.</para>
    /// </summary>
    public class BoatInteriorMeshSpikeRenderTests
    {
        const string ImageDir = "docs/art/spikes/interior-mesh";
        const string ReportDir = "docs/design/spikes";
        const string InteriorFolder = "Assets/_Project/Data/Boats/Interiors";
        const string VisualFolder = "Assets/_Project/Data/Boats/Visuals";
        const string Lobster = "LobsterBoatIso";
        const int ProbeLayer = 31;

        /// <summary>The shader keyword the spike's gate lives behind. OFF by default and enabled
        /// only on the renderer's OWN instance material (<c>new Material(...)</c> with
        /// <c>HideAndDontSave</c>, created per Configure and destroyed with the component), so no
        /// asset is touched — the <c>sharedMaterial.SetFloat</c> trap does not apply here, and this
        /// comment exists so nobody has to re-derive that.</summary>
        const string LevelGateKeyword = "HH_LEVEL_GATE";

        /// <summary>ADR 0038's accepted default for the comfort clamp. The interior draw takes this
        /// much of the hull's rock; all three ways in question C ride the same value, because the
        /// thing being judged is the REPRESENTATION and not the clamp.</summary>
        const float InteriorRockScale = 0.45f;

        /// <summary>Crest of the cycle: <c>HullMeshMath.RockPose</c> puts peak roll and peak heave
        /// at 90 degrees (sin), with the rig's own quarter-cycle pitch lead riding cos.</summary>
        const float CrestPhaseDegrees = 90f;

        /// <summary>The heading the panels are rendered at. Beam-on is the pose that shows the most
        /// of a wheelhouse's inside, and it is the cardinal the fleet's own acceptance floor calls
        /// the worst case (longest straight edges).</summary>
        const float PanelHeadingDegrees = 90f;

        GameConfig _cfg, _prevCfg;

        [SetUp]
        public void Setup()
        {
            _prevCfg = GameServices.Config;
            _cfg = ScriptableObject.CreateInstance<GameConfig>();
            _cfg.HullKeylineFlood = false;      // ADR 0031: the mesh fleet ships the keyline off
            GameServices.Config = _cfg;
            Shader.SetGlobalFloat("_HHLevelShown", 0f);
        }

        [TearDown]
        public void Teardown()
        {
            Shader.SetGlobalFloat("_HHLevelShown", 0f);
            GameServices.Config = _prevCfg;
            if (_cfg != null) Object.DestroyImmediate(_cfg);
            _cfg = null;
        }

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device (Renderer: Null Device). " +
                              "This spike's pictures need the local GPU; CI cannot produce them.");
        }

        static string RepoRoot => Directory.GetParent(Application.dataPath).FullName;

        // =============================================================== A: the measurements

        [Test]
        public void A_TheExtrusionMeasurements_AreWritten()
        {
            string report = BoatInteriorMeshExtrusionSpike.Measure();
            string dir = Path.Combine(RepoRoot, ReportDir);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "interior-mesh-measurements.txt");
            File.WriteAllText(path, report);
            Debug.Log("[interior-mesh spike] A -> " + path + "\n" + report);
            StringAssert.Contains("FLEET SHAPE", report);
        }

        // =============================================================== B: one tag, both halves

        [Test]
        public void B_OneTag_CullsTheExteriorHouseAndShowsTheRoom()
        {
            RequireAGraphicsDevice();

            BoatInteriorDef def; BoatVisualDef vis;
            LoadOrIgnore(Lobster, out def, out vis);
            HullMeshDef hm = vis.HullMesh;

            int levelIndex = HouseLevelIndex(def);
            Assert.Greater(levelIndex, 0, "the lobster publishes no house level to tag");
            string levelId = def.Levels[levelIndex - 1].Id;

            TaggedBuild build = BuildTagged(def, hm);
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            sb.AppendLine("B — ONE TAG, BOTH HALVES OF THE SWAP (spike/interior-mesh)");
            sb.AppendLine("hull: " + Lobster + "   level tagged: " + levelId + " (id " + levelIndex + ")");
            sb.AppendLine(string.Format(inv,
                "combined mesh: {0} verts / {1} tris  (hull {2}/{3} + rooms {4}/{5})",
                build.Mesh.vertexCount, build.Mesh.triangles.Length / 3,
                build.HullVerts, build.HullTris, build.RoomVerts, build.RoomTris));
            int tagsHere = build.TaggedFacesPerLevel.ContainsKey(levelIndex)
                           ? build.TaggedFacesPerLevel[levelIndex] : 0;
            sb.AppendLine(string.Format(inv,
                "hull faces carrying THIS level's tag: {0} of {1} tagged in all   " +
                "interior material id reused: {2}   fore bias {3:0.##} m (the hull's own bounding-sphere diameter)",
                tagsHere, build.HullFacesTagged, build.InteriorMaterialId, build.ForeBiasMeters));
            sb.AppendLine();

            try
            {
                Mesh houseMesh = build.HullFacesOf.ContainsKey(levelIndex) ? build.HullFacesOf[levelIndex] : null;
                Mesh roomMesh = build.RoomOf[levelIndex];
                Mesh roomFlat = build.RoomFlatOf[levelIndex];
                Assert.IsNotNull(houseMesh, "no hull faces carry this level's tag");

                byte[] shipped = RenderTagged(hm, build.HullOnly, 0f, Pose.Dock(), false);
                byte[] inside = RenderTagged(hm, build.Mesh, levelIndex, Pose.Dock());
                byte[] houseOnly = RenderTagged(hm, houseMesh, 0f, Pose.Dock());
                byte[] houseAtGate = RenderTagged(hm, houseMesh, levelIndex, Pose.Dock());
                byte[] roomOnly = RenderTagged(hm, roomMesh, levelIndex, Pose.Dock());

                bool[] mShipped = Mask(shipped), mInside = Mask(inside);
                bool[] mHouse = Mask(houseOnly), mRoom = Mask(roomOnly);

                int houseLeak = Count(Mask(houseAtGate));
                if (houseLeak > 0) SavePng("B-02-house-residue-at-the-gate.png", houseAtGate, hm);

                int house = Count(mHouse), room = Count(mRoom);
                int roomInShipped = CountAnd(mRoom, mShipped, mHouse);
                int roomOutsideHouse = CountAndNot(mRoom, null, mHouse);
                int bothAtOnce = 0;
                for (int i = 0; i < mHouse.Length; i++) if (mHouse[i] && mRoom[i]) bothAtOnce++;

                sb.AppendLine("pixel accounting at the dock pose, " + PanelHeadingDegrees + " deg:");
                sb.AppendLine("   exterior house of the level, drawn alone : " + house + " px");
                sb.AppendLine("   the room, drawn alone                    : " + room + " px");
                sb.AppendLine("   the tagged house faces, DRAWN AT THE GATE: " + houseLeak +
                              " px   (must be 0 — this is the cull, asked exactly)");
                sb.AppendLine("   hull revealed behind it while inside     : " +
                              CountAndNot(mHouse, mInside, mRoom) +
                              " px  (the deck and far rail the house used to hide; NOT a leak)");
                sb.AppendLine("   room pixels outside the house silhouette : " + roomOutsideHouse +
                              " px  (containment, in pixels)");
                sb.AppendLine("   room pixels the SHIPPED frame covers     : " + roomInShipped +
                              " px  (overlap of the two masks: " + bothAtOnce + ")");

                int survFore = SurvivingPixels(inside, roomOnly, mRoom);
                sb.AppendLine();
                sb.AppendLine("   room pixels SURVIVING into the combined frame, WITH the fore bias : " +
                              survFore + " of " + room + "  (" + Pct(survFore, room) + ")");

                // The A/B: the same room with db = 0, i.e. relying on the swap alone.
                Mesh combinedFlat = build.HullPlusFlatRoomOf[levelIndex];
                {
                    byte[] insideFlat = RenderTagged(hm, combinedFlat, levelIndex, Pose.Dock());
                    byte[] roomFlatOnly = RenderTagged(hm, roomFlat, levelIndex, Pose.Dock());
                    bool[] mRoomFlat = Mask(roomFlatOnly);
                    int survFlat = SurvivingPixels(insideFlat, roomFlatOnly, mRoomFlat);
                    sb.AppendLine("   room pixels SURVIVING with db = 0, the SWAP ALONE               : " +
                                  survFlat + " of " + Count(mRoomFlat) + "  (" +
                                  Pct(survFlat, Count(mRoomFlat)) + ")");
                    sb.AppendLine();
                    sb.AppendLine("   ⇒ the swap turns the CABIN off; it does not turn off the hull's own near");
                    sb.AppendLine("     topsides, which in a 3/4 view stand between the camera and a cabin sole.");
                    sb.AppendLine("     The sprite path never met this because a sheet composites OVER the hull.");
                    sb.AppendLine("     The mesh path already owns the same lever: UV0.z, the rig's per-face db.");
                    SavePng("B-03-swap-alone-no-fore-bias.png", insideFlat, hm);
                }

                // ---- THE VARIANT BOUNDARY, which is the claim the coordinator asked to be made
                // honest. The gate lives behind #pragma shader_feature_local HH_LEVEL_GATE, so with
                // the keyword off the compiled program is literally the pre-spike one — no
                // TEXCOORD1 input, no varying, no discard, nothing per fragment. Two renders prove
                // it costs nothing AND hides nothing:
                //   1. the same hull through the SHIPPED variant and through the gated variant at 0
                //   2. the hull WITH the rooms attached, gated at 0, against the shipped variant
                byte[] shippedProgram = RenderTagged(hm, build.HullOnly, 0f, Pose.Dock(), false);
                byte[] gatedAtZero = RenderTagged(hm, build.HullOnly, 0f, Pose.Dock(), true);
                byte[] roomsGatedOff = RenderTagged(hm, build.Mesh, 0f, Pose.Dock(), true);
                int diffVariant = DifferingPixels(shippedProgram, gatedAtZero);
                int diffRooms = DifferingPixels(shippedProgram, roomsGatedOff);
                sb.AppendLine();
                sb.AppendLine("THE VARIANT BOUNDARY (#pragma shader_feature_local HH_LEVEL_GATE, off by default):");
                sb.AppendLine("   shipped program vs the gated variant at 0, same hull : " + diffVariant +
                              " differing px  (must be 0 — compiling the gate in changes no pixel)");
                sb.AppendLine("   shipped program vs hull+ROOMS through the gate at 0  : " + diffRooms +
                              " differing px  (must be 0 — the gate hides the interior geometry)");
                sb.AppendLine("   With the keyword OFF the program has no TEXCOORD1 input, no extra varying");
                sb.AppendLine("   and no discard: it is the pre-spike program, not the same picture for a");
                sb.AppendLine("   per-fragment test on every hull every frame.");
                int diff = diffVariant + diffRooms;
                sb.AppendLine();
                sb.AppendLine("EXACTLY-ONE-LAYER-ON: " + (houseLeak == 0 && room > 0 ? "TRUE" : "FALSE") +
                              "  — gate off: the house draws, the room does not (identity 0 px). " +
                              "Gate at this level: the room draws and the level's own hull faces draw " +
                              houseLeak + " px. One float on TexCoord1.x, one compare in the fragment, " +
                              "no sorting rule added.");

                WriteReport("interior-mesh-B-tag-proof.txt", sb.ToString());
                SavePng("B-00-shipped-gate-off.png", shipped, hm);
                SavePng("B-01-inside-the-house.png", inside, hm);

                Assert.AreEqual(0, houseLeak, "the level's own hull faces still draw while the gate shows the room");
                Assert.Greater(room, 0, "the room drew nothing — the tag or the extrusion is wrong");
                Assert.AreEqual(0, diffVariant, "compiling the gate in changed the hull's own picture");
                Assert.AreEqual(0, diffRooms, "the interior geometry is visible with the gate at 0");
            }
            finally
            {
                build.Dispose();
            }
        }

        static string Pct(int a, int b)
        {
            return b > 0 ? (100.0 * a / b).ToString("0.0", CultureInfo.InvariantCulture) + "%" : "n/a";
        }

        /// <summary>Room pixels whose colour in the combined frame is the room's own — i.e. the room
        /// won the depth test there. Same mesh, same shader, same pose, so the comparison is exact.</summary>
        static int SurvivingPixels(byte[] combined, byte[] roomAlone, bool[] roomMask)
        {
            int n = 0;
            for (int i = 0; i < roomMask.Length; i++)
            {
                if (!roomMask[i]) continue;
                int o = i * 4;
                if (combined[o] == roomAlone[o] && combined[o + 1] == roomAlone[o + 1] &&
                    combined[o + 2] == roomAlone[o + 2]) n++;
            }
            return n;
        }

        // =============================================================== C: the look

        [Test]
        public void C_TheLook_ThreeWays_AtDockAndAtTheCrest()
        {
            RequireAGraphicsDevice();

            BoatInteriorDef def; BoatVisualDef vis;
            LoadOrIgnore(Lobster, out def, out vis);
            HullMeshDef hm = vis.HullMesh;

            // The lobster's CUDDY is the room the handoff names.
            int cuddy = LevelIndexNamed(def, "cuddy");
            if (cuddy <= 0) cuddy = HouseLevelIndex(def);
            Assert.Greater(cuddy, 0, "no level to render");
            string levelId = def.Levels[cuddy - 1].Id;

            var cells = Resources.Load<BoatInteriorCellsDef>(
                BoatInteriorCellsDef.ResourcesFolder + "/" +
                BoatInteriorCellsDef.FileNameFor(def.Id));

            // ONE facing index, shared by the committed sheet and the rig render, so the two
            // ways cannot be showing the boat from different sides. Taken through the SHEET's own
            // committed convention flag rather than a declared one.
            int facingCount = cells != null ? Mathf.Max(1, cells.Facings) : 8;
            bool ccw = cells == null || cells.CellsAreCounterClockwise;
            float turn = ccw ? -PanelHeadingDegrees : PanelHeadingDegrees;
            int facingIndex = ((Mathf.RoundToInt(turn / (360f / facingCount)) % facingCount) + facingCount)
                              % facingCount;

            TaggedBuild build = BuildTagged(def, hm);
            var log = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            log.AppendLine("C — THE LOOK, THREE WAYS (spike/interior-mesh)");
            log.AppendLine("hull " + Lobster + ", level " + levelId + ", heading " +
                           PanelHeadingDegrees.ToString(inv) + " deg, sheet facing index " + facingIndex +
                           " of " + facingCount + " (convention " + (ccw ? "CCW" : "CW") + ", as committed)");
            log.AppendLine("zoom: the lobster's own per-tier framing, BoatHullDef.CameraWorldHeightMeters = 23 m; " +
                           "the cell renders at her bake's " + hm.PxPerMetre + " px/m, which is the on-screen " +
                           "scale at that tier (PixelPerfect zoom 1 at 1080p).");
            log.AppendLine("InteriorRockScale " + InteriorRockScale.ToString(inv) + " on ALL THREE ways.");

            try
            {
                Pose dock = Pose.Dock();
                Pose crest = Pose.ModerateCrest(hm, log);

                byte[][] panels = new byte[6][];
                string[] names = { "dock", "crest" };
                Pose[] poses = { dock, crest };

                for (int p = 0; p < 2; p++)
                {
                    Pose pose = poses[p];

                    // (1) today's sprite: the hull as she is drawn now, with the committed interior
                    //     cell composited over her at the shared pivot, both riding the same pose.
                    byte[] hull = RenderTagged(hm, build.HullOnly, 0f, pose, false);   // today, exactly
                    byte[] sheet = cells != null
                        ? RenderInteriorSprite(hm, cells, def, levelId, facingIndex, pose)
                        : null;
                    panels[p * 3 + 0] = sheet != null ? Over(sheet, hull) : hull;

                    // (2) mesh shell, no fit-out: the same hull with the level gate on.
                    panels[p * 3 + 1] = RenderTagged(hm, build.Mesh, cuddy, pose);

                    // (3) mesh shell + the fit-out pixels over it.
                    byte[] fitout = TryRenderFitoutLayers(def, levelId, hm, facingIndex, pose, log);
                    panels[p * 3 + 2] = fitout != null
                        ? Over(fitout, panels[p * 3 + 1])
                        : Over(sheet, panels[p * 3 + 1]);
                    if (fitout == null)
                        log.AppendLine("   [3] FELL BACK to the whole committed interior cell — the rig's " +
                                       "separate fit-out LAYERS could not be rendered here, so the sole and " +
                                       "shell pixels double up over the mesh. Say so in the verdict.");
                }

                SavePng("interior-mesh-01-sprite-today.png", Strip(hm, panels[0], panels[3]), hm.CellW * 2, hm.CellH);
                SavePng("interior-mesh-02-mesh-shell.png", Strip(hm, panels[1], panels[4]), hm.CellW * 2, hm.CellH);
                SavePng("interior-mesh-03-mesh-plus-fitout.png", Strip(hm, panels[2], panels[5]), hm.CellW * 2, hm.CellH);
                SavePng("interior-mesh-00-side-by-side.png",
                        Grid(hm, panels), hm.CellW * 3, hm.CellH * 2);

                // ---- SUPPLEMENTARY, and labelled as such. Everything above is derived from what
                // the kit publishes; the cuddy's ceiling is not published and the measured stand-in
                // is her WHEELHOUSE ROOF, which is why her room stands proud of the foredeck in
                // panel 2. This one picture supplies that single number by hand so the owner can
                // judge the MECHANISM rather than the missing datum. It is not a measurement and
                // nothing else in this spike uses it.
                const float HandSetCuddyHeadroom = 0.95f;
                var handSet = new Dictionary<int, float> { { cuddy, HandSetCuddyHeadroom } };
                TaggedBuild supplied = BuildTagged(def, hm, handSet);
                try
                {
                    byte[] a = RenderTagged(hm, supplied.Mesh, cuddy, dock);
                    byte[] b = RenderTagged(hm, supplied.Mesh, cuddy, crest);
                    SavePng("interior-mesh-04-ceiling-supplied.png", Strip(hm, a, b),
                            hm.CellW * 2, hm.CellH);
                    log.AppendLine();
                    log.AppendLine("SUPPLEMENTARY 04: the same mesh shell with the cuddy's headroom HAND-SET to " +
                                   HandSetCuddyHeadroom.ToString(inv) + " m. Not measured, not derived — it is " +
                                   "the number the rig does not publish, supplied so the owner judges the " +
                                   "mechanism and not the gap. Compare against 02.");
                }
                finally { supplied.Dispose(); }

                log.AppendLine();
                log.AppendLine("PNGs: 01 sprite (today) | 02 mesh shell, no fit-out | 03 mesh + fit-out.");
                log.AppendLine("Each strip is DOCK on the left, MODERATE CREST on the right.");
                log.AppendLine("00-side-by-side is the contact sheet: columns 1/2/3, row 1 dock, row 2 crest.");
                WriteReport("interior-mesh-C-renders.txt", log.ToString());
                Debug.Log("[interior-mesh spike] C\n" + log);
            }
            finally
            {
                build.Dispose();
            }
        }

        // =============================================================== the tagged mesh

        sealed class TaggedBuild : IDisposable
        {
            /// <summary>Everything: hull (tagged) + every room, the rooms carrying the fore bias.</summary>
            public Mesh Mesh;
            /// <summary>The hull alone, tags intact — the shipped picture's control.</summary>
            public Mesh HullOnly;
            /// <summary>Per level (1-based index): just that level's room / just the hull faces that
            /// carry that level's tag. ⚠ The first version of this fixture pooled EVERY tagged level
            /// into one "house" mesh and then asked whether the gate for level 1 emptied it. It could
            /// not: level 2's faces are correctly kept when you are on level 1, and 477 px of cuddy
            /// read as a broken cull for a whole cycle.</summary>
            public readonly Dictionary<int, Mesh> RoomOf = new Dictionary<int, Mesh>();
            public readonly Dictionary<int, Mesh> HullFacesOf = new Dictionary<int, Mesh>();
            /// <summary>The same rooms with the fore bias set to zero — the A/B for whether the room
            /// needs to be pulled in front of the hull's own near topsides.</summary>
            public readonly Dictionary<int, Mesh> RoomFlatOf = new Dictionary<int, Mesh>();
            /// <summary>Hull + just that level's UNBIASED room, for the db A/B.</summary>
            public readonly Dictionary<int, Mesh> HullPlusFlatRoomOf = new Dictionary<int, Mesh>();

            public int HullVerts, HullTris, RoomVerts, RoomTris, HullFacesTagged, InteriorMaterialId;
            public float ForeBiasMeters;
            public readonly Dictionary<int, int> TaggedFacesPerLevel = new Dictionary<int, int>();

            public void Dispose()
            {
                var all = new List<Mesh> { Mesh, HullOnly };
                all.AddRange(RoomOf.Values); all.AddRange(HullFacesOf.Values);
                all.AddRange(RoomFlatOf.Values); all.AddRange(HullPlusFlatRoomOf.Values);
                foreach (Mesh m in all) if (m != null) Object.DestroyImmediate(m);
            }
        }

        /// <summary>
        /// Copy the hull mesh, tag its own faces with the level they belong to, and append the
        /// extruded rooms. The committed mesh is never touched.
        /// </summary>
        static TaggedBuild BuildTagged(BoatInteriorDef def, HullMeshDef hm)
        {
            return BuildTagged(def, hm, null);
        }

        /// <summary>
        /// <paramref name="ceilingOverride"/> maps a 1-based level index to a HAND-SET headroom in
        /// metres. It exists for exactly one picture: what the same mechanism produces once the one
        /// number the rig does not publish is supplied. Never used for a measurement.
        /// </summary>
        static TaggedBuild BuildTagged(BoatInteriorDef def, HullMeshDef hm,
                                       Dictionary<int, float> ceilingOverride)
        {
            Mesh src = hm.Mesh;
            Vector3[] hv = src.vertices;
            Vector3[] hn = src.normals;
            var hu = new List<Vector4>();
            src.GetUVs(0, hu);
            int[] ht = src.triangles;

            List<BoatInteriorExtruder.HullFace> faces = BoatInteriorExtruder.FacesOf(hv, ht);
            var levels = new List<BoatInteriorLevel>();
            var ceils = new List<float>();
            foreach (BoatInteriorLevel l in def.Levels)
            {
                if (l == null || !l.IsUsable()) continue;
                BoatInteriorExtruder.CeilingSource s;
                float z = BoatInteriorExtruder.MeasureCeiling(def, l, faces, def.Door, out s);
                float hand;
                if (ceilingOverride != null && ceilingOverride.TryGetValue(levels.Count + 1, out hand))
                    z = l.SoleZMeters + hand;
                ceils.Add(z);
                levels.Add(l);
            }

            var verts = new List<Vector3>(hv.Length);
            var norms = new List<Vector3>(hv.Length);
            var uv0 = new List<Vector4>(hv.Length);
            var uv1 = new List<Vector2>(hv.Length);
            var tris = new List<int>(ht.Length);
            var hullTris = new List<int>(ht.Length);
            var perLevelHull = new Dictionary<int, List<int>>();

            int tagged = 0;
            for (int t = 0; t + 2 < ht.Length; t += 3)
            {
                int a = ht[t], b = ht[t + 1], c = ht[t + 2];
                // The SHARED rule (BoatInteriorExtruder.TagFace) — never a second copy of it here.
                int lvl;
                int tag = BoatInteriorExtruder.TagFace(faces[t / 3], levels, out lvl) ==
                          BoatInteriorExtruder.FaceTag.Level ? lvl + 1 : 0;
                if (tag != 0) tagged++;

                int baseIndex = verts.Count;
                foreach (int k in new[] { a, b, c })
                {
                    verts.Add(hv[k]);
                    norms.Add(hn != null && hn.Length > k ? hn[k] : Vector3.forward);
                    uv0.Add(hu.Count > k ? hu[k] : Vector4.zero);
                    uv1.Add(new Vector2(tag, 0f));
                }
                tris.Add(baseIndex); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
                hullTris.Add(baseIndex); hullTris.Add(baseIndex + 1); hullTris.Add(baseIndex + 2);
                if (tag != 0)
                {
                    List<int> bucket;
                    if (!perLevelHull.TryGetValue(tag, out bucket))
                    { bucket = new List<int>(); perLevelHull[tag] = bucket; }
                    bucket.Add(baseIndex); bucket.Add(baseIndex + 1); bucket.Add(baseIndex + 2);
                }
            }

            var build = new TaggedBuild
            {
                HullVerts = verts.Count,
                HullTris = tris.Count / 3,
                HullFacesTagged = tagged,
            };

            // The interior faces borrow the hull's own material — the one whose faces sit nearest
            // the sole. The spike is measuring geometry and the swap, not inventing a palette.
            build.InteriorMaterialId = NearestSoleMaterial(hv, ht, hu, levels);

            // ⭐ THE FORE BIAS, and it is not a new mechanism. UV0.z is the rig's own per-face `db`
            // — "pull this face toward the camera" — which the facet pass already subtracts from the
            // clip-space depth while leaving the TRUE depth (o.wpos.z, the occupant band and the
            // keyline resolve) untouched. Setting it on the room reproduces, in the depth test, what
            // the sprite path gets for free by compositing the interior cell OVER the hull. The
            // value is the hull's own bounding-sphere diameter, so no fragment of her can be nearer.
            build.ForeBiasMeters = 2f * hm.Mesh.bounds.extents.magnitude;

            var roomTrisAll = new List<int>();
            var perLevelRoom = new Dictionary<int, List<int>>();
            var perLevelRoomFlat = new Dictionary<int, List<int>>();
            int roomStart = verts.Count;
            for (int i = 0; i < levels.Count; i++)
            {
                BoatInteriorExtruder.LevelMesh lm =
                    BoatInteriorExtruder.Extrude(levels[i], def.Door, ceils[i],
                                                 BoatInteriorExtruder.CeilingSource.LevelAbove, false);
                var mine = new List<int>();
                var mineFlat = new List<int>();
                for (int f = 0; f * 3 + 2 < lm.Tris.Count; f++)
                {
                    Vector3 n = lm.Normals[f];
                    int baseIndex = verts.Count;
                    int flatIndex = -1;
                    for (int pass = 0; pass < 2; pass++)
                    {
                        int bi = verts.Count;
                        if (pass == 1) flatIndex = bi;
                        for (int k = 0; k < 3; k++)
                        {
                            verts.Add(lm.Verts[lm.Tris[f * 3 + k]]);
                            norms.Add(n);
                            // UV0: material, face bias 0, DEPTH BIAS, interior side code 1 (the sea
                            // must never reach a cabin sole) — the same channel layout
                            // RigMeshBuilder writes, so the facet pass needs no new input.
                            uv0.Add(new Vector4(build.InteriorMaterialId, 0f,
                                                pass == 0 ? build.ForeBiasMeters : 0f, 1f));
                            uv1.Add(new Vector2(i + 1, 1f));
                        }
                        if (pass == 0)
                        {
                            tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);
                            roomTrisAll.Add(bi); roomTrisAll.Add(bi + 1); roomTrisAll.Add(bi + 2);
                            mine.Add(bi); mine.Add(bi + 1); mine.Add(bi + 2);
                        }
                        else
                        {
                            mineFlat.Add(bi); mineFlat.Add(bi + 1); mineFlat.Add(bi + 2);
                        }
                    }
                    if (flatIndex < 0) { }   // silence "assigned but never used" on some compilers
                }
                perLevelRoom[i + 1] = mine;
                perLevelRoomFlat[i + 1] = mineFlat;
            }
            build.RoomVerts = verts.Count - roomStart;
            build.RoomTris = roomTrisAll.Count / 3;

            build.Mesh = Assemble("hh_spike_combined", verts, norms, uv0, uv1, tris);
            build.HullOnly = Assemble("hh_spike_hull", verts, norms, uv0, uv1, hullTris);
            foreach (var kv in perLevelRoom)
                build.RoomOf[kv.Key] = Assemble("hh_spike_room" + kv.Key, verts, norms, uv0, uv1, kv.Value);
            foreach (var kv in perLevelRoomFlat)
            {
                build.RoomFlatOf[kv.Key] = Assemble("hh_spike_roomflat" + kv.Key, verts, norms, uv0, uv1, kv.Value);
                var both = new List<int>(hullTris);
                both.AddRange(kv.Value);
                build.HullPlusFlatRoomOf[kv.Key] =
                    Assemble("hh_spike_hullflat" + kv.Key, verts, norms, uv0, uv1, both);
            }
            foreach (var kv in perLevelHull)
            {
                build.HullFacesOf[kv.Key] = Assemble("hh_spike_hullfaces" + kv.Key, verts, norms, uv0, uv1, kv.Value);
                build.TaggedFacesPerLevel[kv.Key] = kv.Value.Count / 3;
            }
            return build;
        }

        static Mesh Assemble(string name, List<Vector3> v, List<Vector3> n,
                             List<Vector4> uv0, List<Vector2> uv1, List<int> tris)
        {
            var m = new Mesh { name = name };
            m.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv0);
            m.SetUVs(1, uv1);
            m.SetTriangles(tris, 0, true);
            return m;
        }

        static int NearestSoleMaterial(Vector3[] hv, int[] ht, List<Vector4> hu,
                                       List<BoatInteriorLevel> levels)
        {
            if (levels.Count == 0 || hu.Count == 0) return 0;
            BoatInteriorLevel l = levels[0];
            var cen = Vector2.zero;
            foreach (Vector2 q in l.Outline) cen += q;
            cen /= Mathf.Max(1, l.Outline.Length);
            var target = new Vector3(cen.x, cen.y, l.SoleZMeters);
            float best = float.MaxValue;
            int mat = 0;
            for (int t = 0; t + 2 < ht.Length; t += 3)
            {
                Vector3 c = (hv[ht[t]] + hv[ht[t + 1]] + hv[ht[t + 2]]) / 3f;
                float d = (c - target).sqrMagnitude;
                if (d < best) { best = d; mat = Mathf.RoundToInt(hu[ht[t]].x); }
            }
            return mat;
        }

        // =============================================================== poses

        struct Pose
        {
            public float Roll, Pitch, HeavePx;
            public static Pose Dock() { return new Pose(); }

            /// <summary>The crest of a Moderate sea, composed through the SHIPPED path: the storm
            /// channel's response multiplier at sea state Moderate scales the def's own canned
            /// amplitudes, <c>HullMeshMath.RockPose</c> puts them at crest phase, and ADR 0038's
            /// comfort clamp takes its share for the interior draw.</summary>
            public static Pose ModerateCrest(HullMeshDef hm, StringBuilder log)
            {
                float sea01 = (float)(int)SeaState.Moderate / 7f;
                var settings = StormRockSettings.Default;
                float blend = StormRockMath.StormBlend01(sea01, settings);
                float mult = StormRockMath.ResponseMultiplier(blend, settings);
                float roll, pitch, heave;
                HullMeshMath.RockPose(CrestPhaseDegrees,
                                      hm.RockRollDegrees * mult,
                                      hm.RockPitchDegrees * mult,
                                      hm.RockHeavePixels * mult,
                                      out roll, out pitch, out heave);
                if (log != null)
                {
                    var inv = CultureInfo.InvariantCulture;
                    log.AppendLine(string.Format(inv,
                        "Moderate sea: seaState01 {0:0.####} -> storm blend {1:0.#####} -> amplitude x{2:0.####}; " +
                        "def ROCK ({3:0.##} deg roll, {4:0.##} deg pitch, {5:0.##} px heave) at crest gives " +
                        "roll {6:0.###} deg, pitch {7:0.###} deg, heave {8:0.###} px; " +
                        "the interior draw takes {9:0.##} of it.",
                        sea01, blend, mult, hm.RockRollDegrees, hm.RockPitchDegrees, hm.RockHeavePixels,
                        roll, pitch, heave, InteriorRockScale));
                }
                return new Pose { Roll = roll, Pitch = pitch, HeavePx = heave };
            }

            public Pose Scaled(float k)
            {
                return new Pose { Roll = Roll * k, Pitch = Pitch * k, HeavePx = HeavePx * k };
            }
        }

        // =============================================================== rendering

        static byte[] RenderTagged(HullMeshDef def, Mesh mesh, float levelShown, Pose pose)
        {
            // A render that shows a level obviously needs the gate compiled in; one that does not
            // runs the SHIPPED program, which is the point of the keyword.
            return RenderTagged(def, mesh, levelShown, pose, levelShown > 0.5f);
        }

        /// <summary>
        /// <paramref name="gateCompiledIn"/> selects the shader VARIANT, not the gate's value:
        /// false is the program main ships (no TEXCOORD1 input, no varying, no discard), true is
        /// the spike's. Both are exercised, because "the shipped picture is unchanged" is a claim
        /// about the variant boundary and can only be proven by rendering across it.
        /// </summary>
        static byte[] RenderTagged(HullMeshDef def, Mesh mesh, float levelShown, Pose pose,
                                   bool gateCompiledIn)
        {
            var go = new GameObject("SpikeHull") { layer = ProbeLayer };
            try
            {
                var r = go.AddComponent<IsoFacetHullRenderer>();
                IsoFacetHullSetup setup = IsoFacetHullPresentationService.ToSetup(def);
                setup.Mesh = mesh;
                r.Configure(setup);
                SetLevelGate(go, gateCompiledIn);
                r.HeadingDirUnits = HullMeshMath.HeadingToDirUnits(PanelHeadingDegrees, 0f,
                                                                   def.AzimuthCounterClockwise);
                Pose p = pose.Scaled(InteriorRockScale);
                r.RollDegrees = p.Roll;
                r.PitchDegrees = p.Pitch;
                r.HeavePixels = p.HeavePx;
                r.ApplyPose();
                SetLayerRecursive(go.transform, ProbeLayer);
                Shader.SetGlobalFloat("_HHLevelShown", levelShown);
                return RenderCell(def, def.CellW, def.CellH);
            }
            finally
            {
                Shader.SetGlobalFloat("_HHLevelShown", 0f);
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>The committed interior CELL for this level, at the shared pivot, riding the
        /// same clamped pose the mesh does. This is today's picture.</summary>
        static byte[] RenderInteriorSprite(HullMeshDef def, BoatInteriorCellsDef cells,
                                           BoatInteriorDef idef, string levelId, int facingIndex,
                                           Pose pose)
        {
            int row = 0;
            for (int i = 0; i < cells.CellLevels.Length; i++)
                if (string.Equals(cells.CellLevels[i], levelId, StringComparison.Ordinal))
                { row = i < cells.CellRowForLevel.Length ? cells.CellRowForLevel[i] : i; break; }

            int facings = Mathf.Max(1, cells.Facings);
            int index = row * facings + facingIndex;
            if (cells.Cells == null || index < 0 || index >= cells.Cells.Length) return null;
            Sprite cell = cells.Cells[index];
            if (cell == null) return null;

            var go = new GameObject("SpikeSheet") { layer = ProbeLayer };
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = cell;
                var unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                sr.sharedMaterial = new Material(unlit);
                Pose p = pose.Scaled(InteriorRockScale);
                go.transform.rotation = Quaternion.Euler(0f, 0f, -p.Roll);
                go.transform.position = new Vector3(0f, p.HeavePx / Mathf.Max(1, idef.PixelsPerMetre), 0f);
                SetLayerRecursive(go.transform, ProbeLayer);
                return RenderCell(def, def.CellW, def.CellH);
            }
            finally
            {
                Material m = go.GetComponent<SpriteRenderer>().sharedMaterial;
                Object.DestroyImmediate(go);
                if (m != null) Object.DestroyImmediate(m);
            }
        }

        /// <summary>
        /// The rig's own FIT-OUT layers alone (<c>layers:['fitout','props','interact']</c>), run
        /// through the interior rig in the editor's V8 host — the sole and the shell left out, so
        /// what lands on the mesh is the furniture and nothing else. Returns null (and says why in
        /// the log) if the rig cannot be reached; the caller falls back and reports the fallback.
        /// </summary>
        static byte[] TryRenderFitoutLayers(BoatInteriorDef def, string levelId, HullMeshDef hm,
                                            int facingIndex, Pose pose, StringBuilder log)
        {
            try
            {
                using (IRigScriptHost host = RigScriptHostFactory.Create())
                {
                    IReadOnlyList<BoatInteriorRigHost.HullBinding> table =
                        BoatInteriorRigHost.ReadHullTable(host);
                    string stem = Path.GetFileNameWithoutExtension(def.SourceHullRig);
                    BoatInteriorRigHost.HullBinding binding;
                    string bindError;
                    if (!BoatInteriorRigHost.TryBind(table, stem, out binding, out bindError))
                    {
                        log.AppendLine("   [3] rig bind FAILED for stem '" + stem + "': " + bindError);
                        return null;
                    }
                    BoatInteriorRigHost.Install(host, in binding);

                    // ⚠ THE DEF AND THE RIG NAME THE SAME LEVEL DIFFERENTLY. The def carries the
                    // SIDECAR's WALKABLE keys ("cuddy_sole", "house_sole"); the rig's own levelsOf()
                    // returns "cuddy", "house". Handing the rig the def's spelling does not error —
                    // it renders SOME level, and the first pass composited the wheelhouse fit-out
                    // into the cuddy without a word. Matched by prefix, and refused if no rig level
                    // matches at all.
                    string[] rigLevels = BoatInteriorRigHost.LevelsOf(host, binding.Key);
                    string rigLevel = null;
                    foreach (string rl in rigLevels)
                        if (levelId.StartsWith(rl, StringComparison.Ordinal) ||
                            levelId.IndexOf(rl, StringComparison.Ordinal) >= 0) { rigLevel = rl; break; }
                    if (rigLevel == null)
                    {
                        log.AppendLine("   [3] no rig level matches def level '" + levelId + "' (rig has: " +
                                       string.Join(", ", rigLevels) + ")");
                        return null;
                    }
                    if (!string.Equals(rigLevel, levelId, StringComparison.Ordinal))
                        log.AppendLine("   [3] def level '" + levelId + "' -> rig level '" + rigLevel + "'");

                    // The rig's own pose channel, clamped by ADR 0038's comfort scale — the same
                    // numbers the mesh under it is posed with, so the two cannot drift apart.
                    Pose p = pose.Scaled(InteriorRockScale);
                    string opts = BoatInteriorRigHost.OptsLiteral(binding.Key, rigLevel, "")
                                    .Replace("layers:null", "layers:['fitout','props','interact']");
                    opts = Splice(opts, p.Roll, p.Pitch, p.HeavePx);
                    string expr = BoatInteriorRigHost.Global + ".render(" + facingIndex + "," + opts + ")";
                    byte[] rgba = host.EvaluateBytes(expr);
                    int want = hm.CellW * hm.CellH * 4;
                    if (rgba == null || rgba.Length != want)
                    {
                        log.AppendLine("   [3] rig returned " +
                                       (rgba == null ? "null" : rgba.Length + " bytes") +
                                       ", expected " + want + " (cell " + hm.CellW + "x" + hm.CellH + ")");
                        return null;
                    }
                    log.AppendLine("   [3] fit-out rendered by the rig itself: " +
                                   "layers ['fitout','props','interact'], dir " + facingIndex +
                                   ", roll " + p.Roll.ToString("0.###", CultureInfo.InvariantCulture) +
                                   " deg, heave " + p.HeavePx.ToString("0.###", CultureInfo.InvariantCulture) + " px");
                    return rgba;
                }
            }
            catch (Exception e)
            {
                log.AppendLine("   [3] rig render threw: " + e.GetType().Name + " " + e.Message);
                return null;
            }
        }

        /// <summary>Splice roll/pitch/heave into a rig options literal, as the overdraw spike does:
        /// every rig reads them off <c>opts</c> through one shared <c>camBasis</c>, which is the only
        /// reason a rig render and a mesh render can be compared at all.</summary>
        static string Splice(string opts, double roll, double pitch, double heave)
        {
            string body = opts.Trim();
            body = body.Length <= 2 ? "" : body.Substring(1, body.Length - 2) + ",";
            var inv = CultureInfo.InvariantCulture;
            return "{" + body +
                   "roll:" + roll.ToString("R", inv) + "," +
                   "pitch:" + pitch.ToString("R", inv) + "," +
                   "heave:" + heave.ToString("R", inv) + "}";
        }

        static byte[] RenderCell(HullMeshDef def, int w, int h)
        {
            float ppu = def.PxPerMetre;
            float ox = (def.PivotPx.x - def.CellW / 2f) / ppu;
            float oy = (def.CellH / 2f - def.PivotPx.y) / ppu;

            var camGo = new GameObject("SpikeCam");
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
                Object.DestroyImmediate(camGo);
                rt.Release();
                Object.DestroyImmediate(rt);
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
            Object.DestroyImmediate(tex);

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

        /// <summary>Enable or disable the gate keyword on the facet material this renderer just
        /// created for itself. Asserts it found one: a silently-missed material would render the
        /// SHIPPED variant and quietly turn every gate measurement into a no-op.</summary>
        static void SetLevelGate(GameObject go, bool on)
        {
            int touched = 0;
            foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material m = mr.sharedMaterial;
                if (m == null || m.shader == null) continue;
                if (!m.shader.name.EndsWith("IsoFacet", StringComparison.Ordinal)) continue;
                Assert.AreNotEqual(HideFlags.None, m.hideFlags & HideFlags.DontSave,
                    "the facet material is not an instance — refusing to write a keyword onto an asset");
                if (on) m.EnableKeyword(LevelGateKeyword); else m.DisableKeyword(LevelGateKeyword);
                touched++;
            }
            Assert.Greater(touched, 0, "found no IsoFacet material to set " + LevelGateKeyword + " on");
        }

        static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
        }

        // =============================================================== pixels

        static bool[] Mask(byte[] rgba)
        {
            var m = new bool[rgba.Length / 4];
            for (int i = 0; i < m.Length; i++) m[i] = rgba[i * 4 + 3] > 8;
            return m;
        }

        static int Count(bool[] m) { int n = 0; foreach (bool b in m) if (b) n++; return n; }

        static int CountAnd(bool[] a, bool[] b, bool[] c)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++) if (a[i] && b[i] && c[i]) n++;
            return n;
        }

        static int CountAndNot(bool[] a, bool[] b, bool[] notThis)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++)
                if (a[i] && (b == null || b[i]) && !notThis[i]) n++;
            return n;
        }

        static int DifferingPixels(byte[] a, byte[] b)
        {
            int n = 0;
            for (int i = 0; i + 3 < a.Length && i + 3 < b.Length; i += 4)
                if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3]) n++;
            return n;
        }

        static byte[] Over(byte[] top, byte[] under)
        {
            if (top == null) return under;
            var outp = (byte[])under.Clone();
            for (int i = 0; i + 3 < top.Length; i += 4)
            {
                float a = top[i + 3] / 255f;
                if (a <= 0f) continue;
                for (int k = 0; k < 3; k++)
                    outp[i + k] = (byte)Mathf.RoundToInt(top[i + k] * a + outp[i + k] * (1f - a));
                outp[i + 3] = (byte)Mathf.Max(outp[i + 3], top[i + 3]);
            }
            return outp;
        }

        static byte[] Strip(HullMeshDef hm, byte[] left, byte[] right)
        {
            return Compose(hm.CellW, hm.CellH, 2, 1, new[] { left, right });
        }

        static byte[] Grid(HullMeshDef hm, byte[][] panels)
        {
            // rows: dock then crest; columns: sprite, mesh shell, mesh + fit-out
            return Compose(hm.CellW, hm.CellH, 3, 2,
                           new[] { panels[0], panels[1], panels[2], panels[3], panels[4], panels[5] });
        }

        static byte[] Compose(int cw, int ch, int cols, int rows, byte[][] cells)
        {
            int w = cw * cols, h = ch * rows;
            var outp = new byte[w * h * 4];
            // A flat slate behind every panel: the owner is judging the ROOM, and a checkerboard or
            // clear alpha would put a second thing on screen to look at.
            for (int i = 0; i < w * h; i++)
            {
                outp[i * 4] = 24; outp[i * 4 + 1] = 26; outp[i * 4 + 2] = 30; outp[i * 4 + 3] = 255;
            }
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    byte[] src = cells[r * cols + c];
                    if (src == null) continue;
                    for (int y = 0; y < ch; y++)
                        for (int x = 0; x < cw; x++)
                        {
                            int s = (y * cw + x) * 4;
                            float a = src[s + 3] / 255f;
                            if (a <= 0f) continue;
                            int d = ((r * ch + y) * w + (c * cw + x)) * 4;
                            for (int k = 0; k < 3; k++)
                                outp[d + k] = (byte)Mathf.RoundToInt(src[s + k] * a + outp[d + k] * (1f - a));
                            outp[d + 3] = 255;
                        }
                }
            return outp;
        }

        static void SavePng(string name, byte[] rgba, HullMeshDef hm)
        {
            SavePng(name, rgba, hm.CellW, hm.CellH);
        }

        static void SavePng(string name, byte[] rgba, int w, int h)
        {
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
            string dir = Path.Combine(RepoRoot, ImageDir);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[interior-mesh spike] wrote " + ImageDir + "/" + name + "  " + w + "x" + h);
        }

        static void WriteReport(string name, string text)
        {
            string dir = Path.Combine(RepoRoot, ReportDir);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), text);
        }

        // =============================================================== loading

        static void LoadOrIgnore(string stem, out BoatInteriorDef def, out BoatVisualDef vis)
        {
            def = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(InteriorFolder + "/" + stem + ".asset");
            vis = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(VisualFolder + "/" + stem + ".asset");
            if (def == null || vis == null || vis.HullMesh == null || vis.HullMesh.Mesh == null)
                Assert.Ignore("SKIPPED — " + stem + " has no interior def or no baked hull mesh here.");
        }

        static int HouseLevelIndex(BoatInteriorDef def)
        {
            int i = LevelIndexNamed(def, "house");
            if (i > 0) return i;
            for (int k = 0; k < def.Levels.Length; k++)
                if (def.Levels[k] != null && def.Levels[k].IsUsable()) return k + 1;
            return 0;
        }

        static int LevelIndexNamed(BoatInteriorDef def, string needle)
        {
            if (def.Levels == null) return 0;
            for (int k = 0; k < def.Levels.Length; k++)
                if (def.Levels[k] != null && def.Levels[k].IsUsable() &&
                    def.Levels[k].Id != null && def.Levels[k].Id.Contains(needle)) return k + 1;
            return 0;
        }
    }
}
