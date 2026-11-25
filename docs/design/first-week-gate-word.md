# The islander's word at the end of the first week

**Ginny gives her word on the evening the five facts are true; the weather turns the NEXT MORNING (after a sleep), never in the same step.** Once the player has mended the dory, holds the cod licence, has kept a fish caught from the repaired dory, has sold shellfish and has reached day seven, she points him towards Nine Mile Creek. He hears a short, warm line in her usual speech bubble. The next journey grows out of a week of island life.

The [merged canon](../vision-and-pillars.md) and [ADR 0062](../adr/0062-the-first-week-is-closed-by-weather.md) settle the speaker, following-morning weather and demo ending at her word. They are requirements, not alternative outcomes here. First eligible ordinary **evening** talk, on about day seven or later, is the proposed encounter staging; guaranteeing a meeting on day seven remains an encounter choice. A missed conversation stays available on later evenings. The sea and tide still need reading when the way opens in the full game. The demo ends at her word, permits continued island fishing and keeps every outward route closed permanently.

This is copy and encounter design only. No dialogue asset, TASKS string, routine, weather, scene or save data changes in this document's change. Evidence is pinned main `f1089fe61462a7d1cc2a1366c6f918ef940cb66b`; the five-fact/key contract is PR 1511 at `2a0d80e43b37c5444d81cb2c31ecab825c235594`. Its provenance correction is source-checked with authored regressions; current-head execution is not yet verified. The first week stays entirely on St Peters: Basil sells the broken dory at the island slip, the player mends her by hand with materials bought at the St Peters store, and boat hardware remains exclusive to Nine Mile Creek.

## The speaker and where to find her

Ginny already frames work as something the player earns, fronts his shellfish-licence fee, and sends him off with affection and practical caution. Her [opening dialogue](../../Assets/_Project/Data/NPCs/Dialogue/GinnyOpening.asset) uses “child”, brief instructions and reminders about the tide. Her [gift lines](../../Assets/_Project/Data/NPCs/AuntGinny.asset) bring the conversation back to supper and home. Those are the voice references for the new line. The opening asset's early mainland trip and paid mainland repair are old route instructions; they are not directions to repeat in this island week.

Basil is the stronger specialist weather reader. His [first dialogue](../../Assets/_Project/Data/NPCs/Dialogue/BasilFirst.asset) recalls forty-one years working the channel and tells the player to read the sea for himself. His [gift lines](../../Assets/_Project/Data/NPCs/BasilSamson.asset) are spare and appreciative. His daily weather conversation is already separate work in PR 1498. Keep him as the person the player can consult about conditions, while Ginny marks the change from staying with her to making a longer journey. Her line does not claim that a particular wind direction, sea state or forecast is safe.

The routines give a concrete meeting plan:

| Resident | Authored departure and destination windows | What the location means |
|---|---|---|
| Ginny, `routine.aunt_ginny` | 04:30–17:00: `station.st_peters.ginny_village_mark`; 17:00–20:00: `station.st_peters.cottage_garden`; 20:00–04:30: `station.st_peters.cottage_step` | Her daytime mark is in the village, where the opening conversation happens. The garden and step are at her own cottage on the eastern plot. |
| Basil, `routine.basil_samson` | 05:00–12:00: `station.st_peters.wharf_head`; 12:00–15:00: `station.st_peters.slip_head`; 15:00–18:00: `station.st_peters.wharf_head`; 18:00–05:00: `station.st_peters.home_sage_b` | He spends the working day between the outer wharf and shoreward end of the slip, then goes home. |

Sources: [Ginny's routine](../../Assets/_Project/Data/Routines/AuntGinnyRoutine.asset), [Basil's routine](../../Assets/_Project/Data/Routines/BasilSamsonRoutine.asset), and the station entries in [the committed island scene](../../Assets/_Project/Scenes/StPeters.unity). These are departure windows, not exact arrival appointments. Ginny has ±3 minutes of schedule jitter and walks at 1 m/s; Basil has ±8 minutes and walks at 0.95 m/s. Neither asset supplies a special seventh-day or weekly replacement schedule. The existing [route audit](../world/st-peters-routine-routes-audit.md) explains the journeys; its older geometry measurements are not a new movement verification.

