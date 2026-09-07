#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Player;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Stand a clam hod on the flat</b> — the wire roller basket a digger drags along the bar and
    /// tips her catch into.
    ///
    /// <para><b>Sibling of <see cref="DevBucketBuilder"/>, and deliberately the same arrangement:</b>
    /// <see cref="CarriableBucket"/> (the hold + the three verbs) → <see cref="HoldCatchFillSource"/>
    /// (Core events → plain data) → <see cref="HodFillPresenter"/> (band × facing → one baked sprite).
    /// The hod IS its own hold, exactly as the pail is, so it fills by the shipped press — a clam in
    /// hand, walk to the hod, press — which is the owner's 2026-08-13 ruling that a clam can be in hand
    /// but needs to be placed in a container to stack. Nothing about the dig changes: `ClamDig` still
    /// stows into whatever hold its spot names.</para>
    ///
    /// <para><b>What is NOT the same as the pail, and why.</b> A pail is one baked picture per (band ×
    /// group × facing); the hod is baked HOLLOW — <c>Hod2_back</c>, then the band's
    /// <c>Hod2_heap_&lt;band&gt;</c>, then <c>Hod2_front</c> — so that a full hod shows shells through
    /// the galvanised mesh instead of wearing a lid of them. That is three renderers on one transform,
    /// built here in that order, sharing one sorting order so the trio moves as a single Y-sorted prop.
    /// And the hod bakes ONE catch, the clam: <c>CatchKit2.heap</c> heaps shellfish only, and this is
    /// the clam dig's basket.</para>
    ///
    /// <para><b>Null-safe in the greybox way the rest of these builders are:</b> a missing hod sheet
    /// leaves the basket invisible but still liftable and still a working hold, and says which file was
    /// missing. Nothing here refuses to build.</para>
    /// </summary>
    public static class ClamHodBuilder
    {
        const string Storage = "Assets/_Project/Art/Fishing/Storage";

        /// <summary>The hod's clam capacity — matched to <see cref="ClamBucket"/>'s and the dev pail's,
        /// so the three containers on the flat do not disagree about what a basket of clams is.</summary>
        public const int Capacity = DevBucketBuilder.Capacity;

        /// <summary>
        /// Build a clam hod under <paramref name="parent"/> at <paramref name="pos"/> and return it.
        /// </summary>
        /// <param name="interactId">Stable INSTANCE id for the interact registry. ⚠️ Must be unique among
        /// live registrants — it is the resolver's last tie-break, and a duplicate makes its order
        /// non-total.</param>
        public static GameObject Place(Transform parent, Vector2 pos, string interactId)
        {
            var go = new GameObject("ClamHod");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            // The BODY renderer is the one the carry verbs and the Y-sort drive; the three layers hang
            // off it. It draws nothing itself — the hod's picture is entirely in its children — but
            // CarriableBucket wants a renderer to state the carried order on, and one authority for that
            // order is the whole point (see DevBucketBuilder: never two Y-sort policies at once).
            var sr = go.AddComponent<SpriteRenderer>();
            go.AddComponent<YSortSprite>();

            SpriteRenderer back = Layer(go, "Back", 0);
            SpriteRenderer heap = Layer(go, "Heap", 1);
            SpriteRenderer front = Layer(go, "Front", 2);

            var presenter = go.AddComponent<HodFillPresenter>();
            presenter.ConfigureRenderers(back, heap, front);

            Sprite[] backs = ByDir("Hod2_back", out bool haveBack);
            Sprite[] fronts = ByDir("Hod2_front", out bool haveFront);
            presenter.Configure(DevBucketBuilder.Library(), backs, fronts, HeapsFlattened());

            if (!haveBack || !haveFront)
                Debug.LogWarning($"[ClamHodBuilder] No sliced '{Storage}/Hod2_back.png' / " +
                                 "'Hod2_front.png' — the hod is INVISIBLE but still liftable and still a " +
                                 "working hold. Run Hidden Harbours ▸ Art ▸ Bake the Clam Hod, then " +
                                 "Import (after a new drop) ▸ Slice Catch Storage Sheets.");

            var hold = go.AddComponent<CarriableBucket>();
            hold.Configure(Capacity, sr, interactId);

            // The Core bridge LAST: it refreshes on enable, so it wants the hold and the presenter
            // already on the object.
            go.AddComponent<HoldCatchFillSource>();
            return go;
        }

        /// <summary>One of the hod's three stacked layers. They share the body's sorting ORDER and
        /// separate themselves by sortingOrder offset within it, so the trio Y-sorts as one prop and can
        /// never be split by a sort that only sees one of them.</summary>
        static SpriteRenderer Layer(GameObject body, string name, int offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(body.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = offset;
            return sr;
        }

        /// <summary>The eight facings of one hod layer sheet.</summary>
        static Sprite[] ByDir(string stem, out bool haveArt)
        {
            Sprite[] cells = PersistentCoreBuilder.LoadIsoDirFrames($"{Storage}/{stem}.png");
            haveArt = cells.Length >= HodFillPresenter.Facings;
            return haveArt ? First(cells) : System.Array.Empty<Sprite>();
        }

        /// <summary>
        /// The heap states, flattened exactly as <see cref="HodFillPresenter"/> indexes them:
        /// <c>[band × 8 + dir]</c>, band in the shared <see cref="CatchFillMath.FilledBands"/> order.
        ///
        /// <para>⚠️ The order is the presenter's contract and the two must be read together — a table
        /// filled in a different order draws a plausible hod at the wrong fullness, which is the kind of
        /// wrong nobody notices in play. <c>HodFillTests</c> pins the arithmetic.</para>
        /// </summary>
        static Sprite[] HeapsFlattened()
        {
            int facings = HodFillPresenter.Facings;
            var bands = CatchFillMath.FilledBands;
            var flat = new Sprite[bands.Length * facings];

            for (int b = 0; b < bands.Length; b++)
            {
                string band = bands[b].ToString().ToLowerInvariant();
                Sprite[] cells = PersistentCoreBuilder.LoadIsoDirFrames($"{Storage}/Hod2_heap_{band}.png");
                if (cells.Length < facings) continue;   // missing band → the presenter draws no heap

                Sprite[] byDir = First(cells);
                for (int d = 0; d < facings; d++)
                    flat[b * facings + d] = byDir[d];
            }
            return flat;
        }

        /// <summary>The first frame of each direction row — the identity for the hod's one-frame sheets,
        /// written as the general form for the same reason <see cref="DevBucketBuilder"/>'s is.</summary>
        static Sprite[] First(Sprite[] cells)
        {
            int facings = HodFillPresenter.Facings;
            int perDir = cells.Length / facings;
            return Enumerable.Range(0, facings).Select(d => cells[d * perDir]).ToArray();
        }
    }
}
#endif
