using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// A person is separate from their equipment. These discrete keys describe supported baked
    /// identity choices; the workbench must not offer a body/face control without fitted geometry.
    /// Additional structural identity choices follow the same explicit fit contract when baked.
    /// </summary>
    [Serializable]
    public sealed class CharacterIdentityRecipe
    {
        public string BuildId = "";
        public string FitId = "";
        public string Skin = "";
        public string Hair = "";
        public string Eyes = "";
    }

    [Serializable]
    public sealed class CharacterGarmentSelection
    {
        public string GarmentId = "";
        public string ColourwayId = "";
    }

    /// <summary>
    /// An appearance intent, usable for a preview or a saved outfit. It does not imply ownership,
    /// charge a wallet, mutate a Def, or commit a save. The owning service must authorize a commit.
    /// </summary>
    [Serializable]
    public sealed class CharacterAppearanceRecipe
    {
        public int SchemaVersion = CharacterClothingSchema.Version;
        public CharacterIdentityRecipe Identity = new CharacterIdentityRecipe();
        public CharacterGarmentSelection[] Garments = Array.Empty<CharacterGarmentSelection>();
    }
}
