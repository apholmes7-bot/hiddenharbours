# Hidden Harbours — The Mussel Lease & the Longline (DESIGN CAPTURE)

> **CAPTURE ONLY — nothing here is built, and nothing here is a licence to build it.** This doc
> exists so that when the mussel fishery *is* scheduled, the build starts from **canon written down
> on the day the owner said it** rather than from a stale design memory. Phasing:
> [`../roadmap.md`](../roadmap.md) folds mussel/oyster leasing into **M3**; the owner's 2026-08-18
> drop and the **starter-ruin** question both push on that date — §6 states the fork and the ruling
> that decides it. Until the owner rules, **the roadmap's phase stands** (CLAUDE.md rule 8).
>
> **Serves:** **P4 (Earn It, Then Automate It)** first and hardest — a lease is the game's purest
> "set it, come back, then hire it out" engine — then **P2 (Dory to Dynasty)** (owned water is the
> first thing you own that *works while you sleep*), **P1 (The Sea Has Moods)** (the crop's readiness
> is read off the same wave field the hull rocks on; storms are the risk), **P3 (Living Working
> Coast)** (a longline field is what a real inshore bay *looks like*), and **P5 (Cozy but with
> Teeth)** (a neglected line fouls or parts).
>
> **What this doc owns:** the **loop** — lease → set → seed → grow → read → tend → harvest →
> automate — and the contracts it needs from other lanes. **What it does not own:** the trap / haul
> machinery it reuses ([`boats-and-navigation.md`](boats-and-navigation.md) §6.3 and
> [`fish-and-content.md`](fish-and-content.md) §3.5(b) — **cross-linked, deliberately not
> restated**), the licence *system* ([`economy-and-business.md`](economy-and-business.md) §9.1a), the
> property/lease ownership mechanics ([`progression-and-housing.md`](progression-and-housing.md)
> §4.4), or the beds themselves (painted ground, ADR 0028 / ADR 0014, shipped).

---

## 1. The owner's words (verbatim)

> **"large sections of buoys with individual ropes"**
> — owner, **2026-08-18**, asking for the mussel harvesting loop and confirming the art is needed.

That sentence is the whole art brief and most of the design brief. It describes **PEI longline
mussel culture**, which is what the bay this game is drawn from actually does.

> **"the nine mile creek wharf will be home to approximately 16 lobster boats, and a new smaller
> class of boat for mussel fishing, lobster boats can also be outfitted to fish mussels"**
> — owner, **2026-08-07** (the world-map conversation).

That is the **automation end** of this loop, and the ruling that **gear-outfitting is an axis on a
hull, not a hull subclass** (§3.8).

> **"i made mussel beds, oyster beds and some other areas to be hidden by tides"**
> — owner, **2026-08-06** (the reef-bed splat pass; pinned in
> `Assets/Tests/EditMode/StPetersStarterSplatTests.cs`).

The **beds already exist as painted ground** (shipped #438 — ADR 0028's splat channels D.b
*musselbed* / D.a *oysterreef*). This loop does not invent them; it gives them a **fishery**.

---

## 2. The look — a longline, part by part

The player should be able to name every piece after one look, because every piece does a job:

| Piece | What it is | What it does in play |
|---|---|---|
| **Backbone** | A long horizontal rope, anchored at both ends, suspended below the surface | The thing you **haul over the gunwale** at harvest |
| **Float run** | The **run of small floats** strung along the backbone that holds it up | ⭐ **The status read** — as the crop puts on weight the run **rides lower** (§3.5) |
| **Droppers / socks** | The **individual ropes** hanging beneath the backbone, mussel seed socked onto them | The crop itself; the thing stripped on deck |
| **Corner buoys** | Bigger, marked buoys at the plot's corners | The **lease boundary, in the world** — you can see whose water you are in |
| **Anchors** | The backbone's ends, on the bottom | Why a set line stays where you set it — and why a storm can *part* it |

A worked lease is therefore **large sections of buoys with individual ropes**: many small floats in a
line, corner marks around them, and rope going down out of sight. It reads as *industry* at a
distance and as *your* industry up close (P3, P2).

---

## 3. The loop, beat by beat

