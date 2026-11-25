using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HiddenHarbours.Economy
{
    [CreateAssetMenu(menuName = "Mending/Dory", fileName = "DoryMending")]
    public sealed class DoryMendingDef : ScriptableObject
    {
        public string Id;
        public string BoatId;
        public string PlaceId;
        public float WorkBelowMetres;
        public float LaunchAboveMetres;
        [Tooltip("A hull resting above high water needs a separate supported journey to the water. " +
                 "When enabled, this sequence never grants launch readiness from tide height alone.")]
        public bool RequiresSupportedLaunch;
        [Tooltip("Maximum interval between water samples. Work subdivides any interval that cannot " +
                 "be proven dry from the tide's rate bound, including a complete tide between samples.")]
        [Min(0.01f)] public float SearchStepGameMinutes = 1f;
        public DoryMendingJobDef[] Jobs;

        /// <summary>Validate the sequence before consuming anything. Content checks also supply an
        /// item lookup so misspelled or missing material ids cannot reach a placed interaction.</summary>
        public void Validate(Func<string, bool> itemExists = null)
        {
            Require(Id, "mending"); Require(BoatId, "boat"); Require(PlaceId, "region");
            if (!Finite(WorkBelowMetres) || !Finite(LaunchAboveMetres) ||
                !(LaunchAboveMetres > WorkBelowMetres) || !Finite(SearchStepGameMinutes) ||
                SearchStepGameMinutes < 0.01f) throw new ArgumentException("Invalid mending tide limits.");
            if (Jobs == null || Jobs.Length == 0) throw new ArgumentException("Mending needs jobs.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var flags = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<int>();
            foreach (DoryMendingJobDef job in Jobs)
            {
                if (job == null) throw new ArgumentException("Missing mending job.");
                Require(job.Id, "mendjob");
                if (string.IsNullOrWhiteSpace(job.DisplayName) || !ids.Add(job.Id) ||
                    !orders.Add(job.Order) || job.Order < 0 || job.Order >= Jobs.Length)
                    throw new ArgumentException("Mending jobs need unique ids and consecutive orders from zero.");
                if (!Regex.IsMatch(job.CompletedFlag ?? "", @"^st_peters\.dory_mend\.[a-z0-9]+(_[a-z0-9]+)*$") ||
                    !flags.Add(job.CompletedFlag)) throw new ArgumentException("Invalid mending completion flag.");
                if (!Finite(job.WorkGameMinutes) || job.WorkGameMinutes < 0.01f)
                    throw new ArgumentException("Mending work must take finite, positive time.");
                if (job.Materials == null || job.Materials.Length == 0)
                    throw new ArgumentException("Mending needs carried materials.");
                var materials = new HashSet<string>(StringComparer.Ordinal);
                foreach (var material in job.Materials)
                {
                    Require(material.ItemId, "supply");
                    if (material.Count <= 0 || !materials.Add(material.ItemId) ||
                        (itemExists != null && !itemExists(material.ItemId)))
                        throw new ArgumentException("Invalid or missing mending material.");
                }
            }
        }

        private static void Require(string id, string prefix)
        {
            if (!Regex.IsMatch(id ?? "", "^" + prefix + @"\.[a-z0-9]+(_[a-z0-9]+)*$"))
                throw new ArgumentException("Invalid " + prefix + " identity.");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
