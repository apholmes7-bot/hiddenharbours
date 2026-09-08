using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>"IT FEELS LIKE LAYERS" AT MIDDAY AND "REALLY GOOD IN THE EVENING" — the same water, and
    /// the difference is a gate that is ZERO at solar noon by construction.</b>
    ///
    /// <para>Owner, 2026-09-08, Nine Mile Creek beach. At <b>13:12–13:24</b>: <i>"the vertical white
    /// water details vs the horizontal from nw to se water details, they dont seem to work with one
    /// another… it just still doesnt feel like one coherent surface, it feels like layers."</i> At
    /// <b>15:51</b>, same beach, sun low: <i>"it actually looks really good in the evening, its making
    /// me rethink what i just said."</i></para>
    ///
    /// <para><b>The swell's only two shading terms are both gated, and at 13:12 both are shut.</b> The
    /// shader multiplies each by <c>swellReadGate</c>, and the sun side by an elevation gate as well:</para>
    /// <code>
    ///     float e        = _SunElevation;
    ///     float elevGate = smoothstep(0.0, 0.12, e) * (1 - e*e) * (1 - e*e);
    ///     amt = faceSigned * _SunSideStrength * elevGate * swellReadGate;
    /// </code>
    /// <para><c>(1 − e²)²</c> is <b>exactly 0 at solar noon</b> — a high sun has no side, which is right,
    /// and is PR 9's deliberate design (register row 11). Sunrise 6 / sunset 20 puts solar noon at
    /// <b>13:00</b>. <b>The owner was at 13:12 — twelve minutes from the one moment in the day when this
    /// term is switched off.</b></para>
    ///
    /// <para>So the complaint and the compliment are the same sea with the swell's face shading OFF and
    /// ON. That reframes the row: at midday nothing shades the swell at all, so every white layer is
    /// drawn over a FLAT colour field and reads as sitting on top of it. <b>The defect at noon is the
    /// ABSENCE of a surface cue, not the presence of a bad one</b> — which is the same family as
    /// register rows 6 and 25.</para>
    ///
    /// <para>Pure arithmetic: the shader's own gate, transcribed, with the day model stated.</para>
    /// </summary>
    public class SwellFaceGateTests
    {
        // The shipped day model (DayNightProfile): sunrise 06:00, sunset 20:00, so solar noon is 13:00.
        const float Sunrise = 6f, Sunset = 20f;
        static float SolarNoon => 0.5f * (Sunrise + Sunset);

        /// <summary><c>_SunElevation</c> as the day/night bridge forms it: <c>cos(solarX · π/2)</c>,
        /// where solarX is the signed fraction of the half-day away from solar noon.</summary>
        static float SunElevation(float hour)
        {
            float half = Mathf.Max(1e-3f, Sunset - SolarNoon);
            float solarX = Mathf.Clamp((hour - SolarNoon) / half, -1f, 1f);
            return Mathf.Cos(Mathf.Abs(solarX) * Mathf.PI * 0.5f);
        }

        /// <summary>The shader's <c>elevGate</c>, verbatim.</summary>
        static float ElevGate(float hour)
        {
            float e = SunElevation(hour);
            float sinSq = 1f - e * e;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / 0.12f)) * sinSq * sinSq;
        }

        [Test]
        public void TheSunSideGate_IsZeroAtSolarNoon_AndTheOwnerWasTwelveMinutesFromIt()
        {
            var report = new StringBuilder();
            report.AppendLine("THE SWELL'S FACE — the sun-side elevation gate through the owner's afternoon");
            report.AppendLine($"  sunrise {Sunrise:0}:00, sunset {Sunset:0}:00 -> solar noon {SolarNoon:0}:00");
            report.AppendLine();
            report.AppendLine("  time    | sun elev | elevGate  | vs 15:51");

            float at1312 = ElevGate(13.2f), at1551 = ElevGate(15.85f);
            foreach (var (label, h) in new[]
                     { ("12:00", 12f), ("13:12", 13.2f), ("13:24", 13.4f), ("14:30", 14.5f),
                       ("15:51", 15.85f), ("17:00", 17f), ("19:00", 19f) })
            {
                float g = ElevGate(h);
                report.AppendLine($"  {label,-7} | {SunElevation(h),8:0.000} | {g,9:0.00000} | " +
                                  $"{(at1551 > 0 ? g / at1551 : 0f),7:0.000}x");
            }
            report.AppendLine();
            report.AppendLine("  (1 - e^2)^2 is EXACTLY 0 at solar noon: a high sun has no side. That is");
            report.AppendLine("  PR 9's design (register row 11), not a defect — but it means the swell has");
            report.AppendLine("  no face shading at all within minutes of 13:00.");
            TestContext.WriteLine(report.ToString());

            Assert.Less(ElevGate(SolarNoon), 1e-6f,
                "the gate must be zero AT solar noon — that is what 'a high sun has no side' means");

            Assert.Greater(at1551 / Mathf.Max(at1312, 1e-9f), 100f,
                "⭐ THE DISCRIMINATOR: the sun-side term must be orders of magnitude stronger at 15:51 " +
                "than at 13:12. The owner's two readings of the SAME water are the same sea with this " +
                "gate shut and open, which is why the evening reads as one surface and the midday as " +
                "layers.");

            Assert.Greater(at1551, 0.05f,
                "...and at 15:51 it must be genuinely open, or the evening's coherence is something " +
                "else and this whole reading is wrong.");
        }

        /// <summary>
        /// ⚠️ <b>THE DEAD CONTROL, and it is what stops this becoming "blame the sun gate".</b> The gate
        /// is symmetric about solar noon, so a MORNING hour the same distance from 13:00 must read the
        /// same as the afternoon one. If the owner's midday complaint were about the sun side alone, the
        /// sea would look equally layered at 10:09 — and equally good at 10:24. That is a prediction the
        /// plate can falsify, and it is the honest limit of this fixture: it names a term that changes
        /// between his two readings, not the only term that does.
        /// </summary>
        [Test]
        public void TheGateIsSymmetric_SoTheMorningIsAFalsifiablePrediction()
        {
            float afternoon = ElevGate(15.85f);
            float morning = ElevGate(SolarNoon - (15.85f - SolarNoon));   // 10:09
            Assert.AreEqual(afternoon, morning, afternoon * 0.02f,
                "DEAD CONTROL: the elevation gate knows nothing about morning or evening. If the sea " +
                "reads coherent at 15:51 but NOT at 10:09, the sun side is not the whole story and the " +
                "difference is in the warm TINT, not in the gate.");
            TestContext.WriteLine($"  10:09 and 15:51 both gate at {afternoon:0.000} — the prediction the " +
                                  "plate must test: does the morning read as coherent as the evening?");
        }
    }
}
