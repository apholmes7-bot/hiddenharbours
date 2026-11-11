using System;
using System.Collections.Generic;
using System.Linq;

namespace HiddenHarbours.Tools.GoodsModel
{
    public sealed class GoodsStackMember
    {
        public string InstanceId { get; }
        public GoodsContainerSpec Spec { get; }
        public long ContentsMassG { get; }
        public bool LidClosed { get; }
        public long GrossMassG => checked(Spec.EmptyMassG + ContentsMassG + (LidClosed ? Spec.LidMassG : 0));

        public GoodsStackMember(string instanceId, GoodsContainerSpec spec, long contentsMassG, bool lidClosed)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || spec == null || spec.Validate().Count != 0 ||
                contentsMassG < 0 || (lidClosed && !spec.HasLid)) throw new ArgumentException("Invalid stack member");
            InstanceId = instanceId;
            Spec = spec;
            ContentsMassG = contentsMassG;
            LidClosed = lidClosed;
        }
    }

    public sealed class GoodsStackAssessment
    {
        public GoodsResult Result { get; internal set; }
        public long GrossMassG { get; internal set; }
        public int HeightMm { get; internal set; }
        public GoodsBox Envelope { get; internal set; }
        public bool MayCarryByHand { get; internal set; }
        public IReadOnlyList<string> InstanceIds { get; internal set; } = Array.Empty<string>();
    }

    public static class GoodsStackRules
    {
        // Bottom first. This assesses a support chain; it does not invent nesting or lift machinery.
        public static GoodsStackAssessment Assess(IReadOnlyList<GoodsStackMember> members, GoodsHandLimits limits)
        {
            if (members == null || members.Count == 0 || members.Any(m => m == null) || limits == null || !limits.IsValid)
                throw new ArgumentException("Missing stack or hand data");
            var result = new GoodsStackAssessment { Result = GoodsResult.InvalidStack };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            long massAbove = 0;
            long height = 0;
            for (int i = members.Count - 1; i >= 0; i--)
            {
                var member = members[i];
                var spec = member.Spec;
                if (!ids.Add(member.InstanceId) || member.GrossMassG > spec.MaxGrossMassG ||
                    members.Count > spec.MaxStackCount || massAbove > spec.MaxSupportedMassG) return result;
                if (i < members.Count - 1)
                {
                    var upper = members[i + 1].Spec;
                    if (string.IsNullOrWhiteSpace(spec.StackGroup) || spec.StackGroup != upper.StackGroup ||
                        (spec.StackRequiresLid && !member.LidClosed) ||
                        upper.Outside.LengthMm > spec.Outside.LengthMm || upper.Outside.WidthMm > spec.Outside.WidthMm)
                        return result;
                }
                massAbove = checked(massAbove + member.GrossMassG);
                height += spec.Outside.HeightMm;
            }
            if (height > int.MaxValue || members.Any(m => height > m.Spec.MaxStackHeightMm)) return result;
            result.Result = GoodsResult.Success;
            result.GrossMassG = massAbove;
            result.HeightMm = (int)height;
            result.Envelope = new GoodsBox(members[0].Spec.Outside.LengthMm, members[0].Spec.Outside.WidthMm, (int)height);
            result.InstanceIds = Array.AsReadOnly(members.Select(m => m.InstanceId).ToArray());
            result.MayCarryByHand = result.Envelope.Fits(limits.TwoHandEnvelope) && massAbove <= limits.TwoHandMassG &&
                members.All(m => m.Spec.RequiredHands > 0 && massAbove <= m.Spec.MaxHandGrossMassG) &&
                (members.Count == 1 || members.All(m => m.Spec.AllowHandCarriedStack));
            return result;
        }

        public static GoodsResult AccessMember(int bottomFirstIndex, int count)
        {
            if (bottomFirstIndex < 0 || bottomFirstIndex >= count) return GoodsResult.MissingItem;
            return bottomFirstIndex == count - 1 ? GoodsResult.Success : GoodsResult.SupportedByOthers;
        }
    }
}
