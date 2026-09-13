using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Shared authoring interchange for the wardrobe workbench and Unity Defs. Version 1 is a
    /// clothing contract, NOT SaveData's schema version. IDs and field names survive JSON export.
    /// Geometry is baked by editor tools; none of these descriptors runs a rig in a player build.
    /// </summary>
    public static class CharacterClothingSchema
    {
        public const int Version = 1;

        public static bool IsSlot(string value) => value == "headwear" || value == "top" ||
            value == "bottom" || value == "outerwear" || value == "footwear" || value == "accessory";
    }

    [Serializable]
    public sealed class CharacterClothingFitSpec
    {
        public int SchemaVersion = CharacterClothingSchema.Version;
        public string Id = "";
        public string SourceRigSha256 = "";
        public string SkeletonId = "";
        public string[] BoneIds = Array.Empty<string>();
        public string[] SourceSectionIds = Array.Empty<string>();
        public string[] CoverageTags = Array.Empty<string>();
        // Required where the base has no uncovered geometry; optional accessories remain removable.
        public string[] RequiredSlots = Array.Empty<string>();
    }

    [Serializable]
    public sealed class CharacterGarmentFitSpec
    {
        public string FitId = "";
        public string[] SourceSectionIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class CharacterGarmentMaterialRole
    {
        public string SourceMaterial = "";
        public string Role = "";
        public string RampId = "";
    }

    [Serializable]
    public sealed class CharacterGarmentColourway
    {
        public string Id = "";
        public string DisplayName = "";
        // Unmapped source materials retain their authored colour and shading metadata.
        // An empty array is a valid unchanged colourway, not permission to omit the garment.
        public CharacterGarmentMaterialRole[] MaterialRoles = Array.Empty<CharacterGarmentMaterialRole>();
    }

    [Serializable]
    public sealed class CharacterGarmentSpec
    {
        public int SchemaVersion = CharacterClothingSchema.Version;
        public string Id = "";
        public string DisplayName = "";
        public string Category = "";
        public string[] OccupiedSlots = Array.Empty<string>();
        // Tags identify exact authored surfaces (e.g. forearms), never an inferred whole body region.
        public string[] CoverageTags = Array.Empty<string>();
        public string[] CompatibilityTags = Array.Empty<string>();
        public string[] IncompatibleTags = Array.Empty<string>();
        public CharacterGarmentFitSpec[] Fits = Array.Empty<CharacterGarmentFitSpec>();
        public CharacterGarmentColourway[] Colourways = Array.Empty<CharacterGarmentColourway>();
        public int Price;
        public string[] SellerIds = Array.Empty<string>();
        public bool StarterGrant;
    }

    /// <summary>A transport snapshot; authors still store each entity in its own Def/JSON file.</summary>
    [Serializable]
    public sealed class CharacterClothingCatalogue
    {
        public int SchemaVersion = CharacterClothingSchema.Version;
        public CharacterClothingFitSpec[] Fits = Array.Empty<CharacterClothingFitSpec>();
        public CharacterGarmentSpec[] Garments = Array.Empty<CharacterGarmentSpec>();
    }
}
