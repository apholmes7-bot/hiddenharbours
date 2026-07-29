using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// **THE SEAM IS ONE WIDTH, EVERYWHERE.** (ADR 0027 P2 / the ADR 0018 amendment.)
    ///
    /// <para>The shared wave field is declared in five places that must agree: the Core container
    /// (<see cref="WaveTrains.MaxTrains"/>), the packed payload
    /// (<see cref="PackedWaveField.MaxTrains"/>), the bridge's uniform push, the production shader's
    /// <c>WAVE_MAX_TRAINS</c> loop bound, and the shore-seam proof shader that mirrors it. Widening
    /// four of the five is not a compile error and not a visible bug in the common case — it is a
    /// reader that quietly sees a NARROWER sea than the shader draws, and the whole point of ADR
    /// 0018 is that those two are the same sea. ADR 0023's <c>freqScale</c> incident was this exact
    /// failure in a different variable: the hull clamp scanned a sea that was not on screen, and
    /// every hull flooded.</para>
    ///
    /// <para>So the width is asserted <b>structurally</b> — including by reading the two shader
    /// sources as text, which is the only way a C# test can hold the HLSL half of a twin. No GPU is
    /// involved, so this runs in CI where the rendering suites cannot.</para>
    /// </summary>
    public class WaveFieldSeamWidthTests
    {
        private const string WaterShader   = "_Project/Art/Shaders/HiddenHarboursWater.shader";
        private const string ProofShader   = "_Project/Code/Tools/Editor/ShoreSeamProof/ShoreSeamWater.shader";

        private static string ReadShader(string relative)
        {
            string path = Path.Combine(Application.dataPath, relative);
            Assert.IsTrue(File.Exists(path), $"shader not found: {path}");
            return File.ReadAllText(path);
        }

        // ===== (1) THE WIDTH ==============================================================================

        [Test]
        public void CoreContainerAndPackedPayload_DeclareTheSameWidth()
        {
            Assert.AreEqual(WaveTrains.MaxTrains, PackedWaveField.MaxTrains,
                "the packed payload is the Core field as the shader receives it — same slot count");
        }

        [Test]
        public void ProductionShader_LoopBound_MatchesTheCSharpWidth()
        {
            string src = ReadShader(WaterShader);
            var m = Regex.Match(src, @"#define\s+WAVE_MAX_TRAINS\s+(\d+)");
            Assert.IsTrue(m.Success, "WAVE_MAX_TRAINS must be a plain #define the twin review can read");
            Assert.AreEqual(WaveTrains.MaxTrains, int.Parse(m.Groups[1].Value),
                "the shader's FIXED loop bound is the seam's width — widen both or the sea forks");
        }

        [Test]
        public void BothShaders_DeclareEveryTrainUniform_AndBothPhaseVectors()
        {
            foreach (string relative in new[] { WaterShader, ProofShader })
            {
                string src = ReadShader(relative);
                for (int i = 0; i < WaveTrains.MaxTrains; i++)
                    StringAssert.Contains($"float4 _WaveTrain{i};", src,
                        $"{relative}: slot {i}'s uniform must be declared — an undeclared slot reads " +
                        "as zero and silently drops that train from the drawn sea");

                StringAssert.Contains("float4 _WavePhases;", src, $"{relative}: phases 0-3");
                StringAssert.Contains("float4 _WavePhases2;", src, $"{relative}: phases 4-7");
            }
        }

        [Test]
        public void ProductionShader_NeverUnrollsARuntimeBound()
        {
            // The #96 magenta trap, re-asserted here because the widening touched the very loop that
            // carries it: [unroll] must sit over WAVE_MAX_TRAINS (a compile-time constant), never
            // over `count` (a uniform).
            string src = ReadShader(WaterShader);
            foreach (Match m in Regex.Matches(src, @"\[unroll\][\s\S]{0,120}?for\s*\(([^)]*)\)"))
            {
                string header = m.Groups[1].Value;
                Assert.IsFalse(header.Contains("count"),
                    $"[unroll] over a runtime bound — the magenta trap: for ({header})");
            }
        }

        [Test]
        public void ProofShader_TrainArray_IsTheFullWidth()
        {
            string src = ReadShader(ProofShader);
            StringAssert.Contains($"float4 trains[{WaveTrains.MaxTrains}]", src,
                "the shore-seam proof harness reads the same globals; a narrow array there proves " +
                "a seam that is not the one that ships");
        }

        // ===== (2) EVERY SLOT ACTUALLY FLOWS ==============================================================

        /// <summary>A synthetic full-width field. <c>TrainsFrom</c> still derives only four trains
        /// (PR A is the seam, not the spectrum), so the slots the widening ADDS would otherwise go
        /// completely untested until PR B — which is exactly how a half-plumbed seam ships.</summary>
        private static WaveTrains SyntheticFullField(float amplitudeScale = 1f)
        {
            var buffer = new WaveTrain[WaveTrains.MaxTrains];
            for (int i = 0; i < WaveTrains.MaxTrains; i++)
            {
                float angle = 0.7f + i * 0.53f;                        // spread over the circle
                buffer[i] = new WaveTrain(
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                    18f - i * 1.9f,                                    // descending wavelengths
                    amplitudeScale * (0.7f - i * 0.06f),               // descending amplitudes
                    i * 0.61f,                                         // distinct phases
                    9.81f);
            }
            return WaveTrains.From(buffer, WaveTrains.MaxTrains, 2.2f, 0);
        }

        [Test]
        public void PackAndTwin_ReproduceTheReferenceSurface_AcrossAllEightSlots()
        {
            WaveTrains trains = SyntheticFullField();
            Assert.AreEqual(WaveTrains.MaxTrains, trains.Count, "sanity: the field is full width");

            PackedWaveField field = WaveFieldBridge.Pack(in trains);

            foreach (float x in new[] { -31.5f, -4f, 0f, 9.25f, 44f })
            foreach (float y in new[] { -22f, -1.5f, 0f, 17.75f, 38f })
            {
                var pos = new Vector2(x, y);
                WaveSample reference = WaveMath.Sample(pos, 0.0, in trains);
                WaveSample twin = WaveFieldBridge.ShaderTwinSample(pos, in field);
                Assert.AreEqual(reference.Height, twin.Height, 1e-3f, $"height at ({x},{y})");
                Assert.AreEqual(reference.Slope.x, twin.Slope.x, 1e-3f, $"slope.x at ({x},{y})");
                Assert.AreEqual(reference.Slope.y, twin.Slope.y, 1e-3f, $"slope.y at ({x},{y})");
                Assert.AreEqual(reference.CrestFactor, twin.CrestFactor, 1e-3f, $"crest at ({x},{y})");
            }
        }

        [Test]
        public void EverySlotAboveTheOldFour_ActuallyContributesToTheSurface()
        {
            // ⚠ SABOTAGE-MEASURED BY CONSTRUCTION. The parity test above passes just as happily if
            // slots 4-7 are dropped on BOTH sides of the twin — agreeing on nothing is still
            // agreeing. This is the test that says the new slots are not decorative: silencing each
            // one in turn must move the water.
            var pos = new Vector2(6.5f, -3.25f);
            WaveTrains full = SyntheticFullField();
            float reference = WaveFieldBridge.ShaderTwinSample(
                pos, WaveFieldBridge.Pack(in full)).Height;

            for (int silenced = 4; silenced < WaveTrains.MaxTrains; silenced++)
            {
                var buffer = new WaveTrain[WaveTrains.MaxTrains];
                for (int i = 0; i < WaveTrains.MaxTrains; i++)
                {
                    WaveTrain t = full[i];
                    buffer[i] = i == silenced
                        ? new WaveTrain(t.Direction, t.Wavelength, 0f, t.PhaseOffset, 9.81f)
                        : t;
                }
                var muted = WaveTrains.From(buffer, WaveTrains.MaxTrains, full.CrestSharpening, 0);
                float height = WaveFieldBridge.ShaderTwinSample(
                    pos, WaveFieldBridge.Pack(in muted)).Height;

                Assert.AreNotEqual(reference, height,
                    $"slot {silenced} must reach the drawn surface — if silencing it changes nothing, " +
                    "the seam is plumbed on the C# side only and the widening is cosmetic");
            }
        }

        [Test]
        public void SwappingWholeSlots_LeavesTheSeaAlone_BecauseTheFieldIsASum()
        {
            // ⚠ RECORDED BECAUSE THE OBVIOUS SABOTAGE IS VOID. "Swap two train slots and the water
            // must move" reads like a good identity check and is measurably wrong: the surface is
            // Σ over trains, so permuting the slots is a mathematical no-op (it came back
            // bit-identical, not merely close). Pinned here so the void test is not re-invented — the
            // real index sabotage is the next one.
            var pos = new Vector2(-12.75f, 8.5f);
            WaveTrains full = SyntheticFullField();
            float reference = WaveFieldBridge.ShaderTwinSample(
                pos, WaveFieldBridge.Pack(in full)).Height;

            var buffer = new WaveTrain[WaveTrains.MaxTrains];
            for (int i = 0; i < WaveTrains.MaxTrains; i++) buffer[i] = full[i];
            (buffer[1], buffer[6]) = (buffer[6], buffer[1]);
            var swapped = WaveTrains.From(buffer, WaveTrains.MaxTrains, full.CrestSharpening, 0);

            Assert.AreEqual(reference,
                WaveFieldBridge.ShaderTwinSample(pos, WaveFieldBridge.Pack(in swapped)).Height,
                1e-6f, "permuting slots cannot change a sum");
        }

        [Test]
        public void MispairingATrainWithAnotherSlotsPhase_ChangesTheSea()
        {
            // THE REAL INDEX SABOTAGE. What the packing can actually get wrong is the PAIRING: a
            // train's direction/k/amplitude come from `_WaveTrainI` while its phase comes from a
            // different vector (`_WavePhases` vs `_WavePhases2`), so an off-by-one between
            // PackTrain and PhaseAt — or a shader that indexes the two phase vectors wrongly at the
            // 3/4 boundary the widening introduced — would hand slot i someone else's phase. That
            // is invisible to a permutation test and it is exactly what this asserts.
            var pos = new Vector2(-12.75f, 8.5f);
            WaveTrains full = SyntheticFullField();
            float reference = WaveFieldBridge.ShaderTwinSample(
                pos, WaveFieldBridge.Pack(in full)).Height;

            // Straddle the low/high phase-vector boundary deliberately: slot 3 is the last of
            // _WavePhases, slot 4 the first of _WavePhases2.
            var buffer = new WaveTrain[WaveTrains.MaxTrains];
            for (int i = 0; i < WaveTrains.MaxTrains; i++) buffer[i] = full[i];
            buffer[3] = new WaveTrain(full[3].Direction, full[3].Wavelength, full[3].Amplitude,
                                      full[4].PhaseOffset, 9.81f);
            buffer[4] = new WaveTrain(full[4].Direction, full[4].Wavelength, full[4].Amplitude,
                                      full[3].PhaseOffset, 9.81f);
            var mispaired = WaveTrains.From(buffer, WaveTrains.MaxTrains, full.CrestSharpening, 0);

            Assert.AreNotEqual(reference,
                WaveFieldBridge.ShaderTwinSample(pos, WaveFieldBridge.Pack(in mispaired)).Height,
                "a train must carry ITS OWN phase across the _WavePhases/_WavePhases2 boundary");
        }

        // ===== (3) PUSH PARITY — what C# holds is what the uniforms carry =================================

        [Test]
        public void PublishGlobals_ThenReadBack_IsTheSameField()
        {
            // The bridge publishes to shader globals and DisplacedWaterMath's hull clamp reads them
            // straight back (that is the ONE-SEA rule, closed at the globals). No GPU is touched:
            // these are CPU-side global properties, so CI adjudicates this.
            WaveTrains trains = SyntheticFullField();
            PackedWaveField packed = WaveFieldBridge.Pack(in trains);
            try
            {
                WaveFieldBridge.PublishGlobals(in packed);
                PackedWaveField read = WaveFieldBridge.ReadPublishedField();

                for (int i = 0; i < PackedWaveField.MaxTrains; i++)
                {
                    Assert.AreEqual(packed.Train(i), read.Train(i), $"slot {i} survived the globals");
                    Assert.AreEqual(packed.Phase(i), read.Phase(i), 1e-6f, $"slot {i} phase survived");
                }
                Assert.AreEqual(packed.Params, read.Params, "params survived");
                Assert.AreEqual(trains.Count, read.Count, "the count the shader will mask with");
                Assert.AreEqual(trains.DominantIndex, read.DominantIndex,
                    "the spectral-peak slot survived — the whitecap lifecycle keys on it");
            }
            finally
            {
                WaveFieldBridge.PublishGlobals(PackedWaveField.Empty);   // never leak into other tests
            }
        }

        [Test]
        public void ADeadSlotIsNeverReadAsLive_EvenWithStaleGlobalsBehindIt()
        {
            // The failure mode the widening creates: a wide field is published, then a NARROW one.
            // If the second publish only wrote the slots it uses, slots 4-7 would still hold the
            // first field's trains — and any reader whose count came from elsewhere would ride them.
            try
            {
                WaveTrains wide = SyntheticFullField();
                WaveFieldBridge.PublishGlobals(WaveFieldBridge.Pack(in wide));

                WaveTrains narrow = WaveMath.TrainsFrom(new Vector2(5f, 2f), 0.6f,
                                                        WaveFieldSettings.Default);
                WaveFieldBridge.PublishGlobals(WaveFieldBridge.Pack(in narrow));

                PackedWaveField read = WaveFieldBridge.ReadPublishedField();
                for (int i = narrow.Count; i < PackedWaveField.MaxTrains; i++)
                    Assert.AreEqual(Vector4.zero, read.Train(i),
                        $"slot {i} must be CLEARED by the narrower publish, not left holding the wide field");
            }
            finally
            {
                WaveFieldBridge.PublishGlobals(PackedWaveField.Empty);
            }
        }

        // ===== (4) DETERMINISM ACROSS PROCESSES ===========================================================

        /// <summary>
        /// FNV-1a over the raw bit patterns of every derived train across the sweep. A hash rather
        /// than a value table because the point is not "these numbers" but "the SAME numbers, in a
        /// different process, on a different day" — rule 5's actual claim. The literal below was
        /// produced by an earlier run; a test that recomputed its own expectation would prove
        /// nothing about determinism at all.
        /// </summary>
        private const ulong DeterminismGolden = 0xC367BA46C463AC95UL;   // captured 2026-07-29

        [Test]
        public void TrainsFrom_IsDeterministicAcrossProcessRuns()
        {
            ulong hash = 14695981039346656037UL;
            void Feed(float f)
            {
                uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(f));
                for (int b = 0; b < 4; b++)
                {
                    hash ^= (byte)(bits >> (b * 8));
                    hash *= 1099511628211UL;
                }
            }

            var settings = WaveFieldSettings.Default;
            foreach (var wind in new[] { Vector2.zero, new Vector2(3f, 1f), new Vector2(-6f, 4f),
                                         new Vector2(0f, -11f), new Vector2(24f, -18f) })
            foreach (float sea in new[] { 0f, 0.25f, 0.6f, 1f })
            {
                WaveTrains trains = WaveMath.TrainsFrom(wind, sea, in settings);
                Feed(trains.Count);
                Feed(trains.CrestSharpening);
                Feed(trains.DominantIndex);
                for (int i = 0; i < trains.Count; i++)
                {
                    WaveTrain t = trains[i];
                    Feed(t.Direction.x); Feed(t.Direction.y); Feed(t.Wavelength);
                    Feed(t.Amplitude);   Feed(t.PhaseSpeed);  Feed(t.PhaseOffset);
                }
            }

            Assert.AreEqual(DeterminismGolden, hash,
                $"the derived field changed. If this is a DELIBERATE, owner-ratified change to the " +
                $"sea, update the golden to 0x{hash:X16}UL in the SAME PR that carries his verdict " +
                $"— never to make a red test green.");
        }
    }
}
