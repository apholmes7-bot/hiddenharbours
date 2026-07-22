# Capture — Deck boarding, cleats & ropes, diegetic interact highlight

> **Status:** owner vision capture, 2026-07-21. Not yet built; backlog rows M2-37..M2-39. This doc
> records the ask, the recommended data pipeline, and the acceptance seeds so nothing is lost between
> now and the M2 pull. Canon homes when these graduate: `boats-and-navigation.md` (deck/cleats),
> `diegetic-ui-and-inventory.md` (interact highlight).

---

## 1. What the owner asked for (2026-07-21, verbatim intent)

1. **Walkable decks & washboards.** Should the art director define the deck areas of the boats, and
   the walkable washboard areas? The player presses **Space to climb onto the washboards** of boats
   that support them.
2. **Cleat points & rope toss.** Boats have cleat points where ropes can be tied — e.g. the lobster
   boat: *"3 on stern, 2 mid ship and 1 on the bow or 3 on bow."* The player can **grab a rope and
   toss it towards a shore cleat in the same manner you cast in the new fishing mini game**.
3. **Diegetic interact highlight.** *"A shader of some sort highlights an object in front of the
   player that can be right clicked or something to interact — pick up a bucket, a rod, place a
   bucket in a place. The shader is just an indicator, without having a distracting UI."*

Pillars served: P1 (mooring in a moving sea is seamanship), P3 (a working coast where ropes and
buckets are real), P5 (physical, cozy-with-teeth interactions instead of menus). The winch-era
automation of mooring, if ever, is P4 and much later.

## 2. The answer to "should the art director define these?" — yes, as RIG DATA

**Yes — and in the rig source, not as painted overlays or hand-placed Unity markers.** ADR 0022
phases 1–2 (merged 2026-07-21) make each boat rig a queryable geometry source: the same export-object
mechanism that is delivering `F`, `MATS`, `GAIN`, `BIAS`, `LN` per hull can carry gameplay geometry.
Ask the art director to add, per boat rig, in hull-local metres:

| Symbol | Shape | Meaning |
|---|---|---|
| `DECK` | polygon(s) | walkable deck area(s) |
| `WASHBOARD` | strip polygon(s) | walkable washboards, only on hulls that have them |
| `CLEATS` | named points | tie-off points, e.g. `bow_1`, `mid_port`, `mid_stbd`, `stern_1..3` |

Cleat **counts and placement per hull are an owner/art-director design conversation** — the table
above is the schema, not the layout. Absence of a symbol = the hull doesn't support the feature
(a punt has no washboards); that absence is data, not an error.

**Why rig-data and not hand-authoring (lessons already paid for):**

- The baked anchors JSON turned out to be dead code — the runtime's real motor mount was a
  hand-transcribed constant on `BoatVisualDef`. Hand transcription is the failure mode; the
  extractor must carry these sets **straight through to Def assets** with no human copy step.
- One source of truth: the same rig then drives the sprite bake, the 3D mesh, *and* gameplay
  geometry — when the art director moves a rail, the walkable area and cleats move with it.
- Content-is-data (ADR 0003): per-boat cleat sets and deck polygons are Def data; zero per-boat code.
- ⚠️ **Sprite hulls need the iso foreshortening applied per-artwork** when projecting world-metre
  rig data to screen (the overlay-pose lesson: never rescale a tuned constant onto a different lever
  arm). Mesh hulls (phases 4–5) get this for free from the projection itself. The extractor should
  emit hull-local metres and let each presenter project.

**Do it now, cheaply:** fold `DECK`/`WASHBOARD`/`CLEATS` into the already-open export ask to the
art director, and teach `RigMeshExtractor` to pass named point/polygon sets through when present
(per-symbol probing already tolerates them arriving one hull at a time). The gameplay features then
build on data that already exists.

## 3. Feature sketches (acceptance seeds)

### M2-37 — Deck & washboard boarding (Space to climb)
- On-foot movement on a boat is constrained to the `DECK` (+ `WASHBOARD`) polygons of that hull.
- `Space` climbs deck↔washboard (and boat↔wharf where sensible) **only where the hull's data offers
  it**; no prompt on hulls without washboards.
