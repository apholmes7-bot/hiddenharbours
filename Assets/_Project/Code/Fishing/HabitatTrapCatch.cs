using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>Opt-in, single-species composition of the existing fill and catch path. Callers still
    /// haul and pass individual items to DeckPot. No installed caller, save or population mutation.</summary>
    public static class HabitatTrapCatch
    {
        /// <summary>Ready-pot extra slots use placement-time density so later migration cannot uncatch
        /// an animal. The caller supplies TrapFill.Fill01 and gates readiness before calling.</summary>
        public static int Count(float fill01, int capacity, IBottomMap bottom, Vector2 position,
            in TrapHabitatSettings habitat, int worldSeed, string instanceId, double placedAt)
        {
            if (!habitat.IsValid || bottom == null || !TrapHabitatSettings.Finite(position)
                || !BottomMapSettings.Finite(fill01) || !BottomMapSettings.Finite(placedAt) || placedAt < 0d
                || capacity < 1 || string.IsNullOrEmpty(instanceId))
                return 0;
            BottomType type = bottom.BottomAt(position);
            if (type != BottomType.Rock && type != BottomType.Mud && type != BottomType.Sand) return 0;
            float density = TrapHabitat.DensityAt(bottom, position, in habitat, worldSeed, placedAt);
            float effectiveFill = Math.Max(0f, Math.Min(1f, fill01)) * density;
            return TrapFill.ResolveCount(effectiveFill, capacity, worldSeed, instanceId, placedAt);
        }

        /// <summary>Haul-time allocation behavior is the existing resolver's. Species, bait, season,
        /// gear and per-animal seeds are retained. A mixed pool is rejected before resolving anything.</summary>
        public static int Resolve(TrapDef trap, IReadOnlyList<FishSpeciesDef> pool, in CatchContext context,
            IReadOnlyList<string> baitFavours, int favourMultiplier, IBottomMap bottom, Vector2 position,
            in TrapHabitatSettings habitat, int worldSeed, string instanceId, double placedAt,
            double hauledAt, List<CatchItem> results)
        {
            if (results == null) return 0;
            results.Clear();
            if (trap == null || pool == null || pool.Count != 1 || pool[0] == null || !habitat.IsValid
                || !string.Equals(pool[0].Id, habitat.SpeciesId, StringComparison.Ordinal)
                || !Allows(trap.AllowedCatchFishIds, habitat.SpeciesId)
                || !pool[0].GearAllowed(Gear.Trap) || context.Gear != Gear.Trap
                || !BottomMapSettings.Finite(placedAt) || !BottomMapSettings.Finite(hauledAt)
                || placedAt < 0d || hauledAt < placedAt
                || !TrapSoak.IsReady(placedAt, hauledAt, trap.SoakHours)) return 0;
            float fill = TrapFill.Fill01(placedAt, hauledAt, trap.SoakHours, trap.HoursToFullPot);
            int count = Count(fill, trap.CapacityUnits, bottom, position, in habitat, worldSeed, instanceId, placedAt);
            return PlacedTrapCatch.ResolveMany(pool, in context, baitFavours, favourMultiplier, count,
                worldSeed, instanceId, placedAt, results);
        }

        private static bool Allows(string[] ids, string speciesId)
        {
            if (ids == null) return false;
            for (int i = 0; i < ids.Length; i++)
                if (string.Equals(ids[i], speciesId, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
