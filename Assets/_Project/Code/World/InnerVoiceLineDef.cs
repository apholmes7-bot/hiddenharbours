using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>What makes the player think a thing. Append-only: a trigger is a saved-content contract
    /// the moment one line is authored against it.</summary>
    public enum InnerVoiceTrigger
    {
        /// <summary>The game is offering her a verb on something — <c>InteractOfferChanged</c>, matched
        /// on <see cref="InnerVoiceLineDef.SubjectId"/>. This is "approaching a thing" in the owner's
        /// words, and it costs nothing: the offer channel is already computed every frame for the
        /// interact popup, and it already knows the difference between walking near a rod and being
        /// close enough to pick it up.</summary>
        InteractOffered = 0,

        /// <summary>She came within <see cref="InnerVoiceLineDef.RadiusMeters"/> of an authored point in
        /// an authored region. For the things there is nothing to press ON — a hull on the hard with a
        /// price on it, a fallen fence, a light that is out.</summary>
        Proximity = 1,
    }

    /// <summary>
    /// <b>ONE THING THE PLAYER THINKS, AS DATA</b> — the owner's 2026-09-06 ruling that the speech bubble
    /// is also a clue channel: <i>"approaching a broken item and saying 'I could fix this' or other clues
    /// to the player"</i>.
    ///
    /// <para>One asset per line under <c>Data/NPCs/InnerVoice</c>, keyed by a stable append-only
    /// <see cref="Id"/> (<c>innervoice.snake_case</c>), gathered by <see cref="InnerVoiceLibrary"/> and
    /// fired by <see cref="InnerVoiceDirector"/>. Rule 2 taken literally: adding a clue is a new asset
    /// and nothing else, and the owner can rewrite every word of every one of them without a
    /// programmer.</para>
    ///
    /// <para><b>⚠ What this deliberately is NOT yet.</b> The owner's example line is <i>"I could fix
    /// this"</i>, and the game has no idea what is broken: there is no prop condition, wear or
    /// repairability model anywhere (the only repair state in the project is <c>RepairLedger</c>, a list
    /// of repaired hull ids). So the shipped clues speak about things whose state the game already knows,
    /// and a line that reacts to damage waits on a prop-condition charter of its own. Saying so here is
    /// cheaper than a future reader assuming the hook exists and is broken.</para>
    ///
    /// <para><b>It is a thought, so it takes nothing.</b> The director publishes an ambient request and
    /// walks away — no gate, no press, no pause. See <c>AmbientSpeechPresenter</c>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Inner Voice Line", fileName = "InnerVoiceLine")]
    public sealed class InnerVoiceLineDef : ScriptableObject
    {
        /// <summary>The save-flag prefix for <see cref="OnceOnly"/> lines. Namespaced so a clue id can
        /// never collide with an onboarding or deed flag in the one shared flag store.</summary>
        public const string FlagPrefix = "innervoice.said.";

        [Header("Identity")]
        [Tooltip("Stable id, append-only (innervoice.snake_case, e.g. innervoice.damaged_dory). " +
                 "Content-validated for uniqueness, and it is what the save flag is keyed on — renaming " +
                 "one makes a player who already heard it hear it again.")]
        public string Id = "innervoice.example";

        [Header("The thought")]
        [TextArea(2, 4)]
        [Tooltip("What she thinks, in her own words. One bubble's worth — if it needs two sentences it " +
                 "probably wants to be two lines with two triggers.")]
        public string Line = "";

        [Tooltip("How fast the thought fills, and how long it stands once it has. Leave empty and it " +
                 "reads at DialogueVoice.Default. Sharing one voice asset across every clue is the " +
                 "point: her inner voice is one voice.")]
        public DialogueVoiceDef Voice;

        [Header("When she thinks it")]
        [Tooltip("What sets it off.")]
        public InnerVoiceTrigger Trigger = InnerVoiceTrigger.InteractOffered;

        [Tooltip("InteractOffered only: the offered thing's stable id — an IInteractable.Id, an NPC id " +
                 "or a transit key, exactly as InteractOfferChanged carries it (e.g. " +
                 "tool.st_peters.rod). Case-sensitive and matched whole.")]
        public string SubjectId = "";

        [Tooltip("Proximity only: which region the point below is in (region.snake_case). Positions are " +
                 "region-local, so without this a point in one region would fire in another that " +
                 "happens to use the same coordinates.")]
        public string RegionId = "";

        [Tooltip("Proximity only: the point she has to come near, in world metres.")]
        public Vector2 WorldPosition;

        [Tooltip("Proximity only: how near, in metres. She re-arms at ReArmFactor times this, so " +
                 "standing on the line does not stutter the bubble.")]
        [Min(0.5f)] public float RadiusMeters = 5f;

        [Header("How often")]
        [Tooltip("ON: she thinks it once ever, and the save remembers (owner Q4 — the shipped answer " +
                 "for a clue, which stops being a clue the second time). OFF: it can come back, no " +
                 "sooner than the cooldown below.")]
        public bool OnceOnly = true;

        [Tooltip("Repeatable lines only: the shortest gap between two hearings, in real seconds.")]
        [Min(0f)] public float CooldownSeconds = 300f;

        /// <summary>How much further than <see cref="RadiusMeters"/> she must go before a repeatable
        /// proximity line can fire again. Hysteresis, so a player standing exactly on the edge does not
        /// make the bubble flicker — the same reason a thermostat has a dead band.</summary>
        public const float ReArmFactor = 1.5f;

        /// <summary>The persisted key for a <see cref="OnceOnly"/> line. Empty when the def has no id,
        /// which the content validator will not let ship.</summary>
        public string SaveFlagKey => string.IsNullOrWhiteSpace(Id) ? string.Empty : FlagPrefix + Id;

        /// <summary>The voice id to put on the request, or null. Core carries the id, not the asset —
        /// see <see cref="DialogueVoiceCatalog"/>.</summary>
        public string VoiceId => Voice != null ? Voice.Id : null;

        /// <summary>Is this line waiting on <paramref name="offerId"/>? False for a proximity line, for
        /// an empty subject, and for a null offer (the offer channel publishes those to say "nothing is
        /// offered", and nothing is not a thing to have a thought about).</summary>
        public bool WantsOffer(string offerId)
            => Trigger == InnerVoiceTrigger.InteractOffered
               && !string.IsNullOrEmpty(SubjectId)
               && string.Equals(SubjectId, offerId, System.StringComparison.Ordinal);

        /// <summary>
        /// Is <paramref name="playerPosition"/> inside this line's circle, in <paramref name="regionId"/>?
        /// False for a non-proximity line and for a line whose region is not the one loaded.
        /// </summary>
        public bool CoversPoint(Vector2 playerPosition, string regionId)
            => Trigger == InnerVoiceTrigger.Proximity
               && !string.IsNullOrEmpty(RegionId)
               && string.Equals(RegionId, regionId, System.StringComparison.Ordinal)
               && (playerPosition - WorldPosition).sqrMagnitude <= RadiusMeters * RadiusMeters;

        /// <summary>Has she gone far enough away for a repeatable proximity line to arm again? Uses
        /// <see cref="ReArmFactor"/>, so the ring she leaves is wider than the one she entered.</summary>
        public bool HasLeftFor(Vector2 playerPosition)
        {
            float out2 = RadiusMeters * ReArmFactor;
            return (playerPosition - WorldPosition).sqrMagnitude > out2 * out2;
        }
    }
}
