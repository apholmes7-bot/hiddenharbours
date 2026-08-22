# Hidden Harbours — Scene Sizing & World Scale

> **Status:** **Sizes RATIFIED by the owner 2026-07-23** (§5.1 island scale · §5.1a the reef ring and
> its one dock · §5.2 the neap fix); **§5.1 island scale RE-RULED SMALLER 2026-07-30** (240 × 140 m —
> the built 450 × 260 island felt too large; region rectangle unchanged, the difference is open
> water). §6 is the ordered work. Remaining open questions are in §7. Subordinate to
> [`../vision-and-pillars.md`](../vision-and-pillars.md) (canon) and
> [`world-and-regions.md`](world-and-regions.md) (which owns *what* each region is; this doc only
> answers *how big*). Nothing here is built yet — no builder, scene or terrain asset is changed by
> this document.
>
> **What prompted it:** the owner's 2026-07-23 ask — scale St Peters up to something worth exploring,
> give the sandbar its own scene leaving the island's **west** end, and stop open-ocean regions from
> yanking the player through a scene load every minute now that some boats are large.

---

## 1. The unit is TIME, not metres

A region is the right size when **crossing it takes the right amount of time in the boat that region
is gated to** — not when it hits some metre count. A 600 m scene is a long haul in a rowed dory and a
brisk two minutes in a sport skiff. Sizing in metres and hoping is how you get an ocean that loads
every 50 seconds *and* an island you can walk across in eight.

So every number below is derived from two things the code already fixes: **how much world the camera
shows** for that boat, and **how fast that boat actually goes**.

### 1.1 What the camera shows (measured, not guessed)

The camera is data-driven per hull — `BoatHullDef.CameraWorldHeightMeters` — and pixel-perfect at
discrete steps, so visible world height is quantised (`CameraFollow`). Width is height × 16/9 at the
PC-first landscape target.

| Mode / hull | Visible height | Visible width |
|---|---|---|
| On deck | 6.75 m | 12.0 m |
| **On foot** | **9.0 m** | **16.0 m** |
| Fishing skiff | 13.5 m | 24.0 m |
| **Dory** | **14.0 m** | **24.9 m** |
| Punt | 17.0 m | 30.2 m |
| Console skiff | 18.5 m | 32.9 m |
| Sport skiff (twin) | 19.5 m | 34.7 m |
| Lobster boat | 23.0 m | 40.9 m |
| Cape Islander | 24.0 m | 42.7 m |
| **Side dragger** | **40.0 m** | **71.1 m** |

### 1.2 How fast things actually move

Walk **3.0 m/s**, sprint **5.5 m/s** (`PlayerWalkController`). Boat terminal speeds are *derived* from
the force model, not authored — the ladder is written out in `GreyboxBuilder`:

| Hull | Terminal speed | Screens crossed per minute |
|---|---|---|
| On foot (walk) | 3.00 m/s | 11.3 |
| Punt | 2.32 m/s | 4.6 |
| Fishing skiff | 2.50 m/s | 6.3 |
| Dory (rowed) | 3.00 m/s | 7.2 |
| Side dragger | ~3.48 m/s | 3.0 |
| Console skiff | 3.90 m/s | 7.1 |
| Cape Islander | 4.20 m/s | 5.9 |
| Sport skiff (twin) | 5.63 m/s | 9.7 |

> **⚠️ Flag for `gameplay-systems`, not resolved here.** The 25 m **side dragger is slower than the
> 12.9 m Cape Islander** (3.48 vs 4.20 m/s) and barely faster than a *rowed dory*. That may well be
> deliberate — a loaded dragger is not a fast boat — but it is the single number that decides how big
> an offshore region has to be, and P2's "from dory to dynasty" fantasy leans on bigger *feeling*
> like more reach. **§4's offshore sizes are computed at the dragger's current 3.48 m/s**, so if that
> speed changes, the offshore numbers move with it.

### 1.3 The rule

| Scene kind | Target time to cross | Why |
|---|---|---|
| **Foot region** (island, town) | **2–3 min** at walk | Long enough that exploring is a real activity; short enough that fetching a forgotten thing isn't a punishment. |
| **Inshore water** | **3–5 min** in the gating boat | You can see a whole region's shape in one outing. |
| **Offshore water** | **6–10 min** in the gating boat | The owner's actual ask: *don't pull the player out every minute.* At the dragger's speed this is the binding constraint on the whole world. |
| **Corridor** (sandbar, narrows) | see §5 — sized by the **tide window**, not by this table | A crossing whose length is set by drama, not by comfort. |

---

## 2. Where we are today

The three built scenes are all **160 × 120 m** — one sea plane, one 192 × 192 px painted height map
(`StPetersSeabed`: `_worldSize (160,120)`, `_minElevation −4`, `_maxElevation 6`). St Peters' island
is a **radius-22 m disc** — 44 m across, i.e. **under three on-foot screens**, walkable end to end in
15 seconds. The sandbar is a 56 m strip inside the same scene, running **east**.

