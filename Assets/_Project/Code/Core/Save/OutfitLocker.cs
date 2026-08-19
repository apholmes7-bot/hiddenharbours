namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>THE OUTFIT LOCKER</b> — what the player is wearing, read and written over
    /// <see cref="SaveData.WornOutfitId"/>. Pure, engine-free helpers in the shape
    /// <see cref="PotLocker"/> and <see cref="SupplyLocker"/> already established: a static class over
    /// the save DTO, so the whole of "get dressed" is testable with a <c>new SaveData()</c> and no
    /// scene, no service and no wardrobe in the world.
    ///
    /// <para><b>Why it lives in Core rather than with the wardrobe fixture.</b> Three lanes ask the
    /// question and none may reference the others (rule 4): the World's wardrobe sets it, the UI's
    /// picker previews it, and Art draws it. That is the same argument <see cref="HomeDeeds"/> makes,
    /// and it lands in the same place.</para>
    ///
    /// <para><b>An empty id is a real answer, not a missing one.</b> It means "as the sheets were
    /// baked" — what a new game wears, what every save written before the wardrobe existed wears, and
    /// what the fisher falls back to when an outfit cannot be resolved. So there is no such thing as an
    /// undressed player and no null to guard at the point of drawing.</para>
    ///
    /// <para><b>It does not validate.</b> Whether an id names an outfit the shipped wardrobe holds is a
    /// question for <see cref="CharacterWardrobeDef"/> at the point of USE, where a failure can fall
    /// back for one session and say so out loud. Deciding it here would mean a save silently losing the
    /// player's choice the first time an asset failed to load — see the v11→v12 note in
    /// <see cref="SaveMigration"/>.</para>
    /// </summary>
    public static class OutfitLocker
    {
        /// <summary>
        /// The outfit id the player is wearing, or empty for the baked look. Never null, so a caller
        /// can compare it without guarding.
        /// </summary>
        public static string WornOutfitId(SaveData save) =>
            string.IsNullOrEmpty(save?.WornOutfitId) ? string.Empty : save.WornOutfitId;

        /// <summary>
        /// Put an outfit on. A null id is normalised to empty (the baked look), which is how a wardrobe
        /// offers "what I came in".
        ///
        /// <para>Returns true iff this call CHANGED what the player is wearing, so a caller can tell a
        /// real change from re-picking the outfit already on — and only publish, only re-skin and only
        /// write the file when something actually moved.</para>
        /// </summary>
        public static bool Wear(SaveData save, string outfitId)
        {
            if (save == null) return false;

            string next = string.IsNullOrEmpty(outfitId) ? string.Empty : outfitId;
            if (string.Equals(WornOutfitId(save), next, System.StringComparison.Ordinal)) return false;

            save.WornOutfitId = next;
            return true;
        }

        /// <summary>Read what the player is wearing off the live save service — the runtime's form.
        /// Empty when no save service is attached (EditMode rigs, pre-boot), which reads as the baked
        /// look: the same null-safe greybox posture <see cref="HomeDeeds"/> takes.</summary>
        public static string WornOutfitId() => WornOutfitId(GameServices.Save?.Current);

        /// <summary>
        /// Put an outfit on the live save, persist it, and announce it. Returns true iff something
        /// changed.
        ///
        /// <para>The <see cref="OutfitChanged"/> publish is what re-skins the fisher: Core states that
        /// the player is now wearing something else, and whoever is DRAWING them answers. The UI never
        /// touches a material and the Art lane never reads the save — the seam rule 4 asks for, and the
        /// same shape <c>ActiveBoatChanged</c> already uses to move a hull's paint.</para>
        /// </summary>
        public static bool Wear(string outfitId)
        {
            var save = GameServices.Save;
            if (save?.Current == null) return false;
            if (!Wear(save.Current, outfitId)) return false;

            save.Save();
            EventBus.Publish(new OutfitChanged(WornOutfitId(save.Current)));
            return true;
        }
    }
}
