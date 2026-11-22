using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A rowed dory's wake is disturbed water, not a pattern</b> (the owner's look of 11-19: the rings
    /// astern read as a chain of stacked loops, "calculated" and "structured"). A short hull's rings each take
    /// their own height, wavelength and crest phase, and are laid a little unevenly, all fixed from the time
    /// each was laid, so the same voyage draws the same water on every run (rule 5). Longer hulls, the lobster
    /// boat among them, lay exactly the rings they did. A short rowed hull's churned trail is a thinner share
    /// too; a motored hull's trail is halved (MotorVTrailFoamScale) so her bow's V reads over it.
    /// </summary>
    public class HullWaveScatterTests
    {
        const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const float DoryLength = 4.5f, LobsterLength = 12f;

        static HullWaveSettings S => HullWaveSettings.Default;

        static HullWaveSettings Off()
        {
            HullWaveSettings off = S;
            off.SmallHullHeightScatter = 0f;
            off.SmallHullWavelengthScatter = 0f;
            off.SmallHullPhaseScatter = 0f;
            off.SmallHullSpacingScatter = 0f;
            off.SmallHullCentreScatterMetres = 0f;
            off.SmallHullArcShare = 0f;
            return off;
        }

        // A straight row at a steady speed, laid frame by frame, and every live source read back.
        static HullWaveSource[] Row(float length, in HullWaveSettings s, float fps, float seconds = 4f, float speed = 1.65f)
        {
            var field = new HullWaveField();
            field.Limit = s.MaxSources;
            var chain = new HullWaveChain();
            int frames = Mathf.RoundToInt(seconds * fps);
            for (int i = 0; i <= frames; i++)
            {
                double now = 100.0 + i / (double)fps;
                field.Advance(now);
                chain.Step(field, new Vector2((float)(now - 100.0) * speed, 0f), now, speed, length, 1f, 15, 7, in s);
            }
            var all = new HullWaveSource[field.Count];
            for (int i = 0; i < all.Length; i++) all[i] = field[i];
            return all;
        }

        static HullWaveSource Ring(double birth)
        {
            return new HullWaveSource
            {
                Position = new Vector2(3f, -2f), BirthSeconds = birth, Amplitude = 0.04f, Wavenumber = 10f,
                PhaseSpeed = 1.65f, RingSpeed = 0.55f, PacketWidth = 0.48f, Lifetime = 3f, DecaySeconds = 5.4f,
            };
        }

        [Test]
        public void TheScatter_IsAFixedNumberPerRing_InRange()
        {
            for (int i = 0; i < 500; i++)
            {
                double t = 1234.5 + i * 0.173;
                for (int ch = 0; ch < 4; ch++)
                {
                    float u = HullWaveMath.Scatter01(t, 1f, ch);
                    Assert.That(u, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                    Assert.AreEqual(u, HullWaveMath.Scatter01(t, 1f, ch), 0f, "the same ring gives the same number");
                }
            }
            Assert.AreNotEqual(HullWaveMath.Scatter01(10.0, 1f, 0), HullWaveMath.Scatter01(10.0, 0.7f, 0), "the bow's chain differs");
            Assert.AreNotEqual(HullWaveMath.Scatter01(10.0, 1f, 0), HullWaveMath.Scatter01(10.0, 1f, 2), "the dials differ");
        }

        [Test]
        public void ADorysRings_EachDiffer_WithinTheDials()
        {
            HullWaveSource[] rings = Row(DoryLength, S, 60f);
            Assert.Greater(rings.Length, 6, "premise: she lays a trail of rings");
            float minAmp = float.MaxValue, maxAmp = 0f, minK = float.MaxValue, maxK = 0f;
            int offPhase = 0;
            HullWaveSource plain = Row(DoryLength, Off(), 60f)[0];
            foreach (HullWaveSource r in rings)
            {
                minAmp = Mathf.Min(minAmp, r.Amplitude); maxAmp = Mathf.Max(maxAmp, r.Amplitude);
                minK = Mathf.Min(minK, r.Wavenumber); maxK = Mathf.Max(maxK, r.Wavenumber);
                Assert.LessOrEqual(Mathf.Abs(r.PhaseOffset), S.SmallHullPhaseScatter * Mathf.PI + 1e-5f);
                if (Mathf.Abs(r.PhaseOffset) > 0.3f) offPhase++;
                // The band keeps its width in wavelengths: the ring is the same shape, a different size.
                Assert.AreEqual(plain.PacketWidth * plain.Wavenumber, r.PacketWidth * r.Wavenumber, 1e-3f);
            }
            Assert.Greater(maxAmp - minAmp, 0.2f * maxAmp, "their heights differ");
            Assert.GreaterOrEqual(minAmp, plain.Amplitude * (1f - S.SmallHullHeightScatter) - 1e-6f, "never below the dial");
            Assert.LessOrEqual(maxAmp, plain.Amplitude + 1e-6f, "never above her crest");
            Assert.Greater(maxK / minK, 1.1f, "their wavelengths differ");
            Assert.Greater(offPhase, rings.Length / 3, "their crests are out of step");
        }

        [Test]
        public void ADorysRings_AreLaidUnevenly_ButNeverOverHerBudget()
        {
            HullWaveSource[] rings = Row(DoryLength, S, 60f, 10f);
            float minGap = float.MaxValue, maxGap = 0f;
            for (int i = 1; i < rings.Length; i++)
            {
                float gap = (float)(rings[i].BirthSeconds - rings[i - 1].BirthSeconds);
                minGap = Mathf.Min(minGap, gap); maxGap = Mathf.Max(maxGap, gap);
            }
            Assert.Greater(maxGap / minGap, 1.2f, "the gaps differ");
            Assert.LessOrEqual(rings.Length, 15, "never more than her budget alive");
            Assert.AreEqual(1f, HullWaveMath.SpacingScatter(5.0, DoryLength, 1f, Off()), 0f, "0 = evenly");
            for (int i = 0; i < 200; i++)
            {
                float f = HullWaveMath.SpacingScatter(i * 0.37, DoryLength, 1f, S);
                Assert.That(f, Is.GreaterThanOrEqualTo(1f).And.LessThanOrEqualTo(1f + 2f * S.SmallHullSpacingScatter + 1e-6f));
            }
        }

        [Test]
        public void TheSameRow_LaysTheSameRings_EveryRun()
        {
            HullWaveSource[] a = Row(DoryLength, S, 60f), b = Row(DoryLength, S, 60f);
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].BirthSeconds, b[i].BirthSeconds, 0.0);
                Assert.AreEqual(a[i].Amplitude, b[i].Amplitude, 0f);
                Assert.AreEqual(a[i].Wavenumber, b[i].Wavenumber, 0f);
                Assert.AreEqual(a[i].PhaseOffset, b[i].PhaseOffset, 0f);
                Assert.AreEqual(a[i].Position.x, b[i].Position.x, 0f);
            }
        }

        [Test]
        public void TheSameRow_LaysTheSameRings_AtAnyFrameRate()
        {
            HullWaveSource[] a = Row(DoryLength, S, 30f), b = Row(DoryLength, S, 144f);
            Assert.AreEqual(a.Length, b.Length, "the same number of rings");
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].BirthSeconds, b[i].BirthSeconds, 1e-6, "laid at the same time");
                Assert.AreEqual(a[i].Amplitude, b[i].Amplitude, 1e-6f, "the same height");
                Assert.AreEqual(a[i].PhaseOffset, b[i].PhaseOffset, 1e-6f, "the same phase");
                Assert.AreEqual(a[i].Position.x, b[i].Position.x, 1e-3f, "in the same place");
            }
        }

        [Test]
        public void TheLobsterBoat_LaysExactlyTheRingsSheDid()
        {
            Assert.Greater(LobsterLength, S.SmallHullLengthMetres, "premise: she is not a small hull");
            HullWaveSource[] on = Row(LobsterLength, S, 60f, 4f, 4f), off = Row(LobsterLength, Off(), 60f, 4f, 4f);
            Assert.AreEqual(off.Length, on.Length);
            for (int i = 0; i < on.Length; i++)
            {
                Assert.AreEqual(off[i].BirthSeconds, on[i].BirthSeconds, 0.0);
                Assert.AreEqual(off[i].Amplitude, on[i].Amplitude, 0f);
                Assert.AreEqual(off[i].Wavenumber, on[i].Wavenumber, 0f);
                Assert.AreEqual(0f, on[i].PhaseOffset, 0f);
            }
            HullWaveSource ring = Ring(50.0);
            HullWaveSource same = HullWaveMath.ScatterRing(in ring, LobsterLength, 1f, S);
            Assert.AreEqual(ring.Amplitude, same.Amplitude, 0f);
            Assert.AreEqual(ring.Wavenumber, same.Wavenumber, 0f);
            Assert.AreEqual(0f, same.PhaseOffset, 0f);
            Assert.AreEqual(ring.Position, same.Position, "her rings stay on her track");
            Assert.IsFalse(same.Arcs, "her rings stay whole");
            foreach (HullWaveSource r in on) Assert.IsFalse(r.Arcs);
            Assert.AreEqual(S.TrailFoamShare * 0.8f, HullWaveMath.TrailFoamStrength(0.8f, true, LobsterLength, S), 1e-6f,
                "her trail keeps its share");
        }

        [Test]
        public void ARingsPhaseOffset_IsWhatTheWaterDraws_AndWhatTheRideReads()
        {
            var field = new HullWaveField();
            HullWaveSource r = Ring(10.0);
            r.PhaseOffset = 1.1f;
            field.Advance(10.0);
            field.Add(in r);
            field.Advance(10.8);
            var a = new Vector4[4]; var b = new Vector4[4];
            Assert.AreEqual(1, field.Pack(a, b, new Rect(-50f, -50f, 100f, 100f), true));
            Assert.AreEqual(HullWaveMath.Phase(r.Wavenumber, r.PhaseSpeed, 0.8, 1.1f), b[0].y, 1e-6f, "packed with its offset");
            Assert.That(b[0].y, Is.GreaterThanOrEqualTo(0f).And.LessThan(2f * Mathf.PI));
            float age = 0.8f, front = r.RingSpeed * age;
            Vector2 at = r.Position + new Vector2(front, 0f);
            float expected = HullWaveMath.SourceHeight(front, r.AmplitudeAt(10.8), front, r.Wavenumber, b[0].y,
                                                       1f / r.PacketWidth, 1f / S.SpreadMetres);
            Assert.AreEqual(expected, field.SampleHeight(at, S.SpreadMetres), 1e-5f, "the ride reads the same crest");
            Assert.AreEqual(HullWaveMath.Phase(10f, 1.65f, 0.8), HullWaveMath.Phase(10f, 1.65f, 0.8, 0f), 0f, "0 = the older phase");
        }

        [Test]
        public void ADorysRings_DoNotShareACentre_AndAreDrawnAsBrokenArcs()
        {
            HullWaveSettings whole = S;
            whole.SmallHullCentreScatterMetres = 0f;
            whole.SmallHullArcShare = 0f;
            HullWaveSource[] on = Row(DoryLength, S, 60f), off = Row(DoryLength, whole, 60f);
            Assert.AreEqual(off.Length, on.Length, "the same rings, only moved and broken");
            float most = 0f;
            for (int i = 0; i < on.Length; i++)
            {
                Vector2 moved = on[i].Position - off[i].Position;
                Assert.LessOrEqual(Mathf.Abs(moved.x), S.SmallHullCentreScatterMetres + 1e-5f, "within the dial");
                Assert.LessOrEqual(Mathf.Abs(moved.y), S.SmallHullCentreScatterMetres + 1e-5f, "within the dial");
                most = Mathf.Max(most, moved.magnitude);
                Assert.IsTrue(on[i].Arcs, "a short hull's ring is drawn as arcs");
                Assert.IsFalse(off[i].Arcs, "0 = whole rings");
            }
            Assert.Greater(most, 0.3f * S.SmallHullCentreScatterMetres, "her rings are laid off her track");
        }

        [Test]
        public void TheArcFlag_IsPackedAsTheSignOfTheWidth_AndTheDialsAsAGlobal()
        {
            var field = new HullWaveField();
            HullWaveSource whole = Ring(10.0), arcs = Ring(10.0);
            arcs.Position += new Vector2(1f, 0f);
            arcs.Arcs = true;
            field.Advance(10.0);
            field.Add(in whole);
            field.Add(in arcs);
            field.Advance(10.5);
            var a = new Vector4[4]; var b = new Vector4[4];
            Assert.AreEqual(2, field.Pack(a, b, new Rect(-50f, -50f, 100f, 100f), true));
            for (int i = 0; i < 2; i++)
            {
                bool isArcs = a[i].x > 3.5f;
                Assert.AreEqual((isArcs ? -1f : 1f) / whole.PacketWidth, b[i].z, 1e-6f, "the sign is the flag, the size the width");
            }
            HullWaveSettings shipped = S;
            Vector4 g = HiddenHarbours.Art.HullWaveBridge.ArcsVector(in shipped);
            Assert.AreEqual(S.SmallHullArcShare, g.x, 0f);
            Assert.AreEqual(Mathf.Round(S.SmallHullArcLobes), g.y, 0f);
            HullWaveSettings odd = S;
            odd.SmallHullArcLobes = 0.2f;
            Assert.AreEqual(1f, HiddenHarbours.Art.HullWaveBridge.ArcsVector(in odd).y, 0f, "never fewer than one arc");
        }

        [Test]
        public void TheShader_BreaksOnlyFlaggedRings_AndReadsTheWidthsSize()
        {
            string src = Regex.Replace(File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Art/Shaders/HiddenHarboursWater.shader")),
                                       @"\s+", "");
            StringAssert.Contains("float4_HHHullWaveArcs;", src);
            StringAssert.Contains("if(flagged>=0.0||share<=0.001)return1.0;", src, "a whole ring is untouched, bit-exact");
            StringAssert.Contains("HullWaveSourceHeight(r,a.z,a.w,b.x,b.y,abs(b.z),invSpread,h,dhdr);", src);
            StringAssert.Contains("floatarc=HullWaveArcGain(d,a.xy,b.z);", src);
            StringAssert.DoesNotContain("_Time", ExtractFunction(src, "floatHullWaveArcGain("), "fixed in the world");
        }

        static string ExtractFunction(string src, string head)
        {
            int at = src.IndexOf(head, System.StringComparison.Ordinal);
            Assert.Greater(at, 0, head + " missing");
            int end = src.IndexOf("}", at, System.StringComparison.Ordinal);
            return src.Substring(at, end - at);
        }

        // The rowed dory's trail is untouched: the same thin share it always laid, by both overloads.
        [Test]
        public void ARowedDorysTrail_IsUnchanged_AThinnerShare_ThanALongRowedOrSailedHulls()
        {
            Assert.Less(S.SmallHullTrailFoamShare, S.TrailFoamShare, "a rowed hull's trail is a lace, not a mat");
            Assert.AreEqual(0.8f * S.SmallHullTrailFoamShare, HullWaveMath.TrailFoamStrength(0.8f, true, DoryLength, S), 1e-6f);
            Assert.AreEqual(0.8f * S.SmallHullTrailFoamShare,
                HullWaveMath.TrailFoamStrength(0.8f, true, DoryLength, false, S), 1e-6f, "rowed: the motored scale never reaches her");
            Assert.AreEqual(HullWaveMath.TrailFoamStrength(0.8f, true, DoryLength, S),
                HullWaveMath.TrailFoamStrength(0.8f, true, DoryLength, false, S), "rowed: bit for bit the older law");
            Assert.AreEqual(0.8f, HullWaveMath.TrailFoamStrength(0.8f, false, DoryLength, S), 0f, "moored: unchanged");
            HullWaveSettings old = S;
            old.SmallHullTrailFoamShare = -1f;
            Assert.AreEqual(0.8f * S.TrailFoamShare, HullWaveMath.TrailFoamStrength(0.8f, true, DoryLength, old), 1e-6f,
                "below 0 = the older share");
        }

        // Owner ruling of 2026-11-21: a motored hull's stern foam trail is halved, so her bow's V arms read over
        // it. That replaces the older rule that a long hull's trail share stays above the rowed dory's: a motored
        // hull's share is now the configured scale of her base share, and may sit below the dory's. One foam, less.
        [Test]
        public void AMotoredHullsTrail_IsTheConfiguredShare_AndHerBowArmsOutReadIt()
        {
            float lobster = HullWaveMath.TrailFoamStrength(0.8f, true, LobsterLength, true, S);
            Assert.AreEqual(0.8f * S.TrailFoamShare * S.MotorVTrailFoamScale, lobster, 1e-6f, "a long motored hull: the scaled share");
            Assert.AreEqual(0.5f, S.MotorVTrailFoamScale, 0f, "the ruling: halve it");
            Assert.AreEqual(0.8f * S.SmallHullTrailFoamShare * S.MotorVTrailFoamScale,
                HullWaveMath.TrailFoamStrength(0.8f, true, DoryLength, true, S), 1e-6f, "a short motored hull: halved too");
            Assert.AreEqual(0.8f, HullWaveMath.TrailFoamStrength(0.8f, false, LobsterLength, true, S), 0f, "moored: unchanged");

            HullWaveSettings noV = S;
            noV.MotorVShare = 0f;
            Assert.AreEqual(0.8f * S.TrailFoamShare, HullWaveMath.TrailFoamStrength(0.8f, true, LobsterLength, true, noV), 1e-6f,
                "no V laid: her trail is not thinned either");

            // Her bow outweighs her stern on both channels: the V's crest share, and the foam left astern.
            Assert.Greater(S.BowShare, S.MotorVSternShare, "the bow's arms carry more crest than the stern's");
            Assert.Less(lobster, HullWaveMath.TrailFoamStrength(0.8f, true, LobsterLength, false, S), "her trail is thinner than before");
        }

        [Test]
        public void TheShippedConfig_CarriesTheScatterDials()
        {
            string yaml = File.ReadAllText(Path.Combine(Application.dataPath, "..", ConfigPath)).Replace("\r\n", "\n");
            HullWaveSettings d = S;
            foreach (var (name, value) in new[]
                     {
                         ("SmallHullHeightScatter", d.SmallHullHeightScatter),
                         ("SmallHullWavelengthScatter", d.SmallHullWavelengthScatter),
                         ("SmallHullPhaseScatter", d.SmallHullPhaseScatter),
                         ("SmallHullSpacingScatter", d.SmallHullSpacingScatter),
                         ("SmallHullTrailFoamShare", d.SmallHullTrailFoamShare),
                         ("SmallHullCentreScatterMetres", d.SmallHullCentreScatterMetres),
                         ("SmallHullArcShare", d.SmallHullArcShare),
                         ("SmallHullArcLobes", d.SmallHullArcLobes),
                     })
            {
                var m = Regex.Match(yaml, @"(?m)^    " + name + @": (-?[\d.]+)$");
                Assert.IsTrue(m.Success, name + " missing from the asset");
                Assert.AreEqual(value, float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), 1e-6f, name + " ships at its default");
            }
            // Strength B is left as it was for every hull.
            Assert.AreEqual(0.65f, d.ShadeCeiling, 0f);
            Assert.AreEqual(30f, d.FaceLight, 0f);
            Assert.AreEqual(14f, d.CrestLight, 0f);
            Assert.AreEqual(12f, d.TroughDark, 0f);
        }
    }
}
