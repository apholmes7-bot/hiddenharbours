using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b><see cref="ITraitService"/> over the save and the shipped Defs</b> (traits slice 1). The
    /// <see cref="PlayerAppearance"/> shape: both seams are injectable, so a test drives it with its own
    /// save and its own Defs; <paramref name="save"/> defaults to <see cref="GameServices.Save"/> (read at
    /// each call, since it self-installs) and the catalog to every <see cref="TraitDef"/> under
    /// <c>Resources/Traits/</c>, loaded once on first need.
    ///
    /// <para><b>The hot path.</b> A player with no traits answers <see cref="TraitMath.Identity"/> without
    /// touching the catalog. Otherwise the per-stat factors are cached against a copy of the id list they
    /// were built from; a read compares the saved list to that copy (no allocation) and rebuilds only when
    /// it changed, so a loaded or swapped save is picked up without any event wiring.</para>
    /// </summary>
    public sealed class TraitService : ITraitService
    {
        /// <summary>The folder under any <c>Resources</c> root the shipped traits live in.</summary>
        public const string ResourcesFolder = "Traits";

        private static readonly int StatSlots = MaxStat() + 1;

        private readonly Func<ISaveService> _save;
        private readonly Func<IEnumerable<TraitDef>> _catalogSource;
        private List<TraitDef> _all;
        private Dictionary<string, TraitDef> _byId;

        private readonly List<string> _cachedIds = new List<string>();
        private readonly float[] _cachedFactors = new float[StatSlots];
        private bool _cacheValid;
        private readonly List<TraitDef> _scratch = new List<TraitDef>();

        public TraitService(Func<ISaveService> save = null, Func<IEnumerable<TraitDef>> catalog = null)
        {
            _save = save ?? (() => GameServices.Save);
            _catalogSource = catalog ?? (() => Resources.LoadAll<TraitDef>(ResourcesFolder));
        }

        public IReadOnlyList<TraitDef> All
        {
            get { EnsureCatalog(); return _all; }
        }

        public TraitDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureCatalog();
            return _byId.TryGetValue(id, out TraitDef def) ? def : null;
        }

        public IReadOnlyList<string> PlayerTraitIds
            => (IReadOnlyList<string>)_save()?.Current?.PlayerTraits ?? Array.Empty<string>();

        public float PlayerMultiplier(TraitStat stat)
        {
            List<string> ids = _save()?.Current?.PlayerTraits;
            if (ids == null || ids.Count == 0) return TraitMath.Identity;
            int slot = (int)stat;
            if (slot < 0 || slot >= StatSlots) return TraitMath.Identity;
            if (!_cacheValid || !SameIds(ids, _cachedIds)) Rebuild(ids);
            return _cachedFactors[slot];
        }

        public float MultiplierFor(TraitStat stat, IReadOnlyList<string> traitIds)
        {
            if (traitIds == null || traitIds.Count == 0) return TraitMath.Identity;
            Resolve(traitIds, _scratch);
            float factor = TraitMath.Combine(stat, _scratch);
            _scratch.Clear();
            return factor;
        }

        public bool SetPlayerTraits(IReadOnlyList<string> traitIds, out string refusal)
        {
            ISaveService save = _save();
            if (save?.Current == null)
            {
                refusal = "No game is loaded, so there is nowhere to keep your traits.";
                return false;
            }
            refusal = Check(traitIds);
            if (refusal != null) return false;

            List<string> held = save.Current.PlayerTraits;
            if (held != null && SameIds(traitIds ?? Array.Empty<string>(), held)) return true;

            var kept = new List<string>();
            if (traitIds != null) kept.AddRange(traitIds);
            save.Current.PlayerTraits = kept;
            _cacheValid = false;
            save.Save();
            EventBus.Publish(new PlayerTraitsChanged());
            return true;
        }

        private string Check(IReadOnlyList<string> traitIds)
        {
            if (traitIds == null) return null;
            int picks = GameServices.TraitPicksPerCharacter;
            if (traitIds.Count > picks) return $"Please pick no more than {picks} traits.";
            for (int i = 0; i < traitIds.Count; i++)
            {
                if (Find(traitIds[i]) == null) return $"There is no trait called '{traitIds[i]}'.";
                for (int j = 0; j < i; j++)
                    if (string.Equals(traitIds[i], traitIds[j], StringComparison.Ordinal))
                        return "Each trait can only be picked once.";
            }
            return null;
        }

        private void Rebuild(List<string> ids)
        {
            Resolve(ids, _scratch);
            for (int s = 0; s < StatSlots; s++)
                _cachedFactors[s] = TraitMath.Combine((TraitStat)s, _scratch);
            _scratch.Clear();
            _cachedIds.Clear();
            _cachedIds.AddRange(ids);
            _cacheValid = true;
        }

        /// <summary>Known, distinct ids, the first <see cref="GameServices.TraitPicksPerCharacter"/> of
        /// them in saved order, then sorted ordinal by id so the product is order-free.</summary>
        private void Resolve(IReadOnlyList<string> ids, List<TraitDef> into)
        {
            into.Clear();
            int picks = GameServices.TraitPicksPerCharacter;
            for (int i = 0; i < ids.Count && into.Count < picks; i++)
            {
                TraitDef def = Find(ids[i]);
                if (def != null && !into.Contains(def)) into.Add(def);
            }
            into.Sort(ById.Instance);
        }

        private void EnsureCatalog()
        {
            if (_all != null) return;
            _all = new List<TraitDef>();
            _byId = new Dictionary<string, TraitDef>(StringComparer.Ordinal);
            IEnumerable<TraitDef> source = _catalogSource();
            if (source != null)
                foreach (TraitDef def in source)
                {
                    // First by id wins; the content validation test fails a duplicate id loudly.
                    if (def == null || string.IsNullOrEmpty(def.Id) || _byId.ContainsKey(def.Id)) continue;
                    _byId.Add(def.Id, def);
                    _all.Add(def);
                }
            _all.Sort(ById.Instance);
        }

        private static bool SameIds(IReadOnlyList<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
            return true;
        }

        private static int MaxStat()
        {
            int max = 0;
            foreach (TraitStat s in Enum.GetValues(typeof(TraitStat))) max = Math.Max(max, (int)s);
            return max;
        }

        private sealed class ById : IComparer<TraitDef>
        {
            public static readonly ById Instance = new ById();
            public int Compare(TraitDef x, TraitDef y) => string.CompareOrdinal(x.Id, y.Id);
        }
    }
}
