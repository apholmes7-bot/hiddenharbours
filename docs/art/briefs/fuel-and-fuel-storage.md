# ART BRIEF — fuel: cans, pumps, and the things you keep it in

**To:** art-director (`agents/art-director.md` — your lane is `docs/art/rigs/**`)
**From:** the owner, 2026-07-25
**Status:** requested, not started. **M2 content** — nothing in the engine waits on it.
**Design of record:** [`../../design/fuel-and-refuelling.md`](../../design/fuel-and-refuelling.md)

Fuel becomes a real thing you buy, carry and run out of. **Gas** runs every motor; **diesel** runs
the bigger hulls. Nine Mile Creek sells both at dockside pumps; St Peters sells **gas only**, over
the counter at the general store — which means someone is carrying it to the boat in a can.

---

## 1. ⭐ The one requirement that is gameplay, not decoration

**Gas and diesel must be tellable apart at a glance, at 32 px per metre, in the dark, in the rain.**

Running the wrong fuel — or standing at the wrong pump with the wrong boat — is a real mistake the
player can make, and the art is the only thing that warns them. The real world already solved this
with colour convention (gas red, diesel yellow or green), and players import that expectation for
free. **Use a colour split, and make it survive the palette guard-rail** (ADR 0015 — ramps only,
owner palette).

Do not rely on a label, a decal or a letterform: at this resolution and this camera, a 3-px legend
is not readable and the pixel-art rule is no anti-aliasing. **Silhouette and ramp carry it.**

If the two ramps you'd reach for are too close in value once dithered, say so and propose a shape
difference as well (a different cap, a different spout) — belt and braces is fine here.

---

## 2. The pieces

### 2.1 Jerry can — the hero object (do this one first)

The one the player actually handles: bought at the general store, carried down the dock, tipped into
the dory's tank. Portable, ~20 L, a handle, a spout or cap.

- **Two colourways minimum:** gas and diesel (§1).
- It gets **held, set down on a deck, and stacked in a hold** — so it wants the same treatment as
  the existing carried containers. **Precedent to follow: `fishToteRig.js` (`FishTote`) and
  `bucketRig.js` (`BucketIso`)** — both are carried/rest containers with iso pivots, and `bucketRig`
  usefully exposes **`pivotCarry` and `pivotRest`** for exactly this. Copy that shape.
- Empty vs full is worth a thought: does it sit differently, sag differently, read differently? Your
  call whether that is worth a variant.

### 2.2 Fuel pumps — Nine Mile Creek

Dockside pumps you lie alongside. **Two of them: gas and diesel** (§1 applies — they should be
readable as a pair from across the wharf).

Nine Mile Creek is a working wharf, not a marina: these are **utilitarian, weathered, salt-bitten**.
See [`../../design/nine-mile-creek-wharf.md`](../../design/nine-mile-creek-wharf.md) for the wharf's
character — bait sheds, trap storage, a winch on the west wall, fish buyers' trucks in the lot.
Hoses, a stand, maybe a small sign board.

They sit **on the wharf deck**, so they must pin against the wharf tile kit
(`wharfKitRig.js` / the 32×56 deck cell — see [`../../../docs/art/wharf-tile-kit/`](../wharf-tile-kit/)).

### 2.3 Larger storage — the upgrade path

Drums, a shed tank, a wharf-side bowser. **Lower priority and less specified** — the design doc
deliberately leaves the upgrade path open, so treat these as a small set of set-dressing/props that
could later become functional, not as a system.

If you only do one: a **drum** (the 200 L barrel) is the most universally useful, reads instantly,
and doubles as harbour set dressing anywhere in the world.

### 2.4 An oil tin — ⚠️ ONLY IF the owner says yes

Ned's motor is a **two-stroke**, which in the real world burns a petrol/oil premix rather than
straight gas. Whether the game models that is an open owner question
([`../../design/fuel-and-refuelling.md`](../../design/fuel-and-refuelling.md) §7 Q4) — it is either a
lovely diegetic detail on an old inherited engine, or a fiddly second consumable on the player's
first motor. **Do not author this until it is ruled on.** If it is ruled in, it is a small tin,
older than everything around it.

---

## 3. Technical

Standard fleet contract — same as every prop kit you have shipped:

- **32 px = 1 m.** Fixed ¾ iso, **elev 40°**, 45° steps.
- Transparent background, no anti-aliasing, upper-left key light, ordered dither.
- No keyline. The silhouette is carried by the form's own dark side, not by a drawn outline. The
  turning face must go dark enough to separate from any background in the master palette — never
  let a lit face run to the sprite edge.
- Pale subjects need this deliberately. A white hull or wall separates on a darkened sheer strake,
  a shadowed tumblehome, a shaded eave — not on an outline. If a form can't hold its edge without
  one, the form needs work, not a line around it.
- Keep depth-edge darkening. That's the separate interior rule (adjacent surfaces >0.30 m apart in
  depth, far side darkened). It stays — it's what keeps overlapping parts of the same object
  readable. (ADR 0031.)
- Pin by **pivot**, never by corners.
- **Ramps only** (ADR 0015 palette guard-rail).

**Directionality:** a jerry can is roughly symmetric, so it may not need all 8 headings — the catch
kit contains both directional and non-directional rigs, and `shellfishRig` is the precedent for a
prop that legitimately has no camera at all. **Your call; state which you chose in your README**, and
if you go directional, the azimuth convention gets **measured from pixels** at bake time, never read
off a declaration (that mislabel has shipped defects five times — see `docs/art/rigs/README.md` §
"THE AZIMUTH SPLIT").

**Sheets or mesh?** These are props, not hulls — **sprites, baked as usual.** ADR 0022 is explicit
that mesh is for boats; buildings, characters and props have no memory problem and stay sprites.

⭐ **If you export `F`, `MATS`, `GAIN`, `BIAS`, `LN`** the mesh extractor needs no shim, should any
of these ever want to be one. Same ask as the dory outboard brief — free to do on a new rig.

---

## 4. Deliverables

1. The rig(s) — your lane, `docs/art/rigs/`. One rig with the pieces as variants is likely cleaner
   than four files; your call.
2. A `README.txt` in `docs/art/fuel-kit/`, house format (see `docs/art/punt-iso-rig/README.txt`) —
   coordinates first, then the parts, then which colourway is which fuel.
3. A **gameplay sidecar** if any of these need mount points or carry anchors — schema in
   `docs/art/rigs/gameplay/`.
4. Baked sheets per the usual pipeline (§3).

Branch `art/<short-desc>`, one concern per PR, `.github/pull_request_template.md`. Say in the PR
body which builders the owner must re-run after merge.

---

## 5. Open questions

1. **Is St Peters gas sold in cans, over the counter?** (design §7 Q2) — if yes, the jerry can is on
   screen in the opening and deserves the most polish of anything here.
2. **The oil tin** — §2.4, gated on the owner.
3. Empty/full jerry-can variants — worth it, or is one state enough?
