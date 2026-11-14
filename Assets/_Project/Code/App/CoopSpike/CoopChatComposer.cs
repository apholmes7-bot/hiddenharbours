using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>Nonvisual composition state. A future presenter owns focus, text and lifecycle.</summary>
    public sealed class CoopChatComposer : IDisposable
    {
        private readonly IControlIntentSource<ChatIntents> _input;
        private readonly Func<string, bool> _send;
        private bool _disposed;
        public bool IsOpen { get; private set; }
        public string Draft { get; private set; } = string.Empty;

        public CoopChatComposer(IControlIntentSource<ChatIntents> input, Func<string, bool> send)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _send = send ?? throw new ArgumentNullException(nameof(send));
        }

        public void SetDraft(string draft)
        {
            if (IsOpen) Draft = draft ?? string.Empty;
        }

        public void Tick()
        {
            if (_disposed) return;
            ChatIntents intents = _input.Read();
            if (intents.Cancel) { Close(); return; }
            if (!intents.Submit) return;
            if (!IsOpen) { IsOpen = true; Draft = string.Empty; return; }
            // Keep rejected text available for correction. A client send is a submission, not a
            // display receipt; only the session's host-authenticated Received event is displayed.
            string trimmed = Draft.Trim();
            if (trimmed.Length == 0 || _send(trimmed)) Close();
        }

        public void Close() { IsOpen = false; Draft = string.Empty; }
        public void Dispose() { Close(); _disposed = true; }

        /// <summary>Wrap a mode at its source, before gameplay consumes its intents. Construct once.</summary>
        public IControlIntentSource<T> Gate<T>(IControlIntentSource<T> source) where T : struct
            => new TypingGate<T>(this, source ?? throw new ArgumentNullException(nameof(source)));

        private sealed class TypingGate<T> : IControlIntentSource<T> where T : struct
        {
            private readonly CoopChatComposer _composer;
            private readonly IControlIntentSource<T> _source;
            public TypingGate(CoopChatComposer composer, IControlIntentSource<T> source)
            { _composer = composer; _source = source; }
            public T Read() => _composer.IsOpen ? default : _source.Read();
        }
    }
}
