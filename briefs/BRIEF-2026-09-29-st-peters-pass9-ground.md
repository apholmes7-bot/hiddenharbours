# Brief 2026-09-29: St Peters on its planned ground (the extra places again, and every St Peters scene from now on)

From the Art desk, for the owner. First read [STATE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/STATE.md) on this branch.

This follows your quick look at two extra places: the lookout at the South Arm's south end, and the plank bridge over the Gap Brook. It covers:
- what happened to the quick look;
- why its ground looks old, and the new ground in the zip;
- what to draw next: the same quick look on that ground, and every St Peters scene on it from now on.

## 1. The quick look, checked

- **The zip arrived whole.** `SHA256SUMS-extra-places-quick-look.txt` matches 64 of 64.
- **Real Node agrees with your run.** `check-extra-places-quick-look.cjs` passed 43 of 43, with the ground group checked against the game's texture.
- **Checked here against the plan.** At the bridge, (156.4, −40.9), part 2's ground reads +5.02 m, as your NOTES say. Today's texture reads +6.00 m there.

## 2. Why the ground looks old

The owner looked at the boards and said: "the terrain looks like the old shape in this zip". They are right, and it is not your error.
- The game's St Peters height map, `StPetersSeabed_HeightTex.png`, is still the ground from before terrain pass 9. Terrain PR 5 is the step that bakes pass 9 into it, and it has not been built yet.
- Your pass 8 stood on that texture and laid part 2's features on it near each place, as your NOTES say. So the ground under each place follows the plan, but the coast behind it is the old one: the plain oval, the straight bar, and no south ring.
- The owner asked: "wouldnt it be easier that these scenes are built over the exisiting terrain modifications?" Yes. So St Peters' planned ground now comes to you as a texture.

## 3. The ground: the zip

The owner uploads `HH-st-peters-pass9-ground-2026-09-29.zip` with this paste. It holds:
- **`StPetersSeabed_HeightTex_pass9.png`,** St Peters after pass 9 as ruled: [part 1](https://github.com/apholmes7-bot/hiddenharbours/blob/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/docs/design/st-peters-terrain-pass-9.md), and [part 2](https://github.com/apholmes7-bot/hiddenharbours/blob/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/docs/design/st-peters-terrain-pass-9-part-2.md) with the crossing's layout B.
  - It is the plan's own saved ground (part 2, Appendix B: `maps2.E`, layout B), checked against the hash the plan prints. It is not a new derivation.
  - It is in terrain PR 5's own format: a 16-bit grey PNG from −4 to +7 m ([ADR 0046](https://github.com/apholmes7-bot/hiddenharbours/blob/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/docs/adr/0046-still-water-above-the-tide.md), section 8). It covers the same world rectangle, with the same texel centres, as today's texture.
- **`README.md`:** the decode, the file's and the pixels' sha256, spot heights to check your decoder against, what moves, and what the file does not carry.
- **`pictures/`:** the plan's own `plan-island.png` and `change.png`, and `today-vs-pass9.png` (today's texture above, the new file below).

## 4. What to do

1. **Read the 16-bit file.** Teach `tools/stpHeightTex.js`, and each page that reads a texture, the new file: height = −4 + 11 × code / 65535, with the codes big-endian. Check your decoder by the README's pixels sha256 and its spot heights. Keep today's 8-bit file readable as well, because it stays the game's until PR 5 lands.
2. **Stand the stage on it.** For St Peters, `tools/stpGround.js` and your bake pages read the new file by default.
   - Part 2's features are already in it, so drop the part of `pass8/xpGround.js` that lays them on today's texture.
   - Lay only what is yours, such as the dipping pool and the bridge's footings, and list each one in the README.
3. **Re-render the quick look.** Draw the same two places and the same four boards: `lookout_low`, `lookout_dusk`, `bridge_noon` and `bridge_golden`. Keep your eight calls. Say which of them the new ground changes, with old and new numbers, for example the vista hold, and how much of the South Arm's walls shows at a spring low.
4. **From now on, stand every St Peters board on this ground.** That covers the extra places' full return, and each key scene's full return when it comes.
   - Scenes the owner has ruled stay ruled.
   - Redraw one only if the new ground moves it, and list any that it moves.
5. **Ship it as a new zip,** named for this pass, as section 5 of STATE.md asks. Your checker's ground group should check both textures: this one for the boards, and today's for the game as it is.

When terrain PR 5 lands, the Art desk sends you its texture. It should match this one closely. Where it differs, because the Planner is fitting pass 9 round the key scenes, the Art desk will say where.
