using System;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// <b>One NPC boat's working day at the pots</b>, as data (rule 2): whose boat, where she lies, the way out,
    /// the ground, how many pots, and how long each part of the job takes. <see cref="NpcPotDay"/> turns this plus
    /// <c>(worldSeed, dayIndex)</c> into the whole day; nothing here is ever saved (rule 5).
    ///
    /// <para><b>Units.</b> Hours are hours of the game day. Speeds are metres per clock second and durations are
    /// clock seconds (<c>IGameClock.TotalSeconds</c>), so the work stays watchable whatever length the owner
    /// gives the day; only the departure hour scales with it.</para>
    ///
    /// <para>The owner is named by id (<c>owner.*</c>), not by reference: owner Defs live in Boats, which Fishing
    /// may not name (rule 4). The presenter in App resolves it.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/NPC Pot Day", fileName = "NpcPotDay")]
    public class NpcPotDayDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, potday.snake_case. Append-only. Also seeds this boat's day.")]
        public string Id = "potday.";

        [Tooltip("The BoatOwnerDef id (owner.*) whose moored boat works this day.")]
        public string OwnerId = "";

        [Tooltip("The region scene this boat works from. The day runs only while that scene is loaded.")]
        public string RegionSceneName = "NineMileCreek";

        [Tooltip("The pot she fishes: its soak, capacity, depth range, sprites and splash.")]
        public TrapDef Trap;

        [Header("Berth (the scene's moored boat wins when it is present; this is the plan's own copy)")]
        [Tooltip("Where she lies moored, world metres.")]
        public Vector2 BerthPosition;

        [Tooltip("Her bow's compass heading at the berth (0 = North, clockwise).")]
        public float BerthHeadingDegrees = 90f;

        [Tooltip("Her keel's draught. Not used to move her; the content test holds the whole track to it at spring low.")]
        [Min(0f)] public float DraughtMeters = 1.3f;

        [Tooltip("Water the content test demands under her keel along the track at spring low.")]
        [Min(0f)] public float KeelClearanceMeters = 0.2f;

        [Header("Clock")]
        [Tooltip("The hour of the game day she casts off.")]
        [Range(0f, 24f)] public float DepartHour = 6f;

        [Tooltip("Seeded spread on the cast-off, game minutes either side, different every day.")]
        [Min(0f)] public float DepartJitterMinutes = 20f;

        [Header("Passage")]
        [Tooltip("The way out, berth to ground, world metres. The berth itself is not listed. The last point " +
                 "should lie on the line she works the string along.")]
        public Vector2[] PassageOut = Array.Empty<Vector2>();

        [Tooltip("The way home, ground to berth. The berth itself is not listed.")]
        public Vector2[] PassageHome = Array.Empty<Vector2>();

        [Tooltip("How many legs nearest the berth (out and home) are run at harbour speed.")]
        [Min(0)] public int HarbourLegCount = 3;

        [Tooltip("Speed inside the harbour, metres per clock second.")]
        [Min(0.1f)] public float HarbourSpeed = 2.2f;

        [Tooltip("Speed on passage outside, metres per clock second.")]
        [Min(0.1f)] public float CruiseSpeed = 4.5f;

        [Tooltip("Speed between pots on the string, metres per clock second.")]
        [Min(0.1f)] public float WorkSpeed = 1.6f;

        [Tooltip("Clock seconds she takes to swing onto a new heading at the start of a leg.")]
        [Min(0f)] public float TurnSeconds = 4f;

        [Tooltip("Clock seconds alongside at the end of the day while she is made fast and swings to the berth's heading.")]
        [Min(0f)] public float MakeFastSeconds = 8f;

        [Header("The string")]
        [Tooltip("Seabed position of pot 0, world metres.")]
        public Vector2 StringStart;

        [Tooltip("Compass bearing the string runs along from pot 0 (0 = North, clockwise). She works it bow-on to this.")]
        public float StringBearingDegrees = 0f;

        [Tooltip("Pots on the string.")]
        [Range(1, 24)] public int PotCount = 6;

        [Tooltip("Metres between pots along the string.")]
        [Min(1f)] public float PotSpacingMeters = 14f;

        [Tooltip("Each day a pot is set back up to this many metres further along the string (seeded per pot and day). " +
                 "Keep it under the spacing.")]
        [Min(0f)] public float DailyShiftMeters = 3f;

        [Tooltip("Metres from her centreline to the pot she is working. The pot is always to starboard, at the hauler.")]
        [Min(0f)] public float LayAlongsideMeters = 3.2f;

        [Tooltip("Where the rope comes aboard, hull-local metres (x = starboard, y = toward the bow).")]
        public Vector2 HaulerLocalMeters = new Vector2(1.3f, -0.8f);

        [Header("The job at each pot (clock seconds)")]
        [Tooltip("Gaffing the buoy: it is drawn from its float to the rail.")]
        [Min(0.1f)] public float GaffSeconds = 6f;

        [Tooltip("Hauling: the pot comes up the rope to the rail.")]
        [Min(0.1f)] public float HaulSeconds = 14f;

        [Tooltip("The pot is aboard: emptied, the catch worked, the pot baited again.")]
        [Min(0f)] public float DeckWorkSeconds = 30f;

        [Tooltip("Setting: the pot goes over, sinks, and the buoy follows it.")]
        [Min(0.1f)] public float SetSeconds = 8f;

        [Header("The catch and the deck (clock seconds per animal)")]
        [Tooltip("Region id (region.*) whose catch tables her pots are read against. A species comes up only if it " +
                 "lists this region, exactly as for a player's pot set there.")]
        public string CatchRegionId = "region.";

        [Tooltip("The bait she fishes with. It leans the catch toward the species it favours, as it does for the player.")]
        public HiddenHarbours.Economy.BaitDef Bait;

        [Tooltip("Taking one animal out of the pot and putting the gauge on it.")]
        [Min(0.1f)] public float PickSeconds = 2.5f;

        [Tooltip("Banding a keeper's claws.")]
        [Min(0.1f)] public float BandSeconds = 2f;

        [Tooltip("Carrying a banded keeper to its crate.")]
        [Min(0.1f)] public float StowSeconds = 2f;

        [Tooltip("Putting a short or a berried one back over the side.")]
        [Min(0.1f)] public float ReturnSeconds = 1.5f;

        [Tooltip("Baiting the emptied pot. A full pot of keepers plus this must fit inside DeckWorkSeconds.")]
        [Min(0f)] public float BaitSeconds = 4f;

        [Tooltip("Clock seconds the hand takes to walk from the rail back to the helm once the last pot is set.")]
        [Min(0f)] public float DeckWalkSeconds = 2.5f;

        [Tooltip("Carapace lengths (mm, ascending) at which a keeper moves up a grade. Each grade has its own crate.")]
        public float[] GradeBreaksMm = { 95f, 110f };

        [Tooltip("Where the catch is banded, hull-local metres (x = starboard, y = toward the bow).")]
        public Vector2 BenchLocalMeters = new Vector2(0.8f, -1.6f);

        [Tooltip("Where the first grade's crate sits, hull-local metres.")]
        public Vector2 CrateLocalMeters = new Vector2(-0.9f, -2.2f);

        [Tooltip("Hull-local step from one grade's crate to the next.")]
        public Vector2 CrateStepLocalMeters = new Vector2(0f, -0.7f);

        [Tooltip("A crate's inside, metres across the boat by along it. Stowed animals are laid out inside it.")]
        public Vector2 CrateInsideMeters = new Vector2(0.75f, 0.5f);

        [Tooltip("Animals laid side by side across a crate before the next row starts.")]
        [Range(1, 12)] public int CrateRowLength = 4;

        [Tooltip("Most animals one crate is drawn holding. Past it the crate is full to the eye; the count carries on.")]
        [Range(1, 60)] public int CrateShownCapacity = 16;

        [Tooltip("How far over the side a returned animal is dropped, metres from the rail.")]
        [Min(0f)] public float ReturnOverTheSideMeters = 0.9f;

        [Tooltip("Sorting order of an animal in hand or in a crate (above the hull and its crew).")]
        public int CatchSortingOrder = 30;

        [Header("Rope and buoy")]
        [Tooltip("Rope from pot to buoy, metres. Must exceed the deepest water over the string.")]
        [Min(0.5f)] public float RopeLengthMeters = 10.5f;

        [Tooltip("Compass bearing the flood tide streams toward over the ground. The ebb streams the other way.")]
        public float FloodBearingDegrees = 270f;

        [Tooltip("Tide rise or fall, metres per game hour, at which the buoy streams as far as it ever does.")]
        [Min(0.01f)] public float FullStreamRiseMetersPerHour = 1f;

        [Tooltip("Share of the rope's spare scope the buoy lies off the pot at full stream (a rope in a tideway bellies; it is never a straight line).")]
        [Range(0f, 1f)] public float StreamLean01 = 0.35f;

        [Tooltip("Colour of this owner's float. Buoy colour says whose gear it is; never the player's yellow.")]
        public Color BuoyColour = new Color(0.86f, 0.2f, 0.16f, 1f);

        [Tooltip("Rope colour at the surface. It fades to nothing toward the pot.")]
        public Color RopeColour = new Color(0.82f, 0.76f, 0.6f, 0.9f);

        [Tooltip("Rope width, metres.")]
        [Min(0.005f)] public float RopeWidthMeters = 0.06f;

        [Tooltip("How far down-screen a metre of depth draws (the three-quarter view's foreshortening of the water column).")]
        [Range(0f, 1f)] public float DepthDrawFraction = 0.35f;

        [Tooltip("A pot is seen through the water down to this depth, fading as it goes; below it only the rope shows.")]
        [Min(0.1f)] public float SeenToDepthMeters = 2.5f;

        [Tooltip("Sag of the hauling rope when slack, metres.")]
        [Min(0f)] public float HaulRopeSagMeters = 0.4f;

        [Tooltip("Sorting order of the buoy (the ambient fleet's buoys use 3).")]
        public int BuoySortingOrder = 3;

        [Tooltip("Sorting order of the rope and of a pot seen at the rail.")]
        public int RopeSortingOrder = 2;
    }
}
