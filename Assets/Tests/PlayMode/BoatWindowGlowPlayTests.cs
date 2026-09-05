using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The glow is confined to the cabin, and it comes out through the windows</b> — the owner's
    /// ruling of 2026-09-03, measured on the hull he reviews at rather than argued about.
    ///
    /// <para><b>The ruling, verbatim:</b> <i>"The glows should be constrained to their space, if its
    /// interior it should be confined to the cabin with the glow only coming through the windows."</i>
    /// He gave it looking at the fleet at his own zoom, where the cabin disc read "large and blobby".
    /// So the thing to measure is FOOTPRINT: how much of the frame her cabin's light lands on.</para>
    ///
    /// <para><b>⭐ The measurement isolates the CABIN by differencing OCCUPANCY, not by switching the
    /// lamps off.</b> Turning the lamps off would take her navigation lights with them — and those
    /// SHRANK in this same commit, so the difference would carry two changes at once and prove
    /// neither. A hull lying MOORED shows one anchor light and, by <see cref="BoatLamps.ShowsWhen"/>,
    /// a lit cabin only while somebody is aboard. So (moored + nobody) against (moored + skipper
    /// below) is the same frame twice with exactly one thing different. Each arm differences its own
    /// pair, so the anchor light — which is NOT the same size in the two arms — cancels out.</para>
    ///
    /// <para><b>The two arms are one build.</b> <see cref="GameServices.BoatLegacyCabinGlow"/> is the
    /// owner's passthrough: ON restores yesterday's 1.5 m disc and draws no windows at all. So
    /// "before" and "after" are the shipped code path either way, and the plate pair is one build
    /// rather than two working trees.</para>
    ///
    /// <para><b>Time frozen, flicker frozen AND RE-TICKED, one hull alive per capture.</b> The
    /// standing lesson from #697/#702: a lit fixture must own its clock and its scene, and freezing a
    /// flicker AFTER the light has pushed its flickered value is a no-op unless the light is cycled so
    /// that the frozen one gets pushed. The wall spill carries the cabin's 0.03 whisper, so it needs
    /// both. (The PANES do not flicker at all — see <see cref="BoatWindowGlow"/> — which is one fewer
    /// thing for this fixture to hold still.)</para>
    ///
    /// <para>GPU fixture: SKIPS on CI, which has no graphics device. The coordinator re-runs it on the
    /// 4060 and the plates go to the PR.</para>
    /// </summary>
    public class BoatWindowGlowPlayTests
    {
        const string CapeMeshPath = "Assets/_Project/Data/Boats/HullMeshes/CapeIslanderIsoHullMesh.asset";
        static readonly int IdDayNightTint = Shader.PropertyToID("_DayNightTint");

        /// <summary>A night frame: luma ≈ 0.12, so the additive shader's gate reads the cycle as ACTIVE
        /// (luma &gt; 0.02) and darkness ≈ 0.88 sits above its full-on band (0.12 + 0.35).</summary>
        static readonly Color NightTint = new Color(0.10f, 0.12f, 0.20f, 1f);

        /// <summary>Bright neutral noon, from the shipped day/night gradient's 0.45 key. The gate reads
        /// darkness ≈ 0.03, well below its 0.12 threshold: nothing may emit.</summary>
        static readonly Color NoonTint = new Color(1.00f, 0.98f, 0.95f, 1f);

        /// <summary>What counts as "the cabin lit this pixel": the rise in summed rgb between the dark
        /// shot and the lit one. Low on purpose — the question is where her light LANDS, and a generous
        /// threshold would flatter the confined arm by discarding its own soft edges first.</summary>
        const int LitThreshold = 12;

        static readonly float[] Headings = { 0f, 90f, 180f, 270f };

        readonly List<Object> _spawned = new();
        Color _tintBefore;
        float _timeScaleBefore;
        GameConfig _configBefore;
        Camera _cam;
        RenderTexture _rt;
        HullMeshDef _def;

        struct Arm
        {
            public GameObject Root;
            public IsoFacetHullRenderer Hull;
            public BoatLamps Lamps;
            public BoatWindowGlow Windows;
        }

        [SetUp]
        public void SetUp()
        {
            var listener = new GameObject("Listener");
            listener.AddComponent<AudioListener>();
            _spawned.Add(listener);

            _tintBefore = Shader.GetGlobalColor(IdDayNightTint);
            _timeScaleBefore = Time.timeScale;
            _configBefore = GameServices.Config;
            Shader.SetGlobalColor(IdDayNightTint, NightTint);
            Time.timeScale = 0f;

            // Whatever a prior fixture left in the shader globals reaches these fragments too.
            foreach (string g in new[] { "_BoatLightPos", "_BoatLightDir", "_BoatLightParams", "_BoatLightParams2" })
                Shader.SetGlobalVector(g, Vector4.zero);
            Shader.SetGlobalColor("_BoatLightColor", Color.black);
        }

        [TearDown]
        public void TearDown()
        {
            Shader.SetGlobalColor(IdDayNightTint, _tintBefore);
            Time.timeScale = _timeScaleBefore;
            GameServices.Config = _configBefore;
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); _rt = null; }
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();
        }

        // -------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator HerCabinLightStopsBeingAPoolOnTheDeckAndBecomesHerWindows()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device. These pictures need the local GPU.");

            _def = LoadCommitted<HullMeshDef>(CapeMeshPath);
            if (_def == null) yield break;
            Assert.IsNotNull(_def.Panes);
            Assert.AreEqual(8, _def.Panes.Length,
                            "the cape's eight windows — there is nothing to measure until the probe " +
                            "has written them into her def");

            SetUpCamera();

            var report = new StringBuilder();
            report.AppendLine("BOAT GLOW CONFINEMENT (owner's ruling, 2026-09-03) — the Cape Islander, MOORED, " +
                              "the cabin isolated by OCCUPANCY so the shrunk navigation lamps cancel.");
            report.AppendLine($"night tint {NightTint} (luma {Luma(NightTint):0.000}); cell {_def.CellW}x{_def.CellH}; " +
                              $"{_def.Panes.Length} panes; lit threshold {LitThreshold}/765");
            report.AppendLine("heading | (null) same arm twice | (legacy) disc px | (shipped) windows px | ratio | walls washing");

            long legacyTotal = 0, shippedTotal = 0, nullTotal = 0;
            float worstRatio = 0f; float worstAt = -1f;

            foreach (float heading in Headings)
            {
                // ---- yesterday's disc, through the passthrough ----------------------------------
                GameServices.Config = ConfigWith(legacyGlow: true);
                Arm a = BuildArm("CapeLegacy");
                yield return Pose(a, heading);

                yield return SetCabin(a, false);
                byte[] darkL = Capture();
                yield return null;
                byte[] darkLAgain = Capture();          // the NOISE FLOOR: same arm, same state, next frame
                yield return SetCabin(a, true);
                byte[] litL = Capture();

                int nulls = Footprint(darkL, darkLAgain);
                int legacy = Footprint(darkL, litL);
                SavePlate($"cape-{heading:000}-1-legacy-disc.png", litL);
                SavePlate($"cape-{heading:000}-0-dark.png", darkL);
                Kill(a);
                yield return null;

                // ---- the ruling ----------------------------------------------------------------
                GameServices.Config = ConfigWith(legacyGlow: false);
                Arm b = BuildArm("CapeWindows");
                yield return Pose(b, heading);

                yield return SetCabin(b, false);
                byte[] darkS = Capture();
                yield return SetCabin(b, true);
                byte[] litS = Capture();

                int shipped = Footprint(darkS, litS);
                SavePlate($"cape-{heading:000}-2-windows.png", litS);

                // Every wall that is washing must be washing TOWARD the viewer. A wall on the far side
                // would be throwing amber across her own roof — the artifact the back-face rule exists
                // to prevent, and the one plate 3 of the charter asks about.
                int washing = CountWashingWalls(b, heading);

                Kill(b);
                yield return null;

                float here = legacy > 0 ? (float)shipped / legacy : 0f;
                if (here > worstRatio) { worstRatio = here; worstAt = heading; }
                report.AppendLine($"  {heading,5:0} | {nulls,6} | {legacy,7} | {shipped,7} | " +
                                  $"{here,6:0.000} | {washing}");
                nullTotal += nulls; legacyTotal += legacy; shippedTotal += shipped;
            }

            Debug.Log(report.ToString());

            // ⚠️ THE NULL CASE FIRST. A footprint metric that counts something on two identical frames
            // is measuring its own noise, and every number under it would be meaningless.
            Assert.AreEqual(0, nullTotal,
                            "the same arm rendered twice differs — this metric is counting noise, so " +
                            "nothing below it can be believed. Suspect the clock or the flicker freeze.");

            Assert.Greater(legacyTotal, 0, "the passthrough arm lit nothing — the A/B has no 'before'");
            Assert.Greater(shippedTotal, 0, "the shipped arm lit nothing — her windows are not drawing");

            // ⭐ THE RULING, AS A RATIO. Ratioed against the arm it replaced rather than pinned to an
            // absolute pixel count, because an absolute bar rots the moment the art improves: what has
            // to stay true is that the confined glow lands on LESS of the frame than the disc did, not
            // that it lands on some particular number of pixels.
            float ratio = (float)shippedTotal / legacyTotal;

            // ⭐ AND IT MUST HOLD AT EVERY HEADING, NOT ON AVERAGE — which is the assertion the first
            // tune of this feature would have slipped past. At the heading where TWO of her walls face
            // the viewer at once, the two washes together covered 1.23x what the disc did, while the
            // four-heading average sat at a comfortable 0.72 and said nothing was wrong. "Constrained
            // to its space" is a statement about every view of the boat, so a mean is the wrong
            // statistic and a maximum is the right one.
            Assert.Less(worstRatio, 0.85f,
                        $"at heading {worstAt:0} her cabin light covers {worstRatio:P0} of the area the " +
                        "disc covered — the worst of the four. Suspect the wall wash's throw or its " +
                        "half-angle: two walls facing the viewer at once is where this shows first.");

            Assert.Less(ratio, 0.60f,
                        $"her cabin light covers {ratio:P0} of the area the disc covered across the " +
                        "four headings. The ruling was that a glow be CONSTRAINED to its space; if " +
                        "this has crept up, something has widened the wash or put the disc back.");
        }

        [UnityTest]
        public IEnumerator TheCapeRunningInAtNight_OldDiscAgainstNewWindows()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device. These pictures need the local GPU.");

            // ⭐ THE OWNER'S OWN FRAME. The measurement above moors her, because mooring is what lets
            // occupancy isolate the cabin from the navigation lamps. This one does not measure: it is
            // the picture he asked for — the cape UNDER WAY at night, her whole light show going, in
            // both arms at four headings — so that the plate pair he judges is the boat as she sails
            // rather than as a fixture posed her.
            _def = LoadCommitted<HullMeshDef>(CapeMeshPath);
            if (_def == null) yield break;
            SetUpCamera();

            foreach (float heading in Headings)
            {
                GameServices.Config = ConfigWith(legacyGlow: true);
                Arm a = BuildArm("CapeRunningLegacy");
                yield return PoseUnderWay(a, heading);
                SavePlate($"running-{heading:000}-1-legacy-disc.png", Capture());
                Kill(a);
                yield return null;

                GameServices.Config = ConfigWith(legacyGlow: false);
                Arm b = BuildArm("CapeRunningWindows");
                yield return PoseUnderWay(b, heading);
                SavePlate($"running-{heading:000}-2-windows.png", Capture());

                // ⚠️ AND THE FAR SIDE OF THE HOUSE IS NOT THROWING AT THE CAMERA. This is charter
                // plate 3 as an assertion rather than as something to squint at: at every heading she
                // sails, the walls that wash are the walls the viewer can see into.
                CountWashingWalls(b, heading);
                Kill(b);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AtNoonHerWindowsAreJustGlass()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device. These pictures need the local GPU.");

            _def = LoadCommitted<HullMeshDef>(CapeMeshPath);
            if (_def == null) yield break;
            SetUpCamera();

            // The panes ride the SAME published _DayNightTint gate every additive light in the project
            // reads, so a lit cabin at noon must be indistinguishable from a dark one. This is the
            // control that says the feature costs nothing for the twelve hours nobody can see it.
            Shader.SetGlobalColor(IdDayNightTint, NoonTint);
            GameServices.Config = ConfigWith(legacyGlow: false);

            Arm a = BuildArm("CapeNoon");
            yield return Pose(a, 90f);
            yield return SetCabin(a, false);
            byte[] dark = Capture();
            yield return SetCabin(a, true);
            byte[] lit = Capture();

            Assert.AreEqual(0, Footprint(dark, lit),
                            "her windows emit at noon. The gate lives in the shared additive shader and " +
                            "reads the published tint — if this fails, something is bypassing it.");
        }

        // -------------------------------------------------------------------------------------------

        static int CountWashingWalls(Arm arm, float heading)
        {
            Assert.IsNotNull(arm.Windows, "the cape declares panes, so Install must have mounted BoatWindowGlow");
            SceneLight[] spills = arm.Windows.Spills;
            Assert.IsNotNull(spills, "her wall spills were never built");

            int on = 0;
            for (int i = 0; i < spills.Length; i++)
            {
                if (spills[i] == null || !spills[i].enabled) continue;
                on++;
                Vector2 dir = spills[i].BeamDirection;
                Assert.Less(dir.y, 1e-3f,
                            $"heading {heading}: her {arm.Windows.SpillWalls[i]} wall is washing UP the " +
                            "screen, away from the viewer — that light is going through her own roof");
            }
            Assert.Greater(on, 0, $"heading {heading}: no wall of a lit cabin is washing at all");
            Assert.LessOrEqual(on, 2,
                               $"heading {heading}: {on} walls are washing at once. A box shows a viewer " +
                               "at most two of its sides, so more than that means the back-face rule is off.");
            return on;
        }

        // -------------------------------------------------------------------------------------------
        //  scaffolding
        // -------------------------------------------------------------------------------------------

        void SetUpCamera()
        {
            _rt = new RenderTexture(_def.CellW, _def.CellH, 24, RenderTextureFormat.ARGB32)
            { filterMode = FilterMode.Point };
            var camGo = new GameObject("WindowCam");
            _spawned.Add(camGo);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = _def.CellH / (2f * _def.PxPerMetre);
            _cam.nearClipPlane = 1f;
            _cam.farClipPlane = 400f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Color.clear;
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.targetTexture = _rt;
        }

        static GameConfig ConfigWith(bool legacyGlow)
        {
            var c = ScriptableObject.CreateInstance<GameConfig>();
            c.BoatLegacyCabinGlow = legacyGlow;
            return c;
        }

        Arm BuildArm(string name)
        {
            var root = new GameObject(name);
            _spawned.Add(root);
            var host = new GameObject("FacetMesh");
            host.transform.SetParent(root.transform, false);

            IHullMeshRenderer installed = new IsoFacetHullPresentationService().Install(host, _def);
            Assert.IsNotNull(installed, $"{name}: the full install path refused the def");

            var arm = new Arm
            {
                Root = root,
                Hull = host.GetComponent<IsoFacetHullRenderer>(),
                Lamps = host.GetComponent<BoatLamps>(),
                Windows = host.GetComponent<BoatWindowGlow>(),
            };
            Assert.IsNotNull(arm.Lamps, $"{name}: the def declares lamps");

            // Her searchlight is a beam, not a glow, and it is not what is under review.
            var beam = root.GetComponent<BoatSpotlight>();
            if (beam != null) beam.SetBeam(false);
            return arm;
        }

        void Kill(Arm arm)
        {
            if (arm.Root == null) return;
            _spawned.Remove(arm.Root);
            Object.DestroyImmediate(arm.Root);
        }

        /// <summary>Pose her, MOOR her, and let the lamps, the windows and every light quad land — then
        /// freeze the flicker AND re-tick it, so the value that was pushed is the frozen one.</summary>
        IEnumerator Pose(Arm arm, float heading)
        {
            arm.Hull.HeadingDirUnits = HullMeshMath.HeadingToDirUnits(heading, 0f, _def.AzimuthCounterClockwise);
            arm.Lamps.OnVesselWayChanged(VesselWay.Moored);
            yield return null;
            foreach (SceneLight l in arm.Root.GetComponentsInChildren<SceneLight>(true)) l.FlickerAmount = 0f;
            arm.Lamps.LampsOn = false; arm.Lamps.LampsOn = true;
            foreach (SceneLight l in arm.Root.GetComponentsInChildren<SceneLight>(true)) l.FlickerAmount = 0f;
            yield return null;
            yield return new WaitForSecondsRealtime(0.12f);
            yield return null;
            yield return null;
        }

        /// <summary>The same, but UNDER WAY — her full light show, which is how the owner sees her.
        /// A boat making way has somebody at her wheel by definition, so her cabin is lit with nobody
        /// having to go below.</summary>
        IEnumerator PoseUnderWay(Arm arm, float heading)
        {
            arm.Hull.HeadingDirUnits = HullMeshMath.HeadingToDirUnits(heading, 0f, _def.AzimuthCounterClockwise);
            arm.Lamps.OnVesselWayChanged(VesselWay.UnderWay);
            yield return null;
            foreach (SceneLight l in arm.Root.GetComponentsInChildren<SceneLight>(true)) l.FlickerAmount = 0f;
            arm.Lamps.LampsOn = false; arm.Lamps.LampsOn = true;
            foreach (SceneLight l in arm.Root.GetComponentsInChildren<SceneLight>(true)) l.FlickerAmount = 0f;
            yield return null;
            yield return new WaitForSecondsRealtime(0.12f);
            yield return null;
            yield return null;
        }

        /// <summary>Put somebody below, or take them back on deck — the one thing that differs between
        /// the two shots — and let a frame run so the pose loops have moved to match.</summary>
        IEnumerator SetCabin(Arm arm, bool aboard)
        {
            var id = arm.Root.GetEntityId();
            if (aboard) EventBus.Publish(new CabinEntered(id, 0));
            else EventBus.Publish(new CabinLeft(id));

            // ⚠️ RE-FREEZE AND RE-TICK, in that order. Going below re-stamps the cabin preset — which
            // puts the 0.03 flicker back — and with the clock stopped a SceneLight's throttled Update
            // never fires again on its own, so the value the shader is holding would be whatever was
            // pushed last. Cycling the master switch is what makes OnEnable push the frozen one; it is
            // the same lesson that cost #697 five false reds.
            foreach (SceneLight l in arm.Root.GetComponentsInChildren<SceneLight>(true)) l.FlickerAmount = 0f;
            arm.Lamps.LampsOn = false; arm.Lamps.LampsOn = true;
            foreach (SceneLight l in arm.Root.GetComponentsInChildren<SceneLight>(true)) l.FlickerAmount = 0f;
            yield return null;
            yield return null;
        }

        /// <summary>Frame the cell over the origin and render NOW — no frame passes.</summary>
        byte[] Capture()
        {
            float ppu = _def.PxPerMetre;
            float ox = (_def.PivotPx.x - _def.CellW / 2f) / ppu;
            float oy = (_def.CellH / 2f - _def.PivotPx.y) / ppu;
            _cam.transform.position = new Vector3(-ox, -oy, -100f);
            _cam.Render();

            int w = _def.CellW, h = _def.CellH;
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

        /// <summary>How many pixels her cabin lit: those whose summed rgb rose by more than the
        /// threshold between the dark shot and the lit one. It counts AREA, not brightness, because
        /// "blobby" is a statement about area.</summary>
        static int Footprint(byte[] dark, byte[] lit)
        {
            int n = 0;
            for (int i = 0; i + 3 < dark.Length; i += 4)
            {
                int d = (lit[i] - dark[i]) + (lit[i + 1] - dark[i + 1]) + (lit[i + 2] - dark[i + 2]);
                if (d > LitThreshold) n++;
            }
            return n;
        }

        static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        /// <summary>GetRawTextureData is bottom-left and so is Texture2D, so the buffer loads upright;
        /// EncodeToPNG writes top-down. One place, no arithmetic.</summary>
        void SavePlate(string name, byte[] rgbaBottomLeft)
        {
            var tex = new Texture2D(_def.CellW, _def.CellH, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgbaBottomLeft);
            tex.Apply();
            string dir = Path.Combine(Application.temporaryCachePath, "boat-glow-confinement");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            Debug.Log($"[boat-glow-confinement] plate written: {path}");
        }

        static T LoadCommitted<T>(string path) where T : ScriptableObject
        {
#if UNITY_EDITOR
            var a = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Assert.Ignore($"SKIPPED — {path} is not in this tree.");
            return a;
#else
            Assert.Ignore("SKIPPED — needs the editor's asset database.");
            return null;
#endif
        }
    }
}
