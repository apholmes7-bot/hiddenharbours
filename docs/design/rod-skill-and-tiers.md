# Rod skill and rod tiers

Status: built as a draft, every number below is a **PROPOSAL** for the owner to tune.

## In plain words

Every rod has its own numbers: how far it casts, how strong its line is, how fast it reels
up, and how much it helps in a fight. The player also has a **rod-fishing skill** that grows
as he lands fish on a rod. His skill adds to those same four numbers. The two always add
together: **what he fishes with = the rod's numbers + his skill's numbers.**

So a skilled player still gets more from a better rod, and a beginner still gets more from a
better rod too. Neither replaces the other, and nothing is locked behind a level.

## The ruling this follows (owner, 2026-11-25)

"rod tiers include all the suggested helpers, add rods as a player skill that increases
overtime to help with those items, however the rods stats are always added to the players own
skill level, therefore a high skilled player always benefits from the better rods."

## The rods (tiers)

Each rod's numbers live on its own `ToolDef` asset (`RodFishing`, a `RodFishingProfile`).
This branch builds on the earlier numeric rod-tier proposal (`docs/design/rod-tiers-as-data.md`):
the same shape and the same three rods. The reach-only alternative is not taken.

| Number the skill also touches | Starter `tool.rod` | Coast `tool.rod_coast` | Deep `tool.rod_deep` |
|---|---:|---:|---:|
| Cast reach, metres | 12 | 16 | 22 |
| Line strength (strain is divided by it) | 1.0 | 1.2 | 1.5 |
| Reel-up, metres per second | 1.5 | 1.8 | 2.1 |
| Fight help (landing speed multiplier) | 1.0 | 1.2 | 1.5 |

The rods' other numbers (line length, take window, bite delays, fight endpoints) are the rod's
alone; the skill does not touch them. The two upper rods stay unsold, as before.

## The skill

**What raises it.** Only a fish **won on a rod**: the fight ends with the fish landed. That
includes a protected fish that is documented and released, and a fish sent back for want of a
licence: the skill is in landing it. Nothing else counts: no time passing, no waiting for a
bite, no lost fish, no clam dig or hand-gathered tend, no traps, pots or longlines.

**How much.** Experience for one fish = `ExperiencePerFish + round(ExperienceForHardestFish x difficulty)`,
where difficulty runs 0 (easy) to 1 (hard). Difficulty is the fish's existing fight strength:
its `RodFightDef.Strength` when it has one, otherwise the legacy fight's own blend of the
species' category and where its weight falls in the size range. Fixed at the hook, so the same
fish always gives the same amount.

| Tunable (PROPOSAL) | Value |
|---|---:|
| `ExperiencePerFish` | 3 |
| `ExperienceForHardestFish` | 12 (so 3 to 15 per fish) |

**The curve** (cumulative experience, and the whole bonus at that level; every row PROPOSAL):

| Level | Experience to reach | + Reach m | + Line | + Reel m/s | + Fight help |
|---:|---:|---:|---:|---:|---:|
| 0 | 0 | 0 | 0 | 0 | 0 |
| 1 | 30 | 0.4 | 0.03 | 0.03 | 0.03 |
| 2 | 80 | 0.8 | 0.06 | 0.06 | 0.06 |
| 3 | 160 | 1.2 | 0.09 | 0.09 | 0.09 |
| 4 | 280 | 1.6 | 0.12 | 0.12 | 0.12 |
| 5 | 450 | 2.0 | 0.15 | 0.15 | 0.15 |
| 6 | 680 | 2.4 | 0.18 | 0.18 | 0.18 |
| 7 | 980 | 2.8 | 0.21 | 0.21 | 0.21 |
| 8 | 1360 | 3.2 | 0.24 | 0.24 | 0.24 |
| 9 | 1840 | 3.6 | 0.27 | 0.27 | 0.27 |
| 10 | 2440 | 4.0 | 0.30 | 0.30 | 0.30 |

