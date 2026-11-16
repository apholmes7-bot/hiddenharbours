using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The bubble kit's baked pieces, reachable at runtime by anything</b> — a Resources asset that
    /// POINTS AT the shipped sprites under <see cref="DialogueBubbleKit"/>'s art folder. It holds
    /// references, never copies: the panel, the gold and the cursor stay the one set of PNGs the bubble
    /// draws with, so a re-bake of the kit re-dresses everything that links here.
    ///
    /// <para><b>Why it exists.</b> The speech bubble is dressed at build time by the region builders
    /// (one presenter per region scene), so its sprites live in scenes. A self-installing overlay — the
    /// interaction chooser (owner ruling: "the same design language as our notebook and chat
    /// bubbles") — has no scene to be dressed in and no editor lookup in a player, so it resolves the
    /// kit the way the notebook resolves its own: from Resources, once, cached.</para>
    ///
    /// <para>Absent or half-wired is legal (an EditMode fixture, a box before the bake): every reader
    /// falls back to the kit's palette tints (<see cref="NotebookInk"/>), exactly as an unbaked bubble
    /// does.</para>
    /// </summary>
    public sealed class BubbleKitLink : ScriptableObject
    {
        /// <summary>The Resources key the link loads from (its file is
        /// <c>Assets/_Project/Art/UI/Resources/BubbleKitLink.asset</c>).</summary>
        public const string ResourceKey = "BubbleKitLink";

        [Tooltip("DialogueBubbleKit.PiecePanel — the cream 9-slice with the 1 px ink ring and chamfered corners.")]
        public Sprite Panel;

        [Tooltip("DialogueBubbleKit.PieceGold — the chamfer-1 gold 9-slice: the selected-row pill.")]
        public Sprite Gold;

        [Tooltip("DialogueBubbleKit.ShippedCursor — the pointing hand that rests beside the selected row.")]
        public Sprite Cursor;

        private static BubbleKitLink _loaded;
        private static bool _tried;

        /// <summary>The link, or null when it is not there. Loaded once and cached; explicit
        /// <c>== null</c> so a destroyed asset is not handed out forever.</summary>
        public static BubbleKitLink Shipped
        {
            get
            {
                if (_loaded == null && !_tried)
                {
                    _tried = true;
                    _loaded = Resources.Load<BubbleKitLink>(ResourceKey);
                }
                return _loaded != null ? _loaded : null;
            }
        }

        /// <summary>Forget the cache (fixtures). The game never calls it.</summary>
        public static void ForgetCache()
        {
            _loaded = null;
            _tried = false;
        }
    }
}
