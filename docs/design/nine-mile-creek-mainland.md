# Nine Mile Creek — the mainland (Phase A: the geography)

> **Status:** Phase A geography **PROPOSED and BUILT AS DATA + TESTS**, awaiting the owner's first
> in-editor scene build. Subordinate to [`../vision-and-pillars.md`](../vision-and-pillars.md) (canon),
> [`world-and-regions.md`](world-and-regions.md) (what the region *is*) and
> [`scene-sizing-and-world-scale.md`](scene-sizing-and-world-scale.md) (how big it may be).
> The wharf's own brief is [`nine-mile-creek-wharf.md`](nine-mile-creek-wharf.md).
>
> **What prompted it:** the owner's 2026-08-06 dispatch with reference photographs and an overhead —
> *Nine Mile Creek is the MAINLAND, not the small island it currently is.*
>
> **Phase A is the geography** (terrain, coast plan, painted seabed, the tidal crossing, roads, town
> lots, landmarks). **Phase B** dresses the wharf and town from the ISO wharf kit / wharf decor /
> utility pack once those rigs have baked sheets. Everything Phase B needs positioned is positioned in
> Phase A, so nothing is built twice.

---

## 1. The attachments, inventoried

Five images came with the dispatch. **They are the layout truth**; where the coordinator's paraphrase
and the images disagree, the images win — and §2 records the two places they did.

| # | What it is | What it fixes |
|---|---|---|
| 1 | **Wide aerial, wharf + hinterland.** Farm fields with a red dirt road running down; a large shallow **lagoon/barachois** behind a marshy spit; the wharf on a low point jutting into the bay; a rock breakwater running out from the shore; open water filling the bottom-right. | The wharf sits on a **narrow low point between a tidal pond and the open bay**. The land behind is *fields*, not forest, and the road comes in from inland across a **neck between two ponds**. |
| 2 | **Close aerial, the wharf complex.** A row of ~8 grey gable **fishing shanties** plus a second cluster; a **squared-U wharf** (approach pier + face wharf enclosing a basin) with ~14 boats moored in two rows; a **timber-crib breakwater** with a plank catwalk; a concrete **boat ramp**; stacked lobster traps; trucks on a gravel apron. | The wharf's **shape** (walls + basin), the **fleet size** (12–14), the **crib** armour, the **shanty row**, the **ramp**, and the **parking**. |
| 3 | **Oblique aerial from the south-east.** The point in red soil; the road along it; the wharf jutting into visibly **darker (deeper) water**; the breakwater to the south with its catwalk; boats on the hard. | The **deep water lies east/south-east of the point** — which is *why* the wharf grew there. Confirms the point's ground is **low made ground**, not a headland. |
| 4 | **Overhead (Google), close.** "Nine Mile Creek" pinned on Route 19; **St Peters Island offshore to the south-east**; a wide pale-brown **shoal** running from the mainland shore out to the island; Rice Point to the south-west. | The **bearing and the crossing**: the bar leaves the mainland shore and runs **out to the ESE/SE** to St Peters. The bay is a **broad shallow shoal**, not a deep frontage. |
| 5 | **Overhead (Google), wide.** Nine Mile Creek on the **western shore of Hillsborough Bay**; St Peters Island just south-east of it; Point Prim and the strait beyond; Stratford/Southport at the head of the bay. | **Water is EAST.** The mainland coast runs roughly **north–south**, facing into the bay. |

### 1.1 The geography, restated

> Everything below is region-local metres, origin at the region's centre, compass bearings (N = 0,
> clockwise). It is all encoded in `NineMileCreekMainland.cs`, and every number quoted is re-derived by
> the EditMode tests rather than copied.

- **Water is EAST; the fields are WEST.** The coastline runs south → north across the whole region,
  596.0 m of it, from the south edge to the north edge. A mainland is a **side**, not a shape.
- **The crossing lands at `(60, −150)`**, 133.6 m along the coast run from its south end — toward the
  **southern** third of the coast.
