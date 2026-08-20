#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;               // StationPieceDef / YSortSprite
using HiddenHarbours.Art.Editor;        // StationCatalog / StationReachAudit
using HiddenHarbours.Core;              // SortingBands (ADR 0032)

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>HOW A LAID-OUT FORECOURT IS STOOD UP</b> — the part of opening a fuel station that is about the
    /// KIT rather than about a place: turning a list of pieces in metres into prefabs in a scene, at the
    /// right cells, with their colliders on the ground they actually cover, and then checking the whole
    /// thing over as one object.
    ///
    /// <para><b>Why this is shared and the SITES are not.</b> The same division
    /// <see cref="ShopPlacement"/> draws. Nine Mile Creek's wharf pumps and its Route 91 station disagree
    /// about everything a place disagrees about — which ground, which way, how many hoses, what is in the
    /// way. They agree completely about what STANDING A PIECE means, and that agreement is arithmetic a
    /// second copy of would get subtly wrong: the facing cell, the collider's turn, and the reach pass.
    /// </para>
    ///
    /// <para><b>⚠️ THE SPRITE DOES NOT TURN; THE COLLIDERS DO.</b> Baked iso art already contains the
    /// camera, so rotating a renderer shears the picture — the facing IS the rotation, and every kit in
    /// this repo places its art at identity. The sidecar's footprints are a different thing: they are the
    /// GROUND the piece covers, stated in the piece's own frame, so at any cell but 0 they are turned.
    /// The blocker children therefore carry <see cref="StationCatalog.ColliderZRotation"/> and the root
    /// stays at identity. Getting this backwards draws perfectly and puts the collision somewhere else,
    /// which is the failure mode nobody notices until they walk it.</para>
    ///
    /// <para><b>Region-agnostic on purpose:</b> nothing here knows a tide, a road or a wharf. The one
    /// thing a caller passes that is not from the kit is the log tag and the region's own obstructions,
    /// so a build report still says which place asked.</para>
    /// </summary>
    public static class StationForecourt
    {
        // =====================================================================================
        //  A LAYOUT
        // =====================================================================================

        /// <summary>
        /// One piece of a forecourt: which kit build, where it stands in the layout's own frame, which
        /// cell it is drawn at, and one line on why it is there.
        ///
        /// <para>⚠️ <see cref="Key"/> is the KIT's key (<c>dispenser_sMpd</c>) and is never camel-cased
        /// from a label — a stem that cannot be matched to a build fails silently, which is the shrub
        /// kit's lesson and the reason <see cref="NineMileCreekShops"/> carries the same warning.</para>
        /// </summary>
        public readonly struct Piece
        {
            public readonly string Key;

            /// <summary>Metres in the LAYOUT's frame — <c>+y</c> is the road side, which is the frame the
            /// rig's own <c>StationIso.plan()</c> emits.</summary>
            public readonly Vector2 Local;

            /// <summary>The cell this piece is DRAWN at (0…7), absolute. Usually the layout's own, but
            /// carried per piece because a pump on a wharf faces the water and not the road.</summary>
            public readonly int Facing;

            public readonly string Reason;

            public Piece(string key, Vector2 local, int facing, string reason)
            {
                Key = key; Local = local; Facing = StationCatalog.Wrap(facing); Reason = reason;
            }
        }

        /// <summary>
        /// A forecourt: where it stands, which way its road side looks, and what is on it.
        ///
        /// <para><see cref="RoadHeadingDegrees"/> is a compass bearing (N = 0, clockwise) and it is the
        /// ONE number that turns the whole layout — the pieces' local offsets follow it, so re-aiming a
        /// station at a different road is a one-line edit rather than a re-typed table.</para>
        /// </summary>
        public sealed class Layout
        {
            public string Name = "Forecourt";

            /// <summary>The layout's origin in world metres. For a <c>plan()</c> forecourt this is the
            /// ISLAND's ground centre, because that is where the rig puts its own origin.</summary>
            public Vector2 Origin;

            /// <summary>Where the layout's local <c>+y</c> points, as a compass bearing.</summary>
            public float RoadHeadingDegrees;

            public readonly List<Piece> Pieces = new List<Piece>();

            /// <summary>The cell the layout as a whole is drawn at.</summary>
            public int Facing => StationCatalog.FacingForHeading(RoadHeadingDegrees);

            /// <summary>A piece's world position.</summary>
            public Vector2 WorldOf(in Piece p) =>
                StationCatalog.LocalToWorld(p.Local, Origin, Facing);

            /// <summary>Add a piece at the layout's own facing — the ordinary case.</summary>
            public Layout Add(string key, Vector2 local, string reason)
            {
                Pieces.Add(new Piece(key, local, Facing, reason));
                return this;
            }

            /// <summary>Add a piece turned to face something of its own.</summary>
            public Layout Add(string key, Vector2 local, int facing, string reason)
            {
                Pieces.Add(new Piece(key, local, facing, reason));
                return this;
            }

            /// <summary>The layout as the reach pass wants it — pure, so a test can audit a forecourt
            /// without a scene. Pieces with no Def are dropped (and reported by <see cref="Place"/>).</summary>
            public List<StationReachAudit.Placed> AsPlaced()
            {
                var list = new List<StationReachAudit.Placed>();
                foreach (Piece p in Pieces)
                {
                    StationPieceDef def = StationCatalog.Find(p.Key);
                    if (def != null) list.Add(new StationReachAudit.Placed(def, WorldOf(p), p.Facing, p.Key));
                }
                return list;
            }
        }

        // =====================================================================================
        //  THE RESULT
        // =====================================================================================

        /// <summary>The child object name a fitting's standing spot is placed under.</summary>
        public const string FittingPrefix = "fitting_";

        /// <summary>What a placement did, so the caller can report and a test can assert.</summary>
        public sealed class Result
        {
            public GameObject Root;
            public readonly List<GameObject> Placed = new List<GameObject>();
            public readonly List<string> Missing = new List<string>();
            public List<StationReachAudit.Result> Reach = new List<StationReachAudit.Result>();

            /// <summary>
            /// One empty per usable fitting, standing at the spot the WHOLE-SCENE pass verified, keyed
            /// <c>"&lt;index into Placed&gt;/&lt;interactableId&gt;"</c> — see <see cref="Fitting"/>.
            ///
            /// <para><b>Why these exist even where nothing can be done yet.</b> Most of what a forecourt
            /// offers has no verb: the sidecar names <c>buy · drinks · coffee · food · cash · browse</c>
            /// and not one of those interactions exists (#613 recorded it; the economy lane owns it). An
            /// anchor is still worth placing, for three reasons — the audited standing spot becomes
            /// something the owner can SELECT and look at rather than a number in a log; the verb, when
            /// it lands, has a stable place to hang rather than re-deriving the geometry; and the pump
            /// that DOES have a verb hangs on exactly the same seam as the ones that do not, so there is
            /// one way of doing this rather than two.</para>
            /// </summary>
            public readonly Dictionary<string, GameObject> Fittings =
                new Dictionary<string, GameObject>(StringComparer.Ordinal);

            public int Count => Placed.Count;

            /// <summary>The anchor for one fitting on the piece at <paramref name="pieceIndex"/> (its
            /// index in <see cref="Placed"/>), or null. Indexed rather than keyed by piece NAME because a
            /// forecourt stands two machines of the same kind and a name would collide.</summary>
            public GameObject Fitting(int pieceIndex, string interactableId) =>
                Fittings.TryGetValue(pieceIndex + "/" + interactableId, out GameObject go) ? go : null;

            /// <summary>Fittings the whole-scene pass could not find a standing spot for — the only
            /// outcome that is a defect rather than a note.</summary>
            public int Unreachable
            {
                get
                {
                    int n = 0;
                    if (Reach != null)
                        foreach (var r in Reach)
                            if (r.Outcome == StationReachAudit.Verdict.NoClearSpot) n++;
                    return n;
                }
            }
        }

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Stand a forecourt up under <paramref name="parent"/> and audit it as one object.
        ///
        /// <para><b>Re-runnable.</b> The root is built fresh each call, so a second builder pass leaves
        /// one of everything.</para>
        ///
        /// <para>Null-tolerant throughout, for the creek's standing reason: "declared in the contract"
        /// and "has pixels on disk" are different questions, and a partial art state should place what it
        /// can rather than throw halfway through a region build.</para>
        /// </summary>
        /// <param name="sceneBlocks">The region's own obstructions, for the reach pass — see
        /// <see cref="StationReachAudit.Audit"/>. Null means the forecourt is all there is.</param>
        public static Result Place(Layout layout, string rootName, string tag, Transform parent = null,
                                   Func<Vector2, StationReachAudit.Level, bool> sceneBlocks = null)
        {
            var result = new Result();
            if (layout == null) return result;

            if (!StationCatalog.IsInstalled)
            {
                Debug.LogWarning(
                    $"[{tag}] no StationPieceDefs under {StationCatalog.DefFolder} — the station is not " +
                    "placed. Run Hidden Harbours ▸ Art ▸ Bake Gas Station, then ▸ Import ▸ Slice Gas " +
                    "Station Sheets, then ▸ Build Station Piece Defs, and rebuild the region.");
                return result;
            }

            result.Root = new GameObject(rootName);
            if (parent != null) result.Root.transform.SetParent(parent, worldPositionStays: false);

            var audited = new List<StationReachAudit.Placed>();

            foreach (Piece p in layout.Pieces)
            {
                StationPieceDef def = StationCatalog.Find(p.Key);
                GameObject prefab = StationCatalog.Prefab(p.Key);

                if (def == null || prefab == null)
                {
                    result.Missing.Add(p.Key);
                    Debug.LogWarning(
                        $"[{tag}] '{p.Key}' has no {(def == null ? "Def" : "prefab")}, so the station is " +
                        $"missing the piece that would stand {p.Reason}. Skipping it rather than placing " +
                        "a blank.");
                    continue;
                }

                Sprite sprite = def.Frame(p.Facing);
                if (sprite == null)
                {
                    result.Missing.Add(p.Key);
                    Debug.LogWarning(
                        $"[{tag}] {p.Key} cell {p.Facing} has no sprite — its sheet is missing or " +
                        "unsliced. ⚠️ A sheet can be Multiple-mode and still not be sliced this kit's way.");
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, result.Root.transform);
                if (go == null) { result.Missing.Add(p.Key); continue; }

                Vector2 world = layout.WorldOf(p);
                go.name = p.Key;
                go.transform.position = new Vector3(world.x, world.y, 0f);

                // The pivot IS the ground centre of the piece's own footprint (ADR 0026), so the site
                // position is the position. No bottom-centre "correction" — that would stand a 4.7 m
                // canopy several metres into the dirt.
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = sprite;

                TurnColliders(go, p.Facing);
                result.Placed.Add(go);

                // ⚠️ The audit list is built from what ACTUALLY went in, index-aligned with Placed — not
                // from the layout. A piece whose prefab is missing must not be audited as though it were
                // standing there, and the anchors below need the two lists to agree about which piece is
                // which.
                audited.Add(new StationReachAudit.Placed(def, world, p.Facing, p.Key));
            }

            result.Reach = StationReachAudit.Audit(audited, sceneBlocks);
            HangFittings(result);
            return result;
        }

        /// <summary>
        /// Stand an empty at every usable fitting's AUDITED spot, under the piece that owns it.
        ///
        /// <para>The spot is the whole-scene pass's answer, not the bake's request — so where the two
        /// differ, the thing in the scene is the one that was checked against the island, the storefront
        /// and the quay wall rather than against the piece alone. A fitting the pass could not place is
        /// deliberately given no anchor: an empty at a spot nobody can stand on is a lie that reads as
        /// working, which is the failure mode this whole slice is written against.</para>
        /// </summary>
        static void HangFittings(Result result)
        {
            foreach (StationReachAudit.Result r in result.Reach)
            {
                if (!r.Usable) continue;
                if (r.PieceIndex < 0 || r.PieceIndex >= result.Placed.Count) continue;

                GameObject host = result.Placed[r.PieceIndex];
                if (host == null) continue;

                var go = new GameObject(FittingPrefix + r.InteractableId +
                                        (string.IsNullOrEmpty(r.Action) ? "" : "_" + r.Action));
                go.transform.SetParent(host.transform, worldPositionStays: false);
                go.transform.position = new Vector3(r.Point.x, r.Point.y, 0f);
                result.Fittings[r.PieceIndex + "/" + r.InteractableId] = go;
            }
        }

        /// <summary>
        /// Turn a placed piece's collider children onto the ground it covers — see the class remarks for
        /// why this is the children and not the root.
        ///
        /// <para>Every child is turned, deliberately: the prefabs #613 builds carry blocker children and
        /// door LEAVES and nothing else, and both are ground shapes in the piece's own frame. A child
        /// that must not turn would be a renderer, and this kit puts all of its art on the root.</para>
        /// </summary>
        public static void TurnColliders(GameObject placed, int facing)
        {
            if (placed == null) return;
            var turn = Quaternion.Euler(0f, 0f, StationCatalog.ColliderZRotation(facing));
            for (int i = 0; i < placed.transform.childCount; i++)
                placed.transform.GetChild(i).localRotation = turn;
        }

        // =====================================================================================
        //  THE REPORT
        // =====================================================================================

        /// <summary>The sorting order a piece standing at <paramref name="worldY"/> will take, by the
        /// component's own arithmetic — so a builder can say whether the band resolved it or saturated
        /// it without instantiating anything (ADR 0032).</summary>
        public static int SortingOrderAt(float worldY) =>
            YSortSprite.OrderFor(worldY, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                 SortingBands.DecorFloor, SortingBands.DecorCeiling);

        /// <summary>True when the ADR 0032 band actually resolves this latitude rather than clamping it —
        /// the check that used to be missing when the island outgrew a 9.5 m window.</summary>
        public static bool BandResolves(float worldY)
        {
            int o = SortingOrderAt(worldY);
            return o > SortingBands.DecorFloor && o < SortingBands.DecorCeiling;
        }
    }
}
#endif
