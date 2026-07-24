# Nine Mile Creek — the first wharf

> **Status:** PROPOSAL from the owner's brief + reference photographs, 2026-07-23. One structural
> question is genuinely open and gates the rest — see §1. Subordinate to
> [`../vision-and-pillars.md`](../vision-and-pillars.md) (canon) and
> [`world-and-regions.md`](world-and-regions.md). Sizes follow the rules in
> [`scene-sizing-and-world-scale.md`](scene-sizing-and-world-scale.md).
>
> **The owner's brief, verbatim in substance:** the first actual wharf, on the mainland just after
> the tidal crossing. Boats on the north wall, breakwater on the south, beach at the north end. It
> takes small fishing boats up to lobster-boat size — punts, centre-console skiffs, Cape Islanders,
> lobster boats. Bait sheds and trap storage sheds. **A winch on the west wharf wall to unload your
> haul**, nearest the parking lot where you meet **the first fish buyers in their trucks**. Large fish
> storage/hold buildings, but **no processing here yet**. Real-world Nine Mile Creek → St Peters
> geography, smaller in game but the same feel.

---

## 1. ⚠️ The one question that has to be answered first

**Canon says the sandbar lands you at Port Greywick** — the market town with the shops, the auction,
most of the NPCs, the cod licence, the rod, and the shipwright who sells you the damaged dory
([`world-and-regions.md`](world-and-regions.md) §6.0/§6.3, canon §5.8). The brief describes something
that is emphatically **not** a market town: a working wharf with bait sheds, trap storage, a winch, and
buyers who arrive in trucks. "No processing here **yet**" and "the **first** fish buyers" both point
past this place to a bigger one.

So: **what is Nine Mile Creek relative to Port Greywick?**

| | What it means | Cost |
|---|---|---|
| **A. Greywick's working wharf — one region, two zones** ⭐ | Nine Mile Creek *is* Greywick's harbour. You land at the wharf, meet the truck buyers there, and walk up the road to the town for the licence, the rod and the shipwright. One scene, two characters: **the wharf is where fish move, the town is where money and paperwork move.** | **None.** Canon arc unchanged, no new region, no extra scene load, and the owner's layout is built exactly as briefed. |
| **B. Its own region between the bar and Greywick** | A separate landfall. You sell your first clams to a truck at Nine Mile Creek, then travel on to Greywick for gear and the dory. | A new scene, a new passage, and a longer prologue before the player owns anything. |
| **C. Nine Mile Creek replaces Greywick** | The market town is retired; this is the mainland. | Loses the auction, the shops and most of the NPC cast — canon-breaking. |

**Recommendation: A.** It gives the owner everything he described, changes nothing that is ratified,
and it earns a genuinely good beat for free: **your first sale is to a man in a truck on a wharf, not
at a market.** The market is the thing you graduate to, one road up the hill. It also matches the real
place — Nine Mile Creek is a working wharf on PEI's south shore, not a town.

Everything below is written for **A**, and holds for **B** unchanged apart from the scene extent
(§4).

---

## 2. Identity

The first place the sea is somebody's **job**.

St Peters is where you learn the tide by yourself. Nine Mile Creek is where you find out other people
have been doing this for a hundred years and are very good at it. It should feel busy, functional, and
slightly indifferent to you — boats you cannot afford tied to a wall you have no business standing on,
traps stacked higher than you are, a winch you are not yet allowed to touch, and a buyer who will give
you an honest price for a bucket of clams without making a fuss about it. **P3 at its purest.**

It is also the first place that is *not pretty on purpose*: red mud, gravel, tyre fenders, diesel, gull
mess, a shed with a rusted roof. The prettiness is St Peters'. This is the working coast.

---

## 3. Layout

The reference photograph reads as a spit with a hooked wharf, and the owner's compass notes fix it:

```
                                    B E A C H   (north end — the soft arrival)
        ┌───────────────────────────────────────────────┐
        │  fields / the road inland  →  (to the town)   │
        │                                               │
        │   ▣ bait shed   ▣ trap store                  │
        │        ▣ fish store / holds                   │
        │   ░░░ PARKING ░░░   ← buyers' trucks          │
        │        │                                      │
        │   ┌────┴─────────────────────────┐            │
        │   │ W E S T   W A L L            │ NORTH WALL │   ← deck (quay)
        │   │ ⚙ WINCH · unloading apron    │            │
        │   └──┬───────────────────────────┴────────────┤
        │      │        ~~~ B A S I N ~~~               │   ← moored fleet lies along
        │      │   ⛴ ⛴ ⛴ ⛴ ⛴ ⛴ ⛴ ⛴ ⛴ ⛴ ⛴          │     the north wall's SOUTH face
        │      └────────────┐                           │
        │                   └───▨▨▨▨▨▨▨▨▨▨▨▨▨▨▨         │   ← BREAKWATER (south)
        │                                               │
        │              open water / the approach        │
        └───────────────────────────────────────────────┘
                              ↑ camera looks from here (south)
```