**Who is the player sure to meet on the day? Main guarantees no meeting with either islander.** It supplies their destinations, not a mandatory day-seven encounter. Staying with Ginny does not mean she is at the cottage when he wakes: her ordinary departure is 04:30. It would be inaccurate to promise a breakfast-doorway scene from these assets.

The proposed evening contact is **Ginny at her cottage garden or step**, subject to her actual arrival. During a daytime village conversation, if all five facts hold, she asks him to return that evening; she does not give the word early. “About day seven” means the first eligible ordinary evening talk, not a single appointment he can miss. A later build must author its evening window in dialogue/encounter data; the 17:00 garden departure is a staging reference, not a hidden time constant or arrival guarantee. If the experience must guarantee a day-seven meeting, it needs a separately authored encounter. Neither this page nor the existing routine supplies it.

## What must be true before she gives her word

All five facts must hold together when the gate conversation is offered. Their order below is the evaluator's missing-note order, not a required order of play.

| Fact | Source | What does not satisfy it |
|---|---|---|
| Dory mended | Repaired boats contains the configured `boat.dory` | Buying her without finishing the repair |
| Cod licence held | Owned licences contains `license.cod` | The separate `license.clam` |
| A fish kept from the dory | `st_peters.fact.fish_from_dory`, recorded only from a successful licensed landing with the configured dory occupied and already repaired | A release, shellfish, shore/pier catch carried aboard later, another boat's catch, or a catch made before the dory was mended |
| Shellfish sold | `st_peters.fact.shellfish_sold` | Digging, gifting, dumping, or having shellfish in the pail without selling any |
| Day seven reached | Restore the Core clock normally, then pass `IGameClock.DayIndex` to `Read(save, dayIndex)`; index 6 begins day seven with the authored threshold 7 | Day six; a stale saved day cache |

The day rule means **the start of the seventh day**, not seven completed days. The historical catch and sale facts need one qualifying event each; neither asks for a full hold, a full pail, a particular price or a repeated daily quota. A clam sold before he buys the dory still counts. The intended week must make every fact achievable on the island, but main still lacks the island cod seller and the proposed hand-repair route described below. This is not a claim that the full sequence is playable now. The read-only evaluator does not grant passage by itself. Its current `Read(SaveData, int dayIndex)` contract accepts no seconds/day-length conversion or `ReadSaved` call; use the restored/live Core clock, not a saved cache or a season day.

The corrected fact producer captures origin at the fresh landing, independently of the notebook's bounded handoff history. The old notebook can still recount a forgotten catch; that count cannot earn this flag. The correction includes shore, pier, other-hull, repaired-dory and pre-repair cases, including actual place/lift/stow history beyond the notebook bound. These are authored tests awaiting execution, not a verified playthrough. The predicate still uses generic `boat.dory` identity and the existing repaired list; it does not yet distinguish the purchased physical hull or paid versus hand repair. No historical false flag is silently cleared.

## Evening word and morning passage contract

ADR 0062 compresses conversation completion and the following-morning result into one paragraph. A later build must separate them. PR 1494's `CampaignBarrierDirector.Stands` reads `LiftedByFlag` immediately; its four island edges name `st_peters.weather_turned`. Writing that flag in the dialogue completion callback would lift the weather **that evening** and violate the rule. The barrier must consume only passage-open state, never eligibility or word-completed state. Surf and sea-edge consumers must agree, and the world wind, tide and sea simulation are never rewritten.

The following names are the proposed writer contract, not registered flags or a shipped coordinator:

