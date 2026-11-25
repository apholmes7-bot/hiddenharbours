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
| 1 | 30 | 0.4 | 0.03 | 0.03 | 0.06 |
| 2 | 80 | 0.8 | 0.06 | 0.06 | 0.12 |
| 3 | 160 | 1.2 | 0.09 | 0.09 | 0.18 |
| 4 | 280 | 1.6 | 0.12 | 0.12 | 0.24 |
| 5 | 450 | 2.0 | 0.15 | 0.15 | 0.30 |
| 6 | 680 | 2.4 | 0.18 | 0.18 | 0.36 |
| 7 | 980 | 2.8 | 0.21 | 0.21 | 0.42 |
| 8 | 1360 | 3.2 | 0.24 | 0.24 | 0.48 |
| 9 | 1840 | 3.6 | 0.27 | 0.27 | 0.54 |
| 10 | 2440 | 4.0 | 0.30 | 0.30 | 0.60 |

Pace, as a rough guide: at about 8 experience per fish, level 10 is about 300 fish. A steady
day of rod fishing (around 20 fish) reaches level 3 on day one and level 5 by day three; the top
takes a couple of weeks of real rod fishing. A level-10 hand is worth a little less than one rod
tier (the starter at level 10 casts 16 m, like a beginner with the coast rod).

**Fight help is mostly skill** (owner, 2026-11-25, "Yes, raise skill"): its steps are twice the
others', +0.06 a level to +0.60 at level 10, so a top hand helps the fight more than the best rod's
own lift (+0.5 on the deep rod). The rods' lift is not lowered.

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

## Compared with Stardew Valley's fishing level

Stardew's fishing level also runs 0 to 10 and rises only from catching fish, with harder fish
giving more. Each level makes the catch minigame easier (a wider catch bar), which is the same
idea as the fight help here. The big difference: in Stardew, better rods are unlocked by
reaching a fishing level. Here nothing is locked: the owner's rule is that rod and skill always
add, so any rod works at any level, and the two numbers stack.

## Not decided here

Prices and sellers for the two upper rods; whether the skill should also widen the true-take
window; the Notebook line's exact wording; the save row above.
