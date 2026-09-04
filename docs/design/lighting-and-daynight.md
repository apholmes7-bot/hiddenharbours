# Lighting & day/night — design + tuning recipe

> Companion to **ADR 0013** (the architecture decision). This doc is for the **owner** (how to art-direct
> the day) and for the **next agent** (the PR-2 projected-shadow spec). The canon look reference is
> Kingdoms Two Crowns' painterly atmospheric light (`vision-and-pillars.md` §4); the system serves **P1
> "The Sea Has Moods"** and lays the groundwork for **P5** night-sailing.

## 1. What it is

One deterministic 24-hour cycle controls the **whole game's** look. It is computed every tick as a pure
function of the clock hour + the weather, against an owner-tunable **`DayNightProfile`** asset, and applied
as a **single full-screen multiply tint** over the composited frame (so unlit sprites, tilemaps, water and
grass all darken/warm together) plus the **sun globals** (`_SunDir`, `_SunElevation`) that drive the water
specular and the projected shadows, and a **`_ShadowStrength` global** (how firmly a cast shadow reads now —
the sun being up folded with the live weather) that the projected shadows read so they soften under
overcast/storm. See ADR 0013 for *why* the overlay (short version: the
sprites are unlit and sample no 2D light, so only an output-stage tint darkens everything without migrating
every sprite).

**It self-installs.** Nothing to place in a scene. Press Play in any scene and the cycle runs.

## 2. How the owner art-directs the day (no code)

1. **Create the profile:** `Assets ▸ Create ▸ Hidden Harbours ▸ Lighting ▸ Day-Night Profile`.
2. **Save it at exactly** `Assets/_Project/Resources/DayNightProfile.asset` (the name/path is how the
   controller finds it; without it the controller uses a built-in default).
3. **Edit and watch it live.** Press Play, open the **Tide Scrubber / DevFastTide** (or set the clock's
   `TimeScale` up) and scrub the day — the screen warms at dawn, brightens at noon, goes orange at dusk,
   and dark blue at night as you move the clock.

### The tunable set (`DayNightProfile`)

| Field | What it does | Default |
|---|---|---|
| **Sky tint** (Gradient) | The whole-screen MULTIPLY colour across the day fraction (0 = midnight, 0.5 = noon, 1 = midnight). This is the main dial — paint the *mood* of each hour here. | warm low dawn → bright cool noon → orange-red dusk → dark blue night |
| **Intensity** (Curve, 0..1) | Overall brightness multiplied into the tint. Pull the night down HARD here for a darker night without changing hue. | ~0.18 at night, 1.0 at noon |
| **Sunrise hour / Sunset hour** | When the sun crosses the horizon. Solar noon sits halfway between; the sun arc + specular + (PR 2) shadows derive from these. | 6 / 20 |
| **Shadow south-bias** | How far north every shadow leans even at a low sun (the sun sits in the south). | 0.2 |
| **Shadow noon-lift** | Extra northward push at noon so the midday shadow points straight up (reads as "short, sun overhead"). | 0.9 |
| **Fog visibility for full dim** | Visibility (1 clear → 0 fog) at/below which fog fully dims the light. | 0.15 |
| **Sea-state dim start** | Sea-state fraction (Glass 0 → Storm 1) where storm gloom begins (~0.6 ≈ a Gale). | 0.6 |
| **Weather dim max** | The most weather *alone* may dim/cool the daylight — caps it so a storm at noon never blacks out. | 0.6 |
| **Overcast tint** | The cool grey the light shifts toward under cloud/storm. | (0.5, 0.55, 0.62) |
| **Overcast fades shadow** | How much full overcast erases a cast shadow (no sun → no shadow). | 0.85 |

> **Coordinates with `CottageDayNight`.** The cottage window day↔night swap (dawn 6 / dusk 19) already
> exists; keep the profile's sunrise/sunset near those so the windows light up as the global tint goes dark.

## 3. Determinism & the seam (for reviewers)

- The look is a **pure function** of `(HourOfDay, EnvironmentSample, DayNightProfile)` — recomputed every
  tick, **saved nothing** (rule 5). The pure model is `DayNightMath` (unit-tested headless,
  `DayNightMathTests`).
- Time + weather are read **only** through `GameServices.Clock` / `GameServices.Environment` (Core
  interfaces, rule 4). Nothing is written back to the sim.
- The water shader only **adds** a `_SunDir` read with a fallback to its authored `_LightDir`, so the
  owner's committed `Water.mat` look is unchanged until the controller drives the sun.

## 4. Performance (rule 7)

One overlay quad + one material, three global uniform sets on a ~10 Hz throttled tick, no per-frame
allocation, no per-sprite cost. Mobile-portable. The overlay draws above world sprites and below the
screen-space HUD (the HUD stays readable at night — you must still read the sea's mood).

## 5. Projected shadows — SHIPPED (PR 2)

A drop-on **`SpriteShadow`** component (`Assets/_Project/Code/Art/SpriteShadow.cs`) draws a **projected**
copy of a caster's sprite — darkened, semi-transparent, **skewed + length-scaled** by the sun — so the
player **reads the time of day from a shadow's angle and length** (long west at dawn → short north at noon →
long east at dusk → faded/gone at night and under heavy cloud). It mirrors `CottageDayNight`'s drop-on
pattern and consumes the controller's published globals — `_SunDir` / `_SunElevation` (the swing + length)
and `_ShadowStrength` (the alpha; how firmly the shadow reads now, folding the sun being up with the LIVE
weather) — with **no new wiring** to the controller and no per-caster sim read.

