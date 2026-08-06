using System;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>What a walkable area IS — the sidecar section it came from.</summary>
    public enum DeckAreaKind
    {
        /// <summary>A <c>DECK</c> polygon: the working sole/foredeck the player walks freely.</summary>
        Deck = 0,
        /// <summary>A <c>WASHBOARD</c> strip: the side deck you CLIMB onto (Space — M2-37's boarding
        /// move, not built here). Carried as data so the climb has bounds to land in; deliberately NOT
        /// part of the free-walk area until that verb exists.</summary>
        Washboard = 1,
    }

    /// <summary>
    /// One walkable polygon in HULL-LOCAL METRES (origin amidships / keel bottom / centreline;
    /// +x starboard, +y bow, +z up — the sidecars' frame, `docs/art/rigs/gameplay/README.md`),
    /// with the cheap runtime form baked alongside it.
    ///
    /// <para><b>Everything here is imported, never typed.</b> <see cref="Outline"/> is the sidecar's
    /// own <c>polygon</c>/<c>polygon3d</c> vertex list; <see cref="HeightPlane"/> and
    /// <see cref="Bounds"/> are computed FROM it at import. Editing these by hand re-opens exactly the
    /// failure the baked-anchors JSON died of (a hand-copied constant drifting from the rig) — re-run
    /// <c>Hidden Harbours ▸ Dev ▸ Boats ▸ Import deck sidecars</c> instead.</para>
    /// </summary>
    [Serializable]
    public sealed class DeckArea
    {
        [Tooltip("The sidecar's own area id — 'cockpit_sole', 'foredeck', 'washboard_port'.")]
        public string Id = "";

        [Tooltip("DECK (walk freely) or WASHBOARD (climb onto — M2-37's Space move).")]
        public DeckAreaKind Kind = DeckAreaKind.Deck;

        [Tooltip("The polygon in hull-local metres, x abeam (+starboard) / y along the keel (+bow), " +
                 "counter-clockwise from above. Imported verbatim from the sidecar.")]
        public Vector2[] Outline = Array.Empty<Vector2>();

        [Tooltip("The area's height as a fitted PLANE: z = x·HeightPlane.x + y·HeightPlane.y + " +
                 "HeightPlane.z (hull-local metres). A flat 'polygon' + 'z' area fits exactly (x,y = 0); " +
                 "a sheer-following 'polygon3d' area fits to within HeightResidualMax. A plane is O(1) " +
                 "per tick where interpolating the authored triples would not be (rule 7).")]
        public Vector3 HeightPlane = Vector3.zero;

        [Tooltip("Worst |authored z − fitted z| over this area's own vertices (m) — the honest error bar " +
                 "on HeightPlane. Reported by the importer; a big number means the deck is not a plane " +
                 "and this area wants a real height field, not a tighter fit.")]
        public float HeightResidualMax;

        [Tooltip("Baked axis-aligned bounds in the hull frame: (minX, minY, maxX, maxY). The cheap " +
                 "reject before the point-in-polygon test.")]
        public Vector4 Bounds = Vector4.zero;

        /// <summary>True when this area has enough vertices to bound anything.</summary>
        public bool IsUsable() => Outline != null && Outline.Length >= 3;

        /// <summary>
        /// Bake an area from authored hull-local (x, y, z) vertices: the plan outline, the fitted
        /// height plane and its error bar, and the AABB the per-tick containment test rejects against.
        /// <b>The one place an area is built</b> — the sidecar importer and the tests both come through
        /// here, so a test can never be pinning a bake the importer does not perform.
        /// </summary>
        public static DeckArea From(string id, DeckAreaKind kind, Vector3[] hullVertices)
        {
            Vector3[] verts = hullVertices ?? Array.Empty<Vector3>();
            var outline = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++) outline[i] = new Vector2(verts[i].x, verts[i].y);

            Vector3 plane = DeckAreaMath.FitHeightPlane(verts);
            return new DeckArea
            {
                Id = id ?? "",
                Kind = kind,
                Outline = outline,
                HeightPlane = plane,
                HeightResidualMax = DeckAreaMath.HeightPlaneResidualMax(verts, plane),
                Bounds = DeckAreaMath.BoundsOf(outline),
            };
        }
    }

    /// <summary>A named tie-off point in hull-local metres — the sidecars' <c>CLEATS</c>. Carried
    /// through as data for M2-38 (ropes); nothing consumes it yet, by design.</summary>
    [Serializable]
    public sealed class DeckCleat
    {
        [Tooltip("The sidecar's id: bow_1, stern_port, quarter_star…")]
        public string Id = "";
        [Tooltip("The fitting the rig actually models: cleat, samson_post, bow_bitt, bollard, bitt, painter.")]
        public string Type = "";
        [Tooltip("The modelled fitting's box centre, hull-local metres (+x starboard, +y bow, +z up).")]
        public Vector3 PositionMeters = Vector3.zero;
    }

    /// <summary>
    /// <b>Where you can stand on THIS hull — as data, from the rig (ADR 0003, rule 2).</b> One of these
    /// per hull rig, IMPORTED from that rig's <c>docs/art/rigs/gameplay/&lt;rig&gt;.gameplay.json</c>
    /// sidecar by <c>GameplaySidecarImporter</c> and pointed at by <see cref="BoatVisualDef.Deck"/>.
    /// <c>DeckWalkController</c> clamps the on-deck player to
    /// <see cref="Areas"/> instead of the one-size greybox rectangle it used before.
    ///
    /// <para><b>Do not author this asset by hand.</b> The design ruling this slice executes
    /// (`docs/design/deck-boarding-cleats-and-interact-capture.md` §2) is explicit that the extractor
    /// must carry the polygons straight through with <b>no human copy step</b> — the baked-anchors JSON
    /// became dead code precisely because the runtime read a hand-transcribed constant instead. The
    /// importer overwrites every field below on each run; anything typed in here is lost, which is the
    /// point.</para>
    ///
    /// <para><b>Absence is data, at two levels</b> (the sidecar contract's own rule). A hull with no
    /// sidecar has no <see cref="BoatVisualDef.Deck"/> at all and keeps the greybox rectangle — she has
    /// not been measured, and a rectangle is a better answer than no deck. A hull WITH a sidecar but no
    /// <c>WASHBOARD</c> section simply grows no <see cref="DeckAreaKind.Washboard"/> areas: an open boat
    /// never offers the climb. Neither is an error.</para>
    ///
    /// <para><b>The frame is the rig's, not the screen's.</b> Every number here is hull-local metres and
    /// heading-independent. Projecting them onto the drawn hull — which needs that artwork's own iso
    /// foreshortening — is <see cref="DeckAreaMath"/>'s job, per artwork, never baked in here.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Boat Deck", fileName = "BoatDeck")]
    public sealed class BoatDeckDef : ScriptableObject
    {
        [Header("Identity (imported — see the class doc before editing)")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): deck.<rig_basename>, e.g. deck.lobster_boat_iso.")]
        public string Id = "";

        [Header("Provenance (imported)")]
        [Tooltip("The sidecar these polygons were read from, repo-relative.")]
        public string SourceSidecar = "";
        [Tooltip("The rig the sidecar was derived from, repo-relative.")]
        public string SourceRig = "";
        [Tooltip("The sidecar's derivedFromRigSha256 at import time. The importer refuses a sidecar whose " +
                 "rig no longer hashes to this (the staleness rule) — this field records what it matched.")]
        public string DerivedFromRigSha256 = "";
        [Tooltip("The hull's LOA in metres as the sidecar states it. Provenance/inspection only — nothing " +
                 "reads it for gameplay.")]
        public float LoaMeters;

        [Header("Geometry (imported)")]
        [Tooltip("Every walkable area on this hull, DECK and WASHBOARD alike, in hull-local metres.")]
        public DeckArea[] Areas = Array.Empty<DeckArea>();

        [Tooltip("The hull's named tie-off points (M2-38 ropes). Data only — nothing consumes it yet.")]
        public DeckCleat[] Cleats = Array.Empty<DeckCleat>();

        [Header("Walkable bounds (baked from the DECK areas)")]
        [Tooltip("Centre of the axis-aligned box enclosing every DECK area, in the deck frame. This is " +
                 "what DeckStance publishes as DeckCenter, so consumers that grade against a rectangle " +
                 "(the fight's deck-angle term) grade against THIS hull's box instead of the dory's.")]
        public Vector2 WalkCenter = Vector2.zero;
        [Tooltip("Half-extents of that box (m; x = half-beam of the walk area, y = half its length).")]
        public Vector2 WalkHalfExtents = Vector2.zero;

        /// <summary>True when at least one <see cref="DeckAreaKind.Deck"/> area is usable — the gate
        /// <c>DeckWalkController</c> takes the polygon path behind. False
        /// falls back to the greybox rectangle, loudly in neither direction: absence is data.</summary>
        public bool HasWalkableDeck()
        {
            if (Areas == null) return false;
            for (int i = 0; i < Areas.Length; i++)
                if (Areas[i] != null && Areas[i].Kind == DeckAreaKind.Deck && Areas[i].IsUsable())
                    return true;
            return false;
        }

        /// <summary>True when this hull offers washboards to climb onto (M2-37's Space move). False on
        /// every open boat — and that is the DATA saying so, not a missing import.</summary>
        public bool HasWashboards()
        {
            if (Areas == null) return false;
            for (int i = 0; i < Areas.Length; i++)
                if (Areas[i] != null && Areas[i].Kind == DeckAreaKind.Washboard && Areas[i].IsUsable())
                    return true;
            return false;
        }

        /// <summary>
        /// Clamp a DECK-FRAME point (hull-local metres, x abeam / y toward the bow) onto this hull's
        /// walkable area, and report which area holds it and how high that area stands.
        ///
        /// <para><b>Allocation-free and cheap enough for Update (rule 7).</b> The hint is the area the
        /// caller was standing in last tick: it is tested first, and an inside hit costs one AABB reject
        /// plus one crossing test and returns. The all-areas sweep only runs on a genuine excursion —
        /// which is a player pressing into a rail, not a per-frame cost on a hull they are standing
        /// well inside of.</para>
        /// </summary>
        /// <param name="deckPoint">The candidate position, deck frame (m).</param>
        /// <param name="areaHint">In: the area index to try first (−1 = none). Out: the area the result
        /// lies in or on. Pass the same variable back each tick.</param>
        /// <param name="heightMeters">The clamped point's height above the keel (m), from that area's
        /// fitted plane — this is what lifts the player up-screen onto a raised foredeck.</param>
        /// <param name="includeWashboards">Whether the side decks count as standable. FALSE is what the
        /// walk uses: a washboard is somewhere you CLIMB onto, and that verb (Space) is M2-37's other
        /// half. The parameter exists so the climb can turn it on without re-deriving anything.</param>
        public Vector2 ClampToWalkable(Vector2 deckPoint, ref int areaHint, out float heightMeters,
                                       bool includeWashboards = false)
        {
            heightMeters = 0f;
            if (Areas == null || Areas.Length == 0) return deckPoint;

            // (1) The hinted area, first and usually last: standing still on a deck is the common case.
            if (areaHint >= 0 && areaHint < Areas.Length)
            {
                DeckArea hinted = Areas[areaHint];
                if (Accepts(hinted, includeWashboards) && DeckAreaMath.Contains(hinted.Outline, hinted.Bounds, deckPoint))
                {
                    heightMeters = DeckAreaMath.HeightAt(hinted.HeightPlane, deckPoint);
                    return deckPoint;
                }
            }

            // (2) Any OTHER area that contains it — stepping from the cockpit onto the foredeck.
            for (int i = 0; i < Areas.Length; i++)
            {
                if (i == areaHint) continue;
                DeckArea a = Areas[i];
                if (!Accepts(a, includeWashboards) || !DeckAreaMath.Contains(a.Outline, a.Bounds, deckPoint)) continue;
                areaHint = i;
                heightMeters = DeckAreaMath.HeightAt(a.HeightPlane, deckPoint);
                return deckPoint;
            }

            // (3) Outside every area: the nearest point on any of their outlines. Nearest ACROSS all of
            // them, not within the hinted one — walking off the foredeck's forward edge should put you
            // back on the foredeck, but stepping off its aft edge should put you in the cockpit.
            float bestSqr = float.PositiveInfinity;
            Vector2 best = deckPoint;
            int bestArea = -1;
            for (int i = 0; i < Areas.Length; i++)
            {
                DeckArea a = Areas[i];
                if (!Accepts(a, includeWashboards)) continue;
                Vector2 p = DeckAreaMath.ClosestPointOnOutline(a.Outline, deckPoint, out float sqr);
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = p;
                bestArea = i;
            }

            if (bestArea < 0) return deckPoint;      // nothing of the requested kind — leave it alone
            areaHint = bestArea;
            heightMeters = DeckAreaMath.HeightAt(Areas[bestArea].HeightPlane, best);
            return best;
        }

        /// <summary>The height (m above the keel) of the area at <paramref name="areaIndex"/> under
        /// <paramref name="deckPoint"/>; 0 for an index that is not an area.</summary>
        public float HeightAt(int areaIndex, Vector2 deckPoint)
            => (Areas != null && areaIndex >= 0 && areaIndex < Areas.Length && Areas[areaIndex] != null)
                ? DeckAreaMath.HeightAt(Areas[areaIndex].HeightPlane, deckPoint)
                : 0f;

        private static bool Accepts(DeckArea a, bool includeWashboards)
            => a != null && a.IsUsable() &&
               (a.Kind == DeckAreaKind.Deck || (includeWashboards && a.Kind == DeckAreaKind.Washboard));
    }
}
