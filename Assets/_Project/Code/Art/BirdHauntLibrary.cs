using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The gull schedule's global knobs (rule 6), owner-editable on <see cref="BirdHauntLibrary"/>. The
    /// places hold their own numbers; these shape every place's day the same way. The sunrise and
    /// sunset hours are not here: they are read from the sky's own profile, so the birds and the sky
    /// always agree.
    /// </summary>
    [Serializable]
    public sealed class GullScheduleTuning
    {
        [Header("The day")]
        [Tooltip("Hours before sunrise that the first birds leave the roost.")]
        [Min(0f)] public float DawnLeadHours = 0.5f;
        [Tooltip("Hours after sunrise that the morning crowd is complete.")]
        [Min(0f)] public float DawnFullHours = 1f;
        [Tooltip("The hour the busy morning ends.")]
        [Range(0f, 24f)] public float MorningEndHour = 11f;
        [Tooltip("The hour the quiet middle of the day starts.")]
        [Range(0f, 24f)] public float MiddayStartHour = 12f;
        [Tooltip("The hour the quiet middle of the day ends.")]
        [Range(0f, 24f)] public float MiddayEndHour = 15f;
        [Tooltip("Hours before sunset that the evening return starts.")]
        [Min(0f)] public float AfternoonLeadHours = 2.5f;
        [Tooltip("Hours after sunset that the last bird is home.")]
        [Min(0f)] public float EveningTailHours = 0.25f;

        [Header("Wind")]
        [Tooltip("Storm level (sea state 0..1) at which the wind starts to thin the birds.")]
        [Range(0f, 1f)] public float CalmStormMax = 0.35f;
        [Tooltip("Storm level at which the gale rule applies in full: exposed places empty.")]
        [Range(0f, 1f)] public float GaleStorm = 0.75f;
        [Tooltip("The share of its birds a sheltered place keeps in a gale.")]
        [Range(0f, 1f)] public float ShelterKeep = 0.35f;
        [Tooltip("Game hours ahead that the birds read the storm, so they leave before it peaks.")]
        [Min(0f)] public float GaleTellHours = 1f;

        [Header("Fog")]
        [Tooltip("Visibility (0..1) below which fog starts to thin the birds.")]
        [Range(0f, 1f)] public float FogClearVisibility = 0.6f;
        [Tooltip("Visibility at and below which the fog is thick.")]
        [Range(0f, 1f)] public float FogThickVisibility = 0.2f;
        [Tooltip("The share of its birds a place keeps in thick fog.")]
        [Range(0f, 1f)] public float FogKeep = 0.4f;

        [Header("The roost")]
        [Tooltip("Of the birds away from their haunt by day, the share sitting at the roost.")]
        [Range(0f, 1f)] public float RoostLoafShare = 0.25f;
        [Tooltip("The same share in thick fog.")]
        [Range(0f, 1f)] public float FogRoostShare = 0.6f;

        [Header("Population")]
        [Tooltip("A place's birds today = Typical x this, so some are always elsewhere.")]
        [Min(1f)] public float PopulationHeadroom = 1.3f;
        [Tooltip("Per place, per day: Typical x (1 +/- this), hashed from the seed, the place and the day.")]
        [Range(0f, 1f)] public float DayJitter = 0.15f;
        [Tooltip("The young of the year add up to this share.")]
        [Range(0f, 1f)] public float YoungShare = 0.2f;
        [Tooltip("How far through High Summer (0..1) the young start to arrive.")]
        [Range(0f, 1f)] public float YoungFromProgress = 0.5f;
        [Tooltip("The share of the young gone south by the end of The Turn.")]
        [Range(0f, 1f)] public float YoungLeaveByEnd = 0.5f;

        [Header("Surface schools")]
        [Tooltip("Birds over one surface school at the peak of the day.")]
        [Min(0f)] public float SchoolBirds = 6f;
        [Tooltip("The season's share of SchoolBirds, in the clock's order: Early Spring, High Summer, The Turn, Hard Winter.")]
        public float[] SchoolSeason = { 0.9f, 1f, 1f, 0.5f };
        [Tooltip("The quiet middle of the day over a school, as a share of the morning.")]
        [Range(0f, 1f)] public float SchoolMiddayLevel = 0.7f;
        [Tooltip("The afternoon over a school, as a share of the morning.")]
        [Range(0f, 1f)] public float SchoolAfternoonLevel = 0.8f;

        /// <summary>The shipped defaults (a fresh instance: the caller may change it).</summary>
        public static GullScheduleTuning Default => new GullScheduleTuning();

        /// <summary>The schedule's plain-number view, with the sky's sunrise and sunset hours.</summary>
        public GullTuningSpec ToSpec(double sunriseHour, double sunsetHour)
        {
            float[] school = SchoolSeason;
            return new GullTuningSpec
            {
                SunriseHour = sunriseHour,
                SunsetHour = sunsetHour,
                DawnLeadHours = DawnLeadHours,
                DawnFullHours = DawnFullHours,
                MorningEndHour = MorningEndHour,
                MiddayStartHour = MiddayStartHour,
                MiddayEndHour = MiddayEndHour,
                AfternoonLeadHours = AfternoonLeadHours,
                EveningTailHours = EveningTailHours,
                CalmStormMax = CalmStormMax,
                GaleStorm = GaleStorm,
                ShelterKeep = ShelterKeep,
                GaleTellHours = GaleTellHours,
                FogClearVisibility = FogClearVisibility,
                FogThickVisibility = FogThickVisibility,
                FogKeep = FogKeep,
                RoostLoafShare = RoostLoafShare,
                FogRoostShare = FogRoostShare,
                PopulationHeadroom = PopulationHeadroom,
                DayJitter = DayJitter,
                YoungShare = YoungShare,
                YoungFromProgress = YoungFromProgress,
                YoungLeaveByEnd = YoungLeaveByEnd,
                SchoolBirds = SchoolBirds,
                SchoolSeason = school == null
                    ? default
                    : new GullSeasons(At(school, 0), At(school, 1), At(school, 2), At(school, 3)),
                SchoolMiddayLevel = SchoolMiddayLevel,
                SchoolAfternoonLevel = SchoolAfternoonLevel,
            };
        }

        private static double At(float[] values, int i) => i < values.Length ? values[i] : 0.0;
    }

    /// <summary>
    /// The authored index of every <see cref="BirdHauntDef"/>, plus the gull schedule's global knobs:
    /// the <c>AmbientFleetLibrary</c> pattern. The Defs live in <c>Data/Birds/Haunts</c> (one entity per
    /// file, ADR 0003); this one asset gathers them and lives in <c>Resources</c>, so the self-installing
    /// <see cref="GullFlock"/> can load it with no scene or builder wiring.
    ///
    /// <para>Append-only: a new place = a new <see cref="BirdHauntDef"/> asset + one entry here. Pure
    /// content, never serialized into a save. Create via Assets ▸ Create ▸ Hidden Harbours ▸ Bird Haunt
    /// Library, saved at <c>Resources/BirdHauntLibrary</c>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Bird Haunt Library", fileName = "BirdHauntLibrary")]
    public sealed class BirdHauntLibrary : ScriptableObject
    {
        /// <summary>Resources path (no extension) the flock loads the library from.</summary>
        public const string ResourcesPath = "BirdHauntLibrary";

        [Tooltip("Every bird haunt and roost in the game, one Def per place (Data/Birds/Haunts). Null entries are skipped.")]
        public BirdHauntDef[] Haunts = Array.Empty<BirdHauntDef>();

        [Tooltip("The gull schedule's global knobs.")]
        public GullScheduleTuning Tuning = new GullScheduleTuning();

        /// <summary>
        /// Fills <paramref name="into"/> (cleared first) with one region's places, in the library's
        /// order, and returns how many. No allocation once the list has grown to the region's size.
        /// </summary>
        public int HauntsIn(string regionId, List<BirdHauntDef> into)
        {
            if (into == null) return 0;
            into.Clear();
            if (string.IsNullOrEmpty(regionId) || Haunts == null) return 0;
            for (int i = 0; i < Haunts.Length; i++)
            {
                BirdHauntDef def = Haunts[i];
                if (def != null && string.Equals(def.RegionId, regionId, StringComparison.Ordinal)) into.Add(def);
            }
            return into.Count;
        }
    }
}