| Moment | Sole writer in a later build | State change | What the player sees |
|---|---|---|---|
| Five facts true before evening, word not yet completed | Conversation selector reads a fresh `AllHold` snapshot | No completion or weather flag write | An invitation to return that evening; weather still closes the routes |
| Eligible evening line finishes | Dialogue completion adapter asks the island gate coordinator to complete the word, rechecking `AllHold` and the evening window | Set `st_peters.ginny_word_completed` once; in the full game capture the promised morning in that same transaction, while the demo owes no passage date; leave `st_peters.weather_turned` false | Her word and, in the demo, its closing beat; weather remains |
| Player cancels before completion | Dialogue adapter discards the incomplete turn | No word or weather write; no pending morning | He can seek her again on an eligible evening |
| Actual sleep ends after reaching a qualifying morning, full game only | Sleep completion adapter asks the same coordinator to advance pending passage before the wake save is captured | Set `st_peters.weather_turned` once; preserve `st_peters.ginny_word_completed`; pending becomes false | Island weather closure clears on waking; ordinary tide and sea conditions still apply |
| Repeat talk or a later sleep after passage opened | Coordinator reads completed state | No further writes and no relock | Normal conversation and open passage |
| Demo sleep, on any later day | Demo policy prevents passage completion | `st_peters.weather_turned` stays false even after her word | Continued island fishing; routes stay closed |

**Session snapshot:** a prototype may expose a plain `IslandGateWordSnapshot` with `WordCompleted`, `WordCompletedAtSeconds`, `DueMorningSeconds` and `PassageOpen`. In the full game, pending is derived from completed word and closed passage; the demo policy never reports pending passage. This names a proposed type, not an added class or saved field. On full-game word completion, compute `DueMorningSeconds = RestMorning.WakeSeconds(completedAt, config)` from the live Core clock and the existing `GameConfig.WakeHour`/`SecondsPerDay`. Do not invent another dawn hour, calendar counter or wall-clock input. The completion time and due morning are captured once, not recomputed on repeat talk; the demo records only word completion and owes no passage date.

**A qualifying sleep is an actual sleep after the word that reaches a morning.** Its start is at or after `WordCompletedAtSeconds`, its end is later than its start, and the wake clock is at least both `DueMorningSeconds` and `RestMorning.WakeSeconds(sleepStart, config)`. A short nap or early wake before that morning leaves passage pending. If the player stays awake past the promised morning, weather still closes the routes and Ginny's pending line directs him to sleep; elapsed time alone never opens them. A later sleep must reach its own following morning. There is no forced sleep, timeout, same-step opening or retroactive qualification from a sleep before her word.

**Integration boundary:** reuse the existing Core sleep flow, not a second clock or a dialogue timer. `SleepFlow.Wake` and the instant `RestSaveResponder.Fulfil` path both reach `RestSaveResponder.Keep` after advancing actual sleep time; the later adapter needs their actual start/end and outcome. `DayStarted` alone also occurs while awake, `SleepFlowChanged(Awake)` also follows abandonment, and `GameSaved` can report a save-only action, so none alone proves qualifying sleep. The chooser and instant paths must call the same completion contract before the wake save captures the gate state. Sleep that does not advance time, abandonment and failed/cancelled requests do not open passage. Save failure must remain visible and must not be presented as persisted progress.

**Persistence is a release dependency, not permission to change version 26.** This page adds no saved field, flag writer or save-format change. Until an accepted persistence design exists, all newly proposed word/pending state stays session-only; the table's saved-flag writers must not ship independently of that design. The existing boolean flag list can hold completion and weather-open keys, but it cannot by itself retain the authoritative completion time and promised morning. A later accepted save step must preserve those values together with completion, commit the morning transition with the wake save, and restore pending without guessing a day or restarting the wait. It must define interrupted writes, repeat callbacks and old-save behavior. Do not smuggle a date into a new saved field, unrelated row or dynamic flag key here. A full-game build cannot ship the morning promise if reload loses its timing.

For a future accepted persistence step: an old save with neither gate flag remains not completed and uses its real five facts; an existing true weather-open flag must not silently relock passage. Word-completed with weather closed but no recoverable due morning requires an explicit migration policy before release, never a guessed date or automatic opening. A session prototype can reset its unsaved snapshot on a new/load session, but must be labelled incomplete and must not claim the reload contract is met.

