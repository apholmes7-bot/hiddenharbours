#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;                // ITidalTerrain

namespace HiddenHarbours.App.Editor
{
    /// <summary>What a woodland zone DOES to the planting inside it. Three kinds and no fourth: the two
    /// that take trees away, and the one that puts more of them in.</summary>
    public enum WoodlandZoneKind
    {
        /// <summary>Nothing woody stands here at all — a dooryard, a garden, a working yard. The kind
        /// <see cref="StPetersGinnyPlot"/> declares for her land.</summary>
        Clearing,

        /// <summary>A CUT WALKING LANE. Geometrically it is a clearing — no trunk stands in the tread —
        /// but it is a different claim, and carrying the distinction is what lets a test say <i>"this
        /// stand has a path through it"</i> rather than only <i>"this ground is empty"</i>. A corridor is
        /// the owner's <b>paths are cut, not implied</b> (2026-08-16).</summary>
        Corridor,

        /// <summary>A WOODLAND LOT: bounded ground that carries a genuine closed stand, denser than
        /// whatever the region's ambient planting would put there — and thinning through a fringe so the
        /// edge reads as an edge rather than as a shape cut out of a forest.</summary>
        Lot,
    }

    /// <summary>
    /// <b>ONE WOODLAND ZONE — a bounded lot of ground and what grows on it.</b> A row in a region's zone
    /// table: declarative, so a region says <i>where</i> its woods thicken and <i>where</i> a path is cut
    /// without any placement code knowing the difference.
    ///
    /// <para><b>⭐ THE SHAPE IS A CAPSULE, AND THAT IS THE WHOLE REASON ONE STRUCT COVERS THREE KINDS.</b>
    /// A zone is a spine (<see cref="From"/> → <see cref="To"/>) swept by <see cref="Radius"/>. Set the
    /// two ends equal and it is a CIRCLE — a clearing round a cottage, a round wood lot. Leave them apart
    /// and it is a LOZENGE — a path corridor, a wood lot running along a field boundary. Every question
    /// placement asks ("is this ground in the zone", "how near the middle of it am I", "what rectangle
    /// bounds it") is then one distance-to-segment call, which matters because
    /// <see cref="StPetersWoods.IsPlantable"/> and its Nine Mile Creek counterpart ask it tens of
    /// thousands of times per build.</para>
    ///
    /// <para><b>The fringe is not decoration.</b> <see cref="CoreFraction"/> splits the capsule into a
    /// solid core and a taper, and <see cref="CoreShare"/> falls from 1 to 0 across the taper. A lot
    /// planted at a flat density to a hard rim reads as a stamp; the same lot with a thinning fringe
    /// reads as a wood. The owner asked for <i>edges that read</i>, and this is where that lives.</para>
    ///
    /// <para><b>⚠ A zone declares ground, NOT a right.</b> There is no land-ownership system in this repo
    /// (<see cref="StPetersGinnyPlot"/>'s own note) and this struct does not invent one. The only thing
    /// that reads a zone is planting.</para>
    /// </summary>
    public readonly struct WoodlandZone
    {
        /// <summary>The zone's name, so a failing test names a lot or a lane rather than an index.</summary>
        public readonly string Name;

        public readonly WoodlandZoneKind Kind;

        /// <summary>One end of the spine. Equal to <see cref="To"/> for a circular zone.</summary>
        public readonly Vector2 From;

        /// <summary>The other end of the spine.</summary>
        public readonly Vector2 To;

        /// <summary>Half-width of the capsule: how far from the spine the zone reaches.</summary>
        public readonly float Radius;

        /// <summary>
        /// Fraction of <see cref="Radius"/> that is solid CORE — inside it a lot plants at full density
        /// and a clearing is unambiguously clear. From there out to the rim, <see cref="CoreShare"/>
        /// tapers to zero.
        ///
        /// <para>⚠ Read by <see cref="CoreShare"/> only, which every KIND asks: a clearing's taper is
        /// unused (it clears its whole capsule — a dooryard with a ragged edge is still a dooryard), so
        /// the value only bites on a <see cref="WoodlandZoneKind.Lot"/>.</para>
        /// </summary>
        public readonly float CoreFraction;

        /// <summary>
        /// <b>Lot only:</b> how far apart trunks stand in the core, in metres.
        ///
        /// <para><b>⭐ ABSOLUTE, not a multiplier on the region's ambient density, and the two regions are
        /// why.</b> St Peters has ambient woods to be denser THAN; Nine Mile Creek has none at all — its
        /// hinterland is farmed, so a multiplier there would multiply zero. A spacing in metres says the
        /// same thing to both ("trunks about this far apart, which is a closed stand") and is the number a
        /// test can measure on the ground rather than infer.</para>
        /// </summary>
        public readonly float CoreSpacingMetres;

        /// <summary>Why this zone is here, in words. Carried so the table reads as an argument rather than
        /// as a list of coordinates — the same reason <see cref="StPetersGinnyPlot.Shed"/> carries one.</summary>
        public readonly string Reason;

        public WoodlandZone(string name, WoodlandZoneKind kind, Vector2 from, Vector2 to, float radius,
                            float coreFraction, float coreSpacingMetres, string reason)
        {
            Name = name;
            Kind = kind;
            From = from;
            To = to;
            Radius = radius;
            CoreFraction = coreFraction;
            CoreSpacingMetres = coreSpacingMetres;
            Reason = reason;
        }

        /// <summary>A circular zone — the two spine ends at one point. Clearings and round lots.</summary>
        public static WoodlandZone Circle(string name, WoodlandZoneKind kind, Vector2 at, float radius,
                                          float coreFraction, float coreSpacingMetres, string reason)
            => new WoodlandZone(name, kind, at, at, radius, coreFraction, coreSpacingMetres, reason);

        /// <summary>A cut walking lane along a spine. Nothing woody stands within <paramref name="halfWidth"/>
        /// of it.</summary>
        public static WoodlandZone Lane(string name, Vector2 from, Vector2 to, float halfWidth, string reason)
            => new WoodlandZone(name, WoodlandZoneKind.Corridor, from, to, halfWidth, 1f, 0f, reason);

        /// <summary>Distance (m) from <paramref name="p"/> to the zone's spine.</summary>
        public float SpineDistance(Vector2 p) => StPetersShoreMap.DistanceToSegment(p, From, To);

        /// <summary>Is this ground inside the zone at all?</summary>
        public bool Contains(Vector2 p) => SpineDistance(p) <= Radius;

        /// <summary>
        /// How much of the zone's full strength this ground gets: 1 anywhere in the core, falling linearly
        /// to 0 at the rim, and 0 outside. What makes a lot's edge a fringe rather than a wall.
        /// </summary>
        public float CoreShare(Vector2 p)
        {
            float d = SpineDistance(p);
            if (d >= Radius) return 0f;

            float core = Mathf.Clamp01(CoreFraction) * Radius;
            if (d <= core) return 1f;

            // Guard the degenerate CoreFraction == 1 case: a lot that is all core has no taper to divide by.
            float taper = Radius - core;
            return taper <= 1e-4f ? 1f : 1f - (d - core) / taper;
        }

        /// <summary>
        /// The share the LOT SCATTER actually rolls against: <see cref="CoreShare"/> <b>squared</b>.
        ///
        /// <para><b>⭐ MEASURED, NOT CHOSEN — and a test caught the linear version.</b> The first draft
        /// rolled against <c>CoreShare</c> directly, and <c>ALotsCoreIsMeasurablyDenserThanItsFringe</c>
        /// reported St Peters' EastWood carrying <b>41.4 trunks per 1000 m² in its core against 41.4 in its
        /// fringe</b> — a taper that did not taper. Two things were flattening it, and both are real:</para>
        ///
        /// <para>(1) <b>The core saturates against <see cref="WoodlandZones.MinTrunkGapMetres"/>.</b> At a
        /// core spacing near the min gap plus jitter, a full-density core rejects a large share of its own
        /// candidates for standing too close to a trunk already placed — so the core cannot get any denser,
        /// while the fringe (sparse enough never to self-collide) keeps everything its roll allows. The
        /// linear roll's 2:1 candidate ratio came out of the far end as 1:1.</para>
        ///
        /// <para>(2) <b>On St Peters the AMBIENT mosaic plants a lot's ground too</b>, uniformly, because it
        /// does not know lots exist — and a uniform term added to both sides drags any ratio toward 1.</para>
        ///
        /// <para>Squaring is the honest fix rather than a fudge: a core at min-gap saturation IS "as dense
        /// as trunks can stand", which is what a closed canopy should be, so the taper has to be bought at
        /// the fringe end. It also matches what a real wood's edge does — thin fast, not evenly.</para>
        /// </summary>
        public float PlantingShare(Vector2 p)
        {
            float s = CoreShare(p);
            return s * s;
        }

        /// <summary>The rectangle that bounds the zone — what a boundedness test measures, and what the
        /// lot scatter walks instead of walking a region.</summary>
        public Rect Bounds => Rect.MinMaxRect(Mathf.Min(From.x, To.x) - Radius,
                                              Mathf.Min(From.y, To.y) - Radius,
                                              Mathf.Max(From.x, To.x) + Radius,
                                              Mathf.Max(From.y, To.y) + Radius);

        /// <summary>Length (m) of the spine — 0 for a circular zone.</summary>
        public float SpineLength => Vector2.Distance(From, To);

        /// <summary>Ground the zone actually covers (m²): a disc plus the swept rectangle. Exact for a
        /// capsule, and the denominator of the <i>"the lots are a small share of the hinterland"</i> law
        /// that keeps a bounded lot from quietly becoming a blanket.</summary>
        public float AreaMetresSquared => Mathf.PI * Radius * Radius + 2f * Radius * SpineLength;

        /// <summary>Do two zones share any ground? Capsule-to-capsule is not a closed form, so this walks
        /// one spine against the other — enough for a table of a dozen rows checked once in a test.</summary>
        public bool Overlaps(WoodlandZone other, int samples = 64)
        {
            float reach = Radius + other.Radius;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                if (StPetersShoreMap.DistanceToSegment(Vector2.Lerp(From, To, t),
                                                       other.From, other.To) <= reach)
                    return true;
            }
            return false;
        }

