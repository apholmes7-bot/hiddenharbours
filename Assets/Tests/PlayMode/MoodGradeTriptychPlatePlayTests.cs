using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE OWNER RULES ON THE NUMBERS FROM THE PLATES</b> — the evidence for the grade tone-down
    /// (his 2026-09-09 playtest: <i>"a very blue filter at night and very noticeable yellow filter
    /// before and after … it completely washes everything out on the screen except the colour"</i>).
    ///
    /// <para>Five <b>TRIPTYCHS</b>, each one frame photographed three ways with the SAME camera at the
    /// SAME pinned minute:</para>
    /// <list type="number">
    /// <item><b>OFF</b> — <c>GradeEnabled = false</c>. The Volume is disabled and the camera's post flag
    /// goes back off, so this is the genuine pre-juice frame: the <c>DayNightProfile</c> multiply and
    /// nothing else.</item>
    /// <item><b>TODAY</b> — the grade as it ships on <c>main</c> right now: the pre-PR night and
    /// golden-hour keys (cold <c>ColorFilter</c>, cold Lift/Gamma/Gain, Sat −15, vignette 0.30 / warm
    /// filter, warm Lift/Gain, vignette 0.22), <c>GradeGoldenHourWidthHours 1.5</c>, and full strength —
    /// because before this PR there was no strength dial at all.</item>
    /// <item><b>PROPOSED</b> — the shipped <c>Resources/MoodGradeProfile.asset</c> and the shipped
    /// <c>GameConfig.asset</c> exactly as this PR commits them.</item>
    /// </list>
    ///
    /// <para><b>What is staged and what is production.</b> Only the two INPUTS are staged: a runtime
    /// <see cref="Object.Instantiate(Object)"/> copy of the config carrying that arm's
    /// <see cref="JuiceSettings"/>, and (for the TODAY arm only) a runtime copy of the profile with the
    /// two pre-PR keys put back. Nothing on disk is touched. The blend, the strength choice, the volume
    /// write and the render are all the shipped path — <see cref="MoodGradeDirector.Tick"/> is called and
    /// its own <see cref="MoodGradeDirector.LastStrength"/> is asserted, so a plate cannot be a plate of
    /// a grade the game would not actually apply.</para>
    ///
    /// <para><b>The subject of a grade plate is the graded frame.</b> So each arm asserts, before its
    /// pixels are believed: the director exists, the tick actually GRADED (a fixture camera, an
    /// unreported region or the Null device all leave the frame alone and would make three identical
    /// panels), the strength in force is the arm's, and there is a world in the picture (sprites inside
    /// the viewport, and non-zero local contrast). Then the three arms are asserted to be
    /// <b>different from each other</b> — if OFF and PROPOSED came back byte-identical the triptych
    /// would prove nothing at all.</para>
    ///
    /// <para><b>The numbers beside each panel</b> are computed on the display-referred (gamma) pixels the
    /// owner actually sees: <b>mean luminance</b> (Rec.709), <b>mean chroma</b> (max−min per pixel — how
    /// much colour is in the frame), and, because his word was <i>"washes everything out"</i>,
    /// <b>relative local contrast</b> (mean neighbour-to-neighbour luminance step ÷ mean luminance),
    /// which is the metric for washed-out; variance is not. Mean R/G/B are printed too, because a blue
    /// or yellow cast is a number, not an opinion.</para>
    ///
    /// <para>Needs a GPU: skips loudly as NOT VERIFIED on CI's Null device rather than reading green.</para>
    /// </summary>
    public class MoodGradeTriptychPlatePlayTests
    {
        const string StPeters = "StPeters";
        const string NineMileCreek = "NineMileCreek";
        const string PlateDir = "grade-triptych";
        const string CleanupSceneName = "GradeTriptychCleanup";

        /// <summary>Panel height in px. The WIDTH is derived from the camera's own aspect — the whole-frame
        /// multiply is fitted to <c>orthographicSize × aspect</c>, and a plate shot at any other aspect
        /// comes back with the light inset in it as a rectangle.</summary>
        const int PanelHeightPx = 600;

        /// <summary>Gutter between panels, in px.</summary>
        const int GutterPx = 10;

        // ---- what "today" ships as, on main, before this PR ------------------------------------------
        // Captured from the diff of Assets/_Project/Code/Art/MoodGradeProfile.cs in this PR. Day, Fog and
        // Storm are NOT listed because this PR does not touch them: the TODAY arm reuses the shipped
        // asset's own Day/Fog/Storm and puts back only the two keys that moved.

        static MoodGrade TodayNight()
        {
            var g = MoodGrade.Neutral;
            g.BloomIntensity = 0.8f; g.BloomThreshold = 0.75f; g.BloomScatter = 0.8f;
            g.Lift = new Vector4(0.90f, 0.95f, 1.10f, -0.02f);
            g.Gamma = new Vector4(0.95f, 0.98f, 1.05f, 0f);
            g.Gain = new Vector4(0.95f, 0.98f, 1.05f, 0f);
            g.Contrast = 10f; g.Saturation = -15f;
            g.ColorFilter = new Color(0.90f, 0.94f, 1.00f, 1f);
            g.VignetteIntensity = 0.30f; g.VignetteSmoothness = 0.5f;
            g.VignetteColor = new Color(0.02f, 0.03f, 0.08f, 1f);
            return g;
        }

        static MoodGrade TodayGoldenHour()
        {
            MoodGrade g = ShippedGoldenHour();          // start from what ships, then put the hue back
            g.Lift = new Vector4(1.06f, 1.00f, 0.92f, 0.02f);
            g.Gain = new Vector4(1.05f, 0.98f, 0.90f, 0.00f);
            g.ColorFilter = new Color(1.00f, 0.95f, 0.86f, 1f);
            g.VignetteIntensity = 0.22f;
            g.VignetteColor = new Color(0.12f, 0.06f, 0.02f, 1f);
            return g;
        }

        /// <summary>The golden key as the shipped asset carries it — read, not re-typed, so the TODAY arm
        /// differs from PROPOSED in exactly the five fields this PR moved and in nothing else.</summary>
        static MoodGrade ShippedGoldenHour()
        {
            MoodGradeProfile shipped = Resources.Load<MoodGradeProfile>("MoodGradeProfile");
            return shipped != null ? shipped.GoldenHour : MoodGradeProfile.DefaultGoldenHour();
        }

        /// <summary>The golden window as it shipped before this PR — three hours of yellow a day.</summary>
        const float TodayGoldenWidthHours = 1.5f;

        enum Arm { Off, Today, Proposed }

        readonly List<Object> _spawned = new List<Object>();
        readonly List<Object> _runtimeCopies = new List<Object>();
        Camera _cam;
        RenderTexture _rt;
        int _w, _h;
        GameConfig _configOnEntry;
        MoodGradeProfile _profileOnEntry;
        bool _introOnEntry;
        string _sceneLoaded;

        // =============================================================================================
        //  Teardown — every static this fixture leans on is put back
        // =============================================================================================

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;

            // ⚠ STATICS. Left where a plate wants them these stop, un-grade or mis-strength every later
            // test in the run: timeScale 0 freezes the world, a cloned config outlives the fixture that
            // made it, and the intro fact would leave the intro dial standing in ordinary play.
            Time.timeScale = 1f;
            if (GameServices.Clock != null) GameServices.Clock.TimeScale = 1f;
            GameServices.OpeningCinematicRunning = _introOnEntry;
            if (_configOnEntry != null) GameServices.Config = _configOnEntry;
            _configOnEntry = null;

            MoodGradeDirector dir = MoodGradeDirector.Instance;
            if (dir != null && _profileOnEntry != null) SetDirectorProfile(dir, _profileOnEntry);
            _profileOnEntry = null;

            if (_cam != null) { _cam.targetTexture = null; _cam = null; }
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            foreach (Object o in _runtimeCopies) if (o != null) Object.DestroyImmediate(o);
            _runtimeCopies.Clear();

            // ⚠⚠ LOOK THE CLEANUP SCENE UP BEFORE CREATING IT — CreateScene THROWS on a name that already
            // exists, and this teardown also runs after a case that SKIPPED before loading a region (on
            // CI, with no graphics device, that is every case). A teardown that can throw turns a fixture
            // that should have cost CI nothing into a red PR.
            Scene clean = SceneManager.GetSceneByName(CleanupSceneName);
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene(CleanupSceneName);
            if (clean.IsValid()) SceneManager.SetActiveScene(clean);
            if (!string.IsNullOrEmpty(_sceneLoaded))
            {
                for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                {
                    Scene s = SceneManager.GetSceneAt(i);
                    if (s.IsValid() && s != clean && s.name == _sceneLoaded)
                        yield return SceneManager.UnloadSceneAsync(s);
                }
                _sceneLoaded = null;
            }
        }

        // =============================================================================================
        //  The five triptychs
        // =============================================================================================

        /// <summary>Plates 1 and 2 — St Peters, the wharf, at 23:00 (the "very blue filter at night") and
        /// at 19:30 (half an hour off sunset: the "very noticeable yellow filter … after").</summary>
        [UnityTest]
        public IEnumerator StPetersWharf_At2300_And1930_ShowsWhatEachGradeDoesToTheFrame()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            yield return OpenTheLens();

            yield return Triptych("plate-1-stpeters-2300", StPeters, 23.0f, intro: false);
            yield return Triptych("plate-2-stpeters-1930", StPeters, 19.5f, intro: false);
        }

        /// <summary>Plates 3 and 4 — Nine Mile Creek at the same two hours. A second region because the
        /// grade takes a REGION OVERRIDE, and a tone-down proved in one place only is a tone-down proved
        /// for one override.</summary>
        [UnityTest]
        public IEnumerator NineMileCreek_At2300_And1930_ShowsWhatEachGradeDoesToTheFrame()
        {
            RequireAGraphicsDevice();

            // ⚠⚠ NINE MILE CREEK OPENED ON ITS OWN IS A DEV SCENE, NOT THE GAME. DevRegionBootstrap.Start
            // seeds an editor-only dev core whenever GameServices.Ready is false: it DISABLES the scene's
            // standalone review camera and makes the dev core's "DevCamera" the live one — an ordinary
            // scene object that PersistentObject never promotes. MoodGradeDirector.MayGrade then refuses
            // it, and it is RIGHT to: that is not the camera the game grades. The bootstrap says so in its
            // own log — "Start from the St Peters scene for the real boot." So we do. St Peters seeds the
            // real persistent core; travelling to NMC afterwards is what a player does, the dev core
            // destroys itself inactive, and the plate is shot through the lens play actually uses.
            yield return LoadRegion(StPeters);
            yield return LoadRegion(NineMileCreek);
            yield return OpenTheLens();

            yield return Triptych("plate-3-ninemilecreek-2300", NineMileCreek, 23.0f, intro: false);
            yield return Triptych("plate-4-ninemilecreek-1930", NineMileCreek, 19.5f, intro: false);
        }

        /// <summary>
        /// Plate 5 — <b>the arrival opening, at its own hour</b>. The opening runs at the clock's own
        /// start hour (<c>GameClock._startHour</c> = 6 in the shipped scene), which is sunrise to the
        /// minute, so the intro is the one frame that sits at the very centre of the golden key. This is
        /// the plate the owner's <i>"i do like it on the intro maybe toned down a little though"</i> is
        /// about, and the PROPOSED arm here is the only one of the five shot at
        /// <c>GradeIntroStrength</c> rather than <c>GradeStrength</c> — asserted, not assumed.
        /// </summary>
        [UnityTest]
        public IEnumerator TheArrivalOpening_AtItsOwnHour_ShowsTheIntroStrengthNotThePlayStrength()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            yield return OpenTheLens();

            yield return Triptych("plate-5-arrival-opening-0600", StPeters, IntroHour(), intro: true);
        }

        /// <summary>The hour the opening is played at: the shipped clock's own start hour.</summary>
        static float IntroHour()
        {
            var clock = Object.FindFirstObjectByType<HiddenHarbours.Environment.GameClock>();
            if (clock == null) return 6f;
            var f = typeof(HiddenHarbours.Environment.GameClock)
                    .GetField("_startHour", BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null ? (float)f.GetValue(clock) : 6f;
        }

        // =============================================================================================
        //  One triptych: pin the minute, re-aim, freeze, then three arms through the shipped director
        // =============================================================================================

        IEnumerator Triptych(string plateName, string region, float hour, bool intro)
        {
            // ⚠ A PLATE ARM RE-AIMS AFTER EVERY CLOCK MOVE. Seeking runs the world on: the follow drifts,
            // the framing policy re-asserts orthographicSize, and a plate shot without re-settling is a
            // plate of a camera that is still moving.
            yield return SeekTo(hour);
            yield return SettleCamera(_cam);
            FreezeTheWorld();

            MoodGradeDirector dir = MoodGradeDirector.Instance;
            Assert.IsNotNull(dir, "no MoodGradeDirector installed — there is no grade to photograph, and " +
                                  "three identical panels would read as 'the tone-down changed nothing'.");

            GameServices.OpeningCinematicRunning = intro;

            var arms = new List<(Arm arm, byte[] px, Metrics m)>();
            foreach (Arm arm in new[] { Arm.Off, Arm.Today, Arm.Proposed })
            {
                yield return StageArm(dir, arm, intro);

                float wantStrength = intro && arm == Arm.Proposed
                    ? GameServices.Config.Juice.GradeIntroStrength
                    : GameServices.Config.Juice.GradeStrength;

                if (arm == Arm.Off)
                {
                    Assert.IsFalse(dir.GradeIsOn,
                        "the OFF arm still had the grade volume enabled, so the 'before' panel is not a " +
                        "before at all.");
                }
                else
                {
                    Assert.IsTrue(dir.LastTickGraded,
                        $"[{plateName}/{arm}] the director declined to grade this frame (not the " +
                        "persistent camera, no region reported, or no graphics device). Every panel " +
                        "would come back identical and the triptych would be evidence of nothing.");
                    Assert.AreEqual(wantStrength, dir.LastStrength, 1e-4f,
                        $"[{plateName}/{arm}] the grade was applied at strength {dir.LastStrength}, not " +
                        $"the {wantStrength} this arm is a photograph of.");
                }

                byte[] px = Capture();
                Metrics m = Measure(px);
                arms.Add((arm, px, m));

                // ⚠ The OFF arm returns from Tick() BEFORE LastStrength/LastWeights are written, so
                // they still hold the previous arm's values. Printing them beside an ungraded panel would
                // caption it with a strength it was not shot at.
                string strengthSaid = arm == Arm.Off ? "none (grade off)" : dir.LastStrength.ToString("F2");
                string weightsSaid  = arm == Arm.Off ? "n/a" : dir.LastWeights.ToString();

                Debug.Log($"[{PlateDir}] {plateName} {arm,-8} strength {strengthSaid} | " +
                          $"mean luminance {m.Luma:F4} | mean chroma {m.Chroma:F4} | " +
                          $"relative local contrast {m.RelLocalContrast:F4} | " +
                          $"mean RGB ({m.R:F4}, {m.G:F4}, {m.B:F4}) | weights {weightsSaid}");
                TestContext.Out.WriteLine(
                    $"| {plateName} | {arm} | {strengthSaid} | {m.Luma:F4} | {m.Chroma:F4} | " +
                    $"{m.RelLocalContrast:F4} | {m.R:F4} | {m.G:F4} | {m.B:F4} |");
            }

            // ---- the subject is in frame, and the three panels are three different pictures ----------
            Assert.Greater(arms[0].m.RelLocalContrast, 0.001f,
                $"[{plateName}] the OFF frame has essentially no local contrast — the camera is looking " +
                "at a flat void, so nothing below is a photograph of the world.");
            int inFrame = SpritesInFrame();
            Assert.Greater(inFrame, 20,
                $"[{plateName}] only {inFrame} sprites are inside the viewport — the camera is not " +
                "pointed at the place this plate is captioned with.");

            AssertDifferent(plateName, arms[0], arms[1]);
            AssertDifferent(plateName, arms[0], arms[2]);
            AssertDifferent(plateName, arms[1], arms[2]);

            Debug.Log($"[{PlateDir}] {plateName} SHOT FROM region '{dir.LastRegionId}' " +
                      $"({region}) at hour {hour:F2}, camera ({_cam.transform.position.x:0.0}, " +
                      $"{_cam.transform.position.y:0.0}) ortho {_cam.orthographicSize:0.00}, " +
                      $"{_w}x{_h} per panel, {inFrame} sprites in frame");

            SaveTriptych(plateName, arms);
        }

        void AssertDifferent(string plate, (Arm arm, byte[] px, Metrics m) a, (Arm arm, byte[] px, Metrics m) b)
        {
            long diff = 0;
            for (int i = 0; i < a.px.Length; i += 4)
                diff += Mathf.Abs(a.px[i] - b.px[i]) + Mathf.Abs(a.px[i + 1] - b.px[i + 1])
                        + Mathf.Abs(a.px[i + 2] - b.px[i + 2]);
            double perChannel = diff / (double)(a.px.Length / 4 * 3);
            Assert.Greater(perChannel, 0.25,
                $"[{plate}] the {a.arm} and {b.arm} panels differ by only {perChannel:F4}/255 per channel " +
                "— they are the same picture, so this triptych cannot show the owner anything.");
        }

        // =============================================================================================
        //  Staging one arm — only the INPUTS are staged; the write is the shipped path
        // =============================================================================================

        IEnumerator StageArm(MoodGradeDirector dir, Arm arm, bool intro)
        {
            JuiceSettings juice = _configOnEntry.Juice;      // the SHIPPED juice is the starting point

            switch (arm)
            {
                case Arm.Off:
                    juice.GradeEnabled = false;
                    SetDirectorProfile(dir, _profileOnEntry);
                    break;

                case Arm.Today:
                    juice.GradeEnabled = true;
                    // Before this PR there was no strength dial at all — the grade was always full.
                    juice.GradeStrength = 1f;
                    juice.GradeIntroStrength = 1f;
                    juice.GradeGoldenHourWidthHours = TodayGoldenWidthHours;
                    SetDirectorProfile(dir, TodaysProfile());
                    break;

                case Arm.Proposed:
                    // untouched: exactly what GameConfig.asset and MoodGradeProfile.asset carry in this PR
                    SetDirectorProfile(dir, _profileOnEntry);
                    break;
            }

            GameConfig staged = Object.Instantiate(_configOnEntry);
            staged.name = $"GameConfig (plate {arm})";
            staged.hideFlags = HideFlags.HideAndDontSave;
            staged.Juice = juice;
            _runtimeCopies.Add(staged);
            GameServices.Config = staged;

            GameServices.OpeningCinematicRunning = intro;
            dir.Tick();

            // Let the volume system carry the new overrides into the frame before it is read back.
            for (int i = 0; i < 4; i++) yield return null;
        }

        /// <summary>A runtime copy of the SHIPPED profile with the two pre-PR keys put back. Instantiate,
        /// never the asset: a plate must not be able to rewrite what it is a photograph of.</summary>
        MoodGradeProfile TodaysProfile()
        {
            MoodGradeProfile today = Object.Instantiate(_profileOnEntry);
            today.name = "MoodGradeProfile (as main ships it)";
            today.hideFlags = HideFlags.HideAndDontSave;
            SetPrivate(today, "_night", TodayNight());
            SetPrivate(today, "_goldenHour", TodayGoldenHour());
            _runtimeCopies.Add(today);
            return today;
        }

        static void SetPrivate(MoodGradeProfile p, string field, MoodGrade value)
        {
            FieldInfo f = typeof(MoodGradeProfile).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"MoodGradeProfile.{field} is gone — this plate stages the pre-PR look " +
                                "through it, so it cannot be shot until the staging is moved with it.");
            f.SetValue(p, value);
        }

        static void SetDirectorProfile(MoodGradeDirector dir, MoodGradeProfile p)
        {
            FieldInfo f = typeof(MoodGradeDirector).GetField("_profile", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, "MoodGradeDirector._profile is gone — the TODAY arm is staged through it.");
            f.SetValue(dir, p);
        }

        // =============================================================================================
        //  The numbers
        // =============================================================================================

        struct Metrics
        {
            public double Luma, Chroma, RelLocalContrast, R, G, B;
        }

        /// <summary>Measured on the DISPLAY-referred bytes — the values that reach the owner's screen,
        /// not the linear buffer behind them.</summary>
        Metrics Measure(byte[] rgba)
        {
            int n = _w * _h;
            double sr = 0, sg = 0, sb = 0, sl = 0, sc = 0;
            var luma = new float[n];

            for (int i = 0; i < n; i++)
            {
                float r = rgba[i * 4 + 0] / 255f;
                float g = rgba[i * 4 + 1] / 255f;
                float b = rgba[i * 4 + 2] / 255f;
                float l = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                luma[i] = l;
                sr += r; sg += g; sb += b; sl += l;
                sc += Mathf.Max(r, Mathf.Max(g, b)) - Mathf.Min(r, Mathf.Min(g, b));
            }

            // ⚠ "Washed out" is LOCAL CONTRAST, not variance: a frame can keep a wide histogram while
            // every neighbouring pair of pixels flattens into the same milk.
            double step = 0; long pairs = 0;
            for (int y = 0; y < _h; y++)
            {
                int row = y * _w;
                for (int x = 1; x < _w; x++) { step += Mathf.Abs(luma[row + x] - luma[row + x - 1]); pairs++; }
            }
            double meanLuma = sl / n;
            double meanStep = pairs > 0 ? step / pairs : 0.0;

            return new Metrics
            {
                Luma = meanLuma,
                Chroma = sc / n,
                RelLocalContrast = meanLuma > 1e-6 ? meanStep / meanLuma : 0.0,
                R = sr / n, G = sg / n, B = sb / n,
            };
        }

        // =============================================================================================
        //  Region, clock, camera, capture — the house pattern
        // =============================================================================================

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device (Null Device), so nothing " +
                              "rendered and nothing was proved. Expected on CI; a plate needs a GPU.");
        }

        IEnumerator LoadRegion(string sceneName)
        {
            _introOnEntry = GameServices.OpeningCinematicRunning;

            // What the LIVE core had published, if anything. ALL FOUR, because the dev core republishes
            // all four and its teardown takes back every one it recognises (GameRoot.OnDestroy).
            GameConfig configBefore = GameServices.Config;
            IGameClock clockBefore = GameServices.Clock;
            IEnvironmentService envBefore = GameServices.Environment;
            IWallet walletBefore = GameServices.Wallet;

            LogAssert.ignoreFailingMessages = true;      // the regions log decor complaints of their own
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            _sceneLoaded = sceneName;
            for (int i = 0; i < 8; i++) yield return null;   // let the self-installing components register

            yield return WaitForTheGrader(sceneName);

            // ⚠🔴 A DEFECT THIS PLATE MUST NOT PHOTOGRAPH — reported in the PR, not this lane's to fix.
            // Arriving in a region whose scene carries a dev core (NineMileCreek and Greywick do),
            // RegionTravelCoordinator.OnActiveSceneChanged calls SetSceneRootsActive(next, true) — which
            // activates the DevCore root NineMileCreekBuilder deliberately left INACTIVE. That dev
            // GameRoot.Awake (GameRoot.cs:58-69) publishes its OWN clock, environment and wallet over the
            // live core's and republishes the SAME GameConfig asset; DevRegionBootstrap.Start then tears
            // the dev core down, and GameRoot.OnDestroy takes back every slot it still recognises — so
            // Clock, Environment, Wallet and Config all end up NULL. Nothing puts them back.
            // Play reaches this identically: RegionSceneLoader loads additively and calls SetActiveScene,
            // which fires the same handler.
            //
            // With Config null the director grades on JuiceSettings DEFAULTS and with Clock null the hour
            // cannot be pinned, so the plate would be a picture of that defect rather than of the
            // tone-down. Put the live core's services back, and say out loud that we did.
            var healed = new List<string>();
            if (GameServices.Config == null && configBefore != null)
            {
                GameServices.Config = configBefore;
                healed.Add($"Config '{configBefore.name}'");
            }
            if (GameServices.Clock == null && clockBefore != null)
            {
                GameServices.Clock = clockBefore;
                healed.Add("Clock");
            }
            if (GameServices.Environment == null && envBefore != null)
            {
                GameServices.Environment = envBefore;
                healed.Add("Environment");
            }
            if (GameServices.Wallet == null && walletBefore != null)
            {
                GameServices.Wallet = walletBefore;
                healed.Add("Wallet");
            }
            if (healed.Count > 0)
                Debug.Log($"[{PlateDir}] {sceneName}: arriving NULLED {string.Join(", ", healed)} — the " +
                          "region's dev core was activated over the live one and took the services down " +
                          "with it when it was destroyed. Re-published them so this plate photographs the " +
                          "GRADE and not that defect. (Production bug; see the PR.)");

            _configOnEntry = GameServices.Config;
            if (_configOnEntry == null)
                Assert.Ignore("SKIPPED — the region registered no GameConfig, so the shipped juice " +
                              "numbers this plate is evidence FOR are not in play.");

            MoodGradeDirector dir = MoodGradeDirector.Instance;
            if (dir == null)
                Assert.Ignore("SKIPPED — no MoodGradeDirector installed, so there is no grade to shoot.");
            _profileOnEntry = (MoodGradeProfile)typeof(MoodGradeDirector)
                .GetField("_profile", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dir);
            Assert.IsNotNull(_profileOnEntry, "the director loaded no MoodGradeProfile.");
        }

        /// <summary>
        /// Wait until the director's OWN gate facts are true before anything is photographed.
        ///
        /// ⚠ MayGrade wants a PERSISTENT camera, a reported region and a graphics device. A region scene
        /// opened without the persistent core gets an editor-only dev core instead (DevRegionBootstrap),
        /// whose DevCamera is never promoted — so the grade is refused and every panel comes back the same
        /// picture. This waits for production to finish booting and says out loud what it is waiting on.
        ///
        /// It registers none of these facts itself: the assertion that a frame was actually graded still
        /// comes from MoodGradeDirector.LastTickGraded, and the plate is the evidence.
        /// </summary>
        IEnumerator WaitForTheGrader(string sceneName)
        {
            for (int i = 0; i < 240 && !GateIsOpen(); i++) yield return null;

            Assert.IsTrue(GateIsOpen(),
                $"[{sceneName}] the grader's gate is SHUT — {GateSaid()}. MoodGradeDirector.MayGrade would " +
                "refuse every graded arm and the triptych would be three copies of one ungraded frame. If " +
                "the camera is a DevCamera, this region was opened WITHOUT the persistent core and " +
                "DevRegionBootstrap seeded its editor-only dev core: boot St Peters first and travel here.");
            Debug.Log($"[{PlateDir}] {sceneName}: the grader's gate is OPEN — {GateSaid()}");
        }

        /// <summary>The three facts MayGrade reads, asked of the camera the director is actually pointed at.</summary>
        static bool GateIsOpen()
            => MoodGradeDirector.IsPersistentCamera(GraderCamera())
               && !string.IsNullOrEmpty(GameServices.CurrentRegionId)
               && MoodGradeDirector.HasGraphicsDevice;

        /// <summary>Mirror of <c>MoodGradeDirector.ResolveCamera()</c>: the cached camera while it is alive, else Camera.main.</summary>
        static Camera GraderCamera()
        {
            MoodGradeDirector dir = MoodGradeDirector.Instance;
            if (dir == null) return Camera.main;
            var f = typeof(MoodGradeDirector).GetField("_camera", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, "MoodGradeDirector no longer caches a '_camera' — this wait is reading a " +
                                "field that has moved, so it would wave through a lens the grader never uses.");
            var cached = (Camera)f.GetValue(dir);
            return cached != null && cached.isActiveAndEnabled ? cached : Camera.main;
        }

        static string GateSaid()
        {
            Camera cam = GraderCamera();
            string where = cam == null ? "<no camera>" : $"'{cam.name}' in scene '{cam.gameObject.scene.name}'";
            string region = string.IsNullOrEmpty(GameServices.CurrentRegionId) ? "<none>" : GameServices.CurrentRegionId;
            return $"grader camera {where} (persistent {MoodGradeDirector.IsPersistentCamera(cam)}), " +
                   $"region '{region}', graphics device {MoodGradeDirector.HasGraphicsDevice}";
        }

        IEnumerator SeekTo(float hour)
        {
            if (GameServices.Clock == null || GameServices.Config == null)
            {
                Assert.Ignore("SKIPPED — the region registered no clock, so the hour cannot be pinned and " +
                              "the frame would be graded at whatever moment the run happened to be in.");
                yield break;
            }
            Time.timeScale = 1f;
            GameServices.Clock.TimeScale = 1f;
            double spd = GameServices.Config.SecondsPerDay;
            GameServices.Clock.SeekTo((1.0 + hour / 24.0) * spd);

            // ⚠ LET THE CLOCK RUN after the seek: DayNightController publishes on init and thereafter
            // follows a MOVING clock, so seeking and stopping in one breath leaves the frame wearing
            // whatever hour the scene loaded at — which for these plates is the whole subject.
            for (int i = 0; i < 12; i++) yield return null;
        }

        void FreezeTheWorld()
        {
            // Both clocks: the game clock so the hour cannot drift off the caption, and engine time so the
            // swell (which animates on ENGINE time) does not move most of the frame between two arms.
            if (GameServices.Clock != null) GameServices.Clock.TimeScale = 0f;
            Time.timeScale = 0f;
        }

        /// <summary>Settle the camera, then attach the render texture and leave it attached.</summary>
        IEnumerator OpenTheLens()
        {
            // ⚠ THE LENS MUST BE THE ONE THE GRADE IS APPLIED THROUGH. A region can carry its own enabled
            // camera tagged MainCamera (NineMileCreek ships a review camera as well as the dev one), so
            // Camera.main is not reliably the camera MoodGradeDirector resolved. Photographing through a
            // different camera would produce three identical panels and blame the tone-down for it.
            Camera cam = GraderCamera();
            Assert.IsNotNull(cam, "no main camera — there is nothing to photograph the world with");
            Assert.IsTrue(MoodGradeDirector.IsPersistentCamera(cam),
                $"the lens is {cam.name} in scene '{cam.gameObject.scene.name}', not the persistent core's " +
                "camera — the grade would not be applied to the frame this plate captures.");
            yield return SettleCamera(cam);

            _cam = cam;
            _h = PanelHeightPx;
            _w = Mathf.RoundToInt(_h * _cam.aspect);
            _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
            _rt.Create();

            // ⚠⚠ Attach the target BEFORE the world stops and LEAVE IT ATTACHED: a camera's aspect changes
            // the moment a render texture is attached, and the whole-frame overlay refits to it only while
            // frames are running.
            _cam.targetTexture = _rt;
            for (int i = 0; i < 8; i++) yield return null;
        }

        static IEnumerator SettleCamera(Camera cam)
        {
            Vector3 lastPos = cam.transform.position;
            float lastSize = cam.orthographicSize;
            int still = 0, frames = 0;
            while (still < 5 && frames < 400)
            {
                yield return null;
                frames++;
                bool moved = (cam.transform.position - lastPos).sqrMagnitude > 1e-8f
                             || Mathf.Abs(cam.orthographicSize - lastSize) > 1e-5f;
                still = moved ? 0 : still + 1;
                lastPos = cam.transform.position;
                lastSize = cam.orthographicSize;
            }
            Assert.Less(frames, 400, "the camera never stopped moving, so the overlay it is fitted to would " +
                                     "be a frame behind every panel");
        }

        int SpritesInFrame()
        {
            int inFrame = 0;
            foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy) continue;
                Vector3 v = _cam.WorldToViewportPoint(sr.transform.position);
                if (v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) inFrame++;
            }
            return inFrame;
        }

        byte[] Capture()
        {
            _cam.Render();

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_w, _h, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prevActive;

            // ⚠ The project renders in LINEAR colour space, so a raw float read-back saves far too dark.
            Color[] px = tex.GetPixels();
            var outBytes = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++)
            {
                Color c = new Color(Mathf.Clamp01(px[i].r), Mathf.Clamp01(px[i].g),
                                    Mathf.Clamp01(px[i].b), 1f).gamma;
                outBytes[i * 4 + 0] = (byte)Mathf.RoundToInt(c.r * 255f);
                outBytes[i * 4 + 1] = (byte)Mathf.RoundToInt(c.g * 255f);
                outBytes[i * 4 + 2] = (byte)Mathf.RoundToInt(c.b * 255f);
                outBytes[i * 4 + 3] = 255;
            }
            Object.DestroyImmediate(tex);
            return outBytes;
        }

        /// <summary>Compose the three arms into ONE wide picture, left to right, OFF · TODAY · PROPOSED,
        /// so the owner rules on one image and not on three files he has to hold in his head.</summary>
        void SaveTriptych(string name, List<(Arm arm, byte[] px, Metrics m)> arms)
        {
            int panels = arms.Count;
            int width = panels * _w + (panels - 1) * GutterPx;
            var canvas = new byte[width * _h * 4];

            for (int i = 0; i < canvas.Length; i += 4)
            {
                canvas[i] = 24; canvas[i + 1] = 24; canvas[i + 2] = 26; canvas[i + 3] = 255;
            }

            for (int p = 0; p < panels; p++)
            {
                int x0 = p * (_w + GutterPx);
                byte[] src = arms[p].px;
                for (int y = 0; y < _h; y++)
                {
                    int srcRow = y * _w * 4;
                    int dstRow = (y * width + x0) * 4;
                    System.Array.Copy(src, srcRow, canvas, dstRow, _w * 4);
                }
            }

            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            var tex = new Texture2D(width, _h, TextureFormat.RGBA32, false, true);
            tex.LoadRawTextureData(canvas);
            tex.Apply(false, false);
            string path = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            Debug.Log($"[{PlateDir}] TRIPTYCH (OFF | TODAY | PROPOSED) -> {path}");
        }
    }
}
