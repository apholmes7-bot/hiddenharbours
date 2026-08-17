using NUnit.Framework;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>The bubble filling, and the clicks that ARE its voice.</b> Pure: fed a list of deltas rather
    /// than a clock, so the cadence is a number here instead of a thing you have to sit and listen to.
    ///
    /// <para>What is pinned: the rate is the voice's rate; punctuation breathes; whitespace never ticks;
    /// the stride is honoured; and completing a line with an impatient press does not fire eighty ticks
    /// in one frame — the failure mode a "just publish every character" implementation would ship.</para>
    /// </summary>
    public class DialogueTypewriterTests
    {
        static DialogueVoice Voice(float charsPerSecond = 100f, int stride = 1, float pause = 0f)
            => new DialogueVoice
            {
                CharactersPerSecond = charsPerSecond,
                CharactersPerTick = stride,
                PunctuationPauseSeconds = pause,
                TimbreId = "timbre.test",
            };

        static int DrainTicks(DialogueTypewriter t)
        {
            int n = 0;
            while (t.TryTakeTick(out _, out _)) n++;
            return n;
        }

        [Test]
        public void ALineStartsEmptyAndFillsAtTheVoicesRate()
        {
            var t = new DialogueTypewriter("abcdefghij", Voice(charsPerSecond: 8f));

            Assert.That(t.Revealed, Is.Zero, "nothing is on screen before any time has passed");
            Assert.That(t.VisibleText, Is.Empty);
            Assert.That(t.IsComplete, Is.False);

            Assert.That(t.Advance(0.06f), Is.Zero, "half a character is no characters");
            Assert.That(t.Advance(0.07f), Is.EqualTo(1), "8 chars/s means one every 125 ms");
            Assert.That(t.VisibleText, Is.EqualTo("a"));

            t.Advance(0.5f);
            Assert.That(t.Revealed, Is.EqualTo(5), "half a second is four more");
            Assert.That(t.VisibleText, Is.EqualTo("abcde"));
        }

        [Test]
        public void ALongFrameRevealsSeveralCharacters_SoAHitchDoesNotSlowTheLineDown()
        {
            var t = new DialogueTypewriter("abcdefghij", Voice(charsPerSecond: 8f));
            Assert.That(t.Advance(0.4f), Is.EqualTo(3), "0.4 s at 125 ms a character is three of them");
            Assert.That(t.Revealed, Is.EqualTo(3));
        }

        [Test]
        public void ItFillsToTheEndAndStopsThere()
        {
            var t = new DialogueTypewriter("abc", Voice(charsPerSecond: 8f));
            t.Advance(10f);

            Assert.That(t.IsComplete, Is.True);
            Assert.That(t.Revealed, Is.EqualTo(3));
            Assert.That(t.VisibleText, Is.EqualTo("abc"));
            Assert.That(t.Advance(10f), Is.Zero, "a finished line reveals nothing more");
        }

        [Test]
        public void AnEmptyLineIsAlreadyComplete()
        {
            var t = new DialogueTypewriter("", Voice());
            Assert.That(t.IsComplete, Is.True);
            Assert.That(t.Advance(1f), Is.Zero);
            Assert.That(DrainTicks(t), Is.Zero);
        }

        [Test]
        public void PunctuationBreathes_TheNextCharacterWaitsTheVoicesPause()
        {
            // 8 chars/s (125 ms each) with a 250 ms breath after the comma.
            var t = new DialogueTypewriter("a,b", Voice(charsPerSecond: 8f, pause: 0.25f));

            t.Advance(0.3f);
            Assert.That(t.VisibleText, Is.EqualTo("a,"), "two characters in three tenths");

            t.Advance(0.2f);
            Assert.That(t.VisibleText, Is.EqualTo("a,"),
                        "the character after the comma waits out the breath rather than arriving on time");

            t.Advance(0.2f);
            Assert.That(t.VisibleText, Is.EqualTo("a,b"), "and then it lands");
        }

        [Test]
        public void WhitespaceNeverTicks_ASpaceIsAGapNotABeat()
        {
            var t = new DialogueTypewriter("a b", Voice(stride: 1));
            t.Advance(10f);

            int ticks = 0;
            while (t.TryTakeTick(out _, out char c))
            {
                Assert.That(char.IsWhiteSpace(c), Is.False, "a space made a sound");
                ticks++;
            }
            Assert.That(ticks, Is.EqualTo(2), "'a' and 'b' tick; the space between them does not");
        }

        [Test]
        public void TheStrideIsHonoured_SoAChattyCadenceIsARhythmAndNotABuzz()
        {
            var t = new DialogueTypewriter("abcdef", Voice(stride: 2));
            t.Advance(10f);

            Assert.That(DrainTicks(t), Is.EqualTo(3), "one tick every two audible characters");
        }

        [Test]
        public void TicksArriveAsTheCharactersDo_NotAllAtTheEnd()
        {
            var t = new DialogueTypewriter("abcd", Voice(charsPerSecond: 8f, stride: 1));

            t.Advance(0.3f);
            Assert.That(DrainTicks(t), Is.EqualTo(2), "two characters revealed, two ticks owed");
            Assert.That(DrainTicks(t), Is.Zero, "and drained is drained");

            t.Advance(0.3f);
            Assert.That(DrainTicks(t), Is.EqualTo(2));
        }

        [Test]
        public void ATickNamesTheCharacterItIs_SoAudioNeedNotReDeriveIt()
        {
            var t = new DialogueTypewriter("hi", Voice(stride: 1));
            t.Advance(10f);

            Assert.That(t.TryTakeTick(out int i0, out char c0), Is.True);
            Assert.That(i0, Is.Zero);
            Assert.That(c0, Is.EqualTo('h'));

            Assert.That(t.TryTakeTick(out int i1, out char c1), Is.True);
            Assert.That(i1, Is.EqualTo(1));
            Assert.That(c1, Is.EqualTo('i'));

            Assert.That(t.TryTakeTick(out _, out _), Is.False);
        }

        [Test]
        public void CompletingWithAPress_ShowsEverythingAndFiresNoBurstOfTicks()
        {
            // ⭐ The sabotage arm for the audio seam: an impatient press must not machine-gun eighty
            // clicks in one frame. It fills the line and the ticks it skipped are gone.
            var t = new DialogueTypewriter("the whole line, all at once", Voice(charsPerSecond: 8f));
            t.Advance(0.3f);
            DrainTicks(t);

            t.CompleteNow();

            Assert.That(t.IsComplete, Is.True);
            Assert.That(t.VisibleText, Is.EqualTo("the whole line, all at once"));
            Assert.That(DrainTicks(t), Is.Zero, "the skipped characters do not all click at once");
        }

        [Test]
        public void AZeroedVoice_FillsAtTheDefaultRatherThanHangingForever()
        {
            // What an asset deserialized before DialogueVoice existed looks like: every field zero. A
            // voice that filled at 0 chars/s would leave a permanently empty bubble and no way past it.
            var t = new DialogueTypewriter("abc", default(DialogueVoice));

            Assert.That(t.Voice.CharactersPerSecond,
                        Is.EqualTo(DialogueVoice.DefaultCharactersPerSecond).Within(0.001f));
            Assert.That(t.Voice.CharactersPerTick, Is.EqualTo(DialogueVoice.DefaultCharactersPerTick));

            t.Advance(1f);
            Assert.That(t.IsComplete, Is.True, "a second is plenty at any sane default");
        }

        [Test]
        public void TheVisibleTextIsRebuiltOnlyWhenItChanges()
        {
            // Rule 7 in miniature: a finished bubble must not allocate a string every frame.
            var t = new DialogueTypewriter("abc", Voice(charsPerSecond: 8f));
            t.Advance(10f);

            string first = t.VisibleText;
            Assert.That(ReferenceEquals(first, t.VisibleText), Is.True,
                        "the same string instance comes back while nothing has changed");
        }

        [Test]
        public void BreathMarksAreTheSentenceAndClauseOnes()
        {
            foreach (char c in new[] { '.', '!', '?', ',', ';', ':', '—' })
                Assert.That(DialogueTypewriter.IsBreath(c), Is.True, $"'{c}' should breathe");
            foreach (char c in new[] { 'a', '1', '-', '\'' })
                Assert.That(DialogueTypewriter.IsBreath(c), Is.False, $"'{c}' should not");
        }
    }
}
