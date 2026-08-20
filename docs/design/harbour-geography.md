# Hidden Harbours — the harbour's geography: Route 91, the three rivers, the marina

> **Status: DESIGN CAPTURE of the owner's 2026-08-20 rulings.** Six new facts about the bay's
> landform were ruled (§1). This document writes them down one level above any scene — what the
> land *is*, not how a builder lays it out — and states honestly what each one costs the built
> world. **Nothing under `Assets/` is changed by this document.**
>
> Subordinate to [`../vision-and-pillars.md`](../vision-and-pillars.md) (canon) and
> [`world-map-plan.md`](world-map-plan.md) (which owns the bay's *regions and ports*; this document
> owns its *landform*). Sizing law: [`scene-sizing-and-world-scale.md`](scene-sizing-and-world-scale.md).
> The built mainland it must not contradict: `NineMileCreekMainland.cs` and
> [`nine-mile-creek-mainland.md`](nine-mile-creek-mainland.md).
>
> **The headline, and it is good news:** five of the six rulings cost the built world *nothing* —
> they describe land north of Nine Mile Creek's region edge, which has never been built. **One of
> them, the town river, re-cuts Nine Mile Creek**, and §5 costs that out without flinching.

---

## 1. What was ruled, 2026-08-20

The owner attached the Google satellite of the real Charlottetown Harbour and ruled six things onto
it. The 2026-08-07 ruling already made the home world *a variant of Hillsborough Bay*
([`world-map-plan.md`](world-map-plan.md) §1); tonight fills in the land north of home water.

| # | Ruling | Real analogue | Costs the built world |
|---|---|---|---|
| 1 | **Route 91** is the road Wharf Road merges onto. It runs **north** and crosses a **west-running river**, making the peninsula/point that heads **east** toward the harbour entrance. | Rte 19 north; the West River | **Nothing — it already exists.** §2 |
| 2 | Route 91 crosses **two large rivers** (one west, one north) before reaching **Finnigan's Landing**, which has an **east-running river** of its own. | West · North · Hillsborough Rivers | Nothing — all north of the region edge. §3 |
| 3 | Those **three rivers are shellfish and river-fishing waters** — tidal, carrying leases. | PEI's river mussel leases | Nothing yet; M2+/M3 gameplay. §4 |
| 4 | **A river runs off the ocean up through Nine Mile Creek town**, dividing it and making small picturesque **boardwalks**. | the real Nine Mile Creek | ⚠⚠ **This one re-cuts the region.** §5 |
| 5 | **A marina** on the **south side of the peninsula**, north of St Peters Island. | the Charlottetown-area marinas | Nothing built; a region-level mark. §6 |
| 6 | The harbour-mouth shoreline **mirrors Rocky Point**: a point, a small bay behind it, opening to Finnigan's Landing's harbour. | Rocky Point / Fort Amherst | Nothing built. §3.2 |

**Route 91's name is RULED. Nothing else here is named** — the PEI-variant name slate is still owed
to the owner, so §9 lists candidates and christens nothing.

**Two context rulings from other lanes** shape this document without being owned by it: Nine Mile
Creek has all the basic amenities of a town and **more residents than St Peters Island**; and every
boat wants **2–3 crew NPCs who live locally and drive to the wharf**. Both belong to the
settlement-population lane. They matter here only because they say the town is the thing that grows,
which is what makes §5's cost worth paying.

---

## 2. Route 91 — the trunk (⭐ **it is already built, and it is already this road**)

**The single most useful finding in this document.** Route 91 is not a road to be built. It is
`NineMileCreekMainland.ThroughRoad` — shipped, tiled, tested — and the builder's own comment already
calls it *"the overhead's Route 19"*. The owner has just named it and said where it goes.

| Fact | Value | Source |
|---|---|---|
| Route | `ThroughRoad`, 6 points | `NineMileCreekMainland.cs` §9 |
| Length in-region | **564.4 m** | derived by `RouteLength`, never typed |
| Enters region (south) | `(-230, -280)` | |
| **Wharf Road junction** | **`(-178, 92)`** = `WharfRoad[0]` = `ThroughRoad[3]`, **375.8 m along** | the town's centre by construction |
| **Leaves region (north)** | **`(-186, +280)`** | ⭐ where Route 91 heads north out of M1 |
| Class | rural through-road; the community is strung along it | the municipal lane tree roots here |

