using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// One fuel container, as data (ADR 0003) — a specific vessel, at a specific size, carrying a
    /// specific grade. One asset per container, one stable id.
    ///
    /// <para><b>Why this is not a <c>SupplyDef</c>.</b> <see cref="HiddenHarbours.Economy"/>'s
    /// <c>SupplyDef</c> is the ECONOMIC half of a consumable — an id, a display name and a price —
    /// and its own docs say it deliberately holds nothing else. This is the PHYSICAL half: a thing
    /// that stands on a wharf, has a capacity in litres, a tare and full mass, eight or four
    /// facings of baked art, and a level you can read from outside. Putting metres and sprites on
    /// an economy Def would couple art to the market in the direction CLAUDE.md rule 4 forbids. If
    /// the economy lane later sells a can of gas over a counter, that is a <c>SupplyDef</c> beside
    /// ice exactly as its comment anticipates, and it will POINT at one of these.</para>
    ///
    /// <para>Everything here except <see cref="Id"/> and <see cref="DisplayName"/> is DERIVED from
    /// the bake contract by <c>FuelContainerDefBuilder</c>, so a re-bake at a different facing or
    /// fill count updates these assets rather than leaving them stale.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fuel Container", fileName = "FuelContainer")]
    public class FuelContainerDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only id, e.g. \"fuelstore.gas_jerry_s20\". Never reuse or rename.")]
        public string Id = "fuelstore.gas_jerry_s20";

        [Tooltip("Player-facing name. Flavour only; the id is canonical.")]
        public string DisplayName = "20 L Gas Can";

        [Header("What it is (from the rig)")]
        [Tooltip("Vessel family: jug jerry nozzle drum tote skid bulk pump.")]
        public string VesselType = "jerry";

        [Tooltip("The rig's size key, e.g. \"s20\".")]
        public string SizeKey = "s20";

        [Tooltip("Grade: gas diesel mixed oil. ⚠️ Grade is COLOUR ONLY — every grade of a vessel is " +
                 "pixel-identical in silhouette, so nothing distinguishes a gas can from a mix can " +
                 "by shape.")]
        public string Grade = "gas";

        [Tooltip("How the level reads from outside: body (translucent wall) · tube (sight glass) · " +
                 "board (float-and-tape) · none (holds nothing).")]
        public string LevelRead = "body";

        [Tooltip("True for vessels light enough to pick up. NOTHING reads this yet — carrying needs " +
                 "an interact verb that does not exist. The art is carry-ready; the wiring is not.")]
        public bool Carriable = true;

        [Header("Physical")]
        [Min(0)] public float CapacityLitres = 20f;
        [Min(0)] public float TareKg = 2.2f;
        [Min(0)] [Tooltip("Mass full of THIS grade — tare + litres × density.")]
        public float FullKg = 17.1f;

        [Header("Baked art")]
        [Tooltip("Facings baked, in clockwise order from north.")]
        public int Facings = 8;

        [Tooltip("The fill fractions baked, low to high. A vessel that holds nothing has one entry.")]
        public float[] FillFractions = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        [Tooltip("Frames, row-major: FillIndex × Facings + facing. Assigned by the builder from the " +
                 "sliced sheet, so nothing at runtime needs the AssetDatabase.")]
        public Sprite[] Frames;

        public int FillCount => FillFractions?.Length ?? 0;

        /// <summary>
        /// One baked frame, or <c>null</c> if the indices are out of range or the Def was never
        /// populated.
        ///
        /// <para>Returns null rather than throwing because a half-built Def in the inspector is a
        /// normal authoring state, and a presenter that simply draws nothing is easier to diagnose
        /// than one that spams exceptions every frame.</para>
        /// </summary>
        public Sprite Frame(int fillIndex, int facing)
        {
            if (Frames == null || Facings <= 0) return null;
            if (fillIndex < 0 || fillIndex >= FillCount) return null;
            if (facing < 0 || facing >= Facings) return null;

            int i = fillIndex * Facings + facing;
            return i >= 0 && i < Frames.Length ? Frames[i] : null;
        }

        /// <summary>True when the vessel holds a volume whose level is worth drawing.</summary>
        public bool HasLevel => LevelRead != "none" && FillCount > 1;
    }
}