> **The weather hook is LIVE.** `_ShadowStrength` is `DayNightMath.ShadowStrength(hour, sunrise, sunset,
> WeatherDim(visibility, seaState), OvercastFadesShadow)`, computed once per controller tick where the real
> weather already is, and published as the global the shadows read. So **`OvercastFadesShadow` genuinely
> softens the shadow in-game** under overcast/storm — not just in the unit tests. (Off the cycle — a bare
> art scene with no sim — `SpriteShadow` computes the strength locally from its fallback hour with no
> weather, so the demo still shows.)

**What shipped:**
- **Pure projection maths** in `DayNightMath` (unit-tested in `DayNightMathTests`, mirroring the PR-1 style):
  - `ShadowLength(sunElevation, lengthAtNoon, lengthAtHorizon, maxLength)` — length (× the caster's height)
    that **shortens as the sun climbs** and **lengthens as it sinks**, **clamped** so dawn/dusk don't shoot
    to infinity; 0 once the sun is at/below the horizon.
  - `ShadowSkewOffset(shadowDir, sunElevation, casterHeight, …)` — the ground-plane shear offset the
    silhouette's top is laid along (`ShadowDirection × length × height`), anchored at the feet.
  - `ShadowAlpha(maxAlpha, shadowStrength)` — `maxAlpha · ShadowStrength(…)`, so it fades at night and
    under overcast (the weather hook).
- **The `HiddenHarbours/SpriteShadow` shader** (`Assets/_Project/Art/Shaders/HiddenHarboursSpriteShadow.shader`)
  — does the **shear in the VERTEX stage** driven by `_SunDir`/`_SunElevation` (+ per-renderer tunables the
  component pushes), samples the caster sprite's alpha, and outputs a flat dark silhouette. Shipped with
  `Assets/_Project/Resources/SpriteShadow.mat` so the existing magenta shader-compile guard
  (`WaterShaderCompileGuardTests`, which force-compiles every project material) covers it.
- **The component** pools ONE child shadow renderer (created once, reused — rule 7), anchors it at the
  caster's feet, sorts it just under the caster, **pixel-snaps** the anchor (toggleable), and follows the
  caster every frame with the light recompute on a throttled tick (no per-frame allocation).

**Tunables (rule 6):** the LOOK is one shipped asset — `Resources/SpriteShadowProfile.asset` (max alpha,
darkness colour, length-at-noon vs length-at-horizon, the length cap, edge softness, and the ground-contact
pool). What stays on the component is per-caster MACHINERY: sorting offset, pixel-snap + PPU, foot offset,
refresh rate, and a fallback daylight hour for scenes with no clock. The shadow **arc** (south-bias /
noon-lift / overcast-fade / sunrise-sunset) is read from the same `DayNightProfile` the controller uses.
See §5.3 for why the look moved off the component.

**How to see it / add it (owner):**
- **`Hidden Harbours ▸ Dev ▸ Build Shadow Test`** — drops a ground plane + a post, a tree, and a standing figure
  (each already carrying `SpriteShadow`) into the current scene. Press Play, scrub the clock, watch the
  shadows swing + lengthen.
- **`Hidden Harbours ▸ Lighting ▸ Add Sprite Shadow to Selection`** — batch-adds the component to selected
  `SpriteRenderer`s. This is for **trying** a caster, not for shipping one: a shadow added to a scene by hand
  is undone by the next builder run. Production casters are attached in code (see below).

### 5.1 Who actually casts

**Never hand-wired into a scene — always attached on the path the object is BUILT through**, so a rebuild
reproduces it and the owner's paint tools get it for free. The counts are pinned by
`LitDecorCasterBudgetTests` (which logs them, so CI reports the numbers without opening Unity) and the
dawn/noon/dusk/night behaviour by `SpriteShadowCastsPlayTests`.

| Caster | Attached in | Rule |
|---|---|---|
| **Trees** | `AcadianTreeCatalog.Configure` | **All of them.** The kit's smallest mature cell is 4.8 m; there is no short tree. Planter, Tree Paint Tool and prefab builder all come through this one method. |
| **Shrubs** | `StPetersWoodsPlanter.PlantShrubs` | All of them — a metre-ish mass with a real silhouette on open ground. ⚠ *All of them* means **both** island shrub passes, the ambient heath and the woodland lots' understorey: one instantiation path, one caster rule, and the budget test counts the two together. |
| **Shore plants** | `StPetersWoodsPlanter.PlantShorePlants` | The **emergent stands only**: not algae, not the subtidal fringe, standing ≥ `ShadowCasterMinHeightM` (0.6 m). 8 of 16 species. |
| **The player** | `PlayerShadowInstaller` (self-installing host) | Exactly one, covering every state — walk, iso skin, haul and rod-fight all swap the sprite on the *same* renderer. Attached from the Art lane by name, so no `Code/Player` edit (rule 4). |
| **Grass** | — | 🔴 **Never.** Thousands of tufts each pushing a sheared quad and a per-frame `LateUpdate`, bought for a shadow the size of a blade, is the rule-7 violation the caster rules exist to prevent. Asserted, not merely intended. |
| **Boats** | — (sun) / `IsoFacetHullPresentationService.Install` (lamps) | **Sun: not yet** — the hull is a mesh and its sun shadow lands on moving displaced water, its own design slice. **Lamps: every mesh hull** — `HullLampShadowCaster`, fitted where her lamps are, casts from the feature's resolved screen texture (§5.2). |
| **Wharf fittings** | `StPetersWharf.Place` | The **standers** only (bollard, pilehead — `IsStandingFitting`): their pivot is their base. The hangers (ladder, tyre) pivot at the top and would throw from the wrong end. Sun and lamp alike. |

Two things worth knowing before adding the next caster:

- **The shear is scale-invariant, so cell padding costs nothing.** The silhouette is sheared by `uv.y ×
  (length × the sprite's full cell height)`, so a figure occupying only part of its cell still lands its
  crown at `length × its own height`. No per-caster length tuning is needed or wanted.
- **⚠ The shear anchors at the cell's BOTTOM EDGE, not at the pivot.** A caster whose pivot sits a fraction
  `f` up its cell has its whole silhouette pushed `f × length × cellHeight` along the shadow direction — so
  at a raking dawn the shadow's feet stand slightly away from the caster's. This is pre-existing and affects
  every caster in proportion to `f` (shrubs 0.09–0.39, the player 0.11, trees 0.05–0.09 — the shrubs
  shipped in #428 have the largest offset). Fixing it means changing the projection for everything at once,
  which is a lead-architect call, not something to work around per caster.

**Alternative noted (not chosen):** URP `ShadowCaster2D` + a `Light2D` — needs the Sprite-Lit migration ADR
0013 rejects for now, and gives less control over the stylized skew. We ship the projected sprite.

### 5.2 Lamp-cast shadows — SHIPPED (ADR 0016, lights PR B)

The sun is one direction at infinity; a lamp is a point at a height, and that is the whole feature. Every
`SceneLight` that is lit and open at the night gate pairs with every caster inside its range, and
`LampShadowSystem` draws each pair one pooled quad with `HiddenHarbours/LampShadow`: the caster's silhouette
sheared **away from that lamp through its feet**, by a length that grows with distance and shrinks with the
lamp's height (the sun's own `ShadowLength` curve, driven by the lamp's elevation as seen from the feet), at
an alpha that is the lamp's own falloff there — so the shadow feathers with the beam's edge, fades as a
searchlight dims at a standstill, and vanishes with the lamp by day.

**It draws ABOVE the glow and MULTIPLIES.** A lamp's light is added after the day/night multiply, so a dark
sprite in the world sort would be crushed by the night and the glow added over it — invisible. The shadow
quads therefore sort at the light quads' ceiling order and win the depth tie (overlay 0.02 m < shadows
0.06 m < light quads 0.10 m in front of the camera), and `Blend Zero SrcColor` removes a fraction of whatever
light is at the pixel — quad glow, water beam, lit decor — leaving an unlit pixel untouched. The water shader
is not involved.

**Who casts:** every `SpriteShadow` (the whole §5.1 table) registers as a lamp caster automatically; every
mesh hull carries a `HullLampShadowCaster`, whose silhouette is her own pixels in `_HHHullScreenTex` filtered
by her id block — whatever she is drawing this frame casts. The wharf's standers cast (above).

**Budget:** the pool is `LampShadowProfile.MaxShadows` (24) quads; past it the nearest lamp-to-caster pairs
win; the pairing scan is O(lamps × casters) at 10 Hz and the pose follows every frame.

**Tunables (owner, no code):** `Assets ▸ Create ▸ Hidden Harbours ▸ Lighting ▸ Lamp Shadow Profile`, saved
as `Resources/LampShadowProfile.asset` — `Strength` is THE dial (0 = today's frame, byte for byte). Per lamp:
`SceneLight.CastsShadows`, `LampHeightMeters`.

**The approximation:** a skewed silhouette, one direction per caster, screen height for world height — the
`SpriteShadow` model with a point in place of the sun, not a raycast. The full statement, the sorting law and
the rejected alternatives are the PR B amendment to ADR 0016.

### 5.3 The wood's shade — SHIPPED (tree shading PR 2)

#715 (§5.4) turned the trees' sun response on; its plates then measured what the SHADOWS were doing, and
this is that list fixed. Four things, and the last three are proposals the owner rules on from the PR's plates —
each is one field in `Resources/SpriteShadowProfile.asset`, and the **code defaults are main's numbers**, so
a project with no asset renders the pre-PR frame exactly.

**1 · The dials became reachable.** Every look number used to be a `[SerializeField]` on a component that
`AcadianTreeCatalog.Configure` attaches with **no per-tree dials** — so the length of a dawn rake was a
constant in a C# file, and re-tuning it meant a code change and a re-plant. They are now one asset (the
`LampShadowProfile` pattern, guard test included).

**2 · Shadows stopped stacking.** Two crossing rakes used to darken the ground twice — measured, 7.5 % of a
wooded frame at 07:00 carried more than twice a single shadow's darkening, which is what made a stand's
floor read as a patchwork of blots rather than as shade. The shader now writes and tests the **stencil**:
the first shadow at a pixel claims it, later ones are discarded. One shade, per-caster sorting untouched, no
new pass and no buffer.

> ⚠️ **It is render STATE, so it could never have been a per-renderer dial.** A `MaterialPropertyBlock`
> feeds shader uniforms only; state comes from the material. The stencil therefore ships ON in
> `SpriteShadow.mat`, and the three `[HideInInspector]` `_Stencil*` properties are the escape hatch — a
> second material with `_StencilComp = Always` reproduces the old stacking, which is what the PR's
> before/after plate is rendered with. **Nothing else in the project uses the stencil** (asserted by a
> test, because the next feature to reach for one would break shadows silently).

**3 · A crown stopped wearing its neighbour's shadow.** A rake runs north; north is up-screen and therefore
BEHIND; and a shadow sorted at its caster's feet is drawn *after* every sprite between it and its tip — so a
neighbouring canopy wore a tree-shaped blot. `SortByFarEnd` sorts the shadow by its TIP instead, dropping it
`shadowDir.y × length × SortingBands.OrdersPerMetre` orders so it slides under everything it crosses; a tree
standing in a shadow then simply draws over it.

> This is a **trade, not a shading model**. Neither cut is right: one paints a blot on a canopy, the other
> lets a grass tuft standing in a shadow draw over it un-shaded. The honest fix is a receiver that knows it
> is in shade — **§5.5, and it is built**, behind a switch that ships off. The trade swaps a large visible
> error for a small one, and the plate is the argument.

**4 · There is shade UNDER a crown.** At noon the shear is short and runs north, so the trunk foot — the one
place you are certainly under the tree — was in full sun. A **ground-contact pool** now draws at the feet: a
circle in the quad's own uv, scaled by the component into an ellipse (`2r × casterWidth` wide, squashed by
`SpriteLightMath.GroundDepthScale` — taken from the lit path rather than restated, so the shade and the
light cannot disagree about what the ground plane is). It rides the same `_ShadowStrength`, so it fades
under cloud and vanishes at night with everything else, and it writes the same stencil, so a crown's pool
and its own rake meet without doubling. It is the runtime half of the pass-4 "root AO" upstream ask.

