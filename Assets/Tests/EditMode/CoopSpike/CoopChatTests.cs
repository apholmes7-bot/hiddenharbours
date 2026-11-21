using System;
using HiddenHarbours.CoopSpike;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HiddenHarbours.Tests.CoopSpike
{
    public sealed class CoopChatTests
    {
        private static CoopChatLimits Limits() => new() { MaxCharacters = 8, MaxUtf8Bytes = 12, MessagesPerWindow = 2, WindowSeconds = 10 };

        [Test]
        public void CharacterCapIncludesBoundaryAndTrimsBeforeChecking()
        {
            var policy = new CoopChatPolicy(Limits());
            Assert.That(policy.TryNormalize("  abcdefgh  ", out var text), Is.True);
            Assert.That(text, Is.EqualTo("abcdefgh"));
            Assert.That(policy.TryNormalize("abcdefghi", out _), Is.False);
        }

        [Test]
        public void Utf8CapIncludesBoundaryWithoutSplittingACharacter()
        {
            var policy = new CoopChatPolicy(Limits());
            Assert.That(policy.TryNormalize("海海海海", out var text), Is.True);
            Assert.That(text, Is.EqualTo("海海海海"));
            Assert.That(policy.TryNormalize("海海海海海", out _), Is.False);
            Assert.That(policy.TryNormalize("hi 🐟", out text), Is.True);
            Assert.That(text, Is.EqualTo("hi 🐟"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t\r\n")]
        [TestCase("a\nb")]
        [TestCase("a\tb")]
        [TestCase("<b>x")]
        [TestCase("x>")]
        [TestCase("a\u202eb")]
        [TestCase("a\U000e0001b")]
        [TestCase("a\u2028b")]
        [TestCase("a\u2029b")]
        [TestCase("a\0b")]
        public void RejectsEmptyMarkupAndMultilineOrInvisibleControls(string text)
            => Assert.That(new CoopChatPolicy(Limits()).TryNormalize(text, out _), Is.False);

        [Test]
        public void RejectsUnpairedSurrogates()
            => Assert.That(new CoopChatPolicy(Limits()).TryNormalize(new string((char)0xd800, 1), out _), Is.False);

        [Test]
        public void RollingWindowCapsEachSenderAndExpiresAtTheExactBoundary()
        {
            var policy = new CoopChatPolicy(Limits());
            Assert.That(policy.TryAccept(5, "first", 0, out _), Is.True);
            Assert.That(policy.TryAccept(5, "second", 1, out _), Is.True);
            Assert.That(policy.TryAccept(5, "third", 9.999, out _), Is.False);
            Assert.That(policy.TryAccept(6, "other", 9.999, out var other), Is.True);
            Assert.That(other.SenderId, Is.EqualTo(6));
            Assert.That(policy.TryAccept(5, "next", 10, out _), Is.True);
            Assert.That(policy.TryAccept(5, "burst", 10, out _), Is.False);
            Assert.That(policy.TryAccept(5, "next", 11, out _), Is.True);
        }

        [Test]
        public void InvalidTextDoesNotConsumeBudgetAndDisconnectClearsOnlyThatSender()
        {
            var policy = new CoopChatPolicy(Limits());
            Assert.That(policy.TryAccept(5, "<bad>", 0, out _), Is.False);
            for (int i = 0; i < 2; i++)
            {
                Assert.That(policy.TryAccept(5, "ok", 0, out _), Is.True);
                Assert.That(policy.TryAccept(6, "ok", 0, out _), Is.True);
            }
            policy.RemoveSender(5);
            Assert.That(policy.TryAccept(5, "again", 0, out _), Is.True);
            Assert.That(policy.TryAccept(6, "again", 0, out _), Is.False);
        }

        [Test]
        public void ClockRollbackAndNonFiniteTimeCannotResetTheBudget()
        {
            var policy = new CoopChatPolicy(Limits());
            Assert.That(policy.TryAccept(1, "ok", 20, out _), Is.True);
            Assert.That(policy.TryAccept(1, "ok", 19, out _), Is.False);
            Assert.That(policy.TryAccept(1, "ok", double.NaN, out _), Is.False);
            Assert.That(policy.TryAccept(1, "ok", double.PositiveInfinity, out _), Is.False);
            Assert.That(policy.TryAccept(1, "ok", 20, out _), Is.True);
            Assert.That(policy.TryAccept(1, "ok", 20, out _), Is.False);
        }

        [Test]
        public void SessionSnapshotsDataAndRejectsAnInvalidConfiguration()
        {
            var limits = Limits();
            var policy = new CoopChatPolicy(limits);
            limits.MaxCharacters = 100;
            Assert.That(policy.TryNormalize("abcdefghi", out _), Is.False);
            limits.MaxUtf8Bytes = CoopChatWire.MaxTextBytes + 1;
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoopChatPolicy(limits));
            limits = Limits(); limits.WindowSeconds = double.NaN;
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoopChatPolicy(limits));
            limits = Limits(); limits.MessagesPerWindow = 0;
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoopChatPolicy(limits));
        }

        [Test]
        public void SenderAndUnicodeTextRoundTripThroughTheTransportBuffers()
        {
            using var writer = new FastBufferWriter(CoopChatWire.MaxPacketBytes, Allocator.Temp);
            CoopChatWire.Write(writer, new CoopChatMessage(ulong.MaxValue, "Hé! 🐟"));
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            Assert.That(CoopChatWire.TryRead(reader, out var message), Is.True);
            Assert.That(message.SenderId, Is.EqualTo(ulong.MaxValue));
            Assert.That(message.Text, Is.EqualTo("Hé! 🐟"));
        }

        [TestCase(0, 0)]
        [TestCase(1025, 0)]
        [TestCase(3, 2)]
        [TestCase(1, 2)]
        public void MalformedLengthsAreRejectedBeforeAllocatingText(int declared, int actual)
        {
            using var writer = new FastBufferWriter(CoopChatWire.MaxPacketBytes, Allocator.Temp);
            writer.WriteValueSafe(4UL); writer.WriteValueSafe((ushort)declared);
            writer.WriteBytesSafe(new byte[actual]);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            Assert.That(CoopChatWire.TryRead(reader, out _), Is.False);
        }

        [Test]
        public void TruncatedHeaderAndInvalidUtf8AreRejected()
        {
            using var writer = new FastBufferWriter(CoopChatWire.MaxPacketBytes, Allocator.Temp);
            writer.WriteValueSafe(4UL);
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
                Assert.That(CoopChatWire.TryRead(reader, out _), Is.False);
            writer.WriteValueSafe((ushort)1); writer.WriteBytesSafe(new byte[] { 0xff });
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
                Assert.That(CoopChatWire.TryRead(reader, out _), Is.False);
        }

        [Test]
        public void TrialSettingsCarryValidExplicitChatLimits()
        {
            var settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.Chat.MaxCharacters, Is.EqualTo(240));
            Assert.That(settings.Chat.MaxUtf8Bytes, Is.EqualTo(960));
            Assert.That(settings.Chat.MessagesPerWindow, Is.EqualTo(4));
            Assert.That(settings.Chat.WindowSeconds, Is.EqualTo(10));
            Assert.DoesNotThrow(() => new CoopChatPolicy(settings.Chat));
        }
    }
}
