using UnityEngine;

namespace HiddenHarbours.Environment
{
    /// <summary>
    /// ONE KIND OF SHORE ANIMAL — every number about how it behaves, and the frames it is drawn from. One
    /// asset per species (rule 2) under <c>Data/Wildlife</c>; the first is <c>wildlife.grey_seal</c>.
    ///
    /// <para><b>Where the numbers live (rule 6).</b> How many come out and when (the time of day, the sea,
    /// the tide margins), how skittish they are (the distances, the speeds, the wind), how they swim and
    /// dive, and the frame times are all here, so the owner tunes a seal without code. Where a seal lies
    /// is not: that is the haul-out's (<see cref="SealHauloutDef"/>).</para>
    ///
    /// <para><b>Content, never state.</b> Nothing here, and nothing derived from it, enters the save: a
    /// seal is a function of these numbers, the world seed, the game time and the tide (rule 5).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Wildlife Species", fileName = "WildlifeSpecies")]
    public sealed class WildlifeSpeciesDef : ScriptableObject
    {
        /// <summary>The id prefix of a species.</summary>
        public const string IdPrefix = "wildlife.";

        [Tooltip("wildlife.<snake_case>. Permanent once merged: it is the photo subject id and every hash about a seal is keyed by its haul-out's id.")]
        public string Id = "";

        public string DisplayName = "";

        [Header("How many come out")]
        [Tooltip("The share of spots used on a calm, sunny low tide.")]
        [Range(0f, 1f)] public float BaseOccupancy = 0.85f;

        [Tooltip("x the share at night (between dusk's end and dawn's start).")]
        [Range(0f, 1f)] public float NightOccupancyScale = 0.45f;

        [Tooltip("The hour night ends and the morning ramp starts.")]
        [Range(0f, 24f)] public float DawnFromHour = 5f;

        [Tooltip("The hour the morning ramp reaches full day.")]
        [Range(0f, 24f)] public float DawnToHour = 7f;

        [Tooltip("The hour the evening ramp starts.")]
        [Range(0f, 24f)] public float DuskFromHour = 19f;

        [Tooltip("The hour the evening ramp reaches night.")]
        [Range(0f, 24f)] public float DuskToHour = 21f;

        [Tooltip("Sea state (0 glass .. 1 storm) at or under which the weather keeps no one off the rocks.")]
        [Range(0f, 1f)] public float CalmSeaState01 = 0.25f;

        [Tooltip("Sea state at or over which only RoughOccupancyScale of them stay out.")]
        [Range(0f, 1f)] public float RoughSeaState01 = 0.65f;

        [Tooltip("x the share in a rough sea.")]
        [Range(0f, 1f)] public float RoughOccupancyScale = 0.2f;

        [Header("The tide")]
        [Tooltip("On the ebb a seal hauls out once the water is this far (m) below the rock it lies on.")]
        [Min(0f)] public float EbbHaulOutBelowMetres = 0.30f;

        [Tooltip("On the flood it stays out until the water is this close (m) under its rock. Must be > 0, so no seal ever lies on a wet spot.")]
        [Min(0.01f)] public float FloodSlipBelowMetres = 0.15f;

        [Tooltip("Each seal adds up to this much (m, hashed per seal per tide) to both margins, so they come and go one by one.")]
        [Min(0f)] public float ArrivalSpreadMetres = 0.2f;

        [Tooltip("While the water is no more than this (m) over its rock, a seal off it may wait in the water nearby.")]
        [Min(0f)] public float WaterLingerAboveMetres = 0.7f;

        [Tooltip("The share of seals that wait in the water rather than go away.")]
        [Range(0f, 1f)] public float WaterShare = 0.55f;

        [Header("Skittishness (the photo puzzle)")]
        [Tooltip("An intruder (the player, or any hull) this close (m) makes a resting seal lift its head and watch.")]
        [Min(0f)] public float AlertDistanceMetres = 16f;

