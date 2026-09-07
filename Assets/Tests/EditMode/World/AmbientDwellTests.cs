using NUnit.Framework;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>How long an unprompted bubble stays up</b> — the arithmetic that replaces the Interact press.
    ///
    /// <para>An ambient bubble has nothing to wait for, so <see cref="AmbientDwell"/> is the only thing
    /// standing between "the line was read" and "the line blinked". It is also the number a conversation
    /// director schedules its second speaker on BEFORE the first has finished, which is what keeps an
    /// exchange the same length on a fast machine and a slow one.</para>
    ///
    /// <para><b>Two kinds of assertion here, and both are needed.</b> The absolute ones pin hand-computed
    /// seconds, so the rule cannot drift quietly. The agreement ones drive the real
    /// <see cref="DialogueTypewriter"/> a millisecond at a time and check it finishes exactly when the
    /// closed form says it will — a cross-check between two independent transcriptions of one rule, not
    /// a mirror: neither implementation can be edited into agreement with the other without the absolute
    /// numbers going red too.</para>
    /// </summary>
    public class AmbientDwellTests
    {
        /// <summary>A voice with round numbers, so every expected second below is arithmetic a reader can
        /// do in their head rather than a recorded output.</summary>
        static DialogueVoice Voice(float cps = 10f, float pause = 0.5f, float read = 2f)
            => new DialogueVoice
            {
                CharactersPerSecond = cps,
                CharactersPerTick = 1,
                PunctuationPauseSeconds = pause,
                ReadPauseSeconds = read,
            };

        // ---- the fill ---------------------------------------------------------------------------

        [Test]
        public void Fill_IsCharacterCountOverRate_WhenThereIsNoPunctuation()
        {
            // "abcde" = 5 characters at 10/s.
            Assert.That(AmbientDwell.FillSeconds("abcde", Voice()), Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void Fill_ChargesAPauseForEachInteriorMark()
        {
            // "ab,cd" = 5 chars (0.5 s) + one interior comma (0.5 s).
            Assert.That(AmbientDwell.FillSeconds("ab,cd", Voice()), Is.EqualTo(1.0f).Within(1e-4f));
        }

        /// <summary>
        /// ⭐ The one that is easy to get wrong, and it is wrong in the expensive direction: counting
        /// EVERY mark over-reports every sentence that ends in a full stop, which is most of them, and
        /// leaves a bubble standing for a beat nobody asked for.
        /// </summary>
        [Test]
        public void Fill_DoesNotChargeAPauseForAMarkInTheLastPosition()
        {
            // "abcd." = 5 chars (0.5 s) and NOTHING for the stop: there is no next character to delay.
            Assert.That(AmbientDwell.FillSeconds("abcd.", Voice()), Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void Fill_OfASingleMark_IsJustTheCharacter()
        {
            Assert.That(AmbientDwell.FillSeconds(".", Voice()), Is.EqualTo(0.1f).Within(1e-4f));
        }

        [Test]
        public void Fill_OfNothing_IsZero()
        {
            Assert.That(AmbientDwell.FillSeconds("", Voice()), Is.EqualTo(0f));
            Assert.That(AmbientDwell.FillSeconds(null, Voice()), Is.EqualTo(0f));
        }

        // ---- agreement with the thing that actually fills -----------------------------------------

        [Test]
        public void Fill_IsExactlyWhenTheTypewriterFinishes()
        {
            const string line = "She'd swim again. Buying her is one price. Putting her right is another.";
            DialogueVoice voice = Voice(cps: 42f, pause: 0.16f, read: 1.4f);

            float expected = AmbientDwell.FillSeconds(line, voice);
            var typewriter = new DialogueTypewriter(line, voice);

            // A hair BEFORE the closed form says it lands, the line must still be filling. Advancing in
            // one big step would hide an error in either direction, so this is a real tick loop.
            const float step = 0.001f;
            float t = 0f;
            while (t < expected - 0.01f)
            {
                typewriter.Advance(step);
                t += step;
            }
            Assert.IsFalse(typewriter.IsComplete,
                $"the closed form says this line takes {expected:0.###} s, but the typewriter had " +
                $"already finished at {t:0.###} s — one of the two is wrong");

            // ...and a hair after, it must be done.
            while (t < expected + 0.01f)
            {
                typewriter.Advance(step);
                t += step;
            }
            Assert.IsTrue(typewriter.IsComplete,
                $"the closed form says this line takes {expected:0.###} s, but the typewriter was still " +
                $"filling at {t:0.###} s");
        }

        [Test]
        public void Fill_AgreesWithTheTypewriter_AcrossAwkwardLines()
        {
            DialogueVoice voice = Voice(cps: 20f, pause: 0.3f, read: 1f);
            string[] lines =
            {
                "Ayuh.",                       // ends on a mark
                "Well, now — that's a boat.",  // an em dash and two clause marks
                "...",                         // nothing but marks
                "a",                           // one character
                "no marks at all here",
            };

            foreach (string line in lines)
            {
                float expected = AmbientDwell.FillSeconds(line, voice);
                var typewriter = new DialogueTypewriter(line, voice);
                const float step = 0.0005f;
                float t = 0f;
                while (t < expected - 0.005f) { typewriter.Advance(step); t += step; }
                Assert.IsFalse(typewriter.IsComplete, $"\"{line}\" finished early");
                while (t < expected + 0.005f) { typewriter.Advance(step); t += step; }
                Assert.IsTrue(typewriter.IsComplete, $"\"{line}\" had not finished at {t:0.###} s");
            }
        }

        // ---- the whole life ----------------------------------------------------------------------

        [Test]
        public void Seconds_IsTheFillPlusTheReadPause()
        {
            DialogueVoice voice = Voice(read: 2f);
            float fill = AmbientDwell.FillSeconds("abcde", voice);
            Assert.That(AmbientDwell.Seconds("abcde", voice), Is.EqualTo(fill + 2f).Within(1e-4f));
        }

        [Test]
        public void Seconds_OfNothing_IsZero()
        {
            // Not "a read pause over an empty bubble" — which is what makes refusing the request the
            // presenter's correct answer to an empty line.
            Assert.That(AmbientDwell.Seconds("", Voice()), Is.EqualTo(0f));
            Assert.That(AmbientDwell.Seconds(null, Voice()), Is.EqualTo(0f));
        }

        /// <summary>
        /// ⭐ What a voice asset authored BEFORE the read-pause field existed deserializes to: every
        /// field zero. A zero read pause would close the bubble in the same frame its last character
        /// landed, so <see cref="DialogueVoice.Sanitised"/> has to read zero as "unauthored" here — the
        /// opposite of what it does for the punctuation pause, where zero is a real choice.
        /// </summary>
        [Test]
        public void Seconds_OfAZeroedVoice_StillStandsForTheDefaultReadPause()
        {
            var stale = default(DialogueVoice);

            float fill = AmbientDwell.FillSeconds("abcde", stale);
            float dwell = AmbientDwell.Seconds("abcde", stale);

            Assert.That(dwell - fill,
                        Is.EqualTo(DialogueVoice.DefaultReadPauseSeconds).Within(1e-4f),
                        "a voice asset written before ReadPauseSeconds existed must not close its " +
                        "bubbles instantly");
            Assert.That(dwell, Is.GreaterThan(fill));
        }

        [Test]
        public void Seconds_IsAlwaysLongerThanTheFill_ForAnyRealLine()
        {
            // The property behind the arithmetic: a bubble is never taken away before it is readable.
            foreach (float read in new[] { 0.1f, 1.4f, 5f })
            {
                DialogueVoice voice = Voice(read: read);
                Assert.That(AmbientDwell.Seconds("a line of words", voice),
                            Is.GreaterThan(AmbientDwell.FillSeconds("a line of words", voice)));
            }
        }
    }
}
