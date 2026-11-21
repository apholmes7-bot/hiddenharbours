using System;
using System.Text;
using Unity.Netcode;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>A bounded named-message payload: sender id, UTF-8 byte count, plain text.</summary>
    public static class CoopChatWire
    {
        // Protocol ceilings, not balance values. The asset's limits must fit within this envelope.
        public const int MaxTextBytes = 1024;
        public const int HeaderBytes = sizeof(ulong) + sizeof(ushort);
        public const int MaxPacketBytes = HeaderBytes + MaxTextBytes;

        public static void Write(FastBufferWriter writer, in CoopChatMessage message)
        {
            string text = message.Text ?? string.Empty;
            if (CoopChatPolicy.Utf8.GetByteCount(text) > MaxTextBytes) throw new ArgumentOutOfRangeException(nameof(message));
            byte[] bytes = CoopChatPolicy.Utf8.GetBytes(text);
            writer.WriteValueSafe(message.SenderId);
            writer.WriteValueSafe((ushort)bytes.Length);
            writer.WriteBytesSafe(bytes);
        }

        public static bool TryRead(FastBufferReader reader, out CoopChatMessage message)
        {
            message = default;
            if (!reader.TryBeginRead(HeaderBytes)) return false;
            reader.ReadValueSafe(out ulong sender);
            reader.ReadValueSafe(out ushort count);
            // Check the size BEFORE allocating, and reject trailing data or truncated payloads.
            if (count == 0 || count > MaxTextBytes || reader.Length - reader.Position != count ||
                !reader.TryBeginRead(count)) return false;
            var bytes = new byte[count];
            reader.ReadBytesSafe(ref bytes, count);
            try { message = new CoopChatMessage(sender, CoopChatPolicy.Utf8.GetString(bytes)); }
            catch (DecoderFallbackException) { return false; }
            return true;
        }
    }
}
