using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Shared fixtures for the trait tests: test-made <see cref="TraitDef"/>s (so the maths never depends on
    /// the owner's tuned numbers in the shipped assets) and an in-memory save.
    /// </summary>
    internal sealed class TraitFixtures
    {
        private readonly List<Object> _made = new List<Object>();

        public TraitDef Make(string id, params TraitModifier[] modifiers)
        {
            var def = ScriptableObject.CreateInstance<TraitDef>();
            def.Id = id;
            def.DisplayName = id;
            def.Modifiers = modifiers;
            _made.Add(def);
            return def;
        }

        public static TraitModifier Add(TraitStat stat, float value)
            => new TraitModifier(stat, TraitModifierKind.Additive, value);

        public static TraitModifier Mul(TraitStat stat, float value)
            => new TraitModifier(stat, TraitModifierKind.Multiplier, value);

        /// <summary>The three slice-1 traits at the plan's starting numbers, made in the test.</summary>
        public List<TraitDef> SliceOne() => new List<TraitDef>
        {
            Make("trait.speedwalker", Add(TraitStat.WalkSpeed, 0.08f)),
            Make("trait.charismatic", Add(TraitStat.SellPrice, 0.05f)),
            Make("trait.patient_angler", Add(TraitStat.BiteWait, -0.1f)),
        };

        public void DestroyAll()
        {
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        /// <summary>Install a <see cref="TraitService"/> over <paramref name="save"/> and
        /// <paramref name="catalog"/> as <see cref="GameServices.Traits"/>, with the player holding
        /// <paramref name="ids"/>.</summary>
        public static TraitService Install(MemorySave save, IEnumerable<TraitDef> catalog, params string[] ids)
        {
            save.Current.PlayerTraits = new List<string>(ids);
            var service = new TraitService(() => save, () => catalog);
            GameServices.Save = save;
            GameServices.Traits = service;
            return service;
        }

        internal sealed class MemorySave : ISaveService
        {
            public SaveData Current { get; } = SaveMigration.NewGame();
            public int Saves;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() => Saves++;
        }
    }
}
