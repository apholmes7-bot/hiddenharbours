using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>Which brow instrument a click or an expansion is about. One value per instrument that
    /// can be MOUNTED and then expanded. All four are built.</summary>
    public enum HelmInstrumentSlot
    {
        None = 0,
        Sounder = 1,        // DepthRig — the plain depth sounder (ADR 0025 S2)
        FishFinder = 2,     // FishRig  — the colour sonar in the same cutout (S3b)
        Radar = 3,          // RadarRig — the PPI scope in the brow's CENTRE slot (S5)
        Chartplotter = 4,   // NavRig  — the chart in the brow's GPS slot (S6)
    }

    /// <summary>
    /// Which mounted instrument is currently EXPANDED (ADR 0025 S4.5 — the owner's "shown on the dash
    /// and not blown up by default; this should be selectable, which UI can be expanded").
    ///
    /// <para><b>The presentation flip, in one place.</b> Since H2 every instrument has a HOUSING in
    /// the helm band (a named plate: the click target, and on the pilot bands the depth) and a RAISED
    /// card (the standalone card S2/S3b built, where its controls are live), placed by
    /// <see cref="HelmBandLayout.RaisedCardRect"/>. The flush brow faces retired with the dash card.</para>
    ///
    /// <para><b>The raised card's rect is published here</b> (<see cref="PublishRaisedCard"/>) by the
    /// host that draws it, so the band host can leave a press inside the card to the card and treat a
    /// press anywhere else as the click-away. It clears whenever <see cref="Expanded"/> changes, so a
    /// stale rect can never swallow presses meant for the band.</para>
    ///
    /// <para><b>Why a shared arbiter and not a bool per host.</b> The hosts are independent
    /// self-installing singletons that cannot see each other, and "one expanded at a time" is a fact
    /// about the SET of them. A bool each would let two instruments open over one another the moment a
    /// hull can carry two (S5's radar is exactly that hull). The transition itself is a pure function
    /// so the single-expansion invariant is EditMode-pinned without a canvas.</para>
    ///
    /// <para><b>Transient, never persisted</b> (rule 5, and the S3b Ruling A precedent): which view a
    /// player is looking through is where their eyes are, not a preference. Nothing here touches
    /// <c>SounderPrefs</c> or <c>InstrumentLocker</c>, and a reload comes back with nothing raised.</para>
    /// </summary>
    public static class HelmInstrumentExpansion
    {
        private static HelmInstrumentSlot _expanded;
        private static bool _raisedLive;
        private static Rect _raisedCard;

        /// <summary>The instrument expanded right now, or <see cref="HelmInstrumentSlot.None"/>.</summary>
        public static HelmInstrumentSlot Expanded
        {
            get => _expanded;
            private set
            {
                if (value == _expanded) return;
                _expanded = value;
                _raisedLive = false;   // a different card (or none): the old rect is where nothing is
            }
        }

        /// <summary>
        /// The whole state machine: what a click on <paramref name="clicked"/> leaves expanded.
        /// <see cref="HelmInstrumentSlot.None"/> means the click landed away from every instrument.
        ///
        /// <list type="bullet">
        /// <item>a flush instrument → it expands, and whatever was open closes (single expansion);</item>
        /// <item>the one already expanded → it collapses (click-again, the owner's toggle);</item>
        /// <item>away → collapse.</item>
        /// </list>
        /// </summary>
        public static HelmInstrumentSlot Next(HelmInstrumentSlot expanded, HelmInstrumentSlot clicked)
        {
            if (clicked == HelmInstrumentSlot.None) return HelmInstrumentSlot.None;
            return clicked == expanded ? HelmInstrumentSlot.None : clicked;
        }

        /// <summary>Apply a click through <see cref="Next"/>.</summary>
        public static void Click(HelmInstrumentSlot clicked) => Expanded = Next(Expanded, clicked);

        /// <summary>Close whatever is open (Esc, click-away, losing the helm).</summary>
        public static void Collapse() => Expanded = HelmInstrumentSlot.None;

        /// <summary>Is this the expanded one? <see cref="HelmInstrumentSlot.None"/> is never
        /// "expanded", so a caller with no slot can ask without a special case.</summary>
        public static bool IsExpanded(HelmInstrumentSlot slot)
            => slot != HelmInstrumentSlot.None && Expanded == slot;

        /// <summary>True while any instrument is expanded — the dash host's cue to yield the pointer
        /// and Esc to it rather than toggling its own focus underneath.</summary>
        public static bool AnyExpanded => Expanded != HelmInstrumentSlot.None;

        /// <summary>The slot a <see cref="Core.SounderKind"/> occupies. The brow cutout resolves to
        /// ONE of the two, never both (<c>BoatEquipment.EffectiveFit</c>), so this is total.</summary>
        public static HelmInstrumentSlot SlotFor(Core.SounderKind sounder) => sounder switch
        {
            Core.SounderKind.Depth => HelmInstrumentSlot.Sounder,
            Core.SounderKind.Fish => HelmInstrumentSlot.FishFinder,
            _ => HelmInstrumentSlot.None,
        };

        /// <summary>The raised card's screen rect as its host drew it this frame. Ignored unless
        /// <paramref name="slot"/> is the one expanded; a zero rect withdraws it.</summary>
        public static void PublishRaisedCard(HelmInstrumentSlot slot, Rect card)
        {
            if (slot == HelmInstrumentSlot.None || slot != _expanded) return;
            _raisedCard = card;
            _raisedLive = card.width > 0f && card.height > 0f;
        }

        /// <summary>The raised card's rect, when one is up and its host has drawn it.</summary>
        public static bool TryRaisedCard(out Rect card)
        {
            card = _raisedCard;
            return _raisedLive && _expanded != HelmInstrumentSlot.None;
        }

        // Statics survive a play session when the editor's domain reload is disabled (the
        // BoatUiWindows precedent): start every session with nothing raised.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _expanded = HelmInstrumentSlot.None;
            _raisedLive = false;
            _raisedCard = default;
        }
    }
}
