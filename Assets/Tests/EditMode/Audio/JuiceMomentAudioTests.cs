using NUnit.Framework;
using HiddenHarbours.Audio;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Audio
{
    /// <summary>
    /// <b>The three moments' audio hooks</b> (juice charter §4.5): every moment publishes the event the
    /// manifest's slot names, and the director maps each to its own cue. No files — a cue with no clip
    /// is silent until the owner's purchase lands. Pinned so a later "tidy" cannot fold a moment back
    /// into the arrival warmth or double the sale.
    /// </summary>
    public class JuiceMomentAudioTests
    {
        [Test]
        public void Each_moment_has_its_own_cue()
        {
            Assert.AreEqual(AudioCue.LandingHit, AudioDirectorLogic.CueFor(AudioMoment.Landing));
            Assert.AreEqual(AudioCue.DigStrike,  AudioDirectorLogic.CueFor(AudioMoment.DigStrike));
            Assert.AreEqual(AudioCue.CastEntry,  AudioDirectorLogic.CueFor(AudioMoment.CastEntry));
            Assert.AreEqual(AudioCue.SaleChime,  AudioDirectorLogic.CueFor(AudioMoment.CatchSold),
                "the sale diverged from the arrival cue — it is a reward beat, not the home-exhale");
            Assert.AreEqual(AudioCue.HomeWarmth, AudioDirectorLogic.CueFor(AudioMoment.CameAshore),
                "…and the home-warmth stays the rarer, earned arrival cue");
        }

        [Test]
        public void A_published_cue_maps_to_its_slot_and_the_sale_cue_is_silent_because_CatchSold_already_plays_it()
        {
            Assert.AreEqual(AudioCue.LandingHit, AudioDirectorLogic.CueFor(JuiceMoment.Landing));
            Assert.AreEqual(AudioCue.DigStrike,  AudioDirectorLogic.CueFor(JuiceMoment.DigStrike));
            Assert.AreEqual(AudioCue.CastEntry,  AudioDirectorLogic.CueFor(JuiceMoment.CastEntry));
            Assert.AreEqual(AudioCue.None,       AudioDirectorLogic.CueFor(JuiceMoment.Sale),
                "the director plays the sale on CatchSold — the cue must not double it");
        }

        [Test]
        public void The_four_cues_are_distinct_from_each_other_and_from_none()
        {
            var cues = new[] { AudioCue.LandingHit, AudioCue.SaleChime, AudioCue.DigStrike, AudioCue.CastEntry };
            for (int i = 0; i < cues.Length; i++)
            {
                Assert.AreNotEqual(AudioCue.None, cues[i]);
                for (int k = i + 1; k < cues.Length; k++) Assert.AreNotEqual(cues[i], cues[k]);
            }
        }
    }
}