So the owner's *"Wharf Road merges onto Route 91"* is a statement about a junction the game already
has, at a coordinate six test files already assert. **Naming it costs one display string** — region
and route ids stay stable (ADR 0009); `ThroughRoad` is a code identifier, not a player-facing name.

### 2.1 North of the region edge — capture, not content

Everything from `(-186, +280)` northward is **unbuilt and stays unbuilt**. M1 is St Peters + Nine
Mile Creek ([`../roadmap.md`](../roadmap.md)); the trunk is captured here so that when the north of
the bay is built it is not re-invented. In order, heading north:

| Along the trunk | What it is | Phase |
|---|---|---|
| `(-186, +280)` | Route 91 leaves the Nine Mile Creek region | built |
| **Crossing #1** | the **west-running river** | M3 |
| **The peninsula / point** | begins on the far bank; heads **east** toward the harbour entrance. Its **south shore** carries the marina (§6); its **east tip** is the point (§3.2) | M3 |
| **Crossing #2** | the **north-running river** | M3 |
| **Finnigan's Landing** | the city; the **east-running river** runs inland from its harbour | late |

### 2.2 ⚠ The two crossings are FEATURES, and their kind is the owner's call

A river crossing is a landmark, a gate, and possibly a scene seam. Which it is changes the game, so
this document **does not pick**:

| Option | What it buys | What it costs |
|---|---|---|
| **Bridge** | Always open; a silhouette; the road just works | Nothing gates; and **no bridge art ships** (§8) |
| **Causeway** | Cheapest art — the road kit already tiles every surface it needs | Wants a tide story: is it ever overtopped? |
| **Ferry** | ⭐ A real gate, a schedule, an NPC, a fare — P1 and P5 in one feature | A whole system; M3+ at the earliest |

**Recommendation, non-binding:** a **bridge** for the west river — it is on the daily road to the
marina and a gate there would be an irritation, not drama. Hold the **north river** open: it is the
threshold to the city, and a ferry there would be the most Hidden-Harbours answer available. §10 Q2.

---

## 3. The three rivers

### 3.1 What they are

Three large tidal estuaries radiating from the head of the bay. The owner ruled their bearings, and
they are the chart's own: **west-running**, **north-running**, **east-running**.

| River | Runs | Met by Route 91 | Carries | Phase |
|---|---|---|---|---|
| The **west river** | WNW inland from the bay | crossing #1 | ⭐ shellfish leases; river fishing | M3 |
| The **north river** | N inland | crossing #2 | shellfish leases; river fishing | M3 |
| The **east river** | E/NE inland from the city's harbour | — (it is *at* the city) | shellfish leases; river fishing | M3, with the city |

**They are tidal**, and that is not decoration. It means every river reach obeys the law the bar and
the wharf already obey — `waterLevel − bed > draught`, recomputed from `(worldSeed, gameTime)` and
never saved (CLAUDE.md rule 5). A river you can enter at half-flood and cannot leave at half-ebb is
P1 and P5 in one piece of geography, and it is **free**: the systems that do it already ship.

### 3.2 ⭐ The harbour mouth — the Rocky Point mirror

The peninsula's **east tip is a point**, and behind it a **small bay** opens, which in turn opens to
**Finnigan's Landing's harbour**. That is the approach the player sails on the late-game city run,
and its shape is a gift: a point to round, a sheltered pocket behind it, then the city. All three
rivers discharge through that same mouth, which is why a real tide runs in it.

> **Design note, logged not scheduled.** The point is the natural site for the bay's **navigational
> set-piece**. The nav-buoy kit already ships, channels are already data, and IALA-B marks are
> already computed from them. A point to round with a marked channel behind it is the best possible
> advertisement for the chart the player has been learning to read.

### 3.3 How a boat enters a river — the proposed reach ladder

The brief asks for draft limits that **reuse the tide-window logic already gating Nine Mile Creek**,
with rivers shallower still. Two laws bind before any number is picked:

1. **Gating is emergent, never a rule** ([`world-map-plan.md`](world-map-plan.md) §2.3):
   `waterLevel − bed > draught`. No wall turns a boat away; the ebb does.
