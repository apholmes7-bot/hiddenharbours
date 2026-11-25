using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy.SupplyChain
{
    /// <summary>A counted lot of equal-sized individuals. Product units retain source-fish provenance.</summary>
    public struct FishLot
    {
        public string SpeciesId;
        public FishProduct Product;
        public int Count, IndividualRawGrams, IndividualGrams, IndividualVolumeMl;

        public FishLot(string speciesId, int count, int individualGrams, int individualVolumeMl)
        {
            SpeciesId = speciesId; Count = count; IndividualRawGrams = individualGrams;
            IndividualGrams = individualGrams; IndividualVolumeMl = individualVolumeMl; Product = FishProduct.Raw;
        }

        public long MassGrams => (long)Count * IndividualGrams;
        public long VolumeMl => (long)Count * IndividualVolumeMl;
        public FishLot WithCount(int count) { FishLot lot = this; lot.Count = count; return lot; }
        public bool SameType(in FishLot other) => SpeciesId == other.SpeciesId && Product == other.Product &&
            IndividualGrams == other.IndividualGrams && IndividualRawGrams == other.IndividualRawGrams &&
            IndividualVolumeMl == other.IndividualVolumeMl;
    }

    /// <summary>Allocated once; all later inventory operations are bounded scans over value types.</summary>
    internal sealed class FishLotStore
    {
        internal readonly FishStorageSpec Spec;
        internal readonly FishLot[] Lots;
        internal long MassGrams, VolumeMl;

        internal FishLotStore(FishStorageSpec spec) { Spec = spec; Lots = new FishLot[spec.LotSlots]; }

        internal int Fits(in FishLot lot)
        {
            if (lot.Count <= 0 || lot.IndividualGrams <= 0 || lot.IndividualVolumeMl <= 0) return 0;
            bool slot = false;
            for (int i = 0; i < Lots.Length; i++)
                if (Lots[i].Count == 0 || (Lots[i].SameType(lot) && Lots[i].Count <= int.MaxValue - lot.Count))
                { slot = true; break; }
            if (!slot) return 0;
            return (int)Math.Min(lot.Count, Math.Min((Spec.MassCapacityGrams - MassGrams) / lot.IndividualGrams,
                (Spec.VolumeCapacityMl - VolumeMl) / lot.IndividualVolumeMl));
        }

        internal void Add(in FishLot lot)
        {
            for (int i = 0; i < Lots.Length; i++)
                if (Lots[i].Count > 0 && Lots[i].SameType(lot) && Lots[i].Count <= int.MaxValue - lot.Count)
                { Lots[i].Count += lot.Count; MassGrams += lot.MassGrams; VolumeMl += lot.VolumeMl; return; }
            for (int i = 0; i < Lots.Length; i++)
                if (Lots[i].Count == 0)
                { Lots[i] = lot; MassGrams += lot.MassGrams; VolumeMl += lot.VolumeMl; return; }
            throw new InvalidOperationException("Capacity must be checked before adding a lot.");
        }

        internal FishLot Remove(int index, int count)
        {
            FishLot lot = Lots[index].WithCount(count);
            Lots[index].Count -= count; MassGrams -= lot.MassGrams; VolumeMl -= lot.VolumeMl;
            return lot;
        }

        internal long CountOf(string species, FishProduct? product = null)
        {
            long count = 0;
            for (int i = 0; i < Lots.Length; i++)
                if (Lots[i].SpeciesId == species && (!product.HasValue || Lots[i].Product == product.Value)) count += Lots[i].Count;
            return count;
        }
    }
}