- **Beach lies south of the landing**, as the owner said: the landing sits in a 176 m beach sector with
  **133.6 m of it south** of the crossing point and 42.4 m north — 76% south.
- **The bar leaves the shore on bearing 99.3° (ESE)** and runs 305.0 m out to the seam.
- **St Peters bears 121.7° (SE) from the town centre** — inside the SE octant, exactly as the owner
  described. *The bar's own bearing and the island's bearing from the town are different numbers and
  both are true; the paraphrase collapsed them into one.*
- **North of the crossing the coast stands up**: a low ledge bank, then a gully down to the foreshore,
  then the tall red bluff — the weather face. Standing on it you look back **SE across the bar to St
  Peters**, which is the sightline the handoff asked about.
- **The wharf is on a low made spit at the creek's mouth**, ~260 m north of the landing, with the
  **barachois behind it** and a second **marsh pool** south of the road. The road runs the neck between
  them (which is what the causeway in photograph 1 is doing).
- **The town is inland**, strung along a through-road (the overhead's Route 19) ~230 m west of the
  shore — the way a rural PEI community actually sits, not clustered round a square.

### 1.2 Roads

| Road | Route | Length | Walk |
|---|---|---|---|
| **The bar road** (dirt) | the landing → north along the clifftop → the junction | **271.9 m** | 1:30 |
| **Wharf Road** (gravel) | the town → the neck between the ponds → onto the spit → the wharf front | **321.8 m** | 1:47 |
| **The through-road** (gravel) | south edge → the town → north edge; the Route 19 analogue | **564.4 m** | 3:08 |
| **The gully path** (foot) | off the bar road to the head of the Access gully | 19.0 m | — |

The owner's two named roads both do what he named them for: *"from the crossing, a road the character
can follow leads to Nine Mile Creek"* and *"a road named Wharf Road runs all the way to Nine Mile
Creek's wharf front."* They meet at `(−16, 92)`.

**Walk budgets:** landing → wharf front **2:23**; landing → town **2:24**; wharf → town **1:47**.

### 1.3 Town lots

Nine lots, flanking the through-road and the west end of Wharf Road: harbourmaster's office (the cod
licence), chandlery (the rod), general store, tavern, parish hall, three houses, and one **boat shed**
— see the ⚠ in §6. Closest pair 34 m apart against a 6 m reserved radius; every lot ≥ 26 m from the
nearest carriageway; all on the +6 m plateau.

---

## 2. Corrections to the handoff

The handoff asks to be told where it is wrong. Three places.

1. **"The NMC wharf's ruled depth ladder is 0.6 / 1.6 / 6 m — keep it (the wharf memory: NMC is the
   deep-water berth)."** The two halves of that sentence disagree. The ladder
   ([`nine-mile-creek-wharf.md`](nine-mile-creek-wharf.md) §4) is **three harbours**: St Peters' dock
   ~0.6 m, **Nine Mile Creek ~1.6 m**, Port Greywick 6 m dredged. Nine Mile Creek's number is **1.6**.
   The repo wins, and "the deep-water berth" resolves to the sense that survives: NMC is the deepest
   berth the *starter* world has — **the lobster-boat berth**, which is precisely the owner's stated
   progression ceiling for this region. Built at 1.6 m.

2. **The ladder's "excludes the side dragger" does not survive the tide change** (§4 below). Gating is
   emergent — `waterLevel − bed > draught`, which the wharf doc says itself. Against a −1.6 m bed under
   a ±2.2 m swing:

   | Hull | Draught | Afloat, as a fraction of the cycle |
   |---|---|---|
   | Dory | 0.30 m | 70.1% |
   | Fishing skiff | 0.35 m | 69.2% |
   | Punt | 0.50 m | 66.7% |
   | Console skiff | 0.55 m | 65.8% |
   | **Lobster boat** | **1.30 m** | **54.4%** |
   | **Cape Islander** | **1.40 m** | **52.9%** |
   | **Side dragger** | **2.90 m** | **29.9%** |

   The ordering is right and the working hulls land at "a little over half the cycle" — the same
   nagging-constraint shape St Peters' berth has. But **a 4.4 m tide range is wider than the 2.6 m
   draught spread**, so no single bed can admit a 1.4 m hull for half the cycle *and* exclude a 2.9 m
   one outright. The dragger can enter near high water and then **take the ground under her own weight
   for 70% of the cycle** — a better and more honest exclusion than a wall. ⏳ **Owner call** if it
   should land harder; the lever is the bed, not the tide.