That was the right size for a greybox that had to prove the tide gate. It is not a size anything in
the owner's brief fits inside.

---

## 3. What scaling up actually costs

Worth stating plainly, because the honest answer is **"much less than it sounds"**:

- **Water is a shader on a quad plus the displaced mesh** (ADR 0010/0023). A 1600 m sea costs what a
  160 m sea costs — extent is nearly free. The mesh's *tessellated* region is around the camera, not
  the scene.
- **The painted height map is a texture.** `PaintedSeabed`'s 192 × 192 R8 is 36 KB. A 1600 × 1600 m
  region at 1 px/m is 2.5 MB; St Peters at 2 px/m is ~1.6 MB. Not a budget problem.
- **Tiles are cheap and, in these kits, seamless by construction** — the shoreline/road noise phases on
  global tile coords, so a run never visibly repeats no matter how long the coast is.

The real costs are three, and they are all *authoring and quality*, not frame time:

1. **Hand-authoring 20× the area.** Mitigated by ADR 0002's handcrafted-macro / procedural-detail
   split and by the Terrain Paint Tool — the owner paints the shape, the systems dress it.
2. **The shoreline seam gets much longer.** Every metre of new coast is a metre the shader has to fade,
   foam and clip correctly (ADR 0012, `ShoreFadeMath`). This is the one place where "bigger" genuinely
   means "more chances to look wrong" — see §6.
3. **The paint tool is currently hard-coded to the greybox size** (`TerrainPaintTool`: `const int
   res = 192`, `worldSize = (160,120)`). That is a small, contained change and it blocks everything
   else here.

---

## 4. Proposed size per region

Regions and their gating boats come from [`world-and-regions.md`](world-and-regions.md) §5–6.
"Screens" is the scene's long axis in visible-widths for the mode named.

| Region | Scene extent | Gating mode | Cross long axis | Screens | Note |
|---|---|---|---|---|---|
| **St Peters Island** | **760 × 520 m** | on foot | **~2:30** walk / 1:22 sprint | 47 | §5. Island landmass ~240 × 140 m inside it (re-ruled smaller 2026-07-30 — the crossing, not the island, is the region's long walk). |
| **St Peters Bar** *(built: 305 m)* | ~~proposed 640 × 200 m~~ **not a scene** | on foot | **1:42** walk / 0:55 sprint each way | 40 | §5.2. **SUPERSEDED 2026-08-06:** the bar is not its own scene — it is **split across the St Peters ↔ Nine Mile Creek seam**, 305 m in each region, total **610 m**. See [`nine-mile-creek-mainland.md`](nine-mile-creek-mainland.md) §3.2. |
| **Nine Mile Creek** *(the MAINLAND: wharf + town)* | ~~600 × 400 m~~ **760 × 560 m** | on foot | **3:06** N–S · 4:13 E–W | 47 | **RE-SIZED 2026-08-06** — the region is the mainland now, not an island, and it carries **its half of the crossing** as well as the wharf and the town. The long axis is a CORRIDOR (the crossing, sized by the tide window per §1.3); the short axis is the foot region. 760 is St Peters' own width, deliberately: same water, same scale, same 2 px/m seabed. [`nine-mile-creek-mainland.md`](nine-mile-creek-mainland.md) §3.1. |
| **Coddle Cove** | 520 × 400 m | dory | ~2:53 | 21 | Home water: small, sheltered, legible in one look. |
| **The Sunkers** | 700 × 700 m | punt/skiff | ~5:02 punt | 23 | A reef field needs room to pick a line through it. |
| **The Drownded Lands** | 900 × 700 m | skiff + tide | ~3:51 console | 27 | Big flats are the whole point; most of it is walkable at low water. |
| **Fundy Rips** | 900 × 460 m | Cape Islander | ~3:34 | 21 | A corridor — you fight *across* it, not around it. |
| **The Banks** | **1600 × 1600 m** | side dragger | **~7:40** | 22 | The binding case (§1.2). |
| **Ironbound** | 1600 × 1600 m | dragger+ | ~7:40 | 22 | |
| **The Smother** | 1400 × 1400 m | late instruments | ~6:42 | 20 | Fog cuts sightlines, so it plays bigger than it measures. |
| **The Shipping Lanes** | *not a bounded scene* | freighter | — | — | A lane network, not a rectangle — needs its own model, out of scope here. |

**The pattern to notice:** every region lands at **20–27 screens** across regardless of tier. That is
not a coincidence I engineered in — it is what falls out of "6–10 minutes offshore, 2–3 minutes on
foot" once the camera and speed tables are honest. It is a good sign, and it gives a one-line sanity
check for any future region: **~20–25 screens across, and time it in the boat that gets you there.**

---

## 5. St Peters, in detail

### 5.1 The island scene

**Scene 760 × 520 m; island landmass ~240 × 140 m, sitting EAST of centre so the bar exits WEST.**

> **⚠ RE-RULED SMALLER (owner, 2026-07-30).** The first ruled size was **450 × 260 m** (~1:5 linear
> compression of the real island) and it was built at that size — and once it stood, **the island
> felt too large**. The owner ruled it down to **roughly 1/3–1/4 of that area** (≈ half the linear
> span → **240 × 140 m**, area ratio ~29%), with the surrounding **sea/region rectangle unchanged at
> 760 × 520 m** — the difference is **more open water**, which is the point of the change: St Peters
> is a small home island in a big tide, and the water, the bar and the crossing are the region's
> subject, not the meadow. Everything below reflects the re-ruled size; where an old number is worth
> keeping for the record it is marked as the 2026-07-23 era's.

The real St Peter's Island is about 400 acres, roughly 2.4 km × 1.1 km; the shipped island is a
**~1:10 linear compression** — a ~1:20 walk along its length, a perimeter of roughly 620 m (about
3–4 minutes to sail round in the dory), and the whole landmass the size of a low-tide walk. The
region's LONG walk is the **~305 m sandbar** (grown from 200 m by the shrink: the west shore
retreated east while the Nine Mile Creek passage stayed at the region edge), which is exactly where
the length belongs — the crossing is the lesson, the island is home.