### Acceptance cases for that build

Extend the existing `OnboardingProgressTests`, `SleepFlowTests`, `BedBringsTheMorningTests` and campaign-barrier coverage when the adapters are implemented; no parallel eligibility or sleep evaluator is needed. These cases are specifications, not executed runtime tests:

| Case | Required exact result |
|---|---|
| All facts become true on day seven before evening | Both gate flags false; daytime return line; five facts unchanged |
| Missing fact, including a forgotten shore catch stowed aboard | Word cannot complete; first missing-fact line; weather flag false |
| Cancel the eligible evening line | Zero flag writes; no pending morning; line remains available |
| Finish the eligible evening line, full game | One word-completed write; fixed due morning; zero weather-open writes in that step |
| Remain awake through midnight and the due morning | Weather-open false; pending true; routes remain weather-closed; sleep reminder |
| Cancel sleep, abandon it, save without sleeping, or wake before morning | Zero weather-open writes; same pending completion and due morning |
| Sleep after the word through the following morning | Exactly one weather-open write at completion; pending false; both sea edges and surf release together |
| Miss the due morning awake, then take an afternoon nap | Weather-open remains false; only a later sleep that reaches its following morning qualifies |
| Repeat the wake callback or talk after passage opened | Zero additional flag writes; passage never relocks |
| Reload before word, during pending, or after morning completion | Accepted persistence restores exactly the corresponding state and original due morning; no guessed or reset wait |
| Complete the demo line, then sleep through any number of mornings | Closing beat once; weather-open always false; island fishing remains available |

## Three versions of Ginny's line

Choose one version, not a sequence of three. Each is short enough to be treated as one ordinary, player-paced speech turn, with the usual bubble layout still to be checked in the game. There is no HUD banner, checklist recital or new reward screen.

| Version | Proposed line |
|---|---|
| A — recommended | “You've found your feet here, child. Try Nine Mile Creek come morning. Mind the tide.” |
| B | “That little dory's ready for a longer errand. Nine Mile Creek in the morning, child. No racing the tide.” |
| C | “There's more coast waiting for you now. Nine Mile Creek come morning. Bring me back a story, child.” |

A is the clearest route instruction and leaves the achievement in the player's hands. B puts more weight on his repaired boat. C puts more weight on leaving home and coming back. None claims the whole sea is safe. All three are full-game evening lines and require the following-morning sleep contract above; immediate passage is not an alternative.

**Separate demo copy is required.** Suggested closing line: “You've found your feet here, child. There's plenty of fishing left on this shore. Come home for supper.” It marks her word without promising a mainland trip the demo cannot provide. The demo closing beat follows its completion once; the player may then keep fishing on St Peters. The full-game lines and their mainland promise must never be selected by the demo.

## The five missing TASKS notes

These are alternatives for the **same five keys** proposed in PR 1511. A is the recommended set; B and C are editorial choices, not random variants. Display a note only while its corresponding fact is missing, using the ordinary TASKS surface. Keep the authored key order when several are missing. The copy describes things the player can do in the world and adds no sixth fact.

| Text key | A — recommended | B | C |
|---|---|---|---|
| `tasks.st_peters.mend_dory` | Mend the dory at the slip | Put the dory right by hand | Finish the dory's repairs |
| `tasks.st_peters.cod_licence` | Get a cod licence on St Peters | Sign for my cod licence | Get a licence to keep cod |
| `tasks.st_peters.fish_from_dory` | Keep a fish from the dory | Land a kept fish from the dory | Fish from the dory and keep one |
| `tasks.st_peters.sell_shellfish` | Carry shellfish in to sell | Sell shellfish at the store | Sell a clam at the counter |
| `tasks.st_peters.first_week` | Give it a few more tides | See in my seventh island day | Live on St Peters to day seven |

All fifteen alternatives fit the existing 31-character note budget. This is a text-length check, not visual fit proof.

