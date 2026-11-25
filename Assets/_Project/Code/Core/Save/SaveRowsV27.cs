using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The keys a save names its players by</b> (save v27). Every per-player row from v27 on carries one of
    /// these, so the save can hold more than one player later by adding rows, never by moving these.
    ///
    /// <para>A key is a stable id: once a save has written it, it always means the same person in that save,
    /// and it is never given to anyone else. <see cref="Local"/> is the save's own player (the one at this
    /// machine; in a hosted game, the host). A later step that admits more players mints further keys from a
    /// saved counter (rule 5: no random ids) and never reuses one.</para>
    /// </summary>
    public static class SavePlayerKeys
    {
        /// <summary>The save's own player. The same string the wardrobe and the rod skill use for the player
        /// at this machine.</summary>
        public const string Local = "player.local";

        /// <summary>The key a row is filed under: <paramref name="playerKey"/>, or <see cref="Local"/> when it
        /// is null or empty (a save before co-op has one player, and he is the local one).</summary>
        public static string Normalise(string playerKey) =>
            string.IsNullOrEmpty(playerKey) ? Local : playerKey;
    }

    /// <summary>
    /// <b>One player's clothes, as saved</b> (save v27): the garments he owns and the ones he has on. One row
    /// per player, keyed by <see cref="PlayerKey"/>.
    ///
    /// <para><b>Ids only.</b> A garment is named by its stable Def id (<c>clothing.*</c>); what it looks like,
    /// which slot it goes in and what it does are read from that Def, never from the save, so a retuned
    /// garment follows into every save. The save cannot tell slots apart (it holds no content), so "one
    /// garment per slot" is kept by the wardrobe that writes the row; a row whose worn list breaks it is
    /// settled by the wardrobe on load (the last garment named for a slot is the one he has on).</para>
    ///
    /// <para>Read and written through <see cref="PlayerClothingRows"/>. Flat and JsonUtility-friendly.</para>
    /// </summary>
    [Serializable]
    public sealed class PlayerClothingDto
    {
        /// <summary>Whose clothes these are (<see cref="SavePlayerKeys"/>).</summary>
        public string PlayerKey = SavePlayerKeys.Local;

        /// <summary>Every garment he owns, one id each, in the order he got them. Owning a garment twice is
        /// not a thing: the list holds each id once.</summary>
        public List<string> OwnedClothing = new List<string>();

        /// <summary>The garments he has on, one id each. Every id here is also in
        /// <see cref="OwnedClothing"/> (the load repair adds one that is missing: he was wearing it, so he
        /// owns it). Empty means he wears only his own outfit colours.</summary>
        public List<string> WornClothing = new List<string>();
    }

    /// <summary>
    /// <b>Garments sold off a clothing rack, as saved</b> (save v27): one row per rack, garment and game day.
    /// A rack's stock is its Def's stock less every unit on its rows, so a garment that sold out stays sold
    /// out over a reload. Racks belong to the world, not to a player, so there is no player key here.
    ///
    /// <para>Kept in (day, rack, garment) order; rows equal in all three are folded into one with their units
    /// added. Read and written through <see cref="ClothingRackSaleRows"/>. Flat and JsonUtility-friendly.</para>
    /// </summary>
    [Serializable]
    public sealed class ClothingRackSaleDto
    {
        /// <summary>The rack's stable Def id (<c>clothing_rack.*</c>).</summary>
        public string RackId = "";

        /// <summary>The garment's stable Def id (<c>clothing.*</c>).</summary>
        public string ClothingId = "";

        /// <summary>How many were sold (at least 1 on a kept row).</summary>
        public int Units;

        /// <summary>The game day of the sale (<c>IGameClock.DayIndex</c>), kept so a later restock rule can
        /// count only recent sales without another schema change.</summary>
        public int Day;
    }

    /// <summary>
    /// <b>One player's experience in one skill, as saved</b> (save v27): the rod-fishing skill first. One row
    /// per player and skill.
    ///
    /// <para><b>Experience is the saved value; the level is not saved.</b> The level and the help it gives
    /// are worked out from experience by the skill's Def, so retuning the curve moves every save with it and
    /// nothing derived is stored (rule 5).</para>
    ///
    /// <para>Read and written through <see cref="PlayerSkillRows"/>. Flat and JsonUtility-friendly.</para>
    /// </summary>
    [Serializable]
    public sealed class PlayerSkillDto
    {
        /// <summary>Whose skill this is (<see cref="SavePlayerKeys"/>).</summary>
        public string PlayerKey = SavePlayerKeys.Local;

        /// <summary>The skill's stable Def id (for example <c>skill.rod_fishing</c>).</summary>
        public string SkillId = "";

        /// <summary>The experience earned so far, a whole number, never below zero.</summary>
        public int Experience;
    }

    /// <summary>
    /// <b>The line and reel one player has fitted to one rod, as saved</b> (save v27). One row per player and
    /// rod. <b>An empty part id means the rod's own default part</b>, so a rod never fitted has no row.
    ///
    /// <para><b>Ids only.</b> The parts' numbers, and the rod's combined stats, are read from their Defs and
    /// never saved. A part he has bought is owned in <see cref="SaveData.OwnedGear"/>, the existing presence
    /// list; this row only says which owned part is on which rod. An id whose Def is missing is kept as written
    /// (ids are append-only) and read as the default part by <see cref="RodFittingRows"/>.</para>
    ///
    /// <para>Read and written through <see cref="RodFittingRows"/>. Flat and JsonUtility-friendly.</para>
    /// </summary>
    [Serializable]
    public sealed class RodFittingDto
    {
        /// <summary>Whose fitting this is (<see cref="SavePlayerKeys"/>).</summary>
        public string PlayerKey = SavePlayerKeys.Local;

        /// <summary>The rod's stable tool id (for example <c>tool.rod_coast</c>).</summary>
        public string RodKey = "";

        /// <summary>The fitted line's Def id (<c>fishing_line.*</c>); empty for the rod's own line.</summary>
        public string LineId = "";

        /// <summary>The fitted reel's Def id (<c>reel.*</c>); empty for the rod's own reel.</summary>
        public string ReelId = "";
    }
}
