using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What the helm's UI covers at the bottom of the screen. Append-only: a Core
    /// contract (ADR 0050).</summary>
    public enum HelmFootprintKind
    {
        /// <summary>Nothing: no helm UI is up — ashore, a passenger, hide-all, a headless run.</summary>
        None = 0,

        /// <summary>A band across the WHOLE width of the screen, from its bottom edge up.</summary>
        Band = 1,

        /// <summary>One card's rect, wherever the player has it: today's dash card or the tiller's.</summary>
        Card = 2,
    }

    /// <summary>
    /// <b>One statement of what the helm's UI covers</b>, in SCREEN PIXELS: origin at the bottom-left,
    /// y up — the frame <c>Screen.width</c>/<c>Screen.height</c> and the pointer speak, and the one the
    /// helm host already lays its card out in. Each reader turns it into its own canvas units.
    ///
    /// <para><b>The two rules every reader shares live here</b>, so the toast, the popup, the quest note
    /// and the nav cluster cannot drift into four readings of "keep clear":
    /// <list type="bullet">
    /// <item><b>A full-width band is the new bottom edge.</b> An overlay that stood on the screen's
    /// edge stands on the band's top instead: it rises by the band's height (its own layout above the
    /// edge kept, so two overlays stacked today are stacked the same way over the band), and further if
    /// its box started below the edge.</item>
    /// <item><b>A card is stepped over.</b> An overlay whose box reaches the card rises until it clears
    /// the card's top by the overlay's own gap; one that sits beside or wholly below the card stays
    /// put.</item>
    /// </list></para>
    ///
    /// <para><b>Nothing covered means nothing moves</b>: every lift is exactly 0 for
    /// <see cref="HelmFootprintKind.None"/>, which is what keeps every reader where it was, bit-exact,
    /// on foot, at hide-all and in a headless run.</para>
    /// </summary>
    public readonly struct HelmFootprintArea : IEquatable<HelmFootprintArea>
    {
        /// <summary>What kind of area this is.</summary>
        public readonly HelmFootprintKind Kind;

        /// <summary>
        /// A card's rect, in screen pixels. For a band: <c>(0, 0, 0, height)</c> — a band spans every
        /// column, so its width is neither stored nor read. All zero for nothing.
        /// </summary>
        public readonly Rect Rect;

        private HelmFootprintArea(HelmFootprintKind kind, Rect rect)
        {
            Kind = kind;
            Rect = rect;
        }

        /// <summary>Nothing covered. Also the struct's default.</summary>
        public static HelmFootprintArea None => default;

        /// <summary>A band across the whole width, <paramref name="heightPx"/> tall from the bottom
        /// edge. Nothing for a height that is not positive.</summary>
        public static HelmFootprintArea FullWidthBand(float heightPx)
            => heightPx > 0f
                ? new HelmFootprintArea(HelmFootprintKind.Band, new Rect(0f, 0f, 0f, heightPx))
                : default;

        /// <summary>One card's rect, in screen pixels. Nothing for an empty rect.</summary>
        public static HelmFootprintArea OfCard(Rect rectPx)
            => rectPx.width > 0f && rectPx.height > 0f
                ? new HelmFootprintArea(HelmFootprintKind.Card, rectPx)
                : default;

        /// <summary>True when the helm's UI covers nothing.</summary>
        public bool IsNone => Kind == HelmFootprintKind.None;

        /// <summary>True for a band across the whole width — the one kind the camera answers.</summary>
        public bool SpansFullWidth => Kind == HelmFootprintKind.Band;

        /// <summary>The covered area's top edge, in screen pixels. 0 when nothing is covered.</summary>
        public float TopPx => Kind == HelmFootprintKind.None ? 0f : Rect.yMax;

        /// <summary>The full-width height, in screen pixels: the band's, and 0 for a card or for
        /// nothing.</summary>
        public float FullWidthHeightPx => Kind == HelmFootprintKind.Band ? Rect.height : 0f;

        /// <summary>
        /// True when <paramref name="boxPx"/> (screen pixels) shares any area with what is covered.
        /// Edges that only touch do not overlap, and an empty box overlaps nothing.
        /// </summary>
        public bool Overlaps(Rect boxPx)
        {
            if (Kind == HelmFootprintKind.None) return false;
            if (!(boxPx.width > 0f) || !(boxPx.height > 0f)) return false;
            if (!(boxPx.yMin < Rect.yMax) || !(boxPx.yMax > Rect.yMin)) return false;
            if (Kind == HelmFootprintKind.Band) return true;
            return boxPx.xMin < Rect.xMax && boxPx.xMax > Rect.xMin;
        }

        /// <summary>
        /// How far (screen pixels, never negative) an overlay whose box is <paramref name="homePx"/>
        /// must rise to keep out of what is covered. Exactly 0 when nothing is covered.
        ///
        /// <list type="bullet">
        /// <item><b>Band:</b> the band's height, plus whatever of the box hangs below the screen's edge.
        /// <paramref name="gapPx"/> is not used: the overlay's own distance from the edge is its
        /// gap.</item>
        /// <item><b>Card:</b> 0 unless the box, or the <paramref name="gapPx"/> above the card, reaches
        /// the card; then up to <c>card top + gap</c>.</item>
        /// </list>
        /// </summary>
        public float LiftToClear(Rect homePx, float gapPx)
        {
            switch (Kind)
            {
                case HelmFootprintKind.Band:
                    return Rect.height + Mathf.Max(0f, -homePx.yMin);

                case HelmFootprintKind.Card:
                {
                    float gap = Mathf.Max(0f, gapPx);
                    var reach = new Rect(Rect.x, Rect.y, Rect.width, Rect.height + gap);
                    if (!(homePx.width > 0f) || !(homePx.height > 0f)) return 0f;
                    bool meets = homePx.xMin < reach.xMax && homePx.xMax > reach.xMin
                              && homePx.yMin < reach.yMax && homePx.yMax > reach.yMin;
                    return meets ? reach.yMax - homePx.yMin : 0f;
                }

                default:
                    return 0f;
            }
        }

        public bool Equals(HelmFootprintArea other) => Kind == other.Kind && Rect == other.Rect;

        public override bool Equals(object obj) => obj is HelmFootprintArea other && Equals(other);

        public override int GetHashCode() => ((int)Kind * 397) ^ Rect.GetHashCode();

        public static bool operator ==(HelmFootprintArea a, HelmFootprintArea b) => a.Equals(b);

        public static bool operator !=(HelmFootprintArea a, HelmFootprintArea b) => !a.Equals(b);

        public override string ToString()
            => Kind == HelmFootprintKind.None ? "None" : $"{Kind} {Rect}";
    }

    /// <summary>Raised when what the helm's UI covers changes. Published on a genuine change only —
    /// a card at rest publishes once, not every frame.</summary>
    public readonly struct HelmFootprintChanged
    {
        public readonly HelmFootprintArea Current;
        public readonly HelmFootprintArea Previous;

        public HelmFootprintChanged(HelmFootprintArea current, HelmFootprintArea previous)
        {
            Current = current;
            Previous = previous;
        }
    }

    /// <summary>
    /// <b>What the helm's UI covers at the bottom of the screen</b> — the one Core seam every surface
    /// that draws near the bottom edge reads, so none of them has to name the helm's UI (rule 4;
    /// ADR 0050). The helm host publishes; the toast (Player), the quest note (World), the interact
    /// popup and the nav cluster (UI) keep out of it; the camera (App) answers a full-width band by
    /// easing the boat up by half its height.
    ///
    /// <para><b>Nothing until told otherwise.</b> The default is <see cref="HelmFootprintKind.None"/>,
    /// and nothing here reads a screen, a canvas or a service — a headless run, EditMode, and every
    /// scene with no helm UI up see nothing covered, so every reader sits exactly where it did before
    /// this seam existed.</para>
    ///
    /// <para><b>Presentation, never state.</b> Recomputed by the host every frame it draws, cleared the
    /// frame it stops, reset at every play session's start; never saved (rule 5).</para>
    ///
    /// <para><b>Poll or subscribe.</b> <see cref="Current"/> is the fact; <see cref="HelmFootprintChanged"/>
    /// is the edge. A session-long reader polls <see cref="Current"/> as its safety net, because
    /// <see cref="EventBus.Clear{T}"/> is how test suites isolate themselves (the interact popup's
    /// reasoning).</para>
    ///
    /// FLAG lead-architect: new Core contract (the helm dash band's H1 footprint seam, ADR 0050).
    /// </summary>
    public static class HelmFootprint
    {
        /// <summary>What is covered right now.</summary>
        public static HelmFootprintArea Current { get; private set; }

        /// <summary>Say what is covered. A no-op when it is what is already said.</summary>
        public static void Publish(in HelmFootprintArea area)
        {
            if (area == Current) return;
            HelmFootprintArea previous = Current;
            Current = area;
            EventBus.Publish(new HelmFootprintChanged(area, previous));
        }

        /// <summary>Say nothing is covered (the helm UI stood down).</summary>
        public static void Clear() => Publish(HelmFootprintArea.None);

        /// <summary>Back to nothing, raising nothing. For a new play session and for tests.</summary>
        public static void Reset() => Current = HelmFootprintArea.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