The cod-licence wording describes the intended island week. Main currently lists [the cod licence](../../Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset) at the mainland harbourmaster, `seller.nmc_harbourmaster`; the island seller must be supplied by its own content change. Do not point him off-island to satisfy this note. The repair note names the slip and hand work. Canon requires **basic repair materials bought at the St Peters store**, carried to the slip and used by hand; **boat hardware is sold in Nine Mile Creek only**. These are separate stock categories, not permission to move boat fittings onto island shelves. Basil guides the work or points him to the store; he does not replace it as the material supplier. Exact material identities, jobs, quantities and prices remain content decisions. The hand-mending proposal in #1508 and slip sale in #1539 must respect this source rule; proposed recipes do not establish available stock. No mainland repair fee is part of this first-dory route.

The shellfish note is already supported by the island's source data: the supplied starting shovel and pail (`gear.shovel`, `gear.bucket`; physical `tool.st_peters.shovel`, `container.st_peters.bucket`), the fronted fee, `license.clam` at `seller.leblancs`, and the `StPetersStore` buyer at `shop.st_peters_general`. The [shellfish licence](../../Assets/_Project/Data/Resources/Catalog/Licenses/ClamLicense.asset) costs 15 and the [configuration](../../Assets/_Project/Data/Config/GameConfig.asset) fronts 20. Digging without it is allowed, but the clam is released, so he needs it to keep a clam for sale. No separate selling licence is checked by [the sale service](../../Assets/_Project/Code/Economy/SellService.cs). Carrying and selling a single kept clam suffices; an unlisted shovel or pail purchase is not required.

## If he asks before the facts hold

Ginny answers the question naturally and gives one useful next step. Choose the first missing fact in the order above, so repeated talks give a stable answer while the state is unchanged. The other missing notes stay in TASKS. These are suggested conversation responses, not another rules evaluator.

| First missing fact | What he hears |
|---|---|
| Dory mended | “Finish putting that dory right, child. We're in no hurry off this island.” |
| Cod licence held | “Get your cod licence sorted, child. Then you can keep a cod for your trouble.” |
| Fish kept from the dory | “Have a fish from that dory you can keep, child. You'll know her better for it.” |
| Shellfish sold | “Sell a clam at the store, child. You'll need your shellfish licence to keep one.” |
| Day seven reached | “Stay a little yet, child. There's time to learn this shore before you take on the next.” |

An early question changes no fact and promises no immediate passage. No spoken checklist, countdown or map wall is needed. When all five facts hold during the day, she says “Come by this evening, child. We'll talk then.” Once the full-game word has been heard, a repeat question while passage is pending receives “Get some sleep, child. Mind the tide when you go.” That reminder remains truthful if he stays awake past the due morning. It belongs only to real pending state, never to a guessed calendar date or the permanently closed demo. Sleeping through the first six days is not demanded; the following-morning passage transition requires the later sleep described above.

## Decisions reserved for the owner

| Question | Lean and consequence |
|---|---|
| First eligible ordinary evening talk, even after day seven, or a guaranteed seventh-day meeting? | **First eligible evening conversation**, available on later evenings too. A guaranteed meeting requires separate encounter staging; neither current routine guarantees it. The evening window must be authored as data. |
| Which full-game line and note set should ship? | **A**, with the separate demo closing line; copy and bubble layout still need acceptance. |
| Who issues the island cod licence, and which exact basic repair materials, jobs and prices are authored? | **A familiar island counter for paperwork; store-bought consumable repair materials with Basil guiding the work.** The store source and mainland-only boat hardware are fixed. Seller identity, individual stock rows and prices remain unsettled. |
| How will the word's completion and due morning survive reload within the save constraints? | **Agree the authoritative representation and atomic handoff before shipping.** The session snapshot and acceptance cases above specify the behavior without adding a saved field or choosing an unapproved persistence encoding. |

