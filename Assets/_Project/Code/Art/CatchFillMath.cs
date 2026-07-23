using System.Collections.Generic;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The five fill bands every container rig reads (catchKit.js <c>FRAC</c>). A band is a LOOK,
    /// not an inventory: the hold still owns the real count; these only decide how heaped the
    /// drawn catch reads.
    /// </summary>
    public enum CatchFillBand { Empty, Few, Half, Full, Brim }

    /// <summary>One drawn catch item: which rig art, which baked lay variant, what scatter scale.</summary>
    public readonly struct CatchFillItem
    {
        public readonly string Kind;    // catch kind key: cod/haddock/pollock/mackerel/lobster/crab/mussel/clam
        public readonly int Variant;    // baked lay variant 0..3 (a sheet column)
        public readonly float Scale;    // scatter scale (1 for shellfish, 0.82..1.12 otherwise)

        public CatchFillItem(string kind, int variant, float scale)
        {
            Kind = kind; Variant = variant; Scale = scale;
        }

        public override string ToString() => $"{Kind} v{Variant} ×{Scale:0.###}";
    }

    /// <summary>
    /// The art director's mulberry32 PRNG, ported bit-for-bit from catchKit.js so a fill drawn in
    /// Unity is the SAME heap his rig would draw. Deterministic from its seed alone — no engine
    /// state, no global randomness (CLAUDE.md rule 5 applies to the LOOK: same seed in, same heap
    /// out, every session).
    /// </summary>
    public struct Mulberry32
    {
        private uint _a;

        /// <summary>Seed exactly as <c>fillItems</c> does: <c>mulberry((seed||7)*2654435761)</c>.
        /// ⚠️ The JS multiply is a DOUBLE multiply: past 2⁵³ (any seed over ~3.4 million) the
        /// product rounds before <c>&gt;&gt;&gt;0</c> truncates it mod 2³². An exact 64-bit integer
        /// multiply diverges there — caught by the parity test on a date-shaped seed — so this
        /// reproduces the IEEE double product bit-for-bit, then applies ToUint32.</summary>
        public static Mulberry32 ForFill(int seed)
        {
            double p = (double)(seed == 0 ? 7 : seed) * 2654435761.0;   // JS: number × number
            double m = p % 4294967296.0;                                 // integral, exact fmod
            if (m < 0) m += 4294967296.0;                                // ToUint32 is non-negative
            return new Mulberry32 { _a = (uint)m };
        }

        /// <summary>Raw-seed constructor (the rig's <c>mulberry(101)</c> tote-slot jitter etc.).</summary>
        public static Mulberry32 FromRawSeed(uint seed) => new Mulberry32 { _a = seed };

        /// <summary>Next double in [0,1). Twin of the JS: every shift/imul wraps mod 2³², which is
        /// exactly what uint arithmetic does.</summary>
        public double Next()
        {
            unchecked
            {
                _a += 0x6D2B79F5u;
                uint t = _a;
                t = (t ^ (t >> 15)) * (1u | t);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }

    /// <summary>
    /// Pure C# twin of catchKit.js <c>fillItems</c> — the seeded, MONOTONIC container-fill recipe.
    ///
    /// <para><b>The owner-visible contract is monotonicity:</b> item <c>i</c>'s kind, lay variant
    /// and scale depend only on the seed and on draws 0..i, so growing a fill (landing more catch)
    /// NEVER moves or re-dresses the catch already showing — the tote gains fish, it does not
    /// reshuffle them. That is the whole diegetic point: what you see accumulating is what you
    /// caught, staying put.</para>
    ///
    /// <para><b>Source of truth:</b> the tables and draw order here are the rig's
    /// (docs/art/rigs/catchKit.js — art-director source, do not edit). They are restated as code
    /// because the fill runs at runtime without a script host; the EditMode parity test
    /// (<c>CatchStorageBakeTests</c>) replays both against each other so drift is loud, exactly
    /// like <c>FishingSheetSlicer</c>'s pinned pivots.</para>
    /// </summary>
    public static class CatchFillMath
    {
        /// <summary>Band fractions — catchKit.js <c>FRAC</c>.</summary>
        public static double FractionOf(CatchFillBand band) => band switch
        {
            CatchFillBand.Few => 0.25,
            CatchFillBand.Half => 0.55,
            CatchFillBand.Full => 0.85,
            CatchFillBand.Brim => 1.0,
            _ => 0.0,
        };

        /// <summary>Per-catch default item budgets — catchKit.js <c>MAXN</c>. Used only when the
        /// container passes no slot capacity; containers with slot anchors pass their own.</summary>
        private static readonly Dictionary<string, int> MaxN = new(System.StringComparer.Ordinal)
        {
            ["cod"] = 18, ["haddock"] = 20, ["pollock"] = 20, ["mackerel"] = 24,
            ["lobster"] = 24, ["crab"] = 24, ["mussel"] = 24, ["clam"] = 24, ["mixed"] = 22,
        };

        /// <summary>catchKit.js's fallback when a kind is not in MAXN.</summary>
        private const int DefaultMaxN = 8;

        /// <summary>The 'mixed' catch draw pool, in the rig's order (order is load-bearing — it is
        /// consumed by the seeded stream).</summary>
        private static readonly string[] MixedPool =
            { "mackerel", "haddock", "lobster", "crab", "pollock", "cod" };

        /// <summary>Baked lay variants per kind — the literal 4 of the rig's variant draw
        /// (<c>Math.floor(rng()*4)</c>); every item sheet bakes exactly this many columns.</summary>
        public const int Variants = 4;

        // Scatter-scale recipe (catchKit.js): shellfish lie flat at 1; everything else lands
        // 0.82 + rng()*0.3. Recipe constants, not balance dials — parity-tested against the rig.
        private const double ScaleBase = 0.82, ScaleSpread = 0.3;

        private static bool IsShellfish(string kind) => kind == "mussel" || kind == "clam";

        /// <summary>The three interior bands a partial fill can read as (see <see cref="BandFor"/>).</summary>
        private static readonly CatchFillBand[] PartialBands =
            { CatchFillBand.Few, CatchFillBand.Half, CatchFillBand.Full };

        /// <summary>Item count for a band — <c>Math.round(frac × capacity)</c>, JS half-up
        /// rounding. <paramref name="capacity"/> &lt; 0 falls back to the kind's MAXN budget.</summary>
        public static int ItemCount(string catchKey, CatchFillBand band, int capacity = -1)
        {
            int count = capacity >= 0
                ? capacity
                : (catchKey != null && MaxN.TryGetValue(catchKey, out int m) ? m : DefaultMaxN);
            return (int)System.Math.Floor(FractionOf(band) * count + 0.5);
        }

        /// <summary>
        /// The faithful twin of <c>fillItems(catchKey, fill, seed, count)</c>: a seeded item list
        /// for one uniform (or 'mixed') catch. Clears and refills <paramref name="result"/> —
        /// pass a reused list so steady-state refreshes allocate nothing.
        /// </summary>
        public static void FillItems(string catchKey, CatchFillBand band, int seed, int capacity,
                                     List<CatchFillItem> result)
        {
            result.Clear();
            int n = ItemCount(catchKey, band, capacity);
            var rng = Mulberry32.ForFill(seed);
            for (int i = 0; i < n; i++)
            {
                string kind = catchKey;
                if (catchKey == "mixed")
                    kind = MixedPool[(int)(rng.Next() * MixedPool.Length)];
                else
                    rng.Next();   // the kind draw is consumed either way — stream alignment is the contract
                int variant = (int)(rng.Next() * Variants);
                double scale = IsShellfish(kind) ? 1.0 : ScaleBase + rng.Next() * ScaleSpread;
                result.Add(new CatchFillItem(kind, variant, (float)scale));
            }
        }

        /// <summary>Convenience allocating overload (tests / tools).</summary>
        public static List<CatchFillItem> FillItems(string catchKey, CatchFillBand band, int seed,
                                                   int capacity = -1)
        {
            var list = new List<CatchFillItem>();
            FillItems(catchKey, band, seed, capacity, list);
            return list;
        }

        /// <summary>
        /// The ACTUAL-catch variant of the twin: the hold hands us its real, landed-in-order kind
        /// list and we dress each item from the SAME seeded stream shape as the rig's non-mixed
        /// path (one consumed kind draw, then variant, then scale). Because the hold appends and
        /// never reorders, and draws 0..i never change, this inherits the monotonic contract:
        /// landing catch #12 cannot re-dress catch #3.
        /// </summary>
        public static void AppearancesFor(IReadOnlyList<string> kinds, int seed,
                                          List<CatchFillItem> result)
        {
            result.Clear();
            if (kinds == null) return;
            var rng = Mulberry32.ForFill(seed);
            for (int i = 0; i < kinds.Count; i++)
            {
                rng.Next();   // the (unused) kind draw — keeps this stream identical to FillItems'
                string kind = kinds[i];
                int variant = (int)(rng.Next() * Variants);
                double scale = IsShellfish(kind) ? 1.0 : ScaleBase + rng.Next() * ScaleSpread;
                result.Add(new CatchFillItem(kind, variant, (float)scale));
            }
        }

        /// <summary>
        /// How many slot items a continuous fill shows in a container with
        /// <paramref name="slotCapacity"/> slots. Pinned ends + a floor of one: an empty hold shows
        /// nothing, any catch at all shows at least one item (the tray never lies empty-looking),
        /// and only a genuinely full hold heaps every slot. Monotonic in <paramref name="fill01"/>.
        /// </summary>
        public static int VisibleCount(double fill01, int slotCapacity)
        {
            if (slotCapacity <= 0 || fill01 <= 0.0) return 0;
            if (fill01 >= 1.0) return slotCapacity;
            int n = (int)System.Math.Floor(fill01 * slotCapacity + 0.5);
            if (n < 1) n = 1;
            return n < slotCapacity ? n : slotCapacity;
        }

        /// <summary>
        /// The band a continuous fill reads as, for containers whose fills are BAKED states
        /// (the bucket kit) rather than drawn items. Same pinning rule the deck tray uses:
        /// Empty only when truly empty, Brim only when truly full, partials to the nearest of
        /// Few/Half/Full by band fraction (ties read as the fuller band).
        /// </summary>
        public static CatchFillBand BandFor(double fill01, int usedUnits)
        {
            if (usedUnits <= 0 || fill01 <= 0.0) return CatchFillBand.Empty;
            if (fill01 >= 1.0) return CatchFillBand.Brim;
            CatchFillBand best = CatchFillBand.Few;
            double bestDist = double.MaxValue;
            foreach (var band in PartialBands)
            {
                double d = System.Math.Abs(fill01 - FractionOf(band));
                if (d <= bestDist)   // ties fall to the later (fuller) band
                {
                    bestDist = d; best = band;
                }
            }
            return best;
        }
    }
}
