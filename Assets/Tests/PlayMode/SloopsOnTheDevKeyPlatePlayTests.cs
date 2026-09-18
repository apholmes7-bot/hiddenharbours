using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>THE PLATES: the two mesh sloops on the dev key at St Peters</b> — what the owner will see
    /// the first time they press <b>F</b> past the end of the old list. Three sets:
    /// <list type="number">
    /// <item>each sloop <b>after an F-swap</b>, on the key she arrives at;</item>
    /// <item>the thirty through <b>eight headings</b>, because a mesh hull is not a sprite and the
    /// picture has to change when she turns;</item>
    /// <item>the eighty-eight <b>beside the Cape Islander</b>, because 26.8 m means nothing next to
    /// nothing.</item>
    /// </list>
    ///
    /// <para><b>She lands bare-poled.</b> These plates are of a hull and her paint, not of a rig: there
    /// is no boom, no sail and no steering in this branch. A plate that looks like an unfinished boat is
    /// an accurate plate.</para>
    ///
    /// <para><b>The swap goes through <see cref="DevBoatPicker.Show"/>, the scene's own picker</b> — the
    /// same call the F key makes through <c>Next()</c>. Nothing here reaches for the input loop, and
    /// that matters twice: it is the seam the owner will actually use, and
    /// <b>St Peters is the START scene</b>, so a fixture that loads it arrives at the TITLE PAGE with
    /// <see cref="ShellFlow.WorldInputBlocked"/> true and every keystroke silently doing nothing. The
    /// slate is wiped with <see cref="ShellFlow.Reset"/> — never <c>StartNewGame()</c>, which would
    /// begin the arrival and overwrite the savegame this box shares with every other worktree.</para>
    ///
    /// <para><b>⚠️ These SKIP on CI and prove nothing there</b> — a plate needs a GPU and CI runs the
    /// Null device. The numbers under each plate are measured locally and quoted in the PR.</para>
    ///
    /// <para><b>⚠️ The plate directory is shared by every worktree</b> (one
    /// <c>Application.temporaryCachePath</c> for the whole machine), so a plate found in it is not
    /// necessarily this run's plate. A <b>floor file</b> is written before the first frame; only plates
    /// newer than it belong to this run.</para>
    ///
    /// <para><b>Every frame is named.</b> A caption is written beside each plate with the camera, the
    /// zoom, the pixel size, the hour, the hull and her drawn heading — a measurement that does not name
    /// the frame it was shot through is not a measurement.</para>
    /// </summary>
    public class SloopsOnTheDevKeyPlatePlayTests
    {
        const string SceneName  = "StPeters";
        const string PlateDir   = "sloops-dev-key";
        const string BoatsFolder = "Assets/_Project/Data/Boats";
        const string Sloop30Name = "Sloop30";
        const string Sloop88Name = "Sloop88";
        const string CapeName    = "CapeIslander";

        /// <summary>Midday: the sun is up, the lamps are off, and the paint is the only thing to read.</summary>
        const float PlateHour = 12f;

        WharfNightStage _stage;

        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        bool _loadedAny;
        bool _introOnEntry;

        static string s_floor;

        // =============================================================================================
        //  The floor, the snapshot and the #764 teardown
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The floor file, written before any plate.</b> The plate directory is one directory for
        /// the whole machine: every worktree, every branch, every run that ever shot a plate has left
        /// files in it. <c>find -newer</c> against this file is the only way to say which pictures came
        /// out of THIS run.
        /// </summary>
        [OneTimeSetUp]
        public void WriteTheFloor()
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            s_floor = Path.Combine(dir, "_floor.txt");
            File.WriteAllText(
                s_floor,
                "Floor for " + nameof(SloopsOnTheDevKeyPlatePlayTests) + " at " +
                System.DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "Only plates in this directory NEWER than this file were shot by this run.\n",
                new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] FLOOR written: {s_floor} — read only plates newer than it " +
                      $"(`find \"{dir}\" -newer \"{s_floor}\" -name '*.png'`).");
        }

        [UnitySetUp]
        public IEnumerator SetUpRegion()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);
            yield return null;
        }

        /// <summary>
        /// The #764 teardown: the stage unloads the scene, but a <c>LoadSceneMode.Single</c> also leaves
        /// every <c>DontDestroyOnLoad</c> root the region installed standing for the rest of the run.
        /// They are destroyed by IDENTITY against the snapshot taken before the load, never by name.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;   // ⚠ a STATIC: left at 0 by the frozen frame it stops every test after this one

            if (_stage != null)
            {
                yield return _stage.TearDown();
                _stage = null;
            }

            if (!_loadedAny) yield break;

            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go)) Object.DestroyImmediate(go);

            GameServices.Reset();
            GameServices.OpeningCinematicRunning = _introOnEntry;
            ShellFlow.Reset();          // the slate the next fixture expects; publishes nothing
            InteractionGate.Reset();
            _loadedAny = false;
            yield return null;
        }

        // =============================================================================================
        //  The plates
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>PLATE SET 1 — each sloop, on the key, one F-swap after the boat the owner was in.</b>
        /// The picture the change is for.
        /// </summary>
        [UnityTest]
        public IEnumerator TheThirty_AfterAnFSwap_AtStPeters()
        {
            // ⚠️ BOTH of these stand before the first yield: an Assert.Ignore raised after a yield is
            // recorded as FAILED, not skipped, and this fixture must skip cleanly on CI.
            WharfNightStage.RequireAGraphicsDevice();
            BoatHullDef hull = LoadHull(Sloop30Name);
            yield return AfterAnFSwap(Sloop30Name, hull);
        }

        /// <inheritdoc cref="TheThirty_AfterAnFSwap_AtStPeters"/>
        [UnityTest]
        public IEnumerator TheEightyEight_AfterAnFSwap_AtStPeters()
        {
            WharfNightStage.RequireAGraphicsDevice();
            BoatHullDef hull = LoadHull(Sloop88Name);
            yield return AfterAnFSwap(Sloop88Name, hull);
        }

        IEnumerator AfterAnFSwap(string rung, BoatHullDef hull)
        {
            yield return TheKeyAtMidday();

            DevBoatPicker picker = null;
            yield return FindTheScenesPicker(p => picker = p);

            Transform subject = null;
            yield return SwapTo(picker, hull, t => subject = t);

            yield return _stage.FrameOn(subject.position);
            FitTheFrame(1.6f * hull.LengthMeters, $"{rung} is {hull.LengthMeters:0.0} m long");

            byte[] frame = _stage.Capture();
            string what = $"{rung} after the F-swap";
            AssertTheSubjectIsInFrame(subject, what);
            if (CanComposite(subject, what))
                SaveWithItsFrame($"1-{rung.ToLowerInvariant()}-after-the-f-swap.png", frame,
                                 what, subject, hull, null);
        }

        /// <summary>
        /// ⭐⭐ <b>PLATE SET 2 — the thirty through eight headings.</b> A sprite boat has eight drawn
        /// facings and a mesh hull has a continuous pose, so the one thing this set has to prove is that
        /// the picture MOVES: eight frames, eight distinct images, the drawn heading printed under each.
        ///
        /// <para>The boat root is turned and a frame is yielded. <c>MeshHullDriver.LateUpdate</c> reads
        /// <c>transform.up</c> directly with no easing and writes
        /// <c>IsoFacetHullRenderer.HeadingDirUnits</c>, and Update/LateUpdate both run at
        /// <c>Time.timeScale = 0</c> — so the frozen frame the stage parks on can still be re-posed.
        /// Physics cannot: <c>FixedUpdate</c> does not run frozen, so nothing fights the rotation.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheThirty_ThroughEightHeadings()
        {
            WharfNightStage.RequireAGraphicsDevice();
            BoatHullDef hull = LoadHull(Sloop30Name);

            yield return TheKeyAtMidday();

            DevBoatPicker picker = null;
            yield return FindTheScenesPicker(p => picker = p);

            Transform subject = null;
            yield return SwapTo(picker, hull, t => subject = t);

            Transform root = picker.transform;
            Quaternion asSheLay = root.rotation;

            yield return _stage.FrameOn(subject.position);
            FitTheFrame(1.9f * hull.LengthMeters, "she must stay in frame BEAM-ON as well as bow-on");

            var seen = new Dictionary<ulong, int>();
            var poses = new List<string>();

            for (int i = 0; i < 8; i++)
            {
                int deg = i * 45;
                root.rotation = Quaternion.Euler(0f, 0f, -deg);   // compass: 0 = bow up, increasing clockwise
                for (int f = 0; f < 3; f++) yield return null;    // driver LateUpdate -> renderer re-pose

                byte[] frame = _stage.Capture();
                string what = $"{Sloop30Name} heading {deg:000}°";
                AssertTheSubjectIsInFrame(subject, what);

                float units = DrawnDirUnits(subject);
                poses.Add($"{deg:000}° -> {units:0.000} dir units");

                ulong key = Fnv1a(frame);
                if (seen.TryGetValue(key, out int firstAt))
                    Assert.Fail($"[{PlateDir}] heading {deg:000}° rasterised the IDENTICAL image to " +
                                $"heading {firstAt * 45:000}°. A mesh hull that draws the same picture at " +
                                $"two headings is not being re-posed — the plate set would be eight copies " +
                                $"of one photograph. Drawn dir units so far: {string.Join(", ", poses)}.");
                seen[key] = i;

                if (CanComposite(subject, what))
                    SaveWithItsFrame($"2-sloop30-heading-{deg:000}.png", frame, what, subject, hull,
                                     $"drawn heading {units:0.000} dir units (1 unit = 45° CCW)");
            }

            Debug.Log($"[{PlateDir}] the thirty through eight headings, root z -> drawn pose: " +
                      string.Join("; ", poses) + $"; {seen.Count} of 8 frames distinct.");
            Assert.AreEqual(8, seen.Count, "eight headings must make eight different pictures");

            root.rotation = asSheLay;
        }

        /// <summary>
        /// ⭐ <b>PLATE SET 3 — the eighty-eight beside the Cape Islander, for scale.</b> The Cape is the
        /// biggest hull the owner has driven; the 88 is the workbench the sail lane will be built on.
        /// Both are laid parallel, one frame, the two lengths printed under it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheEightyEight_BesideTheCapeIslander_ForScale()
        {
            WharfNightStage.RequireAGraphicsDevice();
            BoatHullDef big  = LoadHull(Sloop88Name);
            BoatHullDef cape = LoadHull(CapeName);

            yield return TheKeyAtMidday();

            DevBoatPicker picker = null;
            yield return FindTheScenesPicker(p => picker = p);

            Transform subject = null;
            yield return SwapTo(picker, big, t => subject = t);

            // Abeam to starboard, laid parallel, a clear 3 m of water between the two hulls.
            Transform root = picker.transform;
            float gap = 0.5f * (big.LengthMeters + cape.LengthMeters) + 3f;
            Vector3 at = root.position + root.right * gap;

            var go = _stage.Track(new GameObject("TheCapeIslanderForScale"));
            go.SetActive(false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            go.transform.rotation = root.rotation;
            var controller = go.AddComponent<BoatController>();
            controller.SetHull(cape);
            go.SetActive(true);
            BoatHullSkinner.Apply(go, cape.Visual, controller);
            for (int f = 0; f < 4; f++) yield return null;

            Transform her = go.transform.Find(BoatHullSkinner.VisualChildName) ?? go.transform;

            Vector3 mid = 0.5f * (subject.position + her.position);
            yield return _stage.FrameOn(mid);
            FitTheFrame(gap + 0.5f * (big.LengthMeters + cape.LengthMeters) + 6f,
                        $"{gap:0.0} m between them, {big.LengthMeters:0.0} m and {cape.LengthMeters:0.0} m of boat");

            byte[] frame = _stage.Capture();
            const string what = "Sloop88 beside the Cape Islander";
            AssertTheSubjectIsInFrame(subject, what + " (the 88)");
            AssertTheSubjectIsInFrame(her, what + " (the Cape)");

            Camera cam = _stage.Camera;
            float spread = Mathf.Abs(cam.WorldToViewportPoint(subject.position).x -
                                     cam.WorldToViewportPoint(her.position).x);
            string caption =
                $"{Sloop88Name} {big.LengthMeters:0.0} m vs {CapeName} {cape.LengthMeters:0.0} m " +
                $"(ratio {big.LengthMeters / Mathf.Max(0.01f, cape.LengthMeters):0.00}×); " +
                $"laid {gap:0.0} m apart, {spread:0.000} of the frame's width between their centres";
            Debug.Log($"[{PlateDir}] {caption}.");

            if (CanComposite(subject, what) && CanComposite(her, what))
                SaveWithItsFrame("3-sloop88-beside-the-cape-islander.png", frame, what, subject, big, caption);
        }

        // =============================================================================================
        //  The region, the hour, the picker and the swap
        // =============================================================================================

        /// <summary>St Peters, loaded, off the title page, at midday.</summary>
        IEnumerator TheKeyAtMidday()
        {
            _introOnEntry = GameServices.OpeningCinematicRunning;
            _loadedAny = true;
            _stage = new WharfNightStage(SceneName, PlateDir);
            yield return _stage.Load();

            // ⚠️ St Peters IS the start scene: a fixture that loads it arrives at the TITLE PAGE, where
            // the world is drawn and the clock runs but every input is silently released. Reset() is a
            // slate wipe that publishes nothing; StartNewGame() would begin the arrival AND overwrite the
            // savegame that every worktree on this machine shares.
            if (ShellFlow.WorldInputBlocked)
            {
                Debug.Log($"[{PlateDir}] arrived at the shell's title page (phase {ShellFlow.Phase}); " +
                          "ShellFlow.Reset() — not StartNewGame(), which would clobber the shared savegame.");
                ShellFlow.Reset();
            }
            InteractionGate.Reset();

            yield return _stage.SetNight(PlateHour);
        }

        /// <summary>
        /// The scene's own picker — the component the F key drives. It may be installed by a dev core a
        /// few frames after the scene loads, so it is waited for; if it never appears the plate cannot be
        /// shot through the seam it is a plate OF, and that is a failure, not a skip.
        /// </summary>
        IEnumerator FindTheScenesPicker(System.Action<DevBoatPicker> found)
        {
            DevBoatPicker picker = null;
            for (int f = 0; f < 240 && picker == null; f++)
            {
                picker = Object.FindFirstObjectByType<DevBoatPicker>();
                if (picker == null) yield return null;
            }

            Assert.IsNotNull(picker,
                $"[{PlateDir}] {SceneName} carries no DevBoatPicker after 240 frames, so there is no " +
                "F-swap seam to photograph. These plates are of what the owner sees one press past the " +
                "end of the old list; a hull stood up by the fixture instead would be a picture of a " +
                "boat, not of the swap.");
            found(picker);
        }

        /// <summary>
        /// The swap itself: <see cref="DevBoatPicker.Show"/>, the call <c>Next()</c> makes for the F key.
        /// </summary>
        IEnumerator SwapTo(DevBoatPicker picker, BoatHullDef hull, System.Action<Transform> got)
        {
            picker.Show(hull);
            for (int f = 0; f < 6; f++) yield return null;   // skinner, driver, first posed LateUpdate

            Transform root = picker.transform;
            Transform visual = root.Find(BoatHullSkinner.VisualChildName);
            Assert.IsNotNull(visual,
                $"[{PlateDir}] after Show({hull.Id}) the picker's root carries no " +
                $"'{BoatHullSkinner.VisualChildName}' child, so nothing of her was skinned onto the boat.");

            Debug.Log($"[{PlateDir}] F-swap -> {hull.Id} ({hull.DisplayName}, {hull.LengthMeters:0.0} m); " +
                      $"root at {root.position}, visual '{visual.name}', " +
                      $"facet id {FacetId(visual)}, drawn {DrawnDirUnits(visual):0.000} dir units.");
            got(visual);
        }

        // =============================================================================================
        //  Naming the frame, and the two guards every plate passes
        // =============================================================================================

        /// <summary>
        /// Raise the zoom only far enough to hold <paramref name="worldWidth"/> metres, and never lower
        /// it: a plate must not be zoomed in past the look the scene itself is authored at. The change is
        /// named in the caption.
        /// </summary>
        void FitTheFrame(float worldWidth, string why)
        {
            Camera cam = _stage.Camera;
            float was = cam.orthographicSize;
            float need = 0.5f * worldWidth / Mathf.Max(0.01f, cam.aspect);
            if (need <= was)
            {
                Debug.Log($"[{PlateDir}] zoom left as the scene authored it: ortho {was:0.00} " +
                          $"({2f * was * cam.aspect:0.0} m across) already holds {worldWidth:0.0} m — {why}.");
                return;
            }

            cam.orthographicSize = need;
            Debug.Log($"[{PlateDir}] zoom WIDENED for this plate: ortho {was:0.00} -> {need:0.00} " +
                      $"({2f * need * cam.aspect:0.0} m across) — {why}.");
        }

        /// <summary>
        /// ⭐ <b>The frame this plate was shot through</b>, logged and written beside the picture. A
        /// number without its frame is not evidence.
        /// </summary>
        void SaveWithItsFrame(string fileName, byte[] frame, string what, Transform subject,
                              BoatHullDef hull, string extra)
        {
            Camera cam = _stage.Camera;
            var sb = new StringBuilder();
            sb.AppendLine(what);
            sb.AppendLine($"scene        {SceneName} (the dev key), hour {PlateHour:00.0}, " +
                          $"shell phase {ShellFlow.Phase}");
            sb.AppendLine($"hull         {hull.Id} \"{hull.DisplayName}\", {hull.LengthMeters:0.0} m, " +
                          $"visual {(hull.Visual != null ? hull.Visual.name : "<none>")}");
            sb.AppendLine($"camera       {cam.transform.position} ortho {cam.orthographicSize:0.00} " +
                          $"(≈{2f * cam.orthographicSize * cam.aspect:0.0} × " +
                          $"{2f * cam.orthographicSize:0.0} m), aspect {cam.aspect:0.000}");
            sb.AppendLine($"plate        {_stage.Width} × {_stage.Height} px, timeScale {Time.timeScale:0.0}");
            sb.AppendLine($"subject      {subject.name} at {subject.position}, facet id {FacetId(subject)}, " +
                          $"drawn {DrawnDirUnits(subject):0.000} dir units");
            sb.AppendLine("rig          NONE — she lands bare-poled: no boom, no sail, no steering in this branch");
            if (!string.IsNullOrEmpty(extra)) sb.AppendLine($"note         {extra}");

            _stage.SavePlate(fileName, frame);
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            File.WriteAllText(Path.Combine(dir, Path.ChangeExtension(fileName, ".txt")),
                              sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] SHOT THROUGH — {fileName}\n{sb}");
        }

        /// <summary>
        /// ⭐⭐ <b>THE PLATE MUST CONTAIN ITS SUBJECT.</b> Two independent reads, because either alone can
        /// be fooled: her own point must land inside the frame, AND something of hers must actually have
        /// rasterised. Copied deliberately from <c>HullsRideTheTidePlatePlayTests</c> — the law it
        /// encodes was bought by a capture that reported every hull's world position truthfully and had
        /// no boats in the picture.
        /// </summary>
        void AssertTheSubjectIsInFrame(Transform subject, string what)
        {
            Camera cam = _stage.Camera;
            Vector3 vp = cam.WorldToViewportPoint(subject.position);
            var renderers = subject.GetComponentsInChildren<Renderer>(true);
            int visible = renderers.Count(r => r != null && r.enabled && r.isVisible);

            string diagnosis =
                $"[{PlateDir}] {what}: subject at world {subject.position}, viewport " +
                $"({vp.x:0.000}, {vp.y:0.000}, z {vp.z:0.00}); camera at {cam.transform.position} " +
                $"ortho {cam.orthographicSize:0.00} aspect {cam.aspect:0.000}; " +
                $"{visible} of {renderers.Length} renderers visible";
            Debug.Log(diagnosis);

            Assert.Greater(vp.z, 0f, $"the subject is BEHIND the camera. {diagnosis}");
            Assert.That(vp.x, Is.InRange(0.02f, 0.98f),
                $"the subject is off the side of the frame — this plate would be a picture of the key " +
                $"without her at it. {diagnosis}");
            Assert.That(vp.y, Is.InRange(0.02f, 0.98f),
                $"the subject is off the top or bottom of the frame. {diagnosis}");
            Assert.Greater(renderers.Length, 0,
                $"the subject carries no renderer at all, so there is nothing of her to photograph. {diagnosis}");
            Assert.Greater(visible, 0,
                $"nothing of the subject rasterised into the frame that is about to be saved as " +
                $"evidence of her. {diagnosis}");
        }

        /// <summary>
        /// ⚠️ <b>A mesh hull holding facet id 0 is not in the picture</b> even though her renderers are
        /// enabled, inside the frustum and <c>isVisible</c>. The compositor has 255 ids at 1 + 12 a hull;
        /// the pool recycles on unregister, but a run that has already stood up a few dozen hulls can
        /// still starve it. The frame is DISCARDED with the reason named rather than saved as a picture
        /// of an empty key.
        /// </summary>
        static bool CanComposite(Transform subject, string what)
        {
            if (subject == null) return false;
            var facet = subject.GetComponentInChildren<IsoFacetHullRenderer>(true);
            if (facet == null) return true;          // a sprite hull composites by drawing; nothing to check
            if (facet.HullId > 0) return true;

            Debug.LogWarning(
                $"[{PlateDir}] NO PLATE WRITTEN for {what}: this hull holds facet id 0, so the " +
                "compositor has no place for her and the frame would show the key without her at it. " +
                "Re-shoot this fixture in a FRESH editor, on its own filter.");
            return false;
        }

        // =============================================================================================
        //  Small readings
        // =============================================================================================

        static int FacetId(Transform t)
        {
            var facet = t != null ? t.GetComponentInChildren<IsoFacetHullRenderer>(true) : null;
            return facet != null ? facet.HullId : -1;
        }

        static float DrawnDirUnits(Transform t)
        {
            var facet = t != null ? t.GetComponentInChildren<IsoFacetHullRenderer>(true) : null;
            return facet != null ? facet.HeadingDirUnits : float.NaN;
        }

        /// <summary>FNV-1a over the raw frame: two headings that hash the same drew the same picture.</summary>
        static ulong Fnv1a(byte[] bytes)
        {
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < bytes.Length; i++) { h ^= bytes[i]; h *= 1099511628211UL; }
            return h;
        }

        static BoatHullDef LoadHull(string name) => LoadCommitted<BoatHullDef>($"{BoatsFolder}/{name}.asset");

        static T LoadCommitted<T>(string path) where T : ScriptableObject
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"missing {path}");
            return asset;
#else
            Assert.Ignore("Needs the AssetDatabase: these plates are of the REAL committed hulls.");
            return null;
#endif
        }

        // =============================================================================================
        //  DontDestroyOnLoad bookkeeping (#764)
        // =============================================================================================

        static Scene PersistentScene()
        {
            var probe = new GameObject("__ddolProbe");
            Object.DontDestroyOnLoad(probe);
            Scene s = probe.scene;
            Object.DestroyImmediate(probe);
            return s;
        }

        static List<GameObject> PersistentRoots()
        {
            Scene s = PersistentScene();
            return s.IsValid() ? s.GetRootGameObjects().ToList() : new List<GameObject>();
        }
    }
}