**⚠️ `_maxLength` was a dead clamp and now binds.** It caps the length MULTIPLIER, whose own ceiling is
`LengthAtHorizon` (5) — so the shipped 7 never clamped a caster in this game, and a mature white pine threw
**54.8 m at 07:00 and 61.9 m at 06:30**. The asset ships **3** (≈41 m for that pine); the code default stays
7. Which the game keeps is the owner's call off the rake plate.

### 5.4 The sun on the foliage — SHIPPED (owner ruling 2026-09-03)

> *"tree lighting is my concern, this should be noticable in day too with the changing sun, and shadows,
> not jsut night lighting."*

A shadow told the time of day; the thing casting it did not. The shared lit-decor response
(`Shaders/Include/SpriteLitDecor.hlsl`, §6's other half) has lit the shrubs and shoreline plants off
`_SunDir`/`_SunElevation` since #428, but **`Tree.mat` shipped at `_LightResponse 0`** — deliberately, as the
owner's call to make — so a planted forest was flat at every hour. He made the call. The dial is now **1**
and the woods read the sun.

**What that buys, measured on the pass-3 sheets.** The catch is per texel against a view-space normal, so
the crown turns as a volume rather than merely brightening: the lit region's centroid sweeps **15 px of a
269 px white pine and 30 px of a 331 px red oak** between dawn and dusk, and a crown texel facing screen-left
and one facing screen-right swap which is brighter between morning and evening. Overall catch peaks at
**10:00 and 16:00** rather than at noon — the normal sheet at work, since a mid-morning sun points nearly
along the view axis while a noon sun points up-screen.

**No canopy-special dials, and this was measured rather than assumed.** The sun catch as a fraction of a
texel's own albedo luminance at 13:00: shore plants 0.35–0.71, shrubs 0.23–0.56 (both families already
shipped at `_LightResponse 1` and accepted), trees at the same dials **0.54–0.69**. A crown is not a big
shrub to a per-texel response — it is more texels of the same shrub. Raising the strength for the canopy
would have made the woods the brightest foliage in the game. `Tree.mat`, `LitShrub.mat` and
`LitShorePlant.mat` therefore carry **identical** sun dials, pinned equal by `TreeSunLightingTests`.