### 3.1 THE LEASE — a plot of water, drawn on your own chart

A **lease** is a **plot of water over a musselbed** — the beds are the painted ground of ADR 0028,
and where they sit is where the leases sit. Nine Mile Creek's **inshore depth ladder** (~1.6 m at the
wall, the sheltered water inside — [`nine-mile-creek-wharf.md`](nine-mile-creek-wharf.md)) is the
natural home ground, with the other sheltered inshore water — Coddle Cove, the Sunkers, the Drownded
Lands edges — as the later spread ([`fish-and-content.md`](fish-and-content.md) §3.5(c)).

**Acquisition** — two doors, and which one the game opens first is the owner's ruling (§6):

- **The lapsed shellfish lease** — an abandoned lease you take over: the **starter-ruin candidate**.
  The repairable-ruin feeling with no new scene and no new buildings, on water the player already
  sails past.
- **Bought or renewed** later — a straightforward money + eligibility purchase, the same shape as
  every other licence the harbourmaster sells.

**Mechanically** the lease is two things and no more:

1. An **`ILicenseService` licence** (`Core/Economy/ILicenseService.cs`, shipped — the wallet the clam
   and cod licences already use). A lease licence is *held or not held*, persisted in
   `SaveData.OwnedLicenses`, and checked by the gear-setting gate. **No new save machinery.**
2. A **plot polygon** — the water it covers.

> ⭐ **The plot is drawn on the PLAYER'S CHART.** This is where the **player-authored chart** arc
> (the owner's 2026-08-17 onboarding conversation — its single highest-value find, and still un-doc'd)
> **meets the economy**: *setting gear starts from your own chart*. You mark your lease, and the marks
> you made are what the placement gate reads. The chart is not a menu *about* the lease — the chart
> **is** the lease's paperwork, the same way the notebook is the quest log
> ([`diegetic-devices.md`](diegetic-devices.md) §5.2) and the rope is the haul meter
> ([`boats-and-navigation.md`](boats-and-navigation.md) §6.3). If the chart layer has not shipped
> when this builds, the plot falls back to an authored polygon on the region Def and the chart layer
> **adds** the authoring later — the seam does not change.

### 3.2 SET GEAR — a line, not a point

From a boat **inside your plot**, you set a longline: anchor, backbone, float run, corner buoys.

A placed longline is a **world-placed persistent object of the ADR 0020 family** — the same
discipline the trap already lives under: **the save stores the placement facts only** (what kind of
line, where, when set, what seed went on it) and **everything else recomputes** (rule 5).

> **The one architectural difference from a trap: a longline is a LINE SEGMENT, not a point.** Every
> shipped piece of placed-gear machinery — placement gating, the depth check, the save DTO, the
> "is it ready" read — is written against a **position**. A backbone has two endpoints and a length,
> and those matter: it must fit inside the plot, both ends must find holding ground, and the *whole
> run* must clear the depth band. That is a real (if small) extension of the placement contract, and
> it is the first thing the build should **design**, not discover.

Placement refusals stay **cozy no-ops** with a plain reason, exactly as `PlaceResult` does for pots
today ("not your lease", "too shallow at the far end", "that line crosses another").

### 3.3 SEED — free and slow, or bought and fast

Two ways to put mussels on the line, and the choice is the P4 shape in miniature:

- **Spat collectors** — hang collector rope early in the season and the bay seeds it for you. **Free
  seed, slow**, and it costs you a *season position*: you must be early.
- **Bought socked seed** — seed already socked. **Fast, costs money**, and it is how a player with
  capital skips the wait.

**Socking is deck work** — by hand first, hired hands later (P4). It joins the existing deck-work
family (`Fishing/DeckWork.cs`, `DeckWorkDef` — the pot's pick/sort/band cycle,
[`fish-and-content.md`](fish-and-content.md) §3.5(b) Build 7) rather than inventing a second on-deck
idiom.

### 3.4 GROW — deterministic, recomputed, never ticked

Crop state is a **pure function of `(set gameTime, now, site quality)`**, where *site quality* comes
from things the world already knows: **depth band**, **current**, and **season**.