3. **The bar's landing did NOT require touching a St Peters coast sector**, so the stop-and-report
   condition did not fire. Regions do not share a coordinate frame, so the seam is a passage band and an
   arrival point rather than a shared line — the mainland could author its half wherever it read best.
   `StPetersCoastTests` is untouched and St Peters' 22.48°/22.5° plan is not nudged.

---

## 3. Region dimensions, and the seam

### 3.1 The rectangle: **760 × 560 m**, centre at the origin

Sized by time to cross, per the sizing ruling:

- **The long axis (E–W, 760 m) is a CORRIDOR** — it carries the crossing, and
  [`scene-sizing-and-world-scale.md`](scene-sizing-and-world-scale.md) §1.3 is explicit that a
  corridor's length is set by the tide window, not by comfort.
- **The short axis (N–S, 560 m) is the FOOT REGION** — the landing → wharf → town walk, **3:06** at the
  3.0 m/s walk. Slightly over the 2–3 min guideline for exactly the reason the wharf doc gives for
  600 × 400: *it is two places, and the walk between them is what makes them feel like two places.*
- **760 m is St Peters' own width, deliberately.** These are the same water 610 m apart across one
  sandbar; they get the same scale, the same tide and the same 2 px/m seabed. A player who walks the
  bar should not feel the world change units halfway.
- Painted seabed: 1520 × 1120 texels = **1.62 MiB** of R8. Comfortable.

### 3.2 The seam sits at the **bar's midpoint**

St Peters owns 305.0 m of crossing; the mainland owns 305.0 m more. **Total crossing 610 m** — which
lands, by coincidence rather than design, within 10 m of the 600 m bar the sizing doc originally
recommended before the bar was shortened.

**Why the midpoint and not the landing:**

- St Peters' bar today **ends in open water** at its region edge, with a passage band 6 m past the tip.
  There is no landfall on that side to move. The mainland supplying the other half is what turns two
  half-crossings into one crossing.
- A load **at the landing** would fire exactly as the player arrives. Stepping ashore after a long walk
  on wet cobble *is* the beat; a scene swap on top of it wastes it. A load at the midpoint fires on flat
  open flats with nothing happening — the cheapest place in the region to hide one.

**The two halves are built as mirror images, and this is the load-bearing part:**

| | St Peters | Mainland |
|---|---|---|
| Crest | 0.88 m (= 0.4 × amplitude) | 0.88 m — the same number, not a similar one |
| Half-width | 30 m | 30 m |
| Flank shoulder | −4 m (its deep-harbour floor) | **−4 m** — copied, *not* this region's −6 m floor |
| Passage band | 6 m past the tip | 6 m past the tip |
| **Ground at the seam** | **0.372 m** | **0.359 m** |
| **Bared half-width at spring low** | **17.5 m** | **17.5 m** |
| Tide | mean 0, amp 2.2, phase 1 h | **identical** |

So the crossing **bares at the same tide and to the same width on both sides of the load**.
`NineMileCreekMainlandTerrainTests.TheCrossingIsOneBarEitherSideOfTheSeam` measures both and holds them
together.

> **The shoulder is not decoration.** How wide a bar bares is decided by how fast its flanks fall, not
> by its half-width. The same 30 m half-width tapering straight into this region's −6 m bay bares
> **13.5 m** either side at spring low where St Peters' bares 17.5 m — the crossing would have visibly
> narrowed by a quarter the instant you crossed the seam. Giving the mainland's bar St Peters' shoulder
> fixes it by construction.

