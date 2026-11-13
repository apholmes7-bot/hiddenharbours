using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Audio;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Audio
{
    /// <summary>
    /// HOW BIG SHE SOUNDS (owner's ask 2026-11-13 — "woah its something big"): the size-aware strain
    /// voice in <see cref="FishingAudioLogic"/>. A heavy fish keeps the line humming and groans deeper;
    /// a big one screams at the hook-set; a small fish and a hand-gathered catch are untouched. The
    /// curves themselves (<see cref="FishFeelMath"/>) are pinned in the root <c>FishFeelTests</c>.
    /// </summary>
    public class FishFeelAudioTests
    {
        private const float Eps = 1e-4f;
        private static FishFeelSettings D => FishFeelSettings.Default;

        private static FishingState Fight(FishingPhase phase, float tension, float size01, bool slack = false)
            => new FishingState(phase, tension, 0.2f, "fish.cod", "Cod", FishCategory.InshoreGroundfish, 5f,
                                depth01: 0f, slackWindowOpen: slack, rodBend01: 0f,
                                fishOffsetX: 1f, fishOffsetY: 0f,
                                castCharge01: 0f, castAimX: 0f, castAimY: 0f, rigDepthM: 0f,
                                fishRunDirX: 0f, fishRunDirY: 0f, size01: size01);

        [Test]
        public void The_strain_voice_is_unchanged_for_a_small_fish_long_after_the_hook()
        {
            FishingState s = Fight(FishingPhase.FightSurface, 0.6f, 0f);
            FishingAudioMix plain = FishingAudioLogic.MixFor(in s, 0.1f, 0.3f);
            FishingAudioMix sized = FishingAudioLogic.MixFor(in s, 0.1f, 0.3f, 10f, D);
            Assert.AreEqual(plain.StrainGain, sized.StrainGain, Eps);
            Assert.AreEqual(plain.StrainPitch, sized.StrainPitch, Eps);
            Assert.AreEqual(plain.ReelGain, sized.ReelGain, Eps);
            Assert.AreEqual(plain.ThrashGain, sized.ThrashGain, Eps);
        }

        [Test]
        public void A_heavy_fish_hums_and_groans_deeper_and_a_big_one_screams_at_the_hook()
        {
            FishFeelSettings f = D;
            FishingState calm = Fight(FishingPhase.FightDeep, 0f, 1f);

            FishingAudioMix late = FishingAudioLogic.MixFor(in calm, 0f, 0f, 10f, in f);
            Assert.AreEqual(f.StrainHumHeavyGain, late.StrainGain, Eps, "no tension, heavy fish: the line still hums");
            Assert.AreEqual(1f - f.StrainPitchHeavyDrop01, late.StrainPitch, Eps, "and the voice sits deeper");

            FishingAudioMix hook = FishingAudioLogic.MixFor(in calm, 0f, 0f, 0f, in f);
            Assert.AreEqual(Mathf.Clamp01(f.StrainHumHeavyGain + f.HookScreamGain), hook.StrainGain, Eps,
                "the hook-set scream");
            Assert.Greater(hook.StrainPitch, late.StrainPitch, "pitched up, falling back");

            FishingState middling = Fight(FishingPhase.FightDeep, 0f, 0.5f);
            FishingAudioMix noScream = FishingAudioLogic.MixFor(in middling, 0f, 0f, 0f, in f);
            Assert.AreEqual(f.StrainHumHeavyGain * 0.5f, noScream.StrainGain, Eps,
                "under the big-one line there is a hum but no scream");

            FishingState slack = Fight(FishingPhase.FightDeep, 0f, 1f, slack: true);
            Assert.AreEqual(0f, FishingAudioLogic.MixFor(in slack, 0f, 0f, 10f, in f).StrainGain, Eps,
                "slack: the hum drops out with the line");
        }

        [Test]
        public void A_hand_gathered_catch_is_never_sized()
        {
            FishingState tend = Fight(FishingPhase.Tending, 0.5f, 1f);
            FishingAudioMix plain = FishingAudioLogic.MixFor(in tend, 0f, 0f);
            FishingAudioMix sized = FishingAudioLogic.MixFor(in tend, 0f, 0f, 0f, D);
            Assert.AreEqual(plain.StrainGain, sized.StrainGain, Eps);
            Assert.AreEqual(plain.StrainPitch, sized.StrainPitch, Eps);
        }
    }
}
