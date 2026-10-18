using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>THE CHARACTER RECIPE</b> — who the player is built as (character PR 2b, ADR 0051): one of the
    /// rig's presets, named through its <see cref="CharacterBuildDef"/>, and a value for each of the
    /// rig's twenty <c>FIELDS</c>, in the rig's own order. The save keeps one
    /// (<see cref="SaveData.PlayerRecipe"/>, v15) and <see cref="CharacterBuilder"/> turns it into a
    /// baked body and its colours, with no JS (ADR 0021).
    ///
    /// <para><b>An empty value is a real answer: "the preset's own".</b> An empty
    /// <see cref="BuildId"/> is the player's default build (<see cref="CharacterBuilderDef.PlayerDefaultBuildId"/>),
    /// and a recipe that is empty throughout is the body as it was baked — what a new game draws and
    /// what every save written before v15 drew. So there is no undrawn player and no null to guard.</para>
    ///
    /// <para><b>Presets only (the owner's ruling for rig 10).</b> A structural field may only restate
    /// its preset's own value; <see cref="CharacterBuilder"/> refuses any other, naming it. The fields
    /// are carried anyway, so the creator (PR 3) and the wardrobe can widen the offer with no second
    /// change to the save.</para>
    ///
    /// <para><b>The vest shares its bottom's colour.</b> There is one <see cref="Outfit"/> for the two,
    /// because the rig paints both from it, and the creator never offers them apart.</para>
    ///
    /// <para><b>Strings throughout, the numbers included</b>, written as the rig prints them
    /// (<c>"0.25"</c>, <c>"-1"</c>): plain for JsonUtility, and an empty string is an "unset" that a
    /// number could not say.</para>
    /// </summary>
    [Serializable]
    public sealed class CharacterRecipe
    {
        /// <summary>The recipe's own schema. Bumped when a field is added, so a recipe written by a
        /// newer game is refused rather than half-read.</summary>
        public const int Version = 1;

        /// <summary>The rig's <c>FIELDS</c>, in its order (rig 10.3). The recipe's field for each is
        /// the same name in PascalCase; the builder's contract must list exactly these.</summary>
        public static readonly string[] Fields =
        {
            "shape", "age", "height", "weight", "head", "skin", "hair", "hairStyle", "beard", "eyes",
            "eyeShape", "garment", "outfit", "shirt", "hat", "hatCol", "apronCol", "bottom", "sex", "frame",
        };

        public int SchemaVersion = Version;

        /// <summary>The <see cref="CharacterBuildDef"/> id the recipe starts from
        /// (<c>char_build.fisher</c>). Empty: the player's default build.</summary>
        public string BuildId = "";

        /// <summary>The clothing fit the recipe's clothes are cut for (<c>fit.character_v10</c>).
        /// Empty: the builder's own.</summary>
        public string FitId = "";

        public string Shape = "";
        public string Age = "";
        public string Height = "";
        public string Weight = "";
        public string Head = "";
        public string Skin = "";
        public string Hair = "";
        public string HairStyle = "";
        public string Beard = "";
        public string Eyes = "";
        public string EyeShape = "";
        public string Garment = "";
        public string Outfit = "";
        public string Shirt = "";
        public string Hat = "";
        public string HatCol = "";
        public string ApronCol = "";
        public string Bottom = "";
        public string Sex = "";
        public string Frame = "";

        /// <summary>The value of the rig field <paramref name="field"/> (one of <see cref="Fields"/>),
        /// empty for "the preset's own"; null for a name the recipe does not carry.</summary>
        public string Get(string field)
        {
            switch (field)
            {
                case "shape": return Shape ?? "";
                case "age": return Age ?? "";
                case "height": return Height ?? "";
                case "weight": return Weight ?? "";
                case "head": return Head ?? "";
                case "skin": return Skin ?? "";
                case "hair": return Hair ?? "";
                case "hairStyle": return HairStyle ?? "";
                case "beard": return Beard ?? "";
                case "eyes": return Eyes ?? "";
                case "eyeShape": return EyeShape ?? "";
                case "garment": return Garment ?? "";
                case "outfit": return Outfit ?? "";
                case "shirt": return Shirt ?? "";
                case "hat": return Hat ?? "";
                case "hatCol": return HatCol ?? "";
                case "apronCol": return ApronCol ?? "";
                case "bottom": return Bottom ?? "";
                case "sex": return Sex ?? "";
                case "frame": return Frame ?? "";
                default: return null;
            }
        }

        /// <summary>Set the rig field <paramref name="field"/>; null is stored as empty. Returns false
        /// for a name the recipe does not carry.</summary>
        public bool Set(string field, string value)
        {
            value ??= "";
            switch (field)
            {
                case "shape": Shape = value; return true;
                case "age": Age = value; return true;
                case "height": Height = value; return true;
                case "weight": Weight = value; return true;
                case "head": Head = value; return true;
                case "skin": Skin = value; return true;
                case "hair": Hair = value; return true;
                case "hairStyle": HairStyle = value; return true;
                case "beard": Beard = value; return true;
                case "eyes": Eyes = value; return true;
                case "eyeShape": EyeShape = value; return true;
                case "garment": Garment = value; return true;
                case "outfit": Outfit = value; return true;
                case "shirt": Shirt = value; return true;
                case "hat": Hat = value; return true;
                case "hatCol": HatCol = value; return true;
                case "apronCol": ApronCol = value; return true;
                case "bottom": Bottom = value; return true;
                case "sex": Sex = value; return true;
                case "frame": Frame = value; return true;
                default: return false;
            }
        }

        /// <summary>True when the recipe names nothing: no build and no field, which draws the body as
        /// baked. The fit alone changes nothing on screen, so it does not count.</summary>
        public bool IsEmpty
        {
            get
            {
                if (!string.IsNullOrEmpty(BuildId)) return false;
                foreach (string f in Fields)
                    if (!string.IsNullOrEmpty(Get(f))) return false;
                return true;
            }
        }

        /// <summary>A copy, so an event or a preview can never edit the saved one.</summary>
        public CharacterRecipe Clone() => (CharacterRecipe)MemberwiseClone();

        /// <summary>True when <paramref name="other"/> names the same build, fit and values (null and
        /// empty alike). The schema stamp is not compared: it says who wrote it, not who it draws.</summary>
        public bool SameAs(CharacterRecipe other)
        {
            if (other == null) return IsEmpty && string.IsNullOrEmpty(FitId);
            if (!string.Equals(BuildId ?? "", other.BuildId ?? "", StringComparison.Ordinal)) return false;
            if (!string.Equals(FitId ?? "", other.FitId ?? "", StringComparison.Ordinal)) return false;
            foreach (string f in Fields)
                if (!string.Equals(Get(f), other.Get(f), StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>Every null string made empty (a hand-edited save can hold one). Idempotent; never
        /// changes a value that is set.</summary>
        public void Heal()
        {
            BuildId ??= "";
            FitId ??= "";
            foreach (string f in Fields) Set(f, Get(f));
        }

        public override string ToString()
        {
            var parts = new System.Text.StringBuilder();
            parts.Append(string.IsNullOrEmpty(BuildId) ? "(default build)" : BuildId);
            foreach (string f in Fields)
            {
                string v = Get(f);
                if (v.Length > 0) parts.Append(' ').Append(f).Append('=').Append(v);
            }
            return parts.ToString();
        }
    }
}
