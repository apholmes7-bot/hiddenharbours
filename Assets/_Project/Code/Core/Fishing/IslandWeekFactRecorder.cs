using System;

namespace HiddenHarbours.Core
{
    /// <summary>Records two historical facts in the existing flag list; no separate saved state.</summary>
    public sealed class IslandWeekFactRecorder
    {
        public const string FishFromDoryKey = "st_peters.fact.fish_from_dory";
        public const string ShellfishSoldKey = "st_peters.fact.shellfish_sold";

        private readonly string _doryId;
        private readonly FishCategory[] _finfishCategories;

        public IslandWeekFactRecorder(IslandWeekRulesDef rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            string error = rules.ValidationError();
            if (error != null) throw new ArgumentException(error, nameof(rules));
            _doryId = rules.DoryId;
            _finfishCategories = (FishCategory[])rules.FinfishCategories.Clone();
        }

        /// <summary>Consumes landing provenance, never the notebook's bounded transfer history.</summary>
        public void RecordKeptCatch(ISaveService save, in FreshCatchKept landing)
        {
            CatchItem item = landing.Item;
            if (landing.LandingHullId != _doryId || !landing.HullWasRepaired || string.IsNullOrEmpty(item.SpeciesId)
                || Array.IndexOf(_finfishCategories, item.Category) < 0) return;
            SetOnce(save, FishFromDoryKey);
        }

        public void RecordSale(ISaveService save, in CatchSold sale)
        {
            if (sale.Count > 0 && sale.ShellfishCount > 0 && sale.ShellfishCount <= sale.Count)
                SetOnce(save, ShellfishSoldKey);
        }

        private static void SetOnce(ISaveService save, string key)
        {
            if (save?.Current != null && !save.GetFlag(key)) save.SetFlag(key, true);
        }
    }
}