**Why this layout is lucky.** The wharf kit bakes a tall vertical face on **south-facing edges only**
(single-camera convention). The owner's arrangement puts the moored fleet along the **north wall's
south face** — which is exactly the edge that gets the face. So the money shot is the one the kit draws
best: a tall quay wall, boats against it, the winch working above them. Had the boats moored on the
south side of the basin they would be seen over a curb, with their backs to the camera.

Two consequences worth naming before it is built:

- **The west wall's water side faces EAST**, which is a curb-only edge at this camera. The **winch**
  there will read as a deck silhouette against the sky rather than as something mounted on a visible
  face. That is fine — probably better — but it means the winch wants to be a tall, legible object
  (the kit's `boom` roof-hoist fitting on the adjacent shed is the same idea), not a detail on a wall.
- **The breakwater is a south arm**, so the camera sees its seaward face. `riprap` or `crib` is the
  right armour here — the reference photo is unmistakably a **timber crib** run, log boxes filled with
  stone, which is what a small community wharf actually builds. Save `sheet` pile for Greywick's
  commercial quay, where money and machinery show.

### What builds what

| Feature | Kit piece |
|---|---|
| Wharf deck, north + west walls | `WharfAtlas` **`quay`** row — concrete, tide-stained face, algae, waterline foam |
| Finger piers into the basin | `WharfAtlas` **`lowpier`** / **`tallpier`**, running **N–S** (the kit's known limit: a thin finger pier reads best N–S) |
| Any floating berth for the small stuff | `WharfAtlas` **`float`** — the one animated material, a ±1 px bob at ~6 fps |
| The south breakwater | `WharfBreakwaters` **`crib`** · `straight` for the run, `diag` for the hook, `end` to cap |
| Mooring hardware | `WharfOverlays` cleat · bollard · ring · **dolphin** (in the water) · **tyre** fenders down the face |
| Getting down to a boat | `WharfOverlays` **ladder** on the quay face; **gangway** to any float |
| Edge safety | `WharfOverlays` rails — **wood** on the old wharf, **pipe** where it has been repaired (they are drop-in swaps, so mixing them is free and reads as history) |
| Bait shed · trap store | `wharfBuildingRig` **`netShed`** / **`redShed`** / **`tealShack`** presets |
| Fish store / holds | `wharfBuildingRig` **`iceHouse`**, and **`storage`** type with `dock` fittings (raised loading dock + roll-up bays) |
| **NOT built** | `fishPlant` / `cannery` presets — the owner's "no processing here yet". They are the visible promise of a later tier. |
| Beach, north end | `ShoreIsoGround` sand/ripple + `ShoreIsoFringe` |
| Roads, parking, apron | `RoadIso` — **dirt/gravel** for the yard, a `concrete` apron at the winch |
| Stacked traps | Existing `potRig` art, stacked as dressing |

**Everything in that table already exists in the repo.** Nine Mile Creek is the first region that can
be built almost entirely from imported art rather than greybox — with one exception: the wharf
buildings still need an in-engine bake (see §6).

---

## 4. Size

Following [`scene-sizing-and-world-scale.md`](scene-sizing-and-world-scale.md) §1.3 — a foot region
with a harbour edge, 2–3 minutes to cross.

**Working footprint** (derived from the fleet it has to hold, not guessed):

| Element | Size | Why |
|---|---|---|
| North wall | **80 m** × 8 m deck | ~14 berths at 5.5 m spacing, bow-in to finger piers — matches the ~12 boats in the reference |
| West wall | 40 m × **10 m** deck | Wider: it carries the unloading apron, the winch, and a truck |
| Basin | 80 × **50 m** | A 12.9 m Cape Islander needs 2–3 lengths to turn; 50 m N–S is enough without being a parade ground |
| Breakwater | ~90 m arm | Shelters the basin's south side |
| Yard (parking + sheds) | ~60 × 40 m | Trucks, trap stacks, three or four buildings |
| Beach | ~60 m of shore | The arrival |

That is a **~200 × 160 m** working core.

**Scene extent, option A (Nine Mile Creek + Greywick town, one region): 600 × 400 m.**
Cross on foot in **3:20** (1:49 sprinting) — 37 screens. The wharf occupies the southern third; the
road runs inland to the town in the north. Slightly over the 2–3 min guideline *on purpose*: it is two
places, and the walk between them is what makes them feel like two places.

**Scene extent, option B (Nine Mile Creek alone): 420 × 320 m.** Cross in **2:20** — 26 screens.

### The depth gate — where this fits the ladder

The reef-ring ruling gave St Peters a **0.6 m** dock. Greywick is already authored as a dredged deep
harbour (`IsDeepHarbour`, `HarbourDepthMeters 6`, and a gentle 0.8 m tide so the market never strands
you). Nine Mile Creek's "up to the lobster boat in size" lands cleanly between them:

| Harbour | Basin gate | Admits | Excludes |
|---|---|---|---|
| **St Peters dock** | ~0.6 m | dory 0.30 · fishing skiff 0.35 · punt 0.50 · sport/console skiff 0.50–0.55 | everything larger |
| **Nine Mile Creek** | **~1.6 m** | all of the above **+ lobster boat 1.30 · Cape Islander 1.40** | **side dragger 2.90** |
| **Port Greywick** | 6 m dredged | everything, including the dragger | — |

**Three harbours, three depths, and they step exactly with the boat ladder.** The dragger — the boat
that makes you an operator rather than a fisherman — can enter exactly one harbour in the world, and it
is the one with the auction and the money. You feel your own promotion in where you are allowed to tie
up. This costs no new systems: draught is already real data and the painted seabed already decides
depth by tide.

> Note that `HarbourDepthMeters` is currently only read by the builders to set a seabed elevation —
> **nothing gates a boat on it.** The gating here is emergent from painted depth vs. `DraughtMeters`,
> the same mechanism as the reef ring. If it should ever be a *hard* rule with a "too shallow" warning
> rather than a grounding, that is a `gameplay-systems` seam, not a terrain one.

---

## 5. What the player does here

In prologue order, and deliberately small:

1. **Arrive on the beach** off the sandbar. Sand underfoot after a long walk on wet cobble.
2. **Walk the yard** — past trap stacks, bait sheds, the smell of it. Nothing to do yet. This is the
   establishing shot and it should be allowed to just be a place for thirty seconds.
3. **Sell the clams** to a buyer at his truck by the parking lot. The first money in the game.
4. **Watch a real boat unload** on the winch. You cannot use it. That is the point — the winch is the
   first thing in the game that is visibly *for later* (it belongs to the trap-haul loop, canon M2-33).
5. **(Option A)** Walk up the road to the town: licence, rod, shipwright, the damaged dory.

**Not here:** no processing, no auction, no shipwright at the wharf itself. The wharf handles fish; the
town handles money.

---

## 6. What has to happen before it can be built

1. ~~**Bake the wharf buildings in-engine.**~~ ✅ **DONE** — `BuildingRigBaker` bakes both building
   rigs (houses and wharf buildings), tight-cropping each preset to its drawn pixels and measuring the
   azimuth convention from the door anchor. Menu: *Hidden Harbours ▸ Art ▸ Bake Buildings*. The owner
   needs to run it once; the sheets are not committed until then.
2. **Stand up the wharf rule-tiles** — the 17-piece auto-tile set needs a `RuleTile` (or the paint
   tool) that also honours the back-to-front draw order the 24 px overhanging face requires. Not hard,
   but it is the thing that turns 119 slices into a paintable wharf. *(`art-pipeline` / `tools-editor`)*
3. **The §1 answer**, which decides whether this is a new scene or a zone of Greywick's.
4. Then the shared blockers from `scene-sizing-and-world-scale.md` §6 — the paint tool's hard-coded
   extent, region extent as data, and camera bounds.

---

## 7. Open questions

1. **§1 — Nine Mile Creek vs Port Greywick.** Everything else waits on this.
2. **Does the winch become usable later, here?** It is the natural home for the trap-haul unload beat,
   but the trap loop is canon M2-33 and this is a prologue region. Showing it now and unlocking it
   later is the cheap, strong version.
3. **Does the sandbar land at the beach, or at the wharf?** §3 assumes the beach, because arriving on
   sand and *then* finding the working wharf is the better sequence. The alternative — stepping
   straight onto a quay full of strangers — is more dramatic and less kind.
4. **Which armour for the breakwater?** §3 recommends `crib` from the reference photo. `riprap` is the
   cheaper-looking, older option; `sheet` should be saved for Greywick.