Pace, as a rough guide: at about 8 experience per fish, level 10 is about 300 fish. A steady
day of rod fishing (around 20 fish) reaches level 3 on day one and level 5 by day three; the top
takes a couple of weeks of real rod fishing. A level-10 hand is worth a little less than one rod
tier (the starter at level 10 casts 16 m, like a beginner with the coast rod).

**The formula, in one line:** for reach, line, reel and fight help,
`effective = rod value + skill bonus at his level`.

**Caps.** The skill stops rising at level 10 (experience is still counted). The sum is not
capped: the owner's ruling is that a better rod always helps. The cast is still limited by the
gesture as before (reach is the most a full wind-back delivers).

**How the player sees it** (proposal only, no UI built here): one line at the top of the
Notebook's CAUGHT page, in its existing style, for example "Rod fishing: level 4, 120 to the
next". The rod's description could say its own numbers in words ("casts a good way out,
strong line"). A level-up could be one inner-voice line ("My casts are getting longer."); the
event `RodSkillLevelReached` is published for that and nothing listens to it yet.

## How it is built

- `Core/Fishing/RodSkillDef.cs`: the tunables (one asset, `Data/Resources/RodSkill/RodSkill.asset`,
  id `skill.rod_fishing`), with a `Problem()` check that refuses a ladder that does not rise.
