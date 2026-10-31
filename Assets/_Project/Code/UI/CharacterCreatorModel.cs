using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>Choices and input state only. The live player is the preview; Core owns every write.</summary>
    public sealed class CharacterCreatorModel
    {
        private readonly CharacterBuilderDef _builder;
        private readonly CharacterWardrobeDef _wardrobe;
        private readonly IPlayerAppearance _appearance;
        private readonly CharacterRecipe _openingRecipe;
        private readonly string _openingOutfit;
        private readonly string _openingName;
        private readonly List<string> _bodies = new List<string>();
        private readonly List<string> _unavailable = new List<string>();
        private readonly List<string> _outfits = new List<string> { string.Empty };
        private readonly List<string> _colourFields = new List<string>();
        private CharacterRecipe _recipe;
        private string _outfit;
        private string _name;
        private bool _axisHeld;
        private bool _awaitRelease = true;
        private bool _upperCase;
        private const string LettersAndDigits = "abcdefghijklmnopqrstuvwxyz0123456789";
        public const int KeyboardColumns = 10;
        public const int KeyboardKeyCount = 40;

        public CharacterCreatorModel(CharacterBuilderDef builder, CharacterWardrobeDef wardrobe,
            IPlayerAppearance appearance = null)
        {
            _builder = builder;
            _wardrobe = wardrobe;
            _appearance = appearance ?? GameServices.Appearance;
            _openingRecipe = _appearance.Committed.Clone();
            _openingOutfit = OutfitLocker.WornOutfitId();
            _openingName = PlayerName.Of(GameServices.Save?.Current);
            _recipe = _openingRecipe.Clone();
            _outfit = _openingOutfit;
            _name = _openingName;
            if (builder != null && builder.Builds != null)
                foreach (CharacterBuildDef build in builder.Builds)
                {
                    if (build == null) continue;
                    var recipe = new CharacterRecipe { BuildId = build.Id };
                    if (CharacterBuilder.TryBuild(builder, recipe, CharacterAudience.Player, out _, out string why))
                        _bodies.Add(build.Id);
                    else _unavailable.Add(why);
                }
            ReadColourFields();
            if (wardrobe != null && wardrobe.Outfits != null)
                foreach (CharacterOutfitDef outfit in wardrobe.Outfits)
                    if (outfit != null && !string.IsNullOrEmpty(outfit.Id) && !_outfits.Contains(outfit.Id))
                        _outfits.Add(outfit.Id);
            // A previously worn id remains the starting choice even if its content is unavailable.
            if (!_outfits.Contains(_outfit)) _outfits.Add(_outfit);
            if (_bodies.Count == 0) Refusal = _unavailable.Count > 0
                ? string.Join("\n", _unavailable) : CharacterCreatorStrings.NoBodies;
        }

        public event Action Changed;
        public IReadOnlyList<string> Bodies => _bodies.AsReadOnly();
        public IReadOnlyList<string> UnavailableBodies => _unavailable.AsReadOnly();
        public IReadOnlyList<string> ColourFields => _colourFields.AsReadOnly();
        public IReadOnlyList<string> Outfits => _outfits.AsReadOnly();
        public CharacterRecipe Recipe => _recipe.Clone();
        public string OutfitId => _outfit;
        public string Name => _name;
        public string Refusal { get; private set; }
        public bool IsOpen { get; private set; } = true;
        public bool NameEditing { get; private set; }
        public int FocusRow { get; private set; }
        public int KeyboardIndex { get; private set; }
        public int OutfitRow => _colourFields.Count + 1;
        public int NameRow => OutfitRow + 1;
        public int ResetRow => NameRow + 1;
        public int CancelRow => ResetRow + 1;
        public int ConfirmRow => CancelRow + 1;
        public int RowCount => ConfirmRow + 1;
        public int ColourSlotCount => CharacterBuilder.TryBuild(_builder, _recipe,
            CharacterAudience.Player, out CharacterLook look, out _) ? look.Colours.Length : 0;

        public bool PreviewRecipe(CharacterRecipe candidate)
        {
            if (!IsOpen) return false;
            if (!_appearance.Preview(candidate, out string refusal)) return Refuse(refusal);
            _recipe = candidate.Clone();
            ReadColourFields();
            FocusRow = Math.Min(FocusRow, ConfirmRow);
            Refusal = null;
            Notify();
            return true;
        }

        public bool SelectBody(string id) => PreviewRecipe(new CharacterRecipe { BuildId = id });

        public IReadOnlyList<string> ColourOptions(string field)
        {
            var options = new List<string>();
            CharacterField spec = _builder != null && _builder.Contract != null
                ? _builder.Contract.Field(field) : null;
            if (spec == null || spec.Kind != CharacterFieldKind.Colour || spec.Options == null) return options;
            foreach (string value in spec.Options)
            {
                CharacterRecipe trial = _recipe.Clone();
                trial.Set(field, value);
                if (CharacterBuilder.TryBuild(_builder, trial, CharacterAudience.Player, out _, out _)) options.Add(value);
            }
            return options;
        }

        public bool SelectColour(string field, string value)
        {
            if (!_colourFields.Contains(field)) return Refuse(CharacterCreatorStrings.UnknownColour);
            CharacterRecipe candidate = _recipe.Clone();
            candidate.Set(field, value);
            return PreviewRecipe(candidate);
        }

        public bool SelectOutfit(string id)
        {
            if (!IsOpen) return false;
            if (!_outfits.Contains(id ?? string.Empty)) return Refuse(CharacterCreatorStrings.UnknownOutfit);
            _outfit = id ?? string.Empty;
            EventBus.Publish(new OutfitPreviewRequested(_outfit));
            Refusal = null;
            Notify();
            return true;
        }

        public bool SetName(string candidate)
        {
            if (!IsOpen) return false;
            string why = PlayerName.Check(candidate);
            if (why != null) return Refuse(why);
            _name = candidate ?? string.Empty;
            Refusal = null;
            Notify();
            return true;
        }

        public bool Commit()
        {
            if (!IsOpen || _bodies.Count == 0) return false;
            string why = PlayerName.Check(_name);
            if (why != null) return Refuse(why);
            ISaveService save = GameServices.Save;
            if (save?.Current == null) return Refuse(CharacterCreatorStrings.NoSave);
            // Name validation precedes every mutation. Appearance is the only remaining refusal.
            using (save.DeferWrites())
            {
                if (!_appearance.Commit(_recipe, out why)) return Refuse(why);
                OutfitLocker.Wear(_outfit);
                PlayerName.Set(_name, out _);
            }
            IsOpen = false;
            NameEditing = false;
            Refusal = null;
            Notify();
            return true;
        }

        public void Reset()
        {
            if (!IsOpen) return;
            if (!PreviewRecipe(_openingRecipe)) return;
            SelectOutfit(_openingOutfit);
            _name = _openingName;
            NameEditing = false;
            Notify();
        }

        public void Cancel()
        {
            if (!IsOpen) return;
            _appearance.CancelPreview();
            EventBus.Publish(new OutfitPreviewRequested(_openingOutfit));
            _recipe = _openingRecipe.Clone();
            _outfit = _openingOutfit;
            _name = _openingName;
            IsOpen = false;
            NameEditing = false;
            Refusal = null;
            Notify();
        }

        public void Focus(int row)
        {
            if (!IsOpen || NameEditing || row < 0 || row >= RowCount || FocusRow == row) return;
            FocusRow = row;
            Notify();
        }

        public void Adjust(int direction)
        {
            if (!IsOpen || NameEditing) return;
            if (FocusRow == 0)
            {
                string current = string.IsNullOrEmpty(_recipe.BuildId) ? _builder.PlayerDefaultBuildId : _recipe.BuildId;
                if (_bodies.Count > 0) SelectBody(_bodies[Wrap(_bodies.IndexOf(current) + direction, _bodies.Count)]);
            }
            else if (FocusRow < OutfitRow)
            {
                string field = _colourFields[FocusRow - 1];
                IReadOnlyList<string> options = ColourOptions(field);
                if (options.Count == 0) return;
                CharacterBuilder.TryBuild(_builder, _recipe, CharacterAudience.Player, out CharacterLook look, out _);
                int fieldIndex = _builder.Contract.FieldIndex(field);
                string current = look != null ? look.Values[fieldIndex] : _recipe.Get(field);
                int index = 0;
                for (int i = 0; i < options.Count; i++) if (options[i] == current) index = i;
                SelectColour(field, options[Wrap(index + direction, options.Count)]);
            }
            else if (FocusRow == OutfitRow)
                SelectOutfit(_outfits[Wrap(_outfits.IndexOf(_outfit) + direction, _outfits.Count)]);
        }

        public void Activate()
        {
            if (!IsOpen) return;
            if (NameEditing) { PressKey(KeyboardIndex); return; }
            if (FocusRow <= OutfitRow) Adjust(1);
            else if (FocusRow == NameRow) { NameEditing = true; Notify(); }
            else if (FocusRow == ResetRow) Reset();
            else if (FocusRow == CancelRow) Cancel();
            else if (FocusRow == ConfirmRow) Commit();
        }

        public void Back()
        {
            if (NameEditing) { NameEditing = false; Notify(); }
            else Cancel();
        }

        public void Type(char character)
        {
            if (NameEditing && !char.IsControl(character)) SetName(_name + character);
        }

        public void Backspace()
        {
            if (!NameEditing || _name.Length == 0) return;
            int remove = _name.Length > 1 && char.IsLowSurrogate(_name[_name.Length - 1])
                && char.IsHighSurrogate(_name[_name.Length - 2]) ? 2 : 1;
            SetName(_name.Substring(0, _name.Length - remove));
        }

        public string KeyLabel(int key) => key < LettersAndDigits.Length
            ? (_upperCase ? char.ToUpperInvariant(LettersAndDigits[key]) : LettersAndDigits[key]).ToString()
            : CharacterCreatorStrings.KeyActions[key - LettersAndDigits.Length];

        public void PressKey(int key)
        {
            if (!IsOpen || !NameEditing || key < 0 || key >= KeyboardKeyCount) return;
            KeyboardIndex = key;
            if (key < LettersAndDigits.Length) Type(KeyLabel(key)[0]);
            else if (key == LettersAndDigits.Length) Type(' ');
            else if (key == LettersAndDigits.Length + 1) Backspace();
            else if (key == LettersAndDigits.Length + 2) { _upperCase = !_upperCase; Notify(); }
            else { NameEditing = false; Notify(); }
        }

        public void Input(float x, float y, bool confirm, bool cancel)
        {
            bool pushed = Math.Abs(x) >= WardrobePickerModel.AxisThreshold || Math.Abs(y) >= WardrobePickerModel.AxisThreshold;
            if (_awaitRelease)
            {
                if (!pushed && !confirm && !cancel) _awaitRelease = false;
                return;
            }
            if (cancel) { Back(); return; }
            if (confirm) { Activate(); return; }
            if (!pushed) { _axisHeld = false; return; }
            if (_axisHeld) return;
            _axisHeld = true;
            if (NameEditing)
            {
                int step = Math.Abs(y) > Math.Abs(x) ? (y > 0 ? -KeyboardColumns : KeyboardColumns) : (x > 0 ? 1 : -1);
                KeyboardIndex = Wrap(KeyboardIndex + step, KeyboardKeyCount);
                Notify();
            }
            else if (Math.Abs(y) > Math.Abs(x)) Focus(Wrap(FocusRow + (y > 0 ? -1 : 1), RowCount));
            else Adjust(x > 0 ? 1 : -1);
        }

        public string RowLabel(int row)
        {
            if (row == 0)
            {
                CharacterBuildDef build = _builder != null
                    ? _builder.Build(string.IsNullOrEmpty(_recipe.BuildId) ? _builder.PlayerDefaultBuildId : _recipe.BuildId) : null;
                return CharacterCreatorStrings.Body + (build != null ? build.Preset : _recipe.BuildId);
            }
            if (row < OutfitRow)
            {
                string field = _colourFields[row - 1];
                CharacterBuilder.TryBuild(_builder, _recipe, CharacterAudience.Player, out CharacterLook look, out _);
                string value = look != null ? look.Values[_builder.Contract.FieldIndex(field)] : _recipe.Get(field);
                return field + ": " + value;
            }
            if (row == OutfitRow)
            {
                CharacterOutfitDef outfit = _wardrobe != null ? _wardrobe.Outfit(_outfit) : null;
                return CharacterCreatorStrings.Outfit + (outfit != null ? outfit.Label
                    : string.IsNullOrEmpty(_outfit) ? CharacterCreatorStrings.AsBaked : _outfit);
            }
            if (row == NameRow) return CharacterCreatorStrings.Name + (string.IsNullOrWhiteSpace(_name) ? CharacterCreatorStrings.Optional : _name);
            if (row == ResetRow) return CharacterCreatorStrings.Reset;
            if (row == CancelRow) return CharacterCreatorStrings.Cancel;
            return CharacterCreatorStrings.Confirm;
        }

        private bool Refuse(string reason) { Refusal = reason; Notify(); return false; }
        private void Notify() => Changed?.Invoke();
        private static int Wrap(int value, int count) => (value % count + count) % count;

        private void ReadColourFields()
        {
            _colourFields.Clear();
            if (!CharacterBuilder.TryBuild(_builder, _recipe, CharacterAudience.Player, out CharacterLook look, out _)) return;
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterSkinDef.Material material in look.Skin.Materials)
            {
                CharacterPaintRule rule = _builder.Contract.Rule(material.Name);
                if (rule == null) continue;
                if (rule.Source == CharacterPaintSource.Axis) used.Add(rule.Field);
                if (rule.Source == CharacterPaintSource.Table && rule.TableFields != null)
                    foreach (string field in rule.TableFields) used.Add(field);
            }
            foreach (CharacterField field in _builder.Contract.Fields)
                if (field != null && field.Kind == CharacterFieldKind.Colour && used.Contains(field.Name))
                    _colourFields.Add(field.Name);
        }
    }

    // Player-facing copy stays together, following WardrobeStrings.
    internal static class CharacterCreatorStrings
    {
        public const string Title = "Make yourself at home";
        public const string Body = "Body: ";
        public const string Outfit = "Starter outfit: ";
        public const string Name = "Name: ";
        public const string Optional = "optional";
        public const string AsBaked = "What I came in";
        public const string Reset = "Reset to opening choices";
        public const string Cancel = "Leave my choices as they were";
        public const string Confirm = "Keep these choices";
        public const string Hint = "Arrows / stick choose and change. Enter / A select. Esc / B back.";
        public const string NameHint = "Type a name, or choose keys. Enter / Start finishes. Esc / B back.";
        public const string NoBodies = "The builder has no available player body.";
        public const string UnknownColour = "That colour field is not offered by this body.";
        public const string UnknownOutfit = "That starter outfit is not in the wardrobe.";
        public const string NoSave = "No game is loaded, so there is nowhere to keep these choices.";
        public static readonly string[] KeyActions = { "Space", "Back", "A/a", "Done" };
    }
}