2. **Tidal elevations are authored as TIDE FRACTIONS of the amplitude, never as metres** — the
   2026-08-01 pacing ruling, already obeyed by the cliff toe (0.45) and the ledge bench (0.25).

⭐ **The happy finding: the rivers need no new depths.** Re-read Nine Mile Creek's own shipped ladder
as fractions of its ±2.2 m amplitude and it already *is* a three-step river profile:

| Nine Mile Creek's shipped level | metres | as a fraction of amplitude |
|---|---|---|
| Harbour shoal / basin (the ruled gate) | −1.60 m | **−0.73 × A** |
| Barachois | −0.80 m | **−0.36 × A** |
| Marsh pool | −0.40 m | **−0.18 × A** |

So the proposed ladder is those three numbers, rounded to clean fractions and applied **per reach** —
mouth, mid, head — rather than to a whole river:

| Reach | Bed | at A = 2.2 m | Bare at | Dory 0.30 | Punt 0.50 | Lobster 1.30 |
|---|---|---|---|---|---|---|
| **Mouth / thalweg** | **−0.75 × A** | −1.65 m | 23.0 % | 71.0 % | 67.5 % | **55.1 %** |
| **Mid reach** | **−0.35 × A** | −0.77 m | 38.6 % | 56.9 % | 53.9 % | **42.3 %** |
| **Head reach** | **−0.15 × A** | −0.33 m | 45.2 % | 50.4 % | 47.5 % | **35.5 %** |

*Percentages are fraction of the tidal cycle afloat, computed the way the builder computes its own
published fleet table — and **verified against it**: at the −1.6 m basin this arithmetic reproduces
the builder's dory 70.1 %, lobster 54.4 % and dragger 29.9 % exactly.*

Read the right-hand column. A **lobster boat works the mouth for over half the cycle, the mid reach
for a nagging 42 %, and the head barely at all** — while the dory and the punt go everywhere. That is
exactly the shape the mussel fishery wants
([`mussel-lease-and-longline.md`](mussel-lease-and-longline.md): leases sit in sheltered inshore
water), and it makes the **small mussel-boat class a reason rather than a purchase** — the boat that
can work the whole river when the lobster boat cannot.

> ⚠ **PROPOSED, not ruled.** These are one architect's fractions, chosen because they are the
> region's own. The lever is always the **bed**, never the tide: tides are shared across neighbouring
> regions by law and must not be tuned per-place (`NineMileCreekMainland.cs` §2 — two tides either
> side of one seam is how the bar breaks).

> ⚠ **A river reach must stay wet at its thalweg or it is not navigable — and the primitive exists.**
> `MainlandChannel`, added by the 2026-08-19 bullpen ruling, is a **meandering trough that narrows as
> the tide falls and never closes** (24 m → 16 m over the last 0.6 m of ebb, with nothing animating
> it). Rivers are the second customer for a primitive built for the first. **⚠⚠ And the lesson that
> came with it applies here in full: a channel must JOIN something.** A river wet at every station
> and leading nowhere is a feature that tests green and plays dead — the journey is the acceptance
> criterion, not the depth.

---

## 4. The rivers as fishery

The owner ruled the three rivers are **shellfish and river-based fishing waters** — tidal, home to
leases and other harvesting. This lane is **M3** and is owned by
[`mussel-lease-and-longline.md`](mussel-lease-and-longline.md) and `economy-sim`; captured here only
where it is *geography*:

- **Leases are places, and places are chart marks.** The lease plot is drawn on the **player's
  chart**, so a river lease is a polygon in a river region — the same shape the mussel doc already
  specifies for inshore water. No new mechanism.
- **A river lease has a different constraint from a bay lease**, and it is the one interesting
  thing geography contributes: the bay lease is gated by *weather*; the river lease is gated by
  *tide*, because the reach ladder above says when you can reach it at all. That is a genuinely
  different working day out of the same system.
- **Which river carries which fishery is unruled** and does not need ruling until M3. The three
  bearings are enough for now.

---

## 5. ⚠⚠ The Nine Mile Creek town river — the one ruling that re-cuts built ground

> *"A river will run off the ocean and up through Nine Mile Creek town. It divides the town and
> creates small picturesque boardwalks."*

