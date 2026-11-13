using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// Logic-only per-hull fit, not installed automatically. Existing console slots and instrument ids
    /// still resolve through BoatEquipment; this profile adds the working readings absent from HelmFit.
    /// No hull, console, purchase, save or presentation is modified by loading this data.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Boats/Working Helm Fit")]
    public sealed class WorkingHelmFitDef : ScriptableObject
    {
        public string Id = "helm_fit.";
        public string HullId = "boat.";
        // An authored fit for later mounting; null on the bare dory. Uses the existing equipment resolver.
        public HelmConsoleDef Console;
        public string[] FactoryInstruments = System.Array.Empty<string>();
        public WorkingInstrument AdditionalReadings;
        [Min(0f)] public float MinimumKeelClearanceMetres;
        [Min(0f)] public float IdleRpm;
        [Min(0f)] public float MaximumRpm;

        /// <summary>Setup-time only. Caller combines purchased ids once, never on the reading tick.</summary>
        public HelmFit DefaultEquipment() => BoatEquipment.EffectiveFit(Console, FactoryInstruments);

        /// <summary>For live equipment pass the existing IHelmInstruments.Fit; do not overwrite its ownership.</summary>
        public WorkingHelmFit Resolve(in HelmFit equipment)
        {
            WorkingInstrument instruments = AdditionalReadings;
            if (equipment.Sounder != SounderKind.None) instruments |= WorkingInstrument.Depth;
            if (equipment.Gps) instruments |= WorkingInstrument.Plotter;
            if (equipment.Compass != CompassMount.None) instruments |= WorkingInstrument.Motion;
            return new WorkingHelmFit(HullId, instruments, MinimumKeelClearanceMetres, IdleRpm, MaximumRpm);
        }

        public string Problem()
        {
            if (string.IsNullOrWhiteSpace(Id) || Id == "helm_fit." || !Id.StartsWith("helm_fit.", System.StringComparison.Ordinal)) return "invalid fit id";
            if (string.IsNullOrWhiteSpace(HullId) || HullId == "boat." || !HullId.StartsWith("boat.", System.StringComparison.Ordinal)) return "invalid hull id";
            if ((AdditionalReadings & ~WorkingInstrument.All) != 0
                || (AdditionalReadings & (WorkingInstrument.Depth | WorkingInstrument.Plotter)) != 0) return "depth and plotter belong to existing equipment";
            if (!Valid(MinimumKeelClearanceMetres) || !Valid(IdleRpm) || !Valid(MaximumRpm) || MaximumRpm < IdleRpm) return "invalid calibration";
            if (FactoryInstruments == null) return "missing factory instrument list";
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (string id in FactoryInstruments)
            {
                if (id != BoatEquipment.DepthSounderId && id != BoatEquipment.FishFinderId && id != BoatEquipment.GpsId
                    && id != BoatEquipment.RadarId && id != BoatEquipment.CompassDomeId && id != BoatEquipment.CompassFlushId) return "unknown instrument id";
                if (!seen.Add(id)) return "duplicate instrument id";
            }
            if (Console == null && FactoryInstruments.Length != 0) return "factory instruments require a console";
            return null;
        }

        private static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
