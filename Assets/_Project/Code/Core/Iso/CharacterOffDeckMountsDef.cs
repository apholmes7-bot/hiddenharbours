using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One character build's WATER LINE, for the two anims that put them in it.
    ///
    /// <para><b>The two fields are the same point said two ways, and a consumer wants one or the
    /// other — never both.</b> <see cref="WaterZ"/> is the height up the BODY in world metres, so it
    /// scales with the build: it is what places the line when the character is posed in world space.
    /// <see cref="WaterRowLane"/> is that same line as a row of the 88-px art cell, which is what a
    /// shader or a clip rect wants. Deriving one from the other needs the sheet's PPU and the cell's
    /// pivot, which is exactly the conversion the importer already did — so read the field you need
    /// and do not re-derive its twin.</para>
    /// </summary>
    [Serializable]
    public struct CharacterWaterLine
    {
        [Tooltip("Height up the BODY in world metres — scales with the build. Fisher swims at 0.4488, " +
                 "treads at 1.0047.")]
        public float WaterZ;

        [Tooltip("The same line as a LANE ROW of the 88-px off-deck cell, counted from the cell top. " +
                 "⚠️ An 88-cell row: these sheets are the rig's 92 cell re-windowed 2 top / 2 bottom, " +
                 "and a row index read against 92 is wrong by two.")]
        public int WaterRowLane;
    }

    /// <summary>One preset's water lines, keyed by the visual def it belongs to.</summary>
    [Serializable]
    public struct CharacterOffDeckRow
    {
        [Tooltip("The CharacterVisualDef id this row describes — visual.fisher_iso, visual.ginny_iso, …")]
        public string VisualId;

        [Tooltip("Where the sea cuts the body while SWIMMING (prone, 8 frames).")]
        public CharacterWaterLine Swim;

        [Tooltip("Where the sea cuts the body while TREADING (upright, 6 frames) — higher up the body " +
                 "than swimming, because treading stands the character up in the water.")]
        public CharacterWaterLine Tread;
    }

    /// <summary>
    /// <b>The rig-6.5 OFF-DECK mount contract, imported once into an asset</b> — the consumer side of
    /// <c>OffDeck_mounts.json</c>, which the character rig exports beside the swim / tread / sleep /
    /// drive sheets.
    ///
    /// <para><b>Why a def and not constants.</b> These are the numbers that place a character who is
    /// not standing on the ground: where the sea cuts them, how high the mattress is, where the seat
    /// and wheel of a vehicle are. They are art-contract geometry — they change when the rig re-bakes
    /// and at no other time — so they are DATA (CLAUDE.md rule 2 and rule 6), imported at edit time
    /// into a committed asset. Runtime never parses the JSON (ADR 0021 §4), the same arrangement as
    /// <see cref="CarryAnchorTableDef"/>.</para>
    ///
    /// <para><b>Why they are NOT on <see cref="CharacterVisualDef"/>.</b> Only the water lines are
    /// per-character; the bed and the driving seat belong to the FURNITURE and the MACHINE, and the
    /// sidecar states them once for everyone. Copying a global onto ten per-character assets is how
    /// two of them quietly disagree later. So the per-preset half lives in <see cref="Rows"/> and the
    /// global half in the single fields below, mirroring the contract file exactly — one number, one
    /// home.</para>
    ///
    /// <para><b>What is NOT here: the ART.</b> Sheets, frame counts and playback rates for the same
    /// four anims ride on <see cref="CharacterVisualDef"/> as ordinary
    /// <see cref="CharacterClip"/>s, behind the same all-or-nothing gate as every other clip. This
    /// asset is geometry only, and it stays useful even for a preset whose sheets have not baked.</para>
    ///
    /// <para><b>The sheets are baked DRY</b> — the sidecar's own <c>_bake</c> note. There is no water
    /// pixel in the art; a consumer pins the line from <see cref="Rows"/> and lets the submersion
    /// shader tint and clip below it.</para>
    ///
    /// Written by <c>CharacterOffDeckMountsBuilder</c>; do not hand-edit.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character Off-Deck Mounts",
                     fileName = "CharacterOffDeckMounts")]
    public class CharacterOffDeckMountsDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5).")]
        public string Id = "offdeckmounts.character_iso";

        [Tooltip("The rig revision these numbers were exported from, as the sidecar states it. " +
                 "Provenance, not a switch — nothing branches on it.")]
        public string Rig = "";

        [Header("Per-character water lines")]
        [Tooltip("One row per cast preset. A character with no row simply has no measured water line " +
                 "and the caller keeps whatever it did before — nothing here throws on a miss.")]
        public CharacterOffDeckRow[] Rows = Array.Empty<CharacterOffDeckRow>();

        [Header("Sleeping — global, one mattress for everyone")]
        [Tooltip("Mattress height in world metres. The sleep sheets are baked LYING on a bed this " +
                 "tall, so a bed of another height needs the sprite lifted by the difference, not a " +
                 "re-bake.")]
        public float SleepBedZ = 0.30f;

        [Header("Driving — global, world metres on the MACHINE")]
        [Tooltip("Seat height. ⚠️ World metres on the vehicle, NOT a body fraction — a seat belongs " +
                 "to the machine, so this never scales with the build.")]
        public float DriveSeatZ = 0.40f;

        [Tooltip("Wheel height above the vehicle floor, world metres.")]
        public float DriveWheelZ = 0.655f;

        [Tooltip("Wheel distance forward of the seat, world metres.")]
        public float DriveWheelY = 0.315f;

        /// <summary>
        /// The water lines for a visual def id, or <c>false</c> with an empty row when this asset
        /// carries none. Never throws and never invents a line: a caller that gets <c>false</c> keeps
        /// whatever placement it already had, which is always a legal picture — the same degrade-per-
        /// element rule the rest of the character data follows.
        /// </summary>
        public bool TryGetRow(string visualId, out CharacterOffDeckRow row)
        {
            row = default;
            if (string.IsNullOrEmpty(visualId) || Rows == null) return false;

            for (int i = 0; i < Rows.Length; i++)
            {
                if (string.Equals(Rows[i].VisualId, visualId, StringComparison.Ordinal))
                {
                    row = Rows[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The water line for a visual def id and one of the two WATER anims
        /// (<see cref="CharacterClip.Swim"/> / <see cref="CharacterClip.Tread"/>). Any other clip has
        /// no water line at all and returns <c>false</c> — sleep and drive are pinned to a bed and a
        /// seat, not to the sea.
        /// </summary>
        public bool TryGetWaterLine(string visualId, CharacterClip clip, out CharacterWaterLine line)
        {
            line = default;
            if (clip != CharacterClip.Swim && clip != CharacterClip.Tread) return false;
            if (!TryGetRow(visualId, out var row)) return false;

            line = clip == CharacterClip.Swim ? row.Swim : row.Tread;
            return true;
        }
    }
}