This is the only ruling that lands on shipped geometry, and the impact list below does not minimise
it. Three parts: what fits beautifully, what does not fit at all, and what it costs either way.

### 5.1 ⭐ What already fits — the estuary is built

The river's seaward half **already exists**, and nobody has to author it:

| Built feature | Where | What it becomes |
|---|---|---|
| **The coast NOTCH** — `CoastPoints` P8…P11 | `(44,128) → (26,134) → (24,152) → (40,168)`, 41.8 m of run | ⭐ **the river mouth.** The builder already calls it *"the creek mouth"* |
| **The barachois carve** | centre `(-10, 132)`, half-size `(54, 26)` → x ∈ [−64, +44], y ∈ [106, 158]; bed −0.8 m | ⭐ **the river's tidal lagoon** |
| The notch's aspect exemption | its inner banks face NE and NNE — the one stretch where **no cliff may stand** | exactly right: a river mouth is marsh and sand, not rock |

⭐ **The insight that makes this cheap: a barachois *is* an estuary lagoon behind a bar.** The owner's
river is the thing that made it. The region was already drawn as a river mouth by someone reading
the same photographs — the ruling names what is there and asks it to continue inland.

**So the seaward reach costs nothing.** The mouth, the lagoon and the aspect exemption are shipped,
tested, and correct.

### 5.2 ⚠⚠ What does not fit — the valley cannot pass between the town's lots

Continue that river west and it must cross the **fields at +6.0 m** to reach the town at
x ≈ −110…−210. The town's nine lots sit in four rows:

| Row | y | Lots |
|---|---|---|
| north houses | 196 | house-north `(-148)` · house-north-west `(-206)` |
| commercial | 152 | general store `(-150)` · tavern `(-206)` |
| civic | ~117 | harbourmaster `(-150, 118)` · chandlery `(-206, 116)` |
| south | ~61 | parish hall `(-152, 62)` · house-south `(-208, 60)` · boat shed `(-108, 64)` |

A river on the barachois' own line — **y ≈ 135** — threads the commercial and civic rows and
**divides the town 4 north / 5 south**, exactly as ruled. It even crosses Route 91 at a clean point:

> **The bridge site is `x = -177.0, y = 135` — 418.8 m along Route 91**, 43 m north of the Wharf Road
> junction. The working side (wharf, harbourmaster, chandlery, parish hall, truck park) ends up
> **south** of the river; the store and the tavern **north**. You cross the bridge for a drink. That
> is a real daily-loop consequence and a good one.

**And then the arithmetic refuses.** The valley is `+6.0 m` of field down to a `−0.77 m` bed —
**6.77 m deep** — and a bank is a slope, not a wall:

| Bank slope | Metres of bank needed **per side** | Total valley |
|---|---|---|
| 1:12 (gentle) | 81.2 m | 162.5 m |
| 1:8 (PEI-natural) | 54.2 m | 108.3 m |
| **1:4 (steep — the floor)** | **27.1 m** | 54.2 m |

Against that, the gap between the commercial and civic rows is **35 m**, less 6 m of `TownLotRadius`
on each side, leaving **23 m total — 11.5 m per bank.** That is **42 % of the steepest legal valley**,
and the shortfall is 15.6 m *per side*.

⚠ **And the cliff kit cannot rescue it.** Steepening the banks into rock is illegal on one side by
the same aspect law that exempts the notch: an east–west valley's **south bank faces north**, and
the kit authors no north facing (W · SW · S · SE · E only). So both banks must be **graded ground** —
splat and grass — which is precisely what needs the width. Every law points the same way.

**The honest conclusion: the town river as ruled does not fit the town as built.** Something moves.

### 5.3 The four ways out, costed

| # | Option | What it costs | Verdict |
|---|---|---|---|
| A | **Move the lots** — open the gap to ≥ 54 m | 2–4 of 9 lots re-sited; the municipal lane table's along-distances re-derived; several test files | ⭐ **Recommended** — see below |
| B | **Lower the town onto a river terrace** (say +3 m instead of +6 m) | Still needs ~30 m per bank at 1:8; re-cuts the whole town's ground, the splat pass and the forest lots | Does not solve it alone |
| C | **Make the town a valley-floor settlement** — the truest reading of *"a river runs off the ocean up through the town"* | The largest re-cut in this document: the town's ground, Route 91's profile, every lot, the fields' edge | Best-looking, most expensive |
| D | **Stop the river at the town's east edge** (x ≈ −110) — a tidal creek head, not a river through the town | Nearly free | ⚠ **Fails the ruling** — it would not *divide* the town |

