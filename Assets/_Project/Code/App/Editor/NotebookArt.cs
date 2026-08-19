using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE ONE LOOKUP</b> for the baked notebook kit and the face — and the one place a region
    /// builder asks for them.
    ///
    /// <para><b>Right art or no art, never wrong art.</b> Every method returns null when the piece is
    /// not on disk, and the presenter's tinted-rect fallback stands behind every one. A book drawn
    /// with the wrong sprite looks finished and is wrong, which is far worse to find than a grey
    /// rectangle that is obviously unfinished — the same rule <see cref="DialogueBubbleArt"/> states
    /// and for the same reason.</para>
    ///
    /// <para>Editor-only, like the builders that call it: the presenter holds plain <c>Sprite</c> and
    /// <c>Font</c> references wired at build time and reads no asset path at runtime.</para>
    /// </summary>
    public static class NotebookArt
    {
        /// <summary>Load one baked piece by its name in <see cref="NotebookKit.Pieces"/>, or null when
        /// the kit has not been baked.</summary>
        public static Sprite TryLoad(string piece)
        {
            if (string.IsNullOrEmpty(piece)) return null;
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{NotebookKit.ArtFolder}/{piece}.png");
        }

        /// <summary>True once every piece resolves — the readable half of "is the book still
        /// greybox".</summary>
        public static bool IsBaked()
        {
            foreach (string piece in NotebookKit.Pieces)
                if (TryLoad(piece) == null) return false;
            return true;
        }

        /// <summary>The face, or null when it has not been baked. Null is a real answer: the presenter
        /// sets in the built-in legacy font, which is legible and obviously not the book's hand.</summary>
        public static Font TryLoadFace() => AssetDatabase.LoadAssetAtPath<Font>(HarbourType.FontPath);

        /// <summary>
        /// Wire a presenter with everything that exists right now.
        ///
        /// <para><b>The pieces the OWNER picked</b>, not every alternate on the taste board — the kit's
        /// <c>Shipped*</c> constants name them, so changing the owner's mind is a one-constant edit in
        /// <see cref="NotebookKit"/> rather than an edit here.</para>
        /// </summary>
        public static void Wire(NotebookPresenter presenter)
        {
            if (presenter == null) return;

            presenter.WireKitArt(
                stock: TryLoad(NotebookKit.ShippedStock),
                box: TryLoad(NotebookKit.ShippedBox),
                boxTicked: TryLoad(NotebookKit.PieceBoxTicked),
                currentMark: TryLoad(NotebookKit.ShippedCurrentMark),
                tab: TryLoad(NotebookKit.ShippedTab),
                ribbon: TryLoad(NotebookKit.PieceRibbon),
                select: TryLoad(NotebookKit.ShippedSelect),
                cursor: TryLoad(NotebookKit.PieceCursorHand),
                markNext: TryLoad(NotebookKit.PieceMarkNext),
                markPrev: TryLoad(NotebookKit.PieceMarkPrev),
                stampDone: TryLoad(NotebookKit.PieceStampDone),
                face: TryLoadFace());
        }
    }
}