        [Tooltip("This close (m) and it slips into the water.")]
        [Min(0f)] public float SlipDistanceMetres = 7f;

        [Tooltip("Closing faster than this (m/s) inside the alert distance startles it into the water at once.")]
        [Min(0f)] public float StartleApproachSpeed = 1.4f;

        [Tooltip("Closing no faster than this (m/s) counts as creeping: a watching seal settles back down after AlertCalmSeconds.")]
        [Min(0f)] public float CreepSpeed = 0.5f;

        [Tooltip("Seconds a watching seal needs of creeping or stillness before it puts its head down again.")]
        [Min(0f)] public float AlertCalmSeconds = 6f;

        [Tooltip("x both distances when the wind blows from the intruder to the seal (it smells you).")]
        [Min(0f)] public float UpwindDistanceScale = 1.5f;

        [Tooltip("x both distances when the intruder is downwind of the seal.")]
        [Min(0f)] public float DownwindDistanceScale = 0.7f;

        [Tooltip("Wind (m/s) at or over which the wind side counts in full; under it the scales fade towards 1.")]
        [Min(0.01f)] public float FullScentWindSpeed = 4f;

        [Tooltip("x both distances for a person on foot (a hull is 1).")]
        [Min(0f)] public float OnFootDistanceScale = 1f;

        [Tooltip("Seconds of calm (no intruder inside the alert distance) before a seal in the water hauls back out.")]
        [Min(0f)] public float CalmReturnSeconds = 40f;

        [Header("In the water")]
        [Tooltip("How far (m) a seal swims off its rock, nearest and farthest, towards the haul-out's open water.")]
        [Min(0f)] public float SwimOffsetMinMetres = 2f;

        [Min(0f)] public float SwimOffsetMaxMetres = 6f;

        [Tooltip("The least water (m) a swimming head is put in.")]
        [Min(0f)] public float MinSwimDepthMetres = 0.3f;

        [Tooltip("Seconds a head stays up, shortest and longest (hashed per surfacing).")]
        [Min(0.1f)] public float SurfaceSecondsMin = 6f;

        [Min(0.1f)] public float SurfaceSecondsMax = 14f;

        [Tooltip("Seconds under after a dive.")]
        [Min(0.1f)] public float DiveSeconds = 7f;

        [Tooltip("Seconds the slide from rock to water (or back) takes.")]
        [Min(0.1f)] public float SlideSeconds = 1.2f;

        [Tooltip("Metres a head drifts about its swim point while up.")]
        [Min(0f)] public float DriftMetres = 0.6f;

        [Tooltip("Seconds for one slow drift of a head about its swim point.")]
        [Min(0.1f)] public float DriftPeriodSeconds = 5f;

        [Tooltip("A seal on land draws over every scene sprite whose rect covers its spot and is no wider or taller " +
                 "than this (m): the rock it lies on, whatever builds the rock. Bigger sheets (terrain, sea) are skipped.")]
        [Min(0f)] public float CoverSpanMetres = 12f;

        [Header("At rest")]
        [Tooltip("Seconds between idles (a head lift, a scratch, a yawn), shortest and longest.")]
        [Min(0.1f)] public float IdleIntervalMinSeconds = 4f;

        [Min(0.1f)] public float IdleIntervalMaxSeconds = 12f;

        [Header("Running")]
        [Tooltip("How often (real seconds) the haul-out re-evaluates the schedule, the tide and who is near. The frames themselves step every frame off a fixed table.")]
        [Min(0.05f)] public float ScheduleTickSeconds = 0.5f;

        [Tooltip("The most seals one haul-out draws at once (its renderer pool).")]
        [Range(1, 32)] public int MaxDrawn = 16;

        [Header("The pictures (Art/Sprites/Creatures/Seal.png)")]
        [Tooltip("Seconds per frame.")]
        [Min(0.02f)] public float FrameSeconds = 0.2f;

