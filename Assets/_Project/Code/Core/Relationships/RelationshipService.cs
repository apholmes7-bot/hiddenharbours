using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b><see cref="IRelationshipService"/> over a <see cref="RelationshipGraph"/></b> (relationships slice 1),
    /// the <see cref="TraitService"/> shape: both data seams are injectable so a test drives it with its own
    /// ties and config; by default the ties are every <see cref="RelationshipTieDef"/> under
    /// <c>Resources/Relationships/Ties/</c> and the config is <c>Resources/Relationships/RelationshipConfig</c>,
    /// loaded once on first need. Ties are seeded in ordinal id order, so the load order cannot change the
    /// graph. A missing config means default thresholds and an empty rule table (nothing moves).
    /// </summary>
    public sealed class RelationshipService : IRelationshipService
    {
        /// <summary>The folder under any <c>Resources</c> root the relationship data lives in.</summary>
        public const string ResourcesFolder = "Relationships";

        /// <summary>The ties' folder, under <see cref="ResourcesFolder"/>.</summary>
        public const string TiesFolder = ResourcesFolder + "/Ties";

        /// <summary>The config asset's Resources path.</summary>
        public const string ConfigPath = ResourcesFolder + "/RelationshipConfig";

        private readonly Func<IEnumerable<RelationshipTieDef>> _ties;
        private readonly Func<RelationshipConfig> _config;
        private RelationshipGraph _graph;

        public RelationshipService(Func<IEnumerable<RelationshipTieDef>> ties = null,
            Func<RelationshipConfig> config = null)
        {
            _ties = ties ?? (() => Resources.LoadAll<RelationshipTieDef>(TiesFolder));
            _config = config ?? (() => Resources.Load<RelationshipConfig>(ConfigPath));
        }

        /// <summary>The graph behind the seam (built on first read). For tests and the slice-2 save.</summary>
        public RelationshipGraph Graph => _graph ??= Build();

        public RelationshipStage Stage(string a, string b) => Graph.Stage(a, b);

        public bool Record(in RelationshipEvent e) => Graph.Record(in e);

        public int ApplyDay()
        {
            List<RelationshipStageChanged> changes = Graph.ApplyDay();
            for (int i = 0; i < changes.Count; i++) EventBus.Publish(changes[i]);
            return changes.Count;
        }

        private RelationshipGraph Build()
        {
            RelationshipConfig config = _config();
            RelationshipStageThresholds thresholds = config != null ? config.Thresholds : RelationshipStageThresholds.Default;
            RelationshipRule[] rules = config != null && config.Rules != null ? config.Rules : Array.Empty<RelationshipRule>();

            var defs = new List<RelationshipTieDef>();
            IEnumerable<RelationshipTieDef> source = _ties();
            if (source != null)
                foreach (RelationshipTieDef def in source)
                    if (def != null && !string.IsNullOrEmpty(def.Id)) defs.Add(def);
            defs.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));

            var seeds = new List<RelationshipSeed>(defs.Count);
            foreach (RelationshipTieDef def in defs) seeds.Add(new RelationshipSeed(def.NpcA, def.NpcB, def.StartingStrength));
            return new RelationshipGraph(thresholds, rules, seeds);
        }
    }
}
