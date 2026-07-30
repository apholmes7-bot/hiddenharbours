# Land, Crafting & Interiors — the owner's direction, captured

**Status:** RATIFIED DIRECTION (owner, 2026-07-30) — design of record for the land game and
seamless interiors. Build phase: **M2/M3** except where a line explicitly says M1. Nothing in
this document authorizes out-of-phase construction (CLAUDE.md rule 8); it exists so that when
these systems build, they build to the owner's words and not to a reconstruction of them.

---

## 1. The owner's words (verbatim, 2026-07-30)

> "im going to go against your judgment and still make it seamless and the interior to
> represet the exterior footprint. it adds value to upgrading your buildings when storing
> items or progressing. when i walked st. peters at its new size it felt vast, which is great
> for exploring if the areas detailed, and makes sailing more imersive. we will add ponds and
> streams to st peters to break up the barreness, but i feel like we need more on land tasks.
> we have all this space i want to make use of it. what level of crafting should be in this
> game? could you manufactuer traps to use or sell? rods to create? what purpose can we give
> to land"

And, ratifying the coordinator's proposal that follows: *"yes please capture this direction."*

## 2. Ruling — interiors are SEAMLESS and true to the exterior footprint

The interior of a building is drawn inside its world silhouette, at true scale, matching the
building's placed orientation. **Footprint is a progression axis you feel from inside**: a
bigger building is a bigger real room — more shelf runs, more floor, more loft — so upgrading
a building is visible in the space itself, not in a menu number.

This supersedes the coordinator's pocket-room recommendation (recorded and withdrawn). What
the pipeline absorbs to honour it:

1. **Interior layout matches the placed facing.** Mitigation: interiors bake TO ORDER per
   *placed* facing — only orientations actually sited ever bake. The open 8→4 facings
   decision (#352) now also multiplies interior bake cost; decide it before the interior kit
   bakes wide.
2. **The building rig gains a roof-off/cutaway export state** (exteriors are single opaque
   sprites), plus an enter reveal — roof fade or sprite swap.
3. **True scale, no bigger-inside cheat.** Interior gameplay density (storage, workstations)
   is designed within the footprint — that constraint IS the upgrade motivation.
4. Interior walkability = walls as colliders within the footprint; the standable-surface and
   positioning patterns from the wharf apply.

## 3. Ruling — St Peters' vastness is a feature; fill it, don't shrink it

*"it felt vast, which is great for exploring if the areas detailed, and makes sailing more
imersive."* The island keeps its 760×520 scale. Two consequences:

- **Ponds and streams** break up the interior. ⚠️ Architecture note: the water stack assumes
  ONE tide-driven sea; inland freshwater needs its own representation (a simple separate
  plane/material — no tide, no swell, no absorption tuned for the Atlantic). Spec required
  before world-content paints any inland water.
- **A pond is winter ice.** Ice cutting → the ice house → the existing cold-chain economy
  (§7.3/#322/#325) is the historically true loop and ties the new terrain directly into a
  shipped system.

## 4. Direction — workshop crafts, not crafting trees

The crafting model is **a small set of real maritime trades, each a physical workstation,
each shallow-but-tactile, each feeding the existing economy** — and each following P4's arc:
*hand-make it first, then build the thing that makes it for you.* The interaction pattern is
the culling-table work-surface (full-screen bench, hands on the work) — never a recipe menu.

The trades, ordered by how directly they connect to shipped systems:

| craft | feeds | the P4 automation rung |
|---|---|---|
| **Lobster-pot building** — laths from the woods, rope from the store; pots are used AND sold (a market channel) | M2-33 trap loop; timber; market | the net shed (M2-J waterfront ladder) |
| **Jig & lure tying** — evening bench work | `FavoredLures`/`LureTag` (shipped, underfed) | — (stays a hand craft) |
| **Salt & smoke** — preservation as the freshness system's OTHER half; salt fish + smokehouse + drying flakes | freshness/rot (§7.3); a durable product with its own price | the cannery (M2-J) |
| **Ice harvest** — cut pond ice in winter | ice/cold chain (shipped) | the ice house (M2-J) |
| **Net & gear mending** — maintenance as rhythm | storm/damage (P5) | staff (M3) |
| **Timber** — the woods become a resource | pots, repairs, building upgrades (§2 makes these felt) | — |

**Rods are NOT crafted.** The rod ladder is shop progression (the used rod, the better one
you save for) — crafting rods would collapse it. Rods are bought and **maintained** (a re-wrap
at the bench when one wears); craft expression lives in jigs and rigs, where handline culture
actually put it.

## 5. What land is FOR — the sentence to build from

**Sea earns; land compounds.** The sea is where money and danger live (P1/P5). The land is
where you convert, prepare, and grow: build pots for tomorrow, salt today's catch, tie
tonight's jigs, cut winter's ice, forage the seasonal round (the shrub kit already bakes
berry/fruit phenology — blueberries on the barren in August are nearly free), dig the flats,
beachcomb after storms (the flotsam vision), tend a kitchen garden, poke through the §5.1
ruins. Vast is right for sailing; it becomes right for walking when every district of the
island has a season and a reason.

## 6. Phase placement & open questions

- **M1:** nothing from this document builds in M1. (M1's land game stays: the dig, the store,
  the village, the freshness clock.)
- **M2:** pots, salt/smoke first pass, ice harvest (needs ponds), interiors for the buildings
  that matter (Ginny's cottage first), the waterfront ladder as the automation spine.
- **M3:** staff working the benches; the full production/logistics graph.
- **Open (owner):** which building gets the first true interior; the 8→4 facings call (now
  doubled in weight); pond/stream placement pass on St Peters; whether the kitchen garden is
  M2 or M3; salt-fish market design (economy-sim brief when its phase opens).
