# Character creator, wardrobe and clothing purchases

Owner-directed, 2026-09-13. Pillars P2, P3 and P4. Coordinating owner: **lead-architect**.
Claim: `feat/character-wardrobe-contracts` in `C:/hh-wardrobe`; the main checkout is shared.
Origin: `HANDOFF-2026-09-13-character-creator-and-wardrobe.md` supplied by the owner.

The objective is **create → enter the world → try on → buy → own → equip → save/reload**.
The owner has already requested this feature; historical creator deferrals do not require a
second request. A review viewer or palette picker alone does not meet the objective.

**Owner follow-up, 2026-09-13:** finish the cast for beauty and clarity in its low-poly pixel-art
form. [Pass 05's art brief](../docs/art/briefs/character-finish-pass05.md) adds coordinated facial,
garment silhouette and material refinement across all ten presets, with a direct pass-04 comparison.
This is part of CW-01's art source delivery; modular clothing fits remain explicitly bounded.

## Delivery and acceptance

| Item | Owner | Status | Acceptance |
| --- | --- | --- | --- |
| CW-01: face refinement and clothing contracts | lead-architect, with art-director/tools-editor | Ready for review; Unity CI pending | Preserve the expressive head, show before/after at 64 and 32 px/m, demonstrate an actually mixed outfit with explicit fit/coverage/material contracts, record measurements and amend ADR 0029. Add tested shared interchange data. |
| CW-02: production appearance and persistence | lead-architect, art-pipeline, gameplay-systems | Pending CW-01 | Bake fitted parts and facial rendering; apply one appearance ashore/aboard/mounted without pose/anchor changes. Separate identity, owned garment variants, equipment and saved outfits. Read the then-current save version before bumping it. Test legacy grants, cancellation, readiness and persistence failures against disposable saves. |
| CW-03: creator, wardrobe and authoring tools | ui-ux, tools-editor, world-content | Pending CW-02 | Reach creation from actual New Game before replacing a save. Rotate body/face previews; meaningful supported options, starter clothes, cancel/reset. Reach a wardrobe/mirror in normal play; equip/remove, edit appearance and save named combinations. Use modal input gates with KB/mouse and gamepad. Create/duplicate/save/validate garment and recipe Defs without editing code or YAML. |
| CW-04: clothing commerce and end-to-end proof | economy-sim, ui-ux, world-content, qa-test | Pending CW-02/CW-03 | Authored stock at an existing seller; try on, cancel, buy, wear now/later. Use IWallet; exactly one charge plus permanent ownership. Reject insufficient funds, invalid content, already owned and duplicate confirmations. Prove reload, legacy wardrobe retention, in-world fits and the measured runtime budget. |

## Rules that span the deliveries

- **Detail · 64 px/m** is the preferred inspection density, alongside the gameplay check at 32.
  World PPU stays 32 and metres/physical height stay unchanged.
- Keep the stronger teal/ochre/shadow palette, eight expressions, fixed eye sockets with moving
  pupils, staged/double blinks, brows and four speech mouth shapes. Speech must not drive brow bounce.
  Review profiles, three-quarter views, the ten cast presets, light/dark skin, blond hair, beard,
  fringes, hats and hoods. Changing clothes never recolours or replaces the person.
- Headwear, tops, bottoms, outer/full garments, footwear and accessories need real geometry.
  Combined garments occupy multiple slots. Explicit coverage hides only matching surfaces and
  restores them on removal. Unsupported fits/options remain unavailable.
- One stable-ID Def per authored entity; the offline workbench exports the same schema. No JS in
  the shipping player. No garment × colour × animation bake explosion or guessed universal skeleton.
- Starting grants are authored and limited. Preview does not grant ownership. Later saved outfits
  can apply only owned variants; missing or incompatible items need a deterministic, explained fallback.
- Preserve existing `WornOutfitId` looks and previously available wardrobe choices when migrating.
  Preview, committed appearance, ownership and wallet changes are separate. Never test on the owner's save.
- Verify walking, running, digging, fishing, swimming, sleeping, sitting/driving, riding and mounting.
  The preview's isolated pose checks do not prove boat, hand, tool or seat contact in Unity.

## Completion evidence

Reviewed PRs and passing relevant tests; updated offline inspection tool; face comparison at both
densities; playable create/buy/equip/reload and legacy-save proof; one new purchasable garment added
through tools; draw calls, ramp limits, memory, swap time and allocations measured in production.
Current baseline and unresolved production seams are in
[`character-wardrobe-baseline-2026-09-13.md`](../docs/verification/character-wardrobe-baseline-2026-09-13.md).
CW-01 does **not** change SaveData or claim that the full feature is complete.