        [Tooltip("Lying at rest: frame 0 is the still pose; the rest are its idles in order (breath, head lift, yawn, scratch).")]
        public Sprite[] RestFrames = new Sprite[0];

        [Tooltip("Head up, watching.")]
        public Sprite[] AlertFrames = new Sprite[0];

        [Tooltip("Humping off the rock into the water (played backwards to haul out).")]
        public Sprite[] SlideFrames = new Sprite[0];

        [Tooltip("The same slide with no water under it (no waterline, no splash), frame for frame with SlideFrames: " +
                 "drawn wherever the ground under the sliding seal is dry, so the water cut shows only in water.")]
        public Sprite[] SlideDryFrames = new Sprite[0];

        [Tooltip("A head in the water: bob, look one way, look the other.")]
        public Sprite[] SwimFrames = new Sprite[0];

        [Tooltip("Going under and the ring it leaves.")]
        public Sprite[] DiveFrames = new Sprite[0];

        [Tooltip("Coat colours (multiplied over the grey sheet), picked per spot.")]
        public Color[] Coats = { Color.white };

        /// <summary>The coat for a spot's coat index (wraps; white with none).</summary>
        public Color CoatFor(int index)
            => Coats == null || Coats.Length == 0 ? Color.white : Coats[((index % Coats.Length) + Coats.Length) % Coats.Length];

        /// <summary>The schedule's and the mind's view of these numbers: plain values, nothing from the engine.</summary>
        public SealTuning ToTuning() => new SealTuning
        {
            BaseOccupancy = BaseOccupancy,
            NightOccupancyScale = NightOccupancyScale,
            DawnFromHour = DawnFromHour, DawnToHour = DawnToHour,
            DuskFromHour = DuskFromHour, DuskToHour = DuskToHour,
            CalmSeaState01 = CalmSeaState01, RoughSeaState01 = RoughSeaState01,
            RoughOccupancyScale = RoughOccupancyScale,
            EbbHaulOutBelowMetres = EbbHaulOutBelowMetres,
            FloodSlipBelowMetres = FloodSlipBelowMetres,
            ArrivalSpreadMetres = ArrivalSpreadMetres,
            WaterLingerAboveMetres = WaterLingerAboveMetres,
            WaterShare = WaterShare,
            AlertDistanceMetres = AlertDistanceMetres,
            SlipDistanceMetres = SlipDistanceMetres,
            StartleApproachSpeed = StartleApproachSpeed,
            CreepSpeed = CreepSpeed,
            AlertCalmSeconds = AlertCalmSeconds,
            UpwindDistanceScale = UpwindDistanceScale,
            DownwindDistanceScale = DownwindDistanceScale,
            FullScentWindSpeed = FullScentWindSpeed,
            OnFootDistanceScale = OnFootDistanceScale,
            CalmReturnSeconds = CalmReturnSeconds,
            SurfaceSecondsMin = SurfaceSecondsMin, SurfaceSecondsMax = SurfaceSecondsMax,
            DiveSeconds = DiveSeconds,
            SlideSeconds = SlideSeconds,
        };
    }

    /// <summary>A species' numbers as plain values, so the pure schedule and mind take no engine object.</summary>
    public struct SealTuning
    {
        public float BaseOccupancy, NightOccupancyScale;
        public float DawnFromHour, DawnToHour, DuskFromHour, DuskToHour;
        public float CalmSeaState01, RoughSeaState01, RoughOccupancyScale;
        public float EbbHaulOutBelowMetres, FloodSlipBelowMetres, ArrivalSpreadMetres;
        public float WaterLingerAboveMetres, WaterShare;
        public float AlertDistanceMetres, SlipDistanceMetres, StartleApproachSpeed, CreepSpeed, AlertCalmSeconds;
        public float UpwindDistanceScale, DownwindDistanceScale, FullScentWindSpeed, OnFootDistanceScale;
        public float CalmReturnSeconds;
        public float SurfaceSecondsMin, SurfaceSecondsMax, DiveSeconds, SlideSeconds;
    }
}
