using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>LAMPS OVER A MESH INTERIOR — the measurement the boat-lights lane was gated on.</b>
    ///
    /// <para>ADR 0041's parity fixture (<c>FullMeshInteriorRenderTests</c>) renders a converted hull
    /// through <c>IsoFacetHullPresentationService.ToSetup</c>, and lamps are wired only by the FULL
    /// <c>Install(host, def, scheme)</c> path — so no <c>BoatLamps</c> ever existed in either of its
    /// arms, and "the cabin costs nothing closed up" had never been shown with her lights burning. This
    /// fixture goes through <c>Install</c>, in PlayMode where <c>BoatLamps</c> and <c>SceneLight</c>
    /// genuinely wake (an EditMode <c>AddComponent</c> fires neither), on the one lamped hull in the
    /// fleet: the cape and her six lamps, at night.</para>
    ///
    /// <para><b>The numbers, per heading.</b> (0) Closed up, lamps DARK: the full mesh against the same
    /// hull with her room stripped out — the arms must agree before any lit number means anything.
    /// (1) Closed up, lamps LIT: the same pair — no pixel may differ by more than 2 LSB, and fewer than
    /// 5% of the lamp footprint may differ at all, or the room is leaking through her lights. (Measured:
    /// the additive glow lands 1-LSB differences inside the cabin glow between two captures — from a
    /// dozen to a few hundred px of ~15k lamp px, varying run to run and heading to heading — with the
    /// DARK pair at exactly 0. That is blend quantisation, not geometry: a room showing through would
    /// be a structured region many LSB deep. Both counts are reported, and the LIT noise floor — the
    /// same arm, lit, on two frames — sits beside them so the reader can see what one capture is
    /// worth.)
    /// (2) The lamps' own footprint closed up (lit vs dark) — must be &gt; 0, or (1) was vacuous.
    /// (3) Cut open, the lamps' footprint over the revealed room. (4) Cut open, the room's footprint under
    /// lit lamps. (5) The cabin-glow occupied boost. (3)–(5) are REPORTED — the composition the lights
    /// lane wants to look at — and only guarded against zero.</para>
    ///
    /// <para>⚠️ <b>Time is frozen, and only ONE hull is alive per capture.</b> Two things fooled this
    /// fixture's first cuts. Two captures of one dark, closed hull a frame apart differed by ~40 px —
    /// frame-time terms in the lit path — which read first as "the room shows through her lights" and
    /// then as "the arms differ"; so <c>Time.timeScale</c> is 0 for the test, and a noise-floor column
    /// (the same arm, dark, on two frames) is asserted to be 0. Then, with both arms alive 20 m apart,
    /// the two DARK arms differed by 51k px at 1–5 LSB across the whole hull: the lights publish
    /// scene-wide shader globals (last writer wins), so each hull was faintly lit by the other's lamps.
    /// So both arms stand at the origin and are toggled — exactly one alive when the camera looks.
    /// And her SEARCHLIGHT (the sixth lamp, <c>BoatSpotlight</c>) is switched OFF for every parity
    /// column: its way-gate smoothing steps by a floored delta-time every frame even with time frozen,
    /// so its cone differed by exactly 1 LSB over 8.5k px between two arms enabled a few frames apart.
    /// A self-animating beam is not hull compositing; it is measured on its own in column (6).</para>
    ///
    /// <para>⚠️ GPU-only, by nature. The report and the plates go to the temporary cache, never the
    /// repo; the PR body quotes them.</para>
    /// </summary>
    public class MeshInteriorLampsPlayTests
    {
        const string CapeMeshPath = "Assets/_Project/Data/Boats/HullMeshes/CapeIslanderIsoHullMesh.asset";
        static readonly int IdDayNightTint = Shader.PropertyToID("_DayNightTint");

        /// <summary>A night frame: luma ≈ 0.12, so the additive-light shader's gate reads the cycle as
        /// ACTIVE (luma &gt; 0.02) and darkness ≈ 0.88 sits above its full-on band (0.12 + 0.35). Stated
        /// here so the number in the report means something.</summary>
        static readonly Color NightTint = new Color(0.10f, 0.12f, 0.20f, 1f);

        static readonly float[] Headings = { 90f, 135f, 180f, 45f };
        readonly List<Arm> _arms = new();

        readonly List<Object> _spawned = new();
        Color _tintBefore;
        float _timeScaleBefore;
        Camera _cam;
        RenderTexture _rt;

        struct Arm
        {
            public GameObject Root;
            public IsoFacetHullRenderer Hull;
            public BoatLamps Lamps;
            public BoatSpotlight Beam;
            public HullMeshDef Def;
        }

        [SetUp]
        public void SetUp()
        {
            var listener = new GameObject("Listener");
            listener.AddComponent<AudioListener>();
            _spawned.Add(listener);

            _tintBefore = Shader.GetGlobalColor(IdDayNightTint);
            Shader.SetGlobalColor(IdDayNightTint, NightTint);
            _timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            Shader.SetGlobalColor(IdDayNightTint, _tintBefore);
            Time.timeScale = _timeScaleBefore;
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); _rt = null; }
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();
        }

        [UnityTest]
        public IEnumerator ClosedUpAtNightWithHerLampsLit_TheRoomStillCostsNothing_ThroughTheFullInstallPath()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device. These pictures need the local GPU.");

            HullMeshDef def = LoadCommitted<HullMeshDef>(CapeMeshPath);
            if (def == null) yield break;
            Assert.IsTrue(def.HasMeshInterior(), "the cape's room is geometry (#690) — nothing to measure otherwise");
            Assert.IsNotNull(def.Lamps);
            Assert.Greater(def.Lamps.Length, 0, "the cape is the lamped hull (#686) — nothing to measure otherwise");

            // The camera first: SceneLight pins its quad to the active camera, and there must be one
            // to pin to when the lamps first pose.
            _rt = new RenderTexture(def.CellW, def.CellH, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            var camGo = new GameObject("LampsCam");
            _spawned.Add(camGo);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = def.CellH / (2f * def.PxPerMetre);
            _cam.nearClipPlane = 1f;
            _cam.farClipPlane = 400f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Color.clear;
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.targetTexture = _rt;

            Mesh stripped = MeshWithoutTheRoom(def.Mesh, out int roomVerts, out int hullVerts);
            _spawned.Add(stripped);
            Assert.Greater(roomVerts, 0, "no room-flagged vertices — the control arm would be the same mesh twice");

            Arm full = BuildArm(def, def.Mesh, "CapeFull");
            Arm control = BuildArm(def, stripped, "CapeRoomStripped");
            control.Root.SetActive(false);   // one hull alive at a time — see the class remarks
            yield return null;
            yield return null;

            Assert.AreEqual(def.Lamps.Length, full.Hull.Lamps.Length, "the renderer carries her lamp table");
            int litQuads = CountEnabledLightQuads(full.Lamps);
            Assert.Greater(litQuads, 0, "BoatLamps built no live light quads — the lamps are not in the picture");

            HullMeshDef.LevelTag house = FirstEnclosedLevel(def);
            var cutOpen = new HullMeshDef.Cut(house.Tag, house.LidTag);

            var report = new StringBuilder();
            report.AppendLine("LAMPS OVER A MESH INTERIOR — the cape, through IsoFacetHullPresentationService.Install (PlayMode, time frozen, one hull alive per capture)");
            report.AppendLine($"night tint {NightTint} (luma {Luma(NightTint):0.000}); lamps {def.Lamps.Length}, live quads {litQuads}; " +
                              $"hull verts {hullVerts}, room verts {roomVerts}; cut = {house.LevelId} (tag {house.Tag}, lid {house.LidTag}); cell {def.CellW}x{def.CellH}");
            report.AppendLine("The five GLOWS (sidelights, stern, masthead, cabin) are the lamps in columns (0)-(5); the SEARCHLIGHT beam is off for them and measured alone in (6).");
            report.AppendLine("heading | (n) noise floor: same arm, dark, two frames | (nL) noise floor: same arm, LIT, two frames | (0) closed, DARK: full vs stripped | (1) closed, LIT: full vs stripped (and how many beyond 2 LSB) | (2) closed: lit vs dark | (3) open: lit vs dark | (4) open, lit: full vs stripped | (5) open, lit: occupied boost vs not | (6) closed: searchlight on vs off (full arm)");

            foreach (float heading in Headings)
            {
                // closed up, lit
                SetLamps(full, true); SetLamps(control, true);
                yield return Pose(full, heading, HullMeshDef.Cut.None);
                byte[] fullClosedLit = Capture(full);
                yield return null;
                byte[] fullClosedLitAgain = Capture(full);   // the LIT noise floor: same arm, next frame
                yield return Pose(control, heading, HullMeshDef.Cut.None);
                byte[] controlClosedLit = Capture(control);

                // closed up, dark; then the same arm again a frame later (the noise floor)
                SetLamps(full, false); SetLamps(control, false);
                yield return Pose(full, heading, HullMeshDef.Cut.None);
                byte[] fullClosedDark = Capture(full);
                yield return null;
                byte[] fullClosedDarkAgain = Capture(full);
                yield return Pose(control, heading, HullMeshDef.Cut.None);
                byte[] controlClosedDark = Capture(control);

                int noiseFloor = CountDiffering(fullClosedDark, fullClosedDarkAgain);
                int litNoiseFloor = CountDiffering(fullClosedLit, fullClosedLitAgain);
                int parityClosedDark = CountDiffering(fullClosedDark, controlClosedDark);
                int parityClosed = CountDiffering(fullClosedLit, controlClosedLit);
                int parityClosedBeyondLsb = CountDifferingBeyond(fullClosedLit, controlClosedLit, 2);
                int lampsClosed = CountDiffering(fullClosedLit, fullClosedDark);

                int inked = CountInked(fullClosedLit);
                if (heading == Headings[0])
                {
                    SavePlate($"diag-{heading:000}-closed-lit.png", fullClosedLit, def.CellW, def.CellH);
                    SavePlate($"diag-{heading:000}-closed-dark.png", fullClosedDark, def.CellW, def.CellH);
                    SavePlate($"diag-{heading:000}-closed-dark-control.png", controlClosedDark, def.CellW, def.CellH);
                    SavePlate($"diag-{heading:000}-closed-lit-control.png", controlClosedLit, def.CellW, def.CellH);
                }
                Assert.Greater(inked, 0, $"at {heading} the capture is empty — the hull itself did not render");

                // cut open, lit; cut open, dark
                SetLamps(full, true); SetLamps(control, true);
                yield return Pose(full, heading, cutOpen);
                byte[] fullOpenLit = Capture(full);
                yield return Pose(control, heading, cutOpen);
                byte[] controlOpenLit = Capture(control);
                SetLamps(full, false);
                yield return Pose(full, heading, cutOpen);
                byte[] fullOpenDark = Capture(full);

                int lampsOpen = CountDiffering(fullOpenLit, fullOpenDark);
                int roomOpenLit = CountDiffering(fullOpenLit, controlOpenLit);

                // The cabin glow's occupied boost, published the way the cabin publishes it. With time
                // frozen SceneLight's throttled Tick never re-runs, so the lights are cycled off and on
                // to push the boosted preset through OnEnable.
                EventBus.Publish(new CabinEntered(full.Root.GetEntityId(), 0));
                SetLamps(full, false); SetLamps(full, true);
                yield return Pose(full, heading, cutOpen);
                byte[] fullOpenLitOccupied = Capture(full);
                int boost = CountDiffering(fullOpenLitOccupied, fullOpenLit);
                EventBus.Publish(new CabinLeft(full.Root.GetEntityId()));
                SetLamps(full, false); SetLamps(full, true);
                yield return Pose(full, heading, cutOpen);

                // (6) the searchlight on its own: the full arm, closed up, glows lit, beam on vs off
                SetLamps(full, true);
                full.Beam.SetBeam(true);
                yield return Pose(full, heading, HullMeshDef.Cut.None);
                byte[] fullClosedBeam = Capture(full);
                full.Beam.SetBeam(false);
                yield return Pose(full, heading, HullMeshDef.Cut.None);
                byte[] fullClosedNoBeam = Capture(full);
                int beamPx = CountDiffering(fullClosedBeam, fullClosedNoBeam);
                if (heading == 135f)
                    SavePlate("cape-135-closed-lamps-and-searchlight-night.png", fullClosedBeam, def.CellW, def.CellH);

                string row = $"{heading,5:0}°   | {noiseFloor,8} px | {litNoiseFloor,8} px | {parityClosedDark,8} px | {parityClosed,5} px ({parityClosedBeyondLsb} >2 LSB) | " +
                             $"{lampsClosed,8} px | {lampsOpen,8} px | {roomOpenLit,8} px | {boost,8} px | {beamPx,8} px";
                report.AppendLine(row);
                Debug.Log("[mesh-interiors-retirement] " + row + $"   (inked {inked})");

                if (heading == 135f)
                    SavePlate("cape-135-open-house-lamps-night.png", fullOpenLit, def.CellW, def.CellH);

                Assert.AreEqual(0, noiseFloor,
                    $"at {heading}° the same arm, dark, captured on two frames differs by {noiseFloor} px " +
                    "— time is not frozen, and every other number here carries that noise");
                Assert.AreEqual(0, parityClosedDark,
                    $"at {heading}° with the lamps DARK, {parityClosedDark} px differ between the two " +
                    "arms — the arms differ for a reason that is not the lamps, so no lit number here " +
                    "would mean anything");
                Assert.Greater(lampsClosed, 0,
                    $"at {heading}° her lit lamps changed no pixels against her dark self — the lamps " +
                    "are not reaching the frame, so the parity number would be vacuous");
                Assert.AreEqual(0, parityClosedBeyondLsb,
                    $"closed up at {heading}° with her lamps lit, {parityClosedBeyondLsb} px differ by MORE " +
                    "than 2 LSB between the full mesh and the room-stripped control carrying the same " +
                    $"lamps ({parityClosed} px differ at all). The room is showing through her lights.");
                Assert.Less(parityClosed, Mathf.Max(1, lampsClosed / 20),
                    $"closed up at {heading}° with her lamps lit, {parityClosed} px differ (all within 2 LSB) " +
                    $"against a lamp footprint of {lampsClosed} px — more than the few-percent blend " +
                    "quantisation this fixture has measured; look at the plates before calling it noise.");
                Assert.Greater(roomOpenLit, 0, $"at {heading}° the cut revealed no room at all under lit lamps");
                Assert.Greater(lampsOpen, 0, $"at {heading}° the lamps changed nothing over the revealed room");
            }

            string dir = Path.Combine(Application.temporaryCachePath, "mesh-interiors-retirement");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "cape-lamps-over-mesh-interior.txt");
            File.WriteAllText(path, report.ToString());
            Debug.Log($"[mesh-interiors-retirement] {path}\n{report}");
        }

        // ------------------------------------------------------------------------------ machinery

        Arm BuildArm(HullMeshDef def, Mesh mesh, string name)
        {
            var root = new GameObject(name);
            _spawned.Add(root);

            var host = new GameObject("FacetMesh");
            host.transform.SetParent(root.transform, false);

            HullMeshDef d = def;
            if (!ReferenceEquals(mesh, def.Mesh))
            {
                d = Object.Instantiate(def);
                d.name = def.name + "_RoomStripped";
                d.Mesh = mesh;
                _spawned.Add(d);
            }

            IHullMeshRenderer installed = new IsoFacetHullPresentationService().Install(host, d);
            Assert.IsNotNull(installed, $"{name}: the full install path refused the def");

            var hull = host.GetComponent<IsoFacetHullRenderer>();
            Assert.IsNotNull(hull);
            var lamps = host.GetComponent<BoatLamps>();
            Assert.IsNotNull(lamps, $"{name}: the def declares lamps, so Install must have mounted BoatLamps");

            // The searchlight is mounted on the boat ROOT by MakeSearchlit, ON and deaf to the key. Off
            // here for the parity columns — see the class remarks — and driven by hand in column (6).
            var beam = root.GetComponent<BoatSpotlight>();
            Assert.IsNotNull(beam, $"{name}: the cape declares a searchlight, so Install must have mounted BoatSpotlight on her root");
            beam.SetBeam(false);

            var arm = new Arm { Root = root, Hull = hull, Lamps = lamps, Beam = beam, Def = d };
            _arms.Add(arm);
            return arm;
        }

        static void SetLamps(Arm arm, bool on) => arm.Lamps.LampsOn = on;

        /// <summary>Make THIS arm the one alive, set her pose and cut, and let real time pass so the
        /// hull's LateUpdate, the lamps' rebuild, the lights' quad pose and SceneLight's property push
        /// have all landed. Toggling the root is what wakes BoatLamps (OnEnable rebuilds her lights) —
        /// so the flicker freeze is re-applied after it.</summary>
        IEnumerator Pose(Arm arm, float heading, HullMeshDef.Cut cut)
        {
            foreach (Arm other in _arms)
            {
                bool alive = ReferenceEquals(other.Root, arm.Root);
                if (other.Root.activeSelf != alive) other.Root.SetActive(alive);
            }
            arm.Hull.HeadingDirUnits = HullMeshMath.HeadingToDirUnits(heading, 0f, arm.Def.AzimuthCounterClockwise);
            arm.Hull.ShowCutaway(cut);
            yield return null;
            // A MEASUREMENT must not depend on the clock: the cabin glow's preset flickers (0.03), and
            // BoatLamps re-stamps the preset on every rebuild and on the occupied boost, so it is
            // frozen after the arm has woken and again before the capture.
            foreach (SceneLight l in arm.Lamps.GetComponentsInChildren<SceneLight>(true)) l.FlickerAmount = 0f;
            yield return new WaitForSecondsRealtime(0.12f);
            yield return null;
            yield return null;
        }

        /// <summary>Frame the cell over the origin and render NOW — no frame passes.</summary>
        byte[] Capture(Arm arm)
        {
            float ppu = arm.Def.PxPerMetre;
            float ox = (arm.Def.PivotPx.x - arm.Def.CellW / 2f) / ppu;
            float oy = (arm.Def.CellH / 2f - arm.Def.PivotPx.y) / ppu;
            _cam.transform.position = new Vector3(-ox, -oy, -100f);
            _cam.Render();

            int w = arm.Def.CellW, h = arm.Def.CellH;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            byte[] bytes = tex.GetRawTextureData();
            var copy = new byte[bytes.Length];
            System.Array.Copy(bytes, copy, bytes.Length);
            Object.Destroy(tex);
            return copy;
        }

        static int CountInked(byte[] rgba)
        {
            int n = 0;
            for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] > 8) n++;
            return n;
        }

        static int CountEnabledLightQuads(BoatLamps lamps)
        {
            int n = 0;
            foreach (MeshRenderer mr in lamps.GetComponentsInChildren<MeshRenderer>(true))
                if (mr.enabled && mr.gameObject.name == "SceneLightQuad") n++;
            return n;
        }

        static HullMeshDef.LevelTag FirstEnclosedLevel(HullMeshDef def)
        {
            foreach (HullMeshDef.LevelTag t in def.LevelTags) if (t.Enclosed) return t;
            Assert.Fail("the cape declares no enclosed level");
            return default;
        }

        static float Luma(Color c) => Mathf.Max(0f, 0.299f * c.r + 0.587f * c.g + 0.114f * c.b);

        /// <summary>Pixels whose largest channel difference exceeds <paramref name="lsb"/>.</summary>
        static int CountDifferingBeyond(byte[] a, byte[] b, int lsb)
        {
            int n = 0;
            for (int i = 0; i + 3 < a.Length; i += 4)
            {
                int d = Mathf.Max(Mathf.Abs(a[i] - b[i]), Mathf.Abs(a[i + 1] - b[i + 1]),
                                  Mathf.Abs(a[i + 2] - b[i + 2]), Mathf.Abs(a[i + 3] - b[i + 3]));
                if (d > lsb) n++;
            }
            return n;
        }

        static int CountDiffering(byte[] a, byte[] b)
        {
            int n = 0;
            for (int i = 0; i + 3 < a.Length; i += 4)
                if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3]) n++;
            return n;
        }

        /// <summary>The same mesh with every room-flagged face removed — the control arm, built the way
        /// <c>FullMeshInteriorRenderTests.MeshWithoutTheRoom</c> builds it (TexCoord1.y is the flag).</summary>
        static Mesh MeshWithoutTheRoom(Mesh src, out int roomVerts, out int hullVerts)
        {
            var tags = new List<Vector2>();
            src.GetUVs(1, tags);
            var uv0 = new List<Vector4>();
            src.GetUVs(0, uv0);
            Vector3[] v = src.vertices, n = src.normals;
            int[] tri = src.triangles;

            var keepV = new List<Vector3>();
            var keepN = new List<Vector3>();
            var keepA = new List<Vector4>();
            var keepL = new List<Vector2>();
            var keepT = new List<int>();
            var remap = new int[v.Length];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;

            roomVerts = 0;
            for (int i = 0; i < v.Length; i++) if (tags[i].y > 0.5f) roomVerts++;
            hullVerts = v.Length - roomVerts;

            for (int t = 0; t + 2 < tri.Length; t += 3)
            {
                if (tags[tri[t]].y > 0.5f) continue;
                for (int k = 0; k < 3; k++)
                {
                    int s = tri[t + k];
                    if (remap[s] < 0)
                    {
                        remap[s] = keepV.Count;
                        keepV.Add(v[s]); keepN.Add(n[s]); keepA.Add(uv0[s]); keepL.Add(tags[s]);
                    }
                    keepT.Add(remap[s]);
                }
            }

            var m = new Mesh { name = src.name + "_HullOnly", indexFormat = src.indexFormat };
            m.SetVertices(keepV); m.SetNormals(keepN);
            m.SetUVs(0, keepA); m.SetUVs(1, keepL);
            m.SetTriangles(keepT, 0, true);
            return m;
        }

        /// <summary>GetRawTextureData is bottom-left and so is Texture2D, so the buffer loads upright;
        /// EncodeToPNG writes top-down. One place, no arithmetic.</summary>
        static void SavePlate(string name, byte[] rgbaBottomLeft, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgbaBottomLeft);
            tex.Apply();
            string dir = Path.Combine(Application.temporaryCachePath, "mesh-interiors-retirement");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            Debug.Log($"[mesh-interiors-retirement] plate written: {path}");
        }

        static T LoadCommitted<T>(string path) where T : ScriptableObject
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"missing {path}");
            return asset;
#else
            Assert.Ignore("Needs the AssetDatabase: this measurement is of the REAL committed cape.");
            return null;
#endif
        }
    }
}
