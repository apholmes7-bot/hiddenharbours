using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>What the HUD yields while a helm card is up (S4.5 — one comparable value, so the
    /// controller applies it only on change).</summary>
    public readonly struct HudHelmSuppression : System.IEquatable<HudHelmSuppression>
    {
        /// <summary>A helm card of any kind is showing: the nav cluster leaves the bottom-centre
        /// (where every helm card anchors) for the bottom-left corner.</summary>
        public readonly bool MoveNavCluster;

        /// <summary>The dash carries a compass, so the HUD's heading trio (compass line, rose
        /// ribbon, needle) duplicates a mounted instrument and hides. Set-&amp;-drift and apparent
        /// wind are NOT on any dash instrument and are never hidden by this.</summary>
        public readonly bool HideCompassCluster;

        /// <summary>A big panel is up (focused helm card or an expanded instrument): the whole nav
        /// cluster and the catch celebration yield the screen to it.</summary>
        public readonly bool HideForBigPanel;

        public HudHelmSuppression(bool moveNavCluster, bool hideCompassCluster, bool hideForBigPanel)
        {
            MoveNavCluster = moveNavCluster;
            HideCompassCluster = hideCompassCluster;
            HideForBigPanel = hideForBigPanel;
        }

        public bool Equals(HudHelmSuppression other)
            => MoveNavCluster == other.MoveNavCluster
               && HideCompassCluster == other.HideCompassCluster
               && HideForBigPanel == other.HideForBigPanel;

        public override bool Equals(object obj) => obj is HudHelmSuppression s && Equals(s);

        public override int GetHashCode()
            => (MoveNavCluster ? 1 : 0) | (HideCompassCluster ? 2 : 0) | (HideForBigPanel ? 4 : 0);
    }

    /// <summary>
    /// The S4.5 HUD-yields-the-helm rule (owner ask 1: "remove any current game UI that obstructs
    /// the new boat UI"), as ONE pure mapping so it is EditMode-testable and cannot fork between
    /// elements. Two rules, applied to everything on screen at a helm:
    ///
    /// <list type="bullet">
    /// <item><b>Overlap:</b> nothing may sit over the helm card. Every helm card anchors
    /// bottom-centre, where the VS-19 nav cluster also lived — so the cluster MOVES to the
    /// bottom-left while any helm card is up, and while a big panel is up (a focused card spans most
    /// of the screen; an expanded instrument is centre-screen) the cluster and the centre-screen
    /// catch celebration HIDE outright.</item>
    /// <item><b>Duplication (data-driven, never hull-named):</b> a HUD element duplicating an
    /// instrument the CURRENT dash actually carries hides while that dash is up. Keyed on the
    /// resolved <see cref="HelmFit"/>: the heading trio hides exactly when
    /// <see cref="HelmFit.Compass"/> says a compass is mounted. The shipped fleet makes the
    /// negative control REAL, not hypothetical: the two skiff consoles author a dome compass, the
    /// two PILOTHOUSE consoles author NONE — so a Novi/Cape helm keeps the HUD heading trio (its
    /// only heading read), and so does every tiller boat. Set-&amp;-drift and apparent wind
    /// duplicate nothing any dash carries and only ever move/yield, never duplication-hide.</item>
    /// </list>
    ///
    /// <para>Clock, tide, wind, sea and money live in the top band, which no helm card reaches —
    /// they never suppress (no dash shows them; P1 keeps them glanceable). On foot every flag is
    /// false and the HUD is untouched.</para>
    /// </summary>
    public static class HudHelmSuppressionRule
    {
        /// <summary>Derive the HUD's posture from the live helm state. Pure — every input is a
        /// value, so the truth table pins in EditMode.</summary>
        /// <param name="helmCardShowing">Any helm card is up (tiller, lever, or composed dash).</param>
        /// <param name="dashShowing">The composed dash specifically (its brow can mount a compass).</param>
        /// <param name="dashCompass">The RESOLVED fit's compass mount (only read when the dash shows).</param>
        /// <param name="helmFocused">The helm card is in its big FOCUSED state.</param>
        /// <param name="instrumentExpanded">A dash instrument is expanded to its big card.</param>
        public static HudHelmSuppression Derive(bool helmCardShowing, bool dashShowing,
                                                CompassMount dashCompass, bool helmFocused,
                                                bool instrumentExpanded)
        {
            bool bigPanel = (helmCardShowing && helmFocused) || instrumentExpanded;
            return new HudHelmSuppression(
                moveNavCluster: helmCardShowing,
                hideCompassCluster: helmCardShowing && dashShowing && dashCompass != CompassMount.None,
                hideForBigPanel: bigPanel);
        }
    }
}
