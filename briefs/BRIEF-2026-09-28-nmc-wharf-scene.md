# Brief 2026-09-28: Nine Mile Creek's wharf, the scene

From the Art desk, for the owner. First read [STATE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/STATE.md) on this branch.

This follows [the wharf pass brief](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-28-nmc-wharf-pass.md). Your quick look (revision 2) arrived on 28 Sept, and the owner has ruled on it. Build the scene now: the full return that brief's section 4 describes, with the rulings and the changes below.

## 1. The quick look, checked

- All 77 files arrived, and SHA256SUMS matched 76 of 76.
- Real Node ran `checks/check-quicklook.cjs`: RESULT PASS 11 of 11.
- Every script the bake page loads was inside the zip, and the pass 6 files it loads are unchanged, byte for byte. Keep both habits in the scene return.
- The skippers match the game's boat-owner register.

## 2. The owner's rulings (28 Sept)

Your calls, answered:

1. **Berths:** as you proposed. Six at the wall with berth 0 kept for the player, eight on the two fingers, the crane berth, four at the working float and three at the rec float.
2. **The finger boats:** the ambient fleet, as you leaned. Each gets a shed lot when the register grows.
3. **The beach landing:** yes to the shore path from the landing to the west arm's root, so the first view is across the basin, then the yard. The owner will add this to the game's scene update for Nine Mile Creek.
4. **The road to town:** as you leaned. Wharf Road enters at the turning pad, and the walk goes up the road from there.
5. **The crane:** yes, it replaces the winch. The owner adds that it needs gameplay (section 3).
6. **The breakwater's armour:** granite armour stone, the kit's breakwater preset.
7. **The boat ramp:** yes, beside the working float, for launching small craft. The boat yard's lift takes the bigger boats.
8. **The restaurant and the fish market:** yes, as you leaned: the restaurant on the north row facing the creek, the fish market beside the office. The wharf's ground can grow north to make room for them; give the new ground as numbers.
9. **The gas station:**
   - The harbourmaster moves to the wharf office (section 3).
   - The town's General Store folds into the gas station's store. Its "GROCERY COFFEE ICE" board already says so.
   - So both town lots in the way go. [NineMileCreekMainland.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/NineMileCreekMainland.cs) holds them, as `HarbourmasterPos` and `GeneralStorePos`.
   - The name stays open for now. Leave the station's signs blank in the pictures, and keep your three names in NOTES.
   - The lane to the stream, and the stream itself, are not ruled yet. Draw your proposals; the owner rules on them in the scene.

Also ruled:
- **The cold store** stays. It is storage, not processing.
- **Ladders** go down the quay face, as the first brief asked (its section 3, job 2). The quick look places none; the scene needs them along the wall and at the crane berth.

## 3. What changes in the scene

**1. The breakwaters reach further south.** The owner likes the fingers running north to south from the wall: keep them. But the entry needs more room, so that boats can sail around the fingers.
- In the quick look, the channel runs along y 40 with a half-width of 7 m, and both fingers end at y 43.9, inside it.
- Extend the arms and move the mouth south. Open clear water between the fingers' south ends and the mouth.
- In NOTES, state the clearance in metres and the boat you designed it for. Draw that boat's track in and out around the fingers.
- Give the ground the longer arms stand on as numbers.
- Add the clearance to your checker. Keep its other rules passing: water at spring low under every working hull, in the mouth and in the channel.
- The region stays 760 × 560 m. The sandbar, the crossing and the beach landing stay where they are.

**2. The crane needs gameplay.** You leaned "watch it work now, and use it later with the trap-haul loop". The owner agrees, and the crane has to be playable.
- In NOTES, propose how the player works it: what they lift, from where to where, what they do, and what the camera shows.
- Make the rig carry what that needs: the boom's reach and swing, the hook's point, its states, and where a load sits.
- The game builds the gameplay. You propose it, and the owner decides.

**3. The harbourmaster's office.** The wharf office trailer becomes the harbourmaster's. In the game, the harbourmaster sells the player the cod licence, so this is where the player buys it. Draw the office so it reads that way.

**4. New on the ground:**
- the ladders;
- the boat ramp;
- the shore path;
- the restaurant and the fish market on the north side, with the ground that grows for them.

## 4. What to send

The first brief's section 4 holds, with the fuel stop as its own scene (its section 6):
- `hh-nmc-wharf-return/`, with `scenes/keyscene.nmc_wharf/scene.json` and `scenes/keyscene.nmc_fuel_stop/scene.json`;
- NOTES.md;
- PNG master frames:
  - the wide frame, by day and at dusk at least;
  - on foot: the arrival view from the shore path, the yard and the buyer's truck, the crane at work, the float;
  - night, with the lamps lit;
  - fog or rain, if there is time;
- the ground's changes, as numbers;
- new pieces as rigs in a kit, each with its own data;
- every rig the scenes load inside the zip; a checker that ends in one `RESULT:` line; SHA256SUMS.

In NOTES, list what the rulings ask of the game:
- the harbourmaster's move and the store's merge;
- the crane's gameplay;
- the longer arms, the new mouth, and the boats' routes in and out;
- the ground that grows north.

One zip is fine. If it runs over 500 entries, counting folders, split it by scene.
