using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>When the player thinks a thing</b> — the pure half of the inner-voice clue channel (owner
    /// ruling 2026-09-06). The def answers two questions and the director does nothing but ask them, so
    /// every trigger rule is assertable here with plain numbers and no scene.
    ///
    /// <para>The gates that matter are the NEGATIVE ones: a clue that fires on the wrong thing, in the
    /// wrong region, or twice while the player stands still is worse than no clue, because it teaches
    /// the player that the bubbles are noise.</para>
    /// </summary>
    public class InnerVoiceLineDefTests
    {
        static InnerVoiceLineDef Offer(string subject)
        {
            var d = ScriptableObject.CreateInstance<InnerVoiceLineDef>();
            d.Id = "innervoice.test_offer";
            d.Line = "Hm.";
            d.Trigger = InnerVoiceTrigger.InteractOffered;
            d.SubjectId = subject;
            return d;
        }

        static InnerVoiceLineDef Near(string region, Vector2 at, float radius)
        {
            var d = ScriptableObject.CreateInstance<InnerVoiceLineDef>();
            d.Id = "innervoice.test_near";
            d.Line = "Hm.";
            d.Trigger = InnerVoiceTrigger.Proximity;
            d.RegionId = region;
            d.WorldPosition = at;
            d.RadiusMeters = radius;
            return d;
        }

        // ---- the offer trigger --------------------------------------------------------------------

        [Test]
        public void WantsOffer_MatchesItsOwnSubject()
        {
            Assert.IsTrue(Offer("tool.st_peters.rod").WantsOffer("tool.st_peters.rod"));
        }

        [Test]
        public void WantsOffer_IsWholeAndCaseSensitive()
        {
            InnerVoiceLineDef d = Offer("tool.st_peters.rod");
            Assert.IsFalse(d.WantsOffer("tool.st_peters.rod.tip"), "a prefix is not a match");
            Assert.IsFalse(d.WantsOffer("tool.st_peters"), "a truncation is not a match");
            Assert.IsFalse(d.WantsOffer("Tool.St_Peters.Rod"), "ids are case-sensitive");
        }

        [Test]
        public void WantsOffer_IgnoresNothingBeingOffered()
        {
            // The offer channel publishes a null id to say "the popup should go away". Nothing is not a
            // thing to have a thought about — and an empty SubjectId must not turn into a wildcard that
            // fires on every empty offer in the game.
            Assert.IsFalse(Offer("tool.st_peters.rod").WantsOffer(null));
            Assert.IsFalse(Offer("").WantsOffer(null));
            Assert.IsFalse(Offer("").WantsOffer(""));
        }

        [Test]
        public void WantsOffer_IsFalseForAProximityLine()
        {
            InnerVoiceLineDef d = Near("region.nine_mile_creek", Vector2.zero, 5f);
            d.SubjectId = "tool.st_peters.rod";     // authored by mistake on the wrong trigger
            Assert.IsFalse(d.WantsOffer("tool.st_peters.rod"),
                           "the trigger decides which field is read; a stray subject must not fire");
        }

        // ---- the proximity trigger ------------------------------------------------------------------

        [Test]
        public void CoversPoint_IsTrueInsideTheCircle()
        {
            InnerVoiceLineDef d = Near("region.nine_mile_creek", new Vector2(64f, 112f), 6f);
            Assert.IsTrue(d.CoversPoint(new Vector2(64f, 112f), "region.nine_mile_creek"));
            Assert.IsTrue(d.CoversPoint(new Vector2(68f, 112f), "region.nine_mile_creek"));
            Assert.IsFalse(d.CoversPoint(new Vector2(71f, 112f), "region.nine_mile_creek"));
        }

        /// <summary>
        /// ⭐ Positions are region-LOCAL. Two regions can, and do, use overlapping coordinates — so a
        /// clue about a hull at Nine Mile Creek that fired at the same numbers off St Peters would be a
        /// thought about something that is not there.
        /// </summary>
        [Test]
        public void CoversPoint_IsFalseInAnotherRegion()
        {
            InnerVoiceLineDef d = Near("region.nine_mile_creek", new Vector2(64f, 112f), 6f);
            Assert.IsFalse(d.CoversPoint(new Vector2(64f, 112f), "region.st_peters"));
            Assert.IsFalse(d.CoversPoint(new Vector2(64f, 112f), null),
                           "no region loaded is not 'any region'");
        }

        [Test]
        public void CoversPoint_IsFalseForALineWithNoRegionAuthored()
        {
            InnerVoiceLineDef d = Near("", new Vector2(64f, 112f), 6f);
            Assert.IsFalse(d.CoversPoint(new Vector2(64f, 112f), "region.nine_mile_creek"),
                           "an unauthored region must fire nowhere, not everywhere");
        }

        [Test]
        public void CoversPoint_IsFalseForAnOfferLine()
        {
            InnerVoiceLineDef d = Offer("tool.st_peters.rod");
            d.RegionId = "region.nine_mile_creek";
            d.RadiusMeters = 100f;
            Assert.IsFalse(d.CoversPoint(Vector2.zero, "region.nine_mile_creek"));
        }

        /// <summary>
        /// The dead band. Standing exactly on the radius must not flicker the bubble on and off, so she
        /// arms again only once she is well outside — which means "just left the circle" is NOT yet
        /// re-armed.
        /// </summary>
        [Test]
        public void HasLeftFor_NeedsTheWiderReArmRing()
        {
            InnerVoiceLineDef d = Near("region.nine_mile_creek", Vector2.zero, 10f);

            Assert.IsFalse(d.HasLeftFor(new Vector2(9f, 0f)), "still inside");
            Assert.IsFalse(d.HasLeftFor(new Vector2(11f, 0f)), "outside the circle but inside the band");
            Assert.IsFalse(d.HasLeftFor(new Vector2(14f, 0f)), "still inside the band");
            Assert.IsTrue(d.HasLeftFor(new Vector2(16f, 0f)), "past the re-arm ring");
        }

        [Test]
        public void ReArmRing_IsWiderThanTheTriggerCircle()
        {
            // The dead band only exists if the factor is above one. A future tuner setting it to 1 would
            // silently reintroduce the flicker this guards.
            Assert.That(InnerVoiceLineDef.ReArmFactor, Is.GreaterThan(1f));
        }

        // ---- the flag -------------------------------------------------------------------------------

        [Test]
        public void SaveFlagKey_IsNamespaced_SoAClueCannotCollideWithAnotherSystemsFlag()
        {
            InnerVoiceLineDef d = Offer("tool.st_peters.rod");
            d.Id = "innervoice.rod_on_the_sand";
            Assert.That(d.SaveFlagKey, Does.StartWith(InnerVoiceLineDef.FlagPrefix));
            Assert.That(d.SaveFlagKey, Does.EndWith("innervoice.rod_on_the_sand"));
        }

        [Test]
        public void SaveFlagKey_IsEmptyWithoutAnId_RatherThanAFlagEverythingShares()
        {
            InnerVoiceLineDef d = Offer("x");
            d.Id = "";
            Assert.That(d.SaveFlagKey, Is.Empty);
        }

        [Test]
        public void VoiceId_IsNullWhenNoVoiceIsAuthored()
        {
            // Which the presenter reads as "the default cadence", never as a stalled bubble.
            Assert.IsNull(Offer("x").VoiceId);
        }
    }
}
