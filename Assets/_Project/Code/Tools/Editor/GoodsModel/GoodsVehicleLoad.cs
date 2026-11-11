using System;
using System.Collections.Generic;
using System.Linq;

namespace HiddenHarbours.Tools.GoodsModel
{
    [Serializable]
    public sealed class GoodsCarrierSpec
    {
        public string Id;
        public GoodsBox Bed;
        public long PayloadG;
        public int MaxStackCount;
        public int MaxLoadCentreMm; // 0 disables this constraint for beds; forks require rated data.
        public long MaxLoadMomentGmm;
        public string[] AcceptedContainers = Array.Empty<string>();
        public GoodsSlot[] Obstructions = Array.Empty<GoodsSlot>();

        public bool IsValid => GoodsContainerSpec.ValidId(Id, "cargo") && Bed.IsValid && PayloadG > 0 &&
            MaxStackCount > 0 && MaxLoadCentreMm >= 0 && MaxLoadMomentGmm >= 0 &&
            AcceptedContainers != null && AcceptedContainers.Length > 0 &&
            AcceptedContainers.All(id => GoodsContainerSpec.ValidId(id, "container")) &&
            AcceptedContainers.Distinct(StringComparer.Ordinal).Count() == AcceptedContainers.Length &&
            Obstructions != null && Obstructions.All(slot => slot.IsInside(Bed));
    }

    public sealed class GoodsVehicleLoad
    {
        private sealed class Placement
        {
            public string[] Ids;
            public GoodsSlot Bounds;
            public long MassG;
        }

        private readonly GoodsCarrierSpec _spec;
        private readonly GoodsHandLimits _limits;
        private readonly List<Placement> _loads = new List<Placement>();
        public long LoadedMassG => _loads.Sum(load => load.MassG);
        public int LoadCount => _loads.Count;

        public GoodsVehicleLoad(GoodsCarrierSpec spec, GoodsHandLimits limits)
        {
            if (spec == null || !spec.IsValid || limits == null || !limits.IsValid) throw new ArgumentException("Invalid carrier data");
            _spec = spec;
            _limits = limits;
        }

        // Local bed coordinates, z=0 is the measured support surface. No lift or automatic transfer occurs.
        public GoodsResult TryLoad(IReadOnlyList<GoodsStackMember> members, int xmm, int ymm, bool rotate)
        {
            var stack = GoodsStackRules.Assess(members, _limits);
            if (stack.Result != GoodsResult.Success) return stack.Result;
            if (members.Count > _spec.MaxStackCount || members.Any(m => !_spec.AcceptedContainers.Contains(m.Spec.Id)))
                return GoodsResult.Incompatible;
            if (members.Any(m => _loads.Any(load => load.Ids.Contains(m.InstanceId)))) return GoodsResult.Occupied;
            var envelope = stack.Envelope;
            if (rotate) envelope = new GoodsBox(envelope.WidthMm, envelope.LengthMm, envelope.HeightMm);
            var bounds = new GoodsSlot { Xmm = xmm, Ymm = ymm, Zmm = 0, Size = envelope };
            if (!bounds.IsInside(_spec.Bed)) return GoodsResult.DoesNotFit;
            if (_spec.Obstructions.Any(bounds.Overlaps) || _loads.Any(load => bounds.Overlaps(load.Bounds))) return GoodsResult.Occupied;
            if (stack.GrossMassG > _spec.PayloadG - LoadedMassG) return GoodsResult.TooHeavy;
            decimal centreMm = xmm + envelope.LengthMm / 2m;
            if ((_spec.MaxLoadCentreMm > 0 && centreMm > _spec.MaxLoadCentreMm) ||
                (_spec.MaxLoadMomentGmm > 0 && stack.GrossMassG * centreMm +
                    _loads.Sum(load => load.MassG * (load.Bounds.Xmm + load.Bounds.Size.LengthMm / 2m)) > _spec.MaxLoadMomentGmm))
                return GoodsResult.TooHeavy;
            _loads.Add(new Placement { Ids = stack.InstanceIds.ToArray(), Bounds = bounds, MassG = stack.GrossMassG });
            return GoodsResult.Success;
        }

        public GoodsResult Unload(string bottomId)
        {
            int index = _loads.FindIndex(load => load.Ids[0] == bottomId);
            if (index < 0) return GoodsResult.WrongSource;
            _loads.RemoveAt(index);
            return GoodsResult.Success;
        }
    }
}
