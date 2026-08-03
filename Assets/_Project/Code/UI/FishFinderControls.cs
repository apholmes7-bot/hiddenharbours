using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The fish finder's PUSHER RULES — the owner's <b>Ruling A</b> control mapping (ADR 0025 S3,
    /// 2026-08-03) as pure functions, so the mapping is EditMode-testable without a scene, a canvas or a
    /// mouse, and so the host has no behaviour of its own to drift from it.
    ///
    /// <para><b>Ruling A, verbatim.</b> The rig draws three keys — MODE, RANGE▲, RANGE▼ — and the finder
    /// has six jobs (range, alarm set-point, arm/disarm, units, night, fish-ID). The ruling settles it:
    /// <c>buttons[0]</c> MODE cycles what ▲/▼ adjust (RANGE → ALARM → RANGE); ▲/▼ step the ACTIVE mode;
    /// the three glass regions carry the three toggles (sonar = fish-ID, status strip = night, ruler =
    /// units); and ▼ past the shallowest set-point DISARMS while ▲ from disarmed re-arms at that minimum.
    /// <b>No new key, no new chrome, and no second alarm rule</b> — the disarm rides
    /// <see cref="DepthSounder.StepAlarm"/>'s existing clamp.</para>
    ///
    /// <para><b>What is NOT here.</b> The active ± mode and the fish-ID toggle are transient UI state the
    /// host holds and never persists: they are not preferences, they are where the player's thumb is. What
    /// this class writes into <see cref="SounderPrefs"/> — range, set-point, armed, units, night — is
    /// exactly what the save already carries (schema v9), so the finder adds no save field.</para>
    /// </summary>
    public static class FishFinderControls
    {
        /// <summary>MODE (<c>buttons[0]</c>): cycle what ▲/▼ adjust. Two modes today, so it toggles —
        /// written as a cycle because that is the ruling's word and a third mode would land here.</summary>
        public static FishRigAdjust CycleMode(FishRigAdjust current)
            => current == FishRigAdjust.Range ? FishRigAdjust.Alarm : FishRigAdjust.Range;

        /// <summary>
        /// One ▲/▼ press (<paramref name="steps"/> = ±1) in whichever mode is active. Mutates
        /// <paramref name="prefs"/> in place and returns true iff something changed, so a caller can skip
        /// a disk write on a press that hit an end stop.
        /// </summary>
        public static bool StepActive(FishRigAdjust mode, ref SounderPrefs prefs, int steps,
                                      in DepthSounderSettings sounder, in FishFinderSettings finder)
        {
            if (steps == 0) return false;
            return mode == FishRigAdjust.Range
                ? StepRange(ref prefs, steps, in finder)
                : StepAlarm(ref prefs, steps, in sounder);
        }

        private static bool StepRange(ref SounderPrefs prefs, int steps, in FishFinderSettings finder)
        {
            float from = FishRigGeometry.SafeRange(prefs.RangeMetres, finder.DefaultRangeMetres);
            float next = FishRigGeometry.StepRange(from, steps);
            if (Mathf.Approximately(next, prefs.RangeMetres)) return false;
            prefs.RangeMetres = next;
            return true;
        }

        /// <summary>
        /// The ALARM half of Ruling A. The set-point itself moves through
        /// <see cref="DepthSounder.StepAlarm"/> — the sounder's own rule, unchanged (Ruling E) — and the
        /// arm/disarm falls out of its CLAMP: a ▼ that cannot go any shallower turns the alarm off, and a
        /// ▲ from off turns it back on at that same shallowest set-point.
        /// </summary>
        private static bool StepAlarm(ref SounderPrefs prefs, int steps, in DepthSounderSettings sounder)
        {
            if (!prefs.Armed)
            {
                if (steps <= 0) return false;                    // already off; ▼ does nothing
                prefs.Armed = true;
                prefs.AlarmMetres = sounder.MinAlarmMetres;
                return true;
            }
            float stepped = DepthSounder.StepAlarm(prefs.AlarmMetres, steps, in sounder);
            if (steps < 0 && Mathf.Approximately(stepped, prefs.AlarmMetres))
            {
                prefs.Armed = false;                             // ▼ off the bottom of the travel
                return true;
            }
            if (Mathf.Approximately(stepped, prefs.AlarmMetres)) return false;   // ▲ against the top stop
            prefs.AlarmMetres = stepped;
            return true;
        }
    }
}