**It is recomputed, never ticked and never saved** (rule 5 — exactly how trap soak works). A lease
that has sat through three real-world weeks and a lease loaded from a save at the same `gameTime` are
**bit-identical**. No growth accumulator lives in `SaveData`: the save holds the set time and the
seed choice, and the crop falls out of the clock.

### 3.5 READ THE CROP DIEGETICALLY — ⭐ the float run rides lower

**There is no progress bar. There is no percentage. There is no "Ready!" toast.**

As the mussels grow they get **heavy**, and the float run **rides lower in the water**. Late in the
cycle individual floats begin to **submerge, one by one**. A line **sitting low with floats dipping
under** is a line that is **ready** — and the player learns to read that the way they learn to read a
tide.

This is not a new rendering system: the **shared deterministic wave field** (ADR 0018) and the buoy's
float/submerge behaviour already do exactly this for the trap buoy and the nav buoys
(`Boats/BuoyWaveMath.cs`, `BuoyWaveVisual.cs`). The crop's weight is **one more input to the
waterline the float sits at**. The status UI is *the sea, doing its job*.

> **Why this is the keystone beat.** It is the standing diegetic-UI direction
> ([`diegetic-ui-and-inventory.md`](diegetic-ui-and-inventory.md) — minimal HUD, information is
> earned) landing on a **production** system for the first time: you learn the crop by **looking at
> it**. It is also the cheapest readiness UI we could build — the floats already bob.

**Teeth (P5):** a **neglected line fouls or parts in a storm**. Weed and drag build on an untended
line; a real blow can **part a backbone** and cost the season's crop on that run. The bite is a
**tuning knob**, not a fixed rule — the cozy setting costs *time and yield*, never something
unrecoverable, which is the posture the trap haul already took ("cozy — no penalty", owner's M2
call). The exact severity is an owner ruling (§8.3).

### 3.6 TEND — the mid-cycle visits that mean something

Two tending actions, both of which exist *because the crop's weight is real*:

- **Add floats** as the weight builds — the direct counter to §3.5's sinking run, and a reason to
  come back mid-season that is not a chore timer.
- **Re-space droppers** — optional depth work: droppers hung deeper or shallower trade growth rate
  against fouling and exposure.

Tending is what turns a lease from a timer into a **fishery you work**. It is also the first thing
you hand to a crew (§3.8).

### 3.7 HARVEST — lay alongside, haul the backbone, strip on deck

Harvest is **assembled from machinery that is already shipped**:

1. **Lay alongside** the line and hold station — the same handling beat as laying alongside a buoy,
   with wind and current setting you off the mark
   ([`boats-and-navigation.md`](boats-and-navigation.md) §6.3, step 2).
2. **Haul the backbone over the gunwale** with the **haul-with-the-swell** interaction — hold on the
   lift, ease on the fall, read off the shared wave field, no HUD meter (§6.3's shipped Build-6
   redesign). A backbone is longer and heavier than a pot, so this is the same verb at a **different
   weight** — a tuning difference, not a new minigame.
3. **Strip the socks on deck** — the deck-work family again (§3.3): mussels come off the dropper by
   hand first, by machine and crew later.
4. **Totes of mussels** enter the **freshness chain**
   ([`economy-and-business.md`](economy-and-business.md) §3 — `freshnessMult`, timestamps not
   countdowns) and sell through the existing sell points. Mussels are a live, perishable product:
   cold storage and the wet well matter to them exactly as they matter to lobster.

> **Content shape (unchanged from [`fish-and-content.md`](fish-and-content.md) §3.5(c)):** farmed
> mussels are **not a wild `FishSpecies` roll**. They are **harvested from a lease at maturity** — a
> grow function plus a yield — so they belong to the **production/economy layer**, not to
> `CatchResolver`. The existing wild `blue-mussel` (hand-gathered off the rocks at low water) stays
> exactly as it is; the farmed product is its own commodity.

### 3.8 AUTOMATE (P4) — the boat, then the crew

The end of the arc, and the reason the loop is worth its slot:

- **The new small mussel-boat class** — the owner's 2026-08-07 ask: a smaller hull beside the lobster
  boat, **shallow-draughted** (leases are inshore) and **low-freeboard** (you work the backbone over
  the rail).
