using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>How hard a piece of work is, for <see cref="EffortGate"/>. Which classes may be refused is
    /// data (<see cref="EffortSettings"/>), never this enum.</summary>
    public enum EffortClass
    {
        /// <summary>Walking, eating, drinking, sleeping, sitting, talking, steering: anything needed to
        /// recover or to get home. Shipped never refused.</summary>
        Light = 0,

        /// <summary>Steady work that is not a heave (casting a line, working the deck). Shipped never refused.</summary>
        Moderate = 1,

        /// <summary>Heavy physical work: hauling a pot, digging clams, carrying a heavy load, gym training.
        /// The only class shipped refusable.</summary>
        Heavy = 2,
    }

    /// <summary>Why a piece of work would not start: a reason code, never a sentence.</summary>
    public enum EffortRefusal
    {
        /// <summary>Go ahead.</summary>
        None = 0,
        /// <summary>Too tired.</summary>
        Tired = 1,
        /// <summary>Too hungry.</summary>
        Hungry = 2,
        /// <summary>Too tired and too hungry.</summary>
        Both = 3,
    }

    /// <summary>
    /// <b>The "too tired / too hungry" numbers</b> (owner ask 2026-11-15: "when a player is too tired to do
    /// an action they will say im too tired or im too hungry or both"), one block on <see cref="GameConfig"/>,
    /// read through <see cref="EffortGate.Settings"/> so an unwired config falls back to <see cref="Default"/>.
    /// Every value is a first-pass guess for the owner to tune (rule 6).
    ///
    /// <para><b>The shipped leans.</b> Only heavy work refuses. Tiredness is the main gate; hunger slows,
    /// never stops (owner ruling 2026-11-14), so hunger alone refuses only at the very top of the scale.
    /// A threshold at or below 0 never refuses (a zeroed block can never lock the player out).</para>
    /// </summary>
    [Serializable]
    public struct EffortSettings
    {
        [Header("Which work may be refused")]
        [Tooltip("Light work (walking, eating, sleeping, sitting, talking, steering) may be refused. Shipped " +
                 "OFF: anything needed to recover or get home must always work.")]
        public bool RefuseLight;

        [Tooltip("Moderate work (casting, deck work) may be refused. Shipped OFF.")]
        public bool RefuseModerate;

        [Tooltip("Heavy work (hauling pots, digging clams, heavy carrying, gym training) may be refused. Shipped ON.")]
        public bool RefuseHeavy;

        [Header("Thresholds (need levels, 0 fed/rested .. 1 starving/worn right out)")]
        [Tooltip("Tiredness at or above this refuses refusable work. 0.9 is the 'worn out' stage (~16 game " +
                 "hours awake). 0 or below = tiredness never refuses.")]
        [Range(0f, 1f)] public float TiredRefuseAt;

        [Tooltip("Hunger at or above this refuses refusable work on its own. Hunger slows, it never stops, so " +
                 "this sits at the very top: 0.97 is the last half hour before fully starving. 0 or below = " +
                 "hunger alone never refuses.")]
        [Range(0f, 1f)] public float HungryRefuseAt;

        [Tooltip("When tiredness refuses, hunger at or above this is named too ('too tired and too hungry'). " +
                 "It never refuses by itself. 0.75 is the 'hungry' stage. 0 or below = never named.")]
        [Range(0f, 1f)] public float HungryAlsoSaidAt;

        [Header("What the player says")]
        [Tooltip("Said when tiredness refuses.")]
        public string TiredLine;

        [Tooltip("Said when hunger refuses.")]
        public string HungryLine;

        [Tooltip("Said when both refuse.")]
        public string BothLine;

        [Tooltip("Real seconds before a refusal line may be said again (a held key must not stack bubbles).")]
        [Min(0f)] public float LineCooldownSeconds;

        [Tooltip("How far above the player's origin the line's bubble hangs, metres (the head height an " +
                 "unanchored speaker uses).")]
        [Min(0f)] public float BubbleHeightMetres;

        [Header("Sitting")]
        [Tooltip("Sitting takes tiredness away at this share of the sleeping rate " +
                 "(NeedsSettings.SleepReliefPerGameHour). 0.25 = a quarter.")]
        [Range(0f, 1f)] public float SitReliefShareOfSleep;

        [Tooltip("Sitting never brings tiredness below this; only sleep clears the rest. 0.4 is under the " +
                 "'tired' stage but well short of rested.")]
        [Range(0f, 1f)] public float SitRestFloor;

        /// <summary>The shipped starting numbers (guesses for the owner to tune by feel).</summary>
        public static EffortSettings Default => new EffortSettings
        {
            RefuseLight = false,
            RefuseModerate = false,
            RefuseHeavy = true,
            TiredRefuseAt = 0.9f,       // the "worn out" stage
            HungryRefuseAt = 0.97f,     // the extreme floor: hunger slows, never stops
            HungryAlsoSaidAt = 0.75f,   // the "hungry" stage, named alongside tiredness
            TiredLine = "I'm too tired.",
            HungryLine = "I'm too hungry.",
            BothLine = "I'm too tired and too hungry.",
            LineCooldownSeconds = 4f,
            BubbleHeightMetres = 2.1f,
            SitReliefShareOfSleep = 0.25f,
            SitRestFloor = 0.4f,
        };
    }

    /// <summary>
    /// A piece of work would not start because the player is too tired, too hungry or both (owner ask
    /// 2026-11-15). Published once per refusal by <see cref="EffortGate.TryBegin"/>, with a reason code and
    /// no text, so audio and the rig can react. Presentation only: nothing decides gameplay off it.
    /// </summary>
    public readonly struct EffortRefused
    {
        public readonly EffortClass Effort;
        public readonly EffortRefusal Reason;
        /// <summary>The stable id of the work asked for (<c>effort.trap_haul</c>), or null.</summary>
        public readonly string ActionId;

        public EffortRefused(EffortClass effort, EffortRefusal reason, string actionId)
        {
            Effort = effort;
            Reason = reason;
            ActionId = actionId;
        }
    }

    /// <summary>
    /// <b>Am I up to this?</b> The one shared check a piece of work asks BEFORE it starts (owner ask
    /// 2026-11-15). Given the work's <see cref="EffortClass"/> and the needs now it allows, or refuses with
    /// <see cref="EffortRefusal.Tired"/>, <see cref="EffortRefusal.Hungry"/> or <see cref="EffortRefusal.Both"/>.
    ///
    /// <para><b>What it never does.</b> Work already under way is never interrupted (the check is asked at
    /// the start only, and says yes to anything under way). It never stops walking, eating, sleeping,
    /// sitting, talking or steering: those are Light and Light is shipped unrefusable. It never draws: on a
    /// refusal the player SAYS the line through the ambient speech bubble (<see cref="AmbientSpeechRequested"/>,
    /// the way the campaign barrier speaks for her), never a HUD text.</para>
    ///
    /// <para><b>Rule 4.</b> Core only: callers in Fishing, Player or Boats ask here and never learn about
    /// each other; the needs come through <see cref="INeedsService"/>. Deterministic: the five-argument <c>Check</c>
    /// is pure; only the line's cooldown reads real time, and nothing in the simulation reads that.</para>
    /// </summary>
    public static class EffortGate
    {
        /// <summary>Stable ids of the work wired to the gate today (<c>effort.snake_case</c>, append-only).</summary>
        public const string ClamDigId = "effort.clam_dig";
        /// <summary>Starting to haul a pot over the rail.</summary>
        public const string TrapHaulId = "effort.trap_haul";

        private static float _sayAgainAt = float.NegativeInfinity;

        /// <summary>The effort numbers, with the fallback for an unwired config.</summary>
        public static EffortSettings Settings
            => GameServices.Config != null ? GameServices.Config.Effort : EffortSettings.Default;

        /// <summary>May work of this class be refused at all?</summary>
        public static bool IsRefusable(EffortClass effort, in EffortSettings s) => effort switch
        {
            EffortClass.Light => s.RefuseLight,
            EffortClass.Moderate => s.RefuseModerate,
            EffortClass.Heavy => s.RefuseHeavy,
            _ => false,
        };

        /// <summary>
        /// <b>The pure check.</b> <paramref name="alreadyUnderway"/> work is always allowed (never
        /// interrupted). A class the settings do not refuse is always allowed. Otherwise tiredness at or above
        /// <see cref="EffortSettings.TiredRefuseAt"/> refuses (naming hunger too from
        /// <see cref="EffortSettings.HungryAlsoSaidAt"/>), and hunger alone refuses only at or above
        /// <see cref="EffortSettings.HungryRefuseAt"/>. A threshold at or below 0 never fires.
        /// </summary>
        public static EffortRefusal Check(EffortClass effort, bool alreadyUnderway, float hunger, float tiredness,
                                          in EffortSettings s)
        {
            if (alreadyUnderway || !IsRefusable(effort, s)) return EffortRefusal.None;
            bool tired = s.TiredRefuseAt > 0f && tiredness >= s.TiredRefuseAt;
            bool hungry = s.HungryRefuseAt > 0f && hunger >= s.HungryRefuseAt;
            if (tired && !hungry && s.HungryAlsoSaidAt > 0f && hunger >= s.HungryAlsoSaidAt) hungry = true;
            if (tired && hungry) return EffortRefusal.Both;
            if (tired) return EffortRefusal.Tired;
            return hungry ? EffortRefusal.Hungry : EffortRefusal.None;
        }

        /// <summary>The check against the local player's needs now (<see cref="GameServices.Needs"/>) and the
        /// config. Changes nothing, publishes nothing.</summary>
        public static EffortRefusal Check(EffortClass effort, bool alreadyUnderway = false, INeedsService needs = null)
        {
            needs ??= GameServices.Needs;
            if (needs == null) return EffortRefusal.None;
            return Check(effort, alreadyUnderway, needs.Level(NeedKind.Hunger), needs.Level(NeedKind.Tiredness),
                         Settings);
        }

        /// <summary>
        /// Ask before starting work: true = go ahead. On a refusal publishes <see cref="EffortRefused"/> once
        /// and the player says the line for the reason (at most once per
        /// <see cref="EffortSettings.LineCooldownSeconds"/>), then returns false; the caller starts nothing.
        /// </summary>
        public static bool TryBegin(EffortClass effort, string actionId = null, INeedsService needs = null)
        {
            EffortRefusal reason = Check(effort, false, needs);
            if (reason == EffortRefusal.None) return true;
            EventBus.Publish(new EffortRefused(effort, reason, actionId));
            Say(reason, Settings, Time.unscaledTime);
            return false;
        }

        /// <summary>The line for <paramref name="reason"/>, or null for <see cref="EffortRefusal.None"/>.</summary>
        public static string LineFor(EffortRefusal reason, in EffortSettings s) => reason switch
        {
            EffortRefusal.Tired => s.TiredLine,
            EffortRefusal.Hungry => s.HungryLine,
            EffortRefusal.Both => s.BothLine,
            _ => null,
        };

        /// <summary>
        /// The player says the refusal line: an ambient bubble anchored at her (<see cref="GameServices.PlayerTransform"/>),
        /// the channel the campaign barrier's spoken line uses. Nothing with no line or inside the cooldown.
        /// Returns true when a line went out. <paramref name="realSeconds"/> is the presentation clock.
        /// </summary>
        public static bool Say(EffortRefusal reason, in EffortSettings s, float realSeconds)
        {
            string line = LineFor(reason, s);
            if (string.IsNullOrEmpty(line) || realSeconds < _sayAgainAt) return false;
            _sayAgainAt = realSeconds + (s.LineCooldownSeconds > 0f ? s.LineCooldownSeconds : 0f);
            EventBus.Publish(new AmbientSpeechRequested(
                AmbientSpeechId.Next(), GameServices.PlayerTransform, new Vector3(0f, s.BubbleHeightMetres, 0f),
                speakerId: null, speakerName: null, line: line, voiceId: null, kind: AmbientSpeechKind.InnerVoice));
            return true;
        }

        /// <summary>Forget the line cooldown (fixtures only; the game never calls it).</summary>
        public static void ResetSpeech() => _sayAgainAt = float.NegativeInfinity;

        /// <summary>
        /// <b>Sitting rests a little</b> (lean 2026-11-15): tiredness after sitting
        /// <paramref name="gameHoursSat"/> in-game hours from <paramref name="tirednessAtSitDown"/>. It falls at
        /// <see cref="EffortSettings.SitReliefShareOfSleep"/> of <paramref name="sleepReliefPerGameHour"/>
        /// and never below <see cref="EffortSettings.SitRestFloor"/>, which only sleep clears; a level already
        /// at or under the floor is left as it is. Pure. A zero rate rests nothing.
        /// </summary>
        public static float TirednessAfterSitting(float tirednessAtSitDown, double gameHoursSat,
                                                  float sleepReliefPerGameHour, in EffortSettings s)
        {
            float level = Mathf.Clamp01(tirednessAtSitDown);
            if (!(gameHoursSat > 0d) || !(sleepReliefPerGameHour > 0f) || !(s.SitReliefShareOfSleep > 0f)) return level;
            float floor = Mathf.Clamp01(s.SitRestFloor);
            if (level <= floor) return level;
            double after = level - (double)sleepReliefPerGameHour * s.SitReliefShareOfSleep * gameHoursSat;
            return after > floor ? (float)after : floor;
        }
    }
}