### 3.3 What the split crossing does to the tide window — ⏳ **owner call**

At the walk, the whole 610 m crossing is **3:23 one way, 6:47 both ways**, against a **9:48** spring dry
window: **+3:01 of margin.**

The sizing doc flags that the crossing's teeth are currently blunt and that a **~900 m** total bar would
restore them ("you must sprint the return"). **That does not fit this region.** A 900 m total needs a
595 m mainland half, which needs a region **~1060 × 560 m** — 300 m wider, almost all of it open water.
The options, with the arithmetic:

| Total bar | Mainland half | Region width | Round trip | vs 9:48 window |
|---|---|---|---|---|
| 610 m *(built)* | 305 m | **760 m** | 6:47 | +3:01 — you can dawdle |
| 750 m | 445 m | ~910 m | 8:20 | +1:28 — you keep an eye on the water |
| 900 m | 595 m | ~1060 m | 10:00 | −0:12 — you must sprint the return |

Built at **610 m** because it is the *midpoint* answer and it changes the crossing's feel least; the
other two rows are one constant away (`NineMileCreekMainland.BarTo` plus the region width).

---

## 4. The tide — ⚠ this region's tide CHANGES

The shipped Nine Mile Creek authors **mean 0, amplitude 0.8 m, phase 2 h** — the "gentle market harbour
so business is never stranded" profile, written when this region stood in for Port Greywick.

**It cannot survive the recreation**, and the reason is geometry rather than taste: the bar is **one
bar spanning the seam**, and its exposure is a function of (crest, amplitude, phase). Two tides either
side of the seam means the crossing is dry on one side and flooded on the other at the same instant.

So the mainland takes **St Peters' tide verbatim: mean 0, amplitude 2.2 m, phase 1 h.** It is also
simply true — these are the same 610 m of water. What it costs is the market-harbour convenience, which
the 2026-07-25 ruling already moved to Port Greywick, one road up the hill. A working wharf on a big-tide
coast *should* dry out under its fleet at spring low; the ladders and tyre fenders in photograph 2 exist
for exactly that.

**Consequential:** `RegionDef` for Nine Mile Creek must change from `IsDeepHarbour = true,
HarbourDepthMeters = 6` to **`false` / `1.6`** when the builder lands (Phase A-2, §7). Those fields are
flavour today — nothing gates on them — but leaving them saying "deep dredged harbour" would be a lie
in the data.

---

## 5. The coast plan

Its own plan and its own tests, as required. St Peters' plan sits **22.48° inside a 22.5° law** and was
not touched.

| From (m along) | Class | Length | What it is |
|---|---|---|---|
| 0 | Beach | 176 | the south beach — **the arrival**, with the landing at 133.6 |
| 176 | Dune | 34 | the marram top of the beach |
| 210 | **LedgeCliff** | 58 | the low red bank; its bench bares on spring tides only |
| 268 | Access | 26 | **the gully** — the one way down to the foreshore |
| 294 | **Cliff** | 76 | the weather face north of the crossing; the St Peters sightline |
| 370 | **DeepShoreCliff** | 29 | the spit's root — deep alongside, which is *why* the wharf is there |
| 399 | Beach | 122 | the creek mouth, both banks |
| 521 | Dune | 30 | |
| 551 | Beach | 45 | the north beach — the wharf's "soft arrival" |

**Cliff share 27.4% of 596.0 m, against a legal ceiling of 93.0%.**

> **The single biggest difference from St Peters.** The aspect law lets a cliff stand only where its
> seaward normal snaps within 22.5° of one of the kit's five facings (W · SW · S · SE · E — no north,
> because a north-facing wall shows its shadowed back to a ¾ top-down camera). **An east-facing mainland
> is almost entirely legal: 93.0% here against St Peters' 55.6%.** That headroom is why the mainland
> could be given its own plan rather than squeezing another sector out of the island's, and it means
> more rock can be added later without an argument with the law. This plan spends it sparingly on
> purpose — the owner's photographs are of a *low* coast, and the drama belongs to the crossing and the
> working wharf.
>
> **The one illegal stretch is the creek mouth** (41.8 m; its two inner banks face NE and NNE) — and
> that is exactly where the marsh and the sand belong anyway. The law wearing the photographs' clothes.

