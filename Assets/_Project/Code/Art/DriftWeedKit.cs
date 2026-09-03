using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The painted drift-weed art, as data: one flat table of every sliced clump the
    /// <see cref="SeaweedPresenter"/> may draw, each carrying <b>the size the art director actually
    /// drew it at</b> and <b>the anchors the rig declared on it</b>. Built from the kit's own sidecar by
    /// <c>DriftWeedKitBuilder</c> (Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Build Drift Weed
    /// Kit) — never hand-maintained, so a re-drop of the art cannot drift away from this table.
    ///
    /// <para><b>Why the authored size is the load-bearing field.</b> Before this asset the presenter
    /// rescaled whatever sprite it was given to the Def's tier footprint
    /// (<c>scale = footprint / sprite.width</c>). That is fine for the code-built greybox blob and
    /// ruinous for pixel art: the kit's clumps span 0.45 m to 2.0 m, so a 2 m sugar kelp forced into a
    /// 0.45 m tier would be drawn at 0.22× and come apart on the pixel grid. With the drawn size
    /// recorded here the presenter can instead pick the clump nearest the tier it wants and draw it at
    /// <b>scale 1</b> — native pixels, no resampling, ever.</para>
    ///
    /// <para><b>The anchors (round 2).</b> Each variant publishes 2–3 <see cref="Entry.Snags"/> — the
    /// outer frond tips that catch on a buoy line — and one <see cref="Entry.DragTail"/>, the end that
    /// trails when it drifts. They are recorded in the <b>sprite's own frame</b>: metres from the pivot
    /// (the variant's buoyancy centre, which the slicer stamped as the sprite pivot) at scale 1, +y up
    /// — i.e. the sidecar's cell pixels over <see cref="PixelsPerMeter"/>, y flipped. ⚠️ The sidecar's
    /// own <c>m</c> values are WATER-PLANE metres (its y divided by <see cref="YForeshorten"/>); they
    /// describe the rig's geometry, not the drawn pixels. A sprite is flat on the screen and rotates in
    /// the screen plane, so the only frame in which "this frond tip sits on that line" is exact is the
    /// pixel frame — the same reasoning as ADR 0042: the squash is baked into the pixels and nothing
    /// at render time un-bakes it.</para>
    ///
    /// <para><b>Shared across beds (ADR 0003).</b> The art is one table; a bed
    /// (<see cref="SeaweedDef"/>) points at it and may narrow it by species. That way St Peters and
    /// the next harbour share 42 sprites rather than each Def carrying its own copy.</para>
    ///
    /// <para><b>Decor tier, presentation-only.</b> Nothing here enters the save or the sim. Which
    /// clump a piece wears is a seeded choice off the piece's spawn key — never
    /// <see cref="System.Random"/> — resolved by the pure <see cref="SeaweedMath.PickWeedArt"/>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Drift Weed Kit", fileName = "DriftWeedKit")]
    public class DriftWeedKit : ScriptableObject
    {
        /// <summary>One sliced clump: the sprite plus the facts needed to choose and to hang it.</summary>
        [Serializable]
        public struct Entry
        {
            [Tooltip("The sliced sprite — pivot already stamped on the variant's own buoyancy centre " +
                     "by DriftWeedSheetSlicer, so it registers to the water surface.")]
            public Sprite Sprite;

            [Tooltip("The size the rig DREW this clump at (the sidecar's params.sizeM, metres). The " +
                     "presenter matches this against a tier footprint and then draws at scale 1.")]
            public float SizeMeters;

            [Tooltip("Index into Species.")]
            public int SpeciesIndex;

            [Tooltip("Index into Ramps — 0 living, 1 golden, 2 bleached (the sheet's row order).")]
            public int RampIndex;

            [Tooltip("The sidecar variant's own 'cell' column, kept for traceability back to the kit.")]
            public int VariantCell;

            [Tooltip("SNAG anchors — the 2–3 outer frond tips that catch on a buoy line — in the " +
                     "sprite's own frame: metres from the pivot at scale 1, +y up (cell pixels over " +
                     "PixelsPerMeter, y flipped). From the sidecar's snags[].px. Empty = no anchors " +
                     "(a legacy sprite); the presenter then rests the clump on a radius as round 1 did.")]
            public Vector2[] Snags;

            [Tooltip("The DRAG TAIL — the one end that trails when the clump drifts — same frame as Snags. " +
                     "From the sidecar's dragTail.px.")]
            public Vector2 DragTail;

            /// <summary>True when the rig published anchors for this clump.</summary>
            public bool HasAnchors => Snags != null && Snags.Length > 0;
        }

        [Tooltip("Species names in the kit, in sidecar order. Entry.SpeciesIndex indexes this.")]
        public string[] Species = Array.Empty<string>();

        [Tooltip("Ramp row names, in sheet row order (top row first) — the sidecar's ramp_rows.")]
        public string[] Ramps = { "living", "golden", "bleached" };

        [Tooltip("Every sliced clump. Built by the editor tool; edit the art or the sidecar, not this.")]
        public Entry[] Entries = Array.Empty<Entry>();

        [Tooltip("Provenance: the sidecar's derivedFromRigSha256 at build time, so a stale table is " +
                 "identifiable rather than merely wrong.")]
        public string BuiltFromRigSha256 = "";

        [Tooltip("Provenance: the sidecar's scale_px_per_m — the pixels-per-metre the anchors were " +
                 "converted at. The builder refuses a sheet whose import PPU disagrees, because then " +
                 "neither the drawn size nor the anchors would be metre-true at scale 1.")]
        public float PixelsPerMeter = 32f;

        [Tooltip("Provenance: the sidecar's y_foreshorten — the screen-y squash the art was drawn with. " +
                 "NOT applied to the anchors (they are pixels of a flat sprite); recorded so the " +
                 "sidecar's own plane-metre values can be reconciled with the pixel frame.")]
        public float YForeshorten = 0.72f;

        public int Count => Entries?.Length ?? 0;

        // ---- flat views for the pure selector ------------------------------------------------------
        //
        // SeaweedMath is pure statics over plain arrays (it must stay EditMode-testable without
        // assets), so the kit hands it flat arrays rather than the Entry struct. Built once, lazily,
        // and invalidated whenever the table is re-built in the editor.

        [NonSerialized] private float[] _sizes;
        [NonSerialized] private int[] _speciesIndex;
        [NonSerialized] private int[] _rampIndex;
        [NonSerialized] private int _cachedFor = -1;

        public float[] Sizes { get { EnsureViews(); return _sizes; } }
        public int[] SpeciesIndices { get { EnsureViews(); return _speciesIndex; } }
        public int[] RampIndices { get { EnsureViews(); return _rampIndex; } }

        private void EnsureViews()
        {
            int n = Count;
            if (_cachedFor == n && _sizes != null) return;

            _sizes = new float[n];
            _speciesIndex = new int[n];
            _rampIndex = new int[n];
            for (int i = 0; i < n; i++)
            {
                _sizes[i] = Entries[i].SizeMeters;
                _speciesIndex[i] = Entries[i].SpeciesIndex;
                _rampIndex[i] = Entries[i].RampIndex;
            }
            _cachedFor = n;
        }

        /// <summary>Drops the cached flat views — call after re-building <see cref="Entries"/>.</summary>
        public void InvalidateViews() => _cachedFor = -1;

        /// <summary>
        /// Resolves a bed's species-name filter to a per-species allow mask the pure selector can use.
        /// A null/empty filter returns null, which the selector reads as "every species allowed".
        /// Unknown names are ignored (an author's typo must not silently empty a bed) — but if the
        /// filter names ONLY unknown species the result is null rather than a mask that forbids
        /// everything, so the bed still draws weed.
        /// </summary>
        public bool[] ResolveSpeciesMask(string[] filter)
        {
            if (filter == null || filter.Length == 0 || Species == null || Species.Length == 0)
                return null;

            var mask = new bool[Species.Length];
            int allowed = 0;
            for (int f = 0; f < filter.Length; f++)
            {
                if (string.IsNullOrWhiteSpace(filter[f])) continue;
                for (int s = 0; s < Species.Length; s++)
                {
                    if (!string.Equals(Species[s], filter[f], StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!mask[s]) { mask[s] = true; allowed++; }
                    break;
                }
            }
            return allowed > 0 ? mask : null;
        }
    }
}