**Recommendation (A, with a touch of B):** move the **civic pair south** — harbourmaster and chandlery
from y ≈ 117 down to y ≈ 95–100 — which opens a **54 m** gap *and improves the town on its own
merits*: it puts the civic pair on the Wharf Road junction they serve, which the municipal design
already calls the place *"where a village green would be if this settlement had one."* Add a modest
riverside terrace and the banks come in at 1:8. Nothing else in the town moves.

⚠ **This is a recommendation, not a ruling — §10 Q1.** It re-sites two lots and re-derives two rows
of the lane table, and that is the owner's call to make, not world-content's to take.

### 5.4 ⭐ The boardwalks — the art ships, and so does the mechanism

The owner asked for *"small picturesque boardwalks"*. Both halves already exist:

- **The surface ships.** `Assets/_Project/Art/Tilesets/RoadsV3/RoadIso3_boardwalk_new_blob47.png` — a
  **0.35 m deck on piles**, blob-47, in road kit v3 alongside gravel, dirt, cobble, brick, asphalt,
  concrete, sand, apron and oiled dirt. **Do not re-ask art for it.**
- **It is deliberately unused today, and the reason does not apply here.** `NineMileCreekRoadPainter`
  refuses to lay it because the region's only decking is the **floating** dock, and *"a tilemap cell
  is nailed to one elevation, so a boardwalk laid there would be swallowed at high water or left
  standing over dry mud at low"* against 4.4 m of tide.
- ⭐ **A riverside boardwalk is the case that works** — it is **static**, not floating.
  `StandablePlatform` is exactly the contract: an axis-aligned footprint plus an authored deck
  height, registering itself so the fisher stands on the deck and not the mud beneath it. Its own
  doc-comment names the rule: *author the deck clear of the region's highest water and it is dry at
  every tide.*
- **So the one number a boardwalk needs is its deck height**, and the region already has the
  precedent: the wharf deck is **+3.0 m**, i.e. 0.8 m of freeboard at spring high (+2.2 m). A town
  boardwalk at the same +3.0 m is dry at every tide, sits 3.77 m above the ruled mid-reach bed, and
  its piles stand in water for ~61 % of the cycle and on red mud for the rest — which is the picture
  the owner asked for.

**Net: the boardwalks are the cheapest part of this ruling, not the most expensive.**

### 5.5 The impact list, in full

Everything the town river touches, whether or not §5.3 is resolved. Nothing here is padded and
nothing is omitted.

| # | What re-cuts | Severity |
|---|---|---|
| 1 | **`NineMileCreekMainland` carves** — the barachois extends inland as a river; a `MainlandChannel` thalweg keeps it wet | moderate — both primitives exist |
| 2 | **The painted seabed must be re-baked** — `PaintedSeabed`, 760 × 560 m at 2 px/m | mechanical, but it is a bake |
| 3 | **The coast run** — ⭐ **recommend leaving `CoastPoints` untouched.** The notch is already the mouth; author the river as *terrain* meeting the sea there. Detouring the coast run up the river and back would double its length and invalidate every s-distance in the coast plan's sector table and its tests | **avoidable — take the cheap path** |
| 4 | **Route 91 needs a bridge at `(-177, 135)`** — and ⚠ **no bridge art ships.** A real art ask (§8) | ⚠ blocking for the town river |
| 5 | **Town lots** — 0 or 2 move, per §5.3 | the open ruling |
| 6 | **The municipal lane table** — the bridge lands at **418.8 m** along Route 91, between `road_civic` (400 m) and `road_commercial` (436 m). ⭐ **One new node; the tree stays a tree**, no cycle | ⭐ small |
| 7 | **The boardwalks** — new routes of a new class, on `StandablePlatform` | small; art ships (§5.4) |
| 8 | **Splat ground, grass and forest passes** over the new water and its banks | mechanical |
| 9 | **The coupled Nine Mile Creek EditMode test files** (terrain, dressing, fields, photograph, greybox-ground, dory) | mechanical but real |
| 10 | **Unaffected, checked:** the utility-pole line (follows Wharf Road, not Route 91) · the truck park at `(-120, 106)`, 29 m clear · the marsh pool and its neck · the crossing to St Peters · the wharf entire | — |

