using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HiddenHarbours.Tools.GoodsModel
{
    [Serializable]
    public sealed class GoodsContainerSpec
    {
        public string Id;
        public GoodsBox Outside;
        public GoodsBox Inside;
        public GoodsBox Opening;
        public long UsableVolumeMl;
        public int CountLimit;
        public string[] AcceptedProfiles = Array.Empty<string>();
        public GoodsSlot[] Slots = Array.Empty<GoodsSlot>();
        public long EmptyMassG;
        public long LidMassG;
        public long MaxGrossMassG;
        public int RequiredHands; // 0 = device only; no implicit second worker.
        public long MaxHandGrossMassG;
        public bool HasLid;
        public string StackGroup;
        public int MaxStackCount;
        public int MaxStackHeightMm;
        public long MaxSupportedMassG;
        public bool StackRequiresLid;
        public bool AllowHandCarriedStack;

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (!ValidId(Id, "container")) errors.Add("invalid container id");
            if (!Outside.IsValid || !Inside.Fits(Outside) || !Opening.Fits(Outside) ||
                Opening.LengthMm > Inside.LengthMm || Opening.WidthMm > Inside.WidthMm)
                errors.Add("invalid shell, inside or opening");
            if (UsableVolumeMl <= 0 || UsableVolumeMl > long.MaxValue / 1000 ||
                (Inside.IsValid && UsableVolumeMl > Inside.VolumeMm3 / 1000))
                errors.Add("invalid usable volume");
            if (EmptyMassG <= 0 || LidMassG < 0 || (!HasLid && LidMassG != 0) ||
                MaxGrossMassG < EmptyMassG || MaxHandGrossMassG < 0 ||
                RequiredHands < 0 || RequiredHands > 2 ||
                (RequiredHands > 0 && MaxHandGrossMassG < EmptyMassG)) errors.Add("invalid lift limits");
            if (CountLimit <= 0 || Slots == null || Slots.Length < CountLimit) errors.Add("invalid count or placements");
            var profiles = new HashSet<string>(StringComparer.Ordinal);
            if (AcceptedProfiles == null || AcceptedProfiles.Length == 0) errors.Add("missing profiles");
            else foreach (string profile in AcceptedProfiles)
                if (!ValidId(profile, "goods") || !profiles.Add(profile)) errors.Add("invalid or duplicate profile");
            if (Slots != null)
            {
                for (int i = 0; i < Slots.Length; i++)
                {
                    if (!Slots[i].IsInside(Inside) || !Slots[i].Size.Fits(Opening, true))
                        errors.Add("placement outside clear envelope or opening");
                    for (int j = 0; j < i; j++)
                        if (Slots[i].Overlaps(Slots[j])) errors.Add("overlapping placements");
                }
            }
            if (MaxStackCount < 1 || MaxStackHeightMm < Outside.HeightMm || MaxSupportedMassG < 0 ||
                (MaxStackCount > 1 && string.IsNullOrWhiteSpace(StackGroup)) ||
                (StackRequiresLid && !HasLid)) errors.Add("invalid stack policy");
            return errors;
        }

        internal static bool ValidId(string id, string prefix)
        {
            return id != null && Regex.IsMatch(id, "^" + prefix + @"\.[a-z][a-z0-9_]*$");
        }
    }
}
