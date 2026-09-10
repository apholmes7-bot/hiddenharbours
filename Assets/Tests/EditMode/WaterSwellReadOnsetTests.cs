using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>REGISTER ROW 25 (row 35 folded in) — the swell had no shading at the sea the owner
    /// actually sails, because its calm gate opened above it. ✅ RULED AND SHIPPED (owner,
    /// 2026-09-09): <c>_SwellReadSeaStateLo</c> 0.28 → 0.10 on the shader and all nine water
    /// materials. <c>_SwellReadSeaStateHi</c> stays 0.45.</b>
    ///
    /// <para>The gate is <c>smoothstep(_SwellReadSeaStateLo, _SwellReadSeaStateHi, _Chop)</c> and it
    /// multiplies BOTH of the modelled swell's shading terms — <c>_SwellReadStrength</c> and
    /// <c>_SwellFaceShade</c> — and, since PR 9, <c>_SunSideStrength</c> too. At 0.28 it is exactly
    /// 0 below a sea state of 0.28, which is most of the day.</para>
    ///
    /// <para><b>What the owner played</b> (2026-09-08, same beach, one afternoon):</para>
    /// <code>
    ///   13:12  sea 0.181  gate 0.000  "it just still doesnt feel like one coherent surface,
    ///                                  it feels like layers"
    ///   15:51  sea 0.341  gate 0.294  "it actually looks really good in the evening, its making
    ///                                  me rethink what i just said"
    /// </code>
    ///
    /// <para>⭐ <b>The floor this ruling was priced against has since MOVED.</b> #797 measured the
    /// wind law as it then stood — <c>3 + strN·1.3 + gustN·1.2</c>, both noises bottoming at −1
    /// — so the wind never fell below 0.50 m/s and the sea state never below <b>0.143</b>. That is
    /// the sea the 0.041 price below was quoted on. <b>PR D (#811) then widened the law</b>: the
    /// strength clamp's lower bound is now a literal <c>0f</c> (<c>WeatherModel</c> ~90–92), and
    /// <c>WindUncapReachTests</c> MEASURES a Glass sea on <b>6.3 %</b> of a game week over nine seeds.
    /// A glass calm is reachable at last — and at sea state <b>0.000</b> this gate reads exactly
    /// <b>0.000</b>. So the ruling survives and cost LESS than the owner was told: on water that is
    /// actually glass, row 5's mirror is untouched. The 0.143 constant below is kept because it is the
    /// sea the price was QUOTED on, not because it is still a floor.</para>
    ///
    /// <para>The onset table this ruling was chosen from (water-rendering.md §45, register row 25):</para>
    /// <code>
    ///   onset | glass 0.05 | sea 0.143 (as priced) | his 13:12 (0.181) | his 15:51 (0.341)
    ///    0.28 |      0.000 |                   0.000 |             0.000 |             0.294   &lt;- SUPERSEDED
    ///    0.15 |      0.000 |                   0.000 |             0.030 |             0.700
    ///    0.10 |      0.000 |                   0.041 |             0.136 |             0.769   &lt;- SHIPPED
    ///    0.05 |      0.000 |                   0.137 |             0.252 |             0.818
    /// </code>
    ///
    /// <para>0.15 was the lowest onset that leaves the 0.143 sea completely unshaded; the
    /// owner took <b>0.10</b> instead, buying a legible midday face (0.136 rather than 0.030) at the
    /// cost of a faint 0.041 on the calmest water the game can draw. That cost is <b>ruled, not
    /// overlooked</b> — which is why it is asserted below rather than merely tolerated.</para>
    ///
    /// <para>⚠️ <b>This pair has a SECOND reader.</b> <c>SwashSeaStateGate()</c> reuses the same two
    /// thresholds for the shore's calm fade (shader ~2975; twin
    /// <see cref="HiddenHarbours.Art.WaterSurface.SwashSeaStateGate"/>). Lowering the onset therefore
    /// also lets the swash wash a little harder on a calm sea. That is a consequence of the ruling,
    /// not a side effect of this PR, and the last test states it in numbers.</para>
    ///
    /// <para>The arithmetic tests need no GPU and no assets. The asset tests read BOTH the file's
    /// bytes AND what <c>AssetDatabase</c> actually deserializes — a hand-added YAML key that lands
    /// outside the mapping reads the shader default while the file still plainly shows the number
    /// (this repo has shipped that bug: memory <c>a-blank-line-ends-a-unity-yaml-mapping</c>).</para>
    /// </summary>
    public class WaterSwellReadOnsetTests
    {
        /// <summary>What ships since the owner's 2026-09-09 ruling.</summary>
        const float RuledOnset = 0.10f;

        /// <summary>⚠️ What shipped from 2026-07-08 UNTIL that ruling. Kept NAMED rather than deleted
        /// because two guards below exist to say why it moved — and because a bare 0.28 left sitting
        /// in a test would read as a current fact inside a year.</summary>
        const float SupersededOnset = 0.28f;

        /// <summary>The upper threshold, unmoved by the ruling: it keys the canon Light→Moderate band
        /// edge (3/7 ≈ 0.4286) so a moderate sea keeps the full read.</summary>
        const float FullAt = 0.45f;

        // The three sea states the ruling was argued on. The first two are the owner's own clock on
        // 2026-09-08 (weather recomputed at his seed); the third is #797's measured floor.
        const float SeaAtHisMidday  = 0.181f;
        const float SeaAtHisEvening = 0.341f;

        /// <summary>⚠️ #797's measurement of the WindProfile AS IT THEN STOOD — and it warned that
        /// "if a future PR retunes the profile this number moves, and the cost side of this ruling has
        /// to be re-argued rather than re-typed". <b>PR D (#811) retuned it.</b> This is no longer a
        /// floor; it is kept because it is the sea the ruling's 0.041 price was QUOTED on, so that cost
        /// stays checkable. For the floor use <see cref="TrueCalmFloorSincePrD"/>.</summary>
        const float CalmSeaTheRulingWasPricedAt = 0.143f;

        /// <summary>The calmest sea the wind law can now produce. <c>WeatherModel</c> clamps the wind
        /// strength at a literal <c>0f</c> lower bound (~90–92), and <c>WindUncapReachTests</c> measures
        /// a Glass sea on 6.3 % of a game week over nine seeds — so this is reachable, not theoretical.
        /// </summary>
        const float TrueCalmFloorSincePrD = 0f;

        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";

        static readonly string[] WaterMaterials =
        {
            "Assets/_Project/Art/Materials/Water.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_DeepBlue.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_FoggySmother.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_GlassyCalm.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_NorthAtlantic.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_StirredBrown.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_StormGrey.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_Tropical.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_WarmShelter.mat",
        };

        /// <summary>HLSL's <c>smoothstep</c>, which is NOT <c>Mathf.SmoothStep</c> (that one lerps
        /// between the edges instead of clamping the parameter). The shader's own line, transcribed.</summary>
        static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The shader's gate, including its degenerate-pair guard (<c>hi = max(hi, lo+1e-3)</c>).</summary>
        static float Gate(float onset, float full, float sea)
            => SmoothStep(Mathf.Clamp01(onset), Mathf.Max(full, Mathf.Clamp01(onset) + 1e-3f), sea);

        // =============================================================================================
        //  Why the knob moved — arithmetic only, no GPU, no assets
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>THE DEFECT, IN ONE LINE: at the superseded onset the owner's midday sea sat BELOW the
        /// gate's foot, so the swell's every shading term was multiplied by exactly zero.</b> Not
        /// "faint" — zero. That is why a flat colour field with white layers composited over it was
        /// the whole of what he could see, and why the identical water an evening later read "really
        /// good": the wind had moved the sea 0.181 → 0.341 and lifted the gate off its floor.
        /// </summary>
        [Test]
        public void AtTheSupersededOnset_TheOwnersMiddaySea_WasMultipliedByExactlyZero()
        {
            float wasMidday  = Gate(SupersededOnset, FullAt, SeaAtHisMidday);
            float wasEvening = Gate(SupersededOnset, FullAt, SeaAtHisEvening);

            Assert.AreEqual(0f, wasMidday, 1e-6f,
                "⭐ ROW 25's DEFECT, kept as the record of why the onset moved: at 0.28 the gate at a " +
                "sea state of 0.181 is not small, it is exactly 0 — every modelled-swell shading term " +
                "(_SwellReadStrength, _SwellFaceShade, _SunSideStrength) is switched off. The owner " +
                "played that water and called it layers.");

            Assert.AreEqual(0.294f, wasEvening, 0.001f,
                "…and the SAME water three hours later read 0.294, which he called really good. The " +
                "gate is the only thing that changed between the two readings — the solar-noon gate " +
                "row 35 first blamed was redundant, because this one was already zero.");
        }

        /// <summary>
        /// ✅ <b>WHAT THE RULING BUYS, AND WHAT IT COSTS — the register's onset table, reproduced.</b>
        /// The owner was offered 0.15 (the lowest onset that leaves the calmest reachable sea at
        /// exactly 0.000) and chose 0.10, which puts a legible face on his midday sea and a faint one
        /// on the calmest water the game can draw. Both halves are asserted, because a ruling whose
        /// cost is not in a test is a ruling that gets quietly "fixed" later.
        /// </summary>
        [Test]
        public void TheRuledOnset_MakesHisMiddaySeaLegible_AndPutsAFaintReadOnTheCalmestWater()
        {
            var report = new StringBuilder();
            report.AppendLine("  onset | floor 0.000 | glass 0.05 | 0.143 as priced | midday 0.181 | evening 0.341");
            foreach (float onset in new[] { SupersededOnset, 0.15f, RuledOnset, 0.05f })
                report.AppendLine($"   {onset:0.00}  | {Gate(onset, FullAt, TrueCalmFloorSincePrD),11:0.000} | " +
                                  $"{Gate(onset, FullAt, 0.05f),10:0.000} | " +
                                  $"{Gate(onset, FullAt, CalmSeaTheRulingWasPricedAt),15:0.000} | " +
                                  $"{Gate(onset, FullAt, SeaAtHisMidday),12:0.000} | " +
                                  $"{Gate(onset, FullAt, SeaAtHisEvening),13:0.000}");
            TestContext.WriteLine(report.ToString());

            Assert.AreEqual(0.136f, Gate(RuledOnset, FullAt, SeaAtHisMidday), 0.001f,
                "⭐ THE RULING'S POINT: at 0.10 the owner's midday sea reads 0.136 instead of 0.000. " +
                "0.15 would have given him 0.030 — awake, but not legible; he was shown both and " +
                "chose the legible one.");

            Assert.AreEqual(0.769f, Gate(RuledOnset, FullAt, SeaAtHisEvening), 0.001f,
                "…and his evening acceptance picture keeps its read, raised 0.294 -> 0.769. That " +
                "picture is the acceptance for this row; the midday one is owed a plate.");

            // 0.0416 exactly; the register and §45 print it rounded to 0.041. Pinned at full
            // precision so the assertion is not sitting on the edge of its own tolerance.
            Assert.AreEqual(0.0416f, Gate(RuledOnset, FullAt, CalmSeaTheRulingWasPricedAt), 0.0005f,
                "⭐ THE RULING'S PRICE, asserted so it cannot be un-noticed: on the 0.143 sea this " +
                "ruling was PRICED against, the swell now shades faintly. Row 5 calls a glass calm " +
                "sacred; the owner ruled on 2026-09-09 that 0.041 on a sea that is not actually glass " +
                "is worth a legible midday. Do NOT 'fix' this back to 0 without a new ruling.");

            // ⭐ ...and that price is NOT paid at the floor, because the floor moved after the ruling.
            Assert.AreEqual(0f, Gate(RuledOnset, FullAt, TrueCalmFloorSincePrD), 1e-6f,
                "⭐ THE RULING COST LESS THAN HE WAS TOLD. The 0.041 above was quoted to him as the " +
                "price on 'the calmest water the game can draw', because #797 had measured a 0.143 " +
                "floor. PR D (#811) then widened the wind law — WeatherModel clamps strength at a " +
                "literal 0f lower bound, and WindUncapReachTests MEASURES a Glass sea on 6.3 % of a " +
                "game week over nine seeds. At that true floor this gate is EXACTLY 0.000, so row 5's " +
                "mirror on water that is actually glass was never touched. Do not re-quote the 0.041 " +
                "as a floor cost; it is the cost at sea state 0.143 and nowhere calmer.");

            Assert.AreEqual(0f, Gate(RuledOnset, FullAt, 0.05f), 1e-6f,
                "…and a TRUE glass calm still reads nothing at the ruled onset. The sea state has to " +
                "climb past 0.10 before anything is shaded at all — the mirror is only touched " +
                "because the wind law cannot go there, not because the gate stopped guarding it.");
        }

        /// <summary>
        /// The gate must slide with the sea, never step: the whole point of a smoothstep here is that
        /// the shading fades in as the weather drifts, so the sea cannot pop between two frames as
        /// the wind crosses a threshold. Checked at the RULED pair, on a fine sweep.
        /// </summary>
        [Test]
        public void TheRuledGate_IsMonotoneAcrossTheWholeSeaStateAxis()
        {
            Assert.Less(RuledOnset, FullAt, "the onset must stay below the full threshold or the " +
                                            "shader's max() guard turns the fade into a hard step");

            float prev = -1f;
            for (int i = 0; i <= 1000; i++)
            {
                float g = Gate(RuledOnset, FullAt, i / 1000f);
                Assert.GreaterOrEqual(g, prev - 1e-6f,
                    $"the swell-read gate fell as the sea rose, at sea state {i / 1000f:0.000}");
                prev = g;
            }
            Assert.AreEqual(1f, prev, 1e-6f, "…and it must reach full read by the top of the axis");
        }

        // =============================================================================================
        //  The ruling, pinned to the assets it changed
        // =============================================================================================

        /// <summary>
        /// ✅ <b>THE SHADER'S OWN DEFAULT.</b> Asserted twice on purpose: once against the ShaderLab
        /// source text, and once against a Material built from the compiled shader — because a
        /// property block that ShaderLab failed to parse would leave the source reading 0.10 while
        /// every material silently rode something else.
        /// </summary>
        [Test]
        public void TheShaderDefault_IsTheRuledPair_InTheSourceAndInAMaterialBuiltFromIt()
        {
            string src = File.ReadAllText(ShaderPath, Encoding.UTF8);
            Assert.AreEqual(RuledOnset, RangeDefault(src, "_SwellReadSeaStateLo"), 1e-6f,
                "the shader's _SwellReadSeaStateLo default must be the ruled 0.10 (owner, 2026-09-09, " +
                "register row 25). Every material that does not serialize the key rides this number.");
            Assert.AreEqual(FullAt, RangeDefault(src, "_SwellReadSeaStateHi"), 1e-6f,
                "_SwellReadSeaStateHi was NOT part of the ruling and must stay at the canon " +
                "Light-to-Moderate band edge");

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(shader, "the water shader must load");
            var bare = new Material(shader);
            try
            {
                Assert.IsTrue(bare.HasProperty("_SwellReadSeaStateLo"),
                    "the compiled shader must actually EXPOSE the onset — a property the block " +
                    "declares but ShaderLab drops is a knob nothing can set");
                Assert.AreEqual(RuledOnset, bare.GetFloat("_SwellReadSeaStateLo"), 1e-6f,
                    "a material built from the bare shader must come up at the ruled onset");
                Assert.AreEqual(FullAt, bare.GetFloat("_SwellReadSeaStateHi"), 1e-6f,
                    "…and at the unmoved full threshold");
            }
            finally { UnityEngine.Object.DestroyImmediate(bare); }
        }

        /// <summary>
        /// ✅ <b>THE RULING, ON THE NINE ASSETS — the file AND the object, which are two different
        /// facts.</b> <c>Apply water preset</c> is a wholesale <c>CopyPropertiesFromMaterial</c>
        /// (register row 3), so a preset that does not carry the key is how the superseded onset
        /// would come back the next time the owner changed the sea's mood — the same reason row 8's
        /// <c>_UntileStrength</c> is pinned on all nine.
        ///
        /// <para>⚠️ The structural half is not decoration. A key appended after a blank line, or after
        /// the object's block ends, deserializes to the shader default while <c>git show</c> still
        /// reads 0.1 — this repo shipped exactly that bug on 34 hull defs (#741, found by #747).</para>
        /// </summary>
        [Test]
        public void EveryWaterMaterial_SerializesTheRuledOnset_AndUnityLoadsWhatTheFileSays()
        {
            var report = new StringBuilder();
            report.AppendLine("  material                       file    loaded    Hi");
            foreach (string rel in WaterMaterials)
            {
                Assert.IsTrue(File.Exists(rel), $"missing water material: {rel}");
                string text = File.ReadAllText(rel, Encoding.UTF8);

                // (1) what the FILE says
                var m = Regex.Match(text, @"^[ \t]+- _SwellReadSeaStateLo:[ \t]*(-?[\d.eE+]+)[ \t]*$",
                                    RegexOptions.Multiline);
                Assert.IsTrue(m.Success,
                    $"{rel} does not serialize _SwellReadSeaStateLo. The owner ruled 0.10 on " +
                    "2026-09-09; an absent key rides the shader default today, but 'Apply water " +
                    "preset' copies the whole property sheet, so an unstated preset is how the old " +
                    "onset comes back.");
                float inFile = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);

                // (2) the two structural faults that would make (1) a lie
                Assert.IsTrue(text.EndsWith("\n"),
                    $"{rel} does not end in a newline — the next hand-appended key would land outside " +
                    "the mapping and read the shader default silently");
                Assert.IsNotEmpty(LineBefore(text, m.Index).Trim(),
                    $"{rel} has a BLANK LINE immediately before _SwellReadSeaStateLo. A blank line " +
                    "ENDS a Unity YAML block mapping: the key would be dropped and the field would " +
                    "keep the shader default, with the file still plainly reading the right number.");

                // (3) what UNITY LOADS — the only one of the three the renderer ever sees
                var mat = AssetDatabase.LoadAssetAtPath<Material>(rel);
                Assert.IsNotNull(mat, $"{rel} did not load as a Material");
                Assert.IsTrue(mat.HasProperty("_SwellReadSeaStateLo"),
                    $"{rel} loaded without the onset property — is it still on the water shader?");
                float loaded = mat.GetFloat("_SwellReadSeaStateLo");
                float loadedHi = mat.GetFloat("_SwellReadSeaStateHi");

                report.AppendLine($"  {Path.GetFileName(rel),-28} {inFile:0.000}   {loaded:0.000}   {loadedHi:0.000}");

                Assert.AreEqual(RuledOnset, inFile, 1e-4f,
                    $"{Path.GetFileName(rel)} serializes _SwellReadSeaStateLo {inFile:0.###}; the " +
                    "owner ruled 0.10 on 2026-09-09 (register row 25).");
                Assert.AreEqual(inFile, loaded, 1e-6f,
                    $"⭐ {Path.GetFileName(rel)}: the FILE says {inFile:0.###} and Unity LOADS " +
                    $"{loaded:0.###}. A guard that never instantiates the asset is checking your " +
                    "typing, not your data (memory: a-field-in-the-file-is-not-a-field-the-object-carries).");
                Assert.AreEqual(FullAt, loadedHi, 1e-4f,
                    $"{Path.GetFileName(rel)} loads _SwellReadSeaStateHi {loadedHi:0.###}. The " +
                    "ruling moved the onset only; the full threshold stays 0.45, and if the pair " +
                    "ever inverts the shader's max() guard turns the fade into a hard step.");
            }
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>
        /// ⚠️ <b>THE SECOND READER, stated in numbers rather than discovered later.</b>
        /// <c>SwashSeaStateGate()</c> deliberately reuses this same pair for the shore's calm fade —
        /// one axis, one place to tune — so lowering the onset also lets the swash wash harder on a
        /// calm sea. Direction and floor are asserted; the magnitude is reported, because it rides
        /// <c>_SwashCalmGate</c>, which the owner tunes per mood and this ruling did not touch.
        /// </summary>
        [Test]
        public void TheRuling_AlsoLetsTheSwashWashHarderOnACalmSea_WhichIsTheSharedAxisWorking()
        {
            var water = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterials[0]);
            Assert.IsNotNull(water, "Water.mat must load");
            float calmGate = water.GetFloat("_SwashCalmGate");
            float floor = 1f - calmGate;

            var report = new StringBuilder();
            report.AppendLine($"  _SwashCalmGate {calmGate:0.###} -> swash floor {floor:0.###}");
            report.AppendLine("  sea state | swash gate was | swash gate now");
            foreach (float sea in new[] { CalmSeaTheRulingWasPricedAt, SeaAtHisMidday, SeaAtHisEvening })
            {
                float was = HiddenHarbours.Art.WaterSurface.SwashSeaStateGate(sea, SupersededOnset, FullAt, calmGate);
                float now = HiddenHarbours.Art.WaterSurface.SwashSeaStateGate(sea, RuledOnset, FullAt, calmGate);
                report.AppendLine($"  {sea,9:0.000} | {was,14:0.000} | {now,14:0.000}");

                Assert.GreaterOrEqual(now, was - 1e-6f,
                    $"at sea state {sea:0.000} the swash must not wash LESS than it did before the " +
                    "onset was lowered — the gate is monotone in the onset, and if this reverses the " +
                    "two readers have stopped sharing an axis");
                Assert.GreaterOrEqual(now, floor - 1e-6f,
                    "the swash gate must never fall below its calm floor");
                Assert.LessOrEqual(now, 1f + 1e-6f, "…nor rise above full wash");
            }
            TestContext.WriteLine(report.ToString());

            float wasCalmest = HiddenHarbours.Art.WaterSurface.SwashSeaStateGate(
                CalmSeaTheRulingWasPricedAt, SupersededOnset, FullAt, calmGate);
            float nowCalmest = HiddenHarbours.Art.WaterSurface.SwashSeaStateGate(
                CalmSeaTheRulingWasPricedAt, RuledOnset, FullAt, calmGate);

            Assert.AreEqual(floor, wasCalmest, 1e-5f,
                "before the ruling the 0.143 sea it was priced on sat exactly on the swash's calm floor");
            Assert.Greater(nowCalmest, wasCalmest,
                "⭐ AND AFTER IT, IT DOES NOT. This is the shared-axis consequence of the ruling: the " +
                "shore's calm fade keys the SAME two thresholds as the swell read (shader " +
                "SwashSeaStateGate, ~line 2975), so the owner bought a slightly livelier calm shore " +
                "along with a legible midday swell. If he wants the old shore back the knob is " +
                "_SwashCalmGate, NOT the onset — moving the onset back would undo row 25.");
        }

        /// <summary>The text of the line immediately preceding <paramref name="index"/>.</summary>
        static string LineBefore(string text, int index)
        {
            int lineStart = text.LastIndexOf('\n', index > 0 ? index - 1 : 0);
            if (lineStart <= 0) return "(start of file)";
            int prevStart = text.LastIndexOf('\n', lineStart - 1);
            return text.Substring(prevStart + 1, lineStart - prevStart - 1);
        }

        /// <summary>Reads a <c>Range(...)</c> property's default straight out of the ShaderLab source.</summary>
        static float RangeDefault(string src, string key)
        {
            var m = Regex.Match(src, Regex.Escape(key) + @"\s*\(""[^""]*"",\s*Range\([^)]*\)\)\s*=\s*(-?[\d.]+)");
            Assert.IsTrue(m.Success, $"{key} has no Range default in {ShaderPath}");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }
}