        public override string ToString() =>
            SpineLength <= 1e-3f
                ? $"{Name} [{Kind}] circle r{Radius:0.#} at ({From.x:0.#},{From.y:0.#})"
                : $"{Name} [{Kind}] lane r{Radius:0.#} ({From.x:0.#},{From.y:0.#})→({To.x:0.#},{To.y:0.#})";
    }

    /// <summary>
    /// <b>THE SHARED WOODLAND MACHINERY</b> — the questions every region's zone table answers, and the
    /// ONE scatter that plants a lot. Region-neutral on purpose: St Peters and Nine Mile Creek disagree
    /// about almost everything (one is a reverting island, the other a farmed coast) but they must not
    /// disagree about what a wood lot IS, or "denser core, thinner fringe" becomes two implementations
    /// and only one of them stays true.
    /// </summary>
    public static class WoodlandZones
    {
        /// <summary>
        /// Is this ground inside any zone that takes trees away — a <see cref="WoodlandZoneKind.Clearing"/>
        /// or a <see cref="WoodlandZoneKind.Corridor"/>?
        ///
        /// <para><b>⭐ THE ONE PREDICATE PLACEMENT ASKS.</b> A region's <c>IsPlantable</c> calls this and
        /// nothing else about zones, which is exactly why the table could take over
        /// <see cref="StPetersGinnyPlot"/>'s clearing without any other line in placement changing.</para>
        /// </summary>
        public static bool IsCleared(IReadOnlyList<WoodlandZone> zones, Vector2 p)
        {
            if (zones == null) return false;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z.Kind == WoodlandZoneKind.Lot) continue;
                if (z.Contains(p)) return true;
            }
            return false;
        }

        /// <summary>Is this ground inside the zone with this name? Asked when a caller means one
        /// particular lot — <see cref="StPetersGinnyPlot.IsInsideClearing"/> means <i>her</i> land, not
        /// <i>any</i> clearing, and answering the general question there would be a bug.</summary>
        public static bool IsInside(IReadOnlyList<WoodlandZone> zones, string name, Vector2 p)
        {
            if (zones == null) return false;
            for (int i = 0; i < zones.Count; i++)
                if (zones[i].Name == name) return zones[i].Contains(p);
            return false;
        }

        /// <summary>The lot this ground is in and how strongly, or a zero share if it is in none. When
        /// lots overlap the STRONGEST wins, so a lot inside a lot reads as one wood.</summary>
        public static float LotShareAt(IReadOnlyList<WoodlandZone> zones, Vector2 p, out string lot)
        {
            lot = null;
            if (zones == null) return 0f;

            float best = 0f;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].Kind != WoodlandZoneKind.Lot) continue;
                float share = zones[i].CoreShare(p);
                if (share <= best) continue;
                best = share;
                lot = zones[i].Name;
            }
            return best;
        }

        /// <summary>Find a row by name, or throw with the table's own contents — a table lookup that
        /// returns a default row silently is how a renamed lot becomes an unplanted one.</summary>
        public static WoodlandZone Require(IReadOnlyList<WoodlandZone> zones, string name)
        {
            if (zones != null)
                for (int i = 0; i < zones.Count; i++)
                    if (zones[i].Name == name) return zones[i];

            var have = new List<string>();
            if (zones != null) foreach (var z in zones) have.Add(z.Name);
            throw new KeyNotFoundException(
                $"no woodland zone named '{name}' — the table declares [{string.Join(", ", have)}]. A " +
                "renamed zone must be renamed at its call site too, or the ground it was holding open " +
                "quietly plants over.");
        }

        /// <summary>
        /// A stable integer for a string, so two zones' scatter rolls cannot correlate. Ordinal and
        /// deliberately simple: <c>string.GetHashCode</c> is not stable across runtimes and this feeds a
        /// scatter that must reproduce (rule 5). The same shape
        /// <see cref="NineMileCreekFields.HashOfLine"/> has always used, which now asks this.
        /// </summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                int h = 17;
                if (s != null) foreach (char c in s) h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }

        // =================================================================================================
        //  THE LOT SCATTER
        // =================================================================================================

        /// <summary>
        /// How close two trunks may stand. Trees pivot at the TRUNK FOOT, so two trunks a metre apart do
        /// not read as two trees — they read as one fat trunk with two crowns, which is the one artefact a
        /// dense stand can produce that a sparse one cannot. Two and a half metres is under the ambient
        /// grain of either region's woods and still wide enough that a stand closes overhead.
        /// </summary>
        public const float MinTrunkGapMetres = 2.5f;

        /// <summary>
        /// Max deterministic offset (m) from a lot's grid, as a fraction of its own spacing.
        ///
        /// <para><b>⚠ 0.30, NOT the 0.44 both regions' sparse scatters use — and the difference is
        /// MEASURED.</b> "Just under half the step, as far as it can go without letting two neighbours swap
        /// places" is the right law for a scatter with no minimum gap, which is what
        /// <see cref="StPetersWoods.TreeJitter"/> and the hedge wander are. A DENSE grid also has
        /// <see cref="MinTrunkGapMetres"/> to satisfy, and at 0.44 the two rules fight: a 3.6 m grid
        /// jittered ±1.58 m puts orthogonal neighbours as close as 0.43 m, so the gap rule was throwing out
        /// <b>46% of a lot's core candidates</b> (measured: 40 trunks per 1000 m² achieved against 77 laid
        /// out). The core could not close, and because the fringe is sparse enough never to self-collide,
        /// the taper flattened at the same time.</para>
        ///
        /// <para>At 0.30 the grid still reads as irregular — ±1.08 m on a 3.6 m step, with four drawn
        /// variants per species mixed through it — while the gap rule goes back to catching genuine
        /// collisions rather than fighting the layout.</para>
        /// </summary>
        public const float LotJitterFraction = 0.30f;

        /// <summary>One tree planted by a lot: where, which species, which drawn individual, and which lot
        /// put it there (so a failing test names a lot rather than a coordinate).</summary>
        public readonly struct LotTreeSite
        {
            public readonly Vector2 Position;
            public readonly string Species;
            public readonly int Variant;
            public readonly string Lot;
            /// <summary>How far into the lot's core this tree stands, 0..1 — carried so a test can prove
            /// the core is genuinely denser than the fringe instead of taking the taper on trust.</summary>
            public readonly float CoreShare;

            public LotTreeSite(Vector2 position, string species, int variant, string lot, float coreShare)
            {
                Position = position; Species = species; Variant = variant; Lot = lot; CoreShare = coreShare;
            }
        }

        /// <summary>
        /// <b>Plant every woodland lot in a table.</b> Pure and deterministic: the grid, the jitter, the
        /// fringe roll, the species and the variant are all stable hashes of a lot's name and a grid index
        /// (no <c>System.Random</c>), so a rebuild reproduces the wood exactly (rule 5).
        ///
        /// <para><b>⭐ IT INFILLS RATHER THAN REPLACING.</b> <paramref name="alreadyStanding"/> is whatever
        /// the region's own pass already planted, and no lot tree lands within
        /// <see cref="MinTrunkGapMetres"/> of one. On St Peters that makes a lot <i>the ambient wood plus
        /// more</i> — the ambient mosaic inside a lot is not disturbed at all, which is why thickening the
        /// island did not have to re-roll its forest. On Nine Mile Creek there is nothing ambient to
        /// infill, so a lot is the whole wood.</para>
        ///
        /// <para><b>The region supplies its own ground rules and its own habitat.</b>
        /// <paramref name="isPlantable"/>, <paramref name="exposureAt"/> and <paramref name="wetnessAt"/>
        /// are the region's — a lot may not plant on a road, a quay or a dooryard just because a table
        /// says "wood here", and the species that grows in a salt wind is the region's own exposure
        /// field's business. The species PREFERENCE itself is
        /// <see cref="StPetersWoods.SpeciesPreference"/> for both regions, because two regions on one
        /// coast must not disagree about which tree takes salt.</para>
        /// </summary>
        public static List<LotTreeSite> ScatterLots(IReadOnlyList<WoodlandZone> zones,
                                                    ITidalTerrain terrain,
                                                    System.Func<Vector2, bool> isPlantable,
                                                    System.Func<Vector2, float> exposureAt,
                                                    System.Func<Vector2, float> wetnessAt,
                                                    IReadOnlyCollection<string> available,
                                                    System.Func<string, int> variantsFor,
                                                    IEnumerable<Vector2> alreadyStanding)
        {
            var sites = new List<LotTreeSite>();
            if (zones == null || terrain == null || isPlantable == null) return sites;
            if (available == null || available.Count == 0) return sites;

            // ⚠ ACCUMULATED ACROSS LOTS, not rebuilt per lot. The first draft seeded each lot's neighbour
            // set from `alreadyStanding` alone, so two lots that shared any ground could each plant a trunk
            // inside the other's. No table declares overlapping lots today (a test forbids it), which is
            // exactly why this was invisible — and exactly why it is worth fixing rather than relying on
            // that test to stay true of a fourth lot somebody adds later.
            var placed = new List<Vector2>();
            if (alreadyStanding != null) placed.AddRange(alreadyStanding);

            for (int z = 0; z < zones.Count; z++)
            {
                var lot = zones[z];
                if (lot.Kind != WoodlandZoneKind.Lot) continue;
                if (lot.CoreSpacingMetres <= 0.01f) continue;

                // Only the trunks that could possibly be in the way — the min-gap test is O(candidates ×
                // neighbours) and a region's whole ambient forest is the wrong second factor.
                var neighbours = new List<Vector2>();
                Rect reach = Grow(lot.Bounds, MinTrunkGapMetres);
                foreach (Vector2 p in placed)
                    if (reach.Contains(p)) neighbours.Add(p);

                int salt = StableHash(lot.Name);
                float step = lot.CoreSpacingMetres;
                float jitter = step * LotJitterFraction;

                Rect bounds = lot.Bounds;
                int nx = Mathf.Max(1, Mathf.CeilToInt(bounds.width / step));
                int ny = Mathf.Max(1, Mathf.CeilToInt(bounds.height / step));

                for (int ix = 0; ix < nx; ix++)
                for (int iy = 0; iy < ny; iy++)
                {
                    float cx = bounds.xMin + (ix + 0.5f) * step
                               + (StPetersShoreMap.Hash01(ix, iy, salt ^ 0x5f3a) * 2f - 1f) * jitter;
                    float cy = bounds.yMin + (iy + 0.5f) * step
                               + (StPetersShoreMap.Hash01(ix, iy, salt ^ 0x2b91) * 2f - 1f) * jitter;
                    var p = new Vector2(cx, cy);

                    // THE FRINGE. Full density in the core, thinning to nothing at the rim — the edge that
                    // reads. Cheapest gate first: it throws out the corners of the bounding rectangle,
                    // which are always outside the capsule, before any terrain read.
                    //
                    // ⚠ PlantingShare, not CoreShare — see its own remarks for the measurement that forced
                    // the difference. CoreShare is the geometric fact and is what a site REPORTS; the roll
                    // uses its square, because a min-gap-saturated core cannot be made denser and the taper
                    // therefore has to be bought at the fringe.
                    float share = lot.CoreShare(p);
                    if (share <= 0f) continue;
                    if (StPetersShoreMap.Hash01(ix, iy, salt ^ 0x7d19) > lot.PlantingShare(p)) continue;

                    // ⚠ The region's OWN ground rules, and a cleared zone beats a lot: a corridor cut
                    // through this very wood, or a dooryard inside it, must stay cut. IsCleared is asked
                    // by the region's isPlantable, so this is one call and not two opinions.
                    if (!isPlantable(p)) continue;

                    if (TooNear(neighbours, p)) continue;

                    string species = StPetersWoods.PickSpecies(
                        StPetersWoods.SpeciesPreference(exposureAt != null ? exposureAt(p) : 0.5f,
                                                       wetnessAt != null ? wetnessAt(p) : 0.5f),
                        available, StPetersShoreMap.Hash01(ix, iy, salt ^ 0x11c7));
                    if (species == null) continue;

                    int variants = Mathf.Max(1, variantsFor != null ? variantsFor(species) : 1);
                    int variant = Mathf.Min(variants - 1,
                        (int)(StPetersShoreMap.Hash01(ix, iy, salt ^ 0x3ae5) * variants));

                    sites.Add(new LotTreeSite(p, species, variant, lot.Name, share));

                    // ⚠ ADDED TO THE NEIGHBOUR SET, not just compared against it — a lot must not plant
                    // two trunks on the same spot either. Without this the min-gap rule only held against
                    // the ambient wood, which is the half of the problem a dense lot does NOT have.
                    neighbours.Add(p);
                    placed.Add(p);          // …and carried to the NEXT lot, per the note above
                }
            }
            return sites;
        }

        static bool TooNear(List<Vector2> neighbours, Vector2 p)
        {
            const float gap2 = MinTrunkGapMetres * MinTrunkGapMetres;
            for (int i = 0; i < neighbours.Count; i++)
                if ((neighbours[i] - p).sqrMagnitude < gap2) return true;
            return false;
        }

        static Rect Grow(Rect r, float by) =>
            Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);
    }
}
#endif
