# Store clothing at LeBlanc's

**Status:** pass 1 (rule, data and tests; no art, no save change, no rack in the scene yet).
**Pillars:** P3 Living Working Coast (the village shop sells what a fisherman wears), P4 Earn It
(gear that makes the work easier is bought with money from the work), P5 Cozy (dressing your
character is a small comfort).

The owner's ask: clothing for sale at LeBlanc's that is **functional**. He can **try it on in the
store** on a page like the character creator, showing the garments on him, with a way to buy.

---

## 1. In plain words

A clothes rack stands in LeBlanc's. He walks up and presses the use key. A page opens with him in
the middle, lit and turning like he is in the character creator. Down the side is a list of what
the rack has, grouped by where it goes on him: **head, top, legs, feet, outerwear, hands**.

Moving through the list puts each garment **on him** in the picture straight away. Next to it the
page shows its **price**, how many the rack has **left**, and one line saying what it **does**
("Fish slime runs off it: landing a fish gets you half as dirty").

- **Buy** pays once, takes one off the rack, and puts it on him. It is his to keep.
- Something he already owns says **Put on** and costs nothing.
- **Leave** (or Cancel) closes the page and puts his own clothes back exactly as they were.
  Trying on never buys, never changes what he wears, and never takes anything off the rack.

Controls: keyboard (up/down, use key to buy, Escape to leave), gamepad (stick or d-pad, A, B) and
mouse (click a row to try it on, click Buy, click Leave). Each garment runs out on its own; when
one is gone the row shows "sold out" and the others are unaffected.

## 2. What each garment does

Every effect below uses a system that **already exists on main**. Where the real system does not
exist yet, the garment says so in its own line, or it is for looks only. Nothing here invents a
system quietly. All numbers and prices are **PROPOSAL** and live in the garment's data file, so
the owner can change them without code.

| Garment | Slot | Does (shown in game) | System it reads | Effect (PROPOSAL) | Price (PROPOSAL) | Rack stock (PROPOSAL) |
|---|---|---|---|---|---|---|
| Oilskin Jacket | outerwear | Fish slime runs off it: landing a fish gets you half as dirty. | Cleanliness: the dirt added each time a fish is landed | x0.5 | 40 | 2 |
| Sou'wester | head | Just for looks for now: rain does not soak you yet. | none (look only) | none | 8 | 2 |
| Rubber Boots | feet | Keep the flats' mud off: clam digging gets you half as dirty (digging does not dirty you yet). | Cleanliness: clam digging | x0.5, **does nothing today** | 18 | 2 |
| Wool Sweater | top | Warm wool: being tired weighs on you a quarter less. | Needs: how much tiredness slows him and lowers his prices | x0.75 of the penalty | 16 | 3 |
| Work Gloves | hands | Baiting a pot gets you half as dirty. | Cleanliness: the dirt added when a trap comes up and is baited | x0.5 | 6 | 4 |
| Watch Cap | head | Just for looks. | none (look only) | none | 5 | 3 |
| Work Overalls | legs | Hauling pots gets you a third less dirty. | Cleanliness: the hard-work stretch of a trap haul | x0.7 | 22 | 2 |

Seven garments. Effects multiply when two garments touch the same thing, and each one goes away
when he takes that garment off.

### Honest notes (what is missing)

- **There is no rain, cold or wetness in the simulation.** Rain is only drawn. So the sou'wester
  cannot keep him dry and the sweater cannot keep him warm in a real sense. The sweater uses the
  nearest real thing: it eases how much **tiredness** weighs on him. A real "warm and dry" effect
  needs a weather-exposure need first; that is a separate decision for the owner.
- **Clam digging, farm work and engine work make no dirt yet** (the kinds exist, nothing reports
  them). The boots' effect is wired and tested but stays at zero until digging reports dirt. A
  stronger idea for boots is **wading faster** (the wade slow-down is a real tunable today), but it
  lives in the player's walking code, which this pass does not touch. Owner's call: keep the mud
  line, or move boots to wading.
- **Wool socks** were left out: nothing to show (they sit under boots) and no real effect.
- A change of clothes in the middle of hauling a pot counts **from the next haul**, so the dirt
  line is never bent partway through.

### How this compares to Stardew Valley

In Stardew most clothes are looks only; rings and boots carry the stats (defence, immunity), and
hats are almost entirely cosmetic. Shops sell a rotating handful, and the tailor lets you see a
piece before you commit. We keep the good parts: a small, honest rack, a try-on before buying, and
a clear one-line effect. We differ on purpose: effects here are **work** effects (dirt, tiredness)
tied to the fishing life, not combat stats, and stock is the shop's real stock that runs out.

