using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>THE REST LOCKER</b> — where the player last went to sleep, read and written over the four
    /// <see cref="SaveData.RestRegion"/> fields as one <see cref="RestAnchor"/>. Pure, engine-light
    /// helpers in the shape <see cref="OutfitLocker"/> and <see cref="PotLocker"/> already established:
    /// a static class over the save DTO, so the whole of "turn in here, wake up there" is testable with
    /// a <c>new SaveData()</c> and no scene, no service and no bed in the world.
    ///
    /// <para><b>Why the four fields are never touched directly.</b> They only mean anything TOGETHER —
    /// a position without its region and storey is not a place (see <see cref="RestAnchor"/>) — and four
    /// public fields are four chances for a caller to write three of them. Everything goes through
    /// <see cref="Stamp"/>, which writes all four or none.</para>
    ///
    /// <para><b>It does not validate against the world.</b> Whether the region still exists, whether the
    /// position is inside it, whether that building still has an upstairs: all content questions, all
    /// answerable differently by the next build, and none of them a loader's business — the same line
    /// <see cref="OutfitLocker"/> draws with the wardrobe, and the reason <see cref="SaveMigration"/>
    /// heals only an anchor that is not a NUMBER. They are settled at the point of use, by
    /// <see cref="RestWakeRestorer"/>, where declining costs one session rather than the save.</para>
    /// </summary>
    public static class RestLocker
    {
        /// <summary>
        /// Where the player last turned in, or <see cref="RestAnchor.None"/> for a player who never has.
        /// Never throws and never returns something a caller has to null-check: an absent save, an empty
        /// region and an unreadable position all come back as <see cref="RestAnchor.None"/>, whose
        /// <see cref="RestAnchor.IsSet"/> is false.
        /// </summary>
        public static RestAnchor Anchor(SaveData save)
        {
            if (save == null || string.IsNullOrEmpty(save.RestRegion)) return RestAnchor.None;

            var anchor = new RestAnchor(save.RestRegion,
                                        new Vector2(save.RestPosX, save.RestPosY),
                                        save.RestLevel);
            return anchor.IsSet ? anchor : RestAnchor.None;
        }

        /// <summary>Where the player last turned in, off the live save service — the runtime's form.
        /// <see cref="RestAnchor.None"/> when no save service is attached (EditMode rigs, pre-boot),
        /// which reads as "never rested": the same null-safe greybox posture the sibling lockers
        /// take.</summary>
        public static RestAnchor Anchor() => Anchor(GameServices.Save?.Current);

        /// <summary>
        /// Record where the player has just turned in. Returns true iff this call CHANGED the anchor, so
        /// a caller can tell a real move from turning in twice in the same bed.
        ///
        /// <para><b>An unset anchor is REFUSED, not written.</b> Clearing the four fields would be a
        /// perfectly good way to say "forget where I slept", and nothing in the game wants to say it —
        /// whereas a caller that reached here with an empty region (no region has reported yet, a bed in
        /// a bare test scene) is one that could not answer the question, and silently erasing a good
        /// anchor because this moment could not improve on it is how a player loses their bed. Use
        /// <see cref="Forget"/> when erasure is genuinely meant.</para>
        /// </summary>
        public static bool Stamp(SaveData save, in RestAnchor anchor)
        {
            if (save == null || !anchor.IsSet) return false;

            if (string.Equals(save.RestRegion, anchor.Region, System.StringComparison.Ordinal)
                && save.RestPosX == anchor.Position.x
                && save.RestPosY == anchor.Position.y
                && save.RestLevel == anchor.Level)
                return false;

            save.RestRegion = anchor.Region;
            save.RestPosX = anchor.Position.x;
            save.RestPosY = anchor.Position.y;
            save.RestLevel = anchor.Level;
            return true;
        }

        /// <summary>
        /// Forget where the player slept — back to waking at the authored spawn. Returns true iff there
        /// was an anchor to forget.
        ///
        /// <para>Nothing in the game calls this yet, and it exists anyway because it is the ONLY correct
        /// way to erase the anchor and the alternative is a caller reaching for the fields. The obvious
        /// future caller is a bed that is sold, burned down or otherwise stops being yours.</para>
        /// </summary>
        public static bool Forget(SaveData save)
        {
            if (save == null || string.IsNullOrEmpty(save.RestRegion)) return false;

            save.RestRegion = "";
            save.RestPosX = 0f;
            save.RestPosY = 0f;
            save.RestLevel = 0;
            return true;
        }
    }
}
