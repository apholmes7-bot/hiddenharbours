# UI text review — passes 1 and 2

Every piece of text the player reads, and how it is drawn: the Notebook, the HUD, the chooser,
shop and shelf prices, the paper bag, bubbles, prompts, the KIT tab, menus, settings, the
creator, and the title screen.

Pictures live in `docs/design/ui-text-review/`. Each is drawn from the game's own baked glyph
atlas at whole-pixel scale, so they show the real letter shapes. In-game captures at
1920x1080 and 1280x720, before and after, were taken for the review and are attached to the pull request discussion, not kept in the repo.

---

## The top ten

**1. Bubbles draw at uneven pixel sizes at 1920x1080.**
Picture: `01-bubble-scale-1080p.png`.
The dialogue and passer-by bubbles scaled with the window (1.5x at 1080p), so one letter pixel
became 4 or 5 screen pixels in turn. Letters looked smeared and uneven.
**Fixed:** the bubble canvases now round the window scale down to a whole letter pixel.

**2. The pixel face has no ₲ and no ▸.**
Picture: `02-missing-glyphs.png`.
Prices on the shelf, catalogue and sale screen ask for ₲, and the dialogue option cursor asks
for ▸. Neither is in the face, so they draw as nothing.
**Owed:** two new glyphs in the face and a re-bake (art). A test names both gaps and fails
once they are filled, so the list cannot go stale.

**3. Two typefaces side by side.**
Picture: `03-two-typefaces.png`.
The Notebook, bubbles, chooser and catalogue use the pixel face. The HUD, tide panel, sale
screen, wardrobe, arrival title and the title/pause/settings menus use a smooth system font.
Next to pixel art the smooth font reads as a different game.
**Taste pick 1** below.

**4. Money was written three ways.**
Picture: `04-words-before-after.png`.
"₲ 1,800" (with a space) in the catalogue, "₲1240" (no comma) at the home door, and "₲1,240"
elsewhere.
**Fixed:** the catalogue, sale screen and home door all use the one money format: `₲1,240`.

**5. Faint ink was too faint.**
Picture: `05-contrast.png`.
Done steps, greyed chooser rows, page marks and the quest note's label sat at 2.5:1 on the
paper. Small pixel text needs 4.5:1.
**Fixed:** faint ink is now 4.9:1. It is still lighter than full ink. The tide panel and
wardrobe use the same weight now.

**6. The tide table showed a raw id and counted days from 0.**
The heading named the region by its id, and the first tomorrow read "day 1".
**Fixed:** it names the place by its display name and counts from day 1, as the title page
does.

**7. Raw ids and dev words reach the player.**
The catalogue head shows "seller.nmc_chandlery". A fish with no page falls back to its id.
Creator fields fall back to field names. Some toasts say "(dev)", "wired", "ADR 0023",
"Lid ON/OFF". A dash reads "STANDBY · OPTIONAL". Two water names still say "(working name)".
**Owed:** sellers need a display name field (content). The dev toasts need a dev-only gate.
Region names are the owner's call.

**8. Key names are written several ways.**
"E" appears five ways. Both "Esc/B" and "Esc / B" are used. The creator hint said
"Left/right" where the keys are A/D.
**Fixed in the creator.** The rest is owed to one prompt table (pass 2).