## 3. The art each garment needs

Today the figure on the try-on page is the live player through the same preview the creator
uses. What can change at runtime is **colour**; the **shape** (which garment, which hat) is baked
into each build (ADR 0051). So in pass 1 a tried garment shows as a colour stand-in on his body
(oilskins paint him oil-yellow with a navy shirt, the sweater paints a cream shirt, overalls paint
navy). Hats, boots and gloves show no change yet, and the page says the picture is to come.

The rig already draws, so they need **no new drawing**, only bakes:

| Garment | Rig option | What is needed |
|---|---|---|
| Oilskin Jacket | `garment: oilskins` | a baked skin per body for it |
| Wool Sweater | `garment: sweater` | a baked skin per body |
| Work Overalls | `garment: overalls` | a baked skin per body |
| Sou'wester | `hat: souwester`, `hatCol: oil` | a baked skin per body, or a hat on a head socket |
| Watch Cap | `hat: watchcap`, `hatCol: navy` | a baked skin per body, or a hat on a head socket |

Files: the options are in `docs/art/rigs/character/rig10/data/options.v10.json`; a build export
goes in `docs/art/rigs/character/rig10/builds/<name>.v10.json` with its sidecar in
`docs/art/rigs/character/rig10/gameplay/characterIsoRig10.<name>.gameplay.json`; the baked skin
lands under `Assets/_Project/Data/Characters/Skin` (or a variant folder like `SkinNext`).

**This is combinatorial.** Every hat times every garment times every body is a bake. The
recommendation for the art pass is to revisit ADR 0051 for clothing: hats as separate meshes on a
head socket (the pattern ADR 0053 uses for sockets), and only the body garment baked.

The rig has **no part** for these, so they need a rig change by the art director first:

| Garment | Rig change |
|---|---|
| Rubber Boots | boots are a fixed colour ramp (`BOOT`); needs a boot colour or a boot section in `docs/art/rigs/character/rig10/Art/characterIsoRig10.js`, a new option in `options.v10.json`, and the sidecars |
| Work Gloves | hands are skin; needs a glove section on the hand in `characterIsoRig10.js`, an option in `options.v10.json`, and the sidecars |

**Layering.** The rig draws one garment on the body. When he wears a sweater under oilskins over
overalls, the picture shows the outermost (outerwear, then legs, then top) while every garment's
effect still applies.

## 4. Save: what must persist, and the question for the owner

What must persist, per player: **which garments he owns** and **which he wears in each slot**.
Also the rack's **stock left**, if sold-out should survive a reload.

**Today's save does not hold any of it.**

- `WornOutfitId` is a colour preset, not a garment.
- `PlayerRecipe` holds his look, but its shape fields are locked to the baked build.
- `OwnedGear` is a list of gear ids. It could technically hold clothing ids, but that is still
  adding clothing to the save.
- `StorePurchases` rows are rewritten whole for a shop by the shelves, so the rack cannot share them
  safely without coordinating.

So **nothing is saved in pass 1**, and the rack is **not placed in the store**: buying something
that vanishes on reload would cost him money for nothing.

**Question for the owner (smallest change):** at the next save version, add two lists per player,
`OwnedClothing` (garment ids) and `WornClothing` (one garment id per slot, empty for none), plus
the rack's sold count as its own rows (rack id, garment id, units, day). Old saves load with
empty lists and a full rack. **Yes or no?** Until then the wardrobe lives only for the session.

## 5. What pass 1 built

- `ClothingDef`: one garment per file, ids `clothing.snake_case`, with slot, price, its one line,
  its effects, the colour stand-in it shows today, and the rig option it needs for art.
- `ClothingRackDef`: the rack at LeBlanc's (`shop.st_peters_general`) with stock per garment.
- `PlayerWardrobe`: one per player (co-op ready), behind `IClothingService` in Core. It owns,
  wears one per slot, and answers effect questions without allocating.
- Effect hook-ups in Core only: needs relief (`NeedsService`) and dirt scaling
  (`CleanlinessService`). With nothing worn both read exactly as before.
- `ClothingShop.TryBuy`: the one buying step (pays once, takes one of that garment, owns, wears).
- `ClothingTryOnModel` (UI): the try-on page as a pure rule, reusing the creator's preview through
  `IPlayerAppearance`. It never commits his look.
- EditMode tests for all of the above and for the shipped content.

**Not built yet:** the page's canvas, the rack in the store scene (the store layout is in progress
on another change; the rack wants a spot of wall near the back shelf, set through data only), saving,
and the art.
