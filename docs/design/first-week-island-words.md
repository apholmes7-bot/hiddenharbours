# First-week words stay on the island

**PROPOSAL — every replacement below awaits the owner's choice.** This page changes no spoken line, task, item or scene. The first week stays on St Peters: clam work, shore fishing, buying and mending the dory at the island slip, then fishing close to shore. Ginny's evening word ends the demo. See [the weather-bound first week](../adr/0062-the-first-week-is-closed-by-weather.md).

## Boundary and audit

The audited base is `98f9f3c55a1bfb3dda2e2c52a035138b0d9fab7a`. It has eight RegionDefs and no demo-build setting. Drafts #1510 and #1572 define the demo as the first build scene. The test fixture records that scene, checks it against the build settings, and derives the inside region and outside names from the RegionDefs. It introduces no runtime boundary or save field.

St Peters Island (`region.st_peters`) is inside. The seven outside regions on this base are:

| RegionDef display name | Region id |
| --- | --- |
| Coddle Cove | `region.coddle_cove` |
| Cumberland | `region.cumberland` |
| The East Water (working name) | `region.east_water` |
| Nine Mile Creek | `region.nine_mile_creek` |
| Rice Point | `region.rice_point` |
| The South Water (working name) | `region.south_water` |
| The West Water (working name) | `region.west_water` |

The scanner also recognises the names without the parenthetical authoring qualifier or leading “The”, and matches place names and region ids at word boundaries, ignoring case and allowing folded whitespace. This is a RegionDef-based vocabulary; it does not guess nicknames such as “the creek”, or destinations that have no RegionDef yet.

The inventory includes all first, repeat and conditional speech, dialogue option labels and replies (including an empty sale), gift replies, island routines and weekly visitors, island conversations, inner-voice cues, Notebook quests and knowledge pages, gear-page names, shared string constants, and serialized scene/prefab text including sign labels and text overrides. Flags and time gates do not remove dialogue branches from the audit. Item flavour includes island catalog listings, their tools, directly placed text content and fish allowed in island water. The cod licence is included prospectively because #1590 brings it to the island store.

Story gates do not exist on this base. The reader recognises their serialized text fields and, when #1572 lands, chooses the demo word in preference to the full-game word. The full-game closing line that opens Nine Mile Creek is outside the demo audit when the gate has its separate demo word.

## Every offending line

Eleven authored lines mention an outside place: four tasks, four lines from Ginny, one store greeting, one line in the letter, and the prospective cod-licence flavour. Each row below corresponds to one exact exception. Array indices start at zero. A line is listed once even if it mentions a place more than once.