**9. Spelling and word drift.**
"License" and "Licence", "Woah", "Randomize", a hull id in a shop note, and " - " for a dash.
Some code comments called the player "she".
**Fixed:** Licence, Whoa, Randomise, "Fits to your boat.", em dashes, and "he" in comments.
**Owed:** pail or bucket? (one word, owner's call).

**10. Case is mixed.**
Title Case, Sentence case and ALL CAPS sit on the same screens.
**Proposed:** Sentence case everywhere, except a place name on arrival (see the style below).

---

## Where each surface stands

| Surface | Face | Scale | Whole pixels? |
|---|---|---|---|
| Notebook | pixel | fixed whole | yes |
| Quest note | pixel | fixed whole | yes |
| Catalogue | pixel | own whole | yes |
| Dialogue bubble | pixel | window, snapped | yes (fixed) |
| Passer-by bubble | pixel | window, snapped | yes (fixed) |
| Chooser, popup | pixel | window | no, owed |
| Creator | pixel | window, 1080 ref | yes at 720/1080/1440 |
| HUD, compass, wind | system | window | n/a |
| Tide panel | system | window | n/a |
| Sale screen | system | window | n/a |
| Wardrobe | system | window | n/a |
| Arrival title | system | window | n/a |
| Title, pause, settings | system | window | n/a |
| Dev toasts | system | window | n/a |

The chooser and popup place themselves from the HUD band's scale. Snapping them changes their
layout, so that waits for pass 2. A test lists every pixel-face canvas that scales with the
window, and fails if a new one is added without being snapped or named.

The HUD wind barbs use ▮, which the system font may not have. Check it in a capture.

---

## Proposed text style

**Face.** One pixel face for everything the player reads. It is 4x8 with a 5-pixel advance and
a 10-pixel line. A second size, if wanted, is the same face at 2x, never a different font.

**Sizes.** Only whole multiples of the face: 1x for body, 2x for headings and the title.
Never a size between.

**Scale.** One letter pixel is always a whole number of screen pixels. A canvas that scales
with the window snaps its scale.

**Colour on paper.** Strong ink for titles. Ink for body. Faint ink for done and greyed,
never under 4.5:1.

**Colour over the world.** Chalk on a dark scrim, or chalk with a 1-pixel dark outline. Never
bare text over the sea. The day sea is the worst case.

**Outline rule.** Over the world: a 1-pixel outline, on the pixel grid. On paper: no outline,
no shadow.

**Notebook pages.**
- One heading per page.
- Rows on the ruled lines.
- Numbers right-aligned in their column.
- An empty page says so in plain words, never a blank.

**Money.** `₲1,240`. Thousands get commas. No "g", no "coins".

**Days.** Counted from 1, everywhere.

**Time.** 24-hour `06:40`, the same on the HUD and in the tide table.

**Keys.** One name per key, one style: `E`, `Esc`, `Tab`, and the pad button as `A`, `B`.
Written as "E: talk", with a colon and no brackets.

**Case.** Sentence case. A place name on arrival may use capitals.

---

## Taste picks — decided

The owner picked A for all three.

1. **One face everywhere.** The title, pause and settings pages, the tide table, the sell screen,
   the arrival card and the toasts now set in the pixel face. Sizes are whole multiples of 24
   (glyph height 8 times the kit's scale 3) on a snapped canvas, so no letter is resampled.
   Not yet on the face: the HUD, the wardrobe panel and the deck freshness tag (see Still owed).
2. **Title layout: wordmark top left.** This is the layout already in the game. The wordmark is
   still type, not drawn art.
3. **Notebook paper: ruled.** This is the paper already in the game. No change.

---

## Pass 2 — what changed

**No ids, no developer words.**
- Sellers have a display name in their data. The catalogue head shows it, never the id.
- A Notebook entry with no fish name falls back to a plain name made from the id, never the id.
- The creator's field names fall back the same way.
- Toasts lose "(dev)", "not wired" and "Lid ON/OFF". The dash screens say NOT FITTED, not
  "STANDBY · OPTIONAL".

**One key table.** Every prompt names its key through `KeyNames` (Core): E, Esc, Tab, Enter,
the pad's A, B, X, Y and Start, and the paired forms Esc/B, Enter/A, Arrows/stick. A test fails if a
prompt spells a key another way.

**Bucket, never pail.** Every line the player reads, display names in data included. Ids keep
their old names; ids never change.

**Sentence case** where a line was plainly out of step with its neighbours: "Tide table".

**The face gains nine glyphs:** ₲ ▸ ◀ ▲ ▼ · × ° •, drawn in the face's own 4x8 cells.

**Tests** (`UiTextReviewTests`): no id shapes or developer words in player strings, no second
spelling of a key, no "pail" in strings or in the data the player reads, every seller named, the
key table uses only the face's letters, and the pages on the face use only its glyphs.

---

## Strings still in code

Content is data. These are still written in C#. They are listed here, not moved.

Small and safe to move into the existing strings tables:
- the HUD and its number formats
- the compass and wind readouts
- shelf goods
- Notebook page text
- fishing and bucket toasts

Medium: a new economy strings table for the shop, catalogue and sale screen.

---

## Still owed

- **HUD glyphs.** The HUD needs arrows and marks the face does not have: the eight compass and wind
  arrows, ⤴ ▮ ▪ ○ ↗ → ▾ and ~. The HUD stays on the system font until they are drawn.
- **Wardrobe panel.** Its rows are sized for a 16 px system font; at the face's 24 they need a new
  layout. Its [ ] marks also need glyphs.
- **Deck freshness tag.** A world-space label; it needs its own pixel sizing before it can wear the face.
- **Dash rig text.** The rig sources for both dash consoles still say STANDBY; the game draws NOT FITTED.
- **Wordmark art.** The title wordmark is still type.
- **Tide table close hint** runs past the page edge in the face at 24; it needs a shorter line or a wider box.
- A dev-only gate on dev toasts.
- Chooser and popup scale snapping (layout).
- The two "(working name)" waters.
