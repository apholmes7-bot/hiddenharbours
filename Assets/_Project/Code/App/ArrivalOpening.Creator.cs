using HiddenHarbours.Core;

namespace HiddenHarbours.App
{
    public sealed partial class ArrivalOpening
    {
        static bool _characterCreatorOpen;
        static ArrivalOpening _creatorHandover;

        void OpenCharacterCreator()
        {
            if (_characterCreatorOpen || GameServices.Save?.Current == null) return;
            if (!EventBus.HasSubscribers<CharacterCreatorRequested>()) return;
            _characterCreatorOpen = true;
            EventBus.Subscribe<CharacterCreatorClosed>(OnCreatorClosed);
            EventBus.Publish(new CharacterCreatorRequested(true));
        }

        static void OnCreatorClosed(CharacterCreatorClosed closed)
        {
            _characterCreatorOpen = false;
            EventBus.Unsubscribe<CharacterCreatorClosed>(OnCreatorClosed);
            var pending = _creatorHandover;
            _creatorHandover = null;
            if (pending != null) pending.HandOver();
        }

        bool HoldHandoverForCharacterCreator()
        {
            if (!_characterCreatorOpen) return false;
            _creatorHandover = this;
            CloseCharacterCreator();
            // A synchronous acknowledgement may already have completed the pending handover.
            return true;
        }

        static void CloseCharacterCreator()
        {
            if (_characterCreatorOpen) EventBus.Publish(new CharacterCreatorRequested(false));
        }

        static void ResetCharacterCreator()
        {
            _creatorHandover = null;
            CloseCharacterCreator();
            EventBus.Unsubscribe<CharacterCreatorClosed>(OnCreatorClosed);
            _characterCreatorOpen = false;
        }
    }
}
