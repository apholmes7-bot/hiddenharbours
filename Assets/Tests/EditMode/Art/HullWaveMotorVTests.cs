using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A boat under motor lays a V</b> (the owner, 11-21: "the dory wake looks much better, make motored
    /// boats a v wake"). Each ring a motored hull lays carries the way she was going when it was laid, and
    /// the water keeps only the arcs of it that touch the arms of her V: the inside of the V and the water
    /// ahead and astern of each ring stay calm. A ROWED hull (the dory under oars, the owner's approved
    /// look) lays exactly the rings she did, bit for bit.
    /// </summary>
    public class HullWaveMotorVTests
    {
        const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        const float PuntLength = 5.2f, CapeLength = 12f;

        static HullWaveSettings S => HullWaveSettings.Default;

        static float ArmAngleDegrees => 90f - S.KelvinHalfAngleDegrees;

        static float GainAt(float degreesFromTravel, in HullWaveSettings s)
            => HullWaveMath.VArmGain(Mathf.Cos(degreesFromTravel * Mathf.Deg2Rad), HullWaveMath.VArmWindow(in s), s.MotorVShare);

        // ==== 1. THE ARM WINDOW =======================================================================

        [Test]
        public void TheGain_PeaksWhereARingTouchesTheArms_AndIsNothingAheadOrAstern()
        {
            HullWaveSettings s = S;
            Assert.Greater(GainAt(ArmAngleDegrees, in s), 0.9f, "the arc that touches an arm keeps its height");
            Assert.AreEqual(0f, GainAt(0f, in s), 1e-6f, "straight ahead of a ring: calm");
            Assert.AreEqual(0f, GainAt(180f, in s), 1e-6f, "straight astern of a ring (inside her V): calm");
            Assert.AreEqual(0f, GainAt(ArmAngleDegrees + s.MotorVArmHalfWidthDegrees + 1f, in s), 1e-6f,
                "past the window's edge: calm");
            for (float deg = 0f; deg <= 180f; deg += 1f)
            {
                float g = GainAt(deg, in s);
                Assert.GreaterOrEqual(g, 0f); Assert.LessOrEqual(g, 1f + 1e-6f);
            }
        }

        [Test]
        public void TheGain_FallsSmoothlyToTheWindowsEdges()
        {
            HullWaveSettings s = S;
            float prev = GainAt(ArmAngleDegrees + s.MotorVArmHalfWidthDegrees, in s);
            Assert.AreEqual(0f, prev, 1e-5f);
            // From the far edge in to the peak, it only rises: no ripple in the window, so no banding.
            for (float deg = ArmAngleDegrees + s.MotorVArmHalfWidthDegrees; deg >= ArmAngleDegrees + 3f; deg -= 0.5f)
            {
                float g = GainAt(deg, in s);
                Assert.GreaterOrEqual(g, prev - 1e-6f, $"at {deg} deg");
                prev = g;
            }
        }

        [Test]
        public void ShareZero_DrawsWholeRings_AndShareBlendsBetween()
        {
            HullWaveSettings off = S;
            off.MotorVShare = 0f;
            for (float deg = 0f; deg <= 180f; deg += 15f) Assert.AreEqual(1f, GainAt(deg, in off), 0f);
            HullWaveSettings half = S;
            half.MotorVShare = 0.5f;
            Assert.AreEqual(0.5f, GainAt(180f, in half), 1e-6f, "half way: half the inside stays");
        }

        // ==== 2. THE PACKED DIRECTION =================================================================

        [Test]
        public void TheTravel_PacksIntoOneFloat_AndBackWithinAThousandth()
        {
            Assert.AreEqual(0f, HullWaveMath.PackTravel(Vector2.zero), 0f, "no travel packs as 0: a whole ring");
            Assert.AreEqual(Vector2.zero, HullWaveMath.UnpackTravel(0f));
            for (int i = 0; i < 360; i += 7)
            {
                float rad = i * Mathf.Deg2Rad;
                Vector2 u = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                float packed = HullWaveMath.PackTravel(u * 3.7f);
                Assert.Greater(packed, 0.5f);
                Assert.AreEqual(packed, Mathf.Round(packed), 0f, "a whole number");
                Assert.Less(packed, 16777216f, "exact in a float");
                Vector2 back = HullWaveMath.UnpackTravel(packed);
                Assert.AreEqual(u.x, back.x, 1.1e-3f, $"x at {i} deg");
                Assert.AreEqual(u.y, back.y, 1.1e-3f, $"y at {i} deg");
            }
        }

        // ==== 3. THE CHAIN ============================================================================

        static HullWaveSource[] Run(float length, Vector2 travel, in HullWaveSettings s, float speed, bool legacy = false)
        {
            var field = new HullWaveField();
            field.Limit = s.MaxSources;
            var chain = new HullWaveChain();
            for (int i = 0; i <= 240; i++)
            {
                double now = 50.0 + i / 60.0;
                field.Advance(now);
                Vector2 at = new Vector2(-(float)(now - 50.0) * speed, 0f);
                if (legacy) chain.Step(field, at, now, speed, length, 1f, 15, 9, in s);
                else chain.Step(field, at, now, speed, length, 1f, 15, 9, travel, in s);
            }
            var all = new HullWaveSource[field.Count];
            for (int i = 0; i < all.Length; i++) all[i] = field[i];
            return all;
        }

        [Test]
        public void ARowedHull_LaysExactlyTheRingsSheDid()
        {
            HullWaveSource[] old = Run(4.5f, Vector2.zero, S, 1.65f, legacy: true);
            HullWaveSource[] now = Run(4.5f, Vector2.zero, S, 1.65f);
            Assert.Greater(old.Length, 3);
            Assert.AreEqual(old.Length, now.Length);
            for (int i = 0; i < old.Length; i++)
            {
                Assert.AreEqual(old[i].Position, now[i].Position);
                Assert.AreEqual(old[i].BirthSeconds, now[i].BirthSeconds);
                Assert.AreEqual(old[i].Amplitude, now[i].Amplitude);
                Assert.AreEqual(old[i].Wavenumber, now[i].Wavenumber);
                Assert.AreEqual(old[i].PhaseOffset, now[i].PhaseOffset);
                Assert.AreEqual(old[i].Arcs, now[i].Arcs);
                Assert.AreEqual(Vector2.zero, now[i].Travel, "a rowed ring carries no direction");
            }
        }

        [Test]
        public void AMotoredShortHull_LaysCleanVRings_NotTheDorysScatteredArcs()
        {
            HullWaveSource[] v = Run(PuntLength, Vector2.left * 2f, S, 6f);
            Assert.Greater(v.Length, 3);
            HullWaveSettings plain = S;
            plain.SmallHullLengthMetres = 0f;
            HullWaveSource[] longHull = Run(PuntLength, Vector2.zero, plain, 6f);
            Assert.AreEqual(longHull.Length, v.Length, "spaced as a long hull is, not at the dory's close spacing");
            foreach (HullWaveSource r in v)
            {
                Assert.IsFalse(r.Arcs, "no broken arcs: the V window draws her");
                Assert.AreEqual(0f, r.PhaseOffset, 0f, "no scatter");
                Assert.AreEqual(Vector2.left, r.Travel, "the way she was going, as a unit vector");
                Assert.AreEqual(0f, r.Position.y, 1e-6f, "laid on her track");
            }
        }

        [Test]
        public void ShareZero_KeepsTheOldRings_ForAMotoredHullToo()
        {
            HullWaveSettings off = S;
            off.MotorVShare = 0f;
            HullWaveSource[] old = Run(PuntLength, Vector2.zero, off, 6f, legacy: true);
            HullWaveSource[] now = Run(PuntLength, Vector2.left, off, 6f);
            Assert.AreEqual(old.Length, now.Length);
            for (int i = 0; i < old.Length; i++)
            {
                Assert.AreEqual(old[i].Amplitude, now[i].Amplitude);
                Assert.AreEqual(old[i].Arcs, now[i].Arcs);
                Assert.AreEqual(Vector2.zero, now[i].Travel, "the dial off lays no direction");
            }
        }

        // ==== 4. THE ENVELOPE =========================================================================

        static HullWaveField Laid(float length, float speed, in HullWaveSettings s)
        {
            var field = new HullWaveField();
            field.Limit = s.MaxSources;
            field.VArms = HullWaveMath.VArmsVector(in s);
            var chain = new HullWaveChain();
            for (int i = 0; i <= 600; i++)
            {
                double now = 50.0 + i / 60.0;
                field.Advance(now);
                chain.Step(field, new Vector2(-(float)(now - 50.0) * speed, 0f), now, speed, length, 1f, 15, 9,
                           Vector2.left, in s);
            }
            return field;
        }

        // Mean |height| over points in her own frame: k = |y| / (x·tan α) across the V (1 = on an arm).
        static float Band(HullWaveField field, Vector2 stern, float reach, float kLo, float kHi, float spread)
        {
            float tanA = Mathf.Tan(S.KelvinHalfAngleDegrees * Mathf.Deg2Rad);
            float sum = 0f; int n = 0;
            for (float x = 2f; x < reach; x += 0.37f)
                for (float y = -reach; y < reach; y += 0.23f)
                {
                    float k = Mathf.Abs(y) / (x * tanA);
                    if (k < kLo || k >= kHi) continue;
                    sum += Mathf.Abs(field.SampleHeight(stern + new Vector2(x, y), spread));
                    n++;
                }
            return n > 0 ? sum / n : 0f;
        }

        [Test]
        public void TheVsArms_CarryTheWaves_AndItsInsideIsCalm()
        {
            foreach (float length in new[] { PuntLength, CapeLength })
            {
                HullWaveSettings s = S;
                float speed = 5f;
                HullWaveField v = Laid(length, speed, in s);
                Vector2 stern = new Vector2(-10f * speed, 0f);
                float reach = 3f * length;
                float arms = Band(v, stern, reach, 0.75f, 1.25f, s.SpreadMetres);
                float inside = Band(v, stern, reach, 0f, 0.45f, s.SpreadMetres);
                Assert.Greater(arms, 0f, $"{length} m: her arms draw");
                Assert.Less(inside, 0.25f * arms, $"{length} m: the inside of her V is calm (inside {inside}, arms {arms})");

                HullWaveSettings rings = s;
                rings.MotorVShare = 0f;
                HullWaveField whole = Laid(length, speed, in rings);
                float wholeInside = Band(whole, stern, reach, 0f, 0.45f, s.SpreadMetres);
                Assert.Less(inside, 0.25f * wholeInside, $"{length} m: whole rings filled the V; the V does not");
            }
        }

        [Test]
        public void NoArmsSet_TheFieldSamplesWholeRings()
        {
            var field = new HullWaveField();
            Assert.AreEqual(Vector3.zero, field.VArms, "unset: share 0, every ring whole (as the shader's unset global)");
        }

        // ==== 5. THE PLUMBING =========================================================================

        static string Squashed() => Regex.Replace(
            File.ReadAllText(Path.Combine(Application.dataPath, "..", ShaderPath)), @"\s+", "");

        static float Define(string name)
        {
            string src = File.ReadAllText(Path.Combine(Application.dataPath, "..", ShaderPath));
            Match m = Regex.Match(src, @"(?m)^\s*#define\s+" + name + @"\s+([0-9.]+)");
            Assert.IsTrue(m.Success, name + " is not defined in the water shader");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        [Test]
        public void TheShader_DecodesTheDirectionAsTheCSharpPacksIt()
        {
            Assert.AreEqual(HullWaveMath.TravelSteps, Define("HH_HULL_WAVE_TRAVEL_STEPS"), 0f);
            Assert.AreEqual(HullWaveMath.TravelHalfRange, Define("HH_HULL_WAVE_TRAVEL_HALF"), 0f);
            string src = Squashed();
            StringAssert.Contains("float4_HHHullWaveV;", src);
            foreach (string piece in new[]
                     {
                         "if(packed<0.5)return1.0;",
                         "floatp=packed-1.0;",
                         "floatqx=floor(p/HH_HULL_WAVE_TRAVEL_STEPS);",
                         "float2u=float2(qx,p-qx*HH_HULL_WAVE_TRAVEL_STEPS)/HH_HULL_WAVE_TRAVEL_HALF-1.0;",
                         "floatt=saturate((dot(d,u)/max(r,1e-4)-_HHHullWaveV.x)*_HHHullWaveV.y);",
                         "floatbump=4.0*t*(1.0-t);",
                         "returnlerp(1.0,bump*bump,saturate(_HHHullWaveV.z));",
                     })
                StringAssert.Contains(piece, src, "the shader's V window moved away from HullWaveMath");
        }

        [Test]
        public void TheSourceLoop_KeepsItsReachAndAppliesTheWindow()
        {
            string src = Squashed();
            StringAssert.Contains("floatreach=a.w+HH_HULL_WAVE_PACKET_REACH/abs(b.z);", src,
                "the reach is the band radius plus the packet's cut, as HullWaveField.Pack had sent it");
            StringAssert.Contains("if(r2>=reach*reach)continue;", src);
            StringAssert.Contains("arc*=HullWaveVGain(d,r,b.w);", src);
            StringAssert.Contains("floatarc=HullWaveArcGain(d,a.xy,b.z);", src);
        }

        [Test]
        public void ThePack_SendsTheDirectionInB_W()
        {
            var field = new HullWaveField();
            var motor = new HullWaveSource
            {
                Position = Vector2.zero, BirthSeconds = 1.0, Amplitude = 0.1f, Wavenumber = Mathf.PI, PhaseSpeed = 1f,
                RingSpeed = 1f, PacketWidth = 2f, Lifetime = 8f, DecaySeconds = 4f, Travel = Vector2.up,
            };
            var rowed = motor;
            rowed.Travel = Vector2.zero;
            rowed.Position = new Vector2(1f, 0f);
            field.Advance(1.0);
            field.Add(in motor);
            field.Add(in rowed);
            field.Advance(2.0);
            var a = new Vector4[4]; var b = new Vector4[4];
            Assert.AreEqual(2, field.Pack(a, b, new Rect(-50f, -50f, 100f, 100f), true));
            Assert.AreEqual(HullWaveMath.PackTravel(Vector2.up), b[0].w, 0f);
            Assert.AreEqual(0f, b[1].w, 0f, "a whole ring sends 0");
            Assert.AreEqual(a[0].w + HullWaveMath.PacketReach * motor.PacketWidth,
                            a[0].w + HullWaveMath.PacketReach / Mathf.Abs(b[0].z), 1e-5f, "the shader's reach is the old one");
        }

        [Test]
        public void TheBridge_PublishesTheWindow()
        {
            Assert.AreEqual(Shader.PropertyToID("_HHHullWaveV"), HiddenHarbours.Art.HullWaveBridge.IdV);
            HullWaveSettings s = S;
            Vector2 w = HullWaveMath.VArmWindow(in s);
            Vector3 v = HullWaveMath.VArmsVector(in s);
            Assert.AreEqual(w.x, v.x, 0f); Assert.AreEqual(w.y, v.y, 0f); Assert.AreEqual(s.MotorVShare, v.z, 0f);
        }

        [Test]
        public void TheDials_AreTheOwnersInGameConfig_AndTheCodeDefaultsMatch()
        {
            string yaml = File.ReadAllText(Path.Combine(Application.dataPath, "..", ConfigPath)).Replace("\r\n", "\n");
            Assert.AreEqual(S.MotorVShare, YamlFloat(yaml, "MotorVShare"), 0f);
            Assert.AreEqual(S.MotorVArmHalfWidthDegrees, YamlFloat(yaml, "MotorVArmHalfWidthDegrees"), 0f);
        }

        static float YamlFloat(string yaml, string key)
        {
            Match m = Regex.Match(yaml, @"(?m)^\s+" + key + @":\s*([-0-9.eE]+)\s*$");
            Assert.IsTrue(m.Success, key + " is not in GameConfig");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }
}
