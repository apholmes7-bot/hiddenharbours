using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>Owns the opening's modal view, installed by the shell's runtime startup path.</summary>
    [DisallowMultipleComponent]
    public sealed class CharacterCreatorOpener : MonoBehaviour
    {
        static CharacterCreatorOpener _instance;
        CharacterCreatorModel _model;
        CharacterCreatorPanel _panel;
        ObservedAppearance _appearance;

        public static CharacterCreatorModel Model => _instance != null ? _instance._model : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            var host = new GameObject("[CharacterCreatorOpener]");
            DontDestroyOnLoad(host);
            host.AddComponent<CharacterCreatorOpener>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        void OnEnable()
        {
            EventBus.Subscribe<CharacterCreatorRequested>(OnRequested);
            EventBus.Subscribe<ShellPhaseChanged>(OnShellPhase);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<CharacterCreatorRequested>(OnRequested);
            EventBus.Unsubscribe<ShellPhaseChanged>(OnShellPhase);
            Cancel();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void OnRequested(CharacterCreatorRequested request)
        {
            if (!request.Open) { Cancel(); return; }
            if ((_model != null && _model.IsOpen) || GameServices.Save?.Current == null) return;
            _appearance = new ObservedAppearance(GameServices.Appearance);
            _model = new CharacterCreatorModel(CharacterBuilderDef.LoadShipped(),
                Resources.Load<CharacterWardrobeDef>(CharacterWardrobeDef.ResourcesPath), _appearance);
            var host = new GameObject("[CharacterCreatorPanel]");
            host.transform.SetParent(transform, false);
            _panel = host.AddComponent<CharacterCreatorPanel>();
            if (!_panel.Open(_model))
            {
                _model.Cancel();
                Destroy(host);
                _panel = null;
                Debug.LogError("The opening could not acquire the character creator's modal input gate.");
                EventBus.Publish(new CharacterCreatorClosed(false));
                return;
            }
            _model.Changed += OnChanged;
        }

        void OnShellPhase(ShellPhaseChanged change)
        {
            if (change.Phase == ShellPhase.Title) Cancel();
        }

        void Cancel()
        {
            if (_model != null && _model.IsOpen) _model.Cancel();
        }

        void OnChanged()
        {
            if (_model == null || _model.IsOpen) return;
            _model.Changed -= OnChanged;
            var panel = _panel;
            _panel = null;
            if (panel != null) Destroy(panel.gameObject);
            EventBus.Publish(new CharacterCreatorClosed(_appearance.Accepted));
        }

        // Core keeps validation, persistence and signals; this adapter records its accepted commit.
        sealed class ObservedAppearance : IPlayerAppearance
        {
            readonly IPlayerAppearance _inner;
            public bool Accepted { get; private set; }
            public CharacterRecipe Committed => _inner.Committed;
            public CharacterRecipe Showing => _inner.Showing;
            public bool IsPreviewing => _inner.IsPreviewing;

            public ObservedAppearance(IPlayerAppearance inner) { _inner = inner; }

            public bool Preview(CharacterRecipe recipe, out string refusal) => _inner.Preview(recipe, out refusal);

            public void CancelPreview()
            {
                Accepted = false;
                _inner.CancelPreview();
            }

            public bool Commit(CharacterRecipe recipe, out string refusal)
            {
                Accepted = _inner.Commit(recipe, out refusal);
                return Accepted;
            }
        }
    }
}
