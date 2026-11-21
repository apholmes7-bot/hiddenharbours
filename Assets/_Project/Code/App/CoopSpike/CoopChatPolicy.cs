using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>Session-local tuning, serialized in the trial settings; never saved with the world.</summary>
    [Serializable]
    public sealed class CoopChatLimits
    {
        public int MaxCharacters = 240;
        public int MaxUtf8Bytes = 960;
        public int MessagesPerWindow = 4;
        public double WindowSeconds = 10;

        internal void Validate()
        {
            if (MaxCharacters < 1 || MaxCharacters > CoopChatWire.MaxTextBytes ||
                MaxUtf8Bytes < 1 || MaxUtf8Bytes > CoopChatWire.MaxTextBytes ||
                MessagesPerWindow < 1 || MessagesPerWindow > 64 ||
                double.IsNaN(WindowSeconds) || double.IsInfinity(WindowSeconds) || WindowSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(CoopChatLimits), "Invalid chat limits.");
        }

        internal CoopChatLimits Snapshot()
        {
            Validate();
            return (CoopChatLimits)MemberwiseClone();
        }
    }

    public readonly struct CoopChatMessage
    {
        public readonly ulong SenderId;
        public readonly string Text;
        public CoopChatMessage(ulong senderId, string text) { SenderId = senderId; Text = text; }
    }

    /// <summary>Host authority: normalize, validate and enforce a rolling per-sender budget.</summary>
    public sealed class CoopChatPolicy
    {
        private readonly CoopChatLimits _limits;
        private readonly Dictionary<ulong, Queue<double>> _sent = new();
        private double _lastTime = double.NegativeInfinity;
        internal static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public CoopChatPolicy(CoopChatLimits limits) => _limits = (limits ?? throw new ArgumentNullException(nameof(limits))).Snapshot();

        public bool TryNormalize(string input, out string text)
        {
            text = null;
            if (input == null) return false;
            string trimmed = input.Trim();
            if (trimmed.Length == 0 || trimmed.Length > _limits.MaxCharacters) return false;
            // Plain single-line text only. Reject markup delimiters and invisible control/format
            // characters, including bidi overrides. Presenters must also disable rich-text parsing.
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                var category = CharUnicodeInfo.GetUnicodeCategory(trimmed, i);
                if (c == '<' || c == '>' || char.IsControl(c) || category == UnicodeCategory.Format ||
                    category == UnicodeCategory.LineSeparator || category == UnicodeCategory.ParagraphSeparator) return false;
                if (char.IsHighSurrogate(c) && i + 1 < trimmed.Length && char.IsLowSurrogate(trimmed[i + 1])) i++;
            }
            try { if (Utf8.GetByteCount(trimmed) > _limits.MaxUtf8Bytes) return false; }
            catch (EncoderFallbackException) { return false; }
            text = trimmed;
            return true;
        }

        // now is elapsed transport time supplied by the caller, independent of the paused game clock.
        // The result is deterministic for the same sender/text/time sequence.
        public bool TryAccept(ulong authenticatedSenderId, string input, double now, out CoopChatMessage message)
        {
            message = default;
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0 || now < _lastTime) return false;
            _lastTime = now;
            if (!TryNormalize(input, out string text)) return false;
            if (!_sent.TryGetValue(authenticatedSenderId, out var sent))
                _sent.Add(authenticatedSenderId, sent = new Queue<double>(_limits.MessagesPerWindow));
            while (sent.Count != 0 && now - sent.Peek() >= _limits.WindowSeconds) sent.Dequeue();
            if (sent.Count == _limits.MessagesPerWindow) return false;
            sent.Enqueue(now);
            message = new CoopChatMessage(authenticatedSenderId, text);
            return true;
        }

        public void RemoveSender(ulong senderId) => _sent.Remove(senderId);
    }
}
