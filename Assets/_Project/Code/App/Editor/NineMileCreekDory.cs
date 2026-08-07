#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;                // SortingBands — the one place the sorting axis is partitioned
using HiddenHarbours.Art;                 // YSortSprite — she lies on ground, so she layers by world Y

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE DERELICT DORY</b> — the boat the whole prologue is for, lying at the wharf where you land,
    /// still damaged, still somebody else's. §7.2's exit condition is one sentence and she is two thirds
    /// of it: <i>"you arrive off the sandbar, sell, SEE THE DORY, and understand she is the next
    /// rung."</i>
    ///
    /// <para><b>She is DRESSING, not a boat.</b> A hull propped on the concrete, drawn from one facing of
    /// the dory's iso sheet — no <c>BoatController</c>, no visual def, no mesh. The fleet's pipeline is
    /// not this lane's, and she would be wrong in it anyway: you cannot sail her, and the point of her is
    /// that you cannot. The purchase is the shipwright-offer path that already exists
    /// (<c>Data/Shipwright/DamagedDoryOffer.asset</c>, placed by the builder); this is her body.</para>
    ///
    /// <para><b>⭐ WHERE SHE LIES IS SOLVED, NOT EYEBALLED.</b> "Visible from arrival" is a claim about a
    /// CAMERA, and this project's on-foot camera frames exactly 9 m of world height at 16:9 — sixteen
    /// metres across. Measured against that, the region has almost no freedom: the player lands at the
    /// disembark point on the quay, and the nearest dry ground off the deck is the beach at x = −4, which
    /// is 6 m west and already at the frame's edge before her hull is drawn. So she lies ON THE QUAY at
    /// its landward end — which is also what canon says (<c>design/nine-mile-creek-wharf.md</c> §1's
    /// second rider: <i>"the dory is here — lying at the wharf, still damaged, still bought"</i>). The
    /// view rectangle is READ from <see cref="CameraFollow"/> rather than restated, so a framing change
    /// re-checks her instead of silently leaving her off-screen.</para>
    ///
    /// <para><b>Visible before reachable.</b> She sits at the far end of the planks from where you step
    /// ashore, so she is in frame from the first moment and you walk the length of the working quay —
    /// past the buyer's truck — to stand at her. And a hull is a solid object: her middle carries a
    /// collider, deliberately narrow enough to leave a walking lane either side, so the quay she is
    /// parked on does not stop being a quay.</para>
    /// </summary>
    public static class NineMileCreekDory
    {
        /// <summary>The root she hangs under.</summary>
        public const string RootName = "DerelictDory";

        /// <summary>The dory's iso hull sheet — the same art the player's own dory wears once she is
        /// bought and repaired, which is the point: this IS her, before.</summary>
        public const string HullSheet = "Assets/_Project/Art/Boats/DoryIso.png";

        /// <summary>
        /// Which facing of the sheet she is drawn at. <b>Derived, not picked:</b> <c>BoatVisualDef</c>'s
        /// contract is that element 0 is the NORTH-facing picture, and the iso kits bake
        /// COUNTER-CLOCKWISE — cell <c>i</c> depicts heading <c>−45°·i</c>. A boat is hauled out bow-first
        /// up the beach, so her bow points WEST (heading 270° = −90°), which is <c>−90 / −45 = 2</c>. That
        /// also puts her broadside to the camera, which is the silhouette that reads as a boat.
        ///
        /// <para>⚠ The counter-clockwise claim is the KIT's, and <c>BoatVisualLibraryBuilder</c> carries a
        /// standing warning about exactly this fact having been wrong before. If she is pointing out to
        /// sea instead of up the beach, it is this one constant, never a re-slice.</para>
        /// </summary>
        public const int HullFacingIndex = 2;

        /// <summary>Facings the sheet ships. Pinned so a re-bake at a different count fails loudly here
        /// rather than silently drawing the wrong quarter of a boat.</summary>
        public const int HullFacingCount = 8;

        // --- HER CELL, in metres (the sheet's own numbers at the locked 32 px = 1 m) ------------------
        // 160 × 156 px, pivot (80, 88) from the cell's TOP-LEFT — the hull's WATERLINE contact point, the
        // anchor every dory layer in the repo shares. Normalised from the bottom that is (0.5, 68/156), so
        // her pivot is horizontally centred and sits 2.125 m above the drawn keel.
        public const float HullWidthMetres = 160f / 32f;      // 5.0
        public const float HullHeightMetres = 156f / 32f;     // 4.875
        public const float HullBelowPivotMetres = 68f / 32f;  // 2.125
        public static float HullAbovePivotMetres => HullHeightMetres - HullBelowPivotMetres;

        /// <summary>
        /// Where she lies: hauled out ON THE HARD at the west end of the spit, among the trap stacks and
        /// the parked trucks — the plan's own site (§7, "the derelict dory on the hard"), and the
        /// 2026-07-25 rider's words.
        ///
        /// <para>⚠ SHE IS NO LONGER ON THE QUAY, and that is the recreation rather than a drift. On the
        /// 120 m island the wharf was the only dry ground inside the on-foot frame, so canon's "lying at
        /// the wharf" and "on the concrete" were the same place. The mainland has a whole made spit behind
        /// the quay — which is where a derelict actually sits, because a working wharf does not store a
        /// wreck on its mooring face. What still has to be true, and what
        /// <see cref="IsVisibleFromArrival"/> measures, is that you can SEE her from where you land.</para>
        /// </summary>
        public static Vector2 HaulOutPos => NineMileCreekMainland.DerelictDoryPos;

        /// <summary>Her drawn silhouette in world metres, from the cell above and the pivot's place in
        /// it. What "does she hang over the water" is asked of.</summary>
        public static Rect HullBounds() => new Rect(
            HaulOutPos.x - HullWidthMetres * 0.5f,
            HaulOutPos.y - HullBelowPivotMetres,
            HullWidthMetres,
            HullHeightMetres);

        // --- SHE IS SOLID, BUT SHE DOES NOT CLOSE THE QUAY -------------------------------------------

        /// <summary>The blocking box on her hull's middle, in metres. Narrower than she is drawn on
        /// purpose: a hull's collision is her planking, not her flare, and the number that matters is the
        /// lane it leaves.</summary>
        public static readonly Vector2 ColliderSize = new Vector2(3.4f, 1.6f);

        /// <summary>The walking lane a person needs to get past her, in metres. Two of these have to fit
        /// between her collider and the deck's two long edges or she has parked across the wharf.</summary>
        public const float WalkingLaneMetres = 1.5f;

        /// <summary>Her collider's world rectangle — centred on the pivot, which is her waterline and so
        /// very nearly her middle.</summary>
        public static Rect ColliderBounds() => new Rect(
            HaulOutPos.x - ColliderSize.x * 0.5f,
            HaulOutPos.y - ColliderSize.y * 0.5f,
            ColliderSize.x,
            ColliderSize.y);

        /// <summary>A sun-bleached, chalky wash over the hull art so she reads as a boat that has been
        /// sitting a long time, without asking art-pipeline for a second sheet.</summary>
        public static readonly Color DerelictWash = new Color(0.80f, 0.78f, 0.74f);

        /// <summary>
        /// She lies on the SPIT now, which is ground rather than a sprite deck, so she takes a
        /// <see cref="YSortSprite"/> and layers by world Y like every other object you walk around — the
        /// same treatment the creek's buildings and the quay's fittings get.
        ///
        /// <para>This is the pre-Play default only; the Y-sort owns the order from Awake. It is the decor
        /// band's FLOOR rather than a number above the wharf's structure band, because the thing she used
        /// to have to out-draw — a deck of wharf-kit sprites — is not drawn any more.</para>
        /// </summary>
        public static int SortingOrder => SortingBands.DecorFloor;

        // =====================================================================================
        //  THE SIGHTLINE — the part that is a maths problem, not a taste one
        // =====================================================================================

        /// <summary>Where the player is STANDING the moment they arrive — the region anchor's disembark
        /// point, which is where <c>RegionTravelCoordinator.ApplyArrival</c> puts them however they got
        /// here, on foot off the bar or under power from the cove.</summary>
        public static Vector2 ArrivalStandingPos => NineMileCreekBuilder.DisembarkPos;

        /// <summary>Visible world height (m) at the on-foot framing — read from the camera, not restated.</summary>
        public static float ArrivalViewHeightMetres => CameraFollow.OnFootWorldHeightMeters;

        /// <summary>Visible world width (m): the height at the project's design aspect, which is what the
        /// pixel-perfect reference resolution is derived from.</summary>
        public static float ArrivalViewWidthMetres =>
            ArrivalViewHeightMetres * CameraFollow.ReferenceWidthPx / (float)CameraFollow.ReferenceHeightPx;

        /// <summary>
        /// How much of the frame counts as "you cannot miss this". The camera EASES toward its target
        /// rather than snapping, and the pixel-perfect crop can shave a row or column at the edges — so a
        /// hull sitting exactly on the boundary is not a hull you can promise the player sees. Three
        /// quarters is a margin wide enough to survive both without pretending the screen is smaller than
        /// it is.
        /// </summary>
        public const float ViewSafeFraction = 0.75f;

        /// <summary>The full on-foot view rectangle at the moment of arrival.</summary>
        public static Rect ArrivalView() => ViewRect(1f);

        /// <summary>The inner rectangle a thing has to be in for "visible from arrival" to be a promise
        /// rather than a hope. See <see cref="ViewSafeFraction"/>.</summary>
        public static Rect ArrivalSafeView() => ViewRect(ViewSafeFraction);

        static Rect ViewRect(float fraction)
        {
            float w = ArrivalViewWidthMetres * fraction;
            float h = ArrivalViewHeightMetres * fraction;
            Vector2 c = ArrivalStandingPos;
            return new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
        }

        /// <summary>
        /// Is <paramref name="worldPos"/> inside the frame the player is handed on arrival?
        ///
        /// <para>⚠ <b>SHE NO LONGER IS, AND THAT IS THE RECREATION — flagged for the owner.</b> §7.2's
        /// exit condition was written for a 120 × 120 m island where the wharf was the only dry ground in
        /// shot: "arrive off the sandbar, sell, SEE THE DORY" happened inside one 16 × 9 m frame because
        /// the whole region nearly fitted in one. On the mainland it cannot. You now come ashore from the
        /// bar 260 m SOUTH of the wharf and walk in; the derelict lies on the hard among the trap stacks,
        /// 41 m from where a boat lands, and no 9 m-tall frame holds both. Contorting either the wharf or
        /// the hard to keep the sentence literally true would be bending the place to fit a test.</para>
        ///
        /// <para>So the promise is re-derived rather than dropped, and the tests measure what the mainland
        /// CAN honestly offer: she is on the working spit you cross, nothing built stands in the line from
        /// where you land to her, and she is nearer the yard that sells her than anything else is. The
        /// helper stays because it is how you MEASURE the beat if the owner wants it re-staged.</para>
        /// </summary>
        public static bool IsVisibleFromArrival(Vector2 worldPos) => ArrivalSafeView().Contains(worldPos);

        /// <summary>
        /// Does anything with a footprint stand between the arrival point and <paramref name="target"/>?
        /// The occluder is treated as a CIRCLE for the same reason the island's village reserves its
        /// ground with one — a footprint's facing is derived and a quarter-turned building presents its
        /// diagonal, so a circle is the only claim that stays true. Pure segment-to-circle distance.
        /// </summary>
        public static bool SightlineIsClear(Vector2 target, Vector2 occluder, float occluderRadius)
            => DistancePointToSegment(occluder, ArrivalStandingPos, target) > occluderRadius;

        /// <summary>Shortest distance from a point to a line SEGMENT (not its infinite line — a building
        /// behind you does not block the view in front of you).</summary>
        public static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-6f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return Vector2.Distance(p, a + ab * t);
        }

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Lay her at the wharf. Returns the GameObject, or null when the hull sheet is not imported —
        /// warned rather than thrown, because a partial art state should still build the region (the same
        /// rule every other dressing layer here follows).
        /// </summary>
        public static GameObject Place()
        {
            var sprite = LoadFacing(HullSheet, HullFacingIndex);
            if (sprite == null)
            {
                Debug.LogWarning(
                    $"[NineMileCreekDory] no facing {HullFacingIndex} in {HullSheet} — the creek has no " +
                    "derelict dory, which is most of what the player is supposed to arrive and SEE. " +
                    "Re-run once the boat sheets import.");
                return null;
            }

            var go = new GameObject(RootName);
            go.transform.position = new Vector3(HaulOutPos.x, HaulOutPos.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = DerelictWash;
            sr.sortingOrder = SortingOrder;   // pre-Play default only; the Y-sort below OWNS the order
            go.AddComponent<YSortSprite>();

            // Solid through her planking only. The lane either side is asserted, not hoped for — a hull
            // parked across the whole quay would wall the player off from the buyer at its root.
            var box = go.AddComponent<BoxCollider2D>();
            box.size = ColliderSize;
            box.offset = Vector2.zero;   // the sheet's pivot is her waterline: near enough her middle

            Debug.Log(
                $"[NineMileCreekDory] The derelict lies at {HaulOutPos} on the quay, facing {HullFacingIndex} " +
                $"(bow up the beach). She is {Vector2.Distance(ArrivalStandingPos, HaulOutPos):0.0} m from " +
                $"where the player lands, inside the {ArrivalViewWidthMetres:0.#} × {ArrivalViewHeightMetres:0.#} m " +
                "on-foot frame — you cannot arrive here and not see her. She is DRESSING: buying her is " +
                "the DamagedDoryOffer the builder places, not this hull.");
            return go;
        }

        /// <summary>One facing of a grid-sliced sheet, by its trailing slice INDEX. The boat sheets import
        /// spriteMode MULTIPLE, so a plain <c>LoadAssetAtPath&lt;Sprite&gt;</c> returns null for them.</summary>
        static Sprite LoadFacing(string path, int index)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(obj is Sprite s)) continue;
                int u = s.name.LastIndexOf('_');
                if (u >= 0 && int.TryParse(s.name.Substring(u + 1), out int n) && n == index) return s;
            }
            return null;
        }

        /// <summary>How many facings the sheet actually sliced to — 0 when it has not imported. Used by
        /// the tests to check the pinned count against the committed pixels.</summary>
        public static int ImportedFacingCount() =>
            AssetDatabase.LoadAllAssetsAtPath(HullSheet).OfType<Sprite>().Count();
    }
}
#endif