- `Core/Fishing/RodSkill.cs`: `RodSkillMath` (pure: level from experience, bonus at level,
  experience for a fish, rod + bonus), `IRodSkill` (one player's skill), `IRodSkillBook` (every
  player's skill by player key), `RodSkillProgress` and `RodSkillBook`.
- `GameServices.RodSkills`: the seam, never null, cleared by `Reset`.
- `FishingController`: at the start of each cast the captured rod becomes rod + skill
  (`CaptureRod`); a won rod fight records its experience (`OnLanded`). It reads only the Core
  interface.

Deterministic: experience is a whole number from the fish's own data, the level is a pure
function of the total, nothing rolls. No per-frame work: the skill is read once per cast and
written once per landed fish.

**Co-op.** The skill is per player. The book holds one skill per player key, and each fishing
rig names its angler's key (`AnglerKey`, default the local player). There is no single "the
player" skill.

## Saving (not done: needs the owner's yes)

The skill is **session-only** in this change. It is held per game (bound to the live save
object, so a load or a new game starts at zero) and never written to the save. With no game at
all, nothing is earned and every skill reads zero.

No existing saved structure fits without changing its meaning:
- `SaveData.CaughtSpecies` (the Notebook's catch record) counts every way a catch comes up,
  traps, pots, longlines and the clam dig included, so a skill derived from it would grow from
  pot hauls. It is also one record per save, not per player.
- `SaveData.OnboardingFlags` holds true/false only.
- `PlayerTraits`, `PlayerNeeds` and `PlayerBody` hold other things.

The smallest save change that would make it last: one list on `SaveData`, the local player's
skill rows, `List<PlayerSkillDto> PlayerSkills` with `{ string SkillId; int Experience; }`, one row
`skill.rod_fishing`. Older saves migrate to an empty list (skill zero); the book would read the
row when it binds to a save and write it on each landed fish, the same way the catch record does.
It needs the next save version, which only the owner authorises.

## Rods from parts

The owner, 2026-11-25: rods should be customisable. The rod's length gives the cast reach; the
line is bought separately and a heavier line is stronger but puts some smaller fish off; the reel
sets how fast the line comes up; the fight is mostly skill, with better gear making it somewhat
easier. He also said yes to saving the rod skill (the save rows are listed below; the save change
itself is planned separately).

### The three parts

Each part is its own Def, one asset per part, with a stable id:

| Part | Def | Id prefix | What it sets |
|------|-----|-----------|--------------|
| Rod | `RodBlankDef` | `rod.` | cast reach (m); the bite timing (delays, auto-hook); the take window (how well the tip shows a bite); the rod's own lift in the fight (`LandingMultiplier`); the old fight's easy/hard ends |
| Line | `FishingLineDef` | `fishing_line.` | line length (m, the deepest drop); line strength; which small fish it puts off and by how much; a small fight ease |
| Reel | `FishingReelDef` | `reel.` | reel-up speed (m/s); a small fight ease |

The line's prefix is `fishing_line.` because `line.` already names the store's stock lines
(`line.bread`).

**The numbers the fishing code reads** are still one `RodFishingProfile`, built from the three
parts and then passed through the same skill interface as before:
- reach = rod's cast + skill
- strength = line's strength + skill
- reel speed = reel's speed + skill
- fight help = rod's lift + line's ease + reel's ease + skill

### A heavier line puts small fish off (the rule)

From the shipped fish data, the rod fish run from rainbow smelt (top weight 0.1 kg), tomcod
(0.3), cunner (0.6), mackerel (1.5) and winter flounder (2) up to haddock (6), pollock (9), cod (12)
and striped bass (25). The rule uses each species' **top weight** (`MaxWeightKg`), so it is fixed
by the fish's own data and never rolls on its own.

A line carries two numbers: `ShyBelowKg` and `ShyDamp01`. For a species whose top weight is
below `ShyBelowKg`, its bites are scaled by

    shy = ShyDamp01 + (1 - ShyDamp01) * (MaxWeightKg / ShyBelowKg)

so the smallest fish are put off the most and a fish at or over the mark not at all. With
`ShyBelowKg` 0 the line puts nothing off.

The bite lost to a heavy line is **lost, not passed to a bigger fish**: the catch roll keeps its
one random draw over the same total as before, and the part of a small fish's share the line puts
off becomes "nothing bit". A heavy line therefore catches fewer small fish, not more big ones.
When no line puts anything off, the catch goes through the existing catch roll unchanged.

Example, the new heavy braid (`ShyBelowKg` 2, `ShyDamp01` 0.35): smelt bites at 0.38 of its usual
rate, tomcod 0.45, cunner 0.55, mackerel 0.84, flounder and everything bigger 1.

### The fight: skill first, gear a little (PROPOSAL, for the owner to confirm)

The owner leans to the fight being skill only, with better gear making it somewhat easier. The
split proposed:
- Most of the fight help comes from the skill (up to +0.30 at level 10 today).
- The line and the reel each carry a small fight ease, from 0 on the starter parts to about +0.03
  on the best ones, so the gear's part stays clearly smaller than the skill's.
- The rod's own lift stays as shipped: 1.0, 1.2 and 1.5 on the three rods.

One thing to decide: the shipped deep rod already lifts +0.5, more than the skill's top +0.30. To
make the skill the bigger part, either raise the skill's top fight help (for example to +0.60,
data only, nothing shipped changes) or lower the upper rods' lift (this changes the shipped rods).
This change does neither; the first is the recommendation.

### Today's rods are pre-fitted sets

The three shipped rods stay as they are, now each a rod with its default line and reel already on:

| Rod (tool) | Rod part | Line | Reel |
|------------|----------|------|------|
| `tool.rod` | `rod.hand` (cast 12 m, lift 1.0) | `fishing_line.light` (60 m, strength 1.0) | `reel.hand` (1.5 m/s) |
| `tool.rod_coast` | `rod.coast` (16 m, 1.2) | `fishing_line.medium` (75 m, 1.2) | `reel.coast` (1.8 m/s) |
| `tool.rod_deep` | `rod.deep` (22 m, 1.5) | `fishing_line.heavy` (90 m, 1.5) | `reel.deep` (2.1 m/s) |

The default lines put no fish off and the default line and reel add no fight ease, so with the
default fit and no skill every number is exactly the shipped rod's. A test pins this.

One new part shows the rule: `fishing_line.heavy_braid` (90 m, strength 1.8, puts off fish under
2 kg, fight ease +0.03). Proposal for the owner: give the deep rod's own heavy line a mark too
(for example under 1 kg); that would change what the shipped deep rod catches, so it is his call.

### Fitting is per player

Which line and reel are on which rod belongs to the player, not the rod: two players with a deep
rod each can fit them differently, and one player changing his fit never changes another's. The
fit is held in a book with one fitting per player key, like the skill (`GameServices.RodFittings`).
A rod with nothing fitted fishes with its own default line and reel.

The fit is keyed by the rod's tool id (`tool.rod_deep`), meaning "this player's deep rod". If the
game later gives each rod its own identity (a rack already remembers rod instances), the key can
become the rod's instance id without changing the parts.

Session-only in this change, like the skill: a load or a new game starts with every rod on its
default fit, and nothing is written to the save.

### How owned and held things are stored today, and what fitting will need

- `SaveData.OwnedGear` is a list of gear ids, one per kind, presence only ("you own a rod").
- A rack with memory (save v25) keeps a rod instance where it was put away.
- No saved field says what is on a rod.

Fitting will need, per player: one row per fitted rod, `{ RodKey; LineId; ReelId }`, where an
empty id means the rod's default part. Owning a part that is not on a rod needs the parts in
`OwnedGear` (presence) or as counted stock if spare lines are to be used up. Not added here.

### Where the player changes his line and reel (described only, nothing built)

- **At the store counter:** buying a line or reel offers to fit it to the rod in his hands
  straight away; the old one goes to his storage.
- **At home:** the rod rack shows each rod with its line and reel, and he swaps from what he owns.
- **In the pocket page:** a rod's line shows its length, strength and which fish it puts off, and
  the reel its speed. Fitting from the pocket while out on the water is left for the owner to decide.

### In the store

The parts are plain data a store section can place. Each part has a `GearId` (empty for now),
the same link a tool uses to its shop listing. To sell one, a shop listing (`GearOffer`), the
store's stock line and the shelf slot are added by the store and economy work; the parts do not
change. Proposed prices: lines 8, 12, 18, braid 30; reels 25, 40, 60; rods 40, 70, 110.

### Art owed

Every part needs a sprite before it is sold; none has one, and no placeholder is drawn:
`rod.hand`, `rod.coast`, `rod.deep`, `fishing_line.light`, `fishing_line.medium`,
`fishing_line.heavy`, `fishing_line.heavy_braid`, `reel.hand`, `reel.coast`, `reel.deep`
(shelf goods art and a pocket-page icon each).

### Co-op

Per player throughout: each player's fit is his own row in the fitting book, each player's skill
is his own, and the rig reads both under the angler's key. A test checks that one player's fit
never changes another's.

### Saved fields both need (for the save plan)

- **Rod skill:** `SaveData.PlayerSkills`, a list of `{ string PlayerKey; string SkillId; int Experience }`.
  The player key is needed now: co-op saves on the host only and a guest leaves with his own
  things, so the host's save has to tell the players' skills apart.
- **Fitted parts:** `SaveData.RodFittings`, a list of `{ string PlayerKey; string RodKey; string LineId; string ReelId }`,
  plus the bought parts' ids in `OwnedGear`.

## Compared with Stardew Valley's fishing level

Stardew's fishing level also runs 0 to 10 and rises only from catching fish, with harder fish
giving more. Each level makes the catch minigame easier (a wider catch bar), which is the same
idea as the fight help here. The big difference: in Stardew, better rods are unlocked by
reaching a fishing level. Here nothing is locked: the owner's rule is that rod and skill always
add, so any rod works at any level, and the two numbers stack.

## Not decided here

Prices and sellers for the two upper rods; whether the skill should also widen the true-take
window; the Notebook line's exact wording; the save row above.
