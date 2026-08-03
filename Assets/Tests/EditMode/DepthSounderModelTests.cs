using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The depth sounder's pure rules (ADR 0025 S2, <see cref="DepthSounder"/>): what the glass reads,
    /// when the SHALLOW alarm trips, how the two pushers walk the set-point, and the blink phase.
    ///
    /// <para><b>The first test is the one that matters.</b> "Is it deep" must equal "is it wet": the
    /// sounding is <see cref="TidalExposure.WaterDepth"/> over the same height map the water render, the
    /// walkability sim and boat grounding read. Asserting the two agree — rather than that the sounder
    /// computes a plausible number — is what stops a second copy of the height map ever appearing.</para>
    /// </summary>
    public class DepthSounderModelTests
    {
        [Test]
        public void TheSounding_IsExactlyTheSharedWaterDepthRule()
        {
            // A ladder across the intertidal: deep water, the waterline, and dry ground above it.
            float[] levels = { 0f, 0.9f, 2.2f, 3.7f };
            float[] seabed = { -6f, -1.5f, 0f, 0.88f, 2.5f };

            foreach (float level in levels)
                foreach (float ground in seabed)
                {
                    float shared = TidalExposure.WaterDepth(level, ground);
                    float shown = DepthSounder.DisplayDepth(level, ground);

                    if (shared > 0f)
                        Assert.AreEqual(shared, shown, 0f,
                            $"level {level}, ground {ground}: the glass shows the SAME number the sim uses");
                    else
                        Assert.AreEqual(0f, shown, 0f,
                            $"level {level}, ground {ground}: aground reads 0.0, never a negative sounding");

                    // And the two never disagree about which side of the waterline you are on.
                    Assert.AreEqual(TidalExposure.IsExposed(level, ground), shown <= 0f,
                        "exposed ground and a zero sounding are the same fact");
                }
        }

        [Test]
        public void TheAlarm_TripsAtOrBelowTheSetPoint_OnlyWhenArmed()
        {
            var armed = new SounderPrefs(3f, armed: true, feet: false, night: false);
            var off = new SounderPrefs(3f, armed: false, feet: false, night: false);

            Assert.IsFalse(DepthSounder.ShallowAlarm(3.01f, in armed), "clear water is quiet");
            Assert.IsTrue(DepthSounder.ShallowAlarm(3.00f, in armed), "the rig trips AT the set-point (<=)");
            Assert.IsTrue(DepthSounder.ShallowAlarm(0.40f, in armed));
            Assert.IsTrue(DepthSounder.ShallowAlarm(0f, in armed), "aground is certainly shallow");

            Assert.IsFalse(DepthSounder.ShallowAlarm(0.40f, in off),
                "an unarmed sounder never flashes, however shoal it gets");
        }

        [Test]
        public void ThePushers_StepTheSetPoint_AndClampToTheOwnersRange()
        {
            DepthSounderSettings cfg = DepthSounderSettings.Default;

            Assert.AreEqual(3.5f, DepthSounder.StepAlarm(3f, +1, in cfg), 1e-6f);
            Assert.AreEqual(2.5f, DepthSounder.StepAlarm(3f, -1, in cfg), 1e-6f);

            // Walk it to both stops and confirm it parks there rather than running away.
            float low = 3f;
            for (int i = 0; i < 40; i++) low = DepthSounder.StepAlarm(low, -1, in cfg);
            Assert.AreEqual(cfg.MinAlarmMetres, low, 1e-6f, "it cannot be wound below the shallow stop");

            float high = 3f;
            for (int i = 0; i < 200; i++) high = DepthSounder.StepAlarm(high, +1, in cfg);
            Assert.AreEqual(cfg.MaxAlarmMetres, high, 1e-6f, "nor past the deep one");
        }

        [Test]
        public void TheSetPointLadder_IsExactInFloat_SoTheDisplayNeverDrifts()
        {
            // 0.5 is a dyadic step, so walking it repeatedly must land on exact halves — a set-point
            // that read "3.4999" after twelve presses would print a different string than it should.
            DepthSounderSettings cfg = DepthSounderSettings.Default;
            float v = cfg.MinAlarmMetres;
            for (int i = 0; i < 12; i++)
            {
                v = DepthSounder.StepAlarm(v, +1, in cfg);
                Assert.AreEqual(cfg.MinAlarmMetres + cfg.AlarmStepMetres * (i + 1), v, 0f,
                    "each press lands exactly on the ladder — no accumulated float drift");
            }
        }

        [Test]
        public void TheBlinkPhase_IsAPureFunctionOfTheClock_AtTheConfiguredRate()
        {
            const float hz = 2f;   // two full on/off cycles a second → a 0.25 s half-period
            Assert.IsTrue(DepthSounder.BlinkPhase(0.00, hz));
            Assert.IsTrue(DepthSounder.BlinkPhase(0.24, hz));
            Assert.IsFalse(DepthSounder.BlinkPhase(0.26, hz));
            Assert.IsFalse(DepthSounder.BlinkPhase(0.49, hz));
            Assert.IsTrue(DepthSounder.BlinkPhase(0.51, hz), "and back on for the next cycle");

            // Asked twice at the same instant it answers the same — the host can repaint on the FLIP.
            Assert.AreEqual(DepthSounder.BlinkPhase(12.345, hz), DepthSounder.BlinkPhase(12.345, hz));

            Assert.IsTrue(DepthSounder.BlinkPhase(99.9, 0f), "a zero rate means lit, not flashing");
        }

        [Test]
        public void TheDefaultPrefs_ComeFromTheOwnersConfig_NotFromLiterals()
        {
            DepthSounderSettings cfg = DepthSounderSettings.Default;
            var prefs = SounderPrefs.FromDefaults(in cfg);
            Assert.AreEqual(cfg.DefaultAlarmMetres, prefs.AlarmMetres, 1e-6f);
            Assert.AreEqual(cfg.DefaultArmed, prefs.Armed);
            Assert.AreEqual(cfg.DefaultFeet, prefs.Feet);
            Assert.IsFalse(prefs.Night, "a fresh sounder starts on the day panel");
        }

        [Test]
        public void TheConfigDefaults_AreSane_SoTheInstrumentIsUsableOutOfTheBox()
        {
            DepthSounderSettings cfg = DepthSounderSettings.Default;
            Assert.Greater(cfg.ReadIntervalSec, 0f, "a zero interval would sound every frame (rule 7)");
            Assert.Less(cfg.ReadIntervalSec, 2f, "and a lazy one would lag a boat crossing a bar");
            Assert.Greater(cfg.MaxAlarmMetres, cfg.MinAlarmMetres);
            Assert.GreaterOrEqual(cfg.DefaultAlarmMetres, cfg.MinAlarmMetres);
            Assert.LessOrEqual(cfg.DefaultAlarmMetres, cfg.MaxAlarmMetres);
            Assert.Greater(cfg.AlarmStepMetres, 0f);
        }
    }
}