⚠ **Phase note.** Nine Mile Creek is an **M1 region under active work** — the wet-wharf arc, the
building-lifecycle arc and the municipal design are all in flight against this exact geometry. A
re-cut of the town **must be sequenced after they land**, or it merges into four open branches at
once. This document does not schedule it; it flags it. §10 Q3.

---

## 6. The marina

**Ruled:** a marina on the **south side of the peninsula**, north of St Peters Island — the pleasure
and small-craft harbour, distinct from Nine Mile Creek's working wharf.

### 6.1 What a marina *is* here

The distinction is the point, and it is a P3 (Living Working Coast) distinction:

| | **Nine Mile Creek wharf** | **The marina** |
|---|---|---|
| Whose boats | the working fleet — ~16 lobster boats, the mussel class | pleasure and small craft; visitors |
| How they lie | rafted pairs, moorings, tyre fenders, a wall you climb a ladder up | **floating slips** in rows, finger docks |
| Ground | made red fill on a shoal; dries under the fleet at spring low | dredged or naturally deeper; slips float, so it never dries |
| Services | fish buyer, bait, ice, traps, a crane | **fuel**, water, power, a chandlery aimed at visitors |
| What it says | this is where people **work** | this is where people **arrive** |

⭐ It is also the natural **fuel** stop for a boat heading north — the gas/diesel split is already
designed, and a marina is where a pleasure hull buys gasoline while the fleet burns diesel at home.

### 6.2 Where it sits, and ⚠ one phrase that needs the owner

The peninsula's **south shore faces the bay, with St Peters Island offshore to the south** — which is
exactly the real chart's arrangement, so the placement is coherent on its own terms.

⚠ **But the ruling's gloss — *"the landform that separates the West Water from St Peters"* — does not
parse against the built world.** The West Water is the region **between** Nine Mile Creek and St
Peters (760 × 520 m; its east door goes to St Peters, its west door to Nine Mile Creek). No landform
separates them — the **bar** does, and the bar is the crossing, not a peninsula. **The reading this
document adopts** — the peninsula's south shore, north of St Peters Island, i.e. across open water
from the island's north side — matches every other clause the owner gave and matches the chart. It is
flagged rather than assumed: **§10 Q4**.

**Which water scene owns it, under that reading:** the marina sits off the **northern** edge of home
water. It is *not* in the built West Water region — a slip harbour there would put a destination
inside the first sail, which is the dory's sheltered water and should stay empty of them. It belongs
to **the mid-bay**, or to a new water scene north of it, and that call belongs to whoever builds the
mid-bay. **Region-level mark only; no layout is proposed here.**

---

## 7. What is M1, and what is capture-only

Stated plainly so no one builds ahead of phase (CLAUDE.md rule 8):

| Ruling | Phase | Why |
|---|---|---|
| **Route 91's name** | ⭐ **now** — one display string on a road that ships | zero risk |
| The town river + boardwalks | **M1, sequenced after the wharf / lifecycle / municipal branches land** | it re-cuts a region three branches are editing |
| The two crossings, the peninsula, the point, the small bay | **capture only** | north of the region edge; nothing is built there |
| The three rivers as *geography* | **capture only** | ditto |
| The three rivers as *fishery* | **M3** | the mussel/lease systems are M3 |
| The marina | **capture only** — a region-level mark | its water scene does not exist yet |
| Finnigan's Landing + its east river | **late** — glimpsed early, per the 08-07 ruling | unchanged |

---

## 8. Art asks — checked against what already ships

Per the standing rule that art asks are checked before they are made:

**⭐ Do NOT re-ask for these — they ship today:**

- **The boardwalk surface** — `RoadIso3_boardwalk_new_blob47.png`, a 0.35 m deck on piles, road kit v3.
- **Every other road surface** the trunk or a causeway could want — gravel, dirt, oiled dirt, asphalt,
  concrete, cobble, brick, sand, apron.
- **Nav marks** for the harbour mouth — the buoy kit ships and IALA-B marks compute from channel data.
- **The standable-deck contract** — `StandablePlatform`.

