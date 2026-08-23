#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Player;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Stand a carriable pail in the world, beside the other things you pick up</b> — the dev test
    /// bucket for the two-hand carry.
    ///
    /// <para><b>Why one builder and not two copies.</b> The pail is wanted in both playable regions (St
    /// Peters' start spawn, beside the shovel and the rod; Nine Mile Creek's dev bootstrap, beside the
    /// player it drops you as) and every part of the arrangement — the hold, the carry verb, the fill
    /// bridge, the eight facings of baked art — is the same in both. Two copies of that is two things to
    /// keep in step, and the wharf lane has already paid for that mistake once. Each region builder calls
    /// <see cref="Place"/>; only the position differs.</para>
    ///
    /// <para><b>The whole chain, wired here so it can be read in one place:</b>
    /// <see cref="CarriableBucket"/> (the hold + the three verbs) → <see cref="HoldCatchFillSource"/>
    /// (Core events → plain data) → <see cref="BucketFillPresenter"/> (band × catch group × facing → one
    /// baked sprite). Break any link and the pail fills invisibly, which is exactly the defect this
    /// arrangement was written to close.</para>
    ///
    /// <para><b>Null-safe in the greybox way the rest of these builders are:</b> a missing bucket sheet
    /// leaves the pail invisible but still liftable and still a working hold, and says which file was
    /// missing. Nothing here refuses to build.</para>
    /// </summary>
    public static class DevBucketBuilder
    {
        const string Storage = "Assets/_Project/Art/Fishing/Storage";

        /// <summary>Which bucket tier the dev pail wears. <c>pail</c> is the galvanised steel bucket —
        /// the rig's ONE-HAND tier, which is the whole point here (the fish tray, tier 3, is a two-hand
        /// carry and would want a different slot rule).</summary>
        public const string Tier = "pail";

        /// <summary>The pail's clam capacity — matched to <see cref="ClamBucket"/>'s so the belt pail and
        /// the carried one do not disagree about what a pail is.</summary>
        public const int Capacity = 20;

        /// <summary>
        /// Build the dev pail under <paramref name="parent"/> at <paramref name="pos"/> and return it.
        /// </summary>
        /// <param name="interactId">Stable INSTANCE id for the interact registry. ⚠️ Must be unique among
        /// live registrants — pass a region-scoped one (the two regions are never loaded together today,
        /// but the ids are what the resolver's last tie-break reads and a duplicate makes its order
        /// non-total).</param>
        public static GameObject Place(Transform parent, Vector2 pos, string interactId)
        {
            var go = new GameObject("Bucket");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();

            // Lying on the ground among the decor: Y-sorted like every other walk-up prop (ADR 0032), so
            // it draws behind the fisher standing north of it and in front of one standing south. While
            // CARRIED this component stands itself down and CarryHands states the order instead — one
            // Y-sort policy at a time, never two fighting over the renderer.
            go.AddComponent<YSortSprite>();

            var presenter = go.AddComponent<BucketFillPresenter>();
            Sprite[] empty = EmptyByDir(out bool haveArt);
            presenter.Configure(Library(), empty, FillsFlattened());
            sr.sprite = empty.Length > 0 ? empty[0] : null;

            if (!haveArt)
                Debug.LogWarning($"[DevBucketBuilder] No sliced '{Storage}/Bucket_{Tier}_empty.png' — the " +
                                 "dev bucket is INVISIBLE but still liftable and still a working hold. " +
                                 "Run Hidden Harbours ▸ Art ▸ Bake Catch Storage Kit, then re-run this " +
                                 "builder.");

            var bucket = go.AddComponent<CarriableBucket>();
            bucket.Configure(Capacity, sr, interactId);

            // The Core bridge LAST: it refreshes on enable, so it wants the hold and the presenter
            // already on the object.
            go.AddComponent<HoldCatchFillSource>();
            return go;
        }

        /// <summary>The catch art table (species → visual kind). Built and committed by
        /// <see cref="CatchItemLibraryBuilder"/>; null here means it has not been built yet, and the
        /// bridge says so at runtime rather than filling silently with nothing.</summary>
        static CatchItemLibrary Library()
        {
            var library = AssetDatabase.LoadAssetAtPath<CatchItemLibrary>(CatchItemLibraryBuilder.LibraryPath);
            if (library != null) return library;

            // ⚠️ BUILD IT rather than warn and carry on. Requiring the owner to run two menu items in the
            // right order is exactly the kind of ordering trap that ships a bucket that fills invisibly —
            // which is the defect this whole arrangement exists to close. The importer is idempotent and
            // non-destructive, so running it here costs a second and cannot clobber anything.
            if (CatchItemLibraryBuilder.BuildLibrary(out string summary))
                Debug.Log($"[DevBucketBuilder] The catch art table was missing, so it was built: {summary}");
            else
                Debug.LogWarning($"[DevBucketBuilder] No catch art table and it could not be built " +
                                 $"({summary}) — the pail cannot map a landed species onto a catch group " +
                                 "and will read as the rig's default group. Run Art ▸ Import (after a new " +
                                 "drop) ▸ Build Catch Item Library.");

            return AssetDatabase.LoadAssetAtPath<CatchItemLibrary>(CatchItemLibraryBuilder.LibraryPath);
        }

        /// <summary>The eight facings of the empty pail.</summary>
        static Sprite[] EmptyByDir(out bool haveArt)
        {
            Sprite[] cells = PersistentCoreBuilder.LoadIsoDirFrames($"{Storage}/Bucket_{Tier}_empty.png");
            haveArt = cells.Length >= BucketFillPresenter.Facings;
            return haveArt ? First(cells) : System.Array.Empty<Sprite>();
        }

        /// <summary>
        /// The filled states, flattened exactly as <see cref="BucketFillPresenter"/> indexes them:
        /// <c>[(band × 3 + group) × 8 + dir]</c>, band in few/half/full/brim order, group in the bucket
        /// rig's fish/shell/crust order.
        ///
        /// <para>⚠️ The order is the presenter's contract and the two must be read together — a table
        /// filled in a different order draws a plausible pail at the wrong fullness, which is the kind of
        /// wrong nobody notices in play. <c>BucketFillPresenterTests</c> pins the arithmetic.</para>
        /// </summary>
        static Sprite[] FillsFlattened()
        {
            int facings = BucketFillPresenter.Facings;
            string[] groups = CatchFillMath.BucketGroups;
            var flat = new Sprite[BucketFillPresenter.Bands.Length * groups.Length * facings];

            for (int b = 0; b < BucketFillPresenter.Bands.Length; b++)
            {
                string band = BucketFillPresenter.Bands[b].ToString().ToLowerInvariant();
                for (int g = 0; g < groups.Length; g++)
                {
                    Sprite[] cells =
                        PersistentCoreBuilder.LoadIsoDirFrames($"{Storage}/Bucket_{Tier}_{band}_{groups[g]}.png");
                    if (cells.Length < facings) continue;   // missing state → the presenter draws empty

                    Sprite[] byDir = First(cells);
                    for (int d = 0; d < facings; d++)
                        flat[(b * groups.Length + g) * facings + d] = byDir[d];
                }
            }
            return flat;
        }

        /// <summary>The first frame of each direction row. The bucket sheets bake one frame per facing,
        /// so this is the identity for them — written as the general form because
        /// <see cref="PersistentCoreBuilder.LoadIsoDirFrames"/> returns <c>dir × perDir + frame</c> and
        /// assuming <c>perDir == 1</c> is how a re-bake with an animated pail would silently rotate the
        /// whole table.</summary>
        static Sprite[] First(Sprite[] cells)
        {
            int facings = BucketFillPresenter.Facings;
            int perDir = cells.Length / facings;
            return Enumerable.Range(0, facings).Select(d => cells[d * perDir]).ToArray();
        }
    }
}
#endif
