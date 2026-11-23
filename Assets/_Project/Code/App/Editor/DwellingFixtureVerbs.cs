#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The verb table</b> (ADR 0060 section 4): which component the dwelling prefab builder puts on a
    /// fixture anchor, keyed by the room sidecar's own action word. One row per verb. An anchor whose verb is
    /// not here is refused by the bake (<c>DwellingMeshBakeRules.KnownVerbs</c>, held equal to these keys by
    /// <c>DwellingPrefabBuilderTests</c>) and by the builder: an unknown verb is a refusal, not a silent prop.
    ///
    /// <para><b>The rig says what a fixture is; the Def holds its numbers</b> (rule 6). Capacities, rates,
    /// cold and deeds are read off the Def the anchor names. Where the room sidecar names no Def (the galley's
    /// stove, prep counter and sink publish none), <see cref="Row.FallbackDefId"/> is the shipped generic Def
    /// of that kind: what a stove is, not how this one is tuned.</para>
    ///
    /// <para><b>Two rows read differently from the ADR's list, on purpose.</b> <c>toilet</c> binds to the
    /// seat (<c>SitPoint</c>), not the wash station: <c>washstation.toilet</c> is a sit-only Def, which
    /// <c>WashStationPoint</c> refuses by design (the bathrooms slice's rule). And <c>step_out</c> adds no
    /// component: the door is the threshold <c>BuildingInterior</c> already crosses, so its child is a marker;
    /// the moving leaf is a facet part on the dwelling when its part exists (ADR 0060 section 4).</para>
    /// </summary>
    public static class DwellingFixtureVerbs
    {
        /// <summary>The component a verb becomes. Append-only.</summary>
        public enum Binding
        {
            Door = 0,            // a marker child; BuildingInterior is the door
            Bed = 1,             // InteriorBed, the player's bed
            CatchFreezer = 2,    // CamperFreezer (FreezerDef)
            Stowage = 3,         // StowageRack (StowageDef)
            FoodStore = 4,       // KitchenStoreFixture (KitchenStorageDef)
            CookingStation = 5,  // CookingStationFixture (CookingStationDef)
            WashStation = 6,     // WashStationPoint (WashStationDef)
            Seat = 7,            // SitPoint (a sit-only WashStationDef)
        }

        /// <summary>Whether a fixture is kitchen, bath or neither, for the wall rule.</summary>
        public enum Room { None = 0, Kitchen = 1, Bath = 2 }

        /// <summary>One verb.</summary>
        public readonly struct Row
        {
            public readonly string Verb;
            public readonly Binding Binding;
            public readonly string FallbackDefId;  // used only when the anchor names no Def; "" = none
            public readonly Room Room;

            public Row(string verb, Binding binding, string fallbackDefId, Room room)
            {
                Verb = verb; Binding = binding; FallbackDefId = fallbackDefId ?? ""; Room = room;
            }
        }

        static readonly Row[] _rows =
        {
            new Row("step_out", Binding.Door, "", Room.None),
            new Row("sleep_save", Binding.Bed, "", Room.None),
            new Row("freeze_catch", Binding.CatchFreezer, "", Room.None),
            new Row("storage", Binding.Stowage, "", Room.None),
            new Row("food_storage", Binding.FoodStore, "", Room.Kitchen),
            new Row("cook", Binding.CookingStation, "cooking_station.stove", Room.Kitchen),
            new Row("prep", Binding.CookingStation, "cooking_station.prep_counter", Room.Kitchen),
            new Row("wash_dishes", Binding.WashStation, "washstation.sink", Room.Kitchen),
            new Row("wash", Binding.WashStation, "", Room.Bath),
            new Row("bathe", Binding.WashStation, "", Room.Bath),
            new Row("toilet", Binding.Seat, "", Room.Bath),
            new Row("sit", Binding.Seat, "", Room.None),
        };

        /// <summary>Every row.</summary>
        public static IReadOnlyList<Row> Rows => _rows;

        /// <summary>The row for <paramref name="verb"/>, or false.</summary>
        public static bool TryGet(string verb, out Row row)
        {
            foreach (Row r in _rows)
                if (string.Equals(r.Verb, verb, StringComparison.Ordinal)) { row = r; return true; }
            row = default;
            return false;
        }

        /// <summary>The Def id a fixture binds to: the anchor's own when it names one, else the row's
        /// fallback ("" when neither).</summary>
        public static string DefIdFor(in DwellingFixtureAnchor anchor, in Row row) =>
            anchor.HasDefId ? anchor.DefId : row.FallbackDefId;
    }
}
#endif