**Genuinely missing, and each is a real ask:**

| Ask | For | Notes |
|---|---|---|
| ⚠ **A bridge / span kit** | Route 91 over the town river (§5.5 #4), and later over the two large rivers | The only asset actually blocking the town river. Nothing in the repo draws a span |
| A causeway end / abutment treatment | wherever a crossing meets a bank | only if the owner rules causeway over bridge (§10 Q2) |
| Marina floating slips + finger docks | §6 | **not yet** — the marina has no scene; do not commission ahead of phase |

---

## 9. Names — candidates, not christenings

**Route 91 is ruled** (the real Rte 19, digits reversed). Everything else here is **unnamed**, and the
PEI-variant name slate is still owed to the owner. Candidates only, in the established house style —
real bay names, lightly varied:

| Thing | Candidates | Note |
|---|---|---|
| The **west-running river** | West River · Eliot River · Westmount River | the real one is the West (Eliot) |
| The **north-running river** | North River · Yorke River · Northam River | the real one is the North (Yorke) |
| The **east-running river** | Hillsborough River · East River · Hillsboro Water | the real one is the Hillsborough (East) |
| The **peninsula / point** | Rocky Point · Amherst Point · Warren Point | the real tip is Rocky Point (Fort Amherst) |
| The **small bay** behind the point | Warren Cove · Amherst Cove · Blockhouse Bay | |
| The **town river** at Nine Mile Creek | **Nine Mile Creek** — the creek that names the town | ⭐ **strongest candidate by far** |
| The **marina** | a local commercial name, per the yard convention | yards and marinas are businesses with local names, never generic labels |

⭐ **The town river should almost certainly just be Nine Mile Creek.** The settlement is named for a
creek the built region already has a mouth for. Naming it anything else would make the town's own
name a coincidence. **§10 Q5** — but this one barely needs asking.

---

## 10. Open questions for the owner

Short and batched, as asked. Q1 and Q3 are the ones that block work.

1. ⭐ **Does the town river fit the town, or does the town move?** §5.2 shows the valley needs ≥ 54 m
   between the lot rows and has 23 m. The recommendation (§5.3 A) is to **move the civic pair —
   harbourmaster and chandlery — south to y ≈ 95–100**, onto the junction they serve, opening a 54 m
   valley and improving the town on its own merits. Two lots move; nothing else does. **Yes, or one
   of options B/C/D?**
2. **Bridge, causeway, or ferry** at the two large river crossings? Recommendation: **bridge** at the
   west river, and hold the **north river** open as a possible ferry — the threshold to the city is
   the best place in the game for a scheduled crossing. A bridge kit is an art ask either way (§8).
3. **When may Nine Mile Creek be re-cut?** The wharf, lifecycle and municipal branches are all open
   against this geometry right now. The town river should land **after** them, and this document
   recommends it is not started until they merge.
4. **The marina's placement — confirm the reading.** *"The landform that separates the West Water
   from St Peters"* does not parse (§6.2); this document reads it as **the peninsula's south shore,
   north of St Peters Island**, which matches every other clause. Correct?
5. **Is the town river simply "Nine Mile Creek"?** (§9 — the town is already named for it.) And the
   three big rivers' names, whenever the name slate comes due.

---

## Appendix — amendments to `world-map-plan.md`

Made in the same PR as this document:

| Where | Amendment |
|---|---|
| §1 (the bay) | The north shore now names **Route 91**, the **peninsula/point**, the **harbour mouth** and the **three rivers** on the way to Finnigan's Landing |
| §2.2 (the three water scenes) | Notes that the **marina** is a mark on home water's northern edge — mid-bay or a new scene, not the West Water |
| §2.3 (the depth ladder) | Cross-references §3.3's **river reach ladder** as the tidal-fishery extension of the same emergent gate |
| §3 (the ports) | Finnigan's Landing gains its **east-running river**; the approach is the **point + small bay** |
| §5 (names) | Route 91 added as **RULED**; the rivers, the point, the cove and the marina added as **owed** |
| §7 (open questions) | The five questions above are cross-linked, not duplicated |

**Checked and *not* amended:** §2's home-water partition and §3's Market Port split were already
superseded and correctly folded in by the 2026-08-07 rewrite. Nothing stale was found there.
