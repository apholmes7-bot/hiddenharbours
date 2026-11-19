# ADR 0061 — The player is always the mesh: no sprite-sheet fallback

- **Status:** Proposed (owner ask 2026-11-19: "change all player animations to the new mesh model")
- **Owner:** lead-architect
- **Touches:** `Core/Iso` (new `PlayerMeshRule`, `CharacterFigureWaterline`), `GameConfig`, `DeckRiderMeshPresenter`,
  `IsoCharacterFigureRenderer`, the `HH_FIGURE` variant of the iso facet shader.

## Context

The player has been one skinned mesh aboard and ashore since the 2026-09 rulings, but the mesh only drew the
states the per-state switch (`CharacterSkinDef.MeshStates`, the "ADR 0041" switch in the presenters' comments)
listed, and only the clips `CharacterClipStateMap` mapped. Everything else handed the picture back to the old
sprite sheets:

| Hand-back | Where it was decided |
|---|---|
| Swim, tread (water) | `CharacterClipStateMap.StateKeyFor` returns null, so `DeckRiderMeshPresenter.TryClipPose` refuses; ashore `WhyNotShownAshore` also refused the whole swim band |
| Wading | `WhyNotShownAshore` ("wading, and the sprite draws her in the water") |
| Sleep, Drive (open machines, the seated washroom pose), the three reaches | null in `CharacterClipStateMap`, refused in `TryClipPose` |
| Carried locomotion `idle/walk/run_tray`, `idle/walk_pot`, and `stagger` | baked, but not in `MeshStates`, so the `DrawsAsMesh` gate refused |
| Any other claim on the iso skin (the legacy haul frames, fishing with `MeshFishingAndDig` off) | the `SkinSuspended` gate |

Every one of those states is already baked from rig 10.3 into all ten preset skins. The gap was wiring, not art.
The owner saw it as "when the player swims or does other actions it switches to the old sprite sheets".

## Decision

1. **One switch, player only.** `GameConfig.PlayerLegacySpriteFallback`. Shipped **off** (the asset). The code
   default is **on** so a config built fresh in code (the PlayMode rigs that test the old hand-over) keeps the old
   behaviour: the same code-default/shipped split as `MeshCharacterAshore`. The cast keeps its own switches and
   its `MeshStates` gate unchanged.
2. **With it off, nothing hands the player back.** `PlayerMeshRule` (Core, pure):
   - a whole-body clip asks for `WantedKeyFor(clip, carry)`: the `CharacterClipStateMap` key where there is one,
     else its own baked state (`swim`, `tread`, `sleep`, `drive`) or a reach name;
   - a state the skin carries as a well-formed clip draws as itself; `MeshStates` is not consulted;
   - otherwise the **fallback map** (`GameConfig.PlayerMeshFallbacks`, data, From/To rows) is walked to the nearest
     pose the skin carries, bounded by the map's length, and `idle` ends every chain.
   - the presenter's clip, suspended-skin and water gates are skipped; only the wiring fences and "inside a cab /
     a helm that hides its pilot" remain, which are not sprite fallbacks.
3. **The sea cuts the mesh.** `CharacterFigureWaterline` computes the line in the figure's own frame from the
   posed bones, by the rig's own rule: swim `pelvis z + 0.09`, tread `mean(shoulder z) − 0.05`, wade the live
   depth over the feet capped at the tread line. The margins are `GameConfig` values. The `HH_FIGURE` shader
   variant discards every fragment under the line (`_HHFigureWater`), in the facet, reflection and guard passes.
   Nothing outside `#ifdef HH_FIGURE` changes, so hulls compile the same program.
4. **A guard test** (`PlayerMeshRuleTests`) walks every state key the player's presenter can ask for, from the
   enums and maps, on all ten preset skins, and requires each to resolve to a clip the skin can pose.

## Consequences

- Swim, tread, sleep, drive, the reaches, the tray/pot carries and stagger now draw as the mesh. A move the mesh
  does not follow frame for frame (the legacy haul frames, fishing with its own switch off) draws the player's
  own stance instead of the sheet.
- Below the waterline the mesh is **hidden**, not tinted and refracted the way `PlayerSubmergeVisual` treated
  the sprite. A tint pass is a later slice.
- `MeshStates` stays the cast's parity switch. A later cast PR can widen it; the player no longer waits on it.
- Placement of the lying and sitting clips (bunk height, an open machine, the washroom fixtures) is the rig's;
  it is unverified on a plate and owed to a check in the editor.
