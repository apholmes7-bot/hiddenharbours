namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The pure <b>soak-to-fill</b> math for a placed trap (trap-fishing arc — multi-catch, owner ask
    /// 2026-07-13): a pot no longer holds exactly one animal — it <b>fills as she soaks</b>, from one
    /// animal the moment she's ready (<see cref="TrapDef.SoakHours"/> — a soaked pot never comes up
    /// empty, today's rule kept) up to her full <see cref="TrapDef.CapacityUnits"/> by
    /// <see cref="TrapDef.HoursToFullPot"/>. This answers the design doc's §7.2 multi-catch question
    /// for pots: passive gear rolls N entries scaled by soak.
    ///
    /// <para><b>How the count is decided (deterministic, rule 5).</b> The first slot is guaranteed at
    /// ready. Each FURTHER slot <c>i</c> holds an animal iff its own stable per-slot roll
    /// (<see cref="SlotHash"/> — the trap's catch-seed lineage folded with the <c>"fill"</c> channel and
    /// the slot index) falls under the pot's current <see cref="Fill01"/>. Because the roll is a fixed
    /// number per (placement facts, slot) and the fill fraction only grows with soak, a slot that has
    /// filled STAYS filled as time passes — the pot monotonically fills, never un-catches. Same facts +
    /// same haul time ⇒ the identical count, this run and every future run; a save→load recomputes it
    /// bit-identically (nothing stored — ADR 0020's "contents are recomputed" discipline, unchanged).</para>
    ///
    /// <para><b>Per-animal catch streams.</b> Each animal index also gets its own stable RNG seed
    /// (<see cref="AnimalCatchSeed"/> — the <c>"catch"</c> channel) so the species pick + size roll per
    /// animal ride independent, reproducible streams (the same indexed-stream pattern the Build-7 deck
    /// work uses for size/berried/nip). Static + parameterised — fully EditMode-testable with no engine,
    /// no clock, no scene. NOTHING here reads Time, the wave field, or any hidden global RNG.</para>
    /// </summary>
    public static class TrapFill
    {
        /// <summary>Channel salt for the per-SLOT fill stream (does slot i hold an animal yet?).</summary>
        public const string FillChannel = "fill";
        /// <summary>Channel salt for the per-ANIMAL catch stream (which species / what size).</summary>
        public const string CatchChannel = "catch";

        /// <summary>
        /// How far the pot has FILLED beyond ready, in [0,1]: 0 at (or before) the moment she's ready
        /// (<paramref name="soakHours"/>), 1 once she's soaked <paramref name="hoursToFullPot"/>, linear
        /// between. A not-yet-ready pot reads 0 (the readiness gate itself stays
        /// <see cref="TrapSoak.IsReady"/> — a not-ready pot yields NOTHING, this is only the beyond-ready
        /// fraction). A degenerate window (<paramref name="hoursToFullPot"/> ≤
        /// <paramref name="soakHours"/>) reads as already-full at ready — the "she fills as fast as she's
        /// ready" collapse, documented on the Def.
        /// </summary>
        public static float Fill01(double placedAtSeconds, double nowSeconds, float soakHours, float hoursToFullPot)
        {
            if (!TrapSoak.IsReady(placedAtSeconds, nowSeconds, soakHours)) return 0f;

            double readySeconds = TrapSoak.SoakSeconds(soakHours);
            double fullSeconds = TrapSoak.SoakSeconds(hoursToFullPot);
            double window = fullSeconds - readySeconds;
            if (window <= 0.0) return 1f;   // full-at-ready collapse

            double beyond = TrapSoak.ElapsedSeconds(placedAtSeconds, nowSeconds) - readySeconds;
            if (beyond <= 0.0) return 0f;
            double f = beyond / window;
            return f >= 1.0 ? 1f : (float)f;
        }

        /// <summary>The stable per-slot fill hash: the trap's catch seed (the same lineage the catch and
        /// deck-work streams hash from) folded with the fill channel and the slot index, avalanched.
        /// Same placement facts + same slot ⇒ same hash, every run.</summary>
        public static uint SlotHash(int worldSeed, string instanceId, double placementGameTimeSeconds, int slotIndex)
        {
            unchecked
            {
                uint h = (uint)StableHash.TrapCatchSeed(worldSeed, instanceId, placementGameTimeSeconds);
                h = StableHash.Fold(h, FillChannel);
                h = StableHash.Fold(h, slotIndex);
                return StableHash.Finalize(h);
            }
        }

        /// <summary>The stable per-animal catch-stream seed: the trap's catch seed folded with the catch
        /// channel and the animal's index. Seeds the <see cref="System.Random"/> that picks THAT animal's
        /// species + size (see <see cref="PlacedTrapCatch.ResolveMany"/>) — indexed streams, so animal 2
        /// is as reproducible as animal 0.</summary>
        public static int AnimalCatchSeed(int worldSeed, string instanceId, double placementGameTimeSeconds, int animalIndex)
        {
            unchecked
            {
                uint h = (uint)StableHash.TrapCatchSeed(worldSeed, instanceId, placementGameTimeSeconds);
                h = StableHash.Fold(h, CatchChannel);
                h = StableHash.Fold(h, animalIndex);
                return (int)StableHash.Finalize(h);
            }
        }

        /// <summary>
        /// How many animals the pot holds at the given fill fraction, in [1, <paramref name="capacity"/>]:
        /// the first slot is a given (a READY pot never comes up empty — the caller gates readiness with
        /// <see cref="TrapSoak.IsReady"/> before asking); each further slot holds iff its stable per-slot
        /// roll falls under <paramref name="fill01"/>. So a just-ready pot holds exactly 1, a fully-soaked
        /// pot holds exactly <paramref name="capacity"/> (a roll in [0,1) is always under a fill of 1),
        /// and between, the count is seed-varied per pot but MONOTONE over time for any one pot (the
        /// rolls are fixed; only the threshold grows). A capacity under 1 reads as 1 (the Def field is
        /// Min(1) — this is the defensive clamp, not a tunable).
        /// </summary>
        public static int ResolveCount(float fill01, int capacity, int worldSeed, string instanceId,
                                       double placementGameTimeSeconds)
        {
            if (capacity <= 1) return 1;
            float f = fill01 < 0f ? 0f : (fill01 > 1f ? 1f : fill01);

            int count = 1;   // the ready floor — slot 0 is a given
            for (int i = 1; i < capacity; i++)
            {
                uint hash = SlotHash(worldSeed, instanceId, placementGameTimeSeconds, i);
                if (DeckWork.U01(hash) < f) count++;
            }
            return count;
        }
    }
}
