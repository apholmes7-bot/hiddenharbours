namespace HiddenHarbours.Core
{
    /// <summary>Chat's submit/open and cancel edges. Text comes from the one-line field, not key polling.</summary>
    public readonly struct ChatIntents
    {
        public readonly bool Submit;
        public readonly bool Cancel;
        public ChatIntents(bool submit, bool cancel) { Submit = submit; Cancel = cancel; }
        public static ChatIntents None => default;
    }

    public sealed class HeldChatIntents : HeldIntents<ChatIntents> { }
}