**⭐ The lit side and the shadow now agree about the weather.** They already agreed about DIRECTION (one
`_SunDir`; the shadow is its exact negation). They did not agree about STRENGTH: the shadow faded under
cloud off `_ShadowStrength` while the sun catch was gated on `saturate(elevation)` alone — under the shipped
profile's heaviest storm, a lit side at **1.00** over a shadow at **0.49**. The catch now spends the same
published `_ShadowStrength`, which *is* `saturate(elevation)` with the weather folded in
(`DayNightMath.ShadowStrength`), so it is one number and not two readings of the sim.

> **A clear day is bit-identical.** `weatherDim 0` makes the weather factor exactly `1f`, so
> `_ShadowStrength` is `saturate(elevation)` to the last bit — the shrubs and shoreline plants that share the
> include render unchanged on a clear day and gain the same agreement under cloud. Asserted with `==` on raw
> floats across the whole day, not with a tolerance.

**Two things measured and left alone, for whoever picks them up:**

- **The back rim is inert on a tree.** Mask G averages 0.010–0.029 across the ten species, so
  `_SunRimStrength` contributes **0–2.3 %** of the catch and 0 % at noon. The grazing dawn/dusk rim the
  front band was tuned to buy has no baked band to steer. That is an upstream rig question (the rig bakes G
  against a fixed back light), not a material one — the dial is left at the shared value so a future rig
  pass that bakes a real rim band works with no material change.
- **`SpriteShadow._maxLength` (7) is a dead clamp.** It caps the length MULTIPLIER, and the multiplier is
  `lerp(lengthAtHorizon 5, lengthAtNoon 0.35, elevation)`, which never exceeds 5. No caster in the game
  reaches it. So a white pine's rake is unclamped: **54.8 m at 07:00, 61.9 m at 06:30, 4.8 m at noon**
  (drawn height 13.81 world units at PPU 32). Long, but drawn at the same faint `_ShadowStrength` — 0.22 at
  07:00 — that the low sun implies. Where a stand's rakes overlap the alphas stack and the wood darkens
  inside; a shared shadow buffer would fix that and is its own PR.

### 5.5 A receiver reads shaded — the shade arm (OWNER-GATED, ships OFF)

**The defect this closes.** Two systems in this project are called "shadows" and their receiver
semantics are opposite:

