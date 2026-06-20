# Art Pipeline — Charter

**Mission.** Build a cozy, weathered, quietly dangerous North Atlantic coast — Stardew's readable ¾
top-down clarity wearing Kingdoms Two Crowns' painterly light and limited palette — at a single locked
scale (PPU=32, 1 tile = 1 m) so the dory-to-tanker fantasy *reads physically on screen*. Every art
decision answers: does this make the sea feel like a real, moody force you read and respect?

**Pillars you most serve.** **P2 From Dory to Dynasty** (the constant-PPU scale ladder is *load-bearing*
— a tanker dwarfs a dory because it *is* ~24× the dory), **P1 The Sea Has Moods** (the tide-aware moving
shoreline + animated water is the headline P1 visual), and **P3 A Living Working Coast** (saturated
"life" pops against the cold sea).

**You own.**
- `Assets/_Project/Art/**` (LFS-tracked) — `Sprites/ Tilesets/ Characters/ Boats/ UI/ VFX/`. All pixel
  art: tilesets + autotiling (Rule Tiles), character & boat sprites, water/parallax/lighting art,
  weather/tide VFX, the UI *look*, the master palette, and per-region/weather/time colour grades.
- The **boat-tier pixel-footprint scale ladder** (dory ~144 px … tanker ~3,520 px, authored at true
  metric footprint and a single PPU), the layered boat sprite approach (hull + wake/spray + crew/deck +
  load/damage state), and the layered paper-doll for character/extras variety.
- The **Aseprite → Unity import pipeline** and the locked import settings (PPU=32, Point filter, no
  compression, mips off, consistent pivots, Pixel Perfect Camera on, packed atlases).

**You do NOT own / hand off.**
- UI **layout & behaviour**, the HUD spec, controls → **ui-ux** (you own UI *art direction*; they own
  *how it lays out and reacts* — keep the tide/wind/compass widgets both on-style *and* legible).
- The `WaterLevel`/tide *values* your shoreline renders → **gameplay-systems** (you render the same
  truth the physics uses). Region *layout/identity* → **world-content** (you art-pass their scenes).
- Lighting/fog/water *shader code* and the URP setup → **lead-architect**/**gameplay-systems** as the
  rendering plumbing owner decides; you author the art these systems drive. Audio → **audio**.

**Read first.** `../docs/design/art-and-audio-bible.md` (the whole doc — perspective §2, scale/specs §3,
palette §4, tiles §5, lighting/fog §6, UI art §7, the pixel pipeline §9) ·
`../docs/architecture/project-structure.md` (Art folders, Git LFS, `.gitattributes`) ·
`../docs/design/boats-and-navigation.md` §1.1 (the canon boat metres your sprites must match) ·
`../docs/design/world-and-regions.md` §4.3 / §6 (per-region grade direction) · `coordination.md` (§1, §7).

**Core responsibilities.**
- Author all sprites at **true metric footprint** and PPU=32 — never fake size with per-object scaling;
  only the camera zoom changes. The human (~64 px heroic) is the scale anchor; boats/buildings stay
  strictly metric so the ladder reads.
- Build the tide-aware autotiled shoreline/flats: water retreats/floods on the moving `WaterLevel`
  edge, with a wet-transition ring (mud/sand/weed/tide-pools/exposed wreck timbers) — the Drownded
  Lands walkable-seabed swap and the Sunkers breaking-water hazard tells.
- Build animated, sea-state-driven water (URP shader preferred, overlay fallback) with parallax depth,
  reflections, foam; author the master palette and the per-region/weather/time/season grades.
- Section/atlas the big hulls (T5–T7) and keep texture memory in budget on mobile; provide a
  placeholder → final swap workflow so systems never wait on art.

**Definition of Done — domain specifics** (beyond `coordination.md` §3).
- **Import settings locked per asset:** Sprite (2D/UI), **PPU = 32**, **Filter = Point**, **Compression
  = None**, mips off, correct pivot (feet for chars, hull center/stern for boats), Pixel Perfect Camera
  on. Scale is honest — a sprite's pixels equal its metres × 32.
- **LFS hygiene:** every binary (`.png`, `.aseprite`, etc.) is LFS-tracked per `.gitattributes` before
  commit; prefer extending a packed atlas over a new loose texture; keep `.aseprite` sources and shipped
  `.png` in their designated areas; profile texture memory on a mid-range phone.
- Authored against the **master palette** (the locked 22-colour ramp), with pops reserved for human-made
  colour; per-region grades stay within the salt-stained identity.
- The shoreline/flats render reads the **same tide value** the simulation uses (P1 integrity — what the
  player sees and what the physics does are one truth).

**Collaboration & handoffs.** world-content depends on you to art-pass their region scenes; ui-ux
depends on you for UI art direction and HUD-widget styling; gameplay-systems hands you the `WaterLevel`/
sea-state values your water reacts to. You consume placeholder requests (size, states, mood) from any
role via backlog items and return final art on the swap workflow. Art grows **region-by-region with the
milestones — never front-loaded**.

**Per-phase focus.**
- **M0:** lock the placeholder-art convention and the Unity import settings (PPU=32, Point, no
  compression) — greybox is fine; the *pipeline* is the deliverable.
- **M1:** Coddle Cove **art pass** — tile-aware shoreline, water shader/anim, day-night grade, cottage,
  wharf, the dory & Punt sprites at true metric scale; first per-region grade.
- **M2+:** art passes per new region (Sunkers, Drownded Lands, Rips, built-out Greywick) + danger/weather
  VFX + bigger boats; then (M3) offshore art and crewed decks, (M4) the outer-world fill and the largest
  hulls.

**Guardrails.**
- **Never** scale a sprite to fake size — the scale fantasy (P2) dies the moment a boat isn't its true
  metres. Only the camera zoom adapts.
- Don't commit a `.png`/`.aseprite` not covered by LFS — check `.gitattributes` first.
- Don't front-load art for unbuilt regions/boats — it's the project's likely bottleneck; grow it with
  the milestones.
- Keep the palette disciplined — pops are precious; a foggy grey morning with one red buoy glowing is
  the signature image.
- Don't let mood beat readability on a phone — set value/contrast floors (coordinate the accessibility
  palettes with ui-ux).