Density check: the same ~12–15 points of interest now sit over ~26,000 m² — one every ~45 m, **about
2–3 on-foot screens between things**. Denser than the 450 m island's spacing, and deliberately so:
a village of five buildings plus a cottage on a 240 m island reads as one small place rather than a
hamlet strung along a road.

Mapping the owner's brief onto that footprint (placement is `world-content`'s call; this is the
inventory the size has to hold):

| From the brief | In the scene | Kit that already exists |
|---|---|---|
| **Four farmsteads** | 4 homesteads, reverting — the "20 families" reduced to what one island really held | `houseIsoRig` |
| **A one-room school** | The teaching beat of the opening | `houseIsoRig` / `interiorIsoRig` |
| **A fish stage, later a lobster factory** | The working relic; the cannery is *optional* per the owner | `wharfKitRig`, `wharfBuildingRig` |
| **A lighthouse, decommissioned** | The landmark you can see from the water — the island's silhouette | `lighthouseIso` |
| **Red sandstone cliffs + sea caves** | The south/east weather coast | **`ShoreIsoCliff` `cap/mid/toe` + `caveToe`** (just imported) |
| **Beaches, sandbar, clam flats** | The intertidal west/north — the dig ground | **`ShoreIsoGround` sand/ripple/shingle** |
| **Marsh, meadows, wild roses/raspberries** | The reverting interior | `ShoreIsoGround` grass/marram + the flower & grass kits |
| **Forest** | Interior cover, hiding ruins | The tree pack |
| **Reefs make landing hard for all but shallow draft** | **This is a gameplay rule, not decoration** — see below | **`ShoreIsoSprites` sea stacks + boulders** |
| **Freshwater springs, ruined tractors/homesteads** | Beachcombing/POI dressing | — |
| **Rabbits, seals, birds** | Ambient life | `foxRig` pattern |

**The sandbar leaves the WEST end.** This flips the original greybox, where the island sat at x = −40
and the bar ran *east* to x = +34. The island moves east of centre and the bar exits west.
**✅ DONE 2026-07-29** at the 450 × 260 size; **re-sized 2026-07-30** to the shrink ruling — island
centre `(70, 0)` with semi-axes **120 × 70** (beach falloff 30 → 20 m, keeping the beach:island
proportion and the readable grass/marram/sand/ripple shore ladder), bar `(−45, 0) → (−350, 0)`, dock
cluster re-derived on the new east shore (mooring/dock zone `(215, 0)`, disembark `(213, 0)`, arrival
`(217, 0)`, pier cells 183–213 — the mooring sits at the same *profile point* as before, beach toe
+5 m onto the shelf, so the measured −1.05 m bed and the whole §5.1a gate arithmetic carry over
unchanged). ⚠ The island stays an **ellipse**: 240 × 140 is not a disc at any radius — the shape
argument holds even though a 240 m disc would now physically fit the scene.

### 5.1a The reef ring and the one dock — ✅ RATIFIED (owner, 2026-07-23)

> *"There is one dock on the far end of the island opposite the sandbar. It's modest, but can take
> powerboats there."*

**The reef ring is in, with one door in it.** Since the bar exits west, the dock sits on the **east
end** — the far side from the crossing, which is also the right side dramatically: you walk out the
west and you come home under power to the east.

This costs no new systems. Draught is already real data and the painted seabed already decides depth
per tide, so the whole thing is authored terrain:

| Hull | Draught | Can it use the dock? |
|---|---|---|
| Dory (rowed) | 0.30 m | ✅ |
| Fishing skiff | 0.35 m | ✅ |
| Punt / punt upgraded | 0.50 / 0.55 m | ✅ |
| Sport skiff / twin | 0.50 / 0.55 m | ✅ |
| Console skiff | 0.55 m | ✅ |
| **Lobster boat** | **1.30 m** | ❌ except near high water |
| **Cape Islander** | **1.40 m** | ❌ except near high water |
| **Side dragger** | **2.90 m** | ❌ never |

