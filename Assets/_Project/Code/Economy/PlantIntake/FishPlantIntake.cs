using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy.PlantIntake
{
    /// <summary>One fish already delivered by hand. The future physical adapter owns authentic receipts.</summary>
    public readonly struct DeliveredCatchReceipt
    {
        public readonly string ReceiptId, SpeciesId;
        public readonly int WeightGrams, FreshnessPpm;
        public readonly long GameSeconds;

        public DeliveredCatchReceipt(string receiptId, string speciesId, int weightGrams, int freshnessPpm,
            long gameSeconds)
        {
            ReceiptId = receiptId; SpeciesId = speciesId; WeightGrams = weightGrams;
            FreshnessPpm = freshnessPpm; GameSeconds = gameSeconds;
        }
    }

    public sealed class GradedCatchLot
    {
        public DeliveredCatchReceipt Source { get; }
        public string GradeId { get; }
        public long Payment { get; }
        public bool Loaded { get; private set; }

        internal GradedCatchLot(in DeliveredCatchReceipt source, FishGradeSpec grade)
        {
            Source = source; GradeId = grade.Id;
            Payment = (long)source.WeightGrams * grade.PricePerKg / FishPlantUnits.GramsPerKg;
        }

        internal bool TryLoad()
        {
            if (Loaded) return false;
            Loaded = true;
            return true;
        }
    }

    /// <summary>Session-only settlement ledger. Confine this and its lots to the simulation thread.</summary>
    public sealed class FishPlantIntake
    {
        private readonly FishIntakeSpec _spec;
        private readonly HashSet<string> _accepted = new HashSet<string>(StringComparer.Ordinal);

        public FishPlantIntake(FishIntakeSpec spec)
            => _spec = spec ?? throw new ArgumentNullException(nameof(spec));

        public bool TryAccept(in DeliveredCatchReceipt receipt, out GradedCatchLot lot)
        {
            lot = null;
            if (string.IsNullOrWhiteSpace(receipt.ReceiptId) || receipt.GameSeconds < 0 ||
                _accepted.Contains(receipt.ReceiptId)) return false;
            FishGradeSpec grade = _spec.Grade(receipt.SpeciesId, receipt.WeightGrams, receipt.FreshnessPpm);
            if (grade == null) return false;
            lot = new GradedCatchLot(receipt, grade);
            _accepted.Add(receipt.ReceiptId);
            return true;
        }
    }

    public readonly struct GradedFishProduct
    {
        public readonly GradedCatchLot SourceLot;
        public readonly FishProduct Product;
        public readonly int ProductGrams, ByproductGrams;

        internal GradedFishProduct(GradedCatchLot lot, GradedFishLineSpec spec)
        {
            SourceLot = lot; Product = spec.Product;
            ProductGrams = (int)((long)lot.Source.WeightGrams * spec.YieldPpm / FishPlantUnits.PartsPerMillion);
            ByproductGrams = lot.Source.WeightGrams - ProductGrams;
        }
    }

    /// <summary>One explicitly loaded slot; output stays here until collection. No transport or hidden clock.</summary>
    public sealed class GradedFishLine
    {
        private readonly GradedFishLineSpec _spec;
        private GradedCatchLot _lot;
        private long _now;
        public long ReadyAtGameSeconds { get; private set; }
        public bool HasOutput => _lot != null && _now - _now % _spec.SlowTickSeconds >= ReadyAtGameSeconds;

        public GradedFishLine(GradedFishLineSpec spec)
            => _spec = spec ?? throw new ArgumentNullException(nameof(spec));

        public void AdvanceTo(long gameSeconds)
        {
            if (gameSeconds < _now) throw new ArgumentOutOfRangeException(nameof(gameSeconds), "Time reversal.");
            _now = gameSeconds;
        }

        /// <summary>Acknowledge a completed hand load into this supported slot; never searches remote inventory.</summary>
        public bool TryLoad(GradedCatchLot lot, long gameSeconds)
        {
            AdvanceTo(gameSeconds);
            if (_lot != null || lot == null || lot.Loaded || lot.Source.GameSeconds > gameSeconds ||
                lot.Source.SpeciesId != _spec.SpeciesId || lot.Source.WeightGrams > _spec.MaximumInputGrams) return false;
            long ready = checked(gameSeconds + _spec.ProcessGameSeconds);
            long remainder = ready % _spec.SlowTickSeconds;
            if (remainder != 0) ready = checked(ready + _spec.SlowTickSeconds - remainder);
            if (!lot.TryLoad()) return false;
            _lot = lot;
            ReadyAtGameSeconds = ready;
            return true;
        }

        /// <summary>The caller must acknowledge completed physical collection before using this in play.</summary>
        public bool TryCollect(out GradedFishProduct product)
        {
            product = default;
            if (!HasOutput) return false;
            product = new GradedFishProduct(_lot, _spec);
            _lot = null;
            return true;
        }
    }
}
