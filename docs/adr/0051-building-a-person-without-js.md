# ADR 0051 — Building a person without JS: the ten baked bodies, recoloured from the rig's own tables

- **Status: PROPOSED** (2026-10-07). Written by character PR 2b's lane
  (`feat/character-builder-rig10`, `lead-architect`) on rig 10.3. The owner accepts it by merging,
  as the PR 2 charter's B1 says. Cells marked **Phase B** were measured in the PR's editor run
  (Unity 6000.5.0f1, batch mode, 2026-10-17). The node numbers beside them are the forecast they
  were checked against.
- **Date:** 2026-10-07
- **Decision owner:** the owner, by merging. `lead-architect` owns what it adds to Core
  (`CharacterBuilder`, `CharacterRecipe`, `IPlayerAppearance`) and save version 15.
- **Serves:**
  - **P3 A Living Working Coast:** the player is one of the coast's people. She is built from the
    same rig, the same ten bodies and the same palette as the people around her.
  - **P5 Cozy, but with Teeth:** choosing how you look is part of the comfort. A look costs
    nothing a frame can feel, so the danger stays where it belongs, on the sea.
- **Applies, amends nothing:**
  - [ADR 0021](0021-in-engine-js-rig-baking.md): the rig runs in the editor's V8, and no JS ships.
  - [ADR 0029](0029-character-colour-runtime-structure-baked.md): colour is runtime and structure
    is baked. This ADR is that rule's builder.
  - [ADR 0003](0003-data-driven-content.md): the tables are assets, one entity per file, with
    append-only ids.
- **Related:**
  - [ADR 0044](0044-characters-are-meshes.md) §9.3: the player's figure key is the empty string,
    so nothing here carries or saves a figure key.
  - #923: rig 10.3's kit and the ten baked defs this builds on.
  - #914: PR 2a.

---

## 1. The question

At run time, with no JS (ADR 0021), the game must turn "who the player is" into a body on screen.
The 09-23 charter put three candidates:

- **A.** a C# port of the rig's build;
- **B.** parts pre-baked from the rig and assembled at run time;
- **C.** a mix of the two.

Two owner rulings bound the answer:

- **09-24, ruling 2:** children are built for the cast only. The player is never offered one.
- **10-01, for rig 10: presets only.** The builder builds the ten core presets that the bake bakes,
  in the rig's colour options:
  - fisher, ginny, skipper, nan and deckboss;
  - packer, cutter, hand, boy and girl.

  There are no NPC bakes, no creator and no random build.

## 2. What the rig does, and what that leaves

Rig 10.3 lives at `docs/art/rigs/character/rig10/Art/characterIsoRig10.js`: 1,408 lines, sha256
`ad563075475740cea1258de68d05c184f2fc136491357bc22988a10d810413f3`. It builds a person from twenty
`FIELDS`:

- **13 structural fields:** shape, age, height, weight, head, hairStyle, beard, eyeShape, garment,
  hat, bottom, sex and frame.
- **7 colour fields:** skin, hair, eyes, outfit, shirt, hatCol and apronCol.

Three facts, all measured in node 24 on the rig's unmodified source:

- **Colour never reaches structure.**
  - Over the ten presets and every one of their 620 single colour changes, the bind mesh and the
    skeleton never change.
  - Over one change per colour field per preset (70 builds), with the build's own name stripped,
    the clips and the gameplay sidecar never change either.
  - So the seven colour fields feed only the materials.
- **A material's colours come from a small, fixed set of colour fields.** The rig has 34 material
  roles:
  - 19 roles are one colour field's palette ramp;
  - 9 are fixed, and no field moves them;
  - 6 mix. The stubble and the buzz cut take skin and hair; the lid, the lip and the mouth take
    skin; the brow takes hair.