**The cut lands exactly where the boat ladder does.** Every skiff- and punt-tier hull is **≤ 0.6 m**;
the first two *working* hulls are **1.3–1.4 m**; the dragger is **2.9 m**. So "modest, but takes
powerboats" is not a vague phrase — it is a **0.6 m gate**, and it separates the tier you learn on
from the tier you graduate to. **The island you start on becomes the island your big boat can never
come home to.** That is P2 and P5 in one piece of geography.

**Authoring numbers** (St Peters swings ±3.5 m at spring, ±1.575 m at neap, about mean 0; a hull
floats where `waterLevel − bedElevation > draught`):

- **Dock approach / berth bed ≈ −1.0 m.** Clears 0.6 m draught whenever the water is above −0.4 m —
  most of the cycle — and dries near spring low, so the dock has its own gentle tide gate rather than
  being a permanent open door. Deliberate: even coming home under power should mean reading the tide.
- **Reef shelf ≈ −1.0 to −1.5 m** around the rest of the coast, shallowing to the beaches. A 1.4 m
  Cape Islander needs water above −0.1 m to cross a −1.5 m shelf, i.e. **roughly the top half of the
  tide** — "difficult", per the brief, not impossible. The 2.9 m dragger never crosses at all.
- Keep a couple of the `ShoreIsoSprites` sea stacks *on* the shelf as the visible tell. The reef
  should be legible from the deck before it is legible from the depth sounder.

> **One thing to watch when this is authored:** the lobster boat and Cape Islander land in a
> *sometimes* band, not a *never* band. That is the more interesting answer — but it means the
> "you've outgrown home" beat arrives as a nagging tide constraint rather than a clean door closing.
> If it should land harder, raise the shelf rather than lowering the boats.
>
> **⚠ MEASURED, 2026-07-29 — and it is a *sometimes* band, wider than the table above suggests.**
> Against a −1.0 m berth the fleet lands like this, as a fraction of the semidiurnal cycle afloat:
> dory **56.9%**, punt/skiff tier **54.6–56.4%**, **lobster boat 47.7%**, **Cape Islander 46.8%**. So the working hulls are not *"❌ except near high water"* as the table puts it —
> they are gated for a little under half the cycle, which is a nagging constraint rather than a door
> closing. The ordering is right (every skiff beats every working hull) and the ruled numbers are
> implemented exactly; **whether it should land harder is the owner's call**, and this section already
> says which lever to pull. Deferred until he plays at size.

#### ⭐⭐ OVERRULED for the east dock — the arrival berth is always wet (owner, 2026-08-19)

> *"The St Peters EAST dock always has water, even at spring low — cut a small cove or channel if
> the bed needs it. It is the game's front door: at new game the player is piloted in by a SKIPPER
> driving a cape islander, who approaches the dock, slows, throws his lines, ties up — and the
> player steps off."*

This **reverses the "gentle tide gate" above for this one berth**, and gives the reason: an arrival
that grounds on its own doorstep at dead low water is not an opening. Everything else in §5.1a
stands — the reef ring, the draught ladder, the one-door reading, and the beach slip's own gate.

**What was built** (`StPetersBuilder.Approach*`, `TidalTerrain.Carve`):

- A **dredged channel and berth pocket** on the *same line* as the slip, from the −4 m contour
  (`ApproachFrom` = 255, 0) in to the wharf head (`ApproachTo` = 206, 0), at the **same ±8 m
  half-width** — so the reef still has exactly one door, now dredged.
- Bed **−4.00 m**, and it is the arithmetic rather than a taste:
  `springLow (−2.20) − (cape islander 1.40 + clearance 0.40)`. The **0.40 m clearance is not
  invented** — it is exactly what this region's own −4 m harbour floor gives that hull at that tide,
  so the dredge cuts the slip down to the floor and stops. The channel offers the water the approach
  offers and no more.
- A **flat bottom** (`ApproachThalwegHalfWidth` 4 m), because a falloff curve cannot state a width:
  carving to −4 m with the old single-slope trough left 3.7 m of usable water against a **4.80 m
  beam**. The flat states the navigable width; the shoulders still narrow it as the tide drops —
  **8.00 m at spring low → 11.80 at neap low → 39 at mean** — which is the owner's *"shrinks in
  width at low tide but stays navigable"*.

**What did NOT change.** The reef apron (−1.0/−1.5) still bares. The flats still bare. The bar guts
still bare. **The beach slip still dries** — wading out to it still means reading the tide — so the
gate moved to the beach rather than evaporating. The draught table above is unchanged for every
hull *crossing the ring*; what changed is that the one door is dredged. The pier root's measured
+5.354 m and every other authored point are **bit-identical** (`StPetersEastBerthTests`).

