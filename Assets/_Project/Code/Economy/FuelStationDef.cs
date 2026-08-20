using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// What ONE grade costs at ONE site, and whether it is sold there at all.
    ///
    /// <para>Availability and price are separate fields on purpose. "Sold here, at this price" and "not
    /// sold here" are different facts, and a sentinel price meaning "unavailable" would make every reader
    /// re-derive the rule — the same reason <c>FuelContainerDef</c> keeps <c>Carriable</c> as its own bool
    /// rather than inferring it from mass. Turning a grade off for a season is then a checkbox the owner
    /// ticks, and the price he tuned is still sitting there when he ticks it back on.</para>
    /// </summary>
    [Serializable]
    public struct FuelGradeOffer
    {
        [Tooltip("Which fuel — one of FuelGrades (gas · diesel · mixed · oil · stove_oil). A grade outside " +
                 "that set is an authoring error; the content-validation test fails on it.")]
        public string Grade;

        [Tooltip("Sold at this site at all. Unticked, the pump says so rather than charging for nothing.")]
        public bool Available;

        [Min(0f)]
        [Tooltip("₲ per LITRE. The owner-tunable that makes geography cost something: the wharf pumps are " +
                 "dearer than the road station, so lying alongside is convenience you pay for. Fractional " +
                 "on purpose — the CHARGE is rounded to whole ₲ once, at the till (FuelPricing).")]
        public float PricePerLitre;
    }

    /// <summary>
    /// <b>A place that sells fuel</b>, as data (ADR 0003) — one asset per SITE, one stable id, and a price
    /// per grade. Nine Mile Creek's wharf pumps and the Route 91 station are two assets, not two code
    /// paths, and the owner re-prices the island by editing them (rule 6).
    ///
    /// <para><b>Why a site and not a vendor component.</b> A site's prices are one fact even when the site
    /// has several hoses: the twin pump on the wharf sells gas from one and diesel from the other at the
    /// same posted prices, and a scene that wired a price per hose could post two different numbers for
    /// the same fuel ten feet apart. So the Def is the SITE, each <see cref="FuelPump"/> in the scene
    /// points at it and names which grade it dispenses, and the posted price has exactly one author.</para>
    ///
    /// <para><b>The economic half only</b> — the sibling ruling <c>FuelContainerDef</c> made and this
    /// honours. That Def is the PHYSICAL half of a fuel vessel (litres, mass, baked facings); this is
    /// where money is. Neither references the other: a station sells GRADES, and any vessel of that grade
    /// can take the fuel.</para>
    ///
    /// <para><b>What this asset does NOT decide.</b> How fast a hull burns it, what a tank holds, or what
    /// happens when you run dry — all <c>boats-and-navigation.md</c>'s, and none of it exists yet (there
    /// is no tank on any hull today). Nor does it decide the St Peters general store's over-the-counter
    /// gas: that is a filled CAN sold as an item, which <c>FuelContainerDef</c>'s own remarks already
    /// place as a <c>SupplyDef</c> beside the ice, and it is a separate piece of work.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fuel Station", fileName = "FuelStation")]
    public class FuelStationDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only site id, e.g. \"station.nmc_wharf_pumps\". Never reuse or rename.")]
        public string Id = "station.nmc_wharf_pumps";

        [Tooltip("Player-facing name. Flavour only; the id is canonical.")]
        public string DisplayName = "Wharf Pumps";

        [TextArea] public string Flavor = "";

        [Header("Prices")]
        [Tooltip("One row per grade this site has an opinion about. A grade with no row is not sold here — " +
                 "the same answer as a row with Available unticked, so a site need only author what it " +
                 "actually stocks.")]
        public FuelGradeOffer[] Grades = Array.Empty<FuelGradeOffer>();

        /// <summary>
        /// This site's offer for <paramref name="grade"/> — or a not-available offer if the site has no row
        /// for it.
        ///
        /// <para>A missing row and an unticked row deliberately answer the SAME way, so a station only has
        /// to author what it stocks and every caller has one case to handle rather than two.</para>
        ///
        /// <para>⚠️ On duplicate rows the FIRST wins. That is an authoring error the content-validation
        /// test fails on; first-wins is stated here only so the behaviour is defined rather than
        /// whichever-the-loop-ended-on.</para>
        /// </summary>
        public FuelGradeOffer Offer(string grade)
        {
            if (Grades != null && !string.IsNullOrEmpty(grade))
            {
                for (int i = 0; i < Grades.Length; i++)
                    if (string.Equals(Grades[i].Grade, grade, StringComparison.Ordinal))
                        return Grades[i];
            }
            return new FuelGradeOffer { Grade = grade, Available = false, PricePerLitre = 0f };
        }

        /// <summary>True iff this site sells <paramref name="grade"/> today.</summary>
        public bool Sells(string grade) => Offer(grade).Available;

        /// <summary>
        /// ₲ per litre for <paramref name="grade"/> here, or 0 if it is not sold. <b>Do not read this as
        /// "it is free"</b> — ask <see cref="Sells"/> first; <see cref="FuelPricing"/> does.
        /// </summary>
        public float PricePerLitre(string grade)
        {
            FuelGradeOffer o = Offer(grade);
            return o.Available ? o.PricePerLitre : 0f;
        }

        /// <summary>
        /// How many grades this site actually stocks — the honest count for a validation sweep or a sign
        /// board, ignoring rows that are authored but switched off.
        /// </summary>
        public int StockedGradeCount
        {
            get
            {
                if (Grades == null) return 0;
                int n = 0;
                for (int i = 0; i < Grades.Length; i++)
                    if (Grades[i].Available && FuelGrades.IsKnown(Grades[i].Grade)) n++;
                return n;
            }
        }
    }
}
