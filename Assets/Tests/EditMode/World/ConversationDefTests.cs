using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// The def's own rules, in the negative where it matters: <b>a mis-authored line must never be put
    /// in the wrong person's mouth.</b> A conversation is content, and the failure mode of content is a
    /// typo — a speaker index of 2 in a two-hander, a participant left null, the same villager listed
    /// twice. Each of those has one right answer here and none of them is "throw".
    /// </summary>
    public class ConversationDefTests
    {
        static NpcDef Npc(string id, string name, DialogueVoiceDef voice = null)
        {
            var n = ScriptableObject.CreateInstance<NpcDef>();
            n.Id = id;
            n.DisplayName = name;
            n.Voice = voice;
            return n;
        }

        static DialogueVoiceDef Voice(string id)
        {
            var v = ScriptableObject.CreateInstance<DialogueVoiceDef>();
            v.Id = id;
            return v;
        }

        static ConversationDef Two(params ConversationLine[] lines)
        {
            var d = ScriptableObject.CreateInstance<ConversationDef>();
            d.Id = "conversation.test";
            d.Participants = new[] { Npc("npc.a", "Ada"), Npc("npc.b", "Ben") };
            d.Lines = lines;
            d.RegionId = "region.st_peters";
            d.StationId = "station.st_peters.store_counter";
            return d;
        }

        static ConversationLine Line(int speaker, string text, DialogueVoiceDef voice = null)
            => new ConversationLine { SpeakerIndex = speaker, Text = text, Voice = voice };

        // ---- runnability ----------------------------------------------------------------------

        [Test]
        public void AWellFormedTwoHander_IsRunnable()
        {
            Assert.IsTrue(Two(Line(0, "Some weather."), Line(1, "It is that.")).IsRunnable);
        }

        [Test]
        public void ADefIsNotRunnable_WithoutTwoDistinctParticipants()
        {
            ConversationDef d = Two(Line(0, "Hm."));

            d.Participants = new[] { Npc("npc.a", "Ada") };
            Assert.IsFalse(d.IsRunnable, "one participant is a line, not a conversation");

            NpcDef same = Npc("npc.a", "Ada");
            d.Participants = new[] { same, same };
            Assert.IsFalse(d.IsRunnable, "somebody talking to themselves is not a two-hander");

            d.Participants = new[] { Npc("npc.a", "Ada"), null };
            Assert.IsFalse(d.IsRunnable, "a null participant has no body to hang a bubble on");
        }

        [Test]
        public void ADefIsNotRunnable_WithoutAPlaceToMeet()
        {
            ConversationDef d = Two(Line(0, "Hm."));
            d.StationId = "";
            Assert.IsFalse(d.IsRunnable);

            d = Two(Line(0, "Hm."));
            d.RegionId = "";
            Assert.IsFalse(d.IsRunnable, "station positions are region-local, so an unregioned station " +
                                         "could resolve somewhere else entirely");
        }

        [Test]
        public void ADefIsNotRunnable_WithNothingToSay()
        {
            ConversationDef d = Two();
            Assert.IsFalse(d.IsRunnable);
        }

        // ---- who says what --------------------------------------------------------------------

        [Test]
        public void SpeakerOf_ResolvesTheIndexToAParticipant()
        {
            ConversationDef d = Two(Line(0, "Some weather."), Line(1, "It is that."));
            Assert.That(d.SpeakerOf(0).Id, Is.EqualTo("npc.a"));
            Assert.That(d.SpeakerOf(1).Id, Is.EqualTo("npc.b"));
        }

        /// <summary>
        /// ⭐ The typo that matters. A speaker index outside the cast must resolve to NOBODY, so the
        /// director skips the line — the alternative is a line arriving over whichever villager happened
        /// to be at index 0, which reads as a bug in the writing rather than in the data.
        /// </summary>
        [Test]
        public void SpeakerOf_IsNullForAnIndexOutsideTheCast()
        {
            ConversationDef d = Two(Line(2, "Who said that?"), Line(-1, "Not me."));
            Assert.IsNull(d.SpeakerOf(0));
            Assert.IsNull(d.SpeakerOf(1));
        }

        [Test]
        public void SpeakerOf_IsNullForALineThatDoesNotExist()
        {
            ConversationDef d = Two(Line(0, "Hm."));
            Assert.IsNull(d.SpeakerOf(5));
            Assert.IsNull(d.SpeakerOf(-1));
        }

        // ---- voices ---------------------------------------------------------------------------

        [Test]
        public void VoiceId_PrefersTheLinesOwnVoice()
        {
            ConversationDef d = Two(Line(0, "Hm.", Voice("voice.line")));
            d.Participants[0].Voice = Voice("voice.speaker");
            Assert.That(d.VoiceIdFor(0), Is.EqualTo("voice.line"));
        }

        [Test]
        public void VoiceId_FallsBackToTheSpeakersOwnVoice()
        {
            ConversationDef d = Two(Line(0, "Hm."));
            d.Participants[0].Voice = Voice("voice.speaker");
            Assert.That(d.VoiceIdFor(0), Is.EqualTo("voice.speaker"),
                        "an overheard line should sound like the person saying it, the same as a spoken " +
                        "one does");
        }

        [Test]
        public void VoiceId_IsNullWhenNobodyNamesOne()
        {
            // Null means the default cadence, never a silent or stalled bubble.
            Assert.IsNull(Two(Line(0, "Hm.")).VoiceIdFor(0));
        }

        [Test]
        public void VoiceId_IsNullForALineThatDoesNotExist()
        {
            Assert.IsNull(Two(Line(0, "Hm.")).VoiceIdFor(9));
        }

        // ---- ids ------------------------------------------------------------------------------

        [Test]
        public void ParticipantId_IsNullOutsideTheCast()
        {
            ConversationDef d = Two(Line(0, "Hm."));
            Assert.That(d.ParticipantId(0), Is.EqualTo("npc.a"));
            Assert.That(d.ParticipantId(1), Is.EqualTo("npc.b"));
            Assert.IsNull(d.ParticipantId(2));
            Assert.IsNull(d.ParticipantId(-1));
        }

        [Test]
        public void TheCastSizeIsNamed_NotScatteredAsATwo()
        {
            Assert.That(ConversationDef.ParticipantCount, Is.EqualTo(2));
        }
    }
}