- **The rig forgives where the game cannot.**
  - `buildOf` throws `no preset "x"` for an unknown preset key.
  - `normBuild` behaves differently. It swaps an option it does not know for something of its own
    (the Fisher's value, a clamp or a default) and builds on. It also drops keys it does not know.

Under presets only, then, the game may only ever draw one of the ten bodies #923 already baked. All
else a recipe can say is colour.

## 3. The candidates, measured

| | A: a C# port of the build | B: pre-baked parts | C: baked bodies, recoloured (**chosen**) |
|---|---|---|---|
| **What runs per build** | The rig's whole build: body, skeleton and materials, plus every clip pose (53 clips, 438 frames). | An assembly of baked parts. | A lookup: a preset's baked def, and one ramp per material. |
| **Build time** | Node, 548 bodies (the 30 named builds plus 518 seeded random ones), cold, after a warm-up: `buildOf` median 3.81 ms, p95 7.91, worst 11.32. With every clip: median 37.3 ms, p95 43.4, worst 67.2. Editor V8, ten presets × 3 cold (Phase B): `buildOf` median 3.02 ms, p95 6.27, worst 9.17; with every clip, median 20.5 ms, p95 35.4, worst 43.7. Both are inside the node forecast (`buildOf` median 4.04 ms, worst 13.21; `exportBuild` median 38.7 ms, worst 62.2). | Not measured, because there is nothing to assemble (below). | 3,150 builds (630 recipes × 5 rounds), Phase B: median 6.4 µs, p95 8.8 µs, worst 39.9 µs. Bounded: median under 1 ms. |
| **Memory per figure** | Per person: a bind mesh of 161 KiB JSON (median; 214 KiB worst) and 1.2 MiB of clip JSON. In all, 2,048 KiB per preset (median), 2,128 KiB worst. | Every body, baked. | The def's own. A recolour adds 3 RGBA32 ramp textures, at most 24 materials of the def's shades, in place of the figure's own. |
| **GC per build** | The whole body. | n/a | The look only: at most 24 ramps of at most 6 shades. Phase B: no gen-0 collection in 3,150 builds. Bytes per build are not measured: this editor's runtime keeps no per-thread allocation count (`GC.GetAllocatedBytesForCurrentThread` reads nothing), so the test reports its 4 KiB bound there instead of checking it. |
| **Load for the cast** | A build per person at load: 10 × about 37 ms, cold, in node (about 21 ms in the editor's V8). | n/a | None. The cast keeps its ten baked defs. The tables load once: 1,281 colours, 5,124 B. On disk, the four baked assets are 100,964 B of YAML: the contract 72,615 B, the ramps 25,745 B, the catalog 1,501 B and the fit 1,103 B. |
| **What has to be kept** | A second implementation of a 1,408-line rig, kept in step with every revision. | A part bake and an assembler. | A lift (editor) and a lookup (Core). |

- **A** costs at least what the rig costs, because a port does the same work in another language.
  Its real price is the second implementation: every rig revision becomes a port.
- **B fails on the geometry.** It was measured over 880 single structural changes to the ten
  presets:
  - every body field (shape, age, height, weight, sex) moves 99.4–100 % of the base's vertices;
  - frame moves 25–100 %;
  - a hat moves the hair (108 of 120 changes) and the hood;
  - a beard moves the head (82 of 90) and the face;
  - a garment moves the boots (234 of 280).

  A part baked on one body is not that part on another, so "parts" means baking every body. The
  09-24 intake counted about 32.76 million geometry cells for rig 9. That cannot be baked, and
  under presets only it does not need to be.
- **C needs nothing new.** Under presets only, the bodies are the ten baked defs, and a recipe is
  a preset plus colour. Colour is a lookup.

## 4. The choice: C

- **Structure is the ten baked `CharacterSkinDef`s** (#923). A recipe's structural fields may only
  restate its preset's own values.
- **Colour is a lookup.** The tables are lifted from rig 10.3 in the editor's V8 by
  `CharacterBuilderContractBaker`. It runs from the menu ("Bake character BUILDER tables (rig 10,
  ADR 0051)") or headless with `-executeMethod …CharacterBuilderContractBaker.BakeCli`, and
  writes four assets, one entity per file:

  | Asset | Type and path | What it holds |
  |---|---|---|
  | `contract.character_iso10` | `CharacterRigContractDef`, `Data/Characters/CharacterContract10.asset` | The twenty fields and every option; the ten presets' own values; the twenty NPC presets it holds back; a paint rule per material role (axis, fixed or table, with its tone). |
  | `ramps.character_iso10` | `CharacterRampsDef`, `Data/Characters/CharacterRamps10.asset` | 69 axis ramps and 6 fixed ones. |
  | `fit.character_v10` | `CharacterClothingFitDef`, `Data/Characters/CharacterFit10.asset` | The fit the bodies are rigged on, with the rig's sha256 and `skeleton.character_v10`. |
  | `builder.character_iso10` | `CharacterBuilderDef`, `Resources/CharacterBuilder10.asset` | The catalog that wires them to the ten `char_build.*` builds. `char_build.fisher` is the player's default. |
- **The paint is observed, not transcribed.** The rig exports its palettes, but not the rules that
  turn them into material ramps. So the lift paints 630 builds (each preset, and each single colour
  change of it) and reads off, for each role, which fields move it. The lift stops if a role moves
  with anything else, or if its tone moves. No colour and no rule is typed in C#.
- **The builder is one Core call:**
  `CharacterBuilder.TryBuild(builder, recipe, audience, out look, out refusal)`.
  - It checks the recipe against the contract and refuses by name.
  - It takes the preset's build and its baked def, and paints each material from its rule.
  - The look is the def plus one `Color32[]` per material.
  - `IsoCharacterFigureRenderer.Recolour` draws the look. It runs on a change, never per frame.

**What C does not do.** It builds no body outside the ten: no structural variation, no creator, no
random build and no NPC bakes.

- Those widen the offer later: the creator in PR 3, then the wardrobe and the NPC bakes after it.
- The recipe already carries all twenty fields, so widening it needs no second save change.
- When structure must vary, this ADR is revisited. §3 rules B out, so the choice would be A, or a
  bake per offered body.

## 5. The oracle, at tolerance zero

The oracle is the rig itself, run unmodified in the editor's V8 (`CharacterBuilderOracleTests`,
`Tests.RigBaking.EditMode`), not the kit's shipped JSON. Nothing is rounded:

- colours compare as bytes;
- tones compare as the numbers the bake stores;
- structure compares as the rig's own JSON.

What the oracle checks:

- **The committed tables are what the lift makes of the rig today,** field for field.
- **Every preset paints what the rig paints,** role by role against `shadingContract`. That covers
  each preset, each of its single colour changes (620 in all) and 80 seeded whole recolours (8 per
  preset).
- **No colour option moves a bone, a vertex or a gameplay number** (`exportBuild`, `skeleton`,
  `bindMesh`, `gameplay`).
- **The ten committed defs are the rig's paint,** and each rebuilds through the builder to itself.
- **The refusals match the rig's** (§6).
- **What candidate A costs in this editor** (§3) is logged and gates nothing.

The same JS was checked in node 24 against rig 10.3 before the editor run:

- the oracle: 710 builds, 24,140 checks, 0 differences;
- the lift: 630 builds, 21,420 role colours.

## 6. Refusals, side by side

| Asked for | The rig | The builder |
|---|---|---|
| An unknown preset (`ghost`) | Throws `no preset "ghost"`. | Refuses: "Unknown preset 'ghost'", listing the presets. |
| One of the 20 NPC presets | Builds it. | Refuses it by name. The NPCs come with the wardrobe, after PR 3. |
| A custom build (`b:{…}`) | Builds it. | Refuses: rig 10's builder builds presets only. |
| No preset at all | Builds the Fisher. | Refuses. It never falls back to the Fisher. |
| An unknown option, on any of the 20 fields | Swaps it for something of its own and builds on. | Refuses, naming the field, the value and the options. |
| A structural value other than the preset's own | Builds that body. | Refuses, naming it: the structure is the preset's. |
| A child for the player (age `child`, or the boy or girl preset) | Builds it. | Refuses: cast-only (ruling 2). The cast is built from all ten presets. |

The oracle pins the table against the rig:

- every preset key the rig refuses, the builder refuses;
- beyond the rig, the builder refuses exactly three kinds of key: the NPCs, a custom build and no
  key;
- on every field, an option the rig swaps is one the builder refuses.

## 7. The 548 bodies and the 48 extremes

The charter asked for two things that presets only makes moot:

- all 548 check-bodies (375 creator, 125 children, 48 extremes), compared vertex by vertex;
- a Phase B plate of the 48 extremes, built by the builder.

None of the 548 is a preset in its own colour options, so the builder refuses every one of them, by
design. The creator's bodies are PR 3's.

Here, "every offered option" means the ten presets in every colour option: the 620 single changes
and 80 whole recolours of §5, byte for byte. The extremes plate becomes the ten presets recoloured,
beside the rig's own paint. The owner rules on that swap in the PR.

## 8. The recipe, the save and the seam

- **The recipe** (`Core/Iso/CharacterRecipe.cs`):
  - It holds a build id, a fit id, and the rig's twenty `FIELDS` in the rig's order, all as
    strings. An empty value means "the preset's own".
  - Colour is option (b), a key per role.
  - The vest and its bottom share one `outfit`, because the rig paints both from it.
- **The save** (`SaveMigration.CurrentVersion` 14 → 15) adds `SaveData.PlayerRecipe`.
  - The migration's whole work is an empty recipe. That draws the body as baked, which is what
    every v14 save drew.
  - `WornOutfitId` stays where it is. It recolours the sprite layer, which the mesh never drew, so
    copying it into the recipe would change how an old save looks.
- **The seam** (rule 4) is `IPlayerAppearance` in Core (`GameServices.Appearance`), plus two
  EventBus events: `PlayerAppearancePreviewRequested` and `PlayerAppearanceChanged`.
  - A picker previews and commits through Core.
  - `DeckRiderMeshPresenter` (Player) answers. It asks the builder for the look and paints both of
    her mesh figures:
    - aboard;
    - ashore.
  - **The sprite is not recoloured.** Wherever the game draws her sprite, she keeps her baked look:
    - mounted (an open machine's Drive);
    - in every clip (the boarding vault, the ladders, the haul, the chop, the bench, sleep, swim);
    - while wading.

    The recipe reaches the mesh only. Recolouring the sprite layer is a follow-up, named in the PR.
  - Only `Commit` writes the save. A preview never reaches it.
  - A refused recipe is drawn as baked, and the refusal is logged. The drawing side never touches
    the save.
  - The cast keeps its ten baked defs, and no figure key is saved.

## 9. Consequences

- **A rig revision re-runs the lift.** The tables carry the rig's sha256, and the oracle's first
  test names the first field that differs.
- **Two ramps assets sit side by side:**
  - the wardrobe's `ramps.character_iso` (`Data/Characters/CharacterRamps.asset`), which the sprite
    layer reads, is untouched;
  - `ramps.character_iso10` keeps all 45 of its keys in the same shades, except one. On
    `hair.white` the lightest shade moved from `#e8eae3` to `#dfe1db`.
  - It adds 24 keys:
    - `eyes`: moss, umber;
    - `outfit`: black, char, denim, khaki, cream, wine, plum, rose, sky, orange;
    - `shirt`: black, sky, rose;
    - `hatCol`: black, white, straw;
    - `apronCol`: black, red, navy, white, orange, yellow.
  - Whether to keep a new asset or edit the old one in place is the owner's call.
- **The 32-material cap holds.** On the ten defs the most is 24 (the packer).
- **The `iris` role paints nothing on rig 10,** because no preset's bind mesh carries it. So `eyes`
  changes no pixel today. It is kept because the rig offers it.
- **The creator and the wardrobe widen the offer through the same recipe and the same refusals.**
  The creator is PR 3.

## 10. Tests

| Fixture | Assembly | Tests | What it holds |
|---|---|---|---|
| `CharacterBuilderTests` | EditMode | 25 | The builder's resolution and every refusal, on hand-made tables. |
| `CharacterBuilderContentTests` | EditMode | 10 | The shipped tables: one entity per file, the twenty fields, the ramps against the wardrobe's, the vest, the cast and the player's offer, and the cost (§3). |
| `CharacterBuilderOracleTests` | RigBaking EditMode | 8 | §5 and §6 against the rig in V8, and candidate A's cost. |
| `IsoCharacterFigureRecolourTests` | Art EditMode | 9 | `Recolour` and `ResetColours`: only colour moves, on fresh textures, and nothing leaks. |
| `PlayerAppearanceTests` | EditMode | 13 | The seam: preview, cancel and commit; a reload; refusals. All on an injected temporary save path. |
| `SaveMigrationV15Tests` | EditMode | 9 | v14 → v15 from a real v14 file: `WornOutfitId` kept, idempotent, and a round trip. |
