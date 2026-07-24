# Hidden Harbours — Scene Sizing & World Scale

> **Status:** PROPOSAL, awaiting the owner's call on §4 and §5. Subordinate to
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
| **St Peters Island** | **760 × 520 m** | on foot | **~2:30** walk / 1:22 sprint | 47 | §5. Island landmass ~450 × 260 m inside it. |
| **St Peters Bar** *(new)* | **400 × 180 m** | on foot | **~2:13** walk / 1:13 sprint | 25 | §5.2. Sized by the tide window, not by comfort. |
| **Port Greywick** | 420 × 320 m | on foot | ~2:20 | 26 | A town is a foot region with a harbour edge. |
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

**Scene 760 × 520 m; island landmass ~450 × 260 m, sitting EAST of centre so the bar exits WEST.**

The real St Peter's Island is about 400 acres, roughly 2.4 km × 1.1 km. Full scale is a 13-minute
walk one way and would be mostly empty. **~1:5 linear compression** puts it at 450 × 260 m: a
2½-minute walk along its length, a perimeter of roughly 1.1 km (about 6 minutes to sail round in the
dory), and room for every landmark in the owner's brief without any of them touching.

Density check: ~12–15 points of interest over 117,000 m² is one every ~90 m — **about 5 on-foot
screens between things**, or 30 seconds of walking. Close enough to keep exploring, far enough that
arriving somewhere feels like arriving.

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

> **"The extensive reefs make landing difficult for all but shallow draft boats" is the single best
> line in the owner's brief**, because it is already implementable with what exists. Draught is real
> data (`BoatHullDef.DraughtMeters` — dory 0.3 m, dragger 2.9 m) and the painted seabed already
> decides depth per tide. Ringing St Peters in a shallow reef shelf means **the island you start on is
> the island your big boat can never come home to** — P2 and P5 in one geographic fact, costing no new
> systems. Worth ratifying deliberately rather than letting it happen by accident.

**The sandbar leaves the WEST end.** This flips today's greybox, where the island sits at x = −40 and
the bar runs *east* to x = +34. The island moves east of centre and the bar exits west.

### 5.2 The sandbar as its own scene — and why it's only 400 m

The owner asked for the bar to be a whole scene. It should be. But its length is not a comfort
decision — **it is set by the tide**, and the arithmetic is unforgiving:

- The clock runs **1200 s per game day**, so one game hour = 50 real seconds.
- A semi-diurnal tide cycle (~12.4 h) is therefore **~10.3 real minutes**.
- The bar is exposed for roughly 40% of that: **a window of about 4 real minutes.**

A 600 m bar is a 3:20 walk each way — you could go, but you could never come back, and "cut off until
the next low water" stops being a lesson and becomes the only outcome. At **400 m** the walk is
**~2:13 each way**, so a prompt round trip fits inside one window and dawdling strands you. That is
exactly the teeth canon asks for: *lost time, never worse* (P5 at its kindest).

The scene is a **corridor, 400 × 180 m**: bar crest ~30–50 m of walkable width, narrowing as the tide
falls and rises, with the deeper **channel** cut across it (boat-crossable at higher water — the same
inverse-over-the-tide relationship the greybox already models in `StPetersBuilder`'s
`SandbarCrestElevation` / `ChannelBedElevation`).

If the crossing later wants to feel longer, **lengthen the tide window before lengthening the bar** —
the window is one number, the bar is a scene's worth of terrain.

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

1. **Un-hard-code the terrain paint tool.** `TerrainPaintTool` fixes `res = 192` and
   `worldSize = (160,120)`. It needs to take world size and a **pixels-per-metre** setting instead, and
   `StPetersSeabed` needs re-baking at the new extent. Recommend **2 px/m for St Peters** (crisp enough
   for coves and cave mouths; 1520 × 1040 R8 ≈ 1.6 MB) and **1 px/m offshore**. *(`tools-editor`)*
2. **Sea plane / region extent becomes data, not a literal.** All three builders hard-code
   `localScale = (160, 120)`. Region extent belongs on `RegionDef` next to the tide fields, so a
   region's size is authored once and read by the sea, the terrain and the camera bounds alike.
   *(`world-content` + `lead-architect` for the Core seam)*
3. **Close the open shoreline defects first** (§5.3, note 1).
4. **Camera bounds.** There is no bounds rig yet (`CameraFollow`'s comment says so). At 160 m nobody
   noticed; at 760 m the camera will sail off the painted map. *(`ui-ux`/`gameplay-systems`)*
5. **Then, and only then, author.** Island → bar → the rest, in that order.

**Also worth doing while the coast is authored, but not blocking:** stand up ISO ground/fringe
rule-tiles and the road blob-47 autotiler from the kits imported today. They are sliced and catalogued
(`ShorelineIsoCatalog`) but nothing paints with them yet.

---

## 7. Open questions for the owner

1. **Is 2½ minutes the right walk across St Peters?** It is the number everything else keys off. A
   smaller island is cheaper to author and cosier; a bigger one earns the word "explore". Easy to dial
   later *if* item 1 above lands first — after that, island size is a number, not a rebuild.
2. **Ratify the reef ring?** (§5.1) — making the home island permanently unreachable by deep-draught
   boats is a strong, free, thematically perfect constraint, but it is a real design commitment.
3. **Cannery: in or out?** The brief says a fish stage that later became a lobster factory, and that a
   cannery "is not needed". A working relic is cheaper and reads better than a building with no job.
4. **Does the sandbar scene support the return trip at all,** or is it deliberately one-way on the
   first crossing (canon §6.0 has the first trip on foot, then you sail home)? §5.2 sizes it so a
   round trip is *possible but tight*; a one-way-only bar could be longer and more dramatic.
5. **The dragger's speed** (§1.2) — is a 25 m dragger being slower than a rowed dory intended?
