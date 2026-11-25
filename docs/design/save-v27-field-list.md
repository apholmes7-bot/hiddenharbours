# Save v27: the field list

What save version 27 adds, one row per field. Decision and detail: [ADR 0064](../adr/0064-save-v27.md).
**Nothing here merges until the owner has seen this list.** Each group can be answered yes or no on its own;
a "no" removes that group's rows and the version still holds the rest.

**Your existing save:** it loads as before, nothing changed or lost. You start with no store-bought garments
(your outfit colours stay), a full clothing rack, and rod skill zero. Tide, wind and weather are still never
saved.

## Group A: clothing (asked for in the store clothing change, accepted 2026-11-25)

| Field | What it means | Who asked | Example |
|---|---|---|---|
| `PlayerClothing` | The list of players' wardrobes, one entry per player | Clothing (#1566) | one entry, for you |
| `PlayerClothing[].PlayerKey` | Whose wardrobe this is | Clothing; co-op (#1563) | `player.local` (you) |
| `PlayerClothing[].OwnedClothing` | Every garment he owns | Clothing | `clothing.oilskin_jacket`, `clothing.rubber_boots`, `clothing.watch_cap` |
| `PlayerClothing[].WornClothing` | The garments he has on | Clothing | `clothing.oilskin_jacket`, `clothing.rubber_boots` |
| `ClothingRackSales` | What the clothing rack has sold, so sold-out stays sold out | Clothing | two entries |
| `ClothingRackSales[].RackId` | Which rack | Clothing | `clothing_rack.st_peters_general` (LeBlanc's) |
| `ClothingRackSales[].ClothingId` | Which garment | Clothing | `clothing.rubber_boots` |
| `ClothingRackSales[].Units` | How many sold | Clothing | `2` |
| `ClothingRackSales[].Day` | The game day they sold | Clothing | `3` |

## Group B: rod skill (asked for by the owner: "add rods as a player skill that increases overtime")

| Field | What it means | Who asked | Example |
|---|---|---|---|
| `PlayerSkills` | The list of players' skills, one entry per player per skill | Rod skill (#1568) | one entry |
| `PlayerSkills[].PlayerKey` | Whose skill this is | Rod skill; co-op (#1563) | `player.local` (you) |
| `PlayerSkills[].SkillId` | Which skill | Rod skill | `skill.rod_fishing` |
| `PlayerSkills[].Experience` | Experience earned so far. The level is worked out from it, never saved | Rod skill | `57` (level 1 on the proposed curve, about 7 fish) |

**Why the player key is there now.** The skill and the wardrobe already belong to one player each. With the
key in every row, a friend who joins later is simply one more row, and his clothes and skill are kept on the
host's PC with no second save change. Without it, the co-op save step would have to change these rows again.

## Not in v27 (could join; each needs its own yes)

| Item | Why not now | Cost to add |
|---|---|---|
| Cabin-light switch per boat (#1564) | Decided not saved for now: boats start on Auto after a load | One small row per boat; about half a day |
| Who paid a special order (#1503) | Only matters with two players | One field |
| The carried pail's place and clams (#1552, #1553) | Needs a ruling first | One row like the paper bag's |
| Shore picking beds (#1527), dory mending in progress (#1508), seller balances (#1230) | Designs not settled | One row each, later |
| Co-op player records: name, look, beds, held things (#1563) | Its own save step, later | Its own version; v27 is shaped so it does not move these rows |

## Size

About 80 bytes for a save that has not used any of it yet; under 2 KB for a demo week with clothes bought and
the rod skill growing.