---

## 6. Depths, and the wharf as terrain

| Feature | Elevation | Why |
|---|---|---|
| Open bay floor | **−6.0 m** | nothing grounds out there |
| Foreshore shelf | −1.0 → −2.6 m over 60 m | the overhead shows a **broad brown shoal**, not a deep frontage |
| **Harbour shoal (the basin, the approach)** | **−1.6 m** | ⭐ **the ruled gate** — the lobster-boat berth |
| Barachois | −0.8 m | a mirror at high water, red mudflat at low |
| Marsh pool | −0.4 m | the second pond; its job is to leave the road a neck |
| Bar crest | +0.88 m | = 0.4 × amplitude — a **ratio**, not a height |
| Wharf deck | **+3.0 m** | 0.8 m of freeboard at spring high |
| Yard / spit | +3.6 m | you step **down** onto the wharf, as you do on a real one |
| Breakwater crest | +3.4 m | |
| Fields | +6.0 m | dry at every tide |
| Cliff toe / ledge bench | −3.19 m / −1.65 m | authored as **tide fractions** (0.45 / 0.25), never metres |

Two findings worth stating plainly:

- ⭐ **The basin is a SHOAL, not a dredging** — the correction that fell out of measuring the first
  draft. Authored as a *carve* to −1.6 m it did nothing at all, because the bay floor here is −6 m and a
  carve can only cut: the "dredged" basin came out four and a half metres **deeper** than its own gate
  and every hull in the game could lie in it at any tide. The photographs show the opposite of dredging
  — the wharf is built **out onto a shoal**, and the shoal *is* the gate. It is now a fill that raises
  the bay to −1.6 m under the walls, the breakwater and the approach; the walls stand on top of it. One
  number gates the berth and the entrance alike.
- ⚠ **The quay face is 4.6 m tall** (deck −1.6 m basin), and 5.2 m of it stands exposed at spring low.
  The ISO wharf kit bakes a **24 px** overhanging face — **0.75 m** at PPU 32 — so **the kit cannot draw
  this wall in one course**. Phase B must tile/stack the face vertically, or the deck must come down
  toward the water. Phase A authors the honest terrain and reports the mismatch rather than quietly
  flattening a big-tide wharf to suit a sprite. **Flagged for `art-pipeline` / Phase B.**

⚠ **The cliffs are still walkable-by-sim** until the slope gate lands. Flagged, not fixed, per the
handoff.

⚠ **The boat shed.** The 2026-07-25 ruling says there is **no shipwright in this region**, but the
shipped scene has a "shipwright shed" that sells the Punt and the pots, and the economy data hangs off
it. A lot is reserved under a neutral name so nothing breaks. **Where the shipwright's yard really lives
is the coordinator's open question**, not world-content's to close.

---

## 7. What Phase B needs, already positioned

Nothing in this list has to be re-decided, and nothing Phase B lands on has to be torn out first. All of
it is in `NineMileCreekMainland.cs`.

