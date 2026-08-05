using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>Where the HUD's VS-19 nav cluster draws — or whether it draws at all.</summary>
    public enum NavClusterPlacement
    {
        /// <summary>Not drawn.</summary>
        Hidden = 0,

        /// <summary>Its authored home: bottom-CENTRE, a natural compass spot.</summary>
        BottomCentre = 1,

        /// <summary>Bottom-LEFT, out from under the bottom-centre helm dash.</summary>
        ClearOfTheDash = 2,
    }

    /// <summary>
    /// What the always-on HUD gives up while a helm dash is on screen (ADR 0025 S4.5 — the owner's
    /// "remove any current game UI that obstructs the new boat UI"). A pure mapping, so the rule is
    /// EditMode-pinned with both its positive and its negative case and nothing has to be eyeballed.
    ///
    /// <para><b>Two rules, and neither is a list of hulls.</b>
    /// <list type="number">
    /// <item><b>Duplication.</b> A HUD read that the CURRENT dash actually carries hides while that
    /// dash is up — the dash's compass is the better instrument and having both is the clutter. Keyed
    /// on the resolved <see cref="HelmFit"/>, never on a hull or rig name, so a compass bought,
    /// swapped or dev-cycled onto a boat moves the HUD with it and nothing has to be kept in step.</item>
    /// <item><b>Overlap.</b> Both the dash card and the nav cluster are anchored BOTTOM-CENTRE, so
    /// even a dash with no compass to duplicate is sitting under the cluster. There the cluster MOVES
    /// rather than hides: it is the only heading, set-and-drift and apparent-wind read that boat
    /// has.</item>
    /// </list></para>
    ///
    /// <para><b>⚠ The shipped fleet needs both rules.</b> It is tempting to read "all four consoled
    /// hulls carry a compass, so the cluster always hides at a dash" — and it is FALSE against the
    /// authored data. <c>ConsoleSkiffHelm</c>/<c>SportSkiffHelm</c> ship <c>DefaultCompass: Dome</c>;
    /// <c>NoviHelm</c>/<c>CapeIslanderHelm</c> ship <c>DefaultCompass: None</c> (they support both
    /// mounts, but a fresh one is fitted with neither). So the two wheelhouses — the newest dashes in
    /// the fleet — are exactly the live case where hiding the cluster would delete the player's only
    /// heading read. Verified against the four <c>.asset</c> files, and pinned in
    /// <c>HelmHudSuppressionTests</c> so an authored change to a default surfaces as a red test rather
    /// than as a helm you cannot steer by.</para>
    /// </summary>
    public static class HelmHudSuppression
    {
        /// <summary>
        /// The ONE predicate for "a helm dash is on screen", shared with
        /// <see cref="HelmOverlayHost"/> so the HUD can never yield to a dash that is not there (or
        /// keep obstructing one that is). Mirrors the host's own gate: a lever-style helm whose hull
        /// actually has a console renderer. A tiller hull, a rowed dory or a hull with no console
        /// shows the lone control card instead, which is small, corner-parked and obstructs nothing.
        /// </summary>
        public static bool DashIsUp(HelmControlStyle style, in HelmFit fit)
            => style == HelmControlStyle.Lever && fit.HasConsole;

        /// <summary>
        /// Where the nav cluster (heading + rose ribbon + needle + set-and-drift + apparent wind)
        /// belongs right now.
        ///
        /// <list type="bullet">
        /// <item>ashore, or aboard with no live kinematics → <see cref="NavClusterPlacement.Hidden"/>
        /// (unchanged: it is a sailing read);</item>
        /// <item>aboard, no dash → <see cref="NavClusterPlacement.BottomCentre"/>, untouched;</item>
        /// <item>at a dash that carries a compass → <see cref="NavClusterPlacement.Hidden"/>
        /// (duplication);</item>
        /// <item>at a dash with no compass → <see cref="NavClusterPlacement.ClearOfTheDash"/>
        /// (overlap, read preserved).</item>
        /// </list>
        /// </summary>
        public static NavClusterPlacement NavCluster(bool aboard, HelmControlStyle style, in HelmFit fit)
        {
            if (!aboard) return NavClusterPlacement.Hidden;
            if (!DashIsUp(style, in fit)) return NavClusterPlacement.BottomCentre;
            return fit.Compass != CompassMount.None
                ? NavClusterPlacement.Hidden
                : NavClusterPlacement.ClearOfTheDash;
        }
    }
}
