using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>Which dash-mounted instrument is EXPANDED to its big card right now (S4.5). One value
    /// — one expanded instrument at a time is a type-level invariant, not a rule anyone enforces.
    /// The sounder cutout is the only expandable mount today; S5/S6's radar/chartplotter join this
    /// enum when they exist (do not add them early — rule 8).</summary>
    public enum DashInstrument
    {
        None    = 0,
        Sounder = 1,   // the brow cutout — whichever unit the fit mounts there (depth OR fish)
    }

    /// <summary>
    /// The S4.5 instrument-expansion state — the owner's brow-squash ruling (2026-08-05): instruments
    /// mount FLUSH on the dash by default; the big card is an opt-in view, selected by clicking the
    /// mounted instrument's glass on the focused dash. Click-again or click-away collapses; Esc backs
    /// out one level (expansion first, then dash focus — no new key, Esc already owns exactly this
    /// shape in the S1 hosts).
    ///
    /// <para><b>Transient UI state, never persisted</b> (rule 5; the fish finder's Ruling A
    /// precedent): where the player's eyes are is not a preference. Nothing here can reach
    /// <c>InstrumentLocker</c> or the save — there is no reference to either, and the EditMode test
    /// pins that expansion moves no prefs.</para>
    ///
    /// <para><b>One owner for every transition.</b> The dash host (<see cref="HelmOverlayHost"/>)
    /// owns expansion/collapse decisions — it knows the dash card, the mount boxes and the expanded
    /// card rect. The instrument hosts (<see cref="SounderOverlayHost"/>,
    /// <see cref="FishFinderOverlayHost"/>) only READ this to decide whether their card — now the
    /// EXPANDED presentation — is on screen. If both sides mutated it on the same click they would
    /// double-toggle (host collapses, dash re-expands), which is why the hosts never write it.</para>
    /// </summary>
    public static class HelmInstrumentExpansion
    {
        /// <summary>The expanded instrument, or <see cref="DashInstrument.None"/> (everything flush).</summary>
        public static DashInstrument Current { get; private set; }

        /// <summary>The pure toggle rule (EditMode-pinned): clicking a mount expands it, clicking the
        /// EXPANDED mount collapses it, clicking a different mount switches — so exactly one can ever
        /// be up. Clicking nothing changes nothing.</summary>
        public static DashInstrument Toggled(DashInstrument current, DashInstrument clicked)
        {
            if (clicked == DashInstrument.None) return current;
            return clicked == current ? DashInstrument.None : clicked;
        }

        /// <summary>Apply <see cref="Toggled"/> to the live state (the dash host's click handler).</summary>
        public static void Toggle(DashInstrument clicked) => Current = Toggled(Current, clicked);

        /// <summary>Collapse whatever is expanded (Esc, click-away, helm lost, the mount emptied).</summary>
        public static void Collapse() => Current = DashInstrument.None;

        /// <summary>
        /// True while the piloted hull's brow instruments are DASH-MOUNTED — a console dash is the
        /// live helm card, so the flush faces carry the glance read and the standalone cards are the
        /// EXPANDED state only. The SAME predicate the dash host uses to choose the composed dash
        /// (<see cref="HelmOverlayHost"/>), shared here so the two can never fork: a hull this says
        /// true for shows the dash, and a hull it says false for (tiller, oars, no console) keeps the
        /// S1/S2 standalone-card behaviour untouched.
        /// </summary>
        public static bool DashCarriesBrow(HelmControlStyle style, ConsoleRigKind rig)
            => style == HelmControlStyle.Lever && rig != ConsoleRigKind.None;

        /// <summary>Convenience read off the live seam (resolves <see cref="IHelmControl.Fit"/> once).</summary>
        public static bool DashCarriesBrow(IHelmControl helm)
            => helm != null && helm.HasHelm
               && DashCarriesBrow(helm.Style, helm.Fit.Rig);

        // Never persisted, and never carried across a play session either: a fresh run starts with
        // everything flush (rule 5 — transient input/UI state).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Current = DashInstrument.None;
    }
}