| | `LampShadowSystem` (§5.2, lamps) | `SpriteShadow` (the sun — trees, posts, the player) |
|---|---|---|
| what it draws | pooled quads at the compositing ceiling, `Blend Zero SrcColor` | a sheared copy of the caster's own sprite, `Blend SrcAlpha OneMinusSrcAlpha` |
| where it sorts | **above everything**, ties broken by depth | **below its caster** (`caster.sortingOrder − 1`, and `SortByFarEnd` drops it further) |
| a sprite standing in it | **is darkened** | **is NOT** — it draws over the shade at full brightness |

So a lamp shadow darkens you and a sun shadow cannot, *by construction*. That is why §5.3 could ship
"the ground under a crown is 6.1 % darker" and nothing else: a fisher standing at that trunk foot was
never going to be shaded by a dark sprite sorted underneath her.

**The fix, in one line.** `SpriteShadowProfile.ScreenSpaceShade` moves the SAME silhouettes — same
casters, same shear, same stencil, same draw count — one rung up the compositing ladder: out of the
decor band and into `SortingBands.SunShade` / `SunShadePool`, drawn with `Blend Zero SrcColor` so the
fragment multiplies the assembled frame instead of hiding under it. Every pixel under the shade then
loses the same fraction whoever drew it — the ground, the fisher standing on it, a mesh hull moored in
it. It is `LampShadowSystem`'s rung applied to the sun.

**The ladder, and why the sun's shade sits where it does** (pinned by one test, in
`LampShadowMathTests.TheDepthPins_AreOrdered_OverlayThenShadowsThenGlow`):

| rung | order | what it does |
|---|---|---|
| the world | ≤ `SortingBands.AboveDecor` | everything the player sees |
| day/night tint | `SortingBands.WorldTint` (32760) | ADR 0013's whole-frame multiply |
| **the sun's ground pool** | `SortingBands.SunShadePool` (32762) | multiply |
| **the sun's cast shade** | `SortingBands.SunShade` (32763) | multiply |
| the lamps' glow | `SceneLight.MaxSortingOrder` (32767), depth 0.10 | **additive** |
| the lamps' shadows | same order, depth 0.06 (nearer ⇒ later) | multiply |

Above the day/night tint is harmless (two multiplies commute). **Below the lamps' glow is not**: a lamp's
light is ADDED, and a tree's shadow must not dim a lantern at dusk. Sorting order alone settles that, so
the sun's shade needs no depth pin of its own — and it spends **two fixed orders and none in the decor
band**, where the legacy arm spends `shadowDir.y × length × OrdersPerMetre` per caster inside a band that
is already tight (ADR 0032).

> ⚠️ **The cost, stated rather than bounded away.** A screen-space multiply darkens whatever occupies the
> pixel, **including something that is above the shade in the world** rather than standing in it — a
> boat's upper works, a roof edge, a gull. The lamp system already accepts exactly this cost. Both arms
> are wrong in some frame: today nothing standing in a sun shadow is ever shaded; with the arm on,
> something passing over one sometimes is. **The owner picks**, which is why this ships off.

