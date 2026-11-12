using System;
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>What the hand on deck is doing with a hauled pot at an instant.</summary>
    public enum NpcDeckStep
    {
        /// <summary>Nothing in hand: before the pot is aboard, or once it is baited and waiting to go over.</summary>
        Idle,
        /// <summary>Taking an animal out of the pot and putting the gauge on it.</summary>
        Picking,
        /// <summary>Banding a keeper's claws.</summary>
        Banding,
        /// <summary>Carrying a banded keeper to its crate.</summary>
        Stowing,
        /// <summary>Putting a short or a berried one back over the side.</summary>
        Returning,
        /// <summary>The pot is empty: baiting it for the next set.</summary>
        Baiting,
    }

    /// <summary>One animal that came up in an NPC pot, with its gauge reading already taken.</summary>
    public readonly struct NpcPotAnimal
    {
        public readonly CatchItem Item;
        public readonly float SizeMm;
        public readonly bool Berried;
        /// <summary>Legal and not berried: it is banded and stowed. Otherwise it goes back.</summary>
        public readonly bool Keeper;
        /// <summary>Which crate a keeper goes in, 0 = smallest grade. -1 for an animal that goes back.</summary>
        public readonly int Grade;
        /// <summary>Clock second this animal comes out of the pot.</summary>
        public readonly double PickedAt;
        /// <summary>Clock second it is in its crate (a keeper) or over the side (not).</summary>
        public readonly double DoneAt;
        /// <summary>The deck sprite the player's own deck work shows for this species, or null.</summary>
        public readonly Sprite Sprite;

        public NpcPotAnimal(CatchItem item, float sizeMm, bool berried, bool keeper, int grade,
                            double pickedAt, double doneAt, Sprite sprite)
        {
            Item = item; SizeMm = sizeMm; Berried = berried; Keeper = keeper; Grade = grade;
            PickedAt = pickedAt; DoneAt = doneAt; Sprite = sprite;
        }
    }

    /// <summary>The deck job at one instant.</summary>
    public readonly struct NpcDeckMoment
    {
        public readonly NpcDeckStep Step;
        /// <summary>Index into the pot's animals, or -1.</summary>
        public readonly int Animal;
        public readonly float Step01;

        public NpcDeckMoment(NpcDeckStep step, int animal, float step01)
        {
            Step = step; Animal = animal; Step01 = step01;
        }
    }

    /// <summary>
    /// <b>What one NPC boat's pots hold today and what becomes of each animal on deck</b>, as a pure function of
    /// the day (rule 5). The catch comes off the same tables and the same seeded streams as a player's pot
    /// (<see cref="TrapFill"/>, <see cref="PlacedTrapCatch"/>, <see cref="DeckPot"/>): a pot that soaked
    /// since yesterday's set, hauled at today's haul, read on the trap's own deck-work rules. Then each animal
    /// takes a fixed share of the deck time, in order: picked and gauged; a keeper is banded and carried to
    /// the crate for its grade; a short or berried one goes back over the side; last, the pot is baited.
    ///
    /// <para>Nothing is saved and nothing is drawn from a hidden stream: the crate's contents at any clock
    /// second are a count over the day's plan.</para>
    /// </summary>
    public sealed class NpcPotDayCatch
    {
        private NpcPotAnimal[][] _pots = Array.Empty<NpcPotAnimal[]>();
        private double[] _deckStart = Array.Empty<double>();
        private double[] _baitStart = Array.Empty<double>();
        private double[] _baitEnd = Array.Empty<double>();
        private float _pick, _band, _stow, _return;

        public int DayIndex { get; private set; }
        public int PotCount => _pots.Length;
        public int KeptCount { get; private set; }
        public int ReturnedCount { get; private set; }
        public float KeptKg { get; private set; }

        /// <summary>The animals pot <paramref name="pot"/> came up with, in the order they are worked.</summary>
        public IReadOnlyList<NpcPotAnimal> Animals(int pot)
            => pot >= 0 && pot < _pots.Length ? _pots[pot] : Array.Empty<NpcPotAnimal>();

        /// <summary>Longest the deck job can take on a full pot of keepers. The Def's deck time must cover it.</summary>
        public static float WorstCaseDeckSeconds(NpcPotDayDef def)
            => def == null || def.Trap == null
                ? 0f
                : Mathf.Max(1, def.Trap.CapacityUnits) * (def.PickSeconds + def.BandSeconds + def.StowSeconds) + def.BaitSeconds;

        /// <summary>The crate a keeper of <paramref name="sizeMm"/> goes in: how many of the breaks it reaches.</summary>
        public static int GradeOf(float sizeMm, float[] gradeBreaksMm)
        {
            int grade = 0;
            if (gradeBreaksMm == null) return grade;
            for (int i = 0; i < gradeBreaksMm.Length; i++)
                if (sizeMm >= gradeBreaksMm[i]) grade++;
            return grade;
        }

        /// <summary>
        /// Resolves the day's catch. <paramref name="tideHeightAt"/> is the water level at a clock second
        /// (itself a function of seed and time); <paramref name="season"/> is the season of the day.
        /// </summary>
        public static NpcPotDayCatch Build(NpcPotDayDef def, int worldSeed, NpcPotDay today, NpcPotDay yesterday,
                                           Season season, Func<double, float> tideHeightAt, double secondsPerDay)
        {
            var result = new NpcPotDayCatch { DayIndex = today != null ? today.DayIndex : 0 };
            if (def == null || def.Trap == null || today == null || yesterday == null
                || today.PotCount == 0 || yesterday.PotCount != today.PotCount)
                return result;

            TrapDef trap = def.Trap;
            int n = today.PotCount;
            result._pots = new NpcPotAnimal[n][];
            result._deckStart = new double[n];
            result._baitStart = new double[n];
            result._baitEnd = new double[n];
            result._pick = def.PickSeconds;
            result._band = def.BandSeconds;
            result._stow = def.StowSeconds;
            result._return = def.ReturnSeconds;

            List<FishSpeciesDef> pool = FishSpeciesRegistry.Resolve(trap.AllowedCatchFishIds);
            IReadOnlyList<string> favours = def.Bait != null ? def.Bait.FavorsSpeciesIds : null;
            var items = new List<CatchItem>(Mathf.Max(1, trap.CapacityUnits));

            for (int i = 0; i < n; i++)
            {
                double setAt = yesterday.SetEnd[i];
                double hauledAt = today.HaulStart[i];
                string id = NpcPotDay.PotInstanceId(def, i);
                items.Clear();

                if (pool.Count > 0 && TrapSoak.IsReady(setAt, hauledAt, trap.SoakHours))
                {
                    float hour = secondsPerDay > 0.0
                        ? (float)((hauledAt - today.DayStartSeconds) / secondsPerDay * 24.0)
                        : 0f;
                    float tide = tideHeightAt != null ? tideHeightAt(hauledAt) : 0f;
                    var ctx = new CatchContext(def.CatchRegionId, tide, hour, season, Gear.Trap);
                    float fill = TrapFill.Fill01(setAt, hauledAt, trap.SoakHours, trap.HoursToFullPot);
                    int count = TrapFill.ResolveCount(fill, trap.CapacityUnits, worldSeed, id, setAt);
                    PlacedTrapCatch.ResolveMany(pool, in ctx, favours, PlacedTrapCatch.BaitFavourMultiplier, count,
                                                worldSeed, id, setAt, items);
                }

                // The player's own deck pot reads size, berried and keeper off the trap's rules: one reading, one truth.
                var deck = new DeckPot(trap, trap.DeckWork, id, setAt, worldSeed, items);
                var animals = new NpcPotAnimal[deck.Animals.Count];
                double t = today.HaulEnd[i];
                result._deckStart[i] = t;
                for (int k = 0; k < animals.Length; k++)
                {
                    DeckPot.Animal a = deck.Animals[k];
                    double picked = t;
                    t += a.Keeper ? def.PickSeconds + def.BandSeconds + def.StowSeconds
                                  : def.PickSeconds + def.ReturnSeconds;
                    int grade = a.Keeper ? GradeOf(a.SizeMm, def.GradeBreaksMm) : -1;
                    animals[k] = new NpcPotAnimal(a.Item, a.SizeMm, a.Berried, a.Keeper, grade, picked, t, a.SpriteOverride);
                    if (a.Keeper) { result.KeptCount++; result.KeptKg += a.Item.WeightKg; }
                    else result.ReturnedCount++;
                }
                result._pots[i] = animals;
                result._baitStart[i] = t;
                result._baitEnd[i] = t + def.BaitSeconds;
            }
            return result;
        }

        /// <summary>What the hand is doing with pot <paramref name="pot"/> at <paramref name="totalSeconds"/>.</summary>
        public NpcDeckMoment MomentAt(int pot, double totalSeconds)
        {
            if (pot < 0 || pot >= _pots.Length || totalSeconds < _deckStart[pot])
                return new NpcDeckMoment(NpcDeckStep.Idle, -1, 0f);

            NpcPotAnimal[] animals = _pots[pot];
            for (int k = 0; k < animals.Length; k++)
            {
                if (totalSeconds >= animals[k].DoneAt) continue;
                double into = totalSeconds - animals[k].PickedAt;
                if (into < _pick) return new NpcDeckMoment(NpcDeckStep.Picking, k, Unit(into, _pick));
                into -= _pick;
                if (!animals[k].Keeper) return new NpcDeckMoment(NpcDeckStep.Returning, k, Unit(into, _return));
                if (into < _band) return new NpcDeckMoment(NpcDeckStep.Banding, k, Unit(into, _band));
                return new NpcDeckMoment(NpcDeckStep.Stowing, k, Unit(into - _band, _stow));
            }

            if (totalSeconds < _baitEnd[pot])
                return new NpcDeckMoment(NpcDeckStep.Baiting, -1, Unit(totalSeconds - _baitStart[pot], _baitEnd[pot] - _baitStart[pot]));
            return new NpcDeckMoment(NpcDeckStep.Idle, -1, 0f);
        }

        /// <summary>Clock second pot <paramref name="pot"/> is baited and ready to go back over.</summary>
        public double ReadyToSetAt(int pot) => pot >= 0 && pot < _baitEnd.Length ? _baitEnd[pot] : 0.0;

        /// <summary>Keepers in the crates at <paramref name="totalSeconds"/>; <paramref name="grade"/> -1 counts every grade.</summary>
        public int StowedAt(double totalSeconds, int grade = -1)
        {
            int count = 0;
            for (int i = 0; i < _pots.Length; i++)
            {
                if (totalSeconds < _deckStart[i]) break;   // pots are worked in order
                NpcPotAnimal[] animals = _pots[i];
                for (int k = 0; k < animals.Length; k++)
                {
                    if (!animals[k].Keeper || totalSeconds < animals[k].DoneAt) continue;
                    if (grade < 0 || animals[k].Grade == grade) count++;
                }
            }
            return count;
        }

        /// <summary>Weight in the crates at <paramref name="totalSeconds"/>, kg.</summary>
        public float StowedKgAt(double totalSeconds)
        {
            float kg = 0f;
            for (int i = 0; i < _pots.Length; i++)
            {
                if (totalSeconds < _deckStart[i]) break;
                NpcPotAnimal[] animals = _pots[i];
                for (int k = 0; k < animals.Length; k++)
                    if (animals[k].Keeper && totalSeconds >= animals[k].DoneAt) kg += animals[k].Item.WeightKg;
            }
            return kg;
        }

        private static float Unit(double into, double span)
            => span > 1e-9 ? Mathf.Clamp01((float)(into / span)) : 1f;
    }
}
