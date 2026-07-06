using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;   // BaitDef (Fishing → Economy is allowed)

namespace HiddenHarbours.Fishing
{
    /// <summary>The live lifecycle of a placed trap. Purely a readout of the deterministic soak — nothing
    /// here is saved; the state is recomputed from the placement facts + the clock every time it's read.</summary>
    public enum PlacedTrapState
    {
        /// <summary>Just dropped this frame — the buoy's settling. (Cosmetic first beat; folds into Soaking.)</summary>
        Deployed = 0,
        /// <summary>Down and soaking — not yet worth hauling.</summary>
        Soaking = 1,
        /// <summary>Soaked its full span — ready to haul.</summary>
        Ready = 2,
        /// <summary>Hauled — its catch has been resolved and taken; the trap is spent this session.</summary>
        Hauled = 3,
    }

    /// <summary>
    /// A logical <b>placed trap</b> at rest on the seabed (trap-fishing arc Build 3 — the runtime). It ties
    /// together the irreducible placement facts (which trap kind, where, when placed, what bait, which
    /// region, a stable instance id) with the two things derived from them: the <b>soak</b> readout
    /// (<see cref="TrapSoak"/>) and, on haul, the <b>deterministic catch</b> (<see cref="PlacedTrapCatch"/>).
    /// It shows the Build-1 <see cref="Boats.BuoyWaveVisual"/> at its position so a set trap reads as a
    /// bobbing buoy on the swell.
    ///
    /// <para><b>Nothing about it is saved except the placement (rule 5).</b> Soak progress and contents are
    /// <em>recomputed</em> from <c>(worldSeed, instanceId, placementTime)</c> + now, never stored — the same
    /// discipline that keeps tide/wind out of the save. The persisted record is the
    /// <see cref="Core.PlacedTrapDto"/> the <see cref="PlacedTrapService"/> owns; this component is the live
    /// projection of one such record, reconstructed on load. So a save→load→haul lands the identical catch:
    /// the seed is a pure function of the placement facts that DO survive the save.</para>
    ///
    /// <para><b>Scope (Build 3, greybox).</b> This is the logical runtime + the deterministic catch + the
    /// buoy. It is NOT the depth-gated placement rule (checking the trap sits within the Def's
    /// <c>Min/MaxSoakDepthMeters</c> band) nor the haul minigame — those are Build 4. The dev drop
    /// (<see cref="DevTrapInput"/>) places without the depth gate; the haul is a single resolve-and-land call.</para>
    ///
    /// <para><b>Seam discipline (rule 4).</b> Reads the clock through <see cref="GameServices.Clock"/> and the
    /// catch pool through <see cref="FishSpeciesRegistry"/> (Fishing-lane); lands into an
    /// <see cref="IHold"/> and publishes the Core <see cref="FishCaught"/> — the same land path the rod and
    /// the clam dig use. No World/Player/Economy concrete classes referenced (BaitDef is data in Economy,
    /// referenced by the Fishing→Economy asmdef edge the trap content already established).</para>
    /// </summary>
    public sealed class PlacedTrap : MonoBehaviour
    {
        [Header("Placement facts (the irreducible record — what a save stores)")]
        [Tooltip("The trap KIND (resolved by id from the DTO on restore; wired directly on a live drop). " +
                 "Drives soak span, capacity, and the allowed catch species.")]
        [SerializeField] private TrapDef _trapDef;
        [Tooltip("The bait loaded, as data — its FavorsSpeciesIds soft-weight the catch. Null = unbaited.")]
        [SerializeField] private BaitDef _bait;

        [Header("Catch nudge (rule 6 — the bait's soft-weight strength, tunable)")]
        [Tooltip("How strongly the loaded bait leans the roll toward its favoured species (>=1; 1 = no nudge). " +
                 "3 = a favoured species carries 3x its base weight; both catches stay possible.")]
        [SerializeField] private int _baitFavourMultiplier = PlacedTrapCatch.BaitFavourMultiplier;

        // --- the placement facts that seed the deterministic catch (set on Configure/restore) ---
        private string _instanceId;
        private double _placementGameTimeSeconds;
        private string _regionId;
        private int _worldSeed;

        private bool _hauled;
        private CatchItem _lastCatch;
        private bool _lastCatchValid;

        /// <summary>The trap kind (data). Null before configured.</summary>
        public TrapDef Trap => _trapDef;

        /// <summary>The loaded bait (data), or null for an unbaited trap.</summary>
        public BaitDef Bait => _bait;

        /// <summary>Stable id unique to THIS placed instance — the key the deterministic catch seed folds in.</summary>
        public string InstanceId => _instanceId;

        /// <summary>The game-clock instant the trap was placed (the soak + catch anchor).</summary>
        public double PlacementGameTimeSeconds => _placementGameTimeSeconds;

        /// <summary>The region this trap sits in (scene-per-region).</summary>
        public string RegionId => _regionId;

        /// <summary>The bait id (or empty) — the persisted placement fact.</summary>
        public string BaitId => _bait != null ? _bait.Id : "";

