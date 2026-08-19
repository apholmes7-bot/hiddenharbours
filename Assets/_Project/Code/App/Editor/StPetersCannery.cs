#if UNITY_EDITOR
using UnityEngine;
using HiddenHarbours.Art;                 // YSortSprite
using HiddenHarbours.Art.Editor;          // VillageBuildingCatalog, VillageBuildingKit
using HiddenHarbours.Core;                // ITidalTerrain

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE ST PETERS CANNERY</b> — the fish plant that shut when the business went to the mainland,
    /// standing derelict on the shore inside the wharf, and the biggest building on the island.
    ///
    /// <para><b>What it is for.</b> The owner ruled on 2026-08-19 that St Peters once had a thriving
    /// cannery, that it closed when the work moved away, and that <b>getting it running again and
    /// employing the village is a long-arc player goal</b> — P2 Dory to Dynasty, P3 Living Working
    /// Coast, P4 Earn It Then Automate It, in one building. <b>This file places the building only.</b>
    /// The restart is an economy-sim arc and it is M3: it is logged in
    /// <c>backlog/building-lifecycle-gameplay.md</c> and there is deliberately no def, no interaction and no
    /// save field for it here (CLAUDE.md rule 8).</para>
    ///
    /// <para><b>⭐ WHY A DERELICT BUILDING IS WORTH BUILDING BEFORE ITS GAMEPLAY IS.</b> The village's
    /// history is currently something the design documents know and the island does not. A closed
    /// cannery is the one piece of set dressing that states the whole premise without a line of
    /// dialogue: there was more work here once, and there could be again. It also gives the arc its art
    /// dependency up front — the lifecycle ladder runs both ways, so the day the restart is built, every
    /// rung from <c>collapsing</c> back to <c>finished</c> is a sheet the kit already knows how to
    /// bake.</para>
    ///
    /// <para><b>⚠ THE SITE IS A PROPOSAL FOR THE OWNER'S WALK</b>, not a ruling — but every number in it
    /// is measured. See <see cref="Site"/>.</para>
    /// </summary>
    public static class StPetersCannery
    {
        /// <summary>The GameObject the cannery hangs off, so a rebuild can find and replace it.</summary>
        public const string RootName = "StPetersCannery";

        /// <summary>Which of the kit's builds it IS — the <c>cannery</c> preset at
        /// <c>decay: 'collapsing'</c>. See <c>VillageBuildingKit.LifecycleSet</c> for why that rung and
        /// not <c>ruin</c>.</summary>
        public const string BuildKey = "stPetersCannery";

        /// <summary>
        /// <b>(170, 16) — on the shore inside the pier, on the north side of the approach.</b>
        ///
        /// <para><b>A cannery loads boats, so it wants wharf frontage</b>, and St Peters has exactly one
        /// working waterfront: <c>StPetersWharf</c>, rooted at cell x = 183 on the island's east end.
        /// This site is <b>20.6 m</b> from that root — close enough that the pier is the building's yard,
        /// far enough that it stands beside the deck rather than on it (the deck occupies
        /// x ∈ [183, 213], y ∈ [−3, 3]).</para>
        ///
        /// <para><b>Why NOT west of the village, which is where the first sketch put it.</b> The village
        /// sits at x ≈ 0–25 and its own shore is the south beach; there is no pier there and no dredged
        /// water. A cannery on a beach is a cannery its boats cannot reach — the same "wet at every
        /// station and still nowhere" mistake the Nine Mile Creek channel taught. The building goes where
        /// the working water is, and the working water is the east berth.</para>
        ///
        /// <para><b>Why it is dry, and why the margin is not thin.</b> The island plateau is a flat 6 m
        /// anywhere inside the ellipse (centre (70, 0), semi-axes 120 × 70) against a 2.2 m spring high;
        /// (170, 16) is comfortably inside it (elliptical distance 0.86 of 1). It is also clear of the
        /// two dredged cuts that pull the ground down near the pier: the berth carve runs along y = 0
        /// with a half-width of 8 m and the approach's shoulder dies at x = 198, and this site is 25.6 m
        /// from the nearer of them. That matters — the pier root itself measures +5.35 m and two cells
        /// further east it is already +3.15 m. <b>Asserted against the authored terrain rather than
        /// trusted to this paragraph</b>, in <see cref="Place"/> and again in the tests.</para>
        ///
        /// <para><b>And why nothing else is there.</b> The three woodland lots are at (52,−42), (128,−30)
        /// and (126,36); Ginny's plot is at (84,28) with a 20 m clearing; the nearest is 46 m away. The
        /// site also already falls inside the wharf's 30 m dock clearance, so no tree plants there — but
        /// that is incidental, and incidental is how a clearing gets forgotten, so
        /// <c>StPetersWoodlandZones</c> declares it explicitly.</para>
        /// </summary>
        public static readonly Vector2 Site = new Vector2(170f, 16f);

        /// <summary>
        /// Where its doors look: at the pier root. A fish plant faces the water it is fed from, and the
        /// facing is DERIVED from the bake's own per-facing door anchors rather than given as an index —
        /// so a re-bake at a different facing count re-derives the nearest cell instead of turning the
        /// building's blank gable to the sea.
        /// </summary>
        public static Vector2 Approach => new Vector2(StPetersWharf.RootCellX + 0.5f, 0f);

        /// <summary>
        /// The ground the cannery reserves at any facing, from the bake contract — the half-diagonal
        /// circle every building in this world is spaced by. 0 when the build is not baked, so a caller
        /// can say so rather than reserve a guess.
        /// </summary>
        public static float FootprintRadiusMetres =>
            StPetersVillage.FootprintRadiusMetres(VillageBuildingCatalog.Find(BuildKey));

        /// <summary>
        /// The clearing the cannery needs around <see cref="Site"/> — its own reach plus a walking gap,
        /// so the woods do not close over the one road in. Falls back to the rig's measured 9.54 × 15.40 m
        /// footprint when the kit is unbaked, so the woodland zone is never a zero-radius no-op.
        /// </summary>
        public static float ClearingRadiusMetres
        {
            get
            {
                float reach = FootprintRadiusMetres;
                if (reach <= 0f) reach = UnbakedFootprintRadiusMetres;
                return reach + WalkingGapMetres;
            }
        }

        /// <summary>Half-diagonal of the cannery preset's own 9.54 × 15.40 m footprint, measured off the
        /// rig in the standalone V8 harness. Only used when the contract is missing.</summary>
        public const float UnbakedFootprintRadiusMetres = 9.06f;

        /// <summary>Room to walk round the building without brushing a trunk. The same order as the
        /// village's own margins; not tuned by eye against the trees, because the site already sits
        /// inside the wharf's dock clearance and this is the belt to that pair of braces.</summary>
        public const float WalkingGapMetres = 4f;

        /// <summary>
        /// Stand the cannery up: the kit's ONE build path, so a cannery the owner drags in and this one
        /// are the same object. Returns true when a building was placed.
        ///
        /// <para>No occupant and no interior: the lifecycle pass draws no interior at any state, a ruin
        /// has no room to bake, and the restart arc that would want one is M3. A greybox marker is stood
        /// instead when the build is not baked, on the same convention the sheds use — a fresh clone
        /// still gets a building-shaped thing on the shore rather than a hole in the world.</para>
        /// </summary>
        public static bool Place(Transform parent, ITidalTerrain terrain, Sprite greybox)
        {
            // ⚠️ Checked against the AUTHORED TERRAIN, not against the constants above — the #345 lesson
            // that cost the village three wrong sites at once, and the one the pier root's +5.35 m vs
            // +3.15 m two cells east makes expensive here in particular.
            AssertDry(terrain, Site);

            var go = new GameObject(RootName);
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = new Vector3(Site.x, Site.y, 0f);

            var placement = VillageBuildingCatalog.Find(BuildKey);
            Sprite art = placement.IsValid
                ? VillageBuildingCatalog.LoadFacing(
                      placement, StPetersVillage.FacingToward(placement.Entry, Site, Approach))
                : null;

            if (art != null)
            {
                VillageBuildingCatalog.Configure(go, placement, art);

                Debug.Log(
                    $"[StPetersCannery] the cannery stands at ({Site.x:0.#},{Site.y:0.#}) — " +
                    $"{placement.Entry.footprintWidthMetres:0.0} × " +
                    $"{placement.Entry.footprintLengthMetres:0.0} m in state " +
                    $"'{placement.Entry.state}', {Vector2.Distance(Site, Approach):0.0} m from the pier " +
                    $"root, reserving {FootprintRadiusMetres:0.0} m of ground. Scenery: the restart is " +
                    "backlog/building-lifecycle-gameplay.md (M3).");
                return true;
            }

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = greybox;
            sr.color = new Color(0.38f, 0.36f, 0.34f);   // rusted corrugated, read as unpainted metal
            sr.sortingOrder = 2;                          // pre-Play seed; the YSortSprite below OWNS it
            go.transform.localScale = new Vector3(9.54f, 15.40f, 1f);
            go.AddComponent<YSortSprite>();

            Debug.LogWarning(
                $"[StPetersCannery] '{BuildKey}' has no baked art, so the cannery is a greybox marker. " +
                "Re-bake (Hidden Harbours ▸ Art ▸ Bake Village Buildings) and re-slice.");
            return true;
        }

        /// <summary>Ground check — see the call site. Silent when there is no terrain to ask, which is
        /// what the pure EditMode fixtures do.</summary>
        static void AssertDry(ITidalTerrain terrain, Vector2 p)
        {
            if (terrain == null) return;

            float ground = terrain.ElevationAt(p);
            float springHigh = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;
            if (ground <= springHigh)
                Debug.LogError(
                    $"[StPetersCannery] the cannery is sited at {p} where the ground is {ground:0.00} m " +
                    $"— at or below spring high water ({springHigh:0.00} m). The two dredged cuts near " +
                    "the pier pull the ground down a long way inland of where they look like they stop " +
                    "(the root measures +5.35 m and two cells east is +3.15 m), so this is the check " +
                    "that catches a site chosen from a map. Move the site; do not lower the tide.");
        }
    }
}
#endif