- **Lobster boats OUTFITTED with mussel gear** — and this is the ruling that matters: **the fishing
  role comes from the gear fitted to a hull, not from the hull's class.** Mussel gear is an
  **outfitting axis** (like the instruments/equipment model) and is the first proof that a hull can
  hold a second role. **No mussel-boat subclass hierarchy.**
- **Crew + a powered hauler** automate **tending and harvest** — the canon P4 turn: you do it by hand
  until you have earned the right to stop ([`economy-and-business.md`](economy-and-business.md)
  §5.5).

---

## 4. What this reuses (the point of the design)

Almost nothing here is new machinery. That is deliberate — it is why the loop is cheap for what it
gives.

| Beat | Reuses | Status |
|---|---|---|
| Lease held / checked | `ILicenseService` + `SaveData.OwnedLicenses` | **Shipped** (clam + cod licences) |
| Line placement + persistence | The ADR 0020 world-placed-object family | **Shipped for points**; a line segment is the extension |
| Growth | Deterministic recompute from `(setTime, now)` — the trap-soak pattern | **Shipped** (`TrapSoak`) |
| Readiness read | Shared wave field + buoy float/submerge | **Shipped** (ADR 0018, `BuoyWaveMath`) |
| Harvest haul | Haul-with-the-swell (`TrapHaulController` / `TrapHaulMath`) | **Shipped** (Build 6) |
| Deck work (sock, strip) | The `DeckWork` / `DeckWorkDef` pick-sort-band family | **Shipped** (Build 7) |
| Sell + spoilage | The freshness chain, sell points, cold storage | **Shipped** |
| Crew + hauler | The staff & automation engine | **M3** ([`economy-and-business.md`](economy-and-business.md) §5) |
| Player-drawn plot | The player-authored chart | **Not built** — owner conversation 2026-08-17, no doc yet |
| **Line-segment placement** | — | **NEW** — the one genuinely new contract (§3.2) |
| **Weight → waterline** | — | **NEW, small** — one more input to an existing float (§3.5) |
| **Farmed-crop commodity** | — | **NEW, small** — a production good, not a catch roll (§3.7) |

**Character animation:** harvest **reuses haul / lift / place / toss**. **No new player sheets for
v1** — the art contract (§7) is props and gear, not a character pass.

---

## 5. Determinism & save contract (rule 5 + ADR 0020)

Stated plainly so the build cannot drift:

- **Saved (irreducible player choices):** the lease held (licence id); the plot (once the chart layer
  can author it); and per line — kind, both endpoints, set time, seed type + seed time, floats added,
  dropper spacing.
- **Never saved (recomputed):** crop mass and maturity, the float run's waterline, yield, fouling,
  whether a blow has hurt the line. All of it derives from the saved facts plus
  `(worldSeed, gameTime)` and the weather the sim already recomputes.
- **Seeded rolls** (which line fouls; per-dropper yield scatter) hash off the **same lineage the trap
  catch uses** — `worldSeed + instanceId + placementTime` plus a channel — so save → load → harvest
  lands the **identical** crop. EditMode-pin it the way `PlacedTrapCatch` is pinned.
- Any change to *what is saved* is a **save-schema change**, and therefore an **ADR plus a version
  bump** (ADR 0008 / ADR 0020) — not a field someone adds in a feature PR.

---

## 6. Phase, and the starter-ruin fork

**Today's canon:** [`../roadmap.md`](../roadmap.md) folds **mussel/oyster aquaculture leasing into
M3**, beside the staff/automation layer, and [`fish-and-content.md`](fish-and-content.md) §3.5(c)
says the same. **That stands until the owner rules otherwise** (rule 8).

**What pushes on it:** the coordinator's 2026-08-18 handoff targets the **build at M2**, and the open
**starter-ruin ruling** (owner conversation, 2026-08-17) lists *the lapsed shellfish lease* as one of
three candidates for the game's first repairable, ownable thing — alongside the **camper lot growing
outbuildings** and **Ginny's derelict sheds becoming purchasable**.

**The fork, stated once:**

- **If the lease is NOT the starter ruin** → the loop builds whole, at its scheduled phase, as
  written above.
