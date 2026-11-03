using HiddenHarbours.Core;
using HiddenHarbours.UI;
using UnityEngine;

namespace HiddenHarbours.App
{
    public sealed partial class ArrivalOpening
    {
        static CharacterCreatorModel _characterCreator;
        static CharacterCreatorPanel _characterCreatorPanel;

        /// <summary>The opening's choices, carried across region hops with the persistent player.</summary>
        public static CharacterCreatorModel CharacterCreator => _characterCreator;

        void OpenCharacterCreator()
        {
            if (_characterCreator != null && _characterCreator.IsOpen) return;
            if (GameServices.Save?.Current == null) return;
            _characterCreator = new CharacterCreatorModel(CharacterBuilderDef.LoadShipped(),
                Resources.Load<CharacterWardrobeDef>(CharacterWardrobeDef.ResourcesPath));
            var host = new GameObject("[ArrivalCharacterCreator]");
            DontDestroyOnLoad(host);
            _characterCreatorPanel = host.AddComponent<CharacterCreatorPanel>();
            if (!_characterCreatorPanel.Open(_characterCreator))
            {
                _characterCreator.Cancel();
                Destroy(host);
                _characterCreatorPanel = null;
                Debug.LogError("The opening could not acquire the character creator's modal input gate.");
                return;
            }
            _characterCreator.Changed += OnCreatorChanged;
            EventBus.Subscribe<ShellPhaseChanged>(OnCreatorShellPhase);
        }

        static void OnCreatorChanged()
        {
            if (_characterCreator == null || _characterCreator.IsOpen) return;
            _characterCreator.Changed -= OnCreatorChanged;
            EventBus.Unsubscribe<ShellPhaseChanged>(OnCreatorShellPhase);
            var panel = _characterCreatorPanel;
            _characterCreatorPanel = null;
            if (panel != null) Destroy(panel.gameObject);
        }

        static void OnCreatorShellPhase(ShellPhaseChanged change)
        {
            if (change.Phase == ShellPhase.Title) CloseCharacterCreator();
        }

        static void CloseCharacterCreator()
        {
            if (_characterCreator != null && _characterCreator.IsOpen) _characterCreator.Cancel();
        }
    }
}