**Consequences already banked:** the entrance channel now claims 1.40 m at *spring low* instead of
0.6 m at mean and is buoyed in to the dock rather than stopped at the drop-off; a **floating dock**
becomes possible at this wharf (the "it would sit on the mud" reason expired — it is now only a
question of the bob-frame driver and the owner's taste).

#### ⭐⭐ …and she lies ALONGSIDE it, which took a berth pocket (owner playtest, 2026-08-22)

The owner watched the finished opening and reported that the boat *"finishes with its bow sitting on
the planks."* That was authored, not a pilot bug: the berth was a point on the pier's own centre-line
one metre off its head, so a 12.9 m hull lying on the channel's axis reached x = 208.6 — **5.4 m
inside a deck that runs 183 → 214.** She was doing exactly what the region asked.

**She now lies alongside the wharf's south face**, and every number is derived rather than typed
(`StPetersBuilder.Alongside*`, `StPetersWharf.MooringFaceY` / `AxisInward` / `LadderPosition`):

- **Which face:** the **south** one — because that is where the gear is. Every fitting the pier
  carries (bollards, tyre fenders, ladder, cleat) is on the south edge, since that is the side the
  camera sees and the side whose tall face the kit draws. You berth where the bollards are.
- **How far off it:** her own half-beam (2.40 m) plus a **0.40 m fendering gap** — one of the pier's
  own tyres squashed. Measured rail-to-face, not centre-to-face.
- **Where along it:** **abreast of the ladder**, the one fitting whose whole job is getting a person
  between a boat and the planks.
- **Which way she lies:** along the **pier's** axis, not the channel's. The two agree today (the pier
  was built down the channel's line) and a test holds them together — but a hull made fast to
  something lies parallel to *that*, and reading the channel would lay her across her own berth the
  day it was re-cut at an angle.
- **The step ashore** is the cove's ratified 1.5 m, measured from her **gunwale**. The old 1.5 m was
  measured from the berth's centre-line, which on a 2.4 m half-beam is a point inside the boat.

**⚠ It needed a dredge as well as a coordinate, and that is the part worth remembering.** The
approach's flat bottom is ±4 m about y = 0 and the pier is ±3 m — **one metre of dredged water beside
each face**, against a hull that needs 4.8 m of beam there. Measured on the terrain as it stood, the
ground under an alongside berth read **−1.94 m at y = −7 against −2.20 m of water at spring low**:
she took the ground, on *either* face. Alongside was never reachable by moving a point.

**What was built:** a third cut, `TidalTerrain`'s **berth pocket** — the same only-ever-downward
`Carve` as the slip and the approach, dredged to the **same −4.00 m bed as the channel that feeds
it**, with its centre-line **exactly her footprint at the berth** (bow to stern) and a flat bottom of
her half-beam + 1 m of steering room.

**Why a pocket and not a wider channel.** Widening the approach's flat from ±4 m to the ±8.2 m an
alongside hull needs would also have floated her — and would have **widened the door**, which is this
region's draught gate. That is a ruling, not a side effect of an arrival fix. A pocket is local: the
channel's *narrowest* section, which is what actually gates the fleet, is somewhere else entirely.
**Measured before and after, the ladder is unchanged** — dory/punt/skiff/lobster boat/cape islander in
at every tide, side dragger everything but spring low, stern trawler at the two highs, coastal packet
at spring high only, **tanker never**.

**What did NOT change.** Every probe the dredge test pins is bit-identical: the pier root's +5.35 m
dry ground and the deck height measured off it, the +1.09 m shoal a metre off each deck lip, the
beach slip, the reef ring, the bar, the dory's mooring. The pocket's shoulder is bounded from above
by the nearest of them — the deck's south lip, 6.79 m from the pocket's nearest end — and that
clearance is asserted rather than trusted.

**⚠ The paint was re-baked with it** (`StPetersSeabed_HeightTex.png`, 575 texels). ADR 0014 is
*paint = sail*: a berth that exists only in the analytic terrain is a shoal in the water the player
actually sees. Everything outside the pocket is byte-identical to the committed map.

**Still open (owner):** whether the channel reads as a channel at low tide, whether 8 m of navigable
water is the right room coming in, and whether a wet berth beside a dry landing reads or looks like
a bug.

### 5.2 The sandbar as its own scene — sized by the tide, and the tide is generous

The owner asked for the bar to be a whole scene. It should be. But its length is not a comfort
decision — **it is set by the tide window**, so here is that window computed from the live config
rather than estimated (`GameConfig`: `SecondsPerDay 1800`, `TidalPeriodHours 12.4206`,
`NeapAmplitudeFraction 0.45`; `StPetersBuilder`: amplitude 2.2 m about mean 0, crest 0.88 m — **both
re-ruled 2026-08-01, see the pacing box below**):

- One game hour = **75 real seconds**, so a full tide cycle = 12.42 × 75 = **15 min 32 s real**.
- At **spring**, the bar is dry whenever the water is under its 0.88 m crest — i.e. `sin θ < 0.4`,
  which is **63.1% of the cycle**. **Exposed ≈ 9 min 48 s; flooded ≈ 5 min 44 s.**
- At **neap**, amplitude falls to 0.45 × 2.2 = **0.99 m — still above the 0.88 m crest**, so the bar
  floods every day of the month. **Exposed ≈ 13 min 10 s; flooded ≈ 2 min 21 s.**

The window is a function of `crest / amplitude` and nothing else (the water is a sinusoid, so the bar
is dry while `sin θ < crest/amplitude`). **In GAME time it has not changed since the ratified box
below** — 7 h 50 m of the 12 h 25 m cycle at spring. What changed is how long that takes to live
through, which is the day-length lever, not the tide.

> ### ⚠️ The crossing's teeth are slack, and this is where to fix it
>
> **The bar has been comfortably walkable for a while, and the pacing ruling widened the margin.**
> The 600 m recommendation below was never built: today's bar is `SandbarFrom (−45,0)` →
> `SandbarTo (−350,0)` = **305 m**, which at the 3 m/s walk is **1:42 each way, 3:23 both ways**.
>
> | | Spring window | Walk both ways | Margin |
> |---|---|---|---|
> | 600 m bar, pre-ruling | 6:32 | 6:40 | **−0:08 — you must sprint** |
> | 305 m bar, pre-ruling | 6:32 | 3:23 | +3:09 |
> | **305 m bar, as shipped** | **9:48** | **3:23** | **+6:25 — you can dawdle twice over** |
>
> So "you are stranded if you stroll" stopped being true when the bar was shortened, and the pacing
> ruling roughly doubled the slack again. **Nothing here is a bug** — the tide gate still exists at
> every point in the lunar month, which is the property #280 was about — but the *teeth* canon asks
> for are currently blunt.
>
> **The lever is the bar's length, not the tide.** A ~900 m bar restores the original feel against the
> new window (900 m = 10:00 both ways vs a 9:48 window — you must sprint the return, exactly as the
> 600 m/6:32 pairing did). That is a terrain change and an owner call, so it is recorded here rather
> than taken. Do **not** reach for the tide constants to fix it: he has just
> ruled on those, and the window is one number while the bar is a scene's worth of terrain.
>
> **⭐ PARTLY ANSWERED 2026-08-06.** The mainland now supplies the bar's other half, so the crossing is
> **610 m** — 305 m each side of the seam, and within 10 m of the 600 m this section originally
> recommended, by coincidence rather than design. Round trip **6:47** against the 9:48 window:
> **+3:01**, sharper than the 305 m bar's +6:25 but still not the "sprint the return" the 900 m option
> buys. Going further is now a REGION-WIDTH decision as well as a terrain one — 900 m total needs a
> ~1060 m-wide Nine Mile Creek. Full table in
> [`nine-mile-creek-mainland.md`](nine-mile-creek-mainland.md) §3.3; still an owner call.

> **An earlier draft of this section sized the bar at 400 m on an estimated ~4-minute window.** The
> real window is 65% longer, which is the difference between a bar you can always walk back across and
> one you have to judge. Worth recording as a reminder that this scene's length and the tide constants
> are the same decision wearing two hats.

The scene is a corridor: bar crest ~30–50 m of walkable width, narrowing as the tide falls and rises,
with the deeper **channel** cut across it (boat-crossable at higher water) — the same
inverse-over-the-tide relationship the greybox already models in `StPetersBuilder`'s
`SandbarCrestElevation` / `ChannelBedElevation`.

> **✅ RATIFIED (owner, 2026-07-23) — the neap gap is fixed: the crest must clear neap high water.**
> **✅ APPLIED IN CODE 2026-07-29 · RE-SCALED 2026-08-01.** `StPetersBuilder.SandbarCrestElevation`
> reads `0.88f` (it was 1.4 against the old ±3.5 m swing — the ratio 0.4 is what actually carries the
> ruling forward). Three EditMode tests hold it: one walks a whole lunar month of the real `TideModel`
> and asserts the crest clears the *weakest* high water it produces (derived from `GameConfig`, so
> re-tuning the neap fraction or the amplitude re-checks the gate); one is the measured sabotage,
> re-aimed at **1.4** — the crest you get by moving the amplitude and forgetting the crest rides on it;
> and `TidePacingInvariantTests` pins the window's in-game length and the peak rate.
>
> Any crest at or above neap high water means that for part of every lunar month the bar never floods
> and the prologue's one tide gate silently switches itself off. Putting the crest below it means
> **the island is cut off twice a day, every day of the month** — the lesson always holds, and the
> tide is never something you can ignore.
>
> **What the change costs, computed both ways** (as shipped: amplitude 2.2 m, 75 s per game hour, so
> the cycle is 15:32 real):
>
> | Crest | crest/amp | Spring: exposed / flooded | Neap: exposed / flooded |
> |---|---|---|---|
> | 1.4 m (un-scaled — the trap) | 0.64 | **11:10** / 4:21 | **15:32 / 0:00 — never floods** |
> | **0.88 m (ratified · shipped)** | **0.40** | **9:48** / 5:44 | **13:10** / 2:21 |
>
> **The gate exists at every point in the month**, which is the whole purpose.
>
> **And the mechanic keeps the gradient nobody designed but everybody wants:** neap is the *forgiving*
> end (13:10 of dry bar, and the flood only lasts 2:21, so being caught costs you a short wait), spring
> is the tense one (9:48, and being caught costs a real 5:44). The sea is kinder some weeks than
> others, and it is kinder in a way the player can learn to read. Those are the same fractions of the
> cycle as before the pacing ruling — only the clock they are read on is slower.

If the crossing later wants to feel longer or shorter, **move the bar, not the tide constants** — the
owner has ruled on the tide twice now, and the ratio that sets the window is load-bearing for the
neap gate as well. (This line used to say the opposite. It was written when the tide was nobody's
decision in particular; it is now the owner's, twice.)

### 5.3 Waves crashing into the island

The owner asked how water meets the coast once the island is big. The mechanism already exists and
does not need replacing:

- The **shader owns the waterline** (ADR 0010/0012/0023): it clips at the live depth-0 tide contour,
  rides foam and swash on that line, and pins the displaced surface to it (`ShoreFadeMath`).
- The **newly imported shoreline kit bakes zero water on purpose**, and every ground material is drawn
  to read right dry *and* submerged — so a whole flat can be swept by the tide as one painted surface.
  Land butts straight against shader water with nothing to line up.

So "waves crash into the island" is **already the design**; scaling up doesn't change the mechanism,
it changes the *amount of it on screen at once*. Two consequences worth naming:

1. **Perimeter grows ~11×** (from a 44 m disc to a ~1.1 km coastline). Every shore-seam artefact gets
   eleven times more chances to be visible. The open shoreline defects from the 2026-07-23 playtest —
   the all-white sea and the swirly shoreline — should be **closed before** the island is scaled, not
   after, or it will be impossible to tell a new bug from an old one at eleven times the surface area.
2. **Cliff coast and flat coast want different water.** A red-sandstone cliff toe should take a wave
   as impact and spray; a clam flat should take it as a long silent sweep. The kit already draws the
   distinction in *land* (`cliff toe` vs `sand`/`ripple`); whether the water reads the difference is an
   open question for the water lane, and a good one to answer while the coast is being authored.

---

## 6. What has to change before any of this can be built

Ordered, with the blocker first. None of it is done in this document.

1. ~~**Un-hard-code the terrain paint tool.**~~ **✅ DONE 2026-07-29.** The tool holds no size of its
   own: it takes the extent from a `RegionDef` and derives the texel grid as **size × pixels-per-metre**
   on each axis. **2 px/m** is the shipped inshore figure; **1 px/m** offshore. *(Note the old
   `res = 192` was SQUARE over a 160 × 120 m rect — 1.2 px/m across and 1.6 px/m up, two densities
   nobody chose. A derived grid is isotropic by construction.)* `StPetersSeabed` still needs re-baking
   when the extent actually grows — the tool will now do it at the right shape.
2. ~~**Sea plane / region extent becomes data, not a literal.**~~ **✅ DONE 2026-07-29.**
   `RegionDef.WorldCenter` / `WorldSizeMeters` / `SeabedPixelsPerMetre` sit next to the tide fields;
   `StPetersBuilder` authors them once and publishes them, and the tiled sea sprite, the flat backdrop,
   the shader's height bake and the displaced mesh all read them. Six copies of `(160, 120)` became
   one. **The camera bounds (item 4) can now read the same number.**
3. ~~**Close the open shoreline defects first** (§5.3, note 1).~~ **✅ VERIFIED CLOSED 2026-07-29.**
   Both defects named in §5.3 — the all-white sea and the swirly shoreline — were fixed and merged the
   same week this document was written, in **#279** (`8623789`), which this section predates. Checked
   against current `main` rather than taken on trust: all four root-cause fixes are live in
   `HiddenHarboursWater.shader` (`_PaletteFloorKnee`, `_CloudMoonlitVis`, the envelope bands ×
   `ShoreFade01`, and the swash/fringe × `SeabedSlopeMag`), and **no material overrides any of them
   back to its legacy value** — the two knobs are not written in `Water.mat` at all, so they run on
   the shader defaults, which are the *fixed* values (0.45 / 0.35) and not the legacy reverts (0 / 1).
   ⚠ Still outstanding is an **owner verdict**, not a defect: #279's fix makes dusk and night seas
   genuinely darker (the brightness *was* the bug), and that has not been played yet.
4. ~~**Camera bounds.**~~ **✅ DONE 2026-07-29.** `CameraBounds` (a pure clamp, tested headless) plus
   `CameraFollow`'s `LateUpdate`. The rectangle is the region's authored extent — item 2's
   `RegionDef.WorldCenter`/`WorldSizeMeters`, propagated by each region builder to its `RegionAnchor`
   exactly as it is propagated to the sea, the backdrop and the height bake. **No second bounds source.**
   - The clamp holds the camera back by its **half-extents at the current zoom**, so the view's edge
     lands on the map edge rather than the camera's centre — and it is automatically right across the
     discrete pixel-perfect tiers and *during* a zoom tween, because it knows nothing about tiers.
   - It runs **last** in `LateUpdate`, after the zoom settles; clamping first would leave a one-frame
     overshoot past the edge on every zoom step.
   - A region **narrower than the view centres** instead of jittering between edges (the naive
     `Clamp(x, min+half, max−half)` has inverted bounds there and snaps to an arbitrary edge).
   - ⚠️ The bounds are **not serialized on the camera**: it lives on the persistent core and outlives
     every region, so a baked rectangle would be the start region's forever. They arrive through the
     region seam (`GameServices.CurrentRegionBounds`, published by `RegionAnchor.OnEnable`, which
     covers boot as well as a hop). Unpublished = unclamped, so nothing changes until a region reports.
   - *(HUD/letterboxing for a region smaller than the view remains `ui-ux`'s call — logged, not built.)*
5. **Then, and only then, author.** Island → bar → the rest, in that order. **⏳ STARTED
   2026-07-29:** the region now *is* 760 × 520 with the island east of centre and the bar leaving the
   west end (§5.1), as greybox analytic terrain — built first at the ruled 450 × 260, then **re-sized
   2026-07-30 to the re-ruled 240 × 140** (the committed `StPetersSeabed` bake of the 450 m coast was
   deleted with that change; the builder re-bakes a fresh one on the next Build St Peters Scene).

~~**One change is ready to make now and depends on none of the above:**
`StPetersBuilder.SandbarCrestElevation` **1.6f → 1.4f** (§5.2, ratified).~~ **✅ DONE 2026-07-29** — it
landed ahead of everything else, as it should have: the old value meant the tide gate was off for part
of every month in whatever anyone playtested next. Now guarded by the neap-gap tests in
`StPetersTerrainTests`.

**Also worth doing while the coast is authored, but not blocking:** stand up ISO ground/fringe
rule-tiles and the road blob-47 autotiler from the kits imported today. They are sliced and catalogued
(`ShorelineIsoCatalog`) but nothing paints with them yet.

---

## 7. Ruled, and still open

### ✅ Ruled by the owner, 2026-07-23 (island scale RE-RULED 2026-07-30)

1. **Island scale — ~240 × 140 m** within the unchanged 760 × 520 m region. §5.1. *(Originally ruled
   ~450 × 260 on 2026-07-23 and built at that size; **re-ruled smaller 2026-07-30** — the built island
   felt too large, and the owner wants roughly 1/3–1/4 of the area with the freed space as open
   water. The sea rectangle, the tide profile and the reef/berth depths did not move.)*
2. **The neap gap — fix it**, crest 1.6 → 1.4 m, so the island is cut off twice a day every day of the
   month. §5.2. *(Bonus: the gate gains a spring-tense / neap-forgiving gradient, at no cost.)*
3. **The reef ring — in, with one modest dock on the east end**, opposite the sandbar, taking
   powerboats. §5.1a. *(Lands as a 0.6 m draught gate: skiff/punt tier home, working hulls tide-gated,
   dragger never.)* **✅ BUILT 2026-07-29** — a 25 m shelf from −1.0 m at the beach toe to −1.5 m at
   the drop-off, ringing the island, with a −1.0 m berth channel cutting the one door through it on
   the east end. The ring has **two** crossings by design: the berth (boats, east) and the sandbar
   riding over it (feet, west) — the two halves of the opening arc. ⚠ Two numbers were NOT in the
   ruling and are mine: the shelf's **25 m width** and the berth's **8 m half-width**. At the original
   450 m island the width was what the scene had room for, exactly (380 − 70 = 310 m, less 225 of
   island, 30 of beach and 30 of drop-off); the 2026-07-30 shrink frees ~100 m of sea room but the
   shelf keeps its 25 m — it is a depth gate, not decoration, and widening it would eat the open
   water the shrink was ruled to create. See the note in §5.1a on where the working hulls actually
   landed (the −1.05 m mooring bed carries over unchanged, so the measured percentages hold).

### Still open

4. **Cannery: in or out?** The brief says a fish stage that later became a lobster factory, and that a
   cannery "is not needed". A working relic is cheaper and reads better than a building with no job.
5. **Does the sandbar support the return trip at all,** or is it deliberately one-way on the first
   crossing (canon §6.0 has the first trip on foot, then you sail home)? §5.2 sizes it so a spring-low
   round trip is *possible but tight*; a one-way-only bar could be longer and more dramatic.
6. **The dragger's speed** (§1.2) — is a 25 m dragger being slower than a rowed dory intended? It sets
   the size of every offshore region.
7. **Does the water read cliff coast differently from flat coast?** (§5.3, note 2) — a sandstone toe
   should take a wave as impact and spray; a clam flat as a long silent sweep. Best answered while the
   coast is being authored, by the water lane.
