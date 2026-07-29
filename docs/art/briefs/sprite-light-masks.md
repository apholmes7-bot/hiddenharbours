# ART BRIEF — sprites that catch a coloured light (the two-channel lighting mask)

**To:** art-director (`agents/art-director.md` — your lane is `docs/art/rigs/**`)
**From:** the owner, 2026-07-25
**Status:** requested. **Trees first.** Nothing in the engine waits on it — the shader is not built
yet, and this brief exists so that when it is, the art already fits.
**Design of record:** [`../../design/sprite-light-response.md`](../../design/sprite-light-response.md)
(read §4.1 for the mechanism and §4.2 for what we take from it).

---

## 1. What the technique needs from a sprite, in one breath

A flat sprite that can **catch and rim with the colour of a nearby light** carries a **two-channel
lighting mask** alongside its art:

| Channel | Means | Lights up when |
|---|---|---|
| **Green** | *front-catch* — the body/masses that face the viewer | the light is **in front of** the object |
| **Blue** | *back-rim* — the outer fringe of the silhouette | the light is **behind** it |

The shader decides front-vs-behind by a screen-space lookup against the light's own gradient sprite
and then lifts one channel or the other. **You never author the light — only the object's two
channels**, or a sprite from which those two channels can be derived.

---

## 2. ⚠️ The measured authoring rule — leave the masses some MASS

We ran the derivation over the existing trees on 2026-07-25 and it split cleanly:

| Sprite | Result |
|---|---|
| **`Tree38`** (104×176, tiered blocky masses) | ✅ **Works.** Clean green interior, clean blue rim. Lit results read like the reference. |
| **`Tree41`** (171×244, fine spruce needles) | ❌ **Degenerates.** Almost every pixel sits within ~2 px of an edge, so the rim channel swallows the whole tree and it glows *uniformly* instead of rimming. |

⇒ **Author foliage as clumps with an interior at least ~6 px across**, so a ~2 px rim still leaves a
body behind it. Filigree that is everywhere-edge has no front-catch channel to lift, and the effect
collapses into a flat glow. This is the single most important line in this brief.

**Corollary — the silhouette IS the rim.** The back-rim channel comes straight off the outline, so a
speckled or heavily dithered edge produces a speckled rim in-game. Keep outer edges deliberate;
noise on the boundary is no longer free.

---

## 3. ⭐ The real ask: **a tree rig**

Every other prop family in this project comes from a rig — hulls, buildings, the wharf kit, flowers,
fish, gear. **The trees are the exception: 43 imported PNGs with no rig behind them**, which is
exactly why the masks have to be *guessed* from alpha instead of *known*.

A `treeIsoRig` would fix that at the source. Because the rigs are flat-facet renderers that already
compute per-face normals and shade from a fixed key direction (ADR 0022), a rig **knows** which
faces catch a front light and which sit on the rim — so **both mask channels become a bake output,
exact, for every tree in the set at once**, instead of a per-sprite art chore that only works on
some of them.

If you author one, please **export the two mask channels as a normal bake output** the way the boat
rigs export their cells, and say so in the rig README so the baker can be pointed at it.

*(The owner has previously said he is open to redoing the tree set — this is the argument for doing
it as a rig rather than as more sheets.)*

---

## 4. Palette — take the mechanism, not the neon

The reference is a **neon fantasy** (electric magenta, lava orange). **Ours is the restrained North
Atlantic coast**, where *one warm light in the cold* is the whole image
([`art-and-audio-bible.md`](../../design/art-and-audio-bible.md) §4.2). Same machinery, dialled to
the master ramp. The on-brand cases are a lighthouse beam raking a wharf, warm window-light at dusk,
a deck lamp picking a hull out of dark water — not a glowing forest.

---

## 5. Technical specs (unchanged from the rest of the tree pipeline)

- **PPU 32**, pixel-snapped, point-sampled. Trees are **~5.5 m** tall.
- **Pivot: bottom-centre, at the trunk.** The wind shader keeps everything below `_TrunkAnchor`
  (uv.y 0.14) planted and sways only the canopy above it, so the trunk must sit at the sprite's
  base or the sway anchors in the wrong place.
- Sheets **≤ 2048 px on every axis** — above that Unity silently *downscales* on import and the
  sprite count still matches, so only a pivot assert catches it.
- Existing trees import as `spriteMode: Multiple` with one full-rect sprite; match that.

---

## 6. ⏳ Open — the owner and lead-architect settle these, not you

1. **Mask packing.** Two channels of one texture (`_LightMask` RG), or a separate `*_mask.png`
   sibling per sprite? Affects file count and memory, not your authoring.
2. **Which sprites opt in.** Hero elements only (perf, art bible) — the full list is not drawn up.
3. **Rig-less tail.** The dense older trees either get hand-authored masks or opt out entirely.

---

**Companion briefs:** [`dory-outboard.md`](dory-outboard.md) · [`small-boat-pass.md`](small-boat-pass.md)
· [`fuel-and-fuel-storage.md`](fuel-and-fuel-storage.md)
