using HiddenHarbours.Art;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Cycle the interact affordance's three looks, in play</b> — the owner's way to pick one by eye.
    ///
    /// <para>The lane shipped three variants on purpose: a shade lift, a breathing outline, and both at
    /// half strength. Which one is right is not an assertable fact — it is a judgement about how loud a
    /// dim room should get when you face a bed, and the only way to make it is to stand in the room and
    /// look. This cycles them on the live presenter so the walk can settle it in one pass instead of
    /// three builds.</para>
    ///
    /// <para>A menu item rather than a dev key, following <see cref="WardrobeDevMenu"/>: the key ledger is
    /// exhausted A–Z, and this is a scaffold with a known end date, not an input the game is gaining.</para>
    ///
    /// <para><b>SCAFFOLD — delete with the losers.</b> When the owner picks a variant, the other two go and
    /// this menu goes with them. It exists to make a choice, not to be a setting.</para>
    /// </summary>
    public static class AffordanceDevMenu
    {
        [MenuItem("Hidden Harbours/Dev/Affordance/Cycle", priority = 154)]
        private static void Cycle()
        {
            InteractAffordancePresenter.Variant =
                InteractAffordanceStyle.Next(InteractAffordancePresenter.Variant);
            Report("Cycled");
        }

        [MenuItem("Hidden Harbours/Dev/Affordance/Shade lift", priority = 155)]
        private static void PickLift() => Pick(InteractAffordanceVariant.ShadeLift);

        [MenuItem("Hidden Harbours/Dev/Affordance/Outline pulse", priority = 156)]
        private static void PickOutline() => Pick(InteractAffordanceVariant.OutlinePulse);

        [MenuItem("Hidden Harbours/Dev/Affordance/Both at half strength", priority = 157)]
        private static void PickBoth() => Pick(InteractAffordanceVariant.Both);

        private static void Pick(InteractAffordanceVariant variant)
        {
            InteractAffordancePresenter.Variant = variant;
            Report("Wearing");
        }

        /// <summary>
        /// Say what happened, and say plainly when nothing will be seen yet.
        ///
        /// <para>The variant is a static, so setting it OUT of play mode is legal and sticks — but nothing
        /// is drawn until there is a presenter and a candidate, and an owner who cycled three times in the
        /// editor and saw nothing would reasonably conclude the feature was broken. Hence the second
        /// line.</para>
        /// </summary>
        private static void Report(string verb)
        {
            InteractAffordanceVariant variant = InteractAffordancePresenter.Variant;

            string described = variant switch
            {
                InteractAffordanceVariant.ShadeLift =>
                    "the candidate lifts one step up its own ramp, no outline",
                InteractAffordanceVariant.OutlinePulse =>
                    "a 1px edge in the sprite's own darkest step, breathing at ~0.5 Hz",
                _ => "both, each at half strength",
            };

            if (!Application.isPlaying)
            {
                Debug.Log($"[Affordance] {verb} to {variant} — {described}. Nothing is drawn until you " +
                          "enter play mode and face something interactable.");
                return;
            }

            // The presenter re-reads the variant every frame it is drawing, so this lands on whatever the
            // owner is facing RIGHT NOW. Saying which case they are in saves a "did that work?" round trip.
            string standing = InteractAffordancePresenter.Instance != null &&
                              InteractAffordancePresenter.Instance.IsShowing
                ? " Live on the fixture you are facing."
                : " Face something interactable to see it.";

            Debug.Log($"[Affordance] {verb} to {variant} — {described}.{standing}");
        }
    }
}
