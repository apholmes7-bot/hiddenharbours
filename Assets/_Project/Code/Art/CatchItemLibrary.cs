using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
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
            // around hpivot per kind: lobster (32,16), crab (32,12), while a walking one uses
            // (32,40). Each held sheet carries its kind's grip on the animal's back.

            [Tooltip("The kind in a hand: HeldFacings × HeldFramesPerFacing, facing-major. Empty " +
                     "for a kind whose rig bakes no held pose.")]
            public Sprite[] Held;

            [Tooltip("8 for an animal the rig lofts and turns (lobster, crab, a fish by the tail); " +
                     "1 for a handful of shellfish, which the rig draws with no camera at all.")]
            public int HeldFacings;

            [Tooltip("Frames per facing in Held — the held pose's own animation, or its variant " +
                     "count for a non-directional handful.")]
            public int HeldFramesPerFacing;

            [Tooltip("Which baked facing each facing DRAWS in a hand, HeldFacings long; empty = each facing " +
                     "draws itself. Set by the library builder for a flatfish (FishSpeciesDef.HeldBroadSideOn): " +
                     "a fish hung by its tail hangs nearly upright, so turning the camera about it is turning " +
                     "the fish about its length, and each facing that shows its edge draws the neighbouring " +
                     "facing that shows the most of it, measured off the held sheet.")]
            public int[] HeldFacingMap;

            [Tooltip("The kind in a hand as a facet MESH (ADR 0053): the fish rig's own geometry at the " +
                     "tail rest, grip at the origin. Set by the library builder from the held-prop bake; " +
                     "empty for a kind the bake does not make. Nothing draws it yet.")]
            public HeldPropMeshDef HeldMesh;

            // ---- the same animal, drawn at another size ----------------------------------------
            //
            // The runtime never scales a fish sprite, so a 2 kg cod and a 12 kg cod only look different
            // because the rig drew them at different sizes (the catch pass 2 size ladder: _sm, the
            // middle rung, _lg). A sized kind is one rung of that ladder: 'cod_sm' is SizeOf 'cod'.

            [Tooltip("Empty for a kind that is its own ladder's base (the middle rung, 'cod'); else the " +
                     "base kind this one is a size rung of ('cod_sm' and 'cod_lg' say 'cod'). Species map " +
                     "to the base kind; SizedKindFor picks the rung by the catch's weight.")]
            public string SizeOf;

            [Tooltip("The weight this rung was drawn at, kg, read from the fish rig's own anchors " +
                     "(FishIsoAnchors.json). 0 = unsized: the kind is drawn at one size whatever it weighs.")]
            public float SizeKg;

            [Tooltip("How long this rung's fish is DRAWN, in sprite px: the longest side of the held " +
                     "sheet's opaque bounds over every facing's still frame. Measured off the sheet by the " +
                     "library builder; the readability floor (GameConfig.MinDrawnFishLengthPx) skips a " +
                     "rung drawn shorter than it. 0 = unmeasured: the floor leaves the rung alone.")]
            public int HeldLengthPx;
        }

        [Serializable]
        public sealed class SpeciesEntry
        {
            [Tooltip("Hold species id, e.g. fish.atlantic_cod")]
            public string SpeciesId;

            [Tooltip("The visual kind it draws as (a Kind key above)")]
            public string Kind;
        }

        /// <summary>One bucket state as a facet mesh (ADR 0053): a bucket tier at a fill band, holding
        /// a catch group — named in the bucket rig's own words, so nothing finds it by arithmetic.</summary>
        [Serializable]
        public sealed class BucketMeshEntry
        {
            [Tooltip("The bucket rig's tier id: pail / tote / tray.")]
            public string TierId;

            [Tooltip("The fill band the mesh shows.")]
            public CatchFillBand Band;

            [Tooltip("The bucket rig's catch group: fish / shell / crust. Empty for the Empty band.")]
            public string Group;

            [Tooltip("The baked state.")]
            public HeldPropMeshDef Mesh;
        }

        [SerializeField, Tooltip("Sprite variants per catch kind")]
        private KindEntry[] _kinds;

        [SerializeField, Tooltip("Hold speciesId → visual kind")]
        private SpeciesEntry[] _species;

        [SerializeField, Tooltip("Visual kind for species with no mapping — the tote should still " +
                                 "gain SOMETHING when an unmapped catch lands, never under-read")]
        private string _fallbackKind = "cod";

        [SerializeField, Tooltip("Every bucket state as a facet mesh — tier × band × catch group, from the " +
                                 "held-prop bake (ADR 0053). The pail's presenter already holds this " +
                                 "asset, so the meshes reach it without a scene holding a copy. Nothing " +
                                 "draws them yet.")]
        private BucketMeshEntry[] _bucketMeshes = Array.Empty<BucketMeshEntry>();

        // Lookup caches (built once; invalidated when the asset reloads).
        [NonSerialized] private Dictionary<string, KindEntry> _kindLookup;
        [NonSerialized] private Dictionary<string, string> _speciesLookup;
        // base kind → every sized kind of its ladder (the base itself included when it is sized).
        [NonSerialized] private Dictionary<string, List<KindEntry>> _ladderLookup;

        private void OnEnable()
        {
            _kindLookup = null;
            _speciesLookup = null;
            _ladderLookup = null;
        }

        private void BuildLookups()
        {
            _kindLookup = new Dictionary<string, KindEntry>(StringComparer.Ordinal);
            _ladderLookup = new Dictionary<string, List<KindEntry>>(StringComparer.Ordinal);
            if (_kinds != null)
                foreach (var e in _kinds)
                    if (e != null && !string.IsNullOrEmpty(e.Kind))
                    {
                        _kindLookup[e.Kind] = e;
                        if (e.SizeKg <= 0f) continue;
                        string ladder = string.IsNullOrEmpty(e.SizeOf) ? e.Kind : e.SizeOf;
                        if (!_ladderLookup.TryGetValue(ladder, out List<KindEntry> rungs))
                            _ladderLookup[ladder] = rungs = new List<KindEntry>(3);
                        rungs.Add(e);
                    }

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
            // The face-on pick (a flatfish shows its broad side): a map the wrong length, or an entry
            // outside the sheet, is ignored rather than trusted, so a stale map never draws a blank.
            if (e.HeldFacingMap != null && e.HeldFacingMap.Length == facings &&
                e.HeldFacingMap[d] >= 0 && e.HeldFacingMap[d] < facings)
                d = e.HeldFacingMap[d];
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

        /// <summary>How long this kind's held fish is drawn, px (<see cref="KindEntry.HeldLengthPx"/>);
        /// 0 when unmeasured or unknown.</summary>
        public int HeldLengthPxFor(string kind)
        {
            if (_kindLookup == null) BuildLookups();
            return kind != null && _kindLookup.TryGetValue(kind, out var e) ? e.HeldLengthPx : 0;
        }

        /// <summary>This kind's face-on facing map (<see cref="KindEntry.HeldFacingMap"/>), or null when
        /// every facing draws itself. For tests and tooling; <see cref="HeldSprite"/> already applies it.</summary>
        public int[] HeldFacingMapFor(string kind)
        {
            if (_kindLookup == null) BuildLookups();
            return kind != null && _kindLookup.TryGetValue(kind, out var e) &&
                   e.HeldFacingMap != null && e.HeldFacingMap.Length > 0 ? e.HeldFacingMap : null;
        }

        /// <summary>This kind in a hand as a facet mesh, or null when the held-prop bake makes none
        /// for it.</summary>
        public HeldPropMeshDef HeldMeshFor(string kind)
        {
            if (_kindLookup == null) BuildLookups();
            return kind != null && _kindLookup.TryGetValue(kind, out var e) ? e.HeldMesh : null;
        }

        /// <summary>One bucket state's mesh, or null when it was not baked. The Empty band carries no
        /// catch, so it ignores <paramref name="group"/>. A scan, not a cache: it runs when a fill band
        /// changes, never per frame.</summary>
        public HeldPropMeshDef BucketMesh(string tierId, CatchFillBand band, string group)
        {
            if (_bucketMeshes == null) return null;
            foreach (BucketMeshEntry e in _bucketMeshes)
                if (e != null && e.TierId == tierId && e.Band == band &&
                    (band == CatchFillBand.Empty || e.Group == group))
                    return e.Mesh;
            return null;
        }

        /// <summary>
        /// Wire the whole table in one call (the importer that builds the committed asset, and tests) —
        /// the <c>Configure</c> seam used across the lane. The arrays are private serialized fields
        /// because they are the OWNER's to edit in an inspector, not an API; this is the one door in, and
        /// it invalidates the lookup caches so a re-wired table is read fresh.
        /// </summary>
        public void Configure(KindEntry[] kinds, SpeciesEntry[] species, string fallbackKind = null,
                              BucketMeshEntry[] bucketMeshes = null)
        {
            if (kinds != null) _kinds = kinds;
            if (species != null) _species = species;
            if (!string.IsNullOrEmpty(fallbackKind)) _fallbackKind = fallbackKind;
            if (bucketMeshes != null) _bucketMeshes = bucketMeshes;
            _kindLookup = null;
            _speciesLookup = null;
            _ladderLookup = null;
        }

        /// <summary>
        /// The visual kind a catch of this species <b>and this weight</b> draws as: the size rung of its
        /// kind closest to it in drawn length (<see cref="CatchSize.LengthGap"/>, the same distance the
        /// fight's ladder picks by), so a 2 kg cod is held and laid as the small cod and a 12 kg cod as the
        /// large one. A kind with no sized rungs answers itself, so a table with no ladder (the shellfish,
        /// a test's hand-made table) behaves exactly as <see cref="KindFor"/>. On an exact tie the lighter
        /// rung wins, as it does on the line. The configured readability floor applies first
        /// (<see cref="ReadableWeightKg(string, float)"/>).
        /// </summary>
        public string SizedKindFor(string speciesId, float weightKg)
            => SizedKindFor(speciesId, weightKg, GameServices.MinDrawnFishLengthPx);

        /// <summary>
        /// <see cref="SizedKindFor(string, float)"/> with the readability floor stated rather than read
        /// from the config: the weight is first raised by <see cref="ReadableWeightKg(string, float, float)"/>,
        /// so a rung drawn shorter than <paramref name="minDrawnLengthPx"/> is never picked while a longer
        /// one exists. 0 = no floor.
        /// </summary>
        public string SizedKindFor(string speciesId, float weightKg, float minDrawnLengthPx)
        {
            string kind = KindFor(speciesId);
            if (kind == null) return null;
            if (_ladderLookup == null) BuildLookups();
            if (!_ladderLookup.TryGetValue(kind, out List<KindEntry> rungs) || rungs.Count == 0) return kind;

            weightKg = ReadableWeightKg(speciesId, weightKg, minDrawnLengthPx);
            KindEntry best = null;
            float bestGap = float.MaxValue;
            for (int i = 0; i < rungs.Count; i++)
            {
                float gap = CatchSize.LengthGap(rungs[i].SizeKg, weightKg);
                if (gap < bestGap || (gap == bestGap && best != null && rungs[i].SizeKg < best.SizeKg))
                {
                    bestGap = gap;
                    best = rungs[i];
                }
            }
            return best != null ? best.Kind : kind;
        }

        /// <summary>
        /// <b>The weight a catch is DRAWN at</b>: its own, raised to the lightest rung of its species'
        /// ladder whose drawing is at least <paramref name="minDrawnLengthPx"/> long (the largest rung
        /// when none is), so a fish too small to read in his hands is shown with its species' smallest
        /// readable drawing (<see cref="CatchSize.DrawnLengthFloor"/>). Only the picture moves: the item's
        /// own weight, value and carry rule never see this number.
        ///
        /// <para><b>One rule, three places.</b> The hands and the tote reach it through
        /// <see cref="SizedKindFor(string, float)"/>, and the fight on the line asks it before picking its
        /// own rung (<c>FishArtLibrary.LineRungFor</c>), so all three draw the same fish. A species with
        /// no ladder, an unmeasured rung (<see cref="KindEntry.HeldLengthPx"/> 0) or a floor of 0 leaves
        /// the weight as it is.</para>
        /// </summary>
        public float ReadableWeightKg(string speciesId, float weightKg, float minDrawnLengthPx)
        {
            if (minDrawnLengthPx <= 0f) return weightKg;
            string kind = KindFor(speciesId);
            if (kind == null) return weightKg;
            if (_ladderLookup == null) BuildLookups();
            if (!_ladderLookup.TryGetValue(kind, out List<KindEntry> rungs) || rungs.Count == 0) return weightKg;

            var floor = new CatchSize.DrawnLengthFloor(minDrawnLengthPx);
            for (int i = 0; i < rungs.Count; i++) floor.Consider(rungs[i].SizeKg, rungs[i].HeldLengthPx);
            return floor.Apply(weightKg);
        }

        /// <summary><see cref="ReadableWeightKg(string, float, float)"/> at the configured floor
        /// (<see cref="GameServices.MinDrawnFishLengthPx"/>).</summary>
        public float ReadableWeightKg(string speciesId, float weightKg)
            => ReadableWeightKg(speciesId, weightKg, GameServices.MinDrawnFishLengthPx);

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
