using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>The work place at the slip dory</b>: the mending sequence it runs, how a held press turns into
    /// game time, the words he thinks while he works, and the water she is launched onto. One asset per
    /// slip (rule 2); every number and line is the owner's to tune here, none is in code.
    /// </summary>
    [CreateAssetMenu(menuName = "Mending/Station", fileName = "MendingStation")]
    public sealed class DoryMendingStationDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (slipmend.snake_case).")]
        public string Id;

        [Tooltip("The jobs, their supplies and the boat they mend.")]
        public DoryMendingDef Mending;

        [Tooltip("How near he stands to work on her or launch her, metres.")]
        [Min(0.1f)] public float ReachMeters = 2.6f;

        [Header("Held work")]
        [Tooltip("Game minutes of mending each real second the interact key is held.")]
        [Min(0.01f)] public float GameMinutesPerHeldSecond = 10f;

        [Tooltip("Real seconds of holding gathered before the work is handed to the clock. Keeps the clock " +
                 "and save checks off the per-frame path.")]
        [Min(0.02f)] public float WorkTickSeconds = 0.25f;

        [Header("Verbs (read every frame: composed when the state changes, never in the getter)")]
        [Tooltip("The mend verb. {job} is the next job's display name.")]
        public string MendLabel = "{job}";

        [Tooltip("The launch verb, once she is mended.")]
        public string LaunchLabel = "Launch her";

        [Header("Thoughts (bubble text)")]
        [Tooltip("When a job starts or resumes. {job} is the job, lower-cased.")]
        public string WorkLine = "Hold at it, and I'll {job}.";

        [Tooltip("When he stops holding before the job is done.")]
        public string PausedLine = "I'll come back to her.";

        [Tooltip("When a job is done and another follows. {next} is the next job, lower-cased.")]
        public string JobDoneLine = "That's done. Next I {next}.";

        [Tooltip("When the last job is done.")]
        public string MendedLine = "She's sound. Now to get her down the slip.";

        [Tooltip("When the bag in his hand lacks the job's supplies. {job} is the job, lower-cased.")]
        public string MissingLine = "I need the makings in my bag to {job}.";

        [Tooltip("When she goes down the slip. Names where she now lies: the launch berth is out of the camera's view from the slip.")]
        public string LaunchedLine = "Down she goes. She's tied at the pier head, and she's mine to row.";

        [Tooltip("The voice the thoughts are spoken in.")]
        public string VoiceId = "voice.inner";

        [Tooltip("How high over his head the thought bubble sits, metres.")]
        [Min(0f)] public float BubbleHeightMetres = 2.1f;

        [Header("Afloat")]
        [Tooltip("Region she is launched into (stable id).")]
        public string LaunchRegionId = "region.st_peters";

        [Tooltip("Where she lies afloat once launched, in that region's frame (metres). Her own mooring: the " +
                 "berth the player's dory always lies at, which floats her at every tide and is where the " +
                 "rig puts her again after a load.")]
        public Vector2 LaunchBerth;

        [Tooltip("Her heading at that berth, degrees about Z.")]
        public float LaunchHeadingDegrees;
    }
}
