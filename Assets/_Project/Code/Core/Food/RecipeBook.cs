using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The recipe lookup</b>, a pure POCO over a set of <see cref="RecipeDef"/>s: which recipe does this
    /// station run on this input? No state, no randomness, no clock; the same recipes and the same input
    /// always answer the same recipe (rule 5).
    ///
    /// <para><b>Specificity.</b> For a raw catch, a recipe naming its species beats one naming its category.
    /// Inside one tier the lowest id (ordinal) wins, so two equally specific assets never depend on load
    /// order. An intermediate (a prep output) matches only an <see cref="RecipeInputKind.Intermediate"/>
    /// recipe naming that exact id.</para>
    ///
    /// <para><b>Refusal.</b> Every lookup answers false with a null recipe for an empty station, an empty or
    /// unknown input, or a station with no matching recipe; it never throws. A blank-id or duplicate-id Def
    /// is skipped (the first one given is kept); the content validation test fails such an asset loudly.
    /// </para>
    /// </summary>
    public sealed class RecipeBook
    {
        /// <summary>The folder under any <c>Resources</c> root the shipped recipes live in.</summary>
        public const string ResourcesFolder = "Recipes";

        private readonly List<RecipeDef> _all = new List<RecipeDef>();
        private readonly Dictionary<string, RecipeDef> _byId = new Dictionary<string, RecipeDef>(StringComparer.Ordinal);

        public RecipeBook(IEnumerable<RecipeDef> recipes)
        {
            if (recipes != null)
                foreach (RecipeDef def in recipes)
                {
                    if (def == null || string.IsNullOrEmpty(def.Id) || _byId.ContainsKey(def.Id)) continue;
                    _byId.Add(def.Id, def);
                    _all.Add(def);
                }
            // Ids are unique now, so this order is total: lookups never depend on load order.
            _all.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        /// <summary>Every shipped recipe under <c>Resources/Recipes/</c>.</summary>
        public static RecipeBook FromResources() => new RecipeBook(Resources.LoadAll<RecipeDef>(ResourcesFolder));

        /// <summary>Every kept recipe, sorted ordinal by id.</summary>
        public IReadOnlyList<RecipeDef> All => _all;

        public RecipeDef Find(string id)
            => !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out RecipeDef def) ? def : null;

        /// <summary>The recipe <paramref name="stationId"/> runs on a raw catch: species first, then category.
        /// A catch with no species id (a default struct) is refused.</summary>
        public bool TryFind(string stationId, in CatchItem catchItem, out RecipeDef recipe)
        {
            recipe = null;
            if (string.IsNullOrEmpty(stationId) || string.IsNullOrEmpty(catchItem.SpeciesId)) return false;

            RecipeDef byCategory = null;
            foreach (RecipeDef def in _all)
            {
                if (!string.Equals(def.Station, stationId, StringComparison.Ordinal)) continue;
                if (def.InputKind == RecipeInputKind.Species
                    && string.Equals(def.InputId, catchItem.SpeciesId, StringComparison.Ordinal))
                {
                    recipe = def;
                    return true;
                }
                if (byCategory == null && def.InputKind == RecipeInputKind.Category
                    && def.InputCategory == catchItem.Category)
                    byCategory = def;
            }
            recipe = byCategory;
            return recipe != null;
        }

        /// <summary>The recipe <paramref name="stationId"/> runs on an intermediate (another recipe's output id).
        /// </summary>
        public bool TryFind(string stationId, string intermediateId, out RecipeDef recipe)
        {
            recipe = null;
            if (string.IsNullOrEmpty(stationId) || string.IsNullOrEmpty(intermediateId)) return false;
            foreach (RecipeDef def in _all)
            {
                if (def.InputKind != RecipeInputKind.Intermediate) continue;
                if (!string.Equals(def.Station, stationId, StringComparison.Ordinal)) continue;
                if (!string.Equals(def.InputId, intermediateId, StringComparison.Ordinal)) continue;
                recipe = def;
                return true;
            }
            return false;
        }

        /// <summary>True when <paramref name="upstream"/>'s output is exactly <paramref name="downstream"/>'s
        /// intermediate input (prep fillet → cooktop dish).</summary>
        public static bool Feeds(RecipeDef upstream, RecipeDef downstream)
            => upstream != null && downstream != null
               && downstream.InputKind == RecipeInputKind.Intermediate
               && !string.IsNullOrEmpty(upstream.OutputId)
               && string.Equals(upstream.OutputId, downstream.InputId, StringComparison.Ordinal);

        /// <summary>The recipes in this book whose output feeds <paramref name="downstream"/>, optionally only
        /// those at <paramref name="stationId"/> (null: any station).</summary>
        public List<RecipeDef> FeedersOf(RecipeDef downstream, string stationId = null)
        {
            var feeders = new List<RecipeDef>();
            foreach (RecipeDef def in _all)
                if (Feeds(def, downstream)
                    && (stationId == null || string.Equals(def.Station, stationId, StringComparison.Ordinal)))
                    feeders.Add(def);
            return feeders;
        }

        /// <summary>True when a chain of recipes in this book leads from a raw catch to
        /// <paramref name="recipe"/>. A cycle or a dangling intermediate answers false.</summary>
        public bool IsReachableFromCatch(RecipeDef recipe) => Reachable(recipe, new HashSet<string>(StringComparer.Ordinal));

        private bool Reachable(RecipeDef recipe, HashSet<string> visiting)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.Id)) return false;
            if (recipe.InputKind == RecipeInputKind.Species) return !string.IsNullOrEmpty(recipe.InputId);
            if (recipe.InputKind == RecipeInputKind.Category) return true;
            if (recipe.InputKind != RecipeInputKind.Intermediate || !visiting.Add(recipe.Id)) return false;
            foreach (RecipeDef def in _all)
                if (Feeds(def, recipe) && Reachable(def, visiting)) return true;
            return false;
        }
    }
}
