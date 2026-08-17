using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>Choosing a row, and never choosing the wrong one.</b> The picker is pure — a float in, a cursor
    /// out — so the sabotage arm ("the option picker must never confirm an option the selection isn't
    /// on") is a property assertion here rather than something to be careful about at a keyboard.
    ///
    /// <para>It also pins the two guarantees that make an option bubble safe to author: the close row is
    /// always last and always present, and a half-typed row is skipped rather than shown blank.</para>
    /// </summary>
    public class DialogueOptionPickerTests
    {
        const float Push = 1f;
        const float Neutral = 0f;

        static DialogueOption Row(string id, string label, params string[] reply)
            => new DialogueOption { Id = id, Label = label, ReplyLines = reply };

        // ---- the cursor ---------------------------------------------------------------------

        [Test]
        public void ItStartsOnTheFirstRow()
        {
            var p = new DialogueOptionPicker(3);
            Assert.That(p.Index, Is.Zero);
            Assert.That(p.Count, Is.EqualTo(3));
        }

        [Test]
        public void PushingDownStepsOneRow_AndTheAxisMustReturnToNeutralBeforeTheNext()
        {
            var p = new DialogueOptionPicker(4);

            Assert.That(p.Step(-Push), Is.True, "the first push steps");
            Assert.That(p.Index, Is.EqualTo(1));

            Assert.That(p.Step(-Push), Is.False, "⭐ HELD is not HELD-AGAIN — one row per push");
            Assert.That(p.Step(-Push), Is.False);
            Assert.That(p.Index, Is.EqualTo(1), "a held key must not rip through four rows in four frames");

            Assert.That(p.Step(Neutral), Is.False, "letting go is not a step");
            Assert.That(p.IsLatched, Is.False);

            Assert.That(p.Step(-Push), Is.True, "and now it steps again");
            Assert.That(p.Index, Is.EqualTo(2));
        }

        [Test]
        public void PushingUpStepsTowardTheFirstRow_BecauseRowZeroIsDrawnAtTheTop()
        {
            var p = new DialogueOptionPicker(3);
            p.Step(-Push); p.Step(Neutral);
            p.Step(-Push); p.Step(Neutral);
            Assert.That(p.Index, Is.EqualTo(2));

            Assert.That(p.Step(Push), Is.True);
            Assert.That(p.Index, Is.EqualTo(1), "up the list is toward the row drawn at the top");
        }

        [Test]
        public void TheCursorWrapsAtBothEnds()
        {
            var p = new DialogueOptionPicker(3);

            p.Step(Push);                       // up from row 0
            Assert.That(p.Index, Is.EqualTo(2), "off the top lands on the bottom");

            p.Step(Neutral);
            p.Step(-Push);
            Assert.That(p.Index, Is.Zero, "and off the bottom lands on the top");
        }

        [Test]
        public void ADriftingAxisInsideTheDeadZone_NeverStepsARow()
        {
            var p = new DialogueOptionPicker(4);
            float drift = DialogueOptionPicker.AxisThreshold * 0.9f;

            for (int i = 0; i < 60; i++)
                Assert.That(p.Step(i % 2 == 0 ? drift : -drift), Is.False);

            Assert.That(p.Index, Is.Zero, "stick drift is not a choice");
        }

        [Test]
        public void ASingleRow_HasNowhereToGo()
        {
            var p = new DialogueOptionPicker(1);
            Assert.That(p.Step(-Push), Is.False);
            Assert.That(p.Step(Push), Is.False);
            Assert.That(p.Index, Is.Zero);
        }

        // ---- confirming ---------------------------------------------------------------------

        [Test]
        public void ConfirmReturnsTheRowTheCursorIsOn_Always()
        {
            // ⭐ THE SABOTAGE ARM. Confirm takes no argument and reads no input, so there is no path by
            // which it can land anywhere but Index — swept over every reachable cursor position.
            var p = new DialogueOptionPicker(4);
            for (int step = 0; step < 12; step++)
            {
                Assert.That(p.Confirm(out int index), Is.True);
                Assert.That(index, Is.EqualTo(p.Index),
                            "confirm landed on a row the selection was not on");
                p.Step(-Push);
                p.Step(Neutral);
            }
        }

        [Test]
        public void AnEmptyPickerConfirmsNothing()
        {
            var p = new DialogueOptionPicker(0);
            Assert.That(p.Confirm(out int index), Is.False);
            Assert.That(index, Is.EqualTo(-1), "a caller must never be handed a row out of an empty list");
        }

        // ---- the rows on offer --------------------------------------------------------------

        [Test]
        public void TheCloseRowIsAppended_AlwaysLastAndAlwaysThere()
        {
            List<DialogueOption> rows = DialogueOptionPicker.RowsFor(new[]
            {
                Row("option.ask_grounds", "Where's the cod running?"),
                Row("option.ask_price", "What are they paying?"),
            });

            Assert.That(rows, Is.Not.Null);
            Assert.That(rows.Count, Is.EqualTo(3), "two authored rows plus the way out");
            Assert.That(rows[rows.Count - 1].Id, Is.EqualTo(DialogueOption.CloseId));
            Assert.That(rows[rows.Count - 1].Label, Is.EqualTo(WorldStrings.CloseConversationOption));
        }

        [Test]
        public void ContentCannotSmuggleInASecondCloseRow()
        {
            // Two rows claiming the reserved id would make which one you got a coin toss. The authored
            // one is dropped; the appended one is the only way out and it is still last.
            List<DialogueOption> rows = DialogueOptionPicker.RowsFor(new[]
            {
                Row(DialogueOption.CloseId, "Bye!"),
                Row("option.ask_grounds", "Where's the cod running?"),
            });

            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0].Id, Is.EqualTo("option.ask_grounds"));
            Assert.That(rows[1].Id, Is.EqualTo(DialogueOption.CloseId));
            Assert.That(rows[1].Label, Is.EqualTo(WorldStrings.CloseConversationOption),
                        "the appended row's copy wins, so the way out reads the same everywhere");
        }

        [Test]
        public void AHalfTypedRowIsSkipped_NotShownBlank()
        {
            List<DialogueOption> rows = DialogueOptionPicker.RowsFor(new[]
            {
                Row("option.real", "Ask about the grounds"),
                Row("", "no id"),
                Row("option.no_label", ""),
            });

            Assert.That(rows.Count, Is.EqualTo(2), "one real row, plus the close row");
            Assert.That(rows[0].Id, Is.EqualTo("option.real"));
        }

        [Test]
        public void NothingAuthored_MeansNoPickerAtAll_NotABubbleWithOneRow()
        {
            Assert.That(DialogueOptionPicker.RowsFor(null), Is.Null);
            Assert.That(DialogueOptionPicker.RowsFor(new DialogueOption[0]), Is.Null);
            Assert.That(DialogueOptionPicker.RowsFor(new[] { Row("", "") }), Is.Null,
                        "a conversation whose only 'option' is unauthored just ends on its last line");
        }

        [Test]
        public void ARowMayCarryAReply_OrNothingButItsSignal()
        {
            List<DialogueOption> rows = DialogueOptionPicker.RowsFor(new[]
            {
                Row("option.ask", "Where's the cod running?", "Off the ledge, this week."),
                Row("option.front_fee", "Can you front me the fee?"),
            });

            Assert.That(rows[0].ReplyLines.Length, Is.EqualTo(1), "a question can be answered");
            Assert.That(rows[1].ReplyLines, Is.Empty,
                        "and a row that only fires a gameplay signal needs no words of its own");
            Assert.That(rows[2].ReplyLines, Is.Empty, "the close row says nothing; it just ends");
        }
    }
}
