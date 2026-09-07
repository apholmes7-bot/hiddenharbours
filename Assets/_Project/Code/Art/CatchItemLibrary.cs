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

            // ---- the same animal, in a hand -----------------------------------------------------
            //
            // A DIFFERENT PIVOT, which is why it cannot be another variant of the row above. The lay
            // variants pivot on GROUND CONTACT — the item dropped in a tote. Held art pivots on THE
            // GRIP, so it can be pinned to a character's hand anchor: the crustacean rig bakes it
            // around hpivot (32,12), the point on the animal's back a hand closes over, while a
            // walking one sits at (32,40). One sheet cannot carry two pivots.

            [Tooltip("The kind in a hand: HeldFacings × HeldFramesPerFacing, facing-major. Empty " +
                     "for a kind whose rig bakes no held pose.")]
            public Sprite[] Held;

            [Tooltip("8 for an animal the rig lofts and turns (lobster, crab, a fish by the tail); " +
                     "1 for a handful of shellfish, which the rig draws with no camera at all.")]
            public int HeldFacings;

            [Tooltip("Frames per facing in Held — the held pose's own animation, or its variant " +
                     "count for a non-directional handful.")]
            public int HeldFramesPerFacing;
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

        /// <summary>
        /// The sprite for this kind held in a hand, at <paramref name="facing"/> and
        /// <paramref name="frame"/>, or null when the kind bakes no held pose.
        ///
        /// <para>Both indices wrap rather than throw, and a non-directional kind (a handful, whose
        /// <c>HeldFacings</c> is 1) simply ignores the facing — so a caller can hand over whatever
        /// facing the carrier is using without first asking whether this particular catch turns.</para>
        /// </summary>
        public Sprite HeldSprite(string kind, int facing, int frame)
        {
            if (_kindLookup == null) BuildLookups();
            if (kind == null || !_kindLookup.TryGetValue(kind, out var e)) return null;
            if (e.Held == null || e.Held.Length == 0) return null;

            int facings = Mathf.Max(1, e.HeldFacings);
            int per = Mathf.Max(1, e.HeldFramesPerFacing);
            int d = ((facing % facings) + facings) % facings;
            int f = ((frame % per) + per) % per;

            int i = d * per + f;
            return i >= 0 && i < e.Held.Length ? e.Held[i] : null;
        }

        /// <summary>How many facings this kind's held art was baked at — 0 when it bakes none. A
        /// carrier asks this to decide whether to turn the thing in the hand at all.</summary>
        public int HeldFacingsFor(string kind)
        {
            if (_kindLookup == null) BuildLookups();
            if (kind == null || !_kindLookup.TryGetValue(kind, out var e)) return 0;
            return e.Held == null || e.Held.Length == 0 ? 0 : Mathf.Max(1, e.HeldFacings);
        }

        /// <summary>
        /// Wire the whole table in one call (the importer that builds the committed asset, and tests) —
        /// the <c>Configure</c> seam used across the lane. The arrays are private serialized fields
        /// because they are the OWNER's to edit in an inspector, not an API; this is the one door in, and
        /// it invalidates the lookup caches so a re-wired table is read fresh.
        /// </summary>
        public void Configure(KindEntry[] kinds, SpeciesEntry[] species, string fallbackKind = null)
        {
            if (kinds != null) _kinds = kinds;
            if (species != null) _species = species;
            if (!string.IsNullOrEmpty(fallbackKind)) _fallbackKind = fallbackKind;
            _kindLookup = null;
            _speciesLookup = null;
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
