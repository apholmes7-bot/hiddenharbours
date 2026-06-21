using NUnit.Framework;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// The dialogue advance/close state machine (VS-21). Pure logic, no Unity lifecycle: covers
    /// opening at the first line, advancing through to the last, the close-on-last-line return, early
    /// close, and the empty-conversation edge.
    /// </summary>
    public class DialogueRunnerTests
    {
        private static DialogueRunner Make(params string[] texts)
        {
            var lines = new DialogueLine[texts.Length];
            for (int i = 0; i < texts.Length; i++)
                lines[i] = new DialogueLine("Ginny", null, texts[i]);
            return new DialogueRunner(lines);
        }

        [Test]
        public void Open_StartsAtFirstLine()
        {
            var r = Make("a", "b", "c");
            Assert.IsFalse(r.IsOpen, "starts closed before Open()");

            r.Open();

            Assert.IsTrue(r.IsOpen);
            Assert.AreEqual(0, r.Index);
            Assert.AreEqual("a", r.Current.Text);
            Assert.IsTrue(r.HasNext);
        }

        [Test]
        public void Advance_StepsThroughLines_ThenClosesOnLast()
        {
            var r = Make("a", "b", "c");
            r.Open();

            Assert.IsTrue(r.Advance(), "advancing to line 2 keeps it open");
            Assert.AreEqual(1, r.Index);
            Assert.AreEqual("b", r.Current.Text);

            Assert.IsTrue(r.Advance(), "advancing to line 3 keeps it open");
            Assert.AreEqual(2, r.Index);
            Assert.IsFalse(r.HasNext, "on the last line there's nothing next");

            Assert.IsFalse(r.Advance(), "advancing past the last line closes and returns false");
            Assert.IsFalse(r.IsOpen);
            Assert.AreEqual(-1, r.Index);
        }

        [Test]
        public void SingleLine_AdvanceClosesImmediately()
        {
            var r = Make("only");
            r.Open();
            Assert.IsTrue(r.IsOpen);
            Assert.IsFalse(r.HasNext);

            Assert.IsFalse(r.Advance(), "a one-line conversation closes on the first advance");
            Assert.IsFalse(r.IsOpen);
        }

        [Test]
        public void Close_EndsConversationEarly()
        {
            var r = Make("a", "b", "c");
            r.Open();
            r.Advance(); // on line 2 of 3

            r.Close();

            Assert.IsFalse(r.IsOpen);
            Assert.AreEqual(-1, r.Index);
            Assert.IsFalse(r.Advance(), "advancing a closed runner is a no-op");
        }

        [Test]
        public void Empty_StaysClosed()
        {
            var r = Make();
            r.Open();

            Assert.IsFalse(r.IsOpen, "no lines → nothing to show");
            Assert.IsFalse(r.HasNext);
            Assert.IsFalse(r.Advance());
        }

        [Test]
        public void NullLines_AreTreatedAsEmpty()
        {
            var r = new DialogueRunner(null);
            r.Open();
            Assert.IsFalse(r.IsOpen);
            Assert.AreEqual(0, r.Count);
        }
    }
}
