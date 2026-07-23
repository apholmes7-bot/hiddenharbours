using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The catch-item ART TABLE (content is data — CLAUDE.md rule 2): which baked sprite draws
    /// each catch kind's lay variants inside a container fill, and which visual kind each hold
    /// <c>speciesId</c> maps to. One asset the owner can rewire without code.
    ///
    /// <para><b>Where the sprites come from:</b> fish kinds reuse the #265 fish sheets' dry
    /// <c>deck</c> lays (<c>Fish_&lt;species&gt;_deck_d&lt;dir&gt;_f&lt;frame&gt;</c> — variant v
    /// is the rig's lay recipe: direction row [2,6,3,5][v], frame v, exactly what
    /// <c>CatchKit.item</c> composes); lobster/crab/mussel/clam come from the storage bake's
    /// <c>CatchItem_&lt;kind&gt;_d0_f&lt;variant&gt;</c> strips. Until the owner runs the storage
    /// bake those entries stay empty and the fill renderer simply skips unmapped kinds.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "CatchItemLibrary",
                     menuName = "Hidden Harbours/Art/Catch Item Library")]
    public sealed class CatchItemLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class KindEntry
        {
            [Tooltip("Catch kind key: cod / haddock / pollock / mackerel / lobster / crab / mussel / clam")]
            public string Kind;

            [Tooltip("The 4 baked lay variants, in variant order (sheet columns f0..f3 for the " +
                     "CatchItem strips; the deck-lay recipe for fish).")]
            public Sprite[] Variants;
        }

        [Serializable]
        public sealed class SpeciesEntry
        {
            [Tooltip("Hold species id, e.g. fish.atlantic_cod")]
            public string SpeciesId;

            [Tooltip("The visual kind it draws as (a Kind key above)")]
            public string Kind;
        }

        [SerializeField, Tooltip("Sprite variants per catch kind")]
        private KindEntry[] _kinds;

        [SerializeField, Tooltip("Hold speciesId → visual kind")]
        private SpeciesEntry[] _species;

        [SerializeField, Tooltip("Visual kind for species with no mapping — the tote should still " +
                                 "gain SOMETHING when an unmapped catch lands, never under-read")]
        private string _fallbackKind = "cod";

        // Lookup caches (built once; invalidated when the asset reloads).
        [NonSerialized] private Dictionary<string, KindEntry> _kindLookup;
        [NonSerialized] private Dictionary<string, string> _speciesLookup;

        private void OnEnable()
        {
            _kindLookup = null;
            _speciesLookup = null;
        }

        private void BuildLookups()
        {
            _kindLookup = new Dictionary<string, KindEntry>(StringComparer.Ordinal);
            if (_kinds != null)
                foreach (var e in _kinds)
                    if (e != null && !string.IsNullOrEmpty(e.Kind))
                        _kindLookup[e.Kind] = e;

            _speciesLookup = new Dictionary<string, string>(StringComparer.Ordinal);
            if (_species != null)
                foreach (var e in _species)
                    if (e != null && !string.IsNullOrEmpty(e.SpeciesId) && !string.IsNullOrEmpty(e.Kind))
                        _speciesLookup[e.SpeciesId] = e.Kind;
        }

        /// <summary>The sprite for one drawn item, or null when that kind has no art wired yet
        /// (the renderer skips it rather than drawing a blank).</summary>
        public Sprite SpriteFor(string kind, int variant)
        {
            if (_kindLookup == null) BuildLookups();
            if (kind == null || !_kindLookup.TryGetValue(kind, out var e)) return null;
            if (e.Variants == null || e.Variants.Length == 0) return null;
            int v = variant % e.Variants.Length;
            if (v < 0) v += e.Variants.Length;
            return e.Variants[v];
        }

        /// <summary>The visual kind a hold species draws as (mapped, else the fallback).</summary>
        public string KindFor(string speciesId)
        {
            if (_speciesLookup == null) BuildLookups();
            return speciesId != null && _speciesLookup.TryGetValue(speciesId, out string kind)
                ? kind
                : _fallbackKind;
        }
    }
}
