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
    /// <para><b>Scope (Build 3, greybox).</b> Placement here is unconditional (the <see cref="DevTrapInput"/>
    /// dev drop) — the depth-gated placement rule and the haul minigame are Build 4. Bait consumption on
    /// placement (the owner's model) is applied here against <see cref="SaveData.BaitStock"/>.</para>
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

        /// <summary>The live placed traps this service owns (read-only view for tests / tooling).</summary>
        public IReadOnlyList<PlacedTrap> Live => _live;

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
                if (bait != null) ConsumeOneBait(save, bait.Id);
            }

            EventBus.Publish(new TrapPlaced(instanceId, position.x, position.y));
            Debug.Log($"[PlacedTrapService] Set a {trapDef.DisplayName} at ({position.x:0.0}, {position.y:0.0}), " +
                      $"soaking {trapDef.SoakHours}h.");
            return trap;
        }

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