> ⚠️ **A caster is never darkened by its own cast silhouette.** Sorted under its caster the legacy arm got
> that for free; at the ceiling a tree would otherwise wear its own crown at noon. The shade arm discards
> on the caster's own opaque pixels — the same rule the lamp shader states — found in uv space through
> the vertex shear (`_ShadowUVPerUnit`, the inverse of the sprite's own texture mapping). The
> **ground-contact pool takes no such exclusion, deliberately**: it is shade lying flat on the ground at
> the feet, and the trunk foot standing in it is the one place that is certainly under the crown.

> ⚠️ **The arm is material STATE, not a keyword and not a property-block value** — blend mode is render
> state, the same wall the stencil hit in §5.3. It is two shipped materials on one shader:
> `Resources/SpriteShadow.mat` (alpha over, the shipped look) and `Resources/SpriteShadowShade.mat`
> (multiply), with `Blend [_SrcBlend] [_DstBlend]` read per material. Material floats rather than a
> shader keyword also means there is no variant for the stripper to drop out of a player build.

**What the owner can change (no code)** — one field in `Resources/SpriteShadowProfile.asset`:

| Want | Change |
|---|---|
| A receiver to read shaded at all | `Screen Space Shade` → on (ships **off**) |
| How dark a receiver reads | `Max Alpha` (0.45) — it is now the SAME number for the ground and for what stands on it |
| Nothing standing in a shadow to be shaded, as today | leave `Screen Space Shade` off |

**Night is untouched either way.** The sun's shade rides `_ShadowStrength` (`saturate(elevation) ×
weather`), so at sunset the alpha reaches 0, the renderer is disabled, and both arms are the same frame.
The lamps and their shadows are not touched by any of this.

## 6. Night lights — additive 2D lights + the boat spotlight — SHIPPED (ADR 0016)

The multiply overlay darkens uniformly; it cannot by itself let a lantern/boat-light punch a bright hole in
the dark. ADR 0016 adds that hole: a **light** is an **ADDITIVE glow drawn ABOVE the day/night overlay** that
ADDS brightness back into the crushed-dark frame — the first concrete one being a **boat spotlight**. This is
the **payoff** of the day/night system (P1 night-as-a-force, P5 night-sailing risk) and the start of the
owner's M2/M3 night-lighting vision.

### What it is

- **An additive glow above the overlay.** Each light draws a soft CONE/RADIAL quad at `sortingOrder ~32770`
  (above the overlay's ~32760, below the HUD) blended `One One`, so it brightens the darkened world (not the
  HUD). Visual-only — drives no sim, saves nothing (rule 5).
- **It auto-gates to night IN THE SHADER.** The `HiddenHarbours/AdditiveLight` shader reads the published
  `_DayNightTint` and scales its output by the frame **darkness** (`≈ 1 − luminance(tint)`): a light is
  ~invisible at a bright noon (so it can't wash daytime out) and full in a dark night — with **zero per-light
  coupling to the cycle**. Drop a light anywhere; it fades with the day on its own. (Off the cycle — EditMode /
  a bare art scene / the demo before Play — the tint is unset/near-black; the gate then **shows** the light, so
  you can see + tune it. Tunable via `_GateFallback`, mirroring the water shader's unset-tint handling.)

### The components

- **`SceneLight`** (`Assets/_Project/Code/Art/SceneLight.cs`) — the reusable drop-on light. Shape (Cone beam /
  Radial halo), colour, intensity, range, cone half-angle, edge + angular softness, the night-gate, optional
  **deterministic** flicker (a `(seed, time)` hash, never `System.Random` — rule 5). Pooled (one child quad +
  one shared `Resources/AdditiveLight.mat` via a `MaterialPropertyBlock`), no per-frame alloc (rule 7); the
  heavy shape runs on the GPU.
- **`BoatSpotlight`** (`Assets/_Project/Code/Art/BoatSpotlight.cs`) — the first concrete light. Configures +
  carries a `SceneLight` **cone**: warm, soft, thrown forward off the **bow** onto the dark water, that
  **follows + rotates with the hull** and **dims toward off when not making way** (a working searchlight under
  way; a faint glow when moored — tunable, with a floor). It reads the boat through **Transform only** (heading
  = its own `transform.up`, the bow anchor = a local forward offset, the way-gate = its own measured speed) — no
  reference to the Boats module (rule 4).

### The tunable set (rule 6) + defaults

| Tunable | Where | Default |
|---|---|---|
| Shape (Cone / Radial) | SceneLight | Cone (BoatSpotlight) |
| Colour | SceneLight / BoatSpotlight | warm amber `(1, 0.88, 0.62)` |
| Intensity | SceneLight / BoatSpotlight | 1.5 (spotlight) |
| Range (throw, m) | SceneLight / BoatSpotlight | 9 (spotlight) |
| Cone half-angle (deg; 180 = radial) | SceneLight / BoatSpotlight | 26 (spotlight) |
| Edge softness (radial fade) | SceneLight / BoatSpotlight | 0.6 |
| Angular softness (cone edge) | SceneLight / BoatSpotlight | 0.45 |
| Bright core boost | SceneLight | 1 |
| Night-gate darkness threshold | SceneLight / material | 0.12 |
| Night-gate fade band | SceneLight / material | 0.35 |
| Show-when-no-cycle fallback | SceneLight / material | 1 (show) |
| Flicker amount / speed | SceneLight / BoatSpotlight | 0.06 / 1 (spotlight) |
| Bow offset / side offset | BoatSpotlight | 0.6 / 0 |
| Dim-when-stationary, full-speed, floor | BoatSpotlight | on, 1.2 m/s, 0.15 |

### How the owner SEES it / ADDS it

- **`Hidden Harbours ▸ Dev ▸ Build Light Test`** — drops a DARK ground plane + a boat-marker carrying a forward CONE
  spotlight + a round RADIAL lantern into the current scene. Press Play, **scrub the clock to NIGHT** (Tide
  Scrubber / DevFastTide / raise the clock `TimeScale`), and watch the beam + halo **cut through the dark**.
  Delete the spawned `LightTest` object to fully revert.
- **`Hidden Harbours ▸ Lighting ▸ Add Light to Selection ▸ Spotlight (boat)`** — adds a `BoatSpotlight` to the
  selected object(s) (drop it on the boat). The other sub-menu entries (Worklight / Window Glow / Lightpost)
  attach a `PreconfiguredLight` carrying the matching `LightPresets.Kind`.

### The placed lights (built)

The follow-up types are no longer stubs. `PreconfiguredLight` + the `LightPresets` library are the owner's
2026-07-05 principle in code: *lighting is AUTOMATIC; the exception is a light SOURCE, and some objects come
PRECONFIGURED with one.* Drop the component on a prefab, pick a `Kind`, and the object carries its own
night-gated pool with no wiring — the gate is in the shader, off the published `_DayNightTint`, so nothing
reads the clock.

| Kind | Look | Reach | Placed on |
|---|---|---|---|
| **WindowGlow** | warm amber spill, faint hearth flicker | 3.4 m | Aunt Ginny's cottage |
| **Lightpost** | warm sodium pool, barely-there hum | 3.6 m | `lanternPost` (2.46 m), `streetLamp` (4.48 m) |
| **Worklight** | near-white, rock steady | 5.2 m | — (a lamp on a wall; nothing yet) |
| **Floodlight** | cool electric flood, steady | 7 m | `yardLight` (7.26 m), `floodMast` (7.8 m) |

**The split is by HEAD HEIGHT**: the two low posts pool warmly, the two tall poles flood. A piece's height is
not a taste call — it is read from the ISO pack's published `heightM` and written to
`SceneLight.LampHeightMeters`, because the lamp's height is what sets the length of every shadow it casts
(§6.3). Left at the 2.5 m default a 7.8 m flood mast lights a yard like a mast and shadows it like a bollard.

⚠️ **Both pools are deliberately smaller than the physical rule.** A lamp lights a circle of roughly twice its
head height, so a 4.48 m street lamp should pool ~4.5 m — and at that size ADR 0016's additive quad, which is
the source's own *bloom* rather than illumination, saturates to a flat disc that hides the very ground it is
lighting (measured, `docs/art/spikes/land-lamp-posts/`, plates 05–07). The sizes above are what READ. Making a
lamp light the ground the way §5 makes the sun light a tree is the standing follow-up.

### Lamps on the land (`LampPosts`)

**A hand-placed light is undone by the next Build.** The regions are rebuilt from their builder scripts, so a
lamp that is not in a builder is a lamp that exists until somebody presses the button. `LampPosts`
(`Code/App/Editor/`) is the one place a lamp post becomes a GameObject — sprite + `YSortSprite` +
`PreconfiguredLight` + `SpriteShadow` — and the region files own the POSITIONS, beside the geometry they are
derived from.

- **St Peters** — two `lanternPost`s on the pier's north half, on the row `StPetersWharf.LampRowY` DERIVES
  from the preset's reach: as far back from the working edge as the pool can afford while still covering it
  (the mooring gear is all on the south lip, and a post among it is a post in the way of a line). The head
  lamp takes the ladder's own x. It does not reach the moored dory, which carries her own anchor light.
- **Nine Mile Creek** — two `streetLamp`s at the FRONT of the quay's gear band (`LampRowY`), the closest to
  the mooring edge that `WorkingStripMetres` lets anything stand: on a 10 m quay no row reaches both the
  berths and the yard, so the lamp takes the berths. Two more in the GAPS of Wharf Road's
  pole line (half a pole-spacing from their neighbours: a lamp goes where the wire is, never *on* a pole), a
  `floodMast` off the laydown pavement, a `yardLight` at the Route 91 forecourt, and #462's yard light at the
  wharf entrance — which had described itself as *"the only lit thing out here at night"* since it was placed
  and emitted nothing at all.

**Varied, not regular.** `municipal-infrastructure.md` §3.4's acceptance test is negative: *if the island reads
REGULAR at night the slice is wrong.* So 322 m of Wharf Road gets two lamps and 84 m of quay gets three, with
dark between them.

**Off the water bridge by construction.** `WaterLightBridge` takes the four nearest `IWaterLightEmitter`s and
only `BoatSpotlight` implements it, so a wharf's posts can never evict the searchlight from the water's four
slots. There is no opt-out to remember because there is no opt-in.

**⚠️ What a placed lamp does NOT yet do: throw a shadow off the wharf's gear at Nine Mile Creek.** The
2026-09-03 charter's acceptance asked for *"the moored boats and bollards throwing shadows from them"*, and at
NMC that is **not delivered** — measured 0 of 24 pooled shadows at 02:00. The reason is structural and has
nothing to do with the lamps: `StPetersWharf.IsStandingFitting` gives that pier's bollards and pileheads a
`SpriteShadow`, and **no Nine Mile Creek builder makes any quay fitting a caster at all**, so there is nothing
on that quay for a lamp to throw — nor anything casting a sun shadow by day. The moored fleet's hulls lie
further from a lamp than the pool reaches. Making NMC's gear cast is a change to that region's daylight as
much as its night and belongs to its own PR.

**⭐ The carrier rule.** A lamp post is the first object in the game to carry a light and a shadow CASTER on one
GameObject, so its lamp-to-feet distance is just the light's origin offset — the smallest in the scene.
`LampShadowSystem` keeps the nearest pairs globally, so without a rule every post would sort to the front of
the pool and spend one of its 24 slots throwing a stub of itself at its own foot. A lamp therefore never throws
the silhouette of a caster on its own GameObject. (A light on a CHILD of its carrier — a walker's headlamp —
is not covered and wants an ancestor walk from the PR that introduces it.)

**⚠ And a lamp post takes no ground-contact pool.** `SpriteShadowProfile`'s pool is gated on the caster's drawn
HEIGHT (3 m), which stands in for *has mass overhead* — true of the trees it was measured on, false of a thin
pole that clears the gate with no crown. Three of the four pieces clear it, so `SpriteShadow.CastsGroundContact`
(per-caster, default on) is off for lamp posts: a shade disc under a lamp is a shade disc on the one patch of
ground the lamp is there to light.

### 6.1 The fleet's lamps, and the rule of the road — SHIPPED (ADR 0016, boat-lights PR 1 + PR 2a)

A boat carries LAMPS as well as a searchlight: her port and starboard sidelights, her stern light, her
masthead, the warm spill out of her wheelhouse, and — when she is lying still — an anchor light. All of it
is data-driven and self-installing; no scene wires a lamp anywhere.

- **`HullMeshDef.Lamps`** (Core) — per-hull rows of `{Kind, RigLocalMetres, IntensityScale}`. The KIND is
  fixed vocabulary (`HullLampKind`, append-only); only the POSITIONS are per-hull. Red to port and green to
  starboard is the rule of the road, not a tunable, so a hull cannot declare it the other way round.
- **`BoatLampPresets`** (Art) — one look per kind, in one place. This is where a sidelight's red lives.
- **`BoatLamps`** (Art) — self-installed by `IsoFacetHullPresentationService` on any mesh hull whose def
  declares lamps, in every region. Absence is data: a hull with no lamps gets no component at all.
- **`BoatSpotlight`** (Art) — the searchlight, on the boat ROOT (see §6 above).

**Which hulls.** Twenty-seven, being every hull whose rig publishes `navMounts` — the Cape Islander, the
lobster boat and her eighteen variants, the side dragger, both stern trawlers, the coastal packet, the
tanker and both sport fishers. The open boats (dory, punt, console skiff, both sport skiffs, both zodiacs)
publish no mounts and carry no lamps, because an open boat has nowhere to bolt one.

**Where the numbers come from.** Not from a screenshot and not from a constant in C#: each triple is
derived from the hull's own rig by `BoatLampAnchorProbe` and pinned by `BoatLampAnchorTests`, which pushes
the shipped def through the runtime's projection and demands it land on the pixels the rig itself draws, at
all eight facings. Re-run the table with **`Hidden Harbours / Rig Baking / Probe: boat lamp anchors`**; it
prints, it never writes.

**The regime (what the owner will notice).** A boat that is lying still shows **an anchor light and her
cabin, and nothing else**; a boat under way shows **her sidelights, stern light and masthead, and no anchor
light**. That is the rule of the road, and it is why the seven boats moored along the Nine Mile Creek wharf
read as a fleet asleep rather than a fleet getting under way. A hull says which she is through Core's
`IVesselWay`; **a hull that says nothing is under way**, which is what every boat did before the regime
existed.

**Whose switch is the L key.** The searchlight answers the key on **the boat the player is standing on** —
at the wheel or on her deck — and on no other, so reaching for your own light does not flip a skipper's two
berths down. An NPC's beam follows her way instead: lit while she is running, out at her berth.

#### The tunables PR 2a added (rule 6)

| Tunable | Where | Default |
|---|---|---|
| Anchor light colour / intensity / range | `BoatLampPresets` | white `(1, 0.96, 0.88)` / 0.8 / 0.75 m |
| Range light (the second masthead) | `BoatLampPresets` | the masthead's look, verbatim |
| Sidelight radius | `BoatLampPresets` | 0.28 m — **bounded** by the tightest sidelight pair in the fleet (the cape's 0.6048 m); the preset test measures that bound off the shipped defs |
| Cabin-glow occupied boost | `BoatLamps` | 1.5x while somebody is below |
| Per-placement trim | `HullMeshDef.Lamps[].IntensityScale` | 1 (= the preset) on every hull today |
| Master switch for a hull's glows | `BoatLamps.LampsOn` | on |

### 6.2 A lit cabin is drawn as its WINDOWS — SHIPPED (ADR 0016, boat-lights PR 2c; owner ruling 2026-09-03)

> The glows should be constrained to their space, if its interior it should be confined to the cabin with
> the glow only coming through the windows.

The cabin glow used to be a 1.5 m amber disc laid over the deck around the house. It is now two things, and
the disc is gone:

- **her glass, lit** — one additive mesh per hull, a quad per pane of glass her own rig publishes, drawn
  from the four PROJECTED corners so a window is the right shape at every heading;
- **a wash off each glazed wall** — one soft cone per wall, aimed outward through the hull's posed frame,
  reaching **1.4x the width of one of that wall's windows** (not of the wall: five portholes over 6.8 m of
  a tanker's accommodation must not add up to a floodlight).

**The far side of the house culls itself.** A wall turning away foreshortens to nothing, hits zero area
edge-on and then shows the viewer its inside — so the panes on it are dropped exactly there. Nothing to
fade, nothing to pop, and no amber crossing her own roof.

**Where the light is drawn matters.** These quads sit ABOVE the day/night multiply with the lamps. That is
not a preference: the multiply is a whole-frame operation at sorting order ~32760 and a mesh hull draws well
below it, so the brightest colour anything in her own pass could take comes out as the night tint itself
(luminance ≈ 0.17 at 02:00). Making her `glas` emissive would have produced a paler dark-blue rectangle, not
a lit window.

**The navigation lamps came down to the size of their own fittings** in the same ruling — masthead 1.35 →
0.50 m, stern 1.00 → 0.40 m, anchor 0.75 → 0.34 m, with the intensity carrying what the radius gave up. The
sidelights did not move: they were already bounded by the gap between them. Their ORDER is unchanged, which
is the part that carries meaning.

**Two hulls keep the disc**, and it is named rather than hidden: the sport fishers' rigs publish a flat
half-width for a side that curves in plan, so their portholes cannot be placed on the boat from the record
alone. The probe refuses them, and `BoatLamps` falls them back to the glow they already had — degrade to
the shipped look, never to an invisible boat. The fix is upstream.

#### The tunables PR 2c added (rule 6)

| Tunable | Where | Default |
|---|---|---|
| Restore yesterday's glows (the A/B, and the way back) | `GameConfig.BoatLegacyCabinGlow` | **off** — the confined look ships |
| Lit-pane brightness | `BoatWindowGlow.PaneIntensity` | 0.72, over a night frame of ≈0.17 luminance |
| How far a wall washes | `BoatLampPresets.WallSpillWindowMultiple` | 1.4 x one window's width, clamped to 0.50–1.60 m |
| Wash cone half-angle / edge | `BoatLampPresets` | 45 degrees, 0.85 angular softness, **zero core** |
| A hull's windows | `HullMeshDef.Panes` | derived by `BoatWindowProbe` from her rig; game-side, survives a re-bake |
| When a pane is too far off her hull to place | `BoatWindowProbe.OnHullToleranceMetres` | 0.30 m (worst kept 0.203, best refused 0.453) |

**How the owner sees it:** `Hidden Harbours/Rig Baking/Probe: boat windows (print the table)` prints every
window in the fleet with its position, size and the way it faces; the writer beside it puts them into the
defs. Flip `BoatLegacyCabinGlow` on the `GameConfig` asset to see yesterday's picture in the same build.

## 7. Migration to true URP 2D lights (still open)

If a future need outgrows additive sprites (e.g. real occlusion/shadow-casting lights), ADR 0013's path (2)
remains: migrate the relevant sprites to Sprite-Lit and drive a `Light2D` from the same `DayNightProfile`. The
**durable model** (`DayNightProfile` + `DayNightMath` + the published globals + now `LightMath`) carries over;
only the *output stage* changes. Decide if/when that need arrives.
