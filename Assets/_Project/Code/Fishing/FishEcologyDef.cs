using System;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    [Flags]
    public enum EcologyHabitat
    {
        None = 0,
        OpenWater = 1,
        Sand = 2,
        Mud = 4,
        Gravel = 8,
        Rock = 16,
        Estuary = 32
    }

    [Flags]
    public enum EcologyVegetation { NoDocumentedPreference = 0, Eelgrass = 1, Kelp = 2 }
    public enum EcologyLayer { Unstated = 0, Pelagic, Demersal, Benthic }
    public enum EcologyGrouping { Unstated = 0, Schooling, LooseAggregation, Solitary, SessileBed }
    public enum EcologyDayHabit { Unstated = 0, Variable, MainlyNocturnal, DayFeeding, DielMigration, ContinuousFilterFeeding }
    public enum EcologyTideHabit { Unstated = 0, NoHardRestriction, MovingWaterFeeding, InundationFilterFeeding }

    /// <summary>
    /// Source-backed ecological REFERENCE data, one record per existing species. No live system reads
    /// this type. Typical depth bands describe a stated life stage and locality, not absolute survival
    /// limits, catch gates or permission to populate terrain. See design/species-ecology-reference.md.
    /// Deliberately separate from movement tuning and the inactive living-fish field under review.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fish Ecology Reference", fileName = "FishEcology")]
    public sealed class FishEcologyDef : ScriptableObject
    {
        public string Id;
        public string SpeciesId;
        public string ScientificName;
        [TextArea] public string LifeStageAndScope;
        public float TypicalMinDepthMetres;
        public float TypicalMaxDepthMetres;
        [TextArea] public string DepthEvidence;
        public EcologyLayer Layer;
        public EcologyHabitat Habitats;
        public EcologyVegetation PreferredVegetation;
        [TextArea] public string VegetationEvidence;
        public EcologyGrouping Grouping;
        public EcologyDayHabit DayHabit;
        public EcologyTideHabit TideHabit;
        [TextArea] public string HabitEvidence;
        [TextArea] public string BiologyNotes;
        public string[] SourceUrls = Array.Empty<string>();
    }
}
