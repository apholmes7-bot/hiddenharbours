using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// THE CATCH ON THE DECK (owner's diegetic physical-inventory vision, first slice): a fish tray at a
    /// fixed spot on the deck whose sprite steps through FILL STATES as the hold fills — band a keeper
    /// and the tray visibly gains; sell at the wharf and it empties. No HUD, no counter: the tray IS the
    /// "how full am I?" readout (P2 — the catch you earned sits there looking earned).
    ///
    /// <para><b>A pure read of the hold.</b> This is presentation over today's systems, NOT a container
    /// inventory: the catch still lands through the unchanged <see cref="IHold.TryAdd"/> +
    /// <see cref="FishCaught"/> path and leaves through the market's <see cref="IHold.Clear"/> +
    /// <see cref="CatchSold"/>. Nothing here gates gameplay, owns items, or saves. Which container a
    /// hull carries (tray → the M2 blue totes) and where it sits are hull DATA
    /// (<see cref="BoatHullDef.DeckContainer"/> / <see cref="BoatHullDef.DeckContainerOffset"/>).</para>
    ///
    /// <para><b>Riding the drawn facing.</b> The visible hull is the snap-directional PICTURE: the
    /// physics body yaws continuously, but the player sees one of 8 pre-drawn facings, screen-aligned
    /// (<see cref="DirectionalBoatSprite"/> — whose child renderer is stomped to identity every
    /// LateUpdate, so nothing may anchor to it). This component therefore lives on the PHYSICS ROOT and
    /// places the tray itself: the anchor is authored in the DECK FRAME (x abeam toward starboard, y
    /// along the keel toward the bow) and rotated by <see cref="DirectionalBoatSprite.DrawnHeadingDegrees"/>
    /// each LateUpdate — exactly the frame the deck-walk clamp uses — so when the picture snaps N→NE the
    /// tray JUMPS with it and stays on the same spot of the PICTURED deck (the starboard quarter stays
    /// the starboard quarter at every heading). The tray sprite itself never rotates (screen-upright,
    /// the DirectionalBoatSprite convention). A hull without facing art rides the true heading.</para>
    ///
    /// <para><b>Seams + budget (rules 4/7).</b> Spawned at runtime by its same-module sibling
    /// <see cref="ShipHold"/> (no builder re-run needed; boats without a hold get no tray). Cross-module
    /// input is Core-only: the fill refreshes on <see cref="FishCaught"/> / <see cref="CatchSold"/> /
    /// <see cref="GameLoaded"/> / <see cref="BoatPurchased"/> — event-driven sprite swaps, never a
    /// per-frame poll. LateUpdate only repositions (plus a no-alloc hull ref-compare that catches a
    /// hull swap regardless of bus subscription order). Greybox sprites are code-built once and cached
    /// static; the owner's painted fill states drop into the Def with zero code change.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeckContainerPresenter : MonoBehaviour
    {
        // Draw-order constant, not balance: above the water/rope band and the hull facing (1), below the
        // worked deck pot (51) and its animals (52) so a pot landed near the tray reads on top of it.
        private const int ContainerSortingOrder = 50;
        // Greybox-art constants, not balance numbers (the PotDeckWorkController silhouette convention) —
        // the pixels-per-unit of the code-built tray states and how many states the built set has. The
        // owner's painted sprites replace both via DeckContainerDef.FillSprites.
        private const float SilhouettePixelsPerUnit = 24f;
        private const int GreyboxStateCount = 4;

        private ShipHold _hold;                       // same-module sibling on the physics root
        private IBoatHullPresenter _hull;             // the drawn-facing read via the seam (null = smooth hull)
        private SpriteRenderer _renderer;             // the tray child (created once, reused)
        private BoatHullDef _lastHull;                // ref-compare guard so a hull swap re-reads the Def

        // ---- pure logic (unit-testable, deterministic) --------------------------------------

        /// <summary>
        /// Map hold fullness onto a fill-state index in [0, <paramref name="stateCount"/>): the EMPTY
        /// state (0) is pinned to an empty hold, the BRIM state (count-1) to a full one, and every
        /// partial fill spreads linearly across the interior states — so one banded keeper always shows
        /// (never reads empty) and only a truly full hold reads brim. With only two states of art, any
        /// partial reads as the fuller state. Degenerate inputs (no capacity, no states) fall to 0.
        /// Pure + static + deterministic (no engine state, no allocation).
        /// </summary>
        public static int FillStateIndex(int usedUnits, int capacityUnits, int stateCount)
        {
            if (stateCount <= 1) return 0;
            if (usedUnits <= 0 || capacityUnits <= 0) return 0;
            if (usedUnits >= capacityUnits) return stateCount - 1;
            if (stateCount == 2) return 1;   // only empty/brim art: anything aboard reads as the fuller state
            float fraction = (float)usedUnits / capacityUnits;
            int idx = 1 + (int)(fraction * (stateCount - 2));
            return Mathf.Min(idx, stateCount - 2);   // partials never touch the pinned brim state
        }

        /// <summary>
        /// A DECK-FRAME offset (x abeam toward starboard, y along the keel toward the bow) expressed as a
        /// boat-relative WORLD offset, for a hull drawn at compass heading
        /// <paramref name="drawnHeadingDeg"/> (0 = North/up, 90 = East, clockwise — the project's bearing
        /// convention). Mirrors the deck-walk clamp's <c>DeckFrameToWorld</c> (Player lane — Boats cannot
        /// reference it; an EditMode parity test keeps the two from drifting). Pure + static.
        /// </summary>
        public static Vector2 DeckOffsetToWorld(Vector2 deckOffset, float drawnHeadingDeg)
        {
            float rad = drawnHeadingDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(deckOffset.x * cos + deckOffset.y * sin,
                               -deckOffset.x * sin + deckOffset.y * cos);
        }

        // ---- lifecycle ------------------------------------------------------------------------

        private void Awake()
        {
            _hold = GetComponent<ShipHold>();
            // The drawn-facing read goes through the presenter seam (ADR 0022 phase 4), resolved like
            // the deck-walk clamp does; a boat without one rides its true heading (smooth hull).
            _hull = BoatHullPresenterHost.Resolve(gameObject);
        }

        private void OnEnable()
        {
            // The four edges the hold's contents (or its hull) can change on — all Core, no polling:
            // a landed catch, a sale, a load-restore, and a bought hull (capacity + container change).
            EventBus.Subscribe<FishCaught>(OnFishCaught);
            EventBus.Subscribe<CatchSold>(OnCatchSold);
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
            EventBus.Subscribe<BoatPurchased>(OnBoatPurchased);
            Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<FishCaught>(OnFishCaught);
            EventBus.Unsubscribe<CatchSold>(OnCatchSold);
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
            EventBus.Unsubscribe<BoatPurchased>(OnBoatPurchased);
        }

        private void OnFishCaught(FishCaught _) => Refresh();
        private void OnCatchSold(CatchSold _) => Refresh();
        private void OnGameLoaded(GameLoaded _) => Refresh();
        private void OnBoatPurchased(BoatPurchased _) => Refresh();

        private void LateUpdate()
        {
            // A hull swap can outrun the bus (subscription order) or bypass it entirely (a direct
            // SetHull) — a reference compare per frame costs nothing and keeps the tray honest.
            BoatHullDef hull = _hold != null ? _hold.Hull : null;
            if (!ReferenceEquals(hull, _lastHull)) Refresh();
            if (_renderer == null || !_renderer.enabled) return;

            // Ride the physics root, sit on the PICTURED deck: the deck-frame anchor rotated by the
            // drawn heading (snapped for a sprite compass, continuous for a mesh hull), the sprite
            // held screen-upright — the deck-walk convention. Read through the live host so a hull
            // swapped under us (the dev picker) is never read through a stale presenter.
            var host = GetComponent<BoatHullPresenterHost>();
            var hull = (host != null && host.Presenter != null) ? host.Presenter : _hull;
            float heading = hull != null
                ? hull.DrawnHeadingDegrees()
                : DirectionalBoatSprite.HeadingDegreesFromBow(transform.up);
            _renderer.transform.position =
                (Vector2)transform.position + DeckOffsetToWorld(_lastHull.DeckContainerOffset, heading);
            if (_renderer.transform.rotation != Quaternion.identity)
                _renderer.transform.rotation = Quaternion.identity;
        }

        // ---- the fill read (event-time only) ----------------------------------------------------

        /// <summary>Re-read the hold and show the matching fill state. Event-time only (plus the hull
        /// ref-compare edge) — never a per-frame poll. Public so tests/tools can force a re-read.</summary>
        public void Refresh()
        {
            BoatHullDef hull = _hold != null ? _hold.Hull : null;
            _lastHull = hull;
            DeckContainerDef def = hull != null ? hull.DeckContainer : null;
            if (def == null)
            {
                if (_renderer != null) _renderer.enabled = false;   // no container on this hull → no prop
                return;
            }

            EnsureRenderer();
            Sprite s = SpriteForFill(def, _hold.UsedUnits, _hold.CapacityUnits);
            if (_renderer.sprite != s) _renderer.sprite = s;
            _renderer.enabled = true;
        }

        /// <summary>The sprite for a hold fill: the Def's authored fill states when present (the owner's
        /// painted art), else the built greybox silhouettes. A null authored entry falls back to greybox
        /// so a half-authored array never blanks the tray.</summary>
        private static Sprite SpriteForFill(DeckContainerDef def, int used, int capacity)
        {
            if (def.FillSprites != null && def.FillSprites.Length > 0)
            {
                int idx = FillStateIndex(used, capacity, def.FillSprites.Length);
                Sprite authored = def.FillSprites[idx];
                if (authored != null) return authored;
            }
            return GreyboxState(FillStateIndex(used, capacity, GreyboxStateCount));
        }

        private void EnsureRenderer()
        {
            if (_renderer != null) return;
            var go = new GameObject("DeckContainer");
            go.transform.SetParent(transform, false);
            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = ContainerSortingOrder;
        }

        // ---- code-built greybox fill states (cached; the owner's sprites replace them via the Def) ----

        private static readonly Sprite[] _greyboxCache = new Sprite[GreyboxStateCount];

        private static Sprite GreyboxState(int index)
        {
            index = Mathf.Clamp(index, 0, GreyboxStateCount - 1);
            if (_greyboxCache[index] != null) return _greyboxCache[index];
            _greyboxCache[index] = BuildSprite(TrayMaps[index], TrayPalette);
            return _greyboxCache[index];
        }

        // Pixel maps, drawn top row first ('.' = transparent). Greybox art, not balance data — a ¾-view
        // fish tray whose contents (lobster 'o', crab 'c') visibly accumulate: empty → low → half → brim
        // (the brim heaps over the back rim). All four share one canvas so the swap never shifts.
        // 'T' = tray shell, 'f' = tray floor.
        private static readonly string[][] TrayMaps =
        {
            new[]   // 0 — empty: bare floor, the tray reads as an open box
            {
                "......................",
                "......................",
                "......................",
                "TTTTTTTTTTTTTTTTTTTTTT",
                "TffffffffffffffffffffT",
                "TffffffffffffffffffffT",
                "TffffffffffffffffffffT",
                "TffffffffffffffffffffT",
                "TffffffffffffffffffffT",
                "TTTTTTTTTTTTTTTTTTTTTT",
                "T.T................T.T",
                "TTTTTTTTTTTTTTTTTTTTTT",
            },
            new[]   // 1 — low: a lobster and a crab in the corners
            {
                "......................",
                "......................",
                "......................",
                "TTTTTTTTTTTTTTTTTTTTTT",
                "TffffffffffffffffffffT",
                "TffooofffffffffffffffT",
                "TfooooofffffffccfffffT",
                "TffoooffffffcccccffffT",
                "TffffofffffffccffffffT",
                "TTTTTTTTTTTTTTTTTTTTTT",
                "T.T................T.T",
                "TTTTTTTTTTTTTTTTTTTTTT",
            },
            new[]   // 2 — half: the floor is going under
            {
                "......................",
                "......................",
                "......................",
                "TTTTTTTTTTTTTTTTTTTTTT",
                "TffccffffooooffffffffT",
                "TfccccffooooooffccfffT",
                "ToooccfffoooofccccfffT",
                "ToooooffffffffccccoofT",
                "TfoooffffccfffffoooooT",
                "TTTTTTTTTTTTTTTTTTTTTT",
                "T.T................T.T",
                "TTTTTTTTTTTTTTTTTTTTTT",
            },
            new[]   // 3 — brim: full, heaped over the back rim
            {
                "......oo.....cc.......",
                "...oooooocccccccoo....",
                ".oooocccccooooccccco..",
                "TTTTTTTTTTTTTTTTTTTTTT",
                "ToooccccooooooccccoooT",
                "ToccccooooccccooooccoT",
                "ToooooccccooooccccoooT",
                "ToccccooooccccooooccoT",
                "ToooccccooooooccccoooT",
                "TTTTTTTTTTTTTTTTTTTTTT",
                "T.T................T.T",
                "TTTTTTTTTTTTTTTTTTTTTT",
            },
        };

        // Greybox palette: a pale weathered tray shell, a darker floor, the deck-work silhouettes'
        // lobster blue-green and crab brick so the tray's contents match the animals you banded.
        private static readonly Dictionary<char, Color> TrayPalette = new()
        {
            { 'T', new Color(0.55f, 0.62f, 0.68f, 1f) },   // tray shell — weathered pale blue-grey
            { 'f', new Color(0.33f, 0.40f, 0.46f, 1f) },   // tray floor — wet slate
            { 'o', new Color(0.13f, 0.22f, 0.28f, 1f) },   // lobster — dark blue-green (deck-work match)
            { 'c', new Color(0.62f, 0.32f, 0.22f, 1f) },   // crab — brick-brown (deck-work match)
        };

        private static Sprite BuildSprite(string[] map, Dictionary<char, Color> palette)
        {
            int h = map.Length;
            int w = map[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < h; y++)
            {
                string row = map[y];
                for (int x = 0; x < w; x++)
                {
                    Color px = clear;
                    if (x < row.Length && palette.TryGetValue(row[x], out Color c)) px = c;
                    tex.SetPixel(x, h - 1 - y, px);
                }
            }
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), SilhouettePixelsPerUnit);
        }
    }
}
