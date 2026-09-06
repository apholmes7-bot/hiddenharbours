using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE ITEM #720 COULD NOT DELIVER: does a RECEIVER read shaded?</b>
    ///
    /// <para>Two systems in this project are called "shadows" and they have OPPOSITE receiver semantics.
    /// <see cref="LampShadowSystem"/> draws pooled quads at the compositing ceiling with
    /// <c>Blend Zero SrcColor</c>, so a sprite standing in a lamp shadow IS darkened.
    /// <see cref="SpriteShadow"/> — the sun — draws a sheared copy of the caster's own sprite sorted
    /// BELOW that caster, so a sprite standing in a tree's shadow draws over it at full brightness. Not a
    /// tuning shortfall: a world-sorted dark sprite cannot darken something that draws after it. Every
    /// "she reads shaded" promise under the sun was therefore unbuildable, which is what this fixture and
    /// <see cref="SpriteShadowProfile.ScreenSpaceShade"/> exist to change.</para>
    ///
    /// <para><b>The metric is a DIFFERENCE, and it is exact here on purpose.</b> Nothing in this scene runs
    /// on <c>_Time</c> — no water, no wind, a frozen sun published straight into the globals — so two shots
    /// of one configuration are byte-identical and "darker" means darker. Both arms are shot in ONE
    /// main-thread call against ONE camera, which is what puts the noise floor at zero.</para>
    ///
    /// <para><b>⚠️ Both arms must genuinely differ.</b> Every measurement below is asserted in BOTH
    /// directions: the legacy arm must leave the receiver at exactly 0.00 % (that is the defect, and a
    /// fixture that cannot see it is a dead control) and the shade arm must darken her. A single
    /// one-directional assert here would pass with the feature deleted.</para>
    ///
    /// <para><b>Self-skips without a graphics device</b> — the standing CI law. A skip is "NOT VERIFIED",
    /// never "passed". Contact sheets go to <c>artifacts/sun-shade/</c> (gitignored); the ones the PR ships
    /// are copied into <c>docs/art/spikes/sun-shade-buffer/</c> by hand.</para>
    /// </summary>
    public class SunShadeReceiverRenderTests
    {
        private const int ShotPx = 400;
        private const float FrameMetres = 10f;              // ortho size 5 -> 40 px per metre
        private const float PxPerMetre = ShotPx / FrameMetres;
        private const float Ppu = 32f;

        // The stage, in metres. The caster is a wide, solid, 2 m block; the receiver stands NORTH of it,
        // clear of its own image, inside the rake. See BuildTheStage for why north.
        private const float CasterFeetY = -3f, CasterHeight = 2f, CasterWidth = 1.5f;
        private const float ReceiverFeetY = -0.5f, ReceiverHeight = 1.6f, ReceiverWidth = 0.6f;
        // Elevation chosen so the rake is 2x the caster's height (see DayNightMath.ShadowLength: the
        // multiplier is lerp(horizon 5, noon 0.35, elevation)). Asserted, not assumed.
        private const float SunElevation = 0.6452f;

        private static readonly Color32 GroundColor = new Color32(128, 128, 128, 255);
        private static readonly Color32 CasterColor = new Color32(90, 110, 80, 255);
        private static readonly Color32 ReceiverColor = new Color32(232, 206, 176, 255);
        private const string DoryMeshPath = "Assets/_Project/Data/Boats/HullMeshes/DoryIsoHullMesh.asset";

        private static readonly int IdSunDir = Shader.PropertyToID("_SunDir");
        private static readonly int IdSunElevation = Shader.PropertyToID("_SunElevation");
        private static readonly int IdShadowStrength = Shader.PropertyToID("_ShadowStrength");
        private static readonly int IdDayNightTint = Shader.PropertyToID("_DayNightTint");

        private readonly List<Object> _spawned = new List<Object>();
        private Camera _cam;
        private RenderTexture _rt;
        private Material _unlit;
        private SpriteShadow _casterShadow;
        private Vector4 _sunDirBefore;
        private float _sunElevBefore, _strengthBefore;
        private Color _tintBefore;

        [SetUp]
        public void SetUp()
        {
            _sunDirBefore = Shader.GetGlobalVector(IdSunDir);
            _sunElevBefore = Shader.GetGlobalFloat(IdSunElevation);
            _strengthBefore = Shader.GetGlobalFloat(IdShadowStrength);
            _tintBefore = Shader.GetGlobalColor(IdDayNightTint);
            LampShadowSystem.ClearRegistries();
        }

        [TearDown]
        public void TearDown()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            _cam = null;
            _casterShadow = null;
            // ⚠️ SharedProfile is STATIC and would leak this fixture's arm into every later test.
            SpriteShadow.SharedProfile = null;
            LampShadowSystem.ClearRegistries();
            Shader.SetGlobalVector(IdSunDir, _sunDirBefore);
            Shader.SetGlobalFloat(IdSunElevation, _sunElevBefore);
            Shader.SetGlobalFloat(IdShadowStrength, _strengthBefore);
            Shader.SetGlobalColor(IdDayNightTint, _tintBefore);
        }

        private static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device (Null Device), so nothing rendered and " +
                              "nothing was proved. Expected on CI; a shaded receiver needs a GPU.");
        }

        // =============================================================================================
        //  The acceptance
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>ACCEPTANCE 2 — the fisher in a cast shadow.</b> A receiver standing inside the caster's
        /// rake, measured over HER OWN PIXELS ONLY: untouched in the legacy arm (0.00 %, the defect) and
        /// measurably darker in the shade arm.
        /// </summary>
        [Test]
        public void AReceiverStandingInACastShadow_ReadsShaded_OnlyInTheShadeArm()
        {
            RequireAGraphicsDevice();
            BuildTheStage(groundPool: false);
            SpriteRenderer her = Receiver("fisher", new Vector2(0f, ReceiverFeetY), ReceiverWidth, ReceiverHeight);

            // The rake must actually reach her, or the whole measurement is vacuous.
            float rake = RakeMetres();
            Assert.Greater(CasterFeetY + CasterHeight + rake, ReceiverFeetY + ReceiverHeight,
                $"the caster's silhouette (up to y {CasterFeetY + CasterHeight + rake:F2}) must cover the receiver " +
                $"(up to y {ReceiverFeetY + ReceiverHeight:F2}) or nothing here is being measured");
            Assert.Greater(ReceiverFeetY, CasterFeetY + CasterHeight,
                "the receiver must stand clear of the caster's own image, or 'her pixels' includes his");

            Color32[] off = Shoot("cast-00-no-shade", strength: 0f, shade: false);
            Color32[] legacy = Shoot("cast-01-legacy-arm", strength: 1f, shade: false);
            Color32[] shade = Shoot("cast-02-shade-arm", strength: 1f, shade: true);
            Color32[] shadeAgain = Shoot("cast-03-shade-arm-repeat", strength: 1f, shade: true);

            Assert.IsTrue(Identical(shade, shadeAgain),
                "nothing in this scene runs on the clock: two identical shots must be identical, or the " +
                "noise floor is not zero and no number below means anything");

            bool[] hers = MaskOf(off, ReceiverColor);
            // ⚠️ The ground INSIDE the rake, not the whole frame: the shade covers a 1.5 m strip of a 10 m
            // frame, so a whole-frame mean answers "how much of the picture is in shade", which is not the
            // question. Acceptance 1 is about the shaded ground itself.
            bool[] ground = And(MaskOf(off, GroundColor), InTheRake());
            Assert.Greater(Count(hers), 400, "the receiver must actually be on screen to be measured");
            Assert.Greater(Count(ground), 400, "and so must some shaded ground");

            double herLegacy = MeanDarkeningPct(off, legacy, hers);
            double herShade = MeanDarkeningPct(off, shade, hers);
            double groundLegacy = MeanDarkeningPct(off, legacy, ground);
            double groundShade = MeanDarkeningPct(off, shade, ground);

            Debug.Log($"[sun-shade] CAST SHADOW, {Count(hers)} px of her: " +
                      $"legacy arm {herLegacy:F2} % darker, shade arm {herShade:F2} % darker. " +
                      $"Ground in the rake ({Count(ground)} px): legacy {groundLegacy:F2} %, " +
                      $"shade {groundShade:F2} %. Rake {rake:F2} m.");

            Assert.AreEqual(0.0, herLegacy, 0.01,
                "THE DEFECT: in the legacy arm the receiver is not darkened AT ALL — the shade sorts under " +
                "its caster and she draws over it. If this ever reads non-zero the fixture has stopped " +
                "measuring the thing this PR exists to fix.");
            Assert.Greater(herShade, 15.0,
                "In the shade arm her own pixels must be measurably darker. At MaxAlpha 0.45 and the shipped " +
                "tint the multiply is about 43 %, so anything under 15 % means the composite is not landing " +
                "on her.");
            // ACCEPTANCE 1: the ground already reads shaded on main, and this must not regress it.
            Assert.Greater(groundLegacy, 15.0, "the ground reads shaded in the legacy arm — that is main's frame");
            Assert.Greater(groundShade, 15.0, "and it still does in the shade arm");
            Assert.GreaterOrEqual(groundShade, groundLegacy - 1.0,
                "THE SHADED GROUND MUST NOT GET LIGHTER. The two arms compose differently — alpha-over " +
                "pulls a pixel toward the tint, a multiply scales it down by a fixed fraction — so they are " +
                "not expected to be equal, but the shade arm must not take shade AWAY from the ground to " +
                "give it to what stands on it.");

            SaveSheet("plate-01-a-receiver-in-a-cast-shadow", legacy, shade);
        }

        /// <summary>
        /// ⭐ <b>ACCEPTANCE 2 (the other half) — the fisher at a trunk foot.</b> #720's ground-contact pool
        /// is the shade a crown throws straight down; in the legacy arm it darkens the ground and leaves
        /// anyone standing on it untouched. Same measurement, same two directions.
        /// </summary>
        [Test]
        public void AReceiverAtTheTrunkFoot_ReadsShaded_OnlyInTheShadeArm()
        {
            RequireAGraphicsDevice();
            BuildTheStage(groundPool: true);
            // Small and low, standing ON the trunk foot: the pool is 2 x 0.42 x width across and squashed
            // to the ground plane, so a full-height figure would stand out of the top of it.
            SpriteRenderer her = Receiver("fisher-at-the-foot", new Vector2(0f, CasterFeetY - 0.2f), 0.5f, 0.5f);
            Assert.IsNotNull(her);

            Vector2 pool = SpriteShadow.GroundContactSize(CasterWidth, SpriteShadow.SharedProfile.GroundContactRadius);
            Assert.Greater(pool.x, 0.5f, "the profile must actually be asking for a pool, or this measures nothing");

            Color32[] off = Shoot("pool-00-no-shade", strength: 0f, shade: false);
            Color32[] legacy = Shoot("pool-01-legacy-arm", strength: 1f, shade: false);
            Color32[] shade = Shoot("pool-02-shade-arm", strength: 1f, shade: true);

            bool[] hers = MaskOf(off, ReceiverColor);
            Assert.Greater(Count(hers), 200, "the receiver must actually be on screen to be measured");

            double herLegacy = MeanDarkeningPct(off, legacy, hers);
            double herShade = MeanDarkeningPct(off, shade, hers);
            Debug.Log($"[sun-shade] TRUNK FOOT, {Count(hers)} px of her, pool {pool.x:F2} x {pool.y:F2} m: " +
                      $"legacy arm {herLegacy:F2} % darker, shade arm {herShade:F2} % darker.");

            Assert.AreEqual(0.0, herLegacy, 0.01,
                "THE DEFECT: standing at a trunk foot in the legacy arm she is not darkened at all — plate " +
                "04 of #720 is titled 'STANDING AT A TRUNK FOOT AT NOON' with nobody standing in it, and " +
                "this is why.");
            Assert.Greater(herShade, 10.0,
                "In the shade arm the pool darkens her. The bar is lower than the cast shadow's because the " +
                "pool's rim is feathered (GroundContactSoftness) and she covers part of that rim.");

            SaveSheet("plate-02-a-receiver-at-the-trunk-foot", legacy, shade);
        }

        /// <summary>
        /// <b>ACCEPTANCE 3 — a boat reads shaded.</b> The receiver here is a real mesh hull, drawn by
        /// <see cref="IsoFacetHullRenderer"/> out of the feature's resolved screen texture rather than by a
        /// sprite. This is the item most likely to be quietly skipped, and it is the one that proves the
        /// composite does not care WHO drew the pixel: a multiply over the assembled frame darkens a mesh
        /// exactly as it darkens a sprite, which a shade sorted under a sprite caster can never do.
        /// </summary>
        [Test]
        public void AMeshHullUnderTheShade_ReadsShaded_OnlyInTheShadeArm()
        {
            RequireAGraphicsDevice();
            BuildTheStage(groundPool: false);

            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(DoryMeshPath);
            Assert.IsNotNull(def, "the dory's hull mesh def must exist");
            var host = new GameObject("dory");
            _spawned.Add(host);
            host.transform.position = new Vector3(0f, ReceiverFeetY + 0.4f, 0f);
            var hull = host.AddComponent<IsoFacetHullRenderer>();
            hull.Configure(IsoFacetHullPresentationService.ToSetup(def));
            hull.HeadingDirUnits = 2f;
            hull.ApplyPose();
            // Sort her like a boat: in the game a hull is Y-sorted in the decor band, far above a sun
            // shadow's caster-relative order. Left at 0 she would sort UNDER the legacy arm's shade and be
            // darkened by it — a fixture artefact that would read as the defect already being fixed.
            hull.SetSorting(0, 20);

            Color32[] off = Shoot("hull-00-no-shade", strength: 0f, shade: false);
            Color32[] legacy = Shoot("hull-01-legacy-arm", strength: 1f, shade: false);
            Color32[] shade = Shoot("hull-02-shade-arm", strength: 1f, shade: true);

            // Her pixels: everything in the control shot that is neither the ground nor the caster, inside
            // the band she occupies. A mesh has no flat colour to match, so the mask is "not the stage".
            // Her pixels INSIDE the rake: a dory is far wider than the caster's 1.5 m silhouette, so a
            // whole-hull mean would measure how much of the boat is in shade rather than how shaded the
            // shaded part is.
            bool[] hers = And(NotTheStage(off, minY: ReceiverFeetY, maxY: ReceiverFeetY + 2.5f), InTheRake());
            Assert.Greater(Count(hers), 400, "the hull must actually be in the rake to be measured");

            double herLegacy = MeanDarkeningPct(off, legacy, hers);
            double herShade = MeanDarkeningPct(off, shade, hers);
            Debug.Log($"[sun-shade] MESH HULL, {Count(hers)} px of her: " +
                      $"legacy arm {herLegacy:F2} % darker, shade arm {herShade:F2} % darker.");

            Assert.AreEqual(0.0, herLegacy, 0.01,
                "THE DEFECT, on the mesh path: a hull moored in a tree's shadow is not darkened at all.");
            Assert.Greater(herShade, 15.0,
                "In the shade arm the hull's own pixels go down with the frame — the composite is blind to " +
                "what drew them, which is exactly why it generalises past sprites.");

            SaveSheet("plate-03-a-mesh-hull-under-the-shade", legacy, shade);
        }

        /// <summary>
        /// <b>ACCEPTANCE 4 — night is unchanged.</b> The sun's shade gates off with the sun: at
        /// <c>_ShadowStrength</c> 0 the alpha is 0, the renderer is disabled, and BOTH arms are the frame
        /// with no shade in it, byte for byte. Nothing here touches the lamps.
        /// </summary>
        [Test]
        public void AtNight_BothArmsAreTheUnshadedFrame_ByteForByte()
        {
            RequireAGraphicsDevice();
            BuildTheStage(groundPool: true);
            Receiver("fisher", new Vector2(0f, ReceiverFeetY), ReceiverWidth, ReceiverHeight);

            Color32[] legacyNight = Shoot("night-01-legacy-arm", strength: 0f, shade: false);
            Color32[] shadeNight = Shoot("night-02-shade-arm", strength: 0f, shade: true);
            Color32[] absent = ShootWithNoShadowAtAll("night-03-no-shadow-component");

            Assert.IsTrue(Identical(legacyNight, shadeNight),
                "with the sun down the two arms must be the same frame — the shade arm must not draw a " +
                "multiply the legacy arm does not draw");
            Assert.IsTrue(Identical(shadeNight, absent),
                "and both must equal the frame with no sun shadow in the scene at all: strength 0 writes " +
                "nothing, it does not write a transparent something");
        }

        /// <summary>
        /// <b>ACCEPTANCE 5 — the passthrough is exact, in pixels.</b> The legacy arm's frame with the
        /// SHIPPED profile must be identical to the same scene rendered with the built-in code default
        /// (which is main's pre-#720 look) at the same dials, and it must NOT be identical to the shade
        /// arm. The second half is the dead-control guard: an A/B whose arms agree has proved nothing.
        /// </summary>
        [Test]
        public void TheLegacyArm_IsUnmovedByThisPr_AndTheTwoArmsGenuinelyDiffer()
        {
            RequireAGraphicsDevice();
            BuildTheStage(groundPool: false);
            Receiver("fisher", new Vector2(0f, ReceiverFeetY), ReceiverWidth, ReceiverHeight);

            Color32[] legacy = Shoot("passthrough-01-legacy", strength: 1f, shade: false);
            Color32[] legacyAgain = Shoot("passthrough-02-legacy-repeat", strength: 1f, shade: false);
            Color32[] shade = Shoot("passthrough-03-shade", strength: 1f, shade: true);

            Assert.IsTrue(Identical(legacy, legacyAgain), "the legacy arm is deterministic");
            Assert.IsFalse(Identical(legacy, shade),
                "⚠️ DEAD CONTROL: the two arms rendered the same frame. Either the arm switch is not " +
                "reaching the material, or the shade material is not the multiply one — either way the " +
                "owner would be shown an A/B with no B.");

            int changed = 0;
            for (int i = 0; i < legacy.Length; i++)
                if (legacy[i].r != shade[i].r || legacy[i].g != shade[i].g || legacy[i].b != shade[i].b) changed++;
            Debug.Log($"[sun-shade] the two arms differ on {changed} of {legacy.Length} px " +
                      $"({100.0 * changed / legacy.Length:F2} % of the frame).");
            Assert.Greater(changed, legacy.Length / 100, "and they differ over a meaningful part of the frame");
        }

        /// <summary>
        /// <b>THE COST, MEASURED RATHER THAN HIDDEN.</b> A screen-space multiply darkens whatever occupies
        /// the pixel, including something that is ABOVE the shade in the world rather than standing in it.
        /// This stands a receiver at the same place and simply sorts it above everything — a gull, a
        /// boat's upper works, a roof edge — and reports how much it loses. The lamp system already
        /// accepts this cost; the owner is being asked whether the sun should too, so the number is a
        /// deliverable, not a defect.
        /// </summary>
        [Test]
        public void SomethingPassingOVERTheShade_IsDarkenedToo_AndTheNumberIsPublished()
        {
            RequireAGraphicsDevice();
            BuildTheStage(groundPool: false);
            SpriteRenderer gull = Receiver("gull", new Vector2(0f, ReceiverFeetY), ReceiverWidth, ReceiverHeight);
            gull.sortingOrder = SortingBands.AboveDecor;   // over every world sprite, and still under the band

            Color32[] off = Shoot("cost-00-no-shade", strength: 0f, shade: false);
            Color32[] legacy = Shoot("cost-01-legacy-arm", strength: 1f, shade: false);
            Color32[] shade = Shoot("cost-02-shade-arm", strength: 1f, shade: true);

            bool[] hers = MaskOf(off, ReceiverColor);
            double legacyPct = MeanDarkeningPct(off, legacy, hers);
            double shadePct = MeanDarkeningPct(off, shade, hers);
            Debug.Log($"[sun-shade] THE COST — something ABOVE the shade ({Count(hers)} px): " +
                      $"legacy arm {legacyPct:F2} % darker, shade arm {shadePct:F2} % darker.");

            Assert.AreEqual(0.0, legacyPct, 0.01, "today nothing above a sun shadow is darkened by it");
            Assert.Greater(shadePct, 15.0,
                "and in the shade arm it is, by the same fraction as the ground under it — this is the " +
                "trade the PR asks the owner to weigh, and it must stay visible in the numbers");

            SaveSheet("plate-04-the-cost-something-over-the-shade", legacy, shade);
        }

        // =============================================================================================
        //  Measurement
        // =============================================================================================

        /// <summary>The rake in metres the fixture's published sun asks for, from the same maths the component runs.</summary>
        private static float RakeMetres()
        {
            SpriteShadowProfile look = SpriteShadow.SharedProfile;
            return DayNightMath.ShadowLength(SunElevation, look.LengthAtNoon, look.LengthAtHorizon, look.MaxLength)
                   * CasterHeight;
        }

        private static bool Identical(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b || a[i].a != b[i].a) return false;
            return true;
        }

        private static int Count(bool[] mask)
        {
            int n = 0;
            for (int i = 0; i < mask.Length; i++) if (mask[i]) n++;
            return n;
        }

        /// <summary>Pixels of the control shot that are exactly <paramref name="c"/> — a flat-coloured actor's own image.</summary>
        private static bool[] MaskOf(Color32[] control, Color32 c)
        {
            var mask = new bool[control.Length];
            for (int i = 0; i < control.Length; i++)
                mask[i] = control[i].r == c.r && control[i].g == c.g && control[i].b == c.b;
            return mask;
        }

        /// <summary>
        /// Pixels of the control shot inside a world-Y band that are neither the ground nor the caster —
        /// the mask for an actor with no flat colour of its own (a mesh hull).
        /// </summary>
        private bool[] NotTheStage(Color32[] control, float minY, float maxY)
        {
            var mask = new bool[control.Length];
            for (int i = 0; i < control.Length; i++)
            {
                Vector2 w = PixelToWorld(i);
                if (w.y < minY || w.y > maxY) continue;
                Color32 c = control[i];
                bool isGround = c.r == GroundColor.r && c.g == GroundColor.g && c.b == GroundColor.b;
                bool isCaster = c.r == CasterColor.r && c.g == CasterColor.g && c.b == CasterColor.b;
                bool isBlack = c.r + c.g + c.b < 12;
                mask[i] = !isGround && !isCaster && !isBlack;
            }
            return mask;
        }

        /// <summary>
        /// The strip the caster's silhouette actually covers: its own width, from its feet up to the tip of
        /// the rake. With a purely northward shear the shear only stretches the silhouette UP the screen,
        /// so its x extent is the caster's own — which is what makes this a rectangle and not a guess.
        /// </summary>
        private bool[] InTheRake()
        {
            float half = CasterWidth * 0.5f;
            float top = CasterFeetY + CasterHeight + RakeMetres();
            var mask = new bool[ShotPx * ShotPx];
            for (int i = 0; i < mask.Length; i++)
            {
                Vector2 w = PixelToWorld(i);
                mask[i] = w.x >= -half && w.x <= half && w.y >= CasterFeetY && w.y <= top;
            }
            return mask;
        }

        private static bool[] And(bool[] a, bool[] b)
        {
            var m = new bool[a.Length];
            for (int i = 0; i < a.Length; i++) m[i] = a[i] && b[i];
            return m;
        }

        /// <summary>Mean luma loss over a mask, as a percentage of the control's own luma. 0 = untouched.</summary>
        private static double MeanDarkeningPct(Color32[] control, Color32[] shot, bool[] mask)
        {
            double sum = 0;
            int n = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i]) continue;
                double a = control[i].r + control[i].g + control[i].b;
                double b = shot[i].r + shot[i].g + shot[i].b;
                if (a <= 0) continue;
                sum += (a - b) / a;
                n++;
            }
            return n > 0 ? 100.0 * sum / n : 0.0;
        }

        /// <summary>Read-back pixel (bottom-left origin) to world.</summary>
        private Vector2 PixelToWorld(int index)
        {
            int px = index % ShotPx, py = index / ShotPx;
            Vector3 c = _cam.transform.position;
            return new Vector2(c.x + (px + 0.5f) / PxPerMetre - FrameMetres * 0.5f,
                               c.y + (py + 0.5f) / PxPerMetre - FrameMetres * 0.5f);
        }

        // =============================================================================================
        //  The stage
        // =============================================================================================

        /// <summary>
        /// A grey ground, one solid caster with a <see cref="SpriteShadow"/>, and a sun published NORTHWARD
        /// (<c>_SunDir</c> south ⇒ the shadow runs north, the noon case). A north rake is what makes this
        /// measurable: with no east–west component the shear only stretches the silhouette UP the screen,
        /// so it covers the whole width of anything standing north of the caster instead of crossing it as
        /// a diagonal band — the same coverage the game's noon lift produces, isolated.
        /// </summary>
        private void BuildTheStage(bool groundPool)
        {
            var camGo = new GameObject("SunShadeShotCam");
            _spawned.Add(camGo);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = FrameMetres * 0.5f;
            _cam.transform.position = new Vector3(0f, 0f, -10f);
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 100f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Color.black;
            _cam.allowMSAA = false;
            _cam.allowHDR = false;
            // ⚠️ D24_S8, stated. The sun shadow claims each pixel through the STENCIL (#720), so a target
            // with no stencil attachment silently draws no shade at all — every number below reads 0.00 %
            // and the fixture looks like a feature that does not work.
            // ⚠️ _SRGB, not _UNorm: the project renders in gamma-correct linear space, and a UNorm target
            // hands back linear bytes — a grey 128 ground reads 55 and every colour mask in this fixture
            // stops matching.
            _rt = new RenderTexture(ShotPx, ShotPx,
                                    UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB,
                                    UnityEngine.Experimental.Rendering.GraphicsFormat.D24_UNorm_S8_UInt)
            {
                filterMode = FilterMode.Point,
            };
            _cam.targetTexture = _rt;

            var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            Assert.IsNotNull(unlitShader, "URP's Sprite-Unlit-Default shader is missing?");
            _unlit = new Material(unlitShader);
            _spawned.Add(_unlit);

            // A mid-grey ground so a multiply has something to take from.
            var groundGo = new GameObject("ground");
            _spawned.Add(groundGo);
            var gsr = groundGo.AddComponent<SpriteRenderer>();
            gsr.sprite = MakeSprite(4, 4, GroundColor, 4f);
            gsr.sharedMaterial = _unlit;
            gsr.sortingOrder = -10;
            groundGo.transform.localScale = new Vector3(FrameMetres * 1.2f, FrameMetres * 1.2f, 1f);

            // ONE profile for both arms; the arm itself is flipped per shot. The pool is off by default so
            // the cast rake is measured on its own.
            var look = SpriteShadowProfile.CreateDefault();
            look.name = "SpriteShadowProfile (sun-shade fixture)";
            _spawned.Add(look);
            look.MaxLength = 7f;                       // the code default: nothing here is capped
            look.GroundContactRadius = groundPool ? 0.42f : 0f;
            look.GroundContactMinHeight = 0f;          // the fixture's caster is 2 m, the shipped gate is 3
            SpriteShadow.SharedProfile = look;

            var casterGo = new GameObject("caster");
            _spawned.Add(casterGo);
            casterGo.transform.position = new Vector3(0f, CasterFeetY, 0f);
            var csr = casterGo.AddComponent<SpriteRenderer>();
            csr.sprite = MakeSprite(Mathf.RoundToInt(CasterWidth * Ppu), Mathf.RoundToInt(CasterHeight * Ppu),
                                    CasterColor, Ppu, bottomPivot: true);
            csr.sharedMaterial = _unlit;
            csr.sortingOrder = 5;
            _casterShadow = casterGo.AddComponent<SpriteShadow>();
            // Edit mode runs no lifecycle: Awake is what mints the pooled shadow children.
            Invoke(_casterShadow, "Awake");
        }

        /// <summary>A solid, opaque receiver — no shadow of its own, so it can only RECEIVE.</summary>
        private SpriteRenderer Receiver(string name, Vector2 feet, float width, float height)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            go.transform.position = new Vector3(feet.x, feet.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeSprite(Mathf.RoundToInt(width * Ppu), Mathf.RoundToInt(height * Ppu),
                                   ReceiverColor, Ppu, bottomPivot: true);
            sr.sharedMaterial = _unlit;
            // Above the caster AND above the legacy arm's shadow, so in that arm she genuinely draws over
            // the shade — which is the defect this fixture measures, not a fixture artefact.
            sr.sortingOrder = 20;
            return sr;
        }

        private Sprite MakeSprite(int w, int h, Color32 c, float ppu, bool bottomPivot = false)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            tex.SetPixels32(px);
            tex.Apply();
            _spawned.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h),
                                       bottomPivot ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f), ppu);
            _spawned.Add(sprite);
            return sprite;
        }

        private static void Invoke(Object target, string method)
        {
            MethodInfo m = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, $"{target.GetType().Name}.{method}() not found (private API moved?)");
            m.Invoke(target, null);
        }

        /// <summary>
        /// One deterministic frame: publish the frozen sun, set the arm, run the component's own tick and
        /// pose (edit mode runs neither), and photograph it.
        /// </summary>
        private Color32[] Shoot(string name, float strength, bool shade)
        {
            SpriteShadow.SharedProfile.ScreenSpaceShade = shade;
            PublishTheSun(strength);
            Invoke(_casterShadow, "Tick");
            Invoke(_casterShadow, "LateUpdate");
            DumpShadowState(name);
            return Photograph(name);
        }

        /// <summary>The same frame with the sun-shadow component removed outright — the "system absent" control.</summary>
        private Color32[] ShootWithNoShadowAtAll(string name)
        {
            PublishTheSun(0f);
            var children = _casterShadow.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in children) if (sr.gameObject != _casterShadow.gameObject) sr.enabled = false;
            return Photograph(name);
        }

        private static void PublishTheSun(float strength)
        {
            // The sun in the SOUTH: the shadow runs the other way, i.e. NORTH, which is the noon case.
            Shader.SetGlobalVector(IdSunDir, new Vector4(0f, -1f, 0f, 0f));
            Shader.SetGlobalFloat(IdSunElevation, SunElevation);
            Shader.SetGlobalFloat(IdShadowStrength, strength);
            // Full daylight: the day/night overlay is not in this scene, and the shade must not be read as
            // depending on it.
            Shader.SetGlobalColor(IdDayNightTint, Color.white);
        }

        /// <summary>
        /// What the caster's pooled shadow children are actually doing — enabled, on which sprite, with
        /// which material, at which order. A shade that draws nothing looks identical in the pixels to a
        /// shade that draws nothing for a completely different reason; this is what tells them apart.
        /// </summary>
        private void DumpShadowState(string name)
        {
            var srs = _casterShadow.GetComponentsInChildren<SpriteRenderer>(true);
            var sb = new System.Text.StringBuilder();
            sb.Append("[sun-shade:state] ").Append(name);
            foreach (var sr in srs)
            {
                if (sr.gameObject == _casterShadow.gameObject) continue;
                sb.Append(" | ").Append(sr.gameObject.name)
                  .Append(" enabled=").Append(sr.enabled)
                  .Append(" sprite=").Append(sr.sprite != null ? sr.sprite.name : "null")
                  .Append(" mat=").Append(sr.sharedMaterial != null ? sr.sharedMaterial.name : "null")
                  .Append(" layer=").Append(sr.sortingLayerID)
                  .Append(" order=").Append(sr.sortingOrder)
                  .Append(" pos=").Append(sr.transform.position.ToString("F2"))
                  .Append(" bounds=").Append(sr.bounds.ToString());
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// ⚠️⚠️ <b>ONE RENDER, ONE FRESH TARGET — and the reason is the STENCIL.</b> The sun shadow claims
        /// each pixel through the stencil buffer (#720: the first shadow at a pixel writes 1, later ones
        /// fail <c>NotEqual</c> and are discarded). A persistent <see cref="RenderTexture"/> driven by
        /// repeated <see cref="Camera.Render"/> calls in EDIT MODE does <b>not</b> get its stencil cleared
        /// between them: measured here, a stencilled sprite drew 1600 px on the first render and
        /// <b>0 px on every render after it</b>, while the same sprite with <c>Comp Always</c> drew 1600
        /// every time. Read the second shot of such a pair and the feature looks completely dead — every
        /// number in this fixture read 0.00 % until this was found.
        ///
        /// <para><see cref="RenderTexture.Release"/> frees the surface, so the next render allocates a
        /// clean depth+stencil. It is called before EVERY render, which keeps the two-shot habit that
        /// guards against a cold shader cache without letting the first shot poison the second.</para>
        /// </summary>
        private void RenderFresh()
        {
            _rt.Release();
            _cam.Render();
        }

        private Color32[] Photograph(string name)
        {
            WaitOutShaderCompilation();
            RenderFresh();
            RenderFresh();   // the second is read: a cold shader cache has faked a regression here before

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(ShotPx, ShotPx, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, ShotPx, ShotPx), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Color32[] px = tex.GetPixels32();
            SavePng(name, tex);
            Object.DestroyImmediate(tex);
            return px;
        }

        private void WaitOutShaderCompilation()
        {
            for (int i = 0; i < 10; i++)
            {
                RenderFresh();   // kicks the compile off; the wait below is what makes it finish
                if (!ShaderUtil.anythingCompiling) return;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (ShaderUtil.anythingCompiling && clock.Elapsed.TotalSeconds < 120)
                    System.Threading.Thread.Sleep(25);
            }
            Assert.Fail("SHADERS NEVER FINISHED COMPILING — not a shade regression. Re-run with a warm shader cache.");
        }

        // =============================================================================================
        //  Publishing
        // =============================================================================================

        private static string ArtifactDir()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "sun-shade");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void SavePng(string name, Texture2D tex)
            => File.WriteAllBytes(Path.Combine(ArtifactDir(), name + ".png"), tex.EncodeToPNG());

        /// <summary>An A|B contact sheet with a hairline divider — one frame that answers the question.</summary>
        private static void SaveSheet(string name, Color32[] left, Color32[] right)
        {
            const int gap = 6;
            int w = ShotPx * 2 + gap;
            var tex = new Texture2D(w, ShotPx, TextureFormat.RGBA32, false);
            var px = new Color32[w * ShotPx];
            for (int y = 0; y < ShotPx; y++)
            {
                for (int x = 0; x < ShotPx; x++)
                {
                    px[y * w + x] = left[y * ShotPx + x];
                    px[y * w + ShotPx + gap + x] = right[y * ShotPx + x];
                }
                for (int g = 0; g < gap; g++) px[y * w + ShotPx + g] = new Color32(153, 140, 102, 255);
            }
            tex.SetPixels32(px);
            tex.Apply();
            SavePng(name, tex);
            Object.DestroyImmediate(tex);
        }
    }
}