- The boat keeps riding the wave field underneath the player (the deck is a moving platform —
  reuses the shared wave/rock phase, same as the leave-helm gaff-haul in M2-33).
- Extends M2-33's leave-the-helm precedent from "stand at the rail" to "walk the working deck".

### M2-38 — Cleats, ropes, and the toss-a-line moor
- Each hull exposes its named `CLEATS`; shore furniture (wharves, floats) has counterpart cleat/bollard
  points (world-content authors those in-scene — shore is hand-authored, boats are rig data).
- Player at a cleat grabs a line; the **toss reuses the fishing-cast verb** (same input feel, same
  skill curve — one verb, two contexts).
- A made-fast line **actually holds the boat** against tide/wind drift (the sim keeps computing;
  the rope constrains). Cast quality can affect whether the loop catches (cozy fail: the line slips
  into the water, coil and try again).
- Un-tying is the same interaction in reverse. No menu.

### M2-39 — Diegetic interact highlight (shader, no UI)
- An `IInteractable` seam in Core; a facing-aware detector on the player picks **exactly one**
  current candidate (nearest in a forward arc).
- The candidate gets a **subtle shader highlight** (outline/rim on the sprite — art-pipeline owns
  the look; it must read at KTC palette values and in night scenes without glowing like UI).
- One bound input (owner said right-click; route it through `InputService` intents — bindings
  retarget, intents don't) performs the context action: pick up bucket/rod, place bucket, grab rope,
  climb. **This is the same verb M2-37/38 consume** — build it first among the three.
- No screen-space prompts, labels, or icons by default. No per-frame allocation in the detector.

## 4. Phasing

| When | What |
|---|---|
| **Now (rides ADR 0022)** | Add `DECK`/`WASHBOARD`/`CLEATS` to the art-director export ask; extractor pass-through to Def data. Additive, small. |
| **M2, in order** | M2-39 (the interact verb — the other two consume it) → M2-37 (boarding) → M2-38 (ropes). Alongside M2-33, which shares the leave-the-helm/moving-deck substrate. |
| **Owner's call** | M2-39 is a strong candidate to pull forward earlier (it improves the existing bucket/rod/trap interactions on its own). Raise, don't sneak. |

## 5. What these symbols do NOT do — occlusion (owner follow-up, 2026-07-21)

**Q: will `DECK`/`WASHBOARD`/`CLEATS` be enough for the character to be hidden behind portions of the
boat that should block the sprite from the camera?**

**No — and nothing extra is needed from the art director either.** Those three symbols are gameplay
geometry (where you can stand, where ropes tie); they carry no depth. Occlusion comes from the mesh
itself: ADR 0022 hulls render with a real z-buffer under the projection trick, so a character sprite
drawn as a **depth-tested, alpha-tested billboard** at its deck position is hidden per-pixel by any
hull part nearer the camera (wheelhouse, rail, gunwale) — free, with no masks and no sorting hacks.
This is one of the quiet wins of meshes over baked sprites.

Requirements this places on the pipeline (fed to the phase 3 agent 2026-07-21):
- The hull pass's **depth buffer must stay available** for later depth-tested sprite passes
  (injection-point choice in the URP render feature must not discard it).
- The reversed-Z conventions (ZTest GEqual, depth clear 0) apply to the future character pass too.

Open edges, for when M2-37 is pulled:
- **Hulls that stay sprites** (dory, punt, skiffs) have no z-buffer. They are small open boats where
  full-body occlusion barely arises; if it ever matters, the rig baker can additionally bake a
  **per-facing depth map** (it runs the real geometry), enabling the same per-pixel test for sprite
  hulls. Option, not scheduled.
- **Full occlusion can hide the player entirely** (inside a wheelhouse). If that ever feels bad, the
  standard cozy fix is a subtle stencil **silhouette** through the hull — cheap to add, owner's call
  on whether hidden-is-hidden or faint-outline.
