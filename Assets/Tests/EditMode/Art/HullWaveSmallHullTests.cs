using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A rowed dory's wake reads</b> (the owner, 2026-11-19: "Yes, more visible"). A short hull's waves
    /// are raised to a floor that follows her Froude response, and her sources lie closer along her track
    /// so her rings blend into a soft V instead of single loops. Hulls longer than the small-hull length,
    /// the lobster boat among them, keep exactly the waves they had.
    /// </summary>
    public class HullWaveSmallHullTests
    {
        const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const float DoryLength = 4.5f, LobsterLength = 12f;

        static HullWaveSettings S => HullWaveSettings.Default;

        static HullWaveSettings Off()
        {
            HullWaveSettings off = S;
            off.SmallHullCrestMetres = 0f;
            off.SmallHullSpacingWavelengths = 0f;
            return off;
        }

        [Test]
        public void ARowedDory_TheSmallHullFloor_NeverLowersHerWaves_AndLiftsThemWhenSetHigh()
        {
            // Owner 11-21 lowered the shipped floor (0.07 -> 0.04) so the rings stop reading as hard loops:
            // at rowing speeds her own height now meets it, so the floor holds her up but no longer lifts her.
            foreach (float v in new[] { 1.5f, 1.65f, 2f, 2.2f })
            {
                float raised = HullWaveMath.CrestHeight(v, DoryLength, S);
                float old = HullWaveMath.CrestHeight(v, DoryLength, Off());
                float response = Mathf.Min(HullWaveMath.Froude(v, DoryLength) / S.FroudeKnee, 1f);
                Assert.GreaterOrEqual(raised, old, $"at {v} m/s a rowed dory's waves are never lowered");
                Assert.LessOrEqual(raised, Mathf.Max(old, S.SmallHullCrestMetres * response) + 1e-6f, "no more than the floor times her response, or her own");
                Assert.Less(raised, 0.1f, "still centimetres: a soft V, not rollers");
            }
            HullWaveSettings higher = S;
            higher.SmallHullCrestMetres = 0.07f;
            Assert.Greater(HullWaveMath.CrestHeight(2f, DoryLength, higher), HullWaveMath.CrestHeight(2f, DoryLength, Off()),
                "the floor still works: set higher, it lifts her waves");
            Assert.AreEqual(0f, HullWaveMath.CrestHeight(S.MinSpeed, DoryLength, S), "at rest she still makes none");
        }

        [Test]
        public void TheLobsterBoat_KeepsHerWaves_AtEverySpeed()
        {
            Assert.Greater(LobsterLength, S.SmallHullLengthMetres, "premise: she is not a small hull");
            HullWaveSettings off = Off();
            for (int i = 1; i <= 60; i++)
            {
                float v = i * 0.2f;
                Assert.AreEqual(HullWaveMath.CrestHeight(v, LobsterLength, off), HullWaveMath.CrestHeight(v, LobsterLength, S),
                    0f, $"her crest at {v} m/s is unchanged");
            }
            HullWaveSettings hers = HullWaveMath.ForHull(LobsterLength, S);
            Assert.AreEqual(S.SpacingWavelengths, hers.SpacingWavelengths, 0f, "and her spacing is unchanged");
        }

        [Test]
        public void ADorysSources_LieCloser_SoHerRingsBlendIntoAV()
        {
            HullWaveSettings hers = HullWaveMath.ForHull(DoryLength, S);
            Assert.AreEqual(S.SmallHullSpacingWavelengths, hers.SpacingWavelengths, 0f);
            Assert.Less(hers.SpacingWavelengths, S.SpacingWavelengths, "closer than a long hull's");
            const int budget = 15;
            float lambda = HullWaveMath.Wavelength(1.65f, DoryLength, in hers);
            float life = HullWaveMath.KeptLifetime(HullWaveMath.Lifetime(DoryLength, in hers), 1.65f, lambda, budget, in hers);
            float closer = HullWaveMath.DepositInterval(1.65f, lambda, life, budget, hers.SpacingWavelengths);
            HullWaveSettings off = Off();
            float oldLife = HullWaveMath.KeptLifetime(HullWaveMath.Lifetime(DoryLength, in off), 1.65f, lambda, budget, in off);
            float wider = HullWaveMath.DepositInterval(1.65f, lambda, oldLife, budget, off.SpacingWavelengths);
            Assert.Less(closer, wider, "she lays her sources more often");
            Assert.LessOrEqual(life / closer, budget + 1e-3f, "and never more than her budget holds");
        }

        [Test]
        public void WithTheSmallHullDialsAtZero_TheOlderWakeComesBack()
        {
            HullWaveSettings off = Off();
            foreach (float v in new[] { 0.5f, 1.65f, 3f })
            {
                float onset = Mathf.Clamp01((v - off.MinSpeed) / Mathf.Max(off.MinSpeed, 0.05f));
                float r = Mathf.Min(HullWaveMath.Froude(v, DoryLength) / off.FroudeKnee, 1f);
                float expected = Mathf.Min(off.HeightPerMetreLength * DoryLength * r * r * onset, off.MaxHeightMetres);
                Assert.AreEqual(expected, HullWaveMath.CrestHeight(v, DoryLength, off), 1e-7f);
            }
            Assert.AreEqual(off.SpacingWavelengths, HullWaveMath.ForHull(DoryLength, in off).SpacingWavelengths, 0f);
        }

        [Test]
        public void TheShippedConfig_CarriesTheSmallHullDials()
        {
            string yaml = File.ReadAllText(Path.Combine(Application.dataPath, "..", ConfigPath)).Replace("\r\n", "\n");
            StringAssert.Contains("\n    SmallHullCrestMetres: 0.04\n", yaml);
            StringAssert.Contains("\n    SmallHullLengthMetres: 6\n", yaml);
            StringAssert.Contains("\n    SmallHullSpacingWavelengths: 0.35\n", yaml);
            Assert.AreEqual(0.04f, S.SmallHullCrestMetres, 1e-6f, "the code's defaults are the shipped dials");
            Assert.AreEqual(6f, S.SmallHullLengthMetres, 1e-6f);
            Assert.AreEqual(0.35f, S.SmallHullSpacingWavelengths, 1e-6f);
        }
    }
}
