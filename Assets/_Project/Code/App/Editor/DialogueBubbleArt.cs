using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.Core;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE ONE LOOKUP</b> for the baked dialogue bubble kit — and the one place a region builder
    /// asks for its sprites.
    ///
    /// <para><b>Right art or no art, never wrong art.</b> Every method here returns null when the
    /// piece is not on disk, and the presenter's tinted-rect fallback is still alive behind every
    /// one of them. That is not a temporary arrangement waiting to be tidied away: a bubble drawn
    /// with the wrong sprite looks finished and is wrong, which is far worse to find than a grey box
    /// that is obviously unfinished (the <c>TryLoadCamperSprite</c> rule, and the #555 lesson).</para>
    ///
    /// <para><b>Single-mode, so the load is the simple one.</b> Each piece bakes to its own PNG, so
    /// <see cref="AssetDatabase.LoadAssetAtPath{T}"/> resolves it directly — none of the
    /// <c>LoadAllAssetsAtPath</c> dance a Multiple-mode sheet forces (see
    /// <c>StPetersCamperLot.TryLoadCamperSprite</c>, which has to match sub-sprites by name because
    /// Unity does not promise the array's order).</para>
    ///
    /// <para>Editor-only, like the builders that call it: the presenter itself holds plain
    /// <c>Sprite</c> references, wired at build time, and reads no asset path at runtime.</para>
    /// </summary>
    public static class DialogueBubbleArt
    {
        /// <summary>Load one baked piece by its name in <see cref="DialogueBubbleKit.Pieces"/>, or
        /// null when the kit has not been baked.</summary>
        public static Sprite TryLoad(string piece)
        {
            if (string.IsNullOrEmpty(piece)) return null;
            return AssetDatabase.LoadAssetAtPath<Sprite>(
                $"{DialoguePresenter.ArtFolder}/{piece}.png");
        }

        /// <summary>True once every piece resolves — i.e. the kit has been baked and imported.
        /// The readable half of "is the bubble still greybox", for builders and for tooling.</summary>
        public static bool IsBaked()
        {
            foreach (string piece in DialogueBubbleKit.Pieces)
                if (TryLoad(piece) == null) return false;
            return true;
        }

        /// <summary>The six tails in kit order, or null entries where a piece is missing.</summary>
        public static Sprite[] TryLoadTails()
        {
            var tails = new Sprite[DialogueBubbleKit.TailPieces.Length];
            for (int i = 0; i < tails.Length; i++)
                tails[i] = TryLoad(DialogueBubbleKit.TailPieces[i]);
            return tails;
        }

        /// <summary>The two continue-marker bob frames, in order.</summary>
        public static Sprite[] TryLoadMarkerFrames() => new[]
        {
            TryLoad(DialogueBubbleKit.PieceMarker0),
            TryLoad(DialogueBubbleKit.PieceMarker1),
        };

        /// <summary>
        /// Wire every baked piece onto a presenter. Safe to call when nothing is baked — it wires
        /// what it finds and leaves the rest null, so a half-baked folder degrades piece by piece
        /// instead of all at once.
        /// </summary>
        /// <returns>True when the presenter came away fully dressed.</returns>
        public static bool Dress(DialoguePresenter presenter)
        {
            if (presenter == null) return false;

            presenter.WireKitArt(
                panel: TryLoad(DialogueBubbleKit.PiecePanel),
                gold: TryLoad(DialogueBubbleKit.PieceGold),
                tails: TryLoadTails(),
                markerFrames: TryLoadMarkerFrames(),
                caret: TryLoad(DialogueBubbleKit.PieceCaret),
                cursor: TryLoad(DialogueBubbleKit.ShippedCursor));

            return presenter.HasBubbleArt && presenter.HasTailArt;
        }
    }
}
