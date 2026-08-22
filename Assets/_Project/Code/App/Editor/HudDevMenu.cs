using HiddenHarbours.UI;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Put the hidden HUD readouts back, for debugging</b> — the escape hatch the owner's 2026-08-22
    /// diegetic ruling asked for.
    ///
    /// <para>The ruling took the tide, the sea state, the wind and the money off the band: information
    /// in this game is earned, and a band that hands you four numbers for free deletes that. But they
    /// are still the fastest way to see whether the tide model is turning when it should, or whether a
    /// sale actually paid — so the ruling kept them reachable, and this is the door. It flips
    /// <see cref="HudVisibilityPolicy.DevShowAll"/>; the HUD notices on its next frame.</para>
    ///
    /// <para>A menu item rather than a dev key, following <see cref="AffordanceDevMenu"/> and
    /// <c>WardrobeDevMenu</c>: the key ledger is exhausted A–Z, and this is a developer's switch, not an
    /// input the game is gaining.</para>
    ///
    /// <para><b>Not a scaffold with an end date.</b> Unlike the affordance menu this one stays: the
    /// readouts are hidden by policy forever, so the way back to them has to outlive the decision.</para>
    /// </summary>
    public static class HudDevMenu
    {
        private const string ShowPath = "Hidden Harbours/Dev/HUD/Show the hidden readouts";

        /// <summary>
        /// A CHECKED item, so the menu says which way the switch is without anyone having to press it
        /// and watch. <see cref="MenuItem"/>'s validate overload paints the tick.
        /// </summary>
        [MenuItem(ShowPath, priority = 158)]
        private static void ToggleShowAll()
        {
            HudVisibilityPolicy.DevShowAll = !HudVisibilityPolicy.DevShowAll;
            Report();
        }

        [MenuItem(ShowPath, validate = true)]
        private static bool ToggleShowAllValidate()
        {
            Menu.SetChecked(ShowPath, HudVisibilityPolicy.DevShowAll);
            return true;   // always available: there is nothing to select, and no play-mode requirement
        }

        /// <summary>
        /// Back to the shipped state in one press — both the override AND any instruments a debugging
        /// session granted, because "why is the tide showing" is a question that costs an hour when the
        /// answer is a flag someone set from a menu two days ago.
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/HUD/Reset to the diegetic HUD", priority = 159)]
        private static void ResetPolicy()
        {
            HudVisibilityPolicy.Reset();
            Report();
        }

        /// <summary>
        /// Grant the instruments themselves, rather than the blanket override — the honest rehearsal of
        /// what a player who has BOUGHT a tide clock will see, which the override cannot show you
        /// because it shows everything at once. Nothing in the game grants these yet; that is the point
        /// of being able to pretend.
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/HUD/Pretend she owns a tide clock", priority = 170)]
        private static void GrantTideClock() => Grant(HudInstruments.TideClock);

        [MenuItem("Hidden Harbours/Dev/HUD/Pretend she owns an anemometer", priority = 171)]
        private static void GrantAnemometer() => Grant(HudInstruments.Anemometer);

        [MenuItem("Hidden Harbours/Dev/HUD/Pretend she owns a sea gauge", priority = 172)]
        private static void GrantSeaGauge() => Grant(HudInstruments.SeaGauge);

        private static void Grant(HudInstruments instrument)
        {
            // Toggle, so the same item takes it away again — a one-way grant would need a second row
            // per instrument to undo, and the reset above is the blunt version of that.
            HudVisibilityPolicy.Owned ^= instrument;
            Report();
        }

        /// <summary>
        /// Say what the band will now show, and say plainly when nothing will be seen yet.
        ///
        /// <para>The policy is a static, so setting it OUT of play mode is legal and sticks — but there
        /// is no HUD to obey it until the game is running, and a developer who ticked the item in the
        /// editor and saw nothing would reasonably conclude the switch was broken. Hence the second
        /// line.</para>
        /// </summary>
        private static void Report()
        {
            string shown = Describe();

            if (!Application.isPlaying)
            {
                Debug.Log($"[HUD] Readouts: {shown}. Nothing changes on screen until you enter play " +
                          "mode — the policy is read by the live HudController.");
                return;
            }

            Debug.Log($"[HUD] Readouts: {shown}. Live on the band now.");
        }

        private static string Describe()
        {
            if (HudVisibilityPolicy.DevShowAll)
                return "ALL (dev override) — tide, wind, sea and money are on the band";

            string owned = HudVisibilityPolicy.Owned == HudInstruments.None
                ? "none"
                : HudVisibilityPolicy.Owned.ToString();

            return HudVisibilityPolicy.Owned == HudInstruments.None
                ? "none — the diegetic HUD, as shipped (money is in the notebook, N)"
                : "by instrument: " + owned;
        }
    }
}