| Source and field | Current words | Island-only wording — PROPOSAL |
| --- | --- | --- |
| [WorldStrings.cs](../../Assets/_Project/Code/World/WorldStrings.cs) · `OnboardBuyDory` | Save up, then BUY the damaged dory at the Nine Mile Creek shipwright. | **PROPOSAL:** Basil's broken dory is for sale at the island slip. Save up and make her yours. |
| [WorldStrings.cs](../../Assets/_Project/Code/World/WorldStrings.cs) · `OnboardBuyLicence` | At Nine Mile Creek: sell your clams, then buy a COD LICENCE to fish. | **PROPOSAL:** Sell your clams at the island store, then buy a cod licence. |
| [WorldStrings.cs](../../Assets/_Project/Code/World/WorldStrings.cs) · `OnboardCrossBar` | Walk the bared sandbar east to Nine Mile Creek before the tide floods it back. | **PROPOSAL:** Carry your clams to the island store and sell them at the counter. |
| [WorldStrings.cs](../../Assets/_Project/Code/World/WorldStrings.cs) · `OnboardSailHome` | Seaworthy at last — sail your dory home to Coddle Cove. | **PROPOSAL:** She's seaworthy. Launch her at the island slip and try a line close to shore. |
| [GinnyOpening.asset](../../Assets/_Project/Data/NPCs/Dialogue/GinnyOpening.asset) · `FirstLines[3]` | Then the sandbar to Nine Mile Creek opens up, dry enough to walk. Go before the flood seals it, mind — dawdle and you're stuck till the next low. | **PROPOSAL:** Mind the water. Work the flats on a falling tide, and carry your clams back to the store before the sea comes in. |
| [GinnyOpening.asset](../../Assets/_Project/Data/NPCs/Dialogue/GinnyOpening.asset) · `FirstLines[4]` | In Nine Mile Creek, sell your clams, buy a COD LICENCE and a ROD, and save. There's a damaged dory for sale at the shipwright — buy her, pay to put her right, and she's truly yours. | **PROPOSAL:** Sell your clams here on the island. Get your cod licence and a rod from the store, then save for Basil's broken dory at the slip. You'll mend her with your own hands. |
| [GinnyOpening.asset](../../Assets/_Project/Data/NPCs/Dialogue/GinnyOpening.asset) · `FirstLines[5]` | Then sail her home to Coddle Cove. That's where your harbour starts. Off you go, now — the tide won't wait on us. | **PROPOSAL:** Once she's sound, launch her here and fish close to shore. Come back for supper, now — there's no hurry to leave. |
| [GinnyOpening.asset](../../Assets/_Project/Data/NPCs/Dialogue/GinnyOpening.asset) · `RepeatLines[0]` | Back already? Good. Watch the tide — clams on the flats at low water, then the bar to Nine Mile Creek. | **PROPOSAL:** Back already? Good. Watch the tide — clams on the flats at low water, then back to the island store. |
| [MargueriteFirst.asset](../../Assets/_Project/Data/NPCs/Dialogue/MargueriteFirst.asset) · `FirstLines[2]` | And I'll not price my neighbours the way they do over at Nine Mile Creek. A store that squeezes a small place kills it — and then where's the store? | **PROPOSAL:** I'll keep my prices fair for my neighbours. A store that squeezes a small place kills it — and then where's the store? |
| [NedLetter.asset](../../Assets/_Project/Data/NPCs/Dialogue/NedLetter.asset) · `FirstLines[2]` | "So I'll leave you something better than a boat: the way to earn one. Dig clams on the low-water flats. Walk the bar to Nine Mile Creek. Buy a licence, a rod, and a hull worth mending." | **PROPOSAL:** "So I'll leave you something better than a boat: the way to earn one. Dig clams on the low-water flats. Buy your licence and rod at the island store, and save for a hull worth mending." |
| [CodLicense.asset](../../Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset) · `Flavor` | Nine Mile Creek's harbourmaster signs you off to take cod on rod and line. | **PROPOSAL:** Your paper for taking cod on rod and line. Keep it dry. |

The storekeeper's comparison is flavour, not an instruction to travel, but it still names an outside place. It receives the same explicit exception as a task. The cod licence's proposed wording avoids naming an issuer whose island role has not been chosen.

## Applying a chosen line

The exceptions file keeps the exact current text, file, field and reason for each row. No wildcard can admit a file or a whole region. A changed line is checked again; a corrected or removed line leaves a stale exception and fails until that exception is removed. New mentions fail. The initial eleven-line ceiling must fall as the debt is paid down; it is not permission to exchange a corrected row for a new offender.

Changing the old crossing task's words alone will not change its completion condition. Coordinate the task changes with the island progression and story-gate work in #1572, the dory sale in #1539, and the island licence listing in #1590. None of those files is edited here. The repair task also still mentions payment to a shipwright without naming an outside place: that is a separate semantic error, beyond a place-name guard, and needs its own wording decision when the hand-mending jobs land.

## Coverage and proof limits

The source audit found no additional outside-place mention in the shipped island Notebook pages, serialized scene text, island inner voice, or the other selected island item flavour. A wider data sweep found one extra mention in `SeaScallop.asset.Flavor`; its only allowed region is Rice Point, so it is excluded from the island inventory rather than excepted.

The existing island content-validation suite now runs the guard. Added EditMode cases cover synthetic outside names and ids, word-boundary negatives, repeat mentions, stable ordering, required and stale exceptions, changed wording, duplicate addresses, serialized dialogue branches, Notebook text, item flavour, folded and escaped sign text, and prefab overrides. Unsupported block-style scene text fails explicitly instead of disappearing from the scan. The scene reader never opens a scene or invokes its components.

Source checks verified the eleven exact exceptions, the RegionDef boundary, unique source addresses in the exception asset, source-file resolution for the selected word classes, fresh metadata and the protected-file exclusion. Unity compilation and EditMode execution were not run locally; those results are pending CI. No picture comparison was run. Text baked into images is outside this text-only audit. Player-written Notebook notes are not authored content and are not scanned.

The guard and its snapshot objects live only in the editor test assembly. They read no player state and have no gameplay or save cost. A save step needs no new field, migration or format change.