        /// <summary>
        /// Soak progress in [0,1] at the given game time — a pure readout, nothing stored. A configured trap
        /// with no Def reads as complete (nothing to soak).
        /// </summary>
        public float Progress01(double nowSeconds)
            => _trapDef != null ? TrapSoak.Progress01(_placementGameTimeSeconds, nowSeconds, _trapDef.SoakHours) : 1f;

        /// <summary>True once the trap has soaked its full span at the given time (readiness gate for haul).</summary>
        public bool IsReady(double nowSeconds)
            => _trapDef == null || TrapSoak.IsReady(_placementGameTimeSeconds, nowSeconds, _trapDef.SoakHours);

        /// <summary>The trap's lifecycle state at the given time — a projection of the soak, plus the
        /// session-only "hauled" flag (spent this session; a reload reconstructs it fresh as Ready).</summary>
        public PlacedTrapState StateAt(double nowSeconds)
        {
            if (_hauled) return PlacedTrapState.Hauled;
            return IsReady(nowSeconds) ? PlacedTrapState.Ready : PlacedTrapState.Soaking;
        }

        /// <summary>True once this trap has been hauled this session (session-only, never saved).</summary>
        public bool Hauled => _hauled;

        /// <summary>The catch the last <see cref="TryHaul"/> resolved, if any (for the dev readout / tooling).</summary>
        public bool TryGetLastCatch(out CatchItem item)
        {
            item = _lastCatch;
            return _lastCatchValid;
        }

        /// <summary>
        /// Configure a placed trap from its facts (the one wire path — a live drop or a save-restore). The
        /// world seed + placement time + instance id are the deterministic catch anchors; keep them stable
        /// across a save/load and the catch is reproducible.
        /// </summary>
        public void Configure(TrapDef trapDef, BaitDef bait, string instanceId, string regionId,
                              double placementGameTimeSeconds, int worldSeed)
        {
            _trapDef = trapDef;
            _bait = bait;
            _instanceId = instanceId;
            _regionId = regionId;
            _placementGameTimeSeconds = placementGameTimeSeconds;
            _worldSeed = worldSeed;
            _hauled = false;
            _lastCatchValid = false;
        }

        /// <summary>
        /// Resolve THIS trap's catch deterministically (pure — no side effects, nothing landed). Same
        /// placement facts + same pool + same context ⇒ identical <see cref="CatchItem"/>, this run and every
        /// future run (rule 5). The pool is built from the Def's allowed species via
        /// <see cref="FishSpeciesRegistry"/>; the bait's favours soft-weight it. Returns null if not yet
        /// soaked, or if the pool is empty / everything gates out.
        /// </summary>
        public CatchItem? ResolveCatch(double nowSeconds, in CatchContext ctx)
        {
            if (_trapDef == null) return null;
            if (!IsReady(nowSeconds)) return null;   // must soak first — a not-ready trap yields nothing

            List<FishSpeciesDef> pool = FishSpeciesRegistry.Resolve(_trapDef.AllowedCatchFishIds);
            if (pool.Count == 0) return null;

            int seed = StableHash.TrapCatchSeed(_worldSeed, _instanceId, _placementGameTimeSeconds);
            var rng = new System.Random(seed);

            IReadOnlyList<string> favours = _bait != null ? _bait.FavorsSpeciesIds : null;
            return PlacedTrapCatch.Resolve(pool, in ctx, favours, _baitFavourMultiplier, rng);
        }

        /// <summary>
        /// Haul the trap: resolve its deterministic catch and land it into <paramref name="hold"/> (the same
        /// <see cref="IHold.TryAdd"/> + <see cref="FishCaught"/> path the rod and clam dig use). Returns true
        /// iff a catch was landed. A not-ready or already-hauled trap, an empty pool, or a full hold is a
        /// cozy no-op (a log, no penalty). Marks the trap <see cref="Hauled"/> this session on a successful
        /// haul (session-only; a reload reconstructs it as Ready and it hauls the SAME catch again — the
        /// deterministic guarantee the tests assert).
        /// </summary>
        public bool TryHaul(IHold hold, in CatchContext ctx)
        {
            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;

            if (_hauled) { Debug.Log("[PlacedTrap] Already hauled this one."); return false; }
            if (!IsReady(now))
            {
                Debug.Log($"[PlacedTrap] Not soaked yet ({Progress01(now) * 100f:0}% — give it time).");
                return false;
            }

            CatchItem? maybe = ResolveCatch(now, in ctx);
            if (maybe == null) { Debug.Log("[PlacedTrap] Hauled it up empty — nothing in the pot."); return false; }

            CatchItem catchItem = maybe.Value;
            _lastCatch = catchItem;
            _lastCatchValid = true;

            if (hold == null || !hold.TryAdd(catchItem))
            {
                Debug.Log($"[PlacedTrap] Landed a {catchItem}, but there's nowhere to stow it (no/full hold).");
                return false;   // don't mark hauled — the catch wasn't taken; try again with room
            }

            EventBus.Publish(new FishCaught(catchItem));   // same land path the rod uses
            _hauled = true;
            Debug.Log($"[PlacedTrap] Hauled a {catchItem} from the {(_trapDef != null ? _trapDef.DisplayName : "trap")}.");
            return true;
        }
    }
}
