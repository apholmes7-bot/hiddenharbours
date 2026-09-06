using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE WOOD'S SHADE</b> — tree shading PR 2. #715 made the trees read the sun and made the lit side
    /// agree with the shadow; its plates then measured what the SHADOWS themselves were doing wrong, and
    /// this is that list turned into guards:
    ///
    /// <list type="number">
    /// <item><b>The dials were unreachable.</b> Every look number was a <c>[SerializeField]</c> on a
    /// component that <see cref="AcadianTreeCatalog"/> attaches with no per-tree dials, so re-tuning a dawn
    /// rake meant a code change and a re-plant. They are now one shipped asset.</item>
    /// <item><b>The length cap never bound.</b> <c>_maxLength 7</c> caps a MULTIPLIER whose own ceiling is
    /// <c>LengthAtHorizon</c> (5), so nothing in the game was ever clamped and a white pine raked 54.8 m at
    /// dawn.</item>
    /// <item><b>Shadows stacked.</b> Two crossing shadows darkened the ground twice; 7.5 % of a wooded
    /// frame at 07:00 carried more than twice a single shadow's darkening.</item>
    /// <item><b>Nothing shaded the ground UNDER a crown.</b> At noon the shear is short and runs north, so
    /// the trunk foot — the one place you are certainly under the tree — was in full sun.</item>
    /// </list>
    ///
    /// <para>Headless twins and asset guards only; the render proofs are the PR's plates and the owner's
    /// eye (CI has no graphics device).</para>
    /// </summary>
    public class SpriteShadowProfileTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursSpriteShadow.shader";
        const string MaterialPath = "Assets/_Project/Resources/SpriteShadow.mat";
        const string ShadeMaterialPath = "Assets/_Project/Resources/SpriteShadowShade.mat";

        readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            // ⚠️ SharedProfile is STATIC and would leak a test's dial into every later fixture.
            SpriteShadow.SharedProfile = null;
        }

        SpriteShadowProfile Default()
        {
            var p = SpriteShadowProfile.CreateDefault();
            _spawned.Add(p);
            return p;
        }

        // =========================================================================================
        //  1. THE OWNER CAN REACH THE DIALS
        // =========================================================================================

        /// <summary>
        /// The shipped asset loads from Resources and carries every field the code declares — the
        /// <c>LampShadowProfile</c> / <c>GameConfigAssetCoverage</c> pattern, so a field added to the code
        /// and never written to the file reddens here rather than silently shipping a different look.
        ///
        /// <para>⚠️ THREE values deliberately diverge from the code defaults, and they are asserted to their
        /// OWN numbers rather than skipped — a skipped field is an untested field. The code defaults are the
        /// component's historical numbers so that a project with NO asset renders exactly the pre-PR frame;
        /// the asset is where this PR's two proposals live, both of which are the owner's call off the
        /// plates.</para>
        /// </summary>
        [Test]
        public void TheShippedProfileAsset_CarriesTheCodeDefaults_ExceptTheThreeItDeliberatelyProposes()
        {
            var asset = Resources.Load<SpriteShadowProfile>(SpriteShadow.ProfileResourcePath);
            Assert.IsNotNull(asset,
                $"Resources/{SpriteShadow.ProfileResourcePath}.asset is missing — the owner has no dial for " +
                "the rake length or the shade under a crown, and every caster is running on code defaults.");

            var code = Default();
            Assert.AreEqual(code.MaxAlpha, asset.MaxAlpha, 1e-6f, "MaxAlpha");
            Assert.AreEqual(code.ShadowColor, asset.ShadowColor, "ShadowColor");
            Assert.AreEqual(code.LengthAtNoon, asset.LengthAtNoon, 1e-6f, "LengthAtNoon");
            Assert.AreEqual(code.LengthAtHorizon, asset.LengthAtHorizon, 1e-6f, "LengthAtHorizon");
            Assert.AreEqual(code.GroundContactAlpha, asset.GroundContactAlpha, 1e-6f, "GroundContactAlpha");
            Assert.AreEqual(code.GroundContactSoftness, asset.GroundContactSoftness, 1e-6f, "GroundContactSoftness");
            Assert.AreEqual(code.EdgeSoftness, asset.EdgeSoftness, 1e-6f, "EdgeSoftness");
            Assert.AreEqual(code.ScreenSpaceShade, asset.ScreenSpaceShade, "ScreenSpaceShade");

            // The two proposals.
            Assert.AreEqual(3f, asset.MaxLength, 1e-6f,
                "The shipped cap has moved. 3 is this PR's proposal — the value that actually BINDS, taking " +
                "a mature white pine's dawn rake from 54.8 m to 41 m. The code default stays 7 (which never " +
                "binds) so a missing asset is the pre-PR frame; which of the two the game keeps is the " +
                "owner's call off the rake plate.");
            Assert.IsTrue(asset.SortByFarEnd,
                "The shipped asset no longer sorts shadows by their far end — this PR's third proposal, " +
                "and the one that stops a crown wearing its neighbour's shadow. The code default is false " +
                "(main's behaviour) so a missing asset is unchanged.");
            Assert.AreEqual(0.42f, asset.GroundContactRadius, 1e-6f,
                "The shipped ground-contact radius has moved. 0.42 x the caster's drawn width is this PR's " +
                "proposal for the shade under a crown; the code default is 0 (no pool at all) so a missing " +
                "asset draws exactly what main draws.");

            // And the FILE carries every serialized field the code declares, and none it no longer does — a
            // key missing from the YAML deserialises to the C# default and would pass the value checks above
            // by accident only while the two happen to agree.
            string path = AssetDatabase.GetAssetPath(asset);
            string yaml = File.ReadAllText(path);
            var declared = new List<string>();
            foreach (var f in typeof(SpriteShadowProfile).GetFields(
                         System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                if (f.GetCustomAttributes(typeof(SerializeField), true).Length > 0) declared.Add(f.Name);
            Assert.Greater(declared.Count, 5, "the profile declares its tunables as serialized fields");
            foreach (string name in declared)
                StringAssert.Contains("\n  " + name + ":", yaml, $"{path} does not carry '{name}' — the asset is behind the code");
            foreach (Match m in Regex.Matches(yaml, @"(?m)^  (_[A-Za-z0-9]+):"))
                Assert.Contains(m.Groups[1].Value, declared, $"{path} carries '{m.Groups[1].Value}', which the code no longer declares");
        }

        /// <summary>
        /// 🔴 <b>No asset = the pre-PR frame, exactly.</b> The built-in default must be the numbers the
        /// component itself used to serialize, or the profile stops being an override and becomes a
        /// dependency — and every scene that has not been re-built would quietly change look.
        /// </summary>
        [Test]
        public void TheCodeDefaults_AreTheComponentsOwnHistoricalNumbers()
        {
            var p = Default();
            Assert.AreEqual(0.45f, p.MaxAlpha, 1e-6f, "_maxAlpha was 0.45 on the component");
            Assert.IsFalse(p.ScreenSpaceShade,
                "_screenSpaceShade must default OFF: with it on, every sun shadow in the game stops sorting " +
                "under its caster and starts multiplying the assembled frame. That is a look change with the " +
                "widest blast radius on the board and it is the OWNER's to make, not a default's.");
            Assert.AreEqual(new Color(0.04f, 0.05f, 0.10f, 1f), p.ShadowColor, "_shadowColor");
            Assert.AreEqual(0.35f, p.LengthAtNoon, 1e-6f, "_lengthAtNoon was 0.35");
            Assert.AreEqual(5f, p.LengthAtHorizon, 1e-6f, "_lengthAtHorizon was 5");
            Assert.AreEqual(7f, p.MaxLength, 1e-6f, "_maxLength was 7 — the dead clamp, kept so a missing asset is today");
            Assert.AreEqual(0f, p.EdgeSoftness, 1e-6f, "_edgeSoftness was 0");
            Assert.AreEqual(0f, p.GroundContactRadius, 1e-6f,
                "The built-in default must draw NO ground pool: the pool is new in this PR, so a project " +
                "with no asset has to render main's frame and not a new one.");
            Assert.IsFalse(p.SortByFarEnd,
                "The built-in default must sort a shadow at its caster's feet, as main does — otherwise a " +
                "project with no asset gets this PR's sorting without anyone choosing it.");
        }

        // =========================================================================================
        //  2. THE CAP THAT NEVER BOUND
        // =========================================================================================

        /// <summary>
        /// 🔴 <b>The clamp was dead, and this states it as arithmetic rather than as a claim.</b>
        /// <see cref="DayNightMath.ShadowLength"/> caps <c>lerp(horizon, noon, elevation)</c>, whose maximum
        /// over the whole day IS <c>LengthAtHorizon</c>. So any cap at or above that can never be reached —
        /// which is why 7 clamped nothing while a white pine threw 54.8 m at 07:00.
        /// </summary>
        [Test]
        public void ACapAboveTheHorizonLength_CanNeverBind_WhichIsWhySevenWasDead()
        {
            var code = Default();

            // ⚠️ LengthAtHorizon is a SUPREMUM THAT IS NEVER ATTAINED, and the distinction is the whole
            // reason 7 was dead. ShadowLength short-circuits to 0 at elevation <= 0 (a sun at or below the
            // horizon casts nothing), and for any elevation ABOVE 0 it lerps strictly BELOW LengthAtHorizon
            // toward LengthAtNoon. So the multiplier approaches 5 as the sun rises off the horizon and
            // never reaches it: a 0.05 h sweep's largest reading is 4.999995, and evaluating "the ceiling"
            // at elevation 0 gives 0. Neither is an equality worth asserting — the CLAIM is the bound.
            float ceiling = code.LengthAtHorizon;
            Assert.AreEqual(0f, DayNightMath.ShadowLength(0f, code.LengthAtNoon, ceiling, float.MaxValue), 1e-6f,
                "A sun exactly on the horizon should cast no shadow at all.");
            Assert.AreEqual(ceiling, DayNightMath.ShadowLength(1e-6f, code.LengthAtNoon, ceiling, float.MaxValue), 1e-3f,
                "Just above the horizon the multiplier is not approaching LengthAtHorizon, so the reasoning " +
                "below — that any cap at or above that value is unreachable — no longer holds.");

            float worstSeen = 0f;
            for (float hour = 0f; hour <= 24f; hour += 0.05f)
            {
                float e = DayNightMath.SunElevation(hour, 6f, 20f);
                float uncapped = DayNightMath.ShadowLength(e, code.LengthAtNoon, code.LengthAtHorizon, float.MaxValue);
                float capped = DayNightMath.ShadowLength(e, code.LengthAtNoon, code.LengthAtHorizon, code.MaxLength);
                Assert.AreEqual(uncapped, capped, 1e-6f,
                    $"At {hour:00.00}h the code default cap ({code.MaxLength}) changed the length. It should " +
                    "not be able to: the multiplier's own ceiling is LengthAtHorizon.");
                Assert.Less(uncapped, ceiling + 1e-6f,
                    $"At {hour:00.00}h the multiplier exceeded its own horizon value — the bound the dead " +
                    "clamp depended on.");
                if (uncapped > worstSeen) worstSeen = uncapped;
            }
            Assert.Greater(code.MaxLength, ceiling,
                "The code default cap is no longer above the multiplier's ceiling — it now BINDS, which " +
                "means a project with no profile asset renders something main did not.");
            Assert.Greater(worstSeen, ceiling * 0.999f, "sanity: the sweep did get near the horizon");
        }

        /// <summary>
        /// The shipped cap, by contrast, must actually do something — and the number it does it to is the
        /// one the owner is judging on the rake plate: a mature white pine, 442 px tall at PPU 32.
        /// </summary>
        [Test]
        public void TheShippedCap_Binds_AndTakesAWhitePinesDawnRakeFromFiftyFiveMetresToForty()
        {
            var asset = Resources.Load<SpriteShadowProfile>(SpriteShadow.ProfileResourcePath);
            Assert.IsNotNull(asset);
            const float whitePineDrawnHeight = 442f / 32f;             // 13.81 world units
            float dawn = DayNightMath.SunElevation(7f, 6f, 20f);

            float uncapped = DayNightMath.ShadowLength(dawn, asset.LengthAtNoon, asset.LengthAtHorizon, 7f)
                             * whitePineDrawnHeight;
            float shipped = DayNightMath.ShadowLength(dawn, asset.LengthAtNoon, asset.LengthAtHorizon, asset.MaxLength)
                             * whitePineDrawnHeight;

            Assert.AreEqual(54.7f, uncapped, 0.5f, "the pre-PR dawn rake — the number on #715's plate");
            Assert.Less(shipped, uncapped - 5f,
                "The shipped cap does not shorten a white pine's dawn rake, so shipping it buys nothing and " +
                "the owner has nothing to compare on the rake plate.");
            Assert.AreEqual(asset.MaxLength * whitePineDrawnHeight, shipped, 1e-3f,
                "A capped rake must be exactly cap x height.");
        }

        // =========================================================================================
        //  3. THE SHADE UNDER A CROWN
        // =========================================================================================

        /// <summary>
        /// The pool's ellipse: as wide as the dial says, and squashed to the GROUND PLANE by the same
        /// <see cref="SpriteLightMath.GroundDepthScale"/> the lit-sprite path uses — taken from there rather
        /// than restated, so the shade and the light can never disagree about what the ground plane is.
        /// </summary>
        [Test]
        public void TheGroundPool_IsTheCastersOwnWidth_SquashedToTheGroundPlane()
        {
            // A red spruce: 156 px wide at PPU 32 = 4.875 m drawn.
            const float spruceWidth = 156f / 32f;
            Vector2 size = SpriteShadow.GroundContactSize(spruceWidth, 0.42f);

            Assert.AreEqual(2f * 0.42f * spruceWidth, size.x, 1e-5f, "diameter = 2 x radius x the caster's width");
            Assert.AreEqual(size.x * SpriteLightMath.GroundDepthScale, size.y, 1e-5f,
                "The pool is not squashed by the ground-plane factor, so it reads as a circle painted on a " +
                "wall rather than an ellipse lying on the ground.");
            Assert.Less(size.y, size.x, "a ground ellipse is wider than it is tall under a 40 degree camera");

            // One dial, wildly different casters: a 0.4 m shore plant gets a proportionate pool, not the
            // spruce's — which is what makes a per-species table unnecessary.
            Vector2 small = SpriteShadow.GroundContactSize(0.4f, 0.42f);
            Assert.AreEqual(size.x * (0.4f / spruceWidth), small.x, 1e-5f, "the pool scales with the caster");

            // OFF is exactly off.
            Assert.AreEqual(Vector2.zero, SpriteShadow.GroundContactSize(spruceWidth, 0f), "radius 0 = no pool");
            Assert.AreEqual(Vector2.zero, SpriteShadow.GroundContactSize(0f, 0.42f), "a caster with no width has no pool");
        }

        /// <summary>
        /// 🔴 <b>The height gate is a COST decision as much as a look one, and it is what keeps the shrubs
        /// and the shoreline plants out of the pool pass entirely.</b> A short caster does not need a pool:
        /// its own noon shadow is <c>LengthAtNoon x its height</c>, so for anything around a metre the
        /// sheared silhouette already lands on its own footprint and a pool would draw the same shade twice.
        ///
        /// <para>Measured on St Peters: ungated, 439 casters draw a pool and it costs about 1.4 ms a frame
        /// at 900x900; gated at the shipped 3 m only 331 do — the trees — and the 148 shrubs and 384 shore
        /// plants keep exactly the shadow they had.</para>
        /// </summary>
        [Test]
        public void TheHeightGate_AdmitsTheSmallestMatureTree_AndExcludesEveryShrub()
        {
            var asset = Resources.Load<SpriteShadowProfile>(SpriteShadow.ProfileResourcePath);
            Assert.IsNotNull(asset);

            // The kit's SHORTEST mature tree, drawn: black spruce, 179 px at PPU 32.
            const float shortestTree = 179f / 32f;
            Assert.LessOrEqual(asset.GroundContactMinHeight, shortestTree,
                "The gate now excludes the shortest mature tree in the kit, so part of the wood has no " +
                "shade under it while the rest does — which reads as a bug, not as a dial.");

            // A shrub and a shore plant are around a metre; nothing in those families should get a pool.
            Assert.Greater(asset.GroundContactMinHeight, 1.5f,
                "The gate now admits shrub-sized casters. That is 108 more pool quads for shade their own " +
                "noon shadow already draws, and it changes two families this PR is not about.");
        }

        // =========================================================================================
        //  4. THE CROWN THAT WORE ITS NEIGHBOUR'S SHADOW
        // =========================================================================================

        /// <summary>
        /// 🔴 <b>A rake sorted at its caster's feet is drawn OVER every sprite it crosses.</b> That is what
        /// puts a tree-shaped blot across a neighbouring canopy: the rake runs north, north is up-screen and
        /// therefore BEHIND, and everything behind was drawn earlier. Sorting by the far end drops the
        /// shadow below them all instead.
        ///
        /// <para>The delta is asserted against the SAME <see cref="SortingBands.OrdersPerMetre"/> the Y-sort
        /// spends, because a shadow that dropped at a different metre-to-order rate than the sprites it is
        /// competing with would slide past the wrong neighbours as the sun swung.</para>
        /// </summary>
        [Test]
        public void SortingByTheFarEnd_DropsAShadowBelowEverythingItCrosses()
        {
            const float opm = SortingBands.OrdersPerMetre;

            // A white pine at dawn: 13.81 m drawn, a 3x cap, and the shipped shadow direction's north lean.
            float worldLen = 3f * (442f / 32f);
            Vector2 dawn = DayNightMath.ShadowDirection(7f, 6f, 20f, 0.2f, 0.9f);
            int delta = SpriteShadow.FarEndSortingDelta(dawn.y, worldLen, opm);

            Assert.Less(delta, 0, "A shadow raking NORTH must sort LOWER, not higher — north is behind.");
            Assert.AreEqual(-Mathf.RoundToInt(dawn.y * worldLen * opm), delta,
                "The delta is not the far end's own Y-sort offset.");
            // It has to clear a real neighbour, or the fix is arithmetic that changes nothing: a tree
            // standing 8 m north (the measured nearest-neighbour spacing in the St Peters wood) sorts
            // 8 x OrdersPerMetre below the caster, and the shadow must get under THAT.
            Assert.LessOrEqual(delta, -Mathf.RoundToInt(8f * opm),
                "At dawn the shadow does not drop far enough to clear a neighbour 8 m north — the blot " +
                "would still land on the nearest crown it crosses.");

            // At solar noon the rake is short, so the drop is small — the shadow stays essentially where it
            // was, which is right: there is almost nothing between a short stub and its caster.
            Vector2 noon = DayNightMath.ShadowDirection(13f, 6f, 20f, 0.2f, 0.9f);
            int noonDelta = SpriteShadow.FarEndSortingDelta(noon.y, 0.35f * (442f / 32f), opm);
            Assert.Less(noonDelta, 0);
            Assert.Greater(noonDelta, delta, "the noon stub must drop LESS far than the dawn rake");

            // And a shadow of no length does not move at all — the night case, and the negative control.
            Assert.AreEqual(0, SpriteShadow.FarEndSortingDelta(dawn.y, 0f, opm),
                "A zero-length shadow changed its sorting. At night the component sends length 0, and a " +
                "shadow that moved anyway would re-sort every caster in the dark for nothing.");
        }

        // =========================================================================================
        //  5. THE SHADER (the twins above are only twins)
        // =========================================================================================

        /// <summary>
        /// The stencil is render STATE, and state cannot come from a MaterialPropertyBlock — so it is
        /// declared in the pass and shipped in the material, and this is what holds it there. Three reads,
        /// each naming a way the stacking fix could quietly stop working while every arithmetic test passed.
        /// </summary>
        [Test]
        public void TheShadowShader_ClaimsEachPixelOnce_AndCanDrawAGroundPool()
        {
            string src = File.ReadAllText(ShaderPath);

            StringAssert.Contains("Stencil", src,
                $"'{ShaderPath}' has lost its Stencil block — two crossing shadows darken the ground twice " +
                "again, which is the patchwork this PR removed.");
            foreach (string s in new[] { "Ref [_StencilRef]", "Comp [_StencilComp]", "Pass [_StencilPass]" })
                StringAssert.Contains(s, src,
                    $"'{ShaderPath}' hard-codes its stencil state instead of taking it from the material. " +
                    "The property indirection IS the escape hatch: a second material with Comp = Always " +
                    "reproduces the old stacking, and that is what the before/after plate is rendered with.");

            StringAssert.Contains("_GroundContact", src,
                "The shader can no longer draw a ground-contact pool.");
            StringAssert.Contains("(_GroundContact > 0.0) ? float2(0.0, 0.0)", src,
                "A ground pool is taking the SHEAR. It is already lying flat on the ground at the caster's " +
                "feet — projecting it would rake a flat ellipse across the field.");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(mat, $"'{MaterialPath}' was not found.");
            Assert.AreEqual(6, mat.GetInt("_StencilComp"),
                "The shipped material is not comparing NotEqual (6), so the first shadow at a pixel no " +
                "longer wins and shadows stack again.");
            Assert.AreEqual(1, mat.GetInt("_StencilRef"), "_StencilRef");
            Assert.AreEqual(2, mat.GetInt("_StencilPass"),
                "The shipped material is not REPLACING (2) on pass, so no shadow ever claims a pixel and " +
                "the NotEqual test always passes — the stencil would be inert.");

            // 🔴 AND THE SAME THREE READ OFF THE FILE, because the three asserts above are not enough on
            // their own. ⚠️ Unity serializes a ShaderLab `Int` property into m_FLOATS, not m_Ints — every
            // other material in this project carries `m_Ints: []`. A value hand-authored under m_Ints is
            // silently ignored, `GetInt` then falls back to the SHADER's declared default, and the asserts
            // above pass whatever the material says. Measured: with the file mutated to Always(8) they all
            // still went green. The file read is what makes them mean something.
            string yaml = File.ReadAllText(MaterialPath);
            StringAssert.Contains("m_Ints: []", yaml,
                $"'{MaterialPath}' has values under m_Ints. Unity reads shader Int properties out of " +
                "m_Floats; anything under m_Ints is dead YAML that looks like configuration.");
            foreach (var kv in new[] { ("_StencilComp", "6"), ("_StencilRef", "1"), ("_StencilPass", "2") })
                StringAssert.Contains("- " + kv.Item1 + ": " + kv.Item2, yaml,
                    $"'{MaterialPath}' does not carry {kv.Item1}: {kv.Item2} under m_Floats, so the shipped " +
                    "material is relying on the shader's default rather than stating its own state.");
        }

        // =========================================================================================
        //  5. THE SHADE ARM — does a RECEIVER read shaded, and does the passthrough hold?
        // =========================================================================================

        /// <summary>
        /// 🔴 <b>THE ARM SHIPS OFF, IN BOTH THE CODE AND THE ASSET — this PR is a no-op until the owner
        /// rules.</b> Every other dial on the profile tunes a shade that darkens the ground; this one
        /// decides whether the shade darkens what STANDS in it, by moving every sun shadow in the game out
        /// of the decor band and onto the compositing ladder. It carries a real cost of its own (a
        /// screen-space multiply cannot tell "standing in the shade" from "passing over it"), so it is the
        /// owner's call off the plates and neither default may make it for him.
        /// </summary>
        [Test]
        public void TheShadeArm_ShipsOff_InBothTheCodeDefaultAndTheAsset()
        {
            Assert.IsFalse(Default().ScreenSpaceShade, "the built-in default");
            var asset = Resources.Load<SpriteShadowProfile>(SpriteShadow.ProfileResourcePath);
            Assert.IsNotNull(asset);
            Assert.IsFalse(asset.ScreenSpaceShade, "the shipped asset");
        }

        /// <summary>
        /// <b>The two arms are two shipped MATERIALS on one shader, and each states its own blend.</b>
        ///
        /// <para>⚠️ It has to be materials rather than a property-block value, because the arms differ in
        /// BLEND STATE — the same wall the stencil hit: a <see cref="MaterialPropertyBlock"/> feeds shader
        /// uniforms and cannot change render state. One shader with two materials keeps the shear maths in
        /// exactly one transcription.</para>
        ///
        /// <para>⚠️ And every value is read off the FILE as well as off the object. Unity serializes a
        /// ShaderLab <c>Float</c>/<c>Int</c> property into <c>m_Floats</c>, never <c>m_Ints</c>, and
        /// <c>GetFloat</c> on a material that carries neither falls through to the SHADER's declared
        /// default — which here is the legacy arm, so an object-only assert on the SHADE material would
        /// pass with the file saying nothing at all.</para>
        /// </summary>
        [Test]
        public void TheTwoArms_AreTwoMaterialsOnOneShader_EachStatingItsOwnBlend()
        {
            var legacy = Resources.Load<Material>("SpriteShadow");
            var shade = Resources.Load<Material>(SpriteShadow.ShadeMaterialPath);
            Assert.IsNotNull(legacy, "Resources/SpriteShadow.mat is missing");
            Assert.IsNotNull(shade,
                $"Resources/{SpriteShadow.ShadeMaterialPath}.mat is missing — the shade arm would fall back " +
                "to a runtime-minted material and the owner would have no asset to look at.");
            Assert.AreEqual(legacy.shader, shade.shader,
                "both arms must be the same shader, or the shear maths has two transcriptions to keep in step");

            // Blend enum values, spelled out: Zero 0, SrcColor 3, SrcAlpha 5, OneMinusSrcAlpha 10.
            Assert.AreEqual(0f, legacy.GetFloat("_ScreenShade"), 1e-6f, "the legacy arm draws a dark sprite");
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.SrcAlpha, legacy.GetFloat("_SrcBlend"), 1e-6f);
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, legacy.GetFloat("_DstBlend"), 1e-6f);

            Assert.AreEqual(1f, shade.GetFloat("_ScreenShade"), 1e-6f, "the shade arm multiplies the frame");
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.Zero, shade.GetFloat("_SrcBlend"), 1e-6f,
                "Blend Zero SrcColor is the multiply — the same blend LampShadow.mat uses, and the reason a " +
                "receiver is darkened at all");
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.SrcColor, shade.GetFloat("_DstBlend"), 1e-6f);

            // The stencil rides across unchanged: one shade per pixel is a property of the sun, not of the arm.
            foreach (var kv in new[] { ("_StencilComp", 6), ("_StencilRef", 1), ("_StencilPass", 2) })
                Assert.AreEqual(kv.Item2, shade.GetInt(kv.Item1),
                    $"the shade arm must claim each pixel once too, or two crossing shades multiply twice ({kv.Item1})");

            foreach (var pair in new[]
                     {
                         (MaterialPath, new[] { ("_ScreenShade", "0"), ("_SrcBlend", "5"), ("_DstBlend", "10") }),
                         (ShadeMaterialPath, new[] { ("_ScreenShade", "1"), ("_SrcBlend", "0"), ("_DstBlend", "3") }),
                     })
            {
                string yaml = File.ReadAllText(pair.Item1);
                StringAssert.Contains("m_Ints: []", yaml,
                    $"'{pair.Item1}' has values under m_Ints, which Unity ignores for shader scalars");
                foreach (var kv in pair.Item2)
                    StringAssert.Contains("- " + kv.Item1 + ": " + kv.Item2, yaml,
                        $"'{pair.Item1}' does not carry {kv.Item1}: {kv.Item2} under m_Floats, so it is relying " +
                        "on the shader's default rather than stating its own arm.");
            }
        }

        /// <summary>
        /// <b>The shader takes its blend from the material, and every line the shade arm adds is dead when
        /// the arm is off.</b> That is what makes the passthrough exact rather than merely untested: the
        /// legacy arm's fragment path is the pre-PR one, gated behind <c>_ScreenShade &gt; 0</c>, and the
        /// blend it draws with is the shader's own declared default.
        /// </summary>
        [Test]
        public void TheShader_TakesItsBlendFromTheMaterial_AndGatesEveryShadeLineBehindTheArm()
        {
            string src = File.ReadAllText(ShaderPath);
            StringAssert.Contains("Blend [_SrcBlend] [_DstBlend]", src,
                $"'{ShaderPath}' no longer takes its blend from the material, so the two arms cannot differ");
            StringAssert.Contains("_SrcBlend (\"Src blend (5 = SrcAlpha, 0 = Zero)\", Float) = 5", src,
                "the shader's declared default must stay the LEGACY blend, so a material that states nothing " +
                "renders exactly as this shader always has");
            StringAssert.Contains("_DstBlend (\"Dst blend (10 = OneMinusSrcAlpha, 3 = SrcColor)\", Float) = 10", src);
            StringAssert.Contains("_ScreenShade (\"Screen-space shade arm (0 = under the caster, 1 = over the frame)\", Float) = 0", src,
                "and the arm itself must default OFF in the shader");

            StringAssert.Contains("lerp(half3(1.0, 1.0, 1.0), _ShadowColor.rgb, a)", src,
                "the shade arm must output lerp(1, tint, alpha) — under Blend Zero SrcColor that is what " +
                "multiplies the frame down by a constant FRACTION, which is what darkens a receiver");
            StringAssert.Contains("if (_ScreenShade > 0.0 && _GroundContact <= 0.0)", src,
                "the self-exclusion must be gated on the ARM (the legacy arm gets it from sorting under its " +
                "caster) and on the CAST path (the ground pool takes no exclusion, deliberately)");

            // Every occurrence of the new output has to sit under the arm gate; a stray one would change
            // the legacy frame.
            Assert.AreEqual(src.IndexOf("if (_ScreenShade > 0.0)"), src.LastIndexOf("if (_ScreenShade > 0.0)"),
                "there must be exactly one un-compounded arm gate in the fragment, or the legacy path has " +
                "grown a second branch that could change today's frame");
        }

        /// <summary>
        /// <b>The pure function the shade arm's self-exclusion rests on.</b> A shade fragment carries its
        /// OWN texel while sitting at the screen point of a different one — the texel its caster draws
        /// there — and the offset between them is the vertex shear converted into uv. That conversion is
        /// <c>ppu / textureSize</c>, the inverse of the sprite's own texture mapping, and it is exact for a
        /// FullRect and a Tight mesh alike because it never touches the mesh.
        ///
        /// <para>Verified by round trip rather than by restating the formula: shear a point by a known
        /// number of OBJECT units, convert, and land on the texel the maths says.</para>
        /// </summary>
        [Test]
        public void UvPerObjectUnit_InvertsTheSpritesOwnTextureMapping()
        {
            Assert.AreEqual(Vector2.one, SpriteShadow.UvPerObjectUnit(null),
                "a caster with no sprite shears nothing away");

            // A 4-row sheet, sliced: the cell is a strip of a much taller texture, which is the production
            // case and the one raw uv gets wrong.
            var tex = new Texture2D(64, 256, TextureFormat.RGBA32, false);
            _spawned.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0f, 128f, 64f, 64f), new Vector2(0.5f, 0f), 32f);
            _spawned.Add(sprite);

            Vector2 perUnit = SpriteShadow.UvPerObjectUnit(sprite);
            Assert.AreEqual(32f / 64f, perUnit.x, 1e-6f, "ppu over texture WIDTH");
            Assert.AreEqual(32f / 256f, perUnit.y, 1e-6f, "ppu over texture HEIGHT — not the cell's height");

            // The round trip. One object unit up the sprite is 32 px of a 256 px sheet, i.e. an eighth of uv.
            const float shearObjectUnits = 2f;
            float shearUv = shearObjectUnits * perUnit.y;
            Assert.AreEqual(0.25f, shearUv, 1e-6f,
                "2 object units at 32 ppu is 64 px, and 64 of 256 is a quarter of the sheet");
            // And the same in x. The cell is 64 px wide, which at 32 ppu is TWO object units, and it fills
            // the texture's width — so two object units must span the whole of uv.x.
            Assert.AreEqual(1f, (64f / 32f) * perUnit.x, 1e-6f, "the cell spans the whole width of the sheet");
        }

        /// <summary>
        /// Nothing else in the project may use the stencil, or "first shadow wins" becomes "first shadow
        /// wins unless something else got there". This is the check that made the cut safe to take, kept as
        /// a guard because the next feature to reach for a stencil would break shadows silently.
        /// </summary>
        [Test]
        public void NothingElseInTheProjectUsesTheStencil()
        {
            var offenders = new List<string>();
            foreach (string path in Directory.GetFiles("Assets/_Project", "*.shader", SearchOption.AllDirectories))
            {
                if (path.Replace('\\', '/').EndsWith("HiddenHarboursSpriteShadow.shader")) continue;
                if (Regex.IsMatch(File.ReadAllText(path), @"(?m)^\s*Stencil\s*$|(?m)^\s*Stencil\s*\{"))
                    offenders.Add(path);
            }
            CollectionAssert.IsEmpty(offenders,
                "Another shader has started using the stencil buffer. The sun shadows claim it per frame to " +
                "stop two shadows darkening the ground twice; a second consumer means whichever draws first " +
                "silently suppresses the other. Give one of them a different bit and say which is which.");
        }
    }
}
