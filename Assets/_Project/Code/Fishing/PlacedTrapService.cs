using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;   // BaitDef (Fishing → Economy is allowed)

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// Owns the player's LIVE placed traps (trap-fishing arc Build 3) and keeps them in step with the save.
    /// It is the trap arc's answer to the same job <c>OwnedFleet</c> does for boats: hold the live objects,
    /// mirror them into <see cref="SaveData"/> as the player places/hauls, and reconstruct them from the
    /// DTOs on load — all off the existing save/load edges, through Core seams only (rule 4).
    ///
    /// <para><b>Save mirror (rule 5 — placement stored, everything else recomputed).</b> On each place/haul
    /// this writes/removes the trap's <see cref="PlacedTrapDto"/> in <see cref="ISaveService.Current"/> —
    /// the irreducible placement facts ONLY (kind, position, bait, region, instance id, placement time). The
    /// trap's soak and contents are never written; they are recomputed from those facts + the clock every
    /// time they're read (<see cref="TrapSoak"/>/<see cref="PlacedTrapCatch"/>), exactly as tide/wind are
    /// recomputed from <c>(seed, gameTime)</c>. So the save stays tiny and a reload lands the identical catch.</para>
    ///
    /// <para><b>Restore (the <see cref="GameLoaded"/> edge, the OwnedFleet precedent).</b> On load it rebuilds
    /// a live <see cref="PlacedTrap"/> per saved DTO — resolving the trap/bait Defs by id from its serialized
    /// registries (the small-fixed-set id→def resolution <c>OwnedFleet</c>'s hull registry uses) and the
    /// world seed off the restored save. Each restored trap re-publishes <see cref="TrapPlaced"/> so the
    /// Boats-lane buoy re-appears; no buoy/soak state is persisted.</para>
    ///
    /// <para><b>Scope (Build 4, greybox).</b> The unconditional drop (<see cref="PlaceTrap"/>) is still the
    /// Build-3 dev path; Build 4 adds the real, <b>depth-gated + bait-checked</b> placement
    /// (<see cref="TryPlaceGated"/>) — a trap may only be set where the water is deep enough
    /// (<see cref="TrapPlacement"/>) and only if the required bait is in stock, consuming one. Bait
    /// consumption (the owner's model) is applied here against <see cref="SaveData.BaitStock"/>.</para>
    /// </summary>
    public sealed class PlacedTrapService : MonoBehaviour
    {
        [Header("Def registries (id → Def resolution on restore — the OwnedFleet hull-registry pattern)")]
        [Tooltip("Every trap KIND that can be restored by id. Add a trap by adding its TrapDef here, not by " +
                 "editing this class. Looked up by stable Id.")]
        [SerializeField] private TrapDef[] _trapRegistry = System.Array.Empty<TrapDef>();
        [Tooltip("Every bait that can be resolved by id on restore. Looked up by stable Id; a miss = unbaited.")]
        [SerializeField] private BaitDef[] _baitRegistry = System.Array.Empty<BaitDef>();

        [Header("Container the live trap objects parent under")]
        [Tooltip("Parent transform for spawned PlacedTrap objects. Null → this service's transform.")]
        [SerializeField] private Transform _trapParent;

        private readonly List<PlacedTrap> _live = new();
        private int _instanceCounter;

        // Pots ABOARD (the transient hauled deck pot) count against the owned stock in the fresh-set
        // gate, but that state lives on the deck-work controller — registered here as a plain counter
        // (same-module wiring; Core stays out of it) so the availability derivation stays honest even
        // if a fresh set is attempted while a pot rides the deck. Null (nothing registered) reads 0.
        private System.Func<string, int> _aboardPotCounter;

        /// <summary>The live placed traps this service owns (read-only view for tests / tooling).</summary>
        public IReadOnlyList<PlacedTrap> Live => _live;

        /// <summary>Register the "pots currently aboard of this kind" source the fresh-set stock gate
        /// consults (the deck-work controller registers itself on Configure). Null to clear.</summary>
        public void SetAboardPotCounter(System.Func<string, int> counter) => _aboardPotCounter = counter;

        /// <summary>Pots of <paramref name="trapDefId"/> currently ABOARD (the transient deck pot).
        /// 0 when no source is registered or nothing is aboard.</summary>
        public int AboardPotCount(string trapDefId) => _aboardPotCounter?.Invoke(trapDefId) ?? 0;

        private void Awake() => EventBus.Subscribe<GameLoaded>(OnGameLoaded);
        private void OnDestroy() => EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);

        private Transform Parent => _trapParent != null ? _trapParent : transform;

        // ---- placement -------------------------------------------------------------------------

        /// <summary>
        /// Drop a trap of <paramref name="trapDef"/> at <paramref name="position"/>, baited with
        /// <paramref name="bait"/> (or null). Mints a stable instance id, spawns the live
        /// <see cref="PlacedTrap"/>, records its DTO in the save, consumes one bait from
        /// <see cref="SaveData.BaitStock"/> (the owner's per-placement model), and publishes
        /// <see cref="TrapPlaced"/> so the buoy appears. Returns the live trap (or null if it couldn't be
        /// placed — e.g. a null Def). Greybox: no depth gate (Build 4).
        /// </summary>
        public PlacedTrap PlaceTrap(TrapDef trapDef, BaitDef bait, Vector2 position, string regionId)
            => PlaceTrap(trapDef, bait, position, regionId, consumeBait: true);

        /// <summary>The one placement body, with the bait-consumption switch: the ordinary drop consumes
        /// one bait from stock (the abstract at-placement model); a Build-7 PRE-BAITED deck pot already
        /// consumed its bait during the deck's re-bait, so setting it must not charge twice.</summary>
        private PlacedTrap PlaceTrap(TrapDef trapDef, BaitDef bait, Vector2 position, string regionId,
                                     bool consumeBait)
        {
            if (trapDef == null) { Debug.LogWarning("[PlacedTrapService] No trap def to place."); return null; }

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            int worldSeed = GameServices.Environment != null ? GameServices.Environment.WorldSeed : 0;
            string instanceId = MintInstanceId(trapDef.Id);

            PlacedTrap trap = SpawnTrap(trapDef, bait, instanceId, regionId, now, worldSeed, position);

            // Mirror the placement into the save (the irreducible facts only) + consume one bait.
            var save = GameServices.Save?.Current;
            if (save != null)
            {
                // Anchor the save's WorldSeed to the live env seed at placement time. On restore the catch is
                // seeded from data.WorldSeed, so it MUST equal the seed the trap was placed under — otherwise a
                // save→load would land a different catch. SaveService.SnapshotLiveState also syncs this on a
                // full Save(); doing it here makes a trap placed before the next Save() restore-correct too.
                save.WorldSeed = worldSeed;

                save.PlacedTraps ??= new List<PlacedTrapDto>();
                save.PlacedTraps.Add(new PlacedTrapDto
                {
                    InstanceId = instanceId,
                    TrapDefId = trapDef.Id,
                    PosX = position.x,
                    PosY = position.y,
                    BaitId = bait != null ? bait.Id : "",
                    PlacementGameTimeSeconds = now,
                    Region = regionId,
                });
                if (consumeBait && bait != null) ConsumeOneBait(save, bait.Id);
            }

            EventBus.Publish(new TrapPlaced(instanceId, position.x, position.y));
            Debug.Log($"[PlacedTrapService] Set a {trapDef.DisplayName} at ({position.x:0.0}, {position.y:0.0}), " +
                      $"soaking {trapDef.SoakHours}h.");
            return trap;
        }

        /// <summary>Why a gated placement was refused (or that it succeeded) — so the caller/UI can phrase a
        /// cozy "can't set here" without a HUD number. All refusals are no-ops (nothing placed, no bait spent).</summary>
        public enum PlaceResult
        {
            /// <summary>The trap was set — the buoy's down, one bait consumed.</summary>
            Placed = 0,
            /// <summary>No trap Def supplied (a wiring error).</summary>
            NoTrap = 1,
            /// <summary>The water here is too shoal (or dry) for this trap's <see cref="TrapDef.MinSoakDepthMeters"/>.</summary>
            TooShallow = 2,
            /// <summary>The required bait isn't in stock — can't arm the pot.</summary>
            NoBait = 3,

            /// <summary>Build 7 (append-only): a hauled pot is aboard but not yet worked to READY — pick /
            /// band / bait her on the deck before she can be set. Nothing placed, nothing spent.</summary>
            PotNotReady = 4,

            /// <summary>Pots-are-owned (append-only): no SPARE pot of this kind in the locker — every pot
            /// you own is already in the water or aboard (available = owned − deployed − aboard ≤ 0,
            /// <see cref="PotLocker"/>). The shipwright sells more. Nothing placed, nothing spent.</summary>
            NoPotStock = 5,
        }

        /// <summary>
        /// The <b>real</b> Build-4 placement: drop a baited trap at <paramref name="position"/> only if
        /// (1) a <b>spare owned pot</b> of this kind is in the locker (pots are bought, not conjured —
        /// available = owned − deployed − aboard, <see cref="PotLocker"/>), (2) the water there is
        /// <b>deep enough</b> for the trap (<see cref="TrapPlacement.CanPlaceAt"/> — the inverse of the
        /// clam dig's exposure gate: deep water, not bared ground) AND (3) the trap's
        /// required bait is <b>in stock</b>. On success it delegates to <see cref="PlaceTrap"/> (which mints
        /// the instance, spawns the live trap, mirrors the DTO, consumes one bait, shows the buoy). Any refusal
        /// is a cozy no-op — nothing placed, no bait spent — and the reason is returned so the caller can
        /// phrase the "too shoal to set here" / "no bait" prompt. Reads the deterministic water level +
        /// terrain through Core (<see cref="GameServices"/>), so the gate matches the SAME depth the
        /// walkability/boat-cross/shader read (rule 5). <paramref name="placedTrap"/> is the live trap on
        /// success, else null.
        /// </summary>
        public PlaceResult TryPlaceGated(TrapDef trapDef, BaitDef bait, Vector2 position, string regionId,
                                         out PlacedTrap placedTrap)
        {
            placedTrap = null;
            if (trapDef == null) { Debug.LogWarning("[PlacedTrapService] No trap def to place."); return PlaceResult.NoTrap; }

            // The P2 stock gate (pots are OWNED, bought at the shipwright): a FRESH set takes a spare
            // pot from the locker — available = owned − deployed − aboard must be positive (PotLocker;
            // the transient deck pot is counted through the registered aboard source, so a hauled pot
            // waiting on the deck can never double as a second, conjured set). The deck RE-SET
            // (TryPlacePreBaited) sets the pot that is ALREADY aboard and is deliberately NOT gated —
            // that flow is stock-neutral by construction.
            if (PotLocker.AvailableCount(GameServices.Save?.Current, trapDef.Id, AboardPotCount(trapDef.Id)) <= 0)
            {
                Debug.Log($"[PlacedTrapService] No spare {trapDef.DisplayName} in the locker — the shipwright sells them.");
                return PlaceResult.NoPotStock;
            }

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            if (!TrapPlacement.CanPlaceAt(trapDef, GameServices.Environment, GameServices.TidalTerrain, now, position))
            {
                float depth = TrapPlacement.DepthAt(GameServices.Environment, GameServices.TidalTerrain, now, position);
                Debug.Log($"[PlacedTrapService] Too shoal to set a {trapDef.DisplayName} here " +
                          $"({depth:0.0} m; needs ≥ {trapDef.MinSoakDepthMeters:0.0} m). Try deeper water.");
                return PlaceResult.TooShallow;
            }

            // The real flow requires the bait in the locker (unlike the Build-3 dev drop, which let it stand).
            if (bait != null && !HasBaitInStock(GameServices.Save?.Current, bait.Id))
            {
                Debug.Log($"[PlacedTrapService] No {bait.DisplayName} in the locker to bait the {trapDef.DisplayName}.");
                return PlaceResult.NoBait;
            }

            placedTrap = PlaceTrap(trapDef, bait, position, regionId);
            return placedTrap != null ? PlaceResult.Placed : PlaceResult.NoTrap;
        }

        /// <summary>
        /// Set a Build-7 <b>pre-baited</b> deck pot: the same depth gate as <see cref="TryPlaceGated"/>,
        /// but NO stock check and NO consumption — the pot was baited by hand on the deck and its bait was
        /// consumed there (<see cref="TryConsumeBaitFromStock"/>). Charging again at the set would double-
        /// spend. Refusals are the same cozy no-ops.
        /// </summary>
        public PlaceResult TryPlacePreBaited(TrapDef trapDef, BaitDef bait, Vector2 position, string regionId,
                                             out PlacedTrap placedTrap)
        {
            placedTrap = null;
            if (trapDef == null) { Debug.LogWarning("[PlacedTrapService] No trap def to place."); return PlaceResult.NoTrap; }

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            if (!TrapPlacement.CanPlaceAt(trapDef, GameServices.Environment, GameServices.TidalTerrain, now, position))
            {
                float depth = TrapPlacement.DepthAt(GameServices.Environment, GameServices.TidalTerrain, now, position);
                Debug.Log($"[PlacedTrapService] Too shoal to set a {trapDef.DisplayName} here " +
                          $"({depth:0.0} m; needs ≥ {trapDef.MinSoakDepthMeters:0.0} m). Try deeper water.");
                return PlaceResult.TooShallow;
            }

            placedTrap = PlaceTrap(trapDef, bait, position, regionId, consumeBait: false);
            return placedTrap != null ? PlaceResult.Placed : PlaceResult.NoTrap;
        }

        /// <summary>
        /// Consume ONE of <paramref name="baitId"/> from <see cref="SaveData.BaitStock"/> if any is held —
        /// the Build-7 deck re-bait's physical consumption (the same stock the at-placement model and the
        /// dev G-grant use). Returns false (nothing spent) when the locker has none.
        /// </summary>
        public bool TryConsumeBaitFromStock(string baitId)
        {
            var save = GameServices.Save?.Current;
            if (save == null || !HasBaitInStock(save, baitId)) return false;
            ConsumeOneBait(save, baitId);
            return true;
        }

        /// <summary>Resolve a bait Def by id from the serialized registry (the restore-path lookup made
        /// public for the deck re-bait, so the loaded bait's favours ride into the next set). Null on a miss.</summary>
        public BaitDef ResolveBait(string id) => FindBait(id);

        /// <summary>
        /// Haul <paramref name="trap"/> into <paramref name="hold"/> (its deterministic catch), then remove it
        /// from the world + the save. Returns true iff a catch was landed. On a successful haul the trap's DTO
        /// is dropped from the save and <see cref="TrapRemoved"/> is published (the buoy goes). A not-ready /
        /// empty haul leaves the trap in place to try again. The context supplies region/tide/hour/season +
        /// <see cref="Gear.Trap"/>.
        /// </summary>
        public bool HaulTrap(PlacedTrap trap, IHold hold, in CatchContext ctx)
        {
            if (trap == null) return false;
            bool landed = trap.TryHaul(hold, in ctx);
            if (!landed) return false;

            RemoveTrap(trap);
            return true;
        }

        /// <summary>Remove a live trap from the world + the save + the buoy (shared by haul and a clear). Idempotent.</summary>
        public void RemoveTrap(PlacedTrap trap)
        {
            if (trap == null) return;
            string id = trap.InstanceId;
            _live.Remove(trap);

            var save = GameServices.Save?.Current;
            if (save?.PlacedTraps != null && !string.IsNullOrEmpty(id))
                save.PlacedTraps.RemoveAll(d => d.InstanceId == id);

            EventBus.Publish(new TrapRemoved(id));
            if (trap != null) DestroyTrapObject(trap.gameObject);
        }

        // ---- restore ---------------------------------------------------------------------------

        /// <summary>Rebuild the live traps from the loaded save (VS-08 load-restore, off the
        /// <see cref="GameLoaded"/> edge). Public so EditMode tests can drive it without the play lifecycle.</summary>
        public void OnGameLoaded(GameLoaded _) => RestoreFromSave(GameServices.Save?.Current);

        /// <summary>
        /// Reconstruct the live placed traps from an explicit blob (testable overload). Clears any current
        /// live traps first (a load replaces the world), then spawns one <see cref="PlacedTrap"/> per DTO —
        /// resolving the trap/bait Defs by id and taking the world seed off the save. Each re-publishes
        /// <see cref="TrapPlaced"/> so the buoys re-appear. A null/empty save leaves no traps.
        /// </summary>
        public void RestoreFromSave(SaveData data)
        {
            ClearLive();
            if (data?.PlacedTraps == null) return;

            for (int i = 0; i < data.PlacedTraps.Count; i++)
            {
                PlacedTrapDto dto = data.PlacedTraps[i];
                TrapDef trapDef = FindTrap(dto.TrapDefId);
                if (trapDef == null)
                {
                    Debug.LogWarning($"[PlacedTrapService] Saved trap '{dto.TrapDefId}' not in the registry — skipped.");
                    continue;   // unknown kind: skip rather than spawn a dead trap
                }
                BaitDef bait = FindBait(dto.BaitId);   // may be null (unbaited or unresolved — fine)

                PlacedTrap trap = SpawnTrap(trapDef, bait, dto.InstanceId, dto.Region,
                                            dto.PlacementGameTimeSeconds, data.WorldSeed,
                                            new Vector2(dto.PosX, dto.PosY));
                if (trap != null)
                    EventBus.Publish(new TrapPlaced(dto.InstanceId, dto.PosX, dto.PosY));
            }
        }

        // ---- helpers ---------------------------------------------------------------------------

        private PlacedTrap SpawnTrap(TrapDef trapDef, BaitDef bait, string instanceId, string regionId,
                                     double placementTime, int worldSeed, Vector2 position)
        {
            var go = new GameObject("PlacedTrap_" + instanceId);
            go.transform.SetParent(Parent, worldPositionStays: true);
            go.transform.position = new Vector3(position.x, position.y, 0f);

            var trap = go.AddComponent<PlacedTrap>();
            trap.Configure(trapDef, bait, instanceId, regionId, placementTime, worldSeed);
            _live.Add(trap);
            return trap;
        }

        private void ClearLive()
        {
            for (int i = 0; i < _live.Count; i++)
            {
                PlacedTrap t = _live[i];
                if (t == null) continue;
                EventBus.Publish(new TrapRemoved(t.InstanceId));
                DestroyTrapObject(t.gameObject);
            }
            _live.Clear();
        }

        /// <summary>Destroy a trap GameObject, edit-mode-safe: <c>Destroy</c> at runtime, <c>DestroyImmediate</c>
        /// in the editor / EditMode tests (where <c>Destroy</c> throws). Keeps the runtime path allocation-free
        /// and the tests headless.</summary>
        private static void DestroyTrapObject(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        /// <summary>A stable, unique-per-instance id for a freshly placed trap. Greybox: the trap kind id +
        /// the placement clock + a monotonic counter, so two traps of the same kind placed the same instant
        /// still differ. (Not deterministic across runs — but a placed trap's id IS saved, so it survives
        /// load; only NEW placements mint fresh ids.)</summary>
        private string MintInstanceId(string trapDefId)
        {
            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            _instanceCounter++;
            return $"{trapDefId}#{(long)now}.{_instanceCounter}";
        }

        /// <summary>Does the save hold at least one of <paramref name="baitId"/>? The stock check the gated
        /// placement applies before it arms a pot. A null save / empty id reads as "no stock" (can't place).</summary>
        private static bool HasBaitInStock(SaveData save, string baitId)
        {
            if (save?.BaitStock == null || string.IsNullOrEmpty(baitId)) return false;
            for (int i = 0; i < save.BaitStock.Count; i++)
                if (save.BaitStock[i].BaitId == baitId && save.BaitStock[i].Count > 0) return true;
            return false;
        }

        private static void ConsumeOneBait(SaveData save, string baitId)
        {
            if (save.BaitStock == null || string.IsNullOrEmpty(baitId)) return;
            for (int i = 0; i < save.BaitStock.Count; i++)
            {
                if (save.BaitStock[i].BaitId == baitId)
                {
                    int left = save.BaitStock[i].Count - 1;
                    if (left > 0) save.BaitStock[i] = new BaitStock(baitId, left);
                    else save.BaitStock.RemoveAt(i);   // spent the last one — drop the record
                    return;
                }
            }
            // No stock recorded for this bait — greybox lets the placement stand (the dev drop isn't gated
            // on stock; Build 4's real placement flow will require the bait first).
        }

        private TrapDef FindTrap(string id)
        {
            if (_trapRegistry == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _trapRegistry.Length; i++)
                if (_trapRegistry[i] != null && _trapRegistry[i].Id == id) return _trapRegistry[i];
            return null;
        }

        private BaitDef FindBait(string id)
        {
            if (_baitRegistry == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _baitRegistry.Length; i++)
                if (_baitRegistry[i] != null && _baitRegistry[i].Id == id) return _baitRegistry[i];
            return null;
        }

        /// <summary>Wire the service in one call (tests / editor). Mirrors <c>OwnedFleet.Configure</c>.</summary>
        public void Configure(TrapDef[] trapRegistry, BaitDef[] baitRegistry, Transform trapParent)
        {
            _trapRegistry = trapRegistry ?? System.Array.Empty<TrapDef>();
            _baitRegistry = baitRegistry ?? System.Array.Empty<BaitDef>();
            _trapParent = trapParent;
        }
    }
}
