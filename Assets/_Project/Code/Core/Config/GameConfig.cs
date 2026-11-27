using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Central tunables for the simulation — "no magic numbers" (CLAUDE.md rule 6). The owner
    /// can tune feel here in the Inspector with no code. Create one via
    /// Assets &gt; Create &gt; Hidden Harbours &gt; Game Config and place it in Data/Config.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public const int DefaultPlayerNameMaxLength = 32;

        [Header("Player name")]
        [Tooltip("Maximum name length after trimming surrounding spaces.")]
        [Min(1)] public int PlayerNameMaxLength = DefaultPlayerNameMaxLength;

        public const int DefaultTraitPicksPerCharacter = 2;

        [Header("Character traits")]
        [Tooltip("How many named traits a character picks (owner ruling 2026-11-14: two). The bonuses " +
                 "themselves live on each TraitDef under Data/Resources/Traits.")]
        [Min(0)] public int TraitPicksPerCharacter = DefaultTraitPicksPerCharacter;

        [Header("Needs (hunger and tiredness)")]
        [Tooltip("Hunger and tiredness (owner ruling 2026-11-14). They only slow the walk a little and " +
                 "shave a little off an in-person sale; they never stop the character and are never shown " +
                 "as a bar. Rates are per IN-GAME hour. Read through GameServices.NeedsSettings.")]
        public NeedsSettings Needs = NeedsSettings.Default;

        [Header("Body (frame, weight, metabolism, overeating)")]
        [Tooltip("The player's body (owner ask 2026-11-15). Frame and height set a lean and a max mass; " +
                 "a bigger body gets hungry sooner (Mifflin-St Jeor), a heavier one walks slower, a taller " +
                 "one faster; eating past a full stomach makes the player sick. Never shown as a number. " +
                 "The default frame at height scale 1 and lean mass is exactly today's player. Read " +
                 "through GameServices.BodySettings.")]
        public BodySettings Body = BodySettings.Default;

        [Tooltip("Body slice 2 (owner ask 2026-11-15): strength and cardio. Training grows grip, back, legs " +
                 "and cardio slowly; keeping them up is cheap; muscle memory regains a lost level faster. " +
                 "Strength and mass set what the player can carry and how much a load slows the walk; cardio " +
                 "makes moving a little faster and cheaper. Untrained and empty-handed is exactly today's " +
                 "player. Read through GameServices.StrengthSettings.")]
        public StrengthSettings Strength = StrengthSettings.Default;

        [Header("Crew and trip limits")]
        [Tooltip("Crew slice R2: how many nights each trip kind plans for, whether hired hands may share a " +
                 "bunk in two watches (owner ruling: yes, shipped ON) and whether month trips need a hired " +
                 "skipper. Players never share a bunk (owner ruling 2026-11-15). Read through " +
                 "GameServices.CrewPolicy. The per-hull numbers live on each BoatVoyageDef.")]
        public CrewPolicy Crew = CrewPolicy.Default;

        [Header("Rowing styles (how a rower pulls: timing and the hull's surge)")]
        [Tooltip("The style the PLAYER rows. rowstyle.steady is the stroke he has always rowed (plus a " +
                 "subtle surge on the pull); point it at rowstyle.casual to give him the unhurried pull " +
                 "with a rest. Empty = the plain stroke with an even push.")]
        public RowingStyleDef PlayerRowingStyle;
        [Tooltip("The style a character the game rows for uses when their CharacterVisualDef names " +
                 "none (rowstyle.casual: nobody else rows like a racer).")]
        public RowingStyleDef NpcRowingStyle;

        [Header("Helm sit and stand (the mesh figure takes and leaves a helm)")]
        [Tooltip("How long the player's figure takes to sit down at a tiller seat, a seated helm or the oars, " +
                 "or to step up and take a standing wheel, and to leave again; and how the move splits " +
                 "between going to the spot and settling into the pose. Visual only: the helm goes live on " +
                 "the press. 0 seconds (or Enabled off) is the old one-frame snap.")]
        public HelmTransitionSettings HelmSitStand = HelmTransitionSettings.Default;

        [Header("Cleanliness (the third need)")]
        [Tooltip("Dirt (owner ask 2026-11-15): rises slowly with game time and faster with dirty work, the " +
                 "gym and the sea; washing at a station clears it, each station only to its own ceiling (sea " +
                 "< sink < shower). Stinky is the curve's StrongAt: villagers notice. Never shown as a bar. " +
                 "The wash rates and ceilings live on the WashStationDef assets. Read through " +
                 "GameServices.CleanlinessSettings.")]
        public CleanlinessSettings Cleanliness = CleanlinessSettings.Default;

        [Header("Kitchens (dining and seated rest)")]
        [Tooltip("Kitchens (owner ask 2026-11-15): the food boost of eating seated or at a table, and how " +
                 "far sitting can rest you (never as far as a bed). Shared by galleys aboard, homes, shops " +
                 "and player-built kitchens. Read through GameServices.KitchenSettings.")]
        public KitchenSettings Kitchen = KitchenSettings.Default;

        public const float DefaultCreatorFigureHeightFraction = 0.6f;

        [Header("Character creator view")]
        public Color CreatorBackdropColor = NotebookInk.Paper;
        [Range(0.1f, 0.7f)] public float CreatorFigureHeightFraction = DefaultCreatorFigureHeightFraction;

        public const float DefaultMeshArmClearancePx = 1f;

        [Header("Character mesh clearance (QA)")]
        [Tooltip("The deepest an arm vertex (forearm or hand) of the player's mesh may sit inside his " +
                 "torso or head on any frame of the dig, hauler and strike clips, in rig pixels " +
                 "(CharacterSkinDef.PxPerMetre to the metre). Each body part is measured as a capsule " +
                 "along its bones. Read by the EditMode clearance test, never at runtime.")]
        [Range(0f, 4f)] public float MeshArmClearancePx = DefaultMeshArmClearancePx;

        [Header("Curling breakers")]
        public CurlingBreakerSettings CurlingBreakers = CurlingBreakerSettings.Default;

        /// <summary>The day length the clock ships with — the single fallback consumers use when no
        /// config asset is wired (EditMode, a bare test rig). One constant, never a scattered literal.
        ///
        /// <para>⚠ <b>1800 = a 30-minute day, ruled by the owner 2026-08-01</b> (was 1200 / 20 min). It is
        /// one of the two levers behind "the tide falls too fast" — this one stretches every in-game hour
        /// from 50 to 75 real seconds, so EVERY tide window gets 1.5× longer in real minutes without moving
        /// a single in-game-hour figure. The other lever is St Peters' tide amplitude
        /// (<c>StPetersBuilder.TideAmplitude</c> 3.5 → 2.2 m). Together the peak water-level rate falls
        /// ~2.4×, from ~3.5 cm/s to ~1.5 cm/s.</para>
        ///
        /// <para>Everything else paced in in-game time — freshness, NPC routines, market ticks, day/night,
        /// the moon — slows by the same 1.5× in REAL time. That is the sanctioned consequence of the day-
        /// length ruling, not a regression.</para></summary>
        public const float DefaultSecondsPerDay = 1800f;

        /// <summary>The canon season length (vision-and-pillars §5.8, 28 days) — the fallback for a
        /// consumer that reads the calendar with no config wired, the sibling of
        /// <see cref="DefaultSecondsPerDay"/>. The tree snow reads the season length through
        /// <c>GameServices.DaysPerSeason</c>, so a literal 28 there would be a second copy.</summary>
        public const int DefaultDaysPerSeason = 28;

        /// <summary>The ruled default for <see cref="InteriorRockScale"/> (ADR 0038 proposal 1) — read
        /// by <c>GameServices.InteriorRockScale</c> when no config is wired, so a test rig and a scene
        /// with no GameConfig draw the same cabin the shipped asset does.</summary>
        public const float DefaultInteriorRockScale = 0.45f;

        /// <summary>The ruled default for <see cref="StairClimbSeconds"/> (ADR 0036) — read by
        /// <c>BuildingInterior</c> when no config is wired, so an EditMode rig, a bare region scene and
        /// the shipped asset all climb a stair at the same pace.</summary>
        public const float DefaultStairClimbSeconds = 0.5f;

        /// <summary>
        /// The default for <see cref="MaxOneHandFishLengthMeters"/> — read by
        /// <c>GameServices.MaxOneHandFishLengthMeters</c> when no config is wired, so an EditMode rig and
        /// a bare art scene split one-hand from two-hand exactly as the shipped asset does.
        ///
        /// <para><b>0.62 m is the ART RIG's own cradle point, converted.</b> <c>fishIsoRig.js</c> holds a
        /// fish in one hand below 2.2 kg and cradles it across both at or above — and 2.2 kg is 0.62 m
        /// through the same cube law <see cref="CatchSize"/> inverts. So the hands agree with the picture
        /// by construction rather than by a number someone matched by eye, and the shipped roster splits
        /// where the rig says it does: a 2 kg cod (0.60 m) hangs from a fist, a 12 kg one (1.09 m) does
        /// not.</para>
        /// </summary>
        public const float DefaultMaxOneHandFishLengthMeters = 0.62f;

        /// <summary>The default for <see cref="FishLengthPerKgCubeRootMeters"/> — the least-squares fit
        /// across <c>fishIsoRig.js</c>'s four declared species (cod, haddock, pollock, mackerel), which
        /// it reproduces to within 4 %. See <see cref="CatchSize"/> for why the law is a cube root.</summary>
        public const float DefaultFishLengthPerKgCubeRootMeters = 0.477f;

        /// <summary>The default for <see cref="MinDrawnFishLengthPx"/>: 12 px, a little under two hand
        /// widths of the 64 px figure. Measured against the catch pass 2 held sheets it lifts only the
        /// drawings shorter than that (the small mackerel and flounder, the small and middle herring) and
        /// leaves every other rung alone.</summary>
        public const float DefaultMinDrawnFishLengthPx = 12f;

        /// <summary>The default for <see cref="HeldFishKeylineMaxLengthPx"/>: 16 px. Measured against the
        /// catch pass 2 held sheets it lines every herring and mackerel drawing, the small haddock, bass and
        /// pollock and the small and middle flounder, and leaves every cod and every larger drawing alone.</summary>
        public const float DefaultHeldFishKeylineMaxLengthPx = 16f;

        /// <summary>The default for <see cref="HeldFishKeylineShade"/>: the line is the fish's own colours at
        /// 40% brightness, a step down its own ramp rather than a colour of its own.</summary>
        public const float DefaultHeldFishKeylineShade = 0.4f;

        /// <summary>The default for <see cref="HeldFishKeylineOffsetsPx"/>: one sprite pixel to either side,
        /// so a fish hanging by its tail gets a dark line down both long edges and none across its ends.</summary>
        public static Vector2[] DefaultHeldFishKeylineOffsetsPx() => new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f) };

        /// <summary>
        /// The default for <see cref="VehicleDoorSweepSeconds"/> — how long a vehicle door takes to
        /// travel from shut to fully open.
        ///
        /// <para><b>Seconds per SWEEP rather than degrees per second, and that is a feel choice, not
        /// a physical claim.</b> The fleet's sweeps run from a 42° van hood flap to a 255° reefer
        /// barn; at a fixed angular rate the barn would take six times as long as the flap — arguably
        /// truer, and it reads as a stall. One "time to open" keeps every door in the pack feeling
        /// like the same hand opened it. It is the owner's to change (rule 6), and changing it moves
        /// nothing that was measured: the sweeps themselves come off the art.</para>
        /// </summary>
        public const float DefaultVehicleDoorSweepSeconds = 0.85f;

        /// <summary>
        /// The default for <see cref="VehicleGearCrankSeconds"/> — how long a trailer's landing gear
        /// takes to wind between down and up.
        ///
        /// <para>Longer than a door on purpose: it is a hand crank, and the trailer sidecar is
        /// explicit that the discipline is <i>couple → gear 0 BEFORE rolling</i>. A crank that
        /// snapped shut would make that discipline free.</para>
        ///
        /// <para>⚠️ The shoes slide smoothly across the whole crank; the LEGS swap at the END of it,
        /// because they telescope and were baked at each end rather than posed. The end of a crank
        /// the player has watched turn is exactly where that swap is least visible.</para>
        /// </summary>
        public const float DefaultVehicleGearCrankSeconds = 2.4f;

        [Header("Vehicle doors")]
        [Tooltip("How long a vehicle door takes to travel from shut to fully open, in seconds. One " +
                 "time-to-open for every door in the road fleet, from the van's 42-degree hood flap " +
                 "to a reefer's 255-degree barn leaf: a fixed angular rate would make the barn take " +
                 "six times as long, which reads as a stall. The sweeps themselves are measured off " +
                 "the art and are not tunable; this is the hand that works them, and it is yours.")]
        [Min(0.05f)] public float VehicleDoorSweepSeconds = DefaultVehicleDoorSweepSeconds;

        [Tooltip("How long a trailer's landing gear takes to wind between down and up, in seconds. " +
                 "Longer than a door deliberately - it is a hand crank, and the kit's own discipline " +
                 "is couple, then wind the legs up BEFORE rolling. A crank that snapped would make " +
                 "that free. " +
                 "The sand shoes slide smoothly across the whole wind; the leg tubes swap at the end " +
                 "of it, because they telescope and were baked at each end rather than posed.")]
        [Min(0.05f)] public float VehicleGearCrankSeconds = DefaultVehicleGearCrankSeconds;

        [Header("Clock")]
        [Tooltip("Real seconds per in-game day. 1800 = a 30-minute day (the owner's 2026-08-01 tide-pacing " +
                 "ruling; was 1200 = 20 min). Raising this slows EVERY real-time pace in the game — tide, " +
                 "rot, routines, the moon — because they are all clocked in in-game hours.")]
        public float SecondsPerDay = DefaultSecondsPerDay;
        [Tooltip("Hour a fresh game opens at. Scene-local start hours are obsolete and ignored.")]
        [Range(0f, 24f)] public float StartHour = 7.5f;

        /// <summary>The hour a night's rest wakes at when no config is wired (EditMode, a bare test
        /// rig): the design's 06:00 (time-tides-weather §1.3). The sibling of
        /// <see cref="DefaultSecondsPerDay"/>, read by <see cref="RestMorning"/>.</summary>
        public const float DefaultWakeHour = 6f;

        [Tooltip("Hour of the morning your own bed wakes you at. Lying down brings the NEXT time the " +
                 "clock reads this hour: tomorrow's when you turn in during the day or the evening, " +
                 "this morning's when you turn in after midnight. 6 = 06:00, the design's wake time. " +
                 "The day rollover (market recovery, snow cover) fires for every midnight slept through.")]
        [Range(0f, 24f)] public float WakeHour = DefaultWakeHour;

        /// <summary>The shortest sleep the chooser offers, when no config is wired (owner ruling
        /// 2026-11-15: "Player chooses sleep duration").</summary>
        public const float DefaultSleepMinHours = 1f;

        /// <summary>The longest sleep, when no config is wired: the ruling's own cap ("Maximum 8 hours
        /// sleep before auto waking"). <see cref="SleepPlan.RulingMaxHours"/> holds the field to it.</summary>
        public const float DefaultSleepMaxHours = 8f;

        /// <summary>How far one press of the chooser moves the hours, when no config is wired.</summary>
        public const float DefaultSleepStepHours = 1f;

        /// <summary>How many in-game hours pass per real second while asleep, when no config is wired:
        /// a full eight-hour night goes by in eight seconds.</summary>
        public const float DefaultSleepGameHoursPerRealSecond = 1f;

        [Tooltip("The shortest sleep you can choose, in in-game hours.")]
        [Range(0.25f, 8f)] public float SleepMinHours = DefaultSleepMinHours;

        [Tooltip("The longest sleep you can choose, in in-game hours. You wake on your own when it is up. " +
                 "Never more than 8: the owner's ruling caps a sleep at eight hours, whatever this says.")]
        [Range(0.25f, 8f)] public float SleepMaxHours = DefaultSleepMaxHours;

        [Tooltip("How many hours one press left or right changes the chosen sleep by.")]
        [Range(0.25f, 4f)] public float SleepStepHours = DefaultSleepStepHours;

        [Tooltip("How fast time runs while you sleep: in-game hours per real second. 1 = an eight-hour " +
                 "night goes by in eight seconds. The world is exactly what it would be had the same " +
                 "hours passed at the normal pace; only the wait changes. You can wake early with a press.")]
        [Min(0.05f)] public float SleepGameHoursPerRealSecond = DefaultSleepGameHoursPerRealSecond;

        /// <summary>Whether a bunk aboard refuses while she is neither made fast nor holding at anchor,
        /// when no config is wired. TRUE: the owner's safe default ("it's not safe to turn in out
        /// here").</summary>
        public const bool DefaultBunkRequiresMooredOrAnchored = true;

        /// <summary>How close (m) to a bunk's reach point the player must stand, when no config is
        /// wired.</summary>
        public const float DefaultBunkReachMetres = 1f;

        [Tooltip("Sleeping aboard (owner 2026-11-14): when ON, a bunk on your boat only lets you turn in " +
                 "while she is made fast to a cleat, berthed, or holding at anchor. Adrift or under way it " +
                 "answers \"It's not safe to turn in out here.\" Turn OFF to let the player sleep anywhere.")]
        public bool BunkRequiresMooredOrAnchored = DefaultBunkRequiresMooredOrAnchored;

        [Tooltip("How close (m) you must stand to a bunk's own reach point, which is already the spot the " +
                 "rig says you stand on to use it — so this is a forgiving step, not a precise one.")]
        [Min(0.1f)] public float BunkReachMetres = DefaultBunkReachMetres;

        /// <summary>How far (m, straight line) a boat's helm may be from her main entry door before she
        /// gets a second cabin-light switch by the door, when no config is wired. 2.5 m sits between the
        /// small cabin boats (helm 0.3-1.4 m from the door) and the flybridge and wheelhouse-deck boats
        /// (3.2 m and up).</summary>
        public const float DefaultCabinLightSwitchDoorDistanceMetres = 2.5f;

        /// <summary>How close (m) to a cabin-light switch the player must stand, when no config is wired.</summary>
        public const float DefaultCabinLightSwitchReachMetres = 1f;

        [Tooltip("Cabin-light switches (owner 2026-11-25): every boat with a cabin has a switch at her helm. " +
                 "When her helm is more than this many metres (straight line) from her entry door, she also " +
                 "gets a second switch by the door. Both throw the same lights. A boat that names her own " +
                 "switches in her visual def ignores this.")]
        [Min(0f)] public float CabinLightSwitchDoorDistanceMetres = DefaultCabinLightSwitchDoorDistanceMetres;

        [Tooltip("How close (m) you must stand to a cabin-light switch to press it.")]
        [Min(0.1f)] public float CabinLightSwitchReachMetres = DefaultCabinLightSwitchReachMetres;

        /// <summary>How far (m, on screen) a cabin-light switch's spot must stand from the helm station, when
        /// no config is wired. Over the switcher's helm radius (0.5 m) with a margin, so a player standing at
        /// the switch is never also at the helm.</summary>
        public const float DefaultCabinLightSwitchHelmClearanceMetres = 0.75f;

        [Tooltip("A cabin-light switch is moved to a spot on her cabin wall where nothing else takes the " +
                 "press: at least this far (m, on screen, at every heading) from her helm station, and out of " +
                 "every door's reach. Keep it above the helm radius (0.5 m).")]
        [Min(0f)] public float CabinLightSwitchHelmClearanceMetres = DefaultCabinLightSwitchHelmClearanceMetres;
        [Tooltip("Season at elapsed day zero. Its offset uses DaysPerSeason, never a separate day count.")]
        public Season StartSeason = Season.HighSummer;
        [Tooltip("One-based day within the starting season. Changes labels, never simulation seconds.")]
        [Min(1)] public int StartDayOfSeason = 1;
        public int StartDayOfYear => (int)StartSeason * System.Math.Max(1, DaysPerSeason)
                                 + Mathf.Clamp(StartDayOfSeason, 1, System.Math.Max(1, DaysPerSeason)) - 1;
        [Min(1)] public int DaysPerWeek = 7;
        [Min(1)] public int DaysPerSeason = DefaultDaysPerSeason;
        [Tooltip("Which weekday is Market Day at Nine Mile Creek (0 = Monday).")]
        public int MarketDayIndex = 4; // Friday

        /// <summary>The chooser's switch when no config is wired: ON (owner ask 2026-11-15).</summary>
        public const bool DefaultInteractChooserEnabled = true;

        /// <summary>The highest candidate rung that becomes a chooser row when no config is wired: the
        /// fixture rung (<see cref="InteractPriority.Fixture"/>).</summary>
        public const int DefaultInteractChooserMaxPriority = InteractPriority.Fixture;

        /// <summary>The most rows the chooser shows when no config is wired.</summary>
        public const int DefaultInteractChooserMaxRows = 6;

        /// <summary>The heavy-work needs bar when no config is wired: refused only when worn out or hungry
        /// (the Strong stage of each).</summary>
        public static readonly NeedsRequirement DefaultHeavyWorkNeeds =
            new NeedsRequirement(NeedStage.Strong, NeedStage.Strong);

        [Header("Interaction chooser (owner 2026-11-15)")]
        [Tooltip("When ON, pressing interact where two or more actions are in reach opens a small list to " +
                 "pick from (arrows/W-S + E or Enter, number keys, the mouse, or a pad's stick/d-pad + " +
                 "south to confirm, east to cancel). OFF: the press acts on the " +
                 "nearest, most specific thing, as before.")]
        public bool InteractChooserEnabled = DefaultInteractChooserEnabled;

        [Tooltip("The highest interaction rung that joins the list. 0 = fixtures and below (beds, doors, " +
                 "lids, counters). Things in your hands (20), at your feet (10) and tool targets (30) keep " +
                 "acting at once, because what you hold is what you meant.")]
        public int InteractChooserMaxPriority = DefaultInteractChooserMaxPriority;

        [Tooltip("The most rows the list shows (at most 9, one per number key).")]
        [Range(2, InteractChoiceList.HardCap)] public int InteractChooserMaxRows = DefaultInteractChooserMaxRows;

        /// <summary>How far the list's up/down axis must travel to count as a push when no config is
        /// wired. 0.5 is the latch the keyboard has always used, so W/S and the arrows are unchanged.</summary>
        public const float DefaultInteractChooserPushThreshold = 0.5f;

        /// <summary>How long a held stick or d-pad waits before the highlight starts running on, when no
        /// config is wired.</summary>
        public const float DefaultInteractChooserRepeatDelaySeconds = 0.4f;

        /// <summary>The gap between steps once the highlight is running on, when no config is wired.</summary>
        public const float DefaultInteractChooserRepeatIntervalSeconds = 0.12f;

        [Tooltip("How far the up/down axis must travel to move the highlight, and fall back before the next " +
                 "push counts (0..1 of full tilt). Sits on top of the stick's own dead zone, so drift never " +
                 "steps a row. Keys are always full tilt.")]
        [Range(0.2f, 0.9f)] public float InteractChooserPushThreshold = DefaultInteractChooserPushThreshold;

        [Tooltip("Gamepad only: seconds a held stick or d-pad waits before the highlight runs on, one row " +
                 "at a time. The keyboard keeps one row per press.")]
        [Range(0.1f, 1.5f)] public float InteractChooserRepeatDelaySeconds = DefaultInteractChooserRepeatDelaySeconds;

        [Tooltip("Gamepad only: seconds between rows once a held stick or d-pad is running on.")]
        [Range(0.05f, 0.5f)] public float InteractChooserRepeatIntervalSeconds = DefaultInteractChooserRepeatIntervalSeconds;

        /// <summary>How long the folded TASKS chip signals a new or finished task, when no config is
        /// wired (owner ask 2026-11-16).</summary>
        public const float DefaultTasksChipSignalSeconds = 2.4f;

        /// <summary>How long each on/off beat of that signal lasts, when no config is wired.</summary>
        public const float DefaultTasksChipBlinkSeconds = 0.3f;

        [Tooltip("Folded TASKS note: seconds its chip flashes gold when a task is added or finished. The " +
                 "note never re-opens itself; this is the whole of the nudge.")]
        [Range(0.5f, 8f)] public float TasksChipSignalSeconds = DefaultTasksChipSignalSeconds;

        [Tooltip("Folded TASKS note: seconds per on/off beat of the chip's flash.")]
        [Range(0.1f, 1f)] public float TasksChipBlinkSeconds = DefaultTasksChipBlinkSeconds;

        [Tooltip("The bar for heavy work, for an action that opts in: refused at this tiredness / hunger " +
                 "stage or worse, and the player says \"I'm too tired\" / \"I'm too hungry\" / both. None = " +
                 "never refused for that need. No action opts in yet (the needs ruling: a need never stops " +
                 "the character); this is the dial for when one does.")]
        public NeedsRequirement HeavyWorkNeeds = DefaultHeavyWorkNeeds;

        [Header("Tide")]
        [Tooltip("Principal lunar semidiurnal period in hours (~12.42 = two highs per tidal day).")]
        public float TidalPeriodHours = 12.4206f;

        /// <summary>The canon lunar month — the fallback for consumers with no config wired, and the
        /// sibling of <see cref="DefaultSecondsPerDay"/>. The DRAWN moon and the tide's spring/neap
        /// envelope must derive from the SAME period or full moon stops landing on a spring tide
        /// (vision-and-pillars §5.5), so neither may carry its own copy.</summary>
        public const float DefaultLunarMonthDays = 28f;

        /// <summary>The shipping neap fraction, as a const so code that must reason about neap
        /// water WITHOUT a config asset in hand can read it (the <see cref="DefaultLunarMonthDays"/>
        /// convention). St Peters' shore paint places its intertidal families against neap high
        /// water — amplitude × this — and a literal 0.45 there would be a second copy of a number
        /// the owner tunes here.</summary>
        public const float DefaultNeapAmplitudeFraction = 0.45f;

        [Tooltip("Moon cycle in in-game days; drives the spring/neap envelope. Canon: 28.")]
        public float LunarMonthDays = DefaultLunarMonthDays;
        [Tooltip("At neap, amplitude is this fraction of spring amplitude (0..1).")]
        [Range(0f, 1f)] public float NeapAmplitudeFraction = DefaultNeapAmplitudeFraction;

        [Tooltip("The tide TABLE the player reads to plan a crossing (VS-06): how many days the almanac " +
                 "page covers and how finely it hunts each high and low water. Defaults reproduce the " +
                 "step sizes the HUD gauge and the editor tool already used, so the shipped page reads " +
                 "exactly as before.")]
        public TideTableSettings TideTable = TideTableSettings.Default;

        [Header("Weather")]
        [Tooltip("Baseline wind strength (m/s) before regional/temporal variation.")]
        public float BaseWindStrength = 4f;
        [Tooltip("How much wind strength swings over time (m/s).")]
        public float WindVariability = 5f;
        [Tooltip("How quickly weather evolves. Larger = slower, smoother changes (hours).")]
        public float WeatherChangeHours = 6f;
        [Tooltip("Base fog tendency for the world (0..1). Regions add their own bias later.")]
        [Range(0f, 1f)] public float BaseFogBias = 0.15f;

        [Header("On-foot / Wading")]
        [Tooltip("Deepest water still WALKABLE on foot (m). At/under this the player wades — walkable but " +
                 "slowed, more as it deepens. 0 would collapse the wade band and make any water a wall. " +
                 "Global for M1 (per-region override is a later item). Owner-tunable feel.")]
        [Min(0f)] public float WadeDepth = 0.5f;
        [Tooltip("Deepest water the player can still move through ON FOOT (m) — the escape-valve limit. " +
                 "Between WadeDepth and this the player SLOW-SWIMS: very slow + vulnerable, used to get OUT " +
                 "toward shallower ground so a rising tide never traps them, never to cross. Deeper than " +
                 "this is BOAT-ONLY — a soft wall stops the player stepping in. Must be > WadeDepth.")]
        [Min(0f)] public float SwimLimit = 2.0f;
        [Tooltip("Move-speed multiplier at the DEEP edge of the wade band (0..1): full speed on dry ground " +
                 "ramps down to this by WadeDepth. Lower = wading feels heavier. Cozy-but-teeth: a drag, " +
                 "not a wall.")]
        [Range(0f, 1f)] public float WadeSlowFactor = 0.6f;
        [Tooltip("Move-speed multiplier in the SLOW-SWIM band (0..1): the crawl the player swims OUT at. " +
                 "Deliberately low so swimming is an escape, never a travel shortcut. Never lethal — just " +
                 "slow + exposed.")]
        [Range(0f, 1f)] public float SwimSlowFactor = 0.25f;

        [Header("Getting aboard and ashore")]
        [Tooltip("Require owned boats to be repaired before boarding. Off while damage and repairs are " +
                 "deferred; enable to enforce the repair ledger.")]
        public bool RequireBoatRepairsBeforeBoarding = false;

        [Tooltip("How close you must be to a hull to climb aboard, in metres — measured to her OUTLINE, " +
                 "not to her root, so a long hull is boardable from her own stern. Ships at the 3.5 m " +
                 "the switcher carried as a serialized field until 2026-09-03.")]
        [Min(0.1f)] public float BoardReachMetres = 3.5f;
        [Tooltip("How far a registered standing surface (a wharf deck) may be from the hull's WALKABLE " +
                 "deck edge and still be somewhere you can step ashore onto, in metres. ⚠ It must span " +
                 "more than the fendering gap: a hull's walkable strip is set in from her rail, so the " +
                 "reach covers (hull half-beam − walkable half-beam) + the fender gap. On the starter " +
                 "dory alongside St Peters that is 0.625 + 0.40 = 1.03 m, so 1.5 leaves margin for a " +
                 "boat lying a little off her lines — while staying small enough that it never invents " +
                 "a step across open water to a pier you are not actually alongside.")]
        [Min(0.1f)] public float StepAshoreReachMetres = 1.5f;
        [Tooltip("How wide the arc must be, in DEGREES of full width centred on the way she is looking, " +
                 "for a press on deck to mean 'step ashore' (owner playtest 2026-09-07: 'im also close " +
                 "to the dock so i understand why it happens but this will be an everyday occurance " +
                 "when docking so we need a smooth solution'). Stepping ashore is a FACING, the same way " +
                 "going over the washboard is: look at the wharf and E puts you on it; look inboard or " +
                 "along the deck and E leaves you aboard. ⚠ Keep it under 180 or 'along the deck' (90° " +
                 "off the wharf) counts as facing it and the everyday docking press throws you ashore " +
                 "again. 120 leaves ±60° — forgiving enough that you need not aim, tight enough that a " +
                 "quarter-turn away means no. 360 switches the rule off (the A/B, and the escape hatch): " +
                 "the arc can then exclude nothing and the press behaves exactly as it did before.")]
        [Range(0f, 360f)] public float StepAshoreFacingArcDegrees = 120f;
        [Tooltip("Width of the gunwale band on a hull with NO authored washboard areas, in metres — the " +
                 "strip just inside her walkable edge that the two-press exit treats as the rail. ⚠ Only " +
                 "the cape islander and the lobster family carry real washboards; an open boat (the " +
                 "starter dory, the punt, the skiffs) has none, and absence is data — this is the band " +
                 "she gets instead. Clamped so it can never exceed half the walkable half-width, or on a " +
                 "narrow hull the 'band' would be the whole deck.")]
        [Min(0.05f)] public float WashboardWidthMetres = 0.45f;
        [Tooltip("Move-speed multiplier while standing out on the washboard (0..1). You are on the " +
                 "gunwale over open water: it should feel like somewhere you pick your way along, not " +
                 "somewhere you stroll.")]
        [Range(0.05f, 1f)] public float WashboardSlowFactor = 0.5f;
        [Tooltip("How far outboard of the rail she lands when she goes over the side, in metres — one " +
                 "body's width, so she is in the water BESIDE the hull rather than inside her own boat's " +
                 "footprint. Too small and she surfaces on top of the boat she just left; too large and " +
                 "she is flung clear of a hull she is supposed to be able to climb straight back onto.")]
        [Min(0.1f)] public float OverTheSideBodyWidthMetres = 0.7f;
        [Tooltip("How close a swimmer must be to a HULL OUTLINE, in metres, for the boat-only soft " +
                 "wall to step aside and let her into deep water — the owner's 2026-09-02 ask ('a " +
                 "player should be able to swim up to a hull and climb aboard anywhere'). ⚠ This is " +
                 "the ONE relaxation of the ratified water-travel model (water travel is boats only): " +
                 "everywhere further out than this the wall still stands, so raising it widens how much " +
                 "open sea a person may swim in. Ships at 6.0 — comfortably more than a boat's length " +
                 "off her rail, so you can miss your grab and try again, and far less than the gap " +
                 "between two berths, so it never joins one boat's water to the next one's.")]
        [Min(0f)] public float SwimBoardReachMetres = 6.0f;

        [Tooltip("The highest freeboard (top of the hull side above the water, metres) a hull may have " +
                 "and still be climbed anywhere from the water. At or under this she behaves as every " +
                 "boat did before 2026-11-13 (the dory, the punt, the skiffs). Over it — or railed, or " +
                 "flagged ladder-only on her deck Def — she is climbed from the water only at one of her " +
                 "boarding points (BoatDeckDef.BoardingPoints); with none, only from a wharf or gangway. " +
                 "Going over the side is governed by her rails, not by this number. Owner, 2026-11-13: " +
                 "'player can easily climb up massive walls which is wrong, should only be ladders'.")]
        [Min(0f)] public float ClimbAnywhereMaxFreeboardMetres = 1.0f;

        [Tooltip("How close (m) you must be to a hull's ladder, swim platform or rail gate to use it, " +
                 "when that point does not author its own reach. Measured in the hull's own deck frame, " +
                 "from the swimmer in the water or from the rider at the rail.")]
        [Min(0.1f)] public float BoardingPointReachMetres = 2.0f;

        [Header("Seakeeping forces (ADR 0018 B3 — the sea pushes the boat)")]
        [Tooltip("World-wide seakeeping FORCE policy (the sea fighting back): the master switch + bite " +
                 "strength, how the bite grows with sea state, how exposure falls off with depth (open water " +
                 "bites, the lee of land is sheltered), and how much a head / beam / following sea matters. " +
                 "ON by default with a moderate 'first feel' bite — calm sheltered water is UNCHANGED by " +
                 "construction (force scales by SeaState01 × exposure). Dial Strength to taste; set Enabled " +
                 "off to restore today's flat-water handling. Per-hull response lives on each BoatHullDef.")]
        public SeakeepingSettings Seakeeping = SeakeepingSettings.Default;

        [Header("Sea yaw (the wind and the sea turn her head)")]
        [Tooltip("World-wide policy for how much the wind and the waves TURN a hull (P1): gusts and a beam " +
                 "wind nudge the heading, a quartering or following sea makes the stern wander, light hulls " +
                 "are turned more than heavy ones (scaled by each hull's mass, windage and seakeeping " +
                 "response), drawing sails add weather helm (BoatHullDef.SailWeatherHelm). Quoted as a share " +
                 "of the hull's own full helm and capped by MaxHelmShare, so the rudder wins with way on. " +
                 "Calm water, sheltered water and a hull tied to the shore are UNCHANGED by construction. " +
                 "Enabled off restores the handling from before.")]
        public SeaYawSettings SeaYaw = SeaYawSettings.Default;

        [Header("Storm seakeeping read (ADR 0018 B2.5 — the hull answers the storm)")]
        [Tooltip("World-wide STORM policy for the VISUAL rock (the owner's 'the boat seemed to not be " +
                 "responsive to the waves'): above a storm-start sea state the rock's gains, caps and a " +
                 "mesh hull's rock amplitudes GROW with the sea, a mesh hull gains real front-to-back " +
                 "pitch from the wave slope, output smoothing tightens, and the vertical ride carries " +
                 "WEIGHT (a gravity-capped spring chase — the hull unweights over a steep crest and " +
                 "lands, never bolted to the surface). Calm/moderate water is byte-identical by " +
                 "construction (the blend is exactly 0 below the storm start); Enabled off restores " +
                 "today's read at every sea state. Per-hull character rides each BoatHullDef's " +
                 "existing seakeeping data.")]
        public StormRockSettings StormRock = StormRockSettings.Default;

        [Header("Hull weight (water fidelity PR 10 — a hull is not a float on the surface)")]
        [Tooltip("World-wide policy for how a hull ANSWERS the sea vertically (owner playtest " +
                 "2026-09-05: \"the boats seem to have too much hangtime after a big wave and bob up " +
                 "and down too fast and jerky as if they have no weight\"). Two physical facts: her " +
                 "ride is the field averaged over her own WATERLINE (so a wave shorter than the hull " +
                 "cancels under her and a wave longer lifts the whole boat), and she answers it " +
                 "through her own NATURAL PERIOD T = 2π√(m/ρgA_wp) at her own damping ratio (so she " +
                 "lags a swell, settles after it, and cannot twitch to a chop). Per-hull character " +
                 "comes from each BoatHullDef's length, beam, draught and SeakeepingDamping — there " +
                 "is no second per-hull knob. Enabled off restores the point-sampled, " +
                 "surface-bolted ride bit-for-bit; that is the A/B.")]
        public HullWeightSettings HullWeight = HullWeightSettings.Default;

        [Header("Hull trim (the bow answers her speed — owner 2026-09-21)")]
        [Tooltip("World-wide policy for how a hull TRIMS (owner 2026-09-21: \"Also i want trim " +
                 "added to the boats depending on speed and deacceleration\"). Her bow rises as she " +
                 "climbs her own bow wave toward hull speed (HumpFroude), a planing hull settles once " +
                 "she is over it (PlaningFroude), the bow squats up when the throttle opens and dips " +
                 "briefly when she slows. How MUCH is per hull — each BoatHullDef's Trim* fields; this " +
                 "block holds where the hump sits and the limits and lag a hull that authors none " +
                 "inherits. Enabled off (or a hull whose Trim* values are all 0) draws her pitch " +
                 "exactly as before trim existed; that is the A/B.")]
        public HullTrimSettings HullTrim = HullTrimSettings.Default;

        [Header("Fleet speed (the go-fast stays the fastest boat — owner 2026-11-13)")]
        [Tooltip("How far under the go-fast's own top speed every other planing hull is held, in m/s. " +
                 "The owner ruled (2026-11-13) that no planing hull may out-run the go-fast: a hull whose " +
                 "0.45 x real-world speed would pass her is authored at (the go-fast's still-water top " +
                 "speed minus this margin) instead. Nothing reads this at run time; a hull's speed is her " +
                 "own BoatHullDef.EnginePower, and FleetSpeedTableTests holds every hull Def to this cap. " +
                 "Move it, then re-solve EnginePower on the capped hulls " +
                 "(docs/design/fleet-speed-and-fuel-audit.md).")]
        [Min(0f)] public float PlaningTopSpeedMarginUnderGoFastMps = 0.1f;

        [Header("Ground tackle (dropping the hook)")]
        [Tooltip("World-wide ANCHORING policy: the dinghy-class rode a hull carries when her own Def " +
                 "does not say (BoatHullDef.RodeMeters = 0), the swing-circle floor, the firm-limit trio " +
                 "the rode is checked with (the mooring rope's own numbers — one restraint mechanism, two " +
                 "consumers), and how a DRAGGING anchor lets her creep when a rising tide takes the bottom " +
                 "away from the hook. The gate itself is not tunable: she anchors where the rode reaches " +
                 "the seabed, and nowhere else.")]
        public AnchorSettings Anchor = AnchorSettings.Default;

        [Header("The shared wave field (ADR 0018 — ONE sea, every consumer)")]
        [Tooltip("The wind + sea-state → wave-train derivation: how many trains, their wavelengths and " +
                 "amplitudes, the crest sharpening, and the ADR 0027 JONSWAP spectrum (SpectrumBlend 0 = " +
                 "the hand-authored 4-train sea; dial it up for variance in sizes, a fan of directions, " +
                 "and waves that arrive in SETS).\n\n" +
                 "⚠️ THIS IS THE ONLY PLACE THESE LIVE. Eight components used to carry their own copy — " +
                 "the water shader's bridge, the hull rocking, the seakeeping forces, the wake, the " +
                 "buoys, the trap haul and the drift weed — and tuning one without the others meant the " +
                 "hull rode a sea the shader was not drawing, which is the one thing ADR 0018 exists to " +
                 "prevent.")]
        public WaveFieldSettings WaveField = WaveFieldSettings.Default;

        [Tooltip("Legacy asset compatibility. The shared sea now uses WaveField's deterministic wind windows; these easing values are inactive.")]
        public WaveFieldAnimatorSettings WaveFieldAnimator = WaveFieldAnimatorSettings.Default;

        [Tooltip("WIND FETCH (ADR 0027 #1): how far the wind has blown over open water before it " +
                 "reaches a spot, and what that does to the waves there — lee shores go calm, exposed " +
                 "shores build. SHIPS OFF (Strength 0 = the exact passthrough); turn Strength up to " +
                 "see it.\n\n" +
                 "⚠️ This is ONE field, not a look. The envelope scales the waves the HULL rides as " +
                 "well as the ones the shader draws — deliberately, so the player never sees glass " +
                 "behind a headland and feels open-water swell in it. Raising Strength changes boat " +
                 "feel and wants a feel verdict, like the spectrum did.")]
        public WaveFetchSettings WaveFetch = WaveFetchSettings.Default;

        [Tooltip("THE WHOLE HULL MAKES FOAM: where a hull churns the water besides her trail. Her trail is " +
                 "shed from the bow when she goes astern; the end she drives into the water and the side " +
                 "she pushes through it foam too. Drawn only: no boat feels it. Weights at 0 = the trail alone.")]
        public HullFoamSettings HullFoam = HullFoamSettings.Default;

        [Tooltip("REAL WAVES FROM HULLS: a hull making way lays wave sources at her stern and her bow; each "+
                 "sends a ring of waves out across the water, and together they make the V-shaped wake with "+
                 "transverse waves behind her, sized by her length and speed. The water draws them (lift, "+
                 "light, crest foam) and other hulls' drawn ride bobs on them. Drawn only: no force.")]
        public HullWaveSettings HullWaves = HullWaveSettings.Default;

        [Tooltip("ONE FOAM SHEET: every foam on the water (wake, shore foam line, whitecaps, surf, storm " +
                 "streaks, convergence foam) is drawn as the same substance in different amounts: the same " +
                 "lace, bands and ageing colour. These dials say how each source joins it; 0 on a source's " +
                 "join dial brings back its older separate look. Drawn only.")]
        public FoamSheetSettings FoamSheet = FoamSheetSettings.Default;

        [Tooltip("STILL POOLS: water the falling tide has cut off from the open sea draws still: no swell, " +
                 "no pulsing edge, no surf or shore foam. The rising tide reconnects it and the swell eases " +
                 "back in over the blend. Recomputed from the seabed and the tide, never saved.")]
        public SeaReachSettings SeaReach = SeaReachSettings.Default;

        [Tooltip("BREAKING WAVES (ADR 0040): where the sea gives out and how — the breaker index γ, " +
                 "the Iribarren thresholds that separate a spilling crumble from a plunging barrel, and " +
                 "the whitewater decay. Ships at the TEXTBOOK PHYSICS (γ = 0.78, Battjes' 0.5/3.3/5.0), " +
                 "because these are constants of the sea rather than art direction.\n\n" +
                 "⚠️ WHERE the surf appears is decided by the PAINTED SEABED and the TIDE, not here — " +
                 "depth is waterLevel − seabed, so a bar that boils at half-ebb sleeps at high water with " +
                 "nothing animating it. Widening the plunging band puts barrels on shoals that have not " +
                 "earned them, which is the one dial to be careful with.")]
        public BreakerSettings Breakers = BreakerSettings.Default;

        [Tooltip("THE LIP SPRAY (ADR 0040 rev 3) — the torn puffs a plunging breaker throws shoreward as " +
                 "its crest arrives, as a move away from the burst that SHIPS: 0 = exactly the shipped " +
                 "spray, −1 = silent, +1 = twice as heavy. A burst can only be judged live (no " +
                 "screenshot can show it), which is why it ships on and this is the dial rather than a " +
                 "switch. " +
                 "⚠️ Written as an OFFSET from the shipped value and not as the intensity itself, on " +
                 "purpose: a YAML key this asset does not carry deserializes to ZERO, and for a plain " +
                 "intensity that zero would silently switch the feature off in every older asset. As an " +
                 "offset, zero IS the shipped spray.")]
        [Range(-1f, 1f)] public float SurfSprayIntensityOffset;

        /// <summary>
        /// The lip spray's MASTER (0 = silent, 1 = the shipped burst, 2 = twice it) — the resolved form
        /// of <see cref="SurfSprayIntensityOffset"/>, which is stored as a move away from the shipped 1
        /// so that a missing YAML key (a zero) reads as "ship it as ruled" rather than as "off". A
        /// PROPERTY, so Unity serializes nothing here and the asset-coverage test asks for no key.
        /// </summary>
        public float SurfSprayIntensity => Mathf.Clamp(1f + SurfSprayIntensityOffset, 0f, 2f);

        [Header("Round floats (the waterline wraps the body)")]
        [Tooltip("The player's pot float's radius at its waterline, in metres (the spindle float of the " +
                 "pot-float rig, PotFloats_bullet). A round float meets the water in a circle the 3/4 camera " +
                 "draws as an ellipse, so its waterline bows down across the near side and wraps up to the " +
                 "sides; this is how wide that ellipse is. 0.084 is the rig's own barrel radius at the still " +
                 "waterline (META.bullet.waterRadius; a test holds the two together). 0 = the old straight " +
                 "cut. NPC pots take theirs from their TrapDef, the ambient fleet from its AmbientFleetDef, " +
                 "the navigation marks from their own DiameterMeters.")]
        [Min(0f)] public float LobsterFloatRadiusMeters = 0.084f;

        [Tooltip("The player's pot float paint: the body coat. Buoy colour says whose gear it is, and the " +
                 "player's is YELLOW. Swapped onto the baked float at draw time (one sheet for every owner).")]
        public Color PlayerPotFloatColour = new Color32(0xe0, 0xb1, 0x3a, 0xff);
        [Tooltip("The player's pot float paint: the band (shoulder stripe, stick tip, flag stripe).")]
        public Color PlayerPotFloatBandColour = new Color32(0x9c, 0x4a, 0x3c, 0xff);
        [Tooltip("The band every OTHER fisher's float wears when their data names only a body colour " +
                 "(NPC pot days, the ambient fleet). Dark, so it reads on every body colour and never " +
                 "matches the player's rust band.")]
        public Color PotFloatBandColour = new Color32(0x2a, 0x3b, 0x52, 0xff);

        [Header("Pot floats on the water (the ride and the drawn size)")]
        [Tooltip("How far a trough may lower the player's spindle float's waterline, in metres (META.bullet.draft): " +
                 "down toward its foot, never so far that its rope eye shows.")]
        [Min(0f)] public float LobsterFloatDraftMeters = 0.08f;
        [Tooltip("How far a crest may raise the player's spindle float's waterline, in metres (META.bullet.freeboard): " +
                 "up to its shoulder band, so a crest washes over the body but never swamps the whole float.")]
        [Min(0f)] public float LobsterFloatFreeboardMeters = 0.17f;
        [Tooltip("How much of the wave a small foam float follows (0..1). It lifts by this share of the wave " +
                 "height and only the rest climbs its side, held within its own reach (draft below, freeboard above). " +
                 "Every pot and marker float rides this way; the navigation marks keep their own bob.")]
        [Range(0.05f, 1f)] public float PotFloatSurfaceFollow = 0.75f;
        [Tooltip("The pot and marker floats' drawn size against their true size (1 = true size: a spindle " +
                 "float ~6 px wide at 32 px a metre). One number for every float; the waterline arc and the " +
                 "ride scale with it. 2 = the owner's pick, 2026-11-25: true size read as a speck from the dory.")]
        [Range(0.5f, 3f)] public float PotFloatDrawScale = 2f;
        [Tooltip("Half-height of the pale waterline band on a pot or marker float, in the sprite's own texels. " +
                 "The buoy default is a fraction of the sprite's height (several texels on a small float), which " +
                 "bled down a thin stick as a pale tick in a trough. 1.5 = one bright row at the line.")]
        [Range(0.5f, 4f)] public float PotFloatFoamTexels = 1.5f;

        [Header("Market (VS-16)")]
        [Tooltip("Demand D at the home cove (Coddle Cove) in priceMult = 1/(1+e·S/D). 1 = neutral baseline.")]
        [Min(0.01f)] public float MarketDemandCove = 1f;
        [Tooltip("Demand D at Nine Mile Creek. Set higher than the cove so WHERE you sell matters: Nine Mile Creek " +
                 "pays a premium on a glut (the reason to make the hop), and its supply recovers separately. " +
                 "(economy-and-business §1.2/§1.4)")]
        // ⚠ RENAMED from MarketDemandGreywick (plan-to-m1 §7.10). The old name is the KEY this
        // value is serialised under in every existing GameConfig.asset — including the owner's
        // hand-tuned local copy — so without this attribute the rename would silently reset his
        // tuning to the 1.4 default and nothing would say so.
        [UnityEngine.Serialization.FormerlySerializedAs("MarketDemandGreywick")]
        [Min(0.01f)] public float MarketDemandNineMileCreek = 1.4f;
        [Tooltip("Demand D at the ISLAND GENERAL STORE on St Peters (plan-to-m1 §7.5) — the first market the " +
                 "player meets, and deliberately the WORST. Keep this BELOW MarketDemandNineMileCreek: the " +
                 "gap IS the economic reason to walk the tide-gated sandbar, and it is what teaches 'where " +
                 "you sell matters' in the first hour. A village shop buying clams over the counter is not " +
                 "competing with a wharf that ships to the mainland — the default is a little under the cove " +
                 "baseline, so the store pays worst, the cove middling, Nine Mile Creek best.")]
        [Min(0.01f)] public float MarketDemandStPetersStore = 0.7f;

        // ---- what each outlet PAYS, before any glut (the price LEVEL) ----------------------------
        // ⚠ WHY THIS EXISTS AND DEMAND ALONE WAS NOT ENOUGH. Demand D only ever appears as S/D inside
        // priceMult = 1/(1+e·S/D). At zero supply that term is 1 for EVERY value of D — so on a market
        // nobody has sold into yet, a low-demand outlet and a high-demand one quote the *same* price, and
        // the first clam of the game fetches the same coin at the village counter as on the Creek's wharf.
        // Demand is a GLUT-ABSORPTION lever, not a price-level one, and plan-to-m1 §7.5's "deliberately
        // worse prices" is a level difference. So this is the level term the canon formula already has:
        // effPrice = P0 · demandMood · seasonDemand · priceMult (economy-and-business §1.2). These are the
        // static seed of that `demandMood` — a per-market multiplier on base value, kept SEPARATE from the
        // supply curve so the two stay independently tunable. When the M2 sim gives demandMood its random
        // walk, it multiplies onto this rather than replacing it.
        [Tooltip("What the home cove pays per unit before any glut, as a multiplier on the species' base " +
                 "value. 1 = pays the book price (the neutral baseline).")]
        [Min(0.01f)] public float MarketPriceLevelCove = 1f;
        [Tooltip("What Nine Mile Creek pays per unit before any glut. 1 = the book price; it is the BEST " +
                 "outlet in M1 by virtue of also having the highest demand (it absorbs a glut better).")]
        [Min(0.01f)] public float MarketPriceLevelNineMileCreek = 1f;
        [Tooltip("What the ISLAND GENERAL STORE pays per unit before any glut — the ONE number that makes " +
                 "the crossing pay (plan-to-m1 §7.5). Keep it BELOW MarketPriceLevelNineMileCreek: this is " +
                 "the gap the player feels on their first bucket of clams, and the whole 'where you sell " +
                 "matters' lesson. 0.6 = the village counter pays 60% of dockside. ⚠ Every unit is floored " +
                 "at ₲1 (SellPricing.UnitPrice), so on a 2₲ clam this lever has only 2₲ of room to move — " +
                 "see the note on FishSpeciesDef base values in m1-progression-pacing §5.")]
        [Min(0.01f)] public float MarketPriceLevelStPetersStore = 0.6f;

        [Tooltip("Fraction of a category's accumulated supply (glut) cleared at each daily settle (0..1). " +
                 "Higher = faster price recovery over days (economy-and-business §1.3). Deterministic — fired " +
                 "on day rollover, not per frame.")]
        [Range(0f, 1f)] public float MarketDailyRecovery = 0.5f;

        [Header("The fronted licence fee (plan-to-m1 §7.5 — the Aunt Ginny beat)")]
        // ⚠ THIS NUMBER EXISTS TO PREVENT A DEADLOCK, not for flavour. CatchLicensePolicy.MayLand FAILS
        // CLOSED: a species some licence gates cannot be landed without that licence. The clam licence gates
        // clams, and clams are the player's only income on day one — so "buy the clam licence with clam
        // money" is a hard soft-lock the moment the dig starts consulting the gate. §7.5's ruled fix is a
        // character beat rather than a mechanic: Ginny FRONTS the fee, and the player still walks to the
        // store and buys the licence themselves (they meet the vendor, they learn licences gate species, and
        // it plants a small warm debt). This is the mechanism half of that beat; her words are world-content's.
        // GUARD-RAIL: keep this ≥ the clam licence's Price. StPetersContentValidationTests enforces it, so a
        // reprice of the licence cannot silently re-open the deadlock.
        [Min(0)]
        [Tooltip("₲ Aunt Ginny fronts the player once, so the clam licence can be bought before there is any " +
                 "clam money. Must stay ≥ the clam licence fee (content validation enforces it). Granted " +
                 "once per game, flag-guarded — see FrontedFeeGrant.")]
        public int FrontedLicenceFee = 20;

        [Header("Carrying (two hands — the size a fish must be under to ride one of them)")]
        [Tooltip("How long a fish may be, nose to tail in metres, and still be carried in ONE hand by " +
                 "the gill — leaving the other hand for the rod or the pail. Anything longer is cradled " +
                 "across both arms, so it can only be picked up with both hands free. Default 0.62 is " +
                 "the fish rig's own 2.2 kg cradle point expressed as a length, so the hands and the " +
                 "picture agree by construction. Raise it and the big ones stop being a decision; drop " +
                 "it and even a small cod occupies you completely.")]
        [Min(0f)] public float MaxOneHandFishLengthMeters = DefaultMaxOneHandFishLengthMeters;

        [Tooltip("Metres of fish per cube root of a kilogram — how a landed catch's WEIGHT (the number " +
                 "the item actually carries) becomes the LENGTH the rule above compares against. The " +
                 "fish rig builds mass from length × girth² and girth tracks length, so mass goes as the " +
                 "cube of length; this is that law inverted, fitted to the rig's own four species. " +
                 "⚠️ Not a feel dial — move it only if the fish rig's proportions move.")]
        [Min(0f)] public float FishLengthPerKgCubeRootMeters = DefaultFishLengthPerKgCubeRootMeters;

        [Tooltip("The shortest a fish may be DRAWN, in sprite pixels, wherever it is drawn: on the " +
                 "line, in his hands, in the tote. A catch whose size rung is drawn shorter than this " +
                 "is shown with the smallest rung of its species that is at least this long (or the " +
                 "largest, if none is). One rule for all three places, so the fish on the line, in the " +
                 "hands and in the tote is always the same drawing. The catch's weight, value and " +
                 "one-hand rule are untouched: only the picture is chosen. 0 turns the floor off. " +
                 "Default 12 lifts only the tiniest drawings; 16 lifts every mackerel and flounder " +
                 "to the large rung, and higher starts to erase the size ladder.")]
        [Min(0f)] public float MinDrawnFishLengthPx = DefaultMinDrawnFishLengthPx;

        [Tooltip("The contrast line on a small fish in his hands: a held fish DRAWN this long or shorter " +
                 "(sprite px, the length measured off its held sheet) gets a dark line along its edges so " +
                 "it reads against his clothes and the deck. Not the interact outline: no lift, no glow, " +
                 "just the fish's own colours a step darker. 0 turns the line off. Default 16 lines every " +
                 "herring and mackerel and the small haddock, bass, pollock and flounder.")]
        [Min(0f)] public float HeldFishKeylineMaxLengthPx = DefaultHeldFishKeylineMaxLengthPx;

        [Tooltip("How dark the contrast line is: the fish's own colours multiplied by this (0 black, 1 no " +
                 "line at all). Default 0.4.")]
        [Range(0f, 1f)] public float HeldFishKeylineShade = DefaultHeldFishKeylineShade;

        [Tooltip("Where the contrast line is drawn: one darker copy of the held fish behind it per entry, " +
                 "moved this many SPRITE PIXELS. The default (-1,0) and (1,0) lines both long edges of a fish " +
                 "hanging by its tail; a single entry gives a line down one side only.")]
        public Vector2[] HeldFishKeylineOffsetsPx = DefaultHeldFishKeylineOffsetsPx();

        [Header("Helm throttle (the notched single-lever throttle)")]
        [Tooltip("Feel of the diegetic notched throttle on engine hulls: how many detents from neutral to " +
                 "full AHEAD and to full ASTERN (reverse usually shorter), and an optional hold-to-repeat " +
                 "rate. Up/Down step a HELD detent (so you can sit at part-throttle — impossible with the " +
                 "old on/off key). The detent value IS the LeverRig 'drive' the console draws. Consumed by " +
                 "ThrottleDetentModel; owner-tunable, no code (rule 6).")]
        public HelmThrottleSettings HelmThrottle = HelmThrottleSettings.Default;

        [Header("Helm overlay (the diegetic instrument card — ADR 0025 S1)")]
        [Tooltip("The screen-space card that shows the active hull's control (tiller or binnacle lever) " +
                 "while piloting a motorised hull: placement, the click-to-focus enlargement, and the " +
                 "pointer-mapping radii. Presentation only — reposition/rescale freely, no code (rule 6).")]
        public HelmOverlaySettings HelmOverlay = HelmOverlaySettings.Default;

        [Header("Depth sounder (the purchasable brow instrument — ADR 0025 S2)")]
        [Tooltip("The diegetic depth sounder: how often it takes a sounding, its shallow-alarm defaults " +
                 "and travel, the alarm flash rate, and where its card sits on screen. Also carries the " +
                 "PLACEHOLDER water temperature the LCD shows until a real water-temperature model " +
                 "exists (there is none in the sim today — no temperature is invented here).")]
        public DepthSounderSettings DepthSounder = DepthSounderSettings.Default;

        [Header("Fish finder (the sonar upgrade — ADR 0025 S3)")]
        [Tooltip("The colour sonar that replaces the plain depth sounder in the same cutout: the vertical " +
                 "RANGE (metres) a freshly fitted unit starts at, how often the scan repaints, where its " +
                 "card sits on screen, how fish marks are sized and scattered, and the two PLACEHOLDER " +
                 "status-strip values. The shallow alarm is NOT here: the finder keeps the depth sounder's " +
                 "alarm and reads its settings, so there is only ever one alarm rule.")]
        public FishFinderSettings FishFinder = FishFinderSettings.Default;

        [Header("Chartplotter (the GPS chart — ADR 0025 S6)")]
        [Tooltip("How much navigation the player may keep (waypoints, route legs, track breadcrumbs), " +
                 "how far the boat must move before the track takes another crumb, and the chart's " +
                 "RANGE ladder in nautical miles. The caps are what keep nav data bounded in the save; " +
                 "the range ladder is what makes a chart of a few-hundred-metre harbour readable.")]
        public ChartplotterSettings Chartplotter = ChartplotterSettings.Default;

        [Header("Radar (the PPI scope — ADR 0025 S5)")]
        [Tooltip("What the radar can see and how it draws it: the RANGE ladder in nautical miles, how " +
                 "hard the set is turned up (gain), how much sea clutter a full gale throws, and how " +
                 "finely the coastline is scanned. The range ladder is what makes a scope of a " +
                 "few-hundred-metre harbour readable; the scan settings are the rule-7 budget for the " +
                 "land echo.")]
        public RadarSettings Radar = RadarSettings.Default;

        [Header("Helm wheel (the grabbable steering wheel — ADR 0025 S2a)")]
        [Tooltip("Mouse-spin feel of the console/sport steering wheel: lock-to-lock turns, coast " +
                 "friction, and the optional self-centre spring (0 = a real cable helm that HOLDS " +
                 "where released; > 0 = springy arcade feel). Rig defaults from wheelRig.js; " +
                 "owner-tunable, no code (rule 6).")]
        public HelmWheelSettings HelmWheel = HelmWheelSettings.Default;

        [Header("Helm autopilot (heading hold — only on hulls whose Def says AutopilotOffered)")]
        [Tooltip("How the autopilot holds a heading on the rudder the hull already has: the three " +
                 "gains, how much helm it may use and how fast it may wind the wheel, and the limits " +
                 "that hand the helm back (too little way, a knock, a steer input). Owner-tunable, no " +
                 "code (rule 6).")]
        public HelmAutopilotSettings HelmAutopilot = HelmAutopilotSettings.Default;

        [Header("Helm footprint (the camera answers a full-width helm band — ADR 0050)")]
        [Tooltip("How the camera answers a helm band that spans the whole width of the screen: the " +
                 "share of the band's height the boat's place on screen rises by (0.5 = half, the " +
                 "ruling: the boat sits centred in the sea the band leaves), and how long the move " +
                 "takes to ease in and out. A card never moves the camera. Presentation only — " +
                 "never saved.")]
        public HelmFootprintSettings HelmFootprint = HelmFootprintSettings.Default;

        [Header("The strike (owner drop §10.2 — \"pull back and press maybe?\": BOTH candidates, tunable)")]
        [Tooltip("Which gesture sets the hook on the true take, and how hard the pull-back must be. " +
                 "BOTH candidates ship ON so the owner picks in play — turn one off to feel the other " +
                 "alone. The strike is judged by the same bite sequence the tells render from; these " +
                 "dials only decide what counts as the player striking.")]
        public StrikeSettings Strike = StrikeSettings.Default;

        [Header("Bait economy (switchable fishing-feel ruling)")]
        [Tooltip("ON in the shipped GameConfig asset: spend one bait only on a LANDED catch. " +
                 "Teases, missed strikes, lost fights and unlicensed releases cost time only. " +
                 "OFF restores spending at the BITE, landed or not; new C# configs still default OFF. " +
                 "Approved for now on 2026-11-13; keep switchable for play review. Changes bait's " +
                 "real cost per fish (economy-sim pacing §7); does not change prices or lot sizes.")]
        public bool BaitSpentOnCatchOnly = false;

        [Header("Rod fight (Rod Fishing v2 — the deep→surface fight, cove defaults)")]
        [Tooltip("Fight-wide DEFAULT tuning for the v2 rod fight (pull-on-slack / maintain-on-run + " +
                 "counter-steer + the deep→surface arc). These are the forgiving cove baselines the owner " +
                 "dials; a species' RodFightDef overrides them per fish later. Two guard-rails keep the cove " +
                 "cozy and are test-enforced: TensionRisePerSec > LandingFillPerSec (a blind sustained pull " +
                 "SNAPS before it lands — skill is a pulse, not a pin) and RunTensionPressure < " +
                 "TensionFallPerSec (MAINTAIN always bleeds tension, even mid-run — a run is a 'back off' " +
                 "tell, never an unavoidable snap).")]
        public RodFightSettings RodFight = RodFightSettings.Default;

        [Header("Flick-cast (Rod Fishing v2 — the gesture cast)")]
        [Tooltip("The mouse-gesture cast that replaced the old press-to-cast: HOLD to start, drag the " +
                 "mouse BEHIND the character to wind the rod back, sweep it forward past them, and RELEASE " +
                 "to let the spool loose. Where you flicked = direction; how fast/far you swept = power " +
                 "(capped by the rod); WHEN you released = quality. A mistimed or weak cast is just a SHORT " +
                 "cast — reel in and go again, no penalty. Every feel dial lives here.")]
        public FlickCastSettings FlickCast = FlickCastSettings.Default;

        [Header("Mooring lines (M2-38 — throw a rope to a cleat, mind the tide)")]
        [Tooltip("The rope you throw to a cleat and make fast: how near you must stand to work a cleat, " +
                 "how close the toss must land to catch, and — the part that carries the seamanship — how " +
                 "much SCOPE (line length) you can pay out, in what steps, and how hard the loop will be " +
                 "worked before it slips. Scope is the player's choice and the tide is the test: too short " +
                 "a line on a falling tide hangs the boat and the loop surrenders (no damage — coil and " +
                 "try again). Every dial is here so the feel is tuned without code.")]
        public MooringLineSettings MooringLine = MooringLineSettings.Default;

        [Header("Ladder boarding (the tide-gap climb — when a step aboard becomes a climb down)")]
        [Tooltip("How the fisher gets aboard when the tide has dropped the boat below the wharf: the GAP " +
                 "at which a step becomes a climb, how near a ladder must be to serve a berth, and the " +
                 "measured rig geometry the climb is driven by. The threshold is the dial that matters — " +
                 "raise it and low water still lets you step across (the ladder becomes a rarity); lower " +
                 "it and the ladder is the ordinary way aboard for most of the tide.")]
        public LadderBoardingSettings LadderBoarding = LadderBoardingSettings.Default;

        [Header("Displaced water (ADR 0023 — the sea's readable drama)")]
        [Tooltip("Owner tuning for the displaced water surface (ADR 0023, phase 2): how much taller " +
                 "the sea DRAWS than it simulates, how wide the tear-safe calm band along every shore " +
                 "is, and how strongly the rare big wave is marked by foam and shade. All read LIVE " +
                 "each water tick (~8 Hz), so tuning this asset in Play moves the sea within a " +
                 "second. WaveExaggeration is THE one shared constant every water-riding visual " +
                 "reads (surface lift now; hull heave, buoys and wake in phase 3) — tune it here " +
                 "and boat and sea stay on the same water, never retuned apart.")]
        public DisplacedWaterSettings DisplacedWater = DisplacedWaterSettings.Default;

        [Header("Light from the water on hull sides")]
        [HideInInspector] public float HullCausticBandMeters = 0.8f; // Serialized legacy width; live reach uses the two terms below.
        [Min(0f)] public float HullCausticBandBaseMeters = 0.35f;
        [Min(0f)] public float HullCausticBandPerChopMeters = 0.9f;
        [Range(0f, 1f)] public float HullCausticFresnelF0 = 0.02f;
        [Range(0.01f, 90f)] public float HullCausticReferenceElevationDegrees = 15f;
        [Min(1f)] public float HullCausticFresnelPower = 5f;
        [Range(0f, 1f)] public float HullCausticChopDimming = 0.5f;
        [Range(0f, 1f)] public float HullCausticAwayFloor = 0.15f;
        [Min(0.01f)] public float HullCausticIrradiancePower = 1f;
        [Range(0f, 1f)] public float HullCausticIrradianceFloor = 0.05f;
        [Range(0f, 45f)] public float HullCausticFacetTiltDegrees = 12f;
        [Min(0f)] public float HullCausticStrength = 0.18f;
        [Tooltip("Visible vein gain inside the existing hull band; does not widen its mask.")]
        [Min(0f)] public float HullCausticVeinGain = 1f;
        [Range(0f, 1f)] public float HullCausticMaxUpwardNormal = 0.5f;

        // -----------------------------------------------------------------------------------------
        //  The floor in the shallows (W4-4; ruling 7 of 2026-10-10, "Shallows: 2B")
        // -----------------------------------------------------------------------------------------
        // In clear shallow water the sea draws the ground's own textures inside its opaque composite
        // (2B; 2A, the splat colours baked into _SeabedTex, stays unbuilt as the mobile-port fallback).
        // Each step has its own switch, and each switch is an EXACT passthrough when off: today's
        // pixels, bit for bit. FLAT fields whose code defaults are OFF, so a GameConfig.asset that lags
        // the code reads false, which is the safe side (the pixel-grid note below says why flat).

        /// <summary>W4-4a's ship default — <b>OFF</b> until its plates are accepted. With it on, the
        /// ground skips its shading wherever the displaced sea covers it opaquely this frame (the
        /// coverage <c>IsoFacetHullFeature</c> draws); the sea paints its own bed there, so no pixel
        /// changes and the terrain's shading under the water is saved.</summary>
        public const bool DefaultTerrainUnderwaterClip = false;

        [Header("The floor in the shallows (W4-4 — the ground seen through clear water)")]
        [Tooltip("Skip the ground's shading under the displaced sea? The sea's composite paints its own " +
                 "bed wherever it covers the ground opaquely, so those ground pixels are never seen: ON " +
                 "changes no pixel and saves their shading; OFF draws the ground everywhere, as before. " +
                 "The clip follows the sea's own coverage this frame (still water, swash and hulls " +
                 "included), never a waterline of the ground's own. Read per camera per frame.")]
        public bool TerrainUnderwaterClip = DefaultTerrainUnderwaterClip;

        /// <summary>W4-4b's ship default — <b>OFF</b> until its plates are accepted. With it on, wherever the
        /// sea composites its bed (a region with a seabed bake, under a mood with absorption) the bed is the
        /// ground's own albedo, drawn by the terrain's include: darkened and blued with depth by the mood's
        /// own absorption, lit by the caustics and graded, as the painted bed is.</summary>
        public const bool DefaultShallowsFloor = false;

        [Tooltip("Draw the ground's own textures as the floor of clear shallow water? ON: where the sea " +
                 "composites its bed, the bed is the ground the terrain draws, wobbling a little with the " +
                 "surface, darker and bluer with depth by the mood's absorption and lit by the caustics; " +
                 "murky and stormy moods hide it as they hide the painted bed. OFF: the painted bed, as " +
                 "before, bit for bit. Desktop only (a mobile build keeps the painted bed). Read every " +
                 "water tick (~8 Hz).")]
        public bool ShallowsFloor = DefaultShallowsFloor;

        /// <summary>The floor's bend by default: about 1 − 1/1.33, the turn light takes entering sea water
        /// from straight above, so a floor point d metres down is seen k × slope × d away.</summary>
        public const float DefaultShallowsFloorRefraction = 0.25f;

        [Tooltip("How far the surface's tilt moves the floor: a floor point is seen this fraction of " +
                 "slope × depth away, rounded to whole ground pixels. 0.25 is about light entering sea " +
                 "water from straight above; 0 holds the floor still under moving water; more " +
                 "exaggerates the wobble.")]
        [Range(0f, 1f)]
        public float ShallowsFloorRefraction = DefaultShallowsFloorRefraction;

        /// <summary>W4-4c's ship default — <b>OFF</b> until its plates are accepted. With it on, a shore plant
        /// wholly under the live water is drawn by the sea's own composite, over the floor and through the
        /// same column: darker and bluer with depth by the mood's absorption, bent with the floor, lit by the
        /// caustics and graded, as the floor beside it is.</summary>
        public const bool DefaultShallowsSubmergedProps = false;

        [Tooltip("Show the plants under clear shallow water through the water, as the floor is shown? ON: " +
                 "a plant wholly under the tide is drawn by the sea over the floor, darker and bluer with " +
                 "depth, wobbling with the floor and lit by the caustics; murky and stormy moods hide it as " +
                 "they hide the floor. OFF: as before, bit for bit (the sea's bed covers it). Desktop only: " +
                 "a mobile build's sea never reads it. Ship it with ShallowsFloor, whose bend it shares. " +
                 "Read per camera per frame, and by each plant on the tide tick.")]
        public bool ShallowsSubmergedProps = DefaultShallowsSubmergedProps;

        /// <summary>The weed's clarity by default: <b>0</b>, the floor's own law, so a config written before
        /// this existed and an unwired sea draw the weed exactly as before. The shipped asset carries the
        /// look's value (2026-11-17, the kelp beds).</summary>
        public const float DefaultUnderWaterWeedClarity = 0f;

        /// <summary>The most clarity the sea takes: some of the column is always read, so even the clearest
        /// weed darkens and blues a little with depth. The water shader clamps with the same value.</summary>
        public const float MaxUnderWaterWeedClarity = 0.9f;

        [Tooltip("How much less water the weed under the tide (kelp, wrack, eelgrass, the shore plants) is " +
                 "seen through than the floor it grows from. 0: the floor's own darkening and blueing with " +
                 "depth. 0.5: the weed reads as if it stood half as deep, so a kelp bed still shows as a dark " +
                 "mass at 3 to 6 m when the sand beside it has gone blue. Up to 0.9. Needs " +
                 "ShallowsSubmergedProps on. Read every water tick (~8 Hz).")]
        [Range(0f, MaxUnderWaterWeedClarity)]
        public float UnderWaterWeedClarity = DefaultUnderWaterWeedClarity;

        /// <summary>The clarity the sea is handed: within 0..<see cref="MaxUnderWaterWeedClarity"/>.</summary>
        public static float ClampUnderWaterWeedClarity(float clarity) =>
            Mathf.Clamp(clarity, 0f, MaxUnderWaterWeedClarity);

        /// <summary>The keyline gate's ship default — <b>OFF</b> (ADR 0031: the 1 px outline is retired
        /// from the world-art style, and the mesh fleet is the first family to go without it). A const so
        /// consumers with no config wired (EditMode, a bare test rig) resolve the SAME style the shipped
        /// game does — and because <c>GameConfig.asset</c> lags the code, the code default here is what
        /// actually ships.</summary>
        public const bool DefaultHullKeylineFlood = false;

        [Header("World-art style (ADR 0031 — the keyline retirement)")]
        [Tooltip("Draw the legacy 1 px keyline around every MESH hull (the fullscreen flood ported from " +
                 "the rigs — ADR 0022 phase 3, rule 2)? OFF is the shipped style since ADR 0031: the " +
                 "outline is retired, and a hull's edge is carried by its own shaded turning faces. ON " +
                 "restores yesterday's look byte-for-byte for an A/B. This gates ONLY the flood — the " +
                 "depth-edge darkening that keeps overlapping parts of one boat readable always runs. " +
                 "Baked SPRITE sheets (buildings, trees, props) still carry drawn keylines until their " +
                 "families are naturally redone; that mixed period is accepted (ADR 0031, Half B skipped).")]
        public bool HullKeylineFlood = DefaultHullKeylineFlood;

        /// <summary>The player reserve's ship default (ADR 0056, the owner's ruling on the facet-id budget): two
        /// facet ids — the player and one co-op friend. A const so a consumer with no config wired
        /// resolves the SHIPPED reserve, because <c>GameConfig.asset</c> lags the code.</summary>
        public const int DefaultPlayerFigureIdReserve = 2;
        /// <summary>The inspector's ceiling for <see cref="PlayerFigureIdReserve"/>: every id reserved is
        /// one the harbour's boats and the cast can never have.</summary>
        public const int MaxPlayerFigureIdReserve = 8;

        [Header("Facet ids (ADR 0056 — the id space is a budget)")]
        [Tooltip("How many of the 255 facet ids are held back for the PLAYERS' figures ashore (the player " +
                 "and each co-op friend). No boat, truck or villager can ever take one, so a player standing " +
                 "ashore is never left as her old sprite because the harbour's boats used the ids up. Each one " +
                 "reserved is one fewer for boats and the cast. Read once, when the first facet id of the " +
                 "session is handed out: a change applies from the next play session.")]
        [Range(0, MaxPlayerFigureIdReserve)]
        public int PlayerFigureIdReserve = DefaultPlayerFigureIdReserve;

        public const int DefaultHullReflectionMaxHulls = 64;
        public const int DefaultHullReflectionMaxPackets = 1024;
        public const int DefaultHullReflectionMaxTriangles = 1000000;
        public const float DefaultHullReflectionFadeStartMeters = 0.1f;
        public const float DefaultHullReflectionFadeMeters = 0.6f;
        public const float DefaultHullReflectionStreakPixels = 6f;
        public const float DefaultHullReflectionEndCutPixels = 3f;
        public const float DefaultHullReflectionEndThinPixels = 3f;
        public const float DefaultHullReflectionEndMinRunPixels = 12f;

        /// <summary>The boat-reflection path's ship default: <b>ON</b> (2026-11-21). The mesh path is the
        /// only one that can draw: the older overlay path records its reflection BEFORE the same frame's
        /// hull keyline resolve, and the render graph rebinds <c>_HHHullScreenTex</c> to black at the end of
        /// every graph, so that path always reads an empty picture and draws nothing.</summary>
        public const bool DefaultHullMeshReflections = true;

        [Header("Boat reflections in the water (hull, fittings and the figure aboard)")]
        [Tooltip("Draw each boat's reflection from her own posed meshes (hull, door leaf, fittings and the " +
                 "figure aboard). Off = no boat reflection at all: the older overlay path cannot draw.")]
        public bool HullMeshReflections = DefaultHullMeshReflections;
        [Range(1, 2)] public int HullReflectionResolutionDivisor = 1;
        [Min(1)] public int HullReflectionMaxHulls = DefaultHullReflectionMaxHulls;
        [Min(1)] public int HullReflectionMaxPackets = DefaultHullReflectionMaxPackets;
        [Min(1)] public int HullReflectionMaxTriangles = DefaultHullReflectionMaxTriangles;
        [Tooltip("How far below the waterline a boat's reflection runs whole before it starts to thin, metres " +
                 "(its source's height above the line). The thinning is the hull's own pixel dither, so the " +
                 "reflection stays crisp and fades with distance from the hull instead of sitting as one block.")]
        [Min(0f)] public float HullReflectionFadeStartMeters = DefaultHullReflectionFadeStartMeters;
        [Tooltip("Over how many metres below that the reflection thins to nothing. 0 = no fade (the look " +
                 "before the sunset rowing clip of 11-22: the whole mirrored hull at one strength).")]
        [Min(0f)] public float HullReflectionFadeMeters = DefaultHullReflectionFadeMeters;
        [Tooltip("How long the streaks a boat's reflection thins in are, in the hull's own pixels: each pixel " +
                 "row breaks into runs that go together, the way a reflection breaks on moving water. Under 1 = " +
                 "the hull's 4x4 ordered dither instead.")]
        [Min(0f)] public float HullReflectionStreakPixels = DefaultHullReflectionStreakPixels;
        [Tooltip("How many of the hull's own pixels each streak row of a boat's reflection may stop short at " +
                 "either end: every row draws its own amount from 0 up to this, fixed to the hull, so the " +
                 "reflection under a square stern or bow ends ragged instead of on one straight column. 0 = " +
                 "every row runs to the hull's outline.")]
        [Min(0f)] public float HullReflectionEndCutPixels = DefaultHullReflectionEndCutPixels;
        [Tooltip("Over how many of the hull's pixels inside that each streak row's end thins out (some pixels " +
                 "kept, more of them further in), so the end frays rather than stops. 0 = a clean end.")]
        [Min(0f)] public float HullReflectionEndThinPixels = DefaultHullReflectionEndThinPixels;
        [Tooltip("A reflection row shorter than this many of the hull's pixels keeps its ends, so a thin oar, " +
                 "rod or mast still reflects; only the long rows under the hull itself fray.")]
        [Min(0f)] public float HullReflectionEndMinRunPixels = DefaultHullReflectionEndMinRunPixels;

        /// <summary>The calm mirror's ship default: <b>0.6, on</b>. The owner liked 0.6 on the prototype
        /// (2026-11-23), it merged behind the dial at 0, and he approved turning it on at 0.6 on
        /// 2026-11-26. 0 still turns it off and gives the water exactly as before.</summary>
        public const float DefaultWaterMirrorStrength = 0.6f;

        [Tooltip("THE CALM MIRROR: how strongly calm water mirrors what stands on it (0..1). Boats, their " +
                 "fittings, the crew aboard and figures ashore show their true reflection, the view from " +
                 "below the water; wharves, sheds, piles and cliffs are flipped about where they meet the " +
                 "water. Fades out as the sea gets up. 0 = off, the water exactly as before. The owner's " +
                 "liked value is 0.6.")]
        [Range(0f, 1f)] public float WaterMirrorStrength = DefaultWaterMirrorStrength;

        [Tooltip("The calm mirror's shaping dials: sea-state fade, reach, ripple and darkening. Read only " +
                 "while WaterMirrorStrength is above 0.")]
        public WaterMirrorSettings WaterMirror = WaterMirrorSettings.Default;

        /// <summary>The cabin-glow passthrough's ship default — <b>OFF</b>, which is the owner's ruling
        /// of 2026-09-03: a glow is confined to its space, and an interior's reaches the outside only
        /// through the windows. A const for exactly the reason the keyline's is: a consumer with no
        /// config wired must resolve the SHIPPED look, and <c>GameConfig.asset</c> lags the code.</summary>
        public const bool DefaultBoatLegacyCabinGlow = false;

        /// <summary>The carried automatic lantern's explicit, default-off way back.</summary>
        public const bool DefaultWalkerAutoLantern = false;
        [Tooltip("Restore the walker's automatic lantern while on foot. The headlamp still answers L.")]
        public bool WalkerAutoLantern = DefaultWalkerAutoLantern;

        /// <summary>How long L is held, seconds, before the worn headlamp goes back into his pocket.
        /// A shorter press switches the beam.</summary>
        public const float DefaultWalkerHeadlampHoldSeconds = 0.4f;
        [Tooltip("Hold L this long (seconds) to take the headlamp off into the pocket. A tap switches the beam; " +
                 "a press with the lamp in the pocket puts it on, lit.")]
        [Min(0.05f)] public float WalkerHeadlampHoldSeconds = DefaultWalkerHeadlampHoldSeconds;

        /// <summary>Where the headlamp's lens sits on his head, metres from the rig's head mid point
        /// (<c>CharacterSkinDef.HeadMid</c>): forward out of the face, then up. The light's glow and
        /// cone both start here, and the drawn lens is pinned to it.</summary>
        public const float DefaultWalkerHeadlampLensForwardMetres = 0.01f;
        public const float DefaultWalkerHeadlampLensUpMetres = 0.10f;
        [Tooltip("Headlamp lens, metres FORWARD of the head mid point (out of the face).")]
        public float WalkerHeadlampLensForwardMetres = DefaultWalkerHeadlampLensForwardMetres;
        [Tooltip("Headlamp lens, metres UP from the head mid point (the forehead, above the eyes).")]
        public float WalkerHeadlampLensUpMetres = DefaultWalkerHeadlampLensUpMetres;
        /// <summary>How far behind the lens the strap crosses the back of his head, metres. Seen from
        /// behind, the strap is drawn there (the lens itself is hidden by his head).</summary>
        public const float DefaultWalkerHeadlampStrapBackMetres = 0.18f;
        [Tooltip("Headlamp strap, metres BEHIND the lens where it crosses the back of the head (drawn when he faces away).")]
        [Min(0f)] public float WalkerHeadlampStrapBackMetres = DefaultWalkerHeadlampStrapBackMetres;

        /// <summary>Gear every game has from the very start (owner ruling 2026-11-22: the headlamp,
        /// "From the start"). Owned by DERIVATION, like a watch that starts owned: never written into
        /// <c>SaveData.OwnedGear</c>, so a new game and a continued old save both hold it without a
        /// save write or a format change.</summary>
        public static readonly string[] DefaultCarriedFromStartGearIds = { "gear.headlamp" };
        [Tooltip("Gear ids the player owns from the start of every game without them being written to the " +
                 "save (the headlamp in his pocket). Ownership checks and the Notebook's KIT page read this.")]
        public string[] CarriedFromStartGearIds = (string[])DefaultCarriedFromStartGearIds.Clone();

        /// <summary>The carried-from-the-start ids of <paramref name="config"/>, or the defaults when
        /// no config is wired (EditMode, pre-boot).</summary>
        public static System.Collections.Generic.IReadOnlyList<string> CarriedFromStart(GameConfig config)
            => config != null ? (config.CarriedFromStartGearIds ?? System.Array.Empty<string>())
                              : DefaultCarriedFromStartGearIds;

        /// <summary>True when <paramref name="gearId"/> is carried from the start under
        /// <paramref name="config"/> (null config: the defaults).</summary>
        public static bool CarriesFromStart(GameConfig config, string gearId)
        {
            if (string.IsNullOrEmpty(gearId)) return false;
            System.Collections.Generic.IReadOnlyList<string> ids = CarriedFromStart(config);
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] == gearId) return true;
            return false;
        }

        [Tooltip("Restore YESTERDAY'S boat glows for an A/B (owner's ruling, 2026-09-03)? OFF is the " +
                 "shipped look: a lit wheelhouse is drawn as its own WINDOWS, with a wash of spill " +
                 "leaving each glazed wall, and the navigation lamps are blooms the size of their own " +
                 "fittings. ON restores the old picture — the 1.5 m amber disc laid over the deck " +
                 "around the house, and the masthead, stern and anchor lamps at their old pool radii " +
                 "— and draws no windows at all. It is the honest arm of the A/B and the way back if " +
                 "the new look is refused. It changes no navigation MEANING: which lamps a hull may " +
                 "show, and in what colour, is the rule of the road and is not on this dial.")]
        public bool BoatLegacyCabinGlow = DefaultBoatLegacyCabinGlow;

        // -----------------------------------------------------------------------------------------
        //  The pixel grid (owner playtest 2026-08-23 — "the running fisher and the Otter go soft")
        // -----------------------------------------------------------------------------------------
        // The locked Pixel Perfect Camera runs GridSnapping.PixelSnapping, and that mode snaps
        // SPRITE RENDERERS to a world grid and nothing else — it never moves the camera, and it never
        // reaches a MeshRenderer. So the camera sampled the snapped world from an arbitrary sub-pixel
        // offset (an asset texel got 2 screen pixels one frame and 3 the next: the "soft while
        // moving" read), and a mesh vehicle was not snapped by anything at all. This puts both
        // RENDERED positions back on the grid; see Core.PixelGrid for the full derivation.
        //
        // ⚠️ A FLAT FIELD WITH A CODE-SIDE DEFAULT, for the same reason the silhouette block below
        // is three flat scalars: a field missing from GameConfig.asset's YAML comes back as the C#
        // type default, and for a bool that is FALSE — the feature silently off, reported as on.

        /// <summary>Ship default — <b>ON</b>. Integer-pixel movement on the play grid is the bible's
        /// own pixel discipline (§3.4, §9.2 "no sub-pixel shimmer"), not an option; OFF exists so the
        /// owner can A/B the exact build that shipped before this flag against the one that snaps.</summary>
        public const bool DefaultPixelGridSnap = true;

        [Header("Pixel grid (the sub-pixel softness fix — bible §3.4 pixel discipline)")]
        [Tooltip("Round the RENDERED position of the camera and of every mesh vehicle onto the " +
                 "current framing's whole-pixel grid? ON is the shipped discipline: a running " +
                 "fisher and a driven Otter keep even, stable texels instead of going soft while " +
                 "they move. OFF restores the pre-fix look exactly — the camera lands wherever its " +
                 "smoothing puts it and a mesh vehicle draws at a raw float position — so the two " +
                 "can be A/B'd on one flag. This NEVER touches a simulated body: the physics " +
                 "position stays the honest float, only the drawn transform is rounded (rule 5), so " +
                 "determinism and every saved value are unaffected.")]
        public bool PixelGridSnap = DefaultPixelGridSnap;

        // -----------------------------------------------------------------------------------------
        //  The silhouette through foliage (owner ruling, 2026-08-16)
        // -----------------------------------------------------------------------------------------
        // "Slight occlusion — you always know where you are, you never lose your character." Dense
        // woods draw in FRONT of the fisher, opaque and correctly sorted; she reads through them as a
        // tinted silhouette. These three are the look, and they are the owner's to tune.
        //
        // ⚠️ THREE SCALARS, NOT A SETTINGS STRUCT, and that is deliberate. GameConfig.asset lags the
        // code: a field the shipped YAML has never heard of keeps its initializer here, but a field
        // MISSING FROM INSIDE a serialized struct comes back as the C# default — zero — because the
        // struct as a whole is overwritten by what the asset does carry. Packed into a struct, an
        // un-re-serialized asset would therefore ship strength 0 and a black tint: the feature silently
        // off, reported as on. Flat fields with code-side Default consts cannot fail that way, and
        // GameConfigSilhouetteDefaultTests pins it against the REAL shipped asset.

        /// <summary>Ship default — <b>ON</b>. The whole point of the arc is that the woods can be made
        /// deep without losing the fisher in them, so the silhouette is not an option the density pass
        /// depends on someone remembering to switch on.</summary>
        public const bool DefaultFoliageSilhouette = true;

        /// <summary>Ship default strength — how far a covered foliage pixel moves toward the tint.
        /// 0.55 reads as "clearly her, clearly behind something": low enough that the canopy still
        /// looks like canopy (the leaves keep better than half their own colour), high enough to find
        /// her at a glance against dark spruce, which is the darkest thing she can stand behind on
        /// this coast.</summary>
        public const float DefaultFoliageSilhouetteStrength = 0.55f;

        /// <summary>Ship default tint — a pale warm bone. NOT white: a pure white silhouette reads as a
        /// UI cutout pasted over the world, and this coast's palette has no pure white in it. Warm
        /// because every light in this game is warm (sun, lamp, window) and a cool silhouette would
        /// read as a ghost rather than as a person with the light behind her.</summary>
        public static readonly Color DefaultFoliageSilhouetteTint = new Color(0.94f, 0.90f, 0.82f, 1f);

        /// <summary>
        /// Ship default for <see cref="CharacterBlink"/> — <b>ON</b> (character PR 2a). A rig 9
        /// figure blinks on its own clock, as the rig's <c>BLINK</c> says: half, shut, half, then
        /// its clip's own eyes again, every 2.4–6 s, and 15% of blinks followed by a second. Each
        /// figure's clock is seeded from its own stable identity, so a crowd never blinks together
        /// and the same figure blinks the same on every run. Nothing the simulation reads comes
        /// from that seed. OFF: the eyes are the clip's, frame by frame.
        /// </summary>
        public const bool DefaultCharacterBlink = true;

        /// <summary>Ship default for <see cref="CharacterHeadLook"/> — <b>ON</b> (character PR 2a):
        /// a figure turns its neck and head toward what it looks at, as the rig's <c>LOOK</c>
        /// says (yaw −60..60°, pitch −15..20°, split 0.4 neck / 0.6 head).</summary>
        public const bool DefaultCharacterHeadLook = true;

        /// <summary>Ship default for <see cref="CharacterEyeLook"/> — <b>ON</b> (character PR 2a):
        /// past 8° of turn the head leaves undone, the open eyes look left or right
        /// (<c>eyes.left</c> / <c>eyes.right</c>).</summary>
        public const bool DefaultCharacterEyeLook = true;

        /// <summary>Ship default for <see cref="CharacterLookRadiusMetres"/>: 5 m on the figure's
        /// own ground, in the rig's metres.</summary>
        public const float DefaultCharacterLookRadiusMetres = 5f;

        /// <summary>Ship default for <see cref="CharacterLookTargetHeightMetres"/>: the player's
        /// head point at rest — rig 10's fisher, <c>head × headMid</c> on idle's first frame, is
        /// 1.6332 m above the ground in the rig's metres. Retuned from rig 9's 1.31 (1.3135 m) with
        /// the switch to rig 10 (the rig 10 intake, Phase B; the owner's ruling of 10-02).</summary>
        public const float DefaultCharacterLookTargetHeightMetres = 1.63f;

        /// <summary>
        /// Ship default for <see cref="MeshFigureKeyline"/> — <b>ON</b> (character PR 2a): a rig 9
        /// figure is outlined as the rig outlines it. The ring of pixels just outside her takes
        /// the rig's keyline ink, <c>#101a19</c>, with 22% of her nearest edge colour mixed in,
        /// and inside her a pixel across a depth break of more than 0.12 m drops one tone. OFF:
        /// the figure draws with the hull's rules, as before this PR.
        /// </summary>
        public const bool DefaultMeshFigureKeyline = true;

        [Header("Figure life (rig 9: blink, look, the figure's own ink)")]
        [Tooltip("Let rig 9 figures blink? Each figure keeps its own clock, seeded from who it is, " +
                 "so a crowd never blinks together. OFF: the eyes are exactly the clip's. Read live.")]
        public bool CharacterBlink = DefaultCharacterBlink;

        [Tooltip("Let a figure turn its neck and head toward what it looks at (a moored skipper or " +
                 "a villager toward the player, within the radius below)? The player herself looks " +
                 "at nothing. OFF: the head is exactly the clip's. Read live.")]
        public bool CharacterHeadLook = DefaultCharacterHeadLook;

        [Tooltip("Let the eyes finish a look the head leaves undone (eyes.left / eyes.right past 8 " +
                 "degrees)? OFF: the eyes stay the clip's. Read live.")]
        public bool CharacterEyeLook = DefaultCharacterEyeLook;

        [Tooltip("How near, in the figure's own metres on its own ground, the player has to be before " +
                 "a figure looks at her. Read live.")]
        [Min(0f)]
        public float CharacterLookRadiusMetres = DefaultCharacterLookRadiusMetres;

        [Tooltip("How high above the player's feet a figure aims its look, in the figure's metres. " +
                 "1.63 is the player's head at rest.")]
        [Min(0f)]
        public float CharacterLookTargetHeightMetres = DefaultCharacterLookTargetHeightMetres;

        [Tooltip("Outline rig 9 figures as the rig does: the ring just outside the figure takes the " +
                 "keyline ink with a little of the figure's nearest colour mixed in, and a pixel " +
                 "across a depth break inside the figure drops one tone. The ring goes on EMPTY " +
                 "pixels only (sea, sky, ground the facet pass does not draw), never over a deck or " +
                 "hull pixel, so no boat pixel moves. OFF draws the figure with the hull's rules, " +
                 "exactly as before. Read live.")]
        public bool MeshFigureKeyline = DefaultMeshFigureKeyline;

        /// <summary>
        /// Ship default for <see cref="FigurePixelTarget"/> — <b>ON</b>, by the owner's ruling of
        /// 10-18 (D5: "pixel switch ships on"): the figure pixel look, route 1 of
        /// <c>docs/design/figure-pixel-target.md</c>. OFF is the picture before the look, byte for
        /// byte: no figure changes queue, no pass is recorded and no overlay reads the figure target.
        /// </summary>
        public const bool DefaultFigurePixelTarget = true;

        /// <summary>
        /// Ship default for <see cref="FigureHeadingSteps"/> — <b>0, smooth</b>, by the owner's
        /// ruling of 10-15 (D2: "heading steps built at 0 (smooth)"). 0 turns a figure through
        /// every angle, exactly as before; N snaps her on-screen facing to N equal compass steps.
        /// Read only while <see cref="FigurePixelTarget"/> is ON.
        /// </summary>
        public const int DefaultFigureHeadingSteps = 0;

        [Header("Figure pixel look (route 1: the figures at the art's own pixel size)")]
        [Tooltip("Draw the mesh figures at the art's own pixel size: one texel per art pixel " +
                 "(1/32 m), enlarged with hard edges at the whole zoom steps (x2 and up), so the " +
                 "player and the cast read like the drawn art and still turn as meshes. Boats are " +
                 "unchanged. Where a nearer hull or wheelhouse covers a figure, that edge is drawn " +
                 "at the same art-pixel size. OFF is exactly the picture before the look. Read live.")]
        public bool FigurePixelTarget = DefaultFigurePixelTarget;

        [Tooltip("With the pixel look ON: how many facings a figure turns through. 0 is smooth " +
                 "(every angle, the shipped value); 8, 16 or 32 snap her facing on screen to that " +
                 "many equal compass steps, the way a drawn figure turns. Boats keep turning " +
                 "smoothly. Read live.")]
        [Min(0)]
        public int FigureHeadingSteps = DefaultFigureHeadingSteps;

        /// <summary>
        /// Ship default — <b>ON</b>, by the owner's 2026-09-12 ruling ("ship it on"), taken with
        /// the look debt below written down and accepted: he wants her in play before the shader
        /// pass lands. ON draws her as ONE skinned mesh while she is ABOARD a mesh hull; ashore she
        /// needs <see cref="MeshCharacterAshore"/> as well. OFF is still exactly the sprite, down
        /// to the byte — this stayed a switch, not a fork.
        ///
        /// <para>The debt the ruling accepted was rig 7's: a mesh measured 43–57% off the inked art,
        /// with no face and no helm or oars clip. Rig 9 bakes the cast since the character intake's
        /// Phase B (2026-09-26), and character PR 2a plays it in full: the face with its blink and
        /// look, the helm and oars clips through <c>CharacterSkinStateMap</c>, the carry clips, and
        /// the rig's own ink (<see cref="MeshFigureKeyline"/>).</para>
        /// </summary>
        public const bool DefaultMeshCharacter = true;

        [Header("Mesh characters (ADR 0044 d — the skinned player, behind a switch)")]
        [Tooltip("Draw the player as ONE SKINNED MESH through the iso facet pass while she is ABOARD, " +
                 "instead of as a sprite? ON is the shipped look and the default (owner ruling " +
                 "2026-09-12). She is rig 9's figure: her face blinks, she plays the rig's helm, " +
                 "oars and carry clips, and she is inked as the rig inks her. ASHORE this switch " +
                 "alone does nothing: she draws as a mesh on land only while Mesh Character Ashore " +
                 "is ON as well, which the shipped config turns ON. Turn this OFF and the sprite is " +
                 "back exactly, down to the byte, aboard and ashore.")]
        public bool MeshCharacter = DefaultMeshCharacter;

        /// <summary>
        /// Code default for <see cref="MeshCharacterAshore"/>: <b>OFF</b>, by the owner's ruling
        /// of 2026-09-19 ("ashore go", decision 1). The shipped <c>GameConfig.asset</c> turns it
        /// <b>ON</b>, by his ruling of the same day ("switch on"), taken on the plate S1 (OFF
        /// against ON, St Peters at noon beside the machine row). A config built fresh in code
        /// still starts OFF.
        ///
        /// <para>ON, and only while <see cref="MeshCharacter"/> is ON too, draws her ASHORE as the
        /// same skinned mesh, through the facet pass under a figure id of her own (ADR 0044
        /// amendment 2026-09-17; the figure's ashore frame is #861's). She is the mesh ashore only
        /// while she stands or walks on her own feet, dry, with no clip playing. Any clip (the
        /// boarding vault, the ladders, the haul, the chop, lift, place and toss, the bench, sleep,
        /// swim and tread, the open machines' Drive) and wading hand the draw back to the sprite
        /// for their whole length. In a cab, and at a helm that hides its pilot, she draws
        /// neither, exactly as before. The held item stays at the sprite's hand.</para>
        ///
        /// <para>When the region has used every facet id she is refused one: she keeps her WHOLE
        /// sprite and the id registry logs its one warning for that ask. She asks again only at her
        /// next arrival ashore (a landing, a clip's end, a region's arrival), never per frame. Once
        /// granted, she keeps her id for as long as the switch stays on.</para>
        ///
        /// <para>OFF: her sprite ashore, byte for byte as before this switch existed — no ashore
        /// figure, no id taken, the sprite untouched.</para>
        /// </summary>
        public const bool DefaultMeshCharacterAshore = false;

        [Tooltip("Draw the player as her skinned mesh ASHORE as well? Needs Mesh Character ON too. " +
                 "ON in the shipped config (owner ruling 2026-09-19, taken on a plate), OFF in a " +
                 "fresh one. Ashore she is the mesh only " +
                 "while she stands or walks dry on her own feet: any clip (the boarding vault, the " +
                 "ladders, the haul, chop, lift, bench, sleep, swim, the open machines) and wading " +
                 "hand her back to the sprite for their length, and the held item stays at the " +
                 "sprite's hand. When the region has run out of facet ids she keeps her whole " +
                 "sprite and asks again at her next landing, never every frame. Read live. Turn it " +
                 "OFF and the sprite is back exactly, down to the byte.")]
        public bool MeshCharacterAshore = DefaultMeshCharacterAshore;

        /// <summary>
        /// Shipped default for <see cref="MeshCast"/>. <b>ON — the owner's ruling of 2026-09-17,
        /// "yes make everyone a mesh now"</b> (ADR 0044, amendment 2026-09-17): the cast follows the
        /// player onto skinned meshes, with the sprite as the fallback for anything that cannot draw.
        ///
        /// <para>What ON reaches: a moored boat's skipper standing on a FACET hull, and, while
        /// <see cref="MeshCastAshore"/> is ON as well, every villager on her own feet ashore, under a
        /// facet id of her own (ADR 0044, amendment 2026-09-27). With Mesh Cast Ashore OFF the
        /// villagers draw their sprites exactly as before. A character whose art def names no
        /// <see cref="CharacterSkinDef"/>, or whose def lists the state it is in nowhere in
        /// <see cref="CharacterSkinDef.MeshStates"/>, keeps its sprite whatever this says.</para>
        /// </summary>
        public const bool DefaultMeshCast = true;

        [Tooltip("Draw the CAST (everyone but the player) as skinned meshes through the iso facet " +
                 "pass where they can be drawn that way? ON is the shipped look (owner ruling " +
                 "2026-09-17). That is a moored boat's skipper standing on a mesh hull and, while " +
                 "Mesh Cast Ashore is ON too, every villager on her own feet ashore. A rig 9 figure " +
                 "plays the rig's face, blink and look, and is inked as the rig inks (the figure " +
                 "life switches above). Read live — flip it with the game running. Turn it OFF and " +
                 "every cast sprite is back exactly.")]
        public bool MeshCast = DefaultMeshCast;

        /// <summary>
        /// Code default for <see cref="MeshCastAshore"/>: <b>OFF</b>. The shipped
        /// <c>GameConfig.asset</c> turns it <b>ON</b>, by the owner's ruling of 2026-09-27
        /// (decision 1: ON once the plates are accepted, OFF one switch away). A config built fresh
        /// in code still starts OFF.
        ///
        /// <para>ON, and only while <see cref="MeshCast"/> is ON too, draws every VILLAGER on her own
        /// feet ashore as her skinned mesh, through the facet pass under a facet id of her own (ADR
        /// 0044, amendment 2026-09-27; the ashore frame is #861's, as the player's). She faces where
        /// her sprite faces, sorts exactly as her sprite, and her idle is moved off her neighbours' by
        /// her NpcDef id. Sheltered indoors she is hidden in both pictures and keeps her id. A clip or
        /// work animation that suspends her sprite, or a state her skin does not mesh, hands the draw
        /// back to her sprite.</para>
        ///
        /// <para>When the region has used every facet id she is refused one: she keeps her WHOLE
        /// sprite and the id registry logs its one warning for that ask. She asks again only when a
        /// switch is turned off and on again or she is enabled again, never per frame. Once granted,
        /// she keeps her id while both switches stay on.</para>
        ///
        /// <para>OFF: every villager's sprite, byte for byte as before this switch existed — no ashore
        /// figure built, no id taken, the sprite untouched.</para>
        /// </summary>
        public const bool DefaultMeshCastAshore = false;

        [Tooltip("Draw the VILLAGERS as their skinned meshes ashore? Needs Mesh Cast ON too. ON in " +
                 "the shipped config (owner ruling 2026-09-27, on the plates), OFF in a fresh one. " +
                 "A villager is the mesh while she is out on her own feet: indoors she is hidden in " +
                 "both pictures, and a clip or a state her skin does not mesh hands her back to the " +
                 "sprite. When the region has run out of facet ids she keeps her whole sprite and " +
                 "does not ask again until a switch is turned off and on. Read live. Turn it OFF and " +
                 "every villager's sprite is back exactly, down to the byte.")]
        public bool MeshCastAshore = DefaultMeshCastAshore;

        [Header("Foliage silhouette (the fisher read through dense woods)")]
        [Tooltip("Let the player read through foliage that draws in front of her? ON is the shipped " +
                 "look: trees and shrubs stay opaque and correctly sorted, and her shape shows through " +
                 "them in the tint below. OFF restores plain occlusion, where deep woods can hide her " +
                 "completely. This costs one extra draw call for the whole screen however dense the " +
                 "forest is, so it is a look switch and not a performance one.")]
        public bool FoliageSilhouette = DefaultFoliageSilhouette;

        [Tooltip("The colour a foliage pixel moves toward where the player is behind it. A pale warm " +
                 "bone by default. Pure white reads as a UI cutout pasted over the world.")]
        public Color FoliageSilhouetteTint = DefaultFoliageSilhouetteTint;

        [Range(0f, 1f)]
        [Tooltip("How far toward the tint a covered foliage pixel moves. 0 is off (and genuinely free " +
                 "— the foliage skips the lookup entirely), 1 is a flat opaque cutout of her shape " +
                 "with no leaf detail left in it. 0.55 is the shipped read.")]
        public float FoliageSilhouetteStrength = DefaultFoliageSilhouetteStrength;

        [Header("Foliage snow (tree snow by the calendar, recomputed and never saved)")]
        [Tooltip("When the snow comes to the trees and when it leaves, in days of the calendar. The trees " +
                 "wear the cover for the day the clock is on; nothing about it is saved, so a load shows " +
                 "that day's snow. Ground, roof and shrub snow are later work and are not driven by this yet.")]
        public FoliageSnowSettings FoliageSnow = FoliageSnowSettings.Default;

        [Header("Depth drop (Rod Fishing v2 — the weighted rig's fall + the slack bottom tell)")]
        [Tooltip("The depth-fishing game's tunables (drop a weighted rig, count the fall, feel the floor): " +
                 "how fast a rig sinks per kilogram (heavier = faster — the whole 'count the fall' read), " +
                 "how much line the reel carries, the just-off-the-floor sweet window, the fishing depth " +
                 "zones in metres, and how strongly the held depth weights the catch toward the species " +
                 "that live there. Dial these to make depth feel readable; they never make a fish " +
                 "impossible — depth is a WEIGHT on the catch roll, not a wall.")]
        public DepthDropSettings DepthDrop = DepthDropSettings.Default;

        [Header("Bait & tackle (what's on the hook — a WEIGHT on the catch, never a wall)")]
        [Tooltip("How much the right bait and the right tackle matter. Bait is the PRECISE tool (a fish " +
                 "that wants food refuses the wrong food); tackle is the BROAD one (a curious fish will " +
                 "still hit the wrong lure). Turn the boosts to 1 and the damps to 1 to make what's on " +
                 "the hook irrelevant again.")]
        public BaitTackleSettings BaitTackle = BaitTackleSettings.Default;

        [Header("Jigging (working the lure — the five hand-feels)")]
        [Tooltip("What each kind of tackle wants you to DO with it, and how much it matters. Bait fishes " +
                 "itself; a lure only fishes if you work it, and each one wants its own tempo and stroke " +
                 "size. Raise Tolerance01 to make the actions easier to find by feel; raise " +
                 "DeadLureFraction01 to be kinder to a player who leaves the rod still.")]
        public JiggingSettings Jigging = JiggingSettings.Default;

        [Header("The sea in the fight (P1 — rough water fishes better and fights harder)")]
        [Tooltip("How much the WEATHER matters to fishing. Rough water is a TRADE: the fish are bolder so " +
                 "bites come quicker and run bigger, but the swell works your line so every fish is " +
                 "harder to hold — and in a real blow a good fish can genuinely beat you. Set every " +
                 "factor to 0 to go back to weather-blind fishing, where a gale plays exactly like a " +
                 "flat calm.")]
        public SeaFishingSettings SeaFishing = SeaFishingSettings.Default;

        [Header("How big she feels (the rod, the line, the camera and the strain voice by fish size)")]
        [Tooltip("How the SIZE of the hooked fish — her weight within her own species' range — reaches the " +
                 "player: how deep the rod bows, how the tip pulses to her head-shakes and swings to her " +
                 "runs, how tight and humming the line is, how hard the hook-set lands, and which fish " +
                 "count as 'a big one'. Pure feedback: nothing here changes how hard a fish is to land.")]
        public FishFeelSettings FishFeel = FishFeelSettings.Default;

        [Header("Fish schools (ADR 0025 S3 — WHERE the fish are, the thing the finder draws)")]
        [Tooltip("The deterministic school sim: how often a patch of water holds fish, how long they " +
                 "hang about, how big the patch is, how deep they sit, how many show — and how much all " +
                 "of that speeds the bite and steers WHICH fish takes. One model: the marks on the fish " +
                 "finder's glass ARE these schools, and these schools are what changes the fishing. " +
                 "Recomputed from (worldSeed, gameTime, place, weather, season) like the tide — never " +
                 "saved, so tuning these moves the whole sea at once with no save surgery.")]
        public FishSchoolSettings FishSchools = FishSchoolSettings.Default;

        [Header("Bar clam show (the squirt that gives a buried clam away)")]
        [Tooltip("Owner ask 2026-11-22: a clam's holes are small and sit in the sand; its squirt is the " +
                 "main giveaway. How often and how long a clam squirts, how tall the jet is drawn, and how " +
                 "it squirts when the player walks up. Cosmetic only, never saved. Read through " +
                 "ClamShowSettings.Current.")]
        public ClamShowSettings ClamShow = ClamShowSettings.Default;

        [Header("Freshness & rot (M1 §7.3 — the clock on every catch)")]
        [Tooltip("How fast each storage mode rots a landed catch, and how far gone a catch can be " +
                 "before no buyer will take it. The per-species base rate lives on each " +
                 "FishSpeciesDef.SpoilPerDay; these are the world-policy dials on top of it.")]
        public FreshnessSettings Freshness = FreshnessSettings.Default;

        [Header("Fuel (fuel-and-refuelling.md §9 — the shape of the burn, whole-fleet)")]
        [Tooltip("How thirst rises with throttle, with the catch aboard, and with the sea — the " +
                 "dimensionless curve every hull's burn is multiplied by. A particular boat's thirst is " +
                 "her own BoatHullDef.FullThrottleLitresPerHour; this is the shape they all share.\n\n" +
                 "⭐ If fuel feels too thirsty or too free ACROSS THE BOARD, move BurnScale — one " +
                 "number re-prices fuel for the whole game, instead of re-authoring 38 hull assets.")]
        public FuelSettings Fuel = FuelSettings.Default;

        [Header("Pots (trap-fishing — the starter kit)")]
        [Tooltip("Pots granted ONCE per game as the cozy starter kit (Economy's StartingPots, flag-" +
                 "guarded): a new game starts with these, and an existing save gets them on its first " +
                 "load after the pots-are-owned update — so nobody is ever stranded potless mid-loop. " +
                 "Each entry names an authored TrapDef by stable id with a count. Owner-tunable; every " +
                 "FURTHER pot is bought at the shipwright (the P2 money wheel).")]
        public PotStarterEntry[] StarterPotKit =
        {
            new PotStarterEntry("trap.lobster", 2),
            new PotStarterEntry("trap.crab", 1),
        };

        [Header("Player zoom (the wheel is the player's eye — owner ruling 2026-08-19)")]
        [Tooltip("How far the mouse wheel may take the WALKING view in and out, in metres of world " +
                 "height. Closest is the interior close-up (you are reading a room); farthest is the " +
                 "outdoor walk-and-look. Both quantise to the camera's integer pixel-perfect steps, so " +
                 "typing any number picks the nearest crisp stop rather than a blurry one — and the " +
                 "wheel only ever moves the ON-FOOT view: the helm keeps the hull's ruled framing, the " +
                 "deck keeps its own. Untick WheelEnabled to take the wheel out of the game entirely.")]
        public PlayerZoomSettings PlayerZoom = PlayerZoomSettings.Default;

        [Header("Boat interiors (ADR 0038 — the cabin that rides)")]
        [Tooltip("How much of her own rock a boat's INTERIOR draw takes — the comfort clamp ruled by " +
                 "ADR 0038 proposal 1. 1 = full fidelity (exactly the hull's own roll/pitch/heave, which " +
                 "is what the kit bakes); 0 = dead flat, the ACCESSIBILITY setting; 0.45 is the ruled " +
                 "default. A cabin fills the frame in a way a deck does not, so the same rock that reads " +
                 "as life outdoors reads as nausea indoors.\n\n" +
                 "⚠ It is a comfort filter on ONE DRAW. It must never feed back into the hull's pose, " +
                 "her handling, or anything saved (rule 5) — the boat moves exactly as she did at every " +
                 "value, and only the picture of her inside is calmed. That is safe only because the " +
                 "interior and the exterior are never co-visible: entering is a LAYER SWAP (ADR 0038 " +
                 "proposal 3), so the two poses are never on screen together to disagree.")]
        [Range(0f, 1f)] public float InteriorRockScale = DefaultInteriorRockScale;

        /// <summary>The ruled default for <see cref="CutTheHelmRoomAtTheWheel"/> (2026-11-14), read by
        /// the boat cutaway when no config is wired.</summary>
        public const bool DefaultCutTheHelmRoomAtTheWheel = true;

        [Header("Boat cutaway at the wheel (owner ruling 2026-11-14)")]
        [Tooltip("ON (the default): while you steer a boat whose wheel stands inside an enclosed room, " +
                 "that one room is cut open so you see the skipper and the instruments. No other level " +
                 "opens, and a wheel on an open deck or a flybridge cuts nothing. OFF: the house stays " +
                 "closed at every wheel, as it was before 2026-11-14. Walking inside a cabin cuts it " +
                 "open either way. Picture only: nothing about handling or the save changes.")]
        public bool CutTheHelmRoomAtTheWheel = DefaultCutTheHelmRoomAtTheWheel;

        [Header("Interior stairs (ADR 0036 — the climb between storeys)")]
        [Tooltip("How long the player takes to walk up (or down) one storey, in seconds. The storey " +
                 "above is drawn at its true height, so the two floors are a real distance apart and " +
                 "the fisher walks that distance rather than blinking across it.\n\n" +
                 "Feel, not physics: the climb covers no GROUND, so this is not a speed and no honest " +
                 "stride can be derived from it. Short reads as a step up and long reads as a cutscene; " +
                 "0.5 s is the ruled default. Set it to 0 for an instant swap — the pre-2026-08-23 " +
                 "behaviour, and a legitimate accessibility answer for anyone who does not want the " +
                 "camera moved for them.")]
        [Min(0f)] public float StairClimbSeconds = DefaultStairClimbSeconds;

        [Header("Juice (the three moments — juice charter 2026-09-09)")]
        [Tooltip("The juice lane's tunables: the post-processing grade's blend curve (PR 1), and later " +
                 "the camera feel and the moment timings. Every field must exist in GameConfig.asset.")]
        public JuiceSettings Juice = JuiceSettings.Default;

        [Header("Dev: all instruments at the helm")]
        [Tooltip("In the editor and development builds, start each console at its best supported fit. " +
                 "K and Shift+K still cycle the instruments. Turn off to rehearse the real owned fit. " +
                 "Ignored in release builds; never buys instruments or changes the save.")]
        public bool DevAllHelmInstruments = true;

        // Convenience
        /// <summary>
        /// THE shared displacement exaggeration (ADR 0023 §(2)) — the accessor every water-riding
        /// consumer reads: the displaced surface's vertex lift today; phase 3's hull heave and every
        /// buoy/wake/oar anchor that turns wave metres into screen metres. Always read it from here
        /// (through <see cref="ShoreFadeMath.DisplacedHeight"/>), never cache a copy — the
        /// overlay-pose lesson made structural: a boat's heave must ride exactly the sea it is
        /// drawn on, including while the owner is tuning this value in Play.
        /// </summary>
        public float WaveExaggeration => DisplacedWater.WaveExaggeration;

        [Header("Fishing and clam digging body presentation")]
        [Tooltip("Use the baked mesh body for rod fishing and clam digging when the skin carries " +
                 "the clip and enables it in MeshStates. Off restores the previous sprite fishing " +
                 "and unanimated dig. Held tools and catch keep their sprite presenters.")]
        public bool MeshFishingAndDig = true;

        /// <summary>
        /// Code default for <see cref="PlayerLegacySpriteFallback"/>: ON, so a config built fresh in
        /// code (every PlayMode rig that tests the old hand-over) keeps the old behaviour. The shipped
        /// <c>GameConfig.asset</c> turns it OFF: in the game the player is always the mesh (ADR 0061),
        /// the same code-default/shipped split as <see cref="MeshCharacterAshore"/>.
        /// </summary>
        public const bool DefaultPlayerLegacySpriteFallback = true;

        [Header("The player is always the mesh (ADR 0061)")]
        [Tooltip("OFF (the shipped game): the player never switches to an old sprite sheet. A swim, a " +
                 "tread, a sleep, a drive, a reach or a carry the mesh did not list yet plays its baked " +
                 "mesh clip, and a state the skin has no clip for plays the nearest one from Player Mesh " +
                 "Fallbacks. ON: the old behaviour, where those moves, wading and swimming hand the picture " +
                 "back to the sprite sheet. Only the player reads this; the cast keeps its own switches.")]
        public bool PlayerLegacySpriteFallback = DefaultPlayerLegacySpriteFallback;

        [Tooltip("When the player's skin has no clip for a state, the state to try instead (From to To), " +
                 "followed row by row until the skin has one; idle ends every chain. Both are baked clip " +
                 "names. Read only while Player Legacy Sprite Fallback is OFF.")]
        public MeshStateFallback[] PlayerMeshFallbacks = PlayerMeshRule.DefaultFallbacks();

        [Tooltip("Swimming, the waterline on the mesh figure sits this far (m) above its pelvis. Rig 10's " +
                 "own swim pose puts the sea 0.09 m over the pelvis.")]
        [Min(0f)] public float FigureSwimWaterAbovePelvisMetres = 0.09f;

        [Tooltip("Treading water, the waterline sits this far (m) below the mean of the shoulders (rig 10: " +
                 "0.05 m). It is also the highest a wade may ever reach on the figure.")]
        [Min(0f)] public float FigureTreadWaterBelowShouldersMetres = 0.05f;

        [Tooltip("ON: the part of the player's mesh under the waterline still draws, tinted and dimmed " +
                 "toward the sea, as the old sprite's submerged body did. OFF: it is hidden (a hard cut).")]
        public bool FigureShowsUnderwater = true;

        [Tooltip("The sea colour the submerged part of the player's mesh tints toward.")]
        public Color FigureUnderwaterTint = new Color(0.16f, 0.34f, 0.44f, 1f);

        [Tooltip("How far the submerged part takes the sea colour (0: its own colours, 1: all sea).")]
        [Range(0f, 1f)] public float FigureUnderwaterTintAmount = 0.55f;

        [Tooltip("How much darker the submerged part draws (0: as lit, 1: black).")]
        [Range(0f, 1f)] public float FigureUnderwaterDim = 0.35f;

        [Tooltip("Height (m) of the pale foam band on the mesh just above the waterline. 0: a hard edge.")]
        [Min(0f)] public float FigureWaterlineFoamMetres = 0.03f;

        [Tooltip("How pale that foam band is right at the waterline (0: none, 1: white).")]
        [Range(0f, 1f)] public float FigureWaterlineFoam = 0.5f;

        [Tooltip("WADING, the height (m) of the pale foam band just above the waterline. He stands " +
                 "upright, so the band is seen edge-on: the figure draws at its def's pixels per metre, " +
                 "foreshortened by the view's elevation, and the swim band's few centimetres come to under " +
                 "one pixel there, so the line at the surface vanished. Keep it at two pixels or more.")]
        [Min(0f)] public float FigureWadeWaterlineFoamMetres = 0.1f;

        [Tooltip("WADING, how pale that foam band is right at the waterline (0: none, 1: white).")]
        [Range(0f, 1f)] public float FigureWadeWaterlineFoam = 0.8f;

        [Tooltip("ON: while the player swims or treads water his sun shadow is not drawn. The shadow is " +
                 "the hidden body sprite's, standing upright, and on open water it read as a ghost " +
                 "beside him. Wading keeps it.")]
        public bool FigureSwimHidesSunShadow = true;

        [Tooltip("ON: while the player swims or treads water the tools he carries (the rod) are stowed: " +
                 "neither the rod mesh on the figure nor the carried sprite is drawn, and they are drawn " +
                 "again the moment he finds his footing. The rod mesh has no waterline cut, so held on the " +
                 "swim pose it stood out of the water at full strength. Wading keeps them in his hands.")]
        public bool SwimStowsCarriedTools = true;

        /// <summary>Code default for <see cref="HeldPropLineWidthPx"/>.</summary>
        public const float DefaultHeldPropLineWidthPx = 1.5f;

        [Header("Held props on the mesh figure (ADR 0053)")]
        [Tooltip("While the mesh body plays the fishing performance, draw the rod as its baked facet mesh " +
                 "in his hand, aimed by the clip's own tool track, instead of the rod sprite overlay. Only " +
                 "for a tool whose ToolDef asks for it (DrawHeldMeshOnFigure). Off, or whenever the mesh " +
                 "cannot draw, the sprite rod is drawn exactly as before.")]
        public bool MeshRodInHand = true;

        [Tooltip("How wide (art pixels) a held prop's thin parts are drawn — the rod blank, until the facet " +
                 "shader's own line pass exists (ADR 0053 D2). 1 to 2 is the ruled look.")]
        [Range(0.5f, 3f)] public float HeldPropLineWidthPx = DefaultHeldPropLineWidthPx;
        [Tooltip("Draw a fish in his hand (carried, and held up at the landing) as the rig's facet mesh on " +
                 "his hand's socket, at its size rung, instead of the held fish sprite. Only for a species " +
                 "the held-prop bake makes a mesh for (CatchItemLibrary.HeldMeshFor); every other catch, " +
                 "and any frame the mesh figure is not drawing him, keeps its sprite.")]
        public bool MeshCatchInHand = true;

        [Tooltip("How wide (art pixels) the thin line along a held fish's fins and tail is drawn, so a fin " +
                 "that is a sliver at a pixel still reads (ADR 0053 D2). 0 = no fin line.")]
        [Range(0f, 3f)] public float HeldFishFinLineWidthPx = 1f;

        [Tooltip("How wide (art pixels) a held fish's eye is drawn. 0 = no eye.")]
        [Range(0f, 3f)] public float HeldFishEyeSizePx = 1f;

        [Tooltip("A held fish mesh's shade gain, as a multiple of its bake's (more contrast between the lit " +
                 "belly and the shaded back, as the held sheet draws it). 1 = the bake's own.")]
        [Range(0.5f, 3f)] public float HeldFishToneGain = 1.5f;

        [Tooltip("Added to a held fish mesh's shade, in ramp steps: a brighter fish. 0 = the bake's own.")]
        [Range(-2f, 2f)] public float HeldFishToneLift = 1f;

        [Tooltip("A held fish mesh's dither, as a multiple of its bake's: 1 = as baked, 0 = clean bands of " +
                 "the ramp's own colours (the held sheet's look at hand size).")]
        [Range(0f, 1f)] public float HeldFishDitherScale = 0f;

        [Tooltip("How far a held fish mesh is drawn toward the camera, in metres, so his body or legs never " +
                 "hide it (the held sheet is always drawn in front). 0 = where the hand is.")]
        [Min(0f)] public float HeldFishFrontBiasMeters = 0.4f;

        [Tooltip("The shortest a held fish MESH is drawn, art pixels along its longest side: a smaller fish " +
                 "is scaled up about its grip to this, so a herring still reads in his hand. The mesh can " +
                 "grow where a sprite can only pick a bigger rung (MinDrawnFishLengthPx). 0 = no floor. " +
                 "16 = the length a held sheet stops needing its keyline (HeldFishKeylineMaxLengthPx), the " +
                 "shortest an unoutlined mesh still reads; 20 drew a smelt two thirds the length of a cod.")]
        [Min(0f)] public float HeldFishMinLengthPx = 16f;

        [Tooltip("At the landing the fish hangs from whichever hand is nearer the camera, so it is never " +
                 "hidden behind him (the rig's own fish prop swaps hands the same way). It changes hands " +
                 "only when the other hand is nearer by more than this (m), so it does not flicker.")]
        [Min(0f)] public float HeldFishHandSwapDepthMeters = 0.05f;

        /// <summary>Code default for <see cref="HeldRodGripToleranceMetres"/>.</summary>
        public const float DefaultHeldRodGripToleranceMetres = 0.02f;

        [Tooltip("How far (m) the held rod's grip may sit from his hand's tool socket on the frame drawn, in " +
                 "every rod state and heading, before the grip test calls it out of his hand. The grip is " +
                 "put on the socket every frame, so this only absorbs float error; raise it only with a reason.")]
        [Min(0f)] public float HeldRodGripToleranceMetres = DefaultHeldRodGripToleranceMetres;

        [Tooltip("Draw a hand tool that is not the rod (the clam shovel) as its baked facet mesh in the mesh " +
                 "figure's hand: aimed by the clip's own tool track while he digs, at the tool's carried aim on " +
                 "a free gait. Only for a tool whose ToolDef asks for it (DrawHeldMeshOnFigure). Off, or " +
                 "whenever the mesh cannot draw, the tool's sprite is drawn exactly as before.")]
        public bool MeshHandToolInHand = true;

        [Tooltip("Draw a carried pail as its baked facet mesh hung by the bail from the mesh figure's carry " +
                 "socket, at the fill and catch the pail holds, so it sorts with his legs pixel by pixel. Only " +
                 "for a carry-pose row that names a mesh tier (CharacterCarryPoseDef.Row.MeshTier) and a pail " +
                 "that shows its fill as baked states. Off, or whenever the mesh cannot draw (the clam hod, the " +
                 "sprite body), the pail sprite is drawn exactly as before.")]
        public bool MeshPailInHand = true;

        /// <summary>Code default for <see cref="HeldShovelShaftHandToleranceMetres"/>.</summary>
        public const float DefaultHeldShovelShaftHandToleranceMetres = 0.06f;

        [Tooltip("QA: how far (m) the dig clip's LEFT hand socket may sit off the drawn shovel's shaft on any " +
                 "frame before the shovel test calls the shaft out of his hand. The right hand holds the grip " +
                 "exactly; the left slides on the shaft as the rig poses it. Read by the EditMode test only.")]
        [Min(0f)] public float HeldShovelShaftHandToleranceMetres = DefaultHeldShovelShaftHandToleranceMetres;

        /// <summary>Code default for <see cref="HeldShovelGroundToleranceMetres"/>.</summary>
        public const float DefaultHeldShovelGroundToleranceMetres = 0.1f;

        [Tooltip("QA: how far (m) the drawn shovel's blade may sit off the ground at the dig's low point (the " +
                 "frame the blade is lowest), above or below, and how far below the ground the blade of a " +
                 "CARRIED shovel may reach on any idle, walk or run frame. Read by the EditMode test only.")]
        [Min(0f)] public float HeldShovelGroundToleranceMetres = DefaultHeldShovelGroundToleranceMetres;

        [Header("Fish at the stove and in the freezer")]
        [Tooltip("The cooked colour ramp: the fish on a browning station is tinted along these stops as its job " +
                 "runs, first stop at the start, last when it is done. One ramp for every species (no sheet " +
                 "per species). Empty = no browning.")]
        public Color[] CookedFishRamp =
        {
            new Color(1f, 1f, 1f, 1f), new Color(1f, 0.86f, 0.64f, 1f), new Color(0.84f, 0.6f, 0.34f, 1f),
        };

        [Tooltip("A fillet on a station (a recipe whose input is a fillet, not a whole fish) is drawn as a fillet " +
                 "shape this long against the whole fish as drawn (its size rung's measured held length), never as the whole fish.")]
        [Range(0.2f, 1f)] public float FilletLengthOfFish = 0.6f;

        [Tooltip("A drawn fillet's height against its own length.")]
        [Range(0.2f, 0.8f)] public float FilletHeightOfLength = 0.4f;

        [Tooltip("A raw fillet's flesh. The cooked ramp tints it as it browns.")]
        public Color FilletFleshColor = new Color(0.98f, 0.9f, 0.86f, 1f);

        [Tooltip("A drawn fillet's 1 px outline.")]
        public Color FilletEdgeColor = new Color(0.62f, 0.5f, 0.46f, 1f);
        [Tooltip("The tint of a frozen fish lying in the freezer (a frost look).")]
        public Color FrozenFishTint = new Color(0.8f, 0.9f, 1f, 1f);

        [Tooltip("The freezer's contents are drawn only while the player stands this close (m), as if the lid " +
                 "were open: no draw calls for a freezer nobody is at.")]
        [Min(0f)] public float FreezerFishShowRangeMeters = 1.6f;

        [Tooltip("The freezer's fish slots: columns across the chest (a fish lies across, so few and wide).")]
        [Min(1)] public int FreezerFishColumns = 2;

        [Tooltip("The freezer's fish slots: rows from the back to the front.")]
        [Min(1)] public int FreezerFishRows = 4;

        [Tooltip("The back-left slot, metres from the freezer's pivot (x right, y up on screen).")]
        public Vector2 FreezerFishOriginMeters = new Vector2(-0.26f, 0.3f);

        [Tooltip("Metres from one slot to the next: x across a row, y down to the next row (towards the front).")]
        public Vector2 FreezerFishPitchMeters = new Vector2(0.52f, 0.2f);

        /// <summary>Shipped crossing hysteresis (m), also the fallback for a rig with no GameConfig.</summary>
        public const float DefaultRegionCrossingHysteresisMetres = 0.25f;

        [Header("Region crossings (ADR 0057: the whole wall is a door)")]
        [Tooltip("How far (m) past a region's edge the player or boat must go before the crossing into the " +
                 "neighbour fires, and so also how far they must " +
                 "come back to cross again: it stops a mover on the seam bouncing between scenes without " +
                 "ever moving them. Small: it is a stretch of the old scene's empty margin you travel " +
                 "through before the new scene appears.")]
        [Min(0f)] public float RegionCrossingHysteresisMetres = DefaultRegionCrossingHysteresisMetres;

        /// <summary>Shipped preload distance (m), also the fallback for a rig with no GameConfig.</summary>
        public const float DefaultRegionPreloadDistanceMetres = 40f;

        [Tooltip("How near (m) a shared edge the player or boat must come before the neighbouring region's " +
                 "scene is loaded in the background and parked out of sight, so the crossing only has to " +
                 "switch it on instead of loading it. Bigger = the load's one heavy frame lands further from " +
                 "the seam, and more neighbours are kept loaded. 0 = no preload (the scene loads when the " +
                 "crossing fires, as before).")]
        [Min(0f)] public float RegionPreloadDistanceMetres = DefaultRegionPreloadDistanceMetres;

        /// <summary>Shipped preload heading gate, release delay and teleport cap, also the fallbacks for a rig
        /// with no GameConfig (PR 1223, the warm-neighbour fix).</summary>
        public const float DefaultRegionPreloadMinHeadingSpeed = 0.5f;
        public const float DefaultRegionPreloadHeadingSeconds = 0.25f;
        public const float DefaultRegionPreloadReleaseSeconds = 15f;
        public const float DefaultRegionMoverMaxPlausibleSpeed = 40f;

        [Tooltip("The slowest (m/s) the player or boat must be heading for a shared edge before its neighbour is " +
                 "loaded in the background. Above the settling jitter of a mover standing still, so only a real " +
                 "approach warms a neighbour.")]
        [Min(0f)] public float RegionPreloadMinHeadingSpeed = DefaultRegionPreloadMinHeadingSpeed;
        [Tooltip("How long (s) the mover must keep heading for the same neighbour before it is loaded, so one odd " +
                 "physics step never loads a whole region.")]
        [Min(0f)] public float RegionPreloadHeadingSeconds = DefaultRegionPreloadHeadingSeconds;
        [Tooltip("How long (s) after the approach ends (the mover turned away, stopped or left the preload " +
                 "distance) a neighbour loaded ahead of a crossing that never came is unloaded again. A region " +
                 "the player has been in is never unloaded by this.")]
        [Min(0f)] public float RegionPreloadReleaseSeconds = DefaultRegionPreloadReleaseSeconds;
        [Tooltip("The fastest (m/s) the player or boat can really move. A step faster than this is a teleport " +
                 "(a test staging, a placement on arrival), not speed, and counts as standing still for the seam " +
                 "fade and the preload.")]
        [Min(0f)] public float RegionMoverMaxPlausibleSpeed = DefaultRegionMoverMaxPlausibleSpeed;

        /// <summary>Shipped seam-fade timings (s) and distances (m), also the fallbacks for a rig with no
        /// GameConfig (owner ruling on PR 1223: "a momentary fade out and screen change").</summary>
        public const float DefaultRegionSeamFadeOutSeconds = 0.15f;
        public const float DefaultRegionSeamFadeInSeconds = 0.2f;
        public const float DefaultRegionSeamMaxBlackSeconds = 1.5f;
        public const float DefaultRegionSeamFootFadeDistanceMetres = 0.45f;
        public const float DefaultRegionSeamBoatFadeDistanceMetres = 1.5f;
        public const float DefaultRegionSeamFootDrawnMarginMetres = 0.1f;
        public const float DefaultRegionSeamBoatDrawnMarginMetres = 0.3f;
        public const float DefaultRegionSeamMinApproachSpeed = 0.1f;

        [Tooltip("The QUICKEST (s) the screen may go black at a seam. The fade follows how near the mover's " +
                 "leading edge is to the edge of the screen; this only stops a very fast (or teleported) mover " +
                 "getting a cut instead of a fade.")]
        [Min(0f)] public float RegionSeamFadeOutSeconds = DefaultRegionSeamFadeOutSeconds;
        [Tooltip("How long (s) the screen takes to clear again: on the far side of a seam, or when the mover " +
                 "stops or turns back before it.")]
        [Min(0f)] public float RegionSeamFadeInSeconds = DefaultRegionSeamFadeInSeconds;
        [Tooltip("The longest (s) the screen may stay black without the crossing being made. Past it the fade " +
                 "gives up and clears.")]
        [Min(0f)] public float RegionSeamMaxBlackSeconds = DefaultRegionSeamMaxBlackSeconds;
        [Tooltip("How far (m) from the edge of the screen a walker's leading edge is when the fade starts. The " +
                 "screen is black as he reaches the edge, whatever his speed (a mover too fast to cover it in the quickest " +
                 "fade starts further out, by that much). 0.45 m is the quickest fade at a 3 m/s walk. Bigger = gentler.")]
        [Min(0f)] public float RegionSeamFootFadeDistanceMetres = DefaultRegionSeamFootFadeDistanceMetres;
        [Tooltip("The same fade distance (m) for a boat, measured from her bow.")]
        [Min(0f)] public float RegionSeamBoatFadeDistanceMetres = DefaultRegionSeamBoatFadeDistanceMetres;
        [Tooltip("How far (m) a walker's drawn figure reaches beyond his collider, added to his extent so the " +
                 "screen is black before any of the drawing meets the screen edge.")]
        [Min(0f)] public float RegionSeamFootDrawnMarginMetres = DefaultRegionSeamFootDrawnMarginMetres;
        [Tooltip("How far (m) a boat's drawn hull reaches beyond her collider (bow, stern, oars), for the same " +
                 "purpose.")]
        [Min(0f)] public float RegionSeamBoatDrawnMarginMetres = DefaultRegionSeamBoatDrawnMarginMetres;
        [Tooltip("The slowest (m/s) a mover can head out of a region and still drive a seam fade. Below it the " +
                 "mover is standing at the edge, and the fade clears.")]
        [Min(0f)] public float RegionSeamMinApproachSpeed = DefaultRegionSeamMinApproachSpeed;

        /// <summary>Shipped seam re-arm distances, PR 1223 play look: an arrived mover does not fade at the edge it
        /// came in by until it is this far inside it.</summary>
        public const float DefaultRegionSeamRearmMoverLengths = 1.5f;
        public const float DefaultRegionSeamRearmMinMetres = 0.5f;

        [Tooltip("After a crossing, how far inside the edge the mover came in by (in her own lengths: a dory " +
                 "about 5 m, a walker under 1 m) she must come before that edge fades again. A boat idling or " +
                 "drifting just past a seam is never under a part-black screen.")]
        [Min(0f)] public float RegionSeamRearmMoverLengths = DefaultRegionSeamRearmMoverLengths;
        [Tooltip("The least re-arm distance (m) whatever the mover's length.")]
        [Min(0f)] public float RegionSeamRearmMinMetres = DefaultRegionSeamRearmMinMetres;

        /// <summary>Shipped return allowance, PR 1223 second play look: how far back out past the edge it came in
        /// by an arrived mover may be carried (a current, a wind) before that is a crossing back.</summary>
        public const float DefaultRegionSeamReturnMoverLengths = 0.5f;

        [Tooltip("After a crossing, how far back out past the edge she came in by (in her own lengths) a drift, " +
                 "a current or a wind may carry her before she crosses back. Stops a boat that stops rowing " +
                 "just past a seam being carried straight back over it. A real turn back crosses once past it, " +
                 "under a full fade.")]
        [Min(0f)] public float RegionSeamReturnMoverLengths = DefaultRegionSeamReturnMoverLengths;

        [Header("Ground prints (footprints and tyre marks)")]
        [Tooltip("Footprints and tyre marks on grass, sand, mud and dirt (owner ask 2026-11-15: \"the print " +
                 "lasts 24/h with a gradual fade\"). Lifetime is in IN-GAME hours; sizes are real metres; which " +
                 "ground takes a print is the Zones list. Read through GroundPrintSettings.Current.")]
        public GroundPrintSettings GroundPrints = GroundPrintSettings.Default;

        [Header("Dug holes (the shovel's hole on the flats)")]
        [Tooltip("Owner ask 2026-11-22: every dig, the player's or a working villager's, leaves a dark hole " +
                 "that stays for the rest of that game day or until the tide covers it, whichever is first. " +
                 "The villagers' holes follow from their day; the player's are remembered for the session " +
                 "only. Read through DugHoleSettings.Current.")]
        public DugHoleSettings DugHoles = DugHoleSettings.Default;

        [Header("Effort (too tired / too hungry)")]
        [Tooltip("Owner ask 2026-11-15: 'when a player is too tired to do an action they will say im too " +
                 "tired or im too hungry or both'. Which work may be refused (shipped: heavy work only), the " +
                 "need levels that refuse (tiredness is the main gate; hunger alone only at the very top), " +
                 "the three lines the player says, and how much sitting rests (a quarter of sleep, never " +
                 "below a floor only sleep clears). Work already under way is never interrupted. Read " +
                 "through EffortGate.Settings.")]
        public EffortSettings Effort = EffortSettings.Default;

        [Header("Stink lines (cleanliness, the look)")]
        [Tooltip("Cleanliness slice 2 (owner ask 2026-11-15): small wavy lines rise from the player's head " +
                 "while he is dirty, more of them as he gets dirtier, gone once he washes. A gentle joke: " +
                 "they change nothing but the picture. Derived from the saved dirt, never saved. Read " +
                 "through StinkLinesSettings.Current.")]
        public StinkLinesSettings StinkLines = StinkLinesSettings.Default;

        [Header("Wash fixtures (cleanliness, the look)")]
        [Tooltip("Owner ask 2026-11-17: baths and basins fill and drain, the shower runs, the water closet " +
                 "flushes, the player climbs into the bath, and suds froth while a soaped wash runs. The " +
                 "picture only; nothing here is saved. Read through WashVisualsSettings.Current.")]
        public WashVisualsSettings WashVisuals = WashVisualsSettings.Default;

        public float SecondsPerHour => SecondsPerDay / 24f;
        public float SecondsPerWeek => SecondsPerDay * DaysPerWeek;
        public float SecondsPerSeason => SecondsPerDay * DaysPerSeason;
        public float SecondsPerYear => SecondsPerSeason * 4f;
    }

    /// <summary>A calendar label; elapsed days and saved seconds retain their own epoch.</summary>
    public readonly struct CalendarDate
    {
        public readonly Season Season;
        public readonly int DayOfSeason;
        public readonly int Year;

        public CalendarDate(Season season, int dayOfSeason, int year)
        {
            Season = season;
            DayOfSeason = dayOfSeason;
            Year = year;
        }
    }

    /// <summary>The one conversion from elapsed days to the four-season calendar.</summary>
    public static class GameCalendar
    {
        public static CalendarDate AtDay(long elapsedDay, int daysPerSeason, int startDayOfYear)
        {
            int length = System.Math.Max(1, daysPerSeason);
            long yearLength = 4L * length;
            long calendarDay = elapsedDay + startDayOfYear;
            long dayOfYear = ((calendarDay % yearLength) + yearLength) % yearLength;
            return new CalendarDate((Season)(dayOfYear / length), (int)(dayOfYear % length) + 1,
                                    (int)((calendarDay - dayOfYear) / yearLength) + 1);
        }

        /// <summary>Read an absolute simulation instant without seeking or modifying its seconds.</summary>
        public static CalendarDate AtSeconds(double seconds, GameConfig config)
        {
            double dayLength = config != null ? config.SecondsPerDay : GameConfig.DefaultSecondsPerDay;
            long elapsedDay = dayLength > 0d ? (long)System.Math.Floor(System.Math.Max(0d, seconds) / dayLength) : 0L;
            int seasonLength = config != null ? config.DaysPerSeason : GameConfig.DefaultDaysPerSeason;
            int offset = config != null ? config.StartDayOfYear : GameConfig.DefaultDaysPerSeason;
            return AtDay(elapsedDay, seasonLength, offset);
        }
    }

    /// <summary>
    /// The fight-wide default tuning for the v2 rod fight (<see cref="GameConfig.RodFight"/>), named and
    /// owner-tunable (rule 6). Lives in Core beside the config it rides on — the same Core-policy /
    /// feature-consumer split as <see cref="SeakeepingSettings"/> (Core) vs its Boats-side math: Core cannot
    /// reference the Fishing module (rule 4), so the tunables live here and the pure math that consumes them
    /// (<c>RodFightMath</c>, Fishing-side) takes them as floats. Per-species overrides arrive later as a
    /// <c>RodFightDef</c> (lead-architect's contract) carrying these same six fields.
    ///
    /// All rates are in normalised gauge-units (0..1) per second; the caller integrates and clamps.
    /// <see cref="Default"/> is the forgiving-cove reference tuning and satisfies both guard-rails
    /// (<c>RodFightMath.PullAloneSnapsBeforeLanding</c> / <c>MaintainOutbleedsTheRun</c>), which the
    /// EditMode tests assert against the shipped asset.
    /// </summary>
    [System.Serializable]
    public struct RodFightSettings
    {
        [Tooltip("Tension gained per second while PULLING (reeling), 0..1-gauge/s. The snap pressure of a " +
                 "held reel. Guard-rail: must exceed LandingFillPerSec, so a blind sustained pull always " +
                 "snaps before it lands (skill is a pulse, not a pin).")]
        [Min(0f)] public float TensionRisePerSec;

        [Tooltip("Tension bled per second while MAINTAINING (holding steady, not reeling), 0..1-gauge/s. " +
                 "The recovery of backing off. Guard-rail: must exceed RunTensionPressure, so a MAINTAIN " +
                 "nets tension DOWN even through her hardest run.")]
        [Min(0f)] public float TensionFallPerSec;

        [Tooltip("Landing gained per second by a clean REEL of her slack, 0..1-gauge/s — and the ceiling on " +
                 "reeling against a run you're fully leaning into. The pace of a well-fought fight. Keep " +
                 "below TensionRisePerSec (the snap-before-land guard-rail).")]
        [Min(0f)] public float LandingFillPerSec;

        [Tooltip("EXTRA tension per second her run adds at full effort (fishEffort01 = 1), applied whether " +
                 "pulling or maintaining — she is fighting too. Keep below TensionFallPerSec so a run is a " +
                 "'back off' tell, never an unavoidable snap.")]
        [Min(0f)] public float RunTensionPressure;

        [Tooltip("Tension bled per second by leaning FULLY against a full run — live from the hookup, deep " +
                 "or surfaced; the same magnitude is the penalty for going WITH her. Bigger = the lean " +
                 "matters more, and the fight is more about direction than timing.")]
        [Min(0f)] public float CounterSteerRelief;

        [Tooltip("Landing fraction (0..1) at which she breaks the surface: the fight crosses Deep (she is " +
                 "unseen — you read her through the rod and the line's entry point) → Surface (she is " +
                 "visible at the end of your line). The fight itself is the same either side; this is how " +
                 "long she stays down. Lower = she shows herself sooner.")]
        [Range(0f, 1f)] public float SurfaceThreshold01;

        [Tooltip("DECK FISHING — the 'light real factor' (rod v2 §4.2): EXTRA tension per second " +
                 "(0..1-gauge/s) at the WORST deck stance, i.e. the line running fully ACROSS the hull " +
                 "(the fish off the far rail / astern of the wrong side while the unmanned boat " +
                 "weathervanes under you). It fades linearly to 0 as you walk the rail toward a clean " +
                 "line, and is exactly 0 anywhere off a boat — dock and shore fishing never feel it. " +
                 "0 = OFF: deck fights read exactly like the dock (set 0 to feel dock-parity first). " +
                 "Guard-rail: keep below TensionFallPerSec − RunTensionPressure so backing off still " +
                 "recovers even at the worst stance mid-run (cozy — a bad angle is a 'walk the rail' " +
                 "nudge, never an unavoidable snap; test-enforced on the default).")]
        [Min(0f)] public float DeckAngleFactor;

        /// <summary>
        /// The forgiving-cove reference tuning: a pull loads clearly faster than it lands (0.55 &gt; 0.35 —
        /// the blind hold snaps first), a maintain bleeds twice her run's pressure (0.70 &gt; 0.35 — backing
        /// off always recovers), a moderate counter-steer axis, the surface break at half-landed so both
        /// halves of the arc get play, and a gentle deck-angle factor (0.15) that keeps the on-deck
        /// guard-rail comfortably true (0.35 + 0.15 &lt; 0.70 — a maintain still bleeds at the worst
        /// stance mid-run; set it to 0 for exact dock-parity).
        /// </summary>
        public static RodFightSettings Default => new RodFightSettings
        {
            TensionRisePerSec = 0.55f,
            TensionFallPerSec = 0.70f,
            LandingFillPerSec = 0.35f,
            RunTensionPressure = 0.35f,
            CounterSteerRelief = 0.45f,
            SurfaceThreshold01 = 0.5f,
            DeckAngleFactor = 0.15f,
        };
    }

    /// <summary>
    /// Owner tuning for the <b>notched single-lever throttle</b> (<see cref="GameConfig.HelmThrottle"/>).
    /// The throttle is no longer on/off: Up/Down each step a <b>held</b> detent, so the player can hold
    /// part-throttle — the thing a keyboard couldn't do. These counts define how many detents lie between
    /// neutral and each end of travel; <c>ThrottleDetentModel</c> (Core) consumes them and produces the
    /// signed <c>drive</c> both the physics (<c>BoatController.SetControl</c>) and the diegetic lever
    /// (<c>LeverRig</c>) read. Reverse travel is usually shorter than ahead, like a real binnacle.
    /// </summary>
    [System.Serializable]
    public struct HelmThrottleSettings
    {
        [Tooltip("Detents between neutral and FULL AHEAD (≥ 1). More = finer throttle control; each Up " +
                 "press advances one. E.g. 4 gives quarter-throttle steps (0 / 0.25 / 0.5 / 0.75 / 1).")]
        [Min(1)] public int AheadNotches;

        [Tooltip("Detents between neutral and FULL ASTERN (≥ 1). Usually fewer than ahead (reverse has " +
                 "shorter travel). Full astern is still normalised to drive −1 (the astern-power factor on " +
                 "the hull does the 'weaker reverse'), so this is only how many steps to get there.")]
        [Min(1)] public int AsternNotches;

        [Tooltip("Hold-to-repeat rate, detents/second, while Up or Down is HELD (0 = edge-only: one detent " +
                 "per press, the deliberate binnacle feel). > 0 lets a held key walk the throttle open.")]
        [Min(0f)] public float HoldRepeatPerSec;

        [Tooltip("How long a throttle key must be HELD (real seconds) before hold-to-repeat starts " +
                 "walking detents (the owner's 'auto-repeat after a delay'). The press itself always " +
                 "steps immediately; this only delays the repeats.")]
        [Min(0f)] public float HoldRepeatDelaySec;

        [Tooltip("The neutral snap window, in drive units around 0: a mouse-dragged lever RELEASED " +
                 "inside this window clicks into the neutral detent (a real lever's centre gate); " +
                 "released anywhere else it holds exactly where it was left.")]
        [Range(0f, 0.5f)] public float NeutralSnapWindow01;

        /// <summary>Four ahead notches (quarter steps), two astern; press steps at once, repeats walk on
        /// after 0.35 s at 3/s (the owner's stepped-and-held directive, 2026-08-03); a ±0.08 centre gate.</summary>
        public static HelmThrottleSettings Default => new HelmThrottleSettings
        {
            AheadNotches = 4,
            AsternNotches = 2,
            HoldRepeatPerSec = 3f,
            HoldRepeatDelaySec = 0.35f,
            NeutralSnapWindow01 = 0.08f,
        };
    }

    /// <summary>
    /// Owner tuning for the <b>helm instrument overlay</b> (<see cref="GameConfig.HelmOverlay"/>) — the
    /// screen-space card that shows the active hull's diegetic control (the tiller or the binnacle
    /// lever, ADR 0025 S1) while piloting a motorised hull. Placement and the pointer-mapping radii
    /// live here so the whole feel is dialled in the Inspector with no code (rule 6). Presentation
    /// preferences only — never sim state, never saved (rule 5).
    ///
    /// <para>The card's FOCUS state and the composed dash's two scales retired with the dash card
    /// (H2): a wheel helm is its band, which takes its size from the screen, and the tiller's and the
    /// lone lever's controls are live on the one card.</para>
    /// </summary>
    [System.Serializable]
    public struct HelmOverlaySettings
    {
        [Tooltip("Scale of the small card in HUD reference pixels per rig pixel. 1 = native at 1280x720.")]
        [Min(0.1f)] public float SmallScale;

        [Tooltip("Where the SMALL card centres horizontally, as a 0..1 fraction of screen width. " +
                 "0.5 = bottom-centre (the owner's placement ruling, 2026-08-03).")]
        [Range(0f, 1f)] public float SmallCenterX01;

        [Tooltip("Margin (HUD reference px) from the screen's bottom edge — the card anchors to the bottom of the " +
                 "screen (the helm rises from the dash).")]
        [Min(0f)] public float MarginY;

        [Tooltip("How close (rig-space px) a click must land to the lever's grip to START a drag; " +
                 "clicks further out on the card jump-to-sig along the travel arc instead.")]
        [Min(1f)] public float GrabRadiusPx;

        [Tooltip("The tiller's throttle drag travel: how many rig-space px of vertical drag sweep the " +
                 "drive across its FULL range (up = ahead). Smaller = twitchier.")]
        [Min(1f)] public float TillerDragFullDrivePx;

        /// <summary>Native-size card CENTRED AT THE BOTTOM (the owner's 2026-08-03 placement
        /// ruling).</summary>
        public static HelmOverlaySettings Default => new HelmOverlaySettings
        {
            SmallScale = 1f,
            SmallCenterX01 = 0.5f,
            MarginY = 16f,
            GrabRadiusPx = 30f,
            TillerDragFullDrivePx = 140f,
        };
    }

    /// <summary>
    /// Owner tuning for the <b>grabbable steering wheel</b> (<see cref="GameConfig.HelmWheel"/> —
    /// ADR 0025 S2a). The spin model itself is the wheel rig's own
    /// (<c>docs/art/rigs/ui/console-wheel/Art/wheelRig.js</c> <c>step()</c>, ported to
    /// <c>WheelRigGeometry</c>); these are the three knobs the rig exposes, defaulted to its own
    /// values, plus the rim-grab pad. The wheel only ever DRIVES steer during a focused grab — it
    /// otherwise mirrors <c>BoatController.Steer</c> (one state, one owner).
    /// </summary>
    [System.Serializable]
    public struct HelmWheelSettings
    {
        [Tooltip("Lock-to-lock turns EACH WAY (wheelRig.js default 1.5 — cable steer on a 7 m " +
                 "skiff). Full lock = turns × 360° of wheel; steer = wheel angle / lock.")]
        [Min(0.25f)] public float Turns;

        [Tooltip("Coast friction (per-second exponential decay of spin velocity) after the rim is " +
                 "released. wheelRig.js default 2.4. Higher = the wheel dies faster.")]
        [Min(0f)] public float Friction;

        [Tooltip("Self-centre spring (0 = a working cable helm: released, the wheel coasts and " +
                 "HOLDS — the rig's stock feel). > 0 opts into a springy arcade return-to-centre.")]
        [Min(0f)] public float SelfCentre;

        [Tooltip("How far outside the wheel's outer rim (rig px) a grab still catches — the same " +
                 "kind of forgiveness as the lever's GrabRadiusPx.")]
        [Min(0f)] public float RimGrabPadPx;

        [Tooltip("KEY STEER EASE (S4.5, the owner's 'the wheel needs to follow the arrow keys — " +
                 "gradual and smooth'): how long (real seconds) a held steer key takes to wind the " +
                 "helm from CENTRE to FULL LOCK. A full reversal takes twice this — a wheel does not " +
                 "spin faster because you asked for more of it. Releasing winds back to centre at the " +
                 "same rate. ⚠ This eases the commanded STEER, not just the wheel picture, so it is a " +
                 "handling change as well as a look: full lock arrives this much later than it used " +
                 "to. 0 = OFF (the pre-S4.5 instant lock-to-lock step). The gamepad stick is already " +
                 "analog and is passed through undamped.")]
        [Min(0f)] public float SteerEaseSeconds;

        /// <summary>The wheel rig's own stock feel (wheelRig.js:183-191): 1.5 turns, friction 2.4,
        /// no self-centre (cable steer holds), an 8 px rim-grab pad — plus a quarter-second wind to
        /// full lock on the keys (S4.5; the owner tunes it, code default ships).</summary>
        public static HelmWheelSettings Default => new HelmWheelSettings
        {
            Turns = 1.5f,
            Friction = 2.4f,
            SelfCentre = 0f,
            RimGrabPadPx = 8f,
            SteerEaseSeconds = 0.25f,
        };
    }

    /// <summary>
    /// Owner tuning for the <b>autopilot's heading hold</b> (<see cref="GameConfig.HelmAutopilot"/>).
    /// A plain PID on the heading error that writes the helm the player would have written; the
    /// hull's own rudder authority (which scales with way) does the turning, so there is no new
    /// physics. Run on the fixed physics step; transient, never saved (it is off after a load).
    ///
    /// <para>Sign: heading is the body's rotation in degrees, counter-clockwise positive; a positive
    /// helm turns the bow to starboard (clockwise), so a heading left of the target (positive error)
    /// asks for positive helm.</para>
    /// </summary>
    [System.Serializable]
    public struct HelmAutopilotSettings
    {
        [Tooltip("Proportional gain: helm (−1..1) per degree off the held heading. 0.04 = the most " +
                 "helm it may use (MaxHelm) at 15° off.")]
        [Min(0f)] public float HeadingGainPerDegree;

        [Tooltip("Damping gain: helm per degree/second the bow is swinging. Checks the swing before " +
                 "she overshoots. 0 = no damping term.")]
        [Min(0f)] public float YawRateGainPerDegreePerSecond;

        [Tooltip("Integral gain: helm added per degree-second of error, which is what takes out a " +
                 "steady offset (a beam wind, a sea on the quarter). 0 = no integral term.")]
        [Min(0f)] public float IntegralGainPerDegreeSecond;

        [Tooltip("The most helm the integral term may carry, either way (anti-windup).")]
        [Min(0f)] public float IntegralLimitHelm;

        [Tooltip("The most helm the autopilot may use, either way (0..1). A real autopilot holds a " +
                 "course with small corrections and never puts the wheel hard over.")]
        [Range(0f, 1f)] public float MaxHelm;

        [Tooltip("How fast the autopilot may wind the helm, in helm units per second (2 = centre to " +
                 "full lock in half a second). 0 = no limit.")]
        [Min(0f)] public float HelmSlewPerSecond;

        [Tooltip("The least way through the water (m/s, ahead) the autopilot will steer with. Below " +
                 "it the rudder has little bite and the autopilot hands the helm back (and will not " +
                 "engage). Going astern counts as below it.")]
        [Min(0f)] public float MinWayMetresPerSecond;

        [Tooltip("How hard a steer input must be (0..1 of full lock) to take the helm back from the " +
                 "autopilot. Keeps a resting stick's jitter from switching it off.")]
        [Range(0f, 1f)] public float SteerOverrideThreshold;

        [Tooltip("How hard a knock must be (relative speed, m/s) to switch the autopilot off. A hull " +
                 "resting against a fender or a quay is not a collision.")]
        [Min(0f)] public float CollisionMinImpactMetresPerSecond;

        /// <summary>The shipped tuning. Checked in a simulated step loop against a 40x spread of yaw
        /// inertia (<c>HeadingHoldAutopilotTests</c>); feel is the owner's to set.</summary>
        public static HelmAutopilotSettings Default => new HelmAutopilotSettings
        {
            HeadingGainPerDegree = 0.04f,
            YawRateGainPerDegreePerSecond = 0.03f,
            IntegralGainPerDegreeSecond = 0.004f,
            IntegralLimitHelm = 0.3f,
            MaxHelm = 0.6f,
            HelmSlewPerSecond = 2f,
            MinWayMetresPerSecond = 0.5f,
            SteerOverrideThreshold = 0.2f,
            CollisionMinImpactMetresPerSecond = 0.5f,
        };
    }

    /// <summary>
    /// Owner tuning for how the <b>camera answers the helm's footprint</b>
    /// (<see cref="GameConfig.HelmFootprint"/> — ADR 0050). While the helm's UI covers a band across the
    /// whole width of the screen (<see cref="HelmFootprint"/>), the boat's place on screen eases up by
    /// <see cref="CameraBandShare"/> of the band's height, so the sea the band leaves is centred on the
    /// boat. A card never moves the camera, and nothing covered means no move at all.
    ///
    /// <para>Presentation only (rule 5): recomputed every frame, never saved.</para>
    /// </summary>
    [System.Serializable]
    public struct HelmFootprintSettings
    {
        [Tooltip("The share of a full-width helm band's height the boat's place on screen rises by. " +
                 "0.5 (the ruling) centres the boat in the sea above the band; 0 turns the move off.")]
        [Range(0f, 1f)] public float CameraBandShare;

        [Tooltip("Seconds the camera takes to ease the boat up when a full-width band comes up, and " +
                 "back down when it goes. 0 = at once.")]
        [Min(0f)] public float CameraEaseSeconds;

        /// <summary>Half the band (the ruling), eased over 0.4 s — the camera's own framing-tween
        /// time, so the two moves read as one hand on the camera.</summary>
        public static HelmFootprintSettings Default => new HelmFootprintSettings
        {
            CameraBandShare = 0.5f,
            CameraEaseSeconds = 0.4f,
        };
    }

    /// <summary>
    /// Owner tuning for the <b>depth sounder</b> (<see cref="GameConfig.DepthSounder"/>) — the first
    /// purchasable brow instrument (ADR 0025 S2, <c>docs/art/rigs/ui/depth-finder/</c>). Everything the
    /// instrument's behaviour and placement depends on lives here so it is dialled in the Inspector with
    /// no code (rule 6).
    ///
    /// <para><b>The reading itself is not tunable</b> — it is <c>waterLevel − seabedElevation</c> over the
    /// one shared height map (<see cref="DepthSounder.DisplayDepth"/>, rule 5). These are the instrument's
    /// dials, not the sea's.</para>
    ///
    /// <para>No card placement lives here any more: since H2 the sounder's place on the helm is its
    /// housing in the band, and its raised card is placed by the one rule every instrument shares.</para>
    /// </summary>
    [System.Serializable]
    public struct DepthSounderSettings
    {
        [Tooltip("How often the transducer takes a sounding (real seconds). The tide moves in MINUTES, so " +
                 "reading per frame would be pure waste (rule 7). Smaller = more responsive when the boat " +
                 "is crossing a bar quickly.")]
        [Min(0.01f)] public float ReadIntervalSec;

        [Tooltip("Shallow set-point (m) a freshly fitted sounder starts at — the depth at or below which " +
                 "the glass flashes SHALLOW.")]
        [Min(0f)] public float DefaultAlarmMetres;

        [Tooltip("Is the shallow alarm armed out of the box? (A new sounder normally arrives armed.)")]
        public bool DefaultArmed;

        [Tooltip("Does a freshly fitted sounder read in FEET rather than metres? The player can toggle it " +
                 "on the glass either way; this is only where it starts.")]
        public bool DefaultFeet;

        [Tooltip("How far each press of the ALARM up/down pushers moves the set-point (m). The rig's own " +
                 "pushers are labelled ±0.5.")]
        [Min(0.01f)] public float AlarmStepMetres;

        [Tooltip("Shallowest set-point the pushers can reach (m).")]
        [Min(0f)] public float MinAlarmMetres;

        [Tooltip("Deepest set-point the pushers can reach (m).")]
        [Min(0f)] public float MaxAlarmMetres;

        [Tooltip("Flash rate of the SHALLOW alarm, full on/off cycles per second. The card repaints only " +
                 "on a flash FLIP and only while the alarm is sounding (rule 7).")]
        [Min(0f)] public float AlarmBlinkHz;

        [Tooltip("⚠ PLACEHOLDER. The water temperature (°C) the LCD prints. There is NO water-temperature " +
                 "model in the simulation yet and this slice does not build one (rule 8 — stay in phase); " +
                 "the rig has a temp line, so it shows this constant until a real one exists. Flagged in " +
                 "the PR that shipped it.")]
        public float PlaceholderWaterTempC;

        [Tooltip("Do the depth sounder and the fish finder print the water temperature? OFF while the " +
                 "temperature is the constant above, so no glass shows a reading the game does not " +
                 "simulate (the owner's 2026-11-13 ruling: temperature is shown only when it is real). " +
                 "Turn it ON only once a real water-temperature model feeds the line.")]
        public bool ShowWaterTemp;

        /// <summary>A 4 Hz sounding, armed at 3 m with ±0.5 m pushers (the rig's own defaults), a 2 Hz
        /// flash, and metres.</summary>
        public static DepthSounderSettings Default => new DepthSounderSettings
        {
            ReadIntervalSec = 0.25f,
            DefaultAlarmMetres = 3f,
            DefaultArmed = true,
            DefaultFeet = false,
            AlarmStepMetres = 0.5f,
            MinAlarmMetres = 0.5f,
            MaxAlarmMetres = 20f,
            AlarmBlinkHz = 2f,
            PlaceholderWaterTempC = 12f,
            ShowWaterTemp = false,
        };
    }

    /// <summary>
    /// The <b>fish finder's</b> owner tunables (<see cref="GameConfig.FishFinder"/> — ADR 0025 S3, the sonar
    /// that supersedes the plain depth sounder in the same cutout).
    ///
    /// <para><b>Why this block exists at Step 0 with one field in it.</b> The vertical RANGE is the finder's
    /// only genuinely NEW piece of persisted state, and it is the denominator of the whole picture — the
    /// bottom contour sits at <c>depth / range</c> (<c>fishRig.js:239</c>). That makes its default a
    /// safety-critical number rather than a taste one: at zero the contour is Inf/NaN, not small. Naming it
    /// here, once, is what lets the save migration heal an old row and the UI draw a fresh one from the SAME
    /// value (rule 6 — the number is data, never a literal in two places). The rest of the finder's tuning
    /// — scan cadence, mark sizing and the placeholder strip — joined it in the UI slice (S3b) and is
    /// documented field by field below; its card placement retired with the band (H2).</para>
    ///
    /// <para><b><see cref="WaterfallHz"/> is the perf knob, not a taste one.</b> The rig's scan phase
    /// free-runs, and its <c>sonarView</c> is O(width) in <c>fillRect</c> spans plus a whole-texture
    /// upload; repainting it per frame is a rule-7 break on its own. The host quantizes the phase to this
    /// rate and folds the bucket into its change key, so the instrument's cost is data the owner can dial
    /// rather than a frame counter.</para>
    ///
    /// <para><b>Two fields are ⚠ PLACEHOLDERS</b> (<see cref="PlaceholderSens01"/>,
    /// <see cref="PlaceholderLink"/>) — the rig's status strip shows a sensitivity and a transducer
    /// link, and the simulation models neither. Following the shipped precedent
    /// (<see cref="DepthSounderSettings.PlaceholderWaterTempC"/>) they are constants with a tooltip that
    /// says what they are and why (rule 8). The strip's battery cell and supply volts were placeholders
    /// too and are GONE from the live glass (the owner's 2026-11-13 ruling: no boat has an electrical
    /// model, so there is nothing true to print); the rig can still draw them, the game never asks.</para>
    ///
    /// <para>The alarm, its set-point, units and night backlight are deliberately NOT here: the finder
    /// keeps the depth sounder's shallow alarm unchanged, reading the same
    /// <see cref="DepthSounderSettings"/> and the same <see cref="SounderPrefs"/> (one alarm rule, not
    /// two).</para>
    /// </summary>
    [System.Serializable]
    public struct FishFinderSettings
    {
        [Tooltip("Vertical sonar scale (metres) a freshly fitted fish finder starts at — how deep the " +
                 "bottom of the glass reaches. The player steps it with the RANGE pushers; the rig's own " +
                 "scale choices are 10 / 20 / 40 / 60 m. ⚠ Must stay ABOVE ZERO: the bottom contour is " +
                 "drawn at depth ÷ range, so a zero range is a divide-by-zero (Inf/NaN — a garbage " +
                 "contour), not merely a small picture. The save migration heals any stored range that " +
                 "is not positive back to this value.")]
        [Min(1f)] public float DefaultRangeMetres;

        [Tooltip("How many times a second the sonar redraws its scan — the finder's PING RATE, and the " +
                 "single number that decides what it costs (rule 7). The rig's scan phase free-runs, so a " +
                 "naive port would raster thousands of spans AND upload a whole texture EVERY FRAME; the " +
                 "host quantizes the phase to this rate instead and change-detects on the bucket. " +
                 "MEASURED: one repaint is ~4.8 ms (480x660, raster + upload), so each Hz here costs " +
                 "~4.8 ms per second of a 16.7 ms frame budget. Shipped at 4 = ~19 ms/s; 8 scrolls more " +
                 "smoothly for ~38 ms/s. ⚠ Keep it a MULTIPLE of 2x DepthSounder.AlarmBlinkHz, so a " +
                 "sounding alarm's flashes land on scan steps and cost no extra repaints.")]
        [Min(0.5f)] public float WaterfallHz;

        [Tooltip("Does a freshly fitted finder tag each fish mark with its depth? The player toggles it by " +
                 "tapping the sonar glass; this is only where it starts. Not persisted — unlike the alarm " +
                 "and the range, it is a look-at-it-now toggle, not a setting a fisherman expects to find " +
                 "tomorrow.")]
        public bool DefaultFishId;

        [Tooltip("Fish-mark size (the rig's own 'size' multiplier, ~0.5-2.5) for a school you are only " +
                 "clipping the EDGE of — signal strength 0. Small marks read as a weak return.")]
        [Range(0.2f, 3f)] public float MarkSizeMin;

        [Tooltip("Fish-mark size for a school you are sitting on the CENTRE of — signal strength 1.")]
        [Range(0.2f, 3f)] public float MarkSizeMax;

        [Tooltip("How much each individual mark's size wanders from the school's, 0..1. Pure look: a " +
                 "shoal of identical fish reads as a graphic, not a return. Deterministic (the rig's own " +
                 "hash noise), never random — a repaint must not reshuffle the picture.")]
        [Range(0f, 1f)] public float MarkSizeJitter01;

        [Tooltip("How far in from the left/right edges of the sonar box marks are scattered, as a 0..1 " +
                 "fraction of its width. Keeps a mark from being half-clipped by the ruler or the bezel.")]
        [Range(0f, 0.45f)] public float MarkXMargin01;

        [Tooltip("How far above/below the school's stated depth individual marks may sit (metres). A " +
                 "school is an AREA at a DEPTH, not a line of fish at one exact metre. Purely cosmetic: " +
                 "the depth that fishes is the school's own.")]
        [Min(0f)] public float MarkDepthJitterMetres;

        [Tooltip("Hard cap on how many fish icons ONE school draws, however dense it is. The glass is " +
                 "small and the raster is per-mark (rule 7) — beyond a handful you are drawing soup.")]
        [Range(1, 24)] public int MaxMarksPerSchool;

        [Tooltip("⚠ PLACEHOLDER. Signal-strength bars (0..1) on the status strip. There is NO transducer " +
                 "or sensitivity model in the simulation and this slice does not build one (rule 8 — stay " +
                 "in phase); the rig has the bars, so they show this constant. Flagged in the PR that " +
                 "shipped it.")]
        [Range(0f, 1f)] public float PlaceholderSens01;

        [Tooltip("⚠ PLACEHOLDER. Transducer-link indicator on the status strip. No transducer model " +
                 "exists; a fitted finder is simply linked. Flagged in the PR that shipped it.")]
        public bool PlaceholderLink;

        /// <summary>The rig's own defaults where it has them — 20 m scale (the second of its four RANGE
        /// steps, <c>fishRig.js:131</c>/<c>:317</c>), fish-ID on (<c>:319</c>), and the two status-strip
        /// placeholders exactly as <c>fishRig.js:320-321</c> states them (sens 0.75, link on) — so
        /// that part of the shipped glass looks like the art director's preview until real models exist.
        /// A 4 Hz scan (measured at ~4.8 ms a repaint, so ~19 ms of every second — and a multiple of
        /// 2× the 2 Hz alarm blink, so the flash costs nothing extra).</summary>
        public static FishFinderSettings Default => new FishFinderSettings
        {
            DefaultRangeMetres = 20f,
            WaterfallHz = 4f,
            DefaultFishId = true,
            MarkSizeMin = 0.7f,
            MarkSizeMax = 1.6f,
            MarkSizeJitter01 = 0.25f,
            MarkXMargin01 = 0.12f,
            MarkDepthJitterMetres = 0.6f,
            MaxMarksPerSchool = 8,
            PlaceholderSens01 = 0.75f,
            PlaceholderLink = true,
        };
    }

    /// <summary>
    /// The chartplotter's tunables (<see cref="GameConfig.Chartplotter"/> — ADR 0025 S6): the caps that
    /// bound nav data in the save, and the RANGE ladder the chart zooms through.
    ///
    /// <para><b>⚠ The range ladder is NOT the rig's.</b> <c>navRig.js:540</c> ships
    /// <c>RANGE_STEPS=[1,2,4,6,10,16]</c> nautical miles, drawn against a fictional 22 × 17 NM chart.
    /// The real world is a few hundred metres across — St Peters is
    /// <c>WorldSizeMeters {760, 520}</c> = <b>0.41 × 0.28 NM</b> — so the rig's SMALLEST range already
    /// shows the entire region at under half the chart's width, and every step above it is mostly
    /// empty sea. The unit (NM) is kept because that is what the rig draws and what a plotter reads
    /// in; only the ladder is re-scaled, to even doublings from a harbour range. Nothing about the
    /// chart's depths or distances changes — this is the zoom control, not the survey.</para>
    /// </summary>
    [System.Serializable]
    public struct ChartplotterSettings
    {
        [Tooltip("Most waypoints the player may keep across the whole save (all regions together). The " +
                 "chart refuses to mark another once this is reached rather than silently dropping the " +
                 "oldest — a waypoint is a deliberate act and losing one quietly would be worse.")]
        [Min(1)] public int MaxWaypoints;

        [Tooltip("Most legs one planned route may have. A route is a working plan, not an archive.")]
        [Min(1)] public int MaxRouteLegs;

        [Tooltip("Most track breadcrumbs kept. This one IS a ring buffer — the track is a record of " +
                 "where you have been and the OLDEST crumb is the right thing to lose when it fills.")]
        [Min(2)] public int MaxTrackPoints;

        [Tooltip("How far (metres) the boat must move before the track takes another crumb. Together " +
                 "with MaxTrackPoints this decides how much water the breadcrumb remembers: 8 m × 512 " +
                 "≈ 4 km of sailing, several times across a region. Too small and the track is a dense " +
                 "smear that fills with one harbour manoeuvre.")]
        [Min(0.5f)] public float TrackMinSpacingMetres;

        [Tooltip("The CLOSEST chart range, in nautical miles — the first rung of the ladder. 0.05 NM " +
                 "≈ 93 m across the glass, about a wharf and its approach.")]
        [Min(0.001f)] public float MinRangeNM;

        [Tooltip("How many rungs the range ladder has. Each is DOUBLE the one below, so the default " +
                 "6 rungs from 0.05 NM reach 1.6 NM (≈ 93 m … 3 km) — from a berth to well outside " +
                 "any region the game has.")]
        [Min(1)] public int RangeStepCount;

        [Tooltip("Which rung a freshly fitted plotter starts on (0 = closest). 2 = 0.2 NM ≈ 370 m, " +
                 "which frames a whole small region.")]
        [Min(0)] public int DefaultRangeStep;

        /// <summary>
        /// The range in NM at a rung, healed against a config block that never got written.
        ///
        /// <para><b>Why this heals rather than trusting the field.</b> A struct member absent from the
        /// wired <c>GameConfig.asset</c> block deserializes to ZERO, not to <see cref="Default"/> — the
        /// trap that shipped three features inert on 2026-08-05. A zero <see cref="MinRangeNM"/> would
        /// make the chart's world-to-screen scale divide by zero and draw nothing at all, so it is
        /// caught here, at the one place the ladder is read.</para>
        /// </summary>
        public float RangeNMAt(int step)
        {
            float min = MinRangeNM > 0f ? MinRangeNM : Default.MinRangeNM;
            int count = RangeStepCount > 0 ? RangeStepCount : Default.RangeStepCount;
            if (step < 0) step = 0;
            else if (step > count - 1) step = count - 1;
            return min * (1 << step);
        }

        /// <summary>Number of rungs, healed the same way <see cref="RangeNMAt"/> heals.</summary>
        public int SafeRangeStepCount => RangeStepCount > 0 ? RangeStepCount : Default.RangeStepCount;

        /// <summary>The rung a fresh unit starts on, clamped into the healed ladder.</summary>
        public int SafeDefaultRangeStep
        {
            get
            {
                int c = SafeRangeStepCount;
                int s = DefaultRangeStep;
                if (s < 0) s = 0;
                else if (s > c - 1) s = c - 1;
                return s;
            }
        }

        /// <summary>Shipped defaults: bounded nav data, and a range ladder sized for a harbour rather
        /// than for the rig's fictional ocean chart.</summary>
        public static ChartplotterSettings Default => new ChartplotterSettings
        {
            MaxWaypoints = 64,
            MaxRouteLegs = 24,
            MaxTrackPoints = 512,
            TrackMinSpacingMetres = 8f,
            MinRangeNM = 0.05f,
            RangeStepCount = 6,
            DefaultRangeStep = 2,
        };
    }

    /// <summary>
    /// The radar's tunables (<see cref="GameConfig.Radar"/> — ADR 0025 S5): the RANGE ladder the scope
    /// zooms through, how hard the set is turned up, how much clutter the sea throws back, and the
    /// budget the coastline scan runs to.
    ///
    /// <para><b>⚠ The range ladder is NOT the rig's</b> — the same correction
    /// <see cref="ChartplotterSettings"/> makes, for the same reason. <c>radarRig.js:129</c> ships
    /// <c>RANGE_STEPS=[0.5,1,2,3,6,12,24]</c> nautical miles, drawn against a fictional ocean. St Peters
    /// is <c>WorldSizeMeters {760, 520}</c> = <b>0.41 × 0.28 NM</b>, so the rig's SMALLEST range already
    /// puts the whole region inside a quarter of the scope and every rung above it is empty water with
    /// the coast pinned at the centre. The unit (NM) is kept, because that is what the rig draws and
    /// what a radar reads in; only the ladder is re-scaled, to even doublings from a harbour range.
    /// Deliberately the SAME ladder shape as the plotter's, with its own fields: the two instruments
    /// sit side by side on one brow and a skipper comparing them should not have to translate.</para>
    ///
    /// <para><b>Gain is here and clutter is not.</b> The rig draws GAIN, SEA and RAIN dials but authors
    /// no pusher for any of them, so none can be a player preference (rule 6 — a knob that never moves).
    /// Gain is factory tuning and lives here. SEA clutter is read live from the sea state instead, which
    /// is the honest source and is what makes the instrument answer to the weather (P1) — this block
    /// only says how much clutter a FULL gale is worth. RAIN has no source at all in this slice and is
    /// drawn at zero: precipitation returns are the Smother's payoff (canon M4), and mapping the
    /// existing <c>Visibility</c> (fog) onto the rain dial would invert the instrument's whole meaning,
    /// since seeing THROUGH fog is exactly what a radar is for.</para>
    /// </summary>
    [System.Serializable]
    public struct RadarSettings
    {
        [Tooltip("The CLOSEST radar range, in nautical miles — the first rung of the ladder. 0.05 NM " +
                 "≈ 93 m to the scope's rim, about a wharf and its approach.")]
        [Min(0.001f)] public float MinRangeNM;

        [Tooltip("How many rungs the range ladder has. Each is DOUBLE the one below, so the default " +
                 "6 rungs from 0.05 NM reach 1.6 NM (≈ 93 m … 3 km) — from a berth to well outside " +
                 "any region the game has.")]
        [Min(1)] public int RangeStepCount;

        [Tooltip("Which rung a freshly fitted set starts on (0 = closest). 2 = 0.2 NM ≈ 370 m to the " +
                 "rim, which puts a whole small region on the scope.")]
        [Min(0)] public int DefaultRangeStep;

        [Tooltip("How many range RINGS the scope draws between the centre and the rim (radarRig.js " +
                 "`rings`). Four is the marine convention and makes each ring a quarter of the range.")]
        [Min(1)] public int Rings;

        [Tooltip("How hard the set is turned up, 0..1 (the rig's GAIN dial). Scales every echo's " +
                 "brightness. Factory tuning: no pusher exposes it, so it is not a player preference.")]
        [Range(0f, 1f)] public float Gain;

        [Tooltip("How much sea clutter a FULL gale throws back, 0..1 (the rig's SEA dial). Scaled by " +
                 "the live sea state, so flat calm is clean and a blow speckles the middle of the " +
                 "scope — the instrument answering to the weather rather than to a constant.")]
        [Range(0f, 1f)] public float SeaClutterAtFullSeaState;

        [Tooltip("How fast the aerial turns, in revolutions per minute. A real small-craft set runs 24 " +
                 "to 48; 24 is one sweep every two and a half seconds, which reads as deliberate " +
                 "rather than frantic on a scope this size.")]
        [Min(1f)] public float SweepRpm;

        [Tooltip("How many times a SECOND the scope may repaint while the set is transmitting. This is " +
                 "the rule-7 budget: a turning sweep changes the picture every frame, so the glass is " +
                 "capped here rather than repainting per frame. In STANDBY nothing turns and the scope " +
                 "falls back to pure change-detection, costing nothing at all. ONE repaint of this " +
                 "480x660 canvas measures 4.79 ms (ADR 0025), so 8 Hz is about 38 ms/s — roughly 3.8% " +
                 "of a 60 fps budget. Raising this is a real cost: 12 Hz is 5.7%.")]
        [Min(1f)] public float SweepRepaintHz;

        [Tooltip("How many AZIMUTHS the coastline scan takes per sweep. The land echo is a polar scan " +
                 "outward from the boat, so this times MaxLandEchoes bounds the whole cost. 72 = every " +
                 "5°, which is finer than the rig's own 5° bearing ticks.")]
        [Min(8)] public int LandScanAzimuths;

        [Tooltip("Most land echoes the scan may publish in one bake. A hard ceiling so a boat sitting " +
                 "in a cove full of rock cannot cost more than a boat in open water (rule 7).")]
        [Min(1)] public int MaxLandEchoes;

        [Tooltip("Metres of tide movement that force the coastline scan to re-run. A radar sees the " +
                 "waterline of the MOMENT, so the coast really does move with the tide — but it moves " +
                 "in minutes, and re-scanning for a centimetre would be a rule-7 disaster.")]
        [Min(0.001f)] public float LandRescanTideMetres;

        /// <summary>
        /// The range in NM at a rung, healed against a config block that never got written.
        ///
        /// <para><b>Why this heals rather than trusting the field.</b> A struct member absent from the
        /// wired <c>GameConfig.asset</c> block deserializes to ZERO, not to <see cref="Default"/> — the
        /// trap that shipped three features inert on 2026-08-05. A zero <see cref="MinRangeNM"/> would
        /// make the scope's world-to-glass scale divide by zero and draw every contact on top of the
        /// boat, so it is caught here, at the one place the ladder is read.</para>
        /// </summary>
        public float RangeNMAt(int step)
        {
            float min = MinRangeNM > 0f ? MinRangeNM : Default.MinRangeNM;
            int count = RangeStepCount > 0 ? RangeStepCount : Default.RangeStepCount;
            if (step < 0) step = 0;
            else if (step > count - 1) step = count - 1;
            return min * (1 << step);
        }

        /// <summary>Number of rungs, healed the same way <see cref="RangeNMAt"/> heals.</summary>
        public int SafeRangeStepCount => RangeStepCount > 0 ? RangeStepCount : Default.RangeStepCount;

        /// <summary>The rung a fresh unit starts on, clamped into the healed ladder.</summary>
        public int SafeDefaultRangeStep
        {
            get
            {
                int c = SafeRangeStepCount;
                int s = DefaultRangeStep;
                if (s < 0) s = 0;
                else if (s > c - 1) s = c - 1;
                return s;
            }
        }

        /// <summary>Range rings, healed — a zero would draw none and make the scope unreadable.</summary>
        public int SafeRings => Rings > 0 ? Rings : Default.Rings;

        /// <summary>Aerial speed, healed — a zero would leave the sweep parked and make a transmitting
        /// set look exactly like a broken one.</summary>
        public float SafeSweepRpm => SweepRpm > 0f ? SweepRpm : Default.SweepRpm;

        /// <summary>Repaint ceiling, healed — a zero would freeze the picture entirely.</summary>
        public float SafeSweepRepaintHz => SweepRepaintHz > 0f ? SweepRepaintHz : Default.SweepRepaintHz;

        /// <summary>Degrees the aerial turns in a second, from <see cref="SafeSweepRpm"/>.</summary>
        public float SweepDegreesPerSecond => SafeSweepRpm * 360f / 60f;

        /// <summary>Azimuths per land scan, healed — a zero would silently drop the coast entirely,
        /// which is the failure mode hardest to tell from "there is no land here".</summary>
        public int SafeLandScanAzimuths => LandScanAzimuths >= 8 ? LandScanAzimuths : Default.LandScanAzimuths;

        /// <summary>Land-echo ceiling, healed — a zero would draw no coast at all.</summary>
        public int SafeMaxLandEchoes => MaxLandEchoes > 0 ? MaxLandEchoes : Default.MaxLandEchoes;

        /// <summary>Tide step that re-runs the coastline scan, healed — a zero would re-scan every
        /// single frame the tide moved a float's worth, which is every frame.</summary>
        public float SafeLandRescanTideMetres
            => LandRescanTideMetres > 0f ? LandRescanTideMetres : Default.LandRescanTideMetres;

        /// <summary>Shipped defaults: a range ladder sized for a harbour rather than for the rig's
        /// fictional ocean, a set turned up about three-quarters, and a coastline scan whose worst case
        /// is a few hundred terrain samples on a slow tick.</summary>
        public static RadarSettings Default => new RadarSettings
        {
            MinRangeNM = 0.05f,
            RangeStepCount = 6,
            DefaultRangeStep = 2,
            Rings = 4,
            Gain = 0.72f,                       // radarRig.js:396 — the rig's own authored default
            SeaClutterAtFullSeaState = 0.45f,
            SweepRpm = 24f,
            SweepRepaintHz = 8f,
            LandScanAzimuths = 72,              // every 5°
            MaxLandEchoes = 96,
            LandRescanTideMetres = 0.05f,
        };
    }

    /// <summary>
    /// The owner-tunable feel of the <b>flick-cast</b> (<see cref="GameConfig.FlickCast"/> — Rod Fishing v2,
    /// design/rod-fishing-v2-brainstorm.md §2.2), named and serializable so the whole gesture is dialled in
    /// the Inspector with no code (rule 6). Lives in Core beside the config it rides on, the same
    /// Core-policy / feature-consumer split as <see cref="RodFightSettings"/>: the pure maths that consumes
    /// it (<c>FlickCastMath</c>, Fishing-side) is fed this struct plus the cast cap.
    ///
    /// <para><b>Two dials decide the distance</b> (owner's ruling 2026-07-23): how far you WIND BACK sets
    /// the range you're aiming at (<see cref="FullRangeWindBackMetres"/>), and how hard you SNAP the
    /// forward sweep decides how much of it you deliver (<see cref="FullSnapFlickSpeed"/> /
    /// <see cref="LimpFlickFraction01"/>). Where you release carries no penalty — the earlier model scored
    /// release position in world metres and, on a ~16 m-wide screen, quietly collapsed every cast onto the
    /// <see cref="MinCastMetres"/> floor.</para>
    ///
    /// <para><b>The per-gear seam.</b> <see cref="MaxCastDistanceMetres"/> is the CAP a full wind-back,
    /// fully snapped, reaches. It is a GameConfig field for now; later, better rods/tackle extend it
    /// (P4), so the maths takes the cap as its own explicit parameter — a GearDef's own cap slots in
    /// without touching this struct.</para>
    /// </summary>
    [System.Serializable]
    public struct FlickCastSettings
    {
        [Tooltip("How far BEHIND the character (m, along the flick) the mouse must have wound back for the " +
                 "gesture to count as a cast at all. Below this the rod was never loaded — nothing flies, " +
                 "you just stand back up (no penalty). Smaller = more forgiving wind-up.")]
        [Min(0f)] public float MinWindBackMetres;

        [Tooltip("Shortest forward sweep (m, wind-back point to release) that still casts. Anything shorter " +
                 "is a twitch, not a flick — nothing flies. Keep small; this only rejects accidents.")]
        [Min(0f)] public float MinFlickLengthMetres;

        [Tooltip("THE RANGE DIAL. How far behind you the mouse must be drawn (m) to aim the FULL cast " +
                 "distance. Draw back half of this and you're aiming half as far. This is exactly what the " +
                 "aim preview shows you while you wind back. Smaller = long casts come from small " +
                 "wind-ups; larger = you must really draw back to reach out.")]
        [Min(0f)] public float FullRangeWindBackMetres;

        [Tooltip("THE SNAP DIAL. Sweep SPEED (m/s at the fastest part of the forward flick) that delivers " +
                 "the whole range you aimed at. Smaller = even a gentle sweep gets there; larger = only a " +
                 "real snap of the wrist does.")]
        [Min(0f)] public float FullSnapFlickSpeed;

        [Tooltip("Fraction of the aimed range (0..1) that a completely LIMP sweep still delivers. This is " +
                 "the dribbled cast that lands well short of where you were aiming — keep it above 0 so a " +
                 "weak flick still puts the line in the water (cozy fail). Lower = the wrist-snap matters " +
                 "more.")]
        [Range(0f, 1f)] public float LimpFlickFraction01;

        [Tooltip("Shortest distance (m) any successful cast lands from the character. The floor under a " +
                 "weak flick, so the bobber is always at least in the water, not on your boots.")]
        [Min(0f)] public float MinCastMetres;

        [Tooltip("The CAP (m): the farthest a full wind-back, fully snapped, can reach with the starter " +
                 "rod. Better rods/tackle extend this later (per-gear data — the P4 upgrade you feel).")]
        [Min(0f)] public float MaxCastDistanceMetres;

        [Tooltip("How fast the cast line flies out (m/s) once released — pacing for the line-in-flight " +
                 "beat between the flick and the splash-down. Feel only; distance is decided at release.")]
        [Min(0.01f)] public float LineFlightMetresPerSec;

        /// <summary>The forgiving-cove reference tuning: a ~0.6 m wind-back to cast at all, the full 12 m
        /// range from a 4 m draw-back (about a quarter of the on-foot screen — deliberate but easy), the
        /// whole of it delivered by a brisk 12 m/s sweep, a limp sweep still throwing a quarter of what it
        /// aimed at, and nothing shorter than 1.5 m.</summary>
        public static FlickCastSettings Default => new FlickCastSettings
        {
            MinWindBackMetres = 0.6f,
            MinFlickLengthMetres = 0.4f,
            FullRangeWindBackMetres = 4f,
            FullSnapFlickSpeed = 12f,
            LimpFlickFraction01 = 0.25f,
            MinCastMetres = 1.5f,
            MaxCastDistanceMetres = 12f,
            LineFlightMetresPerSec = 18f,
        };
    }

    /// <summary>
    /// The owner-tunable feel of a <b>mooring line</b> (<see cref="GameConfig.MooringLine"/> — M2-38,
    /// design/deck-boarding-cleats-and-interact-capture.md §3). Lives in Core beside the config it rides
    /// on, the same Core-policy / feature-consumer split as <see cref="FlickCastSettings"/>: the pure
    /// maths that consumes it (<see cref="MooringLineMath"/>) is fed these numbers, and the Boats/Player
    /// consumers read them off the shared config each time they work a line.
    ///
    /// <para><b>The dial that matters is the scope range against a region's real DROP</b> — which is the
    /// tidal range PLUS how high the wharf stands, not the tidal range alone. St Peters is the worked
    /// example and it is a taller pier than it looks: its deck is measured at <b>+5.35 m</b> above datum
    /// and the tide swings <b>±2.2 m</b> (the 2026-08-01 pacing ruling), so the gap from a bollard down to
    /// a small hull's cleat runs from ~2.6 m at high water to <b>~7.0 m at low</b>. A line has to cover
    /// that vertically before it reaches across the water at all. Hence the defaults below:
    /// <list type="bullet">
    ///   <item><b>9 m</b> to start — she rides the whole ebb, swinging ~8.6 m at high water and ~5.7 m at
    ///   low. The boat is visibly drawn in as the water goes, which is the tell, but she is never hung.</item>
    ///   <item><b>Snug her to ~4 m</b> at high water and it looks perfect — and the ebb collects on it.
    ///   That is the lesson, and it is the player's own choice that sets it up.</item>
    ///   <item><b>16 m</b> at the top so a big hull on a spring tide still has an answer.</item>
    /// </list>
    /// Re-tune these when a region's wharf height or tide amplitude changes, or the gradient flattens and
    /// "mind the tide" becomes scenery. <c>MooringLineMathTests</c> pins that gradient against St Peters'
    /// actual numbers.</para>
    ///
    /// <para><b>Not here on purpose:</b> rope damage, breaking strain and multi-line rafting. V1's failure
    /// is the loop SLIPPING (<see cref="WorkingLoadFactor"/>) and the boat going quietly adrift — the
    /// cozy fail the backlog names. A parting rope is a different, harsher feature and a separate call.</para>
    /// </summary>
    [System.Serializable]
    public struct MooringLineSettings
    {
        [Tooltip("How close (m) you must stand to a cleat to work it — start a toss from it, or tighten, " +
                 "slacken and cast off a line already made fast to it. Roughly arm's reach: you are " +
                 "handling the fitting, not gesturing at it from across the deck.")]
        [Min(0f)] public float CleatReachMetres;

        [Tooltip("How near the far cleat the toss must LAND (m) for the loop to catch. This is the whole " +
                 "skill of the throw — the flick-cast decides where the line lands, and this decides " +
                 "whether that was good enough. Larger = kinder. Miss and the line simply falls in the " +
                 "water: coil it and try again, no penalty (cozy fail).")]
        [Min(0f)] public float TossCatchRadiusMetres;

        [Tooltip("SCOPE the line starts at (m) when it first catches — a sensible working length before " +
                 "the player has tightened or slackened anything.")]
        [Min(0f)] public float DefaultScopeMetres;

        [Tooltip("Shortest scope (m) you can haul a line in to. Above zero: a line hauled to nothing " +
                 "would pin the boat rigidly against the wharf, which is neither seamanlike nor a thing " +
                 "the constraint should have to express.")]
        [Min(0f)] public float MinScopeMetres;

        [Tooltip("Longest scope (m) you can pay out. THE tide dial — see the struct doc: this must be " +
                 "comfortably larger than the region's tidal range or a short line is never a mistake, " +
                 "and never so large that scope stops being a decision.")]
        [Min(0f)] public float MaxScopeMetres;

        [Tooltip("How much line (m) one press of tighten/slacken pays out or hauls in. Stepped rather " +
                 "than continuous so the player can COUNT the scope they are giving the tide, and so a " +
                 "keypress is a decision rather than a drag.")]
        [Min(0.01f)] public float ScopeStepMetres;

        [Tooltip("How far past bar-taut (×) the loop will be worked before it SLIPS off the cleat and the " +
                 "boat goes adrift. 1.0 = it surrenders the instant the line comes taut; 1.25 = it will " +
                 "take a quarter again its length of strain first. Keep above 1 — teeth should be earned " +
                 "by misjudging the tide, not by touching the water.")]
        [Min(1f)] public float WorkingLoadFactor;

        [Tooltip("How long (real seconds) the line must stay over its working load before the loop lets " +
                 "go. A grace period so a single wave that snatches the rope does not cast you off — it " +
                 "is a SUSTAINED overload (a tide that has run away from your scope) that loses the boat.")]
        [Min(0f)] public float SlipGraceSeconds;

        /// <summary>The St Peters reference tuning: arm's reach to a fitting, a forgiving 1.5 m catch on
        /// the throw, 9 m of scope to start, stepped by the metre between 2 m and 16 m, and a loop that
        /// takes a quarter again its length of strain for a couple of seconds before it surrenders. Sized
        /// against that pier's REAL drop (~2.6 m at high water, ~7.0 m at low — see the struct doc), so
        /// the starting line rides an ordinary ebb out and a deliberately snugged one does not.</summary>
        public static MooringLineSettings Default => new MooringLineSettings
        {
            CleatReachMetres = 1.5f,
            TossCatchRadiusMetres = 1.5f,
            DefaultScopeMetres = 9f,
            MinScopeMetres = 2f,
            MaxScopeMetres = 16f,
            ScopeStepMetres = 1f,
            WorkingLoadFactor = 1.25f,
            SlipGraceSeconds = 2f,
        };
    }

    /// <summary>
    /// The owner-tunable knobs of <b>ladder boarding</b> (<see cref="GameConfig.LadderBoarding"/>) — the
    /// tide-gap climb, and the geometry the rig's <c>ladderDown</c> clip was authored against.
    ///
    /// <para><b>Two of these are FEEL and the rest are MEASUREMENTS.</b>
    /// <see cref="BoardClampMetres"/> and <see cref="LadderReachMetres"/> are the owner's to tune. The
    /// four below them are the rig's and the wharf kit's own published numbers, exposed only so a future
    /// kit revision is a data edit rather than a code change — <b>changing them without a re-bake puts
    /// the fisher's feet between the rungs</b>, which is precisely what
    /// <c>LadderBoardingMathTests.TheStair_ReproducesTheBakedDescendTable</c> exists to catch.</para>
    /// </summary>
    [System.Serializable]
    public struct LadderBoardingSettings
    {
        [Tooltip("THE THRESHOLD. How far (m) the boat's deck may lie BELOW the wharf top and still be " +
                 "boarded with a step. Past this, boarding goes down a ladder instead.\n\n" +
                 "⚠ The art kit states two different numbers for this and they measure different things. " +
                 "1.2 m is what characterIsoRig6.js cites as where its 'board' clip soft-clamps, measured " +
                 "on the deck-to-WATER drop, and it is what ships here. wharfIsoRig.js:1103 implements a " +
                 "stricter 0.55 m on the deck-to-GUNWALE gap — which is the quantity this field actually " +
                 "compares, and which is also the rail height the one shipped 'board' sheet was baked at. " +
                 "So 1.2 is the generous reading: it keeps a step aboard available for the upper half of " +
                 "an ordinary tide (the sea has moods, and boarding should FEEL them), at the cost of " +
                 "letting the step clip stretch past the sheer it was drawn for. Dial it to 0.55 for the " +
                 "wharf kit's own stricter rule — one number, no code.")]
        [Min(0f)] public float BoardClampMetres;

        [Tooltip("How near (m) a ladder must be to where you are boarding for it to serve that berth. A " +
                 "wharf with no ladder within reach simply has no climb to offer: boarding falls back to " +
                 "the step, however deep the gap. Roughly the width of a berth, so the ladder mid-wall " +
                 "serves the boats lying either side of it and not the whole quay.")]
        [Min(0f)] public float LadderReachMetres;

        [Tooltip("MEASURED — the wharf kit's rung spacing (m), WharfIso.FIT.ladder.rung. The tread of " +
                 "the descent stair, and what the clip's foot placement was authored against.")]
        [Min(0.01f)] public float RungMetres;

        [Tooltip("MEASURED — the rig's standoff (m), ladderMount().standoff: how far the climber's pivot " +
                 "sits off the ladder plane. Seat the sprite on the ladder line instead and its hands go " +
                 "inside the wall. Not a nudge to taste.")]
        [Min(0f)] public float StandoffMetres;

        [Tooltip("MEASURED — one whole loop of the ladderDown clip (real seconds; 10 frames × 110 ms). " +
                 "The climb is NOT rate-scaled to a duration the way the boarding vault is: real rungs " +
                 "are a real distance apart, so a deeper gap takes longer rather than the same time " +
                 "faster. That is what makes the tide legible on the way down.")]
        [Min(0.01f)] public float ClimbLoopSeconds;

        [Tooltip("How long (real seconds) the unauthored TURN-AROUND at the top of the ladder is given — " +
                 "the moment the fisher swings off the wharf edge onto the top rung. The kit has no clip " +
                 "for it and says it is the gap players will notice, so it is covered with the authored " +
                 "'boardDown' step rather than a cut. Also covers the step OFF at the bottom onto the " +
                 "gunwale, which is the same authored motion.")]
        [Min(0f)] public float TransitionSeconds;

        /// <summary>The Nine Mile Creek reference tuning: a step aboard stays available until the boat's
        /// deck is 1.2 m below the planks, one ladder serves the berths within 4 m of it, and the climb
        /// runs the rig's own measured geometry at its own baked rate. Sized against that wharf's REAL
        /// numbers — a +3.0 m deck against a 2.2 m tide amplitude — so a dory is stepped onto around high
        /// water and climbed down to for the bottom half of the ebb.</summary>
        public static LadderBoardingSettings Default => new LadderBoardingSettings
        {
            BoardClampMetres = 1.2f,
            LadderReachMetres = 4f,
            RungMetres = LadderBoardingMath.RigRungMetres,
            StandoffMetres = LadderBoardingMath.RigStandoffMetres,
            ClimbLoopSeconds = LadderBoardingMath.RigLoopSeconds,
            TransitionSeconds = 0.45f,
        };
    }

    /// <summary>
    /// The owner-tunable knobs of the DISPLACED water surface (<see cref="GameConfig.DisplacedWater"/> —
    /// ADR 0023 phase 2 step 3), named and serializable so the sea's drama is dialled on the config asset
    /// with no code (rule 6). Lives in Core beside the config it rides on — the same Core-policy /
    /// feature-consumer split as <see cref="SeakeepingSettings"/> and <see cref="RodFightSettings"/>:
    /// Core cannot reference the Art module (rule 4), so the tunables live here and the Art-side
    /// consumers (<c>WaterSurface</c> / <c>DisplacedWaterSurface</c>) read them each throttled tick.
    ///
    /// <para><b>Lockstep (the twin discipline).</b> <see cref="Default"/> must equal the water shader's
    /// property defaults AND the Art-side twin constants (<c>WhitecapSalienceMath.Default*</c>) —
    /// config, shader and twin can never disagree silently. <c>DisplacedWaterConfigTests</c> pins all
    /// three sides; change any one only with the others, in the same commit.</para>
    ///
    /// <para><b>What is deliberately NOT here.</b> The four remaining salience properties
    /// (<c>_CapSolidMargin</c> / <c>_CapDitherBand</c> / <c>_EnvelopeBands</c> /
    /// <c>_EnvelopeBandDitherWin</c>) are STYLE constants of the band/dither language — they stay
    /// material-level on <c>Water.mat</c>. The per-coast shore gradient stays on each scene's
    /// <c>DisplacedWaterSurface</c> (it is terrain data, not world policy).</para>
    /// </summary>
    [System.Serializable]
    public struct DisplacedWaterSettings
    {
        [Tooltip("The SHARED displacement exaggeration (ADR 0023 §(2)): how much taller the sea DRAWS " +
                 "than it simulates. 1 = sim-true (already readable); the sweet spot is 1.5–2, and " +
                 "×1.5 (the default) is also provably shear-free at the coast; ×3 BREAKS the ¾-iso " +
                 "framing — crests visually detach from their troughs (spike-measured), so stay well " +
                 "under it. This ONE value drives the surface's lift AND (phase 3) hull heave, buoys " +
                 "and wake — everything rises on the same sea, never retuned apart.")]
        [Min(0f)] public float WaveExaggeration;

        [Tooltip("Safety coefficient of the DERIVED shore-fade band (band = coefficient × wave " +
                 "envelope × exaggeration × shore steepness). 2 (the default) is the proven tear-safe " +
                 "value. RAISING it widens the calm shallow band hugging every shore (safe, just " +
                 "calmer coasts); LOWERING it below ~1.5 risks the coast visibly TEARING — water " +
                 "drawn over dry sand at a crest. 1.5 is exactly marginal, so stay at 2 or above.")]
        [Min(0f)] public float ShoreBandCoefficient;

        [Tooltip("Master strength of the envelope whitecap salience (0..1): how strongly SOLID foam " +
                 "cores are reserved for the rare near-envelope wave. 1 (the default) = the full " +
                 "retune — everyday chop wears thin milky streaks and only the big one wears a solid " +
                 "core. 0 = the legacy look exactly: every crest capped with equal salience (the big " +
                 "wave hides in the speckle again).")]
        [Range(0f, 1f)] public float CapSalienceStrength;

        [Tooltip("Crest height — as a fraction of the sea's wave envelope (0..1) — where whitecap " +
                 "solid cores BEGIN. 0.62 (the default, spike-tuned) reserves cores for near-envelope " +
                 "waves: LOWER it and more everyday waves earn a solid core; RAISE it and cores get " +
                 "rarer still. Envelope-relative, so a bigger SEA does not fake a bigger WAVE.")]
        [Range(0f, 1f)] public float CapEnvelopeThreshold;

        [Tooltip("Strength of the envelope VALUE BANDS (0..1): the posterized light/dark stepping " +
                 "that marks tall water by SHADE before its foam (only a near-envelope crest can " +
                 "reach the top band). 0.35 is the default production blend; 0 = no envelope shading " +
                 "(the pre-retune look).")]
        [Range(0f, 1f)] public float EnvelopeBandStrength;

        [Tooltip("Is the DISPLACED sea the one the player sees? ON (the default since 2026-08-05) " +
                 "makes the vertex-displaced surface the game's water and hides the flat sprite face; " +
                 "OFF restores the flat face exactly, which is what shipped through ADR 0023 phase 3. " +
                 "The dev O key still flips it live either way — this only decides which side a scene " +
                 "STARTS on, so the owner can compare without a rebuild. Edit mode is unaffected and " +
                 "always shows the flat face: the displaced path is a Play instrument, and the coast " +
                 "is designed against the flat water (ADR 0014).")]
        public bool DefaultOn;

        /// <summary>
        /// The ADR-cited defaults: ×1.5 exaggeration (the readability sweet spot, shear-free at the
        /// coast), the proven tear-safe band coefficient (<see cref="ShoreFadeMath.RecommendedBandCoefficient"/>),
        /// full envelope salience with the spike-tuned 0.62 threshold, and the production 0.35 band
        /// blend. Pinned equal to the shader property defaults and the Art twin constants by
        /// <c>DisplacedWaterConfigTests</c>.
        ///
        /// <para><b><see cref="DefaultOn"/> is true</b> as of 2026-08-05 — the displaced sea becomes
        /// the game's water. ⚠️ That default reaches a SCENE only if the config asset actually
        /// serializes the key: a struct field absent from the YAML deserializes to <c>false</c>, not
        /// to this property, so <c>GameConfig.asset</c> carries <c>DefaultOn: 1</c> explicitly and
        /// <c>DisplacedDefaultTests</c> asserts that it does. This is the standing "the asset lags the
        /// code" trap in its most dangerous shape — a silent revert that looks like nothing changed.</para>
        /// </summary>
        public static DisplacedWaterSettings Default => new DisplacedWaterSettings
        {
            WaveExaggeration = 1.5f,
            ShoreBandCoefficient = ShoreFadeMath.RecommendedBandCoefficient,
            CapSalienceStrength = 1f,
            CapEnvelopeThreshold = 0.62f,
            EnvelopeBandStrength = 0.35f,
            DefaultOn = true,
        };
    }

    /// <summary>
    /// Tunables for the <b>depth drop</b> — Rod Fishing v2's weighted-rig fall, the slack "bottom" tell,
    /// and the depth-targeted catch weighting (<c>docs/design/rod-fishing-v2-brainstorm.md</c> §2.1/§2.3/§6;
    /// <see cref="GameConfig.DepthDrop"/>). Lives in Core beside the config it rides on, exactly like
    /// <see cref="RodFightSettings"/>: Core cannot reference the Fishing module (rule 4), so the tunables
    /// live here as plain numbers and the pure Fishing-side maths that consumes them
    /// (<c>DepthDropMath</c>) takes them as parameters.
    ///
    /// <para><b>The read is diegetic (owner's call, decision #4):</b> there is no depth gauge. The player
    /// COUNTS THE FALL — a heavier rig sinks faster — and FEELS the floor when the line goes slack. Every
    /// field here shapes that read or the catch weighting behind it; none of them draws a number on
    /// screen.</para>
    /// </summary>
    [System.Serializable]
    public struct DepthDropSettings
    {
        // ---- the fall (the count-the-fall depth read) --------------------------------------------

        [Tooltip("Extra sink speed per kilogram of rig weight (m/s per kg). THE tactical knob: a heavy jig " +
                 "reaches the deep band quickly, a light rig sinks slowly and fishes the mid-column longer. " +
                 "Bigger = weight matters more.")]
        [Min(0f)] public float SinkSpeedPerKgMps;

        [Tooltip("Slowest a rig ever sinks (m/s) — even a bare hook goes down eventually. Keeps a featherweight " +
                 "rig from hanging forever.")]
        [Min(0f)] public float MinSinkSpeedMps;

        [Tooltip("Fastest a rig ever sinks (m/s) — the heaviest lead still falls like a lure, not a brick. " +
                 "Caps how much the count-the-fall read can be shortcut.")]
        [Min(0f)] public float MaxSinkSpeedMps;

        // ---- the reachable band ------------------------------------------------------------------

        [Tooltip("How much line the reel carries (m) — the deepest the rig can EVER go, even over deeper " +
                 "water. The floor of the reachable band is the shallower of this and the seabed. Gear " +
                 "upgrades can extend it later.")]
        [Min(0f)] public float MaxLineMeters;

        [Tooltip("The bottom-fishing SWEET SPOT: how far above the floor (m) still counts as 'just off the " +
                 "bottom'. Bottom out, then reel up within this window to target bottom fish. Sitting ON the " +
                 "floor (line slack) is outside the window — the lift is the skill beat.")]
        [Min(0f)] public float BottomSweetWindowMeters;

        [Tooltip("How fast holding the action reels the rig UP (m/s) while waiting — the 'reel up slightly' " +
                 "move that lifts a bottomed rig into the sweet window.")]
        [Min(0f)] public float ReelUpMps;

        [Tooltip("A handline rigged with at least this much weight (kg) fishes the DEPTH branch (drop and " +
                 "read the column) instead of the cast/bobber branch. Jigging and longline gear always fish " +
                 "the depth branch; nets/traps never do.")]
        [Min(0f)] public float WeightedHandlineMinKg;

        // ---- the fishing depth zones (metres — where each kind of fish lives) ---------------------

        [Tooltip("Held depths down to this (m) read as TIDEPOOL water — the shore scraps.")]
        [Min(0f)] public float TidepoolMaxMeters;

        [Tooltip("Held depths down to this (m) read as the SHALLOWS.")]
        [Min(0f)] public float ShallowsMaxMeters;

        [Tooltip("Held depths down to this (m) read as INSHORE water.")]
        [Min(0f)] public float InshoreMaxMeters;

        [Tooltip("Held depths down to this (m) read as MIDWATER — stop the drop mid-column to fish it.")]
        [Min(0f)] public float MidwaterMaxMeters;

        [Tooltip("Held depths down to this (m) read as DEEP water; anything deeper is ABYSSAL.")]
        [Min(0f)] public float DeepMaxMeters;

        // ---- the catch weighting (depth as the species-targeting tactic) --------------------------

        [Tooltip("Catch-weight multiplier for a species whose preferred depth zones INCLUDE the zone you're " +
                 "holding in (≥ 1). Bigger = choosing the right depth pays off more.")]
        [Min(1f)] public float InBandAffinity;

        [Tooltip("Catch-weight multiplier for a species you're holding OUTSIDE its preferred zones (0..1). " +
                 "Kept above zero on purpose: depth is a weight, never a wall — the wrong depth makes a fish " +
                 "unlikely, not impossible.")]
        [Range(0.01f, 1f)] public float OffBandAffinity;

        [Tooltip("EXTRA catch-weight multiplier for a BOTTOM species while the rig is held just off the floor " +
                 "(inside the sweet window). The payoff for bottoming out and lifting slightly (≥ 1).")]
        [Min(1f)] public float BottomWindowAffinity;

        /// <summary>
        /// The forgiving-cove reference tuning: a 0.2 kg rig sinks ~0.9 m/s (a countable ~11 s to 10 m), a
        /// 1 kg jig ~2.5 m/s (the heavy shortcut), 60 m of line, a 1 m off-floor sweet window, and a
        /// clear-but-gentle ×2 zone / ×2.5 bottom-window weighting over a ×0.5 off-zone damp.
        /// </summary>
        public static DepthDropSettings Default => new DepthDropSettings
        {
            SinkSpeedPerKgMps = 2.0f,
            MinSinkSpeedMps = 0.5f,
            MaxSinkSpeedMps = 3.5f,
            MaxLineMeters = 60f,
            BottomSweetWindowMeters = 1.0f,
            ReelUpMps = 1.5f,
            WeightedHandlineMinKg = 0.2f,
            TidepoolMaxMeters = 0.6f,
            ShallowsMaxMeters = 3f,
            InshoreMaxMeters = 10f,
            MidwaterMaxMeters = 30f,
            DeepMaxMeters = 90f,
            InBandAffinity = 2.0f,
            OffBandAffinity = 0.5f,
            BottomWindowAffinity = 2.5f,
        };
    }

    /// <summary>
    /// THE STRIKE (<see cref="GameConfig.Strike"/> — owner drop §10.2). His words were "pull back and
    /// press maybe?", explicitly tentative — so BOTH candidates ship, each behind its own toggle, and
    /// he picks (or keeps both) by feel with no code change (rule 6). The press is the plain action
    /// edge; the pull-back is a committed haul of the pointer AWAY from the bobber — the mirror of
    /// the flick-cast's forward sweep. Consumed by the Fishing-side <c>StrikeYank</c>/<c>
    /// StrikeGestureMath</c> (the Core-policy / feature-consumer split, the RodFightSettings shape).
    /// </summary>
    [System.Serializable]
    public struct StrikeSettings
    {
        [Tooltip("The PRESS candidate: the action press strikes (today's input, and the gamepad-safe " +
                 "one). Turn off to feel the pull-back alone.")]
        public bool PressStrikes;

        [Tooltip("The PULL-BACK candidate: hauling the pointer away from the bobber strikes — cast " +
                 "forward, strike back. Turn off to feel the press alone.")]
        public bool PullBackStrikes;

        [Min(0f)]
        [Tooltip("How fast (m/s, world) the pointer must move AWAY from the bobber for the motion to " +
                 "count as hauling. Below this it's repositioning, not striking. The on-foot view is " +
                 "~16 m wide — 6 m/s is a deliberate wrist-snap, not a drift.")]
        public float YankMinSpeedMps;

        [Min(0f)]
        [Tooltip("Backward travel (m, world) the haul must bank at qualifying speed before it fires. " +
                 "Bigger = a fuller arm movement; smaller = a flick of the wrist.")]
        public float YankMinMetres;

        [Range(0f, 1f)]
        [Tooltip("Below this fraction of the qualifying speed the haul STALLS and its banked travel " +
                 "is forfeit — a yank is one committed motion, not a slow wander that eventually " +
                 "adds up.")]
        public float YankStallFraction01;

        /// <summary>Both candidates on (the owner picks in play); a brisk 6 m/s haul, 0.8 m of it,
        /// breaking below a quarter of qualifying speed.</summary>
        public static StrikeSettings Default => new StrikeSettings
        {
            PressStrikes = true,
            PullBackStrikes = true,
            YankMinSpeedMps = 6f,
            YankMinMetres = 0.8f,
            YankStallFraction01 = 0.25f,
        };
    }

    /// <summary>
    /// FRESHNESS &amp; ROT (<see cref="GameConfig.Freshness"/> — M1 §7.3), the owner-tunable dials of
    /// the freshness clock, named and serializable so rot is tuned on the config asset with no code
    /// (rule 6). The same Core-policy / feature-consumer split as <see cref="RodFightSettings"/>: the
    /// pure maths that consumes them is <see cref="Freshness"/> (Core), fed a <see cref="SpoilPolicy"/>
    /// built from these fields via <see cref="Policy"/>.
    ///
    /// <para><b><see cref="Default"/> must mirror <see cref="SpoilPolicy.Default"/></b> — the shipped
    /// GameConfig.asset predates this block, so the C# defaults here ARE the live game until the owner
    /// re-saves the asset; keeping them equal to the Core policy default means the maths and the config
    /// can never disagree silently (test-pinned).</para>
    /// </summary>
    [System.Serializable]
    public struct FreshnessSettings
    {
        [Tooltip("Rate scale while a catch sits IN THE OPEN (a bucket on the wharf, a bare hold). " +
                 "1 = the species' own SpoilPerDay; this is the reference rate the cold modes beat.")]
        [Min(0f)] public float AmbientRateMultiplier;

        [Tooltip("Rate scale while KEPT ALIVE (shellfish in a wet bucket). 0 = fully arrested — the " +
                 "M1 default; raise it later to give live holding a slow attrition without touching code.")]
        [Min(0f)] public float LiveRateMultiplier;

        [Tooltip("Rate scale while FROZEN (Ginny's freezer). 0 = fully arrested; the freezer never " +
                 "runs out — its limit is WHERE it is (home), not how long it holds.")]
        [Min(0f)] public float FrozenRateMultiplier;

        [Tooltip("Rate scale while ON ICE and the ice still holds. 0 = ice stops rot dead while it " +
                 "lasts; raise it slightly to make ice a strong slow rather than a stop. What makes ice " +
                 "interesting either way is that it RUNS OUT — the melt lives on the container Def.")]
        [Min(0f)] public float IcedRateMultiplier;

        [Tooltip("The spoil (0..1) at which NO buyer will take a catch at any price — it is rubbish, " +
                 "still occupying hold space until dumped. 0.9 = refused while visibly off but before " +
                 "liquid; 1 = sellable right up to the instant it is gone.")]
        [Range(0f, 1f)] public float UnsellableSpoil;

        /// <summary>These dials as the Core <see cref="SpoilPolicy"/> the freshness maths consumes.</summary>
        public SpoilPolicy Policy => new SpoilPolicy(AmbientRateMultiplier, LiveRateMultiplier,
                                                     FrozenRateMultiplier, IcedRateMultiplier,
                                                     UnsellableSpoil);

        /// <summary>Mirrors <see cref="SpoilPolicy.Default"/> exactly (see the struct remarks).</summary>
        public static FreshnessSettings Default => new FreshnessSettings
        {
            AmbientRateMultiplier = 1f,
            LiveRateMultiplier    = 0f,
            FrozenRateMultiplier  = 0f,
            IcedRateMultiplier    = 0f,
            UnsellableSpoil       = 0.9f,
        };
    }

    /// <summary>
    /// One entry of the pot starter kit (<see cref="GameConfig.StarterPotKit"/>): a trap kind by stable
    /// TrapDef id, and how many the kit grants. Plain serializable data so the owner tunes the kit on
    /// the GameConfig asset — no code, no scene rebuild (rule 6).
    /// </summary>
    [System.Serializable]
    public struct PotStarterEntry
    {
        [Tooltip("Stable TrapDef id to grant (e.g. \"trap.lobster\"). Must name an authored TrapDef " +
                 "(content validation checks this).")]
        public string TrapDefId;

        [Min(0)]
        [Tooltip("How many of this pot the starter kit grants. 0 disables the entry.")]
        public int Count;

        public PotStarterEntry(string trapDefId, int count)
        {
            TrapDefId = trapDefId;
            Count = count;
        }
    }

    /// <summary>
    /// THE WHEEL IS THE PLAYER'S EYE (<see cref="GameConfig.PlayerZoom"/> — owner ruling 2026-08-19):
    /// <i>"Mouse wheel modifies player zoom — closer to look at interiors, out when outside."</i>
    ///
    /// <para><b>A range, not a free zoom.</b> Pixel art shimmers at a fractional camera scale, so the
    /// camera only ever stops on the integer pixel-perfect steps the follow-cam's ladder defines. These
    /// two heights name the ENDS of the walking player's stretch of that ladder; the wheel walks the
    /// whole-number stops between them and nothing in between exists. Type any number you like — it
    /// quantises to the nearest crisp stop, so the worst a hand-typed value can do is pick a
    /// neighbouring tier, never a blurry framing.</para>
    ///
    /// <para><b>Metres, deliberately, and never step numbers.</b> Ladder steps count UPWARD as the view
    /// gets CLOSER, which reads backwards to anybody tuning a camera. Every other camera dial in the
    /// project is world height in metres and so is this one: smaller number = closer in. The reference
    /// stops at the locked PPU 32 on a 1080p screen are 33.75 / 16.88 / 11.25 / 8.44 / 6.75 / 5.63 /
    /// 4.82 / 4.22 m, and the walking defaults below pick the 11.25 → 5.63 band: one stop WIDER than
    /// today's on-foot framing at the far end, and at the near end the closest framing the game already
    /// ships (the step a live trap haul uses).</para>
    ///
    /// <para><b>These two clamps are the WALKER's range, and only hers.</b> Aboard and on deck the wheel
    /// works too (owner ruling 2026-08-22) but it does not use these heights — it steps within a band
    /// centred on the hull's own ruled framing, <see cref="AboardStopsCloser"/> stops in and
    /// <see cref="AboardStopsWider"/> stops out. That band is re-centred every time the ruled framing
    /// changes, so the hull's "whole vessel visible" derivation, the deck step and the haul tighten all
    /// remain the thing you are given each time you arrive at them; the wheel is a look around from
    /// there, never a new resting place. A live haul and a road vehicle stay ruled outright.</para>
    /// </summary>
    [System.Serializable]
    public struct PlayerZoomSettings
    {
        public const float ShippedDefaultWorldHeightMeters = 11.25f;

        [Tooltip("Walking world height before the wheel moves. It lands on the nearest crisp stop.")]
        [Min(0.5f)] public float DefaultWorldHeightMeters;

        [Tooltip("Let the mouse wheel (and the gamepad's shoulder buttons) change the walking view at " +
                 "all. Off = the on-foot framing is fixed, exactly as it was before this existed.")]
        public bool WheelEnabled;

        [Tooltip("CLOSEST the wheel may take the walking view, in metres of world height — the interior " +
                 "close-up. Smaller = further in. 5.625 is the tightest framing the game already " +
                 "ships (the live-haul step).")]
        [Min(0.5f)] public float ClosestWorldHeightMeters;

        [Tooltip("FARTHEST the wheel may take the walking view, in metres of world height. Larger = " +
                 "further out. 16.875 is one crisp stop wider than the default walking view — enough " +
                 "to see where you are going outdoors without the fisher shrinking to a speck.")]
        [Min(0.5f)] public float FarthestWorldHeightMeters;

        [Tooltip("How much scroll earns ONE tier, in the Input System's OWN units — which reports ONE " +
                 "per wheel detent on this project's setup (owner-measured 2026-08-26). The raw Win32 " +
                 "120-per-detent scale never reaches this code; shipping 120 here made a tier cost 120 " +
                 "clicks, a wheel dead to the hand. A trackpad reports a stream of fractions and has " +
                 "to travel the same distance to earn the same tier.")]
        [Min(1f)] public float WheelUnitsPerNotch;

        [Tooltip("Seconds to ease one tier step. 0 = snap. Either way the view LANDS on a crisp " +
                 "pixel-perfect stop — the ease only bridges the frames between two stops.")]
        [Min(0f)] public float StepSeconds;

        // ---- the ABOARD band (owner ruling 2026-08-22) -------------------------------------------
        //
        // ⚠️ COUNTS OF STOPS, and this is the one place in the camera's tuning that is not metres.
        // Every other dial is a world height because a ladder STEP INDEX reads backwards (it counts
        // up as the view gets closer). These two are neither: they are "how many stops either way",
        // and they cannot be metres because the thing they are measured from — the hull's own ruled
        // framing — is different for every vessel the player will ever own. A dory and a tanker share
        // an allowance of "two stops"; they share no pair of metre clamps at all.

        [Tooltip("How many crisp stops the wheel may take the view CLOSER than the ruled framing " +
                 "while aboard or on deck. 0 = the wheel cannot zoom in there at all. The band is " +
                 "centred on the hull's own framing and is re-centred every time that framing " +
                 "changes, so this is a look, never a new resting place.")]
        [Min(0)] public int AboardStopsCloser;

        [Tooltip("How many crisp stops the wheel may take the view WIDER than the ruled framing " +
                 "while aboard or on deck. 0 = the wheel cannot zoom out there at all. Separate from " +
                 "the closer allowance because the two answer different questions: in to read the " +
                 "deck, out to see the water you are crossing.")]
        [Min(0)] public int AboardStopsWider;

        /// <summary>
        /// The shipping range: 16.875 m out (one stop wider than the 11.25 m walking default) to 5.625 m in (the
        /// live-haul step, the closest framing already in the game), one input unit per tier,
        /// a short ease so a step reads as a move rather than a cut, and an aboard band of two stops
        /// either way around whatever the helm or the deck ruled.
        ///
        /// <para>⚠️ The range ends are the ×2 and ×6 PPU-32 stops at 1080p written out as literals,
        /// because the ladder that derives them lives in the App camera and Core may not reach it.
        /// <c>PlayerZoomTierTests</c> is the tripwire that fires if the ladder and these defaults ever
        /// drift apart.</para>
        /// </summary>
        public static PlayerZoomSettings Default => new PlayerZoomSettings
        {
            DefaultWorldHeightMeters = ShippedDefaultWorldHeightMeters,
            WheelEnabled = true,
            ClosestWorldHeightMeters = 5.625f,   // 1080 / (6 x 32)
            FarthestWorldHeightMeters = 16.875f, // 1080 / (2 x 32)
            WheelUnitsPerNotch = 1f,             // one detent, in Input System units — MEASURED, not Win32's 120
            StepSeconds = 0.18f,
            AboardStopsCloser = 2,               // two stops in from the hull's ruled framing…
            AboardStopsWider = 2,                // …and two out (owner ruling 2026-08-22)
        };

        /// <summary>
        /// True when NOTHING on this struct has been authored — every field sitting on its C# zero.
        ///
        /// <para><b>⚠️ This is a real shape, not a hypothetical.</b> A <c>GameConfig</c> asset
        /// serialized before <see cref="GameConfig.PlayerZoom"/> existed carries no YAML for it, and
        /// Unity deserializes the missing block as <c>default</c> — which reads as a wheel that is
        /// OFF with a range of 0 m to 0 m. Every one of those is a silent, plausible-looking answer:
        /// nothing throws, nothing logs, and the wheel simply does nothing forever. The
        /// <c>[Min]</c> attributes do not help, because they police the Inspector and not the
        /// deserializer.</para>
        ///
        /// <para><b>Why WheelEnabled alone is not the test.</b> An owner who deliberately turns the
        /// wheel off keeps the rest of their tuning; an unwritten struct has no tuning to keep. Asking
        /// whether the WHOLE struct is blank is what separates "off on purpose" from "never
        /// authored", so <see cref="Sanitized"/> can heal the second without ever overriding the
        /// first.</para>
        /// </summary>
        public bool IsUnauthored =>
            DefaultWorldHeightMeters <= 0f && !WheelEnabled && ClosestWorldHeightMeters <= 0f && FarthestWorldHeightMeters <= 0f
            && WheelUnitsPerNotch <= 0f && StepSeconds <= 0f
            && AboardStopsCloser == 0 && AboardStopsWider == 0;

        /// <summary>
        /// These settings with any UNSET value replaced by the shipped default — the read every
        /// consumer should make, so a config asset older than a field can never quietly pin the wheel.
        ///
        /// <para>Wholly blank (<see cref="IsUnauthored"/>) means the block was never written, and the
        /// answer is the shipped defaults entire. Otherwise only the values that cannot mean anything
        /// are healed: a clamp of 0 m is not a framing, and a notch size of 0 is not a wheel. Every
        /// authored value — <see cref="WheelEnabled"/> set to false very much included — is left
        /// exactly as the owner typed it, and the two stop allowances are left alone at zero because
        /// zero is a legitimate answer there ("the wheel does not move the helm").</para>
        /// </summary>
        public PlayerZoomSettings Sanitized()
        {
            if (IsUnauthored) return Default;

            PlayerZoomSettings d = Default;
            PlayerZoomSettings s = this;
            if (s.DefaultWorldHeightMeters <= 0f) s.DefaultWorldHeightMeters = d.DefaultWorldHeightMeters;
            if (s.ClosestWorldHeightMeters <= 0f) s.ClosestWorldHeightMeters = d.ClosestWorldHeightMeters;
            if (s.FarthestWorldHeightMeters <= 0f) s.FarthestWorldHeightMeters = d.FarthestWorldHeightMeters;
            if (s.WheelUnitsPerNotch <= 0f) s.WheelUnitsPerNotch = d.WheelUnitsPerNotch;
            if (s.StepSeconds < 0f) s.StepSeconds = d.StepSeconds;
            if (s.AboardStopsCloser < 0) s.AboardStopsCloser = 0;
            if (s.AboardStopsWider < 0) s.AboardStopsWider = 0;
            return s;
        }
    }

    /// <summary>
    /// WHAT'S ON THE HOOK (<see cref="GameConfig.BaitTackle"/>) — how much the right bait and the right
    /// tackle change what bites. Consumed by <c>CatchResolver.BaitAffinity</c> /
    /// <c>LureAffinity</c> as plain multipliers on the catch roll, the same Core-policy /
    /// feature-consumer split as <see cref="RodFightSettings"/>.
    ///
    /// <para><b>Weights, never walls</b> — the promise depth already makes. The wrong kit catches LESS;
    /// it never catches NOTHING. That is what lets a beginner with a bare spoon still fill a bucket
    /// while an angler who has learned the pairings fills it faster and with what they were after.</para>
    ///
    /// <para><b>Bait is precise, tackle is broad.</b> The wrong bait is damped harder than the wrong
    /// lure, because a fish that wants food will refuse the wrong food outright, while a curious fish
    /// will still occasionally hit a lure it doesn't love. That asymmetry is the whole reason to carry
    /// both: bait to TARGET, tackle to COVER.</para>
    /// </summary>
    [System.Serializable]
    public struct BaitTackleSettings
    {
        [Tooltip("How much likelier a species is when the bait on the hook is one it wants (≥ 1; " +
                 "1 = bait doesn't matter). This is the strongest targeting tool in the game — bigger " +
                 "than the depth weight on purpose, because choosing bait is a deliberate act.")]
        [Min(1f)] public float BaitFavourBoost;

        [Tooltip("What's left of a species' chance when the bait is one it does NOT want (0..1; " +
                 "1 = no penalty). Low: the wrong bait is a real mistake. Never 0 — a haddock will " +
                 "still occasionally take a squid strip meant for cod.")]
        [Range(0f, 1f)] public float WrongBaitDamp01;

        [Tooltip("How much likelier a species is when the tackle tied on is one it chases (≥ 1; " +
                 "1 = tackle doesn't matter). Smaller than the bait boost — tackle covers water, bait " +
                 "picks a fish.")]
        [Min(1f)] public float LureFavourBoost;

        [Tooltip("What's left of a species' chance on a lure it doesn't favour (0..1). Gentler than the " +
                 "wrong-bait damp: a fish will hit the wrong lure out of curiosity far more readily " +
                 "than it will eat the wrong food.")]
        [Range(0f, 1f)] public float WrongLureDamp01;

        /// <summary>The reference tuning: the right bait roughly triples a species' share and the wrong
        /// one cuts it to a third; the right tackle doubles and the wrong one costs a third. So a
        /// correctly-baited, correctly-tackled cast is worth about six times a badly-chosen one — a
        /// decision you can feel — while nothing is ever off the table.</summary>
        public static BaitTackleSettings Default => new BaitTackleSettings
        {
            BaitFavourBoost = 3f,
            WrongBaitDamp01 = 0.35f,
            LureFavourBoost = 2f,
            WrongLureDamp01 = 0.65f,
        };
    }

    /// <summary>
    /// WORKING THE LURE (<see cref="GameConfig.Jigging"/> — owner's ask 2026-07-25). Five hand-feels,
    /// tuned in ONE place rather than restated on every tackle asset: a piece of tackle just names its
    /// <c>JigStyle</c> and the numbers live here, so the owner can re-feel all five without touching
    /// content. The pure maths that consumes them is <c>JigMath</c> (Fishing-side).
    ///
    /// <para>Tempo is strokes per second (a stroke = one reversal of the hand: the top of a lift, the
    /// bottom of a drop). Stroke is how far the pointer travels between reversals, in world metres — so
    /// these read against the same on-screen scale the cast does (the on-foot view is about 16 m wide).</para>
    /// </summary>
    [System.Serializable]
    public struct JiggingSettings
    {
        [Header("The five actions (tempo = strokes/sec, stroke = world metres)")]
        [Tooltip("COD JIG — big slow heaves, then let it flutter back. She takes it on the drop.")]
        [Min(0f)] public float LiftAndDropTempo;
        [Min(0f)] public float LiftAndDropStroke;

        [Tooltip("MACKEREL FEATHERS — short fast twitches, like a shoal of fry breaking up.")]
        [Min(0f)] public float QuickJerksTempo;
        [Min(0f)] public float QuickJerksStroke;

        [Tooltip("SPOON — one long even sweep, wobbling steadily through the water.")]
        [Min(0f)] public float SteadySweepTempo;
        [Min(0f)] public float SteadySweepStroke;

        [Tooltip("SOFT BAIT — slow and small, with long pauses. Something dying, not something fleeing.")]
        [Min(0f)] public float SlowCrawlTempo;
        [Min(0f)] public float SlowCrawlStroke;

        [Tooltip("SPINNER — quick and continuous; the blade has to keep turning or it is just a weight.")]
        [Min(0f)] public float FastSteadyTempo;
        [Min(0f)] public float FastSteadyStroke;

        [Header("How forgiving, and how dead")]
        [Tooltip("How far off the target action you can be and still score well (0..1). Bigger = the " +
                 "actions are easy to find by feel. At 0.5 you do well anywhere from about two-thirds " +
                 "to one-and-a-half times the target.")]
        [Range(0.05f, 1f)] public float Tolerance01;

        [Tooltip("What fraction of its fishing a MOTIONLESS lure keeps (0..1). The owner's call was " +
                 "'nearly dead, but bait still fishes' — low enough that working it is obviously the " +
                 "point, above zero because nothing else in this fishing is a hard wall. Expressed as a " +
                 "much longer wait for a bite, so the player can SEE it rather than being quietly " +
                 "denied by a dice roll.")]
        [Range(0.01f, 1f)] public float DeadLureFraction01;

        /// <summary>The reference feel. The five actions are spread wide enough to be distinct in the
        /// hand — a lift-and-drop is a big movement twice a second, a spinner is small and five times a
        /// second — and a dead lure fishes at a tenth, so it waits roughly ten times as long.</summary>
        public static JiggingSettings Default => new JiggingSettings
        {
            LiftAndDropTempo = 0.8f,  LiftAndDropStroke = 1.6f,
            QuickJerksTempo  = 4.0f,  QuickJerksStroke  = 0.35f,
            SteadySweepTempo = 1.2f,  SteadySweepStroke = 1.1f,
            SlowCrawlTempo   = 0.5f,  SlowCrawlStroke   = 0.45f,
            FastSteadyTempo  = 5.0f,  FastSteadyStroke  = 0.6f,
            Tolerance01 = 0.5f,
            DeadLureFraction01 = 0.1f,
        };
    }

    /// <summary>
    /// THE SEA'S HAND IN THE CATCH (<see cref="GameConfig.SeaFishing"/> — owner's ruling 2026-07-25),
    /// the dials behind Pillar 1's arrival in the rod loop. The pure maths that consumes them is
    /// <c>SeaFightMath</c> (Fishing-side), the same Core-policy / feature-consumer split as
    /// <see cref="RodFightSettings"/>.
    ///
    /// <para><b>The trade.</b> Rough water makes fish bolder — so it fishes BETTER
    /// (<see cref="SeaBoldness01"/> quickens the bite, <see cref="SeaBigFishBias01"/> brings up the better
    /// fish) — while the swell works your line, so it fights HARDER
    /// (<see cref="SeaFightFactor"/>). Weather becomes a decision, not a tax.</para>
    ///
    /// <para><b>All three at 0 = weather-blind fishing</b>, bit-for-bit as the rod loop shipped before
    /// this change: a gale plays exactly like a flat calm. That is the off-switch and the A/B baseline.</para>
    /// </summary>
    [System.Serializable]
    public struct SeaFishingSettings
    {
        [Tooltip("THE COST. Extra tension per second (0..1-gauge/s) the SEA puts through your line at a " +
                 "full storm — the swell working the rod, a wave snatching the line. It grows with the " +
                 "SQUARE of the sea state, so a chop is barely felt and a real sea is dangerous. 0 = OFF " +
                 "(the fight ignores the weather). Guard-rail: keep it low enough that easing off still " +
                 "recovers in EVERYDAY weather (test-enforced up to SeaFightMath.CozySeaCeiling01) — " +
                 "above that line the sea is meant to be able to beat you.")]
        [Min(0f)] public float SeaFightFactor;

        [Tooltip("THE REWARD (bites). How much quicker fish bite at a full storm, 0..1 — broken water " +
                 "hides you and emboldens them. 0.5 = bites arrive in half the time in a storm. Grows " +
                 "LINEARLY (unlike the cost), so even a chop already fishes noticeably better. 0 = OFF.")]
        [Range(0f, 1f)] public float SeaBoldness01;

        [Tooltip("THE REWARD (size). How strongly a full storm favours the BIG ones, 0..1 — the better " +
                 "fish come up to feed in broken water. 1 = a storm catch sits at the very top of the " +
                 "species' weight range. This is what makes a hard sea worth fishing, and a storm fish a " +
                 "story. 0 = OFF (the plain uniform weight roll).")]
        [Range(0f, 1f)] public float SeaBigFishBias01;

        /// <summary>
        /// The reference tuning, solved against the shipped species rather than guessed. The cost squares
        /// away to almost nothing in everyday weather — at the cozy ceiling it adds only 0.087, which every
        /// authored personality absorbs with room to spare — yet reaches 0.35 in a full storm, enough to
        /// out-pressure the ease and genuinely take the three STRONG fish (cod, pollock, haddock). The
        /// mackerel is deliberately left un-toothed even in a gale: a small fish becoming unlandable in
        /// weather would be frustration, not danger. The rewards arrive earlier and more gently — a storm
        /// bites about a third quicker and leans the size roll halfway up the range.
        /// </summary>
        public static SeaFishingSettings Default => new SeaFishingSettings
        {
            SeaFightFactor = 0.35f,
            SeaBoldness01 = 0.35f,
            SeaBigFishBias01 = 0.5f,
        };
    }

    /// <summary>
    /// HOW BIG SHE FEELS (<see cref="GameConfig.FishFeel"/> — owner's ask 2026-11-13: "rods should bend
    /// and react to fish, size … woah its something big!!"). The dials behind <see cref="FishFeelMath"/>,
    /// which the rod presenter, the fight camera and the fishing audio read. "Size" everywhere below is
    /// the hooked fish's weight as a 0..1 place in HER OWN species' range (a 9 kg cod is big; a 0.9 kg
    /// mackerel is big too).
    ///
    /// <para><b>Feedback, never balance.</b> No dial here is read by the fight's rules.</para>
    /// </summary>
    [System.Serializable]
    public struct FishFeelSettings
    {
        [Tooltip("THE BIG ONE. Size (0..1 of her species' range) at or above which a hooked fish is 'a " +
                 "big one': the hook-set slams harder, the camera kicks harder, the strain voice screams, " +
                 "she says 'Woah', and the landing line calls it a heavy one. 0.8 = the top fifth.")]
        [Range(0f, 1f)] public float BigOneThreshold01;
        [Tooltip("Landing line: below this size she is 'a small one'.")]
        [Range(0f, 1f)] public float SizeSmallBelow01;
        [Tooltip("Landing line: from this size she is 'a good one' (below it, 'a fair one').")]
        [Range(0f, 1f)] public float SizeGoodFrom01;
        [Tooltip("Landing line: from this size she is 'a real monster' (never below the big-one line).")]
        [Range(0f, 1f)] public float SizeTrophyFrom01;

        [Tooltip("ROD BEND. How much of the fight's load the SMALLEST fish puts on the rod (her biggest " +
                 "sister puts all of it). Lower = small fish only nod the tip.")]
        [Range(0f, 1f)] public float BendSmallScale01;
        [Tooltip("ROD BEND. How bowed the rod STAYS for the heaviest fish even between her runs, 0..1 of " +
                 "full load (scaled by size). Higher = a big fish holds the rod down.")]
        [Range(0f, 1f)] public float BendHeavyFloor01;
        [Tooltip("ROD BEND. How much of that held bow remains while she is slack (the 'reel now' tell). " +
                 "Lower = the rod springs up more when she gives.")]
        [Range(0f, 1f)] public float BendSlackKeep01;
        [Tooltip("ROD BEND. The rod's maximum lean for the heaviest fish, as a multiple of the presenter's " +
                 "base lean (1 = size does not deepen the lean).")]
        [Min(1f)] public float BendHeavyLeanScale;
        [Tooltip("ROD BEND. The most the rod may rotate in total (lean + pulses + slam), degrees.")]
        [Range(0f, 90f)] public float MaxTotalLeanDegrees;

        [Tooltip("HEAD-SHAKES. Tip pulses per second for the smallest fish (quick rattle).")]
        [Min(0f)] public float ShakeHzSmall;
        [Tooltip("HEAD-SHAKES. Tip pulses per second for the heaviest fish (slow thumps).")]
        [Min(0f)] public float ShakeHzHeavy;
        [Tooltip("HEAD-SHAKES. Depth of each tip pulse for the smallest fish, degrees.")]
        [Min(0f)] public float ShakeDegreesSmall;
        [Tooltip("HEAD-SHAKES. Depth of each tip pulse for the heaviest fish, degrees.")]
        [Min(0f)] public float ShakeDegreesHeavy;
        [Tooltip("HEAD-SHAKES. How sharp each pulse is (1 = a soft nod, higher = a hard jab then rest).")]
        [Min(1f)] public float ShakeSharpness;

        [Tooltip("RUNS. How far her run drags the point the rod leans toward, world metres, for the " +
                 "heaviest fish — the tip follows her run direction.")]
        [Min(0f)] public float RunPullMetres;
        [Tooltip("RUNS. The fraction of the run pull and the turn kick the SMALLEST fish gets.")]
        [Range(0f, 1f)] public float RunPullSmall01;
        [Tooltip("RUNS. How quickly the rod follows a change in her run direction, per second. Higher = " +
                 "snappier swing.")]
        [Min(0.1f)] public float RunSwingPerSec;
        [Tooltip("TURNS. Two run directions whose cosine is below this count as a turn (0.3 = more than " +
                 "about 70 degrees apart).")]
        [Range(-1f, 1f)] public float TurnCosine;
        [Tooltip("TURNS. The tip kick a turn throws for the heaviest fish, degrees.")]
        [Min(0f)] public float TurnKickDegrees;
        [Tooltip("TURNS. How long the turn kick takes to settle, seconds.")]
        [Min(0f)] public float TurnKickSeconds;

        [Tooltip("HOOK-SET. The rod slam the instant she is hooked, for the heaviest ordinary fish, degrees.")]
        [Min(0f)] public float HookSlamDegrees;
        [Tooltip("HOOK-SET. The fraction of the slam the SMALLEST fish gets.")]
        [Range(0f, 1f)] public float HookSlamSmall01;
        [Tooltip("HOOK-SET. How long the slam takes to spring back, seconds.")]
        [Min(0f)] public float HookSlamSeconds;
        [Tooltip("HOOK-SET. A big one slams this many times harder.")]
        [Min(1f)] public float BigOneSlamScale;
        [Tooltip("HOOK-SET. Camera kick multiplier for the smallest fish.")]
        [Min(0f)] public float HookKickSmallScale;
        [Tooltip("HOOK-SET. Camera kick multiplier for the heaviest fish.")]
        [Min(0f)] public float HookKickHeavyScale;
        [Tooltip("HOOK-SET. A big one multiplies the camera kick by this again.")]
        [Min(1f)] public float BigOneKickScale;

        [Tooltip("LINE. How tight the line stays for the heaviest fish while she is not slack, 0..1.")]
        [Range(0f, 1f)] public float LineHeavyTautFloor01;
        [Tooltip("LINE. Extra shudder (0..1 of full) the heaviest fish hums through the line at full effort.")]
        [Range(0f, 1f)] public float LineHumHeavy01;
        [Tooltip("LINE. The fraction of that hum left between her runs.")]
        [Range(0f, 1f)] public float LineHumIdle01;
        [Tooltip("LINE. How far the heaviest fish lifts the resting line toward white, 0..1 of the " +
                 "headroom. Keep it low: white still means 'ease off'.")]
        [Range(0f, 1f)] public float LineWhitenHeavy01;

        [Tooltip("SOUND. Strain-voice gain the heaviest fish keeps humming while she is on and not slack.")]
        [Range(0f, 1f)] public float StrainHumHeavyGain;
        [Tooltip("SOUND. How much deeper the strain voice is for the heaviest fish (0.2 = 20% lower).")]
        [Range(0f, 0.9f)] public float StrainPitchHeavyDrop01;
        [Tooltip("SOUND. The big one's hook-set scream: extra strain-voice gain at the instant she is hooked.")]
        [Range(0f, 1f)] public float HookScreamGain;
        [Tooltip("SOUND. Strain-voice pitch multiplier at the start of the scream (falls back to normal).")]
        [Min(1f)] public float HookScreamPitchScale;
        [Tooltip("SOUND. How long the scream lasts, seconds.")]
        [Min(0f)] public float HookScreamSeconds;

        [Tooltip("THE BIG ONE, ON THE FIGURE (owner's ask 2026-11-13: 'the player sprite should have an " +
                 "exclamation for a big fish too'). ONE SWITCH: on = an exclamation mark pops over the " +
                 "fisher the instant a big one is hooked, and the figure holds its hook-set pose a beat " +
                 "longer. Off = neither happens (her 'Woah' line is unaffected).")]
        public bool BigOneReactionEnabled;
        [Tooltip("EXCLAMATION. How long the mark stays up, seconds (pop-in and fade-out included).")]
        [Min(0f)] public float BigOneMarkSeconds;
        [Tooltip("EXCLAMATION. Where the mark's FOOT sits, metres from the figure's feet (x right, y up).")]
        public Vector2 BigOneMarkOffsetMetres;
        [Tooltip("EXCLAMATION. How tall the mark is once settled, metres.")]
        [Min(0f)] public float BigOneMarkHeightMetres;
        [Tooltip("EXCLAMATION. The pop: it grows from nothing to its overshoot over this many seconds, " +
                 "then settles to its real size over the same again.")]
        [Min(0f)] public float BigOneMarkPopSeconds;
        [Tooltip("EXCLAMATION. The pop's overshoot, as a multiple of the settled size (1 = no overshoot).")]
        [Min(1f)] public float BigOneMarkPopScale;
        [Tooltip("EXCLAMATION. How long the mark takes to fade out at the end of its time, seconds.")]
        [Min(0f)] public float BigOneMarkFadeSeconds;
        [Tooltip("EXCLAMATION. The mark's colour (its one-pixel outline stays dark).")]
        public Color BigOneMarkColor;
        [Tooltip("FIGURE. Extra seconds the fisher holds the hook-set (strike) pose for a big one before " +
                 "the reel cycle takes over. 0 = the ordinary strike.")]
        [Min(0f)] public float BigOneStrikeHoldSeconds;

        [Tooltip("ON THE LINE, SIDE-ON. A surfaced fish travelling within this many degrees of the line " +
                 "(toward the fisher or away) is drawn ACROSS the line, showing her flank, instead of " +
                 "end-on down it. The player and every villager angler share it. 0 = off (her travel alone " +
                 "picks the picture).")]
        [Range(0f, 90f)] public float FlankWithinLineDegrees;

        /// <summary>The reference feel — a first pass for the owner's plate, not a measured tuning.</summary>
        public static FishFeelSettings Default => new FishFeelSettings
        {
            BigOneThreshold01 = 0.8f,
            SizeSmallBelow01 = 0.25f,
            SizeGoodFrom01 = 0.55f,
            SizeTrophyFrom01 = 0.95f,
            BendSmallScale01 = 0.55f,
            BendHeavyFloor01 = 0.6f,
            BendSlackKeep01 = 0.4f,
            BendHeavyLeanScale = 1.6f,
            MaxTotalLeanDegrees = 40f,
            ShakeHzSmall = 7f,
            ShakeHzHeavy = 2.2f,
            ShakeDegreesSmall = 1.5f,
            ShakeDegreesHeavy = 7f,
            ShakeSharpness = 3f,
            RunPullMetres = 0.9f,
            RunPullSmall01 = 0.35f,
            RunSwingPerSec = 9f,
            TurnCosine = 0.3f,
            TurnKickDegrees = 5f,
            TurnKickSeconds = 0.25f,
            HookSlamDegrees = 10f,
            HookSlamSmall01 = 0.35f,
            HookSlamSeconds = 0.45f,
            BigOneSlamScale = 1.6f,
            HookKickSmallScale = 0.7f,
            HookKickHeavyScale = 1.5f,
            BigOneKickScale = 1.4f,
            LineHeavyTautFloor01 = 0.85f,
            LineHumHeavy01 = 0.35f,
            LineHumIdle01 = 0.5f,
            LineWhitenHeavy01 = 0.2f,
            StrainHumHeavyGain = 0.15f,
            StrainPitchHeavyDrop01 = 0.18f,
            HookScreamGain = 0.9f,
            HookScreamPitchScale = 1.6f,
            HookScreamSeconds = 0.8f,
            BigOneReactionEnabled = true,
            BigOneMarkSeconds = 1.2f,
            BigOneMarkOffsetMetres = new Vector2(0f, 1.7f),
            BigOneMarkHeightMetres = 0.38f,
            BigOneMarkPopSeconds = 0.09f,
            BigOneMarkPopScale = 1.4f,
            BigOneMarkFadeSeconds = 0.25f,
            BigOneMarkColor = new Color(1f, 0.85f, 0.2f, 1f),
            BigOneStrikeHoldSeconds = 0.35f,
            FlankWithinLineDegrees = 30f,
        };
    }

    /// <summary>
    /// WHERE THE FISH ARE (<see cref="GameConfig.FishSchools"/> — ADR 0025 S3, the owner's ruling of
    /// 2026-08-03). The dials behind the deterministic school sim: <c>FishSchoolMath</c> /
    /// <c>FishSchoolModel</c> (Fishing-side) consume them, the same Core-policy / feature-consumer split
    /// as <see cref="SeaFishingSettings"/> and <see cref="RodFightSettings"/>.
    ///
    /// <para><b>One model, two readers — so these numbers move BOTH at once.</b> The fish finder draws
    /// the schools these dials produce, and the fishing path raises the bite rate and weights the species
    /// roll from the SAME schools. There is deliberately no second "bite rate" number here: the density
    /// (<see cref="MinMarks"/>/<see cref="MaxMarks"/>) is both how many fish the glass draws and the
    /// expected bite rate, mapped by <see cref="BiteRatePerMark"/>. Tune the picture and you have tuned
    /// the fishing; they cannot drift apart.</para>
    ///
    /// <para><b>The owner's shape.</b> Fish are found in AREAS (<see cref="MinRadiusMetres"/>..
    /// <see cref="MaxRadiusMetres"/>), for a WHILE (<see cref="MinWindowHours"/>..
    /// <see cref="MaxWindowHours"/>), at a DEPTH (<see cref="MinDepthFraction01"/>..
    /// <see cref="MaxDepthFraction01"/> of the water column there). Whether a patch holds fish at all is
    /// decided by LOCATION (<see cref="CellSizeMetres"/> + <see cref="MinWaterColumnMetres"/>), WEATHER
    /// (<see cref="SeaStateAppearanceBias"/>) and DATE (the four season multipliers) — and those same
    /// three pick WHICH species are down there, weather by pushing the school deeper in a blow
    /// (<see cref="SeaStateDepthBias01"/>, which changes the depth band and so the species that live in
    /// it), date through each species' own authored season window.</para>
    ///
    /// <para><b>Recomputed, never saved</b> (rule 5) — schools are a function of
    /// <c>(worldSeed, gameTime, place, weather, season)</c> exactly as the tide and the wind are. Change
    /// any dial here and every school in the world changes with it, in an existing save, with no
    /// migration: there is nothing about a school on <c>SaveData</c> to be stale.</para>
    ///
    /// <para><b>The off switch.</b> <see cref="BaseAppearanceChance01"/> = 0 is an empty sea: no schools,
    /// no marks, and the catch roll is bit-for-bit the one that shipped before this existed (every
    /// school term is neutral with no school present). That is the A/B baseline.</para>
    /// </summary>
    [System.Serializable]
    public struct FishSchoolSettings
    {
        // ---- the lattice (WHERE and WHEN a patch of water is even asked the question) ---------------

        [Tooltip("Grain of the school sim (m): the world is diced into cells this wide, and each cell " +
                 "independently holds a school or doesn't. Smaller = fish are found in more, smaller " +
                 "pockets and the sea reads busier; larger = long empty runs between good ground. " +
                 "⚠ Also the hard cap on MaxRadiusMetres (a school never spills more than one cell), so " +
                 "raising the radius means raising this first.")]
        [Min(1f)] public float CellSizeMetres;

        [Tooltip("How often (in-game HOURS) a cell re-rolls — the fish move on and a new lot may show " +
                 "up. Each school's window lives inside one of these slots, so this is also the longest " +
                 "a window can be. Shorter = a restless sea you must keep re-reading; longer = ground " +
                 "that stays good long enough to be worth remembering.")]
        [Min(0.05f)] public float SlotHours;

        /// <summary>
        /// The rig's own shoal loop radius in metres — <c>fishIsoRig2.js</c> authors <c>shoal</c> with
        /// <c>radius 1.2</c>, and <c>ShoalMath.DefaultRadiusMetres</c> is the port of that same number.
        /// Restated here as a literal because Core may not reference the Fishing module (rule 4); a
        /// guard in the Fishing tests pins the two together so they cannot drift apart in silence.
        /// </summary>
        public const float ShoalMathReferenceSpreadMetres = 1.2f;

        /// <summary>
        /// The density the ONE global <see cref="BaseAppearanceChance01"/> produces at the shipped cell
        /// size, so a species that states no density of its own gets exactly that sea:
        /// <c>0.55 school per cell / (0.022 km)^2 = 1136.36 schools/km^2</c>.
        /// <para>⚠ <b>This number moves with <see cref="CellSizeMetres"/> and is meaningless without it.</b>
        /// It was 38.19 while the cell was 120 m (the owner's 2026-09-09 ruling shrank the cell to 22 m):
        /// a density is only ever realised up to ONE school per cell, so the two are a pair. Both terms
        /// are owner-tunable, so this is the density at the SHIPPED pair and not a law; a guard restates
        /// the division from those two primitives rather than asking the code.</para>
        /// </summary>
        public const float ReferenceSchoolsPerSquareKilometre = 1136.3636f;

        // ---- the appearance gate (location · weather · date — the owner's three) --------------------

        [Tooltip("Base chance (0..1) a cell holds a school in a given slot, BEFORE weather and season " +
                 "adjust it. This is the master 'how much fish is in the sea' dial. 0 = an empty sea: " +
                 "no marks and a catch roll bit-for-bit the pre-school one (the A/B off switch).")]
        [Range(0f, 1f)] public float BaseAppearanceChance01;

        [Tooltip("LOCATION gate: a cell whose water column is shallower than this (m) never holds a " +
                 "school. Fish are not on the beach — this is what keeps marks off the flats and out " +
                 "of the drying sandbar as the tide falls.")]
        [Min(0f)] public float MinWaterColumnMetres;

        [Tooltip("WEATHER gate: how much a full storm adds to (or, negative, takes from) the appearance " +
                 "chance. Positive by default and for the same reason SeaBoldness01 is — broken water " +
                 "emboldens fish, so a blow shows MORE of them. 0 = weather-blind schools.")]
        [Range(-1f, 1f)] public float SeaStateAppearanceBias;

        [Tooltip("DATE gate — Early Spring: multiplier on the appearance chance in this season. 1 = " +
                 "neutral. Below 1 = a lean season you have to work; above 1 = the run is on.")]
        [Min(0f)] public float EarlySpringAppearance;

        [Tooltip("DATE gate — High Summer: multiplier on the appearance chance in this season.")]
        [Min(0f)] public float HighSummerAppearance;

        [Tooltip("DATE gate — The Turn: multiplier on the appearance chance in this season.")]
        [Min(0f)] public float TheTurnAppearance;

        [Tooltip("DATE gate — Hard Winter: multiplier on the appearance chance in this season. Kept " +
                 "well below 1 by default: the winter sea is meant to be hard fishing.")]
        [Min(0f)] public float HardWinterAppearance;

        // ---- the window (the owner: finding one opens a window of time) -----------------------------

        [Tooltip("Shortest a school hangs about (in-game hours) once it shows. Clamped to SlotHours.")]
        [Min(0.01f)] public float MinWindowHours;

        [Tooltip("Longest a school hangs about (in-game hours). Clamped to SlotHours — a window never " +
                 "outlives the slot it was rolled in. The average window ÷ SlotHours is roughly the " +
                 "fraction of 'present' cells that are actually SHOWING at any instant, so this dial and " +
                 "BaseAppearanceChance01 together set how much of the sea is fishable right now.")]
        [Min(0.01f)] public float MaxWindowHours;

        // ---- the area (the owner: a tool for locating AREAS where fish are) -------------------------

        [Tooltip("Smallest school area (radius, m). Small = a tight spot you must sit right on top of.")]
        [Min(0.1f)] public float MinRadiusMetres;

        [Tooltip("Largest school area (radius, m). ⚠ Clamped to CellSizeMetres — the sim only searches " +
                 "the neighbouring cells, so a school that could spill further than one cell would be " +
                 "invisible from its own outer ring.")]
        [Min(0.1f)] public float MaxRadiusMetres;

        // ---- the depth (the owner: bites happen at approximately the depth shown) -------------------

        [Tooltip("Shallowest a school sits, as a FRACTION of the water column there (0 = the surface, " +
                 "1 = on the bottom). A fraction rather than metres so the same dial works over a 2 m " +
                 "flat and a 60 m hole, and so the mark always sits above the finder's bottom contour.")]
        [Range(0f, 1f)] public float MinDepthFraction01;

        [Tooltip("Deepest a school sits, as a fraction of the water column there.")]
        [Range(0f, 1f)] public float MaxDepthFraction01;

        [Tooltip("WEATHER, the second way: how far down a full storm pushes the school (added to the " +
                 "depth fraction). Fish go deep when it blows — and because depth picks the band, this " +
                 "is also how the weather changes WHICH species you find. 0 = weather-blind depth.")]
        [Range(0f, 1f)] public float SeaStateDepthBias01;

        [Tooltip("The water column (m) assumed where no bathymetry is authored (no tidal terrain — a " +
                 "bare test rig, an unpainted region). The 'no height map means open water' posture the " +
                 "rest of the module already takes, given a depth so schools still sit somewhere sane.")]
        [Min(0.1f)] public float OpenWaterColumnMetres;

        [Tooltip("FALLBACK spread (m) of a shoal's own lazy loop, for a species that states no " +
                 "ShoalSpreadMetres of its own. The rig authors its shoal at 1.2 m and that is the " +
                 "reference this ships at. This is a LENGTH, not a fraction of the school's radius: " +
                 "a school is 22-55 m across because that is how far a BOAT may be and still be on it, " +
                 "while the fish inside it hold together within a metre or two of each other.")]
        [Min(0.05f)] public float ShoalSpreadMetres;

        [Tooltip("FALLBACK density (schools per square kilometre of suitable water) for a species that " +
                 "states no SchoolsPerSquareKilometre of its own. Ships at the density the one global " +
                 "BaseAppearanceChance01 already produced at the shipped 120 m cell, so an unstated " +
                 "species keeps exactly the sea it had before the field existed.")]
        [Min(0f)] public float SchoolsPerSquareKilometre;

        // ---- the density (the owner: one fish = lower bite rate, several = higher) ------------------

        [Tooltip("Fewest fish a school shows. The owner's ruling in one number: this IS the mark count " +
                 "on the glass AND the expected bite rate. 1 = the lonely single-fish mark.")]
        [Min(1)] public int MinMarks;

        [Tooltip("Most fish a school shows — the fat, worth-stopping-for return.")]
        [Min(1)] public int MaxMarks;

        [Tooltip("How much ONE mark speeds the bite: the wait is divided by (1 + marks × this), so 0.35 " +
                 "means a 3-fish school bites about twice as fast. This is the whole density→bite-rate " +
                 "map — the one place the picture becomes the fishing. 0 = marks are decoration.")]
        [Min(0f)] public float BiteRatePerMark;

        [Tooltip("Ceiling on that speed-up, so a freak fat school can't make bites instant (which reads " +
                 "as a bug and leaves no room for the cast/settle beat). 1 = the density never speeds " +
                 "anything.")]
        [Min(1f)] public float MaxBiteRateMultiplier;

        // ---- holding at the depth shown -------------------------------------------------------------

        [Tooltip("What the school is worth when you hold the rig in the WRONG depth band (0..1 of its " +
                 "full effect). Never 0 — being over fish is worth something even fished badly, the same " +
                 "promise depth and bait already make. Lower = the depth read on the glass matters more.")]
        [Range(0f, 1f)] public float OffDepthMatch01;

        [Tooltip("What the school is worth on a cast that plays NO depth game at all (the bobber/legacy " +
                 "branch, which has no held depth to judge). 1 = a bobber gets the school's full lift, " +
                 "so this feature never makes the old way of fishing worse. Lower it to make depth " +
                 "fishing the only way to properly work a school.")]
        [Range(0f, 1f)] public float DepthlessMatch01;

        // ---- which fish are down there ---------------------------------------------------------------

        [Tooltip("Fewest species one school holds (picked from the region's authored pool that the " +
                 "season allows and that live at the school's depth band).")]
        [Min(1)] public int MinSpecies;

        [Tooltip("Most species one school holds. Higher = a mixed shoal and a less targeted catch.")]
        [Min(1)] public int MaxSpecies;

        [Tooltip("SLACK-WATER BAR, metres of tide per in-game hour (owner ruling 2026-09-06). A species " +
                 "flagged MovingWaterOnly (the striped bass) is barred while the water is changing more " +
                 "slowly than this — the run of the tide, not its height. Read by the school model AND " +
                 "the catch resolver from this one place. 0 = the gate never bites.")]
        [Min(0f)] public float MovingWaterMetresPerHour;

        [Tooltip("How strongly a species IN the school is favoured in the catch roll. Applied on top of " +
                 "bait/tackle/depth as one more soft WEIGHT — never a filter, so an odd fish can always " +
                 "still take. Eased by how well you are sitting on the school, so clipping the rim " +
                 "barely re-weights anything. 1 = the school says nothing about what bites.")]
        [Min(1f)] public float SchoolSpeciesBoost;

        [Tooltip("How much a species NOT in the school is damped (0..1) while you are on one. Keep " +
                 "clearly above 0: fishing a school of mackerel should not make a cod impossible, only " +
                 "unlikely. 1 = no damp.")]
        [Range(0f, 1f)] public float OffSchoolSpeciesDamp01;

        // ---- the gull's splash (owner's ask 2026-09-09: "fish react to it landing") ----------------

        [Tooltip("How far PAST a school's edge a gull hitting the water still scatters it (m). A school " +
                 "is a disc 8-14 m across and its fish are drawn over the whole of it, so this is the " +
                 "margin beyond the rim, not a radius from the anchor. Ships at the sidecar's own flee " +
                 "radius (2.5 m — the distance a gull bolts from a person), read from the other side: " +
                 "how close something has to land before the fish take it personally. 0 = a splash is " +
                 "never noticed.")]
        [Min(0f)] public float GullSplashScatterRadiusMetres;

        [Tooltip("How hard a school bolts from a splash landing right on top of it (0..1). Scales how " +
                 "far each fish darts — 1 is a full shoal-width, which reads as panic. Ships at 0.5, " +
                 "the sidecar's own gull-to-shoal coupling (attractor 'surface shoal', weight 0.5); " +
                 "nothing in the rig drop measures a FISH's reaction, so this is the number to move if " +
                 "it looks wrong. 0 = no scatter at all. ⚠️ This moves the picture ONLY: bite and catch " +
                 "rates are untouched by a gull, by ruling (2026-09-09).")]
        [Range(0f, 1f)] public float GullSplashScatterStrength01;

        /// <summary>
        /// The reference tuning for the St Peters opening, sized against the OWNER'S FRAME rather than
        /// guessed — his ruling of 2026-09-09, <i>"i want to see them"</i>.
        ///
        /// <para><b>The cell is the whole argument.</b> A <c>(cell, slot)</c> holds at most ONE school, so
        /// <see cref="CellSizeMetres"/> is a hard ceiling on school ANCHORS per square kilometre — and the
        /// fish are drawn AT the anchor, inside ~7 m of it, against a boat camera 24.9 × 14 m. At the
        /// retired 120 m cell that ceiling was 69 schools/km² and a fish was on screen at the St Peters
        /// landing for 0.5–4.8 % of the daylight; at 22 m it is 2 066/km² and 38–58 %
        /// (<c>FishDutyCycleAtTheLandingTests</c>). No density number could have reached that on its own:
        /// <see cref="FishSchoolMath.BaseChanceForDensity"/> clamps at one school per cell.</para>
        ///
        /// <para><b>The disc is not the drawing.</b> Schools run 8–14 m across, which is how far a BOAT may
        /// be and still be ON the mark. It was 22–55 m — two to four screens wide, so the rod worked over
        /// water where nothing could be seen, which is the incoherence the owner was really reporting. The
        /// 8 m floor is not a taste: the furthest a drawn fish sits from its anchor is 6.9 m (a striped
        /// bass, 4 m spread), and a disc smaller than that would cull a school whose fish are on screen.</para>
        ///
        /// <para>Schools sit a quarter to four-fifths of the way down the column and hold 1–5 fish unless
        /// the species says otherwise: a single mark bites ~1.35× as fast, a full five ~2.75× (the ceiling
        /// is 3). A window is 1.75–2.5 h of a 2.5 h slot, so a school you have found is still there when
        /// you have steamed to it. Winter is deliberately lean and high summer generous. Nothing here can
        /// zero a fish out: the wrong depth is still worth a third of the school, and a species the school
        /// does not hold is damped to 0.4, never barred.</para>
        /// </summary>
        public static FishSchoolSettings Default => new FishSchoolSettings
        {
            CellSizeMetres = 22f,
            SlotHours = 2.5f,

            BaseAppearanceChance01 = 0.55f,
            MinWaterColumnMetres = 0.8f,
            SeaStateAppearanceBias = 0.25f,
            EarlySpringAppearance = 1f,
            HighSummerAppearance = 1.2f,
            TheTurnAppearance = 1f,
            HardWinterAppearance = 0.55f,

            MinWindowHours = 1.75f,
            MaxWindowHours = 2.5f,

            MinRadiusMetres = 8f,
            MaxRadiusMetres = 14f,

            MinDepthFraction01 = 0.25f,
            MaxDepthFraction01 = 0.8f,
            SeaStateDepthBias01 = 0.25f,
            OpenWaterColumnMetres = 25f,
            ShoalSpreadMetres = ShoalMathReferenceSpreadMetres,
            SchoolsPerSquareKilometre = ReferenceSchoolsPerSquareKilometre,

            MinMarks = 1,
            MaxMarks = 5,
            BiteRatePerMark = 0.35f,
            MaxBiteRateMultiplier = 3f,

            OffDepthMatch01 = 0.35f,
            DepthlessMatch01 = 1f,

            MinSpecies = 1,
            MaxSpecies = 3,
            MovingWaterMetresPerHour = 0.25f,
            SchoolSpeciesBoost = 3f,
            OffSchoolSpeciesDamp01 = 0.4f,

            GullSplashScatterRadiusMetres = 2.5f,
            GullSplashScatterStrength01 = 0.5f,
        };
    }

    /// <summary>
    /// The three moments (juice charter, owner ruling 2026-09-09: "yes to 4, write the juice charter").
    /// Every amplitude, duration, curve and threshold the juice lane's PRs add is a field here or on a
    /// Def asset (rule 6), and every field is hand-added to <c>GameConfig.asset</c> in the same PR — an
    /// absent field reads ZERO, and a zero here is "no grade at all", not "the default".
    ///
    /// <para><b>Grade*</b> (PR 1 — <c>MoodGradeDirector</c>): the BLEND curve between the five looks the
    /// owner authors in <c>Resources/MoodGradeProfile.asset</c>. The looks are the profile's; how wide
    /// the golden hour is, how fast night fades in, and where fog and storm begin are these. Since the
    /// owner's 2026-09-09 ruling the STRENGTH of the whole thing is one too — <see cref="GradeStrength"/>
    /// in play, <see cref="GradeIntroStrength"/> while the arrival opening runs — each a lerp from the
    /// identity grade toward the authored look. The grade owns TONE only (bloom, contrast, saturation, a
    /// vignette); the whole-screen multiply of <c>DayNightProfile</c> owns the time-of-day COLOUR, and
    /// never both — see <c>docs/design/lighting-and-daynight.md</c> §8.2.</para>
    ///
    /// <para><b>Feel*</b> / <b>CatchPushIn*</b> / <b>ImpactShake*</b> / <b>SpeedPullBack*</b> (PR 2 —
    /// <c>CameraFeel</c> inside the one <c>CameraFollow</c>): the push-in on a landed catch, the shake on a
    /// grounding, the pull-back at speed on open water. Amplitudes, durations, thresholds and the slew;
    /// the mode gate is the charter's, the deck's is a switch.</para>
    ///
    /// <para><b>BoatLead*</b> / <see cref="SpeedPullBackWhenCarriedAboard"/> (boat feel PR 1, owner
    /// ruling 2026-09-19 "lead yes, zoom-out yes, intro gets it too, no shake"): how far the camera
    /// looks ahead of a moving BOAT, how much of the follow's lag it pays back, its cap as a fraction
    /// of the view, and whether a passenger's deck (the intro) gets the helm's pull-back. Every one
    /// is 0/off in code, which hands back the scene's own look-ahead; the ruled numbers are the
    /// asset's. They live here and not on the scenes because the scenes serialize their own
    /// look-ahead, and St Peters cannot be rebuilt.</para>
    ///
    /// <para><b>Moments (PR 3, charter §4) — the three moments and the silent dig (#803).</b> Four
    /// published beats (<c>JuiceMomentCue</c>: Landing, Sale, DigStrike, CastEntry) dressed by pooled
    /// presenters: the landing frame's hit-stop (<see cref="LandingHitStopScale"/> for
    /// <see cref="LandingHitStopSeconds"/> of UNSCALED time — the world clock pauses with it, the
    /// feel timers do not) and a splash burst sized by the fish's weight; the notebook's weight
    /// readout counting up over <see cref="WeightCountUpSeconds"/> with a scale pop on the last
    /// digit; the sale's coins flying crate→ledger (<see cref="CoinFlyCount"/> capped) while the
    /// purse counts up over <see cref="SaleCountUpSeconds"/>; sand chunks on the shovel's strike and
    /// rings on the cast's entry. Per-action anticipation/follow-through timing is NOT here — it is
    /// the <c>ActionTimingDef</c> assets under <c>Resources/ActionTiming/</c>, one per action.
    /// <see cref="MomentsEnabled"/> is the one switch; every presenter ticks at
    /// <see cref="MomentTickHz"/> on unscaled time and early-returns when nothing is alive.</para>
    /// </summary>
    [System.Serializable]
    public struct JuiceSettings
    {
        [Header("Grade (PR 1) — the post-processing blend")]
        [Tooltip("Master switch. OFF disables the Volume and hands the camera back with post-processing " +
                 "off — the frame is exactly the pre-juice frame.")]
        public bool GradeEnabled;

        [Tooltip("Master strength of the grade in PLAY, 0..1. The evaluated look is lerped from the " +
                 "IDENTITY grade (MoodGrade.Neutral: no bloom, no tint, no vignette) toward the look " +
                 "the profile authored. 0 = exactly the pre-juice frame; 1 = the authored look at full. " +
                 "The owner played the full-strength grade on 2026-09-09 and ruled it a filter over the " +
                 "world, so the shipped asset carries a fraction. This field MUST exist in " +
                 "GameConfig.asset — a serialized field absent from the asset reads ZERO, and zero here " +
                 "is no grade at all.")]
        [Range(0f, 1f)] public float GradeStrength;

        [Tooltip("Master strength of the grade WHILE THE ARRIVAL OPENING IS RUNNING, 0..1 — the director " +
                 "reads GameServices.OpeningCinematicRunning and picks this instead of GradeStrength. " +
                 "The owner keeps the grade on the intro ('i do like it on the intro maybe toned down a " +
                 "little'), so this sits ABOVE GradeStrength. Same law: absent from the asset reads ZERO.")]
        [Range(0f, 1f)] public float GradeIntroStrength;

        [Tooltip("Half-width of the golden-hour kernel, in hours, centred on sunrise and on sunset. The " +
                 "Golden Hour look is at full weight ON the horizon crossing and fades linearly to " +
                 "nothing this many hours either side. 0 = no golden hour.")]
        [Min(0f)] public float GradeGoldenHourWidthHours;

        [Tooltip("Legacy pure-math fixture input. The live grade reads the sky's sunset-to-night interval.")]
        [HideInInspector, Min(0f)] public float GradeNightBlendHours;

        [Tooltip("Visibility (EnvironmentSample.Visibility, 0 fog .. 1 clear) at or above which the Fog " +
                 "look has NO weight.")]
        [Range(0f, 1f)] public float GradeFogVisibilityStart;

        [Tooltip("Visibility at or below which the Fog look is at FULL weight. Linear between the two.")]
        [Range(0f, 1f)] public float GradeFogVisibilityFull;

        [Tooltip("Sea state (EnvironmentSample.SeaState01) at or below which the Storm look has NO weight.")]
        [Range(0f, 1f)] public float GradeStormSeaStateStart;

        [Tooltip("Sea state at or above which the Storm look is at FULL weight. Linear between the two.")]
        [Range(0f, 1f)] public float GradeStormSeaStateFull;

        [Tooltip("How often the director re-evaluates the blend, per second (unscaled time). The look " +
                 "changes over minutes of game time, so 10 Hz is invisible; this is a slow-tick budget " +
                 "knob (rule 7), not a smoothing — there is no smoothing.")]
        [Min(1f)] public float GradeRefreshHz;

        [Header("Camera feel (PR 2) — the camera speaks")]
        [Tooltip("Master switch for the camera feel layer (push-in, shake, pull-back). OFF hands CameraFollow " +
                 "back exactly as it was before the layer existed.")]
        public bool FeelEnabled;

        [Tooltip("Whether the layer is live ON DECK (the push-in and the shake; the pull-back is the helm's, " +
                 "and a passenger's only by SpeedPullBackWhenCarriedAboard). The juice charter left the deck untouched, so this DEFAULTED off — but the intro " +
                 "fishes from the deck, and on 2026-09-09 the owner played it and ruled 'accept the deck " +
                 "feel', so the shipped GameConfig asset now carries 1. It stays a number and not a rule.")]
        public bool FeelOnDeckEnabled;

        [Tooltip("Push-in on a landed catch (CatchLanded), as a fraction of the framing height, for a fish at " +
                 "the heavy mark. 0 = no push-in.")]
        [Range(0f, 0.5f)] public float CatchPushInFraction;

        [Tooltip("Seconds the push-in takes to reach its peak (ease-out).")]
        [Min(0f)] public float CatchPushInSeconds;

        [Tooltip("Seconds the push-in takes to release back to the framing (smooth-step).")]
        [Min(0f)] public float CatchPushOutSeconds;

        [Tooltip("Weight (kg) at and above which a catch gets the FULL push-in; lighter fish scale down " +
                 "linearly to the light floor. 0 = every catch is a heavy one.")]
        [Min(0f)] public float CatchPushInHeavyKg;

        [Tooltip("The smallest fraction of the full push-in a light catch gets (a smelt still lands).")]
        [Range(0f, 1f)] public float CatchPushInLightScale;

        [Tooltip("Shake amplitude in world metres at severity 1 (BoatGrounded.Severity scales it; 32 px per " +
                 "metre on the asset grid). 0 = no shake.")]
        [Min(0f)] public float ImpactShakeMeters;

        [Tooltip("Seconds the shake takes to decay to nothing (squared falloff).")]
        [Min(0f)] public float ImpactShakeSeconds;

        [Tooltip("Shake frequency, cycles per second.")]
        [Min(0f)] public float ImpactShakeHz;

        [Tooltip("Pull-back at speed on open water (the helm, and a passenger's deck by " +
                 "SpeedPullBackWhenCarriedAboard), as a fraction of the framing height at full speed. 0 = no pull-back, and the PixelPerfectCamera is never paused for it.")]
        [Range(0f, 0.5f)] public float SpeedPullBackFraction;

        [Tooltip("Boat speed (m/s) at or below which there is no pull-back.")]
        [Min(0f)] public float SpeedPullBackStartMps;

        [Tooltip("Boat speed (m/s) at or above which the pull-back is full. Smooth-step between the two.")]
        [Min(0f)] public float SpeedPullBackFullMps;

        [Tooltip("Extra pull-back fraction at full speed in a full sea (EnvironmentSample.SeaState01 = 1), " +
                 "scaled by the same speed term — a boat lying still in a swell is not pulled back.")]
        [Range(0f, 0.5f)] public float SpeedPullBackSeaStateFraction;

        [Tooltip("Seconds the pull-back takes to cross its whole range, in either direction — the slew limit " +
                 "that keeps the sea from breathing in a swell. 0 = no limit.")]
        [Min(0f)] public float SpeedPullBackSlewSeconds;

        [Tooltip("How often the pull-back re-samples the sea state, per second (unscaled time). A slow-tick " +
                 "budget knob (rule 7): the pull-back is slewed over seconds anyway.")]
        [Min(1f)] public float FeelSeaStateRefreshHz;

        [Header("Boat camera (boat feel PR 1) — lead the boat, and the passenger's pull-back")]
        [Tooltip("Seconds of her travel the camera leads a BOAT by: at the helm, and riding someone else's " +
                 "deck as a passenger (the intro). 0 = the scene's own look-ahead seconds (CameraFollow, " +
                 "0.35), as before. Walking, her own deck and a cabin never read it.")]
        [Min(0f)] public float BoatLeadSeconds;

        [Tooltip("How much of the follow's own lag the boat lead pays back, 0..1. The follow trails a moving " +
                 "boat by ~0.16 s of her travel (Smooth 6 at 60 fps), and the lead you SEE is the lead minus " +
                 "that. 1 = the lead on screen is the lead seconds times her speed; 0 = the lag eats it, as " +
                 "before (under 1 m at every speed).")]
        [Range(0f, 1f)] public float BoatLeadLagCompensation;

        [Tooltip("Cap on the boat lead, as a fraction of HALF the view's height at the framing's rung, so a " +
                 "fast boat is never led off the screen and a small rung is not led as far as a big one. " +
                 "0 = the scene's metres cap (CameraFollow, 2.5 m) at every rung, as before.")]
        [Range(0f, 1f)] public float BoatLeadMaxViewFraction;

        [Tooltip("Seconds the boat lead's speed is smoothed over while the followed hull is ROWED. A rowed " +
                 "hull surges on every stroke (RowingStyleDef), and a lead read off the raw speed pushed the " +
                 "view forward on each pull and dropped it back in each rest, so the world lurched about twice " +
                 "as much as the boat. About one slow stroke. 0 = the raw speed, as before. Engine and sail " +
                 "hulls never read it.")]
        [Min(0f)] public float BoatLeadRowedSpeedSmoothSeconds;

        [Tooltip("Whether the speed pull-back also runs while she RIDES someone else's deck as a passenger " +
                 "(the intro). Off = open water is the helm's alone, as before. The deck gate still holds: " +
                 "it needs FeelOnDeckEnabled as well.")]
        public bool SpeedPullBackWhenCarriedAboard;

        [Header("Moments (PR 3) — the three moments and the silent dig")]
        [Tooltip("The one switch for every moment presenter (hit-stop, bursts, count-ups, coin flights). Off = the pre-charter picture; the audio hooks still publish.")]
        public bool MomentsEnabled;

        [Tooltip("Time.timeScale during the landing frame's hit-stop (0 = a dead freeze, 1 = no stop). Unscaled feel timers ignore it; the world clock and the sim pause with it.")]
        [Range(0f, 1f)] public float LandingHitStopScale;

        [Tooltip("Seconds of UNSCALED time the landing hit-stop lasts. The charter's band is 0.06–0.12; 0 disables the stop.")]
        [Min(0f)] public float LandingHitStopSeconds;

        [Tooltip("Droplets in the landing splash for the lightest fish.")]
        [Min(0)] public int LandingSplashDropsMin;

        [Tooltip("Extra droplets per kilogram of fish — a cod throws more water than a mackerel.")]
        [Min(0f)] public float LandingSplashDropsPerKg;

        [Tooltip("Ceiling on droplets per landing (the pool is fixed; a whale is capped).")]
        [Min(0)] public int LandingSplashDropsMax;

        [Tooltip("Seconds a landing droplet lives.")]
        [Min(0f)] public float LandingSplashSeconds;

        [Tooltip("Seconds the notebook's weight readout counts up from 0.0 to the landed weight (unscaled).")]
        [Min(0f)] public float WeightCountUpSeconds;

        [Tooltip("Peak scale of the readout's pop on the frame the count lands (1 = no pop).")]
        [Min(1f)] public float CountUpPopScale;

        [Tooltip("Seconds the landing pop takes to bump up and settle back to exactly 1.")]
        [Min(0f)] public float CountUpPopSeconds;

        [Tooltip("Coins that fly from the crate to the ledger on a sale — a hard cap, the pool's size. A one-coin sale flies one.")]
        [Min(0)] public int CoinFlyCount;

        [Tooltip("Seconds ONE coin takes to fly crate→ledger (unscaled).")]
        [Min(0f)] public float CoinFlySeconds;

        [Tooltip("Seconds between one coin's launch and the next.")]
        [Min(0f)] public float CoinFlyStaggerSeconds;

        [Tooltip("Height of the coin's arc above the straight line, in notebook pixels.")]
        [Min(0f)] public float CoinFlyArcPixels;

        [Tooltip("Seconds the purse total counts up over once the first coin lands (unscaled). The last coin's landing pops the total.")]
        [Min(0f)] public float SaleCountUpSeconds;

        [Tooltip("Sand chunks thrown on the shovel's strike.")]
        [Min(0)] public int SandChunkCount;

        [Tooltip("Seconds a sand chunk lives.")]
        [Min(0f)] public float SandChunkSeconds;

        [Tooltip("Wet clods thrown off the blade tip at the dig's lift-and-toss phase (0 = no toss).")]
        [Min(0)] public int DigTossChunkCount;

        [Tooltip("Seconds after the blade enters the ground (the impact) that the toss leaves the blade tip: " +
                 "the dig clip's in-ground and lever phases and the lift, up to where the blade is out at his side.")]
        [Min(0f)] public float DigTossDelaySeconds;

        [Tooltip("Seconds a tossed clod lives: its flight off the blade, then lying where it landed until it fades.")]
        [Min(0f)] public float DigTossChunkSeconds;

        [Tooltip("How big a tossed clod is (× the first toss's clod size). The owner asked for a bigger toss " +
                 "(2026-11-22): a real shovelful of separate clods.")]
        [Range(0.5f, 4f)] public float DigTossScale;

        [Tooltip("How far the toss is thrown across the ground (× the first toss's throw speed).")]
        [Range(0.5f, 4f)] public float DigTossReachScale;

        [Tooltip("How high the toss arcs (× the first toss's up-throw, which barely left the blade).")]
        [Range(0.5f, 6f)] public float DigTossUpScale;

        [Tooltip("How far apart the clods of one toss fly, square to the throw (× the first toss's spread), so " +
                 "they read as several clods in the air and land as a small spoil pile, not one lump.")]
        [Range(0f, 4f)] public float DigTossSpreadScale;

        [Tooltip("Seconds over which the clods of one toss leave the blade, the farthest-thrown first, so they " +
                 "stream off it one after another and fly as separate clods (0 = all at once, as one lump).")]
        [Range(0f, 0.4f)] public float DigTossReleaseSeconds;

        [Tooltip("When the dig faces within this many degrees of straight away from the camera (north, his back " +
                 "to the viewer), the mesh body turns to this many degrees off north instead, toward the side he " +
                 "already leans to (his right when dead on), so the shovel and the toss show past his body. 0 = off.")]
        [Range(0f, 45f)] public float DigAwayTurnDegrees;

        [Tooltip("Rings spread from the point the cast line touches down.")]
        [Min(0)] public int CastRingCount;

        [Tooltip("Seconds a cast-entry ring lives (each ring is staggered across the first third of it).")]
        [Min(0f)] public float CastRingSeconds;

        [Tooltip("Ticks per second for the moment presenters (bursts, coin flights, count-ups). Unscaled; a presenter with nothing alive costs one compare.")]
        [Min(1f)] public float MomentTickHz;

        public static JuiceSettings Default => new JuiceSettings
        {
            GradeEnabled = true,
            GradeStrength = 0.35f,
            GradeIntroStrength = 0.7f,
            GradeGoldenHourWidthHours = 0.75f,
            GradeNightBlendHours = 1f,
            GradeFogVisibilityStart = 0.6f,
            GradeFogVisibilityFull = 0.15f,
            GradeStormSeaStateStart = 0.55f,
            GradeStormSeaStateFull = 0.9f,
            GradeRefreshHz = 10f,
            FeelEnabled = true,
            FeelOnDeckEnabled = false,
            CatchPushInFraction = 0.08f,
            CatchPushInSeconds = 0.12f,
            CatchPushOutSeconds = 0.45f,
            CatchPushInHeavyKg = 6f,
            CatchPushInLightScale = 0.35f,
            ImpactShakeMeters = 0.18f,
            ImpactShakeSeconds = 0.3f,
            ImpactShakeHz = 18f,
            SpeedPullBackFraction = 0.1f,
            SpeedPullBackStartMps = 2.5f,
            SpeedPullBackFullMps = 7f,
            SpeedPullBackSeaStateFraction = 0.05f,
            SpeedPullBackSlewSeconds = 2f,
            FeelSeaStateRefreshHz = 4f,

            // Boat camera (boat feel PR 1): every dial OFF in code, so a config that never names them
            // is the old picture. The owner's values (ruling 2026-09-19) live in the ASSET.
            BoatLeadSeconds = 0f,
            BoatLeadLagCompensation = 0f,
            BoatLeadMaxViewFraction = 0f,
            BoatLeadRowedSpeedSmoothSeconds = 0f,
            SpeedPullBackWhenCarriedAboard = false,

            // Moments (PR 3)
            MomentsEnabled = true,
            LandingHitStopScale = 0.05f,
            LandingHitStopSeconds = 0.09f,
            LandingSplashDropsMin = 6,
            LandingSplashDropsPerKg = 3f,
            LandingSplashDropsMax = 24,
            LandingSplashSeconds = 0.55f,
            WeightCountUpSeconds = 0.6f,
            CountUpPopScale = 1.35f,
            CountUpPopSeconds = 0.18f,
            CoinFlyCount = 8,
            CoinFlySeconds = 0.45f,
            CoinFlyStaggerSeconds = 0.05f,
            CoinFlyArcPixels = 14f,
            SaleCountUpSeconds = 0.7f,
            SandChunkCount = 7,
            SandChunkSeconds = 0.45f,
            DigTossChunkCount = 8,
            DigTossDelaySeconds = 0.6f,
            DigTossChunkSeconds = 3.5f,
            DigTossScale = 1.3f,
            DigTossReachScale = 1.8f,
            DigTossUpScale = 3.4f,
            DigTossSpreadScale = 3f,
            DigTossReleaseSeconds = 0.3f,
            DigAwayTurnDegrees = 25f,
            CastRingCount = 3,
            CastRingSeconds = 0.6f,
            MomentTickHz = 30f,
        };
    }
}
