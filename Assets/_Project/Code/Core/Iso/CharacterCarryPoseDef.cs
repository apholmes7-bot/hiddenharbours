using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Which of the rig's carry stances a held thing asks for</b> — one row per carriable kind that
    /// has a rig pose, the whole table as one asset.
    ///
    /// <para><b>Why a table and not a switch.</b> The rig's <c>pose()</c> takes a carry stance
    /// (<c>buckets</c>, <c>tray</c>, <c>pot</c>, <c>helm</c>, <c>oars</c>), and a bucket walk braces the
    /// arms and leans differently from a free walk; the skinned figure plays the rig's clip for it
    /// (<see cref="CharacterSkinStateMap.CarryKey"/>). Which game carriable maps to which stance is
    /// content (rule 2): a new carriable with a rig pose is a new row here, never a code edit.</para>
    ///
    /// <para><b>What is NOT here, and why.</b> The rig's <c>tray</c> and <c>pot</c> stances have no
    /// carriable in the game yet (the fish tray is a deck container, <c>DeckContainerDef</c>, and a
    /// lobster pot is not carried by hand), the jerry can is
    /// a fuel container without a rig stance, and a tool or a catch is a HAND PROP held by
    /// <see cref="CarryAnchorTableDef"/>'s rows, which are rig 6's hand anchors and a different table.
    /// A carriable with no row plays the free clip.</para>
    ///
    /// <para><b>Helm and oars are stances, not carries</b> — <see cref="CharacterStance.Helm"/> and
    /// <see cref="CharacterStance.Oars"/> reach them through the state map — so they have no row.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character Carry Poses", fileName = "CharacterCarryPoses")]
    public class CharacterCarryPoseDef : ScriptableObject
    {
        /// <summary>Where the shipped table lives for a runtime load, following the library
        /// convention (<see cref="CharacterWardrobeDef.ResourcesPath"/>): one asset under
        /// <c>Assets/_Project/Resources</c>, named by the type that reads it.</summary>
        public const string ResourcesPath = "CharacterCarryPoses";

        /// <summary>One carriable kind and the rig carry stance it asks for.</summary>
        [Serializable]
        public struct Row
        {
            [Tooltip("The carriable's DefId — the KIND, shared by every instance " +
                     "(container.bucket for every pail, the clam hod included).")]
            public string CarriableDefId;

            [Tooltip("The rig's carry stance ('buckets', 'tray', 'pot'), exactly as a skin clip's " +
                     "Carry field spells it.")]
            public string Carry;
        }

        [Header("Identity")]
        [Tooltip("Stable id, append-only.")]
        public string Id = "carrypose.character";

        [Header("The rows")]
        [Tooltip("One row per carriable kind with a rig carry stance. A kind with no row plays the " +
                 "free clip.")]
        public Row[] Rows = Array.Empty<Row>();

        /// <summary>The carry stance for a carriable kind, or null. Ordinal; allocation-free.</summary>
        public string CarryFor(string carriableDefId)
        {
            if (Rows == null || string.IsNullOrEmpty(carriableDefId)) return null;
            foreach (Row r in Rows)
                if (string.Equals(r.CarriableDefId, carriableDefId, StringComparison.Ordinal))
                    return string.IsNullOrEmpty(r.Carry) ? null : r.Carry;
            return null;
        }

        /// <summary>
        /// The carry stance for whatever these hands hold: the first held thing (either hand) whose
        /// kind has a row. One pail asks for the rig's two-pail pose, which is the only bucket pose
        /// the rig has. Allocation-free.
        /// </summary>
        public string CarryFor(ICarrier hands)
        {
            CarriedItem.Both(hands, out ICarriable first, out ICarriable second);
            string carry = first != null ? CarryFor(first.DefId) : null;
            if (carry == null && second != null) carry = CarryFor(second.DefId);
            return carry;
        }
    }
}
