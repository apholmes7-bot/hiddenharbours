using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>Apply a golf build's colour fields through the shipped rig's palette resolver.</summary>
    [DisallowMultipleComponent]
    public sealed class HeronViewGolferColours : MonoBehaviour
    {
        [SerializeField] CharacterBuildDef _build;
        CharacterLook _look;
        IsoCharacterFigureRenderer _painted;
        bool _resolved;

        public void Configure(CharacterBuildDef build) { _build = build; _resolved = false; _painted = null; }

        public static bool TryLook(CharacterBuildDef build, out CharacterLook look, out string problem)
        {
            look = null;
            var catalog = CharacterBuilderDef.LoadShipped();
            var basis = catalog != null && build != null ? catalog.BuildForPreset(build.Preset) : null;
            if (basis == null) { problem = "Golf build needs a shipped preset."; return false; }
            var recipe = new CharacterRecipe {
                BuildId = basis.Id, Skin = build.Skin, Hair = build.Hair, Outfit = build.Outfit,
                Shirt = build.Shirt, HatCol = build.HatCol, ApronCol = build.ApronCol, Eyes = build.Eyes
            };
            return CharacterBuilder.TryBuild(catalog, recipe, CharacterAudience.Cast, out look, out problem);
        }

        void LateUpdate()
        {
            if (!_resolved)
            {
                _resolved = true;
                if (!TryLook(_build, out _look, out string problem)) Debug.LogError(problem, this);
            }
            if (_look == null) return;
            var presenter = GetComponent<CharacterFigurePresenter>();
            var figure = presenter != null ? presenter.AshoreFigure : null;
            if (figure == null || !figure.IsConfigured || figure == _painted) return;
            figure.Recolour(_look.Colours);
            _painted = figure;
        }
    }
}