- **If the lease IS the starter ruin** → the **lease + a single line + harvest** pull forward into
  **M1**, and everything else (spat vs. bought seed, tending depth, the mussel-boat class, crew)
  stays where it is. The coordinator re-scopes; this doc does not.

> **The ruling the owner owes:** *whose ruin is the starter ruin* — the camper lot, the sheds, or the
> water. It is one sentence, and it re-phases this doc.

---

## 7. The art contract (the owner paints ahead of the build)

Art for this arrives from the owner's tool **before** the build, as it has for every kit. The
authoritative commissioning list lives with the coordinator; it is reproduced here so the art lane
and the build lane read the same list:

| Asset | Notes |
|---|---|
| **Float-run segment sprites** | The run of small floats, drawn to sit **on the shared wave field** — must bob and submerge through the existing buoy path |
| **Corner lease buoys** | Bigger, marked; the plot boundary made visible |
| **Sock / dropper sprites at 2–3 growth states** | Seed → half-grown → ready: the same rope at three weights |
| **Spat collector** | The free-seed gear (§3.3) |
| **Deck totes of mussels** | The harvest's on-deck container, in the carried-props family |
| **Low-freeboard mussel barge rig** | **Later** — the mussel-boat class (§3.8), not v1 |

**Not needed for v1:** new player character sheets (harvest reuses haul / lift / place / toss). Rows
land in [`../art/asset-manifest.md`](../art/asset-manifest.md) **when the owner commissions them** —
this list is a plan, not a manifest entry.

---

## 8. Owner rulings owed (standing)

1. ⭐ **Whose ruin is the starter ruin** — the camper lot, Ginny's sheds, or **the lapsed lease**
   (§6). This one re-phases the doc.
2. **The musselbed WINDOW + SPOTS ruling** *(standing since the reef-bed pass)* — at what **tide
   window** a musselbed bares, and **where** the beds actually sit in the world. The beds are painted
   and shipped; the fishery needs to know **which water is leaseable**, and that is this ruling.
3. **Cozy-bite tuning for storm damage** (§3.5) — foul-and-lose-yield, or part-and-lose-the-line? The
   trap loop's precedent is "cozy, no penalty"; a farm with **no** risk has no teeth at all.
4. **Season shape** — is the grow cycle tied to the seasonal calendar (be early or wait a year) or a
   rolling N-day cycle? The first is truer and much harsher; the second is kinder to a player who
   finds the lease late.

---

## 9. Cross-references

- [`fish-and-content.md`](fish-and-content.md) §3.5(c) — aquaculture's capture entry (**this doc is
  its expansion**); §3.5(b) — the trap / bait / soak side of the machinery reused here.
- [`boats-and-navigation.md`](boats-and-navigation.md) §6.3 — **the trap-hauling interaction**: lay
  alongside, leave the helm, haul with the swell. Harvest is that verb at a different weight.
- [`economy-and-business.md`](economy-and-business.md) §3 (freshness / spoilage), §5 (staff &
  automation), §7 (properties), §9.1a (the licence system).
- [`progression-and-housing.md`](progression-and-housing.md) §2.2 (licences & permits), §4.4
  (commercial property — the ownership side of a lease).
- [`nine-mile-creek-wharf.md`](nine-mile-creek-wharf.md) — the home wharf, the depth ladder, and the
  ~16-boat fleet this fishery belongs to.
- [`world-map-plan.md`](world-map-plan.md) — the mid-bay home grounds; the mussel fishery's water
  arrives with map phase 4.
- [`diegetic-ui-and-inventory.md`](diegetic-ui-and-inventory.md) and
  [`diegetic-devices.md`](diegetic-devices.md) — why §3.5 has no progress bar.
- ADR [0018](../adr/0018-shared-wave-field.md) (the wave field the floats ride),
  [0020](../adr/0020-world-placed-object-persistence.md) (placed-gear persistence),
  [0028](../adr/0028-terrain-splat-ground.md) + [0014](../adr/0014-painted-seabed-height-authoring.md)
  (the beds are painted ground), and [0008](../adr/0008-save-schema-and-versioning.md) (the save
  contract §5 lives under).
