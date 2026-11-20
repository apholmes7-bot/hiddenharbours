using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One kind of kitchen station</b>: a prep counter, a stove, a microwave (owner asks 2026-11-14 and
    /// 2026-11-15: galleys and kitchens cook raw food, prep first then cook; "add microwaves as a cooking
    /// option to boats, and houses"; "microwaves can only reheat cooked or microwavable items"). One asset
    /// per kind under <c>Data/Resources/CookingStations/</c>, id <c>cooking_station.snake_case</c> (rule 2);
    /// every number and line is the owner's to tune (rule 6).
    ///
    /// <para><b>A station runs recipes, it does not own them.</b> <see cref="AcceptedStations"/> names the
    /// recipe station ids (<see cref="RecipeDef.Station"/>) it can run, so the same
    /// <see cref="RecipeDef"/>s serve a boat galley, a house kitchen and a shop. A stove can also heat what
    /// a microwave heats by listing both ids. <see cref="RecipeRunner"/> is the one place recipes are run.
    /// </para>
    ///
    /// <para><b>The microwave rule is data.</b> With <see cref="MicrowavableOnly"/> set, an input is taken only
    /// when its <see cref="FoodDef.Microwavable"/> is true; a raw catch or a raw cut is refused and stays
    /// where it was.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Food/Cooking Station", fileName = "CookingStation")]
    public sealed class CookingStationDef : ScriptableObject
    {
        /// <summary>The folder under any <c>Resources</c> root the shipped stations live in.</summary>
        public const string ResourcesFolder = "CookingStations";

        [Tooltip("Stable id, `cooking_station.snake_case` (cooking_station.stove). Append-only.")]
        public string Id;

        [Tooltip("The name the player reads.")]
        public string DisplayName;

        [Tooltip("What kind of station this is (append-only enum). A label; what it runs is the fields below.")]
        public CookingStationKind Kind;

        [Tooltip("The recipe station ids this station runs (`station.prep`, `station.cooktop`, " +
                 "`station.microwave`). Open set: a new id here is a new ability, no code.")]
        public string[] AcceptedStations = Array.Empty<string>();

        [Tooltip("A recipe's work time here is its WorkSeconds times this. Below 1 is faster (a microwave).")]
        [Min(0.01f)] public float CookTimeMultiplier = 1f;

        [Tooltip("Only take food whose FoodDef is Microwavable (cooked or microwavable). A raw catch or a raw " +
                 "cut is refused (owner 2026-11-15).")]
        public bool MicrowavableOnly;

        [Tooltip("How close (m) the player must stand to use it.")]
        [Min(0.1f)] public float ReachMeters = 1f;

        [Tooltip("What the interact popup and the chooser offer: present tense, a few words, never the key.")]
        public string VerbLabel;

        [Tooltip("Said when cooking starts. {0} is the recipe's name.")]
        public string StartedLine;

        [Tooltip("Said when the food is done and put away in the supply locker. {0} is the recipe's name.")]
        public string ReadyLine;

        [Tooltip("Said when there is nothing this station can cook.")]
        public string NothingLine;

        [Tooltip("Said when the station is already cooking.")]
        public string BusyLine;

        [Tooltip("Said when the food offered cannot go in (a microwave and raw food).")]
        public string NotMicrowavableLine;

        [Tooltip("Said when the catch in the hand needs preparing first.")]
        public string NeedsPrepLine;

        [Header("The food on it")]
        [Tooltip("Draw the fish being worked on this station (on the pan or the board) for the length of the " +
                 "job. Off for a microwave: its door is shut.")]
        public bool ShowsFood;

        [Tooltip("Where the pan or the board sits, in the station's own frame: metres from its pivot, x along its " +
                 "front, y toward the wall it backs onto, z up. Turned with the wall it stands against.")]
        public Vector3 FoodAnchorPropMeters;

        [Tooltip("The food browns as it cooks, along GameConfig.CookedFishRamp. Off for a prep board: the " +
                 "fish there stays raw.")]
        public bool BrownsAsItCooks;

        /// <summary>The multiplier in force: an unset (zero or less) value reads as 1.</summary>
        public float CookTimeMultiplierInForce => CookTimeMultiplier > 0f ? CookTimeMultiplier : 1f;

        /// <summary>True when this station runs recipes at <paramref name="recipeStationId"/>.</summary>
        public bool Accepts(string recipeStationId)
        {
            if (string.IsNullOrEmpty(recipeStationId) || AcceptedStations == null) return false;
            for (int i = 0; i < AcceptedStations.Length; i++)
                if (string.Equals(AcceptedStations[i], recipeStationId, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>The line for a refusal, or null for <see cref="CookRefusal.None"/>.</summary>
        public string LineFor(CookRefusal reason) => reason switch
        {
            CookRefusal.NothingToCook => NothingLine,
            CookRefusal.NoStation => NothingLine,
            CookRefusal.Busy => BusyLine,
            CookRefusal.NotMicrowavable => NotMicrowavableLine,
            CookRefusal.NeedsPrep => NeedsPrepLine,
            _ => null,
        };
    }
}