| Phase B will build | Phase A has positioned |
|---|---|
| The quay from ISO pieces | **North wall** 84 × 10 m at `(128, 92)`; **west wall** 10 × 48 m at `(87, 68)`; deck +3.0 m; the tall face measured (§6) |
| Moorings, finger piers, fenders, ladders | **14 berths** at 5.5 m spacing from `(98, 85)` along the north wall's **south** face — the one edge the kit gives a tall face to |
| The crib breakwater | A **92 m south arm** at `(140, 38)`, crest +3.4 m, and the ~50 m entrance it leaves |
| The winch + unloading apron | `(87, 84)` / `(87, 74)` on the west wall — and the note that its water side faces **east**, a curb-only edge, so the winch wants to be a **tall legible object**, not a detail on a wall |
| Bait shed · trap store · fish holds | `(136, 132)` · `(148, 130)` · `(120, 112)`. **No `fishPlant` / `cannery`** — the owner's "no processing here yet" |
| The shanty row | Five sheds at 14 m spacing along `y = 132` (the photograph shows eight; five reads as the same place at this scale) |
| Buyers' trucks + parking | `(92, 118)` / `(88, 110)` |
| The derelict dory on the hard | `(70, 118)` — at the wharf, per the 2026-07-25 rider |
| The boat ramp | Head `(66, 100)` → toe `(78, 90)`; deliberately **not** authored into terrain — a ramp is dressing |
| Harbour beacon / range light | The breakwater head `(184, 38)`. *(The "small lighthouse on the point" in `world-and-regions.md` §6.3 belongs to Greywick; a community wharf gets a range light.)* |
| **The utility-pole route** | **Along Wharf Road, 5 m to its north, at 40 m spacing**, town → wharf, ending at the yard light |
| Town buildings | The nine lots of §1.3, each with a 6 m reserved radius |
| Ground paint + road tiles | The coast plan (§5) and the four road routes (§1.2) |

---

## 8. How this shipped, and what the owner has to do

### 8.1 The slice was split, and why

The handoff asks for one PR if possible. It is two, and the reason is honest rather than tidy:

- **Phase A-1 (this):** the geography as **data and pure maths with tests** — the mainland terrain type,
  its coast plan, the authored plan, and 30-odd EditMode assertions. It adds files and changes no
  existing behaviour, so `main` stays green and the build stays playable whatever a review decides.
- **Phase A-2 (next):** the **builder wiring and the scene rebuild** — swapping `NineMileCreekBuilder`'s
  `RectTidalTerrain` for the mainland terrain, moving every creekside site onto the new geography,
  updating the `RegionDef` (§4), re-baking the painted seabed, and re-running the six existing test
  files coupled to those constants (`NineMileCreekDockTests`, `NineMileCreekWharfTests`,
  `NineMileCreekDoryTests`, `NineMileCreekFlavourTests`, `ShorelineConvergenceTests`,
  `CrossingDirectionTests`).

Splitting there is not arbitrary: **A-1 is verifiable headlessly and A-2 is not.** The first build of a
recreated region has to happen in the owner's editor anyway (§8.3), and the geometry is the thing that
must be right before 1500 lines of editor code are written on top of it.

### 8.2 ⚠ Not verified in an editor

There is no Unity install in the environment this was authored in, so **nothing here was compiled and
no test was run locally**. Every geometric claim *was* verified — the whole composition was
re-implemented independently and probed at ~2000 points, which is how the basin defect, the berths
moored on the deck, and a road through the marsh pool were all caught before they shipped. But CI is the
first compiler this has met. Treat a red CI as mine, not as the environment's.

### 8.3 What the owner clicks

`RegionBuildGuard` blocks headless scene rebuilds (batchmode auto-cancels the wipe dialog, exit 0,
silently), so the recreated region's **first build must run in the owner's editor**. When Phase A-2
lands:

1. **Delete the scene file only**, keeping the `.meta`:
   `Assets/_Project/Scenes/NineMileCreek.unity` — **keep** `NineMileCreek.unity.meta` so the GUID
   survives and every reference to the scene keeps pointing at it.
   *(Or skip the delete and answer the wipe dialog with "Rebuild from zero" — same outcome, one more
   click.)*
2. **Hidden Harbours ▸ Build Nine Mile Creek Scene.**
3. **Hidden Harbours ▸ Terrain Paint Tool** → bake the region's painted seabed at 2 px/m
   (`NineMileCreekSeabed`), then save.
4. Press Play and walk it: the crossing, the bar road, Wharf Road, the wharf front.

Nothing else in the project needs deleting. Region builders are re-runnable and converge on re-run.