Ginny is the settled speaker. The next-morning weather after sleep, demo ending at her word with permanently closed routes, store-sourced repair materials, island-only week, hand repairs, licence release rule and weather-only closure are not reopened by these choices. Basil remains the dory seller, practical guide and separate weather reader.

## Evidence and limits

Sources are the two NPC assets, both dialogue assets, both complete routine assets, bounded serialized scene station entries, the shellfish licence and sale path, the Core clock and sleep contracts, the merged canon, ADR 0062 and the five keys on PR 1511. PR 1498's weather conversation and PR 1494's immediate flag consumer remain separate work. The old opening text and currently available island equipment are distinguished from the intended complete week throughout.

The static checks cover all five key spellings against PR 1511 at `2a0d80e43b37c5444d81cb2c31ecab825c235594`, fifteen note lengths, twelve conversation-line mappings, relative source links, settled decision status, material sourcing and the two-step writer/consumer contract. No Unity, bubble-layout check, dialogue playthrough, route simulation, save/reload handoff test or weather-transition verification was run. The work delivers a design page; none of those future behaviors is claimed to ship with it.

## Conversation line, text key and prerequisite

These are proposed text keys, not registered dialogue assets or saved flags. Only one of A/B/C is chosen for the full game. All three require a fresh `AllHold` reading, the authored evening window and word not already completed; recheck at completion. They require the promised next-morning handoff before shipping; **none is suitable as the permanently closed demo's ending**. The demo selects only its separate closing copy. Early answers apply only before word completion and select the first missing fact in the authored order. The daytime invitation and pending-sleep reminder have separate prerequisites. Evening stages the encounter; it does not become a sixth historical fact.

| Conversation line | Proposed text key | Fact or state required |
|---|---|---|
| You've found your feet here, child. Try Nine Mile Creek come morning. Mind the tide. | `dialogue.st_peters.ginny_gate.word_a` | All five facts hold; evening; full game; word not yet completed. |
| That little dory's ready for a longer errand. Nine Mile Creek in the morning, child. No racing the tide. | `dialogue.st_peters.ginny_gate.word_b` | All five facts hold; evening; full game; word not yet completed. |
| There's more coast waiting for you now. Nine Mile Creek come morning. Bring me back a story, child. | `dialogue.st_peters.ginny_gate.word_c` | All five facts hold; evening; full game; word not yet completed. |
| You've found your feet here, child. There's plenty of fishing left on this shore. Come home for supper. | `dialogue.st_peters.ginny_gate.demo_word` | All five facts hold; evening; demo; word not yet completed; weather remains closed permanently. |
| Come by this evening, child. We'll talk then. | `dialogue.st_peters.ginny_gate.return_evening` | All five facts hold; before evening; word not yet completed; either build. |
| Finish putting that dory right, child. We're in no hurry off this island. | `dialogue.st_peters.ginny_gate.mend_dory` | DoryMended is missing. |
| Get your cod licence sorted, child. Then you can keep a cod for your trouble. | `dialogue.st_peters.ginny_gate.cod_licence` | DoryMended holds; CodLicence is missing. |
| Have a fish from that dory you can keep, child. You'll know her better for it. | `dialogue.st_peters.ginny_gate.fish_from_dory` | DoryMended and CodLicence hold; FishFromDory is missing. |
| Sell a clam at the store, child. You'll need your shellfish licence to keep one. | `dialogue.st_peters.ginny_gate.sell_shellfish` | The first three facts hold; ShellfishSold is missing. |
| Stay a little yet, child. There's time to learn this shore before you take on the next. | `dialogue.st_peters.ginny_gate.first_week` | All four practical facts hold; DayReached is missing (day index below 6 with the current threshold). |
| Get some sleep, child. Mind the tide when you go. | `dialogue.st_peters.ginny_gate.pending_morning` | Full game; word completed; passage pending because no qualifying sleep has completed, including after staying awake past the due morning. |
| There's plenty of fishing here yet, child. Come home for supper. | `dialogue.st_peters.ginny_gate.demo_after_word` | Demo; word already completed; no repeated closing beat; no mainland promise. |
